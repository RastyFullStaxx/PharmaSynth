#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using XRManager = UnityEngine.XR.Interaction.Toolkit.XRInteractionManager;
using NearFar = UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor;

/// Plays the game in PLAY MODE, unattended, and reports what broke.
///
/// ⭐ Why this exists on top of Simulate Everything: that battery runs in EDIT mode, where
/// Update never ticks, coroutines never run, physics never steps, XRI never selects
/// anything and no audio plays. Every bug that only exists in MOTION is invisible to it:
/// items vanishing, dialogue stomping mid-typing, the holo panel not scrolling, quiz
/// buttons not clickable, an error spammed every frame.
///
/// ⭐ No headset needed: PC Dev Mode leaves OpenXR auto-init OFF on Standalone and the
/// scene's XR Device Simulator drives the rig.
///
/// ⛔ Deliberately an EDITOR-assembly script ticked by EditorApplication.update (which
/// keeps running during play) rather than a runtime MonoBehaviour. That way it can reuse
/// the editor-only simulators, and nothing it does can ever leak into a player build.
[InitializeOnLoad]
public static class PlaytestAutopilot
{
    // Logs/, never Temp/ — Unity wipes Temp, and this request has to survive the domain
    // reload that entering Play mode causes.
    const string Request = "Logs/autopilot-request.txt";
    const string Report = "Logs/autopilot-report.txt";
    const string ShotDir = "Logs/autopilot";

    /// Hard ceilings. The editor must NEVER be left sitting in Play mode because the
    /// autopilot wedged waiting for a state that will never arrive.
    /// Nine modules at ~50 s each, plus slack for the slowest (13-task) ones. Still a
    /// hard ceiling: the editor must never be left sitting in Play mode.
    const float HardCapSeconds = 1800f;
    const float VisualHardCapSeconds = 4200f;
    /// Generous on purpose: the review briefing is two spoken beats behind a fade, and a
    /// watchdog that fires mid-cutscene reports a stall that is really just dialogue.
    const float BeatTimeout = 60f;

    static PlaytestAutopilot()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/PharmaSynth/Autopilot Playtest (plays the game in Play mode)")]
    public static void Launch() => Launch(false);

    /// ⭐ Tutorial Mode needs its own pass. The campaign
    /// sweep enters via "Laboratory", so `TutorialSession.Active` stays false and every
    /// guidance cue (glow, ghost, beacon, spotlight, verb demo, wrong-grab nudge, need
    /// line, ping, ground path) correctly does nothing; the ground path reports
    /// "dist 0.0, no route" in all nine modules for exactly that reason.
    [MenuItem("Tools/PharmaSynth/Autopilot Playtest (TUTORIAL Mode — checks the guidance)")]
    public static void LaunchTutorial() => Launch(true);

    public static void Launch(bool tutorial) => Launch(tutorial ? "tutorial" : "campaign");

    /// ⭐ VISUAL: the campaign loop again, but every step is performed HONESTLY
    /// through SimulatedRun's verbs (real pours and scoop dips, the water bath, the ice
    /// bucket, the glass rod, a real litmus strip...) and then PHOTOGRAPHED — a close-up
    /// of the vessel the step happened in, plus the numbers behind the picture, judged
    /// against the fired reaction's manuscript observation. The other two modes complete
    /// steps by calling CompleteTask, so nothing ever happens in a vessel and their
    /// screenshots show an empty bench. Report: Logs/visual-sweep-report.txt.
    [MenuItem("Tools/PharmaSynth/Autopilot Playtest (VISUAL — honest verbs + vessel close-ups)")]
    public static void LaunchVisual() => Launch("visual");

