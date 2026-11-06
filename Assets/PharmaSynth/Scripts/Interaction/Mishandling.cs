using System.Collections.Generic;
using UnityEngine;

/// Pure rules for mishandling penalties (§2: spill & breakage):
/// which apparatus is fragile, when an impact shatters it, and
/// when an un-held bottle counts as spilling. Kept plain-C# so the self-tests
/// pin the policy.
public static class Mishandling
{
    /// THIN GLASS that shatters when dropped — and nothing else.
    /// Non-glass equipment must never break.
    /// Sturdy solid glass (rods, funnels), droppers and porcelain (dish/crucible)
    /// are not fragile: the rod is the STIR tool, and shattering a tool
    /// mid-verb is punitive; porcelain survives a bench drop in reality. Metal,
    /// wood and plastic tools never break.
    private static readonly HashSet<string> Breakables = new HashSet<string>
    {
        "Beaker_100mL", "Beaker_100mL_WithLiquid",
        "Beaker_500mL", "Beaker_500mL_WithLiquid",
        "DistillingFlask", "FlorenceFlask",
        "ErlenmeyerFlask_400mL", "ErlenmeyerFlask_400mL_WithLiquid",
        "GraduatedCylinder_50mL", "GraduatedCylinder_50mL_WithLiquid",
        "TestTube", "TestTube_WithLiquid",
        "Vial", "Vial_Brown", "Vial_Brown_WithLabel", "Vial_WithLabel",
        "WatchGlass",
    };

    /// Sturdy glass/ceramic: robust in the hand, but still CLINKS like
    /// glass on impact instead of falling through to the wooden knock.
    private static readonly HashSet<string> CeramicOrSolidGlass = new HashSet<string>
    {
        "GlassRod", "Funnel", "Dropper", "EvaporatingDish", "Crucible",
        "Motar", "Pestle", "Spatula",   // porcelain (pack spells mortar "Motar"; spatula = porcelain per manuscript)
        // Sturdy glass tools/tubes — clink, never shatter mid-verb.
        "Condenser", "DeliveryTube", "Thermometer",
    };

    public static bool IsBreakable(string prefabName) => Breakables.Contains(prefabName ?? "");
    public static IEnumerable<string> BreakableNames => Breakables;

    /// An impact at or above this speed shatters glass. 7.0 m/s ≈ a free fall
    /// of ~2.5 m onto a hard surface — only extreme heights or a genuine hard
    /// throw breaks (4.5 is hair-trigger in the headset; a
    /// normal bench-height drop must survive). Held items are additionally
    /// immune in BreakableGlassware regardless of speed.
    public const float DefaultBreakSpeed = 7.0f;

    public static bool ShouldBreak(float impactSpeed, float breakSpeed = DefaultBreakSpeed)
        => impactSpeed >= breakSpeed;

    /// Metal apparatus — everything else non-glass lands as a dull wooden knock.
    private static readonly HashSet<string> MetalItems = new HashSet<string>
    {
        "CrucibleTongs", "Forceps", "Scoopula", "IronRing",   // (Spatula moved to porcelain/ceramic)
        "Tripod", "RetortStand", "WireGauze", "BunsenBurner", "AlcoholBurner",
        "TestTubeHolder_Metal", "Balance", "ClayTriangle",   // clay triangle = wire frame
        "WaterBath", "UtilityClamp", "Aspirator",            // metal apparatus
    };

    /// SoundBank key for a drop/impact clatter, by material.
    public static string DropSoundKey(string prefabName)
    {
        if (IsBreakable(prefabName) || CeramicOrSolidGlass.Contains(prefabName ?? "")) return "glass-clink";
        if (MetalItems.Contains(prefabName ?? "")) return "drop-metal";
        return "drop-wood";
    }

    /// SoundBank key for a fired reaction's observable outcome.
    public static string SfxForOutcome(ReactionOutcome outcome)
    {
        switch (outcome)
        {
            case ReactionOutcome.Fizzing:
            case ReactionOutcome.GasEvolved:
                return "reaction-fizz";
            case ReactionOutcome.None:
                return "";                       // negative test: nothing to hear
            default:
                return "mixture-complete";       // colour change / precipitate / odour cue
        }
    }

    /// A reagent bottle is SPILLING when nobody holds it, it still has liquid,
    /// and it lies tipped past the threshold (LiquidPourer drains it; this
    /// decides whether that drain is a graded mishandling event).
    public static bool IsSpilling(float tiltDegrees, bool held, float liquidMl, float tiltThreshold = 60f)
        => !held && liquidMl > 0.5f && tiltDegrees > tiltThreshold;

    /// Impact-loudness curve (clinks/shatters must not fire at full
    /// volume on the softest contacts): a gentle set-down is barely audible, a
    /// solid drop reads at full volume. Below ImpactSound's minSpeed nothing
    /// plays at all; this shapes what remains.
    public static float ImpactVolume01(float impactSpeed, float quietSpeed = 0.7f, float fullSpeed = 4.0f)
        => Mathf.Lerp(0.2f, 1f, Mathf.InverseLerp(quietSpeed, fullSpeed, impactSpeed));

    /// Human-readable item name for mistake/shatter messages:
    /// the proper name of what shattered, never its code name.
    /// Prefers the authored LabItem.displayName, then the contained chemical,
    /// then a prettified version of the GO/prefab code name.
    public static string DisplayNameFor(GameObject go)
    {
        if (go == null) return "Item";
        var item = go.GetComponent<LabItem>();
        if (item != null && !string.IsNullOrWhiteSpace(item.displayName)) return item.displayName;
        var lp = go.GetComponent<LiquidPhysics>();
        if (lp != null && lp.currentChemical != null
            && !string.IsNullOrWhiteSpace(lp.currentChemical.chemicalName))
            return lp.currentChemical.chemicalName;
        return Prettify(go.name);
    }

    /// "Reagent_AceticAcid_Diluted (2)" → "Acetic Acid Diluted". Pure + tested.
    public static string Prettify(string codeName)
    {
        if (string.IsNullOrWhiteSpace(codeName)) return "Item";
        string n = codeName.Replace("(Clone)", "");
        int paren = n.IndexOf(" (");                        // strip "(2)" copy suffixes
        if (paren > 0) n = n.Substring(0, paren);
        foreach (var prefix in new[] { "Reagent_", "Prop_", "Chem_", "Template_Raw_", "Raw_", "Spare_", "Staged" })
            if (n.StartsWith(prefix)) { n = n.Substring(prefix.Length); break; }
        foreach (var suffix in new[] { "_WithLiquid", "_WithLabel" })
            if (n.EndsWith(suffix)) n = n.Substring(0, n.Length - suffix.Length);
        n = n.Replace('_', ' ').Replace('-', ' ');
        // Split camelCase ("AceticAcid" → "Acetic Acid") — but only at a real
        // word boundary (upper between two lowers), so unit tails like the
        // "mL" in "100mL" stay intact.
        var sb = new System.Text.StringBuilder(n.Length + 8);
        for (int i = 0; i < n.Length; i++)
        {
            if (i > 0 && char.IsUpper(n[i]) && char.IsLower(n[i - 1])
                && i + 1 < n.Length && char.IsLower(n[i + 1])) sb.Append(' ');
            sb.Append(n[i]);
        }
        var parts = sb.ToString().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? "Item" : string.Join(" ", parts);
    }
}
