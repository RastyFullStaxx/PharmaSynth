#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Makes the lab door actually open, and actually stop you before it does.
///
/// ⛔ THE FAULTS ARE IN THE SCENE, NOT THE CODE. `DoorOpener` is wired correctly: its `door`
/// field resolves to the leaf, `PharmeeGatekeeper.doorOpener` is assigned, and `SetOpen(true)`
/// fires on DoorArmed. Two SCENE faults break it:
///
///   1. The leaf inherits the Environment prefab's `m_StaticEditorFlags = 87`, which
///      includes **Batching Static**. A static-batched renderer is merged into a combined
///      mesh at load and then IGNORES its transform, so the leaf's collider swings open
///      while the drawn door stays shut, and the player walks through what looks like a
///      closed door. Contribute GI goes with it:
///      a door that moves must not carry a lightmap baked in the closed position.
///   2. A null `doorBlocker` leaves nothing to hold the player back while the gate is still
///      talking. The blocker is a plain trigger-free box across the doorway that the
///      gatekeeper enables and disables; it is created here so the scene does not depend on
///      a hand-placed one.
///
/// Idempotent: re-running finds the same objects and rewrites the same values.
/// ⚠ The leaf's lightmap- and navmesh-static flags change, so after running this go through
/// `Build Lab Probes` → `Run Lab Lighting Bake` → `Build Lab NavMesh`.
public static class LabDoorFixBuilder
{
    const string BlockerName = "LabDoorBlocker";

    [MenuItem("Tools/PharmaSynth/Fix Lab Door (swing + blocker)")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabDoor] exit Play mode first."); return; }

        var controller = Object.FindAnyObjectByType<DoorOpener>(FindObjectsInactive.Include);
        if (controller == null) { Debug.LogError("[LabDoor] no DoorOpener in the scene."); return; }
        var leaf = controller.Door;
        if (leaf == null) { Debug.LogError("[LabDoor] DoorOpener has no door leaf assigned."); return; }

        // ---- 1. the leaf must be allowed to move -----------------------------------
        var flagsBefore = GameObjectUtility.GetStaticEditorFlags(leaf.gameObject);
        GameObjectUtility.SetStaticEditorFlags(leaf.gameObject, 0);
        leaf.gameObject.isStatic = false;
        EditorUtility.SetDirty(leaf.gameObject);

        // ---- 2. something to stand behind while the gate is shut -------------------
        var blocker = GameObject.Find(BlockerName);
        if (blocker == null)
        {
            blocker = new GameObject(BlockerName);
            Undo.RegisterCreatedObjectUndo(blocker, "Lab door blocker");
        }
        // Sit it in the doorway, sized off the leaf so it covers exactly the opening.
        var leafRenderer = leaf.GetComponentInChildren<Renderer>();
        Bounds b = leafRenderer != null ? leafRenderer.bounds
                                        : new Bounds(leaf.position, new Vector3(1f, 2.1f, 0.15f));
        blocker.transform.SetPositionAndRotation(
            new Vector3(b.center.x, b.center.y, b.center.z), leaf.rotation);
        // ⛔ Explicit null check, never `??`: GetComponent hands back Unity's fake-null for a
        // freshly created object in some editor paths, and `??` accepts it, so the very next
        // line throws MissingComponentException.
        var box = blocker.GetComponent<BoxCollider>();
        if (box == null) box = blocker.AddComponent<BoxCollider>();
        if (box == null) { Debug.LogError("[LabDoor] could not add a collider to the blocker."); return; }
        box.isTrigger = false;
        box.size = new Vector3(Mathf.Max(b.size.x, 0.9f), Mathf.Max(b.size.y, 2.0f), 0.12f);
        box.center = Vector3.zero;
        blocker.transform.localScale = Vector3.one;
        EditorUtility.SetDirty(blocker);

        // ---- 3. hand it to the gatekeeper ------------------------------------------
        var gate = Object.FindAnyObjectByType<PharmeeGatekeeper>(FindObjectsInactive.Include);
        if (gate != null)
        {
            var so = new SerializedObject(gate);
            var prop = so.FindProperty("doorBlocker");
            if (prop != null) { prop.objectReferenceValue = blocker; so.ApplyModifiedProperties(); }
            EditorUtility.SetDirty(gate);
        }
        // The gate only re-enables it on a state change, so start it solid: the player
        // spawns at the front door and the very first thing they can do is walk forward.
        blocker.SetActive(true);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[LabDoor] leaf '" + leaf.name + "' static flags " + flagsBefore
                  + " → 0 (it can move now); blocker " + box.size.ToString("0.00")
                  + " at " + blocker.transform.position.ToString("0.00")
                  + (gate != null ? "; handed to the gatekeeper" : "; NO gatekeeper found")
                  + "</color>. Re-bake probes → lighting → navmesh.");
    }
}
#endif
