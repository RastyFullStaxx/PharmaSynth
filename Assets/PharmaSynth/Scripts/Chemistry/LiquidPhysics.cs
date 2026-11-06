using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Renderer))]
[RequireComponent(typeof(MeshFilter))]
public class LiquidPhysics : MonoBehaviour
{
    // Chemistry is reported as events; experiment logic (task completion, wrong-reagent
    // grading) lives in bindings that know the current context — not hardcoded here.
    public event Action<ChemicalData, float> LiquidAdded;          // (chemical, amount)
    public event Action<ChemicalData, float> LiquidRejected;       // overflow: add refused (vessel full)
    public event Action<ReactionRule> ReactionOccurred;            // a registered reaction fired
    public event Action<ChemicalData, ChemicalData> WrongReagentMixed; // (current, incoming) with no rule
    public event Action<ReactionRule> ReactionPending;             // right recipe, not hot enough yet
    public event Action Emptied;                                   // taken back to truly empty

    [Header("Components")]
    public Renderer mainRenderer;
    public Renderer precipitateRenderer;

    [Header("Volume Settings")]
    public float maxVolume = 1000f;
    // 0 by default: a freshly AddComponent'ed receiver must start EMPTY so
    // AddLiquid's wake-from-empty branch can adopt the first poured chemical.
    // (A non-zero default leaves stage-built vessels with a phantom half-fill that
    // blocks adoption forever, so pouring into them does nothing. Serialized
    // prefabs/scene objects keep their authored values.)
    public float currentLiquidVolume = 0f;
    public float currentPptVolume = 0f;
    public float HorizonalFloatAdj = 0.13f;

    [Header("Chemical Content")]
    public ChemicalData currentChemical;
    public ChemicalData currentPptChemical;
    public ReactionRegistry registry;

    // ---- temperature-gated reactions -------------------------------------------
    // A rule whose minTemperatureC isn't met at mix time does not fire early, or the
    // Tollens mirror would appear the instant you pour, cold, making the "warm it in
    // the water bath" step meaningless. The mix is HELD as pending and fires the
    // moment the vessel is actually heated to the rule's threshold (heat stations /
    // the water bath propagate their sim temperature to vessels in their zone).
    [Header("Temperature")]
    public float currentTempC = 25f;   // ambient; SetTemperature raises it

    private ReactionRule _pendingRule;
    private float _pendingAmount;

    public bool HasPendingReaction => _pendingRule != null;
    public ReactionRule PendingRule => _pendingRule;

    /// A heat source (bath / burner zone) reports its temperature into the
    /// vessel; a pending recipe fires as soon as its threshold is met.
    public void SetTemperature(float c)
    {
        currentTempC = c;
        if (_pendingRule != null && _pendingRule.TemperatureSatisfied(currentTempC))
        {
            var rule = _pendingRule; float amount = _pendingAmount;
            _pendingRule = null; _pendingAmount = 0f;
            ApplyReaction(rule, amount, alreadyAdded: true);
        }
    }

    /// Display-only story of what went in ("Ethanol 120 ml + NaOH 50 ml") for
    /// hover cards and mix feedback. Chemistry stays in the fields above.
    public VesselLedger Ledger { get; } = new VesselLedger();

    // MIXTURE pH, not the first-poured chemical's: reading currentChemical.pH makes
    // "water first, then the acid" read 7 forever and soft-locks Exp 4's litmus test.
    // The more extreme component dominates (LitmusMath.DominantPH): an acid stays
    // acidic under any amount of water, which is what a real strip in the mixture reports.
    private float _mixPH = 7f;
    public float CurrentPH => _mixPH;

    // Reaction-sting dedupe: see ApplyReaction.
    private ReactionRule _lastCueRule;
    private float _lastCueAt = -999f;

    /// Truly empty (nothing visible, wake branch armed).
    public bool IsEmpty => currentLiquidVolume <= 1f && currentPptVolume <= 1f;

    /// The last chemical this vessel actually held. A vessel poured DRY forgets its
    /// CONTENTS (so a refill starts a clean story) but a labelled source bottle still
    /// knows what it dispenses — the demo top-up restocks from this.
    public ChemicalData LastChemical { get; private set; }

