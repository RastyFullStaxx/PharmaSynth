#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Manuscript re-verification fixes (idempotent, one menu):
///   M1  Benzoic Acid ester test needs an alcohol (benzoic acid + sulfuric acid
///       alone is chemically INERT) → propyl alcohol staged + bound,
///       rule pointed at it (manuscript/test intent: ester with propyl).
///   M2  Chloroform gets the manuscript's oxidation confirmatory test
///       (K-dichromate + conc H2SO4, procedure L3419-21 + results sheet) →
///       new task + reagent + reaction rule.
///   M3  Wine Making ferments non-grape juice, per the manuscript's explicit
///       grape exclusion (L3830-31) → Chem_GrapeJuice renamed "Mixed Fruit
///       Juice" (GUID refs untouched; string lookups updated).
///   M4  Reagent fidelity: Acetanilide prep-HCl 0.1N (not 6N); iodoform tests get
///       their KI (Ethyl Alcohol + Acetone); ester/acid tests use the
///       manuscript's DILUTED acids, with the matching RULE inputs re-pointed
///       (rules match by asset; a layout-only swap would kill the reactions).
///   M5a Chemical Compounding quiz Q3 uses the manuscript's KMnO4 oxidation
///       (a bromine unsaturation question does not fit an all-saturated module).
/// Run AFTER: nothing. Run BEFORE: Tidy Experiment Layouts (new props → slots).
public static class W59ManuscriptDataApplier
{
    const string LayoutDir = "Assets/PharmaSynth/ScriptableObjects/Layouts/";
    const string ChemDir = "Assets/PharmaSynth/ScriptableObjects/Chemicals/";
    const string RxDir = "Assets/PharmaSynth/ScriptableObjects/Reactions/";
    const string ExpDir = "Assets/PharmaSynth/ScriptableObjects/Experiments/";

