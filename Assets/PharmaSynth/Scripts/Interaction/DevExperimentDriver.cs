using UnityEngine;
using UnityEngine.InputSystem;

/// Keyboard driver for in-editor testing of the experiment loop without needing
/// full XR interaction. Lets you watch the HUD, Pharmee, and grade screen react.
///   B = begin/restart · 1-5 = complete step N · F = finish · R = retry
///   P = pour-debug overlay (floating "hit/target" text at every pouring mouth)
///   V = replay Pharmee's test voice line (tune RobotVoiceFx by ear without
///       walking back to the door for every slider tweak)
/// Disabled in builds unless enableInBuild is set.
public class DevExperimentDriver : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private ExperimentStarter starter;
    [SerializeField] private bool enableInBuild = false;

    public void Setup(ExperimentRunner r, ExperimentStarter s) { runner = r; starter = s; }

    private void Update()
    {
        if (!Application.isEditor && !enableInBuild) return;
        var kb = Keyboard.current;
        if (kb == null || runner == null) return;

        if (kb.bKey.wasPressedThisFrame)
        {
            if (starter != null) starter.Begin(); else runner.StartExperiment();
            Debug.Log("[Dev] Begin");
        }
        if (kb.digit1Key.wasPressedThisFrame) CompleteIndex(0);
        if (kb.digit2Key.wasPressedThisFrame) CompleteIndex(1);
        if (kb.digit3Key.wasPressedThisFrame) CompleteIndex(2);
        if (kb.digit4Key.wasPressedThisFrame) CompleteIndex(3);
        if (kb.digit5Key.wasPressedThisFrame) CompleteIndex(4);
        if (kb.fKey.wasPressedThisFrame)
        {
            // Finish before Begin would NRE; guard on a live run.
            if (!runner.IsRunning) { Debug.Log("[Dev] Finish ignored — no run in progress (press B first)."); }
            else { var res = runner.Finish(1f); Debug.Log("[Dev] Finish → grade " + res.grade.Total.ToString("0") + "% passed=" + res.passed); }
        }
        if (kb.rKey.wasPressedThisFrame) { runner.Retry(); Debug.Log("[Dev] Retry"); }
        if (kb.pKey.wasPressedThisFrame)
        {
            LiquidPourer.DebugOverlay = !LiquidPourer.DebugOverlay;
            Debug.Log("[Dev] Pour debug overlay " + (LiquidPourer.DebugOverlay ? "ON" : "OFF"));
        }
        if (kb.vKey.wasPressedThisFrame) ReplayVoiceLine();
    }

    /// Speak the generated Lab Tour line through Pharmee's own channel, on demand.
    /// The robot colouring lives on that AudioSource, so this is the audition loop:
    /// press V, nudge ringHz/ringMix in the Inspector, press V again.
    private void ReplayVoiceLine()
    {
        var gate = FindAnyObjectByType<PharmeeGatekeeper>();
        string line = gate != null ? gate.Lines.labTour : new PharmeeGatekeeper.GateLines().labTour;

        foreach (var n in FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (n == null) continue;
            bool isJimenez = false;
            for (var p = n.transform; p != null; p = p.parent)
            {
                string nm = p.name.ToLowerInvariant();
                if (nm.Contains("jimenez") || nm.Contains("examiner") || nm.Contains("proctor")) { isJimenez = true; break; }
            }
            if (isJimenez) continue;
            n.Say(line, n.SecondsFor(line, 4f), n.ResolveVoice(line));
            Debug.Log("[Dev] Replaying Pharmee voice line" + (n.ResolveVoice(line) != null ? " (voiced clip)" : " (NO CLIP — blips only; run Import & Wire Voice Clips)"));
            return;
        }
        Debug.LogWarning("[Dev] no Pharmee narration channel found.");
    }

    private void CompleteIndex(int i)
    {
        if (runner.Graph == null) { Debug.LogWarning("[Dev] Not started — press B first"); return; }
        var tasks = runner.Graph.Tasks;
        if (i < 0 || i >= tasks.Count) return;
        var res = runner.CompleteTask(tasks[i].taskId);
        Debug.Log("[Dev] step " + (i + 1) + " '" + tasks[i].label + "' → " + res);
    }
}
