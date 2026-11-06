using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// End-of-experiment grade screen. Populated from an ExperimentResult: overall
/// grade %, mistakes, time, per-criteria breakdown, and PASSED / TRY AGAIN based
/// on the two-part gate. Retry/Continue are wired via UnityEvents.
public class GradeScreenController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text gradeText;
    [SerializeField] private TMP_Text mistakesText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private TMP_Text breakdownText;
    [SerializeField] private GameObject passedVisuals;   // confetti + happy Pharmee
    [SerializeField] private GameObject failedVisuals;
    [SerializeField] private GameObject continueButton;  // hidden until passed
    [SerializeField] private GameObject backButton;      // shown only on FAIL (choose another experiment)

    [Header("Events")]
    public UnityEvent onRetry;
    public UnityEvent onContinue;
    public UnityEvent onBackToEntrance;                   // fail-path exit

    [Header("Auto-show")]
    [SerializeField] private ExperimentRunner runner;

    private void OnEnable()
    {
        if (root != null) root.SetActive(false);
        if (runner != null) runner.ExperimentFinished += Show;
    }

    private void OnDisable()
    {
        if (runner != null) runner.ExperimentFinished -= Show;
    }

    public void SetRunner(ExperimentRunner r)
    {
        if (runner != null) runner.ExperimentFinished -= Show;
        runner = r;
        if (runner != null && isActiveAndEnabled) runner.ExperimentFinished += Show;
    }

    private Coroutine _pendingShow;

    /// Subscribe this to ExperimentRunner.ExperimentFinished. In Play mode it waits
    /// one frame (the outro starts off the same event) and then for any cinematic
    /// outro to end — the slides put "Mission Complete" AFTER the cutscene. Hide()
    /// cancels a pending show (the starvation restart hides it in the same breath).
    public void Show(ExperimentResult r)
    {
        if (Application.isPlaying && isActiveAndEnabled)
        {
            if (_pendingShow != null) StopCoroutine(_pendingShow);
            _pendingShow = StartCoroutine(ShowAfterCinematic(r));
            return;
        }
        ShowNow(r);
    }

    private System.Collections.IEnumerator ShowAfterCinematic(ExperimentResult r)
    {
        yield return null;
        var d = FindAnyObjectByType<CutsceneDirector>();
        while (d != null && d.PlayingCinematic) yield return null;
        _pendingShow = null;
        ShowNow(r);
    }

    public void ShowNow(ExperimentResult r)
    {
        if (root != null) root.SetActive(true);
        // FLOOR the gate-relevant numbers: RoundToInt showed "90%" beside
        // TRY AGAIN when the raw Total was 89.5-89.99 (the gate compares raw).
        if (gradeText != null) gradeText.text = GradeDisplay.Percent(r.grade.Total) + "%";
        if (mistakesText != null) mistakesText.text = r.mistakeCount.ToString();
        if (timeText != null) timeText.text = ExperimentHudController.FormatTime(r.elapsedSeconds);
        if (resultText != null) resultText.text = r.passed ? "PASSED" : "TRY AGAIN";
        if (breakdownText != null) breakdownText.text = BuildBreakdown(r);
        if (passedVisuals != null) passedVisuals.SetActive(r.passed);
        if (failedVisuals != null) failedVisuals.SetActive(!r.passed);
        if (continueButton != null) continueButton.SetActive(r.passed); // gate: can't advance until passed
        if (backButton != null) backButton.SetActive(!r.passed);        // fail: offer "choose another"
        if (AudioService.Instance != null) AudioService.Instance.Play(r.passed ? "grade-pass" : "grade-fail");
        // Confetti burst over the panel on a pass (VFX set).
        if (r.passed && Application.isPlaying)
        {
            var origin = (passedVisuals != null ? passedVisuals.transform
                        : (root != null ? root.transform : transform));
            EffectVfx.Confetti(origin.position + Vector3.up * 0.2f);
        }
    }

    /// The PRACTICE ending: same panel, no score.
    ///
    /// Floating text plus an immediate teleport home is easy to miss entirely; a panel
    /// that waits for the player holds attention. It deliberately shows NO percentage:
    /// a score would turn practice back into an exam, which the mode exists to avoid.
    ///
    /// `onDone` runs when the player dismisses it, so the caller keeps ownership of
    /// what "finished" means rather than this panel reaching into the flow.
    public void ShowPractice(int stepsDone, int corrections, System.Action onDone)
    {
        if (_pendingShow != null) { StopCoroutine(_pendingShow); _pendingShow = null; }   // practice replaces the graded card
        if (root != null) root.SetActive(true);
        if (gradeText != null) gradeText.text = stepsDone.ToString();
        if (mistakesText != null) mistakesText.text = corrections.ToString();
        if (timeText != null) timeText.text = "—";              // practice runs are untimed
        if (resultText != null) resultText.text = "PRACTICE COMPLETE";
        if (breakdownText != null) breakdownText.text = TutorialCoach.SummaryText(stepsDone, corrections);
        // No pass/fail theatre: nothing was judged, so celebrating or commiserating
        // would both be lying.
        if (passedVisuals != null) passedVisuals.SetActive(false);
        if (failedVisuals != null) failedVisuals.SetActive(false);
        if (backButton != null) backButton.SetActive(false);
        if (continueButton != null) continueButton.SetActive(true);
        _practiceDone = onDone;
    }

    private System.Action _practiceDone;

    public void Hide()
    {
        if (_pendingShow != null) { StopCoroutine(_pendingShow); _pendingShow = null; }
        if (root != null) root.SetActive(false);
    }

    public void OnRetryPressed() { Hide(); onRetry?.Invoke(); }
    public void OnContinuePressed()
    {
        Hide();
        // A practice run owns its own exit — firing onContinue as well would ALSO run
        // the campaign's unlock/advance chain, which must never happen ungraded.
        if (_practiceDone != null) { var done = _practiceDone; _practiceDone = null; done(); return; }
        onContinue?.Invoke();
    }
    public void OnBackPressed() { Hide(); onBackToEntrance?.Invoke(); }   // fail-path exit

    /// Builder seam: lets Build Review Corner wire the fail-path button.
    public void SetBackButton(GameObject b) => backButton = b;

    /// Per-criteria lines + the gate reason (why passed / what fell short).
    public static string BuildBreakdown(ExperimentResult r)
    {
        var b = r.grade;
        var sb = new StringBuilder(256);
        sb.Append("Procedure: ").Append(Mathf.RoundToInt(b.Procedure)).Append('\n');
        sb.Append("Chemical Tests: ").Append(Mathf.RoundToInt(b.ChemicalTests)).Append('\n');
        sb.Append("Materials & PPE: ").Append(Mathf.RoundToInt(b.MaterialsAndPPE)).Append('\n');
        sb.Append("Time Management: ").Append(Mathf.RoundToInt(b.TimeManagement)).Append('\n');
        sb.Append("Sanitation: ").Append(Mathf.RoundToInt(b.Sanitation)).Append('\n');
        sb.Append("Documentation: ").Append(Mathf.RoundToInt(b.Documentation)).Append('\n');
        // The quiz score, plainly — the quiz is never gated, so
        // this is what you weigh when choosing Retry (to perfect it) vs Complete.
        sb.Append("Quiz: ").Append(Mathf.RoundToInt(r.quizScore01 * 100f)).Append("%")
          .Append(r.quizScore01 >= 0.999f ? "  <color=#5FD08A>perfect!</color>" : "").Append('\n');
        sb.Append("Mastery: ").Append(GradeDisplay.MasteryPercent(r.overallMastery)).Append('%');   // floored — gate-relevant
        if (!r.passed)
        {
            if (!r.gradePassed) sb.Append("\n<color=#FF7070>Grade below the pass mark.</color>");
            if (!r.masteryPassed) sb.Append("\n<color=#FF7070>Mastery not yet demonstrated — keep practicing.</color>");
        }
        return sb.ToString();
    }
}
