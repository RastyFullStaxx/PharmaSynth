using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// Wrist-flip progress tracker (the headline feature). Flipping the wrist so
/// the watch face turns up (supination) while glancing toward it shows a compact
/// panel: current step, progress %, mastery %. A button/thumbstick fallback
/// toggles it so the feature works without the gesture (and is testable pre-HMD).
///
/// Gesture detection is via the anchor transform's up-vector, which works for both
/// controller supination and hand-tracking palm-up. Hysteresis prevents flicker.
public class WristWatchController : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;

    [Header("Anchor & view")]
    [SerializeField] private Transform watchAnchor;     // on the wrist (right hand default)
    [SerializeField] private Transform headTransform;   // HMD camera, for the glance check
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text summaryText;

    [Header("Gesture")]
    [SerializeField, Range(0f, 1f)] private float supinationShow = 0.6f;   // face-up threshold to show
    [SerializeField, Range(0f, 1f)] private float supinationHide = 0.4f;   // lower = hysteresis to hide
    [SerializeField, Range(0f, 1f)] private float gazeThreshold = 0.5f;    // head looking toward wrist
    [SerializeField] private bool requireGaze = true;

    [Header("Fallback input")]
    [SerializeField] private InputActionReference toggleAction;            // button/thumbstick fallback

    [Header("Holo checklist (large panel — the user's headline feature)")]
    [SerializeField] private GameObject holoPanel;      // world-space holographic board
    [SerializeField] private TMP_Text holoTitle;
    [SerializeField] private TMP_Text holoBody;
    [SerializeField] private TMP_Text holoSummary;      // one-line header (absorbed the wrist mini-panel)
    [SerializeField] private TMP_Text holoReaction;     // balanced-reaction footer (absorbed the LabTablet's)
    [SerializeField, TextArea] private string balancedReaction = "";
    [SerializeField] private float holoDistance = 1.15f;
    [SerializeField] private float holoHeightOffset = -0.05f;

    private bool _gestureVisible;
    private bool _manualVisible;
    private bool _lastShow;
    private HoloScroller _scroller;

    // Wrist-flip suppression window: the twisting hand can select Pharmee's interactable
    // since he hovers close by, which would fire his dialogue as the panel is summoned.
    // NPC poke handlers check this.
    private static float _npcPokeSuppressedUntil = -1f;
    public static bool SuppressNpcPokes => Application.isPlaying && Time.time < _npcPokeSuppressedUntil;

    private void OnEnable()
    {
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed += OnTogglePressed;
            toggleAction.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (toggleAction != null && toggleAction.action != null)
            toggleAction.action.performed -= OnTogglePressed;
    }

    private void OnTogglePressed(InputAction.CallbackContext _) => _manualVisible = !_manualVisible;

    private float _forcedUntil = -1f;

    /// How long an unprompted preview stays up before the player has to hold their own
    /// wrist. Long enough to read a nine-step procedure, short enough not to trap them.
    public const float PreviewSeconds = 12f;

    /// Tutorial Mode's sequence preview: put the procedures board up before the
    /// door opens, so the player walks in knowing the shape of the experiment.
    ///
    /// Deliberately just a forced SHOW of the board that already exists. The holo body is
    /// built from ChecklistPager every frame it is visible, so a preview needs no second
    /// panel, no second layout and no second text builder that could drift from it.
    public void ShowProcedurePreview(float seconds = PreviewSeconds)
        => _forcedUntil = Time.time + Mathf.Max(1f, seconds);

    private void Update()
    {
        if (watchAnchor != null)
        {
            // Trigger on the natural "check my watch" pose: the watch face (its
            // +up normal) aimed at the player's eyes — NOT palm-up toward the
            // ceiling (a Vector3.up reference fires on the wrong orientation).
            Vector3 toHead = headTransform != null
                ? (headTransform.position - watchAnchor.position).normalized
                : Vector3.up;
            float faceUp = Vector3.Dot(watchAnchor.up, toHead);
            // Hysteresis: raise above show-threshold to appear, drop below hide-threshold to vanish.
            if (!_gestureVisible && faceUp >= supinationShow) _gestureVisible = true;
            else if (_gestureVisible && faceUp < supinationHide) _gestureVisible = false;

            if (_gestureVisible && requireGaze && headTransform != null)
                _gestureVisible = IsGazingAt(headTransform.position, headTransform.forward, watchAnchor.position, gazeThreshold);
        }

        bool show = _gestureVisible || _manualVisible || Time.time < _forcedUntil;
        // No experiment content, no panels — otherwise the simulator's resting
        // palm-up controllers summon an empty board in the corridor/lab tour.
        if (runner == null || runner.Graph == null) show = false;
        // While the panel is up (and shortly after), the gesture hand must not
        // trigger Pharmee's poke/talk interactions.
        if (show || _gestureVisible) _npcPokeSuppressedUntil = Time.time + 1.5f;
        // One centered procedures display: the holo board below is the single panel.
        if (panel != null && panel.activeSelf) panel.SetActive(false);

        // Large holographic procedures board: appears in front of the player on
        // the same gesture. Focused checklist (active phase in detail, others
        // collapsed) + status header + reaction footer.
        if (show && !_lastShow)
        {
            PlaceHolo();
            // The board keeps its SCROLL POSITION between opens so the player never has to
            // re-find their place after a glance. A fresh experiment still starts at the top
            // because the text itself is rebuilt.
            if (_scroller == null && holoPanel != null)
                _scroller = holoPanel.GetComponentInChildren<HoloScroller>(true);
        }
        _lastShow = show;
        // The wrist gesture is the most-used interaction in the game, so open/close get a
        // sound; without one, nothing confirms the flick registered, which in VR reads as an
        // unresponsive control.
        if (holoPanel != null && holoPanel.activeSelf != show)
        {
            holoPanel.SetActive(show);
            if (Application.isPlaying)
                AudioService.TryPlayFirst(show ? "holo-open" : "holo-close", "ui-click");
        }
        if (show && runner != null)
        {
            if (holoTitle != null) holoTitle.text = runner.Module != null ? runner.Module.moduleTitle : "Procedures";
            if (holoSummary != null) holoSummary.text = ChecklistPager.BuildHeader(runner);
            if (holoBody != null && runner.Graph != null)
                holoBody.text = ChecklistPager.BuildObjectivesHeader(runner.Module)
                              + ChecklistPager.BuildMaterialsHeader(runner.Module)
                              + ChecklistPager.BuildFocusedText(runner.Graph);
            if (holoReaction != null) holoReaction.text = GlyphSafe.Sanitize(balancedReaction);
        }
    }

    /// Park the holo board in front of the player's face, upright, readable.
    private void PlaceHolo()
    {
        if (holoPanel == null || headTransform == null) return;
        Vector3 fwd = headTransform.forward; fwd.y = 0f;
        fwd = fwd.sqrMagnitude < 1e-4f ? Vector3.forward : fwd.normalized;
        holoPanel.transform.position = headTransform.position + fwd * holoDistance + Vector3.up * holoHeightOffset;
        holoPanel.transform.rotation = Quaternion.LookRotation(fwd);   // +Z away → UI reads correctly
    }

    /// Pure: what the holo checklist prints for the current step. Tutorial Mode prints
    /// the task's HINT beneath the label — in campaign a hint only surfaces on the
    /// stuck/poke path, because working out the "how" is part of the assessment there.
    public static string StepText(string label, string hint, bool tutorial)
    {
        if (!tutorial || string.IsNullOrEmpty(hint)) return label;
        return label + "\n<size=70%>" + hint + "</size>";
    }

    /// True when the head is looking roughly toward the wrist.
    public static bool IsGazingAt(Vector3 headPos, Vector3 headForward, Vector3 targetPos, float dotThreshold)
    {
        Vector3 toTarget = targetPos - headPos;
        if (toTarget.sqrMagnitude < 1e-6f) return true;
        return Vector3.Dot(headForward.normalized, toTarget.normalized) >= dotThreshold;
    }
}
