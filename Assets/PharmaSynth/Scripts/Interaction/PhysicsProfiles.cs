using System.Collections.Generic;
using UnityEngine;

/// How an item plausibly rests on a bench when nothing holds it.
public enum RestPose
{
    Upright,      // stable base: beakers, flasks, burners, racks, stands
    LieLongAxis,  // long thin item lies on its side: glass rod, test tube, tongs
    Flat,         // flat tool rests on its face: watch glass, wire gauze, iron ring
}

public struct PhysicsProfile
{
    public float massKg;
    public RestPose pose;
    public PhysicsProfile(float mass, RestPose p) { massKg = mass; pose = p; }
}

/// Physics-attribute table for the ChemLab prefabs — the companion to RealSizes
/// (used by the physics/resting-pose audit tool). Name → realistic mass + plausible
/// resting pose, plus the pure math that turns a pose into a rotation and the
/// guards the audit tool uses (degenerate colliders, resting plausibility).
/// Policy: items spawn KINEMATIC on the shelf; GrabPhysicsPolicy flips them
/// dynamic on first release so a dropped glass rod falls and lies on its side
/// instead of freezing mid-air or balancing upright.
public static class PhysicsProfiles
{
    private static readonly Dictionary<string, PhysicsProfile> Table = new Dictionary<string, PhysicsProfile>
    {
        { "AlcoholBurner",                    new PhysicsProfile(0.25f,  RestPose.Upright) },
        { "Aspirator",                        new PhysicsProfile(0.12f,  RestPose.LieLongAxis) },
        { "Balance",                          new PhysicsProfile(4.00f,  RestPose.Upright) },
        { "Beaker_100mL",                     new PhysicsProfile(0.10f,  RestPose.Upright) },
        { "Beaker_100mL_WithLiquid",          new PhysicsProfile(0.18f,  RestPose.Upright) },
        { "Beaker_500mL",                     new PhysicsProfile(0.28f,  RestPose.Upright) },
        { "Beaker_500mL_WithLiquid",          new PhysicsProfile(0.60f,  RestPose.Upright) },
        { "BunsenBurner",                     new PhysicsProfile(1.20f,  RestPose.Upright) },
        { "ClayTriangle",                     new PhysicsProfile(0.04f,  RestPose.Flat) },
        { "Condenser",                        new PhysicsProfile(0.30f,  RestPose.LieLongAxis) },
        { "Crucible",                         new PhysicsProfile(0.06f,  RestPose.Upright) },
        { "CrucibleTongs",                    new PhysicsProfile(0.15f,  RestPose.LieLongAxis) },
        { "DeliveryTube",                     new PhysicsProfile(0.03f,  RestPose.LieLongAxis) },
        { "Dropper",                          new PhysicsProfile(0.015f, RestPose.LieLongAxis) },
        { "DistillingFlask",                  new PhysicsProfile(0.16f,  RestPose.Upright) },   // glTFast model
        { "ErlenmeyerFlask_400mL",            new PhysicsProfile(0.18f,  RestPose.Upright) },
        { "ErlenmeyerFlask_400mL_WithLiquid", new PhysicsProfile(0.45f,  RestPose.Upright) },
        { "EvaporatingDish",                  new PhysicsProfile(0.09f,  RestPose.Upright) },
        { "FlorenceFlask",                    new PhysicsProfile(0.15f,  RestPose.Upright) },
        { "Forceps",                          new PhysicsProfile(0.03f,  RestPose.LieLongAxis) },
        { "Funnel",                           new PhysicsProfile(0.08f,  RestPose.LieLongAxis) },   // can't stand on its stem
        { "GlassRod",                         new PhysicsProfile(0.03f,  RestPose.LieLongAxis) },
        { "GraduatedCylinder_50mL",           new PhysicsProfile(0.14f,  RestPose.Upright) },
        { "GraduatedCylinder_50mL_WithLiquid",new PhysicsProfile(0.19f,  RestPose.Upright) },
        { "IronRing",                         new PhysicsProfile(0.20f,  RestPose.Flat) },
        { "Motar",                            new PhysicsProfile(0.60f,  RestPose.Upright) },
        { "Pestle",                           new PhysicsProfile(0.15f,  RestPose.LieLongAxis) },
        { "RetortStand",                      new PhysicsProfile(2.50f,  RestPose.Upright) },
        { "RubberStopper",                    new PhysicsProfile(0.02f,  RestPose.Upright) },
        { "Scoopula",                         new PhysicsProfile(0.04f,  RestPose.LieLongAxis) },
        { "Spatula",                          new PhysicsProfile(0.04f,  RestPose.LieLongAxis) },
        { "TestTube",                         new PhysicsProfile(0.025f, RestPose.LieLongAxis) },
        { "TestTube_WithLiquid",              new PhysicsProfile(0.045f, RestPose.LieLongAxis) },
        { "TestTubeBrush",                    new PhysicsProfile(0.03f,  RestPose.LieLongAxis) },
        { "TestTubeHolder_Metal",             new PhysicsProfile(0.08f,  RestPose.LieLongAxis) },
        { "TestTubeHolder_Wooden",            new PhysicsProfile(0.09f,  RestPose.LieLongAxis) },
        { "TestTubeRack",                     new PhysicsProfile(0.35f,  RestPose.Upright) },
        { "TestTubeRack_12Tubes",             new PhysicsProfile(0.40f,  RestPose.Upright) },
        { "TestTubeRack_WithDryingPins",      new PhysicsProfile(0.45f,  RestPose.Upright) },
        { "Thermometer",                      new PhysicsProfile(0.03f,  RestPose.LieLongAxis) },
        { "Tripod",                           new PhysicsProfile(0.70f,  RestPose.Upright) },
        { "UtilityClamp",                     new PhysicsProfile(0.25f,  RestPose.LieLongAxis) },
        { "WaterBath",                        new PhysicsProfile(0.80f,  RestPose.Upright) },      // shallow pan
        { "Vial",                             new PhysicsProfile(0.03f,  RestPose.Upright) },
        { "Vial_Brown",                       new PhysicsProfile(0.03f,  RestPose.Upright) },
        { "Vial_Brown_WithLabel",             new PhysicsProfile(0.03f,  RestPose.Upright) },
        { "Vial_WithLabel",                   new PhysicsProfile(0.03f,  RestPose.Upright) },
        { "WashBottle",                       new PhysicsProfile(0.12f,  RestPose.Upright) },
        { "WashBottle_WithLabel",             new PhysicsProfile(0.12f,  RestPose.Upright) },
        { "WatchGlass",                       new PhysicsProfile(0.05f,  RestPose.Flat) },
        { "WireGauze",                        new PhysicsProfile(0.06f,  RestPose.Flat) },
    };

