#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// Pre-recording copy pass over the CUTSCENE beats. Every beat is a
/// SUBTITLE **and** a text-to-speech script, so a line can be perfectly good on
/// screen and wrong in the ear, or describe apparatus that is not in the scene.
/// Run this before spending voice credits: changing a line changes its
/// VoiceLineId, so the manifest must be re-exported afterwards anyway.
///
/// Edits the assets through SerializedObject rather than the YAML, because these
/// subtitles contain ": " (e.g. "Welcome back! Today: Acetone.") which a
/// hand-written scalar silently truncates.
public static class VoicePolish
{
    /// (find, replace, why). Ordered; applied to every CutsceneData beat subtitle.
    static readonly (string find, string replace, string why)[] Rules =
    {
        // ACCURACY: the thermometer is deleted from the scene as VR-inappropriate
        // scaffolding, so telling the player to watch one is wrong, and
        // Exp 3's cut is driven by the distillation sim, not a readable instrument.
        ("Watch the thermometer through the 70-80 degree window.",
         "Hold the cut steady between 70 and 80 degrees.",
         "thermometer was removed from the lab"),

        // CHEMISTRY — the reagent is Tollens' (Bernhard Tollens); "Tollen's" is a
        // misspelling, and the possessive apostrophe also trips the TTS normaliser.
        ("negative Tollen's", "a negative Tollens test", "Tollens is not a possessive"),

        // TTS — a formula subscript is read letter-by-letter ("em-en-oh-two").
        // Pharmee is speaking aloud; he should say what a demonstrator would say.
        ("filter the MnO2", "filter off the manganese dioxide", "formula read as letters"),
        ("confirm CO2", "confirm carbon dioxide", "formula read as letters"),

        // TTS — a hyphenated range is read as a dash or a subtraction.
        ("the 70-80 degree fraction", "the fraction that comes over between 70 and 80 degrees",
         "hyphen range read as minus"),
    };

    [MenuItem("Tools/PharmaSynth/Voice/Polish Cutscene Copy for Recording")]
    public static void Polish()
    {
        if (Application.isPlaying) { Debug.LogWarning("[VoicePolish] exit Play mode first."); return; }

        var changes = new List<string>();
        foreach (var guid in AssetDatabase.FindAssets("t:CutsceneData",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Cutscenes" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(path);
            if (cs == null) continue;

            var so = new SerializedObject(cs);
            var beats = so.FindProperty("beats");
            if (beats == null || !beats.isArray) continue;
            bool dirty = false;

            for (int i = 0; i < beats.arraySize; i++)
            {
                var sub = beats.GetArrayElementAtIndex(i).FindPropertyRelative("subtitle");
                if (sub == null || string.IsNullOrEmpty(sub.stringValue)) continue;
                string before = sub.stringValue, after = before;
                foreach (var r in Rules)
                    if (after.Contains(r.find)) after = after.Replace(r.find, r.replace);
                if (after == before) continue;
                sub.stringValue = after;
                dirty = true;
                changes.Add(System.IO.Path.GetFileNameWithoutExtension(path) + " beat " + i + ":\n    - " + before + "\n    + " + after);
            }

            if (dirty) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(cs); }
        }

        AssetDatabase.SaveAssets();
        if (changes.Count == 0)
            Debug.Log("<color=#4CD07D>[VoicePolish] cutscene copy already recording-ready — no changes.</color>");
        else
            Debug.Log($"<color=#4CD07D>[VoicePolish] {changes.Count} beat(s) rewritten for recording:</color>\n"
                      + string.Join("\n", changes)
                      + "\n\nRE-EXPORT the voice manifest now — these lines have new ids.");
    }

