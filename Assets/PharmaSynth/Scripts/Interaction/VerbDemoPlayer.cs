using System.Collections.Generic;
using UnityEngine;

/// Tutorial Mode's verb demonstration: a translucent ghost of the object the
/// player has to move, miming the actual motion of the step, twice, then gone.
///
/// ⭐ Deliberately NOT a hand. Rigging and animating a pair of hands is days of art for
/// a hint, and it answers the wrong question — the player already knows what a hand
/// looks like. Ghosting the BOTTLE tells them which object moves, from where, to where,
/// and how it is held at the end of the motion. The mesh-copy machinery is the same one
/// TutorialHighlighter's glow shell already proved.
///
/// Fires only on request (coach level 2, or poking Pharmee for help). Never loops
/// ambiently: a permanently miming ghost is noise, and it would compete with the glow
/// for the player's attention instead of reinforcing it.
public class VerbDemoPlayer : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [Tooltip("PharmaSynth/GuideOverlay, ZTest Always — assigned by Build Tutorial Scene Wiring.")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField, Min(0.5f)] private float loopSeconds = 3f;
    [SerializeField, Min(1)] private int loops = 2;

    private readonly List<GameObject> _ghosts = new List<GameObject>();
    private readonly List<Vector3> _offsets = new List<Vector3>();
    private readonly List<Quaternion> _rots = new List<Quaternion>();
    private float _startedAt = -1f;
    private VerbKind _kind;
    private Vector3 _from, _to;
    private float _revs = VerbDemoMath.DefaultRevs;

    /// Edit-mode / builder seam (AddComponent fires no Awake in edit mode).
    public void Bind(ExperimentRunner r, Material ghost) { runner = r; ghostMaterial = ghost; }

    private void Awake() { if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>(); }

    public bool IsPlaying => _startedAt >= 0f;

    /// Pure: the two ends of the motion, given a task's registered targets. The SOURCE
    /// (or TOOL) is what moves; when a step has no source — a verb performed on a vessel
    /// that is already in place, like stir or heat — the station is both mover and
    /// destination and the curve degenerates to a motion in place, which is right.
    /// Pool targets other than the chosen member are dropped; everything else passes
    /// through untouched. Pure so the suite can pin that the ghost and the glow agree.
    public static List<TaskTarget> CollapsePool(IReadOnlyList<TaskTarget> targets, Transform chosen)
    {
        var kept = new List<TaskTarget>();
        if (targets == null) return kept;
        for (int i = 0; i < targets.Count; i++)
            if (!targets[i].pool || targets[i].transform == chosen) kept.Add(targets[i]);
        return kept;
    }

    public static bool Endpoints(IReadOnlyList<TaskTarget> targets,
                                 out Transform mover, out Transform dest, out VerbKind kind)
    {
        mover = null; dest = null; kind = VerbKind.Place;
        if (targets == null) return false;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t.transform == null) continue;
            if (t.role == TargetRole.Source || t.role == TargetRole.Tool)
            {
                if (mover == null) { mover = t.transform; kind = t.verb; }
            }
            else if (dest == null) { dest = t.transform; kind = t.verb; }
        }
        if (mover == null) mover = dest;
        if (dest == null) dest = mover;
        return mover != null;
    }

    /// Start the demonstration for a step. Silently does nothing outside Tutorial Mode,
    /// with no material wired, or while one is already running — a second request during
    /// a demo must not stack two ghosts on the same object.
    public void Show(string taskId)
    {
        if (!TutorialSession.Active || !Application.isPlaying) return;
        if (ghostMaterial == null || IsPlaying || string.IsNullOrEmpty(taskId)) return;

        if (TaskTargetRegistry.TaskCount == 0) TutorialTargets.Build();
        // A pool of interchangeable glassware collapses to the SAME member the glow and the
        // arrow use — Endpoints takes the first destination it sees, and with pool
        // targets registered that is whichever extra happens to be listed first, so the
        // ghost would mime at one tube while the glow lit another. One chooser for every cue.
        var all = TaskTargetRegistry.Targets(taskId);
        var cam = Camera.main;
        var chosen = TaskTargetRegistry.ChoosePoolMember(all, TutorialHighlighter.IsHeld,
            cam != null ? cam.transform.position : Vector3.zero);
        if (!Endpoints(CollapsePool(all, chosen), out var mover, out var dest, out var kind))
            return;

        // Bounds CENTRE, never transform.position: a shelf bottle's origin sits at its
        // base, and LiquidPourer's world-space stream children would drag a naive
        // renderer sweep down to the floor. SolidWorldBounds is the one safe measurement.
        _from = ExperimentSceneBuilder.SolidWorldBounds(mover.gameObject).center;
        _to = ExperimentSceneBuilder.SolidWorldBounds(dest.gameObject).center;
        _kind = kind;
        _revs = RevsFor(dest);
        BuildGhost(mover);
        if (_ghosts.Count == 0) return;              // nothing solid to copy
        _startedAt = Time.time;
    }

    /// A stir/grind demo turns as many times as the real verb demands, read off the
    /// controller rather than assumed — a demonstration of a shorter motion than the
    /// step will accept teaches the player to give up early.
    private static float RevsFor(Transform dest)
    {
        if (dest == null) return VerbDemoMath.DefaultRevs;
        var stir = dest.GetComponent<StirController>();
        if (stir != null && stir.Math != null) return stir.Math.requiredRevs;
        var grind = dest.GetComponent<GrindController>();
        if (grind != null && grind.Math != null) return grind.Math.requiredRevs;
        return VerbDemoMath.DefaultRevs;
    }

    private const string GhostName = "VerbDemoGhost";

    /// A detached copy of the mover's solid meshes, driven independently while the real
    /// object stays exactly where it is.
    ///
    /// Filtered by ExperimentSceneBuilder.IsSolidMesh — the SHARED predicate. Rolling a
    /// private one here is what produced the hugely oversized x-ray ghosts: it missed the
    /// TMP_Text clause, so every floating ProximityLabel got silhouetted at its own large
    /// compensating scale.
    private void BuildGhost(Transform mover)
    {
        ClearGhost();
        Vector3 pivot = ExperimentSceneBuilder.SolidWorldBounds(mover.gameObject).center;
        foreach (var mf in mover.GetComponentsInChildren<MeshFilter>(false))
        {
            if (!ExperimentSceneBuilder.IsSolidMesh(mf) || mf.sharedMesh == null) continue;
            var g = new GameObject(GhostName);
            g.hideFlags = HideFlags.DontSave;
            var t = g.transform;
            t.SetParent(null, false);
            // World scale is copied ONCE onto an unparented object, so it is also the
            // local scale — no double-application (the trap that oversized the x-rays).
            t.localScale = mf.transform.lossyScale;
            g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = g.AddComponent<MeshRenderer>();
            int subs = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            var mats = new Material[subs];               // one per SUBMESH or it draws in half
            for (int i = 0; i < subs; i++) mats[i] = ghostMaterial;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _ghosts.Add(g);
            // Offsets are held relative to the bounds CENTRE so a two-part prop
            // (bottle + cap) tips as one object instead of shearing apart.
            _offsets.Add(mf.transform.position - pivot);
            _rots.Add(mf.transform.rotation);
        }
    }

    private void ClearGhost()
    {
        foreach (var g in _ghosts)
        {
            if (g == null) continue;
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }
        _ghosts.Clear();
        _offsets.Clear();
        _rots.Clear();
    }

    private void Update()
    {
        if (!IsPlaying) return;
        // The stage can be torn down mid-demo (restart, abort, leaving the mode) —
        // never leave a ghost hanging in the air.
        if (!TutorialSession.Active || runner == null || !runner.IsRunning) { Stop(); return; }

        float elapsed = Time.time - _startedAt;
        if (elapsed >= loopSeconds * loops) { Stop(); return; }

        float t = (elapsed % loopSeconds) / loopSeconds;
        var pose = VerbDemoMath.Sample(_kind, t, _from, _to, _revs);
        for (int i = 0; i < _ghosts.Count; i++)
        {
            var g = _ghosts[i];
            if (g == null) continue;
            g.transform.SetPositionAndRotation(
                pose.position + pose.rotation * _offsets[i],
                pose.rotation * _rots[i]);
        }
    }

    public void Stop() { ClearGhost(); _startedAt = -1f; }

    private void OnDisable() => Stop();
    private void OnDestroy() => ClearGhost();
}
