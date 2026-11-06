#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations.Rigging;

/// Builds the mirror-only first-person avatar. Expects a rigged
/// humanoid prefab (Tripo image→3D + Tripo Rigging v1, casual clothes, T-pose) in
/// Art/Generated/Models with "player"/"avatar" in its name — or select it in the
/// Project. Places it under the XR rig, puts it on the PlayerAvatar layer (culled by
/// the main camera, shown by the mirror), and wires an Animation-Rigging IK setup:
/// two-bone IK on each arm (hands→controllers) + a head rotation constraint (head→HMD),
/// driven by PlayerAvatarRig. Raw-bone IK, so NO Humanoid retarget / T-pose calibration.
///
/// Tools ▸ PharmaSynth ▸ Build Player Avatar (run in SampleScene, edit mode).
public static class PlayerAvatarBuilder
{
    const float TargetHeight = 1.7f;
    const string ModelsDir = "Assets/PharmaSynth/Art/Generated/Models";

    [MenuItem("Tools/PharmaSynth/Build Player Avatar")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PlayerAvatar] exit Play mode first."); return; }

        var prefab = FindAvatarPrefab();
        if (prefab == null)
        {
            Debug.LogError("[PlayerAvatar] No avatar prefab found. Put a rigged humanoid prefab in " + ModelsDir
                + " with 'player' or 'avatar' in the name (or select it in the Project), then re-run.");
            return;
        }

        int layer = EnsureLayer("PlayerAvatar");
        if (layer < 0) { Debug.LogError("[PlayerAvatar] no free user layer slot"); return; }

        var xrOrigin = GameObject.Find("XR Origin (XR Rig)");
        if (xrOrigin == null) { Debug.LogError("[PlayerAvatar] no 'XR Origin (XR Rig)' in the active scene"); return; }

        var old = GameObject.Find("PlayerAvatar");
        if (old != null) Object.DestroyImmediate(old);

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.name = "PlayerAvatar";
        inst.transform.SetParent(xrOrigin.transform, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.identity;
        Undo.RegisterCreatedObjectUndo(inst, "Build Player Avatar");

        NormalizeHeight(inst, TargetHeight);
        SetLayerRecursive(inst, layer);

        // Tripo pivots at the model's CENTER — lift the root so the feet sit on the
        // rig's floor, and remember the lift for the runtime driver.
        float footOffset = FootOffset(inst);
        inst.transform.localPosition = new Vector3(0f, footOffset, 0f);

        // Bones (Tripo naming, tolerant of variants).
        var lUp = FindBone(inst.transform, "l_upperarm", "leftupperarm", "upperarm_l", "upperarm.l", "leftarm");
        var lFo = FindBone(inst.transform, "l_forearm", "leftforearm", "forearm_l", "forearm.l", "leftlowerarm");
        var lHa = FindBone(inst.transform, "l_hand", "lefthand", "hand_l", "hand.l");
        var rUp = FindBone(inst.transform, "r_upperarm", "rightupperarm", "upperarm_r", "upperarm.r", "rightarm");
        var rFo = FindBone(inst.transform, "r_forearm", "rightforearm", "forearm_r", "forearm.r", "rightlowerarm");
        var rHa = FindBone(inst.transform, "r_hand", "righthand", "hand_r", "hand.r");
        var head = FindBone(inst.transform, "head");
        var neck = FindBone(inst.transform, "neck", "necktwist");
        Debug.Log("[PlayerAvatar] bones LUp=" + N(lUp) + " LFo=" + N(lFo) + " LHa=" + N(lHa)
            + " RUp=" + N(rUp) + " RFo=" + N(rFo) + " RHa=" + N(rHa) + " Head=" + N(head) + " Neck=" + N(neck));

        // Animator + generic avatar (Animation Rigging needs the Animator's skeleton).
        var anim = inst.GetComponent<Animator>();
        if (anim == null) anim = inst.AddComponent<Animator>();
        if (anim.runtimeAnimatorController != null) anim.runtimeAnimatorController = null;
        if (anim.avatar == null)
        {
            var smr = inst.GetComponentInChildren<SkinnedMeshRenderer>();
            string rootName = smr != null && smr.rootBone != null ? smr.rootBone.name
                : (inst.transform.childCount > 0 ? inst.transform.GetChild(0).name : "");
            var av = AvatarBuilder.BuildGenericAvatar(inst, rootName);
            av.name = "PlayerAvatar_Generic";
            anim.avatar = av;
            Debug.Log("[PlayerAvatar] built generic avatar (root='" + rootName + "', valid=" + (av != null && av.isValid) + ")");
        }

        // Rig scaffold.
        var rigBuilder = inst.GetComponent<RigBuilder>();
        if (rigBuilder == null) rigBuilder = inst.AddComponent<RigBuilder>();

        var rigGo = new GameObject("AvatarRig");
        rigGo.transform.SetParent(inst.transform, false);
        rigGo.layer = layer;
        var rig = rigGo.AddComponent<Rig>();
        rigBuilder.layers = new List<RigLayer> { new RigLayer(rig, true) };

        Transform MakeTarget(string nm, Transform match)
        {
            var t = new GameObject(nm).transform;
            t.SetParent(rigGo.transform, false);
            if (match != null) t.SetPositionAndRotation(match.position, match.rotation);
            t.gameObject.layer = layer;
            return t;
        }
        var headT = MakeTarget("HeadTarget", head != null ? head : neck);
        var lHandT = MakeTarget("L_HandTarget", lHa);
        var rHandT = MakeTarget("R_HandTarget", rHa);
        var lElbow = MakeTarget("L_ElbowHint", lFo);
        var rElbow = MakeTarget("R_ElbowHint", rFo);

        AddTwoBoneIK(rigGo, "LeftArmIK", lUp, lFo, lHa, lHandT, lElbow, layer);
        AddTwoBoneIK(rigGo, "RightArmIK", rUp, rFo, rHa, rHandT, rElbow, layer);
        AddHeadRotation(rigGo, head != null ? head : neck, headT, layer);

        // Driver.
        var driver = inst.GetComponent<PlayerAvatarRig>();
        if (driver == null) driver = inst.AddComponent<PlayerAvatarRig>();
        var cam = FindRigCamera(xrOrigin);
        var lCtrl = FindDeep(xrOrigin.transform, "Left Controller");
        var rCtrl = FindDeep(xrOrigin.transform, "Right Controller");
        driver.Bind(cam, lCtrl, rCtrl, headT, lHandT, rHandT, lElbow, rElbow);
        driver.SetFootOffset(footOffset);
        Debug.Log("[PlayerAvatar] footOffset=" + footOffset.ToString("F3"));

        // Mirror-only: the main camera stops rendering the PlayerAvatar layer.
        if (cam != null)
        {
            var c = cam.GetComponent<Camera>();
            if (c != null) c.cullingMask &= ~(1 << layer);
        }

        EditorUtility.SetDirty(inst);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(inst.scene);
        Debug.Log("<color=#4CD07D>[PlayerAvatar] built on the rig from '" + prefab.name + "' (mirror-only, layer " + layer + ")</color>");
    }

