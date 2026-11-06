using UnityEngine;

/// Pure motion for Pharmee's hover rings, read as THRUSTER EXHAUST (suite-pinned).
///
/// ⛔ A scale pulse (`scale = 1 + 0.12 * sin(t * 2.2 * 30)`) is a ±12% LATERAL pulse at
/// about 10 Hz on a flat horizontal ring: a sideways shake, which a thruster never does,
/// and nothing flows anywhere.
///
/// Each ring is a puff of exhaust. It is born at the emitter, travels
/// DOWN, spreads slightly as it goes, and fades out at the bottom while the next one is
/// already on its way — so the four rings read as one continuous jet.
public static class PharmeeThrusterMath
{
    /// Cycles per second. ~1.2 reads as a steady hover jet; 30x is a blur.
    public const float DefaultFlowSpeed = 1.2f;
    /// How far a ring travels before it dies, in WORLD METRES.
    ///
    /// ⛔ Metres, not local units. The rings hang off a model node scaled about 24x, so
    /// treating this as local put the exhaust nearly 4 m below the floor. PharmeeAttitude
    /// converts it with InverseTransformVector so the number here means what it says.
    public const float DefaultTravel = 0.16f;
    /// How much wider a ring gets by the end of its run.
    public const float DefaultSpread = 0.45f;

    /// Where ring `index` of `count` is in its cycle right now. Evenly staggered, so the
    /// stream never gaps or clumps.
    public static float Phase(float time, int index, int count, float flowSpeed)
    {
        if (count <= 0) return 0f;
        return Mathf.Repeat(time * flowSpeed + (float)index / count, 1f);
    }

    /// How far DOWN the ring has travelled at this phase (returned positive; the caller
    /// applies it along its own down axis).
    public static float Drop(float phase01, float travel) => Mathf.Clamp01(phase01) * travel;

    /// Scale multiplier over the cycle: spreads as it falls, and is ZERO at both ends.
    ///
    /// The sine is the whole trick — a ring that simply looped would pop back to full size
    /// at the top of every cycle. Fading in and out means the loop point is invisible.
    public static float Swell(float phase01, float spread)
    {
        float p = Mathf.Clamp01(phase01);
        return (1f + spread * p) * Mathf.Sin(Mathf.PI * p);
    }
}
