using UnityEngine;

/// Turns a layout zone station into a REAL sustained verb (generalising
/// MethaneApparatusRig to every experiment): while the required prop occupies the
/// zone the bound chemistry sim advances, and the station's task auto-completes via
/// a TaskGraph condition once the sim reaches its target — the player must actually
/// perform and hold the action, not just drop the prop in.
///
/// Attached + configured by ExperimentSceneBuilder for stations whose layout sets a
/// StationSim other than None. Deterministically testable (ForceOccupied + Tick).
public class ZoneSimStation : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private StationSim _kind;
    private ZoneItemSensor _sensor;

    private TemperatureSim _temp;
    private CrystallizationController _cryst;
    private FiltrationController _filt;
    private GasCollection _gas;

    [Header("Tuning")]
    public float heatSourceC = 240f;
    public float heatTargetC = 80f;
    public float filtrateMlPerSec = 30f;
    public float gasMlPerSec = 25f;
    [Range(0.1f, 1f)] public float doneFraction = 0.95f;
    [Tooltip("A Heat station warms any vessel within this radius to its sim temperature — the water-bath zone that fires temperature-gated reactions (2026-07-17).")]
    public float heatRadius = 0.5f;
    private float _nextHeatPush;

    private bool _subscribed;
    private SimLoopAudio _loop;
    private StationVfx _vfx;

    /// Which task this station's verb completes (read by the simulated-run harness).
    public string TaskId => _taskId;
    public StationSim Kind => _kind;

    /// Optional looping apparatus audio, driven by occupancy (set by the builder).
    public void SetLoopAudio(SimLoopAudio loop) => _loop = loop;

    /// Optional particle effect (steam/frost/drip/bubbles), driven like the audio loop.
    public void SetVfx(StationVfx vfx) => _vfx = vfx;

    /// Optional ignition gate: a Heat station whose required prop is a
    /// burner only heats while the burner is LIT (light it with a match). Null
    /// gate = occupancy-only behaviour.
    private System.Func<bool> _ignitionGate;
    public void SetIgnitionGate(System.Func<bool> gate) => _ignitionGate = gate;

    /// Configure at build time. Registers the auto-check condition immediately (if a
    /// graph exists) and re-registers on every ExperimentStarted (Retry-safe).
    public void Bind(ExperimentRunner runner, string taskId, StationSim kind, ZoneItemSensor sensor,
                     TemperatureSim temp, CrystallizationController cryst, FiltrationController filt, GasCollection gas,
                     float heatTarget)
    {
        _runner = runner; _taskId = taskId; _kind = kind; _sensor = sensor;
        _temp = temp; _cryst = cryst; _filt = filt; _gas = gas;
        heatTargetC = heatTarget;
        Resubscribe();
        Register();
    }

    private void OnEnable() => Resubscribe();
    private void OnDisable() { if (_runner != null) _runner.ExperimentStarted -= OnStarted; _subscribed = false; }

    private void Resubscribe()
    {
        if (_runner == null || _subscribed) return;
        _runner.ExperimentStarted += OnStarted;
        _subscribed = true;
    }

    private void OnStarted(ExperimentModuleDefinition _) => Register();

    /// Register the TaskGraph auto-check predicate for this station's verb.
    public void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        switch (_kind)
        {
            case StationSim.Heat:
                if (_temp != null) { _temp.ResetSim(); _runner.Graph.RegisterCondition(_taskId, () => _temp != null && _temp.AtLeast(heatTargetC)); }
                break;
            case StationSim.Crystallise:
                if (_cryst != null) { _cryst.ResetProcess(); _runner.Graph.RegisterCondition(_taskId, () => _cryst != null && _cryst.IsDone); }
                break;
            case StationSim.Filter:
                if (_filt != null) { _filt.ResetProcess(); _runner.Graph.RegisterCondition(_taskId, () => _filt != null && _filt.Filtered01(doneFraction)); }
                break;
            case StationSim.Collect:
                if (_gas != null) { _gas.ResetCollection(); _runner.Graph.RegisterCondition(_taskId, () => _gas != null && _gas.Collected(doneFraction)); }
                break;
        }
    }

    private void Update()
    {
        if (_runner == null || !_runner.IsRunning)
        {
            if (_loop != null) _loop.SetRunning(false);
            if (_vfx != null) _vfx.SetRunning(false);
            return;
        }
        Drive(Time.deltaTime, _sensor != null && _sensor.IsOccupied);
    }

    /// Advance the bound sim for one step given zone occupancy (public for tests).
    public void Drive(float dt, bool occupied)
    {
        if (_loop != null) _loop.SetRunning(occupied);
        if (_vfx != null) _vfx.SetRunning(occupied);
        switch (_kind)
        {
            case StationSim.Heat:
                if (_temp != null)
                {
                    _temp.SetHeating(occupied && (_ignitionGate == null || _ignitionGate()), heatSourceC);
                    PushHeatToVessels();
                }
                break;
            case StationSim.Crystallise:
                if (occupied && _cryst != null) _cryst.BeginCrystallization();  // ice bath: place & let it set
                break;
            case StationSim.Filter:
                if (occupied && _filt != null) _filt.AddFiltrate(filtrateMlPerSec * Mathf.Max(0f, dt));
                break;
            case StationSim.Collect:
                if (occupied && _gas != null) _gas.AddGas(gasMlPerSec * Mathf.Max(0f, dt));
                break;
        }
    }

    /// The bath IS the heat source: any vessel held/placed within heatRadius
    /// takes the sim temperature, which is what fires temperature-gated
    /// reactions (Tollens mirror, ester odours, the hydrolysis boil) at the
    /// moment the procedure actually asks for — never before. Throttled; only
    /// pushes while the bath is meaningfully above ambient.
    private void PushHeatToVessels()
    {
        if (_temp == null || _temp.CurrentC < 30f) return;
        if (Application.isPlaying && Time.time < _nextHeatPush) return;
        _nextHeatPush = Time.time + 0.25f;
        foreach (var col in Physics.OverlapSphere(transform.position, heatRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null) lp.SetTemperature(_temp.CurrentC);
        }
    }
}
