using UnityEngine;

/// Pure pour rules.
public static class PourMath
{
    /// ⛔ A SOLID never pours. The verb contract is "solids are scooped, liquids poured",
    /// and the tutorial sweep registers a Scoop verb for every solid reagent; without
    /// this check a tilted jar of brown sugar streams a brown liquid column. Nothing to
    /// pour is also not pourable.
    public static bool CanPour(ChemicalData chem)
        => chem != null && chem.state != PhysicalState.Solid && chem.state != PhysicalState.Powder;

    /// A bottle in the player's HAND with no vessel under its spout only trickles, so
    /// walking with a tipped bottle does not empty it onto the floor at the full rate
    /// (~12 ml/s) and quietly starve the experiment's supply. A bottle knocked over on
    /// the bench still spills fully; that is a real spill.
    public const float HeldSpillTrickle = 0.15f;
    public static float SpillAmount(float amount, bool held, bool hasTarget)
        => held && !hasTarget ? amount * HeldSpillTrickle : amount;
}

[RequireComponent(typeof(LiquidPhysics))]
public class LiquidPourer : MonoBehaviour
{
    [Header("Setup")]
    public Transform spout;
    public LineRenderer streamLine;

    [Header("Settings")]
    public float pourThreshold = 45f;
    public float maxFlowRate = 35f;   // ml/s at full inversion (finite ~150 ml bottles must survive 2-3 pours)

    [Header("Stream Animation")]
    public int streamSegments = 10;
    public float streamDropStrength = 0.75f;
    public float flowSmoothingSpeed = 8f;
    public float minStreamWidth = 0.0025f;
    public float maxStreamWidth = 0.015f;

    [Header("Hazard Settings")]
    [Tooltip("Drag your AcidSpill prefab here")]
    public GameObject acidSpillPrefab;
    [Tooltip("Minimum time between spawning spills (prevents lag)")]
    public float spillCooldown = 1.0f;

    private LiquidPhysics sourceContainer;
    private float lastSpillTime = 0f;
    private float smoothedFlow01;

    // Procedural falling-liquid stream + landing puddles, so pours and spills are
    // visible. Independent of the optional LineRenderer.
    private ParticleSystem _pourStream;
    private float _lastPuddle;
    private Transform Mouth => spout != null ? spout : transform;

    // Continuous positional pour sound (realism: pouring was silent).
    private AudioSource pourAudio;
    private float pourBaseVol = 0.5f;

    // Pour assist (pours into vials/tubes/beakers kept missing —
    // the beaker would not catch the spills): VR aim is imprecise, so when the
    // precise ray misses, a forgiving sphere sweep catches a receiver whose
    // mouth is within this radius of the stream line.
    [Header("Pour Assist (W5.12)")]
    public float assistRadius = 0.045f;

    /// Dev toggle (DevExperimentDriver 'P'): shows what each PourTick actually
    /// hits so in-headset misses can be diagnosed live.
    public static bool DebugOverlay;
    private float _lastDebugText;

    /// Pure, testable pour-volume curve: silent when not pouring, otherwise the
    /// clip's base × the Sfx category volume, floored so a trickle is still heard.
    public static float PourVolume(bool pouring, float baseVol, float catVol, float flow01)
        => pouring ? Mathf.Clamp01(baseVol) * Mathf.Clamp01(catVol) * Mathf.Clamp01(0.3f + Mathf.Clamp01(flow01) * 0.7f) : 0f;

    void Start()
    {
        sourceContainer = GetComponent<LiquidPhysics>();

        if (streamLine)
        {
            streamLine.positionCount = Mathf.Max(2, streamSegments);
            streamLine.enabled = false;
        }
    }

    /// Edit-mode/test seam (Start doesn't run on AddComponent in edit mode).
    public void Bind(LiquidPhysics source) => sourceContainer = source;

    void Update()
    {
        float tiltAngle = Vector3.Angle(Vector3.up, transform.up);

        if (tiltAngle > pourThreshold && sourceContainer != null && sourceContainer.currentLiquidVolume > 0
            && PourMath.CanPour(sourceContainer.currentChemical))
        {
            EnsureStreamLine();
            PourTick(Time.deltaTime, tiltAngle);
            UpdatePourAudio(true, smoothedFlow01);
            UpdatePourStream(true, smoothedFlow01);
        }
        else
        {
            smoothedFlow01 = Mathf.Lerp(smoothedFlow01, 0f, Time.deltaTime * flowSmoothingSpeed);
            if (streamLine) streamLine.enabled = false;
            UpdatePourAudio(false, 0f);
            UpdatePourStream(false, 0f);
        }
    }

