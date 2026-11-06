using UnityEngine;

/// Tracks whether the required grabbable LabItem is currently INSIDE this trigger
/// volume (continuous occupancy, not one-shot completion). Rigs use this to run
/// continuous verbs — e.g. the burner heats only while it sits in the heating zone,
/// gas collects only while the collection tube is held in place.
public class ZoneItemSensor : MonoBehaviour
{
    [Tooltip("LabItem.itemId this sensor watches for.")]
    [SerializeField] private string itemId = "";

    private int _count;

    public bool IsOccupied => _count > 0;
    public string ItemId => itemId;

    /// The matching LabItem currently in the zone (lets the builder gate a
    /// Heat station on its burner prop being LIT). Null when unoccupied.
    public LabItem Occupant { get; private set; }

    public void SetItemId(string id) => itemId = id;

    /// Test hook — lets edit-mode suites simulate occupancy without physics.
    public void ForceOccupied(bool on) { _count = on ? 1 : 0; if (!on) Occupant = null; }

    /// Test hook — simulate a specific occupant.
    public void ForceOccupant(LabItem item) { Occupant = item; _count = item != null ? 1 : 0; }

    private void OnTriggerEnter(Collider other)
    {
        var item = LabItem.Resolve(other);
        if (item != null && item.itemId == itemId) { _count++; Occupant = item; }
    }

    private void OnTriggerExit(Collider other)
    {
        var item = LabItem.Resolve(other);
        if (item != null && item.itemId == itemId)
        {
            _count = Mathf.Max(0, _count - 1);
            if (_count == 0) Occupant = null;
        }
    }
}
