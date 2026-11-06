#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Make the methane bench TOOLS (mortar, pestle, scoopula, spatula) permanent
/// fixtures, usable in BOTH Lab Tour and Campaign at all times. Parented under
/// MethaneStage they vanish in Play, because MethaneStageVisibility hides the
/// stage at play-start. This lifts them OUT of the stage (world position preserved),
/// makes sure they're active + rest kinematic (won't fall through the bench), and
/// re-homes them so a Reset keeps them put. Idempotent.
public static class MethaneBenchPermanent
{
    static readonly string[] ToolNames = { "Motar", "Mortar", "Pestle", "Scoopula", "Spatula" };

    [MenuItem("Tools/PharmaSynth/Make Methane Bench Tools Permanent")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[BenchPermanent] exit Play mode first."); return; }

        var stage = GameObject.Find("MethaneStage");
        var stageT = stage != null ? stage.transform : null;
        // Reparent target: whatever the stage lives under (or scene root).
        Transform target = stageT != null ? stageT.parent : null;

        int moved = 0, kept = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = t.gameObject;
            bool isTool = false;
            foreach (var n in ToolNames) if (go.name.Contains(n)) { isTool = true; break; }
            if (!isTool) continue;
            // ROOT tool only (a mortar's mesh child also contains "Mortar").
            if (go.GetComponent<GrindController>() == null && go.GetComponent<ScoopController>() == null
                && go.GetComponent<LabItem>() == null) continue;

            // 1) Out of the stage hierarchy so the stage toggle can't hide it.
            if (stageT != null && go.transform.IsChildOf(stageT) && go.transform != stageT)
            {
                go.transform.SetParent(target, true);   // keep world position
                moved++;
            }
            else kept++;

            // 2) Always active.
            if (!go.activeSelf) go.SetActive(true);

            // 3) Rest kinematic so it stays on the bench (grab re-frees it via XRI).
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // 4) Re-home HERE so Reset keeps it in place.
            var dr = go.GetComponent<DropRespawn>();
            if (dr != null) dr.SetHome(go.transform.position, go.transform.rotation);

            EditorUtility.SetDirty(go);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[BenchPermanent] {moved} tool(s) detached from the stage (now always visible), "
                  + $"{kept} already loose. Mortar/pestle/scoop are permanent — usable in Lab Tour and Campaign. "
                  + "Run Lock My Layout to bake.</color>"
                  + (stage == null ? "\n(note: no 'MethaneStage' object found — tools were already loose.)" : ""));
    }
}
#endif
