#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// Tools ▸ PharmaSynth ▸ Cutscenes ▸ Build Showcase Reels: wires the arrival reel into
/// both scenes and the campaign-complete ending into the lab. In each scene it makes sure
/// the CutsceneMarkers root has a CinemaView and a ShowcaseDirector, creates the camera
/// markers that are missing (existing ones keep their hand-tuned poses), creates the reel
/// assets if missing, adds the demo HUD's Play Ending button, and saves.
/// "Reset Showcase Reels" rewrites markers and reels to these defaults.
///
/// Run it on a scene freshly loaded from disk: it saves both scenes.
public static class ShowcaseReelBuilder
{
    const string Menu = CinemaCutsceneTools.Menu;
    public const string LabScene = "Assets/Scenes/SampleScene.unity";
    public const string CubeScene = "Assets/Scenes/MainMenu.unity";
    public const string Folder = "Assets/PharmaSynth/ScriptableObjects/Cutscenes";
    public const string CubeReelPath = Folder + "/Arrival_CubeRoom.asset";
    public const string LabReelPath = Folder + "/Arrival_Lab.asset";
    public const string EndingReelPath = Folder + "/Ending_Campaign.asset";
    const string ChemFolder = "Assets/PharmaSynth/ScriptableObjects/Chemicals/";
    const string MusicPath = "Assets/PharmaSynth/Audio/Background_Music/MenuRoom/music_1.mp3";

    /// name → (camera position, the point it faces). Look_* markers are aim points.
    public static readonly (string name, Vector3 pos, Vector3 look)[] CubeMarkers =
    {
        // Cube room: 6.2 m cube centred (0, 1.6, 0.5); the menu panel stands at z 2.5.
        ("Arr_CubeHighL",  new Vector3(-2.5f, 3.0f, -2.0f), new Vector3(0f, 1.3f, 1.2f)),
        ("Arr_CubeHighR",  new Vector3( 2.5f, 3.0f, -2.0f), new Vector3(0f, 1.3f, 1.2f)),
        ("Arr_CubeLowA",   new Vector3(-2.4f, 0.5f, -2.2f), new Vector3(0f, 2.4f, 0.4f)),
        ("Arr_CubeLowB",   new Vector3( 2.4f, 0.5f, -2.2f), new Vector3(0f, 2.4f, 0.4f)),
        ("Arr_CubeTitle",  new Vector3( 0.0f, 1.3f, -1.8f), new Vector3(0f, 3.2f, 0.2f)),
        ("Look_CubeCentre", new Vector3(0f, 1.3f, 1.2f), new Vector3(0f, 1.3f, 2.2f)),
        ("Look_CubeCeiling", new Vector3(0f, 2.4f, 0.4f), new Vector3(0f, 2.4f, 1.4f)),
        ("Look_CubeUp",     new Vector3(0f, 3.2f, 0.2f), new Vector3(0f, 3.2f, 1.2f)),
    };

