using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UImage = UnityEngine.UI.Image;

/// Restyles the grade screen into the client slides' "Mission Complete" card: a row of
/// three white tiles (Time · Mistakes · Grade) with large blue figures, the verdict
/// under them, the rubric breakdown below in a smaller type, buttons unchanged.
/// Idempotent: tiles are found-or-created and every rect is SET, never nudged.
/// The controller's text references are untouched (it still fills the same objects).
public static class GradeCardStyler
{
    static readonly Color Tile = new Color(0.97f, 0.98f, 1f, 1f);
    static readonly Color Figure = new Color(0.16f, 0.55f, 0.85f, 1f);

    [MenuItem("Tools/PharmaSynth/Style Grade Card (slide layout)")]
    public static void Style()
    {
        var gs = Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (gs == null) { Debug.LogError("[GradeCard] no GradeScreenController in the scene."); return; }
        var root = new SerializedObject(gs).FindProperty("root").objectReferenceValue as GameObject;
        if (root == null) { Debug.LogError("[GradeCard] grade screen root not wired."); return; }
        var t = root.transform;
        Undo.RegisterFullObjectHierarchyUndo(root, "Style Grade Card");

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        int panelIndex = t.Find("Panel") != null ? t.Find("Panel").GetSiblingIndex() : 0;
        string[] tiles = { "Tile_Time", "Tile_Mistakes", "Tile_Grade" };
        float[] xs = { -310f, 0f, 310f };
        for (int i = 0; i < tiles.Length; i++)
        {
            var tile = t.Find(tiles[i]);
            if (tile == null)
            {
                var go = new GameObject(tiles[i], typeof(RectTransform), typeof(UImage));
                go.layer = root.layer;
                go.transform.SetParent(t, false);
                tile = go.transform;
            }
            tile.SetSiblingIndex(panelIndex + 1 + i);          // behind every text
            var img = tile.GetComponent<UImage>();
            img.sprite = sprite; img.type = UImage.Type.Sliced; img.color = Tile; img.raycastTarget = false;
            Place(tile, new Vector2(xs[i], 150f), new Vector2(280f, 190f));
        }

        Text(t, "Banner", new Vector2(0f, 310f), new Vector2(960f, 70f), 52f, Color.white);
        Text(t, "Time", new Vector2(-310f, 170f), new Vector2(270f, 90f), 58f, Figure);
        Text(t, "TimeLbl", new Vector2(-310f, 90f), new Vector2(270f, 40f), 26f, Figure);
        Text(t, "Mistakes", new Vector2(0f, 170f), new Vector2(270f, 90f), 70f, Figure);
        Text(t, "MistakesLbl", new Vector2(0f, 90f), new Vector2(270f, 40f), 26f, Figure, "Number of Mistakes");
        Text(t, "Grade", new Vector2(310f, 170f), new Vector2(270f, 90f), 70f, Figure);
        Text(t, "Result", new Vector2(0f, 10f), new Vector2(960f, 60f), 40f, Color.white);
        Text(t, "Breakdown", new Vector2(0f, -150f), new Vector2(900f, 230f), 22f, new Color(0.82f, 0.88f, 0.95f));
        // The Grade tile has no label object of its own: give it one.
        var gl = t.Find("GradeLbl");
        if (gl == null)
        {
            var go = new GameObject("GradeLbl", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.layer = root.layer; go.transform.SetParent(t, false); gl = go.transform;
        }
        var glt = gl.GetComponent<TextMeshProUGUI>();
        glt.text = "Grade"; glt.alignment = TextAlignmentOptions.Center; glt.raycastTarget = false;
        Text(t, "GradeLbl", new Vector2(310f, 90f), new Vector2(270f, 40f), 26f, Figure);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[GradeCard] grade screen styled to the slide layout.");
    }

    static void Place(Transform tr, Vector2 pos, Vector2 size)
    {
        var rt = (RectTransform)tr;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Text(Transform root, string name, Vector2 pos, Vector2 size, float fontSize, Color color, string text = null)
    {
        var tr = root.Find(name);
        if (tr == null) { Debug.LogWarning("[GradeCard] missing " + name); return; }
        Place(tr, pos, size);
        var tmp = tr.GetComponent<TMP_Text>();
        if (tmp == null) return;
        tmp.fontSize = fontSize; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
        if (text != null) tmp.text = text;
        if (name == "Breakdown") tmp.alignment = TextAlignmentOptions.Top;
    }
}
