#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Places the ambient atmosphere emitters: cool vapour sinking
/// from the AC unit + a faint haze layer near the floor. Low-density on
/// purpose (Quest overdraw). Door cold-air is code-hooked in DoorOpener, not here.
///
/// Tools ▸ PharmaSynth ▸ Build Atmosphere VFX (SampleScene, edit mode, idempotent).
public static class AtmosphereBuilder
{
    private const string GroupName = "AtmosphereVfx";

    [MenuItem("Tools/PharmaSynth/Build Atmosphere VFX")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Atmosphere] exit Play mode first."); return; }

        var old = GameObject.Find(GroupName);
        if (old != null) Object.DestroyImmediate(old);
        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Build Atmosphere VFX");

        // AC vapour — cool air falling from the unit's TOP vent, on the room-facing side.
        var ac = FindAc();
        int made = 0;
        if (ac != null)
        {
            var rends = ac.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            // Bias toward the room centre so the vapour spills off the front of the unit.
            Vector3 toRoom = new Vector3(-0.5f, b.center.y, -3.5f) - b.center; toRoom.y = 0f;
            toRoom = toRoom.sqrMagnitude < 0.01f ? Vector3.left : toRoom.normalized;
            // Out in the aisle just in front of the unit (over the darker floor, where cool
            // vapour actually reads) rather than flat against the white body.
            var pos = new Vector3(b.center.x, b.max.y - 0.65f, b.center.z) + toRoom * (Mathf.Min(b.extents.x, b.extents.z) + 0.55f);
            made += Emitter(group.transform, "AC_Vapor", pos, AtmosphereVfx.Style.AcVapor, Vector3.zero);
            Debug.Log("[Atmosphere] AC vapour on '" + ac.name + "' vent at " + pos.ToString("F2") + " (unit top y=" + b.max.y.ToString("F2") + ")");
        }
        else Debug.LogWarning("[Atmosphere] no AC unit found — vapour skipped");

        // Haze — a single faint, LOW ground mist that hugs the floor (kept out of the
        // eye-line so it never obscures the view).
        made += Emitter(group.transform, "Haze_Floor", new Vector3(-0.5f, 0.2f, -3.5f),
                        AtmosphereVfx.Style.Haze, new Vector3(7f, 0.35f, 6f));

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log("<color=#4CD07D>[Atmosphere] built " + made + " emitter(s)</color> (AC vapour + floor/ceiling haze). Door cold-air is code-hooked.");
    }

    static int Emitter(Transform parent, string name, Vector3 pos, AtmosphereVfx.Style style, Vector3 hazeSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var av = go.AddComponent<AtmosphereVfx>();
        if (style == AtmosphereVfx.Style.Haze) av.Bind(style, hazeSize); else av.Bind(style);
        EditorUtility.SetDirty(go);
        return 1;
    }

    static GameObject FindAc()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name.ToLower();
            if ((n == "ac" || n.Contains("aircon") || n.Contains("air_cond") || n.Contains("hvac"))
                && t.GetComponentInChildren<Renderer>() != null)
                return t.gameObject;
        }
        return null;
    }
}
#endif
