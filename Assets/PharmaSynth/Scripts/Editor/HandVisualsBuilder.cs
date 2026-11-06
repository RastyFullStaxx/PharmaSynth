#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// Controllers are REPLACED by skinned hands: XR Hands
/// sample meshes (Art/Hands/LeftHand.fbx + RightHand.fbx, real finger bones).
/// Two skins: bare (HandSkin.mat) / nitrile blue (HandNitrile.mat) driven by the
/// PPE gloves state; two poses: free / grab (finger curl while selecting) driven
/// by HandPoseController. Removes any procedural mittens, HandVisualKeeper
/// AND FPGlove_* first-person glove clones (PPE visuals bind to the
/// mirror gloves only; first-person gloving is the material swap).
///
/// Tools ▸ PharmaSynth ▸ Build Hand Visuals — run per scene, edit mode.
public static class HandVisualsBuilder
{
    const string HandL = "HandVisual_L";
    const string HandR = "HandVisual_R";
    const string SkinMatPath = "Assets/PharmaSynth/Art/Generated/HandSkin.mat";
    const string NitrileMatPath = "Assets/PharmaSynth/Art/Generated/HandNitrile.mat";
    const string LeftFbx = "Assets/PharmaSynth/Art/Hands/LeftHand.fbx";
    const string RightFbx = "Assets/PharmaSynth/Art/Hands/RightHand.fbx";

    [MenuItem("Tools/PharmaSynth/Build Hand Visuals")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[HandVisuals] exit Play mode first."); return; }

        var xr = GameObject.Find("XR Origin (XR Rig)");
        if (xr == null) { Debug.LogError("[HandVisuals] no 'XR Origin (XR Rig)' in the open scene."); return; }

        // The wrist watch may live under a previous HandVisual — rescue it onto
        // the controller before the wipe so a rebuild never destroys it.
        var watch = SceneFind.AnyByName("WristWatch");
        var leftCtrlPre = FindDeep(xr, "Left Controller");
        if (watch != null && leftCtrlPre != null && watch.transform.parent != leftCtrlPre)
            watch.transform.SetParent(leftCtrlPre, true);

        // Wipe previous generations: procedural mittens, glove clones, keepers.
        var wipe = new HashSet<string> { HandL, HandR, "FPGlove_L", "FPGlove_R" };
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && wipe.Contains(t.name)) Object.DestroyImmediate(t.gameObject);
        foreach (var k in Object.FindObjectsByType<HandVisualKeeper>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(k);

        // PPE: first-person gloving is the hand-material swap, so the glove
        // visual arrays keep only the mirror-avatar clones.
        var ppe = Object.FindFirstObjectByType<PPEController>(FindObjectsInactive.Include);
        if (ppe != null)
        {
            var coat = SceneFind.AnyByName("WornCoat"); var gog = SceneFind.AnyByName("WornGoggles");
            var gL = SceneFind.AnyByName("WornGlove_L"); var gR = SceneFind.AnyByName("WornGlove_R");
            ppe.BindVisuals(
                coat != null ? new[] { coat } : new GameObject[0],
                gog != null ? new[] { gog } : new GameObject[0],
                (gL != null && gR != null) ? new[] { gL, gR } : new GameObject[0]);
            EditorUtility.SetDirty(ppe);
        }

        var skin = LoadOrCreateMat(SkinMatPath, new Color(0.80f, 0.60f, 0.48f));
        var nitrile = LoadOrCreateMat(NitrileMatPath, new Color(0.16f, 0.42f, 0.85f));

        int made = 0;
        if (BuildHand(xr, "Left Controller", LeftFbx, HandL, "Left Controller Visual", skin, nitrile, ppe)) made++;
        if (BuildHand(xr, "Right Controller", RightFbx, HandR, "Right Controller Visual", skin, nitrile, ppe)) made++;

        // Mount the wrist watch at the hand's wrist.
        // Parent to the HAND ROOT, not the wrist
        // BONE: skinned-rig bone axes are twisted (X-along-bone), which puts the
        // watch on the back of the hand; the root has clean controller axes.
        if (watch != null)
        {
            var handRootL = FindDeep(xr, HandL);
            if (handRootL != null)
            {
                watch.transform.SetParent(handRootL, false);
                watch.transform.localPosition = new Vector3(0f, 0.010f, -0.056f);   // on the arm, behind the palm base
                // 90° Y kept only for the WristWatchController's watchAnchor.up
                // contract (face-up gesture); the visible geometry is the
                // measured FittedBand/Face built below.
                watch.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                ForgeFittedWatch(watch, handRootL);
                Debug.Log("[HandVisuals] fitted watch forged at the left wrist.");
            }
            else Debug.LogWarning("[HandVisuals] no left hand root — watch left on the controller.");
        }

        EditorUtility.SetDirty(xr);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(xr.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[HandVisuals] " + made + "/2 skinned hands built (free/grab poses, bare/nitrile skins).</color>");
    }

    static bool BuildHand(GameObject xr, string controllerName, string fbxPath, string handName,
                          string controllerVisualName, Material skin, Material nitrile, PPEController ppe)
    {
        var ctrl = FindDeep(xr, controllerName);
        if (ctrl == null) { Debug.LogWarning("[HandVisuals] controller '" + controllerName + "' not found."); return false; }
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
        if (fbx == null) { Debug.LogError("[HandVisuals] missing " + fbxPath); return false; }

        var hand = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        hand.name = handName;
        hand.transform.SetParent(ctrl, false);
        // Wrist sits slightly behind/below the grip so the palm lines up where a
        // held controller's grip would be.
        hand.transform.localPosition = new Vector3(0f, -0.03f, -0.06f);
        hand.transform.localRotation = Quaternion.identity;

        var smr = hand.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr != null) smr.sharedMaterial = skin;

        // Hide the default controller model — the hand replaces it.
        GameObject ctrlVisual = null;
        foreach (var t in ctrl.GetComponentsInChildren<Transform>(true))
            if (t.name == controllerVisualName) { ctrlVisual = t.gameObject; break; }
        if (ctrlVisual != null) ctrlVisual.SetActive(false);

        var pose = ctrl.GetComponent<HandPoseController>();
        if (pose == null) pose = ctrl.gameObject.AddComponent<HandPoseController>();
        pose.Bind(hand.transform, ctrlVisual, smr, skin, nitrile, ppe,
            ctrl.GetComponentsInChildren<XRBaseInteractor>(true));
        EditorUtility.SetDirty(pose);
        Undo.RegisterCreatedObjectUndo(hand, "Build Hand Visuals");
        return true;
    }

