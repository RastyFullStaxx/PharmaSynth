#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// Wires the NPC/audio polish into SampleScene:
///   1. Pharmee expressions: PharmeeFace pointed at the robot's EYES + MOUTH
///      meshes, default-happy; PharmeeMood resets the face
///      after every line; the gatekeeper's faceBehaviour drives gate moods.
///   2. Dr. Jimenez proctor roaming — ProctorRoamer + observation points at the
///      reagent shelf, equipment shelf, dynamic stage and fume hood.
///   3. AC proximity hum — ProximityHum on the air-con / vent assets (falls back to
///      the fume hood if no AC mesh exists).
///
/// Tools ▸ PharmaSynth ▸ Wire NPC Polish (SampleScene, edit mode, idempotent).
public static class LabNpcPolishBuilder
{
    /// How far his idle clip lifts his soles above the bind pose (measured in Play).
    public const float JimenezIdleLift = 0.036f;

    /// Anything still reading GREEN on Pharmee goes blue: the antenna
    /// itself, and any other tinted part, without having to guess mesh names. Only
    /// materials on the robot's own hierarchy are touched, and only when they are actually
    /// green — a run over an already-blue robot changes nothing, so the menu stays
    /// re-runnable. sharedMaterial deliberately: renderer.material would leak an instance
    /// in edit mode.
    static bool Greenish(Color c)
        => c.g > 0.35f && c.g > c.r * 1.35f && c.g > c.b * 1.2f;

    static Color BlueFor(Color c)
    {
        float h = Mathf.Max(c.g, Mathf.Max(c.r, c.b));      // keep the part's brightness
        return new Color(h * 0.3f, h * 0.72f, h, c.a);
    }

    static int GreenToBlue(GameObject robot)
    {
        int n = 0;
        var seen = new HashSet<Material>();
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
        {
            foreach (var m in r.sharedMaterials)
            {
                if (m == null || !seen.Add(m)) continue;
                bool changed = false;
                if (m.HasProperty("_BaseColor") && Greenish(m.GetColor("_BaseColor")))
                { m.SetColor("_BaseColor", BlueFor(m.GetColor("_BaseColor"))); changed = true; }
                if (m.HasProperty("_Color") && Greenish(m.GetColor("_Color")))
                { m.SetColor("_Color", BlueFor(m.GetColor("_Color"))); changed = true; }
                if (m.HasProperty("_EmissionColor") && Greenish(m.GetColor("_EmissionColor")))
                { m.SetColor("_EmissionColor", BlueFor(m.GetColor("_EmissionColor"))); changed = true; }
                if (changed) { EditorUtility.SetDirty(m); n++; }
            }
        }
        return n;
    }

    /// Enforce a serialized float. A changed C# default never reaches an already-saved
    /// scene instance, so tuned values must be written, not merely defaulted.
    static void SetFloat(SerializedObject so, string prop, float value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.floatValue = value;
    }

