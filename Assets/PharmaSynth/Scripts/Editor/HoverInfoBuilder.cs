#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using NearFar = UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor;

/// Builds the hover-inspector info card + wires the raycasting HoverInspector into
/// SampleScene. Point the right-hand ray (or gaze) at a reagent,
/// a piece of apparatus or an NPC and a smoothly-animated card names it and explains
/// what it is / how to use it.
///
/// Tools ▸ PharmaSynth ▸ Build Hover Info Panel (SampleScene, edit mode, idempotent).
public static class HoverInfoBuilder
{
    [MenuItem("Tools/PharmaSynth/Build Hover Info Panel")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[HoverInfo] exit Play mode first."); return; }

        Camera cam = Camera.main;
        if (cam == null)
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                if (c.CompareTag("MainCamera")) { cam = c; break; }
        Transform head = cam != null ? cam.transform : null;

        var panel = BuildPanel(head);

        // Inspector lives on the XR rig (or a manager) so it ticks every frame.
        var host = GameObject.Find("ExperimentSystems") ?? GameObject.Find("XR Origin (XR Rig)");
        if (host == null) host = new GameObject("HoverInspectorHost");
        var inspector = host.GetComponent<HoverInspector>() ?? host.AddComponent<HoverInspector>();

        Transform aim = FindRightRay();
        // Cast against everything EXCEPT UI, the mirror-only avatar (layer 8) and the
        // engine's non-interactive layers, so the card never triggers off HUD/own body.
        int ui = LayerMask.NameToLayer("UI");
        int ignore = LayerMask.NameToLayer("Ignore Raycast");
        int tfx = LayerMask.NameToLayer("TransparentFX");
        int avatar = 8;                                   // PlayerAvatar (mirror-only)
        int mask = ~0;
        if (ui >= 0) mask &= ~(1 << ui);
        if (ignore >= 0) mask &= ~(1 << ignore);
        if (tfx >= 0) mask &= ~(1 << tfx);
        mask &= ~(1 << avatar);
        inspector.Bind(aim, head, panel, mask);
        // The OTHER hand points too: carrying a tube in one hand is the normal
        // working posture, so the card must not blank out for both hands the moment either
        // one holds anything.
        Transform alt = FindOtherRay(aim);
        inspector.BindAltAim(alt);
        EditorUtility.SetDirty(inspector);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[HoverInfo] panel built + inspector on '" + host.name +
                  "' (aim=" + (aim != null ? aim.name : "gaze")
                  + ", alt=" + (FindOtherRay(aim) != null ? FindOtherRay(aim).name : "none")
                  + ", cam=" + (head != null) + ")</color>. "
                  + LabInfoDatabase.EquipmentCount + " equipment + " + LabInfoDatabase.ReagentCount + " reagents authored.");
    }

    static HoverInfoPanel BuildPanel(Transform head)
    {
        var old = GameObject.Find("HoverInfoPanel");
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject("HoverInfoPanel", typeof(Canvas), typeof(CanvasGroup));
        root.layer = Mathf.Max(0, LayerMask.NameToLayer("UI"));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 28000;                       // above world panels, below HUD (30000)
        var crt = (RectTransform)root.transform;
        crt.sizeDelta = new Vector2(560f, 340f);
        root.transform.localScale = Vector3.one * 0.0010f;   // ≈ 0.56 × 0.34 m
        root.transform.position = new Vector3(0f, 1.3f, 0f);
        var group = root.GetComponent<CanvasGroup>();
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        // A single "card" child we scale for the pop (leaving the canvas alpha to the group).
        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        var cardRt = (RectTransform)card.transform;
        Stretch(cardRt);
        card.GetComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.94f);   // dark glass

        // Accent bar down the left edge (category colour).
        var accentGo = new GameObject("Accent", typeof(Image));
        accentGo.transform.SetParent(card.transform, false);
        var accent = accentGo.GetComponent<Image>();
        accent.color = new Color(0.38f, 0.82f, 1f);
        var art = accent.rectTransform;
        art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
        art.pivot = new Vector2(0f, 0.5f);
        art.sizeDelta = new Vector2(12f, 0f); art.anchoredPosition = Vector2.zero;

        // Category tag (small, coloured).
        var catGo = new GameObject("Category", typeof(TextMeshProUGUI));
        catGo.transform.SetParent(card.transform, false);
        var cat = catGo.GetComponent<TextMeshProUGUI>();
        cat.text = "EQUIPMENT"; cat.fontSize = 20f; cat.fontStyle = FontStyles.Bold;
        cat.characterSpacing = 6f; cat.color = new Color(0.38f, 0.82f, 1f);
        cat.alignment = TextAlignmentOptions.TopLeft;
        var catRt = cat.rectTransform;
        catRt.anchorMin = new Vector2(0f, 1f); catRt.anchorMax = new Vector2(1f, 1f);
        catRt.pivot = new Vector2(0.5f, 1f);
        catRt.anchoredPosition = new Vector2(0f, -18f); catRt.sizeDelta = new Vector2(-56f, 26f);

        // Title.
        var titleGo = new GameObject("Title", typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(card.transform, false);
        var title = titleGo.GetComponent<TextMeshProUGUI>();
        title.text = "Title"; title.fontSize = 40f; title.fontStyle = FontStyles.Bold;
        title.color = Color.white; title.alignment = TextAlignmentOptions.TopLeft;
        var trt = title.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -46f); trt.sizeDelta = new Vector2(-56f, 54f);

        // Body (generous, word-wrapped, plenty of room).
        var bodyGo = new GameObject("Body", typeof(TextMeshProUGUI));
        bodyGo.transform.SetParent(card.transform, false);
        var body = bodyGo.GetComponent<TextMeshProUGUI>();
        body.text = "Body"; body.fontSize = 26f;
        body.color = new Color(0.9f, 0.93f, 0.98f, 1f);
        body.alignment = TextAlignmentOptions.TopLeft;
        body.textWrappingMode = TextWrappingModes.Normal;
        var brt = body.rectTransform;
        Stretch(brt);
        brt.offsetMin = new Vector2(28f, 24f);             // left/bottom padding
        brt.offsetMax = new Vector2(-24f, -110f);          // right/top padding (below title)

        var comp = root.AddComponent<HoverInfoPanel>();
        comp.Bind(group, cardRt, title, body, cat, accent, head);
        return comp;
    }

    /// The right-hand ray interactor's transform (pointer), else null → gaze fallback.
    static Transform FindRightRay()
    {
        Transform best = null;
        foreach (var nf in Object.FindObjectsByType<NearFar>(FindObjectsInactive.Include))
        {
            string path = nf.name.ToLower();
            var t = nf.transform;
            var p = t.parent; int guard = 0;
            while (p != null && guard++ < 6) { path += "/" + p.name.ToLower(); p = p.parent; }
            if (path.Contains("right")) return nf.transform;
            if (best == null) best = nf.transform;
        }
        return best;
    }

    /// The ray that is NOT `taken` — the second controller, whichever hand it is.
    static Transform FindOtherRay(Transform taken)
    {
        foreach (var nf in Object.FindObjectsByType<NearFar>(FindObjectsInactive.Include))
            if (nf != null && nf.transform != taken) return nf.transform;
        return null;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }
}
#endif
