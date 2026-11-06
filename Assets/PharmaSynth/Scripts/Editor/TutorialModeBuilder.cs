#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// Tutorial Mode scene wiring. Mirrors DemoModeBuilder's shape so the
/// two special modes are built the same way:
///   • Build Tutorial Menu Button — MainMenu scene: clones the Laboratory button
///     into a "Tutorial" button wired to MainMenuController.OnTutorialLaboratory.
///     Unlike the Demo button this one is ALWAYS visible — practice mode is a
///     shipped feature, not a config-gated demo affordance.
/// Idempotent: re-running re-labels and re-wires the existing button in place.
public static class TutorialModeBuilder
{
    // Cyan-green, deliberately NOT the demo amber — a player must never confuse
    // "unlocked practice" with "unlocked demo build".
    static readonly Color TutorialAccent = new Color(0.45f, 0.95f, 0.75f);

    [MenuItem("Tools/PharmaSynth/Build Tutorial Menu Button")]
    public static void BuildMenuButton()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialModeBuilder] exit Play mode first."); return; }
        var lab = FindInScene("LaboratoryButton");
        var controller = Object.FindAnyObjectByType<MainMenuController>();
        if (lab == null || controller == null)
        {
            Debug.LogError("[TutorialModeBuilder] LaboratoryButton/MainMenuController not found — open MainMenu.unity first.");
            return;
        }

        var parent = lab.transform.parent;
        var existing = parent.Find("TutorialModeButton");
        GameObject tut = existing != null ? existing.gameObject : Object.Instantiate(lab, parent);
        tut.name = "TutorialModeButton";

        // Tutorial takes the last SHIPPED slot and pushes the config-gated Demo
        // button below it. DemoModeBuilder places Demo at "lowest + one row", so
        // appending after it would strand Tutorial with a visible gap above it in
        // any build where DemoButtonVisibility keeps Demo hidden — which is every
        // shipped build. Position is recomputed on each run, so the two builders
        // can be re-run in either order without drifting.
        var labRt = lab.GetComponent<RectTransform>();
        float rowH = labRt.sizeDelta.y > 1f ? labRt.sizeDelta.y : 60f;
        float gap = rowH + 14f;

        var demoT = parent.Find("DemoModeButton");
        float minY = labRt.anchoredPosition.y;
        foreach (var b in parent.GetComponentsInChildren<Button>(true))
        {
            var rt = b.GetComponent<RectTransform>();
            if (rt == null || rt.parent != parent) continue;
            if (rt.gameObject == tut) continue;                             // don't chase ourselves
            if (demoT != null && rt.gameObject == demoT.gameObject) continue; // demo sits BELOW us
            if (rt.anchoredPosition.y < minY) minY = rt.anchoredPosition.y;
        }

        var tutRt = tut.GetComponent<RectTransform>();
        tutRt.anchoredPosition = new Vector2(labRt.anchoredPosition.x, minY - gap);
        if (demoT != null)
        {
            var demoRt = demoT.GetComponent<RectTransform>();
            if (demoRt != null)
                demoRt.anchoredPosition = new Vector2(labRt.anchoredPosition.x, tutRt.anchoredPosition.y - gap);
        }

        var label = tut.GetComponentInChildren<TMP_Text>(true);
        if (label != null) { label.text = "Tutorial"; label.color = TutorialAccent; }

        var btn = tut.GetComponent<Button>();
        if (btn != null)
        {
            while (btn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(btn.onClick, 0);
            UnityEventTools.AddVoidPersistentListener(btn.onClick, controller.OnTutorialLaboratory);
        }

        // A cloned Laboratory button may inherit DemoButtonVisibility's hidden state
        // if the panel ever gated it; practice mode is always offered.
        tut.SetActive(true);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TutorialModeBuilder] Tutorial menu button built and wired to OnTutorialLaboratory.");
    }

    // ---- MainMenu: keep the button column above the floor --------------------

    /// Reflows the cube-room menu column and lifts the canvas so nothing is buried.
    ///
    /// Two traps sink the menu:
    ///   1. `ResultsButton` is INACTIVE but would still occupy a row, leaving a visible
    ///      hole in the column and pushing everything below it a slot further down.
    ///   2. Appending new buttons BELOW the lowest existing one grows the column
    ///      downward until its tail crosses the floor plane.
    /// So the KNOWN buttons are laid out in an explicit order, skipping any that
    /// are absent or inactive, then the canvas height is set ABSOLUTELY so the lowest
    /// visible button clears the floor. Absolute, not an offset: an offset would sink
    /// or launch the menu a little further on every re-run.
    ///
    /// Order is also the UX order: the two ways to play first, then settings/quit,
    /// with the config-gated demo button last.
    static readonly string[] MenuOrder =
        { "LaboratoryButton", "TutorialModeButton", "SettingsButton", "QuitButton", "DemoModeButton" };

    // 5 rows x 170 units = 1.83 m, which cannot both sit clear of the floor AND stay in
    // reach: lifting it trades a buried bottom for an unreachable top. Smaller rows
    // satisfy both (5 rows x 128 = 1.37 m), and 0.24 m-tall buttons are still a generous
    // VR target.
    const float MenuRowHeight = 110f;
    const float MenuRowSpacing = 128f;
    const float MenuFloorClearance = 0.5f;  // metres from the floor to the lowest edge
    const float MenuMaxTopHeight = 1.95f;   // metres — above this the player has to crane

    [MenuItem("Tools/PharmaSynth/Fix Cube Room Menu Layout")]
    public static void FixMenuLayout()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialModeBuilder] exit Play mode first."); return; }
        var lab = FindInScene("LaboratoryButton");
        if (lab == null)
        {
            Debug.LogError("[TutorialModeBuilder] LaboratoryButton not found — open MainMenu.unity first.");
            return;
        }

        var parent = lab.transform.parent;
        float topY = lab.GetComponent<RectTransform>().anchoredPosition.y;
        float x = lab.GetComponent<RectTransform>().anchoredPosition.x;

        int row = 0;
        float lowestBottom = topY, highestTop = topY;
        foreach (var name in MenuOrder)
        {
            var t = parent.Find(name);
            // Inactive buttons are SKIPPED, not spaced around, so the column has no gap.
            if (t == null || !t.gameObject.activeSelf) continue;
            var rt = t.GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, MenuRowHeight);
            float y = topY - row * MenuRowSpacing;
            rt.anchoredPosition = new Vector2(x, y);
            if (row == 0) highestTop = y + rt.sizeDelta.y * 0.5f;
            lowestBottom = y - rt.sizeDelta.y * 0.5f;
            EditorUtility.SetDirty(rt);
            row++;
        }

        var canvas = parent.GetComponentInParent<Canvas>();
        var canvasT = canvas != null ? canvas.transform : parent;
        float unitsToMetres = canvasT.lossyScale.y;

        // TWO constraints, not one. Lifting only until the bottom clears the floor puts
        // the top at ~2.18 m with every button shown, which the player has to crane at.
        // Take the LOWER placement that still clears the floor.
        float liftForFloor = MenuFloorClearance - lowestBottom * unitsToMetres;
        float liftForReach = MenuMaxTopHeight - highestTop * unitsToMetres;
        float wanted = Mathf.Max(Mathf.Min(liftForFloor, liftForReach), liftForFloor);

        // When the column is too tall to satisfy both, the floor wins (a buried button
        // is unusable; a high one is merely awkward) — but say so, because the real fix
        // is fewer visible rows or tighter spacing, not more lifting.
        if (liftForReach < liftForFloor)
            Debug.LogWarning("[TutorialModeBuilder] " + row + " visible buttons is too tall to both clear the floor"
                + " and stay under " + MenuMaxTopHeight.ToString("0.00") + " m — top lands at "
                + (liftForFloor + highestTop * unitsToMetres).ToString("0.00")
                + " m. Hide the config-gated Demo button, or reduce MenuRowSpacing/button height.");

        var p = canvasT.position;
        canvasT.position = new Vector3(p.x, wanted, p.z);
        EditorUtility.SetDirty(canvasT);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TutorialModeBuilder] menu reflowed: " + row + " visible buttons, bottom "
                  + (wanted + lowestBottom * unitsToMetres).ToString("0.00") + " m / top "
                  + (wanted + highestTop * unitsToMetres).ToString("0.00") + " m above the floor.");
    }

    // ---- MainMenu: make the room feel alive ----------------------------------

    /// Adds atmosphere to the cube spawn room: breathing neon trim, drifting light
    /// levels with the occasional arc stutter, floor haze and slow rising motes.
    ///
    /// Deliberately built from PARTICLES + emissive pulsing rather than post-processing:
    /// this is the first thing a Quest 3 player sees, a full-screen effect stack costs
    /// frame budget on a mobile GPU for the whole session, and the room only needs to
    /// look alive, not cinematic. Idempotent — re-running replaces its own objects.
    [MenuItem("Tools/PharmaSynth/Build Cube Room FX")]
    public static void BuildRoomFx()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialModeBuilder] exit Play mode first."); return; }
        var canvasGo = FindInScene("MenuCanvas");
        if (canvasGo == null)
        {
            Debug.LogError("[TutorialModeBuilder] MenuCanvas not found — open MainMenu.unity first.");
            return;
        }

        // Collect the room's neon trim + lights. Trim is matched by name because the
        // cube room authors its strips as Trim_* / neon frames.
        var trim = new List<Renderer>();
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                string n = r.gameObject.name;
                if (n.StartsWith("Trim_") || n.Contains("Neon") || n.Contains("Frame"))
                    if (r.GetComponentInParent<Canvas>() == null) trim.Add(r);   // never the UI
            }
        var lights = new List<Light>();
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            lights.AddRange(root.GetComponentsInChildren<Light>(true));

        var host = GameObject.Find("~RoomFx");
        if (host != null) Object.DestroyImmediate(host);      // idempotent: rebuild clean
        host = new GameObject("~RoomFx");

        var fx = host.AddComponent<MenuRoomFx>();
        fx.Bind(trim.ToArray(), lights.ToArray());
        EditorUtility.SetDirty(fx);

        // Floor haze — wide, slow, barely-there. Sits BELOW the menu so it never fogs
        // the buttons the player has to read.
        MakeParticles(host.transform, "Haze", new Vector3(0f, 0.12f, 2.2f),
            rate: 6f, life: 9f, size: 1.6f, speed: 0.05f, radius: 3.2f,
            colour: new Color(0.35f, 0.85f, 1f, 0.05f), gravity: -0.005f);

        // Rising motes — the movement your eye actually catches.
        MakeParticles(host.transform, "Motes", new Vector3(0f, 0.05f, 2.2f),
            rate: 14f, life: 7f, size: 0.022f, speed: 0.16f, radius: 2.6f,
            colour: new Color(0.6f, 1f, 1f, 0.5f), gravity: -0.02f);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TutorialModeBuilder] room FX built: " + trim.Count + " trim strip(s) breathing, "
                  + lights.Count + " light(s) drifting, haze + motes added.");
    }

    static void MakeParticles(Transform parent, string name, Vector3 pos, float rate,
        float life, float size, float speed, float radius, Color colour, float gravity)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.startLifetime = life;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = colour;
        main.gravityModifier = gravity;              // negative = drifts upward
        main.maxParticles = 220;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        // ⛔ Every curve stays in Constant mode. Mixing curve modes on a particle
        // system is what produced 711 errors/second from SpawnVFX — the
        // system spams every frame and the console becomes unusable.
        var emission = ps.emission;
        emission.rateOverTime = rate;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 1f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(colour, 0f), new GradientColorKey(colour, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f),
                    new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var pr = go.GetComponent<ParticleSystemRenderer>();
        pr.renderMode = ParticleSystemRenderMode.Billboard;
        pr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
        pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        pr.receiveShadows = false;
        pr.sortingOrder = -1;                        // behind the menu, never over the text
    }

    // ---- SampleScene: the guidance rig ---------------------------------------

    /// Wires Tutorial Mode into the lab scene:
    ///   • TutorialHighlighter on the runner's object, bound to the runner
    ///   • WaypointGuide bound to the runner (the zone-free bench has no station
    ///     registry for it to read)
    ///   • the beacon's arrow + disc switched to a ZTest Always material so the
    ///     marker reads THROUGH a closed cabinet door — the "see it through things"
    ///     requirement, without touching any shared bench material.
    /// Idempotent.
    [MenuItem("Tools/PharmaSynth/Build Tutorial Scene Wiring")]
    public static void BuildSceneWiring()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialModeBuilder] exit Play mode first."); return; }
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        if (runner == null)
        {
            Debug.LogError("[TutorialModeBuilder] no ExperimentRunner — open SampleScene.unity first.");
            return;
        }

        var hl = runner.GetComponent<TutorialHighlighter>();
        if (hl == null) hl = runner.gameObject.AddComponent<TutorialHighlighter>();
        hl.Bind(runner);
        hl.SetGlowMaterials(
            EnsureGlowMaterial("TutorialGlowSource", new Color(1f, 0.66f, 0.12f, 1f)),    // amber: fetch this
            EnsureGlowMaterial("TutorialGlowTarget", new Color(0.30f, 1f, 0.45f, 1f)));   // green: put it here
        EditorUtility.SetDirty(hl);

        var coach = runner.GetComponent<TutorialCoach>();
        if (coach == null) coach = runner.gameObject.AddComponent<TutorialCoach>();
        coach.Bind(runner);
        EditorUtility.SetDirty(coach);

        // Verb demonstration. Its own material, NOT TutorialXray: that one is
        // ZTest Greater and would draw the miming ghost only where it happens to be
        // hidden — the exact inverse of what a demonstration needs.
        var demo = runner.GetComponent<VerbDemoPlayer>();
        if (demo == null) demo = runner.gameObject.AddComponent<VerbDemoPlayer>();
        demo.Bind(runner, EnsureDemoGhostMaterial());
        EditorUtility.SetDirty(demo);

        // Ground path. Its own material: a floor chevron must read on top of the
        // floor it lies on, so ZTest Always, and NEVER a shared bench material.
        // Name tags stop stacking on each other. Scene-level, one instance, and it
        // idles at 5 Hz over whatever labels happen to be visible — no per-label authoring.
        var declutter = runner.GetComponent<LabelDeclutter>();
        if (declutter == null) declutter = runner.gameObject.AddComponent<LabelDeclutter>();
        declutter.Bind(0.2f, LabelDeclutterMath.MaxLift);
        EditorUtility.SetDirty(declutter);

        var path = runner.GetComponent<GuidePath>();
        if (path == null) path = runner.gameObject.AddComponent<GuidePath>();
        path.Bind(runner, EnsureGuideMaterial("GuideChevron", new Color(0.35f, 0.95f, 1f, 0.75f),
                                              UnityEngine.Rendering.CompareFunction.Always, 4000));
        // ⚠ Values pushed explicitly: changing a [SerializeField] default does NOTHING to a
        // scene that already saved it.
        // 1 = the authored chevron size; the mesh carries real metres.
        path.SetTuning(GuidePathMath.Spacing, GuidePathMath.FlowSpeed, 1f);
        EditorUtility.SetDirty(path);

        int beacons = 0;
        var mat = EnsureThroughWallMaterial();
        foreach (var guide in Object.FindObjectsByType<WaypointGuide>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            guide.SetRunner(runner);
            guide.SetMarkerScale(0.16f);                 // METRES tall at the reference distance
            // Clear the TOP; cage → pull in front; then hold a constant APPARENT size.
            // minMul 0.35 (not 0.55) so the marker doesn't fill the view at arm's
            // length: the near end needs room to shrink further.
            guide.SetPlacement(0.16f, 0.4f, 0.3f, refDistance: 2f, minMul: 0.35f, maxMul: 3.5f);
            EditorUtility.SetDirty(guide);
            foreach (var r in guide.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterial = mat;      // sharedMaterial: .material instances in edit mode
                EditorUtility.SetDirty(r);
            }
            beacons++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[TutorialModeBuilder] highlighter + coach + verb demo + ground path bound; " + beacons
                  + " waypoint guide(s) revived with a through-wall beacon.");
    }

    /// Unlit + ZTest **Greater** + ZWrite Off: draws the ghost ONLY where the object is
    /// occluded, so an unobstructed bottle looks completely normal and a hidden one
    /// shows through the cabinet door. Semi-transparent so it reads as a hint, not a
    /// solid object sitting in the wall.
    /// The pulsing guidance shell. Two passes live inside PharmaSynth/GuideGlow — an
    /// additive fresnel rim where the object is visible, a flat ghost where it is
    /// hidden — so one material covers both "make it glow" and "let me see it through
    /// the cabinet". The pulse is driven by _Time in the shader, not from C#.
    static Material EnsureGlowMaterial(string name, Color colour)
    {
        string path = "Assets/PharmaSynth/Art/Materials/" + name + ".mat";
        var shader = Shader.Find("PharmaSynth/GuideGlow");
        if (shader == null)
        {
            Debug.LogError("[TutorialModeBuilder] PharmaSynth/GuideGlow not found — did PharmaGlow.shader import?");
            return null;
        }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/PharmaSynth/Art/Materials");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        // Slow, large, bright and wide-haloed, so it reads as a deliberate marker;
        // a fast, tight, dim shimmer reads as a specular highlight.
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_Intensity", 4.5f);      // brightness
        mat.SetFloat("_RimPower", 1.1f);       // LOWER = wider halo
        mat.SetFloat("_PulseSpeed", 1.3f);     // slow, ~5 s a breath
        mat.SetFloat("_PulseMin", 0.15f);      // deeper swing = bigger pulse
        mat.SetFloat("_Occluded", 0.5f);       // visibility through a door
        mat.SetFloat("_Swell", 0.012f);        // 12 mm breath, so the pulse changes SIZE
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// Both guidance materials come from PharmaSynth/GuideOverlay, NOT URP's stock
    /// Unlit — that shader does not declare a `_ZTest` property, so SetInt("_ZTest",…)
    /// against it is a silent no-op and the overlay renders with ordinary depth
    /// testing (i.e. shows through nothing). The material looks correctly configured
    /// in the inspector either way, which is what makes it worth a comment.
    ///
    /// Rewrites an existing asset in place rather than returning early, so a material
    /// left over from the stock-Unlit version is repaired instead of kept.
    static Material EnsureGuideMaterial(string name, Color colour,
        UnityEngine.Rendering.CompareFunction zTest, int queue)
    {
        string path = "Assets/PharmaSynth/Art/Materials/" + name + ".mat";
        var shader = Shader.Find("PharmaSynth/GuideOverlay");
        if (shader == null)
        {
            Debug.LogError("[TutorialModeBuilder] PharmaSynth/GuideOverlay shader not found — did PharmaGuide.shader import?");
            return null;
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            System.IO.Directory.CreateDirectory("Assets/PharmaSynth/Art/Materials");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.SetColor("_BaseColor", colour);
        mat.SetFloat("_ZTest", (float)zTest);
        mat.renderQueue = queue;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }

    /// Unlit + ZTest Always + ZWrite Off: the beacon draws on top of whatever is in
    /// front of it. Its own asset, never a shared bench material — flipping ZTest on
    /// one of those would make random glassware render through walls too.
    static Material EnsureThroughWallMaterial()
        => EnsureGuideMaterial("TutorialBeacon", new Color(1f, 0.72f, 0.20f, 1f),
                               UnityEngine.Rendering.CompareFunction.Always, 4000);

    /// The miming ghost: ZTest Always so the demonstration is never hidden by the very
    /// bench it is happening over. A cool near-white, deliberately NEITHER of the two
    /// guidance colours — amber already means "fetch this" and green "put it here", and
    /// a third meaning ("watch this") needs a third colour or it reads as a target that
    /// has come loose and started drifting.
    static Material EnsureDemoGhostMaterial()
        => EnsureGuideMaterial("TutorialDemoGhost", new Color(0.62f, 0.90f, 1f, 0.45f),
                               UnityEngine.Rendering.CompareFunction.Always, 4000);

    // ---- end-to-end guidance simulation --------------------------------------

    /// Walks every module's REAL task graph step by step with Tutorial Mode on, and
    /// checks the guidance actually keeps up: at every point along the progression the
    /// currently-available step must still resolve to a live object, and the clock must
    /// never tick.
    ///
    /// This is the dynamic counterpart to Audit Tutorial Targets. The audit asks "does
    /// every task have a target?" once, in aggregate; this asks "as steps complete, does
    /// the target set MOVE with them?" — a stale or empty set mid-run would leave the
    /// player staring at a lab with nothing lit, which the aggregate check cannot see.
    ///
    /// Mutates the open scene (it builds stages and starts runs) — reopen SampleScene
    /// afterwards. Deliberately a menu item, not a suite pin, for that reason.
    [MenuItem("Tools/PharmaSynth/Simulate Tutorial Guidance")]
    public static void SimulateGuidance()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialSim] exit Play mode first."); return; }
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        if (builder == null || runner == null || lib == null)
        {
            Debug.LogError("[TutorialSim] need ExperimentSceneBuilder + ExperimentRunner + ExperimentLibrary — open SampleScene.unity first.");
            return;
        }

        // The sim starts real runs; restore vessel contents afterwards or an edit-mode
        // pass permanently corrupts the saved scene's supplies.
        var snapshot = new List<(LiquidPhysics lp, ChemicalData chem, float ml, ChemicalData ppt, float pptMl)>();
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            snapshot.Add((lp, lp.currentChemical, lp.currentLiquidVolume, lp.currentPptChemical, lp.currentPptVolume));

        bool wasTutorial = TutorialSession.Active;
        TutorialSession.Active = true;
        var report = new System.Text.StringBuilder("[TutorialSim] step-by-step guidance walk\n");
        int totalBlind = 0, modules = 0;

        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition",
                         new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.graphTasks == null || def.graphTasks.Count == 0) continue;
                var module = lib.Get(def.moduleId);
                if (module == null) continue;

                modules++;
                builder.Build(def.moduleId);
                runner.SetModule(module);
                runner.StartExperiment();
                TutorialTargets.Build();

                var blind = new List<string>();
                int steps = 0, guided = 0, demoable = 0;
                while (steps++ < 300)
                {
                    ExperimentTask next = null;
                    foreach (var t in runner.Graph.AvailableTasks()) { next = t; break; }
                    if (next == null) break;

                    // Would anything actually light for this step, right now?
                    var targets = TaskTargetRegistry.Targets(next.taskId);
                    bool lit = false;
                    for (int i = 0; i < targets.Count && !lit; i++)
                        if (targets[i].transform != null
                            && TutorialHighlighter.ShouldLight(targets[i], false, true)) lit = true;

                    if (lit) guided++;
                    else if (!next.autoCompleteWhenOthersDone) blind.Add(next.taskId);

                    // can the step also be DEMONSTRATED? Endpoints resolving is the
                    // whole precondition for the miming ghost, and a step that lights but
                    // cannot be mimed silently downgrades the coach's level-2 rung to a
                    // point with nothing to look at.
                    if (VerbDemoPlayer.Endpoints(targets, out _, out _, out _)) demoable++;

                    runner.CompleteTask(next.taskId);
                }

                // The clock must not have moved: practice runs are untimed.
                bool untimed = runner.ElapsedSeconds <= 0.001f;
                totalBlind += blind.Count;
                report.Append(blind.Count == 0 && untimed ? "  OK   " : "  BAD  ")
                      .Append(def.moduleId)
                      .Append("  guided ").Append(guided).Append('/').Append(guided + blind.Count)
                      .Append(", demoable ").Append(demoable)
                      .Append(untimed ? ", untimed" : ", CLOCK RAN (" + runner.ElapsedSeconds.ToString("0.0") + "s)");
                if (blind.Count > 0) report.Append(", blind steps: ").Append(string.Join(", ", blind));
                report.Append('\n');

                runner.Abort();
            }
        }
        finally
        {
            TutorialSession.Active = wasTutorial;
            foreach (var s in snapshot)
            {
                if (s.lp == null) continue;
                s.lp.SetContents(s.chem, s.ml);
                s.lp.currentPptChemical = s.ppt;
                s.lp.currentPptVolume = s.pptMl;
            }
            TaskTargetRegistry.Clear();
        }

        report.Append(totalBlind == 0
            ? "  -> guidance kept up at every step of every module."
            : "  -> " + totalBlind + " step(s) would leave the player with nothing lit.");
        if (totalBlind == 0) Debug.Log(report.ToString()); else Debug.LogWarning(report.ToString());
        System.IO.File.WriteAllText("Logs/tutorial-guidance-sim.txt",
            report.ToString() + "\n(" + modules + " modules walked)\n");
    }

    // ---- coverage audit ------------------------------------------------------

    /// Builds every module's stage IN EDIT MODE and reports which steps resolve to no
    /// scene object — i.e. where Tutorial Mode would tell the player to act with
    /// nothing to point at.
    ///
    /// NOT a self-test pin, deliberately: this has to Build() each stage, which mutates
    /// the open scene, and the suite must stay side-effect-free. Run it by hand after
    /// changing a module's tasks, layout, or verb wiring. Like Reveal Stage, it leaves
    /// the LAST module's stage standing — rebuild or reopen the scene afterwards.
    [MenuItem("Tools/PharmaSynth/Audit Tutorial Targets")]
    public static void AuditTargets()
    {
        if (Application.isPlaying) { Debug.LogWarning("[TutorialAudit] exit Play mode first."); return; }
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        if (builder == null)
        {
            Debug.LogError("[TutorialAudit] no ExperimentSceneBuilder in the open scene — open SampleScene.unity first.");
            return;
        }

        var report = new System.Text.StringBuilder("[TutorialAudit] per-module step coverage\n");
        int totalGaps = 0, modules = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.graphTasks == null || m.graphTasks.Count == 0) continue;

            modules++;
            builder.Build(m.moduleId);
            TutorialTargets.Build();
            TutorialTargets.AuditAgainst(m.graphTasks);

            int gaps = TutorialTargets.LastUnresolved.Count;
            totalGaps += gaps;
            report.Append(gaps == 0 ? "  OK   " : "  GAP  ")
                  .Append(m.moduleId)
                  .Append("  (").Append(m.graphTasks.Count).Append(" tasks");
            if (gaps > 0) report.Append(", unresolved: ").Append(string.Join(", ", TutorialTargets.LastUnresolved));
            report.Append(")\n");
        }

        report.Append(totalGaps == 0
            ? "  -> every step in every module resolves to at least one object."
            : "  -> " + totalGaps + " step(s) have NO object to point at; Tutorial Mode will guide the player nowhere.");
        if (totalGaps == 0) Debug.Log(report.ToString());
        else Debug.LogWarning(report.ToString());

        System.IO.File.WriteAllText("Logs/tutorial-target-audit.txt",
            report.ToString() + "\n(" + modules + " modules audited)\n");
        TaskTargetRegistry.Clear();
    }

    // ---- helpers -------------------------------------------------------------

    static GameObject FindInScene(string name)
    {
        foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    static Transform FindDeep(Transform t, string name)
    {
        for (int i = 0; i < t.childCount; i++)
        {
            var c = t.GetChild(i);
            if (c.name == name) return c;
            var deep = FindDeep(c, name);
            if (deep != null) return deep;
        }
        return null;
    }
}
#endif