    private static float s_lastPourTime = float.NegativeInfinity;
    /// Seconds since any bottle in the lab last poured (NPC chatter waits on it).
    public static float SecondsSinceAnyPour => Time.time - s_lastPourTime;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable _grab;
    private bool IsHeld()
    {
        if (_grab == null) _grab = GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
        return _grab != null && _grab.isSelected;
    }

    private float _nextSpillWarn;
    private void WarnSpilling()
    {
        if (!Application.isPlaying || Time.time < _nextSpillWarn) return;
        _nextSpillWarn = Time.time + 4f;
        FloatingText.Show("Careful — you're spilling!", Mouth.position + Vector3.up * 0.12f, new Color(1f, 0.8f, 0.45f), 1.1f);
    }

    /// Drive the looping 3D pour source: swell with flow while pouring, fade to
    /// silence when the vessel is righted. Runtime-only; no-op without an AudioService.
    void UpdatePourAudio(bool pouring, float flow01)
    {
        if (!Application.isPlaying) return;
        if (pourAudio == null)
        {
            if (!pouring) return;                          // don't allocate a source until actually pouring
            if (AudioService.Instance == null) return;
            var e = AudioService.Instance.EntryOf("pour");
            if (e == null || e.clip == null) return;
            pourBaseVol = Mathf.Clamp01(e.volume);
            var host = spout != null ? spout : transform;
            var go = new GameObject("PourAudio");
            go.transform.SetParent(host, false);
            pourAudio = go.AddComponent<AudioSource>();
            pourAudio.clip = e.clip;
            pourAudio.loop = true;
            pourAudio.playOnAwake = false;
            pourAudio.spatialBlend = 1f;                 // 3D positional
            pourAudio.rolloffMode = AudioRolloffMode.Linear;
            pourAudio.minDistance = 0.5f;
            pourAudio.maxDistance = 6f;
            pourAudio.volume = 0f;
        }

        float catVol = AudioService.Instance != null ? AudioService.Instance.VolumeOf(AudioCategory.Sfx) : 1f;
        float target = PourVolume(pouring, pourBaseVol, catVol, flow01);
        pourAudio.volume = Mathf.Lerp(pourAudio.volume, target, Time.deltaTime * 10f);
        if (pouring && !pourAudio.isPlaying) pourAudio.Play();
        else if (!pouring && pourAudio.isPlaying && pourAudio.volume < 0.01f) pourAudio.Stop();
    }

