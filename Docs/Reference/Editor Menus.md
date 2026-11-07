# Editor Menus

> [!warning] Generated note - do not hand-edit
> Derived from the code by `python Tools/gen-vault-reference.py`.
> To change what it says, fix the thing it is derived FROM, then re-run.

Every `[MenuItem]` in the project. Builders are **idempotent and re-runnable**
by design - that is the project convention for all scene edits.

> [!danger] Read [[Gotchas]] before running a stocking/rebuilding builder
> Several of these DESTROY and recreate what they touch, silently taking
> hand-placed components and transforms with them.

Up: [[Home]] - [[Process MOC]] - [[Build and Test Loop]]

---

## Tools/PharmaSynth

### `Tools/PharmaSynth/Add Placement Anchors (drag to fine-tune)`
<sub>`Assets/PharmaSynth/Scripts/Editor/MethaneAnchors.cs`</sub>

Creates the draggable placement anchors the verbs read. Each match/burner gets a "FlameAnchor" child and each scoopula/spatula a "ScoopAnchor" child, dropped at a best-guess spot. SELECT it in the Hierarchy (orange gizmo in the Scene view), drag it onto the exact part (match head, burner mouth, scoop bowl), then run Lock My Layout to bake it. Idempotent: never moves an anchor you've already positioned.

### `Tools/PharmaSynth/Adopt Manual Layout`
<sub>`Assets/PharmaSynth/Scripts/Editor/ManualLayoutAdopter.cs`</sub>

Adopts the hand-placed workspace (kits, duplicates, reagent shelf, spawn point) as the canonical layout in ONE run: 1. renames editor duplicates ("Beaker_100mL (1)") to clean unique names + display names, and gives them the full interaction wiring; 2. re-points the teleport target (FrontDoorSpawn) at the rig's current pose; 3. re-homes every DropRespawn to its current transform; 4. creates + registers the DistillingFlask if missing (its model is an unimported .glb, so no prefab exists) and parks

### `Tools/PharmaSynth/Align Experiment Stages to Table`
<sub>`Assets/PharmaSynth/Scripts/Editor/AlignExperimentStages.cs`</sub>

Experiments spawn their stations/vessels/labels/ waypoints as children of DynamicStage (and Methane uses MethaneStage), both authored at the center table's ORIGINAL spot. This shifts both stages by the table's horizontal offset from that spot so all spawns land on the table wherever it now stands. Idempotent (absolute set from the current table position). Re-run after any further table move.

### `Tools/PharmaSynth/Anchor Furniture (stools not grabbable)`
<sub>`Assets/PharmaSynth/Scripts/Editor/AnchorFurniture.cs`</sub>

Furniture is scenery, not equipment: the stools must not be grabbable. The six lab stools in the Environment prefab carry an `XRGrabInteractable` each, so a player could pick a stool up and carry it around the lab, or fling it across the bench mid-experiment. Nothing in the game asks them to move a stool, and every real interaction the lab needs is with glassware and tools. ⛔ The GRAB goes, the COLLIDER stays. The stool still stops the player walking through it and still holds anything resting o

### `Tools/PharmaSynth/Apply Manuscript Data`
<sub>`Assets/PharmaSynth/Scripts/Editor/ManuscriptDataApplier.cs`</sub>

alone is chemically INERT) → propyl alcohol staged + bound, rule pointed at it (manuscript/test intent: ester with propyl). M2  Chloroform gets the manuscript's oxidation confirmatory test (K-dichromate + conc H2SO4, procedure L3419-21 + results sheet) → new task + reagent + reaction rule. M3  Wine Making ferments non-grape juice, per the manuscript's explicit grape exclusion (L3830-31) → Chem_GrapeJuice renamed "Mixed Fruit Juice" (GUID refs untouched; string lookups updated). M4  Reagent fidel

### `Tools/PharmaSynth/Apply Verb Data`
<sub>`Assets/PharmaSynth/Scripts/Editor/VerbDataApplier.cs`</sub>

