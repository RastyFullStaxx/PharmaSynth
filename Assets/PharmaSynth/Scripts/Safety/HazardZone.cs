using UnityEngine;

/// A hazard volume (spill, hot surface, corrosive) — contact reports a mistake to
/// the runner and can trigger a visual/audio warning. Debounced so a dwell reports once.
public class HazardZone : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private LabErrorType errorType = LabErrorType.ChemicalContact;
    [SerializeField] private string message = "Chemical contact!";
    [SerializeField] private string contactTag = "";   // player/hand
    [SerializeField] private float rearmSeconds = 2f;

    private float _lastReport = -999f;
    private Transform _playerRoot;
    private System.Func<bool> _armed;

    public void SetRunner(ExperimentRunner r) => runner = r;

    /// Builder seam: hazard identity in one call.
    public void Configure(ExperimentRunner r, LabErrorType type, string msg)
    { runner = r; errorType = type; message = msg; }

    /// When set, only colliders under the rig root count (props placed into the
    /// zone never trigger it) — same player test PlayerTriggerRelay uses.
    public void SetPlayerRoot(Transform root) => _playerRoot = root;

    /// When set, contact only reports while armed (e.g. hot surface only counts
    /// once the temperature sim is actually hot).
    public void SetArmedCheck(System.Func<bool> armed) => _armed = armed;

    /// Pure player-membership test (self-tests pin it).
    public static bool IsPlayer(Transform other, Transform playerRoot)
        => playerRoot != null && other != null && (other == playerRoot || other.IsChildOf(playerRoot));

    private void OnTriggerEnter(Collider other)
    {
        if (_playerRoot != null && !IsPlayer(other.transform, _playerRoot)) return;
        if (!string.IsNullOrEmpty(contactTag) && !other.CompareTag(contactTag)) return;
        Report();
    }

    /// Public so it is directly testable / callable by non-physics detectors.
    public void Report()
    {
        if (runner == null) return;
        if (_armed != null && !_armed()) return;
        if (Time.time - _lastReport < rearmSeconds) return;
        _lastReport = Time.time;
        runner.RecordMistake(errorType, message);
    }
}
