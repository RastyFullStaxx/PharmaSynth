using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// The hand's three display poses: FREE (open), GRAB (holding
/// something), POINT (index extended while the ray hovers an interactable —
/// Pharmee, tools, reagents, buttons).
public enum HandPoseKind { Free, Grab, Point }

/// Pure hand display policy (edit-mode testable).
public static class HandPosePolicy
{
    /// Grab wins over Point wins over Free.
    public static HandPoseKind PoseFor(bool selecting, bool hovering)
        => selecting ? HandPoseKind.Grab : hovering ? HandPoseKind.Point : HandPoseKind.Free;

    /// Nitrile material shows exactly while the PPE gloves are worn.
    public static bool Nitrile(bool glovesWorn) => glovesWorn;

    /// Curl angles (degrees, around the knuckle axis) at full pose weight.
    public const float ProximalCurl = 55f;
    public const float IntermediateCurl = 50f;
    public const float DistalCurl = 30f;
    public const float ThumbProximalCurl = 22f;
    public const float ThumbDistalCurl = 25f;
    public const float PoseDegreesPerSecond = 600f;   // fast, readable up close

    /// Target angle for one joint in a pose. segment: 0=proximal 1=intermediate 2=distal.
    public static float AngleFor(HandPoseKind pose, bool isThumb, bool isIndex, int segment)
    {
        if (pose == HandPoseKind.Free) return 0f;
        if (pose == HandPoseKind.Point && isIndex) return 0f;   // the pointing finger stays straight
        if (isThumb) return segment == 0 ? ThumbProximalCurl : ThumbDistalCurl;
        switch (segment)
        {
            case 0: return ProximalCurl;
            case 1: return IntermediateCurl;
            default: return DistalCurl;
        }
    }
}

/// Runtime driver on each controller: shows the skinned hand (XR Hands sample
/// mesh), keeps the default controller model hidden, poses the fingers
/// (Free / Grab while selecting / Point while hovering an interactable), and
/// swaps the material bare<->nitrile from the PPE gloves state.
/// Wired by Tools ▸ PharmaSynth ▸ Build Hand Visuals.
public class HandPoseController : MonoBehaviour
{
    [SerializeField] private Transform handRoot;              // HandVisual_L/R (FBX instance root)
    [SerializeField] private GameObject controllerVisual;     // default controller model (kept hidden)
    [SerializeField] private SkinnedMeshRenderer handRenderer;
    [SerializeField] private Material skinMaterial;
    [SerializeField] private Material nitrileMaterial;
    [SerializeField] private PPEController ppe;               // optional (menu room has none)
    [SerializeField] private XRBaseInteractor[] interactors;  // this hand's interactors

    private struct Joint
    {
        public Transform t;
        public Quaternion open;
        public bool thumb;
        public bool index;
        public int segment;
        public float angle;   // current smoothed angle
    }

    private readonly List<Joint> _joints = new List<Joint>();
    private bool _cached;

    public void Bind(Transform hand, GameObject ctrlVisual, SkinnedMeshRenderer smr,
                     Material skin, Material nitrile, PPEController ppeCtrl, XRBaseInteractor[] hands)
    {
        handRoot = hand; controllerVisual = ctrlVisual; handRenderer = smr;
        skinMaterial = skin; nitrileMaterial = nitrile; ppe = ppeCtrl; interactors = hands;
        _cached = false;
    }

    private void EnsureJoints()
    {
        if (_cached || handRoot == null) return;
        _joints.Clear();
        foreach (var t in handRoot.GetComponentsInChildren<Transform>(true))
        {
            string n = t.name;
            int segment = n.EndsWith("Proximal") ? 0 : n.EndsWith("Intermediate") ? 1 : n.EndsWith("Distal") ? 2 : -1;
            if (segment < 0) continue;
            _joints.Add(new Joint
            {
                t = t,
                open = t.localRotation,
                thumb = n.Contains("Thumb"),
                index = n.Contains("Index"),
                segment = segment,
                angle = 0f
            });
        }
        _cached = true;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;

        // The hand replaces the controller model — keep it that way.
        if (controllerVisual != null && controllerVisual.activeSelf)
            controllerVisual.SetActive(false);
        if (handRoot != null && !handRoot.gameObject.activeSelf)
            handRoot.gameObject.SetActive(true);

        // Skin: nitrile while the PPE gloves are worn.
        if (handRenderer != null && skinMaterial != null && nitrileMaterial != null)
        {
            var want = HandPosePolicy.Nitrile(ppe != null && ppe.IsWorn(PPEPiece.Gloves))
                ? nitrileMaterial : skinMaterial;
            if (handRenderer.sharedMaterial != want) handRenderer.sharedMaterial = want;
        }

        // Pose: Grab while selecting, Point while hovering an interactable OR a
        // UI element (menu/HUD buttons count), else Free.
        bool selecting = false, hovering = false;
        if (interactors != null)
            for (int i = 0; i < interactors.Length; i++)
            {
                var it = interactors[i];
                if (it == null) continue;
                selecting |= it.hasSelection;
                hovering |= it.hasHover;
                if (!hovering && it is UnityEngine.XR.Interaction.Toolkit.UI.IUIInteractor ui
                    && ui.TryGetUIModel(out var uiModel))
                    hovering |= uiModel.currentRaycast.isValid;   // pointing at a button/panel
            }
        var pose = HandPosePolicy.PoseFor(selecting, hovering);

        EnsureJoints();
        float step = HandPosePolicy.PoseDegreesPerSecond * Time.deltaTime;
        bool changed = false;
        for (int i = 0; i < _joints.Count; i++)
        {
            var j = _joints[i];
            float target = HandPosePolicy.AngleFor(pose, j.thumb, j.index, j.segment);
            if (Mathf.Approximately(j.angle, target)) continue;
            j.angle = Mathf.MoveTowards(j.angle, target, step);
            _joints[i] = j;
            changed = true;
        }
        if (changed) ApplyPose();
    }

    private void ApplyPose()
    {
        for (int i = 0; i < _joints.Count; i++)
        {
            var j = _joints[i];
            if (j.t == null) continue;
            j.t.localRotation = j.open * Quaternion.AngleAxis(j.angle, Vector3.right);
        }
    }
}
