#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// The Methane tutorial stage is authored inactive
/// (m_IsActive:0) so it stays hidden in the editor. To review /
/// delete it by hand, so this switches it (and any other hidden methane roots)
/// ON in edit mode and lists what became visible. Runtime is unaffected —
/// ExperimentSceneBuilder still SetActive(moduleId==methane) each build.
/// ⚠ Deleting these breaks Experiment 1 until Methane is rewired to build on the
/// workspace (the splint-pop rig especially is wired to the stage's tube).
public static class RevealMethaneStage
{
    [MenuItem("Tools/PharmaSynth/Reveal Methane Stage (for review)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RevealMethane] exit Play mode first."); return; }

        var sb = new StringBuilder();
        int revealed = 0;
        foreach (var name in new[] { "MethaneStage", "MethaneProps", "MethaneRig", "MethaneStations" })
        {
            var go = GameObject.Find(name);
            // GameObject.Find skips inactive roots — fall back to a full scan.
            if (go == null)
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t.name == name) { go = t.gameObject; break; }
            if (go == null) continue;
            if (!go.activeSelf) { go.SetActive(true); revealed++; }
            Selection.activeGameObject = go;   // leave it selected for inspection
            sb.Append("\n  • ").Append(name).Append("  (children: ");
            foreach (Transform c in go.transform) sb.Append(c.name).Append(", ");
            sb.Append(")");
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[RevealMethane] revealed {revealed} hidden methane root(s) — now visible + selectable for deletion:</color>"
                  + sb + "\n⚠ Deleting them disables Experiment 1's grind/heat/collect/splint until Methane is rewired to the workspace.");
    }
}
#endif
