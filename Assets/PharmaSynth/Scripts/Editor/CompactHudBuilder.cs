#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// Compact VR HUD layout: the timer + title move OUT of the
/// centre and merge into the Progress pill on the LEFT (small, stacked); the top
/// cluster tucks to the top edge; the three Settings/Restart/Quit buttons collapse
/// behind ONE hamburger icon that opens a vertical dropdown; and Pharmee's bottom
/// dialogue bar is raised so it's fully in view. Idempotent — re-run after tuning
/// the constants below. Operates on the open scene's HudRig (SampleScene only).
public static class CompactHudBuilder
{
    // ---- tunables (canvas units unless noted) --------------------------------
    const float VerticalOffset = -0.12f;   // metres; shifts the whole head-locked HUD
    const float HalfAngleV = 23f;          // deg; taller canvas → top cluster tucks higher (dialogue compensated below)

    // Single merged panel: progress bar/label/% stay at the top; timer + title
    // are reparented onto a compact line beneath, in ONE pill.
    static readonly Vector2 ProgressPos  = new Vector2(24f, -8f);      // top-left, near the edge
    static readonly Vector2 ProgressSize = new Vector2(290f, 118f);    // grows DOWN (pivot top-left) to fit the merged line
    static readonly Vector2 TimerPos     = new Vector2(16f, -64f);     // inside the panel, below the bar
    static readonly Vector2 TimerSize    = new Vector2(150f, 26f);
    static readonly Vector2 TitlePos     = new Vector2(16f, -92f);     // inside the panel, bottom line
    static readonly Vector2 TitleSize    = new Vector2(262f, 22f);
    const float TimerFont = 22f;
    const float TitleFont = 12f;

    static readonly Vector2 MenuBtnPos  = new Vector2(-24f, -8f);      // top-right icon
    static readonly Vector2 MenuBtnSize = new Vector2(64f, 64f);
    static readonly Vector2 MenuListPos = new Vector2(-24f, -80f);     // dropdown, just below the icon
    static readonly Vector2 MenuItemSize = new Vector2(224f, 56f);
    const float MenuItemGap = 6f;
    const float MenuListPad = 16f;                                     // top+bottom padding inside the list
    // The list is SIZED FROM its item count (3 normally, 6 in a demo session) so
    // the demo verbs can live inside the same dropdown.
    static Vector2 MenuListSizeFor(int items)
        => new Vector2(240f, MenuListPad + items * MenuItemSize.y + (items - 1) * MenuItemGap);

    const float DialogueY = 361f;          // raised to keep the dialogue in place as HalfAngleV lowers the bottom edge

    static readonly Color PanelBg = new Color(0.045f, 0.065f, 0.11f, 0.95f);
    static readonly Color Cyan    = new Color(0.2f, 0.85f, 1f, 1f);

    [MenuItem("Tools/PharmaSynth/Rebuild Compact HUD")]
    public static void Rebuild()
    {
        if (Application.isPlaying) { Debug.LogWarning("[CompactHUD] exit Play mode first."); return; }

        var hudGo = GameObject.Find("HudRig");
        if (hudGo == null) { Debug.LogError("[CompactHUD] no 'HudRig' in the open scene."); return; }
        var hud = hudGo.transform as RectTransform;

        // 0. Raise the whole head-locked HUD (top cluster to the edge, dialogue up).
        var ctrl = hudGo.GetComponent<HudRigController>();
        if (ctrl != null)
        {
            var so = new SerializedObject(ctrl);
            var pv = so.FindProperty("verticalOffset");
            if (pv != null) pv.floatValue = VerticalOffset;
            var ph = so.FindProperty("halfAngleV");
            if (ph != null) ph.floatValue = HalfAngleV;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 1. ONE compact panel: keep the progress bar/label/% at the top of
        // ProgressPill, and merge the timer + title in beneath it (reparented via
        // the HUD controller's own refs so we hit the exact objects). Grow the pill
        // downward (pivot top-left) so the top content stays put.
        var progress = FindDeep(hud, "ProgressPill");
        if (progress != null) SetRect(progress, new Vector2(0, 1), new Vector2(0, 1), ProgressPos, ProgressSize);

        var hudCtrl = hudGo.GetComponent<ExperimentHudController>();
        TMP_Text timerTxt = null, titleTxt = null;
        if (hudCtrl != null)
        {
            var so2 = new SerializedObject(hudCtrl);
            var tp = so2.FindProperty("timerText"); if (tp != null) timerTxt = tp.objectReferenceValue as TMP_Text;
            var qp = so2.FindProperty("titleText"); if (qp != null) titleTxt = qp.objectReferenceValue as TMP_Text;
        }
        if (progress != null && timerTxt != null)
        {
            timerTxt.transform.SetParent(progress, false);
            timerTxt.enableAutoSizing = false; timerTxt.fontSize = TimerFont;
            timerTxt.alignment = TextAlignmentOptions.Left;
            SetRect(timerTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), TimerPos, TimerSize);
        }
        if (progress != null && titleTxt != null)
        {
            titleTxt.transform.SetParent(progress, false);
            titleTxt.enableAutoSizing = false; titleTxt.fontSize = TitleFont;
            titleTxt.alignment = TextAlignmentOptions.Left;
            titleTxt.overflowMode = TextOverflowModes.Ellipsis;   // never clip a long title
            SetRect(titleTxt.rectTransform, new Vector2(0, 1), new Vector2(0, 1), TitlePos, TitleSize);
        }
        // Retire the now-empty timer pill background (its texts moved into ProgressPill).
        var timerPill = FindDeep(hud, "TimerPill");
        if (timerPill != null) timerPill.gameObject.SetActive(false);

        // 2. Collapse Settings/Restart/Quit behind a single hamburger icon + dropdown,
        //    plus the DEMO verbs, which share the same dropdown. DemoHudController keeps
        //    showing/hiding each demo item, so they only appear in a demo session.
        var list = FindDeep(hud, "HudMenuList");
        if (list == null) list = MakePanel(hud, "HudMenuList", PanelBg);

        var items = new System.Collections.Generic.List<string> { "SettingsBtn", "RestartBtn", "QuitBtn" };
        items.AddRange(FoldDemoButtonsIn(hud));      // appends any demo buttons found

        SetRect(list, new Vector2(1, 1), new Vector2(1, 1), MenuListPos, MenuListSizeFor(items.Count));
        for (int i = 0; i < items.Count; i++)
        {
            var b = FindDeep(hud, items[i]);
            if (b == null) continue;
            b.SetParent(list, false);
            SetRect(b, new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-8f, -8f - i * (MenuItemSize.y + MenuItemGap)), MenuItemSize);
        }

