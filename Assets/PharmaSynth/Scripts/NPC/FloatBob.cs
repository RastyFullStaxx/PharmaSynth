using UnityEngine;

/// Gentle hover animation for a floating character/prop (Pharmee): sinusoidal
/// vertical bob plus a slight sway tilt and a bounded Perlin jitter ("alive"
/// wiggle), around a movable home pose. Local-space so
/// it composes with any parent motion; a mover may drive the home via SetHome.
public class FloatBob : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.055f;
    [SerializeField] private float bobSpeed = 1.6f;
    [SerializeField, Range(0f, 8f)] private float swayDegrees = 2.5f;
    [SerializeField] private float jitterAmplitude = 0.012f;
    [SerializeField] private float jitterSpeed = 2.3f;
    [Tooltip("Turn OFF when a FaceCamera controls this object's rotation (e.g. Pharmee looking at the player), so the two don't fight.")]
    [SerializeField] private bool applyRotation = true;

    [Tooltip("How fast the give-way sidestep offset eases in/out (higher = snappier).")]
    [SerializeField] private float giveWaySmooth = 6f;

    private Vector3 _homePos;
    private Quaternion _homeRot;
    private float _phase;
    private bool _homeSet;
    private Vector3 _giveWay;         // smoothed sidestep offset (local space)
    private Vector3 _giveWayTarget;   // driven by PharmeeGiveWay each frame
    private Vector3 _gesture;         // smoothed gesture offset (local space)
    private Vector3 _gestureTarget;   // driven by PharmeeGestures each frame

    /// Current hover home (local space). A PharmeeMover glides this around.
    public Vector3 Home => _homePos;

    public void SetHome(Vector3 localPos) { _homePos = localPos; _homeSet = true; }

    /// A transient local-space step-aside offset added on top of home + bob, eased
    /// in/out — set to zero to return. Driven by PharmeeGiveWay when the player nears.
    public void SetGiveWayOffset(Vector3 localOffset) => _giveWayTarget = localOffset;

    /// Current smoothed sidestep magnitude (for tests / debug).
    public Vector3 GiveWay => _giveWay;

    /// A transient local-space GESTURE offset (celebrate's rise, warn's recoil), on the same
    /// terms as the sidestep above: one more term in the single position sum, eased the same
    /// way. Deliberately a sibling of SetGiveWayOffset rather than a second component that
    /// writes the transform - FloatBob is the SOLE writer of this object's position, and the
    /// only reason the four Pharmee scripts compose at all is that none of them competes for
    /// it. PharmeeMover and PharmeeGiveWay both feed FloatBob instead of moving anything.
    public void SetGestureOffset(Vector3 localOffset) => _gestureTarget = localOffset;

    /// Current smoothed gesture magnitude (for tests / debug).
    public Vector3 Gesture => _gesture;

    private void Awake()
    {
        if (!_homeSet) _homePos = transform.localPosition;
        _homeRot = transform.localRotation;
        _phase = Random.value * 10f;   // desync multiple floaters
        _homeSet = true;
    }

    private void Update()
    {
        float t = Time.time + _phase;
        float k = Mathf.Clamp01(Time.deltaTime * giveWaySmooth);
        _giveWay = Vector3.Lerp(_giveWay, _giveWayTarget, k);
        _gesture = Vector3.Lerp(_gesture, _gestureTarget, k);
        transform.localPosition = _homePos
            + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude)
            + JitterOffset(t, jitterSpeed, jitterAmplitude)
            + _giveWay
            + _gesture;
        if (applyRotation)
            transform.localRotation = _homeRot * Quaternion.Euler(
                Mathf.Sin(t * bobSpeed * 0.7f) * swayDegrees,
                0f,
                Mathf.Cos(t * bobSpeed * 0.5f) * swayDegrees * 0.6f);
    }

    /// Bounded, smooth 3-axis wiggle (deterministic per t — unit-testable).
    public static Vector3 JitterOffset(float t, float speed, float amplitude)
    {
        if (amplitude <= 0f) return Vector3.zero;
        float x = Mathf.PerlinNoise(t * speed, 0.13f) - 0.5f;
        float y = Mathf.PerlinNoise(0.71f, t * speed) - 0.5f;
        float z = Mathf.PerlinNoise(t * speed * 0.83f, t * speed * 0.59f) - 0.5f;
        return new Vector3(x, y, z) * (2f * amplitude);
    }
}
