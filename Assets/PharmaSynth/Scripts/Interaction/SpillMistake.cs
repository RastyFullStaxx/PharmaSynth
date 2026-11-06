using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Grades reagent spills (§2 mishandling penalties). LiquidPourer already
/// drains any bottle tipped past its pour threshold — this watches for that
/// happening while NOBODY is holding the bottle (knocked over / dropped) and
/// records one SpilledReagent mistake per episode, re-arming once the bottle
/// is righted. The lost volume itself is the second penalty: it comes out of
/// the finite supply, so a bad spill can starve a step into the existing
/// restart-prompt path.
public class SpillMistake : MonoBehaviour
{
    [SerializeField] private float tiltThreshold = 60f;
    [SerializeField] private float rearmTilt = 30f;

    private ExperimentRunner _runner;
    private LiquidPhysics _liquid;
    private XRGrab _grab;
    private string _label = "Reagent";
    private bool _armed = true;

    void Awake()
    {
        if (_liquid == null)
            Bind(FindAnyObjectByType<ExperimentRunner>(), GetComponent<LiquidPhysics>(), GetComponent<XRGrab>(),
                 Mishandling.DisplayNameFor(gameObject));   // proper name, not the code name
    }

    /// Edit-mode / builder seam.
    public void Bind(ExperimentRunner runner, LiquidPhysics liquid, XRGrab grab, string label)
    {
        _runner = runner; _liquid = liquid; _grab = grab;
        if (!string.IsNullOrEmpty(label)) _label = label;
    }

    void Update()
    {
        float tilt = Vector3.Angle(Vector3.up, transform.up);
        bool held = _grab != null && _grab.isSelected;
        float ml = _liquid != null ? _liquid.currentLiquidVolume : 0f;

        if (Mishandling.IsSpilling(tilt, held, ml, tiltThreshold))
        {
            if (_armed)
            {
                _armed = false;
                if (_runner != null)
                    _runner.RecordMistake(LabErrorType.SpilledReagent, _label + " is spilling — stand bottles upright");
                // The spilled liquid pools on the ground, lingers, then fades.
                if (Application.isPlaying)
                    SpillPuddle.Spawn(transform.position, LiquidColorOf(_liquid));
            }
        }
        else if (tilt < rearmTilt)
        {
            _armed = true;
        }
    }

    /// The current liquid's tint (fallback: watery blue).
    public static Color LiquidColorOf(LiquidPhysics liquid)
    {
        if (liquid != null && liquid.currentChemical != null) return liquid.currentChemical.liquidColor;
        return new Color(0.55f, 0.7f, 0.85f);
    }
}
