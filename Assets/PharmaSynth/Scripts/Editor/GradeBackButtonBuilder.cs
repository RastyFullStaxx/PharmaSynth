#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Wires a "Complete Experiment" button onto the grade screen (shown only on FAIL,
/// where Continue is hidden; they share the slot) that ends the attempt and
/// returns the player to the entrance, where any unlocked experiment can be
/// picked, so a failed attempt is never a Retry-only loop. Idempotent.
public static class GradeBackButtonBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire Grade Back Button (W5.9)")]
    public static void Wire()
    {
        if (Application.isPlaying) { Debug.LogWarning("[GradeBack] exit Play mode first."); return; }
        var grade = Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        var gate = Object.FindFirstObjectByType<PharmeeGatekeeper>(FindObjectsInactive.Include);
        if (grade == null || gate == null) { Debug.LogError("[GradeBack] GradeScreenController / PharmeeGatekeeper not found."); return; }

        var so = new SerializedObject(grade);
        var contProp = so.FindProperty("continueButton");
        var backProp = so.FindProperty("backButton");
        var cont = contProp != null ? contProp.objectReferenceValue as GameObject : null;
        if (cont == null) { Debug.LogError("[GradeBack] continueButton not wired on the grade screen — nothing to clone."); return; }

        GameObject back = backProp != null ? backProp.objectReferenceValue as GameObject : null;
        if (back == null)
        {
            // Reuse an earlier clone if present, else clone Continue in place
            // (Continue is pass-only, Back is fail-only — they share the slot).
            var existing = cont.transform.parent != null ? cont.transform.parent.Find("BackButton") : null;
            back = existing != null ? existing.gameObject : Object.Instantiate(cont, cont.transform.parent);
            back.name = "BackButton";
            back.transform.localPosition = cont.transform.localPosition;
            back.transform.localRotation = cont.transform.localRotation;
            back.transform.localScale = cont.transform.localScale;
        }

        var label = back.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = "Complete Experiment";

        var btn = back.GetComponentInChildren<Button>(true);
        if (btn != null)
        {
            // Clear cloned persistent listeners, then wire → OnBackPressed.
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEventTools.AddVoidPersistentListener(btn.onClick, grade.OnBackPressed);
        }

        if (backProp != null) backProp.objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Route the event to the gate's fail-abandon flow (idempotent).
        bool wired = false;
        for (int i = 0; i < grade.onBackToEntrance.GetPersistentEventCount(); i++)
            if ((Object)grade.onBackToEntrance.GetPersistentTarget(i) == gate) wired = true;
        if (!wired)
            UnityEventTools.AddVoidPersistentListener(grade.onBackToEntrance, gate.OnAbandonAfterFail);

        back.SetActive(false);   // Show() toggles it per result
        EditorUtility.SetDirty(grade);
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[GradeBack] 'Complete Experiment' wired: fail-path back-to-entrance is live.");
    }
}
#endif
