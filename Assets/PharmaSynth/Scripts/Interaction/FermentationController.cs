using UnityEngine;

/// The zone-free fermentation → CO₂ → limewater mechanic (Exp 3's signature,
/// manuscript §A f–i). Once the must is prepared, the flask evolves CO₂; a
/// delivery tube leads it into a limewater test tube, which turns milky
/// (CaCO₃) — the proof fermentation is underway. Mirrors WaterBathController:
/// no station, no zone — the flask emits to whatever limewater vessel the
/// player brings near it, wherever in the lab.
///
/// The task is authored longProcess=true, so completing it fades the screen
/// black and returns "one week later" (TimeSkipController) — the manuscript's
/// week of standing, compressed.
public static class FermentationMath
{
    /// Limewater vessels within this distance of the fermenting flask receive CO₂.
    public const float DeliveryRadius = 0.5f;
    /// The must is "fermenting" once its prep step is done and the flask holds it.
    public static bool IsFermenting(bool mustPrepared, float flaskMl) => mustPrepared && flaskMl > 1f;
    /// Confirmed the moment the limewater has clouded (any CaCO₃ precipitate).
    public static bool CO2Confirmed(float limewaterPptMl) => limewaterPptMl > 0.5f;

    /// After this long fermenting with nothing clouding, nudge the player — they
    /// have likely forgotten to lead the delivery tube into a limewater tube.
    public const float NudgeAfterSeconds = 5f;

    /// One-time guidance when the flask is bubbling but no limewater is catching
    /// the CO₂ (edge case: the player sealed the flask but forgot the limewater).
    public static bool ShouldNudge(bool fermenting, bool confirmed, float secondsFermenting, bool alreadyNudged)
        => fermenting && !confirmed && !alreadyNudged && secondsFermenting >= NudgeAfterSeconds;
}

public class FermentationController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private LiquidPhysics flask;        // the fermentation flask (holds the must)
    [SerializeField] private string fermentTaskId;
    [SerializeField] private string mustTaskId = "prepare-must";
    [SerializeField] private ChemicalData co2;           // Chem_CarbonDioxide
    [SerializeField] private ChemicalData limewater;     // Chem_Limewater — only these vessels cloud
    private float _nextPush;
    private bool _subscribed;
    private bool _confirmed;   // a limewater vessel has actually clouded
    private float _fermentingSince = -1f;
    private bool _nudged;

    public bool Fermenting => flask != null && FermentationMath.IsFermenting(
        runner != null && runner.Graph != null && runner.Graph.IsComplete(mustTaskId), flask.currentLiquidVolume);

    public string FermentTaskId => fermentTaskId;
    public ChemicalData Limewater => limewater;

    /// SIM seam: the player has led the delivery tube from the flask into this
    /// limewater vessel — bubble CO₂ in directly (distance-independent). Returns
    /// false unless the flask is actually fermenting and the vessel holds limewater.
    public bool BubbleInto(LiquidPhysics tube)
    {
        if (tube == null || co2 == null || !Fermenting || tube.currentChemical != limewater) return false;
        tube.AddLiquid(co2, 1f, notify: false);   // reaction driver, not a graded reagent
        if (FermentationMath.CO2Confirmed(tube.currentPptVolume)) _confirmed = true;
        return true;
    }

    /// Builder/test seam.
    public void Bind(ExperimentRunner r, LiquidPhysics flaskLp, string fermentTask,
                     ChemicalData co2Chem, ChemicalData limewaterChem)
    {
        Unsubscribe();
        runner = r; flask = flaskLp; fermentTaskId = fermentTask; co2 = co2Chem; limewater = limewaterChem;
        Subscribe();
        Register();
    }

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted; _subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (runner != null) runner.ExperimentStarted -= OnStarted; _subscribed = false;
    }
    public void Detach() => Unsubscribe();
    private void OnDestroy() => Detach();
    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnStarted(ExperimentModuleDefinition _)
    { _confirmed = false; _nudged = false; _fermentingSince = -1f; Register(); }

    private void Register()
    {
        if (runner == null || runner.Graph == null || string.IsNullOrEmpty(fermentTaskId)) return;
        // Done once the limewater has actually clouded — tracked as a flag so it
        // works whether the player held the tube near the flask (proximity emit)
        // or the sim bubbled in directly. Re-checks live vessels too, so a real
        // in-headset cloud that happened off-frame still counts.
        runner.Graph.RegisterCondition(fermentTaskId, () => _confirmed || AnyLimewaterClouded());
    }

    private void Update()
    {
        if (!Application.isPlaying || runner == null || !runner.IsRunning || !Fermenting) return;

        // Guidance for the "forgot the limewater" edge case: if the flask has
        // been bubbling a while with nothing clouding, tell the player what to do.
        if (_fermentingSince < 0f) _fermentingSince = Time.time;
        if (FermentationMath.ShouldNudge(true, _confirmed, Time.time - _fermentingSince, _nudged))
        {
            _nudged = true;
            FloatingText.Show("CO₂ is bubbling — lead the delivery tube into a limewater tube to confirm it",
                              flask.transform.position + Vector3.up * 0.25f, new Color(0.7f, 0.9f, 1f), 1.1f);
        }

        if (Time.time < _nextPush) return;
        _nextPush = Time.time + 0.25f;
        EmitCO2();
    }

    /// CO₂ delivery reach: the hand-scaled "FermentZone" child on the flask
    /// when present (wire-sphere diameter == world scale),
    /// else the coded constant. The zone anchor survives on the bench flask.
    private Transform _fermentZone;
    public float DeliveryRadius
    {
        get
        {
            if (_fermentZone == null && flask != null) _fermentZone = flask.transform.Find("FermentZone");
            return WaterBathMath.EffectRadius(_fermentZone != null ? Mathf.Abs(_fermentZone.lossyScale.x) : 0f,
                                              FermentationMath.DeliveryRadius);
        }
    }

    /// Push a little CO₂ into every nearby limewater vessel — the registered
    /// Limewater_CO2 reaction turns it milky (CaCO₃). Public so the sim drives it.
    public void EmitCO2()
    {
        if (flask == null || co2 == null) return;
        foreach (var col in Physics.OverlapSphere(flask.transform.position, DeliveryRadius,
                                                  ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null && lp != flask && lp.currentChemical == limewater)
            {
                lp.AddLiquid(co2, 1f, notify: false);
                if (FermentationMath.CO2Confirmed(lp.currentPptVolume)) _confirmed = true;
            }
        }
    }

    private bool AnyLimewaterClouded()
    {
        if (flask == null) return false;
        foreach (var col in Physics.OverlapSphere(flask.transform.position, DeliveryRadius,
                                                  ~0, QueryTriggerInteraction.Ignore))
        {
            var lp = col != null ? col.GetComponentInParent<LiquidPhysics>() : null;
            if (lp != null && lp != flask && FermentationMath.CO2Confirmed(lp.currentPptVolume)) return true;
        }
        return false;
    }
}
