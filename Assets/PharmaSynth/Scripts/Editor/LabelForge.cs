#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// Reagent-label compositor (style: MODERN). For every
/// Reagent_* bottle on the shelf: renders LabelBase_Modern + the chemical's
/// name (crisp TMP text) to a PNG, builds a material,
/// and mounts a label quad on the bottle facing the aisle.
/// Tools ▸ PharmaSynth ▸ Generate Reagent Labels — idempotent, re-run anytime.
public static class LabelForge
{
    const string BasePath = "Assets/PharmaSynth/Art/Generated/Labels/LabelBase_Modern.png";
    const string OutDir = "Assets/PharmaSynth/Art/Generated/Labels/Composited";
    const int W = 512, H = 683;

    [MenuItem("Tools/PharmaSynth/Generate Reagent Labels")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabelForge] exit Play mode first."); return; }
        var baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(BasePath);
        if (baseTex == null) { Debug.LogError("[LabelForge] missing base " + BasePath); return; }
        Directory.CreateDirectory(OutDir);

        int made = 0;
        // The west cubby (ReagentShelf) is empty, its stock lives in the east cabinets, so
        // a missing/empty ReagentShelf is NORMAL. It must not abort the run
        // before the cabinets below get their labels.
        var shelf = GameObject.Find("ReagentShelf");
        if (shelf != null)
            foreach (Transform bottle in shelf.transform)
            {
                var lp = bottle.GetComponent<LiquidPhysics>();
                string chem = lp != null && lp.currentChemical != null ? lp.currentChemical.chemicalName : null;
                if (string.IsNullOrEmpty(chem)) continue;
                MountQuad(bottle, EnsureLabelMat(baseTex, chem), RoomOutward(bottle.position));
                made++;
            }

