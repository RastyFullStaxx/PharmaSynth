#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Wires Pharmee's robot voice colouring onto HIS narration channel only
/// Dr. Jimenez is a human examiner and must stay untouched — the
/// whole point of the two-NPC contrast is that one is a machine and one is not.
///
/// Ring modulation does the robot work (RobotVoiceFx); Unity's stock filters do
/// the colouring — a band limit so he sounds like he is coming through a speaker
/// grille, and a short chorus for the metallic doubling. All of it is live-tunable
/// in the Inspector during Play, so the character can be dialled in by ear without
/// regenerating a single clip.
public static class RobotVoiceBuilder
{
    const string Menu = "Tools/PharmaSynth/Voice/";
    const string ProfilePath = "Assets/PharmaSynth/ScriptableObjects/RobotVoiceProfile.asset";

    /// Get-or-add, done the way Unity actually requires.
    ///
    /// ⛔ NEVER write `GetComponent&lt;T&gt;() ?? AddComponent&lt;T&gt;()`. The `??` and `?.`
    /// operators use the CLR's reference null, which bypasses UnityEngine.Object's
    /// overloaded `==`. A destroyed-or-broken component comes back as a "fake null"
    /// that is not reference-null, so `??` happily hands it back and the very next
    /// property write throws a NullReferenceException (it does on RobotNPC, a
    /// GameObject carrying missing-script references).
    /// An explicit `== null` check uses Unity's operator and is correct.
    static T Ensure<T>(GameObject go) where T : Component
    {
        var existing = go.GetComponent<T>();
        if (existing != null) return existing;          // Unity's ==, not the CLR's
        var added = go.AddComponent<T>();
        if (added == null)
            Debug.LogWarning("[RobotVoice] could not add " + typeof(T).Name + " to " + go.name
                           + " — that channel keeps its unprocessed voice.");
        return added;
    }

    /// Load-or-create the ONE shared tuning asset every Pharmee channel reads.
    /// An EXISTING asset is returned untouched — its values are hand tuning,
    /// and this builder must never overwrite them. Only a brand-new asset gets the
    /// class defaults (which are the chosen ring-mod character).
    static RobotVoiceProfile EnsureProfile()
    {
        var p = AssetDatabase.LoadAssetAtPath<RobotVoiceProfile>(ProfilePath);
        if (p != null) return p;
        p = ScriptableObject.CreateInstance<RobotVoiceProfile>();
        AssetDatabase.CreateAsset(p, ProfilePath);
        AssetDatabase.SaveAssets();
        return p;
    }

