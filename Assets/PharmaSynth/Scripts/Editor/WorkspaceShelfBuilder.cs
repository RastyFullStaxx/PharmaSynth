#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds the equipment-shelf platforms on the center-table overhead gantry.
/// One row sits on the rail tops; this also adds the SECOND, lower row at
/// y≈1.20 (clean full-width tiles + slim side posts, so the lower row reads as
/// built in, not floating). Idempotent + re-runnable. Geometry lives in the pure
/// WorkspaceShelfMath; the apparatus kits go on via Build Workspace Kits.
public static class WorkspaceShelfBuilder
{
    const string RootName = "WorkspaceShelf";
    const int TileCount = 3;

    [MenuItem("Tools/PharmaSynth/Build Workspace Shelf")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[WorkspaceShelf] exit Play mode first."); return; }
        if (ManualLayoutAdopter.LayoutIsManual())
        {
            Debug.LogError("[WorkspaceShelf] the scene layout is HAND-PLACED (ManualLayout_W512 marker) — "
                + "a rebuild would clobber it. Delete the marker object to force a rebuild.");
            return;
        }

        // 1) Remove ad-hoc planks (duplicated cabinet shelves parked
        // over the table — both the original single and the lower-row four)
        // and any previous WorkspaceShelf.
        int removed = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            bool adHocPlank = (t.name.StartsWith("Shelf_1.3 (") || t.name.StartsWith("Shelf_0.5"))
                              && t.position.y > 1.1f && t.position.z < -2.5f;
            if (adHocPlank) { Object.DestroyImmediate(t.gameObject); removed++; }
        }
        var existing = GameObject.Find("Environment/" + RootName) ?? GameObject.Find(RootName);
        if (existing != null) { Object.DestroyImmediate(existing); removed++; }

        // 2) Root under Environment (falls back to scene root).
        var env = GameObject.Find("Environment");
        var root = new GameObject(RootName);
        if (env != null) root.transform.SetParent(env.transform, true);

        var mat = ShelfMaterial();

        // 3) Full-width flush tiles on BOTH rows.
        for (int row = 0; row < WorkspaceShelfMath.Rows; row++)
        {
            for (int i = 0; i < TileCount; i++)
            {
                var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tile.name = "ShelfTile_R" + row + "_" + i;
                tile.transform.SetParent(root.transform, true);
                tile.transform.position = WorkspaceShelfMath.TileCenter(i, TileCount, row);
                tile.transform.localScale = WorkspaceShelfMath.TileSize(TileCount);
                tile.GetComponent<Renderer>().sharedMaterial = mat;
                // Keep the BoxCollider the primitive ships with (props seat on it).
            }
            // Subtle back-lip per row so items can't roll off the far edge.
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lip.name = "ShelfBackLip_R" + row;
            lip.transform.SetParent(root.transform, true);
            lip.transform.position = WorkspaceShelfMath.LipCenter(row);
            lip.transform.localScale = WorkspaceShelfMath.LipSize;
            lip.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // 4) Slim side posts hang the lower row from the top one (visual
        // grounding — the hand-laid planks floated in mid-air).
        float postTop = WorkspaceShelfMath.RowCenterY(0) - WorkspaceShelfMath.Thickness * 0.5f;
        float postBottom = WorkspaceShelfMath.RowCenterY(1) - WorkspaceShelfMath.Thickness * 0.5f;
        float postH = postTop - postBottom;
        foreach (float x in new[] { WorkspaceShelfMath.XMin + 0.02f, 0f, WorkspaceShelfMath.XMax - 0.02f })
        {
            var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "ShelfPost_" + x.ToString("0.00");
            post.transform.SetParent(root.transform, true);
            post.transform.position = new Vector3(x, postBottom + postH * 0.5f, WorkspaceShelfMath.ZCenter);
            post.transform.localScale = new Vector3(0.03f, postH, 0.03f);
            post.GetComponent<Renderer>().sharedMaterial = mat;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WorkspaceShelf] built {WorkspaceShelfMath.Rows} rows x {TileCount} tiles + lips + posts " +
                  $"(tops y={WorkspaceShelfMath.TopYOf(0):F3}/{WorkspaceShelfMath.TopYOf(1):F3}); cleared {removed} old object(s).");
    }

    /// Match the bench: reuse Table_1's board material, else a neutral lab grey.
    static Material ShelfMaterial()
    {
        var table = GameObject.Find("Table_1") ?? GameObject.Find("Environment/Table_1");
        if (table != null)
        {
            var r = table.GetComponentInChildren<Renderer>();
            if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
        }
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        var m = new Material(sh) { name = "WorkspaceShelfBoard" };
        m.color = new Color(0.30f, 0.33f, 0.40f);   // dark bench grey-blue
        return m;
    }
}
#endif
