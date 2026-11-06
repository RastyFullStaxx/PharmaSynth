#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Wires the MethaneStageVisibility controller (methane set
/// present only in Lab Tour + the Methane attempt). Puts the component on
/// ExperimentSystems (a manager that keeps running while the stage is hidden),
/// binds the MethaneStage + runner + LabTourGuide, and re-hides the stage in the
/// authored scene so it doesn't flash on lab entry. Idempotent.
public static class MethaneStageVisibilityBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire Methane Stage Visibility")]
    public static void Wire()
    {
        if (Application.isPlaying) { Debug.LogWarning("[MethaneVis] exit Play mode first."); return; }

        GameObject stage = FindInactive("MethaneStage");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        var tour = Object.FindAnyObjectByType<LabTourGuide>(FindObjectsInactive.Include);
        if (stage == null || runner == null)
        { Debug.LogError("[MethaneVis] MethaneStage or ExperimentRunner not found."); return; }

        // Host on ExperimentSystems (never on the stage — it must run while hidden).
        var host = runner.gameObject;
        var vis = host.GetComponent<MethaneStageVisibility>() ?? host.AddComponent<MethaneStageVisibility>();
        vis.Bind(stage, runner, tour);
        EditorUtility.SetDirty(vis);

        // Authored state hidden so it doesn't flash before Start() runs; you
        // can still select + relocate it from the hierarchy, or Reveal it again.
        stage.SetActive(false);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[MethaneVis] wired — Methane stage shows only during Lab Tour + the Methane attempt"
                  + (tour == null ? " (⚠ no LabTourGuide found — tour case inactive)" : "") + ".</color>");
    }

    static GameObject FindInactive(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
