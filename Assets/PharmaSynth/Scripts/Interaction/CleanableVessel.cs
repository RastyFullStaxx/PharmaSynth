using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for glassware cleanliness (scrub a test tube and
/// a text shows the dirtiness dropping with each swipe until the
/// test tube is clean). Educational only — never graded.
public static class CleanupMath
{
    public const float DirtyOnEmpty = 100f;      // residue after holding chemicals
    public const float SwipeDistance = 0.12f;    // brush travel that counts as one swipe
    public const float DirtPerSwipe = 20f;       // five good swipes = clean
    public const float RinsePerMl = 1.2f;        // wash-bottle water also cleans

    /// A vessel that HELD something and is now empty carries residue.
    public static bool BecomesDirty(float previousMl, bool nowEmpty)
        => nowEmpty && previousMl > 0.5f;

    public static float AfterSwipe(float dirtiness) => Mathf.Max(0f, dirtiness - DirtPerSwipe);

    public static float AfterRinse(float dirtiness, float mlAdded)
        => Mathf.Max(0f, dirtiness - Mathf.Max(0f, mlAdded) * RinsePerMl);

    /// Name prefix for the live label: "Dirty Test Tube" → scrub → "Clean Test Tube".
    public static string NamePrefix(float dirtiness, bool everDirty)
        => dirtiness > 0.5f ? "Dirty " : (everDirty ? "Clean " : "");
}

/// Residue state on a vessel: emptying it after use makes it DIRTY (label
/// prefix via VesselStatus); the test-tube brush scrubs it clean swipe by
/// swipe, and wash-bottle water rinses it. Added by the kits builder and the
/// stage builder next to LiquidPhysics.
public class CleanableVessel : MonoBehaviour
{
    private LiquidPhysics _lp;
    private float _lastMl;
    private bool _everDirty;

    public float Dirtiness { get; private set; }
    public bool EverDirty => _everDirty;

    public void Bind(LiquidPhysics lp) { _lp = lp; _lastMl = CurrentMl(); }

    void Awake() { if (_lp == null) Bind(GetComponent<LiquidPhysics>()); }

    float CurrentMl() => _lp != null ? _lp.currentLiquidVolume + _lp.currentPptVolume : 0f;

    void Update()
    {
        if (_lp == null) return;
        float ml = CurrentMl();
        if (CleanupMath.BecomesDirty(_lastMl, ml < 0.01f) && Dirtiness <= 0f)
        {
            Dirtiness = CleanupMath.DirtyOnEmpty;
            _everDirty = true;
        }
        // Wash-bottle rinse: water poured in while dirty scrubs residue too.
        if (Dirtiness > 0f && ml > _lastMl && _lp.currentChemical != null
            && _lp.currentChemical.chemicalName != null
            && _lp.currentChemical.chemicalName.Contains("Water"))
        {
            Dirtiness = CleanupMath.AfterRinse(Dirtiness, ml - _lastMl);
            if (Dirtiness <= 0f) Announce("Rinsed clean!");
        }
        _lastMl = ml;
    }

    /// Back to a fresh vessel: the lab reset calls this so residue never carries
    /// from one attempt into the next. Re-samples the volume so the next Update does not
    /// mistake the reset for an emptying and stamp the vessel dirty again.
    /// Test/builder seam: exactly what Update does the moment a used vessel runs empty.
    public void Soil()
    {
        Dirtiness = CleanupMath.DirtyOnEmpty;
        _everDirty = true;
    }

    public void ResetResidue()
    {
        Dirtiness = 0f;
        _everDirty = false;
        _lastMl = CurrentMl();
    }

    /// One brush swipe (BrushController calls this). Returns the new dirtiness.
    public float Scrub()
    {
        if (Dirtiness <= 0f) return 0f;
        Dirtiness = CleanupMath.AfterSwipe(Dirtiness);
        Announce(Dirtiness > 0f ? "Dirtiness " + Mathf.RoundToInt(Dirtiness) + "%" : "Sparkling clean!");
        return Dirtiness;
    }

    /// "Dirty " / "Clean " prefix for the live vessel label.
    public string NamePrefix() => CleanupMath.NamePrefix(Dirtiness, _everDirty);

    void Announce(string text)
    {
        FloatingText.Show(text, transform.position + Vector3.up * 0.12f,
            Dirtiness > 0f ? new Color(0.95f, 0.8f, 0.5f) : new Color(0.6f, 1f, 0.7f), 0.85f);
    }
}
