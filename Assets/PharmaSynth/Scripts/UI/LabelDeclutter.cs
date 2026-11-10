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
    /// Nothing is nudged further than this, in metres. The tag's pointer line keeps a lifted
    /// tag tied to its item, so it can travel further than bare text could (0.12 m before
    /// the pointer, which left panelled tags overlapping); past this a tag still reads as
    /// belonging somewhere else, and an overlapping tag is better than a misleading one.
    public const float MaxLift = 0.22f;

    /// Air left around each tag, in screen pixels, so neighbouring panels do not touch.
    public const float GapPixels = 3f;

    /// Vertical lift for each label so no two overlap, given their screen-space centres and
    /// half-heights (pixels) and the metres-per-pixel scale at each label's depth.
    ///
    /// Deterministic: labels are resolved bottom-up, each one rising just far enough to
    /// clear the one below it. Order comes from the screen positions alone, so the same
    /// arrangement always produces the same layout and tags do not swap places frame to
    /// frame. Returns metres, one per input, zero where nothing had to move.
    public static float[] Lifts(IReadOnlyList<float> screenY, IReadOnlyList<float> halfHeight,
                                IReadOnlyList<float> metresPerPixel, float maxLift = MaxLift)
        => Lifts(null, null, screenY, halfHeight, metresPerPixel, maxLift);

    /// Which tags to hide because they STILL overlap a kept tag after lifting: where the lift
    /// cap runs out (a dense rack, a close camera) the farther tag gives way rather than
    /// draw over its neighbour. Guided tags are kept first and never hidden; then the
    /// nearer tag (taller on screen) wins, then the lower `order`, so the answer is stable.
    public static bool[] Crowded(IReadOnlyList<float> screenX, IReadOnlyList<float> halfWidth,
                                 IReadOnlyList<float> screenY, IReadOnlyList<float> halfHeight,
                                 IReadOnlyList<float> lifts, IReadOnlyList<float> metresPerPixel,
                                 IReadOnlyList<bool> guided, IReadOnlyList<int> order)
    {
        int n = screenY != null ? screenY.Count : 0;
        var hidden = new bool[n];
        var y = new float[n];
        for (int i = 0; i < n; i++)
            y[i] = screenY[i] + (metresPerPixel[i] > 0f ? lifts[i] / metresPerPixel[i] : 0f);
        var idx = new List<int>(n);
        for (int i = 0; i < n; i++) idx.Add(i);
        idx.Sort((a, b) =>
        {
            bool ga = guided != null && guided[a], gb = guided != null && guided[b];
            if (ga != gb) return ga ? -1 : 1;
            int byHeight = halfHeight[b].CompareTo(halfHeight[a]);
            if (byHeight != 0) return byHeight;
            return (order != null ? order[a] : a).CompareTo(order != null ? order[b] : b);
        });
        var kept = new List<int>(n);
        foreach (int i in idx)
        {
            bool clash = false;
            if (!(guided != null && guided[i]))
                foreach (int j in kept)
                    if (Mathf.Abs(screenX[i] - screenX[j]) < halfWidth[i] + halfWidth[j]
                        && Mathf.Abs(y[i] - y[j]) < halfHeight[i] + halfHeight[j]) { clash = true; break; }
            hidden[i] = clash;
            if (!clash) kept.Add(i);
        }
        return hidden;
    }

    /// Which tags to hide because another tag with the SAME text already overlaps them on
    /// screen: a rack of six empty tubes needs one "Test Tube, empty", not six stacked
    /// copies. Guided tags are kept first and never hidden; among the rest the lower
    /// `order` key wins, so the same tag survives every poll.
    public static bool[] Duplicates(IReadOnlyList<string> text, IReadOnlyList<float> screenX,
                                    IReadOnlyList<float> halfWidth, IReadOnlyList<float> screenY,
                                    IReadOnlyList<float> halfHeight, IReadOnlyList<bool> guided,
                                    IReadOnlyList<int> order)
    {
        int n = text != null ? text.Count : 0;
        var hidden = new bool[n];
        var idx = new List<int>(n);
        for (int i = 0; i < n; i++) idx.Add(i);
        idx.Sort((a, b) =>
        {
            bool ga = guided != null && guided[a], gb = guided != null && guided[b];
            if (ga != gb) return ga ? -1 : 1;
            return (order != null ? order[a] : a).CompareTo(order != null ? order[b] : b);
        });
        var kept = new List<int>(n);
        foreach (int i in idx)
        {
            bool dup = false;
            if (!(guided != null && guided[i]))
                foreach (int j in kept)
                {
                    if (text[i] != text[j]) continue;
                    if (Mathf.Abs(screenX[i] - screenX[j]) < halfWidth[i] + halfWidth[j]
                        && Mathf.Abs(screenY[i] - screenY[j]) < halfHeight[i] + halfHeight[j]) { dup = true; break; }
                }
            hidden[i] = dup;
            if (!dup) kept.Add(i);
        }
        return hidden;
    }

    /// As above, but a label only has to clear the ones it overlaps SIDEWAYS too: two tags
    /// at the same height a metre apart on screen do not collide and must not move. With
    /// no x data every pair counts as overlapping (the original behaviour).
    ///
    /// ⛔ The inputs must be each tag's REST position, before any lift. Measuring the lifted
    /// tag fed the last answer back in: lifted tags no longer overlapped, so they dropped,
    /// overlapped again and rose again, every poll, forever.
    public static float[] Lifts(IReadOnlyList<float> screenX, IReadOnlyList<float> halfWidth,
                                IReadOnlyList<float> screenY, IReadOnlyList<float> halfHeight,
                                IReadOnlyList<float> metresPerPixel, float maxLift = MaxLift)
    {
        int n = screenY != null ? screenY.Count : 0;
        var lifts = new float[n];
        if (n < 2) return lifts;

        var order = new List<int>(n);
        for (int i = 0; i < n; i++) order.Add(i);
        order.Sort((a, b) => screenY[a].CompareTo(screenY[b]));

        bool sideways = screenX != null && halfWidth != null && screenX.Count == n && halfWidth.Count == n;
        var top = new float[n];                      // placed top edge of each label, in pixels
        var placed = new List<int>(n);
        for (int k = 0; k < order.Count; k++)
        {
            int i = order[k];
            float half = halfHeight != null && i < halfHeight.Count ? Mathf.Max(0f, halfHeight[i]) : 0f;
            float want = screenY[i];
            // Lowest centre that clears every already-placed label it overlaps sideways.
            float need = float.NegativeInfinity;
            foreach (int j in placed)
            {
                if (sideways && Mathf.Abs(screenX[i] - screenX[j]) >= halfWidth[i] + halfWidth[j]) continue;
                need = Mathf.Max(need, top[j] + half);
            }
            float y = Mathf.Max(want, need);

            // Clamp in METRES, then convert back: the cap is a physical distance from the
            // object, not a number of pixels, or a tag would drift arbitrarily far when the
            // player stands close.
            float mpp = metresPerPixel != null && i < metresPerPixel.Count ? metresPerPixel[i] : 0f;
            float liftM = mpp > 0f ? (y - want) * mpp : 0f;
            liftM = Mathf.Clamp(liftM, 0f, maxLift);
            lifts[i] = liftM;

            float appliedPixels = mpp > 0f ? liftM / mpp : 0f;
            top[i] = want + appliedPixels + half;
            placed.Add(i);
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

    /// While set, tags are separated in this camera's screen instead of the head's (the
    /// showcase reels set it to the cinema's shot camera, alongside ProximityLabel.Viewer).
    public static Camera ViewerCamera;
    private readonly List<ProximityLabel> _visible = new List<ProximityLabel>();
    private readonly List<float> _x = new List<float>();
    private readonly List<float> _halfW = new List<float>();
    private readonly List<float> _y = new List<float>();
    private readonly List<float> _half = new List<float>();
    private readonly List<float> _mpp = new List<float>();
    private readonly List<bool> _visGuided = new List<bool>();
    private readonly List<int> _visOrder = new List<int>();
    private readonly List<ProximityLabel> _candidates = new List<ProximityLabel>();
    private readonly List<string> _text = new List<string>();
    private readonly List<bool> _guided = new List<bool>();
    private readonly List<int> _order = new List<int>();
    private readonly List<float> _cx = new List<float>(), _cy = new List<float>(), _cw = new List<float>(), _ch = new List<float>();

    /// Builder seam (AddComponent fires no Awake in edit mode).
    public void Bind(float poll, float lift) { pollSeconds = poll; maxLift = lift; }

    private void Update()
    {
        if (!Application.isPlaying || Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollSeconds;

        if (_cam == null) _cam = Camera.main;
        var cam = ViewerCamera != null ? ViewerCamera : _cam;
        if (cam == null) return;

        // Pass 1: of the tags that want to show, hide exact duplicates sitting on top of
        // each other. Measured at rest, so hiding one never changes the next answer.
        _candidates.Clear(); _text.Clear(); _guided.Clear(); _order.Clear();
        _cx.Clear(); _cy.Clear(); _cw.Clear(); _ch.Clear();
        foreach (var label in FindObjectsByType<ProximityLabel>())
        {
            if (label == null || label.Tag == null || !label.WantsToShow) continue;
            Vector3 sp = cam.WorldToScreenPoint(label.RestPosition);
            if (sp.z <= 0.05f) { label.SetDuplicateHidden(false); continue; }
            _candidates.Add(label); _text.Add(label.Text); _guided.Add(label.IsGuided);
            _order.Add(label.Serial);
            _cx.Add(sp.x); _cy.Add(sp.y);
            _cw.Add(Mathf.Max(1f, label.ScreenHalfWidth(cam))); _ch.Add(Mathf.Max(1f, label.ScreenHalfHeight(cam)));
        }
        var dup = LabelDeclutterMath.Duplicates(_text, _cx, _cw, _cy, _ch, _guided, _order);
        for (int i = 0; i < _candidates.Count; i++) _candidates[i].SetDuplicateHidden(dup[i]);

        // Pass 2: lift the tags still showing so none overlap.
        _visible.Clear(); _x.Clear(); _halfW.Clear(); _y.Clear(); _half.Clear(); _mpp.Clear();
        _visGuided.Clear(); _visOrder.Clear();
        for (int c = 0; c < _candidates.Count; c++)
        {
            if (dup[c]) continue;
            var label = _candidates[c];
            Vector3 rest = label.RestPosition;                 // where the tag sits unlifted
            Vector3 sp = cam.WorldToScreenPoint(rest);
            if (sp.z <= 0.05f) continue;                       // behind the camera
            _visible.Add(label);
            _x.Add(sp.x);
            // A few pixels of air around each panel, so neighbours sit apart, not edge to edge.
            _halfW.Add(Mathf.Max(1f, label.ScreenHalfWidth(cam)) + LabelDeclutterMath.GapPixels);
            _y.Add(sp.y);
            _half.Add(Mathf.Max(1f, label.ScreenHalfHeight(cam)) + LabelDeclutterMath.GapPixels);
            // Metres per pixel at this depth, so the pixel solve converts back to a lift.
            Vector3 up = cam.WorldToScreenPoint(rest + Vector3.up * 0.1f);
            float dy = Mathf.Abs(up.y - sp.y);
            _mpp.Add(dy > 0.01f ? 0.1f / dy : 0f);
            _visGuided.Add(label.IsGuided);
            _visOrder.Add(label.Serial);
        }

        var lifts = LabelDeclutterMath.Lifts(_x, _halfW, _y, _half, _mpp, maxLift);
        var crowded = LabelDeclutterMath.Crowded(_x, _halfW, _y, _half, lifts, _mpp, _visGuided, _visOrder);
        for (int i = 0; i < _visible.Count; i++)
        {
            _visible[i].SetDeclutterLift(lifts[i]);
            if (crowded[i]) _visible[i].SetDuplicateHidden(true);   // same switch: hidden by the solver
        }
    }
}