One-shot, idempotent data pass for the tool verbs: re-points the layouts whose tasks are TOOL verbs (weigh/stir/grind) and wires the scene-side pieces (Methane's hand-built mortar, the matches-box striker). • Acetone: `weigh-acetates` zone-touch → Weigh (scoopula on the pan). • Benzamide: `stand` ("Stir & stand") zone-touch → Stir (glass rod). • Scene: Methane's Eq_Motar/Eq_Pestle get a GrindController completing `prepare-mixture` (dual-path with the legacy zone-touch), and the matches dispenser

### `Tools/PharmaSynth/Audit Chemical Hazard Flags`
<sub>`Assets/PharmaSynth/Scripts/Editor/ChemHazardFlagAudit.cs`</sub>

Stamps the HazardousMix flags (isOxidizer / isConcentratedAcid) onto every ChemicalData asset from the pure HazardFlags name rules, and ensures the Chem_RuinedMixture SO (the dark sludge an overheated batch turns into) exists and is registered in the SceneAssetLibrary. Idempotent.

### `Tools/PharmaSynth/Audit Chemical States`
<sub>`Assets/PharmaSynth/Scripts/Editor/ChemicalStateAudit.cs`</sub>

Reagent-nature audit: checks whether each reagent is really a liquid or a solid that has to be scooped. Dumps every ChemicalData's state/flags to Temp/chemical-state-audit.md and flags chemicals that are solids in their common lab form but are marked Liquid (manuscript solutions like "10% NaOH" are correctly liquid — only pure solids that get weighed/scooped are suspects).

### `Tools/PharmaSynth/Audit Reachability`
<sub>`Assets/PharmaSynth/Scripts/Editor/ReachabilityAudit.cs`</sub>

Asks the one question the run simulator structurally cannot: a step can be mechanically perfect while the bottle it needs sits inside a closed cabinet or above head height. `SimulatedRun` reaches every object by reference, so it never notices; otherwise only a headset session would. Its input is already built and already verified: `TutorialTargets.Build()` resolves taskId → the objects each step is about, for all 9 modules. So the audit asks, of every object a step needs, "could a player standin

### `Tools/PharmaSynth/Audit Tutorial Targets`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Builds every module's stage IN EDIT MODE and reports which steps resolve to no scene object — i.e. where Tutorial Mode would tell the player to act with nothing to point at. NOT a self-test pin, deliberately: this has to Build() each stage, which mutates the open scene, and the suite must stay side-effect-free. Run it by hand after changing a module's tasks, layout, or verb wiring. Like Reveal Stage, it leaves the LAST module's stage standing — rebuild or reopen the scene afterwards.

### `Tools/PharmaSynth/Autopilot Playtest (TUTORIAL Mode — checks the guidance)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

⭐ Tutorial Mode needs its own pass. The campaign sweep enters via "Laboratory", so `TutorialSession.Active` stays false and every guidance cue (glow, ghost, beacon, spotlight, verb demo, wrong-grab nudge, need line, ping, ground path) correctly does nothing; the ground path reports "dist 0.0, no route" in all nine modules for exactly that reason.

### `Tools/PharmaSynth/Autopilot Playtest (VISUAL — honest verbs + vessel close-ups)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

⭐ VISUAL: the campaign loop again, but every step is performed HONESTLY through SimulatedRun's verbs (real pours and scoop dips, the water bath, the ice bucket, the glass rod, a real litmus strip...) and then PHOTOGRAPHED — a close-up of the vessel the step happened in, plus the numbers behind the picture, judged against the fired reaction's manuscript observation. The other two modes complete steps by calling CompleteTask, so nothing ever happens in a vessel and their screenshots show an empty 

### `Tools/PharmaSynth/Autopilot Playtest (plays the game in Play mode)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

Plays the game in PLAY MODE, unattended, and reports what broke. ⭐ Why this exists on top of Simulate Everything: that battery runs in EDIT mode, where Update never ticks, coroutines never run, physics never steps, XRI never selects anything and no audio plays. Every bug that only exists in MOTION is invisible to it: items vanishing, dialogue stomping mid-typing, the holo panel not scrolling, quiz buttons not clickable, an error spammed every frame. ⭐ No headset needed: PC Dev Mode leaves OpenXR

### `Tools/PharmaSynth/Brighten Lab Lighting`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabLightingBuilder.cs`</sub>

Brightens the lab so it reads as a well-lit laboratory. Quest-friendly recipe, with NO extra shadow-casting lights: 1. The 16 ceiling `Light (n)` fixture meshes get a white EMISSIVE panel material. 2. Ambient is applied from LabRenderTuner (a ceiling-to-floor gradient). 3. A small grid of shadowless point lights (`LabLights` group, re-runnable) fills the room — 6 lights, wide range, warm-white, no shadows (URP per-object cap safe). 4. The directional key light keeps shadows but drops a touch so 

### `Tools/PharmaSynth/Build Atmosphere VFX`
<sub>`Assets/PharmaSynth/Scripts/Editor/AtmosphereBuilder.cs`</sub>

Places the ambient atmosphere emitters: cool vapour sinking from the AC unit + a faint haze layer near the floor. Low-density on purpose (Quest overdraw). Door cold-air is code-hooked in DoorOpener, not here. Tools ▸ PharmaSynth ▸ Build Atmosphere VFX (SampleScene, edit mode, idempotent).

### `Tools/PharmaSynth/Build Cube Room FX`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Adds atmosphere to the cube spawn room: breathing neon trim, drifting light levels with the occasional arc stutter, floor haze and slow rising motes. Deliberately built from PARTICLES + emissive pulsing rather than post-processing: this is the first thing a Quest 3 player sees, a full-screen effect stack costs frame budget on a mobile GPU for the whole session, and the room only needs to look alive, not cinematic. Idempotent — re-running replaces its own objects.

### `Tools/PharmaSynth/Build Hand Visuals`
<sub>`Assets/PharmaSynth/Scripts/Editor/HandVisualsBuilder.cs`</sub>

Controllers are REPLACED by skinned hands: XR Hands sample meshes (Art/Hands/LeftHand.fbx + RightHand.fbx, real finger bones). Two skins: bare (HandSkin.mat) / nitrile blue (HandNitrile.mat) driven by the PPE gloves state; two poses: free / grab (finger curl while selecting) driven by HandPoseController. Removes any procedural mittens, HandVisualKeeper AND FPGlove_* first-person glove clones (PPE visuals bind to the mirror gloves only; first-person gloving is the material swap). Tools ▸ PharmaSy

### `Tools/PharmaSynth/Build Hover Info Panel`
<sub>`Assets/PharmaSynth/Scripts/Editor/HoverInfoBuilder.cs`</sub>

Builds the hover-inspector info card + wires the raycasting HoverInspector into SampleScene. Point the right-hand ray (or gaze) at a reagent, a piece of apparatus or an NPC and a smoothly-animated card names it and explains what it is / how to use it. Tools ▸ PharmaSynth ▸ Build Hover Info Panel (SampleScene, edit mode, idempotent).

### `Tools/PharmaSynth/Build Jimenez HUD Portrait`
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezHudPortraitBuilder.cs`</sub>

Gives Dr. Jimenez a HUD presence equal to Pharmee's — he appears in the HUD with his own icon image, just as she does. Two jobs, both idempotent: 1. RENDER his portrait from his own rigged model: a transparent-background headshot framed on the humanoid Head bone, so the icon IS the character the player meets. → Art/UI/jimenez_icon.png, imported with pharmee_icon.png's settings (Sprite/Single, alpha, no mips). 2. WIRE the HudDialogueBar for two speakers: the existing DialogueBar Portrait Image + 

### `Tools/PharmaSynth/Build Jimenez HUD Portrait (re-render icon)`
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezHudPortraitBuilder.cs`</sub>

Gives Dr. Jimenez a HUD presence equal to Pharmee's — he appears in the HUD with his own icon image, just as she does. Two jobs, both idempotent: 1. RENDER his portrait from his own rigged model: a transparent-background headshot framed on the humanoid Head bone, so the icon IS the character the player meets. → Art/UI/jimenez_icon.png, imported with pharmee_icon.png's settings (Sprite/Single, alpha, no mips). 2. WIRE the HudDialogueBar for two speakers: the existing DialogueBar Portrait Image + 

### `Tools/PharmaSynth/Build Lab Alarm`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabAlarmBuilder.cs`</sub>

Builds the lab's hazard-alarm fixture (manuscript: "flashing lights, warning messages, alarm beeps"): a small red ceiling box + one red point light + LabAlarm, centred over the lab. Idempotent.

### `Tools/PharmaSynth/Build Lab Music Speaker`
<sub>`Assets/PharmaSynth/Scripts/Editor/MusicSpeakerBuilder.cs`</sub>

Builds the corner music speaker in the lab: a floor-standing speaker cabinet in the empty back-right corner that plays the Background_Music/Lab playlist as a 3D positional source (louder as you approach) and fades in/out with the screen fade on menu<->lab transitions. Also disables the legacy 2D LabMusicPlayer bed and re-points the menu-room music to the supplied track. Tools ▸ PharmaSynth ▸ Build Lab Music Speaker (SampleScene, edit mode, idempotent).

### `Tools/PharmaSynth/Build Lab NavMesh (ground path routing)`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabNavMeshBuilder.cs`</sub>

Bakes the walkable surface Tutorial Mode's ground path routes on. Without this there is no NavMesh in the project at all, and `GuidePath` silently draws nothing, so the floor arrows look "not implemented" rather than "not baked". ⚠ **The bake goes STALE when furniture moves**, like the lightmap bake: the benches ARE the obstacles it routes around. Re-run this together with `Build Lab Probes` → `Run Lab Lighting Bake` after any `Select Movable Furniture` session, or the arrows route through a ben

### `Tools/PharmaSynth/Build Lab Probes`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabProbeBuilder.cs`</sub>

The Mobile RP asset has reflection-probe blending AND box projection switched on, and both need probes to feed them. Without probes: * Every glass material in the ChemLab pack sets _EnvironmentReflections = 1, so it samples the DEFAULT reflection - the built-in procedural outdoor sky - inside a sealed windowless room, and beakers and flasks read as pale plastic. * Every dynamic object (all grabbable glassware, held items, Pharmee, Dr. Jimenez) is lit by flat ambient alone, so a beaker is lit ide

### `Tools/PharmaSynth/Build Menu Cube Room`
<sub>`Assets/PharmaSynth/Scripts/Editor/MenuCubeRoomBuilder.cs`</sub>

Builds the futuristic CUBE SPAWN ROOM in the MainMenu scene: a fully-enclosed, solid, dark room with cyan/teal emissive trim, a couple of soft lights and a glowing floor launch-pad under the menu panel. Sealed on all six sides so no skybox leaks. Re-runnable and idempotent: deletes the prior "MenuCubeRoom" and rebuilds it, and hides the legacy open "MenuRoom" dressing. Tools ▸ PharmaSynth ▸ Build Menu Cube Room. Run with the MainMenu scene open.

### `Tools/PharmaSynth/Build PPE Wearables`
<sub>`Assets/PharmaSynth/Scripts/Editor/PPEWearablesBuilder.cs`</sub>

Wires the per-piece wearable PPE: 1. The locker's goggles + gloves become CLICKABLE (collider + XRSimpleInteractable + PPEDonOnSelect), the coat display forwards Coat, and the legacy don-everything paths are disabled (host donOnSelect off, coat display's old persistent calls cleared). 2. Worn visuals cloned from the locker models onto the mirror avatar's bones (coat→Spine01, goggles→Head, gloves→hand bones — PlayerAvatar layer, mirror-only) and first-person gloves onto the controllers (main-came