    // ---- helpers ------------------------------------------------------------

    static void AddTwoBoneIK(GameObject parent, string name, Transform root, Transform mid, Transform tip,
                             Transform target, Transform hint, int layer)
    {
        if (root == null || mid == null || tip == null)
        { Debug.LogWarning("[PlayerAvatar] " + name + " skipped — missing arm bones"); return; }
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.layer = layer;
        var ik = go.AddComponent<TwoBoneIKConstraint>();
        var d = ik.data;
        d.root = root; d.mid = mid; d.tip = tip; d.target = target; d.hint = hint;
        // Position-only: the controller's rotation convention differs from the hand
        // bone's — driving wrist rotation raw twisted the hands backwards.
        // Arms reach the controllers; wrists keep their natural pose.
        d.targetPositionWeight = 1f; d.targetRotationWeight = 0f; d.hintWeight = 1f;
        ik.data = d;
    }

    static void AddHeadRotation(GameObject parent, Transform headBone, Transform target, int layer)
    {
        if (headBone == null || target == null)
        { Debug.LogWarning("[PlayerAvatar] head rotation skipped — no head bone"); return; }
        var go = new GameObject("HeadAim");
        go.transform.SetParent(parent.transform, false);
        go.layer = layer;
        var rot = go.AddComponent<MultiRotationConstraint>();
        var d = rot.data;
        d.constrainedObject = headBone;
        var arr = new WeightedTransformArray(0);
        arr.Add(new WeightedTransform(target, 1f));
        d.sourceObjects = arr;
        rot.data = d;
    }

    static GameObject FindAvatarPrefab()
    {
        if (Selection.activeObject is GameObject sg && PrefabUtility.IsPartOfPrefabAsset(sg)) return sg;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { ModelsDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var nm = Path.GetFileNameWithoutExtension(path).ToLower();
            if ((nm.Contains("player") || nm.Contains("avatar")) && !nm.Contains("jimenez"))
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    static int EnsureLayer(string name)
    {
        var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (asset == null || asset.Length == 0) return -1;
        var tm = new SerializedObject(asset[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < 32; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == name) return i;
        for (int i = 8; i < 32; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = name; tm.ApplyModifiedProperties(); return i; }
        }
        return -1;
    }

    /// Distance from the root pivot down to the lowest renderer point (the feet).
    static float FootOffset(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return go.transform.position.y - b.min.y;
    }

    static void NormalizeHeight(GameObject go, float target)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;
        Bounds b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        float h = b.size.y;
        if (h < 1e-3f) return;
        go.transform.localScale *= target / h;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }

    static Transform FindBone(Transform root, params string[] candidates)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name.ToLower();
            foreach (var cand in candidates) if (n.Contains(cand)) return t;
        }
        return null;
    }

    static Transform FindDeep(Transform root, string exactName)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == exactName) return t;
        return null;
    }

    static Transform FindRigCamera(GameObject xrOrigin)
    {
        foreach (var c in xrOrigin.GetComponentsInChildren<Camera>(true))
            if (c.CompareTag("MainCamera") && c.gameObject.activeInHierarchy) return c.transform;
        var any = xrOrigin.GetComponentInChildren<Camera>(true);
        return any != null ? any.transform : null;
    }

    static string N(Transform t) => t != null ? t.name : "NULL";
}
#endif
