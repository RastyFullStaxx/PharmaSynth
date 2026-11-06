using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// The GRIND verb: work the pestle in circles inside this mortar's bowl
/// and the grind task completes (dual-path on Methane: the legacy zone-touch
/// still works). When done, a powder heap appears in the bowl. A null/empty
/// taskId makes it purely educational — staged mortars still grind visually.
public class GrindController : MonoBehaviour
{
    private readonly OrbitMath _math = new OrbitMath();
    private ExperimentRunner _runner;
    private string _taskId;
    private Transform _pestle;
    private bool _subscribed;
    private bool _doneAnnounced;
    private int _lastShownPct = -1;
    private float _nextPopupAt;
    private float _nextDustAt;   // throttles the grinding dust puff

    // Forgiving completion. VR grinding is
    // rarely a clean circle, and tracking the pestle's ORIGIN misses the bowl when
    // the origin is the handle. The pestle TIP (closest point to the bowl) is
    // tracked, and the grind completes on EITHER 3 revs OR ~1.1 m of any motion
    // inside a generous bowl radius.
    private float _travel;
    private Vector3 _lastTip;
    private bool _haveTip;
    private float _nextSfxAt;
    private float _nextHintAt;          // throttles the "add the reagent first" hint
    private LiquidPhysics _vessel;      // the mortar's own contents (must be loaded to grind)
    private const float TravelNeeded = 1.1f;
    private const float GrindReach = 0.12f;

    public OrbitMath Math => _math;

    /// The grind is finished once EITHER circular revs OR total motion is enough.
    public bool IsGrindComplete() => _math.IsDone || _travel >= TravelNeeded;

    /// Pure (suite): may this mortar be ground? Only when it actually holds the
    /// reagent — grinding an EMPTY mortar must not complete the step while the
    /// powder is still on the scoop. A mortar with no
    /// LiquidPhysics at all (cosmetic/educational) always grinds.
    public static bool CanGrind(bool hasVessel, float contentsMl) => !hasVessel || contentsMl > 0.5f;

    /// This mortar currently holds something to grind.
    private bool HasMixture()
    {
        if (_vessel == null) _vessel = GetComponent<LiquidPhysics>();
        return CanGrind(_vessel != null, _vessel != null ? _vessel.currentLiquidVolume : 0f);
    }

    /// Builder seam. taskId may be null/empty (cosmetic grind). bowlRadius/bowlBandY
    /// are legacy no-ops kept for call-site compatibility — the bowl is now measured
    /// from the mortar's own bounds and the pestle tip.
    public void Bind(ExperimentRunner runner, string taskId, Transform pestle,
                     float bowlRadius = 0.09f, float bowlBandY = 0.16f, float requiredRevs = 3f)
    {
        _runner = runner; _taskId = taskId; _pestle = pestle;
        _math.requiredRevs = requiredRevs;
        Resubscribe();
        Register();
    }

    /// Hand the grind its runner at runtime — _runner isn't serialized, so after a
    /// domain reload it's null and Register() would no-op (the task never completes).
    /// The Methane rig calls this when it aims the mortar at "prepare-mixture"
    /// so the runner is set.
    public void BindRunner(ExperimentRunner runner)
    {
        _runner = runner;
        Resubscribe();
        Register();
    }

    /// Re-point which task this grind completes (the Methane rig aims the
    /// shared workspace mortar at "prepare-mixture" while its tutorial runs, then
    /// clears it back to cosmetic afterwards). Re-registers the condition.
    public void SetTaskId(string taskId) { _taskId = taskId; Register(); }
    public string TaskId => _taskId;