    public static readonly (string name, Vector3 pos, Vector3 look)[] LabMarkers =
    {
        // The player spawns in the corridor (z 0..3) at (-1.35, 0, 2.23); the lab door leaf
        // hangs at x -2.35, z -0.18; Pharmee waits at (-1.55, 1.05, 0.55); the lab runs
        // x -5.4..4.5, z -7.7..-0.1 behind the glass wall at z 0.
        ("Arr_Corridor",     new Vector3(-0.40f, 1.70f,  2.60f), new Vector3(-2.35f, 1.2f, -0.2f)),
        ("Arr_CorridorIn",   new Vector3(-2.20f, 1.60f,  1.75f), new Vector3(-2.35f, 1.2f, -0.2f)),
        ("Arr_SweepA",       new Vector3(-4.80f, 2.30f, -0.60f), new Vector3(-0.5f, 0.95f, -3.8f)),
        ("Arr_SweepB",       new Vector3( 3.60f, 2.30f, -0.60f), new Vector3(-0.5f, 0.95f, -3.8f)),
        ("Arr_CabinetA",     new Vector3( 2.80f, 1.45f, -1.40f), new Vector3( 4.1f, 1.1f, -2.77f)),
        ("Arr_CabinetB",     new Vector3( 2.80f, 1.45f, -4.10f), new Vector3( 4.1f, 1.1f, -2.77f)),
        ("Arr_ShelfA",       new Vector3(-2.30f, 1.55f, -4.75f), new Vector3(-0.9f, 1.45f, -3.84f)),
        ("Arr_ShelfB",       new Vector3( 0.50f, 1.55f, -4.75f), new Vector3(-0.9f, 1.45f, -3.84f)),
        ("Arr_Burner",       new Vector3(-0.10f, 1.22f, -4.72f), new Vector3(-0.44f, 1.02f, -4.31f)),
        ("Arr_Pour",         new Vector3(-0.68f, 1.70f, -4.30f), new Vector3(-0.68f, 1.62f, -3.84f)),
        ("Arr_Pharmee",      new Vector3(-0.15f, 1.60f,  2.40f), new Vector3(-1.55f, 1.45f, 0.55f)),
        ("Arr_Jimenez",      new Vector3( 1.55f, 1.60f, -1.90f), new Vector3( 2.9f, 1.55f, -1.1f)),
        ("Arr_BenchHigh",    new Vector3( 1.60f, 2.40f, -6.30f), new Vector3(-0.5f, 0.95f, -3.8f)),
        ("Arr_BenchLow",     new Vector3(-3.20f, 1.20f, -5.60f), new Vector3(-0.5f, 0.95f, -3.8f)),
        ("Arr_DoorInside",   new Vector3(-2.35f, 1.60f, -2.40f), new Vector3(-2.35f, 1.3f, 0.5f)),
        ("Look_Bench",       new Vector3(-0.5f, 0.95f, -3.8f),   new Vector3(-0.5f, 0.95f, -2.8f)),
        ("Look_Cabinets",    new Vector3( 4.1f, 1.1f, -2.77f),   new Vector3( 5.1f, 1.1f, -2.77f)),
        ("Look_Shelf",       new Vector3(-0.9f, 1.45f, -3.84f),  new Vector3(-0.9f, 1.45f, -2.84f)),
        ("Look_Burner",      new Vector3(-0.44f, 1.02f, -4.31f), new Vector3(-0.44f, 1.02f, -3.31f)),
        ("Look_Pour",        new Vector3(-0.68f, 1.62f, -3.84f), new Vector3(-0.68f, 1.62f, -2.84f)),
        ("Look_Pharmee",     new Vector3(-1.55f, 1.45f, 0.55f),  new Vector3(-1.55f, 1.45f, 1.55f)),
        ("Look_Jimenez",     new Vector3( 2.9f, 1.55f, -1.1f),   new Vector3( 2.9f, 1.55f, -0.1f)),
        ("Look_Door",        new Vector3(-2.35f, 1.3f, 0.5f),    new Vector3(-2.35f, 1.3f, 1.5f)),
        ("Look_Corridor",    new Vector3(-2.35f, 1.2f, -0.2f),   new Vector3(-2.35f, 1.2f, -1.2f)),
    };

    static CinemaShot Shot(string cam, string to, string look, float fov = 50f, bool blend = false)
        => new CinemaShot { camera = cam, moveTo = to, blend = blend, look = CinemaLook.Marker, lookMarker = look, fov = fov };

    static CutsceneData.Beat B(float s, CinemaShot shot, string caption = "", string card = "", string line = "",
                               ShowcaseAction action = ShowcaseAction.None, string target = "")
        => new CutsceneData.Beat { seconds = s, shot = shot, caption = caption, cardTitle = card, cardLine = line,
                                   action = action, actionTarget = target, subtitle = "" };

    /// The lab's name tags show for the shot camera during this beat.
    static CutsceneData.Beat Labelled(CutsceneData.Beat b) { b.showLabels = true; return b; }

    static CutsceneData.Beat Fx(CutsceneData.Beat b, string effects) { b.effects = effects; return b; }

