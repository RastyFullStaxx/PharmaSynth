# Developer Guide

Up: [[Home]]

The entry point for a developer new to the PharmaSynth code. It covers how the code is
shaped, how to check a change, and how to add content. It links to the detailed notes
instead of repeating them.

PharmaSynth is a guided VR chemistry lab for Meta Quest 3, built in Unity 6000.5.2f1
(URP 17.5, OpenXR, XR Interaction Toolkit 3.5). Everything runs in the editor without a
headset: the XR Device Simulator drives the rig.

---

## Your first hour

1. Open `Assets/Scenes/SampleScene.unity` (the lab). `MainMenu.unity` is the spawn room.
2. **Tools ▸ PharmaSynth ▸ Run Self-Tests.** Expect ALL GREEN plus three deliberate
   warnings. The one-line result is written to `Logs/selftest-result.txt`.
3. **Tools ▸ PharmaSynth ▸ Simulate Everything (full playability check).** Plays all nine
   experiments and the campaign loop in edit mode and writes one verdict to
   `Logs/simulate-everything.txt`.
4. **Tools ▸ PharmaSynth ▸ Autopilot Playtest (plays the game in Play mode).** Drives
   the real loop in Play mode for about eight minutes, then writes
   `Logs/autopilot-report.txt` and screenshots to `Logs/autopilot/`.
5. **Reopen SampleScene from disk** (File ▸ Open Scene, discard changes). The suite and
   the simulators run experiments on the open scene and leave it changed. Never save
   after a run.

Those three checks are the gate for every change: the suite always, Simulate Everything
when task data, layouts or verbs change, and the autopilot when Play-mode behaviour does.
Details: [[Build and Test Loop]].

---

## Where things live

```
Assets/PharmaSynth/
  Scripts/
    Experiment/    the run: TaskGraph, module definitions, ExperimentRunner, quizzes
    Scoring/       BKT mastery, the rubric, the two-part pass gate
    Progression/   save file, the experiment chain, demo mode, results export
    Chemistry/     liquids, pouring, reactions, temperature, gas, filtering
    Interaction/   verbs, grabbing, breakage, stage building, tutorial guidance
    NPC/           Pharmee, Dr. Jimenez, the door gatekeeper, dialogue, cutscenes
    UI/            HUD, wrist watch, quiz tablet, grade screen, fader, settings
    Safety/        PPE, fume hood, hazard zones
    Audio/         AudioService, sound bank, robot voice
    Editor/        builder menus, simulators, the autopilot and the self-tests
  ScriptableObjects/
    Experiments/   one ExperimentModuleDefinition per experiment
    Layouts/       Layout_*.asset: which bench vessels each experiment uses
    Quizzes/       Quiz_*.asset: the three review questions per experiment
    Chemicals/     ChemicalData assets
```

Scripts use the global namespace. The full class list, with each class's summary, is
generated into [[Class Index]]; every builder menu is in [[Editor Menus]].

---

## How the code is shaped

**A thin component over a pure core.** Rules live in plain C# that the suite can call
directly: `TaskGraph`, `GatekeeperModel`, `ScoreCalculator`, `PourMath`, `WeighMath` and
the other `*Math` classes. The MonoBehaviour around each one reads the scene, calls the
core, and applies the result. When you change behaviour, change the core and pin it in
the suite. The component should stay small enough to read in one sitting.

**Every component has a `Bind()` method.** In edit mode `AddComponent` never runs
`Awake` or `OnEnable`, so the simulators and tests wire components through `Bind()`.
In Play mode `AddComponent` runs `OnEnable` immediately, before `Bind()`. Subscribe to
events in `Bind()`, not only in `OnEnable`.

**Experiments are data.** There is one lab scene. An experiment is an
`ExperimentModuleDefinition` plus a layout asset. At run time `ExperimentSceneBuilder`
reads them and wires the task logic onto vessels that already stand on the bench
(`Vessel.benchItem`). It never spawns tools or reagents. Every tool and every reagent is
always out on the bench, and the player chooses.

**The game flow is one state machine.** `GatekeeperModel` is the pure state machine
(door, picker, PPE, run, review, grade, return). `PharmeeGatekeeper` drives the scene
from its transitions. Full flow: [[gameplay-flow]].

**Large classes are partial.** A class with several jobs is split into
`<Class>.<Part>.cs` files, for example `PharmeeGatekeeper.Review.cs` and
`LiquidPhysics.Visuals.cs`. The main file is always named after the class and holds every
field, so serialized state reads in one place and Unity keeps finding the script.
The self-tests follow the same rule: the runner is in `PharmaSelfTests.cs` and the
suites are in `PharmaSelfTests.<Area>.cs`.

**Scene edits go through builder menus.** Builders under **Tools ▸ PharmaSynth** are
idempotent and re-runnable; hand edits are for placement only. Some builders destroy and
recreate what they own, and the order matters: [[systems-reference]] §7.

---

## Adding content

**A new experiment.**

1. Author the `ExperimentModuleDefinition`: graph tasks (id, label, phase,
   prerequisites, skill, rubric category, hint), `trackedSkills`, the mastery
   threshold and par time.
2. Author its `Layout_*.asset`. Bind each task vessel to a bench object by name and
   give it the reagents it expects.
3. Add a `Quiz_*.asset`. Add the module to `ExperimentLibrary.asset` and give it a row
   in `ExperimentCatalog.Entries`; the row's prerequisite sets its place in the
   unlock chain.
4. Run **Apply Verb Data**, then the suite, then that experiment's **Simulate Run**
   entry, then Simulate Everything.
5. If the module is part of the campaign, run **Simulate Campaign**. The pass gate needs
   rubric ≥ 90 and mastery ≥ 0.90, and `trackedSkills` decides whether a flawless run
   can reach that mastery.

The chemistry source is the client manuscript, not the storyboard:
[[experiments-reference]] lists each experiment's steps, apparatus and known deviations.

**A new chemical or reaction.** [[systems-reference]] §8 lists the steps, including
the registries each asset must be added to.

**A new mechanic.** Write the rule as a pure static method or class, pin it in the
suite, then add the thin component with a `Bind()` method. If the scene needs new
objects, give them a builder menu.

---

## Traps that have cost real time

- **Never save the scene after a suite or simulator run.** Reload it from disk first.
  A save after a run once baked empty bottles into the scene.
- **The console can show zero errors after a failed compile.** The only reliable check
  is `grep "error CS" Logs/Editor.log`. A suite whose assertion count did not change
  ran the old assembly.
- **A MonoBehaviour must live in a file named after its class**, or the scene loses
  the link and the component silently becomes "Missing Script".
- **Pure tests cannot see a missing component.** When a rebuild destroys and recreates
  objects, pin the actual scene objects in the suite (`bench:` and `match:` assertions).
- **Statics survive a scene load.** Anything static that refers to scene objects needs
  a reset when its owner is destroyed.
- **Prefab-instance edits made from code** must go through `SerializedObject` and
  `PrefabUtility.RecordPrefabInstancePropertyModifications`, or they revert on load.

The full list, with the incident behind each one: [[Gotchas]].

---

## Read next

[[Home]] routes everything. For the code itself: [[Architecture MOC]], then
[[Systems MOC]] for whichever mechanic you are touching.
