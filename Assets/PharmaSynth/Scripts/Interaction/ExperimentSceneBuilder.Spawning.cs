using System.Collections.Generic;
using UnityEngine;
using TMPro;
using XRGrab = UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable;
using XRSocket = UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor;
using TeleAnchor = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor;
using TeleArea = UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea;

/// Spawning and wiring each kind of stage object: vessels, stations, sensors and bench bindings.
public partial class ExperimentSceneBuilder
{
    // ---- builders ---------------------------------------------------------

    private void BuildStation(Transform stage, ExperimentLayout.Station s)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Station_" + s.taskId;
        pad.transform.SetParent(stage, false);
        pad.transform.position = new Vector3(s.pos.x, s.pos.y + 0.006f, s.pos.z);
        pad.transform.localScale = new Vector3(0.3f, 0.012f, 0.3f);
        var box = pad.GetComponent<BoxCollider>();
        box.isTrigger = true; box.size = new Vector3(1.2f, 40f, 1.2f); box.center = new Vector3(0f, 20f, 0f);
        // Hide the cosmetic pad cube, which only clutters the table, but keep
        // its collider column + every sensor/sim living on it.
        // The socket ghost + billboard label remain the "place here" cues.
        var padMr = pad.GetComponent<MeshRenderer>();
        if (padMr != null) padMr.enabled = false;

        // Zone sims run a sustained chemistry sim; verb stations (Stir/Grind/
        // Weigh) complete through their own tool controllers. Only plain
        // stations still complete on zone-touch.
        bool zoneSim = s.sim == StationSim.Heat || s.sim == StationSim.Crystallise
                    || s.sim == StationSim.Filter || s.sim == StationSim.Collect;
        var st = pad.AddComponent<ExperimentTaskStation>();
        st.Configure(runner, s.taskId, s.requiredItemId, s.sim == StationSim.None, false);
        if (s.sim == StationSim.Weigh) BuildWeighStation(stage, s);

        TemperatureSim temp = null; CrystallizationController cryst = null;
        FiltrationController filt = null; GasCollection gas = null;
        if (zoneSim)
        {
            var sensor = pad.AddComponent<ZoneItemSensor>();
            sensor.SetItemId(s.requiredItemId);
            switch (s.sim)
            {
                case StationSim.Heat:       temp  = pad.AddComponent<TemperatureSim>(); break;
                case StationSim.Crystallise: cryst = pad.AddComponent<CrystallizationController>(); break;
                case StationSim.Filter:     filt  = pad.AddComponent<FiltrationController>(); break;
                case StationSim.Collect:    gas   = pad.AddComponent<GasCollection>(); break;
            }
            var rig = pad.AddComponent<ZoneSimStation>();
            rig.Bind(runner, s.taskId, s.sim, sensor, temp, cryst, filt, gas, s.simTargetC);
            var loop = pad.AddComponent<SimLoopAudio>();
            loop.Bind(SimLoopAudio.KeyFor(s.sim));
            rig.SetLoopAudio(loop);
            var vfx = pad.AddComponent<StationVfx>();   // steam/frost/drip/bubbles while occupied
            vfx.Bind(s.sim);
            rig.SetVfx(vfx);

            // Overheat consequence (error-effects pass): smoke + ruined batch +
            // alarm + Overheat mistake when the sim crosses its threshold.
            if (s.sim == StationSim.Heat && temp != null)
            {
                var overheat = pad.AddComponent<OverheatEffects>();
                overheat.Bind(temp, runner, assets != null ? assets.GetChemical("Ruined Mixture") : null);
            }

            // Hot-surface hazard (§1): touching a HEAT station once it is
            // actually hot records a handling mistake. Player-only (props
            // placed on the pad never trigger it) and armed above 50 °C.
            if (s.sim == StationSim.Heat && temp != null)
            {
                var hot = new GameObject("HotSurface_" + s.taskId);
                hot.transform.SetParent(stage, false);
                hot.transform.position = new Vector3(s.pos.x, s.pos.y + 0.06f, s.pos.z);
                var hotCol = hot.AddComponent<SphereCollider>();
                hotCol.isTrigger = true; hotCol.radius = 0.16f;
                var hz = hot.AddComponent<HazardZone>();
                hz.Configure(runner, LabErrorType.HazardousAction, "Hot surface — don't touch heated apparatus!");
                var tempRef = temp;
                hz.SetArmedCheck(() => tempRef != null && tempRef.AtLeast(50f));
                var cam = Camera.main;
                if (cam != null) hz.SetPlayerRoot(cam.transform.root);
            }
        }

        // Teleport anchor: a floor pad in front of the station so thumbstick
        // teleporters land at each workstation (§2 — only the room-wide floor
        // area existed). Mirrors the existing area's layers/provider.
        var anchorGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anchorGo.name = "TeleAnchor_" + s.taskId;
        anchorGo.transform.SetParent(stage, false);
        Vector3 toward = new Vector3(0.2f, 0f, -2.5f) - new Vector3(s.pos.x, 0f, s.pos.z);
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.right;
        anchorGo.transform.position = new Vector3(s.pos.x, 0.01f, s.pos.z) + toward * 0.75f;
        anchorGo.transform.localScale = new Vector3(0.5f, 0.008f, 0.5f);
        var anchor = anchorGo.AddComponent<TeleAnchor>();
        anchor.teleportAnchorTransform = anchorGo.transform;
        var floorArea = FindAnyObjectByType<TeleArea>();
        if (floorArea != null)
        {
            anchor.interactionLayers = floorArea.interactionLayers;
            anchor.teleportationProvider = floorArea.teleportationProvider;
        }

