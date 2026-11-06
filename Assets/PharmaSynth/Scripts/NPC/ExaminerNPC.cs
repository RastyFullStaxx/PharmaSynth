using UnityEngine;

/// Dr. Jimenez — the assessment-mode examiner (plan §3.4) plus his proctor VOICE
/// (a varied pool of remarks and behaviours). In experiments flagged assessmentMode
/// he observes and gives NO hints (Pharmee also stays quiet); the `State`/`IsObserving`
/// machine still reflects that flag exactly.
/// Independently, whenever ANY run is active he proctors aloud: a stern greeting at
/// the start, then occasional oversight remarks (never hints), each driving the
/// rigged model's "Talking" bool for its duration and showing a subtitle if he has
/// a narration channel. Movement is ProctorRoamer's job; this is his voice + state.
public class ExaminerNPC : MonoBehaviour
{
    public enum ExaminerState { Dormant, Observing, Recording }

    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private MonoBehaviour faceOrAnimatorHook; // optional visual, wired in art pass

    [Header("Proctor voice")]
    [SerializeField] private Animator animator;                 // drives "Talking"
    [SerializeField] private NPCNarrationController narration;  // optional subtitle channel
    [SerializeField] private string talkBool = "Talking";
    [SerializeField] private float lineSeconds = 4f;
    [SerializeField] private float firstRemarkDelay = 12f;
    [SerializeField] private float remarkMin = 45f;
    [SerializeField] private float remarkMax = 90f;

    private bool _subscribed, _hasTalk, _running, _talking;
    private float _talkUntil, _nextRemark;
    private int _variant;

    public ExaminerState State { get; private set; } = ExaminerState.Dormant;
    public bool IsObserving => State == ExaminerState.Observing;
    public bool IsTalking => _talking;
    public string LastLine { get; private set; } = "";

    /// Pure predicate — does this module put the examiner on watch?
    public static bool ShouldObserve(ExperimentModuleDefinition m) => m != null && m.assessmentMode;

    public void SetRunner(ExperimentRunner r) { Unsubscribe(); runner = r; Subscribe(); }

    /// Full wiring seam (animator + subtitle), used by the NPC-polish builder.
    public void Bind(ExperimentRunner r, Animator a, NPCNarrationController n)
    {
        Unsubscribe();
        runner = r; animator = a; narration = n;
        _hasTalk = HasBool(animator, talkBool);
        Subscribe();
    }

    private void Start() { _hasTalk = HasBool(animator, talkBool); Subscribe(); }
    private void OnEnable() => Subscribe();
    private void OnDisable() { Unsubscribe(); SetTalking(false); }

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.ExperimentFinished += OnFinished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.ExperimentFinished -= OnFinished;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        State = ShouldObserve(m) ? ExaminerState.Observing : ExaminerState.Dormant;
        // He proctors every run aloud, assessment or guided (oversight, not hints).
        _running = true;
        Say(PharmeeLines.Pick(PharmeeLines.ExamGreeting, _variant++));
        _nextRemark = Time.time + firstRemarkDelay;
    }

    private void OnFinished(ExperimentResult r)
    {
        if (State == ExaminerState.Observing) State = ExaminerState.Recording;
        _running = false;
        SetTalking(false);
        // (art pass: play a clipboard-note animation here; the grade is recorded by the grader)
    }

    private void Update()
    {
        if (_talking && Time.time >= _talkUntil) SetTalking(false);
        if (!_running || _talking) return;
        // No ambient remarks during the scripted review — they were
        // interrupting Jimenez's own quiz briefing / verdict mid-sentence.
        if (PharmeeGatekeeper.ReviewFlowActive) return;
        if (Time.time < _nextRemark) return;
        Say(PharmeeLines.Pick(PharmeeLines.ExamRemarks, _variant++));
        _nextRemark = Time.time + Random.Range(remarkMin, remarkMax);
    }

    /// External line seam (the gate's quiz briefing + score remarks speak through
    /// Jimenez's own bubble/animator, never Pharmee's channel).
    public void SpeakLine(string line) => Say(line);

    /// How long this line will really take him to say (reveal + read hold) —
    /// the gate schedules its next review beat off THIS, not a fixed dwell, so a
    /// long line (his ILO recap) can't be spoken over by the beat behind it.
    public float SecondsFor(string line)
        => narration != null ? narration.SecondsFor(line, lineSeconds) : lineSeconds;

    private void Say(string line)
    {
        if (string.IsNullOrEmpty(line)) return;
        LastLine = line;
        if (narration != null) narration.Say(line, lineSeconds);
        SetTalking(true);
        // Keep the talking animation up for the line's REAL duration, not the
        // authored dwell, or his mouth stops moving mid-sentence.
        _talkUntil = Time.time + SecondsFor(line);
    }

    private void SetTalking(bool on)
    {
        _talking = on;
        if (_hasTalk && animator != null) animator.SetBool(talkBool, on);
    }

    private static bool HasBool(Animator a, string param)
    {
        if (a == null || a.runtimeAnimatorController == null) return false;
        foreach (var p in a.parameters)
            if (p.type == AnimatorControllerParameterType.Bool && p.name == param) return true;
        return false;
    }
}
