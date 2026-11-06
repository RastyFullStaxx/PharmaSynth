# Gotchas

Up: [[Home]] · [[Process MOC]] · [[Architecture MOC]]

Traps that have already cost this project real time. Every entry here is a **bug that
actually happened**, not a hypothetical. Read the relevant section before touching
that area; add to it when you lose a day to something new.

---

## Unity Editor & MCP

> [!danger] `Unity_ReadConsole` lies about compile errors
> It returned **0 errors** while a CS0136 sat in `PharmaSelfTests` and the assembly
> silently refused to rebuild. `IsCompiling: false`, and "success" from
> `Assets/Refresh` and `RequestScriptCompilation` — all of it still lies.
>
> **The only source of truth:**
> ```bash
> grep "error CS" Logs/Editor.log | tail
> ```
> A stale `Library/ScriptAssemblies/*.dll` after a refresh means the compile
> **failed**, not that Unity is idle. And a suite run whose *assertion count* did not
> move ran the OLD assembly, whatever the timestamp says.

> [!danger] A new `.cs` can import but never reach the assembly
> `PlaytestFixW533.cs` had a valid `.meta`, `LoadAssetAtPath<MonoScript>` returned it,
> the assembly compiled green — and its class was absent from the DLL with its menus
> unregistered. `ImportAsset(ForceUpdate)` and `RequestScriptCompilation` did nothing.
>
> **Fix:** rename the file to a NEW asset path (fresh GUID) and refresh. Verify the
> type actually landed with
> `grep -a <TypeName> Library/ScriptAssemblies/Assembly-CSharp-Editor.dll` — not with
> a menu list.

- **Never `RunCommand` while the user is in Play mode.** `OpenScene` throws and scene
  edits force-exit play. Check `Unity_ManageEditor GetState` first.
- `Unity_RunCommand` compiles inside `Unity.AI.*` → **fully qualify or alias**
  `UnityEngine.UI.*` (`UImage`, `UButton`). `System.Reflection`, `ISet` and
  `GetInstanceID` are blocked. File writes and `AssetDatabase.DeleteAsset` get flagged
  "requires user interaction" → use Bash for files, load-or-overwrite for assets.
  **Menu items are exempt.**
- One scene per command. `AssetDatabase.Refresh()` before cross-scene asset loads.
  A `Refresh: true` menu execute can swallow the run in the domain reload.
- MCP **"named pipe not found"** = editor busy → wait for `Logs/Editor.log` to go
  quiet, retry.
- MCP **"Connection revoked"** = the bridge is **awaiting approval**, not an
  entitlement problem. Approve the client under Project Settings ▸ AI ▸ Unity MCP.
  The decision is logged verbatim in `Logs/Editor.log` — grep for
  `=== Connection Info ===` and read the `Validation:` / `Reason:` lines rather than
  guessing. Use the file fallbacks in [[Build and Test Loop]] while it is down.
- `Unity_Camera_Capture` is broken → use **DevCapture** (yaw **0–360 only**; negative
  values misparse).

> [!warning] Unity's MCP panel misreports code signatures — ignore the scare banner
> The Connected Clients panel flags `claude.exe` in red — *"This application is
> unsigned or not recognized and may be dangerous!"* — and lists its publisher as
> **NAVER Global Root Certification Authority**. Both are wrong. Windows reports
> `Status: Valid`, signer `CN="Anthropic, PBC"`, issuer DigiCert; and there is **no
> NAVER certificate in any machine store** (LocalMachine\Root, CurrentUser\Root,
> LocalMachine\CA all return zero). Unity's own `relay_win.exe` gets the identical
> bogus NAVER attribution, so this is a Unity publisher-lookup bug, not a property of
> either binary or of this machine.
>
> **Verify with `Get-AuthenticodeSignature <path>`, never with Unity's panel or the
> `Signed:`/`Publisher:` lines in `Logs/Editor.log`.** The `Accepted` state is what
> actually governs access. A 2026-08-27 session trusted Unity's report and wrongly
> concluded a binary was unsigned.

> [!danger] An embedded package silently overrides the registry — and freezes you there
> `com.unity.ai.assistant` had been **vendored into `Packages/`** (523 MB, 6340 tracked
> files) at repo init, because pre-release packages were disabled and it could not
> resolve otherwise. A folder directly under `Packages/` is an *embedded* package: UPM
> ignores the manifest version entirely, so the project sat on **2.13.0-pre.2** for two
> months while `Packages/manifest.json` looked perfectly reasonable.
>
> The visible symptom was **"Connection revoked"** on every MCP call, which was
> misdiagnosed for weeks as a lapsed AI seat — and nearly paid for with a subscription
> that would have changed nothing. The real cause was a June build predating the
> 2026-07-21 release that removed entitlement caps.
>
> **Tells:** `packages-lock.json` says `"source": "embedded"` and
> `"version": "file:<name>"`; editor stack traces read `./Packages/<name>/...` instead
> of `Library/PackageCache/...`.
>
> **Fix:** close the editor (loaded DLLs are file-locked), `git rm -r` the folder, drop
> the stale lock entry, pin the version in the manifest, enable pre-release packages if
> the version needs it. Check `du -sh Packages/*` when a Unity-owned package misbehaves
> in a way its version number cannot explain.

---

## Edit mode vs Play mode

> [!danger] The `AddComponent` mirror traps
> Both cost a hard-stuck playtest on 2026-07-17.
>
> - **Edit mode:** `OnEnable`/`Awake` do *not* fire on `AddComponent` → hence the
>   `Bind()` seams everywhere.
> - **Play mode:** `AddComponent` fires `OnEnable` **immediately** — *before*
>   `Bind()`, while fields are still null. So event subscription must live in the
>   `Bind` seam, not in `OnEnable` alone.
> - `DestroyImmediate` **skips** `OnDisable`/`OnDestroy` for components whose
>   `OnEnable` never ran → call an explicit `Detach()` before destroying, or C# event
>   subscriptions ghost forever.

- **Never `renderer.material` in edit mode** — use `sharedMaterial` or an MPB. TMP's
  `outlineWidth` also instances materials.
- `AddComponent<LiquidPhysics>` needs a Renderer host.
- Before deleting a script, **grep all of `Assets/`** for the type name.
- FBX imports have **no colliders**.
- ChemLab `_WithLiquid` prefabs: verify **mesh** names, not GameObject names.

---

## Builders that destroy what they touch

> [!danger] Rebuilding silently takes components with it
> A 2026-07-16 `Build Reagent Cabinets` run wiped `Raw_Matchsticks`'
> `MatchStrikerSurface` and two `FlameAnchor`s. Striking the box did nothing — which
> killed the methane splint test **and every burner light in the game** — and nothing
> caught it, because the pure `ShouldStrike`/`ShouldIgnite` tests still passed.
>
> **Pure math cannot see a missing component. Pin the actual scene objects.**
>
> Recovery: `Apply Verb Data` → `Add Placement Anchors` →
> **`Re-Home Scene Items (Adopt Current)`**. Adopt the CURRENT hand-placement; never
> re-apply a transform from git (a HEAD *local* position against a different parent
> left the ice bucket floating at y = 2.05).

- `Build Reagent Cabinets` preserves only **unit** transforms, never the items inside.
  Hand-placed things must be **excluded** from stocking, not stocked —
  `IsHandPlacedConsumable` covers the ice bucket and the 4 consumable boxes.
- `ShelfY`'s lowest entry (0.11) is the **Base panel's top face**. `BuildUnit` always
  made that surface and nothing stocked it, so every unit wasted a whole bottom row —
  that is why "overflow" was a phantom.
- **Verb wiring is a step.** `Apply Verb Data` wires droppers, the 0.1 g
  spatula, the water bath, the ice bath and time-skip. `Name Tubes + Build Rack Slots`
  wires holder snapping. Both are suite-pinned (`wired:`).

---

## The bench and layouts

> [!danger] The bench ALREADY EXISTS — a layout must never stage it
> Cost a 46-object duplication on 2026-07-16. `SpawnRackKit`, `SpawnSpares` and
> `StageConsumables` were **deleted**: they re-spawned a rack + 6 tubes, 2 spare
> beakers, a flask and a redundant `MatchStriker` cube on **every** module, undoing
> the user's hand-deletions.
>
> Vessels set **`Vessel.benchItem`** (e.g. `Kit_TestTube_0`) → the builder *finds* the
> object and attaches only task wiring. `ClearBenchBindings()` strips it each build or
> it leaks into the next module. **Only task-bound VESSELS may be staged.**

Never author a prop for a general tool or a pourable reagent bottle — they all already
exist: `Eq_Dropper`, `Eq_PorcelainSpatula`, `Eq_Funnel`, `Eq_TestTubeBrush`,
`Eq_GlassRod`, `Eq_WashBottle`, `Eq_Beaker_*`, `Eq_GraduatedCylinder_50mL`,
`Eq_WatchGlass`, `Kit_BunsenBurner_0-9`, `Kit_TestTube_0-18`,
`Kit_Hard-GlassTestTube_0-3`, `Kit_Motar`, `Pestle`, `Tripod`, `WireGauze`, …

A station's `ZoneItemSensor` matches a `LabItem.itemId` **wherever it already lives**,
so point `requiredItemId` at the bench (`kit-bunsenburner`, `kit-funnel`,
`kit-waterbath`).

🔎 **See what a module litters the bench with:**
`Tools ▸ PharmaSynth ▸ Reveal Stage ▸ <module>` — builds the stage in edit mode and
logs an inventory grouped by spawner. Deletes nothing.

### Tubes and racks

Two roles share `itemId kit-testtuberack`:

- **STORAGE racks** (`Eq_TestTubeRack`, `TestTubeRack_2-5`) — where tubes live and
  home. **They get NO slots.** ⛔ Never re-derive them: the tubes were duplicated
  *with* their holders and already sit perfectly.
- **WORKSPACE holders** (`Experiment_Tube_Table_Kit_Holder_1-4`) — start **empty**;
  the player drags tubes in mid-experiment. Only these get `Slot_0-5` anchors.

⚠ **A broken tube respawns at its BAKED home**, so positions must be right *before*
re-homing.

---

## Physics and bounds

