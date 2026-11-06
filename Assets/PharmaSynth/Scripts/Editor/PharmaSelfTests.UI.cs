#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// Self-tests: HUD, fades, menus, the watch panel and text safety.
public static partial class PharmaSelfTests
{
    static void HudRigSuite()
    {
        // Lazy-follow solver math
        var p = HudFollowSolver.Params.Default;
        A("hud: yaw wrap", Near(HudFollowSolver.DeltaYawDeg(350f, 10f), 20f));
        var anchor = HudFollowSolver.AnchorPoint(Vector3.zero, 0f, in p);
        A("hud: anchor at distance", Near(anchor.z, p.distance) && Near(anchor.y, p.heightOffset) && Near(anchor.x, 0f));
        var s = HudFollowSolver.Snapped(Vector3.zero, 0f, in p);
        var before = s.pos; var beforeYaw = s.yawDeg;
        HudFollowSolver.Step(ref s, Vector3.zero, p.yawDeadzoneDeg * 0.5f, in p, 1f / 60f);
        A("hud: deadzone holds", (s.pos - before).magnitude < 0.0001f && Near(s.yawDeg, beforeYaw));
        for (int i = 0; i < 240; i++) HudFollowSolver.Step(ref s, Vector3.zero, 90f, in p, 1f / 60f);
        A("hud: converges on 90 deg turn", Near(s.yawDeg, 90f, 1.5f));
        var target = HudFollowSolver.AnchorPoint(Vector3.zero, 90f, in p);
        A("hud: converges to anchor", (s.pos - target).magnitude < 0.05f);

        // Narration line events + bubble toggling + dialogue-bar mirroring
        var go = new GameObject("hudtest");
        try
        {
            var narr = go.AddComponent<NPCNarrationController>();
            var panel = new GameObject("Bubble"); panel.transform.SetParent(go.transform);
            narr.SetPanelRoot(panel);
            string started = null; int ends = 0;
            narr.LineStarted += (l, sec) => started = l;
            narr.LineEnded += () => ends++;
            narr.BeginLine("hello", 2f);
            A("narration: BeginLine shows bubble + event", panel.activeSelf && started == "hello" && narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine hides bubble + event", !panel.activeSelf && ends == 1 && !narr.IsSpeaking);
            narr.EndLine();
            A("narration: EndLine idempotent", ends == 1);

            // PACING ("pharmee might become too annoying if he
            // speaks rapidly"). The post-reveal READ hold is the floor that keeps
            // a finished line up long enough to actually read.
            A("narration: a finished line always holds the minimum read time",
                Near(NPCNarrationController.HoldSecondsAfterReveal(4f, 3.8f), 1.9f)
                && Near(NPCNarrationController.HoldSecondsAfterReveal(9f, 3f), 6f));
            A("narration: the read floor is unaffected by a long reveal",
                NPCNarrationController.HoldSecondsAfterReveal(2f, 12f) >= 1.9f);

            // OVERLAP: Pharmee must not keep speaking where Dr. Jimenez speaks.
            // Two causes: Pharmee's autonomous brain reacting during the scripted
            // review, and the gate scheduling each review beat on a FIXED dwell, so a
            // line longer than dwell x cps overruns its slot and the next beat lands
            // on top. SecondsFor reports a line's REAL floor time so schedulers can't
            // under-book it.
            float shortLine = narr.SecondsFor("Short.", 4f);
            float longLine = narr.SecondsFor(new string('x', 200), 4f);
            A("narration: a long line reports MORE floor time than a short one",
                longLine > shortLine && longLine > 4f);
            A("narration: floor time is never under the authored dwell",
                narr.SecondsFor("Short.", 4f) >= 4f - 0.01f);
            A("narration: nobody holds the floor when nothing is speaking",
                !NPCNarrationController.FloorBusy);

            var barRoot = new GameObject("Bar"); barRoot.transform.SetParent(go.transform);
            var lineGo = new GameObject("Line"); lineGo.transform.SetParent(barRoot.transform);
            var lineText = lineGo.AddComponent<TMPro.TextMeshProUGUI>();
            var bar = go.AddComponent<HudDialogueBar>();
            bar.Bind(narr, barRoot, null, lineText);
            narr.BeginLine("step two", 1f);
            A("hudbar: mirrors line", barRoot.activeSelf && lineText.text == "step two");
            narr.EndLine();
            A("hudbar: hides on end", !barRoot.activeSelf && lineText.text == string.Empty);

            // MULTI-SPEAKER: Dr. Jimenez owns a SEPARATE narration
            // controller, so a bar subscribed to one channel shows nothing while he
            // speaks, and the scene's baked "Pharmee" name would stick. Each channel
            // carries its own name.
            var speakerGo = new GameObject("Speaker"); speakerGo.transform.SetParent(barRoot.transform);
            var speakerText = speakerGo.AddComponent<TMPro.TextMeshProUGUI>();
            var jimGo = new GameObject("JimenezNarr");
            jimGo.transform.SetParent(go.transform);
            var jimNarr = jimGo.AddComponent<NPCNarrationController>();
            bar.Bind(narr, barRoot, speakerText, lineText, null, null,
                new[] { new HudDialogueBar.Channel
                    { narration = jimNarr, speakerName = "Dr. Jimenez", portrait = null } });
            jimNarr.BeginLine("your documentation counts", 1f);
            A("hudbar: a SECOND speaker reaches the HUD bar",
                barRoot.activeSelf && lineText.text == "your documentation counts");
            A("hudbar: names the speaker who is talking", speakerText.text == "Dr. Jimenez");
            narr.BeginLine("back to me", 1f);
            A("hudbar: name switches back to the primary", speakerText.text == "Pharmee");
            // A stale end from the NON-active speaker must not hide the live line.
            jimNarr.EndLine();
            A("hudbar: the other speaker's end does not steal the bar",
                barRoot.activeSelf && lineText.text == "back to me");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void FadeSuite()
    {
        A("fade: ease endpoints", Near(FadeState.Ease01(0f), 0f) && Near(FadeState.Ease01(1f), 1f));
        A("fade: ease midpoint", Near(FadeState.Ease01(0.5f), 0.5f));
        A("fade: ease monotone", FadeState.Ease01(0.3f) < FadeState.Ease01(0.6f));
        var f = new FadeState();
        f.Begin(1f, 0.5f);
        A("fade: busy while ramping", f.Busy && f.Alpha < 1f);
        for (int i = 0; i < 40; i++) f.Step(1f / 60f);      // 0.66 s > 0.5 s
        A("fade: reaches target + settles", Near(f.Alpha, 1f) && !f.Busy);
        f.Begin(0f, 0.2f);
        for (int i = 0; i < 30; i++) f.Step(1f / 60f);
        A("fade: fades back down", Near(f.Alpha, 0f) && !f.Busy);
    }

    static void LabMenuSuite()
    {
        A("labmenu: restart label running", LabMenuController.RestartConfirmText(true).Contains("attempt"));
        A("labmenu: restart label idle", LabMenuController.RestartConfirmText(false).Contains("Reset"));
        var go = new GameObject("labmenu");
        try
        {
            var lm = go.AddComponent<LabMenuController>();
            var settings = new GameObject("Settings"); settings.transform.SetParent(go.transform); settings.SetActive(false);
            var confirm = go.AddComponent<ChoicePanelController>();
            var root = new GameObject("CRoot"); root.transform.SetParent(go.transform);
            confirm.Bind(root, null, null, null);
            lm.Bind(settings, confirm, null, null, null);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles on", settings.activeSelf);
            lm.OnSettingsToggle();
            A("labmenu: settings toggles off", !settings.activeSelf);
            lm.OnQuitToMenu();
            A("labmenu: quit opens confirm", confirm.IsOpen);
            lm.OnConfirmOption(1);   // Stay
            A("labmenu: cancel closes confirm", !confirm.IsOpen);
            lm.OnQuitToMenu();
            lm.OnConfirmOption(0);   // edit mode: scene-load path is play-guarded
            A("labmenu: confirm closes too", !confirm.IsOpen);
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    static void UISuite()
    {
        A("ui: time format", ExperimentHudController.FormatTime(920) == "00:15:20");
        A("ui: percent format", ExperimentHudController.FormatPercent(0.754f) == "75%");
        A("ui: progress drops on mistakes", Near(ExperimentHudController.DisplayedProgress(0.8f, 2, 0.05f), 0.7f));
        A("ui: instruction from hint", PharmeeBrain.InstructionFor(new ExperimentTask { label = "X", hint = "do x" }) == "do x");
        // Two-line "fact\n→ action" hints: Pharmee speaks ONLY the action line;
        // the fact stays on the wrist panel (speaking both reads out whole paragraphs).
        A("ui: pharmee speaks the ACTION line of a two-line hint",
            PharmeeBrain.InstructionFor(new ExperimentTask { label = "X",
                hint = "KMnO4 oxidises benzaldehyde.\n→ Squeeze 2 dropper-loads in." })
            == "Squeeze 2 dropper-loads in."
            && PharmeeBrain.InstructionFor(new ExperimentTask { label = "X", hint = "plain hint" }) == "plain hint");
    }

    static void RosterDataSuite()
    {
        // Cutscenes: every experiment has all 4 kinds, each with beats.
        string[] keys = { "Methane","ChemicalCompounding","EthylAlcohol","BenzoicAcid","Acetanilide",
            "Acetone","Chloroform","Benzamide","WineMaking" };
        string cdir = "Assets/PharmaSynth/ScriptableObjects/Cutscenes/";
        int csOk = 0;
        foreach (var k in keys)
            foreach (var kind in new[]{ "Intro","ReagentPrep","Success","Failure" })
            {
                var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(cdir + k + "_" + kind + ".asset");
                if (cs != null && cs.beats.Count > 0 && cs.TotalDuration() > 0f) csOk++;
            }
        // 44 → 36: Aspirin + Caffeine dropped (9 modules × 4 kinds).
        A("roster: 36 cutscenes present with beats", csOk == 36);

        // Quiz banks: one per catalog module, all valid, 3 questions, scoring works.
        string qdir = "Assets/PharmaSynth/ScriptableObjects/Quizzes/";
        int qBanks = 0, qTotal = 0; bool allValid = true, scoreOk = true, coverage = true;
        foreach (var e in ExperimentCatalog.Entries)
        {
            QuizBank bank = null;
            foreach (var g in AssetDatabase.FindAssets("t:QuizBank", new[]{ qdir.TrimEnd('/') }))
            {
                var b = AssetDatabase.LoadAssetAtPath<QuizBank>(AssetDatabase.GUIDToAssetPath(g));
                if (b != null && b.moduleId == e.moduleId) { bank = b; break; }
            }
            if (bank == null) { coverage = false; continue; }
            qBanks++; qTotal += bank.Count;
            // ≥3 questions per bank (Chemical Compounding carries 5: manuscript
            // Exp 2 has five distinct tests).
            if (!bank.AllValid() || bank.Count < 3) allValid = false;
            // all-correct answers → perfect score
            var ans = new List<int>();
            foreach (var q in bank.questions) ans.Add(q.correctIndex);
            if (!Near(bank.Score(ans), 1f)) scoreOk = false;
        }
        A("roster: quiz bank per experiment (9)", coverage && qBanks == 9);
        // 33 → 35: compounding's rebuild took its bank from 3 to 5 questions.
        // 35 → 29: Aspirin + Caffeine (3 each) dropped.
        A("roster: 29 quiz questions total, all valid (4 opts, in-range answer)", qTotal == 29 && allValid);
        A("roster: quiz scoring (all-correct = 100%)", scoreOk);
    }

    // Font-safe glyph sanitiser for the lab pads / holo board.
    static void GlyphSafeSuite()
    {
        A("glyph: arrow → ->", GlyphSafe.Sanitize("purple→brown") == "purple->brown");
        A("glyph: gas ↑ → (g)", GlyphSafe.Sanitize("CH4↑") == "CH4(g)");
        A("glyph: delta → (heat)", GlyphSafe.Sanitize("NaOH →Δ CH4") == "NaOH ->(heat) CH4");
        A("glyph: equilibrium ⇌", GlyphSafe.Sanitize("A⇌B") == "A<=>B");
        A("glyph: box ▶ → »", GlyphSafe.Sanitize("▶ step") == "» step");
        A("glyph: plain text untouched", GlyphSafe.Sanitize("Prepare 0.1N HCl (250 mL)") == "Prepare 0.1N HCl (250 mL)");
        A("glyph: null/empty safe", GlyphSafe.Sanitize(null) == null && GlyphSafe.Sanitize("") == "");
    }

    // Fitted watch geometry (the solid Tripo watch cannot wrap a
    // wrist — the band is generated around the MEASURED wrist cross-section).
    static void WatchMathSuite()
    {
        var pts = new System.Collections.Generic.List<UnityEngine.Vector3>
        {
            new UnityEngine.Vector3(-0.03f, 0.00f, -0.045f),
            new UnityEngine.Vector3( 0.03f, 0.02f, -0.045f),
            new UnityEngine.Vector3( 0.00f, -0.01f, -0.044f),
            new UnityEngine.Vector3( 0.00f, 0.03f, -0.046f),
            new UnityEngine.Vector3( 0.50f, 0.50f, 0.200f)    // far point — must be excluded
        };
        var s = WatchMath.MeasureSlice(pts, -0.045f, 0.008f);
        A("watch: slice excludes far points", s.samples == 4);
        A("watch: slice half-width measured", Near(s.halfExtents.x, 0.03f));
        A("watch: slice centre offset found", Near(s.center.y, 0.01f));
        A("watch: empty slice is safe", WatchMath.MeasureSlice(pts, 9f, 0.001f).samples == 0);
        A("watch: band adds skin clearance",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.03f, 0.02f)).x, 0.03f + WatchMath.BandClearance));
        A("watch: band floors tiny measurements", WatchMath.BandRadii(UnityEngine.Vector2.zero).x >= WatchMath.MinHalfWidth);
        A("watch: palm bulge cannot widen the band",
            Near(WatchMath.BandRadii(new UnityEngine.Vector2(0.05f, 0.02f)).x, WatchMath.MaxHalfWidth));
        A("watch: face diameter clamped", WatchMath.FaceDiameter(1f) <= 0.0401f);
        var mesh = WatchMath.BuildBandMesh(new UnityEngine.Vector2(0.03f, 0.02f), 0.004f, 16, 8);
        A("watch: band mesh has full geometry", mesh.vertexCount == 17 * 9 && mesh.triangles.Length == 16 * 8 * 6);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    // Focused checklist for the single holo procedures board:
    // full-detail text overflowed the pads — collapse everything but the active phase).
    static void ChecklistPagerSuite()
    {
        var g = new TaskGraph(new List<ExperimentTask> {
            T("prep", TaskPhase.ReagentPrep, 1, LabSkill.Measuring, RubricCategory.Procedure),
            T("mix", TaskPhase.Synthesis, 2, LabSkill.Transfer, RubricCategory.Procedure, "prep"),
            T("heat", TaskPhase.Synthesis, 1, LabSkill.Heating, RubricCategory.Procedure, "mix"),
            T("test", TaskPhase.ChemicalTests, 1, LabSkill.TestInterpretation, RubricCategory.ChemicalTests, "heat"),
        });

        A("pager: active = first available phase", ChecklistPager.ActivePhase(g) == TaskPhase.ReagentPrep);
        string s = ChecklistPager.BuildFocusedText(g);
        A("pager: active phase in detail", s.Contains("prep") && s.Contains("»"));
        A("pager: future phase collapsed", s.Contains("(2 steps)") && !s.Contains("mix"));
        A("pager: future tests collapsed", s.Contains("(1 steps)") && !s.Contains("test"));

        g.TryComplete("prep");
        A("pager: active advances", ChecklistPager.ActivePhase(g) == TaskPhase.Synthesis);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: completed phase folds to done n/n", s.Contains("done 1/1"));
        A("pager: new active in detail", s.Contains("mix") && s.Contains("heat"));
        A("pager: folded phase hides steps", !s.Contains("prep"));

        g.TryComplete("mix"); g.TryComplete("heat"); g.TryComplete("test");
        A("pager: all complete → no active", ChecklistPager.ActivePhase(g) == null);
        s = ChecklistPager.BuildFocusedText(g);
        A("pager: everything folded when done", s.Contains("done 1/1") && s.Contains("done 2/2") && !s.Contains("»"));

        A("pager: null graph safe", ChecklistPager.BuildFocusedText(null) == "" && ChecklistPager.ActivePhase(null) == null);
        A("pager: null runner header safe", ChecklistPager.BuildHeader(null) == "");

        // Bounded by construction: line count ≤ phases + active-phase tasks + 1.
        var lines = ChecklistPager.BuildFocusedText(g).Split('\n');
        A("pager: folded output is compact", lines.Length <= 6);
    }
}
#endif
