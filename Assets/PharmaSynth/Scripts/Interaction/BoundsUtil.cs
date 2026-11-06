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

    /// Renderer list for bounds that are measured every frame. The bounds stay live (a
    /// renderer's bounds follow it as it moves); only the LIST is cached, re-read when the
    /// hierarchy changes or every `refreshSeconds` in Play mode, so a per-frame check no
    /// longer allocates a fresh array. Edit mode always re-reads.
    public sealed class RendererCache
    {
        private Transform _root;
        private int _count = -1;
        private float _readAt = float.NegativeInfinity;
        private Renderer[] _rs = new Renderer[0];
        private readonly float _refreshSeconds;

        public RendererCache(float refreshSeconds = 0.25f) { _refreshSeconds = refreshSeconds; }

        public Renderer[] Of(Transform root)
        {
            if (root == null) return new Renderer[0];
            if (!Application.isPlaying || root != _root || root.hierarchyCount != _count
                || Time.time - _readAt >= _refreshSeconds)
            {
                _root = root; _count = root.hierarchyCount; _readAt = Time.time;
                _rs = root.GetComponentsInChildren<Renderer>();
            }
            return _rs;
        }
    }

    /// Union of the live renderers in `rs` (destroyed entries skipped). False when none.
    public static bool TryUnion(Renderer[] rs, out Bounds b)
    {
        b = default; bool any = false;
        for (int i = 0; i < rs.Length; i++)
        {
            if (rs[i] == null) continue;
            if (!any) { b = rs[i].bounds; any = true; } else b.Encapsulate(rs[i].bounds);
        }
        return any;
    }

    /// Union of a non-empty renderer list.
    public static Bounds OfRenderers(Renderer[] rs)
    {
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
