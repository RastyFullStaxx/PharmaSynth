using System;
using UnityEngine;

/// Pure fade math: eased alpha ramp between targets. Edit-mode testable.
public class FadeState
{
    private float _from, _to, _duration, _t;

    public float Alpha { get; private set; }
    public bool Busy { get; private set; }

    public void Begin(float toAlpha, float seconds)
    {
        _from = Alpha;
        _to = Mathf.Clamp01(toAlpha);
        _duration = Mathf.Max(0.01f, seconds);
        _t = 0f;
        Busy = true;
    }

    /// Advance by dt; returns the current alpha. Clears Busy on arrival.
    public float Step(float dt)
    {
        if (!Busy) return Alpha;
        _t += Mathf.Max(0f, dt);
        float k = Mathf.Clamp01(_t / _duration);
        Alpha = Mathf.Lerp(_from, _to, Ease01(k));
        if (k >= 1f) { Alpha = _to; Busy = false; }
        return Alpha;
    }

    /// Smoothstep 3t²−2t³.
    public static float Ease01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }

    /// 0→peak→0 warning-pulse alpha (fast attack, slow release). Pure for tests.
    public static float Pulse01(float t01, float peak)
    {
        t01 = Mathf.Clamp01(t01);
        float k = t01 < 0.35f ? Ease01(t01 / 0.35f) : 1f - Ease01((t01 - 0.35f) / 0.65f);
        return Mathf.Clamp01(peak) * k;
    }
}

/// VR-safe screen fade: a black quad floating just in front of the camera (a
/// screen-space canvas is unreliable in the XR compositor). One instance per
/// scene, parented under the active camera. Used for teleports + scene loads.
public class ScreenFader : MonoBehaviour
{
    public static ScreenFader Instance { get; private set; }

    [SerializeField] private Renderer quad;              // auto-built under Camera.main if null
    [SerializeField] private bool fadeInOnStart = true;
    [SerializeField] private float defaultSeconds = 0.4f;

    private readonly FadeState _state = new FadeState();
    private Action _onDone;
    private Material _mat;

    public FadeState State => _state;

    /// The black quad (built lazily under the XR camera). The cinema view moves it
    /// onto its own layer while a cutscene culls everything else.
    public Renderer FadeRenderer { get { EnsureQuad(); return quad; } }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        EnsureQuad();
        if (fadeInOnStart)
        {
            SetAlpha(1f);
            FadeIn();
        }
        else SetAlpha(0f);
    }

    private void EnsureQuad()
    {
        if (quad != null) { _mat = quad.material; return; }
        var cam = Camera.main;
        if (cam == null) return;
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "FadeQuad";
        UnityEngine.Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(cam.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, 0.5f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(2f, 2f, 1f);
        quad = go.GetComponent<Renderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        _mat = new Material(shader);
        _mat.SetFloat("_Surface", 1f);                    // transparent
        _mat.SetFloat("_Blend", 0f);                      // alpha blend
        _mat.SetOverrideTag("RenderType", "Transparent");
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_ZWrite", 0);
        _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        _mat.renderQueue = 4600;                          // over everything, incl. overlays
        _mat.color = new Color(0f, 0f, 0f, 0f);
        quad.sharedMaterial = _mat;
        quad.enabled = false;
    }

    private void SetAlpha(float a)
    {
        // Drive the pure state to the value so subsequent fades start from it.
        _state.Begin(a, 0.01f);
        _state.Step(1f);
        Apply(a);
    }

    private void Apply(float a)
    {
        if (quad == null || _mat == null) return;
        var c = _mat.color; c.a = a; _mat.color = c;
        quad.enabled = a > 0.001f;
    }

    private void Update()
    {
        if (!_state.Busy) return;
        Apply(_state.Step(Time.deltaTime));
        if (!_state.Busy && _onDone != null)
        {
            var cb = _onDone; _onDone = null;
            cb();
        }
    }

    private bool _pulsing;

    /// Brief tinted vignette pulse for player-adjacent hazards (fire/spatter —
    /// the manuscript's "visual alert"). Never fights a real fade: skipped while
    /// one runs, aborted if one starts.
    public void PulseWarning(Color tint, float seconds = 0.9f, float peak = 0.35f)
    {
        if (!Application.isPlaying) return;
        EnsureQuad();
        if (_mat == null || _state.Busy || _pulsing) return;
        StartCoroutine(PulseRoutine(tint, Mathf.Max(0.2f, seconds), peak));
    }

    private System.Collections.IEnumerator PulseRoutine(Color tint, float seconds, float peak)
    {
        _pulsing = true;
        var c = _mat.color;
        _mat.color = new Color(tint.r, tint.g, tint.b, 0f);
        float t = 0f;
        while (t < seconds && !_state.Busy)
        {
            t += Time.deltaTime;
            float a = FadeState.Pulse01(t / seconds, peak);
            var pc = _mat.color; pc.a = a; _mat.color = pc;
            quad.enabled = a > 0.001f;
            yield return null;
        }
        // Hand the quad back to the fade system: black, at the fade's own alpha.
        _mat.color = new Color(0f, 0f, 0f, _state.Alpha);
        quad.enabled = _state.Alpha > 0.001f;
        _pulsing = false;
    }

    /// Compose pending fade callbacks instead of overwriting: a new fade started while
    /// another is mid-flight must not DROP the earlier callback (e.g. a HUD Restart
    /// during the Loading fade would lose DoLoad and wedge the single-exit Loading state).
    /// Both actions run, in order.
    public static Action Compose(Action first, Action second)
    {
        if (first == null) return second;
        if (second == null) return first;
        return () => { first(); second(); };
    }

    public void FadeOut(float seconds = -1f, Action done = null)
    {
        EnsureQuad();
        _onDone = Compose(_onDone, done);
        _state.Begin(1f, seconds > 0f ? seconds : defaultSeconds);
        if (quad != null) quad.enabled = true;
    }

    public void FadeIn(float seconds = -1f, Action done = null)
    {
        EnsureQuad();
        _onDone = Compose(_onDone, done);
        _state.Begin(0f, seconds > 0f ? seconds : defaultSeconds);
    }

    /// Fade to black, run the action (teleport / rebuild), fade back in.
    public void FadeAround(Action mid, float outSeconds = -1f, float inSeconds = -1f)
    {
        FadeOut(outSeconds, () =>
        {
            try { mid?.Invoke(); }
            finally { FadeIn(inSeconds); }
        });
    }

    /// Null-safe helper: with no fader in the scene (menus, tests) the action
    /// runs immediately — callers never need to care.
    public static void FadeOutThen(Action act, float seconds = 0.35f)
    {
        if (Instance != null && Instance.isActiveAndEnabled) Instance.FadeOut(seconds, act);
        else act?.Invoke();
    }
}
