using System.Collections.Generic;
using UnityEngine;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;

/// Drives Tutorial Mode's guidance off the task graph: whatever step is available
/// right now, its objects glow.
///
/// ⭐ It NEVER decides that a step is DONE. This is a pure read of AvailableTasks();
/// the task completes through its existing binding exactly as in campaign, and the
/// glow follows on the next poll because the available set changed. Since no parallel
/// completion detector exists, the guidance cannot disagree with the game — which is
/// the failure mode this design avoids.
public class TutorialHighlighter : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private float pollSeconds = 0.2f;   // 5 Hz — per-frame is pure waste
    [SerializeField] private float regrabDelay = 0.5f;   // dropped-unused flicker guard

    private readonly Dictionary<Transform, TaskTarget> _lit = new Dictionary<Transform, TaskTarget>();
    private readonly Dictionary<Transform, float> _droppedAt = new Dictionary<Transform, float>();
    private readonly Dictionary<Transform, TaskTarget> _wanted = new Dictionary<Transform, TaskTarget>();
    private float _nextPoll;

    private bool _subscribed;

    /// Edit-mode seam — Awake/OnEnable do not fire on AddComponent in edit mode.
    /// The subscription lives HERE and not in OnEnable alone: in PLAY mode
    /// AddComponent fires OnEnable immediately, before Bind() has set `runner`.
    public void Bind(ExperimentRunner r) { Detach(); runner = r; Subscribe(); }

    private void Awake() { if (runner == null) runner = FindAnyObjectByType<ExperimentRunner>(); Subscribe(); }
    private void OnEnable() => Subscribe();

    private void Subscribe()
    {
        if (_subscribed || runner == null) return;
        runner.ExperimentStarted += OnRunBegan;
        runner.ExperimentPrepared += OnRunBegan;      // armed seam: stage is already up
        _subscribed = true;
    }

    /// Explicit detach: DestroyImmediate skips OnDisable/OnDestroy for a component
    /// whose OnEnable never ran, and a ghost subscription would outlive the object.
    public void Detach()
    {
        if (!_subscribed || runner == null) { _subscribed = false; return; }
        runner.ExperimentStarted -= OnRunBegan;
        runner.ExperimentPrepared -= OnRunBegan;
        _subscribed = false;
    }

    private void OnDestroy() => Detach();

    /// The stage is furnished during the launcher's onModuleLoaded, which fires before
    /// the runner starts — so by here every task-bearing component exists to be swept.
    private void OnRunBegan(ExperimentModuleDefinition _)
    {
        TutorialTargets.Build();
        _lit.Clear();
        _droppedAt.Clear();
        _held.Clear();
        _lastTaskId = null;
        // Cached per run, not per poll: the sweep is over every grabbable in the lab
        // (~79 of them), and the set only changes when the stage is rebuilt.
        _allHighlights = Object.FindObjectsByType<HoverHighlight>(
            FindObjectsInactive.Include);
    }

    private HoverHighlight[] _allHighlights;

    /// Everything currently in a hand, so a GRAB is an edge and not a level. Reuses the
    /// per-run HoverHighlight sweep that the spotlight already pays for.
    private readonly HashSet<Transform> _held = new HashSet<Transform>();
    private string _lastTaskId;

    /// Fired when the player picks up something the current step does NOT want.
    ///
    /// ⚠ PREVENTION, NOT PUNISHMENT. It must never call RecordMistake: nothing has gone
    /// wrong yet — they are holding a bottle, not pouring it — and telling them now is the
    /// difference between a correction and a penalty. Silent outside a guided step, so
    /// browsing the bench between steps stays quiet.
    private void CheckWrongGrab(string taskId)
    {
        if (_allHighlights == null || string.IsNullOrEmpty(taskId)) return;
        var wanted = TaskTargetRegistry.Targets(taskId);
        if (wanted.Count == 0) return;

        for (int i = 0; i < _allHighlights.Length; i++)
        {
            var hh = _allHighlights[i];
            if (hh == null) continue;
            var t = hh.transform;
            bool held = IsHeld(t);
            if (!held) { _held.Remove(t); continue; }
            if (!_held.Add(t)) continue;                 // already counted this grab
            if (hh.IsGuided) continue;                   // the right thing: say nothing

            bool isTarget = false;
            for (int k = 0; k < wanted.Count && !isTarget; k++) isTarget = wanted[k].transform == t;
            if (isTarget) continue;

            string want = null;
            for (int k = 0; k < wanted.Count && want == null; k++)
                if (wanted[k].transform != null && wanted[k].role == TargetRole.Source)
                    want = Mishandling.DisplayNameFor(wanted[k].transform.gameObject);

            FloatingText.Show(
                want != null ? "Not that one — you need " + want : "Not that one for this step",
                t.position + Vector3.up * 0.16f, new Color(1f, 0.85f, 0.5f), 1.2f);
            LabHaptics.Pulse(0.35f, 0.05f);              // a nudge, NOT the mistake buzz
        }
    }

    /// A soft directional ping from whatever the step needs.
    ///
    /// A visual-only cue gives a player looking the wrong way
    /// nothing. Fires ONCE on a step change — never on a loop, which is the fastest way
    /// to make someone mute the game — and reuses an existing SoundBank key so it costs no
    /// new audio asset.
    private void PingTarget(string taskId)
    {
        var t = TaskTargetRegistry.PickTarget(taskId);
        if (t == null) return;
        AudioService.TryPlayAt("ui-click",
            ExperimentSceneBuilder.SolidWorldBounds(t.gameObject).center, 0.35f);
    }

    /// Push every grabbable that is NOT part of the current step into the background.
    /// Only touches objects whose state actually changes, so a step that lasts a minute
    /// costs one pass, not one per poll.
    private void ApplySpotlight(bool on)
    {
        if (_allHighlights == null) return;
        for (int i = 0; i < _allHighlights.Length; i++)
        {
            var hh = _allHighlights[i];
            if (hh == null) continue;
            hh.SetDimmed(on && !hh.IsGuided);
        }
    }

    /// Pure rule: should this target be glowing right now?
    /// A SOURCE goes quiet the moment it is in hand — you got the message, and leaving
    /// it lit while you carry it just adds noise. A DESTINATION or TOOL stays lit,
    /// because holding the right test tube is not the same as finishing the step.
    public static bool ShouldLight(TaskTarget target, bool held, bool taskAvailable)
    {
        if (!taskAvailable) return false;
        return !held || target.stayLitWhenHeld;
    }

    /// Pure gate: a longProcess step fades the screen to black, and a glowing bottle
    /// floating on black reads as a bug rather than a hint.
    public static bool GuidanceAllowed(bool running, bool skipping) => running && !skipping;

    private void Update()
    {
        // The glow runs in every mode, alongside
        // the floor path and the beacon. The COACH stays Tutorial-only — a stuck ladder and a
        // no-score summary belong to practice, the cue that says "this glass" does not.
        if (Time.time < _nextPoll) return;
        _nextPoll = Time.time + pollSeconds;

        bool running = runner != null && runner.Graph != null && runner.IsRunning;
        if (!GuidanceAllowed(running, TimeSkipController.IsSkipping)) { ClearAll(); return; }

        // Self-heal: a restart path that re-furnishes the stage without re-firing
        // ExperimentStarted would otherwise leave us pointing at destroyed objects.
        if (TaskTargetRegistry.TaskCount == 0) TutorialTargets.Build();

        // One id drives the conditional cues: the step the player is actually on.
        string current = null;
        foreach (var t0 in runner.Graph.AvailableTasks()) { current = t0.taskId; break; }
        if (current != _lastTaskId)
        {
            _lastTaskId = current;
            PingTarget(current);
        }
        CheckWrongGrab(current);

        _wanted.Clear();
        var cam = Camera.main;
        Vector3 from = cam != null ? cam.transform.position : Vector3.zero;
        foreach (var task in runner.Graph.AvailableTasks())
        {
            // EVERY available task, not just the first: the graph can open parallel
            // branches, and hiding one would teach the player a stricter order than
            // the experiment actually has. (The waypoint still shows a single arrow.)
            var targets = TaskTargetRegistry.Targets(task.taskId);
            // A POOL of interchangeable tubes collapses to ONE glow — the tube in hand,
            // else the nearest (never the whole rack). Same chooser as
            // PickTarget, so the glow and the arrow cannot point at different tubes.
            var chosen = TaskTargetRegistry.ChoosePoolMember(targets, IsHeld, from);
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.transform == null) continue;
                if (t.pool && t.transform != chosen) continue;
                bool held = IsHeld(t.transform);
                if (held) { _droppedAt.Remove(t.transform); }
                else if (!_lit.ContainsKey(t.transform)
                         && _droppedAt.TryGetValue(t.transform, out float when)
                         && Time.time - when < regrabDelay)
                {
                    continue;      // just fumbled it — hold off re-lighting for a beat
                }

                if (ShouldLight(t, held, true)) _wanted[t.transform] = t;
                else if (held) _droppedAt[t.transform] = Time.time;
            }
        }

        foreach (var kv in _lit)                                   // leaving the set
            if (kv.Key != null && !_wanted.ContainsKey(kv.Key)) SetGuide(kv.Key, false, kv.Value.role);
        foreach (var kv in _wanted)                                // entering the set
            if (!_lit.ContainsKey(kv.Key)) SetGuide(kv.Key, true, kv.Value.role);

        _lit.Clear();
        foreach (var kv in _wanted) _lit[kv.Key] = kv.Value;

        // Spotlight AFTER the guide flags are set, so IsGuided is already true on the
        // objects that must stay bright. Only while something is actually guided —
        // dimming the whole lab during a wrap-up step with no target would just look
        // like the lights failed.
        ApplySpotlight(_lit.Count > 0);
    }

    /// Held right now? XRI's own select state is the truth — DropRespawn and the rack
    /// snapping both freeze released objects kinematic, so a rigidbody check would lie.
    public static bool IsHeld(Transform t)
    {
        if (t == null) return false;
        var grab = t.GetComponent<XRGrab>();
        return grab != null && grab.isSelected;
    }

    private const string XrayChildName = "TutorialGlowShell";

    [Tooltip("PharmaSynth/GuideGlow materials — assigned by Build Tutorial Scene Wiring.")]
    [SerializeField] private Material glowSourceMaterial;      // amber: go fetch this
    [SerializeField] private Material glowTargetMaterial;      // green: put it here

    public void SetGlowMaterials(Material source, Material target)
    { glowSourceMaterial = source; glowTargetMaterial = target; }

    private Material GlowFor(TargetRole role)
        => role == TargetRole.Source ? glowSourceMaterial : glowTargetMaterial;

    private void SetGuide(Transform t, bool on, TargetRole role)
    {
        if (t == null) return;
        var hh = t.GetComponent<HoverHighlight>();
        if (hh != null) hh.SetGuide(on, role);

        // The glow shell goes on EVERY guided object, not just the hidden ones: its
        // rim pass is what makes an item read as lit rather than repainted, and it is
        // wanted in plain sight. The through-wall pass inside the same shader costs
        // nothing when the object is unobstructed — it simply draws nothing.
        if (on) AddGlow(t, role); else RemoveGlow(t);
    }

    private readonly Dictionary<Transform, List<GameObject>> _xray = new Dictionary<Transform, List<GameObject>>();

    /// A pulsing additive shell over the target's solid meshes: a fresnel rim where the
    /// object is visible, a flat ghost where it is hidden (both passes live in
    /// PharmaSynth/GuideGlow). Tinting the object's own base colour was not enough —
    /// it read as "this bottle is orange now" rather than as a light.
    ///
    /// Each shell is parented to the mesh it copies with **identity local TRS**, which
    /// makes it match exactly and follow the object for free. The first version parented
    /// everything under one root and assigned `localScale = mf.lossyScale` — feeding a
    /// WORLD scale into a LOCAL field under an already-scaled parent, so the target's own
    /// scale was applied twice and every shell came out oversized.
    ///
    /// NOT a URP Renderer Feature: that needs targets moved onto a dedicated layer, and
    /// layers here are load-bearing for XRI's interaction masks.
    private void AddGlow(Transform t, TargetRole role)
    {
        var mat = GlowFor(role);
        if (mat == null || _xray.ContainsKey(t)) return;

        var ghosts = new List<GameObject>();
        foreach (var mf in t.GetComponentsInChildren<MeshFilter>(false))
        {
            // One shared definition of "part of the body" — effect children (pour
            // streams, liquid columns, powder mounds) and TEXT meshes are excluded.
            if (!ExperimentSceneBuilder.IsSolidMesh(mf)) continue;
            if (mf.gameObject.name == XrayChildName) continue;

            var ghost = new GameObject(XrayChildName);
            ghost.transform.SetParent(mf.transform, false);   // identity local TRS = exact match
            ghost.hideFlags = HideFlags.DontSave;
            ghost.layer = mf.gameObject.layer;
            ghost.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var mr = ghost.AddComponent<MeshRenderer>();
            // One material per SUBMESH. A renderer with a single material draws only
            // submesh 0, so a two-part prop (bottle + cap, flask + neck) would glow
            // in half. sharedMaterials, never .material — that instances in edit mode.
            int subs = Mathf.Max(1, mf.sharedMesh.subMeshCount);
            var mats = new Material[subs];
            for (int i = 0; i < subs; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            ghosts.Add(ghost);
        }
        _xray[t] = ghosts;
    }

    private void RemoveGlow(Transform t)
    {
        if (t == null || !_xray.TryGetValue(t, out var ghosts)) return;
        foreach (var g in ghosts)
        {
            if (g == null) continue;
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }
        _xray.Remove(t);
    }

    private void ClearAll()
    {
        ApplySpotlight(false);          // lights back up, always — even if nothing was lit
        if (_lit.Count == 0 && _xray.Count == 0) return;
        foreach (var kv in _lit) SetGuide(kv.Key, false, kv.Value.role);
        // Belt and braces: a target destroyed mid-run leaves no _lit entry to clear
        // through, and a stranded shell would hang in the air forever.
        foreach (var t in new List<Transform>(_xray.Keys)) RemoveGlow(t);
        _lit.Clear();
        _droppedAt.Clear();
    }

    private void OnDisable() => ClearAll();
}
