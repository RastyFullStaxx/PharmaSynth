using UnityEngine;

/// Animates the waypoint marker: a circular glow that sits on the target surface
/// (pulsing gently) and a downward arrow bobbing above it — a clear "go/act here"
/// signal instead of an ambiguous floating blob. WaypointGuide moves the root;
/// this only animates the children in local space.
public class WaypointBeacon : MonoBehaviour
{
    [SerializeField] private Transform arrow;   // bobs up/down and slowly spins
    [SerializeField] private Transform glow;    // floor disc, pulses scale

    [Header("Motion")]
    [SerializeField] private float bobAmplitude = 0.07f;
    [SerializeField] private float bobSpeed = 2.4f;
    [SerializeField] private float spinDegreesPerSecond = 70f;
    [SerializeField, Range(0f, 0.3f)] private float glowPulse = 0.10f;

    private Vector3 _arrowHome;
    private Vector3 _glowHomeScale;

    private void Awake() => CacheHomes();

    private void CacheHomes()
    {
        if (arrow != null) _arrowHome = arrow.localPosition;
        if (glow != null) _glowHomeScale = glow.localScale;
    }

    private void Update()
    {
        float t = Time.time;
        if (arrow != null)
        {
            arrow.localPosition = _arrowHome + Vector3.up * (Mathf.Sin(t * bobSpeed) * bobAmplitude);
            arrow.Rotate(0f, spinDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }
        if (glow != null)
        {
            float s = 1f + glowPulse * Mathf.Sin(t * bobSpeed);
            glow.localScale = new Vector3(_glowHomeScale.x * s, _glowHomeScale.y, _glowHomeScale.z * s);
        }
    }
}
