using System;
using System.Collections.Generic;
using UnityEngine;

/// After the grade: pass and fail exits, the lab reset and the trip home.
public partial class PharmeeGatekeeper
{
    // ---- return loop ---------------------------------------------------------

    /// GradeScreen Continue (pass-gated) — begin the debrief + return home.
    public void OnContinueAfterPass() => Model.Fire(GateEvent.ContinueAfterPass);

    /// GradeScreen "Choose Another" (fail path): returns a failed player to the entrance
    /// (full lab reset, no congratulatory debrief) so they can pick any unlocked
    /// experiment at the door, not only Retry.
    public void OnAbandonAfterFail()
    {
        if (Model.State != GateState.ScoreReview) return;
        var grade = UnityEngine.Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (grade != null) grade.Hide();
        postLab?.Close();
        DoFaded(() =>
        {
            ResetLabForReturn();
            Model.Fire(GateEvent.AbandonRun);   // ScoreReview → Blocked
            SpeakWelcome();
        });
    }

    /// Pharmee's spawn/entrance greeting (scene load + after a HUD reset).
    public void SpeakWelcome()
    {
        Say(lines.welcome);   // the greeting is voiced — no buzzer under it

        // Tutorial Mode drops the player into the SAME lab with no sign anything is
        // different — glowing bottles and a floating arrow read as a bug until someone
        // says what they are. Spoken once per entrance, after the normal greeting.
        if (TutorialSession.Active)
            After(lineSeconds * 0.9f, () => Say(PharmeeLines.TutorialOrientation));
    }

    /// HUD Reset: everything in the lab returns to its original
    /// spawn — props/bottles re-seated, the wearables taken off — the player is
    /// teleported back to the entrance where Pharmee waits, the gate re-closes, and
    /// Pharmee greets. Wrapped in the black fade like every other transition.
    public void ResetToEntrance()
    {
        void Do()
        {
            // Actually END the attempt (the confirm text promises it). Otherwise a mid-run reset
            // leaves a zombie run ticking and a mid-review one leaves the grade card / quiz
            // tablet floating at the entrance.
            if (runner != null && runner.IsRunning) runner.Abort();
            var grade = UnityEngine.Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
            if (grade != null) grade.Hide();
            postLab?.Close();
            string id = runner != null && runner.Module != null ? runner.Module.moduleId : GameFlow.SelectedModuleId;
            TaskTargetRegistry.Clear();
            launcher?.Launch(id, LaunchMode.StageOnly);   // props/bottles back to original spawns
            DropRespawn.ResetAllHome();                    // hand-placed apparatus + reagents back home too
            RestorePharmeeHome();                          // a mid-review reset must not strand him by the tablet
            // Wearables back on their pegs (+ worn PPE stripped) so the next campaign is dressable.
            if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
            else if (ppe != null) ppe.RemovePPE();        // wearables off
            TeleportToFrontDoor();                         // rig back to the entrance by Pharmee
            Model.ResetToBlocked();                        // door re-closes; player must re-approach
            SpeakWelcome();
        }
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(Do);
        else
            Do();
    }

    /// GradeScreen Retry. From the review corner (the normal fail path) this is a
    /// CLEAN re-armed attempt: back to the door, stage rebuilt, walk-in timer —
    /// consistent with the supply-restart path. Outside the review flow (dev
    /// shortcuts) it keeps the legacy in-place FullStart.
    public void OnRetryRequested()
    {
        var grade = UnityEngine.Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (grade != null) grade.Hide();
        if (Model.State == GateState.ScoreReview)
        {
            _returnOnLoad = true;                        // teleport home inside the load fade
            Model.Fire(GateEvent.RetryRequested);        // → Loading → armed at the door
            return;
        }
        var mod = runner != null ? runner.Module : null;
        string id = mod != null ? mod.moduleId : GameFlow.SelectedModuleId;
        TaskTargetRegistry.Clear();
        void Relaunch() { launcher?.Launch(id, LaunchMode.FullStart); DropRespawn.ResetAllHome(); }
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(Relaunch);
        else
            Relaunch();
    }

