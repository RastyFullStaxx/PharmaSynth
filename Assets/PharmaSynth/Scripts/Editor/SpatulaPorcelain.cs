#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// The manuscript specifies a PORCELAIN spatula, but the Spatula prefab uses the
/// shared metal EquipmentMat and reads as steel. This
/// finds every Spatula prefab instance in the scene (by SOURCE prefab, so it
/// catches hand-placed/renamed copies like "Eq_Spatula" that carry no LabItem),
/// applies the pack's white PorcelainMat, renames + labels it "Porcelain
/// Spatula", gives it the full interaction wiring, and fixes the source prefab.
/// Via the Mishandling table it clinks like ceramic instead of clattering
/// like metal. Idempotent.
public static class SpatulaPorcelain
{
    const string PorcelainMatPath = "Assets/PharmaSynth/Art/Equipment/ChemLabEquipment/Materials/PorcelainMat.mat";
    const string SpatulaPrefabPath = "Assets/PharmaSynth/Art/Equipment/ChemLabEquipment/Prefabs/Spatula.prefab";

    [MenuItem("Tools/PharmaSynth/Make Spatula Porcelain")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Spatula] exit Play mode first."); return; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(PorcelainMatPath);
        if (mat == null) { Debug.LogError("[Spatula] PorcelainMat not found."); return; }
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);

        int scene = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = t.gameObject;
            if (go.GetComponent<MeshRenderer>() == null && go.GetComponentInChildren<MeshRenderer>(true) == null) continue;
            if (PhysicsAudit.PrefabNameFor(go) != "Spatula") continue;      // by SOURCE prefab, LabItem or not
            if (go.transform.parent != null && PhysicsAudit.PrefabNameFor(go.transform.parent.gameObject) == "Spatula") continue; // child mesh, not the root

            ApplyMat(go, mat);
            var item = go.GetComponent<LabItem>() ?? go.AddComponent<LabItem>();
            item.itemId = "kit-spatula"; item.displayName = "Porcelain Spatula";
            PhysicsAudit.WireSceneItem(go, "Spatula", runner);              // grab/physics/respawn/impact/ceramic-clink/home
            var pl = go.GetComponent<ProximityLabel>() ?? go.AddComponent<ProximityLabel>();
            pl.SetLabel("Porcelain Spatula", 1.4f);
            // Preserve the "Eq_" naming convention when renaming.
            go.name = go.name.StartsWith("Eq_") ? "Eq_PorcelainSpatula" : "PorcelainSpatula";
            EditorUtility.SetDirty(go);
            scene++;
        }

        // Fix the source prefab so future spawns/drag-ins are porcelain.
        var root = PrefabUtility.LoadPrefabContents(SpatulaPrefabPath);
        if (root != null) { ApplyMat(root, mat); PrefabUtility.SaveAsPrefabAsset(root, SpatulaPrefabPath); PrefabUtility.UnloadPrefabContents(root); }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[Spatula] {scene} scene spatula(s) + the prefab are now white porcelain "
                  + "(clink, not metal), labelled \"Porcelain Spatula\".</color>");
    }

    static void ApplyMat(GameObject go, Material mat)
    {
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == "Liquid") continue;
            var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
        }
    }
}
#endif
