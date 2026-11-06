#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// Builds the futuristic CUBE SPAWN ROOM in the MainMenu scene:
/// a fully-enclosed, solid, dark room with cyan/teal emissive trim, a couple of
/// soft lights and a glowing floor launch-pad under the menu panel. Sealed on all
/// six sides so no skybox leaks. Re-runnable and idempotent: deletes the prior
/// "MenuCubeRoom" and rebuilds it, and hides the legacy open "MenuRoom" dressing.
///
/// Tools ▸ PharmaSynth ▸ Build Menu Cube Room. Run with the MainMenu scene open.
public static class MenuCubeRoomBuilder
{
    const float Width = 6f;      // X
    const float Depth = 6f;      // Z
    const float Height = 3.2f;   // Y (floor at 0)
    const float Thick = 0.16f;   // wall thickness

    static readonly Color BaseCol = new Color(0.06f, 0.08f, 0.12f);   // deep blue-grey
    static readonly Color TrimEmis = new Color(0.15f, 0.9f, 1f) * 2.6f;
    static readonly Color PanelEmis = new Color(0.10f, 0.72f, 1f) * 1.2f;
    static readonly Color GridEmis = new Color(0.12f, 0.82f, 1f) * 1.5f;

    [MenuItem("Tools/PharmaSynth/Build Menu Cube Room")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[MenuCubeRoom] exit Play mode first."); return; }

        // Centre the cube on the player's spawn (XR rig / camera), floor at y=0.
        Vector3 c = SpawnXZ();
        c.y = 0f;

        var prev = GameObject.Find("MenuCubeRoom");
        if (prev != null) Object.DestroyImmediate(prev);

        // Hide the legacy open room dressing so it can't leak skybox behind the cube.
        var oldRoom = GameObject.Find("MenuRoom");
        if (oldRoom != null && oldRoom.activeSelf)
        {
            Undo.RecordObject(oldRoom, "hide MenuRoom");
            oldRoom.SetActive(false);
        }

        var baseMat = Lit("MenuCube_Base", BaseCol, 0.35f, 0.0f);
        var floorMat = Lit("MenuCube_Floor", new Color(0.05f, 0.07f, 0.11f), 0.6f, 0.1f);
        var trimMat = Emissive("MenuCube_Trim", new Color(0.02f, 0.05f, 0.08f), TrimEmis);
        var panelMat = Emissive("MenuCube_Panel", new Color(0.03f, 0.06f, 0.10f), PanelEmis);
        var gridMat = Emissive("MenuCube_Grid", new Color(0.02f, 0.05f, 0.08f), GridEmis);

        var root = new GameObject("MenuCubeRoom");
        root.transform.position = c;
        Undo.RegisterCreatedObjectUndo(root, "Build Menu Cube Room");

        // --- six sealed walls (inward-facing solid boxes) ---------------------
        Box(root, "Floor", new Vector3(0, -Thick * 0.5f, 0), new Vector3(Width, Thick, Depth), floorMat);
        Box(root, "Ceiling", new Vector3(0, Height + Thick * 0.5f, 0), new Vector3(Width, Thick, Depth), baseMat);
        Box(root, "Wall_West", new Vector3(-Width * 0.5f, Height * 0.5f, 0), new Vector3(Thick, Height, Depth), baseMat);
        Box(root, "Wall_East", new Vector3(Width * 0.5f, Height * 0.5f, 0), new Vector3(Thick, Height, Depth), baseMat);
        Box(root, "Wall_North", new Vector3(0, Height * 0.5f, Depth * 0.5f), new Vector3(Width, Height, Thick), baseMat);
        Box(root, "Wall_South", new Vector3(0, Height * 0.5f, -Depth * 0.5f), new Vector3(Width, Height, Thick), baseMat);

        // --- cyan emissive trim: top-edge strips + vertical corner pillars -----
        float y0 = 0.06f, yTop = Height - 0.06f;
        // top perimeter strips
        Box(root, "Trim_TopN", new Vector3(0, yTop, Depth * 0.5f - 0.04f), new Vector3(Width - 0.2f, 0.06f, 0.03f), trimMat);
        Box(root, "Trim_TopS", new Vector3(0, yTop, -Depth * 0.5f + 0.04f), new Vector3(Width - 0.2f, 0.06f, 0.03f), trimMat);
        Box(root, "Trim_TopE", new Vector3(Width * 0.5f - 0.04f, yTop, 0), new Vector3(0.03f, 0.06f, Depth - 0.2f), trimMat);
        Box(root, "Trim_TopW", new Vector3(-Width * 0.5f + 0.04f, yTop, 0), new Vector3(0.03f, 0.06f, Depth - 0.2f), trimMat);
        // vertical corner pillars
        foreach (var s in new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(-1, -1) })
            Box(root, "Trim_Corner", new Vector3(s.x * (Width * 0.5f - 0.05f), Height * 0.5f, s.y * (Depth * 0.5f - 0.05f)),
                new Vector3(0.05f, Height - 0.2f, 0.05f), trimMat);
        // baseboard glow (skirting)
        Box(root, "Trim_BaseN", new Vector3(0, y0, Depth * 0.5f - 0.04f), new Vector3(Width - 0.2f, 0.05f, 0.03f), trimMat);
        Box(root, "Trim_BaseS", new Vector3(0, y0, -Depth * 0.5f + 0.04f), new Vector3(Width - 0.2f, 0.05f, 0.03f), trimMat);

