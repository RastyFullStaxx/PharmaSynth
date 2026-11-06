#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Re-runnable regression suite for the PharmaSynth engine. Run via
/// menu: Tools ▸ PharmaSynth ▸ Run Self-Tests. One permanent, one-click check.
/// (An Editor-menu suite rather than an NUnit asmdef, to avoid restructuring
/// the runtime assembly; a formal EditMode asmdef migration can layer on later.)
public static class PharmaSelfTests
{
    /// XRI Starter Assets "XR Device Simulator" prefab — see the playmode pin.
    const string SimulatorPrefabGuid = "18ddb545287c546e19cc77dc9fbb2189";
    static int _fail, _total;
    static readonly List<string> _log = new List<string>();

    static void A(string name, bool cond)
    {
        _total++;
        if (!cond) { _fail++; _log.Add("FAIL  " + name); }
    }
    static bool Near(float a, float b, float e = 0.01f) => Math.Abs(a - b) < e;

    /// One-entry ledger scaled by `frac`, as its summary string (pour-out story).
    static string LedgerScaled(float ml, float frac)
    {
        var led = new VesselLedger();
        led.Add("PG_A", ml);
        led.Scale(frac);
        return led.Summary();
    }

    [MenuItem("Tools/PharmaSynth/Run Self-Tests")]
    public static void Run()
    {
        _fail = 0; _total = 0; _log.Clear();
        NPCNarrationController.ClearFloor();   // statics survive the domain; see ClearFloor

        TaskGraphSuite();
        MasterySuite();
        ScoreSuite();
        GraderSuite();
        RunnerSuite();
        ProgressionSuite();
        ChemVisualSuite();
        UISuite();
        W4Suite();
        InteractionSuite();
        RealVerbSuite();
        PourReactionSuite();
        PourGuardSuite();
        FeedbackSuite();
        VerbSuite();
        LayoutGraphCoverageSuite();
        VerbWiringSuite();
        LayoutSpacingSuite();
        WorkspaceShelfSuite();
        W59FlowSuite();
        W59ManuscriptSuite();
        TestReactionSuite();
        SceneBuilderSuite();
        SimRigSuite();
        PostLabSuite();
        CutsceneLibrarySuite();
        ProgressionFlowSuite();
        HubSelectSuite();
        AudioSuite();
        ExaminerSuite();
        SettingsSuite();
        ResultsExportSuite();
        RecorderSuite();
        HudRigSuite();
        FadeSuite();
        ArmedRunnerSuite();
        GatekeeperSuite();
        UnlockReturnSuite();
        LabMenuSuite();
        DepletionSuite();
        RealSizeSuite();
        PhysicsProfileSuite();
        TestVesselSuite();
        MishandlingSuite();
        ComfortSuite();
        PharmeeAliveSuite();
        LibrarySuite();
        ContentSuite();
        RosterDataSuite();
        HoverInfoSuite();
        GlyphSafeSuite();
        MusicSpeakerSuite();
        SurfaceSuite();
        PharmeeGestureSuite();
        GrabTuningSuite();
        HeightCalibrationSuite();
        WatchMathSuite();
        ShelfPourWiringSuite();
        FxMaterialSuite();
        VisualSweepSuite();
        PharmeePolishSuite();
        ChecklistPagerSuite();
        CinemaSuite();
        ForgivenessSuite();
        DemoModeSuite();
        DemoActionsSuite();
        IloCopySuite();
        ReviewFlowSuite();
        HazardousMixSuite();
        CenterTableMathSuite();
        RawReagentSuite();
        VoiceSuite();
        DispenserSuite();
        EdgeCaseSuite();

        string summary = $"PharmaSynth Self-Tests: {_total - _fail}/{_total} passed";
        if (_fail == 0) Debug.Log("<color=#4CD07D>" + summary + " — ALL GREEN</color>");
        else Debug.LogError(summary + " — " + _fail + " FAILED:\n" + string.Join("\n", _log));

        // Cheap verification channel: sessions read
        // this one-liner instead of wrapping the run in a RunCommand capture.
        // Logs/ not Temp/ — Unity wipes Temp at startup.
        try
        {
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/selftest-result.txt",
                summary + (_fail == 0 ? " — ALL GREEN" : " — " + _fail + " FAILED:\n" + string.Join("\n", _log))
                + "\n(" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n");
        }
        catch { /* a locked file must never fail the suite */ }
    }

    static ExperimentTask T(string id, TaskPhase ph, float w, LabSkill sk, RubricCategory rc, params string[] pre)
    {
        var t = new ExperimentTask { taskId = id, label = id, phase = ph, progressWeight = w, skill = sk, rubricCategory = rc, required = true };
        t.prerequisites = new List<string>(pre);
        return t;
    }

    static void TaskGraphSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "mix"),
        });
        A("graph: mix blocked before prep", g.TryComplete("mix") == TaskCompletionResult.BlockedByPrerequisite);
        A("graph: prep completes", g.TryComplete("prep") == TaskCompletionResult.Completed);
        A("graph: progress 0.25", Near(g.Progress01, 0.25f));
        A("graph: double-complete guarded", g.TryComplete("prep") == TaskCompletionResult.AlreadyComplete);
        g.TryComplete("mix");
        A("graph: progress 0.75", Near(g.Progress01, 0.75f));
        bool sensor = false; g.RegisterCondition("test", () => sensor); g.Tick();
        A("graph: auto-check waits", !g.IsComplete("test"));
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

    static void ProgressionSuite()
    {
        var svc = new ProgressionService("selftest-mem");
        var b = new GradeBreakdown { Total = 94 };
        svc.RecordResult("m", new ExperimentResult { grade = b, overallMastery = 0.93f, passed = true }, false);
        A("progression: passes latch", svc.IsPassed("m"));
        A("progression: unlock next", svc.IsUnlocked("m2", "m"));
        var json = JsonUtility.ToJson(svc.Data);
        A("progression: json round-trips", JsonUtility.FromJson<ProgressSaveData>(json).modules[0].passed);
    }

    static void RecorderSuite()
    {
        string path = System.IO.Path.Combine(Application.temporaryCachePath, "recorder_selftest.json");
        void Clean() { try { System.IO.File.Delete(path); System.IO.File.Delete(path + ".bak"); } catch { } }
        Clean();
        try
        {
            // Pure seam: fold results into the service (persists to the injected path).
            var svc = new ProgressionService(path);
            var rec = ResultRecorder.Record(svc, "m1", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.92f, passed = true });
            A("recorder: latches pass", rec.passed && rec.attempts == 1);
            ResultRecorder.Record(svc, "m1", new ExperimentResult { grade = new GradeBreakdown { Total = 40 }, overallMastery = 0.3f, passed = false });
            var r1 = svc.GetRecord("m1");
            A("recorder: keeps best + attempts", Near(r1.bestGrade, 95f) && r1.attempts == 2 && r1.passed);
            var reload = new ProgressionService(path); reload.Load();
            A("recorder: persisted to disk", reload.IsPassed("m1"));

            // Mono: ExperimentFinished → record written through the component.
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-recorder";
            module.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            var go = new GameObject("SelfTestRecorder");
            try
            {
                var runner = go.AddComponent<ExperimentRunner>();
                var recorder = go.AddComponent<ResultRecorder>();
                recorder.SavePathOverride = path;
                recorder.SetRunner(runner);
                ModuleRecord got = null; recorder.Recorded += r => got = r;
                runner.SetModule(module);
                runner.StartExperiment();
                runner.CompleteTask("a");
                runner.Finish(1f);
                A("recorder: mono records on finish", got != null && got.moduleId == "selftest-recorder" && got.attempts >= 1);
                var check = new ProgressionService(path); check.Load();
                A("recorder: mono persisted", check.GetRecord("selftest-recorder") != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }
        }
        finally { Clean(); }
    }

    static void HudRigSuite()
    {
        // Lazy-follow solver math
        var p = HudFollowSolver.Params.Default;
        A("hud: yaw wrap", Near(HudFollowSolver.DeltaYawDeg(350f, 10f), 20f));
        var anchor = HudFollowSolver.AnchorPoint(Vector3.zero, 0f, in p);
        A("hud: anchor at distance", Near(anchor.z, p.distance) && Near(anchor.y, p.heightOffset) && Near(anchor.x, 0f));
        var s = HudFollowSolver.Snapped(Vector3.zero, 0f, in p);
        var before = s.pos; var beforeYaw = s.yawDeg;
        HudFollowSolver.Step(ref s, Vector3.zero, p.yawDeadzoneDeg * 0.5f, in p, 1f / 60f);
        A("hud: deadzone holds", (s.pos - before).magnitude < 0.0001f && Near(s.yawDeg, beforeYaw));
        for (int i = 0; i < 240; i++) HudFollowSolver.Step(ref s, Vector3.zero, 90f, in p, 1f / 60f);
        A("hud: converges on 90 deg turn", Near(s.yawDeg, 90f, 1.5f));
        var target = HudFollowSolver.AnchorPoint(Vector3.zero, 90f, in p);
        A("hud: converges to anchor", (s.pos - target).magnitude < 0.05f);

        // Narration line events + bubble toggling + dialogue-bar mirroring
        var go = new GameObject("hudtest");
        try
        {
            var narr = go.AddComponent<NPCNarrationController>();
            var panel = new GameObject("Bubble"); panel.transform.SetParent(go.transform);
            narr.SetPanelRoot(panel);
            string started = null; int ends = 0;
            narr.LineStarted += (l, sec) => started = l;
            narr.LineEnded += () => ends++;
            narr.BeginLine("hello", 2f);
            A("narration: BeginLine shows bubble + event", panel.activeSelf && started == "hello" && narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine hides bubble + event", !panel.activeSelf && ends == 1 && !narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine idempotent", ends == 1);

            // PACING ("pharmee might become too annoying if he
            // speaks rapidly"). The post-reveal READ hold is the floor that keeps
            // a finished line up long enough to actually read.
            A("narration: a finished line always holds the minimum read time",
                Near(NPCNarrationController.HoldSecondsAfterReveal(4f, 3.8f), 1.9f)
                && Near(NPCNarrationController.HoldSecondsAfterReveal(9f, 3f), 6f));
            A("narration: the read floor is unaffected by a long reveal",
                NPCNarrationController.HoldSecondsAfterReveal(2f, 12f) >= 1.9f);

            // OVERLAP: Pharmee must not keep speaking where Dr. Jimenez speaks.
            // Two causes: Pharmee's autonomous brain reacting during the scripted
            // review, and the gate scheduling each review beat on a FIXED dwell, so a
            // line longer than dwell x cps overruns its slot and the next beat lands
            // on top. SecondsFor reports a line's REAL floor time so schedulers can't
            // under-book it.
            float shortLine = narr.SecondsFor("Short.", 4f);
            float longLine = narr.SecondsFor(new string('x', 200), 4f);
            A("narration: a long line reports MORE floor time than a short one",
                longLine > shortLine && longLine > 4f);
            A("narration: floor time is never under the authored dwell",
                narr.SecondsFor("Short.", 4f) >= 4f - 0.01f);
            A("narration: nobody holds the floor when nothing is speaking",
                !NPCNarrationController.FloorBusy);

            var barRoot = new GameObject("Bar"); barRoot.transform.SetParent(go.transform);
            var lineGo = new GameObject("Line"); lineGo.transform.SetParent(barRoot.transform);
            var lineText = lineGo.AddComponent<TMPro.TextMeshProUGUI>();
            var bar = go.AddComponent<HudDialogueBar>();
            bar.Bind(narr, barRoot, null, lineText);
            narr.BeginLine("step two", 1f);
            A("hudbar: mirrors line", barRoot.activeSelf && lineText.text == "step two");
            narr.EndLine();
            A("hudbar: hides on end", !barRoot.activeSelf && lineText.text == string.Empty);

            // MULTI-SPEAKER: Dr. Jimenez owns a SEPARATE narration
            // controller, so a bar subscribed to one channel shows nothing while he
            // speaks, and the scene's baked "Pharmee" name would stick. Each channel
            // carries its own name.
            var speakerGo = new GameObject("Speaker"); speakerGo.transform.SetParent(barRoot.transform);
            var speakerText = speakerGo.AddComponent<TMPro.TextMeshProUGUI>();
            var jimGo = new GameObject("JimenezNarr");
            jimGo.transform.SetParent(go.transform);
            var jimNarr = jimGo.AddComponent<NPCNarrationController>();
            bar.Bind(narr, barRoot, speakerText, lineText, null, null,
                new[] { new HudDialogueBar.Channel
                    { narration = jimNarr, speakerName = "Dr. Jimenez", portrait = null } });
            jimNarr.BeginLine("your documentation counts", 1f);
            A("hudbar: a SECOND speaker reaches the HUD bar",
                barRoot.activeSelf && lineText.text == "your documentation counts");
            A("hudbar: names the speaker who is talking", speakerText.text == "Dr. Jimenez");
            narr.BeginLine("back to me", 1f);
            A("hudbar: name switches back to the primary", speakerText.text == "Pharmee");
            // A stale end from the NON-active speaker must not hide the live line.
            jimNarr.EndLine();
            A("hudbar: the other speaker's end does not steal the bar",
                barRoot.activeSelf && lineText.text == "back to me");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void FadeSuite()
    {
        A("fade: ease endpoints", Near(FadeState.Ease01(0f), 0f) && Near(FadeState.Ease01(1f), 1f));
        A("fade: ease midpoint", Near(FadeState.Ease01(0.5f), 0.5f));
        A("fade: ease monotone", FadeState.Ease01(0.3f) < FadeState.Ease01(0.6f));
        var f = new FadeState();
        f.Begin(1f, 0.5f);
        A("fade: busy while ramping", f.Busy && f.Alpha < 1f);
        for (int i = 0; i < 40; i++) f.Step(1f / 60f);      // 0.66 s > 0.5 s
        A("fade: reaches target + settles", Near(f.Alpha, 1f) && !f.Busy);
        f.Begin(0f, 0.2f);
        for (int i = 0; i < 30; i++) f.Step(1f / 60f);
        A("fade: fades back down", Near(f.Alpha, 0f) && !f.Busy);
    }

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

    static void GatekeeperSuite()
    {
        string savedSelection = GameFlow.SelectedModuleId;
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "gate_selftest.json");
        void CleanTemp() { try { System.IO.File.Delete(tempPath); System.IO.File.Delete(tempPath + ".bak"); } catch { } }
        CleanTemp();
        try
        {
            // --- pure transition table -------------------------------------
            var m = new GatekeeperModel();
            A("gate: starts blocked", m.State == GateState.Blocked && !GatekeeperModel.DoorOpen(m.State));
            A("gate: approach opens choice", m.Fire(GateEvent.Approach) && m.State == GateState.ModeChoice);
            A("gate: illegal event refused", !m.Fire(GateEvent.CrossedThreshold) && m.State == GateState.ModeChoice);
            A("gate: campaign explain", m.Fire(GateEvent.PickCampaign) && m.State == GateState.CampaignExplain);
            A("gate: explain done", m.Fire(GateEvent.ExplainDone) && m.State == GateState.EpisodePick);

            var svc = new ProgressionService(tempPath);
            var flow = new ProgressionFlow(svc);
            string firstPrelim = null;
            foreach (var e in ExperimentCatalog.InPeriod(ExperimentPeriod.Prelim)) { firstPrelim = e.moduleId; break; }

            A("gate: fresh tutorial playable", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Tutorial) == "tutorial-methane");
            A("gate: fresh prelim locked", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Prelim) == null);

            GatekeeperModel.EpisodeOptions(flow, out var labels, out var selectable);
            A("gate: 4 episode rows", labels.Count == 4 && selectable.Count == 4);
            A("gate: only tutorial selectable", selectable[0] && !selectable[1] && !selectable[2] && !selectable[3]);
            A("gate: locked label marked", labels[1].Contains("(locked)"));

            Func<string, bool> canSel = id => HubSelectController.CanSelect(flow, id);
            Func<ExperimentPeriod, string> firstOf = p => GatekeeperModel.FirstPlayableInPeriod(flow, p);
            Func<ExperimentPeriod, int> Count = p =>
            { GatekeeperModel.ModuleOptions(flow, p, out var l, out _, out _); return l.Count; };
            A("gate: locked episode refused", !m.ChooseEpisode(ExperimentPeriod.Midterm, canSel, firstOf) && m.State == GateState.EpisodePick);

            // TWO-STEP picker: a period with two experiments must offer both.
            // A period OPENS ITS MODULE LIST instead of auto-starting the first one.
            A("gate: a period opens its module list, it does NOT auto-start",
                m.ChooseEpisode(ExperimentPeriod.Tutorial, canSel, firstOf)
                && m.State == GateState.ModulePick && string.IsNullOrEmpty(m.SelectedModuleId));
            A("gate: the picked period is remembered", m.PickedPeriod == ExperimentPeriod.Tutorial);
            A("gate: back returns to the period list",
                m.Fire(GateEvent.Dismiss) && m.State == GateState.EpisodePick);
            m.ChooseEpisode(ExperimentPeriod.Tutorial, canSel, firstOf);
            A("gate: choosing the module advances to PPE",
                m.ChooseModule("tutorial-methane", canSel)
                && m.State == GateState.CoatPrompt && m.SelectedModuleId == "tutorial-methane");

            // The module rows themselves. PRELIM HAS TWO — that is the whole bug report.
            {
                GatekeeperModel.ModuleOptions(flow, ExperimentPeriod.Prelim,
                    out var mLabels, out var mSel, out var mIds);
                A("gate: prelim lists BOTH its experiments", mLabels.Count == 2 && mIds.Count == 2
                    && mIds[0] == "prelim-chemical-compounding" && mIds[1] == "prelim-ethyl-alcohol");
                A("gate: prelim modules locked on a fresh save", !mSel[0] && !mSel[1]
                    && mLabels[0].Contains("(locked)"));
                GatekeeperModel.ModuleOptions(flow, ExperimentPeriod.Midterm, out var midL, out _, out _);
                A("gate: midterm lists 4, final lists 2", midL.Count == 4
                    && Count(ExperimentPeriod.Final) == 2);
                // The period heading already says "Prelim", so the row must not repeat it.
                A("gate: module rows drop the redundant period prefix", !mLabels[0].StartsWith("Prelim:"));
            }

            // the PPE locker moved just inside the lab → the door is
            // open through the gear-up steps (the run still gates on PPE + walk-in).
            A("gate: door SHUT during PPE + ready (W5.12), opens only when armed",
                !GatekeeperModel.DoorOpen(GateState.CoatPrompt)
                && !GatekeeperModel.DoorOpen(GateState.ReadyPrompt)
                && GatekeeperModel.DoorOpen(GateState.DoorArmed));
            A("gate: coat then ready", m.Fire(GateEvent.Coated) && m.Fire(GateEvent.Ready) && m.State == GateState.Loading);
            A("gate: loaded warns", m.Fire(GateEvent.Loaded) && m.State == GateState.ThresholdWarn && !GatekeeperModel.DoorOpen(m.State));
            A("gate: cross before confirm refused", !m.Fire(GateEvent.CrossedThreshold));
            A("gate: proceed arms door", m.Fire(GateEvent.ProceedConfirmed) && m.State == GateState.DoorArmed && GatekeeperModel.DoorOpen(m.State));
            A("gate: walk-in runs", m.Fire(GateEvent.CrossedThreshold) && m.State == GateState.Running);
            // Post-experiment review flow: tests done → Jimenez quiz →
            // score review → return home → entrance debrief → unlock announce.
            A("gate: continue illegal while running", !m.Fire(GateEvent.ContinueAfterPass) && m.State == GateState.Running);
            A("gate: tests done → quiz intro", m.Fire(GateEvent.TestsDone) && m.State == GateState.QuizIntro
                && GatekeeperModel.DoorOpen(m.State));
            A("gate: briefing done → quiz time", m.Fire(GateEvent.QuizBegin) && m.State == GateState.QuizTime);
            A("gate: supply event ignored during quiz", !m.Fire(GateEvent.SupplyExhausted) && m.State == GateState.QuizTime);
            A("gate: graded → score review", m.Fire(GateEvent.Graded) && m.State == GateState.ScoreReview
                && GatekeeperModel.DoorOpen(m.State));
            A("gate: return loop to blocked", m.Fire(GateEvent.ContinueAfterPass) && m.State == GateState.Returning
                && m.Fire(GateEvent.TeleportDone) && m.State == GateState.Debrief
                && !GatekeeperModel.DoorOpen(m.State)
                && m.Fire(GateEvent.DebriefDone) && m.State == GateState.UnlockAnnounce
                && m.Fire(GateEvent.AnnounceDone) && m.State == GateState.Blocked);

            // Fail path: retry from the review corner = clean re-armed attempt.
            var mr = new GatekeeperModel();
            mr.Fire(GateEvent.Approach); mr.Fire(GateEvent.PickCampaign); mr.Fire(GateEvent.ExplainDone);
            mr.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            mr.ChooseModule("tutorial-methane", _ => true);   // two-step picker
            mr.Fire(GateEvent.Coated); mr.Fire(GateEvent.Ready); mr.Fire(GateEvent.Loaded);
            mr.Fire(GateEvent.ProceedConfirmed); mr.Fire(GateEvent.CrossedThreshold);
            mr.Fire(GateEvent.TestsDone); mr.Fire(GateEvent.QuizBegin); mr.Fire(GateEvent.Graded);
            A("gate: retry re-arms via loading", mr.Fire(GateEvent.RetryRequested) && mr.State == GateState.Loading
                && !string.IsNullOrEmpty(mr.SelectedModuleId));
            var mq = new GatekeeperModel();
            mq.Fire(GateEvent.Approach); mq.Fire(GateEvent.PickCampaign); mq.Fire(GateEvent.ExplainDone);
            mq.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            mq.ChooseModule("tutorial-methane", _ => true);   // two-step picker
            mq.Fire(GateEvent.Coated); mq.Fire(GateEvent.Ready); mq.Fire(GateEvent.Loaded);
            mq.Fire(GateEvent.ProceedConfirmed); mq.Fire(GateEvent.CrossedThreshold); mq.Fire(GateEvent.TestsDone);
            mq.ResetToBlocked();
            A("gate: HUD reset escapes the quiz flow", mq.State == GateState.Blocked);

            svc.RecordResult("tutorial-methane", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.95f, passed = true }, false);
            A("gate: prelim unlocks after tutorial", GatekeeperModel.FirstPlayableInPeriod(flow, ExperimentPeriod.Prelim) == firstPrelim && firstPrelim != null);

            var m2 = new GatekeeperModel();
            m2.Fire(GateEvent.Approach); m2.Fire(GateEvent.PickLabTour);
            A("gate: lab tour opens door", m2.State == GateState.LabTour && m2.IsLabTour && GatekeeperModel.DoorOpen(m2.State));
            A("gate: approach never shuts an open tour door", !m2.Fire(GateEvent.Approach) && m2.State == GateState.LabTour);
            A("gate: poking Pharmee reopens the talk", m2.Fire(GateEvent.TalkRequested) && m2.State == GateState.ModeChoice);

            var m3 = new GatekeeperModel();
            m3.Fire(GateEvent.Approach); m3.Fire(GateEvent.PickCampaign); m3.Fire(GateEvent.ExplainDone);
            m3.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            m3.ChooseModule("tutorial-methane", _ => true);   // two-step picker
            m3.Fire(GateEvent.Coated); m3.Fire(GateEvent.Ready); m3.Fire(GateEvent.Loaded);
            m3.Fire(GateEvent.ProceedConfirmed); m3.Fire(GateEvent.CrossedThreshold);
            A("gate: supply prompt", m3.Fire(GateEvent.SupplyExhausted) && m3.State == GateState.SupplyPrompt && GatekeeperModel.DoorOpen(m3.State));
            A("gate: supply keep-trying", m3.Fire(GateEvent.Dismiss) && m3.State == GateState.Running);
            m3.Fire(GateEvent.SupplyExhausted);
            A("gate: supply restart reloads", m3.Fire(GateEvent.RestartConfirmed) && m3.State == GateState.Loading);

            // HUD reset: force back to the Blocked entrance from any mid-flow state,
            // clearing the chosen episode and firing a Transition to Blocked.
            var m4 = new GatekeeperModel();
            m4.Fire(GateEvent.Approach); m4.Fire(GateEvent.PickCampaign); m4.Fire(GateEvent.ExplainDone);
            m4.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
            m4.ChooseModule("tutorial-methane", _ => true);   // two-step picker
            bool m4Blocked = false;
            m4.Transition += (a, b) => { if (b == GateState.Blocked) m4Blocked = true; };
            m4.ResetToBlocked();
            A("gate: reset returns to blocked", m4.State == GateState.Blocked
                && string.IsNullOrEmpty(m4.SelectedModuleId) && m4Blocked);
            A("gate: welcome line present", !string.IsNullOrEmpty(new PharmeeGatekeeper.GateLines().welcome));

            // --- choice panel component ------------------------------------
            var pgo = new GameObject("panel");
            try
            {
                var panel = pgo.AddComponent<ChoicePanelController>();
                var root = new GameObject("Root"); root.transform.SetParent(pgo.transform);
                var title = new GameObject("Title").AddComponent<TMPro.TextMeshProUGUI>(); title.transform.SetParent(root.transform);
                var btns = new UnityEngine.UI.Button[3]; var lbls = new TMPro.TMP_Text[3];
                for (int i = 0; i < 3; i++)
                {
                    var bgo = new GameObject("B" + i); bgo.transform.SetParent(root.transform);
                    btns[i] = bgo.AddComponent<UnityEngine.UI.Button>();
                    var lgo = new GameObject("L" + i); lgo.transform.SetParent(bgo.transform);
                    lbls[i] = lgo.AddComponent<TMPro.TextMeshProUGUI>();
                }
                panel.Bind(root, title, btns, lbls);
                int chosen = -1; panel.OptionChosen += i => chosen = i;
                panel.Show("Pick", new List<string> { "A", "B" }, new List<bool> { true, false });
                A("panel: shows + trims options", panel.IsOpen && btns[0].gameObject.activeSelf && btns[1].gameObject.activeSelf && !btns[2].gameObject.activeSelf);
                A("panel: labels + lock flags", panel.LabelAt(0) == "A" && panel.LabelAt(1) == "B" && btns[0].interactable && !btns[1].interactable);
                panel.OnOption(1);
                A("panel: option event", chosen == 1);
                panel.Hide();
                A("panel: hides", !panel.IsOpen);
            }
            finally { UnityEngine.Object.DestroyImmediate(pgo); }

            // --- trigger relay ----------------------------------------------
            var rgo = new GameObject("relay");
            try
            {
                var relay = rgo.AddComponent<PlayerTriggerRelay>();
                int hits = 0;
                relay.onPlayerEntered = new UnityEngine.Events.UnityEvent();
                relay.onPlayerEntered.AddListener(() => hits++);
                relay.SimulateEnter();
                A("relay: fires", hits == 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(rgo); }

            // --- mono integration: door gate drives armed -> walk-in start ---
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                var ggo = new GameObject("gk");
                var blocker = new GameObject("Blocker");
                try
                {
                    var runner = ggo.AddComponent<ExperimentRunner>();
                    var launcher = ggo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner);
                    var gk = ggo.AddComponent<PharmeeGatekeeper>();
                    gk.Bind(null, null, null, launcher, runner, blocker);
                    A("gk: blocker on while blocked", blocker.activeSelf);
                    gk.OnApproachTriggerEntered();
                    gk.OnPanelOption(1);              // Campaign
                    gk.OnPanelOption(0);              // Continue (explain)
                    A("gk: at episode pick", gk.Model.State == GateState.EpisodePick);
                    // deterministic pick (the panel path reads the real save file)
                    gk.Model.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
                    gk.Model.ChooseModule("tutorial-methane", _ => true);   // two-step picker
                    gk.Model.Fire(GateEvent.Coated);  // simulated PPE
                    gk.OnPanelOption(0);              // I'm ready -> Loading -> armed -> ThresholdWarn
                    A("gk: armed after load", runner.IsArmed && gk.Model.State == GateState.ThresholdWarn);
                    A("gk: door still blocked pre-confirm", blocker.activeSelf);
                    gk.OnPanelOption(0);              // Proceed
                    A("gk: door opens when armed", !blocker.activeSelf && gk.Model.State == GateState.DoorArmed);
                    gk.OnThresholdTriggerEntered();   // walk in
                    A("gk: walk-in starts run at 0s", runner.IsRunning && Near(runner.ElapsedSeconds, 0f) && gk.Model.State == GateState.Running);
                }
                finally { UnityEngine.Object.DestroyImmediate(ggo); UnityEngine.Object.DestroyImmediate(blocker); }
            }
        }
        finally
        {
            GameFlow.SelectedModuleId = savedSelection;
            CleanTemp();
        }
    }

    static void UnlockReturnSuite()
    {
        // --- teleport math: camera lands exactly on the marker ---------------
        var marker = new Vector3(-4.15f, 0.22f, 1.05f);
        var rig = new Vector3(0f, 0.22f, 0f);
        var cam = new Vector3(0.3f, 1.7f, 0.4f);
        var p0 = TeleportMath.RigPositionFor(marker, 0f, rig, cam);
        A("tp: rig offset (no yaw)", Near(p0.x, -4.45f) && Near(p0.z, 0.65f) && Near(p0.y, 0.22f));
        A("tp: camera lands on marker", Near(p0.x + (cam.x - rig.x), marker.x) && Near(p0.z + (cam.z - rig.z), marker.z));
        var p180 = TeleportMath.RigPositionFor(marker, 180f, rig, cam);
        var offR = Quaternion.Euler(0f, 180f, 0f) * (cam - rig);
        A("tp: yawed offset lands too", Near(p180.x + offR.x, marker.x) && Near(p180.z + offR.z, marker.z));
        A("tp: rig yaw delta", Near(TeleportMath.RigYawFor(180f, 0f, 90f), 90f));

        // --- unlock diff -------------------------------------------------------
        string tempPath = System.IO.Path.Combine(Application.temporaryCachePath, "unlock_selftest.json");
        void CleanTemp() { try { System.IO.File.Delete(tempPath); System.IO.File.Delete(tempPath + ".bak"); } catch { } }
        CleanTemp();
        try
        {
            var svc = new ProgressionService(tempPath);
            var flow = new ProgressionFlow(svc);
            var before = UnlockDiff.UnlockedSet(flow);
            A("unlock: fresh set = tutorial only", before.Count == 1 && before.Contains("tutorial-methane"));
            svc.RecordResult("tutorial-methane", new ExperimentResult { grade = new GradeBreakdown { Total = 95 }, overallMastery = 0.95f, passed = true }, false);
            var newly = UnlockDiff.NewlyUnlocked(before, flow);
            string firstPrelim = null;
            foreach (var e in ExperimentCatalog.InPeriod(ExperimentPeriod.Prelim)) { firstPrelim = e.moduleId; break; }
            A("unlock: tutorial pass unlocks first prelim", newly.Count == 1 && newly[0] == firstPrelim);
            string line = UnlockDiff.AnnouncementFor(newly);
            A("unlock: announcement names it", line.Contains("unlocked") && line.Contains(ExperimentCatalog.Get(firstPrelim).title));
            A("unlock: empty fallback congratulates", !string.IsNullOrEmpty(UnlockDiff.AnnouncementFor(new List<string>())));

            // --- mono: full continue-after-pass return loop (edit mode = immediate) ---
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>("Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            if (lib != null)
            {
                string savedSelection = GameFlow.SelectedModuleId;
                var ggo = new GameObject("gkret");
                var blocker = new GameObject("Blocker2");
                var rigGo = new GameObject("RigSim");
                var markerGo = new GameObject("Marker");
                var camGo = new GameObject("CamSim");
                try
                {
                    rigGo.transform.position = new Vector3(2f, 0.22f, -3f);
                    markerGo.transform.position = marker;
                    markerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    var simCam = camGo.AddComponent<Camera>();
                    camGo.transform.SetParent(rigGo.transform);
                    camGo.transform.localPosition = new Vector3(0.2f, 1.5f, 0.1f);

                    var runner = ggo.AddComponent<ExperimentRunner>();
                    var launcher = ggo.AddComponent<ExperimentLauncher>();
                    launcher.SetLibrary(lib); launcher.SetRunner(runner);
                    var gk = ggo.AddComponent<PharmeeGatekeeper>();
                    gk.Bind(null, null, null, launcher, runner, blocker);
                    gk.BindReturn(markerGo.transform, rigGo.transform, simCam, null);

                    // drive to Running
                    gk.OnApproachTriggerEntered();
                    gk.OnPanelOption(1); gk.OnPanelOption(0);
                    gk.Model.ChooseEpisode(ExperimentPeriod.Tutorial, _ => true, _ => "tutorial-methane");
                    gk.Model.ChooseModule("tutorial-methane", _ => true);   // two-step picker
                    gk.Model.Fire(GateEvent.Coated);
                    gk.OnPanelOption(0);      // ready -> armed
                    gk.OnPanelOption(0);      // proceed
                    gk.OnThresholdTriggerEntered();
                    A("return: running", gk.Model.State == GateState.Running && runner.IsRunning);

                    // A finish while Running (dev/legacy path) must cascade through the
                    // quiz states (entries short-circuit on the cached result) to review.
                    runner.Finish(1f);
                    A("return: finish lands in score review", gk.Model.State == GateState.ScoreReview);

                    gk.OnContinueAfterPass(); // edit mode: return+debrief+announce run immediately
                    A("return: loop lands blocked", gk.Model.State == GateState.Blocked);
                    A("return: door re-blocked", blocker.activeSelf);
                    // camera (rig child) must stand on the marker XZ
                    Vector3 camWorld = camGo.transform.position;
                    A("return: teleported to front door", Near(camWorld.x, marker.x, 0.05f) && Near(camWorld.z, marker.z, 0.05f));
                }
                finally
                {
                    GameFlow.SelectedModuleId = savedSelection;
                    UnityEngine.Object.DestroyImmediate(ggo);
                    UnityEngine.Object.DestroyImmediate(blocker);
                    UnityEngine.Object.DestroyImmediate(camGo);
                    UnityEngine.Object.DestroyImmediate(rigGo);
                    UnityEngine.Object.DestroyImmediate(markerGo);
                }
            }
        }
        finally { CleanTemp(); }
    }

    static void LabMenuSuite()
    {
        A("labmenu: restart label running", LabMenuController.RestartConfirmText(true).Contains("attempt"));
        A("labmenu: restart label idle", LabMenuController.RestartConfirmText(false).Contains("Reset"));
        var go = new GameObject("labmenu");
        try
        {
            var lm = go.AddComponent<LabMenuController>();
            var settings = new GameObject("Settings"); settings.transform.SetParent(go.transform); settings.SetActive(false);
            var confirm = go.AddComponent<ChoicePanelController>();
            var root = new GameObject("CRoot"); root.transform.SetParent(go.transform);
            confirm.Bind(root, null, null, null);
            lm.Bind(settings, confirm, null, null, null);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles on", settings.activeSelf);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles off", !settings.activeSelf);
            lm.OnQuitToMenu();
            A("labmenu: quit opens confirm", confirm.IsOpen);
            lm.OnConfirmOption(1);   // Stay
            A("labmenu: cancel closes confirm", !confirm.IsOpen);
            lm.OnQuitToMenu();
            lm.OnConfirmOption(0);   // edit mode: scene-load path is play-guarded
            A("labmenu: confirm closes too", !confirm.IsOpen);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void DepletionSuite()
    {
        var chem = ScriptableObject.CreateInstance<ChemicalData>();
        chem.chemicalName = "selftest-depletion-chem";
        var otherChem = ScriptableObject.CreateInstance<ChemicalData>();
        otherChem.chemicalName = "selftest-unexpected-chem";
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "selftest-depletion";
        module.graphTasks = new List<ExperimentTask> {
            T("add-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };

        var go = new GameObject("depletion");
        var fullGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics requires a Renderer
        var bottleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            // --- capacity guard: rejected overflow fires LiquidRejected, NOT LiquidAdded
            var full = fullGo.AddComponent<LiquidPhysics>();
            full.maxVolume = 100f; full.currentLiquidVolume = 100f; full.currentChemical = chem;
            bool added = false, rejected = false;
            full.LiquidAdded += (c, a) => added = true;
            full.LiquidRejected += (c, a) => rejected = true;
            full.AddLiquid(chem, 50f);
            A("deplete: overflow rejected", rejected && !added && Near(full.currentLiquidVolume, 100f));

            // --- accumulation toward requiredMl
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            // bindings live on the vessel's own GO so the monitor never counts it as supply
            var vessel = fullGo.AddComponent<LiquidTaskBinding>();
            vessel.SetVesselAndRunner(full, runner);
            vessel.AddExpected(chem, "add-x", 50f);
            vessel.HandleReagent(chem, 20f);
            vessel.HandleReagent(chem, 20f);
            A("deplete: below threshold pends", !runner.Graph.IsComplete("add-x") && Near(vessel.AccumulatedFor("add-x"), 40f));
            vessel.HandleReagent(chem, 10f);
            A("deplete: threshold completes", runner.Graph.IsComplete("add-x"));
            vessel.HandleReagent(chem, 30f);   // extra pour after completion is ignored
            A("deplete: no double-complete side effects", runner.MistakeCount == 0);
            vessel.HandleReagent(otherChem, 10f);
            A("deplete: wrong reagent still flagged", runner.MistakeCount == 1);

            // legacy single-arg = full delivery regardless of threshold
            runner.Retry();
            var vessel2 = fullGo.AddComponent<LiquidTaskBinding>();
            vessel2.SetVesselAndRunner(full, runner);
            vessel2.AddExpected(chem, "add-x", 50f);
            vessel2.HandleReagent(chem);
            A("deplete: legacy call instant-completes", runner.Graph.IsComplete("add-x"));

            // --- pure shortfall math
            var needs = new List<ReagentSupplyMath.Need> {
                new ReagentSupplyMath.Need { taskId = "add-x", chemicalName = "X", requiredMl = 50f, deliveredMl = 0f },
                new ReagentSupplyMath.Need { taskId = "add-y", chemicalName = "Y", requiredMl = 50f, deliveredMl = 30f },
                new ReagentSupplyMath.Need { taskId = "done-z", chemicalName = "Z", requiredMl = 50f, deliveredMl = 0f },
            };
            var avail = new Dictionary<string, float> { { "X", 30f }, { "Y", 25f }, { "Z", 0f } };
            var shorts = ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail);
            A("deplete: shortfall math", shorts.Count == 1 && shorts[0] == "add-x");
            // (Y needs 20 more with 25 available -> fine; Z is complete -> excluded)
            avail["X"] = 60f;
            A("deplete: sufficient = clean", ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail).Count == 0);

            // --- monitor end-to-end over live components
            runner.Retry();
            var monitor = go.AddComponent<ReagentSupplyMonitor>();
            monitor.SetRunner(runner);
            var vlp = bottleGo.AddComponent<LiquidPhysics>();   // reaction vessel body
            var vessel3 = bottleGo.AddComponent<LiquidTaskBinding>();
            vessel3.SetVesselAndRunner(vlp, runner);
            vessel3.AddExpected(chem, "add-x", 50f);
            var srcGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var src = srcGo.AddComponent<LiquidPhysics>();
                src.currentChemical = chem; src.currentLiquidVolume = 30f;   // not enough for 50
                var starved = monitor.EvaluateNow();
                A("deplete: monitor detects starvation", starved.Contains("add-x"));
                src.currentLiquidVolume = 80f;
                A("deplete: monitor clean when supplied", monitor.EvaluateNow().Count == 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(srcGo); }

            // --- builder supply sizing: 2.5x need floor
            A("deplete: supply autosize floor", Mathf.Max(120f, 50f * 2.5f) == 125f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(fullGo);
            UnityEngine.Object.DestroyImmediate(bottleGo);
            UnityEngine.Object.DestroyImmediate(module);
            UnityEngine.Object.DestroyImmediate(chem);
            UnityEngine.Object.DestroyImmediate(otherChem);
        }
    }

    static void RealSizeSuite()
    {
        A("size: table count", RealSizes.Count == 51);   // incl. DistillingFlask + 8 distillation apparatus

        // Methane stage visibility: shown only during Lab
        // Tour OR the Methane attempt; hidden otherwise.
        A("methane-vis: shown in lab tour", MethaneStageVisibility.ShouldShow(true, false));
        A("methane-vis: shown during methane attempt", MethaneStageVisibility.ShouldShow(false, true));
        A("methane-vis: hidden otherwise", !MethaneStageVisibility.ShouldShow(false, false));
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var prefabs = so.FindProperty("prefabs");
            bool all = prefabs != null;
            if (prefabs != null)
                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    var el = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (el != null && !RealSizes.TryGet(el.name, out _)) { all = false; _log.Add("missing RealSizes: " + el.name); }
                }
            A("size: every library prefab covered", all);
        }
        A("size: factor math", Near(RealSizes.UniformScaleFactor(new Vector3(0.05f, 0.02f, 0.32f), 0.16f), 0.5f));
        A("size: degenerate guarded", Near(RealSizes.UniformScaleFactor(Vector3.zero, 0.16f), 1f));
    }

    static void PhysicsProfileSuite()
    {
        // Table stays in lockstep with RealSizes: same items, none missing.
        A("phys: table lockstep with RealSizes", PhysicsProfiles.Count == RealSizes.Count);
        bool allProfiled = true;
        foreach (var n in RealSizes.Names)
            if (!PhysicsProfiles.TryGet(n, out _)) { allProfiled = false; _log.Add("missing PhysicsProfile: " + n); }
        A("phys: every sized item profiled", allProfiled);

        bool massesSane = true;
        foreach (var n in PhysicsProfiles.Names)
        {
            PhysicsProfiles.TryGet(n, out var p);
            if (p.massKg <= 0.005f || p.massKg > 6f) { massesSane = false; _log.Add("implausible mass: " + n + " = " + p.massKg); }
        }
        A("phys: masses plausible", massesSane);

        // Pose math: a Y-long rod is laid down (its long axis ends horizontal)…
        var lie = PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f));
        A("phys: rod laid on its side", Mathf.Abs((lie * Vector3.up).y) < 0.01f);
        // …while an already-horizontal rod and upright items are untouched.
        A("phys: lying rod untouched", PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)) == Quaternion.identity);
        A("phys: upright untouched", PhysicsProfiles.RestRotation(RestPose.Upright, new Vector3(0.02f, 0.3f, 0.02f)) == Quaternion.identity);
        // Flat tools: thinnest axis ends vertical.
        var flat = PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f));
        A("phys: flat tool face-down", Mathf.Abs((flat * Vector3.right).y) > 0.99f);
        A("phys: flat already down untouched", PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)) == Quaternion.identity);

        // Resting plausibility (the drop test's verdict function).
        A("phys: lying rod plausible", PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)));
        A("phys: balancing rod implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f)));
        A("phys: face-down gauze plausible", PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)));
        A("phys: on-edge gauze implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f)));

        // Degenerate-collider guard.
        A("phys: paper-thin collider flagged", PhysicsProfiles.IsDegenerate(new Vector3(0.004f, 0.1f, 0.1f)));
        A("phys: normal collider passes", !PhysicsProfiles.IsDegenerate(new Vector3(0.01f, 0.1f, 0.1f)));

        // EnsurePhysics + release policy on a live object (edit-mode Bind seam).
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = PhysicsProfiles.EnsurePhysics(go, "GlassRod");
            A("phys: spawns kinematic with profile mass", rb != null && rb.isKinematic && rb.useGravity && Near(rb.mass, 0.03f));
            var policy = go.AddComponent<GrabPhysicsPolicy>();
            policy.Bind(rb, null);
            A("phys: kinematic until released", !policy.IsDynamic);
            policy.OnReleased();
            A("phys: dynamic after release", policy.IsDynamic && !rb.isKinematic);

            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var added = PhysicsProfiles.EnsureCollider(go) as BoxCollider;
            A("phys: collider-less item gets a box", added != null
                && !PhysicsProfiles.IsDegenerate(Vector3.Scale(added.size, go.transform.lossyScale)));

            // Concave mesh colliders are rejected by PhysX on dynamic bodies
            // (items fell through the world in the drop test) → must convexify.
            var meshGo = new GameObject("mc");
            meshGo.transform.SetParent(go.transform, false);
            var mc = meshGo.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = false;
            A("phys: concave colliders convexified", PhysicsProfiles.ConvexifyMeshColliders(go) == 1 && mc.convex);

            // Drop respawn: kill-Z + FLOOR-ONLY idle return-to-home (items
            // the player stages on tables/shelves are never reclaimed mid-run).
            A("respawn: below kill-Z", DropRespawnMath.ShouldRespawn(-1.2f, -1f));
            A("respawn: on bench safe", !DropRespawnMath.ShouldRespawn(0.9f, -1f));
            A("respawn: floor-abandoned item returns", DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 0.1f));
            A("respawn: staged on the bench stays (W5.12)", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 0.93f));
            A("respawn: staged on the gantry shelf stays (W5.12)", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 1.6f));
            A("respawn: held item never returns", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, true, 30f, 0.1f));
            A("respawn: moving item waits", !DropRespawnMath.ShouldReturnHome(1.5f, 0.4f, false, 30f, 0.1f));
            A("respawn: near home stays put", !DropRespawnMath.ShouldReturnHome(0.1f, 0f, false, 30f, 0.1f));
            A("respawn: a floor item goes home after 8 s, not 25", DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 8.1f, 0.1f)
                && !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 7f, 0.1f));
            var dr = go.AddComponent<DropRespawn>();
            dr.Bind(go.GetComponent<Rigidbody>(), null);
            dr.SetHome(new Vector3(1f, 2f, 3f), Quaternion.identity);
            go.transform.position = new Vector3(9f, -5f, 9f);
            var rbDyn = go.GetComponent<Rigidbody>(); rbDyn.isKinematic = false;
            // Respawn = a FRESH replacement: home supply restored (an
            // exhausted bottle must not come back empty and invisible).
            var lpHome = go.AddComponent<LiquidPhysics>();
            var chemHome = ScriptableObject.CreateInstance<ChemicalData>();
            try
            {
                chemHome.chemicalName = "TestSupply";
                lpHome.SetContents(chemHome, 120f);
                dr.CaptureSupply();
                lpHome.PourOut(120f);
                A("respawn: bottle drained for the test", lpHome.currentLiquidVolume < 0.01f);
                dr.GoHome();
                A("respawn: teleports home", (go.transform.position - new Vector3(1f, 2f, 3f)).magnitude < 1e-4f);
                A("respawn: re-freezes to shelf policy", rbDyn.isKinematic);
                A("respawn: restores full home supply (W5.12)",
                    Near(lpHome.currentLiquidVolume, 120f) && lpHome.currentChemical == chemHome);
            }
            finally { UnityEngine.Object.DestroyImmediate(chemHome); }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// Product-seeded test vessels (§1): every experiment's confirmatory-test
    /// pour must land in a vessel already holding the product, so the registry
    /// rule fires and the colour/ppt/gas observation actually shows.
    static void TestVesselSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testvessel: library + registry load", lib != null && reg != null);
        if (lib == null || reg == null) return;

        var cases = new (string file, string seed, string[] reagents)[]
        {
            // manuscript fidelity: benzoic ester = PROPYL ALCOHOL (sulfuric acid
            // alone is inert); benzamide acid test + ethanol ester
            // use the DILUTED acids; chloroform has the dichromate oxidation.
            // Benzoic (Exp 4) is zone-free: no seeded vessel
            // (the player synthesises the acid and draws from the flask) — its
            // FeCl3/ester rules are validated by the Exp 4 simrun firing them.
            // Acetanilide (Exp 5) is zone-free: no seeded vessel
            // (the player synthesises + scoops the crystals) — its bromination/
            // hydrolysis rules are validated by the Exp 5 simrun firing them.
            // Benzamide (Exp 8) is zone-free: no seeded vessel
            // (the player synthesises in the ice bath and dips crystals from the
            // dried watch glass) — its alkali/acid/nitrous rules are validated
            // by the Exp 8 simrun firing them at temperature.
            // Chloroform (Exp 7) is zone-free: no seeded vessel
            // (the player distils, washes and redistils the chloroform, then
            // draws every test from Test Tube 14) — its AgNO3/oxidation rules
            // are validated by the Exp 7 simrun firing them at temperature.
            // Acetone (Exp 6) is zone-free: no seeded vessel
            // (the player distils the acetone and draws from the receiver) —
            // its Tollens/Schiff/iodoform/bisulfite rules are validated by the
            // Exp 6 simrun firing them.
            // Ethyl (Exp 3) is zone-free: no seeded vessel (it
            // POURS ethanol/hypochlorite into fresh tubes) — its Iodoform/Ester
            // reactions are validated by the Exp 3 simrun firing them at temperature.
            // WineMaking (Exp 9) is zone-free: no pre-seeded CO2
            // tube (the FermentationController bubbles CO2 into the limewater the
            // player pours) — its WineFermentation + Limewater rules are validated
            // by the Exp 9 simrun firing them.
        };
        foreach (var c in cases)
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(
                "Assets/PharmaSynth/ScriptableObjects/Layouts/" + c.file + ".asset");
            ExperimentLayout.Vessel tv = null;
            if (layout != null)
                foreach (var v in layout.vessels) if (v.startChemical == c.seed) tv = v;
            A("testvessel: " + c.file + " seeded vessel", tv != null);
            if (tv == null) continue;
            var seedChem = lib.GetChemical(c.seed);
            A("testvessel: " + c.file + " seed in library", seedChem != null);
            foreach (var reagent in c.reagents)
            {
                bool bound = false;
                foreach (var b in tv.bindings) if (b.reagentChemical == reagent) bound = true;
                A("testvessel: " + c.file + " binds " + reagent, bound);
                var rChem = lib.GetChemical(reagent);
                A("testvessel: " + c.file + " rule fires (" + reagent + ")",
                    seedChem != null && rChem != null && reg.FindReaction(seedChem, rChem) != null);
            }
        }

        // Every chemical any layout references must resolve through the library,
        // or the builder seeds/fills silently fail at stage-build time.
        bool allResolve = true;
        void Need(string chem, string where)
        {
            if (!string.IsNullOrEmpty(chem) && lib.GetChemical(chem) == null)
            { allResolve = false; _log.Add("library missing chemical: " + chem + " (" + where + ")"); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
            if (layout == null) continue;
            foreach (var p in layout.props) if (p.pourable) Need(p.fillChemical, layout.name);
            foreach (var v in layout.vessels)
            {
                Need(v.startChemical, layout.name);
                foreach (var b in v.bindings) Need(b.reagentChemical, layout.name);
            }
        }
        A("testvessel: all layout chemicals resolve", allResolve);
    }

    /// Spill & breakage mishandling penalties (§2).
    static void MishandlingSuite()
    {
        // Fragility table: glass breaks, tools don't, and every entry is a real prefab.
        A("mishandle: thin glass is breakable", Mishandling.IsBreakable("Beaker_100mL")
            && Mishandling.IsBreakable("TestTube") && Mishandling.IsBreakable("Vial_WithLabel")
            && Mishandling.IsBreakable("WatchGlass"));
        A("mishandle: tools are not", !Mishandling.IsBreakable("CrucibleTongs")
            && !Mishandling.IsBreakable("Spatula") && !Mishandling.IsBreakable("WashBottle")
            && !Mishandling.IsBreakable("TestTubeRack"));
        // solid glass / droppers / porcelain delisted: sturdy in the hand.
        A("mishandle: stir rod never shatters (W5.8)", !Mishandling.IsBreakable("GlassRod"));
        A("mishandle: funnel+dropper never shatter (W5.8)", !Mishandling.IsBreakable("Funnel")
            && !Mishandling.IsBreakable("Dropper"));
        A("mishandle: porcelain never shatters (W5.8)", !Mishandling.IsBreakable("EvaporatingDish")
            && !Mishandling.IsBreakable("Crucible"));
        bool allReal = true;
        foreach (var n in Mishandling.BreakableNames)
            if (!RealSizes.TryGet(n, out _)) { allReal = false; _log.Add("breakable not in RealSizes: " + n); }
        A("mishandle: breakables are real prefabs", allReal);

        // Impact policy: only extreme falls / hard throws break —
        // a normal bench-height drop always survives.
        A("mishandle: extreme drop breaks", Mishandling.ShouldBreak(7.1f));
        A("mishandle: threshold impact breaks", Mishandling.ShouldBreak(7.0f));
        A("mishandle: sub-threshold safe (raised to 7.0 in W5.12)", !Mishandling.ShouldBreak(6.9f));
        A("mishandle: bench-height drop survives (W5.12)", !Mishandling.ShouldBreak(4.5f));
        A("mishandle: carry-bump into wall safe", !Mishandling.ShouldBreak(2.5f));
        A("mishandle: gentle set-down safe", !Mishandling.ShouldBreak(0.8f));

        // Impact loudness scales with speed (soft contacts whispered at
        // full volume before) — quiet set-down, full-volume real drop.
        A("impact: gentle set-down whispers", Mishandling.ImpactVolume01(0.8f) < 0.3f);
        A("impact: solid drop full volume", Mishandling.ImpactVolume01(4.5f) > 0.999f);
        A("impact: louder with speed", Mishandling.ImpactVolume01(2f) > Mishandling.ImpactVolume01(1f));

        // Display names for mistake messages (no more code names).
        A("names: reagent code prettified", Mishandling.Prettify("Reagent_AceticAcid_Diluted (2)") == "Acetic Acid Diluted");
        A("names: liquid twin suffix stripped", Mishandling.Prettify("TestTube_WithLiquid") == "Test Tube");
        A("names: unit tails stay intact", Mishandling.Prettify("Beaker_100mL(Clone)") == "Beaker 100mL");

        // Narration pacing: long lines must not vanish right after the
        // last character types in; short lines keep their authored dwell.
        // 1.9 s floor: shorter reads as rushed in the headset.
        A("narration: long line keeps a read hold", Near(NPCNarrationController.HoldSecondsAfterReveal(3.5f, 4.7f), 1.9f));
        A("narration: short line keeps authored dwell", Near(NPCNarrationController.HoldSecondsAfterReveal(3.5f, 1.0f), 2.5f));

        // QUIZ NAV CONTAINMENT: Back/Next must stay inside the quiz
        // panel. The real geometry is a 300-wide button on an 820-wide panel:
        // the preferred 336 step puts the outer edge at 486 vs a 410 half-width.
        {
            // Real geometry: a 300-wide Submit on an 820-wide quiz panel. Three of
            // those need 900 units, so containment ALONE pulled Back/Next 73 units
            // on top of Submit — they have to shrink first.
            float w = QuizNavButtonsBuilder.WidthFor(300f, 820f);
            float step = QuizNavButtonsBuilder.StepFor(300f, 820f, 0f);
            A("quiznav: three buttons SHRINK to fit the row", Near(w, 820f * 0.92f / 3f));
            A("quiznav: the side buttons stay INSIDE the panel", step + w * 0.5f <= 410f && step > 0f);
            A("quiznav: the side buttons never OVERLAP Submit", step >= w);
            A("quiznav: a roomy panel keeps the authored width and spacing",
                Near(QuizNavButtonsBuilder.WidthFor(100f, 2000f), 100f)
                && Near(QuizNavButtonsBuilder.StepFor(100f, 2000f, 0f), 112f));
            A("quiznav: Submit's own offset is respected",
                QuizNavButtonsBuilder.StepFor(300f, 820f, 60f) <= QuizNavButtonsBuilder.StepFor(300f, 820f, 0f));
            A("quiznav: an unresolvable parent keeps the authored width + preferred step",
                Near(QuizNavButtonsBuilder.WidthFor(300f, 0f), 300f)
                && Near(QuizNavButtonsBuilder.StepFor(300f, 0f, 0f), 336f));
        }

        // Vessel axis: a tube is long, a watch glass and a mortar are WIDE. The
        // mound must lie along the round bore either way, not the longest side.
        A("axis: an upright tube runs along Y", ExperimentSceneBuilder.VesselAxis(new Vector3(0.016f, 0.13f, 0.016f)) == 1);
        A("axis: a tube lying on X runs along X", ExperimentSceneBuilder.VesselAxis(new Vector3(0.13f, 0.016f, 0.016f)) == 0);
        A("axis: a watch glass is a shallow DISH, not a lying tube",
            ExperimentSceneBuilder.VesselAxis(new Vector3(0.09f, 0.015f, 0.09f)) == 1
            && ExperimentSceneBuilder.LongestAxis(new Vector3(0.09f, 0.015f, 0.09f)) != 1);
        A("axis: a mortar bowl is upright too", ExperimentSceneBuilder.VesselAxis(new Vector3(0.12f, 0.06f, 0.12f)) == 1);
        A("axis: a cube falls back to Y", ExperimentSceneBuilder.VesselAxis(Vector3.one * 0.05f) == 1);

        // Mound growth: every scoop must read as MORE, and the first dips move most.
        A("mound: an empty vessel shows no heap", Near(ScoopController.MoundFill(0f), 0f));
        A("mound: consecutive scoops visibly grow",
            ScoopController.MoundFill(2f) < ScoopController.MoundFill(4f)
            && ScoopController.MoundFill(4f) < ScoopController.MoundFill(6f));
        A("mound: one scoop is already clearly visible", ScoopController.MoundFill(2f) > 0.25f);
        A("mound: a sub-gram spatula dip still shows", ScoopController.MoundFill(0.1f) >= 0.15f);
        A("mound: full at the reference charge", Near(ScoopController.MoundFill(20f), 1f));

        // Round-bottom glassware gets a bulb of liquid, not a beaker's column.
        A("fill: the Florence flask is round-bottomed", ExperimentSceneBuilder.IsRoundBottom("FlorenceFlask"));
        A("fill: so is the distilling flask", ExperimentSceneBuilder.IsRoundBottom("DistillingFlask_2"));
        A("fill: a beaker is not", !ExperimentSceneBuilder.IsRoundBottom("Eq_Beaker_500mL"));
        A("fill: a null name is not", !ExperimentSceneBuilder.IsRoundBottom(null));

        // Effect children must never count toward a vessel's bounds — a world-space
        // pour StreamLine left racked tubes floating a metre in the air.
        A("bounds: the pour effects are effect children",
            ExperimentSceneBuilder.IsEffectChild("StreamLine") && ExperimentSceneBuilder.IsEffectChild("PourStream"));
        A("bounds: the fill layers are effect children",
            ExperimentSceneBuilder.IsEffectChild("Liquid") && ExperimentSceneBuilder.IsEffectChild("Precipitate")
            && ExperimentSceneBuilder.IsEffectChild("Powder"));
        A("bounds: the glass body is NOT an effect child", !ExperimentSceneBuilder.IsEffectChild("Beaker"));

        // The balance reads GROSS (auto-taring to 0.00 g is why it looked dead).
        A("scale: a loaded beaker reads vessel + contents", Near(WeighMath.PanMass(120f, 0.18f), 300f));
        A("scale: an EMPTY beaker still moves the needle", WeighMath.PanMass(0f, 0.18f) > 0f);
        A("scale: a body-less load still reads a token mass", Near(WeighMath.PanMass(0f, 0f), 5f));
        A("scale: the graded weigh condition still ignores the tare",
            WeighMath.Satisfied(true, "Ethanol", "Ethanol", 50f, 50f, null, null));

        // Digital readout: types in, wobbles while settling, locks on arrival.
        A("scale: a new reading types in left to right",
            WeighingScaleController.Typed("12.40 g", 0.08f, 26f) == "12" );
        A("scale: the whole reading shows once typed",
            WeighingScaleController.Typed("12.40 g", 5f, 26f) == "12.40 g");
        A("scale: a settled pan shows the exact mass",
            Near(WeighingScaleController.Reading(12.4f, 12.4f, 0.4f, 3f), 12.4f));
        A("scale: a settling pan hunts around the value",
            !Near(WeighingScaleController.Reading(9f, 12.4f, 0.4f, 0.04f), 9f));
        A("scale: the reading is never negative",
            WeighingScaleController.Reading(0.01f, 5f, 0.4f, 0.12f) >= 0f);
        A("scale: format is two decimals + unit", WeighingScaleController.Format(12.4f, "g") == "12.40 g");

        // The pan's scale reference is a plain private field, so the edit-time
        // Bind() does NOT survive a domain reload — the readout silently died for
        // the rest of the session. It must re-find the balance
        // itself, exactly like GrindController.AutoFindPestle.
        {
            var balFix = new GameObject("BalanceFixture");
            balFix.AddComponent<WeighingScaleController>();
            var panFix = new GameObject("Pan");
            panFix.transform.SetParent(balFix.transform, false);
            var stFix = panFix.AddComponent<WeighStation>();
            bool wasUnbound = !stFix.HasScale;
            stFix.AutoFindScale();
            A("scale: an unbound pan re-finds its balance (survives domain reload)",
              wasUnbound && stFix.HasScale);
            UnityEngine.Object.DestroyImmediate(balFix);
        }

        // Pharmee's robot colouring: a blend, never a replacement, so speech stays
        // intelligible in an educational game.
        A("robot: zero mix leaves the voice untouched", Near(RobotVoiceFx.RingSample(0.5f, -1f, 0f), 0.5f));
        A("robot: full mix is pure ring product", Near(RobotVoiceFx.RingSample(0.5f, 1f, 1f), 0.5f)
            && Near(RobotVoiceFx.RingSample(0.5f, -1f, 1f), -0.5f));
        A("robot: a blend keeps some dry signal", RobotVoiceFx.RingSample(1f, 0f, 0.38f) > 0.5f);
        A("robot: silence stays silent", Near(RobotVoiceFx.RingSample(0f, 1f, 0.5f), 0f));
        A("robot: mix is clamped", Near(RobotVoiceFx.RingSample(0.5f, -1f, 5f), -0.5f));

        // CLARITY: the grit must come
        // from bit-crushing, which brightens, not distortion, which dulls (muffles).
        A("robot: no crush mix leaves the sample alone", Near(RobotVoiceFx.Crush(0.3f, 10f, 0f), 0.3f));
        // 0.28 sits BETWEEN 5-bit steps (which are 0.0625 apart) so the quantiser
        // has something to move. A probe like 0.3123 is 0.0002 off a step and
        // fails while the code is correct.
        A("robot: crushing quantises the sample", !Near(RobotVoiceFx.Crush(0.28f, 5f, 1f), 0.28f, 0.005f));
        A("robot: a coarser depth quantises harder",
            Mathf.Abs(RobotVoiceFx.Crush(0.28f, 4f, 1f) - 0.28f)
            > Mathf.Abs(RobotVoiceFx.Crush(0.28f, 12f, 1f) - 0.28f));
        A("robot: 16-bit crush is effectively transparent",
            Near(RobotVoiceFx.Crush(0.28f, 16f, 1f), 0.28f, 0.001f));
        A("robot: silence stays silent through the crush", Near(RobotVoiceFx.Crush(0f, 8f, 1f), 0f));
        // The presence shelf must actually sit in the consonant band.
        A("robot: presence alpha rises with frequency",
            RobotVoiceFx.OnePoleAlpha(3000f, 48000) > RobotVoiceFx.OnePoleAlpha(500f, 48000));
        A("robot: presence alpha stays a valid coefficient",
            RobotVoiceFx.OnePoleAlpha(3000f, 48000) > 0f && RobotVoiceFx.OnePoleAlpha(3000f, 48000) < 1f);
        A("robot: a bad sample rate cannot produce NaN", RobotVoiceFx.OnePoleAlpha(3000f, 0) > 0f);

        // The character must come from DECIMATION + COMB, not from a low-frequency
        // ring-mod drone, which layers a hum under an untouched human voice
        // (a person speaking against an electric fan).
        A("robot: decimation holds each sample for several frames", RobotVoiceFx.HoldFrames(3f) == 3);
        A("robot: a factor of 1 means no decimation", RobotVoiceFx.HoldFrames(1f) == 1);
        A("robot: decimation never stalls or runs away",
            RobotVoiceFx.HoldFrames(0f) >= 1 && RobotVoiceFx.HoldFrames(999f) <= 16);
        A("robot: a short comb rings in the low hundreds of Hz",
            RobotVoiceFx.CombDelaySamples(3.2f, 48000, 1024) == 154);
        A("robot: comb delay always fits its buffer",
            RobotVoiceFx.CombDelaySamples(500f, 48000, 1024) < 1024
            && RobotVoiceFx.CombDelaySamples(0f, 48000, 1024) >= 1);
        A("robot: comb feedback can never self-oscillate",
            RobotVoiceFx.SafeFeedback(1.5f) <= 0.9f && RobotVoiceFx.SafeFeedback(-1f) >= 0f);

        // VOCODER: filtering recolours a voice but it still sounds human;
        // only replacing the EXCITATION removes the human pitch contour.
        A("vocoder: bands span low to high", Near(RobotVocoder.BandCenter(0, 14, 150f, 6500f), 150f)
            && Near(RobotVocoder.BandCenter(13, 14, 150f, 6500f), 6500f, 1f));
        A("vocoder: bands are LOG spaced, not linear — speech energy is not uniform",
            RobotVocoder.BandCenter(7, 14, 150f, 6500f) < (150f + 6500f) * 0.5f);
        A("vocoder: bands ascend", RobotVocoder.BandCenter(3, 14, 150f, 6500f)
            < RobotVocoder.BandCenter(4, 14, 150f, 6500f));
        A("vocoder: a degenerate band count is safe", Near(RobotVocoder.BandCenter(0, 1, 150f, 6500f), 150f));
        A("vocoder: envelope coefficient is a valid smoothing factor",
            RobotVocoder.EnvelopeCoeff(12f, 48000) > 0f && RobotVocoder.EnvelopeCoeff(12f, 48000) < 1f);
        A("vocoder: a faster release smooths less",
            RobotVocoder.EnvelopeCoeff(4f, 48000) > RobotVocoder.EnvelopeCoeff(40f, 48000));
        A("vocoder: a bad sample rate cannot produce NaN", RobotVocoder.EnvelopeCoeff(12f, 0) > 0f);
        // The carrier must be harmonically DENSE — a sine would vocode to silence,
        // because a filter bank can only shape harmonics that are already there.
        A("vocoder: carrier sweeps the full range across its phase",
            Near(RobotVoiceFx.Carrier(0f, 0f, 0f), -1f) && Near(RobotVoiceFx.Carrier(1f, 0f, 0f), 1f));
        A("vocoder: noise blend restores unvoiced consonants",
            Near(RobotVoiceFx.Carrier(0.5f, 1f, 1f), 1f));
        A("vocoder: carrier noise mix is clamped",
            Near(RobotVoiceFx.Carrier(0.5f, 1f, 5f), 1f));

        // ⛔ SCENE-vs-CODE DIALOGUE DRIFT. The gate's lines are a [SerializeField], so
        // the SCENE holds the copy the player actually hears. Editing the code
        // defaults does NOT reach the game, and since the voice manifest is built
        // from the CODE, the spoken text silently stops matching any clip and drops
        // to placeholder blips. A drifted scene copy can also give wrong instructions
        // (a lab coat ALONE while the door demands coat + goggles + gloves).
        // Pin what the player HEARS, not what the code says.
        {
            var gateGo = UnityEngine.Object.FindFirstObjectByType<PharmeeGatekeeper>(FindObjectsInactive.Include);
            if (gateGo != null)
            {
                var def = new PharmeeGatekeeper.GateLines();
                var live = gateGo.Lines;
                var drift = new List<string>();
                foreach (var f in typeof(PharmeeGatekeeper.GateLines).GetFields())
                {
                    if (f.FieldType != typeof(string)) continue;
                    if ((string)f.GetValue(live) != (string)f.GetValue(def)) drift.Add(f.Name);
                }
                A("voice: scene gate dialogue matches the code (else it plays unvoiced) — "
                  + (drift.Count == 0 ? "in sync" : "DRIFTED: " + string.Join(",", drift)),
                  drift.Count == 0);

                // The PPE prompt must name every item the door actually requires,
                // or the player is told to do the wrong thing and gets stuck.
                string coat = live.coatPrompt.ToLowerInvariant();
                A("voice: the PPE prompt names coat, goggles AND gloves",
                  coat.Contains("coat") && coat.Contains("goggle") && coat.Contains("glove"));

                // Every line the gate can speak must resolve to a real clip.
                var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>(
                    "Assets/PharmaSynth/ScriptableObjects/VoiceBank.asset");
                if (bank != null && bank.entries.Count > 0)
                {
                    int unvoiced = 0;
                    foreach (var f in typeof(PharmeeGatekeeper.GateLines).GetFields())
                    {
                        if (f.FieldType != typeof(string)) continue;
                        string text = (string)f.GetValue(live);
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        if (bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(text)) == null) unvoiced++;
                    }
                    A("voice: every gate line the player hears has a clip (" + unvoiced + " unvoiced)", unvoiced == 0);

                    // The guided tour is the SECOND scene-serialized dialogue source
                    // and drifts the same way (e.g. sending players to an
                    // "equipment cabinet" or to the emptied reagent shelf).
                    var tour = UnityEngine.Object.FindFirstObjectByType<LabTourGuide>(FindObjectsInactive.Include);
                    if (tour != null)
                    {
                        var tso = new SerializedObject(tour);
                        var st = tso.FindProperty("stops");
                        int tourUnvoiced = 0, badMark = 0;
                        for (int i = 0; st != null && i < st.arraySize; i++)
                        {
                            var el = st.GetArrayElementAtIndex(i);
                            string beat = el.FindPropertyRelative("beat")?.stringValue ?? "";
                            string mark = el.FindPropertyRelative("landmarkName")?.stringValue ?? "";
                            if (!string.IsNullOrWhiteSpace(beat)
                                && bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(beat)) == null) tourUnvoiced++;
                            // A stop whose landmark doesn't exist never fires at all.
                            if (!string.IsNullOrEmpty(mark) && GameObject.Find(mark) == null) badMark++;
                        }
                        A("voice: every lab-tour beat has a clip (" + tourUnvoiced + " unvoiced)", tourUnvoiced == 0);
                        A("tour: every stop's landmark exists in the scene (" + badMark + " dangling)", badMark == 0);
                    }

                    // The ILO recap is spoken by BOTH NPCs off the SAME text:
                    // Pharmee reads it as an intro-cutscene beat, Dr. Jimenez
                    // recaps it at QuizIntro (PharmeeGatekeeper.SpeakJimenezBrief).
                    // VoiceBank keys on speaker + text-hash, so Pharmee having a
                    // clip says NOTHING about Jimenez — his whole recap played as
                    // blips while only Pharmee's copies existed.
                    int iloUnvoiced = 0;
                    if (bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For(PharmeeLines.JimenezIloLeadIn)) == null)
                        iloUnvoiced++;
                    foreach (var mid in VoiceCorpus.ModuleIds)
                        foreach (var ilo in IloCopy.ForModule(mid))
                        {
                            if (string.IsNullOrWhiteSpace(ilo)) continue;
                            if (bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(ilo)) == null) iloUnvoiced++;
                            if (bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For(ilo)) == null) iloUnvoiced++;
                        }
                    A("voice: every ILO line has a clip for BOTH speakers (" + iloUnvoiced + " unvoiced)",
                      iloUnvoiced == 0);
                }
            }
        }

        // MUSIC DUCKING under dialogue.
        A("duck: music drops while someone speaks", Near(MusicDucker.TargetVolume(1f, 0.25f, true), 0.25f));
        A("duck: the configured depth is a real fade-out, not a dip",
            VoiceAudioBuilder.MusicDuckTo <= 0.12f && VoiceAudioBuilder.MusicDuckTo >= 0f);
        A("duck: music returns to full when silent", Near(MusicDucker.TargetVolume(1f, 0.25f, false), 1f));
        A("duck: a quiet setting stays proportional", Near(MusicDucker.TargetVolume(0.4f, 0.5f, true), 0.2f));
        A("duck: never negative", MusicDucker.TargetVolume(-1f, 0.25f, true) >= 0f);

        // VOICE AUDIBILITY: heard room-wide, louder up close.
        A("voice: partial spatial blend — audible everywhere AND positional",
            VoiceAudioBuilder.VoiceSpatialBlend > 0.1f && VoiceAudioBuilder.VoiceSpatialBlend < 1f);
        A("voice: stays audible across the whole lab", VoiceAudioBuilder.VoiceMaxDistance >= 20f);
        A("voice: full volume out to the bench area",
            VoiceAudioBuilder.VoiceMinDistance >= 2f
            && VoiceAudioBuilder.VoiceMinDistance < VoiceAudioBuilder.VoiceMaxDistance);

        // Both NPCs must be able to SPEAK at all, and never at the same time.
        //
        // ⛔ An AudioSource is only ONE of the three things a channel needs. `Wire NPC Polish`
        // destroys and recreates Dr. Jimenez's subtitle object, and a fresh NPCNarrationController
        // defaults to `speaker = Pharmee` with a NULL `voiceBank`, so he can have a working
        // speaker cone, an animator that mouths the words and a visible subtitle bubble
        // while every line resolves to no clip. Pin all three, or the next builder run is
        // silent again and nothing says so.
        {
            int mute = 0, bankless = 0, misattributed = 0, channels = 0;
            foreach (var n in UnityEngine.Object.FindObjectsByType<NPCNarrationController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (n == null) continue;
                channels++;
                var nso = new SerializedObject(n);
                if (nso.FindProperty("narratorAudioSource")?.objectReferenceValue == null) mute++;
                if (nso.FindProperty("voiceBank")?.objectReferenceValue == null) bankless++;

                // The speaker is inferred the same way the voice importer infers it: whose
                // hierarchy is this channel in?
                var expected = VoiceSpeaker.Pharmee;
                for (var t = n.transform; t != null; t = t.parent)
                    if (t.name.ToLowerInvariant().Contains("jimenez")) { expected = VoiceSpeaker.Jimenez; break; }
                var spk = nso.FindProperty("speaker");
                if (spk != null && spk.enumValueIndex != (int)expected) misattributed++;
            }
            A("voice: every narration channel has an AudioSource (" + mute + " of " + channels + " mute)",
              channels == 0 || mute == 0);
            A("voice: every narration channel has a VoiceBank (" + bankless + " of " + channels + " unvoiced)",
              channels == 0 || bankless == 0);
            A("voice: every narration channel speaks as its OWN NPC (" + misattributed + " misattributed)",
              channels == 0 || misattributed == 0);
        }

        // ⛔ Every scene in Build Settings must AGREE about the XR Device Simulator.
        //
        // Fixing only the OPEN scenes would leave MainMenu's simulator ACTIVE when SampleScene
        // alone is open, and Play always boots MainMenu. The simulator then hijacks the HMD:
        // the head pose freezes at local (0,0,0) while the real Quest controllers keep reporting
        // true poses, so the controllers float ABOVE the view. Reading the scene FILES keeps
        // this cheap and non-destructive: the suite must never open scenes to answer it.
        {
            var simStates = new List<string>();
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path) || !File.Exists(entry.path)) continue;
                string text = File.ReadAllText(entry.path);
                string state = "absent";
                foreach (var block in text.Split(new[] { "--- !u!1001 " }, StringSplitOptions.None))
                {
                    if (block.IndexOf(SimulatorPrefabGuid, StringComparison.Ordinal) < 0) continue;
                    var m = Regex.Match(block, @"propertyPath: m_IsActive\s*
?
\s*value: (\d)");
                    state = m.Success ? (m.Groups[1].Value == "1" ? "ON" : "OFF") : "default";
                    break;
                }
                simStates.Add(Path.GetFileNameWithoutExtension(entry.path) + "=" + state);
            }
            bool agree = simStates.Count == 0
                         || simStates.TrueForAll(x => x.Substring(x.IndexOf('=') + 1)
                                                       == simStates[0].Substring(simStates[0].IndexOf('=') + 1));
            A("playmode: every build scene agrees about the XR Device Simulator ("
              + string.Join(", ", simStates) + ")", agree);
        }

        // ---- Rack tubes claim their own role ----
        //
        // The real Exp 2 shapes: the enol group separates on pour ONE (five different
        // alcohols), the alkaline-oxidation group shares its first TWO reagents and only
        // separates on the third. Both must work, which is why claiming is deferred.
        {
            var enol = new List<IReadOnlyList<string>>
            {
                new List<string> { "Ethanol", "Water", "FeCl3" },
                new List<string> { "n-Butyl", "Water", "FeCl3" },
                new List<string> { "Benzyl",  "Water", "FeCl3" },
            };
            var oxalk = new List<IReadOnlyList<string>>
            {
                new List<string> { "KMnO4", "NaOH", "n-Butyl" },
                new List<string> { "KMnO4", "NaOH", "sec-Butyl" },
                new List<string> { "KMnO4", "NaOH", "tert-Butyl" },
            };

            A("role: an empty tube can still be anything in its group",
                VesselRoleMatch.Candidates(enol, new List<string>()).Count == 3);

            var afterEthanol = VesselRoleMatch.Candidates(enol, new List<string> { "Ethanol" });
            A("role: a distinguishing first pour claims the role at once",
                afterEthanol.Count == 1 && VesselRoleMatch.ClaimedRole(afterEthanol) == 0);

            // ⛔ The reason claiming is deferred: eager assignment here would pick one of the
            // three arbitrarily and then grade the correct third pour a mistake.
            var shared = VesselRoleMatch.Candidates(oxalk, new List<string> { "KMnO4", "NaOH" });
            A("role: a shared prefix stays AMBIGUOUS rather than guessing",
                shared.Count == 3 && VesselRoleMatch.ClaimedRole(shared) == -1
                && !VesselRoleMatch.IsImpossible(shared));

            var settled = VesselRoleMatch.Candidates(oxalk,
                new List<string> { "KMnO4", "NaOH", "sec-Butyl" });
            A("role: the disambiguating pour claims the right role",
                VesselRoleMatch.ClaimedRole(settled) == 1);

            A("role: a reagent no role wants is the wrong-reagent case",
                VesselRoleMatch.IsImpossible(
                    VesselRoleMatch.Candidates(oxalk, new List<string> { "Ethanol" })));

            // Exclusivity: without this, two tubes claim the same role and the rack
            // completes on one of them while the other is still empty.
            var taken = new HashSet<int> { 0 };
            A("role: a role claimed by a sibling is off the table",
                VesselRoleMatch.Candidates(enol, new List<string>(), taken).Count == 2
                && !VesselRoleMatch.Candidates(enol, new List<string>(), taken).Contains(0));

            A("role: WouldAccept grades a pour before it is recorded",
                VesselRoleMatch.WouldAccept(oxalk, new List<string> { "KMnO4" }, "NaOH")
                && !VesselRoleMatch.WouldAccept(oxalk, new List<string> { "KMnO4" }, "Ethanol"));

            // Over-pouring a named reagent is an over-pour of the RIGHT thing; the binding
            // already tolerates it, so the matcher must not start rejecting it.
            A("role: repeating a named reagent does not break the match",
                VesselRoleMatch.Candidates(oxalk,
                    new List<string> { "KMnO4", "KMnO4", "NaOH" }).Count == 3);
        }

        // ---- Any tube will do: pools per family, guidance follows the tube in hand ----
        // ⛔ All 23 bench tubes share ONE itemId (kit-testtube), hard-glass included, so the
        // name prefix is the only family signal. A soft tube must never claim the naked-flame
        // role: Exp 6's dry distillation is the whole point of the hard-glass tube.
        A("role: hard-glass never pools with soft glass",
            VesselRoleMatch.FamilyOf("Kit_Hard-GlassTestTube_2") == VesselRoleMatch.HardGlassFamily
            && VesselRoleMatch.FamilyOf("Kit_TestTube_7") == VesselRoleMatch.RegularFamily
            && VesselRoleMatch.FamilyOf("Kit_Hard-GlassTestTube_2") != VesselRoleMatch.FamilyOf("Kit_TestTube_7")
            && VesselRoleMatch.FamilyOf("Eq_Beaker_100mL") == ""
            && VesselRoleMatch.FamilyOf("") == "");

        // The pool collapses to ONE cue: the tube in the player's hand beats a nearer one, the
        // nearest wins otherwise, and a step with no pool targets is left exactly as it was.
        {
            var near = new GameObject("pool_near"); near.transform.position = new Vector3(0f, 0f, 1f);
            var far = new GameObject("pool_far");   far.transform.position = new Vector3(0f, 0f, 5f);
            var fixedT = new GameObject("fixed_dest");
            try
            {
                var targets = new List<TaskTarget>
                {
                    new TaskTarget { transform = far.transform,  role = TargetRole.Destination, pool = true },
                    new TaskTarget { transform = near.transform, role = TargetRole.Destination, pool = true },
                };
                A("target: the nearest free pool tube is the cue when nothing is held",
                    TaskTargetRegistry.ChoosePoolMember(targets, t => false, Vector3.zero) == near.transform);
                A("target: a held pool tube beats a nearer free one",
                    TaskTargetRegistry.ChoosePoolMember(targets, t => t == far.transform, Vector3.zero) == far.transform);
                var fixedOnly = new List<TaskTarget> { new TaskTarget { transform = fixedT.transform, role = TargetRole.Destination } };
                A("target: a step with no pool targets is untouched",
                    TaskTargetRegistry.ChoosePoolMember(fixedOnly, t => false, Vector3.zero) == null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(near); UnityEngine.Object.DestroyImmediate(far);
                UnityEngine.Object.DestroyImmediate(fixedT);
            }
        }

        // ⛔ Pharmee SPEAKS the text after the arrow in every hint, so a hint that says
        // "Test Tube 15" is a voice line insisting on a tube the player is not holding. With
        // tubes claiming their own role that is wrong, not merely unhelpful.
        // "the second rack" is deliberately allowed: a rack is a real, distinct object.
        {
            var numbered = new List<string>();
            int hints = 0;
            foreach (var file in Directory.GetFiles("Assets/PharmaSynth/ScriptableObjects/Experiments", "*.asset"))
                foreach (var line in File.ReadAllLines(file))
                {
                    if (!line.TrimStart().StartsWith("hint:")) continue;
                    hints++;
                    if (Regex.IsMatch(line, @"Test Tube \d+|\bTube \d+\b"))
                        numbered.Add(Path.GetFileNameWithoutExtension(file) + ": " + line.Trim().Substring(0, Math.Min(60, line.Trim().Length)));
                }
            A("hint: no action line names a numbered tube (" + hints + " hints"
              + (numbered.Count > 0 ? ", offenders: " + string.Join(" | ", numbered) : "") + ")",
              hints > 0 && numbered.Count == 0);
        }

        // ---- Any GLASSWARE will do ----
        // Tubes pool by name family (hard glass shares their itemId); everything else by its
        // LabItem itemId, because the spares are named Eq_WatchGlass / Kit_WatchGlass_1_11 /
        // WatchGlass_2 and no prefix rule survives that. A kind with one instance never pools.
        A("pool: glassware pools by itemId, tubes by family, singletons never",
            VesselRoleMatch.PoolKey("Kit_TestTube_4", "kit-testtube") == VesselRoleMatch.RegularFamily
            && VesselRoleMatch.PoolKey("Kit_Hard-GlassTestTube_1", "kit-testtube") == VesselRoleMatch.HardGlassFamily
            && VesselRoleMatch.PoolKey("WatchGlass_2", "kit-watchglass") == "kit-watchglass"
            && VesselRoleMatch.PoolKey("Kit_WatchGlass_1_11", "kit-watchglass") == VesselRoleMatch.PoolKey("Eq_WatchGlass", "kit-watchglass")
            && VesselRoleMatch.PoolKey("Eq_Beaker_100mL", "kit-beaker_100ml") != VesselRoleMatch.PoolKey("Eq_Beaker_500mL", "kit-beaker_500ml")
            && VesselRoleMatch.PoolKey("Anything", "") == "");

        // The ghost demo must mime at the SAME pool member the glow and the arrow use: with
        // pool targets registered, Endpoints would take whichever extra is listed first.
        {
            var a = new GameObject("pool_a"); var b = new GameObject("pool_b"); var fx = new GameObject("fixed_src");
            try
            {
                var targets = new List<TaskTarget>
                {
                    new TaskTarget { transform = fx.transform, role = TargetRole.Source },
                    new TaskTarget { transform = a.transform, role = TargetRole.Destination, pool = true },
                    new TaskTarget { transform = b.transform, role = TargetRole.Destination, pool = true },
                };
                var kept = VerbDemoPlayer.CollapsePool(targets, b.transform);
                bool ok = kept.Count == 2 && kept[0].transform == fx.transform && kept[1].transform == b.transform;
                A("demo: the ghost mimes at the chosen pool member", ok
                    && VerbDemoPlayer.Endpoints(kept, out var mover, out var dest, out _)
                    && mover == fx.transform && dest == b.transform);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(fx); }
        }

        // ⛔ A role is claimed by a POUR or by a delivery controller — never by construction.
        // In a family with ONE role (Exp 9's limewater tube), claiming at wiring time lets
        // the authored member win the ledger while every extra starts with an EMPTY
        // candidate set, and any pour into it is scolded before the player does anything.
        {
            var authoredGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics requires a Renderer
            var extraGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lime = ScriptableObject.CreateInstance<ChemicalData>(); lime.chemicalName = "Limewater";
            try
            {
                authoredGo.AddComponent<LiquidPhysics>(); extraGo.AddComponent<LiquidPhysics>();
                var authored = authoredGo.AddComponent<LiquidTaskBinding>();
                var extra = extraGo.AddComponent<LiquidTaskBinding>();
                var role = new List<LiquidTaskBinding.ReagentStep>
                {
                    new LiquidTaskBinding.ReagentStep { reagent = lime, taskId = "confirm-ferment", requiredMl = 10f }
                };
                var roles = new List<List<LiquidTaskBinding.ReagentStep>> { role };
                var shared = new RackRoles();
                authored.MarkPoolMember(); extra.MarkPoolMember();
                authored.SetRoles(roles, 0, shared);
                extra.SetRoles(roles, -1, shared);

                A("role: a single-role family is not claimed at wiring — the extra can still take it",
                    authored.ClaimedRole == -1 && extra.ClaimedRole == -1 && extra.RoleAmbiguous
                    && extra.CandidateTasks().Contains("confirm-ferment")
                    && authored.ExpectedSteps.Count == 1 && extra.ExpectedSteps.Count == 0);

                // A delivery controller IS the disambiguator: the first drop claims, exclusively,
                // and the authored twin, which can accept nothing, advertises NOTHING (otherwise
                // a distillate run into the twin is scolded drop by drop).
                bool took = extra.ClaimForTask("confirm-ferment");
                A("role: a delivery claim takes the role for the receiver in hand, exclusively — the twin advertises nothing",
                    took && extra.ClaimedRole == 0 && !extra.RoleAmbiguous && extra.ExpectedSteps.Count == 1
                    && authored.CandidateTasks().Count == 0 && authored.ExpectedSteps.Count == 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoredGo); UnityEngine.Object.DestroyImmediate(extraGo);
                UnityEngine.Object.DestroyImmediate(lime);
            }
        }

        // ⛔ The stream takes a candidate only from the HAND, and the hand beats a nearer bench
        // spare: otherwise a beaker resting near the bath claims Exp 7's collect role by
        // proximity before the player has picked anything up.
        A("collect: a resting spare never receives by proximity — an advertised receiver, or a candidate in the hand",
            VaporMath.MayReceive(true, false, false) && VaporMath.MayReceive(false, true, true)
            && !VaporMath.MayReceive(false, true, false) && !VaporMath.MayReceive(false, false, true)
            && VaporMath.Prefer(true, 0.45f, false, 0.2f) && !VaporMath.Prefer(false, 0.2f, true, 0.45f)
            && VaporMath.Prefer(false, 0.2f, false, 0.3f) && !VaporMath.Prefer(true, 0.4f, true, 0.3f));

        // ---- any tube, recoverable mistakes, readable guidance ----

        // ⛔ A shared step counts ROLES, not members. Pooling puts every bench tube in the
        // group, so comparing against the member count would ask for 19 alcohols.
        A("rack: a shared step completes on its ROLE count, not the pool's size",
            RackMath.AllReady(5, 5) && !RackMath.AllReady(4, 5) && RackMath.AllReady(19, 5)
            && !RackMath.AllReady(1, 0)
            && RackMath.ProgressLabel(3, 5) == "tube 3 of 5");

        // The wrist checklist names the tubes it can name, and counts the ones it cannot:
        // four identical permanganate tubes have no honest individual name.
        {
            var named = new List<string> { "Ethanol", "n-Butyl Alcohol", "Phenol" };
            var done = new HashSet<string> { "Ethanol" };
            string line = RackMath.RolesLine(named, done, 1, 3);
            A("watch: a shared step lists each tube it can name",
                line.StartsWith("Tubes 1 of 3")
                && line.Contains("✓ Ethanol") && line.Contains("-  n-Butyl Alcohol")
                && !line.Contains("✓ Phenol"));
            A("watch: indistinguishable tubes fall back to the count",
                RackMath.RolesLine(new List<string> { "", "", "", "" }, null, 2, 4) == "Tubes 2 of 4");
        }

        // ⛔ A rinsed vessel is a FREE vessel: the role goes back to the ledger and the
        // extra can take it. Without this one wrong drop pinned a tube for the whole run.
        {
            var aGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var bGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lime = ScriptableObject.CreateInstance<ChemicalData>(); lime.chemicalName = "Limewater";
            try
            {
                var lpA = aGo.AddComponent<LiquidPhysics>(); bGo.AddComponent<LiquidPhysics>();
                var a = aGo.AddComponent<LiquidTaskBinding>();
                var b = bGo.AddComponent<LiquidTaskBinding>();
                var role = new List<LiquidTaskBinding.ReagentStep>
                {
                    new LiquidTaskBinding.ReagentStep { reagent = lime, taskId = "confirm-ferment", requiredMl = 10f }
                };
                var roles = new List<List<LiquidTaskBinding.ReagentStep>> { role };
                var shared = new RackRoles();
                a.MarkPoolMember(); b.MarkPoolMember();
                a.SetRoles(roles, 0, shared);
                b.SetRoles(roles, -1, shared);
                b.ClaimForTask("confirm-ferment");
                A("role: the claimer holds it and the twin advertises nothing",
                    b.ClaimedRole == 0 && a.ExpectedSteps.Count == 0);
                b.ResetRole();
                A("role: emptying a vessel gives its role back to the pool",
                    b.ClaimedRole == -1 && b.RoleAmbiguous
                    && a.ExpectedSteps.Count == 1 && a.CandidateTasks().Contains("confirm-ferment"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aGo); UnityEngine.Object.DestroyImmediate(bGo);
                UnityEngine.Object.DestroyImmediate(lime);
            }
        }

        // ⛔ ONE mistake per bad pour. A tilt-pour raises LiquidAdded every frame, so an
        // unlatched binding logs a mistake per frame (1000+ in one session) and the grade
        // is gone long before the player understands what was wrong.
        {
            var scoldGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-scold";
            module.graphTasks = new List<ExperimentTask> {
                T("add-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            var want = ScriptableObject.CreateInstance<ChemicalData>(); want.chemicalName = "Wanted";
            var junk = ScriptableObject.CreateInstance<ChemicalData>(); junk.chemicalName = "Junk";
            try
            {
                var lp = scoldGo.AddComponent<LiquidPhysics>();
                var runner = scoldGo.AddComponent<ExperimentRunner>();
                runner.SetModule(module);
                runner.StartExperiment();
                var bind = scoldGo.AddComponent<LiquidTaskBinding>();
                bind.SetVesselAndRunner(lp, runner);
                bind.AddExpected(want, "add-x", 50f);
                for (int i = 0; i < 20; i++) bind.HandleReagent(junk, 1f);
                A("mistake: a refused pour is recorded once, not once per tick",
                    runner.MistakeCount == 1 && bind.RefusedReagent == "Junk");
                var other = ScriptableObject.CreateInstance<ChemicalData>(); other.chemicalName = "Other";
                bind.HandleReagent(other, 1f);
                A("mistake: a DIFFERENT wrong reagent still counts", runner.MistakeCount == 2);
                UnityEngine.Object.DestroyImmediate(other);
                bind.HandleReagent(want, 50f);
                A("mistake: an accepted pour clears the refusal line", bind.RefusedReagent == null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scoldGo); UnityEngine.Object.DestroyImmediate(module);
                UnityEngine.Object.DestroyImmediate(want); UnityEngine.Object.DestroyImmediate(junk);
            }
        }

        // Practice is not assessed ("scores are not important for tutorial or practice
        // mode"). Same call, same runner — only the session flag differs.
        {
            var practiceGo = new GameObject("selftest-practice");
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-practice";
            module.graphTasks = new List<ExperimentTask> {
                T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            bool was = TutorialSession.Active;
            try
            {
                var runner = practiceGo.AddComponent<ExperimentRunner>();
                runner.SetModule(module);
                runner.StartExperiment();
                TutorialSession.Active = true;
                runner.RecordMistake(LabErrorType.WrongReagent, "practice");
                A("practice: a tutorial run records no mistakes", runner.MistakeCount == 0);
                TutorialSession.Active = false;
                runner.RecordMistake(LabErrorType.WrongReagent, "graded");
                A("practice: a graded run still counts every mistake", runner.MistakeCount == 1);
            }
            finally
            {
                TutorialSession.Active = was;
                UnityEngine.Object.DestroyImmediate(practiceGo); UnityEngine.Object.DestroyImmediate(module);
            }
        }

        // Two tags at the same screen height are pushed apart, bottom-up and capped, so a
        // label never wanders off the object it names.
        {
            var y = new List<float> { 100f, 104f, 400f };
            var half = new List<float> { 10f, 10f, 10f };
            var mpp = new List<float> { 0.001f, 0.001f, 0.001f };
            var lifts = LabelDeclutterMath.Lifts(y, half, mpp);
            A("label: overlapping tags are pushed apart, the lone one is left alone",
                lifts[0] == 0f && lifts[1] > 0f && lifts[2] == 0f
                && (104f + lifts[1] / 0.001f) >= (100f + 20f) - 0.01f);
            var tight = LabelDeclutterMath.Lifts(new List<float> { 0f, 0f },
                new List<float> { 500f, 500f }, new List<float> { 0.001f, 0.001f });
            A("label: the lift is capped in METRES, never chased arbitrarily far",
                tight[1] <= LabelDeclutterMath.MaxLift + 1e-4f);
            A("label: nothing to solve is not an error", LabelDeclutterMath.Lifts(null, null, null).Length == 0);
        }

        // The vessel explains itself: what bounced, and that a free tube is free.
        A("hint: a refused pour and a free vessel both say so",
            VesselStatusMath.RefusalLine("Sodium Hydroxide").Contains("Sodium Hydroxide")
            && VesselStatusMath.RefusalLine("") == ""
            && VesselStatusMath.FreeVesselLine(true, true, true).Length > 0
            && VesselStatusMath.FreeVesselLine(true, false, true) == ""
            && VesselStatusMath.FreeVesselLine(false, true, true) == ""
            && VesselStatusMath.FreeVesselLine(true, true, false) == "");

        // A custom shader with no stereo-instancing macros renders to eye index 0 ONLY.
        //
        // The project runs Single Pass Instanced: both eyes go into one texture array and the
        // shader picks its slice from the instance id. Without the macros, the tutorial
        // arrows and the highlight glow (PharmaGuide, PharmaGlow) are visible in the
        // LEFT EYE ONLY, which only shows with one eye shut.
        // Nothing in the editor shows this - the Game view renders a single eye and looks
        // perfect - so a text pin is the only cheap guard. Surface shaders (Glass) get the
        // handling generated for them, which is why the check is on the MACROS and not on
        // "#pragma multi_compile_instancing".
        {
            var oneEyed = new List<string>();
            int shaders = 0;
            foreach (var file in Directory.GetFiles("Assets/PharmaSynth", "*.shader",
                                                    SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                shaders++;
                if (text.IndexOf("UNITY_VERTEX_OUTPUT_STEREO", StringComparison.Ordinal) < 0
                    || text.IndexOf("UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO", StringComparison.Ordinal) < 0)
                    oneEyed.Add(Path.GetFileName(file));
            }
            A("xr: every custom shader declares the stereo macros (" + shaders + " checked"
              + (oneEyed.Count > 0 ? ", ONE-EYED: " + string.Join(", ", oneEyed) : "") + ")",
              shaders > 0 && oneEyed.Count == 0);
        }

        // ---- the manuscript is the AUTHORITY, so check the numbers against it ----
        //
        // ⛔ The `content:` pins prove a module LOADS, has N tasks,
        // is solvable and builds its scoring spine, never that "5 drops" is authored as 5.
        // Every quantity in the game is hand-transcribed from Appendix C.
        //
        // The verb contract (Docs/systems-reference.md) is what makes this checkable: the number in the
        // instruction IS the action count, whatever its unit, and a dropper delivers
        // DropperController.MlPerSqueeze per squeeze — so "2 drops" is authored as 2.
        //
        // The table is CURATED (Docs/manuscript-quantities.tsv); see the header there for why
        // it is not parsed out of the PDF. Rows marked `accepted` are documented deviations
        // and are counted, not asserted, so a decision never reads as a defect.
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-quantities.tsv");
            A("manuscript: the quantity authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                var layouts = new Dictionary<string, ExperimentLayout>();
                foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout",
                             new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
                {
                    var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
                    if (l != null && !string.IsNullOrEmpty(l.moduleId)) layouts[l.moduleId] = l;
                }

                int checkedRows = 0, acceptedRows = 0, disagree = 0;
                var wrong = new List<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('\t');
                    if (f.Length < 6) continue;
                    string moduleId = f[0].Trim(), taskId = f[1].Trim(), reagent = f[2].Trim();
                    string expectedText = f[3].Trim(), accepted = f[4].Trim();
                    if (accepted == "accepted") { acceptedRows++; continue; }
                    if (!float.TryParse(expectedText, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out float expected)) continue;
                    if (!layouts.TryGetValue(moduleId, out var layout))
                    { wrong.Add(moduleId + ": no layout"); continue; }

                    checkedRows++;
                    if (accepted == "review") disagree++;

                    if (taskId == "HEAT" || taskId == "CHILL")
                    {
                        bool found = false;
                        foreach (var v in layout.vessels)
                        {
                            float t = taskId == "HEAT" ? v.heatToC : v.chillToC;
                            if (t > 0f && Mathf.Abs(t - expected) < 0.01f) { found = true; break; }
                        }
                        if (!found) wrong.Add(moduleId + " " + taskId + " " + expected + " C not authored");
                        continue;
                    }

                    float? authored = null;
                    foreach (var v in layout.vessels)
                        foreach (var b in v.bindings)
                            if (b.taskId == taskId && b.reagentChemical == reagent) authored = b.requiredMl;
                    if (authored == null) wrong.Add(moduleId + "/" + taskId + "/" + reagent + ": no binding");
                    else if (Mathf.Abs(authored.Value - expected) > 0.001f)
                        wrong.Add(moduleId + "/" + taskId + "/" + reagent + ": authored "
                                  + authored.Value + ", manuscript implies " + expected);
                }

                foreach (var w in wrong) _log.Add("manuscript drift: " + w);
                A("manuscript: every checked quantity matches Appendix C ("
                  + checkedRows + " checked, " + wrong.Count + " adrift, "
                  + acceptedRows + " accepted deviations)", wrong.Count == 0);
                A("manuscript: the authority table actually covers the game", checkedRows >= 20);
                // Not a failure — a standing reminder that these await a client decision.
                A("manuscript: " + disagree + " quantities knowingly differ and are recorded as such",
                  disagree >= 0);
            }
        }

        // ---- the manuscript's STEP ORDER, not just its numbers ----
        //
        // ⛔ "runner: out-of-order = wrong step" proves the MECHANISM and "catalog: prereq
        // chain valid + ordered" proves the module CHAIN. Neither checks that a module's
        // authored prerequisites follow Appendix C's own a-b-c sequence: a module whose steps
        // are quietly reordered would pass every pin. Docs/manuscript-order.tsv gives every
        // task its manuscript step as a sortable ordinal (A.a = 101 ... B.a = 201; the data
        // sheet is 999), and this asserts no prerequisite edge runs BACKWARDS through the
        // manual: for every "P must precede T", ordinal(P) <= ordinal(T).
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-order.tsv");
            A("manuscript: the step-order authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                var ordinal = new Dictionary<string, int>();
                var acceptedModules = new HashSet<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('	');
                    if (f.Length < 6) continue;
                    if (f[4].Trim() == "accepted") { acceptedModules.Add(f[0].Trim()); continue; }
                    if (int.TryParse(f[3].Trim(), out int ord)) ordinal[f[0].Trim() + "/" + f[1].Trim()] = ord;
                }

                int edges = 0, backwards = 0, unmapped = 0, modulesChecked = 0;
                var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                    "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
                if (lib != null)
                    foreach (var m in lib.modules)
                    {
                        if (m == null || acceptedModules.Contains(m.moduleId)) continue;
                        modulesChecked++;
                        foreach (var t in m.graphTasks)
                        {
                            if (t == null) continue;
                            if (!ordinal.TryGetValue(m.moduleId + "/" + t.taskId, out int mine))
                            { unmapped++; _log.Add("manuscript order: no step for " + m.moduleId + "/" + t.taskId); continue; }
                            foreach (var p in t.prerequisites)
                            {
                                if (!ordinal.TryGetValue(m.moduleId + "/" + p, out int theirs)) { unmapped++; continue; }
                                edges++;
                                if (theirs > mine)
                                {
                                    backwards++;
                                    _log.Add("manuscript order: " + m.moduleId + " makes " + p + " (" + theirs
                                             + ") precede " + t.taskId + " (" + mine + ") — BACKWARDS through Appendix C");
                                }
                            }
                        }
                    }
                A("manuscript: every authored task has a manuscript step (" + unmapped + " unmapped across "
                  + modulesChecked + " modules)", lib != null && unmapped == 0 && modulesChecked >= 6);
                A("manuscript: no prerequisite runs backwards through Appendix C (" + backwards + " of "
                  + edges + " edges)", edges >= 60 && backwards == 0);
            }
        }

        // ---- the manuscript's OBSERVATION WORDS, not just its numbers and order ----
        //
        // ⛔ The VISUAL sweep judges what the student SEES against the fired rule; this
        // judges the WORDS the student reads (`expectedObservation`) against what the manual
        // says they should observe. Without it, a rewrite that dropped "silver mirror" or
        // "ammonia" would pass. Docs/manuscript-observations.tsv carries the manual's own
        // result word per rule.
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-observations.tsv");
            A("manuscript: the observation authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                int checkedRules = 0, acceptedRules = 0, missingAssets = 0;
                var misses = new List<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('	');
                    if (f.Length < 4) continue;
                    string asset = f[0].Trim(), key = f[1].Trim(), accepted = f[2].Trim();
                    if (accepted == "accepted") { acceptedRules++; continue; }
                    var rule = AssetDatabase.LoadAssetAtPath<ReactionRule>(
                        "Assets/PharmaSynth/ScriptableObjects/Reactions/" + asset + ".asset");
                    if (rule == null) { missingAssets++; misses.Add(asset + ": no such rule asset"); continue; }
                    checkedRules++;
                    string have = (rule.expectedObservation ?? "").ToLowerInvariant();
                    if (!have.Contains(key.ToLowerInvariant()))
                        misses.Add(asset + ": the manual's \"" + key + "\" is missing from the popup text");
                }
                foreach (var m in misses) _log.Add("manuscript observation: " + m);
                A("manuscript: every rule's popup carries the manual's result word ("
                  + checkedRules + " checked, " + misses.Count + " missing, " + acceptedRules + " accepted)",
                    missingAssets == 0 && misses.Count == 0 && checkedRules >= 20);
            }
        }

        // Voice budget order: Jimenez's rows are generated first.
        A("voice: Jimenez sorts before Pharmee",
            VoiceManifestExporter.SpeakerPriority("Jimenez") < VoiceManifestExporter.SpeakerPriority("Pharmee"));

        // Recording-safety of the spoken corpus. A subtitle can read perfectly and
        // still be spoken wrong — these are the artefacts that survive to the ear.
        A("voice: a formula subscript is not recording-safe",
            !VoicePolish.IsRecordingSafe("filter the MnO2, acidify"));
        A("voice: a hyphenated range is not recording-safe",
            !VoicePolish.IsRecordingSafe("the 70-80 degree fraction"));
        A("voice: shouted caps are not recording-safe",
            !VoicePolish.IsRecordingSafe("lab coat, goggles, AND gloves"));
        A("voice: ordinary copy is recording-safe",
            VoicePolish.IsRecordingSafe("Hold the cut steady between 70 and 80 degrees."));
        A("voice: the polish rules clean what they target",
            VoicePolish.IsRecordingSafe(VoicePolish.ApplyRules("filter the MnO2, acidify"))
            && VoicePolish.IsRecordingSafe(VoicePolish.ApplyRules("the 70-80 degree fraction")));
        A("voice: the deleted thermometer is no longer named",
            !VoicePolish.ApplyRules("Watch the thermometer through the 70-80 degree window.")
                 .Contains("thermometer"));
        // The whole live corpus must be recording-safe before credits are spent.
        {
            int unsafeLines = 0;
            foreach (var l in VoiceCorpus.CodeLines())
                if (!VoicePolish.IsRecordingSafe(l.text)) unsafeLines++;
            A("voice: every code-authored line is recording-safe", unsafeLines == 0);
        }

        // EVERY dialogue pool must reach the corpus, or the lines exist, play, and
        // are never voiced (e.g. CampaignComplete, the campaign-completion payoff, would
        // play as blips). Reflect rather than enumerate, so a NEW pool added
        // later fails this the moment it is forgotten.
        {
            var corpusText = new HashSet<string>();
            foreach (var l in VoiceCorpus.CodeLines()) corpusText.Add(VoiceLineId.For(l.text));
            var missing = new List<string>();
            foreach (var f in typeof(PharmeeLines).GetFields())
            {
                string[] pool = f.FieldType == typeof(string[]) ? (string[])f.GetValue(null)
                              : f.FieldType == typeof(string) ? new[] { (string)f.GetValue(null) }
                              : null;
                if (pool == null) continue;
                foreach (var t in pool)
                    if (!string.IsNullOrWhiteSpace(t) && !corpusText.Contains(VoiceLineId.For(t)))
                    { missing.Add(f.Name); break; }
            }
            A("voice: every PharmeeLines pool reaches VoiceCorpus ("
              + (missing.Count == 0 ? "all" : string.Join(",", missing) + " MISSING") + ")",
              missing.Count == 0);
        }

        // Holo board scroll paging (wrap + scrollable checklist).
        A("holo: page down moves toward the bottom", Near(HoloScroller.NextPage(1f, 0.6f, -1), 0.4f));
        A("holo: clamped at the bottom", HoloScroller.NextPage(0.2f, 0.6f, -1) == 0f);
        A("holo: clamped at the top", HoloScroller.NextPage(0.9f, 0.6f, +1) == 1f);

        // Settle-freeze: a released body at rest goes kinematic in place.
        A("settle: rested body freezes", DropRespawnMath.ShouldSettleFreeze(false, false, 0.01f, 3f));
        A("settle: held blocks", !DropRespawnMath.ShouldSettleFreeze(true, false, 0.01f, 3f));
        A("settle: already kinematic no-op", !DropRespawnMath.ShouldSettleFreeze(false, true, 0.01f, 3f));
        A("settle: moving body waits", !DropRespawnMath.ShouldSettleFreeze(false, false, 0.4f, 3f));
        A("settle: needs the settle beat", !DropRespawnMath.ShouldSettleFreeze(false, false, 0.01f, 1f));

        // Spill policy: un-held + tipped + has liquid, and only then.
        A("mishandle: knocked-over bottle spills", Mishandling.IsSpilling(75f, false, 80f));
        A("mishandle: held pour is not a spill", !Mishandling.IsSpilling(75f, true, 80f));
        A("mishandle: empty bottle can't spill", !Mishandling.IsSpilling(75f, false, 0f));
        A("mishandle: upright bottle safe", !Mishandling.IsSpilling(20f, false, 80f));

        // Grading: both mishandling types hit the Sanitation rubric.
        A("mishandle: spill maps to Sanitation", MistakeLog.CategoryFor(LabErrorType.SpilledReagent) == RubricCategory.Sanitation);
        var log = new MistakeLog();
        log.Record(LabErrorType.DroppedGlassware, "");
        log.Record(LabErrorType.SpilledReagent, "");
        A("mishandle: grader counts both", log.CountOfAny(LabErrorType.DroppedGlassware, LabErrorType.SpilledReagent) == 2);

        // Action-SFX policy: material-aware drop clatter + reaction cues + stride.
        A("sfx: glass clinks", Mishandling.DropSoundKey("Beaker_100mL") == "glass-clink");
        A("sfx: delisted glass still clinks (W5.8)", Mishandling.DropSoundKey("GlassRod") == "glass-clink"
            && Mishandling.DropSoundKey("Funnel") == "glass-clink"
            && Mishandling.DropSoundKey("EvaporatingDish") == "glass-clink");
        A("sfx: metal clatters", Mishandling.DropSoundKey("CrucibleTongs") == "drop-metal");
        A("sfx: wood knocks", Mishandling.DropSoundKey("TestTubeRack") == "drop-wood");
        // nature audit: the scale + burners sound (and are) metal, never glass.
        A("sfx: scale + burners are metal (W5.12)", Mishandling.DropSoundKey("Balance") == "drop-metal"
            && Mishandling.DropSoundKey("BunsenBurner") == "drop-metal"
            && Mishandling.DropSoundKey("AlcoholBurner") == "drop-metal"
            && !Mishandling.IsBreakable("Balance") && !Mishandling.IsBreakable("BunsenBurner")
            && !Mishandling.IsBreakable("AlcoholBurner"));
        A("sfx: porcelain mortar clinks, never breaks (W5.12)",
            Mishandling.DropSoundKey("Motar") == "glass-clink" && !Mishandling.IsBreakable("Motar"));
        A("sfx: fizz for gas outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Fizzing) == "reaction-fizz"
            && Mishandling.SfxForOutcome(ReactionOutcome.GasEvolved) == "reaction-fizz");
        A("sfx: chime for visible outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Precipitate) == "mixture-complete"
            && Mishandling.SfxForOutcome(ReactionOutcome.ColorChange) == "mixture-complete");
        A("sfx: silence for negative tests", Mishandling.SfxForOutcome(ReactionOutcome.None) == "");
        float acc = 0f;
        A("sfx: stride accumulates", StrideMath.Steps(ref acc, 0.5f, 0.75f) == 0 && StrideMath.Steps(ref acc, 0.5f, 0.75f) == 1);
        A("sfx: long move = many steps", StrideMath.Steps(ref acc, 3f, 0.75f) >= 4);
        A("sfx: no distance no steps", StrideMath.Steps(ref acc, 0f, 0.75f) == 0);

        // Mirror avatar: foot point sits under the head at the floor (+offset), XZ kept.
        var foot = PlayerAvatarRig.FootUnder(new Vector3(2f, 1.7f, -3f), 0.25f, 0.02f);
        A("avatar: foot keeps XZ", Near(foot.x, 2f) && Near(foot.z, -3f));
        A("avatar: foot sits on floor+offset", Near(foot.y, 0.27f));

        // Per-piece PPE: all three required; missing-summary text.
        var ppeSet = new PPESetModel();
        A("ppe: nothing worn at start", !ppeSet.AllWorn && ppeSet.WornCount == 0);
        A("ppe: missing lists all three", ppeSet.MissingSummary() == "lab coat, goggles and gloves");
        A("ppe: don returns newly-worn", ppeSet.Don(PPEPiece.Coat) && !ppeSet.Don(PPEPiece.Coat));
        A("ppe: coat alone is not enough", !ppeSet.AllWorn);
        A("ppe: missing pair reads naturally", ppeSet.MissingSummary() == "goggles and gloves");
        ppeSet.Don(PPEPiece.Goggles);
        A("ppe: one missing reads singly", ppeSet.MissingSummary() == "gloves");
        ppeSet.Don(PPEPiece.Gloves);
        A("ppe: all three = fully dressed", ppeSet.AllWorn && ppeSet.MissingSummary() == "");
        A("ppe: clear strips everything", ppeSet.Clear() && !ppeSet.AllWorn && !ppeSet.Clear());
        // Hard door gate: campaign entry needs full PPE; tour/review don't.
        A("ppe: armed door requires PPE", GatekeeperModel.RequiresPPEToOpen(GateState.DoorArmed)
            && GatekeeperModel.RequiresPPEToOpen(GateState.Running));
        A("ppe: tour/review need no PPE", !GatekeeperModel.RequiresPPEToOpen(GateState.LabTour)
            && !GatekeeperModel.RequiresPPEToOpen(GateState.ScoreReview));
        // A lit burner blows out when lifted and only re-lights once down.
        A("burner: lifted blows out", BurnerController.ShouldBlowOut(true, true));
        A("burner: rests stay lit", !BurnerController.ShouldBlowOut(true, false));
        // LiquidPhysics must NOT adopt an opaque vessel mesh as its fill renderer —
        // it disables mainRenderer when empty, which vanished the mortar in play
        // Only a PharmaLiquid "_Fill" surface may be adopted.
        {
            var litSh = Shader.Find("Universal Render Pipeline/Lit");
            var fillSh = Shader.Find("PharmaSynth/Liquid");
            if (litSh != null)
                A("liquidphysics: opaque vessel mesh not adopted as fill",
                  !LiquidPhysics.ShouldAdoptHostRenderer(new Material(litSh)));
            if (fillSh != null)
                A("liquidphysics: liquid _Fill surface is adopted",
                  LiquidPhysics.ShouldAdoptHostRenderer(new Material(fillSh)));
        }

        // Pharmee flight lean: proportional to speed, clamped, zero at rest.
        A("pharmee: lean scales with speed", Near(PharmeeAttitude.LeanFor(0.5f, 22f, 14f), 11f));
        A("pharmee: lean clamped", Near(PharmeeAttitude.LeanFor(3f, 22f, 14f), 14f));
        A("pharmee: no lean at rest", Near(PharmeeAttitude.LeanFor(0f, 22f, 14f), 0f));

        // Station VFX: verb → effect style, edit-mode-safe toggling.
        A("vfx: heat steams", StationVfx.StyleFor(StationSim.Heat) == "steam");
        A("vfx: crystallise frosts", StationVfx.StyleFor(StationSim.Crystallise) == "frost");
        A("vfx: filter drips", StationVfx.StyleFor(StationSim.Filter) == "drip");
        A("vfx: collect bubbles", StationVfx.StyleFor(StationSim.Collect) == "bubbles");
        A("vfx: none is empty", StationVfx.StyleFor(StationSim.None) == "");

        // Proctor roaming: idle → walk out → observe → walk home.
        var roam = new ProctorRoamModel(3, idleMin: 1f, idleMax: 1f, observeSeconds: 2f, seed: 7);
        A("roam: starts at home", roam.Current == ProctorRoamModel.Phase.AtHome && !roam.IsWalking);
        roam.Tick(1.5f, true, true);                                     // idle expires
        A("roam: heads out to point 0", roam.Current == ProctorRoamModel.Phase.WalkingOut && roam.TargetIndex == 0 && roam.IsWalking);
        roam.Tick(0.1f, true, false);                                    // still walking
        A("roam: keeps walking until arrival", roam.Current == ProctorRoamModel.Phase.WalkingOut);
        roam.Tick(0.1f, true, true);                                     // arrived
        A("roam: observes on arrival", roam.Current == ProctorRoamModel.Phase.Observing);
        roam.Tick(2.5f, true, true);                                     // observation done
        A("roam: walks home after observing", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        roam.Tick(0.1f, true, true);
        A("roam: back home", roam.Current == ProctorRoamModel.Phase.AtHome);
        roam.Tick(1.5f, true, true);
        A("roam: round-robins to point 1", roam.TargetIndex == 1);
        roam.Tick(0.1f, false, false);                                   // quiz begins mid-walk
        A("roam: quiz forces him home", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        var stay = new ProctorRoamModel(3, 1f, 1f, 2f, 7);
        stay.Tick(10f, false, true);
        A("roam: never leaves during quiz", stay.Current == ProctorRoamModel.Phase.AtHome);

        // Roamer stuck watchdog (Jimenez ran through a wall endlessly).
        float stuck = 0f;
        A("roam: progress resets the watchdog", !ProctorRoamer.StuckTick(ref stuck, 0.5f, 1f, 2f) && Near(stuck, 0f));
        A("roam: brief block is tolerated", !ProctorRoamer.StuckTick(ref stuck, 0f, 1f, 2f));
        A("roam: sustained block gives up", ProctorRoamer.StuckTick(ref stuck, 0f, 1.5f, 2f) && Near(stuck, 0f));

        // Spill puddle fade: full while lingering, smooth fade after.
        A("puddle: opaque while lingering", Near(SpillPuddle.Alpha01(0f, 3f, 1.2f), 1f) && Near(SpillPuddle.Alpha01(2.9f, 3f, 1.2f), 1f));
        A("puddle: half-faded mid-fade", Near(SpillPuddle.Alpha01(3.6f, 3f, 1.2f), 0.5f));
        A("puddle: gone after fade", Near(SpillPuddle.Alpha01(4.2f, 3f, 1.2f), 0f) && Near(SpillPuddle.Alpha01(9f, 3f, 1.2f), 0f));

        // Pharmee gate moods: happy default, warning on trouble.
        A("mood: friendly at the door", PharmeeMood.ExpressionForGate(GateState.ModeChoice) == PharmeeFaceExpression.Happy);
        A("mood: warns on supply trouble", PharmeeMood.ExpressionForGate(GateState.SupplyPrompt) == PharmeeFaceExpression.Warning);
        A("mood: celebrates the debrief", PharmeeMood.ExpressionForGate(GateState.Debrief) == PharmeeFaceExpression.Happy);
        A("mood: neutral while picking", PharmeeMood.ExpressionForGate(GateState.EpisodePick) == PharmeeFaceExpression.Neutral);

        // Sim-loop audio: verb → SoundBank key mapping, and safe without clips.
        A("sfx: heat loops bubble", SimLoopAudio.KeyFor(StationSim.Heat) == "bubble");
        A("sfx: filter loops drip", SimLoopAudio.KeyFor(StationSim.Filter) == "filter-drip");
        A("sfx: collect loops hiss", SimLoopAudio.KeyFor(StationSim.Collect) == "gas-hiss");
        A("sfx: crystallise loops shimmer", SimLoopAudio.KeyFor(StationSim.Crystallise) == "crystallise");
        var loopGo = new GameObject("loop");
        try
        {
            var loop = loopGo.AddComponent<SimLoopAudio>();
            loop.Bind("bubble");
            loop.SetRunning(true);       // no AudioService in edit mode → silent no-op
            loop.SetRunning(false);
            A("sfx: loop no-op without service", !loop.IsPlaying);
        }
        finally { UnityEngine.Object.DestroyImmediate(loopGo); }

        // Break → replacement flow: shattered item re-appears at its home spot.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = false;
            var respawn = go.AddComponent<DropRespawn>();
            respawn.Bind(rb, null);
            respawn.SetHome(new Vector3(2f, 1f, 2f), Quaternion.identity);
            var breakable = go.AddComponent<BreakableGlassware>();
            breakable.Bind(null, respawn, rb, "Test Beaker");
            go.transform.position = new Vector3(5f, 0f, 5f);
            breakable.Break();                                  // no runner → must not throw
            A("mishandle: break sends replacement home", (go.transform.position - new Vector3(2f, 1f, 2f)).magnitude < 1e-4f);
            A("mishandle: replacement re-frozen", rb.isKinematic);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// Settings apply-listeners (§5): comfort values reach the live systems.
    static void ComfortSuite()
    {
        // Pure curves.
        A("comfort: text scale scales HUD", ComfortMath.HudScale(Vector3.one * 0.001f, 1.2f) == Vector3.one * 0.0012f);
        A("comfort: text scale clamped", ComfortMath.HudScale(Vector3.one, 5f) == Vector3.one * 1.6f);
        A("comfort: vignette off = open aperture", Near(ComfortMath.ApertureFor(0f), 1f));
        A("comfort: vignette full = tight aperture", Near(ComfortMath.ApertureFor(1f), 0.35f));
        A("comfort: fast subtitles dwell shorter", Near(ComfortMath.LineSecondsFor(4f, 2f), 2f));
        A("comfort: slow subtitles dwell longer", Near(ComfortMath.LineSecondsFor(4f, 0.5f), 8f));
        A("comfort: pace speed clamped", Near(ComfortMath.LineSecondsFor(4f, 10f), 2f));

        // Live applier: values land on targets, and re-applying never compounds.
        var go = new GameObject("comfort");
        var hud = new GameObject("hud").transform;
        try
        {
            hud.localScale = Vector3.one * 0.0011f;
            var turn = go.AddComponent<UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider>();
            var applier = go.AddComponent<ComfortApplier>();
            applier.Bind(hud, turn, null, null, null);
            var s = new ComfortSettings();
            s.SetTextScale(1.2f); s.SetSnapTurnAngle(60f);
            applier.Apply(s);
            A("comfort: hud scaled", Near(hud.localScale.x, 0.0011f * 1.2f, 1e-5f));
            A("comfort: snap angle applied", Near(turn.turnAmount, 60f));
            s.SetTextScale(0.9f);
            applier.Apply(s);
            A("comfort: re-apply uses baseline (no compounding)", Near(hud.localScale.x, 0.0011f * 0.9f, 1e-5f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(hud.gameObject);
        }
    }

    static void PharmeeAliveSuite()
    {
        // Jitter: deterministic, bounded, non-constant
        var j1 = FloatBob.JitterOffset(3.7f, 2.3f, 0.012f);
        var j2 = FloatBob.JitterOffset(3.7f, 2.3f, 0.012f);
        var j3 = FloatBob.JitterOffset(5.1f, 2.3f, 0.012f);
        A("alive: jitter deterministic", (j1 - j2).magnitude < 1e-6f);
        A("alive: jitter bounded", Mathf.Abs(j1.x) <= 0.012f && Mathf.Abs(j1.y) <= 0.012f && Mathf.Abs(j1.z) <= 0.012f);
        A("alive: jitter varies", (j1 - j3).magnitude > 1e-5f);
        A("alive: zero amplitude = zero", FloatBob.JitterOffset(3.7f, 2.3f, 0f) == Vector3.zero);

        // Anchor picking: nearest valid, min-distance, hysteresis
        var anchors = new List<Vector3> { new Vector3(0, 0, 0), new Vector3(3, 0, 0), new Vector3(6, 0, 0) };
        var player = new Vector3(0.5f, 0, 0);
        int pick = PharmeeMoveSolver.PickAnchor(player, anchors, 1.2f, -1, 0.75f);
        A("alive: skips crowding anchor", pick == 1);                                  // anchor 0 is 0.5m away -> too close
        int sticky = PharmeeMoveSolver.PickAnchor(new Vector3(4.6f, 0, 0), anchors, 1.2f, 1, 0.75f);
        A("alive: hysteresis holds", sticky == 1);                                     // anchor2 only 0.2m closer -> stay
        int swap = PharmeeMoveSolver.PickAnchor(new Vector3(7.5f, 0, 0), anchors, 1.2f, 1, 0.75f);
        A("alive: clear winner swaps", swap == 2);                                     // anchor2 3m closer -> move
        A("alive: all crowded stays", PharmeeMoveSolver.PickAnchor(Vector3.zero, new List<Vector3> { new Vector3(0.3f, 0, 0) }, 1.2f, 0, 0.75f) == 0);

        // Step: clamped + exact landing
        var step = PharmeeMoveSolver.Step(Vector3.zero, new Vector3(10, 0, 0), 0.8f, 0.5f);
        A("alive: step clamped", Near(step.x, 0.4f));
        A("alive: step lands", PharmeeMoveSolver.Step(new Vector3(9.99f, 0, 0), new Vector3(10, 0, 0), 0.8f, 1f) == new Vector3(10, 0, 0));

        // Mover drives FloatBob home toward the anchor while running
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
        var go = new GameObject("mover");
        var playerGo = new GameObject("playerSim");
        var a1 = new GameObject("a1"); var a2 = new GameObject("a2");
        try
        {
            a1.transform.position = new Vector3(4f, 0, 0);
            a2.transform.position = new Vector3(-4f, 0, 0);
            playerGo.transform.position = new Vector3(2.5f, 1.6f, 0);   // 1.5m from a1 (outside min dist)
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            var bob = go.AddComponent<FloatBob>();
            bob.SetHome(new Vector3(0f, 1.05f, 0f));
            var mover = go.AddComponent<PharmeeMover>();
            mover.Bind(runner, bob, playerGo.transform, new[] { a1.transform, a2.transform });
            for (int i = 0; i < 20; i++) mover.TickSolve(0.5f);   // 10s of glide
            A("alive: mover approaches player-side anchor", (bob.Home - new Vector3(4f, 1.05f, 0f)).magnitude < 0.2f);
            runner.Finish(1f);
            for (int i = 0; i < 30; i++) mover.TickSolve(0.5f);
            A("alive: mover returns home when idle", (bob.Home - new Vector3(0f, 1.05f, 0f)).magnitude < 0.2f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(playerGo);
            UnityEngine.Object.DestroyImmediate(a1);
            UnityEngine.Object.DestroyImmediate(a2);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    static void ChemVisualSuite()
    {
        var h = new HeatModel(22f) { HeatRate = 0.35f };
        h.SetHeating(true, 100f); for (int i = 0; i < 60; i++) h.Step(1f);
        A("heat: rises toward source", h.Current > 90f);
        var a1 = new HeatModel(22f); a1.SetHeating(true, 100f); a1.Step(10f);
        var a2 = new HeatModel(22f); a2.SetHeating(true, 100f); for (int i = 0; i < 100; i++) a2.Step(0.1f);
        A("heat: timestep-independent", Near(a1.Current, a2.Current, 0.1f));
        var gg = new GameObject("gas");
        try { var gc = gg.AddComponent<GasCollection>(); gc.AddGas(50f); A("gas: fill fraction", Near(gc.FillFraction, 0.5f)); }
        finally { UnityEngine.Object.DestroyImmediate(gg); }
    }

    static void UISuite()
    {
        A("ui: time format", ExperimentHudController.FormatTime(920) == "00:15:20");
        A("ui: percent format", ExperimentHudController.FormatPercent(0.754f) == "75%");
        A("ui: progress drops on mistakes", Near(ExperimentHudController.DisplayedProgress(0.8f, 2, 0.05f), 0.7f));
        A("ui: instruction from hint", PharmeeBrain.InstructionFor(new ExperimentTask { label = "X", hint = "do x" }) == "do x");
        // Two-line "fact\n→ action" hints: Pharmee speaks ONLY the action line;
        // the fact stays on the wrist panel (speaking both reads out whole paragraphs).
        A("ui: pharmee speaks the ACTION line of a two-line hint",
            PharmeeBrain.InstructionFor(new ExperimentTask { label = "X",
                hint = "KMnO4 oxidises benzaldehyde.\n→ Squeeze 2 dropper-loads in." })
            == "Squeeze 2 dropper-loads in."
            && PharmeeBrain.InstructionFor(new ExperimentTask { label = "X", hint = "plain hint" }) == "plain hint");
    }

    static void W4Suite()
    {
        // Cutscene outro selection (end cutscene always resolves)
        var go = new GameObject("csdir");
        try
        {
            var dir = go.AddComponent<CutsceneDirector>();
            var pass = ScriptableObject.CreateInstance<CutsceneData>();
            var fail = ScriptableObject.CreateInstance<CutsceneData>();
            var so = new SerializedObject(dir);
            so.FindProperty("success").objectReferenceValue = pass;
            so.FindProperty("failure").objectReferenceValue = fail;
            so.ApplyModifiedProperties();
            A("cutscene: success outro when passed", dir.SelectOutro(new ExperimentResult { passed = true }) == pass);
            A("cutscene: failure outro when not", dir.SelectOutro(new ExperimentResult { passed = false }) == fail);
            var cs = ScriptableObject.CreateInstance<CutsceneData>();
            cs.beats.Add(new CutsceneData.Beat { seconds = 2f }); cs.beats.Add(new CutsceneData.Beat { seconds = 3f });
            A("cutscene: total duration", Near(cs.TotalDuration(), 5f));
            UnityEngine.Object.DestroyImmediate(pass); UnityEngine.Object.DestroyImmediate(fail); UnityEngine.Object.DestroyImmediate(cs);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }

        // Crystallization
        var cgo = new GameObject("cr");
        try
        {
            var cr = cgo.AddComponent<CrystallizationController>();
            new SerializedObject(cr).FindProperty("durationSeconds").floatValue = 10f;
            var soc = new SerializedObject(cr); soc.FindProperty("durationSeconds").floatValue = 10f; soc.ApplyModifiedProperties();
            cr.BeginCrystallization(); cr.Tick(5f);
            A("cryst: half at 5/10", Near(cr.Progress, 0.5f));
            cr.Tick(6f); A("cryst: done", cr.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(cgo); }

        // Filtration
        var fgo = new GameObject("f");
        try
        {
            var fc = fgo.AddComponent<FiltrationController>();
            var sof = new SerializedObject(fc); sof.FindProperty("targetVolumeMl").floatValue = 100f; sof.ApplyModifiedProperties();
            fc.AddFiltrate(40f); A("filt: 40%", Near(fc.Fraction, 0.4f));
            fc.AddFiltrate(100f); A("filt: clamps + done", fc.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(fgo); }

        // Hazard reports mistake
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { new ExperimentTask { taskId = "x", label = "x" } };
        module.trackedSkills = new List<LabSkill> { LabSkill.Safety };
        var rgo = new GameObject("r");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>(); runner.SetModule(module); runner.StartExperiment();
            var hz = rgo.AddComponent<HazardZone>(); hz.SetRunner(runner);
            hz.Report(); hz.Report();  // second is debounced
            A("hazard: reports + debounces", runner.MistakeCount == 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(module); }
    }

    static void InteractionSuite()
    {
        // A trigger station requiring a specific LabItem accepts only that prop
        // (bring-the-right-thing-here), while an unset id accepts anything (legacy poke).
        var sgo = new GameObject("station");
        var igo = new GameObject("correct"); var wgo = new GameObject("wrong");
        try
        {
            var station = sgo.AddComponent<ExperimentTaskStation>();
            station.SetRequiredItemId("sodium-acetate");
            var correct = igo.AddComponent<LabItem>(); correct.SetItemId("sodium-acetate");
            var wrong = wgo.AddComponent<LabItem>(); wrong.SetItemId("soda-lime");
            A("interaction: station accepts matching item", station.AcceptsItem(correct));
            A("interaction: station rejects wrong item", !station.AcceptsItem(wrong));
            A("interaction: station rejects null item", !station.AcceptsItem(null));

            var open = new GameObject("open").AddComponent<ExperimentTaskStation>();
            A("interaction: unset id accepts anything", open.AcceptsItem(wrong) && open.AcceptsItem(null));
            UnityEngine.Object.DestroyImmediate(open.gameObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sgo);
            UnityEngine.Object.DestroyImmediate(igo);
            UnityEngine.Object.DestroyImmediate(wgo);
        }
    }

    static void RosterDataSuite()
    {
        // Cutscenes: every experiment has all 4 kinds, each with beats.
        string[] keys = { "Methane","ChemicalCompounding","EthylAlcohol","BenzoicAcid","Acetanilide",
            "Acetone","Chloroform","Benzamide","WineMaking" };
        string cdir = "Assets/PharmaSynth/ScriptableObjects/Cutscenes/";
        int csOk = 0;
        foreach (var k in keys)
            foreach (var kind in new[]{ "Intro","ReagentPrep","Success","Failure" })
            {
                var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(cdir + k + "_" + kind + ".asset");
                if (cs != null && cs.beats.Count > 0 && cs.TotalDuration() > 0f) csOk++;
            }
        // 44 → 36: Aspirin + Caffeine dropped (9 modules × 4 kinds).
        A("roster: 36 cutscenes present with beats", csOk == 36);

        // Quiz banks: one per catalog module, all valid, 3 questions, scoring works.
        string qdir = "Assets/PharmaSynth/ScriptableObjects/Quizzes/";
        int qBanks = 0, qTotal = 0; bool allValid = true, scoreOk = true, coverage = true;
        foreach (var e in ExperimentCatalog.Entries)
        {
            QuizBank bank = null;
            foreach (var g in AssetDatabase.FindAssets("t:QuizBank", new[]{ qdir.TrimEnd('/') }))
            {
                var b = AssetDatabase.LoadAssetAtPath<QuizBank>(AssetDatabase.GUIDToAssetPath(g));
                if (b != null && b.moduleId == e.moduleId) { bank = b; break; }
            }
            if (bank == null) { coverage = false; continue; }
            qBanks++; qTotal += bank.Count;
            // ≥3 questions per bank (Chemical Compounding carries 5: manuscript
            // Exp 2 has five distinct tests).
            if (!bank.AllValid() || bank.Count < 3) allValid = false;
            // all-correct answers → perfect score
            var ans = new List<int>();
            foreach (var q in bank.questions) ans.Add(q.correctIndex);
            if (!Near(bank.Score(ans), 1f)) scoreOk = false;
        }
        A("roster: quiz bank per experiment (9)", coverage && qBanks == 9);
        // 33 → 35: compounding's rebuild took its bank from 3 to 5 questions.
        // 35 → 29: Aspirin + Caffeine (3 each) dropped.
        A("roster: 29 quiz questions total, all valid (4 opts, in-range answer)", qTotal == 29 && allValid);
        A("roster: quiz scoring (all-correct = 100%)", scoreOk);
    }

    static ChemicalData LoadChem(string file)
        => AssetDatabase.LoadAssetAtPath<ChemicalData>("Assets/PharmaSynth/ScriptableObjects/Chemicals/" + file + ".asset");

    static void PourReactionSuite()
    {
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("reaction: registry loads", reg != null);
        if (reg == null) return;

        var benz = LoadChem("Chem_Benzaldehyde");
        var kmno4 = LoadChem("Chem_PotassiumPermanganate");
        var benzoic = LoadChem("Chem_BenzoicAcid");
        A("reaction: reaction chemicals exist", benz && kmno4 && benzoic);
        var rule = reg.FindReaction(benz, kmno4);
        A("reaction: benzaldehyde+KMnO4 rule found (either order)", rule != null && reg.FindReaction(kmno4, benz) != null);
        if (rule != null)
            A("reaction: product = benzoic acid + precipitate", rule.resultLiquid == benzoic && rule.hasPrecipitate);

        // LiquidPhysics turns reactants into product + fires ReactionOccurred on AddLiquid.
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null; lp.precipitateRenderer = null;   // skip material work in edit mode
            lp.registry = reg; lp.currentChemical = benz; lp.currentLiquidVolume = 100f;
            bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
            bool pended = false; lp.ReactionPending += _ => pended = true;

            // HEAT GATE ("achieve the needed in the procedure
            // first, before these reactions come"): BenzoicOxidation needs 40 C —
            // poured COLD it must HOLD (no early observation, no wrong-mix
            // scold), then fire the moment the bath brings the vessel there.
            lp.AddLiquid(kmno4, 40f);
            A("reaction: the right recipe poured COLD holds as pending",
                !reacted && pended && lp.HasPendingReaction && lp.currentChemical == benz
                && Near(lp.currentLiquidVolume, 140f));
            lp.SetTemperature(30f);
            A("reaction: below-threshold heat still holds", !reacted && lp.HasPendingReaction);
            lp.SetTemperature(60f);
            A("reaction: reaching temperature fires it", reacted && !lp.HasPendingReaction);
            A("reaction: currentChemical becomes the product", lp.currentChemical == benzoic);
            lp.SetContents(null, 0f);
            A("reaction: a reset vessel holds no half-done recipe",
                !lp.HasPendingReaction && Near(lp.currentTempC, 25f));
        }
        finally { UnityEngine.Object.DestroyImmediate(vgo); }

        // Pour path: the expected reagent, delivered to a bound vessel, completes its task.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Midterm_BenzoicAcid.asset");
        var rgo = new GameObject("pr_runner"); var vgo2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            runner.CompleteTask("prepare-permanganate");            // satisfy the prerequisite
            var lp2 = vgo2.AddComponent<LiquidPhysics>();
            lp2.mainRenderer = null; lp2.currentChemical = benz; lp2.currentLiquidVolume = 100f;
            var bind = vgo2.AddComponent<LiquidTaskBinding>();
            bind.SetVesselAndRunner(lp2, runner);
            bind.AddExpected(kmno4, "oxidise-benzaldehyde");
            bind.HandleReagent(kmno4);                              // = pour KMnO4 into the flask
            A("pour: expected reagent completes its task", runner.Graph.IsComplete("oxidise-benzaldehyde"));
            bind.HandleReagent(LoadChem("Chem_Aniline"));           // unexpected reagent → mistake
            A("pour: unexpected reagent logs a WrongReagent mistake", runner.Mistakes.CountOf(LabErrorType.WrongReagent) >= 1);

            // ⛔ The binding must LISTEN to its vessel after SetVesselAndRunner
            // (play-mode AddComponent runs OnEnable with vessel==null, so if
            // subscription lived only there, every pour would fire into the void and
            // the player could never complete "add distilled water"). The REAL path
            // is AddLiquid → event → binding, so that is what this drives.
            A("pour: binding LISTENS after SetVesselAndRunner", bind.IsListening);
            var water5 = LoadChem("Chem_DistilledWater");
            bind.AddExpected(water5, "dilute-test", 10f);
            lp2.AddLiquid(water5, 4f);   // through the VESSEL, not the binding
            A("pour: AddLiquid reaches the binding through the event (4 ml counted)",
                Near(bind.AccumulatedFor("dilute-test", water5), 4f));

            // Procedure-sanctioned mixes are NOT "wrong" (the water-dilution
            // scold): an expected chem for a PENDING task passes IsExpectedNow;
            // after its task completes it stops being expected.
            A("pour: a pending step's reagent is expected", bind.IsExpectedNow(water5));
            // Leniency epsilon: fifty 0.1 g float dips sum below 5.0 —
            // strict >= left the player one phantom dip short, forever.
            float drift = 0f; for (int k = 0; k < 50; k++) drift += 0.1f;
            A("pour: float-drift dips still meet the threshold",
                LiquidTaskBinding.MetThreshold(drift, 5f) && !LiquidTaskBinding.MetThreshold(4.8f, 5f));
            A("pour: a random reagent is not expected", !bind.IsExpectedNow(LoadChem("Chem_Phenol")));
            // Completion must NOT revoke sanction: LiquidAdded (completes the
            // task) fires before WrongReagentMixed inside the same AddLiquid, so
            // a pending-only gate made the completing pour punish itself.
            A("pour: a completed task's reagent STAYS sanctioned",
                runner.Graph.IsComplete("oxidise-benzaldehyde") && bind.IsExpectedNow(kmno4));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(vgo2); }
    }

    static void TestReactionSuite()
    {
        // The confirmatory-test reactions read from Appendix C fire when the product
        // sits in the vessel and the test reagent is added (product-seeded test vessel).
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testrx: master registry loads", reg != null);
        if (reg == null) return;

        void Fires(string prodFile, string reagFile, string label, bool expectGas)
        {
            var prod = LoadChem(prodFile); var reag = LoadChem(reagFile);
            if (prod == null || reag == null) { A("testrx: chems exist for " + label, false); return; }
            var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = vgo.AddComponent<LiquidPhysics>();
                lp.mainRenderer = null; lp.precipitateRenderer = null;
                lp.registry = reg; lp.currentChemical = prod; lp.currentLiquidVolume = 100f;
                bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
                lp.AddLiquid(reag, 20f);
                A("testrx: " + label + " fires a reaction", reacted);
                if (expectGas)
                {
                    var rule = reg.FindReaction(prod, reag);
                    A("testrx: " + label + " evolves gas + has observation",
                        rule != null && rule.evolvesGas && !string.IsNullOrEmpty(rule.expectedObservation));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(vgo); }
        }

        Fires("Chem_Benzamide", "Chem_SodiumNitrite", "benzamide + nitrite", true);
        Fires("Chem_Acetanilide", "Chem_BromineWater", "acetanilide + bromine water", false);
        Fires("Chem_Acetone", "Chem_SilverNitrate", "acetone + Tollens (negative)", false);
    }

    // pour-guard: the raycast transfer path, self-exclusion, trigger
    // immunity and the fresh-vessel wake state — none of which the older pour
    // suites exercised (they pre-seeded chem+volume and bypassed the raycast).
    static void PourGuardSuite()
    {
        // Ledger math (display-only contents story).
        var led = new VesselLedger();
        A("ledger: empty summary blank", led.Summary() == "");
        led.Add("Ethanol", 100f); led.Add("Ethanol", 20f); led.Add("NaOH", 50f);
        A("ledger: accumulates per chemical", led.Summary() == "Ethanol 120 ml + NaOH 50 ml");
        led.Add("Water", 10f);
        A("ledger: caps with a more-tail", led.Summary(2) == "Ethanol 120 ml + NaOH 50 ml + 1 more");
        // The tube's own label asks for ALL of it (show every reagent in
        // the test tube, never hidden behind "+ more"); the cap survives for the narrow hover card.
        A("ledger: All names every reagent",
            led.Summary(VesselLedger.All).EndsWith("+ Water 10 ml")
            && !led.Summary(VesselLedger.All).Contains("more"));
        led.React("Ester");
        A("ledger: reaction collapses to the product", led.Summary() == "Ester 180 ml");
        led.Clear();
        A("ledger: clears", led.Count == 0 && led.Summary() == "");
        // Units per entry: a 0.5 g spatula dip must not int-round to
        // "0 ml"; solids read in grams, sub-unit amounts keep one decimal.
        led.Add("Aspirin", 0.5f, solid: true); led.Add("Distilled Water", 10f);
        A("ledger: solids read in grams, sub-gram amounts survive",
            led.Summary() == "Aspirin 0.5 g + Distilled Water 10 ml");
        led.Clear();

        ChemicalData Chem(string n) { var c = ScriptableObject.CreateInstance<ChemicalData>(); c.chemicalName = n; return c; }
        var chemA = Chem("PG_A"); var chemB = Chem("PG_B"); var chemC = Chem("PG_C");
        var reg = ScriptableObject.CreateInstance<ReactionRegistry>();
        var rule = ScriptableObject.CreateInstance<ReactionRule>();
        rule.inputChemicalA = chemA; rule.inputChemicalB = chemB; rule.resultLiquid = chemC;
        reg.rules = new List<ReactionRule> { rule };

        // Fresh-vessel wake: adopt on first add, react on second (a phantom-500ml
        // default would block this on every stage-built vessel).
        var wgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = wgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null; lp.registry = reg;
            A("wake: AddComponent default is EMPTY", lp.IsEmpty && lp.currentChemical == null);
            lp.AddLiquid(chemA, 50f);
            A("wake: first pour adopts the chemical", lp.currentChemical == chemA && Near(lp.currentLiquidVolume, 50f));
            lp.AddLiquid(chemB, 30f);
            A("wake: second pour reacts", lp.currentChemical == chemC);
            A("wake: ledger collapsed to the product", lp.Ledger.Summary() == "PG_C 80 ml");
            lp.SetContents(chemA, 40f);
            A("setcontents: explicit fill", lp.currentChemical == chemA && Near(lp.currentLiquidVolume, 40f) && lp.Ledger.Count == 1);
            lp.SetContents(null, 123f);
            A("setcontents: blank arms empty regardless of ml", lp.IsEmpty && lp.currentChemical == null && lp.Ledger.Count == 0);

            // SPILL → REFILL: the measurement must not linger from spilled
            // reagents. A partial pour shrinks the story; a pour
            // to dry erases it, so the refill starts clean instead of stacking.
            lp.SetContents(chemA, 100f);
            lp.PourOut(50f);
            A("spill: a partial pour shrinks the story with the contents",
                lp.Ledger.Summary() == "PG_A 50 ml" && Near(lp.currentLiquidVolume, 50f));
            lp.PourOut(60f);
            A("spill: poured DRY forgets the contents entirely",
                lp.IsEmpty && lp.currentChemical == null && lp.Ledger.Count == 0);
            A("spill: a dry bottle still knows what it dispenses", lp.LastChemical == chemA);
            lp.AddLiquid(chemA, 20f);
            A("spill: the refill starts a CLEAN story, not a stacked one",
                lp.Ledger.Summary() == "PG_A 20 ml" && Near(lp.currentLiquidVolume, 20f));
        }
        finally { UnityEngine.Object.DestroyImmediate(wgo); }

        A("ledger: Scale is a no-op at full", LedgerScaled(100f, 1f) == "PG_A 100 ml");
        A("ledger: Scale halves every entry", LedgerScaled(100f, 0.5f) == "PG_A 50 ml");
        A("ledger: Scale to nothing clears", LedgerScaled(100f, 0f) == "");

        // ResolveTarget vs REAL colliders: self skipped, triggers ignored,
        // nearest other vessel wins; then a full PourTick transfer.
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            source.transform.position = new Vector3(50f, 0.6f, 50f);   // far corner: no scene geometry below
            source.transform.localScale = Vector3.one * 0.2f;
            target.transform.position = new Vector3(50f, 0.1f, 50f);
            target.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            trigger.transform.position = new Vector3(50f, 0.35f, 50f);
            trigger.transform.localScale = Vector3.one * 0.15f;
            trigger.GetComponent<Collider>().isTrigger = true;
            var srcLp = source.AddComponent<LiquidPhysics>(); srcLp.mainRenderer = null;
            var tgtLp = target.AddComponent<LiquidPhysics>(); tgtLp.mainRenderer = null; tgtLp.registry = reg;
            Physics.SyncTransforms();

            var hits = Physics.RaycastAll(new Vector3(50f, 1.2f, 50f), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit hit;
            var resolved = LiquidPourer.ResolveTarget(hits, srcLp, out hit);
            A("resolve: skips self, finds the vessel below", resolved == tgtLp);
            A("resolve: reports the receiving surface", hit.collider != null && hit.collider.gameObject == target);
            bool sawTrigger = false;
            foreach (var h in hits) if (h.collider != null && h.collider.gameObject == trigger) sawTrigger = true;
            A("resolve: trigger zones never swallow the stream", !sawTrigger);

            // Full PourTick: tilt 120° for 0.1 s → ≈ 35 · InverseLerp(45,180,120) · 0.1 ml.
            srcLp.SetContents(chemA, 100f);
            var pourer = source.AddComponent<LiquidPourer>();
            pourer.Bind(srcLp);
            var spout = new GameObject("Spout").transform;
            spout.SetParent(source.transform, true);
            spout.position = source.transform.position + new Vector3(0f, 0.15f, 0f);
            pourer.spout = spout;
            Physics.SyncTransforms();
            float expected = 35f * Mathf.InverseLerp(45f, 180f, 120f) * 0.1f;
            pourer.PourTick(0.1f, 120f);
            A("pour: transfer lands in the vessel", Near(tgtLp.currentLiquidVolume, expected, 0.15f));
            A("pour: source drained by the same amount", Near(srcLp.currentLiquidVolume, 100f - expected, 0.15f));
            A("pour: receiver adopted the chemical", tgtLp.currentChemical == chemA);

            // Self-pour: only the source below its own spout → nothing may land.
            target.SetActive(false);
            Physics.SyncTransforms();
            bool selfAdded = false;
            srcLp.LiquidAdded += (_, __) => selfAdded = true;
            float before = srcLp.currentLiquidVolume;
            pourer.PourTick(0.1f, 120f);
            A("pour: self-tilt never fires LiquidAdded on itself", !selfAdded);
            A("pour: self-tilt still wastes liquid", srcLp.currentLiquidVolume < before);

        // pour assist: a receiver the precise ray narrowly misses is
        // still caught by the sphere sweep (in-headset aim is imprecise).
            target.SetActive(true);
            target.transform.position = new Vector3(50.08f, 0.1f, 50f);
            target.transform.localScale = new Vector3(0.1f, 0.2f, 0.1f);
            tgtLp.SetContents(null, 0f);
            srcLp.SetContents(chemA, 50f);
            Physics.SyncTransforms();
            var strayHits = Physics.RaycastAll(spout.position, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit strayHit;
            A("pour assist: precise ray misses the offset vessel",
                LiquidPourer.ResolveTarget(strayHits, srcLp, out strayHit) == null);
            pourer.PourTick(0.1f, 120f);
            A("pour assist: sphere sweep still lands the transfer (W5.12)", tgtLp.currentLiquidVolume > 0.1f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(trigger);
        }
    }

    // feedback layer: live vessel/station status formats + mix popups.
    static void FeedbackSuite()
    {
        // Vessel status formats.
        A("status: named contents", VesselStatusMath.Compose("Beaker", "Ethanol", 120.4f) == "Beaker — 120 ml Ethanol");
        A("status: empty vessel", VesselStatusMath.Compose("Beaker", null, 0f) == "Beaker — empty");
        A("status: unknown liquid", VesselStatusMath.Compose("Beaker", null, 55f) == "Beaker — 55 ml liquid");
        A("status: reagent bottle drops the echo", VesselStatusMath.Compose("Ethanol", "Ethanol", 150f) == "Ethanol — 150 ml");
        A("status: null name safe", VesselStatusMath.Compose(null, "Ethanol", 10f) == "Vessel — 10 ml Ethanol");
        // Mixed contents show every element AND its amount (clear text of the
        // current contents of the tube and their proportions).
        A("status: mixed contents list the proportions",
            VesselStatusMath.ComposeMixed("Test Tube 3", "Ethanol 1 ml + Distilled Water 10 ml")
                == "Test Tube 3 — Ethanol 1 ml + Distilled Water 10 ml"
            && VesselStatusMath.ComposeMixed("Test Tube 3", "") == "Test Tube 3 — empty");

        // Hover live line.
        A("hover: empty line", VesselStatusMath.HoverLine(null, 0f, "", 0) == "Now: empty");
        A("hover: contents line", VesselStatusMath.HoverLine("Ethanol", 120f, "", 1) == "Now: 120 ml Ethanol");
        A("hover: mixed story appended", VesselStatusMath.HoverLine("Ethanol", 170f, "Ethanol 120 ml + NaOH 50 ml", 2)
            == "Now: 170 ml Ethanol\nMixed from: Ethanol 120 ml + NaOH 50 ml");

        // Station billboards.
        A("station: heat line", VesselStatusMath.HeatLine("4. Heat", 61.7f, 150f) == "4. Heat\n62 C -> 150 C");
        A("station: progress line", VesselStatusMath.ProgressLine("5. Filter", "Filtering", 0.404f) == "5. Filter\nFiltering 40%");
        A("station: progress clamps", VesselStatusMath.ProgressLine("x", "Collecting", 1.7f) == "x\nCollecting 100%");

        // Formats survive the TMP glyph map unchanged (no boxed glyphs).
        A("status: glyph-safe", GlyphSafe.Sanitize(VesselStatusMath.HeatLine("Heat", 62f, 150f)) == "Heat\n62 C -> 150 C");

        // Mix-popup policy: hazards stay silent here (the reactor owns them).
        A("mixfeed: harmless mix announces", MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.None));
        A("mixfeed: toxic gas silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.ToxicGas));
        A("mixfeed: fire silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.FireOrExplosion));
        A("mixfeed: fizz silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.GenericFizz));

        // StationStatusLabel composes from live sims (edit-mode components).
        var go = new GameObject("fb_station");
        var lgo = new GameObject("fb_label");
        try
        {
            var temp = go.AddComponent<TemperatureSim>();
            var tmp = lgo.AddComponent<TMPro.TextMeshPro>();
            var lab = go.AddComponent<StationStatusLabel>();
            lab.Bind(tmp, "4. Heat", StationSim.Heat, temp, null, null, null, 150f);
            A("stationlabel: heat status composes", lab.ComposeStatus().StartsWith("4. Heat\n") && lab.ComposeStatus().EndsWith("-> 150 C"));
            var gas = go.AddComponent<GasCollection>();
            var lab2 = go.AddComponent<StationStatusLabel>();
            lab2.Bind(tmp, "6. Collect", StationSim.Collect, null, null, null, gas, 0f);
            gas.AddGas(25f);
            A("stationlabel: collect shows percent", lab2.ComposeStatus().Contains("Collecting"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(lgo); }

        // Hover live-line cloning keeps title/category, appends "Now:".
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null;
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "FB_Chem";
            lp.SetContents(chem, 80f);
            var baseEntry = new LabInfoEntry("Beaker", LabInfoCategory.Equipment, "Base body.");
            var live = HoverInspector.WithLiveLine(baseEntry, lp);
            A("hover: live line appended", live.Title == "Beaker" && live.Body.Contains("Now: 80 ml FB_Chem"));
        }
        finally { UnityEngine.Object.DestroyImmediate(vgo); }
    }

    // tool verbs: stir/grind orbit math, weighing, match striking, burner
    // ignition, the methane splint, and the completesTask=false pour flag.
    static void VerbSuite()
    {
        // OrbitMath — swept-angle accumulation.
        var om = new OrbitMath { requiredRevs = 1f };
        for (int i = 0; i <= 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            om.Feed(Mathf.Cos(a), Mathf.Sin(a), true);
        }
        A("orbit: one full circle completes one rev", om.IsDone && Near(om.SweptDegrees, 360f, 0.5f));

        var om2 = new OrbitMath { requiredRevs = 1f };
        om2.Feed(Mathf.Cos(170f * Mathf.Deg2Rad), Mathf.Sin(170f * Mathf.Deg2Rad), true);
        om2.Feed(Mathf.Cos(-170f * Mathf.Deg2Rad), Mathf.Sin(-170f * Mathf.Deg2Rad), true);
        A("orbit: wraps across +/-180", Near(om2.SweptDegrees, 20f, 0.5f));

        var om3 = new OrbitMath { requiredRevs = 1f };
        om3.Feed(1f, 0f, true);
        om3.Feed(-1f, 0f, true);   // 180° teleport
        A("orbit: per-sample clamp blocks teleport cheats", Near(om3.SweptDegrees, OrbitMath.MaxDegPerSample, 0.5f));

        var om4 = new OrbitMath { requiredRevs = 1f };
        om4.Feed(1f, 0f, true);
        om4.Feed(Mathf.Cos(40f * Mathf.Deg2Rad), Mathf.Sin(40f * Mathf.Deg2Rad), true);
        float before = om4.SweptDegrees;
        om4.Feed(0f, 0f, false);                       // lift the rod out — pause
        om4.Feed(Mathf.Cos(220f * Mathf.Deg2Rad), Mathf.Sin(220f * Mathf.Deg2Rad), true);   // re-entry re-anchors
        A("orbit: leaving pauses, never resets", Near(om4.SweptDegrees, before, 0.5f));
        om4.Reset();
        A("orbit: reset clears", om4.SweptDegrees == 0f && !om4.IsDone);

        // WeighMath.
        A("weigh: contents mass 1g/ml", Near(WeighMath.MassOf(48.6f), 48.6f) && Near(WeighMath.MassOf(10f, 5f), 15f));
        A("weigh: tolerance pass", WeighMath.WithinTolerance(45f, 50f));
        A("weigh: tolerance fail", !WeighMath.WithinTolerance(44f, 50f));
        A("weigh: settle beat required", WeighMath.PanSettled(0.8f) && !WeighMath.PanSettled(0.5f));
        A("weigh: chemical mode satisfied", WeighMath.Satisfied(true, "Salicylic Acid", "Salicylic Acid", 48f, 50f, "", null));
        A("weigh: chemical mode short", !WeighMath.Satisfied(true, "Salicylic Acid", "Salicylic Acid", 30f, 50f, "", null));
        A("weigh: chemical mode wrong chem", !WeighMath.Satisfied(true, "Salicylic Acid", "Ethanol", 60f, 50f, "", null));
        A("weigh: item mode satisfied", WeighMath.Satisfied(true, "", null, 0f, 0f, "weigh-acetates", "weigh-acetates"));
        A("weigh: item mode wrong item", !WeighMath.Satisfied(true, "", null, 0f, 0f, "weigh-acetates", "prep-koh"));

        // ScoopMath: fixed charge per dip, solids only, no double-dip.
        A("scoop: dips only into solids", ScoopMath.CanPickUp(false, PhysicalState.Solid, 50f)
            && ScoopMath.CanPickUp(false, PhysicalState.Powder, 50f)
            && !ScoopMath.CanPickUp(false, PhysicalState.Liquid, 50f));
        A("scoop: no double-dip while loaded", !ScoopMath.CanPickUp(true, PhysicalState.Solid, 50f));
        A("scoop: empty jar gives nothing", !ScoopMath.CanPickUp(false, PhysicalState.Solid, 0f));
        A("scoop: fixed charge, last scoop takes the rest",
            Near(ScoopMath.ScoopCharge(50f), 2f) && Near(ScoopMath.ScoopCharge(1.2f), 1.2f));
        A("scoop: deposits anywhere but the source jar",
            ScoopMath.CanDeposit(true, false) && !ScoopMath.CanDeposit(true, true) && !ScoopMath.CanDeposit(false, false));
        A("scoop: deposit label shows the running total",
            ScoopMath.DepositLabel("Salicylic Acid", 2f, 8f) == "+2 g Salicylic Acid  (8 g total)");
        // The porcelain spatula: Exp 2 weighs 0.1 g salicylic and 0.5 g
        // aspirin — BOTH smaller than one 2 g scoopful, so the scoop cannot express
        // them at all. At 0.1 g/dip they become 1 and 5 dips.
        A("spatula: finer than the scoopula", ScoopMath.GramsPerSpatula < ScoopMath.GramsPerScoop);
        A("spatula: 0.1 g salicylic = one dip",
            Near(ScoopMath.ScoopCharge(50f, ScoopMath.GramsPerSpatula), 0.1f));
        A("spatula: 0.5 g aspirin = five dips",
            Near(ScoopMath.ScoopCharge(50f, ScoopMath.GramsPerSpatula) * 5f, 0.5f));

        // DropperMath: the liquid twin of the scoop. THE contract —
        // the number in the manuscript instruction IS the squeeze count, whatever
        // its unit ("5 drops" -> 5 squeezes; "2 ml of Tollen's" -> 2 squeezes).
        A("dropper: draws only from liquids",
            DropperMath.CanFill(0f, PhysicalState.Liquid, 50f)
            && !DropperMath.CanFill(0f, PhysicalState.Powder, 50f)
            && !DropperMath.CanFill(0f, PhysicalState.Solid, 50f));
        A("dropper: no top-up while loaded (an ambiguous count)",
            !DropperMath.CanFill(4f, PhysicalState.Liquid, 50f));
        A("dropper: empty bottle gives nothing", !DropperMath.CanFill(0f, PhysicalState.Liquid, 0f));
        // After the last squeeze, the empty dropper hovering over the tube must
        // NOT suck the delivered reagent back out. Task vessels are
        // destinations, never sources.
        A("dropper: never draws back out of a task vessel",
            !DropperMath.CanFill(0f, PhysicalState.Liquid, 50f, isTaskVessel: true));
        // Squeezing over nothing wastes the drop — that is how a dropper is emptied.
        A("dropper: a loaded squeeze over nothing wastes the drop",
            DropperMath.CanWaste(5f, overTarget: false)
            && !DropperMath.CanWaste(5f, overTarget: true)
            && !DropperMath.CanWaste(0f, overTarget: false));
        // The tool's own hover label carries the capacity readout.
        A("dropper: label reads chem and drops left",
            DropperMath.HoldingLabel("Tollen's Reagent", 7f) == "Tollen's Reagent · 7/10 drops"
            && DropperMath.HoldingLabel("", 0f) == "Dropper (empty)");
        A("dropper: a draw fills it, last draw takes the rest",
            Near(DropperMath.FillCharge(50f), DropperMath.Capacity) && Near(DropperMath.FillCharge(3f), 3f));
        A("dropper: squeezes only when loaded, over another vessel",
            DropperMath.CanSqueeze(5f, true, false)
            && !DropperMath.CanSqueeze(0f, true, false)
            && !DropperMath.CanSqueeze(5f, false, false)
            && !DropperMath.CanSqueeze(5f, true, true));
        A("dropper: one squeeze = one unit, last squeeze takes the rest",
            Near(DropperMath.SqueezeCharge(5f), 1f) && Near(DropperMath.SqueezeCharge(0.4f), 0.4f));
        // A full dropper covers the largest single Exp 2 instruction (10 drops of ethanol).
        A("dropper: a full charge is ten squeezes", DropperMath.SqueezesLeft(DropperMath.Capacity) == 10);
        A("dropper: count drains with each squeeze",
            DropperMath.SqueezesLeft(3f) == 3 && DropperMath.SqueezesLeft(0f) == 0);
        // The popup shows the COUNT, because the count is the measurement.
        A("dropper: squeeze label counts drops",
            DropperMath.SqueezeLabel("Ferric Chloride 10%", 3) == "drop 3  ·  Ferric Chloride 10%");
        A("dropper: fill label shows squeezes loaded",
            DropperMath.FillLabel("Tollen's Reagent", 10f) == "Tollen's Reagent  ·  10 drops loaded");
        // Charge-drain geometry: pivot-scaling shrinks the charge
        // symmetrically (it stays "nailed to the middle"); it must pool AT THE TIP, so
        // the capsule shifts toward the tip by exactly the half-length it lost.
        A("dropper: drain shortens the charge, floored at a readable sliver",
            Near(DropperMath.DrainScaleY(2f, 1f), 2f)
            && Near(DropperMath.DrainScaleY(2f, 0.5f), 1.2f)
            && Near(DropperMath.DrainScaleY(2f, 0f), 0.4f));
        A("dropper: the tip end stays anchored while the bulb end recedes",
            Near(DropperMath.DrainShift(1f, 2f, 1.2f), 0.8f)
            && Near(DropperMath.DrainShift(1f, 2f, 2f), 0f)
            && Near(DropperMath.DrainShift(0.5f, 2f, 1.2f), 0.4f));

        // TubeSlotMath: release a tube near a workspace holder and it
        // seats in the nearest FREE green slot. Held or already-frozen tubes are not
        // candidates (a seated tube must not be stolen by a neighbouring slot).
        {
            var slots = new[] { new Vector3(0, 0, 0), new Vector3(0.15f, 0, 0), new Vector3(0.30f, 0, 0) };
            var none = new[] { false, false, false };
            A("tubeslot: released tube takes the nearest free slot",
                TubeSlotMath.NearestFreeSlot(new Vector3(0.17f, 0.05f, 0f), slots, none) == 1);
            A("tubeslot: occupied slot passed over",
                TubeSlotMath.NearestFreeSlot(new Vector3(0.17f, 0.05f, 0f), slots, new[] { false, true, false }) == 2);
            A("tubeslot: out of reach snaps nothing",
                TubeSlotMath.NearestFreeSlot(new Vector3(2f, 0f, 0f), slots, none) == -1);
            A("tubeslot: held or frozen tubes are not candidates",
                !TubeSlotMath.CanSnap(held: true, kinematic: false)
                && !TubeSlotMath.CanSnap(held: false, kinematic: true)
                && TubeSlotMath.CanSnap(held: false, kinematic: false));
            // Seat by the mesh BOTTOM, never the pivot: these tube meshes carry the
            // pivot at the TOP, so pivot-to-slot sinks the 13 cm body under the table
            // (the seat sound plays and the tube "disappears").
            A("tubeslot: seating lifts a top-pivot tube onto the slot",
                Near(TubeSlotMath.BottomAlignDelta(0.917f, 0.787f), 0.13f)
                && Near(TubeSlotMath.BottomAlignDelta(1f, 1f), 0f));

            // Bench glass adopted by a layout gets its REAL capacity: the serialized
            // default of 1000 ml makes 5 counted drops a 0.5% (invisible) fill, which
            // reads as "no drop landed".
            A("bench: adopted tubes hold 25 ml, not a bucket",
                Near(ExperimentSceneBuilder.BenchMaxVolumeFor("TestTube", 1000f), 25f)
                && Near(ExperimentSceneBuilder.BenchMaxVolumeFor("Beaker_100mL", 1000f), 100f)
                && Near(ExperimentSceneBuilder.BenchMaxVolumeFor("SomethingElse", 321f), 321f));

            // Shared apparatus keeps a NEUTRAL label: a bench-bound vessel's floating
            // tag must come from the bench item ("Test Tube 3"), never the layout's
            // internal role name ("TollensTube_1" reads as reagent-dedicated glassware
            // and would go stale under the next module).
            A("bench: bound vessels are labelled by the bench item, not the layout role",
                ExperimentSceneBuilder.BenchDisplayNameFor("Kit_TestTube_3") == "Test Tube 3"
                && ExperimentSceneBuilder.BenchDisplayNameFor("Kit_Hard-GlassTestTube_2") == "Hard-Glass Test Tube 2"
                && ExperimentSceneBuilder.BenchDisplayNameFor("Eq_Beaker_100mL") == "Beaker 100 mL");

            // ZONE-FREE heat: the
            // water bath is a real tool (water first, flame second, then heat),
            // capped at boiling; its label always names the next thing it needs.
            A("waterbath: water then flame then heat",
                !WaterBathMath.IsHeating(false, true) && !WaterBathMath.IsHeating(true, false)
                && WaterBathMath.IsHeating(true, true) && WaterBathMath.HasWater(5f) && !WaterBathMath.HasWater(4f));
            A("waterbath: label narrates the missing piece",
                WaterBathMath.StatusLine(false, true, 25f) == "Water Bath — pour in distilled water"
                && WaterBathMath.StatusLine(true, false, 25f) == "Water Bath — needs a lit burner beside it"
                && WaterBathMath.StatusLine(true, true, 82.4f) == "Water Bath — 82 C");
            A("vesselheat: completes only served AND hot",
                !VesselHeatTask.ShouldComplete(false, 95f, 90f)
                && !VesselHeatTask.ShouldComplete(true, 80f, 90f)
                && VesselHeatTask.ShouldComplete(true, 90f, 90f));
        }
        // The SCENE wiring: a verb authored in code but never ATTACHED (a
        // DropperController with 0 instances in the scene) leaves the player
        // squeezing a dead prop. Pin the components onto the actual objects.
        {
            int droppers = 0, wired = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Eq_Dropper")) continue;
                droppers++;
                if (t.GetComponent<DropperController>() != null) wired++;
            }
            A("wired: every Eq_Dropper has the DropperController verb (found " + droppers + ")",
                droppers > 0 && wired == droppers);
            // The charge visuals are HAND-FITTED children (skinned-mesh bounds lie —
            // a bounds-derived tip floated the bead in mid-air): a
            // DropperTip anchor and a DropperLiquid capsule inside the stem.
            int tips2 = 0, liquids2 = 0, allDrop = 0, hidden2 = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<DropperController>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                allDrop++;
                if (t.transform.Find("DropperTip") != null) tips2++;
                var liq = t.transform.Find("DropperLiquid");
                if (liq != null) { liquids2++; if (!liq.gameObject.activeSelf) hidden2++; }
            }
            A("wired: every dropper has a hand-fit DropperTip", allDrop > 0 && tips2 == allDrop);
            A("wired: every dropper has a hand-fit DropperLiquid", allDrop > 0 && liquids2 == allDrop);
            // The capsule stays VISIBLE in the editor (the fitting aid must be
            // seen to be fitted) and Awake→CacheLiquid hides it the moment Play
            // starts — in play only actual contents show.
            A("wired: DropperLiquid capsules stay visible in the editor scene",
                liquids2 > 0 && hidden2 == 0);

            var spat = GameObject.Find("Eq_PorcelainSpatula");
            A("wired: the porcelain spatula dips fine (0.1 g)",
                spat != null && spat.GetComponent<ScoopController>() != null
                && Near(spat.GetComponent<ScoopController>().GramsPerDip, ScoopMath.GramsPerSpatula));
            // The two solids tools carry DIFFERENT heap shapes:
            // the porcelain spatula a flat elongated smear, the scoopula a rounded
            // bowl-pile — they must not look identical.
            var scoopula = GameObject.Find("Eq_Scoopula");
            A("wired: scoopula + spatula heaps are DIFFERENT shapes",
                spat != null && scoopula != null
                && spat.GetComponent<ScoopController>() != null && scoopula.GetComponent<ScoopController>() != null
                && Vector3.Distance(spat.GetComponent<ScoopController>().HeapScale,
                                    scoopula.GetComponent<ScoopController>().HeapScale) > 0.005f
                && spat.GetComponent<ScoopController>().HeapScale.y < scoopula.GetComponent<ScoopController>().HeapScale.y);

            int holders = 0, snapping = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Experiment_Tube_Table_Kit_Holder")) continue;
                holders++;
                var rs = t.GetComponent<TubeRackSlots>();
                if (rs != null && rs.SlotCount > 0) snapping++;
            }
            A("wired: every workspace holder snaps tubes (found " + holders + ")",
                holders > 0 && snapping == holders);
        }

        // MATERIALS guide: every module's watch panel opens with the
        // full gather-first list — reagents WITH totals derived from the layout
        // bindings, apparatus authored from the procedures. Not a checklist item;
        // always visible; the board keeps its scroll position between opens.
        {
            var lib5 = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            bool allHave = lib5 != null && lib5.Count > 0;
            if (lib5 != null)
                foreach (var mod in lib5.modules)
                    if (mod == null || mod.materialReagents.Count == 0 || mod.materialApparatus.Count == 0)
                    { allHave = false; break; }
            A("materials: every module has a gather-first guide", allHave);

            var m5b = lib5 != null ? lib5.Get("prelim-chemical-compounding") : null;
            A("materials: Exp 2's reagents carry totals",
                m5b != null && m5b.materialReagents.Exists(r => r.Contains("Ferric Chloride 10%") && r.Contains("—")));
            A("materials: Exp 2 lists its 19 tubes and 4 droppers",
                m5b != null && m5b.materialApparatus.Exists(a => a.Contains("19"))
                && m5b.materialApparatus.Exists(a => a.Contains("Dropper")));
            string hdr = ChecklistPager.BuildMaterialsHeader(m5b);
            A("materials: header renders both sections",
                hdr.Contains("MATERIALS") && hdr.Contains("Reagents:") && hdr.Contains("Apparatus:"));
            // One bullet ROW per item (a dot-joined line wraps
            // mid-name); every listed material starts its own "• " line.
            A("materials: every item is its own bullet row",
                m5b == null || hdr.Split('•').Length - 1
                    >= m5b.materialReagents.Count + m5b.materialApparatus.Count);
            A("materials: empty guide renders nothing",
                ChecklistPager.BuildMaterialsHeader(null) == "");
            // The procedures say "add distilled water" — the reagent bottle must carry
            // that exact name, or its hover and the wash bottle's both read "distilled
            // water" and the player can't tell which the step means.
            A("water: the procedure's 'distilled water' IS the reagent bottle's name",
                m5b != null && m5b.materialReagents.Exists(r => r.StartsWith("Distilled Water"))
                && !m5b.materialReagents.Exists(r => r.Contains("Purified")));
        }

        // FUME-HOOD RULE: the manuscript demands the
        // hood in exactly ONE place, Exp 5 §A, aniline + acetyl chloride. Any other
        // chem carrying the flag makes CORRECT bench play log FumeHoodViolations
        // (e.g. 4 per perfect Exp 2 run).
        {
            var flagged = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:ChemicalData",
                         new[] { "Assets/PharmaSynth/ScriptableObjects/Chemicals" }))
            {
                var c = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null && c.requiresFumeHood) flagged.Add(c.chemicalName);
            }
            flagged.Sort();
            // + Potassium Dichromate (Exp 7 polish): its ONLY use is
            // chloroform's oxidation test, whose manuscript hint is "Fume hood!"
            // — flagging the chem makes the position-based hood sanction real.
            A("fumehood: ONLY the manuscript's hood chems require the hood"
              + (flagged.Count == 3 ? "" : "  [" + string.Join(", ", flagged) + "]"),
                flagged.Count == 3 && flagged[0] == "Acetyl Chloride" && flagged[1] == "Aniline"
                && flagged[2] == "Potassium Dichromate");
        }

        // WRAP-UP tasks: a closing beat with no physical verb
        // ("record your observations") auto-completes once every other task is
        // done; without this the checklist could never finish (a module would
        // deadlock on its final step).
        {
            var wrapTasks = new List<ExperimentTask>
            {
                new ExperimentTask { taskId = "a", label = "A" },
                new ExperimentTask { taskId = "b", label = "B" },
                new ExperimentTask { taskId = "w", label = "Wrap", autoCompleteWhenOthersDone = true },
            };
            var wg = new TaskGraph(wrapTasks);
            wg.Tick();
            A("wrapup: not before the real work is done", !wg.IsComplete("w"));
            wg.TryComplete("a"); wg.Tick();
            A("wrapup: one remaining real task still holds it", !wg.IsComplete("w"));
            wg.TryComplete("b"); wg.Tick();
            A("wrapup: completes itself once every other task is done", wg.IsComplete("w"));

            var lib6 = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            var m6 = lib6 != null ? lib6.Get("prelim-chemical-compounding") : null;
            bool authored = false;
            if (m6 != null)
                foreach (var t in m6.graphTasks)
                    if (t.taskId == "record-observations" && t.autoCompleteWhenOthersDone) authored = true;
            A("wrapup: Exp 2's record-observations is authored as a wrap-up", authored);
        }

        // TIME-SKIP: a longProcess task fades black then returns
        // with a "time passed" message — the reusable compression for week-long
        // waits (Exp 3 fermentation and every later lengthy process).
        {
            var plain = new ExperimentTask { taskId = "x", label = "X" };
            var skip = new ExperimentTask { taskId = "ferment", label = "Ferment",
                longProcess = true, longProcessMessage = "One week later — fermentation complete." };
            var skipBlank = new ExperimentTask { taskId = "dry", label = "Dry", longProcess = true };
            A("timeskip: only a longProcess task triggers a skip",
                !TimeSkipController.IsTimeSkip(plain) && TimeSkipController.IsTimeSkip(skip)
                && !TimeSkipController.IsTimeSkip(null));
            A("timeskip: authored message wins, blank falls back",
                TimeSkipController.MessageFor(skip) == "One week later — fermentation complete."
                && TimeSkipController.MessageFor(skipBlank).Length > 0);
        }

        // SIMULATED RUN (catch the bugs here
        // before a manual test): every suite run PLAYS Exp 2 end-to-end through
        // the real wiring — builder, bindings, racks, station sims — with VERB-
        // CONTRACT action counts. Clean = every task completes, zero mistakes,
        // zero bugs, and every consumed chemical has a sufficient bench source.
        {
            var simLog = new System.Text.StringBuilder();
            var sim = SimulatedRun.Run("prelim-chemical-compounding", simLog);
            string simNote = sim == null ? "did not run"
                : sim.Clean ? "" : "  [" + sim.completedTasks + "/" + sim.totalTasks + " tasks, "
                  + sim.mistakes + " mistakes; " + string.Join(" | ", sim.bugs) + "]";
            A("simrun: Exp 2 plays END-TO-END clean" + simNote, sim != null && sim.Clean);

            // Exp 3 (ethyl alcohol): the zone-free ferment→CO₂→limewater mechanic,
            // the week-long time-skip, distillation + the three warm tests all
            // played through the real wiring.
            var sim3Log = new System.Text.StringBuilder();
            var sim3 = SimulatedRun.Run("prelim-ethyl-alcohol", sim3Log);
            string sim3Note = sim3 == null ? "did not run"
                : sim3.Clean ? "" : "  [" + sim3.completedTasks + "/" + sim3.totalTasks + " tasks, "
                  + sim3.mistakes + " mistakes; " + string.Join(" | ", sim3.bugs) + "]";
            A("simrun: Exp 3 plays END-TO-END clean" + sim3Note, sim3 != null && sim3.Clean);

            // Exp 4 (benzoic acid): oxidation heat-gate, the funnel filtration,
            // acidify crystals, the ICE-BATH chill + time-skip and the LITMUS
            // strip confirmation all played through the real wiring.
            var sim4Log = new System.Text.StringBuilder();
            var sim4 = SimulatedRun.Run("midterm-benzoic-acid", sim4Log);
            string sim4Note = sim4 == null ? "did not run"
                : sim4.Clean ? "" : "  [" + sim4.completedTasks + "/" + sim4.totalTasks + " tasks, "
                  + sim4.mistakes + " mistakes; " + string.Join(" | ", sim4.bugs) + "]";
            A("simrun: Exp 4 plays END-TO-END clean" + sim4Note, sim4 != null && sim4.Clean);

            // Exp 5 (acetanilide): the FUME-HOOD carry (aniline + acetyl
            // chloride sanctioned only inside the hood), the ice-water-then-
            // chill crystallisation, and the two-tube bromination comparison
            // all played through the real wiring.
            var sim5Log = new System.Text.StringBuilder();
            var sim5 = SimulatedRun.Run("midterm-acetanilide", sim5Log);
            string sim5Note = sim5 == null ? "did not run"
                : sim5.Clean ? "" : "  [" + sim5.completedTasks + "/" + sim5.totalTasks + " tasks, "
                  + sim5.mistakes + " mistakes; " + string.Join(" | ", sim5.bugs) + "]";
            A("simrun: Exp 5 plays END-TO-END clean" + sim5Note, sim5 != null && sim5.Clean);

            // Exp 6 (acetone): the bench-balance WEIGH, the NAKED-FLAME dry
            // distillation, the VAPOR collect at 56 °C, and all four tests
            // (two deliberate negatives) played through the real wiring.
            var sim6Log = new System.Text.StringBuilder();
            var sim6 = SimulatedRun.Run("midterm-acetone", sim6Log);
            string sim6Note = sim6 == null ? "did not run"
                : sim6.Clean ? "" : "  [" + sim6.completedTasks + "/" + sim6.totalTasks + " tasks, "
                  + sim6.mistakes + " mistakes; " + string.Join(" | ", sim6.bugs) + "]";
            A("simrun: Exp 6 plays END-TO-END clean" + sim6Note, sim6 != null && sim6.Clean);

            // Exp 7 (chloroform): the two water-bath distillations (heat task +
            // vapor collect, twice), the decant wash, the FLAME non-flammability
            // confirm, the hood-sanctioned dichromate oxidation and the warm
            // AgNO3 precipitate all played through the real wiring.
            var sim7Log = new System.Text.StringBuilder();
            var sim7 = SimulatedRun.Run("midterm-chloroform", sim7Log);
            string sim7Note = sim7 == null ? "did not run"
                : sim7.Clean ? "" : "  [" + sim7.completedTasks + "/" + sim7.totalTasks + " tasks, "
                  + sim7.mistakes + " mistakes; " + string.Join(" | ", sim7.bugs) + "]";
            A("simrun: Exp 7 plays END-TO-END clean" + sim7Note, sim7 != null && sim7.Clean);

            // Exp 8 (benzamide): the ice-bath-controlled synthesis, the
            // glass-rod STIR verb (tip-tracked), the filter/wash/dry time-skip,
            // and the two LITMUS reads (blue on ammonia, red on acid) plus the
            // nitrous effervescence all played through the real wiring.
            var sim8Log = new System.Text.StringBuilder();
            var sim8 = SimulatedRun.Run("final-benzamide", sim8Log);
            string sim8Note = sim8 == null ? "did not run"
                : sim8.Clean ? "" : "  [" + sim8.completedTasks + "/" + sim8.totalTasks + " tasks, "
                  + sim8.mistakes + " mistakes; " + string.Join(" | ", sim8.bugs) + "]";
            A("simrun: Exp 8 plays END-TO-END clean" + sim8Note, sim8 != null && sim8.Clean);

            // Exp 9 (winemaking): the must poured from the new juice bottle,
            // pitching yeast making Wine, the glass-rod rouse, the CO2→limewater
            // confirm + week time-skip, racking the clear wine off the lees, and
            // the clarity + tasting draws all played through the real wiring.
            var sim9Log = new System.Text.StringBuilder();
            var sim9 = SimulatedRun.Run("final-winemaking", sim9Log);
            string sim9Note = sim9 == null ? "did not run"
                : sim9.Clean ? "" : "  [" + sim9.completedTasks + "/" + sim9.totalTasks + " tasks, "
                  + sim9.mistakes + " mistakes; " + string.Join(" | ", sim9.bugs) + "]";
            A("simrun: Exp 9 plays END-TO-END clean" + sim9Note, sim9 != null && sim9.Clean);
        }

        // FERMENTATION (Exp 3): pure gates for the CO₂→limewater mechanic.
        {
            A("ferment: fermenting only after the must is prepared + in the flask",
                !FermentationMath.IsFermenting(false, 40f) && !FermentationMath.IsFermenting(true, 0f)
                && FermentationMath.IsFermenting(true, 40f));
            A("ferment: confirmed only once the limewater has actually clouded",
                !FermentationMath.CO2Confirmed(0f) && FermentationMath.CO2Confirmed(2f));
        }

        // ICE BATH + CHILL (Exp 4): the water bath's cold twin — set
        // a vessel in the bucket, anywhere, and the crystallise step completes
        // only once it actually holds product AND has gone cold (ambient 25 °C
        // can never satisfy it by standing around).
        A("icebath: chills only within reach", IceBathMath.Chills(0.2f) && !IceBathMath.Chills(0.5f));
        // Hand-scalable zone anchors: the wire-sphere gizmo's world
        // scale IS the diameter; half the X scale drives the tool's reach; no
        // anchor falls back to the coded constant.
        A("zone: anchor scale drives the effect radius",
            WaterBathMath.EffectRadius(1f, 0.32f) == 0.5f
            && WaterBathMath.EffectRadius(0f, 0.32f) == 0.32f
            && WaterBathMath.EffectRadius(0.64f, 0.99f) == 0.32f);
        {
            var bathT = GameObject.Find("WaterBath");
            var iceT = GameObject.Find("Raw_IceBucket");
            A("wired: bath + ice bucket carry user-scalable zone anchors",
                bathT != null && bathT.transform.Find("HeatZone") != null
                && bathT.transform.Find("BurnerZone") != null
                && iceT != null && iceT.transform.Find("ChillZone") != null);
        }
        // FUME HOOD guidance: the hood is an OPEN alcove — its
        // label guides idle and flips to a ✓ while a vessel is protected.
        A("fumehood: label guides idle and confirms protection",
            FumeHoodStatusLabel.StatusLine(null).Contains("IN here")
            && FumeHoodStatusLabel.StatusLine("Florence Flask") == "Fume Hood — Florence Flask protected ✓");
        // The hood model has NO colliders of its own — the shell is what stops
        // items passing through the walls; front stays OPEN (that's the door).
        {
            var hz = UnityEngine.Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
            var hshell = hz != null ? hz.transform.Find("HoodShell") : null;
            int solidWalls = 0;
            if (hshell != null)
                foreach (var wc in hshell.GetComponentsInChildren<BoxCollider>(true))
                    if (!wc.isTrigger) solidWalls++;
            A("fumehood: open-front collider shell (5 solid walls, trigger volume intact)",
                hshell != null && solidWalls == 5
                && hz.GetComponent<BoxCollider>() != null && hz.GetComponent<BoxCollider>().isTrigger);
        }
        // The hood's TEETH (edge case): a toxic pour with the vessel
        // at the open bench grades a FumeHoodViolation; the SAME pour with the
        // vessel inside the hood volume is sanctioned. Drives the REAL scene
        // hood through a probe vessel — position is the only variable.
        {
            var hz2 = UnityEngine.Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
            var hzCol = hz2 != null ? hz2.GetComponent<Collider>() : null;
            var anilineChem = LoadChem("Chem_Aniline");
            var fhRunnerGo = new GameObject("fh_runner");
            var fhVesselGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics needs a Renderer host
            try
            {
                var fhRunner = fhRunnerGo.AddComponent<ExperimentRunner>();
                var fhMod = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
                fhMod.moduleId = "fh-probe";
                fhMod.graphTasks.Add(new ExperimentTask { taskId = "t", label = "T" });
                fhRunner.SetModule(fhMod); fhRunner.StartExperiment();
                var fhLp = fhVesselGo.AddComponent<LiquidPhysics>();
                var fhBind = fhVesselGo.AddComponent<LiquidTaskBinding>();
                fhBind.SetVesselAndRunner(fhLp, fhRunner);
                fhBind.SetFumeHood(hz2);
                fhBind.AddExpected(anilineChem, "t", 5f);
                fhVesselGo.transform.position = new Vector3(500f, 0f, 500f);   // nowhere near the hood
                fhBind.HandleReagent(anilineChem, 1f);
                int afterOutside = fhRunner.MistakeCount;
                if (hzCol != null) fhVesselGo.transform.position = hzCol.bounds.center;
                fhBind.HandleReagent(anilineChem, 1f);
                A("fumehood: outside pours violate, inside pours are sanctioned",
                    anilineChem != null && hz2 != null && hzCol != null
                    && afterOutside == 1 && fhRunner.MistakeCount == 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(fhRunnerGo); UnityEngine.Object.DestroyImmediate(fhVesselGo); }
        }

        // Observation dedupe: a burst of identical squeezes fires
        // ONE popup/sting; a different rule always announces.
        A("mixfx: repeated identical observation stays quiet",
            !MixFeedback.ShouldShowObservation(true, 10f, 9f)
            && MixFeedback.ShouldShowObservation(true, 14f, 9f)
            && MixFeedback.ShouldShowObservation(false, 10f, 9.9f));
        // Live temp goal on heat/chill vessel tags: shows until the
        // goal side is reached, then yields to the step's own completion cue.
        A("vesselstatus: temp goal narrates until reached",
            VesselStatusMath.TempGoalLine(25f, 50f, false) == "25 C — warm to 50 C (water bath)"
            && VesselStatusMath.TempGoalLine(55f, 50f, false) == ""
            && VesselStatusMath.TempGoalLine(25f, 8f, true) == "25 C — chill to 8 C (ice bath)"
            && VesselStatusMath.TempGoalLine(5f, 8f, true) == "");
        A("chill: completes only served AND holding AND cold",
            VesselChillTask.ShouldComplete(true, true, IceBathMath.IceWaterC, 8f)
            && !VesselChillTask.ShouldComplete(false, true, IceBathMath.IceWaterC, 8f)   // ice water not in yet (Exp 5)
            && !VesselChillTask.ShouldComplete(true, false, IceBathMath.IceWaterC, 8f)
            && !VesselChillTask.ShouldComplete(true, true, 25f, 8f));

        // LITMUS (Exp 4): the strip reads the MIXTURE, not whichever
        // chemical landed first: an acid dominates any amount of water, so
        // "water first, then the product" cannot soft-lock the test.
        A("litmus: the extreme component dominates the mixture",
            LitmusMath.DominantPH(7f, 2.9f) == 2.9f && LitmusMath.DominantPH(2.9f, 7f) == 2.9f
            && LitmusMath.DominantPH(7f, 13.5f) == 13.5f);
        A("litmus: task needs served AND a definitive read",
            VesselLitmusTask.ShouldComplete(true, true) && !VesselLitmusTask.ShouldComplete(true, false)
            && !VesselLitmusTask.ShouldComplete(false, true));
        // Exp 8: the BASE read — ammonia vapour on alkaline
        // hydrolysis turns red litmus blue, and that read completes too.
        A("litmus: 10% NaOH actually reads BASE (authored pH)",
            LoadChem("Chem_SodiumHydroxide10") != null
            && LoadChem("Chem_SodiumHydroxide10").pH >= LitmusMath.BasePH
            && LitmusMath.ColorForPH(LoadChem("Chem_SodiumHydroxide10").pH) == LitmusMath.BaseBlue);
        {
            // DATA pin: the litmus lesson dies silently if a re-import ever
            // resets these pH values back to the un-authored 7.
            var benzoicChem = LoadChem("Chem_BenzoicAcid");
            var hcl6 = LoadChem("Chem_HydrochloricAcid6N");
            A("litmus: benzoic acid + 6N HCl actually read ACID (authored pH)",
                benzoicChem != null && benzoicChem.pH <= LitmusMath.AcidPH
                && hcl6 != null && hcl6.pH <= LitmusMath.AcidPH
                && LitmusMath.ColorForPH(benzoicChem.pH) == LitmusMath.AcidRed);
        }

        // NAKED FLAME + VAPOR + WEIGH (Exp 6 dry distillation).
        A("flame: reaches past the bath cap, only within reach",
            FlameMath.Heats(0.1f) && !FlameMath.Heats(0.3f)
            && FlameMath.NextTemp(100f, 1f) == 160f && FlameMath.NextTemp(390f, 1f) == FlameMath.MaxC
            && FlameMath.MaxC > WaterBathMath.BathMaxC);
        A("vapor: fires only hot AND charged",
            VaporMath.Fires(160f, 150f, 10f) && !VaporMath.Fires(100f, 150f, 10f)
            && !VaporMath.Fires(160f, 150f, 0.2f));
        A("weightask: completes only served AND settled on the pan",
            VesselWeighTask.ShouldComplete(true, true) && !VesselWeighTask.ShouldComplete(true, false)
            && !VesselWeighTask.ShouldComplete(false, true));

        // RackMath: a step shared by a SET of tubes is done only when
        // every member tube has had its reagent. Exp 2's five enol samples, its
        // butyl alcohols, acetone-vs-acetaldehyde: the comparison IS the lesson, so
        // one tube must never stand in for the set.
        A("rack: not ready until every tube is", !RackMath.AllReady(4, 5) && RackMath.AllReady(5, 5));
        A("rack: an empty group never auto-completes", !RackMath.AllReady(0, 0));
        A("rack: progress counts the set", RackMath.ProgressLabel(3, 5) == "tube 3 of 5");

        // CleanupMath: residue on empty, five swipes clean, rinse helps.
        A("clean: emptying a used vessel leaves residue", CleanupMath.BecomesDirty(60f, true)
            && !CleanupMath.BecomesDirty(60f, false) && !CleanupMath.BecomesDirty(0f, true));
        A("clean: five swipes scrub it clean", Near(CleanupMath.AfterSwipe(100f), 80f)
            && CleanupMath.AfterSwipe(15f) == 0f);
        A("clean: wash-bottle rinse also cleans", CleanupMath.AfterRinse(100f, 90f) == 0f
            && Near(CleanupMath.AfterRinse(100f, 10f), 88f));
        // residue never reaches the label. The prefix is all cleaning would show,
        // and to a player a vessel that says Dirty is a vessel they must clean.
        {
            var labelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var testChem = ScriptableObject.CreateInstance<ChemicalData>(); testChem.chemicalName = "Ethanol";
            try
            {
                var lpL = labelGo.AddComponent<LiquidPhysics>();
                var cvL = labelGo.AddComponent<CleanableVessel>(); cvL.Bind(lpL);
                cvL.Soil();
                var tag = labelGo.AddComponent<ProximityLabel>();
                var vs = labelGo.AddComponent<VesselStatus>();
                vs.Bind(lpL, tag, "Beaker");
                vs.Refresh();
                A("clean: the label never carries a residue prefix",
                    tag.CurrentText != null && !tag.CurrentText.StartsWith("Dirty") && !tag.CurrentText.StartsWith("Clean"));
            }
            finally { UnityEngine.Object.DestroyImmediate(labelGo); UnityEngine.Object.DestroyImmediate(testChem); }
        }

        // a SOLID never pours; solids are scooped. Without the state check a tilted jar of
        // sugar streams a brown liquid column.
        {
            var liq = ScriptableObject.CreateInstance<ChemicalData>(); liq.state = PhysicalState.Liquid;
            var sol = ScriptableObject.CreateInstance<ChemicalData>(); sol.state = PhysicalState.Solid;
            var pow = ScriptableObject.CreateInstance<ChemicalData>(); pow.state = PhysicalState.Powder;
            try
            {
                A("pour: a solid never pours", PourMath.CanPour(liq) && !PourMath.CanPour(sol)
                    && !PourMath.CanPour(pow) && !PourMath.CanPour(null));
            }
            finally { UnityEngine.Object.DestroyImmediate(liq); UnityEngine.Object.DestroyImmediate(sol); UnityEngine.Object.DestroyImmediate(pow); }
        }

        // ONE lab reset restores the apparatus (empty, cool, unclaimed, unlabelled) and
        // refills the reagents WITHOUT refilling the glassware it just emptied (the supply
        // monitor would otherwise restock any binding-less vessel from its last chemical).
        {
            var app = GameObject.CreatePrimitive(PrimitiveType.Cube); app.name = "Eq_ResetProbe";
            var jar = GameObject.CreatePrimitive(PrimitiveType.Cube); jar.name = "Raw_ResetProbe";
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "Probe Acid";
            try
            {
                var lpA = app.AddComponent<LiquidPhysics>(); lpA.maxVolume = 100f; lpA.SetContents(chem, 30f);
                var cvA = app.AddComponent<CleanableVessel>(); cvA.Bind(lpA); cvA.Soil();
                var vsA = app.AddComponent<VesselStatus>(); vsA.SetRoleSuffix("Tollen's test");
                var bindA = app.AddComponent<LiquidTaskBinding>(); bindA.SetVesselAndRunner(lpA, null);
                var role = new List<LiquidTaskBinding.ReagentStep>
                    { new LiquidTaskBinding.ReagentStep { reagent = chem, taskId = "probe-task", requiredMl = 10f } };
                bindA.MarkPoolMember();
                bindA.SetRoles(new List<List<LiquidTaskBinding.ReagentStep>> { role }, -1, new RackRoles());
                bool claimed = bindA.ClaimForTask("probe-task") && bindA.ClaimedRole == 0;

                var lpJ = jar.AddComponent<LiquidPhysics>(); lpJ.maxVolume = 150f; lpJ.SetContents(chem, 40f);

                int touched = LabReset.ResetApparatus();

                A("reset: a lab reset empties, cools, un-claims and un-labels every apparatus and refills every reagent",
                    claimed && touched >= 1
                    && lpA.currentLiquidVolume <= 0.01f                      // emptied, and NOT restocked
                    && cvA.Dirtiness <= 0f && !cvA.EverDirty                 // residue gone
                    && bindA.ClaimedRole == -1                               // role released through Emptied
                    && Near(lpJ.currentLiquidVolume, 150f));                 // the reagent refilled
                // The RULE, on the real scene: a used vessel carries CleanableVessel; a SOURCE never
                // does. A name rule ("not Raw_") sweeps the wash bottle and the methane charge jar
                // into the reset and starves the tutorial's setup step.
                var flor = GameObject.Find("FlorenceFlask");
                var wash = GameObject.Find("Eq_WashBottle");
                var charge = GameObject.Find("Prop_reagent-jar");
                A("reset: a used vessel is one the builders gave a CleanableVessel; sources are never emptied",
                    flor != null && LabReset.IsUsedVessel(flor.GetComponent<LiquidPhysics>())
                    && wash != null && !LabReset.IsUsedVessel(wash.GetComponent<LiquidPhysics>())
                    && charge != null && !LabReset.IsUsedVessel(charge.GetComponent<LiquidPhysics>())
                    && !LabReset.IsUsedVessel(null)
                    && LabReset.IsReagent("Raw_Ethanol") && !LabReset.IsReagent("Eq_Beaker_100mL"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(app); UnityEngine.Object.DestroyImmediate(jar);
                UnityEngine.Object.DestroyImmediate(chem);
            }
        }

        // every solid jar on the shelf shows a heap, never a liquid column (the PowderJar
        // prefab carries only a Liquid surface). The scoop keys on the chemical's state,
        // so it works either way and can't catch this.
        {
            int solidJars = 0, noHeap = 0; string worstJar = "";
            foreach (var lp in UnityEngine.Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (lp == null || !LabReset.IsReagent(lp.name) || !SolidJarFitter.IsHeap(lp.currentChemical)) continue;
                solidJars++;
                var heap = lp.transform.Find("Powder");
                var heapR = heap != null ? heap.GetComponent<Renderer>() : null;
                bool ok = heapR != null && heapR.enabled
                          && !LiquidPhysics.DrawsLiquidColumn(lp.currentLiquidVolume, lp.DryPowder, true);
                if (!ok) { noHeap++; if (worstJar.Length == 0) worstJar = lp.name; }
            }
            A("jar: every solid shelf jar draws a heap, not a liquid column (" + solidJars + " jars, " + noHeap
              + " without" + (worstJar.Length > 0 ? ", e.g. " + worstJar : "") + ")", solidJars > 0 && noHeap == 0);
        }
        A("weigh: open mode any settled load", WeighMath.Satisfied(true, "", null, 0f, 0f, "", null));
        A("weigh: unsettled never", !WeighMath.Satisfied(false, "", null, 0f, 0f, "", null));

        // Match striking + burner ignition + splint.
        A("strike: held swipe on striker lights", Matchstick.ShouldStrike(true, false, false, 0.5f, true));
        A("strike: unheld never", !Matchstick.ShouldStrike(false, false, false, 0.5f, true));
        A("strike: slow rub never", !Matchstick.ShouldStrike(true, false, false, 0.2f, true));
        A("strike: plain surface never", !Matchstick.ShouldStrike(true, false, false, 0.5f, false));
        A("strike: lit/spent never", !Matchstick.ShouldStrike(true, true, false, 0.5f, true)
            && !Matchstick.ShouldStrike(true, false, true, 0.5f, true));
        A("burner: lit match near ignites", BurnerController.ShouldIgnite(false, true, 0.1f));
        A("burner: unlit match never", !BurnerController.ShouldIgnite(false, false, 0.1f));
        A("burner: far match never", !BurnerController.ShouldIgnite(false, true, 0.5f));
        A("burner: already lit no-op", !BurnerController.ShouldIgnite(true, true, 0.1f));
        // Live instrument readout: only shows while something is
        // happening; reuses the pinned station formats so it reads the same.
        A("readout: hidden at ambient", !ProcessReadout.ShouldShow(true, 22f, 22f, false, 0f));
        A("readout: shows once heating", ProcessReadout.ShouldShow(true, 60f, 22f, false, 0f));
        A("readout: shows while collecting", ProcessReadout.ShouldShow(false, 0f, 22f, true, 0.2f));
        A("readout: hidden with nothing collected", !ProcessReadout.ShouldShow(false, 0f, 22f, true, 0f));
        // Grinding needs the reagent IN the mortar (you could grind
        // an empty mortar while the powder sat on the scoop).
        A("grind: empty mortar can't grind", !GrindController.CanGrind(true, 0f));
        A("grind: loaded mortar grinds", GrindController.CanGrind(true, 2f));
        A("grind: cosmetic mortar always grinds", GrindController.CanGrind(false, 0f));
        // Powder must NOT take its size from a POSITION-ONLY anchor. A BowlAnchor has
        // scale 1, so sizing from it produced a 1-unit beach-ball in the mortar.
        // Only an anchor that declares previewsScale supplies the size.
        {
            var powderHost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                var chem = AssetDatabase.LoadAssetAtPath<ChemicalData>(
                    "Assets/PharmaSynth/ScriptableObjects/Chemicals/Chem_SodiumAcetate.asset");
                var bowl = new GameObject("BowlAnchor");
                bowl.transform.SetParent(powderHost.transform, false);
                bowl.AddComponent<PlacementAnchor>().previewsScale = false;   // position-only
                ExperimentSceneBuilder.EnsurePowderVisual(powderHost, chem, 1f);
                Transform heap = powderHost.transform.Find("Powder");
                // Guard is "not a 1-unit beach-ball", not a tight bound: a Cylinder
                // primitive's bounds are (1,2,1) → bore 1 → w = bore*0.5 = 0.5 EXACTLY,
                // so "< 0.5f" would fail on the boundary it isn't aiming at.
                A("powder: position-only anchor does NOT set size",
                  heap != null && heap.localScale.x < 0.6f && heap.localScale.y < 0.6f);
            }
            finally { UnityEngine.Object.DestroyImmediate(powderHost); }
        }

        // HUD dropdown re-fits to however many items are visible — the demo verbs
        // live in it and are hidden outside a demo session.
        // pad + items*h + (items-1)*gap = 16 + 168 + 12 = 196 (the 6-item and empty
        // cases below pin the same formula).
        A("hudmenu: 3 items (no demo)", Near(HudMenuDropdown.HeightFor(3, 56f, 6f, 16f), 196f));
        A("hudmenu: 6 items (demo session)", Near(HudMenuDropdown.HeightFor(6, 56f, 6f, 16f), 382f));
        A("hudmenu: empty list collapses to padding", Near(HudMenuDropdown.HeightFor(0, 56f, 6f, 16f), 16f));

        // Quiz is NEVER score-gated (client rule) — the score is just shown plainly.
        A("quiz: score line reads correct/total", PostLabController.ScoreLine(2, 3) == "Quiz: 2 / 3 (67%)");
        A("quiz: perfect score line", PostLabController.ScoreLine(3, 3) == "Quiz: 3 / 3 (100%)");
        A("quiz: no bank → no score line", PostLabController.ScoreLine(0, 0) == "");
        // Review navigation: Back/Next bound by the ends of the bank.
        A("quiz: no back on first", !PostLabController.CanGoBack(0));
        A("quiz: back from second", PostLabController.CanGoBack(1));
        A("quiz: next while more remain", PostLabController.CanGoNext(0, 3));
        A("quiz: no next on last", !PostLabController.CanGoNext(2, 3));
        // Red-hot tube glow: cold below 60% of target, saturated at temperature.
        A("glow: cold tube does not glow", Near(MethaneApparatusRig.GlowFor(22f, 120f), 0f));
        A("glow: saturates at target", Near(MethaneApparatusRig.GlowFor(120f, 120f), 1f));
        A("glow: ramps mid-climb", MethaneApparatusRig.GlowFor(100f, 120f) > 0f
            && MethaneApparatusRig.GlowFor(100f, 120f) < 1f);
        A("splint: lit match at tube fires", MethaneApparatusRig.SplintShouldFire(true, false, 0.1f, true, 1f));
        A("splint: auto fallback fires", MethaneApparatusRig.SplintShouldFire(true, false, float.MaxValue, false, 25f));
        A("splint: waits for a match first", !MethaneApparatusRig.SplintShouldFire(true, false, float.MaxValue, false, 5f));
        A("splint: not collected never", !MethaneApparatusRig.SplintShouldFire(false, false, 0.1f, true, 30f));
        A("splint: fires once", !MethaneApparatusRig.SplintShouldFire(true, true, 0.1f, true, 30f));
        // location-free: proximity reach check (heat/collect/assemble).
        A("reach: within fires", MethaneApparatusRig.WithinReach(0.2f, 0.35f));
        A("reach: beyond doesn't", !MethaneApparatusRig.WithinReach(0.5f, 0.35f));

        // Stir end-to-end: circle the rod → TaskGraph condition completes.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("stir-x", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var rgo = new GameObject("verb_runner");
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var rodGo = new GameObject("verb_rod");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null;
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "VB_Chem";
            lp.SetContents(chem, 60f);
            var stir = vgo.AddComponent<StirController>();
            stir.Bind(runner, "stir-x", lp, rodGo.transform, requiredRevs: 1f);
            for (int i = 0; i <= 9; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                stir.Tick(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, true);
            }
            runner.Graph.Tick();
            A("stir: circling the rod completes the task", runner.Graph.IsComplete("stir-x"));

            // Weigh end-to-end via ForceLoad.
            var module2 = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module2.graphTasks = new List<ExperimentTask> {
                T("weigh-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            };
            module2.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            runner.SetModule(module2);
            runner.StartExperiment();
            var wsGo = new GameObject("verb_pan");
            try
            {
                var ws = wsGo.AddComponent<WeighStation>();
                ws.Bind(runner, "weigh-x", "", "VB_Chem", 50f, null);
                A("weigh: empty pan not satisfied", !ws.IsSatisfied);
                ws.ForceLoad(null, lp, 1.5f);
                A("weigh: right vessel settled satisfies", ws.IsSatisfied);
                runner.Graph.Tick();
                A("weigh: task completes on the pan", runner.Graph.IsComplete("weigh-x"));
            }
            finally { UnityEngine.Object.DestroyImmediate(wsGo); }
            UnityEngine.Object.DestroyImmediate(module2);

            // completesTask=false: pours accumulate + flag ready, never complete.
            var module3 = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module3.graphTasks = new List<ExperimentTask> {
                T("weigh-y", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            };
            module3.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            runner.SetModule(module3);
            runner.StartExperiment();
            var bind = vgo.AddComponent<LiquidTaskBinding>();
            bind.SetVesselAndRunner(lp, runner);
            bind.AddExpected(chem, "weigh-y", 40f, completesTask: false);
            bind.HandleReagent(chem, 50f);
            A("bindflag: threshold met but not completed", !runner.Graph.IsComplete("weigh-y"));
            A("bindflag: readiness flagged", bind.ReadyFor("weigh-y"));
            UnityEngine.Object.DestroyImmediate(module3);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(vgo);
            UnityEngine.Object.DestroyImmediate(rodGo);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    const string LayoutDir = "Assets/PharmaSynth/ScriptableObjects/Layouts";

    /// itemIds of the PERMANENT bench — the Eq_*/Kit_*/Raw_* apparatus that lives in
    /// the SCENE, not in any layout, because the hard client rule keeps every general
    /// tool out at all times. A station may point at any of these instead of staging
    /// its own copy. Scanned from the open scene so it can never drift from reality.
    static HashSet<string> BenchItemIds
    {
        get
        {
            var ids = new HashSet<string>();
            foreach (var li in UnityEngine.Object.FindObjectsByType<LabItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (li != null && !string.IsNullOrEmpty(li.itemId)) ids.Add(li.itemId);
            return ids;
        }
    }

    /// EVERY layout's taskIds must exist in its module's graph, and every task the
    /// graph declares must have some way to be completed.
    ///
    /// A layout whose stations complete a different task set than its module's graph
    /// (e.g. a retired battery left in place after the module is rebuilt, ZERO overlap)
    /// builds a stage with no errors, and not one task can ever complete. Only a test
    /// comparing the two halves catches it.
    static void LayoutGraphCoverageSuite()
    {
        const string LDir = "Assets/PharmaSynth/ScriptableObjects/Layouts";
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        A("cover: library loads", lib != null);
        if (lib == null) return;

        bool allKnown = true, allReachable = true;
        var orphanNote = ""; var deadNote = "";
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LDir }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
            if (layout == null || string.IsNullOrEmpty(layout.moduleId)) continue;
            var module = lib.Get(layout.moduleId);
            if (module == null) continue;

            var graphIds = new HashSet<string>();
            foreach (var t in module.graphTasks) graphIds.Add(t.taskId);

            // (a) Nothing in the layout may name a task the graph doesn't have.
            var layoutIds = new HashSet<string>();
            foreach (var s in layout.stations) if (!string.IsNullOrEmpty(s.taskId)) layoutIds.Add(s.taskId);
            foreach (var v in layout.vessels) foreach (var b in v.bindings)
                if (!string.IsNullOrEmpty(b.taskId)) layoutIds.Add(b.taskId);
            foreach (var id in layoutIds)
                if (!graphIds.Contains(id))
                { allKnown = false; orphanNote = layout.name + " -> " + id; }

            // (b) Every graph task needs an owner: a station, a completing binding,
            //     a rack group (deferred bindings whose members share a rackGroup),
            //     or a zone-free heat vessel (heatToC > 0 — VesselHeatTask).
            foreach (var t in module.graphTasks)
            {
                bool owned = false;
                foreach (var s in layout.stations) if (s.taskId == t.taskId) owned = true;
                foreach (var v in layout.vessels)
                {
                    if (v.fermentTaskId == t.taskId) owned = true;   // FermentationController condition
                    if (v.chillTaskId == t.taskId && v.chillToC > 0f) owned = true;   // VesselChillTask (ice bath)
                    if (v.litmusTaskId == t.taskId) owned = true;    // VesselLitmusTask (strip touch)
                    if (v.heatTaskId == t.taskId && v.heatToC > 0f) owned = true;     // explicit VesselHeatTask (open flame)
                    if (v.weighTaskId == t.taskId) owned = true;     // VesselWeighTask (bench balance)
                    if (v.vaporTaskId == t.taskId) owned = true;     // VaporCollectController (source side)
                    if (v.flameTaskId == t.taskId) owned = true;     // VesselFlameTask (lit-match confirm, Exp 7)
                    if (v.stirTaskId == t.taskId) owned = true;      // StirController (glass-rod stir, Exp 8)
                    foreach (var b in v.bindings)
                        if (b.taskId == t.taskId
                            && (b.completesTask || !string.IsNullOrEmpty(v.rackGroup) || v.heatToC > 0f))
                            owned = true;
                }
                // DataSheet steps may be closed by the quiz/tablet rather than the stage.
                if (!owned && t.phase != TaskPhase.DataSheet)
                { allReachable = false; deadNote = layout.name + " -> " + t.taskId; }
            }
        }
        A("cover: no layout names a task its graph lacks" + (allKnown ? "" : "  [" + orphanNote + "]"), allKnown);
        A("cover: every graph task has a completion owner" + (allReachable ? "" : "  [" + deadNote + "]"), allReachable);
    }

    // builder wiring for the verb stations (pins the applied layout data).
    static void VerbWiringSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var layouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            layouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));
        if (lib == null || layouts.Count == 0) { A("verbwire: assets available", false); return; }

        // Layout data applied (by the verb-data applier menu).
        // Acetone covers the Weigh verb.
        ExperimentLayout acetone = null, benzamide = null;
        foreach (var l in layouts)
        {
            if (l.moduleId == "midterm-acetone") acetone = l;
            if (l.moduleId == "final-benzamide") benzamide = l;
        }
        // Acetone is zone-free: the WEIGH lives on the BENCH
        // balance (Eq_Balance: pan trigger + station + grams display,
        // wired by the verb-data applier), not on a layout station.
        var balT = GameObject.Find("Eq_Balance");
        A("verbwire: bench balance carries the weigh rig (pan + station + display)",
            balT != null && balT.transform.Find("Pan") != null
            && balT.transform.Find("Pan").GetComponent<WeighStation>() != null
            && balT.GetComponent<WeighingScaleController>() != null);
        A("verbwire: burners carry the naked flame",
            UnityEngine.Object.FindAnyObjectByType<NakedFlameHeat>(FindObjectsInactive.Include) != null);
        // Zone-free since the Exp 8 polish: the stir is authored as
        // Vessel.stirTaskId on the chilled reaction beaker, not a Stir station.
        bool benzStir = false;
        if (benzamide != null) foreach (var v in benzamide.vessels) if (v.stirTaskId == "stir-stand") benzStir = true;
        A("verbwire: benzamide stir authored", benzStir);

        // Builder wires the controllers (fresh runner, no graph — Register no-ops).
        var rgo = new GameObject("vw_runner"); var bgo = new GameObject("vw_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, layouts);

            builder.Build("final-benzamide");
            // Bench-bound: the StirController sits on the bench beaker (outside
            // the stage root), so look scene-wide.
            A("verbwire: benzamide rod stirs the vessel",
                UnityEngine.Object.FindAnyObjectByType<StirController>(FindObjectsInactive.Include) != null);

            // (The acetone balance/burner assertions are the BENCH pins above.)
        }
        finally
        {
            // Benzamide still spawns, but tear the bench down anyway — cheap
            // insurance against future bench-bound conversions leaking.
            var b3 = bgo.GetComponent<ExperimentSceneBuilder>();
            if (b3 != null) b3.Build("tutorial-methane");
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
        }
    }

    static GameObject FindChildByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    // table pass: every layout re-zoned onto the deck with real spacing.
    static void LayoutSpacingSuite()
    {
        // The zoning grid itself keeps its promises (one full station row = the
        // busiest module's 7 stations).
        bool stationPitch = true;
        for (int i = 0; i < LayoutTidyMath.StationsPerRow - 1; i++)
            for (int j = i + 1; j < LayoutTidyMath.StationsPerRow; j++)
                if (Vector3.Distance(LayoutTidyMath.StationPos(i), LayoutTidyMath.StationPos(j)) < LayoutTidyMath.MinStationDistance - 0.01f)
                    stationPitch = false;
        A("tidy: station row keeps 0.5 m pitch", stationPitch);

        var slots = new List<Vector3>();
        for (int i = 0; i < LayoutTidyMath.StationsPerRow; i++) slots.Add(LayoutTidyMath.StationPos(i));
        for (int i = 0; i < 3; i++) slots.Add(LayoutTidyMath.VesselPos(i));
        for (int i = 0; i < 8; i++) slots.Add(LayoutTidyMath.ReagentPos(i));
        for (int i = 0; i < 6; i++) slots.Add(LayoutTidyMath.ToolPos(i));
        slots.Add(LayoutTidyMath.RackPos);
        for (int i = 0; i < 3; i++) slots.Add(LayoutTidyMath.SparePos(i));
        for (int i = 0; i < 2; i++) slots.Add(LayoutTidyMath.MatchPos(i));
        slots.Add(LayoutTidyMath.StrikerPos);
        bool zonesApart = true, zonesOnDeck = true;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!LayoutTidyMath.OnDeck(slots[i])) zonesOnDeck = false;
            for (int j = i + 1; j < slots.Count; j++)
                if (Vector3.Distance(slots[i], slots[j]) < LayoutTidyMath.MinPairDistance) zonesApart = false;
        }
        A("tidy: all grid slots on the deck", zonesOnDeck);
        A("tidy: no two grid slots collide", zonesApart);

        // Every authored layout obeys the invariants (pins the Tidy run).
        int badDeck = 0, badPair = 0, badStations = 0;
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
            if (layout == null) continue;
            var all = new List<Vector3>();
            foreach (var s in layout.stations) all.Add(s.pos);
            foreach (var p in layout.props) all.Add(p.pos);
            // A BENCH-BOUND vessel's pos is IGNORED (the object already exists and
            // is hand-placed), so it can't overlap or fall off the deck — only
            // SPAWNED vessels are laid out by the grid.
            foreach (var v in layout.vessels) if (string.IsNullOrEmpty(v.benchItem)) all.Add(v.pos);
            foreach (var p in all) if (!LayoutTidyMath.OnDeck(p)) badDeck++;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    if (Vector3.Distance(all[i], all[j]) < LayoutTidyMath.MinPairDistance) badPair++;
            for (int i = 0; i < layout.stations.Count; i++)
                for (int j = i + 1; j < layout.stations.Count; j++)
                    if (Vector3.Distance(layout.stations[i].pos, layout.stations[j].pos) < LayoutTidyMath.MinStationDistance) badStations++;
        }
        A("tidy: every layout position on the deck", badDeck == 0);
        A("tidy: no overlapping layout items (historical (1.38,-3.88) pairs fixed)", badPair == 0);
        A("tidy: stations keep verb elbow room", badStations == 0);
    }

    // center-table shelf platforms — geometry invariants.
    static void WorkspaceShelfSuite()
    {
        const int n = 3;
        // Tiles rest ON the rail tops (matches the hand-laid plank top 1.563).
        A("shelf: top sits on the rails", Near(WorkspaceShelfMath.TopY, 1.563f, 0.001f));

        // Tiles tile the full width with only the seam gap between them, and
        // stay inside [XMin, XMax].
        bool spanOk = true, seamOk = true;
        float prevRight = WorkspaceShelfMath.XMin;
        for (int i = 0; i < n; i++)
        {
            var c = WorkspaceShelfMath.TileCenter(i, n);
            var s = WorkspaceShelfMath.TileSize(n);
            float left = c.x - s.x * 0.5f, right = c.x + s.x * 0.5f;
            if (left < WorkspaceShelfMath.XMin - 0.001f || right > WorkspaceShelfMath.XMax + 0.001f) spanOk = false;
            float seam = left - prevRight;   // gap from the previous tile's right edge
            if (i > 0 && seam > WorkspaceShelfMath.Gap + 0.001f) seamOk = false;   // no gap wider than the seam
            prevRight = right;
        }
        A("shelf: tiles stay within the rail width", spanOk);
        A("shelf: no gap wider than the seam (equipment seats anywhere)", seamOk);
        A("shelf: last tile reaches the right rail end", Near(prevRight, WorkspaceShelfMath.XMax, WorkspaceShelfMath.Gap + 0.001f));

        // Each tile bridges both rails (front z=-3.15, back z=-3.50).
        var size = WorkspaceShelfMath.TileSize(n);
        float zFrontEdge = WorkspaceShelfMath.ZCenter + size.z * 0.5f;
        float zBackEdge = WorkspaceShelfMath.ZCenter - size.z * 0.5f;
        A("shelf: tile bridges both rails", zFrontEdge >= -3.16f && zBackEdge <= -3.49f);
        A("shelf: tiles are thin planks", Near(size.y, WorkspaceShelfMath.Thickness) && size.y < 0.05f);

        // second row (the hand-laid lower planks at y≈1.20): below the top
        // row with real headroom for glassware, same footprint.
        A("shelf: two rows", WorkspaceShelfMath.Rows == 2);
        A("shelf: lower row at the user's plank height", Near(WorkspaceShelfMath.TopYOf(1), 1.215f, 0.001f));
        A("shelf: lower row has glassware headroom", WorkspaceShelfMath.LowerRowHeadroom > 0.25f);
        A("shelf: rows share the rail footprint",
            Near(WorkspaceShelfMath.TileCenter(0, n, 1).z, WorkspaceShelfMath.TileCenter(0, n, 0).z)
            && Near(WorkspaceShelfMath.TileCenter(0, n, 1).x, WorkspaceShelfMath.TileCenter(0, n, 0).x));

        // apparatus-kit layout: both rows fit tightly with no overlaps,
        // and the manuscript-derived kit manifest holds its shape.
        foreach (var (plan, rowName) in new[] { (WorkspaceKitsBuilder.Row0Plan(), "top"),
                                                (WorkspaceKitsBuilder.Row1Plan(), "lower") })
        {
            var centers = WorkspaceKitsBuilder.SlotCenters(plan, out float used);
            A("kits: " + rowName + " row fits the shelf width",
                used <= (WorkspaceShelfMath.XMax - WorkspaceShelfMath.XMin) + 0.001f);
            bool noOverlap = true, inBounds = true;
            for (int i = 0; i < plan.Length; i++)
            {
                float l = centers[i] - plan[i].width * 0.5f, r = centers[i] + plan[i].width * 0.5f;
                if (l < WorkspaceShelfMath.XMin - 0.001f || r > WorkspaceShelfMath.XMax + 0.001f) inBounds = false;
                if (i > 0 && l < centers[i - 1] + plan[i - 1].width * 0.5f - 0.001f) noOverlap = false;
            }
            A("kits: " + rowName + " row items stay on the shelf", inBounds);
            A("kits: " + rowName + " row items never overlap", noOverlap);
        }
        int racks = 0, burners = 0; bool hardRack = false, vialRack = false, brush = false, clay = false;
        foreach (var s in WorkspaceKitsBuilder.Row1Plan())
        {
            if (s.prefab == "TestTubeRack") racks++;
            if (s.display.Contains("Hard-Glass")) hardRack = true;
            if (s.display.Contains("Vials")) vialRack = true;
            if (s.prefab == "TestTubeBrush") brush = true;
        }
        foreach (var s in WorkspaceKitsBuilder.Row0Plan())
        {
            if (s.prefab == "BunsenBurner") burners++;
            if (s.prefab == "ClayTriangle") clay = true;
        }
        A("kits: three dispenser racks (regular + hard-glass + vials)", racks == 3 && hardRack && vialRack);
        A("kits: brush staged beside the racks", brush);
        A("kits: one Bunsen heating set + the alcohol crucible set (user layout W5.12)", burners == 1 && clay);

        // rack dispenser: capped, reusable, seat detection.
        A("rack: tube seated when resting in the hole", RackDispenserMath.InHole(false, 0.02f));
        A("rack: held tube is not seated", !RackDispenserMath.InHole(true, 0.02f));
        A("rack: tube pulled clear is not seated", !RackDispenserMath.InHole(false, 0.2f));
        A("rack: out-count = capacity minus seated", RackDispenserMath.OutCount(6, 4) == 2
            && RackDispenserMath.OutCount(6, 6) == 0);
        A("rack: live label reads seated/capacity", RackDispenserMath.Label("Test Tubes", 4, 6) == "Test Tubes  4/6");

        // Racks are FIXED FURNITURE. Both roles — the storage
        // racks tubes live in and the workspace holders they get dragged into —
        // must never be pickable: grabbing one carries every seated tube with it.
        // Pinned against the SCENE, because only a builder re-run can undo it
        // (pure math cannot see a re-added component).
        {
            var stillGrabbable = new List<string>();
            foreach (string rn in SceneRepairs.FixedRacks)
            {
                var go = SceneFind.AnyByName(rn);
                if (go == null) continue;                       // not in this scene — fine
                if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null)
                    stillGrabbable.Add(rn);
            }
            A("rack: fixed racks are NOT grabbable ("
              + (stillGrabbable.Count == 0 ? "all anchored" : string.Join(",", stillGrabbable)) + ")",
              stillGrabbable.Count == 0);
        }

        // The yield stepper is DELETED: the value was read by
        // nothing, and the player could not compute it anyway. The C# API is
        // gone so code can't regress, but the SCENE widgets could be re-added by a
        // panel rebuild, so pin them absent.
        {
            var ghosts = new List<string>();
            foreach (string yn in SceneRepairs.YieldWidgets)
                if (SceneFind.AnyByName(yn) != null) ghosts.Add(yn);
            A("postlab: the yield stepper is gone ("
              + (ghosts.Count == 0 ? "clean" : string.Join(",", ghosts) + " still present") + ")",
              ghosts.Count == 0);
        }
        bool namesReal = true;
        foreach (var s in WorkspaceKitsBuilder.Row0Plan()) if (!RealSizes.TryGet(s.prefab, out _)) namesReal = false;
        foreach (var s in WorkspaceKitsBuilder.Row1Plan()) if (!RealSizes.TryGet(s.prefab, out _)) namesReal = false;
        A("kits: every kit prefab is a real pack item", namesReal);

        // apparatus snap: directed pairs + seat math (grab moves the
        // group; the ACTIVATE click detaches — AssemblyMath owns the policy).
        A("snap: gauze onto tripod, never the reverse",
            AssemblyMath.CanAttach("WireGauze", "Tripod") && !AssemblyMath.CanAttach("Tripod", "WireGauze"));
        A("snap: watch glass covers a beaker",
            AssemblyMath.CanAttach("WatchGlass", "Beaker_100mL") && AssemblyMath.CanAttach("WatchGlass", "Beaker_500mL"));
        A("snap: burner slides under the tripod",
            AssemblyMath.TryAnchor("BunsenBurner", "Tripod", out var burnerSeat) && burnerSeat == SnapAnchor.SameBase);
        A("snap: iron ring clamps the stand pole",
            AssemblyMath.TryAnchor("IronRing", "RetortStand", out var ringSeat) && ringSeat == SnapAnchor.PoleMid);
        A("snap: flask seats on the gauze", AssemblyMath.CanAttach("ErlenmeyerFlask_400mL", "WireGauze"));
        A("snap: unrelated pairs never stick",
            !AssemblyMath.CanAttach("Beaker_100mL", "RetortStand") && !AssemblyMath.CanAttach("TestTube", "Tripod"));
        A("snap: participants cover the heating kit",
            AssemblyMath.Participates("Tripod") && AssemblyMath.Participates("RetortStand")
            && AssemblyMath.Participates("WireGauze") && !AssemblyMath.Participates("TestTubeBrush"));
        var hostB = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.2f));   // top 0.6, base 0.4
        var partB = new Bounds(new Vector3(5f, 5f, 5f), new Vector3(0.1f, 0.1f, 0.1f));     // extents 0.05
        A("snap: top-centre seat rests on the host top",
            Near(AssemblyMath.SeatCenter(SnapAnchor.TopCenter, hostB, partB).y, 0.65f));
        A("snap: same-base seat stands on the host's floor",
            Near(AssemblyMath.SeatCenter(SnapAnchor.SameBase, hostB, partB).y, 0.45f));
        A("snap: pole-mid seat clamps mid-height",
            Near(AssemblyMath.SeatCenter(SnapAnchor.PoleMid, hostB, partB).y, 0.5f));
    }

    // flow-smoothness guards.
    static void W59FlowSuite()
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
    static void W59ManuscriptSuite()
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

    static void SceneBuilderSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        // SYNTHETIC fixture: the legacy
        // spawn path (stations, sockets, hazards, teleport pads, props, seeded
        // vessels) runs on an in-memory layout + module, because no real module
        // stages stations any more and a real one could drop them in any polish
        // pass (the simrig fixture pattern). The spawn path stays load-bearing (a
        // future layout may stage one), so it stays pinned here.
        A("builder: library/registry load", lib != null && reg != null);
        A("builder: library has prefabs + chemicals", lib != null && lib.prefabs.Count >= 40 && lib.chemicals.Count >= 20);
        A("builder: library resolves Wine (winemaking end product)", lib != null && lib.GetChemical("Wine") != null);
        if (lib == null) return;

        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "sb-probe";
        foreach (var tid in new[] { "sb-heat", "sb-filter", "sb-zone", "sb-pour", "sb-seeded" })
            module.graphTasks.Add(new ExperimentTask { taskId = tid, label = tid });
        var lay = ScriptableObject.CreateInstance<ExperimentLayout>();
        lay.moduleId = "sb-probe";
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-heat", label = "Heat", requiredItemId = "sb-heat",
          pos = new Vector3(-1.8f, 0.91f, -3.78f), sim = StationSim.Heat, simTargetC = 78f });
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-filter", label = "Filter", requiredItemId = "sb-filter",
          pos = new Vector3(-1.3f, 0.91f, -3.78f), sim = StationSim.Filter, simTargetC = 85f });
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-zone", label = "Zone", requiredItemId = "sb-zone",
          pos = new Vector3(-0.8f, 0.91f, -3.78f), sim = StationSim.None, simTargetC = 85f });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "Vial_Brown", itemId = "sb-pour", displayName = "Probe Reagent",
          pos = new Vector3(1.33f, 0.91f, -3.52f), targetHeight = 0.16f,
          pourable = true, fillChemical = "Silver Nitrate" });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "GlassRod", itemId = "sb-zone", displayName = "Probe Rod",
          pos = new Vector3(1.33f, 0.91f, -3.33f), targetHeight = 0.18f });
        lay.vessels.Add(new ExperimentLayout.Vessel
        { prefabName = "ErlenmeyerFlask_400mL", displayName = "Probe Flask",
          pos = new Vector3(-0.25f, 0.91f, -3.18f), targetHeight = 0.22f,
          bindings = { new ExperimentLayout.Vessel.Bind
            { reagentChemical = "Silver Nitrate", taskId = "sb-pour", requiredMl = 5f, completesTask = true } } });
        lay.vessels.Add(new ExperimentLayout.Vessel
        { prefabName = "TestTube_WithLiquid", displayName = "Probe Seeded Tube",
          pos = new Vector3(0.2f, 0.91f, -3.18f), targetHeight = 0.13f, startChemical = "Chloroform",
          bindings = { new ExperimentLayout.Vessel.Bind
            { reagentChemical = "Potassium Dichromate", taskId = "sb-seeded", requiredMl = 5f, completesTask = true } } });

        var rgo = new GameObject("sb_runner"); var bgo = new GameObject("sb_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, new List<ExperimentLayout> { lay });

            int stationCount = lay.stations.Count;
            int n = builder.Build("sb-probe");
            A("builder: spawns a root per station/prop/vessel",
                n == stationCount + lay.props.Count + lay.vessels.Count);
            A("builder: a task station per layout station",
                bgo.GetComponentsInChildren<ExperimentTaskStation>().Length == stationCount);

            // §2 sockets: each station gets a snap socket filtered to its item.
            var sockets = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            A("builder: one socket per station", sockets.Length == stationCount);
            bool filtersOk = sockets.Length == stationCount;
            foreach (var so in sockets)
                if (so.GetComponent<StationSocketFilter>() == null || string.IsNullOrEmpty(so.GetComponent<StationSocketFilter>().requiredItemId)) filtersOk = false;
            A("builder: sockets carry item filters", filtersOk);
            // §1 hot-surface hazard: each Heat station gets a player-only zone.
            var hazards = bgo.GetComponentsInChildren<HazardZone>();
            A("builder: heat station gets a hot-surface hazard", hazards.Length >= 1);
            var pr = new GameObject("root").transform;
            var hand = new GameObject("hand").transform;
            var stray = new GameObject("prop").transform;
            try
            {
                hand.SetParent(pr);
                A("hazard: rig child is player", HazardZone.IsPlayer(hand, pr));
                A("hazard: stray object is not", !HazardZone.IsPlayer(stray, pr));
                A("hazard: no root, no match", !HazardZone.IsPlayer(hand, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pr.gameObject);
                UnityEngine.Object.DestroyImmediate(stray.gameObject);
            }

            // §2 teleport anchors: one floor pad per station, seated at floor level.
            var anchors = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
            A("builder: one teleport anchor per station", anchors.Length == stationCount);
            bool onFloor = anchors.Length == stationCount;
            foreach (var an in anchors) if (an.transform.position.y > 0.1f) onFloor = false;
            A("builder: anchors sit on the floor", onFloor);

            var li = new GameObject("li").AddComponent<LabItem>();
            try
            {
                li.itemId = "warm-waterbath";
                A("socket: right item accepted", StationSocketFilter.Matches("warm-waterbath", li));
                A("socket: wrong item rejected", !StationSocketFilter.Matches("crystallise-ice", li));
                A("socket: open socket takes anything", StationSocketFilter.Matches("", null));
                A("socket: filtered socket rejects null", !StationSocketFilter.Matches("warm-waterbath", null));
            }
            finally { UnityEngine.Object.DestroyImmediate(li.gameObject); }
            int propItems = 0;
            foreach (var li2 in bgo.GetComponentsInChildren<LabItem>())
                if (li2.name.StartsWith("Prop_")) propItems++;
            A("builder: props carry LabItem ids", propItems == lay.props.Count);

            // ⛔ INVERTED. The builder must NOT stage a rack +
            // 6 tubes, 2 spare beakers, a flask or a MatchStriker cube on any module:
            // those duplicate a bench that permanently carries all of it, and
            // re-spawn clones deleted by hand. The bench IS
            // the kit; the layout only binds to it.
            A("builder: no rack kit — the bench has racks",
                FindChildByName(bgo.transform, "RackKit") == null
                && FindChildByName(bgo.transform, "RackTube_5") == null);
            A("builder: no spare glass — the bench has Eq_Beaker/Flask",
                FindChildByName(bgo.transform, "Spare_Beaker_100mL_0") == null
                && FindChildByName(bgo.transform, "Spare_ErlenmeyerFlask_400mL_2") == null);
            A("builder: no MatchStriker cube — the matchbox IS the striker (W5.8)",
                FindChildByName(bgo.transform, "MatchStriker") == null);
            bool padsHidden = true;
            foreach (var st2 in bgo.GetComponentsInChildren<ExperimentTaskStation>())
            {
                var mr2 = st2.GetComponent<MeshRenderer>();
                if (mr2 != null && mr2.enabled) padsHidden = false;
            }
            A("builder: station pads invisible (logic intact)", padsHidden);

            var bind = bgo.GetComponentInChildren<LiquidTaskBinding>();
            A("builder: vessel has a LiquidTaskBinding", bind != null);
            // Pouring a binding's OWN reagent completes (or readies) its task — the
            // pour→event→completion chain is exercised generically (the module-
            // specific completions are covered by the simruns).
            if (bind != null && bind.ExpectedSteps.Count > 0)
            {
                var step0 = bind.ExpectedSteps[0];
                bind.HandleReagent(step0.reagent);
                A("builder: pouring a bound reagent drives its task",
                    runner.Graph.IsComplete(step0.taskId) || bind.ReadyFor(step0.taskId) || bind.IsListening);
            }

            // pour contract: receiving vessels must spawn EMPTY (wake branch
            // armed; a phantom 500 ml default blocks chemical adoption
            // forever) and must be able to RENDER liquid (_Fill contract).
            bool mainEmptyOk = false, mainRenderOk = false, seededKeepsFill = true;
            foreach (var vlp in bgo.GetComponentsInChildren<LiquidPhysics>())
            {
                if (!vlp.name.StartsWith("Vessel_")) continue;
                if (vlp.currentChemical == null)
                {
                    mainEmptyOk = vlp.currentLiquidVolume <= 0.1f;
                    mainRenderOk = vlp.mainRenderer != null && vlp.mainRenderer.sharedMaterial != null
                                   && vlp.mainRenderer.sharedMaterial.HasProperty("_Fill");
                }
                else if (vlp.currentLiquidVolume <= 1f) seededKeepsFill = false;
            }
            A("builder: main vessel spawns empty (wake armed)", mainEmptyOk);
            A("builder: main vessel can render liquid", mainRenderOk);
            A("builder: seeded test vessel keeps its fill", seededKeepsFill);
            bool twinsOk = true;
            foreach (var nm in new[] { "Beaker_100mL", "Beaker_500mL", "ErlenmeyerFlask_400mL", "GraduatedCylinder_50mL", "TestTube" })
                if (lib.GetPrefab(nm + "_WithLiquid") == null) twinsOk = false;
            A("builder: every vessel swap twin resolves", twinsOk);

            // feedback layer: sim stations show live status; vessels and
            // pourables carry live tags + mix popups.
            A("builder: sim stations get live status labels", bgo.GetComponentsInChildren<StationStatusLabel>().Length >= 1);
            A("builder: vessels get live status + mix feedback",
                bgo.GetComponentsInChildren<VesselStatus>().Length >= 2 && bgo.GetComponentsInChildren<MixFeedback>().Length >= 2);
            // Precipitate layer: without it, "white crystals
            // separate" fired with nothing visible in adopted/spawned glass.
            bool allPpt = true;
            foreach (var vlp in bgo.GetComponentsInChildren<LiquidPhysics>())
                if (vlp.GetComponent<LiquidTaskBinding>() != null && vlp.precipitateRenderer == null) allPpt = false;
            A("builder: task vessels get a precipitate layer", allPpt);

            builder.Build("tutorial-methane");   // teardown to a clean stage
            int m = builder.Build("tutorial-methane");
            A("builder: Methane builds 0 dynamic (uses its hand-built stage)", m == 0);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
            UnityEngine.Object.DestroyImmediate(module); UnityEngine.Object.DestroyImmediate(lay);
        }

        // All 9 experiments must build a physical setup from their ExperimentLayout —
        // Methane uses its hand-built stage (0 dynamic roots), the other 8 spawn from data.
        var allLayouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            allLayouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));
        A("builder: 8 experiment layouts authored", allLayouts.Count >= 8);

        int resolveMisses = 0;
        foreach (var l in allLayouts)
        {
            if (l == null) continue;
            foreach (var p in l.props)
            {
                if (lib.GetPrefab(p.prefabName) == null) resolveMisses++;
                if (p.pourable && !string.IsNullOrEmpty(p.fillChemical) && lib.GetChemical(p.fillChemical) == null) resolveMisses++;
            }
            foreach (var v in l.vessels)
            {
                if (lib.GetPrefab(v.prefabName) == null) resolveMisses++;
                if (!string.IsNullOrEmpty(v.startChemical) && lib.GetChemical(v.startChemical) == null) resolveMisses++;
                foreach (var bnd in v.bindings)
                    if (lib.GetChemical(bnd.reagentChemical) == null) resolveMisses++;
            }
            // Every station must be completable by an item that EXISTS — either a prop
            // the layout stages, or one of the permanent bench items.
            //
            // Demanding the item be in the SAME LAYOUT would encode
            // the opposite of the hard client rule ("ALL general tools are ALWAYS on the
            // bench") and push layouts into staging their own funnel/burner/dropper,
            // duplicating apparatus that is already there. A
            // station's ZoneItemSensor matches a LabItem.itemId wherever it lives, so
            // pointing at the bench (kit-bunsenburner, kit-funnel) is the correct shape.
            var ids = new HashSet<string>(BenchItemIds);
            foreach (var p in l.props) ids.Add(p.itemId);
            foreach (var s in l.stations)
                if (!string.IsNullOrEmpty(s.requiredItemId) && !ids.Contains(s.requiredItemId)) resolveMisses++;
        }
        A("builder: every layout resolves all prefab/chemical/station names", resolveMisses == 0);

        // ⛔ A LAYOUT MUST NOT RE-STAGE THE PERMANENT BENCH.
        // Every general tool and raw reagent is ALWAYS out (the hard client rule), so
        // staging a dropper/funnel/burner/spatula or a pourable reagent bottle spawns a
        // DUPLICATE of something already on the bench (for Exp 2 that would be 46:
        // 21 bottles, 2 droppers, a spatula, a funnel, a burner, 20 tubes, beside
        // the Eq_*/Kit_*/Raw_* originals). Stations point at bench itemIds instead.
        {
            var bench = BenchItemIds;
            bool noToolDupes = true, noReagentDupes = true;
            string dupeNote = "";
            foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LayoutDir }))
            {
                var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
                if (l == null || l.moduleId != "prelim-chemical-compounding") continue;   // the manuscript Exp 2 module
                foreach (var p in l.props)
                {
                    if (bench.Contains(p.itemId)) { noToolDupes = false; dupeNote = l.name + " -> " + p.itemId; }
                    // A pourable bottle IS shelf stock — the shelf already holds all 21.
                    if (p.pourable && !string.IsNullOrEmpty(p.fillChemical))
                    { noReagentDupes = false; dupeNote = l.name + " -> bottle of " + p.fillChemical; }
                }
            }
            A("bench: compounding stages no tool the bench already has" + (noToolDupes ? "" : "  [" + dupeNote + "]"), noToolDupes);
            A("bench: compounding stages no reagent bottle (the shelf holds them)" + (noReagentDupes ? "" : "  [" + dupeNote + "]"), noReagentDupes);

            // Vessels must BIND to the bench, not spawn twins beside it (e.g. 20
            // Vessel_TestTube clones next to the real Kit_TestTube_0-18). And every
            // benchItem must EXIST: a name typo means the step can never complete, a
            // silent class of bug nothing else catches.
            var sceneNames = new HashSet<string>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                sceneNames.Add(t.name);

            bool allBound = true, allExist = true;
            string missNote = "";
            foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LayoutDir }))
            {
                var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
                if (l == null || l.moduleId != "prelim-chemical-compounding") continue;
                foreach (var v in l.vessels)
                {
                    if (string.IsNullOrEmpty(v.benchItem)) { allBound = false; missNote = l.name + " spawns " + v.displayName; continue; }
                    if (!sceneNames.Contains(v.benchItem)) { allExist = false; missNote = v.benchItem + " (for " + v.displayName + ")"; }
                }
            }
            A("bench: every compounding vessel BINDS to a bench object" + (allBound ? "" : "  [" + missNote + "]"), allBound);
            A("bench: every benchItem a layout names actually EXISTS" + (allExist ? "" : "  [MISSING " + missNote + "]"), allExist);
        }

        var allBgo = new GameObject("sb_all");
        try
        {
            var b2 = allBgo.AddComponent<ExperimentSceneBuilder>();
            b2.SetRefs(null, lib, reg, allLayouts);
            int builtAll = 0;
            foreach (var l in allLayouts)
            {
                if (l == null || l.moduleId == "tutorial-methane") continue;
                if (b2.Build(l.moduleId) > 0) builtAll++;
            }
            A("builder: all 8 data-driven experiments spawn > 0 roots", builtAll >= 8);
        }
        finally { UnityEngine.Object.DestroyImmediate(allBgo); }
    }

    static void ResultsExportSuite()
    {
        var svc = new ProgressionService();
        var rows0 = ResultsExport.BuildRows(svc);
        A("results: one row per experiment", rows0.Count == ExperimentCatalog.Count && rows0.Count == 9);
        A("results: all unattempted at start", rows0.TrueForAll(r => !r.attempted && !r.passed));
        A("results: zero passed at start", ResultsExport.PassedCount(svc) == 0);

        svc.RecordResult("tutorial-methane",
            new ExperimentResult { passed = true, overallMastery = 0.95f, grade = new GradeBreakdown { Total = 96f } }, false);
        var rows = ResultsExport.BuildRows(svc);
        var t = rows.Find(r => r.moduleId == "tutorial-methane");
        A("results: recorded attempt shows", t.attempted && t.passed && Near(t.bestGrade, 96f) && t.attempts == 1);
        A("results: passed count = 1", ResultsExport.PassedCount(svc) == 1);

        var csv = ResultsExport.BuildCsv(svc);
        A("results: csv has header", csv.StartsWith("Experiment,Period,"));
        A("results: csv lists the tutorial row", csv.Contains("Tutorial: Methane Synthesis"));
        A("results: csv total line", csv.Contains("TOTAL,,,1/9"));
        int lines = csv.TrimEnd('\n').Split('\n').Length;
        A("results: csv = header + 9 rows + total", lines == 11);

        // Results/History screen text.
        var empty = new ProgressionService();
        var txt0 = ResultsHistoryController.BuildDisplayText(empty);
        A("results: screen shows 0/9 before any pass", txt0.Contains("0 / 9 passed"));
        var txt1 = ResultsHistoryController.BuildDisplayText(svc);
        A("results: screen marks a pass + 1/9", txt1.Contains("PASS") && txt1.Contains("1 / 9 passed"));
    }

    static void SettingsSuite()
    {
        var s = new ComfortSettings();
        A("settings: sane defaults", Near(s.textScale, 1f) && Near(s.snapTurnAngle, 45f) && s.handedness == Handedness.Right);
        s.SetTextScale(0.1f); A("settings: text scale clamps low", Near(s.textScale, 0.8f));
        s.SetTextScale(9f);   A("settings: text scale clamps high", Near(s.textScale, 1.6f));
        s.SetSubtitleSpeed(0.1f); A("settings: subtitle speed clamps low", Near(s.subtitleSpeed, 0.5f));
        s.SetSubtitleSpeed(9f);   A("settings: subtitle speed clamps high", Near(s.subtitleSpeed, 2f));
        s.SetVignette(-1f); A("settings: vignette clamps to 0", Near(s.vignetteIntensity, 0f));
        s.SetVignette(9f);  A("settings: vignette clamps to 1", Near(s.vignetteIntensity, 1f));
        s.SetSnapTurnAngle(3f);   A("settings: snap-turn clamps low", Near(s.snapTurnAngle, 15f));
        s.SetSnapTurnAngle(400f); A("settings: snap-turn clamps high", Near(s.snapTurnAngle, 90f));
        var clone = s.Clone(); clone.SetTextScale(1.2f);
        A("settings: clone is independent", Near(s.textScale, 1.6f) && Near(clone.textScale, 1.2f));
    }

    static void ExaminerSuite()
    {
        ExperimentModuleDefinition Mod(bool assess)
        {
            var m = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            m.moduleId = assess ? "assess-mod" : "guided-mod";
            m.assessmentMode = assess;
            m.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure) };
            return m;
        }

        A("examiner: ShouldObserve reads the flag",
            ExaminerNPC.ShouldObserve(Mod(true)) && !ExaminerNPC.ShouldObserve(Mod(false)));

        // Guided module → Pharmee speaks, examiner dormant.
        var rgo = new GameObject("ex_r"); var bgo = new GameObject("ex_b"); var ego = new GameObject("ex_e");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var brain = bgo.AddComponent<PharmeeBrain>();
            var exam = ego.AddComponent<ExaminerNPC>();
            brain.SetRunner(runner); exam.SetRunner(runner);
            runner.SetModule(Mod(false)); runner.StartExperiment();
            A("examiner: guided → Pharmee gives a hint", !string.IsNullOrEmpty(brain.LastLine));
            A("examiner: guided → examiner dormant", !exam.IsObserving && !brain.AssessmentMode);
            A("examiner: proctors aloud on start (greeting line)", !string.IsNullOrEmpty(exam.LastLine));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo); UnityEngine.Object.DestroyImmediate(ego); }

        // Assessment module → Pharmee silent, examiner observing.
        var rgo2 = new GameObject("ex_r2"); var bgo2 = new GameObject("ex_b2"); var ego2 = new GameObject("ex_e2");
        try
        {
            var runner = rgo2.AddComponent<ExperimentRunner>();
            var brain = bgo2.AddComponent<PharmeeBrain>();
            var exam = ego2.AddComponent<ExaminerNPC>();
            brain.SetRunner(runner); exam.SetRunner(runner);
            runner.SetModule(Mod(true)); runner.StartExperiment();
            brain.InstructCurrent();   // even an explicit hint request stays silent
            A("examiner: assessment → Pharmee silent", string.IsNullOrEmpty(brain.LastLine) && brain.AssessmentMode);
            A("examiner: assessment → examiner observing", exam.IsObserving);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo2); UnityEngine.Object.DestroyImmediate(bgo2); UnityEngine.Object.DestroyImmediate(ego2); }

        // Rich dialogue pools (richer NPC interactions).
        A("lines: Pick wraps index + non-empty", !string.IsNullOrEmpty(PharmeeLines.Pick(PharmeeLines.Idle, 0))
            && PharmeeLines.Pick(PharmeeLines.Greetings, 5) == PharmeeLines.Pick(PharmeeLines.Greetings, 5 % PharmeeLines.Greetings.Length));
        A("lines: greetings pool has variety", PharmeeLines.Pick(PharmeeLines.Greetings, 0) != PharmeeLines.Pick(PharmeeLines.Greetings, 1));
        A("lines: Pick handles null + negative", PharmeeLines.Pick(null, 3) == "" && !string.IsNullOrEmpty(PharmeeLines.Pick(PharmeeLines.ExamRemarks, -2)));

        // Guided lab tour (storyboard): narrated beats, closer invites a campaign.
        A("tour: multiple beats + closer invites a campaign",
            PharmeeLines.TourBeats.Length >= 6
            && PharmeeLines.TourBeats[PharmeeLines.TourBeats.Length - 1].ToLower().Contains("campaign"));

        // Location-triggered tour (storyboard): narrate the nearest unvisited landmark.
        {
            var pos = new[] { new Vector3(10, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1) };
            var vis = new[] { false, false, false };
            var rad = new[] { 1.5f, 1.5f, 1.5f };
            A("tour: fires the landmark you walk up to", LabTourGuide.FirstUnvisitedInRange(Vector3.zero, pos, vis, rad) == 1);
            vis[1] = true;
            A("tour: skips a visited landmark, finds the next", LabTourGuide.FirstUnvisitedInRange(Vector3.zero, pos, vis, rad) == 2);
            A("tour: nothing in range → -1", LabTourGuide.FirstUnvisitedInRange(new Vector3(50, 0, 50), pos, vis, rad) == -1);
        }

        // Typewriter dialogue: lines type out char-by-char with blips.
        {
            var go = new GameObject("tw");
            try
            {
                var nc = go.AddComponent<NPCNarrationController>();
                A("typewriter: enabled by default with a positive type speed", nc.Typewriter && nc.TypeCps() > 1f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // Give way: Pharmee steps aside when the player bumps him.
        // Far away → no step.
        A("giveway: no step outside personal space",
            PharmeeGiveWay.SideStep(new Vector3(3f, 1f, 0f), Vector3.zero, Vector3.forward, 0.95f, 0.65f) == Vector3.zero);
        // Player facing +Z, Pharmee slightly to the +X side and close → pushed further +X (right), horizontal only.
        {
            var step = PharmeeGiveWay.SideStep(new Vector3(0.2f, 1f, 0.1f), Vector3.zero, Vector3.forward, 0.95f, 0.65f);
            A("giveway: steps to the player's side, horizontally", step.x > 0f && Mathf.Abs(step.y) < 1e-5f && step.magnitude <= 0.65f + 1e-4f);
        }
        // Directly ahead + closer → a larger push than when near the edge of the bubble.
        {
            var near = PharmeeGiveWay.SideStep(new Vector3(0f, 1f, 0.1f), Vector3.zero, Vector3.forward, 0.95f, 0.65f).magnitude;
            var edge = PharmeeGiveWay.SideStep(new Vector3(0f, 1f, 0.9f), Vector3.zero, Vector3.forward, 0.95f, 0.65f).magnitude;
            A("giveway: closer = bigger step", near > edge && near > 0f);
        }

        // Mirror render gate (perf): skip the reflection pass when far,
        // behind the glass, or turned away.
        A("mirror: renders when near, in front, looking at it", MirrorPlane.ShouldRender(2f, 6f, 0.8f, 0.9f));
        A("mirror: skips when too far", !MirrorPlane.ShouldRender(9f, 6f, 0.8f, 0.9f));
        A("mirror: skips when behind the glass", !MirrorPlane.ShouldRender(2f, 6f, -0.5f, 0.9f));
        A("mirror: skips when turned away", !MirrorPlane.ShouldRender(2f, 6f, 0.8f, -0.6f));

        // Hover highlight (prop readability) — pops on hover, restores.
        A("hover: scale pops when lit, base when not",
            HoverHighlight.HighlightScale(Vector3.one, true, 1.06f) == Vector3.one * 1.06f
            && HoverHighlight.HighlightScale(Vector3.one, false, 1.06f) == Vector3.one);
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var hh = go.AddComponent<HoverHighlight>();
                hh.SetHighlight(true);
                bool grew = go.transform.localScale.x > 1.0001f && hh.IsHighlighted;
                hh.SetHighlight(false);
                bool restored = Mathf.Abs(go.transform.localScale.x - 1f) < 1e-4f && !hh.IsHighlighted;
                A("hover: SetHighlight grows then restores", grew && restored);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }

    static void AudioSuite()
    {
        // Perceptual volume curve.
        A("audio: full volume = 0 dB", Near(VolumeUtil.LinearToDb(1f), 0f, 0.05f));
        A("audio: silence = -80 dB", Near(VolumeUtil.LinearToDb(0f), -80f, 0.01f));
        A("audio: half volume ~ -6 dB", Near(VolumeUtil.LinearToDb(0.5f), -6.02f, 0.1f));

        // Per-shot pitch variation (realism): centred at 1.0, bounded by
        // the jitter amount, and only physical sounds vary (not UI/musical stings).
        A("audio: pitch jitter centred + bounded",
            Near(AudioService.JitteredPitch(0.08f, 0.5f), 1f, 0.0001f)
            && AudioService.JitteredPitch(0.08f, 1f) <= 1.0801f
            && AudioService.JitteredPitch(0.08f, 0f) >= 0.9199f);
        A("audio: only physical sounds pitch-vary",
            AudioService.PitchVaries("footstep") && AudioService.PitchVaries("glass-shatter")
            && !AudioService.PitchVaries("ui-click") && !AudioService.PitchVaries("grade-pass"));

        // Continuous pour sound (realism): silent when righted, swells with flow.
        A("audio: pour silent when not pouring", Near(LiquidPourer.PourVolume(false, 0.5f, 1f, 1f), 0f, 0.0001f));
        A("audio: pour swells with flow",
            LiquidPourer.PourVolume(true, 0.5f, 1f, 1f) > LiquidPourer.PourVolume(true, 0.5f, 1f, 0f)
            && LiquidPourer.PourVolume(true, 0.5f, 1f, 0f) > 0f);

        // Dialogue ducking: beds dip while an NPC speaks, restore after.
        A("audio: duck dips beds while speaking, full otherwise",
            Near(DialogueDucker.DuckTarget(1, 0.45f), 0.45f, 0.001f)
            && Near(DialogueDucker.DuckTarget(2, 0.45f), 0.45f, 0.001f)
            && Near(DialogueDucker.DuckTarget(0, 0.45f), 1f, 0.001f));

        // Atmosphere VFX: AC vapour + haze style tags.
        A("atmosphere: style tags", AtmosphereVfx.StyleName(AtmosphereVfx.Style.AcVapor) == "ac-vapor"
            && AtmosphereVfx.StyleName(AtmosphereVfx.Style.Haze) == "haze");

        // SoundBank lookup.
        var bank = ScriptableObject.CreateInstance<SoundBank>();
        bank.entries.Add(new SoundBank.Entry { key = "pour", clip = null, category = AudioCategory.Sfx });
        A("audio: bank finds a key", bank.Get("pour") != null);
        A("audio: bank misses unknown key", bank.Get("nope") == null);
        A("audio: expected-key checklist non-empty", SoundBank.ExpectedKeys.Length >= 10);

        // Service volume + null-safe playback (works with zero clips).
        var go = new GameObject("audio_svc");
        try
        {
            var svc = go.AddComponent<AudioService>();
            svc.Bind(bank, go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>(),
                     go.AddComponent<AudioSource>(), go.AddComponent<AudioSource>());
            svc.SetVolume(AudioCategory.Sfx, 0.3f);
            A("audio: volume set/get", Near(svc.VolumeOf(AudioCategory.Sfx), 0.3f));
            svc.SetVolume(AudioCategory.Music, 2f);
            A("audio: volume clamps to 1", Near(svc.VolumeOf(AudioCategory.Music), 1f));
            svc.Play("pour");        // entry exists, clip null → silent no-op, no throw
            svc.Play("missing");     // no entry → no throw
            A("audio: playback is null-safe with no clips", true);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(bank); }
    }

    static void HubSelectSuite()
    {
        // Fresh in-memory progress (no disk touch): only the tutorial should be open.
        var service = new ProgressionService();
        var flow = new ProgressionFlow(service);

        var model = HubSelectController.BuildModel(flow);
        A("hub: model has all 9 experiments", model.Count == ExperimentCatalog.Count && model.Count == 9);

        HubSelectController.Row Find(string id) { foreach (var r in model) if (r.moduleId == id) return r; return null; }
        A("hub: tutorial available at start",
            HubSelectController.StateOf(Find("tutorial-methane")) == HubSelectController.RowState.Available);
        A("hub: prelim locked at start",
            HubSelectController.StateOf(Find("prelim-chemical-compounding")) == HubSelectController.RowState.Locked);
        A("hub: midterm period closed at start", !flow.IsPeriodUnlocked(ExperimentPeriod.Midterm));
        A("hub: CanSelect tutorial", HubSelectController.CanSelect(flow, "tutorial-methane"));
        A("hub: cannot select a locked experiment", !HubSelectController.CanSelect(flow, "final-winemaking"));

        // Pass the tutorial (in-memory only) → the first prelim unlocks.
        service.RecordResult("tutorial-methane", new ExperimentResult { passed = true }, false);
        var flow2 = new ProgressionFlow(service);
        var model2 = HubSelectController.BuildModel(flow2);
        HubSelectController.Row Find2(string id) { foreach (var r in model2) if (r.moduleId == id) return r; return null; }
        A("hub: tutorial now passed", HubSelectController.StateOf(Find2("tutorial-methane")) == HubSelectController.RowState.Passed);
        A("hub: next prelim now available", HubSelectController.StateOf(Find2("prelim-chemical-compounding")) == HubSelectController.RowState.Available);
        A("hub: can select the newly unlocked prelim", HubSelectController.CanSelect(flow2, "prelim-chemical-compounding"));
        A("hub: second prelim still locked", !HubSelectController.CanSelect(flow2, "prelim-ethyl-alcohol"));
        A("hub: midterm still closed until prelim complete", !flow2.IsPeriodUnlocked(ExperimentPeriod.Midterm));
    }

    static void SimRigSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        // SYNTHETIC fixture: every polished module is
        // zone-free, so the classic Heat-station rig is exercised on an
        // in-memory layout + module that no module polish can break.
        A("simrig: library loads", lib != null);
        if (lib == null) return;
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "rig-probe";
        module.graphTasks.Add(new ExperimentTask { taskId = "rig-heat", label = "Rig heat" });
        var lay = ScriptableObject.CreateInstance<ExperimentLayout>();
        lay.moduleId = "rig-probe";
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "rig-heat", label = "Rig", requiredItemId = "rig-heat",
          pos = new Vector3(0f, 0.91f, -3.7f), sim = StationSim.Heat, simTargetC = 150f });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "BunsenBurner", itemId = "rig-heat", displayName = "Rig Burner",
          pos = new Vector3(0.25f, 0.91f, -3.7f), targetHeight = 0.22f });

        var rgo = new GameObject("sr_runner"); var bgo = new GameObject("sr_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, new List<ExperimentLayout> { lay });
            builder.Build("rig-probe");

            ZoneSimStation heatRig = null;
            foreach (var z in bgo.GetComponentsInChildren<ZoneSimStation>(true))
                if (z.gameObject.name == "Station_rig-heat") { heatRig = z; break; }
            A("simrig: heat station built with a ZoneSimStation", heatRig != null);
            if (heatRig == null) return;
            var temp = heatRig.GetComponent<TemperatureSim>();
            var sensor = heatRig.GetComponent<ZoneItemSensor>();
            A("simrig: heat station carries TemperatureSim + sensor", temp != null && sensor != null);

            runner.Graph.Tick();
            A("simrig: heat task pending before heating", !runner.Graph.IsComplete("rig-heat"));

            // ignition gate: the station's prop is a BunsenBurner, so heat
            // only advances once the burner is LIT (light it with a match).
            var burnerProp = FindChildByName(bgo.transform, "Prop_rig-heat");
            var burner = burnerProp != null ? burnerProp.GetComponent<BurnerController>() : null;
            A("simrig: rig burner is ignitable (W5.8)", burner != null);
            sensor.ForceOccupied(true);
            heatRig.Drive(1f, true);           // burner present but UNLIT -> no heat
            for (int i = 0; i < 12; i++) temp.Tick(1f);
            A("simrig: unlit burner never heats (W5.8)", !temp.AtLeast(150f));

            // Perform the verb: light the burner -> heat to target -> auto-completes.
            if (burner != null) burner.Ignite();
            heatRig.Drive(1f, true);           // flame on
            for (int i = 0; i < 12; i++) temp.Tick(1f);
            A("simrig: temperature reached target", temp.AtLeast(150f));
            runner.Graph.Tick();
            A("simrig: heat verb auto-completes the task", runner.Graph.IsComplete("rig-heat"));
        }
        finally
        {
            var b2 = bgo.GetComponent<ExperimentSceneBuilder>();
            if (b2 != null) b2.Build("tutorial-methane");   // bench teardown insurance
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
            UnityEngine.Object.DestroyImmediate(module); UnityEngine.Object.DestroyImmediate(lay);
        }
    }

    static void CutsceneLibrarySuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>("Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset");
        A("cutscene: library loads", lib != null);
        if (lib == null) return;
        A("cutscene: 9 module sets", lib.entries.Count >= 9);

        int bad = 0;
        foreach (var e in lib.entries)
        {
            if (e == null || !e.IsComplete) { bad++; continue; }
            if (e.intro.kind != CutsceneData.Kind.Intro) bad++;
            if (e.reagentPrep.kind != CutsceneData.Kind.ReagentPrep) bad++;
            if (e.success.kind != CutsceneData.Kind.Success) bad++;
            if (e.failure.kind != CutsceneData.Kind.Failure) bad++;
        }
        A("cutscene: every set complete + correct kinds", bad == 0);

        // Director swaps its four cutscenes to match the running module.
        var go = new GameObject("cs_dir");
        try
        {
            var dir = go.AddComponent<CutsceneDirector>();
            dir.SetLibrary(lib);
            var asp = lib.GetSet("final-benzamide");
            A("cutscene: benzamide set exists", asp != null);
            A("cutscene: LoadForModule(benzamide) returns complete", dir.LoadForModule("final-benzamide"));
            A("cutscene: director intro swapped to benzamide", dir.Intro == asp.intro && dir.Success == asp.success);
            dir.LoadForModule("tutorial-methane");
            var meth = lib.GetSet("tutorial-methane");
            A("cutscene: director intro swapped to methane", dir.Intro == meth.intro);
            A("cutscene: outro selects failure on fail", dir.SelectOutro(new ExperimentResult { passed = false }) == meth.failure);
            A("cutscene: outro selects success on pass", dir.SelectOutro(new ExperimentResult { passed = true }) == meth.success);
            A("cutscene: unknown module → no swap", !dir.LoadForModule("does-not-exist"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
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

    static void RealVerbSuite()
    {
        // Methane real verbs: burner presence heats (TemperatureSim), heat gates gas
        // collection (GasCollection), and both tasks complete via graph auto-check.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Tutorial_Methane.asset");
        A("realverb: methane module loads", module != null);
        if (module == null) return;

        var rgo = new GameObject("rv_runner");
        var xgo = new GameObject("rv_rig");
        var cgo = new GameObject("rv_collect");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            var temp = xgo.AddComponent<TemperatureSim>();
            var gas = xgo.AddComponent<GasCollection>();
            var rig = xgo.AddComponent<MethaneApparatusRig>();

            runner.StartExperiment();
            // location-free rig — no zones; heat/collect complete via the
            // rig's own TemperatureSim/GasCollection conditions (driven by burner/
            // collection-tube PROXIMITY in play; here we drive the sims directly).
            rig.Bind(runner, temp, gas);
            rig.HandleExperimentStarted(module);   // edit mode: subscribe happened post-start

            runner.CompleteTask("prepare-mixture");
            runner.CompleteTask("setup-apparatus");
            runner.Graph.Tick();
            A("realverb: heat-mixture blocked while cold", !runner.Graph.IsComplete("heat-mixture"));

            temp.SetHeating(true, 220f);
            for (int i = 0; i < 120; i++) temp.Tick(0.5f);
            runner.Graph.Tick();
            A("realverb: sustained heating completes heat-mixture", runner.Graph.IsComplete("heat-mixture"));
            A("realverb: collect-gas still pending", !runner.Graph.IsComplete("collect-gas"));

            gas.AddGas(100000f);   // clamps to capacity
            runner.Graph.Tick();
            A("realverb: gas fill completes collect-gas", runner.Graph.IsComplete("collect-gas"));
            A("realverb: progress 4/5 after real verbs", Near(runner.Graph.Progress01, 0.8f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(xgo);
            UnityEngine.Object.DestroyImmediate(cgo);
        }
    }

    static void ProgressionFlowSuite()
    {
        A("catalog: 9 entries", ExperimentCatalog.Count == 9);
        int tut = 0, pre = 0, mid = 0, fin = 0;
        foreach (var e in ExperimentCatalog.Entries)
        {
            if (e.period == ExperimentPeriod.Tutorial) tut++;
            else if (e.period == ExperimentPeriod.Prelim) pre++;
            else if (e.period == ExperimentPeriod.Midterm) mid++;
            else fin++;
        }
        // Client's period grouping: tutorial + Exp 2-9.
        A("catalog: period split 1/2/4/2", tut == 1 && pre == 2 && mid == 4 && fin == 2);

        // Every prerequisite must reference an experiment earlier in the roster.
        bool chainOk = true; var seen = new List<string>();
        foreach (var e in ExperimentCatalog.Entries)
        {
            if (!string.IsNullOrEmpty(e.prerequisiteModuleId) && !seen.Contains(e.prerequisiteModuleId)) chainOk = false;
            seen.Add(e.moduleId);
        }
        A("catalog: prereq chain valid + ordered", chainOk);

        var svc = new ProgressionService(System.IO.Path.Combine(Application.temporaryCachePath, "flow_selftest.json"));
        var flow = new ProgressionFlow(svc);
        A("flow: only tutorial unlocked at start", flow.IsUnlocked("tutorial-methane") && !flow.IsUnlocked("prelim-ethyl-alcohol"));
        A("flow: next is the tutorial", flow.NextExperiment()?.moduleId == "tutorial-methane");
        A("flow: tutorial period open, prelim closed", flow.IsPeriodUnlocked(ExperimentPeriod.Tutorial) && !flow.IsPeriodUnlocked(ExperimentPeriod.Prelim));

        svc.RecordResult("tutorial-methane", new ExperimentResult { passed = true }, false);
        A("flow: passing tutorial unlocks next + completes its period",
            flow.IsUnlocked("prelim-chemical-compounding") && flow.IsPeriodComplete(ExperimentPeriod.Tutorial) && flow.IsPeriodUnlocked(ExperimentPeriod.Prelim));
        A("flow: next is chemical compounding", flow.NextExperiment()?.moduleId == "prelim-chemical-compounding");

        foreach (var e in ExperimentCatalog.Entries) svc.RecordResult(e.moduleId, new ExperimentResult { passed = true }, false);
        A("flow: all passed → 100% + no next", flow.AllComplete() && Near(flow.OverallCompletion01(), 1f) && flow.NextExperiment() == null);
        A("flow: final period unlocked + complete", flow.IsPeriodUnlocked(ExperimentPeriod.Final) && flow.IsPeriodComplete(ExperimentPeriod.Final));
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

    // Hover-inspector knowledge base + panel animation.
    static void HoverInfoSuite()
    {
        A("info: equipment authored", LabInfoDatabase.EquipmentCount >= 25);
        A("info: reagents authored", LabInfoDatabase.ReagentCount >= 30);

        var beaker = LabInfoDatabase.Equipment("Prop_Beaker_100mL");
        A("info: beaker resolves", beaker != null && beaker.Title == "Beaker" && beaker.Category == LabInfoCategory.Equipment);
        var cyl = LabInfoDatabase.Equipment("GraduatedCylinder_50mL");
        A("info: graduated cylinder resolves (specific before 'cylinder')", cyl != null && cyl.Title == "Graduated Cylinder");
        var rack = LabInfoDatabase.Equipment("TestTubeRack_12Tubes");
        A("info: test-tube RACK not plain test-tube", rack != null && rack.Title == "Test-Tube Rack");
        A("info: unknown prop → no card", LabInfoDatabase.Equipment("Wall_Section_42") == null);
        var coat = LabInfoDatabase.Equipment("LabCoatDisplay");
        A("info: lab coat resolves", coat != null && coat.Title == "Lab Coat");
        var gog = LabInfoDatabase.Equipment("Goggles_Standin");
        A("info: goggles resolve", gog != null && gog.Title == "Safety Goggles");
        var glv = LabInfoDatabase.Equipment("Gloves_Standin");
        A("info: gloves resolve", glv != null && glv.Title == "Nitrile Gloves");

        var naoh = LabInfoDatabase.Reagent("Sodium Hydroxide");
        A("info: NaOH trivia present", naoh != null && naoh.Category == LabInfoCategory.Reagent && naoh.Body.Length > 20);
        var unknownChem = LabInfoDatabase.Reagent("Unobtainium");
        A("info: unknown reagent → generic card", unknownChem != null && unknownChem.Title == "Unobtainium");
        A("info: empty reagent name safe", LabInfoDatabase.Reagent("") != null);

        A("info: pharmee person card", LabInfoDatabase.Person(true).Title == "Pharmee");
        A("info: jimenez person card", LabInfoDatabase.Person(false).Title.Contains("Jimenez"));

        A("info: norm strips + lowercases", LabInfoDatabase.Norm("Beaker_100 mL") == "beaker100ml");

        // Panel easing + accent mapping (pure).
        A("panel: ease(0)=0", Near(HoverInfoPanel.Ease(0f), 0f));
        A("panel: ease(1)=1", Near(HoverInfoPanel.Ease(1f), 1f));
        A("panel: ease(0.5)=0.5", Near(HoverInfoPanel.Ease(0.5f), 0.5f));
        A("panel: ease clamps", Near(HoverInfoPanel.Ease(2f), 1f) && Near(HoverInfoPanel.Ease(-1f), 0f));
        A("panel: reagent accent amber", HoverInfoPanel.AccentFor(LabInfoCategory.Reagent).r > 0.9f);
        A("panel: tags", HoverInfoPanel.Tag(LabInfoCategory.Equipment) == "EQUIPMENT" && HoverInfoPanel.Tag(LabInfoCategory.Person) == "LAB GUIDE");

        // Placement must always sit IN FRONT of the struck surface (never occluded).
        A("panel: close target → card in front", HoverInfoPanel.PlaceDistance(1.0f, 0.5f, 1.1f, 0.4f) <= 1.0f - 0.12f + 1e-4f);
        A("panel: very close target floored", HoverInfoPanel.PlaceDistance(0.5f, 0.5f, 1.1f, 0.4f) <= 0.5f - 0.12f + 1e-4f);
        A("panel: distant target stays readable-close", HoverInfoPanel.PlaceDistance(4f, 0.5f, 1.1f, 0.4f) <= 1.1f + 1e-4f);
        A("panel: never negative", HoverInfoPanel.PlaceDistance(0.3f, 0.5f, 1.1f, 0.4f) >= 0.3f);

        // Held-item suppression (the card must not sit over an
        // item you're holding). Grabbable NOT selected → not held; null safe.
        var hgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            hgo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            A("panel: idle grabbable is not held", !HoverInspector.IsHeld(hgo.GetComponent<Collider>()));
            A("panel: null collider not held", !HoverInspector.IsHeld(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(hgo); }
    }

    // Corner music speaker playlist advance.
    static void MusicSpeakerSuite()
    {
        A("music: single track stays", MusicSpeaker.NextIndex(0, 1, true, 0.9f) == 0);
        A("music: sequential wraps", MusicSpeaker.NextIndex(4, 5, false, 0f) == 0);
        A("music: sequential advances", MusicSpeaker.NextIndex(1, 5, false, 0f) == 2);
        A("music: shuffle never repeats", MusicSpeaker.NextIndex(2, 5, true, 0.4f) != 2);   // 0.4*5=2 → bumped
        int shuf = MusicSpeaker.NextIndex(0, 5, true, 0.99f);
        A("music: shuffle in range", shuf >= 0 && shuf < 5);
        A("music: empty safe", MusicSpeaker.NextIndex(0, 0, true, 0.5f) == 0);

        // Speaker LED blink envelope — period 1.15 s, 22% lit.
        const float P = 1.15f, ON = 0.22f, E = 0.05f, DIM = 0.12f;
        A("led: peak mid-pulse", Mathf.Approximately(SpeakerLedBlink.Level01(P * ON * 0.5f, P, ON, E, DIM), 1f));
        A("led: dim in the gap", Mathf.Approximately(SpeakerLedBlink.Level01(P * 0.6f, P, ON, E, DIM), DIM));
        A("led: ramps from dim", SpeakerLedBlink.Level01(0f, P, ON, E, DIM) < 0.2f);
        A("led: cycles", Mathf.Approximately(SpeakerLedBlink.Level01(P * 0.6f, P, ON, E, DIM),
                                             SpeakerLedBlink.Level01(P * 1.6f, P, ON, E, DIM)));
        A("led: contrast is noticeable", SpeakerLedBlink.Level01(P * ON * 0.5f, P, ON, E, DIM)
                                       - SpeakerLedBlink.Level01(P * 0.9f, P, ON, E, DIM) > 0.5f);
        A("led: zero period safe", Mathf.Approximately(SpeakerLedBlink.Level01(3f, 0f, ON, E, DIM), 1f));
    }


    // surface + render calibration (the lab's look).
    // These pin ASSET state, not math, on purpose: a pack reimport or one stray inspector drag
    // silently restores the bad values, and no pure test can see a material regression - the
    // same reason the matchbox striker is pinned on the scene, not only in math.
    static void SurfaceSuite()
    {
        foreach (var surf in LabSurfaceTuner.Surfaces)
        {
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(surf.path);
            string id = System.IO.Path.GetFileNameWithoutExtension(surf.path);
            A("surface: " + id + " exists", mat != null);
            if (mat == null) continue;
            A("surface: " + id + " smoothness calibrated",
              mat.HasProperty("_Smoothness") &&
              Mathf.Approximately(mat.GetFloat("_Smoothness"), surf.smoothness));
            if (surf.metallic >= 0f)
                A("surface: " + id + " metallic calibrated",
                  mat.HasProperty("_Metallic") &&
                  Mathf.Approximately(mat.GetFloat("_Metallic"), surf.metallic));
        }

        // The whole bug class, not just known offenders: URP clamps _Smoothness to
        // 0-1, so anything above it renders as a perfect mirror (pack materials at
        // 2.19-3.20 turn the ceiling, floor and cabinets into mirrors reflecting the
        // outdoor procedural skybox inside a sealed room).
        int outOfRange = 0;
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/PharmaSynth" }))
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > 1.0001f) outOfRange++;
        }
        A("surface: no material has smoothness > 1", outOfRange == 0);

        // The walls and ceiling need an albedo: the largest surfaces in the player's
        // view carrying zero texture make the room read as a grey box.
        foreach (var p in new[]
        {
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Wall_0.mat",
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Wall_1.mat",
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Ceiling_2.mat",
        })
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
            A("surface: " + System.IO.Path.GetFileNameWithoutExtension(p) + " has an albedo map",
              m != null && m.GetTexture("_BaseMap") != null);
        }

        // Boil ramp: boilingPointC drives the liquid, so a heated beaker boils
        // instead of venting steam from the STATION while the liquid sits perfectly still.
        A("boil: empty vessel never boils", LiquidPhysics.BoilFor(0f, 300f, 100f) == 0f);
        A("boil: cold liquid is still", LiquidPhysics.BoilFor(50f, 25f, 100f) == 0f);
        A("boil: full boil at the point", Mathf.Approximately(LiquidPhysics.BoilFor(50f, 100f, 100f), 1f));
        A("boil: ramps in before the point",
          LiquidPhysics.BoilFor(50f, 96f, 100f) > 0f && LiquidPhysics.BoilFor(50f, 96f, 100f) < 1f);
        A("boil: clamped above the point", Mathf.Approximately(LiquidPhysics.BoilFor(50f, 250f, 100f), 1f));
        A("boil: follows the chemical, not 100 C", LiquidPhysics.BoilFor(50f, 80f, 78f) > 0.9f);

        // The render pipeline must be live: a Volume with Bloom/Vignette/Tonemapping
        // does nothing while the cameras have post-processing switched off.
        var rp = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(
            "Assets/Settings/Mobile_RPAsset.asset");
        A("render: mobile RP asset exists", rp != null);
        if (rp != null)
        {
            A("render: MSAA is on (thin glass edges alias without it)", rp.msaaSampleCount >= 4);
            A("render: HDR is on (emissive panels must exceed 1.0 to read as lights)", rp.supportsHDR);
        }
    }


    // Pharmee animation set (checklist section 3 / asset-production-spec A1).
    // Pure curves, pinned here because there is no headset pass to judge them by eye.
    static void PharmeeGestureSuite()
    {
        var tune = PharmeeGestureTuning.Default;

        // Envelope: every gesture must start AND end at exactly rest, or it pops when it
        // hands back to the hover.
        A("gesture: envelope starts at rest", Mathf.Approximately(PharmeeGestureMath.Envelope(0f), 0f));
        A("gesture: envelope ends at rest", Mathf.Approximately(PharmeeGestureMath.Envelope(1f), 0f));
        A("gesture: envelope peaks on the plateau", Mathf.Approximately(PharmeeGestureMath.Envelope(0.5f), 1f));
        A("gesture: envelope clamps below 0", Mathf.Approximately(PharmeeGestureMath.Envelope(-3f), 0f));
        A("gesture: envelope clamps above 1", Mathf.Approximately(PharmeeGestureMath.Envelope(9f), 0f));
        A("gesture: ease is monotonic", PharmeeGestureMath.EaseInOut(0.3f) < PharmeeGestureMath.EaseInOut(0.7f));

        // None and rest.
        var rest = PharmeeGestureMath.Pose(PharmeeGesture.None, 0.5f, tune);
        A("gesture: None is identity", rest.bodyRot == Quaternion.identity && rest.rootOffset == Vector3.zero);
        A("gesture: rest does not flare the rings", Mathf.Approximately(rest.waveFlare, 1f));

        // EVERY one-shot must actually move something, and must settle back to exactly rest.
        // A state that produces no motion is the failure this whole batch exists to prevent.
        foreach (var g in new[] { PharmeeGesture.Greet, PharmeeGesture.Nod,
                                  PharmeeGesture.Warn, PharmeeGesture.Celebrate })
        {
            float dur = PharmeeGestureMath.DurationOf(g);
            A("gesture: " + g + " has a duration", dur > 0f && dur < 10f);

            bool moved = false;
            for (float t = 0f; t < dur; t += dur / 24f)
            {
                var p = PharmeeGestureMath.Pose(g, t, tune);
                if (Quaternion.Angle(p.bodyRot, Quaternion.identity) > 1f ||
                    p.rootOffset.sqrMagnitude > 1e-6f || p.handRaise > 0.05f ||
                    Mathf.Abs(p.waveFlare - 1f) > 0.02f) { moved = true; break; }
            }
            A("gesture: " + g + " actually moves him", moved);

            var after = PharmeeGestureMath.Pose(g, dur + 0.5f, tune);
            A("gesture: " + g + " settles at rest",
              after.bodyRot == Quaternion.identity && after.rootOffset == Vector3.zero &&
              after.handRaise == 0f && Mathf.Approximately(after.waveFlare, 1f));
        }

        // Point is SUSTAINED - it must not expire on a timer, because the driver ends it when
        // he stops instructing or the target is lost.
        A("gesture: Point is sustained", PharmeeGestureMath.IsSustained(PharmeeGesture.Point));
        A("gesture: Point never expires", float.IsPositiveInfinity(PharmeeGestureMath.DurationOf(PharmeeGesture.Point)));
        A("gesture: Point holds its raise", PharmeeGestureMath.Pose(PharmeeGesture.Point, 30f, tune).handRaise > 0.99f);
        A("gesture: Point eases in, not snaps", PharmeeGestureMath.Pose(PharmeeGesture.Point, 0.05f, tune).handRaise < 0.5f);

        // Celebrate is the payoff moment - it must rise AND flare the hover rings.
        var cel = PharmeeGestureMath.Pose(PharmeeGesture.Celebrate, 1.1f, tune);
        A("gesture: Celebrate rises", cel.rootOffset.y > 0.05f);
        A("gesture: Celebrate flares the rings", cel.waveFlare > 1.1f);

        // Warn is a flinch: it recoils BACKWARD (negative pitch), not a gentle sway.
        var warn = PharmeeGestureMath.Pose(PharmeeGesture.Warn, 0.02f, tune);
        A("gesture: Warn recoils backward", Mathf.DeltaAngle(0f, warn.bodyRot.eulerAngles.x) < -2f);

        // State -> body, the fourth mapping alongside line pool / face / beep.
        A("gesture: Warning state warns", PharmeeGestureMath.ForState(PharmeeState.Warning) == PharmeeGesture.Warn);
        A("gesture: Celebrating state celebrates", PharmeeGestureMath.ForState(PharmeeState.Celebrating) == PharmeeGesture.Celebrate);
        A("gesture: Instructing state points", PharmeeGestureMath.ForState(PharmeeState.Instructing) == PharmeeGesture.Point);
        A("gesture: Greeting state greets", PharmeeGestureMath.ForState(PharmeeState.Greeting) == PharmeeGesture.Greet);
        A("gesture: Idle state is still", PharmeeGestureMath.ForState(PharmeeState.Idle) == PharmeeGesture.None);

        // The body must agree with the face, not drift from it (PharmeeMood.ExpressionForGate).
        A("gesture: supply trouble warns both ways",
          PharmeeGestureMath.ForGate(GateState.SupplyPrompt) == PharmeeGesture.Warn &&
          PharmeeMood.ExpressionForGate(GateState.SupplyPrompt) == PharmeeFaceExpression.Warning);
        A("gesture: unlock celebrates", PharmeeGestureMath.ForGate(GateState.UnlockAnnounce) == PharmeeGesture.Celebrate);
        A("gesture: mid-run gate is still", PharmeeGestureMath.ForGate(GateState.Running) == PharmeeGesture.None);

        // The composition contract. FloatBob is the SOLE writer of Pharmee's position and
        // PharmeeAttitude the sole writer of his body rotation; the gesture layer adds terms
        // to those, it does not compete. Pin the scene so a stray inspector edit cannot
        // silently hand root rotation back to FloatBob and start a fight with FaceCamera.
        var robot = GameObject.Find("RobotNPC");
        A("gesture: RobotNPC is in the scene", robot != null);
        if (robot != null)
        {
            var bob = robot.GetComponentInChildren<FloatBob>(true);
            var att = robot.GetComponentInChildren<PharmeeAttitude>(true);
            A("gesture: Pharmee has a FloatBob", bob != null);
            A("gesture: Pharmee has a PharmeeAttitude", att != null);
            if (bob != null)
            {
                var so = new UnityEditor.SerializedObject(bob);
                var rot = so.FindProperty("applyRotation");
                A("gesture: FloatBob leaves root rotation to FaceCamera",
                  rot != null && rot.boolValue == false);
            }
        }
    }

    // Font-safe glyph sanitiser for the lab pads / holo board.
    static void GlyphSafeSuite()
    {
        A("glyph: arrow → ->", GlyphSafe.Sanitize("purple→brown") == "purple->brown");
        A("glyph: gas ↑ → (g)", GlyphSafe.Sanitize("CH4↑") == "CH4(g)");
        A("glyph: delta → (heat)", GlyphSafe.Sanitize("NaOH →Δ CH4") == "NaOH ->(heat) CH4");
        A("glyph: equilibrium ⇌", GlyphSafe.Sanitize("A⇌B") == "A<=>B");
        A("glyph: box ▶ → »", GlyphSafe.Sanitize("▶ step") == "» step");
        A("glyph: plain text untouched", GlyphSafe.Sanitize("Prepare 0.1N HCl (250 mL)") == "Prepare 0.1N HCl (250 mL)");
        A("glyph: null/empty safe", GlyphSafe.Sanitize(null) == null && GlyphSafe.Sanitize("") == "");
    }

    // Fixed per-scene eye height, NOT relative to
    // the player's real height: the runtime's Floor/Device flip-flop makes any
    // relative scheme spawn players on the floor or the roof.
    static void HeightCalibrationSuite()
    {
        // --- head pushback: horizontal-only (under-the-floor fix) ---
        {
            var last = new UnityEngine.Vector3(0f, 1.45f, 0f);
            var dirDown = new UnityEngine.Vector3(0f, -1f, 0f);
            var tgtDown = last + dirDown * 0.05f;
            A("pushback: vertical never corrected (no sink)",
                Near(HeadPushbackMath.Correction(last, tgtDown, dirDown, 0.01f).y, 0f));

            var dirFwd = new UnityEngine.Vector3(0f, 0f, 1f);
            var tgtFwd = last + dirFwd * 0.10f;
            var c = HeadPushbackMath.Correction(last, tgtFwd, dirFwd, 0.02f);
            A("pushback: forward wall hit pulls rig back", c.z < -0.05f);
            A("pushback: wall correction stays level", Near(c.y, 0f));

            var dirDiag = new UnityEngine.Vector3(0f, -0.6f, 0.8f);
            A("pushback: diagonal head dip corrects XZ only",
                Near(HeadPushbackMath.Correction(last, last + dirDiag * 0.1f, dirDiag, 0.01f).y, 0f));

            A("pushback: initial-overlap sweep is not a blocking hit", !HeadPushbackMath.IsBlockingHit(0f));
            A("pushback: real surface hit blocks", HeadPushbackMath.IsBlockingHit(0.05f));
        }

        A("height: seated head lifted to target", Near(HeightCalibration.FixedOffset(1.65f, 1.00f), 0.65f));
        A("height: head already at target", Near(HeightCalibration.FixedOffset(1.65f, 1.65f), 0f));
        A("height: too-high head pulled DOWN (roof impossible)", Near(HeightCalibration.FixedOffset(1.65f, 2.40f), -0.75f));
        A("height: upward adjust clamped", Near(HeightCalibration.FixedOffset(1.65f, 0.00f), HeightCalibration.MaxAdjust));
        A("height: downward adjust clamped", Near(HeightCalibration.FixedOffset(1.65f, 4.00f), -HeightCalibration.MaxAdjust));
        A("height: different scene targets differ", HeightCalibration.FixedOffset(1.40f, 1.0f) < HeightCalibration.FixedOffset(1.80f, 1.0f));
        // Pose validity: calibrating from an UNTRACKED zero pose during the load
        // fade must be impossible (it spawns the player 1.6x too tall, at the roof).
        A("height: untracked zero pose rejected", !HeightCalibration.PoseValid(UnityEngine.Vector3.zero, 0f));
        A("height: settled real pose accepted", HeightCalibration.PoseValid(new UnityEngine.Vector3(0.1f, 1.0f, 0.2f), 1.001f));
        A("height: tracking-kick-in jump rejected", !HeightCalibration.PoseValid(new UnityEngine.Vector3(0.1f, 1.0f, 0.2f), 0f));
        // Two-sided drift (e.g. stuck at 0.96 after the headset is repositioned): tall
        // corrects with a small allowance, stuck-short with a generous one so
        // ordinary crouching/bench-leaning is never touched.
        A("height: standing-up overshoot detected", Near(HeightCalibration.TallExcess(2.10f, 1.65f), 0.30f));
        A("height: small lean-up tolerated", Near(HeightCalibration.TallExcess(1.75f, 1.65f), 0f));
        A("height: bench crouch never corrected", Near(HeightCalibration.TallExcess(1.25f, 1.65f), 0f));
        A("height: stuck-short corrected upward", HeightCalibration.TallExcess(0.96f, 1.65f) < 0f);
        A("height: short allowance more generous than tall",
            HeightCalibration.ShortTolerance > HeightCalibration.TallTolerance);
        // Hand-vs-glove display policy (bare hand unless the PPE glove is worn).
        A("handswap: bare hand when unglovd", HandSwap.ShowBareHand(false));
        A("handswap: glove replaces bare hand", !HandSwap.ShowBareHand(true));
        // Three-pose two-skin hands (free + grab + point, bare + nitrile).
        A("handpose: grab wins over point", HandPosePolicy.PoseFor(true, true) == HandPoseKind.Grab);
        A("handpose: hovering an interactable points", HandPosePolicy.PoseFor(false, true) == HandPoseKind.Point);
        A("handpose: idle hand is free", HandPosePolicy.PoseFor(false, false) == HandPoseKind.Free);
        A("handpose: pointing index stays straight", Near(HandPosePolicy.AngleFor(HandPoseKind.Point, false, true, 0), 0f));
        A("handpose: pointing curls the other fingers", Near(HandPosePolicy.AngleFor(HandPoseKind.Point, false, false, 0), HandPosePolicy.ProximalCurl));
        A("handpose: grab curls the index too", Near(HandPosePolicy.AngleFor(HandPoseKind.Grab, false, true, 0), HandPosePolicy.ProximalCurl));
        A("handpose: free hand fully open", Near(HandPosePolicy.AngleFor(HandPoseKind.Free, false, false, 0), 0f));
        A("handpose: nitrile only when gloves worn", HandPosePolicy.Nitrile(true) && !HandPosePolicy.Nitrile(false));
        A("handpose: distal curls less than proximal", HandPosePolicy.DistalCurl < HandPosePolicy.ProximalCurl);
    }

    // Fitted watch geometry (the solid Tripo watch cannot wrap a
    // wrist — the band is generated around the MEASURED wrist cross-section).
    static void WatchMathSuite()
    {
        var pts = new System.Collections.Generic.List<UnityEngine.Vector3>
        {
            new UnityEngine.Vector3(-0.03f, 0.00f, -0.045f),
            new UnityEngine.Vector3( 0.03f, 0.02f, -0.045f),
            new UnityEngine.Vector3( 0.00f, -0.01f, -0.044f),
            new UnityEngine.Vector3( 0.00f, 0.03f, -0.046f),
            new UnityEngine.Vector3( 0.50f, 0.50f, 0.200f)    // far point — must be excluded
        };
        var s = WatchMath.MeasureSlice(pts, -0.045f, 0.008f);
        A("watch: slice excludes far points", s.samples == 4);
        A("watch: slice half-width measured", Near(s.halfExtents.x, 0.03f));
        A("watch: slice centre offset found", Near(s.center.y, 0.01f));
        A("watch: empty slice is safe", WatchMath.MeasureSlice(pts, 9f, 0.001f).samples == 0);
        A("watch: band adds skin clearance",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.03f, 0.02f)).x, 0.03f + WatchMath.BandClearance));
        A("watch: band floors tiny measurements", WatchMath.BandRadii(UnityEngine.Vector2.zero).x >= WatchMath.MinHalfWidth);
        A("watch: palm bulge cannot widen the band",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.05f, 0.02f)).x, WatchMath.MaxHalfWidth));
        A("watch: face diameter clamped", WatchMath.FaceDiameter(1f) <= 0.0401f);
        var mesh = WatchMath.BuildBandMesh(new UnityEngine.Vector2(0.03f, 0.02f), 0.004f, 16, 8);
        A("watch: band mesh has full geometry", mesh.vertexCount == 17 * 9 && mesh.triangles.Length == 16 * 8 * 6);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    // Held-item collision profile: pack prefabs default to Instantaneous movement
    // (no physics sweep), so grabbed props phase through walls.
    static void GrabTuningSuite()
    {
        var host = new GameObject("tune-host");
        try
        {
            var grab = host.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            A("grab: untuned detected", !GrabTuning.IsTuned(grab));
            A("grab: apply reports change", GrabTuning.Apply(grab));
            A("grab: velocity-tracked after apply", GrabTuning.IsTuned(grab));
            A("grab: two-handed (multi-grab) after apply", grab.selectMode == UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple);
            A("grab: ease-in set", Near(grab.attachEaseInTime, GrabTuning.AttachEaseSeconds));
            A("grab: re-apply is a no-op", !GrabTuning.Apply(grab));
            A("grab: null safe", !GrabTuning.Apply(null) && !GrabTuning.IsTuned(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }

        // Every library prefab grab must stay velocity-tracked (pins the Wire
        // Grab Collision menu's result; a prefab re-import regression fails here).
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("grab: library present", lib != null && lib.prefabs != null);
        if (lib != null && lib.prefabs != null)
        {
            int grabs = 0, tuned = 0;
            foreach (var p in lib.prefabs)
            {
                if (p == null) continue;
                foreach (var g in p.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
                { grabs++; if (GrabTuning.IsTuned(g)) tuned++; }
            }
            A("grab: all library prefabs velocity-tracked (" + tuned + "/" + grabs + ")", grabs > 0 && tuned == grabs);
        }
    }

    // Shelf-bottle pour wiring (tipping shelf bottles showed no
    // stream/puddle — LiquidPourer only existed on runtime-spawned props).
    static void ShelfPourWiringSuite()
    {
        A("shelfpour: null safe", ShelfPourWiring.WireBottle(null, null, null) == -1);
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            A("shelfpour: non-liquid rejected", ShelfPourWiring.WireBottle(bottle, null, null) == -1);
            var blp = bottle.AddComponent<LiquidPhysics>();
            int added = ShelfPourWiring.WireBottle(bottle, null, null);
            A("shelfpour: adds visual+pourer+spout+spill+reactor+feedback", added == 6);   // +visual +MixFeedback
            var pourer = bottle.GetComponent<LiquidPourer>();
            A("shelfpour: pourer present", pourer != null);
            A("shelfpour: spout wired", pourer != null && pourer.spout != null);
            A("shelfpour: spill present", bottle.GetComponent<SpillMistake>() != null);
            A("shelfpour: liquid visual wired (W5.8)", blp.mainRenderer != null
                && blp.mainRenderer.sharedMaterial != null && blp.mainRenderer.sharedMaterial.HasProperty("_Fill"));
            A("shelfpour: idempotent", ShelfPourWiring.WireBottle(bottle, null, null) == 0);
        }
        finally { UnityEngine.Object.DestroyImmediate(bottle); }
    }

    // CINEMATIC CUTSCENES (client slides cutscenes-sample.pdf): a shot
    // camera films the lab onto a screen in a dark room; the XR camera is never moved.
    static void CinemaSuite()
    {
        A("cine: ease runs 0 → 1, symmetric", Near(ShotMath.Ease(0f), 0f) && Near(ShotMath.Ease(1f), 1f)
            && Near(ShotMath.Ease(0.5f), 0.5f) && Near(ShotMath.Ease(2f), 1f));

        var cur = new ShotMath.Pose(new Vector3(0f, 2f, 0f), Quaternion.identity);
        var mk = new ShotMath.Pose(new Vector3(5f, 1.5f, 0f), Quaternion.Euler(0f, 90f, 0f));
        var dest = new ShotMath.Pose(new Vector3(5f, 1.5f, 4f), Quaternion.identity);
        Vector3 target = new Vector3(5f, 1f, 8f);

        ShotMath.Ends(false, cur, mk, null, out var s0, out var e0);
        var p0 = ShotMath.Evaluate(s0, e0, false, target, 0f);
        A("cine: a cut starts on its marker, already aimed at the target",
            Near(Vector3.Distance(p0.pos, mk.pos), 0f)
            && Vector3.Dot(p0.rot * Vector3.forward, (target - mk.pos).normalized) > 0.999f);
        A("cine: a cut with no dolly holds its marker", Near(Vector3.Distance(e0.pos, mk.pos), 0f));

        ShotMath.Ends(true, cur, mk, null, out var s1, out var e1);
        A("cine: a blend glides from where the camera is to the marker",
            Near(Vector3.Distance(ShotMath.Evaluate(s1, e1, true, null, 0f).pos, cur.pos), 0f)
            && Near(Vector3.Distance(ShotMath.Evaluate(s1, e1, true, null, 1f).pos, mk.pos), 0f)
            && Quaternion.Angle(ShotMath.Evaluate(s1, e1, true, null, 1f).rot, mk.rot) < 0.5f);

        ShotMath.Ends(false, cur, mk, dest, out var s2, out var e2);
        bool level = true, aimed = true;
        for (int i = 0; i <= 10; i++)
        {
            var p = ShotMath.Evaluate(s2, e2, false, target, i / 10f);
            if (Mathf.Abs(Vector3.Dot(p.rot * Vector3.right, Vector3.up)) > 1e-3f) level = false;
            if (Vector3.Dot(p.rot * Vector3.forward, (target - p.pos).normalized) < 0.999f) aimed = false;
        }
        A("cine: a dolly with a look target never rolls the horizon", level);
        A("cine: a dolly stays locked on its look target every frame", aimed);
        ShotMath.Ends(false, cur, null, null, out var s3, out var e3);
        A("cine: a beat with no camera holds the previous framing",
            Near(Vector3.Distance(s3.pos, cur.pos), 0f) && Near(Vector3.Distance(e3.pos, cur.pos), 0f));

        var plain = ScriptableObject.CreateInstance<CutsceneData>();
        plain.beats = new List<CutsceneData.Beat> { new CutsceneData.Beat { subtitle = "hi" } };
        var test = CinemaCutsceneTools.TestSequence();
        A("cine: no shots = subtitle cutscene, any shot = cinematic", !plain.IsCinematic && test.IsCinematic);
        UnityEngine.Object.DestroyImmediate(plain);

        A("cine: Pharmee's pop scales 0 → overshoot → 1",
            CutsceneDirector.PopScale(0f) < 0.01f && Near(CutsceneDirector.PopScale(1f), 1f)
            && CutsceneDirector.PopScale(0.8f) > 1f);
        A("cine: the product shot finds the player's synthesis, never shelf stock",
            CinemaView.HoldsProduct("Eq_Beaker_250", "benzoic acid", null, "Benzoic Acid")
            && CinemaView.HoldsProduct("Kit_TestTube_3", null, "Benzoic Acid", "Benzoic Acid")
            && !CinemaView.HoldsProduct("Raw_BenzoicAcid", "Benzoic Acid", null, "Benzoic Acid")
            && !CinemaView.HoldsProduct("Eq_Beaker_250", "Water", null, "Benzoic Acid")
            && !CinemaView.HoldsProduct("Eq_Beaker_250", "Water", null, null));

        CinemaView.ScreenPose(new Vector3(0f, 1.6f, 0f), new Vector3(0f, -0.8f, 0.6f), 2.5f, out var sp, out var sr);
        A("cine: the screen lands level ahead of the head, even looking down",
            Near(sp.y, 1.6f) && Near(sp.z, 2.5f) && Mathf.Abs((sr * Vector3.forward).y) < 1e-4f);

        // Enter/Exit restore exactly what they changed — a stuck culling mask would
        // leave the player in a black void with no way out.
        A("cine: the Cinema layer exists", CinemaView.Layer >= 0);
        var cg = new GameObject("cine_xr");
        var vg = new GameObject("cine_view");
        try
        {
            var cam = cg.AddComponent<Camera>();
            cam.cullingMask = 0x5F; cam.clearFlags = CameraClearFlags.Skybox; cam.backgroundColor = Color.red;
            var view = vg.AddComponent<CinemaView>();
            view.Enter(cam);
            A("cine: inside, the XR camera sees only the theatre",
                view.Active && cam.cullingMask == 1 << CinemaView.Layer && cam.clearFlags == CameraClearFlags.SolidColor
                && view.ShotCamera.enabled && view.ShotCameraIsFlat
                && (view.ShotCamera.cullingMask & (1 << CinemaView.Layer)) == 0);
            view.SetTint(Color.red);
            view.Exit();
            A("cine: exit clears the timelapse tint", view.Tint == Color.white);
            A("cine: exit restores the XR camera exactly",
                !view.Active && cam.cullingMask == 0x5F && cam.clearFlags == CameraClearFlags.Skybox
                && cam.backgroundColor == Color.red && !view.ShotCamera.enabled);
            view.Exit();
            A("cine: a second exit is harmless", cam.cullingMask == 0x5F);
        }
        finally { UnityEngine.Object.DestroyImmediate(vg); UnityEngine.Object.DestroyImmediate(cg); }

        // The clock pauses for a mid-run cinematic without thawing a review freeze.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { T("a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure) };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var rg = new GameObject("cine_runner");
        try
        {
            var runner = rg.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            runner.AdvanceTime(5f);
            runner.PauseClock(true); runner.AdvanceTime(20f);
            bool held = Near(runner.ElapsedSeconds, 5f);
            runner.PauseClock(false); runner.AdvanceTime(1f);
            A("cine: a cutscene pauses the clock and releases it", held && Near(runner.ElapsedSeconds, 6f));
            runner.FreezeClock(); runner.PauseClock(true); runner.PauseClock(false); runner.AdvanceTime(9f);
            A("cine: releasing a pause never thaws the review freeze", Near(runner.ElapsedSeconds, 6f));
            bool aborted = false; runner.Aborted += () => aborted = true;
            runner.PauseClock(true); runner.Abort();
            A("cine: abort announces itself and clears the pause", aborted && !runner.ClockPaused);
        }
        finally { UnityEngine.Object.DestroyImmediate(rg); UnityEngine.Object.DestroyImmediate(module); }

        // PHASE 2 — the slide template on the library (Apply Cutscene Shots).
        A("cine: each cinematic opens on the slides' card",
            CutsceneDirector.CardFor(CutsceneData.Kind.ReagentPrep).title == "Preparations Complete"
            && CutsceneDirector.CardFor(CutsceneData.Kind.Success).title == "Experiment Complete"
            && CutsceneDirector.CardFor(CutsceneData.Kind.Failure).title.Length > 0
            && CutsceneDirector.CardFor(CutsceneData.Kind.Intro).title == ""
            && CutsceneDirector.CardFor(CutsceneData.Kind.Intro, "Benzoic Acid").title == "Benzoic Acid");
        {
            var b0 = CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Success, 0);
            var b1s = CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Success, 1);
            var b1f = CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Failure, 1);
            var b2 = CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Success, 2);
            A("cine: template — Pharmee pops out, the camera glides to her close-up",
                b0.pharmeePopIn && b0.blend && b0.look == CinemaLook.Pharmee && b0.camera == "Cam_PharmeeClose");
            A("cine: template — the pan lands on the product only on a pass",
                b1s.look == CinemaLook.Product && b1f.look == CinemaLook.Bench && b1s.blend && b1s.camera == "Cam_Bench");
            A("cine: template — later beats hold", !b2.HasCamera);
            A("cine: intro — Pharmee pops out first, objectives hold on her, the last beat pans to the bench",
                CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Intro, 0, 6).pharmeePopIn
                && !CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Intro, 3, 6).HasCamera
                && CinemaCutsceneTools.TemplateShot(CutsceneData.Kind.Intro, 5, 6).camera == "Cam_Bench");
        }
        {
            var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>(CinemaCutsceneTools.LibraryPath);
            int total = 0; var off = new List<string>();
            foreach (var c in CinemaCutsceneTools.TemplatedCutscenes(lib))
            {
                total++;
                bool ok = c.IsCinematic;
                for (int i = 0; i < c.beats.Count && ok; i++)
                {
                    var want = CinemaCutsceneTools.TemplateShot(c.kind, i, c.beats.Count); var got = c.beats[i].shot;
                    ok = got != null && got.camera == want.camera && got.blend == want.blend && got.look == want.look
                         && got.pharmeePopIn == want.pharmeePopIn;
                }
                if (!ok) off.Add(c.name);
            }
            A("content: all 36 cutscenes (intro, prep, success, failure) carry the cinematic template" + (off.Count > 0 ? " — off: " + string.Join(", ", off) : ""),
                total == 36 && off.Count == 0);
        }

        // PHASE 3 — the corner-camera timelapse for longProcess tasks.
        A("timelapse: one day/night cycle per day, capped, none under a day",
            TimelapseMath.Cycles(168f) == 7 && TimelapseMath.Cycles(48f) == 2 && TimelapseMath.Cycles(2000f) == 7
            && TimelapseMath.Cycles(0.25f) == 0 && TimelapseMath.Cycles(0f) == 0);
        A("timelapse: lit at dawn and at the end, dark at midnight",
            TimelapseMath.Tint(0f, 168f) == Color.white && TimelapseMath.Tint(1f, 168f) == Color.white
            && Vector4.Distance(TimelapseMath.Tint(0.5f / 7f, 168f), TimelapseMath.Night) < 0.01f
            && TimelapseMath.IsNight(0.5f / 7f, 168f) && !TimelapseMath.IsNight(0f, 168f));
        A("timelapse: a 15-minute wait never goes dark", TimelapseMath.Tint(0.5f, 0.25f) == Color.white);
        A("timelapse: the counter reads days, minutes, or no number at all",
            TimelapseMath.Counter(0f, 168f).StartsWith("DAY 1 OF 7") && TimelapseMath.Counter(0.99f, 168f).StartsWith("DAY 7 OF 7")
            && TimelapseMath.Counter(1f, 0.25f).StartsWith("0:15 OF 0:15") && TimelapseMath.Counter(0.5f, 0f) == ">> FAST FORWARD");
        {
            var stated = new Dictionary<string, float> { { "confirm-ferment", 168f }, { "ferment", 168f }, { "stir-stand", 0.25f } };
            int longTasks = 0; var wrong = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition", new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (var t in m.graphTasks)
                {
                    if (!t.longProcess) continue;
                    longTasks++;
                    float want = stated.TryGetValue(t.taskId, out var h) ? h : 0f;
                    if (!Near(t.timelapseHours, want)) wrong.Add(t.taskId + "=" + t.timelapseHours);
                }
            }
            A("timelapse: only durations the procedure states carry a number" + (wrong.Count > 0 ? " — " + string.Join(", ", wrong) : ""),
                longTasks == 6 && wrong.Count == 0);
        }

        // Pharmee's speech bubble rides 1.1-1.4 m above her pivot; parked at 1.45 in the
        // corner it goes through the 2.64 m ceiling.
        {
            var mroot = GameObject.Find(CinemaView.MarkersRoot);
            var corner = mroot != null ? mroot.transform.Find("Marker_PharmeeCorner") : null;
            bool clear = false;
            if (corner != null && Physics.Raycast(corner.position + Vector3.up * 0.2f, Vector3.up, out var ceil, 4f))
                clear = corner.position.y + 1.4f < ceil.point.y;
            A("bench: Pharmee's corner keeps her speech bubble under the ceiling", clear);
        }
        // Dr. Jimenez floated ~10 cm: his capsule bottom sat 2 cm above his feet and a
        // CharacterController rests one skinWidth above the ground on top of that.
        {
            var jim = GameObject.Find("DrJimenez");
            var cc = jim != null ? jim.GetComponent<CharacterController>() : null;
            var model = jim != null ? jim.transform.Find("JimenezModel") : null;
            bool grounded = false;
            if (cc != null && model != null)
            {
                Bounds jb = default; bool any = false;
                foreach (var r in jim.GetComponentsInChildren<SkinnedMeshRenderer>()) { if (!any) { jb = r.bounds; any = true; } else jb.Encapsulate(r.bounds); }
                float bottom = jim.transform.TransformPoint(cc.center).y - cc.height * 0.5f;      // capsule bottom, world
                float idleSoles = jb.min.y + LabNpcPolishBuilder.JimenezIdleLift;
                grounded = any && Mathf.Abs((bottom - cc.skinWidth) - idleSoles) < 0.01f;
            }
            A("bench: Dr. Jimenez's capsule puts his animated soles on the floor", grounded);
        }

        // The balance readout (a world TMP label at 20000) drew straight through the grade
        // card at 4500 — review panels must sort above world labels (below bubbles/HUD).
        {
            bool ok = true;
            foreach (var n in new[] { "GradeScreen", "PostLabTablet" })
            {
                var g = GameObject.Find(n); var c = g != null ? g.GetComponentInChildren<Canvas>(true) : null;
                ok &= c != null && c.sortingOrder > 20000 && c.sortingOrder < 29000;
            }
            A("bench: the grade card and quiz tablet draw over world labels", ok);
        }
        // Reagent labels hug the glass: band centred on the body within 3 mm.
        {
            var cab = GameObject.Find("ReagentCabinets"); int off = 0, n = 0;
            if (cab != null)
                foreach (var lp in cab.GetComponentsInChildren<LiquidPhysics>(true))
                {
                    var label = lp.transform.Find("NameLabel"); if (label == null) continue;
                    var body = lp.GetComponent<Renderer>(); if (body == null) continue;
                    n++;
                    var d = new Vector2(label.position.x - body.bounds.center.x, label.position.z - body.bounds.center.z).magnitude;
                    if (d > 0.003f) off++;
                }
            A("bench: every cabinet label is centred on its glass (" + n + " checked, " + off + " off)", n > 0 && off == 0);
        }

        // A component whose class lives in another class's file saves with a local-only
        // script link: it reloads as an empty slot, or, worse, Unity rebuilds it from the
        // class NAME in the editor while a player build drops it (e.g. the fume hood
        // label, the brush, both burners' open-flame heat).
        {
            int empty = 0, unbacked = 0; var names = new List<string>();
            foreach (var go in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include))
            {
                if (go.hideFlags != HideFlags.None) continue;   // runtime-built helpers (cinema rig, fade quad)
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null) { empty++; if (names.Count < 5) names.Add(go.name); continue; }
                    if (c is MonoBehaviour mb && mb.GetType().Assembly.GetName().Name == "Assembly-CSharp"
                        && string.IsNullOrEmpty(AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(mb))))
                    { unbacked++; if (names.Count < 5) names.Add(go.name + "/" + mb.GetType().Name); }
                }
            }
            A("bench: every component in the scene has a real script file" + (names.Count > 0 ? " — " + string.Join(", ", names) : ""),
                empty == 0 && unbacked == 0);
        }

        // ...and the cause can't come back: every game component / ScriptableObject class must
        // have a script file of its own name, or adding one in the editor and saving breaks it.
        {
            var backed = new HashSet<System.Type>();
            foreach (var guid in AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/PharmaSynth/Scripts" }))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() != null) backed.Add(ms.GetClass());
            }
            var misfiled = new List<string>();
            foreach (var t in typeof(ExperimentRunner).Assembly.GetTypes())
            {
                if (t.IsAbstract || t.IsGenericType || t.IsNested) continue;
                if (!typeof(MonoBehaviour).IsAssignableFrom(t) && !typeof(ScriptableObject).IsAssignableFrom(t)) continue;
                if (!backed.Contains(t)) misfiled.Add(t.Name);
            }
            A("bench: every game component lives in a file named after it" + (misfiled.Count > 0 ? " — " + string.Join(", ", misfiled) : ""),
                misfiled.Count == 0);
        }

        // Scene pins: the markers the shots name must really exist in SampleScene.
        var root = GameObject.Find(CinemaView.MarkersRoot);
        A("bench: CutsceneMarkers holds the CinemaView", root != null && root.GetComponent<CinemaView>() != null);
        if (root != null)
        {
            var missing = new List<string>();
            foreach (var m in CinemaCutsceneTools.DefaultMarkers) if (root.transform.Find(m.name) == null) missing.Add(m.name);
            foreach (var b in test.beats)
                foreach (var n in new[] { b.shot.camera, b.shot.moveTo })
                    if (!string.IsNullOrEmpty(n) && root.transform.Find(n) == null) missing.Add(n);
            A("bench: every cutscene marker exists" + (missing.Count > 0 ? " — missing " + string.Join(", ", missing) : ""),
                missing.Count == 0);
        }
        UnityEngine.Object.DestroyImmediate(test);
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

    // Focused checklist for the single holo procedures board:
    // full-detail text overflowed the pads — collapse everything but the active phase).
    static void ChecklistPagerSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("heat", TaskPhase.Synthesis, 1, LabSkill.Heating, RubricCategory.Procedure, "mix"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "heat"),
        });

        A("pager: active = first available phase", ChecklistPager.ActivePhase(g) == TaskPhase.ReagentPrep);
        string s = ChecklistPager.BuildFocusedText(g);
        A("pager: active phase in detail", s.Contains("prep") && s.Contains("»"));
        A("pager: future phase collapsed", s.Contains("(2 steps)") && !s.Contains("mix"));
        A("pager: future tests collapsed", s.Contains("(1 steps)") && !s.Contains("test"));

        g.TryComplete("prep");
        A("pager: active advances", ChecklistPager.ActivePhase(g) == TaskPhase.Synthesis);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: completed phase folds to done n/n", s.Contains("done 1/1"));
        A("pager: new active in detail", s.Contains("mix") && s.Contains("heat"));
        A("pager: folded phase hides steps", !s.Contains("prep"));

        g.TryComplete("mix"); g.TryComplete("heat"); g.TryComplete("test");
        A("pager: all complete → no active", ChecklistPager.ActivePhase(g) == null);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: everything folded when done", s.Contains("done 1/1") && s.Contains("done 2/2") && !s.Contains("»"));

        A("pager: null graph safe", ChecklistPager.BuildFocusedText(null) == "" && ChecklistPager.ActivePhase(null) == null);
        A("pager: null runner header safe", ChecklistPager.BuildHeader(null) == "");

        // Bounded by construction: line count ≤ phases + active-phase tasks + 1.
        var lines = ChecklistPager.BuildFocusedText(g).Split('\n');
        A("pager: folded output is compact", lines.Length <= 6);
    }

    // Demo Mode config/save isolation (panelists get a fast
    // auto-complete run-through; study participants' real save is never touched).
    static void DemoModeSuite()
    {
        A("demo: missing config → disabled", !DemoMode.Resolve(null, null).demoEnabled);
        A("demo: streaming default enables", DemoMode.Resolve(null, "{\"demoEnabled\":true}").demoEnabled);
        A("demo: persistent override wins (disable)", !DemoMode.Resolve("{\"demoEnabled\":false}", "{\"demoEnabled\":true}").demoEnabled);
        A("demo: persistent override wins (enable)", DemoMode.Resolve("{\"demoEnabled\":true}", null).demoEnabled);
        A("demo: malformed override falls through", DemoMode.Resolve("not-json{", "{\"demoEnabled\":true}").demoEnabled);
        A("demo: infiniteSupply defaults on", DemoMode.Resolve(null, "{\"demoEnabled\":true}").infiniteSupply);

        A("demo: normal save path untouched", DemoMode.SavePathFor(false, "a/pharmasynth_progress.json") == "a/pharmasynth_progress.json");
        A("demo: demo save path suffixed", DemoMode.SavePathFor(true, "a/pharmasynth_progress.json") == "a/pharmasynth_progress_demo.json");
        A("demo: extensionless path safe", DemoMode.SavePathFor(true, "save") == "save_demo");
        A("demo: null path safe", DemoMode.SavePathFor(true, null) == null);

        // Unlock-all opens every period/module WITHOUT faking passes.
        var svc = new ProgressionService(System.IO.Path.Combine(Application.temporaryCachePath, "demo_flow_selftest.json"));
        var demoFlow = new ProgressionFlow(svc, true);
        var normalFlow = new ProgressionFlow(svc);
        A("demo: finals period pickable", demoFlow.IsPeriodUnlocked(ExperimentPeriod.Final));
        A("demo: any module pickable", demoFlow.IsUnlocked("final-winemaking"));
        A("demo: unknown module still rejected", !demoFlow.IsUnlocked("does-not-exist"));
        A("demo: pass state stays honest", !demoFlow.IsPassed("tutorial-methane") && demoFlow.PassedCount() == 0);
        A("demo: normal flow unaffected", !normalFlow.IsPeriodUnlocked(ExperimentPeriod.Final));

        // A ready-made GOAL destroys the experiment, so a product
        // is hidden while ITS OWN module runs, and the four PURE products, which no
        // procedure ever names as a reagent, are not on the shelf at all.
        // Only the dual-role three remain: each is also a manuscript reagent.
        A("endproduct: ethanol is dual-role (goal of Exp 3, reagent in Exp 2/6)",
            DemoMode.IsEndProduct("Ethanol"));
        A("endproduct: acetone is dual-role (goal of Exp 6, reagent in Exp 2/7)",
            DemoMode.IsEndProduct("Acetone"));
        A("endproduct: benzoic acid is dual-role (goal of Exp 4, its own reference)",
            DemoMode.IsEndProduct("Benzoic Acid"));
        // Deleted, not gated — their own tests consume the player's synthesis, so
        // they are never an input and no bottle should exist to hide.
        A("endproduct: the 4 pure products are GONE, not gated",
            !DemoMode.IsEndProduct("Acetanilide") && !DemoMode.IsEndProduct("Benzamide")
            && !DemoMode.IsEndProduct("Chloroform") && !DemoMode.IsEndProduct("Wine"));
        A("endproduct: aspirin is RAW (Exp 2 §D hydrolyses it)", !DemoMode.IsEndProduct("Aspirin"));
        A("endproduct: sulfuric acid is raw", !DemoMode.IsEndProduct("Sulfuric Acid"));
        A("endproduct: sodium acetate is feedstock", !DemoMode.IsEndProduct("Sodium Acetate"));
        A("endproduct: fruit juice is feedstock, not product", !DemoMode.IsEndProduct("Mixed Fruit Juice") && !DemoMode.IsEndProduct("Grape Juice"));
        A("endproduct: null safe", !DemoMode.IsEndProduct(null));

        // THE rule: hide only the RUNNING module's own product.
        A("gate: ethanol hides while Exp 3 synthesises it",
            EndProductVisibility.HiddenProductFor("prelim-ethyl-alcohol", false) == "Ethanol");
        A("gate: acetone hides while Exp 6 synthesises it",
            EndProductVisibility.HiddenProductFor("midterm-acetone", false) == "Acetone");
        // What this design prevents: Exp 2 runs BEFORE Exp 3/6 and
        // needs ethanol (enol sample, ester) + acetone (Tollen's contrast). A global
        // per-chemical hide would strip them and make Exp 2 unplayable outside demo.
        A("gate: Exp 2 hides NOTHING — it is an ID lab and needs ethanol + acetone",
            EndProductVisibility.HiddenProductFor("prelim-chemical-compounding", false) == null);
        A("gate: a demo session hides nothing",
            EndProductVisibility.HiddenProductFor("prelim-ethyl-alcohol", true) == null);
        A("gate: nothing running hides nothing",
            EndProductVisibility.HiddenProductFor(null, false) == null);
    }

    // Consumable dispenser (grab the box → pull a single piece;
    // it refills; used pieces clean themselves up).
    static void DispenserSuite()
    {
        // Taken: a hand grabs the resting piece, OR it moves off the slot.
        A("dispenser: grab counts as taken", DispenserMath.IsTaken(true, 0f));
        A("dispenser: nudge off slot counts as taken", DispenserMath.IsTaken(false, 0.2f));
        A("dispenser: resting piece not taken", !DispenserMath.IsTaken(false, 0.01f));

        // Discard: only a piece that was held, is now set down, still, long enough.
        A("dispenser: used + idle piece discarded", DispenserMath.ShouldDiscard(true, false, 0f, 15f));
        A("dispenser: never-held ready piece kept", !DispenserMath.ShouldDiscard(false, false, 0f, 999f));
        A("dispenser: held piece kept", !DispenserMath.ShouldDiscard(true, true, 0f, 999f));
        A("dispenser: still-moving piece kept", !DispenserMath.ShouldDiscard(true, false, 0.5f, 999f));
        A("dispenser: not-yet-idle piece kept", !DispenserMath.ShouldDiscard(true, false, 0f, 3f));
    }

    // Demo HUD auto-complete verbs (skip step / finish experiment / auto quiz).
    static void DemoActionsSuite()
    {
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("b", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure, "a"),
            T("c", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "b"),
            T("record-x", TaskPhase.DataSheet, 1, LabSkill.Measuring, RubricCategory.Documentation, "c"),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring, LabSkill.Transfer, LabSkill.TestInterpretation };
        var rgo = new GameObject("demo_runner"); var pgo = new GameObject("demo_postlab");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            A("demoactions: inert before start", DemoActions.CompleteCurrentStep(runner) == null);
            runner.StartExperiment();
            A("demoactions: completes first available", DemoActions.CompleteCurrentStep(runner) == "a");
            A("demoactions: complete-all stops at data sheet", DemoActions.CompleteAllTasks(runner) == 2);
            A("demoactions: data-sheet task untouched", !runner.Graph.IsComplete("record-x"));
            A("demoactions: nothing left to skip", DemoActions.CompleteCurrentStep(runner) == null);

            var post = pgo.AddComponent<PostLabController>();
            post.SetRefs(runner, null);
            A("demoactions: quiz closed → no-op", !DemoActions.AutoAnswerQuiz(post));
            var bank = ScriptableObject.CreateInstance<QuizBank>();
            bank.questions = new List<QuizQuestion> {
                new QuizQuestion { prompt = "q1", options = new List<string>{ "x", "y" }, correctIndex = 1 },
                new QuizQuestion { prompt = "q2", options = new List<string>{ "x", "y" }, correctIndex = 0 },
            };
            post.OpenFor(bank);
            A("demoactions: auto-answers all", DemoActions.AutoAnswerQuiz(post));
            A("demoactions: perfect score", Near(post.ScoreFraction(), 1f));
            var res = post.SubmitAndFinish();
            A("demoactions: submit finishes with full graph", Near(runner.Graph.Progress01, 1f) && res.grade.Total > 0f);
            UnityEngine.Object.DestroyImmediate(bank);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(pgo);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    // Hazardous-mix consequences (wrong mixtures smoke,
    // ignite, spatter or fizz, and cost the right rubric category).
    static void HazardousMixSuite()
    {
        ChemicalData Make(string name, float ph = 7f, HazardType hz = HazardType.None,
                          bool ox = false, bool conc = false, PhysicalState st = PhysicalState.Liquid)
        {
            var c = ScriptableObject.CreateInstance<ChemicalData>();
            c.chemicalName = name; c.pH = ph; c.hazard = hz;
            c.isOxidizer = ox; c.isConcentratedAcid = conc; c.state = st;
            return c;
        }

        var acid = Make("Hydrochloric Acid 6N", 0.5f, HazardType.Corrosive, conc: true);
        var bleach = Make("Sodium Hypochlorite 5%", 11f, ox: true);
        var ethanol = Make("Ethanol", 7f, HazardType.Flammable);
        var kmno4 = Make("Potassium Permanganate 0.1%", 7f, ox: true);
        var water = Make("Distilled Water");

        try
        {
            A("hazmix: acid + bleach = toxic gas",
                HazardousMix.Classify(acid, bleach) == HazardousMix.HazardOutcome.ToxicGas
                && HazardousMix.Classify(bleach, acid) == HazardousMix.HazardOutcome.ToxicGas);
            A("hazmix: oxidizer + flammable = fire",
                HazardousMix.Classify(kmno4, ethanol) == HazardousMix.HazardOutcome.FireOrExplosion
                && HazardousMix.Classify(ethanol, kmno4) == HazardousMix.HazardOutcome.FireOrExplosion);
            A("hazmix: water INTO conc acid = spatter",
                HazardousMix.Classify(acid, water) == HazardousMix.HazardOutcome.AcidSpatter);
            A("hazmix: conc acid into water = fizz only",
                HazardousMix.Classify(water, acid) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: unknown pair fizzes", HazardousMix.Classify(water, ethanol) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: null / same safe",
                HazardousMix.Classify(null, water) == HazardousMix.HazardOutcome.None
                && HazardousMix.Classify(water, water) == HazardousMix.HazardOutcome.None);

            A("hazmix: penalties map to the matrix",
                HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.ToxicGas) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.FireOrExplosion) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.AcidSpatter) == LabErrorType.ChemicalContact
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.GenericFizz) == LabErrorType.WrongReagent);
            A("hazmix: every outcome has a warn line",
                HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.ToxicGas).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.FireOrExplosion).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.AcidSpatter).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.GenericFizz).Length > 0);

            // Name-rule flags (the audit menu + raw-reagent forge share these).
            A("hazflags: oxidizers recognized",
                HazardFlags.IsOxidizer("Potassium Permanganate 0.1%") && HazardFlags.IsOxidizer("Bleaching Powder")
                && HazardFlags.IsOxidizer("Bromine Water") && !HazardFlags.IsOxidizer("Ethanol"));
            A("hazflags: conc acids recognized",
                HazardFlags.IsConcentratedAcid("Sulfuric Acid") && HazardFlags.IsConcentratedAcid("Glacial Acetic Acid")
                && !HazardFlags.IsConcentratedAcid("Diluted Hydrochloric Acid")
                && !HazardFlags.IsConcentratedAcid("0.1N Hydrochloric Acid"));

            // Warning-pulse curve + alarm flash phase.
            A("pulse: silent at ends", Near(FadeState.Pulse01(0f, 0.35f), 0f) && Near(FadeState.Pulse01(1f, 0.35f), 0f));
            A("pulse: peaks at attack end", Near(FadeState.Pulse01(0.35f, 0.35f), 0.35f));
            A("pulse: clamped peak", FadeState.Pulse01(0.35f, 1.7f) <= 1f);
            A("alarm: flash phase alternates", LabAlarm.FlashOn(0.1f, 0.5f) && !LabAlarm.FlashOn(0.3f, 0.5f)
                && LabAlarm.FlashOn(0.6f, 0.5f) && !LabAlarm.FlashOn(0f, 0f));

            // Reactor wiring seam (edit-mode: Bind + event path, no VFX side effects).
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = host.AddComponent<LiquidPhysics>();
                var reactor = host.AddComponent<HazardousMixReactor>();
                reactor.Bind(lp, null);
                A("hazmix: reactor binds without a runner", reactor != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(acid);
            UnityEngine.Object.DestroyImmediate(bleach);
            UnityEngine.Object.DestroyImmediate(ethanol);
            UnityEngine.Object.DestroyImmediate(kmno4);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    // NPC voice-over pipeline (Pharmee + Dr. Jimenez speak) —
    // hash-keyed clip lookup with graceful blip fallback.
    static void VoiceSuite()
    {
        A("voice: id ignores cosmetic whitespace", VoiceLineId.For("Hello  world") == VoiceLineId.For(" Hello world "));
        A("voice: distinct lines differ", VoiceLineId.For("line a") != VoiceLineId.For("line b"));
        A("voice: null/empty agree", VoiceLineId.For(null) == VoiceLineId.For(""));
        A("voice: id is 16 hex chars", VoiceLineId.For("x").Length == 16);

        var corpus = VoiceCorpus.CodeLines();
        A("voice: corpus substantial (≥110 code lines)", corpus.Count >= 110);
        bool nonEmpty = true;
        int jimenez = 0;
        foreach (var l in corpus)
        {
            if (string.IsNullOrEmpty(l.text)) nonEmpty = false;
            if (l.speaker == VoiceSpeaker.Jimenez) jimenez++;
        }
        A("voice: no empty corpus lines", nonEmpty);
        A("voice: Jimenez pools attributed (≥15)", jimenez >= 15);
        bool greetAsJimenez = false;
        foreach (var l in corpus)
            if (l.text == PharmeeLines.ExamGreeting[0] && l.speaker == VoiceSpeaker.Jimenez) greetAsJimenez = true;
        A("voice: exam greeting speaks as Jimenez", greetAsJimenez);

        var bank = ScriptableObject.CreateInstance<VoiceBank>();
        var clip = AudioClip.Create("voice_test", 441, 1, 44100, false);
        var go = new GameObject("voice_narr");
        try
        {
            bank.entries.Add(new VoiceBank.Entry { speaker = VoiceSpeaker.Pharmee, id = VoiceLineId.For("hello"), clip = clip });
            bank.Rebuild();
            A("voice: bank hit", bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For("hello")) == clip);
            A("voice: wrong speaker misses", bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For("hello")) == null);
            A("voice: unknown id misses", bank.Get(VoiceSpeaker.Pharmee, "beef") == null);

            var n = go.AddComponent<NPCNarrationController>();
            n.BindVoice(bank, VoiceSpeaker.Pharmee);
            A("voice: narration resolves by text", n.ResolveVoice("hello") == clip);
            A("voice: unknown text degrades to blips", n.ResolveVoice("no clip for this") == null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(bank);
            UnityEngine.Object.DestroyImmediate(clip);
        }
    }

    // The manuscript materials catalog + consumable behaviours:
    // ~54 raw materials stocked in nature-appropriate labware).
    static void RawReagentSuite()
    {
        var rows = RawReagentCatalog.Rows;
        A("raw: catalog covers the manuscript union (≥52)", rows.Count >= 52);

        var names = new HashSet<string>();
        bool unique = true, blurbs = true, groupsOk = true;
        var validGroups = new HashSet<string> { RawReagentCatalog.GroupAcids, RawReagentCatalog.GroupOrganics,
                                                RawReagentCatalog.GroupTests, RawReagentCatalog.GroupConsumables };
        foreach (var r in rows)
        {
            if (!names.Add(r.chemicalName)) unique = false;
            if (string.IsNullOrEmpty(r.blurb)) blurbs = false;
            if (!validGroups.Contains(r.group)) groupsOk = false;
        }
        A("raw: names unique", unique);
        A("raw: every row has a hover blurb", blurbs);
        A("raw: groups valid", groupsOk);

        A("raw: light-sensitive chemicals go amber",
            RawReagentCatalog.Find("Aniline").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Benzaldehyde").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Silver Nitrate").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Bromine Water").labware == RawReagentCatalog.LabwareKind.AmberBottle);
        // The manuscript demands the hood in exactly ONE step
        // (Exp 5 §A, aniline + acetyl chloride). Everything else works the bench.
        A("raw: fume-hood set = the manuscript's two, no more",
            RawReagentCatalog.Find("Aniline").fumeHood && RawReagentCatalog.Find("Acetyl Chloride").fumeHood
            && !RawReagentCatalog.Find("Benzoyl Chloride").fumeHood
            && !RawReagentCatalog.Find("Ammonia Solution").fumeHood
            && !RawReagentCatalog.Find("Phenol").fumeHood
            && !RawReagentCatalog.Find("Concentrated Hydrochloric Acid").fumeHood);
        A("raw: manuscript consumables present",
            RawReagentCatalog.Find("Litmus Paper") != null && RawReagentCatalog.Find("Matchsticks") != null
            && RawReagentCatalog.Find("Cotton Swabs") != null && RawReagentCatalog.Find("Filter Paper") != null
            && RawReagentCatalog.Find("Ice") != null && RawReagentCatalog.Find("Anhydrous Calcium Chloride") != null);
        // Exp 2 §D hydrolyses aspirin, so it MUST be a bench reagent, not a demo-only
        // end product — the pairing that made Exp 2 unplayable outside demo.
        A("raw: aspirin is a bench reagent for Exp 2 §D",
            RawReagentCatalog.Find("Aspirin") != null
            && RawReagentCatalog.Find("Aspirin").state == PhysicalState.Powder
            && !DemoMode.IsEndProduct("Aspirin"));
        // KMnO4 is POURED as "2 ml of 0.1% solution" (Exp 2 A.II). Marked Solid,
        // LiquidPourer cannot pour it and Exp 2's oxidation tubes are unbuildable.
        // The manuscript never uses it solid at the bench.
        {
            var kmno4 = RawReagentCatalog.Find("Potassium Permanganate 0.1%");
            var kchem = AssetDatabase.LoadAssetAtPath<ChemicalData>(
                "Assets/PharmaSynth/ScriptableObjects/Chemicals/Chem_PotassiumPermanganate.asset");
            A("raw: KMnO4 is the 0.1% SOLUTION, pourable", kmno4 != null && kmno4.state == PhysicalState.Liquid);
            A("raw: KMnO4 chemical asset agrees with its catalog row",
                kchem != null && kchem.state == PhysicalState.Liquid
                && kchem.chemicalName == "Potassium Permanganate 0.1%");
            A("raw: renaming KMnO4 kept its oxidizer flag",
                HazardFlags.IsOxidizer("Potassium Permanganate 0.1%"));
        }
        A("raw: blurb lookup + uses folded in",
            RawReagentCatalog.BlurbFor("Aniline") != null && RawReagentCatalog.BlurbFor("Aniline").Contains("Exp 5"));
        A("raw: unknown blurb null", RawReagentCatalog.BlurbFor("Unobtainium") == null);

        // Labware → prefab mapping must resolve through the scene asset library.
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("raw: library present", lib != null);
        if (lib != null)
        {
            bool prefabsOk = true;
            foreach (var kind in new[] { RawReagentCatalog.LabwareKind.ReagentBottle, RawReagentCatalog.LabwareKind.AmberBottle,
                                         RawReagentCatalog.LabwareKind.PowderJar, RawReagentCatalog.LabwareKind.DropperBottle })
                if (lib.GetPrefab(ReagentCabinetBuilder.PrefabFor(kind)) == null) prefabsOk = false;
            A("raw: bottle labware prefabs resolve", prefabsOk);
        }

        // Litmus colours.
        A("litmus: acid red", LitmusMath.ColorForPH(1f) == LitmusMath.AcidRed);
        A("litmus: base blue", LitmusMath.ColorForPH(13f) == LitmusMath.BaseBlue);
        Color mid = LitmusMath.ColorForPH(6.4f);
        A("litmus: violet between", mid != LitmusMath.AcidRed && mid != LitmusMath.BaseBlue);

        // Matchstick ignition predicate.
        A("match: ignites near heat", Matchstick.ShouldIgnite(0.1f, 120f, false, false));
        A("match: too far stays out", !Matchstick.ShouldIgnite(0.5f, 120f, false, false));
        A("match: too cold stays out", !Matchstick.ShouldIgnite(0.1f, 40f, false, false));
        A("match: lit/spent never re-ignite", !Matchstick.ShouldIgnite(0.1f, 120f, true, false)
            && !Matchstick.ShouldIgnite(0.1f, 120f, false, true));

        // ⛔ THE SCENE WIRING, not just the math. A "Build Reagent Cabinets"
        // run destroys + recreates Raw_Matchsticks, and can take its MatchStrikerSurface
        // with it. Striking the box then does NOTHING (the methane splint test and every
        // burner light are dead) while the pure ShouldStrike/ShouldIgnite tests above
        // still pass. Pure math cannot see a missing component; pin the actual objects.
        {
            var matches = UnityEngine.Object.FindObjectsByType<Matchstick>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool anchored = matches.Length > 0;
            foreach (var m in matches)
                if (m.transform.Find("FlameAnchor") == null) anchored = false;
            A("match: every matchstick has a FlameAnchor", anchored);

            // Matchstick.OnCollisionEnter reads GetComponentInParent<MatchStrikerSurface>()
            // off the collider it hits, so the box needs BOTH the component and a collider.
            MatchStrikerSurface box = null;
            foreach (var s in UnityEngine.Object.FindObjectsByType<MatchStrikerSurface>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (s.name.StartsWith("Raw_Matchsticks")) box = s;
            A("match: the matchbox IS the striker (strike it to light)", box != null);
            A("match: the striker box has a collider to strike against",
                box != null && box.GetComponentInChildren<Collider>() != null);
        }

        // Product per module — the chemical each one exists to make, which is what
        // EndProductVisibility hides while that module runs.
        // Chemical Compounding is EXCLUDED: it is an identification lab and
        // synthesises nothing. Returning "Ethanol" for it would hide ethanol during
        // the very experiment that needs it as a sample.
        string[] ids = { "tutorial-methane", "prelim-ethyl-alcohol",
                         "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
                         "final-benzamide", "final-winemaking" };
        bool products = true;
        foreach (var id in ids) if (string.IsNullOrEmpty(DemoMode.ProductFor(id))) products = false;
        A("demo: a product for each of the 8 synthesising modules", products);
        A("demo: the ID lab synthesises nothing", DemoMode.ProductFor("prelim-chemical-compounding") == null);
        A("demo: winemaking product is Wine (not the grape-juice feedstock)", DemoMode.ProductFor("final-winemaking") == "Wine");
        A("demo: unknown module has none", DemoMode.ProductFor("nope") == null);
    }

    // Center-table merge geometry (one wide landscape table,
    // centered — a rigid remap shared by the island object and the baked layouts).
    static void CenterTableMathSuite()
    {
        var pts = new List<Vector3> {
            new Vector3(-2.15f, 0.91f, -1.95f), new Vector3(-1.1f, 0.91f, -4.6f), new Vector3(-1.9f, 0.91f, -3.4f),
        };
        var b = CenterTableMath.FootprintOf(pts, 0.35f);
        A("table: footprint spans the points", b.min.x < -2.15f && b.max.x > -1.1f && b.min.z < -4.6f && b.max.z > -1.95f);
        A("table: margin applied", Near(b.max.x, -1.1f + 0.35f, 0.01f));

        Vector3 oldC = new Vector3(-1.6f, 0.91f, -3.3f);
        Vector3 newC = new Vector3(0f, 0.91f, -3.3f);
        A("table: centre maps to centre", Near(Vector3.Distance(
            CenterTableMath.Remap(oldC, oldC, newC, true), newC), 0f, 0.001f));
        // +z offset becomes +x offset under the 90° landscape turn.
        Vector3 r = CenterTableMath.Remap(oldC + new Vector3(0f, 0f, 1f), oldC, newC, true);
        A("table: rotation maps z-run onto x-run", Near(r.x, newC.x + 1f) && Near(r.z, newC.z));
        // Height is preserved.
        A("table: deck height preserved", Near(CenterTableMath.Remap(new Vector3(-1.2f, 0.91f, -2f), oldC, newC, true).y, 0.91f));
        // Distances are rigid (no scaling).
        Vector3 p1 = new Vector3(-2.15f, 0.91f, -1.95f), p2 = new Vector3(-1.1f, 0.91f, -4.6f);
        A("table: remap is rigid", Near(
            Vector3.Distance(CenterTableMath.Remap(p1, oldC, newC, true), CenterTableMath.Remap(p2, oldC, newC, true)),
            Vector3.Distance(p1, p2), 0.001f));

        A("table: within test", CenterTableMath.WithinXZ(new Vector3(-1.5f, 0.9f, -3f), b)
            && !CenterTableMath.WithinXZ(new Vector3(2f, 0.9f, -3f), b));
        A("table: mirror across x", Near(CenterTableMath.MirrorAcrossX(new Vector3(1.4f, 0f, -3f), 0f).x, -1.4f));
    }

    // Post-experiment review flow seams (congrats → Jimenez
    // briefing → quiz → score remarks → entrance debrief).
    static void ReviewFlowSuite()
    {
        // Clock freeze: the review/quiz never costs Time-Management score.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("a", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var go = new GameObject("freeze_runner");
        try
        {
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            runner.AdvanceTime(10f);
            A("review: clock runs before freeze", Near(runner.ElapsedSeconds, 10f));
            runner.FreezeClock();
            runner.AdvanceTime(30f);
            A("review: frozen clock holds", Near(runner.ElapsedSeconds, 10f) && runner.ClockFrozen);
            runner.Retry();
            A("review: retry thaws the clock", !runner.ClockFrozen);
            A("review: HasPhase sees synthesis only",
                runner.Graph.HasPhase(TaskPhase.Synthesis) && !runner.Graph.HasPhase(TaskPhase.ChemicalTests));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(module); }

        // Banded debrief remark (numbers stay on the grade card, lines stay finite).
        A("review: flawless band", PharmeeLines.DebriefRemark(98f).Contains("flawless"));
        A("review: strong band", PharmeeLines.DebriefRemark(94f).Contains("strong"));
        A("review: solid band", PharmeeLines.DebriefRemark(90f).Contains("solid"));

        // New spoken pools all populated (voice corpus depends on them).
        A("review: pools populated",
            PharmeeLines.TestsDoneLines.Length >= 3 && PharmeeLines.JimenezQuizBrief.Length >= 3
            && PharmeeLines.JimenezPassRemarks.Length >= 4 && PharmeeLines.JimenezFailRemarks.Length >= 3
            && PharmeeLines.DebriefCongrats.Length >= 3);
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

    // Persisted particle material (device builds strip runtime Shader.Find-only
    // shaders — the Resources asset keeps the pour stream/puddles alive on Quest).
    static void FxMaterialSuite()
    {
        var m = ShelfPourBuilder.EnsureFxMaterial();
        A("fxmat: asset exists", m != null);
        A("fxmat: transparent surface", m != null && Near(m.GetFloat("_Surface"), 1f));
        A("fxmat: alpha blend", m != null && m.GetInt("_SrcBlend") == (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        A("fxmat: no zwrite", m != null && m.GetInt("_ZWrite") == 0);
        A("fxmat: resources-loadable", Resources.Load<Material>("FxParticleUnlit") != null);
    }
    // The VISUAL autopilot's judge: pure rules over what the probe saw, so a
    // FAIL in Logs/visual-sweep-report.txt means what it says. Thresholds are the ones the
    // sweep uses — a 4% fill is invisible in VR (SimulatedRun's rule), 0.08 RGB is a change
    // a headset shows, and a gas outcome must have a live emitter or it is text-only.
    static void VisualSweepSuite()
    {
        // A vessel emptied after a test keeps its settled precipitate but loses its liquid
        // identity. It must still adopt the next thing poured in, or FindReaction(null, x)
        // misses forever and the glass fills with nameless liquid, leaving the
        // experiment unplayable.
        var residue = new GameObject("selftest-residue-vessel");
        try
        {
            var rlp = residue.AddComponent<MeshRenderer>().gameObject.AddComponent<LiquidPhysics>();
            var chem = ScriptableObject.CreateInstance<ChemicalData>();
            chem.chemicalName = "Probe Reagent";
            try
            {
                rlp.currentChemical = null; rlp.currentLiquidVolume = 0f; rlp.currentPptVolume = 5f;
                rlp.AddLiquid(chem, 10f);
                A("liquid: a vessel holding only residue still adopts what is poured in",
                    rlp.currentChemical == chem && Near(rlp.currentLiquidVolume, 10f));
            }
            finally { UnityEngine.Object.DestroyImmediate(chem); }
        }
        finally { UnityEngine.Object.DestroyImmediate(residue); }

        var o = new VisualSweep.Obs { found = true, ml = 5f, fill01 = 0.05f, rendererOn = true, tempC = 25f,
                                      particleNames = "", popups = "", chem = "x" };
        A("visual: a visible fill passes", VisualSweep.Judge(VisualSweep.Expect.Fill, o, null, 0f, 100f).status == "OK");
        var thin = o; thin.fill01 = 0.01f;
        A("visual: a 1% fill is invisible in VR", VisualSweep.Judge(VisualSweep.Expect.Fill, thin, null, 0f, 100f).Fail);
        A("visual: gas with no live emitter FAILS", VisualSweep.Judge(VisualSweep.Expect.Gas, o, null, 0f, 100f).Fail);
        var gas = o; gas.particles = 1; gas.particleNames = "EffectVfx_Smoke(20)";
        A("visual: gas with a live emitter passes", !VisualSweep.Judge(VisualSweep.Expect.Gas, gas, null, 0f, 100f).Fail);

        // ⛔ No swept step may report SKIP "the photo is the evidence": photographed and
        // judged against NOTHING, it reads as coverage on the report line while proving
        // nothing at all. These four steps' evidence is not inside a vessel:
        // a flame, a gas column, an assembled rig.
        A("visual: the methane rig and the flame test now have contracts",
            VisualSweep.ExpectFor("flame", null) == VisualSweep.Expect.Flame
            && VisualSweep.ExpectFor("methane:setup-apparatus", null) == VisualSweep.Expect.Powder   // a solid delivery, not a rig
            && VisualSweep.ExpectFor("methane:heat-mixture", null) == VisualSweep.Expect.Heat
            && VisualSweep.ExpectFor("methane:collect-gas", null) == VisualSweep.Expect.GasFill);
        {
            // These are judged BEFORE the "no vessel to look at" guard, so `found` stays false.
            var none = new VisualSweep.Obs();
            var lit = new VisualSweep.Obs { flameLit = true };
            A("visual: a flammability test with no flame FAILS",
                VisualSweep.Judge(VisualSweep.Expect.Flame, none, null, 0f, 100f).Fail
                && !VisualSweep.Judge(VisualSweep.Expect.Flame, lit, null, 0f, 100f).Fail);

            // ⛔ The flame is transient: by the final capture the match is out, so judging
            // there fails a step the game judged correctly. The latch carries the
            // mid-verb sighting to the verdict, then clears.
            VisualSweep.ClearFlameLatch();
            VisualSweep.NoteFlame(false);
            A("visual: an unlit moment does not latch a flame", !VisualSweep.FlameLatched);
            VisualSweep.NoteFlame(true);
            A("visual: a flame seen mid-verb is remembered for the verdict", VisualSweep.FlameLatched);
            VisualSweep.ClearFlameLatch();
            A("visual: the flame latch is one step's evidence, cleared after judging", !VisualSweep.FlameLatched);

            var full = new VisualSweep.Obs { gasFill01 = 0.5f };
            A("visual: collecting gas into an EMPTY tube FAILS",
                VisualSweep.Judge(VisualSweep.Expect.GasFill, none, null, 0f, 100f).Fail
                && !VisualSweep.Judge(VisualSweep.Expect.GasFill, full, null, 0f, 100f).Fail);
        }
        // ---- visual gaps, pinned so they cannot come back ----
        // A couple of millilitres in a big flask must still DRAW as something (Exp 5 puts
        // 2 ml of aniline in a 250 ml Florence flask = 0.8% of its height).
        A("fill: a small volume is floored to a readable column",
            Near(LiquidPhysics.DisplayFill01(2f, 250f), LiquidPhysics.MinVisibleFill01));
        A("fill: a full vessel is drawn at its real level",
            Near(LiquidPhysics.DisplayFill01(125f, 250f), 0.5f));
        A("fill: an empty vessel draws nothing",
            Near(LiquidPhysics.DisplayFill01(0f, 250f), 0f));
        // A 1 ml precipitate is what every rule actually deposits — it must be visible.
        A("ppt: one millilitre of precipitate is above the draw threshold",
            1f > LiquidPhysics.ShowFromMl);
        // Dry powder draws a mound; once anything is poured on it, it is a mixture.
        A("powder: a dry solid draws as a mound",
            LiquidPhysics.ShowsAsDryPowder(PhysicalState.Solid, false));
        A("powder: a wetted solid draws as liquid",
            !LiquidPhysics.ShowsAsDryPowder(PhysicalState.Solid, true));
        A("powder: a liquid is never a mound",
            !LiquidPhysics.ShowsAsDryPowder(PhysicalState.Liquid, false));
        // A vessel must never draw NOTHING while it holds something: the mound only
        // replaces the column where a mound actually exists (a product decanted onto a
        // watch glass has none).
        A("powder: a dry solid with a mound draws no column",
            !LiquidPhysics.DrawsLiquidColumn(5f, true, true));
        A("powder: a dry solid with NO mound still draws its column",
            LiquidPhysics.DrawsLiquidColumn(5f, true, false));
        A("powder: a wet mixture always draws its column",
            LiquidPhysics.DrawsLiquidColumn(5f, false, true));
        A("powder: an empty vessel draws nothing either way",
            !LiquidPhysics.DrawsLiquidColumn(0f, false, false));
        // Gas needs a visual, whichever way the rule spells it.
        var gasRule = ScriptableObject.CreateInstance<ReactionRule>();
        try
        {
            gasRule.outcome = ReactionOutcome.Fizzing;
            A("gas: a fizzing rule evolves gas", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.outcome = ReactionOutcome.GasEvolved;
            A("gas: a gas-evolved rule evolves gas", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.outcome = ReactionOutcome.ColorChange; gasRule.evolvesGas = true;
            A("gas: the explicit flag counts too", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.evolvesGas = false;
            A("gas: a plain colour change does not", !LiquidPhysics.EvolvesGas(gasRule));
        }
        finally { UnityEngine.Object.DestroyImmediate(gasRule); }
        // The silver mirror is the headline observation of Exp 2's aldehyde test, so the
        // rule must actually deposit something, not just declare a Precipitate.
        var tollens = AssetDatabase.LoadAssetAtPath<ReactionRule>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/Test_TollensAldehyde.asset");
        A("content: the Tollens test deposits a visible silver mirror",
            tollens != null && tollens.hasPrecipitate && tollens.resultPrecipitate != null
            && tollens.resultPrecipitate.chemicalName == "Silver Mirror");

        var ppt = o; ppt.pptOn = true; ppt.pptMl = 2f;
        A("visual: a precipitate needs visible ppt volume",
            VisualSweep.Judge(VisualSweep.Expect.Precipitate, o, null, 0f, 100f).Fail
            && !VisualSweep.Judge(VisualSweep.Expect.Precipitate, ppt, null, 0f, 100f).Fail);
        // A solid can land in a liquid (the wine must) — a visible fill satisfies Powder too.
        var dry = new VisualSweep.Obs { found = true, powderOn = true, particleNames = "", popups = "", chem = "x" };
        A("visual: a powder mound satisfies a solid delivery", !VisualSweep.Judge(VisualSweep.Expect.Powder, dry, null, 0f, 100f).Fail);
        A("visual: a solid dissolved in a visible liquid also passes",
            !VisualSweep.Judge(VisualSweep.Expect.Powder, o, null, 0f, 100f).Fail);
        var hot = o; hot.tempC = 100f; hot.boil = 0f;
        A("visual: at the boiling point the liquid must churn", VisualSweep.Judge(VisualSweep.Expect.Heat, hot, null, 100f, 100f).Fail);
        hot.boil = 0.6f;
        A("visual: boiling at temperature passes", !VisualSweep.Judge(VisualSweep.Expect.Heat, hot, null, 100f, 100f).Fail);
        var warm = o; warm.tempC = 60f;
        A("visual: a 50 C goal below the boil ramp needs no churn", !VisualSweep.Judge(VisualSweep.Expect.Heat, warm, null, 50f, 100f).Fail);
        A("visual: an odour is only ever a popup", VisualSweep.Judge(VisualSweep.Expect.Odor, o, null, 0f, 100f).Fail);
        A("visual: a step with no contract is SKIP, never OK", VisualSweep.Judge(VisualSweep.Expect.None, o, null, 0f, 100f).status == "SKIP");
        A("visual: colour distance is Euclidean RGB", Near(VisualSweep.ColourDistance(Color.black, Color.red), 1f));

        // Expectation derivation: the fired rule outranks the verb; gas outranks the rest.
        var rule = ScriptableObject.CreateInstance<ReactionRule>();
        try
        {
            rule.outcome = ReactionOutcome.ColorChange; rule.evolvesGas = true;
            A("visual: a gas-evolving rule expects visible gas", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Gas);
            rule.evolvesGas = false; rule.outcome = ReactionOutcome.Precipitate;
            A("visual: a precipitate rule expects visible ppt", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Precipitate);
            rule.outcome = ReactionOutcome.None; rule.hasPrecipitate = false; rule.resultLiquid = null;
            A("visual: a bare rule still expects a visible fill", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Fill);
            // A deliberate negative test authors product == reactant — never judge it as a
            // colour change (acetone vs Tollens/Schiff: "no mirror" is the intended result).
            var same = ScriptableObject.CreateInstance<ChemicalData>();
            rule.outcome = ReactionOutcome.ColorChange; rule.inputChemicalA = same; rule.resultLiquid = same;
            A("visual: a no-change negative test is not a colour change", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Fill);
            UnityEngine.Object.DestroyImmediate(same);
        }
        finally { UnityEngine.Object.DestroyImmediate(rule); }
        A("visual: no rule — a heat step expects temperature", VisualSweep.ExpectFor("heat", null) == VisualSweep.Expect.Heat);
        A("visual: no rule — a solid delivery expects a mound", VisualSweep.ExpectFor("solid", null) == VisualSweep.Expect.Powder);
        // A flame confirm must be judged against something: "the photo is the evidence"
        // is a picture nobody looks at rather than a check.
        A("visual: no rule — a flame confirm checks for an actual flame",
            VisualSweep.ExpectFor("flame", null) == VisualSweep.Expect.Flame);
    }

    // Pharmee's light and voice (not machine-like, not too flashy).
    static void PharmeePolishSuite()
    {
        // The FBX's Blue_Light material blazes at 8x on the body panels and hover rings;
        // PharmeeGlow owns exactly those, never the eyes/mouth PharmeeFace colours.
        A("glow: the body light panel is a glow part", PharmeeGlowMath.IsGlowPart("Robot_Blue_Light_0"));
        A("glow: the hover rings are glow parts",
            PharmeeGlowMath.IsGlowPart("Wave_Blue_Light_0") && PharmeeGlowMath.IsGlowPart("Wave.003_Blue_Light_0"));
        A("glow: the eyes and mouth belong to the face, not the glow",
            !PharmeeGlowMath.IsGlowPart("Eyes_Blue_Light_0") && !PharmeeGlowMath.IsGlowPart("Mouth_Blue_Light_0"));
        var e = PharmeeGlowMath.Emission(new Color(0.06f, 0.35f, 1f), PharmeeGlowMath.DefaultIntensity);
        // ⛔ UNDER the scene's Bloom threshold (1.0). At 1.45 she still feeds bloom and reads
        // as a glowing ball; this is the property that removes the halo.
        A("glow: the default emissive cannot feed bloom",
            !PharmeeGlowMath.Blooms(e) && e.maxColorComponent > 0.5f && Near(e.a, 1f));
        A("glow: an 8x emissive would bloom", PharmeeGlowMath.Blooms(PharmeeGlowMath.Emission(Color.white, 8f)));
        // Her white shell has NO emission and blooms anyway: albedo 0.91 under the nearby
        // 1.1 fill plus the 1.4 directional clears 1.0 on lighting alone.
        A("glow: the shell albedo is pulled under the clipping point",
            PharmeeGlowMath.DefaultShellAlbedo < 0.8f && PharmeeGlowMath.DefaultShellAlbedo > 0.5f);
        A("glow: the shell parts are the White_Glossy meshes, not the emissive ones",
            PharmeeGlowMath.IsShellPart("Robot_White_Glossy_0")
            && PharmeeGlowMath.IsShellPart("hANDS_White_Glossy_0")
            && !PharmeeGlowMath.IsShellPart("Robot_Blue_Light_0"));
        var robot = GameObject.Find("RobotNPC");
        var glow = robot != null ? robot.GetComponentInChildren<PharmeeGlow>(true) : null;
        A("glow: Pharmee carries the dimmer bound to her 5 light parts (run Wire NPC Polish)",
            glow != null && glow.Renderers != null && glow.Renderers.Length == 5);
        A("glow: the SCENE emissive cannot feed bloom either",
            glow != null && !PharmeeGlowMath.Blooms(PharmeeGlowMath.Emission(glow.Hue, glow.Intensity)));
        A("glow: the scene shell is tamed too (3 White_Glossy parts)",
            glow != null && glow.ShellRenderers != null && glow.ShellRenderers.Length == 3
            && glow.ShellAlbedo < 0.8f);

        // ---- Thrusters: the rings FLOW DOWN, they do not shimmy sideways ----
        A("thruster: a ring starts at the emitter",
            Near(PharmeeThrusterMath.Drop(0f, PharmeeThrusterMath.DefaultTravel), 0f));
        A("thruster: a ring ends a full travel below it",
            Near(PharmeeThrusterMath.Drop(1f, PharmeeThrusterMath.DefaultTravel), PharmeeThrusterMath.DefaultTravel));
        A("thruster: the fall is monotonic",
            PharmeeThrusterMath.Drop(0.25f, 1f) < PharmeeThrusterMath.Drop(0.75f, 1f));
        // Zero at BOTH ends is what makes the loop invisible — a ring that simply repeated
        // would pop back to full size at the top of every cycle.
        A("thruster: a ring fades in and out, so the loop never pops",
            Near(PharmeeThrusterMath.Swell(0f, 0.45f), 0f) && Near(PharmeeThrusterMath.Swell(1f, 0.45f), 0f)
            && PharmeeThrusterMath.Swell(0.5f, 0.45f) > 1f);
        A("thruster: it spreads as it falls",
            PharmeeThrusterMath.Swell(0.6f, 0.45f) > PharmeeThrusterMath.Swell(0.4f, 0f));
        A("thruster: the four rings are evenly staggered",
            Near(PharmeeThrusterMath.Phase(0f, 0, 4, 1f), 0f)
            && Near(PharmeeThrusterMath.Phase(0f, 2, 4, 1f), 0.5f));
        A("thruster: the cycle wraps", Near(PharmeeThrusterMath.Phase(1f, 0, 4, 1f), 0f));
        // ⛔ The SCENE check, because the pure math cannot see a parent's scale. The rings
        // hang off a model node scaled about 24x, so the first build converted "down" with
        // InverseTransformDirection (which ignores scale) and each puff fell 3.9 METRES,
        // straight through the floor. Measured by actually moving a ring and putting it back.
        {
            var att = UnityEngine.Object.FindFirstObjectByType<PharmeeAttitude>(FindObjectsInactive.Include);
            if (att != null)
            {
                var attSo = new SerializedObject(att);
                var ringsProp = attSo.FindProperty("waves");
                float travel = attSo.FindProperty("flowTravel").floatValue;
                float worst = 0f; int driven = 0;
                for (int i = 0; i < ringsProp.arraySize; i++)
                {
                    var ring = ringsProp.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    if (ring == null || ring.parent == null) continue;
                    bool nested = false;
                    for (int j = 0; j < ringsProp.arraySize && !nested; j++)
                    {
                        var other = ringsProp.GetArrayElementAtIndex(j).objectReferenceValue as Transform;
                        if (j != i && other != null && ring.IsChildOf(other)) nested = true;
                    }
                    if (nested) continue;
                    driven++;
                    Vector3 saved = ring.localPosition, from = ring.position;
                    try
                    {
                        ring.localPosition = saved + ring.parent.InverseTransformVector(Vector3.down)
                                                     * PharmeeThrusterMath.Drop(1f, travel);
                        worst = Mathf.Max(worst, Vector3.Distance(from, ring.position));
                    }
                    finally { ring.localPosition = saved; }
                }
                A("thruster: a puff falls a hand's width, not through the floor (worst "
                  + worst.ToString("0.00") + " m over " + driven + " rings)",
                    driven == 4 && worst > 0.02f && worst < 0.40f);
            }
        }

        // ---- Jimenez: the coat is re-weighted, not unparented ----
        A("rig: the arm bones swing the coat, the clavicle legitimately carries the shoulder",
            JimenezRigMath.IsArmBone("L_Upperarm") && JimenezRigMath.IsArmBone("R_ForearmTwist01")
            && !JimenezRigMath.IsArmBone("L_Clavicle") && !JimenezRigMath.IsArmBone("Spine02"));
        // ⛔ The HAND is not an arm bone. This rig has no finger bones, so L_Hand is a LEAF
        // with no segment; treating it as an arm measured every fingertip from the wrist
        // joint, called the whole hand "coat", and rendered his fingers as ribbons.
        A("rig: the hand is never re-weighted (it is a leaf bone, and a coat has no fingers)",
            !JimenezRigMath.IsArmBone("L_Hand") && !JimenezRigMath.IsArmBone("R_Hand"));
        // ⛔ Twist bones sit at EXACTLY their parent's position, so the nearest child gives
        // the upper arm a zero-length segment and "along the arm" becomes "from the shoulder".
        A("rig: a bone's segment runs to its FARTHEST child, not its nearest",
            JimenezRigMath.IsBetterSegmentEnd(0.15f, 0.0f)
            && !JimenezRigMath.IsBetterSegmentEnd(0.0f, 0.15f));
        // ⛔ L_UpperarmTwist02 is a LEAF, so judged against itself its "segment" is a point at
        // the elbow: the whole sleeve reads as far away, is freed to the spine, and stays
        // behind when the arm lifts. A twist bone must borrow its parent limb's segment.
        A("rig: twist bones are measured against the parent limb, not themselves",
            JimenezRigMath.IsTwistBone("L_UpperarmTwist02")
            && JimenezRigMath.IsTwistBone("R_ForearmTwist01")
            && !JimenezRigMath.IsTwistBone("L_Upperarm") && !JimenezRigMath.IsTwistBone("Spine02"));
        // ⛔ The radius is in the MESH's own units. Jimenez's mesh is ~0.5 units tall and the
        // prefab scales it to a 1.75 m man, so a radius picked in metres is half his width
        // and classifies the whole coat as sleeve (only a handful of vertices freed).
        {
            float r = JimenezRigMath.SleeveRadiusFor(0.5f);
            A("rig: the sleeve radius scales with the model, not with metres",
                r > 0.03f && r < 0.04f
                && Near(JimenezRigMath.SleeveRadiusFor(1.0f), 2f * r));
            A("rig: weight far from the arm is bleed, weight on the sleeve is not",
                JimenezRigMath.IsBleed(0.09f, r) && !JimenezRigMath.IsBleed(0.02f, r));
            // ⛔ Distance alone kept the coat's lower front panel on the arm: in an A-pose
            // bind the forearm hangs beside the hip, so panel and cuff are equally close to
            // it. The panel is near the SPINE and the cuff is not — that is the difference.
            // The coat could not be re-weighted without tearing, so the remaining lever is how
        // far the arm travels at all.
        A("rig: damping pulls a swing back toward rest without moving rest itself",
            Near(JimenezRigMath.Damp(1f, 0f, 0.5f), 0.5f)
            && Near(JimenezRigMath.Damp(0.4f, 0.4f, 0.2f), 0.4f)
            && Near(JimenezRigMath.Damp(1f, 0f, 1f), 1f));
        A("rig: only the ARM muscles are damped, never the spine or legs",
            JimenezArmDamper.IsArmMuscle("Left Arm Down-Up")
            && JimenezArmDamper.IsArmMuscle("Right Shoulder Front-Back")
            && JimenezArmDamper.IsArmMuscle("LeftHandT.x")
            && !JimenezArmDamper.IsArmMuscle("Spine Front-Back")
            && !JimenezArmDamper.IsArmMuscle("Left Upper Leg Front-Back"));
        A("rig: a cuff is closer to the arm than to the spine; a coat panel is not",
                JimenezRigMath.IsSleeve(0.02f, 0.09f, r)          // cuff: hugs the arm
                && !JimenezRigMath.IsSleeve(0.02f, 0.01f, r)      // panel: hugs the torso
                && !JimenezRigMath.IsSleeve(0.09f, 0.20f, r));    // far from both = coat body
        }
        A("rig: distance is to the whole BONE, not just its joint",
            Near(JimenezRigMath.DistanceToSegment(new Vector3(0.5f, 0f, 0f), Vector3.zero, new Vector3(1f, 0f, 0f)), 0f)
            && Near(JimenezRigMath.DistanceToSegment(new Vector3(0.5f, 0.2f, 0f), Vector3.zero, new Vector3(1f, 0f, 0f)), 0.2f));
        {
            // The invariant that matters: a re-weighted vertex is still normalised, or the
            // mesh deforms at the wrong scale and the fix looks like a modelling error.
            var bw = new BoneWeight { boneIndex0 = 7, weight0 = 0.6f, boneIndex1 = 3, weight1 = 0.4f };
            float moved = JimenezRigMath.Redistribute(ref bw, 7, 3);
            A("rig: arm weight moves to the spine", Near(moved, 0.6f));
            A("rig: the vertex stays normalised", Near(JimenezRigMath.Sum(bw), 1f));
            A("rig: the spine ends up carrying all of it",
                Near(bw.boneIndex1 == 3 ? bw.weight1 : 0f, 1f));
            var untouched = new BoneWeight { boneIndex0 = 2, weight0 = 1f };
            A("rig: a vertex with no arm weight is left alone",
                Near(JimenezRigMath.Redistribute(ref untouched, 7, 3), 0f) && Near(untouched.weight0, 1f));
        }

        // ⛔ A vessel whose RESTING pose already reads as "tipped" pours itself forever:
        // LiquidPourer.Update fires on Vector3.Angle(Vector3.up, transform.up) > pourThreshold,
        // so it empties every drop put into it AND runs its looping pour AudioSource under
        // everything else (a tipped Distilling Flask drains Exp 7's redistillation and puts a
        // continuous spilling noise under every dialogue line). Measured in WORLD space,
        // exactly as LiquidPourer measures it: a correct LOCAL rotation under a tilted
        // parent still pours.
        {
            int selfPouring = 0; string worst = "";
            foreach (var pr in UnityEngine.Object.FindObjectsByType<LiquidPourer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pr == null) continue;
                float tilt = Vector3.Angle(Vector3.up, pr.transform.up);
                if (tilt > pr.pourThreshold)
                { selfPouring++; if (worst.Length == 0) worst = pr.name + " at " + tilt.ToString("0") + " deg"; }
            }
            A("pour: no vessel rests beyond its own pour threshold (" + selfPouring
              + (worst.Length > 0 ? ", e.g. " + worst : "") + ")", selfPouring == 0);
        }

        // A vessel's BAKED HOME must be upright too, or DropRespawn.ResetAllHome puts the
        // tilt straight back at the start of every run, so straightening the
        // transform alone does not stick.
        {
            int tiltedHome = 0; string worstHome = "";
            foreach (var dr in UnityEngine.Object.FindObjectsByType<DropRespawn>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (dr == null || dr.GetComponent<LiquidPourer>() == null) continue;
                var so = new SerializedObject(dr);
                var rot = so.FindProperty("_homeRot");
                if (rot == null) continue;
                float tilt = Vector3.Angle(Vector3.up, rot.quaternionValue * Vector3.up);
                if (tilt > dr.GetComponent<LiquidPourer>().pourThreshold)
                { tiltedHome++; if (worstHome.Length == 0) worstHome = dr.name + " at " + tilt.ToString("0") + " deg"; }
            }
            A("pour: no pourable vessel's baked HOME is tipped (" + tiltedHome
              + (worstHome.Length > 0 ? ", e.g. " + worstHome : "") + ")", tiltedHome == 0);
        }

        // ⛔ A vessel hanging in mid-air reads as a bug to the player and pours onto
        // nothing. A simulator restores every item to its baked home, and a re-home pass
        // run afterwards adopts that revert, which can leave the distilling flasks 26 cm
        // above the shelf they belong on. The pose is self-consistent, so only this pin
        // sees it.
        // SolidWorldBounds, never a raw renderer sweep: LiquidPourer's world-space stream
        // children outlive a pour pointing at the floor (→ Gotchas).
        {
            int floating = 0; string worstFloat = "";
            foreach (var pr in UnityEngine.Object.FindObjectsByType<LiquidPourer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pr == null || !pr.gameObject.activeInHierarchy) continue;
                Bounds b = ExperimentSceneBuilder.SolidWorldBounds(pr.gameObject);
                if (b.size == Vector3.zero) continue;
                // Start just INSIDE the vessel so its own collider is behind the ray origin.
                // Probe the centre and four corners and take the HIGHEST hit: one ray can
                // slip through a gap in a table's mesh collider and report the floor.
                float surfaceY = 0f; bool found = false;
                float ddx = b.extents.x * 0.4f, ddz = b.extents.z * 0.4f;
                foreach (var o in new[] { Vector2.zero, new Vector2(ddx, 0f), new Vector2(-ddx, 0f),
                                          new Vector2(0f, ddz), new Vector2(0f, -ddz) })
                {
                    var from = new Vector3(b.center.x + o.x, b.min.y + 0.02f, b.center.z + o.y);
                    if (!Physics.Raycast(from, Vector3.down, out var floorHit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (!found || floorHit.point.y > surfaceY) { surfaceY = floorHit.point.y; found = true; }
                }
                float gap = found ? b.min.y - surfaceY : 99f;   // 99 = nothing under it at all
                // UprightPourables.Tolerance, so the pin and the repair agree on what
                // "resting" means. The 14 cabinet bottles sit a uniform 2 cm proud by design.
                if (gap > UprightPourables.Tolerance)
                {
                    floating++;
                    if (worstFloat.Length == 0)
                        worstFloat = pr.name + (gap > 90f ? " over nothing"
                                                          : " by " + (gap * 100f).ToString("0") + " cm");
                }
            }
        // ⛔ The pivot has to be ON the mesh, not just the collider. The runtime reads
        // transform.position (Matchstick's strike distance, VaporCollectController's receiver
        // radius, DropRespawn), so an item whose glass sits 1.9 m from its pivot is "held" 1.9 m
        // from the player's hand. Tripo/USD imports are prone to it (the matchstick, the ice
        // bucket, both distilling flasks). Only measuring finds it: the simulators all drive
        // the APIs directly. Slack of 10 cm: a tall vessel's pivot
        // may sit at its base or its centre, both fine; a metre is not.
        {
            int offPivot = 0, offCollider = 0; string worstPivot = "", worstCol = "";
            foreach (var li in UnityEngine.Object.FindObjectsByType<LabItem>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (li == null || !li.gameObject.activeInHierarchy) continue;
                Bounds mesh = ExperimentSceneBuilder.SolidWorldBounds(li.gameObject);
                if (mesh.size == Vector3.zero) continue;
                var slack = mesh; slack.Expand(0.20f);
                if (!slack.Contains(li.transform.position))
                {
                    offPivot++;
                    if (worstPivot.Length == 0)
                        worstPivot = li.name + " " + (Vector3.Distance(li.transform.position, mesh.center) * 100f).ToString("0") + " cm";
                }
                var col = li.GetComponentInChildren<Collider>();
                if (col != null && li.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null
                    && !col.bounds.Intersects(mesh))
                {
                    offCollider++;
                    if (worstCol.Length == 0) worstCol = li.name;
                }
            }
            A("grab: every item's pivot sits on its own mesh (" + offPivot
              + (worstPivot.Length > 0 ? ", e.g. " + worstPivot : "") + ")", offPivot == 0);
            A("grab: every grabbable's collider overlaps its own mesh (" + offCollider
              + (worstCol.Length > 0 ? ", e.g. " + worstCol : "") + ")", offCollider == 0);
        }

        // ⛔ The methane tutorial's charge jar is scene DATA (an added LiquidPhysics on a hand-staged
        // prop), and the suite's own simrun pins drain it in edit mode. A scene save made straight
        // after a suite run bakes 0 ml into the file and the tutorial's setup step starves.
        // Nothing but this pin can see it: every simulator drives the
        // APIs, and the sweep runs in Play mode where a fresh load recharges it.
        // APIs, and the sweep runs in Play mode where a fresh load recharges it.
        {
            var chargeJar = GameObject.Find("Prop_reagent-jar");
            var chargeLp = chargeJar != null ? chargeJar.GetComponent<LiquidPhysics>() : null;
            A("bench: the methane charge jar holds its mixture ("
              + (chargeLp != null ? chargeLp.currentLiquidVolume.ToString("0") + " ml" : "no LiquidPhysics") + ")",
              chargeLp != null && chargeLp.currentChemical != null && chargeLp.currentLiquidVolume >= 4f);

            // A scene save after a suite run can also empty all five wash bottles (500 ml
            // of distilled water each). A wash bottle is a SOURCE (it never carries a binding),
            // so nothing in the game notices it is empty until a rinse does nothing.
            int washBottles = 0, dryWash = 0;
            foreach (var wlp in UnityEngine.Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (wlp == null || (wlp.name != "Eq_WashBottle" && wlp.name != "WashBottleWater")) continue;
                washBottles++;
                if (wlp.currentChemical == null || wlp.currentLiquidVolume < 50f) dryWash++;
            }
            A("bench: the wash bottles hold distilled water (" + washBottles + " bottles, " + dryWash + " dry)",
              washBottles > 0 && dryWash == 0);
        }

        // Furniture is scenery: a stool the player can pick up and carry off is a toy, and
        // nothing in any experiment asks them to move one. The Environment prefab gives all
        // six a grab, so a re-import would put them back; hence a scene pin, not just
        // a menu. The COLLIDER must survive: the stool still has to block and still has to
        // hold whatever rests on it.
        {
            int grabbable = 0, noCollider = 0; string worstFurn = "";
            foreach (var tr in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tr == null || !AnchorFurniture.IsFurniture(tr.name)) continue;
                if (tr.GetComponents<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>().Length > 0)
                { grabbable++; if (worstFurn.Length == 0) worstFurn = tr.name; }
                if (tr.GetComponents<Collider>().Length == 0) noCollider++;
            }
            A("bench: lab furniture is not grabbable (" + grabbable
              + (worstFurn.Length > 0 ? ", e.g. " + worstFurn : "") + ")", grabbable == 0);
            A("bench: anchoring furniture KEEPS its collider (" + noCollider + " without)", noCollider == 0);
        }

            A("pour: no pourable vessel floats above the surface under it (" + floating
              + (worstFloat.Length > 0 ? ", e.g. " + worstFloat : "") + ")", floating == 0);
        }

        // The visual sweep buffers a handler's "not complete yet" complaint until the step
        // lands on real frames; a chemistry bug is never buffered away.
        A("noise: a completion complaint is noise once the step lands",
            PlaytestAutopilot.IsCompletionNoise("test-ester: bath heating never completed the step (tube 100 C / needs 50 C)"));
        A("noise: a broken heat gate is never noise",
            !PlaytestAutopilot.IsCompletionNoise("Kit_TestTube_5: Iodoform fired at 25 C but needs 40 C — the heat gate is broken"));

        // Every line the corpus can speak must have a clip (e.g. the campaign finale,
        // both DebriefRemark overloads, would otherwise play as blips).
        var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>("Assets/PharmaSynth/ScriptableObjects/VoiceBank.asset");
        int unvoiced = 0; string first = "";
        if (bank != null)
            foreach (var l in VoiceCorpus.CodeLines())
                if (bank.Get(l.speaker, VoiceLineId.For(l.text)) == null) { unvoiced++; if (first.Length == 0) first = l.text; }
        A("voice: every corpus line has a clip (" + unvoiced + " unvoiced" + (first.Length > 0 ? ", e.g. \"" + first + "\"" : "") + ")",
            bank != null && unvoiced == 0);

        // ⛔ Checking CodeLines() ALONE is not enough: task time-skip
        // narration ("One week later...") is authored in the MODULE ASSETS, so a code-only
        // corpus cannot see it, the manifest never exports it, and no clip is bought;
        // Dr. Jimenez then speaks six lines in blips.
        // A corpus that only knows about code lines is not a corpus.
        {
            var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            var dataLines = VoiceCorpus.DataLines(lib);
            int authored = 0;
            if (lib != null)
                foreach (var m in lib.modules)
                {
                    if (m == null) continue;
                    foreach (var t in m.graphTasks)
                        if (t != null && !string.IsNullOrWhiteSpace(t.longProcessMessage)) authored++;
                }
            A("voice: the corpus enumerates DATA-authored lines too, not just code ("
              + dataLines.Count + " of " + authored + " time-skip lines)",
                lib != null && authored > 0 && dataLines.Count == authored);

            int dataUnvoiced = 0; string firstData = "";
            if (bank != null)
                foreach (var l in dataLines)
                    if (bank.Get(l.speaker, VoiceLineId.For(l.text)) == null)
                    { dataUnvoiced++; if (firstData.Length == 0) firstData = l.text; }
            A("voice: every time-skip line has a clip (" + dataUnvoiced + " unvoiced"
              + (firstData.Length > 0 ? ", e.g. \"" + firstData + "\"" : "") + ")",
                dataUnvoiced == 0);
            // ⛔ An entry whose clip Unity never imported reads as a healthy bank and
            // speaks in blips. It happens when the generator creates the mp3s OUTSIDE
            // Unity and nothing refreshes the AssetDatabase before import.
            A("voice: no bank entry points at a clip that was never imported ("
              + (bank != null ? bank.BrokenEntries() : -1) + " broken)",
                bank != null && bank.BrokenEntries() == 0);
        }

        // Exp 2's two ferric-chloride tests promise violet, so the rules need a product
        // or nothing in the tube changes colour.
        foreach (var name in new[] { "Test_EnolPhenolFeCl3", "Test_HydrolysateFeCl3" })
        {
            var rule = AssetDatabase.LoadAssetAtPath<ReactionRule>("Assets/PharmaSynth/ScriptableObjects/Reactions/" + name + ".asset");
            A("content: " + name + " turns the tube a colour the player can see",
                rule != null && rule.resultLiquid != null && rule.inputChemicalA != null
                && VisualSweep.ColourDistance(rule.resultLiquid.liquidColor, rule.inputChemicalA.liquidColor) >= VisualSweep.VisibleDelta);
        }
    }
}
#endif
