#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Physics-attributes / resting-pose audit.
/// Tools ▸ PharmaSynth ▸ Physics Audit (Report)   — non-destructive scan of the
///   scene apparatus + SceneAssetLibrary prefabs: colliders present/degenerate,
///   Rigidbody settings, profile coverage. Writes Temp/physics-audit.md.
/// Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test) — drops every library prefab
///   onto a plane 50 m above the lab for 3 simulated seconds (script-mode
///   simulation, all other dynamic rigidbodies frozen for the sweep) and checks
///   it neither tunnels, rolls away, nor balances implausibly.
public static class PhysicsAudit
{
    // ---- report ------------------------------------------------------------

    [MenuItem("Tools/PharmaSynth/Physics Audit (Report)")]
    public static void Report()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PhysicsAudit] exit Play mode first."); return; }
        var sb = new StringBuilder("# Physics audit report\n\n");
        int issues = 0;

        sb.Append("## Library prefabs\n\n");
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var prefabs = so.FindProperty("prefabs");
            for (int i = 0; prefabs != null && i < prefabs.arraySize; i++)
            {
                var g = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (g == null) continue;
                issues += DescribeItem(sb, g, g.name, isPrefab: true);
            }
        }
        else sb.Append("- SceneAssetLibrary.asset not found\n");

        sb.Append("\n## Scene apparatus\n\n");
        foreach (var go in SceneItems())
            issues += DescribeItem(sb, go, PrefabNameFor(go), isPrefab: false);

        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText("Temp/physics-audit.md", sb.ToString());
        if (issues == 0) Debug.Log("<color=#4CD07D>[PhysicsAudit] report clean — Temp/physics-audit.md</color>");
        else Debug.LogWarning($"[PhysicsAudit] {issues} issue(s) — see Temp/physics-audit.md");
    }

    /// Append one line per problem; returns the number of problems found.
    static int DescribeItem(StringBuilder sb, GameObject g, string prefabName, bool isPrefab)
    {
        int n = 0;
        void Bad(string what) { sb.Append($"- **{g.name}**: {what}\n"); n++; }

        if (!PhysicsProfiles.TryGet(prefabName, out _)) Bad("no PhysicsProfile entry (name: " + prefabName + ")");
        var cols = g.GetComponentsInChildren<Collider>();
        if (cols.Length == 0) Bad("no collider anywhere");
        else if (!isPrefab)
        {
            bool anySolid = false;
            foreach (var c in cols) if (!PhysicsProfiles.IsDegenerate(c.bounds.size)) { anySolid = true; break; }
            if (!anySolid) Bad("only degenerate colliders (" + cols[0].bounds.size + ")");
        }

        var rb = g.GetComponent<Rigidbody>();
        if (!isPrefab && rb != null && !rb.isKinematic && rb.GetComponent<GrabPhysicsPolicy>() == null)
            Bad("dynamic rigidbody with no GrabPhysicsPolicy (will never sleep back to shelf policy)");
        if (rb != null && rb.mass <= 0.001f) Bad("mass ~0");
        return n;
    }

    // ---- fix pass ----------------------------------------------------------

    [MenuItem("Tools/PharmaSynth/Physics Audit (Fix Scene Items)")]
    public static void FixSceneItems()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PhysicsAudit] exit Play mode first."); return; }
        int fixedCount = 0;
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        foreach (var go in SceneItems())
        {
            string prefabName = PrefabNameFor(go);
            if (!PhysicsProfiles.TryGet(prefabName, out _)) continue;
            Undo.RegisterFullObjectHierarchyUndo(go, "Physics Audit Fix");
            WireSceneItem(go, prefabName, runner);
            fixedCount++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"[PhysicsAudit] applied profiles to {fixedCount} scene item(s) — save the scene to keep them.");
    }

    /// The full interaction treatment for ONE hand-built scene item:
    /// physics profile, grab policy, drop respawn, nature-correct breakability +
    /// impact audio, spill grading, scoop verb. Idempotent; shared with the
    /// workspace-kits builder so shelf stock and audited items wire identically.
    public static Rigidbody WireSceneItem(GameObject go, string prefabName, ExperimentRunner runner)
    {
        var rb = PhysicsProfiles.EnsurePhysics(go, prefabName);
        var grab = go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        if (grab != null && go.GetComponent<GrabPhysicsPolicy>() == null)
            go.AddComponent<GrabPhysicsPolicy>();

        // Mishandling + drop-audio pass (shelf items are hand-built — the
        // builder only covers spawned props).
        if (go.GetComponent<DropRespawn>() == null)
        {
            var dr = go.AddComponent<DropRespawn>();
            dr.Bind(rb, grab);
            dr.SetHome(go.transform.position, go.transform.rotation);
        }
        bool breakable = Mishandling.IsBreakable(prefabName)
            || (prefabName.StartsWith("Reagent_") || go.name.StartsWith("Reagent_"));   // shelf bottles are glass
        // Material-nature cleanup: BreakableGlassware breaks whatever it is attached
        // to, so strip it from non-glass items (a scale or burner must not shatter
        // like glass).
        var stale = go.GetComponent<BreakableGlassware>();
        if (!breakable && stale != null) { Object.DestroyImmediate(stale); }
        if (breakable)
        {
            var bg = go.GetComponent<BreakableGlassware>()
                     ?? go.AddComponent<BreakableGlassware>();
            bg.Bind(runner, go.GetComponent<DropRespawn>(), rb, Mishandling.DisplayNameFor(go));
            // The threshold is a SERIALIZED field, so scene components keep a stale
            // value (e.g. 4.5); re-pin every glass item to the current default.
            var bgSo = new SerializedObject(bg);
            var thr = bgSo.FindProperty("breakImpactSpeed");
            if (thr != null && !Mathf.Approximately(thr.floatValue, Mishandling.DefaultBreakSpeed))
            { thr.floatValue = Mishandling.DefaultBreakSpeed; bgSo.ApplyModifiedPropertiesWithoutUndo(); }
        }
        var lp = go.GetComponent<LiquidPhysics>();
        if (lp != null && go.GetComponent<LiquidPourer>() != null && go.GetComponent<SpillMistake>() == null)
            go.AddComponent<SpillMistake>().Bind(runner, lp, grab, Mishandling.DisplayNameFor(go));
        // Scooping tools transfer solid reagents by the scoopful.
        if ((prefabName == "Scoopula" || prefabName == "Spatula")
            && go.GetComponent<ScoopController>() == null)
            go.AddComponent<ScoopController>().Bind(grab);
        // Assembly-capable apparatus sticks together on release.
        if (AssemblyMath.Participates(prefabName))
        {
            var snap = go.GetComponent<ApparatusSnap>() ?? go.AddComponent<ApparatusSnap>();
            snap.Bind(prefabName, grab, rb);
        }
        // The test-tube brush scrubs dirty glassware clean.
        if (prefabName == "TestTubeBrush" && go.GetComponent<BrushController>() == null)
            go.AddComponent<BrushController>().Bind(grab);
        // Re-key ImpactSound by material nature every pass (serialized — stale
        // keys/ceilings persist otherwise).
        var snd = go.GetComponent<ImpactSound>() ?? go.AddComponent<ImpactSound>();
        snd.Bind(rb,
            breakable ? "glass-clink" : Mishandling.DropSoundKey(prefabName),
            breakable ? Mishandling.DefaultBreakSpeed : float.PositiveInfinity);
        EditorUtility.SetDirty(go);
        return rb;
    }

    // ---- drop test ----------------------------------------------------------

    [MenuItem("Tools/PharmaSynth/Physics Audit (Drop Test)")]
    public static void DropTest()
    {
        if (Application.isPlaying) { Debug.LogWarning("[PhysicsAudit] exit Play mode first."); return; }
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib == null) { Debug.LogError("[PhysicsAudit] SceneAssetLibrary.asset not found"); return; }

        // Simulate the DEFAULT physics scene in Script mode, far above the lab
        // (y ≈ 50 m), with every other dynamic rigidbody frozen for the sweep —
        // the open scene never moves. Edit-mode-safe (CreateScene is play-only).
        const float BASE_Y = 50f;
        var prevMode = Physics.simulationMode;
        var frozen = new List<Rigidbody>();
        foreach (var other in Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include))
            if (!other.isKinematic) { other.isKinematic = true; frozen.Add(other); }

        var log = new List<string>();
        int fails = 0, tested = 0;
        GameObject floor = null;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.position = new Vector3(0f, BASE_Y, 0f);
            floor.transform.localScale = Vector3.one * 3f;

            var so = new SerializedObject(lib);
            var prefabs = so.FindProperty("prefabs");
            for (int i = 0; prefabs != null && i < prefabs.arraySize; i++)
            {
                var prefab = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (prefab == null) continue;
                tested++;
                var inst = Object.Instantiate(prefab);
                inst.transform.localScale = Vector3.one;
                var size0 = BoundsUtil.OfRenderers(inst, 0.05f).size;
                if (RealSizes.TryGet(prefab.name, out float target))
                    inst.transform.localScale = Vector3.one * RealSizes.UniformScaleFactor(size0, target);
                PhysicsProfiles.TryGet(prefab.name, out var prof);
                inst.transform.rotation = PhysicsProfiles.RestRotation(prof.pose, BoundsUtil.OfRenderers(inst, 0.05f).size);
                var rb = PhysicsProfiles.EnsurePhysics(inst, prefab.name);
                rb.isKinematic = false;                             // the drop under test
                inst.transform.position = new Vector3(0f, BASE_Y + 0.15f - (BoundsUtil.OfRenderers(inst, 0.05f).min.y - inst.transform.position.y), 0f);

                for (int step = 0; step < 180; step++) Physics.Simulate(1f / 60f);

                var b = BoundsUtil.OfRenderers(inst, 0.05f);
                if (b.min.y < BASE_Y - 0.05f) { log.Add($"FAIL {prefab.name}: tunnelled through the floor (minY {b.min.y - BASE_Y:F3})"); fails++; }
                else if (new Vector2(b.center.x, b.center.z).magnitude > 0.5f) { log.Add($"FAIL {prefab.name}: rolled {new Vector2(b.center.x, b.center.z).magnitude:F2} m away"); fails++; }
                else if (!PhysicsProfiles.IsRestingPlausible(prof.pose, b.size)) { log.Add($"FAIL {prefab.name}: implausible resting pose ({prof.pose}, size {b.size})"); fails++; }
                Object.DestroyImmediate(inst);
            }
        }
        finally
        {
            if (floor != null) Object.DestroyImmediate(floor);
            foreach (var rbF in frozen) if (rbF != null) rbF.isKinematic = false;
            Physics.simulationMode = prevMode;
        }
        if (fails == 0) Debug.Log($"<color=#4CD07D>[PhysicsAudit] drop test: {tested}/{tested} prefabs settle plausibly</color>");
        else Debug.LogError($"[PhysicsAudit] drop test: {fails}/{tested} failed\n" + string.Join("\n", log));
    }

    // ---- helpers ------------------------------------------------------------

    /// Apparatus instances in the open scene: children of the known prop groups
    /// plus anything carrying a LabItem or LiquidPhysics.
    static IEnumerable<GameObject> SceneItems()
    {
        var seen = new HashSet<GameObject>();
        foreach (var groupName in new[] { "ReagentShelf", "EquipmentShelf", "BenchApparatus", "MethaneProps" })
        {
            var group = GameObject.Find(groupName);
            if (group == null) continue;
            foreach (Transform t in group.transform)
                if (t.GetComponentInChildren<Renderer>() != null) seen.Add(t.gameObject);
        }
        foreach (var li in Object.FindObjectsByType<LabItem>()) seen.Add(li.gameObject);
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>()) seen.Add(lp.gameObject);
        return seen;
    }

    /// Best-effort prefab name for a scene instance: the source prefab's name,
    /// else the GO name with Unity's "(N)"/"(Clone)" suffixes stripped.
    /// Public: the workspace-kits builder adopts loose apparatus by this name.
    public static string PrefabNameFor(GameObject go)
    {
        var src = PrefabUtility.GetCorrespondingObjectFromSource(go);
        if (src != null) return src.name;
        string n = go.name.Replace("(Clone)", "").Trim();
        int paren = n.IndexOf(" (");
        if (paren > 0) n = n.Substring(0, paren);
        return n;
    }
}
#endif
