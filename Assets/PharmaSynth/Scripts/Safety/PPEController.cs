using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// PPE donning at the locker. The three pieces — lab coat, goggles,
/// gloves — are donned INDIVIDUALLY (click each locker item; `PPEDonOnSelect` forwards
/// the clicks here) and ALL THREE are required before an experiment can start
/// (`PPEWorn` == all worn — the gate checks it). Per-piece visuals: coat + goggles +
/// gloves appear on the mirror avatar (PlayerAvatar layer — mirror-only), and the
/// gloves ALSO appear first-person on the controllers. `PPEWearablesBuilder` wires
/// the visuals; `RemovePPE` (HUD Reset) strips everything.
public class PPEController : MonoBehaviour
{
    [SerializeField] private GameObject[] labEntryBlockers;  // e.g. a locked-door collider
    [SerializeField] private GameObject[] wornVisuals;       // legacy: shown when FULLY dressed
    [SerializeField] private GameObject[] coatVisuals;       // mirror-avatar coat
    [SerializeField] private GameObject[] gogglesVisuals;    // mirror-avatar goggles
    [SerializeField] private GameObject[] glovesVisuals;     // mirror-avatar + first-person gloves
    [SerializeField] private bool donOnSelect = true;        // host click = don everything (legacy/dev)

    public UnityEvent onPPEWorn;                              // fires when FULLY dressed
    public event Action PPEWornChanged;                       // fires on every piece change

    public PPESetModel Set { get; } = new PPESetModel();

    /// Fully dressed (all three pieces) — the experiment gate checks this.
    public bool PPEWorn => Set.AllWorn;

    public bool IsWorn(PPEPiece p) => Set.IsWorn(p);
    public string MissingSummary() => Set.MissingSummary();

    private XRBaseInteractable _hooked;

    private void OnEnable()
    {
        if (!donOnSelect) return;
        _hooked = GetComponent<XRBaseInteractable>();
        if (_hooked != null) _hooked.selectEntered.AddListener(OnSelect);
    }

    private void OnDisable()
    {
        if (_hooked != null) _hooked.selectEntered.RemoveListener(OnSelect);
    }

    private void OnSelect(SelectEnterEventArgs _) => DonPPE();

    /// Don one piece (locker item click → PPEDonOnSelect → here). Idempotent.
    public void Don(PPEPiece piece)
    {
        if (!Set.Don(piece)) return;
        ApplyVisuals(piece, true);
        AudioService.TryPlay("ppe-rustle");
        if (Set.AllWorn)
        {
            if (labEntryBlockers != null)
                foreach (var b in labEntryBlockers) if (b != null) b.SetActive(false);
            if (wornVisuals != null)
                foreach (var v in wornVisuals) if (v != null) v.SetActive(true);
            onPPEWorn?.Invoke();
        }
        PPEWornChanged?.Invoke();
    }

    // UnityEvent-friendly per-piece entry points.
    public void DonCoat() => Don(PPEPiece.Coat);
    public void DonGoggles() => Don(PPEPiece.Goggles);
    public void DonGloves() => Don(PPEPiece.Gloves);

    /// Don EVERYTHING at once (legacy host click / dev bypass). Idempotent.
    public void DonPPE()
    {
        Don(PPEPiece.Coat);
        Don(PPEPiece.Goggles);
        Don(PPEPiece.Gloves);
    }

    /// Take everything off (HUD Reset / return-to-entrance).
    public void RemovePPE()
    {
        if (!Set.Clear()) return;
        ApplyVisuals(PPEPiece.Coat, false);
        ApplyVisuals(PPEPiece.Goggles, false);
        ApplyVisuals(PPEPiece.Gloves, false);
        if (wornVisuals != null)
            foreach (var v in wornVisuals) if (v != null) v.SetActive(false);
        AudioService.TryPlay("ppe-rustle");   // donning rustled; removing was mute
        PPEWornChanged?.Invoke();
    }

    private void ApplyVisuals(PPEPiece piece, bool on)
    {
        var arr = piece == PPEPiece.Coat ? coatVisuals
                : piece == PPEPiece.Goggles ? gogglesVisuals : glovesVisuals;
        if (arr == null) return;
        foreach (var v in arr) if (v != null) v.SetActive(on);
    }

    /// Editor-builder seam: assign the per-piece worn visuals.
    public void BindVisuals(GameObject[] coat, GameObject[] goggles, GameObject[] gloves)
    {
        coatVisuals = coat; gogglesVisuals = goggles; glovesVisuals = gloves;
    }
}
