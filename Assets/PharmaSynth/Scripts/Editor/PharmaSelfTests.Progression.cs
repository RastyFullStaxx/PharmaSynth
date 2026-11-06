#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Save data, campaign flow, settings, demo mode and result export.
public static partial class PharmaSelfTests
{
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
}
#endif
