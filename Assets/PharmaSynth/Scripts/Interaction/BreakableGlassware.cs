using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Glassware that shatters when dropped hard (§2 mishandling penalties).
/// Only a FREE (dynamic, un-held) item can break — kinematic shelf items and
/// items currently in a hand never do, no matter how they scrape a wall. On
/// break: shatter SFX, a DroppedGlassware mistake against the Sanitation rubric,
/// and the item goes home via DropRespawn as a fresh replacement so the
/// experiment stays completable.
public class BreakableGlassware : MonoBehaviour
{
    [SerializeField] private float breakImpactSpeed = Mishandling.DefaultBreakSpeed;
    [SerializeField, Min(0f)] private float rearmSeconds = 1.5f;
    // A just-released item still carries the hand's velocity for a frame or two;
    // don't let that release spike shatter it against nearby geometry. Only a
    // genuine free-flight impact after this grace window counts.
    [SerializeField, Min(0f)] private float releaseGraceSeconds = 0.35f;

    private ExperimentRunner _runner;
    private DropRespawn _respawn;
    private Rigidbody _rb;
    private XRGrab _grab;
    private string _label = "Glassware";
    private float _cooldownUntil;
    private float _releasedAt = -999f;

    void Awake()
    {
        // Display name, never the GO/prefab code name (otherwise shatter
        // messages read "Reagent_AceticAcid_Diluted").
        if (_rb == null)
            Bind(FindAnyObjectByType<ExperimentRunner>(), GetComponent<DropRespawn>(), GetComponent<Rigidbody>(),
                 Mishandling.DisplayNameFor(gameObject));
    }

    /// Edit-mode / builder seam (Awake doesn't fire on edit-mode AddComponent).
    public void Bind(ExperimentRunner runner, DropRespawn respawn, Rigidbody rb, string label)
    {
        _runner = runner; _respawn = respawn; _rb = rb;
        if (!string.IsNullOrEmpty(label)) _label = label;
        if (_grab == null) _grab = GetComponent<XRGrab>();
    }

    // Velocity-tracked grabs keep the Rigidbody DYNAMIC while held, so
    // isKinematic does not signal "in a hand" — track the grab directly and
    // stamp the release time so the grace window can start.
    private bool Held => _grab != null && _grab.isSelected;

    void OnCollisionEnter(Collision collision)
    {
        if (_rb == null || _rb.isKinematic) return;          // parked on the shelf
        if (Held) { _releasedAt = Time.time; return; }       // in a hand — never breaks
        if (Time.time - _releasedAt < releaseGraceSeconds) return;   // just let go — ignore the spike
        if (Time.time < _cooldownUntil) return;
        if (!Mishandling.ShouldBreak(collision.relativeVelocity.magnitude, breakImpactSpeed)) return;
        Break();
    }

    /// Public so tests and hazard systems can force it.
    public void Break()
    {
        _cooldownUntil = Time.time + rearmSeconds;
        // Without a respawn there is no "fresh replacement" — a full shatter
        // would leave the item standing there looking broken-but-fine. Late-bind
        // if possible; otherwise downgrade to a hard clink and keep the item.
        if (_respawn == null) _respawn = GetComponent<DropRespawn>();
        if (_respawn == null)
        {
            AudioService.TryPlayAt("glass-clink", transform.position);
            return;
        }
        if (_runner != null)
            _runner.RecordMistake(LabErrorType.DroppedGlassware, _label + " shattered — handle glassware gently");
        // Positional shatter at the break point + shard burst.
        AudioService.TryPlayAt("glass-shatter", transform.position);
        if (Application.isPlaying) EffectVfx.Shatter(transform.position);
        // A liquid-carrying vessel leaves its contents pooled where it fell,
        // lingering then fading out.
        var liquid = GetComponent<LiquidPhysics>();
        if (Application.isPlaying && liquid != null && liquid.currentLiquidVolume > 1f)
            SpillPuddle.Spawn(transform.position, SpillMistake.LiquidColorOf(liquid));
        if (_respawn != null) _respawn.GoHome();             // replacement back on the shelf
    }
}
