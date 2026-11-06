using UnityEngine;

/// Pure format functions for the live-status feedback layer: vessel
/// name tags that show contents/volume, hover-card "Now:" lines, and station
/// billboards that show temperature / sim progress. Kept plain so the suite
/// pins every format. All output is TMP-safe ASCII except the em-dash, which
/// the dialogue system already uses everywhere (LiberationSans has it); the
/// degree glyph is deliberately avoided ("62 C", not "62°C").
public static class VesselStatusMath
{
    /// "Beaker — 120 ml Ethanol" / "Beaker — empty". A reagent bottle whose
    /// display name IS the chemical drops the redundant suffix ("Ethanol — 120 ml").
    public static string Compose(string displayName, string chemName, float ml)
    {
        string name = string.IsNullOrEmpty(displayName) ? "Vessel" : displayName;
        if (ml <= 1f) return name + " — empty";
        string chem = string.IsNullOrEmpty(chemName) ? "liquid" : chemName;
        if (chem == name) return name + " — " + Mathf.RoundToInt(ml) + " ml";
        return name + " — " + Mathf.RoundToInt(ml) + " ml " + chem;
    }

    /// Mixed-contents name tag (clear text of the current
    /// contents of this tube and their proportions): the ledger story IS the
    /// proportions — "Test Tube 3 — Ethanol 1 ml + Distilled Water 10 ml".
    public static string ComposeMixed(string displayName, string ledgerSummary)
    {
        string name = string.IsNullOrEmpty(displayName) ? "Vessel" : displayName;
        if (string.IsNullOrEmpty(ledgerSummary)) return name + " — empty";
        return name + " — " + ledgerSummary;
    }

    /// Hover-card live suffix: "Now: 120 ml Ethanol" (+ "Mixed from: …" when
    /// the vessel holds more than one story entry) / "Now: empty".
    public static string HoverLine(string chemName, float ml, string ledgerSummary, int ledgerCount)
    {
        if (ml <= 1f) return "Now: empty";
        string chem = string.IsNullOrEmpty(chemName) ? "liquid" : chemName;
        string line = "Now: " + Mathf.RoundToInt(ml) + " ml " + chem;
        if (ledgerCount > 1 && !string.IsNullOrEmpty(ledgerSummary))
            line += "\nMixed from: " + ledgerSummary;
        return line;
    }

    /// Heat-station billboard: "4. Heat the mix\n62 C -> 150 C".
    public static string HeatLine(string baseLabel, float currentC, float targetC)
        => baseLabel + "\n" + Mathf.RoundToInt(currentC) + " C -> " + Mathf.RoundToInt(targetC) + " C";

    /// Live temperature goal on a vessel that owns a zone-free heat/chill step
    /// so the temperature can be monitored:
    /// "25 C — warm to 50 C (water bath)" / "25 C — chill to 8 C
    /// (ice bath)". Empty once the goal side is reached — the step's own
    /// completion feedback takes over from there.
    public static string TempGoalLine(float currentC, float targetC, bool chill)
    {
        if (chill ? currentC <= targetC : currentC >= targetC) return "";
        return Mathf.RoundToInt(currentC) + " C — " + (chill ? "chill to " : "warm to ")
               + Mathf.RoundToInt(targetC) + " C" + (chill ? " (ice bath)" : " (water bath)");
    }

    /// Generic sim-progress billboard: "5. Filter\nFiltering 40%".
    /// What this vessel still wants for the step being guided.
    ///
    /// Pouring is the commonest verb in the game, so the have/required readout shows
    /// BEFORE the first pour into this vessel, not only once pouring starts. Shown on the
    /// guided destination only, so the bench does not turn into a wall of numbers.
    ///
    /// Returns "" when nothing is wanted, which is also what a satisfied step returns —
    /// the label should go quiet the moment the amount is reached, not sit there reading
    /// "40 / 40" and inviting an overpour.
    public static string NeedLine(string chemName, float have, float required, bool solid = false)
    {
        if (required <= 0f || string.IsNullOrEmpty(chemName)) return "";
        if (have >= required - 0.01f) return "";
        string unit = solid ? " g" : " ml";
        return chemName + "  " + have.ToString("0.#") + " / " + required.ToString("0.#") + unit;
    }

    /// Why the last pour bounced. The scold is spoken once and gone; the vessel
    /// keeps saying it, because "nothing happened" is the single most confusing outcome in
    /// the lab — a headset player poured four reagents into tubes that could not take them
    /// and had no way to tell. Cleared by an accepted pour or by emptying the glass.
    public static string RefusalLine(string reagent)
        => string.IsNullOrEmpty(reagent) ? "" : "✗ " + reagent + " \u2014 not part of this vessel's step";

    /// An unclaimed member of a pool has no role yet, and saying so is what makes "any tube
    /// will do" legible: the glass tells the player it is free rather than staying blank.
    public static string FreeVesselLine(bool poolMember, bool ambiguous, bool empty)
        => poolMember && ambiguous && empty ? "Free \u2014 becomes whatever you pour in first" : "";

    public static string ProgressLine(string baseLabel, string verb, float frac01)
        => baseLabel + "\n" + verb + " " + Mathf.RoundToInt(Mathf.Clamp01(frac01) * 100f) + "%";
}
