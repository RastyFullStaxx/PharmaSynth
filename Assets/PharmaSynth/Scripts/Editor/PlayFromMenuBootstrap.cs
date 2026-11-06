#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Every editor Play starts in the CUBE SPAWN ROOM ("ensure that
/// every start or play, I start at the cuberoom not at the lab right away") —
/// exactly like the built game (MainMenu is build index 0). Uses the editor's
/// play-mode start scene, so whatever scene is OPEN keeps its edits; Play simply
/// boots MainMenu. Toggle off via the menu when a direct lab test is needed
/// (e.g. iterating on stage layout with the DevExperimentDriver).
[InitializeOnLoad]
public static class PlayFromMenuBootstrap
{
    const string MenuPath = "Tools/PharmaSynth/Play Starts In Cube Room";
    const string PrefKey = "PharmaSynth.PlayFromMenu";
    const string ScenePath = "Assets/Scenes/MainMenu.unity";

    static PlayFromMenuBootstrap() => EditorApplication.delayCall += Apply;

    static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set { EditorPrefs.SetBool(PrefKey, value); Apply(); }
    }

    static void Apply()
    {
        EditorSceneManager.playModeStartScene = Enabled
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)
            : null;
        Menu.SetChecked(MenuPath, Enabled);
    }

    [MenuItem(MenuPath)]
    static void Toggle()
    {
        Enabled = !Enabled;
        Debug.Log("[PlayFromMenu] Play now starts in " + (Enabled ? "the CUBE ROOM (MainMenu)" : "the OPEN scene"));
    }
}
#endif
