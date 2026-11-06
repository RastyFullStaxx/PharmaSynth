using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Pure rules for when a loose prop goes back to its shelf spot (§2: kill-Z +
/// idle return-to-home). Separated from the MonoBehaviour so the self-tests
/// can pin the policy.
public static class DropRespawnMath
{
    /// Fell out of the world (through a gap, off the bench into the void).
    public static bool ShouldRespawn(float y, float killZ) => y < killZ;

    /// Resting height below which an item counts as abandoned on the FLOOR.
    /// Bench/table tops sit ≈0.93 m, the workspace shelf higher still — items the
    /// player stages on any work surface must never be reclaimed mid-run (otherwise
    /// carried equipment returns to the shelves and the
    /// experiment cannot be finished). Floor litter still tidies itself.
    public const float FloorY = 0.5f;

    /// How long a dropped item lies on the FLOOR before it goes home. 25 s left the
    /// player hunting the floor for a tube they had already given up on; 8 s is long
    /// enough to bend down and pick it up yourself.
    public const float FloorReturnSeconds = 8f;

    /// Sat still, un-held, away from home long enough that the player has
    /// clearly abandoned it — AND lying at floor level. Items resting on tables,
    /// shelves or station pads stay exactly where the player put them.
    public static bool ShouldReturnHome(float distanceFromHome, float speed, bool held, float idleSeconds,
                                        float restingY,
                                        float minIdle = FloorReturnSeconds, float minDistance = 0.4f, float restSpeed = 0.05f,
                                        float floorY = FloorY)
        => !held && speed < restSpeed && idleSeconds >= minIdle && distanceFromHome > minDistance
           && restingY < floorY;

    /// Settle-freeze: a released body that has genuinely
    /// come to rest goes kinematic IN PLACE. A body left
    /// dynamic can take a physics-solver de-penetration spike on a
    /// resting beaker that crosses the break threshold — kinematic items are
    /// immune in BreakableGlassware, and a still bench costs no solver time.
    /// Grabbing re-frees the body (XRI + GrabPhysicsPolicy own that side).
    public static bool ShouldSettleFreeze(bool held, bool kinematic, float speed, float settledSeconds,
                                          float minSettle = 2.5f, float restSpeed = 0.05f)
        => !held && !kinematic && speed < restSpeed && settledSeconds >= minSettle;
}

/// Sends a dropped prop home: below kill-Z → instant respawn; resting far from
/// home and untouched for ~25 s → quiet return. Restores the shelf policy
/// (kinematic) on arrival so the shelf stays tidy. Home is captured via
/// SetHome() by the spawner (Awake doesn't fire on edit-mode AddComponent).
public class DropRespawn : MonoBehaviour
{
    [SerializeField] private float killZ = -1f;
    // (The serialized minIdleSeconds is retired: scene instances kept their own 25 s copy,
    // so the floor-return delay is now the one constant DropRespawnMath.FloorReturnSeconds.)
    [SerializeField] private float minDistance = 0.4f;

    // Serialized: the editor fix pass sets homes in edit mode and they must
    // survive into play mode.
    [SerializeField] private Vector3 _homePos;
    [SerializeField] private Quaternion _homeRot;
    [SerializeField] private bool _hasHome;
    private float _idle;
    private Rigidbody _rb;
    private XRGrab _grab;

    // Home SUPPLY (without it an exhausted bottle respawns empty, and
    // a bottle whose body renderer IS the fill visual respawns invisible).
    // Captured on the first play-mode Update, after the
    // stage builder has filled spawned pourables; GoHome() restores it so every
    // respawn is a genuinely fresh replacement (lab tour = unlimited by nature).
    private ChemicalData _homeChem;
    private float _homeMl;
    private bool _supplyCaptured;
    private LiquidPhysics _liquid;

    void Awake() { if (_rb == null) Bind(GetComponent<Rigidbody>(), GetComponent<XRGrab>()); }

    public void Bind(Rigidbody rb, XRGrab grab) { _rb = rb; _grab = grab; }

    public void SetHome(Vector3 pos, Quaternion rot) { _homePos = pos; _homeRot = rot; _hasHome = true; }

    /// While part of an apparatus assembly, respawn is the assembly's problem —
    /// a mid-stack GoHome would teleport a piece out of the formation.
    public bool Suspended { get; set; }

    public void SetKillZ(float y) => killZ = y;

    void Update()
    {
        if (Suspended || !_hasHome) return;
        if (!_supplyCaptured) CaptureSupply();
        bool held = _grab != null && _grab.isSelected;
        float speed = _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
        float dist = (transform.position - _homePos).magnitude;

        _idle = held || speed >= 0.05f ? 0f : _idle + Time.deltaTime;

        if (!held && DropRespawnMath.ShouldRespawn(transform.position.y, killZ)) { GoHome(); return; }
        if (DropRespawnMath.ShouldReturnHome(dist, speed, held, _idle, transform.position.y,
                                             DropRespawnMath.FloorReturnSeconds, minDistance)) { ReturnWithShimmer(); return; }

        // Settle-freeze: at rest for a beat → kinematic in place (immune to
        // solver spikes; next grab re-frees it).
        if (_rb != null && DropRespawnMath.ShouldSettleFreeze(held, _rb.isKinematic, speed, _idle))
            _rb.isKinematic = true;
    }

    /// Record the current contents as this item's fresh-replacement supply.
    /// Public so tests (and future builders) can capture deterministically.
    public void CaptureSupply()
    {
        if (_liquid == null) _liquid = GetComponent<LiquidPhysics>();
        _homeChem = _liquid != null ? _liquid.currentChemical : null;
        _homeMl = _liquid != null ? _liquid.currentLiquidVolume : 0f;
        _supplyCaptured = true;
    }

    /// FULL lab reset of item
    /// locations and materials, not just the player. Disassembles every snapped
    /// apparatus, then sends every loose item back to its home spot and restores
    /// its home contents. Called on Restart / abort / return-to-entrance so the
    /// bench looks untouched for the next attempt, regardless of where the player
    /// left things. Complements launcher.Launch(StageOnly), which only rebuilds
    /// the stage-SPAWNED props — hand-placed workspace apparatus needs this.
    public static void ResetAllHome()
    {
        // 1) Break apart any assembled apparatus so members are free to return.
        foreach (var snap in FindObjectsByType<ApparatusSnap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            int guard = 0;
            while (snap.DetachNewest() && guard++ < 64) { }
        }
        // 2) Every loose prop/reagent back to its home transform + home supply.
        foreach (var dr in FindObjectsByType<DropRespawn>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            dr.Suspended = false;
            dr.GoHome();
        }
        // 3) PUT EVERY FLAME OUT — a burner must never still be burning after a
        //    reset, and no match may stay lit.
        foreach (var b in FindObjectsByType<BurnerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (b != null) b.Extinguish();
        foreach (var m in FindObjectsByType<Matchstick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (m != null && m.IsLit) m.Extinguish(true);

        // 4) Clear every USED consumable (struck matchsticks, litmus strips, filter
        //    paper, cotton swabs). Their dispensers immediately lay out a fresh one,
        //    so the bench returns to a clean, fully-stocked state.
        foreach (var d in FindObjectsByType<DispensedConsumable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (d != null) Destroy(d.gameObject);
    }

    /// Teleport back to the shelf spot, re-freeze, and restore the home supply
    /// (public for tests/tools). Empty receivers reset to empty — a respawned
    /// vessel never keeps a mid-run mixture.
    /// The visible version of GoHome: a soft flash where it lay and where it reappears,
    /// so the item is seen to go home instead of silently vanishing from the floor.
    private void ReturnWithShimmer()
    {
        var tint = new Color(0.6f, 0.85f, 1f, 0.7f);
        EffectVfx.ColorFlash(transform.position, tint);
        GoHome();
        if (_hasHome) EffectVfx.ColorFlash(_homePos, tint);
    }

    public void GoHome()
    {
        if (!_hasHome) return;
        if (_rb != null)
        {
            _rb.isKinematic = true;   // back to shelf policy; next release re-frees it
        }
        transform.SetPositionAndRotation(_homePos, _homeRot);
        if (_supplyCaptured)
        {
            if (_liquid == null) _liquid = GetComponent<LiquidPhysics>();
            if (_liquid != null) _liquid.SetContents(_homeChem, _homeMl);
        }
        _idle = 0f;
    }
}
