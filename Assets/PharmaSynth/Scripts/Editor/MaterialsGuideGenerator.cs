#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// Fills every module's MATERIALS guide (the watch-panel header):
/// every reagent and apparatus the module needs, listed first as a guide so the
/// player can assemble them before starting.
///
/// REAGENTS are DERIVED from the module's layout bindings — the ground truth of
/// what the experiment actually consumes — with totals summed per chemical, so the
/// guide can never drift from the tasks. Units follow the game's own convention
/// (1 squeeze = 1 ml): liquids read "N ml", solids/powders "N g".
///
/// APPARATUS is AUTHORED here per module, from the PROCEDURES — never from the
/// manuscript's own apparatus lists, which are documented as defective
/// (experiments-reference §Apparatus: "stage from the PROCEDURE, never the list").
///
/// Idempotent: re-run after any layout change.
public static class MaterialsGuideGenerator
{
    /// Apparatus per module, written from each procedure's actual verbs.
    static readonly Dictionary<string, string[]> Apparatus = new Dictionary<string, string[]>
    {
        ["tutorial-methane"] = new[] {
            "Scoopula", "Mortar & pestle", "Hard-glass tube", "Burner",
            "Collection tube", "Matchsticks + striker box" },
        ["prelim-chemical-compounding"] = new[] {
            "Test tubes × 19", "Tube holders × 4", "Droppers × 4", "Porcelain spatula",
            "Beaker 100 mL", "Funnel + filter paper", "Bunsen burner + matches", "Water bath" },
        ["prelim-ethyl-alcohol"] = new[] {
            "Fermentation flask", "Delivery (bent) tube + stopper", "Cotton swab",
            "Test tubes × 3", "Distilling flask", "Burner", "Watch glass",
            "Graduated cylinder 50 mL", "Litmus paper" },
        ["midterm-benzoic-acid"] = new[] {
            "Beaker 500 mL", "Funnel + filter paper", "Test tubes × 3",
            "Burner", "Glass rod" },
        ["midterm-acetanilide"] = new[] {
            "Beaker 100 mL", "Evaporating dish", "Funnel + filter paper",
            "Test tubes × 2", "Burner (heat bath)" },
        ["midterm-acetone"] = new[] {
            "Weighing balance", "Hard-glass tube (dry distillation)", "Delivery tube",
            "Test tubes × 4", "Burner" },
        ["midterm-chloroform"] = new[] {
            "Distilling flask", "Beaker 100 mL", "Test tubes × 3", "Burner" },
        ["final-benzamide"] = new[] {
            "Ice bucket", "Glass (stirring) rod", "Test tubes × 3",
            "Beaker 100 mL", "Burner" },
        ["final-winemaking"] = new[] {
            "Fermentation flask", "Delivery (bent) tube + stopper", "Cotton swab",
            "Test tube (limewater)", "Distilling flask", "Graduated cylinder 50 mL" },
    };

    /// Methane has no dynamic layout (hand-built stage) → reagents authored here.
    static readonly Dictionary<string, string[]> ReagentOverrides = new Dictionary<string, string[]>
    {
        ["tutorial-methane"] = new[] { "Sodium Acetate — 4 g", "Soda Lime — 4 g" },
    };

    [MenuItem("Tools/PharmaSynth/Generate Materials Guides")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Materials] exit Play mode first."); return; }

        var layouts = AssetDatabase.FindAssets("t:ExperimentLayout",
                new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" })
            .Select(g => AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(l => l != null).ToDictionary(l => l.moduleId, l => l);

        int filled = 0;
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentModuleDefinition",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(g));
            if (m == null || string.IsNullOrEmpty(m.moduleId)) continue;

            m.materialReagents = ReagentOverrides.TryGetValue(m.moduleId, out var over)
                ? over.ToList()
                : DeriveReagents(layouts.TryGetValue(m.moduleId, out var lay) ? lay : null);
            m.materialApparatus = Apparatus.TryGetValue(m.moduleId, out var app)
                ? app.ToList() : new List<string>();

            EditorUtility.SetDirty(m);
            filled++;
            Debug.Log($"[Materials] {m.moduleId}: {m.materialReagents.Count} reagents, "
                      + $"{m.materialApparatus.Count} apparatus");
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#4CD07D>[Materials] guides written for {filled} modules — "
                  + "shown at the top of the holo board via ChecklistPager.BuildMaterialsHeader.</color>");
    }

    /// Totals per chemical across ALL of a layout's bindings + seeded vessels.
    static List<string> DeriveReagents(ExperimentLayout layout)
    {
        var totals = new Dictionary<string, float>();
        if (layout != null)
        {
            foreach (var v in layout.vessels)
                foreach (var b in v.bindings)
                {
                    if (string.IsNullOrEmpty(b.reagentChemical)) continue;
                    totals.TryGetValue(b.reagentChemical, out float t);
                    // Only a TRUE zero ("any amount") counts as 1 — clamping every
                    // binding inflated the sub-gram solids (0.5 g aspirin ×2 read "2 g").
                    totals[b.reagentChemical] = t + (b.requiredMl > 0f ? b.requiredMl : 1f);
                }
        }
        var lines = new List<string>();
        foreach (var kv in totals.OrderByDescending(k => k.Value))
        {
            // Unit by the chemical's STATE, in the game's own 1-squeeze-=-1-ml system.
            var chem = FindChem(kv.Key);
            bool solid = chem != null && (chem.state == PhysicalState.Solid || chem.state == PhysicalState.Powder);
            lines.Add(kv.Key + " — " + kv.Value.ToString("0.#") + (solid ? " g" : " ml"));
        }
        return lines;
    }

    static ChemicalData FindChem(string name)
    {
        foreach (var g in AssetDatabase.FindAssets("t:ChemicalData",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Chemicals" }))
        {
            var c = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(g));
            if (c != null && c.chemicalName == name) return c;
        }
        return null;
    }
}
#endif
