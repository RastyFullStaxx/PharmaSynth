using System;
using System.Collections.Generic;
using UnityEngine;

/// Thin scene driver for the GatekeeperModel: applies each state to the world —
/// door blocker on/off, Pharmee lines, the door choice panel, stage loading with
/// fades, and the walk-in run start. All heavy logic lives in the pure model.
public class PharmeeGatekeeper : MonoBehaviour
{
    [Serializable]
    public class GateLines
    {
        // "Episode" is the old name: the picker shows PERIODS and the EXPERIMENTS inside
        // them, and every other line in the game says "experiment". A third word for the
        // same thing confuses the player at the moment of choosing.
        [TextArea] public string approach = "Not so fast — the lab's still locked! What would you like to do today?";
        [TextArea] public string labTour = "Lab Tour it is! Roam freely and try the tools and reagents. Come back to me when you want to take on a campaign.";
        [TextArea] public string campaignExplain = "The Campaign takes you through the class periods. Pass every experiment with 90% or better to unlock the next. Ready to pick one?";
        [TextArea] public string episodePrompt = "Which experiment will it be?";
        [TextArea] public string lockedEpisode = "That one's still locked — clear the earlier experiments first!";
        // No ALL-CAPS in spoken copy: TTS reads capitalised words as
        // shouting, so "AND" came out as a jarring bark in the middle of the one
        // line every player hears before every experiment. "all three" carries the
        // same emphasis in the subtitle without the artefact.
        [TextArea] public string coatPrompt = "Safety first! Gear up at the locker — lab coat, goggles and gloves, all three — before we begin.";
        [TextArea] public string readyPrompt = "All geared up! Are you prepared to begin?";
        [TextArea] public string thresholdWarn = "The period will start as soon as you walk in. Step through when you're ready!";
        [TextArea] public string congrats = "Congratulations! You handled that experiment brilliantly. Let's head back outside.";
        [TextArea] public string supplyWarn = "Oh no — there isn't enough reagent left to finish the experiment. We'll have to restart the period.";
        // These two must not interpolate ppe.MissingSummary() into the spoken text: a clip
        // is keyed by the hash of the whole string, so a line assembled at runtime matches
        // nothing and drops to placeholder blips. The locker itself shows which pieces are
        // still missing.
        [TextArea] public string ppePartial = "Almost there — you're still missing a piece of your gear. Check the locker.";
        [TextArea] public string ppeBlocked = "Hold on — I can't let you in without your full protective gear. The locker is right beside you.";
        [TextArea] public string welcome = "Welcome to the lab! I'm Pharmee. Come talk to me at the door whenever you're ready to begin.";
    }

    [Header("Wiring")]
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private ChoicePanelController panel;
    [SerializeField] private PPEController ppe;

    /// Where the player has to GO next while the gate is still running the show: the PPE
    /// locker when Pharmee asks for lab gear, then the door once it is worn. Null once a run
    /// owns the guidance, so the step arrows take over cleanly. The door anchor is the
    /// controller, not the leaf: the leaf swings aside when it opens, and an arrow that
    /// suddenly points at the wall beside the doorway is worse than no arrow.
    public Transform GateGuideTarget
    {
        get
        {
            switch (Model.State)
            {
                case GateState.CoatPrompt:
                    return ppe != null ? ppe.transform : null;
                case GateState.ReadyPrompt:
                case GateState.ThresholdWarn:
                case GateState.DoorArmed:
                    return doorOpener != null ? doorOpener.transform : null;
                default:
                    return null;
            }
        }
    }
    [SerializeField] private ExperimentLauncher launcher;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private GameObject doorBlocker;      // legacy holo barrier (optional)
    [SerializeField] private DoorOpener doorOpener;       // the real hinged lab door
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace (expressions)
    [SerializeField] private LabTourGuide tourGuide;      // location-triggered lab tour (optional)

