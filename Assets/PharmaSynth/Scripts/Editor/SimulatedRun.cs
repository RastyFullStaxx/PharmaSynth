#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Plays a module END-TO-END in edit mode, driving the REAL wiring the player
/// would. Not a replacement for the headset pass (it can't feel a grab or see a
/// misplaced visual), but it catches the
/// dead-on-arrival class before a human wastes VR time on it:
///   • a step no mechanism can complete (missing binding/station/rack wiring)
///   • thresholds that complete early or never (the accumulation contract)
///   • rack groups that never fire after every member is served
///   • a required chemical with NO source on the bench, or too little of it
///   • pours that land invisibly small in their vessel
///   • correct play being flagged as a mistake (mis-authored bindings)
///
/// HOW it simulates: the PLAYER PATH, not the plumbing.
/// builder.Build() wires the real scene, runner.StartExperiment() opens the real
/// graph, and every reagent is then TRANSFERRED the way a hand would: drawn out
/// of the actual bench source bottle (PourOut) and landed through
/// LiquidPhysics.AddLiquid in VERB-CONTRACT increments (1 ml a squeeze, 0.1 g a
/// spatula dip, 0.5 ml tilt-pour ticks WITH human overshoot). Completion may
/// only arrive through the real event chain: AddLiquid → LiquidAdded → binding →
/// CompleteTask, rack-group polls, ZoneSimStation sims. Calling
/// binding.HandleReagent directly bypasses the binding's subscription to its
/// vessel's events, so a binding that never subscribed (a hard-stuck player)
/// would still report CLEAN. Pours go through the bottle or they don't count.
public static class SimulatedRun
{
    public class Result
    {
        public int totalTasks, completedTasks, mistakes;
        public readonly List<string> bugs = new List<string>();      // would block or corrupt a run
        public readonly List<string> warnings = new List<string>();  // playable but wrong-feeling
        public bool Clean => bugs.Count == 0 && completedTasks == totalTasks && mistakes == 0;
    }

    [MenuItem("Tools/PharmaSynth/Simulate Run/Tutorial — Methane")]
    public static void SimMethane() => Menu("tutorial-methane");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Prelim 1 — Chemical Compounding")]
    public static void SimCompounding() => Menu("prelim-chemical-compounding");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Prelim 2 — Ethyl Alcohol")]
    public static void SimEthyl() => Menu("prelim-ethyl-alcohol");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Midterm 1 — Benzoic Acid")]
    public static void SimBenzoic() => Menu("midterm-benzoic-acid");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Midterm 2 — Acetanilide")]
    public static void SimAcetanilide() => Menu("midterm-acetanilide");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Midterm 3 — Acetone")]
    public static void SimAcetone() => Menu("midterm-acetone");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Midterm 4 — Chloroform")]
    public static void SimChloroform() => Menu("midterm-chloroform");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Final 1 — Benzamide")]
    public static void SimBenzamide() => Menu("final-benzamide");
    [MenuItem("Tools/PharmaSynth/Simulate Run/Final 2 — Wine Making")]
    public static void SimWine() => Menu("final-winemaking");

    static void Menu(string moduleId)
    {
        if (Application.isPlaying) { Debug.LogWarning("[SimRun] exit Play mode first."); return; }
        var log = new StringBuilder();
        var r = Run(moduleId, log);
        string path = "Logs/simrun-" + moduleId + ".txt";
        System.IO.File.WriteAllText(path, log.ToString());
        string verdict = r == null ? "COULD NOT RUN"
            : r.Clean ? "CLEAN — " + r.completedTasks + "/" + r.totalTasks + " tasks, 0 mistakes"
            : r.bugs.Count + " BUG(S), " + r.completedTasks + "/" + r.totalTasks + " tasks, "
              + r.mistakes + " mistakes, " + r.warnings.Count + " warning(s)";
        Debug.Log((r != null && r.Clean ? "<color=#4CD07D>" : "<color=#FF7A6B>")
                  + "[SimRun] " + moduleId + ": " + verdict + "</color>\n  full transcript → " + path);
    }

    /// Full simulated playthrough. Also callable from the suite (regression lock).
    public static Result Run(string moduleId, StringBuilder log)
    {
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        var module = lib != null ? lib.Get(moduleId) : null;
        if (builder == null || runner == null || module == null)
        {
            log.AppendLine("missing " + (builder == null ? "builder " : "")
                           + (runner == null ? "runner " : "") + (module == null ? "module" : ""));
            return null;
        }

        // The player path DRAINS real bottles and FILLS real tubes — snapshot
        // every vessel's contents first and restore after, or each edit-mode sim
        // permanently corrupts the saved scene's supplies.
        var snapshot = new List<(LiquidPhysics lp, ChemicalData chem, float ml, ChemicalData ppt, float pptMl)>();
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
            snapshot.Add((lp, lp.currentChemical, lp.currentLiquidVolume, lp.currentPptChemical, lp.currentPptVolume));

        // Real wiring, real graph — exactly what a campaign start does.
        builder.Build(moduleId);
        runner.SetModule(module);
        runner.StartExperiment();

        // ⛔ EDIT-MODE SEAM. MethaneApparatusRig wires itself to
        // ExperimentStarted from OnEnable — which does not fire in edit mode, so the
        // event passes it by and NONE of its five completion conditions register. The
        // symptom is brutal to read: the verb is performed perfectly (the grind measured
        // progress 1.00, IsGrindComplete true) and the step still never completes,
        // which looks exactly like a broken verb rather than a missing subscription.
        //
        // Calling the rig's own public handler is the house Bind()-seam rule, not a
        // shortcut: it performs the subscription play mode would have, then every
        // condition still has to be satisfied by the real verbs below.
        foreach (var rig in Object.FindObjectsByType<MethaneApparatusRig>(
                     FindObjectsInactive.Include))
            if (rig != null) rig.HandleExperimentStarted(module);

        var res = new Result { totalTasks = module.graphTasks.Count };
        log.AppendLine("=== Simulated run: " + moduleId + " (" + res.totalTasks + " tasks) ===");

        // Everything a step needs (indexes, reaction watchers, audits) lives in the Session
        // so the PLAY-MODE visual sweep can perform ONE honest step per frame and
        // photograph it; this edit-mode walk just drives the same session to the end.
        var session = new Session();
        session.Begin(runner, moduleId, res, log);

        // Walk until finished or stuck. Each pass serves every currently-available task.
        for (int pass = 0; pass < res.totalTasks + 2 && session.Done.Count < res.totalTasks; pass++)
        {
            bool progressed = false;
            // Reconcile first: a wrap-up ("record observations") or a condition
            // task can auto-complete via Graph.Tick DURING another task's sim
            // (e.g. the ester heat loop ticks the graph). Count it here or the
            // walk false-deadlocks — it's already complete, so never "available".
            foreach (var gt in runner.Graph.Tasks)
                if (runner.Graph.IsComplete(gt.taskId) && !session.Done.Contains(gt.taskId))
                { session.Done.Add(gt.taskId); res.completedTasks++; progressed = true;
                  log.AppendLine("\n» " + gt.taskId + " — auto-completed (wrap-up / condition)"); }

            foreach (var t in new List<ExperimentTask>(runner.Graph.AvailableTasks()))
            {
                if (session.Done.Contains(t.taskId)) continue;
                session.Perform(t);
                if (runner.Graph.IsComplete(t.taskId)) { session.Done.Add(t.taskId); res.completedTasks++; progressed = true; }
            }
            if (!progressed && session.Done.Count < res.totalTasks)
            {
                var avail = new List<string>();
                foreach (var t in runner.Graph.AvailableTasks()) avail.Add(t.taskId);
                res.bugs.Add("DEADLOCK after " + session.Done.Count + "/" + res.totalTasks
                             + " tasks; available-but-stuck: " + string.Join(", ", avail));
                break;
            }
        }

        AuditSupplies(session.Demand, session.LabItems, res, log);
        AuditVisibility(session.PouredInto, res, log);

        res.mistakes = runner.MistakeCount;
        if (res.mistakes > 0)
            res.bugs.Add("a PERFECT run logged " + res.mistakes + " mistake(s) — correct play is being graded as wrong");

        log.AppendLine("\n=== verdict: " + res.completedTasks + "/" + res.totalTasks + " tasks · "
                       + res.mistakes + " mistakes · " + res.bugs.Count + " bugs · " + res.warnings.Count + " warnings ===");
        foreach (var b in res.bugs) log.AppendLine("  BUG  " + b);
        foreach (var w in res.warnings) log.AppendLine("  WARN " + w);

        // TEAR DOWN, don't leave a revealed stage: Build("tutorial-methane") is a
        // pure clear (stage children incl. TeleAnchor_* pads, DynLabel_* billboards,
        // bench bindings) that spawns nothing, because methane's stage is hand-built.
        // Ending on Build(moduleId) would leave the guidance litter in the scene after
        // every suite run, bringing back hand-deleted DynLabels and TeleAnchors. Use
        // Reveal Stage when you WANT the built stage to inspect.
        session.End();
        builder.Build("tutorial-methane");

        // Put every drop back: bottles refill, tubes empty — the scene must be
        // byte-identical to before the run.
        foreach (var (lp, chem, ml, ppt, pptMl) in snapshot)
        {
            if (lp == null) continue;
            lp.SetContents(chem, ml);
            lp.currentPptChemical = ppt;
            lp.currentPptVolume = pptMl;
        }
        return res;
    }

    /// ⛔ NEVER complete a task by fiat while the VISUAL sweep is playing (no
    /// cheating a step through in code). In edit mode a handler that
    /// cannot finish a step forces past it so the walk can report what ELSE breaks — that
    /// is the right trade for a static audit. In Play mode it would silently mark a step
    /// done that no player could have done, which is the one thing the sweep exists to
    /// detect; the autopilot waits, re-serves, and finally reports the step UNPLAYED.
    public static bool NeverForce;

