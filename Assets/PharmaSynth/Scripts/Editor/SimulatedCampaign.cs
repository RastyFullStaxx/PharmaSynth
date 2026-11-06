#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Plays the WHOLE CAMPAIGN LOOP end-to-end in edit mode, Exp 2→9: every
/// module, through the cutscenes, quiz taking and submitting, the teleport back
/// outside and the choice of the next experiment. Where
/// SimulatedRun proves ONE module's task graph, this drives the flow AROUND
/// every module through the REAL wiring a clueless player relies on:
///   • the pure GatekeeperModel FSM (Fire returns false on any illegal move —
///     so a broken gate is caught, not narrated over)
///   • the module picked from its period through the two-step picker, with the
///     real ProgressionFlow.IsUnlocked gating each pick
///   • the honest pour-through of the experiment (SimulatedRun)
///   • the REAL PostLabController quiz — Open, answer, SubmitAndFinish → Finish
///   • the REAL ExperimentGrader result + the floored grade-screen text
///   • the REAL cutscene outro selection + its subtitle beats
///   • the REAL ProgressionService record + ProgressionFlow unlock + UnlockDiff
///     announcement, then the pick of the NEXT experiment — looping to the
///     campaign-complete celebration after Exp 9.
/// The transcript (Logs/simcampaign.txt) is written as the PLAYER EXPERIENCE:
/// every line Pharmee/Jimenez speaks, every panel prompt, every grade/outro
/// beat — so it can be read as a clueless player and critiqued.
///
/// SAFETY: progression is recorded to a TEMP save file (Application.
/// temporaryCachePath), never the real pharmasynth_progress.json.
public static class SimulatedCampaign
{
    public class Result
    {
        public readonly List<string> findings = new List<string>();   // UX / correctness issues
        public int modulesPassed;
        public bool campaignComplete;
        public bool Clean => findings.Count == 0;
    }

    // Exp 2→9 in catalog order (the tutorial is pre-passed to unlock Exp 2).
    static readonly string[] GradedChain =
    {
        "prelim-chemical-compounding", "prelim-ethyl-alcohol",
        "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
        "final-benzamide", "final-winemaking",
    };

