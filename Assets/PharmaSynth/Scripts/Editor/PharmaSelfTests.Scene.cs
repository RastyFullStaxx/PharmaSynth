#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Surface, effect-material and visual-sweep checks on the lab scene.
public static partial class PharmaSelfTests
{
    // surface + render calibration (the lab's look).
    // These pin ASSET state, not math, on purpose: a pack reimport or one stray inspector drag
    // silently restores the bad values, and no pure test can see a material regression - the
    // same reason the matchbox striker is pinned on the scene, not only in math.
    static void SurfaceSuite()
    {
        A("render: the autopilot counts NaN and infinite pixels, and only those",
            PlaytestAutopilot.InvalidPixelCount(new[] { Color.white, new Color(float.NaN, 0f, 0f), new Color(0f, float.PositiveInfinity, 0f), new Color(2f, 3f, 4f) }) == 2);
        foreach (var surf in LabSurfaceTuner.Surfaces)
        {
            var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(surf.path);
            string id = System.IO.Path.GetFileNameWithoutExtension(surf.path);
            A("surface: " + id + " exists", mat != null);
            if (mat == null) continue;
            A("surface: " + id + " smoothness calibrated",
              mat.HasProperty("_Smoothness") &&
              Mathf.Approximately(mat.GetFloat("_Smoothness"), surf.smoothness));
            if (surf.metallic >= 0f)
                A("surface: " + id + " metallic calibrated",
                  mat.HasProperty("_Metallic") &&
                  Mathf.Approximately(mat.GetFloat("_Metallic"), surf.metallic));
        }

        // The whole bug class, not just known offenders: URP clamps _Smoothness to
        // 0-1, so anything above it renders as a perfect mirror (pack materials at
        // 2.19-3.20 turn the ceiling, floor and cabinets into mirrors reflecting the
        // outdoor procedural skybox inside a sealed room).
        int outOfRange = 0;
        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Material", new[] { "Assets/PharmaSynth" }))
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (m != null && m.HasProperty("_Smoothness") && m.GetFloat("_Smoothness") > 1.0001f) outOfRange++;
        }
        A("surface: no material has smoothness > 1", outOfRange == 0);

        // The walls and ceiling need an albedo: the largest surfaces in the player's
        // view carrying zero texture make the room read as a grey box.
        foreach (var p in new[]
        {
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Wall_0.mat",
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Wall_1.mat",
            "Assets/PharmaSynth/Art/Environment/Laboratory/Materials/Exterior/Ceiling_2.mat",
        })
        {
            var m = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(p);
            A("surface: " + System.IO.Path.GetFileNameWithoutExtension(p) + " has an albedo map",
              m != null && m.GetTexture("_BaseMap") != null);
        }

        // Boil ramp: boilingPointC drives the liquid, so a heated beaker boils
        // instead of venting steam from the STATION while the liquid sits perfectly still.
        A("boil: empty vessel never boils", LiquidPhysics.BoilFor(0f, 300f, 100f) == 0f);
        A("boil: cold liquid is still", LiquidPhysics.BoilFor(50f, 25f, 100f) == 0f);
        A("boil: full boil at the point", Mathf.Approximately(LiquidPhysics.BoilFor(50f, 100f, 100f), 1f));
        A("boil: ramps in before the point",
          LiquidPhysics.BoilFor(50f, 96f, 100f) > 0f && LiquidPhysics.BoilFor(50f, 96f, 100f) < 1f);
        A("boil: clamped above the point", Mathf.Approximately(LiquidPhysics.BoilFor(50f, 250f, 100f), 1f));
        A("boil: follows the chemical, not 100 C", LiquidPhysics.BoilFor(50f, 80f, 78f) > 0.9f);

