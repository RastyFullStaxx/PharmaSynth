using UnityEngine;

/// A tiny draggable marker positioned by hand in the editor to tell a verb
/// exactly WHERE something happens on an imported mesh — the flame on a match
/// head, the flame on a burner mouth, the bowl of a scoopula. Bounds-based
/// guessing can't know a model's axis convention,
/// so the code reads this anchor's position and
/// falls back to a heuristic only when no anchor is present.
///
/// Convention: a child named "FlameAnchor" (Matchstick / BurnerController) or
/// "ScoopAnchor" (ScoopController). Drag it in the Scene view, then Lock My
/// Layout to bake it. The gizmo just makes it easy to see/select.
public class PlacementAnchor : MonoBehaviour
{
    public Color gizmoColor = new Color(1f, 0.5f, 0.1f, 0.9f);
    public float gizmoRadius = 0.006f;

    [Tooltip("ONLY for anchors whose SCALE sets a size (PowderAnchor). Draws a wire preview of that size. " +
             "Position-only anchors (FlameAnchor/ScoopAnchor/BowlAnchor) must leave this OFF — their scale is 1, " +
             "which would draw a useless 1-metre sphere over the whole bench.")]
    public bool previewsScale = false;

    [Tooltip("Rack SLOT anchors (Slot_0…): draws a GHOST TEST TUBE at real size (0.13 m) along the " +
             "anchor's +Y, so the seated tube can be eyeballed while dragging — a bare dot can't show " +
             "whether a tube will sit in the hole, lean, or float.")]
    public bool previewsTube = false;

    /// Real test-tube height (RealSizes "TestTube"). Kept here so the ghost matches
    /// what actually gets seated.
    public const float TubeHeight = 0.13f;
    public const float TubeRadius = 0.008f;

    void OnDrawGizmos()
    {
        // A dot marks the spot.
        Gizmos.color = gizmoColor;
        Gizmos.DrawSphere(transform.position, gizmoRadius);

        if (previewsTube)
        {
            // Ghost tube: a wire box the size of a real tube, standing on the anchor
            // along its +Y — drag/rotate the anchor until this sits in the hole.
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.75f);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0f, TubeHeight * 0.5f, 0f),
                                new Vector3(TubeRadius * 2f, TubeHeight, TubeRadius * 2f));
            // Rim ring = the tube's mouth, the part that must clear the rack.
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.4f);
            Gizmos.DrawWireSphere(new Vector3(0f, TubeHeight, 0f), TubeRadius);
            Gizmos.matrix = Matrix4x4.identity;
            return;
        }

        if (!previewsScale)
        {
            // Position-only anchor → a small fixed halo, nothing scale-dependent.
            Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.35f);
            Gizmos.DrawWireSphere(transform.position, gizmoRadius * 2.2f);
            return;
        }
        // Size-setting anchor → a wire ellipsoid matching the REAL extents it will
        // give the powder, so it can be eyeballed while scaling.
        Gizmos.color = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 0.5f);
        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
        Gizmos.DrawWireSphere(Vector3.zero, 0.5f);   // unit sphere → diameter == world scale
        Gizmos.matrix = Matrix4x4.identity;
    }
}
