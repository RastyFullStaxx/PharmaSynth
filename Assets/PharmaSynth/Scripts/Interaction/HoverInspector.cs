using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using SelectInteractor = UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;

/// Points-at-it inspector: each frame it casts from the pointer
/// (right-hand ray, falling back to gaze) and, if it lands on a known reagent bottle,
/// a piece of apparatus or an NPC, shows a smooth info card (HoverInfoPanel) naming
/// it and explaining what it is / how to use it. A short linger stops the card from
/// flickering as the ray grazes edges. Resolution is data-driven (LabInfoDatabase),
/// so no per-object authoring is needed.
public class HoverInspector : MonoBehaviour
{
    [SerializeField] private Transform aimSource;      // primary controller / ray origin
    [Tooltip("The other controller's ray origin — used when the primary hand is full.")]
    [SerializeField] private Transform aimSourceAlt;
    [SerializeField] private Transform head;           // HMD camera (billboard + fallback ray)
    [SerializeField] private HoverInfoPanel panel;
    [SerializeField] private float maxDistance = 4.5f;
    [SerializeField] private LayerMask mask = ~0;      // set by the builder (excludes UI/avatar)
    [SerializeField] private float lingerSeconds = 0.18f;

    private float _lostTimer;

    public void Bind(Transform aim, Transform headT, HoverInfoPanel p, LayerMask m)
    { aimSource = aim; head = headT; panel = p; mask = m; }

    /// Second hand seam, so a rig with two controllers can point with either.
    public void BindAltAim(Transform alt) { aimSourceAlt = alt; }

    /// ⭐ POINT WITH WHICHEVER HAND IS FREE: the other hand's
    /// pointer still triggers the hover text while one hand is
    /// holding something.
    ///
    /// One hand holding something is the normal
    /// working posture in this lab: a tube in one hand, pointing at a shelf with the other.
    /// The card is still suppressed for the item being
    /// carried, per hit, by IsHeld(hit.collider). Both hands
    /// full still means no card: there is nothing the player could pick up next anyway.
    private Transform Source()
    {
        if (Usable(aimSource) && !HandBusy(aimSource)) return aimSource;
        if (Usable(aimSourceAlt) && !HandBusy(aimSourceAlt)) return aimSourceAlt;
        if (Usable(aimSource) || Usable(aimSourceAlt)) return null;   // hands full
        if (head != null) return head;
        var c = Camera.main; if (c != null) head = c.transform;
        return head;
    }

    private static bool Usable(Transform t) => t != null && t.gameObject.activeInHierarchy;

    /// Is the hand that owns this ray holding something? Walks up to the nearest ancestor
    /// that actually carries a select interactor — that object IS the hand — so the other
    /// controller's selection can never be mistaken for this one's.
    private static bool HandBusy(Transform aim)
    {
        for (var t = aim; t != null; t = t.parent)
        {
            bool isHand = false;
            foreach (var it in t.GetComponents<SelectInteractor>())
            {
                if (it == null || it is XRSocket) continue;   // sockets always "hold" — ignore
                isHand = true;
                if (it.hasSelection) return true;
            }
            if (isHand) return false;
        }
        return false;
    }

    private void Update()
    {
        if (panel == null) return;
        var src = Source();
        if (src == null) return;

        if (Physics.Raycast(src.position, src.forward, out var hit, maxDistance, mask, QueryTriggerInteraction.Ignore)
            && !IsHeld(hit.collider))   // don't card an item that's already in hand — it blocks using it
        {
            var entry = Resolve(hit.collider, out _);
            if (entry != null)
            {
                // Anchor at the exact surface point the ray struck (nudged up a touch),
                // so the card can be placed just IN FRONT of it — never buried inside a
                // close, wide target like Pharmee's body.
                panel.Show(entry, hit.point + Vector3.up * 0.05f);
                _lostTimer = 0f;
                return;
            }
        }

        // Nothing informative under the pointer — hold briefly, then fade out.
        _lostTimer += Time.unscaledDeltaTime;
        if (_lostTimer >= lingerSeconds) panel.Hide();
    }

    /// Map a hit collider to an info entry (+ a world anchor near its top).
    public static LabInfoEntry ResolveFor(Collider col, out Vector3 anchor)
    {
        // Anchor a touch above the object's centre (NOT its top — that shoved the card
        // up behind tall targets like Pharmee, where his body occluded it).
        anchor = col != null ? col.bounds.center + Vector3.up * (col.bounds.extents.y * 0.35f) : Vector3.zero;
        if (col == null) return null;

        // NPCs first (their capsule colliders would otherwise fall through to name-match).
        if (col.GetComponentInParent<PharmeeBrain>() != null || col.GetComponentInParent<PharmeeGatekeeper>() != null)
            return LabInfoDatabase.Person(true);
        if (col.GetComponentInParent<ProctorRoamer>() != null || col.GetComponentInParent<ExaminerNPC>() != null)
            return LabInfoDatabase.Person(false);

        // Reagent bottle / filled vessel — identify by the liquid it holds,
        // with a LIVE contents line appended (hover shows real state).
        var lp = col.GetComponentInParent<LiquidPhysics>();
        if (lp != null && lp.currentChemical != null)
            return WithLiveLine(LabInfoDatabase.Reagent(lp.currentChemical.chemicalName), lp);

        // Apparatus — match by display name / item id / object name.
        var li = col.GetComponentInParent<LabItem>();
        string cand = li != null
            ? (!string.IsNullOrEmpty(li.displayName) ? li.displayName : li.itemId)
            : col.transform.name;
        var eq = LabInfoDatabase.Equipment(cand);
        if (eq == null)
            eq = LabInfoDatabase.Equipment(col.transform.root.name);   // e.g. "Prop_Beaker_100mL"
        // An empty vessel still reports its live state ("Now: empty").
        if (eq != null && lp != null) return WithLiveLine(eq, lp);
        return eq;
    }

    /// Clone an entry with the vessel's live contents appended to the body.
    public static LabInfoEntry WithLiveLine(LabInfoEntry e, LiquidPhysics lp)
    {
        if (e == null || lp == null) return e;
        string line = VesselStatusMath.HoverLine(
            lp.currentChemical != null ? lp.currentChemical.chemicalName : null,
            lp.currentLiquidVolume + lp.currentPptVolume,
            lp.Ledger.Summary(3), lp.Ledger.Count);
        return new LabInfoEntry(e.Title, e.Category, e.Body + "\n\n" + GlyphSafe.Sanitize(line));
    }

    private LabInfoEntry Resolve(Collider col, out Vector3 anchor) => ResolveFor(col, out anchor);

    /// True when the collider belongs to a grabbable that a hand is currently
    /// holding — the card is suppressed for it so it doesn't sit over the item
    /// you're trying to use.
    public static bool IsHeld(Collider col)
    {
        if (col == null) return false;
        var grab = col.GetComponentInParent<XRGrab>();
        return grab != null && grab.isSelected;
    }
}
