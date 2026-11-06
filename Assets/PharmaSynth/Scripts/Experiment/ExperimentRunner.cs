using System;
using UnityEngine;
using UnityEngine.Events;

/// Outcome of an experiment attempt, consumed by the grade screen and progression gate.
public struct ExperimentResult
{
    public GradeBreakdown grade;      // per-criterion contributions + Total (0..100)
    public float overallMastery;      // BKT mean P(learned) across tracked skills
    public bool gradePassed;          // Total >= threshold%
    public bool masteryPassed;        // mastery >= threshold
    public bool passed;               // gate = gradePassed AND masteryPassed
    public int mistakeCount;
    public float elapsedSeconds;
    public float quizScore01;         // fraction correct on the post-lab quiz (shown on the grade screen)
}

/// Runtime orchestrator for a single experiment attempt. Owns the TaskGraph,
/// BKT MasteryModel, MistakeLog and grader built from an ExperimentModuleDefinition,
/// and raises the events the HUD / tablet / wrist-watch / grade screen subscribe to.
///
/// All scoring/mastery logic lives in the (unit-tested) pure classes; this is thin
/// glue plus per-frame time + auto-check. Drives the v2 TaskGraph data model and is
/// the sole runner.
public class ExperimentRunner : MonoBehaviour
{
    [SerializeField] private ExperimentModuleDefinition module;
    [SerializeField] private GradingConfig gradingConfig = new GradingConfig();
    [SerializeField] private bool startOnAwake = false;

    [Header("Inspector hooks")]
    public UnityEvent onExperimentStarted;
    public UnityEvent onExperimentFinished;

    // Code-facing events (HUD/tablet/watch/NPC controllers subscribe here).
    public event Action<ExperimentModuleDefinition> ExperimentPrepared;   // armed, clock off
    public event Action<ExperimentModuleDefinition> ExperimentStarted;
    public event Action<ExperimentTask> TaskCompleted;
    public event Action<TaskPhase> PhaseCompleted;
    public event Action<float> ProgressChanged;                 // 0..1
    public event Action<LabErrorType, string> MistakeRecorded;
    public event Action<ExperimentResult> ExperimentFinished;
    public event Action Aborted;                                // HUD Restart / abandon — cutscenes must bail out

    private TaskGraph _graph;
    private MasteryModel _mastery;
    private MistakeLog _mistakes;
    private ExperimentGrader _grader;
    private float _elapsed;
    private bool _running;
    private bool _finished;
    private bool _armed;

    public bool IsRunning => _running && !_finished && _graph != null;
    /// Attempt built and waiting for StartRun() — the clock is NOT running and
    /// tasks are locked (used by the door gate: timer starts on walk-in).
    public bool IsArmed => _armed && !_running && !_finished && _graph != null;
    public float ElapsedSeconds => _elapsed;
    public float Progress01 => _graph != null ? _graph.Progress01 : 0f;
    public int MistakeCount => _mistakes != null ? _mistakes.Count : 0;
    public float OverallMastery => _mastery != null ? _mastery.OverallMastery() : 0f;
    public TaskGraph Graph => _graph;
    public MistakeLog Mistakes => _mistakes;
    public ExperimentModuleDefinition Module => module;

    public void SetModule(ExperimentModuleDefinition m) => module = m;

    private void Awake()
    {
        if (startOnAwake) StartExperiment();
    }

    /// Shared attempt construction (graph/mastery/mistakes/grader, clock zeroed).
    private bool BuildAttempt()
    {
        if (module == null)
        {
            Debug.LogError("[ExperimentRunner] No module assigned.");
            return false;
        }

        _graph = module.BuildTaskGraph();
        _mastery = module.BuildMasteryModel();
        _mistakes = new MistakeLog();
        _grader = new ExperimentGrader(module.BuildScoreCalculator(), gradingConfig);
        _elapsed = 0f;
        _clockFrozen = false;
        _clockPaused = false;
        _outOfOrderScolded.Clear();

        _graph.TaskCompleted += OnGraphTaskCompleted;
        _graph.PhaseCompleted += OnGraphPhaseCompleted;
        _mistakes.MistakeRecorded += OnMistakeRecorded;
        return true;
    }