### `Tools/PharmaSynth/Build Player Avatar`
<sub>`Assets/PharmaSynth/Scripts/Editor/PlayerAvatarBuilder.cs`</sub>

Builds the mirror-only first-person avatar. Expects a rigged humanoid prefab (Tripo image→3D + Tripo Rigging v1, casual clothes, T-pose) in Art/Generated/Models with "player"/"avatar" in its name — or select it in the Project. Places it under the XR rig, puts it on the PlayerAvatar layer (culled by the main camera, shown by the mirror), and wires an Animation-Rigging IK setup: two-bone IK on each arm (hands→controllers) + a head rotation constraint (head→HMD), driven by PlayerAvatarRig. Raw-bone

### `Tools/PharmaSynth/Build Quiz Back-Next Buttons`
<sub>`Assets/PharmaSynth/Scripts/Editor/QuizNavButtonsBuilder.cs`</sub>

Add "< Back" / "Next >" buttons to the quiz tablet so players can review their answers before submitting. Clones the existing Submit button so styling matches, wires them to PostLabController.PreviousQuestion/NextQuestion, assigns the controller's prevButton/nextButton refs (which grey out at the ends), and makes sure the quiz canvas is XR-ray clickable. Idempotent.

### `Tools/PharmaSynth/Build Reagent Cabinets`
<sub>`Assets/PharmaSynth/Scripts/Editor/ReagentCabinetBuilder.cs`</sub>

