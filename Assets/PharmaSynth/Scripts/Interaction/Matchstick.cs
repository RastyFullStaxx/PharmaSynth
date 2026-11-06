using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// A grabbable matchstick (manuscript: combustion/flammability tests, Exp 3/4/7
/// + the methane splint). Two ways to light it: STRIKE it — swipe the
/// held match across a striker surface (the matches box, a burner base) — or
/// hold its head near anything already hot. A lit match then ignites burners
/// (BurnerController) and fires the methane splint test. Pure predicates for
/// the suite.
public class Matchstick : MonoBehaviour
{
    public const float IgniteDistance = 0.22f;
    public const float IgniteTempC = 80f;
    public const float StrikeMinSpeed = 0.35f;
    [SerializeField] private float burnSeconds = 25f;
    [Tooltip("The phosphorus head is at the NEGATIVE end of the match's longest axis. Flip if the flame lights the wrong end.")]
    [SerializeField] private bool headAtPositiveEnd = false;

    private bool _lit, _spent;

    private static readonly System.Collections.Generic.List<Matchstick> s_live = new System.Collections.Generic.List<Matchstick>();
    /// Every enabled match. Play mode reads a list kept by OnEnable/OnDisable; edit mode
    /// (simulators, tests) searches the scene, because edit-mode AddComponent never runs OnEnable.
    public static System.Collections.Generic.IReadOnlyList<Matchstick> Active
        => Application.isPlaying ? (System.Collections.Generic.IReadOnlyList<Matchstick>)s_live
                                 : FindObjectsByType<Matchstick>();
    private void OnEnable() { if (!s_live.Contains(this)) s_live.Add(this); }
    private void OnDisable() => s_live.Remove(this);
    private float _litAt;
    private float _nextScan;
    private GameObject _flame;
    private XRGrab _grab;

    public bool IsLit => _lit;
    public bool IsSpent => _spent;

    /// Pure: does a heat source at this distance/temperature ignite the match?
    public static bool ShouldIgnite(float distance, float tempC, bool alreadyLit, bool spent)
        => !alreadyLit && !spent && distance <= IgniteDistance && tempC >= IgniteTempC;

    /// Pure: does a swipe against a striker surface light the match?
    /// Must be HELD (a match knocked off the shelf doesn't self-ignite), fresh,
    /// and moving fast enough to count as a strike.
    public static bool ShouldStrike(bool held, bool lit, bool spent, float relSpeed, bool strikerSurface, float minSpeed = StrikeMinSpeed)
        => held && !lit && !spent && strikerSurface && relSpeed >= minSpeed;

    private void OnCollisionEnter(Collision collision)
    {
        if (_grab == null) _grab = GetComponent<XRGrab>();
        bool striker = collision.collider != null && collision.collider.GetComponentInParent<MatchStrikerSurface>() != null;
        bool held = _grab != null && _grab.isSelected;
        float speed = collision.relativeVelocity.magnitude;
        if (ShouldStrike(held, _lit, _spent, speed, striker)) { Ignite(); _weakStrikes = 0; return; }
        // A too-gentle swipe still sparks a little so the player sees
        // the box responds, and after two weak tries say what to do differently.
        if (IsWeakStrike(held, _lit, _spent, speed, striker) && Application.isPlaying)
        {
            Vector3 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            EffectVfx.ColorFlash(at, new Color(1f, 0.7f, 0.3f, 0.8f));
            if (++_weakStrikes >= 2 && Time.time >= _nextStrikeHint)
            {
                _nextStrikeHint = Time.time + 5f;
                FloatingText.Show("Strike it faster along the box", at + Vector3.up * 0.08f, new Color(1f, 0.85f, 0.55f), 1.1f);
            }
        }
    }

    private int _weakStrikes;
    private float _nextStrikeHint;

