using UnityEngine;

/// Real-time dressing mirror using the canonical planar
/// reflection-matrix technique. Each frame a child camera is placed at the
/// player's eye REFLECTED across the mirror plane and renders the scene with an
/// oblique near plane clamped to the glass (so the wall the mirror hangs on
/// never occludes the view). Rendered by hand — URP does NOT auto-render an
/// enabled off-screen camera, which would leave the render texture black
/// — with inverted culling so the handedness-flipped reflected
/// geometry keeps facing the right way. Distance-gated for the Quest budget.
///
/// Note: a mirror can only reflect geometry that exists. The player sees the
/// surroundings; seeing THEMSELVES additionally needs a visible body/PPE avatar
/// (tracked separately in the work checklist).
public class MirrorPlane : MonoBehaviour
{
    [SerializeField] private Camera mirrorCam;       // child camera (no listener)
    [SerializeField] private Renderer surface;       // the mirror quad
    [SerializeField] private int rtWidth = 512, rtHeight = 768;
    [SerializeField] private float activeDistance = 6f;
    [SerializeField] private float clipOffset = 0.01f;
    [SerializeField] private LayerMask cullMask = ~(1 << 5);   // everything but UI (HUD)

    private RenderTexture _rt;
    private Material _mat;
    private Transform _head;

    void Start()
    {
        if (mirrorCam == null || surface == null) return;
        _rt = new RenderTexture(rtWidth, rtHeight, 24) { name = "MirrorRT" };
        mirrorCam.targetTexture = _rt;
        mirrorCam.enabled = false;                  // we drive it with Render()
        _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        _mat.mainTexture = _rt;
        if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", _rt);   // URP Unlit's real slot
        if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", Color.white);
        if (_mat.HasProperty("_Cull")) _mat.SetFloat("_Cull", 0f);   // visible both sides
        surface.material = _mat;
    }

    void OnDestroy() { if (_rt != null) _rt.Release(); }

    void LateUpdate()
    {
        if (mirrorCam == null || surface == null) return;
        if (_head == null)
        {
            var c = Camera.main;
            if (c == null) return;
            _head = c.transform;
        }
        Camera headCam = _head.GetComponent<Camera>();
        if (headCam == null) headCam = Camera.main;
        if (headCam == null) return;

        var t = surface.transform;
        Vector3 pos = t.position;
        Vector3 normal = -t.forward;                       // quad's visible face → into the room
        Vector3 toHead = _head.position - pos;

        // Gate: far away, standing behind the wall, OR turned away from the mirror
        // (it's out of view) → skip the whole reflection render. The view gate is
        // generous so the reflection is already live before you turn to face it.
        Vector3 toMirror = pos - _head.position;
        float viewDot = toMirror.sqrMagnitude > 1e-4f
            ? Vector3.Dot(headCam.transform.forward, toMirror.normalized) : 1f;
        if (!ShouldRender(toHead.magnitude, activeDistance, Vector3.Dot(toHead, normal), viewDot)) return;

        // Reflect the eye across the mirror plane and match the head's projection
        // with an oblique near plane pinned to the glass.
        float dot = -Vector3.Dot(normal, pos) - clipOffset;
        Matrix4x4 refl = Reflection(new Vector4(normal.x, normal.y, normal.z, dot));

        mirrorCam.transform.position = refl.MultiplyPoint(headCam.transform.position);
        mirrorCam.worldToCameraMatrix = headCam.worldToCameraMatrix * refl;
        Vector4 cp = CameraSpacePlane(mirrorCam.worldToCameraMatrix, pos + normal * clipOffset, normal, 1f);
        mirrorCam.projectionMatrix = headCam.CalculateObliqueMatrix(cp);
        mirrorCam.cullingMask = cullMask;

        // Render the reflection into the RT this frame (reflected geometry has
        // flipped winding, so invert culling for the duration of the render).
        GL.invertCulling = true;
        mirrorCam.Render();
        GL.invertCulling = false;
    }

    /// Pure render gate (self-tested): render only when the player is close enough,
    /// in FRONT of the glass, AND looking roughly toward it. `facingDot` = dot(toHead,
    /// normal) &gt; 0 when in front; `viewDot` = dot(viewForward, dirToMirror), ≥ -0.25
    /// keeps it live until the player has clearly turned away (~105° off-axis).
    public static bool ShouldRender(float distance, float activeDistance, float facingDot, float viewDot)
        => distance <= activeDistance && facingDot >= 0.05f && viewDot >= -0.25f;

    /// Householder reflection matrix for the plane (n.xyz, d).
    static Matrix4x4 Reflection(Vector4 p)
    {
        Matrix4x4 m = Matrix4x4.identity;
        m.m00 = 1 - 2 * p.x * p.x; m.m01 = -2 * p.x * p.y; m.m02 = -2 * p.x * p.z; m.m03 = -2 * p.w * p.x;
        m.m10 = -2 * p.y * p.x; m.m11 = 1 - 2 * p.y * p.y; m.m12 = -2 * p.y * p.z; m.m13 = -2 * p.w * p.y;
        m.m20 = -2 * p.z * p.x; m.m21 = -2 * p.z * p.y; m.m22 = 1 - 2 * p.z * p.z; m.m23 = -2 * p.w * p.z;
        return m;
    }

    /// The mirror plane expressed in the reflection camera's space (for the
    /// oblique near clip). sign = 1 keeps geometry on the room side of the glass.
    static Vector4 CameraSpacePlane(Matrix4x4 w2c, Vector3 pos, Vector3 normal, float sign)
    {
        Vector3 cpos = w2c.MultiplyPoint(pos);
        Vector3 cnrm = w2c.MultiplyVector(normal).normalized * sign;
        return new Vector4(cnrm.x, cnrm.y, cnrm.z, -Vector3.Dot(cpos, cnrm));
    }
}
