#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Merge Center Tables: removes the second table and makes the remaining one a
/// single wide table at the centre of the room, in landscape.
/// The experiment layouts bake WORLD positions on the left island, so this is a
/// one-time atomic migration:
///   1. discover both islands geometrically (raycast under the baked positions
///      and at their x-mirror; climb to the Environment child),
///   2. deactivate the right island (+ its sink follows to the new short end),
///   3. rigid-remap the left island 90° to the lab centre (landscape),
///   4. apply the SAME remap to every layout-SO position and every in-footprint
///      scene prop (methane stage children, loose items, proctor points),
///   5. verify every remapped position still raycasts onto a deck.
/// Re-run safe: once the layouts no longer sit on the old left-island footprint,
/// the guard aborts before touching anything. Run Re-Home Scene Items after.
public static class CenterTableBuilder
{
    const string LayoutDir = "Assets/PharmaSynth/ScriptableObjects/Layouts";
    static readonly Vector3 NewCenterXZ = new Vector3(0f, 0f, -3.3f);   // lab midline, mid-island z

    [MenuItem("Tools/PharmaSynth/Merge Center Tables")]
    public static void Merge()
    {
        if (Application.isPlaying) { Debug.LogWarning("[CenterTable] exit Play mode first."); return; }

        // 1. Collect every baked layout position.
        var layouts = new List<ExperimentLayout>();
        var positions = new List<Vector3>();
        foreach (string guid in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LayoutDir }))
        {
            var lay = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
            if (lay == null) continue;
            layouts.Add(lay);
            CollectPositions(lay, positions);
        }
        if (positions.Count == 0) { Debug.LogError("[CenterTable] no layout positions found."); return; }

        Bounds footprint = CenterTableMath.FootprintOf(positions, 0.35f);
        Vector3 oldCenter = footprint.center;

        // Re-run guard: if the layouts already live at the lab midline, we're done.
        if (Mathf.Abs(oldCenter.x) < 0.6f)
        {
            Debug.Log("[CenterTable] layouts already centered (x=" + oldCenter.x.ToString("F2") + ") — nothing to merge.");
            return;
        }

        // 2. Discover the two islands.
        var leftIsland = IslandUnder(positions, oldCenter);
        var mirrored = new List<Vector3>();
        foreach (var p in positions) mirrored.Add(new Vector3(-p.x, p.y, p.z));
        var rightIsland = IslandUnder(mirrored, new Vector3(-oldCenter.x, oldCenter.y, oldCenter.z));
        if (leftIsland == null)
        {
            Debug.LogError("[CenterTable] could not find the island under the layout positions — aborted, nothing changed.");
            return;
        }
        if (rightIsland == leftIsland) rightIsland = null;   // asymmetric room — just move the one island
        string leftName = leftIsland.name, rightName = rightIsland != null ? rightIsland.name : "(none)";
        if (leftName.ToLowerInvariant().Contains("floor"))
        {
            Debug.LogError("[CenterTable] island discovery hit the floor ('" + leftName + "') — aborted, nothing changed.");
            return;
        }

        // The pivot is the island DECK's own centre (not the layout footprint's) —
        // pivoting on the footprint shears everything that sat near the deck ends
        // (wash-table-zone positions) off the rotated deck.
        Bounds leftDeck = BoundsUtil.OfRenderers(leftIsland.gameObject, 1f, true);
        Vector3 oldPivot = new Vector3(leftDeck.center.x, oldCenter.y, leftDeck.center.z);
        Vector3 newCenter = new Vector3(NewCenterXZ.x, oldCenter.y, NewCenterXZ.z);
        // Membership: the deck itself plus a skirt for the sinks at its ends.
        Bounds ride = leftDeck; ride.Expand(new Vector3(0.9f, 4f, 0.9f));

        // The hand-built MethaneStage lives on the RIGHT island — measure that
        // deck too, so the tutorial stage rides onto the merged table (the two
        // stages never show simultaneously, so they may share the deck space).
        Bounds rightDeck = rightIsland != null ? BoundsUtil.OfRenderers(rightIsland.gameObject, 1f, true) : new Bounds();
        Vector3 rightPivot = rightIsland != null
            ? new Vector3(rightDeck.center.x, oldCenter.y, rightDeck.center.z)
            : oldPivot;

        // 3. The left island becomes THE table: rotate 90° (landscape) onto the midline.
        leftIsland.position = CenterTableMath.Remap(leftIsland.position, oldPivot, newCenter, true);
        leftIsland.rotation = CenterTableMath.RemapRotation(leftIsland.rotation, true);

        // The left sink rides along (it is inside the skirt); the right island —
        // sink and all — is retired.
        if (rightIsland != null) rightIsland.gameObject.SetActive(false);

        // 4a. Remap every layout position (the atomic SO rewrite).
        int remapped = 0;
        foreach (var lay in layouts)
        {
            remapped += RemapLayout(lay, oldPivot, newCenter);
            EditorUtility.SetDirty(lay);
        }

        // 4b. Remap in-footprint scene objects: methane-stage children, loose
        // props, proctor points — anything STANDING ON the old island (deck-height
        // guard keeps floors/walls out; Canvas-bearing UI furniture like the
        // GradeScreen stays where the review corner needs it). Inside Environment
        // only the sink units ride along by name. MethaneStage children remap
        // around the RIGHT island's own pivot (bounds-matched — some pivots sit
        // away from their meshes; the rigid remap itself is pivot-agnostic).
        int sceneMoved = 0;
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.transform == leftIsland.root) continue;
            if (root.name == "Environment")
                sceneMoved += RemapNamedWithin(root.transform, "wash", ride, oldPivot, newCenter, leftIsland);
            else if (root.name == "MethaneStage" && rightIsland != null)
            {
                Bounds rideRight = rightDeck; rideRight.Expand(new Vector3(0.9f, 4f, 0.9f));
                sceneMoved += RemapBoundsWithin(root.transform, rideRight, rightPivot, newCenter);
            }
            else
                sceneMoved += RemapChildrenWithin(root.transform, ride, oldPivot, newCenter, leftIsland);
        }

        // 5. Verify: every remapped layout position must still sit over a deck.
        // (Edit-mode raycasts see STALE collider poses without an explicit sync.)
        // Any straggler (odd geometry at the old deck ends) is CLAMPED onto the
        // moved deck so no station/prop can ever spawn floating.
        Physics.SyncTransforms();
        Bounds newDeck = BoundsUtil.OfRenderers(leftIsland.gameObject, 1f, true);
        int ok = 0, clamped = 0;
        foreach (var lay in layouts)
        {
            bool dirty = false;
            if (lay.stations != null) foreach (var s in lay.stations)
                { if (VerifyOrClamp(ref s.pos, newDeck, lay.name)) ok++; else { clamped++; dirty = true; } }
            if (lay.props != null) foreach (var p in lay.props)
                { if (VerifyOrClamp(ref p.pos, newDeck, lay.name)) ok++; else { clamped++; dirty = true; } }
            if (lay.vessels != null) foreach (var v in lay.vessels)
                { if (VerifyOrClamp(ref v.pos, newDeck, lay.name)) ok++; else { clamped++; dirty = true; } }
            if (dirty) EditorUtility.SetDirty(lay);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[CenterTable] merged: island '{leftName}' → centered landscape at {newCenter.ToString("F2")}, " +
                  $"'{rightName}' retired, {remapped} layout positions + {sceneMoved} scene objects remapped, " +
                  $"deck check {ok} ok / {clamped} clamped onto the deck. " +
                  "NOW RUN: Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current).");
    }

    /// True when the position raycasts onto something below (the deck); otherwise
    /// pulls it inside the new deck bounds (small inset) and reports false.
    static bool VerifyOrClamp(ref Vector3 pos, Bounds newDeck, string layName)
    {
        if (Physics.Raycast(pos + Vector3.up * 0.5f, Vector3.down, out _, 0.9f, ~0, QueryTriggerInteraction.Ignore))
            return true;
        Vector3 before = pos;
        pos.x = Mathf.Clamp(pos.x, newDeck.min.x + 0.12f, newDeck.max.x - 0.12f);
        pos.z = Mathf.Clamp(pos.z, newDeck.min.z + 0.12f, newDeck.max.z - 0.12f);
        Debug.LogWarning("[CenterTable] clamped off-deck position " + before.ToString("F2")
            + " → " + pos.ToString("F2") + " (" + layName + ")");
        return false;
    }


    static void CollectPositions(ExperimentLayout lay, List<Vector3> into)
    {
        if (lay.stations != null) foreach (var s in lay.stations) into.Add(s.pos);
        if (lay.props != null) foreach (var p in lay.props) into.Add(p.pos);
        if (lay.vessels != null) foreach (var v in lay.vessels) into.Add(v.pos);
    }

    static int RemapLayout(ExperimentLayout lay, Vector3 oldCenter, Vector3 newCenter)
    {
        int n = 0;
        if (lay.stations != null) foreach (var s in lay.stations) { s.pos = CenterTableMath.Remap(s.pos, oldCenter, newCenter, true); n++; }
        if (lay.props != null) foreach (var p in lay.props) { p.pos = CenterTableMath.Remap(p.pos, oldCenter, newCenter, true); n++; }
        if (lay.vessels != null) foreach (var v in lay.vessels) { v.pos = CenterTableMath.Remap(v.pos, oldCenter, newCenter, true); n++; }
        return n;
    }

    /// Depth-first: remap any transform whose position stands on the old island
    /// footprint at deck height (floors/rigs sit near y=0 and are left alone).
    /// Recurses only into groups that are NOT themselves remapped (moving a
    /// parent moves its children).
    static int RemapChildrenWithin(Transform t, Bounds footprint, Vector3 oldCenter, Vector3 newCenter, Transform skip)
    {
        if (t == skip) return 0;
        // Named UI furniture (GradeScreen/PostLabTablet/holo boards/door panel) is
        // positioned for the review corner and the player — it never rides the
        // table. Anonymous world canvases (station label boards) DO ride.
        if (t.GetComponent<Canvas>() != null && IsKeptUi(t.name)) return 0;
        if (CenterTableMath.WithinXZ(t.position, footprint, 0.15f)
            && t.position.y > 0.2f && t.position.y < 1.8f)
        {
            MoveWithRemap(t, oldCenter, newCenter);
            return 1;
        }
        int n = 0;
        for (int i = 0; i < t.childCount; i++)
            n += RemapChildrenWithin(t.GetChild(i), footprint, oldCenter, newCenter, skip);
        return n;
    }

    /// Remap only subtree members whose NAME contains the key (the sink units
    /// inside the Environment prefab), whatever their height. Case-insensitive
    /// (nested-prefab child names vary); logs every match/miss for the record.
    static int RemapNamedWithin(Transform t, string nameKey, Bounds footprint, Vector3 oldCenter, Vector3 newCenter, Transform skip)
    {
        if (t == skip) return 0;
        if (t.name.ToLowerInvariant().Contains(nameKey.ToLowerInvariant()))
        {
            if (CenterTableMath.WithinXZ(t.position, footprint, 0.8f))
            {
                MoveWithRemap(t, oldCenter, newCenter);
                return 1;
            }
            Debug.Log("[CenterTable] named match outside ride zone, left in place: " + t.name + " @ " + t.position.ToString("F2"));
            return 0;
        }
        int n = 0;
        for (int i = 0; i < t.childCount; i++)
            n += RemapNamedWithin(t.GetChild(i), nameKey, footprint, oldCenter, newCenter, skip);
        return n;
    }

    /// MethaneStage children: match by combined renderer bounds (pivot-safe),
    /// remap the child transform with the same global isometry.
    static int RemapBoundsWithin(Transform group, Bounds footprint, Vector3 oldCenter, Vector3 newCenter)
    {
        int n = 0;
        for (int i = 0; i < group.childCount; i++)
        {
            var c = group.GetChild(i);
            Vector3 probe = c.position;
            var rends = c.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int r = 1; r < rends.Length; r++) b.Encapsulate(rends[r].bounds);
                probe = b.center;
            }
            if (CenterTableMath.WithinXZ(probe, footprint, 0.5f))
            {
                MoveWithRemap(c, oldCenter, newCenter);
                n++;
            }
            else Debug.Log("[CenterTable] methane child left in place: " + c.name + " @ " + probe.ToString("F2"));
        }
        return n;
    }

    static void MoveWithRemap(Transform t, Vector3 oldCenter, Vector3 newCenter)
    {
        t.position = CenterTableMath.Remap(t.position, oldCenter, newCenter, true);
        t.rotation = CenterTableMath.RemapRotation(t.rotation, true);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        Debug.Log("[CenterTable] remapped scene object: " + t.name + " → " + t.position.ToString("F2"));
    }

    /// The island under a point cloud: raycast down at three samples, climb each
    /// hit to the Environment child that contains it, take the majority.
    static Transform IslandUnder(List<Vector3> positions, Vector3 center)
    {
        var votes = new Dictionary<Transform, int>();
        Vector3[] samples = { positions[0], positions[positions.Count / 2], center };
        foreach (var s in samples)
        {
            if (!Physics.Raycast(s + Vector3.up * 0.5f, Vector3.down, out var hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                continue;
            var island = ClimbToEnvironmentChild(hit.collider.transform);
            if (island == null) continue;
            votes.TryGetValue(island, out int v);
            votes[island] = v + 1;
        }
        Transform best = null; int bestV = 0;
        foreach (var kv in votes)
            if (kv.Value > bestV) { best = kv.Key; bestV = kv.Value; }
        return best;
    }

    static bool IsKeptUi(string name)
    {
        return name.Contains("Screen") || name.Contains("Tablet") || name.Contains("Holo")
            || name.Contains("Panel") || name.Contains("Hud") || name.Contains("Choice");
    }

    static Transform ClimbToEnvironmentChild(Transform t)
    {
        while (t.parent != null && t.parent.name != "Environment")
        {
            if (t.parent.parent == null) break;   // reached a scene root group
            t = t.parent;
        }
        return t;
    }
}
#endif
