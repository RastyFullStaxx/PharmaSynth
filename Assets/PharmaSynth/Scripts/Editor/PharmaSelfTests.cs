#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Re-runnable regression suite for the PharmaSynth engine. Run via
/// menu: Tools ▸ PharmaSynth ▸ Run Self-Tests. One permanent, one-click check.
/// (An Editor-menu suite rather than an NUnit asmdef, to avoid restructuring
/// the runtime assembly; a formal EditMode asmdef migration can layer on later.)
/// This file holds the runner and shared helpers; each area's suites live in
/// PharmaSelfTests.<Area>.cs. A new suite needs a call in Run().
public static partial class PharmaSelfTests
{
    static int _fail, _total;
    static readonly List<string> _log = new List<string>();

    static void A(string name, bool cond)
    {
        _total++;
        if (!cond) { _fail++; _log.Add("FAIL  " + name); }
    }
    static bool Near(float a, float b, float e = 0.01f) => Math.Abs(a - b) < e;

    [MenuItem("Tools/PharmaSynth/Run Self-Tests")]
    public static void Run()
    {
        _fail = 0; _total = 0; _log.Clear();
        NPCNarrationController.ClearFloor();   // statics survive the domain; see ClearFloor

        TaskGraphSuite();
        MasterySuite();
        ScoreSuite();
        GraderSuite();
        RunnerSuite();
        ProgressionSuite();
        ChemVisualSuite();
        UISuite();
        ReactionRulesSuite();
        InteractionSuite();
        RealVerbSuite();
        PourReactionSuite();
        PourGuardSuite();
        FeedbackSuite();
        VerbSuite();
        LayoutGraphCoverageSuite();
        VerbWiringSuite();
        LayoutSpacingSuite();
        WorkspaceShelfSuite();
        FlowSmoothnessSuite();
        ManuscriptFixSuite();
        TestReactionSuite();
        SceneBuilderSuite();
        SimRigSuite();
        PostLabSuite();
        CutsceneLibrarySuite();
        ProgressionFlowSuite();
        HubSelectSuite();
        AudioSuite();
        ExaminerSuite();
        SettingsSuite();
        ResultsExportSuite();
        RecorderSuite();
        HudRigSuite();
        FadeSuite();
        ArmedRunnerSuite();
        GatekeeperSuite();
        UnlockReturnSuite();
        LabMenuSuite();
        DepletionSuite();
        RealSizeSuite();
        PhysicsProfileSuite();
        TestVesselSuite();
        MishandlingSuite();
        ComfortSuite();
        PharmeeAliveSuite();
        LibrarySuite();
        ContentSuite();
        RosterDataSuite();
        HoverInfoSuite();
        GlyphSafeSuite();
        MusicSpeakerSuite();
        SurfaceSuite();
        PharmeeGestureSuite();
        GrabTuningSuite();
        HeightCalibrationSuite();
        WatchMathSuite();
        ShelfPourWiringSuite();
        FxMaterialSuite();
        VisualSweepSuite();
        PharmeePolishSuite();
        ChecklistPagerSuite();
        CinemaSuite();
        ForgivenessSuite();
        DemoModeSuite();
        DemoActionsSuite();
        IloCopySuite();
        ReviewFlowSuite();
        HazardousMixSuite();
        CenterTableMathSuite();
        RawReagentSuite();
        VoiceSuite();
        DispenserSuite();
        EdgeCaseSuite();

        string summary = $"PharmaSynth Self-Tests: {_total - _fail}/{_total} passed";
        if (_fail == 0) Debug.Log("<color=#4CD07D>" + summary + " — ALL GREEN</color>");
        else Debug.LogError(summary + " — " + _fail + " FAILED:\n" + string.Join("\n", _log));

        // Cheap verification channel: sessions read
        // this one-liner instead of wrapping the run in a RunCommand capture.
        // Logs/ not Temp/ — Unity wipes Temp at startup.
        try
        {
            System.IO.Directory.CreateDirectory("Logs");
            System.IO.File.WriteAllText("Logs/selftest-result.txt",
                summary + (_fail == 0 ? " — ALL GREEN" : " — " + _fail + " FAILED:\n" + string.Join("\n", _log))
                + "\n(" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ")\n");
        }
        catch { /* a locked file must never fail the suite */ }
    }

    static ExperimentTask T(string id, TaskPhase ph, float w, LabSkill sk, RubricCategory rc, params string[] pre)
    {
        var t = new ExperimentTask { taskId = id, label = id, phase = ph, progressWeight = w, skill = sk, rubricCategory = rc, required = true };
        t.prerequisites = new List<string>(pre);
        return t;
    }

    static ChemicalData LoadChem(string file)
        => AssetDatabase.LoadAssetAtPath<ChemicalData>("Assets/PharmaSynth/ScriptableObjects/Chemicals/" + file + ".asset");

    /// itemIds of the PERMANENT bench — the Eq_*/Kit_*/Raw_* apparatus that lives in
    /// the SCENE, not in any layout, because the hard client rule keeps every general
    /// tool out at all times. A station may point at any of these instead of staging
    /// its own copy. Scanned from the open scene so it can never drift from reality.
    static HashSet<string> BenchItemIds
    {
        get
        {
            var ids = new HashSet<string>();
            foreach (var li in UnityEngine.Object.FindObjectsByType<LabItem>(
                         FindObjectsInactive.Include))
                if (li != null && !string.IsNullOrEmpty(li.itemId)) ids.Add(li.itemId);
            return ids;
        }
    }
}
#endif
