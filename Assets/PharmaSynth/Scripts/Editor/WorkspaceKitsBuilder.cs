#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Apparatus kits on the workspace gantry shelf (assemble
/// apparatus that belong together, tightly but not overlapping,
/// with generous duplicates of high-use glass). Kit composition follows the
/// manuscript's Appendix C Equipment lists + the game's Methane heating rig:
///   TOP row   — Heating Set A (full Bunsen rig), Heating Set B (compact
///               Bunsen rig), Alcohol-Burner Set (spirit lamp + clay triangle
///               + crucible: the crucible-work set — the manuscript names no
///               burner, so Bunsen = sustained heat, alcohol lamp = crucible).
///   LOWER row — 4 test-tube rack kits (regular tubes / amber HARD-GLASS tubes
///               / vials / empty drying rack) + brush & wash bottle, then the
///               duplicate glassware and small tools.
/// Existing loose apparatus under "EquipmentShelf" is ADOPTED into matching
/// slots (moved + re-homed); anything missing spawns fresh from the
/// SceneAssetLibrary under a "WorkspaceKits" root. Every placed item gets the
/// full interaction treatment (PhysicsAudit.WireSceneItem + vessel extras) and
/// its DropRespawn home = its kit slot. Idempotent + re-runnable.
public static class WorkspaceKitsBuilder
{
    public struct KitSlot
    {
        public string kit;       // cluster name (kits are laid out contiguously)
        public string prefab;    // SceneAssetLibrary prefab name
        public string display;   // human label (LabItem/status/mistake messages)
        public float width;      // reserved slot width along the row, metres
        public KitSlot(string kit, string prefab, string display, float width)
        { this.kit = kit; this.prefab = prefab; this.display = display; this.width = width; }
    }

    const float ItemGap = 0.025f;
    const float ClusterGap = 0.12f;
    const string RootName = "WorkspaceKits";
    const string HardMatPath = "Assets/PharmaSynth/Art/Generated/Materials/HardGlassTube.mat";

    /// TOP row (tall items — full headroom above the gantry).
    public static KitSlot[] Row0Plan() => new[]
    {
        // ONE Bunsen set: the manuscript only ever runs one train at once.
        new KitSlot("Heating Set A", "RetortStand",   "Retort Stand",    0.16f),
        new KitSlot("Heating Set A", "IronRing",      "Iron Ring",       0.18f),
        new KitSlot("Heating Set A", "Tripod",        "Tripod",          0.18f),
        new KitSlot("Heating Set A", "WireGauze",     "Wire Gauze",      0.16f),
        new KitSlot("Heating Set A", "BunsenBurner",  "Bunsen Burner",   0.20f),
        new KitSlot("Heating Set A", "CrucibleTongs", "Crucible Tongs",  0.12f),
        new KitSlot("Alcohol Burner Set", "AlcoholBurner", "Alcohol Burner", 0.10f),
        new KitSlot("Alcohol Burner Set", "ClayTriangle",  "Clay Triangle",  0.14f),
        new KitSlot("Alcohol Burner Set", "Crucible",      "Crucible",       0.07f),
        new KitSlot("Alcohol Burner Set", "CrucibleTongs", "Crucible Tongs", 0.12f),
    };

