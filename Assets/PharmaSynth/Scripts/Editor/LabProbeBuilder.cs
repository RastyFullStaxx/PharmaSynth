#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Builds the lab's REFLECTION and LIGHT probes.
///
/// The Mobile RP asset has reflection-probe blending AND box projection switched on, and
/// both need probes to feed them. Without probes:
///
///   * Every glass material in the ChemLab pack sets _EnvironmentReflections = 1, so it
///     samples the DEFAULT reflection - the built-in procedural outdoor sky - inside a
///     sealed windowless room, and beakers and flasks read as pale plastic.
///   * Every dynamic object (all grabbable glassware, held items, Pharmee, Dr. Jimenez) is
///     lit by flat ambient alone, so a beaker is lit identically under a closed cabinet and
///     directly beneath a lamp.
///
/// Probe placement is DERIVED from the room's own renderer bounds rather than hard-coded, so
/// it survives the furniture moves the user makes through Select Movable Furniture.
///
/// Tools > PharmaSynth > Build Lab Probes (edit mode, idempotent - deletes and rebuilds its
/// own root). Run the lighting bake afterwards to populate them.
public static class LabProbeBuilder
{
    const string RootName = "LabProbes";

    [MenuItem("Tools/PharmaSynth/Build Lab Probes")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabProbes] exit Play mode first."); return; }

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Lab Probes");

        Bounds room = RoomBounds();
        if (room.size == Vector3.zero) { Debug.LogError("[LabProbes] could not measure the room."); return; }

        int probes = BuildReflectionProbes(root, room);
        int points = BuildLightProbeGroup(root, room);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log(string.Format(
            "<color=#4CD07D>[LabProbes] {0} reflection probe(s) + {1} light-probe point(s) over " +
            "{2} x {3} x {4} m.</color> Now run Lighting > Generate Lighting (or Build Lab Lighting Bake) " +
            "to populate them - unbaked probes contribute nothing.",
            probes, points, room.size.x.ToString("0.0"), room.size.y.ToString("0.0"), room.size.z.ToString("0.0")));
    }

    /// The room's extent, measured from the STATIC shell only. Grabbables and effect children
    /// are excluded deliberately: LiquidPourer's world-space pour stream outlives a pour still
    /// pointing at the floor, and encapsulating it drags bounds a metre downwards.
    static Bounds RoomBounds()
    {
        var b = new Bounds();
        bool any = false;
        foreach (var name in new[] { "Environment", "Wall", "Floor (1)", "Floor" })
        {
            var go = GameObject.Find(name);
            if (go == null) continue;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                if (r.GetComponentInParent<Rigidbody>() != null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        return any ? b : new Bounds();
    }

    static int BuildReflectionProbes(GameObject root, Bounds room)
    {
        var go = new GameObject("LabReflectionProbe");
        go.transform.SetParent(root.transform, false);
        go.transform.position = new Vector3(room.center.x, room.min.y + 1.5f, room.center.z);

        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Baked;
        p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        p.resolution = 128;                 // Quest: a room probe does not need more
        p.hdr = true;
        p.shadowDistance = 20f;
        p.clearFlags = ReflectionProbeClearFlags.SolidColor;
        p.backgroundColor = new Color(0.05f, 0.05f, 0.06f);
        p.importance = 1;
        p.blendDistance = 1.5f;
        // Box projection (already enabled in the RP asset) needs a box that matches the ROOM,
        // otherwise reflections slide across surfaces as the player walks.
        p.boxProjection = true;
        p.size = room.size + new Vector3(0.5f, 0.5f, 0.5f);
        p.center = room.center - go.transform.position;
        return 1;
    }

    /// A coarse 3-D lattice through the play space. Density is deliberately low - probes cost
    /// bake time and memory, and indoor lighting here varies smoothly.
    static int BuildLightProbeGroup(GameObject root, Bounds room)
    {
        var go = new GameObject("LabLightProbes");
        go.transform.SetParent(root.transform, false);
        go.transform.position = Vector3.zero;

        var group = go.AddComponent<LightProbeGroup>();
        var pts = new List<Vector3>();

        const float step = 2.0f;
        // Three heights: knee, hands/bench, head. The bench row is the one that matters -
        // it is where every vessel the player picks up actually lives.
        float[] heights = { room.min.y + 0.35f, room.min.y + 1.05f, room.min.y + 1.85f };

        for (float x = room.min.x + 0.6f; x <= room.max.x - 0.6f; x += step)
        for (float z = room.min.z + 0.6f; z <= room.max.z - 0.6f; z += step)
        foreach (var y in heights)
            pts.Add(new Vector3(x, y, z));

        group.probePositions = pts.ToArray();
        return pts.Count;
    }
}
#endif
