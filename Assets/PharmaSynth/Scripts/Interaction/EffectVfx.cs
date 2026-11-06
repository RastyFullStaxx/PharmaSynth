using UnityEngine;

/// One-shot procedural particle bursts,
/// the event twin of StationVfx's looping station effects. All built at runtime
/// from a shared soft-dot material (no asset deps, Quest-cheap, auto-destroy):
///   • Shatter  — glass breaks into a quick outward spray of pale shards (fired
///                from BreakableGlassware.Break, pairs with the glass-break SFX).
///   • Confetti — a colourful upward burst on a passing grade (GradeScreen).
///   • FlamePop — a brief orange flame puff (burner ignite / methane splint test).
/// Each spawner returns immediately; the emitter fades and self-destroys.
public static class EffectVfx
{
    private static Texture2D _dot;
    private static Material _mat;

    public enum Kind { Shatter, Confetti, FlamePop, ColdAir, Smoke, FireBurst, Spatter, ColorFlash, Fizz }

    /// Glass-shatter burst: pale shards spray out and fall.
    public static void Shatter(Vector3 pos, Color tint) => Play(Kind.Shatter, pos, tint);
    public static void Shatter(Vector3 pos) => Play(Kind.Shatter, pos, new Color(0.85f, 0.92f, 0.98f, 0.9f));

    /// Grade-pass confetti: multicoloured upward burst.
    public static void Confetti(Vector3 pos) => Play(Kind.Confetti, pos, Color.white);

    /// Flame puff: burner ignite / splint flame test.
    public static void FlamePop(Vector3 pos) => Play(Kind.FlamePop, pos, new Color(1f, 0.55f, 0.15f, 0.9f));

    /// Cold-air puff: a soft white vapour cloud that spreads out and sinks — fired
    /// when the lab door opens.
    public static void ColdAir(Vector3 pos) => Play(Kind.ColdAir, pos, new Color(0.72f, 0.82f, 0.95f, 0.6f));

    // ---- Error-effect set (wrong-mix consequences) ----------

    /// Rising smoke plume — overheated/ruined batch. Grey default; ToxicGas mixes
    /// pass a chlorine green-yellow tint.
    public static void Smoke(Vector3 pos) => Play(Kind.Smoke, pos, new Color(0.45f, 0.45f, 0.48f, 0.8f));
    public static void Smoke(Vector3 pos, Color tint) => Play(Kind.Smoke, pos, tint);

    /// Fire burst — oxidizer meets a flammable (isolated in-sim; no player harm).
    public static void FireBurst(Vector3 pos) => Play(Kind.FireBurst, pos, new Color(1f, 0.5f, 0.12f, 0.95f));

    /// Spatter — violent droplets, e.g. liquid poured into a concentrated acid.
    public static void Spatter(Vector3 pos, Color tint) => Play(Kind.Spatter, pos, tint);

    /// Bright expanding flash — generic unknown-mix fizz feedback.
    public static void ColorFlash(Vector3 pos, Color tint) => Play(Kind.ColorFlash, pos, tint);

    /// Effervescence: small bright bubbles breaking the surface and popping. The visual
    /// half of every gas-evolving reaction — CO2 off a fermentation, ammonia off the
    /// benzamide boil, nitrogen off the nitrous test.
    public static void Fizz(Vector3 pos) => Play(Kind.Fizz, pos, new Color(0.92f, 0.97f, 1f, 0.85f));

    /// Shared soft-dot particle material (reused by AtmosphereVfx so vapour matches).
    public static Material ParticleMaterial() => SharedMaterial();

