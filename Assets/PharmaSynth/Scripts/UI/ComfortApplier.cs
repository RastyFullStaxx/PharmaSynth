using UnityEngine;
using SnapTurn = UnityEngine.XR.Interaction.Toolkit.Locomotion.Turning.SnapTurnProvider;
using Vignette = UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort.TunnelingVignetteController;

/// Pure mapping from comfort settings to the values the live systems consume
/// (§5 settings apply-listeners) — separated so the self-tests pin the curves.
public static class ComfortMath
{
    /// HUD root scale: the frustum-fit divides sizeDelta by scale, so a larger
    /// scale renders every HUD element (and its text) proportionally bigger.
    public static Vector3 HudScale(Vector3 baseScale, float textScale)
        => baseScale * Mathf.Clamp(textScale, 0.8f, 1.6f);

    /// Vignette intensity 0 → aperture 1 (off); 1 → 0.35 (strong tunnel).
    public static float ApertureFor(float intensity01)
        => Mathf.Lerp(1f, 0.35f, Mathf.Clamp01(intensity01));

    /// Subtitle pacing: speed 2 = lines dwell half as long, 0.5 = twice as long.
    public static float LineSecondsFor(float baseSeconds, float speed)
        => baseSeconds / Mathf.Clamp(speed, 0.5f, 2f);
}

/// Applies ComfortSettings to the live scene whenever SettingsService raises
/// Changed: HUD/text scale, snap-turn angle, tunneling vignette strength, and
/// Pharmee subtitle pacing. All targets optional — the applier works with
/// whatever the scene has (the menu scene has fewer targets than the lab).
public class ComfortApplier : MonoBehaviour
{
    /// Read by PharmaSynth/GuideGlow. 0 = pulse (the unset default), 1 = hold steady.
    public static readonly int GuideSteadyProperty = Shader.PropertyToID("_GuideSteady");

    [SerializeField] private Transform hudRoot;
    [SerializeField] private SnapTurn snapTurn;
    [SerializeField] private Vignette vignette;
    [SerializeField] private PharmeeBrain brain;
    [SerializeField] private PharmeeGatekeeper gatekeeper;

    private Vector3 _hudBase;
    private bool _hudBaseCaptured;
    private bool _subscribed;

    public void Bind(Transform hud, SnapTurn turn, Vignette vig, PharmeeBrain b, PharmeeGatekeeper g)
    { hudRoot = hud; snapTurn = turn; vignette = vig; brain = b; gatekeeper = g; }

    void OnEnable() => TrySubscribe();
    void Start() => TrySubscribe();          // Services may Awake after us

    void TrySubscribe()
    {
        if (_subscribed || SettingsService.Instance == null) return;
        _subscribed = true;
        SettingsService.Instance.Changed += Apply;
        Apply(SettingsService.Instance.Settings);
    }

    void OnDisable()
    {
        if (_subscribed && SettingsService.Instance != null)
            SettingsService.Instance.Changed -= Apply;
        _subscribed = false;
    }

    /// Public so tests drive it directly (no PlayerPrefs / singleton needed).
    public void Apply(ComfortSettings s)
    {
        if (s == null) return;
        if (hudRoot != null)
        {
            if (!_hudBaseCaptured) { _hudBase = hudRoot.localScale; _hudBaseCaptured = true; }
            hudRoot.localScale = ComfortMath.HudScale(_hudBase, s.textScale);
        }
        if (snapTurn != null) snapTurn.turnAmount = s.snapTurnAngle;

        // Guidance flash switch. A GLOBAL shader float, not a material edit: the two
        // guide materials are shared ASSETS, so writing _PulseMin into them at runtime
        // would dirty them on disk in the editor and persist into the next session.
        Shader.SetGlobalFloat(GuideSteadyProperty, ComfortSettings.SteadyGlobal(s.reduceFlashing));
        // Full, uncircled view (like Meta's home). The XRI
        // tunneling vignette was stuck as a permanent ~0.7-aperture circle in the
        // headset — a locomotion provider (move-under-gravity) reports "locomoting"
        // even while standing, so easing the idle aperture open didn't help. Turn
        // the overlay OFF outright; no provider can bring the circle back. Re-add a
        // comfort tunnel later only if a provider gates on INTENTIONAL movement.
        if (vignette != null && vignette.gameObject.activeSelf) vignette.gameObject.SetActive(false);
        if (brain != null) brain.SetSubtitlePace(s.subtitleSpeed);
        if (gatekeeper != null) gatekeeper.SetSubtitlePace(s.subtitleSpeed);
    }
}
