using UnityEngine;

/// Pure rules for the ZONE-FREE ice bath (Exp 4 crystallisation, Exp 8's ice
/// bath) — the cold twin of WaterBathMath. The bucket needs nothing lit or
/// poured: it IS ice. Set any vessel in (or beside) it, anywhere in the lab,
/// and the vessel takes ice-water temperature.
public static class IceBathMath
{
    /// Vessels within this distance of the bucket take its temperature
    /// (same reach as the water bath — one spatial contract for both tools).
    public const float VesselRadius = 0.32f;
    /// Ice-water sits a couple of degrees above zero.
    public const float IceWaterC = 2f;

    /// A vessel "in the ice" is simply near enough. Pure so the suite pins it.
    public static bool Chills(float distance) => distance <= VesselRadius;

    public static string StatusLine() => "Ice Bath — set a vessel in it to chill";
}

/// The bucket itself: every LiquidPhysics vessel brought close is pulled to
/// ice-water temperature. No station, no pad — carry the bucket or carry the
/// flask, either way works (the zone-free tool rule).
public class IceBathController : MonoBehaviour
{
    [SerializeField] private ProximityLabel _label;
    private float _nextScan;
    private Transform _chillZone;   // user-scalable effect zone (child "ChillZone")

    /// Chill zone centre + radius from the hand-scaled "ChillZone" child when
    /// present (wire-sphere diameter == world scale), else pivot + constant.
    public Vector3 ChillZoneCenter
    { get { if (_chillZone == null) _chillZone = transform.Find("ChillZone"); return _chillZone != null ? _chillZone.position : transform.position; } }
    public float ChillZoneRadius
    { get { if (_chillZone == null) _chillZone = transform.Find("ChillZone"); return WaterBathMath.EffectRadius(_chillZone != null ? Mathf.Abs(_chillZone.lossyScale.x) : 0f, IceBathMath.VesselRadius); } }

    /// Edit-mode / wiring seam.
    public void Bind(ProximityLabel label) => _label = label;

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.25f;

        foreach (var col in Physics.OverlapSphere(ChillZoneCenter, ChillZoneRadius,
                                                  ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null && !lp.transform.IsChildOf(transform)) ChillVessel(lp);
        }
        if (_label == null) _label = GetComponentInChildren<ProximityLabel>(true);
        if (_label != null) _label.SetLabel(IceBathMath.StatusLine(), 1.4f);
    }

    /// Explicitly chill one vessel (SimulatedRun's stand-in for physically
    /// setting the flask in the ice — mirrors WaterBathController.HeatVessel).
    public void ChillVessel(LiquidPhysics vessel)
    { if (vessel != null) vessel.SetTemperature(IceBathMath.IceWaterC); }
}
