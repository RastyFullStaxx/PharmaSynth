using UnityEngine;

/// Which motion a step is asking for. DERIVED in TutorialTargets.Build() from the
/// component that actually completes the step — never authored per task, for the same
/// reason the target sweep is derived: an authored list drifts from the binding it
/// describes, and a demonstration that mimes the wrong verb is worse than none.
///
/// Deliberately only FIVE. Heat, chill, weigh, litmus, flame and collect are all
/// "carry this vessel to that tool", so they share `Place` rather than each earning a
/// bespoke curve that would look identical on screen.
public enum VerbKind { Place, Pour, Stir, Grind, Scoop }

/// Where the demonstration ghost is, at a moment in its loop.
public struct VerbPose
{
    public Vector3 position;
    public Quaternion rotation;
}

/// Pure motion curves for Tutorial Mode's verb demonstration: given a verb and
/// two world points, where should the ghost be at normalised time t?
///
/// Pure so the suite can pin the SHAPE of each motion without a headset — that a pour
/// actually tips past horizontal, that a stir completes real revolutions, that a travel
/// arc never sinks through the bench. A curve that looks plausible in code and does
/// nothing useful in VR is exactly what this file exists to prevent.
///
/// No hand is modelled. The ghost is a copy of the object the player must move, which
/// is both cheaper (no rig, no clips, no new art) and clearer: the thing that moves on
/// screen is the thing they have to pick up.
public static class VerbDemoMath
{
    /// Metres of lift at the peak of a travel arc. Enough to read as "over and across"
    /// rather than "dragged through the bench top".
    public const float ArcHeight = 0.18f;

    /// A real pour tips well PAST horizontal — stopping at 90° reads as hesitation.
    public const float PourTiltDeg = 115f;

    /// How high above the destination the ghost hovers while pouring or stirring.
    public const float WorkHeight = 0.12f;

    public const float CircleRadius = 0.035f;
    public const float ScoopDipDepth = 0.06f;
    public const float ScoopTipDeg = 70f;

    /// Matches OrbitMath's default so the demo shows the number of turns the verb
    /// really wants. Callers that know the controller's own value should pass it.
    public const float DefaultRevs = 2.5f;

    /// Smoothstep — a linear demo reads as a machine, not a hand.
    public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    /// Re-normalise t into a sub-phase [a,b] of the loop, clamped outside it.
    public static float Phase(float t, float a, float b)
        => b <= a ? 0f : Mathf.Clamp01((t - a) / (b - a));

    /// Travel with a parabolic lift. The arc peaks at ArcHeight above the straight
    /// line, so it never dips below either endpoint — pinned, because a ghost that
    /// sinks through the bench on its way across reads as a bug.
    public static Vector3 Arc(Vector3 from, Vector3 to, float t01)
    {
        float t = Ease(t01);
        Vector3 p = Vector3.Lerp(from, to, t);
        p.y += 4f * t * (1f - t) * ArcHeight;
        return p;
    }

    /// Total degrees swept by a stir/grind demo at time t.
    public static float StirAngleDeg(float t01, float revs = DefaultRevs)
        => Mathf.Clamp01(t01) * Mathf.Max(0.1f, revs) * 360f;

    /// The axis a pour tips about: perpendicular to the horizontal approach, so the
    /// mouth of the vessel swings toward the destination rather than sideways to it.
    /// Falls back to world right when the two points are stacked vertically.
    public static Vector3 TiltAxis(Vector3 from, Vector3 to)
    {
        Vector3 dir = to - from; dir.y = 0f;
        if (dir.sqrMagnitude < 1e-5f) return Vector3.right;
        return Vector3.Cross(Vector3.up, dir.normalized).normalized;
    }

    /// The whole demonstration, as one function. t01 = 0 is always the resting start
    /// (the object where it currently sits), so the loop reads as "pick it up from
    /// THERE" every time it repeats.
    public static VerbPose Sample(VerbKind kind, float t01, Vector3 from, Vector3 to,
                                  float revs = DefaultRevs)
    {
        t01 = Mathf.Clamp01(t01);
        var pose = new VerbPose { position = from, rotation = Quaternion.identity };
        Vector3 work = to + Vector3.up * WorkHeight;

        switch (kind)
        {
            case VerbKind.Pour:
                // Carry it over (0–.35), tip it in (.35–.75), level off (.75–1).
                if (t01 < 0.35f)
                {
                    pose.position = Arc(from, work, Phase(t01, 0f, 0.35f));
                }
                else
                {
                    pose.position = work;
                    float tilt = t01 < 0.75f
                        ? Ease(Phase(t01, 0.35f, 0.75f)) * PourTiltDeg
                        : (1f - Ease(Phase(t01, 0.75f, 1f))) * PourTiltDeg;
                    pose.rotation = Quaternion.AngleAxis(tilt, TiltAxis(from, to));
                }
                break;

            case VerbKind.Scoop:
                // Dip in (0–.3), lift out (.3–.4), carry (.4–.85), tip it in (.85–1).
                if (t01 < 0.3f)
                    pose.position = from + Vector3.down * (Ease(Phase(t01, 0f, 0.3f)) * ScoopDipDepth);
                else if (t01 < 0.4f)
                    pose.position = from + Vector3.down * ((1f - Ease(Phase(t01, 0.3f, 0.4f))) * ScoopDipDepth);
                else if (t01 < 0.85f)
                    pose.position = Arc(from, work, Phase(t01, 0.4f, 0.85f));
                else
                {
                    pose.position = work;
                    pose.rotation = Quaternion.AngleAxis(
                        Ease(Phase(t01, 0.85f, 1f)) * ScoopTipDeg, TiltAxis(from, to));
                }
                break;

            case VerbKind.Stir:
            case VerbKind.Grind:
                // Carry it over (0–.25), then circle in the vessel. The tool stays
                // upright throughout — a rod or pestle is used vertically.
                if (t01 < 0.25f)
                {
                    pose.position = Arc(from, work, Phase(t01, 0f, 0.25f));
                }
                else
                {
                    float ang = StirAngleDeg(Phase(t01, 0.25f, 1f), revs) * Mathf.Deg2Rad;
                    pose.position = work + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * CircleRadius;
                }
                break;

            default:   // Place — bring this vessel to that tool.
                pose.position = Arc(from, to, t01);
                break;
        }
        return pose;
    }
}
