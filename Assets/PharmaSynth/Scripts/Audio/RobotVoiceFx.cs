using UnityEngine;

/// Pharmee is a FEMALE ROBOT. Text-to-speech gives a natural human read, so the
/// machine character is applied as a FILTER on her voice channel — never baked
/// into the 182 clips, so retuning stays free.
///
/// ⭐ THE CHOSEN CHARACTER is LOW-FREQUENCY RING MODULATION blended with dry
/// signal — like "speaking against an electric fan", settled on
/// after auditioning everything else. `ringMix` is the fan's strength.
///
/// This file also contains a vocoder, sample-and-hold decimation and a comb resonator,
/// all currently SWITCHED OFF in the profile. Each was auditioned and rejected; they
/// are kept so nobody rebuilds them:
///   • Heavy band-limiting -> "muffled". Fixed by opening the low-pass to 11 kHz
///     and adding the presence lift; those two ARE still in use.
///   • Decimation + comb   -> technically more machine-like, but the verdict was
///     "sounds so human" — they recolour timbre without touching pitch contour.
///   • Vocoder             -> the textbook robot technique (rebuilds the words on
///     a synthetic monotone carrier, discarding the vocal cords entirely) and the
///     verdict was "so much worse".
/// So: the strongest-on-paper techniques lost to the simplest one. Do not swap the
/// character back without asking — every alternative here has already been heard
/// and rejected.
///
/// Everything stays blended enough to keep the words legible: this is a teaching
/// game and Pharmee explains procedure.
[RequireComponent(typeof(AudioSource))]
public class RobotVoiceFx : MonoBehaviour
{
    [Header("Shared tuning")]
    [Tooltip("The ONE profile every Pharmee channel reads. Tune it once and every line " +
             "she speaks follows — including lines not generated yet.")]
    public RobotVoiceProfile profile;

    [Header("Fallback (used only when no profile is assigned)")]
    [Range(0f, 1f)] public float ringMix = 0.27f;      // the 'fan' — the chosen character
    [Range(20f, 800f)] public float ringHz = 62f;
    [Range(0f, 2f)] public float presenceBoost = 0.9f;
    [Range(500f, 8000f)] public float presenceHz = 3000f;
    [Range(4f, 16f)] public float crushBits = 10f;
    [Range(0f, 1f)] public float crushMix = 0.35f;
    // Rejected techniques, off by default (see the class summary).
    [Range(0f, 1f)] public float vocoderMix = 0f;
    [Range(60f, 400f)] public float carrierHz = 205f;
    [Range(0f, 0.6f)] public float carrierNoise = 0.18f;
    [Range(6f, 20f)] public float vocoderBands = 14f;
    [Range(1f, 12f)] public float vocoderQ = 4.5f;
    [Range(1f, 8f)] public float downsample = 1f;
    [Range(0.5f, 10f)] public float combMs = 3.2f;
    [Range(0f, 0.95f)] public float combFeedback = 0.72f;
    [Range(0f, 1f)] public float combMix = 0f;
    public bool active = true;

    public float VocoderMix => profile != null ? profile.vocoderMix : vocoderMix;
    public float CarrierHz => profile != null ? profile.carrierHz : carrierHz;
    public float CarrierNoise => profile != null ? profile.carrierNoise : carrierNoise;
    public float VocoderBands => profile != null ? profile.vocoderBands : vocoderBands;
    public float VocoderQ => profile != null ? profile.vocoderQ : vocoderQ;
    public float Downsample => profile != null ? profile.downsample : downsample;
    public float CombMs => profile != null ? profile.combMs : combMs;
    public float CombFeedback => profile != null ? profile.combFeedback : combFeedback;
    public float CombMix => profile != null ? profile.combMix : combMix;
    public float CrushBits => profile != null ? profile.crushBits : crushBits;
    public float CrushMix => profile != null ? profile.crushMix : crushMix;
    public float RingHz => profile != null ? profile.ringHz : ringHz;
    public float RingMix => profile != null ? profile.ringMix : ringMix;
    public float PresenceBoost => profile != null ? profile.presenceBoost : presenceBoost;
    public float PresenceHz => profile != null ? profile.presenceHz : presenceHz;
    public bool Active => profile != null ? profile.active : active;

