using TMPro;
using UnityEngine;

/// A live world-space readout floating over a vessel — an in-game instrument panel
/// (text that reflects the temperature; other experiments
/// that require heating use it for monitoring). Shows
/// "Hard-glass tube / 62 C -> 120 C" while heating, or "Gas collection tube /
/// Collecting 45%" while a gas fills, and tints from cool blue to hot orange.
///
/// REUSABLE: attach to ANY heated vessel in ANY experiment and BindHeat() it to
/// that vessel's TemperatureSim. It reuses the suite-pinned VesselStatusMath
/// formats, so it reads identically to the station billboards.
///
/// NOTE: this does NOT replace the thermometer apparatus — the thermometer stays
/// on the bench for the experiments that call for it (client rule: all tools are
/// always present). This is an extra monitoring aid, not a substitute.
public class ProcessReadout : MonoBehaviour
{
    private string _base = "";
    private TemperatureSim _temp;
    private float _targetC = 100f;
    private float _ambientC = 22f;
    private GasCollection _gas;

    private GameObject _tag;
    private TextMeshPro _tmp;
    private Transform _cam;
    private Renderer[] _rends;
    private float _nextAt;
    private string _last;

    /// Show a rising temperature for this vessel (heating experiments).
    public void BindHeat(string baseLabel, TemperatureSim temp, float targetC, float ambientC = 22f)
    { _base = baseLabel; _temp = temp; _targetC = targetC; _ambientC = ambientC; _gas = null; }

    /// Show gas-collection progress for this vessel.
    public void BindCollect(string baseLabel, GasCollection gas)
    { _base = baseLabel; _gas = gas; _temp = null; }

    /// Pure (suite): only worth showing while something is actually happening —
    /// a vessel above ambient, or a gas actively collecting.
    public static bool ShouldShow(bool hasHeat, float currentC, float ambientC, bool hasGas, float fill01)
        => (hasHeat && currentC > ambientC + 2f) || (hasGas && fill01 > 0.001f);

    /// The line this readout is currently showing (pure given bound sims).
    public string Compose()
    {
        if (_temp != null) return VesselStatusMath.HeatLine(_base, _temp.CurrentC, _targetC);
        if (_gas != null) return VesselStatusMath.ProgressLine(_base, "Collecting", _gas.FillFraction);
        return _base;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextAt) return;
        _nextAt = Time.unscaledTime + 0.15f;

        bool show = ShouldShow(_temp != null, _temp != null ? _temp.CurrentC : 0f, _ambientC,
                               _gas != null, _gas != null ? _gas.FillFraction : 0f);
        EnsureTag();
        if (_tag == null) return;
        if (_tag.activeSelf != show) _tag.SetActive(show);
        if (!show) return;

        string s = Compose();
        if (s != _last) { _last = s; _tmp.text = GlyphSafe.Sanitize(s); }
        _tmp.color = Tint();
        Place();
    }

    /// Cool blue → hot orange as the vessel approaches its target.
    private Color Tint()
    {
        if (_temp == null) return new Color(0.65f, 0.95f, 1f);
        float t = Mathf.Clamp01(Mathf.InverseLerp(_ambientC, Mathf.Max(_ambientC + 1f, _targetC), _temp.CurrentC));
        return Color.Lerp(new Color(0.65f, 0.9f, 1f), new Color(1f, 0.52f, 0.18f), t);
    }

    private void EnsureTag()
    {
        if (_tag != null) return;
        _rends = GetComponentsInChildren<Renderer>();
        _tag = new GameObject("ProcessReadout");
        _tag.transform.SetParent(transform, false);
        var ls = transform.lossyScale;
        _tag.transform.localScale = new Vector3(1f / Mathf.Max(Mathf.Abs(ls.x), 1e-4f),
                                                1f / Mathf.Max(Mathf.Abs(ls.y), 1e-4f),
                                                1f / Mathf.Max(Mathf.Abs(ls.z), 1e-4f)) * 0.02f;
        _tmp = _tag.AddComponent<TextMeshPro>();
        _tmp.fontSize = 5f;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.fontStyle = FontStyles.Bold;
        _tmp.outlineWidth = 0.25f;
        _tmp.outlineColor = new Color32(6, 12, 22, 255);
        var mr = _tag.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 32759;   // just under the name tag
        }
        _tag.SetActive(false);
    }

    /// Float above the vessel, billboarded, clear of the name tag.
    private void Place()
    {
        if (_cam == null) { var c = Camera.main; if (c == null) return; _cam = c.transform; }
        float top = transform.position.y + 0.22f;
        if (_rends != null && _rends.Length > 0)
        {
            Bounds b = default; bool has = false;
            foreach (var r in _rends)
            {
                if (r == null || r.GetComponent<TMP_Text>() != null) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
            if (has) top = b.max.y + 0.24f;   // above the ProximityLabel name tag
        }
        Vector3 toCam = _cam.position - transform.position; toCam.y = 0f;
        Vector3 fwd = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.forward;
        _tag.transform.position = new Vector3(transform.position.x, top, transform.position.z) + fwd * 0.08f;
        _tag.transform.rotation = Quaternion.LookRotation(_tag.transform.position - _cam.position, Vector3.up);
    }
}
