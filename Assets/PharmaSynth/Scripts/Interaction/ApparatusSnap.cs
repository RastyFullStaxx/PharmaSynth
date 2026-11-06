using System.Collections.Generic;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Where a part seats on its host when snapped.
public enum SnapAnchor
{
    TopCenter,   // part's bounds bottom on the host's bounds top (gauze on tripod, watch glass on beaker)
    SameBase,    // part stands on the host's floor plane, centered under it (burner under tripod)
    PoleMid,     // part's centre clamps to the host's pole mid-height (iron ring on retort stand)
}

/// Pure rules for apparatus assemblies. Apparatus that belongs
/// together sticks when brought close, grab moves the WHOLE group preserving
/// formation, and the ACTIVATE click detaches (never grab). The part→host table
/// mirrors the real heating rigs the manuscript implies plus the watch-glass
/// cover. Kept plain so the suite pins the pairs + seats.
public static class AssemblyMath
{
    /// part prefab → (host prefab, seat). Directed: gauze snaps ONTO a tripod,
    /// never the other way around.
    static readonly Dictionary<string, (string host, SnapAnchor anchor)[]> Rules
        = new Dictionary<string, (string, SnapAnchor)[]>
    {
        { "WireGauze",    new[] { ("Tripod", SnapAnchor.TopCenter), ("IronRing", SnapAnchor.TopCenter) } },
        { "ClayTriangle", new[] { ("Tripod", SnapAnchor.TopCenter) } },
        { "Crucible",     new[] { ("ClayTriangle", SnapAnchor.TopCenter) } },
        { "IronRing",     new[] { ("RetortStand", SnapAnchor.PoleMid) } },
        { "BunsenBurner", new[] { ("Tripod", SnapAnchor.SameBase) } },
        { "AlcoholBurner",new[] { ("Tripod", SnapAnchor.SameBase) } },
        { "Beaker_100mL", new[] { ("WireGauze", SnapAnchor.TopCenter) } },
        { "Beaker_500mL", new[] { ("WireGauze", SnapAnchor.TopCenter) } },
        { "ErlenmeyerFlask_400mL", new[] { ("WireGauze", SnapAnchor.TopCenter) } },
        { "WatchGlass",   new[] { ("Beaker_100mL", SnapAnchor.TopCenter), ("Beaker_500mL", SnapAnchor.TopCenter) } },
    };

    /// Hosts referenced by any rule (participate without being parts themselves).
    static readonly HashSet<string> HostOnly = new HashSet<string> { "Tripod", "RetortStand" };

    public const float SnapRadius = 0.14f;

    public static bool CanAttach(string partPrefab, string hostPrefab)
        => TryAnchor(partPrefab, hostPrefab, out _);

    public static bool TryAnchor(string partPrefab, string hostPrefab, out SnapAnchor anchor)
    {
        anchor = SnapAnchor.TopCenter;
        if (partPrefab == null || hostPrefab == null) return false;
        if (!Rules.TryGetValue(partPrefab, out var hosts)) return false;
        foreach (var h in hosts)
            if (h.host == hostPrefab) { anchor = h.anchor; return true; }
        return false;
    }

    /// Everything that can take part in an assembly (as part or host).
    public static bool Participates(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return false;
        if (Rules.ContainsKey(prefabName) || HostOnly.Contains(prefabName)) return true;
        foreach (var hosts in Rules.Values)
            foreach (var h in hosts)
                if (h.host == prefabName) return true;
        return false;
    }

    /// World position the PART's bounds-centre should take so it seats on the
    /// host per the anchor kind. Pure on Bounds so the suite can pin it.
    public static Vector3 SeatCenter(SnapAnchor anchor, Bounds host, Bounds part)
    {
        switch (anchor)
        {
            case SnapAnchor.SameBase:
                return new Vector3(host.center.x, host.min.y + part.extents.y, host.center.z);
            case SnapAnchor.PoleMid:
                return new Vector3(host.center.x, host.center.y, host.center.z);
            default:   // TopCenter
                return new Vector3(host.center.x, host.max.y + part.extents.y, host.center.z);
        }
    }
}

/// Runtime snap behaviour: releasing a part near a compatible host attaches it
/// (parented, kinematic, formation preserved — grabbing ANY member's mesh grabs
/// the whole assembly via collider forwarding). The ACTIVATE click on the held
/// assembly pops the most recently attached part back off. Added by
/// PhysicsAudit.WireSceneItem / the kits builder for participating apparatus.
public class ApparatusSnap : MonoBehaviour
{
    [SerializeField] private string prefabName = "";

    private XRGrab _grab;
    private Rigidbody _rb;
    private DropRespawn _respawn;

    /// Host I'm currently seated on (null = free).
    public ApparatusSnap AttachedTo { get; private set; }
    /// Parts seated on ME, in attach order (LIFO detach).
    public readonly List<ApparatusSnap> Attached = new List<ApparatusSnap>();

    private Transform _preAttachParent;
    private readonly List<Collider> _forwarded = new List<Collider>();

    public string PrefabName => prefabName;

    void Awake() { if (_grab == null) Bind(prefabName, GetComponent<XRGrab>(), GetComponent<Rigidbody>()); }

    public void Bind(string prefab, XRGrab grab, Rigidbody rb)
    {
        if (!string.IsNullOrEmpty(prefab)) prefabName = prefab;
        _grab = grab; _rb = rb;
        _respawn = GetComponent<DropRespawn>();
        if (_grab != null)
        {
            _grab.selectExited.RemoveListener(OnReleased);
            _grab.selectExited.AddListener(OnReleased);
            _grab.activated.RemoveListener(OnActivated);
            _grab.activated.AddListener(OnActivated);
        }
    }

