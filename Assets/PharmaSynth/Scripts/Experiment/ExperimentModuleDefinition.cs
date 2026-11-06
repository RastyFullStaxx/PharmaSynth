using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ExperimentModule", menuName = "VR ChemLab/Experiment Module")]
public class ExperimentModuleDefinition : ScriptableObject
{
    [Header("Module")]
    public string moduleId = "module-1";
    public string moduleTitle = "Chemical Experiment";

    [Header("Intended Learning Outcomes")]
    [TextArea(2, 5)]
    public List<string> intendedLearningOutcomes = new List<string>();

    [Header("Materials guide (watch panel header — NOT part of the checklist)")]
    [Tooltip("Display-ready lines shown at the top of the holo board so the player can gather everything BEFORE starting (user 2026-07-17). Reagents are GENERATED from the layout's bindings (Tools ▸ PharmaSynth ▸ Generate Materials Guides) so they can never drift from what the experiment actually consumes; apparatus is authored from the PROCEDURE (the manuscript's apparatus lists are defective).")]
    public List<string> materialReagents = new List<string>();
    public List<string> materialApparatus = new List<string>();

    // ⚠ ORPHANED DATA. Nothing reads it.
    // Kept because the field is SERIALIZED into all 9 module assets and removing it
    // would rewrite their on-disk data for no gain; `graphTasks` below is what the
    // game actually runs. Do not author into it, and do not wire anything to it.
    [Header("Task Checklist (ORPHANED — nothing reads this; use graphTasks)")]
    public List<ExperimentTaskDefinition> tasks = new List<ExperimentTaskDefinition>();

    // ---- TaskGraph v2 (plan §4.2) ----------------------------------------

    [Header("TaskGraph (phases A–D, prerequisites, weights)")]
    public List<ExperimentTask> graphTasks = new List<ExperimentTask>();

    [Header("Assessment")]
    [Tooltip("Assessment mode (plan §3.4): Pharmee gives no procedural hints and Dr. Jimenez observes. Off by default — the tutorial + early experiments stay guided.")]
    public bool assessmentMode = false;

    [Header("Mastery (Bayesian Knowledge Tracing)")]
    [Range(0f, 1f), Tooltip("Grade+mastery gate to unlock the next experiment (manuscript = 0.90).")]
    public float masteryThreshold = 0.90f;
    public BktParameters bkt = new BktParameters();
    [Tooltip("Skills tracked for this experiment; leave empty to auto-derive from graphTasks.")]
    public List<LabSkill> trackedSkills = new List<LabSkill>();

    [Header("Scoring")]
    public RubricWeights rubricWeights = new RubricWeights();
    [Min(0f), Tooltip("Par time (seconds) for the time-management rubric criterion.")]
    public float parTimeSeconds = 600f;

    /// Build a fresh runtime TaskGraph from this module's authored tasks.
    public TaskGraph BuildTaskGraph() => new TaskGraph(graphTasks);

    /// Build a fresh BKT model. Uses trackedSkills if set, else the distinct
    /// skills referenced by graphTasks.
    public MasteryModel BuildMasteryModel()
    {
        List<LabSkill> skills = trackedSkills;
        if (skills == null || skills.Count == 0)
        {
            skills = new List<LabSkill>();
            foreach (var t in graphTasks)
                if (t != null && !skills.Contains(t.skill)) skills.Add(t.skill);
        }
        return new MasteryModel(bkt, skills);
    }

    public ScoreCalculator BuildScoreCalculator() => new ScoreCalculator(rubricWeights);
}

[Serializable]
public class ExperimentTaskDefinition
{
    public string taskId = "task-id";
    public string taskLabel = "Task";
    public int scoreValue = 10;
    public bool requiredForCompletion = true;
}