Builds the raw-reagent storage (the manuscript's ~54 materials must exist in the lab): three open shelf units against the wall, stocked from RawReagentCatalog with nature-appropriate labware — reagent bottles, amber bottles for the light-sensitive, powder jars, dropper bottles, consumable boxes (litmus/matches/cotton/filter) and an ice bucket. Every bottle is grabbable, pourable, spill-graded and hover-explained. Chemicals already displayed on the legacy ReagentShelf are skipped. Re-runnable: th

### `Tools/PharmaSynth/Build Review Corner`
<sub>`Assets/PharmaSynth/Scripts/Editor/ReviewCornerBuilder.cs`</sub>

Wires the post-experiment review corner: a ReviewCornerSpawn marker in front of the PostLabTablet (biased toward Dr. Jimenez's spot) that the gatekeeper fade-teleports the player to for the quiz-review flow, plus the gatekeeper's postLab/examiner refs and the quiz's autoOpen=false (the gate opens the quiz after Jimenez's briefing). Idempotent.

### `Tools/PharmaSynth/Build Spawn VFX`
<sub>`Assets/PharmaSynth/Scripts/Editor/SpawnVfxBuilder.cs`</sub>

Builds the cyan "materialize" spawn burst: a one-shot column of cyan particles that rises from the player's feet like smoke, played on every teleport / reset / spawn. Creates a shared soft-dot texture + additive material, then drops a configured `SpawnVFX` object (SpawnBurstFX + ParticleSystem) into the ACTIVE scene. Re-runnable. Run it once in MainMenu and once in SampleScene. Tools ▸ PharmaSynth ▸ Build Spawn VFX.

### `Tools/PharmaSynth/Build Tutorial Menu Button`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Tutorial Mode scene wiring. Mirrors DemoModeBuilder's shape so the two special modes are built the same way: • Build Tutorial Menu Button — MainMenu scene: clones the Laboratory button into a "Tutorial" button wired to MainMenuController.OnTutorialLaboratory. Unlike the Demo button this one is ALWAYS visible — practice mode is a shipped feature, not a config-gated demo affordance. Idempotent: re-running re-labels and re-wires the existing button in place.

### `Tools/PharmaSynth/Build Tutorial Scene Wiring`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Wires Tutorial Mode into the lab scene: • TutorialHighlighter on the runner's object, bound to the runner • WaypointGuide bound to the runner (the zone-free bench has no station registry for it to read) • the beacon's arrow + disc switched to a ZTest Always material so the marker reads THROUGH a closed cabinet door — the "see it through things" requirement, without touching any shared bench material. Idempotent.

### `Tools/PharmaSynth/Build Workspace Kits`
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceKitsBuilder.cs`</sub>

apparatus that belong together, tightly but not overlapping, with generous duplicates of high-use glass). Kit composition follows the manuscript's Appendix C Equipment lists + the game's Methane heating rig: TOP row   — Heating Set A (full Bunsen rig), Heating Set B (compact Bunsen rig), Alcohol-Burner Set (spirit lamp + clay triangle + crucible: the crucible-work set — the manuscript names no burner, so Bunsen = sustained heat, alcohol lamp = crucible). LOWER row — 4 test-tube rack kits (regula

### `Tools/PharmaSynth/Build Workspace Shelf`
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceShelfBuilder.cs`</sub>

Builds the equipment-shelf platforms on the center-table overhead gantry. One row sits on the rail tops; this also adds the SECOND, lower row at y≈1.20 (clean full-width tiles + slim side posts, so the lower row reads as built in, not floating). Idempotent + re-runnable. Geometry lives in the pure WorkspaceShelfMath; the apparatus kits go on via Build Workspace Kits.

### `Tools/PharmaSynth/Consolidate Procedure Panels`
<sub>`Assets/PharmaSynth/Scripts/Editor/PanelConsolidationBuilder.cs`</sub>

One procedures display. The entrance LabTablet duplicates the wrist holo board and the wrist mini-panel duplicates the holo header: three surfaces for the same content, with the tablet's fixed rect overflowing into its reaction footer. This menu retires the LabTablet (deactivated, not deleted) and the MiniPanel, and upgrades the holo board to the single panel: status header (ex mini-panel) + focused checklist + the balanced-reaction footer (ex tablet). Idempotent.

### `Tools/PharmaSynth/Damp Jimenez Arm Swing`
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezArmDamper.cs`</sub>

⛔ WHY THIS EXISTS INSTEAD OF A RE-WEIGHT. Moving the coat's vertex weights off the arm bones does not work on this asset: the coat and the sleeve are one continuous surface with no seam to cut along, so any weight change big enough to free the coat is big enough to rip it (measured against the untouched mesh, worst edge stretch 4.2x → 14.9x and torn edges 32 → 174). Smoothing the transfer across a band removes the tearing but then barely moves the coat (1625 → 1557 dragged vertices). The geometr

### `Tools/PharmaSynth/Demo/Build Demo HUD`
<sub>`Assets/PharmaSynth/Scripts/Editor/DemoModeBuilder.cs`</sub>

Demo Mode scene wiring. Three menus: • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory. • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer Quiz row under the HUD's top-right cluster, driven by DemoHudController. • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json for in-editor testing (the shipped StreamingAssets default stays false).

### `Tools/PharmaSynth/Demo/Build Demo Menu Button`
<sub>`Assets/PharmaSynth/Scripts/Editor/DemoModeBuilder.cs`</sub>

Demo Mode scene wiring. Three menus: • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory. • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer Quiz row under the HUD's top-right cluster, driven by DemoHudController. • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json for in-editor testing (the shipped StreamingAssets default stays false).

### `Tools/PharmaSynth/Demo/Demo Enabled (persistent override)`
<sub>`Assets/PharmaSynth/Scripts/Editor/DemoModeBuilder.cs`</sub>

Demo Mode scene wiring. Three menus: • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory. • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer Quiz row under the HUD's top-right cluster, driven by DemoHudController. • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json for in-editor testing (the shipped StreamingAssets default stays false).

### `Tools/PharmaSynth/Demo/Demo Enabled (persistent override)`
<sub>`Assets/PharmaSynth/Scripts/Editor/DemoModeBuilder.cs`</sub>

Demo Mode scene wiring. Three menus: • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory. • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer Quiz row under the HUD's top-right cluster, driven by DemoHudController. • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json for in-editor testing (the shipped StreamingAssets default stays false).

### `Tools/PharmaSynth/Dev Capture`
<sub>`Assets/PharmaSynth/Scripts/Editor/DevCapture.cs`</sub>

Dev-only capture bridge: renders a one-off camera to a PNG on disk so out-of-editor tooling can see the scene (the MCP scene-preview capture is broken on this machine). Pose/output come from Temp/dev-capture-request.json when present; defaults to the player spawn head pose.

### `Tools/PharmaSynth/Fit Glassware (upright · pivot · seat)`
<sub>`Assets/PharmaSynth/Scripts/Editor/UprightPourables.cs`</sub>

because adoption bakes whatever pose it finds, tipped or not. ⛔ An item whose visible mesh is not on its own PIVOT cannot be placed by anyone and is held nowhere near the player's hand. Tripo/USD imports can carry their geometry 0.7–1.9 m from their origin (both distilling flasks, the ice bucket and the matchstick). Dragging the object in the Scene view moves the pivot; the mesh keeps its offset and hangs in the air wherever it is put. The runtime also reads `transform.position` (`Matchstick` me

### `Tools/PharmaSynth/Fit Solid Jars (powder mounds)`
<sub>`Assets/PharmaSynth/Scripts/Editor/SolidJarFitter.cs`</sub>

Gives every solid reagent jar on the shelf its powder mound. `Build Reagent Cabinets` stocks a PowderJar row from the `Beaker_100mL_WithLiquid` prefab, which carries a `Liquid` surface and nothing else, so without a mound a SOLID is drawn as a column of liquid (`LiquidPhysics.DrawsLiquidColumn` has no mound to show instead). The scoopula is unaffected either way: `ScoopMath.CanPickUp` keys on the chemical's `state`. The mound itself is `ExperimentSceneBuilder.EnsurePowderVisual`, the same helper

### `Tools/PharmaSynth/Fix Cube Room Menu Layout`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Tutorial Mode scene wiring. Mirrors DemoModeBuilder's shape so the two special modes are built the same way: • Build Tutorial Menu Button — MainMenu scene: clones the Laboratory button into a "Tutorial" button wired to MainMenuController.OnTutorialLaboratory. Unlike the Demo button this one is ALWAYS visible — practice mode is a shipped feature, not a config-gated demo affordance. Idempotent: re-running re-labels and re-wires the existing button in place.

### `Tools/PharmaSynth/Fix Holo Board Scroll`
<sub>`Assets/PharmaSynth/Scripts/Editor/PanelConsolidationBuilder.cs`</sub>

Every holo text wraps (an instruction step would otherwise run as one continuous row), and the checklist body moves inside a masked, scrollable viewport with big ^ / v page buttons (poke/ray-friendly) driven by HoloScroller. Idempotent.

### `Tools/PharmaSynth/Fix Jimenez Coat Rig`
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezCoatRig.cs`</sub>

with the arm. ⛔ There is nothing to unparent. He is a Tripo auto-rig: ONE SkinnedMeshRenderer over 41 bones, with the coat baked into the body mesh. The coat follows the arm because coat VERTICES carry weight on the arm bones — an auto-rigger assigns weight by proximity, so a coat panel hanging near the elbow picks up elbow weight even though a real coat hangs from the shoulders. This finds that bleed geometrically (a vertex carrying arm weight while sitting well outside the arm) and hands the w

### `Tools/PharmaSynth/Fix Lab Door (swing + blocker)`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabDoorFixBuilder.cs`</sub>

1. The leaf inherits the Environment prefab's `m_StaticEditorFlags = 87`, which includes **Batching Static**. A static-batched renderer is merged into a combined mesh at load and then IGNORES its transform, so the leaf's collider swings open while the drawn door stays shut, and the player walks through what looks like a closed door. Contribute GI goes with it: a door that moves must not carry a lightmap baked in the closed position. 2. A null `doorBlocker` leaves nothing to hold the player back 

### `Tools/PharmaSynth/Generate Lab Surface Textures`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabSurfaceTextureForge.cs`</sub>

Generates the lab's MISSING surface textures. The Laboratory pack leaves the two largest surfaces in the player's view with no albedo at all - Wall_0/Wall_1 carry a normal map and nothing else, and Ceiling_2 carries no maps whatsoever. A featureless white plane makes the room read as an untextured grey box, and it is worst on the ceiling, which a seated VR player looks straight up into. Everything here is drawn in code and written to PNG, as LabelForge does for reagent labels: deterministic, re-

### `Tools/PharmaSynth/Generate Materials Guides`
<sub>`Assets/PharmaSynth/Scripts/Editor/MaterialsGuideGenerator.cs`</sub>

Fills every module's MATERIALS guide (the watch-panel header): every reagent and apparatus the module needs, listed first as a guide so the player can assemble them before starting. REAGENTS are DERIVED from the module's layout bindings — the ground truth of what the experiment actually consumes — with totals summed per chemical, so the guide can never drift from the tasks. Units follow the game's own convention (1 squeeze = 1 ml): liquids read "N ml", solids/powders "N g". APPARATUS is AUTHORED

### `Tools/PharmaSynth/Generate Raw Reagent Data`
<sub>`Assets/PharmaSynth/Scripts/Editor/RawReagentForge.cs`</sub>

Generates the ChemicalData assets for every RawReagentCatalog row that the game doesn't already know (matched by normalised chemicalName), stamps the HazardousMix flags from the shared HazardFlags rules, and registers everything in the SceneAssetLibrary so layouts and the cabinet builder resolve them. Consumable rows (SmallBox/IceBucket) are physical props, not chemicals — no SO is made for them. Idempotent.

### `Tools/PharmaSynth/Generate Reagent Labels`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabelForge.cs`</sub>

Reagent-label compositor (style: MODERN). For every Reagent_* bottle on the shelf: renders LabelBase_Modern + the chemical's name (crisp TMP text) to a PNG, builds a material, and mounts a label quad on the bottle facing the aisle. Tools ▸ PharmaSynth ▸ Generate Reagent Labels — idempotent, re-run anytime.

### `Tools/PharmaSynth/Hide Station Pads`
<sub>`Assets/PharmaSynth/Scripts/Editor/HideStationPads.cs`</sub>

The hand-built Methane station pads render as coloured cubes on the table. The DynamicStage builder hides its own pads (padMr.enabled = false) but not the authored Station_* objects. The pads are purely cosmetic: the trigger COLLIDER + sensors that detect each step stay, and the guides/labels tell the player where to act — so the cube mesh just clutters the view. This disables the MeshRenderer on every Station_* pad while leaving all functionality intact. Idempotent.

### `Tools/PharmaSynth/Inject ILO Beats`
<sub>`Assets/PharmaSynth/Scripts/Editor/IloBeatInjector.cs`</sub>

Injects each experiment's ILO beats into its Intro cutscene (Pharmee states the learning outcomes in the opening dialogue). Beats slot in after the greeting beat: a lead-in, then one beat per objective (verbatim Appendix C copy from IloCopy). Idempotent — the lead-in text is the marker.

### `Tools/PharmaSynth/Lock My Layout`
<sub>`Assets/PharmaSynth/Scripts/Editor/LockMyLayout.cs`</sub>

One-click self-service layout lock: after moving or duplicating assets, one button does everything needed to make a hand-arranged workspace permanent — with NO rebuilding, so it can never reset placements: 1. Tidy duplicate names  — "Beaker (1)" → "Beaker_100mL_2", + full interaction wiring for any raw duplicate that was missing it. 2. Re-home every item     — current transform becomes its respawn home (moved originals AND duplicates), so nothing snaps back in Play. 3. Save the scene. Run this a

### `Tools/PharmaSynth/Merge Center Tables`
<sub>`Assets/PharmaSynth/Scripts/Editor/CenterTableBuilder.cs`</sub>

Merge Center Tables: removes the second table and makes the remaining one a single wide table at the centre of the room, in landscape. The experiment layouts bake WORLD positions on the left island, so this is a one-time atomic migration: 1. discover both islands geometrically (raycast under the baked positions and at their x-mirror; climb to the Environment child), 2. deactivate the right island (+ its sink follows to the new short end), 3. rigid-remap the left island 90° to the lab centre (lan

### `Tools/PharmaSynth/Name Tubes + Build Rack Slots`
<sub>`Assets/PharmaSynth/Scripts/Editor/TubeRackSlotBuilder.cs`</sub>

⛔ STORAGE RACKS GET NO SLOTS. The tubes were duplicated TOGETHER WITH their kit holders, so every copy is already seated correctly relative to its rack. Moving them onto bounds-GUESSED slots would destroy that placement. Never re-derive rack positions the scene already has right. Only the workspace holders need anchors, because they are the only racks that start EMPTY (the player drags tubes in mid-experiment), so there is no seated tube to copy a position from. Workflow: 1. Tools ▸ PharmaSynth 

### `Tools/PharmaSynth/Physics Audit (Drop Test)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PhysicsAudit.cs`</sub>

Physics-attributes / resting-pose audit. Tools ▸ PharmaSynth ▸ Physics Audit (Report)   — non-destructive scan of the scene apparatus + SceneAssetLibrary prefabs: colliders present/degenerate, Rigidbody settings, profile coverage. Writes Temp/physics-audit.md. Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test) — drops every library prefab onto a plane 50 m above the lab for 3 simulated seconds (script-mode simulation, all other dynamic rigidbodies frozen for the sweep) and checks it neither tunnels

### `Tools/PharmaSynth/Physics Audit (Fix Scene Items)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PhysicsAudit.cs`</sub>

Physics-attributes / resting-pose audit. Tools ▸ PharmaSynth ▸ Physics Audit (Report)   — non-destructive scan of the scene apparatus + SceneAssetLibrary prefabs: colliders present/degenerate, Rigidbody settings, profile coverage. Writes Temp/physics-audit.md. Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test) — drops every library prefab onto a plane 50 m above the lab for 3 simulated seconds (script-mode simulation, all other dynamic rigidbodies frozen for the sweep) and checks it neither tunnels

