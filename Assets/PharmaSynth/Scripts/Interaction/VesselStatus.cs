using UnityEngine;

/// Live contents readout on a vessel's existing ProximityLabel:
/// contents text on hover or when near. Throttled and
/// change-gated so the TMP mesh only rebuilds when the state actually moves.
public class VesselStatus : MonoBehaviour
{
    private LiquidPhysics _lp;
    private ProximityLabel _label;
    private string _displayName;

    /// " — Tollen's test" once a pooled tube has claimed a role, so the tube the
    /// player chose says what it has become. Derived from the claimed role's task LABEL —
    /// never the layout's internal role name, which never appears on the bench.
    private string _roleSuffix = "";
    public void SetRoleSuffix(string suffix) => _roleSuffix = string.IsNullOrEmpty(suffix) ? "" : " — " + suffix;
    private float _showDist = 1.6f;
    private float _nextAt;
    private string _last;

    /// Builder seam (Awake doesn't fire on edit-mode AddComponent).
    public void Bind(LiquidPhysics lp, ProximityLabel label, string displayName, float showDist = 1.6f)
    {
        _lp = lp; _label = label; _displayName = displayName; _showDist = showDist;
        Refresh();
    }

    /// Every ref here is PRIVATE and unserialized, so a hand-placed bench vessel
    /// that nobody calls Bind() on would stay mute forever.
    /// Adopt the
    /// vessel's own components at Awake; a later Bind() still wins.
    private void Awake()
    {
        if (_lp != null) return;
        var lp = GetComponent<LiquidPhysics>();
        var label = GetComponent<ProximityLabel>();
        if (lp == null || label == null) return;
        var item = GetComponent<LabItem>();
        string name = item != null && !string.IsNullOrEmpty(item.displayName)
            ? item.displayName : Mishandling.DisplayNameFor(gameObject);
        Bind(lp, label, name, _showDist);
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAt) return;
        _nextAt = Time.unscaledTime + 0.25f;
        Refresh();
    }

    /// Public for tests + immediate updates.
    public void Refresh()
    {
        if (_lp == null || _label == null) return;
        // ⛔ No "Dirty " / "Clean " prefix. Cleaning is not a requirement;
        // a vessel just has to become empty. Residue gates and grades nothing; the
        // stamp would be its ONLY effect, and to a player a label that says Dirty IS a requirement.
        // Nothing resets it either. The brush and rinse still
        // work as a flourish; the label just never asks for them.
        string name = _displayName + _roleSuffix;
        // A vessel holding a MIX names every element and its amount (the ledger
        // story) — "Ethanol 1 ml + Distilled Water 10 ml"; a single chemical
        // keeps the short form.
        string s = _lp.Ledger.Count > 1 && !_lp.IsEmpty
            ? VesselStatusMath.ComposeMixed(name, _lp.Ledger.Summary(VesselLedger.All))
            : VesselStatusMath.Compose(name,
                _lp.currentChemical != null ? _lp.currentChemical.chemicalName : null,
                _lp.currentLiquidVolume + _lp.currentPptVolume);
        // Zone-free heat/chill steps get a live temperature goal on the tag
        // itself — queried fresh each refresh because the builder's
        // teardown strips these components between modules. Only while the
        // owning step is the player's CURRENT concern (Relevant).
        var heat = GetComponent<VesselHeatTask>();
        var chill = GetComponent<VesselChillTask>();
        string goal = heat != null && heat.Relevant
                        ? VesselStatusMath.TempGoalLine(_lp.currentTempC, heat.RequiredC, false)
                    : chill != null && chill.Relevant
                        ? VesselStatusMath.TempGoalLine(_lp.currentTempC, chill.RequiredC, true) : "";
        if (goal.Length > 0) s += "\n" + goal;
        // What this vessel still wants, and why a pour bounced, shown on
        // any apparatus, since players can use anything.
        // Queried fresh each refresh for the
        // same reason the heat/chill goals are — the builder's teardown strips the binding
        // between modules. Shown only on the glass actually in use (DetailWanted), or the
        // bench becomes a wall of numbers at arm's length.
        if (DetailWanted())
        {
            string need = NeedLineNow();
            if (need.Length > 0) s += NewLine + need;
            var b = GetComponent<LiquidTaskBinding>();
            if (b != null)
            {
                string refused = VesselStatusMath.RefusalLine(b.RefusedReagent);
                if (refused.Length > 0) s += NewLine + refused;
                string free = VesselStatusMath.FreeVesselLine(b.IsPoolMember, b.RoleAmbiguous, _lp.IsEmpty);
                if (free.Length > 0) s += NewLine + free;
            }
        }

        if (s == _last) return;
        _last = s;
        _label.SetLabel(s, _showDist);   // ProximityLabel splits the name from the contents, then sanitizes
    }

    const string NewLine = "\n";

    /// How square-on the player has to be looking for a vessel to count as "the one I am
    /// reading". Tight, because two tubes in a rack are only degrees apart.
    private const float GazeDot = 0.985f;

    /// Is this the glass the player is actually using? Held, being looked at from within
    /// label range, or bound to the step being guided. Anything else keeps the short
    /// contents line, because a bench of vessels all advertising numbers
    /// sends the player to the wrong tube.
    private bool DetailWanted()
    {
        if (TutorialHighlighter.IsHeld(transform)) return true;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 to = transform.position - cam.transform.position;
            float d = to.magnitude;
            if (d > 0.01f && d <= _showDist
                && Vector3.Dot(cam.transform.forward, to / d) >= GazeDot) return true;
        }
        return GuidedStepWantsThis();
    }

    /// True when this vessel advertises a step of the task currently being guided.
    private bool GuidedStepWantsThis()
    {
        var binding = GetComponent<LiquidTaskBinding>();
        if (binding == null || binding.ExpectedSteps == null) return false;
        string guided = GuidedTask();
        if (string.IsNullOrEmpty(guided)) return false;
        foreach (var step in binding.ExpectedSteps)
            if (step != null && step.taskId == guided) return true;
        return false;
    }

    /// The FIRST available task — GuidePath's own convention for "the step being guided".
    private static string GuidedTask()
    {
        var runner = FindAnyObjectByType<ExperimentRunner>();
        if (runner == null || runner.Graph == null || !runner.IsRunning) return null;
        foreach (var t in runner.Graph.AvailableTasks()) return t.taskId;
        return null;
    }

    /// The guided step's outstanding amount for THIS vessel, or "" when it is not the
    /// vessel currently being asked for. Only the guided step is shown: every bound vessel
    /// printing its whole shopping list would turn the bench into a wall of numbers.
    private string NeedLineNow()
    {
        var binding = GetComponent<LiquidTaskBinding>();
        if (binding == null || binding.ExpectedSteps == null) return "";
        var runner = FindAnyObjectByType<ExperimentRunner>();
        if (runner == null || runner.Graph == null || !runner.IsRunning) return "";

        // ⛔ The GUIDED task only - not every AVAILABLE one. Walking the whole
        // AvailableTasks() list with Exp 2's prep steps running in parallel makes the four
        // alkaline tubes advertise "Potassium Permanganate 0 / 2 ml" while the player is
        // still on the ethanol step, and one of them reads as the tube being asked for.
        // GuidePath
        // already treats the FIRST available task as the guided one; reuse that rather than
        // inventing a second notion of "the current step".
        string guided = GuidedTask();
        if (string.IsNullOrEmpty(guided)) return "";

        foreach (var step in binding.ExpectedSteps)
        {
            if (step == null || step.reagent == null || step.taskId != guided) continue;
            if (step.requiredMl <= 0f) continue;
            return VesselStatusMath.NeedLine(step.reagent.chemicalName,
                binding.AccumulatedFor(step.taskId, step.reagent), step.requiredMl,
                step.reagent.state == PhysicalState.Solid);
        }
        return "";
    }
}
