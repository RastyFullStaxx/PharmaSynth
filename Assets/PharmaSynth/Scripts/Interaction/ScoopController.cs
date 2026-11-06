using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for scooping solids (solid reagents are
/// scooped: each scoop adds a fixed amount, and the heap visual grows and so
/// does the scale reading). Kept plain so the suite pins the policy.
public static class ScoopMath
{
    /// Fixed charge one dip transfers, in grams (1 g/ml proxy — WeighMath's
    /// convention, so the balance display and VesselStatus read consistently).
    public const float GramsPerScoop = 2f;

    /// The manuscript's PORCELAIN SPATULA charge. Exp 2 weighs 0.1 g of salicylic
    /// acid and 0.5 g of aspirin — both SMALLER than one 2 g scoopful, so the
    /// scoop simply cannot express them. A spatula dip is 0.1 g, making those
    /// 1 and 5 dips: the same "the number in the instruction is the action count"
    /// contract the dropper uses for drops.
    public const float GramsPerSpatula = 0.1f;

    /// A dip picks up ONLY from a solid/powder store with something left, and
    /// only while the scoop is empty (no double-dipping a full scoop).
    public static bool CanPickUp(bool carrying, PhysicalState state, float availableMl)
        => !carrying && (state == PhysicalState.Solid || state == PhysicalState.Powder)
           && availableMl > 0.01f;

    /// The last scoopful takes whatever remains.
    public static float ScoopCharge(float availableMl, float perScoop = GramsPerScoop)
        => Mathf.Min(Mathf.Max(0f, availableMl), perScoop);

    /// A carried charge deposits into any OTHER container (empty or not — the
    /// vessel's own capacity/reaction rules take over via AddLiquid).
    public static bool CanDeposit(bool carrying, bool sameContainer)
        => carrying && !sameContainer;

    /// Brushing a loaded blade past a vessel tips it in only where that can't be a
    /// mistake: an unbound vessel, or one whose step expects this chemical now. A bound
    /// vessel that does NOT expect it needs a deliberate trigger pull — an accidental
    /// brush would otherwise record a wrong-reagent mistake (-4 grade) for a hand that drifted.
    public static bool DepositsOnTouch(bool vesselIsBound, bool expectsChemical)
        => !vesselIsBound || expectsChemical;

    /// Popup for a deposit: running total so repeated scoops read as progress.
    public static string DepositLabel(string chem, float addedG, float totalG)
        => "+" + addedG.ToString("0.#") + " g " + chem + "  (" + totalG.ToString("0.#") + " g total)";
}

/// Scoopula/spatula verb: dip into a solid reagent's jar to pick up a fixed
/// charge (a visible tinted heap rides the blade), touch a receiving vessel to
/// deposit it — per-scoop FloatingText totals, and the balance/VesselStatus
/// update through the normal contents path. Self-contained: no scene authoring
/// needed beyond adding the component (the proximity probe works off renderer
/// bounds; only a HELD scoop transfers, so shelf contact never scoops).
public class ScoopController : MonoBehaviour
{
    [SerializeField] private float probeRadius = 0.055f;
    [SerializeField] private float actionCooldown = 0.5f;
    [Tooltip("Grams per dip. ScoopMath.GramsPerScoop (2 g) for the scoopula; ScoopMath.GramsPerSpatula (0.1 g) for the porcelain spatula, which is the only tool that can express Exp 2's 0.1 g / 0.5 g weighings.")]
    [SerializeField] private float gramsPerDip = ScoopMath.GramsPerScoop;
    [Tooltip("The scooping BLADE/BOWL is at the NEGATIVE end of the tool's longest axis. Flip if the heap rides the handle.")]
    [SerializeField] private bool bladeAtPositiveEnd = false;
    [Tooltip("Shape of the carried heap on the blade/bowl (local scale). The SCOOPULA gets a rounded bowl-pile (default); the porcelain SPATULA a flat elongated smear (set by Apply Verb Data). The two tools shouldn't look the same.")]
    [SerializeField] private Vector3 heapScale = new Vector3(0.026f, 0.018f, 0.026f);   // rounded bowl-pile (scoopula)

    private XRGrab _grab;
    private ChemicalData _carrying;
    private float _carryingG;
    private float _readyAt;
    private GameObject _heap;
    private LiquidPhysics _lastSource;   // the jar we just dipped — don't instantly re-deposit into it

    public bool Carrying => _carrying != null;

    void Awake() { if (_grab == null) Bind(GetComponent<XRGrab>()); }

    /// Edit-mode / builder seam.
    public void Bind(XRGrab grab)
    {
        if (_grab != null && Application.isPlaying) _grab.activated.RemoveListener(OnTrigger);
        _grab = grab;
        if (_grab != null && Application.isPlaying) _grab.activated.AddListener(OnTrigger);
    }

    /// Trigger = tip the blade into the nearest vessel, whatever it expects (deliberate).
    private void OnTrigger(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
    {
        if (!Carrying || Time.time < _readyAt) return;
        foreach (var col in Physics.OverlapSphere(ProbeCenter(), probeRadius * 1.5f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var lp = col.GetComponentInParent<LiquidPhysics>();
            if (lp != null && Deposit(lp)) return;
        }
    }

    private float _nextTipHint;

    /// Builder seam for the finer porcelain-spatula charge (ScoopMath.GramsPerSpatula).
    public void SetGramsPerDip(float grams) { if (grams > 0f) gramsPerDip = grams; }

    /// Read seam (suite): which charge this tool actually dips.
    public float GramsPerDip => gramsPerDip;

    /// Builder seam: the porcelain spatula's flat elongated smear vs the scoopula's
    /// rounded bowl-pile. Read seam pins the two tools differ.
    public void SetHeapScale(Vector3 s) { if (s.sqrMagnitude > 1e-8f) heapScale = s; }
    public Vector3 HeapScale => heapScale;

    void Update()
    {
        if (!Application.isPlaying) return;
        bool held = _grab != null && _grab.isSelected;
        if (!held || Time.time < _readyAt) return;

        // Forgiving probe: anything near the blade counts as touched.
        var probe = ProbeCenter();
        var cols = Physics.OverlapSphere(probe, probeRadius, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in cols)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var lp = col.GetComponentInParent<LiquidPhysics>();
            if (lp == null) continue;
            if (!Carrying) { if (Dip(lp)) return; continue; }
            var binding = lp.GetComponentInParent<LiquidTaskBinding>();
            if (!ScoopMath.DepositsOnTouch(binding != null, binding != null && binding.IsExpectedNow(_carrying)))
            {
                if (Time.time >= _nextTipHint && lp != _lastSource)
                {
                    _nextTipHint = Time.time + 4f;
                    FloatingText.Show("Not this one — pull the trigger to tip it in anyway", probe + Vector3.up * 0.06f,
                                      new Color(1f, 0.85f, 0.55f), 1.1f);
                }
                continue;
            }
            if (Deposit(lp)) return;
        }
        // Once the loaded scoop has LEFT its source jar, forget it — so a later
        // deliberate return to the same jar deposits back instead of being
        // mistaken for the original dip.
        if (Carrying && _lastSource != null)
        {
            bool stillTouching = false;
            foreach (var col in cols)
                if (col != null && col.GetComponentInParent<LiquidPhysics>() == _lastSource) stillTouching = true;
            if (!stillTouching) _lastSource = null;
        }
    }

    /// One DIP into a solid store: the charge leaves the jar, the tinted heap rides the
    /// blade. Public so the play-mode visual sweep can perform the REAL verb
    /// instead of teleporting grams; Update() calls it on blade contact. False when
    /// nothing can be picked up (empty jar, a liquid, already carrying).
    public bool Dip(LiquidPhysics lp)
    {
        if (lp == null || Carrying || lp.currentChemical == null
            || !ScoopMath.CanPickUp(false, lp.currentChemical.state, lp.currentLiquidVolume)) return false;
        float charge = ScoopMath.ScoopCharge(lp.currentLiquidVolume, gramsPerDip);
        var chem = lp.PourOut(charge);
        if (chem == null) return false;
        _carrying = chem; _carryingG = charge; _lastSource = lp;
        _readyAt = Time.time + actionCooldown;
        ShowHeap(chem);
        RefreshPowder(lp);                                  // source mound shrinks
        var probe = ProbeCenter();
        // GRANULAR, never liquid: a dry blade-into-powder
        // dig. No "pour" fallback — silence beats the wrong material's sound.
        AudioService.TryPlayFirstAt(probe, 0.55f, "scoop");
        FloatingText.Show("+" + charge.ToString("0.#") + " g " + chem.chemicalName,
                          probe + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.6f), 0.8f);
        return true;
    }

    /// Tip the carried charge into a vessel through the normal contents path
    /// (AddLiquid — reactions, bindings and the mound all follow). False when not
    /// carrying or when the "vessel" is the jar it just came from.
    public bool Deposit(LiquidPhysics lp)
    {
        if (lp == null || !ScoopMath.CanDeposit(Carrying, lp == _lastSource)) return false;
        if (LabReset.RefusesTransfer(lp, _carrying)) return false;   // never contaminate shelf stock
        var deposited = _carrying; float grams = _carryingG;
        lp.AddLiquid(_carrying, _carryingG);
        RefreshPowder(lp);                                      // receiver mound grows
        var probe = ProbeCenter();
        // Solid tipping out of the scoop — a granular patter, not a liquid pour.
        AudioService.TryPlayFirstAt(probe, 0.85f, "powder-pour", "scoop");
        FloatingText.Show(ScoopMath.DepositLabel(deposited.chemicalName, grams, lp.currentLiquidVolume),
                          probe + Vector3.up * 0.05f, new Color(0.6f, 1f, 0.7f), 0.8f);
        _carrying = null; _carryingG = 0f; _lastSource = null;
        _readyAt = Time.time + actionCooldown;
        HideHeap();
        return true;
    }

    /// Where the blade is right now (world) — lets a simulator hold it over a jar.
    public Vector3 BladeTip => ProbeCenter();

    /// The scooping BLADE tip — the far end of the tool's longest axis.
    /// Both the pick-up probe and the carried heap live on the blade, not at the
    /// whole-tool centre, or the powder rides the middle of the handle and the
    /// bowl never seems to touch the jar.
    private Vector3 ProbeCenter()
    {
        // Hand-placed "ScoopAnchor" child wins (drag it onto the bowl/blade)
        // — otherwise use the far end of the tool's longest axis.
        var anchor = transform.Find("ScoopAnchor");
        if (anchor != null) return anchor.position;
        // SOLID meshes only — the carried ScoopHeap is a child, and including it
        // walked the probe off the blade a little further with every dip.
        var b = ExperimentSceneBuilder.SolidWorldBounds(gameObject);
        Vector3 axis = Matchstick.LongestLocalAxis(transform, b, out float halfLen);
        return b.center + axis * (halfLen * (bladeAtPositiveEnd ? 1f : -1f));
    }

    /// Small tinted mound riding the blade while a charge is carried.
    private void ShowHeap(ChemicalData chem)
    {
        if (_heap == null)
        {
            _heap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _heap.name = "ScoopHeap";
            var hc = _heap.GetComponent<Collider>();
            if (hc != null) Destroy(hc);
            _heap.transform.SetParent(transform, false);
            _heap.transform.localScale = heapScale;   // per-tool: rounded bowl-pile vs flat smear
            var r = _heap.GetComponent<Renderer>();
            r.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "ScoopHeap_Runtime" };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        _heap.transform.position = ProbeCenter() + Vector3.up * 0.008f;
        var rend = _heap.GetComponent<Renderer>();
        if (rend != null && rend.sharedMaterial != null)
        {
            var c = chem != null ? chem.liquidColor : Color.gray; c.a = 1f;
            if (rend.sharedMaterial.HasProperty("_BaseColor")) rend.sharedMaterial.SetColor("_BaseColor", c);
            else rend.sharedMaterial.color = c;
        }
        _heap.SetActive(true);
    }

    private void HideHeap() { if (_heap != null) _heap.SetActive(false); }

    /// Keep a solid/powder container's mound in sync with its contents so it grows
    /// as scoops go in and shrinks as they come out.
    private void RefreshPowder(LiquidPhysics lp)
    {
        if (lp == null) return;
        var c = lp.currentChemical;
        // Scooped DRY: LiquidPhysics clears its contents, so hide the mound from
        // what it last held instead of leaving the old heap sitting in an empty jar.
        if (c == null)
        {
            if (lp.LastChemical != null) ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, lp.LastChemical, 0f);
            return;
        }
        if (c.state != PhysicalState.Solid && c.state != PhysicalState.Powder) return;
        ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, c, MoundFill(lp.currentLiquidVolume));
    }

    /// Pure (suite): how full the mound reads for `grams` in the vessel. Square-root
    /// so the FIRST dips show the biggest visible jump — a linear grams/20 with a
    /// 0.28 floor makes scoops 1-5 look identical.
    /// 0 g stays 0 so an emptied jar hides its heap.
    public static float MoundFill(float grams, float fullAtG = 20f)
    {
        if (grams <= 0.001f) return 0f;
        return Mathf.Max(0.15f, Mathf.Clamp01(Mathf.Sqrt(grams / Mathf.Max(0.01f, fullAtG))));
    }
}
