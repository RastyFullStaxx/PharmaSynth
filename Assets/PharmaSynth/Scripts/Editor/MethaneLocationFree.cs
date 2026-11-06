#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Convert the Methane tutorial to LOCATION-FREE
/// completion. Deletes the 5 fixed Station_* zone objects (no standing on a
/// pad), and rewires the MethaneApparatusRig to own its TemperatureSim +
/// GasCollection so heat/collect/splint fire by item PROXIMITY anywhere, and
/// prepare-mixture completes by grinding a mortar. Run once. Idempotent.
public static class MethaneLocationFree
{
    [MenuItem("Tools/PharmaSynth/Make Methane Location-Free")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[MethaneFree] exit Play mode first."); return; }

        // 1) Delete the fixed station zone objects (their sims move onto the rig).
        int killed = 0;
        foreach (var name in new[] { "Station_PrepareMixture", "Station_SetupApparatus",
                                     "Station_HeatMixture", "Station_CollectGas", "Station_TestGas" })
        {
            var go = Find(name);
            if (go != null) { Object.DestroyImmediate(go); killed++; }
        }

        // 2) Wire the rig: MethaneApparatusRig + its own sims, bound to the runner.
        var rig = Find("MethaneRig") ?? Find("MethaneStage");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        if (rig == null || runner == null)
        { Debug.LogError("[MethaneFree] MethaneRig or ExperimentRunner not found."); return; }

        var apparatus = rig.GetComponent<MethaneApparatusRig>() ?? rig.AddComponent<MethaneApparatusRig>();
        var temp = rig.GetComponent<TemperatureSim>() ?? rig.AddComponent<TemperatureSim>();
        var gas = rig.GetComponent<GasCollection>() ?? rig.AddComponent<GasCollection>();
        apparatus.Bind(runner, temp, gas);
        EditorUtility.SetDirty(apparatus);

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[MethaneFree] deleted {killed} fixed station(s); Methane now completes by ACTION anywhere — "
                  + "grind (prepare), tube+collection together (setup), lit burner at the tube (heat), "
                  + "collection at the hot tube (collect), lit match at the filled tube (splint). Play-test each step.</color>");
    }

    static GameObject Find(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
#endif
