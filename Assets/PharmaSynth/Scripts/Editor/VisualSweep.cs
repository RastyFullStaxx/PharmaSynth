#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// The photographer behind the VISUAL autopilot: after each honestly-performed
/// step it frames the vessel the step happened in, renders a close-up, reads the numbers
/// behind the picture (fill, colour, precipitate, boil, temperature, live particles,
/// popups) and judges them against what the fired ReactionRule / the verb promised. One
/// manifest row per step feeds Tools/visual-sheet.py (a captioned contact sheet per module).
///
/// ⭐ Why numbers as well as pixels: a screenshot cannot say "the flask is at 3% fill" or
/// "no particle system is alive within 60 cm", and a reviewer skimming seventy thumbnails
/// misses both. Every verdict names the exact quantity it judged, so a FAIL is actionable
/// on its own.
public static class VisualSweep
{
    public const string Dir = "Logs/visual-sweep";
    public const string Manifest = Dir + "/manifest.tsv";
    public const string Report = "Logs/visual-sweep-report.txt";
    /// How far from the vessel a particle system or popup still counts as "at" it.
    public const float NearM = 0.6f;
    const int W = 960, H = 720;

    public enum Expect { None, Fill, ColorChange, Precipitate, Gas, Odor, Heat, Chill, Powder, Grind, Scoop,
                         Flame, GasFill }

    /// What the probe saw. Plain data so the judge is pure and pinnable.
    public struct Obs
    {
        public bool found, rendererOn, pptOn, powderOn, heapOn;
        /// Scene-wide cues, for the steps whose evidence is NOT inside a vessel.
        public bool flameLit;
        public float gasFill01;
        public float ml, pptMl, fill01, boil, tempC;
        public Color colour;
        public int particles;
        public string particleNames, popups, chem;

        public override string ToString()
        {
            if (!found) return "no vessel";
            return chem + " " + ml.ToString("0.#") + " ml (" + (fill01 * 100f).ToString("0.#") + "% fill, liquid "
                   + (rendererOn ? "ON" : "off") + ") · ppt " + pptMl.ToString("0.#") + " ml " + (pptOn ? "ON" : "off")
                   + (powderOn ? " · powder mound" : "") + (heapOn ? " · heap on blade" : "")
                   + " · " + tempC.ToString("0") + " C boil " + boil.ToString("0.00")
                   + " · colour " + Hex(colour)
                   + " · particles " + particles + (string.IsNullOrEmpty(particleNames) ? "" : " [" + particleNames.Trim() + "]")
                   + (string.IsNullOrEmpty(popups) ? "" : " · popup \"" + popups.Trim() + "\"");
        }
    }

    public struct Verdict
    {
        public string status, reason;
        public bool Fail => status == "FAIL";
    }

    static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    // ---- pure (suite-pinned) ------------------------------------------------------

