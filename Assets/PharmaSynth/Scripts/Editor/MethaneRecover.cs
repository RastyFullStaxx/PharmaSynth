#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Diagnose + recover the methane bench items. An item moved AFTER the last
/// Lock My Layout has a stale respawn home, so a Reset teleports it away (often
/// below the floor). This reports every methane item's
/// position + active state, reactivates + lifts anything that fell, and RE-HOMES
/// them all at their current spot so the next Reset keeps them put.
public static class MethaneRecover
{
    static readonly string[] Kinds = { "Motar", "Mortar", "Pestle", "Scoopula", "Spatula",
                                       "BunsenBurner", "AlcoholBurner" };

    [MenuItem("Tools/PharmaSynth/Methane: Find & Recover Bench Items")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[MethaneRecover] exit Play mode first."); return; }

        var found = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var go = t.gameObject;
            bool match = false;
            foreach (var k in Kinds) if (PhysicsAudit.PrefabNameFor(go) == k || go.name.Contains(k)) { match = true; break; }
            var item = go.GetComponent<LabItem>();
            if (item != null && item.itemId == "reagent-jar") match = true;
            if (match && go.GetComponentInChildren<Renderer>() != null) found.Add(go);
        }

        // Bench anchor = centroid of the items that are clearly still on a surface.
        Vector3 sum = Vector3.zero; int healthy = 0;
        foreach (var go in found) if (go.transform.position.y > 0.5f) { sum += go.transform.position; healthy++; }
        Vector3 bench = healthy > 0 ? sum / healthy : new Vector3(0f, 0.95f, -3.2f);

        var sb = new System.Text.StringBuilder("[MethaneRecover] bench items:\n");
        int recovered = 0, reactivated = 0, offset = 0;
        foreach (var go in found)
        {
            bool wasActive = go.activeInHierarchy;
            if (!go.activeSelf) { go.SetActive(true); reactivated++; }

            var p = go.transform.position;
            bool lost = p.y < 0.4f;   // fell through the bench / floor
            if (lost)
            {
                // Drop it back on the bench, fanned out beside the healthy items.
                go.transform.position = bench + new Vector3(0.12f * (offset % 4 - 1.5f), 0.03f, 0.12f * (offset / 4));
                offset++; recovered++;
            }

            // Re-home HERE so a future Reset never yanks it away again.
            var dr = go.GetComponent<DropRespawn>();
            if (dr != null) dr.SetHome(go.transform.position, go.transform.rotation);

            sb.AppendLine($"  • {go.name}  pos={go.transform.position}  active={wasActive}"
                          + (lost ? "  → RECOVERED onto bench" : ""));
            EditorUtility.SetDirty(go);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        sb.AppendLine($"Found {found.Count} item(s); {reactivated} reactivated, {recovered} lifted back onto the bench, all re-homed at current spot.");
        if (found.Count == 0)
            sb.AppendLine("⚠ NONE found — the mortar/tools aren't in the scene at all. Place them on the bench, then run this + Lock My Layout.");
        Debug.Log($"<color=#4CD07D>{sb}</color>");
    }
}
#endif