    public static int Count => Table.Count;
    public static IEnumerable<string> Names => Table.Keys;

    public static bool TryGet(string prefabName, out PhysicsProfile profile)
        => Table.TryGetValue(prefabName ?? "", out profile);

    // ---- pure pose math ----------------------------------------------------

    static int Longest(Vector3 s) => s.x >= s.y ? (s.x >= s.z ? 0 : 2) : (s.y >= s.z ? 1 : 2);
    static int Shortest(Vector3 s) => s.x <= s.y ? (s.x <= s.z ? 0 : 2) : (s.y <= s.z ? 1 : 2);

    /// Rotation (applied on top of the current orientation) that puts an item
    /// into its resting pose, given the bounds size of the UNROTATED item:
    /// LieLongAxis brings the longest axis horizontal, Flat brings the
    /// shortest axis vertical, Upright keeps the authored orientation.
    public static Quaternion RestRotation(RestPose pose, Vector3 boundsSize)
    {
        switch (pose)
        {
            case RestPose.LieLongAxis:
                return Longest(boundsSize) == 1 ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
            case RestPose.Flat:
                switch (Shortest(boundsSize))
                {
                    case 0: return Quaternion.Euler(0f, 0f, 90f);   // X → vertical
                    case 2: return Quaternion.Euler(90f, 0f, 0f);   // Z → vertical
                    default: return Quaternion.identity;            // already face-down
                }
            default:
                return Quaternion.identity;
        }
    }

