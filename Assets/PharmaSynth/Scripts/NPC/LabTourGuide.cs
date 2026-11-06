using System;
using System.Collections.Generic;
using UnityEngine;

/// Location-triggered Lab Tour (storyboard): instead of narrating on a
/// timer, Pharmee points each area out as the player physically walks up to it —
/// workbench, equipment cabinet, reagent shelf — then signs off once they've
/// seen them all. Started/stopped by PharmeeGatekeeper; speaks through its narration
/// via a callback. Landmarks resolve by name (no scene wiring); if none resolve the
/// gatekeeper falls back to the timed sequence. Pure proximity core is self-tested.
public class LabTourGuide : MonoBehaviour
{
    [Serializable]
    public class Stop
    {
        public string landmarkName;
        [TextArea] public string beat;
        public float radius = 1.8f;
    }

    [SerializeField] private Transform player;            // falls back to Camera.main
    [SerializeField] private float speakCooldown = 3.5f;  // min gap so beats don't stampede
    [SerializeField] private List<Stop> stops = new List<Stop>();
    [SerializeField, TextArea] private string introBeat =
        "Welcome! Let's take a quick tour. The bench in the middle is your main workspace — walk around and I'll point out the rest as you reach them.";
    [SerializeField, TextArea] private string closerBeat =
        "That's the tour! Check your wrist panel for each step during a run, and poke me whenever you're ready to take on a graded campaign.";

    private Action<string> _say;
    private Transform[] _resolved;
    private Vector3[] _pos;
    private float[] _radii;
    private bool[] _visited;
    private bool _active, _closerSaid;
    private int _visitedCount;
    private float _nextSpeakOk;

    public bool IsActive => _active;
    public int VisitedCount => _visitedCount;
    public int StopCount => stops.Count;

    /// The landmark whose beat is being spoken right now, or null between beats.
    ///
    /// Exposed so PharmeeGestures can point at the area being described. The tour itself
    /// still owns all the proximity logic and never touches a transform.
    public Transform CurrentLandmark { get; private set; }

    private void Awake() { if (stops.Count == 0) SeedDefaults(); }

    /// Begin the tour; `say` routes a beat to Pharmee's narration. Returns the number
    /// of landmarks that resolved (0 → caller should use the timed fallback).
    public int Begin(Action<string> say)
    {
        if (stops.Count == 0) SeedDefaults();
        _say = say;
        Resolve();
        _visited = new bool[stops.Count];
        _pos = new Vector3[stops.Count];
        _radii = new float[stops.Count];
        int reachable = 0;
        for (int i = 0; i < stops.Count; i++)
        {
            _radii[i] = stops[i].radius;
            if (_resolved[i] != null) reachable++;
            else { _visited[i] = true; _visitedCount++; }   // unresolvable → skip, don't block the closer
        }
        if (reachable == 0) { _active = false; return 0; }
        _active = true; _closerSaid = false;
        _nextSpeakOk = Time.time + speakCooldown;           // let the intro breathe
        Speak(introBeat);
        return reachable;
    }

    public void End() { _active = false; CurrentLandmark = null; }

    private void Resolve()
    {
        _resolved = new Transform[stops.Count];
        for (int i = 0; i < stops.Count; i++)
        {
            var go = GameObject.Find(stops[i].landmarkName);
            _resolved[i] = go != null ? go.transform : null;
        }
    }

    private void Update()
    {
        if (!_active) return;
        var p = player != null ? player : (Camera.main != null ? Camera.main.transform : null);
        if (p == null || Time.time < _nextSpeakOk) return;

        for (int i = 0; i < _pos.Length; i++)
            _pos[i] = _resolved[i] != null ? _resolved[i].position : new Vector3(1e6f, 0f, 1e6f);

        int idx = FirstUnvisitedInRange(p.position, _pos, _visited, _radii);
        if (idx >= 0)
        {
            _visited[idx] = true; _visitedCount++;
            CurrentLandmark = _resolved[idx];
            Speak(stops[idx].beat);
            _nextSpeakOk = Time.time + speakCooldown;
            return;
        }
        if (_visitedCount >= stops.Count && !_closerSaid)
        {
            _closerSaid = true;
            CurrentLandmark = null;      // the closer is not about any one landmark
            Speak(closerBeat);
            _nextSpeakOk = Time.time + speakCooldown;
        }
    }

    private void Speak(string s) { if (!string.IsNullOrEmpty(s)) _say?.Invoke(s); }

    /// Pure (self-tested): index of the first unvisited landmark within its radius of
    /// the player (horizontal distance), or -1 if none is in range.
    public static int FirstUnvisitedInRange(Vector3 playerPos, Vector3[] landmarkPos, bool[] visited, float[] radii)
    {
        if (landmarkPos == null) return -1;
        for (int i = 0; i < landmarkPos.Length; i++)
        {
            if (visited[i]) continue;
            Vector3 d = landmarkPos[i] - playerPos; d.y = 0f;
            if (d.sqrMagnitude <= radii[i] * radii[i]) return i;
        }
        return -1;
    }

    /// The default tour copy, exposed for the voice-over corpus (index 0 = the
    /// intro, last = the closer, the middle entries are the location beats).
    public static readonly string[] DefaultBeatTexts =
    {
        "Welcome! Let's take a quick tour. The bench in the middle is your main workspace — walk around and I'll point out the rest as you reach them.",
        "This bench is your main workspace. Flick your wrist face-up and glance at it — your holographic procedures board appears with every step, live. The progress bar and timer up top track your pace.",
        "Every instrument stays out on this bench, every experiment — picking the right one is part of the work. Flick your wrist anytime to check the next step.",
        "The reagent cabinets hold your chemicals — take only what each step needs. And Settings up top lets you tune audio, text size and comfort whenever you like.",
        "That's the tour! Check your wrist panel for each step during a run, and poke me whenever you're ready to take on a graded campaign.",
    };

    /// Default stops — the storyboard's tour areas, each folding in a UI tip so every
    /// beat has a physical trigger. Names match the SampleScene landmarks.
    public void SeedDefaults()
    {
        stops = new List<Stop>
        {
            new Stop { landmarkName = "DynamicStage", radius = 2.2f, beat = DefaultBeatTexts[1] },
            new Stop { landmarkName = "EquipmentShelf", radius = 2.0f, beat = DefaultBeatTexts[2] },
            // The west ReagentShelf cubby was emptied into the east cabinets
            // — the tour must stop where the bottles actually are.
            new Stop { landmarkName = "ReagentCabinets", radius = 2.4f, beat = DefaultBeatTexts[3] },
        };
    }
}