    [MenuItem("Tools/PharmaSynth/Wire NPC Polish")]
    public static void Build()
    {
        if (Application.isPlaying) { Debug.LogWarning("[NpcPolish] exit Play mode first."); return; }

        // ---- 1. Pharmee face ------------------------------------------------
        var robot = GameObject.Find("RobotNPC");
        if (robot == null) { Debug.LogError("[NpcPolish] no RobotNPC"); return; }
        var face = robot.GetComponentInChildren<PharmeeFace>(true);
        if (face == null) face = robot.AddComponent<PharmeeFace>();

        var faceParts = new List<Renderer>();
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name.ToLower();
            if (n.StartsWith("eyes") || n.StartsWith("mouth")) faceParts.Add(r);
        }
        if (faceParts.Count > 0) face.BindRenderers(faceParts.ToArray());
        // ⛔ The eyes and mouth draw UNLIT. Lit with the model's Blue_Light material, the
        // eye meshes shade to NaN on some frames (measured: hiding the eyes alone took the
        // count of frames with invalid pixels to zero). Bloom spreads one NaN pixel into a
        // white blast over the whole view, which flickered whenever her talk nod swept the
        // angle. They are glowing screen features, so unlit loses nothing.
        var unlit = FaceUnlitMaterial();
        foreach (var r in faceParts) { r.sharedMaterial = unlit; EditorUtility.SetDirty(r); }
        var soFace = new SerializedObject(face);
        var prop = soFace.FindProperty("colorProperty");
        if (prop != null) { prop.stringValue = "_BaseColor"; soFace.ApplyModifiedProperties(); }
        // Cap every expression under the bloom threshold.
        // BLUE, not green, matching the antenna and every other emissive material on the robot
        // (all called Blue_Light); a green face reads as a different character mid-sentence.
        // ⛔ Neutral and Happy are ONE colour. The gate sets Neutral as each line
        // starts and PharmeeMood resets to Happy as it ends, so any difference between the two
        // becomes a step change on every sentence, which reads as flashing.
        // Warning stays amber: it fires only on a real mistake and is meant to be noticed.
        var faceBlue = PharmeeGlowMath.CapBrightness(PharmeeGlowMath.CyanHue, PharmeeGlowMath.FaceCeiling);
        face.SetPalette(faceBlue, faceBlue,
            PharmeeGlowMath.CapBrightness(new Color(1f, 0.6f, 0.15f), PharmeeGlowMath.FaceCeiling));
        int retinted = GreenToBlue(robot);
        if (retinted > 0) Debug.Log("[NpcPolish] retinted " + retinted + " green material(s) to blue");
        EditorUtility.SetDirty(face);
        Debug.Log("[NpcPolish] face renderers: " + faceParts.Count + " (" + string.Join(", ", faceParts.ConvertAll(r => r.name)) + ")");

        // ---- 1b. Body glow: the FBX's Blue_Light material blazes at 8x on the
        // body panels and hover rings; PharmeeFace only covers the eyes and mouth.
        var glow = robot.GetComponentInChildren<PharmeeGlow>(true);
        if (glow == null) glow = robot.AddComponent<PharmeeGlow>();
        var glowParts = new List<Renderer>();
        var shellParts = new List<Renderer>();
        foreach (var r in robot.GetComponentsInChildren<Renderer>(true))
        {
            if (PharmeeGlowMath.IsGlowPart(r.name)) glowParts.Add(r);
            else if (PharmeeGlowMath.IsShellPart(r.name)) shellParts.Add(r);
        }
        glow.BindRenderers(glowParts.ToArray());
        glow.BindShell(shellParts.ToArray());
        // ENFORCE the tuned values (same reason as PharmeeAttitude below: the component is
        // authored in the SCENE, so a changed C# default never reaches the saved instance).
        var soGlow = new SerializedObject(glow);
        SetFloat(soGlow, "intensity", PharmeeGlowMath.DefaultIntensity);
        var hueProp = soGlow.FindProperty("hue");
        if (hueProp != null) hueProp.colorValue = PharmeeGlowMath.CyanHue;
        SetFloat(soGlow, "shellAlbedo", PharmeeGlowMath.DefaultShellAlbedo);
        soGlow.ApplyModifiedProperties();
        glow.Apply();
        EditorUtility.SetDirty(glow);
        Debug.Log("[NpcPolish] glow renderers: " + glowParts.Count + " emissive + " + shellParts.Count
                  + " shell (" + string.Join(", ", shellParts.ConvertAll(r => r.name)) + ")");

        var narration = robot.GetComponentInChildren<NPCNarrationController>(true);
        var mood = robot.GetComponent<PharmeeMood>();
        if (mood == null) mood = robot.AddComponent<PharmeeMood>();
        mood.Bind(narration, face);
        if (narration != null) { narration.SetVoiceBlip("pharmee-blip", 0.3f, 6); EditorUtility.SetDirty(narration); }   // soft, sparse fallback tick

