#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// The SCENE half of a set of fixes whose
/// code half lives in the runtime scripts. One idempotent menu per symptom so a
/// single broken area can be re-run alone; "Fix Everything" runs them in order.
///
/// These are separate from the existing builders because none of them own these
/// objects: three apparatus sit in the scene as RAW model prefabs with no
/// wiring (no collider, no rigidbody, no XRGrabInteractable, so ungrabbable),
/// the wash bottle has no contents, and the balance is pure set dressing.
public static class W533Fixes
{
    const string MenuRoot = "Tools/PharmaSynth/Playtest Fixes (W5.33)/";
    const string RegistryPath = "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset";
    const string GlassOuterGuid = "5fa2d54e0de4b1844bd36402333542fe";
    const string GlassInnerGuid = "ff04f2be4ce6d934a8624dfa3c34aa4b";

    /// The apparatus that need grab wiring. All five sit under
    /// DistillationApparatus; the two DistillingFlasks are wired but share the row.
    public static readonly string[] UngrabbableReport =
    {
        "WaterBath", "RubberStopper", "RubberStopper_2", "DeliveryTube",
        "FlorenceFlask", "DistillingFlask", "DistillingFlask_2",
    };

    /// Prefab assets that are bare imported models (no collider, no
    /// rigidbody, no grab). Fixing the ASSET fixes every instance, present and future.
    static readonly string[] RawPrefabs =
    {
        "Assets/PharmaSynth/Art/Generated/Refs/WaterBath.prefab",
        "Assets/PharmaSynth/Art/Generated/Refs/RubberStopper.prefab",
        "Assets/PharmaSynth/Art/Generated/Refs/DeliveryTube.prefab",
        "Assets/PharmaSynth/Art/Generated/Refs/FlorenceFlask.prefab",
    };

    [MenuItem(MenuRoot + "Fix Everything (W5.33)")]
    public static void FixEverything()
    {
        if (!EditorGuard()) return;
        int racks = FixRacksAnchoredCore();
        int yieldGone = FixYieldGoneCore();
        int grab = FixGrabbableCore();
        int bottle = FixWashBottleCore();
        int glass = FixFlorenceFlaskCore();
        int funnels = FixFunnelsCore();
        int scale = FixBalanceCore();
        int panel = FixResultPanelsCore();
        int tags = FixVesselTagsCore();
        LabelForge.Run();
        Save();
        Debug.Log($"<color=#4CD07D>[W5.33] {racks} tube rack(s) anchored, {yieldGone} yield widget(s) removed, {grab} apparatus made grabbable, wash bottle {(bottle > 0 ? "stocked" : "already stocked")}, "
                  + $"{glass} Florence-flask renderer(s) glassed + round-bottom fill, {funnels} funnel(s) wired to drip from the stem, "
                  + $"balance {(scale > 0 ? "made live" : "already live")}, {panel} result-panel element(s) re-laid out, "
                  + $"{tags} vessel(s) given a live contents tag, reagent labels re-mounted. "
                  + "Run 'Apply W5.8 Verb Data' next if the water bath still reports unbound.</color>");
    }

    // ---- 0. FIXED FURNITURE: racks must never be pickable --------------------

    /// The tube racks. Two roles, both FIXED: the STORAGE racks
    /// where tubes live and home, and the WORKSPACE holders that start empty and
    /// receive tubes mid-experiment. Neither is a hand tool — picking one up
    /// carries every seated tube with it and wrecks the bench.
    public static readonly string[] FixedRacks =
    {
        "Eq_TestTubeRack", "TestTubeRack_2", "TestTubeRack_3", "TestTubeRack_4", "TestTubeRack_5",
        "Experiment_Tube_Table_Kit_Holder_1", "Experiment_Tube_Table_Kit_Holder_2",
        "Experiment_Tube_Table_Kit_Holder_3", "Experiment_Tube_Table_Kit_Holder_4",
    };