    /// LOWER row (glassware height only — headroom ≈ 0.32 m).
    public static KitSlot[] Row1Plan() => new[]
    {
        new KitSlot("Test-Tube Racks", "TestTubeRack", "Rack — Regular Tubes",    0.20f),
        new KitSlot("Test-Tube Racks", "TestTubeRack", "Rack — Hard-Glass Tubes", 0.20f),
        new KitSlot("Test-Tube Racks", "TestTubeRack", "Rack — Vials",            0.20f),
        new KitSlot("Test-Tube Racks", "TestTubeBrush","Test-Tube Brush",         0.06f),
        new KitSlot("Test-Tube Racks", "WashBottle",   "Wash Bottle",             0.10f),
        new KitSlot("Beakers & Flasks", "Beaker_100mL",          "Beaker 100 mL",       0.09f),
        new KitSlot("Beakers & Flasks", "Beaker_100mL",          "Beaker 100 mL",       0.09f),
        new KitSlot("Beakers & Flasks", "Beaker_500mL",          "Beaker 500 mL",       0.14f),
        new KitSlot("Beakers & Flasks", "ErlenmeyerFlask_400mL", "Erlenmeyer Flask",    0.12f),
        new KitSlot("Beakers & Flasks", "GraduatedCylinder_50mL","Graduated Cylinder",  0.08f),
        new KitSlot("Bench Tools", "WatchGlass",      "Watch Glass",       0.10f),
        new KitSlot("Bench Tools", "WatchGlass",      "Watch Glass",       0.10f),
        new KitSlot("Bench Tools", "Funnel",          "Funnel",            0.10f),
        new KitSlot("Bench Tools", "GlassRod",        "Stirring Rod",      0.06f),
        new KitSlot("Bench Tools", "EvaporatingDish", "Evaporating Dish",  0.11f),
        new KitSlot("Bench Tools", "Motar",           "Mortar",            0.12f),
        new KitSlot("Bench Tools", "Pestle",          "Pestle",            0.05f),
    };

    /// Pure layout: slot centre x for each entry, whole row centred between
    /// XMin/XMax. Also reports the total used width so the suite pins the fit.
    public static float[] SlotCenters(KitSlot[] slots, out float usedWidth)
    {
        usedWidth = 0f;
        for (int i = 0; i < slots.Length; i++)
        {
            usedWidth += slots[i].width;
            if (i > 0) usedWidth += slots[i].kit == slots[i - 1].kit ? ItemGap : ClusterGap;
        }
        var centers = new float[slots.Length];
        float x = WorkspaceShelfMath.XMin
                  + ((WorkspaceShelfMath.XMax - WorkspaceShelfMath.XMin) - usedWidth) * 0.5f;
        for (int i = 0; i < slots.Length; i++)
        {
            if (i > 0) x += slots[i].kit == slots[i - 1].kit ? ItemGap : ClusterGap;
            centers[i] = x + slots[i].width * 0.5f;
            x += slots[i].width;
        }
        return centers;
    }

