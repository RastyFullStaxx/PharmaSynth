using UnityEngine;
using HapticPlayer = UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics.HapticImpulsePlayer;

/// Semantic haptics: the hands should be able to tell RIGHT from WRONG without
/// looking.
///
/// The VR affordance pass already gives every interactor a generic grab/poke buzz, so
/// picking up the correct bottle and picking up the wrong one felt exactly the same —
/// and in VR the score bar and the toast are both easy to miss while you are looking
/// at your hands. These two cues ride alongside the audio that already fires for the
/// same events, so there is one place per event rather than a parallel system.
///
/// Deliberately two DISTINCT shapes rather than a rhythm: a short crisp tick for
/// progress and a long coarse buzz for a mistake are told apart instantly, and neither
/// needs a coroutine to play a second beat.
public static class LabHaptics
{
    // Progress: brief and light — it should confirm, not interrupt.
    public const float StepAmplitude = 0.45f, StepSeconds = 0.06f;
    // Mistake: longer and stronger — it should be impossible to miss.
    public const float ErrorAmplitude = 0.85f, ErrorSeconds = 0.22f;

    private static HapticPlayer[] _players;

    /// Cached because FindObjectsByType every step would be waste. Re-resolves if the
    /// rig is rebuilt (a scene reload nulls the cached entries).
    private static HapticPlayer[] Players()
    {
        bool stale = _players == null || _players.Length == 0;
        if (!stale)
            for (int i = 0; i < _players.Length && !stale; i++)
                if (_players[i] == null) stale = true;
        if (stale)
            _players = Object.FindObjectsByType<HapticPlayer>();
        return _players;
    }

    /// Buzz both hands. Which hand acted is not tracked — the events these fire from
    /// (a task completing, a mistake being recorded) are about the RUN, not about one
    /// controller, and guessing wrong would put the cue in the hand that did nothing.
    public static void Pulse(float amplitude, float seconds)
    {
        if (!Application.isPlaying) return;
        var players = Players();
        if (players == null) return;
        for (int i = 0; i < players.Length; i++)
            if (players[i] != null) players[i].SendHapticImpulse(amplitude, seconds);
    }

    public static void StepComplete() => Pulse(StepAmplitude, StepSeconds);
    public static void Mistake() => Pulse(ErrorAmplitude, ErrorSeconds);

    /// Test seam — drops the cache so a rebuilt rig is picked up immediately.
    public static void Forget() => _players = null;
}