    [Header("Return loop")]
    [SerializeField] private Transform frontDoorSpawn;    // where the player lands after passing
    [SerializeField] private Transform playerRig;         // XR Origin root
    [SerializeField] private Camera cameraOverride;       // falls back to Camera.main
    [SerializeField] private HudRigController hudRig;     // snapped after teleports
    [SerializeField] private ReagentSupplyMonitor supplyMonitor;   // insufficiency detector

    [Header("Review corner (post-experiment quiz flow, 2026-07-11)")]
    [SerializeField] private PostLabController postLab;   // the quiz tablet (autoOpen off in-scene)
    [SerializeField] private Transform reviewCornerSpawn; // lands the player facing Jimenez + tablet
    [Tooltip("Pharmee's physical body (the robot). Leave empty when this component IS on the robot.")]
    [SerializeField] private Transform pharmeeBody;
    [Tooltip("How far to the player's LEFT of the tablet Pharmee parks during the review, so he never blocks it.")]
    [SerializeField] private float pharmeeReviewOffset = 1.15f;
    [SerializeField] private ExaminerNPC examiner;        // Dr. Jimenez's voice channel

    [Header("Dialogue")]
    [SerializeField] private GateLines lines = new GateLines();

    /// Read seam: the ACTUAL lines this gate speaks (Inspector overrides included),
    /// so tooling auditions exactly what the player will hear.
    public GateLines Lines => lines;
    [SerializeField] private float lineSeconds = 4f;

    public GatekeeperModel Model { get; } = new GatekeeperModel();

    private readonly System.Collections.Generic.HashSet<string> _unlockedAtStart
        = new System.Collections.Generic.HashSet<string>();

    /// Periods in door-picker row order (matches GatekeeperModel.EpisodeOptions).
    public static readonly ExperimentPeriod[] EpisodeRows =
        (ExperimentPeriod[])Enum.GetValues(typeof(ExperimentPeriod));

    private bool _subscribed;
    private float _baseLineSeconds = -1f;
    private int _tourIndex;
    private int _remarkVariant;
    private bool _returnOnLoad;                 // retry-from-review: teleport home inside the load fade
    private ExperimentResult? _lastResult;      // cached at ExperimentFinished for the review remarks

    /// Comfort seam: subtitle pacing multiplier (see PharmeeBrain.SetSubtitlePace).
    public void SetSubtitlePace(float speed)
    {
        if (_baseLineSeconds < 0f) _baseLineSeconds = lineSeconds;
        lineSeconds = ComfortMath.LineSecondsFor(_baseLineSeconds, speed);
    }

    private void OnEnable()
    {
        Subscribe();
        ApplyDoor(Model.State);
        if (panel != null) panel.Hide();
    }

    private void OnDisable() => Unsubscribe();

    /// Greet the player the moment they teleport into the lab (from the cube spawn
    /// room), just after the screen fade-in settles.
    private void Start()
    {
        if (Application.isPlaying)
        {
            // Menu → lab entry does no runtime placement, so the player would sit
            // at the authored rig transform (~0.7 m behind the entrance). Route the
            // initial spawn through the SAME target/code path Restart uses, so
            // "walk in from the cube room" and "press Restart" land identically —
            // but only AFTER the fixed-height calibration has sized the capsule
            // (teleporting into the doorway while it is still un-sized makes physics
            // depenetration shove the rig onto the roof). The player rests at the
            // safe authored spawn during the brief calibration window.
            if (isActiveAndEnabled) StartCoroutine(SpawnAtEntranceRoutine());
            else { TeleportToFrontDoor(); After(0.6f, SpeakWelcome); }
        }
    }

