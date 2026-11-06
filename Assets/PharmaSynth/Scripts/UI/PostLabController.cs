using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// The post-lab "Documentation" phase (manual's Data Sheet + quiz), shown on a
/// world-space tablet once the Chemical Tests phase is complete. The player enters
/// the module's multiple-choice questions; submitting
/// completes the terminal data-sheet task and ends the attempt with the quiz score
/// feeding the grader's Documentation criterion.
///
/// All UI refs are optional so the open→answer→submit→finish logic is unit-testable
/// headlessly (a scene-built canvas drives the same public methods).
public class PostLabController : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private QuizBankLibrary library;

    [Header("UI (optional — logic works without them)")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text questionCounterText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private Button[] optionButtons = new Button[0];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[0];
    [SerializeField] private TMP_Text explanationText;
    [SerializeField] private Button submitButton;

    [Tooltip("Open automatically when ChemicalTests completes. The gatekeeper's review flow sets this false and opens the quiz itself after Jimenez's briefing.")]
    [SerializeField] private bool autoOpen = true;

    [Header("Review navigation")]
    [Tooltip("Step back/forward through the questions to review answers before submitting.")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    /// Tint of the option you already picked. Unpicked options are restored to their
    /// AUTHORED colour — never hard-set to white, which erased the dark panel styling
    /// and made the white label text unreadable.
    private static readonly Color SelectedTint = new Color(0.24f, 0.62f, 0.92f);
    private Color[] _optionBaseColors;

    [Tooltip("Optional label for the score / prompt feedback on the tablet.")]
    [SerializeField] private TMP_Text feedbackText;

    private QuizBank _bank;
    private int[] _answers;      // chosen option per question, -1 = unanswered
    private int _current;
    private bool _open;

    public bool IsOpen => _open;
    public QuizBank Bank => _bank;
    public int CurrentIndex => _current;

    public void SetRefs(ExperimentRunner r, QuizBankLibrary lib) { runner = r; library = lib; }

    private void OnEnable()  { if (runner != null) runner.PhaseCompleted += OnPhaseCompleted; }
    private void OnDisable() { if (runner != null) runner.PhaseCompleted -= OnPhaseCompleted; }

    private void OnPhaseCompleted(TaskPhase phase)
    {
        // Chemical Tests done → time to document. (Modules whose last phase is
        // ChemicalTests still open here; Submit simply finishes.)
        if (autoOpen && phase == TaskPhase.ChemicalTests) Open();
    }

    /// Open the quiz for the currently running module.
    public void Open()
    {
        var moduleId = runner != null && runner.Module != null ? runner.Module.moduleId : null;
        var bank = library != null ? library.GetBank(moduleId) : null;
        if (bank == null && !string.IsNullOrEmpty(moduleId))
            Debug.LogWarning("[PostLab] no quiz bank for '" + moduleId + "' — Documentation defaults to full credit.");
        // Freeze the clock on EVERY quiz path, not just the gatekeeper's, so auto-open/dev
        // flows never let quiz time bleed into the Time-Management score. Idempotent; the
        // next attempt unfreezes.
        if (runner != null) runner.FreezeClock();
        OpenFor(bank);
    }

    /// Open for a specific bank (bank may be null → an acknowledge-only data sheet).
    public void OpenFor(QuizBank bank)
    {
        _bank = bank;
        int n = bank != null ? bank.Count : 0;
        _answers = new int[n];
        for (int i = 0; i < n; i++) _answers[i] = -1;
        _current = 0;
        _open = true;
        if (root != null) root.SetActive(true);
        if (titleText != null) titleText.text = "Data Sheet & Documentation";
        Render();
        // Cue the tablet's appearance, played 3D AT the tablet so it also tells the player
        // WHERE to look: the review teleport drops them facing it, but it is easy to miss
        // appearing.
        AudioService.TryPlayFirstAt(TabletPosition(), 1f, "ui-confirm", "socket-snap", "ui-click");
    }

    /// Where the tablet actually is, for positional audio: the visible root when
    /// there is one (the panel the player reads), else this component.
    private Vector3 TabletPosition()
        => root != null ? root.transform.position : transform.position;

    /// Hooked to each option button (index passed by the button wiring).
    public void OnOptionSelected(int optionIndex)
    {
        if (!_open || _bank == null || _current < 0 || _current >= _bank.Count) return;
        bool wasUnanswered = _answers != null && _answers[_current] < 0;
        AnswerCurrent(optionIndex);
        if (explanationText != null)
        {
            var q = _bank.questions[_current];
            explanationText.text = q.explanation;
        }
        // First pass: auto-advance so answering flows. REVIEWING an already-answered
        // question: stay put so the choice can be compared/changed without the page
        // jumping away (so answers can be reviewed before submitting).
        if (wasUnanswered && _current < _bank.Count - 1) _current++;
        Render();
    }

    // ---- review navigation --------------------------------

    /// Pure (suite): is there a previous / next question from here?
    public static bool CanGoBack(int index) => index > 0;
    public static bool CanGoNext(int index, int count) => index < count - 1;

    /// Back / Next buttons — step through the questions to review answers.
    public void PreviousQuestion()
    {
        if (!_open || !CanGoBack(_current)) return;
        _current--;
        Render();
    }

    public void NextQuestion()
    {
        if (!_open || _bank == null || !CanGoNext(_current, _bank.Count)) return;
        _current++;
        Render();
    }

    /// Record an answer for the current question and advance the cursor no further.
    public void AnswerCurrent(int optionIndex) { Answer(_current, optionIndex); }

    public void Answer(int questionIndex, int optionIndex)
    {
        if (_answers == null || questionIndex < 0 || questionIndex >= _answers.Length) return;
        _answers[questionIndex] = optionIndex;
        if (submitButton != null) submitButton.gameObject.SetActive(AllAnswered);
    }

    public bool AllAnswered
    {
        get
        {
            if (_answers == null) return true;               // no questions → nothing to answer
            for (int i = 0; i < _answers.Length; i++) if (_answers[i] < 0) return false;
            return true;
        }
    }

    /// Fraction correct (0..1) — drives the grader's Documentation sub-score.
    /// A present-but-EMPTY bank counts like a missing one (full credit), not as a
    /// Documentation 0.
    public float ScoreFraction() => _bank != null && _bank.Count > 0 ? _bank.Score(_answers) : 1f;

    /// Close without submitting (HUD Restart / fail-abandon mid-quiz used
    /// to leave the tablet floating). The attempt is NOT finished here.
    public void Close()
    {
        bool wasOpen = _open;
        _open = false;
        if (root != null) root.SetActive(false);
        // Opening the tablet had a cue; dismissing it had no counterpart.
        if (wasOpen) AudioService.TryPlay("ui-click");
    }

    /// Every answer correct.
    public bool IsPerfect => ScoreFraction() >= 0.999f;

    /// Correct answers out of the bank's total (for the score display).
    public int CorrectCount => _bank == null ? 0 : Mathf.RoundToInt(ScoreFraction() * _bank.Count);

    /// Pure (suite): the score line shown to the player — "Quiz: 2 / 3 (67%)".
    public static string ScoreLine(int correct, int total)
        => total <= 0 ? "" : $"Quiz: {correct} / {total} ({Mathf.RoundToInt(100f * correct / total)}%)";

    /// Submit button. NEVER score-gated (client rule): any score submits and is
    /// shown plainly on the grade screen, where the player chooses Retry (to
    /// perfect it) or Complete Experiment.
    public void Submit()
    {
        // A refused submit sounded EXACTLY like an accepted one (the button's own
        // click), so the rejection was invisible unless you were reading the text.
        if (!AllAnswered)
        {
            SetFeedback("Answer every question before submitting.");
            AudioService.TryPlay("ui-error");
            return;
        }
        SetFeedback(ScoreLine(CorrectCount, _bank != null ? _bank.Count : 0));
        SubmitAndFinish();
    }

    private void SetFeedback(string s)
    {
        if (feedbackText != null) feedbackText.text = GlyphSafe.Sanitize(s);
        LastFeedback = s;
    }

    /// The last message shown to the player (suite/dev visibility).
    public string LastFeedback { get; private set; } = "";

    /// Finish the attempt: complete the terminal data-sheet task and grade.
    public ExperimentResult SubmitAndFinish()
    {
        // Complete the data-sheet / record task so the graph reaches 100%.
        if (runner != null && runner.Graph != null)
        {
            string recordId = FindDataSheetTaskId(runner.Graph);
            if (!string.IsNullOrEmpty(recordId)) runner.CompleteTask(recordId);
        }
        _open = false;
        if (root != null) root.SetActive(false);
        return runner != null ? runner.Finish(ScoreFraction()) : default;
    }

    /// Snapshot each option button's authored colour ONCE, before anything tints it.
    private void CaptureOptionColors()
    {
        if (_optionBaseColors != null) return;
        _optionBaseColors = new Color[optionButtons.Length];
        for (int i = 0; i < optionButtons.Length; i++)
        {
            var im = optionButtons[i] != null ? optionButtons[i].GetComponent<Image>() : null;
            _optionBaseColors[i] = im != null ? im.color : Color.white;
        }
    }

    private void Render()
    {
        if (_bank == null || _bank.Count == 0)
        {
            if (promptText != null) promptText.text = "Review your observations, then submit your data sheet.";
            if (questionCounterText != null) questionCounterText.text = "";
            for (int i = 0; i < optionButtons.Length; i++)
                if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(false);
            if (submitButton != null) submitButton.gameObject.SetActive(true);
            return;
        }

        var q = _bank.questions[_current];
        if (questionCounterText != null) questionCounterText.text = "Question " + (_current + 1) + " / " + _bank.Count;
        if (promptText != null) promptText.text = q.prompt;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool on = i < q.options.Count;
            if (optionButtons[i] != null) optionButtons[i].gameObject.SetActive(on);
            if (i < optionLabels.Length && optionLabels[i] != null && on) optionLabels[i].text = q.options[i];
            // Show the answer already chosen for THIS question, so stepping back to
            // review makes your previous pick obvious. Unpicked
            // options go back to their AUTHORED colour, captured once.
            if (on && optionButtons[i] != null)
            {
                var img = optionButtons[i].GetComponent<Image>();
                if (img != null)
                {
                    CaptureOptionColors();
                    bool picked = _answers != null && _answers[_current] == i;
                    img.color = picked ? SelectedTint : _optionBaseColors[i];
                }
            }
        }
        // Re-show the explanation of an answered question while reviewing it.
        if (explanationText != null)
            explanationText.text = (_answers != null && _answers[_current] >= 0) ? q.explanation : "";

        if (prevButton != null) prevButton.interactable = CanGoBack(_current);
        if (nextButton != null) nextButton.interactable = CanGoNext(_current, _bank.Count);
        if (submitButton != null) submitButton.gameObject.SetActive(AllAnswered);
    }

    /// The terminal Data Sheet task (record-*) for a graph, or null if none.
    public static string FindDataSheetTaskId(TaskGraph graph)
    {
        if (graph == null) return null;
        foreach (var t in graph.Tasks)
            if (t != null && t.phase == TaskPhase.DataSheet && t.required) return t.taskId;
        return null;
    }
}
