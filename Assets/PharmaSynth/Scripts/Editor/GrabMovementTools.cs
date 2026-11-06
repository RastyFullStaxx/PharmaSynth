#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Applies the GrabTuning velocity-tracked profile to every grabbable so held
/// items collide with the world (without it, props can be pushed through
/// walls/floor). Covers the SceneAssetLibrary prefabs (persisted) AND every
/// XRGrabInteractable in the open scene(s) (catches instance overrides, stools,
/// shelf bottles). Idempotent — re-running reports 0 changes.
public static class GrabMovementTools
{
    [MenuItem("Tools/PharmaSynth/Wire Grab Collision (VelocityTracking)")]
    public static void Wire()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[GrabMovementTools] exit Play mode first.");
            return;
        }

        int prefabsChanged = 0, prefabGrabs = 0;
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null && lib.prefabs != null)
        {
            foreach (var prefab in lib.prefabs)
            {
                if (prefab == null) continue;
                bool dirty = false;
                foreach (var g in prefab.GetComponentsInChildren<XRGrab>(true))
                    if (GrabTuning.Apply(g)) { dirty = true; prefabGrabs++; }
                if (dirty) { EditorUtility.SetDirty(prefab); prefabsChanged++; }
            }
        }
        else Debug.LogWarning("[GrabMovementTools] SceneAssetLibrary.asset not found — prefab pass skipped.");

        int sceneChanged = 0, sceneTotal = 0;
        foreach (var g in Object.FindObjectsByType<XRGrab>(FindObjectsInactive.Include))
        {
            sceneTotal++;
            if (GrabTuning.Apply(g))
            {
                // Prefab instances lose direct field writes on save unless recorded.
                PrefabUtility.RecordPrefabInstancePropertyModifications(g);
                EditorUtility.SetDirty(g);
                sceneChanged++;
            }
        }

        AssetDatabase.SaveAssets();
        if (sceneChanged > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log($"[GrabMovementTools] velocity-tracked: {prefabGrabs} grabs on {prefabsChanged} prefabs, " +
                  $"{sceneChanged}/{sceneTotal} scene grabs updated.");
    }
}
#endif
