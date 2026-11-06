#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Names the bench's test tubes into ONE clean sequence, and gives the WORKSPACE
/// HOLDERS draggable SLOT anchors with a ghost-tube preview, so the rack
/// positions inside each kit can be fixed by hand.
///
///
/// ⛔ STORAGE RACKS GET NO SLOTS. The tubes were duplicated
/// TOGETHER WITH their kit holders, so every copy is already seated correctly
/// relative to its rack. Moving them onto bounds-GUESSED slots would destroy that
/// placement. Never re-derive rack positions the scene already has right.
///
/// Only the workspace holders need anchors, because they are the only racks that
/// start EMPTY (the player drags tubes in mid-experiment), so there is no seated
/// tube to copy a position from.
///
/// Workflow:
///   1. Tools ▸ PharmaSynth ▸ Name Tubes + Build Rack Slots   (this)
///   2. Drag each green Slot_* gizmo until its ghost tube sits in the holder's hole
///   3. Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current)  (bakes homes —
///      a broken tube respawns at its baked home via BreakableGlassware→DropRespawn)
public static class TubeRackSlotBuilder
{
    const string RegularId = "kit-testtube";
    const string RackId = "kit-testtuberack";

    /// Slots per rack/holder. Exp 2 is the worst case in the game at 19 regular tubes
    /// (5 enol + 4 alkaline + 4 acidic + 2 Tollen's + 2 esters + 2 hydrolysis);
    /// every other module needs <= 4.
    const int SlotsPerHolder = 6;

    /// TWO ROLES share the kit-testtuberack itemId, and they must not be confused:
    ///
    ///   STORAGE RACK  (Eq_TestTubeRack, TestTubeRack_*) — where tubes LIVE. Their
    ///     slots are the tubes' resting/home positions, so a broken tube respawns
    ///     there (BreakableGlassware -> DropRespawn). Tubes ARE seated into these.
    ///
    ///   WORKSPACE HOLDER (Experiment_Tube_Table_Kit_Holder_*) — the bench
    ///     holders "players can use to hold their test tubes so they won't just
    ///     carelessly lay it out to the table". These START EMPTY: the
    ///     player drags a tube in mid-experiment. Seating tubes here at startup
    ///     would defeat the entire point of them.
    ///
    /// Both get slots (both need positioning by hand); only storage gets tubes.
    static bool IsWorkspaceHolder(string name)
        => name.StartsWith("Experiment_Tube_Table_Kit_Holder");

    [MenuItem("Tools/PharmaSynth/Name Tubes + Build Rack Slots")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TubeSlots] exit Play mode first."); return; }

        // ---- 1. One clean name sequence for the regular tubes -----------------
        // Duplicating in the editor left NAME COLLISIONS (two Kit_TestTube_0, etc.)
        // and Unity's own TestTube_2..8. Ambiguous names break every by-name lookup.
        var regulars = AllWithItemId(RegularId)
            .Where(t => !t.name.ToLowerInvariant().Contains("hard-glass"))
            .OrderBy(t => t.transform.parent != null ? t.transform.parent.name : "")
            .ThenBy(t => t.transform.position.x)
            .ThenBy(t => t.transform.position.z)
            .ToList();

        int renamed = 0;
        for (int i = 0; i < regulars.Count; i++)
        {
            string want = "Kit_TestTube_" + i;
            if (regulars[i].name == want) continue;
            Undo.RecordObject(regulars[i].gameObject, "name tube");
            regulars[i].name = want;
            var li = regulars[i].GetComponent<LabItem>();
            if (li != null) li.displayName = "Test Tube";
            renamed++;
        }

        // Hard-glass keeps its own sequence — a DIFFERENT tube (naked flame only).
        var hard = AllWithItemId(RegularId)
            .Where(t => t.name.ToLowerInvariant().Contains("hard-glass"))
            .OrderBy(t => t.transform.position.x).ToList();
        for (int i = 0; i < hard.Count; i++)
        {
            string want = "Kit_Hard-GlassTestTube_" + i;
            if (hard[i].name == want) continue;
            Undo.RecordObject(hard[i].gameObject, "name tube");
            hard[i].name = want;
            renamed++;
        }

