using UnityEngine;

/// A channel vocoder — the technique behind essentially every convincing robot
/// voice (filtering alone left it clear but far too human).
///
/// WHY FILTERING PLATEAUED. Decimation, comb resonance and bit-crush change a
/// voice's TIMBRE, but they leave its excitation intact: the pitch contour, the
/// vibrato, the breath, the micro-timing of real vocal cords. Those are what the
/// ear uses to decide "human", so no amount of colouring gets past "a woman,
/// processed".
///
/// A vocoder attacks the source instead. It splits the voice into N frequency
/// bands and measures only the ENERGY ENVELOPE of each — that envelope is what
/// carries the words, and it contains no pitch information at all. Those
/// envelopes are then imposed on a SYNTHETIC carrier at a fixed pitch. The
/// consonants and vowels survive; the vocal cords are thrown away and replaced
/// by an oscillator. That is the difference between "human with an effect" and
/// "a machine talking".
///
/// Keeping her FEMALE: the carrier pitch is the voice's new pitch, so a carrier
/// in the 180-260 Hz range reads female while staying perfectly monotone — and
/// monotone is a large part of what reads as robotic.
///
/// Unvoiced sounds (s, f, sh, t) contain no pitch, so a pure tonal carrier turns
/// them to mush. The carrier therefore blends in noise, which is what restores
/// intelligibility on sibilants.
public class RobotVocoder
{
    public const int MaxBands = 20;
    public const int MaxChannels = 8;

    private int _bands, _sampleRate, _channels;
    private float _envCoeff;

    // Per-band biquad coefficients (shared by the analysis and synthesis banks —
    // both filter at the same centre frequencies, which is what makes the
    // envelope of band i meaningful when applied to band i of the carrier).
    private readonly float[] _b0 = new float[MaxBands];
    private readonly float[] _b2 = new float[MaxBands];
    private readonly float[] _a1 = new float[MaxBands];
    private readonly float[] _a2 = new float[MaxBands];

    // Direct-Form-I state, [channel * MaxBands + band].
    private readonly float[] _ax1 = new float[MaxChannels * MaxBands];
    private readonly float[] _ax2 = new float[MaxChannels * MaxBands];
    private readonly float[] _ay1 = new float[MaxChannels * MaxBands];
    private readonly float[] _ay2 = new float[MaxChannels * MaxBands];
    private readonly float[] _cx1 = new float[MaxChannels * MaxBands];
    private readonly float[] _cx2 = new float[MaxChannels * MaxBands];
    private readonly float[] _cy1 = new float[MaxChannels * MaxBands];
    private readonly float[] _cy2 = new float[MaxChannels * MaxBands];
    private readonly float[] _env = new float[MaxChannels * MaxBands];

    public int Bands => _bands;

    /// Pure (suite-pinned): centre frequency of band `i`, spaced LOGARITHMICALLY.
    /// Even spacing in Hz would waste most bands above 3 kHz where speech carries
    /// little energy, and starve the 200-1500 Hz region that actually forms vowels.
    public static float BandCenter(int i, int bands, float lowHz, float highHz)
    {
        if (bands <= 1) return lowHz;
        float t = Mathf.Clamp01(i / (float)(bands - 1));
        return lowHz * Mathf.Pow(Mathf.Max(1.0001f, highHz / Mathf.Max(1f, lowHz)), t);
    }

    /// Pure (suite-pinned): envelope-follower smoothing for a given release time.
    /// Too fast and the carrier buzzes on every glottal pulse (human again); too
    /// slow and consonants smear into each other.
    public static float EnvelopeCoeff(float ms, int sampleRate)
    {
        if (sampleRate <= 0) return 0.5f;
        return Mathf.Clamp01(1f - Mathf.Exp(-1f / (Mathf.Max(0.1f, ms) * 0.001f * sampleRate)));
    }

    /// Rebuild the filter bank. Call from the main thread only.
    public void Configure(int bands, int channels, int sampleRate, float lowHz, float highHz, float q, float envMs)
    {
        _bands = Mathf.Clamp(bands, 2, MaxBands);
        _channels = Mathf.Clamp(channels, 1, MaxChannels);
        _sampleRate = Mathf.Max(8000, sampleRate);
        _envCoeff = EnvelopeCoeff(envMs, _sampleRate);
        q = Mathf.Clamp(q, 0.5f, 20f);

        for (int i = 0; i < _bands; i++)
        {
            float f0 = Mathf.Clamp(BandCenter(i, _bands, lowHz, highHz), 20f, _sampleRate * 0.45f);
            // RBJ cookbook band-pass, constant 0 dB peak gain.
            float w0 = 2f * Mathf.PI * f0 / _sampleRate;
            float alpha = Mathf.Sin(w0) / (2f * q);
            float a0 = 1f + alpha;
            _b0[i] = alpha / a0;
            _b2[i] = -alpha / a0;
            _a1[i] = (-2f * Mathf.Cos(w0)) / a0;
            _a2[i] = (1f - alpha) / a0;
        }
    }

    public void Reset()
    {
        System.Array.Clear(_ax1, 0, _ax1.Length); System.Array.Clear(_ax2, 0, _ax2.Length);
        System.Array.Clear(_ay1, 0, _ay1.Length); System.Array.Clear(_ay2, 0, _ay2.Length);
        System.Array.Clear(_cx1, 0, _cx1.Length); System.Array.Clear(_cx2, 0, _cx2.Length);
        System.Array.Clear(_cy1, 0, _cy1.Length); System.Array.Clear(_cy2, 0, _cy2.Length);
        System.Array.Clear(_env, 0, _env.Length);
    }

    /// One sample. `voice` is the human input, `carrier` the synthetic excitation.
    /// Audio-thread safe: no allocation, no branching on managed state.
    public float Process(int channel, float voice, float carrier)
    {
        if (_bands <= 0) return voice;
        int c = channel & (MaxChannels - 1);
        float sum = 0f;

        for (int i = 0; i < _bands; i++)
        {
            int k = c * MaxBands + i;
            float b0 = _b0[i], b2 = _b2[i], a1 = _a1[i], a2 = _a2[i];

            // --- analysis: band-pass the VOICE, then follow its envelope --------
            float av = b0 * voice + b2 * _ax2[k] - a1 * _ay1[k] - a2 * _ay2[k];
            _ax2[k] = _ax1[k]; _ax1[k] = voice;
            _ay2[k] = _ay1[k]; _ay1[k] = av;

            float rect = av < 0f ? -av : av;
            // Fast attack, smoothed release: track transients, ignore glottal pulses.
            _env[k] = rect > _env[k] ? rect : _env[k] + (rect - _env[k]) * _envCoeff;

            // --- synthesis: same band on the CARRIER, scaled by that envelope ---
            float cv = b0 * carrier + b2 * _cx2[k] - a1 * _cy1[k] - a2 * _cy2[k];
            _cx2[k] = _cx1[k]; _cx1[k] = carrier;
            _cy2[k] = _cy1[k]; _cy1[k] = cv;

            sum += cv * _env[k];
        }

        // Band-pass banks sum to well under unity; lift back to a usable level.
        return sum * 4f;
    }
}
