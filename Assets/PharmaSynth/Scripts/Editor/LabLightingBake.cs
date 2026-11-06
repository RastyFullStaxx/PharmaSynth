#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Prepares and runs the lab's LIGHTMAP BAKE.
///
/// Without it nothing in the lab casts a shadow onto anything else: all 21 lights are Realtime
/// with additional-light shadows disabled in the RP asset, so every stool, cabinet and bench
/// floats. Baked GI is also FREE at runtime, which is why it is the right tool on a Quest.
///
/// The three steps that have to happen in THIS order, because each depends on the last:
///   1. Lightmap UVs. A mesh with no UV2 cannot receive a lightmap. 36 of the project's 107
///      models lack them, including the entire room shell (Wall, Floor, Ceiling_2, the
///      tables). Anything still missing UV2 after the import pass is set to receive from
///      LIGHT PROBES instead, so it still occludes and bounces without a broken lightmap.
///   2. Static flags, filtered HARD. A grabbable baked into a lightmap carries its baked
///      shadow around the room in your hand, so anything with a Rigidbody, an
///      XRGrabInteractable, a DropRespawn or a LabItem is excluded, as is everything the
///      stage builder spawns.
///   3. Light modes. Realtime lights contribute nothing to a bake.
///
/// Tools > PharmaSynth > Prepare Lab Lighting Bake  - steps 1-3, no bake (fast, inspectable)
/// Tools > PharmaSynth > Run Lab Lighting Bake      - prepare, then bake (slow)
public static class LabLightingBake
{
    const string SettingsPath = "Assets/Settings/LabLightingSettings.lighting";

    /// Roots whose CONTENTS are movable at runtime and must never be baked in.
    static readonly HashSet<string> DynamicRoots = new HashSet<string>
    {
        "DynamicStage", "MethaneStage", "XR Origin (XR Rig)", "HudRig", "ScreenFader",
        "DrJimenez", "RobotNPC", "PPE_Standins", "LabCoatDisplay", "WaypointMarker",
        "LabProbes", "LabLights", "AtmosphereVfx", "SpawnVFX", "WorldLabels",
    };

    [MenuItem("Tools/PharmaSynth/Prepare Lab Lighting Bake")]
    public static void Prepare() { DoPrepare(true); }

    [MenuItem("Tools/PharmaSynth/Run Lab Lighting Bake")]
    public static void Run()
    {
        if (!DoPrepare(true)) return;
        Debug.Log("[LabBake] baking - this takes minutes; the editor stays responsive.");
        Lightmapping.BakeAsync();
    }

