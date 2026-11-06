using UnityEngine;

/// Driver for the HUD canvas. Two modes:
///  ScreenLocked (default, storyboard style) — the canvas is rigidly glued to the
///    camera every frame (full rotation), so pills sit at fixed screen corners.
///  LazyFollow — comfort mode: hovers ahead and only re-centres outside a deadzone
///    (kept as an option; some players find head-locked UI fatiguing on-device).
public class HudRigController : MonoBehaviour
{
    public enum Mode { ScreenLocked, LazyFollow }

    [SerializeField] private Mode mode = Mode.ScreenLocked;
    [SerializeField] private Camera cameraOverride;              // falls back to Camera.main
    [SerializeField] private float lockedDistance = 1.1f;        // metres in front of the lens
    // VR comfort: size the head-locked canvas to a FIXED angular field instead of
    // the camera frustum. cam.fieldOfView/aspect are the wrong values for stereo
    // XR, which shoved the corner widgets (progress/buttons) into the far
    // periphery. These half-angles place the canvas edges at predictable angles
    // from the gaze so the side-anchored widgets stay in view. Tunable.
    [SerializeField, Range(8f, 45f)] private float halfAngleH = 30f;   // horizontal half-extent (deg)
    [SerializeField, Range(8f, 45f)] private float halfAngleV = 20f;   // vertical half-extent (deg)
    // Drops the head-locked HUD below the gaze line so the top-row widgets
    // (timer/progress/buttons) sit near eye level instead of above the head in VR.
    // Metres in the camera's local frame; negative = down. Tunable in the inspector.
    [SerializeField] private float verticalOffset = -0.28f;
    [SerializeField] private HudFollowSolver.Params follow = HudFollowSolver.Params.Default;

    private HudFollowSolver.State _state;
    private bool _snapped;

    public Mode CurrentMode { get => mode; set => mode = value; }
    public HudFollowSolver.Params Follow { get => follow; set => follow = value; }

    private Camera Cam => cameraOverride != null ? cameraOverride : Camera.main;

    /// Place the HUD directly on its anchor (scene start, after teleports).
    public void SnapToCamera()
    {
        var cam = Cam;
        if (cam == null) return;
        if (mode == Mode.ScreenLocked)
        {
            ApplyLocked(cam);
        }
        else
        {
            var t = cam.transform;
            _state = HudFollowSolver.Snapped(t.position, t.eulerAngles.y, in follow);
            ApplyFollow();
        }
        _snapped = true;
    }

    private void LateUpdate()
    {
        var cam = Cam;
        if (cam == null) return;
        if (mode == Mode.ScreenLocked)
        {
            ApplyLocked(cam);
            return;
        }
        if (!_snapped) { SnapToCamera(); return; }
        var head = cam.transform;
        HudFollowSolver.Step(ref _state, head.position, head.eulerAngles.y, in follow, Time.deltaTime);
        ApplyFollow();
    }

    private void ApplyLocked(Camera cam)
    {
        var camT = cam.transform;
        transform.SetPositionAndRotation(
            camT.position + camT.rotation * new Vector3(0f, verticalOffset, lockedDistance),
            camT.rotation);
        // Size the canvas to a FIXED angular field (not the stereo-wrong camera
        // frustum), so corner-anchored children sit at predictable, in-view angles.
        var rt = transform as RectTransform;
        if (rt == null) return;
        float h = 2f * lockedDistance * Mathf.Tan(Mathf.Clamp(halfAngleV, 8f, 45f) * Mathf.Deg2Rad);
        float w = 2f * lockedDistance * Mathf.Tan(Mathf.Clamp(halfAngleH, 8f, 45f) * Mathf.Deg2Rad);
        float s = Mathf.Max(transform.localScale.x, 1e-5f);
        rt.sizeDelta = new Vector2(w / s, h / s);
    }

    private void ApplyFollow()
    {
        transform.SetPositionAndRotation(_state.pos, Quaternion.Euler(0f, _state.yawDeg, 0f));
    }
}
