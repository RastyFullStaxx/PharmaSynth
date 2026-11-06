using UnityEngine;

/// Pure litmus colour response (manuscript: pH checks in Exp 3, 4, 8).
public static class LitmusMath
{
    public static readonly Color AcidRed = new Color(0.85f, 0.2f, 0.18f);
    public static readonly Color NeutralViolet = new Color(0.62f, 0.45f, 0.65f);
    public static readonly Color BaseBlue = new Color(0.2f, 0.35f, 0.85f);

    /// Blue litmus turns red at/below this — also the completion gate for
    /// acid litmus-confirmation tasks (Exp 4's "blue litmus turns red").
    public const float AcidPH = 4.5f;

    /// Red litmus turns blue at/above this — the completion gate for BASE
    /// litmus-confirmation tasks (Exp 8's ammonia vapour on alkali hydrolysis).
    public const float BasePH = 8.3f;

    /// Red below ~4.5, blue above ~8.3, graded violet between.
    public static Color ColorForPH(float pH)
    {
        if (pH <= AcidPH) return AcidRed;
        if (pH >= BasePH) return BaseBlue;
        float t = Mathf.InverseLerp(AcidPH, BasePH, pH);
        return t < 0.5f
            ? Color.Lerp(AcidRed, NeutralViolet, t * 2f)
            : Color.Lerp(NeutralViolet, BaseBlue, (t - 0.5f) * 2f);
    }

    /// Mixture pH: the component farther from neutral dominates (an acid stays
    /// acidic under any amount of water). Feeds LiquidPhysics.CurrentPH so the
    /// strip reads the MIXTURE, not whichever chemical happened to land first.
    public static float DominantPH(float a, float b)
        => Mathf.Abs(b - 7f) > Mathf.Abs(a - 7f) ? b : a;
}

/// A grabbable litmus strip: touch it to any liquid (trigger or collision with a
/// vessel holding a chemical) and it tints to the mixture's pH — one-shot, like
/// the real thing. Built by the cabinet builder's consumables box.
public class LitmusStrip : MonoBehaviour
{
    [SerializeField] private Renderer strip;
    private bool _used;
    private MaterialPropertyBlock _mpb;

    public bool Used => _used;

    public void Bind(Renderer r) => strip = r;

    private void OnTriggerEnter(Collider other) => TryRead(other);
    private void OnCollisionEnter(Collision c) => TryRead(c.collider);

    private void TryRead(Collider other)
    {
        if (other == null) return;
        TouchVessel(other.GetComponentInParent<LiquidPhysics>());
    }

    /// The touch itself, physics-free (SimulatedRun's stand-in for physically
    /// dipping the strip — mirrors WaterBathController.HeatVessel). Reads the
    /// MIXTURE pH and tells the vessel's litmus task, if it carries one.
    public void TouchVessel(LiquidPhysics lp)
    {
        if (_used) return;
        if (lp == null || lp.currentChemical == null || lp.currentLiquidVolume <= 0.5f) return;
        Apply(lp.CurrentPH);
        var task = lp.GetComponentInParent<VesselLitmusTask>();
        if (task != null) task.NotifyRead(lp.CurrentPH);
    }

    /// Public + pure-drivable for tests and the methane-style rigs.
    public void Apply(float pH)
    {
        _used = true;
        if (strip == null) strip = GetComponentInChildren<Renderer>();
        if (strip == null) return;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color c = LitmusMath.ColorForPH(pH);
        _mpb.SetColor("_BaseColor", c);
        _mpb.SetColor("_Color", c);
        strip.SetPropertyBlock(_mpb);
    }
}
