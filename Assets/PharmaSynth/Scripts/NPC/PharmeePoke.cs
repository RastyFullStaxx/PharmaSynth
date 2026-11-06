using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Makes Pharmee interactable: poking/selecting the robot repeats the current
/// step's guidance (PharmeeBrain.InstructCurrent). Debounced so rapid pokes
/// don't spam the narration.
[RequireComponent(typeof(XRSimpleInteractable))]
public class PharmeePoke : MonoBehaviour
{
    [SerializeField] private PharmeeBrain brain;
    [SerializeField] private float debounceSeconds = 1.5f;

    private XRSimpleInteractable _interactable;
    private float _lastPoke = -999f;

    private void OnEnable()
    {
        if (brain == null) brain = GetComponentInParent<PharmeeBrain>();
        _interactable = GetComponent<XRSimpleInteractable>();
        if (_interactable != null) _interactable.selectEntered.AddListener(OnSelected);
    }

    private void OnDisable()
    {
        if (_interactable != null) _interactable.selectEntered.RemoveListener(OnSelected);
    }

    private void OnSelected(SelectEnterEventArgs _) => Poke();

    /// Public so UI buttons / tests can trigger it too.
    public void Poke()
    {
        if (brain == null) return;
        if (WristWatchController.SuppressNpcPokes) return;   // wrist-flip, not a poke
        if (Time.time - _lastPoke < debounceSeconds) return;
        _lastPoke = Time.time;
        brain.InstructCurrent();
    }
}
