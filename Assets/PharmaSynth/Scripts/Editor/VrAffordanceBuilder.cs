#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NearFar = UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor;
using Poke = UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using HapticPlayer = UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer;
using SHF = UnityEngine.XR.Interaction.Toolkit.Feedback.SimpleHapticFeedback;

/// Wires the VR affordance batch into the open scene:
///   1. HAPTICS — every hand interactor (NearFar + Poke) gets a HapticImpulsePlayer
///      + SimpleHapticFeedback so grabbing, socket-snapping and poking UI buzz.
///   2. HOVER HIGHLIGHT — every grabbable gets a HoverHighlight so it brightens +
///      pops when a hand/ray hovers it (small real-scale tools are easy to find).
///   3. SOCKET GHOST — every station socket shows a translucent preview of the
///      correct item snapped in place.
/// (Runtime-spawned experiment props/sockets get 2 & 3 from ExperimentSceneBuilder;
///  haptics is interactor-side so it covers everything automatically.)
///
/// Tools ▸ PharmaSynth ▸ Wire VR Affordances (edit mode, idempotent).
public static class VrAffordanceBuilder
{
    [MenuItem("Tools/PharmaSynth/Wire VR Affordances")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VrAffordance] exit Play mode first."); return; }

        int haptics = WireHaptics();
        int hover = WireHoverHighlight();
        int sockets = WireSocketGhosts();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        Debug.Log($"<color=#4CD07D>[VrAffordance] haptics on {haptics} interactor(s), hover-highlight on {hover} grabbable(s), ghost preview on {sockets} socket(s)</color>");
    }

    static int WireHaptics()
    {
        var interactors = new List<Component>();
        interactors.AddRange(Object.FindObjectsByType<NearFar>(FindObjectsInactive.Include));
        interactors.AddRange(Object.FindObjectsByType<Poke>(FindObjectsInactive.Include));
        int n = 0;
        foreach (var it in interactors)
        {
            var go = it.gameObject;
            var player = go.GetComponent<HapticPlayer>() ?? go.AddComponent<HapticPlayer>();
            var shf = go.GetComponent<SHF>() ?? go.AddComponent<SHF>();
            var so = new SerializedObject(shf);
            SetRef(so, "m_InteractorSourceObject", it);
            SetRef(so, "m_HapticImpulsePlayer", player);
            SetBool(so, "m_PlaySelectEntered", true);
            SetF(so, "m_SelectEnteredData.m_Amplitude", 0.5f);
            SetF(so, "m_SelectEnteredData.m_Duration", 0.1f);
            SetBool(so, "m_PlayHoverEntered", true);
            SetF(so, "m_HoverEnteredData.m_Amplitude", 0.15f);
            SetF(so, "m_HoverEnteredData.m_Duration", 0.05f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shf);
            n++;
        }
        return n;
    }

    static int WireHoverHighlight()
    {
        int n = 0;
        foreach (var grab in Object.FindObjectsByType<XRGrab>(FindObjectsInactive.Include))
        {
            var hh = grab.GetComponent<HoverHighlight>() ?? grab.gameObject.AddComponent<HoverHighlight>();
            hh.Bind(grab);
            EditorUtility.SetDirty(hh);
            n++;
        }
        return n;
    }

    static int WireSocketGhosts()
    {
        var mat = GhostMaterial();
        int n = 0;
        foreach (var s in Object.FindObjectsByType<XRSocket>(FindObjectsInactive.Include))
        {
            s.showInteractableHoverMeshes = true;
            s.interactableHoverMeshMaterial = mat;
            EditorUtility.SetDirty(s);
            n++;
        }
        return n;
    }

    /// Load-or-create a persisted translucent-cyan ghost material asset.
    static Material GhostMaterial()
    {
        const string path = "Assets/PharmaSynth/Art/Materials/SocketGhost.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        System.IO.Directory.CreateDirectory("Assets/PharmaSynth/Art/Materials");
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        m = new Material(sh) { name = "SocketGhost" };
        var c = new Color(0.5f, 0.9f, 1f, 0.3f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        return m;
    }

    static void SetRef(SerializedObject so, string path, Object value)
    { var p = so.FindProperty(path); if (p != null) p.objectReferenceValue = value; }
    static void SetBool(SerializedObject so, string path, bool value)
    { var p = so.FindProperty(path); if (p != null) p.boolValue = value; }
    static void SetF(SerializedObject so, string path, float value)
    { var p = so.FindProperty(path); if (p != null) p.floatValue = value; }
}
#endif
