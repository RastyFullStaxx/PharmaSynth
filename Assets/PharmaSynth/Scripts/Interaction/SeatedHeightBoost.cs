using UnityEngine;

/// Pure fixed-eye-height math (edit-mode testable). Design:
/// per-scene FIXED eye height, NOT relative to the player's real height — the
/// Quest/Link runtime flip-flops between Floor and Device origin spaces across
/// sessions, so any "relative" scheme produces floor-spawns or roof-spawns.
/// Measure where the headset says the head is ONCE at load (only from a VALID,
/// SETTLED pose), then offset the rig so the eye line lands exactly on the
/// scene's authored height. Offset may be NEGATIVE (pulls a too-high pose back
/// down). Gravity (move providers, m_UseGravity on) keeps the rig grounded.
public static class HeightCalibration
{
    public const float MaxAdjust = 1.5f;      // sanity clamp, both directions
    public const float TallTolerance = 0.15f; // headroom before the tall-drift pulldown engages

    /// Camera-offset Y that puts the tracked head exactly on targetEye.
    public static float FixedOffset(float targetEye, float trackedHeadY)
        => Mathf.Clamp(targetEye - trackedHeadY, -MaxAdjust, MaxAdjust);

    public const float ShortTolerance = 0.45f;   // deep bench-leans stay uncorrected

    /// Signed drift beyond the tolerated band (one-sided
    /// correction can leave a player STUCK at eye 0.96 after calibrating with the
    /// headset held high — desk testing moves the headset constantly, so both
    /// directions must correct). Positive = too tall (small 0.15 allowance);
    /// negative = stuck short (generous 0.45 allowance so crouching/leaning over
    /// a bench is never touched).
    public static float TallExcess(float eyeNow, float target)
    {
        if (eyeNow > target + TallTolerance) return eyeNow - (target + TallTolerance);
        if (eyeNow < target - ShortTolerance) return eyeNow - (target - ShortTolerance);
        return 0f;
    }

    /// A pose is measurable only when it is REAL: untracked HMDs report exactly
    /// (0,0,0), and the moment tracking kicks in the value jumps — so require a
    /// non-zero pose whose Y barely moved since last frame.
    public static bool PoseValid(Vector3 headLocalPos, float lastY)
        => headLocalPos.sqrMagnitude > 0.0004f && Mathf.Abs(headLocalPos.y - lastY) < 0.01f;
}

/// Thin driver on the XR Origin (class name kept so existing scene wiring stays
/// valid — behaviour is FIXED height, no seated boost / auto-levitate).
/// Counting UNTRACKED (0,0,0) poses during the load
/// fade as settled measures y≈0, applies the max +1.5 offset, and the real head height
/// then stacks on top (eye ≈ 2.5 m; the capsule balloons into the lab
/// ceiling and depenetration shoves the rig onto the roof). So:
///  - calibration only accepts a valid, settled pose (see PoseValid);
///  - the offset is held at 0 until calibrated (a stale Device-mode 1.36 can
///    never balloon the capsule during load);
///  - the roof-recovery guard runs CONTINUOUSLY, not once.
/// Recalibrates only on scene load / explicit Recalibrate() — never mid-play.
/// Per-scene targets set by Tools ▸ PharmaSynth ▸ Wire Spawn Height.
public class SeatedHeightBoost : MonoBehaviour
{
    const int SettleFrames = 45;   // ~0.5 s of valid, stable tracking
    const float RoofY = 0.3f;      // rig ROOT above this = floating/shoved (floors are y≈0, lip 0.19)
    const float CellarY = -0.3f;   // rig ROOT below this = sunk through the floor (single-level lab)
    const float DebugEvery = 3f;   // [HeightDebug] telemetry cadence (seconds)

    [SerializeField] private float targetEyeHeight = 1.65f;   // authored per scene
    [SerializeField] private Transform cameraTransform;        // rig Main Camera
    [SerializeField] private Transform offsetTransform;        // Camera Offset object

    private float _applied;
    private float _lastY;
    private int _stable;
    private bool _done;
    /// Has a real tracked pose EVER arrived? Separates "no headset" (place the eye at the
    /// authored height) from "tracking dropped" (hold the last offset).
    private bool _everTracked;
    private float _nextDebug;

    public float TargetEyeHeight { get { return targetEyeHeight; } }

    /// True once a valid, settled pose has been measured and the eye-height offset
    /// is locked in (capsule correctly sized). Consumers that move the rig on load
    /// (e.g. the entrance teleport) must wait for this — teleporting while the
    /// capsule is still un-sized lets depenetration shove the rig onto the roof.
    public bool Calibrated => _done;