> [!danger] Measure with `ExperimentSceneBuilder.SolidWorldBounds`
> Never all child renderers. `LiquidPourer`'s **world-space** `StreamLine`/`PourStream`
> outlive the pour still pointing at the FLOOR, so encapsulating them dragged bounds
> down a metre and racked tubes flew into the air. Same trap for any bounds-fitted
> child. `IsEffectChild` is the pinned list.

> [!danger] Never write the rig root's Y from a horizontal system
> `HeadCollisionPushback` applied its wall-sweep correction as a full 3D vector.
> Real head tracking jitters every frame; each downward jitter sank the rig, which put
> the knee sweep deeper into the floor and guaranteed the next hit. Runaway sink → the
> CharacterController ends up fully inside the floor collider, `Move()` can no longer
> resolve it, and the player is **stuck under the lab floor**. Headset-only: the XR
> Device Simulator's head barely moves, so it never fired.
>
> Also: `Physics.CapsuleCast` returns **distance 0** when the sweep starts already
> overlapping. Treating that as a blocking hit pinned the head in world space and
> dragged the rig by the inverse of every head movement.
>
> Vertical placement belongs to the **CharacterController + gravity**, always.

- `SeatedHeightBoost`'s ground guard is now **two-sided**. It was up-only, which is
  why a sink was unrecoverable.
- Fixed eye height is **not relative to real height** — the Quest runtime flip-flops
  between Floor and Device origin spaces across sessions, and every relative scheme
  produced floor-spawns or roof-spawns.

---

## Text and UI

- **`GlyphSafe.Sanitize` every new TMP string.** LiberationSans lacks ☑ ▶ → Δ ↑ °.
- Transparent geometry does not z-write → visibility is **sortingOrder**:
  HUD 30000 · bubble 29000 · world panels 4000–5000 · TMP labels 20000.
- **TMP will not rebuild `textInfo` on an inactive GameObject.** Activate → write text
  → `ForceMeshUpdate`. Doing it in the other order returned the *previous* line's
  `characterCount`, so a longer line exited the reveal loop early and sat truncated.
  Never trust a 0 count — fall back to the raw string.

---

## Shaders

> [!warning] A live list kept by OnEnable is empty in edit mode
> `BurnerController.Active` and `Matchstick.Active` read a list that OnEnable/OnDisable keep
> in Play mode, and fall back to a scene search in edit mode. Edit-mode `AddComponent` never
> runs OnEnable, so the simulators would see an empty list. Any new registry needs the same
> fallback.

> [!danger] One MonoBehaviour per file, file named after the class
> Unity links a saved component to its script by the FILE's GUID, and a file resolves to
> the class matching its name (or its first class). A second component in another's file
> (`HazardZones.cs` holding `FumeHoodStatusLabel`, `DryDistill.cs` holding `NakedFlameHeat`)
> saves with a local-only `m_Script` and reloads as an empty slot — or the editor rebuilds
> it from the class NAME while a player build drops it. That silently disabled the fume
> hood label, the brush scrub, and both burners' open-flame heat. Fixed 2026-11-07 by
> splitting the files (renames keep the .meta, so existing links follow the class);
> `bench: every component in the scene has a real script file` pins it, and
> `bench: every game component lives in a file named after it` stops the cause returning
> (all 13 misfiled classes were split the same day).

> [!warning] World TMP labels sort at 20000 — anything meant to cover them must sort higher
> The balance's digital readout (a world label) drew through the grade card (4500) from
> 5 m behind it. Review panels (grade card, quiz tablet) now sort at **21000**: above world
> labels, below speech bubbles (29000) and the HUD (30000). Suite-pinned.

> [!danger] A verb that calls `CompleteTask` every tick floods the mistake log
> `LiquidTaskBinding` calls `runner.CompleteTask` on every delivery tick once a step's
> reagent is in; an out-of-order step returned BlockedByPrerequisite and logged a
> WrongStep (-4 grade) **per tick** — a one-second early pour failed the run.
> `ExperimentRunner.CompleteTask` now latches one WrongStep per task per attempt, and the
> binding retries the early step when any task completes. Any new verb that polls
> `CompleteTask` inherits the latch; never latch at the call site instead.

> [!warning] A CharacterController floats by its own skinWidth
> It rests with the capsule bottom **one `skinWidth` above the ground**. Fit the capsule
> bottom at the feet and the character hovers by skinWidth (0.08 m by default) — Dr.
> Jimenez floated ~10 cm in every cutscene. Put the bottom skinWidth **above** the soles
> (and remember an idle clip can lift the soles off the bind pose).

> [!warning] `Camera.stereoTargetEye` does nothing under URP
> It only applies to the built-in renderer and logs a warning per call. To keep a
> camera flat (not an XR eye) set `UniversalAdditionalCameraData.allowXRRendering = false`
> — what `CinemaView`'s shot camera does.

> [!danger] URP's stock Unlit declares no `_ZTest`
> `SetInt("_ZTest", …)` on it is a **silent no-op**. Both tutorial overlay materials
> inspected as correctly configured and would have shown through nothing. Hence
> `Art/Shaders/PharmaGuide.shader`. **Verify by grepping the `.mat` for `_ZTest`.**

---

## Materials, textures and lighting

> [!danger] `_Smoothness` above 1 is a silent mirror
> URP clamps `_Smoothness` to 0-1, so a material authored at **2.19-3.20** renders as a
> **perfect mirror** and nothing warns you. Four Laboratory-pack materials shipped that
> way - `Ceiling_1` 2.60, `Ceiling_2` 3.20, `Floor` 2.51, `Cabinet` 2.19 - and with **no
> reflection probe in the scene** they mirrored the built-in **procedural outdoor sky**
> inside a sealed windowless room. That is why the lab read as wet plastic for months.
>
> **Never trust a pack's PBR values.** Sweep them:
> ```bash
> grep -rH "  - _Smoothness: [2-9]" --include="*.mat" Assets/
> ```
> Now pinned by `SurfaceSuite` (`surface: no material has smoothness > 1`).
> The opposite error shipped too: the four vessel materials the player handles most
> (beaker 100/500, Erlenmeyer, graduated cylinder) sat at smoothness **0** - matte glass,
> no specular, no reflection - while `GlassMat`/`GlassInnerMat`/`GlassOuterMat` next to
> them were correct at 0.92-0.95.

> [!danger] A Volume profile renders nothing if the camera has post-processing off
> `SampleSceneProfile.asset` had Bloom, Vignette and Tonemapping authored and active, and
> a global Volume sat in the scene - while **both cameras had `m_RenderPostProcessing: 0`**.
> Everything inspected as configured and none of it ran. The same shape of waste: the RP
> asset had reflection-probe **blending and box projection enabled with zero probes** in the
> scene to feed them.
>
> Check the CAMERA, not the profile. And retune before enabling: the template Vignette was
> active at full strength and would have switched on the instant post went live.

> [!danger] Do not re-tile a pack texture without looking at it first
> Retiling `Floor.mat` from 1x1 to 6x6 to "add scale detail" produced hard rectangular
> streaks across the floor. `Floor_AlbedoTransparency.png` has a **defective band of
> vertical lines baked along its top edge**; at the pack's 1x1 mapping that band lands once
> against the far wall and reads as nothing, and tiling repeated the defect six times.
>
> It looked exactly like a bad lightmap bake, and cost **three full rebakes** (resolution
> 12 -> 24 -> 40, plus seam stitching) before the lightmap was ruled out.
> **The decisive test took ten seconds:** set the renderer's `lightmapIndex = -1` and
> capture. The streaks survived, so they were never the bake.
> When an artifact is *invariant to every setting you change*, stop changing settings -
> that invariance is the evidence.

> [!warning] A pack albedo is often an ATLAS, not a tiling swatch
> Binding a flat epoxy map to `Table_1`/`Table_2`/`WashTable` turned **every bench cabinet
> in the room black**: those materials already carried a pack albedo covering the white
> cabinet doors *and* the dark worktop in one atlas. Check whether `_BaseMap` is already
> bound before you bind over it - only `Wall_0`, `Wall_1` and `Ceiling_2` genuinely had none.

> [!warning] The Laboratory FBXs bind materials BY NAME, and it is not recorded on disk
> They import with `materialLocation: 0` (External, obsolete in Unity 6) and an **empty
> `externalObjects: {}`**, so bindings are re-resolved by material name on every reimport.
> Editing the `.mat` files themselves is safe; relying on the FBX to remember an assignment
> is not. Reimporting 12 of them to add lightmap UVs did *not* break anything - verified by
> capture - but check after any reimport rather than assuming.

- **`generateSecondaryUV` is off on 36 of 107 models**, including the whole room shell.
  A mesh with no UV2 cannot receive a lightmap. `LabLightingBake` turns it on only for
  meshes it is about to mark static, and anything still lacking UV2 is set to
  `ReceiveGI.LightProbes` so it still occludes and bounces instead of rendering black.
- **Never bake a grabbable.** An object baked into a lightmap carries its baked shadow
  around the room in your hand. `LabLightingBake.IsBakeCandidate` excludes anything with a
  Rigidbody, `XRGrabInteractable`, `DropRespawn`, `LabItem`, or a dynamic-root ancestor.
- **A runtime `Shader.Find` material gets its shader STRIPPED from device builds.**
  `EffectVfx` has guarded against this since W5.7 by instantiating
  `Resources/FxParticleUnlit`; `StationVfx` did not, so every station steam / frost / drip /
  bubble effect was a candidate to render wrong on the Quest while looking perfect in the
  editor. Both now share the guarded path.

---

## Pharmee's transform ownership

> [!danger] Four scripts move Pharmee, and they only compose because nothing overlaps
> | Transform | Sole owner |
> |---|---|
> | root **position** | `FloatBob` — one sum: `home + bob + jitter + giveWay + gesture` |
> | root **rotation** | `FaceCamera` — FloatBob's `applyRotation` is **0** in the scene |
> | **`Robot Origin`** rotation | `PharmeeAttitude`, in LateUpdate, **overwritten absolutely** |
> | **`Wave*`** ring scale | `PharmeeAttitude` |
>
> `PharmeeMover`, `PharmeeGiveWay` and `PharmeeGestures` write **no transform at all** —
> they feed `SetHome()` / `SetGiveWayOffset()` / `SetGestureOffset()` + `SetPose()`.
>
> **Never add a component that writes `Robot Origin`.** `PharmeeAttitude` assigns it
> absolutely every LateUpdate, so a second writer either loses silently or fights per frame.
> New motion composes *into* that one expression. Suite-pinned (`gesture:`), including
> `applyRotation == 0`, because a stray inspector tick there starts a fight with FaceCamera
> that looks like a physics bug.