    /// What the step promised. The fired rule outranks the verb (its observation is the
    /// manuscript's), and visible GAS outranks whatever else the rule says — "brisk
    /// effervescence" that shows nothing is the finding, not a colour.
    public static Expect ExpectFor(string kind, ReactionRule rule)
    {
        if (rule != null)
        {
            if (rule.evolvesGas || rule.outcome == ReactionOutcome.Fizzing || rule.outcome == ReactionOutcome.GasEvolved)
                return Expect.Gas;
            // A DELIBERATE negative test (acetone vs Tollens/Schiff) authors the product
            // AS the reactant — "no silver mirror" is the lesson, so there is nothing to
            // see and judging it as a colour change is a false positive. The manuscript's
            // own contrast IS the absence of a change.
            bool noChangeByDesign = rule.resultLiquid == rule.inputChemicalA;
            switch (rule.outcome)
            {
                case ReactionOutcome.Precipitate: return Expect.Precipitate;
                case ReactionOutcome.Odor: return Expect.Odor;
                case ReactionOutcome.ColorChange: return noChangeByDesign ? Expect.Fill : Expect.ColorChange;
            }
            if (rule.hasPrecipitate) return Expect.Precipitate;
            return rule.resultLiquid != null && !noChangeByDesign ? Expect.ColorChange : Expect.Fill;
        }
        if (string.IsNullOrEmpty(kind)) return Expect.None;
        switch (kind)
        {
            case "heat": return Expect.Heat;
            case "chill": return Expect.Chill;
            case "solid": return Expect.Powder;
            case "grind": return Expect.Grind;
            case "pour": case "stir": case "vapor": case "weigh": case "litmus": case "ferment": case "rack":
                return Expect.Fill;
        }
        // ⛔ These four must not fall through to Expect.None: the sweep would photograph
        // them and judge NOTHING, a SKIP "the photo is the evidence" that nobody looks at.
        // Their evidence is not inside a vessel — it is a flame, a gas column and an
        // assembled rig.
        switch (kind)
        {
            case "flame": return Expect.Flame;
            // — NOT an apparatus-assembly contract. The step is a SOLID DELIVERY ("Scoop the
            // ground mixture from the mortar into the Hard-glass tube"), and MethaneApparatusRig
            // never uses ApparatusSnap, so counting snap attachments would read 0 forever and
            // report a FAIL no player could see. Judge what the step DOES.
            case "methane:setup-apparatus": return Expect.Powder;
            case "methane:heat-mixture": return Expect.Heat;
            case "methane:collect-gas": return Expect.GasFill;
        }
        return Expect.None;
    }

    public static float ColourDistance(Color a, Color b)
        => Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

    /// Thresholds: a 4% fill is the invisible-in-VR floor SimulatedRun already uses; 0.08 in
    /// RGB is a change a headset shows; 0.12 is how far the shader may lag the product.
    public const float FillFloor = 0.04f, VisibleDelta = 0.08f, ShaderSlack = 0.12f;

