using UnityEngine;

/// Pure classification of chemically-BAD mixes, so a wrong mixture produces smoke,
/// fire, colour and a penalty. Runs on the existing
/// LiquidPhysics.WrongReagentMixed seam, i.e. only for A+B pairs with NO
/// registered ReactionRule (real chemistry always wins). Direction-aware:
/// `current` is what's in the vessel, `incoming` is what got poured in.
/// Everything stays isolated in-sim per the manuscript ("dangerous conditions
/// isolated without risk") — effects + penalty, never player harm.
public static class HazardousMix
{
    public enum HazardOutcome { None, ToxicGas, FireOrExplosion, AcidSpatter, GenericFizz }

    /// Acid + hypochlorite → toxic gas; oxidizer + flammable → fire; pouring a
    /// liquid INTO a concentrated acid → spatter; anything else unknown → fizz.
    public static HazardOutcome Classify(ChemicalData current, ChemicalData incoming)
    {
        if (current == null || incoming == null || current == incoming) return HazardOutcome.None;

        bool acidMeetsHypochlorite =
            (IsAcid(current) && IsHypochloriteLike(incoming)) ||
            (IsAcid(incoming) && IsHypochloriteLike(current));
        if (acidMeetsHypochlorite) return HazardOutcome.ToxicGas;

        bool oxidizerMeetsFlammable =
            (current.isOxidizer && incoming.hazard == HazardType.Flammable) ||
            (incoming.isOxidizer && current.hazard == HazardType.Flammable);
        if (oxidizerMeetsFlammable) return HazardOutcome.FireOrExplosion;

        // "Add acid to water, never water to acid" — liquid INTO conc. acid spatters.
        if (current.isConcentratedAcid && !incoming.isConcentratedAcid
            && incoming.state == PhysicalState.Liquid)
            return HazardOutcome.AcidSpatter;

        return HazardOutcome.GenericFizz;
    }

    public static bool IsAcid(ChemicalData c)
        => c != null && (c.isConcentratedAcid || c.pH < 3f);

    public static bool IsHypochloriteLike(ChemicalData c)
    {
        if (c == null || string.IsNullOrEmpty(c.chemicalName)) return false;
        string n = c.chemicalName.ToLowerInvariant();
        return n.Contains("hypochlorite") || n.Contains("bleach");
    }

    /// Consistent MistakeLog wiring — one table, mirrors the plan §3.7 matrix.
    public static LabErrorType ErrorTypeFor(HazardOutcome o)
    {
        switch (o)
        {
            case HazardOutcome.ToxicGas:
            case HazardOutcome.FireOrExplosion: return LabErrorType.HazardousAction;   // Materials&PPE
            case HazardOutcome.AcidSpatter: return LabErrorType.ChemicalContact;       // Materials&PPE
            default: return LabErrorType.WrongReagent;                                  // Procedure
        }
    }

    /// HUD-toast / subtitle copy per outcome (finite → voice-recordable).
    public static string WarnLineFor(HazardOutcome o)
    {
        switch (o)
        {
            case HazardOutcome.ToxicGas: return "Those must never mix — that's releasing toxic fumes!";
            case HazardOutcome.FireOrExplosion: return "That combination just ignited — oxidizers and flammables stay apart!";
            case HazardOutcome.AcidSpatter: return "Never pour into concentrated acid — it spatters violently!";
            case HazardOutcome.GenericFizz: return "That mixture isn't in the procedure — watch what you combine.";
            default: return "";
        }
    }

    /// Effect tint per outcome (chlorine green-yellow / flame orange / the acid's own colour).
    public static Color TintFor(HazardOutcome o, ChemicalData current)
    {
        switch (o)
        {
            case HazardOutcome.ToxicGas: return new Color(0.75f, 0.85f, 0.35f, 0.8f);
            case HazardOutcome.FireOrExplosion: return new Color(1f, 0.5f, 0.12f, 0.95f);
            case HazardOutcome.AcidSpatter:
                return current != null ? current.liquidColor : new Color(0.9f, 0.85f, 0.4f);
            default: return new Color(0.8f, 0.9f, 1f, 0.8f);
        }
    }
}