    /// Wait for the rig's height calibration to settle (or a safety timeout), then
    /// land the player at the entrance exactly where Restart does.
    private System.Collections.IEnumerator SpawnAtEntranceRoutine()
    {
        var boost = playerRig != null ? playerRig.GetComponent<SeatedHeightBoost>() : null;
        float t = 0f;
        while (boost != null && !boost.Calibrated && t < 3f)
        {
            t += Time.deltaTime;
            yield return null;
        }
        // If calibration outlasted the start fade-in, the snap would be
        // visible — re-wrap the teleport in its own fade in that case.
        if (ScreenFader.Instance != null && ScreenFader.Instance.State.Alpha < 0.5f)
            DoFaded(TeleportToFrontDoor);
        else
            TeleportToFrontDoor();
        After(0.6f, SpeakWelcome);
    }

    private void Subscribe()
    {
        if (_subscribed) return;
        Model.Transition += OnTransition;
        if (panel != null) panel.OptionChosen += OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged += OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted += OnSupplyExhausted;
        if (runner != null)
        {
            runner.PhaseCompleted += OnRunnerPhase;
            runner.ExperimentFinished += OnRunnerFinished;
            runner.ExperimentStarted += OnRunnerStarted;
        }
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        Model.Transition -= OnTransition;
        if (panel != null) panel.OptionChosen -= OnPanelOption;
        if (ppe != null) ppe.PPEWornChanged -= OnPPEWorn;
        if (supplyMonitor != null) supplyMonitor.SupplyExhausted -= OnSupplyExhausted;
        if (runner != null)
        {
            runner.PhaseCompleted -= OnRunnerPhase;
            runner.ExperimentFinished -= OnRunnerFinished;
            runner.ExperimentStarted -= OnRunnerStarted;
        }
        _subscribed = false;
    }

    // ---- post-experiment review flow ----------------------

    private void OnRunnerStarted(ExperimentModuleDefinition m) => _lastResult = null;

    /// Pure gate: only a graded (campaign) run goes to the review corner. Tutorial
    /// Mode is ungraded practice, so quiz/grade/BKT/unlock/save are skipped whole.
    public static bool ShouldEnterReview() => !TutorialSession.Active;

    /// ChemicalTests complete while Running → begin the review flow. Modules
    /// without a ChemicalTests phase route off their Synthesis completion instead.
    private void OnRunnerPhase(TaskPhase p)
    {
        if (Model.State != GateState.Running) return;
        bool done = p == TaskPhase.ChemicalTests
                    || (p == TaskPhase.Synthesis && runner != null && runner.Graph != null
                        && !runner.Graph.HasPhase(TaskPhase.ChemicalTests));
        if (done) EndOfRun();
    }

    /// The ONE seam where a finished run leaves Running. Both routes in (tests-done
    /// and a dev/legacy Finish) go through here, so Tutorial Mode's skip cannot be
    /// half-applied — guarding only one of them would leave the sibling grading a
    /// practice run.
    private void EndOfRun()
    {
        if (ShouldEnterReview()) { Model.Fire(GateEvent.TestsDone); return; }
        // Tutorial Mode: no quiz, no grade, no unlock. Say what happened first —
        // steps and corrections, never a percentage — then ResetToEntrance, which
        // already IS the ungraded ending: it Aborts the run, clears the grade card
        // and tablet, re-seats every prop and walks the player back to Pharmee. So
        // practice reuses it wholesale rather than growing a parallel teardown.
        TutorialSession.MarkPractised(runner != null && runner.Module != null
            ? runner.Module.moduleId : GameFlow.SelectedModuleId);

        var coach = UnityEngine.Object.FindAnyObjectByType<TutorialCoach>();
        var card = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (card != null && coach != null)
        {
            // Hold the panel until the player dismisses it; announcing and teleporting at once
            // would remove the summary before it is read.
            runner?.FreezeClock();
            card.ShowPractice(coach.StepsDone, coach.Corrections, ResetToEntrance);
            return;
        }
        coach?.ShowSummary();          // no panel wired (bare test scene) — never lose the beat
        ResetToEntrance();
    }

