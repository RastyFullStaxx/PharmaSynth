using UnityEngine;

/// A fume-hood working volume. Tracks whether the player's hand / active work is
/// inside it, so toxic/volatile reagents can require the hood (plan §3.7).
public class FumeHoodZone : MonoBehaviour
{
    [SerializeField] private string occupantTag = "";   // e.g. player hand collider tag
    private int _occupants;

    public bool IsOccupied => _occupants > 0;

    /// Position-based test (used by reagent validation without needing physics).
    public bool Contains(Vector3 worldPos)
    {
        var col = GetComponent<Collider>();
        return col != null && col.bounds.Contains(worldPos);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (string.IsNullOrEmpty(occupantTag) || other.CompareTag(occupantTag)) _occupants++;
    }
    private void OnTriggerExit(Collider other)
    {
        if (string.IsNullOrEmpty(occupantTag) || other.CompareTag(occupantTag)) _occupants = Mathf.Max(0, _occupants - 1);
    }
}
