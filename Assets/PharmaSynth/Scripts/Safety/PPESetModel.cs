using System.Collections.Generic;

/// The three personal-protective-equipment pieces (goggles + gloves
/// are required alongside the coat before experimenting).
public enum PPEPiece { Coat, Goggles, Gloves }

/// Pure PPE-worn state (no Unity types — edit-mode testable). The PPEController
/// MonoBehaviour drives visuals/audio from this.
public class PPESetModel
{
    private readonly HashSet<PPEPiece> _worn = new HashSet<PPEPiece>();

    public bool IsWorn(PPEPiece p) => _worn.Contains(p);
    public bool AllWorn => _worn.Count == 3;
    public int WornCount => _worn.Count;

    /// Returns true if the piece was newly donned (false when already worn).
    public bool Don(PPEPiece p) => _worn.Add(p);

    /// Take everything off. Returns true if anything was worn.
    public bool Clear()
    {
        if (_worn.Count == 0) return false;
        _worn.Clear();
        return true;
    }

    /// Human sentence fragment of what is still missing ("goggles and gloves"),
    /// empty when fully dressed. Used by Pharmee's gate prompt.
    public string MissingSummary()
    {
        var missing = new List<string>();
        if (!IsWorn(PPEPiece.Coat)) missing.Add("lab coat");
        if (!IsWorn(PPEPiece.Goggles)) missing.Add("goggles");
        if (!IsWorn(PPEPiece.Gloves)) missing.Add("gloves");
        if (missing.Count == 0) return "";
        if (missing.Count == 1) return missing[0];
        if (missing.Count == 2) return missing[0] + " and " + missing[1];
        return missing[0] + ", " + missing[1] + " and " + missing[2];
    }
}