    /// Quiz submitted (or a dev/legacy Finish) → the result is in. From QuizTime
    /// this lands in ScoreReview; a finish that arrives while still Running (the
    /// DevExperimentDriver F key) cascades through the quiz states, whose entries
    /// short-circuit when the result already exists.
    private void OnRunnerFinished(ExperimentResult r)
    {
        _lastResult = r;
        if (Model.State == GateState.QuizTime) Model.Fire(GateEvent.Graded);
        else if (Model.State == GateState.Running) EndOfRun();
    }

    // ---- scene entry points (trigger relays, PPE, panel) -------------------

    /// DoorApproachTrigger → the gate conversation (only from Blocked — walking
    /// through an OPEN door must never slam it shut again).
    public void OnApproachTriggerEntered() => Model.Fire(GateEvent.Approach);

    /// Poking Pharmee re-opens the conversation (e.g. to end a Lab Tour).
    /// Ignored while the wrist-watch gesture is active — the flipping hand can
    /// brush his interactable and must not reopen the gate talk.
    public void OnPharmeeTalk()
    {
        if (WristWatchController.SuppressNpcPokes) return;

        // Tutorial Mode: poking him mid-run means "help me", not "let's renegotiate"
        // He points at the step's target, mimes the motion and says the hint.
        //
        // Deliberately handled HERE and not as an FSM transition: GateState.Running has
        // never accepted TalkRequested, so the poke is currently a no-op and campaign
        // cannot be reached by this branch even by accident. Adding a transition would
        // put a new edge in a graph the whole game's flow is pinned against.
        if (TutorialSession.Active && Model.State == GateState.Running)
        {
            UnityEngine.Object.FindAnyObjectByType<TutorialCoach>()?.HelpNow();
            return;
        }
        Model.Fire(GateEvent.TalkRequested);
    }

    /// LabThresholdTrigger → the period starts the moment the player walks in.
    public void OnThresholdTriggerEntered()
    {
        if (Model.State == GateState.DoorArmed) Model.Fire(GateEvent.CrossedThreshold);
    }

    public void OnPPEWorn()
    {
        if (Model.State == GateState.CoatPrompt)
        {
            if (ppe == null || ppe.PPEWorn) { Model.Fire(GateEvent.Coated); return; }
            // Partially dressed. The SPOKEN line is static (see ppePartial); the
            // specific missing pieces go to the choice panel, which is text.
            Say(lines.ppePartial);
            panel?.Show("You still need your " + ppe.MissingSummary() + ".", new List<string> { "Back" });
            return;
        }
        // PPE changed outside CoatPrompt (e.g. the player finished dressing at the
        // armed door) → re-evaluate the physical door so the hard gate opens it.
        ApplyDoor(Model.State);
    }

    /// Door panel option pressed — meaning depends on the current state.
    public void OnPanelOption(int index)
    {
        switch (Model.State)
        {
            case GateState.ModeChoice:
                Model.Fire(index == 0 ? GateEvent.PickLabTour : GateEvent.PickCampaign);
                break;
            case GateState.CampaignExplain:
                Model.Fire(GateEvent.ExplainDone);
                break;
            case GateState.EpisodePick:
                if (index >= 0 && index < EpisodeRows.Length) ChooseEpisode(EpisodeRows[index]);
                break;
            case GateState.ModulePick:
                ChooseModule(index);
                break;
            case GateState.CoatPrompt:
                Model.Fire(GateEvent.Dismiss);
                break;
            case GateState.ReadyPrompt:
                Model.Fire(index == 0 ? GateEvent.Ready : GateEvent.Dismiss);
                break;
            case GateState.ThresholdWarn:
                Model.Fire(index == 0 ? GateEvent.ProceedConfirmed : GateEvent.Dismiss);
                break;
            case GateState.SupplyPrompt:
                if (index == 0)
                {
                    // Penalty: the starved attempt is recorded as failed (grade screen and failure
                    // OUTRO suppressed so nothing plays over the restart), and the player returns to
                    // the door inside the Loading fade.
                    if (runner != null && runner.IsRunning)
                    {
                        var director = UnityEngine.Object.FindFirstObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
                        if (director != null) director.SkipNextOutro();
                        runner.Finish(0f);
                        var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
                        if (grade != null) grade.Hide();
                    }
                    _returnOnLoad = true;                 // teleport home inside the load fade
                    Model.Fire(GateEvent.RestartConfirmed);
                }
                else
                {
                    // "Keep trying" re-arms the monitor after a grace window —
                    // a genuine shortfall re-prompts instead of dead-ending.
                    supplyMonitor?.Unlatch();
                    Model.Fire(GateEvent.Dismiss);
                }
                break;
        }
    }

