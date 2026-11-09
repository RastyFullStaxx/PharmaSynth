using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// What the lab does during a showcase beat. Every action is undone when the reel ends.
/// Appended values only: reel assets store the action as its index.
public enum ShowcaseAction { None, OpenDoor, LightBurner, Pour, PharmeeCelebrate, Timelapse, Lineup, CloseDoor, Night }

/// The ending's results, read from the save and written into the reel's cards (suite-pinned).
public static class EndingMath
{
    /// First campaign completion of a save plays it once; the demo button plays it any time.
    public static bool ShouldPlay(bool allComplete, bool seen, bool forced) => forced || (allComplete && !seen);

    /// Best grade per experiment, in catalog order; a missing record reads 0.
    public static List<int> Grades(ProgressSaveData data, ExperimentPeriod? period = null)
    {
        var list = new List<int>();
        foreach (var e in ExperimentCatalog.Entries)
        {
            if (period.HasValue && e.period != period.Value) continue;
            ModuleRecord rec = null;
            if (data != null && data.modules != null)
                foreach (var m in data.modules) if (m != null && m.moduleId == e.moduleId) { rec = m; break; }
            list.Add(rec != null ? Mathf.FloorToInt(rec.bestGrade) : 0);
        }
        return list;
    }

    public static int Passed(ProgressSaveData data)
    {
        int n = 0;
        foreach (var e in ExperimentCatalog.Entries)
            if (data != null && data.modules != null)
                foreach (var m in data.modules) if (m != null && m.moduleId == e.moduleId && m.passed) { n++; break; }
        return n;
    }

    /// Floored mean of the best grades (the grade screen floors too).
    public static int Average(ProgressSaveData data)
    {
        var g = Grades(data);
        if (g.Count == 0) return 0;
        float sum = 0f; foreach (var x in g) sum += x;
        return Mathf.FloorToInt(sum / g.Count);
    }

    /// Replace the reel's tokens: {passed} {total} {average} and {Tutorial} {Prelim}
    /// {Midterm} {Final} (that period's best grades, "96 · 92").
    public static string Fill(string text, ProgressSaveData data)
    {
        if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0) return text;
        text = text.Replace("{passed}", Passed(data).ToString())
                   .Replace("{total}", ExperimentCatalog.Count.ToString())
                   .Replace("{average}", Average(data).ToString());
        foreach (ExperimentPeriod p in Enum.GetValues(typeof(ExperimentPeriod)))
            text = text.Replace("{" + p + "}", string.Join(" · ", Grades(data, p)));
        return text;
    }

    /// Stand-in results for a demo, where nothing has really been graded.
    public static ProgressSaveData Sample()
    {
        var d = new ProgressSaveData();
        int[] g = { 98, 95, 93, 96, 92, 94, 91, 97, 95 };
        int i = 0;
        foreach (var e in ExperimentCatalog.Entries)
            d.modules.Add(new ModuleRecord { moduleId = e.moduleId, bestGrade = g[i++ % g.Length], passed = true, attempts = 1 });
        return d;
    }
}

/// Pure rules for the arrival reel (suite-pinned).
public static class ArrivalMath
{
    /// Holding skip this long ends the whole reel; a tap does nothing.
    public const float SkipHoldSeconds = 1f;
    /// Simulated hours the timelapse beat covers (three day and night cycles).
    public const float TimelapseHours = 72f;
    public const float TimelapseScale = 6f;

    /// First entry of a save plays it; a demo session plays it every time.
    public static bool ShouldPlay(bool seen, bool demo) => demo || !seen;

    public static bool SkipDone(float heldSeconds) => heldSeconds >= SkipHoldSeconds;

    /// Millilitres poured by fraction k of a pour beat (eased, so the stream starts gently).
    public static float PouredMl(float k, float totalMl) => totalMl * ShotMath.Ease(k);
}

/// The hand-off across the cube room → lab scene load.
public static class ArrivalFlow
{
    /// Set by the cube-room half just before it loads the lab; the lab half takes it.
    public static bool LabPartPending;
    public static bool Playing;
    /// The gatekeeper holds the spawn and welcome while this is true.
    public static bool Busy => LabPartPending || Playing;
}

