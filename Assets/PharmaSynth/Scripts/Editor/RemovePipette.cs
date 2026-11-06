#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Drop the modern mechanical pipette — the Dropper
/// (drops) + graduated cylinder (ml) already cover its manuscript role. Removes
/// the scene instance, the SceneAssetLibrary registration, and the generated
/// prefab. The raw MechanicalPipette model pack is left on disk (harmless).
/// Idempotent; safe to run once.
public static class RemovePipette
{
    const string LibraryPath = "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset";
    const string PipettePrefabPath = "Assets/PharmaSynth/Art/Equipment/MechanicalPipette/Pipette.prefab";

    [MenuItem("Tools/PharmaSynth/Remove Pipette (W5.12)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RemovePipette] exit Play mode first."); return; }

        // 1) Scene instance(s).
        int scene = 0;
        foreach (var it in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include))
            if (it.itemId == "kit-pipette" || PhysicsAudit.PrefabNameFor(it.gameObject) == "Pipette")
            { Object.DestroyImmediate(it.gameObject); scene++; }

        // 2) Unregister from the library.
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(LibraryPath);
        int unreg = 0;
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var arr = so.FindProperty("prefabs");
            for (int i = arr.arraySize - 1; i >= 0; i--)
            {
                var g = arr.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (g != null && g.name == "Pipette") { arr.DeleteArrayElementAtIndex(i); unreg++; }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(lib);
        }

        // 3) Delete the generated prefab (keep the raw model pack).
        bool delPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PipettePrefabPath) != null
                         && AssetDatabase.DeleteAsset(PipettePrefabPath);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#4CD07D>[RemovePipette] removed {scene} scene instance(s), {unreg} library entry, "
                  + $"prefab deleted: {delPrefab}. Dropper + graduated cylinder cover its role.</color>");
    }
}
#endif
