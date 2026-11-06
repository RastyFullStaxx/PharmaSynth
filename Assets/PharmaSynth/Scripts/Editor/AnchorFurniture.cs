#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Furniture is scenery, not equipment: the stools must not be grabbable.
///
/// The six lab stools in the Environment prefab carry an `XRGrabInteractable` each, so a
/// player could pick a stool up and carry it around the lab, or fling it across the bench
/// mid-experiment. Nothing in the game asks them to move a stool, and every real
/// interaction the lab needs is with glassware and tools.
///
/// ⛔ The GRAB goes, the COLLIDER stays. The stool still stops the player walking through it
/// and still holds anything resting on it — the same rule `Anchor Tube Racks` follows. The
/// Rigidbody is left kinematic rather than destroyed so nothing doing `GetComponent&lt;Rigidbody&gt;()`
/// starts null-referencing.
///
/// Idempotent: a second run finds nothing to strip. Pinned by
/// `bench: lab furniture is not grabbable`.
public static class AnchorFurniture
{
    /// Name fragments that identify scenery a player must never pick up. Matched
    /// case-insensitively against the object name, so "Stool (4)" is covered.
    public static readonly string[] Fragments = { "stool", "printe" };   // "Printe" is the printer

    /// Pure: is this object name one of the furniture pieces that must not be grabbable?
    public static bool IsFurniture(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;
        string n = objectName.ToLowerInvariant();
        foreach (var f in Fragments)
            if (n.Contains(f)) return true;
        return false;
    }

    [MenuItem("Tools/PharmaSynth/Anchor Furniture (stools not grabbable)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Furniture] exit Play mode first."); return; }

        int stripped = 0, pinned = 0; string names = "";
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            if (t == null || !IsFurniture(t.name)) continue;
            var go = t.gameObject;
            bool touched = false;

            foreach (var grab in go.GetComponents<XRGrab>())
            { Undo.DestroyObjectImmediate(grab); stripped++; touched = true; }

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null && (!rb.isKinematic || rb.useGravity))
            {
                Undo.RecordObject(rb, "Anchor furniture");
                rb.isKinematic = true;
                rb.useGravity = false;
                EditorUtility.SetDirty(rb);
                pinned++; touched = true;
            }

            if (touched && names.Length < 140) names += (names.Length > 0 ? ", " : "") + go.name;
        }

        if (stripped + pinned > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#4CD07D>[Furniture] " + stripped + " grab(s) removed, " + pinned
                  + " body(ies) pinned" + (names.Length > 0 ? ": " + names : " — nothing to do")
                  + ". Colliders kept.</color>");
    }
}
#endif
