#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Fits lab items to reality: stands tipped glassware up, puts an item's PIVOT and grab
/// collider back on its own mesh, seats it on the surface below, and re-bakes its respawn
/// home (upright · seat · pivot).
///
/// ⛔ A vessel whose RESTING pose reads as tipped pours itself forever: `LiquidPourer.Update`
/// fires on `Vector3.Angle(Vector3.up, transform.up) > pourThreshold`, so it empties every
/// drop put into it and runs its looping pour audio under everything else. A
/// `Re-Home Scene Items (Adopt Current)` pass run while a flask lies down brings this back,
/// because adoption bakes whatever pose it finds, tipped or not.
///
/// ⛔ An item whose visible mesh is not on its own PIVOT cannot be placed by anyone and is
/// held nowhere near the player's hand. Tripo/USD imports can carry their geometry
/// 0.7–1.9 m from their origin (both distilling flasks, the ice bucket and the matchstick).
/// Dragging the object in the Scene view moves the pivot; the mesh keeps its offset and hangs
/// in the air wherever it is put. The runtime also reads `transform.position`
/// (`Matchstick` measures its strike distance from it, `VaporCollectController` its receiver
/// radius, `DropRespawn` its home), so a grabbed match would be "held" 1.9 m from the hand
/// and light no burner. The simulators cannot see this: they drive the APIs directly.
///
/// Straightening or re-pivoting the transform alone never sticks: `DropRespawn.ResetAllHome`
/// puts the baked pose back at the start of every run, so the HOME is rewritten too. Pinned
/// by the `pour:` and `grab:` suite lines. Idempotent: a bench that is already right is left
/// completely alone.
public static class UprightPourables
{
    /// Ignore this much daylight when seating. The 14 cabinet bottles all sit a uniform 2 cm
    /// proud of their shelf collider, which is how they are authored: not damage, and not
    /// something to "fix" by dragging every bottle down 2 cm.
    public const float Tolerance = 0.03f;

    /// A pivot further than this OUTSIDE its own mesh is not imprecise, it is unusable.
    /// Containment with slack, not distance-to-centre: a 20 cm flask whose pivot sits at its
    /// base is 10 cm from its centre and perfectly healthy; a metre is not.
    public const float MeshOffsetLimit = 0.05f;
    const float PivotSlack = 0.10f;

