using UnityEngine;

/// Pure hand-vs-glove display policy (edit-mode testable). The bare white hand
/// shows whenever the first-person PPE glove is NOT worn; donning gloves swaps
/// the glove in (never both at once).
public static class HandSwap
{
    public static bool ShowBareHand(bool gloveVisible) => !gloveVisible;
}

/// Runtime keeper on each controller. Every frame it:
///  - hides the default XRI controller MODEL (the hand replaces it),
///  - keeps the bare hand visible (other systems were toggling it off),
///  - swaps bare hand ↔ FPGlove when PPE gloves are donned/removed.
/// Wired by Tools ▸ PharmaSynth ▸ Build Hand Visuals.
public class HandVisualKeeper : MonoBehaviour
{
    [SerializeField] private GameObject handVisual;        // HandVisual_L/R (bare white hand)
    [SerializeField] private GameObject controllerVisual;  // Left/Right Controller Visual (hidden)
    [SerializeField] private GameObject fpGlove;           // FPGlove_L/R (shown when PPE worn)

    public void Bind(GameObject hand, GameObject controllerModel, GameObject glove)
    { handVisual = hand; controllerVisual = controllerModel; fpGlove = glove; }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;

        if (controllerVisual != null && controllerVisual.activeSelf)
            controllerVisual.SetActive(false);

        bool gloveVisible = fpGlove != null && fpGlove.activeSelf;
        bool wantBare = HandSwap.ShowBareHand(gloveVisible);
        if (handVisual != null && handVisual.activeSelf != wantBare)
            handVisual.SetActive(wantBare);
    }
}
