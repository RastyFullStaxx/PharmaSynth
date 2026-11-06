using System.Collections.Generic;
using UnityEngine;

/// Bridges a vessel's LiquidPhysics chemistry events to the experiment logic in a
/// context-aware way: adding a reagent completes the task that expects it (the
/// TaskGraph's prerequisite check enforces order), while a reagent no step expects
/// is a genuine wrong-reagent mistake. Steps may require a MINIMUM poured amount
/// (requiredMl); deliveries accumulate until the threshold is met, so a one-frame
/// splash does not complete a step (the client's depletion mechanic).
public class LiquidTaskBinding : MonoBehaviour
{
    [System.Serializable]
    public class ReagentStep
    {
        public ChemicalData reagent;
        public string taskId;
        [Tooltip("Minimum ml poured in before the step completes. 0 = any amount (legacy).")]
        public float requiredMl;
        [Tooltip("False = the pour is EXPECTED (no wrong-reagent mistake) and accumulates, but completion belongs to another verb (e.g. the weigh station). (W5.8)")]
        public bool completesTask = true;
    }

    [SerializeField] private LiquidPhysics vessel;
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private List<ReagentStep> expectedReagents = new List<ReagentStep>();
    [SerializeField] private FumeHoodZone fumeHood;   // toxic reagents must be handled here

    // Accumulate per STEP, not per task: a task may name SEVERAL reagents (the
    // iodoform test needs KI *and* hypochlorite; Exp 2's tube prep needs the
    // sample *and* its water), and a task-keyed total pooled them — so whichever
    // reagent landed first met the threshold alone and completed the step with
    // half the chemistry missing. Steps are satisfied individually and the task
    // waits for all of them.
    private readonly Dictionary<ReagentStep, float> _accumulated = new Dictionary<ReagentStep, float>();
    private readonly HashSet<ReagentStep> _satisfied = new HashSet<ReagentStep>();

    public IReadOnlyList<ReagentStep> ExpectedSteps => expectedReagents;

    // ---- Dynamic rack roles -------------------------------------------------
    //
    // A rack group's tubes are interchangeable glassware, so which ROLE a tube plays is
    // decided by what the player pours into it rather than by which bench tube it is.
    // See VesselRoleMatch for why claiming is deferred rather than decided on pour one.
    //
    // Null for a standalone vessel, which keeps the original fixed behaviour untouched.
    private List<List<ReagentStep>> _roles;
    private RackRoles _rackRoles;
    private readonly List<string> _poured = new List<string>();
    private List<IReadOnlyList<string>> _roleKeys;

    /// True while more than one of the group's roles still fits this tube. A normal
    /// state, not an error: the four alkaline tubes are indistinguishable until their
    /// third reagent lands.
    public bool RoleAmbiguous => _roles != null && _claimedRole < 0;
    public int ClaimedRole => _claimedRole;
    private int _claimedRole = -1;
    private int _authoredRole = -1;

    /// Fired ONCE, the first time this tube narrows to a single role. The builder
    /// listens so the role's non-pour anchors (heat, chill, litmus, weigh, vapor, flame)
    /// can be attached to THIS tube — the one the player actually used — rather than to
    /// the bench tube the role was authored on. Without this, pooling the pour bindings
    /// would accept the pour and then strand the litmus step on an empty tube.
    public event System.Action<int> RoleClaimed;

    /// True for a POOL member (any tube of the family may take any role), as opposed to a
    /// rack-group member or a standalone vessel.
    public bool IsPoolMember => _roles != null && _authoredRole < 0 || _isPool;
    private bool _isPool;
    public void MarkPoolMember() => _isPool = true;

    /// Every task any SURVIVING candidate role could still serve — empty once claimed or for
    /// a fixed vessel. The tutorial sweep registers these so a free tube can be pointed at
    /// and highlighted as a valid destination before it has committed to anything.
    /// A role a DELIVERY controller has decided this tube plays. -1 = none.
    private int _forcedRole = -1;

