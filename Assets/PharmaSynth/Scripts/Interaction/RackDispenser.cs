using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for a capped tube rack (an unmovable rack that, when
/// grabbed, pulls out single items, with a cap so the maximum is controlled).
/// Unlike the ConsumableDispenser (endless single-piece box for matches/litmus),
/// a rack holds a FINITE set of REUSABLE tubes — one persistent instance per
/// hole. Pulling one leaves its hole empty; the tube stays a normal grabbable
/// (fillable, cleanable, breakable → returns home). The cap is the hole count by
/// construction: you can never have more tubes out than the rack was built with.
public static class RackDispenserMath
{
    /// A tube counts as SEATED in its hole when it's resting there un-held.
    public static bool InHole(bool held, float distFromHole, float slotRadius = 0.045f)
        => !held && distFromHole <= slotRadius;

    /// How many tubes are currently OUT of the rack (capacity minus seated).
    public static int OutCount(int capacity, int seated) => Mathf.Max(0, capacity - seated);

    /// Live rack label: "Test Tubes  4/6".
    public static string Label(string kind, int seated, int capacity)
        => kind + "  " + seated + "/" + capacity;
}

/// A fixed rack that holds a capped pool of reusable tubes. The rack itself is
/// inert furniture (no grab, kinematic); each hole owns one persistent tube whose
/// DropRespawn home is that hole, so a tube pulled out is a free grabbable and an
/// abandoned one finds its way back. Shows a live "seated/capacity" count. Built
/// by WorkspaceKitsBuilder; nothing to author by hand.
public class RackDispenser : MonoBehaviour
{
    [SerializeField] private Transform[] holes;
    [SerializeField] private GameObject[] tubes;    // one per hole; cap = length
    [SerializeField] private float slotRadius = 0.045f;
    [SerializeField] private ProximityLabel label;
    [SerializeField] private string kind = "Test Tubes";

    public int Capacity => tubes != null ? tubes.Length : 0;

    /// Builder seam (edit-mode AddComponent skips Awake).
    public void Bind(Transform[] holes, GameObject[] tubes, ProximityLabel label, string kind)
    {
        this.holes = holes; this.tubes = tubes; this.label = label;
        if (!string.IsNullOrEmpty(kind)) this.kind = kind;
    }

    /// Seated count right now (public for tests + the live label).
    public int SeatedCount()
    {
        if (tubes == null) return 0;
        int seated = 0;
        for (int i = 0; i < tubes.Length; i++)
        {
            var t = tubes[i];
            if (t == null) continue;
            var grab = t.GetComponent<XRGrab>();
            bool held = grab != null && grab.isSelected;
            float d = holes != null && i < holes.Length && holes[i] != null
                ? (t.transform.position - holes[i].position).magnitude : 999f;
            if (RackDispenserMath.InHole(held, d, slotRadius)) seated++;
        }
        return seated;
    }

    private void Update()
    {
        if (!Application.isPlaying || tubes == null || label == null) return;
        label.SetLabel(RackDispenserMath.Label(kind, SeatedCount(), Capacity), 1.4f);
    }
}
