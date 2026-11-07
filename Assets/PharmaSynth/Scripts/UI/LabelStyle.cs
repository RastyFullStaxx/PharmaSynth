using UnityEngine;

/// The look of an item's name tag: dark glass panel, cyan edge, a pointer line to the
/// item, and two lines of text (the name, then the contents in a softer cyan).
///
/// The sprites are generated in code, so there is no art asset to import or strip, and
/// the panel keeps a constant edge at any size (9-sliced). Pure parts are suite-pinned.
public static class LabelStyle
{
    public static readonly Color32 PanelFill = new Color32(14, 36, 48, 210);   // dark glass, 82% opaque
    public static readonly Color32 Edge = new Color32(97, 214, 255, 255);      // the HUD cyan
    public const string DetailColour = "#9FE3FF";                              // second line

    /// Panel padding around the text, in the tag's local units.
    public static readonly Vector2 Padding = new Vector2(2.2f, 1.2f);
    /// Pointer thickness and anchor dot size, in metres.
    public const float LeaderWidth = 0.0016f;
    public const float DotSize = 0.009f;
    /// Below this length the tag already sits on its item and draws no pointer.
    public const float MinLeader = 0.025f;

    /// The tag's display text: everything before the first em dash (or line break) is
    /// the name; the rest becomes the softer second line. Text with neither stays one
    /// line. Dashes are removed after the split, so no dash reaches the screen.
    public static string TagText(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return raw;
        int dash = raw.IndexOf(" — ", System.StringComparison.Ordinal);
        int brk = raw.IndexOf('\n');
        int cut; int skip;
        if (dash > 0 && (brk < 0 || dash < brk)) { cut = dash; skip = 3; }
        else if (brk > 0) { cut = brk; skip = 1; }
        else return GlyphSafe.Sanitize(raw);
        string name = raw.Substring(0, cut).Trim();
        string rest = raw.Substring(cut + skip).Trim();
        if (rest.Length == 0) return GlyphSafe.Sanitize(name);
        return GlyphSafe.Sanitize(name) + "\n<size=82%><color=" + DetailColour + ">"
             + GlyphSafe.Sanitize(rest) + "</color></size>";
    }

    private static Sprite s_panel, s_pixel, s_dot;

    /// 9-sliced rounded rectangle: dark glass fill with a thin cyan edge.
    public static Sprite Panel => s_panel != null ? s_panel : (s_panel = BuildPanel());
    /// A white pixel, stretched into the pointer line.
    public static Sprite Pixel => s_pixel != null ? s_pixel : (s_pixel = BuildPixel());
    /// A soft round dot for the anchor on the item.
    public static Sprite Dot => s_dot != null ? s_dot : (s_dot = BuildDot());

    public const int PanelSize = 64, PanelRadius = 18, PanelEdge = 2;

    /// Alpha-weighted colour of the panel texture at pixel (x, y): transparent outside the
    /// rounded corners, the cyan edge on the rim, the dark fill inside.
    public static Color32 PanelPixel(int x, int y)
    {
        float cx = Mathf.Clamp(x + 0.5f, PanelRadius, PanelSize - PanelRadius);
        float cy = Mathf.Clamp(y + 0.5f, PanelRadius, PanelSize - PanelRadius);
        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
        float inside = Mathf.Clamp01(PanelRadius - d + 0.5f);            // anti-aliased outer edge
        if (inside <= 0f) return new Color32(0, 0, 0, 0);
        float rim = Mathf.Clamp01(d - (PanelRadius - PanelEdge) + 0.5f);  // 1 on the edge band
        Color c = Color.Lerp(PanelFill, Edge, rim);
        c.a *= inside;
        return c;
    }

    private static Sprite BuildPanel()
    {
        var tex = new Texture2D(PanelSize, PanelSize, TextureFormat.RGBA32, false)
        { name = "LabelPanel", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < PanelSize; y++)
            for (int x = 0; x < PanelSize; x++) tex.SetPixel(x, y, PanelPixel(x, y));
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, PanelSize, PanelSize), new Vector2(0.5f, 0.5f), 32f, 0,
                              SpriteMeshType.FullRect, new Vector4(PanelRadius, PanelRadius, PanelRadius, PanelRadius));
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    private static Sprite BuildPixel()
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "LabelPixel", hideFlags = HideFlags.DontSave };
        Color32 w = Color.white;
        tex.SetPixels32(new[] { w, w, w, w });
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }

    private static Sprite BuildDot()
    {
        const int n = 32;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "LabelDot", filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n * 0.5f, n * 0.5f));
                float a = Mathf.Clamp01(n * 0.5f - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        var s = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        s.hideFlags = HideFlags.DontSave;
        return s;
    }
}