        // --- accent panels on the side walls ---------------------------------
        // Centres at 1.25 to suit the unified 1.40 m eye height.
        Box(root, "Panel_W", new Vector3(-Width * 0.5f + 0.09f, 1.25f, -1.2f), new Vector3(0.03f, 1.4f, 1.6f), panelMat);
        Box(root, "Panel_E", new Vector3(Width * 0.5f - 0.09f, 1.25f, -1.2f), new Vector3(0.03f, 1.4f, 1.6f), panelMat);

        // --- holographic neon frames on every wall + a floor grid ------------
        NeonFrames(root, gridMat);
        FloorGrid(root, gridMat);

        // --- glowing floor launch-pad under the spawn ------------------------
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "LaunchPad";
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        pad.transform.SetParent(root.transform, false);
        pad.transform.localPosition = new Vector3(0, 0.015f, 0.2f);
        pad.transform.localScale = new Vector3(2.4f, 0.02f, 2.4f);
        pad.GetComponent<Renderer>().sharedMaterial = Emissive("MenuCube_Pad", new Color(0.04f, 0.09f, 0.13f), new Color(0.08f, 0.6f, 0.9f) * 0.8f);

        // --- soft cyan fill lights -------------------------------------------
        AddLight(root, "Fill_1", new Vector3(-1.5f, Height - 0.5f, 1.2f), new Color(0.6f, 0.9f, 1f), 16f, 7f);
        AddLight(root, "Fill_2", new Vector3(1.5f, Height - 0.5f, -1.2f), new Color(0.5f, 0.88f, 1f), 14f, 7f);
        AddLight(root, "Key", new Vector3(0f, Height - 0.4f, -1.6f), new Color(0.85f, 0.96f, 1f), 18f, 8f);

        EditorUtility.SetDirty(root);
        Debug.Log("<color=#4CD07D>[MenuCubeRoom] built at " + c.ToString("F2") + " (sealed cube, MenuRoom hidden)</color>");
    }

    /// A glowing cyan frame (border + centre line) inset on every wall, sitting
    /// just off the surface toward the room — reads as a holographic panel grid.
    static void NeonFrames(GameObject root, Material mat)
    {
        const float inset = 0.45f, t = 0.035f, off = Thick * 0.5f + 0.04f;   // sit proud of the wall's inner face
        float wIn = Width - inset * 2f, hIn = Height - inset * 2f, dIn = Depth - inset * 2f;
        float midY = Height * 0.5f;

        foreach (int sgn in new[] { 1, -1 })   // North (+Z) / South (−Z): frame in the XY plane
        {
            float z = sgn * (Depth * 0.5f) - sgn * off;
            Box(root, "Frame_NS", new Vector3(0, midY + hIn * 0.5f, z), new Vector3(wIn, t, t), mat);
            Box(root, "Frame_NS", new Vector3(0, midY - hIn * 0.5f, z), new Vector3(wIn, t, t), mat);
            Box(root, "Frame_NS", new Vector3(-wIn * 0.5f, midY, z), new Vector3(t, hIn, t), mat);
            Box(root, "Frame_NS", new Vector3(wIn * 0.5f, midY, z), new Vector3(t, hIn, t), mat);
            Box(root, "Frame_NS", new Vector3(0, midY, z), new Vector3(wIn, t, t), mat);   // centre line
        }
        foreach (int sgn in new[] { 1, -1 })   // East (+X) / West (−X): frame in the ZY plane
        {
            float x = sgn * (Width * 0.5f) - sgn * off;
            Box(root, "Frame_EW", new Vector3(x, midY + hIn * 0.5f, 0), new Vector3(t, t, dIn), mat);
            Box(root, "Frame_EW", new Vector3(x, midY - hIn * 0.5f, 0), new Vector3(t, t, dIn), mat);
            Box(root, "Frame_EW", new Vector3(x, midY, -dIn * 0.5f), new Vector3(t, hIn, t), mat);
            Box(root, "Frame_EW", new Vector3(x, midY, dIn * 0.5f), new Vector3(t, hIn, t), mat);
            Box(root, "Frame_EW", new Vector3(x, midY, 0), new Vector3(t, t, dIn), mat);   // centre line
        }
    }

    /// Thin cyan grid lines on the floor.
    static void FloorGrid(GameObject root, Material mat)
    {
        const float t = 0.03f, y = 0.02f;
        for (float z = -Depth * 0.5f + 1.2f; z < Depth * 0.5f - 0.5f; z += 1.2f)
            Box(root, "Grid_X", new Vector3(0, y, z), new Vector3(Width - 0.5f, 0.012f, t), mat);
        for (float x = -Width * 0.5f + 1.2f; x < Width * 0.5f - 0.5f; x += 1.2f)
            Box(root, "Grid_Z", new Vector3(x, y, 0), new Vector3(t, 0.012f, Depth - 0.5f), mat);
    }

    // ---- helpers ------------------------------------------------------------

    static Vector3 SpawnXZ()
    {
        var rig = GameObject.Find("XR Origin (XR Rig)");
        if (rig != null) return rig.transform.position;
        var cam = Camera.main;
        if (cam != null) return cam.transform.position;
        return Vector3.zero;
    }

    static void Box(GameObject parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.isStatic = true;
    }

    static void AddLight(GameObject parent, string name, Vector3 localPos, Color col, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = col;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
    }

    static Material Lit(string name, Color col, float smooth, float metallic)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.color = col;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        return m;
    }

    static Material Emissive(string name, Color baseCol, Color emis)
    {
        var m = Lit(name, baseCol, 0.5f, 0f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        m.SetColor("_EmissionColor", emis);
        return m;
    }
}
#endif
