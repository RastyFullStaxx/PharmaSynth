#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Adopts the hand-placed workspace (kits, duplicates, reagent shelf, spawn
/// point) as the canonical layout in ONE run:
///   1. renames editor duplicates ("Beaker_100mL (1)") to clean unique names +
///      display names, and gives them the full interaction wiring;
///   2. re-points the teleport target (FrontDoorSpawn) at the rig's current
///      pose;
///   3. re-homes every DropRespawn to its current transform;
///   4. creates + registers the DistillingFlask if missing (its model is an
///      unimported .glb, so no prefab exists) and parks it beside a graduated
///      cylinder;
///   5. drops a ManualLayout_W512 marker that guards the shelf/kits builders
///      from clobbering the hand layout on a re-run.
/// Idempotent; run from SampleScene in edit mode.
public static class ManualLayoutAdopter
{
    public const string MarkerName = "ManualLayout_W512";
    const string FlaskModelPath = "Assets/PharmaSynth/Art/Equipment/DistillationFlask/distillation_flask.glb";
    const string FlaskPrefabPath = "Assets/PharmaSynth/Art/Equipment/DistillationFlask/DistillingFlask.prefab";
    const string LibraryPath = "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset";

    /// True when the hand layout owns the scene (builders must not clobber it).
    public static bool LayoutIsManual() => GameObject.Find(MarkerName) != null;

