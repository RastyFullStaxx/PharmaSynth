using UnityEngine;

/// The one place a lab reset restores the APPARATUS, not just the reagents.
/// Every used vessel is emptied, cooled, unlabelled and unclaimed.
///
/// Without it, re-homing positions, putting flames out, rebuilding wiring and refilling
/// bottles still leaves every piece of glassware as the last attempt left it: the
/// contents, the temperature, the residue stamp and the " — Tollen's test" role suffix on the
/// label all carry into the next run.
///
/// ⛔ Apparatus is a vessel the builders marked as USED — one carrying a `CleanableVessel`,
/// which every stage, kit and adopter builder adds to glassware the player fills and never to
/// a source. Never use a naming rule ("not `Raw_`/`Reagent_`"): it sweeps up
/// sources too: the wash bottles and the methane tutorial's charge jar and glass
/// tube, which starves the tutorial's setup step. Simulate Everything covers this; a suite
/// fixture cannot, because a fixture vessel is whatever the pin says it is. Each used
/// vessel is taken back to empty, cool, unlabelled and unclaimed;
/// `LiquidPhysics.ClearContents` raises `Emptied`, so a pooled vessel gives its role back
/// through the same seam a rinse uses. The bottles then refill through the supply monitor,
/// and a solid jar gets its mound back at full. Positions, flames and consumables stay with
/// `DropRespawn.ResetAllHome`; task wiring with `ExperimentSceneBuilder.ClearBenchBindings`.
///
/// Called at the gate's Loading entry — every run path crosses it (first run, Retry,
/// Restart) — and by `ResetLabForReturn`, which rebuilds the stage without passing Loading.
public static class LabReset
{
    /// A vessel the player USES, as the builders define it. Sources (bottles, the wash
    /// bottle, the methane charge jar) never carry this component.
    public static bool IsUsedVessel(LiquidPhysics lp)
        => lp != null && lp.GetComponent<CleanableVessel>() != null;

    /// A shelf reagent (bottle or jar, `Raw_*` / `Reagent_*`, not a builder-owned work
    /// vessel) only ever takes back its OWN chemical. A loaded scoop brushing a jar of a
    /// different solid, a dropper squeezed over the wrong bottle, or a stream crossing the
    /// shelf would deposit into the stock, and every later dip from that jar would be the
    /// wrong chemical.
    public static bool RefusesTransfer(LiquidPhysics target, ChemicalData incoming)
    {
        if (target == null || incoming == null || IsUsedVessel(target)) return false;
        bool stock = false;
        for (var t = target.transform; t != null && !stock; t = t.parent) stock = IsReagent(t.name);
        return stock && target.currentChemical != incoming;
    }

    /// Pure, suite-pinned: is this the name of a SHELF reagent jar? (The solid-jar fitter and
    /// its pin use it; the reset itself goes by IsUsedVessel.)
    public static bool IsReagent(string objectName)
        => !string.IsNullOrEmpty(objectName)
           && (objectName.StartsWith("Raw_") || objectName.StartsWith("Reagent_"));

    /// Restore every apparatus vessel and refill every reagent. Returns the apparatus count.
    public static int ResetApparatus()
    {
        int n = 0;
        foreach (var lp in Object.FindObjectsByType<LiquidPhysics>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!IsUsedVessel(lp)) continue;
            n++;
            var last = lp.currentChemical != null ? lp.currentChemical : lp.LastChemical;
            lp.ClearContents();                                  // → Emptied → ResetRole on the binding
            if (last != null && (last.state == PhysicalState.Solid || last.state == PhysicalState.Powder))
                ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, last, 0f);   // hide a leftover mound

            var temp = lp.GetComponent<TemperatureSim>();
            if (temp != null) temp.ResetSim();

            var clean = lp.GetComponent<CleanableVessel>();
            if (clean != null) clean.ResetResidue();

            var status = lp.GetComponent<VesselStatus>();
            if (status != null) { status.SetRoleSuffix(""); status.Refresh(); }
        }
        ReagentSupplyMonitor.RefillSourceBottles();               // every SOURCE to 150, solid jars re-mounded
        return n;
    }
}
