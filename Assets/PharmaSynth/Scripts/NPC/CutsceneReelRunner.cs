#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// Play-mode half of CutsceneReelRecorder (Editor/): plays each clip and pipes frames to
/// ffmpeg. Editor-only — a component must live in the runtime assembly to be added, and
/// the UNITY_EDITOR guard keeps it (and its UnityEditor calls) out of every build.
public class CutsceneReelRunner : MonoBehaviour
{
    public string ffmpeg;
    /// "showcase" records the arrival (cube room through to the lab door, one clip across
    /// the scene load) and the campaign-complete ending instead of the cutscene library.
    public bool showcase;
    public const string OutDir = "Logs/cutscene-reel";
    public const string ShowcaseDir = "Logs/showcase-reel";
    string Dir => showcase ? ShowcaseDir : OutDir;
    const int W = 1920, H = 1080, Fps = 30;
    /// Metres behind the viewing distance and below the screen's centre the capture sits.
    const float CaptureBack = 0.2f, CaptureDrop = 0.15f;

    Camera _cap;
    RenderTexture _rt;
    Texture2D _tex;
    int _frame;
    readonly StringBuilder _manifest = new StringBuilder();
    readonly List<(NPCNarrationController n, System.Action<string, float> h)> _hooks = new List<(NPCNarrationController, System.Action<string, float>)>();

    // Awake, not Start: the boot-time MainMenu load would destroy the runner first.
    void Awake() => DontDestroyOnLoad(gameObject);

