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
    }

    private static void Toggle(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    public void OnSkipStep() => DemoActions.CompleteCurrentStep(runner);

    public void OnFinishExperiment() => DemoActions.CompleteAllTasks(runner);

    public void OnAutoQuiz()
    {
        if (DemoActions.AutoAnswerQuiz(postLab) && postLab != null) postLab.Submit();
    }
}
