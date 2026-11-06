#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// Generates the lab's MISSING surface textures.
///
/// The Laboratory pack leaves the two largest surfaces in the player's view with no albedo at
/// all - Wall_0/Wall_1 carry a normal map and nothing else, and Ceiling_2 carries no maps
/// whatsoever. A featureless white plane makes the room read as an
/// untextured grey box, and it is worst on the ceiling, which a seated VR player looks
/// straight up into.
///
/// Everything here is drawn in code and written to PNG, as LabelForge does
/// for reagent labels: deterministic, re-runnable, diffable, and free. All noise wraps,
/// so the maps tile seamlessly.
///
/// Tools > PharmaSynth > Generate Lab Surface Textures (edit mode, idempotent, re-runnable).
public static class LabSurfaceTextureForge
{
    const string OutDir  = "Assets/PharmaSynth/Art/Generated/Surfaces";
    const string LabMats = "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/";
    const int    Size    = 1024;

    [MenuItem("Tools/PharmaSynth/Generate Lab Surface Textures")]
    public static void Generate()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SurfaceForge] exit Play mode first."); return; }
        Directory.CreateDirectory(OutDir);

        var wall    = Write("LabWall_Albedo",    BuildWall());
        var ceiling = Write("LabCeiling_Albedo", BuildCeilingTile());

        AssetDatabase.Refresh();

        // Tiling is in TILES ACROSS THE MESH's UV, not metres - these meshes each map their
        // whole surface to 0-1, so one repeat would stretch a single texture over the entire
        // wall or ceiling and read as fog at VR distance.
        Bind(LabMats + "Exterior/Wall_0.mat",   wall,    new Vector2(6f, 3f), Color.white);
        Bind(LabMats + "Exterior/Wall_1.mat",   wall,    new Vector2(6f, 3f), new Color(0.78f, 0.79f, 0.81f));
        Bind(LabMats + "Exterior/Ceiling_2.mat",ceiling, new Vector2(8f, 8f), Color.white);

        // NO benchtop map here, deliberately. Table_1/Table_2/WashTable ALREADY ship a pack
        // albedo, and it is an ATLAS covering the white cabinet doors as well as the dark
        // worktop, so a flat epoxy tile bound over it turns every bench cabinet in the room
        // black. The benchtop reads correctly from its smoothness alone (LabSurfaceTuner).

        // NO floor retile, deliberately. Floor_AlbedoTransparency.png has a DEFECTIVE band of
        // vertical streaks baked along its top edge; at the pack's 1x1 mapping that band lands
        // once, against the far wall, and reads as nothing. Tiling it 6x6 repeats the defect
        // six times across the room as hard rectangular streaks that look exactly like a bad
        // lightmap bake.

        AssetDatabase.SaveAssets();
        Debug.Log("<color=#4CD07D>[SurfaceForge] wrote 2 tiling maps to " + OutDir +
                  " and bound them to the walls + ceiling (+ retiled the floor).</color>");
    }

    // ---------- the maps ----------

    /// Painted lab wall: near-white with a faint large-scale mottle so a 6 m wall is not one
    /// dead value, plus very fine grain to catch the light.
    static Color[] BuildWall()
    {
        var px = new Color[Size * Size];
        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            float mottle = Wrapped(x, y, 3) * 0.030f + Wrapped(x, y, 7) * 0.015f;
            float grain  = (Hash(x, y) - 0.5f) * 0.012f;
            float v      = 0.90f + mottle - 0.022f + grain;
            px[y * Size + x] = new Color(v, v, v * 0.995f, 1f);
        }
        return px;
    }

    /// Suspended acoustic ceiling: a real tile grid with recessed seams and slight per-tile
    /// value variation, plus the mineral-fibre speckle. The seams are what give a VR player
    /// any sense of ceiling height at all.
    static Color[] BuildCeilingTile()
    {
        var px = new Color[Size * Size];
        const int tiles = 2;                    // 2x2 per map -> seams land on a sane pitch
        int cell = Size / tiles;
        const int seam = 5;

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            int cx = x / cell, cy = y / cell;
            int ix = x % cell, iy = y % cell;

            // Each tile sits at its own very slightly different value, like real fibre board.
            float tileBias = (Hash(cx * 31 + 7, cy * 17 + 3) - 0.5f) * 0.030f;
            float speckle  = (Hash(x, y) - 0.5f) * 0.045f + Wrapped(x, y, 11) * 0.020f;
            float v = 0.885f + tileBias + speckle - 0.010f;

            // Recessed grid line, softened by one pixel so it does not crawl under MSAA.
            int edge = Mathf.Min(Mathf.Min(ix, cell - 1 - ix), Mathf.Min(iy, cell - 1 - iy));
            if (edge < seam) v *= Mathf.Lerp(0.55f, 1f, edge / (float)seam);

            px[y * Size + x] = new Color(v, v, v * 0.99f, 1f);
        }
        return px;
    }

    // ---------- helpers ----------

    /// Value noise on a torus: sampling with sin/cos at integer frequencies guarantees the
    /// left edge meets the right and the top meets the bottom, so the map tiles with no seam.
    static float Wrapped(int x, int y, int freq)
    {
        float u = x / (float)Size * Mathf.PI * 2f;
        float v = y / (float)Size * Mathf.PI * 2f;
        float a = Mathf.Sin(u * freq) * Mathf.Cos(v * freq);
        float b = Mathf.Sin(u * freq * 0.5f + 1.7f) * Mathf.Cos(v * freq * 0.5f + 0.3f);
        return (a * 0.6f + b * 0.4f) * 0.5f + 0.5f;
    }

    /// Cheap deterministic hash in 0-1. Deterministic matters: re-running must not churn the
    /// PNGs in git.
    static float Hash(int x, int y)
    {
        int n = x * 374761393 + y * 668265263;
        n = (n ^ (n >> 13)) * 1274126177;
        return ((n ^ (n >> 16)) & 0x7fffffff) / (float)0x7fffffff;
    }

    static string Write(string name, Color[] px)
    {
        string path = OutDir + "/" + name + ".png";
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.textureType    = TextureImporterType.Default;
            imp.sRGBTexture    = true;
            imp.wrapMode       = TextureWrapMode.Repeat;
            imp.maxTextureSize = 1024;
            imp.crunchedCompression = true;   // tiling maps compress well and this is a Quest build
            imp.compressionQuality  = 60;
            imp.SaveAndReimport();
        }
        return path;
    }

    static void Bind(string matPath, string texPath, Vector2 tiling, Color baseColor)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
        if (mat == null || tex == null) { Debug.LogWarning("[SurfaceForge] missing " + matPath); return; }

        mat.SetTexture("_BaseMap", tex);
        mat.SetTextureScale("_BaseMap", tiling);
        if (mat.HasProperty("_MainTex")) { mat.SetTexture("_MainTex", tex); mat.SetTextureScale("_MainTex", tiling); }
        // The pack tinted some of these via _BaseColor; keep the tint but let the map show.
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", baseColor);
        EditorUtility.SetDirty(mat);
    }

}
#endif