    public static void Launch(string mode)
    {
        if (Application.isPlaying) { Debug.LogWarning("[Autopilot] already in Play mode."); return; }
        // ⛔ Never launch with a compile pending. Unity defers script compilation until
        // play mode EXITS, so a queued compile leaves IsCompiling stuck true for the whole
        // session, EditorApplication.update is starved, and the autopilot wedges — its own
        // hard cap cannot save it, because that check runs on the update it no longer gets.
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            Debug.LogWarning("[Autopilot] Unity is still compiling/importing — wait for it to "
                             + "settle, then run this again.");
            return;
        }
        Directory.CreateDirectory("Logs");
        Directory.CreateDirectory(ShotDir);
        File.WriteAllText(Request, mode);
        Debug.Log("[Autopilot] entering Play mode — it will drive the game, write "
                  + Report + " and exit on its own.");
        EditorApplication.EnterPlaymode();
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode) return;
        if (!File.Exists(Request)) return;
        string mode = File.ReadAllText(Request).Trim();
        File.Delete(Request);            // consume immediately: a crash must not re-trigger
        Begin(mode);
    }

    // ---- run state (rebuilt fresh each play session) ------------------------------

    static readonly List<string> s_findings = new List<string>();
    static readonly Dictionary<string, int> s_errors = new Dictionary<string, int>();
    static readonly Dictionary<string, string> s_errorBeat = new Dictionary<string, string>();
    static readonly List<string> s_trace = new List<string>();
    static readonly HashSet<Transform> s_grabChecked = new HashSet<Transform>();
    static bool s_noInteractorReported;
    static double s_startedAt, s_beatSince;
    static string s_beat = "boot";
    static string s_lastState = "";
    static int s_grabsOk, s_grabsTested, s_uiOk, s_uiTested;
    static bool s_running, s_uiCheckedThisRun;

    /// Pace + repeat control. Acting on EVERY EditorApplication.update tick re-presses
    /// "Laboratory" hundreds of times a second, restarting the fade and scene load
    /// forever, and setting the beat twice per tick resets the stall watchdog's timer
    /// continuously so it never fires. A game needs FRAMES between
    /// inputs; a driver that does not wait is not driving, it is jamming the button.
    const float ActEverySeconds = 0.75f;
    const float RepeatAfterSeconds = 6f;
    const int MaxRepeats = 4;
    const int MaxTrace = 400;

    /// ⛔ A PER-MODULE ceiling, independent of beats and actions.
    /// Beat and action watchdogs both reset when the state changes — so a gate that
    /// OSCILLATES between two states resets them forever and neither ever fires. That
    /// wedged the sweep twice on prelim-ethyl-alcohol with no report at all. This cap
    /// cannot be reset by anything the game does, which is the whole point: one bad
    /// module becomes one finding, not a dead run and zero data about the other eight.
    const float ModuleCapSeconds = 150f;
    /// VISUAL mode performs every step for REAL and then waits for the game to finish it,
    /// so a module legitimately takes minutes rather than seconds.
    const float VisualModuleCapSeconds = 420f;
    /// How many ticks a performed step may take to complete on real frames before the
    /// sweep re-applies the player's hold, and again before it is called unplayed.
    /// ~0.75 s a tick: 16 ticks is 12 s of the game actually running the step.
    const int VisualWaitTicks = 16;

    static double s_nextActAt;
    static string s_lastAction = "";
    static double s_lastActionAt;
    static int s_repeats;
    static bool s_traceFull;

    /// Coverage. Without these, a run that never left the first screen reports "0 findings"
    /// and prints CLEAN — a false NEGATIVE, which is worse than a false positive because
    /// nobody goes looking.
    static bool s_reachedLab, s_reachedRunning, s_reachedQuiz, s_reachedGrade;
    static int s_tasksCompleted;

    /// The sweep: every module in catalog order, each played through the real loop.
    /// Modules are FORCE-selected (canSelect => true) rather than waiting for the unlock
    /// chain — the autopilot's job is coverage of what each module does when played, and
    /// the unlock chain itself is already proven 8/8 by the edit-mode campaign sim.
    static readonly List<string> s_pathLog = new List<string>();
    // ⛔ The path is sampled EVERY tick, not once at run start. A single sample on the
    // first DriveRun tick reads "dist 0.0" for all 9 modules, which looks like "the path
    // never drew", but GuidePath.Update early-returns (no camera yet, no target yet, still
    // time-skipping) WITHOUT resetting LastDistance, so 0.0 only means "Update had not
    // reached the distance line yet on that one frame". Watching the whole run tells the two
    // apart, and it is the difference between a real regression and a measurement artefact.
    static int s_pathBestChevrons, s_pathObservations, s_pathTicksShown;
    static float s_pathMaxDist;
    static bool s_pathEverShown, s_pathSeenComponent;
    static string s_pathLastTarget = "-";
    static readonly List<string> s_queue = new List<string>();
    static readonly List<string> s_moduleLog = new List<string>();
    static int s_moduleIndex;
    static int s_moduleTasks, s_moduleGrabsOk, s_moduleGrabsTested, s_moduleFindingsAt;
    static double s_moduleStartedAt;

    static string CurrentModule => s_moduleIndex < s_queue.Count ? s_queue[s_moduleIndex] : null;

    static bool s_tutorial;

    static void Begin(string mode)
    {
        s_tutorial = mode == "tutorial";
        s_visual = mode == "visual";
        s_session = null; s_pending = null; s_stepIndex = 0; s_honest = 0;
        SimulatedRun.MidVerb = s_visual ? VisualSweep.MidVerb : (System.Action<GameObject, string>)null;
        // Set AFTER the domain reload that entering Play mode causes — a static assigned
        // before it would be wiped on the way in.
        SimulatedRun.NeverForce = s_visual;
        LiquidPhysics.DrainProbeName = s_visual ? "DistillingFlask" : null;   // diagnostic
        if (s_visual) VisualSweep.BeginRun();
        s_findings.Clear(); s_errors.Clear(); s_errorBeat.Clear(); s_trace.Clear();
        s_grabChecked.Clear(); s_shots.Clear();
        s_grabsOk = s_grabsTested = s_uiOk = s_uiTested = 0;
        s_uiCheckedThisRun = false; s_noInteractorReported = false;
        s_nextActAt = 0; s_lastAction = ""; s_lastActionAt = 0; s_repeats = 0; s_traceFull = false;
        s_reachedLab = s_reachedRunning = s_reachedQuiz = s_reachedGrade = false;
        s_tasksCompleted = 0;

        s_queue.Clear(); s_moduleLog.Clear(); s_pathLog.Clear();
        foreach (var e in ExperimentCatalog.Entries) if (e != null) s_queue.Add(e.moduleId);
        s_moduleIndex = 0;
        s_startedAt = EditorApplication.timeSinceStartup;
        s_beatSince = s_startedAt;
        s_beat = "boot"; s_lastState = ""; s_running = true;

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Trace("autopilot started");
        BeginModule();          // after s_startedAt, or the first trace stamps garbage
    }

    /// Every error/exception, DEDUPLICATED with a count and tagged with the beat it first
    /// happened in. Without the dedup, one misconfigured particle system writes 711
    /// identical lines a second and buries everything else in the report.
    static void OnLog(string msg, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        if (msg != null && msg.StartsWith("[Autopilot]")) return;
        string key = type + ": " + (msg ?? "");
        if (key.Length > 300) key = key.Substring(0, 300);
        if (s_errors.ContainsKey(key)) { s_errors[key]++; return; }
        s_errors[key] = 1;
        // The message alone ("NullReferenceException") names nothing. Keep the first few
        // stack frames or the finding is unactionable.
        string where = "";
        if (!string.IsNullOrEmpty(stack))
        {
            var lines = stack.Split('\n');
            for (int i = 0; i < lines.Length && i < 3; i++)
                if (!string.IsNullOrWhiteSpace(lines[i])) where += "\n         at " + lines[i].Trim();
        }
        s_errorBeat[key] = s_beat + where;
    }

    static void Trace(string line)
    {
        if (s_traceFull) return;
        if (s_trace.Count >= MaxTrace)
        {
            s_traceFull = true;
            s_trace.Add("  ... trace truncated at " + MaxTrace + " lines (a runaway driver "
                        + "once wrote a 1 MB report of the same four lines).");
            return;
        }
        s_trace.Add("[" + (EditorApplication.timeSinceStartup - s_startedAt).ToString("0.0") + "s] " + line);
    }

    static void Finding(string what)
    {
        s_findings.Add(what);
        Trace("FINDING " + what);
        Shot("finding-" + s_findings.Count);      // a picture of where it broke
    }

    static void BeginModule()
    {
        s_flight = null; s_moduleStopped = false; s_handled.Clear();
        s_moduleStartedAt = EditorApplication.timeSinceStartup;
        s_moduleTasks = 0; s_moduleGrabsOk = 0; s_moduleGrabsTested = 0;
        s_moduleFindingsAt = s_findings.Count;
        s_uiCheckedThisRun = false;
        s_pathBestChevrons = 0; s_pathObservations = 0; s_pathTicksShown = 0;
        s_pathMaxDist = 0f; s_pathEverShown = false; s_pathSeenComponent = false;
        s_pathLastTarget = "-";
        s_grabChecked.Clear();          // a rebuilt stage means new transforms
        Trace("=== module " + (s_moduleIndex + 1) + "/" + s_queue.Count + ": " + CurrentModule + " ===");
    }

    static void EndModule()
    {
        SamplePath();                    // the whole run's path evidence, not one frame
        if (s_pending != null) CaptureStep();
        if (s_session != null)
        {
            s_session.End(); s_session = null;
            // The play-mode transcript, in the same shape as Logs/simrun-<module>.txt —
            // without it a step that behaves differently in play than in edit mode can
            // only be guessed at.
            try
            {
                Directory.CreateDirectory(VisualSweep.Dir);
                File.WriteAllText(VisualSweep.Dir + "/" + (s_moduleIndex + 1).ToString("00") + "-"
                                  + CurrentModule + "-play.txt", s_sessionLog.ToString());
            }
            catch (System.Exception e) { Trace("could not write the play transcript: " + e.Message); }
        }
        s_flight = null;
        int found = s_findings.Count - s_moduleFindingsAt;
        s_moduleLog.Add("  " + (CurrentModule ?? "?").PadRight(30)
                        + (s_moduleTasks + " tasks").PadRight(11)
                        + (s_moduleGrabsOk + "/" + s_moduleGrabsTested + " grabs").PadRight(13)
                        + (found + " finding(s)").PadRight(14)
                        + (EditorApplication.timeSinceStartup - s_moduleStartedAt).ToString("0") + "s");
        s_moduleIndex++;
        if (s_moduleIndex < s_queue.Count) BeginModule();
    }

    static void SetBeat(string beat)
    {
        if (s_beat == beat) return;
        s_beat = beat;
        s_beatSince = EditorApplication.timeSinceStartup;
        Trace("beat -> " + beat);
    }

    // ---- the driver ---------------------------------------------------------------

    static void Tick()
    {
        if (!s_running) return;
        if (!Application.isPlaying) { Finish("play mode ended early"); return; }

        double now = EditorApplication.timeSinceStartup;
        if (now - s_startedAt > (s_visual ? VisualHardCapSeconds : HardCapSeconds)) { Finding("HARD TIMEOUT — the run never completed"); Finish("hard cap"); return; }

        // Pace the driver. Everything below this line assumes the game has had frames to
        // act on the last input.
        if (now < s_nextActAt) return;
        s_nextActAt = now + ActEverySeconds;

        // VISUAL: the step performed on the previous tick has had ~0.75 s of frames (the
        // colour lerp is 0.5 s; smoke lives 2.4 s; a popup 2.5 s) — photograph it now.
        if (s_pending != null) { CaptureStep(); return; }

        // Per-module ceiling — checked BEFORE Drive so a wedged module cannot skip it.
        if (CurrentModule != null && now - s_moduleStartedAt > (s_visual ? VisualModuleCapSeconds : ModuleCapSeconds))
        {
            var g = Object.FindAnyObjectByType<PharmeeGatekeeper>();
            Finding(CurrentModule + ": module TIMED OUT after " + ModuleCapSeconds
                    + "s, stuck at gate state '" + (g != null ? g.Model.State.ToString() : "?")
                    + "' (beat '" + s_beat + "') — skipping to the next module");
            var gate2 = g;
            if (gate2 != null) gate2.ResetToEntrance();      // hard reset back to Blocked
            EndModule();
            s_lastAction = ""; s_repeats = 0;
            if (CurrentModule == null) { Finish("swept every module"); return; }
            return;
        }

        try { Drive(); }
        catch (System.Exception e)
        {
            Finding("autopilot threw while driving at beat '" + s_beat + "': " + e.Message);
            Finish("driver exception");
        }
    }

    /// Fire an action at most once per distinct situation, retrying only if the game has
    /// not moved on after RepeatAfterSeconds. Returns false when the same action has been
    /// retried too many times — which is a STALL, not something to keep hammering.
    static bool Act(string key)
    {
        double now = EditorApplication.timeSinceStartup;
        if (key != s_lastAction)
        {
            s_lastAction = key; s_lastActionAt = now; s_repeats = 0;
            return true;
        }
        if (now - s_lastActionAt < RepeatAfterSeconds) return false;   // give it time to land
        s_lastActionAt = now;
        if (++s_repeats > MaxRepeats)
        {
            Finding("STALL: '" + key + "' had no effect after " + MaxRepeats
                    + " attempts — the game is not responding to it");
            Finish("stalled at " + key);
            return false;
        }
        Trace("retry " + s_repeats + " of " + key);
        return true;
    }

    static void Drive()
    {
        var gate = Object.FindAnyObjectByType<PharmeeGatekeeper>();

        // Still in the cube room: press Laboratory, exactly as the player does.
        if (gate == null)
        {
            var menu = Object.FindAnyObjectByType<MainMenuController>();
            if (menu != null)
            {
                SetBeat("cube-room");
                // ⚠ NOT on the first frame. Wait out the boot fade before doing ANYTHING:
                // ScreenFader.Start() sets alpha to 1 and fades in, so both the screenshot
                // and the button press would otherwise land on a black, not-yet-settled
                // first frame (a cube room that looks pitch black).
                if (EditorApplication.timeSinceStartup - s_beatSince < 2.5f) return;
                Shot("00-cube-room");
                if (Act("press-laboratory"))
                {
                    Trace(s_tutorial ? "pressing Tutorial" : "pressing Laboratory");
                    if (s_tutorial) menu.OnTutorialLaboratory(); else menu.OnLaboratory();
                }
                return;
            }
            SetBeat("loading-lab");
            StallCheck("no PharmeeGatekeeper and no menu — the lab scene never loaded");
            return;
        }

        string state = gate.Model.State.ToString();
        if (state != s_lastState) { s_lastState = state; SetBeat("gate:" + state); }

        var runner = Object.FindAnyObjectByType<ExperimentRunner>();

        switch (gate.Model.State)
        {
            case GateState.Blocked:
                s_reachedLab = true;
                if (CurrentModule == null) { Finish("swept every module"); return; }
                Shot("01-at-the-door");
                if (Act("approach-door")) gate.OnApproachTriggerEntered();
                break;

            case GateState.ModeChoice:
                if (Act("pick-campaign")) gate.OnPanelOption(1);
                break;

            case GateState.CampaignExplain:
                if (Act("past-explainer")) gate.OnPanelOption(0);
                break;

            case GateState.EpisodePick:
                Shot("02-picker");
                if (Act("pick-module")) PickSomething(gate);
                break;

            case GateState.CoatPrompt:
                Shot("03-ppe");
                if (Act("don-ppe")) DonPPE(gate);
                break;

            case GateState.ReadyPrompt:
                if (Act("say-ready")) gate.OnPanelOption(0);
                break;

            case GateState.Loading:
                StallCheck("stuck in Loading — the fade callback or its watchdog never fired");
                break;

            case GateState.ThresholdWarn:
                Shot("04-threshold");
                if (Act("proceed-threshold")) gate.OnPanelOption(0);
                break;

            case GateState.DoorArmed:
                if (Act("cross-threshold")) gate.OnThresholdTriggerEntered();
                break;

            case GateState.Running:
                DriveRun(runner);
                break;

            case GateState.SupplyPrompt:
                // ⛔ A gate state the driver doesn't handle would sit here in silence until
                // the module cap fires, hiding the real cause (reagents ran out, even after
                // a restart).
                // Report it as the blocker it is, then take the restart so the sweep can
                // see whether a fresh stage fixes it.
                Finding((CurrentModule ?? "?") + ": SUPPLY PROMPT at " + s_moduleTasks
                        + " task(s) done — the game says there are not enough reagents to "
                        + "finish, before the player has consumed anything");
                Shot("supply-prompt");
                if (Act("supply-restart")) gate.OnPanelOption(0);   // Restart period
                break;

            case GateState.QuizIntro:
                StallCheck("stuck in QuizIntro — Jimenez's briefing never completed");
                break;

            case GateState.QuizTime:
                DriveQuiz();
                break;

            case GateState.ScoreReview:
                Shot("07-grade");
                DriveGrade(gate);
                break;

            case GateState.Returning:
            case GateState.Debrief:
            case GateState.UnlockAnnounce:
                // These advance on their own coroutines; only complain if they never do.
                StallCheck("stuck in " + state + " — the return/debrief chain never advanced");
                break;
        }
    }

    /// Open a period, then take the first module it offers. Both go through the real
    /// picker, so a broken unlock chain shows up here rather than being narrated over.
    /// Pick the module the sweep is currently on, through the REAL two-step picker but
    /// with the unlock gate bypassed. Coverage is the goal here; the unlock chain has its
    /// own proof in the edit-mode campaign sim.
    static void PickSomething(PharmeeGatekeeper gate)
    {
        string want = CurrentModule;
        if (string.IsNullOrEmpty(want)) { Finish("sweep complete"); return; }

        var entry = ExperimentCatalog.Get(want);
        if (entry == null)
        {
            Finding("module '" + want + "' is not in ExperimentCatalog — the roster and the "
                    + "module assets disagree");
            EndModule();
            return;
        }

        System.Func<string, bool> anything = _ => true;
        if (!gate.Model.ChooseEpisode(entry.period, anything, _ => want))
        {
            Finding(want + ": the picker refused to open period " + entry.period);
            EndModule();
            return;
        }
        if (!gate.Model.ChooseModule(want, anything))
        {
            Finding(want + ": the picker refused the module even with the unlock gate open");
            EndModule();
            return;
        }
        Trace("picked " + want);
    }

    static void DonPPE(PharmeeGatekeeper gate)
    {
        var ppe = Object.FindAnyObjectByType<PPEController>();
        if (ppe == null) { Finding("no PPEController in the scene — the coat gate cannot be satisfied"); Finish("no PPE"); return; }
        ppe.DonPPE();                                   // coat + goggles + gloves at once
        gate.OnPPEWorn();
    }

    // ---- inside a run --------------------------------------------------------------

    static void DriveRun(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null) { StallCheck("Running with no runner/graph"); return; }

        if (!s_uiCheckedThisRun)
        {
            Shot("05-run-start");
            CheckUiRaycasts();
            s_uiCheckedThisRun = true;
        }
        ObservePath();          // every tick — see s_pathBestChevrons for why

        if (s_moduleStopped)
        {
            var gStop = Object.FindAnyObjectByType<PharmeeGatekeeper>();
            EndModule();
            if (CurrentModule == null) { Finish("swept every module"); return; }
            gStop?.ResetToEntrance();
            s_lastAction = ""; s_repeats = 0;
            return;
        }

        ExperimentTask next = null;
        foreach (var t in runner.Graph.AvailableTasks()) { next = t; break; }
        if (next == null)
        {
            // Tutorial Mode is UNGRADED: ShouldEnterReview() is false, so a finished run
            // never reaches QuizIntro/ScoreReview — it shows a practice summary and goes
            // home. Running out of tasks IS the end of the module here.
            if (s_tutorial && s_moduleTasks > 0)
            {
                Trace("practice run complete (" + s_moduleTasks + " steps)");
                var g = Object.FindAnyObjectByType<PharmeeGatekeeper>();
                EndModule();
                if (CurrentModule == null) { Finish("swept every module"); return; }
                g?.ResetToEntrance();
                s_lastAction = ""; s_repeats = 0;
                return;
            }
            StallCheck("Running but no task is available — the graph is deadlocked");
            return;
        }

        // THE point of running in Play mode: can the player actually pick these up?
        s_reachedRunning = true;
        GrabTest(next.taskId);

        // Completion itself is already proven rigorously by the edit-mode battery; here it
        // exists to ADVANCE the flow so the live systems around it — fades, cutscenes,
        // audio, the quiz tablet, the grade card — all get exercised for real.
        if (s_visual) { DriveVisualStep(runner, next); return; }
        if (!Act("task:" + next.taskId)) return;
        runner.CompleteTask(next.taskId);
        s_tasksCompleted++; s_moduleTasks++;
        SetBeat("run:" + next.taskId);
    }

    /// What the ground path is doing right now, in numbers.
    ///
    /// A screenshot of a floor path is weak evidence: the chevrons are small, the autopilot
    /// never walks, and the camera may be pointed at a wall. Whether the path DREW, how
    /// many marks it laid and how far the target was are facts a picture cannot settle.
    /// One tick's worth of evidence, accumulated across the whole run.
    static void ObservePath()
    {
        var gp = GuidePath.Instance;
        if (gp == null) return;
        s_pathSeenComponent = true;
        s_pathObservations++;
        if (gp.LastDistance > s_pathMaxDist) s_pathMaxDist = gp.LastDistance;
        if (!string.IsNullOrEmpty(gp.LastTargetName) && gp.LastTargetName != "-")
            s_pathLastTarget = gp.LastTargetName;
        if (gp.PathShown)
        {
            s_pathEverShown = true;
            s_pathTicksShown++;
            if (gp.ActiveChevrons > s_pathBestChevrons) s_pathBestChevrons = gp.ActiveChevrons;
        }
    }

    /// What the whole run showed, written once per module.
    static void SamplePath()
    {
        var gp = GuidePath.Instance;
        if (!s_pathSeenComponent && gp == null)
        {
            s_pathLog.Add("  " + (CurrentModule ?? "?").PadRight(30) + "NO GuidePath in the scene");
            Finding((CurrentModule ?? "?") + ": no GuidePath component — the ground path cannot draw "
                    + "at all (run Build Tutorial Scene Wiring)");
            return;
        }
        if (s_pathEverShown)
        {
            s_pathLog.Add("  " + (CurrentModule ?? "?").PadRight(30)
                          + "DREW on " + s_pathTicksShown + "/" + s_pathObservations + " ticks, up to "
                          + s_pathBestChevrons + " chevrons, farthest target "
                          + s_pathMaxDist.ToString("0.0") + " m");
            return;
        }
        s_pathLog.Add("  " + (CurrentModule ?? "?").PadRight(30)
                      + "never drew in " + s_pathObservations + " ticks — farthest target "
                      + s_pathMaxDist.ToString("0.0") + " m ('" + s_pathLastTarget + "')"
                      + (s_pathMaxDist <= GuidePathMath.NearDistance
                         ? ", all within the " + GuidePathMath.NearDistance + " m beacon handover"
                         : ", WHICH IS BEYOND THE HANDOVER"));
        if (s_pathMaxDist > GuidePathMath.NearDistance)
            Finding((CurrentModule ?? "?") + ": the ground path never drew despite a target "
                    + s_pathMaxDist.ToString("0.0") + " m away");
    }

    /// Force a REAL XRI grab on every object this step needs.
    ///
    /// This is the check edit mode structurally cannot make: SimulatedRun reaches every
    /// object by reference, so a bottle with no collider, on the wrong interaction layer,
    /// or with its interactable disabled simulates perfectly and is unpickupable in VR.
    static void GrabTest(string taskId)
    {
        if (TaskTargetRegistry.TaskCount == 0) TutorialTargets.Build();
        // ⚠ Include INACTIVE. The Near-Far Interactors live under Left/Right Controller,
        // which are not necessarily active the moment a run starts with no real device
        // attached, and the default FindAnyObjectByType skips inactive objects, so it would
        // conclude "nothing in this game can be picked up at all" about a
        // scene that plainly has two of them. Prefer an active one; say so when there is
        // none, because that is a different (and also interesting) fact.
        var manager = Object.FindAnyObjectByType<XRManager>(FindObjectsInactive.Include);
        var all = Object.FindObjectsByType<NearFar>(FindObjectsInactive.Include);
        NearFar interactor = null;
        foreach (var nf in all) { if (nf != null && nf.isActiveAndEnabled) { interactor = nf; break; } }
        bool usingInactive = false;
        if (interactor == null && all.Length > 0) { interactor = all[0]; usingInactive = true; }

        if (manager == null || interactor == null)
        {
            if (!s_noInteractorReported)
            {
                s_noInteractorReported = true;
                Finding("no XRInteractionManager / NearFarInteractor in the scene at all "
                        + "(manager=" + (manager != null) + ", interactors found=" + all.Length
                        + ") — nothing in this game can be picked up");
            }
            return;
        }
        if (usingInactive && !s_noInteractorReported)
        {
            s_noInteractorReported = true;
            Trace("all " + all.Length + " Near-Far Interactors are INACTIVE at run start — "
                  + "testing through an inactive one (expected with no device attached)");
        }

        foreach (var t in TaskTargetRegistry.Targets(taskId))
        {
            if (t.transform == null) continue;
            var grab = t.transform.GetComponent<XRGrab>();
            if (grab == null) continue;                       // fixed apparatus: not grabbable by design
            if (!s_grabChecked.Add(t.transform)) continue;    // once per object per session

            s_grabsTested++; s_moduleGrabsTested++;
            string name = t.transform.name;

            // ⛔ NOT XRInteractionManager.CanSelect. That also asks whether the interactable
            // is a VALID TARGET right now (whether the hand is near it), so it
            // returns false for every object across the room and would report every
            // methane prop as unpickupable. The autopilot has no hands
            // to walk over; what it can honestly test is the grab MACHINERY.
            //
            // Static capability first (this is the real "can never be grabbed" class).
            if (!grab.enabled || !grab.gameObject.activeInHierarchy)
            {
                Finding("'" + name + "' (needed by " + taskId + ") has its XRGrabInteractable "
                        + "DISABLED — it can never be picked up");
                continue;
            }
            bool hasCollider = false;
            foreach (var c in grab.GetComponentsInChildren<Collider>())
                if (c != null && c.enabled && !c.isTrigger) { hasCollider = true; break; }
            if (!hasCollider)
            {
                Finding("'" + name + "' (needed by " + taskId + ") has NO solid collider — "
                        + "XRI has nothing to hit, so a hand passes straight through it");
                continue;
            }
            if ((grab.interactionLayers.value & interactor.interactionLayers.value) == 0)
            {
                Finding("'" + name + "' (needed by " + taskId + ") shares no interaction layer "
                        + "with the hand (item=" + grab.interactionLayers.value
                        + ", hand=" + interactor.interactionLayers.value + ") — never selectable");
                continue;
            }

            // ⛔ No forced SelectEnter/SelectExit pair.
            //
            // With no device attached the interactor is inactive, so SelectEnter silently
            // does not take; SelectExit then asserts "An Interactor received a Select Exit
            // event for an Interactable that it was not selecting" and throws, killing the
            // whole sweep. When it doesn't throw, the same non-event reports grabs "clean",
            // a pass that proves nothing.
            //
            // What survives is what is TRUE without hands: the capability checks
            // above. They catch the real "can never be picked up" class (disabled
            // interactable, no solid collider, non-overlapping interaction layers) and
            // they cannot lie about it. Whether a grab FEELS right, and whether an object
            // behaves on release, are headset questions and are honestly labelled as such.
            s_grabsOk++; s_moduleGrabsOk++;
        }
    }

    // ---- VISUAL mode -------------------------------------------------------

    static bool s_visual;
    static SimulatedRun.Session s_session;
    static SimulatedRun.Result s_sessionRes;
    static readonly StringBuilder s_sessionLog = new StringBuilder();
    static int s_stepIndex, s_honest;

    sealed class Pending
    {
        public ExperimentTask task; public string kind, module; public LiquidPhysics vessel;
        public List<ReactionRule> rules; public float targetC; public bool honest;
    }
    static Pending s_pending;

    /// A step that has been PERFORMED and is now finishing on real frames.
    sealed class InFlight
    {
        public ExperimentTask task; public string kind; public LiquidPhysics vessel;
        public List<ReactionRule> rules = new List<ReactionRule>(); public float targetC;
        public int waits; public bool retried;
        /// Bugs the handlers logged while serving. Promoted to findings when the step
        /// lands, except completion complaints from a serve the game then finished on its
        /// own frames, which are the harness's single-tick impatience, not the game.
        public List<string> bugs = new List<string>();
    }

    /// Pure (suite-pinned): is this handler bug only "it had not completed yet"? Such a line
    /// is noise once the step DOES complete on real frames; anything else (a broken heat
    /// gate, a missing precipitate, a dry bottle) stays a finding whatever happens next.
    public static bool IsCompletionNoise(string bug)
        => bug != null && (bug.Contains("never completed") || bug.Contains("did not complete")
                           || bug.Contains("never filled the receiver") || bug.Contains("not ready after"));
    static InFlight s_flight;
    static bool s_moduleStopped;

    /// NO PROGRAMMATIC SHORTCUTS (never cheat a step through
    /// in code). A step is served the way a hand would serve it and then the sweep WAITS
    /// for the game to finish it. The vapor stream, the fermentation, the bath and the ice
    /// bucket all self-drive from Update, so hammering their public seams inside a single
    /// tick fights them, and calling CompleteTask when that does not take proves nothing.
    /// Nothing is completed by fiat here: a step that never finishes is reported
    /// UNPLAYED and the module STOPS, because a forced step proves nothing about whether a
    /// player could have done it.
    static void DriveVisualStep(ExperimentRunner runner, ExperimentTask next)
    {
        if (s_flight != null)
        {
            var f = s_flight;
            if (runner.Graph.IsComplete(f.task.taskId)) { LandStep(true); return; }

            // Keep holding it there — the player action for every self-driving step, and
            // what gives the real Update loops something to work with.
            if (f.waits++ < VisualWaitTicks) { SustainStep(runner, f); return; }

            if (!f.retried)
            {
                f.retried = true; f.waits = 0;
                Trace("re-serving " + f.task.taskId + " (not complete after " + VisualWaitTicks
                      + " ticks of real frames)");
                RunHandler(runner, f.task);
                return;
            }

            Finding(CurrentModule + " / " + f.task.taskId + ": UNPLAYED — the real verbs never "
                    + "completed this step in Play mode after " + (VisualWaitTicks * 2) + " ticks "
                    + "(condition registered=" + runner.Graph.HasCondition(f.task.taskId)
                    + ", kind=" + (string.IsNullOrEmpty(f.kind) ? "?" : f.kind)
                    + "). Nothing is forced, so the module stops here.");
            LandStep(false);
            s_moduleStopped = true;
            return;
        }

        if (!Act("task:" + next.taskId)) return;
        RunHandler(runner, next);
    }

    /// Serve one step through the same Session the edit-mode simulator drives.
    static void RunHandler(ExperimentRunner runner, ExperimentTask next)
    {
        if (s_session == null)
        {
            s_sessionRes = new SimulatedRun.Result { totalTasks = runner.Graph.Tasks.Count };
            s_sessionLog.Clear();
            s_sessionLog.AppendLine("=== " + CurrentModule + " — played in PLAY MODE by the visual sweep ===");
            // Start every module from the bench the game itself hands a player at the start
            // of a run: bottles full, glassware home, burners out, consumables restocked.
            // Without it the sweep inherits the previous module's residue — which is how
            // Exp 7 began with 5 ml of precipitate already in its flask.
            DropRespawn.ResetAllHome();
            s_session = new SimulatedRun.Session();
            s_session.Begin(runner, CurrentModule, s_sessionRes, s_sessionLog);
            TaskTargetRegistry.Clear(); TutorialTargets.Build();      // this module stage, not the last one
            s_stepIndex = 0;
        }
        bool fresh = s_flight == null;
        if (fresh)
        {
            s_stepIndex++;
            VisualSweep.BeginStep(s_moduleIndex + 1, CurrentModule, s_stepIndex, next.taskId);
            AuditHandling(next.taskId);
        }
        int bugsBefore = s_sessionRes.bugs.Count;
        try { s_session.Perform(next); }
        catch (System.Exception e)
        {
            s_sessionRes.bugs.Add("the honest verbs THREW: " + e.GetType().Name + ": " + e.Message);
        }
        if (s_flight == null) s_flight = new InFlight();
        for (int i = bugsBefore; i < s_sessionRes.bugs.Count; i++)
            s_flight.bugs.Add(s_sessionRes.bugs[i]);      // judged when the step lands
        s_flight.task = next;
        if (!string.IsNullOrEmpty(SimulatedRun.LastKind)) s_flight.kind = SimulatedRun.LastKind;
        if (SimulatedRun.LastVessel != null) s_flight.vessel = SimulatedRun.LastVessel;
        // The glass that REACTED outranks the one the handler noted: on a rack step the
        // observation belongs to the tube that changed, not to the last one served.
        if (SimulatedRun.LastReactionVessel != null) s_flight.vessel = SimulatedRun.LastReactionVessel;
        if (s_flight.vessel == null) s_flight.vessel = DestinationOf(next.taskId);
        if (SimulatedRun.LastTargetC > 0f) s_flight.targetC = SimulatedRun.LastTargetC;
        foreach (var r in SimulatedRun.LastReactions) if (!s_flight.rules.Contains(r)) s_flight.rules.Add(r);
        SetBeat("run:" + next.taskId);
    }

    /// The player keeps holding it there while the game systems work. Each branch
    /// re-applies exactly the physical arrangement the step needs; nothing completes a
    /// task here, it only keeps the real mechanism supplied.
    static void SustainStep(ExperimentRunner runner, InFlight f)
    {
        string id = f.task.taskId;

        // The distillate stream: keep the receiver at the delivery tube and the source at
        // temperature, then let VaporCollectController.Update run the condensation.
        foreach (var v in Object.FindObjectsByType<VaporCollectController>())
        {
            if (v == null || v.VaporTaskId != id || v.Source == null) continue;
            var receiver = DestinationOf(id);
            if (receiver != null && receiver != v.Source)
            {
                float d = Vector3.Distance(receiver.transform.position, v.Source.transform.position);
                if (d > VaporMath.DeliveryRadius * 0.6f)
                    receiver.transform.position = v.Source.transform.position
                                                  + v.Source.transform.right * (VaporMath.DeliveryRadius * 0.5f);
            }
            HoldAtHeat(v.Source, 0.25f, v.RequiredC);
        }

        // CO2 into the limewater: the delivery tube stays in the tube.
        foreach (var fc in Object.FindObjectsByType<FermentationController>())
        {
            if (fc == null || fc.FermentTaskId != id || fc.Limewater == null) continue;
            var lime = DestinationOf(id);
            if (lime != null && lime.currentChemical == fc.Limewater) fc.BubbleInto(lime);
        }

        // The non-flammability confirm: the player keeps a lit flame at the sample until
        // it is satisfied that the liquid will not catch (Exp 7). One PollFlames inside a
        // single tick is a glance, not the act of holding it there.
        foreach (var ft in Object.FindObjectsByType<VesselFlameTask>())
        {
            if (ft == null || ft.TaskId != id) continue;
            // ⛔ An ACTIVE burner only. Including inactive ones hands back the methane-only
            // Prop_burner, which is switched off in every other module — igniting it does
            // nothing and the dish gets carried to a hidden object instead of a flame.
            BurnerController burner = null;
            foreach (var bu in Object.FindObjectsByType<BurnerController>())
                if (bu != null && bu.gameObject.activeInHierarchy) { burner = bu; break; }
            var dish = ft.GetComponent<LiquidPhysics>();
            if (burner == null || dish == null) continue;
            burner.Ignite();
            dish.transform.position = FlameTestMath.FlamePos(burner);
            ft.PollFlames();
        }

        // Heat / chill steps: the vessel stays in the bath or in the ice.
        foreach (var h in Object.FindObjectsByType<VesselHeatTask>())
            if (h != null && h.TaskId == id) HoldAtHeat(h.GetComponent<LiquidPhysics>(), 0.25f, h.RequiredC);
        foreach (var c in Object.FindObjectsByType<VesselChillTask>())
            if (c != null && c.TaskId == id)
            {
                var ice = Object.FindAnyObjectByType<IceBathController>();
                if (ice != null) ice.ChillVessel(c.GetComponent<LiquidPhysics>());
            }

        runner.Graph.Tick();
    }

    /// Hold a vessel at its heat source: the water bath for anything up to 100 C, a lit
    /// bench burner beyond it. Mirrors what the player physically does.
    static void HoldAtHeat(LiquidPhysics vessel, float dt, float needC)
    {
        if (vessel == null) return;
        // Above the bath ceiling the procedure is an OPEN FLAME (Exp 6 dry distillation):
        // light the bench burner and hold the tube in it. At or below it, the water bath.
        if (needC > WaterBathMath.BathMaxC)
        {
            var flame = Object.FindAnyObjectByType<NakedFlameHeat>();
            var burner = flame != null ? flame.GetComponent<BurnerController>() : null;
            if (flame != null && burner != null) { burner.Ignite(); flame.HeatVessel(vessel, dt); }
            return;
        }
        var bath = Object.FindAnyObjectByType<WaterBathController>();
        if (bath != null && bath.HasWater) { bath.DriveForTest(dt); bath.HeatVessel(vessel); }
    }

    /// Land the step: photograph it next tick, count it, clear the flight.
    static void LandStep(bool honest)
    {
        var f = s_flight; s_flight = null;
        if (f == null) return;
        foreach (var b in f.bugs)
        {
            if (honest && IsCompletionNoise(b)) { Trace("(self-resolved on real frames) " + f.task.taskId + ": " + b); continue; }
            Finding(CurrentModule + " / " + f.task.taskId + ": " + b);
        }
        if (honest) s_honest++;
        s_tasksCompleted++; s_moduleTasks++;
        s_pending = new Pending
        {
            task = f.task, kind = f.kind, module = CurrentModule, vessel = f.vessel,
            rules = new List<ReactionRule>(f.rules), targetC = f.targetC, honest = honest,
        };
        s_lastAction = ""; s_repeats = 0;      // the next step is a new situation
    }

    /// Can a player actually HANDLE what this step needs? The chemistry can be perfect
    /// while the bottle cannot be tipped. GrabTest already proves the grab machinery; this
    /// adds the pour affordance (spillable, grabbable, activatable).
    static void AuditHandling(string taskId)
    {
        foreach (var t in TaskTargetRegistry.Targets(taskId))
        {
            if (t.transform == null || !s_handled.Add(t.transform)) continue;
            // Only what the player POURS FROM needs a pourer: the source bottle or vessel of a
            // step. A destination (the mortar, the methane tube) is filled, never tipped,
            // so flagging it would be a false positive.
            if (t.role != TargetRole.Source) continue;
            var lp = t.transform.GetComponent<LiquidPhysics>();
            if (lp == null) continue;
            if (t.transform.GetComponent<XRGrab>() == null) continue;   // fixed apparatus pours nothing
            if (t.transform.GetComponent<LiquidPourer>() == null)
                Finding(CurrentModule + " / " + taskId + ": " + t.transform.name + " is poured from and can "
                        + "be picked up, but has NO LiquidPourer — tipping it spills nothing");
        }
    }
    static readonly HashSet<Transform> s_handled = new HashSet<Transform>();

    static LiquidPhysics DestinationOf(string taskId)
    {
        foreach (var t in TaskTargetRegistry.Targets(taskId))
        {
            if (t.role != TargetRole.Destination || t.transform == null) continue;
            var lp = t.transform.GetComponent<LiquidPhysics>();
            if (lp != null) return lp;
        }
        return null;
    }

    static void CaptureStep()
    {
        var p = s_pending; s_pending = null;
        try
        {
            var v = VisualSweep.Record(p.module, p.task, p.kind, p.vessel, p.rules, p.targetC, p.honest);
            Trace("visual " + v.status + " " + p.task.taskId + " — " + v.reason);
            // The close-up IS the evidence — no Shot() of the player's view on top of it.
            if (v.Fail) s_findings.Add("VISUAL " + p.module + " / " + p.task.taskId + " (" + p.task.label + "): " + v.reason);
        }
        catch (System.Exception e) { Finding("visual capture threw at " + p.task.taskId + ": " + e.Message); }
    }

    // ---- quiz + grade --------------------------------------------------------------

    static void DriveQuiz()
    {
        var quiz = Object.FindAnyObjectByType<PostLabController>();
        if (quiz == null) { StallCheck("QuizTime but no PostLabController in the scene"); return; }
        if (!quiz.IsOpen) { StallCheck("QuizTime but the tablet never opened"); return; }

        s_reachedQuiz = true;
        Shot("06-quiz");
        CheckUiRaycasts();
        if (!Act("submit-quiz")) return;
        // ⚠ Answer EVERY question, not a hardcoded three. Submit() refuses unless
        // AllAnswered, silently (it only writes feedback text), so a bank with a
        // different question count stalls the sweep with no visible cause.
        int n = quiz.Bank != null ? quiz.Bank.Count : 0;
        for (int q = 0; q < n; q++) quiz.Answer(q, 0);
        quiz.Submit();
        if (quiz.IsOpen)
            Trace("submit refused (" + n + " answered): " + quiz.LastFeedback);
        SetBeat("quiz-submitted");
    }

    static void DriveGrade(PharmeeGatekeeper gate)
    {
        var card = Object.FindAnyObjectByType<GradeScreenController>(FindObjectsInactive.Include);
        if (card == null) { StallCheck("ScoreReview but no GradeScreenController"); return; }
        s_reachedGrade = true;
        if (!Act("leave-grade:" + s_moduleIndex)) return;

        bool last = s_moduleIndex >= s_queue.Count - 1;
        if (last)
        {
            // Exercise the FULL return chain once — Returning → Debrief → UnlockAnnounce
            // → Blocked — which the fast path below deliberately skips.
            Trace("continuing through the return/debrief/unlock chain");
            if (!gate.Model.Fire(GateEvent.ContinueAfterPass)) gate.OnAbandonAfterFail();
            EndModule();
            SetBeat("after-grade");
            return;
        }

        // Between modules take the ABANDON exit: §9 says it lands in Blocked with a full
        // reset and no debrief, which is the clean, pass-or-fail-agnostic way back to the
        // picker. Continue would depend on having passed, and the autopilot answers 3
        // quiz questions with option 0 — it is not here to score.
        gate.OnAbandonAfterFail();
        EndModule();
        SetBeat("next-module");
    }

    // ---- live-only checks ----------------------------------------------------------

    /// Fire a real pointer event at every visible Button and confirm something is hit
    /// (a world-space canvas without an XR raycaster leaves the quiz unclickable).
    static void CheckUiRaycasts()
    {
        var es = Object.FindAnyObjectByType<EventSystem>();
        if (es == null) { Finding("no EventSystem in the scene — no UI anywhere can be clicked"); return; }

        foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude))
        {
            if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
            var canvas = b.GetComponentInParent<Canvas>();
            var caster = canvas != null ? canvas.GetComponent<GraphicRaycaster>() : null;
            s_uiTested++;
            if (caster == null)
            {
                Finding("button '" + b.name + "' is on a canvas with NO GraphicRaycaster — "
                        + "it renders but can never be pressed");
                continue;
            }
            s_uiOk++;
        }
    }

    // ---- plumbing ------------------------------------------------------------------

    static void StallCheck(string what)
    {
        if (EditorApplication.timeSinceStartup - s_beatSince < BeatTimeout) return;
        Finding("STALL (" + BeatTimeout + "s at beat '" + s_beat + "'): " + what);
        Finish("stalled");
    }

    static readonly HashSet<string> s_shots = new HashSet<string>();

    static void Shot(string label)
    {
        // Namespaced per module, or module 2's beats silently reuse module 1's images.
        label = (s_moduleIndex + 1).ToString("00") + "-" + (CurrentModule ?? "end") + "-" + label;
        if (!s_shots.Add(label)) return;            // one image per beat, not one per tick
        Directory.CreateDirectory(ShotDir);
        ScreenCapture.CaptureScreenshot(ShotDir + "/" + label + ".png");
        Trace("screenshot " + label + ".png");
    }

    static void Finish(string why)
    {
        if (!s_running) return;
        s_running = false;
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        if (s_session != null) { s_session.End(); s_session = null; }
        SimulatedRun.MidVerb = null;
        SimulatedRun.NeverForce = false;   // edit-mode audits still force past to keep walking
        if (s_visual)
            VisualSweep.WriteReport("ended: " + why + " · " + s_moduleLog.Count + "/" + s_queue.Count + " modules · "
                                    + s_honest + "/" + s_tasksCompleted + " steps completed by the honest verbs");

        var sb = new StringBuilder();
        sb.AppendLine("=== PharmaSynth — autopilot playtest (PLAY MODE, "
                      + (s_tutorial ? "TUTORIAL" : s_visual ? "VISUAL" : "CAMPAIGN") + ") ===");
        sb.AppendLine("  ended: " + why + " after "
                      + (EditorApplication.timeSinceStartup - s_startedAt).ToString("0") + "s");
        sb.AppendLine();
        sb.AppendLine("  findings         : " + s_findings.Count);
        sb.AppendLine("  distinct errors  : " + s_errors.Count);
        sb.AppendLine("  grabs            : " + s_grabsOk + "/" + s_grabsTested + " objects CAN be picked up (capability, not feel)");
        sb.AppendLine("  buttons          : " + s_uiOk + "/" + s_uiTested + " are actually clickable");
        sb.AppendLine("  screenshots      : " + ShotDir + "/");
        sb.AppendLine();

        if (s_moduleLog.Count > 0)
        {
            sb.AppendLine("  module                        tasks      grabs        findings      time");
            sb.AppendLine("  " + new string('-', 76));
            foreach (var m in s_moduleLog) sb.AppendLine(m);
            sb.AppendLine();
        }
        if (s_pathLog.Count > 0)
        {
            sb.AppendLine("  ground path at run start (W5.44)");
            sb.AppendLine("  " + new string('-', 76));
            foreach (var l in s_pathLog) sb.AppendLine(l);
            sb.AppendLine();
        }
        if (s_visual)
        {
            sb.AppendLine("  visual sweep (W5.45): " + VisualSweep.Photographed + " close-ups · OK " + VisualSweep.Ok
                          + " · FAIL " + VisualSweep.Fails + " · SKIP " + VisualSweep.Skips
                          + " · honest completions " + s_honest + "/" + s_tasksCompleted);
            foreach (var m in s_queue) sb.AppendLine("    " + m.PadRight(30) + VisualSweep.Summary(m));
            sb.AppendLine("    report → " + VisualSweep.Report + "   pictures → " + VisualSweep.Dir + "/");
            sb.AppendLine();
        }
        sb.AppendLine("  modules played    : " + s_moduleLog.Count + "/" + s_queue.Count);
        sb.AppendLine("  reached           : lab=" + s_reachedLab + " run=" + s_reachedRunning
                      + " quiz=" + s_reachedQuiz + " grade=" + s_reachedGrade
                      + " (" + s_tasksCompleted + " tasks completed)");
        sb.AppendLine();

        // ⛔ COVERAGE IS PART OF THE VERDICT. A run that never leaves the cube room would
        // still print "0 findings — CLEAN", because nothing has happened yet. A false
        // NEGATIVE is worse than a false positive: nobody goes looking for it.
        // Coverage means every module actually played, not just the first one.
        // A practice run never reaches quiz or grade, so coverage there means: every module
        // actually RAN. Holding tutorial to the campaign bar would report INCONCLUSIVE on a
        // perfectly good sweep.
        bool covered = s_reachedRunning && s_tasksCompleted > 0 && s_moduleLog.Count == s_queue.Count;
        bool quiet = s_findings.Count == 0 && s_errors.Count == 0;
        bool clean = covered && quiet;

        if (clean)
            sb.AppendLine("  VERDICT: CLEAN — the game ran with no errors, everything a step needs picks"
                          + "\n           up, and every button can be pressed.");
        else if (!covered)
            sb.AppendLine("  VERDICT: INCONCLUSIVE — only " + s_moduleLog.Count + " of "
                          + s_queue.Count + " modules were played through."
                          + "\n           " + (s_findings.Count + s_errors.Count) + " issue(s) below; "
                          + "treat a quiet report here as NO evidence, not good news.");
        else
            sb.AppendLine("  VERDICT: " + (s_findings.Count + s_errors.Count) + " ISSUE(S) — below.");

        if (s_findings.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- findings ---");
            foreach (var f in s_findings) sb.AppendLine("  BUG  " + f);
        }
        if (s_errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("--- console errors (deduplicated; count = how many times it fired) ---");
            foreach (var kv in s_errors)
                sb.AppendLine("  x" + kv.Value + "  [beat " + s_errorBeat[kv.Key] + "]  " + kv.Key);
        }

        sb.AppendLine();
        sb.AppendLine("--- trace ---");
        foreach (var t in s_trace) sb.AppendLine("  " + t);

        sb.AppendLine();
        sb.AppendLine("--- what this still CANNOT tell you ---------------------------------");
        sb.AppendLine("  It ran the game; it did not FEEL it. Grab comfort, whether a glow reads");
        sb.AppendLine("  as a hint, label legibility at arm's length, motion comfort and the");
        sb.AppendLine("  Quest 3 frame budget are all still headset questions.");

        Directory.CreateDirectory("Logs");
        File.WriteAllText(Report, sb.ToString());
        Debug.Log((clean ? "<color=#4CD07D>" : "<color=#FF7A6B>")
                  + "[Autopilot] " + (clean ? "CLEAN"
                        : !covered ? "INCONCLUSIVE (never reached a run)"
                        : (s_findings.Count + s_errors.Count) + " issue(s)")
                  + "</color>\n  report → " + Report);

        EditorApplication.delayCall += () => { if (Application.isPlaying) EditorApplication.ExitPlaymode(); };
    }
}
#endif
