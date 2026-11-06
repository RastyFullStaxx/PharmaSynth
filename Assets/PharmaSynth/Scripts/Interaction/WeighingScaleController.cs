using TMPro;
using UnityEngine;
using UnityEngine.Events;

/// The balance's digital head. Drives the grams readout from a target mass, the
/// way a bench balance does: the reading TYPES IN, wobbles while the load
/// settles, then locks (like a real digital
/// scale). All display maths is pure so the
/// suite pins it without a scene.
public class WeighingScaleController : MonoBehaviour
{
    [SerializeField] private TMP_Text measurementText;
    [SerializeField] private string unit = "g";
    [SerializeField] private float interpolationSpeed = 20f;
    [SerializeField] private float targetReachedEpsilon = 0.05f;

    [SerializeField] private float currentMass;
    [SerializeField] private float targetMass;

    [Header("Digital readout (2026-07-27)")]
    [Tooltip("Characters revealed per second when a new reading types in.")]
    [SerializeField] private float typeSpeed = 26f;
    [Tooltip("Grams of wobble while the load is still settling.")]
    [SerializeField] private float settleWobble = 0.4f;

    public UnityEvent onTargetReached;

    private bool targetEventSent;
    private float _typeT = 999f;      // seconds since the current reading started typing
    private float _phase;

    public float CurrentMass => currentMass;

    /// What the display shows right now (suite/dev visibility).
    public string DisplayText { get; private set; } = "";

    /// Builder seam: wire the display TMP at runtime.
    public void Bind(TMP_Text display) { measurementText = display; }

    private void Update() => Tick(Time.deltaTime);

    /// One interpolation step (public so the suite can drive it deterministically).
    public void Tick(float dt)
    {
        dt = Mathf.Max(0f, dt);
        _phase += dt;
        _typeT += dt;
        currentMass = Mathf.MoveTowards(currentMass, targetMass, interpolationSpeed * dt);
        UpdateDisplay();

        if (!targetEventSent && Mathf.Abs(currentMass - targetMass) <= targetReachedEpsilon)
        {
            targetEventSent = true;
            onTargetReached?.Invoke();
        }
    }

    public void SetTargetMass(float newTarget)
    {
        newTarget = Mathf.Max(0f, newTarget);
        // Only a REAL change restarts the type-in; a jittering trigger volume must
        // not re-type the same number every frame.
        if (Mathf.Abs(newTarget - targetMass) > 0.005f) { _typeT = 0f; targetEventSent = false; }
        targetMass = newTarget;
    }

    public void AddMass(float delta) => SetTargetMass(targetMass + delta);
    public void RemoveMass(float delta) => SetTargetMass(targetMass - delta);

    /// Pure (suite): the settling reading. While the pan is still converging the
    /// last digit hunts around the true value — deterministic (a sine of the clock,
    /// never Random) so the suite can pin it — and locks exactly on arrival.
    public static float Reading(float shown, float target, float wobbleG, float phase)
    {
        if (Mathf.Abs(shown - target) <= 0.02f) return target;
        return Mathf.Max(0f, shown + Mathf.Sin(phase * 41f) * Mathf.Abs(wobbleG));
    }

    /// Pure (suite): a digital head reveals a new reading left-to-right. `t` is
    /// seconds since the reading changed; past the end the whole string shows.
    public static string Typed(string text, float t, float charsPerSecond)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (charsPerSecond <= 0f) return text;
        int n = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, t) * charsPerSecond), 0, text.Length);
        return text.Substring(0, n);
    }

    /// Pure (suite): the full formatted reading, e.g. "12.40 g".
    public static string Format(float grams, string unit) => grams.ToString("0.00") + " " + unit;

    private void UpdateDisplay()
    {
        string full = Format(Reading(currentMass, targetMass, settleWobble, _phase), unit);
        DisplayText = Typed(full, _typeT, typeSpeed);
        if (measurementText != null) measurementText.text = DisplayText;
    }
}
