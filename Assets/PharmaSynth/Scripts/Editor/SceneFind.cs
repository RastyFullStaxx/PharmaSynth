#if UNITY_EDITOR
using UnityEditor.SceneManagement;
using UnityEngine;

/// Find scene objects by exact name from editor builders. Two lookups because they answer
/// different questions:
/// - `AnyByName`: any loaded object, active or not. The order is whatever Unity returns,
///   so use it only for names that are unique in the scene.
/// - `InActiveScene`: the active scene only, roots first then depth-first. Deterministic,
///   so the first of several same-named objects is always the same one.
public static class SceneFind
{
    public static GameObject AnyByName(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t != null && t.name == name) return t.gameObject;
        return null;
    }

    public static Transform AnyTransform(string name)
    {
        var g = AnyByName(name);
        return g != null ? g.transform : null;
    }

    public static GameObject InActiveScene(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = Deep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    /// Depth-first search below `t` (not `t` itself).
    public static Transform Deep(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name == name) return c;
            var deep = Deep(c, name);
            if (deep != null) return deep;
        }
        return null;
    }
}
#endif
