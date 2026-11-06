#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Re-activate every bench item that was hidden (client rule: ALL tools
/// and reagents are present across ALL experiments — nothing is ever hidden or
/// removed per-experiment). Undoes any accidental deactivation. Idempotent.
public static class RestoreBenchItems
{
    [MenuItem(SceneRepairs.MenuRoot + "Restore All Bench Items")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Restore] exit Play mode first."); return; }

        int restored = 0;
        var names = new List<string>();
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li == null) continue;
            var go = li.gameObject;
            if (!go.activeSelf)
            {
                go.SetActive(true);
                names.Add(go.name);
                EditorUtility.SetDirty(go);
                restored++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[Restore] Re-activated {restored} bench item(s) — all tools & reagents are back "
                  + "and present, as the lab setting requires.</color>"
                  + (names.Count > 0 ? "\nRestored: " + string.Join(", ", names) : ""));
    }
}
#endif
