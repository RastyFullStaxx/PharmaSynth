using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Screen/world HUD: module title, count-up timer, progress bar, and
/// task-accomplished toasts. Subscribes to ExperimentRunner — no polling except
/// the once-per-second timer text. The visible progress bar drops on mistakes
/// (storyboard behaviour) while the underlying TaskGraph progress stays clean.
public class ExperimentHudController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;

    [Header("Widgets")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Image progressFill;        // Image type = Filled, 0..1
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private GameObject toastRoot;

    [Header("Tuning")]
    [SerializeField, Min(0f)] private float toastSeconds = 2.5f;
    [SerializeField, Range(0f, 0.5f)] private float mistakeProgressPenalty = 0.05f;

    private float _graphProgress;
    private int _mistakes;
    private float _toastTimer;
    private int _lastTimerSecond = -1;

    private void OnEnable()
    {
        if (runner == null) return;
        runner.ExperimentPrepared += OnStarted;   // armed stage shows a clean, frozen HUD
        runner.ExperimentStarted += OnStarted;
        runner.ProgressChanged += OnProgress;
        runner.TaskCompleted += OnTaskCompleted;
        runner.MistakeRecorded += OnMistake;
    }

    private void OnDisable()
    {
        if (runner == null) return;
        runner.ExperimentPrepared -= OnStarted;
        runner.ExperimentStarted -= OnStarted;
        runner.ProgressChanged -= OnProgress;
        runner.TaskCompleted -= OnTaskCompleted;
        runner.MistakeRecorded -= OnMistake;
    }

    private void Update()
    {
        if (runner != null && runner.IsRunning)
        {
            int sec = Mathf.FloorToInt(runner.ElapsedSeconds);
            if (sec != _lastTimerSecond)
            {
                _lastTimerSecond = sec;
                if (timerText != null) timerText.text = FormatTime(runner.ElapsedSeconds);
            }
        }

        if (_toastTimer > 0f)
        {
            _toastTimer -= Time.deltaTime;
            if (_toastTimer <= 0f && toastRoot != null) toastRoot.SetActive(false);
        }
    }

    private void OnStarted(ExperimentModuleDefinition m)
    {
        _graphProgress = 0f; _mistakes = 0; _lastTimerSecond = -1;
        if (titleText != null) titleText.text = m != null ? m.moduleTitle : "Experiment";
        if (timerText != null)
        {
            // HIDE the clock in practice mode rather than freezing it at 00:00 —
            // a stopped timer on screen just invites "is this broken?".
            timerText.gameObject.SetActive(ExperimentRunner.ClockRuns);
            timerText.text = FormatTime(0f);
        }
        RefreshProgress();
    }

    private void OnProgress(float p) { _graphProgress = p; RefreshProgress(); }

    private void OnTaskCompleted(ExperimentTask t)
    {
        ShowToast("Step complete:  " + (t != null ? t.label : "Task") + "  ✓");
        // A clear success chime with the on-screen toast.
        if (AudioService.Instance != null) AudioService.Instance.Play("grade-pass");
    }

    private void OnMistake(LabErrorType type, string message)
    {
        _mistakes++;
        RefreshProgress();          // visible bar dips
        ShowToast(message);
        // A mistake gets an audible cue to match the task-completed chime: in VR the toast
        // is easy to miss if you are looking at your hands.
        AudioService.TryPlay("ui-error");
        LabHaptics.Mistake();       // a long coarse buzz — unmissable even hands-down
    }

    private void RefreshProgress()
    {
        float shown = DisplayedProgress(_graphProgress, _mistakes, mistakeProgressPenalty);
        if (progressFill != null) progressFill.fillAmount = shown;
        // Percent only — the pill has its own static "Progress" label; the
        // prefixed copy wrapped and overflowed the value rect.
        if (progressText != null) progressText.text = FormatPercent(shown);
    }

    private void ShowToast(string msg)
    {
        if (toastText != null) toastText.text = msg;
        if (toastRoot != null) toastRoot.SetActive(true);
        _toastTimer = toastSeconds;
    }

    // ---- pure, unit-testable helpers ------------------------------------

    /// Visible progress = graph completion minus a per-mistake penalty, clamped.
    public static float DisplayedProgress(float graphProgress01, int mistakes, float penaltyStep)
    {
        float v = graphProgress01 - mistakes * penaltyStep;
        return v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    public static string FormatPercent(float p01) => Mathf.RoundToInt(Mathf.Clamp01(p01) * 100f) + "%";

    public static string FormatTime(float seconds)
    {
        if (seconds < 0f) seconds = 0f;
        int total = Mathf.FloorToInt(seconds);
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;
        var sb = new StringBuilder(8);
        sb.Append(h.ToString("00")).Append(':').Append(m.ToString("00")).Append(':').Append(s.ToString("00"));
        return sb.ToString();
    }
}
