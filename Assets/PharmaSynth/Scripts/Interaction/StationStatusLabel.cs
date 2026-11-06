using TMPro;
using UnityEngine;

/// Live status on a sim station's billboard label (the temperature
/// shown on heaters etc.): Heat shows "62 C -> 150 C", Filter/Collect/
/// Crystallise show percent progress. Throttled + change-gated; formats live
/// in VesselStatusMath so the suite pins them.
public class StationStatusLabel : MonoBehaviour
{
    private TMP_Text _tmp;
    private string _base;
    private StationSim _kind;
    private TemperatureSim _temp;
    private CrystallizationController _cryst;
    private FiltrationController _filt;
    private GasCollection _gas;
    private float _targetC;
    private float _nextAt;
    private string _last;
    private System.Func<bool> _ignitionLit;   // burner-gated Heat: hint until lit

    /// Burner-gated Heat stations: show a "light the burner" hint while
    /// the gate is cold so nobody stalls wondering why the temperature sits still.
    public void SetIgnitionHint(System.Func<bool> isLit) => _ignitionLit = isLit;

    /// Builder seam.
    public void Bind(TMP_Text tmp, string baseLabel, StationSim kind,
                     TemperatureSim temp, CrystallizationController cryst,
                     FiltrationController filt, GasCollection gas, float targetC)
    {
        _tmp = tmp; _base = baseLabel; _kind = kind;
        _temp = temp; _cryst = cryst; _filt = filt; _gas = gas;
        _targetC = targetC;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAt) return;
        _nextAt = Time.unscaledTime + 0.25f;
        Refresh();
    }

    /// Public for tests + immediate updates.
    public void Refresh()
    {
        if (_tmp == null) return;
        string s = ComposeStatus();
        if (s == _last) return;
        _last = s;
        _tmp.text = GlyphSafe.Sanitize(s);
    }

    /// The current status string (pure given the bound sims' state).
    public string ComposeStatus()
    {
        switch (_kind)
        {
            case StationSim.Heat when _temp != null:
                string line = VesselStatusMath.HeatLine(_base, _temp.CurrentC, _targetC);
                if (_ignitionLit != null && !_ignitionLit()) line += "\nLight the burner (use a match)";
                return line;
            case StationSim.Crystallise when _cryst != null:
                return VesselStatusMath.ProgressLine(_base, "Setting", _cryst.Progress);
            case StationSim.Filter when _filt != null:
                return VesselStatusMath.ProgressLine(_base, "Filtering", _filt.Fraction);
            case StationSim.Collect when _gas != null:
                return VesselStatusMath.ProgressLine(_base, "Collecting", _gas.FillFraction);
            default:
                return _base;
        }
    }
}