    private const int MaxChannels = 8;
    private const int CombSize = 1024;          // 21 ms at 48 kHz — ample

    private double _phase;
    private double _carrierPhase;
    private uint _noiseState = 0x9E3779B9u;
    private int _sampleRate = 48000;
    private readonly float[] _lowBand = new float[MaxChannels];
    private readonly float[] _hold = new float[MaxChannels];
    private float[] _comb;                      // MaxChannels * CombSize, allocated off-thread
    private int _combPos;
    private int _holdCount;

    private RobotVocoder _vocoder;
    private float _cfgBands, _cfgQ;             // last configuration, to avoid rebuilding per buffer

    private void Awake()
    {
        _sampleRate = AudioSettings.outputSampleRate;
        // Allocated HERE, never in OnAudioFilterRead — that runs on the audio
        // thread and an allocation there causes dropouts.
        if (_comb == null) _comb = new float[MaxChannels * CombSize];
        if (_vocoder == null) _vocoder = new RobotVocoder();
        ConfigureVocoder();
    }

    private void OnEnable()
    {
        _phase = 0d; _carrierPhase = 0d; _combPos = 0; _holdCount = 0;
        _vocoder?.Reset();
    }

    /// Rebuild the filter bank on the MAIN thread whenever the band count or Q
    /// changes — never from OnAudioFilterRead.
    private void ConfigureVocoder()
    {
        if (_vocoder == null) return;
        _cfgBands = VocoderBands; _cfgQ = VocoderQ;
        _vocoder.Configure(Mathf.RoundToInt(_cfgBands), MaxChannels, _sampleRate,
                           150f, 6500f, _cfgQ, 12f);
    }

    private void Update()
    {
        // Live tuning: the profile can be edited during Play, so pick up band/Q
        // changes here rather than on the audio thread.
        if (_vocoder != null && (!Mathf.Approximately(_cfgBands, VocoderBands)
                                 || !Mathf.Approximately(_cfgQ, VocoderQ)))
            ConfigureVocoder();
    }

    /// Pure (suite-pinned): one sample of the synthetic excitation. A sawtooth is
    /// harmonically dense, which is what a vocoder needs — the filter bank can only
    /// shape harmonics that are already present, so a sine carrier would vocode to
    /// near-silence. Noise fills in the unvoiced consonants.
    public static float Carrier(float sawPhase01, float noise, float noiseMix)
    {
        noiseMix = Mathf.Clamp01(noiseMix);
        float saw = sawPhase01 * 2f - 1f;             // -1..1 ramp
        return saw * (1f - noiseMix) + noise * noiseMix;
    }

    // ---- pure, suite-pinned pieces ------------------------------------------

    /// One ring-modulated sample, blended with dry so speech stays readable.
    public static float RingSample(float dry, float carrier, float mix)
    {
        mix = Mathf.Clamp01(mix);
        return dry * (1f - mix) + dry * carrier * mix;
    }

    /// Quantise to `bits` and blend. Grit that brightens rather than dulls.
    public static float Crush(float x, float bits, float mix)
    {
        mix = Mathf.Clamp01(mix);
        if (mix <= 0f) return x;
        float levels = Mathf.Pow(2f, Mathf.Clamp(bits, 2f, 16f));
        float step = 2f / levels;
        float q = Mathf.Round(x / step) * step;
        return x * (1f - mix) + q * mix;
    }

