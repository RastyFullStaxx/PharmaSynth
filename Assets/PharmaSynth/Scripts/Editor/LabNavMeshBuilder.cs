#if UNITY_EDITOR
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Bakes the walkable surface Tutorial Mode's ground path routes on.
///
/// Without this there is no NavMesh in the project at all, and `GuidePath` silently draws
/// nothing, so the floor arrows look "not implemented" rather than "not baked".
///
/// ⚠ **The bake goes STALE when furniture moves**, like the lightmap bake: the
/// benches ARE the obstacles it routes around. Re-run this together with
/// `Build Lab Probes` → `Run Lab Lighting Bake` after any `Select Movable Furniture`
/// session, or the arrows route through a bench that has since moved.
public static class LabNavMeshBuilder
{
    /// The walkable band, in world Y. Low enough to exclude a ~0.9 m bench top and the
    /// ceiling; tall enough to keep the floor and the lower body of every obstacle.
    const float FloorBandCentreY = 0.225f;
    const float FloorBandHeight = 1.05f;   // -0.3 m .. +0.75 m

    const string HostName = "LabNavMesh";
    const string AssetDir = "Assets/Scenes";
    const string AssetPath = AssetDir + "/LabNavMesh.asset";

    [MenuItem("Tools/PharmaSynth/Build Lab NavMesh (ground path routing)")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabNavMesh] exit Play mode first."); return; }

        var host = GameObject.Find(HostName);
        if (host == null)
        {
            host = new GameObject(HostName);
            Undo.RegisterCreatedObjectUndo(host, "Build Lab NavMesh");
        }

        var surface = host.GetComponent<NavMeshSurface>();
        if (surface == null) surface = host.AddComponent<NavMeshSurface>();

        // RENDER geometry, inside a FLOOR-HEIGHT VOLUME. Render meshes (not colliders)
        // because the lab's floor and furniture are ordinary meshes, and collecting by
        // collider would pull in the trigger volumes we must not walk on.
        //
        // ⛔ The volume is the whole point. CollectObjects.All bakes the entire scene and makes
        // every upward face walkable, bench tops AND the ceiling panels (a mesh from y=0.10 to
        // y=3.10), so `NavMesh.SamplePosition` for a beaker sitting at y=1.26 finds the
        // CEILING 1.6 m ABOVE it before the floor 1.2 m below, and GuidePath draws its chevrons
        // across the ceiling while still logging a route and a chevron count.
        //
        // Everything the player can walk on is within a knee-height band of the floor.
        // Furniture still BLOCKS, because its lower body is inside the band and carves.
        Bounds room = SceneRenderBounds();
        surface.collectObjects = CollectObjects.Volume;
        surface.center = host.transform.InverseTransformPoint(
            new Vector3(room.center.x, FloorBandCentreY, room.center.z));
        surface.size = new Vector3(room.size.x + 2f, FloorBandHeight, room.size.z + 2f);
        surface.layerMask = ~0;

        // A player-sized agent. The radius matters most: too small and the path hugs bench
        // corners the player's body cannot actually round.
        surface.agentTypeID = 0;          // the project's default Humanoid agent
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.08f;        // finer than default — this is one room, not a level

        surface.BuildNavMesh();

        var data = surface.navMeshData;
        if (data == null)
        {
            Debug.LogError("[LabNavMesh] the bake produced NO NavMeshData — the ground path "
                           + "will draw nothing. Check that the lab floor has a render mesh.");
            return;
        }

        // ⛔ PERSIST THE DATA AS ITS OWN ASSET, or the whole scene turns BINARY.
        //
        // BuildNavMesh leaves NavMeshData as an in-memory object owned by the surface, so
        // saving the scene serialises it INTO the scene — and NavMeshData cannot be written
        // as YAML. Unity then silently ignores ForceText and rewrites SampleScene.unity as
        // binary, which breaks git diffs and the scene-reference generator. Nothing warns you.
        System.IO.Directory.CreateDirectory(AssetDir);
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(AssetPath);
        if (existing == null)
        {
            AssetDatabase.CreateAsset(data, AssetPath);
        }
        else
        {
            // Overwrite in place so the surface's reference (and its guid) survive.
            EditorUtility.CopySerialized(data, existing);
            surface.navMeshData = existing;
            data = existing;
        }
        AssetDatabase.SaveAssets();

        EditorUtility.SetDirty(surface);
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        // Report the vertical extent: a mesh reaching ceiling height means the volume is
        // wrong, and that failure is otherwise completely silent.
        var tri = NavMesh.CalculateTriangulation();
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var v in tri.vertices) { if (v.y < lo) lo = v.y; if (v.y > hi) hi = v.y; }
        Debug.Log("<color=#4CD07D>[LabNavMesh] baked</color> — bounds "
                  + data.sourceBounds.size.ToString("0.0") + ", walkable y "
                  + lo.ToString("0.00") + " to " + hi.ToString("0.00")
                  + " over " + tri.vertices.Length + " verts, data at " + AssetPath
                  + ". ⚠ Re-run this after moving furniture, alongside the lighting bake.");
    }

    /// Extent of everything the player can see, used to size the bake volume.
    static Bounds SceneRenderBounds()
    {
        var b = new Bounds();
        bool first = true;
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(
                     FindObjectsInactive.Exclude))
        {
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        if (first) b = new Bounds(Vector3.zero, new Vector3(20f, 4f, 20f));
        return b;
    }
}
#endif
