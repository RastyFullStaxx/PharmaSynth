#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Swaps the sealed Tripo fume-hood model (a sash, but not hollow inside) for the
/// OPEN-SASH, hollow-chamber one. Idempotent:
///   • deactivates the old FumeHoodModel (kept in the scene for hand-deletion
///     rather than deleted by the tool),
///   • mounts Art/Generated/Refs/FumeHoodOpen.prefab under FumeHood_StandIn,
///     height-normalised to the house 2.35 m,
///   • re-fits the WorkVolume trigger into the new chamber (upper-front region
///     — hand-tune afterwards, the wire box is visible when selected),
///   • rebuilds the HoodShell walls from the refit volume (front stays open).
public static class FumeHoodSwap
{
    const float HoodHeight = 2.35f;

    [MenuItem("Tools/PharmaSynth/Swap In Open Fume Hood")]
    public static void Swap()
    {
        if (Application.isPlaying) { Debug.LogWarning("[HoodSwap] exit Play mode first."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/PharmaSynth/Art/Generated/Refs/FumeHoodOpen.prefab");
        if (prefab == null) { Debug.LogWarning("[HoodSwap] FumeHoodOpen.prefab not found."); return; }

        GameObject standIn = null, oldModel = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name == "FumeHood_StandIn") standIn = t.gameObject;
            if (t.name == "FumeHoodModel") oldModel = t.gameObject;
        }
        if (standIn == null) { Debug.LogWarning("[HoodSwap] FumeHood_StandIn not in scene."); return; }

        if (oldModel != null && oldModel.activeSelf)
        {
            oldModel.SetActive(false);   // kept for hand-deletion
            Debug.Log("[HoodSwap] old FumeHoodModel deactivated (delete by hand when happy).");
        }

        // Mount (idempotent — reuse if already swapped in).
        var existing = standIn.transform.Find("FumeHoodOpenModel");
        GameObject inst = existing != null ? existing.gameObject : null;
        if (inst == null)
        {
            inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, standIn.transform);
            inst.name = "FumeHoodOpenModel";
        }

        // Height-normalise to 2.35 m and stand the base on the old model's floor
        // (fall back to the stand-in's position when no old model exists).
        var rends = inst.GetComponentsInChildren<Renderer>(true);
        if (rends.Length == 0) { Debug.LogWarning("[HoodSwap] generated model has no renderers?"); return; }
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        float scale = HoodHeight / Mathf.Max(0.01f, b.size.y);
        inst.transform.localScale = inst.transform.localScale * scale;

        // Recompute bounds at the new scale, then place: keep the old model's
        // footprint centre, base at the old base height.
        rends = inst.GetComponentsInChildren<Renderer>(true);
        b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        Vector3 anchor = standIn.transform.position;
        float baseY = anchor.y;
        if (oldModel != null)
        {
            var or2 = oldModel.GetComponentsInChildren<Renderer>(true);
            if (or2.Length > 0)
            {
                Bounds ob = or2[0].bounds;
                for (int i = 1; i < or2.Length; i++) ob.Encapsulate(or2[i].bounds);
                anchor = new Vector3(ob.center.x, 0f, ob.center.z);
                baseY = ob.min.y;
            }
        }
        Vector3 delta = new Vector3(anchor.x - b.center.x, baseY - b.min.y, anchor.z - b.center.z);
        inst.transform.position += delta;

        // Face the bench: yaw the model so its local +Z looks toward the Raw_
        // bottles (same heuristic the HoodShell used). Tripo fronts are +Z by
        // convention; if the opening ends up backwards, rotate this child 180°.
        Vector3 benchCenter = Vector3.zero; int nb = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.StartsWith("Raw_") && t.name != "Raw_IceBucket") { benchCenter += t.position; nb++; }
        if (nb > 0)
        {
            benchCenter /= nb;
            Vector3 toBench = benchCenter - inst.transform.position; toBench.y = 0f;
            if (toBench.sqrMagnitude > 0.01f)
                inst.transform.rotation = Quaternion.LookRotation(toBench.normalized, Vector3.up);
            // Snap to the nearest 90°: the hood stands square against its wall
            // (aiming exactly at the bench centre leaves it ~30° diagonal).
            float snappedYaw = Mathf.Round(inst.transform.rotation.eulerAngles.y / 90f) * 90f;
            inst.transform.rotation = Quaternion.Euler(0f, snappedYaw, 0f);
        }

        // Re-fit the WorkVolume into the chamber: upper-front region of the new
        // bounds — a starting fit, hand-tune to the visible cavity.
        var hood = Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
        var wv = hood != null ? hood.GetComponent<BoxCollider>() : null;
        if (wv != null)
        {
            rends = inst.GetComponentsInChildren<Renderer>(true);
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            // Chamber ≈ middle band: 45–75% of height, 80% of width, 60% of depth.
            Vector3 c = new Vector3(b.center.x, b.min.y + b.size.y * 0.60f, b.center.z);
            Vector3 s = new Vector3(b.size.x * 0.8f, b.size.y * 0.30f, b.size.z * 0.6f);
            var ht = hood.transform;
            ht.position = c;
            ht.rotation = inst.transform.rotation;
            wv.center = Vector3.zero;
            // Collider size is LOCAL — compensate the zone object's lossy scale.
            var ls = ht.lossyScale;
            wv.size = new Vector3(s.x / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
                                  s.y / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
                                  s.z / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
            // Rebuild the shell from the refit volume.
            var oldShell = ht.Find("HoodShell");
            if (oldShell != null) Object.DestroyImmediate(oldShell.gameObject);
            Debug.Log("[HoodSwap] WorkVolume refit into the chamber; HoodShell cleared — run Apply Verb Data to rebuild it.");
        }
        else Debug.LogWarning("[HoodSwap] no FumeHoodZone/BoxCollider found to refit.");

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[HoodSwap] done — open hood mounted at " + HoodHeight + " m.");
    }
}
#endif