        var icon = FindDeep(hud, "HudMenuButton");
        if (icon == null) icon = MakeMenuIcon(hud);
        SetRect(icon, new Vector2(1, 1), new Vector2(1, 1), MenuBtnPos, MenuBtnSize);

        var dropdown = icon.GetComponent<HudMenuDropdown>();
        if (dropdown == null) dropdown = icon.gameObject.AddComponent<HudMenuDropdown>();
        dropdown.SetList(list.gameObject);

        // Wire the icon to toggle; each action button also closes the menu.
        var iconBtn = icon.GetComponent<Button>();
        if (iconBtn != null)
        {
            for (int i = iconBtn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(iconBtn.onClick, i);
            UnityEventTools.AddPersistentListener(iconBtn.onClick, dropdown.Toggle);
        }
        foreach (var name in items)
        {
            var b = FindDeep(hud, name);
            var btn = b != null ? b.GetComponent<Button>() : null;
            if (btn != null) AddCloseOnce(btn.onClick, dropdown);
        }
        list.gameObject.SetActive(false);

        // 3. Raise Pharmee's dialogue bar so it's fully visible.
        var dialogue = FindDeep(hud, "DialogueBar");
        if (dialogue != null)
        {
            var pos = dialogue.anchoredPosition;
            dialogue.anchoredPosition = new Vector2(pos.x, DialogueY);
        }

        EditorSceneManager.MarkSceneDirty(hudGo.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[CompactHUD] compact HUD rebuilt in '" + hudGo.scene.name + "'.</color>");
    }

    // ---- helpers -------------------------------------------------------------

    /// Pull the demo verbs (Skip Step / Finish Experiment / Auto-Answer Quiz) out of
    /// their floating row and hand their names back so they get laid out INSIDE the
    /// hamburger dropdown. Returns the names found, in order.
    /// The emptied row root is retired and DemoHudController's `cluster` reference
    /// cleared, or it would keep toggling an empty pill back on every frame.
    static System.Collections.Generic.List<string> FoldDemoButtonsIn(RectTransform hud)
    {
        var found = new System.Collections.Generic.List<string>();
        var demo = Object.FindFirstObjectByType<DemoHudController>(FindObjectsInactive.Include);
        if (demo == null) return found;

        var so = new SerializedObject(demo);
        foreach (var field in new[] { "skipButton", "finishButton", "quizButton" })
        {
            var go = so.FindProperty(field)?.objectReferenceValue as GameObject;
            if (go == null) continue;
            go.SetActive(true);            // DemoHudController re-gates it per frame
            found.Add(go.name);
        }

        // Retire the now-empty cluster row + stop the controller re-showing it.
        var clusterProp = so.FindProperty("cluster");
        var cluster = clusterProp?.objectReferenceValue as GameObject;
        if (cluster != null && !found.Contains(cluster.name))
        {
            cluster.SetActive(false);
            clusterProp.objectReferenceValue = null;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(demo);
        return found;
    }

    static void AddCloseOnce(UnityEngine.Events.UnityEvent evt, HudMenuDropdown d)
    {
        for (int i = 0; i < evt.GetPersistentEventCount(); i++)
            if (evt.GetPersistentTarget(i) == d && evt.GetPersistentMethodName(i) == "Close") return;
        UnityEventTools.AddPersistentListener(evt, d.Close);
    }

    static void SetRect(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = aMax;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    static RectTransform MakePanel(Transform parent, string name, Color bg)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = bg; img.raycastTarget = true;
        return go.GetComponent<RectTransform>();
    }

    static RectTransform MakeMenuIcon(Transform parent)
    {
        var rt = MakePanel(parent, "HudMenuButton", PanelBg);
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = Cyan; outline.effectDistance = new Vector2(2f, -2f);
        rt.gameObject.AddComponent<Button>();
        // three hamburger bars
        for (int i = 0; i < 3; i++)
        {
            var bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            bar.layer = parent.gameObject.layer;
            bar.transform.SetParent(rt, false);
            bar.GetComponent<Image>().color = Cyan;
            var brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(34f, 5f);
            brt.anchoredPosition = new Vector2(0f, 11f - i * 11f);
        }
        return rt;
    }

    static RectTransform FindDeep(Transform root, string exact)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == exact) return t as RectTransform;
        return null;
    }
}
#endif