        // Raw-reagent cabinets: the same crisp labels on every
        // stocked bottle/jar; boxes and the ice bucket (not tubular) get a FLAT
        // label plate on their open face instead of a curved band. Tiny singles
        // (matchsticks, litmus strips) stay label-free — the hover card names them.
        // BOTH roots: the cabinets hold the bottles, and LabConsumables holds the
        // hand-placed ice bucket + paper/stick boxes, which are NOT reagents and live
        // outside the cabinets. Scanning only "ReagentCabinets"
        // silently leaves every consumable card-less.
        foreach (var rootName in new[] { "ReagentCabinets", "LabConsumables" })
        {
            var cabinets = GameObject.Find(rootName);
            if (cabinets == null) continue;
            foreach (var lp in cabinets.GetComponentsInChildren<LiquidPhysics>(true))
            {
                string chem = lp.currentChemical != null ? lp.currentChemical.chemicalName : null;
                if (string.IsNullOrEmpty(chem)) continue;
                MountQuad(lp.transform, EnsureLabelMat(baseTex, chem), CabinetOutward(lp.transform));
                made++;
            }
            foreach (var item in cabinets.GetComponentsInChildren<LabItem>(true))
            {
                if (item.GetComponent<LiquidPhysics>() != null) continue;   // bottles handled above
                if (!item.name.StartsWith("Raw_")) continue;                // singles ride beside boxes
                var rs = item.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                if (Mathf.Max(b.size.x, b.size.z) < 0.05f) continue;        // too small to label
                MountFlat(item.transform, EnsureLabelMat(baseTex, item.displayName), CabinetOutward(item.transform), b);
                made++;
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log("<color=#4CD07D>[LabelForge] composited + mounted " + made + " labels (shelf + cabinets)</color>");
    }

    /// Composite (or refresh) the label texture + material for one chemical name.
    static Material EnsureLabelMat(Texture2D baseTex, string chem)
    {
        string safe = chem.Replace(" ", "").Replace("%", "").Replace("/", "-").Replace("'", "");
        string pngPath = OutDir + "/Label_" + safe + ".png";
        RenderLabel(baseTex, chem, pngPath);

        string matPath = OutDir + "/Label_" + safe + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, matPath);
        }
        mat.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
        mat.SetFloat("_Smoothness", 0.15f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// Label direction for shelf bottles: toward the room centre (legacy heuristic).
    static Vector3 RoomOutward(Vector3 pos)
    {
        Vector3 outward = new Vector3(0.2f, 0f, -2.5f) - pos;
        outward.y = 0f;
        return outward.normalized;
    }

    /// Label direction for cabinet stock: the owning unit's OPEN face (local −x
    /// by construction) — correct even after the user moves/rotates a unit.
    static Vector3 CabinetOutward(Transform t)
    {
        for (var p = t.parent; p != null; p = p.parent)
            if (p.name.StartsWith("CabinetUnit_")) return -p.right;
        return RoomOutward(t.position);
    }

    /// Flat label plate for non-tubular stock (boxes, the ice bucket): a small
    /// quad hovering just off the outward face, sized to the item.
    static void MountFlat(Transform item, Material mat, Vector3 outward, Bounds b)
    {
        var old = item.Find("NameLabel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "NameLabel";
        Object.DestroyImmediate(quad.GetComponent<Collider>());
        quad.transform.SetParent(item, true);
        quad.GetComponent<MeshRenderer>().sharedMaterial = mat;

        float w = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.85f, 0.04f, 0.14f);
        float h = Mathf.Clamp(b.size.y * 0.6f, 0.025f, 0.09f);
        quad.transform.rotation = Quaternion.LookRotation(-outward);   // Quad front is −Z
        var ls = quad.transform.lossyScale;
        quad.transform.localScale = new Vector3(
            quad.transform.localScale.x * w / Mathf.Max(ls.x, 1e-4f),
            quad.transform.localScale.y * h / Mathf.Max(ls.y, 1e-4f),
            quad.transform.localScale.z);
        float faceDist = Mathf.Abs(Vector3.Dot(b.extents, outward)) + 0.007f;
        quad.transform.position = b.center + outward * faceDist;
    }

    /// Render base + centred black name text via an off-scene ortho camera.
    static void RenderLabel(Texture2D baseTex, string chem, string pngPath)
    {
        var root = new GameObject("~LabelForge");
        try
        {
            var far = new Vector3(1000f, 1000f, 1000f);
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.transform.SetParent(root.transform);
            quad.transform.position = far;
            quad.transform.localScale = new Vector3(0.75f, 1f, 1f);
            var unlit = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { mainTexture = baseTex };
            quad.GetComponent<MeshRenderer>().sharedMaterial = unlit;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(root.transform);
            // Quad faces -Z, camera sits at z-1 looking +Z; text floats just in front.
            textGo.transform.position = far + new Vector3(0f, -0.06f, -0.01f);
            textGo.transform.rotation = Quaternion.identity;
            var tmp = textGo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.5f);
            tmp.text = chem;
            tmp.color = Color.black;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.2f; tmp.fontSizeMax = 1.6f;
            tmp.ForceMeshUpdate();

            var camGo = new GameObject("Cam");
            camGo.transform.SetParent(root.transform);
            camGo.transform.position = far + new Vector3(0f, 0f, -1f);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 0.5f;
            cam.nearClipPlane = 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.white;

            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(unlit);
            AssetDatabase.ImportAsset(pngPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    /// Curved label band hugging the bottle (flat quads read
    /// as pasted nameplates). Thin tubes get a small PORTRAIT band (the modern
    /// base is portrait anyway) — every reagent is named on the glass.
    const float MinLabelRadius = 0.005f;

    static void MountQuad(Transform bottle, Material mat, Vector3 outward)
    {
        var old = bottle.Find("NameLabel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var rs = bottle.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);

        // Measure the GLASS where the band sits, not the whole object: the bounds of every
        // child (spout lip, shoulder, fill layers) float the band off the bottle and past
        // the beakers' sides. Body = the largest mesh; slice its vertices at band height.
        float bandY = b.center.y - b.size.y * 0.08f;
        Vector3 axis; float radius;
        if (!GlassSlice(bottle, bandY, b.size.y * 0.42f, out axis, out radius))
        { axis = b.center; radius = Mathf.Max(b.extents.x, b.extents.z); }
        if (radius < MinLabelRadius) return;                     // thin tube: skip

        outward.y = 0f; outward = outward.normalized;

        var band = new GameObject("NameLabel", typeof(MeshFilter), typeof(MeshRenderer));
        band.transform.SetParent(bottle, true);
        band.GetComponent<MeshFilter>().sharedMesh = ArcMesh();
        band.GetComponent<MeshRenderer>().sharedMaterial = mat;

        // Sit the band JUST PROUD of the glass, never inside it. Because the
        // vessels are transparent, a band tucked at <100% radius reads as
        // embedded/"half-buried". Float it a hair outside the widest
        // extent so the whole 140° arc clears the surface all along its length.
        float bandRadius = radius + 0.003f;
        // Wide vessels: squat band; thin tubes: small portrait band (min 3.5 cm
        // so the name stays legible up close).
        float bandHeight = Mathf.Min(b.size.y * 0.42f, Mathf.Max(bandRadius * 2.4f, 0.035f));
        band.transform.localScale = Vector3.one;
        var ls = band.transform.lossyScale;
        band.transform.localScale = new Vector3(bandRadius / Mathf.Max(ls.x, 1e-4f),
                                                bandHeight / Mathf.Max(ls.y, 1e-4f),
                                                bandRadius / Mathf.Max(ls.z, 1e-4f));
        band.transform.position = new Vector3(axis.x, bandY, axis.z);
        band.transform.rotation = Quaternion.LookRotation(-outward);        // arc centre (-Z) toward the room
    }

    /// Fit the band to the glass body (the largest mesh), not to every child: the axis is
    /// the body's own bounds centre, and the radius the 90th-percentile vertex distance at
    /// band height — the outer wall. The vial model carries a modelled label plate on one
    /// side; a max-distance or min/max-centre fit follows that bulge ~2 cm off axis, so
    /// the percentile deliberately ignores its few vertices.
    static bool GlassSlice(Transform bottle, float y, float height, out Vector3 axis, out float radius)
    {
        axis = Vector3.zero; radius = 0f;
        MeshFilter body = null; float best = 0f;
        foreach (var mf in bottle.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null || mf.name == "NameLabel") continue;
            var r = mf.GetComponent<Renderer>();
            if (r == null) continue;
            float vol = r.bounds.size.x * r.bounds.size.y * r.bounds.size.z;
            if (vol > best) { best = vol; body = mf; }
        }
        if (body == null) return false;
        var bb = body.GetComponent<Renderer>().bounds;
        axis = new Vector3(bb.center.x, y, bb.center.z);
        var m = body.transform.localToWorldMatrix;
        float lo = y - height * 0.5f, hi = y + height * 0.5f;
        var d = new System.Collections.Generic.List<float>();
        foreach (var v in body.sharedMesh.vertices)
        {
            var w = m.MultiplyPoint3x4(v);
            if (w.y < lo || w.y > hi) continue;
            d.Add(new Vector2(w.x - axis.x, w.z - axis.z).magnitude);
        }
        if (d.Count < 8) return false;
        d.Sort();
        radius = d[Mathf.Clamp(Mathf.RoundToInt(d.Count * 0.9f), 0, d.Count - 1)];
        return true;
    }

    static Mesh _arc;

    /// Shared unit arc mesh: radius 1, height 1, 120° facing -Z. Saved as an
    /// asset so scene references survive; scaled per bottle by the transform.
    static Mesh ArcMesh()
    {
        string path = OutDir + "/LabelArc120.asset";   // 140° wrapped names round the sides
        if (_arc == null) _arc = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (_arc != null) return _arc;

        const int SEG = 16;
        const float ARC = 120f * Mathf.Deg2Rad;
        var verts = new Vector3[(SEG + 1) * 2];
        var uvs = new Vector2[verts.Length];
        var norms = new Vector3[verts.Length];
        var tris = new int[SEG * 6];
        for (int i = 0; i <= SEG; i++)
        {
            float a = -ARC * 0.5f + ARC * i / SEG;
            float x = Mathf.Sin(a), z = -Mathf.Cos(a);
            verts[i * 2] = new Vector3(x, -0.5f, z);
            verts[i * 2 + 1] = new Vector3(x, 0.5f, z);
            float u = (float)i / SEG;
            uvs[i * 2] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
            norms[i * 2] = norms[i * 2 + 1] = new Vector3(x, 0f, z);
        }
        for (int i = 0; i < SEG; i++)
        {
            // Wound so faces point OUTWARD (away from the bottle axis).
            int v = i * 2, t = i * 6;
            tris[t] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
            tris[t + 3] = v + 1; tris[t + 4] = v + 3; tris[t + 5] = v + 2;
        }
        _arc = new Mesh { name = "LabelArc", vertices = verts, uv = uvs, normals = norms, triangles = tris };
        AssetDatabase.CreateAsset(_arc, path);
        return _arc;
    }
}
#endif