    /// Builder/test seam: set the vessel's contents explicitly WITHOUT touching
    /// materials (edit-mode safe — visuals catch up in Update once playing).
    /// Blank contents = chem null + 0 ml arms the wake-from-empty branch.
    public void SetContents(ChemicalData chem, float ml)
    {
        ProbeDrain("SetContents(" + (chem != null ? chem.chemicalName : "null") + ", " + ml + ")");
        currentChemical = chem;
        if (chem != null) LastChemical = chem;
        currentLiquidVolume = chem != null ? Mathf.Max(0f, ml) : 0f;
        _pendingRule = null; _pendingAmount = 0f;   // a reset vessel holds no half-done recipe
        currentTempC = 25f;
        _mixPH = chem != null ? chem.pH : 7f;
        _wetted = chem != null && chem.state != PhysicalState.Solid && chem.state != PhysicalState.Powder;
        Ledger.Clear();
        if (chem != null && currentLiquidVolume > 0f)
            Ledger.Add(chem.chemicalName, currentLiquidVolume,
                       chem.state == PhysicalState.Solid || chem.state == PhysicalState.Powder);
    }

    [Header("Visual Smoothness")]
    public float colorChangeSpeed = 2.0f;

    private Coroutine liquidChangeRoutine;
    private Coroutine pptChangeRoutine;

    [Header("Wobble Settings")]
    public float MaxWobble = 0.03f;
    public float WobbleSpeed = 1f;
    public float Recovery = 1f;

    private const float MinMovementThreshold = 0.001f;

    // Internal variables
    private Mesh mesh;

    // Shader Property IDs
    private static readonly int FillID = Shader.PropertyToID("_Fill");
    private static readonly int LiquidColorID = Shader.PropertyToID("_LiquidColour");
    private static readonly int SceneColorAmtID = Shader.PropertyToID("_SceneColourAmount");
    private static readonly int UpVectorID = Shader.PropertyToID("_UpVector");
    private static readonly int LocalYMinID = Shader.PropertyToID("_LocalYMin");
    private static readonly int LocalYMaxID = Shader.PropertyToID("_LocalYMax");
    private static readonly int BoilID = Shader.PropertyToID("_Boil");
    private static readonly int WobbleXID = Shader.PropertyToID("_WobbleX");
    private static readonly int WobbleZID = Shader.PropertyToID("_WobbleZ");

    // Wobble Physics Variables
    private Vector3 lastPos;
    private Vector3 lastRot;
    private float wobbleAmountX;
    private float wobbleAmountZ;
    private float wobbleAmountToAddX;
    private float wobbleAmountToAddZ;
    private float pulse;
    private float time = 0.5f;

    // State
    private bool isWobbling = true; // Start active to settle initial state

    /// Pure (suite-pinned): may LiquidPhysics adopt its host's OWN renderer as the
    /// liquid fill surface? Only when that material runs the PharmaLiquid "_Fill"
    /// shader. An opaque VESSEL mesh must NOT be adopted: UpdateFillPhysics disables
    /// mainRenderer while empty, so a mortar/beaker lending its own mesh would vanish
    /// in Play the moment it was empty.
    public static bool ShouldAdoptHostRenderer(Material hostMaterial)
        => hostMaterial != null && hostMaterial.HasProperty("_Fill");

    void Start()
    {
        // A mainRenderer pointing at an OPAQUE surface (the "Powder" mound, a glass
        // shell) is worse than none, and every consumer below trusts it: LerpColor
        // READS _LiquidColour/_SceneColourAmount off it (URP/Lit has neither → a
        // console error every Start), and UpdateFillPhysics does
        // `mainRenderer.enabled = hasLiquid` — so a powder-only vessel HID its own
        // mound the moment it held 0 ml. EnsureLiquidVisual's powder branch returns
        // early and deliberately leaves this field alone, so a vessel that once held
        // liquid can arrive here still pointing at the wrong surface. Drop it, then
        // let the adopt below have its say.
        if (mainRenderer != null && !ShouldAdoptHostRenderer(mainRenderer.sharedMaterial))
            mainRenderer = null;

        if (mainRenderer == null)
        {
            var host = GetComponent<Renderer>();
            if (host != null && ShouldAdoptHostRenderer(host.sharedMaterial)) mainRenderer = host;
            // else: leave null → the fill visual is a no-op; the solid mound is a
            // separate "Powder" child, and the vessel's own mesh is never disabled.
        }
        var mf = GetComponent<MeshFilter>();
        mesh = mf != null ? mf.mesh : null;

        SendMeshBounds();
        UpdateAllVisuals();

        lastPos = transform.position;
        lastRot = transform.rotation.eulerAngles;
    }

