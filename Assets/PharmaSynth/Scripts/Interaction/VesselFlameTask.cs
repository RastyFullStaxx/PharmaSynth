using UnityEngine;

/// Pure rules for the FLAME (non-)flammability confirmation (Exp 7: "Try to
/// ignite the chloroform. Light a match and place the flame near the vapors.
/// Observe." — the NEGATIVE is the observation). Zone-free per the client rule:
/// any lit flame brought to the served sample, anywhere in the lab.
public static class FlameTestMath
{
    /// The flame must come this close to the sample's vessel.
    public const float Reach = 0.2f;

    /// Confirmed only when the sample is actually THERE (its reagent step is
    /// served), the flame is LIT, and it is held close — never any two alone.
    public static bool Confirms(bool served, bool flameLit, float distance)
        => served && flameLit && distance <= Reach;

    /// Where a burner's flame actually burns (FlameAnchor child, like
    /// NakedFlameHeat) — bounds-guessing tool ends is banned.
    public static Vector3 FlamePos(Component burner)
    {
        var anchor = burner.transform.Find("FlameAnchor");
        return anchor != null ? anchor.position : burner.transform.position + Vector3.up * 0.12f;
    }
}

/// Completes a flammability-test step ZONE-FREE: once the sample vessel is
/// served, holding any LIT match or burner flame to it confirms the result
/// ("does not ignite") and completes the task. Wired by the scene builder from
/// ExperimentLayout.Vessel.flameTaskId.
public class VesselFlameTask : MonoBehaviour
{
    private ExperimentRunner _runner;
    private string _taskId;
    private LiquidTaskBinding _binding;
    private LiquidPhysics _lp;
    private bool _confirmed;
    private bool _subscribed;
    private float _nextPoll;

    public string TaskId => _taskId;

    public void Bind(ExperimentRunner runner, string taskId, LiquidTaskBinding binding, LiquidPhysics lp)
    {
        if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; }
        _runner = runner; _taskId = taskId; _binding = binding; _lp = lp;
        if (_runner != null) { _runner.ExperimentStarted += OnStarted; _subscribed = true; }
        Register();
    }

    /// Teardown seam (ClearBenchBindings) — bench items survive rebuilds.
    public void Detach()
    { if (_runner != null && _subscribed) { _runner.ExperimentStarted -= OnStarted; _subscribed = false; } }

    private void OnDestroy() => Detach();

    private void OnStarted(ExperimentModuleDefinition _)
    {
        _confirmed = false;   // a Retry replays the verb, not the memory of it
        Register();
    }

    private void Register()
    {
        if (_runner == null || _runner.Graph == null || string.IsNullOrEmpty(_taskId)) return;
        _runner.Graph.RegisterCondition(_taskId, () => _confirmed);
    }

    private void Update()
    {
        if (!Application.isPlaying || _confirmed) return;
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + 0.25f;
        PollFlames();
    }

    /// One scan over every flame in the lab (the play-mode poll AND the
    /// simulated run's stand-in for bringing the match over — same code path).
    public bool PollFlames()
    {
        if (_confirmed || _lp == null || _runner == null || _runner.Graph == null) return false;
        if (!_runner.Graph.IsAvailable(_taskId) || _runner.Graph.IsComplete(_taskId)) return false;
        bool served = _binding == null || _binding.ReadyFor(_taskId);
        foreach (var m in FindObjectsByType<Matchstick>())
            if (m != null && FlameTestMath.Confirms(served, m.IsLit, MatchDistance(m)))
                return Confirm();
        foreach (var bu in FindObjectsByType<BurnerController>())
            if (bu != null && FlameTestMath.Confirms(served, bu.IsLit,
                    Vector3.Distance(FlameTestMath.FlamePos(bu), _lp.transform.position)))
                return Confirm();
        return false;
    }

    /// Match HEAD to the nearest point of the vessel — the flame goes to the sample's
    /// mouth, not its pivot (pivot-to-match-origin missed matches held right at the rim).
    private float MatchDistance(Matchstick m)
    {
        var col = _lp.GetComponentInParent<Collider>();
        Vector3 head = m.HeadPosition;
        float toPivot = Vector3.Distance(head, _lp.transform.position);
        return col != null ? Mathf.Min(toPivot, Vector3.Distance(head, col.bounds.ClosestPoint(head))) : toPivot;
    }

    private bool Confirm()
    {
        _confirmed = true;
        FloatingText.Show("Won't ignite — NON-FLAMMABLE ✓",
            _lp.transform.position + Vector3.up * 0.14f, new Color(0.55f, 0.8f, 1f));
        return true;
    }
}
