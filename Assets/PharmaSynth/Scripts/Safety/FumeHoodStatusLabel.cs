using UnityEngine;

/// Narrating label for the fume hood: the hood is an OPEN alcove — nothing to open or
/// switch on; protection is purely WHERE the vessel is. The invisible
/// WorkVolume made "am I in far enough?" a guess, so the label says what
/// belongs inside and flips to a ✓ while a vessel is actually protected.
public class FumeHoodStatusLabel : MonoBehaviour
{
    private FumeHoodZone _zone;
    private ProximityLabel _label;
    private float _nextScan;

    public void Bind(FumeHoodZone zone, ProximityLabel label) { _zone = zone; _label = label; }

    /// Pure (suite-pinned): idle guidance vs the in-hood assurance cue.
    public static string StatusLine(string vesselInside)
        => string.IsNullOrEmpty(vesselInside)
            ? "Fume Hood — do aniline & acetyl chloride work IN here"
            : "Fume Hood — " + vesselInside + " protected ✓";

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 0.25f;
        if (_zone == null) _zone = GetComponent<FumeHoodZone>() ?? GetComponentInParent<FumeHoodZone>();
        if (_label == null) _label = GetComponent<ProximityLabel>() ?? gameObject.AddComponent<ProximityLabel>();
        if (_zone == null || _label == null) return;
        string inside = null;
        foreach (var lp in FindObjectsByType<LiquidPhysics>(FindObjectsSortMode.None))
            if (lp != null && _zone.Contains(lp.transform.position))
            { inside = Mishandling.DisplayNameFor(lp.gameObject); break; }
        _label.SetLabel(StatusLine(inside), 2.2f);
    }
}
