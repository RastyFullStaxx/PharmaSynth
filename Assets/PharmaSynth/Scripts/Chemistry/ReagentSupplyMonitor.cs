using System;
using System.Collections.Generic;
using UnityEngine;

/// Pure shortfall analysis: which incomplete pour-steps can no longer be finished
/// because the remaining supply of their reagent (summed across all bottles) is
/// less than what the step still needs. Edit-mode testable.
public static class ReagentSupplyMath
{
    public struct Need
    {
        public string taskId;
        public string chemicalName;
        public float requiredMl;
        public float deliveredMl;
    }

    public static List<string> FindShortfalls(IEnumerable<Need> needs,
        Func<string, bool> isTaskComplete,
        IReadOnlyDictionary<string, float> availableMlByChemical)
    {
        var shortfalls = new List<string>();
        if (needs == null) return shortfalls;
        foreach (var n in needs)
        {
            if (n.requiredMl <= 0f) continue;                          // instant steps never starve
            if (isTaskComplete != null && isTaskComplete(n.taskId)) continue;
            float still = Mathf.Max(0f, n.requiredMl - n.deliveredMl);
            if (still <= 0f) continue;
            float avail = 0f;
            if (availableMlByChemical != null && n.chemicalName != null)
                availableMlByChemical.TryGetValue(n.chemicalName, out avail);
            if (avail + 0.5f < still) shortfalls.Add(n.taskId);        // small epsilon for pour jitter
        }
        return shortfalls;
    }
}

/// Watches the live stage while an experiment runs: if a required pour-step can no
/// longer be satisfied by the reagent left in the scene's bottles, it raises
/// SupplyExhausted (once per attempt) so Pharmee can offer the restart.
public class ReagentSupplyMonitor : MonoBehaviour
{
    [SerializeField] private ExperimentRunner runner;
    [SerializeField] private float pollSeconds = 2f;

    /// taskIds that starved. Latched until the next attempt starts.
    public event Action<List<string>> SupplyExhausted;

    private float _next;
    private bool _latched;

    public void SetRunner(ExperimentRunner r)
    {
        if (runner != null) runner.ExperimentStarted -= OnStarted;
        runner = r;
        if (runner != null && isActiveAndEnabled) runner.ExperimentStarted += OnStarted;
    }

    private void OnEnable() { if (runner != null) runner.ExperimentStarted += OnStarted; }
    private void OnDisable() { if (runner != null) runner.ExperimentStarted -= OnStarted; }
    private void OnStarted(ExperimentModuleDefinition m) => _latched = false;

    /// "Keep trying" on the supply prompt un-latches after a grace window: if the
    /// shortfall still exists (or a new one appears), Pharmee offers the restart again.
    /// Otherwise the latch only clears on a new attempt and Running has no other exit.
    public void Unlatch(float graceSeconds = 20f)
    {
        _latched = false;
        _next = Time.time + Mathf.Max(1f, graceSeconds);
    }

    /// Test seams.
    public bool Latched => _latched;
    public void ForceLatch() => _latched = true;

    private void Update()
    {
        if (_latched || runner == null || !runner.IsRunning) return;
        if (Time.time < _next) return;
        _next = Time.time + Mathf.Max(0.5f, pollSeconds);

        // Tutorial Mode tops the bottles up UNCONDITIONALLY, before the shortfall
        // analysis rather than inside it. Demo's branch below is REACTIVE —
        // it only refills once a step has already starved — so a bottle spilled early
        // would sit empty for the rest of the run even though nothing was short yet.
        // Practice must never be able to dead-end, so there is no shortfall to find:
        // the poll already sweeps every LiquidPhysics, so the refill is free here.
        if (TutorialSession.Active) { RefillSourceBottles(); return; }

        var shortfalls = EvaluateNow();
        if (shortfalls.Count > 0)
        {
            // Demo sessions never dead-end into the restart prompt — the bottles
            // visibly top themselves back up instead (config: infiniteSupply).
            if (DemoSession.Active && DemoMode.InfiniteSupply)
            {
                RefillSourceBottles();
                return;
            }
            _latched = true;
            SupplyExhausted?.Invoke(shortfalls);
        }
    }

