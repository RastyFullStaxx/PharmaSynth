using UnityEngine;

/// Marker: a pour STREAM passes straight through this object (funnels). Otherwise
/// LiquidPourer.ResolveTarget treats the funnel's collider as the landing surface
/// (no LiquidPhysics there), so the hydrolysate the manuscript says to filter is
/// wasted as a puddle on the funnel and the FeCl3 filtrate test below has nothing
/// to react with. The ray continues to the receiving vessel underneath.
///
/// It also OWNS the funnel's visual: the stream disappears into the cone and
/// re-emerges as a thin trickle from the stem's hole. Runtime-only and
/// self-building — no scene authoring beyond the component.
public class LiquidPassthrough : MonoBehaviour
{
    [Tooltip("Optional hand-placed emission point. Defaults to the bottom-centre of the solid mesh — the stem's hole.")]
    public Transform spout;

    private ParticleSystem _trickle;
    private float _flowingUntil;

    /// The stem tip: the hand-placed spout, else the bottom-centre of the funnel's
    /// own solid mesh (recomputed each call — the funnel is grabbable and moves).
    public Vector3 SpoutPoint
    {
        get
        {
            if (spout != null) return spout.position;
            var b = ExperimentSceneBuilder.SolidWorldBounds(gameObject);
            return new Vector3(b.center.x, b.min.y + b.size.y * 0.02f, b.center.z);
        }
    }

    /// True while liquid is still visibly draining out of the stem — the trickle
    /// runs a beat past the pour so it reads as flowing THROUGH, not teleporting.
    public bool IsFlowing => Time.time < _flowingUntil;

    /// Liquid entered the cone this frame. `tint` is the poured chemical's colour.
    public void Flow(Color tint)
    {
        _flowingUntil = Time.time + 0.35f;
        if (!Application.isPlaying) return;
        EnsureTrickle();
        if (_trickle == null) return;
        var main = _trickle.main;
        tint.a = 1f;
        main.startColor = tint;
        _trickle.transform.position = SpoutPoint;
        var em = _trickle.emission;
        em.rateOverTime = 90f;
    }

    private void Update()
    {
        if (_trickle == null || IsFlowing) return;
        var em = _trickle.emission;
        if (em.rateOverTime.constant > 0f) em.rateOverTime = 0f;
    }

    /// A narrow, fast, gravity-fed column — a funnel stem meters the flow down to a
    /// thread, which is what makes it read as passing through rather than around.
    private void EnsureTrickle()
    {
        if (_trickle != null) return;
        var go = new GameObject("FunnelTrickle");
        go.transform.SetParent(transform, true);
        go.transform.position = SpoutPoint;
        go.transform.rotation = Quaternion.LookRotation(Vector3.down);
        _trickle = go.AddComponent<ParticleSystem>();
        _trickle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = _trickle.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.45f;
        main.startSpeed = 0.30f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.005f, 0.010f);
        main.gravityModifier = 2.6f;
        main.maxParticles = 90;
        var em = _trickle.emission; em.rateOverTime = 0f;
        var sh = _trickle.shape;
        sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 2f; sh.radius = 0.0015f;
        var r = _trickle.GetComponent<ParticleSystemRenderer>();
        r.material = EffectVfx.ParticleMaterial();
        r.sortingOrder = 10;
        _trickle.Play();
    }
}
