using UnityEngine;

/// Pure rules for correcting an auto-rigger's weight bleed (suite-pinned).
///
/// Dr. Jimenez is a Tripo auto-rig: ONE SkinnedMeshRenderer over 41 bones, with the lab coat
/// baked into the body mesh. Nothing can be unparented — the coat follows his arm because
/// coat vertices carry weight on the arm bones. Auto-riggers assign weights by proximity, so
/// a coat panel hanging near the elbow picks up elbow weight even though a real coat would
/// hang from the shoulders.
///
/// The correction is geometric: a vertex that carries arm weight while sitting well OUTSIDE
/// the arm is bleed, and its weight belongs on the spine.
public static class JimenezRigMath
{
    /// ⛔ The sleeve radius must be expressed in the MESH's own units, not in metres.
    /// This mesh is 0.5 units tall and the prefab scales it to a 1.75 m man, so a "0.11 m"
    /// radius was HALF THE CHARACTER'S WIDTH and classified the entire coat as sleeve —
    /// the first run freed 43 vertices out of 6992 and changed nothing visible.
    ///
    /// Expressed as a fraction of the model's height it is scale-proof: a re-export at any
    /// size still separates a sleeve from a coat panel. 7% of height ≈ a 12 cm sleeve on a
    /// 1.75 m man, which is a loose lab coat.
    public const float SleeveRadiusFraction = 0.07f;

    public static float SleeveRadiusFor(float meshHeight)
        => Mathf.Max(1e-4f, meshHeight) * SleeveRadiusFraction;

    /// Bones that swing the arm.
    ///
    /// ⛔ Two deliberate exclusions, both of which shredded the model when they were in:
    ///
    /// The CLAVICLE, because the shoulder of a coat really does follow the clavicle, and
    /// freeing it would detach the yoke.
    ///
    /// The HAND, because a lab coat has no geometry past the wrist, so there is nothing there
    /// to free — and this rig has NO finger bones, which makes `L_Hand` a LEAF. A leaf bone
    /// has no segment, so its "distance to the bone" collapses to distance from the wrist
    /// joint, every fingertip measures as far away, and the whole hand is reclassified as
    /// coat and handed to the spine. That renders as fingers stretched into ribbons hanging
    /// off the wrists (verified). Nothing is lost by skipping it.
    public static bool IsArmBone(string boneName)
    {
        if (string.IsNullOrEmpty(boneName)) return false;
        if (!boneName.StartsWith("L_") && !boneName.StartsWith("R_")) return false;
        return boneName.Contains("Upperarm") || boneName.Contains("Forearm");
    }

    /// A twist bone is not a limb of its own — it is a sub-bone that spins WITHIN its parent
    /// limb, so it must be measured against the parent's segment, not its own.
    ///
    /// ⛔ Measuring a twist bone against itself is what tore the sleeve off.
    /// `L_UpperarmTwist02` is a LEAF, so its "segment" is a single point at the elbow; sleeve
    /// vertices weighted to it all measured as far away, were called coat, and were handed to
    /// the spine — so the sleeve stayed behind while the arm rose, stretching it into ribbons
    /// hanging from the wrists. `L_UpperarmTwist01` is barely better: its segment is the 0.038
    /// stub to Twist02. Judged against the whole shoulder-to-elbow limb instead, both are
    /// correct, and no threshold has to be guessed.
    public static bool IsTwistBone(string boneName)
        => !string.IsNullOrEmpty(boneName) && boneName.Contains("Twist");

    /// Which child bone is a bone's segment end: the FARTHEST one, never the nearest.
    ///
    /// ⛔ This rig co-locates every twist bone with its parent — `L_UpperarmTwist01` sits at
    /// EXACTLY the shoulder joint. Taking the nearest child therefore gave `L_Upperarm` a
    /// segment of length 0.000, so "distance along the arm" silently became "distance from
    /// the shoulder joint" and the sleeve near the elbow read as coat. Taking the farthest
    /// child picks Upperarm→Forearm and Forearm→Hand, which are the real limbs.
    public static bool IsBetterSegmentEnd(float candidateDistance, float bestSoFar)
        => candidateDistance > bestSoFar;

    /// Is this arm weight bleed rather than sleeve?
    public static bool IsBleed(float distanceToBone, float sleeveRadius)
        => distanceToBone > sleeveRadius;

    /// The same question, but able to tell a SLEEVE from a COAT PANEL HANGING BESIDE THE ARM.
    ///
    /// ⛔ Distance alone cannot. In this model's A-pose bind, the forearm hangs beside the
    /// hip, so the coat's lower front panel sits as close to the forearm bone as the cuff
    /// does, both well inside the 12 cm sleeve radius. Left on the arm, that panel tears
    /// into a jagged flap at the hip when he raises his hands.
    ///
    /// A cuff wraps the limb and is far from the body's centre line; a coat panel hangs off
    /// the torso and is near it. So "closer to the arm than to the spine" separates them,
    /// and it needs no new tuning knob.
    public static bool IsSleeve(float distanceToArm, float distanceToSpine, float sleeveRadius)
        => distanceToArm <= sleeveRadius && distanceToArm < distanceToSpine;

