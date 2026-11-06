using System.Collections.Generic;

/// Which ROLE a rack tube is playing, worked out from what the player poured into it
/// (the player can pick any tube, so a guide fixed to one tube is useless; the tube
/// detects its role and sets the guide text dynamically).
///
/// Exp 2 authors five enol tubes, four alkaline-oxidation tubes and four acidic ones, and
/// each was pinned to a SPECIFIC bench tube. In VR the player grabs whichever tube is
/// nearest, so "pour the ethanol into tube 0" is guidance they cannot act on: pouring the
/// right reagent into the wrong-numbered tube was graded a wrong-reagent mistake even
/// though the chemistry was correct. A rack group's members are interchangeable glassware,
/// so the role should follow the pour, not the other way round.
///
/// ⛔ CLAIMING MUST BE DEFERRED, not decided on the first pour. The four alkaline tubes all
/// take KMnO₄ then NaOH and differ only at the THIRD reagent (n-butyl / sec-butyl /
/// tert-butyl). Claiming eagerly would assign a role arbitrarily on pour one and then
/// punish a perfectly correct third pour. So a tube holds a CANDIDATE SET that each pour
/// narrows; it is claimed only once one candidate survives, and a pour is wrong only when
/// the set empties. The enol group happens to disambiguate on pour one (five different
/// alcohols) — that falls out of the same rule rather than needing a special case.
///
/// Pure and keyed by strings so the whole rule set is suite-testable with no scene, no
/// ScriptableObjects and no rack.
public static class VesselRoleMatch
{
    /// Roles still possible for a tube, given what it already holds.
    ///
    /// `roles[i]` is the set of reagent keys role i accepts. `poured` is what has gone in
    /// so far. `taken` are roles a SIBLING tube has already claimed — without that, two
    /// tubes could both claim the same role and the rack would complete on one of them.
    ///
    /// A role survives while every poured reagent is one it accepts. Amounts are not
    /// consulted: over-pouring a named reagent is an over-pour of the right thing, which
    /// the binding already tolerates, and re-litigating it here would reject correct play.
    public static List<int> Candidates(IReadOnlyList<IReadOnlyList<string>> roles,
                                       IReadOnlyList<string> poured,
                                       ICollection<int> taken = null)
    {
        var result = new List<int>();
        if (roles == null) return result;

        for (int i = 0; i < roles.Count; i++)
        {
            if (taken != null && taken.Contains(i)) continue;
            var accepts = roles[i];
            if (accepts == null) continue;

            bool ok = true;
            if (poured != null)
                for (int p = 0; p < poured.Count && ok; p++)
                {
                    if (string.IsNullOrEmpty(poured[p])) continue;
                    ok = Accepts(accepts, poured[p]);
                }
            if (ok) result.Add(i);
        }
        return result;
    }

    static bool Accepts(IReadOnlyList<string> accepts, string reagent)
    {
        for (int i = 0; i < accepts.Count; i++)
            if (accepts[i] == reagent) return true;
        return false;
    }

    /// The role index once exactly one candidate is left, else -1 (still ambiguous, or
    /// nothing fits). "Still ambiguous" is a normal, expected state for most of a rack
    /// step — it is not an error and must not be reported as one.
    public static int ClaimedRole(IReadOnlyList<int> candidates)
        => candidates != null && candidates.Count == 1 ? candidates[0] : -1;

    /// No role can explain this tube's contents — the genuine wrong-reagent case, and the
    /// ONLY one. Distinguished from "ambiguous" so that a correct pour mid-rack is never
    /// graded a mistake just because the tube has not committed to a role yet.
    public static bool IsImpossible(IReadOnlyList<int> candidates)
        => candidates == null || candidates.Count == 0;

    /// Which POOL a bench tube belongs to, from its scene name.
    ///
    /// ⛔ All 23 bench tubes share ONE LabItem itemId (`kit-testtube`), hard-glass included, so
    /// the name prefix is the only family signal there is. A hard-glass tube must NEVER pool
    /// with soft glass: Exp 6's naked-flame dry distillation is the whole point of that tube,
    /// and a soft tube "claiming" the role would put the player's hand over a burner with the
    /// wrong glass. Returns "" for anything that is not a poolable tube.
    public const string RegularFamily = "regular";
    public const string HardGlassFamily = "hardglass";

    public static string FamilyOf(string benchName)
    {
        if (string.IsNullOrEmpty(benchName)) return "";
        if (benchName.StartsWith("Kit_Hard-GlassTestTube")) return HardGlassFamily;
        if (benchName.StartsWith("Kit_TestTube")) return RegularFamily;
        return "";
    }

    /// Which POOL any bench glassware belongs to: tubes by name family (hard glass
    /// shares their itemId), everything else by its LabItem itemId — the spares are named
    /// `Eq_WatchGlass`, `Kit_WatchGlass_1_11`, `WatchGlass_2`, so no name rule survives them.
    /// "" when the object is not poolable at all.
    public static string PoolKey(string benchName, string itemId)
    {
        string fam = FamilyOf(benchName);
        if (fam != "") return fam;
        return string.IsNullOrEmpty(itemId) ? "" : itemId;
    }

    /// Would adding `reagent` leave any role standing? Asked BEFORE the pour is recorded,
    /// so the binding can grade it without having to roll the tube's history back.
    public static bool WouldAccept(IReadOnlyList<IReadOnlyList<string>> roles,
                                   IReadOnlyList<string> poured,
                                   string reagent,
                                   ICollection<int> taken = null)
    {
        var next = new List<string>();
        if (poured != null) next.AddRange(poured);
        next.Add(reagent);
        return !IsImpossible(Candidates(roles, next, taken));
    }
}