    [MenuItem("Tools/PharmaSynth/Simulate Campaign (full loop Exp 2-9)")]
    public static void RunMenu()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SimCampaign] exit Play mode first."); return; }
        var log = new StringBuilder();
        var r = Run(log);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/simcampaign.txt", log.ToString());
        string verdict = r == null ? "COULD NOT RUN"
            : r.Clean ? "CLEAN — " + r.modulesPassed + "/8 passed, campaign "
                        + (r.campaignComplete ? "COMPLETE" : "NOT complete") + ", 0 findings"
            : r.findings.Count + " FINDING(S), " + r.modulesPassed + "/8 passed";
        Debug.Log((r != null && r.Clean ? "<color=#4CD07D>" : "<color=#FFC24B>")
                  + "[SimCampaign] " + verdict + "</color>\n  full transcript → Logs/simcampaign.txt");
    }

    public static Result Run(StringBuilder log)
    {
        var res = new Result();
        var lines = new PharmeeGatekeeper.GateLines();   // the driver's fixed prompts (C# defaults)

        var expLib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        var quizLib = AssetDatabase.LoadAssetAtPath<QuizBankLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/QuizBankLibrary.asset");
        var cutscenes = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset");
        if (expLib == null || quizLib == null || cutscenes == null)
        {
            log.AppendLine("missing library: " + (expLib == null ? "ExperimentLibrary " : "")
                           + (quizLib == null ? "QuizBankLibrary " : "") + (cutscenes == null ? "CutsceneLibrary" : ""));
            return null;
        }

        // TEMP save — never the real campaign file.
        string save = Path.Combine(Application.temporaryCachePath, "sim_campaign_progress.json");
        foreach (var p in new[] { save, save + ".bak" }) if (File.Exists(p)) File.Delete(p);
        var svc = new ProgressionService(save);
        svc.Load();
        // PLAY the tutorial rather than assume it: it is the first experiment any
        // player touches, and recording it as passed would leave it uncovered.
        //
        // A failure here is reported but does NOT cascade: the tutorial is still recorded
        // as passed so the graded chain can be evaluated too. One broken module should
        // cost one finding, not the other eight.
        log.AppendLine("\n-- Tutorial: methane (ungraded, unlocks Exp 2) --");
        var methane = SimulatedRun.Run("tutorial-methane", log);
        if (methane == null || !methane.Clean)
        {
            string why = methane == null ? "the simulator could not run it"
                : methane.completedTasks + "/" + methane.totalTasks + " tasks, "
                  + methane.bugs.Count + " bug(s), " + methane.mistakes + " mistake(s)";
            res.findings.Add("tutorial-methane: the tutorial does not play clean (" + why
                             + ") — a first-timer meets this before anything else");
        }
        svc.RecordResult("tutorial-methane",
            new ExperimentResult { passed = true, gradePassed = true, masteryPassed = true, overallMastery = 1f });
        var flow = new ProgressionFlow(svc, false);

        // Own components (edit-mode: AddComponent fires no Awake/OnEnable — we drive seams).
        var host = new GameObject("simcampaign_host");
        var director = host.AddComponent<CutsceneDirector>();
        director.SetLibrary(cutscenes);
        var postLab = host.AddComponent<PostLabController>();
        int variant = 0;   // remark-pool cursor, exactly like the driver's _remarkVariant

        log.AppendLine("================ SIMULATED CAMPAIGN — Exp 2→9, full loop ================");
        log.AppendLine("(temp save: " + save + " — the real campaign is untouched)\n");

        try
        {
            for (int mi = 0; mi < GradedChain.Length; mi++)
            {
                string id = GradedChain[mi];
                var entry = ExperimentCatalog.Get(id);
                string title = entry != null ? entry.title : id;
                var period = entry != null ? entry.period : ExperimentPeriod.Prelim;

                log.AppendLine("\n########## EXPERIMENT " + (mi + 2) + " — " + title + " (" + id + ") ##########");

                // ---- THE DOOR: gate FSM walk, pure model, real unlock gating ----
                var model = new GatekeeperModel();   // fresh gate each loop = the Blocked entrance
                log.AppendLine("\n-- At the door --");
                Say(log, "Pharmee", lines.welcome);
                Fire(model, GateEvent.Approach, "approach the door", res, log);
                Say(log, "Pharmee", lines.approach);
                Fire(model, GateEvent.PickCampaign, "pick Campaign", res, log);
                Say(log, "Pharmee", lines.campaignExplain);
                Fire(model, GateEvent.ExplainDone, "continue past the explainer", res, log);
                Say(log, "Pharmee", lines.episodePrompt);

                // Two-step picker: period → module, both gated by the REAL flow.
                System.Func<string, bool> canSelect = mid => flow.IsUnlocked(mid);
                System.Func<ExperimentPeriod, string> firstPlayable =
                    p => GatekeeperModel.FirstPlayableInPeriod(flow, p);
                LogPeriodOptions(flow, log);
                if (!model.ChooseEpisode(period, canSelect, firstPlayable))
                    res.findings.Add(id + ": could NOT open period " + period + " at the picker (locked or empty)");
                else log.AppendLine("  → picked period: " + period);
                LogModuleOptions(flow, period, log);
                if (!model.ChooseModule(id, canSelect))
                    res.findings.Add(id + ": could NOT pick the module — it is not unlocked when the player reaches it (broken unlock chain)");
                else log.AppendLine("  → picked module: " + title);

                // ---- PPE gate ----
                Say(log, "Pharmee", lines.coatPrompt);
                Fire(model, GateEvent.Coated, "don coat + goggles + gloves", res, log);
                Say(log, "Pharmee", lines.readyPrompt);
                Fire(model, GateEvent.Ready, "say \"I'm ready\"", res, log);
                Fire(model, GateEvent.Loaded, "stage builds ARMED (clock held)", res, log);
                Say(log, "Pharmee", lines.thresholdWarn);
                Fire(model, GateEvent.ProceedConfirmed, "confirm at the threshold", res, log);
                Fire(model, GateEvent.CrossedThreshold, "CROSS the threshold — the timer starts", res, log);
                if (model.State != GateState.Running)
                    res.findings.Add(id + ": gate did not reach Running (stuck at " + model.State + ")");

                // ---- THE EXPERIMENT: honest pour-through (SimulatedRun) ----
                log.AppendLine("\n-- Running the experiment (real pours; see simrun-" + id + ".txt for every drop) --");
                var sub = new StringBuilder();
                var body = SimulatedRun.Run(id, sub);
                if (body == null) { res.findings.Add(id + ": SimulatedRun could not run"); continue; }
                File.WriteAllText("Logs/simrun-" + id + ".txt", sub.ToString());
                log.AppendLine("  " + (body.Clean ? "✓ ran CLEAN — " + body.completedTasks + "/" + body.totalTasks + " tasks, 0 mistakes"
                    : "✗ " + body.bugs.Count + " bug(s), " + body.completedTasks + "/" + body.totalTasks + " tasks, " + body.mistakes + " mistakes"));
                if (!body.Clean)
                    res.findings.Add(id + ": the experiment body did not play clean — " + string.Join(" | ", body.bugs));

                var runner = Object.FindAnyObjectByType<ExperimentRunner>();
                if (runner == null) { res.findings.Add(id + ": no ExperimentRunner in the scene"); continue; }

                // ---- TESTS DONE → the review flow ----
                Fire(model, GateEvent.TestsDone, "chemical tests done → clock FREEZES", res, log);
                log.AppendLine("\n-- Review corner (fade-teleport; Jimenez briefs) --");
                Say(log, "Pharmee", PharmeeLines.Pick(PharmeeLines.TestsDoneLines, variant++));
                Say(log, "Dr. Jimenez", PharmeeLines.Pick(PharmeeLines.JimenezQuizBrief, 0));
                Say(log, "Dr. Jimenez", PharmeeLines.Pick(PharmeeLines.JimenezQuizBrief, 1));
                Fire(model, GateEvent.QuizBegin, "Jimenez's briefing done → tablet opens", res, log);

                // ---- THE QUIZ: real PostLabController ----
                log.AppendLine("\n-- Quiz tablet (Data Sheet & Documentation) --");
                postLab.SetRefs(runner, quizLib);
                postLab.Open();
                var bank = postLab.Bank;
                if (bank == null || bank.Count == 0)
                    log.AppendLine("  (no quiz bank — Documentation defaults to full credit)");
                else
                {
                    if (bank.Count < 3) res.findings.Add(id + ": quiz has only " + bank.Count + " question(s) (design calls for 3)");
                    if (!bank.AllValid()) res.findings.Add(id + ": quiz has an INVALID question (fewer than 2 options or a bad correct index)");
                    for (int q = 0; q < bank.questions.Count; q++)
                    {
                        var qq = bank.questions[q];
                        log.AppendLine("  Q" + (q + 1) + "/" + bank.Count + ": " + qq.prompt);
                        for (int o = 0; o < qq.options.Count; o++)
                            log.AppendLine("      " + (o == qq.correctIndex ? "◉" : "○") + " " + qq.options[o]);
                        postLab.Answer(q, qq.correctIndex);   // answer correctly to validate the PASS path
                        if (!string.IsNullOrEmpty(qq.explanation))
                            log.AppendLine("      why: " + qq.explanation);
                    }
                }
                if (!postLab.AllAnswered)
                    res.findings.Add(id + ": quiz Submit is blocked — not every question is answerable (AllAnswered false)");
                var result = postLab.SubmitAndFinish();   // → runner.Finish
                Fire(model, GateEvent.Graded, "submit → runner.Finish landed", res, log);

                // ---- GRADE SCREEN + OUTRO ----
                log.AppendLine("\n-- Grade screen --");
                log.AppendLine("  " + GradeDisplay.Percent(result.grade.Total) + "%   "
                               + (result.passed ? "PASSED" : "TRY AGAIN")
                               + "   (time " + ExperimentHudController.FormatTime(result.elapsedSeconds) + ")");
                foreach (var bl in GradeScreenController.BuildBreakdown(result).Split('\n'))
                    log.AppendLine("    " + StripRich(bl));
                if (!result.passed)
                    res.findings.Add(id + ": a FLAWLESS run + all-correct quiz did NOT pass (grade "
                        + result.grade.Total.ToString("0.0") + "%, mastery " + (result.overallMastery * 100f).ToString("0")
                        + "%) — the module is unbeatable on a perfect play"
                        + (result.masteryPassed ? "" : " [mastery gate < 0.90]")
                        + (result.gradePassed ? "" : " [grade gate < 90]"));

                director.LoadForModule(id);
                var outro = director.SelectOutro(result);
                var set = cutscenes.GetSet(id);
                if (set == null || !set.IsComplete)
                    res.findings.Add(id + ": cutscene set missing/incomplete — the outro would not play");
                if (outro != null)
                {
                    log.AppendLine("\n-- Outro cutscene: " + (result.passed ? "SUCCESS" : "FAILURE") + " — \"" + outro.title + "\" --");
                    if (outro.beats == null || outro.beats.Count == 0)
                        res.findings.Add(id + ": the " + (result.passed ? "success" : "failure") + " outro has NO subtitle beats (silent cutscene)");
                    else foreach (var b in outro.beats) log.AppendLine("    “" + b.subtitle + "”");
                }

                // Spoken verdicts (after the outro).
                Say(log, "Dr. Jimenez", PharmeeLines.Pick(result.passed ? PharmeeLines.JimenezPassRemarks : PharmeeLines.JimenezFailRemarks, variant));
                Say(log, "Pharmee", PharmeeLines.Pick(result.passed ? PharmeeLines.Celebrate : PharmeeLines.Encourage, variant));
                variant++;

                // ---- RECORD + UNLOCK + CONTINUE (pass path) ----
                var before = UnlockDiff.UnlockedSet(flow);
                svc.RecordResult(id, result);
                var newly = UnlockDiff.NewlyUnlocked(before, flow);

                if (!result.passed)
                {
                    log.AppendLine("\n-- Grade buttons: [Retry] [Choose Another] (fail path — not exercised here) --");
                    res.findings.Add(id + ": stopped the chain — a fail here blocks the campaign walk");
                    break;
                }
                res.modulesPassed++;
                log.AppendLine("\n-- Grade buttons: [Continue] pressed --");
                Fire(model, GateEvent.ContinueAfterPass, "Continue → one-fade reset + teleport home", res, log);
                Fire(model, GateEvent.TeleportDone, "arrive back at the entrance", res, log);
                log.AppendLine("\n-- Entrance debrief --");
                Say(log, "Pharmee", PharmeeLines.Pick(PharmeeLines.DebriefCongrats, variant++) + " "
                                    + PharmeeLines.DebriefRemark(result.grade.Total, flow.AllComplete()));
                Fire(model, GateEvent.DebriefDone, "debrief done", res, log);

                // Unlock announcement.
                log.AppendLine("\n-- Unlock announcement --");
                string announce = flow.AllComplete()
                    ? PharmeeLines.Pick(PharmeeLines.CampaignComplete, variant++)
                    : UnlockDiff.AnnouncementFor(newly);
                Say(log, "Pharmee", announce);
                // Verify the NEXT graded module actually unlocked.
                if (!flow.AllComplete() && mi + 1 < GradedChain.Length)
                {
                    string next = GradedChain[mi + 1];
                    if (!flow.IsUnlocked(next))
                        res.findings.Add(id + ": passing did NOT unlock the next experiment (" + next + ") — the chain is broken");
                    else
                    {
                        bool announced = false;
                        foreach (var n in newly) if (n == next) announced = true;
                        if (!announced)
                            res.findings.Add(id + ": " + next + " unlocked but was NOT named in the announcement");
                    }
                }
                Fire(model, GateEvent.AnnounceDone, "announcement done → door re-blocks, loop repeats", res, log);
                if (model.State != GateState.Blocked)
                    res.findings.Add(id + ": after the loop the gate is " + model.State + ", not Blocked (cannot start the next run)");

                var nextPick = flow.NextExperiment();
                log.AppendLine("  Next experiment the game would offer: "
                               + (nextPick != null ? nextPick.title : "(none — campaign complete)"));
            }

            res.campaignComplete = flow.AllComplete();
            log.AppendLine("\n================ VERDICT ================");
            log.AppendLine("  passed " + res.modulesPassed + "/8 · campaign "
                           + (res.campaignComplete ? "COMPLETE" : "NOT complete")
                           + " · " + res.findings.Count + " finding(s)");
            if (!res.campaignComplete)
                res.findings.Add("after Exp 9, the campaign is NOT marked complete (celebration would not fire)");
            foreach (var f in res.findings) log.AppendLine("  FINDING  " + f);
        }
        finally
        {
            Object.DestroyImmediate(host);
            foreach (var p in new[] { save, save + ".bak" }) if (File.Exists(p)) File.Delete(p);
            // SimulatedRun already restored the vessel snapshot + torn down to the methane stage.
        }
        return res;
    }

    static void Fire(GatekeeperModel model, GateEvent e, string playerAction, Result res, StringBuilder log)
    {
        var from = model.State;
        bool ok = model.Fire(e);
        if (!ok)
            res.findings.Add("gate: illegal transition — could not " + playerAction + " (event " + e + " rejected in " + from + ")");
        log.AppendLine("  [" + from + " → " + model.State + "]  " + playerAction);
    }

    static void Say(StringBuilder log, string who, string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        log.AppendLine("  " + who + ": \"" + line + "\"");
    }

    static void LogPeriodOptions(ProgressionFlow flow, StringBuilder log)
    {
        GatekeeperModel.EpisodeOptions(flow, out var labels, out var selectable);
        if (labels == null) return;
        var sb = new StringBuilder("  Period picker: ");
        for (int i = 0; i < labels.Count; i++)
            sb.Append(labels[i]).Append(i < labels.Count - 1 ? " · " : "");
        log.AppendLine(sb.ToString());
    }

    static void LogModuleOptions(ProgressionFlow flow, ExperimentPeriod period, StringBuilder log)
    {
        GatekeeperModel.ModuleOptions(flow, period, out var labels, out var selectable, out _);
        if (labels == null) return;
        var sb = new StringBuilder("  Module picker (" + period + "): ");
        for (int i = 0; i < labels.Count; i++)
            sb.Append(labels[i]).Append(i < labels.Count - 1 ? " · " : "");
        log.AppendLine(sb.ToString());
    }

    static string StripRich(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var sb = new StringBuilder(s.Length);
        bool intag = false;
        foreach (char c in s)
        {
            if (c == '<') { intag = true; continue; }
            if (c == '>') { intag = false; continue; }
            if (!intag) sb.Append(c);
        }
        return sb.ToString();
    }
}
#endif
