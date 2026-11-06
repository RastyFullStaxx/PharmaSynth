using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// Forwards an XR select on a locker PPE item (coat display / goggles / gloves) to
/// `PPEController.Don(piece)` — each piece is donned individually.
/// Attach next to an XRSimpleInteractable; `PPEWearablesBuilder` wires these.
public class PPEDonOnSelect : MonoBehaviour
{
    [SerializeField] private PPEController controller;
    [SerializeField] private PPEPiece piece;

    private XRBaseInteractable _hooked;

    public void Bind(PPEController c, PPEPiece p) { controller = c; piece = p; }

    private void OnEnable()
    {
        _hooked = GetComponent<XRBaseInteractable>();
        if (_hooked != null) _hooked.selectEntered.AddListener(OnSelect);
    }

    private void OnDisable()
    {
        if (_hooked != null) _hooked.selectEntered.RemoveListener(OnSelect);
    }

    private void OnSelect(SelectEnterEventArgs _)
    {
        if (controller != null) controller.Don(piece);
        // The piece is now on the player (worn visual shows on the avatar) — clear its
        // locker display so it doesn't read as a duplicate. WearableReseat puts it back
        // on reset / next CoatPrompt. Deferred a frame so we don't disable the
        // interactable mid-select-event.
        if (isActiveAndEnabled) StartCoroutine(HideNextFrame());
    }

    private System.Collections.IEnumerator HideNextFrame()
    {
        yield return null;
        gameObject.SetActive(false);
    }
}
