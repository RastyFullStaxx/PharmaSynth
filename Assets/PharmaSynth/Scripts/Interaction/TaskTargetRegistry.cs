using System.Collections.Generic;
using UnityEngine;

/// What role an object plays in a step — drives the guidance tint and whether
/// grabbing it should silence the glow.
public enum TargetRole { Source, Destination, Tool, Station }

/// One highlightable object for one task.
public struct TaskTarget
{
    public Transform transform;
    public TargetRole role;

    /// Source bottles go quiet once in hand (you got the message). Destinations
    /// and tools stay lit while held — "this is the right tube" is still the
    /// answer while you're carrying it.
    public bool stayLitWhenHeld;

    /// The motion this step is asking for, for VerbDemoPlayer. Derived from
    /// the component that registered the target, so it cannot disagree with the
    /// binding for the same reason the target itself cannot.
    public VerbKind verb;
    /// One of several interchangeable tubes that could ALL take this step. Pool
    /// targets are collapsed to ONE at read time — the tube in the player's hand, else
    /// the nearest — so the arrow, the beacon and the glow never disagree, and the whole
    /// rack does not light up.
    public bool pool;
}

/// taskId → the scene objects that step involves.
///
/// Fed by the TutorialTargets sweep, the single source of truth (no module stages an
/// ExperimentTaskStation, so station self-registration would find nothing).
/// Components do not self-register, so there
/// is no second lifetime to keep in sync.
public static class TaskTargetRegistry
{
    private static readonly Dictionary<string, List<TaskTarget>> _map =
        new Dictionary<string, List<TaskTarget>>();

    public static void Register(string taskId, Transform t, TargetRole role, bool stayLitWhenHeld,
                                VerbKind verb = VerbKind.Place, bool pool = false)
    {
        if (string.IsNullOrEmpty(taskId) || t == null) return;
        if (!_map.TryGetValue(taskId, out var list))
        {
            list = new List<TaskTarget>();
            _map[taskId] = list;
        }
        for (int i = 0; i < list.Count; i++)
            if (list[i].transform == t) return;              // idempotent: sweeps may overlap
        list.Add(new TaskTarget { transform = t, role = role, stayLitWhenHeld = stayLitWhenHeld, verb = verb, pool = pool });
    }

    /// The ONE pool member a cue should use right now: the member the player is
    /// HOLDING if any is, else the nearest to `from`. Pure over the target list so the
    /// suite can pin the preference order without a scene. Returns null when no target is
    /// pool-flagged, so non-pool steps are untouched.
    public static Transform ChoosePoolMember(IReadOnlyList<TaskTarget> targets,
                                             System.Func<Transform, bool> isHeld, Vector3 from)
    {
        if (targets == null) return null;
        Transform nearest = null; float best = float.MaxValue; bool anyPool = false;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (!t.pool || t.transform == null) continue;
            anyPool = true;
            if (isHeld != null && isHeld(t.transform)) return t.transform;   // in hand wins outright
            float d = (t.transform.position - from).sqrMagnitude;
            if (d < best) { best = d; nearest = t.transform; }
        }
        return anyPool ? nearest : null;
    }

    /// Position-aware pick: the same Source-first order as before, but a pool of
    /// interchangeable tubes collapses to the held-or-nearest member instead of whichever
    /// was registered first — which is what "the guide follows the tube I picked up" means.
    public static Transform PickTarget(string taskId, Vector3 from)
    {
        var targets = Targets(taskId);
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].role == TargetRole.Source && targets[i].transform != null
                && !TutorialHighlighter.IsHeld(targets[i].transform))
                return targets[i].transform;
        var member = ChoosePoolMember(targets, TutorialHighlighter.IsHeld, from);
        if (member != null) return member;
        for (int i = 0; i < targets.Count; i++)
            if (targets[i].transform != null && !targets[i].pool) return targets[i].transform;
        return null;
    }

    /// Live targets for a step. Null transforms are filtered on every READ, not just
    /// on build — a vessel can break and be destroyed mid-run.
    public static IReadOnlyList<TaskTarget> Targets(string taskId)
    {
        if (string.IsNullOrEmpty(taskId) || !_map.TryGetValue(taskId, out var list))
            return System.Array.Empty<TaskTarget>();
        list.RemoveAll(e => e.transform == null);
        return list;
    }

    /// The ONE object a navigation cue should point at for this step.
    ///
    /// Source first ("go fetch that"), hopping to the destination once the source is in
    /// hand ("now put it here"). Extracted from WaypointGuide so the floor path
    /// and the beacon cannot pick different targets — two cues disagreeing about where to
    /// go is worse than either one alone.
    public static Transform PickTarget(string taskId)
    {
        var cam = Camera.main;
        return PickTarget(taskId, cam != null ? cam.transform.position : Vector3.zero);
    }

    public static int TaskCount => _map.Count;

    public static void Clear() => _map.Clear();
}