    public static Verdict Judge(Expect e, Obs o, ReactionRule rule, float targetC, float boilingPointC)
    {
        Verdict Ok(string r) => new Verdict { status = "OK", reason = r };
        Verdict Fail(string r) => new Verdict { status = "FAIL", reason = r };
        Verdict Skip(string r) => new Verdict { status = "SKIP", reason = r };

        if (e == Expect.None) return Skip("no visual contract for this step (the photo is the evidence)");

        // These two are judged BEFORE the vessel guard on purpose: their evidence is a
        // flame or a gas column, neither of which lives in a LiquidPhysics.
        switch (e)
        {
            case Expect.Flame:
                return o.flameLit ? Ok("a live flame is at the sample")
                                  : Fail("flammability test with NO lit flame anywhere in the scene");
            case Expect.GasFill:
                return o.gasFill01 > 0.01f
                    ? Ok("gas collected, tube " + (o.gasFill01 * 100f).ToString("0") + "% full")
                    : Fail("collection step finished with an EMPTY tube (GasCollection.FillFraction 0)");
        }

        if (!o.found) return Fail("no vessel to look at");
        string fill = "fill " + (o.fill01 * 100f).ToString("0.#") + "% (" + (o.ml + o.pptMl).ToString("0.#") + " ml)";
        switch (e)
        {
            case Expect.Fill:
                return o.fill01 >= FillFloor && (o.rendererOn || o.pptOn || o.powderOn)
                    ? Ok(fill + ", contents shown")
                    : Fail(fill + ", liquid renderer " + (o.rendererOn ? "on" : "OFF") + " — invisible in VR");
            case Expect.Powder:
                // A solid can land in a vessel that also holds liquid (the wine must:
                // sugar + yeast into juice) — a visible liquid fill counts too.
                return o.powderOn ? Ok("powder mound shown, " + o.ml.ToString("0.#") + " g")
                     : o.fill01 >= FillFloor && o.rendererOn ? Ok("solid in a liquid, " + fill + " shown")
                     : Fail("no powder mound and no visible fill after a solid delivery (" + (o.ml + o.pptMl).ToString("0.#") + " in the vessel)");
            case Expect.Scoop:
                return o.heapOn ? Ok("heap riding the blade") : Fail("no heap on the blade after a dip");
            case Expect.Grind:
                return o.powderOn || o.particles > 0 ? Ok("ground heap " + (o.powderOn ? "shown" : "absent") + ", dust " + o.particles)
                                                     : Fail("no ground heap and no dust after grinding");
            case Expect.Precipitate:
                // Judged against the same threshold the game draws at, not a separate one:
                // a rule deposits exactly the incoming pour, so 1 ml is a normal, real,
                // visible precipitate.
                return o.pptOn && o.pptMl > LiquidPhysics.ShowFromMl
                    ? Ok("precipitate " + o.pptMl.ToString("0.#") + " ml shown")
                    : Fail("rule promises a precipitate; ppt " + o.pptMl.ToString("0.#") + " ml, renderer " + (o.pptOn ? "on" : "OFF"));
            case Expect.Gas:
                return o.particles > 0 ? Ok("gas: " + o.particles + " live emitter(s) " + (o.particleNames ?? "").Trim())
                                       : Fail("gas evolved but NOTHING visible — 0 live particle systems within " + NearM + " m");
            case Expect.Odor:
                return !string.IsNullOrEmpty(o.popups) ? Ok("observation popup \"" + o.popups.Trim() + "\"")
                                                       : Fail("odour is the one sense VR lacks — and no observation popup was showing");
            case Expect.Heat:
            {
                if (o.tempC < targetC - 0.5f) return Fail("only " + o.tempC.ToString("0") + " C, needs " + targetC.ToString("0") + " C");
                bool shouldBoil = targetC >= boilingPointC - LiquidPhysics.RampC;
                if (shouldBoil && o.boil <= 0.001f) return Fail(o.tempC.ToString("0") + " C at the boil (bp " + boilingPointC.ToString("0") + ") but the liquid sits still — _Boil " + o.boil.ToString("0.00"));
                return Ok(o.tempC.ToString("0") + " C" + (shouldBoil ? ", boiling " + o.boil.ToString("0.00") : "") + ", " + fill);
            }
            case Expect.Chill:
                return o.tempC <= targetC + 0.5f ? Ok(o.tempC.ToString("0") + " C in the ice, " + fill)
                                                 : Fail("still " + o.tempC.ToString("0") + " C, needs <= " + targetC.ToString("0") + " C");
            case Expect.ColorChange:
            {
                if (rule == null || rule.resultLiquid == null) return Skip("colour rule without a result liquid");
                Color want = rule.resultLiquid.liquidColor;
                Color was = rule.inputChemicalA != null ? rule.inputChemicalA.liquidColor : Color.clear;
                float shown = ColourDistance(o.colour, want), authored = ColourDistance(want, was);
                if (!o.rendererOn) return Fail("liquid renderer OFF after the reaction (" + fill + ")");
                if (authored < VisibleDelta)
                    return Fail("AUTHORING: product colour " + Hex(want) + " is indistinguishable from the reactant " + Hex(was) + " (Δ " + authored.ToString("0.00") + ") — no change to see");
                if (shown > ShaderSlack)
                    return Fail("shader shows " + Hex(o.colour) + " but the product is " + Hex(want) + " (Δ " + shown.ToString("0.00") + ")");
                return Ok("colour " + Hex(o.colour) + " = " + rule.resultLiquid.chemicalName + " (Δ from reactant " + authored.ToString("0.00") + ")");
            }
        }
        return Skip("unhandled expectation " + e);
    }

    // ---- scene ----------------------------------------------------------------

