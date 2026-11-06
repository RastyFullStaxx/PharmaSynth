using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// Plays data-driven cutscenes at the right narrative moments by subscribing to
/// the ExperimentRunner: Intro when the experiment starts, ReagentPrep when that
/// phase completes, and Success/Failure when it finishes. Beats are delivered as
/// Pharmee subtitles + face expressions. A cutscene whose beats carry shots plays as
/// a CINEMATIC: fade black -> CinemaView (shot camera onto a screen in a dark room,
/// clock paused, locomotion locked) -> fade black -> back in control. The XR camera
/// is never animated.
///
/// The end-cutscene ALWAYS plays (success OR failure variant) — a user requirement.
public class CutsceneDirector : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private NPCNarrationController narration;
    [SerializeField] private MonoBehaviour faceBehaviour; // optional IPharmeeFace
    [SerializeField] private CinemaView cinema;           // found in the scene if unset
    [SerializeField] private float cinemaFadeSeconds = 0.5f;

    [Header("Cutscenes")]
    [Tooltip("Optional: moduleId→cutscene-set lookup. When set, the director swaps its four cutscenes to match each experiment as it starts. Falls back to the fields below when a module has no entry.")]
    [SerializeField] private CutsceneLibrary library;
    [SerializeField] private CutsceneData intro;
    [SerializeField] private CutsceneData reagentPrep;
    [SerializeField] private CutsceneData success;
    [SerializeField] private CutsceneData failure;

    // Test/inspection accessors.
    public CutsceneData Intro => intro;
    public CutsceneData ReagentPrep => reagentPrep;
    public CutsceneData Success => success;
    public CutsceneData Failure => failure;
    public void SetLibrary(CutsceneLibrary lib) => library = lib;

    public UnityEvent onCutsceneStarted;
    public UnityEvent onCutsceneFinished;

    private IPharmeeFace _face;
    private Coroutine _routine;
    private bool _subscribed;

    public bool IsPlaying { get; private set; }
    /// True while a cinematic (not a subtitle-only cutscene) runs — the grade screen
    /// waits on this so "Mission Complete" lands after the outro, as in the slides.
    public bool PlayingCinematic { get; private set; }

    [SerializeField] private float titleSeconds = 2.2f;

    /// Pure (suite-pinned): the title card a cinematic opens on, by kind.
    public static (string title, string line) CardFor(CutsceneData.Kind kind, string experimentTitle = null)
    {
        switch (kind)
        {
            case CutsceneData.Kind.Intro:
                return string.IsNullOrEmpty(experimentTitle) ? ("", "") : (experimentTitle, "Today's experiment");
            case CutsceneData.Kind.ReagentPrep: return ("Preparations Complete", "Proceed to the experiment");
            case CutsceneData.Kind.Success: return ("Experiment Complete", "You have successfully completed the experiment!");
            case CutsceneData.Kind.Failure: return ("Experiment Over", "Let's look at what happened.");
            default: return ("", "");
        }
    }

    public void SetRunner(ExperimentRunner r)
    {
        Unsubscribe();
        runner = r;
        _face = faceBehaviour as IPharmeeFace;
        Subscribe();
    }

    private void OnEnable() { _face = faceBehaviour as IPharmeeFace; Subscribe(); }
    private void OnDisable() { Unsubscribe(); EndCinema(); }

    /// A HUD Restart mid-cutscene must never leave the player stuck in the theatre.
    private void OnAborted() { if (IsPlaying) Skip(); }

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnStarted;
        runner.PhaseCompleted += OnPhaseCompleted;
        runner.ExperimentFinished += OnFinished;
        runner.Aborted += OnAborted;
        _subscribed = true;
    }
    private void Unsubscribe()
    {
        if (!_subscribed || runner == null) return;
        runner.ExperimentStarted -= OnStarted;
        runner.PhaseCompleted -= OnPhaseCompleted;
        runner.ExperimentFinished -= OnFinished;
        runner.Aborted -= OnAborted;
        _subscribed = false;
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        if (m != null) LoadForModule(m.moduleId);
        Play(intro);
    }

    /// Swap the four cutscenes to the module's set from the library (if present).
    /// Returns true when a complete set was found. Kept public for edit-mode tests.
    public bool LoadForModule(string moduleId)
    {
        if (library == null) return false;
        var set = library.GetSet(moduleId);
        if (set == null) return false;
        if (set.intro != null) intro = set.intro;
        if (set.reagentPrep != null) reagentPrep = set.reagentPrep;
        if (set.success != null) success = set.success;
        if (set.failure != null) failure = set.failure;
        return set.IsComplete;
    }
    private void OnPhaseCompleted(TaskPhase p) { if (p == TaskPhase.ReagentPrep) Play(reagentPrep); }

    private bool _skipNextOutro;

    /// A supply-starvation restart records the attempt via Finish(0) but
    /// must NOT play the failure outro over the restart fade — call this right
    /// before that Finish. One-shot; the next finish plays normally.
    public void SkipNextOutro() => _skipNextOutro = true;

    private void OnFinished(ExperimentResult r)
    {
        if (_skipNextOutro) { _skipNextOutro = false; return; }
        Play(SelectOutro(r));
    }

    /// The end cutscene always resolves to something (success or failure variant).
    public CutsceneData SelectOutro(ExperimentResult r) => r.passed ? success : failure;

    public void Play(CutsceneData data)
    {
        if (data == null || data.beats == null || data.beats.Count == 0) return;
        if (_routine != null) StopCoroutine(_routine);
        if (isActiveAndEnabled) _routine = StartCoroutine(PlayRoutine(data));
    }

    private IEnumerator PlayRoutine(CutsceneData data)
    {
        IsPlaying = true;
        onCutsceneStarted?.Invoke();
        bool cine = Application.isPlaying && data.IsCinematic && FindCinema() != null && Camera.main != null;
        PlayingCinematic = cine;
        if (cine)
        {
            yield return Fade(1f);
            BeginCinema();
            var card = CardFor(data.kind, data.title);
            cinema.SetTitle(card.title, card.line);
            yield return Fade(0f);
            if (!string.IsNullOrEmpty(card.title))
            {
                yield return new WaitForSeconds(titleSeconds);
                cinema.SetTitle("", "");
            }
        }
        for (int i = 0; i < data.beats.Count; i++)
        {
            var b = data.beats[i];
            if (b == null) continue;
            _face?.SetExpression(b.face);
            if (narration != null) narration.Say(b.subtitle, b.seconds);
            float dur = Mathf.Max(0.2f, b.seconds);
            if (!cine) { yield return new WaitForSeconds(dur); continue; }

            var shot = b.shot ?? new CinemaShot();
            if (shot.pharmeePopIn) PopIn();
            ShotMath.Ends(shot.blend, CurrentShotPose(), cinema.MarkerPose(shot.camera),
                          cinema.MarkerPose(shot.moveTo), out var start, out var end);
            float fov0 = cinema.ShotCamera.fieldOfView, fov1 = shot.fov;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                Vector3? look = cinema.Resolve(shot.look, shot.lookMarker, ModuleId, transform);
                cinema.SetPose(ShotMath.Evaluate(start, end, shot.blend, look, k),
                               shot.blend ? Mathf.Lerp(fov0, fov1, ShotMath.Ease(k)) : fov1);
                TickPop();
                yield return null;
            }
        }
        if (cine)
        {
            yield return Fade(1f);
            EndCinema();
            yield return Fade(0f);
        }
        IsPlaying = false;
        PlayingCinematic = false;
        onCutsceneFinished?.Invoke();
        _routine = null;
    }

    // ── Cinema helpers ─────────────────────────────────────────────────────

    private string ModuleId => runner != null && runner.Module != null ? runner.Module.moduleId : null;

    private CinemaView FindCinema()
    {
        if (cinema == null) cinema = FindAnyObjectByType<CinemaView>();
        return cinema;
    }

    private ShotMath.Pose CurrentShotPose()
    {
        var t = cinema.ShotCamera.transform;
        return new ShotMath.Pose(t.position, t.rotation);
    }

    /// Fade to `alpha` and wait (bounded, so a lost fader callback can't hang a cutscene).
    private IEnumerator Fade(float alpha)
    {
        var f = ScreenFader.Instance;
        if (f == null || !f.isActiveAndEnabled) yield break;
        bool done = false;
        if (alpha > 0.5f) f.FadeOut(cinemaFadeSeconds, () => done = true);
        else f.FadeIn(cinemaFadeSeconds, () => done = true);
        float limit = cinemaFadeSeconds + 2f;
        for (float t = 0f; !done && t < limit; t += Time.unscaledDeltaTime) yield return null;
    }

    private void BeginCinema()
    {
        if (runner != null) runner.PauseClock(true);
        if (cinema.MarkerPose("Cam_LabWide") is ShotMath.Pose wide) cinema.SetPose(wide, 50f);
        cinema.Enter(Camera.main);
        if (narration != null) { narration.LineStarted += OnCinemaLine; narration.LineEnded += OnCinemaLineEnd; }
    }

    private void EndCinema()
    {
        if (narration != null) { narration.LineStarted -= OnCinemaLine; narration.LineEnded -= OnCinemaLineEnd; }
        EndPop();
        if (cinema != null && cinema.Active) cinema.Exit();
        if (runner != null) runner.PauseClock(false);
    }

    private void OnCinemaLine(string line, float _) { if (cinema != null) cinema.SetSubtitle(line); }
    private void OnCinemaLineEnd() { if (cinema != null) cinema.SetSubtitle(""); }

    // Pharmee "pops off the corner of the laboratory": jump to the corner marker and
    // scale up with a little overshoot; everything is put back when the cutscene ends.
    private bool _popped;
    private float _popT;
    private Vector3 _savedPos, _savedScale, _savedHome;
    private bool _savedHomeValid, _moverWasOn;

    public const float PopSeconds = 0.45f;

    /// Pure (suite-pinned): pop scale 0 -> slight overshoot -> 1 (easeOutBack).
    public static float PopScale(float t01)
    {
        t01 = Mathf.Clamp01(t01);
        const float s = 1.70158f;
        float u = t01 - 1f;
        return 1f + (s + 1f) * u * u * u + s * u * u;
    }

    private void PopIn()
    {
        var corner = cinema != null ? cinema.Marker("Marker_PharmeeCorner") : null;
        if (corner == null || _popped) return;
        _popped = true; _popT = 0f;
        _savedPos = transform.position; _savedScale = transform.localScale;
        var mover = GetComponent<PharmeeMover>();
        _moverWasOn = mover != null && mover.enabled;
        if (mover != null) mover.enabled = false;
        var bob = GetComponent<FloatBob>();
        _savedHomeValid = bob != null;
        if (bob != null)
        {
            _savedHome = bob.Home;
            bob.SetHome(transform.parent != null ? transform.parent.InverseTransformPoint(corner.position) : corner.position);
        }
        transform.position = corner.position;
        transform.localScale = _savedScale * 0.01f;
    }

    private void TickPop()
    {
        if (!_popped || _popT >= PopSeconds) return;
        _popT += Time.deltaTime;
        transform.localScale = _savedScale * Mathf.Max(0.01f, PopScale(_popT / PopSeconds));
    }

    private void EndPop()
    {
        if (!_popped) return;
        _popped = false;
        transform.localScale = _savedScale;
        var bob = GetComponent<FloatBob>();
        if (bob != null && _savedHomeValid) bob.SetHome(_savedHome);
        transform.position = _savedPos;
        var mover = GetComponent<PharmeeMover>();
        if (mover != null && _moverWasOn) mover.enabled = true;
    }

    public void Skip()
    {
        if (_routine != null) StopCoroutine(_routine);
        if (narration != null) narration.SkipNarration();
        bool wasCine = cinema != null && cinema.Active;
        PlayingCinematic = false;
        EndCinema();
        if (wasCine && ScreenFader.Instance != null && ScreenFader.Instance.State.Alpha > 0.01f)
            ScreenFader.Instance.FadeIn(cinemaFadeSeconds);
        IsPlaying = false;
        _routine = null;
        onCutsceneFinished?.Invoke();
    }
}
