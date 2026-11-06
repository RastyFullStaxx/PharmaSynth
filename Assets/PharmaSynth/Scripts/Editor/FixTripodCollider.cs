#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// The tripod is a STAND: the burner goes underneath,
/// wire gauze + flask on top. A single CONVEX hull collider (required for a
/// dynamic/grabbable body) fills the open frame so nothing fits below.
/// A tripod can't be both grabbable-dynamic AND hollow. This makes it
/// a KINEMATIC stand with a NON-CONVEX mesh collider that matches the real open
/// legs+ring, so the burner fits underneath and items rest on top. Still grabbable
/// (moves while held), but it stays where you drop it instead of tumbling.
/// Idempotent.
public static class FixTripodCollider
{
    [MenuItem("Tools/PharmaSynth/Fix Tripod Collider (open stand)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Tripod] exit Play mode first."); return; }

        int fixedCount = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = t.gameObject;
            if (PhysicsAudit.PrefabNameFor(go) != "Tripod" && !go.name.Contains("Tripod")) continue;

            // 1) Drop every existing collider (the solid convex hull + any box).
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // 2) Non-convex mesh collider(s) matching the open frame. Non-convex is
            //    only legal on a NON-dynamic body, so the tripod becomes kinematic.
            int meshes = 0;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                mc.convex = false;   // open frame — burner fits inside
                meshes++;
            }
            if (meshes == 0)   // no mesh? fall back to a thin top platform so gauze still rests
            {
                var b = WorldBounds(go);
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(new Vector3(b.center.x, b.max.y - 0.01f, b.center.z));
                box.size = new Vector3(b.size.x, 0.02f, b.size.z);
            }

            // 3) Kinematic stand: stays where dropped (a non-convex collider can't
            //    be dynamic). Remove the dynamic-on-release policy; keep the grab.
            var rb = go.GetComponent<Rigidbody>() ?? go.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var pol = go.GetComponent<GrabPhysicsPolicy>();
            if (pol != null) Object.DestroyImmediate(pol);
            var grab = go.GetComponent<XRGrab>();
            if (grab != null) grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable
                                                    .MovementType.Kinematic;
            EditorUtility.SetDirty(go);
            fixedCount++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[Tripod] {fixedCount} tripod(s) are now open kinematic stands — burner fits underneath, "
                  + "gauze/flask rest on top. Grab to reposition; it stays where you drop it.</color>");
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.1f);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
#endif