    /// Claim the candidate role that serves `taskId`, for a controller that IS the
    /// disambiguator: a vapor stream condensing into a held tube is, by definition, filling
    /// the collect-step receiver, and nothing is poured into a receiver first — so without
    /// this, "hold a clean test tube at its mouth" only ever worked with the authored tube.
    /// Returns true when the tube now plays a role that serves the task.
    public bool ClaimForTask(string taskId)
    {
        if (_roles == null || string.IsNullOrEmpty(taskId)) return false;
        if (_claimedRole >= 0) return RoleServes(_claimedRole, taskId);
        var candidates = VesselRoleMatch.Candidates(_roleKeys, _poured, TakenByOthers());
        foreach (int c in candidates)
            if (RoleServes(c, taskId)) { _forcedRole = c; RebuildActiveSteps(); return _claimedRole == c; }
        return false;
    }

    private bool RoleServes(int role, string taskId)
    {
        if (_roles == null || role < 0 || role >= _roles.Count) return false;
        foreach (var st in _roles[role]) if (st != null && st.taskId == taskId) return true;
        return false;
    }

    /// The step an ACCEPTED pour is counted against while this pool tube is still
    /// ambiguous: the first surviving candidate role's step for that reagent. Any candidate
    /// will do — the narrowing later carries the volume, by reagent, to the claimed role.
    private ReagentStep CandidateStepFor(ChemicalData chem)
    {
        if (_roles == null || _roleKeys == null || chem == null) return null;
        var candidates = VesselRoleMatch.Candidates(_roleKeys, _poured, Blocked());
        foreach (int c in candidates)
            foreach (var st in _roles[c])
                if (st != null && st.reagent == chem) return st;
        return null;
    }

    /// One NAME per role of this pool that serves `taskId`, so the wrist checklist
    /// can tick the alcohols off one at a time instead of saying "5 tubes" and leaving the
    /// player to work out which one they missed.
    ///
    /// A role is named by the reagent that FEWEST sibling roles share — the alcohol, never
    /// the distilled water all five take. When every role wants the same thing (Exp 2's four
    /// identical permanganate tubes) there is no honest name, so the entry is blank and the
    /// caller falls back to the count.
    public List<string> RoleTagsFor(string taskId)
    {
        var tags = new List<string>();
        if (_roles == null || string.IsNullOrEmpty(taskId)) return tags;

        var freq = new Dictionary<string, int>();
        int serving = 0;
        foreach (var role in _roles)
        {
            var seen = new HashSet<string>();
            foreach (var st in role)
                if (st != null && st.taskId == taskId && st.reagent != null) seen.Add(st.reagent.chemicalName);
            if (seen.Count == 0) continue;
            serving++;
            foreach (var n in seen) { freq.TryGetValue(n, out int c); freq[n] = c + 1; }
        }

        foreach (var role in _roles)
        {
            string best = null; int bestN = int.MaxValue;
            foreach (var st in role)
            {
                if (st == null || st.taskId != taskId || st.reagent == null) continue;
                int c = freq[st.reagent.chemicalName];
                if (c < bestN) { bestN = c; best = st.reagent.chemicalName; }
            }
            if (best == null) continue;                       // this role does not serve the task
            tags.Add(bestN < serving ? best : "");
        }
        return tags;
    }

    /// This vessel's own role name for that task once it has claimed one; "" while it is
    /// still free, or when the roles are indistinguishable.
    public string ClaimedRoleTagFor(string taskId)
    {
        if (_roles == null || _claimedRole < 0 || !RoleServes(_claimedRole, taskId)) return "";
        int idx = 0;
        for (int r = 0; r < _roles.Count; r++)
        {
            bool serves = false;
            foreach (var st in _roles[r]) if (st != null && st.taskId == taskId) { serves = true; break; }
            if (!serves) continue;
            if (r == _claimedRole)
            {
                var tags = RoleTagsFor(taskId);
                return idx < tags.Count ? tags[idx] : "";
            }
            idx++;
        }
        return "";
    }

