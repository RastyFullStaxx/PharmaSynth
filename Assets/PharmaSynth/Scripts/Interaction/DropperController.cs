using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for the dropper/pipette verb. The liquid twin of
/// ScoopMath: VR cannot hit an accurate volume, so the ACTION is discretised and
/// the accuracy problem disappears — the student still prepares every tube.
///
/// The contract that makes this work: **the number in the manuscript instruction
/// IS the squeeze count, whatever its unit.** Manuscript Exp 2 measures in three
/// units — "5 drops of Ferric Chloride", "2 ml of Tollen's reagent", "0.1 g of
/// salicylic acid" — and the first two both become countable squeezes:
///     "5 drops" -> 5 squeezes   (1 squeeze = 1 drop; physically honest)
///     "2 ml"    -> 2 squeezes   (1 squeeze = 1 ml; the deliberate abstraction)
/// Bulk volumes (the 10 ml of water) stay on the tilt-pour with its tolerance
/// band; grams go to the spatula (ScoopMath). The watch panel prints the real
/// quantity for learning and the squeeze count underneath it.
///
/// Each squeeze deposits MlPerSqueeze into the target vessel through the normal
/// AddLiquid path, so an existing LiquidTaskBinding.requiredMl does the counting
/// for free ("5 drops" = requiredMl 5) — no new task plumbing, and a miscount is
/// an unambiguous graded mistake that eats finite reagent.
public static class DropperMath
{
    /// One squeeze = one unit of the instruction (one drop, or one ml).
    public const float MlPerSqueeze = 1f;

    /// A full dropper holds ten squeezes — the largest single Exp 2 instruction
    /// ("10 drops of ethanol"), so one fill covers any one step and multi-tube
    /// steps ("5 drops into each of five tubes") pace into natural refills.
    public const float Capacity = 10f;

    /// Draw only from a LIQUID SUPPLY with something left, and only into an empty
    /// dropper (no topping up a partial charge — the count would be ambiguous).
    ///
    /// isTaskVessel: right after the last squeeze the
    /// now-empty dropper is still over the test tube, and without this guard the next
    /// frame the draw branch would suck the delivered reagent back out. A task
    /// vessel (anything carrying a LiquidTaskBinding) is a destination, never a
    /// source — draws come from the shelf bottles only.
    public static bool CanFill(float loadedMl, PhysicalState state, float availableMl,
                               bool isTaskVessel = false)
        => !isTaskVessel && loadedMl <= 0.001f
           && state == PhysicalState.Liquid && availableMl > 0.01f;

    /// A draw takes a full dropper, or whatever the bottle has left.
    public static float FillCharge(float availableMl, float capacity = Capacity)
        => Mathf.Min(Mathf.Max(0f, availableMl), capacity);

    /// Squeeze only a loaded dropper held over some OTHER container.
    public static bool CanSqueeze(float loadedMl, bool overTarget, bool sameAsSource)
        => loadedMl > 0.001f && overTarget && !sameAsSource;

    /// A loaded squeeze over NOTHING wastes the drop onto the floor. Deliberate:
    /// it is how the student empties a dropper.
    /// The wasted reagent is simply gone — that is its own small lesson.
    public static bool CanWaste(float loadedMl, bool overTarget)
        => loadedMl > 0.001f && !overTarget;

    /// The dropper's own hover label: what it holds and how many squeezes remain.
    public static string HoldingLabel(string chem, float loadedMl)
        => loadedMl > 0.001f ? chem + " · " + SqueezesLeft(loadedMl) + "/" + (int)Capacity + " drops"
                             : "Dropper (empty)";

    /// The last squeeze gives whatever remains rather than overdrawing.
    public static float SqueezeCharge(float loadedMl, float perSqueeze = MlPerSqueeze)
        => Mathf.Min(Mathf.Max(0f, loadedMl), perSqueeze);

    /// Squeezes a charge still has in it — what the dropper's own label shows.
    public static int SqueezesLeft(float loadedMl, float perSqueeze = MlPerSqueeze)
        => perSqueeze <= 0f ? 0 : Mathf.FloorToInt((loadedMl + 0.001f) / perSqueeze);

