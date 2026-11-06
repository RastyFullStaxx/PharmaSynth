using UnityEngine;

/// Pharmee steps aside when the player walks into him (so he never
/// blocks the way the player is trying to go). While
/// the player is inside his personal-space bubble, he drifts laterally out of their
/// path (to whichever side he's already on, so he clears their forward direction),
/// then eases back once they pass. Additive on top of FloatBob's home + bob, so it
/// composes with the follow/hover behaviour instead of fighting it.
public class PharmeeGiveWay : MonoBehaviour
{
    [SerializeField] private FloatBob bob;          // whose offset we drive
    [SerializeField] private Transform body;        // the hovering transform (FloatBob's own)
    [SerializeField] private Transform player;      // XR camera (falls back to Camera.main)

    [Header("Tuning")]
    [Tooltip("Radius (m) around Pharmee that counts as the player bumping into him.")]
    [SerializeField] private float personalSpace = 0.95f;
    [Tooltip("Maximum sideways step (m) when the player is right on top of him.")]
    [SerializeField] private float maxPush = 0.65f;

    public void Bind(FloatBob b, Transform bodyXform, Transform p)
    { bob = b; body = bodyXform != null ? bodyXform : (b != null ? b.transform : null); player = p; }

    /// Pure sidestep vector (world, horizontal): zero outside personal space,
    /// otherwise a lateral push (perpendicular to the player's forward) toward the
    /// side Pharmee is already on, scaled by how far inside the bubble he is.
    public static Vector3 SideStep(Vector3 pharmeePos, Vector3 playerPos, Vector3 playerForward,
                                   float personalSpace, float maxPush)
    {
        Vector3 off = pharmeePos - playerPos; off.y = 0f;
        float d = off.magnitude;
        if (personalSpace <= 0f || d >= personalSpace) return Vector3.zero;

        Vector3 fwd = playerForward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-4f) fwd = off.sqrMagnitude > 1e-4f ? off : Vector3.forward;
        fwd.Normalize();

        Vector3 lateral = Vector3.Cross(Vector3.up, fwd);            // player's right
        float side = Vector3.Dot(off, lateral) >= 0f ? 1f : -1f;     // the side he's already on (default right)
        float intrude = Mathf.Clamp01((personalSpace - d) / personalSpace);
        return lateral * side * (maxPush * intrude);
    }

    private void Start()
    {
        if (bob == null) bob = GetComponentInChildren<FloatBob>();
        if (body == null && bob != null) body = bob.transform;
    }

    private void Update()
    {
        if (bob == null || body == null) return;
        var p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        if (p == null) { bob.SetGiveWayOffset(Vector3.zero); return; }

        Vector3 world = SideStep(body.position, p.position, p.forward, personalSpace, maxPush);
        // FloatBob offsets in its own local space (parent of the hovering transform).
        Transform parent = body.parent;
        Vector3 local = parent != null ? parent.InverseTransformVector(world) : world;
        local.y = 0f;
        bob.SetGiveWayOffset(local);
    }
}