    const string BandMeshPath = "Assets/PharmaSynth/Art/Generated/WatchBandMesh.asset";

    /// Measured fitted watch, wrapped neatly round the wrist like a real
    /// watch. The solid Tripo WatchModel physically cannot encircle a wrist,
    /// so: bake the hand's skinned mesh, measure the wrist cross-section at the
    /// band z, generate an elliptical torus band around it, and cap it with a
    /// disc face + rim + crown. WatchModel is deactivated (kept as fallback).
    static void ForgeFittedWatch(GameObject watch, Transform handRootL)
    {
        var smr = handRootL.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null) { Debug.LogWarning("[HandVisuals] no hand SMR — watch not forged."); return; }

        // Wipe previous forge; hide the solid Tripo model.
        var prev = watch.transform.Find("FittedWatch");
        if (prev != null) Object.DestroyImmediate(prev.gameObject);
        var tripo = watch.transform.Find("WatchModel");
        if (tripo != null) tripo.gameObject.SetActive(false);

        // Measure the wrist slice in hand-root local space at the mount z.
        var baked = new Mesh();
        smr.BakeMesh(baked, true);
        var raw = baked.vertices;
        var pts = new System.Collections.Generic.List<Vector3>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
            pts.Add(handRootL.InverseTransformPoint(smr.transform.TransformPoint(raw[i])));
        Object.DestroyImmediate(baked);

