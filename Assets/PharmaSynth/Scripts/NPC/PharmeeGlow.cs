using UnityEngine;

/// Pure rules for how bright Pharmee reads (suite-pinned).
public static class PharmeeGlowMath
{
    /// ⛔ Must stay STRICTLY UNDER the scene's Bloom threshold (1.0). At 1.45 she still fed
    /// bloom and read as a glowing ball; under 1.0 she is lit but contributes no halo at all.
    public const float DefaultIntensity = 0.70f;

    /// Her shell albedo. The pack ships 0.91 (near-white), and with a 1.1 point fill about a
    /// metre away plus the 1.4 directional the LIT surface clears 1.0 and blooms with no
    /// emission whatsoever — which is most of the white ball. 0.72 still reads as a white
    /// robot once lit, without clipping.
    public const float DefaultShellAlbedo = 0.60f;

    /// Ceiling for Pharmee's FACE emissive.
    ///
    /// \u26d4 The face was the flash. `PharmeeFace` writes an HDR `_EmissionColor` to her eyes
    /// and mouth, and the authored colours peaked at EXACTLY 1.0 \u2014 the bloom threshold \u2014
    /// on every expression (happy g=1, warning r=1, neutral b=1). `PharmeeMood` swaps the
    /// expression PER LINE, so the one thing that changed when she spoke was also the one
    /// thing clearing the threshold, so dimming the body and hull alone does not stop the
    /// flash.
    ///
    /// Below the body glow, so the face reads as part of her rather than as a headlight.
    public const float FaceCeiling = 0.55f;

    /// Her light colour: a cyan blue for the body panel, the hover rings and (capped at
    /// FaceCeiling) the face.
    public static readonly Color CyanHue = new Color(0.12f, 0.78f, 1f);

    /// While she speaks, the panel and rings breathe: a slow sine around the base
    /// intensity, eased in and out so neither start nor end of a line steps.
    public const float TalkPulseHz = 1.1f;
    public const float TalkPulseDepth = 0.25f;
    public const float TalkEaseSeconds = 0.35f;

    /// Intensity at time `t` with the talk weight `speak` (0 silent .. 1 speaking).
    public static float TalkIntensity(float baseIntensity, float t, float speak)
    {
        float wave = Mathf.Sin(2f * Mathf.PI * TalkPulseHz * t);
        return baseIntensity * (1f + Mathf.Clamp01(speak) * TalkPulseDepth * wave);
    }

    /// Scale a colour so its brightest channel sits at `ceiling`, keeping the hue.
    /// Colours already below it are left alone \u2014 this is a cap, not a normalisation.
    public static Color CapBrightness(Color c, float ceiling)
    {
        float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
        if (max <= ceiling || max <= 1e-4f) return c;
        float k = ceiling / max;
        return new Color(c.r * k, c.g * k, c.b * k, c.a);
    }

    /// The emission colour to write: the hue scaled to an HDR intensity, alpha pinned.
    public static Color Emission(Color hue, float intensity)
    {
        var c = hue * Mathf.Max(0f, intensity);
        c.a = 1f;
        return c;
    }

    /// Would this emission feed the bloom pass at the given threshold?
    public static bool Blooms(Color emission, float bloomThreshold = 1f)
        => emission.maxColorComponent > bloomThreshold;

    /// The EMISSIVE parts: the body's light panel and the four hover rings. The eyes and
    /// mouth share the same material but belong to PharmeeFace, which colours them per
    /// expression — two writers on one renderer would fight.
    public static bool IsGlowPart(string rendererName)
        => rendererName == "Robot_Blue_Light_0"
           || (rendererName.StartsWith("Wave") && rendererName.EndsWith("_Blue_Light_0"));

    /// The white SHELL parts (body + both hands), which clip on lighting alone.
    public static bool IsShellPart(string rendererName)
        => rendererName != null && rendererName.EndsWith("_White_Glossy_0");
}

/// Keeps Pharmee from blowing out into a white ball while she speaks.
///
/// TWO separate causes, measured rather than guessed:
///   1. The robot FBX ships one emissive material, `Blue_Light`, at HDR (0.46, 2.75, 8.0) —
///      eight times white — on the body panel and all four hover rings. Dimmed here.
///   2. Her `White_Glossy` shell has NO emission at all and still blooms, because albedo
///      0.91 under the nearby fill light pushes the lit result past the 1.0 bloom threshold.
///      Dimming the emissive alone therefore could not have fixed it.
///
/// Everything is written through a MaterialPropertyBlock, the same pattern `PharmeeFace`
/// uses: no material is instanced, the FBX and its importer stay untouched, and both values
/// are tunable live in the inspector. Thin over pure `PharmeeGlowMath`; `Bind*` seams exist
/// because AddComponent fires no Awake in edit mode.
public class PharmeeGlow : MonoBehaviour
{
    [Header("Emissive panels + hover rings")]
    [SerializeField] private Renderer[] glowRenderers = new Renderer[0];
    [Tooltip("The panel/ring hue. Emission = hue x intensity.")]
    [SerializeField] private Color hue = PharmeeGlowMath.CyanHue;
    [Tooltip("HDR intensity. Keep under the scene's Bloom threshold (1.0) or she halos.")]
    [SerializeField] private float intensity = PharmeeGlowMath.DefaultIntensity;

    [Header("White shell (blooms on lighting alone)")]
    [SerializeField] private Renderer[] shellRenderers = new Renderer[0];
    [Tooltip("Base colour for the white shell. The pack's 0.91 clips under the nearby fill light.")]
    [SerializeField] private float shellAlbedo = PharmeeGlowMath.DefaultShellAlbedo;

    private MaterialPropertyBlock _mpb;
    private NPCNarrationController _narration;
    private float _speak;

    public Renderer[] Renderers => glowRenderers;
    public Renderer[] ShellRenderers => shellRenderers;
    public float Intensity => intensity;
    public float ShellAlbedo => shellAlbedo;
    public Color Hue => hue;

    /// Editor-builder seams.
    public void BindRenderers(params Renderer[] rs) { glowRenderers = rs ?? new Renderer[0]; Apply(); }
    public void BindShell(params Renderer[] rs) { shellRenderers = rs ?? new Renderer[0]; Apply(); }

    private void Start() { _narration = GetComponent<NPCNarrationController>(); Apply(); }
    private void OnValidate() => Apply();

    /// The soft talk pulse. Writes only while she is speaking or easing out of it.
    private void Update()
    {
        bool speaking = _narration != null && _narration.IsSpeaking;
        float target = speaking ? 1f : 0f;
        if (_speak == target && !speaking) return;
        _speak = Mathf.MoveTowards(_speak, target, Time.deltaTime / PharmeeGlowMath.TalkEaseSeconds);
        WriteGlow(PharmeeGlowMath.TalkIntensity(intensity, Time.time, _speak));
    }

    private void WriteGlow(float level)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color e = PharmeeGlowMath.Emission(hue, level);
        if (glowRenderers != null)
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", e);
                r.SetPropertyBlock(_mpb);
            }
    }

    public void Apply()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color e = PharmeeGlowMath.Emission(hue, intensity);
        if (glowRenderers != null)
            foreach (var r in glowRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_EmissionColor", e);
                r.SetPropertyBlock(_mpb);
            }

        var shell = new Color(shellAlbedo, shellAlbedo, shellAlbedo, 1f);
        if (shellRenderers != null)
            foreach (var r in shellRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetColor("_BaseColor", shell);
                _mpb.SetColor("_Color", shell);      // whichever name the shader exposes
                r.SetPropertyBlock(_mpb);
            }
    }
}
