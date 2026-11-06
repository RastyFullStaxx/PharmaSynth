#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// Builds the cyan "materialize" spawn burst: a one-shot column
/// of cyan particles that rises from the player's feet like smoke, played on every
/// teleport / reset / spawn. Creates a shared soft-dot texture + additive material,
/// then drops a configured `SpawnVFX` object (SpawnBurstFX + ParticleSystem) into
/// the ACTIVE scene. Re-runnable. Run it once in MainMenu and once in SampleScene.
///
/// Tools ▸ PharmaSynth ▸ Build Spawn VFX.
public static class SpawnVfxBuilder
{
    const string Dir = "Assets/PharmaSynth/Art/Generated/VFX";
    const string DotPath = Dir + "/SoftDot.png";
    const string MatPath = Dir + "/SpawnBurst.mat";

    [MenuItem("Tools/PharmaSynth/Build Spawn VFX")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SpawnVFX] exit Play mode first."); return; }
        Directory.CreateDirectory(Dir);

        var dot = EnsureSoftDot();
        var mat = EnsureMaterial(dot);

        var old = GameObject.Find("SpawnVFX");
        if (old != null) Object.DestroyImmediate(old);

        var go = new GameObject("SpawnVFX");
        Undo.RegisterCreatedObjectUndo(go, "Build Spawn VFX");
        go.transform.position = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        ConfigureSystem(ps, mat);
        go.AddComponent<SpawnBurstFX>();     // auto-finds the ParticleSystem in Awake; playOnStart=true

        EditorUtility.SetDirty(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("<color=#4CD07D>[SpawnVFX] SpawnVFX built in scene '" + go.scene.name + "'</color>");
    }

    static void ConfigureSystem(ParticleSystem ps, Material mat)
    {
        var main = ps.main;
        main.duration = 2.0f;                          // covers all three waves
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.0f, 1.7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.3f, 2.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.22f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.3f, 0.92f, 1f), new Color(0.6f, 1f, 1f));
        main.gravityModifier = -0.03f;                 // gentle upward drift
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;

        var em = ps.emission;
        em.rateOverTime = 0f;
        // THREE waves, so the burst is noticeable.
        em.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)90),
            new ParticleSystem.Burst(0.4f, (short)90),
            new ParticleSystem.Burst(0.8f, (short)90),
        });

        var sh = ps.shape;
        sh.enabled = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle = 9f;                                 // narrow rising column
        sh.radius = 0.38f;
        sh.rotation = new Vector3(-90f, 0f, 0f);       // aim the cone up (+Y)
        sh.length = 0.1f;

        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space = ParticleSystemSimulationSpace.World;
        vol.y = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(new Color(0.6f, 1f, 1f), 0f), new GradientColorKey(new Color(0.2f, 0.85f, 1f), 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.18f), new GradientAlphaKey(0.65f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sc = new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, sc);

        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.sharedMaterial = mat;
        rend.sortingOrder = 100;
    }

    static Texture2D EnsureSoftDot()
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
        if (existing != null) return existing;

        const int S = 64;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(S * 0.5f, S * 0.5f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), c) / (S * 0.5f);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a);              // smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        File.WriteAllBytes(DotPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(DotPath);
        var imp = (TextureImporter)AssetImporter.GetAtPath(DotPath);
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Default;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(DotPath);
    }

    static Material EnsureMaterial(Texture2D dot)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            AssetDatabase.CreateAsset(mat, MatPath);
        }
        mat.SetColor("_BaseColor", new Color(0.35f, 0.95f, 1f, 1f));
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", dot);
        mat.mainTexture = dot;
        mat.SetFloat("_Surface", 1f);                  // transparent
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);   // additive glow
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        return mat;
    }
}
#endif
