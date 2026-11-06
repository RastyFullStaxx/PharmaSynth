using UnityEngine;

/// The plain "how big is this object" measure: the union of the renderer bounds of an
/// object and its children. Builders use it to place and size furniture and kits.
///
/// Not for task vessels at runtime: `ExperimentSceneBuilder.SolidWorldBounds` skips the
/// effect children (pour streams, fill layers, name labels) that drag a vessel's bounds
/// toward the floor.
public static class BoundsUtil
{
    /// Union of every renderer under `g`. With no renderer, a cube of `fallbackSize` metres
    /// at `g`'s position.
    public static Bounds OfRenderers(GameObject g, float fallbackSize = 0.1f, bool includeInactive = false)
    {
        var rs = g.GetComponentsInChildren<Renderer>(includeInactive);
        return rs.Length > 0 ? OfRenderers(rs) : new Bounds(g.transform.position, Vector3.one * fallbackSize);
    }

    /// Union of a non-empty renderer list.
    public static Bounds OfRenderers(Renderer[] rs)
    {
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
