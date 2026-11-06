#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Task graph, scoring, grading, runner, quizzes and module content.
public static partial class PharmaSelfTests
{
    static void TaskGraphSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "mix"),
        });
        A("graph: the guided step is the first open task", g.FirstAvailable()?.taskId == "prep");
        A("graph: mix blocked before prep", g.TryComplete("mix") == TaskCompletionResult.BlockedByPrerequisite);
        A("graph: prep completes", g.TryComplete("prep") == TaskCompletionResult.Completed);
        A("graph: progress 0.25", Near(g.Progress01, 0.25f));
        A("graph: double-complete guarded", g.TryComplete("prep") == TaskCompletionResult.AlreadyComplete);
        g.TryComplete("mix");
        A("graph: progress 0.75", Near(g.Progress01, 0.75f));
        bool sensor = false; g.RegisterCondition("test", () => sensor); g.Tick();
        A("graph: auto-check waits", !g.IsComplete("test"));
        A("graph: the guided step follows the prerequisites", g.FirstAvailable()?.taskId == "test");
        sensor = true; g.Tick();
        A("graph: auto-check fires", g.IsComplete("test"));
        A("graph: progress 1.0", Near(g.Progress01, 1f));
    }

    static void MasterySuite()
    {
        var m = new MasteryModel(new BktParameters(), new[] { LabSkill.Measuring });
        A("bkt: starts at pL0", Near(m.OverallMastery(), 0.25f));
        for (int i = 0; i < 6; i++) m.Observe(LabSkill.Measuring, true);
        A("bkt: converges past gate", m.IsMastered(0.90f));
        float before = m.OverallMastery(); m.Observe(LabSkill.Measuring, false);
        A("bkt: slip lowers mastery", m.OverallMastery() < before);
    }

    static void ScoreSuite()
    {
        var sc = new ScoreCalculator(new RubricWeights());
        var perfect = new Dictionary<RubricCategory, float>();
        foreach (RubricCategory c in Enum.GetValues(typeof(RubricCategory))) perfect[c] = 1f;
        A("score: perfect = 100", Near(sc.Compute(perfect).Total, 100f, 0.2f));
        var w = new RubricWeights { procedure = 0.4f, chemicalTests = 0.4f, materialsAndPPE = 0, timeManagement = 0, sanitation = 0, documentation = 0 };
        var sc2 = new ScoreCalculator(w);
        A("score: weights normalized", Near(sc2.Compute(new Dictionary<RubricCategory, float> { { RubricCategory.Procedure, 1 }, { RubricCategory.ChemicalTests, 1 } }).Total, 100f, 0.2f));
        A("score: time under par", Near(ScoreCalculator.TimeSubScore(300, 600), 1f));
        A("score: full marks up to 1.5x par (first-time VR pace)", Near(ScoreCalculator.TimeSubScore(900, 600), 1f));
        A("score: time halfway between 1.5x and 3x par is half marks", Near(ScoreCalculator.TimeSubScore(1350, 600), 0.5f));
        A("score: time 3x par", Near(ScoreCalculator.TimeSubScore(1800, 600), 0f));
    }

    static void GraderSuite()
    {
        var tasks = new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("synth", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("t", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "synth"),
            T("clean", TaskPhase.DataSheet, 1, LabSkill.Transfer, RubricCategory.Sanitation, "t"),
        };
        var g = new TaskGraph(tasks);
        g.TryComplete("prep"); g.TryComplete("synth"); g.TryComplete("t"); g.TryComplete("clean");
        var grader = new ExperimentGrader(new ScoreCalculator(new RubricWeights()), new GradingConfig());
        A("grader: perfect run = 100", Near(grader.Grade(g, new MistakeLog(), 300, 600, 1f).Total, 100f, 0.2f));
        var log = new MistakeLog();
        log.Record(LabErrorType.WrongReagent, ""); log.Record(LabErrorType.WrongStep, "");
        log.Record(LabErrorType.MissingPPE, ""); log.Record(LabErrorType.DroppedGlassware, "");
        // 900 s on a 600 s par is inside the 1.5x time grace (full time marks).
        A("grader: mistakes = 86.08", Near(grader.Grade(g, log, 900, 600, 2f / 3f).Total, 86.08f, 0.3f));
        A("mistakelog: category mapping", MistakeLog.CategoryFor(LabErrorType.DroppedGlassware) == RubricCategory.Sanitation);
    }

    static void RunnerSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer };
        var go = new GameObject("SelfTestRunner");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            A("runner: out-of-order = wrong step", runner.CompleteTask("b") == TaskCompletionResult.BlockedByPrerequisite && runner.MistakeCount == 1);
            runner.CompleteTask("a"); runner.CompleteTask("b");
            var res = runner.Finish(1f);
            A("runner: two-part gate computed", res.grade.Total > 0 && (res.passed == (res.gradePassed && res.masteryPassed)));
            A("runner: retry resets", RetryResets(runner));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
    }
    static bool RetryResets(ExperimentRunner r) { r.Retry(); return Near(r.Progress01, 0f) && r.MistakeCount == 0; }

    static void ArmedRunnerSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "selftest-armed";
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer };
        var go = new GameObject("SelfTestArmed");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            int prepared = 0, started = 0;
            runner.ExperimentPrepared += _ => prepared++;
            runner.ExperimentStarted += _ => started++;

            runner.PrepareExperiment();
            A("armed: state flags", runner.IsArmed && !runner.IsRunning && prepared == 1 && started == 0);
            runner.AdvanceTime(5f);
            A("armed: clock frozen", Near(runner.ElapsedSeconds, 0f));
            A("armed: tasks locked", runner.CompleteTask("a") == TaskCompletionResult.UnknownTask);
            runner.RecordMistake(LabErrorType.WrongReagent, "pre-run");
            A("armed: mistakes ignored", runner.MistakeCount == 0);

            runner.StartRun();
            A("armed: StartRun begins", runner.IsRunning && !runner.IsArmed && started == 1);
            runner.AdvanceTime(3f);
            A("armed: clock runs after start", Near(runner.ElapsedSeconds, 3f));
            A("armed: tasks unlocked", runner.CompleteTask("a") == TaskCompletionResult.Completed);
            runner.CompleteTask("b");
            var res = runner.Finish(1f);
            A("armed: finish works", res.grade.Total > 0f);

            // StartRun with no armed attempt = legacy full start.
            runner.StartRun();
            A("armed: StartRun fallback", runner.IsRunning && Near(runner.ElapsedSeconds, 0f));

            // Launcher modes over the real library asset.
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                var lgo = new GameObject("SelfTestLauncher");
                try
                {
                    var runner2 = lgo.AddComponent<ExperimentRunner>();
                    var launcher = lgo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner2);
                    var m1 = launcher.Launch("tutorial-methane", LaunchMode.StageOnly);
                    A("launcher: StageOnly furnishes without clock", m1 != null && !runner2.IsRunning && !runner2.IsArmed);
                    var m2 = launcher.Launch("tutorial-methane", LaunchMode.PrepareArmed);
                    A("launcher: PrepareArmed arms", m2 != null && runner2.IsArmed && !runner2.IsRunning);
                    var m3 = launcher.Launch("tutorial-methane");
                    A("launcher: legacy = FullStart", m3 != null && runner2.IsRunning);
                }
                finally { UnityEngine.Object.DestroyImmediate(lgo); }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
    }

    // flow-smoothness guards.
    static void FlowSmoothnessSuite()
    {
        // F2: displayed percentages FLOOR (the gate compares raw values).
        A("display: 89.9 floors to 89", GradeDisplay.Percent(89.9f) == 89);
        A("display: 90.0 shows 90", GradeDisplay.Percent(90f) == 90);
        A("display: clamps", GradeDisplay.Percent(-3f) == 0 && GradeDisplay.Percent(140f) == 100);
        A("display: mastery floors", GradeDisplay.MasteryPercent(0.899f) == 89);

        // F6: the scripted review window suppresses ambient chatter.
        A("review: all review states flagged",
            GatekeeperModel.IsReviewState(GateState.QuizIntro) && GatekeeperModel.IsReviewState(GateState.QuizTime)
            && GatekeeperModel.IsReviewState(GateState.ScoreReview) && GatekeeperModel.IsReviewState(GateState.Returning)
            && GatekeeperModel.IsReviewState(GateState.Debrief) && GatekeeperModel.IsReviewState(GateState.UnlockAnnounce));
        A("review: play states not flagged",
            !GatekeeperModel.IsReviewState(GateState.Running) && !GatekeeperModel.IsReviewState(GateState.Blocked)
            && !GatekeeperModel.IsReviewState(GateState.Loading) && !GatekeeperModel.IsReviewState(GateState.LabTour));

        // Tutorial Mode: a guided run is PRACTICE — it must never reach the graded
        // chain (quiz -> grade -> BKT -> unlock -> save). Campaign must be untouched.
        TutorialSession.Active = true;
        A("tutorial: flag gates the review chain", !PharmeeGatekeeper.ShouldEnterReview());
        TutorialSession.Active = false;
        A("tutorial: campaign still enters the review chain", PharmeeGatekeeper.ShouldEnterReview());

        // Tutorial Mode unlocks the whole chain and stops the clock. A fresh service
        // has nothing passed, so in campaign the prerequisite modules MUST stay shut —
        // that negative is the real regression guard on the unlockAll switch.
        {
            var tutSvc = new ProgressionService("selftest-tutorial");
            TutorialSession.Active = true;
            var tutFlow = ProgressionFlow.Create(tutSvc);
            bool allOpen = true;
            foreach (var e in ExperimentCatalog.Entries)
                if (!tutFlow.IsUnlocked(e.moduleId)) allOpen = false;
            A("tutorial: every module unlocked in practice mode", allOpen);
            A("tutorial: clock does not run in practice mode", !ExperimentRunner.ClockRuns);

            TutorialSession.Active = false;
            var campFlow = ProgressionFlow.Create(tutSvc);
            bool someLocked = false;
            foreach (var e in ExperimentCatalog.Entries)
                if (!campFlow.IsUnlocked(e.moduleId)) someLocked = true;
            A("tutorial: campaign still gates the chain", someLocked);
            A("tutorial: campaign still runs the clock", ExperimentRunner.ClockRuns);
        }

        // F4: the fail path can abandon back to the entrance.
        A("gate: fail can abandon to entrance", GatekeeperModel.Next(GateState.ScoreReview, GateEvent.AbandonRun) == GateState.Blocked);
        A("gate: abandon illegal elsewhere", GatekeeperModel.Next(GateState.Running, GateEvent.AbandonRun) == GateState.Running
            && GatekeeperModel.Next(GateState.QuizTime, GateEvent.AbandonRun) == GateState.QuizTime);
        A("gate: pass-continue and retry unchanged",
            GatekeeperModel.Next(GateState.ScoreReview, GateEvent.ContinueAfterPass) == GateState.Returning
            && GatekeeperModel.Next(GateState.ScoreReview, GateEvent.RetryRequested) == GateState.Loading);

        // F7: fader callbacks compose instead of overwriting.
        int order = 0, aAt = 0, bAt = 0;
        var composed = ScreenFader.Compose(() => aAt = ++order, () => bAt = ++order);
        composed();
        A("fader: composed callbacks both run in order", aAt == 1 && bAt == 2);
        System.Action solo = () => { };
        A("fader: compose null-safe", ScreenFader.Compose(null, null) == null
            && ScreenFader.Compose(solo, null) == solo && ScreenFader.Compose(null, solo) == solo);

        // F3: Abort + Finish guards.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("w59-a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var rgo = new GameObject("w59_runner");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.Finish(1f);   // no attempt built — must not throw
            A("runner: finish before start is safe", !runner.IsRunning);

            runner.SetModule(module);
            runner.StartExperiment();
            runner.FreezeClock();
            runner.Abort();
            A("runner: abort ends the run + unfreezes", !runner.IsRunning && !runner.ClockFrozen);
            bool finishedFired = false;
            runner.ExperimentFinished += _ => finishedFired = true;
            runner.Finish(1f);
            A("runner: finish after abort is a no-op", !finishedFired);

            runner.StartExperiment();
            var r1 = runner.Finish(0.5f);
            var r2 = runner.Finish(1f);   // second call must return the SAME recorded result
            A("runner: double finish returns the recorded result", Near(r1.grade.Total, r2.grade.Total));

            // F8: quiz-bank edge cases + freeze-on-open.
            var pgo = new GameObject("w59_postlab");
            try
            {
                var post = pgo.AddComponent<PostLabController>();
                post.OpenFor(null);
                A("quiz: missing bank = full documentation credit", Near(post.ScoreFraction(), 1f));
                var empty = ScriptableObject.CreateInstance<QuizBank>();
                empty.questions = new List<QuizQuestion>();
                post.OpenFor(empty);
                A("quiz: EMPTY bank also full credit (was 0)", Near(post.ScoreFraction(), 1f));
                UnityEngine.Object.DestroyImmediate(empty);

                runner.StartExperiment();
                post.SetRefs(runner, null);
                post.Open();
                A("quiz: opening freezes the clock on every path", runner.ClockFrozen);
            }
            finally { UnityEngine.Object.DestroyImmediate(pgo); }
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(module); }

        // F5: supply monitor un-latches on "keep trying".
        var mgo = new GameObject("w59_monitor");
        try
        {
            var mon = mgo.AddComponent<ReagentSupplyMonitor>();
            mon.ForceLatch();
            A("supply: latched after prompt", mon.Latched);
            mon.Unlatch();
            A("supply: keep-trying re-arms the monitor", !mon.Latched);
        }
        finally { UnityEngine.Object.DestroyImmediate(mgo); }

        // F1: campaign-complete celebration copy.
        A("campaign: celebration pool authored", PharmeeLines.CampaignComplete.Length >= 3);
        A("campaign: debrief remark swaps on completion",
            PharmeeLines.DebriefRemark(95f, true) != PharmeeLines.DebriefRemark(95f, false));
        A("campaign: non-final remark unchanged", PharmeeLines.DebriefRemark(95f, false) == PharmeeLines.DebriefRemark(95f));

        // Data-integrity guards (silent-unpassable + "outro always plays").
        int emptyModules = 0;
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentModuleDefinition", new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (m == null || m.graphTasks == null || m.graphTasks.Count == 0) emptyModules++;
        }
        A("data: every module has tasks (mastery gate reachable)", emptyModules == 0);

        // Tutorial Mode target registry + gap audit. Deliberately DETERMINISTIC and
        // scene-read-only: the per-module coverage sweep has to Build() each stage,
        // which mutates the open scene, so it lives behind
        // Tools ▸ PharmaSynth ▸ Audit Tutorial Targets instead of dirtying SampleScene
        // on every suite run. What is pinned here is the logic that sweep depends on.
        {
            TaskTargetRegistry.Clear();
            var probe = new GameObject("selftest-target-probe");
            try
            {
                TaskTargetRegistry.Register("t-a", probe.transform, TargetRole.Source, false);
                A("tutorial: registry returns a registered target",
                    TaskTargetRegistry.Targets("t-a").Count == 1);
                TaskTargetRegistry.Register("t-a", probe.transform, TargetRole.Destination, true);
                A("tutorial: registry is idempotent per transform",
                    TaskTargetRegistry.Targets("t-a").Count == 1);
                A("tutorial: unknown task resolves to nothing",
                    TaskTargetRegistry.Targets("t-none").Count == 0);
                PharmeeGatekeeper.ResetSessionState();
                A("lifecycle: the gatekeeper's teardown drops the review flag and every registered target",
                    !PharmeeGatekeeper.ReviewFlowActive && TaskTargetRegistry.TaskCount == 0);
                TaskTargetRegistry.Register("t-a", probe.transform, TargetRole.Source, false);

                // The audit must flag a step with no target, but exempt a wrap-up step
                // — autoCompleteWhenOthersDone tasks have no physical verb by design.
                var real = new ExperimentTask { taskId = "t-a" };
                var gap = new ExperimentTask { taskId = "t-gap" };
                var wrap = new ExperimentTask { taskId = "t-wrap", autoCompleteWhenOthersDone = true };
                TutorialTargets.AuditAgainst(new[] { real, gap, wrap });
                A("tutorial: audit flags a step with no target",
                    TutorialTargets.LastUnresolved.Count == 1 && TutorialTargets.LastUnresolved[0] == "t-gap");

                // The watch prints the step's hint in practice mode only.
                A("tutorial: watch shows label only in campaign",
                    WristWatchController.StepText("Weigh 2 g", "Use the balance.", false) == "Weigh 2 g");
                A("tutorial: watch appends the hint in practice mode",
                    WristWatchController.StepText("Weigh 2 g", "Use the balance.", true)
                        == "Weigh 2 g\n<size=70%>Use the balance.</size>");
                A("tutorial: watch omits an empty hint cleanly",
                    WristWatchController.StepText("Weigh 2 g", "", true) == "Weigh 2 g");

                // The grab rule: a SOURCE goes quiet in hand (message received), a
                // DESTINATION/TOOL stays lit (holding the right tube ≠ step finished).
                var src = new TaskTarget { role = TargetRole.Source, stayLitWhenHeld = false };
                var dst = new TaskTarget { role = TargetRole.Destination, stayLitWhenHeld = true };
                A("tutorial: source glows when not held", TutorialHighlighter.ShouldLight(src, false, true));
                A("tutorial: grabbing the source silences it", !TutorialHighlighter.ShouldLight(src, true, true));
                A("tutorial: destination stays lit while held", TutorialHighlighter.ShouldLight(dst, true, true));
                A("tutorial: nothing glows once the step is unavailable",
                    !TutorialHighlighter.ShouldLight(dst, false, false));

                // Guidance is suppressed while a longProcess skip holds the screen black.
                A("tutorial: no guidance during a time-skip fade",
                    !TutorialHighlighter.GuidanceAllowed(true, true));
                A("tutorial: guidance resumes after the fade",
                    TutorialHighlighter.GuidanceAllowed(true, false));

                // Hover and guidance are independent channels — neither clears the other.
                var hh = probe.AddComponent<HoverHighlight>();
                hh.SetGuide(true, TargetRole.Source);
                hh.SetHighlight(true);
                hh.SetHighlight(false);
                A("tutorial: hover-exit does not clear the guidance glow", hh.IsGuided && hh.IsHighlighted);
                hh.SetGuide(false, TargetRole.Source);
                A("tutorial: clearing guidance unlights it", !hh.IsGuided && !hh.IsHighlighted);

                // Skip-step: offered in practice AND demo, never in campaign, never
                // while the review corner is up.
                A("tutorial: skip offered in practice mode",
                    DemoHudController.SkipAllowed(false, true, true, false));
                A("tutorial: skip never offered in campaign",
                    !DemoHudController.SkipAllowed(false, false, true, false));
                A("tutorial: skip hidden during the review flow",
                    !DemoHudController.SkipAllowed(false, true, true, true));

                // Always-on labels: practice reads the bench from a distance.
                A("tutorial: label radius widens in practice mode",
                    ProximityLabel.VisibleRadius(1.4f, true) > ProximityLabel.VisibleRadius(1.4f, false));
                A("tutorial: campaign label radius unchanged",
                    Near(ProximityLabel.VisibleRadius(1.4f, false), 1.4f));
                A("tutorial: a guided object is labelled at ANY distance",
                    ProximityLabel.ShouldShow(50f, 1.4f, true, true));
                A("tutorial: an unguided far object stays unlabelled",
                    !ProximityLabel.ShouldShow(50f, 1.4f, true, false));
                A("tutorial: campaign ignores the guided flag",
                    !ProximityLabel.ShouldShow(50f, 1.4f, false, true));

                // The x-ray ghost copies only BODY meshes. Each item's floating
                // ProximityLabel carries a big compensating scale, so copying it
                // too makes the silhouette hugely oversized.
                var probeMesh = new Mesh { name = "selftest-probe-mesh" };
                var solidGo = new GameObject("solid-probe");
                var solidMf = solidGo.AddComponent<MeshFilter>();
                solidMf.sharedMesh = probeMesh;
                solidGo.AddComponent<MeshRenderer>();
                A("tutorial: a body mesh counts as solid", ExperimentSceneBuilder.IsSolidMesh(solidMf));

                // Waypoint placement: above the SOLID TOP normally, but pulled out in
                // front when something (a shelf plank) is directly overhead.
                var shelfBottle = new Bounds(new Vector3(0f, 1f, 0f), new Vector3(0.08f, 0.2f, 0.08f));
                var open = WaypointGuide.MarkerPosition(shelfBottle, 0.16f, false, new Vector3(0f, 1.6f, -2f), 0.3f);
                A("tutorial: waypoint clears the object's TOP, not its origin",
                    open.y > shelfBottle.max.y && Near(open.y, shelfBottle.max.y + 0.16f));
                var boxed = WaypointGuide.MarkerPosition(shelfBottle, 0.16f, true, new Vector3(0f, 1.6f, -2f), 0.3f);
                A("tutorial: a shelf-caged waypoint moves in FRONT instead of up",
                    Near(boxed.y, shelfBottle.center.y) && boxed.z < shelfBottle.center.z);
                A("tutorial: caged waypoint pulls out toward the player",
                    Near(Vector3.Distance(new Vector3(boxed.x, 0f, boxed.z),
                                          new Vector3(shelfBottle.center.x, 0f, shelfBottle.center.z)), 0.3f));

                // Distance scaling: apparent size is size/distance, so holding it
                // constant means growing LINEARLY with distance — clamped at both ends.
                A("tutorial: marker is unscaled at the reference distance",
                    Near(WaypointGuide.DistanceScale(2f, 2f, 0.55f, 3.5f), 1f));
                A("tutorial: marker grows with distance",
                    Near(WaypointGuide.DistanceScale(4f, 2f, 0.55f, 3.5f), 2f));
                A("tutorial: far marker is capped, not unbounded",
                    Near(WaypointGuide.DistanceScale(40f, 2f, 0.55f, 3.5f), 3.5f));
                A("tutorial: near marker never shrinks away",
                    Near(WaypointGuide.DistanceScale(0.05f, 2f, 0.55f, 3.5f), 0.55f));
                A("tutorial: a zero reference distance cannot divide by zero",
                    Near(WaypointGuide.DistanceScale(3f, 0f, 0.55f, 3.5f), 1f));

                // Spotlight: non-targets dim, and guidance/hover still outrank dimming
                // so a dimmed object you deliberately point at still responds.
                hh.SetDimmed(true);
                A("tutorial: a non-target dims", hh.IsDimmed && !hh.IsHighlighted);
                hh.SetGuide(true, TargetRole.Destination);
                A("tutorial: guidance outranks dimming", hh.IsHighlighted && hh.IsGuided);
                hh.SetGuide(false, TargetRole.Destination);
                hh.SetHighlight(true);
                A("tutorial: hover outranks dimming too", hh.IsHighlighted);
                hh.SetHighlight(false);
                hh.SetDimmed(false);
                A("tutorial: clearing the spotlight restores the object", !hh.IsDimmed && !hh.IsHighlighted);

                // Reduce-flashing: an UNSET shader global reads 0, so 0 must mean
                // "pulse" or every untouched scene would come up frozen.
                A("comfort: flashing on by default maps to 0 (the unset value)",
                    Near(ComfortSettings.SteadyGlobal(false), 0f));
                A("comfort: reduce-flashing holds the glow steady",
                    Near(ComfortSettings.SteadyGlobal(true), 1f));

                // Haptics: progress is a light tick, a mistake is a longer stronger buzz.
                A("haptics: a mistake outranks a step tick in both amplitude and length",
                    LabHaptics.ErrorAmplitude > LabHaptics.StepAmplitude
                    && LabHaptics.ErrorSeconds > LabHaptics.StepSeconds);

                // Practised ticks are SESSION-only and reset when the mode is entered,
                // so a second student at the same headset never inherits them.
                TutorialSession.BeginSession();
                A("tutorial: a fresh session has practised nothing", TutorialSession.PractisedCount == 0);
                TutorialSession.MarkPractised("midterm-acetone");
                A("tutorial: a finished run marks the module practised",
                    TutorialSession.HasPractised("midterm-acetone"));
                A("tutorial: other modules stay unmarked", !TutorialSession.HasPractised("final-benzamide"));
                TutorialSession.BeginSession();
                A("tutorial: re-entering the mode clears the ticks",
                    !TutorialSession.HasPractised("midterm-acetone") && TutorialSession.PractisedCount == 0);

                // Picker labels: practice never says PASSED (nothing is graded).
                {
                    var pickSvc = new ProgressionService("selftest-tutorial-pick");
                    var pickFlow = ProgressionFlow.Create(pickSvc);
                    TutorialSession.MarkPractised("tutorial-methane");
                    GatekeeperModel.ModuleOptions(pickFlow, ExperimentPeriod.Tutorial,
                        out var tLabels, out _, out var tIds);
                    int idx = tIds.IndexOf("tutorial-methane");
                    A("tutorial: picker ticks a practised module",
                        idx >= 0 && tLabels[idx].Contains("practised"));
                    A("tutorial: picker never claims PASSED in practice mode",
                        idx >= 0 && !tLabels[idx].Contains("PASSED"));
                }
                TutorialSession.Active = false;

                // Waypoint sizing is a TARGET HEIGHT IN METRES, not a multiplier:
                // a multiplier scales whatever the artist authored and then distance
                // scaling multiplies it AGAIN, so the marker fills the view up close.
                A("tutorial: marker sizing holds a constant apparent height",
                    Near(WaypointGuide.DistanceScale(2f, 2f, 0.35f, 3.5f), 1f)
                    && Near(WaypointGuide.DistanceScale(1f, 2f, 0.35f, 3.5f), 0.5f));
                A("tutorial: the near clamp lets the marker shrink out of your face",
                    WaypointGuide.DistanceScale(0.2f, 2f, 0.35f, 3.5f) < 0.5f);

                // Room FX: the breath curve stays in range, and two strips with
                // different phases must genuinely differ — a room pulsing in lockstep
                // reads as one blinking prop, not as somewhere alive.
                A("roomfx: breath stays within 0..1",
                    MenuRoomFx.Breath(3.1f, 0.7f, 0f) >= 0f && MenuRoomFx.Breath(3.1f, 0.7f, 0f) <= 1f);
                A("roomfx: phase offsets desynchronise the strips",
                    !Near(MenuRoomFx.Breath(1f, 0.7f, 0f), MenuRoomFx.Breath(1f, 0.7f, 0.7f)));

                // Orientation line exists and is one breath, not a paragraph.
                A("tutorial: orientation line is authored",
                    !string.IsNullOrEmpty(PharmeeLines.TutorialOrientation)
                    && PharmeeLines.TutorialOrientation.Length < 260);
                var textGo = new GameObject("ProxTag");
                var textMf = textGo.AddComponent<MeshFilter>();
                textMf.sharedMesh = probeMesh;
                textGo.AddComponent<TMPro.TextMeshPro>();
                A("tutorial: a text mesh is NOT solid (x-ray must skip labels)",
                    !ExperimentSceneBuilder.IsSolidMesh(textMf));
                var fxGo = new GameObject("PourStream");
                var fxMf = fxGo.AddComponent<MeshFilter>();
                fxMf.sharedMesh = probeMesh;
                fxGo.AddComponent<MeshRenderer>();
                A("tutorial: an effect child is NOT solid", !ExperimentSceneBuilder.IsSolidMesh(fxMf));
                UnityEngine.Object.DestroyImmediate(solidGo);
                UnityEngine.Object.DestroyImmediate(textGo);
                UnityEngine.Object.DestroyImmediate(fxGo);
                UnityEngine.Object.DestroyImmediate(probeMesh);

                // Stuck ladder: silent while the player is working, escalating only
                // while the SAME step stays unsolved, and capped at "say it out loud".
                A("tutorial: no coaching in the first 15 s on a step", TutorialCoach.LevelFor(10f) == 0);
                A("tutorial: watch nudge at 15 s", TutorialCoach.LevelFor(20f) == 1);
                A("tutorial: marker escalates at 30 s", TutorialCoach.LevelFor(40f) == 2);
                A("tutorial: coach speaks at 60 s", TutorialCoach.LevelFor(90f) == 3);
                A("tutorial: coaching never escalates past 3", TutorialCoach.LevelFor(600f) == 3);

                // Run summary: counts, never a percentage.
                A("tutorial: clean run summary names no corrections",
                    TutorialCoach.SummaryText(12, 0) == "Practice complete — 12 steps, no corrections needed.");
                A("tutorial: summary singularises one correction",
                    TutorialCoach.SummaryText(12, 1) == "Practice complete — 12 steps, 1 correction along the way.");
                A("tutorial: summary pluralises many corrections",
                    TutorialCoach.SummaryText(12, 3) == "Practice complete — 12 steps, 3 corrections along the way.");

                // ---- verb demonstration -------------------------------------
                // The registry carries the VERB as well as the object, derived in the
                // same sweep, so a demonstration can never mime a different motion from
                // the one the step's binding will accept.
                TaskTargetRegistry.Clear();
                TaskTargetRegistry.Register("t-v", probe.transform, TargetRole.Source, false, VerbKind.Scoop);
                A("tutorial: registry carries the step's verb",
                    TaskTargetRegistry.Targets("t-v")[0].verb == VerbKind.Scoop);
                // Every older caller (MethaneApparatusRig among them) omits the verb,
                // so the default has to be the one that suits an unknown step: carry it.
                TaskTargetRegistry.Register("t-w", probe.transform, TargetRole.Station, true);
                A("tutorial: an unspecified verb defaults to Place",
                    TaskTargetRegistry.Targets("t-w")[0].verb == VerbKind.Place);

                // never point the player at something they cannot see. Hidden
                // objects here are hidden ON PURPOSE — the module's own end product, the
                // methane-only staged props, the dispensers' Template_ clone sources —
                // and glowing one is glowing nothing.
                A("tutorial: an active object is a valid target",
                    TutorialTargets.Visible(probe.transform));
                var hidden = new GameObject("selftest-hidden-target");
                var template = new GameObject("Template_selftest-source");
                try
                {
                    hidden.SetActive(false);
                    A("tutorial: a deliberately hidden object is never a target",
                        !TutorialTargets.Visible(hidden.transform));
                    A("tutorial: a dispenser Template_ source is never a target",
                        !TutorialTargets.Visible(template.transform));
                    A("tutorial: a null component is never a target",
                        !TutorialTargets.Visible(null));

                // ---- ground path + aiming cues -------------------------------
                var route = new List<Vector3>
                {
                    new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 2f), new Vector3(3f, 0f, 2f),
                };
                A("path: length follows the polyline, not the straight line",
                    Mathf.Abs(GuidePathMath.Length(route) - 5f) < 0.001f);
                A("path: a single point has no length", GuidePathMath.Length(
                    new List<Vector3> { Vector3.zero }) == 0f);
                A("path: a null route has no length", GuidePathMath.Length(null) == 0f);

                // Sampling must follow the CORNER, not cut across it — the whole point of
                // routing on a navmesh is that the path bends around the benches.
                GuidePathMath.Sample(route, 3f, out var mid, out var midFwd);
                A("path: sampling past a corner turns with it",
                    Mathf.Abs(mid.x - 1f) < 0.001f && Mathf.Abs(mid.z - 2f) < 0.001f);
                A("path: direction follows the segment being walked",
                    Vector3.Dot(midFwd, Vector3.right) > 0.99f);
                GuidePathMath.Sample(route, -5f, out var atStart, out _);
                A("path: a negative distance clamps to the start", atStart == route[0]);
                GuidePathMath.Sample(route, 99f, out var atEnd, out _);
                A("path: an overlong distance clamps to the end", atEnd == route[2]);

                // ⭐ The flow must run TOWARD the destination. A sign error here produces a
                // path that visibly pulls the player backwards, and nothing in a compile or
                // a play-mode run would ever complain about it.
                var t0 = GuidePathMath.Build(route, 0f);
                var t1 = GuidePathMath.Build(route, 0.2f);
                A("path: chevrons are laid along the route", t0.Count > 3);
                A("path: the pattern flows toward the destination",
                    t1.Count > 0 && t0.Count > 0 && t1[0].along01 > t0[0].along01);
                A("path: a degenerate route draws nothing",
                    GuidePathMath.Build(new List<Vector3> { Vector3.zero }, 0f).Count == 0);
                A("path: chevron count is capped",
                    GuidePathMath.Build(new List<Vector3> { Vector3.zero, new Vector3(0f, 0f, 500f) },
                        0f).Count <= GuidePathMath.MaxChevrons);

                // Path and beacon answer the same question and must never both draw.
                A("path: the floor route owns the far case",
                    GuidePathMath.ShowPath(5f, true) && !GuidePathMath.ShowBeacon(5f, true));
                A("path: the beacon owns the near case",
                    !GuidePathMath.ShowPath(1f, true) && GuidePathMath.ShowBeacon(1f, true));
                A("path: with no route the beacon always takes over",
                    !GuidePathMath.ShowPath(5f, false) && GuidePathMath.ShowBeacon(5f, false));

                // THE MARK ITSELF must be a chevron, not a PrimitiveType.Quad: a row of
                // sliding squares cannot read as direction however correct the flow is.
                {
                    GuidePathMath.ChevronGeometry(out var cv, out var ct);
                    A("chevron: the mark is a V, not a square", cv.Length == 6 && ct.Length == 12);

                    float maxZ = float.MinValue; int apex = -1;
                    for (int i = 0; i < cv.Length; i++) if (cv[i].z > maxZ) { maxZ = cv[i].z; apex = i; }
                    A("chevron: the apex points along the route",
                        apex >= 0 && Mathf.Approximately(cv[apex].x, 0f));

                    // Symmetric about the centre line, or the arrow leans to one side.
                    float spanL = 0f, spanR = 0f;
                    foreach (var v in cv) { if (v.x < spanL) spanL = v.x; if (v.x > spanR) spanR = v.x; }
                    A("chevron: the arms are symmetric", Mathf.Abs(spanL + spanR) < 1e-5f);

                    // ⛔ The one that actually bites: PharmaSynth/GuideOverlay is Cull Back,
                    // so a flat mesh wound the wrong way is INVISIBLE FROM ABOVE while every
                    // count, position and log line stays perfectly correct.
                    int up = 0;
                    for (int t = 0; t < ct.Length; t += 3)
                        if (GuidePathMath.FaceNormal(cv[ct[t]], cv[ct[t + 1]], cv[ct[t + 2]]).y > 0.99f) up++;
                    A("chevron: every triangle faces UP (Cull Back hides a mis-wound mark)",
                        up == ct.Length / 3);

                    // A flat mesh authored in XZ must NOT be laid down again by the driver.
                    bool chevFlat = true;
                    foreach (var v in cv) if (Mathf.Abs(v.y) > 1e-6f) chevFlat = false;
                    A("chevron: authored flat, so the driver must not re-rotate it", chevFlat);
                }

                // The ends ramp, or every chevron pops in at the player's feet and vanishes
                // at full strength on arrival. The driver must read `along01` for this.
                A("path: the flow fades in at the start and out at the end",
                    GuidePathMath.FadeAt(0f) < 0.01f && GuidePathMath.FadeAt(1f) < 0.01f
                    && GuidePathMath.FadeAt(0.5f) > 0.99f);
                A("path: the fade ramps monotonically",
                    GuidePathMath.FadeAt(0.02f) < GuidePathMath.FadeAt(0.06f)
                    && GuidePathMath.FadeAt(0.06f) < GuidePathMath.FadeAt(0.12f));

                // The pour readout goes QUIET once satisfied — a label reading "40 / 40"
                // invites an overpour rather than stopping one.
                A("aim: an outstanding pour shows have / needs",
                    VesselStatusMath.NeedLine("Bromine Water", 2f, 5f) == "Bromine Water  2 / 5 ml");
                A("aim: a satisfied step says nothing",
                    VesselStatusMath.NeedLine("Bromine Water", 5f, 5f) == "");
                A("aim: solids are grams, not millilitres",
                    VesselStatusMath.NeedLine("Brown Sugar", 1f, 12f, true) == "Brown Sugar  1 / 12 g");
                A("aim: a step with no requirement shows nothing",
                    VesselStatusMath.NeedLine("Ethanol", 0f, 0f) == "");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hidden);
                    UnityEngine.Object.DestroyImmediate(template);
                }

                var demoB = new GameObject("selftest-demo-dest");
                try
                {
                    var pair = new List<TaskTarget>
                    {
                        new TaskTarget { transform = demoB.transform, role = TargetRole.Destination, verb = VerbKind.Pour },
                        new TaskTarget { transform = probe.transform, role = TargetRole.Source, verb = VerbKind.Pour },
                    };
                    A("tutorial: the SOURCE is what the demo ghost moves",
                        VerbDemoPlayer.Endpoints(pair, out var mover, out var dest, out var kind)
                        && mover == probe.transform && dest == demoB.transform && kind == VerbKind.Pour);

                    // A verb performed on a vessel already in place (stir, heat, weigh)
                    // has no source. It must still resolve — degenerating to a motion in
                    // place — rather than silently refusing to demonstrate.
                    var stationOnly = new List<TaskTarget>
                    {
                        new TaskTarget { transform = demoB.transform, role = TargetRole.Station, verb = VerbKind.Stir },
                    };
                    A("tutorial: a source-less step still resolves both ends",
                        VerbDemoPlayer.Endpoints(stationOnly, out var m2, out var d2, out _)
                        && m2 == demoB.transform && d2 == demoB.transform);
                    A("tutorial: an empty target set demonstrates nothing",
                        !VerbDemoPlayer.Endpoints(new List<TaskTarget>(), out _, out _, out _));
                }
                finally { UnityEngine.Object.DestroyImmediate(demoB); }

                // Motion shapes. Pinned because a curve that compiles and looks plausible
                // can still mime nothing useful, and only a headset would ever notice.
                var vFrom = new Vector3(0f, 1f, 0f);
                var vTo = new Vector3(0.6f, 0.95f, 0.2f);
                foreach (VerbKind k in System.Enum.GetValues(typeof(VerbKind)))
                    A("tutorial: " + k + " demo starts at the object's resting place",
                        (VerbDemoMath.Sample(k, 0f, vFrom, vTo).position - vFrom).sqrMagnitude < 1e-6f);
                A("tutorial: a Place demo ends ON the destination",
                    (VerbDemoMath.Sample(VerbKind.Place, 1f, vFrom, vTo).position - vTo).sqrMagnitude < 1e-6f);

                // A pour that stops at horizontal reads as hesitation, not as pouring.
                A("tutorial: the pour demo tips PAST horizontal",
                    Quaternion.Angle(Quaternion.identity,
                        VerbDemoMath.Sample(VerbKind.Pour, 0.75f, vFrom, vTo).rotation) > 90f);
                A("tutorial: the pour demo levels off again",
                    Quaternion.Angle(Quaternion.identity,
                        VerbDemoMath.Sample(VerbKind.Pour, 1f, vFrom, vTo).rotation) < 1f);

                // The demo must not teach a shorter stir than the verb will accept.
                A("tutorial: a stir demo completes the required revolutions",
                    Mathf.Approximately(VerbDemoMath.StirAngleDeg(1f, 2.5f), 900f));
                A("tutorial: the stir demo circles rather than sitting still",
                    (VerbDemoMath.Sample(VerbKind.Stir, 0.4f, vFrom, vTo).position
                     - VerbDemoMath.Sample(VerbKind.Stir, 0.9f, vFrom, vTo).position).sqrMagnitude > 1e-4f);

                // A ghost that sinks through the bench on its way across reads as a bug.
                bool arcClears = true;
                float floorY = Mathf.Min(vFrom.y, vTo.y);
                for (float t = 0f; t <= 1.0001f; t += 0.05f)
                    if (VerbDemoMath.Arc(vFrom, vTo, t).y < floorY - 1e-4f) arcClears = false;
                A("tutorial: a travel arc never dips below either endpoint", arcClears);

                // The vessel must tip TOWARD the destination, not sideways past it.
                var axis = VerbDemoMath.TiltAxis(vFrom, vTo);
                Vector3 flat = vTo - vFrom; flat.y = 0f;
                A("tutorial: the pour axis is horizontal", Mathf.Abs(axis.y) < 1e-4f);
                A("tutorial: the pour axis is square to the approach",
                    Mathf.Abs(Vector3.Dot(axis, flat.normalized)) < 1e-4f);
                A("tutorial: a vertically-stacked pour still has an axis",
                    VerbDemoMath.TiltAxis(vFrom, vFrom + Vector3.up).sqrMagnitude > 0.9f);

                // ---- playability battery -------------------------------------
                // Reachability: the band a standing VR player can actually work in.
                A("reach: bench height is fine", ReachabilityAudit.HeightVerdict(1.0f) == ReachabilityAudit.Verdict.Fine);
                A("reach: shoulder height is fine", ReachabilityAudit.HeightVerdict(1.6f) == ReachabilityAudit.Verdict.Fine);
                A("reach: a top shelf is an awkward stretch",
                    ReachabilityAudit.HeightVerdict(1.95f) == ReachabilityAudit.Verdict.Awkward);
                A("reach: above the hard ceiling is unreachable",
                    ReachabilityAudit.HeightVerdict(2.4f) == ReachabilityAudit.Verdict.Unreachable);
                A("reach: on the floor is unreachable",
                    ReachabilityAudit.HeightVerdict(0.01f) == ReachabilityAudit.Verdict.Unreachable);

                // Enclosure: ONE open side is enough. Flagging open shelves would bury the
                // real failures (a closed cabinet) in noise.
                A("reach: blocked on every side is enclosed",
                    ReachabilityAudit.IsEnclosed(new[] { true, true, true, true, true, true }));
                A("reach: one open side is reachable",
                    !ReachabilityAudit.IsEnclosed(new[] { true, true, true, true, true, false }));
                A("reach: a bottle standing on a bench is not enclosed",
                    !ReachabilityAudit.IsEnclosed(new[] { false, true, false, false, false, false }));
                A("reach: no probe data is never enclosed",
                    !ReachabilityAudit.IsEnclosed(new bool[0]));

                // HasCondition exists so the simulator can tell "the player did the verb
                // badly" apart from "nothing was listening"; the two look identical
                // otherwise.
                {
                    var g = new TaskGraph(new List<ExperimentTask> { new ExperimentTask { taskId = "hc-a" } });
                    A("graph: no condition until one is registered", !g.HasCondition("hc-a"));
                    g.RegisterCondition("hc-a", () => false);
                    A("graph: a registered condition is visible", g.HasCondition("hc-a"));
                    A("graph: an unknown task never has a condition", !g.HasCondition("hc-nope"));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(probe);
                TaskTargetRegistry.Clear();
            }
        }

        var cuts = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>("Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset");
        int badOutros = 0;
        foreach (var e in ExperimentCatalog.Entries)
        {
            var set = cuts != null ? cuts.GetSet(e.moduleId) : null;
            if (set == null
                || set.success == null || set.success.beats == null || set.success.beats.Count == 0
                || set.failure == null || set.failure.beats == null || set.failure.beats.Count == 0) badOutros++;
        }
        A("data: every module has non-empty success+failure outros", badOutros == 0);

        var quizLib = AssetDatabase.LoadAssetAtPath<QuizBankLibrary>("Assets/PharmaSynth/ScriptableObjects/QuizBankLibrary.asset");
        int missingBanks = 0;
        foreach (var e in ExperimentCatalog.Entries)
            if (quizLib == null || quizLib.GetBank(e.moduleId) == null) missingBanks++;
        A("data: every module resolves a quiz bank", missingBanks == 0);
    }

    // manuscript re-verification fixes.
    static void ManuscriptFixSuite()
    {
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        ChemicalData Chem(string f) => AssetDatabase.LoadAssetAtPath<ChemicalData>("Assets/PharmaSynth/ScriptableObjects/Chemicals/" + f + ".asset");
        ExperimentLayout Layout(string f) => AssetDatabase.LoadAssetAtPath<ExperimentLayout>("Assets/PharmaSynth/ScriptableObjects/Layouts/" + f + ".asset");

        // M1: benzoic ester test has its alcohol.
        var esterRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_BenzoateEster.asset");
        A("m1: ester rule uses propyl alcohol", esterRule != null && esterRule.inputChemicalB == Chem("Chem_PropylAlcohol"));
        A("m1: benzoic+propyl reaction resolves", reg != null
            && reg.FindReaction(Chem("Chem_BenzoicAcid"), Chem("Chem_PropylAlcohol")) != null);
        bool m1Binding = false;
        var benzoic = Layout("Layout_BenzoicAcid");
        if (benzoic != null)
            foreach (var v in benzoic.vessels) foreach (var b in v.bindings)
                if (b.reagentChemical == "Propyl Alcohol" && b.taskId == "test-ester") m1Binding = true;
        A("m1: propyl alcohol staged + bound", m1Binding);

        // M2: chloroform oxidation test (manuscript procedure + results sheet).
        var oxRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_ChloroformOxidation.asset");
        A("m2: oxidation rule exists + registered", oxRule != null && reg != null && reg.rules.Contains(oxRule));
        A("m2: chloroform+dichromate reaction resolves", reg != null
            && reg.FindReaction(Chem("Chem_Chloroform"), Chem("Chem_PotassiumDichromate")) != null);
        var chloroModule = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Midterm_Chloroform.asset");
        bool m2Task = false, m2Gated = false;
        if (chloroModule != null)
            foreach (var t in chloroModule.graphTasks)
            {
                if (t.taskId == "test-oxidation" && t.phase == TaskPhase.ChemicalTests) m2Task = true;
                if (t.taskId == "record-yield" && t.prerequisites.Contains("test-oxidation")) m2Gated = true;
            }
        A("m2: module has the oxidation test task", m2Task);
        A("m2: data sheet waits for it", m2Gated);
        // Exp 7 rules: the oxidation rule needs a resultLiquid (NULL is the product
        // trap: the mix would VANISH on firing) and a heat gate (the manuscript's
        // "HEAT a few drops"); the AgNO3 rule needs a resultPrecipitate (it claims a
        // precipitate) and pairs ALCOHOLIC silver nitrate, as the manuscript says.
        A("m2: oxidation rule keeps its product + heat gate", oxRule != null
            && oxRule.resultLiquid == Chem("Chem_Chloroform") && oxRule.minTemperatureC >= 50f);
        var agRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_ChloroformAgNO3.asset");
        A("m2: AgNO3 rule is alcoholic + shows its white AgCl", agRule != null
            && agRule.inputChemicalB == Chem("Chem_AlcoholicSilverNitrate")
            && agRule.hasPrecipitate && agRule.resultPrecipitate == Chem("Chem_SilverChloride")
            && agRule.minTemperatureC >= 50f);
        // The zone-free FLAME confirm (test-flammability): pure gate + wiring.
        A("m2: flame confirm needs served + lit + close — never two alone",
            FlameTestMath.Confirms(true, true, 0.1f)
            && !FlameTestMath.Confirms(false, true, 0.1f)
            && !FlameTestMath.Confirms(true, false, 0.1f)
            && !FlameTestMath.Confirms(true, true, FlameTestMath.Reach + 0.05f));
        var chloroLayout = Layout("Layout_Chloroform");
        bool m2Flame = false, m2Stations = chloroLayout != null && chloroLayout.stations.Count == 0;
        if (chloroLayout != null)
            foreach (var v in chloroLayout.vessels)
                if (v.flameTaskId == "test-flammability" && v.benchItem == "Eq_WatchGlass") m2Flame = true;
        A("m2: chloroform layout is zone-free with the watch-glass flame test",
            m2Flame && m2Stations);

        // M6 (Exp 8): benzamide rules free of the product trap and paired
        // with bottles that EXIST. The synthesis + acid rules need a resultLiquid
        // (NULL = the product trap); the hydrolyses fire at boiling ("heat to
        // BOILING"), not 40; the alkali pairs a bottled NaOH (only the 10% and 6N
        // bottles exist, plain "Sodium Hydroxide" has none); Sodium Nitrite is in
        // the reagent catalog as a solution, since the manuscript adds "3 ml of 10% solution".
        var benzRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Benzamide.asset");
        A("m6: benzamide synthesis keeps its product + fires in the ice (no gate)",
            benzRule != null && benzRule.resultLiquid == Chem("Chem_Benzamide") && benzRule.minTemperatureC <= 0f);
        var acidHyd = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_BenzamideAcid.asset");
        A("m6: acid hydrolysis keeps its product + gates at the boil",
            acidHyd != null && acidHyd.resultLiquid == Chem("Chem_Benzamide") && acidHyd.minTemperatureC >= 90f);
        var alkHyd = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_BenzamideAlkali.asset");
        A("m6: alkali hydrolysis pairs the 10% NaOH bottle + gates at the boil",
            alkHyd != null && alkHyd.inputChemicalB == Chem("Chem_SodiumHydroxide10") && alkHyd.minTemperatureC >= 90f);
        A("m6: sodium nitrite is the manuscript's 10% SOLUTION with a catalog bottle",
            Chem("Chem_SodiumNitrite") != null && Chem("Chem_SodiumNitrite").state == PhysicalState.Liquid
            && RawReagentCatalog.Find("Sodium Nitrite") != null
            && RawReagentCatalog.Find("Sodium Nitrite").labware == RawReagentCatalog.LabwareKind.ReagentBottle);
        var benzLayout = Layout("Layout_Benzamide");
        bool m6Stir = false, m6LitmusA = false, m6LitmusB = false;
        bool m6Stations = benzLayout != null && benzLayout.stations.Count == 0;
        if (benzLayout != null)
            foreach (var v in benzLayout.vessels)
            {
                if (v.stirTaskId == "stir-stand" && v.chillTaskId == "ice-bath") m6Stir = true;
                if (v.litmusTaskId == "test-alkali") m6LitmusA = true;
                if (v.litmusTaskId == "test-acid") m6LitmusB = true;
            }
        A("m6: benzamide layout is zone-free — chilled stir beaker + both litmus tubes",
            m6Stations && m6Stir && m6LitmusA && m6LitmusB);

        // M3: no grapes (manuscript exclusion).
        A("m3: fruit juice renamed", Chem("Chem_GrapeJuice") != null && Chem("Chem_GrapeJuice").chemicalName == "Mixed Fruit Juice");
        A("m3: old name no longer resolves", lib != null && lib.GetChemical("Grape Juice") == null
            && lib.GetChemical("Mixed Fruit Juice") != null);
        // Exp 9 is zone-free: the jar starts EMPTY (a start-filled jar hides the
        // missing bench bottle, so the player could not pour their own must) and
        // BINDS Mixed Fruit Juice for prepare-must, poured from the
        // Raw_MixedFruitJuice catalog bottle.
        var wine = Layout("Layout_WineMaking");
        bool m3Binds = false, m3Empty = true, m3Stir = false, m3Ferment = false;
        if (wine != null)
            foreach (var v in wine.vessels)
            {
                if (!string.IsNullOrEmpty(v.startChemical)) m3Empty = false;
                if (v.stirTaskId == "stir-must" && v.fermentTaskId == "confirm-ferment") { m3Stir = true; m3Ferment = true; }
                foreach (var b in v.bindings)
                    if (b.reagentChemical == "Mixed Fruit Juice" && b.taskId == "prepare-must") m3Binds = true;
            }
        A("m3: fermentation jar starts EMPTY and binds the fruit-juice must", m3Binds && m3Empty);
        A("m3: winemaking is zone-free — stir + ferment on the jar, no stations",
            m3Stir && m3Ferment && wine != null && wine.stations.Count == 0);
        A("m3: Mixed Fruit Juice has a bench bottle (catalog row)",
            RawReagentCatalog.Find("Mixed Fruit Juice") != null
            && RawReagentCatalog.Find("Mixed Fruit Juice").labware == RawReagentCatalog.LabwareKind.ReagentBottle);
        // The new WineFermentation rule: pitching yeast into the juice makes Wine
        // (the client-rule product the player crafts) — without it the jar only
        // ever held juice and every draw poured juice, not wine.
        var wineRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/WineFermentation.asset");
        A("m3: WineFermentation makes Wine from juice + yeast (registered)",
            wineRule != null && reg != null && reg.rules.Contains(wineRule)
            && wineRule.resultLiquid == Chem("Chem_Wine")
            && reg.FindReaction(Chem("Chem_GrapeJuice"), Chem("Chem_Yeast")) == wineRule);

        // M4: reagent fidelity — layouts AND rules agree.
        var esterForm = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/EsterFormation.asset");
        A("m4: ethanol ester uses diluted acetic acid", esterForm != null && esterForm.inputChemicalB == Chem("Chem_DilutedAceticAcid"));
        var acidRule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/Test_BenzamideAcid.asset");
        A("m4: benzamide acid test uses diluted HCl", acidRule != null && acidRule.inputChemicalB == Chem("Chem_DilutedHydrochloricAcid"));
        bool m4Acet = false;
        var acetanilide = Layout("Layout_Acetanilide");
        // The manuscript's 0.1N HCl prep-preamble bottle is
        // USED in the bromination tubes (trace acidification); there is no
        // staged prep-hcl prop in the zone-free layout.
        if (acetanilide != null)
            foreach (var v in acetanilide.vessels) foreach (var b in v.bindings)
                if (b.reagentChemical == "Hydrochloric Acid 0.1N" && b.taskId == "test-bromination") m4Acet = true;
        A("m4: acetanilide uses the 0.1N HCl", m4Acet);
        int kiBindings = 0;
        foreach (var name in new[] { "Layout_EthylAlcohol", "Layout_Acetone" })
        {
            var lay = Layout(name);
            if (lay == null) continue;
            foreach (var v in lay.vessels) foreach (var b in v.bindings)
                if (b.reagentChemical == "Potassium Iodide 10%" && b.taskId == "test-iodoform") kiBindings++;
        }
        A("m4: iodoform tests stage their KI", kiBindings == 2);

        // M5a: the compounding quiz matches the REBUILT manuscript battery —
        // it must probe the manual's own tests, never the retired sodium/bromine ones.
        var quiz = AssetDatabase.LoadAssetAtPath<QuizBank>("Assets/PharmaSynth/ScriptableObjects/Quizzes/Quiz_ChemicalCompounding.asset");
        bool noRetired = true;
        if (quiz != null)
            foreach (var q in quiz.questions)
                if (q.prompt.Contains("sodium metal") || q.prompt.Contains("unsaturation")) noRetired = false;
        A("m5: compounding quiz drops the retired sodium/bromine battery", quiz != null && noRetired);
        // Watch-panel contract: every Exp 2 step prints the
        // manuscript's REAL quantity for learning, then the achievable action under
        // it — "Add 2 ml of Tollen's reagent" / "-> 2 squeezes". ChecklistPager shows
        // t.hint under the current step, so this copy IS the in-game instruction.
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
                "Assets/PharmaSynth/ScriptableObjects/Experiments/Prelim_ChemicalCompounding.asset");
            bool twoLine = m != null, saysHow = m != null;
            if (m != null)
                foreach (var t in m.graphTasks)
                {
                    if (string.IsNullOrWhiteSpace(t.hint) || !t.hint.Contains("\n")) twoLine = false;
                    else
                    {
                        // The action line must name a COUNTABLE verb: the count is
                        // the measurement, and an un-actionable hint leaves the
                        // player with nothing to do.
                        string how = t.hint.Substring(t.hint.IndexOf('\n')).ToLowerInvariant();
                        if (!(how.Contains("squeeze") || how.Contains("dip") || how.Contains("tilt-pour")
                              || how.Contains("funnel") || how.Contains("burner") || how.Contains("board")))
                            saysHow = false;
                    }
                }
            A("m5: every step's hint is two lines (fact + action)", twoLine);
            A("m5: every action line names a countable verb", saysHow);
        }
        // Exp 2's reactions. The NEGATIVES are deliberately unauthored: tert-butyl
        // resisting KMnO4, acetone leaving Tollen's clear, unboiled aspirin giving no
        // violet. "Nothing happens" IS each test's answer and the contrast is the
        // lesson — authoring a rule there would invent chemistry the manuscript denies.
        {
            ReactionRule Rule(string n) => AssetDatabase.LoadAssetAtPath<ReactionRule>(
                "Assets/PharmaSynth/ScriptableObjects/Reactions/" + n + ".asset");
            var positives = new[] { "Test_EnolPhenolFeCl3", "Test_OxidationPrimaryKMnO4",
                "Test_OxidationSecondaryKMnO4", "Test_TollensAldehyde", "Test_EsterEthylAcetate",
                "Test_EsterMethylSalicylate", "Test_AspirinHydrolysis", "Test_HydrolysateFeCl3" };
            bool authored = true, observed = true;
            foreach (var n in positives)
            {
                var r = Rule(n);
                if (r == null || r.inputChemicalA == null || r.inputChemicalB == null) { authored = false; continue; }
                if (string.IsNullOrWhiteSpace(r.expectedObservation)) observed = false;
            }
            A("m5: the 8 positive tests are authored", authored);
            A("m5: every test states its expected observation", observed);
            // The odour cue is the ONE sensory loss VR can't reproduce (client-approved
            // on-screen substitute), so both esters must actually carry it.
            var ea = Rule("Test_EsterEthylAcetate"); var ms = Rule("Test_EsterMethylSalicylate");
            A("m5: both esters report their odour on screen",
                ea != null && ms != null && ea.outcome == ReactionOutcome.Odor && ms.outcome == ReactionOutcome.Odor
                && ea.expectedObservation.Contains("fruity") && ms.expectedObservation.Contains("wintergreen"));
            // Hydrolysis only happens on a real boil — that's why the step has a bath.
            var hy = Rule("Test_AspirinHydrolysis");
            A("m5: aspirin only hydrolyses when boiled", hy != null && hy.minTemperatureC >= 90f);
            // The negatives must STAY unauthored.
            A("m5: tert-butyl has no KMnO4 rule (it cannot oxidise)",
                Rule("Test_OxidationTertiaryKMnO4") == null);
            A("m5: acetone has no Tollen's rule (a ketone gives no mirror)",
                Rule("Test_TollensAcetone") == null);
        }
        A("m5: compounding quiz covers the manuscript tests", quiz != null && quiz.questions.Count == 5
            && quiz.questions[0].prompt.Contains("enol") && quiz.questions[1].prompt.Contains("oxidation")
            && quiz.questions[2].prompt.Contains("fourth") && quiz.questions[3].prompt.Contains("Tollen")
            && quiz.questions[4].prompt.Contains("aspirin"));
    }

    static void PostLabSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<QuizBankLibrary>("Assets/PharmaSynth/ScriptableObjects/QuizBankLibrary.asset");
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Prelim_EthylAlcohol.asset");
        A("postlab: library + module load", lib != null && module != null);
        if (lib == null || module == null) return;
        var bank = lib.GetBank("prelim-ethyl-alcohol");
        A("postlab: bank found + 3 questions", bank != null && bank.Count == 3);
        A("postlab: all 9 banks registered", lib.banks.Count >= 9);
        if (bank == null) return;

        var rgo = new GameObject("pl_runner"); var cgo = new GameObject("pl_ctrl");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            // Complete every non-DataSheet task in authored order (prereqs satisfied).
            foreach (var t in runner.Graph.Tasks)
                if (t.phase != TaskPhase.DataSheet) runner.CompleteTask(t.taskId);

            var ctrl = cgo.AddComponent<PostLabController>();
            ctrl.SetRefs(runner, lib);
            A("postlab: finds record-yield data-sheet task", PostLabController.FindDataSheetTaskId(runner.Graph) == "record-yield");
            A("postlab: record task pending before submit", !runner.Graph.IsComplete("record-yield"));

            ctrl.OpenFor(bank);
            A("postlab: opens", ctrl.IsOpen);
            A("postlab: not answered at open", !ctrl.AllAnswered);
            for (int i = 0; i < bank.Count; i++) ctrl.Answer(i, bank.questions[i].correctIndex);
            A("postlab: all answered", ctrl.AllAnswered);
            A("postlab: perfect quiz fraction = 1", Near(ctrl.ScoreFraction(), 1f));

            var res = ctrl.SubmitAndFinish();
            A("postlab: submit completes record-yield", runner.Graph.IsComplete("record-yield"));
            A("postlab: full progress after submit", Near(runner.Graph.Progress01, 1f));
            A("postlab: quiz closed after submit", !ctrl.IsOpen);
            float expectedDoc = (module.rubricWeights.documentation / module.rubricWeights.Total()) * 100f;
            A("postlab: documentation contribution maxed on perfect quiz", Near(res.grade.Documentation, expectedDoc, 0.5f) && expectedDoc > 0f);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(cgo); }

        var rgo2 = new GameObject("pl_runner2"); var cgo2 = new GameObject("pl_ctrl2");
        try
        {
            var runner2 = rgo2.AddComponent<ExperimentRunner>();
            runner2.SetModule(module); runner2.StartExperiment();
            var ctrl2 = cgo2.AddComponent<PostLabController>();
            ctrl2.SetRefs(runner2, lib);
            ctrl2.OpenFor(bank);
            for (int i = 0; i < bank.Count; i++)
            {
                var q = bank.questions[i];
                ctrl2.Answer(i, (q.correctIndex + 1) % q.options.Count);
            }
            A("postlab: all-wrong quiz fraction = 0", Near(ctrl2.ScoreFraction(), 0f));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo2); UnityEngine.Object.DestroyImmediate(cgo2); }
    }

    static void LibrarySuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        A("library: loads", lib != null);
        if (lib == null) return;
        A("library: 9 modules", lib.Count == 9);

        bool allResolve = true;
        foreach (var e in ExperimentCatalog.Entries) if (!lib.Has(e.moduleId)) allResolve = false;
        A("library: covers the whole catalog", allResolve);

        GameFlow.Select("midterm-acetone");
        A("gameflow: selection persists", GameFlow.SelectedModuleId == "midterm-acetone");
        GameFlow.Select("");
        A("gameflow: empty selection ignored", GameFlow.SelectedModuleId == "midterm-acetone");

        var rgo = new GameObject("runner"); var lgo = new GameObject("launcher");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var launcher = lgo.AddComponent<ExperimentLauncher>();
            launcher.SetLibrary(lib); launcher.SetRunner(runner);
            var loaded = launcher.Launch("final-benzamide");
            A("launcher: loads the requested module", loaded != null && runner.Module != null && runner.Module.moduleId == "final-benzamide");
            A("launcher: unknown id returns null", launcher.Launch("does-not-exist") == null);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(lgo); }
    }

    /// Adversarial EDGE CASES ("cover edge cases with proper
    /// error handling, effects, features"): a confused/mischievous player does
    /// the WRONG thing and the game must respond — grade the mistake, refuse the
    /// impossible, block the out-of-order, never false-complete. Each drives the
    /// REAL components with real Exp 3 chemicals (effects self-guard in edit mode).
    static void EdgeCaseSuite()
    {
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var ethanol = LoadChem("Chem_Ethanol");
        var sugar   = LoadChem("Chem_BrownSugar");
        var hypo    = LoadChem("Chem_SodiumHypochlorite");
        var sulfuric= LoadChem("Chem_SulfuricAcid");
        var lime    = LoadChem("Chem_Limewater");
        var co2     = LoadChem("Chem_CarbonDioxide");
        A("edge: Exp 3 chemicals resolve",
            ethanol && sugar && hypo && sulfuric && lime && co2 && reg != null);
        if (!(ethanol && sugar && hypo && sulfuric && lime && co2) || reg == null) return;

        ExperimentModuleDefinition OneTask(string id) {
            var m = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            m.graphTasks = new List<ExperimentTask> { new ExperimentTask { taskId = id, label = id } };
            return m;
        }

        // E1 — WRONG REAGENT into a task vessel is graded (not silently swallowed).
        {
            var rgo = new GameObject("ec1r"); var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                var runner = rgo.AddComponent<ExperimentRunner>();
                runner.SetModule(OneTask("t")); runner.StartExperiment();
                var lp = vgo.AddComponent<LiquidPhysics>(); lp.mainRenderer = null; lp.registry = reg;
                lp.currentChemical = ethanol; lp.currentLiquidVolume = 10f;
                var bind = vgo.AddComponent<LiquidTaskBinding>();
                bind.SetVesselAndRunner(lp, runner);
                bind.AddExpected(ethanol, "t");
                int before = runner.MistakeCount;
                lp.AddLiquid(sugar, 2f);                    // brown sugar — no step wants it here
                A("edge: a wrong reagent is graded a mistake", runner.MistakeCount > before);
            } finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(vgo); }
        }

        // E2 — OVERFLOW: pouring past capacity is REFUSED (no phantom completion).
        {
            var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                var lp = vgo.AddComponent<LiquidPhysics>(); lp.mainRenderer = null; lp.registry = reg;
                lp.maxVolume = 25f; lp.SetContents(null, 0f);
                bool rejected = false, added = false;
                lp.LiquidRejected += (c, m) => rejected = true;
                lp.LiquidAdded += (c, m) => added = true;
                lp.AddLiquid(ethanol, 30f);                 // 30 into a 25 ml tube
                A("edge: overfilling a vessel is refused (rejected, nothing added)",
                    rejected && !added && lp.currentLiquidVolume <= 25f);
            } finally { UnityEngine.Object.DestroyImmediate(vgo); }
        }

        // E3 — HAZARDOUS MIX: acid into hypochlorite (the iodoform tube holds
        // hypochlorite) = toxic gas → classified, and the reactor grades it.
        {
            A("edge: acid + hypochlorite classifies as toxic gas",
                HazardousMix.Classify(hypo, sulfuric) == HazardousMix.HazardOutcome.ToxicGas);
            var rgo = new GameObject("ec3r"); var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                var runner = rgo.AddComponent<ExperimentRunner>();
                runner.SetModule(OneTask("t")); runner.StartExperiment();
                var lp = vgo.AddComponent<LiquidPhysics>(); lp.mainRenderer = null; lp.registry = reg;
                lp.currentChemical = hypo; lp.currentLiquidVolume = 10f;
                var hz = vgo.AddComponent<HazardousMixReactor>(); hz.Bind(lp, runner);
                int before = runner.MistakeCount;
                lp.AddLiquid(sulfuric, 2f);                 // acid into bleach → toxic gas
                A("edge: a dangerous mix is graded a hazardous action", runner.MistakeCount > before);
            } finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(vgo); }
        }

        // E4 — OUT OF ORDER: distilling before fermenting is BLOCKED + graded
        // (the real Exp 3 graph, prerequisites intact).
        {
            var rgo = new GameObject("ec4r");
            try {
                var runner = rgo.AddComponent<ExperimentRunner>();
                var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
                    "Assets/PharmaSynth/ScriptableObjects/Experiments/Prelim_EthylAlcohol.asset");
                runner.SetModule(module); runner.StartExperiment();
                var res = runner.CompleteTask("distill");   // ferment/adjust-ph not done
                A("edge: distilling before fermenting is blocked + graded",
                    res == TaskCompletionResult.BlockedByPrerequisite && runner.MistakeCount >= 1);
            } finally { UnityEngine.Object.DestroyImmediate(rgo); }
        }

        // E5 — FERMENTATION with NO limewater never FALSE-confirms (the player
        // forgot the limewater tube — the step legitimately can't complete).
        {
            var rgo = new GameObject("ec5r"); var fgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try {
                var runner = rgo.AddComponent<ExperimentRunner>();
                var m = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
                m.graphTasks = new List<ExperimentTask> {
                    new ExperimentTask { taskId = "prepare-must", label = "m" },
                    new ExperimentTask { taskId = "ferment", label = "f",
                        prerequisites = new List<string> { "prepare-must" } } };
                runner.SetModule(m); runner.StartExperiment();
                runner.CompleteTask("prepare-must");
                var flask = fgo.AddComponent<LiquidPhysics>(); flask.mainRenderer = null; flask.registry = reg;
                flask.currentChemical = sugar; flask.currentLiquidVolume = 30f;   // must is in
                var fc = fgo.AddComponent<FermentationController>();
                fc.Bind(runner, flask, "ferment", co2, lime);
                A("edge: the flask IS fermenting once the must is prepared", fc.Fermenting);
                bool got = fc.BubbleInto(null);              // no limewater tube to lead into
                runner.Graph.Tick();
                A("edge: fermenting with no limewater never false-completes",
                    !got && !runner.Graph.IsComplete("ferment"));
            } finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(fgo); }
        }

        // E5b — GUIDANCE feature for that edge case: after bubbling a while with
        // nothing clouding, the player is nudged once (not spammed, not before).
        A("edge: forgot-the-limewater nudge fires once, only after a delay",
            !FermentationMath.ShouldNudge(true, false, 1f, false)                 // too soon
            && FermentationMath.ShouldNudge(true, false, 6f, false)               // now
            && !FermentationMath.ShouldNudge(true, false, 6f, true)               // already nudged
            && !FermentationMath.ShouldNudge(true, true, 6f, false)               // already confirmed
            && !FermentationMath.ShouldNudge(false, false, 6f, false));           // not fermenting

        // E6 — the Exp 3 BENCH vessels actually carry the safety/feedback wiring,
        // so the edge cases above fire in the built scene, not just in a probe.
        {
            var builder = UnityEngine.Object.FindAnyObjectByType<ExperimentSceneBuilder>();
            if (builder != null)
            {
                builder.Build("prelim-ethyl-alcohol");
                var tube = GameObject.Find("Kit_TestTube_6");   // the iodoform tube
                A("edge: Exp 3 tubes carry hazard + feedback + status wiring",
                    tube != null && tube.GetComponent<HazardousMixReactor>() != null
                    && tube.GetComponent<MixFeedback>() != null && tube.GetComponent<VesselStatus>() != null);
                builder.Build("tutorial-methane");              // tear down
            }
        }
    }

    static void ContentSuite()
    {
        string dir = "Assets/PharmaSynth/ScriptableObjects/Experiments/";
        foreach (var (file, tasks) in new[] {
            // Compounding: 13 tasks, manuscript Exp 2.
            ("Tutorial_Methane", 5), ("Prelim_ChemicalCompounding", 13), ("Prelim_EthylAlcohol", 8),
            ("Midterm_BenzoicAcid", 9),
            ("Midterm_Acetanilide", 8), ("Midterm_Acetone", 8), ("Midterm_Chloroform", 13),   // Exp 7 = two distillations + weigh
            ("Final_Benzamide", 10), ("Final_WineMaking", 7) })   // Exp 9 = zone-free ferment→rack→clarity/taste→label
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(dir + file + ".asset");
            A("content: " + file + " loads", m != null);
            if (m == null) continue;

            var g = m.BuildTaskGraph();
            A("content: " + file + " has " + tasks + " tasks", g.Tasks.Count == tasks);

            // The authored graph must be fully solvable respecting prerequisites,
            // proving there are no unreachable steps or prerequisite cycles.
            A("content: " + file + " solvable to 100%", DriveToCompletion(g) && g.Progress01 >= 0.999f);

            // Every phase that has tasks must be reachable/complete after a clean run.
            foreach (TaskPhase ph in System.Enum.GetValues(typeof(TaskPhase)))
            {
                bool hasPhase = false;
                foreach (var t in g.Tasks) if (t.phase == ph) { hasPhase = true; break; }
                if (hasPhase) A("content: " + file + " phase " + ph + " complete", g.IsPhaseComplete(ph));
            }

            // The scoring/mastery spine must build from the authored data.
            A("content: " + file + " builds mastery model", m.BuildMasteryModel() != null);
            A("content: " + file + " builds score calculator", m.BuildScoreCalculator() != null);

            // BEATABLE: a PERFECT run (every task completed, zero mistakes, all quiz
            // answers right) must clear the BKT mastery gate. With the default
            // parameters a skill observed ONCE caps at 0.68, so a module whose tracked
            // set holds several single-task skills can NEVER reach the 0.90 mean.
            // Each module's trackedSkills names its SIGNATURE skills (>=2
            // observations on a perfect run); this replays the exact runner
            // observation walk purely so no module ever ships unbeatable.
            var mastery = m.BuildMasteryModel();
            foreach (var t in m.graphTasks)
                if (mastery.IsTracked(t.skill)) mastery.Observe(t.skill, true);
            A("content: " + file + " is BEATABLE (perfect run clears the mastery gate: "
                + mastery.OverallMastery().ToString("0.000") + ")",
                mastery.OverallMastery() >= m.masteryThreshold);

            // FORGIVING (pTransit 0.30): the mastery gate is
            // INVISIBLE to the player — the grade screen shows a rubric %, so a
            // run that earns >=90 there must not be secretly failed by BKT. One
            // slip costs the rubric 4 points (procedure -0.10 × weight 0.40), so
            // a 2-mistake run still shows ~92% and MUST still pass mastery.
            // Models "fumbled each skill once, then got it right" — the realistic
            // shape of a learner's run. (Two slips on the SAME skill is a
            // different, harsher case: the 5-task tutorial gives its one tracked
            // skill only 2 opportunities, so it mathematically cannot recover —
            // correctly, since that IS a failing performance on a 5-step lab.)
            var forgiving = m.BuildMasteryModel();
            var fumbled = new HashSet<LabSkill>();
            foreach (var t in m.graphTasks)
            {
                if (!forgiving.IsTracked(t.skill)) continue;
                if (fumbled.Add(t.skill)) forgiving.Observe(t.skill, false);   // fumble it once
                forgiving.Observe(t.skill, true);                             // then get it right
            }
            A("content: " + file + " survives a fumble per skill (mastery "
                + forgiving.OverallMastery().ToString("0.000") + " still clears the gate)",
                forgiving.OverallMastery() >= m.masteryThreshold);

            // ILOs REACH THE PLAYER. intendedLearningOutcomes is synced to the
            // manuscript-verbatim IloCopy the intro speaks, rendered on the wrist
            // board for the whole run, and recapped by Dr. Jimenez before the quiz.
            var ilos = m.intendedLearningOutcomes;
            A("content: " + file + " carries its manuscript ILOs", ilos != null && ilos.Count > 0);
            var copy = IloCopy.ForModule(m.moduleId);
            bool iloMatch = ilos != null && copy != null && ilos.Count == copy.Length;
            if (iloMatch)
                for (int i = 0; i < copy.Length; i++)
                    if (ilos[i] != copy[i]) iloMatch = false;
            A("content: " + file + " ILOs match the manuscript (IloCopy) verbatim", iloMatch);
            A("content: " + file + " objectives render on the wrist board",
                ChecklistPager.BuildObjectivesHeader(m).Contains("OBJECTIVES")
                && (ilos.Count == 0 || ChecklistPager.BuildObjectivesHeader(m).Contains(ilos[0].Trim())));
        }
    }

    /// Repeatedly completes every currently-available task until none remain.
    /// Returns false if it stalls with required tasks still incomplete (unreachable step).
    static bool DriveToCompletion(TaskGraph g)
    {
        for (int guard = 0; guard < 100; guard++)
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (var t in g.AvailableTasks()) ids.Add(t.taskId);
            if (ids.Count == 0) break;
            foreach (var id in ids) g.TryComplete(id);
        }
        foreach (var t in g.Tasks)
            if (t.required && !g.IsComplete(t.taskId)) return false;
        return true;
    }

    // FORGIVENESS for an imprecise player: one slip is one mistake,
    // an early pour recovers by itself, and shelf stock can never be contaminated.
    static void ForgivenessSuite()
    {
        // Shared measure for builders: renderer union, or a fallback cube when nothing renders.
        {
            var parent = new GameObject("bounds_parent");
            var a = GameObject.CreatePrimitive(PrimitiveType.Cube); a.transform.SetParent(parent.transform); a.transform.position = Vector3.zero;
            var b = GameObject.CreatePrimitive(PrimitiveType.Cube); b.transform.SetParent(parent.transform); b.transform.position = new Vector3(2f, 0f, 0f);
            var empty = new GameObject("bounds_empty"); empty.transform.position = new Vector3(5f, 0f, 0f);
            try
            {
                var u = BoundsUtil.OfRenderers(parent);
                var f = BoundsUtil.OfRenderers(empty, 0.05f);
                A("util: bounds are the union of every child renderer", Near(u.min.x, -0.5f) && Near(u.max.x, 2.5f));
                A("util: no renderer gives a fallback cube at the object", Near(f.size.x, 0.05f) && Near(f.center.x, 5f));
                var list = parent.GetComponentsInChildren<Renderer>();
                var withHole = new Renderer[] { null, list[0], null, list[1] };
                A("util: a cached renderer list skips destroyed entries",
                    BoundsUtil.TryUnion(withHole, out var hb) && Near(hb.min.x, -0.5f) && Near(hb.max.x, 2.5f)
                    && !BoundsUtil.TryUnion(new Renderer[] { null }, out _));
                A("util: burners and matches are found by the scene search in edit mode",
                    BurnerController.Active.Count == UnityEngine.Object.FindObjectsByType<BurnerController>().Length
                    && Matchstick.Active.Count == UnityEngine.Object.FindObjectsByType<Matchstick>().Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); UnityEngine.Object.DestroyImmediate(empty); }
        }

        A("forgive: a held bottle with nothing under it only trickles; a knocked-over one spills fully",
            Near(PourMath.SpillAmount(10f, true, false), 1.5f) && Near(PourMath.SpillAmount(10f, true, true), 10f)
            && Near(PourMath.SpillAmount(10f, false, false), 10f));
        A("forgive: a brushing scoop tips only where it can't be a mistake",
            ScoopMath.DepositsOnTouch(false, false) && ScoopMath.DepositsOnTouch(true, true) && !ScoopMath.DepositsOnTouch(true, false));
        A("forgive: a bump off the balance pan keeps the settle timer",
            WeighMath.WithinBumpGrace(0.2f) && !WeighMath.WithinBumpGrace(0.6f));
        A("forgive: a too-gentle match strike is recognised (spark + hint), a resting touch is not",
            Matchstick.IsWeakStrike(true, false, false, 0.2f, true) && !Matchstick.IsWeakStrike(true, false, false, 0.02f, true)
            && !Matchstick.IsWeakStrike(true, false, false, 0.5f, true));
        A("calm: Pharmee repeats a warning only after 6 s, a different one at once",
            !PharmeeBrain.WarnAgain(LabErrorType.WrongReagent, LabErrorType.WrongReagent, 2f)
            && PharmeeBrain.WarnAgain(LabErrorType.WrongReagent, LabErrorType.WrongReagent, 7f)
            && PharmeeBrain.WarnAgain(LabErrorType.SpilledReagent, LabErrorType.WrongReagent, 1f));

        var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "Water";
        var other = ScriptableObject.CreateInstance<ChemicalData>(); other.chemicalName = "Ethanol";
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("first", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
            T("second", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "first"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        // LiquidPhysics needs a Renderer host — primitives, not bare GameObjects.
        var rg = new GameObject("forgive_runner");
        var vg = GameObject.CreatePrimitive(PrimitiveType.Cube); vg.name = "forgive_vessel";
        var shelf = new GameObject("Raw_TestStock");
        var jar = GameObject.CreatePrimitive(PrimitiveType.Cube); jar.name = "Jar"; jar.transform.SetParent(shelf.transform);
        try
        {
            var runner = rg.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            for (int i = 0; i < 30; i++) runner.CompleteTask("second");
            A("forgive: thirty early attempts are ONE out-of-order mistake", runner.MistakeCount == 1);

            runner.Retry();
            var lp = vg.AddComponent<LiquidPhysics>(); lp.maxVolume = 500f;
            var b = vg.AddComponent<LiquidTaskBinding>();
            b.SetVesselAndRunner(lp, runner);
            b.AddExpected(chem, "second", 10f);
            for (int i = 0; i < 12; i++) b.HandleReagent(chem, 1f);     // poured early, a tick at a time
            bool early = !runner.Graph.IsComplete("second") && runner.MistakeCount == 1;
            runner.CompleteTask("first");
            A("forgive: an early pour costs one mistake, not one per tick", early);
            A("forgive: the early-poured step completes when its prerequisite does", runner.Graph.IsComplete("second"));
            b.Detach();

            var stock = jar.AddComponent<LiquidPhysics>(); stock.currentChemical = chem; stock.currentLiquidVolume = 100f;
            A("forgive: shelf stock refuses a different chemical, accepts its own",
                LabReset.RefusesTransfer(stock, other) && !LabReset.RefusesTransfer(stock, chem));
            A("forgive: a work vessel takes anything", !LabReset.RefusesTransfer(lp, other));

            // Slips are charged to the grade, not to mastery (methane's single tracked skill).
            var meth = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>("Assets/PharmaSynth/ScriptableObjects/Experiments/Tutorial_Methane.asset");
            runner.SetModule(meth); runner.StartExperiment();
            foreach (var t in meth.graphTasks) runner.CompleteTask(t.taskId);
            float clean = runner.OverallMastery;
            runner.RecordMistake(LabErrorType.DroppedGlassware, "test");
            runner.RecordMistake(LabErrorType.SpilledReagent, "test");
            bool slipsSpared = Near(runner.OverallMastery, clean) && runner.OverallMastery >= meth.masteryThreshold;
            runner.RecordMistake(LabErrorType.WrongReagent, "test");
            A("forgive: a dropped tube or a spill never fails the methane mastery gate", slipsSpared);
            A("forgive: a wrong reagent still counts against mastery", runner.OverallMastery < clean);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rg); UnityEngine.Object.DestroyImmediate(vg); UnityEngine.Object.DestroyImmediate(shelf);
            UnityEngine.Object.DestroyImmediate(module); UnityEngine.Object.DestroyImmediate(chem); UnityEngine.Object.DestroyImmediate(other);
        }
    }

    // ILO opening dialogue (Pharmee states each experiment's
    // learning outcomes — verbatim Appendix C copy — in the intro cutscene).
    static void IloCopySuite()
    {
        string[] ids = {
            "tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol",
            "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
            "final-benzamide", "final-winemaking",
        };
        bool allPresent = true, allSized = true;
        foreach (var id in ids)
        {
            var ilos = IloCopy.ForModule(id);
            if (ilos.Length == 0) allPresent = false;
            foreach (var line in ilos)
                if (string.IsNullOrEmpty(line) || line.Length > 220) allSized = false;
        }
        A("ilo: all 11 modules covered", allPresent);
        A("ilo: subtitle-sized lines", allSized);
        A("ilo: synthesis modules keep manuscript verbs",
            IloCopy.ForModule("prelim-ethyl-alcohol")[0].Contains("Synthesize ethyl alcohol")
            && IloCopy.ForModule("final-benzamide")[1].Contains("identity through chemical tests"));
        A("ilo: unknown id → empty", IloCopy.ForModule("does-not-exist").Length == 0);
        A("ilo: beat pacing floors and caps",
            Near(IloCopy.BeatSeconds("short"), 2.5f) && Near(IloCopy.BeatSeconds(new string('x', 200)), 6f));

        var cs = ScriptableObject.CreateInstance<CutsceneData>();
        try
        {
            cs.beats = new List<CutsceneData.Beat> { new CutsceneData.Beat { subtitle = "greet", seconds = 3f } };
            int added = IloBeatInjector.InjectInto(cs, "prelim-ethyl-alcohol");
            A("ilo: injects lead-in + objectives", added == 3 && cs.beats.Count == 4);
            A("ilo: lead-in lands after the greeting", cs.beats[1].subtitle == IloCopy.LeadIn);
            A("ilo: objectives follow in order", cs.beats[2].subtitle.Contains("Objective 1"));
            A("ilo: re-inject is a no-op", IloBeatInjector.InjectInto(cs, "prelim-ethyl-alcohol") == 0 && cs.beats.Count == 4);
        }
        finally { UnityEngine.Object.DestroyImmediate(cs); }
    }
}
#endif