    /// The evidence that is NOT inside a vessel: a lit flame, a filling gas tube and an
    /// assembled apparatus. Scanned scene-wide because that is what these steps promise \u2014
    /// the methane rig's whole point is that the parts are joined and the gas goes somewhere.
    // ⛔ A flame is TRANSIENT evidence. Sampled once at the final capture, after the step has
    // completed and DropRespawn has extinguished the match, it reads "NO lit flame anywhere
    // in the scene" on a step the game judged correctly ("Won't ignite — NON-FLAMMABLE"):
    // one sample of a sometimes-true fact measures its default. So the flame is LATCHED at the
    // mid-verb moment, when the match is provably at the sample, and the latch clears once the
    // step has been judged.
    static bool s_flameLatched;
    public static bool FlameLatched => s_flameLatched;
    public static void NoteFlame(bool litNow) { if (litNow) s_flameLatched = true; }
    public static void ClearFlameLatch() => s_flameLatched = false;

    /// Is any flame lit in the scene right now?
    public static bool AnyFlameLit()
    {
        foreach (var b in Object.FindObjectsByType<BurnerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b != null && b.IsLit) return true;
        foreach (var m in Object.FindObjectsByType<Matchstick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (m != null && m.IsLit) return true;
        return false;
    }

    static void SceneCues(ref Obs o)
    {
        if (s_flameLatched) o.flameLit = true;
        if (AnyFlameLit()) o.flameLit = true;

        foreach (var g in Object.FindObjectsByType<GasCollection>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g != null && g.FillFraction > o.gasFill01) o.gasFill01 = g.FillFraction;

    }

    public static Obs Probe(LiquidPhysics lp)
    {
        var o = new Obs { found = lp != null, particleNames = "", popups = "", chem = "" };
        SceneCues(ref o);
        if (lp == null) return o;
        o.ml = lp.currentLiquidVolume; o.pptMl = lp.currentPptVolume;
        // Judge what the player SEES: the drawn column, which carries the readability
        // floor for the manuscript's small volumes.
        o.fill01 = LiquidPhysics.DisplayFill01(o.ml + o.pptMl, lp.maxVolume);
        o.tempC = lp.currentTempC; o.boil = lp.BoilAmount();
        o.chem = lp.currentChemical != null ? lp.currentChemical.chemicalName : "(empty)";
        var mr = lp.mainRenderer;
        o.rendererOn = mr != null && mr.enabled && mr.gameObject.activeInHierarchy;
        if (mr != null && mr.material != null && mr.material.HasProperty("_LiquidColour"))
            o.colour = mr.material.GetColor("_LiquidColour");
        var pr = lp.precipitateRenderer;
        o.pptOn = pr != null && pr.enabled && pr.gameObject.activeInHierarchy;
        foreach (var r in lp.GetComponentsInChildren<Renderer>(true))
            if (r != null && r.name == "Powder")
                o.powderOn = r.gameObject.activeInHierarchy && r.transform.localScale.sqrMagnitude > 1e-8f;

        Vector3 c = ExperimentSceneBuilder.SolidWorldBounds(lp.gameObject).center;
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (ps == null || !ps.gameObject.activeInHierarchy) continue;
            if (ps.GetComponentInParent<AtmosphereVfx>() != null) continue;      // room haze is everywhere
            if (Vector3.Distance(ps.transform.position, c) > NearM) continue;
            if (ps.particleCount <= 0) continue;
            o.particles++;
            o.particleNames += ps.name + "(" + ps.particleCount + ") ";
        }
        foreach (var fx in Object.FindObjectsByType<FloatingTextFx>(FindObjectsSortMode.None))
        {
            if (fx == null || Vector3.Distance(fx.transform.position, c) > NearM) continue;
            var tmp = fx.GetComponent<TMPro.TMP_Text>();
            if (tmp != null && !string.IsNullOrEmpty(tmp.text)) o.popups += tmp.text + " | ";
        }
        foreach (var sc in Object.FindObjectsByType<ScoopController>(FindObjectsSortMode.None))
        {
            if (sc == null || !sc.Carrying || Vector3.Distance(sc.BladeTip, c) > NearM) continue;
            var heap = sc.transform.Find("ScoopHeap");
            if (heap != null && heap.gameObject.activeInHierarchy) o.heapOn = true;
        }
        return o;
    }

