#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Makes the generated voice-over AUDIBLE and well-behaved.
/// Three separate concerns, one pass:
///
///  1. Dr. Jimenez's narration channel needs a narratorAudioSource. SayRoutine
///     only plays a clip when one exists, so without it every one of his 37
///     lines falls through to the placeholder blips.
///  2. Fully 3D voices fade to nothing a couple of metres away. The client wants
///     them heard across the room but LOUDER up close, which is
///     a partial spatial blend: the 2D share guarantees a floor everywhere, the
///     3D share still swells as you approach.
///  3. The music ducks under dialogue.
public static class VoiceAudioBuilder
{
    /// Blend of 2D (always audible) and 3D (positional). 1 = fully positional,
    /// which is what made them inaudible across the lab; 0 would kill the sense of
    /// where the speaker is. Lowered 0.65 -> 0.55 to lift the overall
    /// level a step while keeping a clear walk-closer swell.
    public const float VoiceSpatialBlend = 0.55f;
    /// Full volume out to here, then rolls off. Raised 4 -> 8 m so the NPCs are at
    /// FULL level across essentially the whole working area — an AudioSource's own
    /// volume is already hard-capped at 1, so pushing the falloff out (and ducking
    /// the music harder) is the only real way to make them louder.
    public const float VoiceMinDistance = 8f;
    /// Still audible at the far wall; the lab is ~12 m.
    public const float VoiceMaxDistance = 30f;
    /// The music all but disappears under dialogue and eases back once it is clear.
    public const float MusicDuckTo = 0.06f;

    /// Give a narration channel a working, room-audible AudioSource, and return it.
    ///
    /// Public so the builder that CREATES a channel can leave it complete. `Wire NPC Polish`
    /// destroys and recreates Jimenez's whole subtitle object, and a channel without a
    /// source stays mute until this menu runs.
    public static AudioSource EnsureVoiceChannel(NPCNarrationController n, out bool added)
    {
        added = false;
        if (n == null) return null;

        var so = new SerializedObject(n);
        var prop = so.FindProperty("narratorAudioSource");
        var src = prop != null ? prop.objectReferenceValue as AudioSource : null;

        // 1. A channel with no source can never speak. Give it one.
        if (src == null)
        {
            src = n.GetComponent<AudioSource>();
            if (src == null) src = n.gameObject.AddComponent<AudioSource>();
            if (prop != null) { prop.objectReferenceValue = src; so.ApplyModifiedPropertiesWithoutUndo(); }
            added = true;
        }

        // 2. Heard across the room, louder close up.
        src.playOnAwake = false;
        src.loop = false;
        src.spatialBlend = VoiceSpatialBlend;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = VoiceMinDistance;
        src.maxDistance = VoiceMaxDistance;
        src.dopplerLevel = 0f;               // speech must never pitch-bend as you move
        src.priority = 0;                    // dialogue outranks every other sound
        src.volume = 1f;
        EditorUtility.SetDirty(n.gameObject);
        return src;
    }

    [MenuItem("Tools/PharmaSynth/Voice/Fix Voice Audibility + Music Ducking")]
    public static void Apply()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VoiceAudio] exit Play mode first."); return; }

        var notes = new List<string>();
        int channels = 0;

        foreach (var n in Object.FindObjectsByType<NPCNarrationController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (n == null) continue;
            Undo.RegisterFullObjectHierarchyUndo(n.gameObject, "Voice audibility");

            if (EnsureVoiceChannel(n, out bool added) != null && added)
                notes.Add("ADDED the missing narratorAudioSource on '" + n.name + "' (it was mute)");
            channels++;
        }

        // 3. Duck the music under dialogue, on every music source we can find.
        int duckers = 0;
        foreach (var src in MusicSources())
        {
            if (src == null) continue;
            var d = src.GetComponent<MusicDucker>();
            if (d == null) d = src.gameObject.AddComponent<MusicDucker>();
            d.Bind(src);
            d.duckTo = MusicDuckTo;
            d.attackPerSecond = 3.5f;    // ~0.27 s fade OUT — quick, never clips a syllable
            d.releasePerSecond = 0.55f;  // ~1.7 s fade IN — unhurried, no pumping between lines
            EditorUtility.SetDirty(src.gameObject);
            duckers++;
            notes.Add("music ducking on '" + src.gameObject.name + "'");
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[VoiceAudio] {channels} narration channel(s) made audible room-wide "
                  + $"(blend {VoiceSpatialBlend}, FULL volume to {VoiceMinDistance} m, audible to {VoiceMaxDistance} m), "
                  + $"{duckers} music source(s) fading to {Mathf.RoundToInt(MusicDuckTo * 100f)}% under dialogue "
                  + "and back up once it is clear.</color>\n  "
                  + string.Join("\n  ", notes));
    }

    /// Every plausible background-music source: the AudioService's own music
    /// channel plus the in-world speaker prop.
    static IEnumerable<AudioSource> MusicSources()
    {
        var seen = new HashSet<AudioSource>();
        var svc = Object.FindFirstObjectByType<AudioService>(FindObjectsInactive.Include);
        if (svc != null && svc.MusicSource != null) seen.Add(svc.MusicSource);
        foreach (var ms in Object.FindObjectsByType<MusicSpeaker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var a = ms.GetComponent<AudioSource>();
            if (a != null) seen.Add(a);
        }
        // Any looping source already playing a Music-category clip.
        foreach (var a in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (a != null && a.loop && a.clip != null
                && (a.gameObject.name.ToLowerInvariant().Contains("music")
                    || a.clip.name.ToLowerInvariant().Contains("music")))
                seen.Add(a);
        return seen;
    }
}
#endif
