#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Wires the distillation-completion apparatus into the game: the 6
/// generated pieces (Condenser, RubberStopper, DeliveryTube, WaterBath,
/// UtilityClamp, Aspirator) plus the 3 that exist only as raw models
/// (Pipette, Thermometer, FlorenceFlask). Each is prefabbed if needed,
/// registered in the SceneAssetLibrary, and one instance is spawned + fully
/// wired + placed in a tidy row beside the distilling flask (the user then
/// nudges + re-homes). Idempotent. Size/physics/breakage live in the code
/// tables (RealSizes/PhysicsProfiles/Mishandling).
public static class DistillationApparatusWiring
{
    const string LibraryPath = "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset";
    const string Refs = "Assets/PharmaSynth/Art/Generated/Refs/";
    const string RootName = "DistillationApparatus";

    struct Spec
    {
        public string prefabName, display, modelPath, prefabPath;
        public bool isVessel;
        public Spec(string p, string d, string model, string prefab, bool vessel)
        { prefabName = p; display = d; modelPath = model; prefabPath = prefab; isVessel = vessel; }
    }

    static List<Spec> Plan() => new List<Spec>
    {
        // Generated — already prefabs in Refs/ (modelPath == prefabPath).
        new Spec("Condenser",     "Condenser",      Refs+"Condenser.prefab",     Refs+"Condenser.prefab",     false),
        new Spec("DeliveryTube",  "Delivery Tube",  Refs+"DeliveryTube.prefab",  Refs+"DeliveryTube.prefab",  false),
        new Spec("RubberStopper", "Rubber Stopper", Refs+"RubberStopper.prefab", Refs+"RubberStopper.prefab", false),
        new Spec("WaterBath",     "Water Bath",     Refs+"WaterBath.prefab",     Refs+"WaterBath.prefab",     false),
        new Spec("UtilityClamp",  "Utility Clamp",  Refs+"UtilityClamp.prefab",  Refs+"UtilityClamp.prefab",  false),
        new Spec("Aspirator",     "Aspirator",      Refs+"Aspirator.prefab",     Refs+"Aspirator.prefab",     false),
        // Existing raw models — prefab them here, then register + place.
        // (Pipette is left out: it models a modern micropipette, and the Dropper +
        //  graduated cylinder cover the manuscript's drops/ml role.)
        new Spec("Thermometer",   "Thermometer",    "Assets/PharmaSynth/Art/Equipment/Thermometer/thermometer.glb",
                                                    "Assets/PharmaSynth/Art/Equipment/Thermometer/Thermometer.prefab", false),
        new Spec("FlorenceFlask", "Florence Flask", Refs+"FlorenceFlask.glb", Refs+"FlorenceFlask.prefab", true),
    };

    [MenuItem("Tools/PharmaSynth/Wire Distillation Apparatus (W5.12)")]
    public static void Wire()
    {
        if (Application.isPlaying) { Debug.LogWarning("[DistApparatus] exit Play mode first."); return; }
        // NEVER clobber a hand-placed layout, since a re-run resets manually
        // moved + duplicated apparatus. Same guard the shelf/kits builders use;
        // once the layout is manual, this menu is one-shot-done.
        if (ManualLayoutAdopter.LayoutIsManual())
        {
            Debug.LogError("[DistApparatus] the scene layout is HAND-PLACED (ManualLayout_W512 marker) — "
                + "re-running would reset your moved/duplicated apparatus. It is already wired; nothing to do. "
                + "Delete the marker object only if you truly want to rebuild the default row.");
            return;
        }
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(LibraryPath);
        if (lib == null) { Debug.LogError("[DistApparatus] SceneAssetLibrary not found."); return; }
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);

        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        var env = GameObject.Find("Environment");
        if (env != null) root.transform.SetParent(env.transform, true);

