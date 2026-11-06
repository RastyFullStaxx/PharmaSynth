using System.Collections.Generic;

/// The four progression periods the experiments are grouped into (plan §3.2).
/// Period doors open in order once the previous period is fully passed.
public enum ExperimentPeriod
{
    Tutorial,
    Prelim,
    Midterm,
    Final
}

/// One roster entry: its module id (must match the ExperimentModuleDefinition asset's
/// moduleId), the asset file name, its period, the single prerequisite that gates it,
/// and the build tier (1–3, informational).
public class CatalogEntry
{
    public string moduleId;
    public string assetName;   // file under ScriptableObjects/Experiments/ (no extension)
    public string title;
    public ExperimentPeriod period;
    public string prerequisiteModuleId;   // null/empty = always unlocked
    public int tier;

    public CatalogEntry(string id, string asset, string title, ExperimentPeriod period, string prereq, int tier)
    { this.moduleId = id; this.assetName = asset; this.title = title; this.period = period; this.prerequisiteModuleId = prereq; this.tier = tier; }
}

/// The ordered 9-experiment roster — the client's period grouping, which is
/// exactly the manuscript's 8 bench labs (Exp 2-9) plus the methane tutorial. (Manuscript
/// Exp 1, Stoichiometry, is a pen-and-paper calc exercise, so it has no bench module.)
/// Aspirin + Caffeine were game-authored, are absent from the grouping, and were dropped
/// — Aspirin survives as a RAW REAGENT because Exp 2 §D hydrolyses it.
/// Linear mastery chain: each experiment
/// unlocks the next once its two-part 90% gate is cleared. Kept as plain data so the
/// menu/hub/experiment-select and ProgressionFlow can drive off one source of truth,
/// and so it is unit-testable without a scene.
public static class ExperimentCatalog
{
    public static readonly IReadOnlyList<CatalogEntry> Entries = new List<CatalogEntry>
    {
        new CatalogEntry("tutorial-methane",           "Tutorial_Methane",            "Tutorial: Methane Synthesis",   ExperimentPeriod.Tutorial, null,                         1),
        new CatalogEntry("prelim-chemical-compounding","Prelim_ChemicalCompounding",  "Prelim: Chemical Compounding",  ExperimentPeriod.Prelim,   "tutorial-methane",            1),
        new CatalogEntry("prelim-ethyl-alcohol",       "Prelim_EthylAlcohol",         "Prelim: Ethyl Alcohol",         ExperimentPeriod.Prelim,   "prelim-chemical-compounding", 1),
        new CatalogEntry("midterm-benzoic-acid",       "Midterm_BenzoicAcid",         "Midterm: Benzoic Acid",         ExperimentPeriod.Midterm,  "prelim-ethyl-alcohol",        1),
        new CatalogEntry("midterm-acetanilide",        "Midterm_Acetanilide",         "Midterm: Acetanilide",          ExperimentPeriod.Midterm,  "midterm-benzoic-acid",        2),
        new CatalogEntry("midterm-acetone",            "Midterm_Acetone",             "Midterm: Acetone",              ExperimentPeriod.Midterm,  "midterm-acetanilide",         2),
        new CatalogEntry("midterm-chloroform",         "Midterm_Chloroform",          "Midterm: Chloroform",           ExperimentPeriod.Midterm,  "midterm-acetone",             2),
        new CatalogEntry("final-benzamide",            "Final_Benzamide",             "Final: Benzamide",              ExperimentPeriod.Final,    "midterm-chloroform",          2),
        new CatalogEntry("final-winemaking",           "Final_WineMaking",            "Final: Wine Making",            ExperimentPeriod.Final,    "final-benzamide",             2),
    };

    public static CatalogEntry Get(string moduleId)
    {
        foreach (var e in Entries) if (e.moduleId == moduleId) return e;
        return null;
    }

    public static string PrerequisiteOf(string moduleId) => Get(moduleId)?.prerequisiteModuleId;

    public static IEnumerable<CatalogEntry> InPeriod(ExperimentPeriod period)
    {
        foreach (var e in Entries) if (e.period == period) yield return e;
    }

    public static int Count => Entries.Count;
}