    /// Drama on the glassware shelf: tubes and beakers cycle colours, the Florence flask
    /// bubbles, the water bath steams, a distilling flask smokes.
    const string ShelfEffects = "shift:Kit_TestTube_0|shift:Kit_TestTube_1|shift:Kit_TestTube_2|steam:WaterBath"
        + "|shift:FlorenceFlask|fizz:FlorenceFlask|shift:Eq_Beaker_100mL|shift:ErlenmeyerFlask_400mL_2|smoke:DistillingFlask";
    static readonly string[] ShiftColours = { "Chem_PotassiumPermanganate", "Chem_BenedictsReagent", "Chem_FerricChloride10", "Chem_BromineWater" };

    static CutsceneData.Beat WithChemicals(CutsceneData.Beat b, string[] chems)
    {
        b.chemicals = new ChemicalData[chems.Length];
        for (int i = 0; i < chems.Length; i++)
            b.chemicals[i] = AssetDatabase.LoadAssetAtPath<ChemicalData>(ChemFolder + chems[i] + ".asset");
        return b;
    }

    public static List<CutsceneData.Beat> CubeBeats() => new List<CutsceneData.Beat>
    {
        B(5.0f, Shot("Arr_CubeHighL", "Arr_CubeHighR", "Look_CubeCentre", 60f)),
        B(4.0f, Shot("Arr_CubeLowA", "Arr_CubeLowB", "Look_CubeCeiling", 62f)),
        B(4.0f, Shot("Arr_CubeTitle", "", "Look_CubeUp", 60f), card: "PharmaSynth", line: "Gear Up, Synth It Up!"),
    };

    public static List<CutsceneData.Beat> LabBeats() => new List<CutsceneData.Beat>
    {
        B(5.0f, Shot("Arr_Corridor", "Arr_CorridorIn", "Look_Corridor", 55f), action: ShowcaseAction.OpenDoor),
        B(7.0f, Shot("Arr_SweepA", "Arr_SweepB", "Look_Bench", 58f), caption: "A full chemistry lab, built for VR"),
        Labelled(Fx(B(6.0f, Shot("Arr_CabinetA", "Arr_CabinetB", "Look_Cabinets", 50f), caption: "57 reagents, always on the shelf"),
          "coldair:Raw_IceBucket")),
        Labelled(WithChemicals(Fx(B(8.0f, Shot("Arr_ShelfA", "Arr_ShelfB", "Look_Shelf", 45f), caption: "Real glassware, real reactions"),
          ShelfEffects), ShiftColours)),
        Labelled(Fx(B(4.0f, Shot("Arr_Burner", "", "Look_Burner", 40f), caption: "Light the burner",
          action: ShowcaseAction.LightBurner, target: "Kit_BunsenBurner_0_9"),
          "flamepop:Kit_BunsenBurner_0_9|fireburst:Kit_BunsenBurner_0_9|smoke:Kit_BunsenBurner_0_9")),
        Labelled(Fx(B(4.5f, Shot("Arr_Pour", "", "Look_Pour", 38f), caption: "Pour every reagent by hand",
          action: ShowcaseAction.Pour, target: "Raw_PotassiumPermanganate0.1%|FlorenceFlask"),
          "fizz:FlorenceFlask|spatter:FlorenceFlask|colorflash:FlorenceFlask")),
        B(5.0f, Shot("Cam_Timelapse", "", "Look_Bench", 55f), action: ShowcaseAction.Timelapse),
        Fx(B(4.5f, Shot("Arr_Pharmee", "", "Look_Pharmee", 42f), caption: "Pharmee, your lab partner",
          action: ShowcaseAction.PharmeeCelebrate), "confetti:RobotNPC"),
        B(4.0f, Shot("Arr_Jimenez", "", "Look_Jimenez", 42f), caption: "Dr. Jimenez, your examiner"),
        B(3.0f, Shot("Arr_BenchHigh", "", "Look_Bench", 55f), card: "9 Experiments", line: "Prelim · Midterm · Finals"),
        B(2.5f, Shot("Arr_BenchLow", "", "Look_Bench", 50f), card: "Prelim", line: "Chemical Reactions · Ethyl Alcohol"),
        B(2.5f, Shot("Cam_Bench", "", "Look_Bench", 50f), card: "Midterm", line: "Benzoic Acid · Acetanilide · Acetone · Chloroform"),
        Fx(B(2.5f, Shot("Arr_BenchHigh", "Arr_SweepB", "Look_Bench", 55f), card: "Finals", line: "Benzamide · Wine Making"),
          "shatter:ErlenmeyerFlask_400mL_3|steam:WaterBath"),
        B(4.0f, Shot("Arr_DoorInside", "", "Look_Door", 55f), card: "Your lab is ready", line: "Step inside"),
    };

