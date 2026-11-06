#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// One-click self-service layout lock: after moving or
/// duplicating assets, one button does everything needed to make a
/// hand-arranged workspace permanent — with NO rebuilding, so it can never
/// reset placements:
///   1. Tidy duplicate names  — "Beaker (1)" → "Beaker_100mL_2", + full
///      interaction wiring for any raw duplicate that was missing it.
///   2. Re-home every item     — current transform becomes its respawn home
///      (moved originals AND duplicates), so nothing snaps back in Play.
///   3. Save the scene.
/// Run this after ANY manual arrangement.
/// Purely additive — spawns/destroys nothing.
public static class LockMyLayout
{
    [MenuItem("Tools/PharmaSynth/Lock My Layout")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[LockLayout] exit Play mode first."); return; }

        int renamed = ManualLayoutAdopter.RenameAndWireDuplicates();   // clean names + wire duplicates
        ReHomeSceneItems.Adopt();                                       // homes = current positions

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[LockLayout] locked — {renamed} duplicate(s) tidied+wired, all respawn homes "
                  + "adopted at current positions, scene saved. Your layout is permanent.</color>");
    }
}
#endif
