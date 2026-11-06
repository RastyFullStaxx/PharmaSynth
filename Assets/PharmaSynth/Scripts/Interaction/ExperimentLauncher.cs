using UnityEngine;
using UnityEngine.Events;

/// How Launch() leaves the runner:
///  FullStart    — legacy behavior: stage built AND the clock starts immediately.
///  PrepareArmed — stage built + attempt armed; the clock waits for StartRun()
///                 (door gate: "the period starts as soon as you walk in").
///  StageOnly    — stage furnished, runner untouched (scene-load default + Lab Tour).
public enum LaunchMode { FullStart, PrepareArmed, StageOnly }

/// Loads any of the 9 experiments into the lab scene by moduleId: it swaps the
/// ExperimentRunner's active module (from the ExperimentLibrary) and readies an
/// attempt per LaunchMode. The menu / period hub / door gate call Launch(); on
/// lab-scene entry it can auto-launch whatever GameFlow.SelectedModuleId holds.
public class ExperimentLauncher : MonoBehaviour
{
    [SerializeField] private ExperimentLibrary library;
    [SerializeField] private ExperimentRunner runner;
    [Tooltip("Launch GameFlow.SelectedModuleId automatically when this scene loads.")]
    [SerializeField] private bool launchSelectedOnStart = false;
    [Tooltip("How the scene-load auto-launch leaves the runner (StageOnly = furnish, no clock).")]
    [SerializeField] private LaunchMode startupMode = LaunchMode.FullStart;

    [Tooltip("Raised after a module is loaded, so scene wiring (stations/props/cutscenes) can rebuild for it.")]
    public UnityEvent<ExperimentModuleDefinition> onModuleLoaded;

    public ExperimentLibrary Library => library;
    public LaunchMode StartupMode => startupMode;
    public void SetLibrary(ExperimentLibrary l) => library = l;
    public void SetRunner(ExperimentRunner r) => runner = r;

    private void Start()
    {
        if (launchSelectedOnStart) LaunchSelected();
    }

    public ExperimentModuleDefinition LaunchSelected() => Launch(GameFlow.SelectedModuleId, startupMode);

    /// Legacy single-arg launch — full start (all existing callers/tests unchanged).
    public ExperimentModuleDefinition Launch(string moduleId) => Launch(moduleId, LaunchMode.FullStart);

    /// Swap the runner to the requested module and ready an attempt per mode.
    /// Returns the loaded module, or null if unknown / unwired.
    public ExperimentModuleDefinition Launch(string moduleId, LaunchMode mode)
    {
        if (library == null || runner == null)
        {
            Debug.LogWarning("[ExperimentLauncher] Library or runner not assigned.");
            return null;
        }
        var mod = library.Get(moduleId);
        if (mod == null)
        {
            Debug.LogWarning("[ExperimentLauncher] Unknown moduleId: " + moduleId);
            return null;
        }
        runner.SetModule(mod);
        onModuleLoaded?.Invoke(mod);
        switch (mode)
        {
            case LaunchMode.FullStart: runner.StartExperiment(); break;
            case LaunchMode.PrepareArmed: runner.PrepareExperiment(); break;
            case LaunchMode.StageOnly: break;   // furnish only
        }
        return mod;
    }
}
