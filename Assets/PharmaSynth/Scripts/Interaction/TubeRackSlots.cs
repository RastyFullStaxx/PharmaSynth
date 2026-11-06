using System.Collections.Generic;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for snapping a released test tube into a workspace holder slot
/// of Experiment_Tube_Table_Kit_Holder_1 to 4: any tube, empty or filled,
/// released near a free slot.
///
/// The green Slot_* anchors are hand-placed editor markers in the holder's holes;
/// this is their runtime half:
/// release any tube (regular or hard-glass — both carry itemId kit-testtube) near
/// a free slot and it seats upright in the hole.
public static class TubeSlotMath
{
    /// How far from a slot a released tube still counts as "meant for it".
    /// Generous on purpose: in VR you let go NEAR the rack, not inside a 8 mm hole.
    public const float SnapRadius = 0.15f;

    /// A tube is a snap candidate only when it is FREE-FALLING near the rack:
    /// not held (releasing is the gesture), not already frozen kinematic
    /// (a seated/settled tube must not be re-stolen by a neighbouring slot).
    public static bool CanSnap(bool held, bool kinematic) => !held && !kinematic;

    /// Vertical shift that puts a tube's BOTTOM on the slot. Seat by the mesh
    /// bounds, never the pivot: these tube meshes carry their pivot at the TOP,
    /// so pivot-to-slot sinks the whole 13 cm body under the tabletop.
    public static float BottomAlignDelta(float slotY, float boundsMinY) => slotY - boundsMinY;

    /// Nearest FREE slot within radius; -1 when none. Distance is measured to the
    /// slot's BASE, where the tube's bottom lands.
    public static int NearestFreeSlot(Vector3 tubePos, IReadOnlyList<Vector3> slotPos,
                                      IReadOnlyList<bool> occupied, float radius = SnapRadius)
    {
        int best = -1;
        float bestSq = radius * radius;
        for (int i = 0; i < slotPos.Count; i++)
        {
            if (occupied[i]) continue;
            float dSq = (tubePos - slotPos[i]).sqrMagnitude;
            if (dSq <= bestSq) { bestSq = dSq; best = i; }
        }
        return best;
    }
}

/// Runtime half of the workspace tube holders: watches every kit-testtube in the
/// lab and seats a released one into the nearest free Slot_* anchor. A seated tube
/// is frozen kinematic (the same end state DropRespawn's settle-freeze produces,
/// so the physics policy chain — grab → dynamic → release — keeps working);
/// grabbing it back frees the slot.
///
/// One component per holder, wired by "Name Tubes + Build Rack Slots".
public class TubeRackSlots : MonoBehaviour
{
    [SerializeField] private float snapRadius = TubeSlotMath.SnapRadius;

    private readonly List<Transform> _slots = new List<Transform>();
    private readonly List<Transform> _occupants = new List<Transform>();
    private readonly List<Vector3> _seatPos = new List<Vector3>();   // where the occupant's PIVOT ended up
    private static List<LabItem> _tubes;            // shared: tubes never spawn mid-run
    private static float _tubesScannedAt = -999f;

    /// Edit-mode / builder seam: collect the Slot_* children.
    public void Bind()
    {
        _slots.Clear();
        _occupants.Clear();
        _seatPos.Clear();
        for (int i = 0; ; i++)
        {
            var s = transform.Find("Slot_" + i);
            if (s == null) break;
            _slots.Add(s);
            _occupants.Add(null);
            _seatPos.Add(Vector3.zero);
        }
    }

    public int SlotCount { get { if (_slots.Count == 0) Bind(); return _slots.Count; } }

    void Awake() { Bind(); }

    void Update()
    {
        if (!Application.isPlaying || _slots.Count == 0) return;

        // Tube roster, shared across holders, refreshed rarely (a broken tube is
        // REPLACED in place by DropRespawn, so the objects are stable; the rescan
        // is only a guard against late scene surgery).
        if (_tubes == null || Time.time - _tubesScannedAt > 10f)
        {
            _tubes = new List<LabItem>();
            foreach (var li in FindObjectsByType<LabItem>(FindObjectsInactive.Include))
                if (li != null && li.itemId == "kit-testtube") _tubes.Add(li);
            _tubesScannedAt = Time.time;
        }

        // Free any slot whose occupant was taken away (grabbed or moved).
        for (int i = 0; i < _slots.Count; i++)
        {
            var occ = _occupants[i];
            if (occ == null) continue;
            var g = occ.GetComponent<XRGrab>();
            // Drift is measured against the pose Seat() actually left the tube in —
            // NOT the slot: the tube pivot is at its TOP, ~0.13 above the slot base,
            // which is already at the edge of snapRadius and would self-evict.
            bool taken = (g != null && g.isSelected)
                         || (occ.position - _seatPos[i]).sqrMagnitude > snapRadius * snapRadius;
            if (taken) _occupants[i] = null;
        }

        // Seat any free-falling tube released near a free slot.
        foreach (var li in _tubes)
        {
            if (li == null) continue;
            var t = li.transform;
            if (IsSeatedHere(t)) continue;
            var grab = li.GetComponent<XRGrab>();
            var rb = li.GetComponent<Rigidbody>();
            if (!TubeSlotMath.CanSnap(grab != null && grab.isSelected, rb != null && rb.isKinematic)) continue;

            int slot = NearestFree(t.position);
            if (slot < 0) continue;
            Seat(t, rb, slot);
        }
    }

    private bool IsSeatedHere(Transform t)
    {
        for (int i = 0; i < _occupants.Count; i++) if (_occupants[i] == t) return true;
        return false;
    }

    private int NearestFree(Vector3 pos)
    {
        // Local shim over the pure math (Transforms → positions).
        var positions = new Vector3[_slots.Count];
        var occupied = new bool[_slots.Count];
        for (int i = 0; i < _slots.Count; i++)
        {
            positions[i] = _slots[i].position;
            occupied[i] = _occupants[i] != null;
        }
        return TubeSlotMath.NearestFreeSlot(pos, positions, occupied, snapRadius);
    }

    private void Seat(Transform tube, Rigidbody rb, int slot)
    {
        // Stand it upright first, THEN measure: rotation changes where the mesh
        // bottom lands. Bottom-align by bounds, never the pivot (see BottomAlignDelta).
        tube.SetPositionAndRotation(_slots[slot].position, _slots[slot].rotation);
        // SOLID meshes only. A tube that has been poured from keeps a world-space
        // StreamLine / PourStream child pointing at wherever the arc last landed,
        // so encapsulating every renderer puts b.min.y on the FLOOR and the
        // bottom-align shoves the tube a metre into the air.
        var b = ExperimentSceneBuilder.SolidWorldBounds(tube.gameObject);
        tube.position += Vector3.up * TubeSlotMath.BottomAlignDelta(_slots[slot].position.y, b.min.y);
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;   // = DropRespawn's settle-freeze end state
        }
        _occupants[slot] = tube;
        _seatPos[slot] = tube.position;
        AudioService.TryPlayFirstAt(tube.position, 0.5f, "glass-set", "glass-clink");
    }
}
