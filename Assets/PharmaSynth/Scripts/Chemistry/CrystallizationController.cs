using System;
using UnityEngine;
using UnityEngine.Events;

/// Timed crystallization: after BeginCrystallization(), a liquid gradually turns
/// to solid crystals over a duration (e.g. aspirin/benzoic acid on ice). Cross-
/// fades a crystal renderer in as it progresses; fires Crystallized when done and
/// exposes a TaskGraph auto-check predicate. Timestep-driven, deterministically tested.
public class CrystallizationController : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float durationSeconds = 8f;
    [SerializeField] private Renderer liquidRenderer;   // faded out
    [SerializeField] private Renderer crystalRenderer;  // faded in

    public UnityEvent onCrystallized;
    public event Action Crystallized;

    private float _t;
    private bool _running;
    private bool _done;

    public float Progress => Mathf.Clamp01(_t / Mathf.Max(0.01f, durationSeconds));
    public bool IsDone => _done;

    public void BeginCrystallization() { if (!_done) _running = true; }

    private void Update() => Tick(Time.deltaTime);

    /// Advance the process (public for deterministic tests).
    public void Tick(float dt)
    {
        if (!_running || _done) return;
        _t += Mathf.Max(0f, dt);
        UpdateVisual(Progress);
        if (_t >= durationSeconds)
        {
            _done = true; _running = false;
            Crystallized?.Invoke();
            onCrystallized?.Invoke();
        }
    }

    private void UpdateVisual(float p)
    {
        if (crystalRenderer != null)
        {
            if (!crystalRenderer.enabled && p > 0.02f) crystalRenderer.enabled = true;
            SetAlpha(crystalRenderer, p);
        }
        if (liquidRenderer != null) SetAlpha(liquidRenderer, 1f - p);
    }

    private static void SetAlpha(Renderer r, float a)
    {
        var m = r.material;
        if (m.HasProperty("_BaseColor")) { var c = m.GetColor("_BaseColor"); c.a = a; m.SetColor("_BaseColor", c); }
    }

    public void ResetProcess() { _t = 0f; _running = false; _done = false; }
}
