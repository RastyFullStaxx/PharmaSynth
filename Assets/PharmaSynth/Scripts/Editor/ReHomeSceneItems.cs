#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Adopts every scene item's CURRENT transform as its DropRespawn home, so
/// equipment relocated by hand keeps its new spot as its
/// default spawn point. Without this, manually moved props teleport back to
/// their previous serialized homes after ~25 s idle / a kill-Z fall / a reset.
///
/// Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current) — run in SampleScene
/// edit mode after ANY manual re-arrangement, then save the scene.
///
/// ⛔ NEVER run the ALL-items version straight after a simulator. Every simulator
/// calls `DropRespawn.ResetAllHome()`, which teleports all 120 items back to their BAKED
/// homes, so "adopt current" then re-bakes the pose the reset just restored and writes it
/// to disk, silently destroying whatever had been placed by hand. Nothing is logged and
/// the result is self-consistent, just wrong. Safe order is REOPEN the scene (discarding), then
/// re-home, then save. `Re-Home MOVED Items Only` below is the version that cannot do this.
public static class ReHomeSceneItems
{
    /// Adopt the current transform ONLY where it actually differs from the baked home.
    ///
    /// An item a simulator has just restored to its home is by definition NOT different from
    /// it, so this can never re-bake a revert — it can only pick up a real move. It also
    /// names every item it claims, so a run that grabs more than expected is visible instead
    /// of silent.
    [MenuItem("Tools/PharmaSynth/Re-Home MOVED Items Only")]
    public static void AdoptMoved()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ReHome] exit Play mode first."); return; }

        const float posEpsilon = 0.001f;    // 1 mm
        const float rotEpsilon = 1f;        // 1 degree
        int n = 0; string names = "";
        foreach (var dr in Object.FindObjectsByType<DropRespawn>(FindObjectsInactive.Include))
        {
            if (dr == null) continue;
            var so = new SerializedObject(dr);
            var hasHome = so.FindProperty("_hasHome");
            var homePos = so.FindProperty("_homePos");
            var homeRot = so.FindProperty("_homeRot");
            if (homePos == null || homeRot == null) continue;

            bool never = hasHome != null && !hasHome.boolValue;
            bool moved = never
                || Vector3.Distance(homePos.vector3Value, dr.transform.position) > posEpsilon
                || Quaternion.Angle(homeRot.quaternionValue, dr.transform.rotation) > rotEpsilon;
            if (!moved) continue;

            Undo.RecordObject(dr, "Re-Home Moved Items");
            Debug.Log("[ReHome] " + dr.name + "  " + homePos.vector3Value.ToString("0.000")
                      + " → " + dr.transform.position.ToString("0.000"));
            dr.SetHome(dr.transform.position, dr.transform.rotation);
            EditorUtility.SetDirty(dr);
            n++;
            if (names.Length < 140) names += (names.Length > 0 ? ", " : "") + dr.name;
        }

        if (n > 0)
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[ReHome] adopted " + n + " MOVED item(s)"
                  + (names.Length > 0 ? ": " + names : " — nothing had moved") + "</color>");
    }

    [MenuItem("Tools/PharmaSynth/Re-Home Scene Items (Adopt Current)")]
    public static void Adopt()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ReHome] exit Play mode first."); return; }

        int n = 0;
        foreach (var dr in Object.FindObjectsByType<DropRespawn>(FindObjectsInactive.Include))
        {
            Undo.RecordObject(dr, "Re-Home Scene Items");
            dr.SetHome(dr.transform.position, dr.transform.rotation);
            EditorUtility.SetDirty(dr);
            n++;
        }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[ReHome] adopted current transforms as home for " + n + " item(s)</color>");
    }
}
#endif
