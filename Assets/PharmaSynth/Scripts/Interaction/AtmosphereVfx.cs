using UnityEngine;

/// Ambient lab atmosphere: gentle white vapour so the room feels
/// alive — a cool stream sinking from the AC unit, and faint slow-drifting haze near
/// the floor/ceiling. Procedural (shares EffectVfx's soft-dot material, no asset
/// deps) and deliberately LOW-density — transparent smoke is the main overdraw cost
/// on Quest, so counts + alpha are kept small. Built + played on Start; placed by
/// AtmosphereBuilder.
public class AtmosphereVfx : MonoBehaviour
{
    public enum Style { AcVapor, Haze }

    [SerializeField] private Style style = Style.AcVapor;
    [SerializeField] private Vector3 hazeVolume = new Vector3(6f, 0.4f, 6f);   // box size for Haze

    private ParticleSystem _ps;

    /// Pure style tag (self-tested).
    public static string StyleName(Style s) => s == Style.AcVapor ? "ac-vapor" : "haze";

    public void Bind(Style s) { style = s; }
    public void Bind(Style s, Vector3 hazeSize) { style = s; hazeVolume = hazeSize; }

    private void Start() => Build();

    public void Build()
    {
        if (_ps != null) return;
        _ps = BuildSystem(style, hazeVolume, transform);
        if (_ps != null && Application.isPlaying) _ps.Play();
    }

    private static ParticleSystem BuildSystem(Style style, Vector3 hazeSize, Transform parent)
    {
        var go = new GameObject("AtmosphereVfx_" + StyleName(style));
        go.transform.SetParent(parent, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = ps.emission;
        var shape = ps.shape;
        var col = ps.colorOverLifetime; col.enabled = true;

        if (style == Style.AcVapor)
        {
            // A cool puff that hangs at the vent and sinks slowly — clusters, so it
            // actually READS against the bright white unit (a thin fast stream vanished).
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.6f, 3.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);      // very slow → clusters
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startColor = new Color(0.68f, 0.79f, 0.93f, 0.68f);            // cool blue-grey → reads on white
            main.gravityModifier = 0.03f;                                        // sinks gently
            emission.rateOverTime = 9f;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.22f;
            col.color = Fade(new Color(0.68f, 0.79f, 0.93f), 0.68f);
            var siz = ps.sizeOverLifetime; siz.enabled = true;
            siz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.6f));
        }
        else // Haze — a faint, low ground mist (kept out of eye-line)
        {
            main.maxParticles = 12;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);    // gentle drift
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.4f);
            main.startColor = new Color(0.75f, 0.83f, 0.95f, 0.18f);           // faint cool ground veil
            main.gravityModifier = -0.004f;                                     // barely rises
            emission.rateOverTime = 1.5f;
            shape.shapeType = ParticleSystemShapeType.Box; shape.scale = hazeSize;
            shape.randomDirectionAmount = 1f;                                   // drift every which way (no velocity module)
            col.color = Fade(new Color(0.95f, 0.97f, 1f), 0.2f);
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = EffectVfx.SmokeMaterial();                                  // AI soft-smoke texture
        r.sortingOrder = 8;                                                      // behind station VFX + labels
        return ps;
    }

    private static Gradient Fade(Color c, float peak)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peak, 0.25f), new GradientAlphaKey(0f, 1f) });
        return g;
    }
}
