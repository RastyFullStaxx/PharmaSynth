#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Imports the generated voice clips and wires the bank into the scene:
///   1. Quest-friendly import settings on Audio/Voice/** (mono, Vorbis).
///   2. Rebuilds VoiceBank.asset from Audio/Voice/<Speaker>/<id>.mp3|wav.
///   3. Points every NPCNarrationController in the open scene at the bank —
///      controllers under Dr. Jimenez speak as Jimenez, everything else as
///      Pharmee. Missing clips keep today's blip+typewriter. Idempotent.
public static class VoiceImportTool
{
    const string VoiceDir = "Assets/PharmaSynth/Audio/Voice";
    public const string BankPath = "Assets/PharmaSynth/ScriptableObjects/VoiceBank.asset";

    [MenuItem("Tools/PharmaSynth/Voice/Import & Wire Voice Clips")]
    public static void ImportAndWire()
    {
        // ⛔ REFRESH FIRST. The generation script writes the mp3s from PowerShell, OUTSIDE
        // Unity, so until the AssetDatabase has imported them LoadAssetAtPath returns null
        // and this tool silently skips them. It then reports a healthy clip count while
        // the new lines still speak in blips.
        AssetDatabase.Refresh();

        var bank = AssetDatabase.LoadAssetAtPath<VoiceBank>(BankPath);
        if (bank == null)
        {
            bank = ScriptableObject.CreateInstance<VoiceBank>();
            AssetDatabase.CreateAsset(bank, BankPath);
        }
        bank.entries.Clear();

        int clips = 0;
        foreach (VoiceSpeaker speaker in System.Enum.GetValues(typeof(VoiceSpeaker)))
        {
            string dir = VoiceDir + "/" + speaker;
            if (!Directory.Exists(dir)) continue;
            foreach (string file in Directory.GetFiles(dir))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".mp3" && ext != ".wav" && ext != ".ogg") continue;
                string assetPath = file.Replace('\\', '/');

                var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                if (importer != null)
                {
                    bool dirty = false;
                    if (!importer.forceToMono) { importer.forceToMono = true; dirty = true; }
                    var s = importer.defaultSampleSettings;
                    if (s.compressionFormat != AudioCompressionFormat.Vorbis || !Mathf.Approximately(s.quality, 0.45f))
                    {
                        s.compressionFormat = AudioCompressionFormat.Vorbis;
                        s.quality = 0.45f;
                        s.loadType = AudioClipLoadType.CompressedInMemory;
                        importer.defaultSampleSettings = s;
                        dirty = true;
                    }
                    if (dirty) importer.SaveAndReimport();
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                if (clip == null) continue;
                bank.entries.Add(new VoiceBank.Entry
                {
                    speaker = speaker,
                    id = Path.GetFileNameWithoutExtension(file),
                    clip = clip,
                });
                clips++;
            }
        }
        bank.Rebuild();
        EditorUtility.SetDirty(bank);
        AssetDatabase.SaveAssets();

        // Wire every narration channel in the open scene.
        int wired = 0;
        foreach (var narration in Object.FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include))
        {
            var who = VoiceSpeaker.Pharmee;
            for (var t = narration.transform; t != null; t = t.parent)
                if (t.name.ToLowerInvariant().Contains("jimenez")) { who = VoiceSpeaker.Jimenez; break; }
            var so = new SerializedObject(narration);
            so.FindProperty("voiceBank").objectReferenceValue = bank;
            so.FindProperty("speaker").enumValueIndex = (int)who;
            so.ApplyModifiedPropertiesWithoutUndo();
            wired++;
        }

        EditorSceneManager.MarkAllScenesDirty();
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[VoiceImport] {clips} clips in the bank, {wired} narration channels wired "
                  + "(missing clips fall back to blips + typewriter).");
    }
}
#endif
