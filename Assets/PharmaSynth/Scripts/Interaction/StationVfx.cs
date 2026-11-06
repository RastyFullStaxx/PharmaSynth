using UnityEngine;

/// Per-station particle effects (special effects for boiling,
/// freezing etc.), the visual twin of SimLoopAudio: while the required prop
/// occupies a sim station the effect plays — steam for Heat (boiling), a frosty
/// sparkle for Crystallise (freezing/ice bath), a falling drip for Filter, rising
/// bubbles for Collect. Attached + bound by ExperimentSceneBuilder per station;
/// ZoneSimStation drives SetRunning from occupancy. The ParticleSystem is built
/// procedurally on first use (no asset dependencies — Quest-cheap, ≤60 live).
public class StationVfx : MonoBehaviour
{
    private ParticleSystem _ps;
    private ParticleSystem _flame;      // Heat stations also get a burner flame
    private StationSim _kind = StationSim.None;
    private bool _running;

    private static Material _sharedMat;

    /// Pure mapping for tests: which style a station verb gets.
    public static string StyleFor(StationSim s)
    {
        switch (s)
        {
            case StationSim.Heat: return "steam";
            case StationSim.Crystallise: return "frost";
            case StationSim.Filter: return "drip";
            case StationSim.Collect: return "bubbles";
            default: return "";
        }
    }

    public bool IsPlaying => _running && _ps != null && _ps.isEmitting;

    public void Bind(StationSim kind) => _kind = kind;

    public void SetRunning(bool on)
    {
        if (on == _running) return;
        _running = on;
        if (_kind == StationSim.None) return;
        if (on)
        {
            if (_ps == null) _ps = BuildSystem(_kind, transform);
            if (_ps != null) _ps.Play();
            if (_kind == StationSim.Heat)
            {
                if (_flame == null) _flame = BuildFlame(transform);
                if (_flame != null) _flame.Play();
            }
        }
        else
        {
            if (_ps != null) _ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);   // let live ones fade
            if (_flame != null) _flame.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// A small orange burner flame at the station base — plays under the steam
    /// while a Heat station is occupied (the visible source of the boil).
    private static ParticleSystem BuildFlame(Transform parent)
    {
        var go = new GameObject("StationVfx_flame");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true; main.playOnAwake = false; main.maxParticles = 40;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
        main.startColor = new Color(1f, 0.6f, 0.2f, 0.9f);

        var emission = ps.emission; emission.rateOverTime = 26f;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 8f; shape.radius = 0.02f;
        var col = ps.colorOverLifetime; col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.1f), 0.5f),
                    new GradientColorKey(new Color(0.55f, 0.12f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.6f, 0.5f), new GradientAlphaKey(0f, 1f) });
        col.color = g;
        var siz = ps.sizeOverLifetime; siz.enabled = true;
        siz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.35f));

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedMaterial();
        r.sortingOrder = 9;
        return ps;
    }

    // ---- procedural particle construction ------------------------------------

    private static ParticleSystem BuildSystem(StationSim kind, Transform parent)
    {
        var go = new GameObject("StationVfx_" + StyleFor(kind));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.maxParticles = 60;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        var col = ps.colorOverLifetime;
        col.enabled = true;

        switch (kind)
        {
            case StationSim.Heat:        // boiling steam: soft white plume, rising + expanding
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.0f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
                main.startColor = new Color(1f, 1f, 1f, 0.35f);
                emission.rateOverTime = 14f;
                shape.angle = 12f; shape.radius = 0.05f;
                col.color = FadeGradient(new Color(0.95f, 0.97f, 1f), 0.35f);
                var siz = ps.sizeOverLifetime; siz.enabled = true;
                siz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.8f));
                break;

            case StationSim.Crystallise: // freezing: slow pale-cyan sparkle cloud
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.6f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
                main.startColor = new Color(0.75f, 0.95f, 1f, 0.9f);
                emission.rateOverTime = 18f;
                shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.10f;
                col.color = FadeGradient(new Color(0.75f, 0.95f, 1f), 0.9f);
                break;

            case StationSim.Filter:      // filtrate drip: small drops falling
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.7f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.02f);
                main.startColor = new Color(0.8f, 0.9f, 1f, 0.85f);
                main.gravityModifier = 0.6f;
                emission.rateOverTime = 6f;
                shape.angle = 2f; shape.radius = 0.01f;
                go.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);   // aim down
                col.color = FadeGradient(new Color(0.8f, 0.9f, 1f), 0.85f);
                break;

            case StationSim.Collect:     // gas bubbles: tiny spheres rising
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
                main.startColor = new Color(1f, 1f, 1f, 0.6f);
                emission.rateOverTime = 20f;
                shape.angle = 6f; shape.radius = 0.03f;
                col.color = FadeGradient(Color.white, 0.6f);
                break;
        }

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SharedMaterial();
        r.sortingOrder = 10;
        return ps;
    }

    private static Gradient FadeGradient(Color c, float peakAlpha)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(peakAlpha, 0.2f), new GradientAlphaKey(0f, 1f) });
        return g;
    }

    private static Material SharedMaterial()
    {
        if (_sharedMat != null) return _sharedMat;
        // Routed through EffectVfx so this shares the STRIP-SAFE path: a material built from a
        // bare Shader.Find has its shader stripped out of a device build, so the station
        // steam / frost / drip / bubbles would render wrong or not at all on the Quest while
        // looking perfect in the editor. EffectVfx instantiates the persisted
        // Resources/FxParticleUnlit asset, which keeps the URP particle shader included.
        // It also means one soft-dot texture instead of two identical ones.
        _sharedMat = EffectVfx.MakeParticleMat(EffectVfx.SoftDot());
        _sharedMat.renderQueue = 3000;
        return _sharedMat;
    }

}
