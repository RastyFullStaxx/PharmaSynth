using System;
using System.Collections.Generic;

/// States of the door-gated game loop (the confirmed client workflow,
/// post-experiment review flow included):
/// Pharmee blocks the lab door; the player picks Lab Tour or Campaign → episode →
/// lab-coat → ready → the lab loads/resets → threshold warning → the period starts
/// the moment they walk in → tests complete → Pharmee congratulates and fades the
/// player to Dr. Jimenez's review corner (QuizIntro) → Jimenez briefs → the quiz
/// (QuizTime, never score-gated — manuscript) → grade + outro + spoken remarks
/// (ScoreReview) → Continue teleports home with a full lab/wearables reset
/// (Returning) → Pharmee's quiz-completion debrief AT THE ENTRANCE (Debrief) →
/// unlock announcement → repeat. Retry from the review corner re-arms at the door.
public enum GateState
{
    Blocked, ModeChoice, CampaignExplain, EpisodePick, CoatPrompt,
    ReadyPrompt, Loading, ThresholdWarn, DoorArmed, Running,
    SupplyPrompt, Debrief, Returning, UnlockAnnounce, LabTour,
    QuizIntro, ScoreReview, QuizTime,
    /// Second level of the door picker: the chosen PERIOD's modules, so the player sees
    /// every experiment in a period instead of auto-starting the first playable one.
    /// APPENDED deliberately so the serialized ints of the states above stay valid.
    ModulePick
}

public enum GateEvent
{
    Approach, PickLabTour, PickCampaign, ExplainDone, EpisodeChosen,
    PeriodChosen,   // a period opens its module list — append-only
    Coated, Ready, Loaded, ProceedConfirmed, CrossedThreshold,
    ContinueAfterPass, DebriefDone, TeleportDone, AnnounceDone,
    SupplyExhausted, RestartConfirmed, Dismiss,
    TalkRequested,  // poking Pharmee re-opens the conversation (e.g. during Lab Tour)
    TestsDone,      // last chemical-test phase completed → review flow begins
    QuizBegin,      // Jimenez's briefing done → the tablet quiz opens
    Graded,         // runner.Finish landed (quiz submitted) → score review
    RetryRequested, // failed review → clean re-armed attempt at the door
    AbandonRun      // failed review → back to the entrance to choose ANOTHER experiment
}

/// Pure, table-driven state machine for the Pharmee door gate. No Unity types —
/// fully edit-mode testable; the thin PharmeeGatekeeper MonoBehaviour applies each
/// transition to the scene (door blocker, panels, launcher, runner, fades).
public class GatekeeperModel
{
    public GateState State { get; private set; } = GateState.Blocked;

    /// The campaign module chosen at the door (set by ChooseEpisode).
    public string SelectedModuleId { get; private set; }

    public bool IsLabTour => State == GateState.LabTour;

    /// (from, to) after every successful Fire.
    public event Action<GateState, GateState> Transition;

    /// Attempt a transition; returns false (state unchanged) for illegal events.
    public bool Fire(GateEvent e)
    {
        GateState next = Next(State, e);
        if (next == State) return false;
        GateState from = State;
        State = next;
        Transition?.Invoke(from, next);
        return true;
    }

    /// Force the gate back to the Blocked entrance state, from ANY state (used by
    /// the HUD Reset: the whole lab returns to spawn and the player must re-approach
    /// Pharmee). Clears the chosen episode and fires a Transition so the driver
    /// re-applies the closed door / hidden panels — even when already Blocked.
    public void ResetToBlocked()
    {
        GateState from = State;
        State = GateState.Blocked;
        SelectedModuleId = null;
        Transition?.Invoke(from, GateState.Blocked);
    }

