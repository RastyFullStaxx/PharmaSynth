using System.Collections.Generic;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// A single piece that came out of a dispenser: once it's been picked up and then
/// set down and left alone (or falls out of the world), it removes itself so used
/// consumables don't accumulate. The dispenser handles refills; this handles the
/// far end of the piece's life.
public class DispensedConsumable : MonoBehaviour
{
    [SerializeField] private float idleLifetime = 12f;
    [SerializeField] private float killY = -1f;

    private XRGrab _grab;
    private Rigidbody _rb;
    private bool _everHeld;
    private float _idle;

    private void Awake()
    {
        _grab = GetComponent<XRGrab>();
        _rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (transform.position.y < killY) { Destroy(gameObject); return; }

        bool held = _grab != null && _grab.isSelected;
        if (held) { _everHeld = true; _idle = 0f; return; }

        float speed = _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
        _idle = speed >= 0.05f ? 0f : _idle + Time.deltaTime;
        if (DispenserMath.ShouldDiscard(_everHeld, false, speed, _idle, idleLifetime))
            Destroy(gameObject);
    }
}