    public List<string> CandidateTasks()
    {
        var tasks = new List<string>();
        if (_roles == null || _claimedRole >= 0 || _roleKeys == null) return tasks;
        var candidates = VesselRoleMatch.Candidates(_roleKeys, _poured, Blocked());
        foreach (int c in candidates)
            foreach (var st in _roles[c])
                if (st != null && !string.IsNullOrEmpty(st.taskId) && !tasks.Contains(st.taskId))
                    tasks.Add(st.taskId);
        return tasks;
    }

    /// Give this tube the whole group's roles. Called by ExperimentSceneBuilder once every
    /// member exists, so the roles can be shared and claimed exclusively.
    public void SetRoles(List<List<ReagentStep>> roles, int authoredRole, RackRoles shared)
    {
        _roles = roles; _rackRoles = shared; _authoredRole = authoredRole;
        shared?.Join(this);
        _roleKeys = new List<IReadOnlyList<string>>();
        for (int i = 0; i < roles.Count; i++)
        {
            var keys = new List<string>();
            foreach (var st in roles[i])
                if (st != null && st.reagent != null && !keys.Contains(st.reagent.chemicalName))
                    keys.Add(st.reagent.chemicalName);
            _roleKeys.Add(keys);
        }
        RebuildActiveSteps();
    }

    private ICollection<int> TakenByOthers()
        => _rackRoles != null ? _rackRoles.TakenByOthers(this) : null;

    /// Roles this vessel may not take RIGHT NOW: claimed by another member, or belonging to
    /// a step the run has not reached.
    ///
    /// ⛔ Pooling every tube in the module put every tube role on every tube, and acceptance
    /// asked only "does some role want this reagent" — so methanol, which a much later ester
    /// tube wants, was silently accepted into a tube during the very first step, narrowed it
    /// to that future role, and the ethanol the player then poured stopped counting. "Any
    /// tube will do" has to mean any tube for the step you are ON, not a free pass to start
    /// step nine. A role whose tasks are all complete or not yet available is out of play;
    /// the role this vessel has already claimed never is, or a tube would lose its own role
    /// the moment its prep task completed.
    private ICollection<int> Blocked()
    {
        var blocked = new HashSet<int>();
        var taken = TakenByOthers();
        if (taken != null) foreach (var t in taken) blocked.Add(t);
        if (_roles == null || runner == null || runner.Graph == null) return blocked;
        for (int r = 0; r < _roles.Count; r++)
        {
            if (r == _claimedRole || blocked.Contains(r)) continue;
            bool open = false;
            foreach (var st in _roles[r])
            {
                if (st == null || string.IsNullOrEmpty(st.taskId)) continue;
                if (runner.Graph.IsComplete(st.taskId)) continue;
                if (runner.Graph.IsAvailable(st.taskId)) { open = true; break; }
            }
            if (!open) blocked.Add(r);
        }
        return blocked;
    }

    /// Ledger seam: another member just claimed a role, so what this one may still
    /// become — and advertise — has changed. Idempotent; may claim by elimination.
    public void RefreshRoles() => RebuildActiveSteps();

    /// Every reagent ANY role of this pooled vessel could legitimately take (or just its
    /// own steps when it has no roles). Pouring one of these into a free tube makes it that
    /// role's tube — correct play, not a mistake.
    public IEnumerable<ChemicalData> AllRoleReagents()
    {
        foreach (var s in expectedReagents) if (s != null && s.reagent != null) yield return s.reagent;
        if (_roles == null) yield break;
        foreach (var role in _roles)
            if (role != null) foreach (var s in role) if (s != null && s.reagent != null) yield return s.reagent;
    }

