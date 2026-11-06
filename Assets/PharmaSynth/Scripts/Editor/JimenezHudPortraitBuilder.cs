#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UImage = UnityEngine.UI.Image;

/// Gives Dr. Jimenez a HUD presence equal to Pharmee's — he appears in the
/// HUD with his own icon image, just as she does.
///
/// Two jobs, both idempotent:
///  1. RENDER his portrait from his own rigged model: a transparent-background
///     headshot framed on the humanoid Head bone, so the icon IS the character the
///     player meets. → Art/UI/jimenez_icon.png,
///     imported with pharmee_icon.png's settings (Sprite/Single, alpha, no mips).
///  2. WIRE the HudDialogueBar for two speakers: the existing DialogueBar
///     Portrait Image + Pharmee's icon as the primary channel, and Jimenez's
///     narration + name + new icon as an extra channel. Without it his lines
///     appear only in his own world bubble and the HUD bar shows nothing.
public static class JimenezHudPortraitBuilder
{
    const string IconPath = "Assets/PharmaSynth/Art/UI/jimenez_icon.png";
    const string PharmeeIconPath = "Assets/PharmaSynth/Art/UI/pharmee_icon.png";
    const string JimenezPrefab = "Assets/PharmaSynth/Art/Generated/Models/RiggedDrjimenez.prefab";
    const int Size = 512;
    const int RenderLayer = 31;   // rendered in isolation; nothing else is on it

    [MenuItem("Tools/PharmaSynth/Build Jimenez HUD Portrait")]
    public static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[JimenezHUD] exit Play mode first."); return; }
        if (!File.Exists(IconPath)) RenderIcon();
        WireBar();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Tools/PharmaSynth/Build Jimenez HUD Portrait (re-render icon)")]
    public static void RunForce()
    {
        if (Application.isPlaying) { Debug.LogWarning("[JimenezHUD] exit Play mode first."); return; }
        RenderIcon();
        WireBar();
        AssetDatabase.SaveAssets();
    }

    /// Headshot from the rigged prefab: a throwaway instance parked far from the
    /// lab on an isolated layer, lit and framed on the Head bone, rendered to a
    /// transparent RT. The scene is never touched.
    static void RenderIcon()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(JimenezPrefab);
        if (prefab == null) { Debug.LogWarning("[JimenezHUD] missing " + JimenezPrefab); return; }

