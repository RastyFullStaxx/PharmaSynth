using System;
using System.Collections.Generic;
using UnityEngine;

public enum VoiceSpeaker { Pharmee, Jimenez }

/// Voice-over clip lookup (Pharmee + Dr. Jimenez SPEAK
/// their lines). Keyed by speaker + the normalised-text hash (VoiceLineId), so
/// no line/SO schema changes anywhere — a missing clip simply falls back to the
/// existing blip + typewriter. Rebuilt from Audio/Voice/<speaker>/<id>.mp3 by
/// Tools ▸ PharmaSynth ▸ Voice ▸ Import & Wire Voice Clips.
[CreateAssetMenu(fileName = "VoiceBank", menuName = "PharmaSynth/Voice Bank")]
public class VoiceBank : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public VoiceSpeaker speaker;
        public string id;          // VoiceLineId.For(subtitle)
        public AudioClip clip;
    }

    public List<Entry> entries = new List<Entry>();

    private Dictionary<string, AudioClip> _map;
    /// How many entries the map was built from — NOT how many it contains.
    ///
    /// ⛔ The staleness check must not be `_map.Count != entries.Count`: Rebuild skips
    /// entries with a null clip or a blank id, so those two numbers differ permanently as
    /// soon as ONE entry is incomplete, and every Get() would rebuild the whole
    /// dictionary on every spoken character of every line.
    private int _builtFrom = -1;

    public AudioClip Get(VoiceSpeaker speaker, string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (_map == null || _builtFrom != entries.Count) Rebuild();
        _map.TryGetValue(Key(speaker, id), out var clip);
        return clip;
    }

    public void Rebuild()
    {
        _map = new Dictionary<string, AudioClip>();
        foreach (var e in entries)
            if (e != null && !string.IsNullOrEmpty(e.id) && e.clip != null)
                _map[Key(e.speaker, e.id)] = e.clip;
        _builtFrom = entries.Count;
    }

    /// Entries that name a clip Unity never imported. A non-zero count means the bank looks
    /// full and speaks in blips — the failure mode that cost the six time-skip lines.
    public int BrokenEntries()
    {
        int broken = 0;
        foreach (var e in entries)
            if (e == null || string.IsNullOrEmpty(e.id) || e.clip == null) broken++;
        return broken;
    }

    private static string Key(VoiceSpeaker s, string id) => (int)s + ":" + id;
}

/// Stable line-id: FNV-1a 64-bit over the whitespace-normalised subtitle text.
/// The generation script names files by this id, so a changed line regenerates
/// exactly one clip and stale clips simply stop matching.
public static class VoiceLineId
{
    public static string For(string text)
    {
        string n = Normalize(text);
        ulong h = 14695981039346656037UL;
        foreach (char c in n)
        {
            h ^= c;
            h *= 1099511628211UL;
        }
        return h.ToString("x16");
    }

    /// Trim + collapse runs of whitespace so cosmetic edits don't re-key a line.
    public static string Normalize(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastSpace = false;
        foreach (char c in text.Trim())
        {
            bool space = char.IsWhiteSpace(c);
            if (space && lastSpace) continue;
            sb.Append(space ? ' ' : c);
            lastSpace = space;
        }
        return sb.ToString();
    }
}