    public static readonly (string name, Vector3 pos, Vector3 look)[] EndingMarkers =
    {
        // The product line-up: seven beakers and flasks on the shelf's lower row.
        ("End_LineupA",      new Vector3(-0.75f, 1.38f, -4.35f), new Vector3(-0.15f, 1.28f, -3.83f)),
        ("End_LineupB",      new Vector3( 0.35f, 1.38f, -4.35f), new Vector3(-0.15f, 1.28f, -3.83f)),
        ("End_Tubes",        new Vector3(-1.45f, 1.82f, -4.25f), new Vector3(-1.64f, 1.72f, -3.83f)),
        ("End_Corridor",     new Vector3(-2.35f, 1.60f,  1.25f), new Vector3(-2.35f, 1.30f, -2.5f)),
        ("End_CorridorBack", new Vector3(-2.10f, 1.75f,  2.60f), new Vector3(-2.35f, 1.30f, -2.5f)),
        ("Look_Lineup",      new Vector3(-0.15f, 1.28f, -3.83f), new Vector3(-0.15f, 1.28f, -2.83f)),
        ("Look_Tubes",       new Vector3(-1.64f, 1.72f, -3.83f), new Vector3(-1.64f, 1.72f, -2.83f)),
        ("Look_LabFromCorridor", new Vector3(-2.35f, 1.30f, -2.5f), new Vector3(-2.35f, 1.30f, -3.5f)),
    };

    /// Products, in catalog order, and the shelf vessel each one is shown in.
    static readonly (string vessel, string chem)[] Products =
    {
        ("Eq_Beaker_100mL", "Chem_Ethanol"), ("Beaker_100mL_2", "Chem_BenzoicAcid"),
        ("Kit_Beaker_100mL_1_6", "Chem_Acetanilide"), ("ErlenmeyerFlask_400mL_2", "Chem_Acetone"),
        ("ErlenmeyerFlask_400mL_3", "Chem_Chloroform"), ("Eq_Beaker_500mL", "Chem_Benzamide"),
        ("Beaker_500mL_2", "Chem_Wine"),
    };
    /// Chemical Reactions has no product: its tests' colours stand in a row of tubes.
    static readonly (string vessel, string chem)[] TestColours =
    {
        ("Kit_TestTube_0", "Chem_PotassiumPermanganate"), ("Kit_TestTube_1", "Chem_BenedictsReagent"),
        ("Kit_TestTube_2", "Chem_FerricChloride10"), ("Kit_TestTube_3", "Chem_BromineWater"),
    };

    static CutsceneData.Beat Lineup(float s, CinemaShot shot, string caption, (string vessel, string chem)[] set)
    {
        var b = B(s, shot, caption: caption, action: ShowcaseAction.Lineup);
        var names = new string[set.Length];
        b.chemicals = new ChemicalData[set.Length];
        for (int i = 0; i < set.Length; i++)
        {
            names[i] = set[i].vessel;
            b.chemicals[i] = AssetDatabase.LoadAssetAtPath<ChemicalData>(ChemFolder + set[i].chem + ".asset");
        }
        b.actionTarget = string.Join("|", names);
        return b;
    }