        // Scan thin slabs along the arm stub and take the NARROWEST cross-section
        // (the true wrist neck). A single slab can catch the palm flare and give
        // a loose band.
        float sliceZ = watch.transform.localPosition.z;
        var slice = default(WatchMath.WristSlice);
        float bandZ = sliceZ, bestWidth = float.MaxValue;
        for (float z = -0.048f; z >= -0.0601f; z -= 0.003f)   // stop before the stub's end cap
        {
            var s = WatchMath.MeasureSlice(pts, z, 0.003f);
            if (s.samples < 10) continue;
            if (s.halfExtents.x < bestWidth) { bestWidth = s.halfExtents.x; slice = s; bandZ = z; }
        }
        if (slice.samples == 0) { slice = WatchMath.MeasureSlice(pts, sliceZ, 0.006f); bandZ = sliceZ; }
        var radii = WatchMath.BandRadii(slice.samples > 0 ? slice.halfExtents : new Vector2(0.024f, 0.016f));
        var centre = slice.samples > 0 ? slice.center : new Vector2(0f, 0.005f);
        Debug.Log("[HandVisuals] wrist neck: z=" + bandZ.ToString("F3") + " samples=" + slice.samples
            + " halfExtents=" + slice.halfExtents.ToString("F3") + " centre=" + centre.ToString("F3")
            + " -> band radii " + radii.ToString("F3"));

        // Assembly root: axes aligned to the HAND (band loop normal = forearm Z),
        // centred on the measured wrist centre. World-align then reparent keeps
        // the WristWatch root's own rotation (gesture contract) untouched.
        var fit = new GameObject("FittedWatch");
        fit.transform.SetParent(watch.transform, false);
        fit.transform.rotation = handRootL.rotation;
        fit.transform.position = handRootL.TransformPoint(new Vector3(centre.x, centre.y, bandZ));

        // Band: measured elliptical torus.
        var bandMesh = WatchMath.BuildBandMesh(radii, WatchMath.BandTube, 40, 12);
        SaveMeshAsset(bandMesh, BandMeshPath);
        var band = new GameObject("Band");
        band.transform.SetParent(fit.transform, false);
        band.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(BandMeshPath);
        band.AddComponent<MeshRenderer>().sharedMaterial =
            LoadOrCreateMat("Assets/PharmaSynth/Art/Generated/WatchBandBlack.mat", new Color(0.07f, 0.07f, 0.08f));

        // Face group on top of the band (+Y).
        float faceD = WatchMath.FaceDiameter(radii.x);
        float topY = radii.y + WatchMath.BandTube * 0.5f;
        GameObject Disc(string n, float d, float h, float y, Color c, string matPath)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            g.name = n;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(fit.transform, false);
            g.transform.localPosition = new Vector3(0f, y, 0f);
            g.transform.localScale = new Vector3(d, h * 0.5f, d);   // cylinder height = 2*scaleY
            g.GetComponent<Renderer>().sharedMaterial = LoadOrCreateMat(matPath, c);
            return g;
        }
        Disc("Rim", faceD + 0.004f, 0.006f, topY + 0.002f,
            new Color(0.62f, 0.64f, 0.66f), "Assets/PharmaSynth/Art/Generated/WatchSteel.mat");
        Disc("Face", faceD, 0.0035f, topY + 0.0045f,
            new Color(0.93f, 0.93f, 0.90f), "Assets/PharmaSynth/Art/Generated/WatchFaceWhite.mat");
        var crown = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crown.name = "Crown";
        Object.DestroyImmediate(crown.GetComponent<Collider>());
        crown.transform.SetParent(fit.transform, false);
        crown.transform.localPosition = new Vector3((faceD + 0.006f) * 0.5f, topY + 0.002f, 0f);
        crown.transform.localScale = new Vector3(0.004f, 0.0028f, 0.0028f);
        crown.GetComponent<Renderer>().sharedMaterial =
            LoadOrCreateMat("Assets/PharmaSynth/Art/Generated/WatchSteel.mat", new Color(0.62f, 0.64f, 0.66f));
    }

    /// Load-or-overwrite (AssetDatabase.DeleteAsset is gated in this project).
    static void SaveMeshAsset(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            AssetDatabase.SaveAssets();
        }
        else AssetDatabase.CreateAsset(mesh, path);
    }

    static Material LoadOrCreateMat(string path, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader) { color = color };
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        return mat;
    }

    static Transform FindDeep(GameObject root, string exact)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == exact) return t;
        return null;
    }
}
#endif