        // ---- 2. Slot anchors — WORKSPACE HOLDERS ONLY -------------------------
        // Storage racks already hold correctly-seated tubes (they were duplicated
        // with their holders), so they need no anchors and must not be touched.
        var holders = AllWithItemId(RackId).Where(t => IsWorkspaceHolder(t.name))
                                           .OrderBy(t => t.name).ToList();
        int slots = 0, purged = PurgeStorageSlots();
        foreach (var h in holders)
        {
            var b = WorldBounds(h.gameObject);
            for (int i = 0; i < SlotsPerHolder; i++)
            {
                var slot = h.transform.Find("Slot_" + i);
                if (slot == null)
                {
                    var go = new GameObject("Slot_" + i);
                    Undo.RegisterCreatedObjectUndo(go, "add slot");
                    go.transform.SetParent(h.transform, false);
                    slot = go.transform;
                    // First guess only — these get dragged by hand. Spread along the
                    // holder's LONGEST horizontal axis, sitting on its top face.
                    bool alongX = b.size.x >= b.size.z;
                    float t01 = SlotsPerHolder == 1 ? 0.5f : i / (float)(SlotsPerHolder - 1);
                    float lo = (alongX ? b.min.x : b.min.z) + (alongX ? b.size.x : b.size.z) * 0.12f;
                    float hi = (alongX ? b.max.x : b.max.z) - (alongX ? b.size.x : b.size.z) * 0.12f;
                    float along = Mathf.Lerp(lo, hi, t01);
                    var world = new Vector3(alongX ? along : b.center.x, b.max.y,
                                            alongX ? b.center.z : along);
                    slot.position = world;
                    slot.rotation = Quaternion.identity;   // tube stands +Y
                    slots++;
                }
                var pa = slot.GetComponent<PlacementAnchor>() ?? slot.gameObject.AddComponent<PlacementAnchor>();
                pa.previewsTube = true;      // ghost tube, so the fit is eyeballable
                pa.previewsScale = false;
                pa.gizmoColor = new Color(0.45f, 1f, 0.55f, 0.9f);   // green = drag me
                EditorUtility.SetDirty(slot.gameObject);
            }
            // Runtime half: without this the anchors are editor ghosts
            // only and nothing racks the tubes. Release any
            // tube near a free slot and TubeRackSlots seats it.
            var rackSlots = h.GetComponent<TubeRackSlots>();
            if (rackSlots == null) rackSlots = h.gameObject.AddComponent<TubeRackSlots>();
            rackSlots.Bind();
            EditorUtility.SetDirty(h.gameObject);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[TubeSlots] {regulars.Count} regular + {hard.Count} hard-glass tubes "
                  + $"({renamed} renamed). {holders.Count} WORKSPACE holders x {SlotsPerHolder} slots "
                  + $"({slots} new); {purged} stray slot(s) removed from storage racks — their tubes "
                  + "already sit correctly and must not be re-derived.\n"
                  + "NEXT: drag each green Slot_* until its ghost tube sits in the holder's hole, "
                  + "then Re-Home Scene Items (Adopt Current).</color>");
    }

    /// Remove slots from STORAGE racks. Those racks already hold correctly seated
    /// tubes, so slots there are noise, and seating tubes into them would move 19
    /// correctly-placed tubes onto bounds-guessed positions.
    static int PurgeStorageSlots()
    {
        int killed = 0;
        foreach (var rack in AllWithItemId(RackId).Where(t => !IsWorkspaceHolder(t.name)))
        {
            var doomed = new List<GameObject>();
            foreach (Transform c in rack.transform)
                if (c.name.StartsWith("Slot_") && c.GetComponent<PlacementAnchor>() != null)
                    doomed.Add(c.gameObject);
            foreach (var d in doomed) { Undo.DestroyObjectImmediate(d); killed++; }
        }
        return killed;
    }

    static List<LabItem> AllWithItemId(string id)
        => Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                 .Where(li => li != null && li.itemId == id).ToList();

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
#endif
