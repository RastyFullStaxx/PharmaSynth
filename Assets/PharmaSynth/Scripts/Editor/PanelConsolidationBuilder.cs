#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// One procedures display. The entrance LabTablet duplicates the wrist holo
/// board and the wrist mini-panel duplicates the holo header: three surfaces
/// for the same content, with the tablet's fixed rect overflowing into its
/// reaction footer. This menu retires the LabTablet
/// (deactivated, not deleted) and the MiniPanel, and upgrades the holo board to
/// the single panel: status header (ex mini-panel) + focused checklist + the
/// balanced-reaction footer (ex tablet). Idempotent.
public static class PanelConsolidationBuilder
{
    [MenuItem("Tools/PharmaSynth/Consolidate Procedure Panels")]
    public static void Consolidate()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[PanelConsolidation] exit Play mode first.");
            return;
        }

        var watch = FirstSceneObject<WristWatchController>();
        if (watch == null) { Debug.LogError("[PanelConsolidation] no WristWatchController in the open scene."); return; }
        var so = new SerializedObject(watch);

        // 1. Retire the entrance LabTablet, harvesting its reaction string first.
        string reaction = "";
        var tablet = FindInScene("LabTablet");
        if (tablet != null)
        {
            var tc = tablet.GetComponent<TabletChecklistController>();
            if (tc != null)
            {
                var tso = new SerializedObject(tc);
                var rp = tso.FindProperty("balancedReaction");
                if (rp != null) reaction = rp.stringValue;
            }
            if (tablet.activeSelf) tablet.SetActive(false);
        }

        // 2. Retire the wrist MiniPanel (the runtime also force-hides it).
        var panelProp = so.FindProperty("panel");
        if (panelProp != null && panelProp.objectReferenceValue is GameObject mini && mini.activeSelf)
            mini.SetActive(false);

        // 3. Upgrade the holo board: header + resized body + reaction footer.
        var holoProp = so.FindProperty("holoPanel");
        var bodyProp = so.FindProperty("holoBody");
        var holo = holoProp != null ? holoProp.objectReferenceValue as GameObject : null;
        var body = bodyProp != null ? bodyProp.objectReferenceValue as TMP_Text : null;
        if (holo == null || body == null)
        {
            Debug.LogError("[PanelConsolidation] holo board refs missing on WristWatchController.");
            return;
        }

        // Keep the procedures board readable as the player turns (the
        // results/procedures panels must face the avatar). PlaceHolo aims it on
        // summon; FaceCamera holds it facing the head every frame after.
        if (holo.GetComponent<FaceCamera>() == null)
        {
            var fc = holo.AddComponent<FaceCamera>();
            fc.yAxisOnly = true; fc.faceTowardCamera = false;
        }

        var summary = EnsureText(holo.transform, "HoloSummary", body,
            new Vector2(680f, 46f), new Vector2(0f, 368f), 26f, TextAlignmentOptions.Center,
            new Color(0.85f, 0.95f, 1f));
        var footer = EnsureText(holo.transform, "HoloReaction", body,
            new Vector2(680f, 40f), new Vector2(0f, -462f), 24f, TextAlignmentOptions.Center,
            new Color(0.65f, 0.85f, 1f, 0.9f));

        var bodyRt = body.rectTransform;
        bodyRt.sizeDelta = new Vector2(680f, 740f);
        bodyRt.anchoredPosition = new Vector2(0f, -75f);

        // 4. Wire the new refs + harvested reaction into the controller.
        so.FindProperty("holoSummary").objectReferenceValue = summary;
        so.FindProperty("holoReaction").objectReferenceValue = footer;
        if (!string.IsNullOrEmpty(reaction))
            so.FindProperty("balancedReaction").stringValue = reaction;
        so.ApplyModifiedPropertiesWithoutUndo();

        // 5. Re-seed the tour (its first stop taught the retired tablet).
        var guide = FirstSceneObject<LabTourGuide>();
        if (guide != null)
        {
            guide.SeedDefaults();
            EditorUtility.SetDirty(guide);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PanelConsolidation] LabTablet retired, MiniPanel retired, holo board upgraded"
                  + (tablet == null ? " (no LabTablet found — already retired?)" : "")
                  + (guide == null ? ", NO tour guide found" : ", tour re-seeded") + ".");
    }

    /// Every holo text wraps (an instruction step would otherwise run as one
    /// continuous row), and the checklist body
    /// moves inside a masked, scrollable viewport with big ^ / v page buttons
    /// (poke/ray-friendly) driven by HoloScroller. Idempotent.
    [MenuItem("Tools/PharmaSynth/Fix Holo Board Scroll")]
    public static void FixHoloScroll()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PanelConsolidation] exit Play mode first."); return; }
        var watch = FirstSceneObject<WristWatchController>();
        if (watch == null) { Debug.LogError("[PanelConsolidation] no WristWatchController in the open scene."); return; }
        var so = new SerializedObject(watch);
        var holo = so.FindProperty("holoPanel")?.objectReferenceValue as GameObject;
        var body = so.FindProperty("holoBody")?.objectReferenceValue as TMP_Text;
        var summary = so.FindProperty("holoSummary")?.objectReferenceValue as TMP_Text;
        var reaction = so.FindProperty("holoReaction")?.objectReferenceValue as TMP_Text;
        if (holo == null || body == null)
        { Debug.LogError("[PanelConsolidation] holo board refs missing — run Consolidate Procedure Panels first."); return; }

        // 1. Wrap everything; never truncate. The summary grows to two lines
        //    (step label on its own line — see ChecklistPager.BuildHeader).
        foreach (var t in new[] { body, summary, reaction })
        {
            if (t == null) continue;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
        }
        if (summary != null) summary.rectTransform.sizeDelta = new Vector2(680f, 84f);

        // 2. Masked, scrollable viewport around the body (left-of-buttons column).
        var viewportT = holo.transform.Find("BodyViewport");
        GameObject viewport = viewportT != null ? viewportT.gameObject : null;
        if (viewport == null)
        {
            viewport = new GameObject("BodyViewport", typeof(RectTransform));
            viewport.transform.SetParent(holo.transform, false);
        }
        var vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = vpRt.anchorMax = new Vector2(0.5f, 0.5f);
        vpRt.pivot = new Vector2(0.5f, 0.5f);
        vpRt.sizeDelta = new Vector2(610f, 700f);
        vpRt.anchoredPosition = new Vector2(-35f, -85f);
        if (viewport.GetComponent<RectMask2D>() == null) viewport.AddComponent<RectMask2D>();
        // An Image makes the viewport a raycast target so ray-drag scrolling works.
        var vpImg = viewport.GetComponent<Image>() ?? viewport.AddComponent<Image>();
        vpImg.color = new Color(0f, 0f, 0f, 0.001f);

        // Body becomes the scrolled content: top-anchored, auto-height.
        var bodyRt = body.rectTransform;
        if (bodyRt.parent != vpRt) bodyRt.SetParent(vpRt, false);
        bodyRt.anchorMin = new Vector2(0.5f, 1f);
        bodyRt.anchorMax = new Vector2(0.5f, 1f);
        bodyRt.pivot = new Vector2(0.5f, 1f);
        bodyRt.sizeDelta = new Vector2(610f, 700f);
        bodyRt.anchoredPosition = Vector2.zero;
        var fitter = body.GetComponent<ContentSizeFitter>() ?? body.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        var sr = viewport.GetComponent<ScrollRect>() ?? viewport.AddComponent<ScrollRect>();
        sr.content = bodyRt;
        sr.viewport = vpRt;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 30f;

        // 3. HoloScroller + big page buttons on the right-edge column.
        var scroller = holo.GetComponent<HoloScroller>() ?? holo.AddComponent<HoloScroller>();
        scroller.Bind(sr);
        EnsurePageButton(holo.transform, "PageUpButton", "^", new Vector2(330f, 60f), scroller, up: true);
        EnsurePageButton(holo.transform, "PageDownButton", "v", new Vector2(330f, -230f), scroller, up: false);

        // 4. The board's canvas must catch XR rays/pokes.
        var canvas = holo.GetComponentInParent<Canvas>(true);
        if (canvas != null)
        {
            if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[PanelConsolidation] holo board wraps + scrolls (viewport 610x700, page buttons wired)"
                  + (canvas == null ? " — WARNING: no parent Canvas found" : "") + ".");
    }

    /// Load-or-create one ^ / v page button wired to the scroller (persistent
    /// listener — no runtime wiring needed).
    static void EnsurePageButton(Transform holo, string name, string glyph, Vector2 pos,
                                 HoloScroller scroller, bool up)
    {
        var t = holo.Find(name);
        GameObject go = t != null ? t.gameObject : null;
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(holo, false);
        }
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(72f, 72f);
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>() ?? go.AddComponent<Image>();
        img.color = new Color(0.10f, 0.16f, 0.26f, 0.92f);
        var btn = go.GetComponent<Button>() ?? go.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.25f, 0.55f, 0.85f, 1f);
        colors.pressedColor = new Color(0.15f, 0.35f, 0.6f, 1f);
        btn.colors = colors;
        // Every other button factory adds UiButtonSounds; these two need it too,
        // or they are the only buttons with no hover/click cue.
        if (go.GetComponent<UiButtonSounds>() == null) go.AddComponent<UiButtonSounds>();

        var labelT = go.transform.Find("Label");
        TextMeshProUGUI label;
        if (labelT != null) label = labelT.GetComponent<TextMeshProUGUI>();
        else
        {
            var lgo = new GameObject("Label", typeof(RectTransform));
            lgo.transform.SetParent(go.transform, false);
            label = lgo.AddComponent<TextMeshProUGUI>();
        }
        var lrt = label.rectTransform;
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        label.text = glyph;
        label.fontSize = 40f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.61f, 0.85f, 1f);
        label.raycastTarget = false;

        // Re-wire idempotently: clear then add ONE persistent listener.
        for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(btn.onClick,
            up ? (UnityEngine.Events.UnityAction)scroller.PageUp
               : (UnityEngine.Events.UnityAction)scroller.PageDown);
    }

    static T FirstSceneObject<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return all.Length > 0 ? all[0] : null;
    }

    static GameObject FindInScene(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name == name) return c;
            var deep = FindDeep(c, name);
            if (deep != null) return deep;
        }
        return null;
    }

    /// Load-or-create a TMP child on the holo board, styled after the body text.
    static TMP_Text EnsureText(Transform parent, string name, TMP_Text styleSource,
        Vector2 size, Vector2 anchoredPos, float fontSize, TextAlignmentOptions align, Color color)
    {
        var existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing != null) text = existing.GetComponent<TextMeshProUGUI>();
        else
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            text = go.AddComponent<TextMeshProUGUI>();
        }
        if (text == null) return null;
        if (styleSource != null && styleSource.font != null) text.font = styleSource.font;
        text.fontSize = fontSize;
        text.alignment = align;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        var rt = text.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return text;
    }
}
#endif
