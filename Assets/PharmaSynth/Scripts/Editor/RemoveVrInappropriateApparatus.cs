#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Deletes the SCENE INSTANCES of apparatus that the manuscript lists but that
/// carry no meaningful VR interaction: pure bench scaffolding and passive
/// instruments the game already abstracts. The prefabs stay in their folders.
///
/// ⛔ DELIBERATE, DOCUMENTED EXCEPTION to the "ALL tools always present" client
/// rule. These six are NOT a decluttering of usable tools — each is either a
/// support rig the zone-free heat model made unnecessary or an instrument whose
/// reading the game shows on-screen. The PREFAB ASSETS stay in the project, so a
/// future experiment that genuinely needs one can re-place it. Do NOT let
/// "Restore All Bench Items" or an all-tools audit re-add them — they are
/// removed on purpose. Justifications live in experiments-reference.md.
public static class RemoveVrInappropriateApparatus
{
    // itemId -> why it isn't a VR interaction (also written into the docs).
    static readonly (string itemId, string why)[] Removed =
    {
        ("kit-retortstand",  "support scaffold — VR heats zone-free (bring the flask to the bath), no clamp-rig to assemble"),
        ("kit-utilityclamp", "the iron/S-clamp that holds glassware on the stand — same scaffold, no VR interaction"),
        ("kit-aspirator",    "vacuum suction for filtration/transfer — the game moves liquid by pour/decant, never suction"),
        ("kit-condenser",    "distillation cooling — distillation is an abstracted heat+collect sim, no condenser assembly"),
        ("kit-thermometer",  "temperature is shown live by the floating ProcessReadout / water-bath label, so the physical device is decorative"),
        // Flame-rig / crucible ignition set. NO manuscript step
        // and NO game experiment uses any of these; the zone-free water bath caps
        // at 100 °C so a crucible's >500 °C ignition has no place. The TRIPOD +
        // WIRE GAUZE are KEPT — they serve as a heat platform over a burner.
        ("kit-claytriangle", "cradles a crucible over an open flame — the crucible is gone and the tripod+gauze cover 'hold over the burner'"),
        ("kit-crucible",     "strong >500 °C ignition/ashing vessel — no experiment needs it; the water bath caps at 100 °C"),
        ("kit-crucibletongs","handles the hot crucible — useless once the crucible is removed"),
        ("kit-alcoholburner","redundant flame source — the Bunsen burner beside the water bath covers all heating (user 2026-07-18)"),
        // Empty sample vials: 0 manuscript ("vial"/"amber" never
        // appear) and 0 game usage — no layout, prop, or code references them.
        // Reagent-staging containers with no use in the bench-bound Ethyl layout;
        // the bench-bound rebuild draws every reagent from the Raw_ bottles.
        ("kit-vial", "empty sample vial — 0 manuscript & 0 game usage (leftover reagent-staging container)"),
    };

    // Matched by NAME (these carry NO LabItem itemId). CONTAINS, not StartsWith.
    //   • IronRing — TWO of them (`Eq_IronRing` + `IronRing_2`), both orphaned
    //     (they clamp the already-removed retort stand); StartsWith missed `Eq_`.
    //   • VialBrown — the four brown/amber vials (`Eq_Vial_Brown` + `Vial_Brown_2/3/4`),
    //     same unused-vial justification as kit-vial above.
    //   • Forceps — `Eq_Forceps`. 0 manuscript, 0 game usage; its only refs are
    //     generic metadata (size/physics/hover), no task or verb reads it. In VR
    //     the player grabs litmus/paper/crystals directly, so it's redundant.
    static readonly string[] RemovedByName = { "IronRing", "VialBrown", "Forceps" };

    [MenuItem("Tools/PharmaSynth/Remove VR-Inappropriate Apparatus")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RemoveApparatus] exit Play mode first."); return; }

        var ids = new HashSet<string>(Removed.Select(r => r.itemId));
        var kill = new List<GameObject>();
        foreach (var li in Object.FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (li != null && !string.IsNullOrEmpty(li.itemId) && ids.Contains(li.itemId)) kill.Add(li.gameObject);
        // By-name items (no itemId) — CONTAINS so it catches both `Eq_IronRing`
        // and `IronRing_2` (StartsWith missed the Eq_-prefixed one).
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            foreach (var nm in RemovedByName)
                if (t != null && t.name.Replace("_", "").IndexOf(nm, System.StringComparison.OrdinalIgnoreCase) >= 0
                    && !kill.Contains(t.gameObject))
                    kill.Add(t.gameObject);

        foreach (var go in kill)
        {
            if (go == null) continue;   // a child already died with its parent (fake-null)
            Undo.DestroyObjectImmediate(go);
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[RemoveApparatus] removed {kill.Count} scene instance(s) "
                  + "(prefabs kept in the project). Idempotent — safe to re-run.</color>\n  "
                  + string.Join("\n  ", Removed.Select(r => r.itemId + " — " + r.why)));
    }
}
#endif
