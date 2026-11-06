#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Edit-mode helper ("let me select + reposition the stools in the
/// editor, not in Play mode"). The stools sit tucked under the tables, so clicking
/// them in a crowded Scene view is fiddly. This selects all of them in one go — then
/// move them with the transform gizmo and run Re-Home Scene Items to make it stick.
///
/// Tools ▸ PharmaSynth ▸ Select Stools (edit mode).
public static class StoolTools
{
    [MenuItem("Tools/PharmaSynth/Select Stools")]
    public static void SelectStools()
    {
        var list = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name.ToLower();
            if (n.Contains("stool") || n.Contains("chair")) list.Add(t.gameObject);
        }

        // Stools with Scene-view PICKING disabled (the pointer toggle in the
        // Hierarchy) can't be clicked in the Scene view. Turn picking back on and
        // unhide them so they're directly click-selectable.
        foreach (var go in list)
        {
            SceneVisibilityManager.instance.EnablePicking(go, true);
            SceneVisibilityManager.instance.Show(go, true);
        }

        Selection.objects = list.ToArray();
        if (list.Count > 0 && SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.FrameSelected();

        Debug.Log("<color=#4CD07D>[Stools] picking re-enabled + selected " + list.Count + " stool(s).</color> " +
                  "You can now click each one in the Scene view and move it with the gizmo (W). " +
                  "Then run Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current) and save the scene.");
    }
}
#endif
