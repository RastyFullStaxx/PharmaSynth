using System.Collections.Generic;
using UnityEngine;

/// Pure rules for keeping world-space name tags from sitting on top of each other
/// (labels must not overlap: they push each other apart while staying next to the item
/// they name, so players can read them).
///
/// Every ProximityLabel billboards independently, so a rack of 19 identical tubes, a shelf
/// of bottles or a cluster of glassware produced a stack of tags occupying the same few
/// centimetres of screen. Nothing was wrong with any single tag; the problem only exists
/// between them, which is why it needs a pass that can see them all at once.
///
/// The separation is computed in SCREEN space (that is where the overlap actually happens —
/// two tubes a hand's width apart overlap from across the room and not from close up) and
/// applied as a small WORLD-space lift, so a tag never leaves the item it names.
public static class LabelDeclutterMath
{
    /// Nothing is nudged further than this, in metres. A tag that has to travel further to
    /// find room has stopped labelling its own object, and a wrong-looking label is worse
    /// than an overlapping one.
    public const float MaxLift = 0.12f;

    /// Vertical lift for each label so no two overlap, given their screen-space centres and
    /// half-heights (pixels) and the metres-per-pixel scale at each label's depth.
    ///
    /// Deterministic: labels are resolved bottom-up, each one rising just far enough to
    /// clear the one below it. Order comes from the screen positions alone, so the same
    /// arrangement always produces the same layout and tags do not swap places frame to
    /// frame. Returns metres, one per input, zero where nothing had to move.
    public static float[] Lifts(IReadOnlyList<float> screenY, IReadOnlyList<float> halfHeight,
                                IReadOnlyList<float> metresPerPixel, float maxLift = MaxLift)
    {
        int n = screenY != null ? screenY.Count : 0;
        var lifts = new float[n];
        if (n < 2) return lifts;

        var order = new List<int>(n);
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => screenY[a].CompareTo(screenY[b]));

        float ceiling = float.NegativeInfinity;      // top edge of the label below, in pixels
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            float half = halfHeight != null && i < halfHeight.Count ? Mathf.Max(0f, halfHeight[i]) : 0f;
            float want = screenY[i];
            float need = ceiling + half;             // lowest centre that clears the one below
            float y = Mathf.Max(want, need);

            // Clamp in METRES, then convert back: the cap is a physical distance from the
            // object, not a number of pixels, or a tag would drift arbitrarily far when the
            // player stands close.
            float mpp = metresPerPixel != null && i < metresPerPixel.Count ? metresPerPixel[i] : 0f;
            float liftM = mpp > 0f ? (y - want) * mpp : 0f;
            liftM = Mathf.Clamp(liftM, 0f, maxLift);
            lifts[i] = liftM;

            float appliedPixels = mpp > 0f ? liftM / mpp : 0f;
            ceiling = want + appliedPixels + half;
        }
        return lifts;
    }
}

/// Drives LabelDeclutterMath over the live ProximityLabels. Thin: it measures, calls
/// the pure solver and hands each label a lift.
///
/// Polls at the same 5 Hz TutorialHighlighter uses. Tags are read at a human pace, and a
/// per-frame solve over every visible label would cost more than the problem.
public class LabelDeclutter : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float pollSeconds = 0.2f;
    [SerializeField, Min(0f)] private float maxLift = LabelDeclutterMath.MaxLift;

    private float _nextPoll;
    private Camera _cam;
    private readonly List<ProximityLabel> _visible = new List<ProximityLabel>();
    private readonly List<float> _y = new List<float>();
    private readonly List<float> _half = new List<float>();
    private readonly List<float> _mpp = new List<float>();

    /// Builder seam (AddComponent fires no Awake in edit mode).
    public void Bind(float poll, float lift) { pollSeconds = poll; maxLift = lift; }

    private void Update()
    {
        if (!Application.isPlaying || Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollSeconds;

        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        _visible.Clear(); _y.Clear(); _half.Clear(); _mpp.Clear();
        foreach (var label in FindObjectsByType<ProximityLabel>(FindObjectsSortMode.None))
        {
            if (label == null) continue;
            var tag = label.Tag;
            if (tag == null || !tag.activeInHierarchy) continue;

            Vector3 sp = _cam.WorldToScreenPoint(tag.transform.position);
            if (sp.z <= 0.05f) continue;                       // behind the camera
            _visible.Add(label);
            _y.Add(sp.y);
            _half.Add(Mathf.Max(1f, label.ScreenHalfHeight(_cam)));
            // Metres per pixel at this depth, so the pixel solve converts back to a lift.
            Vector3 up = _cam.WorldToScreenPoint(tag.transform.position + Vector3.up * 0.1f);
            float dy = Mathf.Abs(up.y - sp.y);
            _mpp.Add(dy > 0.01f ? 0.1f / dy : 0f);
        }

        var lifts = LabelDeclutterMath.Lifts(_y, _half, _mpp, maxLift);
        for (int i = 0; i < _visible.Count; i++) _visible[i].SetDeclutterLift(lifts[i]);
    }
}
