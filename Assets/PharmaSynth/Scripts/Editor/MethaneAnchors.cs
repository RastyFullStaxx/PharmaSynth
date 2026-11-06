#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Creates the draggable placement anchors the verbs read. Each match/burner
/// gets a "FlameAnchor" child and each scoopula/spatula a "ScoopAnchor" child,
/// dropped at a best-guess spot. SELECT it in the Hierarchy (orange gizmo in the
/// Scene view), drag it onto the exact part (match head, burner mouth, scoop
/// bowl), then run Lock My Layout to bake it. Idempotent: never moves an anchor
/// you've already positioned.
public static class MethaneAnchors
{
    [MenuItem("Tools/PharmaSynth/Add Placement Anchors (drag to fine-tune)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Anchors] exit Play mode first."); return; }

        int flames = 0, scoops = 0;

        foreach (var m in Object.FindObjectsByType<Matchstick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EnsureAnchor(m.gameObject, "FlameAnchor", TipGuess(m.gameObject, positiveEnd: false))) flames++;

        foreach (var burner in Object.FindObjectsByType<BurnerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EnsureAnchor(burner.gameObject, "FlameAnchor", BarrelTop(burner.gameObject))) flames++;

        foreach (var s in Object.FindObjectsByType<ScoopController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EnsureAnchor(s.gameObject, "ScoopAnchor", TipGuess(s.gameObject, positiveEnd: false))) scoops++;

        // Mortars get a BowlAnchor = where the ground powder + dust appear (you
        // drag it into the bowl so the heap sits right).
        int bowls = 0;
        foreach (var g in Object.FindObjectsByType<GrindController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (EnsureAnchor(g.gameObject, "BowlAnchor", BowlGuess(g.gameObject))) bowls++;

        // Solid RECEIVERS (the hard-glass tube) get a PowderAnchor: drag it where
        // the powder should sit AND SCALE it to set the powder's size by hand
        // Its scale = the size at a full charge.
        int powders = 0;
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li == null || li.itemId != "glass-tube") continue;
            if (EnsureAnchor(li.gameObject, "PowderAnchor", BowlGuess(li.gameObject), PowderScale(li.gameObject)))
                powders++;
        }

        // Droppers: the dropper is a SKINNED mesh, and
        // SkinnedMeshRenderer.bounds come from the bind pose — every bounds-derived
        // "tip" landed in mid-air, so the charge bead floated OUTSIDE the tool.
        // Hand-fitted children end that for good:
        //   DropperTip    — drag onto the real tip (probe + droplet origin; the
        //                   controller already prefers it over bounds).
        //   DropperLiquid — a capsule; MOVE + SCALE it to sit INSIDE the stem.
        //                   Its authored size = a FULL charge; runtime tints it
        //                   with the loaded chemical and drains it per squeeze.
        int tips = 0, liquids = 0;
        foreach (var d in Object.FindObjectsByType<DropperController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (EnsureAnchor(d.gameObject, "DropperTip", TipGuess(d.gameObject, positiveEnd: false))) tips++;
            if (d.transform.Find("DropperLiquid") == null)
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                cap.name = "DropperLiquid";
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.transform.SetParent(d.transform, false);
                cap.transform.localPosition = Vector3.zero;
                // World-size the first guess (~1 cm x 5 cm sliver) against the parent's
                // import scale, so it starts visible instead of microscopic or huge.
                var ls = d.transform.lossyScale;
                cap.transform.localScale = new Vector3(
                    0.005f / Mathf.Max(ls.x, 1e-4f),
                    0.025f / Mathf.Max(ls.y, 1e-4f),
                    0.005f / Mathf.Max(ls.z, 1e-4f));
                var r = cap.GetComponent<Renderer>();
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                r.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                    { name = "DropperLiquid_" + d.name, color = new Color(0.65f, 0.85f, 1f, 1f) };
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                liquids++;
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[Anchors] {flames} FlameAnchor + {scoops} ScoopAnchor + {bowls} BowlAnchor + "
                  + $"{powders} PowderAnchor + {tips} DropperTip + {liquids} DropperLiquid placed. "
                  + "Select each in the Hierarchy and drag the marker onto the exact part (match head / burner "
                  + "mouth / scoop bowl / inside the mortar bowl / dropper tip). "
                  + "The PowderAnchor on the hard-glass tube also takes its SCALE — set it to the powder size you "
                  + "want. Fit each pale-blue DropperLiquid capsule INSIDE its dropper's stem (move + scale; its "
                  + "size = a full charge). Then run Lock My Layout.</color>");
    }

    /// Add a named anchor child at a guess position/scale — but leave any existing
    /// one exactly where (and at whatever size) it was hand-set.
    static bool EnsureAnchor(GameObject host, string name, Vector3 worldPos, Vector3? localScale = null)
    {
        var existing = host.transform.Find(name);
        if (existing != null)
        {
            var pa0 = existing.GetComponent<PlacementAnchor>() ?? existing.gameObject.AddComponent<PlacementAnchor>();
            // Only a size-setting anchor previews its scale — a position-only anchor
            // has scale 1 and would draw a 1 m sphere over the bench.
            pa0.previewsScale = localScale.HasValue;
            EditorUtility.SetDirty(existing.gameObject);
            return false;   // respect the hand placement + hand-set size
        }
        var go = new GameObject(name);
        go.transform.SetParent(host.transform, true);
        go.transform.position = worldPos;
        go.transform.localRotation = Quaternion.identity;
        if (localScale.HasValue) go.transform.localScale = localScale.Value;
        var pa = go.AddComponent<PlacementAnchor>();
        pa.previewsScale = localScale.HasValue;
        EditorUtility.SetDirty(host);
        return true;
    }

    /// A sensible starting size for a powder charge inside this vessel: a squat
    /// mound about half the bore, to be scaled by hand afterwards.
    static Vector3 PowderScale(GameObject go)
    {
        var lb = ExperimentSceneBuilder.LocalMeshBounds(go.transform, "Powder", "Liquid", "CollectedGas");
        int ax = ExperimentSceneBuilder.LongestAxis(lb.size);
        float bore = ExperimentSceneBuilder.BoreOf(lb.size, ax);
        float w = Mathf.Max(1e-4f, bore * 0.5f);
        return new Vector3(w, Mathf.Max(1e-4f, lb.size[ax] * 0.12f), w);
    }

    /// Far end of the longest local axis — the tip guess for a match head / scoop bowl.
    static Vector3 TipGuess(GameObject go, bool positiveEnd)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        Vector3 axis = Matchstick.LongestLocalAxis(go.transform, b, out float half);
        return b.center + axis * (half * (positiveEnd ? 1f : -1f));
    }

    /// Interior of the mortar bowl — the middle, a little above centre.
    static Vector3 BowlGuess(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return new Vector3(b.center.x, b.center.y + b.size.y * 0.1f, b.center.z);
    }

    /// Top-centre of the tallest renderer (the burner barrel mouth).
    static Vector3 BarrelTop(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return go.transform.position + Vector3.up * 0.12f;
        Renderer top = rs[0];
        for (int i = 1; i < rs.Length; i++) if (rs[i].bounds.max.y > top.bounds.max.y) top = rs[i];
        var tb = top.bounds;
        return new Vector3(tb.center.x, tb.max.y + 0.01f, tb.center.z);
    }
}
#endif