    static void ForcePast(ExperimentRunner runner, string id)
    {
        if (NeverForce || runner == null) return;
        runner.CompleteTask(id);
    }

    // ---- play-mode visual sweep seams ----------------------------------
    /// What the last Perform acted ON, so a photographer can point a camera at it
    /// without re-deriving the handler's choice: the vessel the step happened in, the
    /// verb kind, the temperature goal of a heat/chill step, and every reaction rule
    /// that fired during the step (its expectedObservation is what gets judged).
    public static LiquidPhysics LastVessel;
    public static string LastKind = "";
    public static float LastTargetC;
    public static readonly List<ReactionRule> LastReactions = new List<ReactionRule>();
    /// The vessel the last reaction actually fired IN. A rack step serves five tubes and
    /// the observation belongs to whichever one reacted, not to whichever the handler
    /// happened to note last; judging the wrong tube reads the permanganate as
    /// unchanged when the reaction fired one tube over.
    public static LiquidPhysics LastReactionVessel;
    /// Mid-verb hook: photograph a verb IN FLIGHT — the loaded scoop over the jar, the
    /// dish at the flame, the vessel on the balance. Null outside the visual sweep.
    public static System.Action<GameObject, string> MidVerb;

    static void Note(string kind, LiquidPhysics lp, float targetC = 0f)
    { LastKind = kind; if (lp != null) LastVessel = lp; if (targetC > 0f) LastTargetC = targetC; }

    /// What a vessel actually holds, for a transcript line. Print this beside any failure
    /// rather than describing the failure alone.
    public static string StateOf(LiquidPhysics lp)
    {
        if (lp == null) return "(no vessel)";
        string ledger = "";
        foreach (var n in lp.Ledger.Names) ledger += (ledger.Length > 0 ? " + " : "") + n;
        return lp.name + " holds "
               + (lp.currentChemical != null ? lp.currentChemical.chemicalName : "NOTHING")
               + " " + lp.currentLiquidVolume.ToString("0.#") + " ml"
               + (lp.currentPptVolume > 0.01f ? " (+ppt " + lp.currentPptVolume.ToString("0.#") + ")" : "")
               + " at " + lp.currentTempC.ToString("0") + " C"
               + (ledger.Length > 0 ? " [" + ledger + "]" : " [empty ledger]");
    }

    /// The bench tool that dips exactly `grams` per action (scoopula 2 g, porcelain
    /// spatula 0.1 g) — the verb contract's tool choice, mirrored.
    static ScoopController FindScoopTool(float grams)
    {
        foreach (var sc in Object.FindObjectsByType<ScoopController>())
            if (sc != null && sc.gameObject.activeInHierarchy && !sc.name.StartsWith("Template_")
                && Mathf.Abs(sc.GramsPerDip - grams) < 0.001f) return sc;
        return null;
    }

    /// Carry the tool so its blade sits at the vessel's mouth, as a hand would.
    static void HoldBladeAt(ScoopController tool, LiquidPhysics vessel)
    {
        var b = ExperimentSceneBuilder.SolidWorldBounds(vessel.gameObject);
        tool.transform.position += new Vector3(b.center.x, b.max.y + 0.01f, b.center.z) - tool.BladeTip;
    }

    /// One honest run's working state: the mechanisms the build produced, the reaction
    /// watchers, the supply/visibility tallies. Run() drives it to the end in edit mode;
    /// the VISUAL autopilot performs one step per frame in Play mode and photographs each.
    public sealed class Session
    {
        public ExperimentRunner Runner; public Result Res; public StringBuilder Log;
        public Dictionary<string, List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)>> StepsByTask;
        public RackTaskGroup[] Racks; public ZoneSimStation[] SimStations; public ExperimentTaskStation[] ZoneStations;
        public TemperatureSim[] Temps; public LabItem[] LabItems;
        public readonly Dictionary<ChemicalData, float> Demand = new Dictionary<ChemicalData, float>();          // supply audit
        public readonly Dictionary<LiquidTaskBinding, float> PouredInto = new Dictionary<LiquidTaskBinding, float>(); // fill-visibility audit
        public readonly HashSet<string> Done = new HashSet<string>();
        readonly HashSet<string> _firedRules = new HashSet<string>();
        readonly List<(LiquidPhysics lp, System.Action<ReactionRule> h)> _watchers = new List<(LiquidPhysics, System.Action<ReactionRule>)>();
        System.Action<LabErrorType, string> _onMistake;

        public void Begin(ExperimentRunner runner, string moduleId, Result res, StringBuilder log)
        {
            Runner = runner; Res = res; Log = log;

            // The module's own END PRODUCT is hidden from the bench during its run
            // (the player must craft it), so a shelf bottle of it CANNOT
            // be a source, however the edit-mode scene looks (e.g. the litmus sample
            // must not come from the play-mode-hidden Raw_BenzoicAcid).
            s_hiddenProduct = EndProductVisibility.HiddenProductFor(moduleId, false);
            // The product IDENTITY (independent of whether a shelf bottle exists to
            // hide): a bound vessel holding ONLY this substance is the player's own
            // purified product — the preferred source for every test draw.
            s_moduleProduct = DemoMode.ProductFor(moduleId);

            // Every mistake as it lands, in context — a count alone can't tell a
            // mis-authored binding from a fume-hood rule firing on a bench pour.
            _onMistake = (type, msg) => log.AppendLine("  ⚠ MISTAKE [" + type + "] " + msg);
            runner.Mistakes.MistakeRecorded += _onMistake;

            // Heat-gate watchdog: the procedure's heating must be achieved before its
            // reactions come, so any temperature-gated
            // rule that fires while its vessel is still COLD is a broken gate.
            // ALSO the reaction TRANSCRIPT: every fire is
            // logged with its observation, so "did the violet discharge / did the
            // buff precipitate drop" is read straight off the log instead of being
            // proven only transitively by the next step's source lookup.
            foreach (var wlp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
            {
                var cap = wlp;
                System.Action<ReactionRule> h = rule =>
                {
                    if (rule == null) return;
                    LastReactions.Add(rule);
                    LastReactionVessel = cap;
                    if (!rule.TemperatureSatisfied(cap.currentTempC))
                        res.bugs.Add(cap.name + ": " + rule.name + " fired at " + cap.currentTempC.ToString("0")
                                     + " C but needs " + rule.minTemperatureC.ToString("0") + " C — the heat gate is broken");
                    if (_firedRules.Add(rule.name + "@" + cap.name))   // first fire per vessel — bursts stay one line
                        log.AppendLine("  ⚗ " + rule.name + " in " + cap.name
                                       + (string.IsNullOrEmpty(rule.expectedObservation) ? "" : " — " + rule.expectedObservation));
                    // A precipitate rule must leave VISIBLE precipitate volume, or
                    // the observation text lies to the player.
                    if (rule.hasPrecipitate && rule.resultPrecipitate != null && cap.currentPptVolume <= 0.01f)
                        res.bugs.Add(cap.name + ": " + rule.name + " claims a precipitate but the vessel shows none");
                };
                cap.ReactionOccurred += h;
                _watchers.Add((cap, h));
            }

            // FUNNEL audit: a funnel WITHOUT the
            // LiquidPassthrough marker EATS any pour aimed through it (the stream
            // lands on its collider as a puddle). The hints
            // tell the player to pour through the funnel, so a bad funnel breaks
            // the filter step even though a direct pour would work.
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if ((t.name.StartsWith("Eq_Funnel") || t.name.StartsWith("Funnel_"))
                    && t.GetComponent<LiquidPassthrough>() == null)
                    res.bugs.Add(t.name + " has NO LiquidPassthrough — a pour through it is wasted (run Apply Verb Data)");

            // Index the mechanisms the build produced.
            var bindings = Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include);
            Racks = Object.FindObjectsByType<RackTaskGroup>(FindObjectsInactive.Include);
            SimStations = Object.FindObjectsByType<ZoneSimStation>(FindObjectsInactive.Include);
            ZoneStations = Object.FindObjectsByType<ExperimentTaskStation>(FindObjectsInactive.Include);
            Temps = Object.FindObjectsByType<TemperatureSim>(FindObjectsInactive.Include);
            LabItems = Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include);

