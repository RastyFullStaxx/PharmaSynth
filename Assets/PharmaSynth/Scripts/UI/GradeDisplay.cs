using UnityEngine;

/// Pure display rules for grades: the pass gate compares RAW values
/// (Total >= 90), so displayed percentages must FLOOR — rounding 89.6 up to
/// "90%" beside a TRY AGAIN verdict reads as a contradiction. Suite-pinned.
public static class GradeDisplay
{
    /// Gate-relevant percent for display: floored, clamped to [0,100].
    public static int Percent(float raw) => Mathf.Clamp(Mathf.FloorToInt(raw), 0, 100);

    /// Mastery fraction (0..1) for display: floored percent.
    public static int MasteryPercent(float raw01) => Percent(raw01 * 100f);
}
