using System;
using UnityEngine;

/// What a cinematic shot keeps its eye on. Resolved at play time by CinemaTargets.
public enum CinemaLook { None, Pharmee, Bench, Product, Marker }

/// One camera shot, attached to a cutscene beat; it lasts the beat's seconds.
/// Positions are NAMES of empty transforms under the scene's `CutsceneMarkers`
/// root, so shots are hand-tunable in the scene and the cutscene assets stay data.
[Serializable]
public class CinemaShot
{
    [Tooltip("Marker the camera starts at. Empty + no blend = hold the previous framing.")]
    public string camera = "";
    [Tooltip("Optional marker the camera dollies to over the beat.")]
    public string moveTo = "";
    [Tooltip("Glide from wherever the camera is to `camera` instead of cutting.")]
    public bool blend;
    public CinemaLook look = CinemaLook.None;
    [Tooltip("Marker name when look = Marker.")]
    public string lookMarker = "";
    [Range(20f, 90f)] public float fov = 50f;
    [Tooltip("Pharmee pops out of Marker_PharmeeCorner at the start of this beat.")]
    public bool pharmeePopIn;

    public bool HasCamera => !string.IsNullOrEmpty(camera) || blend;
}

/// Pure camera math for cinematic cutscenes (suite-pinned). Eased position, level
/// horizon (never a roll), and a look-at that is re-aimed every frame so a pan stays
/// locked on a moving or distant target.
public static class ShotMath
{
    public struct Pose { public Vector3 pos; public Quaternion rot; public Pose(Vector3 p, Quaternion r) { pos = p; rot = r; } }

    public static float Ease(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

    /// Level look rotation (roll 0). Falls back to `fallback` when the target sits on the eye.
    public static Quaternion LookAt(Vector3 eye, Vector3 target, Quaternion fallback)
    {
        Vector3 d = target - eye;
        if (d.sqrMagnitude < 1e-6f) return fallback;
        if (Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.999f) return fallback;   // straight up/down: no stable up
        return Quaternion.LookRotation(d, Vector3.up);
    }

    /// Where a shot starts and ends. Cut = start at the `camera` marker; blend = start
    /// where the camera already is and glide to `camera`. `moveTo` (if any) is the end.
    public static void Ends(bool blend, Pose current, Pose? cameraMarker, Pose? moveTo,
                            out Pose start, out Pose end)
    {
        start = blend || !cameraMarker.HasValue ? current : cameraMarker.Value;
        if (moveTo.HasValue) end = moveTo.Value;
        else if (blend && cameraMarker.HasValue) end = cameraMarker.Value;
        else end = start;
    }

    /// Camera pose at t01 through the shot. With a look target the aim is recomputed
    /// from the CURRENT position each frame: a cut is locked on it throughout, a blend
    /// eases its rotation onto it and arrives locked.
    public static Pose Evaluate(Pose start, Pose end, bool blend, Vector3? look, float t01)
    {
        float e = Ease(t01);
        Vector3 pos = Vector3.LerpUnclamped(start.pos, end.pos, e);
        Quaternion target = look.HasValue ? LookAt(pos, look.Value, end.rot) : end.rot;
        if (!blend && look.HasValue) return new Pose(pos, target);   // a cut stays locked on its subject
        return new Pose(pos, Quaternion.Slerp(start.rot, target, e)); // a blend turns onto it as it glides
    }
}
