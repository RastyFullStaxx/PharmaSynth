#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Wires the fixed eye height onto the open scene's XR rig (fixed, not
/// relative to the player's real height). Run once per scene (MainMenu +
/// SampleScene). Idempotent. Tune the constants below and re-run to adjust.
public static class SpawnHeightWiring
{
    /// ONE eye height everywhere: per-scene heights made tests diverge, so the
    /// cube room's furnishings are lowered to suit the lab's height instead.
    public const float EyeHeight = 1.45f;

    [MenuItem("Tools/PharmaSynth/Wire Spawn Height (Fixed Per Scene)")]
    public static void Wire()
    {
        if (Application.isPlaying) { Debug.LogWarning("[SpawnHeight] exit Play mode first."); return; }

        var xr = GameObject.Find("XR Origin (XR Rig)");
        if (xr == null) { Debug.LogError("[SpawnHeight] no 'XR Origin (XR Rig)' in the open scene."); return; }

        Transform offset = FindDeep(xr, "Camera Offset");
        Transform cam = FindDeep(xr, "Main Camera");
        if (offset == null || cam == null)
        { Debug.LogError("[SpawnHeight] rig missing Camera Offset / Main Camera child."); return; }

        float target = EyeHeight;

        var fixedHeight = xr.GetComponent<SeatedHeightBoost>();
        if (fixedHeight == null) fixedHeight = xr.AddComponent<SeatedHeightBoost>();
        fixedHeight.Bind(cam, offset);
        fixedHeight.SetTarget(target);
        EditorUtility.SetDirty(fixedHeight);

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(xr.scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[SpawnHeight] fixed eye height " + target + " m wired in '" + xr.scene.name + "'.</color>");
    }

    static Transform FindDeep(GameObject root, string exact)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == exact) return t;
        return null;
    }
}
#endif
