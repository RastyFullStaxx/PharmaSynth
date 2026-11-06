using System.Collections.Generic;
using UnityEngine;

/// The hand-built Methane stage should be PRESENT only
/// during the Lab Tour and the Methane tutorial itself, and gone the moment you
/// move on to any other experiment or are idle in the lab. Methane is the only
/// experiment that uses this stage (Experiment 1), so a single controller owns
/// its visibility — the sole authority (ExperimentSceneBuilder does not toggle
/// it). Lives on a manager object, NOT on the stage (it must keep running while
/// the stage is hidden). Edit mode leaves the stage as-authored so it can be
/// relocated by hand; this only governs Play.
///
/// The 5 methane PROPS sit outside the stage hierarchy on the
/// workspace, so toggling the stage alone would leave them visible.
/// This also gathers those loose props by their methane itemIds and toggles them
/// alongside the stage — no reparenting, so the hand placement is untouched.
public class MethaneStageVisibility : MonoBehaviour
{
    [SerializeField] private GameObject stage;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private LabTourGuide tour;

    public const string MethaneModuleId = "tutorial-methane";

    /// The methane-only props (by LabItem.itemId) that must hide with the stage.
    /// NOTE the bench TOOLS (mortar/pestle/scoop) are deliberately NOT here — the
    /// user wants them usable in BOTH modes all the time, so they are detached from
    /// the stage and stay visible (see menu "Make Methane Bench Tools Permanent").
    private static readonly HashSet<string> PropItemIds = new HashSet<string>
    { "burner", "glass-tube", "collection-tube", "reagent-jar" };   // no lit-splint (not in manuscript)

    private readonly List<GameObject> _looseProps = new List<GameObject>();
    private bool _gathered;

    /// Pure rule (suite-pinned): visible during the Lab Tour OR while the Methane
    /// attempt is the active one; hidden otherwise.
    public static bool ShouldShow(bool tourActive, bool methaneAttemptActive)
        => tourActive || methaneAttemptActive;

    /// Builder/test seam.
    public void Bind(GameObject stage, ExperimentRunner runner, LabTourGuide tour)
    { this.stage = stage; this.runner = runner; this.tour = tour; }

    private void Start()
    {
        Gather();
        Apply(ShowNow());
    }

    private void Update()
    {
        if (!Application.isPlaying) return;   // EDITOR: always visible for arranging
        Apply(ShowNow());
    }

    /// The METHANE-SPECIFIC staged props are present only where they belong:
    /// the Lab Tour and the Methane tutorial. Any other campaign
    /// experiment hides them — nothing else uses sodium-acetate/soda-lime, the
    /// hard-glass tube or the gas collection tube.
    ///
    /// This is NOT a violation of the all-tools-always-present rule: that rule
    /// protects the GENERAL apparatus + raw reagents every experiment draws from
    /// (kits, racks, shelf/cabinet reagents), which stay out permanently. These
    /// four are methane-only staged props.
    ///
    /// Detection deliberately covers the WHOLE methane flow — from the moment
    /// methane is CHOSEN, not just once its graph is live — because gating on the
    /// live run leaves the player unable to pick the tubes up while setting up.
    private bool ShowNow()
    {
        bool tourActive = tour != null && tour.IsActive;
        bool methaneRunning = runner != null && runner.Graph != null
                              && runner.Module != null && runner.Module.moduleId == MethaneModuleId;
        bool methaneSelected = GameFlow.SelectedModuleId == MethaneModuleId;
        bool otherRunning = runner != null && runner.Module != null
                            && runner.Module.moduleId != MethaneModuleId;
        return ShouldShow(tourActive, (methaneRunning || methaneSelected) && !otherRunning);
    }

    /// Find the loose methane props once (while still active, so they're findable).
    private void Gather()
    {
        if (_gathered) return;
        _gathered = true;
        _looseProps.Clear();
        foreach (var li in FindObjectsByType<LabItem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (li == null || !PropItemIds.Contains(li.itemId)) continue;
            // Skip props already inside the stage (the stage toggle covers them);
            // capture only the ones moved out onto the workspace.
            if (stage != null && li.transform.IsChildOf(stage.transform)) continue;
            _looseProps.Add(li.gameObject);
        }
    }

    private void Apply(bool show)
    {
        if (stage != null && stage.activeSelf != show) stage.SetActive(show);
        for (int i = 0; i < _looseProps.Count; i++)
        {
            var p = _looseProps[i];
            if (p != null && p.activeSelf != show) p.SetActive(show);
        }
    }
}
