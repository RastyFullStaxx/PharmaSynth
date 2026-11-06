using UnityEngine;

/// Material-aware drop clatter (§4 action SFX): a FREE (dynamic) item landing
/// plays its material's clip — glass clinks, metal clatters, wood knocks.
/// Impacts at glass-breaking speed stay silent here; BreakableGlassware plays
/// the shatter instead. (No RequireComponent: colliders often live on children;
/// collision events still route to the Rigidbody host.)
public class ImpactSound : MonoBehaviour
{
    [SerializeField] private string key = "drop-wood";
    [SerializeField] private float minSpeed = 0.7f;
    [SerializeField] private float maxSpeed = float.PositiveInfinity;
    [SerializeField] private float cooldownSeconds = 0.3f;

    private Rigidbody _rb;
    private float _readyAt;

    void Awake() { if (_rb == null) _rb = GetComponent<Rigidbody>(); }

    public void Bind(Rigidbody rb, string soundKey, float breakSpeedCeiling = float.PositiveInfinity)
    { _rb = rb; key = soundKey; maxSpeed = breakSpeedCeiling; }

    void OnCollisionEnter(Collision c)
    {
        if (_rb == null || _rb.isKinematic) return;
        if (Time.time < _readyAt) return;
        float v = c.relativeVelocity.magnitude;
        // A CAREFUL placement never clears minSpeed, so setting glassware down
        // gently — the most common thing a player does — would make no sound at all.
        // Below the threshold it gets a soft set-down cue
        // instead of silence; above it keeps the material-correct impact.
        if (v < minSpeed)
        {
            if (v > 0.05f && Time.time >= _readyAt)
            {
                _readyAt = Time.time + cooldownSeconds;
                AudioService.TryPlayFirstAt(transform.position, 0.7f, "set-down-soft", key);
            }
            return;
        }
        if (v >= maxSpeed) return;
        _readyAt = Time.time + cooldownSeconds;
        // Positional: the clatter comes from where the item actually landed.
        // Loudness scales with impact speed so a gentle set-down whispers and
        // only a real drop clatters at full volume.
        var at = c.contactCount > 0 ? c.GetContact(0).point : transform.position;
        AudioService.TryPlayAt(key, at, Mishandling.ImpactVolume01(v));
    }
}
