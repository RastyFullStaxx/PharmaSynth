#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Pharmee, the gatekeeper flow, the examiner, voice, audio and cutscenes.
public static partial class PharmaSelfTests
{
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

    // CINEMATIC CUTSCENES (client slides cutscenes-sample.pdf): a shot
    // camera films the lab onto a screen in a dark room; the XR camera is never moved.
    static void CinemaSuite()
    {
        // The screen stays in front of the viewer.
        {
            Vector3 head = Vector3.zero, fwd = Vector3.forward;
            CinemaView.ScreenPose(head, fwd, ScreenMath.DefaultDistance, out var scrP, out var scrR);
            Vector3 scrF = scrR * Vector3.forward;
            A("cine: a freshly placed screen needs no re-centre",
                !ScreenMath.NeedsRecentre(head, fwd, scrP, scrF, ScreenMath.DefaultDistance));
            A("cine: a viewer behind the screen gets it back in front (no mirrored caption)",
                ScreenMath.NeedsRecentre(scrP + scrF * 1f, fwd, scrP, scrF, ScreenMath.DefaultDistance));
            A("cine: a viewer teleported far away gets it back",
                ScreenMath.NeedsRecentre(new Vector3(0f, 0f, -6f), fwd, scrP, scrF, ScreenMath.DefaultDistance));
            A("cine: a screen left off to the side is re-centred",
                ScreenMath.NeedsRecentre(head, Vector3.right, scrP, scrF, ScreenMath.DefaultDistance));
            A("cine: a small head turn keeps the screen where it is",
                !ScreenMath.NeedsRecentre(head, Quaternion.Euler(0f, 30f, 0f) * fwd, scrP, scrF, ScreenMath.DefaultDistance));
            float spanDeg = 2f * Mathf.Atan(1.7f / ScreenMath.DefaultDistance) * Mathf.Rad2Deg;
            A("cine: the 3.4 m screen spans 70-90 degrees from its distance", spanDeg > 70f && spanDeg < 90f);
            var cv = UnityEngine.Object.FindAnyObjectByType<CinemaView>(FindObjectsInactive.Include);
            A("cine: the scene's screen sits at the close distance",
                cv != null && Mathf.Abs(cv.ScreenDistance - ScreenMath.DefaultDistance) < 0.01f);
        }
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
        A("timelapse: game time visibly speeds up during the shot", TimelapseMath.DefaultSpeed >= 4f);
        A("timelapse: nothing else in the game changes the time scale", Mathf.Approximately(Time.timeScale, 1f));
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
        var e = PharmeeGlowMath.Emission(PharmeeGlowMath.CyanHue, PharmeeGlowMath.DefaultIntensity);
        // ⛔ UNDER the scene's Bloom threshold (1.0). At 1.45 she still feeds bloom and reads
        // as a glowing ball; this is the property that removes the halo.
        A("glow: the default emissive cannot feed bloom",
            !PharmeeGlowMath.Blooms(e) && e.maxColorComponent > 0.5f && Near(e.a, 1f));
        A("glow: an 8x emissive would bloom", PharmeeGlowMath.Blooms(PharmeeGlowMath.Emission(Color.white, 8f)));
        // The talk pulse breathes visibly and never reaches bloom at its peak.
        {
            float lo = float.MaxValue, hi = 0f, peakEm = 0f;
            for (int i = 0; i <= 200; i++)
            {
                float t = i / (200f * PharmeeGlowMath.TalkPulseHz);
                float v = PharmeeGlowMath.TalkIntensity(PharmeeGlowMath.DefaultIntensity, t, 1f);
                lo = Mathf.Min(lo, v); hi = Mathf.Max(hi, v);
                peakEm = Mathf.Max(peakEm, PharmeeGlowMath.Emission(PharmeeGlowMath.CyanHue, v).maxColorComponent);
            }
            A("glow: the talk pulse peaks below bloom", peakEm < 1f);
            A("glow: the talk pulse is visible (swings at least 0.3 of base)",
                hi - lo >= 0.3f * PharmeeGlowMath.DefaultIntensity);
            A("glow: silent, the light holds steady",
                Near(PharmeeGlowMath.TalkIntensity(0.7f, 0.37f, 0f), 0.7f, 1e-4f));
            A("glow: the hue is cyan blue (green and blue lead, red low)",
                PharmeeGlowMath.CyanHue.g > 0.6f && PharmeeGlowMath.CyanHue.b >= PharmeeGlowMath.CyanHue.g
                && PharmeeGlowMath.CyanHue.r < 0.3f);
        }
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
        A("glow: the scene's panel and rings are cyan (run Wire NPC Polish)",
            glow != null && Vector4.Distance(glow.Hue, PharmeeGlowMath.CyanHue) < 0.01f);
        {
            var faceCmp = robot != null ? robot.GetComponentInChildren<PharmeeFace>(true) : null;
            Color faceHappy = Color.black;
            if (faceCmp != null) faceHappy = new UnityEditor.SerializedObject(faceCmp).FindProperty("happy").colorValue;
            A("glow: the scene face is cyan too, under the face ceiling",
                faceCmp != null && faceHappy.g > 3f * faceHappy.r && faceHappy.maxColorComponent <= PharmeeGlowMath.FaceCeiling + 1e-3f);
        }
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
            var att = UnityEngine.Object.FindAnyObjectByType<PharmeeAttitude>(FindObjectsInactive.Include);
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
                         FindObjectsInactive.Include))
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
                         FindObjectsInactive.Include))
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
                         FindObjectsInactive.Include))
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
                         FindObjectsInactive.Include))
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
            foreach (var wlp in UnityEngine.Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
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
                         FindObjectsInactive.Include))
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
