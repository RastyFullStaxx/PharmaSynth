#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: Verbs, stage building, layouts, physics and grabbing.
public static partial class PharmaSelfTests
{
    /// XRI Starter Assets "XR Device Simulator" prefab — see the playmode pin.
    const string SimulatorPrefabGuid = "18ddb545287c546e19cc77dc9fbb2189";
    static void RealSizeSuite()
    {
        A("size: table count", RealSizes.Count == 51);   // incl. DistillingFlask + 8 distillation apparatus

        // Methane stage visibility: shown only during Lab
        // Tour OR the Methane attempt; hidden otherwise.
        A("methane-vis: shown in lab tour", MethaneStageVisibility.ShouldShow(true, false));
        A("methane-vis: shown during methane attempt", MethaneStageVisibility.ShouldShow(false, true));
        A("methane-vis: hidden otherwise", !MethaneStageVisibility.ShouldShow(false, false));
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        if (lib != null)
        {
            var so = new SerializedObject(lib);
            var prefabs = so.FindProperty("prefabs");
            bool all = prefabs != null;
            if (prefabs != null)
                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    var el = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                    if (el != null && !RealSizes.TryGet(el.name, out _)) { all = false; _log.Add("missing RealSizes: " + el.name); }
                }
            A("size: every library prefab covered", all);
        }
        A("size: factor math", Near(RealSizes.UniformScaleFactor(new Vector3(0.05f, 0.02f, 0.32f), 0.16f), 0.5f));
        A("size: degenerate guarded", Near(RealSizes.UniformScaleFactor(Vector3.zero, 0.16f), 1f));
    }

    static void PhysicsProfileSuite()
    {
        // Table stays in lockstep with RealSizes: same items, none missing.
        A("phys: table lockstep with RealSizes", PhysicsProfiles.Count == RealSizes.Count);
        bool allProfiled = true;
        foreach (var n in RealSizes.Names)
            if (!PhysicsProfiles.TryGet(n, out _)) { allProfiled = false; _log.Add("missing PhysicsProfile: " + n); }
        A("phys: every sized item profiled", allProfiled);

        bool massesSane = true;
        foreach (var n in PhysicsProfiles.Names)
        {
            PhysicsProfiles.TryGet(n, out var p);
            if (p.massKg <= 0.005f || p.massKg > 6f) { massesSane = false; _log.Add("implausible mass: " + n + " = " + p.massKg); }
        }
        A("phys: masses plausible", massesSane);

        // Pose math: a Y-long rod is laid down (its long axis ends horizontal)…
        var lie = PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f));
        A("phys: rod laid on its side", Mathf.Abs((lie * Vector3.up).y) < 0.01f);
        // …while an already-horizontal rod and upright items are untouched.
        A("phys: lying rod untouched", PhysicsProfiles.RestRotation(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)) == Quaternion.identity);
        A("phys: upright untouched", PhysicsProfiles.RestRotation(RestPose.Upright, new Vector3(0.02f, 0.3f, 0.02f)) == Quaternion.identity);
        // Flat tools: thinnest axis ends vertical.
        var flat = PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f));
        A("phys: flat tool face-down", Mathf.Abs((flat * Vector3.right).y) > 0.99f);
        A("phys: flat already down untouched", PhysicsProfiles.RestRotation(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)) == Quaternion.identity);

        // Resting plausibility (the drop test's verdict function).
        A("phys: lying rod plausible", PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.3f, 0.02f, 0.02f)));
        A("phys: balancing rod implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.LieLongAxis, new Vector3(0.02f, 0.3f, 0.02f)));
        A("phys: face-down gauze plausible", PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.15f, 0.01f, 0.10f)));
        A("phys: on-edge gauze implausible", !PhysicsProfiles.IsRestingPlausible(RestPose.Flat, new Vector3(0.01f, 0.15f, 0.10f)));

        // Degenerate-collider guard.
        A("phys: paper-thin collider flagged", PhysicsProfiles.IsDegenerate(new Vector3(0.004f, 0.1f, 0.1f)));
        A("phys: normal collider passes", !PhysicsProfiles.IsDegenerate(new Vector3(0.01f, 0.1f, 0.1f)));

        // EnsurePhysics + release policy on a live object (edit-mode Bind seam).
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = PhysicsProfiles.EnsurePhysics(go, "GlassRod");
            A("phys: spawns kinematic with profile mass", rb != null && rb.isKinematic && rb.useGravity && Near(rb.mass, 0.03f));
            var policy = go.AddComponent<GrabPhysicsPolicy>();
            policy.Bind(rb, null);
            A("phys: kinematic until released", !policy.IsDynamic);
            policy.OnReleased();
            A("phys: dynamic after release", policy.IsDynamic && !rb.isKinematic);

            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var added = PhysicsProfiles.EnsureCollider(go) as BoxCollider;
            A("phys: collider-less item gets a box", added != null
                && !PhysicsProfiles.IsDegenerate(Vector3.Scale(added.size, go.transform.lossyScale)));

            // Concave mesh colliders are rejected by PhysX on dynamic bodies
            // (items fell through the world in the drop test) → must convexify.
            var meshGo = new GameObject("mc");
            meshGo.transform.SetParent(go.transform, false);
            var mc = meshGo.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.convex = false;
            A("phys: concave colliders convexified", PhysicsProfiles.ConvexifyMeshColliders(go) == 1 && mc.convex);

            // Drop respawn: kill-Z + FLOOR-ONLY idle return-to-home (items
            // the player stages on tables/shelves are never reclaimed mid-run).
            A("respawn: below kill-Z", DropRespawnMath.ShouldRespawn(-1.2f, -1f));
            A("respawn: on bench safe", !DropRespawnMath.ShouldRespawn(0.9f, -1f));
            A("respawn: floor-abandoned item returns", DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 0.1f));
            A("respawn: staged on the bench stays (W5.12)", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 0.93f));
            A("respawn: staged on the gantry shelf stays (W5.12)", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 30f, 1.6f));
            A("respawn: held item never returns", !DropRespawnMath.ShouldReturnHome(1.5f, 0f, true, 30f, 0.1f));
            A("respawn: moving item waits", !DropRespawnMath.ShouldReturnHome(1.5f, 0.4f, false, 30f, 0.1f));
            A("respawn: near home stays put", !DropRespawnMath.ShouldReturnHome(0.1f, 0f, false, 30f, 0.1f));
            A("respawn: a floor item goes home after 8 s, not 25", DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 8.1f, 0.1f)
                && !DropRespawnMath.ShouldReturnHome(1.5f, 0f, false, 7f, 0.1f));
            var dr = go.AddComponent<DropRespawn>();
            dr.Bind(go.GetComponent<Rigidbody>(), null);
            dr.SetHome(new Vector3(1f, 2f, 3f), Quaternion.identity);
            go.transform.position = new Vector3(9f, -5f, 9f);
            var rbDyn = go.GetComponent<Rigidbody>(); rbDyn.isKinematic = false;
            // Respawn = a FRESH replacement: home supply restored (an
            // exhausted bottle must not come back empty and invisible).
            var lpHome = go.AddComponent<LiquidPhysics>();
            var chemHome = ScriptableObject.CreateInstance<ChemicalData>();
            try
            {
                chemHome.chemicalName = "TestSupply";
                lpHome.SetContents(chemHome, 120f);
                dr.CaptureSupply();
                lpHome.PourOut(120f);
                A("respawn: bottle drained for the test", lpHome.currentLiquidVolume < 0.01f);
                dr.GoHome();
                A("respawn: teleports home", (go.transform.position - new Vector3(1f, 2f, 3f)).magnitude < 1e-4f);
                A("respawn: re-freezes to shelf policy", rbDyn.isKinematic);
                A("respawn: restores full home supply (W5.12)",
                    Near(lpHome.currentLiquidVolume, 120f) && lpHome.currentChemical == chemHome);
            }
            finally { UnityEngine.Object.DestroyImmediate(chemHome); }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    /// Spill & breakage mishandling penalties (§2).
    static void MishandlingSuite()
    {
        // Fragility table: glass breaks, tools don't, and every entry is a real prefab.
        A("mishandle: thin glass is breakable", Mishandling.IsBreakable("Beaker_100mL")
            && Mishandling.IsBreakable("TestTube") && Mishandling.IsBreakable("Vial_WithLabel")
            && Mishandling.IsBreakable("WatchGlass"));
        A("mishandle: tools are not", !Mishandling.IsBreakable("CrucibleTongs")
            && !Mishandling.IsBreakable("Spatula") && !Mishandling.IsBreakable("WashBottle")
            && !Mishandling.IsBreakable("TestTubeRack"));
        // solid glass / droppers / porcelain delisted: sturdy in the hand.
        A("mishandle: stir rod never shatters (W5.8)", !Mishandling.IsBreakable("GlassRod"));
        A("mishandle: funnel+dropper never shatter (W5.8)", !Mishandling.IsBreakable("Funnel")
            && !Mishandling.IsBreakable("Dropper"));
        A("mishandle: porcelain never shatters (W5.8)", !Mishandling.IsBreakable("EvaporatingDish")
            && !Mishandling.IsBreakable("Crucible"));
        bool allReal = true;
        foreach (var n in Mishandling.BreakableNames)
            if (!RealSizes.TryGet(n, out _)) { allReal = false; _log.Add("breakable not in RealSizes: " + n); }
        A("mishandle: breakables are real prefabs", allReal);

        // Impact policy: only extreme falls / hard throws break —
        // a normal bench-height drop always survives.
        A("mishandle: extreme drop breaks", Mishandling.ShouldBreak(7.1f));
        A("mishandle: threshold impact breaks", Mishandling.ShouldBreak(7.0f));
        A("mishandle: sub-threshold safe (raised to 7.0 in W5.12)", !Mishandling.ShouldBreak(6.9f));
        A("mishandle: bench-height drop survives (W5.12)", !Mishandling.ShouldBreak(4.5f));
        A("mishandle: carry-bump into wall safe", !Mishandling.ShouldBreak(2.5f));
        A("mishandle: gentle set-down safe", !Mishandling.ShouldBreak(0.8f));

        // Impact loudness scales with speed (soft contacts whispered at
        // full volume before) — quiet set-down, full-volume real drop.
        A("impact: gentle set-down whispers", Mishandling.ImpactVolume01(0.8f) < 0.3f);
        A("impact: solid drop full volume", Mishandling.ImpactVolume01(4.5f) > 0.999f);
        A("impact: louder with speed", Mishandling.ImpactVolume01(2f) > Mishandling.ImpactVolume01(1f));

        // Display names for mistake messages (no more code names).
        A("names: reagent code prettified", Mishandling.Prettify("Reagent_AceticAcid_Diluted (2)") == "Acetic Acid Diluted");
        A("names: liquid twin suffix stripped", Mishandling.Prettify("TestTube_WithLiquid") == "Test Tube");
        A("names: unit tails stay intact", Mishandling.Prettify("Beaker_100mL(Clone)") == "Beaker 100mL");

        // Narration pacing: long lines must not vanish right after the
        // last character types in; short lines keep their authored dwell.
        // 1.9 s floor: shorter reads as rushed in the headset.
        A("narration: long line keeps a read hold", Near(NPCNarrationController.HoldSecondsAfterReveal(3.5f, 4.7f), 1.9f));
        A("narration: short line keeps authored dwell", Near(NPCNarrationController.HoldSecondsAfterReveal(3.5f, 1.0f), 2.5f));

        // QUIZ NAV CONTAINMENT: Back/Next must stay inside the quiz
        // panel. The real geometry is a 300-wide button on an 820-wide panel:
        // the preferred 336 step puts the outer edge at 486 vs a 410 half-width.
        {
            // Real geometry: a 300-wide Submit on an 820-wide quiz panel. Three of
            // those need 900 units, so containment ALONE pulled Back/Next 73 units
            // on top of Submit — they have to shrink first.
            float w = QuizNavButtonsBuilder.WidthFor(300f, 820f);
            float step = QuizNavButtonsBuilder.StepFor(300f, 820f, 0f);
            A("quiznav: three buttons SHRINK to fit the row", Near(w, 820f * 0.92f / 3f));
            A("quiznav: the side buttons stay INSIDE the panel", step + w * 0.5f <= 410f && step > 0f);
            A("quiznav: the side buttons never OVERLAP Submit", step >= w);
            A("quiznav: a roomy panel keeps the authored width and spacing",
                Near(QuizNavButtonsBuilder.WidthFor(100f, 2000f), 100f)
                && Near(QuizNavButtonsBuilder.StepFor(100f, 2000f, 0f), 112f));
            A("quiznav: Submit's own offset is respected",
                QuizNavButtonsBuilder.StepFor(300f, 820f, 60f) <= QuizNavButtonsBuilder.StepFor(300f, 820f, 0f));
            A("quiznav: an unresolvable parent keeps the authored width + preferred step",
                Near(QuizNavButtonsBuilder.WidthFor(300f, 0f), 300f)
                && Near(QuizNavButtonsBuilder.StepFor(300f, 0f, 0f), 336f));
        }

        // Vessel axis: a tube is long, a watch glass and a mortar are WIDE. The
        // mound must lie along the round bore either way, not the longest side.
        A("axis: an upright tube runs along Y", ExperimentSceneBuilder.VesselAxis(new Vector3(0.016f, 0.13f, 0.016f)) == 1);
        A("axis: a tube lying on X runs along X", ExperimentSceneBuilder.VesselAxis(new Vector3(0.13f, 0.016f, 0.016f)) == 0);
        A("axis: a watch glass is a shallow DISH, not a lying tube",
            ExperimentSceneBuilder.VesselAxis(new Vector3(0.09f, 0.015f, 0.09f)) == 1
            && ExperimentSceneBuilder.LongestAxis(new Vector3(0.09f, 0.015f, 0.09f)) != 1);
        A("axis: a mortar bowl is upright too", ExperimentSceneBuilder.VesselAxis(new Vector3(0.12f, 0.06f, 0.12f)) == 1);
        A("axis: a cube falls back to Y", ExperimentSceneBuilder.VesselAxis(Vector3.one * 0.05f) == 1);

        // Mound growth: every scoop must read as MORE, and the first dips move most.
        A("mound: an empty vessel shows no heap", Near(ScoopController.MoundFill(0f), 0f));
        A("mound: consecutive scoops visibly grow",
            ScoopController.MoundFill(2f) < ScoopController.MoundFill(4f)
            && ScoopController.MoundFill(4f) < ScoopController.MoundFill(6f));
        A("mound: one scoop is already clearly visible", ScoopController.MoundFill(2f) > 0.25f);
        A("mound: a sub-gram spatula dip still shows", ScoopController.MoundFill(0.1f) >= 0.15f);
        A("mound: full at the reference charge", Near(ScoopController.MoundFill(20f), 1f));

        // Round-bottom glassware gets a bulb of liquid, not a beaker's column.
        A("fill: the Florence flask is round-bottomed", ExperimentSceneBuilder.IsRoundBottom("FlorenceFlask"));
        A("fill: so is the distilling flask", ExperimentSceneBuilder.IsRoundBottom("DistillingFlask_2"));
        A("fill: a beaker is not", !ExperimentSceneBuilder.IsRoundBottom("Eq_Beaker_500mL"));
        A("fill: a null name is not", !ExperimentSceneBuilder.IsRoundBottom(null));

        // Effect children must never count toward a vessel's bounds — a world-space
        // pour StreamLine left racked tubes floating a metre in the air.
        A("bounds: the pour effects are effect children",
            ExperimentSceneBuilder.IsEffectChild("StreamLine") && ExperimentSceneBuilder.IsEffectChild("PourStream"));
        A("bounds: the fill layers are effect children",
            ExperimentSceneBuilder.IsEffectChild("Liquid") && ExperimentSceneBuilder.IsEffectChild("Precipitate")
            && ExperimentSceneBuilder.IsEffectChild("Powder"));
        A("bounds: the glass body is NOT an effect child", !ExperimentSceneBuilder.IsEffectChild("Beaker"));

        // The balance reads GROSS (auto-taring to 0.00 g is why it looked dead).
        A("scale: a loaded beaker reads vessel + contents", Near(WeighMath.PanMass(120f, 0.18f), 300f));
        A("scale: an EMPTY beaker still moves the needle", WeighMath.PanMass(0f, 0.18f) > 0f);
        A("scale: a body-less load still reads a token mass", Near(WeighMath.PanMass(0f, 0f), 5f));
        A("scale: the graded weigh condition still ignores the tare",
            WeighMath.Satisfied(true, "Ethanol", "Ethanol", 50f, 50f, null, null));

        // Digital readout: types in, wobbles while settling, locks on arrival.
        A("scale: a new reading types in left to right",
            WeighingScaleController.Typed("12.40 g", 0.08f, 26f) == "12" );
        A("scale: the whole reading shows once typed",
            WeighingScaleController.Typed("12.40 g", 5f, 26f) == "12.40 g");
        A("scale: a settled pan shows the exact mass",
            Near(WeighingScaleController.Reading(12.4f, 12.4f, 0.4f, 3f), 12.4f));
        A("scale: a settling pan hunts around the value",
            !Near(WeighingScaleController.Reading(9f, 12.4f, 0.4f, 0.04f), 9f));
        A("scale: the reading is never negative",
            WeighingScaleController.Reading(0.01f, 5f, 0.4f, 0.12f) >= 0f);
        A("scale: format is two decimals + unit", WeighingScaleController.Format(12.4f, "g") == "12.40 g");

        // The pan's scale reference is a plain private field, so the edit-time
        // Bind() does NOT survive a domain reload — the readout silently died for
        // the rest of the session. It must re-find the balance
        // itself, exactly like GrindController.AutoFindPestle.
        {
            var balFix = new GameObject("BalanceFixture");
            balFix.AddComponent<WeighingScaleController>();
            var panFix = new GameObject("Pan");
            panFix.transform.SetParent(balFix.transform, false);
            var stFix = panFix.AddComponent<WeighStation>();
            bool wasUnbound = !stFix.HasScale;
            stFix.AutoFindScale();
            A("scale: an unbound pan re-finds its balance (survives domain reload)",
              wasUnbound && stFix.HasScale);
            UnityEngine.Object.DestroyImmediate(balFix);
        }

        // Pharmee's robot colouring: a blend, never a replacement, so speech stays
        // intelligible in an educational game.
        A("robot: zero mix leaves the voice untouched", Near(RobotVoiceFx.RingSample(0.5f, -1f, 0f), 0.5f));
        A("robot: full mix is pure ring product", Near(RobotVoiceFx.RingSample(0.5f, 1f, 1f), 0.5f)
            && Near(RobotVoiceFx.RingSample(0.5f, -1f, 1f), -0.5f));
        A("robot: a blend keeps some dry signal", RobotVoiceFx.RingSample(1f, 0f, 0.38f) > 0.5f);
        A("robot: silence stays silent", Near(RobotVoiceFx.RingSample(0f, 1f, 0.5f), 0f));
        A("robot: mix is clamped", Near(RobotVoiceFx.RingSample(0.5f, -1f, 5f), -0.5f));

        // CLARITY: the grit must come
        // from bit-crushing, which brightens, not distortion, which dulls (muffles).
        A("robot: no crush mix leaves the sample alone", Near(RobotVoiceFx.Crush(0.3f, 10f, 0f), 0.3f));
        // 0.28 sits BETWEEN 5-bit steps (which are 0.0625 apart) so the quantiser
        // has something to move. A probe like 0.3123 is 0.0002 off a step and
        // fails while the code is correct.
        A("robot: crushing quantises the sample", !Near(RobotVoiceFx.Crush(0.28f, 5f, 1f), 0.28f, 0.005f));
        A("robot: a coarser depth quantises harder",
            Mathf.Abs(RobotVoiceFx.Crush(0.28f, 4f, 1f) - 0.28f)
            > Mathf.Abs(RobotVoiceFx.Crush(0.28f, 12f, 1f) - 0.28f));
        A("robot: 16-bit crush is effectively transparent",
            Near(RobotVoiceFx.Crush(0.28f, 16f, 1f), 0.28f, 0.001f));
        A("robot: silence stays silent through the crush", Near(RobotVoiceFx.Crush(0f, 8f, 1f), 0f));
        // The presence shelf must actually sit in the consonant band.
        A("robot: presence alpha rises with frequency",
            RobotVoiceFx.OnePoleAlpha(3000f, 48000) > RobotVoiceFx.OnePoleAlpha(500f, 48000));
        A("robot: presence alpha stays a valid coefficient",
            RobotVoiceFx.OnePoleAlpha(3000f, 48000) > 0f && RobotVoiceFx.OnePoleAlpha(3000f, 48000) < 1f);
        A("robot: a bad sample rate cannot produce NaN", RobotVoiceFx.OnePoleAlpha(3000f, 0) > 0f);

        // The character must come from DECIMATION + COMB, not from a low-frequency
        // ring-mod drone, which layers a hum under an untouched human voice
        // (a person speaking against an electric fan).
        A("robot: decimation holds each sample for several frames", RobotVoiceFx.HoldFrames(3f) == 3);
        A("robot: a factor of 1 means no decimation", RobotVoiceFx.HoldFrames(1f) == 1);
        A("robot: decimation never stalls or runs away",
            RobotVoiceFx.HoldFrames(0f) >= 1 && RobotVoiceFx.HoldFrames(999f) <= 16);
        A("robot: a short comb rings in the low hundreds of Hz",
            RobotVoiceFx.CombDelaySamples(3.2f, 48000, 1024) == 154);
        A("robot: comb delay always fits its buffer",
            RobotVoiceFx.CombDelaySamples(500f, 48000, 1024) < 1024
            && RobotVoiceFx.CombDelaySamples(0f, 48000, 1024) >= 1);
        A("robot: comb feedback can never self-oscillate",
            RobotVoiceFx.SafeFeedback(1.5f) <= 0.9f && RobotVoiceFx.SafeFeedback(-1f) >= 0f);

        // VOCODER: filtering recolours a voice but it still sounds human;
        // only replacing the EXCITATION removes the human pitch contour.
        A("vocoder: bands span low to high", Near(RobotVocoder.BandCenter(0, 14, 150f, 6500f), 150f)
            && Near(RobotVocoder.BandCenter(13, 14, 150f, 6500f), 6500f, 1f));
        A("vocoder: bands are LOG spaced, not linear — speech energy is not uniform",
            RobotVocoder.BandCenter(7, 14, 150f, 6500f) < (150f + 6500f) * 0.5f);
        A("vocoder: bands ascend", RobotVocoder.BandCenter(3, 14, 150f, 6500f)
            < RobotVocoder.BandCenter(4, 14, 150f, 6500f));
        A("vocoder: a degenerate band count is safe", Near(RobotVocoder.BandCenter(0, 1, 150f, 6500f), 150f));
        A("vocoder: envelope coefficient is a valid smoothing factor",
            RobotVocoder.EnvelopeCoeff(12f, 48000) > 0f && RobotVocoder.EnvelopeCoeff(12f, 48000) < 1f);
        A("vocoder: a faster release smooths less",
            RobotVocoder.EnvelopeCoeff(4f, 48000) > RobotVocoder.EnvelopeCoeff(40f, 48000));
        A("vocoder: a bad sample rate cannot produce NaN", RobotVocoder.EnvelopeCoeff(12f, 0) > 0f);
        // The carrier must be harmonically DENSE — a sine would vocode to silence,
        // because a filter bank can only shape harmonics that are already there.
        A("vocoder: carrier sweeps the full range across its phase",
            Near(RobotVoiceFx.Carrier(0f, 0f, 0f), -1f) && Near(RobotVoiceFx.Carrier(1f, 0f, 0f), 1f));
        A("vocoder: noise blend restores unvoiced consonants",
            Near(RobotVoiceFx.Carrier(0.5f, 1f, 1f), 1f));
        A("vocoder: carrier noise mix is clamped",
            Near(RobotVoiceFx.Carrier(0.5f, 1f, 5f), 1f));

        // ⛔ SCENE-vs-CODE DIALOGUE DRIFT. The gate's lines are a [SerializeField], so
        // the SCENE holds the copy the player actually hears. Editing the code
        // defaults does NOT reach the game, and since the voice manifest is built
        // from the CODE, the spoken text silently stops matching any clip and drops
        // to placeholder blips. A drifted scene copy can also give wrong instructions
        // (a lab coat ALONE while the door demands coat + goggles + gloves).
        // Pin what the player HEARS, not what the code says.
        {
            var gateGo = UnityEngine.Object.FindAnyObjectByType<PharmeeGatekeeper>(FindObjectsInactive.Include);
            if (gateGo != null)
            {
                var def = new PharmeeGatekeeper.GateLines();
                var live = gateGo.Lines;
                var drift = new List<string>();
                foreach (var f in typeof(PharmeeGatekeeper.GateLines).GetFields())
                {
                    if (f.FieldType != typeof(string)) continue;
                    if ((string)f.GetValue(live) != (string)f.GetValue(def)) drift.Add(f.Name);
                }
                A("voice: scene gate dialogue matches the code (else it plays unvoiced) — "
                  + (drift.Count == 0 ? "in sync" : "DRIFTED: " + string.Join(",", drift)),
                  drift.Count == 0);

                // The PPE prompt must name every item the door actually requires,
                // or the player is told to do the wrong thing and gets stuck.
                string coat = live.coatPrompt.ToLowerInvariant();
                A("voice: the PPE prompt names coat, goggles AND gloves",
                  coat.Contains("coat") && coat.Contains("goggle") && coat.Contains("glove"));

                // Every line the gate can speak must resolve to a real clip.
                var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>(
                    "Assets/PharmaSynth/ScriptableObjects/VoiceBank.asset");
                if (bank != null && bank.entries.Count > 0)
                {
                    int unvoiced = 0;
                    foreach (var f in typeof(PharmeeGatekeeper.GateLines).GetFields())
                    {
                        if (f.FieldType != typeof(string)) continue;
                        string text = (string)f.GetValue(live);
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        if (bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(text)) == null) unvoiced++;
                    }
                    A("voice: every gate line the player hears has a clip (" + unvoiced + " unvoiced)", unvoiced == 0);

                    // The guided tour is the SECOND scene-serialized dialogue source
                    // and drifts the same way (e.g. sending players to an
                    // "equipment cabinet" or to the emptied reagent shelf).
                    var tour = UnityEngine.Object.FindAnyObjectByType<LabTourGuide>(FindObjectsInactive.Include);
                    if (tour != null)
                    {
                        var tso = new SerializedObject(tour);
                        var st = tso.FindProperty("stops");
                        int tourUnvoiced = 0, badMark = 0;
                        for (int i = 0; st != null && i < st.arraySize; i++)
                        {
                            var el = st.GetArrayElementAtIndex(i);
                            string beat = el.FindPropertyRelative("beat")?.stringValue ?? "";
                            string mark = el.FindPropertyRelative("landmarkName")?.stringValue ?? "";
                            if (!string.IsNullOrWhiteSpace(beat)
                                && bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(beat)) == null) tourUnvoiced++;
                            // A stop whose landmark doesn't exist never fires at all.
                            if (!string.IsNullOrEmpty(mark) && GameObject.Find(mark) == null) badMark++;
                        }
                        A("voice: every lab-tour beat has a clip (" + tourUnvoiced + " unvoiced)", tourUnvoiced == 0);
                        A("tour: every stop's landmark exists in the scene (" + badMark + " dangling)", badMark == 0);
                    }

                    // The ILO recap is spoken by BOTH NPCs off the SAME text:
                    // Pharmee reads it as an intro-cutscene beat, Dr. Jimenez
                    // recaps it at QuizIntro (PharmeeGatekeeper.SpeakJimenezBrief).
                    // VoiceBank keys on speaker + text-hash, so Pharmee having a
                    // clip says NOTHING about Jimenez — his whole recap played as
                    // blips while only Pharmee's copies existed.
                    int iloUnvoiced = 0;
                    if (bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For(PharmeeLines.JimenezIloLeadIn)) == null)
                        iloUnvoiced++;
                    foreach (var mid in VoiceCorpus.ModuleIds)
                        foreach (var ilo in IloCopy.ForModule(mid))
                        {
                            if (string.IsNullOrWhiteSpace(ilo)) continue;
                            if (bank.Get(VoiceSpeaker.Pharmee, VoiceLineId.For(ilo)) == null) iloUnvoiced++;
                            if (bank.Get(VoiceSpeaker.Jimenez, VoiceLineId.For(ilo)) == null) iloUnvoiced++;
                        }
                    A("voice: every ILO line has a clip for BOTH speakers (" + iloUnvoiced + " unvoiced)",
                      iloUnvoiced == 0);
                }
            }
        }

        // MUSIC DUCKING under dialogue.
        A("duck: music drops while someone speaks", Near(MusicDucker.TargetVolume(1f, 0.25f, true), 0.25f));
        A("duck: the configured depth is a real fade-out, not a dip",
            VoiceAudioBuilder.MusicDuckTo <= 0.12f && VoiceAudioBuilder.MusicDuckTo >= 0f);
        A("duck: music returns to full when silent", Near(MusicDucker.TargetVolume(1f, 0.25f, false), 1f));
        A("duck: a quiet setting stays proportional", Near(MusicDucker.TargetVolume(0.4f, 0.5f, true), 0.2f));
        A("duck: never negative", MusicDucker.TargetVolume(-1f, 0.25f, true) >= 0f);

        // VOICE AUDIBILITY: heard room-wide, louder up close.
        A("voice: partial spatial blend — audible everywhere AND positional",
            VoiceAudioBuilder.VoiceSpatialBlend > 0.1f && VoiceAudioBuilder.VoiceSpatialBlend < 1f);
        A("voice: stays audible across the whole lab", VoiceAudioBuilder.VoiceMaxDistance >= 20f);
        A("voice: full volume out to the bench area",
            VoiceAudioBuilder.VoiceMinDistance >= 2f
            && VoiceAudioBuilder.VoiceMinDistance < VoiceAudioBuilder.VoiceMaxDistance);

        // Both NPCs must be able to SPEAK at all, and never at the same time.
        //
        // ⛔ An AudioSource is only ONE of the three things a channel needs. `Wire NPC Polish`
        // destroys and recreates Dr. Jimenez's subtitle object, and a fresh NPCNarrationController
        // defaults to `speaker = Pharmee` with a NULL `voiceBank`, so he can have a working
        // speaker cone, an animator that mouths the words and a visible subtitle bubble
        // while every line resolves to no clip. Pin all three, or the next builder run is
        // silent again and nothing says so.
        {
            int mute = 0, bankless = 0, misattributed = 0, channels = 0;
            foreach (var n in UnityEngine.Object.FindObjectsByType<NPCNarrationController>(
                         FindObjectsInactive.Include))
            {
                if (n == null) continue;
                channels++;
                var nso = new SerializedObject(n);
                if (nso.FindProperty("narratorAudioSource")?.objectReferenceValue == null) mute++;
                if (nso.FindProperty("voiceBank")?.objectReferenceValue == null) bankless++;

                // The speaker is inferred the same way the voice importer infers it: whose
                // hierarchy is this channel in?
                var expected = VoiceSpeaker.Pharmee;
                for (var t = n.transform; t != null; t = t.parent)
                    if (t.name.ToLowerInvariant().Contains("jimenez")) { expected = VoiceSpeaker.Jimenez; break; }
                var spk = nso.FindProperty("speaker");
                if (spk != null && spk.enumValueIndex != (int)expected) misattributed++;
            }
            A("voice: every narration channel has an AudioSource (" + mute + " of " + channels + " mute)",
              channels == 0 || mute == 0);
            A("voice: every narration channel has a VoiceBank (" + bankless + " of " + channels + " unvoiced)",
              channels == 0 || bankless == 0);
            A("voice: every narration channel speaks as its OWN NPC (" + misattributed + " misattributed)",
              channels == 0 || misattributed == 0);
        }

        // ⛔ Every scene in Build Settings must AGREE about the XR Device Simulator.
        //
        // Fixing only the OPEN scenes would leave MainMenu's simulator ACTIVE when SampleScene
        // alone is open, and Play always boots MainMenu. The simulator then hijacks the HMD:
        // the head pose freezes at local (0,0,0) while the real Quest controllers keep reporting
        // true poses, so the controllers float ABOVE the view. Reading the scene FILES keeps
        // this cheap and non-destructive: the suite must never open scenes to answer it.
        {
            var simStates = new List<string>();
            foreach (var entry in EditorBuildSettings.scenes)
            {
                if (!entry.enabled || string.IsNullOrEmpty(entry.path) || !File.Exists(entry.path)) continue;
                string text = File.ReadAllText(entry.path);
                string state = "absent";
                foreach (var block in text.Split(new[] { "--- !u!1001 " }, StringSplitOptions.None))
                {
                    if (block.IndexOf(SimulatorPrefabGuid, StringComparison.Ordinal) < 0) continue;
                    var m = Regex.Match(block, @"propertyPath: m_IsActive\s*
?
\s*value: (\d)");
                    state = m.Success ? (m.Groups[1].Value == "1" ? "ON" : "OFF") : "default";
                    break;
                }
                simStates.Add(Path.GetFileNameWithoutExtension(entry.path) + "=" + state);
            }
            bool agree = simStates.Count == 0
                         || simStates.TrueForAll(x => x.Substring(x.IndexOf('=') + 1)
                                                       == simStates[0].Substring(simStates[0].IndexOf('=') + 1));
            A("playmode: every build scene agrees about the XR Device Simulator ("
              + string.Join(", ", simStates) + ")", agree);
        }

        // ---- Rack tubes claim their own role ----
        //
        // The real Exp 2 shapes: the enol group separates on pour ONE (five different
        // alcohols), the alkaline-oxidation group shares its first TWO reagents and only
        // separates on the third. Both must work, which is why claiming is deferred.
        {
            var enol = new List<IReadOnlyList<string>>
            {
                new List<string> { "Ethanol", "Water", "FeCl3" },
                new List<string> { "n-Butyl", "Water", "FeCl3" },
                new List<string> { "Benzyl",  "Water", "FeCl3" },
            };
            var oxalk = new List<IReadOnlyList<string>>
            {
                new List<string> { "KMnO4", "NaOH", "n-Butyl" },
                new List<string> { "KMnO4", "NaOH", "sec-Butyl" },
                new List<string> { "KMnO4", "NaOH", "tert-Butyl" },
            };

            A("role: an empty tube can still be anything in its group",
                VesselRoleMatch.Candidates(enol, new List<string>()).Count == 3);

            var afterEthanol = VesselRoleMatch.Candidates(enol, new List<string> { "Ethanol" });
            A("role: a distinguishing first pour claims the role at once",
                afterEthanol.Count == 1 && VesselRoleMatch.ClaimedRole(afterEthanol) == 0);

            // ⛔ The reason claiming is deferred: eager assignment here would pick one of the
            // three arbitrarily and then grade the correct third pour a mistake.
            var shared = VesselRoleMatch.Candidates(oxalk, new List<string> { "KMnO4", "NaOH" });
            A("role: a shared prefix stays AMBIGUOUS rather than guessing",
                shared.Count == 3 && VesselRoleMatch.ClaimedRole(shared) == -1
                && !VesselRoleMatch.IsImpossible(shared));

            var settled = VesselRoleMatch.Candidates(oxalk,
                new List<string> { "KMnO4", "NaOH", "sec-Butyl" });
            A("role: the disambiguating pour claims the right role",
                VesselRoleMatch.ClaimedRole(settled) == 1);

            A("role: a reagent no role wants is the wrong-reagent case",
                VesselRoleMatch.IsImpossible(
                    VesselRoleMatch.Candidates(oxalk, new List<string> { "Ethanol" })));

            // Exclusivity: without this, two tubes claim the same role and the rack
            // completes on one of them while the other is still empty.
            var taken = new HashSet<int> { 0 };
            A("role: a role claimed by a sibling is off the table",
                VesselRoleMatch.Candidates(enol, new List<string>(), taken).Count == 2
                && !VesselRoleMatch.Candidates(enol, new List<string>(), taken).Contains(0));

            A("role: WouldAccept grades a pour before it is recorded",
                VesselRoleMatch.WouldAccept(oxalk, new List<string> { "KMnO4" }, "NaOH")
                && !VesselRoleMatch.WouldAccept(oxalk, new List<string> { "KMnO4" }, "Ethanol"));

            // Over-pouring a named reagent is an over-pour of the RIGHT thing; the binding
            // already tolerates it, so the matcher must not start rejecting it.
            A("role: repeating a named reagent does not break the match",
                VesselRoleMatch.Candidates(oxalk,
                    new List<string> { "KMnO4", "KMnO4", "NaOH" }).Count == 3);
        }

        // ---- Any tube will do: pools per family, guidance follows the tube in hand ----
        // ⛔ All 23 bench tubes share ONE itemId (kit-testtube), hard-glass included, so the
        // name prefix is the only family signal. A soft tube must never claim the naked-flame
        // role: Exp 6's dry distillation is the whole point of the hard-glass tube.
        A("role: hard-glass never pools with soft glass",
            VesselRoleMatch.FamilyOf("Kit_Hard-GlassTestTube_2") == VesselRoleMatch.HardGlassFamily
            && VesselRoleMatch.FamilyOf("Kit_TestTube_7") == VesselRoleMatch.RegularFamily
            && VesselRoleMatch.FamilyOf("Kit_Hard-GlassTestTube_2") != VesselRoleMatch.FamilyOf("Kit_TestTube_7")
            && VesselRoleMatch.FamilyOf("Eq_Beaker_100mL") == ""
            && VesselRoleMatch.FamilyOf("") == "");

        // The pool collapses to ONE cue: the tube in the player's hand beats a nearer one, the
        // nearest wins otherwise, and a step with no pool targets is left exactly as it was.
        {
            var near = new GameObject("pool_near"); near.transform.position = new Vector3(0f, 0f, 1f);
            var far = new GameObject("pool_far");   far.transform.position = new Vector3(0f, 0f, 5f);
            var fixedT = new GameObject("fixed_dest");
            try
            {
                var targets = new List<TaskTarget>
                {
                    new TaskTarget { transform = far.transform,  role = TargetRole.Destination, pool = true },
                    new TaskTarget { transform = near.transform, role = TargetRole.Destination, pool = true },
                };
                A("target: the nearest free pool tube is the cue when nothing is held",
                    TaskTargetRegistry.ChoosePoolMember(targets, t => false, Vector3.zero) == near.transform);
                A("target: a held pool tube beats a nearer free one",
                    TaskTargetRegistry.ChoosePoolMember(targets, t => t == far.transform, Vector3.zero) == far.transform);
                var fixedOnly = new List<TaskTarget> { new TaskTarget { transform = fixedT.transform, role = TargetRole.Destination } };
                A("target: a step with no pool targets is untouched",
                    TaskTargetRegistry.ChoosePoolMember(fixedOnly, t => false, Vector3.zero) == null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(near); UnityEngine.Object.DestroyImmediate(far);
                UnityEngine.Object.DestroyImmediate(fixedT);
            }
        }

        // ⛔ Pharmee SPEAKS the text after the arrow in every hint, so a hint that says
        // "Test Tube 15" is a voice line insisting on a tube the player is not holding. With
        // tubes claiming their own role that is wrong, not merely unhelpful.
        // "the second rack" is deliberately allowed: a rack is a real, distinct object.
        {
            var numbered = new List<string>();
            int hints = 0;
            foreach (var file in Directory.GetFiles("Assets/PharmaSynth/ScriptableObjects/Experiments", "*.asset"))
                foreach (var line in File.ReadAllLines(file))
                {
                    if (!line.TrimStart().StartsWith("hint:")) continue;
                    hints++;
                    if (Regex.IsMatch(line, @"Test Tube \d+|\bTube \d+\b"))
                        numbered.Add(Path.GetFileNameWithoutExtension(file) + ": " + line.Trim().Substring(0, Math.Min(60, line.Trim().Length)));
                }
            A("hint: no action line names a numbered tube (" + hints + " hints"
              + (numbered.Count > 0 ? ", offenders: " + string.Join(" | ", numbered) : "") + ")",
              hints > 0 && numbered.Count == 0);
        }

        // ---- Any GLASSWARE will do ----
        // Tubes pool by name family (hard glass shares their itemId); everything else by its
        // LabItem itemId, because the spares are named Eq_WatchGlass / Kit_WatchGlass_1_11 /
        // WatchGlass_2 and no prefix rule survives that. A kind with one instance never pools.
        A("pool: glassware pools by itemId, tubes by family, singletons never",
            VesselRoleMatch.PoolKey("Kit_TestTube_4", "kit-testtube") == VesselRoleMatch.RegularFamily
            && VesselRoleMatch.PoolKey("Kit_Hard-GlassTestTube_1", "kit-testtube") == VesselRoleMatch.HardGlassFamily
            && VesselRoleMatch.PoolKey("WatchGlass_2", "kit-watchglass") == "kit-watchglass"
            && VesselRoleMatch.PoolKey("Kit_WatchGlass_1_11", "kit-watchglass") == VesselRoleMatch.PoolKey("Eq_WatchGlass", "kit-watchglass")
            && VesselRoleMatch.PoolKey("Eq_Beaker_100mL", "kit-beaker_100ml") != VesselRoleMatch.PoolKey("Eq_Beaker_500mL", "kit-beaker_500ml")
            && VesselRoleMatch.PoolKey("Anything", "") == "");

        // The ghost demo must mime at the SAME pool member the glow and the arrow use: with
        // pool targets registered, Endpoints would take whichever extra is listed first.
        {
            var a = new GameObject("pool_a"); var b = new GameObject("pool_b"); var fx = new GameObject("fixed_src");
            try
            {
                var targets = new List<TaskTarget>
                {
                    new TaskTarget { transform = fx.transform, role = TargetRole.Source },
                    new TaskTarget { transform = a.transform, role = TargetRole.Destination, pool = true },
                    new TaskTarget { transform = b.transform, role = TargetRole.Destination, pool = true },
                };
                var kept = VerbDemoPlayer.CollapsePool(targets, b.transform);
                bool ok = kept.Count == 2 && kept[0].transform == fx.transform && kept[1].transform == b.transform;
                A("demo: the ghost mimes at the chosen pool member", ok
                    && VerbDemoPlayer.Endpoints(kept, out var mover, out var dest, out _)
                    && mover == fx.transform && dest == b.transform);
            }
            finally { UnityEngine.Object.DestroyImmediate(a); UnityEngine.Object.DestroyImmediate(b); UnityEngine.Object.DestroyImmediate(fx); }
        }

        // ⛔ A role is claimed by a POUR or by a delivery controller — never by construction.
        // In a family with ONE role (Exp 9's limewater tube), claiming at wiring time lets
        // the authored member win the ledger while every extra starts with an EMPTY
        // candidate set, and any pour into it is scolded before the player does anything.
        {
            var authoredGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics requires a Renderer
            var extraGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lime = ScriptableObject.CreateInstance<ChemicalData>(); lime.chemicalName = "Limewater";
            try
            {
                authoredGo.AddComponent<LiquidPhysics>(); extraGo.AddComponent<LiquidPhysics>();
                var authored = authoredGo.AddComponent<LiquidTaskBinding>();
                var extra = extraGo.AddComponent<LiquidTaskBinding>();
                var role = new List<LiquidTaskBinding.ReagentStep>
                {
                    new LiquidTaskBinding.ReagentStep { reagent = lime, taskId = "confirm-ferment", requiredMl = 10f }
                };
                var roles = new List<List<LiquidTaskBinding.ReagentStep>> { role };
                var shared = new RackRoles();
                authored.MarkPoolMember(); extra.MarkPoolMember();
                authored.SetRoles(roles, 0, shared);
                extra.SetRoles(roles, -1, shared);

                A("role: a single-role family is not claimed at wiring — the extra can still take it",
                    authored.ClaimedRole == -1 && extra.ClaimedRole == -1 && extra.RoleAmbiguous
                    && extra.CandidateTasks().Contains("confirm-ferment")
                    && authored.ExpectedSteps.Count == 1 && extra.ExpectedSteps.Count == 0);

                // A delivery controller IS the disambiguator: the first drop claims, exclusively,
                // and the authored twin, which can accept nothing, advertises NOTHING (otherwise
                // a distillate run into the twin is scolded drop by drop).
                bool took = extra.ClaimForTask("confirm-ferment");
                A("role: a delivery claim takes the role for the receiver in hand, exclusively — the twin advertises nothing",
                    took && extra.ClaimedRole == 0 && !extra.RoleAmbiguous && extra.ExpectedSteps.Count == 1
                    && authored.CandidateTasks().Count == 0 && authored.ExpectedSteps.Count == 0);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(authoredGo); UnityEngine.Object.DestroyImmediate(extraGo);
                UnityEngine.Object.DestroyImmediate(lime);
            }
        }

        // ⛔ The stream takes a candidate only from the HAND, and the hand beats a nearer bench
        // spare: otherwise a beaker resting near the bath claims Exp 7's collect role by
        // proximity before the player has picked anything up.
        A("collect: a resting spare never receives by proximity — an advertised receiver, or a candidate in the hand",
            VaporMath.MayReceive(true, false, false) && VaporMath.MayReceive(false, true, true)
            && !VaporMath.MayReceive(false, true, false) && !VaporMath.MayReceive(false, false, true)
            && VaporMath.Prefer(true, 0.45f, false, 0.2f) && !VaporMath.Prefer(false, 0.2f, true, 0.45f)
            && VaporMath.Prefer(false, 0.2f, false, 0.3f) && !VaporMath.Prefer(true, 0.4f, true, 0.3f));

        // ---- any tube, recoverable mistakes, readable guidance ----

        // ⛔ A shared step counts ROLES, not members. Pooling puts every bench tube in the
        // group, so comparing against the member count would ask for 19 alcohols.
        A("rack: a shared step completes on its ROLE count, not the pool's size",
            RackMath.AllReady(5, 5) && !RackMath.AllReady(4, 5) && RackMath.AllReady(19, 5)
            && !RackMath.AllReady(1, 0)
            && RackMath.ProgressLabel(3, 5) == "tube 3 of 5");

        // The wrist checklist names the tubes it can name, and counts the ones it cannot:
        // four identical permanganate tubes have no honest individual name.
        {
            var named = new List<string> { "Ethanol", "n-Butyl Alcohol", "Phenol" };
            var done = new HashSet<string> { "Ethanol" };
            string line = RackMath.RolesLine(named, done, 1, 3);
            A("watch: a shared step lists each tube it can name",
                line.StartsWith("Tubes 1 of 3")
                && line.Contains("• Ethanol") && line.Contains("□ n-Butyl Alcohol")
                && !line.Contains("• Phenol"));
            A("watch: indistinguishable tubes fall back to the count",
                RackMath.RolesLine(new List<string> { "", "", "", "" }, null, 2, 4) == "Tubes 2 of 4");

            // The board prints the breakdown under the current step, and only there.
            var g = new TaskGraph(new List<ExperimentTask> {
                new ExperimentTask { taskId = "rack", label = "Add the alcohols", phase = TaskPhase.ChemicalTests,
                                     prerequisites = new List<string>() },
                new ExperimentTask { taskId = "later", label = "Observe", phase = TaskPhase.ChemicalTests,
                                     prerequisites = new List<string> { "rack" } } });
            string board = ChecklistPager.BuildFocusedText(g, id => id == "rack" || id == "later" ? line : "");
            int at = board.IndexOf("Add the alcohols");
            A("watch: the per-tube breakdown prints under the current step",
                at >= 0 && board.IndexOf("Tubes 1 of 3") > at && board.IndexOf("Tubes 1 of 3") < board.IndexOf("Observe")
                && board.IndexOf("Tubes 1 of 3") == board.LastIndexOf("Tubes 1 of 3"));
        }

        // ⛔ A rinsed vessel is a FREE vessel: the role goes back to the ledger and the
        // extra can take it. Without this one wrong drop pinned a tube for the whole run.
        {
            var aGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var bGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var lime = ScriptableObject.CreateInstance<ChemicalData>(); lime.chemicalName = "Limewater";
            try
            {
                var lpA = aGo.AddComponent<LiquidPhysics>(); bGo.AddComponent<LiquidPhysics>();
                var a = aGo.AddComponent<LiquidTaskBinding>();
                var b = bGo.AddComponent<LiquidTaskBinding>();
                var role = new List<LiquidTaskBinding.ReagentStep>
                {
                    new LiquidTaskBinding.ReagentStep { reagent = lime, taskId = "confirm-ferment", requiredMl = 10f }
                };
                var roles = new List<List<LiquidTaskBinding.ReagentStep>> { role };
                var shared = new RackRoles();
                a.MarkPoolMember(); b.MarkPoolMember();
                a.SetRoles(roles, 0, shared);
                b.SetRoles(roles, -1, shared);
                b.ClaimForTask("confirm-ferment");
                A("role: the claimer holds it and the twin advertises nothing",
                    b.ClaimedRole == 0 && a.ExpectedSteps.Count == 0);
                b.ResetRole();
                A("role: emptying a vessel gives its role back to the pool",
                    b.ClaimedRole == -1 && b.RoleAmbiguous
                    && a.ExpectedSteps.Count == 1 && a.CandidateTasks().Contains("confirm-ferment"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(aGo); UnityEngine.Object.DestroyImmediate(bGo);
                UnityEngine.Object.DestroyImmediate(lime);
            }
        }

        // ⛔ ONE mistake per bad pour. A tilt-pour raises LiquidAdded every frame, so an
        // unlatched binding logs a mistake per frame (1000+ in one session) and the grade
        // is gone long before the player understands what was wrong.
        {
            var scoldGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-scold";
            module.graphTasks = new List<ExperimentTask> {
                T("add-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            var want = ScriptableObject.CreateInstance<ChemicalData>(); want.chemicalName = "Wanted";
            var junk = ScriptableObject.CreateInstance<ChemicalData>(); junk.chemicalName = "Junk";
            try
            {
                var lp = scoldGo.AddComponent<LiquidPhysics>();
                var runner = scoldGo.AddComponent<ExperimentRunner>();
                runner.SetModule(module);
                runner.StartExperiment();
                var bind = scoldGo.AddComponent<LiquidTaskBinding>();
                bind.SetVesselAndRunner(lp, runner);
                bind.AddExpected(want, "add-x", 50f);
                for (int i = 0; i < 20; i++) bind.HandleReagent(junk, 1f);
                A("mistake: a refused pour is recorded once, not once per tick",
                    runner.MistakeCount == 1 && bind.RefusedReagent == "Junk");
                var other = ScriptableObject.CreateInstance<ChemicalData>(); other.chemicalName = "Other";
                bind.HandleReagent(other, 1f);
                A("mistake: a DIFFERENT wrong reagent still counts", runner.MistakeCount == 2);
                UnityEngine.Object.DestroyImmediate(other);
                bind.HandleReagent(want, 50f);
                A("mistake: an accepted pour clears the refusal line", bind.RefusedReagent == null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(scoldGo); UnityEngine.Object.DestroyImmediate(module);
                UnityEngine.Object.DestroyImmediate(want); UnityEngine.Object.DestroyImmediate(junk);
            }
        }

        // Practice is not assessed ("scores are not important for tutorial or practice
        // mode"). Same call, same runner — only the session flag differs.
        {
            var practiceGo = new GameObject("selftest-practice");
            var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module.moduleId = "selftest-practice";
            module.graphTasks = new List<ExperimentTask> {
                T("a", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure) };
            bool was = TutorialSession.Active;
            try
            {
                var runner = practiceGo.AddComponent<ExperimentRunner>();
                runner.SetModule(module);
                runner.StartExperiment();
                TutorialSession.Active = true;
                runner.RecordMistake(LabErrorType.WrongReagent, "practice");
                A("practice: a tutorial run records no mistakes", runner.MistakeCount == 0);
                TutorialSession.Active = false;
                runner.RecordMistake(LabErrorType.WrongReagent, "graded");
                A("practice: a graded run still counts every mistake", runner.MistakeCount == 1);
            }
            finally
            {
                TutorialSession.Active = was;
                UnityEngine.Object.DestroyImmediate(practiceGo); UnityEngine.Object.DestroyImmediate(module);
            }
        }

        // Two tags at the same screen height are pushed apart, bottom-up and capped, so a
        // label never wanders off the object it names.
        {
            var y = new List<float> { 100f, 104f, 400f };
            var half = new List<float> { 10f, 10f, 10f };
            var mpp = new List<float> { 0.001f, 0.001f, 0.001f };
            var lifts = LabelDeclutterMath.Lifts(y, half, mpp);
            A("label: overlapping tags are pushed apart, the lone one is left alone",
                lifts[0] == 0f && lifts[1] > 0f && lifts[2] == 0f
                && (104f + lifts[1] / 0.001f) >= (100f + 20f) - 0.01f);
            var tight = LabelDeclutterMath.Lifts(new List<float> { 0f, 0f },
                new List<float> { 500f, 500f }, new List<float> { 0.001f, 0.001f });
            A("label: the lift is capped in METRES, never chased arbitrarily far",
                tight[1] <= LabelDeclutterMath.MaxLift + 1e-4f);
            A("label: nothing to solve is not an error", LabelDeclutterMath.Lifts(null, null, null).Length == 0);
        }

        // The vessel explains itself: what bounced, and that a free tube is free.
        A("hint: a refused pour and a free vessel both say so",
            VesselStatusMath.RefusalLine("Sodium Hydroxide").Contains("Sodium Hydroxide")
            && VesselStatusMath.RefusalLine("") == ""
            && VesselStatusMath.FreeVesselLine(true, true, true).Length > 0
            && VesselStatusMath.FreeVesselLine(true, false, true) == ""
            && VesselStatusMath.FreeVesselLine(false, true, true) == ""
            && VesselStatusMath.FreeVesselLine(true, true, false) == "");

        // A custom shader with no stereo-instancing macros renders to eye index 0 ONLY.
        //
        // The project runs Single Pass Instanced: both eyes go into one texture array and the
        // shader picks its slice from the instance id. Without the macros, the tutorial
        // arrows and the highlight glow (PharmaGuide, PharmaGlow) are visible in the
        // LEFT EYE ONLY, which only shows with one eye shut.
        // Nothing in the editor shows this - the Game view renders a single eye and looks
        // perfect - so a text pin is the only cheap guard. Surface shaders (Glass) get the
        // handling generated for them, which is why the check is on the MACROS and not on
        // "#pragma multi_compile_instancing".
        {
            var oneEyed = new List<string>();
            int shaders = 0;
            foreach (var file in Directory.GetFiles("Assets/PharmaSynth", "*.shader",
                                                    SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(file);
                shaders++;
                if (text.IndexOf("UNITY_VERTEX_OUTPUT_STEREO", StringComparison.Ordinal) < 0
                    || text.IndexOf("UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO", StringComparison.Ordinal) < 0)
                    oneEyed.Add(Path.GetFileName(file));
            }
            A("xr: every custom shader declares the stereo macros (" + shaders + " checked"
              + (oneEyed.Count > 0 ? ", ONE-EYED: " + string.Join(", ", oneEyed) : "") + ")",
              shaders > 0 && oneEyed.Count == 0);
        }

        // ---- the manuscript is the AUTHORITY, so check the numbers against it ----
        //
        // ⛔ The `content:` pins prove a module LOADS, has N tasks,
        // is solvable and builds its scoring spine, never that "5 drops" is authored as 5.
        // Every quantity in the game is hand-transcribed from Appendix C.
        //
        // The verb contract (Docs/systems-reference.md) is what makes this checkable: the number in the
        // instruction IS the action count, whatever its unit, and a dropper delivers
        // DropperController.MlPerSqueeze per squeeze — so "2 drops" is authored as 2.
        //
        // The table is CURATED (Docs/manuscript-quantities.tsv); see the header there for why
        // it is not parsed out of the PDF. Rows marked `accepted` are documented deviations
        // and are counted, not asserted, so a decision never reads as a defect.
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-quantities.tsv");
            A("manuscript: the quantity authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                var layouts = new Dictionary<string, ExperimentLayout>();
                foreach (var guid in AssetDatabase.FindAssets("t:ExperimentLayout",
                             new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
                {
                    var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(guid));
                    if (l != null && !string.IsNullOrEmpty(l.moduleId)) layouts[l.moduleId] = l;
                }

                int checkedRows = 0, acceptedRows = 0, disagree = 0;
                var wrong = new List<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('\t');
                    if (f.Length < 6) continue;
                    string moduleId = f[0].Trim(), taskId = f[1].Trim(), reagent = f[2].Trim();
                    string expectedText = f[3].Trim(), accepted = f[4].Trim();
                    if (accepted == "accepted") { acceptedRows++; continue; }
                    if (!float.TryParse(expectedText, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out float expected)) continue;
                    if (!layouts.TryGetValue(moduleId, out var layout))
                    { wrong.Add(moduleId + ": no layout"); continue; }

                    checkedRows++;
                    if (accepted == "review") disagree++;

                    if (taskId == "HEAT" || taskId == "CHILL")
                    {
                        bool found = false;
                        foreach (var v in layout.vessels)
                        {
                            float t = taskId == "HEAT" ? v.heatToC : v.chillToC;
                            if (t > 0f && Mathf.Abs(t - expected) < 0.01f) { found = true; break; }
                        }
                        if (!found) wrong.Add(moduleId + " " + taskId + " " + expected + " C not authored");
                        continue;
                    }

                    float? authored = null;
                    foreach (var v in layout.vessels)
                        foreach (var b in v.bindings)
                            if (b.taskId == taskId && b.reagentChemical == reagent) authored = b.requiredMl;
                    if (authored == null) wrong.Add(moduleId + "/" + taskId + "/" + reagent + ": no binding");
                    else if (Mathf.Abs(authored.Value - expected) > 0.001f)
                        wrong.Add(moduleId + "/" + taskId + "/" + reagent + ": authored "
                                  + authored.Value + ", manuscript implies " + expected);
                }

                foreach (var w in wrong) _log.Add("manuscript drift: " + w);
                A("manuscript: every checked quantity matches Appendix C ("
                  + checkedRows + " checked, " + wrong.Count + " adrift, "
                  + acceptedRows + " accepted deviations)", wrong.Count == 0);
                A("manuscript: the authority table actually covers the game", checkedRows >= 20);
                // Not a failure — a standing reminder that these await a client decision.
                A("manuscript: " + disagree + " quantities knowingly differ and are recorded as such",
                  disagree >= 0);
            }
        }

        // ---- the manuscript's STEP ORDER, not just its numbers ----
        //
        // ⛔ "runner: out-of-order = wrong step" proves the MECHANISM and "catalog: prereq
        // chain valid + ordered" proves the module CHAIN. Neither checks that a module's
        // authored prerequisites follow Appendix C's own a-b-c sequence: a module whose steps
        // are quietly reordered would pass every pin. Docs/manuscript-order.tsv gives every
        // task its manuscript step as a sortable ordinal (A.a = 101 ... B.a = 201; the data
        // sheet is 999), and this asserts no prerequisite edge runs BACKWARDS through the
        // manual: for every "P must precede T", ordinal(P) <= ordinal(T).
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-order.tsv");
            A("manuscript: the step-order authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                var ordinal = new Dictionary<string, int>();
                var acceptedModules = new HashSet<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('	');
                    if (f.Length < 6) continue;
                    if (f[4].Trim() == "accepted") { acceptedModules.Add(f[0].Trim()); continue; }
                    if (int.TryParse(f[3].Trim(), out int ord)) ordinal[f[0].Trim() + "/" + f[1].Trim()] = ord;
                }

                int edges = 0, backwards = 0, unmapped = 0, modulesChecked = 0;
                var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                    "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
                if (lib != null)
                    foreach (var m in lib.modules)
                    {
                        if (m == null || acceptedModules.Contains(m.moduleId)) continue;
                        modulesChecked++;
                        foreach (var t in m.graphTasks)
                        {
                            if (t == null) continue;
                            if (!ordinal.TryGetValue(m.moduleId + "/" + t.taskId, out int mine))
                            { unmapped++; _log.Add("manuscript order: no step for " + m.moduleId + "/" + t.taskId); continue; }
                            foreach (var p in t.prerequisites)
                            {
                                if (!ordinal.TryGetValue(m.moduleId + "/" + p, out int theirs)) { unmapped++; continue; }
                                edges++;
                                if (theirs > mine)
                                {
                                    backwards++;
                                    _log.Add("manuscript order: " + m.moduleId + " makes " + p + " (" + theirs
                                             + ") precede " + t.taskId + " (" + mine + ") — BACKWARDS through Appendix C");
                                }
                            }
                        }
                    }
                A("manuscript: every authored task has a manuscript step (" + unmapped + " unmapped across "
                  + modulesChecked + " modules)", lib != null && unmapped == 0 && modulesChecked >= 6);
                A("manuscript: no prerequisite runs backwards through Appendix C (" + backwards + " of "
                  + edges + " edges)", edges >= 60 && backwards == 0);
            }
        }

        // ---- the manuscript's OBSERVATION WORDS, not just its numbers and order ----
        //
        // ⛔ The VISUAL sweep judges what the student SEES against the fired rule; this
        // judges the WORDS the student reads (`expectedObservation`) against what the manual
        // says they should observe. Without it, a rewrite that dropped "silver mirror" or
        // "ammonia" would pass. Docs/manuscript-observations.tsv carries the manual's own
        // result word per rule.
        {
            string tsv = System.IO.Path.Combine(Application.dataPath, "..", "Docs/manuscript-observations.tsv");
            A("manuscript: the observation authority file is present", System.IO.File.Exists(tsv));
            if (System.IO.File.Exists(tsv))
            {
                int checkedRules = 0, acceptedRules = 0, missingAssets = 0;
                var misses = new List<string>();
                foreach (var raw in System.IO.File.ReadAllLines(tsv))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.StartsWith("#")) continue;
                    var f = raw.Split('	');
                    if (f.Length < 4) continue;
                    string asset = f[0].Trim(), key = f[1].Trim(), accepted = f[2].Trim();
                    if (accepted == "accepted") { acceptedRules++; continue; }
                    var rule = AssetDatabase.LoadAssetAtPath<ReactionRule>(
                        "Assets/PharmaSynth/ScriptableObjects/Reactions/" + asset + ".asset");
                    if (rule == null) { missingAssets++; misses.Add(asset + ": no such rule asset"); continue; }
                    checkedRules++;
                    string have = (rule.expectedObservation ?? "").ToLowerInvariant();
                    if (!have.Contains(key.ToLowerInvariant()))
                        misses.Add(asset + ": the manual's \"" + key + "\" is missing from the popup text");
                }
                foreach (var m in misses) _log.Add("manuscript observation: " + m);
                A("manuscript: every rule's popup carries the manual's result word ("
                  + checkedRules + " checked, " + misses.Count + " missing, " + acceptedRules + " accepted)",
                    missingAssets == 0 && misses.Count == 0 && checkedRules >= 20);
            }
        }

        // Voice budget order: Jimenez's rows are generated first.
        A("voice: Jimenez sorts before Pharmee",
            VoiceManifestExporter.SpeakerPriority("Jimenez") < VoiceManifestExporter.SpeakerPriority("Pharmee"));

        // Recording-safety of the spoken corpus. A subtitle can read perfectly and
        // still be spoken wrong — these are the artefacts that survive to the ear.
        A("voice: a formula subscript is not recording-safe",
            !VoicePolish.IsRecordingSafe("filter the MnO2, acidify"));
        A("voice: a hyphenated range is not recording-safe",
            !VoicePolish.IsRecordingSafe("the 70-80 degree fraction"));
        A("voice: shouted caps are not recording-safe",
            !VoicePolish.IsRecordingSafe("lab coat, goggles, AND gloves"));
        A("voice: ordinary copy is recording-safe",
            VoicePolish.IsRecordingSafe("Hold the cut steady between 70 and 80 degrees."));
        A("voice: the polish rules clean what they target",
            VoicePolish.IsRecordingSafe(VoicePolish.ApplyRules("filter the MnO2, acidify"))
            && VoicePolish.IsRecordingSafe(VoicePolish.ApplyRules("the 70-80 degree fraction")));
        A("voice: the deleted thermometer is no longer named",
            !VoicePolish.ApplyRules("Watch the thermometer through the 70-80 degree window.")
                 .Contains("thermometer"));
        // The whole live corpus must be recording-safe before credits are spent.
        {
            int unsafeLines = 0;
            foreach (var l in VoiceCorpus.CodeLines())
                if (!VoicePolish.IsRecordingSafe(l.text)) unsafeLines++;
            A("voice: every code-authored line is recording-safe", unsafeLines == 0);
        }

        // EVERY dialogue pool must reach the corpus, or the lines exist, play, and
        // are never voiced (e.g. CampaignComplete, the campaign-completion payoff, would
        // play as blips). Reflect rather than enumerate, so a NEW pool added
        // later fails this the moment it is forgotten.
        {
            var corpusText = new HashSet<string>();
            foreach (var l in VoiceCorpus.CodeLines()) corpusText.Add(VoiceLineId.For(l.text));
            var missing = new List<string>();
            foreach (var f in typeof(PharmeeLines).GetFields())
            {
                string[] pool = f.FieldType == typeof(string[]) ? (string[])f.GetValue(null)
                              : f.FieldType == typeof(string) ? new[] { (string)f.GetValue(null) }
                              : null;
                if (pool == null) continue;
                foreach (var t in pool)
                    if (!string.IsNullOrWhiteSpace(t) && !corpusText.Contains(VoiceLineId.For(t)))
                    { missing.Add(f.Name); break; }
            }
            A("voice: every PharmeeLines pool reaches VoiceCorpus ("
              + (missing.Count == 0 ? "all" : string.Join(",", missing) + " MISSING") + ")",
              missing.Count == 0);
        }

        // Holo board scroll paging (wrap + scrollable checklist).
        A("holo: page down moves toward the bottom", Near(HoloScroller.NextPage(1f, 0.6f, -1), 0.4f));
        A("holo: clamped at the bottom", HoloScroller.NextPage(0.2f, 0.6f, -1) == 0f);
        A("holo: clamped at the top", HoloScroller.NextPage(0.9f, 0.6f, +1) == 1f);

        // Settle-freeze: a released body at rest goes kinematic in place.
        A("settle: rested body freezes", DropRespawnMath.ShouldSettleFreeze(false, false, 0.01f, 3f));
        A("settle: held blocks", !DropRespawnMath.ShouldSettleFreeze(true, false, 0.01f, 3f));
        A("settle: already kinematic no-op", !DropRespawnMath.ShouldSettleFreeze(false, true, 0.01f, 3f));
        A("settle: moving body waits", !DropRespawnMath.ShouldSettleFreeze(false, false, 0.4f, 3f));
        A("settle: needs the settle beat", !DropRespawnMath.ShouldSettleFreeze(false, false, 0.01f, 1f));

        // Spill policy: un-held + tipped + has liquid, and only then.
        A("mishandle: knocked-over bottle spills", Mishandling.IsSpilling(75f, false, 80f));
        A("mishandle: held pour is not a spill", !Mishandling.IsSpilling(75f, true, 80f));
        A("mishandle: empty bottle can't spill", !Mishandling.IsSpilling(75f, false, 0f));
        A("mishandle: upright bottle safe", !Mishandling.IsSpilling(20f, false, 80f));

        // Grading: both mishandling types hit the Sanitation rubric.
        A("mishandle: spill maps to Sanitation", MistakeLog.CategoryFor(LabErrorType.SpilledReagent) == RubricCategory.Sanitation);
        var log = new MistakeLog();
        log.Record(LabErrorType.DroppedGlassware, "");
        log.Record(LabErrorType.SpilledReagent, "");
        A("mishandle: grader counts both", log.CountOfAny(LabErrorType.DroppedGlassware, LabErrorType.SpilledReagent) == 2);

        // Action-SFX policy: material-aware drop clatter + reaction cues + stride.
        A("sfx: glass clinks", Mishandling.DropSoundKey("Beaker_100mL") == "glass-clink");
        A("sfx: delisted glass still clinks (W5.8)", Mishandling.DropSoundKey("GlassRod") == "glass-clink"
            && Mishandling.DropSoundKey("Funnel") == "glass-clink"
            && Mishandling.DropSoundKey("EvaporatingDish") == "glass-clink");
        A("sfx: metal clatters", Mishandling.DropSoundKey("CrucibleTongs") == "drop-metal");
        A("sfx: wood knocks", Mishandling.DropSoundKey("TestTubeRack") == "drop-wood");
        // nature audit: the scale + burners sound (and are) metal, never glass.
        A("sfx: scale + burners are metal (W5.12)", Mishandling.DropSoundKey("Balance") == "drop-metal"
            && Mishandling.DropSoundKey("BunsenBurner") == "drop-metal"
            && Mishandling.DropSoundKey("AlcoholBurner") == "drop-metal"
            && !Mishandling.IsBreakable("Balance") && !Mishandling.IsBreakable("BunsenBurner")
            && !Mishandling.IsBreakable("AlcoholBurner"));
        A("sfx: porcelain mortar clinks, never breaks (W5.12)",
            Mishandling.DropSoundKey("Motar") == "glass-clink" && !Mishandling.IsBreakable("Motar"));
        A("sfx: fizz for gas outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Fizzing) == "reaction-fizz"
            && Mishandling.SfxForOutcome(ReactionOutcome.GasEvolved) == "reaction-fizz");
        A("sfx: chime for visible outcomes", Mishandling.SfxForOutcome(ReactionOutcome.Precipitate) == "mixture-complete"
            && Mishandling.SfxForOutcome(ReactionOutcome.ColorChange) == "mixture-complete");
        A("sfx: silence for negative tests", Mishandling.SfxForOutcome(ReactionOutcome.None) == "");
        float acc = 0f;
        A("sfx: stride accumulates", StrideMath.Steps(ref acc, 0.5f, 0.75f) == 0 && StrideMath.Steps(ref acc, 0.5f, 0.75f) == 1);
        A("sfx: long move = many steps", StrideMath.Steps(ref acc, 3f, 0.75f) >= 4);
        A("sfx: no distance no steps", StrideMath.Steps(ref acc, 0f, 0.75f) == 0);

        // Mirror avatar: foot point sits under the head at the floor (+offset), XZ kept.
        var foot = PlayerAvatarRig.FootUnder(new Vector3(2f, 1.7f, -3f), 0.25f, 0.02f);
        A("avatar: foot keeps XZ", Near(foot.x, 2f) && Near(foot.z, -3f));
        A("avatar: foot sits on floor+offset", Near(foot.y, 0.27f));

        // Per-piece PPE: all three required; missing-summary text.
        var ppeSet = new PPESetModel();
        A("ppe: nothing worn at start", !ppeSet.AllWorn && ppeSet.WornCount == 0);
        A("ppe: missing lists all three", ppeSet.MissingSummary() == "lab coat, goggles and gloves");
        A("ppe: don returns newly-worn", ppeSet.Don(PPEPiece.Coat) && !ppeSet.Don(PPEPiece.Coat));
        A("ppe: coat alone is not enough", !ppeSet.AllWorn);
        A("ppe: missing pair reads naturally", ppeSet.MissingSummary() == "goggles and gloves");
        ppeSet.Don(PPEPiece.Goggles);
        A("ppe: one missing reads singly", ppeSet.MissingSummary() == "gloves");
        ppeSet.Don(PPEPiece.Gloves);
        A("ppe: all three = fully dressed", ppeSet.AllWorn && ppeSet.MissingSummary() == "");
        A("ppe: clear strips everything", ppeSet.Clear() && !ppeSet.AllWorn && !ppeSet.Clear());
        // Hard door gate: campaign entry needs full PPE; tour/review don't.
        A("ppe: armed door requires PPE", GatekeeperModel.RequiresPPEToOpen(GateState.DoorArmed)
            && GatekeeperModel.RequiresPPEToOpen(GateState.Running));
        A("ppe: tour/review need no PPE", !GatekeeperModel.RequiresPPEToOpen(GateState.LabTour)
            && !GatekeeperModel.RequiresPPEToOpen(GateState.ScoreReview));
        // A lit burner blows out when lifted and only re-lights once down.
        A("burner: lifted blows out", BurnerController.ShouldBlowOut(true, true));
        A("burner: rests stay lit", !BurnerController.ShouldBlowOut(true, false));
        // LiquidPhysics must NOT adopt an opaque vessel mesh as its fill renderer —
        // it disables mainRenderer when empty, which vanished the mortar in play
        // Only a PharmaLiquid "_Fill" surface may be adopted.
        {
            var litSh = Shader.Find("Universal Render Pipeline/Lit");
            var fillSh = Shader.Find("PharmaSynth/Liquid");
            if (litSh != null)
                A("liquidphysics: opaque vessel mesh not adopted as fill",
                  !LiquidPhysics.ShouldAdoptHostRenderer(new Material(litSh)));
            if (fillSh != null)
                A("liquidphysics: liquid _Fill surface is adopted",
                  LiquidPhysics.ShouldAdoptHostRenderer(new Material(fillSh)));
        }

        // Pharmee flight lean: proportional to speed, clamped, zero at rest.
        A("pharmee: lean scales with speed", Near(PharmeeAttitude.LeanFor(0.5f, 22f, 14f), 11f));
        A("pharmee: lean clamped", Near(PharmeeAttitude.LeanFor(3f, 22f, 14f), 14f));
        A("pharmee: no lean at rest", Near(PharmeeAttitude.LeanFor(0f, 22f, 14f), 0f));

        // Station VFX: verb → effect style, edit-mode-safe toggling.
        A("vfx: heat steams", StationVfx.StyleFor(StationSim.Heat) == "steam");
        A("vfx: crystallise frosts", StationVfx.StyleFor(StationSim.Crystallise) == "frost");
        A("vfx: filter drips", StationVfx.StyleFor(StationSim.Filter) == "drip");
        A("vfx: collect bubbles", StationVfx.StyleFor(StationSim.Collect) == "bubbles");
        A("vfx: none is empty", StationVfx.StyleFor(StationSim.None) == "");

        // Proctor roaming: idle → walk out → observe → walk home.
        var roam = new ProctorRoamModel(3, idleMin: 1f, idleMax: 1f, observeSeconds: 2f, seed: 7);
        A("roam: starts at home", roam.Current == ProctorRoamModel.Phase.AtHome && !roam.IsWalking);
        roam.Tick(1.5f, true, true);                                     // idle expires
        A("roam: heads out to point 0", roam.Current == ProctorRoamModel.Phase.WalkingOut && roam.TargetIndex == 0 && roam.IsWalking);
        roam.Tick(0.1f, true, false);                                    // still walking
        A("roam: keeps walking until arrival", roam.Current == ProctorRoamModel.Phase.WalkingOut);
        roam.Tick(0.1f, true, true);                                     // arrived
        A("roam: observes on arrival", roam.Current == ProctorRoamModel.Phase.Observing);
        roam.Tick(2.5f, true, true);                                     // observation done
        A("roam: walks home after observing", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        roam.Tick(0.1f, true, true);
        A("roam: back home", roam.Current == ProctorRoamModel.Phase.AtHome);
        roam.Tick(1.5f, true, true);
        A("roam: round-robins to point 1", roam.TargetIndex == 1);
        roam.Tick(0.1f, false, false);                                   // quiz begins mid-walk
        A("roam: quiz forces him home", roam.Current == ProctorRoamModel.Phase.WalkingHome);
        var stay = new ProctorRoamModel(3, 1f, 1f, 2f, 7);
        stay.Tick(10f, false, true);
        A("roam: never leaves during quiz", stay.Current == ProctorRoamModel.Phase.AtHome);

        // Roamer stuck watchdog (Jimenez ran through a wall endlessly).
        float stuck = 0f;
        A("roam: progress resets the watchdog", !ProctorRoamer.StuckTick(ref stuck, 0.5f, 1f, 2f) && Near(stuck, 0f));
        A("roam: brief block is tolerated", !ProctorRoamer.StuckTick(ref stuck, 0f, 1f, 2f));
        A("roam: sustained block gives up", ProctorRoamer.StuckTick(ref stuck, 0f, 1.5f, 2f) && Near(stuck, 0f));

        // Spill puddle fade: full while lingering, smooth fade after.
        A("puddle: opaque while lingering", Near(SpillPuddle.Alpha01(0f, 3f, 1.2f), 1f) && Near(SpillPuddle.Alpha01(2.9f, 3f, 1.2f), 1f));
        A("puddle: half-faded mid-fade", Near(SpillPuddle.Alpha01(3.6f, 3f, 1.2f), 0.5f));
        A("puddle: gone after fade", Near(SpillPuddle.Alpha01(4.2f, 3f, 1.2f), 0f) && Near(SpillPuddle.Alpha01(9f, 3f, 1.2f), 0f));

        // Pharmee gate moods: happy default, warning on trouble.
        A("mood: friendly at the door", PharmeeMood.ExpressionForGate(GateState.ModeChoice) == PharmeeFaceExpression.Happy);
        A("mood: warns on supply trouble", PharmeeMood.ExpressionForGate(GateState.SupplyPrompt) == PharmeeFaceExpression.Warning);
        A("mood: celebrates the debrief", PharmeeMood.ExpressionForGate(GateState.Debrief) == PharmeeFaceExpression.Happy);
        A("mood: neutral while picking", PharmeeMood.ExpressionForGate(GateState.EpisodePick) == PharmeeFaceExpression.Neutral);

        // Sim-loop audio: verb → SoundBank key mapping, and safe without clips.
        A("sfx: heat loops bubble", SimLoopAudio.KeyFor(StationSim.Heat) == "bubble");
        A("sfx: filter loops drip", SimLoopAudio.KeyFor(StationSim.Filter) == "filter-drip");
        A("sfx: collect loops hiss", SimLoopAudio.KeyFor(StationSim.Collect) == "gas-hiss");
        A("sfx: crystallise loops shimmer", SimLoopAudio.KeyFor(StationSim.Crystallise) == "crystallise");
        var loopGo = new GameObject("loop");
        try
        {
            var loop = loopGo.AddComponent<SimLoopAudio>();
            loop.Bind("bubble");
            loop.SetRunning(true);       // no AudioService in edit mode → silent no-op
            loop.SetRunning(false);
            A("sfx: loop no-op without service", !loop.IsPlaying);
        }
        finally { UnityEngine.Object.DestroyImmediate(loopGo); }

        // Break → replacement flow: shattered item re-appears at its home spot.
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var rb = go.AddComponent<Rigidbody>(); rb.isKinematic = false;
            var respawn = go.AddComponent<DropRespawn>();
            respawn.Bind(rb, null);
            respawn.SetHome(new Vector3(2f, 1f, 2f), Quaternion.identity);
            var breakable = go.AddComponent<BreakableGlassware>();
            breakable.Bind(null, respawn, rb, "Test Beaker");
            go.transform.position = new Vector3(5f, 0f, 5f);
            breakable.Break();                                  // no runner → must not throw
            A("mishandle: break sends replacement home", (go.transform.position - new Vector3(2f, 1f, 2f)).magnitude < 1e-4f);
            A("mishandle: replacement re-frozen", rb.isKinematic);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void InteractionSuite()
    {
        // A trigger station requiring a specific LabItem accepts only that prop
        // (bring-the-right-thing-here), while an unset id accepts anything (legacy poke).
        var sgo = new GameObject("station");
        var igo = new GameObject("correct"); var wgo = new GameObject("wrong");
        try
        {
            var station = sgo.AddComponent<ExperimentTaskStation>();
            station.SetRequiredItemId("sodium-acetate");
            var correct = igo.AddComponent<LabItem>(); correct.SetItemId("sodium-acetate");
            var wrong = wgo.AddComponent<LabItem>(); wrong.SetItemId("soda-lime");
            A("interaction: station accepts matching item", station.AcceptsItem(correct));
            A("interaction: station rejects wrong item", !station.AcceptsItem(wrong));
            A("interaction: station rejects null item", !station.AcceptsItem(null));

            var open = new GameObject("open").AddComponent<ExperimentTaskStation>();
            A("interaction: unset id accepts anything", open.AcceptsItem(wrong) && open.AcceptsItem(null));
            UnityEngine.Object.DestroyImmediate(open.gameObject);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sgo);
            UnityEngine.Object.DestroyImmediate(igo);
            UnityEngine.Object.DestroyImmediate(wgo);
        }
    }

    // feedback layer: live vessel/station status formats + mix popups.
    static void FeedbackSuite()
    {
        // Vessel status formats.
        A("status: named contents", VesselStatusMath.Compose("Beaker", "Ethanol", 120.4f) == "Beaker — 120 ml Ethanol");
        A("status: empty vessel", VesselStatusMath.Compose("Beaker", null, 0f) == "Beaker — empty");
        A("status: unknown liquid", VesselStatusMath.Compose("Beaker", null, 55f) == "Beaker — 55 ml liquid");
        A("status: reagent bottle drops the echo", VesselStatusMath.Compose("Ethanol", "Ethanol", 150f) == "Ethanol — 150 ml");
        A("status: null name safe", VesselStatusMath.Compose(null, "Ethanol", 10f) == "Vessel — 10 ml Ethanol");
        // Mixed contents show every element AND its amount (clear text of the
        // current contents of the tube and their proportions).
        A("status: mixed contents list the proportions",
            VesselStatusMath.ComposeMixed("Test Tube 3", "Ethanol 1 ml + Distilled Water 10 ml")
                == "Test Tube 3 — Ethanol 1 ml + Distilled Water 10 ml"
            && VesselStatusMath.ComposeMixed("Test Tube 3", "") == "Test Tube 3 — empty");

        // Hover live line.
        A("hover: empty line", VesselStatusMath.HoverLine(null, 0f, "", 0) == "Now: empty");
        A("hover: contents line", VesselStatusMath.HoverLine("Ethanol", 120f, "", 1) == "Now: 120 ml Ethanol");
        A("hover: mixed story appended", VesselStatusMath.HoverLine("Ethanol", 170f, "Ethanol 120 ml + NaOH 50 ml", 2)
            == "Now: 170 ml Ethanol\nMixed from: Ethanol 120 ml + NaOH 50 ml");

        // Station billboards.
        A("station: heat line", VesselStatusMath.HeatLine("4. Heat", 61.7f, 150f) == "4. Heat\n62 C -> 150 C");
        A("station: progress line", VesselStatusMath.ProgressLine("5. Filter", "Filtering", 0.404f) == "5. Filter\nFiltering 40%");
        A("station: progress clamps", VesselStatusMath.ProgressLine("x", "Collecting", 1.7f) == "x\nCollecting 100%");

        // Formats survive the TMP glyph map unchanged (no boxed glyphs).
        A("status: glyph-safe", GlyphSafe.Sanitize(VesselStatusMath.HeatLine("Heat", 62f, 150f)) == "Heat\n62 C -> 150 C");

        // Mix-popup policy: hazards stay silent here (the reactor owns them).
        A("mixfeed: harmless mix announces", MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.None));
        A("mixfeed: toxic gas silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.ToxicGas));
        A("mixfeed: fire silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.FireOrExplosion));
        A("mixfeed: fizz silent", !MixFeedback.ShouldAnnounceWrongMix(HazardousMix.HazardOutcome.GenericFizz));

        // StationStatusLabel composes from live sims (edit-mode components).
        var go = new GameObject("fb_station");
        var lgo = new GameObject("fb_label");
        try
        {
            var temp = go.AddComponent<TemperatureSim>();
            var tmp = lgo.AddComponent<TMPro.TextMeshPro>();
            var lab = go.AddComponent<StationStatusLabel>();
            lab.Bind(tmp, "4. Heat", StationSim.Heat, temp, null, null, null, 150f);
            A("stationlabel: heat status composes", lab.ComposeStatus().StartsWith("4. Heat\n") && lab.ComposeStatus().EndsWith("-> 150 C"));
            var gas = go.AddComponent<GasCollection>();
            var lab2 = go.AddComponent<StationStatusLabel>();
            lab2.Bind(tmp, "6. Collect", StationSim.Collect, null, null, null, gas, 0f);
            gas.AddGas(25f);
            A("stationlabel: collect shows percent", lab2.ComposeStatus().Contains("Collecting"));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(lgo); }

        // Hover live-line cloning keeps title/category, appends "Now:".
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null;
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "FB_Chem";
            lp.SetContents(chem, 80f);
            var baseEntry = new LabInfoEntry("Beaker", LabInfoCategory.Equipment, "Base body.");
            var live = HoverInspector.WithLiveLine(baseEntry, lp);
            A("hover: live line appended", live.Title == "Beaker" && live.Body.Contains("Now: 80 ml FB_Chem"));
        }
        finally { UnityEngine.Object.DestroyImmediate(vgo); }
    }

    // tool verbs: stir/grind orbit math, weighing, match striking, burner
    // ignition, the methane splint, and the completesTask=false pour flag.
    static void VerbSuite()
    {
        // OrbitMath — swept-angle accumulation.
        var om = new OrbitMath { requiredRevs = 1f };
        for (int i = 0; i <= 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            om.Feed(Mathf.Cos(a), Mathf.Sin(a), true);
        }
        A("orbit: one full circle completes one rev", om.IsDone && Near(om.SweptDegrees, 360f, 0.5f));

        var om2 = new OrbitMath { requiredRevs = 1f };
        om2.Feed(Mathf.Cos(170f * Mathf.Deg2Rad), Mathf.Sin(170f * Mathf.Deg2Rad), true);
        om2.Feed(Mathf.Cos(-170f * Mathf.Deg2Rad), Mathf.Sin(-170f * Mathf.Deg2Rad), true);
        A("orbit: wraps across +/-180", Near(om2.SweptDegrees, 20f, 0.5f));

        var om3 = new OrbitMath { requiredRevs = 1f };
        om3.Feed(1f, 0f, true);
        om3.Feed(-1f, 0f, true);   // 180° teleport
        A("orbit: per-sample clamp blocks teleport cheats", Near(om3.SweptDegrees, OrbitMath.MaxDegPerSample, 0.5f));

        var om4 = new OrbitMath { requiredRevs = 1f };
        om4.Feed(1f, 0f, true);
        om4.Feed(Mathf.Cos(40f * Mathf.Deg2Rad), Mathf.Sin(40f * Mathf.Deg2Rad), true);
        float before = om4.SweptDegrees;
        om4.Feed(0f, 0f, false);                       // lift the rod out — pause
        om4.Feed(Mathf.Cos(220f * Mathf.Deg2Rad), Mathf.Sin(220f * Mathf.Deg2Rad), true);   // re-entry re-anchors
        A("orbit: leaving pauses, never resets", Near(om4.SweptDegrees, before, 0.5f));
        om4.Reset();
        A("orbit: reset clears", om4.SweptDegrees == 0f && !om4.IsDone);

        // WeighMath.
        A("weigh: contents mass 1g/ml", Near(WeighMath.MassOf(48.6f), 48.6f) && Near(WeighMath.MassOf(10f, 5f), 15f));
        A("weigh: tolerance pass", WeighMath.WithinTolerance(45f, 50f));
        A("weigh: tolerance fail", !WeighMath.WithinTolerance(44f, 50f));
        A("weigh: settle beat required", WeighMath.PanSettled(0.8f) && !WeighMath.PanSettled(0.5f));
        A("weigh: chemical mode satisfied", WeighMath.Satisfied(true, "Salicylic Acid", "Salicylic Acid", 48f, 50f, "", null));
        A("weigh: chemical mode short", !WeighMath.Satisfied(true, "Salicylic Acid", "Salicylic Acid", 30f, 50f, "", null));
        A("weigh: chemical mode wrong chem", !WeighMath.Satisfied(true, "Salicylic Acid", "Ethanol", 60f, 50f, "", null));
        A("weigh: item mode satisfied", WeighMath.Satisfied(true, "", null, 0f, 0f, "weigh-acetates", "weigh-acetates"));
        A("weigh: item mode wrong item", !WeighMath.Satisfied(true, "", null, 0f, 0f, "weigh-acetates", "prep-koh"));

        // ScoopMath: fixed charge per dip, solids only, no double-dip.
        A("scoop: dips only into solids", ScoopMath.CanPickUp(false, PhysicalState.Solid, 50f)
            && ScoopMath.CanPickUp(false, PhysicalState.Powder, 50f)
            && !ScoopMath.CanPickUp(false, PhysicalState.Liquid, 50f));
        A("scoop: no double-dip while loaded", !ScoopMath.CanPickUp(true, PhysicalState.Solid, 50f));
        A("scoop: empty jar gives nothing", !ScoopMath.CanPickUp(false, PhysicalState.Solid, 0f));
        A("scoop: fixed charge, last scoop takes the rest",
            Near(ScoopMath.ScoopCharge(50f), 2f) && Near(ScoopMath.ScoopCharge(1.2f), 1.2f));
        A("scoop: deposits anywhere but the source jar",
            ScoopMath.CanDeposit(true, false) && !ScoopMath.CanDeposit(true, true) && !ScoopMath.CanDeposit(false, false));
        A("scoop: deposit label shows the running total",
            ScoopMath.DepositLabel("Salicylic Acid", 2f, 8f) == "+2 g Salicylic Acid  (8 g total)");
        // The porcelain spatula: Exp 2 weighs 0.1 g salicylic and 0.5 g
        // aspirin — BOTH smaller than one 2 g scoopful, so the scoop cannot express
        // them at all. At 0.1 g/dip they become 1 and 5 dips.
        A("spatula: finer than the scoopula", ScoopMath.GramsPerSpatula < ScoopMath.GramsPerScoop);
        A("spatula: 0.1 g salicylic = one dip",
            Near(ScoopMath.ScoopCharge(50f, ScoopMath.GramsPerSpatula), 0.1f));
        A("spatula: 0.5 g aspirin = five dips",
            Near(ScoopMath.ScoopCharge(50f, ScoopMath.GramsPerSpatula) * 5f, 0.5f));

        // DropperMath: the liquid twin of the scoop. THE contract —
        // the number in the manuscript instruction IS the squeeze count, whatever
        // its unit ("5 drops" -> 5 squeezes; "2 ml of Tollen's" -> 2 squeezes).
        A("dropper: draws only from liquids",
            DropperMath.CanFill(0f, PhysicalState.Liquid, 50f)
            && !DropperMath.CanFill(0f, PhysicalState.Powder, 50f)
            && !DropperMath.CanFill(0f, PhysicalState.Solid, 50f));
        A("dropper: no top-up while loaded (an ambiguous count)",
            !DropperMath.CanFill(4f, PhysicalState.Liquid, 50f));
        A("dropper: empty bottle gives nothing", !DropperMath.CanFill(0f, PhysicalState.Liquid, 0f));
        // After the last squeeze, the empty dropper hovering over the tube must
        // NOT suck the delivered reagent back out. Task vessels are
        // destinations, never sources.
        A("dropper: never draws back out of a task vessel",
            !DropperMath.CanFill(0f, PhysicalState.Liquid, 50f, isTaskVessel: true));
        // Squeezing over nothing wastes the drop — that is how a dropper is emptied.
        A("dropper: a loaded squeeze over nothing wastes the drop",
            DropperMath.CanWaste(5f, overTarget: false)
            && !DropperMath.CanWaste(5f, overTarget: true)
            && !DropperMath.CanWaste(0f, overTarget: false));
        // The tool's own hover label carries the capacity readout.
        A("dropper: label reads chem and drops left",
            DropperMath.HoldingLabel("Tollen's Reagent", 7f) == "Tollen's Reagent · 7/10 drops"
            && DropperMath.HoldingLabel("", 0f) == "Dropper (empty)");
        A("dropper: a draw fills it, last draw takes the rest",
            Near(DropperMath.FillCharge(50f), DropperMath.Capacity) && Near(DropperMath.FillCharge(3f), 3f));
        A("dropper: squeezes only when loaded, over another vessel",
            DropperMath.CanSqueeze(5f, true, false)
            && !DropperMath.CanSqueeze(0f, true, false)
            && !DropperMath.CanSqueeze(5f, false, false)
            && !DropperMath.CanSqueeze(5f, true, true));
        A("dropper: one squeeze = one unit, last squeeze takes the rest",
            Near(DropperMath.SqueezeCharge(5f), 1f) && Near(DropperMath.SqueezeCharge(0.4f), 0.4f));
        // A full dropper covers the largest single Exp 2 instruction (10 drops of ethanol).
        A("dropper: a full charge is ten squeezes", DropperMath.SqueezesLeft(DropperMath.Capacity) == 10);
        A("dropper: count drains with each squeeze",
            DropperMath.SqueezesLeft(3f) == 3 && DropperMath.SqueezesLeft(0f) == 0);
        // The popup shows the COUNT, because the count is the measurement.
        A("dropper: squeeze label counts drops",
            DropperMath.SqueezeLabel("Ferric Chloride 10%", 3) == "drop 3  ·  Ferric Chloride 10%");
        A("dropper: fill label shows squeezes loaded",
            DropperMath.FillLabel("Tollen's Reagent", 10f) == "Tollen's Reagent  ·  10 drops loaded");
        // Charge-drain geometry: pivot-scaling shrinks the charge
        // symmetrically (it stays "nailed to the middle"); it must pool AT THE TIP, so
        // the capsule shifts toward the tip by exactly the half-length it lost.
        A("dropper: drain shortens the charge, floored at a readable sliver",
            Near(DropperMath.DrainScaleY(2f, 1f), 2f)
            && Near(DropperMath.DrainScaleY(2f, 0.5f), 1.2f)
            && Near(DropperMath.DrainScaleY(2f, 0f), 0.4f));
        A("dropper: the tip end stays anchored while the bulb end recedes",
            Near(DropperMath.DrainShift(1f, 2f, 1.2f), 0.8f)
            && Near(DropperMath.DrainShift(1f, 2f, 2f), 0f)
            && Near(DropperMath.DrainShift(0.5f, 2f, 1.2f), 0.4f));

        // TubeSlotMath: release a tube near a workspace holder and it
        // seats in the nearest FREE green slot. Held or already-frozen tubes are not
        // candidates (a seated tube must not be stolen by a neighbouring slot).
        {
            var slots = new[] { new Vector3(0, 0, 0), new Vector3(0.15f, 0, 0), new Vector3(0.30f, 0, 0) };
            var none = new[] { false, false, false };
            A("tubeslot: released tube takes the nearest free slot",
                TubeSlotMath.NearestFreeSlot(new Vector3(0.17f, 0.05f, 0f), slots, none) == 1);
            A("tubeslot: occupied slot passed over",
                TubeSlotMath.NearestFreeSlot(new Vector3(0.17f, 0.05f, 0f), slots, new[] { false, true, false }) == 2);
            A("tubeslot: out of reach snaps nothing",
                TubeSlotMath.NearestFreeSlot(new Vector3(2f, 0f, 0f), slots, none) == -1);
            A("tubeslot: held or frozen tubes are not candidates",
                !TubeSlotMath.CanSnap(held: true, kinematic: false)
                && !TubeSlotMath.CanSnap(held: false, kinematic: true)
                && TubeSlotMath.CanSnap(held: false, kinematic: false));
            // Seat by the mesh BOTTOM, never the pivot: these tube meshes carry the
            // pivot at the TOP, so pivot-to-slot sinks the 13 cm body under the table
            // (the seat sound plays and the tube "disappears").
            A("tubeslot: seating lifts a top-pivot tube onto the slot",
                Near(TubeSlotMath.BottomAlignDelta(0.917f, 0.787f), 0.13f)
                && Near(TubeSlotMath.BottomAlignDelta(1f, 1f), 0f));

            // Bench glass adopted by a layout gets its REAL capacity: the serialized
            // default of 1000 ml makes 5 counted drops a 0.5% (invisible) fill, which
            // reads as "no drop landed".
            A("bench: adopted tubes hold 25 ml, not a bucket",
                Near(ExperimentSceneBuilder.BenchMaxVolumeFor("TestTube", 1000f), 25f)
                && Near(ExperimentSceneBuilder.BenchMaxVolumeFor("Beaker_100mL", 1000f), 100f)
                && Near(ExperimentSceneBuilder.BenchMaxVolumeFor("SomethingElse", 321f), 321f));

            // Shared apparatus keeps a NEUTRAL label: a bench-bound vessel's floating
            // tag must come from the bench item ("Test Tube 3"), never the layout's
            // internal role name ("TollensTube_1" reads as reagent-dedicated glassware
            // and would go stale under the next module).
            A("bench: bound vessels are labelled by the bench item, not the layout role",
                ExperimentSceneBuilder.BenchDisplayNameFor("Kit_TestTube_3") == "Test Tube 3"
                && ExperimentSceneBuilder.BenchDisplayNameFor("Kit_Hard-GlassTestTube_2") == "Hard-Glass Test Tube 2"
                && ExperimentSceneBuilder.BenchDisplayNameFor("Eq_Beaker_100mL") == "Beaker 100 mL");

            // ZONE-FREE heat: the
            // water bath is a real tool (water first, flame second, then heat),
            // capped at boiling; its label always names the next thing it needs.
            A("waterbath: water then flame then heat",
                !WaterBathMath.IsHeating(false, true) && !WaterBathMath.IsHeating(true, false)
                && WaterBathMath.IsHeating(true, true) && WaterBathMath.HasWater(5f) && !WaterBathMath.HasWater(4f));
            A("waterbath: label narrates the missing piece",
                WaterBathMath.StatusLine(false, true, 25f) == "Water Bath — pour in distilled water"
                && WaterBathMath.StatusLine(true, false, 25f) == "Water Bath — needs a lit burner beside it"
                && WaterBathMath.StatusLine(true, true, 82.4f) == "Water Bath — 82 C");
            A("vesselheat: completes only served AND hot",
                !VesselHeatTask.ShouldComplete(false, 95f, 90f)
                && !VesselHeatTask.ShouldComplete(true, 80f, 90f)
                && VesselHeatTask.ShouldComplete(true, 90f, 90f));
        }
        // The SCENE wiring: a verb authored in code but never ATTACHED (a
        // DropperController with 0 instances in the scene) leaves the player
        // squeezing a dead prop. Pin the components onto the actual objects.
        {
            int droppers = 0, wired = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
            {
                if (!t.name.StartsWith("Eq_Dropper")) continue;
                droppers++;
                if (t.GetComponent<DropperController>() != null) wired++;
            }
            A("wired: every Eq_Dropper has the DropperController verb (found " + droppers + ")",
                droppers > 0 && wired == droppers);
            // The charge visuals are HAND-FITTED children (skinned-mesh bounds lie —
            // a bounds-derived tip floated the bead in mid-air): a
            // DropperTip anchor and a DropperLiquid capsule inside the stem.
            int tips2 = 0, liquids2 = 0, allDrop = 0, hidden2 = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<DropperController>(
                         FindObjectsInactive.Include))
            {
                allDrop++;
                if (t.transform.Find("DropperTip") != null) tips2++;
                var liq = t.transform.Find("DropperLiquid");
                if (liq != null) { liquids2++; if (!liq.gameObject.activeSelf) hidden2++; }
            }
            A("wired: every dropper has a hand-fit DropperTip", allDrop > 0 && tips2 == allDrop);
            A("wired: every dropper has a hand-fit DropperLiquid", allDrop > 0 && liquids2 == allDrop);
            // The capsule stays VISIBLE in the editor (the fitting aid must be
            // seen to be fitted) and Awake→CacheLiquid hides it the moment Play
            // starts — in play only actual contents show.
            A("wired: DropperLiquid capsules stay visible in the editor scene",
                liquids2 > 0 && hidden2 == 0);

            var spat = GameObject.Find("Eq_PorcelainSpatula");
            A("wired: the porcelain spatula dips fine (0.1 g)",
                spat != null && spat.GetComponent<ScoopController>() != null
                && Near(spat.GetComponent<ScoopController>().GramsPerDip, ScoopMath.GramsPerSpatula));
            // The two solids tools carry DIFFERENT heap shapes:
            // the porcelain spatula a flat elongated smear, the scoopula a rounded
            // bowl-pile — they must not look identical.
            var scoopula = GameObject.Find("Eq_Scoopula");
            A("wired: scoopula + spatula heaps are DIFFERENT shapes",
                spat != null && scoopula != null
                && spat.GetComponent<ScoopController>() != null && scoopula.GetComponent<ScoopController>() != null
                && Vector3.Distance(spat.GetComponent<ScoopController>().HeapScale,
                                    scoopula.GetComponent<ScoopController>().HeapScale) > 0.005f
                && spat.GetComponent<ScoopController>().HeapScale.y < scoopula.GetComponent<ScoopController>().HeapScale.y);

            int holders = 0, snapping = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
            {
                if (!t.name.StartsWith("Experiment_Tube_Table_Kit_Holder")) continue;
                holders++;
                var rs = t.GetComponent<TubeRackSlots>();
                if (rs != null && rs.SlotCount > 0) snapping++;
            }
            A("wired: every workspace holder snaps tubes (found " + holders + ")",
                holders > 0 && snapping == holders);
        }

        // MATERIALS guide: every module's watch panel opens with the
        // full gather-first list — reagents WITH totals derived from the layout
        // bindings, apparatus authored from the procedures. Not a checklist item;
        // always visible; the board keeps its scroll position between opens.
        {
            var lib5 = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            bool allHave = lib5 != null && lib5.Count > 0;
            if (lib5 != null)
                foreach (var mod in lib5.modules)
                    if (mod == null || mod.materialReagents.Count == 0 || mod.materialApparatus.Count == 0)
                    { allHave = false; break; }
            A("materials: every module has a gather-first guide", allHave);

            var m5b = lib5 != null ? lib5.Get("prelim-chemical-compounding") : null;
            A("materials: Exp 2's reagents carry totals",
                m5b != null && m5b.materialReagents.Exists(r => r.Contains("Ferric Chloride 10%") && r.Contains("—")));
            A("materials: Exp 2 lists its 19 tubes and 4 droppers",
                m5b != null && m5b.materialApparatus.Exists(a => a.Contains("19"))
                && m5b.materialApparatus.Exists(a => a.Contains("Dropper")));
            string hdr = ChecklistPager.BuildMaterialsHeader(m5b);
            A("materials: header renders both sections",
                hdr.Contains("MATERIALS") && hdr.Contains("Reagents:") && hdr.Contains("Apparatus:"));
            // One bullet ROW per item (a dot-joined line wraps
            // mid-name); every listed material starts its own "• " line.
            A("materials: every item is its own bullet row",
                m5b == null || hdr.Split('•').Length - 1
                    >= m5b.materialReagents.Count + m5b.materialApparatus.Count);
            A("materials: empty guide renders nothing",
                ChecklistPager.BuildMaterialsHeader(null) == "");
            // The procedures say "add distilled water" — the reagent bottle must carry
            // that exact name, or its hover and the wash bottle's both read "distilled
            // water" and the player can't tell which the step means.
            A("water: the procedure's 'distilled water' IS the reagent bottle's name",
                m5b != null && m5b.materialReagents.Exists(r => r.StartsWith("Distilled Water"))
                && !m5b.materialReagents.Exists(r => r.Contains("Purified")));
        }

        // FUME-HOOD RULE: the manuscript demands the
        // hood in exactly ONE place, Exp 5 §A, aniline + acetyl chloride. Any other
        // chem carrying the flag makes CORRECT bench play log FumeHoodViolations
        // (e.g. 4 per perfect Exp 2 run).
        {
            var flagged = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:ChemicalData",
                         new[] { "Assets/PharmaSynth/ScriptableObjects/Chemicals" }))
            {
                var c = AssetDatabase.LoadAssetAtPath<ChemicalData>(AssetDatabase.GUIDToAssetPath(g));
                if (c != null && c.requiresFumeHood) flagged.Add(c.chemicalName);
            }
            flagged.Sort();
            // + Potassium Dichromate (Exp 7 polish): its ONLY use is
            // chloroform's oxidation test, whose manuscript hint is "Fume hood!"
            // — flagging the chem makes the position-based hood sanction real.
            A("fumehood: ONLY the manuscript's hood chems require the hood"
              + (flagged.Count == 3 ? "" : "  [" + string.Join(", ", flagged) + "]"),
                flagged.Count == 3 && flagged[0] == "Acetyl Chloride" && flagged[1] == "Aniline"
                && flagged[2] == "Potassium Dichromate");
        }

        // WRAP-UP tasks: a closing beat with no physical verb
        // ("record your observations") auto-completes once every other task is
        // done; without this the checklist could never finish (a module would
        // deadlock on its final step).
        {
            var wrapTasks = new List<ExperimentTask>
            {
                new ExperimentTask { taskId = "a", label = "A" },
                new ExperimentTask { taskId = "b", label = "B" },
                new ExperimentTask { taskId = "w", label = "Wrap", autoCompleteWhenOthersDone = true },
            };
            var wg = new TaskGraph(wrapTasks);
            wg.Tick();
            A("wrapup: not before the real work is done", !wg.IsComplete("w"));
            wg.TryComplete("a"); wg.Tick();
            A("wrapup: one remaining real task still holds it", !wg.IsComplete("w"));
            wg.TryComplete("b"); wg.Tick();
            A("wrapup: completes itself once every other task is done", wg.IsComplete("w"));

            var lib6 = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
            var m6 = lib6 != null ? lib6.Get("prelim-chemical-compounding") : null;
            bool authored = false;
            if (m6 != null)
                foreach (var t in m6.graphTasks)
                    if (t.taskId == "record-observations" && t.autoCompleteWhenOthersDone) authored = true;
            A("wrapup: Exp 2's record-observations is authored as a wrap-up", authored);
        }

        // TIME-SKIP: a longProcess task fades black then returns
        // with a "time passed" message — the reusable compression for week-long
        // waits (Exp 3 fermentation and every later lengthy process).
        {
            var plain = new ExperimentTask { taskId = "x", label = "X" };
            var skip = new ExperimentTask { taskId = "ferment", label = "Ferment",
                longProcess = true, longProcessMessage = "One week later — fermentation complete." };
            var skipBlank = new ExperimentTask { taskId = "dry", label = "Dry", longProcess = true };
            A("timeskip: only a longProcess task triggers a skip",
                !TimeSkipController.IsTimeSkip(plain) && TimeSkipController.IsTimeSkip(skip)
                && !TimeSkipController.IsTimeSkip(null));
            A("timeskip: authored message wins, blank falls back",
                TimeSkipController.MessageFor(skip) == "One week later — fermentation complete."
                && TimeSkipController.MessageFor(skipBlank).Length > 0);
        }

        // SIMULATED RUN (catch the bugs here
        // before a manual test): every suite run PLAYS Exp 2 end-to-end through
        // the real wiring — builder, bindings, racks, station sims — with VERB-
        // CONTRACT action counts. Clean = every task completes, zero mistakes,
        // zero bugs, and every consumed chemical has a sufficient bench source.
        {
            var simLog = new System.Text.StringBuilder();
            var sim = SimulatedRun.Run("prelim-chemical-compounding", simLog);
            string simNote = sim == null ? "did not run"
                : sim.Clean ? "" : "  [" + sim.completedTasks + "/" + sim.totalTasks + " tasks, "
                  + sim.mistakes + " mistakes; " + string.Join(" | ", sim.bugs) + "]";
            A("simrun: Exp 2 plays END-TO-END clean" + simNote, sim != null && sim.Clean);

            // Exp 3 (ethyl alcohol): the zone-free ferment→CO₂→limewater mechanic,
            // the week-long time-skip, distillation + the three warm tests all
            // played through the real wiring.
            var sim3Log = new System.Text.StringBuilder();
            var sim3 = SimulatedRun.Run("prelim-ethyl-alcohol", sim3Log);
            string sim3Note = sim3 == null ? "did not run"
                : sim3.Clean ? "" : "  [" + sim3.completedTasks + "/" + sim3.totalTasks + " tasks, "
                  + sim3.mistakes + " mistakes; " + string.Join(" | ", sim3.bugs) + "]";
            A("simrun: Exp 3 plays END-TO-END clean" + sim3Note, sim3 != null && sim3.Clean);

            // Exp 4 (benzoic acid): oxidation heat-gate, the funnel filtration,
            // acidify crystals, the ICE-BATH chill + time-skip and the LITMUS
            // strip confirmation all played through the real wiring.
            var sim4Log = new System.Text.StringBuilder();
            var sim4 = SimulatedRun.Run("midterm-benzoic-acid", sim4Log);
            string sim4Note = sim4 == null ? "did not run"
                : sim4.Clean ? "" : "  [" + sim4.completedTasks + "/" + sim4.totalTasks + " tasks, "
                  + sim4.mistakes + " mistakes; " + string.Join(" | ", sim4.bugs) + "]";
            A("simrun: Exp 4 plays END-TO-END clean" + sim4Note, sim4 != null && sim4.Clean);

            // Exp 5 (acetanilide): the FUME-HOOD carry (aniline + acetyl
            // chloride sanctioned only inside the hood), the ice-water-then-
            // chill crystallisation, and the two-tube bromination comparison
            // all played through the real wiring.
            var sim5Log = new System.Text.StringBuilder();
            var sim5 = SimulatedRun.Run("midterm-acetanilide", sim5Log);
            string sim5Note = sim5 == null ? "did not run"
                : sim5.Clean ? "" : "  [" + sim5.completedTasks + "/" + sim5.totalTasks + " tasks, "
                  + sim5.mistakes + " mistakes; " + string.Join(" | ", sim5.bugs) + "]";
            A("simrun: Exp 5 plays END-TO-END clean" + sim5Note, sim5 != null && sim5.Clean);

            // Exp 6 (acetone): the bench-balance WEIGH, the NAKED-FLAME dry
            // distillation, the VAPOR collect at 56 °C, and all four tests
            // (two deliberate negatives) played through the real wiring.
            var sim6Log = new System.Text.StringBuilder();
            var sim6 = SimulatedRun.Run("midterm-acetone", sim6Log);
            string sim6Note = sim6 == null ? "did not run"
                : sim6.Clean ? "" : "  [" + sim6.completedTasks + "/" + sim6.totalTasks + " tasks, "
                  + sim6.mistakes + " mistakes; " + string.Join(" | ", sim6.bugs) + "]";
            A("simrun: Exp 6 plays END-TO-END clean" + sim6Note, sim6 != null && sim6.Clean);

            // Exp 7 (chloroform): the two water-bath distillations (heat task +
            // vapor collect, twice), the decant wash, the FLAME non-flammability
            // confirm, the hood-sanctioned dichromate oxidation and the warm
            // AgNO3 precipitate all played through the real wiring.
            var sim7Log = new System.Text.StringBuilder();
            var sim7 = SimulatedRun.Run("midterm-chloroform", sim7Log);
            string sim7Note = sim7 == null ? "did not run"
                : sim7.Clean ? "" : "  [" + sim7.completedTasks + "/" + sim7.totalTasks + " tasks, "
                  + sim7.mistakes + " mistakes; " + string.Join(" | ", sim7.bugs) + "]";
            A("simrun: Exp 7 plays END-TO-END clean" + sim7Note, sim7 != null && sim7.Clean);

            // Exp 8 (benzamide): the ice-bath-controlled synthesis, the
            // glass-rod STIR verb (tip-tracked), the filter/wash/dry time-skip,
            // and the two LITMUS reads (blue on ammonia, red on acid) plus the
            // nitrous effervescence all played through the real wiring.
            var sim8Log = new System.Text.StringBuilder();
            var sim8 = SimulatedRun.Run("final-benzamide", sim8Log);
            string sim8Note = sim8 == null ? "did not run"
                : sim8.Clean ? "" : "  [" + sim8.completedTasks + "/" + sim8.totalTasks + " tasks, "
                  + sim8.mistakes + " mistakes; " + string.Join(" | ", sim8.bugs) + "]";
            A("simrun: Exp 8 plays END-TO-END clean" + sim8Note, sim8 != null && sim8.Clean);

            // Exp 9 (winemaking): the must poured from the new juice bottle,
            // pitching yeast making Wine, the glass-rod rouse, the CO2→limewater
            // confirm + week time-skip, racking the clear wine off the lees, and
            // the clarity + tasting draws all played through the real wiring.
            var sim9Log = new System.Text.StringBuilder();
            var sim9 = SimulatedRun.Run("final-winemaking", sim9Log);
            string sim9Note = sim9 == null ? "did not run"
                : sim9.Clean ? "" : "  [" + sim9.completedTasks + "/" + sim9.totalTasks + " tasks, "
                  + sim9.mistakes + " mistakes; " + string.Join(" | ", sim9.bugs) + "]";
            A("simrun: Exp 9 plays END-TO-END clean" + sim9Note, sim9 != null && sim9.Clean);
        }

        // FERMENTATION (Exp 3): pure gates for the CO₂→limewater mechanic.
        {
            A("ferment: fermenting only after the must is prepared + in the flask",
                !FermentationMath.IsFermenting(false, 40f) && !FermentationMath.IsFermenting(true, 0f)
                && FermentationMath.IsFermenting(true, 40f));
            A("ferment: confirmed only once the limewater has actually clouded",
                !FermentationMath.CO2Confirmed(0f) && FermentationMath.CO2Confirmed(2f));
        }

        // ICE BATH + CHILL (Exp 4): the water bath's cold twin — set
        // a vessel in the bucket, anywhere, and the crystallise step completes
        // only once it actually holds product AND has gone cold (ambient 25 °C
        // can never satisfy it by standing around).
        A("icebath: chills only within reach", IceBathMath.Chills(0.2f) && !IceBathMath.Chills(0.5f));
        // Hand-scalable zone anchors: the wire-sphere gizmo's world
        // scale IS the diameter; half the X scale drives the tool's reach; no
        // anchor falls back to the coded constant.
        A("zone: anchor scale drives the effect radius",
            WaterBathMath.EffectRadius(1f, 0.32f) == 0.5f
            && WaterBathMath.EffectRadius(0f, 0.32f) == 0.32f
            && WaterBathMath.EffectRadius(0.64f, 0.99f) == 0.32f);
        {
            var bathT = GameObject.Find("WaterBath");
            var iceT = GameObject.Find("Raw_IceBucket");
            A("wired: bath + ice bucket carry user-scalable zone anchors",
                bathT != null && bathT.transform.Find("HeatZone") != null
                && bathT.transform.Find("BurnerZone") != null
                && iceT != null && iceT.transform.Find("ChillZone") != null);
        }
        // FUME HOOD guidance: the hood is an OPEN alcove — its
        // label guides idle and flips to a ✓ while a vessel is protected.
        A("fumehood: label guides idle and confirms protection",
            FumeHoodStatusLabel.StatusLine(null).Contains("IN here")
            && FumeHoodStatusLabel.StatusLine("Florence Flask") == "Fume Hood — Florence Flask protected ✓");
        // The hood model has NO colliders of its own — the shell is what stops
        // items passing through the walls; front stays OPEN (that's the door).
        {
            var hz = UnityEngine.Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
            var hshell = hz != null ? hz.transform.Find("HoodShell") : null;
            int solidWalls = 0;
            if (hshell != null)
                foreach (var wc in hshell.GetComponentsInChildren<BoxCollider>(true))
                    if (!wc.isTrigger) solidWalls++;
            A("fumehood: open-front collider shell (5 solid walls, trigger volume intact)",
                hshell != null && solidWalls == 5
                && hz.GetComponent<BoxCollider>() != null && hz.GetComponent<BoxCollider>().isTrigger);
        }
        // The hood's TEETH (edge case): a toxic pour with the vessel
        // at the open bench grades a FumeHoodViolation; the SAME pour with the
        // vessel inside the hood volume is sanctioned. Drives the REAL scene
        // hood through a probe vessel — position is the only variable.
        {
            var hz2 = UnityEngine.Object.FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include);
            var hzCol = hz2 != null ? hz2.GetComponent<Collider>() : null;
            var anilineChem = LoadChem("Chem_Aniline");
            var fhRunnerGo = new GameObject("fh_runner");
            var fhVesselGo = GameObject.CreatePrimitive(PrimitiveType.Cube);   // LiquidPhysics needs a Renderer host
            try
            {
                var fhRunner = fhRunnerGo.AddComponent<ExperimentRunner>();
                var fhMod = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
                fhMod.moduleId = "fh-probe";
                fhMod.graphTasks.Add(new ExperimentTask { taskId = "t", label = "T" });
                fhRunner.SetModule(fhMod); fhRunner.StartExperiment();
                var fhLp = fhVesselGo.AddComponent<LiquidPhysics>();
                var fhBind = fhVesselGo.AddComponent<LiquidTaskBinding>();
                fhBind.SetVesselAndRunner(fhLp, fhRunner);
                fhBind.SetFumeHood(hz2);
                fhBind.AddExpected(anilineChem, "t", 5f);
                fhVesselGo.transform.position = new Vector3(500f, 0f, 500f);   // nowhere near the hood
                fhBind.HandleReagent(anilineChem, 1f);
                int afterOutside = fhRunner.MistakeCount;
                if (hzCol != null) fhVesselGo.transform.position = hzCol.bounds.center;
                fhBind.HandleReagent(anilineChem, 1f);
                A("fumehood: outside pours violate, inside pours are sanctioned",
                    anilineChem != null && hz2 != null && hzCol != null
                    && afterOutside == 1 && fhRunner.MistakeCount == 1);
            }
            finally { UnityEngine.Object.DestroyImmediate(fhRunnerGo); UnityEngine.Object.DestroyImmediate(fhVesselGo); }
        }

        // Observation dedupe: a burst of identical squeezes fires
        // ONE popup/sting; a different rule always announces.
        A("mixfx: repeated identical observation stays quiet",
            !MixFeedback.ShouldShowObservation(true, 10f, 9f)
            && MixFeedback.ShouldShowObservation(true, 14f, 9f)
            && MixFeedback.ShouldShowObservation(false, 10f, 9.9f));
        // Live temp goal on heat/chill vessel tags: shows until the
        // goal side is reached, then yields to the step's own completion cue.
        A("vesselstatus: temp goal narrates until reached",
            VesselStatusMath.TempGoalLine(25f, 50f, false) == "25 C — warm to 50 C (water bath)"
            && VesselStatusMath.TempGoalLine(55f, 50f, false) == ""
            && VesselStatusMath.TempGoalLine(25f, 8f, true) == "25 C — chill to 8 C (ice bath)"
            && VesselStatusMath.TempGoalLine(5f, 8f, true) == "");
        A("chill: completes only served AND holding AND cold",
            VesselChillTask.ShouldComplete(true, true, IceBathMath.IceWaterC, 8f)
            && !VesselChillTask.ShouldComplete(false, true, IceBathMath.IceWaterC, 8f)   // ice water not in yet (Exp 5)
            && !VesselChillTask.ShouldComplete(true, false, IceBathMath.IceWaterC, 8f)
            && !VesselChillTask.ShouldComplete(true, true, 25f, 8f));

        // LITMUS (Exp 4): the strip reads the MIXTURE, not whichever
        // chemical landed first: an acid dominates any amount of water, so
        // "water first, then the product" cannot soft-lock the test.
        A("litmus: the extreme component dominates the mixture",
            LitmusMath.DominantPH(7f, 2.9f) == 2.9f && LitmusMath.DominantPH(2.9f, 7f) == 2.9f
            && LitmusMath.DominantPH(7f, 13.5f) == 13.5f);
        A("litmus: task needs served AND a definitive read",
            VesselLitmusTask.ShouldComplete(true, true) && !VesselLitmusTask.ShouldComplete(true, false)
            && !VesselLitmusTask.ShouldComplete(false, true));
        // Exp 8: the BASE read — ammonia vapour on alkaline
        // hydrolysis turns red litmus blue, and that read completes too.
        A("litmus: 10% NaOH actually reads BASE (authored pH)",
            LoadChem("Chem_SodiumHydroxide10") != null
            && LoadChem("Chem_SodiumHydroxide10").pH >= LitmusMath.BasePH
            && LitmusMath.ColorForPH(LoadChem("Chem_SodiumHydroxide10").pH) == LitmusMath.BaseBlue);
        {
            // DATA pin: the litmus lesson dies silently if a re-import ever
            // resets these pH values back to the un-authored 7.
            var benzoicChem = LoadChem("Chem_BenzoicAcid");
            var hcl6 = LoadChem("Chem_HydrochloricAcid6N");
            A("litmus: benzoic acid + 6N HCl actually read ACID (authored pH)",
                benzoicChem != null && benzoicChem.pH <= LitmusMath.AcidPH
                && hcl6 != null && hcl6.pH <= LitmusMath.AcidPH
                && LitmusMath.ColorForPH(benzoicChem.pH) == LitmusMath.AcidRed);
        }

        // NAKED FLAME + VAPOR + WEIGH (Exp 6 dry distillation).
        A("flame: reaches past the bath cap, only within reach",
            FlameMath.Heats(0.1f) && !FlameMath.Heats(0.3f)
            && FlameMath.NextTemp(100f, 1f) == 160f && FlameMath.NextTemp(390f, 1f) == FlameMath.MaxC
            && FlameMath.MaxC > WaterBathMath.BathMaxC);
        A("vapor: fires only hot AND charged",
            VaporMath.Fires(160f, 150f, 10f) && !VaporMath.Fires(100f, 150f, 10f)
            && !VaporMath.Fires(160f, 150f, 0.2f));
        A("weightask: completes only served AND settled on the pan",
            VesselWeighTask.ShouldComplete(true, true) && !VesselWeighTask.ShouldComplete(true, false)
            && !VesselWeighTask.ShouldComplete(false, true));

        // RackMath: a step shared by a SET of tubes is done only when
        // every member tube has had its reagent. Exp 2's five enol samples, its
        // butyl alcohols, acetone-vs-acetaldehyde: the comparison IS the lesson, so
        // one tube must never stand in for the set.
        A("rack: not ready until every tube is", !RackMath.AllReady(4, 5) && RackMath.AllReady(5, 5));
        A("rack: an empty group never auto-completes", !RackMath.AllReady(0, 0));
        A("rack: progress counts the set", RackMath.ProgressLabel(3, 5) == "tube 3 of 5");

        // CleanupMath: residue on empty, five swipes clean, rinse helps.
        A("clean: emptying a used vessel leaves residue", CleanupMath.BecomesDirty(60f, true)
            && !CleanupMath.BecomesDirty(60f, false) && !CleanupMath.BecomesDirty(0f, true));
        A("clean: five swipes scrub it clean", Near(CleanupMath.AfterSwipe(100f), 80f)
            && CleanupMath.AfterSwipe(15f) == 0f);
        A("clean: wash-bottle rinse also cleans", CleanupMath.AfterRinse(100f, 90f) == 0f
            && Near(CleanupMath.AfterRinse(100f, 10f), 88f));
        // residue never reaches the label. The prefix is all cleaning would show,
        // and to a player a vessel that says Dirty is a vessel they must clean.
        {
            var labelGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var testChem = ScriptableObject.CreateInstance<ChemicalData>(); testChem.chemicalName = "Ethanol";
            try
            {
                var lpL = labelGo.AddComponent<LiquidPhysics>();
                var cvL = labelGo.AddComponent<CleanableVessel>(); cvL.Bind(lpL);
                cvL.Soil();
                var tag = labelGo.AddComponent<ProximityLabel>();
                var vs = labelGo.AddComponent<VesselStatus>();
                vs.Bind(lpL, tag, "Beaker");
                vs.Refresh();
                A("clean: the label never carries a residue prefix",
                    tag.CurrentText != null && !tag.CurrentText.StartsWith("Dirty") && !tag.CurrentText.StartsWith("Clean"));
            }
            finally { UnityEngine.Object.DestroyImmediate(labelGo); UnityEngine.Object.DestroyImmediate(testChem); }
        }

        // a SOLID never pours; solids are scooped. Without the state check a tilted jar of
        // sugar streams a brown liquid column.
        {
            var liq = ScriptableObject.CreateInstance<ChemicalData>(); liq.state = PhysicalState.Liquid;
            var sol = ScriptableObject.CreateInstance<ChemicalData>(); sol.state = PhysicalState.Solid;
            var pow = ScriptableObject.CreateInstance<ChemicalData>(); pow.state = PhysicalState.Powder;
            try
            {
                A("pour: a solid never pours", PourMath.CanPour(liq) && !PourMath.CanPour(sol)
                    && !PourMath.CanPour(pow) && !PourMath.CanPour(null));
            }
            finally { UnityEngine.Object.DestroyImmediate(liq); UnityEngine.Object.DestroyImmediate(sol); UnityEngine.Object.DestroyImmediate(pow); }
        }

        // ONE lab reset restores the apparatus (empty, cool, unclaimed, unlabelled) and
        // refills the reagents WITHOUT refilling the glassware it just emptied (the supply
        // monitor would otherwise restock any binding-less vessel from its last chemical).
        {
            var app = GameObject.CreatePrimitive(PrimitiveType.Cube); app.name = "Eq_ResetProbe";
            var jar = GameObject.CreatePrimitive(PrimitiveType.Cube); jar.name = "Raw_ResetProbe";
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "Probe Acid";
            try
            {
                var lpA = app.AddComponent<LiquidPhysics>(); lpA.maxVolume = 100f; lpA.SetContents(chem, 30f);
                var cvA = app.AddComponent<CleanableVessel>(); cvA.Bind(lpA); cvA.Soil();
                var vsA = app.AddComponent<VesselStatus>(); vsA.SetRoleSuffix("Tollen's test");
                var bindA = app.AddComponent<LiquidTaskBinding>(); bindA.SetVesselAndRunner(lpA, null);
                var role = new List<LiquidTaskBinding.ReagentStep>
                    { new LiquidTaskBinding.ReagentStep { reagent = chem, taskId = "probe-task", requiredMl = 10f } };
                bindA.MarkPoolMember();
                bindA.SetRoles(new List<List<LiquidTaskBinding.ReagentStep>> { role }, -1, new RackRoles());
                bool claimed = bindA.ClaimForTask("probe-task") && bindA.ClaimedRole == 0;

                var lpJ = jar.AddComponent<LiquidPhysics>(); lpJ.maxVolume = 150f; lpJ.SetContents(chem, 40f);

                int touched = LabReset.ResetApparatus();

                A("reset: a lab reset empties, cools, un-claims and un-labels every apparatus and refills every reagent",
                    claimed && touched >= 1
                    && lpA.currentLiquidVolume <= 0.01f                      // emptied, and NOT restocked
                    && cvA.Dirtiness <= 0f && !cvA.EverDirty                 // residue gone
                    && bindA.ClaimedRole == -1                               // role released through Emptied
                    && Near(lpJ.currentLiquidVolume, 150f));                 // the reagent refilled
                // The RULE, on the real scene: a used vessel carries CleanableVessel; a SOURCE never
                // does. A name rule ("not Raw_") sweeps the wash bottle and the methane charge jar
                // into the reset and starves the tutorial's setup step.
                var flor = GameObject.Find("FlorenceFlask");
                var wash = GameObject.Find("Eq_WashBottle");
                var charge = GameObject.Find("Prop_reagent-jar");
                A("reset: a used vessel is one the builders gave a CleanableVessel; sources are never emptied",
                    flor != null && LabReset.IsUsedVessel(flor.GetComponent<LiquidPhysics>())
                    && wash != null && !LabReset.IsUsedVessel(wash.GetComponent<LiquidPhysics>())
                    && charge != null && !LabReset.IsUsedVessel(charge.GetComponent<LiquidPhysics>())
                    && !LabReset.IsUsedVessel(null)
                    && LabReset.IsReagent("Raw_Ethanol") && !LabReset.IsReagent("Eq_Beaker_100mL"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(app); UnityEngine.Object.DestroyImmediate(jar);
                UnityEngine.Object.DestroyImmediate(chem);
            }
        }

        // every solid jar on the shelf shows a heap, never a liquid column (the PowderJar
        // prefab carries only a Liquid surface). The scoop keys on the chemical's state,
        // so it works either way and can't catch this.
        {
            int solidJars = 0, noHeap = 0; string worstJar = "";
            foreach (var lp in UnityEngine.Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include))
            {
                if (lp == null || !LabReset.IsReagent(lp.name) || !SolidJarFitter.IsHeap(lp.currentChemical)) continue;
                solidJars++;
                var heap = lp.transform.Find("Powder");
                var heapR = heap != null ? heap.GetComponent<Renderer>() : null;
                bool ok = heapR != null && heapR.enabled
                          && !LiquidPhysics.DrawsLiquidColumn(lp.currentLiquidVolume, lp.DryPowder, true);
                if (!ok) { noHeap++; if (worstJar.Length == 0) worstJar = lp.name; }
            }
            A("jar: every solid shelf jar draws a heap, not a liquid column (" + solidJars + " jars, " + noHeap
              + " without" + (worstJar.Length > 0 ? ", e.g. " + worstJar : "") + ")", solidJars > 0 && noHeap == 0);
        }
        A("weigh: open mode any settled load", WeighMath.Satisfied(true, "", null, 0f, 0f, "", null));
        A("weigh: unsettled never", !WeighMath.Satisfied(false, "", null, 0f, 0f, "", null));

        // Match striking + burner ignition + splint.
        A("strike: held swipe on striker lights", Matchstick.ShouldStrike(true, false, false, 0.5f, true));
        A("strike: unheld never", !Matchstick.ShouldStrike(false, false, false, 0.5f, true));
        A("strike: slow rub never", !Matchstick.ShouldStrike(true, false, false, 0.2f, true));
        A("strike: plain surface never", !Matchstick.ShouldStrike(true, false, false, 0.5f, false));
        A("strike: lit/spent never", !Matchstick.ShouldStrike(true, true, false, 0.5f, true)
            && !Matchstick.ShouldStrike(true, false, true, 0.5f, true));
        A("burner: lit match near ignites", BurnerController.ShouldIgnite(false, true, 0.1f));
        A("burner: unlit match never", !BurnerController.ShouldIgnite(false, false, 0.1f));
        A("burner: far match never", !BurnerController.ShouldIgnite(false, true, 0.5f));
        A("burner: already lit no-op", !BurnerController.ShouldIgnite(true, true, 0.1f));
        // Live instrument readout: only shows while something is
        // happening; reuses the pinned station formats so it reads the same.
        A("readout: hidden at ambient", !ProcessReadout.ShouldShow(true, 22f, 22f, false, 0f));
        A("readout: shows once heating", ProcessReadout.ShouldShow(true, 60f, 22f, false, 0f));
        A("readout: shows while collecting", ProcessReadout.ShouldShow(false, 0f, 22f, true, 0.2f));
        A("readout: hidden with nothing collected", !ProcessReadout.ShouldShow(false, 0f, 22f, true, 0f));
        // Grinding needs the reagent IN the mortar (you could grind
        // an empty mortar while the powder sat on the scoop).
        A("grind: empty mortar can't grind", !GrindController.CanGrind(true, 0f));
        A("grind: loaded mortar grinds", GrindController.CanGrind(true, 2f));
        A("grind: cosmetic mortar always grinds", GrindController.CanGrind(false, 0f));
        // Powder must NOT take its size from a POSITION-ONLY anchor. A BowlAnchor has
        // scale 1, so sizing from it produced a 1-unit beach-ball in the mortar.
        // Only an anchor that declares previewsScale supplies the size.
        {
            var powderHost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            try
            {
                var chem = AssetDatabase.LoadAssetAtPath<ChemicalData>(
                    "Assets/PharmaSynth/ScriptableObjects/Chemicals/Chem_SodiumAcetate.asset");
                var bowl = new GameObject("BowlAnchor");
                bowl.transform.SetParent(powderHost.transform, false);
                bowl.AddComponent<PlacementAnchor>().previewsScale = false;   // position-only
                ExperimentSceneBuilder.EnsurePowderVisual(powderHost, chem, 1f);
                Transform heap = powderHost.transform.Find("Powder");
                // Guard is "not a 1-unit beach-ball", not a tight bound: a Cylinder
                // primitive's bounds are (1,2,1) → bore 1 → w = bore*0.5 = 0.5 EXACTLY,
                // so "< 0.5f" would fail on the boundary it isn't aiming at.
                A("powder: position-only anchor does NOT set size",
                  heap != null && heap.localScale.x < 0.6f && heap.localScale.y < 0.6f);
            }
            finally { UnityEngine.Object.DestroyImmediate(powderHost); }
        }

        // HUD dropdown re-fits to however many items are visible — the demo verbs
        // live in it and are hidden outside a demo session.
        // pad + items*h + (items-1)*gap = 16 + 168 + 12 = 196 (the 6-item and empty
        // cases below pin the same formula).
        A("hudmenu: 3 items (no demo)", Near(HudMenuDropdown.HeightFor(3, 56f, 6f, 16f), 196f));
        A("hudmenu: 6 items (demo session)", Near(HudMenuDropdown.HeightFor(6, 56f, 6f, 16f), 382f));
        A("hudmenu: empty list collapses to padding", Near(HudMenuDropdown.HeightFor(0, 56f, 6f, 16f), 16f));

        // Quiz is NEVER score-gated (client rule) — the score is just shown plainly.
        A("quiz: score line reads correct/total", PostLabController.ScoreLine(2, 3) == "Quiz: 2 / 3 (67%)");
        A("quiz: perfect score line", PostLabController.ScoreLine(3, 3) == "Quiz: 3 / 3 (100%)");
        A("quiz: no bank → no score line", PostLabController.ScoreLine(0, 0) == "");
        // Review navigation: Back/Next bound by the ends of the bank.
        A("quiz: no back on first", !PostLabController.CanGoBack(0));
        A("quiz: back from second", PostLabController.CanGoBack(1));
        A("quiz: next while more remain", PostLabController.CanGoNext(0, 3));
        A("quiz: no next on last", !PostLabController.CanGoNext(2, 3));
        // Red-hot tube glow: cold below 60% of target, saturated at temperature.
        A("glow: cold tube does not glow", Near(MethaneApparatusRig.GlowFor(22f, 120f), 0f));
        A("glow: saturates at target", Near(MethaneApparatusRig.GlowFor(120f, 120f), 1f));
        A("glow: ramps mid-climb", MethaneApparatusRig.GlowFor(100f, 120f) > 0f
            && MethaneApparatusRig.GlowFor(100f, 120f) < 1f);
        A("splint: lit match at tube fires", MethaneApparatusRig.SplintShouldFire(true, false, 0.1f, true, 1f));
        A("splint: auto fallback fires", MethaneApparatusRig.SplintShouldFire(true, false, float.MaxValue, false, 25f));
        A("splint: waits for a match first", !MethaneApparatusRig.SplintShouldFire(true, false, float.MaxValue, false, 5f));
        A("splint: not collected never", !MethaneApparatusRig.SplintShouldFire(false, false, 0.1f, true, 30f));
        A("splint: fires once", !MethaneApparatusRig.SplintShouldFire(true, true, 0.1f, true, 30f));
        // location-free: proximity reach check (heat/collect/assemble).
        A("reach: within fires", MethaneApparatusRig.WithinReach(0.2f, 0.35f));
        A("reach: beyond doesn't", !MethaneApparatusRig.WithinReach(0.5f, 0.35f));

        // Stir end-to-end: circle the rod → TaskGraph condition completes.
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.graphTasks = new List<ExperimentTask> {
            T("stir-x", TaskPhase.Synthesis, 1, LabSkill.Transfer, RubricCategory.Procedure),
        };
        module.trackedSkills = new List<LabSkill> { LabSkill.Transfer };
        var rgo = new GameObject("verb_runner");
        var vgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        var rodGo = new GameObject("verb_rod");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            runner.StartExperiment();
            var lp = vgo.AddComponent<LiquidPhysics>();
            lp.mainRenderer = null;
            var chem = ScriptableObject.CreateInstance<ChemicalData>(); chem.chemicalName = "VB_Chem";
            lp.SetContents(chem, 60f);
            var stir = vgo.AddComponent<StirController>();
            stir.Bind(runner, "stir-x", lp, rodGo.transform, requiredRevs: 1f);
            for (int i = 0; i <= 9; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                stir.Tick(Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, true);
            }
            runner.Graph.Tick();
            A("stir: circling the rod completes the task", runner.Graph.IsComplete("stir-x"));

            // Weigh end-to-end via ForceLoad.
            var module2 = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module2.graphTasks = new List<ExperimentTask> {
                T("weigh-x", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            };
            module2.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            runner.SetModule(module2);
            runner.StartExperiment();
            var wsGo = new GameObject("verb_pan");
            try
            {
                var ws = wsGo.AddComponent<WeighStation>();
                ws.Bind(runner, "weigh-x", "", "VB_Chem", 50f, null);
                A("weigh: empty pan not satisfied", !ws.IsSatisfied);
                ws.ForceLoad(null, lp, 1.5f);
                A("weigh: right vessel settled satisfies", ws.IsSatisfied);
                runner.Graph.Tick();
                A("weigh: task completes on the pan", runner.Graph.IsComplete("weigh-x"));
            }
            finally { UnityEngine.Object.DestroyImmediate(wsGo); }
            UnityEngine.Object.DestroyImmediate(module2);

            // completesTask=false: pours accumulate + flag ready, never complete.
            var module3 = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
            module3.graphTasks = new List<ExperimentTask> {
                T("weigh-y", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            };
            module3.trackedSkills = new List<LabSkill> { LabSkill.Measuring };
            runner.SetModule(module3);
            runner.StartExperiment();
            var bind = vgo.AddComponent<LiquidTaskBinding>();
            bind.SetVesselAndRunner(lp, runner);
            bind.AddExpected(chem, "weigh-y", 40f, completesTask: false);
            bind.HandleReagent(chem, 50f);
            A("bindflag: threshold met but not completed", !runner.Graph.IsComplete("weigh-y"));
            A("bindflag: readiness flagged", bind.ReadyFor("weigh-y"));
            UnityEngine.Object.DestroyImmediate(module3);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(vgo);
            UnityEngine.Object.DestroyImmediate(rodGo);
            UnityEngine.Object.DestroyImmediate(module);
        }
    }

    const string LayoutDir = "Assets/PharmaSynth/ScriptableObjects/Layouts";

    /// EVERY layout's taskIds must exist in its module's graph, and every task the
    /// graph declares must have some way to be completed.
    ///
    /// A layout whose stations complete a different task set than its module's graph
    /// (e.g. a retired battery left in place after the module is rebuilt, ZERO overlap)
    /// builds a stage with no errors, and not one task can ever complete. Only a test
    /// comparing the two halves catches it.
    static void LayoutGraphCoverageSuite()
    {
        const string LDir = "Assets/PharmaSynth/ScriptableObjects/Layouts";
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        A("cover: library loads", lib != null);
        if (lib == null) return;

        bool allKnown = true, allReachable = true;
        var orphanNote = ""; var deadNote = "";
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LDir }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
            if (layout == null || string.IsNullOrEmpty(layout.moduleId)) continue;
            var module = lib.Get(layout.moduleId);
            if (module == null) continue;

            var graphIds = new HashSet<string>();
            foreach (var t in module.graphTasks) graphIds.Add(t.taskId);

            // (a) Nothing in the layout may name a task the graph doesn't have.
            var layoutIds = new HashSet<string>();
            foreach (var s in layout.stations) if (!string.IsNullOrEmpty(s.taskId)) layoutIds.Add(s.taskId);
            foreach (var v in layout.vessels) foreach (var b in v.bindings)
                if (!string.IsNullOrEmpty(b.taskId)) layoutIds.Add(b.taskId);
            foreach (var id in layoutIds)
                if (!graphIds.Contains(id))
                { allKnown = false; orphanNote = layout.name + " -> " + id; }

            // (b) Every graph task needs an owner: a station, a completing binding,
            //     a rack group (deferred bindings whose members share a rackGroup),
            //     or a zone-free heat vessel (heatToC > 0 — VesselHeatTask).
            foreach (var t in module.graphTasks)
            {
                bool owned = false;
                foreach (var s in layout.stations) if (s.taskId == t.taskId) owned = true;
                foreach (var v in layout.vessels)
                {
                    if (v.fermentTaskId == t.taskId) owned = true;   // FermentationController condition
                    if (v.chillTaskId == t.taskId && v.chillToC > 0f) owned = true;   // VesselChillTask (ice bath)
                    if (v.litmusTaskId == t.taskId) owned = true;    // VesselLitmusTask (strip touch)
                    if (v.heatTaskId == t.taskId && v.heatToC > 0f) owned = true;     // explicit VesselHeatTask (open flame)
                    if (v.weighTaskId == t.taskId) owned = true;     // VesselWeighTask (bench balance)
                    if (v.vaporTaskId == t.taskId) owned = true;     // VaporCollectController (source side)
                    if (v.flameTaskId == t.taskId) owned = true;     // VesselFlameTask (lit-match confirm, Exp 7)
                    if (v.stirTaskId == t.taskId) owned = true;      // StirController (glass-rod stir, Exp 8)
                    foreach (var b in v.bindings)
                        if (b.taskId == t.taskId
                            && (b.completesTask || !string.IsNullOrEmpty(v.rackGroup) || v.heatToC > 0f))
                            owned = true;
                }
                // DataSheet steps may be closed by the quiz/tablet rather than the stage.
                if (!owned && t.phase != TaskPhase.DataSheet)
                { allReachable = false; deadNote = layout.name + " -> " + t.taskId; }
            }
        }
        A("cover: no layout names a task its graph lacks" + (allKnown ? "" : "  [" + orphanNote + "]"), allKnown);
        A("cover: every graph task has a completion owner" + (allReachable ? "" : "  [" + deadNote + "]"), allReachable);
    }

    // builder wiring for the verb stations (pins the applied layout data).
    static void VerbWiringSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var layouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            layouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));
        if (lib == null || layouts.Count == 0) { A("verbwire: assets available", false); return; }

        // Layout data applied (by the verb-data applier menu).
        // Acetone covers the Weigh verb.
        ExperimentLayout acetone = null, benzamide = null;
        foreach (var l in layouts)
        {
            if (l.moduleId == "midterm-acetone") acetone = l;
            if (l.moduleId == "final-benzamide") benzamide = l;
        }
        // Acetone is zone-free: the WEIGH lives on the BENCH
        // balance (Eq_Balance: pan trigger + station + grams display,
        // wired by the verb-data applier), not on a layout station.
        var balT = GameObject.Find("Eq_Balance");
        A("verbwire: bench balance carries the weigh rig (pan + station + display)",
            balT != null && balT.transform.Find("Pan") != null
            && balT.transform.Find("Pan").GetComponent<WeighStation>() != null
            && balT.GetComponent<WeighingScaleController>() != null);
        A("verbwire: burners carry the naked flame",
            UnityEngine.Object.FindAnyObjectByType<NakedFlameHeat>(FindObjectsInactive.Include) != null);
        // Zone-free since the Exp 8 polish: the stir is authored as
        // Vessel.stirTaskId on the chilled reaction beaker, not a Stir station.
        bool benzStir = false;
        if (benzamide != null) foreach (var v in benzamide.vessels) if (v.stirTaskId == "stir-stand") benzStir = true;
        A("verbwire: benzamide stir authored", benzStir);

        // Builder wires the controllers (fresh runner, no graph — Register no-ops).
        var rgo = new GameObject("vw_runner"); var bgo = new GameObject("vw_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, layouts);

            builder.Build("final-benzamide");
            // Bench-bound: the StirController sits on the bench beaker (outside
            // the stage root), so look scene-wide.
            A("verbwire: benzamide rod stirs the vessel",
                UnityEngine.Object.FindAnyObjectByType<StirController>(FindObjectsInactive.Include) != null);

            // (The acetone balance/burner assertions are the BENCH pins above.)
        }
        finally
        {
            // Benzamide still spawns, but tear the bench down anyway — cheap
            // insurance against future bench-bound conversions leaking.
            var b3 = bgo.GetComponent<ExperimentSceneBuilder>();
            if (b3 != null) b3.Build("tutorial-methane");
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
        }
    }

    static GameObject FindChildByName(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    // table pass: every layout re-zoned onto the deck with real spacing.
    static void LayoutSpacingSuite()
    {
        // The zoning grid itself keeps its promises (one full station row = the
        // busiest module's 7 stations).
        bool stationPitch = true;
        for (int i = 0; i < LayoutTidyMath.StationsPerRow - 1; i++)
            for (int j = i + 1; j < LayoutTidyMath.StationsPerRow; j++)
                if (Vector3.Distance(LayoutTidyMath.StationPos(i), LayoutTidyMath.StationPos(j)) < LayoutTidyMath.MinStationDistance - 0.01f)
                    stationPitch = false;
        A("tidy: station row keeps 0.5 m pitch", stationPitch);

        var slots = new List<Vector3>();
        for (int i = 0; i < LayoutTidyMath.StationsPerRow; i++) slots.Add(LayoutTidyMath.StationPos(i));
        for (int i = 0; i < 3; i++) slots.Add(LayoutTidyMath.VesselPos(i));
        for (int i = 0; i < 8; i++) slots.Add(LayoutTidyMath.ReagentPos(i));
        for (int i = 0; i < 6; i++) slots.Add(LayoutTidyMath.ToolPos(i));
        slots.Add(LayoutTidyMath.RackPos);
        for (int i = 0; i < 3; i++) slots.Add(LayoutTidyMath.SparePos(i));
        for (int i = 0; i < 2; i++) slots.Add(LayoutTidyMath.MatchPos(i));
        slots.Add(LayoutTidyMath.StrikerPos);
        bool zonesApart = true, zonesOnDeck = true;
        for (int i = 0; i < slots.Count; i++)
        {
            if (!LayoutTidyMath.OnDeck(slots[i])) zonesOnDeck = false;
            for (int j = i + 1; j < slots.Count; j++)
                if (Vector3.Distance(slots[i], slots[j]) < LayoutTidyMath.MinPairDistance) zonesApart = false;
        }
        A("tidy: all grid slots on the deck", zonesOnDeck);
        A("tidy: no two grid slots collide", zonesApart);

        // Every authored layout obeys the invariants (pins the Tidy run).
        int badDeck = 0, badPair = 0, badStations = 0;
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
        {
            var layout = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
            if (layout == null) continue;
            var all = new List<Vector3>();
            foreach (var s in layout.stations) all.Add(s.pos);
            foreach (var p in layout.props) all.Add(p.pos);
            // A BENCH-BOUND vessel's pos is IGNORED (the object already exists and
            // is hand-placed), so it can't overlap or fall off the deck — only
            // SPAWNED vessels are laid out by the grid.
            foreach (var v in layout.vessels) if (string.IsNullOrEmpty(v.benchItem)) all.Add(v.pos);
            foreach (var p in all) if (!LayoutTidyMath.OnDeck(p)) badDeck++;
            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                    if (Vector3.Distance(all[i], all[j]) < LayoutTidyMath.MinPairDistance) badPair++;
            for (int i = 0; i < layout.stations.Count; i++)
                for (int j = i + 1; j < layout.stations.Count; j++)
                    if (Vector3.Distance(layout.stations[i].pos, layout.stations[j].pos) < LayoutTidyMath.MinStationDistance) badStations++;
        }
        A("tidy: every layout position on the deck", badDeck == 0);
        A("tidy: no overlapping layout items (historical (1.38,-3.88) pairs fixed)", badPair == 0);
        A("tidy: stations keep verb elbow room", badStations == 0);
    }

    // center-table shelf platforms — geometry invariants.
    static void WorkspaceShelfSuite()
    {
        const int n = 3;
        // Tiles rest ON the rail tops (matches the hand-laid plank top 1.563).
        A("shelf: top sits on the rails", Near(WorkspaceShelfMath.TopY, 1.563f, 0.001f));

        // Tiles tile the full width with only the seam gap between them, and
        // stay inside [XMin, XMax].
        bool spanOk = true, seamOk = true;
        float prevRight = WorkspaceShelfMath.XMin;
        for (int i = 0; i < n; i++)
        {
            var c = WorkspaceShelfMath.TileCenter(i, n);
            var s = WorkspaceShelfMath.TileSize(n);
            float left = c.x - s.x * 0.5f, right = c.x + s.x * 0.5f;
            if (left < WorkspaceShelfMath.XMin - 0.001f || right > WorkspaceShelfMath.XMax + 0.001f) spanOk = false;
            float seam = left - prevRight;   // gap from the previous tile's right edge
            if (i > 0 && seam > WorkspaceShelfMath.Gap + 0.001f) seamOk = false;   // no gap wider than the seam
            prevRight = right;
        }
        A("shelf: tiles stay within the rail width", spanOk);
        A("shelf: no gap wider than the seam (equipment seats anywhere)", seamOk);
        A("shelf: last tile reaches the right rail end", Near(prevRight, WorkspaceShelfMath.XMax, WorkspaceShelfMath.Gap + 0.001f));

        // Each tile bridges both rails (front z=-3.15, back z=-3.50).
        var size = WorkspaceShelfMath.TileSize(n);
        float zFrontEdge = WorkspaceShelfMath.ZCenter + size.z * 0.5f;
        float zBackEdge = WorkspaceShelfMath.ZCenter - size.z * 0.5f;
        A("shelf: tile bridges both rails", zFrontEdge >= -3.16f && zBackEdge <= -3.49f);
        A("shelf: tiles are thin planks", Near(size.y, WorkspaceShelfMath.Thickness) && size.y < 0.05f);

        // second row (the hand-laid lower planks at y≈1.20): below the top
        // row with real headroom for glassware, same footprint.
        A("shelf: two rows", WorkspaceShelfMath.Rows == 2);
        A("shelf: lower row at the user's plank height", Near(WorkspaceShelfMath.TopYOf(1), 1.215f, 0.001f));
        A("shelf: lower row has glassware headroom", WorkspaceShelfMath.LowerRowHeadroom > 0.25f);
        A("shelf: rows share the rail footprint",
            Near(WorkspaceShelfMath.TileCenter(0, n, 1).z, WorkspaceShelfMath.TileCenter(0, n, 0).z)
            && Near(WorkspaceShelfMath.TileCenter(0, n, 1).x, WorkspaceShelfMath.TileCenter(0, n, 0).x));

        // apparatus-kit layout: both rows fit tightly with no overlaps,
        // and the manuscript-derived kit manifest holds its shape.
        foreach (var (plan, rowName) in new[] { (WorkspaceKitsBuilder.Row0Plan(), "top"),
                                                (WorkspaceKitsBuilder.Row1Plan(), "lower") })
        {
            var centers = WorkspaceKitsBuilder.SlotCenters(plan, out float used);
            A("kits: " + rowName + " row fits the shelf width",
                used <= (WorkspaceShelfMath.XMax - WorkspaceShelfMath.XMin) + 0.001f);
            bool noOverlap = true, inBounds = true;
            for (int i = 0; i < plan.Length; i++)
            {
                float l = centers[i] - plan[i].width * 0.5f, r = centers[i] + plan[i].width * 0.5f;
                if (l < WorkspaceShelfMath.XMin - 0.001f || r > WorkspaceShelfMath.XMax + 0.001f) inBounds = false;
                if (i > 0 && l < centers[i - 1] + plan[i - 1].width * 0.5f - 0.001f) noOverlap = false;
            }
            A("kits: " + rowName + " row items stay on the shelf", inBounds);
            A("kits: " + rowName + " row items never overlap", noOverlap);
        }
        int racks = 0, burners = 0; bool hardRack = false, vialRack = false, brush = false, clay = false;
        foreach (var s in WorkspaceKitsBuilder.Row1Plan())
        {
            if (s.prefab == "TestTubeRack") racks++;
            if (s.display.Contains("Hard-Glass")) hardRack = true;
            if (s.display.Contains("Vials")) vialRack = true;
            if (s.prefab == "TestTubeBrush") brush = true;
        }
        foreach (var s in WorkspaceKitsBuilder.Row0Plan())
        {
            if (s.prefab == "BunsenBurner") burners++;
            if (s.prefab == "ClayTriangle") clay = true;
        }
        A("kits: three dispenser racks (regular + hard-glass + vials)", racks == 3 && hardRack && vialRack);
        A("kits: brush staged beside the racks", brush);
        A("kits: one Bunsen heating set + the alcohol crucible set (user layout W5.12)", burners == 1 && clay);

        // rack dispenser: capped, reusable, seat detection.
        A("rack: tube seated when resting in the hole", RackDispenserMath.InHole(false, 0.02f));
        A("rack: held tube is not seated", !RackDispenserMath.InHole(true, 0.02f));
        A("rack: tube pulled clear is not seated", !RackDispenserMath.InHole(false, 0.2f));
        A("rack: out-count = capacity minus seated", RackDispenserMath.OutCount(6, 4) == 2
            && RackDispenserMath.OutCount(6, 6) == 0);
        A("rack: live label reads seated/capacity", RackDispenserMath.Label("Test Tubes", 4, 6) == "Test Tubes  4/6");

        // Racks are FIXED FURNITURE. Both roles — the storage
        // racks tubes live in and the workspace holders they get dragged into —
        // must never be pickable: grabbing one carries every seated tube with it.
        // Pinned against the SCENE, because only a builder re-run can undo it
        // (pure math cannot see a re-added component).
        {
            var stillGrabbable = new List<string>();
            foreach (string rn in SceneRepairs.FixedRacks)
            {
                var go = SceneFind.AnyByName(rn);
                if (go == null) continue;                       // not in this scene — fine
                if (go.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>() != null)
                    stillGrabbable.Add(rn);
            }
            A("rack: fixed racks are NOT grabbable ("
              + (stillGrabbable.Count == 0 ? "all anchored" : string.Join(",", stillGrabbable)) + ")",
              stillGrabbable.Count == 0);
        }

        // The yield stepper is DELETED: the value was read by
        // nothing, and the player could not compute it anyway. The C# API is
        // gone so code can't regress, but the SCENE widgets could be re-added by a
        // panel rebuild, so pin them absent.
        {
            var ghosts = new List<string>();
            foreach (string yn in SceneRepairs.YieldWidgets)
                if (SceneFind.AnyByName(yn) != null) ghosts.Add(yn);
            A("postlab: the yield stepper is gone ("
              + (ghosts.Count == 0 ? "clean" : string.Join(",", ghosts) + " still present") + ")",
              ghosts.Count == 0);
        }
        bool namesReal = true;
        foreach (var s in WorkspaceKitsBuilder.Row0Plan()) if (!RealSizes.TryGet(s.prefab, out _)) namesReal = false;
        foreach (var s in WorkspaceKitsBuilder.Row1Plan()) if (!RealSizes.TryGet(s.prefab, out _)) namesReal = false;
        A("kits: every kit prefab is a real pack item", namesReal);

        // apparatus snap: directed pairs + seat math (grab moves the
        // group; the ACTIVATE click detaches — AssemblyMath owns the policy).
        A("snap: gauze onto tripod, never the reverse",
            AssemblyMath.CanAttach("WireGauze", "Tripod") && !AssemblyMath.CanAttach("Tripod", "WireGauze"));
        A("snap: watch glass covers a beaker",
            AssemblyMath.CanAttach("WatchGlass", "Beaker_100mL") && AssemblyMath.CanAttach("WatchGlass", "Beaker_500mL"));
        A("snap: burner slides under the tripod",
            AssemblyMath.TryAnchor("BunsenBurner", "Tripod", out var burnerSeat) && burnerSeat == SnapAnchor.SameBase);
        A("snap: iron ring clamps the stand pole",
            AssemblyMath.TryAnchor("IronRing", "RetortStand", out var ringSeat) && ringSeat == SnapAnchor.PoleMid);
        A("snap: flask seats on the gauze", AssemblyMath.CanAttach("ErlenmeyerFlask_400mL", "WireGauze"));
        A("snap: unrelated pairs never stick",
            !AssemblyMath.CanAttach("Beaker_100mL", "RetortStand") && !AssemblyMath.CanAttach("TestTube", "Tripod"));
        A("snap: participants cover the heating kit",
            AssemblyMath.Participates("Tripod") && AssemblyMath.Participates("RetortStand")
            && AssemblyMath.Participates("WireGauze") && !AssemblyMath.Participates("TestTubeBrush"));
        var hostB = new Bounds(new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 0.2f, 0.2f));   // top 0.6, base 0.4
        var partB = new Bounds(new Vector3(5f, 5f, 5f), new Vector3(0.1f, 0.1f, 0.1f));     // extents 0.05
        A("snap: top-centre seat rests on the host top",
            Near(AssemblyMath.SeatCenter(SnapAnchor.TopCenter, hostB, partB).y, 0.65f));
        A("snap: same-base seat stands on the host's floor",
            Near(AssemblyMath.SeatCenter(SnapAnchor.SameBase, hostB, partB).y, 0.45f));
        A("snap: pole-mid seat clamps mid-height",
            Near(AssemblyMath.SeatCenter(SnapAnchor.PoleMid, hostB, partB).y, 0.5f));
    }

    static void SceneBuilderSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        // SYNTHETIC fixture: the legacy
        // spawn path (stations, sockets, hazards, teleport pads, props, seeded
        // vessels) runs on an in-memory layout + module, because no real module
        // stages stations any more and a real one could drop them in any polish
        // pass (the simrig fixture pattern). The spawn path stays load-bearing (a
        // future layout may stage one), so it stays pinned here.
        A("builder: library/registry load", lib != null && reg != null);
        A("builder: library has prefabs + chemicals", lib != null && lib.prefabs.Count >= 40 && lib.chemicals.Count >= 20);
        A("builder: library resolves Wine (winemaking end product)", lib != null && lib.GetChemical("Wine") != null);
        if (lib == null) return;

        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "sb-probe";
        foreach (var tid in new[] { "sb-heat", "sb-filter", "sb-zone", "sb-pour", "sb-seeded" })
            module.graphTasks.Add(new ExperimentTask { taskId = tid, label = tid });
        var lay = ScriptableObject.CreateInstance<ExperimentLayout>();
        lay.moduleId = "sb-probe";
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-heat", label = "Heat", requiredItemId = "sb-heat",
          pos = new Vector3(-1.8f, 0.91f, -3.78f), sim = StationSim.Heat, simTargetC = 78f });
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-filter", label = "Filter", requiredItemId = "sb-filter",
          pos = new Vector3(-1.3f, 0.91f, -3.78f), sim = StationSim.Filter, simTargetC = 85f });
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "sb-zone", label = "Zone", requiredItemId = "sb-zone",
          pos = new Vector3(-0.8f, 0.91f, -3.78f), sim = StationSim.None, simTargetC = 85f });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "Vial_Brown", itemId = "sb-pour", displayName = "Probe Reagent",
          pos = new Vector3(1.33f, 0.91f, -3.52f), targetHeight = 0.16f,
          pourable = true, fillChemical = "Silver Nitrate" });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "GlassRod", itemId = "sb-zone", displayName = "Probe Rod",
          pos = new Vector3(1.33f, 0.91f, -3.33f), targetHeight = 0.18f });
        lay.vessels.Add(new ExperimentLayout.Vessel
        { prefabName = "ErlenmeyerFlask_400mL", displayName = "Probe Flask",
          pos = new Vector3(-0.25f, 0.91f, -3.18f), targetHeight = 0.22f,
          bindings = { new ExperimentLayout.Vessel.Bind
            { reagentChemical = "Silver Nitrate", taskId = "sb-pour", requiredMl = 5f, completesTask = true } } });
        lay.vessels.Add(new ExperimentLayout.Vessel
        { prefabName = "TestTube_WithLiquid", displayName = "Probe Seeded Tube",
          pos = new Vector3(0.2f, 0.91f, -3.18f), targetHeight = 0.13f, startChemical = "Chloroform",
          bindings = { new ExperimentLayout.Vessel.Bind
            { reagentChemical = "Potassium Dichromate", taskId = "sb-seeded", requiredMl = 5f, completesTask = true } } });

        var rgo = new GameObject("sb_runner"); var bgo = new GameObject("sb_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, new List<ExperimentLayout> { lay });

            int stationCount = lay.stations.Count;
            int n = builder.Build("sb-probe");
            A("builder: spawns a root per station/prop/vessel",
                n == stationCount + lay.props.Count + lay.vessels.Count);
            A("builder: a task station per layout station",
                bgo.GetComponentsInChildren<ExperimentTaskStation>().Length == stationCount);

            // §2 sockets: each station gets a snap socket filtered to its item.
            var sockets = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor>();
            A("builder: one socket per station", sockets.Length == stationCount);
            bool filtersOk = sockets.Length == stationCount;
            foreach (var so in sockets)
                if (so.GetComponent<StationSocketFilter>() == null || string.IsNullOrEmpty(so.GetComponent<StationSocketFilter>().requiredItemId)) filtersOk = false;
            A("builder: sockets carry item filters", filtersOk);
            // §1 hot-surface hazard: each Heat station gets a player-only zone.
            var hazards = bgo.GetComponentsInChildren<HazardZone>();
            A("builder: heat station gets a hot-surface hazard", hazards.Length >= 1);
            var pr = new GameObject("root").transform;
            var hand = new GameObject("hand").transform;
            var stray = new GameObject("prop").transform;
            try
            {
                hand.SetParent(pr);
                A("hazard: rig child is player", HazardZone.IsPlayer(hand, pr));
                A("hazard: stray object is not", !HazardZone.IsPlayer(stray, pr));
                A("hazard: no root, no match", !HazardZone.IsPlayer(hand, null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pr.gameObject);
                UnityEngine.Object.DestroyImmediate(stray.gameObject);
            }

            // §2 teleport anchors: one floor pad per station, seated at floor level.
            var anchors = bgo.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();
            A("builder: one teleport anchor per station", anchors.Length == stationCount);
            bool onFloor = anchors.Length == stationCount;
            foreach (var an in anchors) if (an.transform.position.y > 0.1f) onFloor = false;
            A("builder: anchors sit on the floor", onFloor);

            var li = new GameObject("li").AddComponent<LabItem>();
            try
            {
                li.itemId = "warm-waterbath";
                A("socket: right item accepted", StationSocketFilter.Matches("warm-waterbath", li));
                A("socket: wrong item rejected", !StationSocketFilter.Matches("crystallise-ice", li));
                A("socket: open socket takes anything", StationSocketFilter.Matches("", null));
                A("socket: filtered socket rejects null", !StationSocketFilter.Matches("warm-waterbath", null));
            }
            finally { UnityEngine.Object.DestroyImmediate(li.gameObject); }
            int propItems = 0;
            foreach (var li2 in bgo.GetComponentsInChildren<LabItem>())
                if (li2.name.StartsWith("Prop_")) propItems++;
            A("builder: props carry LabItem ids", propItems == lay.props.Count);

            // ⛔ INVERTED. The builder must NOT stage a rack +
            // 6 tubes, 2 spare beakers, a flask or a MatchStriker cube on any module:
            // those duplicate a bench that permanently carries all of it, and
            // re-spawn clones deleted by hand. The bench IS
            // the kit; the layout only binds to it.
            A("builder: no rack kit — the bench has racks",
                FindChildByName(bgo.transform, "RackKit") == null
                && FindChildByName(bgo.transform, "RackTube_5") == null);
            A("builder: no spare glass — the bench has Eq_Beaker/Flask",
                FindChildByName(bgo.transform, "Spare_Beaker_100mL_0") == null
                && FindChildByName(bgo.transform, "Spare_ErlenmeyerFlask_400mL_2") == null);
            A("builder: no MatchStriker cube — the matchbox IS the striker (W5.8)",
                FindChildByName(bgo.transform, "MatchStriker") == null);
            bool padsHidden = true;
            foreach (var st2 in bgo.GetComponentsInChildren<ExperimentTaskStation>())
            {
                var mr2 = st2.GetComponent<MeshRenderer>();
                if (mr2 != null && mr2.enabled) padsHidden = false;
            }
            A("builder: station pads invisible (logic intact)", padsHidden);

            var bind = bgo.GetComponentInChildren<LiquidTaskBinding>();
            A("builder: vessel has a LiquidTaskBinding", bind != null);
            // Pouring a binding's OWN reagent completes (or readies) its task — the
            // pour→event→completion chain is exercised generically (the module-
            // specific completions are covered by the simruns).
            if (bind != null && bind.ExpectedSteps.Count > 0)
            {
                var step0 = bind.ExpectedSteps[0];
                bind.HandleReagent(step0.reagent);
                A("builder: pouring a bound reagent drives its task",
                    runner.Graph.IsComplete(step0.taskId) || bind.ReadyFor(step0.taskId) || bind.IsListening);
            }

            // pour contract: receiving vessels must spawn EMPTY (wake branch
            // armed; a phantom 500 ml default blocks chemical adoption
            // forever) and must be able to RENDER liquid (_Fill contract).
            bool mainEmptyOk = false, mainRenderOk = false, seededKeepsFill = true;
            foreach (var vlp in bgo.GetComponentsInChildren<LiquidPhysics>())
            {
                if (!vlp.name.StartsWith("Vessel_")) continue;
                if (vlp.currentChemical == null)
                {
                    mainEmptyOk = vlp.currentLiquidVolume <= 0.1f;
                    mainRenderOk = vlp.mainRenderer != null && vlp.mainRenderer.sharedMaterial != null
                                   && vlp.mainRenderer.sharedMaterial.HasProperty("_Fill");
                }
                else if (vlp.currentLiquidVolume <= 1f) seededKeepsFill = false;
            }
            A("builder: main vessel spawns empty (wake armed)", mainEmptyOk);
            A("builder: main vessel can render liquid", mainRenderOk);
            A("builder: seeded test vessel keeps its fill", seededKeepsFill);
            bool twinsOk = true;
            foreach (var nm in new[] { "Beaker_100mL", "Beaker_500mL", "ErlenmeyerFlask_400mL", "GraduatedCylinder_50mL", "TestTube" })
                if (lib.GetPrefab(nm + "_WithLiquid") == null) twinsOk = false;
            A("builder: every vessel swap twin resolves", twinsOk);

            // feedback layer: sim stations show live status; vessels and
            // pourables carry live tags + mix popups.
            A("builder: sim stations get live status labels", bgo.GetComponentsInChildren<StationStatusLabel>().Length >= 1);
            A("builder: vessels get live status + mix feedback",
                bgo.GetComponentsInChildren<VesselStatus>().Length >= 2 && bgo.GetComponentsInChildren<MixFeedback>().Length >= 2);
            // Precipitate layer: without it, "white crystals
            // separate" fired with nothing visible in adopted/spawned glass.
            bool allPpt = true;
            foreach (var vlp in bgo.GetComponentsInChildren<LiquidPhysics>())
                if (vlp.GetComponent<LiquidTaskBinding>() != null && vlp.precipitateRenderer == null) allPpt = false;
            A("builder: task vessels get a precipitate layer", allPpt);

            builder.Build("tutorial-methane");   // teardown to a clean stage
            int m = builder.Build("tutorial-methane");
            A("builder: Methane builds 0 dynamic (uses its hand-built stage)", m == 0);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
            UnityEngine.Object.DestroyImmediate(module); UnityEngine.Object.DestroyImmediate(lay);
        }

        // All 9 experiments must build a physical setup from their ExperimentLayout —
        // Methane uses its hand-built stage (0 dynamic roots), the other 8 spawn from data.
        var allLayouts = new List<ExperimentLayout>();
        foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { "Assets/PharmaSynth/ScriptableObjects/Layouts" }))
            allLayouts.Add(AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g)));
        A("builder: 8 experiment layouts authored", allLayouts.Count >= 8);

        int resolveMisses = 0;
        foreach (var l in allLayouts)
        {
            if (l == null) continue;
            foreach (var p in l.props)
            {
                if (lib.GetPrefab(p.prefabName) == null) resolveMisses++;
                if (p.pourable && !string.IsNullOrEmpty(p.fillChemical) && lib.GetChemical(p.fillChemical) == null) resolveMisses++;
            }
            foreach (var v in l.vessels)
            {
                if (lib.GetPrefab(v.prefabName) == null) resolveMisses++;
                if (!string.IsNullOrEmpty(v.startChemical) && lib.GetChemical(v.startChemical) == null) resolveMisses++;
                foreach (var bnd in v.bindings)
                    if (lib.GetChemical(bnd.reagentChemical) == null) resolveMisses++;
            }
            // Every station must be completable by an item that EXISTS — either a prop
            // the layout stages, or one of the permanent bench items.
            //
            // Demanding the item be in the SAME LAYOUT would encode
            // the opposite of the hard client rule ("ALL general tools are ALWAYS on the
            // bench") and push layouts into staging their own funnel/burner/dropper,
            // duplicating apparatus that is already there. A
            // station's ZoneItemSensor matches a LabItem.itemId wherever it lives, so
            // pointing at the bench (kit-bunsenburner, kit-funnel) is the correct shape.
            var ids = new HashSet<string>(BenchItemIds);
            foreach (var p in l.props) ids.Add(p.itemId);
            foreach (var s in l.stations)
                if (!string.IsNullOrEmpty(s.requiredItemId) && !ids.Contains(s.requiredItemId)) resolveMisses++;
        }
        A("builder: every layout resolves all prefab/chemical/station names", resolveMisses == 0);

        // ⛔ A LAYOUT MUST NOT RE-STAGE THE PERMANENT BENCH.
        // Every general tool and raw reagent is ALWAYS out (the hard client rule), so
        // staging a dropper/funnel/burner/spatula or a pourable reagent bottle spawns a
        // DUPLICATE of something already on the bench (for Exp 2 that would be 46:
        // 21 bottles, 2 droppers, a spatula, a funnel, a burner, 20 tubes, beside
        // the Eq_*/Kit_*/Raw_* originals). Stations point at bench itemIds instead.
        {
            var bench = BenchItemIds;
            bool noToolDupes = true, noReagentDupes = true;
            string dupeNote = "";
            foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LayoutDir }))
            {
                var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
                if (l == null || l.moduleId != "prelim-chemical-compounding") continue;   // the manuscript Exp 2 module
                foreach (var p in l.props)
                {
                    if (bench.Contains(p.itemId)) { noToolDupes = false; dupeNote = l.name + " -> " + p.itemId; }
                    // A pourable bottle IS shelf stock — the shelf already holds all 21.
                    if (p.pourable && !string.IsNullOrEmpty(p.fillChemical))
                    { noReagentDupes = false; dupeNote = l.name + " -> bottle of " + p.fillChemical; }
                }
            }
            A("bench: compounding stages no tool the bench already has" + (noToolDupes ? "" : "  [" + dupeNote + "]"), noToolDupes);
            A("bench: compounding stages no reagent bottle (the shelf holds them)" + (noReagentDupes ? "" : "  [" + dupeNote + "]"), noReagentDupes);

            // Vessels must BIND to the bench, not spawn twins beside it (e.g. 20
            // Vessel_TestTube clones next to the real Kit_TestTube_0-18). And every
            // benchItem must EXIST: a name typo means the step can never complete, a
            // silent class of bug nothing else catches.
            var sceneNames = new HashSet<string>();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include))
                sceneNames.Add(t.name);

            bool allBound = true, allExist = true;
            string missNote = "";
            foreach (var g in AssetDatabase.FindAssets("t:ExperimentLayout", new[] { LayoutDir }))
            {
                var l = AssetDatabase.LoadAssetAtPath<ExperimentLayout>(AssetDatabase.GUIDToAssetPath(g));
                if (l == null || l.moduleId != "prelim-chemical-compounding") continue;
                foreach (var v in l.vessels)
                {
                    if (string.IsNullOrEmpty(v.benchItem)) { allBound = false; missNote = l.name + " spawns " + v.displayName; continue; }
                    if (!sceneNames.Contains(v.benchItem)) { allExist = false; missNote = v.benchItem + " (for " + v.displayName + ")"; }
                }
            }
            A("bench: every compounding vessel BINDS to a bench object" + (allBound ? "" : "  [" + missNote + "]"), allBound);
            A("bench: every benchItem a layout names actually EXISTS" + (allExist ? "" : "  [MISSING " + missNote + "]"), allExist);
        }

        var allBgo = new GameObject("sb_all");
        try
        {
            var b2 = allBgo.AddComponent<ExperimentSceneBuilder>();
            b2.SetRefs(null, lib, reg, allLayouts);
            int builtAll = 0;
            foreach (var l in allLayouts)
            {
                if (l == null || l.moduleId == "tutorial-methane") continue;
                if (b2.Build(l.moduleId) > 0) builtAll++;
            }
            A("builder: all 8 data-driven experiments spawn > 0 roots", builtAll >= 8);
        }
        finally { UnityEngine.Object.DestroyImmediate(allBgo); }
    }

    static void SimRigSuite()
    {
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var reg = AssetDatabase.LoadAssetAtPath<ReactionRegistry>("Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        // SYNTHETIC fixture: every polished module is
        // zone-free, so the classic Heat-station rig is exercised on an
        // in-memory layout + module that no module polish can break.
        A("simrig: library loads", lib != null);
        if (lib == null) return;
        var module = ScriptableObject.CreateInstance<ExperimentModuleDefinition>();
        module.moduleId = "rig-probe";
        module.graphTasks.Add(new ExperimentTask { taskId = "rig-heat", label = "Rig heat" });
        var lay = ScriptableObject.CreateInstance<ExperimentLayout>();
        lay.moduleId = "rig-probe";
        lay.stations.Add(new ExperimentLayout.Station
        { taskId = "rig-heat", label = "Rig", requiredItemId = "rig-heat",
          pos = new Vector3(0f, 0.91f, -3.7f), sim = StationSim.Heat, simTargetC = 150f });
        lay.props.Add(new ExperimentLayout.Prop
        { prefabName = "BunsenBurner", itemId = "rig-heat", displayName = "Rig Burner",
          pos = new Vector3(0.25f, 0.91f, -3.7f), targetHeight = 0.22f });

        var rgo = new GameObject("sr_runner"); var bgo = new GameObject("sr_builder");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module); runner.StartExperiment();
            var builder = bgo.AddComponent<ExperimentSceneBuilder>();
            builder.SetRefs(runner, lib, reg, new List<ExperimentLayout> { lay });
            builder.Build("rig-probe");

            ZoneSimStation heatRig = null;
            foreach (var z in bgo.GetComponentsInChildren<ZoneSimStation>(true))
                if (z.gameObject.name == "Station_rig-heat") { heatRig = z; break; }
            A("simrig: heat station built with a ZoneSimStation", heatRig != null);
            if (heatRig == null) return;
            var temp = heatRig.GetComponent<TemperatureSim>();
            var sensor = heatRig.GetComponent<ZoneItemSensor>();
            A("simrig: heat station carries TemperatureSim + sensor", temp != null && sensor != null);

            runner.Graph.Tick();
            A("simrig: heat task pending before heating", !runner.Graph.IsComplete("rig-heat"));

            // ignition gate: the station's prop is a BunsenBurner, so heat
            // only advances once the burner is LIT (light it with a match).
            var burnerProp = FindChildByName(bgo.transform, "Prop_rig-heat");
            var burner = burnerProp != null ? burnerProp.GetComponent<BurnerController>() : null;
            A("simrig: rig burner is ignitable (W5.8)", burner != null);
            sensor.ForceOccupied(true);
            heatRig.Drive(1f, true);           // burner present but UNLIT -> no heat
            for (int i = 0; i < 12; i++) temp.Tick(1f);
            A("simrig: unlit burner never heats (W5.8)", !temp.AtLeast(150f));

            // Perform the verb: light the burner -> heat to target -> auto-completes.
            if (burner != null) burner.Ignite();
            heatRig.Drive(1f, true);           // flame on
            for (int i = 0; i < 12; i++) temp.Tick(1f);
            A("simrig: temperature reached target", temp.AtLeast(150f));
            runner.Graph.Tick();
            A("simrig: heat verb auto-completes the task", runner.Graph.IsComplete("rig-heat"));
        }
        finally
        {
            var b2 = bgo.GetComponent<ExperimentSceneBuilder>();
            if (b2 != null) b2.Build("tutorial-methane");   // bench teardown insurance
            UnityEngine.Object.DestroyImmediate(rgo); UnityEngine.Object.DestroyImmediate(bgo);
            UnityEngine.Object.DestroyImmediate(module); UnityEngine.Object.DestroyImmediate(lay);
        }
    }

    static void RealVerbSuite()
    {
        // Methane real verbs: burner presence heats (TemperatureSim), heat gates gas
        // collection (GasCollection), and both tasks complete via graph auto-check.
        var module = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
            "Assets/PharmaSynth/ScriptableObjects/Experiments/Tutorial_Methane.asset");
        A("realverb: methane module loads", module != null);
        if (module == null) return;

        var rgo = new GameObject("rv_runner");
        var xgo = new GameObject("rv_rig");
        var cgo = new GameObject("rv_collect");
        try
        {
            var runner = rgo.AddComponent<ExperimentRunner>();
            runner.SetModule(module);
            var temp = xgo.AddComponent<TemperatureSim>();
            var gas = xgo.AddComponent<GasCollection>();
            var rig = xgo.AddComponent<MethaneApparatusRig>();

            runner.StartExperiment();
            // location-free rig — no zones; heat/collect complete via the
            // rig's own TemperatureSim/GasCollection conditions (driven by burner/
            // collection-tube PROXIMITY in play; here we drive the sims directly).
            rig.Bind(runner, temp, gas);
            rig.HandleExperimentStarted(module);   // edit mode: subscribe happened post-start

            runner.CompleteTask("prepare-mixture");
            runner.CompleteTask("setup-apparatus");
            runner.Graph.Tick();
            A("realverb: heat-mixture blocked while cold", !runner.Graph.IsComplete("heat-mixture"));

            temp.SetHeating(true, 220f);
            for (int i = 0; i < 120; i++) temp.Tick(0.5f);
            runner.Graph.Tick();
            A("realverb: sustained heating completes heat-mixture", runner.Graph.IsComplete("heat-mixture"));
            A("realverb: collect-gas still pending", !runner.Graph.IsComplete("collect-gas"));

            gas.AddGas(100000f);   // clamps to capacity
            runner.Graph.Tick();
            A("realverb: gas fill completes collect-gas", runner.Graph.IsComplete("collect-gas"));
            A("realverb: progress 4/5 after real verbs", Near(runner.Graph.Progress01, 0.8f));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rgo);
            UnityEngine.Object.DestroyImmediate(xgo);
            UnityEngine.Object.DestroyImmediate(cgo);
        }
    }

    // Hover-inspector knowledge base + panel animation.
    static void HoverInfoSuite()
    {
        A("info: equipment authored", LabInfoDatabase.EquipmentCount >= 25);
        A("info: reagents authored", LabInfoDatabase.ReagentCount >= 30);

        var beaker = LabInfoDatabase.Equipment("Prop_Beaker_100mL");
        A("info: beaker resolves", beaker != null && beaker.Title == "Beaker" && beaker.Category == LabInfoCategory.Equipment);
        var cyl = LabInfoDatabase.Equipment("GraduatedCylinder_50mL");
        A("info: graduated cylinder resolves (specific before 'cylinder')", cyl != null && cyl.Title == "Graduated Cylinder");
        var rack = LabInfoDatabase.Equipment("TestTubeRack_12Tubes");
        A("info: test-tube RACK not plain test-tube", rack != null && rack.Title == "Test-Tube Rack");
        A("info: unknown prop → no card", LabInfoDatabase.Equipment("Wall_Section_42") == null);
        var coat = LabInfoDatabase.Equipment("LabCoatDisplay");
        A("info: lab coat resolves", coat != null && coat.Title == "Lab Coat");
        var gog = LabInfoDatabase.Equipment("Goggles_Standin");
        A("info: goggles resolve", gog != null && gog.Title == "Safety Goggles");
        var glv = LabInfoDatabase.Equipment("Gloves_Standin");
        A("info: gloves resolve", glv != null && glv.Title == "Nitrile Gloves");

        var naoh = LabInfoDatabase.Reagent("Sodium Hydroxide");
        A("info: NaOH trivia present", naoh != null && naoh.Category == LabInfoCategory.Reagent && naoh.Body.Length > 20);
        var unknownChem = LabInfoDatabase.Reagent("Unobtainium");
        A("info: unknown reagent → generic card", unknownChem != null && unknownChem.Title == "Unobtainium");
        A("info: empty reagent name safe", LabInfoDatabase.Reagent("") != null);

        A("info: pharmee person card", LabInfoDatabase.Person(true).Title == "Pharmee");
        A("info: jimenez person card", LabInfoDatabase.Person(false).Title.Contains("Jimenez"));

        A("info: norm strips + lowercases", LabInfoDatabase.Norm("Beaker_100 mL") == "beaker100ml");

        // Panel easing + accent mapping (pure).
        A("panel: ease(0)=0", Near(HoverInfoPanel.Ease(0f), 0f));
        A("panel: ease(1)=1", Near(HoverInfoPanel.Ease(1f), 1f));
        A("panel: ease(0.5)=0.5", Near(HoverInfoPanel.Ease(0.5f), 0.5f));
        A("panel: ease clamps", Near(HoverInfoPanel.Ease(2f), 1f) && Near(HoverInfoPanel.Ease(-1f), 0f));
        A("panel: reagent accent amber", HoverInfoPanel.AccentFor(LabInfoCategory.Reagent).r > 0.9f);
        A("panel: tags", HoverInfoPanel.Tag(LabInfoCategory.Equipment) == "EQUIPMENT" && HoverInfoPanel.Tag(LabInfoCategory.Person) == "LAB GUIDE");

        // Placement must always sit IN FRONT of the struck surface (never occluded).
        A("panel: close target → card in front", HoverInfoPanel.PlaceDistance(1.0f, 0.5f, 1.1f, 0.4f) <= 1.0f - 0.12f + 1e-4f);
        A("panel: very close target floored", HoverInfoPanel.PlaceDistance(0.5f, 0.5f, 1.1f, 0.4f) <= 0.5f - 0.12f + 1e-4f);
        A("panel: distant target stays readable-close", HoverInfoPanel.PlaceDistance(4f, 0.5f, 1.1f, 0.4f) <= 1.1f + 1e-4f);
        A("panel: never negative", HoverInfoPanel.PlaceDistance(0.3f, 0.5f, 1.1f, 0.4f) >= 0.3f);

        // Held-item suppression (the card must not sit over an
        // item you're holding). Grabbable NOT selected → not held; null safe.
        var hgo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            hgo.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            A("panel: idle grabbable is not held", !HoverInspector.IsHeld(hgo.GetComponent<Collider>()));
            A("panel: null collider not held", !HoverInspector.IsHeld(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(hgo); }
    }

    // Held-item collision profile: pack prefabs default to Instantaneous movement
    // (no physics sweep), so grabbed props phase through walls.
    static void GrabTuningSuite()
    {
        var host = new GameObject("tune-host");
        try
        {
            var grab = host.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            grab.movementType = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable.MovementType.Instantaneous;
            A("grab: untuned detected", !GrabTuning.IsTuned(grab));
            A("grab: apply reports change", GrabTuning.Apply(grab));
            A("grab: velocity-tracked after apply", GrabTuning.IsTuned(grab));
            A("grab: two-handed (multi-grab) after apply", grab.selectMode == UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode.Multiple);
            A("grab: ease-in set", Near(grab.attachEaseInTime, GrabTuning.AttachEaseSeconds));
            A("grab: re-apply is a no-op", !GrabTuning.Apply(grab));
            A("grab: null safe", !GrabTuning.Apply(null) && !GrabTuning.IsTuned(null));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }

        // Every library prefab grab must stay velocity-tracked (pins the Wire
        // Grab Collision menu's result; a prefab re-import regression fails here).
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>("Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        A("grab: library present", lib != null && lib.prefabs != null);
        if (lib != null && lib.prefabs != null)
        {
            int grabs = 0, tuned = 0;
            foreach (var p in lib.prefabs)
            {
                if (p == null) continue;
                foreach (var g in p.GetComponentsInChildren<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>(true))
                { grabs++; if (GrabTuning.IsTuned(g)) tuned++; }
            }
            A("grab: all library prefabs velocity-tracked (" + tuned + "/" + grabs + ")", grabs > 0 && tuned == grabs);
        }
    }

    // Center-table merge geometry (one wide landscape table,
    // centered — a rigid remap shared by the island object and the baked layouts).
    static void CenterTableMathSuite()
    {
        var pts = new List<Vector3> {
            new Vector3(-2.15f, 0.91f, -1.95f), new Vector3(-1.1f, 0.91f, -4.6f), new Vector3(-1.9f, 0.91f, -3.4f),
        };
        var b = CenterTableMath.FootprintOf(pts, 0.35f);
        A("table: footprint spans the points", b.min.x < -2.15f && b.max.x > -1.1f && b.min.z < -4.6f && b.max.z > -1.95f);
        A("table: margin applied", Near(b.max.x, -1.1f + 0.35f, 0.01f));

        Vector3 oldC = new Vector3(-1.6f, 0.91f, -3.3f);
        Vector3 newC = new Vector3(0f, 0.91f, -3.3f);
        A("table: centre maps to centre", Near(Vector3.Distance(
            CenterTableMath.Remap(oldC, oldC, newC, true), newC), 0f, 0.001f));
        // +z offset becomes +x offset under the 90° landscape turn.
        Vector3 r = CenterTableMath.Remap(oldC + new Vector3(0f, 0f, 1f), oldC, newC, true);
        A("table: rotation maps z-run onto x-run", Near(r.x, newC.x + 1f) && Near(r.z, newC.z));
        // Height is preserved.
        A("table: deck height preserved", Near(CenterTableMath.Remap(new Vector3(-1.2f, 0.91f, -2f), oldC, newC, true).y, 0.91f));
        // Distances are rigid (no scaling).
        Vector3 p1 = new Vector3(-2.15f, 0.91f, -1.95f), p2 = new Vector3(-1.1f, 0.91f, -4.6f);
        A("table: remap is rigid", Near(
            Vector3.Distance(CenterTableMath.Remap(p1, oldC, newC, true), CenterTableMath.Remap(p2, oldC, newC, true)),
            Vector3.Distance(p1, p2), 0.001f));

        A("table: within test", CenterTableMath.WithinXZ(new Vector3(-1.5f, 0.9f, -3f), b)
            && !CenterTableMath.WithinXZ(new Vector3(2f, 0.9f, -3f), b));
        A("table: mirror across x", Near(CenterTableMath.MirrorAcrossX(new Vector3(1.4f, 0f, -3f), 0f).x, -1.4f));
    }
}
#endif