    /// Smoothing coefficient for a one-pole filter at `hz`.
    public static float OnePoleAlpha(float hz, int sampleRate)
    {
        if (sampleRate <= 0) return 0.5f;
        return Mathf.Clamp01(1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(1f, hz) / sampleRate));
    }

    /// Comb delay in SAMPLES for a given length in ms, clamped to the buffer.
    /// A short comb (2-4 ms) rings in the low hundreds of Hz — the hollow,
    /// metallic "speaker inside a machine" timbre.
    public static int CombDelaySamples(float ms, int sampleRate, int maxSamples)
        => Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(0.1f, ms) * 0.001f * Mathf.Max(1, sampleRate)),
                       1, Mathf.Max(1, maxSamples - 1));

    /// Feedback must stay below 1 or the comb self-oscillates into a howl.
    public static float SafeFeedback(float fb) => Mathf.Clamp(fb, 0f, 0.9f);

    /// How many output frames each input sample is HELD for. 1 = no decimation.
    public static int HoldFrames(float factor) => Mathf.Clamp(Mathf.RoundToInt(factor), 1, 16);

    // ---- audio thread --------------------------------------------------------

    private void OnAudioFilterRead(float[] data, int channels)
    {
        if (!Active || _comb == null) return;
        if (_sampleRate <= 0) _sampleRate = 48000;
        int ch = Mathf.Min(channels, MaxChannels);

        // The vocoder is the expensive stage (bands x 2 biquads per sample). Skip
        // the whole chain when nothing is actually being spoken — this filter is
        // called continuously whether or not the source is playing.
        bool silent = true;
        for (int s = 0; s < data.Length; s++)
            if (data[s] > 0.0002f || data[s] < -0.0002f) { silent = false; break; }
        if (silent) return;

        float boost = PresenceBoost, cMix = CrushMix, cBits = CrushBits;
        float rMix = RingMix, kMix = CombMix, fb = SafeFeedback(CombFeedback);
        float vMix = Mathf.Clamp01(VocoderMix), nMix = CarrierNoise;
        int hold = HoldFrames(Downsample);
        int delay = CombDelaySamples(CombMs, _sampleRate, CombSize);
        float alpha = OnePoleAlpha(PresenceHz, _sampleRate);
        double step = 2d * System.Math.PI * RingHz / _sampleRate;
        double carrierStep = CarrierHz / _sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            float carrier = (float)System.Math.Sin(_phase);
            _phase += step;
            if (_phase > 2d * System.Math.PI) _phase -= 2d * System.Math.PI;

            // Synthetic excitation for the vocoder: a monotone sawtooth plus noise.
            _carrierPhase += carrierStep;
            if (_carrierPhase >= 1d) _carrierPhase -= 1d;
            // xorshift — deterministic, allocation-free, audio-thread safe.
            _noiseState ^= _noiseState << 13; _noiseState ^= _noiseState >> 17; _noiseState ^= _noiseState << 5;
            float noise = (_noiseState & 0xFFFFFF) / 8388607.5f - 1f;
            float exc = Carrier((float)_carrierPhase, noise, nMix);

            bool capture = (_holdCount % hold) == 0;   // sample-and-hold boundary
            _holdCount++;

            for (int c = 0; c < ch; c++)
            {
                float x = data[i + c];

                // 0. VOCODE. The human excitation is replaced by the synthetic one,
                //    keeping only the per-band energy envelopes that carry the words.
                //    This is the stage that stops it sounding like a person.
                if (vMix > 0.001f && _vocoder != null)
                {
                    float v = _vocoder.Process(c, x, exc);
                    x = x * (1f - vMix) + v * vMix;
                }

                // 1. Presence, to keep consonants legible through what follows.
                _lowBand[c] += alpha * (x - _lowBand[c]);
                x += (x - _lowBand[c]) * boost;

                // 2. DECIMATE. Reconstructing the voice from held samples is the
                //    strongest "a computer is producing this" cue, and unlike ring
                //    mod it changes the voice itself rather than adding a layer.
                if (capture) _hold[c] = x;
                x = _hold[c];

                // 3. COMB. Short feedback delay = hollow metallic resonance. This
                //    is what makes it read as a machine SPEAKING rather than a
                //    person with an effect over them.
                int bufBase = c * CombSize;
                int readIdx = _combPos - delay;
                if (readIdx < 0) readIdx += CombSize;
                float combed = x + _comb[bufBase + readIdx] * fb;
                if (combed < -4f) combed = -4f; else if (combed > 4f) combed = 4f;  // runaway guard
                _comb[bufBase + _combPos] = combed;
                x = x * (1f - kMix) + combed * kMix;

                // 4. Digital grit, then a seasoning of inharmonic edge.
                x = Crush(x, cBits, cMix);
                x = RingSample(x, carrier, rMix);

                data[i + c] = x < -1f ? -1f : (x > 1f ? 1f : x);
            }

            _combPos++;
            if (_combPos >= CombSize) _combPos = 0;
        }
    }
}
