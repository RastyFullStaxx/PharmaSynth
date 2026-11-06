using UnityEngine;

/// Pure circular-motion accumulator shared by the STIR and GRIND verbs:
/// feed it the tool tip's XZ offset from the vessel/bowl axis each frame; while
/// the tip stays inside the working radius the swept angle accumulates
/// (direction-agnostic, per-sample clamped so teleports/jitter can't cheat).
/// Leaving the zone PAUSES progress — it never resets (a student lifting the
/// rod to check shouldn't lose their work).
public class OrbitMath
{
    private float _lastAngleDeg;
    private bool _tracking;
    private float _sweptDeg;

    public float requiredRevs = 2.5f;
    public const float MaxDegPerSample = 45f;   // anti-cheat: no quarter-turn teleports

    public float Progress01 => Mathf.Clamp01(_sweptDeg / (Mathf.Max(0.1f, requiredRevs) * 360f));
    public bool IsDone => Progress01 >= 1f;
    public float SweptDegrees => _sweptDeg;

    /// One sample: (x,z) = tool tip offset from the axis, inside = within the
    /// working radius and depth band.
    public void Feed(float x, float z, bool inside)
    {
        if (!inside || (Mathf.Abs(x) < 1e-5f && Mathf.Abs(z) < 1e-5f))
        {
            _tracking = false;   // pause — angle re-anchors on re-entry
            return;
        }
        float a = Mathf.Atan2(z, x) * Mathf.Rad2Deg;
        if (_tracking)
            _sweptDeg += Mathf.Min(Mathf.Abs(Mathf.DeltaAngle(_lastAngleDeg, a)), MaxDegPerSample);
        _lastAngleDeg = a;
        _tracking = true;
    }

    public void Reset()
    {
        _sweptDeg = 0f;
        _tracking = false;
    }
}
