using UnityEngine;

/// Drives Pharmee's BODY off the state machine that already exists (checklist section 3 /
/// asset-production-spec A1: "idle-float, talk, gesture-point, celebrate, warn - looping,
/// root-motion-free").
///
/// `PharmeeState` already selects his line pool, his face and his beep. This adds the fourth
/// mapping - state to body - and nothing else. It is deliberately THIN: all the curves live
/// in the pure `PharmeeGestureMath` so the suite can pin them, because there is no headset
/// pass available to judge them by eye.
///
/// It writes NO transform. Like `PharmeeMover` and `PharmeeGiveWay`, it feeds the two
/// components that own Pharmee's transforms:
///   * position -> `FloatBob.SetGestureOffset` (one more term in FloatBob's single sum)
///   * rotation + hands + rings -> `PharmeeAttitude.SetPose` (folded into its single
///     localRotation assignment)
/// Adding a component that wrote `Robot Origin` directly would fight `PharmeeAttitude`,
/// which overwrites it absolutely every LateUpdate.
[RequireComponent(typeof(PharmeeAttitude))]
public class PharmeeGestures : MonoBehaviour
{
    [SerializeField] private PharmeeAttitude attitude;
    [SerializeField] private FloatBob bob;
    [SerializeField] private PharmeeBrain brain;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private LabTourGuide tour;

    [Tooltip("How far the body yaws toward what he is pointing at.")]
    [SerializeField] private float aimYawDegrees = 22f;
    [SerializeField] private float aimSharpness = 4f;

    [SerializeField] private PharmeeGestureTuning tuning = new PharmeeGestureTuning
    {
        nodDegrees = 7f, pointLeanDegrees = 8f, warnRecoilDegrees = 10f, warnShakeDegrees = 5f,
        celebrateRise = 0.18f, celebrateSpinDegrees = 360f, celebrateFlare = 0.35f,
    };

    private PharmeeGesture _current = PharmeeGesture.None;
    private float _startedAt = -999f;
    private PharmeeState _lastState = PharmeeState.Idle;
    private float _aimYaw;

    /// Edit-mode AddComponent fires no Awake/OnEnable, hence the seam (house rule).
    public void Bind(PharmeeAttitude a, FloatBob b, PharmeeBrain br, ExperimentRunner r, LabTourGuide t)
    { attitude = a; bob = b; brain = br; runner = r; tour = t; }

    public void SetTuning(PharmeeGestureTuning t) => tuning = t;

    /// What he is doing right now (for tests / the simulate menu).
    public PharmeeGesture Current => _current;

    /// Start a gesture now. Re-triggering the same one restarts it, which is what you want
    /// for a second warning in a row.
    public void Play(PharmeeGesture g)
    {
        _current = g;
        _startedAt = Time.time;
    }

    private void Awake()
    {
        if (attitude == null) attitude = GetComponent<PharmeeAttitude>();
        if (bob == null) bob = GetComponent<FloatBob>();
        if (brain == null) brain = GetComponent<PharmeeBrain>();
        if (tour == null) tour = GetComponent<LabTourGuide>();
        if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>();
    }

    private void Update()
    {
        // 1. Follow the brain's state. Speak() is the single funnel that sets it, so one
        //    change-detect here covers greeting / instructing / warning / celebrating /
        //    encouraging without touching PharmeeBrain at all.
        if (brain != null && brain.State != _lastState)
        {
            _lastState = brain.State;
            var want = PharmeeGestureMath.ForState(_lastState);
            if (want != PharmeeGesture.None) Play(want);
        }

        // 2. A sustained point ends when he stops instructing or loses the target.
        if (_current == PharmeeGesture.Point && PointTarget() == null) _current = PharmeeGesture.None;

        // 3. Expire finished one-shots so they settle at exactly rest.
        if (_current != PharmeeGesture.None && !PharmeeGestureMath.IsSustained(_current) &&
            Time.time - _startedAt >= PharmeeGestureMath.DurationOf(_current))
            _current = PharmeeGesture.None;

        var pose = PharmeeGestureMath.Pose(_current, Time.time - _startedAt, tuning);

        // 4. Aim. Pure math cannot see a world target, so the yaw toward it is computed here
        //    and folded into the same pose before it is handed on.
        float wantYaw = 0f;
        var target = _current == PharmeeGesture.Point ? PointTarget() : null;
        if (target != null)
        {
            Vector3 to = target.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 1e-4f)
                wantYaw = Mathf.Clamp(Mathf.DeltaAngle(transform.eulerAngles.y,
                              Quaternion.LookRotation(to).eulerAngles.y),
                          -aimYawDegrees, aimYawDegrees);
        }
        _aimYaw = Mathf.Lerp(_aimYaw, wantYaw, Mathf.Clamp01(aimSharpness * Time.deltaTime));
        if (Mathf.Abs(_aimYaw) > 0.01f)
            pose.bodyRot = Quaternion.Euler(0f, _aimYaw, 0f) * pose.bodyRot;

        if (attitude != null) attitude.SetPose(pose);
        if (bob != null) bob.SetGestureOffset(pose.rootOffset);
    }

    /// What to point at, in the two GUIDED contexts only.
    ///
    /// Deliberately NOT during a graded run: the tutorial target map is
    /// only populated in Tutorial Mode, and extending it to scored play would add a standing
    /// visual hint on top of the wrist checklist. That is a game-design change, not polish.
    private Transform PointTarget()
    {
        // Lab Tour: the landmark whose beat he is speaking.
        if (tour != null && tour.IsActive && tour.CurrentLandmark != null) return tour.CurrentLandmark;

        // Tutorial Mode: the same derived map that drives the tutorial glow, read the same
        // way TutorialHighlighter reads it - including its lazy-build self-heal, because a
        // restart can re-furnish the stage without refilling the registry.
        if (!TutorialSession.Active) return null;
        if (runner == null || runner.Graph == null || !runner.IsRunning) return null;

        if (TaskTargetRegistry.TaskCount == 0) TutorialTargets.Build();
        foreach (var task in runner.Graph.AvailableTasks())
        {
            var targets = TaskTargetRegistry.Targets(task.taskId);
            for (int i = 0; i < targets.Count; i++)
                if (targets[i].transform != null) return targets[i].transform;
        }
        return null;
    }
}
