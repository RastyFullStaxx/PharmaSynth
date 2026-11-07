using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Reusable world-space option dialog (door gate, confirms): a title plus up to N
/// option buttons. Buttons carry persistent int-arg listeners → OnOption(i); code
/// listens on OptionChosen. All refs optional so it is edit-mode testable.
public class ChoicePanelController : MonoBehaviour
{
    [SerializeField] private GameObject root;            // toggled panel
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button[] optionButtons = new Button[0];
    [SerializeField] private TMP_Text[] optionLabels = new TMP_Text[0];

    public event Action<int> OptionChosen;

    public bool IsOpen => root != null && root.activeSelf;

    /// Edit-mode/test binding.
    public void Bind(GameObject panelRoot, TMP_Text title, Button[] buttons, TMP_Text[] labels)
    {
        root = panelRoot; titleText = title;
        optionButtons = buttons ?? new Button[0];
        optionLabels = labels ?? new TMP_Text[0];
    }

    /// Show the panel with the given options; extras are hidden. `interactable`
    /// null = all enabled (locked rows pass false and render dimmed).
    public void Show(string title, IList<string> options, IList<bool> interactable = null)
    {
        if (titleText != null) titleText.text = GlyphSafe.Sanitize(title ?? "");
        int n = options != null ? options.Count : 0;
        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool used = i < n;
            if (optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(used);
                optionButtons[i].interactable = !used || interactable == null || i >= interactable.Count || interactable[i];
            }
            if (used && i < optionLabels.Length && optionLabels[i] != null)
                optionLabels[i].text = GlyphSafe.Sanitize(options[i]);
        }
        bool wasHidden = root != null && !root.activeSelf;
        if (root != null) root.SetActive(true);
        // The panel appeared in total silence — the quiz tablet got an arrival cue
        // but this one never did, and it is the panel that gates the whole door
        // flow. Only on a genuine show, so re-rendering options stays quiet.
        if (wasHidden) AudioService.TryPlayFirst("ui-confirm", "ui-click");
    }

    /// Single-option message ("Continue" / "Back").
    public void ShowMessage(string title, string confirmLabel)
        => Show(title, new List<string> { confirmLabel });

    public void Hide()
    {
        if (root != null) root.SetActive(false);
    }

    /// Persistent-listener target for the option buttons (int arg = option index).
    public void OnOption(int index) => OptionChosen?.Invoke(index);

    /// Test accessor.
    public string LabelAt(int i)
        => i >= 0 && i < optionLabels.Length && optionLabels[i] != null ? optionLabels[i].text : null;
}