    /// Width of the blend band beyond the sleeve, as a fraction of the model's height.
    public const float BlendFraction = 0.06f;

    public static float BlendBandFor(float meshHeight)
        => Mathf.Max(1e-4f, meshHeight) * BlendFraction;

    /// HOW MUCH of a vertex's arm weight moves to the spine: 0 on the sleeve, ramping
    /// smoothly to 1 across a band, so neighbouring vertices are never handed to different
    /// bones outright.
    ///
    /// ⛔ The blend is required. A hard cut moves 100% of the weight on one side of the
    /// radius and 0% on the other, so adjacent vertices follow different bones and the
    /// edge between them rips: against the untouched mesh, worst edge stretch went from
    /// x4.2 to x14.9 and torn edges from 32 to 174, worse than the auto-rig itself. An
    /// auto-rigger's weights are crude but CONTINUOUS; any correction has to stay continuous too.
    public static float TransferFraction(float distanceToArm, float sleeveRadius, float blendBand)
    {
        if (distanceToArm <= sleeveRadius) return 0f;
        if (blendBand <= 1e-5f) return 1f;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((distanceToArm - sleeveRadius) / blendBand));
    }

    /// Distance from a point to a bone SEGMENT (bone head to its child), not just to the
    /// joint — an upper arm is a long limb and measuring to the shoulder joint alone would
    /// call the whole forearm "far away".
    public static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 1e-8f) return Vector3.Distance(p, a);
        float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
        return Vector3.Distance(p, a + ab * t);
    }

    /// Move every gram of weight sitting on `fromBone` onto `toBone`, keeping the four
    /// influences summing to 1. Returns the amount transferred (0 when there was none).
    ///
    /// Written as a value-in/value-out helper so the suite can prove the invariant that
    /// matters: a re-weighted vertex is still normalised, or the mesh deforms at the wrong
    /// scale and every fix looks like a modelling error.
    public static float Redistribute(ref BoneWeight w, int fromBone, int toBone)
        => Redistribute(ref w, fromBone, toBone, 1f);

    /// Move `fraction` of the weight sitting on `fromBone` onto `toBone`. A fraction below 1
    /// leaves the rest on the arm, which is what keeps the surface continuous across the
    /// sleeve boundary — see TransferFraction.
    public static float Redistribute(ref BoneWeight w, int fromBone, int toBone, float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        if (fraction <= 0f) return 0f;
        float moved = 0f;
        if (w.boneIndex0 == fromBone) { float t = w.weight0 * fraction; moved += t; w.weight0 -= t; }
        if (w.boneIndex1 == fromBone) { float t = w.weight1 * fraction; moved += t; w.weight1 -= t; }
        if (w.boneIndex2 == fromBone) { float t = w.weight2 * fraction; moved += t; w.weight2 -= t; }
        if (w.boneIndex3 == fromBone) { float t = w.weight3 * fraction; moved += t; w.weight3 -= t; }
        if (moved <= 0f) return 0f;

        // Add to the target if it is already an influence; otherwise take the emptiest slot.
        if (w.boneIndex0 == toBone) w.weight0 += moved;
        else if (w.boneIndex1 == toBone) w.weight1 += moved;
        else if (w.boneIndex2 == toBone) w.weight2 += moved;
        else if (w.boneIndex3 == toBone) w.weight3 += moved;
        else
        {
            if (w.weight0 <= 0f) { w.boneIndex0 = toBone; w.weight0 = moved; }
            else if (w.weight1 <= 0f) { w.boneIndex1 = toBone; w.weight1 = moved; }
            else if (w.weight2 <= 0f) { w.boneIndex2 = toBone; w.weight2 = moved; }
            else { w.boneIndex3 = toBone; w.weight3 = moved; }
        }
        Normalise(ref w);
        return moved;
    }

    /// Unity requires the four influences to sum to 1.
    public static void Normalise(ref BoneWeight w)
    {
        float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
        if (sum <= 1e-6f) { w.weight0 = 1f; w.weight1 = w.weight2 = w.weight3 = 0f; return; }
        w.weight0 /= sum; w.weight1 /= sum; w.weight2 /= sum; w.weight3 /= sum;
    }

    public static float Sum(BoneWeight w) => w.weight0 + w.weight1 + w.weight2 + w.weight3;

    /// Pull an animated value back toward its rest pose. `factor` 1 leaves it untouched, 0
    /// freezes it at rest.
    ///
    /// Used to soften how far Jimenez throws his arms, which is the only lever left on the
    /// coat: the mesh cannot be re-weighted without tearing (see TransferFraction), but the
    /// coat can only drag as far as the arm actually travels.
    public static float Damp(float value, float rest, float factor)
        => rest + (value - rest) * factor;
}
