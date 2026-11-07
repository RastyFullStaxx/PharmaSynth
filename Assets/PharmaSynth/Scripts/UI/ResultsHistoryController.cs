using System.Text;
using TMPro;
using UnityEngine;

/// The local Results/History screen (plan §S2 — the descoped analytics dashboard):
/// shows each experiment's best grade / mastery / attempts + overall completion, and
/// writes a spreadsheet-ready CSV to persistentDataPath. Reads the live
/// ProgressionService; the formatting is a pure static so it is unit-testable.
public class ResultsHistoryController : MonoBehaviour
{
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private TMP_Text statusText;

    public void Show() { if (root != null) root.SetActive(true); Refresh(); }
    public void Hide() { if (root != null) root.SetActive(false); }

    public void Refresh()
    {
        var svc = new ProgressionService();
        svc.Load();
        if (bodyText != null) bodyText.text = GlyphSafe.Sanitize(BuildDisplayText(svc));
    }

    /// Rich-text table of the transcript (pure — drives the screen and the tests).
    public static string BuildDisplayText(ProgressionService service)
    {
        var sb = new StringBuilder();
        foreach (var r in ResultsExport.BuildRows(service))
        {
            string badge = r.passed
                ? "<color=#5CD07D>PASS</color>"
                : (r.attempted ? "<color=#FFB86B>TRY</color>" : "<color=#8894A6> -- </color>");
            sb.Append(badge).Append("  ").Append(r.title)
              .Append("   grade ").Append(Mathf.RoundToInt(r.bestGrade)).Append('%')
              .Append("   mastery ").Append(Mathf.RoundToInt(r.bestMastery * 100f)).Append('%')
              .Append("   x").Append(r.attempts).Append('\n');
        }
        sb.Append("\nOverall: ").Append(ResultsExport.PassedCount(service))
          .Append(" / ").Append(ExperimentCatalog.Count).Append(" passed");
        return sb.ToString();
    }

    /// Write the CSV transcript to persistentDataPath; report the path/result.
    public void ExportCsv()
    {
        var svc = new ProgressionService();
        svc.Load();
        string path = System.IO.Path.Combine(Application.persistentDataPath, "pharmasynth_results.csv");
        try
        {
            System.IO.File.WriteAllText(path, ResultsExport.BuildCsv(svc));
            if (statusText != null) statusText.text = "Exported to:\n" + path;
        }
        catch (System.Exception e)
        {
            if (statusText != null) statusText.text = "Export failed: " + e.Message;
        }
    }
}