        // The render pipeline must be live: a Volume with Bloom/Vignette/Tonemapping
        // does nothing while the cameras have post-processing switched off.
        var rp = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>(
            "Assets/Settings/Mobile_RPAsset.asset");
        A("render: mobile RP asset exists", rp != null);
        if (rp != null)
        {
            A("render: MSAA is on (thin glass edges alias without it)", rp.msaaSampleCount >= 4);
            A("render: HDR is on (emissive panels must exceed 1.0 to read as lights)", rp.supportsHDR);
        }
    }


    // Persisted particle material (device builds strip runtime Shader.Find-only
    // shaders — the Resources asset keeps the pour stream/puddles alive on Quest).
    static void FxMaterialSuite()
    {
        var m = ShelfPourBuilder.EnsureFxMaterial();
        A("fxmat: asset exists", m != null);
        A("fxmat: transparent surface", m != null && Near(m.GetFloat("_Surface"), 1f));
        A("fxmat: alpha blend", m != null && m.GetInt("_SrcBlend") == (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        A("fxmat: no zwrite", m != null && m.GetInt("_ZWrite") == 0);
        A("fxmat: resources-loadable", Resources.Load<Material>("FxParticleUnlit") != null);
    }
    // The VISUAL autopilot's judge: pure rules over what the probe saw, so a
    // FAIL in Logs/visual-sweep-report.txt means what it says. Thresholds are the ones the
    // sweep uses — a 4% fill is invisible in VR (SimulatedRun's rule), 0.08 RGB is a change
    // a headset shows, and a gas outcome must have a live emitter or it is text-only.
    static void VisualSweepSuite()
    {
        // A vessel emptied after a test keeps its settled precipitate but loses its liquid
        // identity. It must still adopt the next thing poured in, or FindReaction(null, x)
        // misses forever and the glass fills with nameless liquid, leaving the
        // experiment unplayable.
        var residue = new GameObject("selftest-residue-vessel");
        try
        {
            var rlp = residue.AddComponent<MeshRenderer>().gameObject.AddComponent<LiquidPhysics>();
            var chem = ScriptableObject.CreateInstance<ChemicalData>();
            chem.chemicalName = "Probe Reagent";
            try
            {
                rlp.currentChemical = null; rlp.currentLiquidVolume = 0f; rlp.currentPptVolume = 5f;
                rlp.AddLiquid(chem, 10f);
                A("liquid: a vessel holding only residue still adopts what is poured in",
                    rlp.currentChemical == chem && Near(rlp.currentLiquidVolume, 10f));
            }
            finally { UnityEngine.Object.DestroyImmediate(chem); }
        }
        finally { UnityEngine.Object.DestroyImmediate(residue); }

        var o = new VisualSweep.Obs { found = true, ml = 5f, fill01 = 0.05f, rendererOn = true, tempC = 25f,
                                      particleNames = "", popups = "", chem = "x" };
        A("visual: a visible fill passes", VisualSweep.Judge(VisualSweep.Expect.Fill, o, null, 0f, 100f).status == "OK");
        var thin = o; thin.fill01 = 0.01f;
        A("visual: a 1% fill is invisible in VR", VisualSweep.Judge(VisualSweep.Expect.Fill, thin, null, 0f, 100f).Fail);
        A("visual: gas with no live emitter FAILS", VisualSweep.Judge(VisualSweep.Expect.Gas, o, null, 0f, 100f).Fail);
        var gas = o; gas.particles = 1; gas.particleNames = "EffectVfx_Smoke(20)";
        A("visual: gas with a live emitter passes", !VisualSweep.Judge(VisualSweep.Expect.Gas, gas, null, 0f, 100f).Fail);

        // ⛔ No swept step may report SKIP "the photo is the evidence": photographed and
        // judged against NOTHING, it reads as coverage on the report line while proving
        // nothing at all. These four steps' evidence is not inside a vessel:
        // a flame, a gas column, an assembled rig.
        A("visual: the methane rig and the flame test now have contracts",
            VisualSweep.ExpectFor("flame", null) == VisualSweep.Expect.Flame
            && VisualSweep.ExpectFor("methane:setup-apparatus", null) == VisualSweep.Expect.Powder   // a solid delivery, not a rig
            && VisualSweep.ExpectFor("methane:heat-mixture", null) == VisualSweep.Expect.Heat
            && VisualSweep.ExpectFor("methane:collect-gas", null) == VisualSweep.Expect.GasFill);
        {
            // These are judged BEFORE the "no vessel to look at" guard, so `found` stays false.
            var none = new VisualSweep.Obs();
            var lit = new VisualSweep.Obs { flameLit = true };
            A("visual: a flammability test with no flame FAILS",
                VisualSweep.Judge(VisualSweep.Expect.Flame, none, null, 0f, 100f).Fail
                && !VisualSweep.Judge(VisualSweep.Expect.Flame, lit, null, 0f, 100f).Fail);

            // ⛔ The flame is transient: by the final capture the match is out, so judging
            // there fails a step the game judged correctly. The latch carries the
            // mid-verb sighting to the verdict, then clears.
            VisualSweep.ClearFlameLatch();
            VisualSweep.NoteFlame(false);
            A("visual: an unlit moment does not latch a flame", !VisualSweep.FlameLatched);
            VisualSweep.NoteFlame(true);
            A("visual: a flame seen mid-verb is remembered for the verdict", VisualSweep.FlameLatched);
            VisualSweep.ClearFlameLatch();
            A("visual: the flame latch is one step's evidence, cleared after judging", !VisualSweep.FlameLatched);

            var full = new VisualSweep.Obs { gasFill01 = 0.5f };
            A("visual: collecting gas into an EMPTY tube FAILS",
                VisualSweep.Judge(VisualSweep.Expect.GasFill, none, null, 0f, 100f).Fail
                && !VisualSweep.Judge(VisualSweep.Expect.GasFill, full, null, 0f, 100f).Fail);
        }
        // ---- visual gaps, pinned so they cannot come back ----
        // A couple of millilitres in a big flask must still DRAW as something (Exp 5 puts
        // 2 ml of aniline in a 250 ml Florence flask = 0.8% of its height).
        A("fill: a small volume is floored to a readable column",
            Near(LiquidPhysics.DisplayFill01(2f, 250f), LiquidPhysics.MinVisibleFill01));
        A("fill: a full vessel is drawn at its real level",
            Near(LiquidPhysics.DisplayFill01(125f, 250f), 0.5f));
        A("fill: an empty vessel draws nothing",
            Near(LiquidPhysics.DisplayFill01(0f, 250f), 0f));
        // A 1 ml precipitate is what every rule actually deposits — it must be visible.
        A("ppt: one millilitre of precipitate is above the draw threshold",
            1f > LiquidPhysics.ShowFromMl);
        // Dry powder draws a mound; once anything is poured on it, it is a mixture.
        A("powder: a dry solid draws as a mound",
            LiquidPhysics.ShowsAsDryPowder(PhysicalState.Solid, false));
        A("powder: a wetted solid draws as liquid",
            !LiquidPhysics.ShowsAsDryPowder(PhysicalState.Solid, true));
        A("powder: a liquid is never a mound",
            !LiquidPhysics.ShowsAsDryPowder(PhysicalState.Liquid, false));
        // A vessel must never draw NOTHING while it holds something: the mound only
        // replaces the column where a mound actually exists (a product decanted onto a
        // watch glass has none).
        A("powder: a dry solid with a mound draws no column",
            !LiquidPhysics.DrawsLiquidColumn(5f, true, true));
        A("powder: a dry solid with NO mound still draws its column",
            LiquidPhysics.DrawsLiquidColumn(5f, true, false));
        A("powder: a wet mixture always draws its column",
            LiquidPhysics.DrawsLiquidColumn(5f, false, true));
        A("powder: an empty vessel draws nothing either way",
            !LiquidPhysics.DrawsLiquidColumn(0f, false, false));
        // Gas needs a visual, whichever way the rule spells it.
        var gasRule = ScriptableObject.CreateInstance<ReactionRule>();
        try
        {
            gasRule.outcome = ReactionOutcome.Fizzing;
            A("gas: a fizzing rule evolves gas", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.outcome = ReactionOutcome.GasEvolved;
            A("gas: a gas-evolved rule evolves gas", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.outcome = ReactionOutcome.ColorChange; gasRule.evolvesGas = true;
            A("gas: the explicit flag counts too", LiquidPhysics.EvolvesGas(gasRule));
            gasRule.evolvesGas = false;
            A("gas: a plain colour change does not", !LiquidPhysics.EvolvesGas(gasRule));
        }
        finally { UnityEngine.Object.DestroyImmediate(gasRule); }
        // The silver mirror is the headline observation of Exp 2's aldehyde test, so the
        // rule must actually deposit something, not just declare a Precipitate.
        var tollens = AssetDatabase.LoadAssetAtPath<ReactionRule>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/Test_TollensAldehyde.asset");
        A("content: the Tollens test deposits a visible silver mirror",
            tollens != null && tollens.hasPrecipitate && tollens.resultPrecipitate != null
            && tollens.resultPrecipitate.chemicalName == "Silver Mirror");

        var ppt = o; ppt.pptOn = true; ppt.pptMl = 2f;
        A("visual: a precipitate needs visible ppt volume",
            VisualSweep.Judge(VisualSweep.Expect.Precipitate, o, null, 0f, 100f).Fail
            && !VisualSweep.Judge(VisualSweep.Expect.Precipitate, ppt, null, 0f, 100f).Fail);
        // A solid can land in a liquid (the wine must) — a visible fill satisfies Powder too.
        var dry = new VisualSweep.Obs { found = true, powderOn = true, particleNames = "", popups = "", chem = "x" };
        A("visual: a powder mound satisfies a solid delivery", !VisualSweep.Judge(VisualSweep.Expect.Powder, dry, null, 0f, 100f).Fail);
        A("visual: a solid dissolved in a visible liquid also passes",
            !VisualSweep.Judge(VisualSweep.Expect.Powder, o, null, 0f, 100f).Fail);
        var hot = o; hot.tempC = 100f; hot.boil = 0f;
        A("visual: at the boiling point the liquid must churn", VisualSweep.Judge(VisualSweep.Expect.Heat, hot, null, 100f, 100f).Fail);
        hot.boil = 0.6f;
        A("visual: boiling at temperature passes", !VisualSweep.Judge(VisualSweep.Expect.Heat, hot, null, 100f, 100f).Fail);
        var warm = o; warm.tempC = 60f;
        A("visual: a 50 C goal below the boil ramp needs no churn", !VisualSweep.Judge(VisualSweep.Expect.Heat, warm, null, 50f, 100f).Fail);
        A("visual: an odour is only ever a popup", VisualSweep.Judge(VisualSweep.Expect.Odor, o, null, 0f, 100f).Fail);
        A("visual: a step with no contract is SKIP, never OK", VisualSweep.Judge(VisualSweep.Expect.None, o, null, 0f, 100f).status == "SKIP");
        A("visual: colour distance is Euclidean RGB", Near(VisualSweep.ColourDistance(Color.black, Color.red), 1f));

        // Expectation derivation: the fired rule outranks the verb; gas outranks the rest.
        var rule = ScriptableObject.CreateInstance<ReactionRule>();
        try
        {
            rule.outcome = ReactionOutcome.ColorChange; rule.evolvesGas = true;
            A("visual: a gas-evolving rule expects visible gas", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Gas);
            rule.evolvesGas = false; rule.outcome = ReactionOutcome.Precipitate;
            A("visual: a precipitate rule expects visible ppt", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Precipitate);
            rule.outcome = ReactionOutcome.None; rule.hasPrecipitate = false; rule.resultLiquid = null;
            A("visual: a bare rule still expects a visible fill", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Fill);
            // A deliberate negative test authors product == reactant — never judge it as a
            // colour change (acetone vs Tollens/Schiff: "no mirror" is the intended result).
            var same = ScriptableObject.CreateInstance<ChemicalData>();
            rule.outcome = ReactionOutcome.ColorChange; rule.inputChemicalA = same; rule.resultLiquid = same;
            A("visual: a no-change negative test is not a colour change", VisualSweep.ExpectFor("pour", rule) == VisualSweep.Expect.Fill);
            UnityEngine.Object.DestroyImmediate(same);
        }
        finally { UnityEngine.Object.DestroyImmediate(rule); }
        A("visual: no rule — a heat step expects temperature", VisualSweep.ExpectFor("heat", null) == VisualSweep.Expect.Heat);
        A("visual: no rule — a solid delivery expects a mound", VisualSweep.ExpectFor("solid", null) == VisualSweep.Expect.Powder);
        // A flame confirm must be judged against something: "the photo is the evidence"
        // is a picture nobody looks at rather than a check.
        A("visual: no rule — a flame confirm checks for an actual flame",
            VisualSweep.ExpectFor("flame", null) == VisualSweep.Expect.Flame);
    }
}
#endif