    /// The pure transition table. Illegal (state,event) pairs return the same state.
    public static GateState Next(GateState s, GateEvent e)
    {
        switch (s)
        {
            case GateState.Blocked:
                if (e == GateEvent.Approach || e == GateEvent.TalkRequested) return GateState.ModeChoice;
                break;
            case GateState.ModeChoice:
                if (e == GateEvent.PickLabTour) return GateState.LabTour;
                if (e == GateEvent.PickCampaign) return GateState.CampaignExplain;
                if (e == GateEvent.Dismiss) return GateState.Blocked;
                break;
            case GateState.LabTour:
                // NOTE: Approach must NOT transition here — walking to the open door
                // re-enters the approach trigger and would slam it shut again.
                // Poke Pharmee (TalkRequested) to change plans instead.
                if (e == GateEvent.TalkRequested) return GateState.ModeChoice;
                break;
            case GateState.CampaignExplain:
                if (e == GateEvent.ExplainDone) return GateState.EpisodePick;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.EpisodePick:
                // A period now opens its MODULE list instead of auto-starting one.
                if (e == GateEvent.PeriodChosen) return GateState.ModulePick;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.ModulePick:
                if (e == GateEvent.EpisodeChosen) return GateState.CoatPrompt;
                if (e == GateEvent.Dismiss) return GateState.EpisodePick;   // back to the periods
                break;
            case GateState.CoatPrompt:
                if (e == GateEvent.Coated) return GateState.ReadyPrompt;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.ReadyPrompt:
                if (e == GateEvent.Ready) return GateState.Loading;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.Loading:
                if (e == GateEvent.Loaded) return GateState.ThresholdWarn;
                break;
            case GateState.ThresholdWarn:
                if (e == GateEvent.ProceedConfirmed) return GateState.DoorArmed;
                if (e == GateEvent.Dismiss) return GateState.ModeChoice;
                break;
            case GateState.DoorArmed:
                if (e == GateEvent.CrossedThreshold) return GateState.Running;
                if (e == GateEvent.Dismiss) return GateState.ThresholdWarn;
                break;
            case GateState.Running:
                if (e == GateEvent.SupplyExhausted) return GateState.SupplyPrompt;
                if (e == GateEvent.TestsDone) return GateState.QuizIntro;
                break;
            case GateState.QuizIntro:
                if (e == GateEvent.QuizBegin) return GateState.QuizTime;
                break;
            case GateState.QuizTime:
                if (e == GateEvent.Graded) return GateState.ScoreReview;
                break;
            case GateState.ScoreReview:
                if (e == GateEvent.ContinueAfterPass) return GateState.Returning;   // pass-gated by the grade screen
                if (e == GateEvent.RetryRequested) return GateState.Loading;        // clean re-armed attempt
                if (e == GateEvent.AbandonRun) return GateState.Blocked;            // fail → pick another experiment
                break;
            case GateState.SupplyPrompt:
                if (e == GateEvent.RestartConfirmed) return GateState.Loading;
                if (e == GateEvent.Dismiss) return GateState.Running;       // keep trying
                break;
            case GateState.Returning:
                if (e == GateEvent.TeleportDone) return GateState.Debrief;  // debrief happens AT the entrance
                break;
            case GateState.Debrief:
                if (e == GateEvent.DebriefDone) return GateState.UnlockAnnounce;
                break;
            case GateState.UnlockAnnounce:
                if (e == GateEvent.AnnounceDone) return GateState.Blocked;
                break;
        }
        return s;
    }

    /// Whether the physical door blocker should be OFF in this state. (Debrief now
    /// happens back at the entrance after the Returning teleport → door closed.
    /// CoatPrompt/ReadyPrompt are open: the PPE locker is in
    /// the corner just INSIDE the lab, so gearing up happens through the door.)
    public static bool DoorOpen(GateState s)
        // The door stays SHUT through all prep (mode pick, episode pick, PPE in
        // CoatPrompt, "I'm ready" in ReadyPrompt) and opens only once the experiment
        // is ARMED, so the player is never let in before they are ready. The PPE locker
        // is on the entrance side, so it's reachable with the door closed.
        => s == GateState.LabTour || s == GateState.DoorArmed || s == GateState.Running
        || s == GateState.SupplyPrompt
        || s == GateState.QuizIntro || s == GateState.QuizTime || s == GateState.ScoreReview;

    /// States where the door may only open once the player is FULLY dressed
    /// (all three PPE pieces worn).
    /// The campaign in-lab states require PPE; the Lab Tour and the review-corner
    /// states do NOT (the tour is a walk-through and PPE is stripped for review).
    public static bool RequiresPPEToOpen(GateState s)
        => s == GateState.DoorArmed || s == GateState.Running || s == GateState.SupplyPrompt;

    /// The scripted post-experiment review window: ambient NPC chatter
    /// (Pharmee idle lines, Jimenez exam remarks) and the demo skip buttons are
    /// suppressed here so Jimenez's briefing/verdict and Pharmee's debrief are
    /// never interrupted mid-sentence.
    public static bool IsReviewState(GateState s)
        => s == GateState.QuizIntro || s == GateState.QuizTime || s == GateState.ScoreReview
        || s == GateState.Returning || s == GateState.Debrief || s == GateState.UnlockAnnounce;

