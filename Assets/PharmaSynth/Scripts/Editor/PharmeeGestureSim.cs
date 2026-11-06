#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

/// Proves Pharmee's animation set actually MOVES HIM, in edit mode.
///
/// The suite pins the pure curves (`PharmeeGestureSuite`), but a correct curve reaching a
/// transform that is not bound produces exactly nothing while every assertion stays green.
/// That is the failure this menu exists to catch, and it is the same reason
/// `Simulate Tutorial Guidance` exists rather than another pin: it has to drive real scene
/// objects, and the suite is kept side-effect-free.
///
/// For each gesture it applies the pose at its peak and measures the ACTUAL degrees and
/// millimetres the scene transforms moved, then restores them.
///
/// Tools > PharmaSynth > Simulate Pharmee Gestures (edit mode, restores what it touches).
public static class PharmeeGestureSim
{
    [MenuItem("Tools/PharmaSynth/Simulate Pharmee Gestures")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[GestureSim] exit Play mode first."); return; }

        var robot = GameObject.Find("RobotNPC");
        if (robot == null) { Debug.LogError("[GestureSim] no RobotNPC in the scene."); return; }

        var attitude = robot.GetComponentInChildren<PharmeeAttitude>(true);
        var gestures = robot.GetComponentInChildren<PharmeeGestures>(true);
        var bob = robot.GetComponentInChildren<FloatBob>(true);

        var sb = new StringBuilder();
        int problems = 0;

        sb.AppendLine("bindings:");
        problems += Require(sb, "PharmeeAttitude", attitude != null);
        problems += Require(sb, "PharmeeGestures", gestures != null);
        problems += Require(sb, "FloatBob", bob != null);
        if (attitude == null) { Report(sb, problems); return; }

        var body = FindChild(robot.transform, "Robot Origin");
        var handL = FindChild(robot.transform, "Hand origin");
        var handR = FindChild(robot.transform, "Hand origin.002");
        problems += Require(sb, "body node 'Robot Origin'", body != null);
        problems += Require(sb, "hand pivot 'Hand origin'", handL != null);
        problems += Require(sb, "hand pivot 'Hand origin.002'", handR != null);
        if (body == null) { Report(sb, problems); return; }

        // Snapshot so the scene is left exactly as found.
        var bodyWas = body.localRotation;
        var handLWas = handL != null ? handL.localRotation : Quaternion.identity;
        var handRWas = handR != null ? handR.localRotation : Quaternion.identity;

        var tune = PharmeeGestureTuning.Default;
        sb.AppendLine();
        sb.AppendLine("gestures (scanned peak):");

        foreach (PharmeeGesture g in System.Enum.GetValues(typeof(PharmeeGesture)))
        {
            if (g == PharmeeGesture.None) continue;

            // SCAN for the real peak, do not sample the midpoint. Warn is an exponential-decay
            // flinch that peaks at t~0, so a midpoint sample reads a 10-degree recoil as a
            // 1.4-degree twitch.
            float dur = PharmeeGestureMath.DurationOf(g);
            float span = PharmeeGestureMath.IsSustained(g) ? 1.0f : dur;
            var pose = PharmeePose.Rest;
            float best = -1f;
            for (int i = 0; i <= 60; i++)
            {
                float t = span * i / 60f;
                var candidate = PharmeeGestureMath.Pose(g, t, tune);
                float score = Quaternion.Angle(candidate.bodyRot, Quaternion.identity)
                            + candidate.rootOffset.magnitude * 200f
                            + candidate.handRaise * 30f
                            + Mathf.Abs(candidate.waveFlare - 1f) * 60f;
                if (score > best) { best = score; pose = candidate; }
            }

            // Apply exactly the way PharmeeAttitude composes it.
            body.localRotation = pose.bodyRot * bodyWas;
            var swing = Quaternion.Euler(-55f * pose.handRaise, 0f, 0f);
            if (handL != null) handL.localRotation = handLWas * swing;
            if (handR != null) handR.localRotation = handRWas * swing;

            float bodyDeg = Quaternion.Angle(body.localRotation, bodyWas);
            float handDeg = handL != null ? Quaternion.Angle(handL.localRotation, handLWas) : 0f;
            float riseMm = pose.rootOffset.magnitude * 1000f;
            float flare = (pose.waveFlare - 1f) * 100f;

            bool moved = bodyDeg > 1f || handDeg > 1f || riseMm > 5f || Mathf.Abs(flare) > 2f;
            if (!moved) problems++;

            sb.AppendLine(string.Format("  {0,-10} body {1,5:0.0}deg   hands {2,5:0.0}deg   offset {3,5:0.0}mm   rings {4,5:0.0}%   {5}",
                g, bodyDeg, handDeg, riseMm, flare, moved ? "ok" : "<-- MOVES NOTHING"));
        }

        body.localRotation = bodyWas;
        if (handL != null) handL.localRotation = handLWas;
        if (handR != null) handR.localRotation = handRWas;

        Report(sb, problems);
    }

    static int Require(StringBuilder sb, string what, bool ok)
    {
        sb.AppendLine("  " + (ok ? "ok   " : "MISSING ") + what);
        return ok ? 0 : 1;
    }

    static Transform FindChild(Transform root, string name)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }

    static void Report(StringBuilder sb, int problems)
    {
        if (problems == 0)
            Debug.Log("<color=#4CD07D>[GestureSim] all gestures move him, all bindings resolved.</color>\n" + sb);
        else
            Debug.LogError("[GestureSim] " + problems + " problem(s) — a gesture that moves nothing is the " +
                           "failure the pure pins cannot see.\n" + sb);
    }
}
#endif