    /// A vessel taken back to empty is a FREE vessel again.
    ///
    /// Without this, one wrong drop pins a pool tube to a dead candidate set for the rest of
    /// the run: pouring it out changes nothing and there are no spare roles to move to.
    /// Emptying it releases the claimed role back to the shared ledger, forgets what was
    /// poured and un-scolds it, so the player can rinse and start that tube again. Tasks it
    /// ALREADY completed are the runner's, not the vessel's, so they stand: this frees the
    /// glass, it does not undo the experiment.
    public void ResetRole()
    {
        _scolded.Clear();
        _hoodScolded.Clear();
        if (_roles == null) return;
        if (_claimedRole >= 0) _rackRoles?.Release(this);
        _claimedRole = -1;
        _forcedRole = -1;
        _poured.Clear();
        _accumulated.Clear();
        _satisfied.Clear();
        _ready.Clear();
        _blockedEarly.Clear();
        RebuildActiveSteps();
        _rackRoles?.RefreshOthers(this);
    }

    /// Recompute which steps this tube is currently working to.
    ///
    /// Claimed -> exactly that role's steps, i.e. identical to the old fixed behaviour.
    /// Ambiguous -> the union of the surviving roles, deduped by (reagent, task), so a
    /// shared prefix like "KMnO4 then NaOH" still reads and completes correctly.
    private void RebuildActiveSteps()
    {
        if (_roles == null) return;
        var candidates = VesselRoleMatch.Candidates(_roleKeys, _poured, TakenByOthers());
        // ⛔ A role is claimed by a POUR, or by a delivery controller that is the disambiguator
        // by definition (ClaimForTask), never by construction. In a family with ONE role
        // (Exp 9's limewater tube, Acetanilide's hydrolysis tube, Exp 6's hard-glass acetate
        // tube), claiming at SetRoles lets the authored member win the ledger and leaves every
        // extra with an EMPTY candidate set, so any pour into it is scolded before the player
        // has done anything.
        int claimed = _forcedRole >= 0 && candidates.Contains(_forcedRole)
            ? _forcedRole
            : (_poured.Count > 0 ? VesselRoleMatch.ClaimedRole(candidates) : -1);
        bool firstClaim = claimed >= 0 && _claimedRole < 0;
        if (claimed >= 0 && _claimedRole != claimed)
        {
            _claimedRole = claimed;
            _rackRoles?.Claim(this, claimed);
        }

        // ⛔ While AMBIGUOUS, show the tube's AUTHORED role - never the union of the
        // survivors. Exposing the union made every enol tube advertise all five alcohols,
        // and anything that reads ExpectedSteps to decide where a reagent goes (the
        // player-path simulator, the vessel label, the rack membership) then treated every
        // tube as a valid target: the sim poured all five alcohols into all five tubes and
        // logged 144 mistakes on a PERFECT run. Acceptance stays permissive - that is the
        // whole point of dynamic roles - but the ADVERTISED step must stay a single
        // deterministic answer.
        //
        // ⛔ ...and a twin whose authored role ANOTHER member has claimed advertises NOTHING
        // Its candidate set is empty, so it can accept nothing, and an advertised step
        // there is a lie the sim, the label and the vapor stream all believed: Exp 7's crude
        // distillate ran into the authored beaker after a spare had claimed the role, every
        // drop was scolded, the flask drained and the step never completed (visual sweep).
        var taken = TakenByOthers();
        int show = claimed >= 0 ? claimed
                 : (_authoredRole >= 0 && (taken == null || !taken.Contains(_authoredRole)) ? _authoredRole : -1);
        var next = new List<ReagentStep>();
        if (show >= 0 && show < _roles.Count) next.AddRange(_roles[show]);

        // Carry accumulated volume across the narrowing. The same reagent exists as a
        // DIFFERENT ReagentStep object in each role, so without this remap the water a
        // player poured while the tube was still ambiguous would vanish the moment an
        // alcohol claimed a different role, and they would be asked to pour it again.
        //
        // ⛔ Match by REAGENT, preferring the same task but never requiring it. Two roles
        // routinely expect the same liquid under DIFFERENT task ids (Exp 6: acetone for
        // `test-tollens` in one role, `test-schiff` in another), and 2 ml of acetone in the
        // tube is 2 ml of acetone whatever the claimed role ends up calling the step.
        var carriedAmount = new Dictionary<ReagentStep, float>();
        var carriedDone = new HashSet<ReagentStep>();
        foreach (var st in next)
        {
            ReagentStep bestAmt = null;
            foreach (var kv in _accumulated)
            {
                if (kv.Key.reagent != st.reagent) continue;
                if (bestAmt == null || kv.Key.taskId == st.taskId) bestAmt = kv.Key;
                if (kv.Key.taskId == st.taskId) break;
            }
            if (bestAmt != null) carriedAmount[st] = _accumulated[bestAmt];
            foreach (var done in _satisfied)
                if (done.reagent == st.reagent && (done.taskId == st.taskId || !carriedDone.Contains(st)))
                { if (done.taskId == st.taskId || done.requiredMl >= st.requiredMl) carriedDone.Add(st); if (done.taskId == st.taskId) break; }
        }
        _accumulated.Clear();
        foreach (var kv in carriedAmount) _accumulated[kv.Key] = kv.Value;
        _satisfied.Clear();
        foreach (var st in carriedDone) _satisfied.Add(st);

        expectedReagents.Clear();
        expectedReagents.AddRange(next);

        // Raised AFTER the active steps are rebuilt, so a listener attaching the role's
        // anchors sees ExpectedSteps / ReadyFor already describing the claimed role.
        if (firstClaim) RoleClaimed?.Invoke(claimed);
    }