### `Tools/PharmaSynth/Physics Audit (Report)`
<sub>`Assets/PharmaSynth/Scripts/Editor/PhysicsAudit.cs`</sub>

Physics-attributes / resting-pose audit. Tools ▸ PharmaSynth ▸ Physics Audit (Report)   — non-destructive scan of the scene apparatus + SceneAssetLibrary prefabs: colliders present/degenerate, Rigidbody settings, profile coverage. Writes Temp/physics-audit.md. Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test) — drops every library prefab onto a plane 50 m above the lab for 3 simulated seconds (script-mode simulation, all other dynamic rigidbodies frozen for the sweep) and checks it neither tunnels

### `Tools/PharmaSynth/Prepare Lab Lighting Bake`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabLightingBake.cs`</sub>

floats. Baked GI is also FREE at runtime, which is why it is the right tool on a Quest. The three steps that have to happen in THIS order, because each depends on the last: 1. Lightmap UVs. A mesh with no UV2 cannot receive a lightmap. 36 of the project's 107 models lack them, including the entire room shell (Wall, Floor, Ceiling_2, the tables). Anything still missing UV2 after the import pass is set to receive from LIGHT PROBES instead, so it still occludes and bounces without a broken lightmap

### `Tools/PharmaSynth/Purge Stale Workspace Labels`
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceLabelPurge.cs`</sub>

Removes the stale Methane-tutorial text labels that float over the center workspace. They sit directly under WorldLabels (NOT under MethaneStage), so toggling the Methane stage never hides them, and they are orphaned at the x≈1.15 position. Pure scene leftovers: no script references them. Landmark labels (PPE locker, fume hood) and runtime DynLabel_* are kept. Idempotent + re-runnable.

### `Tools/PharmaSynth/Re-Home MOVED Items Only`
<sub>`Assets/PharmaSynth/Scripts/Editor/ReHomeSceneItems.cs`</sub>

Adopt the current transform ONLY where it actually differs from the baked home. An item a simulator has just restored to its home is by definition NOT different from it, so this can never re-bake a revert — it can only pick up a real move. It also names every item it claims, so a run that grabs more than expected is visible instead of silent.

### `Tools/PharmaSynth/Re-Home Scene Items (Adopt Current)`
<sub>`Assets/PharmaSynth/Scripts/Editor/ReHomeSceneItems.cs`</sub>

Adopts every scene item's CURRENT transform as its DropRespawn home, so equipment relocated by hand keeps its new spot as its default spawn point. Without this, manually moved props teleport back to their previous serialized homes after ~25 s idle / a kill-Z fall / a reset. Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current) — run in SampleScene edit mode after ANY manual re-arrangement, then save the scene. ⛔ NEVER run the ALL-items version straight after a simulator. Every simulator call

### `Tools/PharmaSynth/Rebuild Compact HUD`
<sub>`Assets/PharmaSynth/Scripts/Editor/CompactHudBuilder.cs`</sub>

Compact VR HUD layout: the timer + title move OUT of the centre and merge into the Progress pill on the LEFT (small, stacked); the top cluster tucks to the top edge; the three Settings/Restart/Quit buttons collapse behind ONE hamburger icon that opens a vertical dropdown; and Pharmee's bottom dialogue bar is raised so it's fully in view. Idempotent — re-run after tuning the constants below. Operates on the open scene's HudRig (SampleScene only).

### `Tools/PharmaSynth/Remove VR-Inappropriate Apparatus`
<sub>`Assets/PharmaSynth/Scripts/Editor/RemoveVrInappropriateApparatus.cs`</sub>

Deletes the SCENE INSTANCES of apparatus that the manuscript lists but that carry no meaningful VR interaction: pure bench scaffolding and passive instruments the game already abstracts. The prefabs stay in their folders. ⛔ DELIBERATE, DOCUMENTED EXCEPTION to the "ALL tools always present" client rule. These six are NOT a decluttering of usable tools — each is either a support rig the zone-free heat model made unnecessary or an instrument whose reading the game shows on-screen. The PREFAB ASSETS

### `Tools/PharmaSynth/Reveal Methane + Waypoint (for editing)`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealMethaneAndWaypoint.cs`</sub>

