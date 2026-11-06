using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pour wiring for HAND-PLACED bottles, so tipping a reagent-shelf bottle shows a
/// stream and puddle. ExperimentSceneBuilder wires runtime-spawned
/// pourables, but the 16 shelf display bottles (and batch-H cabinet stock) are
/// scene objects with LiquidPhysics only. This mirrors the builder's pourable
/// block for an existing bottle, idempotently, callable from the editor menu
/// (Tools ▸ PharmaSynth ▸ Wire Shelf Pourers) and from runtime builders.
///
/// Edit-mode note: SpillMistake/LiquidPourer self-bind in Awake/Start at play
/// time, so edit-mode wiring only has to ADD the components and set the
/// serialized fields (registry, spout); the runner param matters only for
/// runtime callers.
public static class ShelfPourWiring
{
    /// Ensure the bottle pours visibly and grades spills. Returns the number of
    /// components/objects added (0 = was already fully wired, -1 = not a liquid
    /// container).
    public static int WireBottle(GameObject bottle, ExperimentRunner runner, ReactionRegistry registry)
    {
        if (bottle == null) return -1;
        var lp = bottle.GetComponent<LiquidPhysics>();
        if (lp == null) return -1;

        int added = 0;
        if (lp.registry == null && registry != null) { lp.registry = registry; added++; }

        // Visible fill: most ChemLab bottle prefabs have no liquid child
        // mesh, so pours in/out were invisible. Idempotent — authored _WithLiquid
        // setups and already-wired bottles are left alone.
        if (lp.mainRenderer == null || lp.mainRenderer.sharedMaterial == null
            || !lp.mainRenderer.sharedMaterial.HasProperty("_Fill"))
        {
            ExperimentSceneBuilder.EnsureLiquidVisual(bottle, lp);
            if (lp.mainRenderer != null) added++;
        }

        var pourer = bottle.GetComponent<LiquidPourer>();
        if (pourer == null) { pourer = bottle.AddComponent<LiquidPourer>(); added++; }
        if (pourer.spout == null)
        {
            // Mouth at the TOP of the bottle's real bounds (a fixed 0.12 m guess sits
            // mid-body on tall bottles).
            // SOLID meshes only: a bottle that has poured before carries a
            // world-space StreamLine child, and encapsulating it walked the spout
            // out to wherever the last arc landed.
            var spout = new GameObject("Spout").transform;
            spout.SetParent(bottle.transform, false);
            var b = ExperimentSceneBuilder.SolidWorldBounds(bottle);
            if (b.size.sqrMagnitude > 1e-8f)
                spout.position = new Vector3(b.center.x, b.max.y - 0.005f, b.center.z);
            else spout.localPosition = new Vector3(0f, 0.12f, 0f);
            pourer.spout = spout;
            added++;
        }

        if (bottle.GetComponent<SpillMistake>() == null)
        {
            var spill = bottle.AddComponent<SpillMistake>();
            spill.Bind(runner, lp, bottle.GetComponent<XRGrab>(), Mishandling.DisplayNameFor(bottle));
            added++;
        }

        if (bottle.GetComponent<HazardousMixReactor>() == null)
        {
            bottle.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
            added++;
        }

        // Feedback layer: reaction/mix/overflow popups + a live contents
        // tag when the bottle already carries a ProximityLabel.
        if (bottle.GetComponent<MixFeedback>() == null)
        {
            bottle.AddComponent<MixFeedback>().Bind(lp);
            added++;
        }
        var label = bottle.GetComponent<ProximityLabel>();
        if (label != null && bottle.GetComponent<VesselStatus>() == null)
        {
            string display = lp.currentChemical != null ? lp.currentChemical.chemicalName : bottle.name;
            bottle.AddComponent<VesselStatus>().Bind(lp, label, display, 1.4f);
            added++;
        }
        return added;
    }
}
