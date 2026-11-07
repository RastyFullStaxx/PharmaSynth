using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// Period hub + experiment-select. Presents the 11-experiment roster grouped by
/// period with per-entry state (locked / available / passed) driven by the
/// ProgressionFlow gate, and launches the chosen experiment into the lab scene.
///
/// The presentation model (BuildModel/StateOf) and the launch gate (CanSelect) are
/// pure statics so they are unit-testable without a scene or disk.
public class HubSelectController : MonoBehaviour
{
    [SerializeField] private string labSceneName = "SampleScene";

    [Header("Runtime UI (one entry per catalog experiment, in order)")]
    [SerializeField] private Button[] entryButtons = new Button[0];
    [SerializeField] private TMP_Text[] entryLabels = new TMP_Text[0];
    [SerializeField] private TMP_Text overallText;

    static readonly Color LockedC = new Color(0.35f, 0.37f, 0.42f, 1f);
    static readonly Color AvailC = new Color(0.25f, 0.8f, 1f, 1f);
    static readonly Color PassedC = new Color(0.35f, 0.85f, 0.45f, 1f);

    public enum RowState { Locked, Available, Passed }

    public class Row
    {
        public string moduleId;
        public string title;
        public ExperimentPeriod period;
        public bool periodUnlocked;   // the period door is open
        public bool unlocked;         // this experiment's prerequisite is cleared
        public bool passed;           // two-part gate cleared before
        public RowState State => passed ? RowState.Passed : (unlocked ? RowState.Available : RowState.Locked);
    }

    /// Build the roster view for the given progress state, in catalog order.
    public static List<Row> BuildModel(ProgressionFlow flow)
    {
        var rows = new List<Row>();
        if (flow == null) return rows;
        foreach (var e in ExperimentCatalog.Entries)
        {
            bool periodOpen = flow.IsPeriodUnlocked(e.period);
            rows.Add(new Row
            {
                moduleId = e.moduleId,
                title = e.title,
                period = e.period,
                periodUnlocked = periodOpen,
                unlocked = periodOpen && flow.IsUnlocked(e.moduleId),
                passed = flow.IsPassed(e.moduleId),
            });
        }
        return rows;
    }

    public static RowState StateOf(Row r) => r == null ? RowState.Locked : r.State;

    /// May the player launch this experiment? True when its period door is open AND
    /// its prerequisite is cleared (passed experiments remain replayable).
    public static bool CanSelect(ProgressionFlow flow, string moduleId)
    {
        var entry = ExperimentCatalog.Get(moduleId);
        if (flow == null || entry == null) return false;
        return flow.IsPeriodUnlocked(entry.period) && flow.IsUnlocked(moduleId);
    }

    private void OnEnable() => Refresh();

    /// Rebuild the select buttons from the live progression state (called on show).
    public void Refresh()
    {
        var service = new ProgressionService();
        service.Load();
        var flow = ProgressionFlow.Create(service);
        var model = BuildModel(flow);

        for (int i = 0; i < entryButtons.Length && i < model.Count; i++)
        {
            var row = model[i];
            var st = row.State;
            if (i < entryLabels.Length && entryLabels[i] != null)
            {
                string tag = st == RowState.Passed ? "   (done)" : (st == RowState.Locked ? "   (locked)" : "");
                entryLabels[i].text = GlyphSafe.Sanitize(row.title + tag);
                entryLabels[i].color = st == RowState.Passed ? PassedC : (st == RowState.Locked ? LockedC : AvailC);
            }
            if (entryButtons[i] != null)
            {
                entryButtons[i].interactable = st != RowState.Locked;
                string id = row.moduleId;             // capture for the closure
                entryButtons[i].onClick.RemoveAllListeners();
                entryButtons[i].onClick.AddListener(() => Select(id));
            }
        }
        if (overallText != null)
            overallText.text = "Progress: " + flow.PassedCount() + " / " + ExperimentCatalog.Count
                             + "  (" + Mathf.RoundToInt(flow.OverallCompletion01() * 100f) + "%)";
    }

    /// Launch an experiment if it is selectable. Returns false (no-op) when locked.
    public bool Select(string moduleId)
    {
        var service = new ProgressionService();
        service.Load();
        var flow = ProgressionFlow.Create(service);
        if (!CanSelect(flow, moduleId)) return false;
        GameFlow.Select(moduleId);
        if (Application.isPlaying)
            ScreenFader.FadeOutThen(() => SceneManager.LoadScene(labSceneName));
        return true;
    }
}
