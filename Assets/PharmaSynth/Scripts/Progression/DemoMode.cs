using System;
using System.IO;
using UnityEngine;

/// Backend demo-mode switch: panelists need a fast run-through
/// with auto-complete controls, while Chapter-3 study participants must see the
/// untouched normal flow. A config FILE decides whether the cube-room menu even
/// shows the "Demo Mode" button; pressing that button starts a demo SESSION
/// (separate throwaway save, all periods unlocked, HUD auto-complete controls,
/// infinite reagents). Config off = zero footprint.
[Serializable]
public class DemoConfig
{
    public bool demoEnabled;              // shows the menu button
    public bool infiniteSupply = true;    // demo sessions refill starved bottles
}

/// Pure config resolution + demo save-path mapping (edit-mode testable).
public static class DemoMode
{
    private static DemoConfig _config;      // null until resolved (lazily or by the loader)

    /// The config file's verdict — gates only the BUTTON's visibility.
    public static bool IsEnabled { get { EnsureResolved(); return _config != null && _config.demoEnabled; } }

    /// Demo sessions top starved bottles back up instead of forcing a restart.
    public static bool InfiniteSupply { get { EnsureResolved(); return _config != null && _config.infiniteSupply; } }

    /// Installed by DemoModeLoader once the config files are read — authoritative
    /// (wins over the lazy fallback, e.g. on Android where StreamingAssets needs
    /// an async read the sync fallback can't do).
    public static void SetResolved(DemoConfig config) => _config = config ?? new DemoConfig();

    /// Lazy fallback so the button works in the Editor / on desktop WITHOUT the
    /// DemoModeLoader component in the scene: read the config synchronously the
    /// first time it's queried. StreamingAssets is a real folder in the Editor and
    /// standalone player (only Android needs the async loader). The persistentData
    /// override still wins when present.
    private static void EnsureResolved()
    {
        if (_config != null) return;
        string persistent = ReadOrNull(Path.Combine(Application.persistentDataPath, "demo-config.json"));
        string streaming = null;
        string streamingPath = Path.Combine(Application.streamingAssetsPath, "demo-config.json");
        if (!streamingPath.Contains("://")) streaming = ReadOrNull(streamingPath);   // Android → loader handles it
        _config = Resolve(persistent, streaming);
    }

    private static string ReadOrNull(string path)
    {
        try { return File.Exists(path) ? File.ReadAllText(path) : null; }
        catch { return null; }
    }

    /// The persistentDataPath override wins outright when present (it can also
    /// force-disable); otherwise the shipped StreamingAssets default applies;
    /// missing/malformed → disabled.
    public static DemoConfig Resolve(string persistentJson, string streamingJson)
        => TryParse(persistentJson) ?? TryParse(streamingJson) ?? new DemoConfig();

    private static DemoConfig TryParse(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonUtility.FromJson<DemoConfig>(json); }
        catch { return null; }
    }

    /// The ready-made end product each demo stage also spawns (in demo
    /// mode the ready-made products are present alongside the raw materials).
    public static string ProductFor(string moduleId)
    {
        switch (moduleId)
        {
            case "tutorial-methane": return "Sodium Acetate";        // gas product — show the feedstock
            // Exp 2 is an IDENTIFICATION lab — it synthesises nothing, and it needs ethanol as
            // a sample, so naming a product here would hide it.
            case "prelim-chemical-compounding": return null;
            case "prelim-ethyl-alcohol": return "Ethanol";
            case "midterm-benzoic-acid": return "Benzoic Acid";
            case "midterm-acetanilide": return "Acetanilide";
            case "midterm-acetone": return "Acetone";
            case "midterm-chloroform": return "Chloroform";
            case "final-benzamide": return "Benzamide";
            case "final-winemaking": return "Wine";
            default: return null;
        }
    }

    /// Chemicals that are SOME module's end product AND still exist on the bench,
    /// so `EndProductVisibility` can hide each one WHILE ITS OWN MODULE RUNS
    /// (a client rule: the player must synthesise them, not find them ready-made;
    /// a ready-made goal would lose the very meaning of the experiment).
    ///
    /// Only THREE remain, and only because each is also a manuscript-listed REAGENT
    /// for other experiments, so it cannot simply be deleted:
    ///     Ethanol      — goal of Exp 3, but a reagent in Exp 2 and Exp 6
    ///     Acetone      — goal of Exp 6, but a reagent in Exp 2 and Exp 7
    ///     Benzoic Acid — goal of Exp 4, and its own reference sample
    /// Exp 2 runs BEFORE Exp 3 and Exp 6, so hiding these globally would strip Exp 2 of
    /// reagents it cannot proceed without.
    ///
    /// The four PURE products — Acetanilide, Benzamide, Chloroform, Wine — are named as a
    /// reagent by no procedure (their own tests consume the player's synthesis), so they
    /// are not on the shelf at all. Aspirin is a plain raw reagent (Exp 2 §D hydrolyses
    /// it). Sodium Acetate and Mixed Fruit Juice are FEEDSTOCKS.
    ///
    /// NOTE this does not mean "hide whenever not in demo": see EndProductVisibility,
    /// which gates on the RUNNING module's own product via ProductFor().
    public static bool IsEndProduct(string chemicalName)
    {
        switch (chemicalName)
        {
            case "Ethanol":
            case "Benzoic Acid":
            case "Acetone":
                return true;
            default:
                return false;
        }
    }

    /// Demo sessions persist to their own file so panel demos never pollute the
    /// real progression ("pharmasynth_progress.json" → "pharmasynth_progress_demo.json").
    public static string SavePathFor(bool demoActive, string normalPath)
    {
        if (!demoActive || string.IsNullOrEmpty(normalPath)) return normalPath;
        int dot = normalPath.LastIndexOf('.');
        return dot < 0 ? normalPath + "_demo"
                       : normalPath.Substring(0, dot) + "_demo" + normalPath.Substring(dot);
    }
}

/// Whether THIS play session was entered through the Demo Mode button.
/// (Static so it survives the menu→lab scene load, like GameFlow.SelectedModuleId.)
public static class DemoSession
{
    public static bool Active;
}
