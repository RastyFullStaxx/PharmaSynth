using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// Tutorial Mode's ground path: chevrons on the floor that flow from the player's
/// feet toward whatever the current step needs.
///
/// ⛔ It routes on the NAVMESH, not in a straight line. The lab's benches sit in the middle
/// of the room, so a straight line to a wall shelf points through solid furniture — an
/// arrow that lies about the way there is worse than no arrow. `NavMesh.CalculatePath`
/// needs no agent; it just needs the surface baked (Tools ▸ PharmaSynth ▸ Build Lab NavMesh).
///
/// ⭐ It never draws at the same time as the beacon. The path answers "which way round the
/// benches", the beacon answers "which object, through that cabinet door" — and Tutorial
/// Mode already spends most of the player's attention budget. `GuidePathMath.ShowPath`
/// owns that split so the rule lives in one testable place.
public class GuidePath : MonoBehaviour
{
    /// Read by WaypointGuide so the two cues stay mutually exclusive without either one
    /// re-deriving the rule.
    public static GuidePath Instance { get; private set; }
    public bool PathShown { get; private set; }

    /// Diagnostics for the play-mode autopilot: a screenshot of a floor path is easy to
    /// misread (the marks are small, and the camera may be looking at a wall), so the
    /// harness reads these numbers instead of squinting at pixels.
    public int ActiveChevrons { get; private set; }
    public float LastDistance { get; private set; }
    public int RouteCorners => _corners.Count;

    /// What the path was last aiming at, and whether each end could be put on the navmesh.
    /// Reported by the autopilot: "no path" is useless without knowing WHICH object and
    /// WHICH end failed.
    public string LastTargetName { get; private set; } = "-";
    public Vector3 LastGoal { get; private set; }
    public bool StartOnMesh { get; private set; }
    public bool GoalOnMesh { get; private set; }

    [SerializeField] private ExperimentRunner runner;
    [Tooltip("PharmaSynth/GuideOverlay — assigned by Build Tutorial Scene Wiring.")]
    [SerializeField] private Material chevronMaterial;
    [SerializeField] private float spacing = GuidePathMath.Spacing;
    [SerializeField] private float flowSpeed = GuidePathMath.FlowSpeed;
    // The mark is a real chevron mesh authored in METRES (GuidePathMath.ChevronGeometry),
    // so this is an overall multiplier and 1 means "the authored size".
    [SerializeField] private float chevronScale = 1f;
    [SerializeField] private float chevronWidth = GuidePathMath.ChevronWidth;
    [SerializeField] private float chevronDepth = GuidePathMath.ChevronDepth;
    [SerializeField] private float chevronThickness = GuidePathMath.ChevronThickness;
    [SerializeField] private float floorLift = 0.02f;      // above the floor, or it z-fights
    [SerializeField] private float repathSeconds = 0.25f;  // route recompute, not redraw

    private readonly List<Transform> _pool = new List<Transform>();
    private readonly List<MeshRenderer> _renderers = new List<MeshRenderer>();
    private MaterialPropertyBlock _mpb;
    private Mesh _chevron;
    private readonly List<Vector3> _corners = new List<Vector3>();
    private NavMeshPath _path;
    private float _nextRepath;
    private Transform _cam;
    private PharmeeGatekeeper _gate;

    /// Edit-mode / builder seam — AddComponent fires no Awake in edit mode (house rule).
    public void Bind(ExperimentRunner r, Material chevron) { runner = r; chevronMaterial = chevron; }
    public void SetTuning(float spacingM, float flow, float scale)
    { spacing = spacingM; flowSpeed = flow; chevronScale = scale; }

    private void Awake()
    {
        Instance = this;
        if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>();
        _path = new NavMeshPath();
    }

    private void OnEnable() { Instance = this; }
    private void OnDisable() { if (Instance == this) Instance = null; HideAll(); }

    private void Update()
    {
        if (TimeSkipController.IsSkipping || chevronMaterial == null) { HideAll(); return; }

        if (_cam == null && Camera.main != null) _cam = Camera.main.transform;
        if (_cam == null) { HideAll(); return; }

        // ⭐ EVERY MODE, not Tutorial-only: a player who knows the chemistry
        // perfectly still has to find the glassware. The target map is DERIVED,
        // so build it on demand instead of relying on the tutorial highlighter's build.
        Transform target = null;
        if (runner != null && runner.Graph != null && runner.IsRunning)
        {
            string id = null;
            foreach (var t in runner.Graph.AvailableTasks()) { id = t.taskId; break; }
            if (TaskTargetRegistry.TaskCount == 0) TutorialTargets.Build();
            // Position-aware: a pool of interchangeable tubes resolves to the one in
            // the player's hand, else the nearest — the arrow follows the tube they picked up.
            target = TaskTargetRegistry.PickTarget(id, _cam.position);
        }
        // Before the run starts the GATE owns the guidance: the locker, then the door.
        if (target == null)
        {
            if (_gate == null) _gate = FindAnyObjectByType<PharmeeGatekeeper>();
            if (_gate != null) target = _gate.GateGuideTarget;
        }
        if (target == null) { HideAll(); return; }

        // Bounds CENTRE, never transform.position: a shelf bottle's origin sits at its base,
        // and LiquidPourer's world-space stream children would drag a naive sweep to the
        // floor (→ Gotchas).
        Vector3 goal = ExperimentSceneBuilder.SolidWorldBounds(target.gameObject).center;
        LastTargetName = target.name;
        LastGoal = goal;
        Vector3 feet = new Vector3(_cam.position.x, _cam.position.y, _cam.position.z);

        // Recompute the ROUTE occasionally; redraw the flow every frame. The route only
        // changes when the player walks, but the chevrons must slide smoothly or the path
        // reads as a row of stationary marks rather than as motion toward something.
        if (Time.time >= _nextRepath)
        {
            _nextRepath = Time.time + repathSeconds;
            RecomputeRoute(feet, goal);
        }

        float dist = Vector3.Distance(feet, goal);
        LastDistance = dist;
        PathShown = GuidePathMath.ShowPath(dist, _corners.Count >= 2);
        if (!PathShown) { HideAll(); return; }

        var marks = GuidePathMath.Build(_corners, Time.time, spacing, flowSpeed);
        ActiveChevrons = marks.Count;
        Draw(marks);
    }

