using UnityEngine;

/// Rotates a world-space label or canvas to face the player's camera every frame,
/// so text stays readable from any side (never mirrored / seen from behind).
/// Y-axis mode keeps signs upright; full mode also pitches toward the viewer.
public class FaceCamera : MonoBehaviour
{
    [Tooltip("Rotate around Y only (stay upright) — right for signs, panels and labels.")]
    public bool yAxisOnly = true;

    [Tooltip("Extra yaw in degrees if an asset's readable side is not its -Z face.")]
    public float yawOffset = 0f;

    [Tooltip("Characters (Pharmee): point +Z TOWARD the viewer so the face looks at them. " +
             "Leave off for UI text/panels, whose +Z should point away to read correctly.")]
    public bool faceTowardCamera = false;

    [Tooltip("Characters: yaw the AUTHORED pose around world-up instead of rebuilding rotation, " +
             "so the model keeps its upright orientation (never lies on its back). Use for Pharmee.")]
    public bool preserveInitialTilt = false;

    /// While set (a cinematic cutscene), everything faces this viewer instead of the
    /// player's head — Pharmee looks into the cutscene camera, labels stay readable.
    public static Transform Override;

    private Transform _cam;
    private Quaternion _initial;
    private bool _haveInitial;

    private void Awake()
    {
        _initial = transform.rotation;
        _haveInitial = true;
    }

    private void LateUpdate()
    {
        if (_cam == null)
        {
            var c = Camera.main;
            if (c == null) return;      // no camera yet (e.g. edit mode)
            _cam = c.transform;
        }
        var view = Override != null ? Override : _cam;

        if (preserveInitialTilt)
        {
            if (!_haveInitial) { _initial = transform.rotation; _haveInitial = true; }
            // Spin the authored pose around world-up only → stays upright, just turns.
            Vector3 f0 = _initial * Vector3.forward; f0.y = 0f;
            // Models imported with a 90° axis fix (Pharmee's glb) have a VERTICAL
            // authored forward — flattening it degenerates to zero and the yaw
            // never applied (he stared at one wall forever). Fall back to the
            // authored up-axis, which such imports point horizontally.
            if (f0.sqrMagnitude < 1e-4f) { f0 = _initial * Vector3.up; f0.y = 0f; }
            Vector3 want = faceTowardCamera ? (view.position - transform.position)
                                            : (transform.position - view.position);
            want.y = 0f;
            if (f0.sqrMagnitude < 1e-4f || want.sqrMagnitude < 1e-4f) return;
            float delta = Vector3.SignedAngle(f0, want, Vector3.up) + yawOffset;
            transform.rotation = Quaternion.AngleAxis(delta, Vector3.up) * _initial;
            return;
        }

        // UI text/panels read when +Z points AWAY from the viewer; a character faces
        // the viewer when +Z points TOWARD them.
        Vector3 dir = faceTowardCamera
            ? view.position - transform.position
            : transform.position - view.position;
        if (yAxisOnly) dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up)
                             * Quaternion.Euler(0f, yawOffset, 0f);
    }
}