    /// The period whose module list is open (set by ChooseEpisode).
    public ExperimentPeriod PickedPeriod { get; private set; }

    /// Pick the PERIOD → open its module list. Starts nothing; the module pick does.
    /// Refuses a period with nothing playable in it.
    public bool ChooseEpisode(ExperimentPeriod period, Func<string, bool> canSelect,
                              Func<ExperimentPeriod, string> firstPlayable)
    {
        if (State != GateState.EpisodePick) return false;
        string anyPlayable = firstPlayable != null ? firstPlayable(period) : null;
        if (string.IsNullOrEmpty(anyPlayable)) return false;        // fully locked period
        if (canSelect != null && !canSelect(anyPlayable)) return false;
        PickedPeriod = period;
        return Fire(GateEvent.PeriodChosen);
    }

    /// Pick the MODULE from the open period's list, then advance to the PPE prompt.
    /// False = locked/unknown, no move.
    public bool ChooseModule(string moduleId, Func<string, bool> canSelect)
    {
        if (State != GateState.ModulePick) return false;
        if (string.IsNullOrEmpty(moduleId)) return false;
        if (canSelect != null && !canSelect(moduleId)) return false;
        SelectedModuleId = moduleId;
        return Fire(GateEvent.EpisodeChosen);
    }

    /// The first unlocked-but-unpassed module in a period; falls back to the first
    /// unlocked (replay); null when the period is fully locked.
    public static string FirstPlayableInPeriod(ProgressionFlow flow, ExperimentPeriod period)
    {
        if (flow == null) return null;
        string firstUnlocked = null;
        foreach (var e in ExperimentCatalog.InPeriod(period))
        {
            if (!flow.IsUnlocked(e.moduleId)) continue;
            if (firstUnlocked == null) firstUnlocked = e.moduleId;
            if (!flow.IsPassed(e.moduleId)) return e.moduleId;
        }
        return firstUnlocked;
    }

    /// Episode-picker rows for the door dialogue: label + selectable per period,
    /// in catalog order (Tutorial, Prelim, Midterm, Final). Pure for tests.
    public static void EpisodeOptions(ProgressionFlow flow,
        out List<string> labels, out List<bool> selectable)
    {
        labels = new List<string>();
        selectable = new List<bool>();
        foreach (ExperimentPeriod p in Enum.GetValues(typeof(ExperimentPeriod)))
        {
            bool periodOpen = flow != null && flow.IsPeriodUnlocked(p);
            string first = periodOpen ? FirstPlayableInPeriod(flow, p) : null;
            bool ok = periodOpen && !string.IsNullOrEmpty(first);
            labels.Add(p + (ok ? "" : "  (locked)"));
            selectable.Add(ok);
        }
    }

    /// MODULE rows for a chosen period — the picker's second level.
    /// Same shape as EpisodeOptions, one level down: catalog order, locked modules
    /// dimmed, PASSED ones marked but still selectable (a passed lab stays replayable
    /// for revision; the rubric/BKT already handle re-attempts and keep the best score).
    /// Pure for tests.
    public static void ModuleOptions(ProgressionFlow flow, ExperimentPeriod period,
        out List<string> labels, out List<bool> selectable, out List<string> moduleIds)
    {
        labels = new List<string>();
        selectable = new List<bool>();
        moduleIds = new List<string>();
        foreach (var e in ExperimentCatalog.InPeriod(period))
        {
            bool unlocked = flow != null && flow.IsUnlocked(e.moduleId);
            bool passed = unlocked && flow.IsPassed(e.moduleId);
            // Strip the redundant "Prelim: " etc. — the period is already the heading.
            string title = e.title;
            int colon = title.IndexOf(':');
            if (colon >= 0 && colon + 2 <= title.Length) title = title.Substring(colon + 1).Trim();

            // Practice mode never marks anything PASSED (nothing is graded), so its
            // tick reads "tried this one", session-only. Campaign is untouched.
            string suffix = !unlocked ? "  (locked)" : passed ? "  ✓ PASSED" : "";
            if (TutorialSession.Active)
                suffix = TutorialSession.HasPractised(e.moduleId) ? "  ✓ practised" : "";
            labels.Add(title + suffix);
            selectable.Add(unlocked);
            moduleIds.Add(e.moduleId);
        }
    }
}
