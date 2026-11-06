using UnityEngine;

/// Completes a chill step ZONE-FREE: the task is done when this vessel actually
/// HOLDS something and has been brought down to the required temperature —
/// however and wherever the player cooled it. The cold twin of VesselHeatTask
/// (Exp 4's "cool in an ice bath; crystallise"). Ambient is 25 °C, so a vessel
/// can never satisfy a chill threshold by simply standing on the bench.
public class VesselChillTask : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private float _requiredC;
    private LiquidPhysics _lp;
    private LiquidTaskBinding _binding;   // optional: the chill step's own reagents (Exp 5's ice-cold water)
    private bool _subscribed;

    public string TaskId => _taskId;
    public float RequiredC => _requiredC;

    /// The step is the player's CURRENT concern: available and not yet done.
    /// Gates the vessel tag's "chill to N C" line so it shows only while chilling is the step.
    public bool Relevant => _runner != null && _runner.Graph != null
        && _runner.Graph.IsAvailable(_taskId) && !_runner.Graph.IsComplete(_taskId);

    /// Pure (suite-pinned): reagents served AND holding something AND cold —
    /// never one without the others. (Exp 5's ice-crystallise pours 20 ml of
    /// ice-cold water FIRST, then chills; Exp 4's crystallise has no reagent of
    /// its own, so served=true when no binding step exists.)
    public static bool ShouldComplete(bool allReagentsIn, bool hasContents, float tempC, float requiredC)
        => allReagentsIn && hasContents && tempC <= requiredC;

    public void Bind(ExperimentRunner runner, string taskId, float requiredC, LiquidPhysics lp,
                     LiquidTaskBinding binding = null)
    {
        if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; }
        _runner = runner; _taskId = taskId; _requiredC = requiredC; _lp = lp; _binding = binding;
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
        _runner.Graph.RegisterCondition(_taskId, () =>
            _lp != null
            && ShouldComplete(_binding == null || _binding.ReadyFor(_taskId),
                              _lp.currentLiquidVolume + _lp.currentPptVolume > 0.5f,
                              _lp.currentTempC, _requiredC));
    }
}
