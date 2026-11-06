using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LocomotionProvider = UnityEngine.XR.Interaction.Toolkit.Locomotion.LocomotionProvider;

/// The cinematic cutscene "theatre". A separate, non-XR ShotCamera films the live lab
/// into a letterboxed RenderTexture; while active, the player's XR camera renders ONLY
/// the `Cinema` layer — a black void holding a wide screen showing that texture, plus
/// the subtitle and the fade quad. The XR camera is never moved or rotated (head
/// tracking stays the player's), the shot camera owns every frame of the picture, and
/// the screen is world-fixed where the player was looking, so nothing can be missed
/// and nothing swims. Hands, HUD and the lab vanish with the culling swap.
///
/// Enter/Exit record exactly what they changed and restore exactly that.
public class CinemaView : MonoBehaviour
{
    public const string LayerName = "Cinema";
    public const string MarkersRoot = "CutsceneMarkers";

    [SerializeField] private Camera shotCamera;
    // 2.35:1. 1280x544 read soft in the client reel; during a cinematic the XR camera
    // draws almost nothing (the dark room), so the shot camera has the budget.
    [SerializeField] private Vector2Int resolution = new Vector2Int(1600, 680);
    [SerializeField] private float screenDistance = 2.6f;
    [SerializeField] private float screenWidth = 3.4f;

    private RenderTexture _rt;
    private Transform _screen;
    private TextMeshPro _subtitle;
    private TextMeshPro _title;
    private Material _screenMat;

    // What Enter changed (restored by Exit).
    private Camera _xr;
    private int _savedMask;
    private CameraClearFlags _savedClear;
    private Color _savedBg;
    private Renderer _fadeQuad;
    private int _savedFadeLayer = -1;
    private readonly List<Behaviour> _locked = new List<Behaviour>();

    public bool Active { get; private set; }
    private static int s_active;
    /// True while any cinema (cutscene or timelapse) is showing.
    public static bool AnyActive => s_active > 0;
    public Camera ShotCamera { get { EnsureBuilt(); return shotCamera; } }
    public RenderTexture Texture { get { EnsureBuilt(); return _rt; } }
    public static int Layer => LayerMask.NameToLayer(LayerName);
    public const float PharmeeAimLift = 0.3f;

    /// True when the shot camera renders flat (not as an XR eye) — suite-pinned.
    public bool ShotCameraIsFlat
    {
        get
        {
            var d = ShotCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            return d != null && !d.allowXRRendering;
        }
    }

    /// Mask for the shot camera: everything but the theatre itself, the player's own
    /// avatar and screen-space UI.
    public static int ShotMask()
    {
        int m = ~0;
        foreach (var n in new[] { LayerName, "PlayerAvatar", "UI" })
        {
            int l = LayerMask.NameToLayer(n);
            if (l >= 0) m &= ~(1 << l);
        }
        return m;
    }

    /// Yaw-only screen placement `distance` ahead of the head, at eye height.
    public static void ScreenPose(Vector3 head, Vector3 headForward, float distance,
                                  out Vector3 pos, out Quaternion rot)
    {
        Vector3 f = headForward; f.y = 0f;
        if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
        f.Normalize();
        pos = head + f * distance;
        rot = Quaternion.LookRotation(f, Vector3.up);   // quad's -Z faces the viewer → +Z points away
    }

