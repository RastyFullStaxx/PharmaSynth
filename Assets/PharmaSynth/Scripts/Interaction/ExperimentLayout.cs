using System;
using System.Collections.Generic;
using UnityEngine;

/// Data description of one experiment's physical setup: where its stations, grabbable
/// props and reagent vessels go. The ExperimentSceneBuilder spawns this on module load,
/// so all 9 experiments share one lab scene instead of 9 hand-built scenes.
/// Positions are WORLD-space (the lab is a fixed room).
/// How a station completes. None = the prop simply entering the zone completes the
/// step. Heat/Crystallise/Filter/Collect run a sustained chemistry sim while the
/// prop occupies the zone. Stir/Grind/Weigh (append-only — serialized ints
/// stay valid) are TOOL verbs: circle the rod in the vessel, work the pestle in
/// the mortar, rest the right load on the balance pan.
public enum StationSim { None, Heat, Crystallise, Filter, Collect, Stir, Grind, Weigh }

[CreateAssetMenu(fileName = "ExperimentLayout", menuName = "PharmaSynth/Experiment Layout")]
public class ExperimentLayout : ScriptableObject
{
    [Serializable]
    public class Station
    {
        public string taskId;
        public string label;
        public string requiredItemId;     // grabbable prop that completes it (empty = any)
        public Vector3 pos;               // pad centre, world space (y = surface)
        [Tooltip("Real-verb sim driven while the prop occupies the zone. None = instant zone-touch completion.")]
        public StationSim sim = StationSim.None;
        [Tooltip("Target °C for Heat stations (distillation cut-off / water-bath).")]
        public float simTargetC = 80f;
    }

    [Serializable]
    public class Prop
    {
        public string prefabName;         // ChemLabEquipment prefab (by name) via the library
        public string itemId;             // matches a Station.requiredItemId
        public string displayName;        // proximity label
        public Vector3 pos;
        public float targetHeight = 0.16f;
        public bool pourable = false;     // add LiquidPourer + fill
        public string fillChemical = "";  // ChemicalData name to fill a pourable bottle with
        [Tooltip("Finite supply in ml (0 = auto: 2.5x the summed need, so ~2 spare pours).")]
        public float supplyMl = 0f;
    }

    [Serializable]
    public class Vessel
    {
        [Serializable] public class Bind
        {
            public string reagentChemical;
            public string taskId;
            [Tooltip("Minimum ml poured before the step completes (0 = any amount).")]
            public float requiredMl;
            [Tooltip("False = pouring only ACCUMULATES (expected, never a wrong-reagent mistake); something else — e.g. the weigh station — completes the task. (W5.8)")]
            public bool completesTask = true;
        }
        [Tooltip("BIND to an existing bench object by NAME (e.g. Kit_TestTube_0) instead of spawning a twin. " +
                 "The hard client rule keeps every tool permanently on the bench, so a layout that spawns its own " +
                 "tubes/beakers just duplicates it — Exp 2 shipped 20 Vessel_TestTube clones beside the real " +
                 "Kit_TestTube_0-18. When set, prefabName/pos/targetHeight are IGNORED (the object already exists " +
                 "and is placed by hand); only the bindings + rackGroup are attached. Leave empty to spawn.")]
        public string benchItem = "";

        public string prefabName;
        public string displayName;
        public Vector3 pos;
        public float targetHeight = 0.2f;
        public string startChemical = "";     // what the vessel starts holding (optional)
        public List<Bind> bindings = new List<Bind>();   // reagent poured in → task completed
        [Tooltip("Vessels sharing a rackGroup are ONE SET: a step they share only completes once EVERY member has had its reagent (RackTaskGroup). Exp 2's five enol tubes, its butyl alcohols, acetone-vs-acetaldehyde. Empty = a standalone vessel. Members author their shared bindings completesTask:false — the group completes them.")]
        public string rackGroup = "";

        [Tooltip("ZONE-FREE heat step (2026-07-17, replaces fixed Heat stations): >0 = the vessel's deferred (completesTask:false, no rack) task completes when the vessel has ALL its reagents AND has been heated to this temperature — wherever the player does it (water bath + lit burner, anywhere in the lab). VesselHeatTask registers the graph condition.")]
        public float heatToC = 0f;

        [Tooltip("FERMENTATION flask (Exp 3): set to the ferment taskId. Once prepare-must is done, this flask evolves CO₂ into any nearby limewater vessel (turning it milky); the named task completes when that vessel clouds. FermentationController; zone-free like the water bath.")]
        public string fermentTaskId = "";

        [Tooltip("ZONE-FREE chill step (Exp 4 crystallise): >0 = the chillTaskId task completes when this vessel holds something AND has been cooled to this temperature — set it in the ice bath, anywhere. VesselChillTask; ambient is 25 °C so it can never self-complete.")]
        public float chillToC = 0f;
        [Tooltip("The task chillToC completes (chill has no reagent binding of its own, so the owner is named explicitly).")]
        public string chillTaskId = "";

        [Tooltip("LITMUS confirmation (Exp 4 test-litmus): the named task completes when this vessel is served its reagents AND a litmus strip touches it while the mixture reads acid (strip turns red). VesselLitmusTask; zone-free.")]
        public string litmusTaskId = "";

        [Tooltip("EXPLICIT heat-task owner (Exp 6 heat-glow): when set, heatToC completes THIS task instead of the inferred first deferred binding — for heat steps with no reagents of their own. Empty = infer as before.")]
        public string heatTaskId = "";

        [Tooltip("WEIGH step on the bench balance (Exp 6 weigh-acetates): the named task completes when this vessel is served its solids AND rests settled on the balance pan (VesselWeighTask + the Eq_Balance WeighStation).")]
        public string weighTaskId = "";

        [Tooltip("VAPOR collection (Exp 6 collect-56): this vessel is the SOURCE — once the named task is available and the vessel is at heatToC, it converts its charge into the module's product, condensing into the nearest vessel whose binding expects it (VaporCollectController).")]
        public string vaporTaskId = "";

        [Tooltip("FLAME (non-)flammability confirmation (Exp 7 test-flammability): the named task completes when this vessel is served its sample AND a LIT match/burner flame is held within reach (VesselFlameTask; zone-free — the negative 'does not ignite' is the observation).")]
        public string flameTaskId = "";

        [Tooltip("ZONE-FREE stir step (Exp 8 'shake frequently while dropping'): the named task completes when the bench glass rod is circled inside this vessel while it holds liquid (StirController, vessel-relative — on the bench or in a hand).")]
        public string stirTaskId = "";
    }

    public string moduleId;
    public List<Station> stations = new List<Station>();
    public List<Prop> props = new List<Prop>();
    public List<Vessel> vessels = new List<Vessel>();
}