    /// Put Dr. Jimenez back at his authored review position, facing the player, and stop
    /// him roaming. The quiz begins while the run is STILL live (Finish only lands on
    /// submit), so ProctorRoamer's own come-home-on-finish hook fires far too late.
    private void StageExaminerForReview()
    {
        var roamer = examiner != null ? examiner.GetComponent<ProctorRoamer>() : null;
        if (roamer == null)
            roamer = UnityEngine.Object.FindAnyObjectByType<ProctorRoamer>(FindObjectsInactive.Include);
        if (roamer == null) return;
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        roamer.ReturnHomeAndHold(cam != null ? cam.transform : playerRig);
    }

    private void StagePharmeeForReview()
    {
        var body = PharmeeBody();
        if (body == null || reviewCornerSpawn == null || postLab == null) return;
        if (!_pharmeeParked)
        { _pharmeeHomePos = body.position; _pharmeeHomeRot = body.rotation; _pharmeeParked = true; }

        Vector3 viewer = reviewCornerSpawn.position;
        Vector3 tablet = postLab.transform.position;
        Vector3 toTablet = tablet - viewer; toTablet.y = 0f;
        if (toTablet.sqrMagnitude < 1e-4f) return;
        toTablet.Normalize();
        Vector3 left = new Vector3(-toTablet.z, 0f, toTablet.x);   // player's left of the sightline
        // Beside the tablet and a touch nearer the player, well clear of the panel.
        Vector3 spot = tablet + left * pharmeeReviewOffset - toTablet * 0.5f;
        spot.y = body.position.y;
        body.position = spot;
        Vector3 face = viewer - spot; face.y = 0f;
        if (face.sqrMagnitude > 1e-4f) body.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
    }

    /// Put him back on the door after the review (called by the lab reset).
    private void RestorePharmeeHome()
    {
        var body = PharmeeBody();
        if (body == null || !_pharmeeParked) return;
        body.position = _pharmeeHomePos;
        body.rotation = _pharmeeHomeRot;
        _pharmeeParked = false;
    }

    /// Pharmee's physical body — the robot this gatekeeper drives. Serialized when
    /// the gatekeeper lives elsewhere; otherwise this component's own transform.
    private Transform PharmeeBody() => pharmeeBody != null ? pharmeeBody : transform;

    /// Jimenez's quiz briefing at the review corner, then the quiz opens.
    /// Beats 0-1 = the generic brief; then he RECAPS this module's manuscript learning
    /// outcomes before the questions. Jimenez's pools are module-blind, so this is the
    /// one place he says something specific to the experiment just performed.
    private void SpeakJimenezBrief(int beat)
    {
        if (Model.State != GateState.QuizIntro) return;
        var ilos = BriefObjectives();
        if (beat == 2 && ilos.Length > 0 && examiner != null)
        {
            SpeakAsJimenez(PharmeeLines.JimenezIloLeadIn, beat);
            return;
        }
        int iloIndex = beat - 3;
        if (iloIndex >= 0 && iloIndex < ilos.Length)
        {
            SpeakAsJimenez(ilos[iloIndex], beat);
            return;
        }
        if (beat >= 2) { Model.Fire(GateEvent.QuizBegin); return; }
        SpeakAsJimenez(PharmeeLines.Pick(PharmeeLines.JimenezQuizBrief, beat), beat);
    }

    /// Speak one of Jimenez's review beats and schedule the next off the line's REAL
    /// duration. A fixed `lineSeconds` under-schedules anything longer than
    /// (lineSeconds x reveal-speed) characters; his ILO recap lines are ~140 chars, so
    /// the next beat would start while he is still mid-sentence.
    private void SpeakAsJimenez(string line, int beat)
    {
        float wait = lineSeconds;
        if (examiner != null)
        {
            examiner.SpeakLine(line);
            wait = Mathf.Max(lineSeconds, examiner.SecondsFor(line));
        }
        After(wait + 0.25f, () => SpeakJimenezBrief(beat + 1));
    }

    /// The running module's Intended Learning Outcomes, manuscript-verbatim.
    /// Prefers the authored asset field, falling back to IloCopy (the same
    /// Appendix C source the intro cutscene beats are injected from).
    private string[] BriefObjectives()
    {
        var m = runner != null ? runner.Module : null;
        if (m == null) return new string[0];
        if (m.intendedLearningOutcomes != null && m.intendedLearningOutcomes.Count > 0)
            return m.intendedLearningOutcomes.ToArray();
        return IloCopy.ForModule(m.moduleId);
    }

