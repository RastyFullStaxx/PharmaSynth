#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Softens how far Dr. Jimenez throws his arms, so his lab coat stops visibly dragging
/// (it reads as the coat torn open at the hip).
///
/// ⛔ WHY THIS EXISTS INSTEAD OF A RE-WEIGHT. Moving the coat's vertex weights off the arm
/// bones does not work on this asset: the coat and the sleeve are one continuous surface with
/// no seam to cut along, so any weight change big enough to free the coat is big enough to rip
/// it (measured against the untouched mesh, worst edge stretch 4.2x → 14.9x and torn edges
/// 32 → 174). Smoothing the transfer across a band removes the tearing but then barely moves
/// the coat (1625 → 1557 dragged vertices). The geometry, not the weighting, is the limit.
/// See JimenezRigMath.TransferFraction.
///
/// So this attacks the OTHER side of the equation: the coat only drags as far as the arm
/// swings. His clips are HUMANOID, so the knobs are muscle curves (-1..1 across each joint's
/// range), not bone rotations — a bone-name scan finds nothing here.
///
/// ⛔ Never compounds: the first run copies each clip to Originals/ and EVERY run re-derives
/// from that copy, so re-running at a different factor cannot stack. Delete the damped clips
/// and restore from Originals/ to undo.
public static class JimenezArmDamper
{
    const string Dir = "Assets/PharmaSynth/Art/Generated/Animations";
    const string Backups = Dir + "/Originals";

    static readonly string[] Clips = { "Jimenez_Idle", "Jimenez_Talk", "Jimenez_Walk" };

    /// How much of the original swing survives. 0.55 keeps him visibly animated — he is a
    /// proctor who gestures while he talks — while cutting the reach that drags the coat.
    public const float DefaultFactor = 0.55f;

    /// Muscles that move the ARM. Shoulder is included (it carries the whole limb) but the
    /// spine, head and legs are left completely alone.
    public static bool IsArmMuscle(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return false;
        return propertyName.Contains("Arm")          // "Left Arm Down-Up", "Left Forearm Stretch"
               || propertyName.Contains("Shoulder")
               || propertyName.StartsWith("LeftHand")   // the hand IK goal (LeftHandT/Q)
               || propertyName.StartsWith("RightHand");
    }

    [MenuItem("Tools/PharmaSynth/Damp Jimenez Arm Swing")]
    public static void Run() => Run(DefaultFactor);

    public static void Run(float factor)
    {
        Directory.CreateDirectory(Backups);
        var sb = new StringBuilder();
        sb.AppendLine("[ArmDamper] factor " + factor.ToString("0.00")
                      + " (1 = untouched, 0 = arms frozen at their opening pose)");

        foreach (var name in Clips)
        {
            string livePath = Dir + "/" + name + ".anim";
            string backupPath = Backups + "/" + name + ".anim";
            var live = AssetDatabase.LoadAssetAtPath<AnimationClip>(livePath);
            if (live == null) { sb.AppendLine("  " + name + ": not found"); continue; }

            // First run takes the pristine copy; every run reads from it.
            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(backupPath) == null)
                AssetDatabase.CopyAsset(livePath, backupPath);
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>(backupPath);
            if (source == null) { sb.AppendLine("  " + name + ": could not back up"); continue; }

            int damped = 0;
            float wasMax = 0f, nowMax = 0f;
            foreach (var b in AnimationUtility.GetCurveBindings(source))
            {
                if (!IsArmMuscle(b.propertyName)) continue;
                var curve = AnimationUtility.GetEditorCurve(source, b);
                if (curve == null || curve.keys.Length == 0) continue;

                var keys = curve.keys;
                // ⛔ The MEAN of the curve, not keys[0]. Damping toward the opening pose
                // leaves the opening pose untouched by construction, and this clip OPENS at
                // its widest reach, so a first-key reference changes nothing on screen.
                // The mean is the centre the gesture swings about, so pulling toward it
                // narrows the swing at both extremes.
                float rest = 0f;
                foreach (var k in keys) rest += k.value;
                rest /= keys.Length;
                for (int i = 0; i < keys.Length; i++)
                {
                    wasMax = Mathf.Max(wasMax, Mathf.Abs(keys[i].value - rest));
                    keys[i].value = JimenezRigMath.Damp(keys[i].value, rest, factor);
                    keys[i].inTangent *= factor;     // or the eased curve overshoots the keys
                    keys[i].outTangent *= factor;
                    nowMax = Mathf.Max(nowMax, Mathf.Abs(keys[i].value - rest));
                }
                AnimationUtility.SetEditorCurve(live, b, new AnimationCurve(keys));
                damped++;
            }
            EditorUtility.SetDirty(live);
            sb.AppendLine("  " + name.PadRight(14) + damped + " arm curve(s), widest swing "
                          + wasMax.ToString("0.00") + " -> " + nowMax.ToString("0.00") + " (muscle units)");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(sb.ToString());
    }
}
#endif
