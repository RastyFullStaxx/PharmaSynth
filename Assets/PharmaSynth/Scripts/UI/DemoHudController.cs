using UnityEngine;

/// The demo HUD cluster (Skip Step / Finish Experiment / Auto-Answer Quiz),
/// visible only during a demo session and only when each verb applies. Built by
/// Tools ▸ PharmaSynth ▸ Demo ▸ Build Demo HUD; buttons call the On* methods.
public class DemoHudController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private PostLabController postLab;
    [SerializeField] private GameObject cluster;        // row root
    [SerializeField] private GameObject skipButton;
    [SerializeField] private GameObject finishButton;
    [SerializeField] private GameObject quizButton;
    [SerializeField] private GameObject endingButton;   // Play Ending: shown at the door, between runs

    public void Bind(ExperimentRunner r, PostLabController p,
        GameObject clusterRoot, GameObject skip, GameObject finish, GameObject quiz)
    { runner = r; postLab = p; cluster = clusterRoot; skipButton = skip; finishButton = finish; quizButton = quiz; }

    /// Pure: may the player skip the current step? Practice mode reuses this button —
    /// a student stuck on one pour must be able to reach the interesting part. Campaign
    /// never offers it: skipping there would hand out a grade for work not done.
    public static bool SkipAllowed(bool demo, bool tutorial, bool running, bool review)
        => (demo || tutorial) && running && !review;

    private void Update()
    {
        // IsRunning persists through the review corner, so hide Skip/Finish there (they
        // do nothing beside the quiz); only the Auto-Quiz button applies while the tablet is open.
        bool review = PharmeeGatekeeper.ReviewFlowActive;
        bool live = runner != null && runner.IsRunning && !review;
        bool skip = SkipAllowed(DemoSession.Active, TutorialSession.Active, live, false);
        // Finish + Auto-Quiz stay DEMO-only: a practice run is ungraded and never opens
        // the quiz tablet, so both would be dead buttons that only raise questions.
        bool demoOnly = DemoSession.Active && live;
        bool quizOpen = DemoSession.Active && postLab != null && postLab.IsOpen;
        Toggle(cluster, skip || demoOnly || quizOpen);
        Toggle(skipButton, skip);
        Toggle(finishButton, demoOnly);
        Toggle(quizButton, quizOpen);
        var show = endingButton != null ? ShowcaseDirector.Find() : null;
        Toggle(endingButton, DemoSession.Active && !live && !quizOpen && !review
                             && show != null && show.Ending != null && !show.Playing);
    }

    private static void Toggle(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    public void OnSkipStep() => DemoActions.CompleteCurrentStep(runner);

    public void OnFinishExperiment() => DemoActions.CompleteAllTasks(runner);

    /// Demo only: the campaign-complete reel with sample results, then the celebration.
    public void OnPlayEnding()
    {
        var show = ShowcaseDirector.Find();
        var gate = FindAnyObjectByType<PharmeeGatekeeper>();
        if (show != null) show.TryPlayEnding(true, gate != null ? gate.CelebrateCampaign : (System.Action)null);
    }

    public void OnAutoQuiz()
    {
        if (DemoActions.AutoAnswerQuiz(postLab) && postLab != null) postLab.Submit();
    }
}
