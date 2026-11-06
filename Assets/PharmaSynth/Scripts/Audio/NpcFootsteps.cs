using UnityEngine;

/// Subtle positional footsteps for a walking NPC (Dr. Jimenez).
/// Tracks its own transform's horizontal movement and plays a quiet 3D footstep at
/// its feet once per stride — so his roaming reads audibly but stays background
/// (distance rolloff keeps it soft across the room). Reuses the shared StrideMath +
/// the SoundBank footstep clip at a lowered, own-source volume.
public class NpcFootsteps : MonoBehaviour
{
    [SerializeField] private string key = "footstep";
    [SerializeField] private float strideMeters = 0.62f;   // his gait is shorter than the player's
    [Range(0f, 1f)][SerializeField] private float volume = 0.22f;   // subtle
    [SerializeField] private float snapGuardMeters = 1.2f; // bigger jump = teleport, not a step
    [SerializeField] private float minDistance = 1.5f;
    [SerializeField] private float maxDistance = 12f;

    private AudioSource _src;
    private Vector3 _last;
    private float _acc;
    private bool _has;

    private void Start()
    {
        _src = gameObject.AddComponent<AudioSource>();
        _src.playOnAwake = false;
        _src.spatialBlend = 1f;                             // 3D — comes from his feet
        _src.rolloffMode = AudioRolloffMode.Logarithmic;
        _src.minDistance = minDistance;
        _src.maxDistance = maxDistance;
        _src.dopplerLevel = 0f;
        _has = false;
    }

    private void Update()
    {
        Vector3 p = transform.position; p.y = 0f;
        if (!_has) { _last = p; _has = true; return; }
        float d = (p - _last).magnitude;
        _last = p;
        if (d > snapGuardMeters) { _acc = 0f; return; }     // teleport (leash snap-back)
        for (int i = StrideMath.Steps(ref _acc, d, strideMeters); i > 0; i--) PlayStep();
    }

    private void PlayStep()
    {
        if (_src == null || AudioService.Instance == null) return;
        var e = AudioService.Instance.EntryOf(key);
        if (e == null || e.clip == null) return;
        float cat = AudioService.Instance.VolumeOf(AudioCategory.Sfx);
        _src.pitch = AudioService.JitteredPitch(0.1f, Random.value);
        _src.PlayOneShot(e.clip, Mathf.Clamp01(volume) * cat);
    }
}