        var gk = robot.GetComponentInChildren<PharmeeGatekeeper>(true);
        if (gk != null)
        {
            var soGk = new SerializedObject(gk);
            soGk.FindProperty("faceBehaviour").objectReferenceValue = face;
            soGk.ApplyModifiedProperties();
        }
        var brain = robot.GetComponentInChildren<PharmeeBrain>(true);
        if (brain != null)
        {
            var soBr = new SerializedObject(brain);
            var p = soBr.FindProperty("faceBehaviour");
            if (p != null && p.objectReferenceValue == null)
            { p.objectReferenceValue = face; soBr.ApplyModifiedProperties(); }
        }

        // ---- 1b. Give way ---------------------------------
        // Pharmee steps aside when the player walks into him.
        var floatBob = robot.GetComponentInChildren<FloatBob>(true);
        if (floatBob != null)
        {
            var giveWay = robot.GetComponentInChildren<PharmeeGiveWay>(true);
            if (giveWay == null) giveWay = floatBob.gameObject.AddComponent<PharmeeGiveWay>();
            Camera pcam = Camera.main;
            if (pcam == null)
                foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                    if (c.CompareTag("MainCamera")) { pcam = c; break; }
            giveWay.Bind(floatBob, floatBob.transform, pcam != null ? pcam.transform : null);
            EditorUtility.SetDirty(giveWay);
            Debug.Log("[NpcPolish] Pharmee give-way wired (bob on '" + floatBob.name + "', cam=" + (pcam != null) + ")");
        }
        else Debug.LogWarning("[NpcPolish] no FloatBob on RobotNPC — give-way skipped");

        // ---- 1c. Gestures -------------------------------------------
        // The animation set: PharmeeState -> body. He has NO skeleton (no skins, no joints,
        // no LimbNodes in either model file), so this is procedural and rides on the two
        // components that already own his transforms.
        var attitude = robot.GetComponentInChildren<PharmeeAttitude>(true);
        if (attitude != null)
        {
            // The model ships two hand pivots ("Hand origin", "Hand origin.002").
            // Matched by prefix because the FBX importer sorts and may rename.
            Transform hL = null, hR = null;
            foreach (var t in robot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.ToLower().StartsWith("hand origin")) continue;
                if (hL == null) hL = t; else if (hR == null) hR = t;
            }
            attitude.BindHands(hL, hR);

            // ENFORCE the tuned values. PharmeeAttitude is authored directly in the scene, so
            // a changed C# default NEVER reaches the saved instance. Set them explicitly, every run.
            var soAtt = new SerializedObject(attitude);
            SetFloat(soAtt, "talkPitchDegrees", 6.5f);
            SetFloat(soAtt, "talkRollDegrees", 3.5f);
            SetFloat(soAtt, "handRaiseDegrees", 55f);
            SetFloat(soAtt, "handSharpness", 8f);
            SetFloat(soAtt, "flowSpeed", PharmeeThrusterMath.DefaultFlowSpeed);
            SetFloat(soAtt, "flowTravel", PharmeeThrusterMath.DefaultTravel);
            SetFloat(soAtt, "flowSpread", PharmeeThrusterMath.DefaultSpread);
            soAtt.ApplyModifiedProperties();
            EditorUtility.SetDirty(attitude);

            var gestures = robot.GetComponentInChildren<PharmeeGestures>(true);
            if (gestures == null) gestures = attitude.gameObject.AddComponent<PharmeeGestures>();
            gestures.Bind(attitude, floatBob, brain,
                          Object.FindAnyObjectByType<ExperimentRunner>(),
                          robot.GetComponentInChildren<LabTourGuide>(true));
            gestures.SetTuning(PharmeeGestureTuning.Default);
            EditorUtility.SetDirty(gestures);