    /// The last frame's geometry, for the manifest — a bad picture is diagnosable from its row.
    public static string LastFrame = "";

    /// Vantages tried in order: the player's side first, then swung around the vessel,
    /// then straight down. The first with a clear line of sight wins — a bench top, a
    /// cabinet or the fume-hood shell otherwise puts the lens inside a wall.
    static readonly (float yaw, float pitch, string name)[] Vantages =
    {
        (0f, 22f, "player-side"), (45f, 22f, "player-left"), (-45f, 22f, "player-right"),
        (180f, 22f, "far-side"), (0f, 70f, "above"),
    };

    /// Is the way from the vessel's surface out to the camera free of OTHER colliders?
    /// Cast outward from the vessel, never from the camera: a ray that starts inside a
    /// bench sees no back faces and would call that vantage clear.
    static bool Clear(Vector3 look, Vector3 cam, float targetRadius, Transform target)
    {
        Vector3 d = cam - look; float len = d.magnitude;
        float start = targetRadius + 0.02f;
        if (len <= start + 0.05f) return true;
        if (!Physics.Raycast(look + d / len * start, d / len, out var hit, len - start, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.collider != null && hit.collider.transform.IsChildOf(target);
    }

    /// DevCapture's recipe (temp URP camera, post on, the pipeline's MSAA) framed on the
    /// target's SOLID bounds from the player's side, with headroom for rising smoke.
    public static string Snap(GameObject target, string file, bool fastForwardFx)
    {
        if (target == null) return null;
        var b = ExperimentSceneBuilder.SolidWorldBounds(target);
        Vector3 look = b.center;
        float targetRadius = b.extents.magnitude;
        b.Encapsulate(b.center + Vector3.up * (b.extents.y + 0.2f));
        // Thin glass frames as a sliver at its true radius — never closer than a hand's length.
        float radius = Mathf.Max(0.12f, b.extents.magnitude);
        const float fov = 40f;
        float dist = radius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * 1.25f;
        var main = Camera.main;
        Vector3 side = (main != null ? main.transform.position : look + Vector3.back) - look;
        side.y = 0f;
        if (side.sqrMagnitude < 1e-4f) side = Vector3.back;
        side.Normalize();
        Vector3 pos = look + side * dist + Vector3.up * (dist * 0.4f);
        string vantage = "blocked-everywhere";
        foreach (var (yaw, pitch, name) in Vantages)
        {
            Vector3 d = Quaternion.AngleAxis(yaw, Vector3.up) * side;
            Vector3 p = look + d * (dist * Mathf.Cos(pitch * Mathf.Deg2Rad)) + Vector3.up * (dist * Mathf.Sin(pitch * Mathf.Deg2Rad));
            if (Clear(look, p, targetRadius, target.transform)) { pos = p; vantage = name; break; }
        }
        LastFrame = "frame " + vantage + " d=" + dist.ToString("0.00") + " r=" + targetRadius.ToString("0.00")
                    + " at " + look.ToString("0.00");

        // A one-shot burst spawned THIS frame has zero particles until it simulates; a
        // synchronous mid-verb shot would miss the flame pop and the grinding dust.
        if (fastForwardFx) FastForwardFx(look);
        // A popup spawned THIS frame still faces world +Z until its LateUpdate — it
        // photographs mirrored. Turn every billboard near the vessel before rendering.
        foreach (var fc in Object.FindObjectsByType<FaceCamera>(FindObjectsSortMode.None))
            if (fc != null && fc.isActiveAndEnabled && Vector3.Distance(fc.transform.position, look) <= NearM)
                fc.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

        var go = new GameObject("~VisualSweepCam");
        RenderTexture rt = null; Texture2D tex = null;
        try
        {
            var cam = go.AddComponent<Camera>();
            go.transform.position = pos;
            go.transform.LookAt(look);
            cam.fieldOfView = fov; cam.nearClipPlane = 0.02f; cam.farClipPlane = 50f;
            var camData = go.GetComponent<UniversalAdditionalCameraData>();
            if (camData == null) camData = go.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            int msaa = 1;
            var urp = UniversalRenderPipeline.asset;
            if (urp != null) msaa = Mathf.Max(1, urp.msaaSampleCount);
            rt = new RenderTexture(W, H, 24) { antiAliasing = msaa };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
            return file;
        }
        finally
        {
            if (rt != null) Object.DestroyImmediate(rt);
            if (tex != null) Object.DestroyImmediate(tex);
            Object.DestroyImmediate(go);
        }
    }

    static void FastForwardFx(Vector3 near)
    {
        foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
        {
            if (ps == null || !ps.gameObject.activeInHierarchy || !ps.isPlaying) continue;
            if (ps.GetComponentInParent<AtmosphereVfx>() != null) continue;
            if (Vector3.Distance(ps.transform.position, near) > 1.5f) continue;
            ps.Simulate(0.35f, true, false);
            ps.Play(true);
        }
    }

    // ---- recording ----------------------------------------------------------------

    static readonly List<string> s_rows = new List<string>();
    static readonly List<string> s_lines = new List<string>();
    static readonly Dictionary<string, int[]> s_perModule = new Dictionary<string, int[]>();   // ok, fail, skip, forced
    static string s_stepDir, s_stepStem, s_lastMid;
    public static int Ok, Fails, Skips, Photographed;

    public static void BeginRun()
    {
        s_rows.Clear(); s_lines.Clear(); s_perModule.Clear();
        Ok = Fails = Skips = Photographed = 0; s_lastMid = null; s_stepDir = null;
        if (Directory.Exists(Dir)) Directory.Delete(Dir, true);   // a stale sheet is worse than none
        Directory.CreateDirectory(Dir);
    }

    /// Where the next step's pictures go. Called by the autopilot BEFORE Perform, so a
    /// mid-verb shot fired from inside a handler lands in the right folder.
    public static void BeginStep(int moduleIndex, string module, int step, string taskId)
    {
        s_stepDir = Path.Combine(Dir, moduleIndex.ToString("00") + "-" + module);
        s_stepStem = step.ToString("00") + "-" + taskId;
        s_lastMid = null;
        if (!s_perModule.ContainsKey(module)) s_perModule[module] = new int[4];
    }

    /// SimulatedRun.MidVerb target: the verb in flight, photographed synchronously.
    public static void MidVerb(GameObject go, string tag)
    {
        if (tag == "flame") NoteFlame(AnyFlameLit());   // the match is at the sample NOW
        if (go == null || s_stepDir == null) return;
        try { s_lastMid = Snap(go, Path.Combine(s_stepDir, s_stepStem + "-mid-" + tag + ".png"), true); }
        catch (System.Exception e) { Debug.LogWarning("[VisualSweep] mid-verb shot failed: " + e.Message); }
    }

    public static Verdict Record(string module, ExperimentTask task, string kind, LiquidPhysics vessel,
                                 IList<ReactionRule> rules, float targetC, bool honest)
    {
        var rule = rules != null && rules.Count > 0 ? rules[rules.Count - 1] : null;
        var e = ExpectFor(kind, rule);
        string file = null, frame = "";
        if (vessel != null)
        {
            try { file = Snap(vessel.gameObject, Path.Combine(s_stepDir, s_stepStem + ".png"), false); Photographed++; frame = " · " + LastFrame; }
            catch (System.Exception ex) { Debug.LogWarning("[VisualSweep] shot failed: " + ex.Message); }
        }
        var o = Probe(vessel);
        float bp = vessel != null && vessel.currentChemical != null ? vessel.currentChemical.boilingPointC : 100f;
        var v = Judge(e, o, rule, targetC, bp);
        ClearFlameLatch();                                   // one step's evidence, not the next's

        if (!s_perModule.TryGetValue(module, out var tally)) s_perModule[module] = tally = new int[4];
        if (v.status == "OK") { Ok++; tally[0]++; } else if (v.Fail) { Fails++; tally[1]++; } else { Skips++; tally[2]++; }
        if (!honest) tally[3]++;

        string expected = rule != null
            ? rule.name + (string.IsNullOrEmpty(rule.expectedObservation) ? "" : ": \"" + rule.expectedObservation + "\"")
            : kind;
        s_rows.Add(string.Join("\t", new[]
        {
            module, s_stepStem, task.taskId, Tsv(task.label), kind, honest ? "honest" : "FORCED",
            e.ToString(), Tsv(expected), Tsv(o + frame), v.status, Tsv(v.reason),
            file != null ? Rel(file) : "", s_lastMid != null ? Rel(s_lastMid) : ""
        }));
        s_lines.Add("  [" + v.status.PadRight(4) + "] " + (honest ? "" : "FORCED ") + task.taskId.PadRight(22) + " " + expected
                    + "\n         → " + v.reason
                    + "\n         · " + o + frame
                    + (file != null ? "\n         · " + Rel(file) : "") + (s_lastMid != null ? " + " + Path.GetFileName(s_lastMid) : ""));
        return v;
    }

    static string Tsv(string s) => (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " / ");
    static string Rel(string p) => p.Replace('\\', '/');

    public static string Summary(string module)
        => s_perModule.TryGetValue(module, out var t)
            ? "OK " + t[0] + " · FAIL " + t[1] + " · SKIP " + t[2] + (t[3] > 0 ? " · FORCED " + t[3] : "")
            : "not photographed";

    public static void WriteReport(string headline)
    {
        Directory.CreateDirectory(Dir);
        var m = new StringBuilder();
        m.AppendLine("module\tstep\ttaskId\tlabel\tkind\tcompletion\texpect\texpected\tobserved\tstatus\treason\tfile\tmid");
        foreach (var r in s_rows) m.AppendLine(r);
        File.WriteAllText(Manifest, m.ToString());

        var sb = new StringBuilder();
        sb.AppendLine("=== PharmaSynth — VISUAL sweep (Play mode, honest verbs, one close-up per step) ===");
        sb.AppendLine("  " + headline);
        sb.AppendLine("  photographed " + Photographed + " step(s): OK " + Ok + " · FAIL " + Fails + " · SKIP " + Skips);
        sb.AppendLine("  pictures → " + Dir + "/   ·   sheets: python Tools/visual-sheet.py");
        sb.AppendLine();
        sb.AppendLine("  Each step is judged against the fired ReactionRule's manuscript observation (or the verb's");
        sb.AppendLine("  own contract: fill visible, mound shown, at temperature, boiling, chilled). SKIP = the picture");
        sb.AppendLine("  is the only evidence (flame confirms, the methane rig). FORCED = the honest verbs could not");
        sb.AppendLine("  complete the step in Play mode and it was pushed past — a completion-detection finding.");
        sb.AppendLine();
        sb.AppendLine("--- every step ---");
        string cur = null;
        for (int i = 0; i < s_rows.Count; i++)
        {
            string mod = s_rows[i].Substring(0, s_rows[i].IndexOf('\t'));
            if (mod != cur) { cur = mod; sb.AppendLine(); sb.AppendLine("  " + mod + " — " + Summary(mod)); }
            sb.AppendLine(s_lines[i]);
        }
        sb.AppendLine();
        sb.AppendLine("--- what this still CANNOT show -----------------------------------------------");
        sb.AppendLine("  The pour STREAM (reagents arrive through AddLiquid, not a tilted bottle), the dropper");
        sb.AppendLine("  squeeze, hand feel, and anything only a headset frame shows. Wrong-mix / overheat smoke");
        sb.AppendLine("  is not exercised here (correct play only) — SimulatedMisplay owns that path.");
        File.WriteAllText(Report, sb.ToString());
    }
}
#endif
