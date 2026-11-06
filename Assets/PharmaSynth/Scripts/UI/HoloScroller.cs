using UnityEngine;
using UnityEngine.UI;

/// Scroll driver for the holo procedures board (the instruction
/// text rendered as one continuous row and couldn't be read — the body now wraps
/// inside a masked viewport and this scrolls it). Big ▲/▼ page buttons are the
/// primary VR affordance (poke/ray-friendly); the ScrollRect still accepts direct
/// drag. Pure page math kept static so the suite pins the clamping.
public class HoloScroller : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField, Range(0.1f, 1f)] private float pageFraction = 0.6f;

    /// One page step in normalized scroll space. 1 = top, 0 = bottom (Unity's
    /// convention), so paging DOWN subtracts. Clamped; a short body (viewport
    /// taller than content) always reads 1 (pinned at top).
    public static float NextPage(float current01, float pageFrac, int direction)
        => Mathf.Clamp01(current01 + direction * Mathf.Clamp01(pageFrac));

    public void Bind(ScrollRect sr) => scrollRect = sr;

    public void PageUp() => Step(+1);
    public void PageDown() => Step(-1);

    private void Step(int direction)
    {
        if (scrollRect == null) return;
        scrollRect.verticalNormalizedPosition =
            NextPage(scrollRect.verticalNormalizedPosition, PageFrac(), direction);
    }

    /// Fraction of the content one button press moves: most of a viewport.
    private float PageFrac()
    {
        if (scrollRect == null || scrollRect.content == null || scrollRect.viewport == null)
            return pageFraction;
        float content = scrollRect.content.rect.height;
        float view = scrollRect.viewport.rect.height;
        if (content <= view + 1f) return 1f;               // nothing to scroll
        return Mathf.Clamp01(view * 0.85f / (content - view));
    }

}
