#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Adds UiButtonSounds (hover blip + click) to every UI Button in the OPEN scene.
/// Run it once per scene — MainMenu (cube room) and SampleScene
/// (HUD / choice panels / settings / grade / post-lab). Idempotent.
///
/// Tools ▸ PharmaSynth ▸ Wire Button Sounds (edit mode).
public static class ButtonSoundsBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire Button Sounds")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ButtonSounds] exit Play mode first."); return; }

        int n = 0;
        foreach (var btn in Object.FindObjectsByType<Button>(FindObjectsInactive.Include))
        {
            if (btn.GetComponent<UiButtonSounds>() == null)
            {
                btn.gameObject.AddComponent<UiButtonSounds>();
                EditorUtility.SetDirty(btn.gameObject);
            }
            n++;
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[ButtonSounds] hover+click on " + n + " button(s) in '" +
                  UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name + "'</color>");
    }
}
#endif
