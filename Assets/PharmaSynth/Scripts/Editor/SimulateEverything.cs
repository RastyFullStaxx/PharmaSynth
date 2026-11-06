#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// ONE command that plays the whole game and answers one question: is every experiment
/// actually doable right now?
///
/// It adds no simulation of its own. `SimulatedRun.Run` and `SimulatedCampaign.Run`
/// already return structured results; this is the single entry point and the single
/// verdict over them and the two tutorial audits.
///
/// Everything lands in Logs/simulate-everything.txt, worst module FIRST: a report you
/// have to scroll to find the failure in is a report that gets skimmed.
public static class SimulateEverything
{
    /// All NINE, tutorial included. Methane is deliberately first: it is the first thing
    /// a player ever touches.
    static readonly string[] AllModules =
    {
        "tutorial-methane",
        "prelim-chemical-compounding", "prelim-ethyl-alcohol",
        "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
        "final-benzamide", "final-winemaking",
    };

    [MenuItem("Tools/PharmaSynth/Simulate Everything (full playability check)")]
    public static void RunMenu()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SimAll] exit Play mode first."); return; }
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        if (builder == null)
        {
            Debug.LogError("[SimAll] no ExperimentSceneBuilder — open SampleScene.unity first.");
            return;
        }

        var report = new StringBuilder();
        var detail = new StringBuilder();
        var rows = new List<Row>();
        int bugs = 0, warns = 0;

        // ---- 1. every module, played end to end -----------------------------------
        foreach (var id in AllModules)
        {
            var log = new StringBuilder();
            SimulatedRun.Result r = null;
            // A module that throws must not take the other eight down with it — the
            // whole point of the battery is one run that reports on everything.
            try { r = SimulatedRun.Run(id, log); }
            catch (System.Exception e) { log.AppendLine("EXCEPTION: " + e); }

            var row = new Row { id = id };
            if (r == null)
            {
                row.verdict = "COULD NOT RUN";
                row.bugs.Add("the simulator could not start this module (see transcript)");
            }
            else
            {
                row.tasks = r.completedTasks; row.totalTasks = r.totalTasks; row.mistakes = r.mistakes;
                row.bugs.AddRange(r.bugs); row.warnings.AddRange(r.warnings);
                row.verdict = r.Clean ? "CLEAN" : "FAIL";
            }
            bugs += row.bugs.Count; warns += row.warnings.Count;
            rows.Add(row);

            // Keep the per-module transcripts too: the table says WHICH step broke, the
            // transcript says what the player was doing when it did.
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/simrun-" + id + ".txt", log.ToString());
        }

        // ---- 2. the campaign loop around them -------------------------------------
        var campLog = new StringBuilder();
        SimulatedCampaign.Result camp = null;
        try { camp = SimulatedCampaign.Run(campLog); }
        catch (System.Exception e) { campLog.AppendLine("EXCEPTION: " + e); }
        File.WriteAllText("Logs/simcampaign.txt", campLog.ToString());

        // ---- 3. tutorial guidance coverage ----------------------------------------
        // Cheap to fold in and it fails LOUDLY when a module's tasks change: a step with
        // no target is a step Tutorial Mode cannot guide anyone through.
        var guidance = new StringBuilder();
        int blindSteps = TutorialCoverage(guidance);

        // ---- 4. can the player physically reach what each step needs? --------------
        var reach = new StringBuilder();
        var reachFindings = ReachabilityAudit.RunAll(reach);

        // ---- 4b. can each module even be STARTED without starving? -----------------
        var supply = new StringBuilder();
        var supplyFindings = SupplyAtStart(supply);

        // ---- 5. the player who gets it wrong --------------------------------------
        var mis = new StringBuilder();
        var misFindings = SimulatedMisplay.RunAll(mis);

        // ---- the report -----------------------------------------------------------
        // Worst first. A clean module needs one line; a broken one needs all of them.
        rows.Sort((a, b) => b.bugs.Count.CompareTo(a.bugs.Count));

        bool allClean = bugs == 0 && blindSteps == 0 && reachFindings.Count == 0
                        && misFindings.Count == 0 && supplyFindings.Count == 0
                        && camp != null && camp.Clean;

        report.AppendLine("=== PharmaSynth — full playability check ===");
        report.AppendLine();
        report.AppendLine("  module                        tasks   mistakes  bugs  warns  verdict");
        report.AppendLine("  " + new string('-', 74));
        foreach (var r in rows)
            report.AppendLine("  " + r.id.PadRight(30)
                              + (r.totalTasks > 0 ? (r.tasks + "/" + r.totalTasks) : "-").PadRight(8)
                              + r.mistakes.ToString().PadRight(10)
                              + r.bugs.Count.ToString().PadRight(6)
                              + r.warnings.Count.ToString().PadRight(7)
                              + r.verdict);
        report.AppendLine();
        report.AppendLine("  campaign loop      : " + (camp == null ? "COULD NOT RUN"
            : camp.Clean ? "CLEAN — " + camp.modulesPassed + "/8 passed, campaign "
                           + (camp.campaignComplete ? "COMPLETE" : "NOT complete")
            : camp.findings.Count + " finding(s), " + camp.modulesPassed + "/8 passed"));
        report.AppendLine("  tutorial guidance  : " + (blindSteps == 0 ? "every step has a target"
                                                      : blindSteps + " step(s) with NOTHING to point at"));
        report.AppendLine("  reachability       : " + (reachFindings.Count == 0 ? "everything reachable"
                                                      : reachFindings.Count + " finding(s)"));
        report.AppendLine("  imperfect play     : " + (misFindings.Count == 0 ? "every recovery path holds"
                                                      : misFindings.Count + " finding(s)"));
        report.AppendLine("  supply at start    : " + (supplyFindings.Count == 0
                              ? "no module starves before you begin"
                              : supplyFindings.Count + " module(s) starve at 0% progress"));
        report.AppendLine();
        report.AppendLine(allClean
            ? "  VERDICT: CLEAN — all 9 experiments are playable end to end, correct play is never"
              + "\n           punished, every recovery path holds, and nothing is out of reach."
            : "  VERDICT: " + (bugs + reachFindings.Count + misFindings.Count + blindSteps
                                 + supplyFindings.Count)
              + " ISSUE(S) — details below, worst module first.");

        // Detail sections, only for what actually has something to say.
        foreach (var r in rows)
        {
            if (r.bugs.Count == 0 && r.warnings.Count == 0) continue;
            detail.AppendLine();
            detail.AppendLine("--- " + r.id + " (" + r.verdict + ") ---");
            foreach (var b in r.bugs) detail.AppendLine("  BUG  " + b);
            foreach (var w in r.warnings) detail.AppendLine("  WARN " + w);
            detail.AppendLine("  transcript → Logs/simrun-" + r.id + ".txt");
        }
        if (camp != null && camp.findings.Count > 0)
        {
            detail.AppendLine();
            detail.AppendLine("--- campaign loop ---");
            foreach (var f in camp.findings) detail.AppendLine("  FINDING " + f);
            detail.AppendLine("  transcript → Logs/simcampaign.txt");
        }
        if (blindSteps > 0) { detail.AppendLine(); detail.Append(guidance); }
        if (reachFindings.Count > 0) { detail.AppendLine(); detail.Append(reach); }
        if (misFindings.Count > 0) { detail.AppendLine(); detail.Append(mis); }
        if (supplyFindings.Count > 0) { detail.AppendLine(); detail.Append(supply); }

        report.Append(detail);
        report.AppendLine();
        report.AppendLine("--- what this CANNOT tell you -------------------------------------------");
        report.AppendLine("  Everything above is mechanism. It cannot feel a grab, judge whether a");
        report.AppendLine("  glow reads as a hint, or notice that a label is unreadable at arm's");
        report.AppendLine("  length. The headset pass is still the only answer to how it FEELS.");
        report.AppendLine();
        report.AppendLine("  Every simulator mutates the open scene by design — REOPEN SampleScene");
        report.AppendLine("  before doing anything else, so none of this gets saved.");

        File.WriteAllText("Logs/simulate-everything.txt", report.ToString());
        Debug.Log((allClean ? "<color=#4CD07D>" : "<color=#FF7A6B>")
                  + "[SimAll] " + (allClean ? "CLEAN — all 9 experiments playable end to end"
                                            : (bugs + reachFindings.Count + misFindings.Count + blindSteps
                                               + supplyFindings.Count)
                                              + " issue(s) across 9 modules")
                  + "</color>\n  full report → Logs/simulate-everything.txt"
                  + "\n  ⚠ reopen SampleScene — the battery mutated it.");
    }

    class Row
    {
        public string id, verdict = "-";
        public int tasks, totalTasks, mistakes;
        public readonly List<string> bugs = new List<string>();
        public readonly List<string> warnings = new List<string>();
    }

    /// Can each module be STARTED without the supply monitor immediately declaring
    /// starvation ("Not enough reagents left to finish. Restart the period?" at
    /// Progress 0% on a fresh run, which restarting does not help)?
    ///
    /// This is the monitor's OWN arithmetic, run on a freshly built stage with nothing
    /// consumed. The other supply checks ask whether a bottle runs dry during a run;
    /// this asks whether the bench can satisfy the module at all before the player has
    /// touched anything.
    ///
    /// ⚠ The monitor deliberately counts as AVAILABLE only vessels with NO
    /// LiquidTaskBinding — so a reagent whose only source in the scene is itself a task
    /// vessel is invisible to it, and the module starves permanently at 0%.
    static List<string> SupplyAtStart(StringBuilder log)
    {
        var findings = new List<string>();
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var monitor = Object.FindAnyObjectByType<ReagentSupplyMonitor>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        if (builder == null || runner == null || monitor == null || lib == null)
        {
            log.AppendLine("--- supply at start: skipped (need builder + runner + ReagentSupplyMonitor) ---");
            return findings;
        }

        log.AppendLine("--- supply at start: can the bench satisfy each module at 0% progress? ---");
        var snapshot = new List<(LiquidPhysics lp, ChemicalData chem, float ml)>();
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
            snapshot.Add((lp, lp.currentChemical, lp.currentLiquidVolume));

        try
        {
            foreach (var id in AllModules)
            {
                var module = lib.Get(id);
                if (module == null) continue;
                builder.Build(id);
                runner.SetModule(module);
                runner.StartExperiment();

                var short_ = monitor.EvaluateNow();
                if (short_.Count == 0) { log.AppendLine("  ok   " + id); runner.Abort(); continue; }

                foreach (var taskId in short_)
                {
                    string chem = "?"; float need = 0f;
                    foreach (var b in Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include))
                    {
                        if (b == null || b.ExpectedSteps == null) continue;
                        foreach (var st in b.ExpectedSteps)
                            if (st != null && st.taskId == taskId && st.reagent != null)
                            { chem = st.reagent.chemicalName; need = st.requiredMl; }
                    }
                    // Count exactly what the monitor counts: unbound vessels of that name.
                    float avail = 0f; int bottles = 0;
                    foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
                    {
                        if (lp == null || lp.currentChemical == null || lp.currentLiquidVolume <= 0f) continue;
                        if (lp.currentChemical.chemicalName != chem) continue;
                        if (lp.GetComponent<LiquidTaskBinding>() != null) continue;
                        avail += lp.currentLiquidVolume; bottles++;
                    }
                    string f = id + " · " + taskId + ": STARVED BEFORE THE PLAYER STARTS — needs "
                               + need.ToString("0.#") + " of '" + chem + "' but the bench offers "
                               + avail.ToString("0.#") + " across " + bottles + " unbound bottle(s)";
                    log.AppendLine("  BUG  " + f);
                    findings.Add(f);
                }
                runner.Abort();
            }
        }
        finally
        {
            foreach (var (lp, chem, ml) in snapshot) if (lp != null) lp.SetContents(chem, ml);
        }
        if (findings.Count == 0) log.AppendLine("  -> every module can be started.");
        return findings;
    }

    /// Build each module's stage and ask whether every step resolves to an object.
    /// Same question as Audit Tutorial Targets, folded in so the battery is one click.
    static int TutorialCoverage(StringBuilder log)
    {
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        if (builder == null || lib == null) return 0;

        log.AppendLine("--- tutorial guidance coverage ---");
        int blind = 0;
        foreach (var id in AllModules)
        {
            var module = lib.Get(id);
            if (module == null || module.graphTasks == null) continue;
            builder.Build(id);
            TutorialTargets.Build();
            TutorialTargets.AuditAgainst(module.graphTasks);
            if (TutorialTargets.LastUnresolved.Count == 0) continue;
            blind += TutorialTargets.LastUnresolved.Count;
            log.AppendLine("  " + id + ": no target for "
                           + string.Join(", ", TutorialTargets.LastUnresolved));
        }
        TaskTargetRegistry.Clear();
        return blind;
    }
}
#endif