    /// Demo-only: top every SOURCE bottle (no task binding) back up to at least
    /// 150 ml so no pour-step can starve.
    public static int RefillSourceBottles()
    {
        int refilled = 0;
        var bottles = UnityEngine.Object.FindObjectsByType<LiquidPhysics>();
        foreach (var lp in bottles)
        {
            // A bottle poured DRY clears its contents, so restock from what it last
            // dispensed — otherwise demo mode silently skipped every empty bottle.
            var stock = lp.currentChemical != null ? lp.currentChemical : lp.LastChemical;
            if (stock == null) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;
            // ⛔ SOURCES only. Outside a run no vessel carries a binding, so without this the lab
            // reset would refill the glassware it had just emptied. A used vessel is one the
            // builders gave a CleanableVessel; a source (bottle, wash bottle, the methane charge
            // jar) never has one. Don't use a NAME rule here: it starves the methane charge jar.
            if (LabReset.IsUsedVessel(lp)) continue;
            if (lp.currentLiquidVolume < 150f) { lp.SetContents(stock, 150f); refilled++; }
            // A solid jar shows its heap, not a liquid column, and a full jar shows a full heap.
            if (stock.state == PhysicalState.Solid || stock.state == PhysicalState.Powder)
                ExperimentSceneBuilder.EnsurePowderVisual(lp.gameObject, stock,
                    lp.maxVolume > 0f ? Mathf.Clamp01(lp.currentLiquidVolume / lp.maxVolume) : 1f);
        }
        return refilled;
    }

    /// One poll pass (public for headless tests): gathers needs from every live
    /// LiquidTaskBinding and supply from every bottle holding the right chemical.
    public List<string> EvaluateNow()
    {
        var needs = new List<ReagentSupplyMath.Need>();

        // ⛔ The module's OWN PRODUCT is not a bench supply — the player SYNTHESISES it.
        //
        // There are no ready-made bottles of Acetanilide, Chloroform, Benzamide or Wine (a
        // client rule: the player crafts them). Steps that draw from your own product (the
        // chemical tests, the wash, the racking) would otherwise look like steps needing a
        // bottle that does not exist, and the monitor would declare "Not enough reagents
        // left to finish" at 0% progress in exactly those four modules.
        string ownProduct = runner != null && runner.Module != null
            ? DemoMode.ProductFor(runner.Module.moduleId) : null;

        var binds = UnityEngine.Object.FindObjectsByType<LiquidTaskBinding>();
        foreach (var b in binds)
            foreach (var s in b.ExpectedSteps)
            {
                if (s == null || s.reagent == null || s.requiredMl <= 0f) continue;
                if (!string.IsNullOrEmpty(ownProduct) && s.reagent.chemicalName == ownProduct) continue;
                needs.Add(new ReagentSupplyMath.Need
                {
                    taskId = s.taskId,
                    chemicalName = s.reagent.chemicalName,
                    requiredMl = s.requiredMl,
                    deliveredMl = b.AccumulatedFor(s.taskId),
                });
            }
        if (needs.Count == 0) return new List<string>();

        // Available supply: SOURCE bottles only (vessels with a task binding are
        // reaction targets, not supplies).
        var avail = new Dictionary<string, float>();
        var bottles = UnityEngine.Object.FindObjectsByType<LiquidPhysics>();
        foreach (var lp in bottles)
        {
            if (lp.currentChemical == null || lp.currentLiquidVolume <= 0f) continue;
            if (lp.GetComponent<LiquidTaskBinding>() != null) continue;
            string key = lp.currentChemical.chemicalName;
            avail.TryGetValue(key, out float v);
            avail[key] = v + lp.currentLiquidVolume;
        }

        bool Complete(string taskId) => runner != null && runner.Graph != null && runner.Graph.IsComplete(taskId);
        return ReagentSupplyMath.FindShortfalls(needs, Complete, avail);
    }
}
