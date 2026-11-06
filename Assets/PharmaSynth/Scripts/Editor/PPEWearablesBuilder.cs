#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Wires the per-piece wearable PPE:
///   1. The locker's goggles + gloves become CLICKABLE (collider + XRSimpleInteractable
///      + PPEDonOnSelect), the coat display forwards Coat, and the legacy don-everything
///      paths are disabled (host donOnSelect off, coat display's old persistent calls cleared).
///   2. Worn visuals cloned from the locker models onto the mirror avatar's bones
///      (coat→Spine01, goggles→Head, gloves→hand bones — PlayerAvatar layer, mirror-only)
///      and first-person gloves onto the controllers (main-camera visible).
///   3. All visuals assigned to PPEController's per-piece arrays, initially hidden.
///
/// Tools ▸ PharmaSynth ▸ Build PPE Wearables (run in SampleScene AFTER Build Player
/// Avatar, edit mode, idempotent).
public static class PPEWearablesBuilder
{
    [MenuItem("Tools/PharmaSynth/Build PPE Wearables")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PPEWear] exit Play mode first."); return; }

        var locker = GameObject.Find("PPELocker");
        var ppe = locker != null ? locker.GetComponent<PPEController>() : null;
        if (ppe == null) { Debug.LogError("[PPEWear] no PPELocker/PPEController"); return; }
        var avatar = GameObject.Find("PlayerAvatar");
        if (avatar == null) { Debug.LogError("[PPEWear] no PlayerAvatar — run Build Player Avatar first"); return; }
        var xr = GameObject.Find("XR Origin (XR Rig)");

        // 1a. Host must NOT don everything on a stray click.
        var soPpe = new SerializedObject(ppe);
        soPpe.FindProperty("donOnSelect").boolValue = false;
        soPpe.ApplyModifiedProperties();

        // 1b. Clickable locker items → per-piece don.
        MakeClickable("LabCoatDisplay", ppe, PPEPiece.Coat, clearOldCalls: true);
        MakeClickable("Goggles_Standin", ppe, PPEPiece.Goggles, clearOldCalls: false);
        MakeClickable("Gloves_Standin", ppe, PPEPiece.Gloves, clearOldCalls: false);