    IEnumerator Start()
    {
        if (showcase) { yield return Showcase(); yield break; }
        // Play mode boots the MainMenu cube room; go straight to the lab and survive the load.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "SampleScene")
            UnityEngine.SceneManagement.SceneManager.LoadScene("SampleScene");
        for (int i = 0; i < 600 && FindAnyObjectByType<CutsceneDirector>() == null; i++) yield return null;
        yield return new WaitForSeconds(5f);   // let the scene settle (greeting, fades)
        var director = FindAnyObjectByType<CutsceneDirector>();
        var skip = FindAnyObjectByType<TimeSkipController>();
        var cinema = FindAnyObjectByType<CinemaView>();
        var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>("Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset");
        if (director == null || cinema == null || lib == null) { Debug.LogError("[Reel] scene is missing the director / cinema / library."); Finish(); yield break; }

        MakeCapture();

        foreach (var n in FindObjectsByType<NPCNarrationController>())
        {
            var nn = n;
            System.Action<string, float> h = (line, s) =>
            {
                var clip = nn.ResolveVoice(line);
                string path = clip != null ? AssetDatabase.GetAssetPath(clip) : "";
                _manifest.AppendLine($"voice\t{_frame}\t{path}\t{line.Replace('\t', ' ').Replace('\n', ' ')}");
            };
            n.LineStarted += h; _hooks.Add((n, h));
        }

        Time.captureFramerate = Fps;
        int index = 0;
        foreach (var e in lib.entries)
        {
            if (e == null) continue;
            foreach (var (c, label) in new[] { (e.intro, "Intro"), (e.reagentPrep, "Preparations complete"), (e.success, "Experiment complete (pass)"), (e.failure, "Experiment over (fail)") })
            {
                if (c == null) continue;
                index++;
                yield return Clip(index, $"{e.moduleId}  -  {label}", () => director.Play(c),
                                  () => director.IsPlaying);
            }
        }
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition", new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var t in m.graphTasks)
            {
                if (!t.longProcess || skip == null) continue;
                index++;
                var task = t;
                yield return Clip(index, $"{m.moduleId}  -  timelapse: {t.label}", () => skip.PlayTimelapse(task),
                                  () => TimeSkipController.IsSkipping, 2.5f);
            }
        }
        Finish();
    }

    /// The capture camera films the Cinema layer (the dark room, the screen, its text) from
    /// the head, as the player sees it. Parented to the runner so it survives scene loads.
    void MakeCapture()
    {
        _rt = new RenderTexture(W, H, 24);
        _tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var cg = new GameObject("ReelCapture");
        cg.transform.SetParent(transform, false);
        _cap = cg.AddComponent<Camera>();
        _cap.enabled = false;
        _cap.clearFlags = CameraClearFlags.SolidColor; _cap.backgroundColor = Color.black;
        _cap.cullingMask = 1 << CinemaView.Layer;
        _cap.fieldOfView = 44f; _cap.nearClipPlane = 0.05f;
        _cap.targetTexture = _rt;
        var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.allowXRRendering = false;
    }

    /// Arrival: Demo Mode's Laboratory in the cube room (a demo always plays it), recorded
    /// to the lab door. Ending: the demo HUD's Play Ending, once the arrival has settled.
    IEnumerator Showcase()
    {
        // The first Play after an editor start can take a while to load; wait in real time,
        // and open the cube room ourselves if Play began anywhere else.
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MainMenu"
            && FindAnyObjectByType<MainMenuController>() == null)
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        for (float w = 0f; w < 30f && FindAnyObjectByType<MainMenuController>() == null; w += Time.unscaledDeltaTime) yield return null;
        yield return new WaitForSeconds(3f);   // the cube room's own fade-in
        var menu = FindAnyObjectByType<MainMenuController>();
        if (menu == null) { Debug.LogError("[Reel] Play mode did not start in the cube room."); Finish(); yield break; }
        MakeCapture();
        Time.captureFramerate = Fps;
        yield return Clip(1, "Arrival", menu.OnDemoLaboratory,
                          () => ArrivalFlow.Busy || (ShowcaseDirector.Find() != null && ShowcaseDirector.Find().Playing),
                          0.6f, 180f, "PharmaSynth-arrival-silent.mp4");
        yield return new WaitForSeconds(3f);
        var show = ShowcaseDirector.Find();
        var gate = FindAnyObjectByType<PharmeeGatekeeper>();
        if (show == null || show.Ending == null) { Debug.LogError("[Reel] the lab has no ending reel."); Finish(); yield break; }
        yield return Clip(2, "Ending", () => show.TryPlayEnding(true, gate != null ? gate.CelebrateCampaign : (System.Action)null),
                          () => show.Playing, 0.6f, 180f, "PharmaSynth-ending-silent.mp4");
        Finish();
    }

    IEnumerator Clip(int index, string label, System.Action play, System.Func<bool> busy, float tail = 0.6f,
                     float maxSeconds = 60f, string name = null)
    {
        string file = Path.Combine(Dir, name ?? $"clip{index:00}.mp4");
        var p = new Process
        {
            StartInfo = new ProcessStartInfo(ffmpeg,
                $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {W}x{H} -r {Fps} -i - -vf vflip -c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p \"{Path.GetFullPath(file)}\"")
            { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true }
        };
        p.Start();
        var stdin = p.StandardInput.BaseStream;
        _frame = 0;
        _manifest.AppendLine($"clip\t{index}\t{file}\t{label}");
        play();
        int tailFrames = Mathf.RoundToInt(tail * Fps), quiet = 0, guard = 0;
        while (quiet < tailFrames && guard++ < Fps * maxSeconds)
        {
            yield return new WaitForEndOfFrame();
            // Square in front of the cinema screen, never on the head: a head that moves
            // while recording (the simulator's mouse, a worn headset) would shake the whole
            // video. Level, a little lower and further back rather than tilted down: a
            // tilted camera leans upright letters at the frame's edges (keystone), and this
            // keeps the picture and the text band under it both in frame. With no screen
            // up, the last pose holds.
            var cinema = FindAnyObjectByType<CinemaView>();
            var screen = cinema != null && cinema.Active ? cinema.ScreenTransform : null;
            if (screen != null)
                _cap.transform.SetPositionAndRotation(
                    screen.position - screen.forward * (cinema.ScreenDistance + CaptureBack) - Vector3.up * CaptureDrop,
                    Quaternion.LookRotation(screen.forward));
            _cap.Render();
            var prev = RenderTexture.active; RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, W, H), 0, 0, false);
            RenderTexture.active = prev;
            var bytes = _tex.GetRawTextureData();
            stdin.Write(bytes, 0, bytes.Length);
            _frame++;
            bool fading = ScreenFader.Instance != null && ScreenFader.Instance.State.Busy;
            quiet = busy() || fading ? 0 : quiet + 1;
        }
        stdin.Close();
        p.WaitForExit(60000);
        _manifest.AppendLine($"frames\t{index}\t{_frame}");
        File.WriteAllText(Path.Combine(Dir, "manifest.txt"), _manifest.ToString());
        Debug.Log($"[Reel] {label}: {_frame} frames → {file}");
    }

    void Finish()
    {
        Time.captureFramerate = 0;
        foreach (var (n, h) in _hooks) if (n != null) n.LineStarted -= h;
        File.WriteAllText(Path.Combine(Dir, "manifest.txt"), _manifest.ToString());
        File.WriteAllText(Path.Combine(Dir, "done.txt"), System.DateTime.Now.ToString("s"));
        EditorApplication.ExitPlaymode();
    }
}
#endif