    [MenuItem(MenuRoot + "Anchor Tube Racks (not grabbable)")]
    public static void FixRacksAnchored()
    { if (!EditorGuard()) return; int n = FixRacksAnchoredCore(); Save(); Debug.Log($"[W5.33] {n} rack(s) anchored."); }

    /// Strip the grab, pin the body. Colliders are KEPT — the tubes still rest on
    /// the rack and the player still cannot walk through it. The Rigidbody is made
    /// kinematic rather than destroyed so nothing doing GetComponent<Rigidbody>()
    /// starts null-referencing.
    static int FixRacksAnchoredCore()
    {
        int changed = 0;
        foreach (string n in FixedRacks)
        {
            var go = GameObject.Find(n);
            if (go == null) continue;
            bool touched = false;

            foreach (var grab in go.GetComponents<XRGrab>())
            { Undo.DestroyObjectImmediate(grab); touched = true; }

            var rb = go.GetComponent<Rigidbody>();
            if (rb != null && (!rb.isKinematic || rb.useGravity))
            {
                Undo.RecordObject(rb, "anchor rack");
                rb.isKinematic = true;
                rb.useGravity = false;
                touched = true;
            }

            // A rack that cannot move cannot be dropped, so its respawn bookkeeping
            // is dead weight that would also fight the anchoring.
            var respawn = go.GetComponent<DropRespawn>();
            if (respawn != null) { Undo.DestroyObjectImmediate(respawn); touched = true; }

            if (touched) { changed++; EditorUtility.SetDirty(go); }
        }
        return changed;
    }

    // ---- 0b. the yield stepper is GONE --------------------------------------

    /// Removes the data sheet's yield control.
    /// `PostLabController.Yield` is read by nothing but the self-tests, so the
    /// player's number would be silently discarded, and the game never states the
    /// theoretical mass to divide by anyway. Yield never affects the grade (client
    /// ruling), so the control would only misrepresent itself.
    public static readonly string[] YieldWidgets = { "YieldPlus", "YieldMinus", "YieldValue" };

    [MenuItem(MenuRoot + "Remove Yield Stepper")]
    public static void FixYieldGone()
    { if (!EditorGuard()) return; int n = FixYieldGoneCore(); Save(); Debug.Log($"[W5.33] {n} yield widget(s) removed."); }

    static int FixYieldGoneCore()
    {
        int removed = 0;
        foreach (string n in YieldWidgets)
        {
            var go = FindAnyByName(n);
            if (go == null) continue;
            Undo.DestroyObjectImmediate(go);
            removed++;
        }
        return removed;
    }