Reveal the Methane set + the waypoint marker in the editor so they can be hand-aligned, and permanently strip the waypoint's yellow ground glow (keep only the arrow). The Methane STATIONS carry the step-detection zones AND are what the waypoint arrow follows, so moving them with the props is how both detection and the arrow get aimed correctly.

### `Tools/PharmaSynth/Reveal Methane Stage (for review)`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealMethaneStage.cs`</sub>

The Methane tutorial stage is authored inactive (m_IsActive:0) so it stays hidden in the editor. To review / delete it by hand, so this switches it (and any other hidden methane roots) ON in edit mode and lists what became visible. Runtime is unaffected — ExperimentSceneBuilder still SetActive(moduleId==methane) each build. ⚠ Deleting these breaks Experiment 1 until Methane is rewired to build on the workspace (the splint-pop rig especially is wired to the stage's tube).

### `Tools/PharmaSynth/Reveal Stage/Final 1 — Benzamide`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Final 2 — Wine Making`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Midterm 1 — Benzoic Acid`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Midterm 2 — Acetanilide`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Midterm 3 — Acetone`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Midterm 4 — Chloroform`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Prelim 1 — Chemical Compounding`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Reveal Stage/Prelim 2 — Ethyl Alcohol`
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate t

### `Tools/PharmaSynth/Round UI Corners`
<sub>`Assets/PharmaSynth/Scripts/Editor/RoundedCornersBuilder.cs`</sub>

Tools ▸ PharmaSynth ▸ Round UI Corners: writes the shared rounded and ring sprites (once) and bakes it into every panel and button of SampleScene and MainMenu, then saves both. Idempotent. Run it on a scene freshly loaded from disk, never straight after a suite or simulator run (those drain the bench, and this saves).

### `Tools/PharmaSynth/Run Lab Lighting Bake`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabLightingBake.cs`</sub>

floats. Baked GI is also FREE at runtime, which is why it is the right tool on a Quest. The three steps that have to happen in THIS order, because each depends on the last: 1. Lightmap UVs. A mesh with no UV2 cannot receive a lightmap. 36 of the project's 107 models lack them, including the entire room shell (Wall, Floor, Ceiling_2, the tables). Anything still missing UV2 after the import pass is set to receive from LIGHT PROBES instead, so it still occludes and bounces without a broken lightmap

### `Tools/PharmaSynth/Run Self-Tests`
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs`</sub>

Re-runnable regression suite for the PharmaSynth engine. Run via menu: Tools ▸ PharmaSynth ▸ Run Self-Tests. One permanent, one-click check. (An Editor-menu suite rather than an NUnit asmdef, to avoid restructuring the runtime assembly; a formal EditMode asmdef migration can layer on later.) This file holds the runner and shared helpers; each area's suites live in PharmaSelfTests.<Area>.cs. A new suite needs a call in Run().

### `Tools/PharmaSynth/Seal Entrance Gaps`
<sub>`Assets/PharmaSynth/Scripts/Editor/EntranceSealBuilder.cs`</sub>

Seals the skybox seams at BOTH entrances (front corridor door + interior lab door). Each doorway's frame doesn't quite meet the surrounding wall, so the skybox shows through thin cracks at the jambs/lintel. This wraps each doorway with opaque trim strips (top lintel + two jambs) centred on the wall plane and standing slightly proud on both faces, so the seam is covered whether viewed from the corridor or the lab. Reuses GapSealWall's dark material for a consistent look; re-runnable. Tools ▸ Phar

### `Tools/PharmaSynth/Select Movable Furniture`
<sub>`Assets/PharmaSynth/Scripts/Editor/FixtureTools.cs`</sub>

Edit-mode helper so the sinks, speaker, tables and shelves can be repositioned by hand in the editor. Two things block Scene-view clicks: 1. The Environment furniture (tables, wall cabinets, wash-table sinks) has PICKING DISABLED (the pointer toggle in the Hierarchy), as do the stools. 2. Some fixtures (the LabSpeaker, and the shelf ROOTS that sit at the origin with their meshes parented elsewhere) have no click target, so a click selects a child prop instead of the movable unit. This re-enables

### `Tools/PharmaSynth/Select Stools`
<sub>`Assets/PharmaSynth/Scripts/Editor/StoolTools.cs`</sub>

Edit-mode helper ("let me select + reposition the stools in the editor, not in Play mode"). The stools sit tucked under the tables, so clicking them in a crowded Scene view is fiddly. This selects all of them in one go — then move them with the transform gizmo and run Re-Home Scene Items to make it stick. Tools ▸ PharmaSynth ▸ Select Stools (edit mode).

### `Tools/PharmaSynth/Simulate Campaign (full loop Exp 2-9)`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedCampaign.cs`</sub>

• the module picked from its period through the two-step picker, with the real ProgressionFlow.IsUnlocked gating each pick • the honest pour-through of the experiment (SimulatedRun) • the REAL PostLabController quiz — Open, answer, SubmitAndFinish → Finish • the REAL ExperimentGrader result + the floored grade-screen text • the REAL cutscene outro selection + its subtitle beats • the REAL ProgressionService record + ProgressionFlow unlock + UnlockDiff announcement, then the pick of the NEXT expe

### `Tools/PharmaSynth/Simulate Everything (full playability check)`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulateEverything.cs`</sub>

ONE command that plays the whole game and answers one question: is every experiment actually doable right now? It adds no simulation of its own. `SimulatedRun.Run` and `SimulatedCampaign.Run` already return structured results; this is the single entry point and the single verdict over them and the two tutorial audits. Everything lands in Logs/simulate-everything.txt, worst module FIRST: a report you have to scroll to find the failure in is a report that gets skimmed.

### `Tools/PharmaSynth/Simulate Imperfect Play`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedMisplay.cs`</sub>

Simulates the player who gets it WRONG. Every other simulator plays a flawless run, which answers "does correct play work?". It does not answer the question that actually decides whether nine experiments are doable by a student: **after a mistake, can they still finish?** A contaminated vessel, an out-of-order attempt or an exhausted bottle that quietly makes a run unfinishable looks identical to a clean sim — the perfect path never touches it. Each probe asserts two things, and the second is th

### `Tools/PharmaSynth/Simulate Pharmee Gestures`
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmeeGestureSim.cs`</sub>

Proves Pharmee's animation set actually MOVES HIM, in edit mode. The suite pins the pure curves (`PharmeeGestureSuite`), but a correct curve reaching a transform that is not bound produces exactly nothing while every assertion stays green. That is the failure this menu exists to catch, and it is the same reason `Simulate Tutorial Guidance` exists rather than another pin: it has to drive real scene objects, and the suite is kept side-effect-free. For each gesture it applies the pose at its peak a

### `Tools/PharmaSynth/Simulate Run/Final 1 — Benzamide`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Final 2 — Wine Making`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Midterm 1 — Benzoic Acid`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Midterm 2 — Acetanilide`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Midterm 3 — Acetone`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Midterm 4 — Chloroform`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Prelim 1 — Chemical Compounding`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Prelim 2 — Ethyl Alcohol`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Run/Tutorial — Methane`
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT i

### `Tools/PharmaSynth/Simulate Tutorial Guidance`
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Walks every module's REAL task graph step by step with Tutorial Mode on, and checks the guidance actually keeps up: at every point along the progression the currently-available step must still resolve to a live object, and the clock must never tick. This is the dynamic counterpart to Audit Tutorial Targets. The audit asks "does every task have a target?" once, in aggregate; this asks "as steps complete, does the target set MOVE with them?" — a stale or empty set mid-run would leave the player st

### `Tools/PharmaSynth/Stand Tipped Glassware Up`
<sub>`Assets/PharmaSynth/Scripts/Editor/UprightPourables.cs`</sub>

Legacy menu path, kept because the docs still use it.

### `Tools/PharmaSynth/Stock End-Product Shelf`
<sub>`Assets/PharmaSynth/Scripts/Editor/EndProductShelfStocker.cs`</sub>

Acetanilide · Benzamide · Chloroform · Wine are named as a reagent by NO manuscript procedure — their own chemical tests consume the product the player just made (Exp 5's "place 1 gram of acetanilide in a test tube" is testing YOUR synthesis). They are never inputs to anything, so they are simply absent rather than gated. Ethanol · Acetone · Benzoic Acid are the exception: each is some module's goal AND a manuscript-listed reagent for others (Ethanol → Exp 2, 6; Acetone → Exp 2, 7; Benzoic Acid 

### `Tools/PharmaSynth/Style Grade Card (slide layout)`
<sub>`Assets/PharmaSynth/Scripts/Editor/GradeCardStyler.cs`</sub>

Restyles the grade screen into the client slides' "Mission Complete" card: a row of three white tiles (Time · Mistakes · Grade) with large blue figures, the verdict under them, the rubric breakdown below in a smaller type, buttons unchanged. Idempotent: tiles are found-or-created and every rect is SET, never nudged. The controller's text references are untouched (it still fills the same objects).

### `Tools/PharmaSynth/Swap In Open Fume Hood`
<sub>`Assets/PharmaSynth/Scripts/Editor/FumeHoodSwap.cs`</sub>

Swaps the sealed Tripo fume-hood model (a sash, but not hollow inside) for the OPEN-SASH, hollow-chamber one. Idempotent: • deactivates the old FumeHoodModel (kept in the scene for hand-deletion rather than deleted by the tool), • mounts Art/Generated/Refs/FumeHoodOpen.prefab under FumeHood_StandIn, height-normalised to the house 2.35 m, • re-fits the WorkVolume trigger into the new chamber (upper-front region — hand-tune afterwards, the wire box is visible when selected), • rebuilds the HoodShe

### `Tools/PharmaSynth/Tidy Experiment Layouts`
<sub>`Assets/PharmaSynth/Scripts/Editor/LayoutTidy.cs`</sub>

Re-seats every experiment layout onto the LayoutTidyMath zoning grid (a clean center table — stations across the back, vessels center-front, reagents right, tools left; the front strip stays free for the rack and spares). Deterministic + idempotent; also structurally removes the two clamped overlaps at (1.38, −3.88) in Acetone and Benzamide. Run AFTER `Apply Verb Data` so new stations/props get slots too.

### `Tools/PharmaSynth/Tune Lab Surfaces`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabSurfaceTuner.cs`</sub>

a sealed indoor lab reads as wet plastic. * The benchtops sit at 1.00. A lab bench is matte epoxy resin. * The four vessel materials the player handles constantly - beaker 100/500, Erlenmeyer, graduated cylinder - sit at smoothness 0, and glass with NO specular and NO reflection reads as pale plastic blobs. The same pack ships CORRECT glass values on GlassMat/GlassInnerMat/GlassOuterMat (0.92-0.95). * Several non-metals sit at metallic 0 + smoothness 1 - a physically impossible "non-metal mirror

### `Tools/PharmaSynth/Tune Render Pipeline`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabRenderTuner.cs`</sub>

* MSAA 1 (off). On the Adreno tile GPU 4x resolves in tile memory and is close to free, and this scene is nothing but thin edges - tube rims, glass rods, rack rails. It is the largest per-pixel quality gain available. * HDR off, so emission above 1.0 just clamps: the 16 emissive ceiling panels can never read as LIGHTS, only as white paint, and tonemapping has no range to work with. * FLAT grey 0.45 ambient, identical from every direction, so nothing in the room has any vertical shading and the w

### `Tools/PharmaSynth/Voice/Export Voice Manifest`
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceManifestExporter.cs`</sub>

Exports the full voice-over manifest (NPCs speak): every code-authored line (VoiceCorpus) plus every cutscene beat, one row per unique (speaker, text) with its stable id. Tools/voice/generate-voice.ps1 consumes the manifest; changed lines re-key and regenerate individually.

### `Tools/PharmaSynth/Voice/Fix Voice Audibility + Music Ducking`
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceAudioBuilder.cs`</sub>

Makes the generated voice-over AUDIBLE and well-behaved. Three separate concerns, one pass: 1. Dr. Jimenez's narration channel needs a narratorAudioSource. SayRoutine only plays a clip when one exists, so without it every one of his 37 lines falls through to the placeholder blips. 2. Fully 3D voices fade to nothing a couple of metres away. The client wants them heard across the room but LOUDER up close, which is a partial spatial blend: the 2D share guarantees a floor everywhere, the 3D share st

### `Tools/PharmaSynth/Voice/Import & Wire Voice Clips`
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceImportTool.cs`</sub>

Imports the generated voice clips and wires the bank into the scene: 1. Quest-friendly import settings on Audio/Voice/** (mono, Vorbis). 2. Rebuilds VoiceBank.asset from Audio/Voice/<Speaker>/<id>.mp3|wav. 3. Points every NPCNarrationController in the open scene at the bank — controllers under Dr. Jimenez speak as Jimenez, everything else as Pharmee. Missing clips keep today's blip+typewriter. Idempotent.

### `Tools/PharmaSynth/Voice/Polish Cutscene Copy for Recording`
<sub>`Assets/PharmaSynth/Scripts/Editor/VoicePolish.cs`</sub>

Pre-recording copy pass over the CUTSCENE beats. Every beat is a SUBTITLE **and** a text-to-speech script, so a line can be perfectly good on screen and wrong in the ear, or describe apparatus that is not in the scene. Run this before spending voice credits: changing a line changes its VoiceLineId, so the manifest must be re-exported afterwards anyway. Edits the assets through SerializedObject rather than the YAML, because these subtitles contain ": " (e.g. "Welcome back! Today: Acetone.") which

### `Tools/PharmaSynth/Voice/Sync Gate Dialogue to Code`
<sub>`Assets/PharmaSynth/Scripts/Editor/VoicePolish.cs`</sub>

The gate's dialogue is a [SerializeField], so the SCENE keeps its own copy of every line and that copy is what the player actually hears. Any edit to the code defaults silently fails to reach the game, and because the voice manifest is built from the CODE, the spoken text stops matching any clip and the line drops back to placeholder blips. A drifted scene copy can also give wrong instructions (asking for a lab coat alone, while the door requires coat + goggles + gloves). Code is the source of t

### `Tools/PharmaSynth/Wire Button Sounds`
<sub>`Assets/PharmaSynth/Scripts/Editor/ButtonSoundsBuilder.cs`</sub>

Adds UiButtonSounds (hover blip + click) to every UI Button in the OPEN scene. Run it once per scene — MainMenu (cube room) and SampleScene (HUD / choice panels / settings / grade / post-lab). Idempotent. Tools ▸ PharmaSynth ▸ Wire Button Sounds (edit mode).

### `Tools/PharmaSynth/Wire End-Product Gate`
<sub>`Assets/PharmaSynth/Scripts/Editor/EndProductGateBuilder.cs`</sub>

A ready-made GOAL destroys the experiment — attaches EndProductVisibility to both storage roots and binds the runner, so each product bottle SetActive(false)s WHILE ITS OWN MODULE RUNS. The gate is per-EXPERIMENT, not per-chemical: Ethanol, Acetone and Benzoic Acid are each some module's goal AND a manuscript-listed reagent for others, so a global hide would strip Exp 2 (which runs before Exp 3/6) of reagents it needs. The four PURE products are not on the shelf at all, so there is nothing to ga

### `Tools/PharmaSynth/Wire Grab Collision (VelocityTracking)`
<sub>`Assets/PharmaSynth/Scripts/Editor/GrabMovementTools.cs`</sub>

Applies the GrabTuning velocity-tracked profile to every grabbable so held items collide with the world (without it, props can be pushed through walls/floor). Covers the SceneAssetLibrary prefabs (persisted) AND every XRGrabInteractable in the open scene(s) (catches instance overrides, stools, shelf bottles). Idempotent — re-running reports 0 changes.

### `Tools/PharmaSynth/Wire Grade Back Button`
<sub>`Assets/PharmaSynth/Scripts/Editor/GradeBackButtonBuilder.cs`</sub>

Wires a "Complete Experiment" button onto the grade screen (shown only on FAIL, where Continue is hidden; they share the slot) that ends the attempt and returns the player to the entrance, where any unlocked experiment can be picked, so a failed attempt is never a Retry-only loop. Idempotent.

### `Tools/PharmaSynth/Wire Methane Stage Visibility`
<sub>`Assets/PharmaSynth/Scripts/Editor/MethaneStageVisibilityBuilder.cs`</sub>

Wires the MethaneStageVisibility controller (methane set present only in Lab Tour + the Methane attempt). Puts the component on ExperimentSystems (a manager that keeps running while the stage is hidden), binds the MethaneStage + runner + LabTourGuide, and re-hides the stage in the authored scene so it doesn't flash on lab entry. Idempotent.

### `Tools/PharmaSynth/Wire NPC Polish`
<sub>`Assets/PharmaSynth/Scripts/Editor/LabNpcPolishBuilder.cs`</sub>

Wires the NPC/audio polish into SampleScene: 1. Pharmee expressions: PharmeeFace pointed at the robot's EYES + MOUTH meshes, default-happy; PharmeeMood resets the face after every line; the gatekeeper's faceBehaviour drives gate moods. 2. Dr. Jimenez proctor roaming — ProctorRoamer + observation points at the reagent shelf, equipment shelf, dynamic stage and fume hood. 3. AC proximity hum — ProximityHum on the air-con / vent assets (falls back to the fume hood if no AC mesh exists). Tools ▸ Phar

### `Tools/PharmaSynth/Wire Scoop Sounds`
<sub>`Assets/PharmaSynth/Scripts/Editor/ScoopSoundWiring.cs`</sub>

Wire the generated solid-material SFX into the SoundBank so scooping powder doesn't sound like liquid. The scoop verb calls the "scoop" and "powder-pour" keys via AudioService.TryPlayFirstAt, which deliberately has NO liquid fallback, so these clips are what make it audible. Clips generated with elevenlabs-sound-effects-v2 into Audio/Generated/. Idempotent: updates the entries in place if they already exist.

### `Tools/PharmaSynth/Wire Shelf Pourers`
<sub>`Assets/PharmaSynth/Scripts/Editor/ShelfPourBuilder.cs`</sub>

Wires the hand-placed reagent bottles for visible pouring (LiquidPourer is otherwise only on runtime-spawned props, so tipping a shelf bottle shows nothing). Sweeps every LiquidPhysics under the ReagentShelf and ReagentCabinets roots through ShelfPourWiring.WireBottle, and ensures the persisted particle material asset exists so device builds don't strip the URP particle shader. Idempotent; re-running reports 0 additions.

### `Tools/PharmaSynth/Wire Spawn Height (Fixed Per Scene)`
<sub>`Assets/PharmaSynth/Scripts/Editor/SpawnHeightWiring.cs`</sub>

Wires the fixed eye height onto the open scene's XR rig (fixed, not relative to the player's real height). Run once per scene (MainMenu + SampleScene). Idempotent. Tune the constants below and re-run to adjust.

### `Tools/PharmaSynth/Wire VR Affordances`
<sub>`Assets/PharmaSynth/Scripts/Editor/VrAffordanceBuilder.cs`</sub>

Wires the VR affordance batch into the open scene: 1. HAPTICS — every hand interactor (NearFar + Poke) gets a HapticImpulsePlayer + SimpleHapticFeedback so grabbing, socket-snapping and poking UI buzz. 2. HOVER HIGHLIGHT — every grabbable gets a HoverHighlight so it brightens + pops when a hand/ray hovers it (small real-scale tools are easy to find). 3. SOCKET GHOST — every station socket shows a translucent preview of the correct item snapped in place. (Runtime-spawned experiment props/sockets 
