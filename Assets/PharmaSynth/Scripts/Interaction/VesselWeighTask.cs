using UnityEngine;

/// Completes a WEIGH step ZONE-FREE on the bench balance (Exp 6's "weigh 7 g of
/// each acetate"): the task is done when the vessel has been served its solids
/// AND is resting SETTLED on the balance pan — the balance narrates the grams
/// live the whole time.
public class VesselWeighTask : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private LiquidPhysics _lp;
    private LiquidTaskBinding _binding;
    private bool _subscribed;

    public string TaskId => _taskId;

    /// Pure (suite-pinned): served AND settled on the pan.
    public static bool ShouldComplete(bool allReagentsIn, bool settledOnPan)
        => allReagentsIn && settledOnPan;

    public void Bind(ExperimentRunner runner, string taskId, LiquidPhysics lp, LiquidTaskBinding binding)
    {
        if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; }
        _runner = runner; _taskId = taskId; _lp = lp; _binding = binding;
        if (_runner != null) { _runner.ExperimentStarted += OnStarted; _subscribed = true; }
        Register();
    }

    /// Teardown seam (ClearBenchBindings).
    public void Detach()
    { if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; } }

    private void OnDestroy() => Detach();
    private void OnStarted(ExperimentModuleDefinition _) => Register();

    private void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () =>
        {
            var station = FindAnyObjectByType<WeighStation>();
            return _lp != null && station != null
                && ShouldComplete(_binding == null || _binding.ReadyFor(_taskId),
                                  station.SettledWith(_lp));
        });
    }
}
