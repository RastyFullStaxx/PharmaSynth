using System.Collections;
using UnityEngine;

/// Pure timelapse maths (suite-pinned): day/night tint and the on-screen counter.
public static class TimelapseMath
{
    public static readonly Color Night = new Color(0.22f, 0.28f, 0.55f);

    /// Game-time speed during the timelapse shot. Everything that runs on scaled time
    /// (Pharmee, Dr. Jimenez's walk, physics, liquids) visibly hurries; the lab clock is
    /// paused for the shot, so grading never sees it.
    public const float DefaultSpeed = 6f;

    /// Cycles of day and night the picture runs through: one per day, capped so a
    /// month-long wait doesn't strobe. Under a day: none (a 15-minute wait stays lit).
    public static int Cycles(float hours) => hours >= 24f ? Mathf.Clamp(Mathf.RoundToInt(hours / 24f), 1, 7) : 0;

    /// Screen tint at progress k: white at each dawn, Night at each midnight.
    public static Color Tint(float k, float hours)
    {
        int n = Cycles(hours);
        if (n == 0) return Color.white;
        float phase = Mathf.Repeat(Mathf.Clamp01(k) * n, 1f);
        if (k >= 1f) phase = 0f;
        float dark = (1f - Mathf.Cos(phase * 2f * Mathf.PI)) * 0.5f;
        return Color.Lerp(Color.white, Night, dark);
    }

    public static bool IsNight(float k, float hours)
    {
        int n = Cycles(hours);
        if (n == 0 || k >= 1f) return false;
        float phase = Mathf.Repeat(Mathf.Clamp01(k) * n, 1f);
        return phase > 0.25f && phase < 0.75f;
    }

    /// "DAY 3 OF 7 · NIGHT", "0:07 OF 0:15", or just the fast-forward label when the
    /// procedure states no duration.
    public static string Counter(float k, float hours)
    {
        k = Mathf.Clamp01(k);
        const string ff = ">> FAST FORWARD";
        if (hours >= 24f)
        {
            int days = Mathf.Max(1, Mathf.RoundToInt(hours / 24f));
            int day = Mathf.Clamp(1 + Mathf.FloorToInt(k * days), 1, days);
            return $"DAY {day} OF {days}  ·  {(IsNight(k, hours) ? "NIGHT" : "DAY")}   {ff}";
        }
        if (hours > 0f)
        {
            int total = Mathf.RoundToInt(hours * 60f), now = Mathf.RoundToInt(k * total);
            return $"{now / 60}:{now % 60:00} OF {total / 60}:{total % 60:00}   {ff}";
        }
        return ff;
    }
}

