using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using TeleAnchor = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor;
using TeleArea = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea;

/// Fitting liquids, labels and colliders to a vessel's mesh, plus shared helpers.
public partial class ExperimentSceneBuilder
{
    // ---- vessel fitting (local space) ---------------------------------------
    // Contents must be placed in the vessel's LOCAL frame, never world axes — a
    // tilted/held tube would otherwise get a world-vertical fill sticking out of
    // it.

    /// Local-space bounds of a vessel's solid meshes, skipping fill/label children.
    public static Bounds LocalMeshBounds(Transform root, params string[] skipNames)
    {
        Bounds b = default; bool has = false;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.GetComponent<TMPro.TMP_Text>() != null) continue;
            bool skip = false;
            foreach (var n in skipNames) if (mf.gameObject.name == n) { skip = true; break; }
            if (skip) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? mb.min.x : mb.max.x,
                    (i & 2) == 0 ? mb.min.y : mb.max.y,
                    (i & 4) == 0 ? mb.min.z : mb.max.z);
                var local = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                if (!has) { b = new Bounds(local, Vector3.zero); has = true; } else b.Encapsulate(local);
            }
        }
        return has ? b : new Bounds(Vector3.zero, Vector3.one * 0.05f);
    }

    /// Index of the longest axis (0=x, 1=y, 2=z) — a vessel's "up" from its base.
    public static int LongestAxis(Vector3 size)
        => (size.y >= size.x && size.y >= size.z) ? 1 : (size.z >= size.x ? 2 : 0);

    /// The vessel's own UP axis: the one whose two PERPENDICULAR extents are most
    /// nearly EQUAL, i.e. the axis the round bore runs along.
    ///
    /// LongestAxis is right for a tube and wrong for anything wide and shallow. A
    /// watch glass (90 x 15 x 90 mm) and a mortar are LONGEST across, so the mound
    /// would lie along a horizontal axis: sized from a 15 mm "bore" and parked at
    /// the RIM as a tiny dot that never grows.
    /// Measuring roundness instead picks Y for both, and still picks
    /// the long axis of a tube lying on X or Z. Ties (a cube) go to Y.
    public static int VesselAxis(Vector3 size)
    {
        int best = 1; float bestScore = float.MaxValue;
        for (int a = 0; a < 3; a++)
        {
            float p = Mathf.Abs(size[(a + 1) % 3]), q = Mathf.Abs(size[(a + 2) % 3]);
            float m = Mathf.Max(p, q);
            float score = m <= 1e-6f ? 0f : Mathf.Abs(p - q) / m;
            if (score < bestScore - 1e-4f || (a == 1 && score <= bestScore + 1e-4f))
            { bestScore = score; best = a; }
        }
        return best;
    }

    /// Rotation mapping a primitive's +Y onto the given local axis.
    public static Quaternion AxisAlign(int axis)
        => axis == 1 ? Quaternion.identity
         : axis == 0 ? Quaternion.Euler(0f, 0f, -90f)
                     : Quaternion.Euler(90f, 0f, 0f);

    /// The two extents perpendicular to `axis` → the vessel's internal bore.
    public static float BoreOf(Vector3 size, int axis)
        => axis == 0 ? Mathf.Min(size.y, size.z)
         : axis == 1 ? Mathf.Min(size.x, size.z)
                     : Mathf.Min(size.x, size.y);

    /// Opaque powder mound for solid/powder reagents: a low, wide, matte heap
    /// sitting in the bottom of the jar so it reads as granular, not liquid.
    /// Idempotent (reuses a child named "Powder"); sharedMaterial only.
    public static void EnsurePowderVisual(GameObject inst, ChemicalData chem, float fill01 = 1f)
    {
        if (inst == null) return;
        fill01 = Mathf.Clamp01(fill01);
        Renderer heap = null;
        var owner = inst.GetComponent<LiquidPhysics>();
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            if (r.name == "Powder") heap = r;
            else if (r.name == "Liquid")
            {
                // ⛔ The liquid surface is KEPT, not destroyed. Destroying it leaves
                // LiquidPhysics.mainRenderer dangling (thousands of MissingReferenceExceptions per
                // run) and means a vessel scooped full of powder can never draw
                // liquid again, however much is later poured on it. Hiding it is enough:
                // LiquidPhysics.DryPowder decides every frame which layer shows, so a dry
                // jar still reads as a jar of powder and a wetted mixture
                // reads as liquid.
                if (owner != null && owner.mainRenderer == null
                    && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Fill"))
                    owner.mainRenderer = r;
                r.enabled = false;
            }
        }

        // (The vessel is measured below in its LOCAL frame, always excluding the
        // mound itself — including it makes each re-run wrap the previous,
        // bigger mound and balloon it into a giant blob.)
        if (heap == null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);   // squashed = a mound
            go.name = "Powder";
            var col = go.GetComponent<Collider>();
            if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
            go.transform.SetParent(inst.transform, true);
            heap = go.GetComponent<Renderer>();
            heap.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            heap.sharedMaterial = new Material(shader != null ? shader : Shader.Find("Standard"))
                { name = "PharmaPowder_Runtime" };
        }
        // Empty → hide the mound entirely (a scooped-out source shows no powder).
        if (fill01 <= 0.001f) { heap.gameObject.SetActive(false); return; }
        heap.gameObject.SetActive(true);

        // Fit the mound INSIDE the vessel in its own LOCAL frame, so a tilted or
        // held vessel (e.g. the hard-glass tube) keeps its contents aligned and
        // contained instead of a world-vertical blob poking out.
        var lb = LocalMeshBounds(inst.transform, "Powder", "Liquid", "CollectedGas");
        int ax = VesselAxis(lb.size);
        float bore = BoreOf(lb.size, ax);
        // Size PURELY as a fraction of the vessel's own local bounds — never mix in
        // absolute metres. lb is in LOCAL units, so a metre cap here becomes
        // microscopic on an import-scaled prefab (the powder vanishes
        // inside the glass tube). Fractions stay contained at any scale.
        //
        // Width stays half the bore (it must never poke through the glass wall);
        // HEIGHT is scaled off the BORE and only capped by the vessel's own
        // depth. min(depth * 0.22, bore * 0.6) would take the depth term on any
        // shallow dish and collapse the mound to a film — combined with the wrong
        // axis it turns a scoop on a watch glass into a dot.
        float w = bore * 0.5f * Mathf.Lerp(0.6f, 1f, fill01);                       // half the bore
        float h = Mathf.Min(bore * Mathf.Lerp(0.10f, 0.34f, fill01), lb.size[ax] * 0.6f);
        h = Mathf.Max(bore * 0.06f, h);                                             // always a little visible

        // A hand-placed anchor wins for placement — and, ONLY if it is a size-setting
        // anchor, for size too.
        // "PowderAnchor" = position + SIZE; "BowlAnchor" (mortar) = position ONLY.
        // A position-only anchor has scale 1, so taking its scale gives a 1-unit
        // beach-ball of powder — never size from an anchor unless it says it sizes.
        var anchor = inst.transform.Find("PowderAnchor") ?? inst.transform.Find("BowlAnchor");
        if (anchor != null)
        {
            var pa = anchor.GetComponent<PlacementAnchor>();
            bool anchorSetsSize = pa != null && pa.previewsScale;
            heap.transform.localPosition = anchor.localPosition;
            heap.transform.localRotation = anchor.localRotation;
            heap.transform.localScale = anchorSetsSize
                ? anchor.localScale * Mathf.Lerp(0.55f, 1f, fill01)   // hand-set size
                : new Vector3(w, h, w);                                // computed fraction of the bore
        }
        else
        {
            // Rest on the vessel floor, centred in the bore, along the vessel's axis.
            Vector3 lp = lb.center;
            lp[ax] = lb.min[ax] + h * 0.5f + lb.size[ax] * 0.05f;
            heap.transform.localPosition = lp;
            heap.transform.localRotation = AxisAlign(ax);
            // Local scale is in the vessel's frame → no lossyScale compensation.
            heap.transform.localScale = new Vector3(w, h, w);
        }
        var mat = heap.sharedMaterial;
        var c = chem != null ? chem.liquidColor : new Color(0.9f, 0.88f, 0.82f); c.a = 1f;
        if (mat != null)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.08f);   // matte, not glossy
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.08f);
        }
    }

    // ---- helpers ----------------------------------------------------------

    /// Finite bottle supply: the authored supplyMl, or 2.5x the summed requiredMl of
    /// every binding this chemical feeds (min 120 ml). A chemical no binding consumes
    /// keeps the legacy 60% fill (display/test reagents).
    private float SupplyFor(ExperimentLayout.Prop p, ChemicalData chem)
    {
        if (p.supplyMl > 0f) return p.supplyMl;
        float needed = 0f;
        var layout = FindLayout(runner != null && runner.Module != null ? runner.Module.moduleId : null);
        if (layout != null)
            foreach (var v in layout.vessels)
                foreach (var b in v.bindings)
                    if (b.reagentChemical == p.fillChemical && b.requiredMl > 0f) needed += b.requiredMl;
        return needed > 0f ? Mathf.Max(120f, needed * 2.5f) : 600f;
    }

    private static Bounds WB(GameObject g) => SolidWorldBounds(g);

    /// Names of the runtime children that RIDE a vessel but are not part of its
    /// body: the fill/mound layers, the collected-gas column, the mounted label,
    /// and the pour effects. Pure so the suite can pin the list.
    public static bool IsEffectChild(string childName)
        => childName == "Liquid" || childName == "Precipitate" || childName == "Powder"
        || childName == "CollectedGas" || childName == "NameLabel"
        || childName == "StreamLine" || childName == "PourStream" || childName == "ScoopHeap";

    /// World bounds of a vessel's SOLID meshes only.
    ///
    /// Every effect child must be excluded, because two of them are authored in
    /// WORLD space and outlive the pour that made them: LiquidPourer's StreamLine
    /// (a world-space LineRenderer still holding the arc's last floor point) and
    /// its PourStream particles. Encapsulating those drags a poured-from test
    /// tube's bounds down to the floor, so seating it in a rack lifts it a metre
    /// into the air.
    /// The same applies to every bounds-fitted child.
    /// Is this mesh part of a vessel's actual BODY? Effect children and text meshes
    /// are not. Extracted so every consumer shares one definition — the Tutorial Mode
    /// x-ray ghost needs the TMP_Text clause too: without it it
    /// silhouettes each item's floating ProximityLabel. That label carries a large
    /// compensating scale to undo its parent's, so the ghost comes out hugely oversized.
    public static bool IsSolidMesh(MeshFilter mf)
        => mf != null && mf.sharedMesh != null
           && !IsEffectChild(mf.gameObject.name)
           && mf.GetComponent<TMPro.TMP_Text>() == null
           && mf.GetComponent<Renderer>() != null;

    public static Bounds SolidWorldBounds(GameObject g)
    {
        if (g == null) return new Bounds(Vector3.zero, Vector3.one * 0.05f);
        Bounds b = default; bool has = false;
        foreach (var mf in g.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!IsSolidMesh(mf)) continue;
            var r = mf.GetComponent<Renderer>();
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        // Skinned/effect-only hosts still need SOME extent to work with.
        if (!has)
        {
            foreach (var r in g.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || IsEffectChild(r.gameObject.name)) continue;
                if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
            }
        }
        return has ? b : new Bounds(g.transform.position, Vector3.one * 0.1f);
    }

    /// World bounds of a prop's SOLID mesh only — skips the given child names
    /// (the powder/liquid fill so it can't inflate its own host on a re-run) AND
    /// any non-mesh renderer: floating TMP name labels, the pour-arc LineRenderer,
    /// particle systems. Those balloon the measured jar size, so a tiny
    /// vial would grow a beach-ball of powder.
    private static Bounds BoundsExcluding(GameObject g, params string[] skipNames)
    {
        Bounds b = default; bool has = false;
        foreach (var r in g.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;   // no text/line/particle
            if (r.GetComponent<TMPro.TMP_Text>() != null) continue;           // TMP labels use a MeshRenderer
            bool skip = false;
            foreach (var n in skipNames) if (r.name == n) { skip = true; break; }
            if (skip) continue;
            if (!has) { b = r.bounds; has = true; } else b.Encapsulate(r.bounds);
        }
        return has ? b : new Bounds(g.transform.position, Vector3.one * 0.05f);
    }

    private static void Normalise(GameObject g, float targetHeight)
        => Normalise(g, null, targetHeight);

    /// RealSizes-aware normalisation: known prefabs scale by their realistic
    /// LONGEST dimension (bounds-HEIGHT normalisation inflated flat tools 3-16x);
    /// unknown names keep the legacy height behaviour.
    private static void Normalise(GameObject g, string prefabName, float fallbackHeight)
    {
        g.transform.localScale = Vector3.one;
        var size = WB(g).size;
        if (RealSizes.TryGet(prefabName, out float target))
            g.transform.localScale = Vector3.one * RealSizes.UniformScaleFactor(size, target);
        else
            g.transform.localScale = Vector3.one * (fallbackHeight / Mathf.Max(size.y, 0.01f));
    }

    /// Rotate a spawned item into its plausible resting pose (a spatula lies
    /// flat, a glass rod on its side) BEFORE seating, so bounds-based seating
    /// uses the rotated footprint.
    private static void RestPoseFor(GameObject g, string prefabName)
    {
        if (!PhysicsProfiles.TryGet(prefabName, out var prof)) return;
        g.transform.rotation = PhysicsProfiles.RestRotation(prof.pose, WB(g).size) * g.transform.rotation;
    }

    private static void Seat(Transform t, Vector3 pos)
    {
        float surfaceY = pos.y;
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(pos.x, pos.y + 0.5f, pos.z), Vector3.down, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
            surfaceY = hit.point.y;
        t.position = new Vector3(pos.x, surfaceY + 0.3f, pos.z);
        t.position += Vector3.up * (surfaceY + 0.005f - WB(t.gameObject).min.y);
    }

    private TextMeshPro MakeLabel(string text, Vector3 pos, float scale)
    {
        // Fall back to the stage when the scene's WorldLabels root isn't wired
        // (edit-mode builder tests) — stage teardown cleans those up anyway.
        var parent = labelsRoot != null ? labelsRoot : Stage();
        if (parent == null) return null;
        var go = new GameObject("DynLabel_" + text);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = text; tmp.fontSize = 6f; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.fontStyle = FontStyles.Bold;
        // outlineWidth instances the font material — play-mode only (the edit-mode
        // suite builds stages too, and .material instancing errors there).
        if (Application.isPlaying)
        {
            tmp.outlineWidth = 0.25f; tmp.outlineColor = new Color32(6, 12, 22, 255);
        }
        go.AddComponent<FaceCamera>();
        return tmp;
    }
}
