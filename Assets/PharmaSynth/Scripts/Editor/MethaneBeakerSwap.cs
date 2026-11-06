#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrabInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Swap the sodium-acetate/soda-lime SOURCE from a sealed amber vial to an OPEN
/// beaker. The charge is scooped, not poured, so it belongs in a wide-mouth
/// vessel you can dip a scoop into. Preserves each jar's position + contents + wiring: instantiates
/// Beaker_100mL in place, re-tags it "reagent-jar", refills it with the solid,
/// rebuilds the powder mound (open top → the scoop reaches it), re-labels it, and
/// destroys the old vial. Idempotent (skips jars that are already beakers).
public static class MethaneBeakerSwap
{
    const string BeakerPath = "Assets/PharmaSynth/Art/Equipment/ChemLabEquipment/Prefabs/Beaker_100mL.prefab";

    [MenuItem("Tools/PharmaSynth/Swap Acetate Vial → Open Beaker")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[BeakerSwap] exit Play mode first."); return; }

        var beakerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BeakerPath);
        if (beakerPrefab == null) { Debug.LogError("[BeakerSwap] Beaker_100mL prefab not found at " + BeakerPath); return; }

        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);

        // Collect the current source vials first (mutating the scene mid-scan is unsafe).
        var oldJars = new List<GameObject>();
        foreach (var it in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (it != null && it.itemId == "reagent-jar") oldJars.Add(it.gameObject);

        int swapped = 0, skipped = 0;
        foreach (var old in oldJars)
        {
            if (PhysicsAudit.PrefabNameFor(old).Contains("Beaker")) { skipped++; continue; }   // already done

            // Capture what to carry over.
            var parent = old.transform.parent;
            Vector3 pos = old.transform.position;
            float yaw = old.transform.eulerAngles.y;
            var oldLp = old.GetComponent<LiquidPhysics>();
            ChemicalData chem = oldLp != null ? oldLp.currentChemical : null;
            float ml = oldLp != null && oldLp.currentLiquidVolume > 0.01f ? oldLp.currentLiquidVolume : 60f;
            if (chem == null && lib != null) chem = lib.GetChemical("Sodium Acetate");
            string label = old.GetComponent<LabItem>()?.displayName;
            if (string.IsNullOrEmpty(label)) label = "Sodium Acetate + Soda Lime";

            // Instantiate the open beaker in the vial's place (keep the prefab link).
            var beaker = (GameObject)PrefabUtility.InstantiatePrefab(beakerPrefab);
            beaker.name = "Prop_reagent-jar";
            beaker.transform.SetParent(parent, true);
            beaker.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));   // upright

            var item = beaker.GetComponent<LabItem>() ?? beaker.AddComponent<LabItem>();
            item.itemId = "reagent-jar"; item.displayName = label;

            // Full interaction wiring (grab/physics/respawn/impact/home), then the
            // liquid-physics source + powder mound + floating name.
            PhysicsAudit.WireSceneItem(beaker, "Beaker_100mL", runner);
            var lp = beaker.GetComponent<LiquidPhysics>() ?? beaker.AddComponent<LiquidPhysics>();
            lp.registry = registry;
            lp.SetContents(chem, ml);
            ExperimentSceneBuilder.EnsureLiquidVisual(beaker, lp);   // Solid → powder mound in the open beaker
            var pl = beaker.GetComponent<ProximityLabel>() ?? beaker.AddComponent<ProximityLabel>();
            pl.SetLabel(label, 1.4f);
            var dr = beaker.GetComponent<DropRespawn>();
            if (dr != null) dr.SetHome(beaker.transform.position, beaker.transform.rotation);

            Object.DestroyImmediate(old);
            swapped++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[BeakerSwap] {swapped} vial(s) → open beaker (scoopable solid source), {skipped} already beakers. "
                  + "Run Lock My Layout to bake, then scoop straight from the open top.</color>");
    }
}
#endif
