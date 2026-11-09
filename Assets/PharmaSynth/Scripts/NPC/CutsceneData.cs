using System;
using System.Collections.Generic;
using UnityEngine;

/// A data-driven cutscene: a sequence of subtitle "beats" with a Pharmee expression
/// and duration. A beat may carry a camera SHOT; a cutscene with any shot plays as a
/// cinematic (CinemaView: a separate camera films the lab onto a screen in a dark
/// room — the XR camera itself is never animated). No shots = subtitles only.
[CreateAssetMenu(fileName = "Cutscene", menuName = "PharmaSynth/Cutscene")]
public class CutsceneData : ScriptableObject
{
    /// Appended values only: assets store the kind as its index.
    public enum Kind { Intro, ReagentPrep, Success, Failure, Arrival, Ending }

    [Serializable]
    public class Beat
    {
        [TextArea(1, 3)] public string subtitle;
        [Min(0.2f)] public float seconds = 3f;
        public PharmeeFaceExpression face = PharmeeFaceExpression.Neutral;
        public CinemaShot shot = new CinemaShot();

        // Silent on-screen text for the arrival reel (the subtitle above is SPOKEN by
        // Pharmee's narration channel). Both show in the text band under the picture: the
        // card in the title size, the caption in the caption size.
        public string cardTitle = "";
        public string cardLine = "";
        public string caption = "";
        /// Something the lab does during this beat (light a burner, open the door).
        public ShowcaseAction action = ShowcaseAction.None;
        /// The scene object(s) the action works on, by name; "a|b" = from a into b.
        public string actionTarget = "";
        /// Lineup: the chemical each named vessel is filled with, in the same order.
        public ChemicalData[] chemicals;
    }

    public Kind kind = Kind.Intro;
    [TextArea(1, 2)] public string title = "";
    public List<Beat> beats = new List<Beat>();

    /// True when any beat frames a shot — the director then plays it in the cinema view.
    public bool IsCinematic
    {
        get
        {
            if (beats == null) return false;
            for (int i = 0; i < beats.Count; i++)
                if (beats[i] != null && beats[i].shot != null && beats[i].shot.HasCamera) return true;
            return false;
        }
    }

    public float TotalDuration()
    {
        float t = 0f;
        for (int i = 0; i < beats.Count; i++) if (beats[i] != null) t += Mathf.Max(0.2f, beats[i].seconds);
        return t;
    }
}