/// The showcase reels: silent cinematic tours (cards and captions, no voice) played on
/// the cinema screen, so the headset view is never moved.
///
/// ARRIVAL, between the cube room and the lab door. Two halves, one per scene: the
/// cube-room half ends on black and loads the lab, the lab half picks up under that
/// black and ends with the player at the front door, where the gatekeeper's normal
/// welcome takes over.
/// ENDING (lab only), once per save when the whole campaign is passed, then the
/// gatekeeper's voiced celebration; the demo HUD's Play Ending replays it any time. Its
/// cards carry EndingMath tokens filled from the save.
///
/// Lives on the scene's CutsceneMarkers root beside its CinemaView.
/// Built by Tools ▸ PharmaSynth ▸ Cutscenes ▸ Build Showcase Reels.
public class ShowcaseDirector : MonoBehaviour
{
    [SerializeField] private CutsceneData reel;
    [Tooltip("Lab scene only: the campaign-complete reel.")]
    [SerializeField] private CutsceneData ending;
    [Tooltip("The lab half: plays on load when the cube-room half handed over.")]
    [SerializeField] private bool labPart;
    [Tooltip("Played under the lab half (the cube room's own music carries the first half).")]
    [SerializeField] private AudioClip music;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.55f;
    [SerializeField] private float fadeSeconds = 0.6f;

    public CutsceneData Reel => reel;
    public CutsceneData Ending => ending;
    public bool LabPart => labPart;
    public bool Playing { get; private set; }
    public void Bind(CutsceneData r, bool lab, AudioClip m, CutsceneData end = null) { reel = r; labPart = lab; music = m; ending = end; }

    /// Results the playing reel's tokens are filled from (null = no tokens filled).
    private ProgressSaveData _results;

    /// The campaign-complete reel. Plays when the whole campaign is passed for the first
    /// time on this save, or always when `forced` (the demo button, with sample results).
    /// `done` runs after it, or straight away when nothing plays. True when it plays.
    public bool TryPlayEnding(bool forced, Action done)
    {
        var s = new ProgressionService();
        s.Load();
        bool all = ProgressionFlow.Create(s).AllComplete();
        if (Playing || !EndingMath.ShouldPlay(all, s.Data.endingSeen, forced) || !CanPlay(ending))
        {
            done?.Invoke();
            return false;
        }
        _results = forced && !all ? EndingMath.Sample() : s.Data;
        StartCoroutine(Run(ending, skipped =>
        {
            if (!forced) { s.Load(); s.Data.endingSeen = true; s.Save(); }
            _results = null;
            if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(fadeSeconds);
            done?.Invoke();
        }));
        return true;
    }

    public static ShowcaseDirector Find() => FindAnyObjectByType<ShowcaseDirector>();

    private CinemaView _cinema;
    private AudioSource _musicSource;
    private InputAction _skip;
    private float _held;
    private bool _skipped;
    private readonly List<Action> _undo = new List<Action>();

    /// Enter the lab from the cube room: the arrival reel first when it is due, else the
    /// plain fade. Every lab entry in the cube room goes through here.
    public static void EnterLab(string labScene, bool demo)
    {
        var service = new ProgressionService();
        service.Load();
        var arrival = Find();
        if (arrival != null && ArrivalMath.ShouldPlay(service.Data.arrivalSeen, demo) && arrival.PlayThenLoad(labScene))
            return;
        ScreenFader.FadeOutThen(() => SceneManager.LoadScene(labScene));
    }

    /// The cube-room half: play, then load the lab under black. False when it can't play.
    public bool PlayThenLoad(string labScene)
    {
        if (!CanPlay(reel)) return false;
        StartCoroutine(Run(reel, skipped =>
        {
            if (skipped) MarkSeen();
            ArrivalFlow.LabPartPending = !skipped;
            ScreenFader.HoldBlackOnStart = !skipped;
            SceneManager.LoadScene(labScene);
        }));
        return true;
    }

    private void Start()
    {
        if (!labPart || !ArrivalFlow.LabPartPending) return;
        ArrivalFlow.Playing = true;
        ArrivalFlow.LabPartPending = false;
        if (!CanPlay(reel)) { FinishLab(false); return; }
        StartCoroutine(Run(reel, FinishLab));
    }

