using UnityEngine;

/// A functional balance: whatever rests on the
/// pan drives the live grams display (auto-tared contents, 1 g/ml proxy), and a
/// weigh-* task completes once the CORRECT load sits settled on the pan —
/// either a vessel holding enough of the required chemical (Aspirin: flask with
/// ~50 ml Salicylic Acid) or the required tool (Acetone: the acetate scoopula).
/// TaskGraph condition + ExperimentStarted resubscribe = Retry-safe (the
/// ZoneSimStation pattern). The pan is a trigger volume over the Balance model.
public class WeighStation : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private string _requiredItemId;
    private string _requiredChemical;
    private float _requiredMl;
    private WeighingScaleController _scale;
    private bool _subscribed;
    private bool _doneAnnounced;

    private LabItem _occupantItem;
    private LiquidPhysics _occupantVessel;
    private Rigidbody _occupantBody;
    private int _occupants;
    private float _onPanSince = -1f;
    // Explicit vacancy flag: a "-1 = vacant" timestamp sentinel misreads a
    // legitimately NEGATIVE backdated timestamp (Time.time ≈ 0 in a fresh
    // batchmode editor) as an empty pan (breaks the headless suite).
    private bool _panTimed;

    public LabItem OccupantItem => _occupantItem;
    public LiquidPhysics OccupantVessel => _occupantVessel;
    /// The graph task this scale satisfies. Held privately for the condition
    /// registration; exposed so the tutorial target sweep can locate the balance
    /// for its step.
    public string TaskId => _taskId;
    public float SecondsOnPan => _occupants > 0 && _panTimed ? Time.time - _onPanSince : 0f;

    /// True while THIS vessel rests settled on the pan — the zone-free weigh
    /// condition (VesselWeighTask, Exp 6's bench balance).
    public bool SettledWith(LiquidPhysics lp)
        => lp != null && _occupantVessel == lp && WeighMath.PanSettled(SecondsOnPan);

    /// Builder seam.
    public void Bind(ExperimentRunner runner, string taskId, string requiredItemId,
                     string requiredChemical, float requiredMl, WeighingScaleController scale)
    {
        _runner = runner; _taskId = taskId; _requiredItemId = requiredItemId;
        _requiredChemical = requiredChemical; _requiredMl = requiredMl; _scale = scale;
        Resubscribe();
        Register();
    }

    // Also re-Register on enable (see StirController note).
    private void OnEnable() { Resubscribe(); Register(); }
    private void OnDisable() { if (_runner != null) _runner.ExperimentStarted -= OnStarted; _subscribed = false; }

    private void Resubscribe()
    {
        if (_runner == null || _subscribed) return;
        _runner.ExperimentStarted += OnStarted;
        _subscribed = true;
    }

    private void OnStarted(ExperimentModuleDefinition _) => Register();

    public void Register()
    {
        _doneAnnounced = false;
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () => IsSatisfied);
    }

    /// The weigh condition (public so the suite can pin it).
    public bool IsSatisfied
        => WeighMath.Satisfied(
            WeighMath.PanSettled(SecondsOnPan),
            _requiredChemical,
            _occupantVessel != null && _occupantVessel.currentChemical != null ? _occupantVessel.currentChemical.chemicalName : null,
            _occupantVessel != null ? _occupantVessel.currentLiquidVolume + _occupantVessel.currentPptVolume : 0f,
            _requiredMl,
            _requiredItemId,
            _occupantItem != null ? _occupantItem.itemId : null);

    /// Test hook — simulate a load without physics.
    public void ForceLoad(LabItem item, LiquidPhysics vessel, float secondsAgo)
    {
        _occupantItem = item; _occupantVessel = vessel;
        _occupants = (item != null || vessel != null) ? 1 : 0;
        _panTimed = _occupants > 0;
        _onPanSince = _occupants > 0 ? Time.time - secondsAgo : -1f;
    }

    private void OnTriggerEnter(Collider other)
    {
        var item = LabItem.Resolve(other);
        var lp = other.GetComponentInParent<LiquidPhysics>();
        if (item == null && lp == null) return;
        _occupants++;
        if (item != null) _occupantItem = item;
        if (lp != null) _occupantVessel = lp;
        var rb = other.attachedRigidbody;
        if (rb != null) _occupantBody = rb;
        // A bump that lifts the load off the pan for a moment keeps the settle timer —
        // an imprecise hand nudging the balance would otherwise restart the 0.75 s hold each time.
        if (_occupants == 1 && !(_panTimed && WeighMath.WithinBumpGrace(Time.time - _leftAt)))
        { _onPanSince = Time.time; _panTimed = true; }
    }

    private float _leftAt = float.NegativeInfinity;

    private void OnTriggerExit(Collider other)
    {
        var item = LabItem.Resolve(other);
        var lp = other.GetComponentInParent<LiquidPhysics>();
        if (item == null && lp == null) return;
        _occupants = Mathf.Max(0, _occupants - 1);
        if (item != null && _occupantItem == item) _occupantItem = null;
        if (lp != null && _occupantVessel == lp) _occupantVessel = null;
        if (_occupants == 0) { _leftAt = Time.time; _occupantBody = null; }   // timer kept for the bump grace
    }

    /// The permanent bench balance is wired ONCE at edit time
    /// (`W533Fixes.FixBalanceCore` → `Bind(null,…,scale)`), but `_scale` is a plain
    /// private field — NOT serialized — so it comes back NULL after every domain
    /// reload and the readout silently stops updating for the rest of the
    /// session. Same class as the
    /// `_pestle` / `_runner` bug: re-find at runtime instead of trusting the bind.
    /// The pan is a child of the balance, so the controller is straight up the
    /// parent chain.
    public void AutoFindScale()
    {
        if (_scale == null) _scale = GetComponentInParent<WeighingScaleController>();
    }

    /// Suite visibility: is this pan actually driving a readout?
    public bool HasScale => _scale != null;

    private void Update()
    {
        AutoFindScale();

        // Live display: the GROSS reading — whatever rests on the pan plus its
        // contents. It updates for a bare beaker, an empty tube, the mortar and a
        // watch glass, which auto-taring to the contents alone never did.
        if (_scale != null)
        {
            float contents = _occupantVessel != null
                ? _occupantVessel.currentLiquidVolume + _occupantVessel.currentPptVolume : 0f;
            float grams = _occupants > 0
                ? WeighMath.PanMass(contents, _occupantBody != null ? _occupantBody.mass : 0f)
                : 0f;
            _scale.SetTargetMass(grams);
        }

        if (!_doneAnnounced && IsSatisfied && _runner != null && _runner.IsRunning)
        {
            _doneAnnounced = true;
            FloatingText.Show("Measured!", transform.position + Vector3.up * 0.25f, new Color(0.6f, 1f, 0.7f));
            AudioService.TryPlayAt("mixture-complete", transform.position);
        }
    }
}
