using System;
using System.Collections.Generic;
using UnityEngine;

/// Thin scene driver for the GatekeeperModel: applies each state to the world —
/// door blocker on/off, Pharmee lines, the door choice panel, stage loading with
/// fades, and the walk-in run start. All heavy logic lives in the pure model.
public partial class PharmeeGatekeeper : MonoBehaviour
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

    [Header("Review corner (post-experiment quiz flow)")]
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

    // Statics survive a scene load. Quitting to the menu mid-review would otherwise
    // leave the review flag up and the target registry pointing at destroyed objects
    // when the lab is entered again.
    private void OnDestroy() => ResetSessionState();

    /// Clears the lab-session statics this gatekeeper owns.
    public static void ResetSessionState()
    {
        ReviewFlowActive = false;
        TaskTargetRegistry.Clear();
    }

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
        // The arrival reel plays first; the player is landed at the door under its black.
        while (ArrivalFlow.Busy)
        {
            if (ShowcaseDirector.Find() == null) { ArrivalFlow.LabPartPending = false; ArrivalFlow.Playing = false; ScreenFader.HoldBlackOnStart = false; if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(); }
            yield return null;
        }
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
                        var director = UnityEngine.Object.FindAnyObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
                        if (director != null) director.SkipNextOutro();
                        runner.Finish(0f);
                        var grade = UnityEngine.Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
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

    /// The scripted review window is live: ambient chatter + demo skip
    /// buttons consult this (static like DemoSession.Active — no wiring needed).
    public static bool ReviewFlowActive { get; private set; }

    /// Park Pharmee off to the PLAYER'S LEFT of the quiz tablet so he never blocks it,
    /// still facing them so he reads as present, and remember where he
    /// was so the lab reset puts him back on door duty.
    private Vector3 _pharmeeHomePos;
    private Quaternion _pharmeeHomeRot;
    private bool _pharmeeParked;
}
