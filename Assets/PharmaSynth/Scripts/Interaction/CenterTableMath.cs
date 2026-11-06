using System.Collections.Generic;
using UnityEngine;

/// Pure geometry for the center-table merge (one wide
/// LANDSCAPE table centered in the lab replaces the two islands). The experiment layouts
/// bake world positions on the old left island, so the merge is a rigid remap —
/// rotate 90° about the old island centre, translate to the new centre — applied
/// identically to the island object AND every baked position on it, keeping every
/// station/prop exactly where it sat on the deck.
public static class CenterTableMath
{
    /// XZ bounds of a point set, padded by margin (y spans the points as-is).
    public static Bounds FootprintOf(IEnumerable<Vector3> positions, float margin)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var p in positions)
        {
            if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
            else b.Encapsulate(p);
        }
        if (any) b.Expand(new Vector3(margin * 2f, 0.2f, margin * 2f));
        return b;
    }

    /// Rigid remap: offset from the old centre, optional +90° yaw, onto the new
    /// centre. Height is preserved (same deck height on the moved table).
    public static Vector3 Remap(Vector3 p, Vector3 oldCenter, Vector3 newCenter, bool rotate90)
    {
        Vector3 off = p - oldCenter;
        if (rotate90) off = new Vector3(off.z, off.y, -off.x);   // yaw +90°
        return new Vector3(newCenter.x + off.x, p.y, newCenter.z + off.z);
    }

    /// The matching rotation for objects that ride the remap.
    public static Quaternion RemapRotation(Quaternion q, bool rotate90)
        => rotate90 ? Quaternion.Euler(0f, 90f, 0f) * q : q;

    /// XZ-only containment test with padding.
    public static bool WithinXZ(Vector3 p, Bounds b, float pad = 0f)
        => p.x >= b.min.x - pad && p.x <= b.max.x + pad
        && p.z >= b.min.z - pad && p.z <= b.max.z + pad;

    /// Mirror a point across the table's long (x) axis centre — used to seat the
    /// second sink at the opposite short end.
    public static Vector3 MirrorAcrossX(Vector3 p, float centerX)
        => new Vector3(2f * centerX - p.x, p.y, p.z);
}