    private bool CanPlay(CutsceneData r)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || r == null || r.beats == null || r.beats.Count == 0) return false;
        if (_cinema == null) _cinema = GetComponent<CinemaView>();
        if (_cinema == null) _cinema = FindAnyObjectByType<CinemaView>();
        return _cinema != null && Camera.main != null;
    }

    /// The lab half is over: remember it was seen, release the gatekeeper (it lands the
    /// player at the front door while the screen is still black), then fade in.
    private void FinishLab(bool skipped)
    {
        MarkSeen();
        ArrivalFlow.Playing = false;
        ScreenFader.HoldBlackOnStart = false;
        StartCoroutine(FadeInAfterSpawn());
    }

    private IEnumerator FadeInAfterSpawn()
    {
        yield return null;   // the gatekeeper's spawn routine runs its teleport this frame
        yield return null;
        if (ScreenFader.Instance != null) ScreenFader.Instance.FadeIn(fadeSeconds);
    }

    private static void MarkSeen()
    {
        var s = new ProgressionService();
        s.Load();
        if (s.Data.arrivalSeen) return;
        s.Data.arrivalSeen = true;
        s.Save();
    }

    private IEnumerator Run(CutsceneData reel, Action<bool> after)
    {
        Playing = true;
        if (reel == this.reel) ArrivalFlow.Playing = true;
        _skipped = false; _held = 0f;
        EnableSkip();
        yield return Fade(1f);
        var first = reel.beats[0].shot;
        var opening = _cinema.MarkerPose(first != null ? first.camera : null);
        if (opening is ShotMath.Pose o) _cinema.SetPose(o, first.fov);
        _cinema.Enter(Camera.main);
        HidePlayer();
        StartMusic();
        yield return Fade(0f);

        for (int i = 0; i < reel.beats.Count && !_skipped; i++)
        {
            var b = reel.beats[i];
            if (b == null) continue;
            _cinema.SetTitle(EndingMath.Fill(b.cardTitle, _results), EndingMath.Fill(b.cardLine, _results));
            _cinema.SetSubtitle(EndingMath.Fill(b.caption, _results));
            var shot = b.shot ?? new CinemaShot();
            ShotMath.Ends(shot.blend, CurrentPose(), _cinema.MarkerPose(shot.camera), _cinema.MarkerPose(shot.moveTo),
                          out var start, out var end);
            float fov0 = _cinema.ShotCamera.fieldOfView;
            float dur = Mathf.Max(0.2f, b.seconds);
            var tick = BeginAction(b);
            // Real seconds: the timelapse speeds the lab up, never the reel.
            for (float t = 0f; t < dur && !_skipped; t += Time.unscaledDeltaTime)
            {
                float k = t / dur;
                Vector3? look = _cinema.Resolve(shot.look, shot.lookMarker, null, null);
                _cinema.SetPose(ShotMath.Evaluate(start, end, shot.blend, look, k),
                                shot.blend ? Mathf.Lerp(fov0, shot.fov, ShotMath.Ease(k)) : shot.fov);
                tick?.Invoke(k);
                PollSkip();
                yield return null;
            }
            EndTimelapse();
        }

        yield return Fade(1f);
        StopMusic();
        UndoActions();
        _cinema.SetTitle("", "");
        _cinema.SetSubtitle("");
        _cinema.Exit();
        DisableSkip();
        if (reel == this.reel) ArrivalFlow.Playing = false;
        Playing = false;
        after?.Invoke(_skipped);
    }

    private ShotMath.Pose CurrentPose()
    {
        var t = _cinema.ShotCamera.transform;
        return new ShotMath.Pose(t.position, t.rotation);
    }

    private IEnumerator Fade(float alpha)
    {
        var f = ScreenFader.Instance;
        if (f == null || !f.isActiveAndEnabled) yield break;
        bool done = false;
        if (alpha > 0.5f) f.FadeOut(fadeSeconds, () => done = true);
        else f.FadeIn(fadeSeconds, () => done = true);
        for (float t = 0f; !done && t < fadeSeconds + 2f; t += Time.unscaledDeltaTime) yield return null;
    }

    // ── Hold to skip ───────────────────────────────────────────────────────

    private void EnableSkip()
    {
        if (_skip == null)
        {
            _skip = new InputAction("ArrivalSkip", InputActionType.Button);
            _skip.AddBinding("<XRController>{LeftHand}/secondaryButton");
            _skip.AddBinding("<XRController>{RightHand}/secondaryButton");
            _skip.AddBinding("<Keyboard>/escape");
        }
        _skip.Enable();
    }

    private void DisableSkip() { _skip?.Disable(); }
    private void OnDestroy() { _skip?.Dispose(); _skip = null; }

    private void PollSkip()
    {
        _held = _skip != null && _skip.IsPressed() ? _held + Time.unscaledDeltaTime : 0f;
        if (ArrivalMath.SkipDone(_held)) _skipped = true;
    }

    // ── Music ──────────────────────────────────────────────────────────────

    private void StartMusic()
    {
        if (music == null) return;
        if (_musicSource == null) _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.clip = music; _musicSource.loop = true; _musicSource.spatialBlend = 0f;
        _musicSource.volume = musicVolume;
        _musicSource.Play();
    }

    private void StopMusic() { if (_musicSource != null) _musicSource.Stop(); }

    /// The player stands in the corridor the reel films, so their hands and pointer rays
    /// would appear in shot. Hide everything the rig draws except the theatre itself and
    /// the fade quad (which the fades still need); the undo list shows it all again.
    private void HidePlayer()
    {
        var rig = CinemaView.RigOf(Camera.main);
        if (rig == null) return;
        int theatre = CinemaView.Layer;
        // The interactors' line visuals switch their LineRenderer back on every frame, so
        // the visuals themselves are paused too (the interactors keep running).
        foreach (var line in rig.GetComponentsInChildren<LineRenderer>())
            foreach (var b in line.GetComponents<MonoBehaviour>())
            {
                if (!b.enabled || !b.GetType().Name.Contains("Visual")) continue;
                var bb = b;
                bb.enabled = false;
                _undo.Add(() => { if (bb != null) bb.enabled = true; });
            }
        foreach (var r in rig.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r.gameObject.layer == theatre || r.name == "FadeQuad") continue;
            var rr = r;
            rr.enabled = false;
            _undo.Add(() => { if (rr != null) rr.enabled = true; });
        }
    }

    // ── Showcase actions (each one registers its own undo) ─────────────────

    private float _savedTimeScale = 1f;
    private bool _timelapse;

    /// Start the beat's action; returns a per-frame tick (k = 0..1 through the beat) or null.
    private Action<float> BeginAction(CutsceneData.Beat b)
    {
        string[] names = (b.actionTarget ?? "").Split('|');
        switch (b.action)
        {
            case ShowcaseAction.OpenDoor:
            {
                var door = FindAnyObjectByType<DoorOpener>();
                if (door == null) return null;
                door.SetOpen(true);
                _undo.Add(() => door.SetOpen(false));
                return null;
            }
            case ShowcaseAction.LightBurner:
            {
                var burner = Named<BurnerController>(names[0]);
                if (burner == null || burner.IsLit) return null;
                burner.Ignite();
                _undo.Add(burner.Extinguish);
                return null;
            }
            case ShowcaseAction.Pour:
            {
                var from = names.Length > 0 ? Named<LiquidPhysics>(names[0]) : null;
                var into = names.Length > 1 ? Named<LiquidPhysics>(names[1]) : null;
                if (from == null || into == null || from.currentChemical == null) return null;
                var chem = from.currentChemical;
                var before = into.currentChemical; float beforeMl = into.currentLiquidVolume;
                _undo.Add(() => { if (before != null && beforeMl > 0f) into.SetContents(before, beforeMl); else into.ClearContents(); });
                float total = Mathf.Min(into.maxVolume * 0.6f, into.maxVolume - beforeMl), poured = 0f;
                return k =>
                {
                    float want = ArrivalMath.PouredMl(k, total);
                    if (want - poured < 0.5f) return;
                    into.AddLiquid(chem, want - poured, notify: false);   // a display pour, never a task
                    poured = want;
                };
            }
            case ShowcaseAction.CloseDoor:
            {
                var door = FindAnyObjectByType<DoorOpener>();
                if (door != null) door.SetOpen(false);
                return null;
            }
            case ShowcaseAction.Lineup:
            {
                // actionTarget "a|b|c" with chemicals[i] for vessel i: each fills to 60%.
                var chems = b.chemicals ?? new ChemicalData[0];
                for (int i = 0; i < names.Length && i < chems.Length; i++)
                {
                    var into = Named<LiquidPhysics>(names[i]);
                    if (into == null || chems[i] == null) continue;
                    var before = into.currentChemical; float beforeMl = into.currentLiquidVolume;
                    var v = into;
                    _undo.Add(() => { if (before != null && beforeMl > 0f) v.SetContents(before, beforeMl); else v.ClearContents(); });
                    into.SetContents(chems[i], into.maxVolume * 0.6f);
                }
                return null;
            }
            case ShowcaseAction.Night:
            {
                _cinema.SetTint(TimelapseMath.Night);   // held until the reel ends
                return null;
            }
            case ShowcaseAction.PharmeeCelebrate:
            {
                var g = FindAnyObjectByType<PharmeeGestures>();
                if (g != null) g.Play(PharmeeGesture.Celebrate);
                return null;
            }
            case ShowcaseAction.Timelapse:
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = ArrivalMath.TimelapseScale;
                _timelapse = true;
                return k =>
                {
                    _cinema.SetTint(TimelapseMath.Tint(k, ArrivalMath.TimelapseHours));
                    _cinema.SetSubtitle(TimelapseMath.Counter(k, ArrivalMath.TimelapseHours));
                };
            }
            default: return null;
        }
    }

    private void EndTimelapse()
    {
        if (!_timelapse) return;
        _timelapse = false;
        Time.timeScale = _savedTimeScale;
        _cinema.SetTint(Color.white);
    }

    private void UndoActions()
    {
        EndTimelapse();
        for (int i = _undo.Count - 1; i >= 0; i--)
        {
            try { _undo[i](); } catch (Exception e) { Debug.LogWarning("[Arrival] undo failed: " + e.Message); }
        }
        _undo.Clear();
    }

    private void OnDisable()
    {
        if (_cinema != null && _cinema.Active) { UndoActions(); _cinema.Exit(); }
    }

    private static T Named<T>(string name) where T : Component
    {
        if (string.IsNullOrEmpty(name)) return null;
        var go = GameObject.Find(name);
        return go != null ? go.GetComponentInChildren<T>() : null;
    }
}
