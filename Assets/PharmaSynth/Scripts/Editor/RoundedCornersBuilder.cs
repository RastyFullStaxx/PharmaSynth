#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tools ▸ PharmaSynth ▸ Round UI Corners: writes the shared rounded and ring sprites (once) and
/// bakes it into every panel and button of SampleScene and MainMenu, then saves both.
/// Idempotent. Run it on a scene freshly loaded from disk, never straight after a suite
/// or simulator run (those drain the bench, and this saves).
public static class RoundedCornersBuilder
{
    public const string SpriteAsset = "Assets/PharmaSynth/Resources/" + RoundedCorners.ResourcePath + ".png";
    public const string RingAsset = "Assets/PharmaSynth/Resources/" + RoundedCorners.RingResourcePath + ".png";
    const string MainMenu = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/PharmaSynth/Round UI Corners")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RoundedCorners] exit Play mode first."); return; }
        EnsureSprite(SpriteAsset, RoundedCorners.Alpha);
        EnsureSprite(RingAsset, RoundedCorners.RingAlpha);
        int total = 0;
        var active = SceneManager.GetActiveScene();
        total += Bake(active);
        if (active.path != MainMenu)
        {
            var menu = EditorSceneManager.OpenScene(MainMenu, OpenSceneMode.Additive);
            total += Bake(menu);
            EditorSceneManager.CloseScene(menu, true);
        }
        Debug.Log("[RoundedCorners] rounded " + total + " panels and buttons.");
    }

    static int Bake(Scene scene)
    {
        int n = RoundedCorners.ApplyAll(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return n;
    }

    static void EnsureSprite(string asset, System.Func<int, int, float> alpha)
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(asset) != null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(asset));
        int s = RoundedCorners.SpritePixels;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha(x, y)));
        File.WriteAllBytes(asset, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(asset);
        var imp = (TextureImporter)AssetImporter.GetAtPath(asset);
        imp.textureType = TextureImporterType.Sprite;
        imp.spriteImportMode = SpriteImportMode.Single;
        imp.spritePixelsPerUnit = 100f;
        imp.spriteBorder = Vector4.one * RoundedCorners.BorderPixels;
        imp.mipmapEnabled = false;
        imp.alphaIsTransparency = true;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.filterMode = FilterMode.Bilinear;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        var st = new TextureImporterSettings();
        imp.ReadTextureSettings(st);
        st.spriteMeshType = SpriteMeshType.FullRect;   // sliced drawing needs the full quad
        imp.SetTextureSettings(st);
        imp.SaveAndReimport();
    }
}
#endif