    /// Popup per squeeze: a running COUNT, because the count is the measurement.
    /// ("drop 3" reads as progress; a bare "+1 ml" would hide the thing being taught.)
    public static string SqueezeLabel(string chem, int dropsSoFar)
        => "drop " + dropsSoFar + "  ·  " + chem;

    /// Popup for a draw: how many squeezes the student now has in hand.
    public static string FillLabel(string chem, float chargeMl)
        => chem + "  ·  " + SqueezesLeft(chargeMl) + " drops loaded";

    // ---- charge-drain geometry --------------------------------------------
    // Scaling a capsule shrinks it symmetrically about its pivot, so both ends
    // recede toward the centre. Physically the liquid POOLS AT THE TIP and the
    // empty space grows at the bulb — so the capsule also shifts toward the tip
    // by exactly the half-length it lost, keeping the tip end anchored.

    /// Visible charge length for a loaded fraction. Floors at 20% so a loaded
    /// dropper always shows SOME charge (readability over realism).
    public static float DrainScaleY(float authoredScaleY, float frac01)
        => authoredScaleY * Mathf.Lerp(0.2f, 1f, Mathf.Clamp01(frac01));

    /// Distance the capsule's CENTER moves toward the tip so its tip end stays
    /// fixed: the half-length lost to the drain (meshHalfY = the capsule mesh's
    /// unscaled Y half-extent, 1 for a Unity capsule primitive).
    public static float DrainShift(float meshHalfY, float authoredScaleY, float currentScaleY)
        => meshHalfY * (authoredScaleY - currentScaleY);
}

/// Dropper/pipette verb: touch a liquid reagent's bottle to draw a charge, hold
/// the dropper over a vessel and ACTIVATE (trigger) to release exactly one drop.
/// Deliberately discrete — the squeeze count IS the measurement, so it must be a
/// press, not a proximity brush like the scoop's dip.
///
/// Self-contained like ScoopController: no scene authoring beyond the component
/// (the probe works off renderer bounds; only a HELD dropper transfers, so shelf
/// contact never draws). A hand-placed "DropperTip" child wins for the probe —
/// same convention as the scoop's ScoopAnchor and the pestle's PestleTip, and
/// the same latent bug it avoids (tracking the transform ORIGIN instead of the
/// working end is what made the grind silently never complete).
public class DropperController : MonoBehaviour
{
    [SerializeField] private float probeRadius = 0.05f;
    [SerializeField] private float fillCooldown = 0.35f;
    [SerializeField] private float squeezeCooldown = 0.18f;
    [Tooltip("The dropper TIP is at the NEGATIVE end of the tool's longest axis. Flip if drops fall from the bulb end.")]
    [SerializeField] private bool tipAtPositiveEnd = false;

    private XRGrab _grab;
    private ChemicalData _loaded;
    private float _loadedMl;
    private float _readyAt;
    private int _dropCount;              // squeezes since this charge was drawn
    private LiquidPhysics _lastSource;   // the bottle just drawn from — don't instantly squeeze back into it

    public bool Loaded => _loaded != null && _loadedMl > 0.001f;
    public int SqueezesLeft => DropperMath.SqueezesLeft(_loadedMl);

    void Awake()
    {
        if (_grab == null) Bind(GetComponent<XRGrab>());
        // Hide the hand-fitted charge capsule IMMEDIATELY — it is a fitting aid
        // in the editor and "the actual contents" in play; an empty dropper must
        // never show it. Without this it stays visible from
        // scene load until the first draw calls RefreshFill.
        CacheLiquid();
    }

    /// Edit-mode / builder seam (AddComponent fires no Awake in edit mode).
    public void Bind(XRGrab grab)
    {
        _grab = grab;
        if (_grab != null)
        {
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }
    }

