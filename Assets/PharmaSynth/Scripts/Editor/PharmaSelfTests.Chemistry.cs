#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Pouring, reactions, chemical tests, supply and shelf wiring.
public static partial class PharmaSelfTests
{
    /// One-entry ledger scaled by `frac`, as its summary string (pour-out story).
    static string LedgerScaled(float ml, float frac)
    {
        var led = new VesselLedger();
        led.Add("PG_A", ml);
        led.Scale(frac);
        return led.Summary();
    }

    static void DepletionSuite()
    {
        var chem = ScriptableObject.CreateInstance<ChemicalData>();
        chem.chemicalName = "selftest-depletion-chem";
        var otherChem = ScriptableObject.CreateInstance<ChemicalData>();
        otherChem.chemicalName = "selftest-unexpected-chem";
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "selftest-depletion";
        module.graphTasks = new List<ExperimentTask> {
            T("add-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Measuring };

        var go = new GameObject("depletion");
        var fullGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics requires a Renderer
        var bottleGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            // --- capacity guard: rejected overflow fires LiquidRejected, NOT LiquidAdded
            var full = fullGo.AddComponent<LiquidPhysics>();
            full.maxVolume = 100f; full.currentLiquidVolume = 100f; full.currentChemical = chem;
            bool added = false, rejected = false;
            full.LiquidAdded += (c, a) => added = true;
            full.LiquidRejected += (c, a) => rejected = true;
            full.AddLiquid(chem, 50f);
            A("deplete: overflow rejected", rejected && !added && Near(full.currentLiquidVolume, 100f));

            // --- accumulation toward requiredMl
            var runner = go.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            // bindings live on the vessel's own GO so the monitor never counts it as supply
            var vessel = fullGo.AddComponent<LiquidTaskBinding>();
            vessel.SetVesselAndRunner(full, runner);
            vessel.AddExpected(chem, "add-x", 50f);
            vessel.HandleReagent(chem, 20f);
            vessel.HandleReagent(chem, 20f);
            A("deplete: below threshold pends", !runner.Graph.IsComplete("add-x") && Near(vessel.AccumulatedFor("add-x"), 40f));
            vessel.HandleReagent(chem, 10f);
            A("deplete: threshold completes", runner.Graph.IsComplete("add-x"));
            vessel.HandleReagent(chem, 30f);   // extra pour after completion is ignored
            A("deplete: no double-complete side effects", runner.MistakeCount == 0);
            vessel.HandleReagent(otherChem, 10f);
            A("deplete: wrong reagent still flagged", runner.MistakeCount == 1);

            // legacy single-arg = full delivery regardless of threshold
            runner.Retry();
            var vessel2 = fullGo.AddComponent<LiquidTaskBinding>();
            vessel2.SetVesselAndRunner(full, runner);
            vessel2.AddExpected(chem, "add-x", 50f);
            vessel2.HandleReagent(chem);
            A("deplete: legacy call instant-completes", runner.Graph.IsComplete("add-x"));

            // --- pure shortfall math
            var needs = new List<ReagentSupplyMath.Need> {
                new ReagentSupplyMath.Need { taskId = "add-x", chemicalName = "X", requiredMl = 50f, deliveredMl = 0f },
                new ReagentSupplyMath.Need { taskId = "add-y", chemicalName = "Y", requiredMl = 50f, deliveredMl = 30f },
                new ReagentSupplyMath.Need { taskId = "done-z", chemicalName = "Z", requiredMl = 50f, deliveredMl = 0f },
            };
            var avail = new Dictionary<string, float> { { "X", 30f }, { "Y", 25f }, { "Z", 0f } };
            var shorts = ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail);
            A("deplete: shortfall math", shorts.Count == 1 && shorts[0] == "add-x");
            // (Y needs 20 more with 25 available -> fine; Z is complete -> excluded)
            avail["X"] = 60f;
            A("deplete: sufficient = clean", ReagentSupplyMath.FindShortfalls(needs, id => id == "done-z", avail).Count == 0);

            // --- monitor end-to-end over live components
            runner.Retry();
            var monitor = go.AddComponent<ReagentSupplyMonitor>();
            monitor.SetRunner(runner);
            var vlp = bottleGo.AddComponent<LiquidPhysics>();   // reaction vessel body
            var vessel3 = bottleGo.AddComponent<LiquidTaskBinding>();
            vessel3.SetVesselAndRunner(vlp, runner);
            vessel3.AddExpected(chem, "add-x", 50f);
            var srcGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var src = srcGo.AddComponent<LiquidPhysics>();
                src.currentChemical = chem; src.currentLiquidVolume = 30f;   // not enough for 50
                var starved = monitor.EvaluateNow();
                A("deplete: monitor detects starvation", starved.Contains("add-x"));
                src.currentLiquidVolume = 80f;
                A("deplete: monitor clean when supplied", monitor.EvaluateNow().Count == 0);
            }
            finally { UnityEngine.Object.DestroyImmediate(srcGo); }

            // --- builder supply sizing: 2.5x need floor
            A("deplete: supply autosize floor", Mathf.Max(120f, 50f * 2.5f) == 125f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(fullGo);
            UnityEngine.Object.DestroyImmediate(bottleGo);
            UnityEngine.Object.DestroyImmediate(module);
            UnityEngine.Object.DestroyImmediate(chem);
            UnityEngine.Object.DestroyImmediate(otherChem);
        }
    }

