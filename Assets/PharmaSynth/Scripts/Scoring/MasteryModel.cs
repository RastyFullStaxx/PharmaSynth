using System;
using System.Collections.Generic;
using UnityEngine;

/// Bayesian Knowledge Tracing parameters (per module, tunable in the inspector).
/// Defaults from plan §3.6.
[Serializable]
public class BktParameters
{
    [Range(0f, 1f), Tooltip("Prior probability the skill is already learned.")]
    public float pL0 = 0.25f;
    // 0.20 → 0.30: at 0.20 a skill needed THREE clean observations to
    // clear the 0.90 gate (1 obs .68 / 2 .92 / 3 .99) and ONE mistake knocked it
    // back to ~.68 with little room to recover inside an attempt — so a player who
    // earned ≥90 on the visible rubric could still be failed by the invisible
    // mastery gate. At 0.30 two clean observations reach .944 and a mid-run mistake
    // recovers to ~.958 with one more correct step. The 0.90 GATE is untouched.
    [Range(0f, 1f), Tooltip("Probability of learning the skill after an opportunity.")]
    public float pTransit = 0.30f;
    [Range(0f, 1f), Tooltip("Probability of slipping (wrong despite knowing).")]
    public float pSlip = 0.10f;
    [Range(0f, 1f), Tooltip("Probability of guessing (right without knowing).")]
    public float pGuess = 0.20f;
}

/// Bayesian Knowledge Tracing mastery estimator (plan §3.6, Logic Tier).
/// Tracks P(learned) per LabSkill; the 90% gate reads OverallMastery().
///
/// Plain C# so the BKT math is unit-testable. One instance per experiment attempt.
public class MasteryModel
{
    private readonly BktParameters _p;
    private readonly Dictionary<LabSkill, float> _pL = new Dictionary<LabSkill, float>();
    private readonly List<LabSkill> _skills = new List<LabSkill>();

    public MasteryModel(BktParameters parameters, IEnumerable<LabSkill> trackedSkills)
    {
        _p = parameters ?? new BktParameters();
        if (trackedSkills != null)
        {
            foreach (var s in trackedSkills)
            {
                if (_pL.ContainsKey(s)) continue;
                _skills.Add(s);
                _pL[s] = Clamp01(_p.pL0);
            }
        }
    }

    public IReadOnlyList<LabSkill> TrackedSkills => _skills;

    /// Update the estimate for a skill from one correct/incorrect observation.
    public void Observe(LabSkill skill, bool correct)
    {
        if (!_pL.TryGetValue(skill, out float prior))
        {
            // Track a skill first seen at runtime.
            _skills.Add(skill);
            prior = Clamp01(_p.pL0);
        }

        // Posterior P(learned | evidence) via Bayes.
        float posterior;
        if (correct)
        {
            float num = prior * (1f - _p.pSlip);
            float den = num + (1f - prior) * _p.pGuess;
            posterior = den > 0f ? num / den : prior;
        }
        else
        {
            float num = prior * _p.pSlip;
            float den = num + (1f - prior) * (1f - _p.pGuess);
            posterior = den > 0f ? num / den : prior;
        }

        // Account for learning during the opportunity.
        float updated = posterior + (1f - posterior) * _p.pTransit;
        _pL[skill] = Clamp01(updated);
    }

    public bool IsTracked(LabSkill skill) => _pL.ContainsKey(skill);

    public float GetMastery(LabSkill skill) => _pL.TryGetValue(skill, out float v) ? v : Clamp01(_p.pL0);

    /// Mean P(learned) across tracked skills — the value compared to the mastery gate.
    public float OverallMastery()
    {
        if (_skills.Count == 0) return 0f;
        float sum = 0f;
        foreach (var s in _skills) sum += _pL[s];
        return sum / _skills.Count;
    }

    public bool IsMastered(float threshold) => OverallMastery() >= threshold;

    public void Reset()
    {
        foreach (var s in _skills) _pL[s] = Clamp01(_p.pL0);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}