    public static void Play(Kind kind, Vector3 pos, Color tint)
    {
        if (!Application.isPlaying) return;               // one-shots are a runtime effect
        var go = new GameObject("EffectVfx_" + kind);
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        var col = ps.colorOverLifetime; col.enabled = true;
        float life;
        int burst;

        switch (kind)
        {
            case Kind.Shatter:
                life = 0.7f; burst = 22;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.03f);
                main.startColor = tint;
                main.gravityModifier = 1.4f;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.04f;
                col.color = Fade(tint, tint.a);
                break;

            case Kind.Confetti:
                life = 2.2f; burst = 60;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 3.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
                main.startColor = ConfettiPalette();
                main.gravityModifier = 0.9f;
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 32f; shape.radius = 0.05f;
                go.transform.localRotation = Quaternion.identity;   // fire upward
                col.color = Fade(Color.white, 1f);
                break;

            case Kind.ColdAir:
                life = 2.2f; burst = 20;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startColor = tint;
                main.gravityModifier = 0.08f;                        // cold air sinks slowly
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 55f; shape.radius = 0.12f;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // spill outward + down
                col.color = Fade(tint, tint.a);
                var csz = ps.sizeOverLifetime; csz.enabled = true;
                csz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.8f));   // expand as it drifts
                break;

            case Kind.Smoke:
                life = 2.4f; burst = 26;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
                main.startColor = tint;
                main.gravityModifier = -0.06f;                       // smoke rises
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 16f; shape.radius = 0.05f;
                col.color = Fade(tint, tint.a);
                var ssz = ps.sizeOverLifetime; ssz.enabled = true;
                ssz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.9f));
                break;

            case Kind.FireBurst:
                life = 1.0f; burst = 34;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                main.startColor = tint;
                main.gravityModifier = -0.15f;                       // flames lick upward
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 30f; shape.radius = 0.05f;
                col.color = FlameGradient();
                var fsz = ps.sizeOverLifetime; fsz.enabled = true;
                fsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));
                break;

            case Kind.Spatter:
                life = 0.8f; burst = 24;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.024f);
                main.startColor = tint;
                main.gravityModifier = 2.2f;                         // droplets arc and fall
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 42f; shape.radius = 0.02f;
                col.color = Fade(tint, tint.a);
                break;

            case Kind.Fizz:
                life = 2.2f; burst = 30;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.32f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.017f);
                main.startColor = tint;
                main.gravityModifier = -0.22f;                       // bubbles rise
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12f; shape.radius = 0.018f;
                col.color = Fade(tint, tint.a);
                var bsz = ps.sizeOverLifetime; bsz.enabled = true;   // swell, then pop
                bsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.55f, 1f, 1.35f));
                break;

            case Kind.ColorFlash:
                life = 0.9f; burst = 16;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.7f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
                main.startColor = tint;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.04f;
                col.color = Fade(tint, tint.a);
                var csz2 = ps.sizeOverLifetime; csz2.enabled = true;
                csz2.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.8f, 1f, 1.6f));
                break;

            default: // FlamePop
                life = 0.6f; burst = 18;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
                main.startColor = tint;
                shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 14f; shape.radius = 0.03f;
                col.color = FlameGradient();
                var siz = ps.sizeOverLifetime; siz.enabled = true;
                siz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                break;
        }

        var em = new ParticleSystem.Burst(0f, (short)burst);
        emission.SetBursts(new[] { em });

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = (kind == Kind.ColdAir || kind == Kind.Smoke) ? SmokeMaterial() : SharedMaterial();
        r.sortingOrder = 12;

        ps.Play();
        Object.Destroy(go, life + 0.5f);
    }

    private static Color ConfettiPalette()
    {
        // Deterministic-ish bright hue set; ParticleSystem randomises between two
        // colours if we set a gradient, but a single bright start is Quest-cheap.
        Color[] c = { new Color(1f, 0.35f, 0.4f), new Color(0.35f, 0.8f, 1f),
                      new Color(1f, 0.85f, 0.3f), new Color(0.5f, 1f, 0.55f),
                      new Color(0.8f, 0.5f, 1f) };
        return c[Mathf.Abs(Time.frameCount) % c.Length];
    }

    private static Gradient Fade(Color c, float peak)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(peak, 0f), new GradientAlphaKey(peak, 0.6f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Gradient FlameGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Material SharedMaterial()
    {
        if (_mat != null) return _mat;
        _mat = MakeParticleMat(SoftDot());
        return _mat;
    }

    private static Material _smokeMat;
    /// Alpha-blended material using the generated soft-smoke texture — for the
    /// realistic cold vapour (AtmosphereVfx + cold-air puff). Falls back to the soft
    /// dot if the texture is missing.
    public static Material SmokeMaterial()
    {
        if (_smokeMat != null) return _smokeMat;
        var tex = Resources.Load<Texture2D>("smoke-soft");
        _smokeMat = MakeParticleMat(tex != null ? (Texture)tex : SoftDot());
        return _smokeMat;
    }

    /// Build an unlit transparent ALPHA-blended particle material. The explicit
    /// blend/ZWrite state is what makes it soft — without it URP renders the quads
    /// opaque (the earlier "blocky white squares" bug). Instantiates from the
    /// persisted Resources/FxParticleUnlit asset when present — a runtime
    /// Shader.Find-only material gets its shader STRIPPED from device builds;
    /// the asset (created by Tools ▸ PharmaSynth ▸ Wire Shelf Pourers) keeps the
    /// URP particle shader included.
    internal static Material MakeParticleMat(Texture tex)
    {
        var src = Resources.Load<Material>("FxParticleUnlit");
        if (src != null)
        {
            var inst = new Material(src);
            inst.SetTexture("_BaseMap", tex);
            return inst;
        }
        var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
        var m = new Material(sh);
        m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);          // transparent
        m.SetFloat("_Blend", 0f);            // alpha
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    internal static Texture2D SoftDot()
    {
        if (_dot != null) return _dot;
        const int n = 64;
        _dot = new Texture2D(n, n, TextureFormat.RGBA32, false);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        _dot.Apply();
        return _dot;
    }
}