    // ⛔ In play mode AddComponent fires OnEnable IMMEDIATELY, before the builder can
    // assign the vessel, so subscribing here alone runs with vessel==null, subscribes to
    // nothing, and SetVesselAndRunner never fixes it: every pour fires LiquidAdded into
    // the void, no step counts, and the player is stuck at "add distilled water" (while
    // the HazardousMixReactor, which binds correctly, keeps scolding them).
    // Subscription now lives in one idempotent seam that BOTH paths call.
    private bool _subscribed;

    private void Subscribe()
    {
        if (_subscribed || vessel == null) return;
        vessel.LiquidAdded += OnLiquidAdded;
        vessel.WrongReagentMixed += OnWrongReagentMixed;
        vessel.Emptied += OnEmptied;
        if (runner != null) { runner.TaskCompleted += OnAnyTaskCompleted; _taskListener = runner; }
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        if (vessel != null)
        {
            vessel.LiquidAdded -= OnLiquidAdded;
            vessel.WrongReagentMixed -= OnWrongReagentMixed;
            vessel.Emptied -= OnEmptied;
        }
        if (_taskListener != null) { _taskListener.TaskCompleted -= OnAnyTaskCompleted; _taskListener = null; }
        _subscribed = false;
    }

    private void OnEmptied() { RefusedReagent = null; ResetRole(); }

    /// True once this binding actually listens to its vessel (suite-pinned: the
    /// silent-unsubscribed state is exactly the bug that shipped).
    public bool IsListening => _subscribed;

    /// Builder seam: the lab's fume hood (assigned per build — Exp 5's aniline
    /// and acetyl chloride pours are only sanctioned inside it).
    public void SetFumeHood(FumeHoodZone hood) => fumeHood = hood;

    /// Whether this vessel currently counts as "in the fume hood".
    public bool InFumeHood()
        => fumeHood != null && (fumeHood.IsOccupied || fumeHood.Contains(transform.position));

    /// Explicit unhook for teardown (ClearBenchBindings): DestroyImmediate skips
    /// OnDisable for edit-mode components whose OnEnable never ran, so relying on
    /// lifecycle left ghost subscriptions on the permanent bench vessels.
    public void Detach() => Unsubscribe();

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void OnLiquidAdded(ChemicalData chem, float amount) => HandleReagent(chem, amount);