        // 2. Worn visuals (idempotent: wipe previous clones).
        // (search INACTIVE too — worn visuals are hidden by default)
        var wipeNames = new HashSet<string> { "WornCoat", "WornGoggles", "WornGlove_L", "WornGlove_R", "FPGlove_L", "FPGlove_R" };
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && wipeNames.Contains(t.name)) Object.DestroyImmediate(t.gameObject);

        int layer = LayerMask.NameToLayer("PlayerAvatar");
        Transform Bone(string n)
        {
            foreach (var t in avatar.GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }
        Transform spine = Bone("Spine01"), headB = Bone("Head"), lHand = Bone("L_Hand"), rHand = Bone("R_Hand");
        var aRot = avatar.transform.rotation;
        var aFwd = avatar.transform.forward;
        var aUp = avatar.transform.up;

        var coatVis = new List<GameObject>();
        var gogglesVis = new List<GameObject>();
        var glovesVis = new List<GameObject>();

        // Coat draped on the torso.
        var coat = CloneOnto("CoatModel", "WornCoat", spine,
            spine.position + aFwd * 0.02f + aUp * 0.06f, aRot, 1.15f, layer);
        if (coat != null) coatVis.Add(coat);

        // Goggles across the eyes.
        var gog = CloneOnto("GogglesModel", "WornGoggles", headB,
            headB.position + aFwd * 0.08f + aUp * 0.12f, aRot, 0.85f, layer);
        if (gog != null) gogglesVis.Add(gog);

        // Mirror gloves on the hand bones.
        var mgL = CloneOnto("GloveModel_L", "WornGlove_L", lHand, lHand.position, aRot, 1.0f, layer);
        var mgR = CloneOnto("GloveModel_R", "WornGlove_R", rHand, rHand.position, aRot, 1.0f, layer);
        if (mgL != null) glovesVis.Add(mgL);
        if (mgR != null) glovesVis.Add(mgR);

        // First-person gloves on the controllers (default layer — main camera sees them).
        Transform FindDeep(string exact)
        {
            if (xr == null) return null;
            foreach (var t in xr.GetComponentsInChildren<Transform>(true)) if (t.name == exact) return t;
            return null;
        }
        var lCtrl = FindDeep("Left Controller");
        var rCtrl = FindDeep("Right Controller");
        var fpL = CloneOnto("GloveModel_L", "FPGlove_L", lCtrl,
            lCtrl != null ? lCtrl.position + lCtrl.forward * 0.04f : Vector3.zero,
            lCtrl != null ? lCtrl.rotation : Quaternion.identity, 1.0f, 0);
        var fpR = CloneOnto("GloveModel_R", "FPGlove_R", rCtrl,
            rCtrl != null ? rCtrl.position + rCtrl.forward * 0.04f : Vector3.zero,
            rCtrl != null ? rCtrl.rotation : Quaternion.identity, 1.0f, 0);
        if (fpL != null) glovesVis.Add(fpL);
        if (fpR != null) glovesVis.Add(fpR);

        foreach (var v in coatVis) v.SetActive(false);
        foreach (var v in gogglesVis) v.SetActive(false);
        foreach (var v in glovesVis) v.SetActive(false);

        // 3. Assign.
        ppe.BindVisuals(coatVis.ToArray(), gogglesVis.ToArray(), glovesVis.ToArray());
        EditorUtility.SetDirty(ppe);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(ppe.gameObject.scene);
        Debug.Log("<color=#4CD07D>[PPEWear] clickable coat/goggles/gloves + worn visuals: coat="
            + coatVis.Count + " goggles=" + gogglesVis.Count + " gloves=" + glovesVis.Count + "</color>");
    }

    static void MakeClickable(string name, PPEController ppe, PPEPiece piece, bool clearOldCalls)
    {
        var go = GameObject.Find(name);
        if (go == null) { Debug.LogWarning("[PPEWear] locker item '" + name + "' not found"); return; }

        if (go.GetComponent<Collider>() == null)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var box = go.AddComponent<BoxCollider>();
            if (rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                box.center = go.transform.InverseTransformPoint(b.center);
                var ls = go.transform.lossyScale;
                box.size = new Vector3(
                    Mathf.Abs(b.size.x / Mathf.Max(1e-4f, ls.x)),
                    Mathf.Abs(b.size.y / Mathf.Max(1e-4f, ls.y)),
                    Mathf.Abs(b.size.z / Mathf.Max(1e-4f, ls.z)));
            }
        }
        var it = go.GetComponent<XRSimpleInteractable>();
        if (it == null) it = go.AddComponent<XRSimpleInteractable>();
        if (clearOldCalls)
        {
            // The coat display's persistent call dons EVERYTHING; clear it.
            var soIt = new SerializedObject(it);
            var calls = soIt.FindProperty("m_SelectEntered.m_PersistentCalls.m_Calls");
            if (calls != null && calls.arraySize > 0)
            {
                Debug.Log("[PPEWear] cleared " + calls.arraySize + " old persistent call(s) on " + name);
                calls.ClearArray();
                soIt.ApplyModifiedProperties();
            }
        }
        var don = go.GetComponent<PPEDonOnSelect>();
        if (don == null) don = go.AddComponent<PPEDonOnSelect>();
        don.Bind(ppe, piece);
        EditorUtility.SetDirty(go);
    }

    /// Clone a locker model, keep its WORLD scale (times fit), parent to the bone.
    static GameObject CloneOnto(string srcName, string cloneName, Transform bone,
                                Vector3 worldPos, Quaternion worldRot, float fit, int layer)
    {
        var src = GameObject.Find(srcName);
        if (src == null || bone == null)
        { Debug.LogWarning("[PPEWear] skip " + cloneName + " (src=" + (src != null) + " bone=" + (bone != null) + ")"); return null; }

        var clone = Object.Instantiate(src);
        clone.name = cloneName;
        // Strip anything interactive/physical — it's a pure visual.
        foreach (var c in clone.GetComponentsInChildren<Component>(true))
            if (c is Collider || c is Rigidbody || c is ProximityLabel || c is XRSimpleInteractable || c is PPEDonOnSelect)
                Object.DestroyImmediate(c);
        clone.transform.SetPositionAndRotation(worldPos, worldRot);
        clone.transform.localScale = src.transform.lossyScale * fit;
        clone.transform.SetParent(bone, true);   // keep world pose; bone drives it afterwards
        SetLayerRecursive(clone, layer);
        Undo.RegisterCreatedObjectUndo(clone, "Build PPE Wearables");
        return clone;
    }

    static void SetLayerRecursive(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayerRecursive(c.gameObject, layer);
    }
}
#endif
