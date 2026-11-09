#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tools ▸ PharmaSynth ▸ Apply Text Theme: creates the theme (once), makes its font TextMesh
/// Pro's default so runtime-created text uses it, and bakes it into every text of
/// SampleScene and MainMenu, then saves both. Idempotent; to change the game's font, set
/// the theme's font and run this again. Run it on a scene freshly loaded from disk.
///
/// The balance's LCD readout (legacy UI.Text in a digital face) is an instrument display,
/// not game text, and is left alone.
public static class TextThemeBuilder
{
    public const string ThemeAsset = "Assets/PharmaSynth/Resources/" + TextTheme.ResourcePath + ".asset";
    public const string DefaultFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string LabScene = "Assets/Scenes/SampleScene.unity";
    const string CubeScene = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/PharmaSynth/Apply Text Theme")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TextTheme] exit Play mode first."); return; }
        var theme = EnsureTheme();
        SetTmpDefault(theme.font);

        var active = SceneManager.GetActiveScene();
        int n = Bake(active);
        foreach (var path in new[] { LabScene, CubeScene })
        {
            if (path == active.path) continue;
            var sc = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            n += Bake(sc);
            EditorSceneManager.CloseScene(sc, true);
        }
        Debug.Log($"[TextTheme] {theme.font.name} on every text; {n} text(s) changed.");
    }

    static int Bake(Scene scene)
    {
        int n = TextTheme.ApplyAll(scene);
        if (n > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        return n;
    }

    public static TextTheme EnsureTheme()
    {
        var theme = AssetDatabase.LoadAssetAtPath<TextTheme>(ThemeAsset);
        if (theme == null)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(ThemeAsset));
            theme = ScriptableObject.CreateInstance<TextTheme>();
            AssetDatabase.CreateAsset(theme, ThemeAsset);
        }
        if (theme.font == null)
        {
            theme.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DefaultFont);
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
        }
        return theme;
    }

    static void SetTmpDefault(TMP_FontAsset font)
    {
        var settings = TMP_Settings.instance;
        if (settings == null || font == null) return;
        var so = new SerializedObject(settings);
        var p = so.FindProperty("m_defaultFontAsset");
        if (p == null || p.objectReferenceValue == font) return;
        p.objectReferenceValue = font;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }
}
#endif
