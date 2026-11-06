using UnityEngine;

/// What Pharmee's body is doing right now, on top of his hover.
///
/// Deliberately NOT a new state enum: `PharmeeState` (PharmeeBrain.cs) already IS the set,
/// and already drives his line pool, his face and his beep. This is the fourth mapping off
/// the same state, not a parallel machine.
public enum PharmeeGesture { None, Greet, Point, Warn, Celebrate, Nod }

/// One frame of gesture, in the four channels Pharmee actually has.
public struct PharmeePose
{
    /// Extra body rotation, folded into PharmeeAttitude's single localRotation assignment.
    public Quaternion bodyRot;
    /// Extra local offset, folded into FloatBob's single localPosition sum.
    public Vector3 rootOffset;
    /// 0-1, how far the hand pivots are raised. The model ships two hand nodes
    /// ("Hand origin", "Hand origin.002") that nothing has ever moved.
    public float handRaise;
    /// Extra multiplier on the hover rings' scale (1 = untouched).
    public float waveFlare;

    public static PharmeePose Rest => new PharmeePose
    { bodyRot = Quaternion.identity, rootOffset = Vector3.zero, handRaise = 0f, waveFlare = 1f };
}

/// Tunable magnitudes, passed in rather than read from statics so the suite can drive them
/// and the inspector can retune them without a code change. Real motion always needs a
/// calibration knob - the right amplitude is a headset judgement, not an editor one.
[System.Serializable]
public struct PharmeeGestureTuning
{
    public float nodDegrees;
    public float pointLeanDegrees;
    public float warnRecoilDegrees;
    public float warnShakeDegrees;
    public float celebrateRise;
    public float celebrateSpinDegrees;
    public float celebrateFlare;

    /// "Clearly readable": noticeable without staring. For scale, the
    /// talk nod this replaces was 2.5 degrees against a 14-degree lean cap - invisible.
    public static PharmeeGestureTuning Default => new PharmeeGestureTuning
    {
        nodDegrees          = 7f,
        pointLeanDegrees    = 8f,
        warnRecoilDegrees   = 10f,
        warnShakeDegrees    = 5f,
        celebrateRise       = 0.18f,
        celebrateSpinDegrees= 360f,
        celebrateFlare      = 0.35f,
    };
}

/// Pure gesture curves - no Time.time, no transforms, no Unity state. Every value is a
/// function of (gesture, seconds since it started, tuning), which is what makes the whole
/// animation set checkable in edit mode. There is no headset pass available, so anything
/// that cannot be pinned cannot be trusted.
///
/// Precedents this matches exactly: PharmeeAttitude.LeanFor, PharmeeGiveWay.SideStep,
/// FloatBob.JitterOffset, LabTourGuide.FirstUnvisitedInRange, SpeakerLedBlink.Level01.
public static class PharmeeGestureMath
{
    /// How long each gesture runs before it returns to rest. Point is SUSTAINED - it holds
    /// for as long as he is instructing, so the driver ends it, not the clock.
    public static float DurationOf(PharmeeGesture g)
    {
        switch (g)
        {
            case PharmeeGesture.Greet:     return 1.6f;
            case PharmeeGesture.Nod:       return 0.9f;
            case PharmeeGesture.Warn:      return 1.1f;
            case PharmeeGesture.Celebrate: return 2.2f;
            case PharmeeGesture.Point:     return float.PositiveInfinity;
            default:                       return 0f;
        }
    }

    public static bool IsSustained(PharmeeGesture g) => g == PharmeeGesture.Point;

    /// Which gesture a behavioural state asks for. The fourth mapping off PharmeeState,
    /// alongside the line pool, the face and the beep that already exist.
    public static PharmeeGesture ForState(PharmeeState s)
    {
        switch (s)
        {
            case PharmeeState.Greeting:    return PharmeeGesture.Greet;
            case PharmeeState.Instructing: return PharmeeGesture.Point;
            case PharmeeState.Warning:     return PharmeeGesture.Warn;
            case PharmeeState.Celebrating: return PharmeeGesture.Celebrate;
            case PharmeeState.Encouraging: return PharmeeGesture.Nod;
            default:                       return PharmeeGesture.None;
        }
    }

