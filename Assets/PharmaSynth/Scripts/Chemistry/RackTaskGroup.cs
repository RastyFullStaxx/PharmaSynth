using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// Pure rules for a rack of tubes that share one step.
///
/// Manuscript Exp 2 runs the same test across a SET of tubes — five alcohols for
/// the enol test, three butyl alcohols beside a negative control, acetone beside
/// acetaldehyde, hydrolysed beside unhydrolysed aspirin. In every case **the
/// comparison across the tubes IS the lesson**, so the step is only done when
/// every tube that the step names has had its reagent.
///
/// This exists because LiquidTaskBinding is per-VESSEL: give five tubes a binding
/// for the same task and the first tube to hit its threshold completes it,
/// quietly making the other four optional and throwing the lesson away. The rack
/// members are authored completesTask:false (so their pours are expected and
/// accumulate, but completion is not theirs) and the group calls it in.
public static class RackMath
{
    /// The step is done once as many tubes are ready as the step has ROLES.
    ///
    /// Compared against ROLES, not MEMBERS: every bench tube of the family is a member of
    /// one pool (so the player may use any tube), so the member count is 19 while the
    /// required count is the number of roles the step names (five alcohols, four
    /// permanganate tubes, two Tollens tubes). Zero required is never ready: an empty
    /// group must not auto-complete a step.
    public static bool AllReady(int readyTubes, int requiredTubes)
        => requiredTubes > 0 && readyTubes >= requiredTubes;

    /// Progress copy while the set fills in — the count is the point, so show it.
    public static string ProgressLabel(int readyTubes, int requiredTubes)
        => "tube " + readyTubes + " of " + requiredTubes;

    /// A tube left DELIBERATELY empty (Exp 2's negative control) is simply not a
    /// member of that step's group: it declares no binding for the task, so it is
    /// never counted, and pouring into it is a genuine wrong-reagent mistake —
    /// which is exactly how the control teaches experimental design.
    /// The wrist checklist's per-tube breakdown, so the player can see which tube is
    /// still outstanding. Named roles tick off one at a time; roles that cannot be told
    /// apart (four identical permanganate tubes) honestly get the count alone. Pure so
    /// the suite pins the text without a scene. The marks are the checklist's own (• done,
    /// □ outstanding) because the board's font has no check-mark glyph.
    public static string RolesLine(IReadOnlyList<string> tags, ICollection<string> served,
                                   int ready, int required)
    {
        string head = "Tubes " + ready + " of " + required;
        if (tags == null) return head;
        var sb = new StringBuilder(head);
        int named = 0;
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.IsNullOrEmpty(tags[i])) continue;
            named++;
            bool done = served != null && served.Contains(tags[i]);
            sb.Append("\n  ").Append(done ? "\u2022 " : "\u25a1 ").Append(tags[i]);
        }
        return named > 0 ? sb.ToString() : head;
    }

    public static int CountReady(IReadOnlyList<LiquidTaskBinding> members, string taskId)
    {
        if (members == null) return 0;
        int n = 0;
        for (int i = 0; i < members.Count; i++)
            if (members[i] != null && members[i].ReadyFor(taskId)) n++;
        return n;
    }
}

/// Completes one task when every tube in its rack has had what the step asked of
/// it. Thin driver over RackMath; poll-based so it needs no event plumbing into
/// LiquidTaskBinding (which already tracks its own readiness).
public class RackTaskGroup : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private string taskId;
    [SerializeField] private List<LiquidTaskBinding> members = new List<LiquidTaskBinding>();
    [Tooltip("How many tubes the step needs — its ROLE count. -1 falls back to every member.")]
    [SerializeField] private int required = -1;

    private bool _fired;
    private int _lastReady = -1;

    public int MemberCount => members.Count;
    /// Tubes this step needs served. The pool's members are every tube on the bench, so
    /// this is the number that decides completion and the number the player is shown.
    public int Required => required > 0 ? required : members.Count;
    public string TaskId => taskId;

    /// Every role of this step, ready or not — what the wrist checklist lists.
    public IReadOnlyList<LiquidTaskBinding> Members => members;

    /// Edit-mode / builder seam (AddComponent fires no Awake in edit mode).
    public void Bind(ExperimentRunner r, string task, List<LiquidTaskBinding> tubes, int requiredCount = -1)
    {
        runner = r; taskId = task;
        members = tubes ?? new List<LiquidTaskBinding>();
        required = requiredCount;
        _fired = false; _lastReady = -1;
    }

    /// Pure check, exposed so the suite can drive it without a frame loop.
    public bool ShouldFire()
        => !_fired && runner != null && !string.IsNullOrEmpty(taskId)
           && runner.Graph != null && !runner.Graph.IsComplete(taskId)
           && RackMath.AllReady(RackMath.CountReady(members, taskId), Required);

    void Update()
    {
        if (!Application.isPlaying || _fired) return;
        if (runner == null || runner.Graph == null || string.IsNullOrEmpty(taskId)) return;

        int ready = RackMath.CountReady(members, taskId);
        if (ready != _lastReady && ready > 0 && ready < Required)
        {
            // Tell the player the SET is the step, so a finished tube doesn't read
            // as a finished step ("why didn't it tick?").
            FloatingText.Show(RackMath.ProgressLabel(ready, Required),
                              transform.position + Vector3.up * 0.25f,
                              new Color(0.6f, 0.85f, 1f), 0.9f);
        }
        _lastReady = ready;

        if (ShouldFire()) { _fired = true; runner.CompleteTask(taskId); }
    }
}
