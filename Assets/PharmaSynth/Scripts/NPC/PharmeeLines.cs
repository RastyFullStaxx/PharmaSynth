/// Rich dialogue pools for the NPCs (varied messages and
/// interactions for Pharmee and the Dr.). Pure static data +
/// a deterministic picker so the self-tests can pin variety without RNG. Pharmee
/// rotates through these instead of repeating one canned line; Dr. Jimenez draws
/// his stern examiner remarks from the exam pools. Client-reviewable copy (the
/// §5 dialogue sign-off still applies).
public static class PharmeeLines
{
    // ---- Pharmee (friendly robot guide) -------------------------------------
    public static readonly string[] Greetings =
    {
        "Welcome back to the lab! I'm Pharmee — follow your wrist checklist and I'll be right here.",
        "Suited up and ready? Let's synthesise something great today!",
        "Great to see you! Take it step by step and check your wrist for the procedure.",
        "Lab's all yours. Work safe, work smart, and call on me anytime.",
    };

    public static readonly string[] Praise =
    {
        "Nice work!", "Perfect.", "That's the way!", "Excellent technique.",
        "Spot on.", "You're getting the hang of this!", "Cleanly done.",
        "Textbook.", "Lovely control.", "That's the one.",
        "Good hands.", "Just right.",
    };

    public static readonly string[] Celebrate =
    {
        "Outstanding! Experiment complete — beautiful synthesis!",
        "You nailed it! That's textbook lab work.",
        "Brilliant! Your product looks great and your method was sound.",
        "Success! Dr. Jimenez will be impressed with that one.",
    };

    public static readonly string[] Encourage =
    {
        "Good effort — review the steps and give it another go. You've got this!",
        "Almost there. Check where it slipped and try once more.",
        "Don't worry, every chemist reworks a prep. Let's run it again.",
        "So close! Tighten up the tricky steps and you'll pass next time.",
    };

    // Pharmee's ambient channel is the game's only TEACHING voice for the
    // zone-free tools: there are no station pads or labels telling
    // you the bath needs water and a flame, or that the two scoops carry
    // different charges, so if he never mentions them the player has to guess.
    // Every line below describes a mechanic that is really in the build.
    public static readonly string[] Idle =
    {
        "Take your time — precision beats speed in here.",
        "Remember to keep your goggles on while you work.",
        "Reagents run low, so pour only what the step needs.",
        "Your wrist checklist updates as you go — glance at it anytime.",
        "The fume hood is there for the smelly stuff. Use it!",
        "Doing great. Steady hands.",
        "If a bottle runs dry, I can reset the period for you.",
        // Tool teaching — the zone-free verbs, in plain language.
        "The water bath only works once there's water in it and a lit burner beside it. Then it warms anything you rest nearby.",
        "Need something chilled? Stand it in the ice bucket and watch its temperature drop.",
        "To stir, just take a glass rod and circle it inside the vessel. Anywhere in the lab works.",
        "The scoopula lifts a couple of grams a dip. For the really small weighings, use the little porcelain spatula instead.",
        "Strike a match on the side of the matchbox, then bring it to the burner.",
        "Rest anything on the balance and it reads out the weight — vessel included.",
        "Dip a litmus strip into a mixture to test whether it's acid or base.",
        "Pour through the funnel and the liquid runs straight into whatever sits underneath.",
        "Every tool you need is already out on the bench — picking the right one is half the skill.",
    };

    // ERROR POOLS. These fire more often than any others, so they need the most variety;
    // three lines repeated across a whole experiment stop sounding like a mentor and start
    // sounding like an alarm. Pharmee CORRECTS, never scolds: he names what is wrong and
    // points at where the answer is, and never gives the answer away (that is the
    // assessment's job).
    public static readonly string[] WrongReagent =
    {
        "Hmm, that isn't the reagent this step needs — check your wrist checklist.",
        "Wrong bottle! Look again at the procedure for the right reagent.",
        "That's not it. The step calls for a different chemical.",
        "Close, but not the one. Read the step again and check the label.",
        "That bottle isn't part of this step — put it back and have another look.",
        "Careful, that's a different chemical. Your checklist names the one you want.",
        "Not that one. Check the label against your wrist before you pour.",
    };

    public static readonly string[] WrongStep =
    {
        "Let's not skip ahead — finish the current step first.",
        "One step at a time; that one comes later.",
        "Hold on — complete what's in front of you before moving on.",
        "That comes further down the procedure. Finish this one first.",
        "Not yet — there's a step still open above that one.",
        "Patience! The order matters in this experiment.",
    };

