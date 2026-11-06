using System;
using System.Collections.Generic;
using UnityEngine;

// Data model for the experiment TaskGraph. Kept in the global namespace to match
// the inherited scripts. These are pure serializable types with no scene dependency
// so the TaskGraph/scoring logic can be unit-tested without a running scene.

/// The four graded phases every experiment moves through (plan §3.2).
public enum TaskPhase
{
    ReagentPrep,
    Synthesis,
    ChemicalTests,
    DataSheet
}

/// Reusable lab competencies tracked by the Bayesian mastery model (plan §3.6).
public enum LabSkill
{
    Measuring,
    Heating,
    Filtration,
    Transfer,
    Safety,
    TestInterpretation
}

/// Rubric criteria that the grade is composed from (WCC lab manual rubric, plan §3.6).
public enum RubricCategory
{
    Procedure,
    ChemicalTests,
    MaterialsAndPPE,
    TimeManagement,
    Sanitation,
    Documentation
}

/// Result of attempting to complete a task — drives the error matrix
/// (BlockedByPrerequisite is the canonical "wrong step order" signal).
public enum TaskCompletionResult
{
    Completed,
    AlreadyComplete,
    BlockedByPrerequisite,
    UnknownTask
}

/// One node in a module's TaskGraph. A task becomes available once every
/// prerequisite task is complete; it can then be completed by a world-state
/// condition (auto-check) or an explicit trigger/event.
[Serializable]
public class ExperimentTask
{
    public string taskId = "task-id";
    public string label = "Task";
    public TaskPhase phase = TaskPhase.Synthesis;

    [Tooltip("Task ids that must be complete before this task becomes available.")]
    public List<string> prerequisites = new List<string>();

    [Min(0f), Tooltip("Relative contribution to the module's overall progress %.")]
    public float progressWeight = 1f;

    [Tooltip("Which competency a correct/incorrect observation here updates in the BKT model.")]
    public LabSkill skill = LabSkill.Measuring;

    [Tooltip("Which rubric criterion this task's outcome scores under.")]
    public RubricCategory rubricCategory = RubricCategory.Procedure;

    [Tooltip("Required tasks gate phase/module completion; optional tasks only add score.")]
    public bool required = true;

    [TextArea(1, 3), Tooltip("Shown by Pharmee / tablet when the player is stuck on this step.")]
    public string hint = "";

    [Tooltip("WRAP-UP step: auto-completes (via Graph.Tick) once every other task is done. For closing beats like 'record your observations' that have no physical verb of their own — without this the checklist can never finish (SimulatedRun caught Exp 2 deadlocked here, 2026-07-17).")]
    public bool autoCompleteWhenOthersDone = false;

    [Tooltip("LONG PROCESS (user 2026-07-17): a step whose real duration is impractical in VR (a week's fermentation, an overnight dry). On completion TimeSkipController plays a corner-camera timelapse (black hold without the cinema rig) and returns with a 'time has passed' success message. Set the message; empty falls back to a generic one. Reusable across every experiment with a lengthy wait.")]
    public bool longProcess = false;
    [TextArea(1, 2)]
    public string longProcessMessage = "";
    [Tooltip("How long the process really takes, in hours, for the timelapse counter (168 = 7 days, 0.25 = 15 min). 0 = not stated in the procedure: the timelapse runs with no number rather than an invented one.")]
    [Min(0f)] public float timelapseHours = 0f;
}