    void SendMeshBounds()
    {
        // Prefer the liquid renderer's own mesh bounds (the _WithLiquid prefabs keep the
        // liquid volume on a child mesh; the root mesh is the glass shell).
        Mesh boundsMesh = mesh;
        if (mainRenderer != null)
        {
            var mrFilter = mainRenderer.GetComponent<MeshFilter>();
            if (mrFilter != null && mrFilter.sharedMesh != null) boundsMesh = mrFilter.sharedMesh;
        }
        if (boundsMesh == null) return;
        Bounds bounds = boundsMesh.bounds;

        if (mainRenderer != null)
        {
            mainRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            mainRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(LocalYMinID, bounds.min.y);
            precipitateRenderer.material.SetFloat(LocalYMaxID, bounds.max.y);
        }
    }

    void Update()
    {
        // 1. Clamp Volumes
        currentLiquidVolume = Mathf.Clamp(currentLiquidVolume, 0, maxVolume);
        currentPptVolume = Mathf.Clamp(currentPptVolume, 0, maxVolume - currentLiquidVolume);

        // 2. Calculate Fill & Tilt (Must run every frame for rotation accuracy)
        UpdateFillPhysics();

        // 3. WOBBLE PHYSICS
        // First, calculate movement speed
        Vector3 currentPos = transform.position;
        Vector3 currentRot = transform.rotation.eulerAngles;

        Vector3 velocity = (lastPos - currentPos) / Time.deltaTime;
        Vector3 angularVelocity = currentRot - lastRot;

        // Check if we are moving enough to matter (using sqrMagnitude is faster)
        bool isMoving = velocity.sqrMagnitude > MinMovementThreshold || angularVelocity.sqrMagnitude > MinMovementThreshold;

        // Check if we still have leftover wobble energy
        bool hasWobbleEnergy = Mathf.Abs(wobbleAmountToAddX) > MinMovementThreshold || Mathf.Abs(wobbleAmountToAddZ) > MinMovementThreshold;

        if (isMoving || hasWobbleEnergy || isWobbling)
        {
            UpdateWobble(velocity, angularVelocity);

            if (!isMoving && !hasWobbleEnergy)
            {
                isWobbling = false;
                // Force Zero one last time to ensure it looks perfect
                if (mainRenderer)
                {
                    mainRenderer.material.SetFloat(WobbleXID, 0);
                    mainRenderer.material.SetFloat(WobbleZID, 0);
                }
            }
            else
            {
                isWobbling = true;
            }
        }

        // Update history for next frame
        lastPos = currentPos;
        lastRot = currentRot;
    }

    /// The smallest fraction of a vessel a non-empty column is allowed to READ as.
    ///
    /// The manuscript regularly asks for a couple of millilitres in a big flask — Exp 5
    /// puts 2 ml of aniline in a 250 ml Florence flask — which is 0.8% of its height and
    /// simply invisible at arm's length in VR. The quantity stays honest everywhere it is
    /// judged (VesselStatus, the watch panel, every binding); only the DRAWN column is
    /// floored, so "there is something in this flask" is legible.
    public const float MinVisibleFill01 = 0.06f;

    /// Volume below which a column counts as nothing at all.
    public const float ShowFromMl = 0.05f;

    /// Pure (suite-pinned): the fraction the shader should draw for `ml` in this vessel.
    public static float DisplayFill01(float ml, float maxVolume)
    {
        if (maxVolume <= 0f || ml <= ShowFromMl) return 0f;
        return Mathf.Max(ml / maxVolume, MinVisibleFill01);
    }

    /// Has any LIQUID gone into this vessel since it was last emptied? A jar of dry powder
    /// shows a mound and no liquid column; the moment something is poured on it — water,
    /// acid — it is a wet mixture and must draw as one, whatever the resulting chemical's
    /// authored state says. Exp 2 boils aspirin in acid and the product, Salicylic Acid, is
    /// authored Solid, so 13.5 ml in the tube would otherwise draw nothing at all.
    private bool _wetted;

