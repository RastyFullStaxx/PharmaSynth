using UnityEngine;

/// Pure rules for VAPOR COLLECTION (Exp 6: distill the acetone off the glowing
/// acetates at 56 °C into a receiver tube).
public static class VaporMath
{
    /// The receiver must sit this close to the hot tube's mouth (the delivery
    /// tube bridges them, like Exp 3's CO₂ run).
    public const float DeliveryRadius = 0.5f;
    /// Millilitres condensed per emission tick.
    public const float MlPerTick = 0.5f;

    /// Vapor comes off only while the source is at temperature AND still has
    /// charge left to decompose.
    public static bool Fires(float sourceTempC, float requiredC, float sourceMl)
        => sourceTempC >= requiredC && sourceMl > 0.5f;

    /// A vessel may receive the stream when its binding ADVERTISES the step, or when it is an
    /// unclaimed pool candidate for the step AND in the player's hand. ⛔ A spare
    /// resting on the bench near the bath must never claim a role by proximity, or Exp 7's crude
    /// distillate runs into a bench beaker before the player has picked anything up, and the
    /// beaker they then hold is scolded on every drop.
    public static bool MayReceive(bool advertises, bool candidate, bool held)
        => advertises || (candidate && held);

    /// Among eligible receivers in range, the one in the HAND beats a nearer one on the bench;
    /// otherwise nearest wins.
    public static bool Prefer(bool held, float d, bool bestHeld, float best)
        => held != bestHeld ? held : d < best;
}

/// The dry-distillation product stream, zone-free (the fermentation pattern):
/// once the collect task is AVAILABLE and the source tube is at temperature,
/// it converts its charge into the module's product, condensing into the
/// nearest nearby vessel whose binding EXPECTS the product — targeted, so the
/// stream can never pollute the water bath or a bystander tube.
public class VaporCollectController : MonoBehaviour
{
    private ExperimentRunner _runner;
    private LiquidPhysics _source;
    private string _taskId;
    private ChemicalData _product;
    private float _requiredC;
    private float _nextTick;

    public string VaporTaskId => _taskId;
    public LiquidPhysics Source => _source;
    /// The source temperature the stream needs, so a driver can hold it there.
    public float RequiredC => _requiredC;

    public void Bind(ExperimentRunner runner, LiquidPhysics source, string taskId,
                     ChemicalData product, float requiredC)
    { _runner = runner; _source = source; _taskId = taskId; _product = product; _requiredC = requiredC; }

    /// Teardown seam (ClearBenchBindings).
    public void Detach() { _runner = null; }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Time.time < _nextTick) return;
        _nextTick = Time.time + 0.4f;
        if (_runner == null || _runner.Graph == null || !_runner.IsRunning) return;
        if (!_runner.Graph.IsAvailable(_taskId)) return;   // not this step yet
        if (_source == null || _product == null) return;
        if (!VaporMath.Fires(_source.currentTempC, _requiredC, _source.currentLiquidVolume)) return;

        LiquidPhysics receiver = null; float best = float.MaxValue; bool bestHeld = false;
        foreach (var b in FindObjectsByType<LiquidTaskBinding>())
        {
            var lp = b.GetComponent<LiquidPhysics>();
            if (lp == null || lp == _source) continue;
            bool held = TutorialHighlighter.IsHeld(b.transform);
            if (!VaporMath.MayReceive(Advertises(b), IsCandidate(b), held)) continue;
            float d = Vector3.Distance(lp.transform.position, _source.transform.position);
            if (d > VaporMath.DeliveryRadius) continue;
            if (receiver == null || VaporMath.Prefer(held, d, bestHeld, best)) { best = d; receiver = lp; bestHeld = held; }
        }
        if (receiver != null) EmitTick(receiver);
    }

    /// ⛔ The stream may only fill the glass THIS step names. `IsExpectedNow` asks merely
    /// "does some step of this binding want the product", which is true for every later
    /// test that draws on it — so in Exp 7 the redistillate would run into a test tube belonging
    /// to a downstream step, empty the flask, and `dry-redistil` could never finish.
    /// This makes the stream "targeted, so it can never pollute a bystander tube"
    /// across TASKS, not just within one.
    public bool Advertises(LiquidTaskBinding b)
    {
        if (b == null) return false;
        foreach (var s in b.ExpectedSteps)
            if (s != null && s.taskId == _taskId && s.reagent == _product) return true;
        return false;
    }

    /// A free POOL member that COULD still become this step's receiver. An unclaimed
    /// extra advertises nothing, and nothing is ever poured into a receiver first, so without
    /// this "hold a clean test tube at its mouth" only works with the authored tube.
    /// The claim itself happens on
    /// delivery (EmitTick → ClaimForTask): condensing into a HELD tube is the disambiguating
    /// act — held, never merely nearby (VaporMath.MayReceive).
    public bool IsCandidate(LiquidTaskBinding b)
        => b != null && b.IsPoolMember && b.RoleAmbiguous && b.CandidateTasks().Contains(_taskId);

    /// One condensation tick into a receiver (public: the sim's stand-in for
    /// the delivery-tube run). Consumes the source charge, delivers product.
    public bool EmitTick(LiquidPhysics receiver)
    {
        if (receiver == null || _source == null || _product == null) return false;
        if (!VaporMath.Fires(_source.currentTempC, _requiredC, _source.currentLiquidVolume)) return false;
        // The receiver in the player's hand becomes THE receiver: a pooled tube claims
        // the collect role on the first condensed drop, so its binding advertises the step and
        // the accumulated distillate can complete it.
        var rb = receiver.GetComponent<LiquidTaskBinding>();
        if (rb != null && rb.IsPoolMember && rb.RoleAmbiguous) rb.ClaimForTask(_taskId);
        if (_source.PourOut(VaporMath.MlPerTick) == null) return false;
        receiver.AddLiquid(_product, VaporMath.MlPerTick);
        return true;
    }
}
