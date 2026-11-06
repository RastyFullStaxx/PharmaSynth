using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Focused checklist text for the holo procedures board (the
/// flat pads rendered every phase at full detail into a fixed rect, so long
/// checklists overflowed and overlaid the reaction footer — unreadable).
/// Instead of shrinking text to fit, collapse what the player doesn't need:
/// completed phases fold to a "done n/n" line, future phases to a "(n steps)"
/// stub, and only the ACTIVE phase renders its full step list. Line count is
/// bounded by construction (phases + longest single phase), so the text always
/// fits its rect at a readable size.
public static class ChecklistPager
{
    /// The phase the player is working in: the phase of the first available
    /// task, else the first phase with an incomplete task (all-complete → null).
    public static TaskPhase? ActivePhase(TaskGraph graph)
    {
        if (graph == null) return null;
        foreach (var t in graph.Tasks)
            if (graph.IsAvailable(t.taskId)) return t.phase;
        foreach (var t in graph.Tasks)
            if (!graph.IsComplete(t.taskId)) return t.phase;
        return null;
    }

    /// OBJECTIVES header — the manuscript's Intended Learning Outcomes, on the wrist
    /// board for the WHOLE run. This is the only reader of `intendedLearningOutcomes`
    /// after the intro cutscene speaks them once.
    /// Sits ABOVE materials: why you're here, then what to grab.
    public static string BuildObjectivesHeader(ExperimentModuleDefinition module)
    {
        if (module == null) return string.Empty;
        var ilos = module.intendedLearningOutcomes;
        if (ilos == null || ilos.Count == 0) return string.Empty;

        var sb = new StringBuilder(256);
        sb.Append("<b>OBJECTIVES</b> <size=78%><color=#AEB9CC>— what this session teaches</color></size>\n<size=85%>");
        foreach (var o in ilos)
        {
            if (string.IsNullOrWhiteSpace(o)) continue;
            sb.Append("• ").Append(GlyphSafe.Sanitize(o.Trim())).Append('\n');
        }
        sb.Append("</size>\n");
        return sb.ToString();
    }

    /// MATERIALS header for the top of the holo board: everything
    /// the experiment will need — reagents WITH totals, apparatus with counts — so
    /// the player can assemble the bench BEFORE starting. Deliberately not part of
    /// the checklist: it completes nothing and stays fully visible for the whole
    /// run (the player scrolls; the panel keeps its scroll position between opens).
    public static string BuildMaterialsHeader(ExperimentModuleDefinition module)
    {
        if (module == null) return string.Empty;
        bool anyR = module.materialReagents != null && module.materialReagents.Count > 0;
        bool anyA = module.materialApparatus != null && module.materialApparatus.Count > 0;
        if (!anyR && !anyA) return string.Empty;

        // One BULLET ROW per item — the dot-joined single line
        // wrapped mid-name and made totals hard to scan against the bench.
        var sb = new StringBuilder(256);
        sb.Append("<b>MATERIALS</b> <size=78%><color=#AEB9CC>— gather these before you start</color></size>\n");
        if (anyR)
        {
            sb.Append("<color=#61D6FF>Reagents:</color>\n<size=85%>");
            foreach (var r in module.materialReagents)
                sb.Append("• ").Append(GlyphSafe.Sanitize(r)).Append('\n');
            sb.Append("</size>");
        }
        if (anyA)
        {
            sb.Append("<color=#61D6FF>Apparatus:</color>\n<size=85%>");
            foreach (var a in module.materialApparatus)
                sb.Append("• ").Append(GlyphSafe.Sanitize(a)).Append('\n');
            sb.Append("</size>");
        }
        sb.Append('\n');
        return sb.ToString();
    }

    /// `stepDetail`, when given, returns extra lines for a current step (the per-tube
    /// rack breakdown); they print under that step's hint.
    public static string BuildFocusedText(TaskGraph graph, System.Func<string, string> stepDetail = null)
    {
        if (graph == null) return string.Empty;
        TaskPhase? active = ActivePhase(graph);

        // Ordered distinct phases + counts (authoring order groups phases).
        var order = new List<TaskPhase>();
        var total = new Dictionary<TaskPhase, int>();
        var done = new Dictionary<TaskPhase, int>();
        foreach (var t in graph.Tasks)
        {
            if (!total.ContainsKey(t.phase)) { order.Add(t.phase); total[t.phase] = 0; done[t.phase] = 0; }
            total[t.phase]++;
            if (graph.IsComplete(t.taskId)) done[t.phase]++;
        }

        var sb = new StringBuilder(256);
        bool first = true;
        foreach (var phase in order)
        {
            if (!first) sb.Append('\n');
            first = false;
            string label = TabletChecklistController.PhaseLabel(phase);
            if (active.HasValue && phase == active.Value)
            {
                sb.Append("<b>").Append(label).Append("</b>\n");
                foreach (var t in graph.Tasks)
                {
                    if (t.phase != phase) continue;
                    string mark = graph.IsComplete(t.taskId) ? "<color=#5FD08A>•</color>"
                                : (graph.IsAvailable(t.taskId) ? "<color=#61D6FF>»</color>"
                                : "<color=#7C8AA5>□</color>");
                    sb.Append(mark).Append(' ').Append(GlyphSafe.Sanitize(t.label)).Append('\n');
                    // The CURRENT step also shows its how-to hint so the board tells
                    // the player exactly what to do.
                    if (graph.IsAvailable(t.taskId) && !string.IsNullOrWhiteSpace(t.hint))
                        sb.Append("    <size=78%><color=#AEB9CC>").Append(GlyphSafe.Sanitize(t.hint))
                          .Append("</color></size>\n");
                    string detail = graph.IsAvailable(t.taskId) ? stepDetail?.Invoke(t.taskId) : null;
                    if (!string.IsNullOrWhiteSpace(detail))
                        sb.Append("    <size=78%><color=#61D6FF>")
                          .Append(GlyphSafe.Sanitize(detail).Replace("\n", "\n    "))
                          .Append("</color></size>\n");
                }
            }
            else if (done[phase] >= total[phase])
            {
                sb.Append("<b>").Append(label).Append("</b>  <color=#5FD08A>• done ")
                  .Append(done[phase]).Append('/').Append(total[phase]).Append("</color>\n");
            }
            else
            {
                sb.Append("<color=#7C8AA5>□ <b>").Append(label).Append("</b> (")
                  .Append(total[phase]).Append(" steps)</color>\n");
            }
        }
        return sb.ToString().TrimEnd();
    }

    /// Status header for the holo board (the single procedures panel). TWO lines: the
    /// step label gets its own wrapped line so a long instruction never renders as one
    /// unreadable row; stats sit below.
    public static string BuildHeader(ExperimentRunner runner)
    {
        if (runner == null || runner.Graph == null) return "";
        string current = "—";
        foreach (var t in runner.Graph.AvailableTasks()) { current = GlyphSafe.Sanitize(t.label); break; }
        return "<color=#61D6FF>»</color> " + current
             + "\n<color=#61D6FF>" + ExperimentHudController.FormatPercent(runner.Progress01) + "</color>"
             + " · Mastery " + Mathf.RoundToInt(runner.OverallMastery * 100f) + "%";
    }
}
