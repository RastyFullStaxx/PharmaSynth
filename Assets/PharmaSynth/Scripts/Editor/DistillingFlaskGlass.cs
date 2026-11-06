#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// The DistillingFlask model (glTFast .glb) imports with a bare grey
/// metallic material, so it reads as a chrome flask instead of glass. This swaps
/// every mesh on the scene flask (and its prefab) to the SAME borosilicate glass
/// materials the ChemLab beakers use, so it matches the rest of the glassware.
/// Idempotent; run from SampleScene edit mode.
public static class DistillingFlaskGlass
{
    // The beakers' authored glass materials.
    const string OuterGuid = "5fa2d54e0de4b1844bd36402333542fe";   // GlassOuterMat
    const string InnerGuid = "ff04f2be4ce6d934a8624dfa3c34aa4b";   // GlassInnerMat

    [MenuItem("Tools/PharmaSynth/Upgrade Distilling Flask Glass")]
    public static void Upgrade()
    {
        if (Application.isPlaying) { Debug.LogWarning("[FlaskGlass] exit Play mode first."); return; }

        var outer = LoadMat(OuterGuid);
        if (outer == null) { Debug.LogError("[FlaskGlass] GlassOuterMat not found."); return; }
        var inner = LoadMat(InnerGuid) ?? outer;

        int done = 0;
        foreach (var it in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include))
        {
            if (it.itemId != "kit-distillingflask") continue;
            done += Apply(it.gameObject, outer, inner);
        }
        // Also fix the source prefab so future spawns are glass from the start.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/PharmaSynth/Art/Equipment/DistillationFlask/DistillingFlask.prefab");
        if (prefab != null)
        {
            var root = PrefabUtility.LoadPrefabContents(
                "Assets/PharmaSynth/Art/Equipment/DistillationFlask/DistillingFlask.prefab");
            Apply(root, outer, inner);
            PrefabUtility.SaveAsPrefabAsset(root,
                "Assets/PharmaSynth/Art/Equipment/DistillationFlask/DistillingFlask.prefab");
            PrefabUtility.UnloadPrefabContents(root);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[FlaskGlass] applied borosilicate glass to {done} flask renderer(s) + the prefab.</color>");
    }

    /// Swap every real MESH renderer (not the empty root host, not the liquid
    /// child) to the glass materials.
    static int Apply(GameObject go, Material outer, Material inner)
    {
        int n = 0;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r.name == "Liquid") continue;                     // the fill mesh keeps its shader
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;     // skip the empty root host
            int count = r.sharedMaterials.Length;
            var mats = new Material[Mathf.Max(1, count)];
            for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? outer : inner;
            r.sharedMaterials = mats;
            EditorUtility.SetDirty(r);
            n++;
        }
        return n;
    }

    static Material LoadMat(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
    }
}
#endif