> [!warning] Pharmee has NO skeleton — and the model ships animation that never plays
> `RobotNPC.glb` and `RobotNPC.fbx` both have zero skins, joints, LimbNodes, Deformers and
> BindPoses; all 12 of his renderers are `MeshRenderer`. Skeletal animation is impossible
> without a re-rig, which is why the animation set is procedural.
>
> The FBX *does* carry ~364 `AnimationCurveNode` of baked per-node TRS (it is a Sketchfab
> "Futuristic flying animated Robot"), with `importAnimation: 1` and no controller — so none
> of it runs. **Do not simply switch it on:** those curves drive the same Wave rings and body
> node `PharmeeAttitude` already animates, including the `waveSpeedMultiplier = 30` the user
> asked for.
>
> He does have two **hand pivots** (`Hand origin`, `Hand origin.002`) that nothing moved
> before W5.38 — that is the pointing affordance, already in the mesh.
> `RobotNPC.glb` is **orphaned**: zero references anywhere.

> [!warning] `Wire NPC Polish` rebuilds Jimenez's subtitle bubble and drops its AudioSource
> A W5.38 run left `JimenezSubtitles` with **no AudioSource at all** and failed
> `voice: every narration channel has an AudioSource`. Recovery is
> **`Voice ▸ Fix Voice Audibility + Music Ducking`**. Same family as the
> `Build Reagent Cabinets` component wipe — run the repair menu after the builder.

> [!warning] A `FloorBusy` suite failure is usually the PREVIOUS run, not the product
> `NPCNarrationController.s_floor` is a **static** and survives every suite run inside one
> editor domain, so temporary narrators from an earlier run strand it and the next run fails
> *"nobody holds the floor when nothing is speaking"* — a false red that clears only on a
> domain reload. `Run()` now calls `NPCNarrationController.ClearFloor()` first.
> **If a suite failure disappears after a domain reload, it was leaked static state.**

---

## Files, YAML and encoding

> [!warning] Hand-written Unity YAML
> **Single-quote any string scalar containing `": "`** — e.g. `"unchanged: that
> contrast"`. Unquoted, YAML parses it as a key/value split and silently truncates the
> field. Double internal apostrophes.

- `re.sub` expands escapes in its **replacement** template → a literal `\n` becomes a
  real newline. Pass a lambda.
- **Windows FS is case-insensitive** → case-only asset renames need
  `AssetDatabase.RenameAsset`.
- **PDFs:** TEMP is hijacked, so the Read tool cannot open them. Use
  `"C:/Program Files/Git/mingw64/bin/pdftotext.exe"` or pypdf.
- Internet via `curl` (github-raw rate-limits at 429).
- `generate-voice.ps1` needs `-Encoding UTF8` on the manifest read **and** a UTF-8
  *byte* body, or PowerShell 5.1 mangles every em-dash. The `.ps1` must keep its BOM.

---

## Test suite

- **Expected warnings** (not failures): the two W5.9 guard tests and the
  Unknown-moduleId negative test.
- The suite **pins behaviour** — Mishandling lists, ContentSuite task counts, layout
  spacing. Move a pinned assertion **in the same change** as the behaviour it pins.
- Scene-pinned assertions fail *en masse* when the wrong scene is open. → [[Build and Test Loop]]

---

## A bug class the simulator cannot catch

> [!danger] `SimulatedRun` pours from the BINDING, never the HINT
> So a hint whose ACTION line contradicts the binding it must satisfy is structurally
> invisible to it. The W5.34 clueless-player audit found an Exp 2 hard-stuck blocker
> this way: the hint said "leave tube 4 alone" for a tube its own `rackGroup`
> required — and there is no feedback, because the have/required readout only appears
> on a vessel you actually pour into.
>
> **Read hints cold, as a first-timer, and cross-check each ACTION line against the
> binding.**

## Verifying a type actually reached the DLL

> [!warning] grep the DLL for TYPE and MEMBER names, never for a string literal
> .NET stores type and member names in the `#Strings` heap as **UTF-8**, but user string
> literals in the `#US` heap as **UTF-16**. So `grep -a "my assertion text"` against
> `Library/ScriptAssemblies/*.dll` returns 0 even when the code is definitely there, and
> reads exactly like "my edit did not compile" (W5.39, 2026-09-02).
>
> Check `grep -ac VerbDemoMath …` or `grep -ac EnsureDemoGhostMaterial …` instead.
> The companion rule still stands: a suite run whose **assertion count did not move**
> ran the old assembly, whatever the DLL timestamp says.

## A guidance rung that computes but is never consumed

> [!danger] `TutorialCoach.LevelFor` returned 1/2/3; `Update()` returned unless it was 3
> The 15 s and 30 s rungs of the stuck ladder were fully specified, pinned by the suite
> (`LevelFor(20f) == 1`), documented in gameplay-flow — and had never once run in the
> shipped game. The pins tested the *calculation*, not that anything acted on it.
>
> This is the same shape as `WaypointGuide` calling `Hide()` in all 9 modules for weeks:
> **a consumer that silently does nothing reads exactly like a feature that is merely
> off.** When a pure function returns a value nobody branches on, pin the CONSUMER too.

## A freshly generated voice clip does not import itself

> [!warning] `Import & Wire Voice Clips` will report the OLD count
> `generate-voice.ps1` writes the mp3 straight to disk from outside Unity, so until the
> editor imports it there is no `.meta` and no `AudioClip` asset — the wire tool then
> scans the bank and truthfully reports the previous total, which reads as "the
> generator silently failed" (2026-09-02).
>
> Force `AssetDatabase.ImportAsset(<path>, ForceUpdate)` (or refocus the editor) and run
> the wire step **again**. Confirm by the count moving, and by grepping
> `VoiceBank.asset` for the clip id — not by the menu returning success.

## Edit-mode simulation: the component never woke up

> [!danger] `OnEnable` subscriptions and `Update`-driven sims are both absent in edit mode
> `MethaneApparatusRig` wires itself to `ExperimentStarted` from `OnEnable`, so in edit mode
> the event passed it by and **none of its five completion conditions registered**. The
> symptom is vicious: the verb is performed perfectly — the grind reported
> `progress 1.00, IsGrindComplete=true` — and the step still never completes, which reads
> as a broken verb rather than a missing subscription. Separately `TemperatureSim.Update`
> (`→ Tick(deltaTime)`) never runs, so a lit burner held to a tube never warms it: the rig
> only ever calls `SetHeating`.
>
> **Prime the seam and drive the sim** — call the component's own public handler and tick
> the sims the simulator replaces. Do not "fix" the game for a frame that edit mode simply
> does not run. Refactor `Update()` into a public `Step(float dt)` so the simulator drives
> the REAL frame rather than a reimplementation that can drift.
>
> `TaskGraph.HasCondition(taskId)` exists to tell these apart: a verb nothing is listening
> for looks identical, from the outside, to a verb performed badly.

## A test harness that contaminates itself

> [!danger] The first `SimulatedMisplay` pass reported 11 bugs. All 11 were the harness.
> Three independent mistakes, each of which produced confident, plausible, wrong findings:
> - **Shared state between probes.** `StartExperiment` rebuilds the task graph but NOT the
>   scene, and a `LiquidTaskBinding` accumulator lives on the component — so the
>   wrong-reagent probe delivered the step in full and the starvation probe then blamed the
>   supply monitor for correctly seeing no shortfall. Rebuild the stage per probe.
> - **Matching by reference where the code matches by name.** The drain compared
>   `currentChemical` by asset reference; `ReagentSupplyMath` keys availability by
>   `chemicalName`. Same-named bottles stayed full.
> - **Asserting the wrong contract.** Recovery was measured on TASK completion, but a task
>   may name several reagents and the probe pours one — the evidence was in the message
>   the whole time ("needs 40.0, accumulated 42.0").
>
> **Make the failure message print the state it judged.** Every one of these was diagnosed
> the moment the report quoted its own numbers instead of just saying "failed".

## Unity_ManageAsset: the action is `Import`

> [!tip] `Refresh` and `Reimport` are not valid AssetActions
> When the editor stops auto-refreshing (MCP menu executes run against the STALE assembly
> and the DLL timestamp never moves), `Unity_ManageAsset` with `action: "Import"` and the
> script's path forces the import and the recompile. `Unity_RunCommand` frequently answers
> "Unity not detected" in the same state.

## With no headset you spawn INSIDE THE FLOOR

> [!danger] `SeatedHeightBoost` never calibrates without a tracked pose
> The rig uses `RequestedTrackingOriginMode: Floor`, and the fixed-eye-height driver only
> applies its offset when `cameraTransform.localPosition.sqrMagnitude > 0.0001f`. An
> untracked HMD reports **exactly (0,0,0) forever**, so in PC Dev Mode the guard never
> passes, the offset stays 0, and the eye sits on the rig root — at floor level. The lower
> half of the view fills with the floor plane seen from inside it, which reads as "the room
> is dark" or "there is a big blue wall" rather than as a height bug (user, 2026-09-02).
>
> On a Quest `head.y` is non-zero from the first tracked frame, so the headset path was
> always fine — which is why months of edit-mode verification never saw it, and why it was
> the play-mode autopilot's very first screenshot that caught it.
>
> **Fixed** by distinguishing *never tracked* (place the eye at the authored height) from
> *tracking dropped mid-session* (hold the last offset, as before) via `_everTracked`. The
> new branch is unreachable on a headset, so the scarred headset behaviour is untouched by
> construction.

## "Not enough reagents left to finish" at 0% progress

> [!danger] The supply monitor counted the module's OWN PRODUCT as a bench reagent
> The 2026-07-16 client rule DELETED ready-made bottles of Acetanilide, Chloroform,
> Benzamide and Wine precisely so the player must synthesise them. Every later step that
> draws from your own product — the chemical tests, the wash, the racking — then looked to
> `ReagentSupplyMonitor` like a step needing a bottle that does not exist. It raised
> `SupplyExhausted` **at 0% progress, before the player had touched anything**, and
> restarting could never help because the bottle is absent by design.
>
> Four of nine experiments were unplayable this way (user, 2026-09-02). It survived every
> edit-mode check because they all ask "does a bottle run DRY during a run", never the
> simpler "can the bench satisfy this module before the player starts".
>
> **Fixed** by skipping steps whose reagent is `DemoMode.ProductFor(moduleId)`. Pinned by
> the battery's new **supply-at-start** section, which starts every module on a fresh stage
> and requires `EvaluateNow()` to be empty.

