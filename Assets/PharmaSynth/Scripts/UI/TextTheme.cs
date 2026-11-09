using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// The game's one text style: the font every text uses (HUD, dialogue, labels, panels,
/// the cube room, the cinema) and the palette text is drawn in. Lives at
/// Resources/UI/TextTheme; swapping the font here changes every text in the game.
///
/// It is also TextMesh Pro's default font (TMP Settings), so text created at runtime
/// picks it up; every text already in a scene is baked by Tools ▸ PharmaSynth ▸ Apply Text
/// Theme and re-applied on each scene load, so nothing drifts back to another font.
[CreateAssetMenu(fileName = "TextTheme", menuName = "PharmaSynth/Text Theme")]
public class TextTheme : ScriptableObject
{
    public const string ResourcePath = "UI/TextTheme";

    public TMP_FontAsset font;

    [Header("Palette")]
    public Color body = new Color32(234, 246, 255, 255);      // default text
    public Color accent = new Color32(97, 214, 255, 255);     // the HUD cyan
    public Color muted = new Color32(159, 227, 255, 255);     // second lines, captions
    public Color warning = new Color32(255, 183, 77, 255);

    [Header("Cinema screen (TMP world size; 1 ≈ 8.5 cm, read from 2.1 m)")]
    [Min(0.3f)] public float cinemaTitleSize = 1.4f;
    [Min(0.3f)] public float cinemaCaptionSize = 0.9f;

    private static TextTheme s_current;
    public static TextTheme Current => s_current != null ? s_current : (s_current = Resources.Load<TextTheme>(ResourcePath));

    /// The theme font, or TMP's default when no theme is installed.
    public static TMP_FontAsset Font
    {
        get
        {
            var t = Current;
            return t != null && t.font != null ? t.font : TMP_Settings.defaultFontAsset;
        }
    }

    /// Put one text on the theme font. A material made for another font is replaced by the
    /// theme font's own; a custom material already made for the theme font is kept.
    public static bool Apply(TMP_Text t)
    {
        var f = Font;
        if (t == null || f == null || t.font == f) return false;
        t.font = f;
        t.fontSharedMaterial = f.material;
        return true;
    }

    public static int ApplyAll(Scene scene)
    {
        if (!scene.IsValid() || Font == null) return 0;
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true))
                if (Apply(t)) n++;
        return n;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplyAll(scene);
}