    private void EnsureBuilt()
    {
        int layer = Layer;
        if (_rt == null)
        {
            _rt = new RenderTexture(resolution.x, resolution.y, 24) { name = "CinemaRT", hideFlags = HideFlags.DontSave };
            _rt.Create();
        }
        if (shotCamera == null)
        {
            var cg = new GameObject("ShotCamera") { hideFlags = HideFlags.DontSave };
            cg.transform.SetParent(transform, false);
            shotCamera = cg.AddComponent<Camera>();
            // A bare URP camera has post-processing OFF: opt in so the cutscene
            // looks like the lab the player sees (DevCapture learned this too).
            var data = cg.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            data.allowXRRendering = false;   // a flat camera: never stereo, never head-tracked
        }
        shotCamera.targetTexture = _rt;
        shotCamera.cullingMask = ShotMask();
        shotCamera.nearClipPlane = 0.05f;
        if (!Active) shotCamera.enabled = false;

        if (_screen == null)
        {
            var sg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sg.name = "CinemaScreen"; sg.hideFlags = HideFlags.DontSave;
            DestroyImmediate(sg.GetComponent<Collider>());
            sg.transform.SetParent(transform, false);
            float h = screenWidth * resolution.y / resolution.x;
            sg.transform.localScale = new Vector3(screenWidth, h, 1f);
            _screenMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { hideFlags = HideFlags.DontSave };
            _screenMat.SetTexture("_BaseMap", _rt);
            _screenMat.mainTexture = _rt;
            var r = sg.GetComponent<Renderer>();
            r.sharedMaterial = _screenMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _screen = sg.transform;

            var tg = new GameObject("CinemaSubtitle") { hideFlags = HideFlags.DontSave };
            tg.transform.SetParent(_screen, false);
            tg.transform.localPosition = new Vector3(0f, -0.62f, -0.01f);
            tg.transform.localScale = new Vector3(1f / screenWidth, 1f / h, 1f);   // undo the quad's stretch
            _subtitle = tg.AddComponent<TextMeshPro>();
            _subtitle.alignment = TextAlignmentOptions.Center;
            _subtitle.fontSize = 1.4f;
            _subtitle.rectTransform.sizeDelta = new Vector2(screenWidth * 0.9f, 0.4f);
            _subtitle.text = "";

            // Title card ("Preparations Complete / Proceed to the experiment") over the
            // opening wide shot — the slides' card, without a button to press.
            var cg2 = new GameObject("CinemaTitle") { hideFlags = HideFlags.DontSave };
            cg2.transform.SetParent(_screen, false);
            cg2.transform.localPosition = new Vector3(0f, 0.05f, -0.01f);
            cg2.transform.localScale = new Vector3(1f / screenWidth, 1f / h, 1f);
            _title = cg2.AddComponent<TextMeshPro>();
            _title.alignment = TextAlignmentOptions.Center;
            _title.fontSize = 2.2f;   // 3.2 wrapped "Experiment Complete" onto two lines (autopilot shot)
            _title.fontStyle = FontStyles.Bold;
            _title.rectTransform.sizeDelta = new Vector2(screenWidth * 0.9f, 1.2f);
            _title.text = "";
            _screen.gameObject.SetActive(false);
        }
        if (layer >= 0) { _screen.gameObject.layer = layer; _subtitle.gameObject.layer = layer; _title.gameObject.layer = layer; }
    }

    /// Fade must already be black (the director wraps this in ScreenFader).
    public void Enter(Camera xr)
    {
        if (Active || xr == null) return;
        int layer = Layer;
        if (layer < 0) { Debug.LogError("[CinemaView] the 'Cinema' layer is missing (Tags & Layers)."); return; }
        EnsureBuilt();
        _xr = xr;
        _savedMask = xr.cullingMask; _savedClear = xr.clearFlags; _savedBg = xr.backgroundColor;
        xr.cullingMask = 1 << layer;
        xr.clearFlags = CameraClearFlags.SolidColor;
        xr.backgroundColor = Color.black;

        var fader = ScreenFader.Instance;
        _fadeQuad = fader != null ? fader.FadeRenderer : null;
        if (_fadeQuad != null) { _savedFadeLayer = _fadeQuad.gameObject.layer; _fadeQuad.gameObject.layer = layer; }

        _locked.Clear();
        foreach (var p in FindObjectsByType<LocomotionProvider>())
            if (p.enabled) { p.enabled = false; _locked.Add(p); }

        ScreenPose(xr.transform.position, xr.transform.forward, screenDistance, out var sp, out var sr);
        _screen.SetPositionAndRotation(sp, sr);
        _screen.gameObject.SetActive(true);
        _subtitle.text = "";
        _title.text = "";
        shotCamera.enabled = true;
        // Pharmee, Dr. Jimenez's bubble and every label turn to the shot camera — left
        // on the player's head they show the camera their backs (mirrored text).
        FaceCamera.Override = shotCamera.transform;
        Active = true;
        s_active++;
    }

    public void Exit()
    {
        if (!Active) return;
        Active = false;
        s_active = Mathf.Max(0, s_active - 1);
        if (shotCamera != null && FaceCamera.Override == shotCamera.transform) FaceCamera.Override = null;
        if (_xr != null)
        {
            _xr.cullingMask = _savedMask; _xr.clearFlags = _savedClear; _xr.backgroundColor = _savedBg;
        }
        if (_fadeQuad != null && _savedFadeLayer >= 0) _fadeQuad.gameObject.layer = _savedFadeLayer;
        _fadeQuad = null; _savedFadeLayer = -1;
        foreach (var b in _locked) if (b != null) b.enabled = true;
        _locked.Clear();
        if (_screen != null) _screen.gameObject.SetActive(false);
        if (_screenMat != null) { _screenMat.SetColor("_BaseColor", Color.white); _screenMat.color = Color.white; }
        if (_title != null) _title.text = "";
        if (shotCamera != null) shotCamera.enabled = false;
        _xr = null;
    }

    public void SetSubtitle(string text) { if (_subtitle != null) _subtitle.text = text ?? ""; }

