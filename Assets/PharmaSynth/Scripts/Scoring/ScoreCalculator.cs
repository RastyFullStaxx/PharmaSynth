using System;
using System.Collections.Generic;
using UnityEngine;

/// Per-experiment rubric weights (WCC lab manual rubric, plan §3.6).
/// Defaults sum to 1.0 but need not — ScoreCalculator normalizes, which handles
/// the manual's inconsistent printed weights (flagged for client sign-off).
[Serializable]
public class RubricWeights
{
    [Min(0f)] public float procedure = 0.40f;
    [Min(0f)] public float chemicalTests = 0.20f;
    [Min(0f)] public float materialsAndPPE = 0.15f;
    [Min(0f)] public float timeManagement = 0.10f;
    [Min(0f)] public float sanitation = 0.10f;
    [Min(0f)] public float documentation = 0.05f;

    public float Total() => procedure + chemicalTests + materialsAndPPE
                          + timeManagement + sanitation + documentation;

    public float WeightOf(RubricCategory c)
    {
        switch (c)
        {
            case RubricCategory.Procedure: return procedure;
            case RubricCategory.ChemicalTests: return chemicalTests;
            case RubricCategory.MaterialsAndPPE: return materialsAndPPE;
            case RubricCategory.TimeManagement: return timeManagement;
            case RubricCategory.Sanitation: return sanitation;
            case RubricCategory.Documentation: return documentation;
            default: return 0f;
        }
    }
}

/// Per-criterion contribution and the final grade, all in percent (0..100).
public struct GradeBreakdown
{
    public float Procedure;
    public float ChemicalTests;
    public float MaterialsAndPPE;
    public float TimeManagement;
    public float Sanitation;
    public float Documentation;
    public float Total;
}

/// Turns per-criterion sub-scores (each 0..1) into a weight-normalized grade %.
public class ScoreCalculator
{
    private readonly RubricWeights _w;

    public ScoreCalculator(RubricWeights weights)
    {
        _w = weights ?? new RubricWeights();
    }

    /// subScores: fraction earned per criterion (0..1). Missing criteria count as 0.
    /// Weights are normalized to sum to 1 so the grade is always a clean percentage.
    public GradeBreakdown Compute(IDictionary<RubricCategory, float> subScores)
    {
        float total = _w.Total();
        float norm = total > 0f ? total : 1f;

        float Contribution(RubricCategory c)
        {
            float sub = 0f;
            if (subScores != null && subScores.TryGetValue(c, out float v)) sub = Clamp01(v);
            return (_w.WeightOf(c) / norm) * sub * 100f;
        }

        var b = new GradeBreakdown
        {
            Procedure = Contribution(RubricCategory.Procedure),
            ChemicalTests = Contribution(RubricCategory.ChemicalTests),
            MaterialsAndPPE = Contribution(RubricCategory.MaterialsAndPPE),
            TimeManagement = Contribution(RubricCategory.TimeManagement),
            Sanitation = Contribution(RubricCategory.Sanitation),
            Documentation = Contribution(RubricCategory.Documentation)
        };
        b.Total = b.Procedure + b.ChemicalTests + b.MaterialsAndPPE
                + b.TimeManagement + b.Sanitation + b.Documentation;
        return b;
    }

    /// Time-management sub-score: full credit at or under par, decaying to 0 at 2x par.
    /// Full marks up to GraceFactor × par, then linear to 0 at ZeroFactor × par.
    /// Par is an experienced pace; a careful first-time VR player runs slower, and the
    /// old "0 at 2× par" cost the whole 10-point pass margin on its own.
    public const float GraceFactor = 1.5f, ZeroFactor = 3f;

    public static float TimeSubScore(float elapsedSeconds, float parSeconds)
    {
        if (parSeconds <= 0f) return 1f;
        float grace = parSeconds * GraceFactor, zero = parSeconds * ZeroFactor;
        if (elapsedSeconds <= grace) return 1f;
        return Clamp01(1f - (elapsedSeconds - grace) / (zero - grace));
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
