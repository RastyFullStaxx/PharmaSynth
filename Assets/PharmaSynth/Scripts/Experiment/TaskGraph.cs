using System;
using System.Collections.Generic;

/// Runtime engine that drives an experiment's tasks: enforces prerequisites,
/// tracks weighted progress, raises phase/completion events, and auto-completes
/// tasks whose registered world-state condition becomes true.
///
/// Plain C# (not a MonoBehaviour) so it is unit-testable in isolation; a thin
/// scene component owns one of these and forwards trigger/condition events to it.
public class TaskGraph
{
    private readonly List<ExperimentTask> _tasks = new List<ExperimentTask>();
    private readonly Dictionary<string, ExperimentTask> _byId = new Dictionary<string, ExperimentTask>();
    private readonly HashSet<string> _completed = new HashSet<string>();
    private readonly Dictionary<string, Func<bool>> _conditions = new Dictionary<string, Func<bool>>();
    private readonly HashSet<TaskPhase> _completedPhases = new HashSet<TaskPhase>();

    private float _totalWeight;
    private bool _allRequiredFired;

    /// Fired when a task transitions to complete (auto or manual).
    public event Action<ExperimentTask> TaskCompleted;
    /// Fired once, when the last required task of a phase completes.
    public event Action<TaskPhase> PhaseCompleted;
    /// Fired once, when every required task in the module is complete.
    public event Action AllRequiredCompleted;

    public TaskGraph(IEnumerable<ExperimentTask> tasks)
    {
        if (tasks == null) return;
        foreach (var t in tasks)
        {
            if (t == null || string.IsNullOrEmpty(t.taskId) || _byId.ContainsKey(t.taskId))
                continue;
            _tasks.Add(t);
            _byId[t.taskId] = t;
            _totalWeight += Math.Max(0f, t.progressWeight);
        }
    }

    public IReadOnlyList<ExperimentTask> Tasks => _tasks;

    public bool IsComplete(string taskId) => _completed.Contains(taskId);

    /// Whether any task belongs to the given phase (the gate uses this to route
    /// modules that have no ChemicalTests phase into the review flow off Synthesis).
    public bool HasPhase(TaskPhase phase)
    {
        for (int i = 0; i < _tasks.Count; i++)
            if (_tasks[i] != null && _tasks[i].phase == phase) return true;
        return false;
    }

    public bool PrerequisitesMet(string taskId)
    {
        if (!_byId.TryGetValue(taskId, out var task)) return false;
        foreach (var pre in task.prerequisites)
            if (!string.IsNullOrEmpty(pre) && !_completed.Contains(pre))
                return false;
        return true;
    }

    /// A task is available when it exists, is not complete, and all prerequisites are met.
    public bool IsAvailable(string taskId)
    {
        return _byId.ContainsKey(taskId) && !_completed.Contains(taskId) && PrerequisitesMet(taskId);
    }

    public IEnumerable<ExperimentTask> AvailableTasks()
    {
        foreach (var t in _tasks)
            if (IsAvailable(t.taskId))
                yield return t;
    }

    /// The step the player is guided to: the first available task in authored order,
    /// or null when none is open.
    public ExperimentTask FirstAvailable()
    {
        foreach (var t in _tasks)
            if (IsAvailable(t.taskId)) return t;
        return null;
    }

    /// Bind a world-state predicate to a task; Tick() auto-completes it when true.
    public void RegisterCondition(string taskId, Func<bool> condition)
    {
        if (_byId.ContainsKey(taskId) && condition != null)
            _conditions[taskId] = condition;
    }

    /// Is a world-state predicate bound to this task? Diagnostic only: a verb whose
    /// condition never registered looks identical, from the outside, to a verb the
    /// player performed badly — and the simulator's report should tell them apart.
    public bool HasCondition(string taskId) => taskId != null && _conditions.ContainsKey(taskId);

    /// Evaluate registered conditions and auto-complete any available task whose
    /// condition is satisfied. Only available tasks are checked, so this is cheap.
    public void Tick()
    {
        // Snapshot ids because completing a task can make later tasks available.
        for (int i = 0; i < _tasks.Count; i++)
        {
            var id = _tasks[i].taskId;
            if (_conditions.TryGetValue(id, out var cond) && IsAvailable(id) && cond())
                TryComplete(id);
        }
        // WRAP-UP steps ("record your observations"): no physical verb of their
        // own — they complete once every NON-wrap-up task is done. Checked after
        // conditions so a condition completing the last real task cascades in
        // the same tick.
        for (int i = 0; i < _tasks.Count; i++)
        {
            var t = _tasks[i];
            if (!t.autoCompleteWhenOthersDone || IsComplete(t.taskId)) continue;
            bool othersDone = true;
            for (int j = 0; j < _tasks.Count; j++)
                if (!_tasks[j].autoCompleteWhenOthersDone && !IsComplete(_tasks[j].taskId))
                { othersDone = false; break; }
            if (othersDone) TryComplete(t.taskId);
        }
    }

    /// Explicitly complete a task (from a trigger/UI event). Prerequisite-guarded.
    public TaskCompletionResult TryComplete(string taskId)
    {
        if (!_byId.TryGetValue(taskId, out var task))
            return TaskCompletionResult.UnknownTask;
        if (_completed.Contains(taskId))
            return TaskCompletionResult.AlreadyComplete;
        if (!PrerequisitesMet(taskId))
            return TaskCompletionResult.BlockedByPrerequisite;

        _completed.Add(taskId);
        TaskCompleted?.Invoke(task);

        if (!_completedPhases.Contains(task.phase) && IsPhaseComplete(task.phase))
        {
            _completedPhases.Add(task.phase);
            PhaseCompleted?.Invoke(task.phase);
        }

        if (!_allRequiredFired && RequiredRemaining() == 0)
        {
            _allRequiredFired = true;
            AllRequiredCompleted?.Invoke();
        }
        return TaskCompletionResult.Completed;
    }

    /// Completed weight / total weight, clamped to [0,1]. Pure completion ratio;
    /// mistake penalties on the visible progress bar are applied by the HUD layer.
    public float Progress01
    {
        get
        {
            if (_totalWeight <= 0f) return _completed.Count > 0 ? 1f : 0f;
            float done = 0f;
            foreach (var id in _completed)
                if (_byId.TryGetValue(id, out var t)) done += Math.Max(0f, t.progressWeight);
            float p = done / _totalWeight;
            return p < 0f ? 0f : (p > 1f ? 1f : p);
        }
    }

    public bool IsPhaseComplete(TaskPhase phase)
    {
        bool any = false;
        foreach (var t in _tasks)
        {
            if (t.phase != phase || !t.required) continue;
            any = true;
            if (!_completed.Contains(t.taskId)) return false;
        }
        return any; // a phase with no required tasks is not considered "complete"
    }

    public int RequiredRemaining()
    {
        int n = 0;
        foreach (var t in _tasks)
            if (t.required && !_completed.Contains(t.taskId)) n++;
        return n;
    }

    /// Rebuild to initial state (used by experiment Retry).
    public void Reset()
    {
        _completed.Clear();
        _completedPhases.Clear();
        _allRequiredFired = false;
    }
}
