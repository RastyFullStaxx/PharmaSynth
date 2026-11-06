using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The floating info card the hover-inspector shows when you point at a piece of
/// equipment, a reagent bottle or an NPC. It smoothly fades and
/// scales in/out and rides a comfortable distance in front of you along the line to
/// the pointed object, always billboarded and readable. Pure easing helpers so the
/// animation curve is unit-testable; HoverInspector feeds it entries.
public class HoverInfoPanel : MonoBehaviour
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform card;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text catText;
    [SerializeField] private Image accentBar;
    [SerializeField] private Transform head;

    [Header("Feel")]
    [SerializeField] private float appearSpeed = 11f;      // alpha/scale lerp rate (per second)
    [SerializeField] private float nearDistance = 0.5f;    // preferred minimum comfortable distance
    [SerializeField] private float farDistance = 1.1f;     // never farther than this (stays readable)
    [SerializeField] private float standoff = 0.4f;        // sit this far IN FRONT of the target so it's never occluded by it

    private bool _showing;
    private Vector3 _anchor;
    private float _t;                                       // 0 hidden → 1 shown

    public void Bind(CanvasGroup g, RectTransform c, TMP_Text title, TMP_Text body, TMP_Text cat, Image accent, Transform headT)
    { group = g; card = c; titleText = title; bodyText = body; catText = cat; accentBar = accent; head = headT; }

    /// Smoothstep so the fade eases at both ends instead of ramping linearly.
    public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    /// Where to place the card along the sightline: a readable stand-off in front of
    /// the target, but ALWAYS at least 0.12 m nearer the camera than the struck
    /// surface (floor 0.35 m) so the object can never occlude the card. Pure/tested.
    public static float PlaceDistance(float dist, float near, float far, float standoff)
    {
        float place = Mathf.Clamp(dist - standoff, near, far);
        return Mathf.Min(place, Mathf.Max(0.35f, dist - 0.12f));
    }

    /// Category accent colour (cyan equipment / amber reagent / green person).
    public static Color AccentFor(LabInfoCategory c)
        => c == LabInfoCategory.Reagent ? new Color(1f, 0.72f, 0.28f)
         : c == LabInfoCategory.Person ? new Color(0.42f, 0.86f, 0.55f)
         : new Color(0.38f, 0.82f, 1f);

    private static readonly string[] CategoryTag = { "EQUIPMENT", "REAGENT", "LAB GUIDE" };

    public void Show(LabInfoEntry e, Vector3 worldAnchor)
    {
        if (e == null) { Hide(); return; }
        if (titleText != null) titleText.text = GlyphSafe.Sanitize(e.Title);
        if (bodyText != null) bodyText.text = GlyphSafe.Sanitize(e.Body);
        if (catText != null) { catText.text = Tag(e.Category); catText.color = AccentFor(e.Category); }
        if (accentBar != null) accentBar.color = AccentFor(e.Category);
        _anchor = worldAnchor;
        _showing = true;
    }

    public void Hide() => _showing = false;

    public bool IsVisible => _t > 0.01f;

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        _t = Mathf.MoveTowards(_t, _showing ? 1f : 0f, appearSpeed * dt);
        float e = Ease(_t);
        if (group != null) group.alpha = e;
        if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, e);

        if (_t <= 0.001f) return;                          // fully hidden — skip placement
        if (head == null) { var c = Camera.main; if (c != null) head = c.transform; else return; }

        // Ride a comfortable distance in front of the player along the sightline to
        // the pointed object — but ALWAYS closer to the camera than the object itself,
        // so the object (e.g. Pharmee's body) can never occlude the card. A distant
        // shelf item still lands at a readable ~1 m.
        Vector3 dir = _anchor - head.position;
        float dist = dir.magnitude;
        dir = dist > 1e-4f ? dir / dist : head.forward;
        transform.position = head.position + dir * PlaceDistance(dist, nearDistance, farDistance, standoff);
        transform.rotation = Quaternion.LookRotation(transform.position - head.position, Vector3.up);
    }

    /// Category label (small caps header) — exposed for the builder/tests.
    public static string Tag(LabInfoCategory c) => CategoryTag[(int)c];
}
