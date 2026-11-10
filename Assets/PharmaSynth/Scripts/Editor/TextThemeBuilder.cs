#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tools ▸ PharmaSynth ▸ Apply Text Theme: creates the theme (once) and the game font's
/// TMP assets (Inter, regular + bold, dynamic, LiberationSans as glyph fallback), makes the
/// theme font TextMesh Pro's default so runtime-created text uses it, and bakes it into
/// every text of SampleScene and MainMenu, then saves both. Idempotent. A theme still on
/// the stock LiberationSans is moved to Inter; any other font set in the theme is kept, so
/// to change the game's font, set the theme's font and run this again. Run it on a scene
/// freshly loaded from disk.
///
/// The balance's LCD readout (legacy UI.Text in a digital face) is an instrument display,
/// not game text, and is left alone.
public static class TextThemeBuilder
{
    public const string ThemeAsset = "Assets/PharmaSynth/Resources/" + TextTheme.ResourcePath + ".asset";
    public const string StockFont = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
    const string FontFolder = "Assets/PharmaSynth/Art/Fonts/Inter/";
    public const string GameFont = FontFolder + "Inter SDF.asset";
    public const string GameFontBold = FontFolder + "Inter Bold SDF.asset";
    /// Every character the game's text uses (ASCII plus the symbols found in its data and
    /// code), baked once; the atlas is then locked, so nothing is built mid-frame on the
    /// Quest and Play never rewrites the asset. Anything else falls back to LiberationSans.
    public const string Prebaked = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
        + "§°±²³µ·»×÷üé–—•…₀₁₂₃₄₅₆₇₈₉⁺⁻ΔαβγμπΩ←↑→↓↔⇄⇌−≈≠≤≥✓✗▲▼▶▸►";
    const string LabScene = "Assets/Scenes/SampleScene.unity";
    const string CubeScene = "Assets/Scenes/MainMenu.unity";

    [MenuItem("Tools/PharmaSynth/Apply Text Theme")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TextTheme] exit Play mode first."); return; }
        var theme = EnsureTheme();
        var stock = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(StockFont);
        var bold = EnsureFontAsset(FontFolder + "Inter-Bold.ttf", GameFontBold, stock);
        var regular = EnsureFontAsset(FontFolder + "Inter-Regular.ttf", GameFont, stock);
        if (regular != null && bold != null)
        {
            var weights = regular.fontWeightTable;
            if (weights != null && weights.Length > 7 && weights[7].regularTypeface != bold)
            {
                weights[7].regularTypeface = bold;   // <b> and FontStyles.Bold draw real Inter Bold
                EditorUtility.SetDirty(regular);
            }
        }
        if (regular != null && (theme.font == null || theme.font == stock))
        {
            theme.font = regular;
            EditorUtility.SetDirty(theme);
        }
        AssetDatabase.SaveAssets();
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
        return theme;
    }

    /// A dynamic SDF font asset for one TTF, saved with its atlas and material as
    /// sub-assets; existing assets are returned untouched.
    static TMP_FontAsset EnsureFontAsset(string ttf, string assetPath, TMP_FontAsset fallback)
    {
        var fa = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (fa != null) return fa;
        var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (font == null) { Debug.LogError("[TextTheme] missing " + ttf); return null; }
        // 64 pt in a 1024 atlas holds the whole set in one page (90 pt overflowed to two).
        fa = TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,
                                           1024, 1024, AtlasPopulationMode.Dynamic, false);
        fa.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
        fa.TryAddCharacters(Prebaked, out string missing);
        if (!string.IsNullOrEmpty(missing)) Debug.Log("[TextTheme] " + fa.name + " has no glyph for: " + missing + " (LiberationSans covers them)");
        fa.atlasPopulationMode = AtlasPopulationMode.Static;
        if (fallback != null) fa.fallbackFontAssetTable = new List<TMP_FontAsset> { fallback };
        AssetDatabase.CreateAsset(fa, assetPath);
        foreach (var tex in fa.atlasTextures)
        {
            if (tex == null) continue;
            tex.name = fa.name + " Atlas";
            AssetDatabase.AddObjectToAsset(tex, fa);
        }
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        return fa;
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
