using UnityEngine;

/// Makes the lab speaker cabinet's power LED blink like a real machine's standby
/// light: a crisp bright pulse with a long dark gap, rather than a flat always-on
/// emissive. Driven through a MaterialPropertyBlock
/// so the shared SpeakerLED material is never instanced (edit-mode safe).
///
/// Thin MonoBehaviour: the envelope is a pure static function; the renderer is bound
/// through Bind() because AddComponent doesn't fire Awake in edit mode.
[RequireComponent(typeof(Renderer))]
public class SpeakerLedBlink : MonoBehaviour
{
    [SerializeField] private Color color = new Color(0.3f, 0.85f, 1f);
    [SerializeField] private float period = 1.15f;      // one full blink cycle, seconds
    [SerializeField] private float onFraction = 0.22f;  // share of the cycle lit
    [SerializeField] private float edgeSeconds = 0.05f; // ramp in/out so it reads as a glow, not a flicker
    [SerializeField] private float dimLevel = 0.12f;    // never fully black — a powered unit still shows
    [SerializeField] private float peakEmission = 6f;   // bright enough to bloom/read across the room
    [SerializeField] private float phase;               // offset so multiple LEDs don't blink in lockstep

    private Renderer _rend;
    private MaterialPropertyBlock _mpb;

    public float CurrentLevel { get; private set; }

    /// Builder seam — Awake/OnEnable don't run on AddComponent in edit mode.
    public void Bind(Color c, float cyclePeriod, float lit, float peak)
    {
        color = c; period = cyclePeriod; onFraction = lit; peakEmission = peak;
        _rend = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
        Apply(Level01(0f, period, onFraction, edgeSeconds, dimLevel));
    }

    /// Blink envelope at time t: dimLevel between pulses, 1 at the top of a pulse,
    /// linearly ramped over edgeSeconds on each side. Pure/tested.
    public static float Level01(float t, float period, float onFraction, float edgeSeconds, float dimLevel)
    {
        if (period <= 0f) return 1f;
        float on = Mathf.Clamp01(onFraction) * period;
        float x = Mathf.Repeat(Mathf.Max(0f, t), period);
        float dim = Mathf.Clamp01(dimLevel);
        if (x >= on) return dim;                                  // the dark gap

        float e = Mathf.Clamp(edgeSeconds, 0f, on * 0.5f);
        float k = 1f;
        if (e > 0f)
        {
            if (x < e) k = x / e;                                 // ramp up
            else if (x > on - e) k = (on - x) / e;                // ramp down
        }
        return Mathf.Lerp(dim, 1f, Mathf.Clamp01(k));
    }

    private void Awake()
    {
        _rend = GetComponent<Renderer>();
        _mpb = new MaterialPropertyBlock();
    }

    private void Update()
    {
        if (_rend == null) return;
        // Unscaled: the LED keeps blinking while the clock is frozen for the quiz/grade.
        CurrentLevel = Level01(Time.unscaledTime + phase, period, onFraction, edgeSeconds, dimLevel);
        Apply(CurrentLevel);
    }

    private void Apply(float level)
    {
        if (_rend == null) return;
        CurrentLevel = level;
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        _rend.GetPropertyBlock(_mpb);
        _mpb.SetColor("_EmissionColor", color * (peakEmission * level));
        _mpb.SetColor("_BaseColor", color * Mathf.Lerp(0.35f, 1f, level));
        _rend.SetPropertyBlock(_mpb);
    }
}
