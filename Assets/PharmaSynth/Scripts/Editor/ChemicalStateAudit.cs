#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// Reagent-nature audit: checks whether each reagent is really a liquid or
/// a solid that has to be scooped. Dumps every
/// ChemicalData's state/flags to Temp/chemical-state-audit.md and flags
/// chemicals that are solids in their common lab form but are marked Liquid
/// (manuscript solutions like "10% NaOH" are correctly liquid — only pure
/// solids that get weighed/scooped are suspects).
public static class ChemicalStateAudit
{
    /// Pure solids in the form the manuscript handles them (weighed/scooped),
    /// matched by substring against chemicalName. Solutions stay liquid.
    static readonly string[] SolidHints =
    {
        "Sodium Acetate", "Calcium Acetate", "Soda Lime", "Salicylic",
        "Benzoic Acid", "Acetanilide", "Benzamide", "Aspirin",
        "Urea",   // NOT KMnO4 or Sodium Nitrite: the manuscript only ever uses their SOLUTIONS (0.1% / 10%, added by the ml)
        "Iodine Crystals", "Yeast",
    };

    [MenuItem("Tools/PharmaSynth/Audit Chemical States")]
    public static void Run()
    {
        var sb = new StringBuilder("# Chemical state audit (W5.12)\n\n");
        sb.Append("| Chemical | State | Suspect | Hazard | Boil °C |\n|---|---|---|---|---|\n");
        int total = 0, suspects = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:ChemicalData"))
        {
            var chem = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(guid));
            if (chem == null) continue;
            total++;
            bool solidByNature = false;
            foreach (var hint in SolidHints)
                if (chem.chemicalName != null && chem.chemicalName.Contains(hint)) { solidByNature = true; break; }
            bool suspect = solidByNature && chem.state == PhysicalState.Liquid;
            if (suspect) suspects++;
            sb.Append("| ").Append(chem.chemicalName)
              .Append(" | ").Append(chem.state)
              .Append(" | ").Append(suspect ? "**SOLID by nature?**" : "")
              .Append(" | ").Append(chem.hazard)
              .Append(" | ").Append(chem.boilingPointC)
              .Append(" |\n");
        }
        System.IO.Directory.CreateDirectory("Temp");
        System.IO.File.WriteAllText("Temp/chemical-state-audit.md", sb.ToString());
        Debug.Log($"[ChemAudit] {total} chemicals scanned, {suspects} suspect state(s) — Temp/chemical-state-audit.md");
    }
}
#endif