            Debug.Log("[NpcPolish] Pharmee gestures wired (hands: " +
                      (hL != null ? hL.name : "none") + " / " + (hR != null ? hR.name : "none") + ")");
        }
        else Debug.LogWarning("[NpcPolish] no PharmeeAttitude on RobotNPC — gestures skipped");

        // ---- 2. Jimenez roaming ---------------------------------------------
        var jim = GameObject.Find("DrJimenez");
        if (jim == null) jim = GameObject.Find("RiggedDrjimenez");
        if (jim == null)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t.name.ToLower().Contains("jimenez") && t.parent == null) { jim = t.gameObject; break; }
        }
        if (jim != null)
        {
            var runnerGo = GameObject.Find("ExperimentSystems");
            var runner = runnerGo != null ? runnerGo.GetComponent<ExperimentRunner>() : null;
            var animator = jim.GetComponentInChildren<Animator>(true);

            // Observation points: proud of each landmark, standing height on the floor.
            var group = GameObject.Find("ProctorPoints");
            if (group != null) Object.DestroyImmediate(group);
            group = new GameObject("ProctorPoints");
            Undo.RegisterCreatedObjectUndo(group, "Wire NPC Polish");
            var points = new List<Transform>();
            void Point(string landmark, Vector3 fallback, Vector3 offset)
            {
                var lm = GameObject.Find(landmark);
                Vector3 p = (lm != null ? lm.transform.position : fallback) + offset;
                p.y = jim.transform.position.y;                        // keep his standing height
                var pt = new GameObject("Watch_" + landmark).transform;
                pt.SetParent(group.transform, false);
                pt.position = p;
                points.Add(pt);
            }
            Point("ReagentShelf", new Vector3(-4.5f, 0f, -3f), new Vector3(0.9f, 0f, 0f));
            Point("DynamicStage", new Vector3(-2f, 0f, -5f), new Vector3(0.8f, 0f, 0.8f));
            Point("EquipmentShelf", new Vector3(-4.8f, 0f, -6.5f), new Vector3(0.9f, 0f, 0.3f));
            Point("FumeHood_StandIn", new Vector3(2f, 0f, -6.5f), new Vector3(0f, 0f, 1.0f));

            var roamer = jim.GetComponent<ProctorRoamer>();
            if (roamer == null) roamer = jim.AddComponent<ProctorRoamer>();
            roamer.Bind(animator, runner, points);

            // Examiner VOICE: stern greeting + periodic proctor remarks,
            // each driving the animator's "Talking" bool. Uses his narration channel
            // if he has one (else talks via animation only).
            var exam = jim.GetComponent<ExaminerNPC>();
            if (exam == null) exam = jim.AddComponent<ExaminerNPC>();
            var jimNarration = BuildJimenezBubble(jim);   // overhead subtitle so his lines are visible
            if (jimNarration != null) { jimNarration.SetVoiceBlip("jimenez-blip", 0.3f, 6); EditorUtility.SetDirty(jimNarration); }
            exam.Bind(runner, animator, jimNarration);
            EnsureTalkParam(animator);
            Debug.Log("[NpcPolish] Jimenez examiner voice wired (narration=" + (jimNarration != null) + ")");

            // Solid body: a CharacterController stops him phasing through walls while
            // roaming (ProctorRoamer moves via cc.Move) AND blocks the player.
            var cc = jim.GetComponent<CharacterController>();
            if (cc == null) cc = jim.AddComponent<CharacterController>();
            // Fit the capsule from his real renderer bounds — the pivot may not be
            // at the feet (Tripo pivots at model centre), and a floating capsule
            // slides over low furniture / under wall trims.
            var jrs = jim.GetComponentsInChildren<Renderer>();
            if (jrs.Length > 0)
            {
                Bounds jb = jrs[0].bounds;
                foreach (var r in jrs) jb.Encapsulate(r.bounds);
                cc.height = Mathf.Max(1f, jb.size.y * 0.95f);
                // A CharacterController rests with its capsule bottom one skinWidth ABOVE
                // the ground. His bind-pose soles sit JimenezIdleLift below where the idle
                // clip holds them (the model is sunk by that much so the ANIMATED feet meet
                // the floor), so the bottom goes skinWidth above the idle soles. Offsetting
                // from the bind feet instead ("bind feet + 0.02") floats him ~10 cm.
                cc.center = jim.transform.InverseTransformPoint(new Vector3(jb.center.x,
                    jb.min.y + JimenezIdleLift + cc.height * 0.5f + cc.skinWidth, jb.center.z));
            }
            else { cc.height = 1.7f; cc.center = new Vector3(0f, 0.88f, 0f); }
            cc.radius = 0.28f;
            cc.slopeLimit = 45f;
            cc.stepOffset = 0.15f;
            Debug.Log("[NpcPolish] Jimenez CC height=" + cc.height.ToString("F2") + " center=" + cc.center.ToString("F2"));

            // Walk animation: ensure the controller has a "Walking" bool + a Walk
            // state wired Idle⇄Walk (the clip exists; wiring may not).
            EnsureWalkState(animator);
            EditorUtility.SetDirty(jim);
            Debug.Log("[NpcPolish] Jimenez roamer wired (animator=" + (animator != null) + ", runner=" + (runner != null) + ", points=" + points.Count + ", cc added)");
        }
        else Debug.LogWarning("[NpcPolish] Dr. Jimenez not found — roamer skipped");

        // ---- 3. AC hum --------------------------------------------------------
        int hums = 0;
        var humHosts = new List<GameObject>();
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
        {
            string n = t.name.ToLower();
            if ((n == "ac" || n.Contains("aircon") || n.Contains("air_cond") || n.Contains("airconditioner")
                 || n.Contains("ac_unit") || n.Contains("hvac") || n.Contains("vent"))
                && t.GetComponentInChildren<Renderer>() != null)
                humHosts.Add(t.gameObject);
        }
        if (humHosts.Count == 0)
        {
            var hood = GameObject.Find("FumeHood_StandIn");
            if (hood != null) humHosts.Add(hood);   // the hood's fan IS the room's machine noise
        }
        foreach (var host in humHosts)
        {
            var hum = host.GetComponent<ProximityHum>();
            if (hum == null) hum = host.AddComponent<ProximityHum>();
            hum.Bind("ac-hum", 0.5f);      // dedicated AC compressor loop
            EditorUtility.SetDirty(host);
            hums++;
        }
        Debug.Log("[NpcPolish] proximity hums on " + hums + " host(s): "
            + string.Join(", ", humHosts.ConvertAll(h => h.name)));

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(robot.scene);
        Debug.Log("<color=#4CD07D>[NpcPolish] done</color>");
    }

    /// Build (idempotently) a world-space overhead subtitle bubble on Dr. Jimenez so
    /// his examiner remarks are VISIBLE. Mirrors Pharmee's only-while-speaking bubble;
    /// returns its controller.
    static NPCNarrationController BuildJimenezBubble(GameObject jim)
    {
        var existing = jim.transform.Find("JimenezSubtitles");
        if (existing != null) Object.DestroyImmediate(existing.gameObject);

        // Above his head (bounds top ≈ 1.86 m; his pivot is at the floor).
        var root = new GameObject("JimenezSubtitles");
        root.transform.SetParent(jim.transform, false);
        root.transform.localPosition = new Vector3(0f, 2.15f, 0f);

        var canvasGo = new GameObject("Bubble", typeof(Canvas), typeof(FaceCamera));
        canvasGo.transform.SetParent(root.transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 29000;                       // bubble tier (UI conventions)
        var crt = (RectTransform)canvasGo.transform;
        crt.sizeDelta = new Vector2(620f, 220f);
        canvasGo.transform.localScale = Vector3.one * 0.0016f;

        // Dark rounded panel.
        var panel = new GameObject("Panel", typeof(Image));
        panel.transform.SetParent(canvasGo.transform, false);
        Stretch((RectTransform)panel.transform);
        panel.GetComponent<Image>().color = new Color(0.06f, 0.07f, 0.11f, 0.92f);

        // Name plate.
        var nameGo = new GameObject("Name", typeof(TextMeshProUGUI));
        nameGo.transform.SetParent(panel.transform, false);
        var nameT = nameGo.GetComponent<TextMeshProUGUI>();
        nameT.text = "Dr. Jimenez";
        nameT.fontSize = 26f; nameT.fontStyle = FontStyles.Bold;
        nameT.alignment = TextAlignmentOptions.Top;
        nameT.color = new Color(0.75f, 0.85f, 1f, 1f);
        var nrt = nameT.rectTransform;
        nrt.anchorMin = new Vector2(0f, 1f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.pivot = new Vector2(0.5f, 1f); nrt.anchoredPosition = new Vector2(0f, -8f);
        nrt.sizeDelta = new Vector2(-24f, 44f);

        // Subtitle line.
        var textGo = new GameObject("Subtitle", typeof(TextMeshProUGUI));
        textGo.transform.SetParent(panel.transform, false);
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = string.Empty;
        tmp.fontSize = 30f; tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white; tmp.textWrappingMode = TextWrappingModes.Normal;
        var trt = tmp.rectTransform;
        trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 1f);
        trt.offsetMin = new Vector2(18f, 14f); trt.offsetMax = new Vector2(-18f, -50f);

        var narr = root.AddComponent<NPCNarrationController>();
        var so = new SerializedObject(narr);
        so.FindProperty("subtitleText").objectReferenceValue = tmp;
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("playOnStart").boolValue = false;

        // ⛔ This method DESTROYS the previous JimenezSubtitles object, so it destroys his
        // voice wiring with it, and a fresh NPCNarrationController defaults to
        // `speaker = Pharmee` with a NULL `voiceBank`: the subtitle bubble still appears but
        // every line resolves to no clip. `Fix Voice Audibility + Music Ducking` restores the
        // SPEAKER and never the CLIPS, so the channel is wired completely here and one
        // builder run leaves it working with no follow-up menu.
        so.FindProperty("voiceBank").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<VoiceBank>(VoiceImportTool.BankPath);
        so.FindProperty("speaker").enumValueIndex = (int)VoiceSpeaker.Jimenez;
        so.ApplyModifiedProperties();
        VoiceAudioBuilder.EnsureVoiceChannel(narr, out _);
        panel.SetActive(false);                            // silent until he speaks
        return narr;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    /// Make sure the animator has a bool "Talking" and an Idle⇄Talk transition pair
    /// (the rigged Jimenez ships with a Talk clip; this guarantees the wiring).
    static void EnsureTalkParam(Animator animator)
    {
        if (animator == null) return;
        var ctrl = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null && animator.runtimeAnimatorController is AnimatorOverrideController ov)
            ctrl = ov.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null) { Debug.LogWarning("[NpcPolish] Jimenez animator not editable — talk wiring skipped"); return; }

        bool hasParam = false;
        foreach (var p in ctrl.parameters)
            if (p.name == "Talking" && p.type == AnimatorControllerParameterType.Bool) hasParam = true;
        if (!hasParam) ctrl.AddParameter("Talking", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState idle = null, talk = null;
        foreach (var s in sm.states)
        {
            string n = s.state.name.ToLower();
            if (n.Contains("idle")) idle = s.state;
            if (n.Contains("talk")) talk = s.state;
        }
        if (talk == null)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip talk"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) { talk = sm.AddState("Talk"); talk.motion = clip; break; }
            }
        }
        if (idle == null || talk == null)
        { Debug.LogWarning("[NpcPolish] talk: idle=" + (idle != null) + " talk=" + (talk != null) + " — transitions skipped"); EditorUtility.SetDirty(ctrl); return; }

        bool hasOut = false, hasBack = false;
        foreach (var tr in idle.transitions) if (tr.destinationState == talk) hasOut = true;
        foreach (var tr in talk.transitions) if (tr.destinationState == idle) hasBack = true;
        if (!hasOut)
        {
            var tr = idle.AddTransition(talk);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Talking");
        }
        if (!hasBack)
        {
            var tr = talk.AddTransition(idle);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Talking");
        }
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[NpcPolish] talk state OK (param=Talking, idle='" + idle.name + "', talk='" + talk.name + "')");
    }

    /// Make sure the animator has a bool "Walking" and an Idle⇄Walk transition pair.
    static void EnsureWalkState(Animator animator)
    {
        if (animator == null) return;
        var ctrl = animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        if (ctrl == null)
        {
            // scene instances often reference an override/runtime — resolve the base asset
            var rt = animator.runtimeAnimatorController;
            if (rt is AnimatorOverrideController ov)
                ctrl = ov.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
        }
        if (ctrl == null) { Debug.LogWarning("[NpcPolish] Jimenez animator controller not editable — walk wiring skipped"); return; }

        bool hasParam = false;
        foreach (var p in ctrl.parameters)
            if (p.name == "Walking" && p.type == AnimatorControllerParameterType.Bool) hasParam = true;
        if (!hasParam) ctrl.AddParameter("Walking", AnimatorControllerParameterType.Bool);

        var sm = ctrl.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState idle = null, walk = null;
        foreach (var s in sm.states)
        {
            string n = s.state.name.ToLower();
            if (n.Contains("idle")) idle = s.state;
            if (n.Contains("walk")) walk = s.state;
        }
        if (walk == null)
        {
            // find a walk clip in the project (uthana text-to-motion set)
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip walk"))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null) { walk = sm.AddState("Walk"); walk.motion = clip; break; }
            }
        }
        if (idle == null || walk == null)
        { Debug.LogWarning("[NpcPolish] idle=" + (idle != null) + " walk=" + (walk != null) + " — transitions skipped"); return; }

        bool hasOut = false, hasBack = false;
        foreach (var tr in idle.transitions)
            if (tr.destinationState == walk) hasOut = true;
        foreach (var tr in walk.transitions)
            if (tr.destinationState == idle) hasBack = true;
        if (!hasOut)
        {
            var tr = idle.AddTransition(walk);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Walking");
        }
        if (!hasBack)
        {
            var tr = walk.AddTransition(idle);
            tr.hasExitTime = false; tr.duration = 0.15f;
            tr.AddCondition(UnityEditor.Animations.AnimatorConditionMode.IfNot, 0f, "Walking");
        }
        EditorUtility.SetDirty(ctrl);
        Debug.Log("[NpcPolish] walk state OK (param=Walking, idle='" + idle.name + "', walk='" + walk.name + "')");
    }

    public const string FaceUnlitPath = "Assets/PharmaSynth/Materials/PharmeeFaceUnlit.mat";

    /// Unlit URP material for Pharmee's eyes and mouth; PharmeeFace sets its colour.
    /// Double-sided: the eye meshes face backwards in the model, so a back-culled
    /// material hides them (the model's own material draws both sides).
    public static Material FaceUnlitMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(FaceUnlitPath);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "PharmeeFaceUnlit" };
            m.SetColor("_BaseColor", PharmeeGlowMath.CapBrightness(PharmeeGlowMath.CyanHue, PharmeeGlowMath.FaceCeiling));
            AssetDatabase.CreateAsset(m, FaceUnlitPath);
        }
        if (m.HasProperty("_Cull") && m.GetFloat("_Cull") != 0f)
        {
            m.SetFloat("_Cull", 0f);   // UnityEngine.Rendering.CullMode.Off
            m.doubleSidedGI = true;
            EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();
        return m;
    }
}
#endif