    /// Cards carry EndingMath tokens; credits are placeholders to fill in the Inspector.
    public static List<CutsceneData.Beat> EndingBeats() => new List<CutsceneData.Beat>
    {
        B(4.0f, Shot("Cam_LabWide", "Arr_SweepB", "Look_Bench", 58f), card: "Campaign Complete", line: "{passed} of {total} experiments passed"),
        Lineup(6.0f, Shot("End_LineupA", "End_LineupB", "Look_Lineup", 40f),
               "Ethanol · Benzoic Acid · Acetanilide · Acetone · Chloroform · Benzamide · Wine", Products),
        Lineup(4.0f, Shot("End_Tubes", "", "Look_Tubes", 38f), "Chemical Reactions, Prelim", TestColours),
        B(3.5f, Shot("Arr_Burner", "", "Look_Burner", 40f), caption: "Methane, the Tutorial",
          action: ShowcaseAction.LightBurner, target: "Kit_BunsenBurner_0_9"),
        B(3.5f, Shot("Arr_BenchHigh", "", "Look_Bench", 55f), card: "Average grade {average}%", line: "Your best grade in every experiment"),
        B(2.5f, Shot("Arr_BenchLow", "", "Look_Bench", 50f), card: "Tutorial", line: "{Tutorial}"),
        B(2.5f, Shot("Cam_Bench", "", "Look_Bench", 50f), card: "Prelim", line: "{Prelim}"),
        B(2.5f, Shot("Arr_BenchHigh", "", "Look_Bench", 55f), card: "Midterm", line: "{Midterm}"),
        B(2.5f, Shot("Arr_SweepA", "", "Look_Bench", 58f), card: "Finals", line: "{Final}"),
        B(4.0f, Shot("Arr_Pharmee", "", "Look_Pharmee", 42f), caption: "Pharmee is proud of you", action: ShowcaseAction.PharmeeCelebrate),
        // The door opens off camera here, so the corridor pull-back can watch it swing shut.
        B(3.5f, Shot("Arr_Jimenez", "", "Look_Jimenez", 42f), caption: "So is Dr. Jimenez", action: ShowcaseAction.OpenDoor),
        B(5.0f, Shot("Cam_Timelapse", "", "Look_Bench", 55f), action: ShowcaseAction.Timelapse),
        B(4.5f, Shot("End_Corridor", "End_CorridorBack", "Look_LabFromCorridor", 55f), action: ShowcaseAction.Night),
        B(3.0f, Shot("End_CorridorBack", "", "Look_LabFromCorridor", 55f), action: ShowcaseAction.CloseDoor),
        B(3.5f, Shot("Cam_LabWide", "", "Look_Bench", 58f), card: "PharmaSynth", line: "Gear Up, Synth It Up!"),
        B(3.5f, Shot("Arr_SweepA", "", "Look_Bench", 58f), card: "Laboratory manuscript", line: "[Manuscript authors]"),
        B(3.5f, Shot("Arr_BenchHigh", "", "Look_Bench", 55f), card: "Developed by", line: "[Development team]"),
        B(3.5f, Shot("Cam_Bench", "", "Look_Bench", 50f), card: "Made for", line: "[Institution]"),
        B(4.0f, Shot("Arr_DoorInside", "", "Look_Door", 55f), card: "Thank you for playing"),
    };

    [MenuItem(Menu + "Build Showcase Reels")]
    public static void Build() => Run(reset: false);

    [MenuItem(Menu + "Reset Showcase Reels (markers + reels)")]
    public static void Reset() => Run(reset: true);

    static void Run(bool reset)
    {
        if (Application.isPlaying) { Debug.LogWarning("[Showcase] exit Play mode first."); return; }
        System.IO.Directory.CreateDirectory(Folder);
        var cubeReel = Reel(CubeReelPath, "Arrival_CubeRoom", CubeBeats(), reset);
        var labReel = Reel(LabReelPath, "Arrival_Lab", LabBeats(), reset);
        var endReel = Reel(EndingReelPath, "Ending_Campaign", EndingBeats(), reset, CutsceneData.Kind.Ending);
        var music = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);

