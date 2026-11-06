using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Pure, testable record of what went into a vessel (feedback layer).
/// Display-only — chemistry stays in LiquidPhysics/ReactionRegistry; this just
/// remembers the story so hover cards and mix feedback can say "Ethanol 120 ml
/// + NaOH 50 ml" or "Reacted -> Acetanilide". Volumes are per-chemical totals;
/// a reaction collapses the story to the product (matching what the vessel now
/// holds), keeping summaries short after multi-step syntheses.
public class VesselLedger
{
    private readonly List<string> _order = new List<string>();
    private readonly Dictionary<string, float> _ml = new Dictionary<string, float>();
    private readonly HashSet<string> _solid = new HashSet<string>();   // grams, not ml

    public int Count => _order.Count;

    /// Entry names in insertion order (read-only) — lets a caller reason about
    /// the vessel's STORY ("product + wash water only" = decantable, Exp 7).
    public System.Collections.Generic.IReadOnlyList<string> Names => _order;

    /// Record an accepted add of `ml` of `chemicalName`. `solid` switches the summary
    /// unit to grams: "Aspirin 0.5 g", not the int-rounded "0 ml" of a spatula dip.
    public void Add(string chemicalName, float ml, bool solid = false)
    {
        if (string.IsNullOrEmpty(chemicalName) || ml <= 0f) return;
        if (!_ml.ContainsKey(chemicalName))
        {
            _order.Add(chemicalName);
            _ml[chemicalName] = 0f;
        }
        _ml[chemicalName] += ml;
        if (solid) _solid.Add(chemicalName);
    }

    /// A registered reaction fired: the story collapses to the product.
    public void React(string resultName)
    {
        float total = 0f;
        foreach (var kv in _ml) total += kv.Value;
        _order.Clear();
        _ml.Clear();
        _solid.Clear();
        if (string.IsNullOrEmpty(resultName)) return;
        _order.Add(resultName);
        _ml[resultName] = total;
    }

    /// Everything poured out / vessel emptied.
    public void Clear()
    {
        _order.Clear();
        _ml.Clear();
        _solid.Clear();
    }

    /// A PARTIAL pour-out: a mixture leaves evenly, so every entry shrinks by the
    /// same fraction. Without this the name tag kept quoting the volumes that were
    /// poured IN, so a half-spilled tube still read "Ethanol 20 ml" after the
    /// spill: the measurement stayed behind from the
    /// spilled reagent). frac >= 1 is a no-op; frac <= 0 empties the story.
    public void Scale(float frac)
    {
        if (frac >= 0.999f) return;
        if (frac <= 0.001f) { Clear(); return; }
        for (int i = 0; i < _order.Count; i++) _ml[_order[i]] *= frac;
    }

    /// "Ethanol 120 ml + NaOH 50 ml" (insertion order, at most `max` entries,
    /// "+ n more" tail beyond that). Sub-ml amounts keep one decimal; solids
    /// read in grams. Empty ledger -> "".
    /// Pass All to name every reagent (every reagent in the test
    /// tube is shown, never folded into "+ more"). The vessel's own label uses it — a tube holding four
    /// things is exactly when the player needs to read all four. The cap stays for the
    /// narrow hover card, where a long story would overflow the panel.
    public const int All = 0;

    public string Summary(int max = 3)
    {
        if (_order.Count == 0) return "";
        if (max <= All) max = int.MaxValue;
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var name in _order)
        {
            if (shown >= max) { sb.Append(" + ").Append(_order.Count - shown).Append(" more"); break; }
            if (shown > 0) sb.Append(" + ");
            sb.Append(name).Append(' ').Append(_ml[name].ToString("0.#"))
              .Append(_solid.Contains(name) ? " g" : " ml");
            shown++;
        }
        return sb.ToString();
    }
}
