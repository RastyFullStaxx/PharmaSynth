#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// Two VR-UI scene fixes.
///   • Quiz answers clickable: a world-space Canvas needs a
///     TrackedDeviceGraphicRaycaster for the XR ray to hit it; a plain
///     GraphicRaycaster is mouse-only, so a panel without one ignores the
///     controller ray.
///   • Dr. Jimenez's subtitles sit just above his head (at local y=2.15 they
///     float up by the ceiling, unreadable).
/// Both idempotent.
public static class VrUiFixes
{
    const float DialogueY = 1.8f;   // just above a standing NPC's head, at player reading height

    [MenuItem("Tools/PharmaSynth/Fix VR UI Raycasters (quiz clickable)")]
    public static void FixRaycasters()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VrUi] exit Play mode first."); return; }

        int added = 0;
        var names = new List<string>();
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (canvas == null) continue;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay) continue;      // not ray-driven
            if (canvas.GetComponent<GraphicRaycaster>() == null) continue;          // no UI raycasting at all
            if (canvas.GetComponent<TrackedDeviceGraphicRaycaster>() != null) continue;
            canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            names.Add(canvas.name);
            EditorUtility.SetDirty(canvas.gameObject);
            added++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[VrUi] Added TrackedDeviceGraphicRaycaster to {added} world-space canvas(es) — "
                  + "the controller ray can now click quiz answers and panel buttons.</color>"
                  + (names.Count > 0 ? "\nFixed: " + string.Join(", ", names) : "  (all canvases already tracked.)"));
    }

    [MenuItem("Tools/PharmaSynth/Fix NPC Dialogue Height")]
    public static void FixDialogueHeight()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VrUi] exit Play mode first."); return; }

        int moved = 0;
        foreach (var ex in Object.FindObjectsByType<ExaminerNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (ex == null) continue;
            var so = new SerializedObject(ex);
            var narr = so.FindProperty("narration")?.objectReferenceValue as NPCNarrationController;
            if (narr == null) continue;
            var t = narr.transform;
            var lp = t.localPosition;
            if (lp.y <= DialogueY + 0.01f) continue;   // already readable — leave it
            Debug.Log($"[VrUi] {t.name}: y {lp.y:0.00} → {DialogueY:0.00}");
            lp.y = DialogueY;
            t.localPosition = lp;
            EditorUtility.SetDirty(t);
            moved++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[VrUi] Lowered {moved} NPC subtitle anchor(s) to y={DialogueY} — Dr. Jimenez's lines "
                  + "now sit just above his head instead of by the ceiling.</color>");
    }
}
#endif
