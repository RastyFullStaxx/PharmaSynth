using UnityEngine;

/// Ducks the looping ambient + music beds while ANY NPC is speaking, so dialogue
/// reads clearly, then eases them back afterward (UX pass). Ref-counts
/// speakers so overlapping lines (Pharmee + Dr. Jimenez) hold the dip until both stop.
/// Subscribes to every NPCNarrationController in the scene at Start — no wiring needed.
/// Only touches the source volumes while a duck is active; otherwise leaves them to
/// AudioService / the settings sliders.
public class DialogueDucker : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float duckFactor = 0.45f;   // beds drop to this while speaking
    [SerializeField] private float lerpSpeed = 6f;

    private int _speakers;
    private float _ambientBase = -1f, _musicBase = -1f;   // captured un-ducked volume; -1 = not ducking
    private bool _subscribed;

    /// Pure target multiplier (self-tested): full while nobody's speaking, ducked otherwise.
    public static float DuckTarget(int speakers, float factor)
        => speakers > 0 ? Mathf.Clamp01(factor) : 1f;

    public int Speakers => _speakers;

    private void Start() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed) return;
        foreach (var n in Object.FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include))
        {
            n.LineStarted += OnLineStarted;
            n.LineEnded += OnLineEnded;
        }
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        foreach (var n in Object.FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include))
        {
            n.LineStarted -= OnLineStarted;
            n.LineEnded -= OnLineEnded;
        }
        _subscribed = false;
        _speakers = 0;
    }

    private void OnLineStarted(string _, float __) => _speakers++;
    private void OnLineEnded() => _speakers = Mathf.Max(0, _speakers - 1);

    private void Update()
    {
        var svc = AudioService.Instance;
        if (svc == null) return;
        bool ducking = _speakers > 0;
        Duck(svc.AmbientSource, ref _ambientBase, ducking);
        Duck(svc.MusicSource, ref _musicBase, ducking);
    }

    /// While ducking, capture the bed's natural volume once and lerp toward
    /// base×factor; when the last speaker stops, ease back to base and release it
    /// (so settings/AudioService own the volume again between lines).
    private void Duck(AudioSource src, ref float baseVol, bool ducking)
    {
        if (src == null) return;
        if (ducking)
        {
            if (baseVol < 0f) baseVol = src.volume;              // capture at duck start
            src.volume = Mathf.Lerp(src.volume, baseVol * duckFactor, Time.deltaTime * lerpSpeed);
        }
        else if (baseVol >= 0f)
        {
            src.volume = Mathf.Lerp(src.volume, baseVol, Time.deltaTime * lerpSpeed);
            if (Mathf.Abs(src.volume - baseVol) < 0.005f) { src.volume = baseVol; baseVol = -1f; }  // restored → release
        }
    }
}