        // Anchor the row beside the distilling flask (fallback: table centre).
        Vector3 basePos = new Vector3(-0.6f, 0.95f, -3.0f);
        foreach (var it in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include))
            if (it.itemId == "kit-distillingflask") { basePos = it.transform.position + new Vector3(-0.25f, 0f, 0f); break; }

        var plan = Plan();
        int wired = 0, prefabbed = 0, registered = 0;
        float x = 0f;
        foreach (var s in plan)
        {
            var prefab = EnsurePrefab(s, ref prefabbed);
            if (prefab == null) { Debug.LogWarning("[DistApparatus] no prefab for " + s.prefabName); continue; }
            if (RegisterInLibrary(lib, prefab)) registered++;

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.name = s.prefabName;
            inst.transform.SetParent(root.transform, true);
            NormaliseTo(inst, s.prefabName);
            SeatAt(inst, basePos + new Vector3(x, 0f, 0f));
            x -= 0.16f;   // tidy row to the left of the flask
            WireItem(inst, s, runner, registry);
            wired++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#4CD07D>[DistApparatus] {wired} apparatus wired + placed beside the flask "
                  + $"({prefabbed} prefabbed from raw models, {registered} newly registered in the library). "
                  + "Nudge + Re-Home to lock positions.</color>");
    }

    /// Load the prefab; for raw-model specs, build the prefab first (once).
    static GameObject EnsurePrefab(Spec s, ref int prefabbed)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(s.prefabPath);
        if (existing != null) return existing;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(s.modelPath);
        if (model == null) return null;
        var temp = (GameObject)Object.Instantiate(model);
        temp.name = s.prefabName;
        PhysicsProfiles.EnsurePhysics(temp, s.prefabName);
        if (temp.GetComponent<XRGrab>() == null) temp.AddComponent<XRGrab>();
        GrabTuning.Apply(temp.GetComponent<XRGrab>());
        var saved = PrefabUtility.SaveAsPrefabAsset(temp, s.prefabPath);
        Object.DestroyImmediate(temp);
        if (saved != null) prefabbed++;
        return saved;
    }

    static bool RegisterInLibrary(SceneAssetLibrary lib, GameObject prefab)
    {
        var so = new SerializedObject(lib);
        var arr = so.FindProperty("prefabs");
        for (int i = 0; i < arr.arraySize; i++)
            if (arr.GetArrayElementAtIndex(i).objectReferenceValue == prefab) return false;
        arr.InsertArrayElementAtIndex(arr.arraySize);
        arr.GetArrayElementAtIndex(arr.arraySize - 1).objectReferenceValue = prefab;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(lib);
        return true;
    }

    static void WireItem(GameObject inst, Spec s, ExperimentRunner runner, ReactionRegistry registry)
    {
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = "kit-" + s.prefabName.ToLowerInvariant();
        item.displayName = s.display;
        PhysicsAudit.WireSceneItem(inst, s.prefabName, runner);   // physics/grab/respawn/impact/breakable-nature/home
        var grab = inst.GetComponent<XRGrab>();
        if (grab != null && inst.GetComponent<HoverHighlight>() == null) inst.AddComponent<HoverHighlight>().Bind(grab);

        if (s.isVessel)
        {
            // glTFast/Florence root may lack a Renderer host that LiquidPhysics needs.
            if (inst.GetComponent<MeshFilter>() == null) inst.AddComponent<MeshFilter>();
            if (inst.GetComponent<Renderer>() == null) inst.AddComponent<MeshRenderer>();
            var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
            lp.registry = registry; lp.SetContents(null, 0f);
            ExperimentSceneBuilder.EnsureLiquidVisual(inst, lp);
            if (inst.GetComponent<HazardousMixReactor>() == null) inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);
            if (inst.GetComponent<CleanableVessel>() == null) inst.AddComponent<CleanableVessel>().Bind(lp);
            var pl = inst.GetComponent<ProximityLabel>() ?? inst.AddComponent<ProximityLabel>();
            pl.SetLabel(s.display, 1.6f);
            if (inst.GetComponent<VesselStatus>() == null) inst.AddComponent<VesselStatus>().Bind(lp, pl, s.display, 1.6f);
            if (inst.GetComponent<MixFeedback>() == null) inst.AddComponent<MixFeedback>().Bind(lp);
        }
        else
        {
            var pl = inst.GetComponent<ProximityLabel>() ?? inst.AddComponent<ProximityLabel>();
            pl.SetLabel(s.display, 1.4f);
        }
    }

    static void NormaliseTo(GameObject g, string prefabName)
    {
        if (!RealSizes.TryGet(prefabName, out float target)) return;
        var b = WorldBounds(g);
        g.transform.localScale *= RealSizes.UniformScaleFactor(b.size, target);
    }

    static void SeatAt(GameObject g, Vector3 topPos)
    {
        PhysicsProfiles.TryGet(g.name, out var prof);
        g.transform.rotation = PhysicsProfiles.RestRotation(prof.pose, WorldBounds(g).size);
        var b = WorldBounds(g);
        g.transform.position = new Vector3(topPos.x, topPos.y + (g.transform.position.y - b.min.y) + 0.002f, topPos.z);
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
