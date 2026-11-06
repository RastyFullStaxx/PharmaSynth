using UnityEngine;

/// Pure rules for NAKED-FLAME heating (Exp 6's dry distillation — the ONE step
/// in any experiment that heats over an open flame; everything else is a
/// ≤100 °C water bath). A vessel held over a LIT burner climbs toward a
/// red-glow temperature the bath can never reach.
public static class FlameMath
{
    /// A vessel this close to the burner's flame takes its heat.
    public const float Reach = 0.18f;
    /// An open flame tops out far beyond the bath's 100 °C cap.
    public const float MaxC = 400f;
    /// Degrees per second while over the flame.
    public const float RatePerSecond = 60f;

    public static bool Heats(float distance) => distance <= Reach;

    public static float NextTemp(float current, float dt)
        => Mathf.Min(MaxC, current + RatePerSecond * Mathf.Max(0f, dt));
}

/// Zone-free open-flame heat on every Bunsen burner (wired by the Apply W5.8 Verb Data menu):
/// while LIT, any vessel within reach of the flame anchor heats toward 400 °C.
/// This is what the hard-glass tubes exist for — the water bath owns every
/// gentler step.
public class NakedFlameHeat : MonoBehaviour
{
    private BurnerController _burner;
    private Transform _flame;
    private float _nextScan;

    public void Bind(BurnerController burner) { _burner = burner; _flame = null; }

    private Vector3 FlamePos()
    {
        if (_flame == null) _flame = transform.Find("FlameAnchor");
        return _flame != null ? _flame.position : transform.position + Vector3.up * 0.12f;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.25f;
        if (_burner == null) _burner = GetComponent<BurnerController>();
        if (_burner == null || !_burner.IsLit) return;
        foreach (var col in Physics.OverlapSphere(FlamePos(), FlameMath.Reach, ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null && !lp.transform.IsChildOf(transform)) HeatVessel(lp, 0.25f);
        }
    }

    /// One heating step for a vessel over the flame (SimulatedRun's stand-in
    /// for physically holding the tube there — mirrors WaterBath.HeatVessel).
    public void HeatVessel(LiquidPhysics vessel, float dt)
    { if (vessel != null) vessel.SetTemperature(FlameMath.NextTemp(vessel.currentTempC, dt)); }
}