    static bool DoPrepare(bool verbose)
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabBake] exit Play mode first."); return false; }

        int uvFixed = EnsureLightmapUVs();
        int marked, probeOnly;
        MarkStatics(out marked, out probeOnly);
        int lights = SetLightModes();
        ConfigureSettings();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        if (verbose)
            Debug.Log(string.Format(
                "<color=#4CD07D>[LabBake] ready: {0} model(s) given lightmap UVs, {1} object(s) static " +
                "({2} of them probe-lit only), {3} light(s) switched off Realtime.</color>",
                uvFixed, marked, probeOnly, lights));
        return true;
    }

    // ---------- 1. lightmap UVs ----------

    /// Turns on generateSecondaryUV for every FBX feeding an object we are about to mark
    /// static. Scoped to those meshes on purpose: a blanket reimport of all 107 models would
    /// re-resolve materials by name on the Laboratory pack (materialLocation External with an
    /// empty externalObjects map), and there is no need to risk that on meshes the bake will
    /// never touch.
    static int EnsureLightmapUVs()
    {
        var paths = new HashSet<string>();
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include))
        {
            if (mf.sharedMesh == null) continue;
            if (!IsBakeCandidate(mf.gameObject)) continue;
            var p = AssetDatabase.GetAssetPath(mf.sharedMesh);
            if (string.IsNullOrEmpty(p) || !p.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) continue;
            paths.Add(p);
        }

        int n = 0;
        foreach (var p in paths)
        {
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp == null || imp.generateSecondaryUV) continue;
            imp.generateSecondaryUV = true;
            imp.SaveAndReimport();
            n++;
        }
        return n;
    }

    // ---------- 2. static flags ----------

    static void MarkStatics(out int marked, out int probeOnly)
    {
        marked = 0; probeOnly = 0;
        foreach (var mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include))
        {
            var go = mr.gameObject;
            if (!IsBakeCandidate(go)) continue;

            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic |
                        StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
                        StaticEditorFlags.ReflectionProbeStatic;
            if (GameObjectUtility.GetStaticEditorFlags(go) != flags)
            {
                Undo.RecordObject(go, "Prepare Lab Lighting Bake");
                GameObjectUtility.SetStaticEditorFlags(go, flags);
                EditorUtility.SetDirty(go);
            }

            // No UV2 means no lightmap is possible; fall back to probe lighting so the object
            // still occludes and bounces instead of rendering black.
            var mf = go.GetComponent<MeshFilter>();
            bool hasUv2 = mf != null && mf.sharedMesh != null && mf.sharedMesh.uv2 != null && mf.sharedMesh.uv2.Length > 0;
            var want = hasUv2 ? ReceiveGI.Lightmaps : ReceiveGI.LightProbes;
            if (mr.receiveGI != want)
            {
                Undo.RecordObject(mr, "Prepare Lab Lighting Bake");
                mr.receiveGI = want;
                EditorUtility.SetDirty(mr);
            }
            // Fixes the floor's rectangular streaks. A big slab auto-unwraps into many
            // charts, and lighting solved per-chart does not agree across their shared edges,
            // so every chart boundary shows as a hard line on an otherwise flat surface.
            // Seam stitching solves the boundary texels jointly and costs bake time only.
            if (hasUv2 && !mr.stitchLightmapSeams)
            {
                Undo.RecordObject(mr, "Prepare Lab Lighting Bake");
                mr.stitchLightmapSeams = true;
                EditorUtility.SetDirty(mr);
            }

            if (!hasUv2) probeOnly++;
            marked++;
        }
    }

    /// Conservative on purpose: anything that can move, be picked up, be re-homed, or be
    /// spawned by the stage builder is NOT a bake candidate.
    static bool IsBakeCandidate(GameObject go)
    {
        if (go.GetComponent<MeshRenderer>() == null) return false;
        if (go.GetComponentInParent<Rigidbody>() != null) return false;
        if (go.GetComponentInParent<Canvas>() != null) return false;
        if (go.GetComponent<DropRespawn>() != null) return false;
        if (go.GetComponent<LabItem>() != null) return false;
        if (go.GetComponentInParent<LiquidPhysics>() != null) return false;

        for (var t = go.transform; t != null; t = t.parent)
        {
            if (DynamicRoots.Contains(t.name)) return false;
            if (t.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null)
                return false;
        }
        return true;
    }

    // ---------- 3. light modes ----------

    /// Point fills become fully Baked (free at runtime); the directional key becomes Mixed so
    /// dynamic objects still get a real-time shadow-casting key from it.
    static int SetLightModes()
    {
        int n = 0;
        foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (l.GetComponentInParent<Canvas>() != null) continue;
            var want = l.type == LightType.Directional ? LightmapBakeType.Mixed : LightmapBakeType.Baked;
            if (l.lightmapBakeType == want) continue;
            Undo.RecordObject(l, "Prepare Lab Lighting Bake");
            l.lightmapBakeType = want;
            EditorUtility.SetDirty(l);
            n++;
        }
        return n;
    }

    // ---------- 4. bake settings ----------

    static void ConfigureSettings()
    {
        var s = AssetDatabase.LoadAssetAtPath<LightingSettings>(SettingsPath);
        if (s == null)
        {
            s = new LightingSettings { name = "LabLightingSettings" };
            AssetDatabase.CreateAsset(s, SettingsPath);
        }

        s.bakedGI = true;
        s.realtimeGI = false;
        // Resolution does not cause the floor's streaks (12 -> 24 leaves them unchanged): they are
        // SEAMS between the auto-unwrapped charts of one 10.5 x 11 m slab, fixed by
        // stitchLightmapSeams on the shell (see MarkStatics). Resolution 40 is here so the atlas
        // grows past 512 and the 16-texel padding stops being a large fraction of it.
        s.lightmapResolution = 40f;
        s.lightmapMaxSize = 2048;
        s.lightmapPadding = 16;
        s.lightmapCompression = LightmapCompression.NormalQuality;
        s.ao = true;                    // the contact darkening that actually grounds objects
        s.aoMaxDistance = 0.7f;
        s.aoExponentDirect = 0.6f;
        s.aoExponentIndirect = 1f;
        s.directionalityMode = LightmapsMode.NonDirectional;   // half the memory, fine for matte surfaces
        s.indirectScale = 1.2f;
        s.albedoBoost = 1f;
        s.mixedBakeMode = MixedLightingMode.IndirectOnly;      // no shadowmask memory on Quest
        s.lightProbeSampleCountMultiplier = 2f;

        EditorUtility.SetDirty(s);
        Lightmapping.lightingSettings = s;
        AssetDatabase.SaveAssets();
    }
}
#endif
