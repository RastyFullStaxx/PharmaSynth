using UnityEngine;

/// Audio mixer categories. Volume sliders in Settings map 1:1 to these.
public enum AudioCategory { Sfx, Ambient, Voice, Music }

/// Pure volume maths — a perceptual linear(0..1)→decibel curve for an AudioMixer
/// exposed parameter. Kept separate so it is unit-testable without a mixer asset.
public static class VolumeUtil
{
    public const float MinDb = -80f;

    /// 0 → silence (-80 dB), 1 → 0 dB, with a log taper that sounds linear to the ear.
    public static float LinearToDb(float linear01)
    {
        linear01 = Mathf.Clamp01(linear01);
        if (linear01 <= 0.0001f) return MinDb;
        return Mathf.Clamp(Mathf.Log10(linear01) * 20f, MinDb, 0f);
    }
}

/// One-stop audio playback + volume control. Holds a per-category AudioSource and a
/// SoundBank; gameplay calls Play("key") / PlayAt(...) and sets category volumes
/// (persisted to PlayerPrefs, and pushed to an optional AudioMixer's exposed
/// "<Category>Volume" params). Clips are assigned in the SoundBank later — the whole
/// service works (as silent no-ops) with zero audio assets, so it can ship now.
public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [SerializeField] private SoundBank bank;
    [SerializeField] private AudioSource sfxSource;      // one-shot
    [SerializeField] private AudioSource ambientSource;  // looping
    [SerializeField] private AudioSource voiceSource;    // Pharmee beeps
    [SerializeField] private AudioSource musicSource;    // looping
    [SerializeField] private UnityEngine.Audio.AudioMixer mixer;   // optional
    [Tooltip("Per-shot pitch variation on SFX one-shots so repeated sounds (footsteps, " +
             "clinks, pours) don't sound robotically identical — realism polish 2026-07-10.")]
    [SerializeField, Range(0f, 0.3f)] private float sfxPitchJitter = 0.08f;

    private readonly float[] _vol = { 1f, 1f, 1f, 1f };
    private bool _loaded;

    /// Pure, testable pitch jitter: base 1.0 ± amount, from a 0..1 random sample.
    public static float JitteredPitch(float amount, float rand01)
        => 1f + (Mathf.Clamp01(rand01) * 2f - 1f) * Mathf.Clamp(amount, 0f, 0.9f);

    // UI clicks and musical stings must keep a CONSTANT pitch — only physical
    // sounds (footsteps, glass, pours, drops…) get natural per-shot variation.
    private static readonly System.Collections.Generic.HashSet<string> _noPitchVary =
        new System.Collections.Generic.HashSet<string>
        { "ui-click", "ui-confirm", "ui-error", "task-complete", "grade-pass", "grade-fail",
          "mixture-complete", "alarm" };

    /// True when a key represents a physical sound that should pitch-vary.
    public static bool PitchVaries(string key)
        => !string.IsNullOrEmpty(key) && !_noPitchVary.Contains(key);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        LoadVolumes();
        // Dialogue ducking rides on the service — no scene wiring needed.
        if (GetComponent<DialogueDucker>() == null) gameObject.AddComponent<DialogueDucker>();
    }

    /// Null-safe one-shot for gameplay hooks (no-op before the service exists,
    /// e.g. in edit-mode tests).
    public static void TryPlay(string key) { if (Instance != null) Instance.Play(key); }

    /// Bank access for systems that manage their own AudioSource (looping sims).
    public SoundBank.Entry EntryOf(string key) => bank != null ? bank.Get(key) : null;

    /// The looping beds — DialogueDucker dips these while an NPC is speaking.
    public AudioSource AmbientSource => ambientSource;
    public AudioSource MusicSource => musicSource;

    // ---- volume ----------------------------------------------------------

    public float VolumeOf(AudioCategory c) => _vol[(int)c];

    public void SetVolume(AudioCategory c, float linear01)
    {
        _vol[(int)c] = Mathf.Clamp01(linear01);
        Apply(c);
        PlayerPrefs.SetFloat(PrefKey(c), _vol[(int)c]);
    }

    public void LoadVolumes()
    {
        if (_loaded) return;
        for (int i = 0; i < _vol.Length; i++)
        {
            _vol[i] = PlayerPrefs.GetFloat(PrefKey((AudioCategory)i), 1f);
            Apply((AudioCategory)i);
        }
        _loaded = true;
    }

    private void Apply(AudioCategory c)
    {
        // Push to the mixer if wired; also gate the raw AudioSources as a fallback.
        if (mixer != null) mixer.SetFloat(c + "Volume", VolumeUtil.LinearToDb(_vol[(int)c]));
        var src = SourceFor(c);
        if (src != null && mixer == null) src.volume = _vol[(int)c];
    }

    private static string PrefKey(AudioCategory c) => "audio.vol." + c;

    // ---- playback --------------------------------------------------------

    /// Play a bank sound by key on its category source (no-op if key/clip missing).
    public void Play(string key)
    {
        var e = bank != null ? bank.Get(key) : null;
        if (e == null || e.clip == null) return;
        var src = SourceFor(e.category);
        if (src == null) return;
        float v = _vol[(int)e.category] * Mathf.Clamp01(e.volume);
        if (e.category == AudioCategory.Ambient || e.category == AudioCategory.Music)
        {
            src.clip = e.clip; src.loop = true; src.volume = mixer == null ? v : 1f; src.Play();
        }
        else if (e.category == AudioCategory.Voice)
        {
            // Pharmee's beeps must NOT stack — a new line interrupts the current beep
            // instead of piling PlayOneShots on top of each other (which got deafening).
            src.loop = false; src.clip = e.clip;
            src.volume = mixer == null ? v : Mathf.Clamp01(e.volume);
            src.Play();
        }
        else
        {
            // Subtle per-shot pitch variation keeps repeated physical SFX from
            // sounding canned; UI/musical stings stay at a constant pitch.
            src.pitch = PitchVaries(key) ? JitteredPitch(sfxPitchJitter, Random.value) : 1f;
            src.PlayOneShot(e.clip, mixer == null ? v : Mathf.Clamp01(e.volume));
        }
    }

    /// Play a bank sound at a world position (spawns a temporary one-shot source).
    public void PlayAt(string key, Vector3 pos)
    {
        var e = bank != null ? bank.Get(key) : null;
        if (e == null || e.clip == null) return;
        AudioSource.PlayClipAtPoint(e.clip, pos, _vol[(int)e.category] * Mathf.Clamp01(e.volume));
    }

    /// Positional one-shot with tuned 3D rolloff + pitch variation — for spatial
    /// physical events (a beaker breaking / a tool clattering across the room), so
    /// they read as coming from that spot rather than flat 2D. Self-destroys.
    public void PlayAt3D(string key, Vector3 pos, float volumeScale = 1f)
    {
        var e = bank != null ? bank.Get(key) : null;
        if (e == null || e.clip == null) return;
        var go = new GameObject("SfxAt_" + key);
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.clip = e.clip;
        src.volume = _vol[(int)e.category] * Mathf.Clamp01(e.volume) * Mathf.Clamp01(volumeScale);
        src.pitch = PitchVaries(key) ? JitteredPitch(sfxPitchJitter, Random.value) : 1f;
        src.spatialBlend = 1f;                       // 3D
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 0.5f;
        src.maxDistance = 8f;
        src.Play();
        Destroy(go, e.clip.length / Mathf.Max(0.1f, src.pitch) + 0.1f);
    }

    /// Null-safe static positional one-shot (no-op before the service exists).
    public static void TryPlayAt(string key, Vector3 pos, float volumeScale = 1f)
    { if (Instance != null) Instance.PlayAt3D(key, pos, volumeScale); }

    /// Play the FIRST key in `keys` that actually has a clip. Lets a caller name its
    /// ideal sound and degrade to a same-material stand-in rather than the wrong one
    /// (scooping powder must not fall back to the liquid "pour").
    /// Plays nothing if none are authored yet — silence beats a misleading sound.
    public static void TryPlayFirstAt(Vector3 pos, float volumeScale, params string[] keys)
    {
        if (Instance == null || keys == null) return;
        var bank = Instance.bank;
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            if (bank != null && !bank.HasClip(k)) continue;   // no clip → try the next
            Instance.PlayAt3D(k, pos, volumeScale);
            return;
        }
    }

    /// Non-positional twin of TryPlayFirstAt, for UI/2D cues (a HUD toast or a
    /// panel has no world position). Same degrade-gracefully rule: name the ideal
    /// key first, fall back to a stand-in, stay silent rather than play the wrong
    /// thing. Exists so newly-wired UI cues can name a not-yet-authored
    /// sound without erroring.
    public static void TryPlayFirst(params string[] keys)
    {
        if (Instance == null || keys == null) return;
        var bank = Instance.bank;
        foreach (var k in keys)
        {
            if (string.IsNullOrEmpty(k)) continue;
            if (bank != null && !bank.HasClip(k)) continue;
            Instance.Play(k);
            return;
        }
    }

    public void StopAmbient() { if (ambientSource != null) ambientSource.Stop(); }

    // One-arg void wrappers for UI slider onValueChanged persistent listeners.
    public void SetSfxVolume(float v)     => SetVolume(AudioCategory.Sfx, v);
    public void SetAmbientVolume(float v) => SetVolume(AudioCategory.Ambient, v);
    public void SetVoiceVolume(float v)   => SetVolume(AudioCategory.Voice, v);
    public void SetMusicVolume(float v)   => SetVolume(AudioCategory.Music, v);

    private AudioSource SourceFor(AudioCategory c)
    {
        switch (c)
        {
            case AudioCategory.Sfx: return sfxSource;
            case AudioCategory.Ambient: return ambientSource;
            case AudioCategory.Voice: return voiceSource;
            case AudioCategory.Music: return musicSource;
            default: return sfxSource;
        }
    }

    /// Edit-mode/test binding (Awake/serialization don't run on AddComponent).
    public void Bind(SoundBank b, AudioSource sfx, AudioSource ambient, AudioSource voice, AudioSource music)
    { bank = b; sfxSource = sfx; ambientSource = ambient; voiceSource = voice; musicSource = music; }
}
