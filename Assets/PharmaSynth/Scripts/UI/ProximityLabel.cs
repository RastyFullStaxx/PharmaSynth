using UnityEngine;
using TMPro;

/// Shows a small floating name tag above an object only when the player's camera is
/// within range — so apparatus and reagents identify themselves as you approach,
/// without cluttering the lab with permanent labels. Builds its own world-space TMP
/// child on first enable; billboards to the camera.
public class ProximityLabel : MonoBehaviour
{
    [SerializeField] private string label = "";

    /// What the tag says right now — the suite reads it to pin what a label must NOT carry.
    public string CurrentText => label;
    [SerializeField] private float showDistance = 1.4f;
    [SerializeField] private float heightOffset = 0.14f;
    [SerializeField] private float fontSize = 5.5f;

    private Transform _cam;
    private GameObject _tag;

    /// The tag object, so LabelDeclutter can measure where it landed on screen.
    public GameObject Tag => _tag;

    /// Extra world-space lift handed down by LabelDeclutter so two tags stop sitting on
    /// top of each other. Zero for a label with nothing near it, which is most of them.
    private float _declutterLift;
    public void SetDeclutterLift(float metres) => _declutterLift = metres;

    // The lift actually shown glides toward the solved one, so a re-solve after a head
    // turn moves a tag smoothly instead of snapping it.
    private float _liftShown, _liftVelocity;
    public const float LiftGlideSeconds = 0.25f;

    // Hidden by LabelDeclutter because an identical tag already says the same thing in the
    // same spot (six "Test Tube, empty" tags on one rack). A guided item is never hidden.
    private bool _duplicateHidden;
    public void SetDuplicateHidden(bool hidden) => _duplicateHidden = hidden;

    /// Would this tag show if no duplicate hid it? LabelDeclutter solves over these, so a
    /// hidden duplicate keeps its place in the solve and cannot flicker back.
    public bool WantsToShow { get; private set; }
    public bool IsGuided => _guide != null && _guide.IsGuided;
    public string Text => _tmp != null ? _tmp.text : label;

    // Creation order, a stable tie-break for which duplicate stays visible.
    private static int s_nextSerial;
    public int Serial { get; } = ++s_nextSerial;

    /// Where the tag sits without any declutter lift: what LabelDeclutter must measure.
    public Vector3 RestPosition => _tag != null ? _tag.transform.position - Vector3.up * _liftShown : transform.position;

    /// Half the tag's width in SCREEN pixels, for the sideways overlap test.
    public float ScreenHalfWidth(Camera cam)
    {
        if (_tag == null || cam == null) return 0f;
        float worldHalf = _tmp != null ? _tmp.bounds.extents.x * _tag.transform.lossyScale.x : 0.05f;
        Vector3 c = cam.WorldToScreenPoint(_tag.transform.position);
        Vector3 r = cam.WorldToScreenPoint(_tag.transform.position + cam.transform.right * Mathf.Max(worldHalf, 1e-4f));
        return Mathf.Abs(r.x - c.x);
    }

    /// Half the tag's height in SCREEN pixels — what the overlap test actually needs.
    /// Measured from the rendered text bounds, so a two-line contents readout correctly
    /// claims twice the room of a bare name.
    public float ScreenHalfHeight(Camera cam)
    {
        if (_tag == null || cam == null) return 0f;
        float worldHalf = _tmp != null ? _tmp.bounds.extents.y * _tag.transform.lossyScale.y : 0.02f;
        Vector3 c = cam.WorldToScreenPoint(_tag.transform.position);
        Vector3 t = cam.WorldToScreenPoint(_tag.transform.position + Vector3.up * Mathf.Max(worldHalf, 1e-4f));
        return Mathf.Abs(t.y - c.y);
    }
    private TextMeshPro _tmp;
    private Renderer[] _itemRends;   // the item's own renderers, cached (no per-frame alloc)
    private HoverHighlight _guide;   // cached once: GetComponent every frame is waste
    private bool _guideChecked;

    public void SetLabel(string text, float dist = 1.4f)
    {
        label = text; showDistance = dist;
        if (_tmp != null) _tmp.text = GlyphSafe.Sanitize(text);
    }

    /// Practice mode reads labels from further out: a student who cannot tell two
    /// bottles apart learns nothing from finding the right one by glow alone, and this
    /// is a cheaper answer to findability than an x-ray silhouette because it also
    /// teaches label-reading.
    ///
    /// 2.5× (≈3.5 m), NOT the 4× first drafted — the reagent cabinets alone hold 57
    /// bottles, and a 5.6 m radius lights the entire east wall at once, which is text
    /// soup rather than help. Reach is handled instead by ForceShow below: the objects
    /// the current step actually needs are labelled at ANY distance.
    public const float TutorialRadiusMultiplier = 2.5f;