        var active = SceneManager.GetActiveScene();
        Scene lab = active.path == LabScene ? active : EditorSceneManager.OpenScene(LabScene, OpenSceneMode.Additive);
        Scene cube = active.path == CubeScene ? active : EditorSceneManager.OpenScene(CubeScene, OpenSceneMode.Additive);
        int a = Wire(lab, LabMarkers, labReel, true, music, reset, endReel);
        a += Wire(lab, EndingMarkers, labReel, true, music, reset, endReel);
        EnsureEndingButton();
        int b = Wire(cube, CubeMarkers, cubeReel, false, null, reset);
        EditorSceneManager.SaveScene(lab);
        EditorSceneManager.SaveScene(cube);
        if (lab != active) EditorSceneManager.CloseScene(lab, true);
        if (cube != active) EditorSceneManager.CloseScene(cube, true);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Showcase] wired: lab {a} marker(s) placed, cube room {b}; reels at {Folder}.");
    }

    static CutsceneData Reel(string path, string name, List<CutsceneData.Beat> beats, bool reset,
                             CutsceneData.Kind kind = CutsceneData.Kind.Arrival)
    {
        var reel = AssetDatabase.LoadAssetAtPath<CutsceneData>(path);
        if (reel != null && !reset) return reel;
        if (reel == null) { reel = ScriptableObject.CreateInstance<CutsceneData>(); AssetDatabase.CreateAsset(reel, path); }
        reel.name = name;
        reel.kind = kind;
        reel.title = "";
        reel.beats = beats;
        EditorUtility.SetDirty(reel);
        return reel;
    }

    static int Wire(Scene scene, (string name, Vector3 pos, Vector3 look)[] markers, CutsceneData reel,
                    bool labPart, AudioClip music, bool reset, CutsceneData ending = null)
    {
        GameObject root = null;
        foreach (var g in scene.GetRootGameObjects()) if (g.name == CinemaView.MarkersRoot) root = g;
        if (root == null)
        {
            root = new GameObject(CinemaView.MarkersRoot);
            SceneManager.MoveGameObjectToScene(root, scene);
        }
        if (root.GetComponent<CinemaView>() == null) root.AddComponent<CinemaView>();
        var dir = root.GetComponent<ShowcaseDirector>();
        if (dir == null) dir = root.AddComponent<ShowcaseDirector>();
        dir.Bind(reel, labPart, music, ending);
        EditorUtility.SetDirty(dir);

        int placed = 0;
        foreach (var m in markers)
        {
            var t = root.transform.Find(m.name);
            if (t != null && !reset) continue;
            if (t == null) { t = new GameObject(m.name).transform; t.SetParent(root.transform, false); }
            t.SetPositionAndRotation(m.pos, Quaternion.LookRotation(m.look - m.pos, Vector3.up));
            placed++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        return placed;
    }

    /// The demo HUD's Play Ending: a copy of Auto Quiz, relabelled, in the hamburger menu.
    static void EnsureEndingButton()
    {
        var demo = Object.FindAnyObjectByType<DemoHudController>(FindObjectsInactive.Include);
        if (demo == null) return;
        var so = new SerializedObject(demo);
        var endingProp = so.FindProperty("endingButton");
        var quiz = so.FindProperty("quizButton").objectReferenceValue as GameObject;
        var go = endingProp.objectReferenceValue as GameObject;
        if (go == null)
        {
            if (quiz == null) return;
            go = Object.Instantiate(quiz, quiz.transform.parent);
            go.name = "DemoEndingBtn";
            endingProp.objectReferenceValue = go;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.text = "Play Ending";
        var btn = go.GetComponent<UnityEngine.UI.Button>();
        if (btn != null)
        {
            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEditor.Events.UnityEventTools.RemovePersistentListener(btn.onClick, i);
            UnityEditor.Events.UnityEventTools.AddPersistentListener(btn.onClick, demo.OnPlayEnding);
        }
        if (go.transform.parent is RectTransform list)
            CompactHudBuilder.AppendToMenu(list, (RectTransform)go.transform);
        go.SetActive(false);   // DemoHudController shows it only in a demo, between runs
        EditorUtility.SetDirty(demo);
    }

    /// Edit-mode framing review (start / middle / end of every beat), written by
    /// CinemaCutsceneTools.RenderShots: the open scene's arrival reel, and the ending in the lab.
    [MenuItem(Menu + "Render Showcase Shots (open scene)")]
    public static void RenderOpen()
    {
        var dir = Object.FindAnyObjectByType<ShowcaseDirector>();
        if (dir == null || dir.Reel == null) { Debug.LogError("[Showcase] run Build Showcase Reels first."); return; }
        CinemaCutsceneTools.RenderShots(dir.Reel, dir.LabPart ? "arrival-lab" : "arrival-cube");
        if (dir.Ending != null) CinemaCutsceneTools.RenderShots(dir.Ending, "ending");
    }
}
#endif