    [MenuItem("Tools/PharmaSynth/Apply W5.9 Manuscript Data")]
    public static void Apply()
    {
        if (Application.isPlaying) { Debug.LogWarning("[W59] exit Play mode first."); return; }
        int changes = 0;
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(RxDir + "MasterReactionRegistry.asset");

        ChemicalData Chem(string file) => AssetDatabase.LoadAssetAtPath<ChemicalData>(ChemDir + file + ".asset");
        ExperimentLayout Layout(string file) => AssetDatabase.LoadAssetAtPath<ExperimentLayout>(LayoutDir + file + ".asset");

        var propyl = Chem("Chem_PropylAlcohol");
        var dichromate = Chem("Chem_PotassiumDichromate");
        var chloroform = Chem("Chem_Chloroform");
        var dilAcetic = Chem("Chem_DilutedAceticAcid");
        var dilHcl = Chem("Chem_DilutedHydrochloricAcid");
        var hcl01 = Chem("Chem_HydrochloricAcid01N");
        var ki = Chem("Chem_PotassiumIodide10");

        // Library must resolve everything the layouts will now pour.
        foreach (var c in new[] { propyl, dichromate, dilAcetic, dilHcl, hcl01, ki })
            if (c != null && lib != null && !lib.chemicals.Contains(c)) { lib.chemicals.Add(c); changes++; }

        // ---- M1: Benzoic Acid ester test --------------------------------------
        var esterRule = AssetDatabase.LoadAssetAtPath<ReactionRule>(RxDir + "Test_BenzoateEster.asset");
        if (esterRule != null && propyl != null && esterRule.inputChemicalB != propyl)
        {
            esterRule.inputChemicalB = propyl;   // not Sulfuric Acid: no ester possible
            esterRule.expectedObservation = "Sweet aromatic ester odour — benzoic acid + propyl alcohol (drop of H2SO4) on warming.";
            EditorUtility.SetDirty(esterRule);
            changes++;
        }
        var benzoic = Layout("Layout_BenzoicAcid");
        if (benzoic != null)
        {
            changes += EnsureProp(benzoic, "Vial_Brown", "test-ester-alcohol", "Propyl Alcohol", true, "Propyl Alcohol");
            changes += RemoveMisplacedBinding(benzoic, "Propyl Alcohol", "test-ester");
            changes += EnsureBinding(benzoic, "Propyl Alcohol", "test-ester", 50f);
            EditorUtility.SetDirty(benzoic);
        }

        // ---- M2: Chloroform oxidation test ------------------------------------
        var chloroModule = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(ExpDir + "Midterm_Chloroform.asset");
        if (chloroModule != null && chloroModule.graphTasks != null)
        {
            bool has = false;
            foreach (var t in chloroModule.graphTasks) if (t.taskId == "test-oxidation") has = true;
            if (!has)
            {
                int insertAt = 0;
                for (int i = 0; i < chloroModule.graphTasks.Count; i++)
                    if (chloroModule.graphTasks[i].taskId == "test-flammability") insertAt = i + 1;
                var task = new ExperimentTask
                {
                    taskId = "test-oxidation",
                    label = "Oxidation test (dichromate + H2SO4)",
                    phase = TaskPhase.ChemicalTests,
                    progressWeight = 1,
                    skill = LabSkill.TestInterpretation,
                    rubricCategory = RubricCategory.ChemicalTests,
                    required = true,
                    hint = "Warm chloroform with potassium dichromate and conc. H2SO4 — the pungent phosgene-like odour confirms it. Fume hood!",
                    prerequisites = new List<string> { "dry-redistil" },
                };
                chloroModule.graphTasks.Insert(insertAt, task);
                foreach (var t in chloroModule.graphTasks)
                    if (t.taskId == "record-yield" && !t.prerequisites.Contains("test-oxidation"))
                        t.prerequisites.Add("test-oxidation");
                EditorUtility.SetDirty(chloroModule);
                changes++;
            }
        }
        var chloroLayout = Layout("Layout_Chloroform");
        if (chloroLayout != null)
        {
            changes += EnsureProp(chloroLayout, "Vial_Brown", "test-oxidation", "Potassium Dichromate", true, "Potassium Dichromate");
            changes += RemoveMisplacedBinding(chloroLayout, "Potassium Dichromate", "test-oxidation");
            changes += EnsureBinding(chloroLayout, "Potassium Dichromate", "test-oxidation", 50f);
            EditorUtility.SetDirty(chloroLayout);
        }
        var oxRulePath = RxDir + "Test_ChloroformOxidation.asset";
        var oxRule = AssetDatabase.LoadAssetAtPath<ReactionRule>(oxRulePath);
        if (oxRule == null && chloroform != null && dichromate != null)
        {
            oxRule = ScriptableObject.CreateInstance<ReactionRule>();
            oxRule.inputChemicalA = chloroform;
            oxRule.inputChemicalB = dichromate;
            oxRule.resultLiquid = null;   // contents stay chloroform; the odour is the observation
            oxRule.outcome = (ReactionOutcome)4;   // odour/colour cue (same class as the ester tests)
            oxRule.expectedObservation = "Pungent, suffocating odour (phosgene-like) — oxidation of chloroform confirmed. Fume hood!";
            AssetDatabase.CreateAsset(oxRule, oxRulePath);
            changes++;
        }
        if (oxRule != null && registry != null && !registry.rules.Contains(oxRule))
        {
            registry.rules.Add(oxRule);
            EditorUtility.SetDirty(registry);
            changes++;
        }

        // ---- M3: grape → mixed fruit juice ------------------------------------
        var juice = Chem("Chem_GrapeJuice");
        if (juice != null && juice.chemicalName != "Mixed Fruit Juice")
        {
            juice.chemicalName = "Mixed Fruit Juice";   // manuscript excludes grapes (L3830-31)
            EditorUtility.SetDirty(juice);
            changes++;
        }
        var wine = Layout("Layout_WineMaking");
        if (wine != null)
        {
            foreach (var v in wine.vessels)
                if (v.startChemical == "Grape Juice") { v.startChemical = "Mixed Fruit Juice"; changes++; }
            foreach (var p in wine.props)
                if (p.fillChemical == "Grape Juice") { p.fillChemical = "Mixed Fruit Juice"; changes++; }
            EditorUtility.SetDirty(wine);
        }

        // ---- M4a: Acetanilide prep-HCl 0.1N ------------------------------------
        var acetanilide = Layout("Layout_Acetanilide");
        if (acetanilide != null)
        {
            foreach (var p in acetanilide.props)
                if (p.itemId == "prep-hcl" && p.fillChemical == "Hydrochloric Acid 6N")
                { p.fillChemical = "Hydrochloric Acid 0.1N"; changes++; }
            foreach (var v in acetanilide.vessels)
                foreach (var b in v.bindings)
                    if (b.taskId == "prep-hcl" && b.reagentChemical == "Hydrochloric Acid 6N")
                    { b.reagentChemical = "Hydrochloric Acid 0.1N"; changes++; }
            EditorUtility.SetDirty(acetanilide);
        }

        // ---- M4b: iodoform KI (Ethyl Alcohol + Acetone) ------------------------
        foreach (var name in new[] { "Layout_EthylAlcohol", "Layout_Acetone" })
        {
            var lay = Layout(name);
            if (lay == null) continue;
            changes += EnsureProp(lay, "Vial_Brown", "test-iodoform-ki", "Potassium Iodide 10%", true, "Potassium Iodide 10%");
            changes += EnsureBinding(lay, "Potassium Iodide 10%", "test-iodoform", 50f);
            EditorUtility.SetDirty(lay);
        }

        // ---- M4c: diluted acids (layout + RULE inputs together) ----------------
        var ethyl = Layout("Layout_EthylAlcohol");
        if (ethyl != null && dilAcetic != null)
        {
            foreach (var p in ethyl.props)
                if (p.itemId == "test-ester" && p.fillChemical == "Glacial Acetic Acid")
                { p.fillChemical = "Diluted Acetic Acid"; changes++; }
            foreach (var v in ethyl.vessels)
                foreach (var b in v.bindings)
                    if (b.taskId == "test-ester" && b.reagentChemical == "Glacial Acetic Acid")
                    { b.reagentChemical = "Diluted Acetic Acid"; changes++; }
            EditorUtility.SetDirty(ethyl);
            var ester = AssetDatabase.LoadAssetAtPath<ReactionRule>(RxDir + "EsterFormation.asset");
            if (ester != null && ester.inputChemicalB != dilAcetic
                && ester.inputChemicalB == Chem("Chem_GlacialAceticAcid"))
            {
                ester.inputChemicalB = dilAcetic;
                EditorUtility.SetDirty(ester);
                changes++;
            }
        }
        var benzamide = Layout("Layout_Benzamide");
        if (benzamide != null && dilHcl != null)
        {
            foreach (var p in benzamide.props)
                if (p.itemId == "test-acid" && p.fillChemical == "Hydrochloric Acid 6N")
                { p.fillChemical = "Diluted Hydrochloric Acid"; changes++; }
            foreach (var v in benzamide.vessels)
                foreach (var b in v.bindings)
                    if (b.taskId == "test-acid" && b.reagentChemical == "Hydrochloric Acid 6N")
                    { b.reagentChemical = "Diluted Hydrochloric Acid"; changes++; }
            EditorUtility.SetDirty(benzamide);
            var acidRule = AssetDatabase.LoadAssetAtPath<ReactionRule>(RxDir + "Test_BenzamideAcid.asset");
            if (acidRule != null && acidRule.inputChemicalB == Chem("Chem_HydrochloricAcid6N"))
            {
                acidRule.inputChemicalB = dilHcl;
                EditorUtility.SetDirty(acidRule);
                changes++;
            }
        }

        // ---- M5a: Chemical Compounding quiz Q3 ---------------------------------
        var quiz = AssetDatabase.LoadAssetAtPath<QuizBank>("Assets/PharmaSynth/ScriptableObjects/Quizzes/Quiz_ChemicalCompounding.asset");
        if (quiz != null && quiz.questions != null && quiz.questions.Count >= 3
            && quiz.questions[2].prompt.Contains("unsaturation"))
        {
            quiz.questions[2] = new QuizQuestion
            {
                prompt = "What shows that potassium permanganate has OXIDISED an alcohol?",
                options = new List<string> { "The purple colour fades / turns brown", "The mixture freezes", "White smoke appears", "Nothing changes" },
                correctIndex = 0,
                explanation = "KMnO4's violet colour is discharged (brown MnO2 forms) as it oxidises the alcohol — the manuscript's rate-of-oxidation test.",
            };
            EditorUtility.SetDirty(quiz);
            changes++;
        }

        if (lib != null) EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        Debug.Log($"[W59] {changes} manuscript-fidelity changes applied (idempotent). Run Tidy Experiment Layouts next.");
    }

