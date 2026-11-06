using System.Collections.Generic;

/// Who has claimed which role inside ONE rack group.
///
/// A rack group's tubes are interchangeable, so a role belongs to whichever tube the
/// player actually poured it into. This is the shared ledger that keeps that exclusive:
/// without it two tubes both narrow to the same role, the RackTaskGroup counts that role
/// twice, and the step completes while a tube still sits empty.
///
/// Plain C# rather than a MonoBehaviour: it holds no transform, needs no update, and is
/// created by ExperimentSceneBuilder alongside the group's RackTaskGroups.
public class RackRoles
{
    private readonly Dictionary<LiquidTaskBinding, int> _claims
        = new Dictionary<LiquidTaskBinding, int>();
    private readonly List<LiquidTaskBinding> _members = new List<LiquidTaskBinding>();

    /// Every member registers as it is wired (SetRoles), so a claim can reach the others.
    public void Join(LiquidTaskBinding who)
    {
        if (who != null && !_members.Contains(who)) _members.Add(who);
    }

    public void Claim(LiquidTaskBinding who, int role)
    {
        if (who == null) return;
        _claims[who] = role;
        // ⛔ A claim changes what every OTHER member may still become, and what it may
        // ADVERTISE. If members only recompute when something happens to THEM, a twin whose
        // authored role was just taken keeps advertising it, and the vapor stream, the sim
        // and the label all trust the stale promise (Exp 7's crude distillate: 32 ml scolded
        // into a beaker that could no longer take it). Refresh the others now; a snapshot,
        // because a refresh may itself claim by elimination.
        foreach (var m in _members.ToArray())
            if (m != null && m != who) m.RefreshRoles();
    }

    /// Give a role back: the vessel was emptied, so whatever it had claimed is
    /// available again and every other member's candidate set just widened.
    public void Release(LiquidTaskBinding who)
    {
        if (who != null) _claims.Remove(who);
    }

    /// Recompute every OTHER member — the public half of what Claim does internally, for a
    /// caller that changed the ledger by releasing rather than claiming.
    public void RefreshOthers(LiquidTaskBinding who)
    {
        foreach (var m in _members.ToArray())
            if (m != null && m != who) m.RefreshRoles();
    }

    /// Roles claimed by every OTHER member — what this tube may no longer become.
    /// Excluding the caller matters: a tube must not be blocked by its own claim, or it
    /// would lose the role it just took and start oscillating.
    public ICollection<int> TakenByOthers(LiquidTaskBinding me)
    {
        var taken = new HashSet<int>();
        foreach (var kv in _claims)
            if (kv.Key != null && kv.Key != me) taken.Add(kv.Value);
        return taken;
    }

    // No Clear(): ExperimentSceneBuilder creates a NEW ledger every time it wires a rack,
    // so a retry starts with no claims by construction. A reset method would only be a
    // second way to do what rebuilding already does.
}
