#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Re-seats every experiment layout onto the LayoutTidyMath zoning grid (a
/// clean center table — stations across the back, vessels center-front,
/// reagents right, tools left; the front strip stays free for the rack and
/// spares). Deterministic + idempotent; also structurally removes the two
/// clamped overlaps at (1.38, −3.88) in Acetone and Benzamide.
/// Run AFTER `Apply W5.8 Verb Data` so new stations/props get slots too.
public static class LayoutTidy
{
    [MenuItem("Tools/PharmaSynth/Tidy Experiment Layouts")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LayoutTidy] exit Play mode first."); return; }
        int layoutsDone = 0, moved = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
            if (layout == null) continue;
            moved += Tidy(layout);
            EditorUtility.SetDirty(layout);
            layoutsDone++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[LayoutTidy] {layoutsDone} layouts re-zoned, {moved} positions moved. " +
                  "Suite's LayoutSpacingSuite pins the spacing invariants.");
    }

    /// Returns how many positions changed.
    public static int Tidy(ExperimentLayout layout)
    {
        int moved = 0;
        for (int i = 0; i < layout.stations.Count; i++)
            moved += Move(ref layout.stations[i].pos, LayoutTidyMath.StationPos(i));
        for (int i = 0; i < layout.vessels.Count; i++)
            moved += Move(ref layout.vessels[i].pos, LayoutTidyMath.VesselPos(i));

        int reagentIdx = 0, toolIdx = 0;
        foreach (var p in layout.props)
        {
            if (p.pourable) moved += Move(ref p.pos, LayoutTidyMath.ReagentPos(reagentIdx++));
            else moved += Move(ref p.pos, LayoutTidyMath.ToolPos(toolIdx++));
        }
        return moved;
    }

    private static int Move(ref Vector3 pos, Vector3 target)
    {
        if ((pos - target).sqrMagnitude < 1e-6f) return 0;
        pos = target;
        return 1;
    }
}
#endif
