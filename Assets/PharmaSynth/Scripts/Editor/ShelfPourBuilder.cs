#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Wires the hand-placed reagent bottles for visible pouring (LiquidPourer is
/// otherwise only on runtime-spawned props, so tipping a shelf bottle shows
/// nothing). Sweeps every LiquidPhysics under the ReagentShelf
/// and ReagentCabinets roots through
/// ShelfPourWiring.WireBottle, and ensures the persisted particle material
/// asset exists so device builds don't strip the URP particle shader.
/// Idempotent; re-running reports 0 additions.
public static class ShelfPourBuilder
{
    const string FxMatPath = "Assets/PharmaSynth/Resources/FxParticleUnlit.mat";
    static readonly string[] BottleRoots = { "ReagentShelf", "ReagentCabinets" };

    [MenuItem("Tools/PharmaSynth/Wire Shelf Pourers")]
    public static void Wire()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[ShelfPourBuilder] exit Play mode first.");
            return;
        }

        EnsureFxMaterial();

        var runner = Object.FindAnyObjectByType<ExperimentRunner>();
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");

        int bottles = 0, additions = 0;
        foreach (string rootName in BottleRoots)
        {
            var root = GameObject.Find(rootName);
            if (root == null) continue;
            foreach (var lp in root.GetComponentsInChildren<LiquidPhysics>(true))
            {
                int added = ShelfPourWiring.WireBottle(lp.gameObject, runner, registry);
                if (added < 0) continue;
                bottles++;
                if (added > 0)
                {
                    additions += added;
                    // The bottles are prefab INSTANCES: direct field writes (lp.registry)
                    // are lost on save unless recorded as instance overrides.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(lp);
                    EditorUtility.SetDirty(lp.gameObject);
                }
            }
        }

        if (additions > 0)
        {
            EditorSceneManager.MarkAllScenesDirty();
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log($"[ShelfPourBuilder] {bottles} bottles checked, {additions} components added.");
    }

    /// Load-or-create the persisted alpha-blended particle material. Its job is
    /// (a) forcing the URP particle shader into device builds (runtime
    /// Shader.Find-only materials get stripped) and (b) carrying the exact
    /// blend state EffectVfx needs. EffectVfx instantiates from it at runtime.
    public static Material EnsureFxMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(FxMatPath);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) { Debug.LogError("[ShelfPourBuilder] no particle shader found"); return null; }
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, FxMatPath);
        }
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
        m.SetFloat("_Surface", 1f);          // transparent
        if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);   // alpha
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
