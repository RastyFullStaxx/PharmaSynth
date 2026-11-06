#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builds a module's DynamicStage IN EDIT MODE and prints an inventory of every
/// object it spawned, grouped by WHO spawned it, so the stage can be reviewed and
/// pruned by hand.
///
/// ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only
/// exists at runtime, so there is no way to see what an experiment litters the bench
/// with until you are inside VR. This makes it visible and named.
///
/// The grouping matters because the spawn sources are independent, and only ONE of
/// them is the layout's fault:
///   • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate the bench)
///   • SpawnRackKit     — a rack + 6 tubes, EVERY module, predates the permanent bench
///   • SpawnSpares      — 2 spare beakers + a flask, EVERY module
///   • StageConsumables — matches + a "Striker" cube at Heat experiments. The cube is
///                        redundant: the matchbox itself is the striker.
/// Re-run after a rebuild; the builder clears the stage each time, so it is idempotent.
public static class RevealExperimentStage
{
    // One entry per module: a MenuItem can't take an argument, and defaulting to
    // whatever the runner happens to hold can reveal the wrong experiment (e.g.
    // tutorial-methane, which builds no dynamic stage at all, so the list is empty).
    // Ordered like the campaign.
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Prelim 1 — Chemical Compounding")]
    public static void RevealCompounding() => Reveal("prelim-chemical-compounding");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Prelim 2 — Ethyl Alcohol")]
    public static void RevealEthyl() => Reveal("prelim-ethyl-alcohol");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Midterm 1 — Benzoic Acid")]
    public static void RevealBenzoic() => Reveal("midterm-benzoic-acid");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Midterm 2 — Acetanilide")]
    public static void RevealAcetanilide() => Reveal("midterm-acetanilide");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Midterm 3 — Acetone")]
    public static void RevealAcetone() => Reveal("midterm-acetone");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Midterm 4 — Chloroform")]
    public static void RevealChloroform() => Reveal("midterm-chloroform");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Final 1 — Benzamide")]
    public static void RevealBenzamide() => Reveal("final-benzamide");
    [MenuItem("Tools/PharmaSynth/Reveal Stage/Final 2 — Wine Making")]
    public static void RevealWine() => Reveal("final-winemaking");

    static void Reveal(string moduleId)
    {
        if (Application.isPlaying) { Debug.LogWarning("[RevealStage] exit Play mode first."); return; }

        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        if (builder == null) { Debug.LogError("[RevealStage] no ExperimentSceneBuilder in the open scene."); return; }

        int roots = builder.Build(moduleId);

        // Take the FULLEST DynamicStage: Transform.Find returns the first match, and the
        // scene can hold two siblings (one orphaned + empty), so the first match may report
        // "0 objects" while 46 sit in the other. Stage() collapses duplicates, but stay
        // defensive: reporting an empty stage as "clean" is exactly the wrong answer.
        Transform stage = null;
        for (int i = 0; i < builder.transform.childCount; i++)
        {
            var c = builder.transform.GetChild(i);
            if (c.name == "DynamicStage" && (stage == null || c.childCount > stage.childCount)) stage = c;
        }
        if (stage == null) { Debug.LogWarning("[RevealStage] no DynamicStage after Build(" + moduleId + ")."); return; }

        var layout = builder.FindLayout(moduleId);
        var authored = new HashSet<string>();
        if (layout != null)
        {
            foreach (var v in layout.vessels) authored.Add("Vessel_" + v.prefabName);
            foreach (var p in layout.props) authored.Add("Prop_" + p.itemId);
            foreach (var s in layout.stations) authored.Add("Station_" + s.taskId);
        }

        var bySource = new Dictionary<string, List<string>>();
        void Add(string src, string name)
        {
            if (!bySource.TryGetValue(src, out var l)) bySource[src] = l = new List<string>();
            l.Add(name);
        }

        foreach (Transform t in stage)
        {
            string n = t.name;
            string src =
                  n == "RackKit"                              ? "SpawnRackKit (rack + 6 tubes — EVERY module)"
                : n.StartsWith("Spare_")                      ? "SpawnSpares (EVERY module)"
                : n == "MatchStriker"                         ? "StageConsumables — REDUNDANT: the matchbox is the striker"
                : n.StartsWith("Match") || n.Contains("Matchstick") ? "StageConsumables (matches)"
                : n.StartsWith("Station_")                    ? "LAYOUT — station"
                : n.StartsWith("Vessel_")                     ? "LAYOUT — vessel (duplicates the bench?)"
                : n.StartsWith("Prop_")                       ? "LAYOUT — prop"
                : n.StartsWith("Rack_")                       ? "LAYOUT — RackTaskGroup (logic only, no mesh)"
                : "other";
            Add(src, n);
        }

        var sb = new StringBuilder();
        sb.Append("<color=#4CD07D>[RevealStage] ").Append(moduleId)
          .Append(" — DynamicStage built in EDIT MODE: ").Append(stage.childCount)
          .Append(" objects (").Append(roots).Append(" layout roots). NOTHING deleted.</color>\n");
        foreach (var kv in bySource.OrderByDescending(k => k.Value.Count))
        {
            sb.Append("\n  ").Append(kv.Key).Append("  x").Append(kv.Value.Count).Append('\n');
            foreach (var n in kv.Value.OrderBy(x => x)) sb.Append("      ").Append(n).Append('\n');
        }
        sb.Append("\n  The bench ALREADY provides: Kit_TestTube_0-18, Kit_Hard-GlassTestTube_0-3,")
          .Append("\n  Eq_Dropper_1-4, Eq_PorcelainSpatula, Eq_Funnel/Funnel_2, Kit_BunsenBurner_0_9,")
          .Append("\n  Eq_Beaker_100mL/500mL, Eq_GlassRod, Eq_WashBottle, racks — anything above that")
          .Append("\n  repeats one of these is a duplicate and safe to delete by hand.");

        Debug.Log(sb.ToString());
        EditorSceneManager.MarkAllScenesDirty();
        EditorUtility.SetDirty(builder);
    }
}
#endif
