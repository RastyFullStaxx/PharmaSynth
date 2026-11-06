using XRBase = UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using SelectMode = UnityEngine.XR.Interaction.Toolkit.Interactables.InteractableSelectMode;

/// Held-item collision policy (grabbed props must never be forced
/// through walls/floor/furniture). The ChemLab pack prefabs shipped with
/// movementType = Instantaneous, which teleports the transform to the hand each
/// frame — no physics sweep, so a held beaker passes straight through static
/// geometry. VelocityTracking drives the rigidbody with velocities instead, so
/// PhysX resolves collisions and a held item stops against the world.
///
/// Two-handed grab (hold a container in one hand and pour with
/// the other; steady the mortar and grind with the pestle — for everything).
/// selectMode = Multiple lets two interactors select the SAME item at once; XRI's
/// default General grab transformer blends the two-handed pose (midpoint position,
/// hand-to-hand rotation). Single-hand use is unchanged — it just also permits a
/// second hand. Independent dual-hand use (one object per hand) already works.
///
/// One seam applied everywhere: prefabs + scene instances (Wire Grab Collision
/// menu) and every runtime-spawned prop (ExperimentSceneBuilder).
public static class GrabTuning
{
    public const float AttachEaseSeconds = 0.15f;   // removes the snap-jerk on pickup

    /// True when the grab already has the collision-respecting + two-handed config.
    public static bool IsTuned(XRGrab grab)
        => grab != null
           && grab.movementType == XRBase.MovementType.VelocityTracking
           && grab.selectMode == SelectMode.Multiple;

    /// Apply the velocity-tracked + two-handed profile. Returns true when a change
    /// was made (false = null or already tuned), so callers/menus can count real work.
    public static bool Apply(XRGrab grab)
    {
        if (grab == null) return false;
        bool changed = !IsTuned(grab);
        grab.movementType = XRBase.MovementType.VelocityTracking;
        grab.selectMode = SelectMode.Multiple;          // allow both hands on one item
        grab.attachEaseInTime = AttachEaseSeconds;
        grab.velocityDamping = 1f;
        grab.velocityScale = 1f;
        grab.angularVelocityDamping = 1f;
        grab.angularVelocityScale = 0.95f;
        // throwOnDetach stays as authored — with velocity tracking the throw
        // velocity comes from the tracked hand, which is exactly what we want.
        return changed;
    }
}