    /// The highest collider under a footprint, probed at the centre AND four corners. One
    /// ray is not enough: a single ray can slip through a gap in a table's mesh collider and
    /// report the floor 30 cm lower, which would seat the vessel inside the furniture.
    static bool SurfaceUnder(Bounds b, out float y)
    {
        y = 0f;
        bool found = false;
        float dx = b.extents.x * 0.4f, dz = b.extents.z * 0.4f;
        var offsets = new[]
        {
            Vector2.zero, new Vector2(dx, 0f), new Vector2(-dx, 0f),
            new Vector2(0f, dz), new Vector2(0f, -dz)
        };
        foreach (var o in offsets)
        {
            var from = new Vector3(b.center.x + o.x, b.min.y + 0.02f, b.center.z + o.y);
            if (!Physics.Raycast(from, Vector3.down, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
            if (!found || hit.point.y > y) { y = hit.point.y; found = true; }
        }
        return found;
    }

    static void BakeHome(GameObject go, string undo)
    {
        var dr = go.GetComponent<DropRespawn>();
        if (dr == null) return;
        Undo.RecordObject(dr, undo);
        dr.SetHome(go.transform.position, go.transform.rotation);
        EditorUtility.SetDirty(dr);
    }

    /// ⛔ RE-PIVOT: the mesh must NOT move. Dragging the visual assembly onto the pivot
    /// "aligns" it but teleports the object (both flasks: half a metre) off the surface it
    /// was placed over. Instead the pivot moves
    /// TO the mesh and every direct child is shifted by the same amount the other way, so the
    /// mesh, liquid, spout, flame anchor, chill zone and label all stay exactly where they are
    /// in world space. The grab collider is then wrapped around the mesh and the home re-baked.
    ///
    /// A Ctrl+Z in the Scene view undoes it like any edit; the `grab:` pins are what make it
    /// stick, because drift shows up as a red suite line and one menu run restores it.
    static bool RePivot(GameObject go, out string note)
    {
        note = "";
        Bounds mesh = ExperimentSceneBuilder.SolidWorldBounds(go);
        if (mesh.size == Vector3.zero) return false;
        var slack = mesh; slack.Expand(PivotSlack * 2f);
        if (slack.Contains(go.transform.position)) return false;

        Vector3 delta = mesh.center - go.transform.position;   // pivot -> mesh
        Undo.RecordObject(go.transform, "Re-pivot item");
        go.transform.position += delta;
        foreach (Transform child in go.transform)
        {
            Undo.RecordObject(child, "Re-pivot item");
            child.position -= delta;
            EditorUtility.SetDirty(child);
        }
        EditorUtility.SetDirty(go.transform);

        // The grab box follows the mesh. A MeshCollider already does; a BoxCollider is
        // authored at the original pivot (a fixed-size fallback in the Tripo loader gives the
        // matchstick a 1 mm one) and must be re-fitted. Only when the object is
        // upright: an AABB divided by scale is meaningless for a rotated body.
        var box = go.GetComponent<BoxCollider>();
        if (box != null && Vector3.Angle(Vector3.up, go.transform.up) < 1f)
        {
            Undo.RecordObject(box, "Re-pivot item");
            var ls = go.transform.lossyScale;
            box.center = go.transform.InverseTransformPoint(mesh.center);
            box.size = new Vector3(mesh.size.x / Mathf.Max(Mathf.Abs(ls.x), 1e-4f),
                                   mesh.size.y / Mathf.Max(Mathf.Abs(ls.y), 1e-4f),
                                   mesh.size.z / Mathf.Max(Mathf.Abs(ls.z), 1e-4f));
            EditorUtility.SetDirty(box);
        }

        BakeHome(go, "Re-pivot item");
        note = go.name + " pivot moved " + delta.magnitude.ToString("0.00") + " m onto its mesh";
        return true;
    }

    /// Drop an item until its BASE rests on the surface below, keeping the hand-placed x and z
    /// and re-bake its respawn home so a reset keeps it there.
    static bool SeatOnSurface(GameObject go, out string note)
    {
        note = "";
        Bounds b = ExperimentSceneBuilder.SolidWorldBounds(go);
        if (b.size == Vector3.zero) return false;
        if (!SurfaceUnder(b, out float surfaceY))
        { note = go.name + ": nothing underneath — left where it is"; return false; }

        float gap = b.min.y - surfaceY;
        if (gap <= Tolerance && gap >= -Tolerance) return false;

        Undo.RecordObject(go.transform, "Seat item");
        // Bottom-align by BOUNDS, never the pivot: these flasks pivot at their centre.
        go.transform.position += Vector3.up * (surfaceY + 0.002f - b.min.y);
        EditorUtility.SetDirty(go.transform);
        note = go.name + " " + (gap * 100f).ToString("0") + " cm → seated at y " + surfaceY.ToString("0.000");
        BakeHome(go, "Seat item");
        return true;
    }

    /// Legacy menu path, kept because the docs still use it.
    [MenuItem("Tools/PharmaSynth/Stand Tipped Glassware Up")]
    public static void RunLegacy() => Run();

    [MenuItem("Tools/PharmaSynth/Fit Glassware (upright · pivot · seat)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Fit] exit Play mode first."); return; }

        // ---- 1. upright: pourables only (the self-pouring bug) ---------------------------
        int straightened = 0, rehomed = 0;
        string names = "";
        foreach (var pr in Object.FindObjectsByType<LiquidPourer>(
                     FindObjectsInactive.Include))
        {
            if (pr == null) continue;

            // Keep the yaw it was placed at; only the tip is wrong.
            if (Vector3.Angle(Vector3.up, pr.transform.up) > pr.pourThreshold)
            {
                Undo.RecordObject(pr.transform, "Stand glassware up");
                Vector3 fwd = pr.transform.forward;
                fwd.y = 0f;
                pr.transform.rotation = Quaternion.LookRotation(
                    fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward, Vector3.up);
                EditorUtility.SetDirty(pr.transform);
                straightened++;
                if (names.Length < 120) names += (names.Length > 0 ? ", " : "") + pr.name;
            }

            // The baked home matters even when the live transform is fine: the next run
            // restores it.
            var dr = pr.GetComponent<DropRespawn>();
            if (dr == null) continue;
            var so = new SerializedObject(dr);
            var rot = so.FindProperty("_homeRot");
            if (rot == null) continue;
            if (Vector3.Angle(Vector3.up, rot.quaternionValue * Vector3.up) <= pr.pourThreshold) continue;
            Vector3 hf = rot.quaternionValue * Vector3.forward;
            hf.y = 0f;
            rot.quaternionValue = Quaternion.LookRotation(
                hf.sqrMagnitude > 1e-4f ? hf.normalized : Vector3.forward, Vector3.up);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(dr);
            rehomed++;
        }

        // ---- 2. pivot: every equipment item, before anything is measured from it ----------
        int pivoted = 0; string pivotNotes = "";
        var toSeat = new HashSet<GameObject>();
        foreach (var li in Object.FindObjectsByType<LabItem>(
                     FindObjectsInactive.Include))
        {
            if (li == null || !li.gameObject.activeInHierarchy) continue;
            if (!RePivot(li.gameObject, out string note)) continue;
            pivoted++; toSeat.Add(li.gameObject);
            if (pivotNotes.Length < 200) pivotNotes += (pivotNotes.Length > 0 ? "; " : "") + note;
        }

        // ---- 3. seat: pourables, plus whatever was just re-pivoted ----------------------
        // Seating measures the MESH, so it is only meaningful once the pivot is on it — a mesh
        // half a metre from its pivot cannot be seated, because seating moves the pivot and the
        // mesh keeps the offset.
        foreach (var pr in Object.FindObjectsByType<LiquidPourer>(
                     FindObjectsInactive.Include))
            if (pr != null && pr.gameObject.activeInHierarchy) toSeat.Add(pr.gameObject);
        int seated = 0; string seatNotes = "";
        foreach (var go in toSeat)
        {
            if (!SeatOnSurface(go, out string note)) { if (note.Length > 0) Debug.LogWarning("[Fit] " + note); continue; }
            seated++;
            if (seatNotes.Length < 160) seatNotes += (seatNotes.Length > 0 ? "; " : "") + note;
        }

        if (straightened + rehomed + pivoted + seated > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("<color=#4CD07D>[Fit] " + straightened + " vessel(s) stood up"
                  + (names.Length > 0 ? " (" + names + ")" : "")
                  + ", " + rehomed + " baked home(s) straightened, " + pivoted + " pivot(s) put on their mesh"
                  + (pivotNotes.Length > 0 ? " (" + pivotNotes + ")" : "")
                  + ", " + seated + " seated"
                  + (seatNotes.Length > 0 ? ": " + seatNotes : "") + ".</color>");
    }
}
#endif