## Guidance can point at deliberately HIDDEN objects

> [!warning] The target sweep includes inactive objects, and some are hidden ON PURPOSE
> `TutorialTargets.Build()` matches source bottles over all `LiquidPhysics` including
> inactive, so it registers things that are deliberately switched off for that module:
> the module's own end product (`EndProductVisibility`), the methane-only staged props
> (`MethaneStageVisibility`), and the consumable dispensers' hidden `Template_*` clone
> sources. Tutorial Mode then glows and points at something the player can never see.
>
> **Fixed** by one shared rule, `TutorialTargets.Visible(Component)`: a target must be
> `activeInHierarchy` and must not be a `Template_*` clone source. Applied to the source
> lookup and the station-tool lookup.
>
> **Safe for coverage by construction** — the pour branch registers the DESTINATION before
> it looks for a source, and the verb branch registers the station before the tool, so
> dropping a hidden object can never leave a step with nothing to point at. Verified:
> `Audit Tutorial Targets` still 9/9 81/81, `Simulate Tutorial Guidance` still 73/73, and
> the autopilot's four findings went to zero (143/143 objects pickupable).

## Two bakes go stale when furniture moves, not one

> [!warning] The NavMesh bake has the same shelf life as the lightmap bake
> `Build Lab NavMesh` (W5.44) feeds Tutorial Mode's ground path, and the benches ARE the
> obstacles it routes around — so moving furniture invalidates it exactly as it
> invalidates the lighting. Re-run **both** after any `Select Movable Furniture` session:
> `Build Lab NavMesh` · `Build Lab Probes` → `Run Lab Lighting Bake`.
>
> A stale navmesh fails quietly and confidently: the arrows still draw, they simply route
> through a bench that has since moved.

## Two cues answering one question

> [!tip] Attention is the scarce resource in Tutorial Mode, not information
> The mode already carries glow, x-ray ghost, beacon, spotlight dim, labels, watch hint,
> coach ladder and the verb demo. Every cue added makes the others weaker, so W5.44's rule
> is that new cues are **conditional** and mutually exclusive where they overlap: the
> ground path owns "which way round the benches" beyond 2 m, the beacon owns "which object,
> through that door" within it, and `GuidePathMath.ShowPath`/`ShowBeacon` keep that split in
> one suite-pinned place rather than in two components that can drift.

## Baking a NavMesh silently rewrote the whole SCENE as binary

> [!danger] `NavMeshSurface.BuildNavMesh()` leaves its data IN the scene unless you save it
> `BuildNavMesh` produces a NavMeshData object owned by the surface in memory. Saving the
> scene then serialises it INTO the scene — and NavMeshData cannot be written as YAML, so
> Unity **ignores ForceText and rewrites the scene as binary**. `SampleScene.unity` went
> from 5.08 MB of readable YAML to 1.88 MB of bytes, `git diff` reported only
> `Bin 5080510 -> 1879714`, and `gen-vault-reference.py` parsed **0** objects out of it and
> cheerfully reported "15 Scene Objects" (all from MainMenu). Nothing warned (W5.44).
>
> The content was never lost — only the format — but a binary scene destroys diffs, merges
> and every text-based audit this project relies on.
>
> **Fix:** persist the data as its own asset immediately after the bake
> (`AssetDatabase.CreateAsset`, or `CopySerialized` onto the existing one so the surface's
> guid survives) → `Assets/Scenes/LabNavMesh.asset`. The scene stays YAML.
>
> **Check after ANY new scene-saving builder:** `head -c 10 Assets/Scenes/SampleScene.unity`
> must print `%YAML 1.1`. A size that DROPS sharply is the tell.

## Reopen the scene before running a builder that SAVES

> [!warning] Simulators mutate the open scene; builders save it
> `Simulate Everything`, `Audit Tutorial Targets` and `Simulate Tutorial Guidance` all build
> stages into the open scene by design, and say so. Running a builder that calls
> `SaveOpenScenes()` afterwards commits that mutated state to disk. Reopen SampleScene
> between the two, every time — the reports say so for a reason.


## A vessel with residue can never hold anything again

> [!danger] `AddLiquid` refused to adopt while a precipitate sat in the glass
> `PourOut` drops `currentChemical` once the liquid runs dry but leaves the settled
> precipitate behind. The wake-from-empty guard required **both** columns near-empty, so a
> tube emptied after a test held residue and NO chemical — and every later pour piled up
> with no identity at all. `FindReaction(null, x)` misses forever: no reaction, no colour,
> no product, and nothing anywhere says why.
>
> It made **two experiments unplayable** and no edit-mode check could see it, because the
> edit-mode simulator snapshots and restores every vessel around each run while the play
> sweep inherits the previous module's glassware (W5.45).
>
> **Fixed:** `currentChemical == null` now wakes the vessel too. Pinned by
> `liquid: a vessel holding only residue still adopts what is poured in`.

## A product stream that delivers to the wrong glass

> [!danger] "Expected now" is true for every later step, not just this one
> `VaporCollectController.Update` picked the nearest binding satisfying
> `IsExpectedNow(product)`. In Exp 7 four later steps also draw on chloroform, so the
> redistillate condensed into a downstream test tube, emptied the source flask, and
> `dry-redistil` could never complete — while the class doc claimed the stream was
> "targeted, so it can never pollute a bystander tube".
>
> **Fixed** by scoping the receiver search to a binding expecting the product **for the
> controller's own task** (`ExpectsForThisStep`). When a controller owns one task, match on
> that task — a chemical-only match silently spans the whole module.

## Glassware laid on its side drains itself

> [!danger] `LiquidPourer` measures the ROOT transform up, so a rotated prefab pours forever
> Both `DistillingFlask` objects sat at **90° from upright** (a -90° X rotation, the shape a
> glTF import takes). `PourTick` fires on
> `Vector3.Angle(Vector3.up, transform.up) > pourThreshold`, so the flasks drained every drop
> poured into them: a false `SpilledReagent` mistake on a *perfect* Exp 3 run, and an empty
> source for Exp 7's redistillation.
>
> Nothing caught it for months because edit-mode simulation never runs `Update`, so nothing
> ever poured. **Scan before trusting a vessel:** compute each vessel transform tilt from
> world up; only these two were wrong.
>
> **Fixed** by standing them upright and re-running `Re-Home Scene Items (Adopt Current)` —
> the baked home carries the rotation too, so re-homing is part of the fix, not optional.

## A harness that completes steps for the player proves nothing

