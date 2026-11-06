using UnityEngine;

/// Positional machine hum (a subtle AC-unit hum from the
/// AC assets that grows a little louder up close). Builds its own looping 3D
/// AudioSource from a SoundBank key — quiet across the room, louder up close via a
/// logarithmic rolloff. Attach to each AC unit (LabAudioBuilder wires them);
/// no-ops silently when the clip isn't supplied yet.
public class ProximityHum : MonoBehaviour
{
    [SerializeField] private string clipKey = "ambient-lab";   // ventilation hum
    [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;
    [SerializeField] private float minDistance = 0.7f;   // full volume inside this
    [SerializeField] private float maxDistance = 7f;     // inaudible beyond this

    private AudioSource _src;
    private bool _started;

    public bool IsHumming => _src != null && _src.isPlaying;

    public void Bind(string key, float vol) { clipKey = key; volume = vol; }

    private void Update()
    {
        // Lazy start: AudioService may awake after us, and clips can arrive later.
        if (_started || AudioService.Instance == null) return;
        var e = AudioService.Instance.EntryOf(clipKey);
        if (e == null || e.clip == null) return;
        _src = gameObject.AddComponent<AudioSource>();
        _src.clip = e.clip;
        _src.loop = true;
        _src.playOnAwake = false;
        _src.spatialBlend = 1f;                            // fully 3D
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.minDistance = minDistance;
        _src.maxDistance = maxDistance;
        _src.dopplerLevel = 0f;
        _src.volume = Mathf.Clamp01(volume * Mathf.Clamp01(e.volume) * 3f); // positional hum sits above the bed
        _src.Play();
        _started = true;
    }
}