    /// Build a fresh attempt from the module data and begin.
    public void StartExperiment()
    {
        if (!BuildAttempt()) return;
        _armed = false;
        _running = true;
        _finished = false;

        ExperimentStarted?.Invoke(module);
        onExperimentStarted?.Invoke();
        ProgressChanged?.Invoke(0f);
    }

    /// Build the attempt but hold the clock: tasks stay locked (IsRunning false)
    /// until StartRun() — the door-gate seam ("the period starts when you walk in").
    public void PrepareExperiment()
    {
        if (!BuildAttempt()) return;
        _armed = true;
        _running = false;
        _finished = false;

        ExperimentPrepared?.Invoke(module);
        ProgressChanged?.Invoke(0f);
    }

    /// Start the clock on an armed attempt; falls back to a full start when not armed.
    public void StartRun()
    {
        if (!IsArmed) { StartExperiment(); return; }
        _armed = false;
        _running = true;

        ExperimentStarted?.Invoke(module);
        onExperimentStarted?.Invoke();
        ProgressChanged?.Invoke(0f);
    }

    private void OnGraphTaskCompleted(ExperimentTask t)
    {
        if (_mastery.IsTracked(t.skill))
            _mastery.Observe(t.skill, true);      // a correct step is positive evidence
        TaskCompleted?.Invoke(t);
        ProgressChanged?.Invoke(_graph.Progress01);
    }

    private void OnGraphPhaseCompleted(TaskPhase p)
    {
        // Finishing a whole PHASE must sound at least as big as finishing one step inside it.
        AudioService.TryPlay("task-complete");
        LabHaptics.StepComplete();     // the hands confirm progress too, not just the ears
        PhaseCompleted?.Invoke(p);
    }
    private void OnMistakeRecorded(LabErrorType t, string m) => MistakeRecorded?.Invoke(t, m);

    /// Complete a task (from a trigger/UI/auto-check). An out-of-order attempt is
    /// auto-recorded as a WrongStep mistake — the error matrix wiring.
    ///
    /// ONE mistake per task per attempt, however many times it is tried early: a pour
    /// binding calls this on every delivery tick, so a one-second early pour would log a
    /// WrongStep (-4 grade points) per tick and make the run unwinnable at once.
    public TaskCompletionResult CompleteTask(string taskId)
    {
        if (!IsRunning) return TaskCompletionResult.UnknownTask;
        TaskCompletionResult r = _graph.TryComplete(taskId);
        if (r == TaskCompletionResult.BlockedByPrerequisite && _outOfOrderScolded.Add(taskId))
            RecordMistake(LabErrorType.WrongStep, "Attempted '" + taskId + "' out of order");
        return r;
    }

    private readonly System.Collections.Generic.HashSet<string> _outOfOrderScolded = new System.Collections.Generic.HashSet<string>();

    /// Report an error from the safety/interaction systems. Lowers the associated
    /// skill's mastery and feeds the grade penalty.
    public void RecordMistake(LabErrorType type, string message)
    {
        // Armed-but-not-started (and Lab Tour, where nothing is built) never grades.
        if (!IsRunning || _mistakes == null) return;
        // ⛔ PRACTICE IS NOT ASSESSED (scores do not matter in tutorial or
        // practice mode; a pile of errors must never make the score unrecoverable and
        // block the player). Tutorial Mode already refills starved reagents; it kept
        // recording mistakes and pulling mastery down, so a practice run could still be
        // spoiled by the very experimenting it exists for. The player still SEES every scold
        // — those are raised at the call site — but nothing reaches the log or the mastery
        // model. Gate on the session flag alone: a graded run must still count every mistake.
        if (TutorialSession.Active) return;
        _mistakes.Record(type, message);
        LabSkill skill = MistakeLog.SkillFor(type);
        if (_mastery != null && MistakeLog.AffectsMastery(type) && _mastery.IsTracked(skill))
            _mastery.Observe(skill, false);
    }

