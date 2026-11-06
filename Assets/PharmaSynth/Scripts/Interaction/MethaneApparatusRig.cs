using UnityEngine;

/// Methane (Experiment 1) completion — LOCATION-FREE (the
/// steps can be done anywhere in the lab). Instead of
/// gating heat/collect on FIXED trigger zones, it detects the ACTIONS
/// by item proximity, so the tutorial works wherever the player does it:
///   prepare-mixture : grinding a mortar (the rig aims the workspace mortar at it)
///   setup-apparatus : the collection tube brought up to the hard-glass tube
///   heat-mixture    : a LIT burner held near the hard-glass tube (it heats)
///   collect-gas     : the hot tube + collection tube held together (gas fills)
///   test-gas        : a LIT match brought to the FILLED collection tube (pop)
/// The rig owns its own TemperatureSim + GasCollection (no station objects).
/// Items are found at runtime by LabItem.itemId, so the player can use their own
/// workspace burner/tube/mortar anywhere.
public class MethaneApparatusRig : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private TemperatureSim temperature;
    [SerializeField] private GasCollection gas;

    [Header("Tuning")]
    [SerializeField] private float burnerSourceC = 220f;
    [SerializeField] private float heatDoneC = 120f;
    [SerializeField] private float gasMlPerSecond = 22f;      // ~5 s to fill once hot
    [SerializeField, Range(0.1f, 1f)] private float collectedFraction = 0.9f;
    [SerializeField] private float heatDistance = 0.35f;     // lit burner within this of the tube heats it
    [SerializeField] private float collectDistance = 0.30f;  // collection tube within this of the tube collects

    private const string ModuleId = "tutorial-methane";
    private bool _active, _prevHeating, _splintFired;
    /// Run-local clock, advanced by Step's dt. Replaces Time.time so the rig can be
    /// stepped by the editor simulator; only ever read as a DURATION since collection.
    private float _clock;
    private float _collectedAt = -1f;
    public const float SplintMatchDistance = 0.25f;
    public const float SplintAutoSeconds = 20f;

    private Transform _tube, _collect;
    private GrindController _mortar;
    private float _nextBubbleAt;        // throttles the collecting bubble stream
    private GameObject _gasColumn;      // pale gas level inside the collection tube
    private Transform _glowTube;        // tube whose renderers are cached below
    private Renderer[] _tubeRends;      // cached for the red-hot emissive glow

    /// Pure: the splint test fires when a LIT match reaches the filled
    /// tube — or automatically after a grace period so nothing ever stalls.
    public static bool SplintShouldFire(bool collected, bool alreadyFired, float matchDistance, bool matchLit, float sinceCollected)
        => collected && !alreadyFired
           && ((matchLit && matchDistance <= SplintMatchDistance) || sinceCollected >= SplintAutoSeconds);

    /// Pure proximity check (suite-pinned).
    public static bool WithinReach(float distance, float reach) => distance <= reach;

    /// Edit-mode/test binding (OnEnable doesn't fire on AddComponent in edit mode).
    /// Zones are gone — the rig only needs the runner + its own sims.
    public void Bind(ExperimentRunner r, TemperatureSim t, GasCollection g)
    {
        runner = r; temperature = t; gas = g;
        EnsureSims();
        Resubscribe();
        TryRegisterIfRunning();
    }

    private void OnEnable() { EnsureSims(); Resubscribe(); TryRegisterIfRunning(); }

    private void OnDisable()
    {
        if (runner != null) runner.ExperimentStarted -= HandleExperimentStarted;
        ReleaseMortar();
    }

    private void EnsureSims()
    {
        if (temperature == null) temperature = GetComponent<TemperatureSim>() ?? gameObject.AddComponent<TemperatureSim>();
        if (gas == null) gas = GetComponent<GasCollection>() ?? gameObject.AddComponent<GasCollection>();
    }

    private void Resubscribe()
    {
        if (runner == null) return;
        runner.ExperimentStarted -= HandleExperimentStarted;
        runner.ExperimentStarted += HandleExperimentStarted;
    }

    /// If the rig came alive AFTER methane already started (the stage is shown a
    /// frame later than ExperimentStarted), register now so nothing is missed.
    private void TryRegisterIfRunning()
    {
        if (runner != null && runner.IsRunning && runner.Module != null && runner.Module.moduleId == ModuleId)
            HandleExperimentStarted(runner.Module);
    }

    /// Registers the location-free completion conditions for a fresh attempt.
    public void HandleExperimentStarted(ExperimentModuleDefinition module)
    {
        bool wasActive = _active;
        _active = module != null && module.moduleId == ModuleId && runner != null && runner.Graph != null;
        _prevHeating = false; _splintFired = false; _collectedAt = -1f; _clock = 0f;
        if (!_active) { if (wasActive) ReleaseMortar(); return; }

        EnsureSims();
        temperature.ResetSim();
        gas.ResetCollection();
        _tube = _collect = null;
        if (_gasColumn != null) { Destroy(_gasColumn); _gasColumn = null; }   // fresh attempt = empty tube
        _glowTube = null; _tubeRends = null;                                  // re-cache + glow back to cold

        // prepare-mixture: aim the nearest workspace mortar's grind at this step.
        AcquireMortar();

        // setup-apparatus: the ground mixture must be LOADED into the hard-glass
        // tube (scoop the mixture into the tube first, then
        // heat it). Completes when the tube has received the solid.
        runner.Graph.RegisterCondition("setup-apparatus", TubeLoaded);
        runner.Graph.RegisterCondition("heat-mixture", () => temperature != null && temperature.AtLeast(heatDoneC));
        runner.Graph.RegisterCondition("collect-gas", () => gas != null && gas.Collected(collectedFraction));
        runner.Graph.RegisterCondition("test-gas", () => _splintFired);
    }

    /// Tutorial Mode target map — which prop each of this rig's steps is ABOUT.
    ///
    /// It lives here, immediately beneath RegisterConditions, because this rig is the
    /// only place these four task ids are written as literals: the methane tutorial
    /// stages no dynamic vessels, so the generic sweep finds nothing for it. Keeping
    /// the map adjacent to the conditions means the ids are read off the same block a
    /// maintainer edits — anywhere else and the two would silently drift.
    ///
    /// Called BY TutorialTargets.Build() rather than self-registering, so the registry
    /// keeps exactly one lifetime (rebuilt per run, cleared on teardown).
    public void RegisterTutorialTargets()
    {
        // Resolved independently of FindItems()/_tube/_collect: those skip INACTIVE
        // objects and cache into runtime state. The methane props are visibility-gated
        // (MethaneStageVisibility), so while another module's stage stands they are
        // switched off — and an audit that silently found nothing would read exactly
        // like a module with no targets.
        Transform tube = FindByItemId("glass-tube");
        Transform collect = FindByItemId("collection-tube");
        Transform burner = FindByItemId("burner");
        var match = FindObjectsByType<Matchstick>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        // prepare-mixture lives on a mortar that only CARRIES the taskId while a
        // methane run is live — AcquireMortar stamps it on ExperimentStarted and
        // ReleaseMortar nulls it again. So the generic GrindController sweep finds
        // nothing whenever the map is built outside a run, and the very first step
        // of the tutorial would have no target. Resolve it by the same "first grind
        // verb" rule AcquireMortar uses, independent of run state.
        foreach (var g in FindObjectsByType<GrindController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (g == null) continue;
            Add("prepare-mixture", g.transform, TargetRole.Station, true);
            break;
        }

        // Scoop the ground mixture into the hard-glass tube.
        Add("setup-apparatus", tube, TargetRole.Destination, true);
        // Hold a LIT burner to that tube — fetch the burner, heat the tube.
        Add("heat-mixture", burner, TargetRole.Source, false);
        Add("heat-mixture", tube, TargetRole.Station, true);
        // Bring the collection tube up to the hot tube; the gas fills it.
        Add("collect-gas", collect, TargetRole.Destination, true);
        Add("collect-gas", tube, TargetRole.Station, true);
        // A lit match to the FILLED collection tube — the pop.
        Add("test-gas", collect, TargetRole.Destination, true);
        // ⚠ NOT simply match[0]. The consumable boxes keep a hidden, DISABLED
        // "Template_*" matchstick that ConsumableDispenser clones from, and an
        // inactive-inclusive search finds it first, which would point Tutorial Mode's
        // test-gas glow and waypoint at an object that can never be seen or picked up.
        // Prefer a real, live match.
        Transform pick = null;
        foreach (var m in match)
        {
            if (m == null || m.name.StartsWith("Template_")) continue;
            if (pick == null) pick = m.transform;
            if (m.gameObject.activeInHierarchy) { pick = m.transform; break; }
        }
        if (pick != null) Add("test-gas", pick, TargetRole.Source, false);

        void Add(string taskId, Transform t, TargetRole role, bool stayLit)
            => TaskTargetRegistry.Register(taskId, t, role, stayLit);
    }

    private Transform FindByItemId(string itemId)
    {
        foreach (var li in FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (li != null && li.itemId == itemId) return li.transform;
        return null;
    }

    // ---- item lookup -------------------------------------------------------

    private void FindItems()
    {
        if (_tube != null && _collect != null) return;
        foreach (var li in FindObjectsByType<LabItem>(FindObjectsSortMode.None))
        {
            if (li == null) continue;
            if (_tube == null && li.itemId == "glass-tube") _tube = li.transform;
            else if (_collect == null && li.itemId == "collection-tube") _collect = li.transform;
        }
    }

    private void AcquireMortar()
    {
        // The first mortar with a grind verb becomes the "prepare-mixture" mortar.
        foreach (var g in FindObjectsByType<GrindController>(FindObjectsSortMode.None))
        {
            if (g == null) continue;
            _mortar = g;
            _mortar.BindRunner(runner);          // _runner isn't serialized — supply it now
            _mortar.SetTaskId("prepare-mixture"); // registers the completion condition
            return;
        }
    }

    private void ReleaseMortar()
    {
        if (_mortar != null) { _mortar.SetTaskId(null); _mortar = null; }
    }

    /// The hard-glass tube has been loaded with the ground mixture (scoop from the
    /// mortar into the tube). Any solid the player deposits counts.
    private bool TubeLoaded()
    {
        FindItems();
        if (_tube == null) return false;
        var lp = _tube.GetComponent<LiquidPhysics>();
        return lp != null && lp.currentLiquidVolume > 0.5f;
    }

    // ---- per-frame action detection ----------------------------------------

    private void Update() => Step(Time.deltaTime);

    /// One frame of the rig, with the timestep injected.
    ///
    /// ⭐ Public so the editor simulator can play the methane tutorial through the REAL
    /// logic instead of a copy of it. Without this no simulator could cover it,
    /// because everything here is driven by Update and Time — neither of which
    /// exists in edit mode. Extracting the step rather than duplicating it means the sim
    /// cannot pass against a stale reimplementation of what the game actually does.
    ///
    /// No Time.time: the only thing it would measure is the DURATION
    /// the gas has been collected, so an internal clock advanced by dt is
    /// and drivable. In play mode Update feeds it Time.deltaTime.
    public void Step(float dt)
    {
        if (!_active || runner == null || !runner.IsRunning) return;
        _clock += Mathf.Max(0f, dt);
        FindItems();
        if (_tube == null) return;

        // Heat: any LIT burner within reach of the hard-glass tube — anywhere.
        bool heating = AnyLitBurnerNear(_tube.position);
        if (temperature != null) temperature.SetHeating(heating, burnerSourceC);
        if (heating && !_prevHeating) EffectVfx.FlamePop(_tube.position + Vector3.up * 0.08f);
        _prevHeating = heating;

        // The tube glows red-hot as it nears/holds the reaction temperature.
        UpdateTubeGlow();

        // Live instrument readouts over the two tubes (temperature + collection %).
        EnsureReadouts();

        // Collect: hot apparatus + collection tube held at the tube → gas fills.
        bool hot = temperature != null && temperature.AtLeast(heatDoneC);
        bool collecting = hot && gas != null && _collect != null
                          && WithinReach(Vector3.Distance(_tube.position, _collect.position), collectDistance);
        if (collecting)
        {
            gas.AddGas(gasMlPerSecond * dt);
            CollectAnimation();
        }

        // Splint: LIT match brought to the FILLED collection tube → pop.
        bool collected = gas != null && gas.Collected(collectedFraction);
        if (collected && _collectedAt < 0f) _collectedAt = _clock;
        if (!collected) _collectedAt = -1f;
        if (!_splintFired && collected && _collect != null)
        {
            float best = float.MaxValue; bool anyLit = false;
            foreach (var m in FindObjectsByType<Matchstick>(FindObjectsSortMode.None))
            {
                if (m == null || !m.IsLit) continue;
                anyLit = true;
                best = Mathf.Min(best, Vector3.Distance(m.transform.position, _collect.position));
            }
            if (SplintShouldFire(true, _splintFired, anyLit ? best : float.MaxValue, anyLit, _clock - _collectedAt))
            {
                EffectVfx.FlamePop(_collect.position + Vector3.up * 0.1f);
                FloatingText.Show("Pop! Methane confirmed", _collect.position + Vector3.up * 0.2f, new Color(0.7f, 1f, 0.7f));
                _splintFired = true;
            }
        }
    }

    // ---- glow + readouts + collecting animation -----------------------------

    /// Pure (suite): how red-hot the tube looks — ramps in over the last stretch of
    /// the climb and saturates at the reaction temperature:
    /// a reddish glow signals it is at the correct boiling temperature.
    public static float GlowFor(float currentC, float targetC)
        => Mathf.Clamp01(Mathf.InverseLerp(targetC * 0.6f, targetC, currentC));

    /// Drive an emissive red glow on the hard-glass tube's own meshes.
    private void UpdateTubeGlow()
    {
        if (!Application.isPlaying || _tube == null || temperature == null) return;
        if (_glowTube != _tube)
        {
            _glowTube = _tube;
            var list = new System.Collections.Generic.List<Renderer>();
            foreach (var r in _tube.GetComponentsInChildren<Renderer>())
            {
                if (r == null || !(r is MeshRenderer)) continue;
                if (r.GetComponent<TMPro.TMP_Text>() != null) continue;
                if (r.gameObject.name == "Powder" || r.gameObject.name == "CollectedGas") continue;
                list.Add(r);
            }
            _tubeRends = list.ToArray();
        }
        float t = GlowFor(temperature.CurrentC, heatDoneC);
        float pulse = 0.82f + 0.18f * Mathf.Sin(Time.time * 5f);       // gentle ember shimmer
        Color e = new Color(1f, 0.16f, 0.04f) * (t * t * 2.6f * pulse); // black when cold
        foreach (var r in _tubeRends)
        {
            if (r == null) continue;
            var m = r.material;                                          // instanced once, then cached
            if (m == null || !m.HasProperty("_EmissionColor")) continue;
            if (t > 0.01f) m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", e);
        }
    }

    // ---- readouts + collecting animation ------------------------------------

    /// Attach the live readouts once the tubes are found: rising temperature over
    /// the hard-glass tube, collection % over the collection tube.
    private void EnsureReadouts()
    {
        if (_tube != null && _tube.GetComponent<ProcessReadout>() == null)
            _tube.gameObject.AddComponent<ProcessReadout>()
                 .BindHeat("Hard-glass tube", temperature, heatDoneC);
        if (_collect != null && _collect.GetComponent<ProcessReadout>() == null)
            _collect.gameObject.AddComponent<ProcessReadout>()
                    .BindCollect("Gas collection tube", gas);
    }

    /// Gas visibly travelling from the hot tube into the collection tube: a stream
    /// of bubbles up the gap, plus a pale gas column that grows with the fill.
    private void CollectAnimation()
    {
        if (!Application.isPlaying || _tube == null || _collect == null) return;

        if (Time.time >= _nextBubbleAt)
        {
            _nextBubbleAt = Time.time + 0.12f;
            // A bubble puff partway along the tube → collection-tube path.
            Vector3 a = _tube.position + Vector3.up * 0.04f;
            Vector3 b = _collect.position;
            EffectVfx.Smoke(Vector3.Lerp(a, b, Random.Range(0.25f, 0.85f)) + Vector3.up * 0.02f,
                            new Color(0.75f, 0.9f, 1f, 0.5f));
            AudioService.TryPlayAt("bubble", _collect.position, 0.35f);
        }
        UpdateGasColumn();
    }

    /// A translucent pale-blue column inside the collection tube that rises with
    /// the collected fraction — the "gas over water" level.
    private void UpdateGasColumn()
    {
        if (gas == null || _collect == null) return;
        float f = gas.FillFraction;
        if (_gasColumn == null)
        {
            _gasColumn = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _gasColumn.name = "CollectedGas";
            var col = _gasColumn.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _gasColumn.transform.SetParent(_collect, true);
            var mr0 = _gasColumn.GetComponent<Renderer>();
            mr0.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            mr0.sharedMaterial = new Material(sh != null ? sh : Shader.Find("Standard"))
            { name = "CollectedGas_Runtime" };
        }
        // Fit the column INSIDE the tube in its LOCAL frame, so a tilted/held tube
        // keeps its gas aligned and contained.
        var lb = ExperimentSceneBuilder.LocalMeshBounds(_collect, "CollectedGas", "Powder", "Liquid");
        int ax = ExperimentSceneBuilder.LongestAxis(lb.size);
        float bore = ExperimentSceneBuilder.BoreOf(lb.size, ax);
        float w = bore * 0.6f;                                   // inside the walls
        float h = Mathf.Max(0.004f, lb.size[ax] * 0.82f * f);    // rises with the fill
        Vector3 lp = lb.center;
        lp[ax] = lb.min[ax] + h * 0.5f + lb.size[ax] * 0.05f;    // grows up from the closed end
        _gasColumn.transform.localPosition = lp;
        _gasColumn.transform.localRotation = ExperimentSceneBuilder.AxisAlign(ax);
        // Local frame → no lossyScale compensation; cylinder mesh is 2 units tall.
        _gasColumn.transform.localScale = new Vector3(w, h * 0.5f, w);
        var mat = _gasColumn.GetComponent<Renderer>().sharedMaterial;
        var c = new Color(0.72f, 0.88f, 1f);
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        }
    }

    private bool AnyLitBurnerNear(Vector3 pos)
    {
        foreach (var b in FindObjectsByType<BurnerController>(FindObjectsSortMode.None))
            if (b != null && b.IsLit && WithinReach(Vector3.Distance(b.transform.position, pos), heatDistance))
                return true;
        return false;
    }
}
