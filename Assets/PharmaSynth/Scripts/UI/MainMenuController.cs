using UnityEngine;
using UnityEngine.SceneManagement;

/// Drives the cube spawn-room menu: Laboratory enters the lab at the entrance
/// (Pharmee's gate flow then handles episode choice); Tutorial enters the same lab
/// in ungraded guided-practice mode with everything unlocked; Settings toggles the
/// settings panel; Quit exits the game. Plus the config-gated amber Demo button.
/// The lab scene's ExperimentLauncher reads GameFlow.SelectedModuleId on load; the
/// methane TUTORIAL EXPERIMENT is a separate thing, reached inside the lab via
/// Pharmee's episode picker — not this Tutorial MODE button.
public class MainMenuController : MonoBehaviour
{
    [SerializeField] private string labSceneName = "SampleScene";
    [SerializeField] private string fallbackModuleId = "tutorial-methane";
    [SerializeField] private GameObject settingsPanel;

    /// Compute which experiment "Enter Laboratory" pre-selects as the default: the
    /// player's next unlocked-but-unpassed experiment, or the tutorial if none/unknown.
    /// (Only a default — the in-lab gate flow lets the player pick.) Pure so the
    /// self-tests can check it without a live ProgressionService on disk.
    public static string ResolveLabTarget(ProgressionFlow flow, string fallback)
    {
        var next = flow?.NextExperiment();
        return next != null ? next.moduleId : fallback;
    }

    public void OnLaboratory() => EnterLab(demo: false, tutorial: false);

    /// The config-gated Demo Mode button (visible only when the backend file
    /// enables it): same entry, but on the throwaway demo save with every
    /// period unlocked and the HUD auto-complete controls armed.
    public void OnDemoLaboratory() => EnterLab(demo: true, tutorial: false);

    /// Tutorial Mode: all 9 experiments unlocked and heavily guided
    /// — glow + waypoint on the next apparatus, hints on the watch, always-on
    /// labels, skippable steps — and completely ungraded.
    public void OnTutorialLaboratory() => EnterLab(demo: false, tutorial: true);

    /// Every entry declares the FULL mode, both flags, every time. Setting only the
    /// one you're turning on lets a returning player carry the other back in — a
    /// campaign run inheriting Tutorial Mode would be unlocked and ungraded.
    private void EnterLab(bool demo, bool tutorial)
    {
        DemoSession.Active = demo;
        // BeginSession, not just the flag: it also clears the session-only "practised"
        // ticks so a new student at the same headset starts clean.
        if (tutorial) TutorialSession.BeginSession(); else TutorialSession.Active = false;
        var service = new ProgressionService();
        service.Load();
        GameFlow.Select(ResolveLabTarget(ProgressionFlow.Create(service), fallbackModuleId));
        ScreenFader.FadeOutThen(() => SceneManager.LoadScene(labSceneName));
    }

    public void OnSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnQuit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