    /// Multiplies the picture (the timelapse's day/night). Exit resets it to white.
    public void SetTint(Color c)
    {
        EnsureBuilt();
        _screenMat.SetColor("_BaseColor", c);
        _screenMat.color = c;
    }
    public Color Tint => _screenMat != null ? _screenMat.color : Color.white;

    public void SetTitle(string title, string line)
    {
        EnsureBuilt();
        _title.text = string.IsNullOrEmpty(title) ? ""
            : GlyphSafe.Sanitize(title) + (string.IsNullOrEmpty(line) ? "" : "\n<size=45%>" + GlyphSafe.Sanitize(line) + "</size>");
    }
    public string TitleText => _title != null ? _title.text : "";

    public void SetPose(ShotMath.Pose p, float fov)
    {
        EnsureBuilt();
        shotCamera.transform.SetPositionAndRotation(p.pos, p.rot);
        shotCamera.fieldOfView = fov;
    }

    private void OnDisable() => Exit();

    private void OnDestroy()
    {
        Exit();
        if (_rt != null) { _rt.Release(); DestroyImmediate(_rt); }
        if (_screenMat != null) DestroyImmediate(_screenMat);
    }

    // ── Marker + target lookup ──────────────────────────────────────────────

    private Transform _markers;
    public Transform Markers
    {
        get
        {
            if (_markers == null) { var g = GameObject.Find(MarkersRoot); if (g != null) _markers = g.transform; }
            return _markers;
        }
    }

    public Transform Marker(string name)
    {
        if (string.IsNullOrEmpty(name) || Markers == null) return null;
        return Markers.Find(name);
    }

    public ShotMath.Pose? MarkerPose(string name)
    {
        var t = Marker(name);
        return t != null ? new ShotMath.Pose(t.position, t.rotation) : (ShotMath.Pose?)null;
    }

    /// Where `look` points right now. Product = the fullest non-shelf vessel holding the
    /// running module's product (the player's own synthesis); falls back to the bench.
    public Vector3? Resolve(CinemaLook look, string lookMarker, string moduleId, Transform pharmee)
    {
        switch (look)
        {
            // Aim between her body and her speech bubble (1.1-1.4 m above her pivot), so a
            // close-up keeps the bubble in frame. Constant, not the live bubble bounds:
            // the bubble only exists while she talks and the aim would jump.
            case CinemaLook.Pharmee: return pharmee != null ? VisualCentre(pharmee) + Vector3.up * PharmeeAimLift : (Vector3?)null;
            case CinemaLook.Marker: { var t = Marker(lookMarker); return t != null ? t.position : (Vector3?)null; }
            case CinemaLook.Product:
            {
                var v = FindProduct(DemoMode.ProductFor(moduleId));
                if (v != null) return v.position;
                goto case CinemaLook.Bench;
            }
            case CinemaLook.Bench: { var t = Marker("Marker_Bench"); return t != null ? t.position : (Vector3?)null; }
            default: return null;
        }
    }

    /// The middle of what you SEE (mesh bounds), not the pivot — Pharmee's pivot sits
    /// well below her head, so aiming at it cropped her out of the top of the frame.
    public static Vector3 VisualCentre(Transform t)
    {
        bool any = false; Bounds b = default;
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
            if (r.GetComponent<TMP_Text>() != null) continue;   // the speech bubble is a MeshRenderer too
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b.center : t.position;
    }

    /// Pure: does a vessel holding these chemicals count as the product? Shelf stock
    /// (`Raw_*` / `Reagent_*`) never does — that's a bottle, not the player's synthesis.
    public static bool HoldsProduct(string objectName, string liquid, string precipitate, string product)
    {
        if (string.IsNullOrEmpty(product) || objectName == null) return false;
        if (LabReset.IsReagent(objectName)) return false;
        return string.Equals(liquid, product, System.StringComparison.OrdinalIgnoreCase)
            || string.Equals(precipitate, product, System.StringComparison.OrdinalIgnoreCase);
    }

    /// The liquid may live on a child mesh: report a shelf ancestor's name if there is one.
    private static string StockName(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
            if (LabReset.IsReagent(p.name)) return p.name;
        return t.name;
    }

    public static Transform FindProduct(string product)
    {
        if (string.IsNullOrEmpty(product)) return null;
        Transform best = null; float most = 0f;
        foreach (var lp in FindObjectsByType<LiquidPhysics>())
        {
            float ml = lp.currentLiquidVolume + lp.currentPptVolume;
            if (ml <= most) continue;
            if (!HoldsProduct(StockName(lp.transform),
                    lp.currentChemical != null ? lp.currentChemical.chemicalName : null,
                    lp.currentPptChemical != null ? lp.currentPptChemical.chemicalName : null, product)) continue;
            best = lp.transform; most = ml;
        }
        return best;
    }
}
