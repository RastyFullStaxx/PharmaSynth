#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Injects each experiment's ILO beats into its Intro cutscene
/// (Pharmee states the learning outcomes in the opening dialogue). Beats slot in
/// after the greeting beat: a lead-in, then one beat per objective (verbatim
/// Appendix C copy from IloCopy). Idempotent — the lead-in text is the marker.
public static class IloBeatInjector
{
    const string Dir = "Assets/PharmaSynth/ScriptableObjects/Cutscenes/";

    /// moduleId → cutscene asset prefix.
    public static readonly (string moduleId, string prefix)[] Modules =
    {
        ("tutorial-methane", "Methane"),
        ("prelim-chemical-compounding", "ChemicalCompounding"),
        ("prelim-ethyl-alcohol", "EthylAlcohol"),
        ("midterm-benzoic-acid", "BenzoicAcid"),
        ("midterm-acetanilide", "Acetanilide"),
        ("midterm-acetone", "Acetone"),
        ("midterm-chloroform", "Chloroform"),
        ("final-benzamide", "Benzamide"),
        ("final-winemaking", "WineMaking"),
    };

    [MenuItem("Tools/PharmaSynth/Inject ILO Beats")]
    public static void Inject()
    {
        int injected = 0, skipped = 0, missing = 0;
        foreach (var (moduleId, prefix) in Modules)
        {
            var data = AssetDatabase.LoadAssetAtPath<CutsceneData>(Dir + prefix + "_Intro.asset");
            if (data == null) { missing++; Debug.LogWarning("[IloBeatInjector] missing " + prefix + "_Intro.asset"); continue; }
            int added = InjectInto(data, moduleId);
            if (added > 0) { EditorUtility.SetDirty(data); injected++; }
            else skipped++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[IloBeatInjector] ILO beats: {injected} intros injected, {skipped} already had them, {missing} missing.");
    }

    /// Insert the lead-in + one beat per ILO after the first (greeting) beat.
    /// Returns beats added (0 = already injected / no ILO copy). Pure enough to
    /// self-test on a temporary CutsceneData instance.
    public static int InjectInto(CutsceneData data, string moduleId)
    {
        if (data == null || data.beats == null) return 0;
        foreach (var b in data.beats)
            if (b != null && b.subtitle == IloCopy.LeadIn) return 0;   // idempotent
        var ilos = IloCopy.ForModule(moduleId);
        if (ilos.Length == 0) return 0;

        int at = data.beats.Count > 0 ? 1 : 0;   // after the greeting beat
        data.beats.Insert(at, new CutsceneData.Beat
        {
            subtitle = IloCopy.LeadIn,
            seconds = 2.8f,
            face = PharmeeFaceExpression.Happy,
        });
        for (int i = 0; i < ilos.Length; i++)
        {
            data.beats.Insert(at + 1 + i, new CutsceneData.Beat
            {
                subtitle = ilos[i],
                seconds = IloCopy.BeatSeconds(ilos[i]),
                face = PharmeeFaceExpression.Neutral,
            });
        }
        return 1 + ilos.Length;
    }
}
#endif