    /// Procedural tinted droplet stream falling from the vessel mouth while pouring —
    /// visible even when the optional LineRenderer isn't wired. Gravity does the work,
    /// so orientation is forgiving; emission scales with flow, colour tracks the liquid.
    void UpdatePourStream(bool pouring, float flow01)
    {
        if (!Application.isPlaying) return;
        if (_pourStream == null)
        {
            if (!pouring) return;
            var go = new GameObject("PourStream");
            go.transform.SetParent(Mouth, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.rotation = Quaternion.LookRotation(Vector3.down);   // emit downward
            _pourStream = go.AddComponent<ParticleSystem>();
            _pourStream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _pourStream.main;
            main.loop = true; main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.55f;
            main.startSpeed = 0.45f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            main.gravityModifier = 2.6f;
            main.maxParticles = 160;
            var em = _pourStream.emission; em.rateOverTime = 0f;
            var sh = _pourStream.shape;
            sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 5f; sh.radius = 0.004f;
            var r = _pourStream.GetComponent<ParticleSystemRenderer>();
            r.material = EffectVfx.ParticleMaterial();
            r.sortingOrder = 10;
            _pourStream.Play();
        }
        var emis = _pourStream.emission;
        emis.rateOverTime = pouring ? Mathf.Lerp(35f, 110f, Mathf.Clamp01(flow01)) : 0f;
        if (pouring)
        {
            var main = _pourStream.main;
            Color c = sourceContainer != null && sourceContainer.currentChemical != null
                ? sourceContainer.currentChemical.liquidColor : new Color(0.6f, 0.75f, 0.9f);
            c.a = 1f;
            main.startColor = c;
        }
    }

    /// The connected pour ARC (DrawCurvedStream) otherwise renders only on bottles with
    /// an authored LineRenderer, which is none of them. Build it on first pour so every
    /// pourer shows a continuous mouth-to-surface stream on top of the droplet particles.
    void EnsureStreamLine()
    {
        if (streamLine != null || !Application.isPlaying) return;
        var go = new GameObject("StreamLine");
        go.transform.SetParent(Mouth, false);
        streamLine = go.AddComponent<LineRenderer>();
        streamLine.useWorldSpace = true;
        streamLine.positionCount = Mathf.Max(2, streamSegments);
        streamLine.startWidth = minStreamWidth;
        streamLine.endWidth = minStreamWidth * 0.65f;
        streamLine.numCapVertices = 4;
        streamLine.material = EffectVfx.ParticleMaterial();
        streamLine.sortingOrder = 10;
        streamLine.enabled = false;
    }

    /// Pick the receiving vessel from a downward raycast set.
    /// Skips the source's own colliders: a tilted bottle's ray often exits through its
    /// own body, and a single raycast can hit itself, fire LiquidAdded on itself, and
    /// even complete pour tasks by self-tilting.
    /// The nearest remaining hit decides: a LiquidPhysics there = the transfer
    /// target; anything else = the waste/puddle surface. Static + real-collider
    /// friendly so the self-test suite can exercise it in edit mode.
    public static LiquidPhysics ResolveTarget(RaycastHit[] hits, LiquidPhysics source, out RaycastHit firstHit)
        => ResolveTarget(hits, source, out firstHit, out _);

    /// Overload that also reports the funnel the stream passed THROUGH, so the
    /// pour can show liquid leaving its stem.
    public static LiquidPhysics ResolveTarget(RaycastHit[] hits, LiquidPhysics source,
                                              out RaycastHit firstHit, out LiquidPassthrough through)
    {
        firstHit = default;
        through = null;
        float bestDist = float.MaxValue;
        float throughDist = float.MaxValue;
        Collider bestCol = null;
        for (int i = 0; i < hits.Length; i++)
        {
            var col = hits[i].collider;
            if (col == null) continue;
            // Own hierarchy (IsChildOf includes self). NOT transform.root — stage
            // props and vessels share the DynamicStage root, and shelf bottles
            // share the ReagentShelf root, so a root check would skip real targets.
            if (source != null && col.transform.IsChildOf(source.transform)) continue;
            // Funnels pass the stream through to whatever sits underneath —
            // treating their collider as the landing surface wasted the
            // hydrolysate as a puddle ON the funnel.
            var pass = col.GetComponentInParent<LiquidPassthrough>();
            if (pass != null)
            {
                // Nearest funnel wins: the stream enters the FIRST cone it meets.
                if (hits[i].distance < throughDist) { throughDist = hits[i].distance; through = pass; }
                continue;
            }
            if (hits[i].distance < bestDist)
            {
                bestDist = hits[i].distance;
                bestCol = col;
                firstHit = hits[i];
            }
        }
        if (bestCol == null) return null;
        var lp = bestCol.GetComponentInParent<LiquidPhysics>();
        if (lp == null || lp == source) return null;
        // A stream crossing the reagent shelf never contaminates stock (it spills instead).
        return LabReset.RefusesTransfer(lp, source != null ? source.currentChemical : null) ? null : lp;
    }

    /// One pour step (extracted from Update so edit-mode tests can drive a real
    /// transfer deterministically). Triggers are ignored — station sensor columns,
    /// socket spheres and hot-surface zones must never swallow the stream.
    public void PourTick(float dt, float currentTilt)
    {
        if (sourceContainer == null) sourceContainer = GetComponent<LiquidPhysics>();
        if (sourceContainer == null) return;

        // Calculate Amount
        float tiltDelta = Mathf.InverseLerp(pourThreshold, 180f, currentTilt);
        smoothedFlow01 = Mathf.Lerp(smoothedFlow01, tiltDelta, dt * flowSmoothingSpeed);
        float currentFlowRate = maxFlowRate * tiltDelta;
        float amountToPour = currentFlowRate * dt;

        // Visuals
        if (streamLine)
        {
            streamLine.enabled = true;

            if (sourceContainer.currentChemical != null)
            {
                Color c = sourceContainer.currentChemical.liquidColor;
                streamLine.startColor = c;
                streamLine.endColor = c;
            }

            float width = Mathf.Lerp(minStreamWidth, maxStreamWidth, smoothedFlow01);
            streamLine.startWidth = width;
            streamLine.endWidth = width * 0.65f;
        }

        // Raycast Physics: precise ray first, then the forgiving sphere sweep —
        // the sweep also catches a receiver already overlapping the mouth
        // (distance-0 hits), which the thin ray can slip past entirely.
        var hits = Physics.RaycastAll(Mouth.position, Vector3.down, 2.0f, ~0, QueryTriggerInteraction.Ignore);
        RaycastHit hit;
        LiquidPhysics target = ResolveTarget(hits, sourceContainer, out hit, out var funnel);
        if (target == null)
        {
            var sweep = Physics.SphereCastAll(Mouth.position, assistRadius, Vector3.down, 2.0f, ~0,
                                              QueryTriggerInteraction.Ignore);
            RaycastHit sweepHit;
            var sweepTarget = ResolveTarget(sweep, sourceContainer, out sweepHit, out var sweepFunnel);
            if (funnel == null) funnel = sweepFunnel;
            if (sweepTarget != null)
            {
                target = sweepTarget;
                // Distance-0 overlap hits report a degenerate point — land the
                // visual arc on the vessel instead so it matches the transfer.
                hit = sweepHit;
                if (hit.distance <= 0f)
                {
                    hit.point = target.transform.position;
                    hit.normal = Vector3.up;
                }
            }
        }
        if (DebugOverlay && Application.isPlaying && Time.time - _lastDebugText > 0.5f)
        {
            _lastDebugText = Time.time;
            FloatingText.Show(
                "pour hit: " + (hit.collider != null ? hit.collider.name : "—")
                + "\ntarget: " + (target != null ? target.name : "none"),
                Mouth.position + Vector3.up * 0.06f,
                target != null ? new Color(0.5f, 1f, 0.6f) : new Color(1f, 0.6f, 0.5f), 0.8f);
        }
        // The stream vanished into a funnel: show it leaving the STEM, and end the
        // visible arc at the cone's mouth so it reads as flowing through the hole
        // below rather than straight past the glass.
        if (funnel != null)
        {
            funnel.Flow(sourceContainer.currentChemical != null
                        ? sourceContainer.currentChemical.liquidColor : new Color(0.6f, 0.75f, 0.9f));
            if (streamLine)
            {
                var cone = ExperimentSceneBuilder.SolidWorldBounds(funnel.gameObject);
                DrawCurvedStream(Mouth.position, new Vector3(cone.center.x, cone.max.y, cone.center.z), smoothedFlow01);
                streamLine.enabled = true;
            }
        }

        s_lastPourTime = Time.time;
        bool held = IsHeld();
        if (target == null) amountToPour = PourMath.SpillAmount(amountToPour, held, false);
        if (target == null && held) WarnSpilling();

        if (hit.collider != null)
        {
            if (streamLine && funnel == null) DrawCurvedStream(Mouth.position, hit.point, smoothedFlow01);

            if (target != null)
            {
                // TRANSFER LOGIC (Pouring into another flask)
                ChemicalData pouredLiquid = sourceContainer.PourOut(amountToPour);
                if (pouredLiquid != null) target.AddLiquid(pouredLiquid, amountToPour);
            }
            else
            {
                // 1. Check if the chemical causes a hazard
                // (Make sure your Chemical Data name actually contains "H2SO4")
                if (sourceContainer.currentChemical != null && sourceContainer.currentChemical.isDangerous)
                {
                    SpawnHazard(hit.point, hit.normal);
                }

                // 2. Waste the liquid — and leave a growing wet puddle where it lands.
                sourceContainer.PourOut(amountToPour);
                if (Application.isPlaying && Time.time - _lastPuddle > 0.6f)
                {
                    _lastPuddle = Time.time;
                    Color pc = sourceContainer.currentChemical != null
                        ? sourceContainer.currentChemical.liquidColor : new Color(0.55f, 0.7f, 0.85f);
                    SpillPuddle.Spawn(hit.point + Vector3.up * 0.02f, pc, 0.10f);
                }
            }
        }
        else
        {
            // Poured into void
            if (streamLine && funnel == null) DrawCurvedStream(Mouth.position, Mouth.position + Vector3.down * 0.5f, smoothedFlow01);
            sourceContainer.PourOut(amountToPour);
        }
    }

    void DrawCurvedStream(Vector3 start, Vector3 end, float flow01)
    {
        if (streamLine == null)
            return;

        int segments = Mathf.Max(2, streamSegments);
        if (streamLine.positionCount != segments)
            streamLine.positionCount = segments;

        Vector3 middle = (start + end) * 0.5f + Vector3.down * Mathf.Lerp(0.02f, streamDropStrength, flow01);

        for (int i = 0; i < segments; i++)
        {
            float t = i / (float)(segments - 1);
            Vector3 p0 = Vector3.Lerp(start, middle, t);
            Vector3 p1 = Vector3.Lerp(middle, end, t);
            Vector3 point = Vector3.Lerp(p0, p1, t);
            streamLine.SetPosition(i, point);
        }
    }

    void SpawnHazard(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Don't spawn if we just spawned
        if (Time.time - lastSpillTime < spillCooldown) return;
        lastSpillTime = Time.time;

        if (acidSpillPrefab != null)
        {
            Vector3 spawnPos = hitPoint + (hitNormal * 0.1f);
            Quaternion spawnRot = Quaternion.FromToRotation(Vector3.up, hitNormal);
            Instantiate(acidSpillPrefab, spawnPos, spawnRot);
        }
    }
}