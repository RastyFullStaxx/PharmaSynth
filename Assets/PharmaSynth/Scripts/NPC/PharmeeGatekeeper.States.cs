using System;
using System.Collections.Generic;
using UnityEngine;

/// Applying each gate state to the scene (door, dialogue, panel, fades) and the lab tour.
public partial class PharmeeGatekeeper
{
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
}
