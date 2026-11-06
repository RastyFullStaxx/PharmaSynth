#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Seals the skybox seams at BOTH entrances (front corridor
/// door + interior lab door). Each doorway's frame doesn't quite
/// meet the surrounding wall, so the skybox shows through thin cracks at the
/// jambs/lintel. This wraps each doorway with opaque trim strips (top lintel +
/// two jambs) centred on the wall plane and standing slightly proud on both
/// faces, so the seam is covered whether viewed from the corridor or the lab.
/// Reuses GapSealWall's dark material for a consistent look; re-runnable.
///
/// Tools ▸ PharmaSynth ▸ Seal Entrance Gaps (SampleScene, edit mode, idempotent).
public static class EntranceSealBuilder
{
    private const string GroupName = "EntranceSeals";

    [MenuItem("Tools/PharmaSynth/Seal Entrance Gaps")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[EntranceSeal] exit Play mode first."); return; }

        var old = GameObject.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Seal Entrance Gaps");

        // Match the existing seal look.
        Material mat = null;
        var gsw = GameObject.Find("GapSealWall");
        if (gsw != null) { var r = gsw.GetComponentInChildren<Renderer>(); if (r != null) mat = r.sharedMaterial; }
        if (mat == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            mat = new Material(sh); mat.color = new Color(0.16f, 0.17f, 0.2f);
        }

        int count = 0;
        count += SealDoor("Door", group.transform, mat);       // front corridor door
        count += SealDoor("Door (1)", group.transform, mat);   // interior lab door

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[EntranceSeal] wrapped " + count + " doorway edge-set(s)</color>");
    }

    /// Wrap one doorway (identified by its Door object) with lintel + jamb strips.
    static int SealDoor(string doorName, Transform parent, Material mat)
    {
        var door = GameObject.Find(doorName);
        if (door == null) { Debug.LogWarning("[EntranceSeal] " + doorName + " not found"); return 0; }
        var dr = door.GetComponentInChildren<Renderer>();
        if (dr == null) { Debug.LogWarning("[EntranceSeal] " + doorName + " has no renderer"); return 0; }

        // The door leaf swings, so derive the OPENING from a sibling frame if we
        // can find one near the door hinge; otherwise fall back to the leaf's
        // resting bounds. Frames are the reliable opening footprint.
        Bounds b = dr.bounds;
        var frame = FindFrameNear(door.transform.position);
        if (frame != null) b = frame.bounds;

        // Wall plane: these doorways are thin along Z (face ±Z). Strips live in
        // the X–Y plane at the frame's Z, standing proud on both faces.
        float z = b.center.z;
        float halfW = b.size.x * 0.5f;
        float halfH = b.size.y * 0.5f;
        float cx = b.center.x;
        float cy = b.center.y;
        const float strip = 0.14f;   // trim width
        const float ov = 0.09f;      // overlap onto frame + wall
        const float depth = 0.34f;   // > wall thickness → covers seam both sides

        // Top lintel
        Strip(parent, mat, doorName + "_Lintel",
            new Vector3(cx, cy + halfH + strip * 0.5f - ov, z),
            new Vector3(b.size.x + strip * 2f + ov, strip + ov, depth));
        // Left jamb
        Strip(parent, mat, doorName + "_JambL",
            new Vector3(cx - halfW - strip * 0.5f + ov, cy, z),
            new Vector3(strip + ov, b.size.y + ov, depth));
        // Right jamb
        Strip(parent, mat, doorName + "_JambR",
            new Vector3(cx + halfW + strip * 0.5f - ov, cy, z),
            new Vector3(strip + ov, b.size.y + ov, depth));
        return 1;
    }

    /// The nearest DoorFrame within ~1.2 m (in X/Y, same wall) of the door.
    static Renderer FindFrameNear(Vector3 doorPos)
    {
        Renderer best = null; float bestD = 1.4f;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (!t.name.ToLower().Contains("doorframe")) continue;
            var r = t.GetComponentInChildren<Renderer>();
            if (r == null) continue;
            float d = Vector2.Distance(new Vector2(doorPos.x, doorPos.z),
                                       new Vector2(r.bounds.center.x, r.bounds.center.z));
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }

    static void Strip(Transform parent, Material mat, string name, Vector3 center, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
        // Static décor — keep its box collider so it also blocks head-poke phasing.
        go.isStatic = true;
    }
}
#endif