> [!danger] The escape hatch that makes an edit-mode audit useful is a lie in Play mode
> `SimulatedRun`'s handlers call `runner.CompleteTask(id)` — "force past to keep exploring
> downstream" — in 22 places. That is the right trade for a static audit: one broken step
> should not hide the other twelve. Run the same handlers in PLAY mode and those same lines
> silently mark steps done that no player could have done, which is the one thing a
> play-mode sweep exists to detect (user, 2026-09-02: "no programmatically cheating
> through").
>
> **`SimulatedRun.NeverForce`** disables all 22 while the visual sweep plays. The sweep
> instead waits ~12 s of real frames, re-applies the player physical hold each tick,
> re-serves once, and then reports the step **UNPLAYED** and stops the module.
>
> The first no-force run went from "10 steps forced, everything green" to two genuinely
> unplayable experiments — both of which turned out to be real game bugs.

## An observation that fires, announces itself, and is never drawn

> [!danger] Every precipitate rule deposits exactly 1 ml, and the gate was `> 1f`
> `ApplyReaction` adds the INCOMING pour to the precipitate column, and a dropper squeeze
> is 1 ml — so `currentPptVolume` lands on exactly 1.0 and `1.0 > 1.0` is false. Six of the
> manuscript's headline observations were authored correctly, fired correctly, announced in
> text and **never rendered**: the milky limewater, both iodoform yellows, the acetanilide
> plates, its hydrolysis crystals and the benzamide solid.
>
> Nothing caught it because every check asked "did the reaction fire", never "could the
> player SEE it". The threshold is now `ShowFromMl` (0.05).
>
> **When a gate compares against a quantity the code itself produces, check the exact
> equality case.** Ship a pin for the value the game actually generates, not a round number.

## Data a rule declares but never carries

> [!warning] `outcome: Precipitate` with `hasPrecipitate: 0` and no precipitate chemical
> `Test_TollensAldehyde` — the silver mirror, the whole point of Exp 2's aldehyde test —
> declared the Precipitate outcome and had neither `hasPrecipitate` nor a
> `resultPrecipitate`. It therefore deposited nothing, forever.
>
> The simulator's own watchdog only fires when `hasPrecipitate` is TRUE and the volume is
> zero, so a rule that never claims the precipitate slips past it. **Audit the outcome
> against the payload**, not just the payload against itself. Now `Chem_SilverMirror` +
> a suite pin.

## A visual rule that can make a vessel draw nothing

> [!danger] "Solids show a mound, so hide the liquid" blanked five finished products
> Suppressing the liquid column whenever the contents are a solid is only safe where a
> mound actually exists. A dry solid arrives by POUR as well as by scoop — the filter and
> drying steps decant the product onto a watch glass, and nothing builds a mound there — so
> the vessel drew neither layer and the finished product was invisible in its own glass.
>
> `DrawsLiquidColumn(ml, dryPowder, hasMound)` now requires a real mound before the column
> is suppressed, pinned with the no-mound case. **Any either/or visual rule needs the
> neither branch checked**: the failure is silent and looks like an empty vessel.

## Millilitres the manuscript asks for can be invisible

> [!tip] 2 ml of aniline in a 250 ml Florence flask is 0.8% of its height
> The quantity is correct, the vessel is correct (the manuscript names the Florence flask),
> and the player still sees an empty flask at arm's length. `DisplayFill01` floors a
> non-empty column at `MinVisibleFill01` (6%) so "there is something in here" reads, while
> every number the player is shown or graded on stays exact. Floor the DRAWING, never the
> data.

## An emissive baked inside an FBX is invisible to every material scan

> [!danger] Pharmee blazed for months and no `.mat` in the project could explain it
> The robot's glow comes from `Blue_Light`, a material **embedded in `RobotNPC.fbx`**
> (`materialImportMode: 2`, no extracted assets), at HDR `(0.46, 2.75, 8.0)` — eight times
> white — on the body panels and all four hover rings. Grepping `Assets/**/*.mat` for high
> `_EmissionColor` finds nine materials and **none of them is this one**, because it does
> not exist as a file. `PharmeeFace` overrode only the eyes and mouth, so everything else
> bloomed into a white ball that wobbled with every talk nod and read as "flashy when she
> speaks" (user, 2026-09-05).
>
> **Read the live renderer, not the asset folder**: `r.sharedMaterials[i].GetColor("_EmissionColor")`
> in a `Unity_RunCommand` found it in one pass. Fixed with `PharmeeGlow`, an MPB writer —
> the FBX and its importer stay untouched and the value stays tunable.

## A voiced character that still beeps

> [!warning] The beep predated the voice-over and nobody removed it
> `PharmeeBrain.Speak` ended with `AudioService.Play(BeepKey(state))` — a per-mood buzzer
> whose own comment called it "Pharmee's robotic voice". That was true before voice-over;
> with 344 real clips it became an error tone fired **under every single line**, which the
> user heard as "a broken machine" (2026-09-05).
>
> **When a system is replaced, grep for what the old one still triggers.** The beep keys are
> still in the SoundBank; only the callers are gone.

## A line assembled at runtime can never have a clip

> [!danger] Clips are keyed by the hash of the WHOLE subtitle
> `VoiceLineId.For(text)` hashes the entire string, so anything built with `+` at runtime
> matches nothing and drops to blips forever. Two shipped that way: the tutorial's
> `"Practice complete — " + steps + " steps, …"` (every practice run ended in blips) and
> the campaign finale.
>
> **Speak a fixed STEM under the dynamic text** — `Say(subtitle, seconds, clip)` already
> takes the clip explicitly, so the bubble keeps the numbers and the voice says the stem.

## Enumerate BOTH overloads in the voice corpus

> [!danger] `DebriefRemark(grade)` was in the corpus; `DebriefRemark(grade, campaignComplete)` was not
> The very last thing Pharmee says in the entire campaign had no clip and played as blips.
> `VoiceCorpus.CodeLines()` is hand-enumerated, so an overload added later (W5.9) is simply
> missed — and the old pin only covered the GATE lines, not the corpus.
>
> **Fixed** and pinned: `voice: every corpus line has a clip (N unvoiced)` walks all of
> `CodeLines()`. It caught 3 the moment it was written.

## A vessel resting TIPPED pours itself forever — and sounds like it

> [!danger] Both Distilling Flasks sat at 90 deg, which broke Exp 7 *and* the audio
> `LiquidPourer.Update` decides you are pouring from
> `Vector3.Angle(Vector3.up, transform.up) > pourThreshold` (45 deg). A vessel whose RESTING
> pose is beyond that threshold therefore pours every drop put into it, every frame, forever.
> Two symptoms that looked completely unrelated:
> - **Exp 7 was unplayable.** `dry-cacl2` filled the DistillingFlask, the flask emptied
>   itself, and `dry-redistil` had nothing to distil. Caught by the drain probe:
>   `ClearContents ← PourOut ← LiquidPourer.PourTick ← LiquidPourer.Update`.
> - **"Pharmee sounds broken for every dialog"** (user, 2026-09-05). `LiquidPourer` keeps a
>   LOOPING pour AudioSource alive while it thinks it is pouring, so a continuous spilling
>   noise played under every line. Intermittent, because it only sounds while the flask
>   still has something to lose.
>
> ⛔ **Two traps here, and the first fix fell into both.**
> 1. **LOCAL vs WORLD.** The first attempt set `transform.localRotation = identity` and
>    verified the LOCAL value. `LiquidPourer` reads `transform.up`, which is WORLD. Measure
>    what the consumer measures.
> 2. **The BAKED HOME.** `DropRespawn._homeRot` still held the 90 deg pose, so
>    `ResetAllHome()` put the tilt straight back at the start of every run — the scene file
>    read 90 deg again a few hours later and it looked like the fix had been reverted by
>    hand. Straightening a prop means `SetHome(pos, rot)` too.
>
> Both are now pinned, measured exactly as `LiquidPourer` measures them:
> `pour: no vessel rests beyond its own pour threshold` and
> `pour: no pourable vessel's baked HOME is tipped`.
>
> 🔎 **The tool that found it:** `LiquidPhysics.DrainProbeName` — set it to a name prefix
> and any matching vessel logs a stack trace (once) the first time its contents are cleared.
> The autopilot arms it for `DistillingFlask` as a standing tripwire. "Who emptied this
> vessel?" is otherwise undebuggable: every reader sees the aftermath, none sees the caller.

## Let a play-mode sweep finish before touching the project

> [!warning] Asset writes during a run can hitch the editor
> Still good practice — a play session's verdict is only as stable as the editor it ran in —
> but note that this was NOT the cause of the chloroform failure above.

## An auto-rigged character has no coat to unparent

> [!danger] The lab coat is baked into the body mesh, so the fix is weights, not hierarchy
> Dr. Jimenez's coat swung with his arm. There is no coat object: he is a Tripo auto-rig
> with **one** `SkinnedMeshRenderer` over 41 bones and the coat baked into the body mesh.
> Nothing can be unparented, hidden or re-parented. The coat follows the arm because coat
> VERTICES carry weight on the arm bones — an auto-rigger assigns weight by proximity, so a
> panel hanging near the elbow picks up elbow weight even though a real coat hangs from the
> shoulders. `Fix Jimenez Coat Rig` finds that bleed geometrically and hands it to `Spine02`.
>
> Three traps inside that fix, each of which produced a *confidently wrong* result that
> still compiled, still passed every pure test, and had to be caught by looking at a render:
>
> 1. **The radius is in the MESH's own units, not metres.** This mesh is 0.5 units tall and
>    the prefab scales it to a 1.75 m man, so a "0.11 m" sleeve radius was half the
>    character's width: it classified the entire coat as sleeve and freed **43 vertices out
>    of 6992**. Derive it from `source.bounds.size.y` (`SleeveRadiusFor`, 7% of height) and
>    it survives a re-export at any scale.
> 2. **A LEAF bone has no segment, so distance to it is meaningless.** This rig has no
>    finger bones, which makes `L_Hand` a leaf: its "segment" collapses to a point at the
>    wrist, every fingertip measured as far away, the whole hand was reclassified as coat
>    and handed to the spine — his fingers rendered as **ribbons hanging off the wrists**.
>    Same shape for `L_UpperarmTwist02`/`L_ForearmTwist02`, also leaves, which tore the
>    **sleeve** clean off the arm. The hand is now excluded (a coat has no geometry past the
>    wrist) and a twist bone borrows its PARENT limb's segment (`IsTwistBone`).
> 3. **Take the FARTHEST child as the segment end, never the nearest.** Every twist bone sits
>    at EXACTLY its parent's position, so "nearest child" gave `L_Upperarm` a segment of
>    length **0.000** and "distance along the arm" silently became "distance from the
>    shoulder joint".
>
> 🔎 **Verify by BAKING, not by rendering a posed rig.** `SkinnedMeshRenderer` does not
> re-skin on a manual edit-mode `cam.Render()`, so the first before/after pair came back
> pixel-identical and looked like the fix had done nothing. `BakeMesh` forces the CPU skin;
> compare rest against posed and count how far vertices travel, bucketed by distance from
> the arm. That turns "does the coat still follow?" into a number: 745 coat vertices dragged
> (worst 65 cm) before, **0** after, sleeve unchanged.

## A "direction" conversion silently drops the parent's scale

> [!danger] `InverseTransformDirection` ignores scale; `InverseTransformVector` does not
> Pharmee's thruster rings hang off a model node scaled about **24x**. Converting world
> "down" into their local space with `InverseTransformDirection` returns a unit direction
> with the scale thrown away, so a `flowTravel` of 0.16 became **3.9 metres** of world
> movement and each exhaust puff shot through the floor. `InverseTransformVector` carries
> the scale, so the serialized number honestly means metres.
>
> The pure math could not see this — `Drop(phase, travel)` was correct at every step. Only
> the transform chain was wrong, so the pin has to live in the SCENE: move a ring by exactly
> what the drive loop would write, measure the world distance, put it back
> (`thruster: a puff falls a hand's width, not through the floor`).
>
> ⚠ Related, and it wasted three captures: **`RobotNPC.forward` is near-vertical** because
> the model carries a 90 deg axis fix. A camera placed with `fwd.y = 0; fwd.Normalize()`
> gets a ZERO vector, lands exactly on the target, and photographs the inside of her head.
> Frame from an explicit world offset, not from a character's forward.

## A silent NPC that looks perfectly fine

> [!danger] A narration channel needs THREE things, and only one of them was pinned
> Dr. Jimenez stopped speaking, and nothing looked wrong: his subtitle bubble appeared, the
> typewriter ran, his animator mouthed the words, and his AudioSource was present, unmuted,
> at volume 1. He was mute because `NPCNarrationController` needs **all three** of
> `narratorAudioSource`, `voiceBank` and `speaker`, and he had only the first.
>
> **How it broke.** `Wire NPC Polish` → `BuildJimenezBubble` opens with
> `DestroyImmediate(existing)` and rebuilds the whole `JimenezSubtitles` object. A fresh
> `NPCNarrationController` defaults to `speaker = VoiceSpeaker.Pharmee` with a NULL
> `voiceBank`, so every line resolved to no clip. `Import & Wire Voice Clips` is what
> normally assigns those two, inferring the speaker from a "jimenez" ancestor name — so
> the wiring only survives until the next builder run. W5.46 and W5.47 both ran that builder
> to bind Pharmee's glow, and he was silent from then on.
>
> ⛔ **The documented repair could not fix it.** The standing advice was
> `Voice ▸ Fix Voice Audibility + Music Ducking`, which restores the AudioSource and its
> spatial tuning — the SPEAKER, never the CLIPS. Following it exactly left him just as mute,
> which is why the AudioSource was present and correct while he still said nothing.
>
> **The fix is ownership**: the builder that DESTROYS a channel now rebuilds it completely
> (bank + speaker + audio source, via the extracted `VoiceAudioBuilder.EnsureVoiceChannel`),
> instead of leaving two of three fields to a menu somebody has to remember.
>
> 🔎 **The pin that missed it** asserted only `narratorAudioSource != null`. A partial pin on
> a multi-field contract reads as coverage and is worse than none, because it makes the next
> silent-NPC bug look impossible. All three fields are pinned now, including that a channel
> under a "jimenez" ancestor speaks AS Jimenez.
>
> ⚠ While hunting this, one suite run false-failed
> `narration: nobody holds the floor when nothing is speaking`. That is edit-mode residue,
> not a product bug: `StartCoroutine` runs synchronously up to its first `yield`, so a `Say()`
> triggered in edit mode sets `IsSpeaking`/`IsRevealing`, claims the static floor, and never
> resumes. `IsSpeaking` and `IsRevealing` are runtime auto-properties, so Play mode resets
> them; the suite's `ClearFloor()` clears it on the next run.

## Re-weighting a coat that shares a surface with the sleeve makes it WORSE

> [!danger] Measure the tearing, not the drag — they move in opposite directions
> Dr. Jimenez's coat follows his arm. The obvious fix, moving coat vertices off the arm
> bones onto the spine, was built, measured, shipped and then REVERTED, because the metric
> used to accept it only looked at half the problem.
>
> "Coat-body vertices dragged by the arm" went **745 → 0**, which read as a complete success.
> What that number cannot see is what happens at the BOUNDARY. Moving 100% of a vertex's
> weight while its neighbour keeps 100% makes two adjacent vertices follow different bones,
> and the edge between them rips:
>
> | | worst edge stretch | edges over 2x |
> |---|---|---|
> | untouched auto-rig | x4.2 | 32 |
> | hard re-weight | **x14.9** | **174** |
> | smooth falloff across a band | x4.2 | 44 |
>
> The smooth version removes the tearing — and then barely moves the coat either
> (1625 → 1557 dragged). That is the real finding: on this asset the coat and the sleeve are
> ONE CONTINUOUS SURFACE with no seam to cut along, so any weight change big enough to free
> the coat is big enough to rip it. The limit is the geometry, not the weighting. Fixing it
> properly means separating the coat as its own mesh in a DCC tool.
>
> ⚠ **An auto-rigger's weights are crude but CONTINUOUS.** Any correction has to stay
> continuous, so a per-vertex hard reassignment is almost always wrong.
>
> 🔎 **The metric to use is edge stretch**: bake the skin at rest and posed, and compare each
> triangle edge's length. A tear IS an over-stretched edge, and unlike "how far did the coat
> move" it cannot be gamed by making the deformation worse in a different way.

## Damping a humanoid clip: the knobs are muscles, and the reference is the mean

> [!warning] Two traps, one after the other
> Jimenez's clips are HUMANOID, so a scan for `m_LocalRotation` curves on bone paths finds
> **zero** arm curves. The animated values are muscle curves named in plain English
> ("Left Arm Down-Up", "LeftHandT.x"), normalised to -1..1 across each joint's range.
>
> Then: damping toward `keys[0]` does nothing if the clip OPENS at its widest reach, which
> this one does. The log dutifully reported "widest swing 1.04 → 0.57" while the rendered
> frame was pixel-for-pixel identical, because the frame being rendered was the reference
> being damped toward. Pull toward the curve's MEAN — the centre the gesture swings about —
> and the peak actually moves (hand height 1.372 → 1.318 m).

## A harness that samples once reports the default, not the truth

> [!warning] "dist 0.0 on all 9 modules" looked like a total regression and was a stale field
> `PlaytestAutopilot` sampled the tutorial ground path ONCE, on the first tick of each run.
> `GuidePath.Update` early-returns through `HideAll()` in six different cases (no camera yet,
> no target yet, still time-skipping, ...) and **`HideAll()` does not reset `LastDistance`** —
> so on a frame where Update had not yet reached the distance line, the report printed the
> field's default `0.0` and the harness dutifully explained it as "within the 2 m beacon
> handover". Nine modules, all reading 0.0, looked exactly like a catastrophic regression.
>
> Watching every tick instead showed the opposite: **9/9 modules draw, 11-16 chevrons over
> 7.0-8.7 m routes**. Nothing had ever been broken.
>
> ⛔ A single sample of a field that is only *sometimes* written measures the default. Either
> observe across the whole run, or reset the field when you stop writing it — preferably both.
> This harness has now made the same class of mistake twice (W5.44b: a stale route made a
> 6.1 m target read as "inside the handover").

## A verification contract written from a stale doc verifies the doc, not the game

> [!danger] The sweep's only FAIL in W5.50 was my own contract, and no player could ever see it
> `experiments-reference.md` said methane's `setup-apparatus` step was "Clamp the tube, fit the
> cork + delivery tube into the water trough". So the new visual contract counted
> `ApparatusSnap` attachments and failed the step: "the rig is not assembled — 0 snapped
> parts". The LIVE asset says "Scoop the ground mixture from the mortar into the Hard-glass
> tube" — a solid delivery — and `MethaneApparatusRig` never uses `ApparatusSnap` at all. The
> count was 0 forever, by construction.
>
> The vault's task table had been generated once at W5.11 and hand-patched since; the
> "clamp"/"splint" hints it still carried were removed from the asset in July. A cold session
> trusts the vault and does not re-verify — which is exactly what I did.
>
> ⛔ **Read the asset, not the note about the asset, before writing anything that judges it.**
> The contract is now `Expect.Powder` (what the step DOES), and the tables regenerate from the
> live assets so this class of drift cannot recur.

## A generator must VERIFY hand-curated tables, never overwrite them

> [!warning] Six of the nine module tables were not the table I thought they were
> Regenerating `experiments-reference.md` matched only 3 of 9 task tables. The other six use
> hand-curated variants — `| # | task | phase | label | prereq | how it completes |` and
> `... | manual ref | apparatus the step needs |` — whose columns hold knowledge the assets do
> not (how a step completes, which apparatus it needs). Overwriting them with the generic
> hint table would have destroyed that.
>
> Tables are therefore located by their HEADER-ROW SIGNATURE, never by the heading above
> them (the headings vary, one module had a duplicate, one had none). Canonical tables are
> regenerated; curated ones are read back and their `task` column compared to the live
> asset's taskIds in order — drift is REPORTED ("N curated table(s) adrift"), never repaired.
> Result on first run: 3 regenerated, 9 quizzes regenerated, 0 curated tables adrift.

## Transient evidence sampled once at capture measures its default (third time)

> [!danger] The flame contract failed a step the game had just judged correctly
> `chloroform:test-flammability` completed — the popup read "Won't ignite — NON-FLAMMABLE ✓",
> which `VesselFlameTask` can only produce with a lit match within 0.2 m. The new visual
> contract then reported "flammability test with NO lit flame anywhere in the scene". Both
> were true: the contract sampled `IsLit` at the FINAL capture, after the step had completed
> and `DropRespawn` had extinguished the match. The evidence was real and gone.
>
> This harness has now made the same mistake three times: a stale route (W5.44b), a
> once-sampled `LastDistance` (W5.49), a once-sampled flame (W5.50). The shape is always
> identical — **a fact that is only sometimes true, read at a moment it happens to be false.**
>
> ⛔ Latch transient evidence at the moment it is provably present and carry the latch to the
> verdict. `VisualSweep.MidVerb("flame")` fires while the match is at the sample, so it now
> calls `NoteFlame(AnyFlameLit())`; `SceneCues` folds the latch into the observation; `Record`
> clears it after judging so one step's flame never vouches for the next.

## A simulator branch that skips the real verb can only prove the shortcut

> [!warning] The methane tube "drew nothing" — because the harness never scooped
> Once `methane:setup-apparatus` had a real contract (solid delivered → mound visible), the
> sweep failed it: 4 g of Sodium Acetate in `Prop_glass-tube`, no mound, no column, nothing.
> It looked like the invisible-solid class of bug W5.45b fixed. It was not. A player scoops
> the ground mixture in by hand — `ScoopController.Dip` → `Deposit` → `EnsurePowderVisual` —
> and always gets the mound. `SimulatedRun`'s methane branch predated W5.45's "solids go
> through the real scoopula" rule and moved the volume with a bare `PourOut`/`AddLiquid`,
> which builds no visual. The harness was the one skipping the verb.
>
> ⛔ **A sim path that bypasses the player's verb cannot verify what the player sees**, and
> its FAIL is about the harness. Every solid step now goes through the scoopula in Play mode;
> the edit-mode fallback (kept so the `simrun:` pins do not move) mirrors what `Deposit`
> would have drawn. Before blaming the game for a sweep FAIL, confirm the harness reached the
> vessel the way a hand would.

## A half-applied play-mode switch is worse than none

> [!danger] Headset Mode fixed only the OPEN scenes, and the game boots into the other one
> In the headset the controllers floated ABOVE the player's head. The instinct is to raise the
> POV — and that cannot work: the camera and both controllers are children of the same
> **Camera Offset**, so lifting it moves them together and the gap is unchanged.
>
> `[HeightDebug] scene=MainMenu root=0.00 offset=1.45 head=0.00 eyeWorld=1.45 calibrated=False`
>
> **`head=0.00` is the whole story.** The HMD pose never reached the camera, so
> `SeatedHeightBoost` computed `FixedOffset(target 1.45, head 0.00) = 1.45` and planted a
> STATIC eye, while the real Quest controllers kept reporting true tracked poses — hence
> controllers a metre above a frozen viewpoint.
>
> The cause: `Play Mode ▸ Headset Mode` walked `SceneManager`, i.e. the **open** scenes only.
> Run with just SampleScene open, it disabled that simulator and left **MainMenu's ACTIVE** —
> and `PlayFromMenuBootstrap` makes Play always boot MainMenu. The scene files proved it:
> SampleScene `m_IsActive: 0`, MainMenu `m_IsActive: 1`. The switch now walks every scene in
> **Build Settings**, opening closed ones additively, and reports its state per scene.
>
> ⛔ Any editor switch that configures "the scene" must define WHICH scenes, or it silently
> half-applies — and the half left wrong is the one the game starts in. Pinned by
> `playmode: every build scene agrees about the XR Device Simulator`, which reads the scene
> FILES as text so the suite never has to open a scene to answer it.
>
> ⚠ Related: `SeatedHeightBoost` DISCARDS the player's real height by design ("everyone has
> the same view") and re-plants the eye on the scene's authored `targetEyeHeight`. Both scenes
> authored **1.45 m** — the eye line of a ~1.55 m person, which reads as a too-tall world when
> standing. Raised to 1.65 m (user 2026-09-05).

## A custom shader with no stereo macros is invisible in one eye

> [!danger] Nothing in the editor shows it — the Game view renders a single eye
> The tutorial arrows and the highlight glow appeared in the LEFT EYE ONLY, and the user
> played with one eye shut until it made them dizzy. The project runs **Single Pass
> Instanced** (`OpenXR Package Settings … m_renderMode: 1`): both eyes render into one
> texture array and the shader picks its slice from the instance id. A shader that omits the
> macros silently draws to eye index 0.
>
> `PharmaGuide.shader` and `PharmaGlow.shader` had **zero** of them; `PharmaLiquid` had six
> and `Glass` is a surface shader (Unity generates the handling), which is exactly why the
> glassware and liquids looked perfect and only the guidance was broken.
>
> Needed in every hand-written pass: `UNITY_VERTEX_INPUT_INSTANCE_ID` in the vertex INPUT and
> the OUTPUT struct, `UNITY_VERTEX_OUTPUT_STEREO` in the output,
> `UNITY_SETUP_INSTANCE_ID` + `UNITY_TRANSFER_INSTANCE_ID` +
> `UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO` in the vertex stage,
> `UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX` in the fragment stage, and
> `#pragma multi_compile_instancing` in EACH pass (a shared HLSLINCLUDE cannot carry a pragma).
>
> ⛔ This is invisible to every check the project has except a headset — the suite, the
> simulators and DevCapture all render one eye. Pinned as
> `xr: every custom shader declares the stereo macros`, which reads the `.shader` files as
> text. Surface shaders are exempt by construction, so the pin checks the MACROS and not the
> pragma.

## A fixed role on a bench object is guidance a VR player cannot follow

> [!danger] "Pour the ethanol into tube 0" assumes the player uses tube 0
> Exp 2 authors five enol tubes, four alkaline-oxidation tubes and four acidic ones, each
> pinned to a SPECIFIC bench tube. In VR the player grabs whichever tube is nearest, so
> pouring the correct reagent into the wrong-NUMBERED tube was graded a wrong-reagent
> mistake even though the chemistry was right. The player is not wrong; the binding is.
>
> Rack members are interchangeable glassware, so the role now follows the pour
> (`VesselRoleMatch` + `RackRoles`). Three things make it work:
>
> 1. **Claiming is DEFERRED.** The four alkaline tubes all take KMnO₄ then NaOH and differ
>    only at the third reagent. Claiming on pour one would assign a role arbitrarily and then
>    punish a perfectly correct third pour. A tube holds a CANDIDATE SET that each pour
>    narrows; ambiguity is a normal state, not an error.
> 2. **Wrong = the set EMPTIES.** That is the only wrong-reagent case, and it still fires:
>    ethanol into an oxidation tube is still graded.
> 3. **Roles are claimed EXCLUSIVELY** through a shared `RackRoles` ledger, or two tubes
>    narrow to the same role and the rack completes with one still empty.
>
> ⛔ **Do not advertise the union of the survivors.** The first cut exposed every surviving
> role through `ExpectedSteps`, so each enol tube advertised all five alcohols — and
> everything that reads that list to decide WHERE a reagent goes believed it. The player-path
> simulator poured all five alcohols into all five tubes and logged **144 mistakes on a
> perfect run**. Acceptance must be permissive; the ADVERTISED step must stay one
> deterministic answer (the claimed role, else the authored one).

## A vessel label that answers a question the player did not ask

> [!warning] `AvailableTasks()` is every unlocked task, not the one being guided
> `VesselStatus.NeedLineNow()` walked the whole `AvailableTasks()` list, so with Exp 2's prep
> steps running in parallel the alkaline tubes advertised "Potassium Permanganate 0 / 2 ml"
> while the player was still on the ethanol step — and a requirement printed on a tube reads
> as an instruction. That is what sent the player to the wrong tube in the headset.
>
> Its own doc comment already claimed it showed the step only "when it is the vessel currently
> being asked for"; the code did something else. `GuidePath` had already settled the
> convention — the FIRST entry of `AvailableTasks()` is the guided one — so the fix was to
> reuse it rather than invent a second notion of "the current step".

## An anchor on the wrong tube is worse than a scold

> [!danger] Pooling the pour bindings alone would ACCEPT the pour and then strand the step
> Twelve of the 24 standalone tubes carry a non-pour anchor bolted to the bench object at build
> time — `heatToC`, `litmusTaskId`, `chillToC`, `weighTaskId`, `vaporTaskId`. Let any tube take
> the role but leave the anchor on the authored tube, and the player's pour is accepted, the
> label says "claimed", and the litmus strip or the water bath then completes nothing, because
> `VesselLitmusTask` lives on a tube they never touched. That is a stuck player with no
> mistake logged — worse than the scold it replaced.
>
> The anchor must follow the claim. `LiquidTaskBinding.RoleClaimed` fires exactly once, after
> the active steps are rebuilt, and the builder's extracted `AttachAnchors` attaches the
> claimed role's anchors to the tube in hand. Safe because every anchor gates on
> `binding.ReadyFor(taskId)`: one attached to a tube that never claims that role is inert, and
> `ClearBenchBindings` strips all of them on the next build.
>
> ⛔ **The first pour into a free tube was scolded** even though the candidate set accepted
> it: an ambiguous pool extra advertises NO steps (so the simulator never targets it), and the
> legacy step lookup after acceptance found nothing and recorded "unexpected reagent". Found on
> the real Exp 6 / Exp 8 stages. Fix: count the accepted pour against the first surviving
> candidate's step, and carry volume across the narrowing **by reagent**, never by task id —
> Exp 6 names the same acetone `test-tollens` in one role and `test-schiff` in another.

## Hard glass is a naming convention, and every hint is a voice clip

> [!warning] Two facts that constrain any tube or wording change
> **All 23 bench tubes share ONE LabItem `itemId` (`kit-testtube`), hard-glass included.** The
> only family signal is the scene-name prefix (`Kit_Hard-GlassTestTube_*` vs `Kit_TestTube_*`),
> so `VesselRoleMatch.FamilyOf` is a naming convention and is pinned. A soft tube must never
> claim Exp 6's naked-flame role — the hard glass IS the point of that step.
>
> **`PharmeeBrain.InstructionFor` speaks the text after "→" in every hint, and
> `VoiceManifestExporter` exports exactly that**, so every rewritten action line invalidates a
> clip. Twenty-one hints named a tube number ("Test Tube 15: 5 spatula dips…") — with tubes
> claiming their own role that is a voice line insisting on a tube the player is not holding.
> Rewritten to name the family ("In a clean test tube: …"); 21 clips regenerated at
> `eleven_flash_v2_5` = **0.5 credits/char, 1,328 credits** (the first estimate assumed 1/char).
> The generator is incremental (`Test-Path` before every API call) — but its `-WhatIf` lists
> the whole selection WITHOUT that check, so a dry run quoting 10,944 credits is not the spend.
> Pinned: `hint: no action line names a numbered tube` ("the second rack" is allowed: a rack is
> a real, distinct object). 48 orphaned clips (27 from earlier copy edits) were moved to the
> off-repo backup, not deleted; the bank is 326 = the manifest.

## A role claimed by construction is a role nobody can take

> [!danger] `SetRoles` used to claim whenever exactly one candidate survived — including at build
> The W5.52/W5.53 rule "claim when one candidate is left" is right after a POUR. Applied at
> `SetRoles`, with nothing poured, a family with ONE role (Exp 9's limewater tube, Acetanilide's
> hydrolysis tube, Exp 6's hard-glass acetate tube) claimed it on every member as they were
> wired: the authored member won the shared ledger, every extra then saw the role as taken,
> its candidate set was EMPTY from birth, and any pour into it was scolded before the player
> had done anything. The W5.53 "acetone into hard glass is scolded" test passed — for the
> wrong reason: not because families do not mix, but because the only hard-glass role was
> already spoken for.
>
> A role is claimed by a pour, or by a delivery controller that is the disambiguator by
> definition (`ClaimForTask`). Never by construction. Found by tracing the vapor path, not by
> a test — the pin that should have caught it (single-role family, extra accepts the first
> pour) exists now.

## A derived map built once goes stale the moment the truth moves — and "expects" must include "could claim"

> [!warning] Two ways a guide kept pointing at the authored object after the player chose another
> **The target map.** `TutorialTargets.Build()` sweeps the live components once per run. An
> anchor attached on a claim is therefore never registered, and the authored object — still
> carrying its inert twin — stays the heat/litmus/weigh step's target. Rebuild the derived map
> when the truth changes (`RoleClaimed` → `Clear` + `Build`), and remove the twin
> (`DetachAnchors`) so it cannot register at all. A derived map is cheap; a stale one is a lie.
>
> **The receiver.** `VaporCollectController` delivered only to vessels whose binding ADVERTISES
> the product. An unclaimed pool extra advertises nothing, and nothing is poured into a
> receiver first, so "hold a clean test tube at its mouth" — the W5.53 hint rewrite — promised
> what the mechanic did not deliver: only the authored tube ever collected. "Expects" now
> includes "could still claim the role that serves this step", and the delivery itself claims it.
>
> **And then the sweep found the mirror image.** With candidates accepted by PROXIMITY, a spare
> beaker resting near the bath claimed Exp 7's collect role during the distil-to-collect
> transition, before the player had picked anything up — and the authored twin, whose role was
> now taken, still ADVERTISED it ("claimed role, else authored"), so the sim's receiver and the
> stream both believed it: 32 ml ran into the twin, every drop was scolded as an unexpected
> reagent, the flask drained and the step never completed (module stopped at 5/13). A candidate
> counts only in the HAND (`VaporMath.MayReceive`), the hand beats a nearer bench spare, and a
> twin whose role another member holds advertises NOTHING. Under both halves: an advertised step
> is a promise the whole system acts on — never advertise what the vessel can no longer accept.
>
> Also from this batch: the ghost demo (`VerbDemoPlayer.Endpoints`) took the FIRST destination
> in the list, so with pool targets registered it mimed at whichever extra was listed first
> while the glow lit the held-or-nearest one — the W5.44 "two cues must never disagree" rule,
> broken by W5.53's own registration. One chooser for every cue now.

## A static-batched renderer ignores its own transform

> [!danger] The lab door "never opened" — and every line of the door code was correct
> `DoorOpener` resolved its leaf, the gatekeeper's `doorOpener` was assigned, and
> `SetOpen(true)` fired on DoorArmed. The leaf still never moved on screen, and the player
> walked straight through what looked like a closed door.
>
> The leaf inherited the Environment prefab's `m_StaticEditorFlags = 87`, which includes
> **Batching Static**. Unity merges a batching-static renderer into a combined mesh at load;
> the transform still moves, and the COLLIDER moves with it, but the drawn geometry does
> not. So the doorway physically opened while the picture stayed shut — which is exactly
> what "it is not doing that open function, I'm just walking through it" describes.
>
> Anything that MOVES must not be batching-static, and must not contribute GI either (a
> moving object carrying a lightmap baked in one pose is the next bug). `Fix Lab Door
> (swing + blocker)` clears the flags and builds the blocker; re-bake probes, lighting and
> navmesh afterwards. When a transform visibly does nothing, check the static flags before
> reading another line of the script that sets it.

## "Any tube will do" has to mean any tube for the step you are ON

> [!warning] Pooling every role onto every tube made a future step's reagent legal today
> W5.55 merged the closed rack groups into one family pool, which is what the player needs
> — but acceptance asked only "does SOME surviving role want this reagent". Exp 2 wants
> methanol much later, for the methyl-salicylate ester tube, so methanol poured into a tube
> during step one was silently accepted, narrowed that tube to the ester role, and the
> ethanol the player poured next stopped counting. No mistake was recorded and the step
> could not be finished: the worst of both worlds.
>
> `LiquidTaskBinding.Blocked()` now rules out any role whose tasks are complete or not yet
> available, and acceptance, the accepted-pour step lookup and the guidance list all use it.
> The role a vessel has ALREADY claimed is never blocked, or a tube would lose its own role
> the moment its prep task completed. Found by `Simulate Everything`'s imperfect-play probe,
> not by a unit test — the pure rule was right in isolation and wrong in a run.

## A per-tick penalty is a per-frame penalty

> [!danger] 1025 mistakes from about a dozen misaimed pours
> `LiquidTaskBinding.Handle` runs on every `LiquidAdded` event, and a tilt-pour raises one
> per frame. Each refused tick recorded its own `WrongReagent` mistake, so a headset session
> reached 1025 mistakes and an unrecoverable grade before the player worked out what was
> wrong. Anything that grades inside a per-frame handler needs a latch: the FEEDBACK should
> fire every time (the player has to see it), the RECORD once. Latched per reagent and
> cleared when the vessel is emptied, so a repeat offence still counts.
>
> The same shape bit `VaporCollectController` in W5.54 and the wrong-reagent scold here; when
> you add a new grading call, ask what raises it and how often.

## A simulator resets every item to its baked home, so re-homing afterwards bakes the revert

> [!danger] Two distilling flasks lost a hand placement twice, silently
> Every simulator (`Simulate Everything`, the VISUAL sweep) calls
> `DropRespawn.ResetAllHome()`, which teleports all 120 items back to their BAKED homes.
> Run `Re-Home Scene Items (Adopt Current)` or `Lock My Layout` after that and it adopts the
> pose the reset just restored, writes it to disk, and the user's placement is gone from both
> the transform and the home. Nothing is logged, and nothing in the suite could see it: the
> result is perfectly self-consistent, just not what anyone chose.
>
> Safe order is **reopen the scene (discarding), then re-home, then save**. Prefer
> `Re-Home MOVED Items Only`, which adopts a transform only where it actually differs from
> the baked home — an item a reset has just restored is by definition not different, so it
> cannot re-bake a revert.

## A mesh that is not on its own pivot cannot be placed by anyone

> [!danger] The flask you drag is not the flask you see
> Both distilling flasks are USD imports whose visible glass, liquid and spout sat **71 cm**
> from the object's pivot and grab collider (49 cm sideways, 51 cm up). Dragging the object
> in the Scene view moves the pivot; the glass keeps its offset and hangs in the air wherever
> you put it. The user re-placed them by hand twice and watched them float both times, which
> is not a placement mistake — it was unplaceable. It also means the player reaches for the
> glass in VR and grabs nothing, because the collider is half a metre away.
>
> ⛔ **The PIVOT has to be on the mesh, not just the collider** (corrected W5.57). Wrapping
> the collider fixed grabbing, but the runtime reads `transform.position`: `Matchstick`
> measures its strike distance from it, `VaporCollectController` its 0.5 m receiver radius,
> `DropRespawn` its home. A grabbed match whose pivot sat 1.9 m from the stick was "held"
> 1.9 m from the hand and could light nothing in VR. The repair is RE-PIVOT: root to the
> solid-mesh centre, every direct child shifted the opposite way, so the mesh, liquid,
> spout, flame anchor, chill zone and label never move in world space; then the BoxCollider
> is wrapped around the mesh and the home re-baked. Never drag the mesh onto the pivot — the
> first attempt did, and teleported both flasks off the worktop the user had just chosen.
>
> A Ctrl+Z undoes it like any edit, which is how it was lost once. The `grab:` suite pins are
> what make it stick: drift is a red line, and one run of `Fit Glassware (upright · pivot ·
> seat)` restores it. The sweep that found all four (`pivot outside its own SolidWorldBounds`
> over every `LabItem`) is now that pin. `TryTripo` no longer bakes the offset in: it centres
> the model's mesh on the root and sizes the grab box from the mesh, so a cabinet rebuild
> cannot recreate the 1 mm matchstick collider.
>
> Measure it with `ExperimentSceneBuilder.SolidWorldBounds` against the collider bounds. Every
> other vessel in the lab measures under 3.5 cm; `Stand Tipped Glassware Up` repairs anything
> past 5 cm, then seats the base on the surface below and re-bakes the home. Pinned by
> `pour: no pourable vessel floats above the surface under it`.

## `GameObjectUtility.SetStaticEditorFlags` does not survive a reload on a prefab instance

> [!danger] The door fix was applied, logged success, saved — and was gone the next session
> `Fix Lab Door` cleared the leaf's Batching Static flag with
> `GameObjectUtility.SetStaticEditorFlags(go, 0)`. That changes the LIVE object, so every
> readback in the same session agreed, and the menu logged "87 → 0". But the leaf is a child
> of the Environment prefab instance, and that API records no prefab-instance OVERRIDE, so
> the value reverted to the prefab's 87 on the next load. The door went on being merged into
> the static batch: its collider swung open while the drawn leaf stayed shut, and the player
> reported "the door is not opening" a second time, after being told it was fixed.
>
> Write serialized values on a prefab instance through `SerializedObject` +
> `ApplyModifiedProperties`, then `PrefabUtility.RecordPrefabInstancePropertyModifications`.
> Verify by reading back `PrefabUtility.GetPropertyModifications` and finding the property in
> the list — not by re-reading the live object, which will happily agree either way.
>
> Same family as the flask children a Ctrl+Z reverted: **a scene edit that leaves no recorded
> override is not saved, however convincingly it reads back.**

## A face that changes colour on every line reads as flashing, however dim it is

> [!warning] Two batches capped the BRIGHTNESS; the flashing was the CHANGE
> W5.47 dimmed Pharmee's panels and hull, W5.49 capped the face palette under the bloom
> threshold, and the user still reported flashing while she spoke. The cause was not
> intensity: the gate sets `Neutral` as each line starts and `PharmeeMood` resets to the
> default `Happy` as it ends, so a teal-to-blue STEP ran on every sentence. Making the two
> colours identical removes it with no logic change at all — `SetExpression`, `PharmeeMood`
> and `ExpressionForGate` are untouched, and `Warning` keeps its amber because it fires only
> on a real mistake and is meant to be noticed.

## A save after a suite run bakes what the suite drained — and the suite drains scene data

> [!danger] The methane tutorial starved for an afternoon, and five wash bottles went dry, from two saves
> The suite's `simrun:` pins and every simulator run the modules in EDIT mode on the live
> scene. The methane tutorial's charge jar (`Prop_reagent-jar`, an added `LiquidPhysics` on a
> hand-staged prop) is scene DATA — nothing in code charges it — and the simulated
> `setup-apparatus` pours it dry; the wash bottles pour dry the same way. Two W5.58/W5.59 scene
> saves made straight after a suite run wrote 0 ml into all six, the user committed it, and
> `Simulate Everything` failed methane on a bench that looked untouched. The scene-file diff of
> prefab OVERRIDES showed nothing, because an added component is its own block, not an
> override — which is why the search took hours.
>
> Rules: **reload the scene from disk after ANY suite or simulator run, before ANY save**;
> never diff scene contents through `m_Modifications` alone; and pin scene data the sims
> consume (`bench: the methane charge jar holds its mixture`, `bench: the wash bottles hold
> distilled water`), because no simulator can see it — they all drive the APIs.

## "Apparatus" is what the builders marked, never what a name suggests

> [!warning] The first lab reset emptied the wash bottles and the tutorial's charge jar
> `LabReset` began with "apparatus = anything not named `Raw_`/`Reagent_`". Nine vessels the
> builders never treated as used matched it — the wash bottles, the mortar, the methane charge
> jar and glass tube — and the reset drained sources the player needs full. The codebase
> already has the definition: every builder adds `CleanableVessel` to a vessel the player
> fills and to nothing else. `LabReset.IsUsedVessel` reads that, in both directions (reset
> empties used vessels; refill restocks the rest). A fixture pin could not catch this — a
> fixture vessel is whatever the pin says it is — so the rule is pinned against the real
> scene objects.

## Launch Play-mode sweeps from the menu tool, never from inside a RunCommand

> [!warning] Two VISUAL sweeps ended "play mode ended early" with no fault anywhere
> Both were started by `EditorApplication.ExecuteMenuItem(...)` inside a `Unity_RunCommand`
> script; Play then ended by itself after 130 s and 220 s, mid-module, with no exception of
> ours, no autopilot stop and nothing in the log. The same sweep launched through
> `Unity_ManageMenuItem` ran all nine modules. Whatever the command runner does when its
> execution record is finalised, it does not survive a Play session that began inside it.
> Reload the scene in a RunCommand if you must, return, then launch Play from the menu tool.
