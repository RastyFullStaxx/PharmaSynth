#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Wires the post-experiment review corner: a ReviewCornerSpawn
/// marker in front of the PostLabTablet (biased toward Dr. Jimenez's spot) that
/// the gatekeeper fade-teleports the player to for the quiz-review flow, plus the
/// gatekeeper's postLab/examiner refs and the quiz's autoOpen=false (the gate
/// opens the quiz after Jimenez's briefing). Idempotent.
public static class ReviewCornerBuilder
{
    [MenuItem("Tools/PharmaSynth/Build Review Corner")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[ReviewCorner] exit Play mode first."); return; }

        var tablet = SceneFind.InActiveScene("PostLabTablet");
        var jimenez = SceneFind.InActiveScene("DrJimenez");
        var gatekeeper = FirstIncludingInactive<PharmeeGatekeeper>();
        var postLab = FirstIncludingInactive<PostLabController>();
        var examiner = FirstIncludingInactive<ExaminerNPC>();
        if (tablet == null || gatekeeper == null || postLab == null)
        {
            Debug.LogError("[ReviewCorner] PostLabTablet/PharmeeGatekeeper/PostLabController not found — open SampleScene.unity first.");
            return;
        }

        // Spawn marker: ~1.4 m back from the tablet toward the room centre, nudged
        // toward Jimenez so the player lands facing tablet AND examiner.
        var existing = GameObject.Find("ReviewCornerSpawn");
        var spawn = existing != null ? existing.transform : new GameObject("ReviewCornerSpawn").transform;
        Vector3 tabletPos = tablet.transform.position;
        Vector3 back = -tablet.transform.forward; back.y = 0f;
        if (back.sqrMagnitude < 1e-4f) back = new Vector3(0f, 0f, -1f);
        back.Normalize();
        Vector3 pos = tabletPos + back * 1.4f;
        if (jimenez != null)
        {
            Vector3 towardJimenez = jimenez.transform.position - pos; towardJimenez.y = 0f;
            pos += towardJimenez.normalized * 0.35f;   // slight bias so he's in view
        }
        pos.y = 0.22f;                                  // rig-camera marker height (matches FrontDoorSpawn)
        Vector3 look = tabletPos - pos; look.y = 0f;
        spawn.SetPositionAndRotation(pos, Quaternion.LookRotation(look.normalized));

        // Wire the gatekeeper's review refs + flip the quiz to gate-driven opening.
        var so = new SerializedObject(gatekeeper);
        so.FindProperty("postLab").objectReferenceValue = postLab;
        so.FindProperty("reviewCornerSpawn").objectReferenceValue = spawn;
        so.FindProperty("examiner").objectReferenceValue = examiner;
        so.ApplyModifiedPropertiesWithoutUndo();

        var pso = new SerializedObject(postLab);
        pso.FindProperty("autoOpen").boolValue = false;
        pso.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[ReviewCorner] spawn at " + pos.ToString("F2") + ", gatekeeper wired (postLab/examiner"
                  + (examiner == null ? " — NO ExaminerNPC found!" : "") + "), quiz autoOpen=false.");
    }

    static T FirstIncludingInactive<T>() where T : Component
    {
        var all = Object.FindObjectsByType<T>(FindObjectsInactive.Include);
        return all.Length > 0 ? all[0] : null;
    }
}
#endif