    /// Bind a world-state predicate so the task auto-completes when satisfied.
    public void RegisterCondition(string taskId, Func<bool> condition) => _graph?.RegisterCondition(taskId, condition);

    /// Practice runs are untimed. A guided learning session that still runs a
    /// stopwatch reads as an exam, which is the opposite of Tutorial Mode's point —
    /// and its Time-Management score is never computed anyway.
    public static bool ClockRuns => !TutorialSession.Active;

    /// Advance the experiment clock (called by Update; exposed for deterministic tests).
    public void AdvanceTime(float dt)
    {
        if (IsRunning && dt > 0f && !_clockFrozen && !_clockPaused && ClockRuns) _elapsed += dt;
    }

    private bool _clockFrozen;

    /// Stop the Time-Management clock without ending the run — called when the
    /// chemical tests complete, so the review-corner teleport, Jimenez's briefing
    /// and the quiz never count against the time score.
    public void FreezeClock() => _clockFrozen = true;

    public bool ClockFrozen => _clockFrozen;

    private bool _clockPaused;

    /// Hold the clock for a mid-run cinematic cutscene, then release it. Separate
    /// from FreezeClock (one-way, end of run) so a cutscene ending never thaws a
    /// clock the review froze.
    public void PauseClock(bool paused) => _clockPaused = paused;
    public bool ClockPaused => _clockPaused;

    private void Update()
    {
        if (!IsRunning) return;
        AdvanceTime(Time.deltaTime);
        _graph.Tick();   // evaluate world-state auto-check conditions
    }

    private ExperimentResult _lastResult;

    /// Compute the result without ending the attempt (for a live grade preview).
    /// Guarded: no attempt built yet → default result (no NRE).
    public ExperimentResult Evaluate(float quizFraction)
        => _graph != null ? BuildResult(quizFraction) : default;

    /// End the attempt and emit the final result to the grade screen + gate.
    /// Guards: calling with no attempt built is ignored instead of
    /// throwing (dev F-key before B); a second call returns the SAME recorded
    /// result instead of recomputing with a different quiz fraction.
    public ExperimentResult Finish(float quizFraction = 0f)
    {
        if (_graph == null)
        {
            Debug.LogWarning("[ExperimentRunner] Finish called with no attempt built — ignored.");
            return default;
        }
        if (_finished) return _lastResult;

        ExperimentResult result = BuildResult(quizFraction);
        _lastResult = result;
        _finished = true;
        _running = false;
        ExperimentFinished?.Invoke(result);
        onExperimentFinished?.Invoke();
        return result;
    }

    /// End the attempt WITHOUT grading, recording or cutscenes: the HUD
    /// Restart / reset-to-entrance path — "your current attempt ends" — must not
    /// leave a zombie run ticking (clock, graph auto-checks, supply monitor,
    /// NPC chatter all key off IsRunning). No ExperimentFinished fires; a later
    /// stray Finish() is a no-op via the _finished latch.
    public void Abort()
    {
        _running = false;
        _armed = false;
        _finished = true;
        _clockFrozen = false;
        _clockPaused = false;
        Aborted?.Invoke();
    }

    /// Retry rebuilds a fresh attempt from the module (deterministic reset).
    public void Retry() => StartExperiment();

    private ExperimentResult BuildResult(float quizFraction)
    {
        float threshold = module != null ? module.masteryThreshold : 0.9f;
        float par = module != null ? module.parTimeSeconds : 600f;

        GradeBreakdown grade = _grader.Grade(_graph, _mistakes, _elapsed, par, quizFraction);
        float mastery = _mastery.OverallMastery();
        bool gradePass = grade.Total >= threshold * 100f;
        bool masteryPass = mastery >= threshold;

        return new ExperimentResult
        {
            grade = grade,
            overallMastery = mastery,
            gradePassed = gradePass,
            masteryPassed = masteryPass,
            passed = gradePass && masteryPass,
            mistakeCount = _mistakes.Count,
            elapsedSeconds = _elapsed,
            quizScore01 = Mathf.Clamp01(quizFraction)
        };
    }
}
