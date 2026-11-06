using System.Collections.Generic;
using UnityEngine;

/// Pure geometry for Tutorial Mode's ground path: given the corners of a walkable
/// route, where does each flowing chevron sit and which way does it face?
///
/// Pure so the suite can pin the SHAPE of the path without a headset — that chevrons are
/// evenly spaced regardless of how unevenly the navmesh corners fall, that they face along
/// the route rather than at the destination, and that the flow runs toward the target and
/// not away from it. A path that compiles and points backwards is not something a compile
/// or a play-mode run would ever complain about.
public static class GuidePathMath
{
    /// Metres between chevrons. Close enough to read as a continuous flow at walking pace,
    /// far enough apart not to become a solid stripe.
    public const float Spacing = 0.45f;

    /// Metres per second the pattern slides toward the destination. Deliberately slower
    /// than a walk: a flow that outruns the player reads as "hurry up" rather than "this
    /// way", and this mode is explicitly untimed.
    public const float FlowSpeed = 0.7f;

    /// Hard ceiling on drawn chevrons, so a pathological route across the whole lab cannot
    /// spawn hundreds of quads.
    public const int MaxChevrons = 40;

    public struct Chevron
    {
        public Vector3 position;
        public Quaternion rotation;
        /// 0 at the player, 1 at the destination — drives the fade-in at the far end.
        public float along01;
    }

    /// Total length of a polyline. Zero for null/short input rather than throwing: an
    /// unreachable target is a normal outcome here, not an error.
    public static float Length(IReadOnlyList<Vector3> corners)
    {
        if (corners == null || corners.Count < 2) return 0f;
        float total = 0f;
        for (int i = 1; i < corners.Count; i++) total += Vector3.Distance(corners[i - 1], corners[i]);
        return total;
    }

    /// The point `distance` metres along the polyline, plus the direction of travel there.
    /// Clamped at both ends, so callers never have to bounds-check.
    public static bool Sample(IReadOnlyList<Vector3> corners, float distance,
                              out Vector3 point, out Vector3 forward)
    {
        point = default; forward = Vector3.forward;
        if (corners == null || corners.Count < 2) return false;

        if (distance <= 0f)
        {
            point = corners[0];
            var d0 = corners[1] - corners[0];
            if (d0.sqrMagnitude > 1e-6f) forward = d0.normalized;
            return true;
        }

        float walked = 0f;
        for (int i = 1; i < corners.Count; i++)
        {
            Vector3 seg = corners[i] - corners[i - 1];
            float len = seg.magnitude;
            if (len < 1e-6f) continue;
            if (walked + len >= distance)
            {
                float t = (distance - walked) / len;
                point = Vector3.Lerp(corners[i - 1], corners[i], t);
                forward = seg / len;
                return true;
            }
            walked += len;
        }

        point = corners[corners.Count - 1];
        var last = corners[corners.Count - 1] - corners[corners.Count - 2];
        if (last.sqrMagnitude > 1e-6f) forward = last.normalized;
        return true;
    }

    /// Lay chevrons along the route, offset by a scrolling phase so the pattern FLOWS.
    ///
    /// The phase subtracts from the distance rather than adding to it: the marks must
    /// travel from the player TOWARD the destination. Getting that sign wrong produces a
    /// path that visibly pulls you backwards, which is worse than no path at all — hence
    /// the suite pin.
    public static List<Chevron> Build(IReadOnlyList<Vector3> corners, float time,
                                      float spacing = Spacing, float flowSpeed = FlowSpeed)
    {
        var result = new List<Chevron>();
        float total = Length(corners);
        if (total < 0.05f) return result;

        spacing = Mathf.Max(0.05f, spacing);
        // Phase runs 0→spacing and wraps, so the marks slide forward continuously instead
        // of every chevron jumping a whole step at once.
        float phase = Mathf.Repeat(time * flowSpeed, spacing);

        for (float d = phase; d <= total && result.Count < MaxChevrons; d += spacing)
        {
            if (!Sample(corners, d, out var p, out var fwd)) break;
            result.Add(new Chevron
            {
                position = p,
                rotation = Quaternion.LookRotation(fwd, Vector3.up),
                along01 = total > 0f ? Mathf.Clamp01(d / total) : 0f,
            });
        }
        return result;
    }


    // ---- the mark itself -----------------------------------------------------------
    //
    // ⛔ The mark must be an actual chevron mesh, not a `PrimitiveType.Quad`.
    // A row of sliding squares cannot
    // read as direction, however correct the flow maths is, and the path would look
    // static to the player.

