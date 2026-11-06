using UnityEngine;

/// Completes a litmus-confirmation step ZONE-FREE (Exp 4's "blue litmus turns
/// red"; Exp 8's "red litmus turns blue"): the task is done when this vessel
/// has been served its reagents AND a litmus strip actually touched it while
/// the mixture read a DEFINITIVE pH — red on acid, blue on base — wherever the
/// player does it. A neutral read (water first, nothing dissolved yet) marks
/// nothing; touch again with a fresh strip once the product is in.
public class VesselLitmusTask : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private LiquidTaskBinding _binding;
    private LiquidPhysics _lp;
    private bool _readDefinitive;
    private bool _subscribed;

    public string TaskId => _taskId;

    /// Pure (suite-pinned): served AND the strip really changed colour.
    public static bool ShouldComplete(bool allReagentsIn, bool stripReadDefinitive)
        => allReagentsIn && stripReadDefinitive;

    /// Called by the strip that touched this vessel.
    public void NotifyRead(float pH)
    {
        if (!_readDefinitive && pH <= LitmusMath.AcidPH)
        {
            _readDefinitive = true;
            // Assurance cue: the colour change is small — say what it means.
            if (Application.isPlaying)
                FloatingText.Show("Blue litmus turns RED — acid confirmed",
                                  transform.position + Vector3.up * 0.15f, LitmusMath.AcidRed, 1f);
        }
        else if (!_readDefinitive && pH >= LitmusMath.BasePH)
        {
            _readDefinitive = true;
            if (Application.isPlaying)
                FloatingText.Show("Red litmus turns BLUE — base confirmed",
                                  transform.position + Vector3.up * 0.15f, LitmusMath.BaseBlue, 1f);
        }
        if (_runner != null && _runner.Graph != null) _runner.Graph.Tick();
    }

    public void Bind(ExperimentRunner runner, string taskId, LiquidTaskBinding binding, LiquidPhysics lp)
    {
        if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; }
        _runner = runner; _taskId = taskId; _binding = binding; _lp = lp;
        _readDefinitive = false;
        if (_runner != null) { _runner.ExperimentStarted += OnStarted; _subscribed = true; }
        Register();
    }

    /// Teardown seam (ClearBenchBindings) — bench items survive rebuilds.
    public void Detach()
    { if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; } }

    private void OnDestroy() => Detach();
    private void OnStarted(ExperimentModuleDefinition _) { _readDefinitive = false; Register(); }

    private void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () =>
            _binding != null && ShouldComplete(_binding.ReadyFor(_taskId), _readDefinitive));
    }
}
