#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Experiments spawn their stations/vessels/labels/
/// waypoints as children of DynamicStage (and Methane uses MethaneStage), both
/// authored at the center table's ORIGINAL spot. This shifts both stages by the
/// table's horizontal offset from that spot so all spawns land on the table
/// wherever it now stands.
/// Idempotent (absolute set from the current table position). Re-run after any
/// further table move.
public static class AlignExperimentStages
{
    // The center table's ORIGINAL authored centre (0, 0.91, -3.3), where both stages were authored.
    static readonly Vector3 OldTableCentre = new Vector3(0f, 0.91f, -3.3f);

    [MenuItem("Tools/PharmaSynth/Align Experiment Stages to Table")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[AlignStages] exit Play mode first."); return; }

        var table = GameObject.Find("Table_1") ?? GameObject.Find("Environment/Table_1");
        if (table == null) { Debug.LogError("[AlignStages] Table_1 not found — can't derive the move delta."); return; }

        // Horizontal delta only (keep spawns at their authored bench height).
        Vector3 p = table.transform.position;
        Vector3 delta = new Vector3(p.x - OldTableCentre.x, 0f, p.z - OldTableCentre.z);

        int moved = 0;
        foreach (var name in new[] { "DynamicStage", "MethaneStage" })
        {
            var stage = Find(name);
            if (stage == null) continue;
            stage.transform.position = delta;   // absolute → idempotent
            moved++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[AlignStages] table at {p} → shifted {moved} stage(s) by ({delta.x:F2}, 0, {delta.z:F2}). "
                  + "Experiment apparatus/labels/waypoints now spawn on the moved table.</color>\n"
                  + "If it's off, tell me the table's target spot and I'll adjust the reference.");
    }

    static GameObject Find(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