    /// Product-seeded test vessels (§1): every experiment's confirmatory-test
    /// pour must land in a vessel already holding the product, so the registry
    /// rule fires and the colour/ppt/gas observation actually shows.
    static void TestVesselSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testvessel: library + registry load", lib != null && reg != null);
        if (lib == null || reg == null) return;

        var cases = new (string file, string seed, string[] reagents)[]
        {
            // manuscript fidelity: benzoic ester = PROPYL ALCOHOL (sulfuric acid
            // alone is inert); benzamide acid test + ethanol ester
            // use the DILUTED acids; chloroform has the dichromate oxidation.
            // Benzoic (Exp 4) is zone-free: no seeded vessel
            // (the player synthesises the acid and draws from the flask) — its
            // FeCl3/ester rules are validated by the Exp 4 simrun firing them.
            // Acetanilide (Exp 5) is zone-free: no seeded vessel
            // (the player synthesises + scoops the crystals) — its bromination/
            // hydrolysis rules are validated by the Exp 5 simrun firing them.
            // Benzamide (Exp 8) is zone-free: no seeded vessel
            // (the player synthesises in the ice bath and dips crystals from the
            // dried watch glass) — its alkali/acid/nitrous rules are validated
            // by the Exp 8 simrun firing them at temperature.
            // Chloroform (Exp 7) is zone-free: no seeded vessel
            // (the player distils, washes and redistils the chloroform, then
            // draws every test from Test Tube 14) — its AgNO3/oxidation rules
            // are validated by the Exp 7 simrun firing them at temperature.
            // Acetone (Exp 6) is zone-free: no seeded vessel
            // (the player distils the acetone and draws from the receiver) —
            // its Tollens/Schiff/iodoform/bisulfite rules are validated by the
            // Exp 6 simrun firing them.
            // Ethyl (Exp 3) is zone-free: no seeded vessel (it
            // POURS ethanol/hypochlorite into fresh tubes) — its Iodoform/Ester
            // reactions are validated by the Exp 3 simrun firing them at temperature.
            // WineMaking (Exp 9) is zone-free: no pre-seeded CO2
            // tube (the FermentationController bubbles CO2 into the limewater the
            // player pours) — its WineFermentation + Limewater rules are validated
            // by the Exp 9 simrun firing them.
        };
        foreach (var c in cases)
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(
                "Assets/PharmaSynth/ScriptableObjects/Layouts/" + c.file + ".asset");
            ExperimentLayout.Vessel tv = null;
            if (layout != null)
                foreach (var v in layout.vessels) if (v.startChemical == c.seed) tv = v;
            A("testvessel: " + c.file + " seeded vessel", tv != null);
            if (tv == null) continue;
            var seedChem = lib.GetChemical(c.seed);
            A("testvessel: " + c.file + " seed in library", seedChem != null);
            foreach (var reagent in c.reagents)
            {
                bool bound = false;
                foreach (var b in tv.bindings) if (b.reagentChemical == reagent) bound = true;
                A("testvessel: " + c.file + " binds " + reagent, bound);
                var rChem = lib.GetChemical(reagent);
                A("testvessel: " + c.file + " rule fires (" + reagent + ")",
                    seedChem != null && rChem != null && reg.FindReaction(seedChem, rChem) != null);
            }
        }

        // Every chemical any layout references must resolve through the library,
        // or the builder seeds/fills silently fail at stage-build time.
        bool allResolve = true;
        void Need(string chem, string where)
        {
            if (!string.IsNullOrEmpty(chem) && lib.GetChemical(chem) == null)
            { allResolve = false; _log.Add("library missing chemical: " + chem + " (" + where + ")"); }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
            if (layout == null) continue;
            foreach (var p in layout.props) if (p.pourable) Need(p.fillChemical, layout.name);
            foreach (var v in layout.vessels)
            {
                Need(v.startChemical, layout.name);
                foreach (var b in v.bindings) Need(b.reagentChemical, layout.name);
            }
        }
        A("testvessel: all layout chemicals resolve", allResolve);
    }

    static void ChemVisualSuite()
    {
        var h = new HeatModel(22f) { HeatRate = 0.35f };
        h.SetHeating(true, 100f); for (int i = 0; i < 60; i++) h.Step(1f);
        A("heat: rises toward source", h.Current > 90f);
        var a1 = new HeatModel(22f); a1.SetHeating(true, 100f); a1.Step(10f);
        var a2 = new HeatModel(22f); a2.SetHeating(true, 100f); for (int i = 0; i < 100; i++) a2.Step(0.1f);
        A("heat: timestep-independent", Near(a1.Current, a2.Current, 0.1f));
        var gg = new GameObject("gas");
        try { var gc = gg.AddComponent<GasCollection>(); gc.AddGas(50f); A("gas: fill fraction", Near(gc.FillFraction, 0.5f)); }
        finally { UnityEngine.Object.DestroyImmediate(gg); }
    }

    static void ReactionRulesSuite()
    {
        // Cutscene outro selection (end cutscene always resolves)
        var go = new GameObject("csdir");
        try
        {
            var dir = go.AddComponent<CutsceneDirector>();
            var pass = ScriptableObject.CreateInstance<CutsceneData>();
            var fail = ScriptableObject.CreateInstance<CutsceneData>();
            var so = new SerializedObject(dir);
            so.FindProperty("success").objectReferenceValue = pass;
            so.FindProperty("failure").objectReferenceValue = fail;
            so.ApplyModifiedProperties();
            A("cutscene: success outro when passed", dir.SelectOutro(new ExperimentResult { passed = true }) == pass);
            A("cutscene: failure outro when not", dir.SelectOutro(new ExperimentResult { passed = false }) == fail);
            var cs = ScriptableObject.CreateInstance<CutsceneData>();
            cs.beats.Add(new CutsceneData.Beat { seconds = 2f }); cs.beats.Add(new CutsceneData.Beat { seconds = 3f });
            A("cutscene: total duration", Near(cs.TotalDuration(), 5f));
            UnityEngine.Object.DestroyImmediate(pass); UnityEngine.Object.DestroyImmediate(fail); UnityEngine.Object.DestroyImmediate(cs);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }

        // Crystallization
        var cgo = new GameObject("cr");
        try
        {
            var cr = cgo.AddComponent<CrystallizationController>();
            new SerializedObject(cr).FindProperty("durationSeconds").floatValue = 10f;
            var soc = new SerializedObject(cr); soc.FindProperty("durationSeconds").floatValue = 10f; soc.ApplyModifiedProperties();
            cr.BeginCrystallization(); cr.Tick(5f);
            A("cryst: half at 5/10", Near(cr.Progress, 0.5f));
            cr.Tick(6f); A("cryst: done", cr.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(cgo); }

        // Filtration
        var fgo = new GameObject("f");
        try
        {
            var fc = fgo.AddComponent<FiltrationController>();
            var sof = new SerializedObject(fc); sof.FindProperty("targetVolumeMl").floatValue = 100f; sof.ApplyModifiedProperties();
            fc.AddFiltrate(40f); A("filt: 40%", Near(fc.Fraction, 0.4f));
            fc.AddFiltrate(100f); A("filt: clamps + done", fc.IsDone);
        }
        finally { UnityEngine.Object.DestroyImmediate(fgo); }

        // Hazard reports mistake
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> { new ExperimentTask { taskId = "x", label = "x" } };
        module.trackedSkills = new List<LabSkill> { LabSkill.Safety };
        var rgo = new GameObject("r");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>(); runner.SetModule(module); runner.StartExperiment();
            var hz = rgo.AddComponent<HazardZone>(); hz.SetRunner(runner);
            hz.Report(); hz.Report();  // second is debounced
            A("hazard: reports + debounces", runner.MistakeCount == 1);
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(module); }
    }

    static void PourReactionSuite()
    {
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("reaction: registry loads", reg != null);
        if (reg == null) return;

        var benz = LoadChem("Chem_Benzaldehyde");
        var kmno4 = LoadChem("Chem_PotassiumPermanganate");
        var benzoic = LoadChem("Chem_BenzoicAcid");
        A("reaction: reaction chemicals exist", benz && kmno4 && benzoic);
        var rule = reg.FindReaction(benz, kmno4);
        A("reaction: benzaldehyde+KMnO4 rule found (either order)", rule != null && reg.FindReaction(kmno4, benz) != null);
        if (rule != null)
            A("reaction: product = benzoic acid + precipitate", rule.resultLiquid == benzoic && rule.hasPrecipitate);

        // LiquidPhysics turns reactants into product + fires ReactionOccurred on AddLiquid.
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null; lp.precipitateRenderer = null;   // skip material work in edit mode
            lp.registry = reg; lp.currentChemical = benz; lp.currentLiquidVolume = 100f;
            bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
            bool pended = false; lp.ReactionPending += _ => pended = true;

            // HEAT GATE ("achieve the needed in the procedure
            // first, before these reactions come"): BenzoicOxidation needs 40 C —
            // poured COLD it must HOLD (no early observation, no wrong-mix
            // scold), then fire the moment the bath brings the vessel there.
            lp.AddLiquid(kmno4, 40f);
            A("reaction: the right recipe poured COLD holds as pending",
                !reacted && pended && lp.HasPendingReaction && lp.currentChemical == benz
                && Near(lp.currentLiquidVolume, 140f));
            lp.SetTemperature(30f);
            A("reaction: below-threshold heat still holds", !reacted && lp.HasPendingReaction);
            lp.SetTemperature(60f);
            A("reaction: reaching temperature fires it", reacted && !lp.HasPendingReaction);
            A("reaction: currentChemical becomes the product", lp.currentChemical == benzoic);
            lp.SetContents(null, 0f);
            A("reaction: a reset vessel holds no half-done recipe",
                !lp.HasPendingReaction && Near(lp.currentTempC, 25f));
        }
        finally { UnityEngine.Object.DestroyImmediate(vgo); }

        // Pour path: the expected reagent, delivered to a bound vessel, completes its task.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Midterm_BenzoicAcid.asset");
        var rgo = new GameObject("pr_runner"); var vgo2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            runner.CompleteTask("prepare-permanganate");            // satisfy the prerequisite
            var lp2 = vgo2.AddComponent<LiquidPhysics>();
            lp2.mainRenderer = null; lp2.currentChemical = benz; lp2.currentLiquidVolume = 100f;
            var bind = vgo2.AddComponent<LiquidTaskBinding>();
            bind.SetVesselAndRunner(lp2, runner);
            bind.AddExpected(kmno4, "oxidise-benzaldehyde");
            bind.HandleReagent(kmno4);                              // = pour KMnO4 into the flask
            A("pour: expected reagent completes its task", runner.Graph.IsComplete("oxidise-benzaldehyde"));
            bind.HandleReagent(LoadChem("Chem_Aniline"));           // unexpected reagent → mistake
            A("pour: unexpected reagent logs a WrongReagent mistake", runner.Mistakes.CountOf(LabErrorType.WrongReagent) >= 1);

            // ⛔ The binding must LISTEN to its vessel after SetVesselAndRunner
            // (play-mode AddComponent runs OnEnable with vessel==null, so if
            // subscription lived only there, every pour would fire into the void and
            // the player could never complete "add distilled water"). The REAL path
            // is AddLiquid → event → binding, so that is what this drives.
            A("pour: binding LISTENS after SetVesselAndRunner", bind.IsListening);
            var water5 = LoadChem("Chem_DistilledWater");
            bind.AddExpected(water5, "dilute-test", 10f);
            lp2.AddLiquid(water5, 4f);   // through the VESSEL, not the binding
            A("pour: AddLiquid reaches the binding through the event (4 ml counted)",
                Near(bind.AccumulatedFor("dilute-test", water5), 4f));

            // Procedure-sanctioned mixes are NOT "wrong" (the water-dilution
            // scold): an expected chem for a PENDING task passes IsExpectedNow;
            // after its task completes it stops being expected.
            A("pour: a pending step's reagent is expected", bind.IsExpectedNow(water5));
            // Leniency epsilon: fifty 0.1 g float dips sum below 5.0 —
            // strict >= left the player one phantom dip short, forever.
            float drift = 0f; for (int k = 0; k < 50; k++) drift += 0.1f;
            A("pour: float-drift dips still meet the threshold",
                LiquidTaskBinding.MetThreshold(drift, 5f) && !LiquidTaskBinding.MetThreshold(4.8f, 5f));
            A("pour: a random reagent is not expected", !bind.IsExpectedNow(LoadChem("Chem_Phenol")));
            // Completion must NOT revoke sanction: LiquidAdded (completes the
            // task) fires before WrongReagentMixed inside the same AddLiquid, so
            // a pending-only gate made the completing pour punish itself.
            A("pour: a completed task's reagent STAYS sanctioned",
                runner.Graph.IsComplete("oxidise-benzaldehyde") && bind.IsExpectedNow(kmno4));
        }
        finally { UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(vgo2); }
    }

    static void TestReactionSuite()
    {
        // The confirmatory-test reactions read from Appendix C fire when the product
        // sits in the vessel and the test reagent is added (product-seeded test vessel).
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        A("testrx: master registry loads", reg != null);
        if (reg == null) return;

        void Fires(string prodFile, string reagFile, string label, bool expectGas)
        {
            var prod = LoadChem(prodFile); var reag = LoadChem(reagFile);
            if (prod == null || reag == null) { A("testrx: chems exist for " + label, false); return; }
            var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = vgo.AddComponent<LiquidPhysics>();
                lp.mainRenderer = null; lp.precipitateRenderer = null;
                lp.registry = reg; lp.currentChemical = prod; lp.currentLiquidVolume = 100f;
                bool reacted = false; lp.ReactionOccurred += _ => reacted = true;
                lp.AddLiquid(reag, 20f);
                A("testrx: " + label + " fires a reaction", reacted);
                if (expectGas)
                {
                    var rule = reg.FindReaction(prod, reag);
                    A("testrx: " + label + " evolves gas + has observation",
                        rule != null && rule.evolvesGas && !string.IsNullOrEmpty(rule.expectedObservation));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(vgo); }
        }

        Fires("Chem_Benzamide", "Chem_SodiumNitrite", "benzamide + nitrite", true);
        Fires("Chem_Acetanilide", "Chem_BromineWater", "acetanilide + bromine water", false);
        Fires("Chem_Acetone", "Chem_SilverNitrate", "acetone + Tollens (negative)", false);
    }

    // pour-guard: the raycast transfer path, self-exclusion, trigger
    // immunity and the fresh-vessel wake state — none of which the older pour
    // suites exercised (they pre-seeded chem+volume and bypassed the raycast).
    static void PourGuardSuite()
    {
        // Ledger math (display-only contents story).
        var led = new VesselLedger();
        A("ledger: empty summary blank", led.Summary() == "");
        led.Add("Ethanol", 100f); led.Add("Ethanol", 20f); led.Add("NaOH", 50f);
        A("ledger: accumulates per chemical", led.Summary() == "Ethanol 120 ml + NaOH 50 ml");
        led.Add("Water", 10f);
        A("ledger: caps with a more-tail", led.Summary(2) == "Ethanol 120 ml + NaOH 50 ml + 1 more");
        // The tube's own label asks for ALL of it (show every reagent in
        // the test tube, never hidden behind "+ more"); the cap survives for the narrow hover card.
        A("ledger: All names every reagent",
            led.Summary(VesselLedger.All).EndsWith("+ Water 10 ml")
            && !led.Summary(VesselLedger.All).Contains("more"));
        led.React("Ester");
        A("ledger: reaction collapses to the product", led.Summary() == "Ester 180 ml");
        led.Clear();
        A("ledger: clears", led.Count == 0 && led.Summary() == "");
        // Units per entry: a 0.5 g spatula dip must not int-round to
        // "0 ml"; solids read in grams, sub-unit amounts keep one decimal.
        led.Add("Aspirin", 0.5f, solid: true); led.Add("Distilled Water", 10f);
        A("ledger: solids read in grams, sub-gram amounts survive",
            led.Summary() == "Aspirin 0.5 g + Distilled Water 10 ml");
        led.Clear();

        ChemicalData Chem(string n) { var c = ScriptableObject.CreateInstance<ChemicalData>(); c.chemicalName = n; return c; }
        var chemA = Chem("PG_A"); var chemB = Chem("PG_B"); var chemC = Chem("PG_C");
        var reg = ScriptableObject.CreateInstance<ReactionRegistry>();
        var rule = ScriptableObject.CreateInstance<ReactionRule>();
        rule.inputChemicalA = chemA; rule.inputChemicalB = chemB; rule.resultLiquid = chemC;
        reg.rules = new List<ReactionRule> { rule };

        // Fresh-vessel wake: adopt on first add, react on second (a phantom-500ml
        // default would block this on every stage-built vessel).
        var wgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = wgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null; lp.registry = reg;
            A("wake: AddComponent default is EMPTY", lp.IsEmpty && lp.currentChemical == null);
            lp.AddLiquid(chemA, 50f);
            A("wake: first pour adopts the chemical", lp.currentChemical == chemA && Near(lp.currentLiquidVolume, 50f));
            lp.AddLiquid(chemB, 30f);
            A("wake: second pour reacts", lp.currentChemical == chemC);
            A("wake: ledger collapsed to the product", lp.Ledger.Summary() == "PG_C 80 ml");
            lp.SetContents(chemA, 40f);
            A("setcontents: explicit fill", lp.currentChemical == chemA && Near(lp.currentLiquidVolume, 40f) && lp.Ledger.Count == 1);
            lp.SetContents(null, 123f);
            A("setcontents: blank arms empty regardless of ml", lp.IsEmpty && lp.currentChemical == null && lp.Ledger.Count == 0);

            // SPILL → REFILL: the measurement must not linger from spilled
            // reagents. A partial pour shrinks the story; a pour
            // to dry erases it, so the refill starts clean instead of stacking.
            lp.SetContents(chemA, 100f);
            lp.PourOut(50f);
            A("spill: a partial pour shrinks the story with the contents",
                lp.Ledger.Summary() == "PG_A 50 ml" && Near(lp.currentLiquidVolume, 50f));
            lp.PourOut(60f);
            A("spill: poured DRY forgets the contents entirely",
                lp.IsEmpty && lp.currentChemical == null && lp.Ledger.Count == 0);
            A("spill: a dry bottle still knows what it dispenses", lp.LastChemical == chemA);
            lp.AddLiquid(chemA, 20f);
            A("spill: the refill starts a CLEAN story, not a stacked one",
                lp.Ledger.Summary() == "PG_A 20 ml" && Near(lp.currentLiquidVolume, 20f));
        }
        finally { UnityEngine.Object.DestroyImmediate(wgo); }

        A("ledger: Scale is a no-op at full", LedgerScaled(100f, 1f) == "PG_A 100 ml");
        A("ledger: Scale halves every entry", LedgerScaled(100f, 0.5f) == "PG_A 50 ml");
        A("ledger: Scale to nothing clears", LedgerScaled(100f, 0f) == "");

        // ResolveTarget vs REAL colliders: self skipped, triggers ignored,
        // nearest other vessel wins; then a full PourTick transfer.
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var trigger = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            source.transform.position = new Vector3(50f, 0.6f, 50f);   // far corner: no scene geometry below
            source.transform.localScale = Vector3.one * 0.2f;
            target.transform.position = new Vector3(50f, 0.1f, 50f);
            target.transform.localScale = new Vector3(0.3f, 0.2f, 0.3f);
            trigger.transform.position = new Vector3(50f, 0.35f, 50f);
            trigger.transform.localScale = Vector3.one * 0.15f;
            trigger.GetComponent<Collider>().isTrigger = true;
            var srcLp = source.AddComponent<LiquidPhysics>(); srcLp.mainRenderer = null;
            var tgtLp = target.AddComponent<LiquidPhysics>(); tgtLp.mainRenderer = null; tgtLp.registry = reg;
            Physics.SyncTransforms();

            var hits = Physics.RaycastAll(new Vector3(50f, 1.2f, 50f), Vector3.down, 3f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit hit;
            var resolved = LiquidPourer.ResolveTarget(hits, srcLp, out hit);
            A("resolve: skips self, finds the vessel below", resolved == tgtLp);
            A("resolve: reports the receiving surface", hit.collider != null && hit.collider.gameObject == target);
            bool sawTrigger = false;
            foreach (var h in hits) if (h.collider != null && h.collider.gameObject == trigger) sawTrigger = true;
            A("resolve: trigger zones never swallow the stream", !sawTrigger);

            // Full PourTick: tilt 120° for 0.1 s → ≈ 35 · InverseLerp(45,180,120) · 0.1 ml.
            srcLp.SetContents(chemA, 100f);
            var pourer = source.AddComponent<LiquidPourer>();
            pourer.Bind(srcLp);
            var spout = new GameObject("Spout").transform;
            spout.SetParent(source.transform, true);
            spout.position = source.transform.position + new Vector3(0f, 0.15f, 0f);
            pourer.spout = spout;
            Physics.SyncTransforms();
            float expected = 35f * Mathf.InverseLerp(45f, 180f, 120f) * 0.1f;
            pourer.PourTick(0.1f, 120f);
            A("pour: transfer lands in the vessel", Near(tgtLp.currentLiquidVolume, expected, 0.15f));
            A("pour: source drained by the same amount", Near(srcLp.currentLiquidVolume, 100f - expected, 0.15f));
            A("pour: receiver adopted the chemical", tgtLp.currentChemical == chemA);

            // Self-pour: only the source below its own spout → nothing may land.
            target.SetActive(false);
            Physics.SyncTransforms();
            bool selfAdded = false;
            srcLp.LiquidAdded += (_, __) => selfAdded = true;
            float before = srcLp.currentLiquidVolume;
            pourer.PourTick(0.1f, 120f);
            A("pour: self-tilt never fires LiquidAdded on itself", !selfAdded);
            A("pour: self-tilt still wastes liquid", srcLp.currentLiquidVolume < before);

        // pour assist: a receiver the precise ray narrowly misses is
        // still caught by the sphere sweep (in-headset aim is imprecise).
            target.SetActive(true);
            target.transform.position = new Vector3(50.08f, 0.1f, 50f);
            target.transform.localScale = new Vector3(0.1f, 0.2f, 0.1f);
            tgtLp.SetContents(null, 0f);
            srcLp.SetContents(chemA, 50f);
            Physics.SyncTransforms();
            var strayHits = Physics.RaycastAll(spout.position, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit strayHit;
            A("pour assist: precise ray misses the offset vessel",
                LiquidPourer.ResolveTarget(strayHits, srcLp, out strayHit) == null);
            pourer.PourTick(0.1f, 120f);
            A("pour assist: sphere sweep still lands the transfer (W5.12)", tgtLp.currentLiquidVolume > 0.1f);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(trigger);
        }
    }

    // Shelf-bottle pour wiring (tipping shelf bottles showed no
    // stream/puddle — LiquidPourer only existed on runtime-spawned props).
    static void ShelfPourWiringSuite()
    {
        A("shelfpour: null safe", ShelfPourWiring.WireBottle(null, null, null) == -1);
        var bottle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            A("shelfpour: non-liquid rejected", ShelfPourWiring.WireBottle(bottle, null, null) == -1);
            var blp = bottle.AddComponent<LiquidPhysics>();
            int added = ShelfPourWiring.WireBottle(bottle, null, null);
            A("shelfpour: adds visual+pourer+spout+spill+reactor+feedback", added == 6);   // +visual +MixFeedback
            var pourer = bottle.GetComponent<LiquidPourer>();
            A("shelfpour: pourer present", pourer != null);
            A("shelfpour: spout wired", pourer != null && pourer.spout != null);
            A("shelfpour: spill present", bottle.GetComponent<SpillMistake>() != null);
            A("shelfpour: liquid visual wired (W5.8)", blp.mainRenderer != null
                && blp.mainRenderer.sharedMaterial != null && blp.mainRenderer.sharedMaterial.HasProperty("_Fill"));
            A("shelfpour: idempotent", ShelfPourWiring.WireBottle(bottle, null, null) == 0);
        }
        finally { UnityEngine.Object.DestroyImmediate(bottle); }
    }

    // Consumable dispenser (grab the box → pull a single piece;
    // it refills; used pieces clean themselves up).
    static void DispenserSuite()
    {
        // Taken: a hand grabs the resting piece, OR it moves off the slot.
        A("dispenser: grab counts as taken", DispenserMath.IsTaken(true, 0f));
        A("dispenser: nudge off slot counts as taken", DispenserMath.IsTaken(false, 0.2f));
        A("dispenser: resting piece not taken", !DispenserMath.IsTaken(false, 0.01f));

        // Discard: only a piece that was held, is now set down, still, long enough.
        A("dispenser: used + idle piece discarded", DispenserMath.ShouldDiscard(true, false, 0f, 15f));
        A("dispenser: never-held ready piece kept", !DispenserMath.ShouldDiscard(false, false, 0f, 999f));
        A("dispenser: held piece kept", !DispenserMath.ShouldDiscard(true, true, 0f, 999f));
        A("dispenser: still-moving piece kept", !DispenserMath.ShouldDiscard(true, false, 0.5f, 999f));
        A("dispenser: not-yet-idle piece kept", !DispenserMath.ShouldDiscard(true, false, 0f, 3f));
    }

    // Hazardous-mix consequences (wrong mixtures smoke,
    // ignite, spatter or fizz, and cost the right rubric category).
    static void HazardousMixSuite()
    {
        ChemicalData Make(string name, float ph = 7f, HazardType hz = HazardType.None,
                          bool ox = false, bool conc = false, PhysicalState st = PhysicalState.Liquid)
        {
            var c = ScriptableObject.CreateInstance<ChemicalData>();
            c.chemicalName = name; c.pH = ph; c.hazard = hz;
            c.isOxidizer = ox; c.isConcentratedAcid = conc; c.state = st;
            return c;
        }

        var acid = Make("Hydrochloric Acid 6N", 0.5f, HazardType.Corrosive, conc: true);
        var bleach = Make("Sodium Hypochlorite 5%", 11f, ox: true);
        var ethanol = Make("Ethanol", 7f, HazardType.Flammable);
        var kmno4 = Make("Potassium Permanganate 0.1%", 7f, ox: true);
        var water = Make("Distilled Water");

        try
        {
            A("hazmix: acid + bleach = toxic gas",
                HazardousMix.Classify(acid, bleach) == HazardousMix.HazardOutcome.ToxicGas
                && HazardousMix.Classify(bleach, acid) == HazardousMix.HazardOutcome.ToxicGas);
            A("hazmix: oxidizer + flammable = fire",
                HazardousMix.Classify(kmno4, ethanol) == HazardousMix.HazardOutcome.FireOrExplosion
                && HazardousMix.Classify(ethanol, kmno4) == HazardousMix.HazardOutcome.FireOrExplosion);
            A("hazmix: water INTO conc acid = spatter",
                HazardousMix.Classify(acid, water) == HazardousMix.HazardOutcome.AcidSpatter);
            A("hazmix: conc acid into water = fizz only",
                HazardousMix.Classify(water, acid) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: unknown pair fizzes", HazardousMix.Classify(water, ethanol) == HazardousMix.HazardOutcome.GenericFizz);
            A("hazmix: null / same safe",
                HazardousMix.Classify(null, water) == HazardousMix.HazardOutcome.None
                && HazardousMix.Classify(water, water) == HazardousMix.HazardOutcome.None);

            A("hazmix: penalties map to the matrix",
                HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.ToxicGas) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.FireOrExplosion) == LabErrorType.HazardousAction
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.AcidSpatter) == LabErrorType.ChemicalContact
                && HazardousMix.ErrorTypeFor(HazardousMix.HazardOutcome.GenericFizz) == LabErrorType.WrongReagent);
            A("hazmix: every outcome has a warn line",
                HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.ToxicGas).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.FireOrExplosion).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.AcidSpatter).Length > 0
                && HazardousMix.WarnLineFor(HazardousMix.HazardOutcome.GenericFizz).Length > 0);

            // Name-rule flags (the audit menu + raw-reagent forge share these).
            A("hazflags: oxidizers recognized",
                HazardFlags.IsOxidizer("Potassium Permanganate 0.1%") && HazardFlags.IsOxidizer("Bleaching Powder")
                && HazardFlags.IsOxidizer("Bromine Water") && !HazardFlags.IsOxidizer("Ethanol"));
            A("hazflags: conc acids recognized",
                HazardFlags.IsConcentratedAcid("Sulfuric Acid") && HazardFlags.IsConcentratedAcid("Glacial Acetic Acid")
                && !HazardFlags.IsConcentratedAcid("Diluted Hydrochloric Acid")
                && !HazardFlags.IsConcentratedAcid("0.1N Hydrochloric Acid"));

            // Warning-pulse curve + alarm flash phase.
            A("pulse: silent at ends", Near(FadeState.Pulse01(0f, 0.35f), 0f) && Near(FadeState.Pulse01(1f, 0.35f), 0f));
            A("pulse: peaks at attack end", Near(FadeState.Pulse01(0.35f, 0.35f), 0.35f));
            A("pulse: clamped peak", FadeState.Pulse01(0.35f, 1.7f) <= 1f);
            A("alarm: flash phase alternates", LabAlarm.FlashOn(0.1f, 0.5f) && !LabAlarm.FlashOn(0.3f, 0.5f)
                && LabAlarm.FlashOn(0.6f, 0.5f) && !LabAlarm.FlashOn(0f, 0f));

            // Reactor wiring seam (edit-mode: Bind + event path, no VFX side effects).
            var host = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var lp = host.AddComponent<LiquidPhysics>();
                var reactor = host.AddComponent<HazardousMixReactor>();
                reactor.Bind(lp, null);
                A("hazmix: reactor binds without a runner", reactor != null);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(acid);
            UnityEngine.Object.DestroyImmediate(bleach);
            UnityEngine.Object.DestroyImmediate(ethanol);
            UnityEngine.Object.DestroyImmediate(kmno4);
            UnityEngine.Object.DestroyImmediate(water);
        }
    }

    // The manuscript materials catalog + consumable behaviours:
    // ~54 raw materials stocked in nature-appropriate labware).
    static void RawReagentSuite()
    {
        var rows = RawReagentCatalog.Rows;
        A("raw: catalog covers the manuscript union (≥52)", rows.Count >= 52);

        var names = new HashSet<string>();
        bool unique = true, blurbs = true, groupsOk = true;
        var validGroups = new HashSet<string> { RawReagentCatalog.GroupAcids, RawReagentCatalog.GroupOrganics,
                                                RawReagentCatalog.GroupTests, RawReagentCatalog.GroupConsumables };
        foreach (var r in rows)
        {
            if (!names.Add(r.chemicalName)) unique = false;
            if (string.IsNullOrEmpty(r.blurb)) blurbs = false;
            if (!validGroups.Contains(r.group)) groupsOk = false;
        }
        A("raw: names unique", unique);
        A("raw: every row has a hover blurb", blurbs);
        A("raw: groups valid", groupsOk);

        A("raw: light-sensitive chemicals go amber",
            RawReagentCatalog.Find("Aniline").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Benzaldehyde").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Silver Nitrate").labware == RawReagentCatalog.LabwareKind.AmberBottle
            && RawReagentCatalog.Find("Bromine Water").labware == RawReagentCatalog.LabwareKind.AmberBottle);
        // The manuscript demands the hood in exactly ONE step
        // (Exp 5 §A, aniline + acetyl chloride). Everything else works the bench.
        A("raw: fume-hood set = the manuscript's two, no more",
            RawReagentCatalog.Find("Aniline").fumeHood && RawReagentCatalog.Find("Acetyl Chloride").fumeHood
            && !RawReagentCatalog.Find("Benzoyl Chloride").fumeHood
            && !RawReagentCatalog.Find("Ammonia Solution").fumeHood
            && !RawReagentCatalog.Find("Phenol").fumeHood
            && !RawReagentCatalog.Find("Concentrated Hydrochloric Acid").fumeHood);
        A("raw: manuscript consumables present",
            RawReagentCatalog.Find("Litmus Paper") != null && RawReagentCatalog.Find("Matchsticks") != null
            && RawReagentCatalog.Find("Cotton Swabs") != null && RawReagentCatalog.Find("Filter Paper") != null
            && RawReagentCatalog.Find("Ice") != null && RawReagentCatalog.Find("Anhydrous Calcium Chloride") != null);
        // Exp 2 §D hydrolyses aspirin, so it MUST be a bench reagent, not a demo-only
        // end product — the pairing that made Exp 2 unplayable outside demo.
        A("raw: aspirin is a bench reagent for Exp 2 §D",
            RawReagentCatalog.Find("Aspirin") != null
            && RawReagentCatalog.Find("Aspirin").state == PhysicalState.Powder
            && !DemoMode.IsEndProduct("Aspirin"));
        // KMnO4 is POURED as "2 ml of 0.1% solution" (Exp 2 A.II). Marked Solid,
        // LiquidPourer cannot pour it and Exp 2's oxidation tubes are unbuildable.
        // The manuscript never uses it solid at the bench.
        {
            var kmno4 = RawReagentCatalog.Find("Potassium Permanganate 0.1%");
            var kchem = AssetDatabase.LoadAssetAtPath<ChemicalData>(
                "Assets/PharmaSynth/ScriptableObjects/Chemicals/Chem_PotassiumPermanganate.asset");
            A("raw: KMnO4 is the 0.1% SOLUTION, pourable", kmno4 != null && kmno4.state == PhysicalState.Liquid);
            A("raw: KMnO4 chemical asset agrees with its catalog row",
                kchem != null && kchem.state == PhysicalState.Liquid
                && kchem.chemicalName == "Potassium Permanganate 0.1%");
            A("raw: renaming KMnO4 kept its oxidizer flag",
                HazardFlags.IsOxidizer("Potassium Permanganate 0.1%"));
        }
        A("raw: blurb lookup + uses folded in",
            RawReagentCatalog.BlurbFor("Aniline") != null && RawReagentCatalog.BlurbFor("Aniline").Contains("Exp 5"));
        A("raw: unknown blurb null", RawReagentCatalog.BlurbFor("Unobtainium") == null);

        // Labware → prefab mapping must resolve through the scene asset library.
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("raw: library present", lib != null);
        if (lib != null)
        {
            bool prefabsOk = true;
            foreach (var kind in new[] { RawReagentCatalog.LabwareKind.ReagentBottle, RawReagentCatalog.LabwareKind.AmberBottle,
                                         RawReagentCatalog.LabwareKind.PowderJar, RawReagentCatalog.LabwareKind.DropperBottle })
                if (lib.GetPrefab(ReagentCabinetBuilder.PrefabFor(kind)) == null) prefabsOk = false;
            A("raw: bottle labware prefabs resolve", prefabsOk);
        }

        // Litmus colours.
        A("litmus: acid red", LitmusMath.ColorForPH(1f) == LitmusMath.AcidRed);
        A("litmus: base blue", LitmusMath.ColorForPH(13f) == LitmusMath.BaseBlue);
        Color mid = LitmusMath.ColorForPH(6.4f);
        A("litmus: violet between", mid != LitmusMath.AcidRed && mid != LitmusMath.BaseBlue);

        // Matchstick ignition predicate.
        A("match: ignites near heat", Matchstick.ShouldIgnite(0.1f, 120f, false, false));
        A("match: too far stays out", !Matchstick.ShouldIgnite(0.5f, 120f, false, false));
        A("match: too cold stays out", !Matchstick.ShouldIgnite(0.1f, 40f, false, false));
        A("match: lit/spent never re-ignite", !Matchstick.ShouldIgnite(0.1f, 120f, true, false)
            && !Matchstick.ShouldIgnite(0.1f, 120f, false, true));

        // ⛔ THE SCENE WIRING, not just the math. A "Build Reagent Cabinets"
        // run destroys + recreates Raw_Matchsticks, and can take its MatchStrikerSurface
        // with it. Striking the box then does NOTHING (the methane splint test and every
        // burner light are dead) while the pure ShouldStrike/ShouldIgnite tests above
        // still pass. Pure math cannot see a missing component; pin the actual objects.
        {
            var matches = UnityEngine.Object.FindObjectsByType<Matchstick>(
                FindObjectsInactive.Include);
            bool anchored = matches.Length > 0;
            foreach (var m in matches)
                if (m.transform.Find("FlameAnchor") == null) anchored = false;
            A("match: every matchstick has a FlameAnchor", anchored);

            // Matchstick.OnCollisionEnter reads GetComponentInParent<MatchStrikerSurface>()
            // off the collider it hits, so the box needs BOTH the component and a collider.
            MatchStrikerSurface box = null;
            foreach (var s in UnityEngine.Object.FindObjectsByType<MatchStrikerSurface>(
                         FindObjectsInactive.Include))
                if (s.name.StartsWith("Raw_Matchsticks")) box = s;
            A("match: the matchbox IS the striker (strike it to light)", box != null);
            A("match: the striker box has a collider to strike against",
                box != null && box.GetComponentInChildren<Collider>() != null);
        }

        // Product per module — the chemical each one exists to make, which is what
        // EndProductVisibility hides while that module runs.
        // Chemical Compounding is EXCLUDED: it is an identification lab and
        // synthesises nothing. Returning "Ethanol" for it would hide ethanol during
        // the very experiment that needs it as a sample.
        string[] ids = { "tutorial-methane", "prelim-ethyl-alcohol",
                         "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
                         "final-benzamide", "final-winemaking" };
        bool products = true;
        foreach (var id in ids) if (string.IsNullOrEmpty(DemoMode.ProductFor(id))) products = false;
        A("demo: a product for each of the 8 synthesising modules", products);
        A("demo: the ID lab synthesises nothing", DemoMode.ProductFor("prelim-chemical-compounding") == null);
        A("demo: winemaking product is Wine (not the grape-juice feedstock)", DemoMode.ProductFor("final-winemaking") == "Wine");
        A("demo: unknown module has none", DemoMode.ProductFor("nope") == null);
    }
}
#endif
