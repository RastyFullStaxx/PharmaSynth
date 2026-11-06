/// Tutorial Mode: all 9 experiments unlocked, heavily guided
/// (glow + waypoint + hint on the watch + always-on labels), and UNGRADED —
/// no quiz, no grade screen, no BKT update, no save write, no unlock.
/// Practice only; a run leaves no trace.
///
/// Deliberately the same shape as DemoSession.Active: one static flag every
/// consumer early-returns on, so the campaign path stays unchanged by
/// construction rather than by testing.
public static class TutorialSession
{
    public static bool Active;

    /// Modules practised in THIS session, in memory only.
    ///
    /// Deliberately not persisted: "a practice run leaves no trace" is the whole
    /// contract of the mode, and writing a save file would break it. But with nothing
    /// at all a student could not tell which of the nine they had already tried, so
    /// the picker shows a tick that lasts exactly as long as the session does.
    private static readonly System.Collections.Generic.HashSet<string> _practised =
        new System.Collections.Generic.HashSet<string>();

    public static bool HasPractised(string moduleId)
        => !string.IsNullOrEmpty(moduleId) && _practised.Contains(moduleId);

    public static void MarkPractised(string moduleId)
    {
        if (!string.IsNullOrEmpty(moduleId)) _practised.Add(moduleId);
    }

    /// Cleared whenever the mode is entered afresh, so a new student at the same
    /// headset never inherits the previous one's ticks.
    public static void BeginSession()
    {
        Active = true;
        _practised.Clear();
    }

    public static int PractisedCount => _practised.Count;
}