    /// Span across the route, in metres. Wider than it is deep: a chevron reads as an
    /// arrowhead because of the angle of its arms, and a narrow one just looks like a tick.
    public const float ChevronWidth = 0.30f;
    /// Front-to-back extent of the V, in metres.
    public const float ChevronDepth = 0.18f;
    /// Arm thickness, measured PERPENDICULAR to the arm (thick enough to
    /// be noticeable — in the headset thin arms read as outlines).
    public const float ChevronThickness = 0.075f;

    /// The open "V", flat in the XZ plane with its apex forward (+Z) and its face up (+Y).
    ///
    /// Returned as raw geometry rather than a Mesh so the suite can pin the SHAPE without a
    /// scene: that the apex really is the most-forward point, that the arms are symmetric,
    /// and above all that every triangle winds to face +Y.
    ///
    /// ⛔ The winding is load-bearing. `PharmaSynth/GuideOverlay` is `Cull Back`, so a flat
    /// mesh wound the wrong way is INVISIBLE FROM ABOVE — the path would silently draw
    /// nothing while every count, position and log line stayed correct.
    ///
    /// ⚠ Authored FLAT, unlike the quad it replaces. The old code rotated its upright quad
    /// by Euler(90,0,0) to lay it down; applying that to this mesh would stand the chevrons
    /// on edge across the route.
    public static void ChevronGeometry(out Vector3[] verts, out int[] tris,
                                       float width = ChevronWidth, float depth = ChevronDepth,
                                       float thickness = ChevronThickness)
    {
        float halfW = Mathf.Max(0.001f, width) * 0.5f;
        float halfD = Mathf.Max(0.001f, depth) * 0.5f;
        float t = Mathf.Max(0.001f, thickness);

        // The V as a polyline (left tail → apex → right tail), offset inward by `t`.
        //
        // ⛔ PERPENDICULAR to each arm, not straight back along Z. Extruding along Z makes
        // the visible arm thickness t·cos(arm angle) — about 64% of the number at these
        // proportions — so the parameter lied, and it also deepened the whole glyph as it
        // thickened. Offsetting along the arm's own normal makes `thickness` mean thickness.
        Vector3 apex = new Vector3(0f, 0f, halfD);
        Vector3 left = new Vector3(-halfW, 0f, -halfD);
        Vector3 right = new Vector3(halfW, 0f, -halfD);

        float armLen = Mathf.Sqrt(halfW * halfW + depth * depth);
        // Inward normal of the LEFT arm, in the XZ plane; the right arm mirrors it.
        Vector3 n = new Vector3(depth / armLen, 0f, -halfW / armLen);
        // The apex slides down its own bisector far enough for both offset arms to meet it.
        float apexDrop = t * armLen / Mathf.Max(1e-4f, halfW);

        Vector3 apexIn = apex + new Vector3(0f, 0f, -apexDrop);
        Vector3 leftIn = left + n * t;
        Vector3 rightIn = right + new Vector3(-n.x, 0f, n.z) * t;

        verts = new[] { apex, apexIn, left, leftIn, right, rightIn };
        //                0      1      2     3       4      5
        tris = new[]
        {
            0, 1, 2,   1, 3, 2,     // left arm
            0, 4, 1,   1, 4, 5,     // right arm
        };
    }

    /// Face normal of one triangle — the pinned property is that this is +Y for all four.
    public static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
        => Vector3.Cross(b - a, c - a).normalized;

    /// How far into the route the flow ramps up and back down, as a fraction of the whole.
    public const float FadeEdge = 0.12f;

    /// Per-mark opacity, 0 at both ends of the route and 1 through the middle.
    ///
    /// This is what makes the trail read as FLOW rather than as a marching row: a chevron is
    /// born at the player's feet and dies at the destination, and without a ramp each one
    /// POPS in and out at full strength every time the phase wraps. `along01` exists
    /// for exactly this; the driver must read it.
    public static float FadeAt(float along01, float edge = FadeEdge)
    {
        float p = Mathf.Clamp01(along01);
        edge = Mathf.Clamp(edge, 0.001f, 0.5f);
        float rampIn = Mathf.Clamp01(p / edge);
        float rampOut = Mathf.Clamp01((1f - p) / edge);
        return Mathf.SmoothStep(0f, 1f, Mathf.Min(rampIn, rampOut));
    }

    /// Which navigation cue answers "where is it" right now.
    ///
    /// The two must never draw together (design rule): the floor path routes AROUND
    /// the benches and is what you want at a distance; the beacon reads THROUGH a cabinet
    /// door and is what you want once the object itself is the question. Showing both
    /// makes each one weaker.
    public const float NearDistance = 2f;

    public static bool ShowPath(float distanceToTarget, bool hasRoute)
        => hasRoute && distanceToTarget > NearDistance;

    public static bool ShowBeacon(float distanceToTarget, bool hasRoute)
        => !ShowPath(distanceToTarget, hasRoute);
}
