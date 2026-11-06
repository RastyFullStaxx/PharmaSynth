using UnityEngine;

/// ONE shared tuning for Pharmee's robot voice (one
/// reusable effect, so one tuning applies to every Pharmee voice).
///
/// Every RobotVoiceFx reads this asset, so there is a single place to dial the
/// character and it reaches every line she will ever speak — generated or not.
/// Tuning never costs a credit and never requires regenerating audio.
///
/// It is a ScriptableObject on purpose: values changed while PLAY is running are
/// written to the asset and SURVIVE exiting play mode, unlike edits to a scene
/// component. So you can tune by ear in a live session and keep what you land on.
///
/// ⭐ THE CHOSEN CHARACTER (picked after auditioning all the alternatives):
/// RING MODULATION at a LOW carrier, blended with dry signal. It sounds
/// like "speaking against an electric fan", which is intended — just not too strong.
/// This was arrived at by elimination, so do not "improve" it away:
///   • Heavier band-limiting  -> "muffled"           (rejected)
///   • Decimation + comb      -> "sounds so human"   (rejected — it recolours
///                                timbre but leaves the human pitch contour)
///   • Full VOCODER           -> "so much worse"     (rejected outright, even
///                                though it is the textbook robot technique)
/// The vocoder, decimation and comb code all still exist and are simply switched
/// off here. Turning them back on is a values change, not a code change — but ask
/// first, because every one of them has already been auditioned and refused.
[CreateAssetMenu(menuName = "PharmaSynth/Robot Voice Profile", fileName = "RobotVoiceProfile")]
public class RobotVoiceProfile : ScriptableObject
{
    [Header("⭐ THE CHARACTER — ring modulation")]
    [Tooltip("How strong the 'fan' is. THIS is the knob for Pharmee's robot character. " +
             "0.38 was judged too strong, 0.27 is the current setting. Lower = more human, " +
             "higher = more machine drone.")]
    [Range(0f, 1f)] public float ringMix = 0.27f;

    [Tooltip("Carrier frequency in Hz — the PITCH of the fan. 62 is the chosen sound. " +
             "Lower = slower, heavier motor; higher stops reading as a fan and starts " +
             "sounding like a dissonant ring.")]
    [Range(20f, 800f)] public float ringHz = 62f;

    [Header("Clarity — keeps her intelligible")]
    [Tooltip("Lift of the band above presenceHz. This is what fixed the earlier 'muffled' " +
             "complaint — consonants live at 3 kHz and up.")]
    [Range(0f, 2f)] public float presenceBoost = 0.9f;

    [Range(500f, 8000f)] public float presenceHz = 3000f;

    [Tooltip("Bit depth. Digital grit that BRIGHTENS (unlike distortion, which dulls).")]
    [Range(4f, 16f)] public float crushBits = 10f;

    [Range(0f, 1f)] public float crushMix = 0.35f;

    [Header("OFF — auditioned and rejected. Ask before enabling.")]
    [Tooltip("Vocoder blend. Textbook robot technique — rebuilds the words on a synthetic " +
             "monotone carrier. User verdict 2026-07-28: 'so much worse'. Left at 0.")]
    [Range(0f, 1f)] public float vocoderMix = 0f;

    [Range(60f, 400f)] public float carrierHz = 205f;
    [Range(0f, 0.6f)] public float carrierNoise = 0.18f;
    [Range(6f, 20f)] public float vocoderBands = 14f;
    [Range(1f, 12f)] public float vocoderQ = 4.5f;

    [Tooltip("Sample-and-hold decimation. Strong 'computer' cue, but the user found the " +
             "result still read as human. 1 = off.")]
    [Range(1f, 8f)] public float downsample = 1f;

    [Tooltip("Comb resonance mix. Hollow/metallic. Rejected with decimation. 0 = off.")]
    [Range(0f, 1f)] public float combMix = 0f;

    [Range(0.5f, 10f)] public float combMs = 3.2f;
    [Range(0f, 0.95f)] public float combFeedback = 0.72f;

    [Header("A/B")]
    [Tooltip("Off = hear the raw text-to-speech read, for comparison.")]
    public bool active = true;
}