    void OnDestroy()
    {
        if (_grab != null)
        {
            _grab.selectExited.RemoveListener(OnReleased);
            _grab.activated.RemoveListener(OnActivated);
        }
    }

    /// Root of the assembly this piece belongs to.
    public ApparatusSnap Root()
    {
        var r = this;
        while (r.AttachedTo != null) r = r.AttachedTo;
        return r;
    }

    // ---- attach on release --------------------------------------------------

    private void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs _)
    {
        if (!Application.isPlaying || AttachedTo != null) return;
        var host = FindHost(out SnapAnchor anchor);
        if (host != null) Attach(host, anchor);
    }

    private ApparatusSnap FindHost(out SnapAnchor anchor)
    {
        anchor = SnapAnchor.TopCenter;
        var myB = WorldBounds(gameObject);
        var cols = Physics.OverlapSphere(myB.center, AssemblyMath.SnapRadius, ~0, QueryTriggerInteraction.Ignore);
        ApparatusSnap best = null;
        float bestDist = float.MaxValue;
        foreach (var col in cols)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            var snap = col.GetComponentInParent<ApparatusSnap>();
            if (snap == null || snap == this) continue;
            if (snap.Root() == this) continue;   // never attach to my own subtree
            if (!AssemblyMath.TryAnchor(prefabName, snap.prefabName, out var a)) continue;
            float d = (col.ClosestPoint(myB.center) - myB.center).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = snap; anchor = a; }
        }
        return best;
    }

    public void Attach(ApparatusSnap host, SnapAnchor anchor)
    {
        var hostB = WorldBounds(host.gameObject);
        var partB = WorldBounds(gameObject);

        _preAttachParent = transform.parent;
        transform.SetParent(host.transform, true);
        // Upright, aligned with the host's yaw, seated per the anchor rule.
        transform.rotation = Quaternion.Euler(0f, host.transform.eulerAngles.y, 0f);
        var seated = WorldBounds(gameObject);
        Vector3 target = AssemblyMath.SeatCenter(anchor, hostB, seated);
        transform.position += target - seated.center;

        if (_rb != null) _rb.isKinematic = true;
        if (_respawn != null) _respawn.Suspended = true;
        AttachedTo = host;
        host.Attached.Add(this);

        // Forward my colliders to the ROOT's grab so grabbing any member mesh
        // grabs the whole assembly, formation preserved.
        var root = host.Root();
        root.ForwardColliders(this);
        if (_grab != null) _grab.enabled = false;

        AudioService.TryPlayAt("socket-snap", transform.position);
        FloatingText.Show("Attached: " + Mishandling.DisplayNameFor(gameObject)
                          + " → " + Mishandling.DisplayNameFor(root.gameObject),
                          transform.position + Vector3.up * 0.08f, new Color(0.6f, 0.95f, 1f), 0.85f);
    }

    // ---- detach on ACTIVATE (the click, never the grab) ----------------------

    private void OnActivated(UnityEngine.XR.Interaction.Toolkit.ActivateEventArgs _)
    {
        // Fires on the assembly ROOT's grab while held: pop the newest part.
        DetachNewest();
    }

    /// Detach the most recently attached part anywhere in my subtree.
    public bool DetachNewest()
    {
        // Depth-first: the newest attachment of the deepest chain pops first
        // (beaker off the gauze before the gauze off the tripod).
        for (int i = Attached.Count - 1; i >= 0; i--)
        {
            var part = Attached[i];
            if (part == null) { Attached.RemoveAt(i); continue; }
            if (part.DetachNewest()) return true;
            part.Detach();
            return true;
        }
        return false;
    }

    /// Detach myself from my host (in place, parked kinematic — grab it next).
    public void Detach()
    {
        if (AttachedTo == null) return;
        var root = Root();
        root.UnforwardColliders(this);
        AttachedTo.Attached.Remove(this);
        AttachedTo = null;
        transform.SetParent(_preAttachParent, true);
        if (_rb != null) _rb.isKinematic = true;   // parked; next grab re-frees it
        if (_respawn != null) _respawn.Suspended = false;
        if (_grab != null) _grab.enabled = true;
        AudioService.TryPlayAt("glass-clink", transform.position, 0.4f);
        FloatingText.Show("Detached: " + Mishandling.DisplayNameFor(gameObject),
                          transform.position + Vector3.up * 0.08f, new Color(1f, 0.9f, 0.6f), 0.85f);
    }

    // ---- collider forwarding (grab-any-member → move the group) --------------

    private void ForwardColliders(ApparatusSnap part)
    {
        if (_grab == null) return;
        bool changed = false;
        foreach (var col in part.GetComponentsInChildren<Collider>())
        {
            if (col == null || col.isTrigger || _grab.colliders.Contains(col)) continue;
            _grab.colliders.Add(col);
            _forwarded.Add(col);
            changed = true;
        }
        if (changed) Reregister();
    }

    private void UnforwardColliders(ApparatusSnap part)
    {
        if (_grab == null) return;
        bool changed = false;
        foreach (var col in part.GetComponentsInChildren<Collider>())
        {
            if (col == null) continue;
            if (_grab.colliders.Remove(col)) { _forwarded.Remove(col); changed = true; }
        }
        if (changed) Reregister();
    }

    /// XRI maps collider→interactable at registration; rebuild after mutating.
    private void Reregister()
    {
        var manager = _grab.interactionManager;
        if (manager == null || !_grab.isActiveAndEnabled) return;
        manager.UnregisterInteractable(_grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable);
        manager.RegisterInteractable(_grab as UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable);
    }

    static Bounds WorldBounds(GameObject g)
    {
        var rs = g.GetComponentsInChildren<Renderer>();
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(g.transform.position, Vector3.one * 0.05f);
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }
}
