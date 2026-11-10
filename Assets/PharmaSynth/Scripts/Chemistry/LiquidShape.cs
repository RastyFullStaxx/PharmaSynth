using System.Collections.Generic;
using UnityEngine;

/// The shape of a vessel's liquid. A beaker's liquid is a straight column, but an
/// Erlenmeyer flask narrows from its body into its neck, and a straight column of liquid
/// pokes out through the sloping glass at any real fill. Conical flasks get a lathed
/// mesh that follows the glass inward instead.
///
/// Meshes are in the unit space of Unity's cylinder primitive (y -1..1, radius 0.5 at the
/// base), so the fill-child sizing and the fill shader are unchanged.
public static class LiquidShape
{
    public const string ConicalMeshName = "ConicalLiquid";

    /// (t, r): t = fraction of the liquid's height from its floor, r = radius in cylinder
    /// units. Measured on ErlenmeyerFlask_400mL: full width to a third of the way up, then
    /// the cone, then the neck at about 56% of the base radius, inset from the glass.
    public static readonly Vector2[] ConicalProfile =
    {
        new Vector2(0f, 0.5f), new Vector2(0.36f, 0.5f), new Vector2(0.79f, 0.28f), new Vector2(1f, 0.28f),
    };

    public static bool IsConical(string vesselName)
    {
        if (string.IsNullOrEmpty(vesselName)) return false;
        string n = vesselName.ToLowerInvariant();
        return n.Contains("erlenmeyer") || n.Contains("conical");
    }

    /// Radius at height fraction t, linear between profile points (pure, suite-pinned).
    public static float RadiusAt(Vector2[] profile, float t)
    {
        t = Mathf.Clamp01(t);
        for (int i = 1; i < profile.Length; i++)
            if (t <= profile[i].x)
            {
                float k = Mathf.InverseLerp(profile[i - 1].x, profile[i].x, t);
                return Mathf.Lerp(profile[i - 1].y, profile[i].y, k);
            }
        return profile[profile.Length - 1].y;
    }

    private static Mesh s_conical;
    public static Mesh Conical => s_conical != null ? s_conical : (s_conical = Lathe(ConicalProfile, 24, ConicalMeshName));

    /// A closed solid of revolution from a (t, r) profile: sides, floor and top cap.
    public static Mesh Lathe(Vector2[] profile, int segments, string name)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        int rings = profile.Length;
        for (int i = 0; i < rings; i++)
        {
            float y = -1f + 2f * profile[i].x, r = profile[i].y;
            for (int s = 0; s <= segments; s++)
            {
                float a = s * Mathf.PI * 2f / segments;
                verts.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));
            }
        }
        int row = segments + 1;
        for (int i = 0; i < rings - 1; i++)
            for (int s = 0; s < segments; s++)
            {
                int a = i * row + s, b = a + 1, c = a + row, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        foreach (bool top in new[] { false, true })
        {
            int ring = top ? rings - 1 : 0;
            int centre = verts.Count;
            verts.Add(new Vector3(0f, top ? 1f : -1f, 0f));
            for (int s = 0; s < segments; s++)
            {
                int a = ring * row + s, b = a + 1;
                if (top) { tris.Add(centre); tris.Add(b); tris.Add(a); }
                else { tris.Add(centre); tris.Add(a); tris.Add(b); }
            }
        }
        var m = new Mesh { name = name };
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