    /// Pure (suite-pinned): are these contents a DRY solid rather than a wet mixture?
    public static bool ShowsAsDryPowder(PhysicalState state, bool wetted)
        => !wetted && (state == PhysicalState.Solid || state == PhysicalState.Powder);

    public bool DryPowder => currentChemical != null
                             && ShowsAsDryPowder(currentChemical.state, _wetted);

    /// Pure (suite-pinned): should the liquid column be drawn at all?
    ///
    /// ⛔ The mound only REPLACES the column when a mound actually exists. A dry solid can
    /// arrive by pour as well as by scoop — the filter and drying steps decant the product
    /// onto a watch glass, and nothing builds a mound there — so suppressing the column on
    /// "this is a solid" alone made five finished products invisible in their own glass.
    /// A vessel must never end up drawing nothing while it holds something.
    public static bool DrawsLiquidColumn(float ml, bool dryPowder, bool hasMound)
        => ml > ShowFromMl && !(dryPowder && hasMound);

    private Renderer _mound;

    /// Is a powder mound actually being drawn in this vessel right now?
    private bool HasMound
    {
        get
        {
            if (_mound == null)
            {
                var t = transform.Find("Powder");
                _mound = t != null ? t.GetComponent<Renderer>() : null;
            }
            return _mound != null && _mound.enabled && _mound.gameObject.activeInHierarchy;
        }
    }

    void UpdateFillPhysics()
    {
        float liquidFill = DisplayFill01(currentLiquidVolume, maxVolume);
        float pptFill = DisplayFill01(currentPptVolume, maxVolume);

        // Tilt Correction
        float tilt = Mathf.Abs(Vector3.Dot(transform.up, Vector3.up));
        float correction = Mathf.Lerp(HorizonalFloatAdj, 1.0f, tilt);

        // Apply to Shader
        if (mainRenderer) mainRenderer.material.SetFloat(FillID, liquidFill * correction);
        if (precipitateRenderer) precipitateRenderer.material.SetFloat(FillID, pptFill * correction);

        // Cutoff Logic (Hide if empty). A DRY powder draws its mound instead of a liquid
        // surface; once anything has been poured on it, it is a mixture and draws like one.
        if (mainRenderer)
        {
            bool hasLiquid = DrawsLiquidColumn(currentLiquidVolume, DryPowder, HasMound);
            if (mainRenderer.enabled != hasLiquid) mainRenderer.enabled = hasLiquid;
        }

        if (precipitateRenderer)
        {
            // ⛔ `>` ShowFromMl, not `> 1f`. Every precipitate rule deposits exactly the incoming
            // pour (one dropper squeeze, 1.0 ml), and 1.0 is not greater than 1.0, so the milky
            // limewater, both iodoform yellows, the acetanilide plates and the benzamide solid
            // would never be drawn.
            bool hasPpt = currentPptVolume > ShowFromMl;
            if (precipitateRenderer.enabled != hasPpt) precipitateRenderer.enabled = hasPpt;
        }

        Vector3 localUp = transform.InverseTransformDirection(Vector3.up);
        if (mainRenderer) mainRenderer.material.SetVector(UpVectorID, localUp);
        if (precipitateRenderer) precipitateRenderer.material.SetVector(UpVectorID, localUp);

        if (mainRenderer) mainRenderer.material.SetFloat(BoilID, BoilAmount());
    }

    /// How hard this vessel's contents are boiling, 0-1.
    ///
    /// Driven by `boilingPointC`, so the liquid itself moves when heated instead of only
    /// the station venting steam. It ramps over the last 8 degrees rather than switching on at the threshold, so a
    /// vessel coming up to temperature visibly starts to move before it runs.
    public float BoilAmount()
        => BoilFor(currentLiquidVolume, currentTempC,
                   currentChemical != null ? currentChemical.boilingPointC : 100f);

    /// Pure so the suite can pin it without a Renderer host (thin-MonoBehaviour-over-pure-core).
    public static float BoilFor(float volumeMl, float tempC, float boilingPointC)
    {
        if (volumeMl <= 1f) return 0f;                       // an empty vessel never boils
        return Mathf.Clamp01((tempC - (boilingPointC - RampC)) / RampC);
    }

    /// Degrees below the boiling point at which the churn starts to show.
    public const float RampC = 8f;

