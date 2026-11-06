using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// The HUD's top-right cluster inside the lab: Settings toggles the in-lab
/// settings panel; Quit (confirm) fades back to the menu scene; Restart (confirm)
/// rebuilds the whole stage — the panic button for misplaced props / physics lag.
public class LabMenuController : MonoBehaviour
{
    [SerializeField] private string menuSceneName = "MainMenu";
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private ChoicePanelController confirmPanel;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private ExperimentLauncher launcher;
    [SerializeField] private PharmeeGatekeeper gatekeeper;

    private enum Pending { None, Quit, Restart }
    private Pending _pending;
    private bool _subscribed;

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || confirmPanel == null) return;
        confirmPanel.OptionChosen += OnConfirmOption;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || confirmPanel == null) return;
        confirmPanel.OptionChosen -= OnConfirmOption;
        _subscribed = false;
    }

    /// Edit-mode/test binding.
    public void Bind(GameObject settings, ChoicePanelController confirm,
                     ExperimentRunner r, ExperimentLauncher l, PharmeeGatekeeper gk)
    {
        Unsubscribe();
        settingsPanel = settings; confirmPanel = confirm; runner = r; launcher = l; gatekeeper = gk;
        Subscribe();
    }

    // ---- button targets ------------------------------------------------------

    public void OnSettingsToggle()
    {
        if (settingsPanel != null) settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void OnQuitToMenu()
    {
        _pending = Pending.Quit;
        confirmPanel?.Show("Leave the lab and return to the menu?",
            new List<string> { "Yes, quit", "Stay" });
    }

    public void OnRestart()
    {
        _pending = Pending.Restart;
        confirmPanel?.Show(RestartConfirmText(runner != null && runner.IsRunning),
            new List<string> { "Yes, restart", "Cancel" });
    }

    public void OnConfirmOption(int index)
    {
        var pending = _pending;
        _pending = Pending.None;
        confirmPanel?.Hide();
        if (index != 0) return;

        if (pending == Pending.Quit)
        {
            if (Application.isPlaying)
                ScreenFader.FadeOutThen(() => SceneManager.LoadScene(menuSceneName));
        }
        else if (pending == Pending.Restart)
        {
            DoRestart();
        }
    }

    private void DoRestart()
    {
        // Reset = full return to the entrance: props/bottles back
        // to spawn, wearables off, teleport to Pharmee at the door, gate re-closed,
        // Pharmee greets — all handled by the gatekeeper (fade included).
        if (gatekeeper != null) { gatekeeper.ResetToEntrance(); return; }

        // Fallback (no gatekeeper wired — e.g. a bare test scene): rebuild in place.
        string id = runner != null && runner.Module != null ? runner.Module.moduleId : GameFlow.SelectedModuleId;
        TaskTargetRegistry.Clear();
        if (ScreenFader.Instance != null && Application.isPlaying)
            ScreenFader.Instance.FadeAround(() => launcher?.Launch(id, LaunchMode.StageOnly));
        else
            launcher?.Launch(id, LaunchMode.StageOnly);
    }

    /// Pure label helper (testable).
    public static string RestartConfirmText(bool running) => running
        ? "Reset the lab? Your current attempt ends and everything returns to the start."
        : "Reset the lab? All items return to their places and you'll head back to the entrance.";
}