    /// Which gesture a door-gate state asks for. Mirrors PharmeeMood.ExpressionForGate so
    /// the body agrees with the face rather than drifting from it.
    public static PharmeeGesture ForGate(GateState s)
    {
        switch (s)
        {
            case GateState.SupplyPrompt:
            case GateState.ThresholdWarn:   return PharmeeGesture.Warn;
            case GateState.UnlockAnnounce:  return PharmeeGesture.Celebrate;
            case GateState.ModeChoice:
            case GateState.Blocked:         return PharmeeGesture.Greet;
            case GateState.Debrief:         return PharmeeGesture.Nod;
            default:                        return PharmeeGesture.None;
        }
    }

    /// The pose for one frame. `t` is seconds since the gesture began.
    public static PharmeePose Pose(PharmeeGesture g, float t, PharmeeGestureTuning tune)
    {
        var pose = PharmeePose.Rest;
        if (g == PharmeeGesture.None || t < 0f) return pose;

        float dur = DurationOf(g);
        if (!IsSustained(g) && t >= dur) return pose;   // finished - back to rest exactly

        switch (g)
        {
            case PharmeeGesture.Nod:
            {
                // Two quick pitch dips under a rise-and-fall envelope.
                float e = Envelope(t / dur);
                pose.bodyRot = Quaternion.Euler(Mathf.Sin(t * 9f) * tune.nodDegrees * e, 0f, 0f);
                break;
            }

            case PharmeeGesture.Greet:
            {
                float u = t / dur;
                float e = Envelope(u);
                // Hand comes up and waves; the body nods along with it.
                pose.handRaise = e;
                pose.bodyRot = Quaternion.Euler(Mathf.Sin(t * 7f) * tune.nodDegrees * 0.7f * e,
                                                0f,
                                                Mathf.Sin(t * 5f) * tune.nodDegrees * 0.5f * e);
                break;
            }

            case PharmeeGesture.Point:
            {
                // Sustained: ease in over ~0.35 s and HOLD. The aim itself is the driver's
                // job - it needs a world target, which pure math cannot see.
                float e = Mathf.Clamp01(t / 0.35f);
                pose.handRaise = e;
                pose.bodyRot = Quaternion.Euler(tune.pointLeanDegrees * e, 0f, 0f);
                break;
            }

            case PharmeeGesture.Warn:
            {
                // Sharp recoil BACK, then a damped shake as he settles - a flinch, not a wobble.
                float u = t / dur;
                float decay = 1f - u;
                float recoil = -tune.warnRecoilDegrees * Mathf.Exp(-t * 6f);
                float shake  = Mathf.Sin(t * 26f) * tune.warnShakeDegrees * decay * decay;
                pose.bodyRot = Quaternion.Euler(recoil, shake, shake * 0.5f);
                pose.rootOffset = new Vector3(0f, 0f, -0.05f * Mathf.Exp(-t * 6f));
                break;
            }

            case PharmeeGesture.Celebrate:
            {
                float u = t / dur;
                float e = Envelope(u);
                // Rise, one full spin, and the hover rings flare - the rings are already his
                // most characterful part, so the celebration uses them.
                pose.rootOffset = Vector3.up * (tune.celebrateRise * e);
                pose.bodyRot = Quaternion.Euler(0f, tune.celebrateSpinDegrees * EaseInOut(u), 0f);
                pose.handRaise = e;
                pose.waveFlare = 1f + tune.celebrateFlare * e;
                break;
            }
        }
        return pose;
    }

    /// Rise-hold-fall in 0-1, peaking on the plateau. Keeps every gesture starting and
    /// ending at exactly rest, so nothing pops when it hands back to the hover.
    public static float Envelope(float u)
    {
        u = Mathf.Clamp01(u);
        if (u < 0.25f) return EaseInOut(u / 0.25f);
        if (u > 0.70f) return EaseInOut(1f - (u - 0.70f) / 0.30f);
        return 1f;
    }

    /// Smoothstep. Deterministic and allocation-free.
    public static float EaseInOut(float u)
    {
        u = Mathf.Clamp01(u);
        return u * u * (3f - 2f * u);
    }
}
