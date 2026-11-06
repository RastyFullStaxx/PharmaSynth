using System.Collections.Generic;
using UnityEngine;

/// Restores the PPE locker displays (lab coat / goggles / gloves) to their original
/// pegs on demand, so a restart from the lab tour can never leave a piece missing
/// and the campaign unenterable. Snapshots each display's local
/// pose + active state at scene start; `Reseat()` puts them back AND strips any worn
/// PPE, giving a clean, dressable locker every time the player is asked to gear up
/// (gate CoatPrompt) or restarts (ResetToEntrance).
public class WearableReseat : MonoBehaviour
{
    public static WearableReseat Instance { get; private set; }

    [SerializeField] private string[] displayNames = { "LabCoatDisplay", "Goggles_Standin", "Gloves_Standin" };
    [SerializeField] private PPEController ppe;

    private class Snap { public Vector3 lp; public Quaternion lr; public Vector3 ls; public bool active; }
    private readonly Dictionary<string, Snap> _snaps = new Dictionary<string, Snap>();

    public void Bind(PPEController controller, string[] names)
    {
        ppe = controller;
        if (names != null && names.Length > 0) displayNames = names;
    }

    private void Awake() { Instance = this; }
    private void OnDestroy() { if (Instance == this) Instance = null; }
    private void Start() { Capture(); }

    private void Capture()
    {
        _snaps.Clear();
        foreach (var n in displayNames)
        {
            var go = Find(n);
            if (go == null) continue;
            var t = go.transform;
            _snaps[n] = new Snap { lp = t.localPosition, lr = t.localRotation, ls = t.localScale, active = go.activeSelf };
        }
    }

    /// Put every wearable display back on its peg and take any worn PPE off.
    public void Reseat()
    {
        if (_snaps.Count == 0) Capture();
        foreach (var kv in _snaps)
        {
            var go = Find(kv.Key);
            if (go == null) continue;
            var t = go.transform;
            var s = kv.Value;
            t.localPosition = s.lp; t.localRotation = s.lr; t.localScale = s.ls;
            if (go.activeSelf != s.active) go.SetActive(s.active);
        }
        if (ppe != null) ppe.RemovePPE();
    }

    private static GameObject Find(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name) return t.gameObject;
        return null;
    }
}
