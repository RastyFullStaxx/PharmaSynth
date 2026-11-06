#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Asks the one question the run simulator structurally cannot: a step can be
/// mechanically perfect while the bottle it needs sits inside a closed cabinet or above
/// head height. `SimulatedRun` reaches every object by reference, so it never
/// notices; otherwise only a headset session would.
///
/// Its input is already built and already verified: `TutorialTargets.Build()` resolves
/// taskId → the objects each step is about, for all 9 modules. So the audit asks, of
/// every object a step needs, "could a player standing in this room actually get to it?"
///
/// Deliberately GEOMETRIC rather than a navmesh walk. The player has continuous
/// locomotion over the whole lab floor, so "can I stand near it" is nearly always true;
/// what goes wrong is height and enclosure, which two raycasts answer.
public static class ReachabilityAudit
{
    // A standing adult in VR, arms working. Above the hard ceiling nothing is grabbable;
    // between soft and hard it is a stretch worth flagging but not a blocker.
    public const float HardHigh = 2.1f, SoftHigh = 1.9f;
    public const float HardLow = 0.05f, SoftLow = 0.25f;

    /// How far past its own surface an object must have clear air on SOME side before we
    /// call it enclosed. A closed cabinet blocks every direction within a few cm.
    public const float Clearance = 0.30f;

    public enum Verdict { Fine, Awkward, Unreachable }

    /// Pure: the height rule, so the suite can pin the band without a scene.
    public static Verdict HeightVerdict(float y)
    {
        if (y > HardHigh || y < HardLow) return Verdict.Unreachable;
        if (y > SoftHigh || y < SoftLow) return Verdict.Awkward;
        return Verdict.Fine;
    }

    /// Pure: enclosed only when EVERY probed direction is blocked. One open side is
    /// enough — that is how a cabinet with its front off, or an open shelf, works, and
    /// flagging those would bury the real failures in noise.
    public static bool IsEnclosed(bool[] blockedPerDirection)
    {
        if (blockedPerDirection == null || blockedPerDirection.Length == 0) return false;
        foreach (bool b in blockedPerDirection) if (!b) return false;
        return true;
    }

    static readonly Vector3[] Probes =
    { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };

    /// Cast outward from the object's middle and report which directions are walled in.
    /// Hits on the object's OWN colliders are ignored — otherwise everything with a lid
    /// or a cap reports itself enclosed.
    public static bool[] ProbeDirections(GameObject go, Bounds b)
    {
        var blocked = new bool[Probes.Length];
        float radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z));
        for (int i = 0; i < Probes.Length; i++)
        {
            var hits = Physics.RaycastAll(b.center, Probes[i], radius + Clearance,
                                          ~0, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.collider == null) continue;
                if (h.collider.transform.IsChildOf(go.transform)) continue;   // its own body
                blocked[i] = true;
                break;
            }
        }
        return blocked;
    }

    [MenuItem("Tools/PharmaSynth/Audit Reachability")]
    public static void RunMenu()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Reach] exit Play mode first."); return; }
        var log = new StringBuilder();
        var findings = RunAll(log);
        System.IO.Directory.CreateDirectory("Logs");
        System.IO.File.WriteAllText("Logs/reachability-audit.txt", log.ToString());
        Debug.Log((findings.Count == 0 ? "<color=#4CD07D>" : "<color=#FF7A6B>")
                  + "[Reach] " + (findings.Count == 0 ? "every step's objects are reachable"
                                                      : findings.Count + " finding(s)")
                  + "</color>\n  → Logs/reachability-audit.txt");
    }

    /// Every module, every step, every object. Returns the findings; the log carries the
    /// detail. Mutates the open scene (it builds each stage) — reopen SampleScene after.
    public static List<string> RunAll(StringBuilder log)
    {
        var findings = new List<string>();
        var builder = Object.FindAnyObjectByType<ExperimentSceneBuilder>();
        var lib = AssetDatabase.LoadAssetAtPath<ExperimentLibrary>(
            "Assets/PharmaSynth/ScriptableObjects/ExperimentLibrary.asset");
        if (builder == null || lib == null)
        {
            log.AppendLine("[Reach] need ExperimentSceneBuilder + ExperimentLibrary — open SampleScene first.");
            return findings;
        }

        log.AppendLine("--- reachability: can the player physically get to what each step needs? ---");
        foreach (var guid in AssetDatabase.FindAssets("t:ExperimentModuleDefinition",
                     new[] { "Assets/PharmaSynth/ScriptableObjects/Experiments" }))
        {
            var def = AssetDatabase.LoadAssetAtPath<ExperimentModuleDefinition>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (def == null || def.graphTasks == null || def.graphTasks.Count == 0) continue;

            builder.Build(def.moduleId);
            TutorialTargets.Build();

            // One object can serve several steps; report it once per module, not per step.
            var seen = new HashSet<Transform>();
            int checkedCount = 0;
            foreach (var task in def.graphTasks)
            {
                if (task == null) continue;
                foreach (var t in TaskTargetRegistry.Targets(task.taskId))
                {
                    if (t.transform == null || !seen.Add(t.transform)) continue;
                    checkedCount++;
                    var go = t.transform.gameObject;
                    // SolidWorldBounds, never a raw renderer sweep: LiquidPourer's
                    // world-space stream children outlive a pour pointing at the FLOOR
                    // and would drag the measured centre down a metre (→ Gotchas).
                    var b = ExperimentSceneBuilder.SolidWorldBounds(go);

                    var v = HeightVerdict(b.center.y);
                    if (v != Verdict.Fine)
                    {
                        string line = def.moduleId + " · " + task.taskId + " · " + go.name
                                      + ": centre at y=" + b.center.y.ToString("0.00") + " m — "
                                      + (v == Verdict.Unreachable ? "UNREACHABLE" : "awkward reach");
                        log.AppendLine("  " + (v == Verdict.Unreachable ? "BUG  " : "WARN ") + line);
                        if (v == Verdict.Unreachable) findings.Add(line);
                    }

                    if (IsEnclosed(ProbeDirections(go, b)))
                    {
                        string line = def.moduleId + " · " + task.taskId + " · " + go.name
                                      + ": walled in on all six sides — the player cannot get a hand to it";
                        log.AppendLine("  BUG  " + line);
                        findings.Add(line);
                    }
                }
            }
            log.AppendLine("  " + def.moduleId + ": " + checkedCount + " object(s) checked");
        }

        TaskTargetRegistry.Clear();
        if (findings.Count == 0) log.AppendLine("  -> every object every step needs is within reach.");
        return findings;
    }
}
#endif
