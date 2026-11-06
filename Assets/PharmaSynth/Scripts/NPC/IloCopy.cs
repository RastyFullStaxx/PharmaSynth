/// Intended Learning Outcomes per experiment (Pharmee states
/// the session's objectives in the opening dialogue). The 8 manuscript modules
/// use the VERBATIM Appendix C "Objectives" text (transcribed in
/// Docs/manuscript-reconciliation.md §2 — the chemistry authority); Methane is
/// game-authored in the same voice and is client-CONFIRMED as the
/// tutorial. (Aspirin + Caffeine were dropped with their modules.)
public static class IloCopy
{
    /// Pharmee's lead-in beat — also the injector's idempotence marker.
    public const string LeadIn = "Here's what you'll be able to do by the end of this session:";

    public static string[] ForModule(string moduleId)
    {
        switch (moduleId)
        {
            case "tutorial-methane":                 // game-authored — PENDING CLIENT CONFIRM
                return new[]
                {
                    "Objective 1: Synthesize methane gas by heating sodium acetate with soda lime.",
                    "Objective 2: Determine its identity through the flame test.",
                };
            case "prelim-chemical-compounding":      // Appendix C Exp 2
                return new[]
                {
                    "Objective 1: Write chemical reactions involved in some organic compounds such as alcohols, aldehydes, ketones, carboxylic acids and esters.",
                    "Objective 2: Differentiate tests for different organic compounds.",
                };
            case "prelim-ethyl-alcohol":             // Appendix C Exp 3
                return new[]
                {
                    "Objective 1: Synthesize ethyl alcohol.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "midterm-benzoic-acid":             // Appendix C Exp 4
                return new[]
                {
                    "Objective 1: Synthesize benzoic acid.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "midterm-acetanilide":              // Appendix C Exp 5
                return new[]
                {
                    "Objective 1: Synthesize acetanilide.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "midterm-acetone":                  // Appendix C Exp 6
                return new[]
                {
                    "Objective 1: Synthesize acetone.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "midterm-chloroform":               // Appendix C Exp 7
                return new[]
                {
                    "Objective 1: Synthesize chloroform.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "final-benzamide":                  // Appendix C Exp 8
                return new[]
                {
                    "Objective 1: Synthesize benzamide.",
                    "Objective 2: Determine its identity through chemical tests.",
                };
            case "final-winemaking":                 // Appendix C Exp 9
                return new[]
                {
                    "Objective: Learn the basic methodology in preparation and synthesis of alcohol using the fermentation technique, with basic ingredients found in any household kitchen.",
                };
            default:
                return new string[0];
        }
    }

    /// Spoken pacing for one ILO beat (~16 chars/sec, floor 2.5 s, cap 6 s).
    public static float BeatSeconds(string line)
    {
        int chars = string.IsNullOrEmpty(line) ? 0 : line.Length;
        float s = chars / 16f;
        return s < 2.5f ? 2.5f : (s > 6f ? 6f : s);
    }
}
