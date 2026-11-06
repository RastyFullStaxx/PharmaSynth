using System.Collections.Generic;
using UnityEngine;

/// Pure fitted-watch geometry (edit-mode testable). The authored WatchModel is a
/// SOLID Tripo mesh — a solid object can never encircle a wrist
/// the way a real watch wraps. Instead we MEASURE the hand
/// mesh's wrist cross-section and generate an elliptical band around it, with a
/// disc face on top. Everything here is deterministic math on plain data.
public static class WatchMath
{
    public const float BandClearance = 0.0008f;  // hairline over the skin — snug like a worn strap
    public const float BandTube = 0.0035f;       // band strap thickness radius
    public const float MinHalfWidth = 0.012f;    // sanity floors if measurement fails
    public const float MinHalfHeight = 0.010f;
    public const float MaxHalfWidth = 0.032f;    // palm-base bulge must not widen the band
    public const float MaxHalfHeight = 0.028f;

    public struct WristSlice
    {
        public Vector2 center;       // cross-section centre (hand-local X/Y)
        public Vector2 halfExtents;  // half-width (X) and half-height (Y)
        public int samples;          // vertices that landed in the slice
    }

    /// Measure the wrist cross-section from mesh points (hand-root local space)
    /// inside a thin slab |z - sliceZ| <= tol.
    public static WristSlice MeasureSlice(IList<Vector3> points, float sliceZ, float tol)
    {
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        int n = 0;
        for (int i = 0; i < points.Count; i++)
        {
            var p = points[i];
            if (Mathf.Abs(p.z - sliceZ) > tol) continue;
            n++;
            if (p.x < minX) minX = p.x;
            if (p.x > maxX) maxX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.y > maxY) maxY = p.y;
        }
        if (n == 0) return new WristSlice { center = Vector2.zero, halfExtents = Vector2.zero, samples = 0 };
        return new WristSlice
        {
            center = new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f),
            halfExtents = new Vector2((maxX - minX) * 0.5f, (maxY - minY) * 0.5f),
            samples = n
        };
    }

    /// Elliptical band radii: measured half-extents + clearance, clamped to a
    /// plausible wrist range (the slice can catch the palm-base bulge).
    public static Vector2 BandRadii(Vector2 wristHalfExtents)
        => new Vector2(Mathf.Clamp(wristHalfExtents.x + BandClearance, MinHalfWidth, MaxHalfWidth),
                       Mathf.Clamp(wristHalfExtents.y + BandClearance, MinHalfHeight, MaxHalfHeight));

    /// Face sized against the band width, clamped to plausible watch sizes.
    public static float FaceDiameter(float bandRadiusX)
        => Mathf.Clamp(bandRadiusX * 1.5f, 0.024f, 0.040f);

    /// Elliptical torus: loop in the X-Y plane (loop normal +Z = the forearm
    /// axis), so it genuinely wraps a wrist whose arm runs along Z.
    public static Mesh BuildBandMesh(Vector2 radii, float tube, int loopSegs, int tubeSegs)
    {
        var verts = new List<Vector3>((loopSegs + 1) * (tubeSegs + 1));
        var norms = new List<Vector3>(verts.Capacity);
        var tris = new List<int>(loopSegs * tubeSegs * 6);
        for (int i = 0; i <= loopSegs; i++)
        {
            float u = (float)i / loopSegs * Mathf.PI * 2f;
            var centre = new Vector3(Mathf.Cos(u) * radii.x, Mathf.Sin(u) * radii.y, 0f);
            var outward = new Vector3(Mathf.Cos(u), Mathf.Sin(u), 0f);
            for (int j = 0; j <= tubeSegs; j++)
            {
                float v = (float)j / tubeSegs * Mathf.PI * 2f;
                var n = outward * Mathf.Cos(v) + Vector3.forward * Mathf.Sin(v);
                verts.Add(centre + n * tube);
                norms.Add(n);
            }
        }
        int ring = tubeSegs + 1;
        for (int i = 0; i < loopSegs; i++)
            for (int j = 0; j < tubeSegs; j++)
            {
                int a = i * ring + j, b = a + ring;
                tris.Add(a); tris.Add(a + 1); tris.Add(b);
                tris.Add(b); tris.Add(a + 1); tris.Add(b + 1);
            }
        var m = new Mesh { name = "WatchBand" };
        m.SetVertices(verts);
        m.SetNormals(norms);
        m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }
}
