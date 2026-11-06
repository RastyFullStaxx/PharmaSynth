using UnityEngine;

/// Cyan "materialize" spawn burst: a one-shot column of cyan
/// particles that rises from the player's feet like smoke on every teleport /
/// reset / spawn — the classic game spawn animation. Scene singleton (mirrors
/// ScreenFader); triggers fire it null-safely via SpawnBurstFX.Instance?.PlayAtPlayer().
public class SpawnBurstFX : MonoBehaviour
{
    public static SpawnBurstFX Instance { get; private set; }

    [SerializeField] private ParticleSystem burst;       // child system, looping OFF
    [SerializeField] private bool playOnStart = true;    // fire once on scene load
    [SerializeField] private float startDelay = 0.35f;   // let the fade-in begin first
    [SerializeField] private float footOffsetY = 0.02f;  // just above the floor
    [SerializeField] private float fallbackEyeHeight = 1.6f;

    private void Awake()
    {
        Instance = this;
        if (burst == null) burst = GetComponentInChildren<ParticleSystem>(true);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        if (playOnStart && Application.isPlaying)
            Invoke(nameof(PlayAtPlayer), Mathf.Max(0f, startDelay));
    }

    /// Emit at the player's feet: camera XZ, floor Y (ray-cast down, else eye − height).
    public void PlayAtPlayer()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 p = cam.transform.position;
        float footY = p.y - fallbackEyeHeight;
        if (Physics.Raycast(p + Vector3.up * 0.1f, Vector3.down, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore))
            footY = hit.point.y;
        PlayAt(new Vector3(p.x, footY + footOffsetY, p.z));
    }

    public void PlayAt(Vector3 feet)
    {
        if (burst == null) return;
        transform.position = feet;
        burst.Clear(true);
        burst.Play(true);
    }
}
