using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Grabbable affordance (prop readability): when a hand/ray
/// hovers a real-scale lab tool, it brightens (base-colour tint via a
/// MaterialPropertyBlock — no per-material keyword or shader needed) and pops
/// slightly larger, so small items are easy to spot and grab; it restores on
/// hover-exit and while actually held. Thin MB over a pure scale helper.
public class HoverHighlight : MonoBehaviour
{
    [SerializeField] private float scaleFactor = 1.06f;
    [SerializeField] private Color glow = new Color(0.55f, 0.9f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float glowMix = 0.45f;

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");

    private XRGrab _grab;
    private Renderer[] _rends;
    private MaterialPropertyBlock _mpb;
    private Color[] _orig;
    private bool[] _hasBase, _hasColor;
    private Vector3 _baseScale;
    private bool _cached;

    // TWO independent sources, not one bool. Tutorial Mode lights an
    // object because it is the next step; hover lights it because a ray is on it.
    // With a single flag, waving the ray past a guided object and off again would
    // silently switch the guidance glow off.
    private bool _hover, _guide, _dim;
    private TargetRole _guideRole;

    /// How far a non-target is pushed toward black while a guided step is running.
    /// Glowing one bottle among 57 near-identical ones is only a RELATIVE signal — it
    /// competes with 56 equally bright neighbours. Dimming the rest turns it into an
    /// absolute one. Not fully dark: the player must still be able to read the shelf
    /// and pick a different bottle if they want to.
    private static readonly Color DimTowards = new Color(0.10f, 0.11f, 0.14f, 1f);
    private const float DimMix = 0.62f;

    private static readonly Color GuideSource      = new Color(1f, 0.72f, 0.20f, 1f);   // amber: go fetch this
    private static readonly Color GuideDestination = new Color(0.35f, 1f, 0.45f, 1f);   // green: put it here

    public bool IsHighlighted => _hover || _guide;
    public bool IsGuided => _guide;

    void Awake() { if (_grab == null) Bind(GetComponent<XRGrab>()); }

    /// Edit-mode seam (Awake doesn't fire on AddComponent in edit mode).
    public void Bind(XRGrab grab)
    {
        if (_grab != null)
        {
            _grab.hoverEntered.RemoveListener(OnHoverEnter);
            _grab.hoverExited.RemoveListener(OnHoverExit);
            _grab.selectEntered.RemoveListener(OnSelect);
            _grab.selectExited.RemoveListener(OnRelease);
        }
        _grab = grab;
        if (_grab != null)
        {
            _grab.hoverEntered.AddListener(OnHoverEnter);
            _grab.hoverExited.AddListener(OnHoverExit);
            _grab.selectEntered.AddListener(OnSelect);
            _grab.selectExited.AddListener(OnRelease);
        }
        Cache();
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.hoverEntered.RemoveListener(OnHoverEnter);
            _grab.hoverExited.RemoveListener(OnHoverExit);
            _grab.selectEntered.RemoveListener(OnSelect);
            _grab.selectExited.RemoveListener(OnRelease);
        }
    }

    private void Cache()
    {
        if (_cached) return;
        _rends = GetComponentsInChildren<Renderer>();
        _mpb = new MaterialPropertyBlock();
        _baseScale = transform.localScale;
        _orig = new Color[_rends.Length];
        _hasBase = new bool[_rends.Length];
        _hasColor = new bool[_rends.Length];
        for (int i = 0; i < _rends.Length; i++)
        {
            var mat = _rends[i] != null ? _rends[i].sharedMaterial : null;
            _hasBase[i] = mat != null && mat.HasProperty(BaseColorID);
            _hasColor[i] = mat != null && mat.HasProperty(ColorID);
            _orig[i] = _hasBase[i] ? mat.GetColor(BaseColorID)
                     : (_hasColor[i] ? mat.GetColor(ColorID) : Color.white);
        }
        _cached = true;
    }

    // Global throttle so sweeping a ray across a shelf of items doesn't machine-gun
    // the hover blip — at most one hover tick per this many seconds, lab-wide.
    private const float HoverSfxInterval = 0.09f;
    private static float _lastHoverSfx = -1f;

    private void OnHoverEnter(HoverEnterEventArgs _)
    {
        SetHighlight(true);
        if (Application.isPlaying && Time.unscaledTime - _lastHoverSfx >= HoverSfxInterval)
        {
            _lastHoverSfx = Time.unscaledTime;
            AudioService.TryPlay("hover");
        }
    }
    private void OnHoverExit(HoverExitEventArgs _) => SetHighlight(false);
    private void OnSelect(SelectEnterEventArgs _)
    {
        SetHighlight(false);                       // grabbed → drop the glow
        if (Application.isPlaying) AudioService.TryPlay("grab");   // universal grab/hold cue
    }

    /// The universal RELEASE cue. Nothing else listens to `selectExited`,
    /// so without this, letting go of anything is
    /// silent while picking it up chirps. Played
    /// centrally here for the same reason "grab" is: exactly once, on everything
    /// grabbable, without each verb having to remember.
    private void OnRelease(SelectExitEventArgs _)
    {
        if (Application.isPlaying) AudioService.TryPlayFirst("release", "grab");
    }

    /// Pure scale rule (self-tested): grow by factor while lit, back to base otherwise.
    public static Vector3 HighlightScale(Vector3 baseScale, bool on, float factor)
        => on ? baseScale * Mathf.Max(1f, factor) : baseScale;

    /// Toggle the hover look. Public so tests / other affordance drivers can call it.
    public void SetHighlight(bool on)
    {
        if (on == _hover) return;
        _hover = on;
        Apply();
    }

    /// Toggle the Tutorial Mode guidance look. Independent of hover: neither channel
    /// can clear the other, and the role picks the tint (amber source / green target).
    public void SetGuide(bool on, TargetRole role)
    {
        if (on == _guide && role == _guideRole) return;
        _guide = on; _guideRole = role;
        Apply();
    }

    /// Spotlight channel: push everything that is NOT part of the current step back,
    /// so the guided object stands out absolutely rather than relatively.
    public void SetDimmed(bool on)
    {
        if (on == _dim) return;
        _dim = on;
        Apply();
    }

    public bool IsDimmed => _dim;

    private void Apply()
    {
        Cache();
        bool lit = _hover || _guide;
        transform.localScale = HighlightScale(_baseScale, lit, scaleFactor);
        if (_rends == null) return;
        // Priority: guidance > hover > dim > untouched. "THIS is the next thing"
        // outranks "your ray is on something", and BOTH outrank being pushed back —
        // a dimmed object you deliberately point at must still respond, or the lab
        // stops feeling alive outside the one glowing bottle.
        Color tint = _guide
            ? (_guideRole == TargetRole.Source ? GuideSource : GuideDestination)
            : glow;
        for (int i = 0; i < _rends.Length; i++)
        {
            if (_rends[i] == null) continue;
            _rends[i].GetPropertyBlock(_mpb);
            Color c = lit ? Color.Lerp(_orig[i], tint, glowMix)
                    : (_dim ? Color.Lerp(_orig[i], DimTowards, DimMix) : _orig[i]);
            if (_hasBase[i]) _mpb.SetColor(BaseColorID, c);
            if (_hasColor[i]) _mpb.SetColor(ColorID, c);
            _rends[i].SetPropertyBlock(_mpb);
        }
    }
}