    private void OnWrongReagentMixed(ChemicalData current, ChemicalData incoming)
    {
        // Already handled by HandleReagent via LiquidAdded; nothing extra needed here.
    }

    /// Legacy single-arg path (self-tests, scripted deliveries): treated as a FULL
    /// delivery — the step completes regardless of its requiredMl threshold.
    public void HandleReagent(ChemicalData chem) => Handle(chem, 0f, true);

    /// Amount-aware handling: pours accumulate toward the step's requiredMl.
    public void HandleReagent(ChemicalData chem, float amountMl) => Handle(chem, amountMl, false);

    private void Handle(ChemicalData chem, float amountMl, bool fullDelivery)
    {
        // A DESTROYED binding can still be subscribed (edit-mode components whose OnEnable
        // never ran get no OnDisable/OnDestroy on DestroyImmediate); its stale accumulators
        // would complete tasks on the first squeeze while its stale step list scolds
        // sanctioned pours. Unity's fake-null catches the corpse.
        if (this == null) return;
        if (runner == null || chem == null) return;

        // Fume-hood safety: a toxic/volatile reagent handled outside the hood is a
        // violation. "In the hood" = THIS VESSEL sits inside the hood volume (position
        // test; the work happens where the vessel is), or the physics occupancy when the
        // trigger setup exists.
        if (chem.requiresFumeHood && !InFumeHood() && _hoodScolded.Add(chem.chemicalName))
            runner.RecordMistake(LabErrorType.FumeHoodViolation, chem.chemicalName + " must be handled in the fume hood");

        // A RACK TUBE grades the pour against its surviving roles, not against a fixed
        // list: pouring the right reagent into the "wrong" numbered tube is correct play
        // in VR, where the player grabs whichever tube is nearest. Only a reagent that no
        // surviving role wants is a real mistake.
        if (_roles != null)
        {
            if (!VesselRoleMatch.WouldAccept(_roleKeys, _poured, chem.chemicalName, Blocked()))
            {
                Scold(chem);
                return;
            }
            if (!_poured.Contains(chem.chemicalName)) _poured.Add(chem.chemicalName);
            RebuildActiveSteps();
        }

        var step = StepForReagent(chem);
        // ⛔ An AMBIGUOUS pool extra advertises no steps at all (so the simulator never
        // targets it), yet the candidate set just ACCEPTED this pour. Without this branch the
        // lookup below finds nothing and records "unexpected reagent" on a pour the tube said
        // yes to, so the first pour into any free tube would be scolded. Count it against
        // the first surviving candidate's step for this reagent; the narrowing carries the
        // volume to whichever role the tube finally claims.
        if (step == null && _roles != null && _claimedRole < 0) step = CandidateStepFor(chem);
        if (step == null)
        {
            // No step in this experiment expects this reagent → wrong reagent.
            Scold(chem);
            return;
        }
        // Accepted: the vessel has nothing to complain about any more. Every accepted pour
        // clears it, pooled or fixed — the refusal line is about the LAST thing that bounced.
        RefusedReagent = null;

        // Already done? Ignore extra pours of the same reagent (no double-completes).
        if (runner.Graph != null && runner.Graph.IsComplete(step.taskId)) return;

        if (!fullDelivery && step.requiredMl > 0f)
        {
            _accumulated.TryGetValue(step, out float have);
            have += Mathf.Max(0f, amountMl);
            _accumulated[step] = have;
            if (!MetThreshold(have, step.requiredMl))
            {
                // Live pour guide: a throttled running count over the vessel, so the player can
                // see how much has been poured.
                ShowProgress(step, have);
                return;                            // keep pouring — not enough yet
            }
        }
        bool firstSatisfy = !_satisfied.Contains(step);
        _satisfied.Add(step);
        if (firstSatisfy && step.requiredMl > 0f && Application.isPlaying && vessel != null)
            FloatingText.Show("✓ " + chem.chemicalName + " — enough",
                              vessel.transform.position + Vector3.up * 0.22f,
                              new Color(0.55f, 1f, 0.65f), 0.75f);

        // A task that names several reagents in this vessel needs them ALL before
        // it can be called done — half a recipe is not the step.
        if (!AllStepsSatisfied(step.taskId)) return;

        // Every step satisfied. Tasks owned by another verb (the weigh
        // station; a RackTaskGroup that waits for every tube) only
        // flag readiness — no wrong-reagent mistake was recorded (the pour IS
        // expected), but completion is theirs.
        if (!CompletesHere(step.taskId)) { _ready.Add(step.taskId); return; }

        // Enough reagent delivered. CompleteTask enforces order and will
        // auto-record a WrongStep mistake if prerequisites aren't met yet.
        if (runner.CompleteTask(step.taskId) == TaskCompletionResult.BlockedByPrerequisite)
            _blockedEarly.Add(step.taskId);
    }

