using UnityEngine;

/// Concrete face for Pharmee: tints the screen-face renderer(s) per expression.
/// Implements IPharmeeFace so PharmeeBrain/PharmeeGatekeeper drive it. Point
/// faceRenderers at the robot's eye/mouth meshes; the color property matches the
/// shader (_EmissionColor for an emissive screen, else _BaseColor). Uses a
/// MaterialPropertyBlock — no material instantiation, edit-mode safe.
/// Default = HAPPY (happy by default, especially while following).
public class PharmeeFace : MonoBehaviour, IPharmeeFace
{
    [SerializeField] private Renderer faceRenderer;      // legacy single (kept wired)
    [SerializeField] private Renderer[] faceRenderers;   // eyes + mouth meshes
    [SerializeField] private string colorProperty = "_EmissionColor";
    [SerializeField] private PharmeeFaceExpression defaultExpression = PharmeeFaceExpression.Happy;
    // ⛔ Kept under the bloom threshold. At exactly 1.0 on EVERY expression the face
    // flashes whenever she speaks, because PharmeeMood swaps expression per LINE.
    // Authored under PharmeeGlowMath.FaceCeiling and enforced by Wire NPC Polish.
    // ⛔ NEUTRAL AND HAPPY ARE THE SAME COLOUR. Every gate line sets Neutral on start and
    // PharmeeMood resets to the default (Happy) on end, so different colours step on EVERY
    // sentence, which reads as flashing. Identical colours make the swap invisible while
    // leaving the expression logic, PharmeeMood and the gate mapping untouched.
    [ColorUsage(true, true)] [SerializeField] private Color neutral = new Color(0.12f, 0.30f, 0.42f);
    [ColorUsage(true, true)] [SerializeField] private Color happy = new Color(0.12f, 0.30f, 0.42f);
    // Warning KEEPS its amber: it fires only when the player actually errs, it is the one
    // face change that carries information, and a rare deliberate change is not a flash.
    [ColorUsage(true, true)] [SerializeField] private Color warning = new Color(0.55f, 0.33f, 0.08f);

    private MaterialPropertyBlock _mpb;

    public PharmeeFaceExpression Current { get; private set; } = PharmeeFaceExpression.Neutral;

    /// Editor-builder seam: point the face at the screen meshes.
    public void BindRenderers(params Renderer[] rs) { faceRenderers = rs; }

    /// Builder seam: cap the palette so no expression clears the bloom threshold.
    public void SetPalette(Color n, Color h, Color w) { neutral = n; happy = h; warning = w; }

    private void Start() => ResetToDefault();

    /// Back to the resting mood (happy) — PharmeeMood calls this when a line ends.
    public void ResetToDefault() => SetExpression(defaultExpression);

    public void SetExpression(PharmeeFaceExpression e)
    {
        Current = e;
        Color c = e == PharmeeFaceExpression.Happy ? happy
                : e == PharmeeFaceExpression.Warning ? warning
                : neutral;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Apply(faceRenderer, c);
        if (faceRenderers != null)
            foreach (var r in faceRenderers) Apply(r, c);
    }

    private void Apply(Renderer r, Color c)
    {
        if (r == null) return;
        r.GetPropertyBlock(_mpb);
        _mpb.SetColor(colorProperty, c);
        if (colorProperty == "_EmissionColor") _mpb.SetColor("_BaseColor", c * 0.4f);
        r.SetPropertyBlock(_mpb);
    }
}