    public static readonly string[] Overheat =
    {
        "Careful — it's overheating! Ease off the heat.",
        "Too hot! Back off the burner before it boils over.",
        "Watch the temperature — lower the flame.",
        "That's running away from you — pull it off the heat for a moment.",
        "Gently! Too much heat will ruin the batch.",
    };

    public static readonly string[] Safety =
    {
        "Safety first! Mind the hazard and keep your protective gear on.",
        "Easy — handle the glassware gently, please.",
        "Careful there. Let's keep the bench clean and safe.",
        "Mind that spill — clean as you go.",
        "Whoa! Let's slow down before something breaks.",
        "Keep that away from the flame, please.",
        "Set it down gently — glass doesn't forgive a knock.",
        "That belongs in the fume hood. Let's not breathe it in.",
    };

    // ---- Dr. Jimenez (stern examiner, gives NO hints) ------------------------
    // Voice direction (for the ElevenLabs pass): measured, unhurried,
    // lower register. Full sentences, no contractions, no exclamation marks — the
    // authority comes from restraint. He never names a reagent, a step or a tool,
    // because naming one would be a hint. Every line stands alone so the pools can
    // be spoken in any order.
    public static readonly string[] ExamGreeting =
    {
        "I am Doctor Jimenez. I will be observing this assessment. Proceed.",
        "This is a graded exercise. Work carefully. I am watching your technique.",
        "Begin when you are ready. I evaluate method, safety, and results.",
        "Your procedure is on your wrist. My assessment is my own. Start when you wish.",
        "I will not be assisting you today. Show me what you have learned.",
    };

    public static readonly string[] ExamRemarks =
    {
        "Mind your procedure.",
        "I am noting your technique.",
        "Observe your safety protocol.",
        "Efficiency counts toward your grade.",
        "Precision, please.",
        "I expect proper use of the fume hood.",
        "Keep your workspace orderly.",
        "Measure twice. Pour once.",
        "A careful chemist is never a hurried one.",
        "Your protective equipment stays on until the bench is clear.",
        "Read the step before you reach for the glass.",
        "Waste is a mark against you. Take only what the step requires.",
        "I have seen that shortcut before. It rarely ends well.",
        "Steady hands, please. The glassware is not replaceable.",
    };

    /// Spoken once on entering the lab in Tutorial Mode. A single fixed line, not a
    /// pool: it is an instruction, and a first-timer should hear the same words every
    /// time rather than a variation. Keep it under one breath — it plays right after
    /// the ordinary greeting.
    public const string TutorialOrientation =
        "This is practice mode. Whatever you need next will glow, and the marker points right at it. Nothing here is graded, so take your time — and you can skip a step if you get stuck.";

    /// Spoken at the threshold in Tutorial Mode, while the procedures board is showing
    /// itself unprompted. Fixed, not a pool, for the same reason as the line
    /// above: it explains a control, and an explanation should not vary.
    public const string TutorialPreview =
        "Here is the whole procedure before we start. Read it through, then walk in when you are ready — and poke me any time if you get stuck.";

    /// The VOICED stem under the practice summary. The summary text itself carries the
    /// step and correction counts (TutorialCoach.SummaryText) and so can never have a
    /// clip of its own; the voice says this and the bubble shows the numbers.
    public const string PracticeComplete = "Practice complete.";

    // ---- Post-experiment review flow (congrats → Jimenez quiz
    // briefing → score remarks → entrance debrief). Finite pools, numbers stay on
    // the grade card so every line is voice-recordable.
    public static readonly string[] TestsDoneLines =
    {
        "Excellent — every test is complete! Dr. Jimenez will review your work now. Let's head over to him.",
        "That's the last test done! Time for your assessment review — Dr. Jimenez is waiting.",
        "All tests finished — wonderful work! Off to Dr. Jimenez for your documentation and review.",
    };

    public static readonly string[] JimenezQuizBrief =
    {
        "Well done reaching this stage. Before your grade is final, you will document your work.",
        "Answer the questions on the data sheet. Your documentation counts toward your final grade. Begin.",
        "Take your time. Accuracy over speed. The data sheet is ready for you.",
        "The bench work is finished. The record is not. Complete your data sheet.",
        "A result you cannot document is a result I cannot credit. Proceed to the data sheet.",
    };