    // Poured before its prerequisite: the reagent is in the vessel and the counter
    // reads "enough", so once the prerequisite completes the step completes too,
    // without making the player pour again.
    private readonly HashSet<string> _blockedEarly = new HashSet<string>();
    private ExperimentRunner _taskListener;

    private void OnAnyTaskCompleted(ExperimentTask _)
    {
        if (this == null || _blockedEarly.Count == 0 || runner == null) return;
        foreach (var id in new List<string>(_blockedEarly))
            if (AllStepsSatisfied(id) && runner.CompleteTask(id) != TaskCompletionResult.BlockedByPrerequisite)
                _blockedEarly.Remove(id);
    }

    private readonly HashSet<string> _ready = new HashSet<string>();

    // ⛔ ONE mistake per bad pour, not one per DELIVERY TICK. Handle runs on every
    // LiquidAdded event, and a tilt-pour raises one per frame, so a handful of misaimed
    // pours logged 1025 mistakes in a headset session and put the grade beyond recovery
    // before the player understood what was wrong. The scold still fires every time (the
    // player must see it); only the RECORDED mistake is latched, per reagent, and the latch
    // lifts when the vessel is emptied (ResetRole) so a repeat offence still counts.
    private readonly HashSet<string> _scolded = new HashSet<string>();
    private readonly HashSet<string> _hoodScolded = new HashSet<string>();
    private float _nextNoteAt;   // progress-text throttle (a tilt-pour ticks every frame)

    /// "Distilled Water 6 / 10 ml" over the vessel while a metered step fills.
    private void ShowProgress(ReagentStep step, float have)
    {
        if (!Application.isPlaying || vessel == null || Time.time < _nextNoteAt) return;
        _nextNoteAt = Time.time + 0.5f;
        bool solid = step.reagent != null
                     && (step.reagent.state == PhysicalState.Solid || step.reagent.state == PhysicalState.Powder);
        FloatingText.Show(step.reagent.chemicalName + "  "
                          + Mathf.Min(have, step.requiredMl).ToString("0.#") + " / "
                          + step.requiredMl.ToString("0.#") + (solid ? " g" : " ml"),
                          vessel.transform.position + Vector3.up * 0.22f,
                          new Color(0.6f, 0.85f, 1f), 0.7f);
    }

    /// Every reagent this task names in THIS vessel has met its own threshold.
    private bool AllStepsSatisfied(string taskId)
    {
        bool any = false;
        for (int i = 0; i < expectedReagents.Count; i++)
        {
            var s = expectedReagents[i];
            if (s == null || s.taskId != taskId) continue;
            any = true;
            if (!_satisfied.Contains(s)) return false;
        }
        return any;
    }

    /// False when ANY of the task's steps defers completion to another verb.
    private bool CompletesHere(string taskId)
    {
        for (int i = 0; i < expectedReagents.Count; i++)
        {
            var s = expectedReagents[i];
            if (s != null && s.taskId == taskId && !s.completesTask) return false;
        }
        return true;
    }