/// Builds the taskId → objects map by sweeping the live scene once per run.
///
/// Deliberately DERIVED from the components that actually complete each task rather
/// than from an authored per-task list. An authored list can drift from the binding
/// it describes — the bug class of
/// (a hint whose ACTION line contradicted the binding it had to satisfy). Deriving
/// makes that disagreement structurally impossible: if the sweep points at it, it is
/// the thing that completes the step.
public static class TutorialTargets
{
    /// Tasks in the last audited module that nothing in the scene claimed.
    public static readonly List<string> LastUnresolved = new List<string>();

    public static void Build()
    {
        TaskTargetRegistry.Clear();

        // --- Pours and scoops. The binding lives on the DESTINATION vessel and names
        //     the SOURCE chemical, so one entry yields both ends of the step.
        //     ScoopController deliberately has no taskId of its own: it completes via
        //     AddLiquid on the target vessel, so it is covered here rather than below.
        var vessels = Object.FindObjectsByType<LiquidPhysics>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var b in Object.FindObjectsByType<LiquidTaskBinding>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (b == null) continue;

            // A POOL member that has not committed to a role is a valid destination for
            // every task any of its surviving roles could serve. Registered with
            // pool=true so the consumers collapse the set to held-or-nearest.
            if (b.IsPoolMember)
                foreach (var task in b.CandidateTasks())
                    TaskTargetRegistry.Register(task, b.transform, TargetRole.Destination, true, VerbKind.Pour, pool: true);

            var steps = b.ExpectedSteps;
            if (steps == null) continue;
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                if (step == null || string.IsNullOrEmpty(step.taskId)) continue;
                // A solid reagent is SCOOPED, a liquid is POURED — the same distinction
                // the player has to make, read off the chemical itself rather than
                // authored anywhere.
                var verb = step.reagent != null && step.reagent.state == PhysicalState.Solid
                    ? VerbKind.Scoop : VerbKind.Pour;
                TaskTargetRegistry.Register(step.taskId, b.transform, TargetRole.Destination, true, verb);
                if (step.reagent == null) continue;
                foreach (var v in vessels)
                    if (v != null && Visible(v) && v.currentChemical == step.reagent && v.transform != b.transform)
                        TaskTargetRegistry.Register(step.taskId, v.transform, TargetRole.Source, false, verb);
            }
        }

        // --- Verb components: each already owns the taskId it satisfies.
        //
        //     The zone-free conversion put the taskId on a per-VESSEL
        //     companion (VesselHeatTask, VesselChillTask, …) rather than on the
        //     apparatus controller — the bath/bucket/strip are shared bench tools and
        //     cannot know whose step is running. So sweep the Vessel*Task components:
        //     they sit on the exact glassware the step is about, which is also what
        //     the player needs pointed out.
        //     The VerbKind is what the demonstration ghost mimes. Only grind and stir
        //     earn their own curve; everything else is "carry this vessel to that
        //     tool", which Place already shows correctly.
        RegisterVerb<GrindController>(c => c.TaskId, VerbKind.Grind);
        RegisterVerb<StirController>(c => c.TaskId, VerbKind.Stir);
        RegisterVerb<VesselHeatTask>(c => c.TaskId);
        RegisterVerb<VesselChillTask>(c => c.TaskId);
        RegisterVerb<VesselLitmusTask>(c => c.TaskId);
        RegisterVerb<VesselFlameTask>(c => c.TaskId);
        RegisterVerb<VesselWeighTask>(c => c.TaskId);
        RegisterVerb<VaporCollectController>(c => c.VaporTaskId);
        RegisterVerb<ZoneSimStation>(c => c.TaskId);
        RegisterVerb<RackTaskGroup>(c => c.TaskId);
        RegisterVerb<WeighStation>(c => c.TaskId);
        RegisterVerb<FermentationController>(c => c.FermentTaskId);
        RegisterVerb<ExperimentTaskStation>(c => c.TaskId);
        // (FermentationController.mustTaskId is deliberately NOT swept: it is a
        //  prerequisite the controller READS, not a step it completes — the juice
        //  pour's own LiquidTaskBinding already owns that target.)

        // --- The methane tutorial stages no dynamic vessels and hard-codes its four
        //     task ids inside the rig, so it cannot be swept generically. The rig
        //     publishes its own map (see MethaneApparatusRig.RegisterTutorialTargets)
        //     and we call it here, keeping ONE registry lifetime.
        foreach (var rig in Object.FindObjectsByType<MethaneApparatusRig>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rig != null) rig.RegisterTutorialTargets();

        // --- A station that demands a specific prop also points at the prop itself.
        //     NOTE: ZoneItemSensor is NOT a source here — it is a continuous-occupancy
        //     sensor for rigs (burner-in-zone, tube-in-place) and carries no taskId at
        //     all, so it cannot say which STEP a tool belongs to.
        var items = Object.FindObjectsByType<LabItem>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var st in Object.FindObjectsByType<ExperimentTaskStation>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (st == null || string.IsNullOrEmpty(st.TaskId) || string.IsNullOrEmpty(st.RequiredItemId)) continue;
            foreach (var it in items)
                if (it != null && Visible(it) && it.itemId == st.RequiredItemId)
                    TaskTargetRegistry.Register(st.TaskId, it.transform, TargetRole.Tool, true);
        }
    }

    /// Never point the player at something they cannot see.
    ///
    /// The sweep searches with FindObjectsInactive.Include on purpose — a stage's own
    /// objects can be momentarily inactive while it is being built — but some objects are
    /// switched off DELIBERATELY for the module being played: the module's own end product
    /// (`EndProductVisibility`, so you must synthesise it), the methane-only staged props
    /// (`MethaneStageVisibility`), and the consumable dispensers' hidden `Template_*` clone
    /// sources. Registering those would make Tutorial Mode glow, ghost and aim a waypoint at an
    /// invisible object (`Raw_Ethanol` during ethyl-alcohol, `Raw_BenzoicAcid` during
    /// benzoic-acid, `Raw_Acetone` during acetone, `Prop_reagent-jar` during acetone).
    ///
    /// Safe for coverage by construction: the pour branch registers the DESTINATION before
    /// it ever looks for a source, and the verb branch registers the station before the
    /// tool — so dropping a hidden source can never leave a step with nothing to point at.
    public static bool Visible(Component c)
        => c != null && c.gameObject.activeInHierarchy && !c.gameObject.name.StartsWith("Template_");

    private static void RegisterVerb<T>(System.Func<T, string> taskIdOf,
                                        VerbKind verb = VerbKind.Place) where T : Component
    {
        foreach (var c in Object.FindObjectsByType<T>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (c == null) continue;
            string id = taskIdOf(c);
            if (!string.IsNullOrEmpty(id))
                TaskTargetRegistry.Register(id, c.transform, TargetRole.Station, true, verb);
        }
    }

    /// Records which of a module's tasks nothing in the scene claimed. A wrap-up step
    /// (autoCompleteWhenOthersDone) legitimately has no physical target and is skipped;
    /// anything else is an authoring gap and should be surfaced loudly, because the
    /// player would be told to act with nothing to point at.
    public static void AuditAgainst(IEnumerable<ExperimentTask> tasks)
    {
        LastUnresolved.Clear();
        if (tasks == null) return;
        foreach (var t in tasks)
        {
            if (t == null || t.autoCompleteWhenOthersDone) continue;
            if (TaskTargetRegistry.Targets(t.taskId).Count == 0)
                LastUnresolved.Add(t.taskId);
        }
    }
}