/// Compresses a real-world wait the player can't sit through (once the
/// mixture requirement is met, the screen fades black for 2 s, and
/// when it returns a success text says the time has passed and the step is done;
/// this covers every procedure in every experiment that needs lengthy time).
///
/// A task authored longProcess=true plays a TIMELAPSE on completion: fade black, a
/// security-camera view of the lab from its top corner (Cam_Timelapse) running through
/// days and nights with a counter, fade black, then back to play with a "time has
/// passed" message. Without the cinema rig it falls back to a plain black hold. Zone-free and
/// experiment-agnostic: it keys off the task flag, so any module's week-long
/// fermentation / overnight dry / hour-long crystallisation reuses it for free.
public class TimeSkipController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private float holdSeconds = 2f;
    [SerializeField] private float fadeSeconds = 0.6f;
    [Tooltip("Length of the corner-camera timelapse (when the scene has a CinemaView with Cam_Timelapse).")]
    [SerializeField] private float timelapseSeconds = 6f;
    [Tooltip("How far the timelapse camera creeps toward the bench over the clip (0-1).")]
    [SerializeField] private float timelapsePush = 0.12f;
    [Tooltip("Game-time speed while the timelapse plays. 1 = no speed-up.")]
    [SerializeField] private float timelapseSpeed = TimelapseMath.DefaultSpeed;
    private float _savedTimeScale = -1f;
    private bool _subscribed;
    private Coroutine _timelapse;
    private CinemaView _cinema;

    /// Pure (suite-pinned): does this task trigger a time skip, and what does it say?
    public static bool IsTimeSkip(ExperimentTask t) => t != null && t.longProcess;
    public static string MessageFor(ExperimentTask t)
    {
        if (t == null) return "";
        return string.IsNullOrWhiteSpace(t.longProcessMessage)
            ? "Time passes… the process is complete."
            : t.longProcessMessage.Trim();
    }

    private void Awake() { if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>(); Subscribe(); }
    private void OnEnable() => Subscribe();
    private void OnDisable() { RestoreTimeScale(); Unsubscribe(); }

    private void SpeedUp()
    {
        if (_savedTimeScale < 0f) _savedTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Max(1f, timelapseSpeed);
    }

    private void RestoreTimeScale()
    {
        if (_savedTimeScale < 0f) return;
        Time.timeScale = _savedTimeScale;
        _savedTimeScale = -1f;
    }

    /// Builder/test seam.
    public void Bind(ExperimentRunner r) { Unsubscribe(); runner = r; Subscribe(); }

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.TaskCompleted += OnTaskCompleted;
        runner.Aborted += OnAborted;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (runner != null) { runner.TaskCompleted -= OnTaskCompleted; runner.Aborted -= OnAborted; }
        _subscribed = false;
        EndTimelapse();
    }

    /// True while the screen is black for a long-process skip. Read by Tutorial Mode's
    /// highlighter, which must drop every glow for the duration — a lit bottle floating
    /// on a black screen reads as a rendering bug, not a hint.
    public static bool IsSkipping { get; private set; }

    private void OnTaskCompleted(ExperimentTask t)
    {
        if (!IsTimeSkip(t) || !Application.isPlaying) return;
        string msg = MessageFor(t);

        // The timelapse: a security-camera view from the lab's top corner, the lab
        // running through days and nights, then back to play. Needs the cinema rig.
        if (_cinema == null) _cinema = FindAnyObjectByType<CinemaView>();
        if (_cinema != null && _cinema.Marker("Cam_Timelapse") != null && Camera.main != null)
        {
            PlayTimelapse(t);
            return;
        }

        if (ScreenFader.Instance != null && ScreenFader.Instance.isActiveAndEnabled)
        {
            // Fade to black, HOLD the darkness, then fade back and announce.
            IsSkipping = true;
            ScreenFader.Instance.FadeOut(fadeSeconds, () =>
                Invoke(nameof(ReturnFromSkip), Mathf.Max(0f, holdSeconds)));
            _pendingMessage = msg;
        }
        else
        {
            // No fader (tests / menus): just announce, so the beat isn't lost.
            Announce(msg);
        }
    }

    /// Public for the Preview menu. Waits for any cinematic cutscene first.
    public void PlayTimelapse(ExperimentTask t)
    {
        if (_cinema == null) _cinema = FindAnyObjectByType<CinemaView>();
        if (_cinema == null || !isActiveAndEnabled) return;
        if (_timelapse != null) StopCoroutine(_timelapse);
        _timelapse = StartCoroutine(TimelapseRoutine(t));
    }

    private IEnumerator TimelapseRoutine(ExperimentTask t)
    {
        IsSkipping = true;
        var director = FindAnyObjectByType<CutsceneDirector>();
        while (director != null && director.PlayingCinematic) yield return null;
        if (runner != null) runner.PauseClock(true);

        yield return Fade(1f);
        var cam = _cinema.Marker("Cam_Timelapse");
        var bench = _cinema.Marker("Marker_Bench");
        Vector3 look = bench != null ? bench.position : cam.position + cam.forward;
        var start = new ShotMath.Pose(cam.position, cam.rotation);
        var end = new ShotMath.Pose(Vector3.Lerp(cam.position, look, timelapsePush), cam.rotation);
        _cinema.SetPose(ShotMath.Evaluate(start, end, false, look, 0f), 55f);
        _cinema.Enter(Camera.main);
        _cinema.SetSubtitle(GlyphSafe.Sanitize(TimelapseMath.Counter(0f, t.timelapseHours)));
        yield return Fade(0f);

        // Real seconds, so the shot lasts timelapseSeconds however fast the lab runs.
        SpeedUp();
        for (float s = 0f; s < timelapseSeconds; s += Time.unscaledDeltaTime)
        {
            float k = s / timelapseSeconds;
            _cinema.SetPose(ShotMath.Evaluate(start, end, false, look, k), 55f);
            _cinema.SetTint(TimelapseMath.Tint(k, t.timelapseHours));
            _cinema.SetSubtitle(GlyphSafe.Sanitize(TimelapseMath.Counter(k, t.timelapseHours)));
            yield return null;
        }
        RestoreTimeScale();
        _cinema.SetTint(Color.white);
        _cinema.SetSubtitle(GlyphSafe.Sanitize(TimelapseMath.Counter(1f, t.timelapseHours)));

        yield return Fade(1f);
        EndTimelapse();
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(fadeSeconds);
        Announce(MessageFor(t));
    }

    private void OnAborted()
    {
        if (_timelapse == null) return;
        EndTimelapse();
        if (ScreenFader.Instance != null && ScreenFader.Instance.State.Alpha > 0.01f) ScreenFader.Instance.FadeIn(fadeSeconds);
    }

    private void EndTimelapse()
    {
        if (_timelapse != null) { StopCoroutine(_timelapse); _timelapse = null; }
        RestoreTimeScale();
        if (_cinema != null && _cinema.Active) _cinema.Exit();
        if (IsSkipping && runner != null) runner.PauseClock(false);
        IsSkipping = false;
    }

    private IEnumerator Fade(float alpha)
    {
        var f = ScreenFader.Instance;
        if (f == null || !f.isActiveAndEnabled) yield break;
        bool done = false;
        if (alpha > 0.5f) f.FadeOut(fadeSeconds, () => done = true);
        else f.FadeIn(fadeSeconds, () => done = true);
        for (float s = 0f; !done && s < fadeSeconds + 2f; s += Time.unscaledDeltaTime) yield return null;
    }

    private string _pendingMessage;
    private void ReturnFromSkip()
    {
        // Cleared as the light comes back rather than after the fade completes, so
        // guidance is already on screen by the time the player can see anything.
        IsSkipping = false;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(fadeSeconds);
        Announce(_pendingMessage);
        _pendingMessage = null;
    }

    private void Announce(string msg)
    {
        if (string.IsNullOrEmpty(msg)) return;
        Vector3 pos = Camera.main != null
            ? Camera.main.transform.position + Camera.main.transform.forward * 1.4f
            : transform.position + Vector3.up * 1.5f;
        FloatingText.Show(msg, pos, new Color(0.7f, 1f, 0.8f), 1.3f);
        var narr = FindAnyObjectByType<NPCNarrationController>();
        if (narr != null) narr.Say(msg, 3.5f);
    }
}
