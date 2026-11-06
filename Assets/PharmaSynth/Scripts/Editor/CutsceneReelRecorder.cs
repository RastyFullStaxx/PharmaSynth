using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// Records every cinematic cutscene and timelapse to video, as the player sees it
/// (the dark room, the screen, the title card and subtitles), for client review.
///
/// `Cutscenes ▸ Record Cutscene Reel` enters Play mode; the runner plays the 27
/// prep/success/failure cinematics and the 6 timelapses back to back at a locked
/// 30 fps (Time.captureFramerate, so slow capture never changes timing) and pipes
/// raw frames into ffmpeg — one silent MP4 per clip under Logs/cutscene-reel/.
/// Voice lines are logged with their frame offsets in manifest.txt so the audio
/// can be laid back on offline (Tools/cutscene-reel.py), since captureFramerate
/// decouples game time from the audio clock.
[InitializeOnLoad]
public static class CutsceneReelRecorder
{
    public const string OutDir = CutsceneReelRunner.OutDir;
    const string Request = "Logs/cutscene-reel-request.txt";   // Logs/, not Temp/ — survives the Play-mode domain reload

    static CutsceneReelRecorder()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s != PlayModeStateChange.EnteredPlayMode || !File.Exists(Request)) return;
            string ffmpeg = File.ReadAllText(Request).Trim();
            File.Delete(Request);
            var go = new GameObject("CutsceneReelRunner");
            go.AddComponent<CutsceneReelRunner>().ffmpeg = ffmpeg;
        };
    }

    [MenuItem(CinemaCutsceneTools.Menu + "Record Cutscene Reel (enters Play mode)")]
    public static void Record()
    {
        string ffmpeg = FindFfmpeg();
        if (ffmpeg == null) { Debug.LogError("[Reel] ffmpeg.exe not found on PATH or in WinGet packages."); return; }
        Directory.CreateDirectory(OutDir);
        foreach (var f in Directory.GetFiles(OutDir)) File.Delete(f);
        File.WriteAllText(Request, ffmpeg);
        EditorApplication.EnterPlaymode();
    }

    static string FindFfmpeg()
    {
        foreach (var dir in (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
        {
            try { var p = Path.Combine(dir.Trim(), "ffmpeg.exe"); if (File.Exists(p)) return p; } catch { }
        }
        string wg = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                                 "Microsoft", "WinGet", "Packages");
        if (Directory.Exists(wg))
            foreach (var p in Directory.GetFiles(wg, "ffmpeg.exe", SearchOption.AllDirectories)) return p;
        return null;
    }
}