    /// GameObject.Find only sees ACTIVE objects, and the data sheet / quiz panel is
    /// disabled until the review, so a plain Find silently reports "nothing to
    /// do" for every widget on it. Search inactive too.
    public static GameObject FindAnyByName(string n)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t != null && t.name == n) return t.gameObject;
        return null;
    }

    // ---- 1. ungrabbable apparatus -------------------------------------------

    [MenuItem(MenuRoot + "Make Apparatus Grabbable")]
    public static void FixGrabbable() { if (!EditorGuard()) return; int n = FixGrabbableCore(); Save(); Debug.Log($"[W5.33] {n} apparatus wired for grabbing."); }

    static int FixGrabbableCore()
    {
        // Fix the ASSETS first, so the instances inherit collider+body+grab.
        foreach (var path in RawPrefabs) EnsurePrefabInteractable(path);

        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        int wired = 0;
        foreach (var go in ApparatusInScene())
        {
            if (go == null) continue;
            string prefabName = PhysicsAudit.PrefabNameFor(go);
            Undo.RegisterFullObjectHierarchyUndo(go, "W5.33 grabbable");
            bool changed = EnsureCollider(go);
            if (go.GetComponent<XRGrab>() == null) { go.AddComponent<XRGrab>(); changed = true; }
            var grab = go.GetComponent<XRGrab>();
            // Interaction layer 0 = "Nothing" makes an interactable invisible to
            // every interactor — indistinguishable from having no grab at all.
            if (grab.interactionLayers.value == 0) grab.interactionLayers = 1;
            GrabTuning.Apply(grab);
            var item = go.GetComponent<LabItem>() ?? go.AddComponent<LabItem>();
            if (string.IsNullOrEmpty(item.itemId)) item.itemId = "kit-" + prefabName.ToLowerInvariant();
            if (string.IsNullOrEmpty(item.displayName)) item.displayName = Mishandling.DisplayNameFor(go);
            if (go.GetComponent<HoverHighlight>() == null) go.AddComponent<HoverHighlight>().Bind(grab);
            var pl = go.GetComponent<ProximityLabel>() ?? go.AddComponent<ProximityLabel>();
            pl.SetLabel(item.displayName, 1.4f);
            if (PhysicsProfiles.TryGet(prefabName, out _)) PhysicsAudit.WireSceneItem(go, prefabName, runner);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            EditorUtility.SetDirty(go);
            if (changed) wired++;
        }
        return wired;
    }

    /// Every hand-placed apparatus that a player is meant to be able to pick up:
    /// the prop groups plus anything already carrying a LabItem / LiquidPhysics.
    /// DistillationApparatus is included explicitly because PhysicsAudit.SceneItems
    /// does not list it.
    static IEnumerable<GameObject> ApparatusInScene()
    {
        var seen = new HashSet<GameObject>();
        foreach (var groupName in new[] { "DistillationApparatus", "EquipmentShelf", "BenchApparatus",
                                          "WorkspaceKits", "MethaneProps" })
        {
            var group = GameObject.Find(groupName);
            if (group == null) continue;
            foreach (Transform t in group.transform)
                if (t.GetComponentInChildren<Renderer>() != null) seen.Add(t.gameObject);
        }
        foreach (var n in UngrabbableReport)
        {
            var go = GameObject.Find(n);
            if (go != null) seen.Add(go);
        }
        return seen;
    }

    /// A grabbable needs SOMETHING for the ray to hit. Adds a BoxCollider fitted to
    /// the object's own solid meshes when there is no non-trigger collider that
    /// covers them (raw Tripo prefabs have none at all).
    static bool EnsureCollider(GameObject go)
    {
        DedupeColliders(go);
        foreach (var c in go.GetComponentsInChildren<Collider>(true))
            if (c != null && c.enabled && !c.isTrigger) return false;    // already hittable

        var lb = ExperimentSceneBuilder.LocalMeshBounds(go.transform);
        if (lb.size.sqrMagnitude <= 1e-8f) return false;
        var box = go.AddComponent<BoxCollider>();
        box.center = lb.center;
        box.size = lb.size;
        return true;
    }

    /// Drop identical duplicate box colliders on one object. Fixing the PREFAB and
    /// then the instance in the same pass can add the same fitted box twice (the
    /// reimport has not propagated when the instance is inspected), and two
    /// coincident colliders on a grabbable make the physics fight itself.
    static void DedupeColliders(GameObject go)
    {
        var boxes = go.GetComponents<BoxCollider>();
        for (int i = boxes.Length - 1; i > 0; i--)
        {
            for (int j = 0; j < i; j++)
            {
                if (boxes[j] == null || boxes[i] == null) continue;
                if ((boxes[i].center - boxes[j].center).sqrMagnitude < 1e-8f
                    && (boxes[i].size - boxes[j].size).sqrMagnitude < 1e-8f
                    && boxes[i].isTrigger == boxes[j].isTrigger)
                { Object.DestroyImmediate(boxes[i], true); break; }
            }
        }
    }

    /// Give a raw imported prefab the interactable core (collider + body + grab).
    static void EnsurePrefabInteractable(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset == null) return;
        if (asset.GetComponent<XRGrab>() != null && asset.GetComponentInChildren<Collider>(true) != null) return;

        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            EnsureCollider(root);
            PhysicsProfiles.EnsurePhysics(root, root.name);
            var grab = root.GetComponent<XRGrab>() ?? root.AddComponent<XRGrab>();
            if (grab.interactionLayers.value == 0) grab.interactionLayers = 1;
            GrabTuning.Apply(grab);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ---- 2. the empty wash bottle -------------------------------------------

    [MenuItem(MenuRoot + "Stock the Wash Bottle")]
    public static void FixWashBottle() { if (!EditorGuard()) return; int n = FixWashBottleCore(); Save(); Debug.Log($"[W5.33] wash bottle: {n} change(s)."); }

    /// A wash bottle with no LiquidPhysics at all is why "nothing comes out when I
    /// pour it". It is the lab's distilled-water dispenser, so it
    /// is stocked generously and is never a graded reagent source.
    static int FixWashBottleCore()
    {
        var water = FindChemical("Distilled Water") ?? FindChemical("Water");
        if (water == null) { Debug.LogWarning("[W5.33] no Distilled Water ChemicalData — wash bottle left empty."); return 0; }
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(RegistryPath);
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);

        int changed = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.name.Contains("WashBottle")) continue;
            var host = t.gameObject;
            if (host.GetComponent<Renderer>() == null)
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true)) { host = r.gameObject; break; }
            var lp = host.GetComponent<LiquidPhysics>();
            if (lp == null)
            {
                lp = host.AddComponent<LiquidPhysics>();
                lp.maxVolume = 500f;
                changed++;
            }
            if (lp.currentLiquidVolume < 1f) { lp.SetContents(water, 500f); changed++; }
            lp.registry = registry;
            if (ShelfPourWiring.WireBottle(host, runner, registry) > 0) changed++;
            var pl = host.GetComponent<ProximityLabel>() ?? host.AddComponent<ProximityLabel>();
            pl.SetLabel("Wash Bottle — Distilled Water", 1.4f);
            if (host.GetComponent<VesselStatus>() == null)
                host.AddComponent<VesselStatus>().Bind(lp, pl, "Wash Bottle", 1.4f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lp);
            EditorUtility.SetDirty(host);
        }
        return changed;
    }

    // ---- 3. the Florence flask ----------------------------------------------

    [MenuItem(MenuRoot + "Glass the Florence Flask")]
    public static void FixFlorenceFlask() { if (!EditorGuard()) return; int n = FixFlorenceFlaskCore(); Save(); Debug.Log($"[W5.33] Florence flask: {n} renderer(s)."); }

    /// A ROUND-BOTTOM flask must not render as solid white with a cylinder of liquid.
    /// Swap in the same borosilicate glass the beakers use, then
    /// throw away the cylindrical fill child so EnsureLiquidVisual rebuilds it as
    /// the sphere IsRoundBottom asks for.
    static int FixFlorenceFlaskCore()
    {
        var outer = LoadMat(GlassOuterGuid);
        if (outer == null) { Debug.LogWarning("[W5.33] GlassOuterMat missing — flask left as-is."); return 0; }
        var inner = LoadMat(GlassInnerGuid) ?? outer;
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(RegistryPath);

        int done = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !ExperimentSceneBuilder.IsRoundBottom(t.name)) continue;
            foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (ExperimentSceneBuilder.IsEffectChild(r.name)) continue;
                var mf = r.GetComponent<MeshFilter>();
                // An EMPTY host renderer (LiquidPhysics needs one on the root, so the
                // wiring pass adds a mesh-less MeshRenderer) draws nothing but does
                // log a null-material warning — give it the glass too rather than
                // leaving a dangling null.
                if (mf == null || mf.sharedMesh == null)
                {
                    if (r.sharedMaterial == null) { r.sharedMaterial = outer; EditorUtility.SetDirty(r); }
                    continue;
                }
                var mats = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                for (int i = 0; i < mats.Length; i++) mats[i] = i == 0 ? outer : inner;
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
                done++;
            }
            // Rebuild the fill as a bulb.
            var host = t.gameObject;
            var lp = host.GetComponent<LiquidPhysics>();
            if (lp == null) continue;
            foreach (var name in new[] { "Liquid", "Precipitate" })
            {
                var old = t.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            lp.mainRenderer = null; lp.precipitateRenderer = null;
            lp.registry = registry;
            ExperimentSceneBuilder.EnsureLiquidVisual(host, lp);
            PrefabUtility.RecordPrefabInstancePropertyModifications(lp);
        }
        return done;
    }

    // ---- 4. funnels ----------------------------------------------------------

    [MenuItem(MenuRoot + "Wire Funnel Flow-Through")]
    public static void FixFunnels() { if (!EditorGuard()) return; int n = FixFunnelsCore(); Save(); Debug.Log($"[W5.33] {n} funnel(s) wired."); }

    static int FixFunnelsCore()
    {
        int n = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.name.Contains("Funnel")) continue;
            if (t.GetComponent<LiquidPassthrough>() != null) { n++; continue; }
            t.gameObject.AddComponent<LiquidPassthrough>();
            EditorUtility.SetDirty(t.gameObject);
            n++;
        }
        return n;
    }

    // ---- 5. the balance ------------------------------------------------------

    [MenuItem(MenuRoot + "Make the Balance Live")]
    public static void FixBalance() { if (!EditorGuard()) return; int n = FixBalanceCore(); Save(); Debug.Log($"[W5.33] balance: {n} change(s)."); }

    /// The balance's digital canvas has no controller and no pan sensor of its own,
    /// so the readout would never update whatever rests on it.
    /// Give it a permanent pan trigger + readout, independent of any
    /// experiment: the ZONE-FREE TOOL RULE applied to the scale.
    static int FixBalanceCore()
    {
        int changed = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || !t.name.Contains("Balance") || t.name.Contains("Digital")) continue;
            var go = t.gameObject;

            var display = FindDisplay(t);
            var scale = go.GetComponent<WeighingScaleController>();
            if (scale == null) { scale = go.AddComponent<WeighingScaleController>(); changed++; }
            scale.Bind(display);

            // The readout has to be readable from wherever the player stands.
            if (display != null)
            {
                var canvas = display.GetComponentInParent<Canvas>();
                var host = canvas != null ? canvas.gameObject : display.gameObject;
                if (host.GetComponent<FaceCamera>() == null)
                {
                    var fc = host.AddComponent<FaceCamera>();
                    fc.yAxisOnly = true;            // stay upright: it is a bench instrument
                    fc.faceTowardCamera = false;    // UI reads with +Z pointing away
                    changed++;
                }
            }

            // Pan sensor: a thin trigger slab over the balance's top face.
            var pan = t.Find("Pan");
            if (pan == null)
            {
                var panGo = new GameObject("Pan");
                panGo.transform.SetParent(t, false);
                pan = panGo.transform;
                changed++;
            }
            var lb = ExperimentSceneBuilder.LocalMeshBounds(t, "Pan");
            var box = pan.GetComponent<BoxCollider>() ?? pan.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = Vector3.zero;
            box.size = new Vector3(lb.size.x * 0.8f, Mathf.Max(0.02f, lb.size.y * 0.35f), lb.size.z * 0.8f);
            pan.localPosition = new Vector3(lb.center.x, lb.max.y, lb.center.z);

            var station = pan.GetComponent<WeighStation>() ?? pan.gameObject.AddComponent<WeighStation>();
            // No runner / no taskId: a permanently live readout. A weigh STEP still
            // binds its own station on top of this through the normal builder path.
            station.Bind(null, null, null, null, 0f, scale);
            EditorUtility.SetDirty(go);
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        }
        return changed;
    }

    /// The balance's grams field: the digital canvas's numeric text, else any TMP
    /// under the balance that is not the unit suffix.
    static TMP_Text FindDisplay(Transform balance)
    {
        TMP_Text fallback = null;
        foreach (var tmp in balance.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null) continue;
            if (tmp.name.Contains("Num")) return tmp;
            if (fallback == null && !tmp.name.EndsWith("G")) fallback = tmp;
        }
        return fallback;
    }

    // ---- 6. quiz + grade panels ---------------------------------------------

    [MenuItem(MenuRoot + "Fix Quiz + Result Panel Overlaps")]
    public static void FixResultPanels() { if (!EditorGuard()) return; int n = FixResultPanelsCore(); Save(); Debug.Log($"[W5.33] {n} panel element(s) re-laid out."); }

    /// Two separate overlaps:
    ///   • the quiz row (Back and Next 73 units ON TOP of Submit), handled by
    ///     QuizNavButtonsBuilder shrinking all three to a fitting width;
    ///   • the grade card: the ten-line Breakdown is authored at 26 pt in a
    ///     240-tall box with Overflow, so it spills ~70 units down over the
    ///     Retry / Continue buttons. Auto-sizing keeps it inside its band.
    static int FixResultPanelsCore()
    {
        int n = 0;
        QuizNavButtonsBuilder.Build();

        var grade = Object.FindFirstObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (grade == null) return n;
        foreach (var tmp in grade.GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null) continue;
            if (tmp.name == "Breakdown")
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 15f;
                tmp.fontSizeMax = 26f;
                tmp.overflowMode = TextOverflowModes.Truncate;
                EditorUtility.SetDirty(tmp);
                n++;
            }
            // The stat LABELS overlapped their own values by 5 units.
            else if (tmp.name == "MistakesLbl" || tmp.name == "TimeLbl")
            {
                var rt = tmp.rectTransform;
                if (rt.anchoredPosition.y < 90f)
                {
                    rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 92f);
                    EditorUtility.SetDirty(tmp);
                    n++;
                }
            }
        }
        return n;
    }

    // ---- 7. live contents tags ----------------------------------------------

    [MenuItem(MenuRoot + "Give Every Vessel a Live Contents Tag")]
    public static void FixVesselTags() { if (!EditorGuard()) return; int n = FixVesselTagsCore(); Save(); Debug.Log($"[W5.33] {n} vessel(s) given a live contents tag."); }

    /// Gives every liquid container its ml readout.
    /// Only the vessels a module's builder stages get a VesselStatus, so the rest
    /// of the lab has no way to report its contents. VesselStatus self-binds at
    /// Awake, so adding the pair (label + status) to every liquid container is
    /// enough.
    static int FixVesselTagsCore()
    {
        int added = 0;
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lp == null) continue;
            var go = lp.gameObject;
            var pl = go.GetComponent<ProximityLabel>();
            if (pl == null)
            {
                pl = go.AddComponent<ProximityLabel>();
                pl.SetLabel(Mishandling.DisplayNameFor(go), 1.4f);
                added++;
            }
            if (go.GetComponent<VesselStatus>() == null)
            {
                var item = go.GetComponent<LabItem>();
                string name = item != null && !string.IsNullOrEmpty(item.displayName)
                    ? item.displayName : Mishandling.DisplayNameFor(go);
                go.AddComponent<VesselStatus>().Bind(lp, pl, name, 1.4f);
                added++;
            }
            EditorUtility.SetDirty(go);
        }
        return added;
    }

    // ---- helpers -------------------------------------------------------------

    static bool EditorGuard()
    {
        if (Application.isPlaying) { Debug.LogWarning("[W5.33] exit Play mode first."); return false; }
        return true;
    }

    static void Save()
    {
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
    }

    static Material LoadMat(string guid)
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<Material>(path);
    }

    static ChemicalData FindChemical(string chemicalName)
    {
        foreach (var guid in AssetDatabase.FindAssets("t:ChemicalData"))
        {
            var c = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c != null && c.chemicalName == chemicalName) return c;
        }
        return null;
    }
}
#endif
