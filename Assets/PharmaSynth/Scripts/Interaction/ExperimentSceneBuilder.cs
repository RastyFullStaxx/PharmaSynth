using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using TeleAnchor = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor;
using TeleArea = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea;

/// Spawns an experiment's physical setup (stations, grabbable props, reagent vessels)
/// from its ExperimentLayout when a module loads — so all 9 experiments live in one
/// lab scene. The hand-built Methane objects stay as a grouped stage that is simply
/// toggled; every other experiment is built into a DynamicStage that is cleared and
/// rebuilt on each module change.
public partial class ExperimentSceneBuilder : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private SceneAssetLibrary assets;
    [SerializeField] private ReactionRegistry registry;
    [SerializeField] private List<ExperimentLayout> layouts = new List<ExperimentLayout>();
    [SerializeField] private GameObject methaneStage;   // existing hand-built Methane objects
    [SerializeField] private Transform labelsRoot;      // WorldLabels
    [SerializeField] private string methaneModuleId = "tutorial-methane";

    private Transform _stage;

    public void SetRefs(ExperimentRunner r, SceneAssetLibrary a, ReactionRegistry reg, List<ExperimentLayout> ls)
    { runner = r; assets = a; registry = reg; if (ls != null) layouts = ls; }

    public ExperimentLayout FindLayout(string moduleId)
    {
        foreach (var l in layouts) if (l != null && l.moduleId == moduleId) return l;
        return null;
    }

    /// Hook for ExperimentLauncher.onModuleLoaded.
    public void OnModuleLoaded(ExperimentModuleDefinition m) { _module = m; if (m != null) Build(m.moduleId); }

    /// The definition behind the current build, when one was handed over (null for the
    /// simulators' id-only Build calls) — read for a claimed role's task LABEL.
    private ExperimentModuleDefinition _module;

    /// The stage root. ADOPTS an existing "DynamicStage" child before making one:
    /// `_stage` is a plain field, so it is null after every domain reload, and
    /// creating a fresh one would orphan the first
    /// with the previous build's objects still in it, since Build() clears
    /// only the new one and the old contents would stay
    /// on the bench. Duplicates are collapsed here.
    private Transform Stage()
    {
        if (_stage != null) return _stage;

        Transform found = null;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var c = transform.GetChild(i);
            if (c.name != "DynamicStage") continue;
            if (found == null) found = c;
            else Kill(c.gameObject);        // collapse an earlier orphaned stage
        }
        if (found == null)
        {
            var go = new GameObject("DynamicStage");
            go.transform.SetParent(transform, false);
            found = go.transform;
        }
        _stage = found;
        return _stage;
    }

    private static void Kill(GameObject go)
    {
        if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
    }

    private static void Kill(Component c)
    {
        if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
    }

    /// Build the setup for a module. Returns the number of spawned root objects.
    /// Public + returns count so edit-mode self-tests can verify it.
    /// The module currently being built — vessel wiring that needs the module's
    /// identity (the vapor stream's product chemical) reads it here.
    private string moduleBeingBuilt = "";

    public int Build(string moduleId)
    {
        moduleBeingBuilt = moduleId;
        var stage = Stage();
        for (int i = stage.childCount - 1; i >= 0; i--) Kill(stage.GetChild(i).gameObject);
        // Bench-bound vessels wire task logic onto PERMANENT objects, which the line
        // above cannot reach — strip it or the last module's bindings complete this
        // one's steps. Must run every build, before anything is wired.
        ClearBenchBindings();
        // Clear the dynamic labels we spawned last time.
        if (labelsRoot != null)
        {
            var dead = new List<GameObject>();
            foreach (Transform t in labelsRoot) if (t.name.StartsWith("DynLabel_")) dead.Add(t.gameObject);
            foreach (var d in dead) Kill(d);
        }

        bool methane = moduleId == methaneModuleId;
        // Methane stage visibility is owned by MethaneStageVisibility (shows
        // during Lab Tour + the Methane attempt only) — the builder does not
        // toggles it, so the two never fight.
        // Demo sessions spawn no ready-made vial here;
        // the finished products live on the ReagentShelf and are
        // revealed there by EndProductVisibility while a demo session is active.
        if (methane) return 0;                      // Methane uses its hand-built stage

        var layout = FindLayout(moduleId);
        if (layout == null) { Debug.LogWarning("[SceneBuilder] no layout for " + moduleId); return 0; }

        int n = 0;
        _currentLayout = layout;
        foreach (var s in layout.stations) { BuildStation(stage, s); n++; }
        foreach (var p in layout.props)    { BuildProp(stage, p); n++; }
        _rackBindings.Clear();
        foreach (var v in layout.vessels)  { BuildVessel(stage, v); n++; }
        WireRackGroups(stage, layout);        // "all five tubes" steps — needs vessels spawned
        WireVerbControllers(stage, layout);   // stir/grind/burner-gate — needs props+vessels spawned
        // ⛔ Never stage bench items here (extra racks, tubes, spare glass, a striker cube).
        // The permanent bench already carries all of it (Kit_TestTube_0-18, Eq_Beaker_100mL/
        // 500mL, ErlenmeyerFlask, racks, Raw_Matchsticks; the matchbox is the striker), and
        // re-spawning duplicates would undo hand edits to the bench on every module load.
        return n;
    }

    /// A fixed test-tube rack near the vessels, pre-filled with 6 grabbable
    /// tubes (full receiver treatment; each tube's DropRespawn home = its slot,
    /// so the rack refills itself when tubes are abandoned).
    private void SpawnRackKit(Transform stage)
    {
        var prefab = assets != null ? assets.GetPrefab("TestTubeRack") : null;
        if (prefab == null) return;
        var rack = Instantiate(prefab, stage);
        rack.name = "RackKit";
        Normalise(rack, "TestTubeRack", 0.18f);
        Seat(rack.transform, LayoutTidyMath.RackPos);
        var grab = rack.GetComponent<XRGrab>(); if (grab != null) Kill(grab);
        var rrb = rack.GetComponent<Rigidbody>(); if (rrb != null) Kill(rrb);

        var b = WB(rack);
        var tubePrefab = assets.GetPrefab("TestTube");
        if (tubePrefab == null) return;
        for (int i = 0; i < 6; i++)
        {
            int col = i % 3, row = i / 3;
            var pos = new Vector3(
                Mathf.Lerp(b.min.x + 0.03f, b.max.x - 0.03f, col / 2f),
                b.max.y + 0.02f,
                Mathf.Lerp(b.min.z + 0.02f, b.max.z - 0.02f, row));
            SpawnReceiver(stage, tubePrefab, "TestTube", "RackTube_" + i, "Test Tube", pos, 0.15f);
        }
    }

    /// Spare vital glassware on the free front strip (enough duplicates
    /// of vital items like beakers).
    private void SpawnSpares(Transform stage)
    {
        if (assets == null) return;
        var specs = new (string prefab, string label)[]
        {
            ("Beaker_100mL", "Spare Beaker"),
            ("Beaker_100mL", "Spare Beaker"),
            ("ErlenmeyerFlask_400mL", "Spare Flask"),
        };
        for (int i = 0; i < specs.Length; i++)
        {
            var prefab = assets.GetPrefab(specs[i].prefab);
            if (prefab == null) continue;
            SpawnReceiver(stage, prefab, specs[i].prefab, "Spare_" + specs[i].prefab + "_" + i,
                          specs[i].label, LayoutTidyMath.SparePos(i), 0.14f);
        }
    }

    /// A grabbable empty receiver vessel with the full vessel treatment.
    private void SpawnReceiver(Transform stage, GameObject prefab, string prefabName, string name,
                               string label, Vector3 pos, float targetHeight)
    {
        var swap = VesselPrefabFor(prefabName);
        var inst = Instantiate(swap != null ? swap : prefab, stage);
        inst.name = name;
        Normalise(inst, prefabName, targetHeight);
        Seat(inst.transform, pos);
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = name; item.displayName = label;
        var rb = PhysicsProfiles.EnsurePhysics(inst, prefabName);
        inst.AddComponent<GrabPhysicsPolicy>();
        GrabTuning.Apply(inst.GetComponent<XRGrab>());
        inst.AddComponent<HoverHighlight>().Bind(inst.GetComponent<XRGrab>());
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        if (Mishandling.IsBreakable(prefabName))
        {
            var breakable = inst.AddComponent<BreakableGlassware>();
            breakable.Bind(runner, respawn, rb, label);
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(prefabName), Mishandling.DefaultBreakSpeed);
        }
        else inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(prefabName));
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        lp.SetContents(null, 0f);
        EnsureLiquidVisual(inst, lp);
        inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);
        inst.AddComponent<CleanableVessel>().Bind(lp);   // used vessels get dirty
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(label, 1.6f);
        inst.AddComponent<VesselStatus>().Bind(lp, pl, label, 1.6f);
        inst.AddComponent<MixFeedback>().Bind(lp);
    }

    /// Heat experiments stage two ready matchsticks + a striker block on the
    /// table (the cabinet dispenser remains the endless source). Cloned from
    /// the dispenser's hidden template when it exists; skipped silently if not.
    private void StageConsumables(Transform stage, ExperimentLayout layout)
    {
        bool hasHeat = false;
        foreach (var s in layout.stations) if (s.sim == StationSim.Heat) hasHeat = true;
        if (!hasHeat) return;

        // Striker block (always useful next to the matches).
        var striker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        striker.name = "MatchStriker";
        striker.transform.SetParent(stage, false);
        striker.transform.localScale = new Vector3(0.09f, 0.02f, 0.06f);
        Seat(striker.transform, LayoutTidyMath.StrikerPos);
        striker.AddComponent<MatchStrikerSurface>();
        var spl = striker.AddComponent<ProximityLabel>(); spl.SetLabel("Striker", 1.2f);

        GameObject template = null;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == "Template_Raw_Matchsticks") { template = t.gameObject; break; }
        if (template == null) return;
        for (int i = 0; i < 2; i++)
        {
            var match = Instantiate(template, stage);
            match.name = "StagedMatch_" + i;
            match.SetActive(true);
            Seat(match.transform, LayoutTidyMath.MatchPos(i));
            var respawn = match.GetComponent<DropRespawn>() ?? match.AddComponent<DropRespawn>();
            respawn.Bind(match.GetComponent<Rigidbody>(), match.GetComponent<XRGrab>());
            respawn.SetHome(match.transform.position, match.transform.rotation);
        }
    }

    private ExperimentLayout _currentLayout;

    /// Post-pass: wire the tool verbs that span a station + a prop + a
    /// vessel — the rod stirs the bindings vessel, the pestle grinds the mortar,
    /// and a Heat station whose required prop is a burner only heats while LIT.
    private void WireVerbControllers(Transform stage, ExperimentLayout layout)
    {
        foreach (var s in layout.stations)
        {
            if (s.sim == StationSim.Heat)
            {
                var prop = FindLayoutProp(layout, s.requiredItemId);
                if (prop == null || (prop.prefabName != "BunsenBurner" && prop.prefabName != "AlcoholBurner")) continue;
                var propGo = FindStageChild(stage, "Prop_" + s.requiredItemId);
                var padGo = FindStageChild(stage, "Station_" + s.taskId);
                if (propGo == null || padGo == null) continue;
                var burner = propGo.GetComponent<BurnerController>() ?? propGo.AddComponent<BurnerController>();
                var rig = padGo.GetComponent<ZoneSimStation>();
                if (rig != null) rig.SetIgnitionGate(() => burner != null && burner.IsLit);
                var status = padGo.GetComponent<StationStatusLabel>();
                if (status != null) status.SetIgnitionHint(() => burner != null && burner.IsLit);
                // The burner base doubles as a match striker.
                if (propGo.GetComponent<MatchStrikerSurface>() == null) propGo.AddComponent<MatchStrikerSurface>();
            }
            else if (s.sim == StationSim.Stir)
            {
                var rodGo = FindStageChild(stage, "Prop_" + s.requiredItemId);
                var vesselLp = FirstBindingsVessel(stage);
                if (rodGo == null || vesselLp == null) continue;
                var stir = vesselLp.GetComponent<StirController>() ?? vesselLp.gameObject.AddComponent<StirController>();
                stir.Bind(runner, s.taskId, vesselLp, rodGo.transform);
            }
            else if (s.sim == StationSim.Grind)
            {
                WireGrind(stage, s.taskId);
            }
        }

        // A staged mortar+pestle with no Grind station still grinds (educational).
        bool hasGrindStation = false;
        foreach (var s in layout.stations) if (s.sim == StationSim.Grind) hasGrindStation = true;
        if (!hasGrindStation) WireGrind(stage, null);
    }

    private void WireGrind(Transform stage, string taskId)
    {
        GameObject mortar = null, pestle = null;
        foreach (Transform t in stage)
        {
            if (t.name.StartsWith("Prop_"))
            {
                var li = t.GetComponent<LabItem>();
                string dn = li != null ? (li.displayName ?? "") : "";
                if (mortar == null && (dn.Contains("Mortar") || t.name.Contains("Motar") || dn.Contains("Motar"))) mortar = t.gameObject;
                if (pestle == null && dn.Contains("Pestle")) pestle = t.gameObject;
            }
        }
        if (mortar == null || pestle == null) return;
        var grind = mortar.GetComponent<GrindController>() ?? mortar.AddComponent<GrindController>();
        grind.Bind(runner, taskId, pestle.transform);
    }

    private static ExperimentLayout.Prop FindLayoutProp(ExperimentLayout layout, string itemId)
    {
        if (string.IsNullOrEmpty(itemId)) return null;
        foreach (var p in layout.props) if (p.itemId == itemId) return p;
        return null;
    }

    private static GameObject FindStageChild(Transform stage, string name)
    {
        foreach (Transform t in stage) if (t.name == name) return t.gameObject;
        return null;
    }

    private LiquidPhysics FirstBindingsVessel(Transform stage)
    {
        foreach (Transform t in stage)
        {
            if (!t.name.StartsWith("Vessel_")) continue;
            var bind = t.GetComponent<LiquidTaskBinding>();
            if (bind != null && bind.ExpectedSteps.Count > 0) return t.GetComponent<LiquidPhysics>();
        }
        return null;
    }

    private static Material _socketGhostMat;
    /// Vessels of one rackGroup, collected during BuildVessel.
    private readonly Dictionary<string, List<LiquidTaskBinding>> _rackBindings
        = new Dictionary<string, List<LiquidTaskBinding>>();

    /// One family's interchangeable glassware: the module's roles for that kind, in role
    /// order, and every bench object that may play one of them.
    private class GlasswarePool
    {
        public readonly List<ExperimentLayout.Vessel> RoleVessels = new List<ExperimentLayout.Vessel>();
        public readonly List<LiquidTaskBinding> Members = new List<LiquidTaskBinding>();
    }
}
