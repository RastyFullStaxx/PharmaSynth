#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// Frees Dr. Jimenez's lab coat from his arms, so the coat no longer moves
/// with the arm.
///
/// ⛔ There is nothing to unparent. He is a Tripo auto-rig: ONE SkinnedMeshRenderer over 41
/// bones, with the coat baked into the body mesh. The coat follows the arm because coat
/// VERTICES carry weight on the arm bones — an auto-rigger assigns weight by proximity, so a
/// coat panel hanging near the elbow picks up elbow weight even though a real coat hangs
/// from the shoulders.
///
/// This finds that bleed geometrically (a vertex carrying arm weight while sitting well
/// outside the arm) and hands the weight to the spine. The sleeve is kept: anything within
/// `sleeveRadius` of the arm bone still follows it, or he would wear a rigid tube.
///
/// ⛔ It ALWAYS rebuilds from the ORIGINAL imported mesh, never from its own output, so
/// re-running at a different radius cannot compound. The source .glb is never modified and
/// the change is undone by clearing the mesh override on the prefab.
public static class JimenezCoatRig
{
    const string Prefab = "Assets/PharmaSynth/Art/Generated/Models/RiggedDrjimenez.prefab";
    const string Output = "Assets/PharmaSynth/Art/Generated/Models/RiggedDrjimenez_CoatFixed.asset";

    [MenuItem("Tools/PharmaSynth/Fix Jimenez Coat Rig")]
    public static void Run() => Run(-1f);