    void UpdateWobble(Vector3 velocity, Vector3 angularVelocity)
    {
        if (mainRenderer == null || !mainRenderer.enabled) return;

        time += Time.deltaTime;

        // Decay
        wobbleAmountToAddX = Mathf.Lerp(wobbleAmountToAddX, 0, Time.deltaTime * Recovery);
        wobbleAmountToAddZ = Mathf.Lerp(wobbleAmountToAddZ, 0, Time.deltaTime * Recovery);

        // Oscillate
        pulse = 2 * Mathf.PI * WobbleSpeed;
        wobbleAmountX = wobbleAmountToAddX * Mathf.Sin(pulse * time);
        wobbleAmountZ = wobbleAmountToAddZ * Mathf.Sin(pulse * time);

        // Add Velocity Impact
        wobbleAmountToAddX += Mathf.Clamp((velocity.x + (angularVelocity.z * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);
        wobbleAmountToAddZ += Mathf.Clamp((velocity.z + (angularVelocity.x * 0.2f)) * MaxWobble, -MaxWobble, MaxWobble);

        // Send to Shader
        mainRenderer.material.SetFloat(WobbleXID, wobbleAmountX);
        mainRenderer.material.SetFloat(WobbleZID, wobbleAmountZ);

        if (precipitateRenderer != null)
        {
            precipitateRenderer.material.SetFloat(WobbleXID, 0);
            precipitateRenderer.material.SetFloat(WobbleZID, 0);
        }
    }

    /// notify=false: run the chemistry (reactions, precipitate, ledger) WITHOUT
    /// raising LiquidAdded/WrongReagentMixed — for a REACTION DRIVER that is not a
    /// procedure reagent (Exp 3's CO₂ bubbled into limewater). Without this the task
    /// binding grades every CO₂ bubble a "wrong reagent" while the Limewater_CO2 reaction clouds the tube.
    public void AddLiquid(ChemicalData incomingChemical, float amountToAdd, bool notify = true)
    {
        if (incomingChemical == null)
            return;

        // Capacity guard FIRST: a rejected overflow must not raise LiquidAdded, or a pour
        // into an already-full vessel completes tasks.
        if (currentLiquidVolume + currentPptVolume + amountToAdd > maxVolume)
        {
            if (notify) LiquidRejected?.Invoke(incomingChemical, amountToAdd);
            return;
        }

        if (notify) LiquidAdded?.Invoke(incomingChemical, amountToAdd);
        LastChemical = incomingChemical;
        bool solid = incomingChemical.state == PhysicalState.Solid || incomingChemical.state == PhysicalState.Powder;

        // If waking up from empty, ensure visuals update. The ledger is CLEARED first and
        // only then given the incoming pour; recording before the wake check would carry a
        // spilled vessel's old story into the refill, naming reagents no longer in it.
        // ⛔ `currentChemical == null` MUST wake too, even with precipitate still in the
        // glass. PourOut drops the identity once the liquid runs dry but leaves the settled
        // precipitate behind, so a tube emptied after a test holds residue and NO chemical.
        // Guarding on "both columns near-empty" alone refuses to adopt anything poured in
        // next; the liquid piles up with no identity and `FindReaction(null, x)` misses
        // forever: no reaction, no colour, no product (Exp 7 carries 5 ml of residue into
        // its flask, so its chloroform reaction would never fire).
        if (!solid) _wetted = true;     // anything poured onto a powder makes it a mixture
        if (currentChemical == null || (currentLiquidVolume <= 0.1f && currentPptVolume <= 0.1f))
        {
            Ledger.Clear();
            Ledger.Add(incomingChemical.chemicalName, amountToAdd, solid);
            currentChemical = incomingChemical;
            currentLiquidVolume += amountToAdd;
            _mixPH = incomingChemical.pH;
            UpdateAllVisuals();
            return;
        }
        Ledger.Add(incomingChemical.chemicalName, amountToAdd, solid);
        _mixPH = LitmusMath.DominantPH(_mixPH, incomingChemical.pH);

        if (currentChemical == incomingChemical)
        {
            currentLiquidVolume += amountToAdd;
            return;
        }

        if (registry != null)
        {
            ReactionRule rule = registry.FindReaction(currentChemical, incomingChemical);

            if (rule != null)
            {
                if (!rule.TemperatureSatisfied(currentTempC))
                {
                    // The RIGHT recipe, not hot enough yet: hold it pending (no
                    // early observation, no wrong-mix scold) until a heat source
                    // brings the vessel to the rule's threshold.
                    currentLiquidVolume += amountToAdd;
                    _pendingRule = rule; _pendingAmount = amountToAdd;
                    ReactionPending?.Invoke(rule);
                }
                else
                    ApplyReaction(rule, amountToAdd, alreadyAdded: false);
            }
            else
            {
                currentLiquidVolume += amountToAdd;
                // No registered reaction: report the mix so a context-aware binding can
                // decide whether it is actually "wrong" for the current step.
                if (notify && currentChemical != null && incomingChemical != null && currentChemical != incomingChemical)
                    WrongReagentMixed?.Invoke(currentChemical, incomingChemical);
            }
        }
    }

    /// The reaction body, shared by the immediate path and a pending fire.
    /// alreadyAdded: a pending mix already put the incoming amount into the
    /// liquid column; a precipitate result moves it over instead of doubling it.
    private void ApplyReaction(ReactionRule rule, float amount, bool alreadyAdded)
    {
        // A FIRED reaction supersedes any half-armed pending recipe — the ledger
        // collapses to the product, so the pending pair no longer exists in the
        // vessel (Exp 8's nitrous tube: the heat-gated acid-hydrolysis pend must
        // not outlive the instant nitrite effervescence and keep begging for heat).
        _pendingRule = null; _pendingAmount = 0f;
        if (rule.resultLiquid != null) { currentChemical = rule.resultLiquid; _mixPH = rule.resultLiquid.pH; }
        if (rule.hasPrecipitate && rule.resultPrecipitate != null)
        {
            currentPptChemical = rule.resultPrecipitate;
            if (alreadyAdded) currentLiquidVolume = Mathf.Max(0f, currentLiquidVolume - amount);
            currentPptVolume += amount;
        }
        else if (!alreadyAdded)
        {
            currentLiquidVolume += amount;
        }
        UpdateAllVisuals(); // Update Color only on reaction
        Ledger.React(currentChemical != null ? currentChemical.chemicalName : null);
        // The same rule re-firing within a burst (each of 5 acid squeezes) plays
        // ONE sting, not five — mirrors MixFeedback's observation dedupe.
        string cue = Mishandling.SfxForOutcome(rule.outcome);
        bool freshCue = rule != _lastCueRule || Time.time - _lastCueAt >= 4f;
        if (cue.Length > 0 && freshCue)
            AudioService.TryPlay(cue);
        // Gas had NO visual consumer at all: `evolvesGas` and the Fizzing / GasEvolved
        // outcomes drove a sound and a line of text and nothing else, so brisk
        // effervescence, the CO2 of both fermentations and the ammonia boil were all
        // invisible. Same dedupe as the sting, so a burst of squeezes fizzes once.
        if (freshCue && EvolvesGas(rule)) EffectVfx.Fizz(GasVentPos());
        _lastCueRule = rule; _lastCueAt = Time.time;
        ReactionOccurred?.Invoke(rule);
    }

    /// Pure (suite-pinned): does this reaction put gas into the room? Either the rule says
    /// so outright, or its outcome IS the gas.
    public static bool EvolvesGas(ReactionRule rule)
        => rule != null && (rule.evolvesGas
                            || rule.outcome == ReactionOutcome.Fizzing
                            || rule.outcome == ReactionOutcome.GasEvolved);

    /// Where bubbles leave the liquid: the top of the vessel's own body.
    private Vector3 GasVentPos()
    {
        var r = mainRenderer != null ? mainRenderer : GetComponent<Renderer>();
        return r != null ? new Vector3(r.bounds.center.x, r.bounds.max.y, r.bounds.center.z)
                         : transform.position + Vector3.up * 0.05f;
    }

    public void UpdateAllVisuals()
    {
        // Play-mode only: LerpColor instantiates renderer.material, which leaks
        // (and errors) in edit mode — the suite drives AddLiquid on vessels that
        // now carry REAL liquid renderers, so guard here, not per-caller.
        if (!Application.isPlaying) return;

        // 1. Handle Main Liquid Transition
        if (currentChemical != null && mainRenderer != null)
        {
            // Stop any old transition so they don't fight
            if (liquidChangeRoutine != null) StopCoroutine(liquidChangeRoutine);

            // Start the new smooth transition
            liquidChangeRoutine = StartCoroutine(LerpColor(
                mainRenderer,
                currentChemical.liquidColor,
                currentChemical.sceneColourAmount
            ));
        }

        // 2. Handle Precipitate Transition
        if (currentPptChemical != null && precipitateRenderer != null)
        {
            if (pptChangeRoutine != null) StopCoroutine(pptChangeRoutine);

            pptChangeRoutine = StartCoroutine(LerpColor(
                precipitateRenderer,
                currentPptChemical.liquidColor,
                currentPptChemical.sceneColourAmount
            ));
        }
    }

    // The Worker Function: smoothly changes color over time
    System.Collections.IEnumerator LerpColor(Renderer targetRenderer, Color targetColor, float targetSceneAmt)
    {
        // Get starting values from the material currently
        Color startColor = targetRenderer.material.GetColor(LiquidColorID);
        float startSceneAmt = targetRenderer.material.GetFloat(SceneColorAmtID);
        float t = 0;

        // Loop until t reaches 1 (100% complete)
        while (t < 1f)
        {
            t += Time.deltaTime * colorChangeSpeed;

            // Calculate intermediate values
            Color newColor = Color.Lerp(startColor, targetColor, t);
            float newAmt = Mathf.Lerp(startSceneAmt, targetSceneAmt, t);

            // Apply to shader
            targetRenderer.material.SetColor(LiquidColorID, newColor);
            targetRenderer.material.SetFloat(SceneColorAmtID, newAmt);

            yield return null; // Wait for next frame
        }
    }

    /// Volume below which a vessel counts as poured DRY and forgets its contents.
    /// Above VesselStatusMath's 1 ml "empty" threshold would leave the tag saying
    /// "empty" while the ledger still held a story; below it, a rounding crumb
    /// would keep a phantom chemical alive forever.
    private const float DryMl = 0.5f;

    /// ⛔ "Who emptied this vessel?" — set to a name prefix and any vessel matching it logs a
    /// STACK TRACE whenever its contents are cleared. Editor-only. A vessel losing its charge
    /// between two steps is otherwise undebuggable: every reader sees the aftermath and none
    /// sees the caller.
    public static string DrainProbeName;

    private bool _probed;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    private void ProbeDrain(string how)
    {
        if (string.IsNullOrEmpty(DrainProbeName) || name == null || !name.StartsWith(DrainProbeName)) return;
        // ONCE per vessel. A self-pouring vessel clears itself every frame, and
        // Environment.StackTrace is expensive enough that logging each one turns the
        // diagnostic into the performance problem it is meant to find.
        if (_probed) return;
        _probed = true;
        Debug.LogWarning("[DrainProbe] " + name + " cleared via " + how + "\n" + System.Environment.StackTrace);
    }

    /// Pour/scoop the vessel out. The ledger follows the liquid: a PARTIAL pour
    /// shrinks every entry proportionally, and a vessel taken to dry forgets what
    /// it held entirely — so spilling a tube and refilling it starts a clean story
    /// instead of stacking the old amounts onto the new ones.
    public ChemicalData PourOut(float amountToRemove)
    {
        if (currentLiquidVolume <= 0) return null;

        float before = currentLiquidVolume;
        currentLiquidVolume -= amountToRemove;
        if (currentLiquidVolume < 0) currentLiquidVolume = 0;

        var poured = currentChemical;
        if (currentLiquidVolume <= DryMl && currentPptVolume <= DryMl) ClearContents();
        else if (before > 0.001f) Ledger.Scale(currentLiquidVolume / before);
        return poured;
    }

    /// Take the vessel back to truly empty: no chemical, no story, no half-armed
    /// recipe, neutral pH. The wake-from-empty branch in AddLiquid can then adopt
    /// the next pour cleanly. (SetContents(null, 0) does the same for builders.)
    public void ClearContents()
    {
        ProbeDrain("ClearContents");
        currentLiquidVolume = 0f;
        currentPptVolume = 0f;
        currentChemical = null;
        currentPptChemical = null;
        _pendingRule = null; _pendingAmount = 0f;
        _mixPH = 7f;
        Ledger.Clear();
        // A rinsed vessel is a free vessel: LiquidTaskBinding listens and gives its claimed
        // role back, so a wrong pour is recoverable instead of dead for the run.
        Emptied?.Invoke();
    }
}