    [MenuItem(Menu + "Robotise Pharmee's Voice")]
    public static void Apply()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RobotVoice] exit Play mode first."); return; }

        var profile = EnsureProfile();
        var notes = new System.Collections.Generic.List<string>();
        int done = 0;
        foreach (var src in PharmeeVoiceSources())
        {
            Undo.RegisterFullObjectHierarchyUndo(src.gameObject, "Robotise Pharmee");

            var fx = Ensure<RobotVoiceFx>(src.gameObject);
            if (fx != null) fx.profile = profile;   // every channel reads the SAME asset

            // ⛔ DO NOT re-tune an existing profile here. Overwriting the shared asset
            // whenever its values differ from the latest defaults throws away the hand
            // tuning on every run. Defaults belong at CREATION
            // only (see EnsureProfile); after that the asset is hand-owned.

            // ⚠ Band limits decide how muffled the voice sounds.
            // Consonants (s, t, f, th) live between 5 and 10 kHz, so a 5 kHz ceiling
            // removes exactly the detail that makes speech sound crisp. The top is open
            // to 11 kHz (still suggests a speaker grille without eating sibilance) and the
            // bottom sits at 180 Hz so he has some body instead of sounding thin.
            var hp = Ensure<AudioHighPassFilter>(src.gameObject);
            if (hp != null) hp.cutoffFrequency = 180f;
            var lp = Ensure<AudioLowPassFilter>(src.gameObject);
            if (lp != null) lp.cutoffFrequency = 11000f;

            // Chorus SMEARS transients and dulls the voice, so it is kept
            // only as a hint of metal: dry and short.
            var ch = Ensure<AudioChorusFilter>(src.gameObject);
            if (ch != null)
            {
                ch.delay = 12f; ch.rate = 1.2f; ch.depth = 0.10f;
                ch.dryMix = 0.85f; ch.wetMix1 = 0.15f; ch.wetMix2 = 0.05f; ch.wetMix3 = 0f;
            }

            // Distortion DULLS. Grit comes from the bit-crush in RobotVoiceFx instead,
            // which adds it by BRIGHTENING. Remove any existing one so a re-run undoes a
            // muffled build rather than layering on it.
            var stale = src.GetComponent<AudioDistortionFilter>();
            if (stale != null) Object.DestroyImmediate(stale);

            // NEVER pitch-shift speech on an AudioSource: Unity's pitch also changes
            // SPEED, so it would make him gabble rather than sound synthetic.
            src.pitch = 1f;

            EditorUtility.SetDirty(src.gameObject);
            done++;
        }

        if (done == 0)
        {
            Debug.LogError("[RobotVoice] no Pharmee narration AudioSource found — is the scene open, "
                         + "and has 'Wire NPC Polish' run?");
            return;
        }
        Save();
        Debug.Log($"<color=#4CD07D>[RobotVoice] robot colouring applied to {done} Pharmee voice source(s), all reading "
                  + $"the ONE shared profile at {ProfilePath}. Character = RING MODULATION (ringMix is the "
                  + "'fan' strength). An existing profile is left ALONE — your tuning is never overwritten. "
                  + "Tune it by ear during Play; the values stick. Dr. Jimenez untouched.</color>"
                  + (notes.Count > 0 ? "\n  " + string.Join("\n  ", notes) : ""));
    }

    [MenuItem(Menu + "Remove Pharmee Robot Voice (A/B)")]
    public static void Remove()
    {
        if (Application.isPlaying) { Debug.LogWarning("[RobotVoice] exit Play mode first."); return; }
        int done = 0;
        foreach (var src in PharmeeVoiceSources())
        {
            foreach (var c in new Component[]
            {
                src.GetComponent<RobotVoiceFx>(), src.GetComponent<AudioHighPassFilter>(),
                src.GetComponent<AudioLowPassFilter>(), src.GetComponent<AudioChorusFilter>(),
                src.GetComponent<AudioDistortionFilter>(),
            })
                if (c != null) { Object.DestroyImmediate(c); done++; }
        }
        Save();
        Debug.Log($"[RobotVoice] removed {done} component(s) — Pharmee is back to the raw read.");
    }

    /// Pharmee's narration AudioSources: every NPCNarrationController in the scene
    /// EXCEPT the ones under Dr. Jimenez. Matches VoiceImportTool's own rule, so a
    /// channel can never be robotised and voiced as Jimenez at the same time.
    static System.Collections.Generic.List<AudioSource> PharmeeVoiceSources()
    {
        var list = new System.Collections.Generic.List<AudioSource>();
        foreach (var n in Object.FindObjectsByType<NPCNarrationController>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (n == null || IsJimenez(n.transform)) continue;
            var so = new SerializedObject(n);
            var src = so.FindProperty("narratorAudioSource")?.objectReferenceValue as AudioSource;
            if (src == null) src = n.GetComponent<AudioSource>();
            if (src != null && !list.Contains(src)) list.Add(src);
        }
        return list;
    }

    static bool IsJimenez(Transform t)
    {
        for (var p = t; p != null; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("jimenez") || n.Contains("examiner") || n.Contains("proctor")) return true;
        }
        return false;
    }

    static void Save()
    {
        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
    }
}
#endif
