using UnityEngine;

/// Collapses the HUD's Settings/Restart/Quit actions behind a single icon button —
/// plus the demo verbs (Skip Step / Finish Experiment / Auto-Answer Quiz), which
/// live in the same list and are shown only during a demo session.
/// The icon's onClick calls Toggle(); each action button's onClick also calls
/// Close() so the list dismisses after a pick. Starts hidden. The action buttons
/// keep their own LabMenuController / DemoHudController wiring untouched.
public class HudMenuDropdown : MonoBehaviour
{
    [SerializeField] private GameObject listPanel;   // the vertical action list
    [Tooltip("Item height + gap, matching the builder's layout — used to re-fit the panel to however many items are actually visible.")]
    [SerializeField] private float itemHeight = 56f;
    [SerializeField] private float itemGap = 6f;
    [SerializeField] private float padding = 16f;

    public void SetList(GameObject panel) { listPanel = panel; if (listPanel != null) listPanel.SetActive(false); }

    private void OnEnable() { if (listPanel != null) listPanel.SetActive(false); }

    public void Toggle()
    {
        if (listPanel == null) return;
        bool show = !listPanel.activeSelf;
        if (show) FitToVisibleItems();   // demo items may be hidden — don't leave a gap
        listPanel.SetActive(show);
    }

    public void Close() { if (listPanel != null) listPanel.SetActive(false); }

    /// Pure (suite): panel height for N visible items.
    public static float HeightFor(int items, float itemHeight, float gap, float pad)
        => items <= 0 ? pad : pad + items * itemHeight + (items - 1) * gap;

    /// Shrink/grow the panel to however many items are currently shown, so a
    /// non-demo session doesn't get a dropdown with three empty slots in it.
    private void FitToVisibleItems()
    {
        var rt = listPanel.transform as RectTransform;
        if (rt == null) return;
        int visible = 0;
        foreach (Transform c in listPanel.transform) if (c.gameObject.activeSelf) visible++;
        rt.sizeDelta = new Vector2(rt.sizeDelta.x, HeightFor(visible, itemHeight, itemGap, padding));
    }
}