    /// `sleeveRadius` is in the MESH's own units. Pass a negative value to derive it from
    /// the model's height, which is the scale-proof default.
    public static void Run(float sleeveRadius)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null) { Debug.LogError("[CoatRig] no prefab at " + Prefab); return; }
        var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
        if (smr == null) { Debug.LogError("[CoatRig] no SkinnedMeshRenderer"); return; }

        // ALWAYS start from the imported source, never from a previous run's output.
        Mesh source = SourceMesh(smr);
        if (source == null) { Debug.LogError("[CoatRig] could not resolve the original imported mesh"); return; }

        var bones = smr.bones;
        var bindposes = source.bindposes;
        if (bones == null || bindposes == null || bones.Length != bindposes.Length)
        { Debug.LogError("[CoatRig] bones/bindposes mismatch"); return; }

        // Bind-pose position of every bone, in mesh space.
        var bonePos = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
            bonePos[i] = bindposes[i].inverse.MultiplyPoint3x4(Vector3.zero);

        // Each bone's segment end = its FARTHEST skinned child; else itself. See
        // JimenezRigMath.IsBetterSegmentEnd for why "farthest" and not "nearest".
        var segStart = new Vector3[bones.Length];
        var segEnd = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            segStart[i] = segEnd[i] = bonePos[i];
            if (bones[i] == null) continue;
            float best = -1f;
            for (int j = 0; j < bones.Length; j++)
            {
                if (j == i || bones[j] == null || bones[j].parent != bones[i]) continue;
                float d = Vector3.Distance(bonePos[i], bonePos[j]);
                if (JimenezRigMath.IsBetterSegmentEnd(d, best)) { best = d; segEnd[i] = bonePos[j]; }
            }
        }

        // A twist bone borrows its parent limb's segment. See JimenezRigMath.IsTwistBone —
        // judged against itself, a leaf twist bone tears the sleeve clean off the arm.
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] == null || !JimenezRigMath.IsTwistBone(bones[i].name)) continue;
            for (int j = 0; j < bones.Length; j++)
            {
                if (bones[j] == null || bones[j] != bones[i].parent) continue;
                segStart[i] = segStart[j]; segEnd[i] = segEnd[j];
                break;
            }
        }

        // Torso targets, best first.
        int spine = IndexOf(bones, "Spine02");
        if (spine < 0) spine = IndexOf(bones, "Spine01");
        if (spine < 0) spine = IndexOf(bones, "Waist");
        if (spine < 0) { Debug.LogError("[CoatRig] no torso bone (Spine02/Spine01/Waist)"); return; }

        // ⛔ Mesh units, NOT metres: this model is ~0.5 units tall and the prefab scales it
        // to a 1.75 m man. Deriving the radius from the mesh's own height is what makes the
        // classification survive a re-export at a different scale.
        float meshHeight = source.bounds.size.y;
        if (sleeveRadius <= 0f) sleeveRadius = JimenezRigMath.SleeveRadiusFor(meshHeight);
        float blendBand = JimenezRigMath.BlendBandFor(meshHeight);

        var verts = source.vertices;
        var weights = source.boneWeights;
        int movedVerts = 0, keptSleeve = 0;
        float movedWeight = 0f;
        var perBone = new Dictionary<string, int>();

        for (int v = 0; v < weights.Length; v++)
        {
            var w = weights[v];
            int[] idx = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
            float[] wt = { w.weight0, w.weight1, w.weight2, w.weight3 };
            bool changed = false;

            for (int s = 0; s < 4; s++)
            {
                if (wt[s] <= 0f) continue;
                int b = idx[s];
                if (b < 0 || b >= bones.Length || bones[b] == null) continue;
                if (!JimenezRigMath.IsArmBone(bones[b].name)) continue;

                float dist = JimenezRigMath.DistanceToSegment(verts[v], segStart[b], segEnd[b]);
                // ⛔ A FRACTION, ramped across a band — never an all-or-nothing hand-over.
                // Moving 100% of the weight on one side of the radius and 0% on the other
                // makes neighbouring vertices follow different bones, and the edge between
                // them rips. See JimenezRigMath.TransferFraction.
                float share = JimenezRigMath.TransferFraction(dist, sleeveRadius, blendBand);
                if (share <= 0f) { keptSleeve++; continue; }

                float moved = JimenezRigMath.Redistribute(ref w, b, spine, share);
                if (moved > 0f)
                {
                    movedWeight += moved; changed = true;
                    perBone.TryGetValue(bones[b].name, out int c);
                    perBone[bones[b].name] = c + 1;
                    idx = new[] { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 };
                    wt = new[] { w.weight0, w.weight1, w.weight2, w.weight3 };
                }
            }
            if (changed) { weights[v] = w; movedVerts++; }
        }

        // Bake a NEW mesh; the imported one is read-only and must stay pristine.
        var fixedMesh = Object.Instantiate(source);
        fixedMesh.name = "RiggedDrjimenez_CoatFixed";
        fixedMesh.boneWeights = weights;
        fixedMesh.bindposes = bindposes;

        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(Output);
        if (existing != null) { EditorUtility.CopySerialized(fixedMesh, existing); Object.DestroyImmediate(fixedMesh); fixedMesh = existing; }
        else AssetDatabase.CreateAsset(fixedMesh, Output);

        smr.sharedMesh = fixedMesh;
        EditorUtility.SetDirty(smr);
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        var sb = new StringBuilder();
        sb.AppendLine("[CoatRig] mesh height " + meshHeight.ToString("0.000")
                      + " units, sleeveRadius " + sleeveRadius.ToString("0.0000") + " units");
        sb.AppendLine("  vertices freed from the arms : " + movedVerts + " of " + weights.Length);
        sb.AppendLine("  influences left on the sleeve: " + keptSleeve);
        sb.AppendLine("  total weight moved to '" + bones[spine].name + "': " + movedWeight.ToString("0.0"));
        foreach (var kv in perBone) sb.AppendLine("    from " + kv.Key + ": " + kv.Value + " influences");
        sb.AppendLine("  mesh -> " + Output);
        Debug.Log(sb.ToString());
    }

    /// The imported mesh behind the renderer — never the corrected output.
    static Mesh SourceMesh(SkinnedMeshRenderer smr)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(
                     "Assets/PharmaSynth/Art/Generated/Models/RiggedDrjimenez_Assets/selected.glb"))
            if (o is Mesh m) return m;
        // Fall back to whatever is assigned, unless that is our own output.
        return smr.sharedMesh != null && smr.sharedMesh.name == "RiggedDrjimenez_CoatFixed" ? null : smr.sharedMesh;
    }

    static int IndexOf(Transform[] bones, string name)
    {
        for (int i = 0; i < bones.Length; i++) if (bones[i] != null && bones[i].name == name) return i;
        return -1;
    }
}
#endif
