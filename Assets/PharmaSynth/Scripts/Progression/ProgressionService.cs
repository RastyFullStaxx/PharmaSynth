using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// Per-module progress record (best attempt).
[Serializable]
public class ModuleRecord
{
    public string moduleId;
    public float bestGrade;     // 0..100
    public float bestMastery;   // 0..1
    public bool passed;         // ever cleared the two-part gate
    public int attempts;
}

/// Versioned save payload.
[Serializable]
public class ProgressSaveData
{
    public int version = 1;
    public List<ModuleRecord> modules = new List<ModuleRecord>();
    /// The arrival reel has played (or been skipped) for this save.
    public bool arrivalSeen;
}

/// Tracks which experiments the player has passed and persists it (versioned JSON
/// with a backup slot, corruption-safe). Drives the mastery gate / period-door
/// unlocking. Plain C# — the save path is injectable so it is unit-testable.
public class ProgressionService
{
    public const int CurrentVersion = 1;

    private readonly string _path;
    private readonly string _backupPath;
    private ProgressSaveData _data = new ProgressSaveData();

    /// Default location: Application.persistentDataPath/pharmasynth_progress.json —
    /// remapped to the throwaway _demo file while a demo session is active, so
    /// every default-path consumer (menu, gatekeeper, recorder, results screen)
    /// reads/writes demo progress without touching the real save. Tests pass an
    /// explicit path.
    public ProgressionService(string path = null)
    {
        _path = string.IsNullOrEmpty(path)
            ? DemoMode.SavePathFor(DemoSession.Active,
                Path.Combine(Application.persistentDataPath, "pharmasynth_progress.json"))
            : path;
        _backupPath = _path + ".bak";
    }

    public ProgressSaveData Data => _data;

    public ModuleRecord GetRecord(string moduleId)
    {
        for (int i = 0; i < _data.modules.Count; i++)
            if (_data.modules[i].moduleId == moduleId) return _data.modules[i];
        return null;
    }

    public bool IsPassed(string moduleId)
    {
        var r = GetRecord(moduleId);
        return r != null && r.passed;
    }

    /// A module is unlocked if it has no prerequisite, or its prerequisite is passed.
    public bool IsUnlocked(string moduleId, string prerequisiteModuleId)
    {
        if (string.IsNullOrEmpty(prerequisiteModuleId)) return true;
        return IsPassed(prerequisiteModuleId);
    }

    /// Fold an attempt result into the record (keeps the best), bumps attempts,
    /// and persists. `passed` latches true once earned.
    public ModuleRecord RecordResult(string moduleId, ExperimentResult result, bool autoSave = true)
    {
        var r = GetRecord(moduleId);
        if (r == null)
        {
            r = new ModuleRecord { moduleId = moduleId };
            _data.modules.Add(r);
        }
        r.attempts++;
        if (result.grade.Total > r.bestGrade) r.bestGrade = result.grade.Total;
        if (result.overallMastery > r.bestMastery) r.bestMastery = result.overallMastery;
        if (result.passed) r.passed = true;
        if (autoSave) Save();
        return r;
    }

    // ---- Persistence -----------------------------------------------------

    public void Save()
    {
        try
        {
            _data.version = CurrentVersion;
            string json = JsonUtility.ToJson(_data, true);
            // Keep the previous good file as a backup before overwriting.
            if (File.Exists(_path))
                File.Copy(_path, _backupPath, true);
            File.WriteAllText(_path, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ProgressionService] Save failed: " + e.Message);
        }
    }

    /// Load from disk. Falls back to the backup on parse failure, then to empty.
    public void Load()
    {
        if (TryLoadFrom(_path)) return;
        if (TryLoadFrom(_backupPath))
        {
            Debug.LogWarning("[ProgressionService] Primary save unreadable; recovered from backup.");
            return;
        }
        _data = new ProgressSaveData();
    }

    private bool TryLoadFrom(string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var loaded = JsonUtility.FromJson<ProgressSaveData>(File.ReadAllText(path));
            if (loaded == null) return false;
            if (loaded.modules == null) loaded.modules = new List<ModuleRecord>();
            // Migration hook for future versions would go here.
            _data = loaded;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void ResetAll()
    {
        _data = new ProgressSaveData();
        Save();
    }
}
