using UnityEngine;

/// The STIR verb: circle the glass rod inside this vessel's mouth while
/// it holds liquid and the stir task completes — works with the vessel on the
/// table OR held in a hand (everything is vessel-relative). Progress pops as
/// floating text; the TaskGraph condition owns completion (Retry-safe via the
/// ZoneSimStation resubscribe pattern).
public class StirController : MonoBehaviour
{
    private readonly OrbitMath _math = new OrbitMath();
    private ExperimentRunner _runner;
    private string _taskId;
    private LiquidPhysics _lp;
    private Transform _rod;
    private float _mouthRadius = 0.09f;
    private float _rimBandY = 0.30f;
    private bool _subscribed;
    private bool _doneAnnounced;
    private int _lastShownPct = -1;
    private float _nextPopupAt;

    public OrbitMath Math => _math;
    public string TaskId => _taskId;

    /// Builder seam. rod may be null — AutoFindRod picks up the bench glass rod.
    public void Bind(ExperimentRunner runner, string taskId, LiquidPhysics lp, Transform rod,
                     float mouthRadius = 0.09f, float rimBandY = 0.30f, float requiredRevs = 2.5f)
    {
        _runner = runner; _taskId = taskId; _lp = lp; _rod = rod;
        _mouthRadius = mouthRadius; _rimBandY = rimBandY;
        _math.requiredRevs = requiredRevs;
        Resubscribe();
        Register();
    }

    /// Teardown seam (ClearBenchBindings) — bench vessels survive rebuilds, and
    /// edit-mode DestroyImmediate skips OnDisable (ghost subscriptions).
    public void Detach()
    { if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; } }

    private void OnDestroy() => Detach();

    // Also re-Register on enable — a station toggled off/on mid-run would
    // otherwise lose its condition on the current graph until the next start.
    private void OnEnable() { Resubscribe(); Register(); }
    private void OnDisable() { if (_runner != null) _runner.ExperimentStarted -= OnStarted; _subscribed = false; }

    private void Resubscribe()
    {
        if (_runner == null || _subscribed) return;
        _runner.ExperimentStarted += OnStarted;
        _subscribed = true;
    }

    private void OnStarted(ExperimentModuleDefinition _) => Register();

    public void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _math.Reset();
        _doneAnnounced = false;
        _lastShownPct = -1;
        _runner.Graph.RegisterCondition(_taskId, () => _math.IsDone);
    }

    /// Rod TIP = the closest point of the rod's geometry to the vessel —
    /// origin-agnostic and ANCHOR-FREE (the glass rod's pivot sits at its
    /// handle end; tracking the ORIGIN missed the mouth entirely — the same
    /// latent bug that broke the grind, fixed the same way as
    /// GrindController.PestleTip; no tip anchor to place or drift).
    private readonly System.Collections.Generic.Dictionary<Transform, BoundsUtil.RendererCache> _rodRends
        = new System.Collections.Generic.Dictionary<Transform, BoundsUtil.RendererCache>();
    private readonly BoundsUtil.RendererCache _vesselRends = new BoundsUtil.RendererCache();

    private Vector3 TipOf(Transform rod, Vector3 to)
    {
        if (rod == null) return to + Vector3.up;
        if (!_rodRends.TryGetValue(rod, out var cache)) _rodRends[rod] = cache = new BoundsUtil.RendererCache();
        if (!BoundsUtil.TryUnion(cache.Of(rod), out var b)) return rod.position;
        return b.ClosestPoint(to);
    }

    private Vector3 RodTip(Vector3 to) => TipOf(_rod, to);

    /// The stir axis: centre of the vessel's REAL bounds (bench glass keeps
    /// whatever pivot its model author left, rarely the body centre).
    private Vector3 MouthCenter()
        => BoundsUtil.TryUnion(_vesselRends.Of(transform), out var b) ? b.center : transform.position;

    /// True while the rod tip is inside the mouth band (vessel-relative).
    public bool RodInMouth()
    {
        if (_rod == null) return false;
        Vector3 c = MouthCenter();
        Vector3 d = RodTip(c) - c;
        float horiz = new Vector2(d.x, d.z).magnitude;
        return horiz <= _mouthRadius && Mathf.Abs(d.y) <= _rimBandY;
    }

    /// ALL the lab's stirring rods (the bench carries Eq_GlassRod AND
    /// GlassRod_2 ; tracking only one leaves the other stirring nothing):
    /// whichever rod is nearest the vessel each poll is the tracked one, so
    /// EITHER works, wherever it currently lies. Mirrors AutoFindPestle.
    private readonly System.Collections.Generic.List<Transform> _rodCandidates
        = new System.Collections.Generic.List<Transform>();
    private float _nextRodScan;

    private void FindRodCandidates()
    {
        _rodCandidates.Clear();
        foreach (var li in FindObjectsByType<LabItem>())
        {
            if (li == null) continue;
            string id = ((li.itemId ?? "") + " " + (li.displayName ?? "") + " " + li.name).ToLowerInvariant();
            if (id.Contains("glassrod") || id.Contains("glass rod") || id.Contains("stirring rod"))
                _rodCandidates.Add(li.transform);
        }
        if (_rod != null && !_rodCandidates.Contains(_rod)) _rodCandidates.Add(_rod);
    }

    private void Update()
    {
        if (_runner == null || !_runner.IsRunning || _lp == null) return;
        if (_lp.currentLiquidVolume <= 1f) return;   // nothing to stir yet

        Vector3 c = MouthCenter();
        if (Time.time >= _nextRodScan)
        { _nextRodScan = Time.time + 2f; FindRodCandidates(); }
        Transform best = _rod; float bestD = float.MaxValue;
        foreach (var r in _rodCandidates)
        {
            if (r == null) continue;
            float d = Vector3.Distance(TipOf(r, c), c);
            if (d < bestD) { bestD = d; best = r; }
        }
        _rod = best;
        if (_rod == null) return;

        Vector3 dd = RodTip(c) - c;
        Tick(dd.x, dd.z, RodInMouth());
    }

    /// One stir sample (public so tests can drive it without physics).
    public void Tick(float x, float z, bool inside)
    {
        bool wasDone = _math.IsDone;
        _math.Feed(x, z, inside);

        if (!inside) return;
        int pct = Mathf.RoundToInt(_math.Progress01 * 100f);
        if (!_math.IsDone && pct != _lastShownPct && pct % 25 == 0 && pct > 0 && Time.time >= _nextPopupAt)
        {
            _lastShownPct = pct;
            _nextPopupAt = Time.time + 0.8f;
            FloatingText.Show("Stirring... " + pct + "%", transform.position + Vector3.up * 0.25f, new Color(0.7f, 0.9f, 1f), 0.8f);
            AudioService.TryPlayAt("stir", transform.position);
        }
        if (_math.IsDone && !wasDone && !_doneAnnounced)
        {
            _doneAnnounced = true;
            FloatingText.Show("Well stirred!", transform.position + Vector3.up * 0.28f, new Color(0.6f, 1f, 0.7f));
            AudioService.TryPlayAt("mixture-complete", transform.position);
        }
    }
}
