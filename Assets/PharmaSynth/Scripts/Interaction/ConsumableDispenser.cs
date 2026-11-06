using System.Collections.Generic;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure dispenser rules (the box sits on the shelf; grabbing it
/// pulls out a single piece to use). Kept plain-C# so the self-tests pin the
/// policy without a headset.
public static class DispenserMath
{
    /// A resting piece counts as TAKEN once a hand grabs it or it has moved off
    /// its rest slot — that's the cue to queue a fresh one.
    public static bool IsTaken(bool grabbed, float distFromRest, float takenDistance = 0.08f)
        => grabbed || distFromRest > takenDistance;

    /// A used piece (picked up at least once, now set down and still) is
    /// abandoned after a short idle — removed so spent strips/sticks don't pile
    /// up on the bench. Never discards a piece that's still in hand or moving.
    public static bool ShouldDiscard(bool everHeld, bool held, float speed, float idleSeconds,
                                     float minIdle = 12f, float restSpeed = 0.05f)
        => everHeld && !held && speed < restSpeed && idleSeconds >= minIdle;
}

/// Turns a consumable box into an endless single-piece dispenser. The box stays
/// fixed on the shelf; one ready piece rests in it. When a hand takes that piece,
/// a fresh one appears after a short delay — so there's always exactly
/// `readyCount` pieces to grab. Taken pieces get a DispensedConsumable that
/// cleans them up once abandoned. Clones a hidden, fully-wired TEMPLATE so all
/// the per-consumable wiring lives in the editor builder, not here.
public class ConsumableDispenser : MonoBehaviour
{
    [SerializeField] private GameObject template;     // hidden, fully-wired single to clone
    [SerializeField] private Transform restAnchor;    // where a ready piece sits
    [SerializeField] private Transform spawnParent;    // clones parent here (NOT under a rigidbody)
    [SerializeField] private GameObject seedSingle;    // the editor-placed first ready piece
    [SerializeField, Min(1)] private int readyCount = 1;
    [SerializeField, Min(0f)] private float refillDelay = 0.6f;
    [SerializeField, Min(0f)] private float takenDistance = 0.08f;

    private readonly List<GameObject> _ready = new List<GameObject>();
    private float _nextSpawn;

    /// Builder seam (edit-mode AddComponent skips Awake).
    public void Bind(GameObject template, Transform restAnchor, Transform spawnParent, GameObject seed, int readyCount)
    {
        this.template = template; this.restAnchor = restAnchor; this.spawnParent = spawnParent;
        this.seedSingle = seed; this.readyCount = Mathf.Max(1, readyCount);
    }

    private void Start()
    {
        if (seedSingle != null) _ready.Add(seedSingle);
    }

    private void Update()
    {
        if (!Application.isPlaying || template == null || restAnchor == null) return;

        // Retire pieces the player has taken from the ready set.
        for (int i = _ready.Count - 1; i >= 0; i--)
        {
            var s = _ready[i];
            if (s == null) { _ready.RemoveAt(i); continue; }
            var grab = s.GetComponent<XRGrab>();
            float dist = (s.transform.position - restAnchor.position).magnitude;
            if (DispenserMath.IsTaken(grab != null && grab.isSelected, dist, takenDistance))
            {
                if (s.GetComponent<DispensedConsumable>() == null) s.AddComponent<DispensedConsumable>();
                _ready.RemoveAt(i);
            }
        }

        // Keep the slot stocked.
        if (_ready.Count < readyCount && Time.time >= _nextSpawn)
        {
            Spawn();
            _nextSpawn = Time.time + refillDelay;
        }
    }

    private void Spawn()
    {
        var go = Instantiate(template, spawnParent != null ? spawnParent : transform.parent);
        go.name = template.name.Replace("Template_", "");
        go.transform.SetPositionAndRotation(
            restAnchor.position + Vector3.up * (0.004f * _ready.Count), restAnchor.rotation);
        go.SetActive(true);   // template is kept inactive
        _ready.Add(go);
    }
}