    /// Add a pourable prop if the layout lacks one with this itemId. Position is
    /// a placeholder — Tidy re-zones every prop deterministically right after.
    static int EnsureProp(ExperimentLayout lay, string prefab, string itemId, string display, bool pourable, string fill)
    {
        foreach (var p in lay.props) if (p.itemId == itemId) return 0;
        lay.props.Add(new ExperimentLayout.Prop
        {
            prefabName = prefab, itemId = itemId, displayName = display,
            pos = new Vector3(1.1f, 0.91f, -3.0f), targetHeight = 0.16f,
            pourable = pourable, fillChemical = fill,
        });
        return 1;
    }

    /// Add a reagent→task binding, preferring (1) the vessel that already binds
    /// that task, then (2) the SEEDED confirmatory-test vessel (a test reagent
    /// must meet the product for the reaction to fire), then (3) any bindings-
    /// bearing vessel. (The first release of this helper grabbed the first
    /// vessel outright — bindings landed on the empty main flask; Repair below
    /// migrates those.)
    static int EnsureBinding(ExperimentLayout lay, string reagent, string taskId, float ml)
    {
        ExperimentLayout.Vessel sameTask = null, seeded = null, any = null;
        foreach (var v in lay.vessels)
        {
            foreach (var b in v.bindings)
            {
                if (b.reagentChemical == reagent && b.taskId == taskId) return 0;   // already there
                if (b.taskId == taskId && sameTask == null) sameTask = v;
            }
            if (seeded == null && !string.IsNullOrEmpty(v.startChemical)) seeded = v;
            if (any == null && v.bindings.Count > 0) any = v;
        }
        var target = sameTask ?? seeded ?? any ?? (lay.vessels.Count > 0 ? lay.vessels[0] : null);
        if (target == null) return 0;
        target.bindings.Add(new ExperimentLayout.Vessel.Bind { reagentChemical = reagent, taskId = taskId, requiredMl = ml });
        return 1;
    }

    /// Migration: remove a binding from EMPTY-start vessels (mis-seated by the
    /// first helper release) so EnsureBinding can re-seat it on the test vessel.
    static int RemoveMisplacedBinding(ExperimentLayout lay, string reagent, string taskId)
    {
        int removed = 0;
        foreach (var v in lay.vessels)
        {
            if (!string.IsNullOrEmpty(v.startChemical)) continue;   // seeded vessel = correct home
            for (int i = v.bindings.Count - 1; i >= 0; i--)
                if (v.bindings[i].reagentChemical == reagent && v.bindings[i].taskId == taskId)
                { v.bindings.RemoveAt(i); removed++; }
        }
        return removed;
    }
}
#endif
