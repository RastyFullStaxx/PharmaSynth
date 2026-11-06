using UnityEngine;

/// Tutorial Mode's voice: the stuck-escalation ladder and the end-of-run summary.
/// Both are "what practice mode says to a struggling player", and both read the same
/// run counters, so they live together.
///
/// ⚠ Level 3 deliberately speaks the task's EXISTING hint rather than new copy: 76 of
/// 81 hint ACTION lines are voiced, and inventing coach dialogue would mean a fresh
/// voice generation pass for every mistake class.
public class TutorialCoach : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;

    public const float NudgeAfter = 15f, PulseAfter = 30f, SpeakAfter = 60f;

    private string _currentTaskId;
    private float _onStepSince;
    private int _shownLevel;
    private int _stepsDone, _corrections;
    private bool _subscribed;
    private PharmeeGestures _gestures;
    private VerbDemoPlayer _demo;

    /// Pure: 0 = silent, 1 = the watch nudge is enough, 2 = escalate the marker,
    /// 3 = say it out loud. Escalates only while the SAME step stays unsolved, so a
    /// player working steadily is never nagged.
    public static int LevelFor(float secondsOnStep)
    {
        if (secondsOnStep >= SpeakAfter) return 3;
        if (secondsOnStep >= PulseAfter) return 2;
        if (secondsOnStep >= NudgeAfter) return 1;
        return 0;
    }

    /// Pure: closure without a score. A percentage would turn practice back into an
    /// exam, which is exactly what this mode exists to avoid.
    public static string SummaryText(int stepsDone, int corrections)
    {
        string tail = corrections == 0 ? "no corrections needed."
                    : corrections == 1 ? "1 correction along the way."
                    : corrections + " corrections along the way.";
        return "Practice complete — " + stepsDone + " steps, " + tail;
    }

    /// Edit-mode seam; subscription lives here because in PLAY mode OnEnable fires
    /// before Bind() has set `runner`.
    public void Bind(ExperimentRunner r) { Detach(); runner = r; Subscribe(); }

    private void Awake() { if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>(); Subscribe(); }
    private void OnEnable() => Subscribe();
    private void OnDestroy() => Detach();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.TaskCompleted += OnTaskCompleted;
        runner.MistakeRecorded += OnMistake;
        _subscribed = true;
    }

    /// Explicit detach — DestroyImmediate skips OnDestroy for a component whose
    /// OnEnable never ran, and a ghost subscription would outlive the object.
    public void Detach()
    {
        if (!_subscribed || runner == null) { _subscribed = false; return; }
        runner.ExperimentStarted -= OnStarted;
        runner.TaskCompleted -= OnTaskCompleted;
        runner.MistakeRecorded -= OnMistake;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition _)
    {
        _stepsDone = 0; _corrections = 0;
        _currentTaskId = null; _shownLevel = 0; _onStepSince = Time.time;
    }

    private void OnTaskCompleted(ExperimentTask _) => _stepsDone++;
    private void OnMistake(LabErrorType _, string __) => _corrections++;

    /// Read by the practice summary panel.
    public int StepsDone => _stepsDone;
    public int Corrections => _corrections;

    /// Called by the gatekeeper when a practice run ends, in place of the grade screen.
    public void ShowSummary()
    {
        if (!TutorialSession.Active) return;
        // The subtitle carries the numbers; the VOICE says the fixed stem. A line assembled
        // at runtime can never match a clip (clips are keyed by the hash of the whole text),
        // so this summary had ended every practice run in fallback blips.
        Announce(SummaryText(_stepsDone, _corrections), PharmeeLines.PracticeComplete);
    }

    private void Update()
    {
        if (!TutorialSession.Active || runner == null || runner.Graph == null || !runner.IsRunning) return;
        if (TimeSkipController.IsSkipping) return;

        var first = runner.Graph.FirstAvailable();
        string id = first?.taskId, hint = first?.hint;
        if (id != _currentTaskId)
        {
            // Progress resets the ladder — the player is moving, leave them alone.
            _currentTaskId = id; _onStepSince = Time.time; _shownLevel = 0;
            return;
        }

        // Fire each rung exactly once, in order. A `while` rather than an `if` because a
        // long frame (a stage rebuild, a fade) can step over a threshold entirely, and a
        // player who waited 30 s should still get the 15 s nudge on the way past.
        //
        // ⛔ Levels 1 and 2 existed in LevelFor from the start but Update() returned
        // unless the level was 3, so the "15 s nudge / 30 s marker" the docs promised had
        // never once run. LevelFor was right; nothing consumed it.
        int level = LevelFor(Time.time - _onStepSince);
        while (_shownLevel < level) Escalate(++_shownLevel, id, hint);
    }

    /// One rung of the stuck ladder. Each is a DIFFERENT channel on purpose — repeating
    /// the same signal louder is what nagging is; changing the channel is what helping is.
    private void Escalate(int level, string taskId, string hint)
    {
        switch (level)
        {
            case 1:      // Silent. The hint is already on the watch — put it in their eyeline.
                if (!string.IsNullOrEmpty(hint)) ShowText(GlyphSafe.Sanitize(hint));
                break;
            case 2:      // Show, don't tell: Pharmee points, and the motion is mimed.
                PointAtTarget();
                Demo(taskId);
                break;
            case 3:      // Say it out loud, using the step's EXISTING (voiced) hint.
                if (!string.IsNullOrEmpty(hint)) Announce(GlyphSafe.Sanitize(hint));
                break;
        }
    }

    /// Pharmee turns and points at the step's target. PharmeeGestures already resolves
    /// tutorial targets off the same registry the glow uses — it was simply never asked
    /// to point at a step the player was stuck on.
    private void PointAtTarget()
    {
        if (_gestures == null) _gestures = FindAnyObjectByType<PharmeeGestures>();
        if (_gestures != null) _gestures.Play(PharmeeGesture.Point);
    }

    private void Demo(string taskId)
    {
        if (_demo == null) _demo = FindAnyObjectByType<VerbDemoPlayer>();
        if (_demo != null) _demo.Show(taskId);
    }

    /// Everything a poke asks for, at once: point, mime, and say it. Skips the ladder to
    /// its top because the player has explicitly asked — making them wait out the timers
    /// after asking for help is the opposite of helping.
    public void HelpNow()
    {
        if (!TutorialSession.Active || runner == null || runner.Graph == null || !runner.IsRunning) return;
        var first = runner.Graph.FirstAvailable();
        string id = first?.taskId, hint = first?.hint;
        if (string.IsNullOrEmpty(id)) return;
        PointAtTarget();
        Demo(id);
        if (!string.IsNullOrEmpty(hint)) Announce(GlyphSafe.Sanitize(hint));
        // Restart the ladder from the top rung so the timers do not immediately repeat
        // what the player just asked for and received.
        _shownLevel = 3; _onStepSince = Time.time;
    }

    /// Text only, no voice — the first rung must not interrupt a player who is thinking.
    private void ShowText(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        Vector3 pos = Camera.main != null
            ? Camera.main.transform.position + Camera.main.transform.forward * 1.4f
            : transform.position + Vector3.up * 1.5f;
        FloatingText.Show(msg, pos, new Color(0.7f, 1f, 0.8f), 1.3f);
    }

    private void Announce(string msg, string voicedStem = null)
    {
        if (string.IsNullOrEmpty(msg)) return;
        ShowText(msg);
        var narr = FindAnyObjectByType<NPCNarrationController>();
        if (narr == null) return;
        // Dynamic text rides a fixed voiced line: Say() accepts the clip explicitly.
        var clip = !string.IsNullOrEmpty(voicedStem) ? narr.ResolveVoice(voicedStem) : null;
        narr.Say(msg, 3.5f, clip);
    }
}