    /// True when a WORLD bounds size is consistent with the pose — the audit's
    /// post-drop check ("a glass rod lies on its side, never balances upright").
    public static bool IsRestingPlausible(RestPose pose, Vector3 worldSize)
    {
        switch (pose)
        {
            case RestPose.LieLongAxis:
            {
                float longest = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
                return worldSize.y < longest * 0.95f;               // longest axis is NOT vertical
            }
            case RestPose.Flat:
            {
                float shortest = Mathf.Min(worldSize.x, Mathf.Min(worldSize.y, worldSize.z));
                return worldSize.y < shortest * 1.5f;               // thinnest axis IS (near) vertical
            }
            default:
                return true;                                        // upright items keep authored pose
        }
    }

    /// Degenerate collider guard: any world dimension thinner than minDim
    /// (default 5 mm) tunnels through geometry at Quest fixed-step speeds.
    public static bool IsDegenerate(Vector3 colliderWorldSize, float minDim = 0.005f)
        => colliderWorldSize.x < minDim || colliderWorldSize.y < minDim || colliderWorldSize.z < minDim;

    // ---- application seam (edit-mode safe: no OnEnable/Awake dependence) ----

    /// Ensure go has a Rigidbody with profile mass + shelf policy (kinematic,
    /// gravity on for when the policy releases it) and a non-degenerate collider
    /// (adds a bounds-fitted BoxCollider when the prefab imported without one).
    /// Returns the Rigidbody, or null when go is null.
    public static Rigidbody EnsurePhysics(GameObject go, string prefabName)
    {
        if (go == null) return null;
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        if (TryGet(prefabName, out var p)) rb.mass = p.massKg;
        rb.isKinematic = true;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        ConvexifyMeshColliders(go);
        EnsureCollider(go);
        return rb;
    }

    /// PhysX rejects concave MeshColliders on dynamic rigidbodies (the item then
    /// has NO collision and falls through the world — drop-test finding). Any
    /// item that hosts a Rigidbody must use convex hulls.
    public static int ConvexifyMeshColliders(GameObject go)
    {
        int n = 0;
        foreach (var mc in go.GetComponentsInChildren<MeshCollider>())
            if (!mc.convex) { mc.convex = true; n++; }
        return n;
    }

    /// Add a renderer-bounds BoxCollider when the item has no collider at all
    /// (FBX imports arrive collider-less) OR only degenerate ones (flat tools
    /// like wire gauze ship with paper-thin colliders that tunnel), padding
    /// every axis to at least 6 mm. Idempotent: a non-degenerate collider
    /// anywhere on the item satisfies the check.
    public static Collider EnsureCollider(GameObject go)
    {
        if (go == null) return null;
        foreach (var c in go.GetComponentsInChildren<Collider>())
            if (!IsDegenerate(c.bounds.size)) return c;

        var rs = go.GetComponentsInChildren<Renderer>();
        Bounds wb = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.one * 0.05f);
        for (int i = 1; i < rs.Length; i++) wb.Encapsulate(rs[i].bounds);

        var box = go.AddComponent<BoxCollider>();
        var ls = go.transform.lossyScale;
        Vector3 size = new Vector3(
            Mathf.Max(wb.size.x / Mathf.Max(ls.x, 1e-4f), 0.006f),
            Mathf.Max(wb.size.y / Mathf.Max(ls.y, 1e-4f), 0.006f),
            Mathf.Max(wb.size.z / Mathf.Max(ls.z, 1e-4f), 0.006f));
        box.size = size;
        box.center = go.transform.InverseTransformPoint(wb.center);
        return box;
    }
}
