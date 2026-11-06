#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Delete the wooden splint prop.
/// "splint" appears NOWHERE in the client manuscript: every combustion/flame test
/// is run with a "lighted matchstick" (Exp 3). The methane gas test already fires off a
/// lit Matchstick (MethaneApparatusRig.SplintShouldFire checks Matchstick), so the
/// splint prop is redundant. The method names keep the "splint" wording (suite-pinned);
/// only the prop goes. Idempotent.
public static class RemoveLitSplint
{
    [MenuItem("Tools/PharmaSynth/Remove Lit Splint Prop")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Splint] exit Play mode first."); return; }

        int removed = 0;
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li == null || li.itemId != "lit-splint") continue;
            Object.DestroyImmediate(li.gameObject);
            removed++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[Splint] Removed {removed} lit-splint prop(s). The gas test uses a lit MATCH "
                  + "(as the manuscript specifies).</color>"
                  + (removed == 0 ? "  (none found — already gone.)" : ""));
    }
}
#endif