            StepsByTask = new Dictionary<string, List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)>>();
            foreach (var b in bindings)
                foreach (var s in b.ExpectedSteps)
                {
                    if (s == null || string.IsNullOrEmpty(s.taskId)) continue;
                    if (!StepsByTask.TryGetValue(s.taskId, out var l)) StepsByTask[s.taskId] = l = new List<(LiquidTaskBinding, LiquidTaskBinding.ReagentStep)>();
                    l.Add((b, s));
                }
        }

        /// Serve ONE available task the way a hand would. True only when the task
        /// completed through the real chain with no new bug logged — a handler that
        /// had to force past a broken step returns false (the bug is in Res.bugs).
        public bool Perform(ExperimentTask t)
        {
            var runner = Runner; var res = Res; var log = Log;   // the handlers' names
            string id = t.taskId;
            int bugsBefore = res.bugs.Count;
            LastVessel = null; LastKind = ""; LastTargetC = 0f;
            LastReactions.Clear(); LastReactionVessel = null;
            log.AppendLine("\n» " + id + " — \"" + t.label + "\"");

            bool handled;
            if (StepsByTask.TryGetValue(id, out var steps))
            {
                handled = true;
                // A vapor-collect task's "reagent" is PRODUCED by the hot
                // source tube, never poured from a bottle — own it first.
                if (SimulateVapor(runner, id, steps, PouredInto, res, log)) { }
                else if (SimulatePours(runner, id, steps, Demand, PouredInto, res, log)
                    && !runner.Graph.IsComplete(id))
                {
                    // Delivery done but completion belongs elsewhere: a rack
                    // group (comparison sets) or a station (pour THEN heat —
                    // Exp 2's hydrolysis boil is exactly this handoff).
                    bool deferred = false;
                    foreach (var (_, s) in steps) if (!s.completesTask) deferred = true;
                    RackTaskGroup rack = null;
                    foreach (var r in Racks) if (r != null && r.TaskId == id) rack = r;
                    if (rack != null)
                    {
                        if (!rack.ShouldFire())
                        {
                            res.bugs.Add(id + ": rack group refuses to fire after every member was served ("
                                         + rack.MemberCount + " members)");
                            log.AppendLine("  ✗ rack not ready after full service");
                        }
                        else
                        {
                            runner.CompleteTask(id);   // exactly what the group's Update does in play
                            log.AppendLine("  ✓ rack fired (" + rack.MemberCount + " tubes)");
                        }
                    }
                    else if (SimulateVesselHeat(runner, id, steps, res, log)) { }
                    else if (SimulateFermentation(runner, id, steps, res, log)) { }
                    else if (SimulateChillTask(runner, id, res, log)) { }   // chill WITH reagents (Exp 5's ice water)
                    else if (SimulateWeigh(runner, id, steps, res, log)) { }
                    else if (SimulateLitmus(runner, id, steps, res, log)) { }
                    else if (SimulateFlame(runner, id, steps, res, log)) { }   // flammability confirm (Exp 7)
                    else if (SimulateStation(runner, id, SimStations, ZoneStations, Temps, LabItems, res, log)) { }
                    else if (deferred)
                    {
                        res.bugs.Add(id + ": steps defer completion (completesTask:false) but NO rack group, heat task, or station owns it");
                        log.AppendLine("  ✗ deferred but ownerless");
                    }
                    else
                        res.bugs.Add(id + ": every step delivered its full amount yet the task did not complete");
                }
                else if (runner.Graph.IsComplete(id))
                    log.AppendLine("  ✓ completed by delivery");
                // The procedure's heat step, played: mixes held cold go into
                // the water bath and must fire there.
                if (runner.Graph.IsComplete(id)) WarmAtBath(id, steps, res, log);
            }
            else
                handled = SimulateStation(runner, id, SimStations, ZoneStations, Temps, LabItems, res, log)
                          || SimulateChillTask(runner, id, res, log)
                          || SimulateVesselHeat(runner, id, null, res, log)   // heat with no reagents of its own (Exp 6's heat-glow)
                          || SimulateWeigh(runner, id, null, res, log)        // weigh with no pours of its own (Exp 7's weigh-product)
                          || SimulateStir(runner, id, res, log)               // stir with no pours of its own (Exp 8's stir & stand)
                          || SimulateGrind(runner, id, res, log)              // pestle in the mortar (methane's prepare-mixture)
                          || SimulateMethane(runner, id, res, log);           // the hand-built tutorial rig

            if (!handled)
            {
                // No binding, no station: maybe an externally-registered condition.
                runner.Graph.Tick();
                if (!runner.Graph.IsComplete(id))
                {
                    res.bugs.Add(id + ": NO mechanism completes this task (no binding, station, or satisfied condition)");
                    log.AppendLine("  ✗ no mechanism — the player could never finish this step");
                    Done.Add(id);           // skip so the walk can report what else breaks
                    ForcePast(runner, id); // force past it to keep exploring downstream
                }
            }
            return runner.Graph.IsComplete(id) && res.bugs.Count == bugsBefore;
        }

        public void End()
        {
            if (Runner != null && _onMistake != null) Runner.Mistakes.MistakeRecorded -= _onMistake;
            foreach (var (wlp, wh) in _watchers) if (wlp != null) wlp.ReactionOccurred -= wh;
            _watchers.Clear();
        }
    }

    /// Deliver every reagent step of one task THE WAY A HAND WOULD: draw each
    /// increment out of the real bench source bottle and land it through
    /// LiquidPhysics.AddLiquid — the full event chain (wake/mix/hazard/ledger/
    /// binding) runs for every action. Bulk tilt-pours overshoot ~20% like a
    /// human, which also proves the leniency contract (thresholds are minimums).
    /// Returns false when the task completed EARLY (already reported as a bug).
    static bool SimulatePours(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps,
        Dictionary<ChemicalData, float> demand,
        Dictionary<LiquidTaskBinding, float> pouredInto, Result res, StringBuilder log)
    {
        // PLAN first (source, increment, action count per step) so the early-
        // completion check compares against the REAL minimum — planning dips but
        // executing tilt-pours made 10/10 look like 10/50; overshoot pours are
        // OPTIONAL (a player stops when the ✓ shows), so only completion BELOW
        // the minimum is a bug.
        var plan = new List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s, LiquidPhysics src, bool vesselSource, bool scoopDraw, float inc, int n)>();
        int minTotal = 0;
        foreach (var (b, s) in steps)
        {
            var plp = b.GetComponent<LiquidPhysics>();
            if (plp == null)
            {
                res.bugs.Add(id + ": binding for " + s.reagent.chemicalName + " sits on '" + b.name
                             + "' which has NO LiquidPhysics — nothing to pour into");
                return false;
            }
            var psrc = FindSource(s.reagent, plp);
            if (psrc == null)
            {
                res.bugs.Add(id + ": no bench source holds '" + s.reagent.chemicalName
                             + "' — the player cannot perform this step");
                log.AppendLine("  ✗ no source bottle for " + s.reagent.chemicalName);
                continue;   // supply audit details it; keep walking
            }
            // Pouring FROM another vessel (the filtrate through the funnel) is a
            // tilt-pour of the solution, never spatula dips, whatever the
            // chemical's dry state says (dipping a hydrolysate 50× is wrong).
            // EXCEPT dry crystals, which take a porcelain spatula or scoopula:
            // a solid product drawn from
            // the PURE-PRODUCT vessel (crystallised flask / dried-crystal
            // beaker) is a scoop dip — a crude chill flask still holding mother
            // liquor is decanted (the filter pour), and heat vessels hold
            // liquid filtrates/distillates.
            bool pv = psrc.GetComponent<LiquidTaskBinding>() != null;
            bool scoopDraw = pv && IsSolid(s) && IsPureProductVessel(psrc);
            float pinc = pv && !scoopDraw ? 0.5f : IncrementFor(s);
            int pmin = s.requiredMl <= 0f ? 1 : Mathf.CeilToInt(s.requiredMl / pinc - 0.0001f);
            int pn = pmin;
            // Human overshoot on bulk tilt-pours only — squeezes and dips are counted.
            if (((pv && !scoopDraw) || !IsSolid(s)) && s.requiredMl > 4f) pn = Mathf.CeilToInt(pn * 1.2f);
            plan.Add((b, s, psrc, pv, scoopDraw, pinc, pn));
            minTotal += pmin;
        }

        int doneActions = 0;

        foreach (var (b, s, src, vesselSource, scoopDraw, inc, n) in plan)
        {
            var lp = b.GetComponent<LiquidPhysics>();
            Note(scoopDraw || (IsSolid(s) && !vesselSource) ? "solid" : "pour", lp);

            // FUME HOOD (Exp 5): a toxic/volatile reagent is only sanctioned
            // while the DESTINATION vessel sits inside the hood — so the sim
            // does what the player does: carries the vessel to the hood, pours
            // there, brings it back. A missing/collider-less hood is a hard bug
            // (the pour would grade a violation with no way to avoid it).
            Vector3 homePos = b.transform.position;
            bool carried = false;
            if (s.reagent.requiresFumeHood)
            {
                var hood = Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
                var hoodCol = hood != null ? hood.GetComponent<Collider>() : null;
                if (hood == null || hoodCol == null)
                {
                    res.bugs.Add(id + ": " + s.reagent.chemicalName + " requires the fume hood but the scene has "
                                 + (hood == null ? "no FumeHoodZone" : "a FumeHoodZone with NO collider")
                                 + " — the pour cannot be sanctioned anywhere");
                }
                else
                {
                    b.transform.position = hoodCol.bounds.center;
                    carried = true;
                    log.AppendLine("  (carried " + b.name + " into the fume hood)");
                    if (!b.InFumeHood())
                        res.bugs.Add(id + ": vessel placed at the hood's centre still doesn't count as inside — the hood volume is broken");
                }
            }

            float before = b.AccumulatedFor(id, s.reagent);
            int poured = 0;
            // PLAY MODE: solids go through the REAL scoop verb — the bench
            // scoopula/spatula dips the jar (the heap rides its blade), is carried to
            // the vessel and tipped in. Edit mode keeps the direct path below: Dip
            // builds runtime objects, and the suite's simrun pins must not move.
            ScoopController scoop = null;
            if (Application.isPlaying && (scoopDraw || (IsSolid(s) && !vesselSource))
                && src.currentChemical != null
                && ScoopMath.CanPickUp(false, src.currentChemical.state, src.currentLiquidVolume))
                scoop = FindScoopTool(inc);
            Vector3 scoopHome = scoop != null ? scoop.transform.position : Vector3.zero;
            for (int k = 0; k < n; k++)
            {
                if (scoop != null)
                {
                    HoldBladeAt(scoop, src);
                    if (!scoop.Dip(src))
                    {
                        res.warnings.Add(id + ": source " + src.name + " ran DRY mid-step ("
                                         + s.reagent.chemicalName + ") — starvation");
                        break;
                    }
                    if (k == 0) MidVerb?.Invoke(scoop.gameObject, "scoop");
                    HoldBladeAt(scoop, lp);
                    scoop.Deposit(lp);                          // AddLiquid inside — the real chemistry path
                }
                else
                {
                    var chem = src.PourOut(inc);                    // out of the real bottle
                    if (chem == null)
                    {
                        res.warnings.Add(id + ": source " + src.name + " ran DRY mid-step ("
                                         + s.reagent.chemicalName + ") — starvation");
                        break;
                    }
                    lp.AddLiquid(chem, inc);                        // the real chemistry path
                }
                doneActions++; poured++;
                if (runner.Graph.IsComplete(id))
                {
                    if (doneActions < minTotal)
                    {
                        res.bugs.Add(id + ": completed EARLY at action " + doneActions + "/" + minTotal
                                     + " minimum (" + s.reagent.chemicalName + ") — a threshold or step-set is wrong");
                        log.AppendLine("  ✗ completed early at " + doneActions + "/" + minTotal);
                        return false;
                    }
                    break;   // the ✓ showed — a player stops pouring
                }
            }
            if (scoop != null) scoop.transform.position = scoopHome;
            // The pour happened; did the BINDING hear it? A binding silently
            // unsubscribed from its vessel leaves a real player hard-stuck.
            if (s.requiredMl > 0f && b.AccumulatedFor(id, s.reagent) <= before + 0.001f
                && !runner.Graph.IsComplete(id))
            {
                res.bugs.Add(id + ": poured " + s.reagent.chemicalName + " into " + b.name
                             + " but the binding COUNTED NOTHING (IsListening=" + b.IsListening
                             + ") — the event chain is broken");
                log.AppendLine("  ✗ binding heard nothing");
            }

            // Only SHELF draws feed the supply audit — a vessel-to-vessel pour
            // (the filtrate, the product draws) consumes nothing from a bottle.
            if (!vesselSource)
            { demand.TryGetValue(s.reagent, out float d); demand[s.reagent] = d + Mathf.Max(inc * poured, 0f); }
            pouredInto.TryGetValue(b, out float pv); pouredInto[b] = pv + inc * poured;
            string verb = scoopDraw ? "scoopula-dip (crystals)"
                        : vesselSource ? "tilt-pour (vessel to vessel)" : VerbFor(s);
            log.AppendLine("  " + poured + "× " + verb
                           + " " + s.reagent.chemicalName + " (from " + src.name + ") → " + b.name
                           + " (" + (inc * poured).ToString("0.#") + (vesselSource && !scoopDraw ? " ml" : UnitFor(s))
                           + (carried ? ", in the fume hood" : "") + ")");
            if (carried)
            {
                b.transform.position = homePos;   // back to its hand-placed home
                log.AppendLine("  (returned " + b.name + " to the bench)");
            }
        }
        // What the glass holds once the step is served — the single most useful line in
        // the transcript when Play mode and edit mode disagree.
        var seenDest = new HashSet<LiquidPhysics>();
        foreach (var (b, _, _, _, _, _, _) in plan)
        {
            var dlp = b != null ? b.GetComponent<LiquidPhysics>() : null;
            if (dlp != null && seenDest.Add(dlp)) log.AppendLine("    → " + StateOf(dlp));
        }
        return true;
    }

    /// The REAL water bath, prepared like a player: pour distilled water into it
    /// from the shelf bottle (once), stand a lit burner beside it, let it heat.
    /// Returns the ready controller, or null with a bug when the tool chain is
    /// broken. The bath is the only heat source — no zones, no stations.
    static WaterBathController PrepareBath(Result res, StringBuilder log)
    {
        var bath = Object.FindAnyObjectByType<WaterBathController>();
        if (bath == null)
        {
            res.bugs.Add("no WaterBathController in the scene — heated steps are unplayable (run Apply Verb Data)");
            return null;
        }
        if (!bath.HasWater)
        {
            var bathLp = bath.GetComponentInChildren<LiquidPhysics>();
            ChemicalData water = null;
            foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
                if (lp != null && lp.currentChemical != null && lp.currentChemical.chemicalName == "Distilled Water"
                    && lp.GetComponent<LiquidTaskBinding>() == null)
                { water = lp.currentChemical;
                  for (int i = 0; i < 20; i++) { var c = lp.PourOut(1f); if (c == null) break; bathLp.AddLiquid(c, 1f); }
                  break; }
            if (water == null || !bath.HasWater)
            {
                res.bugs.Add("could not fill the water bath with Distilled Water — bath unusable");
                return null;
            }
            log.AppendLine("  (filled the water bath with 20 ml Distilled Water)");
        }
        log.AppendLine("  (lit a burner beside the bath)");
        return bath;
    }

    /// A mixed-cold recipe carried to the bath: heat until it fires (or the bath
    /// tops out — then the gate itself is broken).
    static void WarmAtBath(string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        var seen = new HashSet<LiquidPhysics>();
        foreach (var (b, _) in steps)
        {
            var lp = b != null ? b.GetComponent<LiquidPhysics>() : null;
            if (lp == null || !seen.Add(lp) || !lp.HasPendingReaction) continue;
            var rule = lp.PendingRule;
            var bath = PrepareBath(res, log);
            if (bath == null) return;
            for (int i = 0; i < 600 && lp.HasPendingReaction; i++)
            {
                bath.DriveForTest(0.5f);
                bath.HeatVessel(lp);
            }
            if (!lp.HasPendingReaction)
                log.AppendLine("  ✓ held " + lp.name + " in the bath (" + bath.BathC.ToString("0")
                               + " C) → " + rule.name
                               + (string.IsNullOrEmpty(rule.expectedObservation) ? "" : " (" + rule.expectedObservation + ")"));
            else
                res.bugs.Add(id + ": bath topped out at " + bath.BathC.ToString("0") + " C but "
                             + rule.name + " (needs " + rule.minTemperatureC.ToString("0") + " C) did not fire");
        }
    }

    /// The zone-free FERMENTATION step (Exp 3): the must is prepared, the player
    /// leads the delivery tube from the flask into a limewater tube; CO₂ clouds
    /// it (CaCO₃) and the task completes — then the longProcess time-skip fires
    /// ("one week later"). Played by bubbling CO₂ into the limewater vessel this
    /// task's binding names, until the FermentationController's condition trips.
    static bool SimulateFermentation(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        FermentationController fc = null;
        foreach (var f in Object.FindObjectsByType<FermentationController>(FindObjectsInactive.Include))
            if (f != null && f.FermentTaskId == id) { fc = f; break; }
        if (fc == null) return false;

        // The limewater vessel this task fills (its binding holds limewater).
        LiquidPhysics lime = null;
        foreach (var (b, s) in steps)
        {
            var lp = b != null ? b.GetComponent<LiquidPhysics>() : null;
            if (lp != null && fc.Limewater != null && lp.currentChemical == fc.Limewater) { lime = lp; break; }
        }
        if (lime == null)
        {
            res.bugs.Add(id + ": fermentation has no limewater vessel to cloud (pour limewater into a tube first)");
            return true;
        }
        Note("ferment", lime);
        if (!fc.Fermenting)
        {
            res.bugs.Add(id + ": flask is not fermenting (is prepare-must complete + the must in the flask?)");
            return true;
        }
        for (int i = 0; i < 200 && !runner.Graph.IsComplete(id); i++)
        {
            fc.BubbleInto(lime);
            runner.Graph.Tick();
        }
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ led the delivery tube in — limewater clouded (CaCO₃), fermentation confirmed → time-skip");
        else
        {
            res.bugs.Add(id + ": bubbled CO₂ but the limewater never clouded / task never completed");
            ForcePast(runner, id);
        }
        return true;
    }

    /// The zone-free heat STEP: the vessel owning a VesselHeatTask is served,
    /// then held at the right heat source until its task condition trips.
    /// ≤100 °C = the water bath; beyond = the NAKED FLAME (Exp 6's dry
    /// distillation — hold the hard-glass tube over a lit burner). steps may be
    /// null for heat tasks with no reagents of their own (found by TaskId).
    static bool SimulateVesselHeat(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        VesselHeatTask heat = null; LiquidPhysics tube = null;
        if (steps != null)
            foreach (var (b, _) in steps)
            {
                var h = b != null ? b.GetComponent<VesselHeatTask>() : null;
                if (h != null && h.TaskId == id) { heat = h; tube = b.GetComponent<LiquidPhysics>(); break; }
            }
        if (heat == null)
            foreach (var h in Object.FindObjectsByType<VesselHeatTask>(FindObjectsInactive.Include))
                if (h != null && h.TaskId == id) { heat = h; tube = h.GetComponent<LiquidPhysics>(); break; }
        if (heat == null || tube == null) return false;
        Note("heat", tube, heat.RequiredC);

        if (heat.RequiredC > WaterBathMath.BathMaxC)
        {
            // OPEN FLAME: the bath tops out at 100 — hold the tube over a lit burner.
            var flame = Object.FindAnyObjectByType<NakedFlameHeat>(FindObjectsInactive.Include);
            var fburner = flame != null ? flame.GetComponent<BurnerController>() : null;
            if (flame == null || fburner == null)
            {
                res.bugs.Add(id + ": needs " + heat.RequiredC.ToString("0")
                             + " C (open flame) but no burner carries NakedFlameHeat (run Apply Verb Data)");
                ForcePast(runner, id);
                return true;
            }
            fburner.Ignite();
            log.AppendLine("  (lit a burner — OPEN FLAME, no water bath)");
            for (int i = 0; i < 600 && !runner.Graph.IsComplete(id); i++)
            {
                flame.HeatVessel(tube, 0.5f);
                runner.Graph.Tick();
            }
            fburner.Extinguish();
            if (runner.Graph.IsComplete(id))
                log.AppendLine("  ✓ held " + tube.name + " over the flame to " + heat.RequiredC.ToString("0")
                               + " C — the acetates glow (tube at " + tube.currentTempC.ToString("0") + " C)");
            else
            {
                res.bugs.Add(id + ": open-flame heating never completed the step (tube "
                             + tube.currentTempC.ToString("0") + " C / needs " + heat.RequiredC.ToString("0") + " C)");
                ForcePast(runner, id);
            }
            return true;
        }

        var bath = PrepareBath(res, log);
        if (bath == null) return true;   // owned, but the tool chain is broken (bug already logged)
        for (int i = 0; i < 600 && !runner.Graph.IsComplete(id); i++)
        {
            bath.DriveForTest(0.5f);
            bath.HeatVessel(tube);
            runner.Graph.Tick();
        }
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ held " + tube.name + " in the bath to " + heat.RequiredC.ToString("0")
                           + " C — step complete (tube at " + tube.currentTempC.ToString("0")
                           + " C; the sim's 0.5 s ticks overshoot — in play the step completes AT the threshold)");
        else
        {
            res.bugs.Add(id + ": bath heating never completed the step (tube " + tube.currentTempC.ToString("0")
                         + " C / needs " + heat.RequiredC.ToString("0") + " C, bath " + bath.BathC.ToString("0") + " C)");
            ForcePast(runner, id);   // force past to keep exploring downstream
        }
        return true;
    }

    /// The zone-free chill STEP (Exp 4 crystallise): the flask holding the
    /// acidified product is set in the real ice bucket until its task condition
    /// (holding something AND cold) trips. No binding of its own — the vessel is
    /// found by its VesselChillTask.
    static bool SimulateChillTask(ExperimentRunner runner, string id, Result res, StringBuilder log)
    {
        VesselChillTask chill = null;
        foreach (var c in Object.FindObjectsByType<VesselChillTask>(FindObjectsInactive.Include))
            if (c != null && c.TaskId == id) { chill = c; break; }
        if (chill == null) return false;

        var lp = chill.GetComponent<LiquidPhysics>();
        Note("chill", lp, chill.RequiredC);
        var ice = Object.FindAnyObjectByType<IceBathController>();
        if (ice == null)
        {
            res.bugs.Add(id + ": no IceBathController in the scene — chill steps are unplayable (run Apply Verb Data)");
            ForcePast(runner, id);
            return true;
        }
        for (int i = 0; i < 40 && !runner.Graph.IsComplete(id); i++)
        {
            ice.ChillVessel(lp);
            runner.Graph.Tick();
        }
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ set " + (lp != null ? lp.name : "the vessel") + " in the ice bath (zone r="
                           + ice.ChillZoneRadius.ToString("0.00") + " m) — chilled to "
                           + (lp != null ? lp.currentTempC.ToString("0") : "?") + " C, crystals formed");
        else
        {
            res.bugs.Add(id + ": ice bath never completed the chill (vessel "
                         + (lp != null ? lp.currentTempC.ToString("0") + " C holding " + (lp.currentLiquidVolume + lp.currentPptVolume).ToString("0.#") + " ml" : "missing")
                         + " / needs <= " + chill.RequiredC.ToString("0") + " C with contents)");
            ForcePast(runner, id);   // force past to keep exploring downstream
        }
        return true;
    }

    /// The zone-free LITMUS confirmation (Exp 4 test-litmus): once the tube is
    /// served, a fresh strip is touched to it through the strip's real read path
    /// (TouchVessel — the physics-free stand-in for the dip). The mixture must
    /// actually read acid or the strip stays violet and the task must not pass.
    static bool SimulateLitmus(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        VesselLitmusTask lt = null; LiquidPhysics tube = null;
        foreach (var (b, _) in steps)
        {
            var l = b != null ? b.GetComponent<VesselLitmusTask>() : null;
            if (l != null && l.TaskId == id) { lt = l; tube = b.GetComponent<LiquidPhysics>(); break; }
        }
        if (lt == null) return false;
        Note("litmus", tube);

        // The player tears a strip off the bench litmus box — it must exist,
        // and at least one dispensable strip must CARRY the LitmusStrip
        // component with a collider, or a real touch can never register.
        bool boxOnBench = false;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name.StartsWith("Raw_LitmusPaper")) { boxOnBench = true; break; }
        if (!boxOnBench)
            res.bugs.Add(id + ": no Raw_LitmusPaper box on the bench — the player has no strip to test with");
        bool stripWired = false;
        foreach (var st in Object.FindObjectsByType<LitmusStrip>(FindObjectsInactive.Include))
            if (st != null && st.GetComponentInChildren<Collider>(true) != null) { stripWired = true; break; }
        if (!stripWired)
            res.bugs.Add(id + ": no litmus strip in the scene carries a Collider — a physical touch can never register in play");

        // PLAY MODE: use a REAL bench strip when one is out, so the strip's own
        // colour (LitmusMath.ColorForPH) is what gets photographed. Edit mode keeps the
        // renderer-less stand-in.
        LitmusStrip real = null;
        if (Application.isPlaying)
            foreach (var st in Object.FindObjectsByType<LitmusStrip>())
                if (st != null && !st.Used && st.gameObject.activeInHierarchy && !st.name.StartsWith("Template_"))
                { real = st; break; }
        if (real != null)
        {
            Vector3 stripHome = real.transform.position;
            var tb = ExperimentSceneBuilder.SolidWorldBounds(tube.gameObject);
            real.transform.position = new Vector3(tb.center.x, tb.max.y + 0.02f, tb.center.z);
            real.TouchVessel(tube);
            runner.Graph.Tick();
            MidVerb?.Invoke(real.gameObject, "litmus");
            real.transform.position = stripHome;
        }
        else
        {
            var stripGo = new GameObject("SimLitmusStrip");
            try
            {
                stripGo.AddComponent<LitmusStrip>().TouchVessel(tube);
                runner.Graph.Tick();
            }
            finally { Object.DestroyImmediate(stripGo); }
        }

        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ touched a litmus strip to " + tube.name + " — mixture pH "
                           + tube.CurrentPH.ToString("0.#")
                           + (tube.CurrentPH <= LitmusMath.AcidPH
                              ? " read ACID, strip turned red" : " read BASE, strip turned blue"));
        else
        {
            res.bugs.Add(id + ": the litmus touch did not complete the task (mixture pH "
                         + (tube != null ? tube.CurrentPH.ToString("0.#") : "?")
                         + ", definitive needs <= " + LitmusMath.AcidPH + " or >= " + LitmusMath.BasePH + ")");
            ForcePast(runner, id);   // force past to keep exploring downstream
        }
        return true;
    }

    /// The zone-free STIR verb (Exp 8 "shake frequently while dropping"): the
    /// bench glass rod is circled inside the vessel that owns the task — driven
    /// through StirController.Tick, the SAME sample path a play-mode frame
    /// feeds, in quarter-rev-safe 10° steps around the mouth.
    static bool SimulateStir(ExperimentRunner runner, string id, Result res, StringBuilder log)
    {
        StirController stir = null;
        foreach (var s in Object.FindObjectsByType<StirController>(FindObjectsInactive.Include))
            if (s != null && s.TaskId == id) { stir = s; break; }
        if (stir == null) return false;
        Note("stir", stir.GetComponent<LiquidPhysics>());

        int samples = 0;
        for (int i = 0; i < 200 && !runner.Graph.IsComplete(id); i++)
        {
            float a = i * 10f * Mathf.Deg2Rad;
            stir.Tick(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, true);
            samples++;
            if (i % 18 == 0) runner.Graph.Tick();
        }
        runner.Graph.Tick();
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ circled the glass rod in " + stir.name + " — "
                           + (samples / 36f).ToString("0.#") + " revolutions, well stirred");
        else
        {
            res.bugs.Add(id + ": circling the rod " + (samples / 36f).ToString("0.#")
                         + " revolutions never completed the stir");
            ForcePast(runner, id);
        }
        return true;
    }

    /// Circle the pestle in the mortar. Same shape as the stir verb: GrindController
    /// carries its own OrbitMath and the same Tick(x, z, inside) seam.
    static bool SimulateGrind(ExperimentRunner runner, string id, Result res, StringBuilder log)
    {
        GrindController grind = null;
        foreach (var g in Object.FindObjectsByType<GrindController>(FindObjectsInactive.Include))
            if (g != null && g.TaskId == id) { grind = g; break; }
        if (grind == null) return false;
        Note("grind", grind.GetComponent<LiquidPhysics>());

        int samples = 0;
        for (int i = 0; i < 300 && !runner.Graph.IsComplete(id); i++)
        {
            float a = i * 10f * Mathf.Deg2Rad;
            grind.Tick(Mathf.Cos(a) * 0.04f, Mathf.Sin(a) * 0.04f, true);
            samples++;
            if (i % 18 == 0) runner.Graph.Tick();
        }
        runner.Graph.Tick();
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  OK ground the mixture in " + grind.name + " - "
                           + (samples / 36f).ToString("0.#") + " revolutions of the pestle");
        else
        {
            res.bugs.Add(id + ": circling the pestle " + (samples / 36f).ToString("0.#")
                         + " revolutions never completed the grind"
                         + " [swept " + grind.Math.SweptDegrees.ToString("0") + " deg"
                         + ", needs " + grind.Math.requiredRevs + " revs"
                         + ", progress " + grind.Math.Progress01.ToString("0.00")
                         + ", IsGrindComplete=" + grind.IsGrindComplete()
                         + ", condition registered=" + runner.Graph.HasCondition(id) + "]");
            ForcePast(runner, id);
        }
        return true;
    }

    /// The methane tutorial's four rig steps. Everything here is driven by the rig's own
    /// per-frame logic, and Update and Time do not exist in edit mode.
    /// MethaneApparatusRig.Step(dt) is that same frame
    /// with the timestep injected, so this plays the REAL rig rather than a copy of it.
    ///
    /// Props are carried into position and put back afterwards, exactly as SimulateFlame
    /// already holds a dish to the burner: "bring the burner to the tube" IS the player
    /// action, and a sim that teleported the completion instead would prove nothing.
    static bool SimulateMethane(ExperimentRunner runner, string id, Result res, StringBuilder log)
    {
        var rig = Object.FindAnyObjectByType<MethaneApparatusRig>(FindObjectsInactive.Include);
        if (rig == null) return false;
        if (id != "setup-apparatus" && id != "heat-mixture" && id != "collect-gas" && id != "test-gas")
            return false;

        Transform tube = FindItemTransform("glass-tube");
        Transform collect = FindItemTransform("collection-tube");
        if (tube == null)
        {
            res.bugs.Add(id + ": no 'glass-tube' on the bench — the methane rig has nothing to heat");
            ForcePast(runner, id);
            return true;
        }
        {
            var mlp = (id == "collect-gas" || id == "test-gas") && collect != null ? collect.GetComponent<LiquidPhysics>() : null;
            Note("methane:" + id, mlp != null ? mlp : tube.GetComponent<LiquidPhysics>());
        }

        var moved = new List<(Transform t, Vector3 pos)>();
        void Carry(Transform t, Vector3 to) { if (t != null) { moved.Add((t, t.position)); t.position = to; } }

        try
        {
            if (id == "setup-apparatus")
            {
                // Scoop the ground mixture from the reagent jar into the hard-glass tube.
                var lp = tube.GetComponent<LiquidPhysics>();
                var jar = FindItemTransform("reagent-jar");
                var src = jar != null ? jar.GetComponent<LiquidPhysics>() : null;
                if (lp == null)
                    res.bugs.Add(id + ": the hard-glass tube has no LiquidPhysics — nothing can be loaded into it");
                else
                {
                    // ⛔ Through the REAL scoopula in Play mode, exactly like every other solid
                    // step: Dip builds the heap on the blade, Deposit builds the mound in
                    // the tube via EnsurePowderVisual. A bare AddLiquid moves the volume but
                    // draws NOTHING (4 g of solid, no mound), which the VISUAL sweep fails and a
                    // player scooping by hand never sees. Edit mode keeps the direct path so
                    // the simrun pins do not move, but mirrors what Deposit would have drawn.
                    ScoopController scoop = Application.isPlaying && src != null
                                            && src.currentChemical != null
                                            && ScoopMath.CanPickUp(false, src.currentChemical.state, src.currentLiquidVolume)
                        ? FindScoopTool(2f) : null;
                    if (scoop != null)
                    {
                        for (int k = 0; k < 2; k++)                 // 4 g = two 2 g dips
                        {
                            HoldBladeAt(scoop, src);
                            if (!scoop.Dip(src)) break;
                            if (k == 0) MidVerb?.Invoke(scoop.gameObject, "scoop");
                            HoldBladeAt(scoop, lp);
                            scoop.Deposit(lp);                      // AddLiquid + the mound, the real path
                        }
                    }
                    else
                    {
                        var chem = src != null ? src.PourOut(4f) : null;
                        var loaded = chem != null ? chem : lp.currentChemical;
                        lp.AddLiquid(loaded, 4f);
                        if (loaded != null)
                            ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, loaded,
                                ScoopController.MoundFill(lp.currentLiquidVolume));
                    }
                }
                runner.Graph.Tick();
            }
            else
            {
                // Heat, collect and the splint test all need a LIT burner held at the tube.
                var burner = Object.FindAnyObjectByType<BurnerController>(FindObjectsInactive.Include);
                if (burner == null)
                {
                    res.bugs.Add(id + ": no BurnerController in the scene — the methane tutorial cannot be heated");
                    ForcePast(runner, id);
                    return true;
                }
                Carry(burner.transform, tube.position + Vector3.down * 0.12f);
                burner.Ignite();

                if (id == "collect-gas" || id == "test-gas") Carry(collect, tube.position + Vector3.up * 0.06f);

                Matchstick match = null;
                if (id == "test-gas")
                {
                    foreach (var m in Object.FindObjectsByType<Matchstick>(FindObjectsInactive.Include))
                        if (m != null) { match = m; break; }
                    if (match == null)
                    {
                        res.bugs.Add(id + ": no Matchstick in the scene — the splint test is unplayable");
                        ForcePast(runner, id);
                        return true;
                    }
                    Carry(match.transform, (collect != null ? collect.position : tube.position) + Vector3.up * 0.05f);
                    match.Ignite();
                }

                // 60 s of rig time at 20 Hz — generous on purpose, so a failure here is a
                // real one and not the simulator running out of patience.
                // In play mode TWO Updates run: the rig's, and the temperature sim's own
                // (TemperatureSim.Update -> Tick(deltaTime)). The rig only ever calls
                // SetHeating; nothing advances the heat but that second Update. Edit mode
                // runs neither, so the sim drives both — a burner held to a tube that
                // never warms is not a game bug, it is a missing frame.
                var sims = Object.FindObjectsByType<TemperatureSim>(
                    FindObjectsInactive.Include);
                for (int i = 0; i < 1200 && !runner.Graph.IsComplete(id); i++)
                {
                    rig.Step(0.05f);
                    foreach (var ts in sims) if (ts != null) ts.Tick(0.05f);
                    if (i % 5 == 0) runner.Graph.Tick();
                }
                runner.Graph.Tick();
                MidVerb?.Invoke((id == "test-gas" && collect != null ? collect : tube).gameObject, id);
                burner.Extinguish();
                if (match != null) match.Extinguish(true);
            }
        }
        finally
        {
            foreach (var (t, pos) in moved) if (t != null) t.position = pos;
        }

        if (runner.Graph.IsComplete(id))
            log.AppendLine("  OK methane rig: " + id + " completed through the rig's own frame");
        else
        {
            res.bugs.Add(id + ": the methane rig never completed this step (60 s of held apparatus)"
                         + " [rig active=" + rig.gameObject.activeInHierarchy
                         + ", tube=" + (tube != null ? tube.name : "MISSING")
                         + ", tubeMl=" + (tube != null && tube.GetComponent<LiquidPhysics>() != null
                                          ? tube.GetComponent<LiquidPhysics>().currentLiquidVolume.ToString("0.0") : "n/a")
                         + ", collect=" + (collect != null ? collect.name : "MISSING")
                         + ", condition registered=" + runner.Graph.HasCondition(id) + "]");
            ForcePast(runner, id);
        }
        return true;
    }

    static Transform FindItemTransform(string itemId)
    {
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include))
            if (li != null && li.itemId == itemId) return li.transform;
        return null;
    }

    /// The zone-free FLAMMABILITY confirmation (Exp 7): the sample is poured
    /// onto its dish, then a LIT flame is brought to it — the sim lights the
    /// bench burner and holds the dish at its flame (the match-strike stand-in),
    /// driving the SAME PollFlames scan a play-mode frame runs. The task must
    /// complete on the flame verb, never on the pour alone.
    static bool SimulateFlame(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        VesselFlameTask flame = null; LiquidPhysics dish = null;
        foreach (var (b, _) in steps)
        {
            var f = b != null ? b.GetComponent<VesselFlameTask>() : null;
            if (f != null && f.TaskId == id) { flame = f; dish = b.GetComponent<LiquidPhysics>(); break; }
        }
        if (flame == null || dish == null) return false;
        Note("flame", dish);

        var burner = Object.FindAnyObjectByType<BurnerController>(FindObjectsInactive.Include);
        if (burner == null)
        {
            res.bugs.Add(id + ": no bench burner to take a flame from — the flammability test is unplayable");
            ForcePast(runner, id);
            return true;
        }
        Vector3 home = dish.transform.position;
        burner.Ignite();
        dish.transform.position = FlameTestMath.FlamePos(burner);   // hold the sample AT the flame
        flame.PollFlames();
        runner.Graph.Tick();
        MidVerb?.Invoke(dish.gameObject, "flame");
        dish.transform.position = home;
        burner.Extinguish();
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ lit a burner and held " + dish.name
                           + " to the flame — it refuses to ignite (non-flammable confirmed)");
        else
        {
            res.bugs.Add(id + ": holding a lit flame to the served sample did not complete the flammability test");
            ForcePast(runner, id);
        }
        return true;
    }

    /// The DRY-DISTILLATION collect step (Exp 6): the hot source tube converts
    /// its acetate charge into the product, condensing into the receiver — the
    /// product is PRODUCED, never poured from a bottle. Played by keeping the
    /// source over the flame and running the delivery ticks.
    static bool SimulateVapor(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps,
        Dictionary<LiquidTaskBinding, float> pouredInto, Result res, StringBuilder log)
    {
        VaporCollectController vapor = null;
        foreach (var v in Object.FindObjectsByType<VaporCollectController>(FindObjectsInactive.Include))
            if (v != null && v.VaporTaskId == id) { vapor = v; break; }
        if (vapor == null) return false;

        // The step map was built when the session began; a pool member may have claimed the
        // receiver role since, and the authored twin then advertises nothing. Condense
        // into a vessel that STILL advertises the step, never into a stale first entry
        // (a twin that no longer wants it scolds every drop).
        LiquidTaskBinding recvBind = null;
        foreach (var (b, _) in steps)
        {
            if (b == null || b.ExpectedSteps == null || recvBind != null) continue;
            foreach (var st in b.ExpectedSteps)
                if (st != null && st.taskId == id) { recvBind = b; break; }
        }
        if (recvBind == null && steps.Count > 0) recvBind = steps[0].b;
        var receiver = recvBind != null ? recvBind.GetComponent<LiquidPhysics>() : null;
        if (receiver == null)
        {
            res.bugs.Add(id + ": vapor task has no receiver vessel bound");
            ForcePast(runner, id);
            return true;
        }
        Note("vapor", receiver);
        log.AppendLine("    source " + StateOf(vapor.Source));
        log.AppendLine("    receiver " + StateOf(receiver));
        // Keep the source at temperature (the flame again — the player leaves
        // the tube over the burner while collecting).
        var flame = Object.FindAnyObjectByType<NakedFlameHeat>(FindObjectsInactive.Include);
        var fburner = flame != null ? flame.GetComponent<BurnerController>() : null;
        if (fburner != null) fburner.Ignite();
        float emitted = 0f;
        for (int i = 0; i < 300 && !runner.Graph.IsComplete(id); i++)
        {
            if (flame != null && vapor.Source != null) flame.HeatVessel(vapor.Source, 0.5f);
            if (vapor.EmitTick(receiver)) emitted += VaporMath.MlPerTick;
            runner.Graph.Tick();
        }
        if (fburner != null) fburner.Extinguish();
        pouredInto.TryGetValue(recvBind, out float pv2); pouredInto[recvBind] = pv2 + emitted;
        if (runner.Graph.IsComplete(id))
            log.AppendLine("  ✓ distillate ran over: " + emitted.ToString("0.#") + " ml condensed from "
                           + (vapor.Source != null ? vapor.Source.name : "the source") + " → " + receiver.name);
        else
        {
            res.bugs.Add(id + ": the vapor stream never filled the receiver (emitted "
                         + emitted.ToString("0.#") + " ml; source "
                         + (vapor.Source != null ? vapor.Source.currentTempC.ToString("0") + " C, " + vapor.Source.currentLiquidVolume.ToString("0.#") + " ml left" : "missing") + ")");
            ForcePast(runner, id);
        }
        return true;
    }

    /// The zone-free WEIGH step (Exp 6): the served vessel is set on the bench
    /// balance's pan (ForceLoad — the sim's stand-in for placing it) until the
    /// condition (served AND settled) trips. steps may be null for weigh tasks
    /// with no pours of their own (Exp 7's "collect the distillate and weigh").
    static bool SimulateWeigh(ExperimentRunner runner, string id,
        List<(LiquidTaskBinding b, LiquidTaskBinding.ReagentStep s)> steps, Result res, StringBuilder log)
    {
        VesselWeighTask weigh = null; LiquidPhysics tube = null;
        if (steps != null)
            foreach (var (b, _) in steps)
            {
                var w = b != null ? b.GetComponent<VesselWeighTask>() : null;
                if (w != null && w.TaskId == id) { weigh = w; tube = b.GetComponent<LiquidPhysics>(); break; }
            }
        if (weigh == null)
            foreach (var w in Object.FindObjectsByType<VesselWeighTask>(FindObjectsInactive.Include))
                if (w != null && w.TaskId == id) { weigh = w; tube = w.GetComponent<LiquidPhysics>(); break; }
        if (weigh == null || tube == null) return false;

        var station = Object.FindAnyObjectByType<WeighStation>(FindObjectsInactive.Include);
        if (station == null)
        {
            res.bugs.Add(id + ": no WeighStation on the bench balance — weigh steps are unplayable (run Apply Verb Data)");
            ForcePast(runner, id);
            return true;
        }
        Note("weigh", tube);
        station.ForceLoad(null, tube, 5f);   // rested + settled on the pan
        runner.Graph.Tick();
        bool done = runner.Graph.IsComplete(id);
        MidVerb?.Invoke(station.gameObject, "weigh");
        station.ForceLoad(null, null, 0f);   // lift it back off
        if (done)
            log.AppendLine("  ✓ set " + tube.name + " on the balance — "
                           + (tube.currentLiquidVolume + tube.currentPptVolume).ToString("0.#")
                           + " g settled, step complete");
        else
        {
            res.bugs.Add(id + ": resting the served vessel on the balance did not complete the step");
            ForcePast(runner, id);
        }
        return true;
    }

    /// The filter pour, played honestly: source = a liquid-holding vessel bound
    /// to one of the filter task's PREREQUISITE tasks (the boiled tube);
    /// destination = the vessel bound to a task that lists the filter task as
    /// its prerequisite (the beaker the test happens in). Transferred in real
    /// 0.5 ml PourOut→AddLiquid ticks, exactly like a tilt through the funnel.
    static void SimulateFunnelPour(ExperimentRunner runner, string filterTaskId, StringBuilder log)
    {
        ExperimentTask filterTask = null;
        foreach (var t in runner.Graph.Tasks) if (t.taskId == filterTaskId) filterTask = t;
        if (filterTask == null) return;

        var bindings = Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include);
        LiquidPhysics src = null, dst = null;
        foreach (var b in bindings)
        {
            var lp = b.GetComponent<LiquidPhysics>();
            if (lp == null) continue;
            foreach (var s in b.ExpectedSteps)
            {
                if (s == null) continue;
                if (filterTask.prerequisites.Contains(s.taskId) && lp.currentLiquidVolume > 1f) src = lp;
                foreach (var t in runner.Graph.Tasks)
                    if (t.taskId == s.taskId && t.prerequisites.Contains(filterTaskId)) dst = lp;
            }
        }
        if (src == null || dst == null)
        {
            log.AppendLine("  (no funnel pour modelled: src=" + (src != null ? src.name : "—")
                           + " dst=" + (dst != null ? dst.name : "—") + ")");
            return;
        }
        float moved = 0f;
        for (int i = 0; i < 400 && src.currentLiquidVolume > 0.5f; i++)
        {
            var chem = src.PourOut(0.5f);
            if (chem == null) break;
            dst.AddLiquid(chem, 0.5f);
            moved += 0.5f;
        }
        log.AppendLine("  poured " + moved.ToString("0.#") + " ml of "
                       + (dst.currentChemical != null ? dst.currentChemical.chemicalName : "filtrate")
                       + " through the funnel: " + src.name + " → " + dst.name);
    }

    /// The module's own hidden end product (null when it has none) — set per run.
    static string s_hiddenProduct;
    /// The module's product NAME (DemoMode.ProductFor) — identifies the player's
    /// own purified-product vessel even when no shelf bottle of it exists.
    static string s_moduleProduct;

    /// A bound vessel holding ONLY the module's product = the purified product
    /// (Exp 4's crystallised flask, Exp 5's dried-crystal beaker, Exp 6's
    /// receiver tube). ⚠ A TEST tube whose rule collapsed its ledger back to
    /// the product name (Tollens' resultLiquid = Acetone) can masquerade — so
    /// any vessel that has already RECEIVED a non-product reagent is test
    /// residue, never the product store (Exp 6 round one poured the Schiff
    /// sample out of the silver-laced Tollens tube).
    static bool IsPureProductVessel(LiquidPhysics lp)
    {
        if (lp == null || lp.Ledger.Count != 1 || lp.currentChemical == null
            || lp.currentChemical.chemicalName != s_moduleProduct) return false;
        var b = lp.GetComponent<LiquidTaskBinding>();
        if (b != null)
            foreach (var s in b.ExpectedSteps)
                if (s != null && s.reagent != null && s.reagent.chemicalName != s_moduleProduct
                    && b.AccumulatedFor(s.taskId, s.reagent) > 0.01f) return false;
        return true;
    }

    /// A bound vessel whose story is PRODUCT + WASH WATER only (Exp 7's crude
    /// beaker after the decantation wash: dense chloroform under distilled
    /// water) — decantable, because PourOut yields the dense product layer. Any
    /// OTHER foreign ledger entry marks test residue, never a source.
    static bool IsWashedProductVessel(LiquidPhysics lp)
    {
        if (lp == null || lp.Ledger.Count < 2 || lp.currentChemical == null
            || lp.currentChemical.chemicalName != s_moduleProduct
            || lp.GetComponent<LiquidTaskBinding>() == null) return false;
        foreach (var name in lp.Ledger.Names)
            if (name != s_moduleProduct && name != "Distilled Water") return false;
        return true;
    }

    /// The source a player would pour from. A TASK VESSEL that already holds the
    /// chemical wins over the shelf — that IS the filtrate pour (tube 17's
    /// hydrolysate through the funnel into the beaker), the intended play; the
    /// shelf bottle is the fallback for fresh reagents.
    static LiquidPhysics FindSource(ChemicalData chem, LiquidPhysics destination)
    {
        LiquidPhysics product = null, chillV = null, washed = null, vessel = null, wash = null, shelf = null;
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
        {
            if (lp == null || lp == destination || lp.currentChemical != chem
                || lp.currentLiquidVolume <= 0.01f || lp.name.StartsWith("Vessel_")) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null)
            {
                // Bound-vessel source classes, best first:
                //  1. PURE PRODUCT (ledger collapsed to the module's own product
                //     — Exp 4's crystallised flask, Exp 5's dried-crystal
                //     beaker): every test draw comes from here.
                //  2. CHILL (crystallising) vessel — may still hold mother
                //     liquor (Exp 5's flask after the ice water), which is
                //     exactly what the filter pour decants.
                //  3. HEAT synthesis vessel, single-entry only (the hydrolysate
                //     pour; round one drew "ethanol" out of a finished enol
                //     tube, round two tipped violet TEST RESIDUE — hence the
                //     ledger==1 guard).
                //  4. FERMENTATION wash — a true mixture, no entry guard.
                if (IsPureProductVessel(lp))
                { if (product == null || lp.currentLiquidVolume > product.currentLiquidVolume) product = lp; }
                else if (lp.GetComponent<VesselChillTask>() != null)
                { if (chillV == null || lp.currentLiquidVolume > chillV.currentLiquidVolume) chillV = lp; }
                else if (IsWashedProductVessel(lp))
                { if (washed == null || lp.currentLiquidVolume > washed.currentLiquidVolume) washed = lp; }
                else if (lp.GetComponent<VesselHeatTask>() != null && lp.Ledger.Count == 1)
                { if (vessel == null || lp.currentLiquidVolume > vessel.currentLiquidVolume) vessel = lp; }
                else if (lp.GetComponent<FermentationController>() != null)
                { if (wash == null || lp.currentLiquidVolume > wash.currentLiquidVolume) wash = lp; }
            }
            else
            {
                // A shelf bottle of the module's own END PRODUCT is hidden in
                // play: the player cannot pour from it, so neither may the sim
                // (e.g. bench Ethanol is hidden during Exp 3, so it cannot stand in
                // for the wash).
                if (chem != null && chem.chemicalName == s_hiddenProduct) continue;
                if (shelf == null || lp.currentLiquidVolume > shelf.currentLiquidVolume) shelf = lp;
            }
        }
        return product != null ? product
             : chillV != null ? chillV
             : washed != null ? washed
             : vessel != null ? vessel
             : wash != null ? wash : shelf;
    }

    /// Sustained-verb (sim) and drop-zone stations. Returns false when no station
    /// owns the task at all.
    static bool SimulateStation(ExperimentRunner runner, string id, ZoneSimStation[] sims,
        ExperimentTaskStation[] zones, TemperatureSim[] temps, LabItem[] labItems,
        Result res, StringBuilder log)
    {
        foreach (var st in sims)
        {
            if (st == null || st.TaskId != id) continue;
            // FILTER stations: the player POURS the previous step's vessel
            // through the funnel into the receiver below (LiquidPassthrough lets
            // the stream through). Model that pour for real: the receiving
            // vessel must actually hold the filtrate or the follow-up chemical
            // test has nothing to react with (e.g. the FeCl3 violet).
            if (st.Kind == StationSim.Filter)
                SimulateFunnelPour(runner, id, log);
            // Heat stations gate on a LIT burner (ignition gate) — the player's
            // match-strike becomes an Ignite() call here. Snuffed after, so the
            // scene isn't left with burning burners.
            var burners = Object.FindObjectsByType<BurnerController>(FindObjectsInactive.Include);
            if (st.Kind == StationSim.Heat)
            {
                foreach (var bu in burners) if (bu != null) bu.Ignite();
                log.AppendLine("  (lit the burner)");
            }
            // Occupied + lit: 300 simulated seconds at 2 Hz.
            for (int i = 0; i < 600 && !runner.Graph.IsComplete(id); i++)
            {
                st.Drive(0.5f, true);
                foreach (var ts in temps) if (ts != null) ts.Tick(0.5f);
                runner.Graph.Tick();
            }
            // Vacate the zone (the player takes the vessel out to move on) — one
            // unoccupied Drive latches heating OFF, else the bath keeps cooking
            // through the NEXT task's sim ticks and logs a phantom Overheat.
            st.Drive(0.5f, false);
            if (st.Kind == StationSim.Heat)
                foreach (var bu in burners) if (bu != null) bu.Extinguish();
            if (runner.Graph.IsComplete(id))
                log.AppendLine("  ✓ " + st.Kind + " sim ran to target (station occupied)");
            else
            {
                res.bugs.Add(id + ": " + st.Kind + " station never reached its target in 300 sim-seconds");
                log.AppendLine("  ✗ sim stalled");
                ForcePast(runner, id);   // force past to keep exploring downstream
            }
            // The boil itself: the bath at target heats the vessels bound to
            // this task, which is what fires their high-threshold pending
            // reactions (the aspirin hydrolysis needs 90 C — only here).
            if (st.Kind == StationSim.Heat)
                foreach (var b2 in Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include))
                    foreach (var s2 in b2.ExpectedSteps)
                    {
                        if (s2 == null || s2.taskId != id) continue;
                        var vlp = b2.GetComponent<LiquidPhysics>();
                        if (vlp != null)
                        {
                            bool had = vlp.HasPendingReaction;
                            var pr = vlp.PendingRule;
                            vlp.SetTemperature(100f);
                            if (had && !vlp.HasPendingReaction)
                                log.AppendLine("  ✓ boil fired " + (pr != null ? pr.name : "the pending reaction")
                                               + " in " + vlp.name);
                            else if (had)
                                res.bugs.Add(id + ": boiled " + vlp.name + " to 100 C but "
                                             + (pr != null ? pr.name : "its reaction") + " still pending");
                        }
                        break;
                    }
            return true;
        }
        foreach (var st in zones)
        {
            if (st == null || st.TaskId != id) continue;
            bool served = string.IsNullOrEmpty(st.RequiredItemId);
            foreach (var li in labItems) if (st.AcceptsItem(li)) { served = true; break; }
            if (!served)
            {
                res.bugs.Add(id + ": station wants item '" + st.RequiredItemId + "' but NOTHING on the bench has that itemId");
                log.AppendLine("  ✗ required item missing from the scene");
                ForcePast(runner, id);
            }
            else
            {
                st.Activate();
                log.AppendLine("  ✓ zone station activated"
                               + (string.IsNullOrEmpty(st.RequiredItemId) ? "" : " (bench has '" + st.RequiredItemId + "')"));
            }
            return true;
        }
        return false;
    }

    /// The run REALLY drained the bottles (PourOut per action), so sufficiency
    /// was proven by playing: a dry source mid-step already warned. This audit
    /// reports what the full run left in each bottle — a small remainder is the
    /// early starvation signal for a player who wastes a few pours.
    static void AuditSupplies(Dictionary<ChemicalData, float> demand, LabItem[] labItems,
                              Result res, StringBuilder log)
    {
        log.AppendLine("\n--- supply audit (post-run: consumed vs left in the bottle) ---");
        var sources = Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include);
        foreach (var kv in demand)
        {
            LiquidPhysics best = null;
            foreach (var lp in sources)
                if (lp != null && lp.currentChemical == kv.Key
                    && kv.Key.chemicalName != s_hiddenProduct   // hidden end product: not a supply
                    && (best == null || lp.currentLiquidVolume > best.currentLiquidVolume)
                    && !lp.name.StartsWith("Vessel_") && lp.GetComponent<LiquidTaskBinding>() == null)
                    best = lp;   // a task vessel is a destination, never the shelf supply
            if (best == null)
                res.bugs.Add("no bench source holds '" + kv.Key.chemicalName + "' (run consumed "
                             + kv.Value.ToString("0.#") + ") — the player cannot obtain it");
            else if (best.currentLiquidVolume < kv.Value * 0.5f)
                res.warnings.Add("'" + kv.Key.chemicalName + "': one perfect run left only "
                                 + best.currentLiquidVolume.ToString("0.#") + " in " + best.name
                                 + " — little margin for a wasteful player");
            log.AppendLine("  " + kv.Key.chemicalName + ": consumed " + kv.Value.ToString("0.#")
                           + (best == null ? "  ✗ NO SOURCE" : "  · " + best.name + " has " + best.currentLiquidVolume.ToString("0.#") + " left"));
        }
    }

    /// A correct delivery the player cannot SEE reads as a bug in VR (e.g. a 0.5%
    /// fill), so flag any task vessel that ENDS the run under 4% of
    /// its capacity. Reads the LIVE vessel (funnel pours and reactions included),
    /// not the scripted-delivery tally, which under-counted the filtrate beaker.
    static void AuditVisibility(Dictionary<LiquidTaskBinding, float> pouredInto,
                                Result res, StringBuilder log)
    {
        foreach (var kv in pouredInto)
        {
            var lp = kv.Key != null ? kv.Key.GetComponent<LiquidPhysics>() : null;
            if (lp == null || lp.maxVolume <= 0f) continue;
            // Judge the PEAK the player saw — a vessel deliberately emptied later
            // (the hydrolysis tube pours through the funnel) is not "invisible".
            float end = lp.currentLiquidVolume + lp.currentPptVolume;
            float peak = Mathf.Max(end, kv.Value);
            float frac = peak / lp.maxVolume;
            if (frac > 0f && frac < 0.04f)
                res.warnings.Add(kv.Key.name + ": never exceeds " + (frac * 100f).ToString("0.#")
                                 + "% of capacity (" + peak.ToString("0.#") + "/" + lp.maxVolume + " ml) — invisible in VR");
        }
    }

    // ---- the VERB CONTRACT, mirrored ---------------------------------------
    static bool IsSolid(LiquidTaskBinding.ReagentStep s)
        => s.reagent != null && (s.reagent.state == PhysicalState.Solid || s.reagent.state == PhysicalState.Powder);
    // A player picks the tool by scale: the SCOOPULA (2 g) for a bulk weigh-out
    // (Exp 3's 12 g sugar / 4 g yeast — 120 spatula dips would be absurd), the
    // fine porcelain SPATULA (0.1 g) only for sub-gram amounts (Exp 2's 0.1 g).
    static float IncrementFor(LiquidTaskBinding.ReagentStep s)
        => IsSolid(s) ? (s.requiredMl >= 2f ? ScoopMath.GramsPerScoop : ScoopMath.GramsPerSpatula)
                      : DropperMath.MlPerSqueeze;
    static int ActionsFor(LiquidTaskBinding.ReagentStep s)
        => s.requiredMl <= 0f ? 1 : Mathf.CeilToInt(s.requiredMl / IncrementFor(s) - 0.0001f);
    static string VerbFor(LiquidTaskBinding.ReagentStep s)
        => IsSolid(s) ? (s.requiredMl >= 2f ? "scoopula-dip" : "spatula-dip") : "squeeze/pour";
    static string UnitFor(LiquidTaskBinding.ReagentStep s) => IsSolid(s) ? " g" : " ml";
}
#endif

// touch: suite-autorun trigger
