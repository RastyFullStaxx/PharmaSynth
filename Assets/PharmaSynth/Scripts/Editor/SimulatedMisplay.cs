#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Simulates the player who gets it WRONG.
///
/// Every other simulator plays a flawless run, which answers "does correct play work?".
/// It does not answer the question that actually decides whether nine experiments are
/// doable by a student: **after a mistake, can they still finish?** A contaminated
/// vessel, an out-of-order attempt or an exhausted bottle that quietly makes a run
/// unfinishable looks identical to a clean sim — the perfect path never touches it.
///
/// Each probe asserts two things, and the second is the important one: the mistake is
/// reported AND the run remains completable afterwards.
public static class SimulatedMisplay
{
    [MenuItem("Tools/PharmaSynth/Simulate Imperfect Play")]
    public static void RunMenu()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Misplay] exit Play mode first."); return; }
        var log = new StringBuilder();
        var findings = RunAll(log);
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/simulate-misplay.txt", log.ToString());
        Debug.Log((findings.Count == 0 ? "<color=#4CD07D>" : "<color=#FF7A6B>")
                  + "[Misplay] " + (findings.Count == 0 ? "every recovery path holds"
                                                        : findings.Count + " finding(s)")
                  + "</color>\n  → Logs/simulate-misplay.txt"
                  + "\n  ⚠ reopen SampleScene — this mutated it.");
    }

    public static List<string> RunAll(StringBuilder log)
    {
        var findings = new List<string>();
        log.AppendLine("--- imperfect play: after a mistake, can the player still finish? ---");

        // The FSM probes are PURE — no scene, no stage, no restore. Run them first so a
        // scene problem later cannot cost us the restart-matrix answer.
        RestartMatrix(findings, log);

        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        if (builder == null || runner == null || lib == null)
        {
            log.AppendLine("  (scene probes skipped — open SampleScene.unity first)");
            return findings;
        }

        // The probes DRAIN real bottles like the run sim does; snapshot and restore or an
        // edit-mode pass permanently corrupts the saved scene's supplies.
        var snapshot = new List<(LiquidPhysics lp, ChemicalData chem, float ml, ChemicalData ppt, float pptMl)>();
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            snapshot.Add((lp, lp.currentChemical, lp.currentLiquidVolume, lp.currentPptChemical, lp.currentPptVolume));

        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition",
                         new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.graphTasks == null || def.graphTasks.Count == 0) continue;
                var module = lib.Get(def.moduleId);
                if (module == null) continue;

                runner.SetModule(module);

                // ⚠ A fresh STAGE per probe, not merely a fresh attempt.
                //
                // StartExperiment rebuilds the task graph but NOT the scene, and a
                // LiquidTaskBinding's accumulator lives on the component, so a reused
                // stage hands probe 2 everything probe 1 poured. The starvation probe
                // would then drain the bench, find the step already delivered, and blame
                // the supply monitor for correctly reporting no shortfall. A real Retry
                // rebuilds the stage (ExperimentLauncher), so this matches the game rather
                // than working around it.
                void Fresh() { builder.Build(def.moduleId); runner.StartExperiment(); }

                Fresh(); WrongReagentProbe(runner, def.moduleId, findings, log); runner.Abort();
                Fresh(); WrongOrderProbe(runner, def.moduleId, findings, log); runner.Abort();
                Fresh(); StarvationProbe(runner, def.moduleId, findings, log); runner.Abort();
            }
        }
        finally
        {
            foreach (var (lp, chem, ml, ppt, pptMl) in snapshot)
            {
                if (lp == null) continue;
                lp.SetContents(chem, ml);
                lp.currentPptChemical = ppt; lp.currentPptVolume = pptMl;
            }
        }

        if (findings.Count == 0) log.AppendLine("  -> every probe recovered; no mistake can strand a run.");
        return findings;
    }

    // ---- probe 1: the wrong bottle ------------------------------------------------

    /// Pour something the step does NOT want into its vessel, then pour the right thing.
    /// The mistake must be reported and the step must still be completable — a binding
    /// that refuses to accept the correct reagent after contamination is a hard stop with
    /// no message, the worst failure this whole harness exists to find.
    static void WrongReagentProbe(ExperimentRunner runner, string moduleId,
                                  List<string> findings, StringBuilder log)
    {
        var binding = FirstPourStep(runner, out var step);
        if (binding == null || step == null) return;

        var wrong = FindWrongSource(binding, step);
        if (wrong == null) { log.AppendLine("  " + moduleId + ": no wrong bottle to test with — skipped"); return; }

        var vessel = binding.GetComponent<LiquidPhysics>();
        if (vessel == null) return;

        int before = runner.MistakeCount;
        string wrongName = wrong.currentChemical != null ? wrong.currentChemical.chemicalName : "?";
        // Through the bottle, never the binding: a direct HandleReagent call can report
        // a module CLEAN while a real player is hard-stuck.
        var drawn = wrong.PourOut(5f);
        if (drawn != null) vessel.AddLiquid(drawn, 5f);

        if (runner.MistakeCount == before)
        {
            string f = moduleId + " · " + step.taskId + ": pouring " + wrongName
                       + " into " + binding.name + " recorded NO mistake — wrong reagents go unnoticed";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }
        if (runner.Graph.IsComplete(step.taskId))
        {
            string f = moduleId + " · " + step.taskId + ": completed on the WRONG reagent (" + wrongName + ")";
            log.AppendLine("  BUG  " + f); findings.Add(f);
            return;
        }

        // …and now the recovery: does the right reagent still finish the step?
        var right = FindSourceOf(step.reagent, vessel);
        if (right == null) return;                       // supply audit's job, not this probe's
        float need = Mathf.Max(step.requiredMl, 1f) + 2f;
        var good = right.PourOut(need);
        if (good != null) vessel.AddLiquid(good, need);

        // ⚠ Recovery is measured on the STEP, never on the task.
        //
        // LiquidTaskBinding accumulates per step because one task may name several
        // reagents, and a completesTask:false step is finished later by a weigh station or
        // rack group. Asserting task completion after pouring ONE of its reagents would
        // blame the game for the probe's own partial play.
        //
        // What contamination could really break is the accumulator: if the wrong pour made
        // the binding stop counting the right reagent, the player is stranded. That is the
        // question, and it is answerable from one step.
        float got = binding.AccumulatedFor(step.taskId, step.reagent);
        bool recovered = LiquidTaskBinding.MetThreshold(got, step.requiredMl);

        if (!recovered)
        {
            string f = moduleId + " · " + step.taskId + ": after a wrong pour the binding STOPPED "
                       + "counting " + step.reagent.chemicalName + " — the player cannot recover"
                       + " [poured " + need.ToString("0.0") + ", needs " + step.requiredMl.ToString("0.0")
                       + ", accumulated " + got.ToString("0.0")
                       + ", vessel now holds " + (vessel.currentChemical != null ? vessel.currentChemical.chemicalName : "nothing")
                       + " " + vessel.currentLiquidVolume.ToString("0.0") + "]";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }
        else log.AppendLine("  ok   " + moduleId + " · " + step.taskId
                            + ": wrong pour flagged, correct reagent still counts ("
                            + got.ToString("0.0") + "/" + step.requiredMl.ToString("0.0") + ")");
    }

    // ---- probe 2: the wrong order -------------------------------------------------

    /// Attempt a step whose prerequisites are not met. The runner's own error-matrix
    /// wiring must record WrongStep and refuse the completion.
    static void WrongOrderProbe(ExperimentRunner runner, string moduleId,
                                List<string> findings, StringBuilder log)
    {
        string blocked = null;
        var available = new HashSet<string>();
        foreach (var t in runner.Graph.AvailableTasks()) available.Add(t.taskId);
        foreach (var t in runner.Module.graphTasks)
        {
            if (t == null || available.Contains(t.taskId) || runner.Graph.IsComplete(t.taskId)) continue;
            blocked = t.taskId; break;
        }
        if (blocked == null) return;                     // a fully parallel graph — nothing to test

        int before = runner.MistakeCount;
        runner.CompleteTask(blocked);
        if (runner.Graph.IsComplete(blocked))
        {
            string f = moduleId + " · " + blocked + ": completed OUT OF ORDER — prerequisites are not enforced";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }
        else if (runner.MistakeCount == before)
        {
            string f = moduleId + " · " + blocked + ": an out-of-order attempt was silently ignored — "
                       + "the player gets no signal that they are on the wrong step";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }
        else log.AppendLine("  ok   " + moduleId + " · " + blocked + ": out-of-order attempt refused + flagged");
    }

    // ---- probe 3: the empty bottle ------------------------------------------------

    /// Drain every bench source of a required reagent. The monitor must SEE it (campaign
    /// offers the restart on this signal), and RefillSourceBottles must put it back —
    /// that call is the whole of Tutorial Mode's "nothing can dead-end" guarantee.
    static void StarvationProbe(ExperimentRunner runner, string moduleId,
                                List<string> findings, StringBuilder log)
    {
        var monitor = Object.FindAnyObjectByType<ReagentSupplyMonitor>();
        if (monitor == null) return;

        var binding = FirstPourStep(runner, out var step);
        if (binding == null || step == null || step.reagent == null) return;
        if (runner.Graph.IsComplete(step.taskId)) return;          // probe 1 already finished it

        // ⚠ By NAME, not by asset reference: ReagentSupplyMath sums availability into a
        // dictionary keyed on chemicalName, so a second asset with the same name still
        // supplies the step. A reference-matched drain left those bottles full, and the
        // probe then blamed the monitor for correctly seeing no shortfall.
        string want = step.reagent.chemicalName;
        var drained = new List<LiquidPhysics>();
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lp == null || lp.currentChemical == null) continue;
            if (lp.currentChemical.chemicalName != want) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;   // a target, not a supply
            lp.SetContents(lp.currentChemical, 0f);
            drained.Add(lp);
        }
        if (drained.Count == 0) return;

        var shortfalls = monitor.EvaluateNow();
        if (!shortfalls.Contains(step.taskId))
        {
            string f = moduleId + " · " + step.taskId + ": all " + drained.Count + " bottle(s) of "
                       + want + " are empty but the supply monitor sees no shortfall — the player "
                       + "would hunt a bench that can never satisfy the step"
                       + " [monitor returned: " + (shortfalls.Count == 0 ? "nothing" : string.Join(", ", shortfalls))
                       + "; step needs " + step.requiredMl.ToString("0.0")
                       + ", already delivered " + binding.AccumulatedFor(step.taskId, step.reagent).ToString("0.0")
                       + ", task complete=" + runner.Graph.IsComplete(step.taskId) + "]";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }

        int refilled = ReagentSupplyMonitor.RefillSourceBottles();
        if (refilled == 0)
        {
            string f = moduleId + ": RefillSourceBottles restocked NOTHING after a full drain — "
                       + "Tutorial Mode's no-dead-end guarantee does not hold";
            log.AppendLine("  BUG  " + f); findings.Add(f);
        }
        else log.AppendLine("  ok   " + moduleId + " · " + step.taskId
                            + ": starvation detected, " + refilled + " bottle(s) restocked on refill");
    }

    // ---- probe 4: the restart matrix ----------------------------------------------

    /// Every documented exit from a run (gameplay-flow §9), driven through the REAL pure
    /// FSM. `Fire` returns false on an illegal move, so a broken gate is caught here
    /// rather than narrated over.
    ///
    /// Each probe walks the gate from Blocked the way a player does, rather than forcing
    /// the state — so the walk itself proves the path INTO the state still works, and the
    /// FSM needs no test-only setter.
    static void RestartMatrix(List<string> findings, StringBuilder log)
    {
        log.AppendLine("  restart matrix (gameplay-flow §9):");

        Check("Retry after a failed review", GateState.ScoreReview, GateEvent.RetryRequested,
              GateState.Loading, findings, log);
        Check("Choose Another after a failure", GateState.ScoreReview, GateEvent.AbandonRun,
              GateState.Blocked, findings, log);
        Check("Continue after a pass", GateState.ScoreReview, GateEvent.ContinueAfterPass,
              GateState.Returning, findings, log);
        Check("Restart on reagent starvation", GateState.SupplyPrompt, GateEvent.RestartConfirmed,
              GateState.Loading, findings, log);
        Check("Keep trying on starvation", GateState.SupplyPrompt, GateEvent.Dismiss,
              GateState.Running, findings, log);
    }

    /// Walk the gate from Blocked to `target` using only legal moves. Returns null if the
    /// walk itself broke, which is a finding in its own right.
    static GatekeeperModel DriveTo(GateState target, out string failure)
    {
        failure = null;
        var m = new GatekeeperModel();
        System.Func<string, bool> anything = _ => true;

        if (!m.Fire(GateEvent.Approach)) { failure = "cannot approach the door"; return null; }
        if (!m.Fire(GateEvent.PickCampaign)) { failure = "cannot pick Campaign"; return null; }
        if (!m.Fire(GateEvent.ExplainDone)) { failure = "cannot pass the explainer"; return null; }
        if (!m.ChooseEpisode(ExperimentPeriod.Prelim, anything, _ => "x"))
        { failure = "cannot open a period at the picker"; return null; }
        if (!m.ChooseModule("x", anything)) { failure = "cannot pick a module"; return null; }
        foreach (var e in new[] { GateEvent.Coated, GateEvent.Ready, GateEvent.Loaded,
                                  GateEvent.ProceedConfirmed, GateEvent.CrossedThreshold })
            if (!m.Fire(e)) { failure = "stuck before Running at " + m.State + " on " + e; return null; }
        if (m.State != GateState.Running) { failure = "did not reach Running (at " + m.State + ")"; return null; }
        if (target == GateState.Running) return m;

        if (target == GateState.SupplyPrompt)
        {
            if (!m.Fire(GateEvent.SupplyExhausted)) { failure = "starvation never opens the prompt"; return null; }
            return m;
        }

        // ScoreReview: through the whole review chain, exactly as a finished run does.
        foreach (var e in new[] { GateEvent.TestsDone, GateEvent.QuizBegin, GateEvent.Graded })
            if (!m.Fire(e)) { failure = "stuck before the grade screen at " + m.State + " on " + e; return null; }
        if (m.State != target) { failure = "landed in " + m.State + ", wanted " + target; return null; }
        return m;
    }

    static void Check(string what, GateState from, GateEvent e, GateState expected,
                      List<string> findings, StringBuilder log)
    {
        var model = DriveTo(from, out string failure);
        if (model == null)
        {
            string f = "restart matrix — " + what + ": could not even reach " + from + " — " + failure;
            log.AppendLine("    BUG  " + f); findings.Add(f);
            return;
        }
        bool ok = model.Fire(e);
        if (!ok || model.State != expected)
        {
            string f = "restart matrix — " + what + ": " + from + " + " + e + " landed in "
                       + (ok ? model.State.ToString() : "REFUSED") + ", expected " + expected;
            log.AppendLine("    BUG  " + f); findings.Add(f);
        }
        else log.AppendLine("    ok   " + what + " → " + expected);
    }

    // ---- shared lookups ------------------------------------------------------------

    /// The first still-incomplete pour step of the run — the one a fumbling player is
    /// most likely to be standing in front of.
    static LiquidTaskBinding FirstPourStep(ExperimentRunner runner, out LiquidTaskBinding.ReagentStep step)
    {
        step = null;
        if (runner == null || runner.Graph == null) return null;
        foreach (var t in runner.Graph.AvailableTasks())
            foreach (var b in Object.FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b == null || b.ExpectedSteps == null) continue;
                foreach (var s in b.ExpectedSteps)
                {
                    if (s == null || s.taskId != t.taskId || s.reagent == null || s.requiredMl <= 0f) continue;
                    step = s; return b;
                }
            }
        return null;
    }

    /// A stocked bench bottle holding something this binding does NOT expect.
    static LiquidPhysics FindWrongSource(LiquidTaskBinding binding, LiquidTaskBinding.ReagentStep step)
    {
        // Exclude what ANY role of a pooled tube takes: pouring Exp 2's glycerol into a free
        // tube makes it the glycerol tube (correct play), which would otherwise be reported
        // as a silent wrong pour whenever the unordered scene search picks it.
        var expected = new HashSet<ChemicalData>(binding.AllRoleReagents());
        foreach (var s in binding.ExpectedSteps) if (s != null && s.reagent != null) expected.Add(s.reagent);

        var all = new List<LiquidPhysics>(Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include));
        all.Sort((a, b) => string.CompareOrdinal(a != null ? a.name : "", b != null ? b.name : ""));   // stable run to run
        foreach (var lp in all)
        {
            if (lp == null || lp.currentChemical == null || lp.currentLiquidVolume < 10f) continue;
            if (expected.Contains(lp.currentChemical)) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;       // a target, not a supply
            // Never a HAZARDOUS pairing: the point is to test the ordinary "grabbed the
            // wrong bottle" mistake, not to set the lab on fire and measure the theatrics.
            if (HazardousMix.Classify(binding.GetComponent<LiquidPhysics>()?.currentChemical,
                                      lp.currentChemical) != HazardousMix.HazardOutcome.None) continue;
            return lp;
        }
        return null;
    }

    static LiquidPhysics FindSourceOf(ChemicalData chem, LiquidPhysics destination)
    {
        if (chem == null) return null;
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lp == null || lp == destination || lp.currentChemical != chem) continue;
            if (lp.currentLiquidVolume <= 0.5f) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;
            return lp;
        }
        return null;
    }
}
#endif
