#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// A ready-made GOAL destroys the experiment —
/// attaches EndProductVisibility to both storage roots and binds the runner, so
/// each product bottle SetActive(false)s WHILE ITS OWN MODULE RUNS.
///
/// The gate is per-EXPERIMENT, not per-chemical: Ethanol, Acetone and Benzoic Acid
/// are each some module's goal AND a manuscript-listed reagent for others, so a
/// global hide would strip Exp 2 (which runs before Exp 3/6) of reagents it needs.
/// The four PURE products are not on the shelf at all, so there is nothing to gate.
/// See EndProductVisibility.
///
/// Idempotent; safe to re-run after any storage rework.
public static class EndProductGateBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire End-Product Gate")]
    public static void Wire()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[EndProductGate] exit Play mode first.");
            return;
        }

        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        if (runner == null)
            Debug.LogWarning("[EndProductGate] no ExperimentRunner found — the gate cannot "
                             + "know which module is running and will hide nothing.");

        int wired = 0, scanned = 0;
        foreach (var rootName in new[] { "ReagentShelf", "ReagentCabinets" })
        {
            var root = GameObject.Find(rootName);
            if (root == null) { Debug.LogWarning("[EndProductGate] no " + rootName + " in the open scene."); continue; }
            var gate = root.GetComponent<EndProductVisibility>();
            if (gate == null) gate = root.AddComponent<EndProductVisibility>();
            gate.Bind(runner);            // AddComponent fires no Awake in edit mode
            scanned += gate.Rescan();     // edit-mode dry scan: count + validate wiring
            wired++;
            EditorUtility.SetDirty(gate);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[EndProductGate] " + wired + " roots wired, " + scanned
                  + " bottles scanned. Each module's OWN product hides while it runs; "
                  + "everything else stays on the bench.</color>");
    }
}
#endif
