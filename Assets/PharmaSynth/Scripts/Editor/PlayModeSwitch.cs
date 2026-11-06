#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

/// One-click switch between the two ways to run PharmaSynth in the editor, so you
/// never have to hunt the Hierarchy or Project Settings:
///   • PC Dev Mode — OpenXR auto-init OFF + the "XR Device Simulator" GameObject
///     ENABLED, so W/A/S/D + mouse move the view on the PC with no headset.
///   • Headset Mode — OpenXR auto-init ON + the simulator DISABLED, so a Quest on
///     Quest Link / Air Link drives the view.
/// Each mode fixes BOTH halves (the XR init setting AND the scene's simulator
/// object) and saves. Flipping only the init setting leaves the simulator off,
/// so nothing drives the camera. Menu shows a checkmark for the active mode.
/// Android is left untouched.
public static class PlayModeSwitch
{
    const string SimName = "XR Device Simulator";
    const string PcMenu  = "Tools/PharmaSynth/Play Mode/PC Dev Mode (keyboard + simulator)";
    const string HmdMenu = "Tools/PharmaSynth/Play Mode/Headset Mode (Quest Link)";

    static XRGeneralSettings Standalone()
    {
        EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget per);
        return per != null ? per.SettingsForBuildTarget(BuildTargetGroup.Standalone) : null;
    }

    [MenuItem(PcMenu, priority = 2010)]
    static void PcMode() => Apply(headset: false);

    [MenuItem(HmdMenu, priority = 2011)]
    static void HmdMode() => Apply(headset: true);

    static void Apply(bool headset)
    {
        if (Application.isPlaying) { Debug.LogWarning("[PlayMode] exit Play mode first, then switch."); return; }

        // 1. OpenXR auto-init on the Standalone (PC) target.
        var s = Standalone();
        if (s != null) { s.InitManagerOnStart = headset; EditorUtility.SetDirty(s); AssetDatabase.SaveAssets(); }
        else Debug.LogWarning("[PlayMode] no Standalone XR settings (assign OpenXR under XR Plug-in Management ▸ Desktop).");

        // 2. XR Device Simulator active state in EVERY SCENE IN BUILD SETTINGS.
        //
        // ⛔ Not just the open ones. Walking only the loaded scenes (`SceneManager`) would
        // leave MainMenu's simulator ACTIVE when Headset Mode runs with only SampleScene open.
        // Play always boots MainMenu (`PlayFromMenuBootstrap`), so the simulator would hijack
        // the HMD there: the head pose freezes at local (0,0,0), `SeatedHeightBoost` falls back
        // to its no-device offset, and the real Quest controllers, still reporting true poses,
        // float ABOVE the view. Half-applied is worse than not applied, because the wrong half
        // is the scene the game starts in.
        var open = new System.Collections.Generic.Dictionary<string, Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var sc = SceneManager.GetSceneAt(i);
            if (sc.isLoaded && !string.IsNullOrEmpty(sc.path)) open[sc.path] = sc;
        }

        int touched = 0;
        bool savedOpen = false;
        var report = new System.Text.StringBuilder();
        foreach (var entry in EditorBuildSettings.scenes)
        {
            if (!entry.enabled || string.IsNullOrEmpty(entry.path)) continue;

            // An OPEN scene is edited in place; a closed one is opened additively, fixed,
            // saved and closed again, so the switch never depends on what happens to be open.
            bool wasOpen = open.TryGetValue(entry.path, out var scene);
            if (!wasOpen) scene = EditorSceneManager.OpenScene(entry.path, OpenSceneMode.Additive);

            bool found = false, changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                var sim = FindDeep(root.transform, SimName);
                if (sim == null) continue;
                found = true;
                if (sim.gameObject.activeSelf != !headset)
                {
                    sim.gameObject.SetActive(!headset);
                    EditorSceneManager.MarkSceneDirty(scene);
                    changed = true; touched++;
                }
            }

            report.AppendLine("    " + System.IO.Path.GetFileNameWithoutExtension(entry.path)
                              + ": simulator " + (!found ? "NOT PRESENT" : (headset ? "OFF" : "ON"))
                              + (changed ? "  (changed)" : ""));

            if (wasOpen) { if (changed) savedOpen = true; }
            else
            {
                if (changed) EditorSceneManager.SaveScene(scene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }
        if (savedOpen) EditorSceneManager.SaveOpenScenes();

        Debug.Log(headset
            ? "<color=#4CD07D>[PlayMode] HEADSET MODE - OpenXR on Play, simulator off. Connect the Quest (Quest Link / Air Link), then press Play.</color>"
            : "<color=#4CD07D>[PlayMode] PC DEV MODE - simulator on, OpenXR off. Press Play; move with W/A/S/D, hold Right-Mouse to look (Q/E = down/up).</color>");
        // Per-scene, so a half-applied switch is visible instead of silent.
        Debug.Log("[PlayMode] every scene in Build Settings, changed " + touched + ":");
        Debug.Log(report.ToString().TrimEnd());
    }

    static Transform FindDeep(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    [MenuItem(PcMenu, validate = true)]
    static bool PcValidate() { var s = Standalone(); Menu.SetChecked(PcMenu, s != null && !s.InitManagerOnStart); return true; }

    [MenuItem(HmdMenu, validate = true)]
    static bool HmdValidate() { var s = Standalone(); Menu.SetChecked(HmdMenu, s != null && s.InitManagerOnStart); return true; }
}
#endif
