#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Gives every solid reagent jar on the shelf its powder mound.
///
/// `Build Reagent Cabinets` stocks a PowderJar row from the `Beaker_100mL_WithLiquid`
/// prefab, which carries a `Liquid` surface and nothing else, so without a mound a SOLID
/// is drawn as a column of liquid (`LiquidPhysics.DrawsLiquidColumn` has no mound to show
/// instead). The scoopula is unaffected either way: `ScoopMath.CanPickUp` keys on the
/// chemical's `state`.
///
/// The mound itself is `ExperimentSceneBuilder.EnsurePowderVisual`, the same helper the
/// stage builder and the scoop already use; this only points it at the shelf. Idempotent:
/// a jar that already has its mound is left alone. Called by `Build Reagent Cabinets` at
/// the end of a rebuild, and available on its own for the saved scene.
public static class SolidJarFitter
{
    /// Pure, suite-pinned: does this chemical sit in a jar as a heap rather than a liquid?
    public static bool IsHeap(ChemicalData chem)
        => chem != null && (chem.state == PhysicalState.Solid || chem.state == PhysicalState.Powder);

    [MenuItem("Tools/PharmaSynth/Fit Solid Jars (powder mounds)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SolidJars] exit Play mode first."); return; }
        int n = FitAll(out string names);
        if (n > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[SolidJars] " + n + " jar(s) given a powder mound"
                  + (names.Length > 0 ? ": " + names : " — nothing to do") + "</color>");
    }

    /// Every reagent jar holding a solid gets a mound sized to what it holds. Returns how
    /// many were missing one.
    public static int FitAll(out string names)
    {
        int fitted = 0; names = "";
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lp == null || !LabReset.IsReagent(lp.name) || !IsHeap(lp.currentChemical)) continue;
            bool had = lp.transform.Find("Powder") != null;
            float fill = lp.maxVolume > 0f ? Mathf.Clamp01(lp.currentLiquidVolume / lp.maxVolume) : 1f;
            ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, lp.currentChemical, fill);
            EditorUtility.SetDirty(lp.gameObject);
            if (had) continue;
            fitted++;
            if (names.Length < 160) names += (names.Length > 0 ? ", " : "") + lp.name;
        }
        return fitted;
    }
}
#endif
