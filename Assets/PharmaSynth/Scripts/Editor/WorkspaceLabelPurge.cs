#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Removes the stale Methane-tutorial text labels that float over the center
/// workspace. They sit directly under WorldLabels (NOT under
/// MethaneStage), so toggling the Methane stage never hides them, and they are
/// orphaned at the x≈1.15 position. Pure scene
/// leftovers: no script references them. Landmark labels (PPE locker, fume
/// hood) and runtime DynLabel_* are kept. Idempotent + re-runnable.
public static class WorkspaceLabelPurge
{
    [MenuItem("Tools/PharmaSynth/Purge Stale Workspace Labels")]
    public static void Purge()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabelPurge] exit Play mode first."); return; }
        var root = GameObject.Find("WorldLabels");
        if (root == null) { Debug.LogWarning("[LabelPurge] no WorldLabels root found."); return; }

        var doomed = new List<GameObject>();
        foreach (Transform c in root.transform)
        {
            string n = c.name;
            // The methane station labels, the Begin button, and every prop
            // tag — but never the PPE-locker / fume-hood landmark labels or any
            // runtime DynLabel_*.
            if (n.StartsWith("Label_Station_") || n.StartsWith("Tag_") || n == "Label_BeginButton")
                doomed.Add(c.gameObject);
        }

        foreach (var go in doomed)
        {
            Debug.Log("[LabelPurge] removing " + go.name);
            Object.DestroyImmediate(go);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[LabelPurge] removed {doomed.Count} stale workspace labels; " +
                  (root.transform.childCount) + " labels remain (landmarks kept).");
    }
}
#endif