        var far = new Vector3(0f, 5000f, 0f);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var camGo = new GameObject("PortraitCam");
        var keyGo = new GameObject("PortraitKey");
        var fillGo = new GameObject("PortraitFill");
        RenderTexture rt = null;
        try
        {
            inst.transform.position = far;
            inst.transform.rotation = Quaternion.identity;
            SetLayer(inst, RenderLayer);

            // Frame on the HEAD bone — guessing a head height from bounds breaks
            // on any rig whose pivot isn't at the feet.
            Transform head = null;
            var anim = inst.GetComponentInChildren<Animator>();
            if (anim != null && anim.isHuman) head = anim.GetBoneTransform(HumanBodyBones.Head);
            Vector3 focus;
            if (head != null) focus = head.position + Vector3.up * 0.06f;
            else
            {
                var b = BoundsUtil.OfRenderers(inst, 1f);
                focus = new Vector3(b.center.x, b.max.y - b.size.y * 0.12f, b.center.z);
            }

            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);   // transparent PNG
            cam.cullingMask = 1 << RenderLayer;
            cam.orthographic = true;
            cam.orthographicSize = 0.19f;                       // head + shoulders
            cam.nearClipPlane = 0.01f; cam.farClipPlane = 10f;
            // Slightly off-axis: a dead-on render reads flat/mugshot.
            var dir = Quaternion.Euler(6f, 202f, 0f) * Vector3.forward;
            camGo.transform.position = focus - dir * 1.2f;
            camGo.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 1.25f; key.color = new Color(1f, 0.98f, 0.94f);
            key.cullingMask = 1 << RenderLayer;
            keyGo.transform.rotation = Quaternion.Euler(28f, 205f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = 0.55f; fill.color = new Color(0.85f, 0.9f, 1f);
            fill.cullingMask = 1 << RenderLayer;
            fillGo.transform.rotation = Quaternion.Euler(12f, 40f, 0f);

            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var framed = CenterOnOpaqueBounds(tex);
            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, framed.EncodeToPNG());
            if (framed != tex) Object.DestroyImmediate(framed);
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceUpdate);
            ApplySpriteSettings(IconPath);
            Debug.Log("<color=#4CD07D>[JimenezHUD] rendered " + IconPath + " (" + Size + "x" + Size + ")</color>");
        }
        finally
        {
            if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(keyGo);
            Object.DestroyImmediate(fillGo); Object.DestroyImmediate(inst);
        }
    }

    /// Crop to the rendered subject and re-centre it on a square canvas. The raw
    /// render sits wherever the rig's pose put it, which reads as a small,
    /// badly-hung portrait in the bar's PreserveAspect slot. Framing from the
    /// actual opaque pixels is pose-proof, with no camera numbers to re-tune.
    static Texture2D CenterOnOpaqueBounds(Texture2D src, float marginFrac = 0.06f)
    {
        int w = src.width, h = src.height;
        var px = src.GetPixels32();
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 8)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < minX || maxY < minY) return src;   // nothing rendered — keep the raw frame

        int side = Mathf.Max(maxX - minX + 1, maxY - minY + 1);
        side = Mathf.CeilToInt(side * (1f + marginFrac * 2f));
        var outPx = new Color32[side * side];         // transparent by default
        int offX = (side - (maxX - minX + 1)) / 2 - minX;
        int offY = (side - (maxY - minY + 1)) / 2 - minY;
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                int dx = x + offX, dy = y + offY;
                if (dx < 0 || dy < 0 || dx >= side || dy >= side) continue;
                outPx[dy * side + dx] = px[y * w + x];
            }
        var dst = new Texture2D(side, side, TextureFormat.RGBA32, false);
        dst.SetPixels32(outPx);
        dst.Apply();
        return dst;
    }

    /// Mirror pharmee_icon.png's import settings so the two portraits behave the
    /// same in the bar's 128x128 PreserveAspect slot.
    static void ApplySpriteSettings(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f;
        ti.alphaIsTransparency = true;
        ti.mipmapEnabled = false;
        ti.maxTextureSize = 2048;
        ti.SaveAndReimport();
    }

    static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform t in go.transform) SetLayer(t.gameObject, layer);
    }

    /// Point the HudDialogueBar at the Portrait Image and add Jimenez's channel.
    static void WireBar()
    {
        var bar = Object.FindAnyObjectByType<HudDialogueBar>(FindObjectsInactive.Include);
        if (bar == null) { Debug.LogWarning("[JimenezHUD] no HudDialogueBar in the open scene."); return; }

        var portrait = FindDeep(bar.transform, "Portrait");
        var img = portrait != null ? portrait.GetComponent<UImage>() : null;
        var barRoot = FindDeep(bar.transform, "DialogueBar");
        var speaker = FindDeep(bar.transform, "Speaker");
        var line = FindDeep(bar.transform, "Line");

        // Jimenez's own narration channel (his world bubble) — the bar mirrors it.
        NPCNarrationController jimenez = null;
        var examiner = Object.FindAnyObjectByType<ExaminerNPC>(FindObjectsInactive.Include);
        if (examiner != null) jimenez = examiner.GetComponentInChildren<NPCNarrationController>(true);
        if (jimenez == null)
            foreach (var n in Object.FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include))
                if (n.name.Contains("Jimenez")) { jimenez = n; break; }

        var pharmeeIcon = AssetDatabase.LoadAssetAtPath<Sprite>(PharmeeIconPath);
        var jimenezIcon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        NPCNarrationController pharmee = null;
        foreach (var n in Object.FindObjectsByType<NPCNarrationController>(FindObjectsInactive.Include))
            if (n.name.Contains("Pharmee")) { pharmee = n; break; }

        var extras = jimenez != null
            ? new[] { new HudDialogueBar.Channel
                { narration = jimenez, speakerName = "Dr. Jimenez", portrait = jimenezIcon } }
            : new HudDialogueBar.Channel[0];

        bar.Bind(pharmee, barRoot != null ? barRoot.gameObject : null,
                 speaker != null ? speaker.GetComponent<TMP_Text>() : null,
                 line != null ? line.GetComponent<TMP_Text>() : null,
                 img, pharmeeIcon, extras);
        EditorUtility.SetDirty(bar);
        EditorSceneManagerMarkDirty();
        Debug.Log("<color=#4CD07D>[JimenezHUD] bar wired — Pharmee" + (jimenez != null ? " + Dr. Jimenez" : " (Jimenez narration NOT found)")
                  + (img != null ? " · portrait Image bound" : " · NO Portrait Image found") + "</color>");
    }

    static void EditorSceneManagerMarkDirty()
        => UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

    static Transform FindDeep(Transform root, string name)
    {
        var top = root;
        while (top.parent != null) top = top.parent;
        foreach (var t in top.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t;
        return null;
    }
}
#endif
