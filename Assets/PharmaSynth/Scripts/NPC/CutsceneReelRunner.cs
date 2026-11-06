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
    public const string OutDir = "Logs/cutscene-reel";
    const int W = 1920, H = 1080, Fps = 30;

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

        _rt = new RenderTexture(W, H, 24);
        _tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        var cg = new GameObject("ReelCapture");
        _cap = cg.AddComponent<Camera>();
        _cap.enabled = false;
        _cap.clearFlags = CameraClearFlags.SolidColor; _cap.backgroundColor = Color.black;
        _cap.cullingMask = 1 << CinemaView.Layer;
        _cap.fieldOfView = 44f; _cap.nearClipPlane = 0.05f;
        _cap.targetTexture = _rt;
        var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        data.allowXRRendering = false;

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

    IEnumerator Clip(int index, string label, System.Action play, System.Func<bool> busy, float tail = 0.6f)
    {
        string file = Path.Combine(OutDir, $"clip{index:00}.mp4");
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
        while (quiet < tailFrames && guard++ < Fps * 60)
        {
            yield return new WaitForEndOfFrame();
            var head = Camera.main != null ? Camera.main.transform : null;
            if (head != null)
            {
                Vector3 f = head.forward; f.y = 0f; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
                _cap.transform.SetPositionAndRotation(head.position, Quaternion.LookRotation(f.normalized) * Quaternion.Euler(6f, 0f, 0f));
            }
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
        File.WriteAllText(Path.Combine(OutDir, "manifest.txt"), _manifest.ToString());
        Debug.Log($"[Reel] {label}: {_frame} frames → {file}");
    }

    void Finish()
    {
        Time.captureFramerate = 0;
        foreach (var (n, h) in _hooks) if (n != null) n.LineStarted -= h;
        File.WriteAllText(Path.Combine(OutDir, "manifest.txt"), _manifest.ToString());
        File.WriteAllText(Path.Combine(OutDir, "done.txt"), System.DateTime.Now.ToString("s"));
        EditorApplication.ExitPlaymode();
    }
}
#endif
