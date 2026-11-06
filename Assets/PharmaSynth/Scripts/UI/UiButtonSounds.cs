using UnityEngine;
using UnityEngine.EventSystems;

/// Adds audio feedback to a UI button: a soft blip when the pointer/ray hovers it and
/// a click when it's pressed. Works with the XR ray (XRUIInputModule
/// dispatches pointer-enter/click to these handlers) and the desktop mouse. Attach to
/// any Button's GameObject — no per-button wiring, no new clips.
public class UiButtonSounds : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private string hoverKey = "hover";
    [SerializeField] private string clickKey = "ui-click";

    public void SetKeys(string hover, string click) { hoverKey = hover; clickKey = click; }

    public void OnPointerEnter(PointerEventData _)
    {
        if (IsInteractable()) AudioService.TryPlay(hoverKey);
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (IsInteractable()) AudioService.TryPlay(clickKey);
    }

    private bool IsInteractable()
    {
        var sel = GetComponent<UnityEngine.UI.Selectable>();
        return sel == null || sel.interactable;             // don't chirp on disabled buttons
    }
}