    // Also re-Register on enable (see StirController note).
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
        _math.Reset();
        _travel = 0f; _haveTip = false;
        _doneAnnounced = false;
        _lastShownPct = -1;
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, IsGrindComplete);
    }

    /// Bowl centre in world space. A hand-placed "BowlAnchor" child wins (drag it
    /// into the bowl so the ground powder + dust land exactly right);
    /// otherwise the middle of the mortar, a touch above centre.
    private Vector3 BowlCenter()
    {
        var a = transform.Find("BowlAnchor");
        if (a != null) return a.position;
        var b = MortarBounds();
        return new Vector3(b.center.x, b.center.y + b.size.y * 0.12f, b.center.z);
    }

    private readonly BoundsUtil.RendererCache _mortarRends = new BoundsUtil.RendererCache();
    private readonly BoundsUtil.RendererCache _pestleRends = new BoundsUtil.RendererCache();
    private float _nextPestleScan;

    private Bounds MortarBounds()
    {
        var rends = _mortarRends.Of(transform);
        int first = -1;
        for (int i = 0; i < rends.Length && first < 0; i++) if (rends[i] != null) first = i;
        if (first < 0) return new Bounds(transform.position, Vector3.one * 0.06f);
        Bounds b = rends[first].bounds;
        for (int i = first + 1; i < rends.Length; i++)
            if (rends[i] != null && rends[i].name != "PowderHeap" && rends[i].name != "Powder") b.Encapsulate(rends[i].bounds);
        return b;
    }

    /// Closest point of the pestle to the bowl — origin-agnostic, so it works no
    /// matter which end of the pestle model is its transform pivot.
    private Vector3 PestleTip(Vector3 to)
    {
        if (_pestle == null) return to + Vector3.up;
        if (!BoundsUtil.TryUnion(_pestleRends.Of(_pestle), out var b)) return _pestle.position;
        return b.ClosestPoint(to);
    }

    /// Last-resort pestle lookup so the grind still works if nothing bound one.
    private void AutoFindPestle()
    {
        foreach (var li in FindObjectsByType<LabItem>())
        {
            if (li == null) continue;
            string id = (li.itemId ?? "") + " " + (li.displayName ?? "") + " " + li.name;
            if (id.ToLowerInvariant().Contains("pestle")) { _pestle = li.transform; return; }
        }
    }

    private void Update()
    {
        // The scene-wide search only runs while nothing is bound; twice a second is enough.
        if (_pestle == null && (!Application.isPlaying || Time.time >= _nextPestleScan))
        { _nextPestleScan = Time.time + 0.5f; AutoFindPestle(); }
        if (_pestle == null) return;
        if (_runner != null && !string.IsNullOrEmpty(_taskId) && !_runner.IsRunning) return;

        // Grind ONLY while the pestle is actually held (a pestle resting in the bowl
        // must not keep "grinding" and smoke the mortar at play-start).
        var pg = _pestle.GetComponent<XRGrab>();
        bool held = pg != null && pg.isSelected;

        Vector3 bowl = BowlCenter();
        Vector3 tip = PestleTip(bowl);
        bool reaching = held && Vector3.Distance(tip, bowl) <= GrindReach;

        // …and only while the mortar actually CONTAINS the reagent, so an
        // empty mortar cannot finish the step while the powder
        // is still on the scoop. A mortar with no LiquidPhysics is a
        // cosmetic/educational mortar and still grinds freely.
        bool inside = reaching && HasMixture();
        if (reaching && !HasMixture() && Application.isPlaying && Time.time >= _nextHintAt)
        {
            _nextHintAt = Time.time + 2.5f;
            FloatingText.Show("Add the reagent to the mortar first", BowlCenter() + Vector3.up * 0.14f,
                              new Color(1f, 0.85f, 0.5f), 1.2f);
        }

        // Accumulate pestle motion inside the bowl (forgiving path).
        if (inside && _haveTip)
            _travel += Mathf.Min(Vector3.Distance(tip, _lastTip), 0.05f);
        _lastTip = tip; _haveTip = true;

        Vector3 d = tip - bowl;
        Tick(d.x, d.z, inside);
    }

    /// One grind sample (public so tests can drive it without physics).
    public void Tick(float x, float z, bool inside)
    {
        _math.Feed(x, z, inside);

        if (!inside) return;

        bool done = IsGrindComplete();
        // Combined progress: whichever of circular-revs / total-motion is further.
        float progress = Mathf.Max(_math.Progress01, _travel / TravelNeeded);

        // Grinding feedback while it works: a rising tan dust puff + a soft
        // repeating grind sound, so the action clearly reads.
        if (!done && Application.isPlaying && Time.time >= _nextDustAt)
        {
            _nextDustAt = Time.time + 0.25f;
            EffectVfx.Smoke(BowlCenter(), new Color(0.82f, 0.72f, 0.55f, 0.45f));
            if (Time.time >= _nextSfxAt) { _nextSfxAt = Time.time + 0.35f; AudioService.TryPlayFirstAt(transform.position, 0.6f, "grind", "stir"); }
        }

        int pct = Mathf.RoundToInt(progress * 100f);
        if (!done && pct >= _lastShownPct + 20 && pct > 0 && Time.time >= _nextPopupAt)
        {
            _lastShownPct = pct - (pct % 20);
            _nextPopupAt = Time.time + 0.5f;
            FloatingText.Show("Grinding... " + pct + "%", BowlCenter() + Vector3.up * 0.12f, new Color(1f, 0.92f, 0.7f), 0.8f);
        }
        if (done && !_doneAnnounced)
        {
            _doneAnnounced = true;
            FloatingText.Show("Ground & mixed to a fine powder!", BowlCenter() + Vector3.up * 0.16f, new Color(0.6f, 1f, 0.7f));
            AudioService.TryPlayAt("mixture-complete", transform.position);
            MarkGround();
        }
    }

    /// On completion, UPDATE the existing scooped mound rather than spawn a second
    /// blob. Reuses the mortar's "Powder" child; only makes one if
    /// nothing was scooped in. Recolours it to a fine, mixed look.
    private void MarkGround()
    {
        if (!Application.isPlaying) return;
        Renderer powder = null;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
            if (r.name == "Powder" || r.name == "PowderHeap") { powder = r; break; }

        if (powder == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Powder";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(transform, true);
            go.transform.position = BowlCenter();
            var b = MortarBounds();
            float w = Mathf.Min(Mathf.Min(b.size.x, b.size.z) * 0.42f, 0.032f);
            var ls = transform.lossyScale;
            go.transform.localScale = new Vector3(
                w / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                (w * 0.4f) / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                w / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            powder = go.GetComponent<Renderer>();
            powder.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            powder.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        }
        // Recolour the SAME mound to a fine ground-powder look — no extra blob.
        var mat = powder.sharedMaterial;
        var c = new Color(0.86f, 0.84f, 0.78f);
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
        EffectVfx.Smoke(BowlCenter(), new Color(0.92f, 0.88f, 0.75f, 0.5f));
    }
}
