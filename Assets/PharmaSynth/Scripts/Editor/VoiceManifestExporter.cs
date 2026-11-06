#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Exports the full voice-over manifest (NPCs speak): every
/// code-authored line (VoiceCorpus) plus every cutscene beat, one row per
/// unique (speaker, text) with its stable id. Tools/voice/generate-voice.ps1
/// consumes the manifest; changed lines re-key and regenerate individually.
public static class VoiceManifestExporter
{
    const string OutPath = "Assets/PharmaSynth/Audio/Voice/voice-manifest.json";

    [System.Serializable]
    public class ManifestLine { public string id; public string speaker; public string text; public int chars; public string group; }

    /// Generation ORDER: Dr. Jimenez before Pharmee, stopping only when the credits
    /// run out. The PowerShell generator
    /// walks the manifest top-to-bottom and skips what already exists, so writing
    /// Jimenez's rows first means a run that dies on an exhausted quota has spent
    /// every credit on him — no ordering logic needed in the script itself.
    /// Pure + pinned: lower sorts first.
    public static int SpeakerPriority(string speaker) => speaker == "Jimenez" ? 0 : 1;

    [System.Serializable]
    public class Manifest { public List<ManifestLine> lines = new List<ManifestLine>(); }

    [MenuItem("Tools/PharmaSynth/Voice/Export Voice Manifest")]
    public static void Export()
    {
        var manifest = new Manifest();
        var seen = new HashSet<string>();

        void Add(VoiceSpeaker speaker, string text, string group)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            string id = VoiceLineId.For(text);
            string key = speaker + ":" + id;
            if (!seen.Add(key)) return;
            manifest.lines.Add(new ManifestLine
            {
                id = id,
                speaker = speaker.ToString(),
                text = VoiceLineId.Normalize(text),
                chars = VoiceLineId.Normalize(text).Length,
                group = group,
            });
        }

        foreach (var l in VoiceCorpus.CodeLines()) Add(l.speaker, l.text, l.group);
        // ⛔ DATA-authored lines too. Task time-skip narration ("One week later...") lives
        // in the module assets, so exporting CodeLines() alone leaves six of Jimenez's lines
        // out of every manifest, unbought and speaking in blips.
        foreach (var l in VoiceCorpus.DataLines(
                     AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
                         "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset")))
            Add(l.speaker, l.text, l.group);

        // Cutscene beats (Pharmee narrates all four cutscenes per module).
        int beats = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CutsceneData", new[] { "Assets/PharmaSynth/ScriptableObjects/Cutscenes" }))
        {
            var cs = AssetDatabase.LoadAssetAtPath<CutsceneData>(AssetDatabase.GUIDToAssetPath(guid));
            if (cs == null || cs.beats == null) continue;
            foreach (var b in cs.beats)
                if (b != null) { Add(VoiceSpeaker.Pharmee, b.subtitle, "Cutscene"); beats++; }
        }

        // STEP INSTRUCTIONS: the lines Pharmee says constantly DURING an experiment,
        // and by far the most-heard dialogue in the game. They live on the module
        // assets (graphTasks[].hint), so runtime code cannot enumerate them; left out
        // of the corpus, every one of them plays as a placeholder blip.
        // PharmeeBrain.InstructionFor is the exact transform the game
        // applies, so exporting through it guarantees the hashes match at runtime.
        int steps = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition"))
        {
            var m = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (m == null || m.graphTasks == null) continue;
            foreach (var t in m.graphTasks)
            {
                if (t == null) continue;
                string line = PharmeeBrain.InstructionFor(t);
                if (string.IsNullOrWhiteSpace(line)) continue;
                Add(VoiceSpeaker.Pharmee, line, "Steps");
                steps++;
            }
        }

        // Jimenez first (stable within each speaker — OrderBy is a stable sort, so
        // the corpus order is preserved and re-exports don't shuffle the queue).
        manifest.lines = new List<ManifestLine>(
            System.Linq.Enumerable.OrderBy(manifest.lines, l => SpeakerPriority(l.speaker)));

        Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
        File.WriteAllText(OutPath, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutPath);

        int chars = 0, pharmee = 0, jimenez = 0, jChars = 0;
        foreach (var l in manifest.lines)
        {
            chars += l.chars;
            if (l.speaker == "Pharmee") pharmee++; else { jimenez++; jChars += l.chars; }
        }
        // eleven_flash_v2_5 ≈ 0.5 credits per character.
        // Per-group cost, so a scene can be bought one at a time (-Group on the script).
        var byGroup = new Dictionary<string, (int n, int c)>();
        foreach (var l in manifest.lines)
        {
            string g = string.IsNullOrEmpty(l.group) ? "Misc" : l.group;
            byGroup.TryGetValue(g, out var cur);
            byGroup[g] = (cur.n + 1, cur.c + l.chars);
        }
        var groupLines = new List<string>();
        foreach (var kv in byGroup)
            groupLines.Add($"    {kv.Key,-11} {kv.Value.n,3} lines  {kv.Value.c,6:n0} chars  ~{kv.Value.c / 2,5:n0} credits");
        groupLines.Sort();

        Debug.Log($"[VoiceManifest] {manifest.lines.Count} unique lines ({jimenez} Jimenez FIRST, {pharmee} Pharmee, "
                  + $"{beats} cutscene beats + {steps} step instructions folded in), {chars:n0} characters ≈ {chars / 2:n0} ElevenLabs credits "
                  + $"on eleven_flash_v2_5. Jimenez alone: {jChars:n0} characters ≈ {jChars / 2:n0} credits.\n"
                  + "  Per group (generate-voice.ps1 -Group <name>):\n" + string.Join("\n", groupLines)
                  + $"\n  Wrote {OutPath}.");
    }
}
#endif
