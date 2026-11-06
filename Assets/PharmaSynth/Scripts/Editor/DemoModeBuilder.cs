#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Demo Mode scene wiring. Three menus:
///   • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into
///     a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory.
///   • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer
///     Quiz row under the HUD's top-right cluster, driven by DemoHudController.
///   • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json
///     for in-editor testing (the shipped StreamingAssets default stays false).
/// All idempotent.
public static class DemoModeBuilder
{
    static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.11f, 0.94f);
    static readonly Color DemoAccent = new Color(1f, 0.72f, 0.25f);   // amber = "special mode"
    static readonly Color CyanText = new Color(0.55f, 0.9f, 1f);

    // ---- MainMenu: the config-gated Demo Mode button -----------------------

    [MenuItem("Tools/PharmaSynth/Demo/Build Demo Menu Button")]
    public static void BuildMenuButton()
    {
        if (Application.isPlaying) { Debug.LogWarning("[DemoModeBuilder] exit Play mode first."); return; }
        var lab = FindInScene("LaboratoryButton");
        var controller = Object.FindAnyObjectByType<MainMenuController>();
        if (lab == null || controller == null)
        {
            Debug.LogError("[DemoModeBuilder] LaboratoryButton/MainMenuController not found — open MainMenu.unity first.");
            return;
        }

        var parent = lab.transform.parent;
        var existing = parent.Find("DemoModeButton");
        GameObject demo;
        if (existing != null) demo = existing.gameObject;
        else
        {
            demo = Object.Instantiate(lab, parent);
            demo.name = "DemoModeButton";
            // Slot it below the lowest button in the panel, one row-gap down.
            var labRt = lab.GetComponent<RectTransform>();
            float minY = labRt.anchoredPosition.y;
            foreach (var b in parent.GetComponentsInChildren<Button>(true))
            {
                var rt = b.GetComponent<RectTransform>();
                if (rt != null && rt.parent == parent && rt.anchoredPosition.y < minY)
                    minY = rt.anchoredPosition.y;
            }
            var demoRt = demo.GetComponent<RectTransform>();
            demoRt.anchoredPosition = new Vector2(labRt.anchoredPosition.x,
                minY - (labRt.sizeDelta.y > 1f ? labRt.sizeDelta.y : 60f) - 14f);
        }

        var label = demo.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = "Demo Mode"; label.color = DemoAccent; }

        var btn = demo.GetComponent<Button>();
        if (btn != null)
        {
            while (btn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(btn.onClick, 0);
            UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.OnDemoLaboratory);
        }

        demo.SetActive(false);   // DemoButtonVisibility reveals it when the config says so
        var vis = parent.GetComponent<DemoButtonVisibility>();
        if (vis == null) vis = parent.gameObject.AddComponent<DemoButtonVisibility>();
        vis.Bind(demo);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[DemoModeBuilder] Demo Mode menu button built (hidden until the config enables it).");
    }

    // ---- SampleScene: the demo HUD cluster ---------------------------------

    [MenuItem("Tools/PharmaSynth/Demo/Build Demo HUD")]
    public static void BuildDemoHud()
    {
        if (Application.isPlaying) { Debug.LogWarning("[DemoModeBuilder] exit Play mode first."); return; }
        var topRight = FindInScene("TopRight");
        var hudRig = FindInScene("HudRig");
        if (topRight == null || hudRig == null)
        {
            Debug.LogError("[DemoModeBuilder] HudRig/TopRight not found — open SampleScene.unity first.");
            return;
        }

        var parent = topRight.transform.parent;
        var clusterT = parent.Find("DemoCluster");
        GameObject cluster;
        if (clusterT != null) cluster = clusterT.gameObject;
        else
        {
            cluster = new GameObject("DemoCluster", typeof(RectTransform));
            cluster.transform.SetParent(parent, false);
            var topRt = topRight.GetComponent<RectTransform>();
            var rt = cluster.GetComponent<RectTransform>();
            rt.anchorMin = topRt.anchorMin; rt.anchorMax = topRt.anchorMax; rt.pivot = topRt.pivot;
            rt.sizeDelta = new Vector2(470f, 56f);
            rt.anchoredPosition = topRt.anchoredPosition + new Vector2(0f, -(Mathf.Max(topRt.sizeDelta.y, 56f) + 14f));
        }

        var skip = EnsureHudButton(cluster.transform, "DemoSkipBtn", "Skip Step", 0);
        var finish = EnsureHudButton(cluster.transform, "DemoFinishBtn", "Finish Exp.", 1);
        var quiz = EnsureHudButton(cluster.transform, "DemoQuizBtn", "Auto Quiz", 2);

        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var postLab = FirstIncludingInactive<PostLabController>();
        var ctrl = hudRig.GetComponent<DemoHudController>();
        if (ctrl == null) ctrl = hudRig.AddComponent<DemoHudController>();
        ctrl.Bind(runner, postLab, cluster, skip.gameObject, finish.gameObject, quiz.gameObject);
        EditorUtility.SetDirty(ctrl);

        WireClick(skip, ctrl, nameof(DemoHudController.OnSkipStep));
        WireClick(finish, ctrl, nameof(DemoHudController.OnFinishExperiment));
        WireClick(quiz, ctrl, nameof(DemoHudController.OnAutoQuiz));

        cluster.SetActive(false);   // controller shows it only during demo sessions

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[DemoModeBuilder] demo HUD cluster built under the top-right buttons.");
    }

    static Button EnsureHudButton(Transform cluster, string name, string label, int slot)
    {
        var t = cluster.Find(name);
        GameObject go;
        if (t != null) go = t.gameObject;
        else
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(cluster, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(148f, 52f);
            rt.anchoredPosition = new Vector2(-slot * 158f, 0f);

            var img = go.AddComponent<Image>();
            img.color = PanelBg;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = DemoAccent;
        }
        var lbl = go.GetComponentInChildren<TMP_Text>(true);
        if (lbl != null) { lbl.text = label; lbl.color = slot == 2 ? CyanText : DemoAccent; }
        return go.GetComponent<Button>();
    }

    static void WireClick(Button btn, DemoHudController ctrl, string method)
    {
        if (btn == null) return;
        while (btn.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(btn.onClick, 0);
        var action = (UnityEngine.Events.UnityAction)System.Delegate.CreateDelegate(
            typeof(UnityEngine.Events.UnityAction), ctrl, method);
        UnityEventTools.AddVoidPersistentListener(btn.onClick, action);
    }

    // ---- persistent-override toggles ----------------------------------------

    [MenuItem("Tools/PharmaSynth/Demo/Demo Enabled (persistent override)")]
    public static void ToggleOverride()
    {
        string path = Path.Combine(Application.persistentDataPath, DemoModeLoader.FileName);
        if (File.Exists(path)) { File.Delete(path); Debug.Log("[DemoModeBuilder] override removed → shipped default applies (disabled)."); }
        else
        {
            File.WriteAllText(path, "{\n  \"demoEnabled\": true,\n  \"infiniteSupply\": true\n}\n");
            Debug.Log("[DemoModeBuilder] override written: demo ENABLED — " + path);
        }
    }

    [MenuItem("Tools/PharmaSynth/Demo/Demo Enabled (persistent override)", true)]
    public static bool ToggleOverrideValidate()
    {
        Menu.SetChecked("Tools/PharmaSynth/Demo/Demo Enabled (persistent override)",
            File.Exists(Path.Combine(Application.persistentDataPath, DemoModeLoader.FileName)));
        return true;
    }

    // ---- helpers -------------------------------------------------------------

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

    static T FirstIncludingInactive<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return all.Length > 0 ? all[0] : null;
    }
}
#endif
