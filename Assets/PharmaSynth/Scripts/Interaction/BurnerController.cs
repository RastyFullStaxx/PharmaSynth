using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// An ignitable Bunsen/alcohol burner.
/// Bring a lit matchstick to the burner and it lights — a looping flame +
/// point light; Heat stations whose required prop is a burner only advance
/// their sim while it is LIT (ZoneSimStation ignition gate).
public class BurnerController : MonoBehaviour
{
    public const float MatchIgniteDistance = 0.18f;

    private bool _lit;
    private float _nextScan;
    private GameObject _flame;
    private XRGrab _grab;

    public bool IsLit => _lit;

    private static readonly System.Collections.Generic.List<BurnerController> s_live = new System.Collections.Generic.List<BurnerController>();
    /// Every enabled burner. Play mode reads a list kept by OnEnable/OnDisable; edit mode
    /// (simulators, tests) searches the scene, because edit-mode AddComponent never runs OnEnable.
    public static System.Collections.Generic.IReadOnlyList<BurnerController> Active
        => Application.isPlaying ? (System.Collections.Generic.IReadOnlyList<BurnerController>)s_live
                                 : FindObjectsByType<BurnerController>();
    private void OnEnable() { if (!s_live.Contains(this)) s_live.Add(this); }
    private void OnDisable() => s_live.Remove(this);

    /// Pure ignition predicate (suite-pinned).
    public static bool ShouldIgnite(bool burnerLit, bool matchLit, float distance)
        => !burnerLit && matchLit && distance <= MatchIgniteDistance;

    /// Pure: a lit burner that gets picked up blows out; re-light it
    /// once it is back down.
    public static bool ShouldBlowOut(bool lit, bool held) => lit && held;

    private void Update()
    {
        if (_grab == null) _grab = GetComponent<XRGrab>();
        // Lit + lifted → snuff it; the player must re-strike a match once it rests.
        if (ShouldBlowOut(_lit, _grab != null && _grab.isSelected)) { Extinguish(); return; }

        if (_lit || !Application.isPlaying) return;
        if (_grab != null && _grab.isSelected) return;   // don't light while being carried
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.25f;
        foreach (var match in FindObjectsByType<Matchstick>(FindObjectsSortMode.None))
        {
            // Measure flame-to-flame: the burner's MOUTH to the match HEAD. Pivot to match
            // origin put the target at the burner's foot, so a match held where a person
            // lights a burner (its mouth) often sat out of range.
            float d = Mathf.Min(Vector3.Distance(FlamePos(), match.HeadPosition),
                                Vector3.Distance(transform.position, match.transform.position));
            if (ShouldIgnite(_lit, match.IsLit, d))
            {
                Ignite();
                break;
            }
        }
    }

    public void Ignite()
    {
        if (_lit) return;
        _lit = true;
        EffectVfx.FlamePop(FlamePos());
        AudioService.TryPlayAt("burner-ignite", transform.position);
        if (Application.isPlaying) BuildFlame();
    }

    public void Extinguish()
    {
        _lit = false;
        if (_flame != null) Destroy(_flame);
    }

    /// The barrel = the tallest renderer; the flame belongs at its top-centre.
    private Renderer Barrel()
    {
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return null;
        Renderer top = rends[0];
        for (int i = 1; i < rends.Length; i++)
            if (rends[i].bounds.max.y > top.bounds.max.y) top = rends[i];
        return top;
    }

    private Vector3 FlamePos()
    {
        // Hand-placed "FlameAnchor" child wins (drag it onto the burner mouth),
        // otherwise use the barrel top.
        var anchor = transform.Find("FlameAnchor");
        if (anchor != null) return anchor.position;
        var top = Barrel();
        if (top == null) return transform.position + Vector3.up * 0.12f;
        // Put the flame at the top of the BARREL, not the centre of the whole
        // burner+hose bounds (the long gas hose drags the
        // bounds centre off to the side).
        var tb = top.bounds;
        return new Vector3(tb.center.x, tb.max.y + 0.01f, tb.center.z);
    }

    /// Small looping procedural flame + warm light at the burner top.
    private void BuildFlame()
    {
        _flame = new GameObject("BurnerFlame");
        // Parent to the burner ROOT — NOT the barrel mesh. A nested barrel transform
        // can carry a tiny import scale, which shrinks the particles to invisibility.
        // The root still carries the flame when the burner is moved.
        _flame.transform.SetParent(transform, true);
        _flame.transform.position = FlamePos();   // FlameAnchor child wins, else barrel top
        // Neutralise the host's scale so the flame is always real-world sized,
        // identical on every burner regardless of its import scale.
        var ls = transform.lossyScale;
        _flame.transform.localScale = new Vector3(
            1f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
            1f / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));

        var ps = _flame.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true; main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 0.35f;
        main.startSpeed = 0.35f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.2f, 0.85f), new Color(0.4f, 0.65f, 1f, 0.8f));
        main.maxParticles = 60;
        var em = ps.emission; em.rateOverTime = 45f;
        var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.012f;
        sh.rotation = new Vector3(-90f, 0f, 0f);   // upward
        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.material = EffectVfx.ParticleMaterial();
        r.sortingOrder = 10;
        ps.Play();

        var light = _flame.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.25f);
        light.range = 0.8f;
        light.intensity = 1.2f;
        light.shadows = LightShadows.None;
    }
}
