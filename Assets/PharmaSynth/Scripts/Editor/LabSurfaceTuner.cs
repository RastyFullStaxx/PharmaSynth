#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Calibrates the lab's SURFACE materials.
/// The Laboratory pack and the ChemLab equipment pack both ship with
/// uncalibrated PBR values:
///
///   * FOUR materials have _Smoothness ABOVE the 0-1 range (Ceiling_2 = 3.20, Floor = 2.51,
///     Cabinet = 2.19, Ceiling_1 = 2.60). URP clamps to 1 -> a PERFECT MIRROR. With no
///     reflection probe in the scene they mirror the built-in procedural SKYBOX, so
///     a sealed indoor lab reads as wet plastic.
///   * The benchtops sit at 1.00. A lab bench is matte epoxy resin.
///   * The four vessel materials the player handles constantly - beaker 100/500, Erlenmeyer,
///     graduated cylinder - sit at smoothness 0, and glass with NO specular and NO reflection
///     reads as pale plastic blobs. The same pack ships CORRECT glass values on
///     GlassMat/GlassInnerMat/GlassOuterMat (0.92-0.95).
///   * Several non-metals sit at metallic 0 + smoothness 1 - a physically impossible
///     "non-metal mirror" that produces a hard white specular blob instead of a highlight.
///
/// Every value here is a judgement about what the surface IS, so they live in one table
/// rather than being scattered. Suite-pinned (surface:) because a pack reimport or a stray
/// inspector drag restores the bad values silently and pure math cannot see a material
/// regression.
///
/// Tools > PharmaSynth > Tune Lab Surfaces (edit mode, idempotent, re-runnable).
public static class LabSurfaceTuner
{
    const string LabMats  = "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/";
    const string ChemMats = "Assets/PharmaSynth/Art/Equipment/ChemLabEquipment/Materials/";
    const string CabinetNormal =
        "Assets/PharmaSynth/Art/Environment/Laboratory/Textures/Interior/Cabinet_Normal.png";

    /// One surface's calibrated look. metallic < 0 means "leave it alone".
    public struct Surface
    {
        public string path;
        public float smoothness;
        public float metallic;
        public string why;

        public Surface(string path, float smoothness, float metallic, string why)
        { this.path = path; this.smoothness = smoothness; this.metallic = metallic; this.why = why; }
    }

    /// THE calibration table. Pinned by the suite - change a value here and the pin moves with it.
    public static readonly Surface[] Surfaces =
    {
        // --- room shell: the four out-of-range mirrors + the mirror benchtops ---
        new Surface(LabMats + "Exterior/Ceiling_2.mat",               0.08f, -1f, "painted ceiling panel (was 3.20 = mirror)"),
        // Ceiling_1 is DEAD - its FBX, material and prefabs are referenced by nothing - but is
        // still calibrated so the invariant "no material has smoothness > 1" holds with no
        // exemption. One float is cheaper than a documented exemption.
        new Surface(LabMats + "Exterior/Ceiling_1.mat",               0.08f, -1f, "painted ceiling panel (was 2.60 = mirror; unused asset)"),
        new Surface(LabMats + "Exterior/Floor.mat",                   0.35f, -1f, "polished vinyl, not a mirror (was 2.51)"),
        new Surface(LabMats + "Interior/Cabinet.mat",                 0.35f, -1f, "painted steel (was 2.19 = mirror)"),
        new Surface(LabMats + "Interior/Table_1.mat",                 0.25f, -1f, "epoxy resin benchtop - MATTE (was 1.00)"),
        new Surface(LabMats + "Interior/Table_2.mat",                 0.25f, -1f, "epoxy resin benchtop - MATTE (was 1.00)"),
        new Surface(LabMats + "Interior/WashTable.mat",               0.25f, -1f, "epoxy resin benchtop - MATTE (was 1.00)"),
        new Surface(LabMats + "Interior/Switch.mat",                  0.40f, -1f, "moulded plastic switch plate (was 1.00)"),
        new Surface(LabMats + "Interior/HangingTypeExhaustArm_2.mat", 0.60f, -1f, "painted metal ducting (was 1.00)"),

        // --- glassware: the hero props, shipped MATTE ---
        new Surface(ChemMats + "Beaker100mLMat.mat",                  0.93f, -1f, "borosilicate glass (was 0 = matte)"),
        new Surface(ChemMats + "Beaker500mLMat.mat",                  0.93f, -1f, "borosilicate glass (was 0 = matte)"),
        new Surface(ChemMats + "ErlenmeyerFlask400mLMat.mat",         0.93f, -1f, "borosilicate glass (was 0 = matte)"),
        new Surface(ChemMats + "GraduatedCylinderMat.mat",            0.93f, -1f, "borosilicate glass (was 0 = matte)"),

        // --- equipment: non-metal mirrors and un-metalled metal ---
        new Surface(ChemMats + "TestTubeRackMat.mat",                 0.40f, -1f,   "moulded rack, not a mirror (was 1.00)"),
        new Surface(ChemMats + "BalanceMat.mat",                      0.50f, 0.80f, "metal balance chassis (was metallic 0 + mirror)"),
        new Surface(ChemMats + "BunsenBurner.mat",                    0.45f, 0.90f, "cast metal burner (was metallic 0 + mirror)"),
        new Surface(ChemMats + "AlcoholBurnerMat.mat",                0.45f, 0.80f, "metal burner body (was metallic 0 + mirror)"),
        new Surface(ChemMats + "EquipmentMat.mat",                    0.45f, -1f,   "mixed lab hardware (was 1.00)"),
        new Surface(ChemMats + "Equipment2Mat.mat",                   0.45f, -1f,   "mixed lab hardware (was 1.00)"),
        new Surface(ChemMats + "PlasticMat.mat",                      0.30f, -1f,   "moulded plastic (was 1.00)"),
    };