    /// Both ends must be ON the navmesh before a path can be asked for — the player is
    /// usually standing on it, but a target sits on a bench or a shelf, well above it.
    /// Failing to find a route is a normal outcome (an object inside a closed cabinet has
    /// no floor path that would explain it): we simply draw nothing and let the beacon take
    /// over, which is exactly the split ShowPath/ShowBeacon encode.
    private void RecomputeRoute(Vector3 from, Vector3 to)
    {
        _corners.Clear();
        if (_path == null) _path = new NavMeshPath();

        StartOnMesh = NavMesh.SamplePosition(from, out var startHit, 2f, NavMesh.AllAreas);
        GoalOnMesh = NavMesh.SamplePosition(to, out _, 3f, NavMesh.AllAreas);
        if (!StartOnMesh) return;
        // 3 m is enough to reach the floor in front of any shelf. (Widening this to 5 m was
        // TRIED as a fix for midterm-acetone's missing path and changed nothing, so the
        // radius is not the cause — reverted rather than left in as cargo.)
        if (!NavMesh.SamplePosition(to, out var endHit, 3f, NavMesh.AllAreas)) return;
        if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, _path)) return;
        if (_path.status == NavMeshPathStatus.PathInvalid) return;

        foreach (var c in _path.corners) _corners.Add(c + Vector3.up * floorLift);
    }

    private void Draw(List<GuidePathMath.Chevron> marks)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        Color baseColour = chevronMaterial.HasProperty(BaseColourId)
            ? chevronMaterial.GetColor(BaseColourId) : Color.white;

        for (int i = 0; i < marks.Count; i++)
        {
            var q = At(i);
            // ⚠ NO Euler(90,0,0) here. The old mark was an upright quad that had to be laid
            // down; the chevron mesh is authored FLAT already, so re-applying that rotation
            // would stand every arrow on edge across the route.
            q.SetPositionAndRotation(marks[i].position, marks[i].rotation);
            q.localScale = Vector3.one * chevronScale;
            if (!q.gameObject.activeSelf) q.gameObject.SetActive(true);

            // Ramp the ends so a chevron does not POP into existence at the player's feet
            // and vanish at full strength on arrival. This is what reads as flow.
            var mr = _renderers[i];
            if (mr != null)
            {
                var c = baseColour;
                c.a *= GuidePathMath.FadeAt(marks[i].along01);
                _mpb.SetColor(BaseColourId, c);
                mr.SetPropertyBlock(_mpb);
            }
        }
        for (int i = marks.Count; i < _pool.Count; i++)
            if (_pool[i] != null && _pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
    }

    static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");

    /// One shared chevron mesh for the whole pool — the shape never changes at runtime.
    private Mesh Chevron()
    {
        if (_chevron != null) return _chevron;
        GuidePathMath.ChevronGeometry(out var verts, out var tris,
                                      chevronWidth, chevronDepth, chevronThickness);
        _chevron = new Mesh { name = "GuideChevron", hideFlags = HideFlags.DontSave };
        _chevron.SetVertices(new List<Vector3>(verts));
        _chevron.SetTriangles(tris, 0);
        _chevron.RecalculateNormals();
        _chevron.RecalculateBounds();
        return _chevron;
    }

    private void OnDestroy()
    {
        if (_chevron == null) return;
        if (Application.isPlaying) Destroy(_chevron); else DestroyImmediate(_chevron);
        _chevron = null;
    }

    private Transform At(int i)
    {
        while (_pool.Count <= i)
        {
            // Built from parts rather than CreatePrimitive: a primitive brings a collider
            // (a guidance mark must never be something the player can bump into) and a quad
            // mesh we would only throw away.
            var go = new GameObject("GuideChevron", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.DontSave };
            go.GetComponent<MeshFilter>().sharedMesh = Chevron();
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = chevronMaterial;          // sharedMaterial: .material instances in edit mode
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            go.transform.SetParent(transform, false);
            _pool.Add(go.transform);
            _renderers.Add(mr);
        }
        return _pool[i];
    }

    private void HideAll()
    {
        PathShown = false;
        ActiveChevrons = 0;
        // ⚠ Drop the route too. Otherwise a module can be asked about the path
        // and answer with the PREVIOUS module's corners, reporting a route
        // before the new one is computed (a 6.1 m target read as "inside the 2 m handover").
        _corners.Clear();
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] != null && _pool[i].gameObject.activeSelf) _pool[i].gameObject.SetActive(false);
    }
}
