using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Builder + review menus for cinematic cutscenes (CinemaView / CinemaShot).
///  - Build Cutscene Markers: idempotent; creates the `CutsceneMarkers` root (with the
///    CinemaView) and any MISSING marker at a default pose. Never moves an existing
///    marker — hand-tuned framing survives a re-run.
///  - Render Test Sequence Shots: edit mode; writes the start/middle/end frame of every
///    shot to Logs/cutscene-shots/test/ so framing is reviewable without Play mode.
///  - Preview Test Sequence: Play mode; plays it for real (fades, subtitles, return).
public static class CinemaCutsceneTools
{
    public const string Menu = "Tools/PharmaSynth/Cutscenes/";

    /// name → (position, point it looks at). Defaults only; tune in the scene.
    public static readonly (string name, Vector3 pos, Vector3 look)[] DefaultMarkers =
    {
        // Lab interior: x -5.4..4.5, z -7.7..-0.1; centre island bench x -2.6..1.6, z -3.1..-4.5.
        ("Marker_Bench",         new Vector3(-0.50f, 0.95f, -3.80f), new Vector3(-0.50f, 0.95f, -2.80f)),
        // Pharmee floats at her normal 1.05 m: her speech bubble sits 1.1-1.4 m above that,
        // so parking her any higher pushes it into the 2.64 m ceiling.
        ("Marker_PharmeeCorner", new Vector3(-3.50f, 1.05f, -5.90f), new Vector3(-0.50f, 1.05f, -3.80f)),
        ("Cam_LabWide",          new Vector3(-4.60f, 2.20f, -0.70f), new Vector3( 0.20f, 0.90f, -4.60f)),
        ("Cam_PharmeeClose",     new Vector3(-1.70f, 1.70f, -4.65f), new Vector3(-3.50f, 1.75f, -5.90f)),
        ("Cam_Bench",            new Vector3(-1.00f, 1.70f, -5.70f), new Vector3(-0.50f, 0.95f, -3.80f)),
        ("Cam_Timelapse",        new Vector3( 4.30f, 2.45f, -7.30f), new Vector3(-0.50f, 0.95f, -3.80f)),
    };

    [MenuItem(Menu + "Build Cutscene Markers")]
    public static void BuildMarkers()
    {
        var root = GameObject.Find(CinemaView.MarkersRoot);
        if (root == null)
        {
            root = new GameObject(CinemaView.MarkersRoot);
            Undo.RegisterCreatedObjectUndo(root, "Build Cutscene Markers");
        }
        if (root.GetComponent<CinemaView>() == null) Undo.AddComponent<CinemaView>(root);
        int made = 0;
        foreach (var m in DefaultMarkers)
        {
            if (root.transform.Find(m.name) != null) continue;
            var g = new GameObject(m.name);
            Undo.RegisterCreatedObjectUndo(g, "Build Cutscene Markers");
            g.transform.SetParent(root.transform, false);
            g.transform.SetPositionAndRotation(m.pos, Quaternion.LookRotation(m.look - m.pos, Vector3.up));
            made++;
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Cinema] CutsceneMarkers ready ({made} marker(s) created, existing ones untouched).");
    }

    /// The prep-complete template from the client's slides: wide shot as Pharmee pops
    /// out of the corner → glide to her close-up → pan through the lab to the bench.
    public static CutsceneData TestSequence()
    {
        var c = ScriptableObject.CreateInstance<CutsceneData>();
        c.name = "CinemaTest";
        c.kind = CutsceneData.Kind.ReagentPrep;
        c.title = "Preparations Complete";
        c.beats = new List<CutsceneData.Beat>
        {
            new CutsceneData.Beat { subtitle = "Good job! You have prepared every reagent this experiment needs.", seconds = 3.8f,
                face = PharmeeFaceExpression.Happy, shot = TemplateShot(c.kind, 0) },
            new CutsceneData.Beat { subtitle = "Now head to the bench and run the experiment. Good luck!", seconds = 4.2f,
                shot = TemplateShot(c.kind, 1) },
        };
        return c;
    }

    public const string LibraryPath = "Assets/PharmaSynth/ScriptableObjects/CutsceneLibrary.asset";

    /// The slides' template (cutscenes-sample.pdf), by beat. The cinematic opens on
    /// Cam_LabWide under the title card; then:
    ///   beat 1 — Pharmee pops out of the corner, the camera glides in to her close-up;
    ///   beat 2 — it pans through the lab to the bench (the player's PRODUCT on a pass);
    ///   beat 3+ — holds that framing.
    /// Intro (not in the slides, built to match them): beat 1 pops Pharmee out and glides
    /// in, the objectives hold on her, and the LAST beat pans to the bench.
    public static CinemaShot TemplateShot(CutsceneData.Kind kind, int beat, int beatCount = 0)
    {
        if (kind == CutsceneData.Kind.Intro && beat > 0)
            return beat == beatCount - 1 && beatCount > 2
                ? new CinemaShot { camera = "Cam_Bench", blend = true, look = CinemaLook.Bench, fov = 50f }
                : new CinemaShot();
        if (beat == 0)
            return new CinemaShot { camera = "Cam_PharmeeClose", blend = true, look = CinemaLook.Pharmee, fov = 45f, pharmeePopIn = true };
        if (beat == 1)
            return new CinemaShot { camera = "Cam_Bench", blend = true,
                look = kind == CutsceneData.Kind.Success ? CinemaLook.Product : CinemaLook.Bench, fov = 50f };
        return new CinemaShot();
    }