    [MenuItem("Tools/PharmaSynth/Tune Lab Surfaces")]
    public static void Tune()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LabSurfaces] exit Play mode first."); return; }

        int changed = 0, missing = 0;
        var lines = new List<string>();

        foreach (var s in Surfaces)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(s.path);
            if (mat == null) { missing++; Debug.LogWarning("[LabSurfaces] missing " + s.path); continue; }

            bool touched = false;
            if (mat.HasProperty("_Smoothness") && !Mathf.Approximately(mat.GetFloat("_Smoothness"), s.smoothness))
            {
                lines.Add(string.Format("  {0,-28} smooth {1,5:0.00} -> {2:0.00}   ({3})",
                    System.IO.Path.GetFileNameWithoutExtension(s.path),
                    mat.GetFloat("_Smoothness"), s.smoothness, s.why));
                mat.SetFloat("_Smoothness", s.smoothness);
                touched = true;
            }
            // Some import paths mirror smoothness into _Glossiness; keep the two in step.
            if (mat.HasProperty("_Glossiness") && !Mathf.Approximately(mat.GetFloat("_Glossiness"), s.smoothness))
            { mat.SetFloat("_Glossiness", s.smoothness); touched = true; }

            if (s.metallic >= 0f && mat.HasProperty("_Metallic") &&
                !Mathf.Approximately(mat.GetFloat("_Metallic"), s.metallic))
            {
                lines.Add(string.Format("  {0,-28} metal  {1,5:0.00} -> {2:0.00}",
                    System.IO.Path.GetFileNameWithoutExtension(s.path),
                    mat.GetFloat("_Metallic"), s.metallic));
                mat.SetFloat("_Metallic", s.metallic);
                touched = true;
            }

            if (touched) { EditorUtility.SetDirty(mat); changed++; }
        }

        // The pack ships Cabinet_Normal.png and Cabinet.mat never binds it - the only genuine
        // orphan map in either texture tree.
        int bound = BindCabinetNormal();

        AssetDatabase.SaveAssets();

        if (lines.Count > 0) Debug.Log("[LabSurfaces] changes:\n" + string.Join("\n", lines));
        Debug.Log(string.Format(
            "<color=#4CD07D>[LabSurfaces] {0} material(s) retuned, {1} normal map bound, {2} missing.</color> " +
            "Re-run any time; already-correct materials are skipped.", changed, bound, missing));
    }

    /// Returns 1 if it bound the orphaned cabinet normal this run, else 0.
    static int BindCabinetNormal()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(LabMats + "Interior/Cabinet.mat");
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(CabinetNormal);
        if (mat == null || tex == null) return 0;
        if (!mat.HasProperty("_BumpMap") || mat.GetTexture("_BumpMap") != null) return 0;

        mat.SetTexture("_BumpMap", tex);
        mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
        return 1;
    }
}
#endif