    public static float VisibleRadius(float baseRadius, bool tutorial)
        => tutorial ? baseRadius * TutorialRadiusMultiplier : baseRadius;

    /// Pure: is this label shown right now? Guided objects are always named — that is
    /// the one label the player is actually looking for.
    public static bool ShouldShow(float distance, float baseRadius, bool tutorial, bool guided)
        => (tutorial && guided) || distance <= VisibleRadius(baseRadius, tutorial);

    private void Awake() => Build();

    private void Build()
    {
        if (_tag != null || string.IsNullOrEmpty(label)) return;
        // Cache the item's own renderers BEFORE the tag's TMP renderer is added,
        // so Update never re-queries the component tree (this ran every frame while
        // the label was visible — a per-frame array allocation).
        _itemRends = GetComponentsInChildren<Renderer>();
        _tag = new GameObject("ProxTag");
        _tag.transform.SetParent(transform, false);
        // Sit just above the object's top, in world scale (undo parent scaling).
        var rends = _itemRends;
        float top = 0f;
        if (rends.Length > 0)
        {
            Bounds b = rends[0].bounds; for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            top = (b.max.y - transform.position.y);
        }
        _tag.transform.position = transform.position + Vector3.up * (top + heightOffset);
        var ls = transform.lossyScale;
        _tag.transform.localScale = new Vector3(1f / Mathf.Max(ls.x, 1e-4f), 1f / Mathf.Max(ls.y, 1e-4f), 1f / Mathf.Max(ls.z, 1e-4f)) * 0.02f;

        _tmp = _tag.AddComponent<TextMeshPro>();
        _tmp.text = GlyphSafe.Sanitize(label);
        _tmp.fontSize = fontSize;
        _tmp.alignment = TextAlignmentOptions.Center;
        _tmp.color = Color.white;
        _tmp.fontStyle = FontStyles.Bold;
        _tmp.outlineWidth = 0.25f;                       // dark halo → readable on any background
        _tmp.outlineColor = new Color32(6, 12, 22, 255);
        var mr = _tag.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sortingOrder = 32760;                     // draw over glass/props
        }
        _tag.SetActive(false);
    }

    private void Update()
    {
        if (_tag == null) { Build(); if (_tag == null) return; }
        if (_cam == null)
        {
            var c = Camera.main; if (c == null) return; _cam = c.transform;
        }
        float d = Vector3.Distance(_cam.position, transform.position);
        if (!_guideChecked) { _guide = GetComponent<HoverHighlight>(); _guideChecked = true; }
        bool guided = _guide != null && _guide.IsGuided;
        WantsToShow = ShouldShow(d, showDistance, TutorialSession.Active, guided);
        bool show = WantsToShow && (guided || !_duplicateHidden);
        if (_tag.activeSelf != show) _tag.SetActive(show);
        if (WantsToShow)   // placed even while hidden, so its rest position stays current
        {
            // Float the tag toward the player and above the item, then billboard it.
            var rends = _itemRends;   // cached item renderers (excludes the tag's own)
            float top = transform.position.y + heightOffset;
            float mid = transform.position.y;
            if (rends != null && rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                top = b.max.y + heightOffset;
                mid = b.center.y;
            }
            // Shelf-caged items: a plank right above would swallow the tag — hang it
            // at mid-height instead and pull it well out of the shelf face.
            float fwdDist = 0.08f;
            float tagY = top;
            if (Physics.Raycast(new Vector3(transform.position.x, top - heightOffset + 0.01f, transform.position.z),
                                Vector3.up, 0.35f, ~0, QueryTriggerInteraction.Ignore))
            {
                tagY = mid;
                fwdDist = 0.3f;
            }
            Vector3 toCam = (_cam.position - transform.position); toCam.y = 0f;
            Vector3 fwd = toCam.sqrMagnitude > 1e-4f ? toCam.normalized : Vector3.forward;
            _liftShown = Mathf.SmoothDamp(_liftShown, _declutterLift, ref _liftVelocity, LiftGlideSeconds);
            _tag.transform.position = new Vector3(transform.position.x, tagY + _liftShown, transform.position.z) + fwd * fwdDist;
            _tag.transform.rotation = Quaternion.LookRotation(_tag.transform.position - _cam.position, Vector3.up);
        }
    }
}