    [MenuItem("Tools/PharmaSynth/Build Workspace Kits")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[WorkspaceKits] exit Play mode first."); return; }
        if (ManualLayoutAdopter.LayoutIsManual())
        {
            Debug.LogError("[WorkspaceKits] the scene layout is HAND-PLACED (ManualLayout_W512 marker) — "
                + "a rebuild would clobber it. Delete the marker object to force a rebuild.");
            return;
        }
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib == null) { Debug.LogError("[WorkspaceKits] SceneAssetLibrary.asset not found."); return; }
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);

        // Fresh root (spawned stock); adopted items keep their own parents.
        var old = GameObject.Find(RootName);
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject(RootName);
        var env = GameObject.Find("Environment");
        if (env != null) root.transform.SetParent(env.transform, true);

        // Adoption pool: loose apparatus on the legacy EquipmentShelf.
        var pool = new List<GameObject>();
        var equipShelf = GameObject.Find("EquipmentShelf");
        if (equipShelf != null)
            foreach (Transform c in equipShelf.transform)
                if (c.GetComponentInChildren<Renderer>() != null) pool.Add(c.gameObject);

        int adopted = 0, spawned = 0, tubes = 0;
        for (int row = 0; row < 2; row++)
        {
            var plan = row == 0 ? Row0Plan() : Row1Plan();
            var centers = SlotCenters(plan, out float used);
            float topY = WorkspaceShelfMath.TopYOf(row);
            string lastKit = null;
            float kitStartX = 0f;
            for (int i = 0; i < plan.Length; i++)
            {
                var slot = plan[i];
                var pos = new Vector3(centers[i], topY, WorkspaceShelfMath.ZCenter);

                // Adopt-or-spawn.
                GameObject item = TakeFromPool(pool, slot.prefab);
                if (item != null) adopted++;
                else
                {
                    var prefab = lib.GetPrefab(slot.prefab);
                    if (prefab == null) { Debug.LogWarning("[WorkspaceKits] missing prefab " + slot.prefab); continue; }
                    item = (GameObject)Object.Instantiate(prefab, root.transform);
                    item.name = "Kit_" + slot.prefab + "_" + row + "_" + i;
                    NormaliseTo(item, slot.prefab);
                    spawned++;
                }
                PlaceItem(item, slot.prefab, pos, slot.width);
                WireKitItem(item, slot.prefab, slot.display, runner, registry);

                // Rack kits: fill with their tube kind (homes = rack slots, so
                // the racks refill themselves when tubes are abandoned).
                tubes += FillRack(item, slot.display, lib, root.transform, runner, registry);

                // One sign per kit, above its span.
                if (slot.kit != lastKit)
                {
                    if (lastKit != null) { /* previous sign already placed at kit start */ }
                    lastKit = slot.kit;
                    kitStartX = centers[i] - slot.width * 0.5f;
                }
                bool kitEnds = i == plan.Length - 1 || plan[i + 1].kit != slot.kit;
                if (kitEnds)
                {
                    float kitEndX = centers[i] + slot.width * 0.5f;
                    MakeKitSign(root.transform, slot.kit, (kitStartX + kitEndX) * 0.5f, row);
                }
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[WorkspaceKits] kits built: {adopted} adopted, {spawned} spawned, {tubes} rack tubes/vials; " +
                  "homes = kit slots (no Re-Home needed).");
    }

    // ---- placement helpers --------------------------------------------------

    static GameObject TakeFromPool(List<GameObject> pool, string prefabName)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == null) continue;
            if (PhysicsAudit.PrefabNameFor(pool[i]) == prefabName)
            {
                var g = pool[i];
                pool.RemoveAt(i);
                return g;
            }
        }
        return null;
    }

    /// Rest-pose the item, turn it across the shelf depth if it's too long for
    /// its slot, and seat its bounds bottom on the shelf top.
    static void PlaceItem(GameObject g, string prefabName, Vector3 slotTop, float slotWidth)
    {
        PhysicsProfiles.TryGet(prefabName, out var prof);
        g.transform.rotation = PhysicsProfiles.RestRotation(prof.pose, WorldBounds(g).size);
        var b = WorldBounds(g);
        if (b.size.x > slotWidth + 0.02f)   // long tools lie front-to-back instead
        {
            g.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * g.transform.rotation;
            b = WorldBounds(g);
        }
        float lift = g.transform.position.y - b.min.y;
        g.transform.position = new Vector3(slotTop.x, slotTop.y + lift + 0.002f, slotTop.z);
    }

    /// Full interaction treatment + slot home + label/status for vessels.
    static void WireKitItem(GameObject g, string prefabName, string display,
                            ExperimentRunner runner, ReactionRegistry registry)
    {
        var item = g.GetComponent<LabItem>() ?? g.AddComponent<LabItem>();
        if (string.IsNullOrEmpty(item.itemId)) item.itemId = "kit-" + prefabName.ToLowerInvariant();
        item.displayName = display;

        var rb = PhysicsAudit.WireSceneItem(g, prefabName, runner);
        var grab = g.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && g.GetComponent<HoverHighlight>() == null)
            g.AddComponent<HoverHighlight>().Bind(grab);

        // Kit slot = home, ALWAYS (WireSceneItem only homes brand-new respawns).
        var dr = g.GetComponent<DropRespawn>();
        if (dr != null)
        {
            if (rb != null) dr.Bind(rb, grab);
            dr.SetHome(g.transform.position, g.transform.rotation);
        }

        // Vessels are ready receivers: liquid physics + visible fill + live tag.
        if (IsVessel(prefabName))
        {
            var lp = g.GetComponent<LiquidPhysics>() ?? g.AddComponent<LiquidPhysics>();
            lp.registry = registry;
            lp.SetContents(null, 0f);
            ExperimentSceneBuilder.EnsureLiquidVisual(g, lp);
            if (g.GetComponent<HazardousMixReactor>() == null)
                g.AddComponent<HazardousMixReactor>().Bind(lp, runner);
            var pl = g.GetComponent<ProximityLabel>() ?? g.AddComponent<ProximityLabel>();
            pl.SetLabel(display, 1.6f);
            if (g.GetComponent<CleanableVessel>() == null)
                g.AddComponent<CleanableVessel>().Bind(lp);   // residue → brush/rinse loop
            if (g.GetComponent<VesselStatus>() == null)
                g.AddComponent<VesselStatus>().Bind(lp, pl, display, 1.6f);
            if (g.GetComponent<MixFeedback>() == null)
                g.AddComponent<MixFeedback>().Bind(lp);
        }
        else
        {
            var pl = g.GetComponent<ProximityLabel>() ?? g.AddComponent<ProximityLabel>();
            pl.SetLabel(display, 1.4f);
        }
    }

    static bool IsVessel(string prefabName) =>
        prefabName.StartsWith("Beaker_") || prefabName.StartsWith("ErlenmeyerFlask")
        || prefabName.StartsWith("GraduatedCylinder") || prefabName == "TestTube"
        || prefabName == "Vial" || prefabName == "WatchGlass"
        || prefabName == "EvaporatingDish" || prefabName == "Crucible";

    /// Fill a rack with its capped, reusable tube pool + a RackDispenser
    /// (unmovable rack, pull single tubes, hard cap = hole count).
    /// Each tube's DropRespawn home is its hole, so an abandoned tube returns and
    /// the rack visibly re-stocks; the finite pool IS the cap. Returns the count.
    static int FillRack(GameObject rack, string display, SceneAssetLibrary lib,
                        Transform root, ExperimentRunner runner, ReactionRegistry registry)
    {
        string tubePrefab; string tubeLabel, kind; int count; bool hardGlass = false;
        switch (display)
        {
            case "Rack — Regular Tubes":    tubePrefab = "TestTube"; tubeLabel = "Test Tube"; kind = "Test Tubes"; count = 6; break;   // enol-test peak = 5 + spare
            case "Rack — Hard-Glass Tubes": tubePrefab = "TestTube"; tubeLabel = "Hard-Glass Test Tube"; kind = "Hard-Glass Tubes"; count = 4; hardGlass = true; break;
            case "Rack — Vials":            tubePrefab = "Vial"; tubeLabel = "Vial"; kind = "Vials"; count = 4; break;
            default: return 0;
        }
        var prefab = lib.GetPrefab(tubePrefab);
        if (prefab == null) return 0;

        // The rack is inert furniture: unmovable (no grab) + kinematic.
        var rgrab = rack.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (rgrab != null) Object.DestroyImmediate(rgrab);
        var rrb = rack.GetComponent<Rigidbody>() ?? rack.AddComponent<Rigidbody>();
        rrb.isKinematic = true;
        var rdr = rack.GetComponent<DropRespawn>();
        if (rdr != null) Object.DestroyImmediate(rdr);   // the rack never wanders home

        var b = WorldBounds(rack);
        var holes = new Transform[count];
        var tubes = new GameObject[count];
        for (int i = 0; i < count; i++)
        {
            int col = i % 3, row = i / 3;
            var pos = new Vector3(
                Mathf.Lerp(b.min.x + 0.03f, b.max.x - 0.03f, count <= 3 ? i / 2f : col / 2f),
                b.max.y + 0.02f,
                Mathf.Lerp(b.min.z + 0.02f, b.max.z - 0.02f, count <= 3 ? 0.5f : row));
            var tube = (GameObject)Object.Instantiate(prefab, root);
            tube.name = "Kit_" + tubeLabel.Replace(" ", "") + "_" + i;
            NormaliseTo(tube, tubePrefab);
            tube.transform.rotation = Quaternion.identity;   // tubes stand upright in the rack
            var tb = WorldBounds(tube);
            tube.transform.position = new Vector3(pos.x, pos.y + (tube.transform.position.y - tb.min.y), pos.z);
            if (hardGlass) ApplyHardGlassTint(tube);
            WireKitItem(tube, tubePrefab, tubeLabel, runner, registry);   // home = this hole
            tubes[i] = tube;

            var hole = new GameObject("Hole_" + i).transform;   // stable anchor the tube returns to
            hole.SetParent(rack.transform, true);
            hole.position = tube.transform.position;
            holes[i] = hole;
        }
        var pl = rack.GetComponent<ProximityLabel>() ?? rack.AddComponent<ProximityLabel>();
        rack.AddComponent<RackDispenser>().Bind(holes, tubes, pl, kind);
        return count;
    }

    /// Amber borosilicate tint so hard-glass tubes read apart from regular ones
    /// One persisted material asset, shared by all of them.
    static void ApplyHardGlassTint(GameObject tube)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HardMatPath);
        if (mat == null)
        {
            var r0 = tube.GetComponentInChildren<Renderer>();
            if (r0 == null || r0.sharedMaterial == null) return;
            mat = new Material(r0.sharedMaterial) { name = "HardGlassTube" };
            var amber = new Color(1.0f, 0.72f, 0.35f, 1f);
            if (mat.HasProperty("_BaseColor"))
            {
                var c = mat.GetColor("_BaseColor");
                mat.SetColor("_BaseColor", new Color(c.r * amber.r, c.g * amber.g, c.b * amber.b, Mathf.Max(c.a, 0.45f)));
            }
            else if (mat.HasProperty("_Color"))
            {
                var c = mat.GetColor("_Color");
                mat.SetColor("_Color", new Color(c.r * amber.r, c.g * amber.g, c.b * amber.b, Mathf.Max(c.a, 0.45f)));
            }
            AssetDatabase.CreateAsset(mat, HardMatPath);
        }
        foreach (var r in tube.GetComponentsInChildren<Renderer>())
            if (r.name != "Liquid") r.sharedMaterial = mat;
    }

    /// Small holo-style sign above each kit cluster.
    static void MakeKitSign(Transform root, string kit, float x, int row)
    {
        var go = new GameObject("KitSign_" + kit.Replace(" ", ""));
        go.transform.SetParent(root, true);
        float y = WorkspaceShelfMath.TopYOf(row) + (row == 0 ? 0.35f : 0.26f);
        go.transform.position = new Vector3(x, y, WorkspaceShelfMath.ZCenter - WorkspaceShelfMath.Depth * 0.5f + 0.02f);
        // Authored to read from the room front (FaceCamera only runs in Play;
        // TMP's readable face pointed away in edit-mode captures).
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = Vector3.one * 0.03f;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = kit;
        tmp.fontSize = 8f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = new Color(0.75f, 0.9f, 1f, 0.95f);
        tmp.fontStyle = FontStyles.Bold;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) { mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.sortingOrder = 4000; }
        var fc = go.AddComponent<FaceCamera>();
        fc.yAxisOnly = true;
        fc.faceTowardCamera = false;   // TMP reads down its -Z; toward-camera mirrors it
    }

    static void NormaliseTo(GameObject g, string prefabName)
    {
        if (!RealSizes.TryGet(prefabName, out float target)) return;
        var b = WorldBounds(g);
        g.transform.localScale *= RealSizes.UniformScaleFactor(b.size, target);
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.05f);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
#endif
