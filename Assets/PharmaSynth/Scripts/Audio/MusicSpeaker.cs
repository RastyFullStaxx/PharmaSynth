using UnityEngine;

/// A physical music speaker in the lab: a 3D positional source
/// standing in a corner that plays a PLAYLIST of background tracks — louder as you
/// approach it (logarithmic rolloff), quieter across the room. Its volume also
/// smoothly fades in/out with the screen fade, so moving between the menu room and
/// the lab crossfades the music instead of hard-cutting it.
///
/// Thin MonoBehaviour: playlist advance + fade envelope are pure/testable; the
/// AudioSource is built at runtime so the clip list can be swapped by a builder.
public class MusicSpeaker : MonoBehaviour
{
    [SerializeField] private AudioClip[] playlist;
    [Range(0f, 1f)][SerializeField] private float baseVolume = 0.6f;
    [SerializeField] private float minDistance = 3.5f;   // full inside this radius
    [SerializeField] private float maxDistance = 24f;    // fades to inaudible by here
    [SerializeField] private float fadeSeconds = 1.6f;   // smooth music fade in/out
    [SerializeField] private bool tieToScreenFade = true;// dim while the screen is black
    [SerializeField] private bool shuffle = true;

    private AudioSource _src;
    private int _index = -1;
    private float _gain;                                  // 0..1 eased envelope

    public bool IsPlaying => _src != null && _src.isPlaying;
    public float CurrentGain => _gain;

    /// Builder seam (Awake/serialization don't run on AddComponent in edit mode).
    public void Configure(AudioClip[] clips, float volume, float minD, float maxD)
    {
        playlist = clips; baseVolume = volume; minDistance = minD; maxDistance = maxD;
    }

    /// Next track index: sequential wrap, or a non-repeating shuffle. Pure/tested.
    public static int NextIndex(int current, int count, bool shuffle, float rand01)
    {
        if (count <= 1) return 0;
        if (!shuffle) return (current + 1) % count;
        int n = Mathf.Clamp((int)(rand01 * count), 0, count - 1);
        if (n == current) n = (n + 1) % count;            // never repeat the same track back-to-back
        return n;
    }

    private void Start() => Build();

    private void Build()
    {
        if (_src != null || playlist == null || playlist.Length == 0) return;
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.loop = false;                                // playlist advances manually
        _src.spatialBlend = 1f;                           // fully 3D — proximity is the point
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.minDistance = minDistance;
        _src.maxDistance = maxDistance;
        _src.dopplerLevel = 0f;
        _src.volume = 0f;                                 // fades up via _gain
        PlayNext();
    }

    private void PlayNext()
    {
        if (_src == null || playlist == null || playlist.Length == 0) return;
        _index = NextIndex(_index, playlist.Length, shuffle, Random.value);
        var clip = playlist[_index];
        if (clip == null) return;
        _src.clip = clip;
        _src.Play();
    }

    private void Update()
    {
        if (_src == null) return;

        // Advance to the next track when the current one finishes.
        if (_src.clip != null && !_src.isPlaying) PlayNext();

        // Fade the music with the screen: full when clear, silent when black — so
        // scene transitions (menu <-> lab, resets) crossfade smoothly.
        float target = 1f;
        if (tieToScreenFade && ScreenFader.Instance != null)
            target = 1f - Mathf.Clamp01(ScreenFader.Instance.State.Alpha);
        _gain = Mathf.MoveTowards(_gain, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeSeconds));

        float musicCat = AudioService.Instance != null ? AudioService.Instance.VolumeOf(AudioCategory.Music) : 1f;
        _src.volume = Mathf.Clamp01(baseVolume) * musicCat * _gain;
    }
}
