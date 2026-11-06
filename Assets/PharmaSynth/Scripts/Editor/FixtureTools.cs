#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Edit-mode helper so the sinks, speaker, tables and shelves can be
/// repositioned by hand in the editor. Two things block
/// Scene-view clicks:
///   1. The Environment furniture (tables, wall cabinets, wash-table sinks) has
///      PICKING DISABLED (the pointer toggle in the Hierarchy), as do the stools.
///   2. Some fixtures (the LabSpeaker, and the shelf ROOTS that sit at the origin
///      with their meshes parented elsewhere) have no click target, so a click
///      selects a child prop instead of the movable unit.
/// This re-enables picking on every fixture, ensures a click collider where one
/// is missing, and selects them all so they show in the Hierarchy — click the
/// one you want in the Scene view and move it with the gizmo (W), then Ctrl-S.
///
/// Tools ▸ PharmaSynth ▸ Select Movable Furniture (edit mode).
public static class FixtureTools
{
    /// Top-level group roots that hold props parented under them — selecting the
    /// ROOT lets you move the whole unit (a mesh click would grab a child prop).
    static readonly HashSet<string> Roots = new HashSet<string>
    {
        "ReagentShelf", "EquipmentShelf", "ReagentCabinets", "BenchApparatus",
        "DynamicStage", "MethaneStage", "LabSpeaker",
    };

    [MenuItem("Tools/PharmaSynth/Select Movable Furniture")]
    public static void SelectFurniture()
    {
        var list = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!IsFixture(t)) continue;

            // Re-enable Scene-view picking + unhide so a direct click selects it.
            SceneVisibilityManager.instance.EnablePicking(t.gameObject, true);
            SceneVisibilityManager.instance.Show(t.gameObject, true);

            // No collider anywhere → nothing to click in the Scene view. Give the
            // fixture a bounds-fitted box so it's directly selectable.
            if (t.GetComponentInChildren<Collider>() == null)
                AddClickCollider(t.gameObject);

            list.Add(t.gameObject);
        }

        Selection.objects = list.ToArray();
        Debug.Log("<color=#4CD07D>[Furniture] picking re-enabled + selected " + list.Count +
                  " fixture(s) (tables, sinks, cabinets, shelves, speaker).</color> " +
                  "Click the one you want in the Scene view (or pick it in the Hierarchy — they're all " +
                  "selected now), move it with the gizmo (W), then Ctrl-S to save the scene.");
    }

    static bool IsFixture(Transform t)
    {
        // UI panels (tablet/grade screen/holo) never count, even if named "Counter".
        if (t.GetComponentInParent<Canvas>() != null) return false;

        string n = t.name.ToLower();

        // Furniture directly under the Environment root: tables, wall cabinets,
        // sinks, the printer and the AC unit.
        if (t.parent != null && t.parent.name == "Environment")
        {
            bool table = (n.Contains("table") && !n.Contains("tablet")) || n.Contains("bench")
                         || n.Contains("counter") || n.Contains("island") || n.Contains("desk");
            bool cabinet = n.Contains("cabinet");
            bool sink = n.Contains("wash") || n.Contains("sink");
            bool appliance = n.StartsWith("printe") || n == "ac" || n.Contains("aircon");
            if (table || cabinet || sink || appliance) return true;
        }

        // Prop-holding group roots (move the whole unit, not a child prop).
        if (Roots.Contains(t.name)) return true;

        // The music speaker, wherever it sits.
        if (t.GetComponent<MusicSpeaker>() != null) return true;

        return false;
    }

    /// A non-trigger BoxCollider fitted to the renderer bounds, added on the root
    /// so the whole fixture is one clickable target.
    static void AddClickCollider(GameObject go)
    {
        var rends = go.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) return;   // nothing to fit / click (empty group root)
        var box = go.AddComponent<BoxCollider>();
        var b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        box.center = go.transform.InverseTransformPoint(b.center);
        Vector3 ls = go.transform.lossyScale;
        box.size = new Vector3(
            b.size.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            b.size.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
            b.size.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
    }
}
#endif