    /// The gate's dialogue is a [SerializeField], so the SCENE keeps its own copy
    /// of every line and that copy is what the player actually hears. Any edit to
    /// the code defaults silently fails to reach the game, and because the voice
    /// manifest is built from the CODE, the spoken text stops matching any clip
    /// and the line drops back to placeholder blips. A drifted scene copy can also
    /// give wrong instructions (asking for a lab coat alone, while the door
    /// requires coat + goggles + gloves).
    ///
    /// Code is the source of truth: it is reviewed, version-controlled, and what the
    /// manifest and the generated audio were built from. Every change is logged.
    [MenuItem("Tools/PharmaSynth/Voice/Sync Gate Dialogue to Code")]
    public static void SyncGateLines()
    {
        if (Application.isPlaying) { Debug.LogWarning("[GateSync] exit Play mode first."); return; }

        var gate = Object.FindAnyObjectByType<PharmeeGatekeeper>(FindObjectsInactive.Include);
        if (gate == null) { Debug.LogError("[GateSync] no PharmeeGatekeeper in the open scene."); return; }

        var defaults = new PharmeeGatekeeper.GateLines();
        var so = new SerializedObject(gate);
        var lines = so.FindProperty("lines");
        if (lines == null) { Debug.LogError("[GateSync] 'lines' not found on the gatekeeper."); return; }

        var changes = new List<string>();
        foreach (var field in new[]
        {
            "approach", "labTour", "campaignExplain", "episodePrompt", "lockedEpisode",
            "coatPrompt", "readyPrompt", "thresholdWarn", "congrats", "supplyWarn", "welcome",
        })
        {
            var prop = lines.FindPropertyRelative(field);
            if (prop == null) continue;
            string want = (string)typeof(PharmeeGatekeeper.GateLines)
                .GetField(field).GetValue(defaults);
            if (prop.stringValue == want) continue;
            changes.Add(field + "\n    scene was: " + prop.stringValue + "\n    now      : " + want);
            prop.stringValue = want;
        }

        // NO early return here: the TOUR guide is a second, independent source of
        // scene-serialized dialogue, and an in-sync gate must not skip checking it.
        if (changes.Count > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(gate);
        }

        changes.AddRange(SyncTourBeats());

        if (changes.Count == 0) { Debug.Log("<color=#4CD07D>[GateSync] scene dialogue already matches the code — nothing to do.</color>"); return; }

        UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"<color=#4CD07D>[GateSync] {changes.Count} stale line(s) re-synced from code:</color>\n"
                  + string.Join("\n", changes)
                  + "\n\nThese now match the generated clips — no regeneration needed.");
    }

    /// The guided tour has the SAME [SerializeField] trap as the gate: its stops
    /// carry their own beat text, which drifts out of date (e.g. sending the player
    /// to "the equipment cabinet" when every instrument lives on the bench, or to
    /// "the reagent shelf" since emptied into the wall cabinets, with the trigger
    /// landmark still pointing at the emptied shelf). Reseeding fixes the words, the
    /// landmark, and the missing voice clips in one go.
    static List<string> SyncTourBeats()
    {
        var changes = new List<string>();
        var guide = Object.FindAnyObjectByType<LabTourGuide>(FindObjectsInactive.Include);
        if (guide == null) return changes;

        var so = new SerializedObject(guide);

        // Capture the stops BEFORE reseeding (the field is private — read it the
        // serialized way rather than reaching through the class).
        var stops = so.FindProperty("stops");
        var beforeBeat = new List<string>();
        var beforeMark = new List<string>();
        for (int i = 0; stops != null && i < stops.arraySize; i++)
        {
            var el = stops.GetArrayElementAtIndex(i);
            beforeBeat.Add(el.FindPropertyRelative("beat")?.stringValue ?? "");
            beforeMark.Add(el.FindPropertyRelative("landmarkName")?.stringValue ?? "");
        }

        guide.SeedDefaults();          // writes the private field directly
        so.Update();                   // pull the new values back into the SerializedObject

        stops = so.FindProperty("stops");
        for (int i = 0; stops != null && i < stops.arraySize; i++)
        {
            var el = stops.GetArrayElementAtIndex(i);
            string beat = el.FindPropertyRelative("beat")?.stringValue ?? "";
            string mark = el.FindPropertyRelative("landmarkName")?.stringValue ?? "";
            string wasBeat = i < beforeBeat.Count ? beforeBeat[i] : "(no stop)";
            string wasMark = i < beforeMark.Count ? beforeMark[i] : "(none)";
            if (wasBeat == beat && wasMark == mark) continue;
            changes.Add("tour stop " + i + " [" + wasMark + " -> " + mark + "]"
                        + "\n    scene was: " + wasBeat + "\n    now      : " + beat);
        }

        // The intro and closer are separate private fields, so they drift too.
        // DefaultBeatTexts is the single source: [0] opens the tour, [last] closes it.
        var texts = LabTourGuide.DefaultBeatTexts;
        foreach (var pair in new[]
        {
            new[] { "introBeat", texts.Length > 0 ? texts[0] : null },
            new[] { "closerBeat", texts.Length > 0 ? texts[texts.Length - 1] : null },
        })
        {
            if (pair[1] == null) continue;
            var prop = so.FindProperty(pair[0]);
            if (prop == null || prop.stringValue == pair[1]) continue;
            changes.Add("tour " + pair[0] + "\n    scene was: " + prop.stringValue + "\n    now      : " + pair[1]);
            prop.stringValue = pair[1];
        }

        if (changes.Count > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(guide);
        }
        return changes;
    }

    /// Pure (suite): every rule must actually change the text it targets, and the
    /// result must be free of the artefacts we are removing. Guards against a rule
    /// silently going stale when someone rewrites a beat by hand.
    public static string ApplyRules(string subtitle)
    {
        if (string.IsNullOrEmpty(subtitle)) return subtitle;
        string s = subtitle;
        foreach (var r in Rules) if (s.Contains(r.find)) s = s.Replace(r.find, r.replace);
        return s;
    }

    /// Ordinary words that only ever appear capitalised for EMPHASIS. A blanket
    /// all-caps rule is wrong: "PPE" is a genuine initialism and is meant to be
    /// spelled out by the engine, so flagging every caps run would condemn correct
    /// copy. Only real words shouted for stress are the artefact.
    static readonly string[] ShoutedWords =
    { "AND", "OR", "NOT", "ALL", "NEVER", "ALWAYS", "MUST", "ONLY", "BEFORE", "AFTER", "NOW", "DO" };

    /// Pure (suite): copy that is ready to hand to a TTS engine — no formula
    /// subscripts read out as letters, no hyphenated numeric ranges (read as a
    /// dash or a subtraction), no ordinary word shouted in capitals.
    public static bool IsRecordingSafe(string line)
    {
        if (string.IsNullOrEmpty(line)) return true;
        if (line.Contains("MnO2") || line.Contains("CO2") || line.Contains("H2O")
            || line.Contains("H2SO4") || line.Contains("NaOH")) return false;
        if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\d+-\d+")) return false;
        foreach (var w in ShoutedWords)
            if (System.Text.RegularExpressions.Regex.IsMatch(line, @"(?<![A-Za-z])" + w + @"(?![A-Za-z])"))
                return false;
        return true;
    }
}
#endif
