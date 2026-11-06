using UnityEngine;

/// Floats a marker/beacon above the station for the current available step,
/// guiding the player where to go next (storyboard: "follow the markers").
/// Hides when nothing is available (between steps / finished).
public class WaypointGuide : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private Transform marker;

    [Header("Placement")]
    [Tooltip("Clearance above the target's SOLID TOP — not its transform origin, which for a shelf bottle sits at its base.")]
    [SerializeField] private float heightOffset = 0.16f;
    [Tooltip("If anything is within this distance straight above, the target is shelf-caged and the marker moves in FRONT instead.")]
    [SerializeField] private float shelfClearance = 0.4f;
    [Tooltip("How far to pull the marker out toward the player when caged.")]
    [SerializeField] private float frontDistance = 0.3f;
    [Tooltip("How TALL the marker should appear at referenceDistance, in metres.")]
    [SerializeField] private float targetHeightAtReference = 0.16f;

    [Header("Distance scaling")]
    [Tooltip("Distance at which the marker is exactly markerScale. Nearer shrinks, farther grows.")]
    [SerializeField] private float referenceDistance = 2f;
    [Tooltip("Floor on the distance multiplier — stops the marker vanishing when you lean right into it.")]
    [SerializeField] private float minDistanceMul = 0.55f;
    [Tooltip("Ceiling on the distance multiplier — stops a far marker swelling to fill the room.")]
    [SerializeField] private float maxDistanceMul = 3.5f;

    /// The marker's ORIGINAL authored scale, captured exactly once and SERIALIZED.
    /// It has to persist: the multiplier is applied to it every frame, so re-deriving
    /// the "home" size from the marker's current scale would multiply an already
    /// multiplied value — the builder is re-run often, and each run would compound.
    /// Zero means "not captured yet".
    [SerializeField, HideInInspector] private Vector3 markerHomeScale = Vector3.zero;

    private Transform _cam;

    public void SetRunner(ExperimentRunner r) => runner = r;

    /// Set by Build Tutorial Scene Wiring so the tuned size lives in ONE place. The
    /// field is [SerializeField], so a value already saved in the scene would otherwise
    /// win over any new code default and the change would look like it did nothing.
    public void SetMarkerScale(float metresAtReference)
    {
        targetHeightAtReference = metresAtReference;
        ApplyMarkerScale(1f);       // no camera in edit mode — tuned size, unscaled
    }

    /// Pure: how much to grow the marker for its distance from the eye.
    ///
    /// Apparent (angular) size is size ÷ distance, so holding it constant means growing
    /// the marker LINEARLY with distance — that is the whole point: a fixed-size beacon
    /// shrinks exactly as fast as the thing you cannot find, which is backwards.
    ///
    /// Clamped at both ends deliberately, so it is NOT perfectly constant: without a
    /// floor it disappears into nothing when you lean over the bench, and without a
    /// ceiling a marker across the lab swells to fill the view.
    public static float DistanceScale(float distance, float referenceDistance,
                                      float minMul, float maxMul)
    {
        if (referenceDistance <= 0.001f) return 1f;
        return Mathf.Clamp(distance / referenceDistance, minMul, maxMul);
    }

    /// Push the tuned placement into the scene. Necessary, not merely tidy: these are
    /// [SerializeField]s that ALREADY existed in the saved scene, so Unity keeps the
    /// old serialized numbers and a changed C# default silently does nothing —
    /// heightOffset sat at the original 0.55 long after the default became 0.16.
    public void SetPlacement(float height, float clearance, float front,
                             float refDistance, float minMul, float maxMul)
    {
        heightOffset = height;
        shelfClearance = clearance;
        frontDistance = front;
        referenceDistance = refDistance;
        minDistanceMul = minMul;
        maxDistanceMul = maxMul;
    }

    /// The beacon's own height at its authored scale, measured once and SERIALIZED.
    /// Needed because the sizing is expressed in METRES, and metres mean nothing
    /// without knowing how big the art already is. Zero = not measured yet.
    [SerializeField, HideInInspector] private float markerBaseHeight = 0f;

    /// Size the marker to a real-world height rather than to a multiplier.
    ///
    /// A multiplier was the wrong model and ping-ponged twice: it scaled whatever the
    /// artist happened to author (~0.5 m here), and once distance scaling multiplied it
    /// again the marker filled the view at arm's length. A target height in METRES is
    /// absolute — 0.16 m at 2 m reads as a comfortable ~4.5°, and the distance term
    /// then holds that angle instead of compounding with it.
    private void ApplyMarkerScale(float distanceMul)
    {
        if (marker == null) return;
        if (markerHomeScale == Vector3.zero) markerHomeScale = marker.localScale;

        if (markerBaseHeight <= 0f)
        {
            // Measure at HOME scale, never at whatever it is currently wearing, or the
            // base is recorded pre-multiplied and every later size compounds.
            var current = marker.localScale;
            marker.localScale = markerHomeScale;
            markerBaseHeight = Mathf.Max(0.001f,
                ExperimentSceneBuilder.SolidWorldBounds(marker.gameObject).size.y);
            marker.localScale = current;
        }

        float mul = Mathf.Max(0.01f, targetHeightAtReference / markerBaseHeight) * distanceMul;
        marker.localScale = markerHomeScale * mul;
    }

    /// Pure placement rule (suite-pinned).
    ///
    /// Anchored to the target's SOLID BOUNDS, never its transform origin: a shelf
    /// bottle's origin sits at its base, so "origin + up" buries the marker in the
    /// bottle itself.
    ///
    /// When something is directly overhead — the shelf plank above, in a cabinet —
    /// going up hides the marker inside it. Caged targets get the marker pulled OUT
    /// toward the player at mid height instead, the same escape ProximityLabel makes
    /// for exactly the same reason.
    public static Vector3 MarkerPosition(Bounds solid, float heightOffset, bool caged,
                                         Vector3 camPos, float frontDistance)
    {
        if (!caged)
            return new Vector3(solid.center.x, solid.max.y + heightOffset, solid.center.z);

        Vector3 toCam = camPos - solid.center; toCam.y = 0f;
        Vector3 fwd = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.forward;
        return solid.center + fwd * frontDistance;
    }

    public string CurrentTargetTaskId { get; private set; }
    private PharmeeGatekeeper _gate;

    private void Update()
    {
        // Guidance runs in EVERY mode (see GuidePath). The beacon still stands down whenever
        // the floor path can do the job.
        if (marker == null || TimeSkipController.IsSkipping) { Hide(); return; }
        bool running = runner != null && runner.Graph != null && runner.IsRunning;

        string id = null;
        if (running) foreach (var t in runner.Graph.AvailableTasks()) { id = t.taskId; break; }
        CurrentTargetTaskId = id;

        // ONE arrow, never two — pick the SOURCE first ("go fetch that"), and hop to
        // the destination once it is in hand ("now put it here"). Two simultaneous
        // arrows would just ask the player which one to follow. The rule lives in
        // TaskTargetRegistry.PickTarget so the floor path picks the SAME object;
        // two navigation cues disagreeing is worse than either alone.
        var camNow = Camera.main;
        Transform station = running
            ? TaskTargetRegistry.PickTarget(id,
                camNow != null ? camNow.transform.position : Vector3.zero)   // pool → held-or-nearest
            : null;
        // Gear up, then the door: the gate's own destination before any run exists.
        if (station == null)
        {
            if (_gate == null) _gate = FindAnyObjectByType<PharmeeGatekeeper>();
            if (_gate != null) station = _gate.GateGuideTarget;
        }
        if (station == null) { Hide(); return; }

        // ⭐ Stand down while the ground path is showing. The path routes around the
        // benches and owns the far case; the beacon reads through a cabinet door and owns
        // the near one. Drawing both spends attention twice to answer one question, and
        // Tutorial Mode has little of it left to spend (design rule).
        if (station != null && GuidePath.Instance != null && GuidePath.Instance.PathShown)
        { Hide(); return; }

        if (station != null)
        {
            if (!marker.gameObject.activeSelf) marker.gameObject.SetActive(true);
            if (_cam == null && Camera.main != null) _cam = Camera.main.transform;

            // SolidWorldBounds, not all renderers: LiquidPourer's StreamLine/PourStream
            // are world-space and outlive a pour pointing at the floor, which would drag
            // the top of the bounds down a metre and drop the marker through the bench.
            Bounds solid = ExperimentSceneBuilder.SolidWorldBounds(station.gameObject);
            bool caged = Physics.Raycast(
                new Vector3(solid.center.x, solid.max.y + 0.01f, solid.center.z),
                Vector3.up, shelfClearance, ~0, QueryTriggerInteraction.Ignore);

            marker.position = MarkerPosition(solid, heightOffset, caged,
                _cam != null ? _cam.position : marker.position, frontDistance);

            // Scale AFTER positioning: the distance that matters is to where the marker
            // actually landed, which for a shelf-caged target is well in front of the
            // object it points at.
            float eyeDistance = _cam != null
                ? Vector3.Distance(_cam.position, marker.position)
                : referenceDistance;
            ApplyMarkerScale(DistanceScale(eyeDistance, referenceDistance, minDistanceMul, maxDistanceMul));
        }
        else Hide();
    }

    private void Hide()
    {
        if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
    }
}
