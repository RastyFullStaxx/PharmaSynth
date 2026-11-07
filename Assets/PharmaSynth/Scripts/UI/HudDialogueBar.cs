using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// Screen-bottom dialogue bar (storyboard style): the speaker's portrait + name +
/// the line they are currently speaking, mirrored from NPCNarrationController so
/// the player can read them without looking at them. Visible ONLY while a line is
/// live; fades out smoothly when the line ends.
///
/// MULTI-SPEAKER. Pharmee and Dr. Jimenez own SEPARATE NPCNarrationControllers (their
/// own world bubbles), so the bar subscribes to each channel; each carries its own
/// name + portrait, and whichever speaks last owns the bar.
public class HudDialogueBar : MonoBehaviour
{
    [System.Serializable]
    public class Channel
    {
        public NPCNarrationController narration;
        public string speakerName = "Pharmee";
        public Sprite portrait;
    }

    [SerializeField] private NPCNarrationController narration;   // channel 0 (legacy field, kept for scene refs)
    [SerializeField] private GameObject barRoot;      // toggled with the line
    [SerializeField] private TMP_Text speakerText;    // "Pharmee" / "Dr. Jimenez"
    [SerializeField] private TMP_Text lineText;
    [SerializeField] private Image portraitImage;     // the DialogueBar's Portrait Image
    [SerializeField] private string primarySpeakerName = "Pharmee";
    [SerializeField] private Sprite primaryPortrait;
    [Tooltip("Additional speakers (Dr. Jimenez). Each has its own narration channel, name and portrait.")]
    [SerializeField] private Channel[] extraSpeakers = new Channel[0];
    [SerializeField] private float fadeSeconds = 0.5f;

    private bool _subscribed;
    private CanvasGroup _group;
    private Coroutine _fade;
    /// The controller whose line is currently on the bar — the typewriter mirror
    /// must follow THIS one, or a second speaker's line would never reveal.
    private NPCNarrationController _active;
    // Per-channel handlers, kept so Unsubscribe removes exactly what it added.
    private System.Action<string, float>[] _started;
    private System.Action[] _ended;

    /// Edit-mode/test binding (single speaker — the original signature).
    public void Bind(NPCNarrationController n, GameObject root, TMP_Text speaker, TMP_Text line)
    {
        Unsubscribe();
        narration = n; barRoot = root; speakerText = speaker; lineText = line;
        Subscribe();
        if (barRoot != null) barRoot.SetActive(false);
    }

    /// Full binding: portrait + the extra speaker channels.
    public void Bind(NPCNarrationController n, GameObject root, TMP_Text speaker, TMP_Text line,
                     Image portrait, Sprite primaryIcon, Channel[] extras)
    {
        Unsubscribe();
        narration = n; barRoot = root; speakerText = speaker; lineText = line;
        portraitImage = portrait; primaryPortrait = primaryIcon;
        extraSpeakers = extras ?? new Channel[0];
        Subscribe();
        if (barRoot != null) barRoot.SetActive(false);
    }

    /// Every channel, primary first.
    private Channel[] Channels()
    {
        int extra = extraSpeakers != null ? extraSpeakers.Length : 0;
        var all = new Channel[1 + extra];
        all[0] = new Channel { narration = narration, speakerName = primarySpeakerName, portrait = primaryPortrait };
        for (int i = 0; i < extra; i++) all[1 + i] = extraSpeakers[i];
        return all;
    }

    private void OnEnable()
    {
        Subscribe();
        if (barRoot != null && (narration == null || !narration.IsSpeaking))
            barRoot.SetActive(false);
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed) return;
        var all = Channels();
        _started = new System.Action<string, float>[all.Length];
        _ended = new System.Action[all.Length];
        bool any = false;
        for (int i = 0; i < all.Length; i++)
        {
            var ch = all[i];
            if (ch == null || ch.narration == null) continue;
            var cap = ch;
            _started[i] = (l, s) => HandleLineStarted(l, s, cap);
            _ended[i] = () => HandleLineEnded(cap.narration);
            ch.narration.LineStarted += _started[i];
            ch.narration.LineEnded += _ended[i];
            any = true;
        }
        _subscribed = any;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        var all = Channels();
        for (int i = 0; i < all.Length && i < _started.Length; i++)
        {
            var ch = all[i];
            if (ch == null || ch.narration == null) continue;
            if (_started[i] != null) ch.narration.LineStarted -= _started[i];
            if (_ended[i] != null) ch.narration.LineEnded -= _ended[i];
        }
        _subscribed = false;
    }

    private CanvasGroup Group()
    {
        if (_group == null && barRoot != null)
        {
            _group = barRoot.GetComponent<CanvasGroup>();
            if (_group == null) _group = barRoot.AddComponent<CanvasGroup>();
        }
        return _group;
    }

    /// Public for headless tests (single-speaker path).
    public void HandleLineStarted(string line, float seconds)
        => HandleLineStarted(line, seconds,
            new Channel { narration = narration, speakerName = primarySpeakerName, portrait = primaryPortrait });

    private void HandleLineStarted(string line, float seconds, Channel ch)
    {
        if (_fade != null) { StopCoroutine(_fade); _fade = null; }
        _active = ch != null ? ch.narration : narration;
        bool typing = _active != null && _active.Typewriter;
        if (lineText != null)
        {
            lineText.text = GlyphSafe.Sanitize(line);
            lineText.maxVisibleCharacters = typing ? 0 : int.MaxValue;
        }
        // Set the name UNCONDITIONALLY: guarding on IsNullOrEmpty makes the scene's baked
        // "Pharmee" sticky, so a second speaker would show that name.
        if (speakerText != null && ch != null && !string.IsNullOrEmpty(ch.speakerName))
            speakerText.text = GlyphSafe.Sanitize(ch.speakerName);
        if (portraitImage != null && ch != null && ch.portrait != null)
            portraitImage.sprite = ch.portrait;
        var g = Group();
        if (g != null) g.alpha = 1f;
        if (barRoot != null) barRoot.SetActive(true);
    }

    // Mirror the ACTIVE speaker's reveal count each frame → the HUD line types in
    // lockstep with their bubble (and a skip fills both at once), no second timer.
    private void Update()
    {
        var n = _active != null ? _active : narration;
        if (n == null || lineText == null) return;
        if (n.IsSpeaking && n.Typewriter)
            lineText.maxVisibleCharacters = n.VisibleCount;
    }

    public void HandleLineEnded() => HandleLineEnded(null);

    private void HandleLineEnded(NPCNarrationController from)
    {
        // Ignore a stale end from a speaker who no longer owns the bar (the other
        // NPC took it over) — otherwise one ending line hides the other's live one.
        if (from != null && _active != null && from != _active) return;
        // Runtime: fade out smoothly; edit mode/tests: hide immediately.
        if (Application.isPlaying && isActiveAndEnabled && barRoot != null && barRoot.activeSelf)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeOutRoutine());
        }
        else HideNow();
    }

    private void HideNow()
    {
        if (barRoot != null) barRoot.SetActive(false);
        if (lineText != null) lineText.text = string.Empty;
        var g = Group();
        if (g != null) g.alpha = 1f;
        _active = null;
    }

    private IEnumerator FadeOutRoutine()
    {
        var g = Group();
        float t = 0f;
        float dur = Mathf.Max(0.05f, fadeSeconds);
        while (t < dur)
        {
            t += Time.deltaTime;
            if (g != null) g.alpha = 1f - Mathf.Clamp01(t / dur);
            yield return null;
        }
        _fade = null;
        HideNow();
    }
}