        // Snap socket: releasing the required prop near the pad clicks it into
        // place (parented to the stage — the pad's non-uniform scale would
        // distort a child). Wrong items are rejected by the filter.
        var sockGo = new GameObject("Socket_" + s.taskId);
        sockGo.transform.SetParent(stage, false);
        sockGo.transform.position = new Vector3(s.pos.x, s.pos.y + 0.02f, s.pos.z);
        var sockCol = sockGo.AddComponent<SphereCollider>();
        sockCol.isTrigger = true; sockCol.radius = 0.10f;
        var filter = sockGo.AddComponent<StationSocketFilter>();
        filter.requiredItemId = s.requiredItemId;
        var sock = sockGo.AddComponent<XRSocket>();
        sock.selectFilters.Add(filter);
        sock.attachTransform = sockGo.transform;
        // Ghost preview: bringing the correct item near shows where it snaps.
        sock.showInteractableHoverMeshes = true;
        sock.interactableHoverMeshMaterial = SocketGhostMaterial();
        sockGo.AddComponent<SelectSfx>().Bind(sock, "socket-snap");

        var labelTmp = MakeLabel(s.label, new Vector3(s.pos.x, s.pos.y + 0.32f, s.pos.z), 0.13f);
        // Live status on the billboard: Heat shows the temperature climb,
        // the other sims show percent progress — the player can SEE the verb work.
        if (zoneSim && labelTmp != null)
            pad.AddComponent<StationStatusLabel>().Bind(labelTmp, s.label, s.sim, temp, cryst, filt, gas, s.simTargetC);
    }

    /// A functional balance fixture at a Weigh station: the Balance model
    /// (fixed, not grabbable), a live grams display, and the WeighStation whose
    /// TaskGraph condition completes the weigh task. The required measure comes
    /// from the layout's vessel binding for the same task (chemical mode); a
    /// station with no such binding falls back to its requiredItemId (tool mode).
    private void BuildWeighStation(Transform stage, ExperimentLayout.Station s)
    {
        GameObject balance;
        var prefab = assets != null ? assets.GetPrefab("Balance") : null;
        if (prefab != null)
        {
            balance = Instantiate(prefab, stage);
            Normalise(balance, "Balance", 0.18f);
        }
        else
        {
            balance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            balance.transform.SetParent(stage, false);
            balance.transform.localScale = new Vector3(0.22f, 0.05f, 0.18f);
        }
        balance.name = "Weigh_" + s.taskId;
        Seat(balance.transform, s.pos);
        // Fixed fixture: collider yes, grab/physics no (the pan must stay put).
        var grab = balance.GetComponent<XRGrab>();
        if (grab != null) Kill(grab);
        var brb = balance.GetComponent<Rigidbody>();
        if (brb != null) Kill(brb);
        if (balance.GetComponentInChildren<Collider>() == null)
        {
            var bc = balance.AddComponent<BoxCollider>();
            bc.size = new Vector3(0.22f, 0.06f, 0.18f);
        }

        // Pan trigger just above the balance top.
        var b = WB(balance);
        var pan = new GameObject("Pan_" + s.taskId);
        pan.transform.SetParent(balance.transform, true);
        pan.transform.position = new Vector3(b.center.x, b.max.y + 0.05f, b.center.z);
        var panCol = pan.AddComponent<BoxCollider>();
        panCol.isTrigger = true;
        panCol.size = new Vector3(0.24f, 0.16f, 0.2f);

        // Grams display above the pan.
        var display = MakeLabel("0.00 g", new Vector3(b.center.x, b.max.y + 0.22f, b.center.z), 0.1f);
        var scale = balance.AddComponent<WeighingScaleController>();
        if (display != null) scale.Bind(display);

        // The required measure: the vessel binding that feeds this task.
        string chem = null; float ml = 0f;
        if (_currentLayout != null)
            foreach (var v in _currentLayout.vessels)
                foreach (var bind in v.bindings)
                    if (bind.taskId == s.taskId) { chem = bind.reagentChemical; ml = bind.requiredMl; }

        var ws = pan.AddComponent<WeighStation>();
        ws.Bind(runner, s.taskId, s.requiredItemId, chem, ml, scale);
    }

    /// Shared translucent-cyan material for socket hover-mesh previews (built once).
    private static Material SocketGhostMaterial()
    {
        if (_socketGhostMat != null) return _socketGhostMat;
        var sh = Shader.Find("Universal Render Pipeline/Unlit");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        var m = new Material(sh) { name = "SocketGhost" };
        var c = new Color(0.5f, 0.9f, 1f, 0.3f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        m.SetOverrideTag("RenderType", "Transparent");
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
        if (m.HasProperty("_SrcBlend")) m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (m.HasProperty("_DstBlend")) m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        _socketGhostMat = m;
        return m;
    }

    private void BuildProp(Transform stage, ExperimentLayout.Prop p)
    {
        var prefab = assets != null ? assets.GetPrefab(p.prefabName) : null;
        if (prefab == null) { Debug.LogWarning("[SceneBuilder] missing prefab " + p.prefabName); return; }
        var inst = Instantiate(prefab, stage);
        inst.name = "Prop_" + p.itemId;
        Normalise(inst, p.prefabName, p.targetHeight);
        RestPoseFor(inst, p.prefabName);
        Seat(inst.transform, p.pos);
        var item = inst.GetComponent<LabItem>() ?? inst.AddComponent<LabItem>();
        item.itemId = p.itemId; item.displayName = p.displayName;
        var rb = PhysicsProfiles.EnsurePhysics(inst, p.prefabName);
        inst.AddComponent<GrabPhysicsPolicy>();
        GrabTuning.Apply(inst.GetComponent<XRGrab>());   // held items collide with the world
        inst.AddComponent<HoverHighlight>().Bind(inst.GetComponent<XRGrab>());   // hover affordance
        // Scooping tools transfer solid reagents by the scoopful.
        if (p.prefabName == "Scoopula" || p.prefabName == "Spatula")
            inst.AddComponent<ScoopController>().Bind(inst.GetComponent<XRGrab>());
        var respawn = inst.AddComponent<DropRespawn>();
        respawn.SetHome(inst.transform.position, inst.transform.rotation);
        if (Mishandling.IsBreakable(p.prefabName))
        {
            var breakable = inst.AddComponent<BreakableGlassware>();
            breakable.Bind(runner, respawn, rb, p.displayName);
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(p.prefabName), Mishandling.DefaultBreakSpeed);
        }
        else
        {
            inst.AddComponent<ImpactSound>().Bind(rb, Mishandling.DropSoundKey(p.prefabName));
        }
        if (p.pourable)
        {
            var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
            lp.registry = registry;
            var chem = assets.GetChemical(p.fillChemical);
            if (chem != null)
                lp.SetContents(chem, SupplyFor(p, chem));   // finite: ~need + 2 spare pours
            EnsureLiquidVisual(inst, lp);                    // visible fill
            var pourer = inst.GetComponent<LiquidPourer>() ?? inst.AddComponent<LiquidPourer>();
            pourer.Bind(lp);
            if (pourer.spout == null)
            {
                // Mouth at the TOP of the item's real bounds — the old guessed
                // (0, 0.12, 0) sat mid-body on tall bottles and above short vials.
                var b = WB(inst);
                var spout = new GameObject("Spout").transform;
                spout.SetParent(inst.transform, true);
                spout.position = new Vector3(b.center.x, b.max.y - 0.005f, b.center.z);
                pourer.spout = spout;
            }
            var spill = inst.AddComponent<SpillMistake>();
            spill.Bind(runner, lp, inst.GetComponent<XRGrab>(), p.displayName);
            inst.AddComponent<HazardousMixReactor>().Bind(lp, runner);   // bad-mix consequences
        }
        var pl = inst.AddComponent<ProximityLabel>(); pl.SetLabel(p.displayName, 1.6f);
        if (p.pourable)
        {
            var plp = inst.GetComponent<LiquidPhysics>();
            if (plp != null)
            {
                inst.AddComponent<VesselStatus>().Bind(plp, pl, p.displayName, 1.6f);   // live supply tag
                inst.AddComponent<MixFeedback>().Bind(plp);
            }
        }
    }

    /// Empty ChemLab prefabs have no liquid child mesh — their `_WithLiquid`
    /// twin carries the authored PharmaLiquid setup (fill bounds, precipitate
    /// renderer). Receiving vessels swap to the twin so poured liquid RENDERS
    /// (otherwise a pour into a beaker shows nothing).
    private GameObject VesselPrefabFor(string prefabName)
    {
        if (assets == null) return null;
        if (!prefabName.EndsWith("_WithLiquid"))
        {
            var twin = assets.GetPrefab(prefabName + "_WithLiquid");
            if (twin != null) return twin;
        }
        return assets.GetPrefab(prefabName);
    }

    /// One RackTaskGroup per (rackGroup, shared task): the step completes only once as many
    /// tubes are READY as the step has roles.
    ///
    /// ⭐ The members are the whole family POOL, not a fixed set of tubes.
    /// All 19 tubes sit on the same shelf and look identical, so which TUBE is
    /// not a decision the player can be asked to make; a closed set refuses every drop
    /// into a tube outside it and the step never ticks. What still counts is how MANY
    /// tubes the step names: a role the step does not name — Exp 2's negative control —
    /// is not counted, so leaving the control alone stays correct play.
    private void WireRackGroups(Transform stage, ExperimentLayout layout)
    {
        var pools = WireGlasswarePools(layout);

        foreach (var kv in _rackBindings)
        {
            // Tasks this group DEFERS (completesTask:false) — the ones a group owns.
            var tasks = new List<string>();
            foreach (var v in layout.vessels)
            {
                if (v.rackGroup != kv.Key) continue;
                foreach (var b in v.bindings)
                    if (!b.completesTask && !tasks.Contains(b.taskId)) tasks.Add(b.taskId);
            }

            // Whichever pool adopted this group's tubes; a family with no spare on the bench
            // is not pooled at all, and then the group's own members are still the truth.
            var members = kv.Value;
            foreach (var p in pools.Values)
                if (kv.Value.Count > 0 && p.Members.Contains(kv.Value[0])) { members = p.Members; break; }

            foreach (var taskId in tasks)
            {
                // REQUIRED = how many of the group's roles name the task.
                // Three butyl alcohols beside one untouched
                // control is "3 of 4 tubes", and it must stay 3.
                int required = 0;
                foreach (var v in layout.vessels)
                {
                    if (v.rackGroup != kv.Key) continue;
                    foreach (var b in v.bindings)
                        if (b.taskId == taskId) { required++; break; }
                }
                if (required == 0 || members.Count == 0) continue;
                var go = new GameObject("Rack_" + kv.Key + "_" + taskId);
                go.transform.SetParent(stage, false);
                go.transform.position = members[0].transform.position;
                go.AddComponent<RackTaskGroup>().Bind(runner, taskId, members, required);
            }
        }
    }

    /// ANY GLASSWARE WILL DO (tubes, every spared kind, and rack groups too:
    /// any rack can hold a test tube while the experiment runs).
    ///
    /// A role authored onto a SPECIFIC bench object is guidance a VR player cannot follow:
    /// they grab whichever glass is nearest, and every tube in the lab looks the same. Every
    /// free bench object of a POOL (VesselRoleMatch.PoolKey: tubes by name family because
    /// hard glass shares their itemId, everything else by LabItem itemId) carries the
    /// module's whole role set as candidates, each pour narrows it, and a role's non-pour
    /// anchors move to whichever object claims it. A kind with a single instance on the
    /// bench is never pooled: there is only one Florence flask, and a fixed role is right
    /// there. Hard glass stays its own family — Exp 6's naked flame is the reason it exists.
    ///
    /// Returns the pools so WireRackGroups can hand a shared step the whole pool as members.
    private Dictionary<string, GlasswarePool> WireGlasswarePools(ExperimentLayout layout)
    {
        var pools = new Dictionary<string, GlasswarePool>();

        // The roles: EVERY bench-bound vessel of this layout, racked or standalone, grouped
        // by pool key. Racked vessels are included, never wired as closed groups.
        var rolesByKey = new Dictionary<string, List<ExperimentLayout.Vessel>>();
        foreach (var v in layout.vessels)
        {
            if (v == null || string.IsNullOrEmpty(v.benchItem)) continue;
            var go = FindBenchItem(v.benchItem);
            var li = go != null ? go.GetComponent<LabItem>() : null;
            string key = VesselRoleMatch.PoolKey(v.benchItem, li != null ? li.itemId : "");
            if (key == "") continue;
            if (!rolesByKey.TryGetValue(key, out var list)) rolesByKey[key] = list = new List<ExperimentLayout.Vessel>();
            list.Add(v);
        }

        var allItems = FindObjectsByType<LabItem>(FindObjectsInactive.Include);

        foreach (var kv in rolesByKey)
        {
            var roleVessels = kv.Value;

            // Only a kind with SPARES is pooled. One flask on the bench = one fixed role, and
            // pooling it would only add a binding that can never be used.
            int onBench = 0;
            foreach (var li in allItems)
                if (li != null && VesselRoleMatch.PoolKey(li.name, li.itemId) == kv.Key) onBench++;
            if (onBench < 2) continue;

            var roles = new List<List<LiquidTaskBinding.ReagentStep>>();
            var members = new List<LiquidTaskBinding>();
            var authoredIndex = new Dictionary<LiquidTaskBinding, int>();
            var authoredObject = new Dictionary<int, GameObject>();

            // Authored objects: BuildVessel already wired them; their steps ARE the roles.
            for (int r = 0; r < roleVessels.Count; r++)
            {
                var go = FindBenchItem(roleVessels[r].benchItem);
                var bind = go != null ? go.GetComponent<LiquidTaskBinding>() : null;
                roles.Add(bind != null ? new List<LiquidTaskBinding.ReagentStep>(bind.ExpectedSteps)
                                       : new List<LiquidTaskBinding.ReagentStep>());
                if (bind != null) { members.Add(bind); authoredIndex[bind] = r; authoredObject[r] = go; }
            }
            if (roles.Count == 0) continue;

            // Extras: every OTHER free bench object of the pool gets a binding with no
            // authored role — it advertises nothing until it claims. Capacity follows the
            // authored vessel's kind: a 500 ml beaker extra must not become a 25 ml tube.
            string prefabKind = roleVessels[0].prefabName;
            foreach (var li in allItems)
            {
                if (li == null || VesselRoleMatch.PoolKey(li.name, li.itemId) != kv.Key) continue;
                var go = li.gameObject;
                if (go.GetComponent<LiquidTaskBinding>() != null) continue;   // authored here, or already a member
                var lp = go.GetComponent<LiquidPhysics>() ?? go.AddComponent<LiquidPhysics>();
                lp.registry = registry;
                lp.maxVolume = BenchMaxVolumeFor(prefabKind, lp.maxVolume);
                lp.SetContents(null, 0f);
                EnsureLiquidVisual(go, lp);
                var bind = go.AddComponent<LiquidTaskBinding>();
                bind.SetVesselAndRunner(lp, runner);
                bind.SetFumeHood(FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include));
                WireVesselServices(go, lp, BenchDisplayNameFor(li.name));
                members.Add(bind);
            }

            var shared = new RackRoles();
            foreach (var member in members)
            {
                var m = member;
                m.MarkPoolMember();
                m.SetRoles(roles, authoredIndex.TryGetValue(m, out int idx) ? idx : -1, shared);
                var inst = m.gameObject;
                var lp = inst.GetComponent<LiquidPhysics>();
                m.RoleClaimed += claimed =>
                {
                    if (claimed < 0 || claimed >= roleVessels.Count || m == null) return;
                    var role = roleVessels[claimed];
                    AttachAnchors(inst, role, m, lp);
                    // A claimed role lives in ONE place: the authored object's now
                    // orphaned anchors for this role are removed, so the inert twin can neither
                    // register as a guidance target nor complete anything.
                    if (authoredObject.TryGetValue(claimed, out var authored) && authored != null && authored != inst)
                        DetachAnchors(authored, role);
                    var status = inst.GetComponent<VesselStatus>();
                    if (status != null) status.SetRoleSuffix(LabelForRole(role));
                    // The target map is DERIVED from the live components and built once per
                    // run; the truth just moved, so rebuild it — the heat / litmus / weigh
                    // step's glow and arrow now point at the object that will complete it.
                    if (Application.isPlaying) { TaskTargetRegistry.Clear(); TutorialTargets.Build(); }
                };
            }

            var pool = new GlasswarePool();
            pool.RoleVessels.AddRange(roleVessels);
            pool.Members.AddRange(members);
            pools[kv.Key] = pool;
        }

        return pools;
    }

    /// Remove the anchors `role` would have bolted onto `go` (the mirror of AttachAnchors),
    /// matched by TASK so anchors for other roles on the same object are untouched.
    private static void DetachAnchors(GameObject go, ExperimentLayout.Vessel role)
    {
        if (go == null || role == null) return;
        string heatTask = role.heatTaskId;
        if (string.IsNullOrEmpty(heatTask))
            foreach (var b in role.bindings)
                if (!b.completesTask && !string.IsNullOrEmpty(b.taskId)) { heatTask = b.taskId; break; }
        foreach (var c in go.GetComponents<VesselHeatTask>())        if (c != null && c.TaskId == heatTask && role.heatToC > 0f) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<VesselWeighTask>())       if (c != null && c.TaskId == role.weighTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<VaporCollectController>()) if (c != null && c.VaporTaskId == role.vaporTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<VesselChillTask>())       if (c != null && c.TaskId == role.chillTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<VesselLitmusTask>())      if (c != null && c.TaskId == role.litmusTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<VesselFlameTask>())       if (c != null && c.TaskId == role.flameTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<StirController>())        if (c != null && c.TaskId == role.stirTaskId) { c.Detach(); Kill(c); }
        foreach (var c in go.GetComponents<FermentationController>()) if (c != null && c.FermentTaskId == role.fermentTaskId) { c.Detach(); Kill(c); }
    }

    /// A player-facing name for a claimed role: the LABEL of the first task its steps
    /// serve ("Tollen's test"). Empty when no definition was handed over.
    private string LabelForRole(ExperimentLayout.Vessel role)
    {
        if (_module == null || role == null) return "";
        foreach (var b in role.bindings)
        {
            if (b == null || string.IsNullOrEmpty(b.taskId)) continue;
            foreach (var t in _module.graphTasks)
                if (t != null && t.taskId == b.taskId && !string.IsNullOrEmpty(t.label)) return t.label;
        }
        return "";
    }

    private void BuildVessel(Transform stage, ExperimentLayout.Vessel v)
    {
        // BENCH-BOUND vessel: the object already exists and is placed by hand, so
        // adopt it instead of spawning a twin beside it (the hard client rule keeps
        // every tool permanently out). Only the task wiring is attached; its
        // transform, physics and grab setup are the scene's, not ours.
        GameObject inst;
        if (!string.IsNullOrEmpty(v.benchItem))
        {
            inst = FindBenchItem(v.benchItem);
            if (inst == null)
            {
                Debug.LogWarning("[SceneBuilder] layout wants bench item '" + v.benchItem
                                 + "' but it is not in the scene — step cannot complete.");
                return;
            }
        }
        else
        {
            var prefab = VesselPrefabFor(v.prefabName);
            if (prefab == null) { Debug.LogWarning("[SceneBuilder] missing vessel prefab " + v.prefabName); return; }
            inst = Instantiate(prefab, stage);
            inst.name = "Vessel_" + v.prefabName;   // keep the AUTHORED name (RealSizes/Mishandling lookups)
            Normalise(inst, v.prefabName, v.targetHeight);
            Seat(inst.transform, v.pos);
            PhysicsProfiles.EnsurePhysics(inst, v.prefabName);   // vessels stay kinematic (no release policy)
            GrabTuning.Apply(inst.GetComponent<XRGrab>());       // collide while held; re-freezes on release
        }
        var lp = inst.GetComponent<LiquidPhysics>() ?? inst.AddComponent<LiquidPhysics>();
        lp.registry = registry;
        // Bench glass keeps LiquidPhysics' serialized default of 1000 ml — a "test
        // tube" the size of a bucket. Fill renders as volume/maxVolume, so 5 counted
        // drops was 0.5%: invisible, as if no drop landed.
        // Adopting a bench vessel gives it its REAL capacity.
        if (!string.IsNullOrEmpty(v.benchItem))
            lp.maxVolume = BenchMaxVolumeFor(v.prefabName, lp.maxVolume);
        // ALWAYS set contents explicitly: the _WithLiquid twin serializes a
        // phantom half-fill, and a blank start must arm the wake-from-empty
        // branch (chem null + 0 ml) so the first pour adopts its chemical.
        var startChem = !string.IsNullOrEmpty(v.startChemical) ? assets.GetChemical(v.startChemical) : null;
        lp.SetContents(startChem, startChem != null ? lp.maxVolume * 0.3f : 0f);
        EnsureLiquidVisual(inst, lp);
        var bind = inst.AddComponent<LiquidTaskBinding>();
        bind.SetVesselAndRunner(lp, runner);
        // No hood reference is wired, so the requiresFumeHood check is position-based.
        // The pour is sanctioned only when THIS vessel is inside the hood.
        bind.SetFumeHood(FindAnyObjectByType<FumeHoodZone>(FindObjectsInactive.Include));
        foreach (var b in v.bindings)
        {
            var reagent = assets.GetChemical(b.reagentChemical);
            if (reagent != null) bind.AddExpected(reagent, b.taskId, b.requiredMl, b.completesTask);
        }
        if (!string.IsNullOrEmpty(v.rackGroup))
        {
            if (!_rackBindings.TryGetValue(v.rackGroup, out var list))
                _rackBindings[v.rackGroup] = list = new List<LiquidTaskBinding>();
            list.Add(bind);
        }
        AttachAnchors(inst, v, bind, lp);
        WireVesselServices(inst, lp, !string.IsNullOrEmpty(v.benchItem) ? BenchDisplayNameFor(v.benchItem) : v.displayName);
    }

    /// Every NON-POUR anchor a role needs (heat, weigh, vapor, chill, litmus, flame, stir,
    /// ferment), attached to `inst` for the role `v` describes.
    ///
    /// Extracted from BuildVessel so it can be called for a role the object was NOT
    /// authored for: a pooled tube attaches these the moment it CLAIMS a role, so the litmus
    /// strip or the water bath completes the step on the tube the player actually used.
    /// Every anchor gates on the binding's readiness for its task, so one attached to a tube
    /// that never claims that role stays inert; the clear path strips it on the next build.
    private void AttachAnchors(GameObject inst, ExperimentLayout.Vessel v, LiquidTaskBinding bind, LiquidPhysics lp)
    {
        // ZONE-FREE heat step: the vessel's deferred task completes
        // when it is served AND heated to heatToC — wherever the player does it.
        // There is no fixed Heat station, pad, label or teleport anchor.
        // An EXPLICIT heatTaskId (Exp 6's heat-glow) wins over the inferred
        // first-deferred binding — for heat steps with no reagents of their own,
        // where the binding gate is skipped (null binding = vacuously served).
        if (v.heatToC > 0f)
        {
            string heatTask = v.heatTaskId;
            if (string.IsNullOrEmpty(heatTask))
                foreach (var b in v.bindings)
                    if (!b.completesTask && !string.IsNullOrEmpty(b.taskId)) { heatTask = b.taskId; break; }
            if (!string.IsNullOrEmpty(heatTask))
            {
                bool heatHasSteps = false;
                foreach (var b in v.bindings) if (b.taskId == heatTask) heatHasSteps = true;
                (inst.GetComponent<VesselHeatTask>() ?? inst.AddComponent<VesselHeatTask>())
                    .Bind(runner, heatTask, v.heatToC, heatHasSteps ? bind : null, lp);
            }
            else
                Debug.LogWarning("[SceneBuilder] " + v.benchItem + " sets heatToC but has no deferred (completesTask:false) binding to own.");
        }
        // WEIGH step on the bench balance (Exp 6): served AND settled on the pan.
        if (!string.IsNullOrEmpty(v.weighTaskId))
        {
            bool weighHasSteps = false;
            foreach (var b in v.bindings) if (b.taskId == v.weighTaskId) weighHasSteps = true;
            (inst.GetComponent<VesselWeighTask>() ?? inst.AddComponent<VesselWeighTask>())
                .Bind(runner, v.weighTaskId, lp, weighHasSteps ? bind : null);
        }
        // VAPOR collection source (Exp 6): the hot tube condenses the module's
        // product into the nearest vessel whose binding expects it.
        if (!string.IsNullOrEmpty(v.vaporTaskId))
        {
            var productChem = assets.GetChemical(DemoMode.ProductFor(moduleBeingBuilt));
            (inst.GetComponent<VaporCollectController>() ?? inst.AddComponent<VaporCollectController>())
                .Bind(runner, lp, v.vaporTaskId, productChem, Mathf.Max(1f, v.heatToC));
        }
        // ZONE-FREE chill step (Exp 4): completes when the vessel holds product
        // AND has been cooled to chillToC — the ice bucket, anywhere in the lab.
        // If the chill task has its own reagent steps (Exp 5's 20 ml ice-cold
        // water), the binding gates completion too: served AND cold.
        if (v.chillToC > 0f && !string.IsNullOrEmpty(v.chillTaskId))
        {
            bool chillHasSteps = false;
            foreach (var b in v.bindings) if (b.taskId == v.chillTaskId) chillHasSteps = true;
            (inst.GetComponent<VesselChillTask>() ?? inst.AddComponent<VesselChillTask>())
                .Bind(runner, v.chillTaskId, v.chillToC, lp, chillHasSteps ? bind : null);
        }
        // LITMUS confirmation (Exp 4): a strip touching this vessel while the
        // mixture reads acid completes the task — zone-free, no station.
        if (!string.IsNullOrEmpty(v.litmusTaskId))
            (inst.GetComponent<VesselLitmusTask>() ?? inst.AddComponent<VesselLitmusTask>())
                .Bind(runner, v.litmusTaskId, bind, lp);
        // FLAME confirmation (Exp 7): a lit match/burner flame held to the served
        // sample completes the task — the NEGATIVE ("won't ignite") is the result.
        if (!string.IsNullOrEmpty(v.flameTaskId))
        {
            bool flameHasSteps = false;
            foreach (var b in v.bindings) if (b.taskId == v.flameTaskId) flameHasSteps = true;
            (inst.GetComponent<VesselFlameTask>() ?? inst.AddComponent<VesselFlameTask>())
                .Bind(runner, v.flameTaskId, flameHasSteps ? bind : null, lp);
        }
        // ZONE-FREE stir step (Exp 8 "shake frequently"): circle the bench glass
        // rod inside this vessel, anywhere — StirController is vessel-relative.
        if (!string.IsNullOrEmpty(v.stirTaskId))
        {
            var rodGo = FindBenchItem("Eq_GlassRod");
            (inst.GetComponent<StirController>() ?? inst.AddComponent<StirController>())
                .Bind(runner, v.stirTaskId, lp, rodGo != null ? rodGo.transform : null);
        }
        // FERMENTATION flask (Exp 3): evolves CO₂ into nearby limewater vessels.
        if (!string.IsNullOrEmpty(v.fermentTaskId))
        {
            var co2 = assets.GetChemical("Carbon Dioxide");
            var lime = assets.GetChemical("Limewater");
            (inst.GetComponent<FermentationController>() ?? inst.AddComponent<FermentationController>())
                .Bind(runner, lp, v.fermentTaskId, co2, lime);
        }
    }

    /// The per-vessel services every task vessel gets: hazard reactor, neutral label,
    /// live status, mix feedback and the ability to pour OUT. Shared by BuildVessel and
    /// the tube pools, so a pooled extra is indistinguishable from an authored tube.
    private void WireVesselServices(GameObject inst, LiquidPhysics lp, string labelName)
    {
        // GetComponent-or-Add: a SPAWNED vessel is fresh, but a BENCH item survives
        // every rebuild — blind AddComponent would stack a new copy of each of these
        // on it per module load.
        (inst.GetComponent<HazardousMixReactor>() ?? inst.AddComponent<HazardousMixReactor>()).Bind(lp, runner);
        // A BENCH item is shared apparatus, so its floating label must stay NEUTRAL
        // ("Test Tube 3"), never the layout's internal role name ("TollensTube_1") —
        // that reads as "this tube is only for Tollens" and goes stale the moment
        // another module adopts the same tube.
        var pl = inst.GetComponent<ProximityLabel>() ?? inst.AddComponent<ProximityLabel>();
        pl.SetLabel(labelName, 1.6f);
        (inst.GetComponent<VesselStatus>() ?? inst.AddComponent<VesselStatus>()).Bind(lp, pl, labelName, 1.6f);
        (inst.GetComponent<MixFeedback>() ?? inst.AddComponent<MixFeedback>()).Bind(lp);
        // POUR-OUT ability: a task vessel must also POUR — "tilt the
        // beaker through the funnel" and every draw-from-your-own-product step
        // need a LiquidPourer on the vessel, not only on shelf bottles
        // (the sim's direct PourOut calls would hide its absence). WireBottle is
        // idempotent: pourer + spout + spill grading, only where missing.
        ShelfPourWiring.WireBottle(inst, runner, registry);
    }

    /// Real capacity for an adopted bench vessel, keyed off the layout's prefab kind.
    /// Pure so the suite pins the table. A 25 ml tube makes 5 drops a visible 20%
    /// fill and puts Exp 2's "to the 10 ml mark" pour at a readable 40%.
    public static float BenchMaxVolumeFor(string prefabName, float current)
    {
        if (string.IsNullOrEmpty(prefabName)) return current;
        if (prefabName.Contains("TestTube")) return 25f;
        if (prefabName.Contains("Beaker_100")) return 100f;
        if (prefabName.Contains("Beaker_500")) return 500f;
        if (prefabName.Contains("GraduatedCylinder_50")) return 50f;
        if (prefabName.Contains("ErlenmeyerFlask")) return 400f;
        if (prefabName.Contains("FlorenceFlask")) return 250f;      // Exp 3 fermentation
        if (prefabName.Contains("DistillingFlask")) return 250f;    // Exp 3 distillation
        if (prefabName.Contains("WatchGlass")) return 20f;          // combustion test drops
        return current;
    }

    /// Neutral player-facing label for a bench item, derived from its scene name —
    /// "Kit_TestTube_3" → "Test Tube 3", "Eq_Beaker_100mL" → "Beaker 100 mL". Pure
    /// so the suite pins it: shared apparatus must never carry a reagent-role name.
    public static string BenchDisplayNameFor(string benchItem)
    {
        if (string.IsNullOrEmpty(benchItem)) return benchItem;
        string n = benchItem;
        foreach (var p in new[] { "Kit_", "Eq_", "Raw_" })
            if (n.StartsWith(p)) { n = n.Substring(p.Length); break; }
        return n.Replace("Hard-GlassTestTube", "Hard-Glass Test Tube")
                .Replace("TestTube", "Test Tube")
                .Replace("GraduatedCylinder", "Graduated Cylinder")
                .Replace("mL", " mL")
                .Replace('_', ' ')
                .Replace("  ", " ")
                .Trim();
    }

    /// A permanent bench object by name (Kit_TestTube_3, Eq_Beaker_100mL…). Searched
    /// scene-wide because the bench lives OUTSIDE the stage — that is the whole point.
    private GameObject FindBenchItem(string name)
    {
        foreach (var li in FindObjectsByType<LabItem>(FindObjectsInactive.Include))
            if (li != null && li.name == name) return li.gameObject;
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include))
            if (t.name == name) return t.gameObject;
        return null;
    }

    /// Strip the task wiring off every bench item before a build.
    ///
    /// Build() clears only the STAGE's children, so bindings attached to bench objects
    /// (which live outside it) would survive into the next module and silently complete
    /// its steps with the previous experiment's reagents. Bench-bound vessels only work
    /// if their wiring is torn down first.
    private void ClearBenchBindings()
    {
        foreach (var b in FindObjectsByType<LiquidTaskBinding>(FindObjectsInactive.Include))
        {
            if (b == null || (_stage != null && b.transform.IsChildOf(_stage))) continue;   // stage ones die with the stage
            b.Detach();   // DestroyImmediate skips OnDisable in edit mode → ghost subscriptions
            Kill(b);
        }
        foreach (var g in FindObjectsByType<RackTaskGroup>(FindObjectsInactive.Include))
        {
            if (g == null || (_stage != null && g.transform.IsChildOf(_stage))) continue;
            Kill(g);
        }
        foreach (var h in FindObjectsByType<VesselHeatTask>(FindObjectsInactive.Include))
        {
            if (h == null || (_stage != null && h.transform.IsChildOf(_stage))) continue;
            h.Detach();
            Kill(h);
        }
        foreach (var fc in FindObjectsByType<FermentationController>(FindObjectsInactive.Include))
        {
            if (fc == null || (_stage != null && fc.transform.IsChildOf(_stage))) continue;
            fc.Detach();
            Kill(fc);
        }
        foreach (var ct in FindObjectsByType<VesselChillTask>(FindObjectsInactive.Include))
        {
            if (ct == null || (_stage != null && ct.transform.IsChildOf(_stage))) continue;
            ct.Detach();
            Kill(ct);
        }
        foreach (var lt in FindObjectsByType<VesselLitmusTask>(FindObjectsInactive.Include))
        {
            if (lt == null || (_stage != null && lt.transform.IsChildOf(_stage))) continue;
            lt.Detach();
            Kill(lt);
        }
        foreach (var wt in FindObjectsByType<VesselWeighTask>(FindObjectsInactive.Include))
        {
            if (wt == null || (_stage != null && wt.transform.IsChildOf(_stage))) continue;
            wt.Detach();
            Kill(wt);
        }
        foreach (var vc in FindObjectsByType<VaporCollectController>(FindObjectsInactive.Include))
        {
            if (vc == null || (_stage != null && vc.transform.IsChildOf(_stage))) continue;
            vc.Detach();
            Kill(vc);
        }
        foreach (var ft in FindObjectsByType<VesselFlameTask>(FindObjectsInactive.Include))
        {
            if (ft == null || (_stage != null && ft.transform.IsChildOf(_stage))) continue;
            ft.Detach();
            Kill(ft);
        }
        foreach (var sc in FindObjectsByType<StirController>(FindObjectsInactive.Include))
        {
            if (sc == null || (_stage != null && sc.transform.IsChildOf(_stage))) continue;
            sc.Detach();
            Kill(sc);
        }
    }

    /// Guarantee the vessel/prop can RENDER its liquid: if LiquidPhysics has no
    /// mainRenderer (or it points at the glass shell, which lacks the PharmaLiquid
    /// fill properties), build an inset "Liquid" child running the liquid shader.
    /// sharedMaterial only — edit-mode safe (the suite drives the builder).
    public static void EnsureLiquidVisual(GameObject inst, LiquidPhysics lp)
    {
        if (inst == null || lp == null) return;

        // Solids/powders (sodium acetate, soda lime, salicylic acid…) must NOT
        // read as a sloshing liquid vial. Render an opaque granular mound in
        // the lower part of the jar instead of the translucent liquid shader.
        var chem = lp.currentChemical;
        if (chem != null && (chem.state == PhysicalState.Solid || chem.state == PhysicalState.Powder))
            EnsurePowderVisual(inst, chem);
        // ⛔ NO early return. A vessel that happens to hold a SOLID when the stage is built
        // still needs its liquid surface, because the procedure pours onto that powder a
        // step later (Exp 2 boils scooped aspirin in acid). The old code returned here, the
        // surface was never built, and the resulting 13.5 ml drew nothing at all. Which of
        // the two is visible is decided per frame by LiquidPhysics.DryPowder, not here.

        bool authoredFill = lp.mainRenderer != null && lp.mainRenderer.sharedMaterial != null
            && lp.mainRenderer.sharedMaterial.HasProperty("_Fill");   // authored setup (e.g. _WithLiquid twin)
        if (!authoredFill)
        {
            // Reuse an existing child named "Liquid" when present but unwired.
            Renderer liquidR = null;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Liquid" && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Fill")) { liquidR = r; break; }

            if (liquidR == null)
                liquidR = BuildFillChild(inst, "Liquid", 0.72f);
            lp.mainRenderer = liquidR;
        }

        // PRECIPITATE layer: bench-adopted glass has no
        // precipitateRenderer (only the authored _WithLiquid twins carry one),
        // and without it Exp 4's MnO2 sludge, white benzoic crystals and buff ferric
        // benzoate would fire their observation text with NOTHING visible in the
        // vessel. Same inset-cylinder trick, slightly wider so the settled
        // layer reads at the bottom through the liquid.
        if (lp.precipitateRenderer == null)
        {
            Renderer pptR = null;
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                if (r.name == "Precipitate" && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Fill")) { pptR = r; break; }
            if (pptR == null) pptR = BuildFillChild(inst, "Precipitate", 0.76f);
            if (pptR != null) pptR.enabled = false;   // LiquidPhysics enables it while ppt volume > 1 ml
            lp.precipitateRenderer = pptR;
        }
    }

    /// Inset PharmaLiquid cylinder fitted to the vessel's glass bounds — the
    /// runtime fill surface for adopted bench glass (Liquid + Precipitate layers).
    /// Round-bottom glassware: the fill must take the BULB's shape, not a beaker's
    /// straight column. A sphere clipped by the same _Fill
    /// height gives exactly the spherical cap a round bottom holds. Pure + pinned.
    public static bool IsRoundBottom(string objectName)
    {
        if (string.IsNullOrEmpty(objectName)) return false;
        string n = objectName.ToLowerInvariant();
        return n.Contains("florence") || n.Contains("distillingflask") || n.Contains("distilling_flask")
            || n.Contains("distillationflask") || n.Contains("roundbottom") || n.Contains("boilingflask");
    }

    private static Renderer BuildFillChild(GameObject inst, string childName, float widthFrac)
    {
        var shader = Shader.Find("PharmaSynth/Liquid");
        if (shader == null) return null;   // shader stripped — leave numeric-only rather than magenta
        var b = WB(inst);
        bool round = IsRoundBottom(inst.name);
        var go = GameObject.CreatePrimitive(round ? PrimitiveType.Sphere : PrimitiveType.Cylinder);
        go.name = childName;
        var col = go.GetComponent<Collider>();
        if (col != null) { if (Application.isPlaying) Destroy(col); else DestroyImmediate(col); }
        go.transform.SetParent(inst.transform, true);
        // Inset body, floor to just under the rim — or, for a round bottom, a
        // sphere seated in the bulb (as wide as it is tall).
        float w = Mathf.Min(b.size.x, b.size.z) * widthFrac;
        float h = round ? w : Mathf.Max(0.01f, b.size.y * 0.86f);
        go.transform.position = new Vector3(
            b.center.x, b.min.y + h * 0.5f + b.size.y * (round ? 0.02f : 0.04f), b.center.z);
        go.transform.rotation = inst.transform.rotation;
        var ls = inst.transform.lossyScale;
        go.transform.localScale = new Vector3(
            w / Mathf.Max(1e-4f, Mathf.Abs(ls.x)),
            (round ? h : h * 0.5f) / Mathf.Max(1e-4f, Mathf.Abs(ls.y)),   // cylinder mesh is 2 units tall, sphere 1
            w / Mathf.Max(1e-4f, Mathf.Abs(ls.z)));
        var r2 = go.GetComponent<Renderer>();
        r2.sharedMaterial = new Material(shader) { name = "PharmaLiquid_Runtime" };
        r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return r2;
    }
}
