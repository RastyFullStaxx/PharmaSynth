using System;
using UnityEngine;

/// Pharmee's expressions, driven onto the robot's screen-face (material/animator).
public enum PharmeeFaceExpression { Neutral, Happy, Warning }

/// Pharmee's behavioural states.
public enum PharmeeState { Idle, Greeting, Instructing, Warning, Celebrating, Encouraging }

/// Optional visual face — implemented by the robot's face material/animator layer.
public interface IPharmeeFace { void SetExpression(PharmeeFaceExpression e); }

/// Robot-guide brain: reacts to ExperimentRunner events with subtitle dialogue and
/// a face expression (a core requirement: the NPC robot has dialogue). Greets on
/// start, instructs the current step, warns on mistakes, celebrates or encourages at
/// the end. Dialogue lives in serialized data (client-reviewable); step instructions
/// come from each task's hint.
public class PharmeeBrain : MonoBehaviour
{
    [Serializable]
    public class DialogueSet
    {
        [TextArea] public string greeting = "Welcome to the lab! I'm Pharmee. Follow the steps on your wrist checklist and I'll guide you.";
        [TextArea] public string celebrate = "Outstanding! You've completed the experiment. Great synthesis!";
        [TextArea] public string encourage = "Good effort — review the steps and give it another go. You've got this!";
        [TextArea] public string wrongReagent = "Hmm, that isn't the reagent this step needs. Check your wrist checklist.";
        [TextArea] public string wrongStep = "Let's not skip ahead — complete the current step first.";
        [TextArea] public string overheat = "Careful — it's overheating! Ease off the heat.";
        [TextArea] public string safety = "Safety first! Mind the hazard and keep your PPE on.";
    }

    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace
    [SerializeField] private DialogueSet lines = new DialogueSet();
    [SerializeField] private float lineSeconds = 3.5f;
    [Tooltip("When a CutsceneDirector handles intro/outro, let it greet — the brain only instructs/warns.")]
    [SerializeField] private bool deferIntroToDirector = false;

    [Header("Ambient chatter")]
    [Tooltip("Seconds of quiet before Pharmee offers an idle comment while a run is active.")]
    [SerializeField] private float idleChatterGap = 80f;   // seconds; long enough that idle chatter stays rare
    [SerializeField] private bool idleChatterEnabled = true;
    [Tooltip("Minimum gap between REACTION lines (step done). Collapses genuine duplicates — e.g. one pour satisfying two bindings in the same frame — WITHOUT silencing the instruction for a newly-available step. Kept short on purpose: the wrist checklist is the authoritative guidance, but the spoken next-step hint is what a lost player leans on.")]
    [SerializeField] private float reactionCooldown = 3f;

    private IPharmeeFace _face;
    private bool _subscribed;
    private bool _assessment;   // assessment mode → no procedural hints (safety warnings stay)
    private float _baseLineSeconds = -1f;
    private int _variant;        // rotates line pools so he doesn't repeat himself
    private bool _praiseToggle;
    private float _lastLineTime = -999f;

    /// Comfort seam: subtitle pacing multiplier (2 = lines dwell half as long,
    /// 0.5 = twice as long). Baseline is the authored lineSeconds.
    public void SetSubtitlePace(float speed)
    {
        if (_baseLineSeconds < 0f) _baseLineSeconds = lineSeconds;
        lineSeconds = ComfortMath.LineSecondsFor(_baseLineSeconds, speed);
    }

    /// True while the current experiment is in assessment mode (Pharmee stays quiet).
    public bool AssessmentMode => _assessment;

    public PharmeeState State { get; private set; } = PharmeeState.Idle;
    public string LastLine { get; private set; } = "";
    public PharmeeFaceExpression LastExpression { get; private set; } = PharmeeFaceExpression.Neutral;

