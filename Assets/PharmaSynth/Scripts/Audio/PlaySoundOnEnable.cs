using UnityEngine;

/// Plays a SoundBank key through the AudioService when the scene starts — used for
/// looping beds (menu music, lab ambient). No-op if there's no AudioService/clip.
public class PlaySoundOnEnable : MonoBehaviour
{
    [SerializeField] private string key = "";
    [SerializeField] private bool playOnStart = true;

    private void Start() { if (playOnStart) Play(); }

    public void Play()
    {
        if (AudioService.Instance != null && !string.IsNullOrEmpty(key))
            AudioService.Instance.Play(key);
    }
}