    /// Hold the spoken score remarks until the Success/Failure outro cutscene ends.
    private void WaitOutroThenRemarks()
    {
        if (Model.State != GateState.ScoreReview) return;
        var director = UnityEngine.Object.FindAnyObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
        if (Application.isPlaying && director != null && director.IsPlaying && director.onCutsceneFinished != null)
        {
            UnityEngine.Events.UnityAction handler = null;
            handler = () =>
            {
                director.onCutsceneFinished.RemoveListener(handler);
                SpeakScoreRemarks();
            };
            director.onCutsceneFinished.AddListener(handler);
        }
        else SpeakScoreRemarks();
    }

    /// Jimenez gives the verdict, Pharmee follows up — numbers stay on the grade
    /// card so every spoken line comes from a finite (voice-recordable) pool.
    private void SpeakScoreRemarks()
    {
        if (Model.State != GateState.ScoreReview) return;
        bool passed = _lastResult.HasValue && _lastResult.Value.passed;
        if (examiner != null)
            examiner.SpeakLine(PharmeeLines.Pick(
                passed ? PharmeeLines.JimenezPassRemarks : PharmeeLines.JimenezFailRemarks, _remarkVariant++));
        After(lineSeconds + 0.5f, () =>
        {
            if (Model.State != GateState.ScoreReview) return;
            Say(PharmeeLines.Pick(passed ? PharmeeLines.Celebrate : PharmeeLines.Encourage, _remarkVariant++));
        });
    }

    /// The return sequence: props/bottles re-seated, wearables back on their
    /// pegs, player at the entrance — all inside the Returning fade.
    private void ResetLabForReturn()
    {
        string id = runner != null && runner.Module != null ? runner.Module.moduleId : GameFlow.SelectedModuleId;
        TaskTargetRegistry.Clear();
        LabReset.ResetApparatus();                     // contents, temperature, residue, labels
        launcher?.Launch(id, LaunchMode.StageOnly);
        DropRespawn.ResetAllHome();                    // hand-placed apparatus + reagents back home
        RestorePharmeeHome();                          // off the review spot, back on door duty
        if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
        else if (ppe != null) ppe.RemovePPE();
        TeleportToFrontDoor();
    }

    /// Whether the player's head is already inside the lab proper (past the
    /// front-wall plane at z≈−0.2, i.e. deeper than the entrance strip).
    private bool PlayerInsideLab()
    {
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        Transform head = cam != null ? cam.transform : playerRig;
        return head != null && head.position.z < -0.45f;
    }

    /// Fade wrapper with the edit-mode/test fallback (immediate).
    private void DoFaded(Action mid)
    {
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(mid);
        else
            mid();
    }

    private void SnapshotUnlocks()
    {
        _unlockedAtStart.Clear();
        var svc = new ProgressionService();
        svc.Load();
        foreach (var id in UnlockDiff.UnlockedSet(ProgressionFlow.Create(svc)))
            _unlockedAtStart.Add(id);
    }

    private void TeleportToFrontDoor() => TeleportTo(frontDoorSpawn);

    /// Move the rig so the CAMERA lands on the marker, facing the marker's yaw.
    private void TeleportTo(Transform marker)
    {
        if (marker == null || playerRig == null) return;
        // Being moved across the room with no sound is disorienting in VR — the
        // fade covered it visually but nothing covered it audibly.
        AudioService.TryPlayFirst("teleport-whoosh", "socket-snap");
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        Vector3 camPos = cam != null ? cam.transform.position : playerRig.position;
        float camYaw = cam != null ? cam.transform.eulerAngles.y : playerRig.eulerAngles.y;
        float markerYaw = marker.eulerAngles.y;
        float rigYaw = playerRig.eulerAngles.y;
        float deltaYaw = Mathf.DeltaAngle(camYaw, markerYaw);
        Vector3 newPos = TeleportMath.RigPositionFor(marker.position, deltaYaw, playerRig.position, camPos);
        playerRig.SetPositionAndRotation(newPos,
            Quaternion.Euler(0f, TeleportMath.RigYawFor(markerYaw, rigYaw, camYaw), 0f));
        if (hudRig != null) hudRig.SnapToCamera();
        if (SpawnBurstFX.Instance != null) SpawnBurstFX.Instance.PlayAtPlayer();   // cyan materialize
    }

