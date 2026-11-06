#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Wire the generated solid-material SFX into the SoundBank so scooping powder
/// doesn't sound like liquid. The scoop verb calls the "scoop"
/// and "powder-pour" keys via AudioService.TryPlayFirstAt, which deliberately has
/// NO liquid fallback, so these clips are what make it audible. Clips generated
/// with elevenlabs-sound-effects-v2 into Audio/Generated/. Idempotent: updates the
/// entries in place if they already exist.
public static class ScoopSoundWiring
{
    const string BankPath = "Assets/PharmaSynth/ScriptableObjects/SoundBank.asset";
    const string ScoopClip = "Assets/PharmaSynth/Audio/Generated/scoop.wav";
    const string PourClip = "Assets/PharmaSynth/Audio/Generated/powder-pour.wav";

    [MenuItem("Tools/PharmaSynth/Wire Scoop Sounds")]
    public static void Run()
    {
        var bank = AssetDatabase.LoadAssetAtPath<SoundBank>(BankPath);
        if (bank == null) { Debug.LogError("[ScoopSfx] SoundBank not found at " + BankPath); return; }

        int n = 0;
        n += Upsert(bank, "scoop", ScoopClip, 0.55f) ? 1 : 0;
        n += Upsert(bank, "powder-pour", PourClip, 0.7f) ? 1 : 0;

        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();
        Debug.Log($"<color=#4CD07D>[ScoopSfx] {n} solid-material sound(s) wired — scooping powder now sounds "
                  + "granular instead of liquid.</color>");
    }

    /// Add the key, or repoint an existing one at the clip. Sfx category (0).
    static bool Upsert(SoundBank bank, string key, string clipPath, float volume)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip == null) { Debug.LogWarning("[ScoopSfx] missing clip: " + clipPath); return false; }

        var e = bank.Get(key);
        if (e == null)
        {
            e = new SoundBank.Entry { key = key };
            bank.entries.Add(e);
        }
        e.clip = clip;
        e.category = AudioCategory.Sfx;
        e.volume = volume;
        Debug.Log($"[ScoopSfx] '{key}' -> {clipPath}  (vol {volume})");
        return true;
    }
}
#endif
