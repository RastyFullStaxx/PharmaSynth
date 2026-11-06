using System;
using System.Collections.Generic;
using UnityEngine;

/// Named clip lookup so gameplay code triggers sounds by key (e.g. "pour",
/// "glass-shatter", "pharmee-warn") and the actual AudioClips are assigned in the
/// asset later — no code change when the audio pass lands. Entries with a null clip
/// are valid placeholders (AudioService no-ops on them).
[CreateAssetMenu(fileName = "SoundBank", menuName = "PharmaSynth/Sound Bank")]
public class SoundBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        public AudioClip clip;
        public AudioCategory category = AudioCategory.Sfx;
        [Range(0f, 1f)] public float volume = 1f;
    }

    public List<Entry> entries = new List<Entry>();

    public Entry Get(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < entries.Count; i++)
            if (entries[i] != null && entries[i].key == key) return entries[i];
        return null;
    }

    /// True when this key exists AND has a real clip — lets callers pick a sensible
    /// fallback instead of playing the wrong material's sound (e.g.
    /// scooping powder must never sound like pouring liquid).
    public bool HasClip(string key)
    {
        var e = Get(key);
        return e != null && e.clip != null;
    }

    /// Keys the game expects to exist (so production has a checklist). Missing keys
    /// are reported by the self-test; missing *clips* are fine until the audio pass.
    public static readonly string[] ExpectedKeys =
    {
        "pour", "bubble", "glass-clink", "glass-shatter", "burner-ignite", "alarm",
        "ui-click", "ui-confirm", "ui-error", "task-complete", "grade-pass", "grade-fail",
        "pharmee-greet", "pharmee-instruct", "pharmee-warn", "pharmee-celebrate",
        "ambient-lab", "music-menu", "music-lab",
        // Solids: powder must sound granular, never like liquid.
        "scoop",        // blade digging into a powder/crystal charge
        "powder-pour",  // tipping a scoopful of solid into a vessel
    };
}
