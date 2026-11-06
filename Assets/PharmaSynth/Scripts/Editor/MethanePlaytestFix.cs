#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using XRGrabInteractable = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Wires the location-free Methane rig's missing pieces: a workspace burner
/// needs a BurnerController to be lit, striking a match needs a
/// MatchStrikerSurface, and the Methane reagent jar needs a LiquidPhysics to be
/// scooped/poured. Without them the heat step is blocked.
///   • every Bunsen/Alcohol burner gets BurnerController + MatchStrikerSurface
///     (strike a match on the burner base to light it, then it lights the tube),
///   • any matchbox-like object becomes a striker too,
///   • the reagent jar becomes a scoopable solid (Sodium Acetate).
/// Idempotent.
public static class MethanePlaytestFix
{
    [MenuItem(SceneRepairs.MenuRoot + "Fix Methane Verbs (burner/match/reagent)")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[MethaneFix] exit Play mode first."); return; }
        var lib = AssetDatabase.LoadAssetAtPath<SceneAssetLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/SceneAssetLibrary.asset");
        var registry = AssetDatabase.LoadAssetAtPath<ReactionRegistry>(
            "Assets/PharmaSynth/ScriptableObjects/Reactions/MasterReactionRegistry.asset");

        var runner = Object.FindAnyObjectByType<ExperimentRunner>(FindObjectsInactive.Include);
        // Match by prefab name OR GO name substring — kit items are named
        // "Kit_Motar_1_15" / "Kit_Pestle_1_16" (Object.Instantiate = no prefab
        // source), so an exact prefab-name match misses them.
        bool IsKind(GameObject g, string kind)
            => PhysicsAudit.PrefabNameFor(g) == kind || g.name.Contains(kind);

        // Find a pestle to drive the mortar's grind verb.
        Transform pestle = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (IsKind(t.gameObject, "Pestle")) { pestle = t; break; }

        int burners = 0, strikers = 0, jars = 0, grinders = 0, scoops = 0, mortars = 0;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            var go = t.gameObject;
            bool isBurner = IsKind(go, "BunsenBurner") || IsKind(go, "AlcoholBurner");
            if (IsKind(go, "Motar") || IsKind(go, "Mortar")) mortars++;

            // Scoopula / porcelain spatula → dip-and-deposit verb (needed to move
            // the solid sodium-acetate/soda-lime into the mortar — you can't pour it).
            if ((IsKind(go, "Scoopula") || IsKind(go, "Spatula")) && go.GetComponent<ScoopController>() == null
                && go.GetComponentInChildren<Renderer>() != null)
            {
                go.AddComponent<ScoopController>().Bind(go.GetComponent<XRGrabInteractable>());
                scoops++; EditorUtility.SetDirty(go);
            }

            // Mortar: give it a grind verb (cosmetic taskId — the Methane rig
            // re-points it to "prepare-mixture" while the tutorial runs) AND a
            // LiquidPhysics receiver so the scoop can DEPOSIT the solid into it
            // (deposit needs a LiquidPhysics on the target — without one the
            // scooped content could not be transferred to the mortar).
            if (IsKind(go, "Motar") || IsKind(go, "Mortar"))
            {
                if (go.GetComponent<GrindController>() == null)
                {
                    var grind = go.AddComponent<GrindController>();
                    grind.Bind(runner, null, pestle);
                    grinders++; EditorUtility.SetDirty(go);
                }
                {
                    var mlp = go.GetComponent<LiquidPhysics>() ?? go.AddComponent<LiquidPhysics>();
                    mlp.registry = registry;
                    if (mlp.currentChemical == null && mlp.currentLiquidVolume <= 0.01f)
                        mlp.SetContents(null, 0f);       // starts empty; scoops fill it
                    // The mortar's OWN mesh must never become the fill renderer (it
                    // would vanish in play). LiquidPhysics.Start
                    // refuses to adopt an opaque host mesh, so mainRenderer stays null
                    // and the mortar mesh is never disabled; the mound is a Powder child.
                    EditorUtility.SetDirty(go);
                }
            }

            // Burner: can be lit + is itself a striker (strike a match on its base).
            if (isBurner)
            {
                if (go.GetComponentInChildren<Collider>() != null)
                {
                    if (go.GetComponent<BurnerController>() == null) { go.AddComponent<BurnerController>(); burners++; }
                    if (go.GetComponent<MatchStrikerSurface>() == null) { go.AddComponent<MatchStrikerSurface>(); strikers++; }
                    EditorUtility.SetDirty(go);
                }
            }
            // Any matchbox / striker-ish prop the player rubs a match on.
            else if ((go.name.ToLowerInvariant().Contains("match") || go.name.ToLowerInvariant().Contains("striker"))
                     && go.GetComponentInChildren<Collider>() != null
                     && go.GetComponent<MatchStrikerSurface>() == null
                     && go.GetComponent<Matchstick>() == null)   // not a match itself
            {
                go.AddComponent<MatchStrikerSurface>(); strikers++; EditorUtility.SetDirty(go);
            }

            // Methane reagent jar → scoopable solid (Sodium Acetate + Soda Lime).
            // Idempotent: re-assert contents + the POWDER visual even if a prior
            // run already added LiquidPhysics with a liquid-fill twin.
            var item = go.GetComponent<LabItem>();
            if (item != null && item.itemId == "reagent-jar")
            {
                var lp = go.GetComponent<LiquidPhysics>() ?? go.AddComponent<LiquidPhysics>();
                lp.registry = registry;
                var chem = lib != null ? lib.GetChemical("Sodium Acetate") : null;
                if (lp.currentChemical == null || lp.currentLiquidVolume <= 0.01f)
                    lp.SetContents(chem, chem != null ? 60f : 0f);
                ExperimentSceneBuilder.EnsureLiquidVisual(go, lp);   // branches to powder
                jars++; EditorUtility.SetDirty(go);
            }
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[MethaneFix] {burners} burner(s) can now be lit, {strikers} striker surface(s) added "
                  + $"(strike a match on a burner base to light it), {grinders} mortar(s) got a grind verb, "
                  + $"{jars} reagent jar(s) made scoopable (shown as powder, not liquid), {scoops} scoop tool(s) wired. "
                  + $"The grind step completes on the grind MOTION. Found {mortars} mortar(s) in the scene.</color>"
                  + (mortars == 0 ? "\n⚠ No MORTAR found in the scene — place a Mortar (and Pestle) on the bench and re-run, or the scoop has nowhere to deposit and grinding can't happen." : "")
                  + (pestle == null ? "\n⚠ No Pestle found — the grind verb needs one; place a Pestle and re-run." : ""));
    }
}
#endif