    void OnDestroy()
    {
        if (_grab != null) _grab.activated.RemoveListener(OnActivated);
        if (_fill != null) Destroy(_fill);   // unparented — would otherwise outlive the tool
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        FollowFill();                            // the charge bead rides the tip
        bool held = _grab != null && _grab.isSelected;
        if (!held || Loaded || Time.time < _readyAt) return;

        // Drawing up is a proximity touch (like the scoop's dip) — only the
        // RELEASE needs to be a deliberate press.
        var probe = ProbeCenter();
        foreach (var col in Physics.OverlapSphere(probe, probeRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var lp = col.GetComponentInParent<LiquidPhysics>();
            if (lp == null || lp.currentChemical == null) continue;
            // Task vessels (test tubes etc.) are destinations, never sources — see CanFill.
            bool isTaskVessel = lp.GetComponent<LiquidTaskBinding>() != null;
            if (!DropperMath.CanFill(_loadedMl, lp.currentChemical.state, lp.currentLiquidVolume, isTaskVessel)) continue;

            float charge = DropperMath.FillCharge(lp.currentLiquidVolume);
            var chem = lp.PourOut(charge);
            if (chem == null) continue;
            _loaded = chem; _loadedMl = charge; _dropCount = 0; _lastSource = lp;
            _readyAt = Time.time + fillCooldown;
            RefreshFill();                       // the charge must be SEEN, not just read
            AudioService.TryPlayFirstAt(probe, 0.5f, "pour");
            FloatingText.Show(DropperMath.FillLabel(chem.chemicalName, charge),
                              probe + Vector3.up * 0.05f, new Color(0.7f, 0.9f, 1f), 0.9f);
            return;
        }
    }

    /// Trigger = ONE drop into whatever the tip is over.
    private void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
    {
        if (!Application.isPlaying || Time.time < _readyAt) return;
        var probe = ProbeCenter();
        LiquidPhysics target = null;
        foreach (var col in Physics.OverlapSphere(probe, probeRadius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var lp = col.GetComponentInParent<LiquidPhysics>();
            if (lp == null || lp == _lastSource) continue;
            if (LabReset.RefusesTransfer(lp, _loaded)) continue;   // never contaminate shelf stock
            target = lp; break;
        }
        // A loaded squeeze over nothing WASTES the drop to the floor — that is how
        // the student deliberately empties a dropper.
        if (DropperMath.CanWaste(_loadedMl, target != null))
        {
            float wasted = DropperMath.SqueezeCharge(_loadedMl);
            SpawnDroplet(probe, _loaded);
            _loadedMl -= wasted;
            _readyAt = Time.time + squeezeCooldown;
            AudioService.TryPlayFirstAt(probe, 0.35f, "drip", "pour");
            FloatingText.Show("drop wasted · " + DropperMath.SqueezesLeft(_loadedMl) + " left",
                              probe + Vector3.up * 0.05f, new Color(1f, 0.8f, 0.5f), 0.7f);
            if (_loadedMl <= 0.001f) { _loaded = null; _loadedMl = 0f; _lastSource = null; }
            RefreshFill();
            return;
        }
        if (!DropperMath.CanSqueeze(_loadedMl, target != null, false))
        {
            // Squeezing an empty dropper is a no-op the player should SEE —
            // silence here reads as a broken trigger.
            if (!Loaded)
                FloatingText.Show("dropper is empty", probe + Vector3.up * 0.05f,
                                  new Color(1f, 0.8f, 0.5f), 0.7f);
            return;
        }

        float drop = DropperMath.SqueezeCharge(_loadedMl);
        SpawnDroplet(probe, _loaded);            // a visible bead falls from the tip
        target.AddLiquid(_loaded, drop);
        _loadedMl -= drop;
        _dropCount++;
        _readyAt = Time.time + squeezeCooldown;
        AudioService.TryPlayFirstAt(probe, 0.4f, "drip", "pour");
        FloatingText.Show(DropperMath.SqueezeLabel(_loaded.chemicalName, _dropCount),
                          probe + Vector3.up * 0.05f, new Color(0.6f, 1f, 0.7f), 0.7f);
        if (_loadedMl <= 0.001f) { _loaded = null; _loadedMl = 0f; _lastSource = null; }
        RefreshFill();                           // the bead inside shrinks with each drop
    }

    // ---- charge visuals -------------------------------------------------------
    // Bounds-based fitting does not work here: mesh bounds give a
    // blob bigger than the tool, and a pendant bead at ProbeCenter() fails
    // because the dropper is a SKINNED mesh, and SkinnedMeshRenderer.bounds come from
    // the bind pose, so the "tip" lands nowhere near the visible glass.
    //
    // So the charge uses HAND-FITTED children,
    // exactly like FlameAnchor on the match head and the ghost-tube slots.
    //   • "DropperLiquid" — a capsule scaled by hand INSIDE the stem once, in
    //     the editor. Runtime only toggles it, tints it, and shortens it toward
    //     the bulb as the charge drains. Created by Add Placement Anchors.
    //   • "DropperTip"   — dragged onto the real tip; ProbeCenter() already
    //     prefers it, which also fixes the draw probe + droplet origin.
    // The pendant bead survives ONLY as a fallback for a dropper that has no
    // hand-fitted liquid yet.

    private GameObject _fill;            // fallback bead (no DropperLiquid child)
    private Transform _liquid;           // the hand-fitted interior capsule
    private Vector3 _liquidScale;        // its authored full-charge scale
    private Vector3 _liquidPos;          // its authored full-charge position
    private Vector3 _liquidTipAxis;      // capsule's long axis TOWARD the tip (parent space); zero = unknown
    private float _liquidMeshHalfY = 1f; // unscaled mesh half-extent along the capsule's Y
    private bool _liquidCached;

    private void CacheLiquid()
    {
        if (_liquidCached) return;
        _liquid = transform.Find("DropperLiquid");
        if (_liquid != null)
        {
            _liquidScale = _liquid.localScale;
            _liquidPos = _liquid.localPosition;
            var mf = _liquid.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
                _liquidMeshHalfY = mf.sharedMesh.bounds.extents.y;
            // Which way is the tip? The capsule's own long axis (its local Y),
            // signed toward the hand-placed DropperTip — both are direct children
            // of the tool, so this is all one parent space.
            var tip = transform.Find("DropperTip");
            if (tip != null)
            {
                Vector3 axis = _liquid.localRotation * Vector3.up;
                float sign = Vector3.Dot(axis, tip.localPosition - _liquidPos) >= 0f ? 1f : -1f;
                _liquidTipAxis = axis.normalized * sign;
            }
            if (Application.isPlaying && !Loaded) _liquid.gameObject.SetActive(false);
        }
        _liquidCached = true;
    }

    private void RefreshFill()
    {
        // Label first — it exists whether or not any visual shows.
        var label = GetComponent<ProximityLabel>();
        if (label != null)
            label.SetLabel(DropperMath.HoldingLabel(_loaded != null ? _loaded.chemicalName : "", _loadedMl), 1.4f);

        TintBarrel();
        CacheLiquid();

        if (_liquid != null)
        {
            if (!Loaded) { _liquid.gameObject.SetActive(false); return; }
            _liquid.gameObject.SetActive(true);
            // Drain along the capsule's own length (its local Y): the authored
            // scale IS full charge, and it recedes as squeezes go out. Never
            // fully vanishes while loaded — a sliver of charge stays readable.
            // The capsule also SHIFTS toward the tip by the half-length it lost,
            // so the charge stays pooled AT THE TIP instead of shrinking about
            // its own centre.
            float frac = Mathf.Clamp01(_loadedMl / DropperMath.Capacity);
            var s = _liquidScale;
            s.y = DropperMath.DrainScaleY(_liquidScale.y, frac);
            _liquid.localScale = s;
            if (_liquidTipAxis != Vector3.zero)
                _liquid.localPosition = _liquidPos
                    + _liquidTipAxis * DropperMath.DrainShift(_liquidMeshHalfY, _liquidScale.y, s.y);
            var lr = _liquid.GetComponent<Renderer>();
            if (lr != null)
            {
                var mpb = new MaterialPropertyBlock();
                var lc = _loaded.liquidColor; lc.a = 1f;
                mpb.SetColor(lr.sharedMaterial != null && lr.sharedMaterial.HasProperty("_BaseColor")
                             ? "_BaseColor" : "_Color", lc);
                lr.SetPropertyBlock(mpb);
            }
            if (_fill != null) { Destroy(_fill); _fill = null; }   // no double visual
            return;
        }

        // ---- fallback bead (dropper not yet hand-fitted) ----
        if (!Loaded) { if (_fill != null) Destroy(_fill); _fill = null; return; }
        if (_fill == null)
        {
            _fill = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _fill.name = "DropperFill";
            var col = _fill.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var fr = _fill.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            fr.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                { name = "DropperFill_Runtime" };
            fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        var rend = _fill.GetComponent<Renderer>();
        var c = _loaded.liquidColor; c.a = 1f;
        if (rend.sharedMaterial.HasProperty("_BaseColor")) rend.sharedMaterial.SetColor("_BaseColor", c);
        else rend.sharedMaterial.color = c;
        FollowFill();
    }

    /// Tint the glass barrel itself with the loaded chemical.
    /// Via MaterialPropertyBlock, NEVER the material:
    /// all four droppers (and other glassware) share GlassMat, so touching
    /// sharedMaterial would tint the whole set — and renderer.material would
    /// leak an instance. The MPB blends the chemical colour over the glass base
    /// while keeping its alpha (still reads as glass), and clears to the original
    /// look when the dropper empties.
    private void TintBarrel()
    {
        var r = GetComponent<Renderer>();
        if (r == null || r.sharedMaterial == null) return;
        var mpb = new MaterialPropertyBlock();
        if (!Loaded) { r.SetPropertyBlock(mpb); return; }   // empty block = back to GlassMat

        string prop = r.sharedMaterial.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
        Color glass = r.sharedMaterial.HasProperty(prop) ? r.sharedMaterial.GetColor(prop) : Color.white;
        Color c = Color.Lerp(glass, _loaded.liquidColor, 0.65f);
        c.a = glass.a;                                      // keep the glass's transparency
        mpb.SetColor(prop, c);
        r.SetPropertyBlock(mpb);
    }

    /// Keep the bead pinned to the tip (fixed WORLD size — never parented, so the
    /// tool's import scale can't distort it). Called every frame from Update.
    private void FollowFill()
    {
        if (_fill == null) return;
        float frac = Mathf.Clamp01(_loadedMl / DropperMath.Capacity);
        _fill.transform.position = ProbeCenter();
        _fill.transform.localScale = Vector3.one * Mathf.Lerp(0.007f, 0.013f, frac);
    }

    /// A tiny falling bead of the chemical, gone in a second — purely visual;
    /// the real transfer went through AddLiquid above.
    private static void SpawnDroplet(Vector3 tip, ChemicalData chem)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Droplet";
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);           // never knocks the tube over
        go.transform.position = tip;
        go.transform.localScale = Vector3.one * 0.011f;
        var r = go.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        r.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"));
        var c = chem != null ? chem.liquidColor : Color.cyan; c.a = 1f;
        if (r.sharedMaterial.HasProperty("_BaseColor")) r.sharedMaterial.SetColor("_BaseColor", c);
        else r.sharedMaterial.color = c;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = true;
        Destroy(go, 0.8f);
    }

    /// The dropper TIP — a hand-placed "DropperTip" child wins, else the far end
    /// of the tool's longest axis (never the transform origin: see class note).
    private Vector3 ProbeCenter()
    {
        var anchor = transform.Find("DropperTip");
        if (anchor != null) return anchor.position;
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return transform.position;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Vector3 axis = Matchstick.LongestLocalAxis(transform, b, out float halfLen);
        return b.center + axis * (halfLen * (tipAtPositiveEnd ? 1f : -1f));
    }
}
