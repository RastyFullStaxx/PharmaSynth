#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Tools ▸ PharmaSynth ▸ Fit Conical Liquids: gives every conical flask in the open scene
/// the lathed LiquidShape mesh (saved as an asset so the scene can keep it) in place of
/// the straight cylinder that poked through the glass. Idempotent; saves the scene, so
/// run it on a scene freshly loaded from disk.
public static class ConicalLiquidFitter
{
    public const string MeshAsset = "Assets/PharmaSynth/Art/Generated/ConicalLiquid.asset";

    [MenuItem("Tools/PharmaSynth/Fit Conical Liquids")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ConicalLiquid] exit Play mode first."); return; }
        var mesh = EnsureMesh();
        int n = 0;
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
        {
            if (!LiquidShape.IsConical(lp.name) || lp.mainRenderer == null) continue;
            var mf = lp.mainRenderer.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == mesh) continue;
            Undo.RecordObject(mf, "Fit Conical Liquids");
            mf.sharedMesh = mesh;
            n++;
        }
        if (n > 0)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        Debug.Log($"[ConicalLiquid] {n} conical flask liquid(s) fitted.");
    }

    public static Mesh EnsureMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshAsset);
        if (mesh != null) return mesh;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MeshAsset));
        mesh = LiquidShape.Lathe(LiquidShape.ConicalProfile, 24, LiquidShape.ConicalMeshName);
        AssetDatabase.CreateAsset(mesh, MeshAsset);
        AssetDatabase.SaveAssets();
        return mesh;
    }
}
#endif
