# Architecture MOC

Up: [[Home]] · Siblings: [[Systems MOC]] · [[Content MOC]] · [[Process MOC]]

How the codebase is shaped, and the reasoning behind the shape. For *what a specific
class does*, go to [[Class Index]]. For *what a mechanic does at runtime*, go to
[[Systems MOC]].

---

## The one architectural rule

**Thin MonoBehaviours over pure, suite-tested C# cores.**

Every mechanic splits in two:

- a **pure static/plain class** holding the maths and decisions — no Unity lifecycle,
  no scene dependency, callable from an editor test
- a **MonoBehaviour driver** that reads the scene, calls the pure core, and applies
  the result

Examples across the codebase:

| Pure core | Driver | Decides |
|---|---|---|
| `GatekeeperModel` | `PharmeeGatekeeper` | the whole door/flow FSM |
| `HeightCalibration` | `SeatedHeightBoost` | fixed eye height |
| `HeadPushbackMath` | `HeadCollisionPushback` | head-vs-wall correction |
| `OrbitMath` | `StirController` | swept-angle stirring |
| `WeighMath` | `WeighStation` | pan settle + mass |
| `ScoopMath` | `ScoopController` | 2 g charge pickup/deposit |
| `AssemblyMath` | `ApparatusSnap` | which parts snap to which |
| `MasteryModel` | `ExperimentGrader` | BKT mastery |

This is *why* the self-test suite can assert well over a thousand behaviours without entering Play
mode. It is also the reason a whole class of bug is invisible to the suite — see the
warning at the end of this note.

### The `Bind()` seam

Edit-mode `AddComponent` does **not** fire `Awake` or `OnEnable`. Every component
therefore exposes a `Bind(...)` method that wires its references explicitly, and
builders call it. Two mirror traps follow from this, and both have cost a stuck
playtest:

- In **Play** mode `AddComponent` fires `OnEnable` **immediately** — before `Bind()`,
  while fields are still null. So event subscription must live in the `Bind` seam,
  not in `OnEnable` alone.
- `DestroyImmediate` **skips** `OnDisable`/`OnDestroy` for components whose `OnEnable`
  never ran → an explicit `Detach()` before destroying, or C# event subscriptions
  ghost forever.

→ [[Gotchas]]

---

## Namespace and layout

Everything runtime lives under `Assets/PharmaSynth/Scripts/` in the **global
namespace** (no `namespace` declarations — inherited convention, kept).

```
Assets/PharmaSynth/Scripts/
  Experiment/    the run itself: TaskGraph, module defs, runner, mistakes, quizzes
  Chemistry/     liquids, pouring, reactions, temperature, gas/crystallise/filter
  Interaction/   the physical lab: verbs, grabbing, breakage, stage building, feedback,
                 tutorial guidance (TutorialCoach lives in UI/)
  Scoring/       BKT mastery, rubric, the two-part gate
  Progression/   save file, catalog, unlock chain, demo mode, results
  NPC/           Pharmee, Dr. Jimenez, dialogue pools, cutscenes, tour guide
  UI/            HUD, wrist watch, quiz tablet, grade screen, fader, settings
  Safety/        PPE, fume hood, hazard zones
  Audio/         AudioService, SoundBank, voice
  Editor/        the self-test suite + 100+ builder menus (editor-only assembly)
```

Rough size: **294 files, ~44,000 lines**, of which the `Editor/` folder is ~19,500 —
tooling is nearly half the codebase, which is normal for a project whose scene edits
all go through idempotent builders.

---

## Experiments are DATA, not scenes

There is **one** lab scene. An experiment is a `ExperimentModuleDefinition`
ScriptableObject describing tasks, reagents, layout and quiz. At run time
`ExperimentSceneBuilder` reads it and builds a stage into `DynamicStage`.

This means:

- adding an experiment is authoring an asset, not building a scene
- the layout may only stage **task-bound vessels** — never tools, never reagents
- `ClearBenchBindings()` runs on every build, or bindings leak into the next module

→ [[experiments-reference]] for the data, [[systems-reference]] for the builder.

---

## The flow spine

```
GameFlow ──▶ ProgressionFlow ──▶ ExperimentCatalog (the 9-chain)
                  │
                  ▼
         GatekeeperModel (pure FSM)  ◀── the actual state machine of the game
                  │
                  ▼
         PharmeeGatekeeper (driver: door, dialogue, teleports, review)
                  │
                  ▼
         ExperimentRunner  ──▶ TaskGraph ──▶ tasks completed by verbs
                  │
                  ▼
         ExperimentGrader ──▶ ScoreCalculator (rubric) + MasteryModel (BKT)
                  │
                  ▼
         ResultRecorder ──▶ ProgressionService (JSON save) ──▶ UnlockDiff
```

`GatekeeperModel` is the single source of truth for "what phase is the game in".
`PharmeeGatekeeper` only *drives* it. If flow behaviour looks wrong, read the model
first — it is pure and suite-pinned.

→ [[gameplay-flow]]

---

## The grading gate

Two independent gates, **both** must pass:

- **Rubric** ≥ 90% — `ScoreCalculator`, floored percentage, deducts for mistakes
- **BKT mastery** ≥ 0.90 — `MasteryModel`, Bayesian Knowledge Tracing per `LabSkill`

> [!warning] The gate was once unwinnable
> Mastery stayed below 0.90 on flawless runs until every module's `trackedSkills` was
> set to its own signature skills. **Re-run `Simulate Campaign` whenever you change a
> module's tasks or skills**, and check the `content: … is BEATABLE` pins.

---

## The XR rig

`XR Origin (XR Rig)` — one prefab, instanced in both scenes (same GUID), with
per-scene overrides. Notable pieces:

- `CharacterController` r=0.25, step 0.25, gravity via XRI's `GravityProvider`
- `SeatedHeightBoost` — **fixed** eye height (1.45 m) for everyone, re-planted every
  frame. Not relative to real height: the Quest runtime flip-flops between Floor and
  Device origin spaces across sessions, and every "relative" scheme produced
  floor-spawns or roof-spawns.
- `HeadCollisionPushback` — stops the head phasing through walls and tables.
  **Horizontal only**; vertical placement belongs to the CharacterController.
- `WalkBob` — subtle locomotion bob, rides *on top of* the fixed height offset

> [!bug] These three fight over the same transforms
> `SeatedHeightBoost` owns the Camera Offset's Y. `WalkBob` must add to
> `AppliedOffset`, never overwrite it. `HeadCollisionPushback` writes the rig root
> directly, which is why its Y term had to be removed — see [[Gotchas]].

→ [[The Lab Scene]]

---

## What the pure-core pattern cannot protect you from

> [!danger] Pure math cannot see a missing component
> A builder that destroys and recreates objects can silently take a component with it.
> The pure `ShouldStrike` / `ShouldIgnite` tests still passed while the matchbox had
> lost its `MatchStrikerSurface` — so striking it did nothing, and every burner in the
> game was unlightable, and nothing caught it.
>
> The answer is **scene-pinning assertions** (`match:`, `bench:`, `wired:`,
> `verbwire:`) that assert against real scene objects, not just maths. When you add a
> mechanic that depends on a scene component existing, add a scene pin for it.

→ [[Gotchas]] · [[Build and Test Loop]]