    /// Pure: a real swipe at the striker that was just too slow (a resting touch is not one).
    public const float WeakStrikeMinSpeed = 0.08f;
    public static bool IsWeakStrike(bool held, bool lit, bool spent, float relSpeed, bool strikerSurface)
        => held && !lit && !spent && strikerSurface && relSpeed >= WeakStrikeMinSpeed && relSpeed < StrikeMinSpeed;

    private void Update()
    {
        if (_lit && Time.time - _litAt > burnSeconds) Extinguish(true);
        if (_lit || _spent || !Application.isPlaying) return;
        // Secondary path: light off an already-hot station. Throttled — the
        // scene scan is too costly to run every frame per match.
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.5f;

        foreach (var sim in FindObjectsByType<TemperatureSim>())
        {
            float d = Vector3.Distance(transform.position, sim.transform.position);
            if (ShouldIgnite(d, sim.AtLeast(IgniteTempC) ? IgniteTempC : 0f, _lit, _spent))
            {
                Ignite();
                break;
            }
        }
    }

    public void Ignite()
    {
        if (_lit || _spent) return;
        _lit = true;
        _litAt = Time.time;
        EffectVfx.FlamePop(transform.position);
        AudioService.TryPlayAt("burner-ignite", transform.position);
        if (Application.isPlaying)
        {
            _flame = new GameObject("MatchFlame");
            // Sit the flame on the phosphorus HEAD, not a fixed offset
            // near the match's origin, or it burns mid-stick.
            // The head is one end of the longest axis; parent to the match so it
            // tracks as the stick is waved around.
            _flame.transform.SetParent(transform, true);
            _flame.transform.position = HeadWorldPos();

            // A small looping flame at the tip.
            var ps = _flame.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = 0.22f;
            main.startSpeed = 0.16f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.62f, 0.2f, 0.9f), new Color(1f, 0.85f, 0.35f, 0.85f));
            main.maxParticles = 24;
            var em = ps.emission; em.rateOverTime = 35f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 6f; sh.radius = 0.003f;
            sh.rotation = new Vector3(-90f, 0f, 0f);   // upward from the tip
            var pr = ps.GetComponent<ParticleSystemRenderer>();
            pr.material = EffectVfx.ParticleMaterial();
            pr.sortingOrder = 10;
            ps.Play();

            var light = _flame.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.range = 0.6f;
            light.intensity = 1.1f;
            light.shadows = LightShadows.None;
        }
    }

    public void Extinguish(bool spent)
    {
        _lit = false;
        if (spent) _spent = true;
        if (_flame != null) Destroy(_flame);
    }

    /// World position of the match HEAD. Prefers a hand-placed "FlameAnchor" child
    /// (drag it onto the phosphorus tip) — otherwise falls back to
    /// the far end of the longest local axis of the combined mesh bounds.
    /// Where the flame is — what a burner or a flame test should measure to.
    public Vector3 HeadPosition => HeadWorldPos();

    private Vector3 HeadWorldPos()
    {
        var anchor = transform.Find("FlameAnchor");
        if (anchor != null) return anchor.position;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position + transform.up * 0.05f;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Vector3 axis = LongestLocalAxis(transform, b, out float halfLen);
        return b.center + axis * (halfLen * (headAtPositiveEnd ? 1f : -1f));
    }

    /// The object-local axis (right/up/forward) along which an AABB is longest,
    /// plus that half-extent. Shared shape logic for tip placement.
    public static Vector3 LongestLocalAxis(Transform t, Bounds b, out float halfExtent)
    {
        Vector3 best = t.up; float bestE = -1f;
        foreach (var a in new[] { t.right, t.up, t.forward })
        {
            float e = Mathf.Abs(a.x) * b.extents.x + Mathf.Abs(a.y) * b.extents.y + Mathf.Abs(a.z) * b.extents.z;
            if (e > bestE) { bestE = e; best = a; }
        }
        halfExtent = bestE;
        return best;
    }
}
