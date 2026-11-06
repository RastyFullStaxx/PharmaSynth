using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// The test-tube brush: while held and touching a dirty vessel, its travel
/// accumulates — every SwipeDistance of scrubbing motion knocks one swipe off
/// the dirtiness.
public class BrushController : MonoBehaviour
{
    [SerializeField] private float probeRadius = 0.05f;

    private XRGrab _grab;
    private Vector3 _lastPos;
    private float _travel;
    private CleanableVessel _target;

    public void Bind(XRGrab grab) { _grab = grab; _lastPos = transform.position; }

    void Awake() { if (_grab == null) Bind(GetComponent<XRGrab>()); }

    void Update()
    {
        if (!Application.isPlaying) return;
        bool held = _grab != null && _grab.isSelected;
        if (!held) { _target = null; _travel = 0f; _lastPos = transform.position; return; }

        var cols = Physics.OverlapSphere(transform.position, probeRadius, ~0, QueryTriggerInteraction.Ignore);
        CleanableVessel touching = null;
        foreach (var col in cols)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var cv = col.GetComponentInParent<CleanableVessel>();
            if (cv != null && cv.Dirtiness > 0f) { touching = cv; break; }
        }

        if (touching != _target) { _target = touching; _travel = 0f; }
        if (_target != null)
        {
            _travel += (transform.position - _lastPos).magnitude;
            if (_travel >= CleanupMath.SwipeDistance)
            {
                _travel = 0f;
                _target.Scrub();
                // Bristle-on-glass, not the liquid swirl.
                AudioService.TryPlayFirstAt(transform.position, 0.35f, "scrub", "stir");
            }
        }
        _lastPos = transform.position;
    }
}
