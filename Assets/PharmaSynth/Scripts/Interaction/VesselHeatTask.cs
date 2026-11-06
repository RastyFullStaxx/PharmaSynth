using UnityEngine;

/// Completes a heat step ZONE-FREE: the task is done when this vessel has been
/// served all its reagents AND actually reached the required temperature —
/// however and wherever the player heated it. Replaces the old fixed
/// Station_prep-hydrolysis (deleted with its pad/label/teleport anchor).
public class VesselHeatTask : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private float _requiredC;
    private LiquidTaskBinding _binding;
    private LiquidPhysics _lp;
    private bool _subscribed;

    public string TaskId => _taskId;
    public float RequiredC => _requiredC;

    /// The step is the player's CURRENT concern: available and not yet done.
    /// Gates the vessel tag's "warm to N C" line so it shows only while heating is the step.
    public bool Relevant => _runner != null && _runner.Graph != null
        && _runner.Graph.IsAvailable(_taskId) && !_runner.Graph.IsComplete(_taskId);

    /// Pure (suite-pinned): served AND hot — never one without the other.
    public static bool ShouldComplete(bool allReagentsIn, float tempC, float requiredC)
        => allReagentsIn && tempC >= requiredC;

    public void Bind(ExperimentRunner runner, string taskId, float requiredC,
                     LiquidTaskBinding binding, LiquidPhysics lp)
    {
        if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; }
        _runner = runner; _taskId = taskId; _requiredC = requiredC; _binding = binding; _lp = lp;
        if (_runner != null) { _runner.ExperimentStarted += OnStarted; _subscribed = true; }
        Register();
    }

    /// Teardown seam (ClearBenchBindings) — bench items survive rebuilds.
    public void Detach()
    { if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; } }

    private void OnDestroy() => Detach();
    private void OnStarted(ExperimentModuleDefinition _) => Register();

    private void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        // A null binding = the heat step has no reagents of its own (Exp 6's
        // heat-glow: the acetates went in during the WEIGH step) — graph
        // availability alone gates it, and served is vacuously true.
        _runner.Graph.RegisterCondition(_taskId, () =>
            _lp != null
            && ShouldComplete(_binding == null || _binding.ReadyFor(_taskId),
                              _lp.currentTempC, _requiredC));
    }
}
