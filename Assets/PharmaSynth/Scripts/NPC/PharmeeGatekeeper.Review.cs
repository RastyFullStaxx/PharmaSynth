using System;
using System.Collections.Generic;
using UnityEngine;

/// The post-experiment review corner: briefing, quiz tablet and grade screen.
public partial class PharmeeGatekeeper
{
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
        var card = UnityEngine.Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
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
}
