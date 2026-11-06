#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Reveal the Methane set + the waypoint marker in the
/// editor so they can be hand-aligned, and permanently strip the waypoint's
/// yellow ground glow (keep only the arrow). The Methane STATIONS carry the
/// step-detection zones AND are what the waypoint arrow follows, so moving them
/// with the props is how both detection and the arrow get aimed correctly.
public static class RevealMethaneAndWaypoint
{
    [MenuItem("Tools/PharmaSynth/Reveal Methane + Waypoint (for editing)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Reveal] exit Play mode first."); return; }

        var sb = new StringBuilder();

        // 1) Methane stage + its stations (the loose props are already visible).
        var stage = Find("MethaneStage");
        if (stage != null && !stage.activeSelf) { stage.SetActive(true); sb.Append("MethaneStage shown; "); }
        var stations = Find("MethaneStations");
        if (stations != null)
        {
            Selection.activeGameObject = stations;
            sb.Append("select MethaneStations to move the 5 Station_* zones with your props; ");
        }

        // 2) Waypoint marker: reveal it + drop the yellow ground glow (arrow only).
        var marker = Find("WaypointMarker");
        if (marker != null)
        {
            if (!marker.activeSelf) marker.SetActive(true);
            var glow = marker.transform.Find("Glow");
            if (glow != null && glow.gameObject.activeSelf)
            { glow.gameObject.SetActive(false); sb.Append("waypoint glow removed (arrow kept); "); }
            sb.Append("WaypointMarker shown — note it AUTO-follows the current step's station in play, so aim it by placing the stations. ");
        }
        else sb.Append("(WaypointMarker not found) ");

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("<color=#4CD07D>[Reveal] " + sb + "</color>");
    }

    static GameObject Find(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
