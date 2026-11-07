# PharmaSynth — Build & Run Guide

How to run, test, and build **PharmaSynth**, the Meta Quest 3 VR chemistry-lab education game (Unity 6000.5.2f1, URP, OpenXR + XRI). This guide covers day-to-day in-editor iteration (XR Device Simulator + keyboard fallback), the headless regression suite that gates every change, scene/build wiring, the Quest 3 Android build configuration, the on-device performance budget, and the tooling gotchas specific to this workstation. Written 2026-07-08 on branch `main` (all W1–W5 work committed; `feature/asset-intake` is a stale stub at the planning commit — do not use it); verified state at last doc refresh (2026-07-12, W5.11): self-tests **969/969 green**, console zero-error.

## Table of contents

1. [Prerequisites](#1-prerequisites)
2. [Running in-editor](#2-running-in-editor)
3. [The regression suite](#3-the-regression-suite)
4. [Scenes & Build Settings](#4-scenes--build-settings)
5. [Quest 3 build](#5-quest-3-build)
6. [Performance budget & device-day validation](#6-performance-budget--device-day-validation)
7. [Known machine/tooling gotchas](#7-known-machinetooling-gotchas)

---

## 1. Prerequisites

| Requirement | Version | Notes |
|---|---|---|
| Unity Editor | **6000.5.2f1** | URP template project. Open via Unity Hub. |
| Android Build Support | installed | SDK/NDK/OpenJDK modules via Hub (already installed on this workstation). The project's **active build target is Android**. |
| Universal Render Pipeline | 17.5.0 | `com.unity.render-pipelines.universal` |
| XR Interaction Toolkit | 3.5.1 | `com.unity.xr.interaction.toolkit` — Starter Assets + **XR Device Simulator** samples imported under `Assets/Samples/XR Interaction Toolkit/3.5.1/`. |
| OpenXR Plugin | 1.17.1 | `com.unity.xr.openxr` — the **sole** XR loader for both Standalone and Android. |
| Unity OpenXR: Meta | 2.5.0 | `com.unity.xr.meta-openxr` — Quest-specific features (foveated rendering etc.); pulls AR Foundation. |
| Input System | 1.19.0 | `com.unity.inputsystem` — all input (simulator, wrist-watch gesture, dev driver) goes through it. |
| TextMeshPro | via `com.unity.ugui` 2.5.0 | TMP ships inside uGUI in Unity 6; TMP Essential Resources already imported. |
| Timeline | 1.8.12 | Cutscene staging (no Cinemachine — deliberate; the XR camera is never animated). |

Everything above is **already configured in the repo** — a fresh clone + open in 6000.5.2f1 should compile with zero errors and need no package work. No Quest 3 headset is required for editor work (see §2); the headset is only needed for §5–§6.

> **Do not add** `com.unity.xr.oculus` (deprecated Oculus XR Plugin) — it was deliberately removed. Exactly one XR loader (OpenXR) must stay active per target.

## 2. Running in-editor

1. Open **`Assets/Scenes/SampleScene.unity`** (the assembled lab).
2. Press **Play**. Two ways to run: (a) **headless** — the XR Device Simulator + keyboard stand in for the headset (keep Standalone `InitManagerOnStart` OFF); (b) **headset** — connect a Quest via Quest Link / Air Link and turn Standalone `InitManagerOnStart` ON. Switch with **Tools ▸ PharmaSynth ▸ Play Mode ▸ PC Dev Mode / Headset Mode (Quest Link)** (checkmark = active). Each sets the init flag AND the XR Device Simulator in every build scene; with the simulator left on, it removes the real headset and the hands float above the view.
3. Walk to the **Begin button**, don PPE at the **PPELocker**, and run the Methane tutorial — or skip all interaction with the keyboard driver below.

You can also start from `Assets/Scenes/MainMenu.unity` to exercise the full menu → lab flow (§4).

### XR Device Simulator basics

The simulator shows its own on-screen help overlay in Play mode. The bindings that matter (from `Assets/Samples/XR Interaction Toolkit/3.5.1/XR Device Simulator/*.inputactions`):

| Input | Action |
|---|---|
| **W/A/S/D** (+ **Q/E** down/up) | Translate the currently-manipulated device — the **HMD by default** |
| **Right mouse (hold) + mouse** | Look around (manipulate head) |
| **Tab** | Cycle which device is manipulated (HMD / left / right controller); **Esc** stops manipulation |
| **T** / **Y** | Toggle manipulating the **left** / **right** controller; hold **Left Shift** / **Space** for momentary |
| **G** | Grip (grab/release) on the manipulated controller |
| **Left mouse** | Trigger on the manipulated controller |
| **B** / **N** | Primary / secondary controller button |
| **W/A/S/D** *while manipulating a controller* | That controller's **thumbstick** (drives XRI locomotion) |

> **Collision caveat:** WASD in HMD mode **teleports the simulated headset directly and bypasses all collision** — you can fly through benches and walls. **Thumbstick locomotion** (WASD while manipulating a controller) goes through the XRI move provider + CharacterController and **respects collision**. Use thumbstick locomotion when validating clearances, aisle widths, or anything collision-related.

> **Key-conflict tip:** while a controller is being manipulated, **B** and the digit keys are also simulator bindings (primary button, axis-target toggles). Press **Esc** first when you want the DevExperimentDriver keys below.

### DevExperimentDriver keyboard fallback

`DevExperimentDriver` (on the `ExperimentSystems` GameObject; `Assets/PharmaSynth/Scripts/Interaction/DevExperimentDriver.cs`) drives the whole experiment loop without any XR interaction — HUD, tablet, Pharmee, cutscenes, and grade screen all react. Editor-only unless `enableInBuild` is set.

| Key | Action |
|---|---|
| **B** | Begin / restart the experiment (`ExperimentStarter.Begin()`) |
| **1–5** | Complete step N of the current task graph |
| **F** | Finish → logs grade % and pass/fail, grade screen appears |
| **R** | Retry (rebuilds the run) |

### Hands-on flow (grab → station)

The real interaction path for Methane: **grab a prop off the `ReagentBench` (`MethaneProps`) and carry it into its matching station's trigger zone.** Each prop carries a `LabItem.itemId`; each `ExperimentTaskStation` has a `requiredItemId` and only accepts the exact right prop (`AcceptsItem()`):

| Prop (`itemId`) | Station / task |
|---|---|
| `reagent-jar` | PrepareMixture |
| `glass-tube` | SetupApparatus |
| `burner` | HeatMixture |
| `collection-tube` | CollectGas |
| `lit-splint` | TestGas |

Order is enforced by the runner: a wrong prop is ignored; the right prop out of order records a **WrongStep** mistake. In the simulator: manipulate a controller (T/Y), hover the prop, **G** to grab, walk it into the zone.

## 3. The regression suite

**Menu: `Tools ▸ PharmaSynth ▸ Run Self-Tests`** — runner in `Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs`, suites in `PharmaSelfTests.<Area>.cs` (Experiment, Progression, Chemistry, Interaction, NPC, UI, Scene). Runs the whole suite entirely in edit mode (no Play button, no headset) across 70+ suites incl.: TaskGraph, Mastery (BKT), Score, Grader, Runner, Progression, ChemVisual, UI, ReactionRules (cutscenes/crystallization/filtration/hazards), Interaction, ProgressionFlow, Library, and Content (loads and solves **all 9 experiment modules** to 100%).

- **Green:** one console line — `PharmaSynth Self-Tests: N/N passed — ALL GREEN`, also written to `Logs/selftest-result.txt` (read that rather than wrapping the run in a capture script).
- **Red:** an error log listing every failed assertion by name.

**The gate is two-part: all assertions pass AND the console is otherwise zero-error.** Run it:

- after any change to `Assets/PharmaSynth/Scripts/` (engine, scoring, progression, chemistry, UI, NPC, interaction, safety),
- after editing any `ScriptableObjects/Experiments/*.asset` module or the `ExperimentLibrary.asset`,
- after package/XR settings changes and before any commit,
- as the first sanity check when the editor recovers from a crash or a big reimport.

It costs seconds and needs no play mode, so there is no reason to skip it.

## 4. Scenes & Build Settings

Two scenes are in Build Settings (`ProjectSettings/EditorBuildSettings.asset`), both enabled:

| Index | Scene | Role |
|---|---|---|
| 0 | `Assets/Scenes/MainMenu.unity` | Main menu — Tutorial / Laboratory / Settings / Quit |
| 1 | `Assets/Scenes/SampleScene.unity` | The laboratory (all 9 experiments run here; experiments are data, not scenes) |

**Menu → lab wiring:** `MainMenuController` (`Scripts/UI/MainMenuController.cs`) writes the chosen experiment id into the static **`GameFlow.SelectedModuleId`** (`Scripts/Progression/GameFlow.cs`; defaults to `"tutorial-methane"`) and calls `SceneManager.LoadScene("SampleScene")`. *Tutorial* selects the tutorial; *Laboratory* selects the player's next unlocked-but-unpassed experiment via `ProgressionFlow.NextExperiment()` (fallback: tutorial). In the lab scene, **`ExperimentLauncher`** (`Scripts/Interaction/ExperimentLauncher.cs`) reads `GameFlow.SelectedModuleId` (with `launchSelectedOnStart`), resolves the module asset through **`ExperimentLibrary.asset`** (`ScriptableObjects/ExperimentLibrary.asset`, runtime-safe refs to all 9 modules), swaps it into the `ExperimentRunner`, raises `onModuleLoaded` so scene wiring can rebuild, and starts a fresh attempt. Cross-scene state is deliberately tiny — persistent progress lives in `ProgressionService`'s JSON save, not in `GameFlow`.

## 5. Quest 3 build

The **active build target is already Android** — no target switch needed. Configuration already in place:

**Player settings** (`ProjectSettings/ProjectSettings.asset`):
- Graphics APIs (Android): **Vulkan first, OpenGLES3 fallback** (auto-graphics off)
- Scripting backend: **IL2CPP**; target architectures: **ARM64 only**
- Texture compression: **ASTC** (Android build setting; verify in the Build Settings/Build Profiles window — it lives in the binary `Library/EditorUserBuildSettings.asset`)

**OpenXR features (Android)** — Project Settings ▸ XR Plug-in Management ▸ OpenXR ▸ Android tab:
- **Meta Quest Support** (feature group; Quest 3 in target devices)
- **Meta Quest Touch Plus** + **Oculus Touch** controller profiles
- **Hand Interaction Profile**
- **Foveated Rendering**

> ⚠️ **Verify the feature toggles before the first device build.** The session log records enabling all of the above and clearing Project Validation, but the serialized asset on disk (`Assets/XR/Settings/OpenXR Package Settings.asset`) currently shows these Android features with `m_enabled: 0` — editor-side settings may not have been flushed to disk (the editor crashed twice during XR package operations). Re-check the checkboxes in the UI, fix anything Project Validation flags, then **File ▸ Save Project**.

**XR initialization** (`Assets/XR/XRGeneralSettingsPerBuildTarget.asset`):
- **Android: `InitManagerOnStart = true`** — the APK must bring OpenXR up automatically on the headset.
- **Standalone: `InitManagerOnStart`** — ON to drive a connected Quest (Link/Air Link) in editor Play; OFF for the headset-less keyboard/simulator loop (initializing OpenXR with NO headset can stall Play). Flip it with **Tools ▸ PharmaSynth ▸ Headset Play Mode (OpenXR on Play)**. Currently **ON** (2026-07-12, user is headset-testing).
- OpenXRLoader is the sole loader for both targets.

**Keystore: not yet created.** `AndroidKeystoreName` is empty and `androidUseCustomKeystore = 0`, so builds sign with Unity's debug keystore — fine for sideloaded QA builds via `adb install`, **not** for store/turnover delivery. Create a release keystore (Player Settings ▸ Publishing Settings) before final delivery and back it up off-repo (a lost keystore cannot be regenerated).

**Build:** File ▸ Build Settings (Android) ▸ Build → APK; deploy with `adb install -r PharmaSynth.apk` (Developer Mode enabled on the Quest 3 via the Meta Horizon app).

## 6. Performance budget & device-day validation

Quest 3 budget (from the master plan — measure on device, not in editor):

| Budget item | Target |
|---|---|
| Frame rate | **90 Hz**; drop to **72 Hz** only as a documented fallback |
| Draw calls | **≤ 150** per frame |
| Triangles | **≤ 1.2 M** per frame |
| MSAA | **4x — validate first** on device; step down to 2x if the GPU budget doesn't hold |
| Dynamic rigidbodies | **≤ 40 concurrent active**, with **kinematic-on-grab** (a held object stops simulating). Note: all 42 `ChemLabEquipment` prefabs are now gravity-on physics objects — keep scene composition within this cap and rest props on collidered surfaces. |
| Stereo rendering | **Single-pass instanced** (Multiview on Quest); the liquid shader is stereo-instancing-safe |
| Textures | ASTC, atlased where possible |

**Day-1 on-device checklist** (the moment the headset arrives):

1. **Wrist-gesture ergonomics** — `WristWatchController` supination + gaze gesture: tune hysteresis/thresholds on a real wrist; confirm the button fallback.
2. **Comfort pass** — tunneling vignette on move/turn, snap-turn defaults, no camera animation anywhere, teleport + continuous move both usable; check for hotspots of judder.
3. **90 Hz validation** — OVR Metrics / Perf HUD against the budget table above; foveated rendering active.
4. Trigger-zone ergonomics for the grab→station flow (zone sizes/heights vs. real reach).
5. World-space UI legibility & placement (HUD, tablet, grade screen, Pharmee subtitles) at headset resolution.
6. Full Methane tutorial UAT end-to-end (PPE → grab props → stations → grade screen → retry).

## 7. Known machine/tooling gotchas

Workstation-specific traps (Windows 11, this dev PC) — carried forward from the session log:

- **Unity MCP `Unity_RunCommand` namespace trap:** injected code compiles inside the `Unity.AI.*` namespace, so bare `Image` resolves to `Unity.AI.Image`. Fully-qualify `UnityEngine.UI.Image` (or alias `using UImage = UnityEngine.UI.Image;`). `System.Reflection` and `ISet` are blocked; filesystem writes (`File.WriteAllText`, bulk `AssetDatabase` delete/move loops) get flagged "requires user interaction" — do those via a shell or single `AssetDatabase.CreateAsset` calls.
- **MCP-driven play mode is unreliable:** issuing `Unity_RunCommand` during play force-exits play mode, and MCP-initiated play sessions exit silently shortly after entry. Stage state in edit mode and have **a human press Play** for runtime validation.
- **"Named pipe not found" from MCP** = Unity is busy/reloading (import, domain reload, crash recovery). Wait for `Logs/Editor.log` to go quiet (~30 s idle), then retry. XR/package operations have crashed the editor twice; it auto-recovered both times.
- **Poppler/PDF is broken:** the machine's TEMP variable points into `C:\Program Files\poppler-24.08.0\Library\bin`, so the default PDF tooling cannot read PDFs. Use `"C:/Program Files/Git/mingw64/bin/pdftotext.exe"` for text and Python (`pypdf` + `pillow`, installed) for pages/images. The manuscript Google Doc exports cleanly via `curl -sL "<doc-url>/export?format=txt"`.
- **Screenshot/capture tools can fail:** the MCP capture tools (`Camera_Capture`, scene-view captures) intermittently fail on this machine — usually the same busy-editor condition as the named-pipe error. Retry after the editor idles; if a capture keeps failing, verify visually in the editor instead.
- **Edit-mode component gotcha** (affects test writing): `OnEnable`/`Awake` don't fire on `AddComponent` in edit mode — components expose public `SetX()`/bind methods for edit-mode tests; use them.
- **Before deleting any script**, grep all of `Assets/` for the **type name** (not just scene/prefab component scans) — a past deletion (`LiquidData`) broke a dependent script and sent the editor into Safe Mode.
- **FBX imports have no colliders** — colliders are added in-scene (the walkable floor mesh is `Floor (1)`). Keep this in mind when importing replacement art.
