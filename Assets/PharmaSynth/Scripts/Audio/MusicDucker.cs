using UnityEngine;

/// Pulls the background music down while an NPC is speaking, so dialogue is
/// always clearly audible over it (the background music must never
/// mask what the player needs to hear), then eases it back afterwards.
///
/// Ducks FAST and releases SLOWLY — the standard broadcast shape. A quick duck
/// means the first syllable is never buried; a slow release stops the music
/// pumping up and down between two lines of the same conversation.
public class MusicDucker : MonoBehaviour
{
    [SerializeField] private AudioSource music;

    [Tooltip("Fraction of normal volume while someone is speaking. 0.25 = duck to a quarter.")]
    [Range(0f, 1f)] public float duckTo = 0.25f;

    [Tooltip("Volume units per second going DOWN — fast, so no syllable is lost.")]
    public float attackPerSecond = 4f;

    [Tooltip("Volume units per second coming BACK — slow, so it doesn't pump between lines.")]
    public float releasePerSecond = 0.6f;

    private float _base = -1f;
    private float _lastApplied = -1f;

    public void Bind(AudioSource src) { music = src; _base = -1f; _lastApplied = -1f; }

    /// Pure (suite): where the volume should be heading right now.
    public static float TargetVolume(float baseVolume, float duckTo, bool speaking)
        => Mathf.Max(0f, baseVolume) * (speaking ? Mathf.Clamp01(duckTo) : 1f);

    private void Update()
    {
        if (music == null) return;
        bool speaking = NPCNarrationController.AnySpeaking;

        // Adopt the base level from whoever else owns this AudioSource (the
        // Settings slider, AudioService). Anything we did NOT write ourselves is
        // treated as the new normal, so ducking never fights the volume controls.
        if (_lastApplied < 0f || !Mathf.Approximately(music.volume, _lastApplied))
            if (!speaking) _base = music.volume;
        if (_base < 0f) _base = music.volume;

        float want = TargetVolume(_base, duckTo, speaking);
        float rate = (speaking ? attackPerSecond : releasePerSecond) * Time.unscaledDeltaTime;
        music.volume = Mathf.MoveTowards(music.volume, want, rate);
        _lastApplied = music.volume;
    }
}