    /// Dr. Jimenez's lead-in to the module ILO recap at QuizIntro. Lives HERE, not
    /// as a literal at the call site: VoiceCorpus can only voice what it can
    /// enumerate, and a bare string in PharmeeGatekeeper was invisible to it, so
    /// the line played as blips.
    public const string JimenezIloLeadIn =
        "Before you answer — recall this session's objectives.";

    public static readonly string[] JimenezPassRemarks =
    {
        "A commendable performance. Your technique meets the standard.",
        "Satisfactory work. Your method was sound and your record is complete.",
        "You have met the requirements of this assessment. Well executed.",
        "Precise and orderly. I am marking this assessment as passed.",
        "That is how the procedure is meant to be run. My compliments.",
        "Clean work, clean bench, clean record. You have passed.",
    };

    public static readonly string[] JimenezFailRemarks =
    {
        "Not yet to standard. Review your procedure and attempt it again.",
        "Your record shows gaps. Repeat the exercise with more care.",
        "This attempt falls short of the requirement. I expect better on your retry.",
        "The chemistry was not at fault here. The method was. Run it again.",
        "I have seen you work more carefully than this. Try once more.",
        "A chemist who cannot repeat a result has not yet obtained one. Again, please.",
        "You have the knowledge. Today you did not apply it. That is correctable.",
    };

    public static readonly string[] DebriefCongrats =
    {
        "Congratulations on completing the assessment and the quiz!",
        "Assessment complete — quiz and all. Well done making it through!",
        "That's a full experiment, tests, and documentation finished. Great job!",
    };

    /// Banded performance remark for Pharmee's entrance debrief (Debrief is only
    /// reachable after a pass, so bands split the passing range). Pure for tests.
    public static string DebriefRemark(float gradeTotal)
    {
        if (gradeTotal >= 97f) return "A flawless run — truly impressive work.";
        if (gradeTotal >= 93f) return "A strong performance — you clearly know your way around this lab.";
        return "A solid pass — keep practising and the tricky steps will feel natural.";
    }

    /// Overload: the FINAL campaign pass swaps in a campaign-flavoured
    /// remark (the standard banded line reads oddly when nothing is left).
    public static string DebriefRemark(float gradeTotal, bool campaignComplete)
    {
        if (!campaignComplete) return DebriefRemark(gradeTotal);
        if (gradeTotal >= 97f) return "And what a way to finish — a flawless final experiment!";
        return "And that was the very last experiment — you've done it!";
    }

    /// Whole-campaign completion celebration: spoken at the entrance
    /// instead of the unlock announcement once every catalog experiment is passed
    /// (the chain is 9 since Aspirin + Caffeine were dropped — the copy
    /// deliberately names no count so it survives a roster change).
    public static readonly string[] CampaignComplete =
    {
        "Congratulations, graduate! You've passed every experiment in the campaign — Tutorial, Prelims, Midterms and Finals. This lab is yours!",
        "That was the final experiment — the entire campaign is complete! Dr. Jimenez and I are so proud of you. Gear up, synth it up — you did it!",
        "Every single synthesis, test and quiz — finished! You've mastered the whole PharmaSynth course. Outstanding work, chemist!",
    };

    // ---- Guided lab tour (storyboard beats, refined to exceed) ---------------
    // Played in order when the player picks "Lab Tour" — Pharmee walks them through
    // each area in turn. The last beat is the closer.
    public static readonly string[] TourBeats =
    {
        "Welcome to the lab! Let's take a quick tour so you feel right at home before any graded work.",
        "Flick your wrist face-up and glance at it — your holographic procedures board lays out every step of the experiment, live.",
        "The bench in front of you is your main workspace: prepare reagents, run your reactions, and handle glassware here.",
        "Every instrument in this lab stays out on the bench, all the time — choosing the right one is part of the work. Reach for whatever your checklist calls for.",
        "The reagent cabinets along the wall hold your chemicals. Take only what each step needs — the bottles are limited!",
        "Flick your wrist anytime for the live checklist, and watch the progress bar and timer up top to track how you're doing.",
        "The Settings button up top lets you tune audio, text size and comfort options whenever you like.",
        "Check your wrist panel for each step. Poke me whenever you're ready to take on a graded campaign!",
    };

    /// Deterministic pool picker (tests pin it): wraps any index, non-negative.
    public static string Pick(string[] pool, int n)
    {
        if (pool == null || pool.Length == 0) return "";
        int i = ((n % pool.Length) + pool.Length) % pool.Length;
        return pool[i];
    }
}