    private void AnnounceUnlocks()
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = ProgressionFlow.Create(svc);
        // The LAST experiment has no "next challenge", so the whole-campaign finish gets
        // its own celebration: dedicated lines + confetti + the pass sting.
        if (flow.AllComplete())
        {
            Say(PharmeeLines.Pick(PharmeeLines.CampaignComplete, _remarkVariant++));
            AudioService.TryPlay("grade-pass");
            if (Application.isPlaying)
                EffectVfx.Confetti(transform.position + Vector3.up * 1.4f);
            return;
        }
        var newly = UnlockDiff.NewlyUnlocked(_unlockedAtStart, flow);
        Say(UnlockDiff.AnnouncementFor(newly));
    }

    /// All 9 experiments passed (checked fresh from disk).
    private bool AllCampaignComplete()
    {
        var svc = new ProgressionService();
        svc.Load();
        return ProgressionFlow.Create(svc).AllComplete();
    }

    /// Runtime: delayed action; edit mode/tests: immediate (deterministic).
    private void After(float seconds, Action act)
    {
        if (Application.isPlaying && isActiveAndEnabled) StartCoroutine(AfterRoutine(seconds, act));
        else act();
    }

    private System.Collections.IEnumerator AfterRoutine(float seconds, Action act)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, seconds));
        act();
    }

    private void ShowEpisodePicker()
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = ProgressionFlow.Create(svc);
        GatekeeperModel.EpisodeOptions(flow, out var labels, out var selectable);
        Say(lines.episodePrompt);
        panel?.Show("Choose your episode", labels, selectable);
    }

    /// The picker's SECOND level: the chosen period's experiments, so every module in a
    /// period is shown. A trailing "Back" row returns to the period list.
    private void ShowModulePicker()
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = ProgressionFlow.Create(svc);
        GatekeeperModel.ModuleOptions(flow, Model.PickedPeriod, out var labels, out var selectable, out _);
        labels.Add("< Back");
        selectable.Add(true);
        Say(lines.episodePrompt);
        panel?.Show(Model.PickedPeriod + " — choose your experiment", labels, selectable);
    }

    private void LoadSelected()
    {
        string id = Model.SelectedModuleId;
        if (string.IsNullOrEmpty(id)) id = GameFlow.SelectedModuleId;
        GameFlow.Select(id);
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(() => DoLoad(id));
        else
            DoLoad(id);
    }

    private void DoLoad(string id)
    {
        launcher?.Launch(id, LaunchMode.PrepareArmed);   // stage rebuilt + armed, clock held
        if (_returnOnLoad) { _returnOnLoad = false; TeleportToFrontDoor(); }   // retry-from-review
        Model.Fire(GateEvent.Loaded);
    }

    private void ApplyDoor(GateState s)
    {
        bool open = GatekeeperModel.DoorOpen(s);
        // Hard PPE gate: the physical door must NOT open for a
        // campaign entry unless all three PPE pieces are actually worn — even if
        // the FSM somehow advanced. Self-heals: donning the last piece fires
        // OnPPEWorn → ApplyDoor again.
        if (open && GatekeeperModel.RequiresPPEToOpen(s) && ppe != null && !ppe.PPEWorn)
        {
            open = false;
            Say(lines.ppeBlocked);   // static so it can carry a voice clip
        }
        if (doorBlocker != null) doorBlocker.SetActive(!open);
        if (doorOpener != null) doorOpener.SetOpen(open);
    }

    private void Say(string line)
    {
        if (narration != null && !string.IsNullOrEmpty(line)) narration.Say(line, lineSeconds);
    }

    /// Edit-mode/test binding (OnEnable does not fire on AddComponent in edit mode,
    /// so this also performs the event subscription).
    public void Bind(NPCNarrationController n, ChoicePanelController p, PPEController ppeCtrl,
                     ExperimentLauncher l, ExperimentRunner r, GameObject blocker)
    {
        Unsubscribe();
        narration = n; panel = p; ppe = ppeCtrl; launcher = l; runner = r; doorBlocker = blocker;
        Subscribe();
        ApplyDoor(Model.State);
    }

    /// Edit-mode/test binding for the return loop refs.
    public void BindReturn(Transform frontDoor, Transform rig, Camera cam, HudRigController hud)
    {
        frontDoorSpawn = frontDoor; playerRig = rig; cameraOverride = cam; hudRig = hud;
    }
}