    /// Threshold check with a leniency epsilon (suite-pinned): fifty 0.1 g spatula dips
    /// sum to 4.9999995 in floats, so strictly-less-than leaves the player one phantom
    /// dip short of a 5 g step forever. Thresholds are minimums, not calipers.
    public static bool MetThreshold(float have, float required) => have >= required - 0.01f;

    /// This vessel has everything the task asked of it (the rack group's poll).
    public bool ReadyFor(string taskId) => _ready.Contains(taskId);

    /// The reagent this vessel most recently refused, for the live label. Cleared
    /// when the vessel is emptied.
    public string RefusedReagent { get; private set; }

    /// Record a wrong-reagent mistake ONCE per reagent (see _scolded). Always sets the
    /// refusal line, so the label explains the refusal even on the ticks that don't count.
    private void Scold(ChemicalData chem)
    {
        RefusedReagent = chem.chemicalName;
        if (_scolded.Add(chem.chemicalName))
            runner.RecordMistake(LabErrorType.WrongReagent, "Unexpected reagent: " + chem.chemicalName);
    }

    /// Delivered-so-far toward a step — SUMMED across the task's reagents, which
    /// is what the supply monitor wants (how much has gone in for this step).
    public float AccumulatedFor(string taskId)
    {
        float total = 0f;
        foreach (var kv in _accumulated)
            if (kv.Key != null && kv.Key.taskId == taskId) total += kv.Value;
        return total;
    }

    /// Delivered-so-far of ONE reagent toward a step.
    public float AccumulatedFor(string taskId, ChemicalData reagent)
    {
        foreach (var kv in _accumulated)
            if (kv.Key != null && kv.Key.taskId == taskId && kv.Key.reagent == reagent) return kv.Value;
        return 0f;
    }

    /// Reagents this task still expects in this vessel (watch-panel / debug).
    public int StepsRemaining(string taskId)
    {
        int n = 0;
        for (int i = 0; i < expectedReagents.Count; i++)
        {
            var s = expectedReagents[i];
            if (s != null && s.taskId == taskId && !_satisfied.Contains(s)) n++;
        }
        return n;
    }

    public ReagentStep StepForReagent(ChemicalData chem)
    {
        for (int i = 0; i < expectedReagents.Count; i++)
            if (expectedReagents[i] != null && expectedReagents[i].reagent == chem)
                return expectedReagents[i];
        return null;
    }

    public string TaskForReagent(ChemicalData chem)
    {
        var s = StepForReagent(chem);
        return s != null ? s.taskId : null;
    }

    /// The incoming chemical is one this vessel's OWN procedure names. The wrong-mix
    /// layer (HazardousMixReactor + MixFeedback) checks this before punishing, so a
    /// sanctioned dilution ("add 10 ml of distilled water" onto the sample) never reads
    /// as "not in the procedure". Deliberately ignores task completion: inside one
    /// AddLiquid call LiquidAdded (which completes the task) fires BEFORE WrongReagentMixed,
    /// so a pending-only check would make the COMPLETING pour punish itself (e.g. the last
    /// sulfuric squeeze of each ester test). Extra pours of a named reagent are over-pours
    /// of the right thing, not crimes.
    public bool IsExpectedNow(ChemicalData chem) => StepForReagent(chem) != null;

    // Runtime helpers for authoring/binding.
    public void AddExpected(ChemicalData reagent, string taskId, float requiredMl = 0f, bool completesTask = true)
        => expectedReagents.Add(new ReagentStep { reagent = reagent, taskId = taskId, requiredMl = requiredMl, completesTask = completesTask });

    public void SetVesselAndRunner(LiquidPhysics v, ExperimentRunner r)
    {
        Unsubscribe();          // may be re-bound to a different vessel between modules
        vessel = v; runner = r;
        Subscribe();
    }
}