    private void ChooseEpisode(ExperimentPeriod period)
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = ProgressionFlow.Create(svc);
        bool ok = Model.ChooseEpisode(period,
            id => HubSelectController.CanSelect(flow, id),
            p => GatekeeperModel.FirstPlayableInPeriod(flow, p));
        if (!ok) Say(lines.lockedEpisode);
    }

    /// Row click on the picker's SECOND level. The last row is always "Back".
    private void ChooseModule(int index)
    {
        var svc = new ProgressionService();
        svc.Load();
        var flow = ProgressionFlow.Create(svc);
        GatekeeperModel.ModuleOptions(flow, Model.PickedPeriod, out _, out _, out var ids);
        if (index < 0) return;
        if (index >= ids.Count) { Model.Fire(GateEvent.Dismiss); return; }   // Back → periods
        bool ok = Model.ChooseModule(ids[index], id => HubSelectController.CanSelect(flow, id));
        if (!ok) Say(lines.lockedEpisode);
    }

    // ---- state application --------------------------------------------------

    /// Guided lab tour (storyboard): Pharmee narrates each area in sequence, one
    /// beat auto-advancing to the next.
    /// Stops the moment the player leaves the tour (poke → ModeChoice).
    private void StartLabTour() { _tourIndex = 0; SpeakTourBeat(); }

    private void SpeakTourBeat()
    {
        if (Model.State != GateState.LabTour) return;             // player ended the tour
        if (_tourIndex >= PharmeeLines.TourBeats.Length) return;  // tour finished; free roam
        Say(PharmeeLines.TourBeats[_tourIndex]);
        _tourIndex++;
        After(lineSeconds + 1.5f, SpeakTourBeat);                 // gentle auto-advance
    }

    /// The scripted review window is live: ambient chatter + demo skip
    /// buttons consult this (static like DemoSession.Active — no wiring needed).
    public static bool ReviewFlowActive { get; private set; }

    private void OnTransition(GateState from, GateState to)
    {
        if (from == GateState.LabTour && to != GateState.LabTour) tourGuide?.End();
        ReviewFlowActive = GatekeeperModel.IsReviewState(to);
        ApplyDoor(to);
        // Dismiss from an open-door state closes the door — but never trap a
        // player already inside the lab behind it.
        if (to == GateState.ModeChoice && PlayerInsideLab())
        {
            if (doorBlocker != null) doorBlocker.SetActive(false);
            if (doorOpener != null) doorOpener.SetOpen(true);
        }
        // Face mood tracks the conversation (PharmeeMood resets to happy after lines).
        (faceBehaviour as IPharmeeFace)?.SetExpression(PharmeeMood.ExpressionForGate(to));
        switch (to)
        {
            case GateState.Blocked:
                panel?.Hide();
                break;

            case GateState.ModeChoice:
                Say(lines.approach);
                panel?.Show("What would you like to do?", new List<string> { "Lab Tour", "Campaign" });
                break;

            case GateState.LabTour:
                panel?.Hide();
                launcher?.Launch(GameFlow.SelectedModuleId, LaunchMode.StageOnly);
                // Location-triggered tour if a guide is wired + its landmarks resolve;
                // otherwise the timed narrated sequence (storyboard). Poke to end either.
                if (tourGuide == null || tourGuide.Begin(s => Say(s)) == 0) StartLabTour();
                break;

            case GateState.CampaignExplain:
                Say(lines.campaignExplain);
                panel?.ShowMessage("Campaign: clear each period's experiments — 90% or better to advance.", "Continue");
                break;

            case GateState.EpisodePick:
                ShowEpisodePicker();
                break;

            case GateState.ModulePick:
                ShowModulePicker();
                break;

            case GateState.CoatPrompt:
                // Put the wearables back on their pegs + strip any stale worn PPE, so the
                // locker is always dressable here (the coat had gone missing after a lab-
                // tour restart). This makes PPEWorn false, so the player re-dons cleanly.
                if (WearableReseat.Instance != null) WearableReseat.Instance.Reseat();
                if (ppe != null && ppe.PPEWorn) { Model.Fire(GateEvent.Coated); return; }
                Say(lines.coatPrompt);
                panel?.Show("Wear the lab coat, goggles and gloves from the locker beside you.", new List<string> { "Back" });
                break;

            case GateState.ReadyPrompt:
                Say(lines.readyPrompt);
                panel?.Show("Are you prepared to begin?", new List<string> { "I'm ready", "Not yet" });
                break;

            case GateState.Loading:
                panel?.Hide();
                // ⭐ Every run path crosses Loading (first run, Retry, Restart), so this is the one
                // place the APPARATUS is restored: empty, cool, unlabelled, unclaimed.
                LabReset.ResetApparatus();
                LoadSelected();
                // Watchdog: Loading's only exit is the fade callback firing
                // Loaded — if a concurrent fade ever swallowed it, force the exit
                // instead of wedging the single-exit state.
                After(5f, () =>
                {
                    if (Model.State != GateState.Loading) return;
                    Debug.LogWarning("[Gatekeeper] Loading watchdog fired — forcing Loaded.");
                    Model.Fire(GateEvent.Loaded);
                });
                break;

            case GateState.ThresholdWarn:
                Say(lines.thresholdWarn);
                panel?.Show("The period will start as soon as you walk in.", new List<string> { "Proceed", "Not yet" });
                // Tutorial Mode: show the whole procedure before the door opens.
                // The stage is already built ARMED by now, so runner.Graph exists and the
                // board renders the real step list rather than a written-out copy of it.
                if (TutorialSession.Active)
                {
                    UnityEngine.Object.FindAnyObjectByType<WristWatchController>()?.ShowProcedurePreview();
                    After(lineSeconds * 0.9f, () => Say(PharmeeLines.TutorialPreview));
                }
                break;

            case GateState.DoorArmed:
                panel?.Hide();
                // The PPE locker sits just INSIDE the lab, so the
                // player may already be past the threshold when the timer arms —
                // the walk-in trigger would never fire. Auto-start in that case.
                After(0.8f, () =>
                {
                    if (Model.State == GateState.DoorArmed && PlayerInsideLab())
                        Model.Fire(GateEvent.CrossedThreshold);
                });
                break;

            case GateState.Running:
                panel?.Hide();
                SnapshotUnlocks();
                runner?.StartRun();          // the walk-in timer start
                break;

            case GateState.QuizIntro:
                panel?.Hide();
                // Dev/legacy finish already graded everything — skip the staging.
                if (_lastResult.HasValue) { Model.Fire(GateEvent.QuizBegin); break; }
                runner?.FreezeClock();       // the review never costs Time-Management score
                Say(PharmeeLines.Pick(PharmeeLines.TestsDoneLines, _remarkVariant++));
                After(lineSeconds * 0.9f, () =>
                {
                    if (Model.State != GateState.QuizIntro) return;
                    // Stage Jimenez INSIDE the same fade as the teleport, so he is
                    // always back at his review spot facing the player when it lifts.
                    DoFaded(() => { TeleportTo(reviewCornerSpawn); StageExaminerForReview(); StagePharmeeForReview(); });
                    After(0.9f, () => SpeakJimenezBrief(0));
                });
                break;

            case GateState.QuizTime:
                panel?.Hide();
                if (_lastResult.HasValue) { Model.Fire(GateEvent.Graded); break; }
                postLab?.Open();             // never score-gated (manuscript)
                break;

            case GateState.ScoreReview:
                panel?.Hide();
                // The grade screen + Success/Failure outro fire off ExperimentFinished
                // on their own; once the outro ends, Jimenez + Pharmee speak the remarks.
                After(0.8f, WaitOutroThenRemarks);
                break;

            case GateState.Returning:
                // One fade: full lab reset (props re-seated, wearables back on their
                // pegs) + teleport home.
                DoFaded(() => { ResetLabForReturn(); Model.Fire(GateEvent.TeleportDone); });
                break;

            case GateState.Debrief:
                // Now AT the entrance, after the return teleport: quiz-completion
                // congrats + a banded performance remark, then the unlock announce.
                // The line starts AFTER the fade-in reveals the scene (it used
                // to begin while the screen was still black), and a final-campaign
                // pass gets the campaign-flavoured remark.
                panel?.Hide();
                After(0.5f, () =>
                {
                    if (Model.State != GateState.Debrief) return;
                    // TWO Say() calls, never a concatenation — a joined string hashes
                    // to nothing and loses its voice clip. The controller queues them.
                    Say(PharmeeLines.Pick(PharmeeLines.DebriefCongrats, _remarkVariant++));
                    Say(PharmeeLines.DebriefRemark(_lastResult.HasValue ? _lastResult.Value.grade.Total : 100f,
                                                   AllCampaignComplete()));
                });
                After(lineSeconds + 1.5f, () => Model.Fire(GateEvent.DebriefDone));
                break;

            case GateState.UnlockAnnounce:
                AnnounceUnlocks();
                After(lineSeconds + 1f, () => Model.Fire(GateEvent.AnnounceDone));
                break;

            case GateState.SupplyPrompt:
                Say(lines.supplyWarn);
                panel?.Show("Not enough reagents left to finish. Restart the period?",
                    new List<string> { "Restart period", "Keep trying" });
                break;
        }
    }

    /// ReagentSupplyMonitor: a required pour-step can no longer be satisfied.
    public void OnSupplyExhausted(List<string> starvedTaskIds) => Model.Fire(GateEvent.SupplyExhausted);

    // ---- return loop ---------------------------------------------------------

    /// GradeScreen Continue (pass-gated) — begin the debrief + return home.
    public void OnContinueAfterPass() => Model.Fire(GateEvent.ContinueAfterPass);

    /// GradeScreen "Choose Another" (fail path): returns a failed player to the entrance
    /// (full lab reset, no congratulatory debrief) so they can pick any unlocked
    /// experiment at the door, not only Retry.
    public void OnAbandonAfterFail()
    {
        if (Model.State != GateState.ScoreReview) return;
        var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
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
            var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
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
        var grade = UnityEngine.Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
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
            roamer = UnityEngine.Object.FindFirstObjectByType<ProctorRoamer>(FindObjectsInactive.Include);
        if (roamer == null) return;
        var cam = cameraOverride != null ? cameraOverride : Camera.main;
        roamer.ReturnHomeAndHold(cam != null ? cam.transform : playerRig);
    }

    /// Park Pharmee off to the PLAYER'S LEFT of the quiz tablet so he never blocks it,
    /// still facing them so he reads as present, and remember where he
    /// was so the lab reset puts him back on door duty.
    private Vector3 _pharmeeHomePos;
    private Quaternion _pharmeeHomeRot;
    private bool _pharmeeParked;

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
        var director = UnityEngine.Object.FindFirstObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
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

    /// Edit-mode/test binding for the review-corner quiz flow refs.
    public void BindQuiz(PostLabController quiz, Transform reviewSpawn, ExaminerNPC ex)
    {
        postLab = quiz; reviewCornerSpawn = reviewSpawn; examiner = ex;
    }
}
