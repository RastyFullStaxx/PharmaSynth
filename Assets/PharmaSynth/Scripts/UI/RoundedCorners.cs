using UnityEngine;
using UnityEngine.SceneManagement;
using UImage = UnityEngine.UI.Image;

/// Soft rounded corners for every UI panel and button, matching the item name tags.
///
/// A sprite-less Image draws a hard rectangle, so each one gets the shared 9-sliced
/// rounded sprite (Resources/UI/RoundedPanel), with a corner radius scaled to that
/// Image's own size: panels get a generous curve, buttons a smaller one, thin bars and
/// slider handles become pills. Real art (portraits, icons) and fill bars are left alone.
///
/// Baked into both scenes by Tools ▸ PharmaSynth ▸ Round UI Corners, and applied again on
/// every scene load, so a panel a builder menu recreates can never come back square.
public static class RoundedCorners
{
    public const string ResourcePath = "UI/RoundedPanel";
    public const int SpritePixels = 128, BorderPixels = 48;
    public const float MaxRadius = 28f;
    /// A stripe thinner than this that runs along a whole panel side is an accent line:
    /// it is shortened so its ends stay inside the panel's curved corners.
    public const float StripeMax = 16f;

    /// Corner radius, in the Image's own rect units. Never more than half the short side
    /// (so thin bars are full pills), rising with size up to MaxRadius.
    public static float RadiusFor(Vector2 size)
    {
        float m = Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y));
        if (m <= 0f) return 8f;   // not laid out yet; the scene-load pass corrects it
        return Mathf.Min(m * 0.5f, Mathf.Clamp(m * 0.25f + 6f, 2f, MaxRadius));
    }

    /// The Image.pixelsPerUnitMultiplier that draws the sprite's corner at `radius` units.
    public static float Multiplier(float radius, float canvasPixelsPerUnit, float spritePixelsPerUnit)
        => BorderPixels * canvasPixelsPerUnit / (spritePixelsPerUnit * Mathf.Max(radius, 0.01f));

    /// Alpha of the sprite texture at pixel (x, y): a white rounded square, anti-aliased.
    public static float Alpha(int x, int y) => Mathf.Clamp01(BorderPixels - CornerDistance(x, y) + 0.5f);

    /// Alpha of the ring sprite: only a band RingPixels wide along the rounded edge.
    public static float RingAlpha(int x, int y)
    {
        float d = CornerDistance(x, y);
        return Mathf.Clamp01(BorderPixels - d + 0.5f) * Mathf.Clamp01(d - (BorderPixels - RingPixels) + 0.5f);
    }

    /// Ring thickness in sprite pixels: about 3 units on a full-radius panel.
    public const int RingPixels = 5;

    private static float CornerDistance(int x, int y)
    {
        float cx = Mathf.Clamp(x + 0.5f, BorderPixels, SpritePixels - BorderPixels);
        float cy = Mathf.Clamp(y + 0.5f, BorderPixels, SpritePixels - BorderPixels);
        return Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
    }

    public const string RingResourcePath = "UI/RoundedRing";
    public const string EdgeName = "RoundEdge";

    private static Sprite s_sprite, s_ring;
    public static Sprite Sprite => s_sprite != null ? s_sprite : (s_sprite = Resources.Load<Sprite>(ResourcePath));
    public static Sprite Ring => s_ring != null ? s_ring : (s_ring = Resources.Load<Sprite>(RingResourcePath));

    /// Should this Image get the rounded sprite? Sprite-less ones and the stock UISprite,
    /// visible, and not a fill bar.
    public static bool Wants(UImage img, Sprite rounded)
    {
        if (img == null || img.color.a <= 0f || img.type == UImage.Type.Filled) return false;
        return img.sprite == null || img.sprite == rounded || img.sprite.name == "UISprite";
    }

    public static bool Apply(UImage img, Sprite rounded)
    {
        if (rounded == null || !Wants(img, rounded)) return false;
        img.sprite = rounded;
        img.type = UImage.Type.Sliced;
        img.fillCenter = true;
        var rt = img.rectTransform;
        InsetStripe(rt);
        float canvasPpu = img.canvas != null ? img.canvas.referencePixelsPerUnit : 100f;
        img.pixelsPerUnitMultiplier = Multiplier(RadiusFor(rt.rect.size), canvasPpu, rounded.pixelsPerUnit);
        RoundOutline(img);
        return true;
    }

    /// An Outline effect draws four diagonal copies, which line up on a square but leave
    /// jogs on a curve. Replace it with a ring child in the outline's colour, drawn first so
    /// text stays on top.
    private static void RoundOutline(UImage img)
    {
        var outline = img.GetComponent<UnityEngine.UI.Outline>();
        var edgeT = img.transform.Find(EdgeName);
        if ((outline == null || !outline.enabled) && edgeT == null) return;
        var ring = Ring;
        if (ring == null) return;
        UImage edge;
        if (edgeT == null)
        {
            var go = new GameObject(EdgeName, typeof(RectTransform), typeof(UImage));
            go.layer = img.gameObject.layer;
            go.transform.SetParent(img.transform, false);
            edge = go.GetComponent<UImage>();
        }
        else edge = edgeT.GetComponent<UImage>();
        if (outline != null && outline.enabled)
        {
            edge.color = outline.effectColor;
            outline.enabled = false;
        }
        var ert = edge.rectTransform;
        ert.anchorMin = Vector2.zero; ert.anchorMax = Vector2.one;
        ert.offsetMin = ert.offsetMax = Vector2.zero;
        ert.SetAsFirstSibling();
        edge.sprite = ring;
        edge.type = UImage.Type.Sliced;
        edge.raycastTarget = false;
        edge.pixelsPerUnitMultiplier = img.pixelsPerUnitMultiplier;
    }

    /// Shorten an accent stripe that spans a panel's whole side by that panel's radius at
    /// each end. Absolute offsets, so applying it twice changes nothing.
    private static void InsetStripe(RectTransform rt)
    {
        var parent = rt.parent as RectTransform;
        if (parent == null) return;
        Vector2 ps = parent.rect.size;
        if (Mathf.Min(ps.x, ps.y) < 100f) return;   // only along real panels
        float r = RadiusFor(ps);
        bool spansX = rt.anchorMin.x == 0f && rt.anchorMax.x == 1f && rt.anchorMin.y == rt.anchorMax.y;
        bool spansY = rt.anchorMin.y == 0f && rt.anchorMax.y == 1f && rt.anchorMin.x == rt.anchorMax.x;
        if (spansX && rt.rect.height <= StripeMax)
        { rt.offsetMin = new Vector2(r, rt.offsetMin.y); rt.offsetMax = new Vector2(-r, rt.offsetMax.y); }
        else if (spansY && rt.rect.width <= StripeMax)
        { rt.offsetMin = new Vector2(rt.offsetMin.x, r); rt.offsetMax = new Vector2(rt.offsetMax.x, -r); }
    }

    public static int ApplyAll(Scene scene)
    {
        var rounded = Sprite;
        if (rounded == null || !scene.IsValid()) return 0;
        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var img in root.GetComponentsInChildren<UImage>(true))
                if (Apply(img, rounded)) n++;
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