    [MenuItem("Tools/PharmaSynth/Adopt Manual Layout (W5.12)")]
    public static void Adopt()
    {
        if (Application.isPlaying) { Debug.LogWarning("[AdoptLayout] exit Play mode first."); return; }

        int renamed = RenameAndWireDuplicates();
        bool spawnMoved = AlignTeleportToRig();
        // A flask-model quirk must never abort the re-home + save that follow.
        string flaskNote;
        try { flaskNote = EnsureDistillingFlask(); }
        catch (System.Exception e) { flaskNote = "DistillingFlask SKIPPED (" + e.Message + ")"; Debug.LogWarning("[AdoptLayout] flask: " + e); }
        ReHomeSceneItems.Adopt();                       // homes = the hand layout

        if (GameObject.Find(MarkerName) == null) new GameObject(MarkerName);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[AdoptLayout] manual layout adopted — {renamed} duplicate(s) renamed+wired, "
                  + $"teleport {(spawnMoved ? "re-pointed at the rig" : "unchanged")}, {flaskNote}, all homes re-adopted, "
                  + "builders guarded.</color>");
    }

    // ---- 1. duplicates ------------------------------------------------------

    static readonly Regex DupSuffix = new Regex(@"^(.*?)\s*\((\d+)\)$");

    internal static int RenameAndWireDuplicates()
    {
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        // Longest-first so "TestTubeRack" wins over "TestTube".
        var known = RealSizes.Names.OrderByDescending(n => n.Length).ToArray();
        var taken = new HashSet<string>(
            Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                  .Select(t => t.name));
        int renamed = 0;

        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null) continue;
            var go = t.gameObject;
            var m = DupSuffix.Match(go.name);
            if (!m.Success) continue;
            // Only lab items — never environment dups (walls, shelves, lights).
            if (go.GetComponent<DropRespawn>() == null && go.GetComponent<LabItem>() == null
                && go.GetComponent<LiquidPhysics>() == null) continue;

            string baseName = m.Groups[1].Value.Trim();
            string prefabName = ResolvePrefabName(go, baseName, known);

            // Clean unique name: <PrefabOrBase>_2, _3, …
            string root = prefabName ?? baseName;
            int i = 2; string candidate;
            do { candidate = root + "_" + i; i++; } while (taken.Contains(candidate));
            Undo.RecordObject(go, "Adopt Manual Layout");
            go.name = candidate;
            taken.Add(candidate);
            renamed++;

            // Full treatment (idempotent) + a readable display name.
            if (prefabName != null && PhysicsProfiles.TryGet(prefabName, out _))
                PhysicsAudit.WireSceneItem(go, prefabName, runner);
            var item = go.GetComponent<LabItem>() ?? go.AddComponent<LabItem>();
            if (string.IsNullOrWhiteSpace(item.displayName))
                item.displayName = Mishandling.Prettify(root);
            var lp = go.GetComponent<LiquidPhysics>();
            if (lp != null && registry != null && lp.registry == null) lp.registry = registry;
            EditorUtility.SetDirty(go);
        }
        return renamed;
    }

    /// Prefab identity for a (possibly kit-spawned, prefab-link-less) duplicate:
    /// prefab source first, else the longest known prefab name the GO name
    /// contains ("Kit_Beaker_100mL_1_7 (1)" → "Beaker_100mL").
    static string ResolvePrefabName(GameObject go, string baseName, string[] knownLongestFirst)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (src != null) return src.name;
        foreach (var k in knownLongestFirst)
            if (baseName == k || baseName.Contains(k)) return k;
        return null;
    }

    // ---- 2. teleport/spawn --------------------------------------------------

    static bool AlignTeleportToRig()
    {
        var spawn = GameObject.Find("FrontDoorSpawn");
        var rig = Object.FindAnyObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
        if (spawn == null || rig == null)
        {
            Debug.LogWarning("[AdoptLayout] FrontDoorSpawn or XR Origin not found — teleport target untouched.");
            return false;
        }
        var rigT = rig.transform;
        if ((spawn.transform.position - rigT.position).magnitude < 0.05f) return false;   // already there
        Undo.RecordObject(spawn.transform, "Adopt Manual Layout");
        spawn.transform.position = rigT.position;
        spawn.transform.rotation = Quaternion.Euler(0f, rigT.eulerAngles.y, 0f);
        EditorUtility.SetDirty(spawn.transform);
        Debug.Log($"[AdoptLayout] FrontDoorSpawn → rig pose {rigT.position} yaw {rigT.eulerAngles.y:F0}° "
                  + "(resets/teleports now land where the rig was placed).");
        return true;
    }

    // ---- 4. distilling flask -------------------------------------------------

    static string EnsureDistillingFlask()
    {
        // Remove any prior (possibly half-wired) instance so a re-run rebuilds it
        // cleanly — it's a brand-new prop nobody has positioned yet.
        foreach (var it in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include))
            if (it.itemId == "kit-distillingflask") Object.DestroyImmediate(it.gameObject);

        // Load-or-create the prefab from the glTFast-imported model.
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlaskPrefabPath);
        if (prefab == null)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FlaskModelPath);
            if (model == null) return "DistillingFlask SKIPPED (glb failed to load)";
            var temp = (GameObject)Object.Instantiate(model);
            temp.name = "DistillingFlask";
            PhysicsProfiles.EnsurePhysics(temp, "DistillingFlask");   // convex collider + rb (kinematic)
            if (temp.GetComponent<XRGrab>() == null) temp.AddComponent<XRGrab>();
            GrabTuning.Apply(temp.GetComponent<XRGrab>());
            prefab = PrefabUtility.SaveAsPrefabAsset(temp, FlaskPrefabPath);
            Object.DestroyImmediate(temp);
            if (prefab == null) return "DistillingFlask SKIPPED (prefab save failed)";
        }

        // Register in the SceneAssetLibrary (skip if already listed).
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(LibraryPath);
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var arr = so.FindProperty("prefabs");
            bool listed = false;
            for (int i = 0; i < arr.arraySize; i++)
                if (arr.GetArrayElementAtIndex(i).objectReferenceValue == prefab) listed = true;
            if (!listed)
            {
                arr.InsertArrayElementAtIndex(arr.arraySize);
                arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = prefab;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(lib);
            }
        }

        // Park it beside a graduated cylinder ("place it near the
        // graduated cylinder and I'll adjust manually").
        Transform anchor = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.Contains("GraduatedCylinder")) { anchor = t; break; }
        var inst = (GameObject)Object.Instantiate(prefab);
        inst.name = "DistillingFlask";
        // Real size + rest pose + seat next to the anchor.
        var b = WorldBounds(inst);
        if (RealSizes.TryGet("DistillingFlask", out float target))
            inst.transform.localScale *= RealSizes.UniformScaleFactor(b.size, target);
        b = WorldBounds(inst);
        Vector3 pos = anchor != null
            ? anchor.position + new Vector3(0.14f, 0f, 0f)
            : new Vector3(0f, 1.0f, -3.0f);
        // Drop its bounds bottom to the anchor's bounds bottom (same surface).
        float anchorBottom = anchor != null ? AnchorBottom(anchor) : pos.y;
        inst.transform.position = new Vector3(pos.x, anchorBottom + (inst.transform.position.y - b.min.y) + 0.002f, pos.z);

        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = "kit-distillingflask"; item.displayName = "Distilling Flask";
        PhysicsAudit.WireSceneItem(inst, "DistillingFlask", runner);
        // The glTFast model keeps its meshes on CHILDREN, so the root has no
        // Renderer/MeshFilter — LiquidPhysics requires both on its own GO. Add an
        // empty renderer host (draws nothing; the visible fill is a child mesh
        // EnsureLiquidVisual builds and points mainRenderer at).
        if (inst.GetComponent<MeshFilter>() == null) inst.AddComponent<MeshFilter>();
        if (inst.GetComponent<Renderer>() == null) inst.AddComponent<MeshRenderer>();
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        lp.SetContents(null, 0f);
        ExperimentSceneBuilder.EnsureLiquidVisual(inst, lp);
        if (inst.GetComponent<HazardousMixReactor>() == null) inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);
        if (inst.GetComponent<CleanableVessel>() == null) inst.AddComponent<CleanableVessel>().Bind(lp);
        var pl = inst.GetComponent<ProximityLabel>() ?? inst.AddComponent<ProximityLabel>();
        pl.SetLabel("Distilling Flask", 1.6f);
        if (inst.GetComponent<VesselStatus>() == null) inst.AddComponent<VesselStatus>().Bind(lp, pl, "Distilling Flask", 1.6f);
        if (inst.GetComponent<MixFeedback>() == null) inst.AddComponent<MixFeedback>().Bind(lp);
        var grab = inst.GetComponent<XRGrab>();
        if (grab != null && inst.GetComponent<HoverHighlight>() == null)
            inst.AddComponent<HoverHighlight>().Bind(grab);

        return anchor != null
            ? "DistillingFlask created + placed beside " + anchor.name
            : "DistillingFlask created (no graduated cylinder found — parked at table centre)";
    }

    static float AnchorBottom(Transform anchor)
    {
        var rs = anchor.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return anchor.position.y;
        var b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b.min.y;
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.1f);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
#endif