    /// Assign (or swap) the runner at runtime; subscribes immediately.
    public void SetRunner(ExperimentRunner r)
    {
        Unsubscribe();
        runner = r;
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnEnable()
    {
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.TaskCompleted += OnTaskCompleted;
        runner.MistakeRecorded += OnMistake;
        runner.ExperimentFinished += OnFinished;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.TaskCompleted -= OnTaskCompleted;
        runner.MistakeRecorded -= OnMistake;
        runner.ExperimentFinished -= OnFinished;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        // Assessment mode (Dr. Jimenez observing): Pharmee gives no procedural hints.
        _assessment = m != null && m.assessmentMode;
        if (_assessment) return;
        // With a cutscene director present, it plays the intro then calls
        // InstructCurrent() on finish — the brain stays quiet here to avoid overlap.
        if (deferIntroToDirector) return;
        Speak(PharmeeState.Greeting, PharmeeFaceExpression.Happy, Variant(lines.greeting, PharmeeLines.Greetings));
        InstructCurrent();
    }

    /// On a completed step: fold an occasional bit of praise into the next
    /// instruction (one line, so it doesn't stomp itself) — livelier than a bare hint.
    private void OnTaskCompleted(ExperimentTask t)
    {
        if (_assessment) return;
        if (runner == null || runner.Graph == null) return;
        // ONE pour can satisfy two bindings in the same frame, which fires two Speak()
        // calls back-to-back and reads as Pharmee talking over himself. SAFETY warnings
        // deliberately bypass this cooldown (see OnMistake).
        if (Time.time - _lastLineTime < reactionCooldown) return;
        // ⛔ NEVER CONCATENATE SPOKEN LINES. Voice clips are keyed by
        // the hash of the WHOLE string, so "Nice work! " + an instruction hashes to
        // something that exists in no bank and silently drops the line back to
        // placeholder blips — even though both halves are voiced individually.
        // Speak them as two lines instead; the narration controller queues them and
        // plays them back to back with its normal beat.
        string praise = (_praiseToggle = !_praiseToggle) ? PharmeeLines.Pick(PharmeeLines.Praise, _variant++) : null;
        if (!string.IsNullOrEmpty(praise))
            Speak(PharmeeState.Instructing, PharmeeFaceExpression.Happy, praise);
        var next = runner.Graph.FirstAvailable();
        if (next != null) Speak(PharmeeState.Instructing, PharmeeFaceExpression.Happy, InstructionFor(next));
    }

    /// Idle chatter: after a stretch of quiet mid-run, Pharmee offers a friendly
    /// comment (never in assessment mode; never overlapping a fresh line;
    /// never during the scripted review flow — IsRunning stays true through the
    /// quiz, and idle lines were cutting off the gate's congratulations).
    private void Update()
    {
        // While the wrist procedures panel is up, clear any line Pharmee is
        // showing so it never lingers over the panel — but
        // NEVER mid-reveal: this ran every frame with no IsRevealing check, so a
        // single stray palm-up frame (suppression latches 1.5 s) truncated the
        // sentence the player was still reading. A typing line is
        // allowed to finish; only a fully-revealed one is cleared early.
        if (WristWatchController.SuppressNpcPokes)
        {
            if (narration != null && narration.IsSpeaking && !narration.IsRevealing)
                narration.EndLine();
            return;
        }
        if (!idleChatterEnabled || _assessment) return;
        if (PharmeeGatekeeper.ReviewFlowActive) return;
        if (runner == null || !runner.IsRunning) return;
        // Never chatter at someone mid-pour or during a cinematic: wait until they are idle.
        if (LiquidPourer.SecondsSinceAnyPour < 6f || CinemaView.AnyActive) return;   // quiet until 6 s after the last pour
        // Enforce a long quiet gap even if the scene serialized an older value
        // (constant chatter was distracting; the gap went 45 -> 80).
        if (Time.time - _lastLineTime < Mathf.Max(idleChatterGap, 80f)) return;
        Speak(PharmeeState.Encouraging, PharmeeFaceExpression.Happy,
              PharmeeLines.Pick(PharmeeLines.Idle, _variant++));
    }

    /// Pick a fresh pool line; falls back to the authored single line if the pool is empty.
    private string Variant(string authored, string[] pool)
    {
        var v = PharmeeLines.Pick(pool, _variant++);
        return string.IsNullOrEmpty(v) ? authored : v;
    }

    /// Safety/mistake warnings keep a SHORT floor only — the player must always
    /// hear why something went wrong, but a burst of identical mistakes (a spill
    /// bouncing) must not machine-gun the same warning.
    private void OnMistake(LabErrorType type, string message)
    {
        if (Time.time - _lastLineTime < 1.5f) return;
        // The same warning twice in a row reads as nagging; the HUD toast still marks
        // every mistake. A DIFFERENT mistake is still spoken at once.
        if (!WarnAgain(type, _lastWarnType, Time.time - _lastWarnTime)) return;
        _lastWarnType = type; _lastWarnTime = Time.time;
        Speak(PharmeeState.Warning, PharmeeFaceExpression.Warning, WarnLineFor(type));
    }

    private LabErrorType? _lastWarnType;
    private float _lastWarnTime = float.NegativeInfinity;
    public const float RepeatWarnSeconds = 6f;

    /// Pure (suite-pinned): speak this warning? A repeat of the same type waits 6 s.
    public static bool WarnAgain(LabErrorType type, LabErrorType? lastType, float secondsSinceLast)
        => lastType != type || secondsSinceLast >= RepeatWarnSeconds;

    private void OnFinished(ExperimentResult r)
    {
        // The end cutscene carries the celebrate/encourage line when a director is present.
        if (deferIntroToDirector) return;
        if (r.passed) Speak(PharmeeState.Celebrating, PharmeeFaceExpression.Happy, Variant(lines.celebrate, PharmeeLines.Celebrate));
        else Speak(PharmeeState.Encouraging, PharmeeFaceExpression.Neutral, Variant(lines.encourage, PharmeeLines.Encourage));
    }

    /// Instruct the current available step (nearest to done). No-op when finished.
    public void InstructCurrent()
    {
        if (_assessment) return;                       // no procedural hints in assessment mode
        if (runner == null || runner.Graph == null) return;
        // Nothing available (all complete): stay in the current state.
        var current = runner.Graph.FirstAvailable();
        if (current != null) Speak(PharmeeState.Instructing, PharmeeFaceExpression.Neutral, InstructionFor(current));
    }

    private void Speak(PharmeeState state, PharmeeFaceExpression face, string line)
    {
        // Stay quiet while the wrist-watch procedures panel is up, so Pharmee never talks
        // over it: the player is reading, not acting. The window also covers 1.5 s after it closes.
        if (WristWatchController.SuppressNpcPokes) return;
        // ⛔ THE REVIEW BELONGS TO THE GATEKEEPER'S SCRIPT (Pharmee
        // kept talking through the quiz cutscenes
        // while Dr. Jimenez spoke). Only Update() checked this, so
        // Pharmee's AUTONOMOUS reactions still fired over Dr. Jimenez: submitting
        // the quiz completes the DataSheet task → OnTaskCompleted → an
        // instruction line, and runner.Finish → OnFinished → a celebrate line
        // landing on top of Jimenez's verdict. The gatekeeper still speaks
        // Pharmee's SCRIPTED review lines directly through the narration
        // controller, which is unaffected — this only silences the brain.
        if (PharmeeGatekeeper.ReviewFlowActive) return;

        State = state;
        LastLine = line;
        LastExpression = face;
        _lastLineTime = Time.time;
        _face?.SetExpression(face);
        if (narration != null) narration.Say(line, lineSeconds);
        // ⛔ No mood beep. The SoundBank buzzers (pharmee-instruct / -greet / -celebrate / -warn)
        // predate voice-over; on top of the real clips they read as an error tone on every
        // sentence. The keys stay in the SoundBank; nothing at runtime plays them.
    }

    private string WarnLineFor(LabErrorType type)
    {
        switch (type)
        {
            case LabErrorType.WrongReagent: return Variant(lines.wrongReagent, PharmeeLines.WrongReagent);
            case LabErrorType.WrongStep: return Variant(lines.wrongStep, PharmeeLines.WrongStep);
            case LabErrorType.Overheat: return Variant(lines.overheat, PharmeeLines.Overheat);
            default: return Variant(lines.safety, PharmeeLines.Safety); // fire, PPE, fume hood, contact, hazardous, dropped glass
        }
    }

    /// The spoken instruction for a step: its hint, or a fallback from the label.
    /// Two-line watch hints are "fact\n→ action" (the verb contract):
    /// Pharmee GUIDES, so he speaks only the ACTION — short and imperative, what
    /// to do right now — while the fact line stays on the wrist panel to read.
    /// Speaking the whole hint had him slowly typing out a paragraph the player
    /// was already reading on their wrist.
    public static string InstructionFor(ExperimentTask task)
    {
        if (task == null) return "";
        if (string.IsNullOrWhiteSpace(task.hint)) return "Next: " + task.label;
        int arrow = task.hint.IndexOf('→');
        if (arrow >= 0 && arrow + 1 < task.hint.Length)
        {
            string act = task.hint.Substring(arrow + 1).Trim();
            if (act.Length > 0) return act;
        }
        return task.hint;
    }
}
