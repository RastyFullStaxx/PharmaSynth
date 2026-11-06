using UnityEngine;

/// "What did my mix produce?" feedback on a vessel: a registered
/// reaction pops its authored observation text + a colour flash; a harmless
/// no-rule mix reports the mixture story; an overflow says "Vessel full!" and
/// wets the bench. Hazardous mixes stay SILENT here — HazardousMixReactor owns
/// that consequence theatre (smoke/fire/alarm) and double-messaging would
/// bury it. Feedback only: no new graded mistakes.
public class MixFeedback : MonoBehaviour
{
    private LiquidPhysics _lp;
    private float _quietUntil;   // throttle chatty events (per-vessel)
    private ReactionRule _lastRule;
    private float _lastReactionAt = -999f;

    /// Pure policy the suite pins: only non-hazardous no-rule mixes announce.
    public static bool ShouldAnnounceWrongMix(HazardousMix.HazardOutcome outcome)
        => outcome == HazardousMix.HazardOutcome.None;

    /// Pure (suite-pinned): the SAME rule re-firing within the window stays
    /// quiet, or Exp 4's five HCl squeezes each re-fire "white crystals
    /// separate" (5 popups + 5 stings for one action burst).
    /// A different rule always announces.
    public static bool ShouldShowObservation(bool sameRule, float now, float lastAt, float window = 4f)
        => !sameRule || now - lastAt >= window;

    /// Builder seam.
    public void Bind(LiquidPhysics lp)
    {
        if (_lp != null)
        {
            _lp.ReactionOccurred -= OnReaction;
            _lp.WrongReagentMixed -= OnWrongMix;
            _lp.LiquidRejected -= OnRejected;
            _lp.ReactionPending -= OnPending;
        }
        _lp = lp;
        if (_lp == null) return;
        _lp.ReactionOccurred += OnReaction;
        _lp.WrongReagentMixed += OnWrongMix;
        _lp.LiquidRejected += OnRejected;
        _lp.ReactionPending += OnPending;
    }

    /// The right recipe, cold: tell the player exactly what the procedure wants
    /// next (reactions do not fire before their heat step).
    private void OnPending(ReactionRule rule)
    {
        if (rule == null || Time.time < _quietUntil) return;
        _quietUntil = Time.time + 2.5f;
        FloatingText.Show("Needs heat — warm to " + Mathf.RoundToInt(rule.minTemperatureC) + " C (water bath)",
                          PopupPos(), new Color(1f, 0.75f, 0.4f));
    }

    private void OnDestroy() { if (_lp != null) Bind(null); }

    private Vector3 PopupPos()
    {
        // SOLID meshes only — a leftover world-space pour StreamLine would put the
        // popup wherever the arc last landed instead of over the vessel.
        var b = ExperimentSceneBuilder.SolidWorldBounds(gameObject);
        return new Vector3(b.center.x, b.max.y + 0.08f, b.center.z);
    }

    private void OnReaction(ReactionRule rule)
    {
        if (rule == null) return;
        bool same = rule == _lastRule;
        bool show = ShouldShowObservation(same, Time.time, _lastReactionAt);
        _lastRule = rule; _lastReactionAt = Time.time;   // a burst keeps refreshing the window
        if (!show) return;
        string text = !string.IsNullOrEmpty(rule.expectedObservation)
            ? rule.expectedObservation
            : "Reaction: " + (rule.resultLiquid != null ? rule.resultLiquid.chemicalName : "product formed");
        Color c = rule.resultLiquid != null ? rule.resultLiquid.liquidColor : new Color(0.6f, 0.9f, 1f);
        FloatingText.Show(text, PopupPos(), Color.white);
        if (Application.isPlaying) EffectVfx.ColorFlash(PopupPos(), c);
    }

    private void OnWrongMix(ChemicalData current, ChemicalData incoming)
    {
        // An EXPECTED pour (a pending step of this vessel names the incoming
        // chemical) is the binding's moment — it shows the live "6 / 10 ml"
        // count; an amber "Mixed — no reaction" on top reads as an error message
        // during every correct dilution.
        var binding = GetComponent<LiquidTaskBinding>();
        if (binding != null && binding.IsExpectedNow(incoming)) return;

        if (!ShouldAnnounceWrongMix(HazardousMix.Classify(current, incoming))) return;   // reactor owns hazards
        if (Time.time < _quietUntil) return;
        _quietUntil = Time.time + 2.5f;
        string story = _lp != null ? _lp.Ledger.Summary(3) : "";
        FloatingText.Show(string.IsNullOrEmpty(story) ? "Mixed — no reaction" : "Mixed: " + story + " — no reaction",
                          PopupPos(), new Color(1f, 0.85f, 0.5f));
    }

    private void OnRejected(ChemicalData incoming, float ml)
    {
        if (Time.time < _quietUntil) return;
        _quietUntil = Time.time + 2.5f;
        FloatingText.Show("Vessel full!", PopupPos(), new Color(1f, 0.6f, 0.5f));
        if (Application.isPlaying && _lp != null)
        {
            Color c = incoming != null ? incoming.liquidColor : new Color(0.55f, 0.7f, 0.85f);
            SpillPuddle.Spawn(transform.position + Vector3.up * 0.01f, c, 0.08f);
        }
    }
}
