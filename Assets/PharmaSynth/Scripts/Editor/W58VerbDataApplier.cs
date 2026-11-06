#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One-shot, idempotent data pass for the tool verbs: re-points the
/// layouts whose tasks are TOOL verbs (weigh/stir/grind) and wires the
/// scene-side pieces (Methane's hand-built mortar, the matches-box striker).
///   • Acetone: `weigh-acetates` zone-touch → Weigh (scoopula on the pan).
///   • Benzamide: `stand` ("Stir & stand") zone-touch → Stir (glass rod).
///   • Scene: Methane's Eq_Motar/Eq_Pestle get a GrindController completing
///     `prepare-mixture` (dual-path with the legacy zone-touch), and the
///     matches dispenser box becomes a MatchStrikerSurface.
public static class W58VerbDataApplier
{
    const string Dir = "Assets/PharmaSynth/ScriptableObjects/Layouts/";

    [MenuItem("Tools/PharmaSynth/Apply W5.8 Verb Data")]
    public static void Apply()
    {
        if (Application.isPlaying) { Debug.LogWarning("[W58VerbData] exit Play mode first."); return; }
        int changes = 0;

        // ---- Acetone: weigh-acetates zone-touch → Weigh -----------------------
        var acetone = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(Dir + "Layout_Acetone.asset");
        if (acetone != null)
        {
            foreach (var s in acetone.stations)
                if (s.taskId == "weigh-acetates" && s.sim != StationSim.Weigh)
                { s.sim = StationSim.Weigh; changes++; }
            EditorUtility.SetDirty(acetone);
        }

        // ---- Benzamide: stand ("Stir & stand") → Stir -------------------------
        var benzamide = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(Dir + "Layout_Benzamide.asset");
        if (benzamide != null)
        {
            foreach (var s in benzamide.stations)
                if (s.taskId == "stand" && s.sim != StationSim.Stir)
                { s.sim = StationSim.Stir; changes++; }
            EditorUtility.SetDirty(benzamide);
        }

        AssetDatabase.SaveAssets();

        // ---- Scene wiring: Methane mortar grind + matches striker -------------
        int sceneChanges = 0;
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        GameObject mortarGo = null, pestleGo = null, matchesBox = null;
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li.itemId == "Eq_Motar" || li.name.Contains("Motar")) mortarGo = li.gameObject;
            if (li.itemId == "Eq_Pestle" || li.name.Contains("Pestle")) pestleGo = li.gameObject;
        }
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name.StartsWith("Raw_Matchsticks")) matchesBox = t.gameObject;

        if (mortarGo != null && pestleGo != null)
        {
            var grind = mortarGo.GetComponent<GrindController>();
            if (grind == null)
            {
                grind = mortarGo.AddComponent<GrindController>();
                sceneChanges++;
            }
            grind.Bind(runner, "prepare-mixture", pestleGo.transform);   // dual-path with the zone-touch
        }
        else Debug.LogWarning("[W58VerbData] scene mortar/pestle not found — Methane grind not wired.");

        if (matchesBox != null && matchesBox.GetComponent<MatchStrikerSurface>() == null)
        {
            matchesBox.AddComponent<MatchStrikerSurface>();
            sceneChanges++;
        }

        // Dropper verb: DropperController does nothing unless ATTACHED, and no
        // scene object carries one by default, so grabbing Eq_Dropper would do nothing.
        // Every Eq_Dropper* gets the verb: grip to hold, touch a bottle to draw,
        // TRIGGER to release exactly one counted drop.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Eq_Dropper")) continue;
            if (t.GetComponent<DropperController>() == null)
            {
                var d = t.gameObject.AddComponent<DropperController>();
                d.Bind(t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>());
                sceneChanges++;
            }
        }

        // Funnels are liquid PASS-THROUGH: without the marker the
        // pour ray landed ON the funnel's collider and wasted the stream as a
        // puddle there — a filter pour never reached the beaker underneath.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!t.name.StartsWith("Eq_Funnel") && !t.name.StartsWith("Funnel_")) continue;
            if (t.GetComponent<LiquidPassthrough>() == null)
            {
                t.gameObject.AddComponent<LiquidPassthrough>();
                sceneChanges++;
            }
        }

        // TIME-SKIP is a scene singleton: one controller listens for
        // any longProcess task (a week's fermentation…) and fades black → "time
        // passed". Lives on the runner's object; self-subscribes in Awake.
        if (runner != null && runner.GetComponent<TimeSkipController>() == null)
        {
            runner.gameObject.AddComponent<TimeSkipController>().Bind(runner);
            sceneChanges++;
        }

        // The WATER BATH is a zone-free tool: fill it with distilled
        // water, put a lit burner beside it, and it warms any vessel brought
        // close — anywhere in the lab. Needs a liquid receiver (the water the
        // player pours), a heat model, its live label, and the controller.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "WaterBath") continue;
            var host = t.gameObject;
            // LiquidPhysics requires a Renderer host — use the bath root only if
            // it renders, else the first mesh child (colliders find it via parent).
            if (host.GetComponent<Renderer>() == null)
                foreach (var r in t.GetComponentsInChildren<MeshRenderer>(true))
                { host = r.gameObject; break; }
            var blp = host.GetComponent<LiquidPhysics>();
            if (blp == null)
            {
                blp = host.AddComponent<LiquidPhysics>();
                blp.maxVolume = 400f;
                blp.SetContents(null, 0f);   // starts EMPTY — the player fills it
                sceneChanges++;
            }
            var bts = t.GetComponent<TemperatureSim>();
            if (bts == null)
            {
                bts = t.gameObject.AddComponent<TemperatureSim>();
                var so = new SerializedObject(bts);
                so.FindProperty("ambientC").floatValue = 25f;
                so.FindProperty("targetC").floatValue = 95f;
                so.FindProperty("overheatC").floatValue = 999f;   // a WATER bath cannot ruin a batch
                so.ApplyModifiedPropertiesWithoutUndo();
                sceneChanges++;
            }
            var blabel = t.GetComponent<ProximityLabel>() ?? t.gameObject.AddComponent<ProximityLabel>();
            var wbc = t.GetComponent<WaterBathController>();
            if (wbc == null) { wbc = t.gameObject.AddComponent<WaterBathController>(); sceneChanges++; }
            wbc.Bind(blp, bts, blabel);
            blabel.SetLabel(WaterBathMath.StatusLine(false, false, 25f), 1.6f);
            // Scalable effect zones: scale the wire sphere in
            // the editor and that IS the reach; the controller reads it.
            sceneChanges += EnsureZoneAnchor(t, "HeatZone", WaterBathMath.VesselRadius, new Color(1f, 0.55f, 0.15f, 0.9f));
            sceneChanges += EnsureZoneAnchor(t, "BurnerZone", WaterBathMath.BurnerRadius, new Color(1f, 0.3f, 0.15f, 0.9f));
        }

        // The ICE BATH is the water bath's cold twin (Exp 4's
        // crystallisation): the hand-placed Raw_IceBucket chills any vessel
        // brought to it — zone-free, needs nothing lit or poured.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Raw_IceBucket") continue;
            var ilabel = t.GetComponent<ProximityLabel>() ?? t.gameObject.AddComponent<ProximityLabel>();
            var ice = t.GetComponent<IceBathController>();
            if (ice == null) { ice = t.gameObject.AddComponent<IceBathController>(); sceneChanges++; }
            ice.Bind(ilabel);
            ilabel.SetLabel(IceBathMath.StatusLine(), 1.4f);
            sceneChanges += EnsureZoneAnchor(t, "ChillZone", IceBathMath.VesselRadius, new Color(0.45f, 0.75f, 1f, 0.9f));
        }

        // The FUME HOOD narrates itself: a label on the WorkVolume says what
        // belongs inside and flips to "<vessel> protected ✓" while one actually is.
        {
            var hood = Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
            if (hood != null)
            {
                var hlabel = hood.GetComponent<ProximityLabel>() ?? hood.gameObject.AddComponent<ProximityLabel>();
                if (hood.GetComponent<FumeHoodStatusLabel>() == null)
                { hood.gameObject.AddComponent<FumeHoodStatusLabel>().Bind(hood, hlabel); sceneChanges++; }
                hlabel.SetLabel(FumeHoodStatusLabel.StatusLine(null), 2.2f);

                // PHYSICAL SHELL: the Tripo hood model has ZERO colliders, so without
                // this every wall is passable and a released item can vanish behind
                // the panels and never come back. Five thin STATIC boxes hug the
                // WorkVolume (back, left, right, top, and a COUNTER to set the vessel
                // on) with the FRONT left open: that IS the door (reach in, work, take
                // it back; nothing can ever be trapped). The open side faces
                // the bench (where the Raw_ bottles live); hand-adjust the
                // HoodShell_* children if the model's walls sit differently.
                var wvCol = hood.GetComponent<BoxCollider>();
                if (wvCol != null && hood.transform.Find("HoodShell") == null)
                {
                    var shell = new GameObject("HoodShell").transform;
                    shell.SetParent(hood.transform, false);
                    Vector3 sz = wvCol.size, cc = wvCol.center;
                    const float th = 0.03f, pad = 0.02f;
                    Vector3 benchCenter = Vector3.zero; int nb = 0;
                    foreach (var tr in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (tr.name.StartsWith("Raw_")) { benchCenter += tr.position; nb++; }
                    if (nb > 0) benchCenter /= nb;
                    float frontSign = Vector3.Dot(hood.transform.TransformDirection(Vector3.forward),
                                                  (benchCenter - hood.transform.position).normalized) >= 0f ? 1f : -1f;
                    void Panel(string pname, Vector3 center, Vector3 size)
                    {
                        var pgo = new GameObject(pname);
                        pgo.transform.SetParent(shell, false);
                        pgo.transform.localPosition = center;
                        var bc = pgo.AddComponent<BoxCollider>();
                        bc.size = size;   // SOLID (not a trigger): a wall
                    }
                    Panel("HoodShell_Back",    cc + new Vector3(0f, 0f, -frontSign * (sz.z * 0.5f + pad)), new Vector3(sz.x + 2f * pad, sz.y + 2f * pad, th));
                    Panel("HoodShell_Left",    cc + new Vector3(-(sz.x * 0.5f + pad), 0f, 0f), new Vector3(th, sz.y + 2f * pad, sz.z + 2f * pad));
                    Panel("HoodShell_Right",   cc + new Vector3( (sz.x * 0.5f + pad), 0f, 0f), new Vector3(th, sz.y + 2f * pad, sz.z + 2f * pad));
                    Panel("HoodShell_Top",     cc + new Vector3(0f,  (sz.y * 0.5f + pad), 0f), new Vector3(sz.x + 2f * pad, th, sz.z + 2f * pad));
                    Panel("HoodShell_Counter", cc + new Vector3(0f, -(sz.y * 0.5f + pad), 0f), new Vector3(sz.x + 2f * pad, th, sz.z + 2f * pad));
                    sceneChanges++;
                }
            }
        }

        // Exp 3's CO₂ delivery reach gets the same scalable zone anchor
        // (flask → limewater tube; FermentationController reads it).
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "FlorenceFlask") continue;
            sceneChanges += EnsureZoneAnchor(t, "FermentZone", FermentationMath.DeliveryRadius, new Color(0.7f, 1f, 0.5f, 0.9f));
        }

        // NAKED FLAME (Exp 6 dry distillation): every burner heats
        // vessels held over its flame — the ONE heat source beyond the water
        // bath's 100 °C cap. That is what the hard-glass tubes exist for.
        foreach (var bc in Object.FindObjectsByType<BurnerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (bc.GetComponent<NakedFlameHeat>() == null)
            { bc.gameObject.AddComponent<NakedFlameHeat>().Bind(bc); sceneChanges++; }
        }

        // THE BENCH BALANCE becomes the live weigh tool (Exp 6):
        // grams display + a pan trigger whose occupancy VesselWeighTask reads.
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name != "Eq_Balance") continue;
            Bounds BalBounds()
            {
                var rr = t.GetComponentsInChildren<Renderer>();
                if (rr.Length == 0) return new Bounds(t.position, Vector3.one * 0.1f);
                var bb = rr[0].bounds;
                for (int i = 1; i < rr.Length; i++) bb.Encapsulate(rr[i].bounds);
                return bb;
            }
            var scale = t.GetComponent<WeighingScaleController>();
            if (scale == null) { scale = t.gameObject.AddComponent<WeighingScaleController>(); sceneChanges++; }
            if (t.Find("MassDisplay") == null)
            {
                var b = BalBounds();
                var dgo = new GameObject("MassDisplay");
                dgo.transform.SetParent(t, false);
                dgo.transform.position = new Vector3(b.center.x, b.max.y + 0.1f, b.center.z);
                var tmp = dgo.AddComponent<TMPro.TextMeshPro>();
                tmp.text = "0 g"; tmp.fontSize = 0.6f;
                tmp.alignment = TMPro.TextAlignmentOptions.Center;
                var mr = dgo.GetComponent<MeshRenderer>(); if (mr != null) mr.sortingOrder = 20000;
                var rt2 = dgo.GetComponent<RectTransform>(); if (rt2 != null) rt2.sizeDelta = new Vector2(1.2f, 0.3f);
                scale.Bind(tmp);
                sceneChanges++;
            }
            if (t.Find("Pan") == null)
            {
                var b = BalBounds();
                var pgo = new GameObject("Pan");
                pgo.transform.SetParent(t, false);
                pgo.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
                var pcol = pgo.AddComponent<BoxCollider>();
                pcol.isTrigger = true;
                var pls = pgo.transform.lossyScale;
                pcol.size = new Vector3(
                    Mathf.Max(0.14f, b.size.x) / Mathf.Max(1e-4f, Mathf.Abs(pls.x)),
                    0.12f / Mathf.Max(1e-4f, Mathf.Abs(pls.y)),
                    Mathf.Max(0.14f, b.size.z) / Mathf.Max(1e-4f, Mathf.Abs(pls.z)));
                pgo.AddComponent<WeighStation>().Bind(null, "", null, null, 0f, scale);
                sceneChanges++;
            }
        }

        // The porcelain spatula is the FINE solids tool: Exp 2 weighs 0.1 g salicylic
        // and 0.5 g aspirin, both smaller than the scoopula's 2 g dip. Its charge is
        // ScoopMath.GramsPerSpatula; the scoopula keeps the coarse 2 g.
        var spatula = GameObject.Find("Eq_PorcelainSpatula");
        var spatScoop = spatula != null ? spatula.GetComponent<ScoopController>() : null;
        if (spatScoop != null)
        {
            spatScoop.SetGramsPerDip(ScoopMath.GramsPerSpatula);
            // The porcelain spatula is a thin flat blade → its 0.1 g smear is a
            // flat, slightly-elongated wafer, NOT the scoopula's rounded bowl-pile.
            spatScoop.SetHeapScale(new Vector3(0.012f, 0.004f, 0.022f));
            EditorUtility.SetDirty(spatScoop);
            sceneChanges++;
        }
        else Debug.LogWarning("[W58VerbData] Eq_PorcelainSpatula/ScoopController not found — Exp 2's 0.1 g dips not wired.");

        if (sceneChanges > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log($"[W58VerbData] {changes} layout changes, {sceneChanges} scene changes applied (idempotent).");
    }

    /// A hand-scalable EFFECT-ZONE anchor child: a PlacementAnchor whose wire-sphere
    /// gizmo diameter == its world scale; the tool controller reads half that
    /// scale as its reach. Centred on the object's rendered body, not its pivot.
    static int EnsureZoneAnchor(Transform parent, string name, float radius, Color colour)
    {
        if (parent.Find(name) != null) return 0;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rends = parent.GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            go.transform.position = b.center;
        }
        var ls = parent.lossyScale;   // compensate a scaled parent so world scale == diameter
        go.transform.localScale = new Vector3(
            radius * 2f / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            radius * 2f / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),
            radius * 2f / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        var pa = go.AddComponent<PlacementAnchor>();
        pa.previewsScale = true;
        pa.gizmoColor = colour;
        return 1;
    }
}
#endif