    /// The fixed-height offset this component wants on the Camera Offset's local Y
    /// this frame (target − measured head). Exposed so a sibling that also writes
    /// the Camera Offset (e.g. WalkBob's head-bob) can ride ON TOP of the fixed
    /// height instead of overwriting it — WalkBob's LateUpdate otherwise clobbers
    /// this Update-write every frame, letting real head height leak through 1:1.
    public float AppliedOffset => _applied;

    public void Bind(Transform cam, Transform offset) { cameraTransform = cam; offsetTransform = offset; }
    public void SetTarget(float eyeHeight) { targetEyeHeight = eyeHeight; }

    /// Fresh measurement (scene load does this automatically via OnEnable).
    public void Recalibrate() { _done = false; _stable = 0; _applied = 0f; _everTracked = false; }

    private void OnEnable() { Recalibrate(); }

    private void Update()
    {
        if (!Application.isPlaying || cameraTransform == null || offsetTransform == null) return;

        // Continuous ground guard — BOTH directions. Depenetration can shove the
        // rig up; anything that writes the rig root's Y directly can sink it under
        // the floor, where the CharacterController is fully inside the floor
        // collider and Move() can no longer resolve it (= stuck, no way back).
        // The guard must work both ways; up-only leaves a sink unrecoverable.
        if (transform.position.y > RoofY || transform.position.y < CellarY)
        {
            var p = transform.position;
            Debug.LogWarning("[FixedEyeHeight] rig root at y=" + p.y.ToString("F2")
                + (p.y > RoofY ? " (floating)" : " (below floor)") + " — snapping to ground level.");
            transform.position = new Vector3(p.x, 0f, p.z);
        }

        Vector3 head = cameraTransform.localPosition;

        // TRULY FIXED eye height (regardless of the player's
        // height, everyone has the same view). The offset cancels the player's
        // measured head height and re-plants the eye on targetEyeHeight EVERY
        // frame — so a tall and a short player, standing or not, share the exact
        // same viewpoint. (Locking once and then drifting would let real standing
        // height leak back in above the tolerance band and make the
        // headset feel too tall.) A non-zero pose is required; while the HMD
        // reports the untracked (0,0,0) origin we hold the last offset instead of
        // snapping to 0. NOTE: this also cancels physical crouching (bending down
        // no longer lowers the view) — the intended trade for a uniform height.
        if (head.sqrMagnitude > 0.0001f)
        {
            _everTracked = true;
            _applied = HeightCalibration.FixedOffset(targetEyeHeight, head.y);
        }
        else if (!_everTracked)
        {
            // NO DEVICE AT ALL (PC Dev Mode / XR Device Simulator): an untracked HMD
            // reports exactly (0,0,0) forever, so the guard above never fires, the offset
            // stays 0, and the eye sits on the rig root — i.e. you spawn INSIDE THE FLOOR.
            //
            // Distinguished from LOSING tracking mid-session, which must still hold the
            // last offset rather than snap: that is what _everTracked separates. On a
            // headset head.y is non-zero from the first tracked frame, so this branch is
            // unreachable there and the headset path is untouched by construction.
            _applied = HeightCalibration.FixedOffset(targetEyeHeight, 0f);
        }

        // Spawn gate only: once a valid, settled pose confirms the offset is real,
        // mark Calibrated so the entrance teleport (which waits on it) never fires
        // on a garbage first-frame pose and shoves the rig onto the roof.
        if (!_done)
        {
            bool ok = HeightCalibration.PoseValid(head, _lastY);
            _lastY = head.y;
            if (!ok) _stable = 0;
            else if (++_stable >= SettleFrames)
            {
                _done = true;
                Debug.Log("[FixedEyeHeight] head measured at " + head.y.ToString("F2")
                    + " m -> offset " + _applied.ToString("F2") + " m (eye = " + targetEyeHeight + " m).");
            }
        }

        // The offset object's Y is OURS every frame.
        var lp = offsetTransform.localPosition;
        if (Mathf.Abs(lp.y - _applied) > 0.001f)
            offsetTransform.localPosition = new Vector3(lp.x, _applied, lp.z);

        // Telemetry — cheap, definitive when a height report comes in.
        if (Time.unscaledTime >= _nextDebug)
        {
            _nextDebug = Time.unscaledTime + DebugEvery;
            Debug.Log("[HeightDebug] scene=" + gameObject.scene.name
                + " root=" + transform.position.y.ToString("F2")
                + " offset=" + _applied.ToString("F2")
                + " head=" + head.y.ToString("F2")
                + " eyeWorld=" + cameraTransform.position.y.ToString("F2")
                + " calibrated=" + _done);
        }
    }
}
