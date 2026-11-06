#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// Turns the render pipeline ON.
///
/// What it corrects in the scene defaults:
///   * BOTH cameras have renderPostProcessing = false, so the global Volume with Bloom,
///     Vignette and Tonemapping renders NOTHING.
///   * MSAA 1 (off). On the Adreno tile GPU 4x resolves in tile memory and is close to
///     free, and this scene is nothing but thin edges - tube rims, glass rods, rack rails.
///     It is the largest per-pixel quality gain available.
///   * HDR off, so emission above 1.0 just clamps: the 16 emissive ceiling panels can
///     never read as LIGHTS, only as white paint, and tonemapping has no range to work with.
///   * FLAT grey 0.45 ambient, identical from every direction, so nothing in the room
///     has any vertical shading and the whole space reads as one plane of grey.
///   * The built-in PROCEDURAL SKY, in a sealed windowless room, feeding
///     ambient and every reflection an outdoor gradient.
///
/// Vignette is deliberately DISABLED rather than tuned down: the template has it at full
/// strength, so it switches on the moment post is enabled, and a vignette in a
/// headset reads as a dirty lens rather than as framing.
///
/// Tools > PharmaSynth > Tune Render Pipeline (edit mode, idempotent, re-runnable).
public static class LabRenderTuner
{
    const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
    const string MobileRp    = "Assets/Settings/Mobile_RPAsset.asset";
    const string PcRp        = "Assets/Settings/PC_RPAsset.asset";

    public const int   Msaa            = 4;
    public const float BloomIntensity  = 0.15f;
    public const float BloomThreshold  = 1.0f;
    public const float PostExposure    = 0.15f;
    public const float Contrast        = 8f;
    public const float Saturation      = 5f;
    public const float Temperature     = -6f;   // cool = clinical

    // Trilight ambient. Bright from the ceiling, mid at eye level, dark at the floor - the
    // vertical gradient is what stops every surface reading as the same flat value.
    public static readonly Color AmbientSky     = new Color(0.42f, 0.43f, 0.46f);
    public static readonly Color AmbientEquator = new Color(0.30f, 0.30f, 0.31f);
    public static readonly Color AmbientGround  = new Color(0.14f, 0.13f, 0.12f);

    [MenuItem("Tools/PharmaSynth/Tune Render Pipeline")]
    public static void Tune()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabRender] exit Play mode first."); return; }

        var log = new List<string>();
        TuneRpAsset(MobileRp, log);
        TuneRpAsset(PcRp, log);
        TuneProfile(log);
        int cams = EnableCameraPostProcessing(log);
        ApplyAmbient();
        log.Add("  ambient      -> Trilight gradient (was flat 0.45, no directional variation)");
        log.Add("  skybox       -> none (sealed room; the procedural sky was feeding ambient + reflections)");

        AssetDatabase.SaveAssets();
        EditorUtility.SetDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().GetRootGameObjects()[0]);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[LabRender] changes:\n" + string.Join("\n", log));
        Debug.Log(string.Format(
            "<color=#4CD07D>[LabRender] pipeline on: MSAA {0}x, HDR, post-processing on {1} camera(s), " +
            "gradient ambient.</color> Save the scene (Ctrl-S) to keep the camera + ambient changes.",
            Msaa, cams));
    }

    /// Ambient lives here, not in LabLightingBuilder, so a later "Brighten Lab Lighting" run
    /// cannot silently stomp it back to flat grey.
    public static void ApplyAmbient()
    {
        RenderSettings.ambientMode      = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor  = AmbientSky;
        RenderSettings.ambientEquatorColor = AmbientEquator;
        RenderSettings.ambientGroundColor  = AmbientGround;
        RenderSettings.ambientIntensity = 1f;
        // A sealed lab has no sky. The procedural skybox makes every smooth
        // surface reflect an outdoor gradient.
        RenderSettings.skybox = null;
    }

    static void TuneRpAsset(string path, List<string> log)
    {
        var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (rp == null) { Debug.LogWarning("[LabRender] missing " + path); return; }

        var so = new SerializedObject(rp);
        string name = System.IO.Path.GetFileNameWithoutExtension(path);

        var msaa = so.FindProperty("m_MSAA");
        if (msaa != null && msaa.intValue != Msaa)
        { log.Add(string.Format("  {0,-16} MSAA {1}x -> {2}x", name, msaa.intValue, Msaa)); msaa.intValue = Msaa; }

        var hdr = so.FindProperty("m_SupportsHDR");
        if (hdr != null && !hdr.boolValue)
        { log.Add(string.Format("  {0,-16} HDR off -> ON", name)); hdr.boolValue = true; }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(rp);
    }

    static void TuneProfile(List<string> log)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null) { Debug.LogWarning("[LabRender] missing " + ProfilePath); return; }

        // Neutral, not ACES: ACES crushes and desaturates, and a clinical lab wants clean whites.
        var tone = Get<Tonemapping>(profile);
        tone.active = true;
        tone.mode.overrideState = true;
        tone.mode.value = TonemappingMode.Neutral;
        log.Add("  profile      Tonemapping -> Neutral (was inert; post was off)");

        // Just enough for the emissive ceiling panels to read as lights. Heavy bloom in
        // stereo is nauseating.
        var bloom = Get<Bloom>(profile);
        bloom.active = true;
        bloom.intensity.overrideState = true;  bloom.intensity.value = BloomIntensity;
        bloom.threshold.overrideState = true;  bloom.threshold.value = BloomThreshold;
        log.Add(string.Format("  profile      Bloom -> {0} @ threshold {1}", BloomIntensity, BloomThreshold));

        // OFF, not merely low. In a headset a vignette reads as a dirty lens.
        var vig = Get<Vignette>(profile);
        vig.active = false;
        log.Add("  profile      Vignette -> OFF (VR comfort; it was active at template strength)");

        var col = Get<ColorAdjustments>(profile);
        col.active = true;
        col.postExposure.overrideState = true; col.postExposure.value = PostExposure;
        col.contrast.overrideState = true;     col.contrast.value = Contrast;
        col.saturation.overrideState = true;   col.saturation.value = Saturation;
        log.Add(string.Format("  profile      ColorAdjustments -> exposure {0}, contrast {1}, saturation {2}",
            PostExposure, Contrast, Saturation));

        var wb = Get<WhiteBalance>(profile);
        wb.active = true;
        wb.temperature.overrideState = true; wb.temperature.value = Temperature;
        log.Add(string.Format("  profile      WhiteBalance -> temperature {0} (cool = clinical)", Temperature));

        // Never in VR.
        var mb = Get<MotionBlur>(profile);
        mb.active = false;

        EditorUtility.SetDirty(profile);
    }

    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T c;
        if (!profile.TryGet(out c)) c = profile.Add<T>(true);
        return c;
    }

    /// Only MainCamera-tagged cameras. The mirror's reflection camera renders to its own
    /// RenderTexture every frame and must NOT pay for a post stack.
    static int EnableCameraPostProcessing(List<string> log)
    {
        int n = 0;
        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!cam.CompareTag("MainCamera")) continue;
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = Undo.AddComponent<UniversalAdditionalCameraData>(cam.gameObject);
            if (data.renderPostProcessing) continue;

            Undo.RecordObject(data, "Tune Render Pipeline");
            data.renderPostProcessing = true;
            EditorUtility.SetDirty(data);
            log.Add("  camera       " + cam.name + " -> post-processing ON");
            n++;
        }
        return n;
    }
}
#endif
