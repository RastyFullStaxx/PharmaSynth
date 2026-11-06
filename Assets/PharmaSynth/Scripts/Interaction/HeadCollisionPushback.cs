using UnityEngine;

/// Pure pushback resolution (edit-mode testable). Both rules are needed: without
/// them the player can end up under the
/// lab floor, unable to walk).
public static class HeadPushbackMath
{
    public const float SurfaceSkin = 0.02f;   // stop just short of the surface

    /// A sweep that STARTS overlapping reports distance 0 with a zero normal —
    /// that is not a real blocking hit. Honouring it pinned the head in world
    /// space and dragged the rig by the inverse of every head movement, with no
    /// way back out. In a lab the knee end of the sweep overlaps a bench or
    /// cabinet base most of the time, so it fired almost every frame.
    public static bool IsBlockingHit(float hitDistance) => hitDistance > 0f;

    /// Rig correction for a head that swept toward `target` and was stopped at
    /// `nearest`. HORIZONTAL ONLY: vertical placement belongs to the
    /// CharacterController + gravity. A Y term here writes the rig root directly,
    /// so the CC never resolves it — and since real head tracking jitters every
    /// frame, each downward jitter sank the rig further, putting the knee sweep
    /// deeper into the floor and guaranteeing the next hit. Runaway sink.
    public static Vector3 Correction(Vector3 lastValid, Vector3 target, Vector3 dir, float nearest)
    {
        Vector3 allowed = lastValid + dir * Mathf.Max(0f, nearest - SurfaceSkin);
        Vector3 c = allowed - target;
        c.y = 0f;
        return c;
    }
}

/// Stops the player's HEAD from phasing through static geometry, no matter how the
/// camera moved — thumbstick locomotion, the XR Device Simulator's direct HMD
/// translate (which bypasses the CharacterController by design), or physically
/// leaning through a wall. Each frame the head's path is swept as a small sphere;
/// if it would cross static geometry, the whole rig is pulled back so the head
/// stays on the outside. Triggers, the rig's own colliders, and dynamic props
/// (anything with a Rigidbody) never push back.
public class HeadCollisionPushback : MonoBehaviour
{
    [SerializeField] private Transform head;          // the XR camera (falls back to Camera.main)
    [SerializeField] private Transform rig;           // XR Origin root (falls back to this transform)
    [SerializeField] private float headRadius = 0.14f;
    [Tooltip("A jump larger than this is a teleport/scene move — accepted, not blocked.")]
    [SerializeField] private float teleportThreshold = 1.0f;

    private Vector3 _lastValid;
    private bool _has;

    private static readonly RaycastHit[] Hits = new RaycastHit[16];

    public void Bind(Transform headT, Transform rigT) { head = headT; rig = rigT; _has = false; }

    private void LateUpdate()
    {
        var rigT = rig != null ? rig : transform;
        var headT = head;
        if (headT == null)
        {
            var c = Camera.main;
            if (c == null) return;
            headT = head = c.transform;
        }

        Vector3 target = headT.position;
        if (!_has) { _lastValid = target; _has = true; return; }

        Vector3 delta = target - _lastValid;
        float dist = delta.magnitude;
        if (dist < 1e-5f) return;
        if (dist > teleportThreshold)               // scripted teleport / respawn — accept
        {
            _lastValid = target;
            return;
        }

        Vector3 dir = delta / dist;
        // Sweep a head-to-knee CAPSULE, not just the head sphere — a head-only
        // sweep let the player glide THROUGH waist-high tables and chairs
        // (their tops sit below head height).
        Vector3 knee = new Vector3(_lastValid.x, rigT.position.y + 0.35f, _lastValid.z);
        if (knee.y > _lastValid.y - headRadius) knee = _lastValid;   // crouched below knee: degenerate to sphere
        int n = Physics.CapsuleCastNonAlloc(_lastValid, knee, headRadius, dir, Hits, dist, ~0, QueryTriggerInteraction.Ignore);
        float nearest = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = Hits[i];
            if (h.collider == null) continue;
            if (h.rigidbody != null) continue;                          // dynamic props never wall the player
            if (h.collider.transform.IsChildOf(rigT)) continue;         // the rig's own body
            // A sweep that STARTS overlapping reports distance 0 with a zero
            // normal — not a real blocking hit. Honouring it pinned the head in
            // world space (allowed == _lastValid, which then never advanced) and
            // dragged the whole rig by the inverse of every head movement. In a
            // lab the knee end of the capsule overlaps a bench/cabinet base most
            // of the time, so this fired constantly and left no way back out.
            if (!HeadPushbackMath.IsBlockingHit(h.distance)) continue;
            if (h.distance < nearest) nearest = h.distance;
        }

        if (nearest < float.MaxValue)
        {
            // Head tried to cross static geometry: allow travel up to the surface,
            // then shift the RIG back by the overshoot so the head stays outside.
            Vector3 correction = HeadPushbackMath.Correction(_lastValid, target, dir, nearest);
            rigT.position += correction;
            _lastValid = target + correction;   // where the head actually ended up
        }
        else
        {
            _lastValid = target;
        }
    }
}
