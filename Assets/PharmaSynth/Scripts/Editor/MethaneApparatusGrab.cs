#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Guarantee the methane apparatus is pick-up-able ("I can't pick
/// up the draw tubes"). Normalises every common grab-blocker on the hard-glass
/// tube, collection tube and burner: active, on the Default layer, with an
/// XRGrabInteractable (velocity-tracked + two-handed), a Rigidbody, a live convex
/// collider, and the shelf/respawn policy. Idempotent.
public static class MethaneApparatusGrab
{
    static readonly HashSet<string> Ids = new HashSet<string>
    { "glass-tube", "collection-tube", "delivery-tube", "burner" };

    [MenuItem(SceneRepairs.MenuRoot + "Fix Methane Apparatus Grab")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ApparatusGrab] exit Play mode first."); return; }

        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        int fixedCount = 0;
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li == null || !Ids.Contains(li.itemId)) continue;
            var go = li.gameObject;

            if (!go.activeSelf) go.SetActive(true);
            SetLayer(go, 0);   // Default — the hand ray/poke masks include it

            // The hard-glass tube must RECEIVE the ground mixture scooped from the
            // mortar (load the tube, then heat it). It's a solid
            // receiver; LiquidPhysics.Start won't touch its own mesh (opaque host).
            if (li.itemId == "glass-tube" && go.GetComponent<LiquidPhysics>() == null)
            {
                var tlp = go.AddComponent<LiquidPhysics>();
                tlp.registry = registry;
                tlp.SetContents(null, 0f);
            }

            var grab = go.GetComponent<XRGrab>() ?? go.AddComponent<XRGrab>();
            grab.enabled = true;
            GrabTuning.Apply(grab);

            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            EnsureCollider(go, rb);

            if (go.GetComponent<GrabPhysicsPolicy>() == null) go.AddComponent<GrabPhysicsPolicy>().Bind(rb, grab);
            if (go.GetComponent<HoverHighlight>() == null) go.AddComponent<HoverHighlight>().Bind(grab);
            var dr = go.GetComponent<DropRespawn>() ?? go.AddComponent<DropRespawn>();
            dr.Bind(rb, grab);
            dr.SetHome(go.transform.position, go.transform.rotation);

            EditorUtility.SetDirty(go);
            fixedCount++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[ApparatusGrab] {fixedCount} methane apparatus item(s) made grabbable "
                  + "(active, Default layer, velocity-tracked grab, live collider). Try picking up the tubes now."
                  + (fixedCount == 0 ? "  ⚠ none found — no glass-tube/collection-tube/burner LabItems in the scene." : "")
                  + "</color>");
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
    }

    /// Make sure at least one enabled, non-trigger, convex collider exists.
    static void EnsureCollider(GameObject go, Rigidbody rb)
    {
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            if (c.enabled && !c.isTrigger)
            {
                if (c is MeshCollider mc && !mc.convex) mc.convex = true;   // dynamic bodies need convex
                return;
            }
        // None usable — build one from a mesh, else a box around the renderers.
        var mf = go.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            var mc = mf.gameObject.AddComponent<MeshCollider>();
            mc.sharedMesh = mf.sharedMesh; mc.convex = true;
            return;
        }
        var rends = go.GetComponentsInChildren<Renderer>();
        var box = go.AddComponent<BoxCollider>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            box.center = go.transform.InverseTransformPoint(b.center);
            box.size = go.transform.InverseTransformVector(b.size);
        }
    }
}
#endif