    public static IEnumerable<CutsceneData> TemplatedCutscenes(CutsceneLibrary lib)
    {
        if (lib == null) yield break;
        foreach (var e in lib.entries)
        {
            if (e == null) continue;
            if (e.intro != null) yield return e.intro;
            if (e.reagentPrep != null) yield return e.reagentPrep;
            if (e.success != null) yield return e.success;
            if (e.failure != null) yield return e.failure;
        }
    }

    /// Idempotent: (re)writes the template's shot onto every beat of every prep /
    /// success / failure cutscene in the library. Subtitles, faces and timings untouched.
    [MenuItem(Menu + "Apply Cutscene Shots")]
    public static int ApplyShots()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>(LibraryPath);
        if (lib == null) { Debug.LogError("[Cinema] CutsceneLibrary not found at " + LibraryPath); return 0; }
        int n = 0;
        foreach (var c in TemplatedCutscenes(lib))
        {
            for (int i = 0; i < c.beats.Count; i++)
                if (c.beats[i] != null) c.beats[i].shot = TemplateShot(c.kind, i, c.beats.Count);
            EditorUtility.SetDirty(c);
            n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Cinema] applied the slide template to {n} cutscene(s).");
        return n;
    }

    [MenuItem(Menu + "Render Library Shots (all modules)")]
    public static void RenderLibrary()
    {
        var lib = AssetDatabase.LoadAssetAtPath<CutsceneLibrary>(LibraryPath);
        foreach (var c in TemplatedCutscenes(lib)) RenderShots(c, c.name);
    }

    [MenuItem(Menu + "Preview Timelapse (Play mode)")]
    public static void PreviewTimelapse()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Cinema] Enter Play mode first (Headset Play Mode OFF is fine)."); return; }
        var ts = Object.FindAnyObjectByType<TimeSkipController>();
        if (ts == null) { Debug.LogError("[Cinema] no TimeSkipController in the scene."); return; }
        ts.PlayTimelapse(new ExperimentTask { taskId = "preview", longProcess = true, timelapseHours = 168f,
            longProcessMessage = "Seven days and seven nights later - fermentation is complete." });
    }

    [MenuItem(Menu + "Preview Test Sequence (Play mode)")]
    public static void PreviewTest()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Cinema] Enter Play mode first (Headset Play Mode OFF is fine)."); return; }
        var d = Object.FindAnyObjectByType<CutsceneDirector>();
        if (d == null) { Debug.LogError("[Cinema] no CutsceneDirector in the scene."); return; }
        d.Play(TestSequence());
    }

    [MenuItem(Menu + "Render Test Sequence Shots")]
    public static void RenderTest() => RenderShots(TestSequence(), "test");

    /// Edit-mode framing review: start / middle / end frame of every beat's shot.
    public static int RenderShots(CutsceneData data, string folder)
    {
        var cinema = Object.FindAnyObjectByType<CinemaView>();
        var pharmee = Object.FindAnyObjectByType<CutsceneDirector>();
        if (cinema == null || cinema.Markers == null) { Debug.LogError("[Cinema] run Build Cutscene Markers first."); return 0; }
        string dir = Path.Combine("Logs", "cutscene-shots", folder);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);

        Transform pt = pharmee != null ? pharmee.transform : null;
        Vector3 savedPos = pt != null ? pt.position : Vector3.zero;
        var cam = cinema.ShotCamera;
        var pose = cinema.MarkerPose("Cam_LabWide") ?? new ShotMath.Pose(cam.transform.position, cam.transform.rotation);
        int written = 0;
        try
        {
            for (int i = 0; i < data.beats.Count; i++)
            {
                var b = data.beats[i]; var s = b.shot;
                if (s == null || !s.HasCamera && string.IsNullOrEmpty(s.moveTo)) continue;
                if (s.pharmeePopIn && pt != null && cinema.Marker("Marker_PharmeeCorner") is Transform corner)
                    pt.position = corner.position;
                ShotMath.Ends(s.blend, pose, cinema.MarkerPose(s.camera), cinema.MarkerPose(s.moveTo), out var a, out var e);
                foreach (float k in new[] { 0f, 0.5f, 1f })
                {
                    var look = cinema.Resolve(s.look, s.lookMarker, null, pt);
                    var p = ShotMath.Evaluate(a, e, s.blend, look, k);
                    cinema.SetPose(p, s.fov);
                    string file = Path.Combine(dir, $"beat{i + 1}_{(int)(k * 100):000}.png");
                    WritePng(cam, file);
                    written++;
                    if (k >= 1f) pose = p;
                }
            }
        }
        finally { if (pt != null) pt.position = savedPos; }
        Debug.Log($"[Cinema] wrote {written} frame(s) to {dir}");
        return written;
    }

    private static void WritePng(Camera cam, string file)
    {
        var rt = cam.targetTexture;
        bool was = cam.enabled;
        cam.Render();
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        cam.enabled = was;
        File.WriteAllBytes(file, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }
}
