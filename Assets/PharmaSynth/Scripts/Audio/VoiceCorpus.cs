using System.Collections.Generic;

/// Every code-authored NPC line, with its speaker — the voice-over corpus
/// (both NPCs speak). The manifest exporter adds the
/// cutscene SO beats on top (assets aren't reachable from runtime code).
/// Numbers were deliberately kept OUT of spoken lines (grade bands, finite
/// unlock variants), so this enumeration is exhaustive and finite.
public static class VoiceCorpus
{
    public struct Line
    {
        public VoiceSpeaker speaker;
        public string text;
        /// Which pool the line came from. Carried into the manifest so voice
        /// generation can be STAGED (e.g. Pharmee's
        /// welcome first, then the remaining beeps one scene at a time) — you can
        /// buy one coherent scene at a time instead of the whole corpus.
        public string group;
        public Line(VoiceSpeaker s, string t, string g = "Misc") { speaker = s; text = t; group = g; }
    }

    /// Lines authored in DATA rather than in code: each task's `longProcessMessage`, the
    /// narration over a time skip ("One week later...").
    ///
    /// ⛔ CodeLines() cannot see these, so without this list they never reach the voice
    /// manifest and play as BLIPS. The `voice: every corpus line has a clip` pin over
    /// CodeLines() alone stays green while they are missing.
    public static List<Line> DataLines(ExperimentLibrary library)
    {
        var lines = new List<Line>();
        if (library == null) return lines;
        foreach (var m in library.modules)
        {
            if (m == null) continue;
            foreach (var t in m.graphTasks)
            {
                if (t == null || string.IsNullOrWhiteSpace(t.longProcessMessage)) continue;
                lines.Add(new Line(VoiceSpeaker.Jimenez, t.longProcessMessage.Trim(), "TimeSkip"));
            }
        }
        return lines;
    }

    public static List<Line> CodeLines()
    {
        var lines = new List<Line>();

        // Pharmee pools (variety, praise, warnings, tour, review flow).
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Greetings, "Greeting");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Praise, "Praise");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Celebrate, "Celebrate");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Encourage, "Encourage");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Idle, "Idle");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.WrongReagent, "Error");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.WrongStep, "Error");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Overheat, "Error");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.Safety, "Error");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.TourBeats, "Tour");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.TestsDoneLines, "Review");
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.DebriefCongrats, "Review");
        // The campaign-completion celebration, the payoff line of the whole game; without
        // it here it plays as blips.
        AddPool(lines, VoiceSpeaker.Pharmee, PharmeeLines.CampaignComplete, "Celebrate");
        // Tutorial Mode's one-off orientation. A single line, not a pool.
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.TutorialOrientation, "Tour"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.TutorialPreview, "Tour"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.PracticeComplete, "Tour"));

        // Dr. Jimenez pools (exam voice + review verdicts).
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.ExamGreeting, "Exam");
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.ExamRemarks, "Exam");
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezQuizBrief, "Review");
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezPassRemarks, "Review");
        AddPool(lines, VoiceSpeaker.Jimenez, PharmeeLines.JimenezFailRemarks, "Review");

        // Banded debrief remarks (finite bands, numbers live on the grade card).
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(98f), "Review"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(94f), "Review"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(90f), "Review"));
        // ⛔ BOTH overloads. The campaign-complete flavour was never enumerated, so
        // the very last thing Pharmee says in the whole campaign played as blips.
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(98f, true), "Celebrate"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, PharmeeLines.DebriefRemark(90f, true), "Celebrate"));

        // Door-gate lines (the scene uses the code defaults — verified:
        // the RobotNPC instance carries no overrides, so what is voiced is what
        // plays). This is the whole front-of-game flow: welcome -> mode choice ->
        // experiment pick -> PPE -> threshold.
        var gate = new PharmeeGatekeeper.GateLines();
        AddPool(lines, VoiceSpeaker.Pharmee, new[]
        {
            gate.approach, gate.labTour, gate.campaignExplain, gate.episodePrompt,
            gate.lockedEpisode, gate.coatPrompt, gate.readyPrompt, gate.thresholdWarn,
            gate.congrats, gate.supplyWarn, gate.welcome,
            gate.ppePartial, gate.ppeBlocked,
        }, "Gate");

        // Guided-tour beats (location-triggered guide).
        AddPool(lines, VoiceSpeaker.Pharmee, LabTourGuide.DefaultBeatTexts, "Tour");

        // ILO dialogue (verbatim Appendix C + game-authored trio). BOTH NPCs speak
        // these, so BOTH need clips: Pharmee reads them as intro-cutscene beats
        // (IloBeatInjector), and Dr. Jimenez RECAPS the same text at QuizIntro
        // (PharmeeGatekeeper.SpeakJimenezBrief). VoiceBank is keyed by
        // speaker + text-hash, so a clip filed under one speaker is INVISIBLE to
        // the other, so Jimenez's recap needs its own clips under his speaker or it plays
        // as blips.
        lines.Add(new Line(VoiceSpeaker.Pharmee, IloCopy.LeadIn, "Objectives"));
        lines.Add(new Line(VoiceSpeaker.Jimenez, PharmeeLines.JimenezIloLeadIn, "Objectives"));
        foreach (var id in ModuleIds)
            foreach (var ilo in IloCopy.ForModule(id))
            {
                lines.Add(new Line(VoiceSpeaker.Pharmee, ilo, "Objectives"));
                lines.Add(new Line(VoiceSpeaker.Jimenez, ilo, "Objectives"));
            }

        // Hazard warnings (HUD toast copy doubles as the spoken warning).
        foreach (HazardousMix.HazardOutcome o in System.Enum.GetValues(typeof(HazardousMix.HazardOutcome)))
        {
            string w = HazardousMix.WarnLineFor(o);
            if (!string.IsNullOrEmpty(w)) lines.Add(new Line(VoiceSpeaker.Pharmee, w, "Error"));
        }

        // Unlock announcements: finite variants over the catalog (one unlock at a
        // time in a linear roster) + the nothing-new fallback.
        foreach (var e in ExperimentCatalog.Entries)
            lines.Add(new Line(VoiceSpeaker.Pharmee, UnlockDiff.AnnouncementFor(new List<string> { e.moduleId }), "Unlock"));
        lines.Add(new Line(VoiceSpeaker.Pharmee, UnlockDiff.AnnouncementFor(new List<string>()), "Unlock"));

        // Dedupe by speaker+normalised text (pools share a few lines).
        var seen = new HashSet<string>();
        var unique = new List<Line>();
        foreach (var l in lines)
        {
            if (string.IsNullOrEmpty(l.text)) continue;
            string key = (int)l.speaker + ":" + VoiceLineId.For(l.text);
            if (seen.Add(key)) unique.Add(l);
        }
        return unique;
    }

    public static readonly string[] ModuleIds =
    {
        "tutorial-methane", "prelim-chemical-compounding", "prelim-ethyl-alcohol",
        "midterm-benzoic-acid", "midterm-acetanilide", "midterm-acetone", "midterm-chloroform",
        "final-benzamide", "final-winemaking",
    };

    private static void AddPool(List<Line> into, VoiceSpeaker s, string[] pool, string group = "Misc")
    {
        if (pool == null) return;
        foreach (var t in pool) into.Add(new Line(s, t, group));
    }
}
