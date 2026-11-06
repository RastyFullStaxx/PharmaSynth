using UnityEngine;

/// Pure rules for the ZONE-FREE water bath (no fixed
/// zone: the entire lab IS the zone, and tools function when brought
/// together ANYWHERE). The bath is a real tool with real requirements, exactly
/// like the manuscript's: fill it with water, put a lit burner under it, and it
/// warms whatever vessel you bring to it — wherever in the lab it happens to sit.
public static class WaterBathMath
{
    /// Lenient minimum: a splash of water is enough (accuracy is never the test).
    public const float MinWaterMl = 5f;
    /// A water bath can never exceed boiling — that's its whole point (the only
    /// naked-flame heat in any experiment is Exp 6's dry distillation).
    public const float BathMaxC = 100f;
    /// A lit burner within this distance drives the bath.
    public const float BurnerRadius = 0.45f;
    /// Vessels within this distance of the bath take its temperature.
    public const float VesselRadius = 0.32f;

    public static bool HasWater(float waterMl) => waterMl >= MinWaterMl;

    public static bool IsHeating(bool hasWater, bool litBurnerNear) => hasWater && litBurnerNear;

    /// Effect radius from a user-scalable ZONE ANCHOR:
    /// the anchor's wire-sphere
    /// gizmo diameter == its world scale (PlacementAnchor.previewsScale
    /// convention), so radius = half its X scale — what you see in the editor
    /// IS the area of effect. Falls back to the coded constant with no anchor.
    public static float EffectRadius(float anchorScaleX, float fallback)
        => anchorScaleX > 0.0001f ? anchorScaleX * 0.5f : fallback;

    /// The bath's own live label — always tells the player the NEXT thing it needs.
    public static string StatusLine(bool hasWater, bool litBurnerNear, float bathC)
        => !hasWater ? "Water Bath — pour in distilled water"
         : !litBurnerNear ? "Water Bath — needs a lit burner beside it"
         : "Water Bath — " + Mathf.RoundToInt(bathC) + " C";
}

/// The bath itself: LiquidPhysics holds the water the player pours in,
/// TemperatureSim heats while a lit burner sits near, and every vessel brought
/// close takes the bath's temperature — which is what releases temperature-gated
/// reactions (Tollens mirror, ester odours, the hydrolysis boil) and satisfies
/// VesselHeatTask steps. No station, no pad, no fixed position: carry the bath
/// anywhere, it still works.
public class WaterBathController : MonoBehaviour
{
    // SERIALIZED: a domain reload wipes non-serialized privates, and
    // in edit mode Awake never re-binds — the SimulatedRun then sees an unbound
    // bath after any recompile. Serialized, the
    // Apply-W5.8 Bind persists into the saved scene and survives reloads.
    [SerializeField] private LiquidPhysics _lp;
    [SerializeField] private TemperatureSim _temp;
    [SerializeField] private ProximityLabel _label;
    private float _nextScan;
    private Transform _heatZone, _burnerZone;   // user-scalable effect zones (children)

    /// The vessel-warming zone: centre + radius from the hand-scaled "HeatZone"
    /// child when present, else the bath pivot + the coded constant.
    public Vector3 HeatZoneCenter
    { get { FindZones(); return _heatZone != null ? _heatZone.position : transform.position; } }
    public float HeatZoneRadius
    { get { FindZones(); return WaterBathMath.EffectRadius(_heatZone != null ? Mathf.Abs(_heatZone.lossyScale.x) : 0f, WaterBathMath.VesselRadius); } }
    public float BurnerZoneRadius
    { get { FindZones(); return WaterBathMath.EffectRadius(_burnerZone != null ? Mathf.Abs(_burnerZone.lossyScale.x) : 0f, WaterBathMath.BurnerRadius); } }

    private void FindZones()
    {
        if (_heatZone == null) _heatZone = transform.Find("HeatZone");
        if (_burnerZone == null) _burnerZone = transform.Find("BurnerZone");
    }

    public float BathC { get { EnsureRefs(); return _temp != null ? Mathf.Min(_temp.CurrentC, WaterBathMath.BathMaxC) : 25f; } }
    public bool HasWater { get { EnsureRefs(); return _lp != null && WaterBathMath.HasWater(_lp.currentLiquidVolume); } }

    /// Belt-and-suspenders: resolve from own hierarchy if a reference is missing
    /// (a bath that never went through Apply W5.8, or a stale reload).
    private void EnsureRefs()
    {
        if (_lp == null) _lp = GetComponentInChildren<LiquidPhysics>(true);
        if (_temp == null) _temp = GetComponentInChildren<TemperatureSim>(true);
        if (_label == null) _label = GetComponentInChildren<ProximityLabel>(true);
    }

    /// Edit-mode / wiring seam.
    public void Bind(LiquidPhysics lp, TemperatureSim temp, ProximityLabel label)
    { _lp = lp; _temp = temp; _label = label; }

    private void Update()
    {
        if (!Application.isPlaying || _temp == null) return;
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.25f;

        bool lit = AnyLitBurnerNear();
        bool heating = WaterBathMath.IsHeating(HasWater, lit);
        // Source slightly above boiling so the model actually REACHES 100; the
        // push below clamps at BathMaxC — a water bath cannot exceed boiling.
        _temp.SetHeating(heating, 110f);

        if (BathC > 30f) PushHeat();
        if (_label != null)
            _label.SetLabel(WaterBathMath.StatusLine(HasWater, lit, BathC), 1.6f);
    }

    private bool AnyLitBurnerNear()
    {
        var center = _burnerZone != null ? _burnerZone.position : transform.position;
        return BurnerController.AnyLitWithin(center, BurnerZoneRadius);
    }

    private void PushHeat()
    {
        foreach (var col in Physics.OverlapSphere(HeatZoneCenter, HeatZoneRadius,
                                                  ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null && lp != _lp) lp.SetTemperature(BathC);
        }
    }

    /// Explicitly warm one vessel to the bath's temperature (SimulatedRun's
    /// stand-in for physically holding the tube in the bath).
    public void HeatVessel(LiquidPhysics vessel)
    { if (vessel != null && vessel != _lp) vessel.SetTemperature(BathC); }

    /// Deterministic drive for edit-mode simulation: a lit burner is present,
    /// water state is real (the sim pours it in first, like the player).
    public void DriveForTest(float dt)
    {
        if (_temp == null) return;
        _temp.SetHeating(WaterBathMath.IsHeating(HasWater, true), 110f);
        _temp.Tick(dt);
    }
}
