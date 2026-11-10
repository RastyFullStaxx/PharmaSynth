# Class Index

> [!warning] Generated note - do not hand-edit
> Derived from the code by `python Tools/gen-vault-reference.py`.
> To change what it says, fix the thing it is derived FROM, then re-run.

Every type in `Assets/PharmaSynth/Scripts/`, with the design rationale its
author left in the `///` comment. The comments are unusually load-bearing in
this codebase - most record a bug that cost real time.

Up: [[Home]] - [[Architecture MOC]] - [[Systems MOC]] - [[Gotchas]]

---

## Experiment

`Assets/PharmaSynth/Scripts/Experiment/`

### `DemoActions` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/DemoActions.cs`</sub>

Demo-session auto-complete verbs (pure statics over existing runner/quiz seams — no gate-state coupling, so they survive flow redesigns). Used by the demo HUD buttons so panelists can sweep through an experiment quickly.

```csharp
static string CompleteCurrentStep(ExperimentRunner runner)
static int CompleteAllTasks(ExperimentRunner runner)
static bool AutoAnswerQuiz(PostLabController postLab)
```

### `ExperimentModuleDefinition` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/ExperimentModuleDefinition.cs`</sub>

```csharp
string moduleId
string moduleTitle
List<string> intendedLearningOutcomes
List<string> materialReagents
List<string> materialApparatus
List<ExperimentTaskDefinition> tasks
List<ExperimentTask> graphTasks
bool assessmentMode
float masteryThreshold
BktParameters bkt
List<LabSkill> trackedSkills
RubricWeights rubricWeights
float parTimeSeconds
TaskGraph BuildTaskGraph()
```

### `ExperimentTaskDefinition` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/ExperimentModuleDefinition.cs`</sub>

```csharp
string taskId
string taskLabel
int scoreValue
bool requiredForCompletion
```

### `ExperimentResult` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/ExperimentRunner.cs`</sub>

Outcome of an experiment attempt, consumed by the grade screen and progression gate.

```csharp
GradeBreakdown grade
float overallMastery
bool gradePassed
bool masteryPassed
bool passed
int mistakeCount
float elapsedSeconds
float quizScore01
```

### `ExperimentRunner` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/ExperimentRunner.cs`</sub>

Runtime orchestrator for a single experiment attempt. Owns the TaskGraph, BKT MasteryModel, MistakeLog and grader built from an ExperimentModuleDefinition, and raises the events the HUD / tablet / wrist-watch / grade screen subscribe to. All scoring/mastery logic lives in the (unit-tested) pure classes; this is thin glue plus per-frame time + auto-check. Drives the v2 TaskGraph data model and is the sole runner.

```csharp
UnityEvent onExperimentStarted
UnityEvent onExperimentFinished
event Action<ExperimentModuleDefinition> ExperimentPrepared
event Action<ExperimentModuleDefinition> ExperimentStarted
event Action<ExperimentTask> TaskCompleted
event Action<TaskPhase> PhaseCompleted
event Action<float> ProgressChanged
event Action<LabErrorType, string> MistakeRecorded
event Action<ExperimentResult> ExperimentFinished
event Action Aborted
bool IsRunning
bool IsArmed
float ElapsedSeconds
float Progress01
```

### `LabErrorType` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/MistakeLog.cs`</sub>

The full error taxonomy the safety/error matrix reports (plan §3.7). Extends the inherited 4 hardcoded penalties (wrong step/reagent/glass/fire) with the missing overheat, PPE, fume-hood and chemical-contact cases.

### `MistakeLog` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/MistakeLog.cs`</sub>

Records mistakes during an experiment attempt and exposes counts (the grade screen needs "Number of Mistakes"). Plain C# so it is unit-testable.

```csharp
event Action<LabErrorType, string> MistakeRecorded
int Count
void Record(LabErrorType type, string message)
int CountOf(LabErrorType type)
int CountOfAny(params LabErrorType[] types)
void Clear()
static RubricCategory CategoryFor(LabErrorType type)
static bool AffectsMastery(LabErrorType type)
static LabSkill SkillFor(LabErrorType type)
```

### `QuizQuestion` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/QuizBank.cs`</sub>

A post-lab multiple-choice question (tablet "Documentation" phase). Client-reviewable content — kept as data so questions can be edited without code changes.

```csharp
List<string> options
int correctIndex
string explanation
bool IsValid()
```

### `QuizBank` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/QuizBank.cs`</sub>

The 3-question post-lab quiz for one experiment (manual's "Documentation" criterion, VR-feasible form). One asset per module, keyed by moduleId.

```csharp
string moduleId
List<QuizQuestion> questions
int Count
float Score(IReadOnlyList<int> answers)
bool AllValid()
```

### `QuizBankLibrary` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/QuizBankLibrary.cs`</sub>

Runtime-safe moduleId → QuizBank lookup (serialized direct references so it works in a build). One asset holds every experiment's post-lab quiz; the PostLabController asks it for the bank matching the running module.

```csharp
List<QuizBank> banks
QuizBank GetBank(string moduleId)
```

### `TaskGraph` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraph.cs`</sub>

Runtime engine that drives an experiment's tasks: enforces prerequisites, tracks weighted progress, raises phase/completion events, and auto-completes tasks whose registered world-state condition becomes true. Plain C# (not a MonoBehaviour) so it is unit-testable in isolation; a thin scene component owns one of these and forwards trigger/condition events to it.

```csharp
event Action<ExperimentTask> TaskCompleted
event Action<TaskPhase> PhaseCompleted
event Action AllRequiredCompleted
TaskGraph(IEnumerable<ExperimentTask> tasks)
IReadOnlyList<ExperimentTask> Tasks
bool IsComplete(string taskId)
bool HasPhase(TaskPhase phase)
bool PrerequisitesMet(string taskId)
bool IsAvailable(string taskId)
IEnumerable<ExperimentTask> AvailableTasks()
ExperimentTask FirstAvailable()
void RegisterCondition(string taskId, Func<bool> condition)
bool HasCondition(string taskId)
void Tick()
```

### `TaskPhase` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`</sub>

The four graded phases every experiment moves through (plan §3.2).

### `LabSkill` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`</sub>

Reusable lab competencies tracked by the Bayesian mastery model (plan §3.6).

### `RubricCategory` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`</sub>

Rubric criteria that the grade is composed from (WCC lab manual rubric, plan §3.6).

### `TaskCompletionResult` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`</sub>

Result of attempting to complete a task — drives the error matrix (BlockedByPrerequisite is the canonical "wrong step order" signal).

### `ExperimentTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Experiment/TaskGraphModel.cs`</sub>

One node in a module's TaskGraph. A task becomes available once every prerequisite task is complete; it can then be completed by a world-state condition (auto-check) or an explicit trigger/event.

```csharp
string taskId
string label
TaskPhase phase
List<string> prerequisites
float progressWeight
LabSkill skill
RubricCategory rubricCategory
bool required
string hint
bool autoCompleteWhenOthersDone
bool longProcess
string longProcessMessage
```

---

## Chemistry

`Assets/PharmaSynth/Scripts/Chemistry/`

### `PhysicalState` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ChemicalData.cs`</sub>

### `HazardType` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ChemicalData.cs`</sub>

Hazard class — drives spill/contact feedback and the fume-hood requirement.

### `ChemicalData` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ChemicalData.cs`</sub>

```csharp
string chemicalName
PhysicalState state
Color liquidColor
Color liquidTopColor
float sceneColourAmount
float viscosity
float boilingPointC
Color precipitateColor
bool evolvesGas
HazardType hazard
bool requiresFumeHood
bool isDangerous
bool isOxidizer
bool isConcentratedAcid
```

### `CrystallizationController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/CrystallizationController.cs`</sub>

Timed crystallization: after BeginCrystallization(), a liquid gradually turns to solid crystals over a duration (e.g. aspirin/benzoic acid on ice). Cross- fades a crystal renderer in as it progresses; fires Crystallized when done and exposes a TaskGraph auto-check predicate. Timestep-driven, deterministically tested.

```csharp
UnityEvent onCrystallized
event Action Crystallized
float Progress
bool IsDone
void BeginCrystallization()
void Tick(float dt)
void ResetProcess()
```

### `EndProductVisibility` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/EndProductVisibility.cs`</sub>

Benzoic Acid — goal of Exp 4, and its own reference sample Exp 2 runs BEFORE Exp 3 and Exp 6, so a global per-chemical hide would remove reagents Exp 2 cannot proceed without. Hiding only the running module's own product keeps both rules true: you never get your goal handed to you, and every experiment still has its inputs. The four PURE products — Acetanilide, Benzamide, Chloroform, Wine — are named as a reagent by no manuscript procedure, so they are not stocked at all rather than gated (see EndProductShelfStocker). Lives on a storage ROOT (ReagentShelf / ReagentCabinets) — the root stays active so this keeps running, and gated bottles are fully SetActive(false) so the supply monitor, hover cards and grabs all ignore them. Play-mode only: in the editor everything stays visible for arranging, and Unity restores the authored state when Play ends.

```csharp
void Bind(ExperimentRunner r)
static string HiddenProductFor(string moduleId, bool demoActive)
int Rescan()
int Apply(string hiddenChemical)
```

### `FiltrationController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/FiltrationController.cs`</sub>

Filtration (gravity / Büchner): filtrate accumulates as the player pours/pumps the mixture through. Tracks a 0..1 fraction toward a target volume, fires Filtered when complete, and exposes a TaskGraph auto-check predicate.

```csharp
UnityEvent onFiltered
event Action Filtered
event Action<float> FilteredChanged
float Fraction
bool IsDone
void AddFiltrate(float ml)
bool Filtered01(float fraction)
void ResetProcess()
```

### `GasCollection` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/GasCollection.cs`</sub>

Collects evolved gas over a run (e.g. CH4 over water, CO2 into a balloon or limewater). Tracks a 0..1 fill fraction that drives a balloon scale / bubble VFX, fires an event when full, and exposes a TaskGraph auto-check predicate.

```csharp
UnityEvent onFull
event Action Full
event Action<float> FillChanged
float FillFraction
bool IsFull
void AddGas(float ml)
bool Collected(float fraction)
void ResetCollection()
```

### `HazardFlags` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/HazardFlags.cs`</sub>

Pure name-based hazard-flag rules for ChemicalData (one table shared by the editor audit menu, the raw-reagent forge, and the self-tests). Names are the stable key — the chemistry SOs are authored per display name.

```csharp
static bool IsOxidizer(string chemicalName)
static bool IsConcentratedAcid(string chemicalName)
```

### `HazardousMix` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/HazardousMix.cs`</sub>

Pure classification of chemically-BAD mixes, so a wrong mixture produces smoke, fire, colour and a penalty. Runs on the existing LiquidPhysics.WrongReagentMixed seam, i.e. only for A+B pairs with NO registered ReactionRule (real chemistry always wins). Direction-aware: `current` is what's in the vessel, `incoming` is what got poured in. Everything stays isolated in-sim per the manuscript ("dangerous conditions isolated without risk") — effects + penalty, never player harm.

### `HazardOutcome` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/HazardousMix.cs`</sub>

```csharp
static HazardOutcome Classify(ChemicalData current, ChemicalData incoming)
static bool IsAcid(ChemicalData c)
static bool IsHypochloriteLike(ChemicalData c)
static LabErrorType ErrorTypeFor(HazardOutcome o)
static string WarnLineFor(HazardOutcome o)
static Color TintFor(HazardOutcome o, ChemicalData current)
```

### `HazardousMixReactor` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/HazardousMixReactor.cs`</sub>

Scene driver for HazardousMix: subscribes to a vessel's WrongReagentMixed (the previously-silent no-rule mix) and stages the consequence — outcome VFX at the vessel, positional SFX, the lab alarm + warning vignette for the dangerous ones, and the graded mistake. One reaction per vessel per 2 s so a continuous pour doesn't spam penalties. Attached by ExperimentSceneBuilder (vessels + pourables) and ShelfPourWiring (shelf/cabinet bottles).

```csharp
const float CooldownSeconds
void Bind(LiquidPhysics liquid, ExperimentRunner runner)
```

### `LiquidPassthrough` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidPassthrough.cs`</sub>

Marker: a pour STREAM passes straight through this object (funnels). Otherwise LiquidPourer.ResolveTarget treats the funnel's collider as the landing surface (no LiquidPhysics there), so the hydrolysate the manuscript says to filter is wasted as a puddle on the funnel and the FeCl3 filtrate test below has nothing to react with. The ray continues to the receiving vessel underneath. It also OWNS the funnel's visual: the stream disappears into the cone and re-emerges as a thin trickle from the stem's hole. Runtime-only and self-building — no scene authoring beyond the component.

```csharp
Transform spout
Vector3 SpoutPoint
bool IsFlowing
void Flow(Color tint)
```

### `LiquidPhysics` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidPhysics.Visuals.cs`</sub>

Drawing the contents: fill level, colour, powder mound, boil and wobble.

```csharp
static bool ShouldAdoptHostRenderer(Material hostMaterial)
static float DisplayFill01(float ml, float maxVolume)
static bool ShowsAsDryPowder(PhysicalState state, bool wetted)
static bool DrawsLiquidColumn(float ml, bool dryPowder, bool hasMound)
float BoilAmount()
static float BoilFor(float volumeMl, float tempC, float boilingPointC)
void UpdateAllVisuals()
```

### `LiquidPhysics` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidPhysics.cs`</sub>

```csharp
event Action<ChemicalData, float> LiquidAdded
event Action<ChemicalData, float> LiquidRejected
event Action<ReactionRule> ReactionOccurred
event Action<ChemicalData, ChemicalData> WrongReagentMixed
event Action<ReactionRule> ReactionPending
event Action Emptied
Renderer mainRenderer
Renderer precipitateRenderer
float maxVolume
float currentLiquidVolume
float currentPptVolume
float HorizonalFloatAdj
ChemicalData currentChemical
ChemicalData currentPptChemical
```

### `PourMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidPourer.cs`</sub>

Pure pour rules.

```csharp
static bool CanPour(ChemicalData chem)
const float HeldSpillTrickle
static float SpillAmount(float amount, bool held, bool hasTarget)
```

### `LiquidPourer` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidPourer.cs`</sub>

```csharp
Transform spout
LineRenderer streamLine
float pourThreshold
float maxFlowRate
int streamSegments
float streamDropStrength
float flowSmoothingSpeed
float minStreamWidth
float maxStreamWidth
GameObject acidSpillPrefab
float spillCooldown
float assistRadius
static bool DebugOverlay
static float PourVolume(bool pouring, float baseVol, float catVol, float flow01)
```

### `LiquidTaskBinding` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidTaskBinding.cs`</sub>

Bridges a vessel's LiquidPhysics chemistry events to the experiment logic in a context-aware way: adding a reagent completes the task that expects it (the TaskGraph's prerequisite check enforces order), while a reagent no step expects is a genuine wrong-reagent mistake. Steps may require a MINIMUM poured amount (requiredMl); deliveries accumulate until the threshold is met, so a one-frame splash does not complete a step (the client's depletion mechanic).

### `ReagentStep` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/LiquidTaskBinding.cs`</sub>

```csharp
IReadOnlyList<ReagentStep> ExpectedSteps
bool RoleAmbiguous
int ClaimedRole
event System.Action<int> RoleClaimed
bool IsPoolMember
void MarkPoolMember()
bool ClaimForTask(string taskId)
List<string> RoleTagsFor(string taskId)
string ClaimedRoleTagFor(string taskId)
List<string> CandidateTasks()
void SetRoles(List<List<ReagentStep>> roles, int authoredRole, RackRoles shared)
void RefreshRoles()
IEnumerable<ChemicalData> AllRoleReagents()
void ResetRole()
```

### `OverheatEffects` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/OverheatEffects.cs`</sub>

Overheat consequence at a Heat station (error-effects pass): when the station's TemperatureSim crosses its overheat threshold, the vessel on the pad starts SMOKING, its contents turn into a ruined dark mixture, the alarm fires, and an Overheat mistake is recorded. Attached per Heat station by ExperimentSceneBuilder.

```csharp
void Bind(TemperatureSim sim, ExperimentRunner runner, ChemicalData ruined)
```

### `RackRoles` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RackRoles.cs`</sub>

Who has claimed which role inside ONE rack group. A rack group's tubes are interchangeable, so a role belongs to whichever tube the player actually poured it into. This is the shared ledger that keeps that exclusive: without it two tubes both narrow to the same role, the RackTaskGroup counts that role twice, and the step completes while a tube still sits empty. Plain C# rather than a MonoBehaviour: it holds no transform, needs no update, and is created by ExperimentSceneBuilder alongside the group's RackTaskGroups.

```csharp
void Join(LiquidTaskBinding who)
void Claim(LiquidTaskBinding who, int role)
void Release(LiquidTaskBinding who)
void RefreshOthers(LiquidTaskBinding who)
ICollection<int> TakenByOthers(LiquidTaskBinding me)
```

### `RackMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RackTaskGroup.cs`</sub>

Pure rules for a rack of tubes that share one step. Manuscript Exp 2 runs the same test across a SET of tubes — five alcohols for the enol test, three butyl alcohols beside a negative control, acetone beside acetaldehyde, hydrolysed beside unhydrolysed aspirin. In every case **the comparison across the tubes IS the lesson**, so the step is only done when every tube that the step names has had its reagent. This exists because LiquidTaskBinding is per-VESSEL: give five tubes a binding for the same task and the first tube to hit its threshold completes it, quietly making the other four optional and throwing the lesson away. The rack members are authored completesTask:false (so their pours are expected and accumulate, but completion is not theirs) and the group calls it in.

```csharp
static bool AllReady(int readyTubes, int requiredTubes)
static string ProgressLabel(int readyTubes, int requiredTubes)
static string RolesLine(IReadOnlyList<string> tags, ICollection<string> served,
static int CountReady(IReadOnlyList<LiquidTaskBinding> members, string taskId)
```

### `RackTaskGroup` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RackTaskGroup.cs`</sub>

Completes one task when every tube in its rack has had what the step asked of it. Thin driver over RackMath; poll-based so it needs no event plumbing into LiquidTaskBinding (which already tracks its own readiness).

```csharp
int MemberCount
int Required
string TaskId
IReadOnlyList<LiquidTaskBinding> Members
void Bind(ExperimentRunner r, string task, List<LiquidTaskBinding> tubes, int requiredCount
bool ShouldFire()
```

### `RawReagentCatalog` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RawReagentCatalog.cs`</sub>

THE manuscript materials table: Appendix C names ~54 distinct materials (raw precursors, prepared solutions and small consumables) that must exist in the lab, not just end-products. One row per manuscript material, each mapped to nature-appropriate labware. Source of truth for: ChemicalData generation (RawReagentForge), the cabinet stocking pass (ReagentCabinetBuilder), hover-info blurbs (LabInfoDatabase fallback) and the demo ready-made kits. Pure + test-pinned.

### `LabwareKind` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RawReagentCatalog.cs`</sub>

### `Row` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/RawReagentCatalog.cs`</sub>

```csharp
const string GroupAcids
const string GroupOrganics
const string GroupTests
const string GroupConsumables
static IReadOnlyList<Row> Rows
static Row Find(string chemicalName)
static string BlurbFor(string chemicalName)
```

### `ReactionRegistry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReactionRegistry.cs`</sub>

```csharp
List<ReactionRule> rules
ReactionRule FindReaction(ChemicalData a, ChemicalData b)
```

### `ReactionOutcome` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReactionRule.cs`</sub>

Observable outcome of a reaction — the gradeable signal a chemical test checks for.

### `ReactionRule` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReactionRule.cs`</sub>

```csharp
ChemicalData inputChemicalA
ChemicalData inputChemicalB
ChemicalData resultLiquid
ChemicalData resultPrecipitate
bool hasPrecipitate
float minTemperatureC
ReactionOutcome outcome
bool evolvesGas
string expectedObservation
bool TemperatureSatisfied(float currentTemperatureC)
```

### `ReagentSupplyMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReagentSupplyMonitor.cs`</sub>

Pure shortfall analysis: which incomplete pour-steps can no longer be finished because the remaining supply of their reagent (summed across all bottles) is less than what the step still needs. Edit-mode testable.

### `Need` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReagentSupplyMonitor.cs`</sub>

```csharp
static List<string> FindShortfalls(IEnumerable<Need> needs,
```

### `ReagentSupplyMonitor` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ReagentSupplyMonitor.cs`</sub>

Watches the live stage while an experiment runs: if a required pour-step can no longer be satisfied by the reagent left in the scene's bottles, it raises SupplyExhausted (once per attempt) so Pharmee can offer the restart.

```csharp
event Action<List<string>> SupplyExhausted
void SetRunner(ExperimentRunner r)
void Unlatch(float graceSeconds
bool Latched
void ForceLatch()
static int RefillSourceBottles()
List<string> EvaluateNow()
```

### `ShelfPourWiring` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/ShelfPourWiring.cs`</sub>

Pour wiring for HAND-PLACED bottles, so tipping a reagent-shelf bottle shows a stream and puddle. ExperimentSceneBuilder wires runtime-spawned pourables, but the 16 shelf display bottles (and batch-H cabinet stock) are scene objects with LiquidPhysics only. This mirrors the builder's pourable block for an existing bottle, idempotently, callable from the editor menu (Tools ▸ PharmaSynth ▸ Wire Shelf Pourers) and from runtime builders. Edit-mode note: SpillMistake/LiquidPourer self-bind in Awake/Start at play time, so edit-mode wiring only has to ADD the components and set the serialized fields (registry, spout); the runner param matters only for runtime callers.

```csharp
static int WireBottle(GameObject bottle, ExperimentRunner runner, ReactionRegistry registry)
```

### `HeatModel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/TemperatureSim.cs`</sub>

Exact exponential heat model: temperature approaches the heat source (when heating) or ambient (when cooling). Stable for any timestep, so it is deterministically unit-testable. Powers distillation cut-offs (56 °C acetone, 70-80 °C ethanol) and the aspirin overheat branch.

```csharp
float Ambient
float HeatRate
float CoolRate
float Current
HeatModel(float ambient
void SetHeating(bool on, float sourceTemp)
void Step(float dt)
```

### `TemperatureSim` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/TemperatureSim.cs`</sub>

Per-vessel temperature with target-reached and overheat threshold events, plus a condition predicate for TaskGraph auto-check. Thin wrapper over HeatModel.

```csharp
UnityEvent onReachedTarget
UnityEvent onOverheated
event Action ReachedTarget
event Action Overheated
float CurrentC
bool IsOverheated
void SetHeating(bool on, float sourceTemp)
void Tick(float dt)
bool AtLeast(float temperatureC)
void ResetSim()
```

### `VesselLedger` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/VesselLedger.cs`</sub>

Pure, testable record of what went into a vessel (feedback layer). Display-only — chemistry stays in LiquidPhysics/ReactionRegistry; this just remembers the story so hover cards and mix feedback can say "Ethanol 120 ml + NaOH 50 ml" or "Reacted -> Acetanilide". Volumes are per-chemical totals; a reaction collapses the story to the product (matching what the vessel now holds), keeping summaries short after multi-step syntheses.

```csharp
int Count
System.Collections.Generic.IReadOnlyList<string> Names
void Add(string chemicalName, float ml, bool solid
void React(string resultName)
void Clear()
void Scale(float frac)
const int All
string Summary(int max
```

### `VesselRoleMatch` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Chemistry/VesselRoleMatch.cs`</sub>

nearest, so "pour the ethanol into tube 0" is guidance they cannot act on: pouring the right reagent into the wrong-numbered tube was graded a wrong-reagent mistake even though the chemistry was correct. A rack group's members are interchangeable glassware, so the role should follow the pour, not the other way round. ⛔ CLAIMING MUST BE DEFERRED, not decided on the first pour. The four alkaline tubes all take KMnO₄ then NaOH and differ only at the THIRD reagent (n-butyl / sec-butyl / tert-butyl). Claiming eagerly would assign a role arbitrarily on pour one and then punish a perfectly correct third pour. So a tube holds a CANDIDATE SET that each pour narrows; it is claimed only once one candidate survives, and a pour is wrong only when the set empties. The enol group happens to disambiguate on pour one (five different alcohols) — that falls out of the same rule rather than needing a specia

```csharp
static List<int> Candidates(IReadOnlyList<IReadOnlyList<string>> roles,
static int ClaimedRole(IReadOnlyList<int> candidates)
static bool IsImpossible(IReadOnlyList<int> candidates)
const string RegularFamily
const string HardGlassFamily
static string FamilyOf(string benchName)
static string PoolKey(string benchName, string itemId)
static bool WouldAccept(IReadOnlyList<IReadOnlyList<string>> roles,
```

---

## Interaction

`Assets/PharmaSynth/Scripts/Interaction/`

### `SnapAnchor` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ApparatusSnap.cs`</sub>

Where a part seats on its host when snapped.

### `AssemblyMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ApparatusSnap.cs`</sub>

Pure rules for apparatus assemblies. Apparatus that belongs together sticks when brought close, grab moves the WHOLE group preserving formation, and the ACTIVATE click detaches (never grab). The part→host table mirrors the real heating rigs the manuscript implies plus the watch-glass cover. Kept plain so the suite pins the pairs + seats.

```csharp
const float SnapRadius
static bool CanAttach(string partPrefab, string hostPrefab)
static bool TryAnchor(string partPrefab, string hostPrefab, out SnapAnchor anchor)
static bool Participates(string prefabName)
static Vector3 SeatCenter(SnapAnchor anchor, Bounds host, Bounds part)
```

### `ApparatusSnap` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ApparatusSnap.cs`</sub>

Runtime snap behaviour: releasing a part near a compatible host attaches it (parented, kinematic, formation preserved — grabbing ANY member's mesh grabs the whole assembly via collider forwarding). The ACTIVATE click on the held assembly pops the most recently attached part back off. Added by PhysicsAudit.WireSceneItem / the kits builder for participating apparatus.

```csharp
ApparatusSnap AttachedTo
readonly List<ApparatusSnap> Attached
string PrefabName
void Bind(string prefab, XRGrab grab, Rigidbody rb)
ApparatusSnap Root()
void Attach(ApparatusSnap host, SnapAnchor anchor)
bool DetachNewest()
void Detach()
```

### `AtmosphereVfx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/AtmosphereVfx.cs`</sub>

Ambient lab atmosphere: gentle white vapour so the room feels alive — a cool stream sinking from the AC unit, and faint slow-drifting haze near the floor/ceiling. Procedural (shares EffectVfx's soft-dot material, no asset deps) and deliberately LOW-density — transparent smoke is the main overdraw cost on Quest, so counts + alpha are kept small. Built + played on Start; placed by AtmosphereBuilder.

### `Style` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/AtmosphereVfx.cs`</sub>

```csharp
static string StyleName(Style s)
void Bind(Style s)
void Bind(Style s, Vector3 hazeSize)
void Build()
```

### `BoundsUtil` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/BoundsUtil.cs`</sub>

The plain "how big is this object" measure: the union of the renderer bounds of an object and its children. Builders use it to place and size furniture and kits. Not for task vessels at runtime: `ExperimentSceneBuilder.SolidWorldBounds` skips the effect children (pour streams, fill layers, name labels) that drag a vessel's bounds toward the floor.

```csharp
static Bounds OfRenderers(GameObject g, float fallbackSize
```

### `RendererCache` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/BoundsUtil.cs`</sub>

Renderer list for bounds that are measured every frame. The bounds stay live (a renderer's bounds follow it as it moves); only the LIST is cached, re-read when the hierarchy changes or every `refreshSeconds` in Play mode, so a per-frame check no longer allocates a fresh array. Edit mode always re-reads.

```csharp
static bool TryUnion(Renderer[] rs, out Bounds b)
static Bounds OfRenderers(Renderer[] rs)
```

### `BreakableGlassware` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/BreakableGlassware.cs`</sub>

Glassware that shatters when dropped hard (§2 mishandling penalties). Only a FREE (dynamic, un-held) item can break — kinematic shelf items and items currently in a hand never do, no matter how they scrape a wall. On break: shatter SFX, a DroppedGlassware mistake against the Sanitation rubric, and the item goes home via DropRespawn as a fresh replacement so the experiment stays completable.

```csharp
void Bind(ExperimentRunner runner, DropRespawn respawn, Rigidbody rb, string label)
void Break()
```

### `BrushController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/BrushController.cs`</sub>

The test-tube brush: while held and touching a dirty vessel, its travel accumulates — every SwipeDistance of scrubbing motion knocks one swipe off the dirtiness.

```csharp
void Bind(XRGrab grab)
```

### `BurnerController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/BurnerController.cs`</sub>

An ignitable Bunsen/alcohol burner. Bring a lit matchstick to the burner and it lights — a looping flame + point light; Heat stations whose required prop is a burner only advance their sim while it is LIT (ZoneSimStation ignition gate).

```csharp
const float MatchIgniteDistance
bool IsLit
static System.Collections.Generic.IReadOnlyList<BurnerController> Active
static bool AnyLitWithin(Vector3 pos, float radius)
static bool ShouldIgnite(bool burnerLit, bool matchLit, float distance)
static bool ShouldBlowOut(bool lit, bool held)
void Ignite()
void Extinguish()
```

### `CenterTableMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/CenterTableMath.cs`</sub>

Pure geometry for the center-table merge (one wide LANDSCAPE table centered in the lab replaces the two islands). The experiment layouts bake world positions on the old left island, so the merge is a rigid remap — rotate 90° about the old island centre, translate to the new centre — applied identically to the island object AND every baked position on it, keeping every station/prop exactly where it sat on the deck.

```csharp
static Bounds FootprintOf(IEnumerable<Vector3> positions, float margin)
static Vector3 Remap(Vector3 p, Vector3 oldCenter, Vector3 newCenter, bool rotate90)
static Quaternion RemapRotation(Quaternion q, bool rotate90)
static bool WithinXZ(Vector3 p, Bounds b, float pad
static Vector3 MirrorAcrossX(Vector3 p, float centerX)
```

### `CleanupMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/CleanableVessel.cs`</sub>

Pure rules for glassware cleanliness (scrub a test tube and a text shows the dirtiness dropping with each swipe until the test tube is clean). Educational only — never graded.

```csharp
const float DirtyOnEmpty
const float SwipeDistance
const float DirtPerSwipe
const float RinsePerMl
static bool BecomesDirty(float previousMl, bool nowEmpty)
static float AfterSwipe(float dirtiness)
static float AfterRinse(float dirtiness, float mlAdded)
static string NamePrefix(float dirtiness, bool everDirty)
```

### `CleanableVessel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/CleanableVessel.cs`</sub>

Residue state on a vessel: emptying it after use makes it DIRTY (label prefix via VesselStatus); the test-tube brush scrubs it clean swipe by swipe, and wash-bottle water rinses it. Added by the kits builder and the stage builder next to LiquidPhysics.

```csharp
float Dirtiness
bool EverDirty
void Bind(LiquidPhysics lp)
void Soil()
void ResetResidue()
float Scrub()
string NamePrefix()
```

### `DispenserMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ConsumableDispenser.cs`</sub>

Pure dispenser rules (the box sits on the shelf; grabbing it pulls out a single piece to use). Kept plain-C# so the self-tests pin the policy without a headset.

```csharp
static bool IsTaken(bool grabbed, float distFromRest, float takenDistance
static bool ShouldDiscard(bool everHeld, bool held, float speed, float idleSeconds,
```

### `ConsumableDispenser` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ConsumableDispenser.cs`</sub>

Turns a consumable box into an endless single-piece dispenser. The box stays fixed on the shelf; one ready piece rests in it. When a hand takes that piece, a fresh one appears after a short delay — so there's always exactly `readyCount` pieces to grab. Taken pieces get a DispensedConsumable that cleans them up once abandoned. Clones a hidden, fully-wired TEMPLATE so all the per-consumable wiring lives in the editor builder, not here.

```csharp
void Bind(GameObject template, Transform restAnchor, Transform spawnParent, GameObject seed, int readyCount)
```

### `DevExperimentDriver` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DevExperimentDriver.cs`</sub>

Keyboard driver for in-editor testing of the experiment loop without needing full XR interaction. Lets you watch the HUD, Pharmee, and grade screen react. B = begin/restart · 1-5 = complete step N · F = finish · R = retry P = pour-debug overlay (floating "hit/target" text at every pouring mouth) V = replay Pharmee's test voice line (tune RobotVoiceFx by ear without walking back to the door for every slider tweak) Disabled in builds unless enableInBuild is set.

```csharp
void Setup(ExperimentRunner r, ExperimentStarter s)
```

### `DispensedConsumable` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DispensedConsumable.cs`</sub>

A single piece that came out of a dispenser: once it's been picked up and then set down and left alone (or falls out of the world), it removes itself so used consumables don't accumulate. The dispenser handles refills; this handles the far end of the piece's life.

### `DoorOpener` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DoorOpener.cs`</sub>

Swings a hinged door leaf open/closed and toggles its colliders — the lab entrance the PharmeeGatekeeper controls. Runtime animates the swing; edit mode snaps instantly (headless-testable).

```csharp
bool IsOpen
Transform Door
void SetOpen(bool open)
```

### `DropRespawnMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DropRespawn.cs`</sub>

Pure rules for when a loose prop goes back to its shelf spot (§2: kill-Z + idle return-to-home). Separated from the MonoBehaviour so the self-tests can pin the policy.

```csharp
static bool ShouldRespawn(float y, float killZ)
const float FloorY
const float FloorReturnSeconds
static bool ShouldReturnHome(float distanceFromHome, float speed, bool held, float idleSeconds,
static bool ShouldSettleFreeze(bool held, bool kinematic, float speed, float settledSeconds,
```

### `DropRespawn` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DropRespawn.cs`</sub>

Sends a dropped prop home: below kill-Z → instant respawn; resting far from home and untouched for ~25 s → quiet return. Restores the shelf policy (kinematic) on arrival so the shelf stays tidy. Home is captured via SetHome() by the spawner (Awake doesn't fire on edit-mode AddComponent).

```csharp
void Bind(Rigidbody rb, XRGrab grab)
void SetHome(Vector3 pos, Quaternion rot)
bool Suspended
void CaptureSupply()
static void ResetAllHome()
void GoHome()
```

### `DropperMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DropperController.cs`</sub>

The contract that makes this work: **the number in the manuscript instruction IS the squeeze count, whatever its unit.** Manuscript Exp 2 measures in three units — "5 drops of Ferric Chloride", "2 ml of Tollen's reagent", "0.1 g of salicylic acid" — and the first two both become countable squeezes: "5 drops" -> 5 squeezes   (1 squeeze = 1 drop; physically honest) "2 ml"    -> 2 squeezes   (1 squeeze = 1 ml; the deliberate abstraction) Bulk volumes (the 10 ml of water) stay on the tilt-pour with its tolerance band; grams go to the spatula (ScoopMath). The watch panel prints the real quantity for learning and the squeeze count underneath it. Each squeeze deposits MlPerSqueeze into the target vessel through the normal AddLiquid path, so an existing LiquidTaskBinding.requiredMl does the counting for free ("5 drops" = requiredMl 5) — no new task plumbing, and a miscount is an unambiguous grad

```csharp
const float MlPerSqueeze
const float Capacity
static bool CanFill(float loadedMl, PhysicalState state, float availableMl,
static float FillCharge(float availableMl, float capacity
static bool CanSqueeze(float loadedMl, bool overTarget, bool sameAsSource)
static bool CanWaste(float loadedMl, bool overTarget)
static string HoldingLabel(string chem, float loadedMl)
static float SqueezeCharge(float loadedMl, float perSqueeze
static int SqueezesLeft(float loadedMl, float perSqueeze
static string SqueezeLabel(string chem, int dropsSoFar)
static string FillLabel(string chem, float chargeMl)
static float DrainScaleY(float authoredScaleY, float frac01)
static float DrainShift(float meshHalfY, float authoredScaleY, float currentScaleY)
```

### `DropperController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/DropperController.cs`</sub>

Dropper/pipette verb: touch a liquid reagent's bottle to draw a charge, hold the dropper over a vessel and ACTIVATE (trigger) to release exactly one drop. Deliberately discrete — the squeeze count IS the measurement, so it must be a press, not a proximity brush like the scoop's dip. Self-contained like ScoopController: no scene authoring beyond the component (the probe works off renderer bounds; only a HELD dropper transfers, so shelf contact never draws). A hand-placed "DropperTip" child wins for the probe — same convention as the scoop's ScoopAnchor and the pestle's PestleTip, and the same latent bug it avoids (tracking the transform ORIGIN instead of the working end is what made the grind silently never complete).

```csharp
bool Loaded
int SqueezesLeft
void Bind(XRGrab grab)
```

### `EffectVfx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/EffectVfx.cs`</sub>

One-shot procedural particle bursts, the event twin of StationVfx's looping station effects. All built at runtime from a shared soft-dot material (no asset deps, Quest-cheap, auto-destroy): • Shatter  — glass breaks into a quick outward spray of pale shards (fired from BreakableGlassware.Break, pairs with the glass-break SFX). • Confetti — a colourful upward burst on a passing grade (GradeScreen). • FlamePop — a brief orange flame puff (burner ignite / methane splint test). Each spawner returns immediately; the emitter fades and self-destroys.

### `Kind` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/EffectVfx.cs`</sub>

```csharp
static void Shatter(Vector3 pos, Color tint)
static void Shatter(Vector3 pos)
static void Confetti(Vector3 pos)
static void FlamePop(Vector3 pos)
static void ColdAir(Vector3 pos)
static void Smoke(Vector3 pos)
static void Smoke(Vector3 pos, Color tint)
static void FireBurst(Vector3 pos)
static void Spatter(Vector3 pos, Color tint)
static void ColorFlash(Vector3 pos, Color tint)
static void Fizz(Vector3 pos)
static Material ParticleMaterial()
static void Play(Kind kind, Vector3 pos, Color tint)
static Material SmokeMaterial()
```

### `LaunchMode` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLauncher.cs`</sub>

How Launch() leaves the runner: FullStart    — legacy behavior: stage built AND the clock starts immediately. PrepareArmed — stage built + attempt armed; the clock waits for StartRun() (door gate: "the period starts as soon as you walk in"). StageOnly    — stage furnished, runner untouched (scene-load default + Lab Tour).

### `ExperimentLauncher` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLauncher.cs`</sub>

Loads any of the 9 experiments into the lab scene by moduleId: it swaps the ExperimentRunner's active module (from the ExperimentLibrary) and readies an attempt per LaunchMode. The menu / period hub / door gate call Launch(); on lab-scene entry it can auto-launch whatever GameFlow.SelectedModuleId holds.

```csharp
UnityEvent<ExperimentModuleDefinition> onModuleLoaded
ExperimentLibrary Library
LaunchMode StartupMode
void SetLibrary(ExperimentLibrary l)
void SetRunner(ExperimentRunner r)
ExperimentModuleDefinition LaunchSelected()
ExperimentModuleDefinition Launch(string moduleId)
ExperimentModuleDefinition Launch(string moduleId, LaunchMode mode)
```

### `StationSim` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLayout.cs`</sub>

Data description of one experiment's physical setup: where its stations, grabbable props and reagent vessels go. The ExperimentSceneBuilder spawns this on module load, so all 9 experiments share one lab scene instead of 9 hand-built scenes. Positions are WORLD-space (the lab is a fixed room). How a station completes. None = the prop simply entering the zone completes the step. Heat/Crystallise/Filter/Collect run a sustained chemistry sim while the prop occupies the zone. Stir/Grind/Weigh (append-only — serialized ints stay valid) are TOOL verbs: circle the rod in the vessel, work the pestle in the mortar, rest the right load on the balance pan.

### `ExperimentLayout` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLayout.cs`</sub>

### `Station` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLayout.cs`</sub>

### `Prop` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLayout.cs`</sub>

### `Vessel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentLayout.cs`</sub>

```csharp
string moduleId
List<Station> stations
List<Prop> props
List<Vessel> vessels
```

### `ExperimentSceneBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentSceneBuilder.Fitting.cs`</sub>

Fitting liquids, labels and colliders to a vessel's mesh, plus shared helpers.

```csharp
static Bounds LocalMeshBounds(Transform root, params string[] skipNames)
static int LongestAxis(Vector3 size)
static int VesselAxis(Vector3 size)
static Quaternion AxisAlign(int axis)
static float BoreOf(Vector3 size, int axis)
static void EnsurePowderVisual(GameObject inst, ChemicalData chem, float fill01
static bool IsEffectChild(string childName)
static bool IsSolidMesh(MeshFilter mf)
static Bounds SolidWorldBounds(GameObject g)
```

### `ExperimentSceneBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentSceneBuilder.Spawning.cs`</sub>

Spawning and wiring each kind of stage object: vessels, stations, sensors and bench bindings.

### `ExperimentSceneBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentSceneBuilder.cs`</sub>

Spawns an experiment's physical setup (stations, grabbable props, reagent vessels) from its ExperimentLayout when a module loads — so all 9 experiments live in one lab scene. The hand-built Methane objects stay as a grouped stage that is simply toggled; every other experiment is built into a DynamicStage that is cleared and rebuilt on each module change.

```csharp
void SetRefs(ExperimentRunner r, SceneAssetLibrary a, ReactionRegistry reg, List<ExperimentLayout> ls)
ExperimentLayout FindLayout(string moduleId)
void OnModuleLoaded(ExperimentModuleDefinition m)
int Build(string moduleId)
```

### `GlasswarePool` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentSceneBuilder.cs`</sub>

One family's interchangeable glassware: the module's roles for that kind, in role order, and every bench object that may play one of them.

### `ExperimentStarter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentStarter.cs`</sub>

Kicks off (or restarts) an experiment — hook to the intro-cutscene "Start" button or a begin trigger (poke/grab an XR interactable on this object). Also serves the grade screen's Retry.

```csharp
void SetRunner(ExperimentRunner r)
void Begin()
void Retry()
```

### `ExperimentTaskStation` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ExperimentTaskStation.cs`</sub>

A world location where a procedure step is performed. Completing the station (via interaction, trigger, or Activate()) advances the bound task in the runner. Prerequisite order is enforced by the runner (out-of-order → WrongStep mistake).

```csharp
string TaskId
string RequiredItemId
void SetRunner(ExperimentRunner r)
void SetTaskId(string id)
void SetRequiredItemId(string id)
void Configure(ExperimentRunner r, string task, string itemId, bool triggerEnter, bool onSelect)
TaskCompletionResult Activate()
bool AcceptsItem(LabItem item)
```

### `FermentationMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FermentationController.cs`</sub>

The zone-free fermentation → CO₂ → limewater mechanic (Exp 3's signature, manuscript §A f–i). Once the must is prepared, the flask evolves CO₂; a delivery tube leads it into a limewater test tube, which turns milky (CaCO₃) — the proof fermentation is underway. Mirrors WaterBathController: no station, no zone — the flask emits to whatever limewater vessel the player brings near it, wherever in the lab. The task is authored longProcess=true, so completing it fades the screen black and returns "one week later" (TimeSkipController) — the manuscript's week of standing, compressed.

```csharp
const float DeliveryRadius
static bool IsFermenting(bool mustPrepared, float flaskMl)
static bool CO2Confirmed(float limewaterPptMl)
const float NudgeAfterSeconds
static bool ShouldNudge(bool fermenting, bool confirmed, float secondsFermenting, bool alreadyNudged)
```

### `FermentationController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FermentationController.cs`</sub>

```csharp
bool Fermenting
string FermentTaskId
ChemicalData Limewater
bool BubbleInto(LiquidPhysics tube)
void Bind(ExperimentRunner r, LiquidPhysics flaskLp, string fermentTask,
void Detach()
float DeliveryRadius
void EmitCO2()
```

### `FloatingText` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FloatingText.cs`</sub>

One-shot rising/fading world-space text — the "what did my mix produce" popup, "Vessel full!", stir progress, etc. EffectVfx style: static entry point, procedural, self-destroying, runtime-only.

```csharp
static void Show(string text, Vector3 pos, Color? color
```

### `FloatingTextFx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FloatingTextFx.cs`</sub>

Rise + fade driver for FloatingText (component so it survives the frame).

```csharp
void Bind(TextMeshPro tmp)
```

### `StrideMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FootstepPlayer.cs`</sub>

Pure stride accumulator so the step cadence is unit-testable.

```csharp
static int Steps(ref float accumulator, float distance, float strideMeters)
```

### `FootstepPlayer` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/FootstepPlayer.cs`</sub>

Plays a footstep per stride of horizontal locomotion (§4 action SFX). Tracks the HEAD, not the rig root — the XR Device Simulator's WASD (and real-world walking) move the HMD without moving the origin, so tracking the origin missed them entirely. Teleports/fades are ignored via a snap guard.

### `GrabPhysicsPolicy` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GrabPhysicsPolicy.cs`</sub>

Kinematic-on-shelf / dynamic-on-release policy (task #78). Props spawn kinematic so shelves stay tidy and the rigidbody budget stays cheap; the first time the player releases one, it goes dynamic so it falls and settles instead of freezing mid-air (XRGrabInteractable restores the grab-time kinematic state on release, which would leave items floating — this runs after that restore and overrides it).

```csharp
void Bind(Rigidbody rb, XRGrab grab)
void OnReleased()
bool IsDynamic
```

### `GrabTuning` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GrabTuning.cs`</sub>

through walls/floor/furniture). The ChemLab pack prefabs shipped with movementType = Instantaneous, which teleports the transform to the hand each frame — no physics sweep, so a held beaker passes straight through static geometry. VelocityTracking drives the rigidbody with velocities instead, so PhysX resolves collisions and a held item stops against the world. Two-handed grab (hold a container in one hand and pour with the other; steady the mortar and grind with the pestle — for everything). selectMode = Multiple lets two interactors select the SAME item at once; XRI's default General grab transformer blends the two-handed pose (midpoint position, hand-to-hand rotation). Single-hand use is unchanged — it just also permits a second hand. Independent dual-hand use (one object per hand) already works. One seam applied everywhere: prefabs + scene instances (Wire Grab Collision menu) and eve

```csharp
const float AttachEaseSeconds
static bool IsTuned(XRGrab grab)
static bool Apply(XRGrab grab)
```

### `GrindController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GrindController.cs`</sub>

The GRIND verb: work the pestle in circles inside this mortar's bowl and the grind task completes (dual-path on Methane: the legacy zone-touch still works). When done, a powder heap appears in the bowl. A null/empty taskId makes it purely educational — staged mortars still grind visually.

```csharp
OrbitMath Math
bool IsGrindComplete()
static bool CanGrind(bool hasVessel, float contentsMl)
void Bind(ExperimentRunner runner, string taskId, Transform pestle,
void BindRunner(ExperimentRunner runner)
void SetTaskId(string taskId)
string TaskId
void Register()
void Tick(float x, float z, bool inside)
```

### `GuidePath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GuidePath.cs`</sub>

Tutorial Mode's ground path: chevrons on the floor that flow from the player's feet toward whatever the current step needs. ⛔ It routes on the NAVMESH, not in a straight line. The lab's benches sit in the middle of the room, so a straight line to a wall shelf points through solid furniture — an arrow that lies about the way there is worse than no arrow. `NavMesh.CalculatePath` needs no agent; it just needs the surface baked (Tools ▸ PharmaSynth ▸ Build Lab NavMesh). ⭐ It never draws at the same time as the beacon. The path answers "which way round the benches", the beacon answers "which object, through that cabinet door" — and Tutorial Mode already spends most of the player's attention budget. `GuidePathMath.ShowPath` owns that split so the rule lives in one testable place.

```csharp
static GuidePath Instance
bool PathShown
int ActiveChevrons
float LastDistance
int RouteCorners
string LastTargetName
Vector3 LastGoal
bool StartOnMesh
bool GoalOnMesh
void Bind(ExperimentRunner r, Material chevron)
void SetTuning(float spacingM, float flow, float scale)
```

### `GuidePathMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GuidePathMath.cs`</sub>

Pure geometry for Tutorial Mode's ground path: given the corners of a walkable route, where does each flowing chevron sit and which way does it face? Pure so the suite can pin the SHAPE of the path without a headset — that chevrons are evenly spaced regardless of how unevenly the navmesh corners fall, that they face along the route rather than at the destination, and that the flow runs toward the target and not away from it. A path that compiles and points backwards is not something a compile or a play-mode run would ever complain about.

```csharp
const float Spacing
const float FlowSpeed
const int MaxChevrons
```

### `Chevron` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/GuidePathMath.cs`</sub>

```csharp
static float Length(IReadOnlyList<Vector3> corners)
static bool Sample(IReadOnlyList<Vector3> corners, float distance,
static List<Chevron> Build(IReadOnlyList<Vector3> corners, float time,
const float ChevronWidth
const float ChevronDepth
const float ChevronThickness
static void ChevronGeometry(out Vector3[] verts, out int[] tris,
static Vector3 FaceNormal(Vector3 a, Vector3 b, Vector3 c)
const float FadeEdge
static float FadeAt(float along01, float edge
const float NearDistance
static bool ShowPath(float distanceToTarget, bool hasRoute)
static bool ShowBeacon(float distanceToTarget, bool hasRoute)
```

### `HandPoseKind` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandPoseController.cs`</sub>

The hand's three display poses: FREE (open), GRAB (holding something), POINT (index extended while the ray hovers an interactable — Pharmee, tools, reagents, buttons).

### `HandPosePolicy` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandPoseController.cs`</sub>

Pure hand display policy (edit-mode testable).

```csharp
static HandPoseKind PoseFor(bool selecting, bool hovering)
static bool Nitrile(bool glovesWorn)
const float ProximalCurl
const float IntermediateCurl
const float DistalCurl
const float ThumbProximalCurl
const float ThumbDistalCurl
const float PoseDegreesPerSecond
static float AngleFor(HandPoseKind pose, bool isThumb, bool isIndex, int segment)
```

### `HandPoseController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandPoseController.cs`</sub>

Runtime driver on each controller: shows the skinned hand (XR Hands sample mesh), keeps the default controller model hidden, poses the fingers (Free / Grab while selecting / Point while hovering an interactable), and swaps the material bare<->nitrile from the PPE gloves state. Wired by Tools ▸ PharmaSynth ▸ Build Hand Visuals.

### `Joint` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandPoseController.cs`</sub>

```csharp
void Bind(Transform hand, GameObject ctrlVisual, SkinnedMeshRenderer smr,
```

### `HandSwap` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandVisualKeeper.cs`</sub>

Pure hand-vs-glove display policy (edit-mode testable). The bare white hand shows whenever the first-person PPE glove is NOT worn; donning gloves swaps the glove in (never both at once).

```csharp
static bool ShowBareHand(bool gloveVisible)
```

### `HandVisualKeeper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HandVisualKeeper.cs`</sub>

Runtime keeper on each controller. Every frame it: - hides the default XRI controller MODEL (the hand replaces it), - keeps the bare hand visible (other systems were toggling it off), - swaps bare hand ↔ FPGlove when PPE gloves are donned/removed. Wired by Tools ▸ PharmaSynth ▸ Build Hand Visuals.

```csharp
void Bind(GameObject hand, GameObject controllerModel, GameObject glove)
```

### `HeadPushbackMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HeadCollisionPushback.cs`</sub>

Pure pushback resolution (edit-mode testable). Both rules are needed: without them the player can end up under the lab floor, unable to walk).

```csharp
const float SurfaceSkin
static bool IsBlockingHit(float hitDistance)
static Vector3 Correction(Vector3 lastValid, Vector3 target, Vector3 dir, float nearest)
```

### `HeadCollisionPushback` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HeadCollisionPushback.cs`</sub>

Stops the player's HEAD from phasing through static geometry, no matter how the camera moved — thumbstick locomotion, the XR Device Simulator's direct HMD translate (which bypasses the CharacterController by design), or physically leaning through a wall. Each frame the head's path is swept as a small sphere; if it would cross static geometry, the whole rig is pulled back so the head stays on the outside. Triggers, the rig's own colliders, and dynamic props (anything with a Rigidbody) never push back.

```csharp
void Bind(Transform headT, Transform rigT)
```

### `HoverHighlight` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HoverHighlight.cs`</sub>

Grabbable affordance (prop readability): when a hand/ray hovers a real-scale lab tool, it brightens (base-colour tint via a MaterialPropertyBlock — no per-material keyword or shader needed) and pops slightly larger, so small items are easy to spot and grab; it restores on hover-exit and while actually held. Thin MB over a pure scale helper.

```csharp
bool IsHighlighted
bool IsGuided
void Bind(XRGrab grab)
static Vector3 HighlightScale(Vector3 baseScale, bool on, float factor)
void SetHighlight(bool on)
void SetGuide(bool on, TargetRole role)
void SetDimmed(bool on)
bool IsDimmed
```

### `HoverInspector` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/HoverInspector.cs`</sub>

Points-at-it inspector: each frame it casts from the pointer (right-hand ray, falling back to gaze) and, if it lands on a known reagent bottle, a piece of apparatus or an NPC, shows a smooth info card (HoverInfoPanel) naming it and explaining what it is / how to use it. A short linger stops the card from flickering as the ray grazes edges. Resolution is data-driven (LabInfoDatabase), so no per-object authoring is needed.

```csharp
void Bind(Transform aim, Transform headT, HoverInfoPanel p, LayerMask m)
void BindAltAim(Transform alt)
const float PersonCardSide
static Vector3 PersonCardAnchor(Vector3 struck, Vector3 headPos, Vector3 headRight)
static Transform NpcRoot(Collider col)
static bool IsSpeaking(Transform npc)
static LabInfoEntry ResolveFor(Collider col, out Vector3 anchor)
static LabInfoEntry WithLiveLine(LabInfoEntry e, LiquidPhysics lp)
static bool IsHeld(Collider col)
```

### `IceBathMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/IceBathController.cs`</sub>

Pure rules for the ZONE-FREE ice bath (Exp 4 crystallisation, Exp 8's ice bath) — the cold twin of WaterBathMath. The bucket needs nothing lit or poured: it IS ice. Set any vessel in (or beside) it, anywhere in the lab, and the vessel takes ice-water temperature.

```csharp
const float VesselRadius
const float IceWaterC
static bool Chills(float distance)
static string StatusLine()
```

### `IceBathController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/IceBathController.cs`</sub>

The bucket itself: every LiquidPhysics vessel brought close is pulled to ice-water temperature. No station, no pad — carry the bucket or carry the flask, either way works (the zone-free tool rule).

```csharp
Vector3 ChillZoneCenter
float ChillZoneRadius
void Bind(ProximityLabel label)
void ChillVessel(LiquidPhysics vessel)
```

### `ImpactSound` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ImpactSound.cs`</sub>

Material-aware drop clatter (§4 action SFX): a FREE (dynamic) item landing plays its material's clip — glass clinks, metal clatters, wood knocks. Impacts at glass-breaking speed stay silent here; BreakableGlassware plays the shatter instead. (No RequireComponent: colliders often live on children; collision events still route to the Rigidbody host.)

```csharp
void Bind(Rigidbody rb, string soundKey, float breakSpeedCeiling
```

### `LabHaptics` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LabHaptics.cs`</sub>

Semantic haptics: the hands should be able to tell RIGHT from WRONG without looking. The VR affordance pass already gives every interactor a generic grab/poke buzz, so picking up the correct bottle and picking up the wrong one felt exactly the same — and in VR the score bar and the toast are both easy to miss while you are looking at your hands. These two cues ride alongside the audio that already fires for the same events, so there is one place per event rather than a parallel system. Deliberately two DISTINCT shapes rather than a rhythm: a short crisp tick for progress and a long coarse buzz for a mistake are told apart instantly, and neither needs a coroutine to play a second beat.

```csharp
const float StepAmplitude
const float ErrorAmplitude
static void Pulse(float amplitude, float seconds)
static void StepComplete()
static void Mistake()
```

### `LabItem` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LabItem.cs`</sub>

Identity carried by a grabbable lab prop (reagent jar, glass tube, lit splint, collection tube, burner…). An ExperimentTaskStation can require a specific itemId so that bringing the *right* prop to the *right* apparatus — not just any grabbed object — completes that step. This turns the abstract poke-stations into hands-on "gather the correct item and place it" interactions (plan §3.2, W5).

```csharp
string itemId
string displayName
void SetItemId(string id)
static LabItem Resolve(Collider other)
```

### `LabReset` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LabReset.cs`</sub>

⛔ Apparatus is a vessel the builders marked as USED — one carrying a `CleanableVessel`, which every stage, kit and adopter builder adds to glassware the player fills and never to a source. Never use a naming rule ("not `Raw_`/`Reagent_`"): it sweeps up sources too: the wash bottles and the methane tutorial's charge jar and glass tube, which starves the tutorial's setup step. Simulate Everything covers this; a suite fixture cannot, because a fixture vessel is whatever the pin says it is. Each used vessel is taken back to empty, cool, unlabelled and unclaimed; `LiquidPhysics.ClearContents` raises `Emptied`, so a pooled vessel gives its role back through the same seam a rinse uses. The bottles then refill through the supply monitor, and a solid jar gets its mound back at full. Positions, flames and consumables stay with `DropRespawn.ResetAllHome`; task wiring with `ExperimentSceneBuilder.Cl

```csharp
static bool IsUsedVessel(LiquidPhysics lp)
static bool RefusesTransfer(LiquidPhysics target, ChemicalData incoming)
static bool IsReagent(string objectName)
static int ResetApparatus()
```

### `LayoutTidyMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LayoutTidyMath.cs`</sub>

Pure zoning grid for the center-table layout pass (place all the equipment properly — facing, spacing, open space). The wide landscape deck (y 0.91, x −1.85…1.40, z −3.94…−2.74; the player works the +z front edge) is divided into four zones; every layout's items are re-seated onto the deterministic slots below, in authored order: • STATIONS  — ONE back row, 0.5 m pitch (verbs need elbow room; the busiest module has 7 stations — exactly one full row) • VESSELS   — center-front, where both hands can reach • REAGENTS  — right-side grid (pourable bottles), 4 per column • TOOLS     — left-side grid (rods, tongs, dishes…), 3 per column The front strip z > −2.90 stays free for the builder's rack, spares and match kits. Suite-pinned; the LayoutTidy menu writes these into the SOs.

```csharp
const float DeckY
const float MinX
const float MinZ
const float MinPairDistance
const float MinStationDistance
const int StationsPerRow
static Vector3 StationPos(int i)
static Vector3 VesselPos(int i)
static Vector3 ReagentPos(int i)
static Vector3 ToolPos(int i)
static Vector3 RackPos
static Vector3 SparePos(int i)
static Vector3 MatchPos(int i)
static Vector3 StrikerPos
```

### `LitmusMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LitmusStrip.cs`</sub>

Pure litmus colour response (manuscript: pH checks in Exp 3, 4, 8).

```csharp
static readonly Color AcidRed
static readonly Color NeutralViolet
static readonly Color BaseBlue
const float AcidPH
const float BasePH
static Color ColorForPH(float pH)
static float DominantPH(float a, float b)
```

### `LitmusStrip` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/LitmusStrip.cs`</sub>

A grabbable litmus strip: touch it to any liquid (trigger or collision with a vessel holding a chemical) and it tints to the mixture's pH — one-shot, like the real thing. Built by the cabinet builder's consumables box.

```csharp
bool Used
void Bind(Renderer r)
void TouchVessel(LiquidPhysics lp)
void Apply(float pH)
```

### `MatchStrikerSurface` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/MatchStrikerSurface.cs`</sub>

Marker: swiping a held matchstick across this surface lights it. Lives on the matches dispenser box and burner bases.

### `Matchstick` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/Matchstick.cs`</sub>

A grabbable matchstick (manuscript: combustion/flammability tests, Exp 3/4/7 + the methane splint). Two ways to light it: STRIKE it — swipe the held match across a striker surface (the matches box, a burner base) — or hold its head near anything already hot. A lit match then ignites burners (BurnerController) and fires the methane splint test. Pure predicates for the suite.

```csharp
const float IgniteDistance
const float IgniteTempC
const float StrikeMinSpeed
static System.Collections.Generic.IReadOnlyList<Matchstick> Active
bool IsLit
bool IsSpent
static bool ShouldIgnite(float distance, float tempC, bool alreadyLit, bool spent)
static bool ShouldStrike(bool held, bool lit, bool spent, float relSpeed, bool strikerSurface, float minSpeed
const float WeakStrikeMinSpeed
static bool IsWeakStrike(bool held, bool lit, bool spent, float relSpeed, bool strikerSurface)
void Ignite()
void Extinguish(bool spent)
Vector3 HeadPosition
static Vector3 LongestLocalAxis(Transform t, Bounds b, out float halfExtent)
```

### `MethaneApparatusRig` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/MethaneApparatusRig.cs`</sub>

Methane (Experiment 1) completion — LOCATION-FREE (the steps can be done anywhere in the lab). Instead of gating heat/collect on FIXED trigger zones, it detects the ACTIONS by item proximity, so the tutorial works wherever the player does it: prepare-mixture : grinding a mortar (the rig aims the workspace mortar at it) setup-apparatus : the collection tube brought up to the hard-glass tube heat-mixture    : a LIT burner held near the hard-glass tube (it heats) collect-gas     : the hot tube + collection tube held together (gas fills) test-gas        : a LIT match brought to the FILLED collection tube (pop) The rig owns its own TemperatureSim + GasCollection (no station objects). Items are found at runtime by LabItem.itemId, so the player can use their own workspace burner/tube/mortar anywhere.

```csharp
const float SplintMatchDistance
const float SplintAutoSeconds
static bool WithinReach(float distance, float reach)
void Bind(ExperimentRunner r, TemperatureSim t, GasCollection g)
void HandleExperimentStarted(ExperimentModuleDefinition module)
void RegisterTutorialTargets()
void Step(float dt)
static float GlowFor(float currentC, float targetC)
```

### `MethaneStageVisibility` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/MethaneStageVisibility.cs`</sub>

The hand-built Methane stage should be PRESENT only during the Lab Tour and the Methane tutorial itself, and gone the moment you move on to any other experiment or are idle in the lab. Methane is the only experiment that uses this stage (Experiment 1), so a single controller owns its visibility — the sole authority (ExperimentSceneBuilder does not toggle it). Lives on a manager object, NOT on the stage (it must keep running while the stage is hidden). Edit mode leaves the stage as-authored so it can be relocated by hand; this only governs Play. The 5 methane PROPS sit outside the stage hierarchy on the workspace, so toggling the stage alone would leave them visible. This also gathers those loose props by their methane itemIds and toggles them alongside the stage — no reparenting, so the hand placement is untouched.

```csharp
const string MethaneModuleId
static bool ShouldShow(bool tourActive, bool methaneAttemptActive)
void Bind(GameObject stage, ExperimentRunner runner, LabTourGuide tour)
```

### `MirrorPlane` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/MirrorPlane.cs`</sub>

Real-time dressing mirror using the canonical planar reflection-matrix technique. Each frame a child camera is placed at the player's eye REFLECTED across the mirror plane and renders the scene with an oblique near plane clamped to the glass (so the wall the mirror hangs on never occludes the view). Rendered by hand — URP does NOT auto-render an enabled off-screen camera, which would leave the render texture black — with inverted culling so the handedness-flipped reflected geometry keeps facing the right way. Distance-gated for the Quest budget. Note: a mirror can only reflect geometry that exists. The player sees the surroundings; seeing THEMSELVES additionally needs a visible body/PPE avatar (tracked separately in the work checklist).

```csharp
static bool ShouldRender(float distance, float activeDistance, float facingDot, float viewDot)
```

### `Mishandling` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/Mishandling.cs`</sub>

Pure rules for mishandling penalties (§2: spill & breakage): which apparatus is fragile, when an impact shatters it, and when an un-held bottle counts as spilling. Kept plain-C# so the self-tests pin the policy.

```csharp
static bool IsBreakable(string prefabName)
static IEnumerable<string> BreakableNames
const float DefaultBreakSpeed
static bool ShouldBreak(float impactSpeed, float breakSpeed
static string DropSoundKey(string prefabName)
static string SfxForOutcome(ReactionOutcome outcome)
static bool IsSpilling(float tiltDegrees, bool held, float liquidMl, float tiltThreshold
static float ImpactVolume01(float impactSpeed, float quietSpeed
static string DisplayNameFor(GameObject go)
static string Prettify(string codeName)
```

### `MixFeedback` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/MixFeedback.cs`</sub>

"What did my mix produce?" feedback on a vessel: a registered reaction pops its authored observation text + a colour flash; a harmless no-rule mix reports the mixture story; an overflow says "Vessel full!" and wets the bench. Hazardous mixes stay SILENT here — HazardousMixReactor owns that consequence theatre (smoke/fire/alarm) and double-messaging would bury it. Feedback only: no new graded mistakes.

```csharp
static bool ShouldAnnounceWrongMix(HazardousMix.HazardOutcome outcome)
static bool ShouldShowObservation(bool sameRule, float now, float lastAt, float window
void Bind(LiquidPhysics lp)
```

### `FlameMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/NakedFlameHeat.cs`</sub>

Pure rules for NAKED-FLAME heating (Exp 6's dry distillation — the ONE step in any experiment that heats over an open flame; everything else is a ≤100 °C water bath). A vessel held over a LIT burner climbs toward a red-glow temperature the bath can never reach.

```csharp
const float Reach
const float MaxC
const float RatePerSecond
static bool Heats(float distance)
static float NextTemp(float current, float dt)
```

### `NakedFlameHeat` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/NakedFlameHeat.cs`</sub>

Zone-free open-flame heat on every Bunsen burner (wired by the Apply Verb Data menu): while LIT, any vessel within reach of the flame anchor heats toward 400 °C. This is what the hard-glass tubes exist for — the water bath owns every gentler step.

```csharp
void Bind(BurnerController burner)
void HeatVessel(LiquidPhysics vessel, float dt)
```

### `OrbitMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/OrbitMath.cs`</sub>

Pure circular-motion accumulator shared by the STIR and GRIND verbs: feed it the tool tip's XZ offset from the vessel/bowl axis each frame; while the tip stays inside the working radius the swept angle accumulates (direction-agnostic, per-sample clamped so teleports/jitter can't cheat). Leaving the zone PAUSES progress — it never resets (a student lifting the rod to check shouldn't lose their work).

```csharp
float requiredRevs
const float MaxDegPerSample
float Progress01
bool IsDone
float SweptDegrees
void Feed(float x, float z, bool inside)
void Reset()
```

### `RestPose` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PhysicsProfiles.cs`</sub>

How an item plausibly rests on a bench when nothing holds it.

### `PhysicsProfile` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PhysicsProfiles.cs`</sub>

```csharp
float massKg
RestPose pose
PhysicsProfile(float mass, RestPose p)
```

### `PhysicsProfiles` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PhysicsProfiles.cs`</sub>

Physics-attribute table for the ChemLab prefabs — the companion to RealSizes (used by the physics/resting-pose audit tool). Name → realistic mass + plausible resting pose, plus the pure math that turns a pose into a rotation and the guards the audit tool uses (degenerate colliders, resting plausibility). Policy: items spawn KINEMATIC on the shelf; GrabPhysicsPolicy flips them dynamic on first release so a dropped glass rod falls and lies on its side instead of freezing mid-air or balancing upright.

```csharp
static int Count
static IEnumerable<string> Names
static bool TryGet(string prefabName, out PhysicsProfile profile)
static Quaternion RestRotation(RestPose pose, Vector3 boundsSize)
static bool IsRestingPlausible(RestPose pose, Vector3 worldSize)
static bool IsDegenerate(Vector3 colliderWorldSize, float minDim
static Rigidbody EnsurePhysics(GameObject go, string prefabName)
static int ConvexifyMeshColliders(GameObject go)
static Collider EnsureCollider(GameObject go)
```

### `PlacementAnchor` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PlacementAnchor.cs`</sub>

A tiny draggable marker positioned by hand in the editor to tell a verb exactly WHERE something happens on an imported mesh — the flame on a match head, the flame on a burner mouth, the bowl of a scoopula. Bounds-based guessing can't know a model's axis convention, so the code reads this anchor's position and falls back to a heuristic only when no anchor is present. Convention: a child named "FlameAnchor" (Matchstick / BurnerController) or "ScoopAnchor" (ScoopController). Drag it in the Scene view, then Lock My Layout to bake it. The gizmo just makes it easy to see/select.

```csharp
Color gizmoColor
float gizmoRadius
bool previewsScale
bool previewsTube
const float TubeHeight
const float TubeRadius
```

### `PlayerAvatarRig` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PlayerAvatarRig.cs`</sub>

Drives the mirror-only first-person avatar: stands the body at the player's feet, turns it to the head's yaw, and feeds the Animation-Rigging IK targets (head + both hands) from the HMD and controllers so the reflection moves with you. Attach to the avatar root; `PlayerAvatarBuilder` wires the transforms. The avatar renders on the PlayerAvatar layer, which the main camera culls and the mirror includes — so you see this only in the mirror.

```csharp
void Bind(Transform h, Transform l, Transform r,
void SetFootOffset(float offset)
static Vector3 FootUnder(Vector3 headPos, float floorY, float offsetY)
```

### `PlayerTriggerRelay` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/PlayerTriggerRelay.cs`</sub>

Fires an event when the PLAYER (any collider under the rig root) enters this trigger volume — used by the door-gate approach + threshold zones. Ignores props, NPCs and stray physics bodies.

```csharp
UnityEvent onPlayerEntered
void SetPlayerRoot(Transform t)
void SimulateEnter()
```

### `RackDispenserMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/RackDispenser.cs`</sub>

Pure rules for a capped tube rack (an unmovable rack that, when grabbed, pulls out single items, with a cap so the maximum is controlled). Unlike the ConsumableDispenser (endless single-piece box for matches/litmus), a rack holds a FINITE set of REUSABLE tubes — one persistent instance per hole. Pulling one leaves its hole empty; the tube stays a normal grabbable (fillable, cleanable, breakable → returns home). The cap is the hole count by construction: you can never have more tubes out than the rack was built with.

```csharp
static bool InHole(bool held, float distFromHole, float slotRadius
static int OutCount(int capacity, int seated)
static string Label(string kind, int seated, int capacity)
```

### `RackDispenser` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/RackDispenser.cs`</sub>

A fixed rack that holds a capped pool of reusable tubes. The rack itself is inert furniture (no grab, kinematic); each hole owns one persistent tube whose DropRespawn home is that hole, so a tube pulled out is a free grabbable and an abandoned one finds its way back. Shows a live "seated/capacity" count. Built by WorkspaceKitsBuilder; nothing to author by hand.

```csharp
int Capacity
void Bind(Transform[] holes, GameObject[] tubes, ProximityLabel label, string kind)
int SeatedCount()
```

### `RealSizes` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/RealSizes.cs`</sub>

Real-world size table for the ChemLab prefabs: name → LONGEST dimension in metres. The pack's meshes are authored true-to-scale, so these pin each item to its realistic size and normalisation scales by the longest axis — the old bounds-HEIGHT normalisation inflated flat tools (spatula, iron ring, wire gauze) by 3-16x, which read as comically massive in-headset.

```csharp
static int Count
static IEnumerable<string> Names
static bool TryGet(string prefabName, out float longestMeters)
static float UniformScaleFactor(Vector3 currentWorldSize, float targetLongest)
```

### `ScoopMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ScoopController.cs`</sub>

Pure rules for scooping solids (solid reagents are scooped: each scoop adds a fixed amount, and the heap visual grows and so does the scale reading). Kept plain so the suite pins the policy.

```csharp
const float GramsPerScoop
const float GramsPerSpatula
static bool CanPickUp(bool carrying, PhysicalState state, float availableMl)
static float ScoopCharge(float availableMl, float perScoop
static bool CanDeposit(bool carrying, bool sameContainer)
static bool DepositsOnTouch(bool vesselIsBound, bool expectsChemical)
static string DepositLabel(string chem, float addedG, float totalG)
```

### `ScoopController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ScoopController.cs`</sub>

Scoopula/spatula verb: dip into a solid reagent's jar to pick up a fixed charge (a visible tinted heap rides the blade), touch a receiving vessel to deposit it — per-scoop FloatingText totals, and the balance/VesselStatus update through the normal contents path. Self-contained: no scene authoring needed beyond adding the component (the proximity probe works off renderer bounds; only a HELD scoop transfers, so shelf contact never scoops).

```csharp
bool Carrying
void Bind(XRGrab grab)
void SetGramsPerDip(float grams)
float GramsPerDip
void SetHeapScale(Vector3 s)
Vector3 HeapScale
bool Dip(LiquidPhysics lp)
bool Deposit(LiquidPhysics lp)
Vector3 BladeTip
static float MoundFill(float grams, float fullAtG
```

### `HeightCalibration` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SeatedHeightBoost.cs`</sub>

Pure fixed-eye-height math (edit-mode testable). Design: per-scene FIXED eye height, NOT relative to the player's real height — the Quest/Link runtime flip-flops between Floor and Device origin spaces across sessions, so any "relative" scheme produces floor-spawns or roof-spawns. Measure where the headset says the head is ONCE at load (only from a VALID, SETTLED pose), then offset the rig so the eye line lands exactly on the scene's authored height. Offset may be NEGATIVE (pulls a too-high pose back down). Gravity (move providers, m_UseGravity on) keeps the rig grounded.

```csharp
const float MaxAdjust
const float TallTolerance
static float FixedOffset(float targetEye, float trackedHeadY)
const float ShortTolerance
static float TallExcess(float eyeNow, float target)
static bool PoseValid(Vector3 headLocalPos, float lastY)
```

### `SeatedHeightBoost` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SeatedHeightBoost.cs`</sub>

Thin driver on the XR Origin (class name kept so existing scene wiring stays valid — behaviour is FIXED height, no seated boost / auto-levitate). Counting UNTRACKED (0,0,0) poses during the load fade as settled measures y≈0, applies the max +1.5 offset, and the real head height then stacks on top (eye ≈ 2.5 m; the capsule balloons into the lab ceiling and depenetration shoves the rig onto the roof). So: - calibration only accepts a valid, settled pose (see PoseValid); - the offset is held at 0 until calibrated (a stale Device-mode 1.36 can never balloon the capsule during load); - the roof-recovery guard runs CONTINUOUSLY, not once. Recalibrates only on scene load / explicit Recalibrate() — never mid-play. Per-scene targets set by Tools ▸ PharmaSynth ▸ Wire Spawn Height.

```csharp
float TargetEyeHeight
bool Calibrated
float AppliedOffset
void Bind(Transform cam, Transform offset)
void SetTarget(float eyeHeight)
void Recalibrate()
```

### `SelectSfx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SelectSfx.cs`</sub>

Plays a SoundBank key when this interactor selects something — used for the station snap sockets ("socket-snap" as the prop clicks into place).

```csharp
void Bind(XRInteractor interactor, string soundKey)
```

### `SimLoopAudio` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SimLoopAudio.cs`</sub>

Looping apparatus audio for a sim-driven station (§4): boil bubbles while heating, drips while filtering, hiss while collecting gas, shimmer while crystallising. Owns a positional AudioSource; ZoneSimStation drives SetRunning from zone occupancy. Safe with missing clips (silent no-op).

```csharp
static string KeyFor(StationSim kind)
void Bind(string soundKey)
bool IsPlaying
void SetRunning(bool on)
```

### `SpawnBurstFX` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SpawnBurstFX.cs`</sub>

Cyan "materialize" spawn burst: a one-shot column of cyan particles that rises from the player's feet like smoke on every teleport / reset / spawn — the classic game spawn animation. Scene singleton (mirrors ScreenFader); triggers fire it null-safely via SpawnBurstFX.Instance?.PlayAtPlayer().

```csharp
static SpawnBurstFX Instance
void PlayAtPlayer()
void PlayAt(Vector3 feet)
```

### `SpillMistake` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SpillMistake.cs`</sub>

Grades reagent spills (§2 mishandling penalties). LiquidPourer already drains any bottle tipped past its pour threshold — this watches for that happening while NOBODY is holding the bottle (knocked over / dropped) and records one SpilledReagent mistake per episode, re-arming once the bottle is righted. The lost volume itself is the second penalty: it comes out of the finite supply, so a bad spill can starve a step into the existing restart-prompt path.

```csharp
void Bind(ExperimentRunner runner, LiquidPhysics liquid, XRGrab grab, string label)
static Color LiquidColorOf(LiquidPhysics liquid)
```

### `SpillPuddle` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/SpillPuddle.cs`</sub>

A liquid puddle left on the ground by a spilled/broken vessel: it lingers ~3 s, then smoothly fades away. Spawn() drops a flat translucent disc at the impact point (raycast to the floor), tinted with the spilled chemical's colour; it lingers, then fades out and destroys itself. Purely visual — the graded penalty + supply loss already happen in SpillMistake/LiquidPourer/BreakableGlassware.

```csharp
static float Alpha01(float age, float linger, float fade)
static SpillPuddle Spawn(Vector3 worldPos, Color liquidColor, float radius
```

### `StationSocketFilter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/StationSocketFilter.cs`</sub>

Select filter for a station's snap socket (§2 sockets): the socket accepts only the station's required item, so the beaker clicks onto the heat pad but a random spatula bounces off. Empty requiredItemId = accept anything.

```csharp
string requiredItemId
bool canProcess
bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
static bool Matches(string requiredId, LabItem item)
```

### `StationStatusLabel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/StationStatusLabel.cs`</sub>

Live status on a sim station's billboard label (the temperature shown on heaters etc.): Heat shows "62 C -> 150 C", Filter/Collect/ Crystallise show percent progress. Throttled + change-gated; formats live in VesselStatusMath so the suite pins them.

```csharp
void SetIgnitionHint(System.Func<bool> isLit)
void Bind(TMP_Text tmp, string baseLabel, StationSim kind,
void Refresh()
string ComposeStatus()
```

### `StationVfx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/StationVfx.cs`</sub>

Per-station particle effects (special effects for boiling, freezing etc.), the visual twin of SimLoopAudio: while the required prop occupies a sim station the effect plays — steam for Heat (boiling), a frosty sparkle for Crystallise (freezing/ice bath), a falling drip for Filter, rising bubbles for Collect. Attached + bound by ExperimentSceneBuilder per station; ZoneSimStation drives SetRunning from occupancy. The ParticleSystem is built procedurally on first use (no asset dependencies — Quest-cheap, ≤60 live).

```csharp
static string StyleFor(StationSim s)
bool IsPlaying
void Bind(StationSim kind)
void SetRunning(bool on)
```

### `StirController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/StirController.cs`</sub>

The STIR verb: circle the glass rod inside this vessel's mouth while it holds liquid and the stir task completes — works with the vessel on the table OR held in a hand (everything is vessel-relative). Progress pops as floating text; the TaskGraph condition owns completion (Retry-safe via the ZoneSimStation resubscribe pattern).

```csharp
OrbitMath Math
string TaskId
void Bind(ExperimentRunner runner, string taskId, LiquidPhysics lp, Transform rod,
void Detach()
void Register()
bool RodInMouth()
void Tick(float x, float z, bool inside)
```

### `TargetRole` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TaskTargetRegistry.cs`</sub>

What role an object plays in a step — drives the guidance tint and whether grabbing it should silence the glow.

### `TaskTarget` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TaskTargetRegistry.cs`</sub>

One highlightable object for one task.

```csharp
Transform transform
TargetRole role
bool stayLitWhenHeld
VerbKind verb
bool pool
```

### `TaskTargetRegistry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TaskTargetRegistry.cs`</sub>

taskId → the scene objects that step involves. Fed by the TutorialTargets sweep, the single source of truth (no module stages an ExperimentTaskStation, so station self-registration would find nothing). Components do not self-register, so there is no second lifetime to keep in sync.

```csharp
static void Register(string taskId, Transform t, TargetRole role, bool stayLitWhenHeld,
static Transform ChoosePoolMember(IReadOnlyList<TaskTarget> targets,
static Transform PickTarget(string taskId, Vector3 from)
static IReadOnlyList<TaskTarget> Targets(string taskId)
static Transform PickTarget(string taskId)
static int TaskCount
static void Clear()
```

### `TutorialTargets` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TaskTargetRegistry.cs`</sub>

Builds the taskId → objects map by sweeping the live scene once per run. Deliberately DERIVED from the components that actually complete each task rather than from an authored per-task list. An authored list can drift from the binding it describes — the bug class of (a hint whose ACTION line contradicted the binding it had to satisfy). Deriving makes that disagreement structurally impossible: if the sweep points at it, it is the thing that completes the step.

```csharp
static readonly List<string> LastUnresolved
static void Build()
static bool Visible(Component c)
static void AuditAgainst(IEnumerable<ExperimentTask> tasks)
```

### `TimelapseMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TimeSkipController.cs`</sub>

Pure timelapse maths (suite-pinned): day/night tint and the on-screen counter.

```csharp
static readonly Color Night
const float DefaultSpeed
static int Cycles(float hours)
static Color Tint(float k, float hours)
static bool IsNight(float k, float hours)
static string Counter(float k, float hours)
```

### `TimeSkipController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TimeSkipController.cs`</sub>

Compresses a real-world wait the player can't sit through (once the mixture requirement is met, the screen fades black for 2 s, and when it returns a success text says the time has passed and the step is done; this covers every procedure in every experiment that needs lengthy time). A task authored longProcess=true plays a TIMELAPSE on completion: fade black, a security-camera view of the lab from its top corner (Cam_Timelapse) running through days and nights with a counter, fade black, then back to play with a "time has passed" message. Without the cinema rig it falls back to a plain black hold. Zone-free and experiment-agnostic: it keys off the task flag, so any module's week-long fermentation / overnight dry / hour-long crystallisation reuses it for free.

```csharp
static bool IsTimeSkip(ExperimentTask t)
static string MessageFor(ExperimentTask t)
void Bind(ExperimentRunner r)
static bool IsSkipping
void PlayTimelapse(ExperimentTask t)
```

### `TubeSlotMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TubeRackSlots.cs`</sub>

Pure rules for snapping a released test tube into a workspace holder slot of Experiment_Tube_Table_Kit_Holder_1 to 4: any tube, empty or filled, released near a free slot. The green Slot_* anchors are hand-placed editor markers in the holder's holes; this is their runtime half: release any tube (regular or hard-glass — both carry itemId kit-testtube) near a free slot and it seats upright in the hole.

```csharp
const float SnapRadius
static bool CanSnap(bool held, bool kinematic)
static float BottomAlignDelta(float slotY, float boundsMinY)
static int NearestFreeSlot(Vector3 tubePos, IReadOnlyList<Vector3> slotPos,
```

### `TubeRackSlots` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TubeRackSlots.cs`</sub>

Runtime half of the workspace tube holders: watches every kit-testtube in the lab and seats a released one into the nearest free Slot_* anchor. A seated tube is frozen kinematic (the same end state DropRespawn's settle-freeze produces, so the physics policy chain — grab → dynamic → release — keeps working); grabbing it back frees the slot. One component per holder, wired by "Name Tubes + Build Rack Slots".

```csharp
void Bind()
int SlotCount
```

### `TutorialHighlighter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/TutorialHighlighter.cs`</sub>

Drives Tutorial Mode's guidance off the task graph: whatever step is available right now, its objects glow. ⭐ It NEVER decides that a step is DONE. This is a pure read of AvailableTasks(); the task completes through its existing binding exactly as in campaign, and the glow follows on the next poll because the available set changed. Since no parallel completion detector exists, the guidance cannot disagree with the game — which is the failure mode this design avoids.

```csharp
void Bind(ExperimentRunner r)
void Detach()
static bool ShouldLight(TaskTarget target, bool held, bool taskAvailable)
static bool GuidanceAllowed(bool running, bool skipping)
static bool IsHeld(Transform t)
void SetGlowMaterials(Material source, Material target)
```

### `VaporMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VaporCollectController.cs`</sub>

Pure rules for VAPOR COLLECTION (Exp 6: distill the acetone off the glowing acetates at 56 °C into a receiver tube).

```csharp
const float DeliveryRadius
const float MlPerTick
static bool Fires(float sourceTempC, float requiredC, float sourceMl)
static bool MayReceive(bool advertises, bool candidate, bool held)
static bool Prefer(bool held, float d, bool bestHeld, float best)
```

### `VaporCollectController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VaporCollectController.cs`</sub>

The dry-distillation product stream, zone-free (the fermentation pattern): once the collect task is AVAILABLE and the source tube is at temperature, it converts its charge into the module's product, condensing into the nearest nearby vessel whose binding EXPECTS the product — targeted, so the stream can never pollute the water bath or a bystander tube.

```csharp
string VaporTaskId
LiquidPhysics Source
float RequiredC
void Bind(ExperimentRunner runner, LiquidPhysics source, string taskId,
void Detach()
bool Advertises(LiquidTaskBinding b)
bool IsCandidate(LiquidTaskBinding b)
bool EmitTick(LiquidPhysics receiver)
```

### `VerbKind` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VerbDemoMath.cs`</sub>

Which motion a step is asking for. DERIVED in TutorialTargets.Build() from the component that actually completes the step — never authored per task, for the same reason the target sweep is derived: an authored list drifts from the binding it describes, and a demonstration that mimes the wrong verb is worse than none. Deliberately only FIVE. Heat, chill, weigh, litmus, flame and collect are all "carry this vessel to that tool", so they share `Place` rather than each earning a bespoke curve that would look identical on screen.

### `VerbPose` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VerbDemoMath.cs`</sub>

Where the demonstration ghost is, at a moment in its loop.

```csharp
Vector3 position
Quaternion rotation
```

### `VerbDemoMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VerbDemoMath.cs`</sub>

Pure motion curves for Tutorial Mode's verb demonstration: given a verb and two world points, where should the ghost be at normalised time t? Pure so the suite can pin the SHAPE of each motion without a headset — that a pour actually tips past horizontal, that a stir completes real revolutions, that a travel arc never sinks through the bench. A curve that looks plausible in code and does nothing useful in VR is exactly what this file exists to prevent. No hand is modelled. The ghost is a copy of the object the player must move, which is both cheaper (no rig, no clips, no new art) and clearer: the thing that moves on screen is the thing they have to pick up.

```csharp
const float ArcHeight
const float PourTiltDeg
const float WorkHeight
const float CircleRadius
const float ScoopDipDepth
const float ScoopTipDeg
const float DefaultRevs
static float Ease(float t)
static float Phase(float t, float a, float b)
static Vector3 Arc(Vector3 from, Vector3 to, float t01)
static float StirAngleDeg(float t01, float revs
static Vector3 TiltAxis(Vector3 from, Vector3 to)
static VerbPose Sample(VerbKind kind, float t01, Vector3 from, Vector3 to,
```

### `VerbDemoPlayer` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VerbDemoPlayer.cs`</sub>

Tutorial Mode's verb demonstration: a translucent ghost of the object the player has to move, miming the actual motion of the step, twice, then gone. ⭐ Deliberately NOT a hand. Rigging and animating a pair of hands is days of art for a hint, and it answers the wrong question — the player already knows what a hand looks like. Ghosting the BOTTLE tells them which object moves, from where, to where, and how it is held at the end of the motion. The mesh-copy machinery is the same one TutorialHighlighter's glow shell already proved. Fires only on request (coach level 2, or poking Pharmee for help). Never loops ambiently: a permanently miming ghost is noise, and it would compete with the glow for the player's attention instead of reinforcing it.

```csharp
void Bind(ExperimentRunner r, Material ghost)
bool IsPlaying
static List<TaskTarget> CollapsePool(IReadOnlyList<TaskTarget> targets, Transform chosen)
static bool Endpoints(IReadOnlyList<TaskTarget> targets,
void Show(string taskId)
void Stop()
```

### `VesselChillTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselChillTask.cs`</sub>

Completes a chill step ZONE-FREE: the task is done when this vessel actually HOLDS something and has been brought down to the required temperature — however and wherever the player cooled it. The cold twin of VesselHeatTask (Exp 4's "cool in an ice bath; crystallise"). Ambient is 25 °C, so a vessel can never satisfy a chill threshold by simply standing on the bench.

```csharp
string TaskId
float RequiredC
bool Relevant
static bool ShouldComplete(bool allReagentsIn, bool hasContents, float tempC, float requiredC)
void Bind(ExperimentRunner runner, string taskId, float requiredC, LiquidPhysics lp,
void Detach()
```

### `FlameTestMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselFlameTask.cs`</sub>

Pure rules for the FLAME (non-)flammability confirmation (Exp 7: "Try to ignite the chloroform. Light a match and place the flame near the vapors. Observe." — the NEGATIVE is the observation). Zone-free per the client rule: any lit flame brought to the served sample, anywhere in the lab.

```csharp
const float Reach
static bool Confirms(bool served, bool flameLit, float distance)
static Vector3 FlamePos(Component burner)
```

### `VesselFlameTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselFlameTask.cs`</sub>

Completes a flammability-test step ZONE-FREE: once the sample vessel is served, holding any LIT match or burner flame to it confirms the result ("does not ignite") and completes the task. Wired by the scene builder from ExperimentLayout.Vessel.flameTaskId.

```csharp
string TaskId
void Bind(ExperimentRunner runner, string taskId, LiquidTaskBinding binding, LiquidPhysics lp)
void Detach()
bool PollFlames()
```

### `VesselHeatTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselHeatTask.cs`</sub>

Completes a heat step ZONE-FREE: the task is done when this vessel has been served all its reagents AND actually reached the required temperature — however and wherever the player heated it. Replaces the old fixed Station_prep-hydrolysis (deleted with its pad/label/teleport anchor).

```csharp
string TaskId
float RequiredC
bool Relevant
static bool ShouldComplete(bool allReagentsIn, float tempC, float requiredC)
void Bind(ExperimentRunner runner, string taskId, float requiredC,
void Detach()
```

### `VesselLitmusTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselLitmusTask.cs`</sub>

Completes a litmus-confirmation step ZONE-FREE (Exp 4's "blue litmus turns red"; Exp 8's "red litmus turns blue"): the task is done when this vessel has been served its reagents AND a litmus strip actually touched it while the mixture read a DEFINITIVE pH — red on acid, blue on base — wherever the player does it. A neutral read (water first, nothing dissolved yet) marks nothing; touch again with a fresh strip once the product is in.

```csharp
string TaskId
static bool ShouldComplete(bool allReagentsIn, bool stripReadDefinitive)
void NotifyRead(float pH)
void Bind(ExperimentRunner runner, string taskId, LiquidTaskBinding binding, LiquidPhysics lp)
void Detach()
```

### `VesselStatus` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselStatus.cs`</sub>

Live contents readout on a vessel's existing ProximityLabel: contents text on hover or when near. Throttled and change-gated so the TMP mesh only rebuilds when the state actually moves.

```csharp
void SetRoleSuffix(string suffix)
void Bind(LiquidPhysics lp, ProximityLabel label, string displayName, float showDist
void Refresh()
```

### `VesselWeighTask` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/VesselWeighTask.cs`</sub>

Completes a WEIGH step ZONE-FREE on the bench balance (Exp 6's "weigh 7 g of each acetate"): the task is done when the vessel has been served its solids AND is resting SETTLED on the balance pan — the balance narrates the grams live the whole time.

```csharp
string TaskId
static bool ShouldComplete(bool allReagentsIn, bool settledOnPan)
void Bind(ExperimentRunner runner, string taskId, LiquidPhysics lp, LiquidTaskBinding binding)
void Detach()
```

### `WalkBob` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WalkBob.cs`</sub>

Subtle head-bob while the player is locomoting, for a grounded, "walking" feel. Applied to the XR camera's local offset so it never fights head tracking — and kept small + speed-scaled for VR comfort (bob only when actually moving). Amplitude is intentionally low; expose it so a comfort setting can zero it out.

### `WatchMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WatchMath.cs`</sub>

Pure fitted-watch geometry (edit-mode testable). The authored WatchModel is a SOLID Tripo mesh — a solid object can never encircle a wrist the way a real watch wraps. Instead we MEASURE the hand mesh's wrist cross-section and generate an elliptical band around it, with a disc face on top. Everything here is deterministic math on plain data.

```csharp
const float BandClearance
const float BandTube
const float MinHalfWidth
const float MinHalfHeight
const float MaxHalfWidth
const float MaxHalfHeight
```

### `WristSlice` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WatchMath.cs`</sub>

```csharp
static WristSlice MeasureSlice(IList<Vector3> points, float sliceZ, float tol)
static Vector2 BandRadii(Vector2 wristHalfExtents)
static float FaceDiameter(float bandRadiusX)
static Mesh BuildBandMesh(Vector2 radii, float tube, int loopSegs, int tubeSegs)
```

### `WaterBathMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WaterBathController.cs`</sub>

Pure rules for the ZONE-FREE water bath (no fixed zone: the entire lab IS the zone, and tools function when brought together ANYWHERE). The bath is a real tool with real requirements, exactly like the manuscript's: fill it with water, put a lit burner under it, and it warms whatever vessel you bring to it — wherever in the lab it happens to sit.

```csharp
const float MinWaterMl
const float BathMaxC
const float BurnerRadius
const float VesselRadius
static bool HasWater(float waterMl)
static bool IsHeating(bool hasWater, bool litBurnerNear)
static float EffectRadius(float anchorScaleX, float fallback)
static string StatusLine(bool hasWater, bool litBurnerNear, float bathC)
```

### `WaterBathController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WaterBathController.cs`</sub>

The bath itself: LiquidPhysics holds the water the player pours in, TemperatureSim heats while a lit burner sits near, and every vessel brought close takes the bath's temperature — which is what releases temperature-gated reactions (Tollens mirror, ester odours, the hydrolysis boil) and satisfies VesselHeatTask steps. No station, no pad, no fixed position: carry the bath anywhere, it still works.

```csharp
Vector3 HeatZoneCenter
float HeatZoneRadius
float BurnerZoneRadius
float BathC
bool HasWater
void Bind(LiquidPhysics lp, TemperatureSim temp, ProximityLabel label)
void HeatVessel(LiquidPhysics vessel)
void DriveForTest(float dt)
```

### `WaypointBeacon` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WaypointBeacon.cs`</sub>

Animates the waypoint marker: a circular glow that sits on the target surface (pulsing gently) and a downward arrow bobbing above it — a clear "go/act here" signal instead of an ambiguous floating blob. WaypointGuide moves the root; this only animates the children in local space.

### `WaypointGuide` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WaypointGuide.cs`</sub>

Floats a marker/beacon above the station for the current available step, guiding the player where to go next (storyboard: "follow the markers"). Hides when nothing is available (between steps / finished).

```csharp
void SetRunner(ExperimentRunner r)
void SetMarkerScale(float metresAtReference)
static float DistanceScale(float distance, float referenceDistance,
void SetPlacement(float height, float clearance, float front,
static Vector3 MarkerPosition(Bounds solid, float heightOffset, bool caged,
string CurrentTargetTaskId
```

### `WeighMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WeighMath.cs`</sub>

Pure weighing rules: the balance pan auto-tares the vessel, so the display reads the CONTENTS in grams (1 g/ml proxy — real densities are out of scope per the client's record-only-yield ruling). The suite pins these.

```csharp
static float MassOf(float liquidMl, float solidG
static float PanMass(float contentsMlOrG, float rbMassKg)
static bool WithinTolerance(float massG, float targetG, float tolFrac
const float BumpGraceSeconds
static bool WithinBumpGrace(float secondsOff)
static bool PanSettled(float secondsOnPan, float minSeconds
```

### `WeighStation` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WeighStation.cs`</sub>

A functional balance: whatever rests on the pan drives the live grams display (auto-tared contents, 1 g/ml proxy), and a weigh-* task completes once the CORRECT load sits settled on the pan — either a vessel holding enough of the required chemical (Aspirin: flask with ~50 ml Salicylic Acid) or the required tool (Acetone: the acetate scoopula). TaskGraph condition + ExperimentStarted resubscribe = Retry-safe (the ZoneSimStation pattern). The pan is a trigger volume over the Balance model.

```csharp
LabItem OccupantItem
LiquidPhysics OccupantVessel
string TaskId
float SecondsOnPan
bool SettledWith(LiquidPhysics lp)
void Bind(ExperimentRunner runner, string taskId, string requiredItemId,
void Register()
bool IsSatisfied
void ForceLoad(LabItem item, LiquidPhysics vessel, float secondsAgo)
void AutoFindScale()
bool HasScale
```

### `WeighingScaleController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WeighingScaleController.cs`</sub>

The balance's digital head. Drives the grams readout from a target mass, the way a bench balance does: the reading TYPES IN, wobbles while the load settles, then locks (like a real digital scale). All display maths is pure so the suite pins it without a scene.

```csharp
UnityEvent onTargetReached
float CurrentMass
string DisplayText
void Bind(TMP_Text display)
void Tick(float dt)
void SetTargetMass(float newTarget)
static float Reading(float shown, float target, float wobbleG, float phase)
static string Typed(string text, float t, float charsPerSecond)
static string Format(float grams, string unit)
```

### `WorkspaceShelfMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/WorkspaceShelfMath.cs`</sub>

Pure geometry for the center-table overhead shelf. One row of flush platform tiles sits on the gantry rails (front z=-3.15, back z=-3.50, tops y≈1.55). A SECOND, lower row (plank centres y≈1.200) for the apparatus kits — this math describes BOTH rows so the builder can rebuild them cleanly and the kit layout can target either. Values match the hand-placed planks (row pitch, z centre, depth). Kept plain so the suite pins coverage + heights.

```csharp
const int Rows
const float Thickness
const float ZCenter
const float Depth
const float XMin
const float Gap
static float RowCenterY(int row)
const float TileCenterY
static float TopY
static float TopYOf(int row)
static float LowerRowHeadroom
static Vector3 TileCenter(int i, int count, int row
static Vector3 TileSize(int count)
static Vector3 LipCenter(int row
```

### `ZoneItemSensor` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ZoneItemSensor.cs`</sub>

Tracks whether the required grabbable LabItem is currently INSIDE this trigger volume (continuous occupancy, not one-shot completion). Rigs use this to run continuous verbs — e.g. the burner heats only while it sits in the heating zone, gas collects only while the collection tube is held in place.

```csharp
bool IsOccupied
string ItemId
LabItem Occupant
void SetItemId(string id)
void ForceOccupied(bool on)
```

### `ZoneSimStation` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Interaction/ZoneSimStation.cs`</sub>

Turns a layout zone station into a REAL sustained verb (generalising MethaneApparatusRig to every experiment): while the required prop occupies the zone the bound chemistry sim advances, and the station's task auto-completes via a TaskGraph condition once the sim reaches its target — the player must actually perform and hold the action, not just drop the prop in. Attached + configured by ExperimentSceneBuilder for stations whose layout sets a StationSim other than None. Deterministically testable (ForceOccupied + Tick).

```csharp
float heatSourceC
float heatTargetC
float filtrateMlPerSec
float gasMlPerSec
float heatRadius
string TaskId
StationSim Kind
void SetLoopAudio(SimLoopAudio loop)
void SetVfx(StationVfx vfx)
void SetIgnitionGate(System.Func<bool> gate)
void Bind(ExperimentRunner runner, string taskId, StationSim kind, ZoneItemSensor sensor,
void Register()
void Drive(float dt, bool occupied)
```

---

## Scoring

`Assets/PharmaSynth/Scripts/Scoring/`

### `GradingConfig` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/ExperimentGrader.cs`</sub>

Tunables for how mistakes erode rubric sub-scores. Client-adjustable.

```csharp
float procedureMistakeStep
float safetyMistakeStep
float sanitationMistakeStep
```

### `ExperimentGrader` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/ExperimentGrader.cs`</sub>

Turns an experiment attempt's run-state (task completion + mistakes + time + quiz) into the per-criterion sub-scores, then the final GradeBreakdown via ScoreCalculator. This is the missing link between the TaskGraph/MistakeLog and the grade screen numbers (Grade %, per-criteria breakdown). Pure C#, unit-testable.

```csharp
ExperimentGrader(ScoreCalculator calc, GradingConfig cfg
```

### `BktParameters` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/MasteryModel.cs`</sub>

Bayesian Knowledge Tracing parameters (per module, tunable in the inspector). Defaults from plan §3.6.

```csharp
float pL0
float pTransit
float pSlip
float pGuess
```

### `MasteryModel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/MasteryModel.cs`</sub>

Bayesian Knowledge Tracing mastery estimator (plan §3.6, Logic Tier). Tracks P(learned) per LabSkill; the 90% gate reads OverallMastery(). Plain C# so the BKT math is unit-testable. One instance per experiment attempt.

```csharp
MasteryModel(BktParameters parameters, IEnumerable<LabSkill> trackedSkills)
IReadOnlyList<LabSkill> TrackedSkills
void Observe(LabSkill skill, bool correct)
bool IsTracked(LabSkill skill)
float GetMastery(LabSkill skill)
float OverallMastery()
bool IsMastered(float threshold)
void Reset()
```

### `RubricWeights` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/ScoreCalculator.cs`</sub>

Per-experiment rubric weights (WCC lab manual rubric, plan §3.6). Defaults sum to 1.0 but need not — ScoreCalculator normalizes, which handles the manual's inconsistent printed weights (flagged for client sign-off).

```csharp
float Total()
float WeightOf(RubricCategory c)
```

### `GradeBreakdown` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/ScoreCalculator.cs`</sub>

Per-criterion contribution and the final grade, all in percent (0..100).

```csharp
float Procedure
float ChemicalTests
float MaterialsAndPPE
float TimeManagement
float Sanitation
float Documentation
float Total
```

### `ScoreCalculator` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Scoring/ScoreCalculator.cs`</sub>

Turns per-criterion sub-scores (each 0..1) into a weight-normalized grade %.

```csharp
ScoreCalculator(RubricWeights weights)
GradeBreakdown Compute(IDictionary<RubricCategory, float> subScores)
const float GraceFactor
static float TimeSubScore(float elapsedSeconds, float parSeconds)
```

---

## Progression

`Assets/PharmaSynth/Scripts/Progression/`

### `DemoConfig` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/DemoMode.cs`</sub>

Backend demo-mode switch: panelists need a fast run-through with auto-complete controls, while Chapter-3 study participants must see the untouched normal flow. A config FILE decides whether the cube-room menu even shows the "Demo Mode" button; pressing that button starts a demo SESSION (separate throwaway save, all periods unlocked, HUD auto-complete controls, infinite reagents). Config off = zero footprint.

```csharp
bool demoEnabled
bool infiniteSupply
```

### `DemoMode` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/DemoMode.cs`</sub>

Pure config resolution + demo save-path mapping (edit-mode testable).

```csharp
static bool IsEnabled
static bool InfiniteSupply
static void SetResolved(DemoConfig config)
static DemoConfig Resolve(string persistentJson, string streamingJson)
static string ProductFor(string moduleId)
static bool IsEndProduct(string chemicalName)
static string SavePathFor(bool demoActive, string normalPath)
```

### `DemoSession` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/DemoMode.cs`</sub>

Whether THIS play session was entered through the Demo Mode button. (Static so it survives the menu→lab scene load, like GameFlow.SelectedModuleId.)

```csharp
static bool Active
```

### `DemoModeLoader` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/DemoModeLoader.cs`</sub>

Reads the demo-mode config at startup (on the Services GO in both scenes) and installs it into DemoMode. Two locations: • StreamingAssets/demo-config.json — the shipped default. On Android it lives INSIDE the APK, so it must be read via UnityWebRequest. • persistentDataPath/demo-config.json — the field override; wins when present.

```csharp
const string FileName
```

### `ExperimentPeriod` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ExperimentCatalog.cs`</sub>

The four progression periods the experiments are grouped into (plan §3.2). Period doors open in order once the previous period is fully passed.

### `CatalogEntry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ExperimentCatalog.cs`</sub>

One roster entry: its module id (must match the ExperimentModuleDefinition asset's moduleId), the asset file name, its period, the single prerequisite that gates it, and the build tier (1–3, informational).

```csharp
string moduleId
string assetName
string title
ExperimentPeriod period
string prerequisiteModuleId
int tier
CatalogEntry(string id, string asset, string title, ExperimentPeriod period, string prereq, int tier)
```

### `ExperimentCatalog` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ExperimentCatalog.cs`</sub>

The ordered 9-experiment roster — the client's period grouping, which is exactly the manuscript's 8 bench labs (Exp 2-9) plus the methane tutorial. (Manuscript Exp 1, Stoichiometry, is a pen-and-paper calc exercise, so it has no bench module.) Aspirin + Caffeine were game-authored, are absent from the grouping, and were dropped — Aspirin survives as a RAW REAGENT because Exp 2 §D hydrolyses it. Linear mastery chain: each experiment unlocks the next once its two-part 90% gate is cleared. Kept as plain data so the menu/hub/experiment-select and ProgressionFlow can drive off one source of truth, and so it is unit-testable without a scene.

```csharp
static readonly IReadOnlyList<CatalogEntry> Entries
static CatalogEntry Get(string moduleId)
static string PrerequisiteOf(string moduleId)
static IEnumerable<CatalogEntry> InPeriod(ExperimentPeriod period)
static int Count
```

### `ExperimentLibrary` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ExperimentLibrary.cs`</sub>

A runtime-safe registry of every ExperimentModuleDefinition, referenced directly (serialized asset refs — no Resources/AssetDatabase needed in a build). One asset instance is referenced by the ExperimentLauncher so any of the 9 experiments can be loaded by moduleId from the menu / period hub / experiment-select.

```csharp
List<ExperimentModuleDefinition> modules
ExperimentModuleDefinition Get(string moduleId)
bool Has(string moduleId)
int Count
```

### `GameFlow` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/GameFlow.cs`</sub>

Transient cross-scene game-flow state. The main menu / experiment-select writes the chosen experiment here, then loads the lab scene where ExperimentLauncher reads it. Kept deliberately tiny; persistent progress lives in ProgressionService/save file.

```csharp
static string SelectedModuleId
static void Select(string moduleId)
```

### `ProgressionFlow` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ProgressionFlow.cs`</sub>

Read-only view over a ProgressionService that answers the game-flow questions the menu / period hub / experiment-select screens ask: what's unlocked, what's next, which period doors are open, and overall completion. Uses ExperimentCatalog as the roster + prerequisite source of truth. Plain C# so it is unit-testable.

```csharp
ProgressionFlow(ProgressionService service) : this(service, false)
ProgressionFlow(ProgressionService service, bool unlockAll)
static ProgressionFlow Create(ProgressionService service)
bool IsUnlocked(string moduleId)
bool IsPassed(string moduleId)
CatalogEntry NextExperiment()
bool IsPeriodComplete(ExperimentPeriod period)
bool IsPeriodUnlocked(ExperimentPeriod period)
int PassedCount()
float OverallCompletion01()
bool AllComplete()
```

### `ModuleRecord` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ProgressionService.cs`</sub>

Per-module progress record (best attempt).

```csharp
string moduleId
float bestGrade
float bestMastery
bool passed
int attempts
```

### `ProgressSaveData` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ProgressionService.cs`</sub>

Versioned save payload.

```csharp
int version
List<ModuleRecord> modules
bool arrivalSeen
bool endingSeen
```

### `ProgressionService` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ProgressionService.cs`</sub>

Tracks which experiments the player has passed and persists it (versioned JSON with a backup slot, corruption-safe). Drives the mastery gate / period-door unlocking. Plain C# — the save path is injectable so it is unit-testable.

```csharp
const int CurrentVersion
ProgressionService(string path
ProgressSaveData Data
ModuleRecord GetRecord(string moduleId)
bool IsPassed(string moduleId)
bool IsUnlocked(string moduleId, string prerequisiteModuleId)
ModuleRecord RecordResult(string moduleId, ExperimentResult result, bool autoSave
void Save()
void Load()
void ResetAll()
```

### `ResultRecorder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ResultRecorder.cs`</sub>

Persists every finished attempt into the ProgressionService save file — the missing link between ExperimentRunner.ExperimentFinished and the unlock gates (without this, passes were never saved and nothing ever unlocked at runtime). Thin MonoBehaviour over the already-tested ProgressionService.RecordResult.

```csharp
event Action<ModuleRecord> Recorded
ModuleRecord LastRecord
string SavePathOverride
void SetRunner(ExperimentRunner r)
static ModuleRecord Record(ProgressionService svc, string moduleId, ExperimentResult result)
```

### `ResultRow` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ResultsExport.cs`</sub>

One row of the Results/History screen: an experiment's best attempt.

```csharp
string moduleId
string title
ExperimentPeriod period
bool attempted
bool passed
float bestGrade
float bestMastery
int attempts
```

### `ResultsExport` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/ResultsExport.cs`</sub>

The manuscript's analytics dashboard, descoped (plan §S2) to a local Results/History view + an exportable scores file. Pure C# over ProgressionService + ExperimentCatalog so it is unit-testable and drives both the screen and the export.

```csharp
static List<ResultRow> BuildRows(ProgressionService service)
static int PassedCount(ProgressionService service)
static string BuildCsv(ProgressionService service)
```

### `SceneAssetLibrary` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/SceneAssetLibrary.cs`</sub>

Runtime-safe name→asset lookup for the ExperimentSceneBuilder (serialized direct references, so it works in a build — no AssetDatabase/Resources). Holds the equipment prefabs and the reagent ChemicalData the layouts reference by name.

```csharp
List<GameObject> prefabs
List<ChemicalData> chemicals
GameObject GetPrefab(string n)
ChemicalData GetChemical(string n)
```

### `TutorialSession` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/TutorialSession.cs`</sub>

Tutorial Mode: all 9 experiments unlocked, heavily guided (glow + waypoint + hint on the watch + always-on labels), and UNGRADED — no quiz, no grade screen, no BKT update, no save write, no unlock. Practice only; a run leaves no trace. Deliberately the same shape as DemoSession.Active: one static flag every consumer early-returns on, so the campaign path stays unchanged by construction rather than by testing.

```csharp
static bool Active
static bool HasPractised(string moduleId)
static void MarkPractised(string moduleId)
static void BeginSession()
static int PractisedCount
```

### `UnlockDiff` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/UnlockDiff.cs`</sub>

Pure helpers for the post-experiment return loop: what did this pass unlock (for Pharmee's announcement), and where does the rig go so the CAMERA lands on the front-door marker. No MonoBehaviours — edit-mode testable.

```csharp
static HashSet<string> UnlockedSet(ProgressionFlow flow)
static List<string> NewlyUnlocked(HashSet<string> before, ProgressionFlow after)
static string AnnouncementFor(IReadOnlyList<string> newIds)
```

### `TeleportMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Progression/UnlockDiff.cs`</sub>

Rig-vs-camera teleport math: an XR rig's origin is NOT the player's head, so landing the HEAD on a marker means offsetting the rig by the (yaw-corrected) head offset.

```csharp
static float RigYawFor(float markerYawDeg, float rigYawDeg, float camYawDeg)
static Vector3 RigPositionFor(Vector3 markerPos, float deltaYawDeg, Vector3 rigPos, Vector3 camPos)
```

---

## NPC

`Assets/PharmaSynth/Scripts/NPC/`

### `CinemaLook` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaShots.cs`</sub>

What a cinematic shot keeps its eye on. Resolved at play time by CinemaTargets.

### `CinemaShot` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaShots.cs`</sub>

One camera shot, attached to a cutscene beat; it lasts the beat's seconds. Positions are NAMES of empty transforms under the scene's `CutsceneMarkers` root, so shots are hand-tunable in the scene and the cutscene assets stay data.

```csharp
string camera
string moveTo
bool blend
CinemaLook look
string lookMarker
bool pharmeePopIn
bool HasCamera
```

### `ScreenMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaShots.cs`</sub>

Pure camera math for cinematic cutscenes (suite-pinned). Eased position, level horizon (never a roll), and a look-at that is re-aimed every frame so a pan stays locked on a moving or distant target. Where the cinema screen sits relative to the viewer (suite-pinned).

```csharp
const float DefaultDistance
const float MaxOffAxisDegrees
static bool NeedsRecentre(Vector3 head, Vector3 headForward, Vector3 screenPos,
```

### `ShotMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaShots.cs`</sub>

### `Pose` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaShots.cs`</sub>

```csharp
static float Ease(float t)
static Quaternion LookAt(Vector3 eye, Vector3 target, Quaternion fallback)
static void Ends(bool blend, Pose current, Pose? cameraMarker, Pose? moveTo,
static Pose Evaluate(Pose start, Pose end, bool blend, Vector3? look, float t01)
```

### `CinemaView` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CinemaView.cs`</sub>

The cinematic cutscene "theatre". A separate, non-XR ShotCamera films the live lab into a letterboxed RenderTexture; while active, the player's XR camera renders ONLY the `Cinema` layer — a black void holding a wide screen showing that texture, plus the subtitle and the fade quad. The XR camera is never moved or rotated (head tracking stays the player's), and the shot camera owns every frame of the picture. The screen is placed where the player is looking and then held fixed relative to the player's rig, not the world: cutscenes teleport the rig under the fade (to the review corner, back home), and a world-fixed screen was left beside, far from or behind the player. Seen from behind, only the double-sided caption showed, mirrored. If the head still ends up behind the screen, too far, or well off to one side, the screen is placed in front again. Hands, HUD and the lab vanish with the cull

```csharp
const string LayerName
const string MarkersRoot
float ScreenDistance
Transform ScreenTransform
bool Active
static bool AnyActive
Camera ShotCamera
RenderTexture Texture
static int Layer
const float PharmeeAimLift
bool ShotCameraIsFlat
static int ShotMask()
static void ScreenPose(Vector3 head, Vector3 headForward, float distance,
void Enter(Camera xr)
```

### `CutsceneData` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneData.cs`</sub>

A data-driven cutscene: a sequence of subtitle "beats" with a Pharmee expression and duration. A beat may carry a camera SHOT; a cutscene with any shot plays as a cinematic (CinemaView: a separate camera films the lab onto a screen in a dark room — the XR camera itself is never animated). No shots = subtitles only.

### `Kind` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneData.cs`</sub>

Appended values only: assets store the kind as its index.

### `Beat` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneData.cs`</sub>

```csharp
Kind kind
List<Beat> beats
bool IsCinematic
float TotalDuration()
```

### `CutsceneDirector` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneDirector.cs`</sub>

Plays data-driven cutscenes at the right narrative moments by subscribing to the ExperimentRunner: Intro when the experiment starts, ReagentPrep when that phase completes, and Success/Failure when it finishes. Beats are delivered as Pharmee subtitles + face expressions. A cutscene whose beats carry shots plays as a CINEMATIC: fade black -> CinemaView (shot camera onto a screen in a dark room, clock paused, locomotion locked) -> fade black -> back in control. The XR camera is never animated. The end-cutscene ALWAYS plays (success OR failure variant) — a user requirement.

```csharp
CutsceneData Intro
CutsceneData ReagentPrep
CutsceneData Success
CutsceneData Failure
void SetLibrary(CutsceneLibrary lib)
UnityEvent onCutsceneStarted
UnityEvent onCutsceneFinished
bool IsPlaying
bool PlayingCinematic
static (string title, string line) CardFor(CutsceneData.Kind kind, string experimentTitle
void SetRunner(ExperimentRunner r)
bool LoadForModule(string moduleId)
void SkipNextOutro()
CutsceneData SelectOutro(ExperimentResult r)
```

### `CutsceneLibrary` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneLibrary.cs`</sub>

moduleId → the four cutscenes for that experiment (Intro, ReagentPrep, Success, Failure). Lets the single scene CutsceneDirector serve all 9 experiments: on ExperimentStarted it swaps its set from here instead of holding one hand-wired set.

### `Entry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneLibrary.cs`</sub>

```csharp
List<Entry> entries
Entry GetSet(string moduleId)
```

### `CutsceneReelRunner` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/CutsceneReelRunner.cs`</sub>

Play-mode half of CutsceneReelRecorder (Editor/): plays each clip and pipes frames to ffmpeg. Editor-only — a component must live in the runtime assembly to be added, and the UNITY_EDITOR guard keeps it (and its UnityEditor calls) out of every build.

```csharp
string ffmpeg
const string OutDir
```

### `ExaminerNPC` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ExaminerNPC.cs`</sub>

Dr. Jimenez — the assessment-mode examiner (plan §3.4) plus his proctor VOICE (a varied pool of remarks and behaviours). In experiments flagged assessmentMode he observes and gives NO hints (Pharmee also stays quiet); the `State`/`IsObserving` machine still reflects that flag exactly. Independently, whenever ANY run is active he proctors aloud: a stern greeting at the start, then occasional oversight remarks (never hints), each driving the rigged model's "Talking" bool for its duration and showing a subtitle if he has a narration channel. Movement is ProctorRoamer's job; this is his voice + state.

### `ExaminerState` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ExaminerNPC.cs`</sub>

```csharp
ExaminerState State
bool IsObserving
bool IsTalking
string LastLine
static bool ShouldObserve(ExperimentModuleDefinition m)
void SetRunner(ExperimentRunner r)
void Bind(ExperimentRunner r, Animator a, NPCNarrationController n)
void SpeakLine(string line)
float SecondsFor(string line)
```

### `FloatBob` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/FloatBob.cs`</sub>

Gentle hover animation for a floating character/prop (Pharmee): sinusoidal vertical bob plus a slight sway tilt and a bounded Perlin jitter ("alive" wiggle), around a movable home pose. Local-space so it composes with any parent motion; a mover may drive the home via SetHome.

```csharp
Vector3 Home
void SetHome(Vector3 localPos)
void SetGiveWayOffset(Vector3 localOffset)
Vector3 GiveWay
void SetGestureOffset(Vector3 localOffset)
Vector3 Gesture
static Vector3 JitterOffset(float t, float speed, float amplitude)
```

### `GateState` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/GatekeeperModel.cs`</sub>

States of the door-gated game loop (the confirmed client workflow, post-experiment review flow included): Pharmee blocks the lab door; the player picks Lab Tour or Campaign → episode → lab-coat → ready → the lab loads/resets → threshold warning → the period starts the moment they walk in → tests complete → Pharmee congratulates and fades the player to Dr. Jimenez's review corner (QuizIntro) → Jimenez briefs → the quiz (QuizTime, never score-gated — manuscript) → grade + outro + spoken remarks (ScoreReview) → Continue teleports home with a full lab/wearables reset (Returning) → Pharmee's quiz-completion debrief AT THE ENTRANCE (Debrief) → unlock announcement → repeat. Retry from the review corner re-arms at the door.

### `GateEvent` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/GatekeeperModel.cs`</sub>

### `GatekeeperModel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/GatekeeperModel.cs`</sub>

Pure, table-driven state machine for the Pharmee door gate. No Unity types — fully edit-mode testable; the thin PharmeeGatekeeper MonoBehaviour applies each transition to the scene (door blocker, panels, launcher, runner, fades).

```csharp
GateState State
string SelectedModuleId
bool IsLabTour
event Action<GateState, GateState> Transition
bool Fire(GateEvent e)
void ResetToBlocked()
static GateState Next(GateState s, GateEvent e)
static bool DoorOpen(GateState s)
static bool RequiresPPEToOpen(GateState s)
static bool IsReviewState(GateState s)
ExperimentPeriod PickedPeriod
bool ChooseEpisode(ExperimentPeriod period, Func<string, bool> canSelect,
bool ChooseModule(string moduleId, Func<string, bool> canSelect)
static string FirstPlayableInPeriod(ProgressionFlow flow, ExperimentPeriod period)
```

### `IloCopy` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/IloCopy.cs`</sub>

Intended Learning Outcomes per experiment (Pharmee states the session's objectives in the opening dialogue). The 8 manuscript modules use the VERBATIM Appendix C "Objectives" text (transcribed in Docs/manuscript-reconciliation.md §2 — the chemistry authority); Methane is game-authored in the same voice and is client-CONFIRMED as the tutorial. (Aspirin + Caffeine were dropped with their modules.)

```csharp
const string LeadIn
static string[] ForModule(string moduleId)
static float BeatSeconds(string line)
```

### `JimenezRigMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/JimenezRigMath.cs`</sub>

Pure rules for correcting an auto-rigger's weight bleed (suite-pinned). Dr. Jimenez is a Tripo auto-rig: ONE SkinnedMeshRenderer over 41 bones, with the lab coat baked into the body mesh. Nothing can be unparented — the coat follows his arm because coat vertices carry weight on the arm bones. Auto-riggers assign weights by proximity, so a coat panel hanging near the elbow picks up elbow weight even though a real coat would hang from the shoulders. The correction is geometric: a vertex that carries arm weight while sitting well OUTSIDE the arm is bleed, and its weight belongs on the spine.

```csharp
const float SleeveRadiusFraction
static float SleeveRadiusFor(float meshHeight)
static bool IsArmBone(string boneName)
static bool IsTwistBone(string boneName)
static bool IsBetterSegmentEnd(float candidateDistance, float bestSoFar)
static bool IsBleed(float distanceToBone, float sleeveRadius)
static bool IsSleeve(float distanceToArm, float distanceToSpine, float sleeveRadius)
const float BlendFraction
static float BlendBandFor(float meshHeight)
static float TransferFraction(float distanceToArm, float sleeveRadius, float blendBand)
static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
static float Redistribute(ref BoneWeight w, int fromBone, int toBone)
static float Redistribute(ref BoneWeight w, int fromBone, int toBone, float fraction)
static void Normalise(ref BoneWeight w)
```

### `LabTourGuide` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/LabTourGuide.cs`</sub>

Location-triggered Lab Tour (storyboard): instead of narrating on a timer, Pharmee points each area out as the player physically walks up to it — workbench, equipment cabinet, reagent shelf — then signs off once they've seen them all. Started/stopped by PharmeeGatekeeper; speaks through its narration via a callback. Landmarks resolve by name (no scene wiring); if none resolve the gatekeeper falls back to the timed sequence. Pure proximity core is self-tested.

### `Stop` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/LabTourGuide.cs`</sub>

```csharp
bool IsActive
int VisitedCount
int StopCount
Transform CurrentLandmark
int Begin(Action<string> say)
void End()
static int FirstUnvisitedInRange(Vector3 playerPos, Vector3[] landmarkPos, bool[] visited, float[] radii)
static readonly string[] DefaultBeatTexts
void SeedDefaults()
```

### `NPCNarrationController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/NPCNarrationController.cs`</sub>

### `NarrationLine` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/NPCNarrationController.cs`</sub>

```csharp
UnityEvent onNarrationFinished
event Action<string, float> LineStarted
event Action LineEnded
bool IsSpeaking
bool Typewriter
float TypeCps()
int VisibleCount
bool IsRevealing
static float HoldSecondsAfterReveal(float authoredWait, float revealSeconds, float minHold
void SetPanelRoot(GameObject g)
void BindVoice(VoiceBank bank, VoiceSpeaker who)
AudioClip ResolveVoice(string subtitle)
void SetVoiceBlip(string key, float volume
void PlayTutorialNarration()
```

### `PharmeeAttitude` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeAttitude.cs`</sub>

Pharmee flight attitude: lean the body into the movement direction so he reads as flying through air, pulse the hover waves at his base, and add a gentle bob-nod while he talks. Composes with FaceCamera (root yaw) and FloatBob (position) by twisting only the body CHILD. Also folds in the GESTURE pose (PharmeeGestureMath) and aims the model's two hand pivots. Everything still lands in the ONE localRotation assignment below - this class is the sole writer of bodyRoot, and a second component writing it would silently lose or fight per-frame. The model has no skeleton (no skins, no joints, no LimbNodes in either RobotNPC.glb or .fbx), so all of this is procedural by necessity as well as by choice.

```csharp
void Bind(Transform body, Transform[] waveRings, FloatBob b, NPCNarrationController n)
void BindHands(Transform left, Transform right)
void SetPose(PharmeePose pose)
static float LeanFor(float speedMps, float degPerMps, float maxDeg)
```

### `PharmeeFaceExpression` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`</sub>

Pharmee's expressions, driven onto the robot's screen-face (material/animator).

### `PharmeeState` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`</sub>

Pharmee's behavioural states.

### `IPharmeeFace` <sub>interface</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`</sub>

Optional visual face — implemented by the robot's face material/animator layer.

### `PharmeeBrain` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`</sub>

Robot-guide brain: reacts to ExperimentRunner events with subtitle dialogue and a face expression (a core requirement: the NPC robot has dialogue). Greets on start, instructs the current step, warns on mistakes, celebrates or encourages at the end. Dialogue lives in serialized data (client-reviewable); step instructions come from each task's hint.

### `DialogueSet` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeBrain.cs`</sub>

```csharp
void SetSubtitlePace(float speed)
bool AssessmentMode
PharmeeState State
string LastLine
PharmeeFaceExpression LastExpression
void SetRunner(ExperimentRunner r)
const float RepeatWarnSeconds
static bool WarnAgain(LabErrorType type, LabErrorType? lastType, float secondsSinceLast)
void InstructCurrent()
static string InstructionFor(ExperimentTask task)
```

### `PharmeeFace` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeFace.cs`</sub>

Concrete face for Pharmee: tints the screen-face renderer(s) per expression. Implements IPharmeeFace so PharmeeBrain/PharmeeGatekeeper drive it. Point faceRenderers at the robot's eye/mouth meshes; the color property matches the shader (_EmissionColor for an emissive screen, else _BaseColor). Uses a MaterialPropertyBlock — no material instantiation, edit-mode safe. Default = HAPPY (happy by default, especially while following).

```csharp
PharmeeFaceExpression Current
void BindRenderers(params Renderer[] rs)
void SetPalette(Color n, Color h, Color w)
void ResetToDefault()
void SetExpression(PharmeeFaceExpression e)
```

### `PharmeeGatekeeper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGatekeeper.ReturnLoop.cs`</sub>

After the grade: pass and fail exits, the lab reset and the trip home.

```csharp
void OnContinueAfterPass()
void OnAbandonAfterFail()
void SpeakWelcome()
void ResetToEntrance()
void OnRetryRequested()
void CelebrateCampaign()
```

### `PharmeeGatekeeper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGatekeeper.Review.cs`</sub>

The post-experiment review corner: briefing, quiz tablet and grade screen.

```csharp
static bool ShouldEnterReview()
```

### `PharmeeGatekeeper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGatekeeper.States.cs`</sub>

Applying each gate state to the scene (door, dialogue, panel, fades) and the lab tour.

```csharp
void OnSupplyExhausted(List<string> starvedTaskIds)
```

### `PharmeeGatekeeper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGatekeeper.cs`</sub>

Thin scene driver for the GatekeeperModel: applies each state to the world — door blocker on/off, Pharmee lines, the door choice panel, stage loading with fades, and the walk-in run start. All heavy logic lives in the pure model.

### `GateLines` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGatekeeper.cs`</sub>

```csharp
Transform GateGuideTarget
GateLines Lines
GatekeeperModel Model
static readonly ExperimentPeriod[] EpisodeRows
void SetSubtitlePace(float speed)
static void ResetSessionState()
void OnApproachTriggerEntered()
void OnPharmeeTalk()
void OnThresholdTriggerEntered()
void OnPPEWorn()
void OnPanelOption(int index)
static bool ReviewFlowActive
```

### `PharmeeGesture` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGestureMath.cs`</sub>

What Pharmee's body is doing right now, on top of his hover. Deliberately NOT a new state enum: `PharmeeState` (PharmeeBrain.cs) already IS the set, and already drives his line pool, his face and his beep. This is the fourth mapping off the same state, not a parallel machine.

### `PharmeePose` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGestureMath.cs`</sub>

One frame of gesture, in the four channels Pharmee actually has.

```csharp
Quaternion bodyRot
Vector3 rootOffset
float handRaise
float waveFlare
static PharmeePose Rest
```

### `PharmeeGestureTuning` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGestureMath.cs`</sub>

Tunable magnitudes, passed in rather than read from statics so the suite can drive them and the inspector can retune them without a code change. Real motion always needs a calibration knob - the right amplitude is a headset judgement, not an editor one.

```csharp
float nodDegrees
float pointLeanDegrees
float warnRecoilDegrees
float warnShakeDegrees
float celebrateRise
float celebrateSpinDegrees
float celebrateFlare
static PharmeeGestureTuning Default
```

### `PharmeeGestureMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGestureMath.cs`</sub>

Pure gesture curves - no Time.time, no transforms, no Unity state. Every value is a function of (gesture, seconds since it started, tuning), which is what makes the whole animation set checkable in edit mode. There is no headset pass available, so anything that cannot be pinned cannot be trusted. Precedents this matches exactly: PharmeeAttitude.LeanFor, PharmeeGiveWay.SideStep, FloatBob.JitterOffset, LabTourGuide.FirstUnvisitedInRange, SpeakerLedBlink.Level01.

```csharp
static float DurationOf(PharmeeGesture g)
static bool IsSustained(PharmeeGesture g)
static PharmeeGesture ForState(PharmeeState s)
static PharmeeGesture ForGate(GateState s)
static PharmeePose Pose(PharmeeGesture g, float t, PharmeeGestureTuning tune)
static float Envelope(float u)
static float EaseInOut(float u)
```

### `PharmeeGestures` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGestures.cs`</sub>

root-motion-free"). `PharmeeState` already selects his line pool, his face and his beep. This adds the fourth mapping - state to body - and nothing else. It is deliberately THIN: all the curves live in the pure `PharmeeGestureMath` so the suite can pin them, because there is no headset pass available to judge them by eye. It writes NO transform. Like `PharmeeMover` and `PharmeeGiveWay`, it feeds the two components that own Pharmee's transforms: * position -> `FloatBob.SetGestureOffset` (one more term in FloatBob's single sum) * rotation + hands + rings -> `PharmeeAttitude.SetPose` (folded into its single localRotation assignment) Adding a component that wrote `Robot Origin` directly would fight `PharmeeAttitude`, which overwrites it absolutely every LateUpdate.

```csharp
void Bind(PharmeeAttitude a, FloatBob b, PharmeeBrain br, ExperimentRunner r, LabTourGuide t)
void SetTuning(PharmeeGestureTuning t)
PharmeeGesture Current
void Play(PharmeeGesture g)
```

### `PharmeeGiveWay` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGiveWay.cs`</sub>

Pharmee steps aside when the player walks into him (so he never blocks the way the player is trying to go). While the player is inside his personal-space bubble, he drifts laterally out of their path (to whichever side he's already on, so he clears their forward direction), then eases back once they pass. Additive on top of FloatBob's home + bob, so it composes with the follow/hover behaviour instead of fighting it.

```csharp
void Bind(FloatBob b, Transform bodyXform, Transform p)
static Vector3 SideStep(Vector3 pharmeePos, Vector3 playerPos, Vector3 playerForward,
```

### `PharmeeGlowMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGlow.cs`</sub>

Pure rules for how bright Pharmee reads (suite-pinned).

```csharp
const float DefaultIntensity
const float DefaultShellAlbedo
const float FaceCeiling
static readonly Color CyanHue
const float TalkPulseHz
const float TalkPulseDepth
const float TalkEaseSeconds
static float TalkIntensity(float baseIntensity, float t, float speak)
static Color CapBrightness(Color c, float ceiling)
static Color Emission(Color hue, float intensity)
static bool Blooms(Color emission, float bloomThreshold
static bool IsGlowPart(string rendererName)
static bool IsShellPart(string rendererName)
```

### `PharmeeGlow` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeGlow.cs`</sub>

Keeps Pharmee from blowing out into a white ball while she speaks. TWO separate causes, measured rather than guessed: 1. The robot FBX ships one emissive material, `Blue_Light`, at HDR (0.46, 2.75, 8.0) — eight times white — on the body panel and all four hover rings. Dimmed here. 2. Her `White_Glossy` shell has NO emission at all and still blooms, because albedo 0.91 under the nearby fill light pushes the lit result past the 1.0 bloom threshold. Dimming the emissive alone therefore could not have fixed it. Everything is written through a MaterialPropertyBlock, the same pattern `PharmeeFace` uses: no material is instanced, the FBX and its importer stay untouched, and both values are tunable live in the inspector. Thin over pure `PharmeeGlowMath`; `Bind*` seams exist because AddComponent fires no Awake in edit mode.

```csharp
Renderer[] Renderers
Renderer[] ShellRenderers
float Intensity
float ShellAlbedo
Color Hue
void BindRenderers(params Renderer[] rs)
void BindShell(params Renderer[] rs)
void Apply()
```

### `PharmeeLines` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeLines.cs`</sub>

interactions for Pharmee and the Dr.). Pure static data + a deterministic picker so the self-tests can pin variety without RNG. Pharmee rotates through these instead of repeating one canned line; Dr. Jimenez draws his stern examiner remarks from the exam pools. Client-reviewable copy (the §5 dialogue sign-off still applies).

```csharp
static readonly string[] Greetings
static readonly string[] Praise
static readonly string[] Celebrate
static readonly string[] Encourage
static readonly string[] Idle
static readonly string[] WrongReagent
static readonly string[] WrongStep
static readonly string[] Overheat
static readonly string[] Safety
static readonly string[] ExamGreeting
static readonly string[] ExamRemarks
const string TutorialOrientation
const string TutorialPreview
const string PracticeComplete
```

### `PharmeeMood` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeMood.cs`</sub>

Keeps Pharmee's face in sync with what he's doing (expressions depend on what he's saying; HAPPY by default, especially while following you). PharmeeBrain/PharmeeGatekeeper set the per-line expression when a line starts; this component resets the face to its default (happy) when the line ENDS, so he never gets stuck on a warning face while floating after the player.

```csharp
void Bind(NPCNarrationController n, PharmeeFace f)
static PharmeeFaceExpression ExpressionForGate(GateState s)
```

### `PharmeeMoveSolver` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeMover.cs`</sub>

Pure hover-follow math for Pharmee's in-lab movement: pick the anchor nearest the player (with hysteresis so he doesn't ping-pong), glide toward it at a clamped speed, and never crowd the player. Edit-mode testable.

```csharp
static int PickAnchor(Vector3 playerPos, IReadOnlyList<Vector3> anchors,
static Vector3 Step(Vector3 current, Vector3 target, float maxSpeed, float dt)
```

### `PharmeeMover` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeMover.cs`</sub>

Runtime driver: while an experiment runs, Pharmee glides between hover anchors to stay near (but not on top of) the player — "he follows and watches me work". When idle he returns to his door-home. Drives FloatBob's home so bob + jitter keep composing on top of the motion.

```csharp
void Bind(ExperimentRunner r, FloatBob b, Transform p, Transform[] a)
void TickSolve(float dt)
```

### `PharmeePoke` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeePoke.cs`</sub>

Makes Pharmee interactable: poking/selecting the robot repeats the current step's guidance (PharmeeBrain.InstructCurrent). Debounced so rapid pokes don't spam the narration.

```csharp
void Poke()
```

### `PharmeeThrusterMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/PharmeeThrusterMath.cs`</sub>

Pure motion for Pharmee's hover rings, read as THRUSTER EXHAUST (suite-pinned). ⛔ A scale pulse (`scale = 1 + 0.12 * sin(t * 2.2 * 30)`) is a ±12% LATERAL pulse at about 10 Hz on a flat horizontal ring: a sideways shake, which a thruster never does, and nothing flows anywhere. Each ring is a puff of exhaust. It is born at the emitter, travels DOWN, spreads slightly as it goes, and fades out at the bottom while the next one is already on its way — so the four rings read as one continuous jet.

```csharp
const float DefaultFlowSpeed
const float DefaultTravel
const float DefaultSpread
static float Phase(float time, int index, int count, float flowSpeed)
static float Drop(float phase01, float travel)
static float Swell(float phase01, float spread)
```

### `ProctorRoamModel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ProctorRoamModel.cs`</sub>

Pure state machine for Dr. Jimenez's proctor roaming (he roams the room from time to time, observing the player or looking at the shelves, then walks back to his original position to run the quiz). No Unity types. Loop: AtHome (idle a while) → WalkingOut (to the next observation point) → Observing (look at it) → WalkingHome → AtHome … . Round-robin over the points with a deterministic idle-time jitter (seeded — testable). When roaming is disallowed (quiz/post-lab time) any outing is cut short and he heads home.

### `Phase` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ProctorRoamModel.cs`</sub>

```csharp
Phase Current
int TargetIndex
ProctorRoamModel(int pointCount, float idleMin
bool Tick(float dt, bool allowRoam, bool arrived)
bool IsWalking
```

### `ProctorRoamer` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ProctorRoamer.cs`</sub>

Thin driver for ProctorRoamModel on Dr. Jimenez: walks him between his home post and the observation points (shelf/benches), faces the walk direction, plays the Walk animator state while moving, and glances at the point (or the player) while observing. Roaming pauses (he returns home) from the moment an experiment FINISHES until the next one starts — home is where he proctors the quiz.

```csharp
ProctorRoamModel Model
void Bind(Animator a, ExperimentRunner r, List<Transform> points)
void ReturnHomeAndHold(Transform faceTarget
static bool StuckTick(ref float timer, float movedSq, float dt, float giveUpSeconds)
```

### `ShowcaseAction` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ShowcaseDirector.cs`</sub>

What the lab does during a showcase beat. Every action is undone when the reel ends. Appended values only: reel assets store the action as its index.

### `EndingMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ShowcaseDirector.cs`</sub>

The ending's results, read from the save and written into the reel's cards (suite-pinned).

```csharp
static bool ShouldPlay(bool allComplete, bool seen, bool forced)
static List<int> Grades(ProgressSaveData data, ExperimentPeriod? period
static int Passed(ProgressSaveData data)
static int Average(ProgressSaveData data)
static string Fill(string text, ProgressSaveData data)
static ProgressSaveData Sample()
```

### `ArrivalMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ShowcaseDirector.cs`</sub>

Pure rules for the arrival reel (suite-pinned).

```csharp
const float SkipHoldSeconds
const float TimelapseHours
const float TimelapseScale
static bool ShouldPlay(bool seen, bool demo)
static bool SkipDone(float heldSeconds)
static float PouredMl(float k, float totalMl)
```

### `ArrivalFlow` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ShowcaseDirector.cs`</sub>

The hand-off across the cube room → lab scene load.

```csharp
static bool LabPartPending
static bool Playing
static bool Busy
```

### `ShowcaseDirector` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/NPC/ShowcaseDirector.cs`</sub>

The showcase reels: silent cinematic tours (cards and captions, no voice) played on the cinema screen, so the headset view is never moved. ARRIVAL, between the cube room and the lab door. Two halves, one per scene: the cube-room half ends on black and loads the lab, the lab half picks up under that black and ends with the player at the front door, where the gatekeeper's normal welcome takes over. ENDING (lab only), once per save when the whole campaign is passed, then the gatekeeper's voiced celebration; the demo HUD's Play Ending replays it any time. Its cards carry EndingMath tokens filled from the save. Lives on the scene's CutsceneMarkers root beside its CinemaView. Built by Tools ▸ PharmaSynth ▸ Cutscenes ▸ Build Showcase Reels.

```csharp
CutsceneData Reel
CutsceneData Ending
bool LabPart
bool Playing
void Bind(CutsceneData r, bool lab, AudioClip m, CutsceneData end
bool TryPlayEnding(bool forced, Action done)
static ShowcaseDirector Find()
static void EnterLab(string labScene, bool demo)
bool PlayThenLoad(string labScene)
```

---

## UI

`Assets/PharmaSynth/Scripts/UI/`

### `ChecklistPager` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ChecklistPager.cs`</sub>

Focused checklist text for the holo procedures board (the flat pads rendered every phase at full detail into a fixed rect, so long checklists overflowed and overlaid the reaction footer — unreadable). Instead of shrinking text to fit, collapse what the player doesn't need: completed phases fold to a "done n/n" line, future phases to a "(n steps)" stub, and only the ACTIVE phase renders its full step list. Line count is bounded by construction (phases + longest single phase), so the text always fits its rect at a readable size.

```csharp
static TaskPhase? ActivePhase(TaskGraph graph)
static string BuildObjectivesHeader(ExperimentModuleDefinition module)
static string BuildMaterialsHeader(ExperimentModuleDefinition module)
static string BuildFocusedText(TaskGraph graph, System.Func<string, string> stepDetail
static string BuildHeader(ExperimentRunner runner)
```

### `ChoicePanelController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ChoicePanelController.cs`</sub>

Reusable world-space option dialog (door gate, confirms): a title plus up to N option buttons. Buttons carry persistent int-arg listeners → OnOption(i); code listens on OptionChosen. All refs optional so it is edit-mode testable.

```csharp
event Action<int> OptionChosen
bool IsOpen
void Bind(GameObject panelRoot, TMP_Text title, Button[] buttons, TMP_Text[] labels)
void Show(string title, IList<string> options, IList<bool> interactable
void ShowMessage(string title, string confirmLabel)
void Hide()
void OnOption(int index)
string LabelAt(int i)
```

### `ComfortMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ComfortApplier.cs`</sub>

Pure mapping from comfort settings to the values the live systems consume (§5 settings apply-listeners) — separated so the self-tests pin the curves.

```csharp
static Vector3 HudScale(Vector3 baseScale, float textScale)
static float ApertureFor(float intensity01)
static float LineSecondsFor(float baseSeconds, float speed)
```

### `ComfortApplier` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ComfortApplier.cs`</sub>

Applies ComfortSettings to the live scene whenever SettingsService raises Changed: HUD/text scale, snap-turn angle, tunneling vignette strength, and Pharmee subtitle pacing. All targets optional — the applier works with whatever the scene has (the menu scene has fewer targets than the lab).

```csharp
static readonly int GuideSteadyProperty
void Bind(Transform hud, SnapTurn turn, Vignette vig, PharmeeBrain b, PharmeeGatekeeper g)
void Apply(ComfortSettings s)
```

### `DemoButtonVisibility` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/DemoButtonVisibility.cs`</sub>

Shows/hides the cube-room "Demo Mode" button from the config file's verdict. Lives on an always-active parent (the menu panel) because a disabled button can't re-enable itself — and the config loads asynchronously on Android, so this polls rather than checking once. [ExecuteAlways] so it also reflects the config in the Editor Scene view (the button shows without entering Play mode).

```csharp
void Bind(GameObject button)
```

### `DemoHudController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/DemoHudController.cs`</sub>

The demo HUD cluster (Skip Step / Finish Experiment / Auto-Answer Quiz), visible only during a demo session and only when each verb applies. Built by Tools ▸ PharmaSynth ▸ Demo ▸ Build Demo HUD; buttons call the On* methods.

```csharp
void Bind(ExperimentRunner r, PostLabController p,
static bool SkipAllowed(bool demo, bool tutorial, bool running, bool review)
void OnSkipStep()
void OnFinishExperiment()
void OnPlayEnding()
void OnAutoQuiz()
```

### `ExperimentHudController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ExperimentHudController.cs`</sub>

Screen/world HUD: module title, count-up timer, progress bar, and task-accomplished toasts. Subscribes to ExperimentRunner — no polling except the once-per-second timer text. The visible progress bar drops on mistakes (storyboard behaviour) while the underlying TaskGraph progress stays clean.

```csharp
static float DisplayedProgress(float graphProgress01, int mistakes, float penaltyStep)
static string FormatPercent(float p01)
static string FormatTime(float seconds)
```

### `FaceCamera` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/FaceCamera.cs`</sub>

Rotates a world-space label or canvas to face the player's camera every frame, so text stays readable from any side (never mirrored / seen from behind). Y-axis mode keeps signs upright; full mode also pitches toward the viewer.

```csharp
bool yAxisOnly
float yawOffset
bool faceTowardCamera
bool preserveInitialTilt
static Transform Override
```

### `GlyphSafe` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/GlyphSafe.cs`</sub>

The world-space lab UI (tablet "pads", wrist holo board, reaction footer) renders with LiberationSans SDF, whose baked atlas is missing arrows, Greek and box glyphs (→ ← ↑ ↓ Δ ⇌ ☑ ☐ ▶ …). Those showed as blank "missing-glyph" boxes on the tablets. Rather than degrade the source chemistry data or regenerate the font atlas (fragile), we map the unsupported glyphs to font-safe equivalents at DISPLAY time. Pure + tested.

```csharp
static string Sanitize(string s)
static string Dashes(string s)
```

### `GradeDisplay` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/GradeDisplay.cs`</sub>

Pure display rules for grades: the pass gate compares RAW values (Total >= 90), so displayed percentages must FLOOR — rounding 89.6 up to "90%" beside a TRY AGAIN verdict reads as a contradiction. Suite-pinned.

```csharp
static int Percent(float raw)
static int MasteryPercent(float raw01)
```

### `GradeScreenController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/GradeScreenController.cs`</sub>

End-of-experiment grade screen. Populated from an ExperimentResult: overall grade %, mistakes, time, per-criteria breakdown, and PASSED / TRY AGAIN based on the two-part gate. Retry/Continue are wired via UnityEvents.

```csharp
UnityEvent onRetry
UnityEvent onContinue
UnityEvent onBackToEntrance
void SetRunner(ExperimentRunner r)
void Show(ExperimentResult r)
void ShowNow(ExperimentResult r)
void ShowPractice(int stepsDone, int corrections, System.Action onDone)
void Hide()
void OnRetryPressed()
void OnContinuePressed()
void OnBackPressed()
static string BuildBreakdown(ExperimentResult r)
```

### `HoloScroller` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HoloScroller.cs`</sub>

Scroll driver for the holo procedures board (the instruction text rendered as one continuous row and couldn't be read — the body now wraps inside a masked viewport and this scrolls it). Big ▲/▼ page buttons are the primary VR affordance (poke/ray-friendly); the ScrollRect still accepts direct drag. Pure page math kept static so the suite pins the clamping.

```csharp
static float NextPage(float current01, float pageFrac, int direction)
void Bind(ScrollRect sr)
void PageUp()
void PageDown()
```

### `HoverInfoPanel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HoverInfoPanel.cs`</sub>

The floating info card the hover-inspector shows when you point at a piece of equipment, a reagent bottle or an NPC. It smoothly fades and scales in/out and rides a comfortable distance in front of you along the line to the pointed object, always billboarded and readable. Pure easing helpers so the animation curve is unit-testable; HoverInspector feeds it entries.

```csharp
static float Ease(float t)
static float PlaceDistance(float dist, float near, float far, float standoff)
static Color AccentFor(LabInfoCategory c)
void Show(LabInfoEntry e, Vector3 worldAnchor)
void Hide()
bool IsVisible
static string Tag(LabInfoCategory c)
```

### `HubSelectController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HubSelectController.cs`</sub>

Period hub + experiment-select. Presents the 11-experiment roster grouped by period with per-entry state (locked / available / passed) driven by the ProgressionFlow gate, and launches the chosen experiment into the lab scene. The presentation model (BuildModel/StateOf) and the launch gate (CanSelect) are pure statics so they are unit-testable without a scene or disk.

### `RowState` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HubSelectController.cs`</sub>

### `Row` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HubSelectController.cs`</sub>

```csharp
static List<Row> BuildModel(ProgressionFlow flow)
static RowState StateOf(Row r)
static bool CanSelect(ProgressionFlow flow, string moduleId)
void Refresh()
bool Select(string moduleId)
```

### `HudDialogueBar` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudDialogueBar.cs`</sub>

Screen-bottom dialogue bar (storyboard style): the speaker's portrait + name + the line they are currently speaking, mirrored from NPCNarrationController so the player can read them without looking at them. Visible ONLY while a line is live; fades out smoothly when the line ends. MULTI-SPEAKER. Pharmee and Dr. Jimenez own SEPARATE NPCNarrationControllers (their own world bubbles), so the bar subscribes to each channel; each carries its own name + portrait, and whichever speaks last owns the bar.

### `Channel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudDialogueBar.cs`</sub>

```csharp
void Bind(NPCNarrationController n, GameObject root, TMP_Text speaker, TMP_Text line)
void Bind(NPCNarrationController n, GameObject root, TMP_Text speaker, TMP_Text line,
void HandleLineStarted(string line, float seconds)
void HandleLineEnded()
```

### `HudFollowSolver` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudFollowSolver.cs`</sub>

Pure math for a lazy-follow VR HUD: the canvas hovers a fixed distance in front of the head and only re-centres when the head strays outside a yaw/position deadzone — the accepted comfort pattern for "screen-anchored" HUDs (a hard head-lock is nauseating in VR). Static + stateless so it is edit-mode testable.

### `Params` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudFollowSolver.cs`</sub>

### `State` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudFollowSolver.cs`</sub>

```csharp
static float DeltaYawDeg(float fromDeg, float toDeg)
static Vector3 AnchorPoint(Vector3 headPos, float headYawDeg, in Params p)
static bool OutsideDeadzone(in State s, Vector3 headPos, float headYawDeg, in Params p)
static void Step(ref State s, Vector3 headPos, float headYawDeg, in Params p, float dt)
static State Snapped(Vector3 headPos, float headYawDeg, in Params p)
```

### `HudMenuDropdown` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudMenuDropdown.cs`</sub>

Collapses the HUD's Settings/Restart/Quit actions behind a single icon button — plus the demo verbs (Skip Step / Finish Experiment / Auto-Answer Quiz), which live in the same list and are shown only during a demo session. The icon's onClick calls Toggle(); each action button's onClick also calls Close() so the list dismisses after a pick. Starts hidden. The action buttons keep their own LabMenuController / DemoHudController wiring untouched.

```csharp
void SetList(GameObject panel)
void Toggle()
void Close()
static float HeightFor(int items, float itemHeight, float gap, float pad)
```

### `HudRigController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudRigController.cs`</sub>

Driver for the HUD canvas. Two modes: ScreenLocked (default, storyboard style) — the canvas is rigidly glued to the camera every frame (full rotation), so pills sit at fixed screen corners. LazyFollow — comfort mode: hovers ahead and only re-centres outside a deadzone (kept as an option; some players find head-locked UI fatiguing on-device).

### `Mode` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/HudRigController.cs`</sub>

```csharp
Mode CurrentMode
HudFollowSolver.Params Follow
void SnapToCamera()
```

### `LabInfoCategory` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabInfoDatabase.cs`</sub>

Knowledge base behind the hover-inspector pane: pointing at a piece of equipment, a reagent bottle or an NPC pops a card with its name and a short, learn-as-you-play blurb — "what it's for + how to use it" for apparatus, trivia + hazard for reagents. Pure + tested; the resolver (HoverInspector) feeds it a chemical name / prop name and gets back a ready-to-display entry.

### `LabInfoEntry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabInfoDatabase.cs`</sub>

```csharp
readonly string Title
readonly LabInfoCategory Category
readonly string Body
LabInfoEntry(string title, LabInfoCategory cat, string body)
```

### `LabInfoDatabase` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabInfoDatabase.cs`</sub>

```csharp
static string Norm(string s)
static LabInfoEntry Reagent(string chemicalName)
static LabInfoEntry Equipment(string candidate)
static LabInfoEntry Person(bool pharmee)
static int ReagentCount
static int EquipmentCount
```

### `LabMenuController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabMenuController.cs`</sub>

The HUD's top-right cluster inside the lab: Settings toggles the in-lab settings panel; Quit (confirm) fades back to the menu scene; Restart (confirm) rebuilds the whole stage — the panic button for misplaced props / physics lag.

### `Pending` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabMenuController.cs`</sub>

```csharp
void Bind(GameObject settings, ChoicePanelController confirm,
void OnSettingsToggle()
void OnQuitToMenu()
void OnRestart()
void OnConfirmOption(int index)
static string RestartConfirmText(bool running)
```

### `LabelDeclutterMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabelDeclutter.cs`</sub>

Pure rules for keeping world-space name tags from sitting on top of each other (labels must not overlap: they push each other apart while staying next to the item they name, so players can read them). Every ProximityLabel billboards independently, so a rack of 19 identical tubes, a shelf of bottles or a cluster of glassware produced a stack of tags occupying the same few centimetres of screen. Nothing was wrong with any single tag; the problem only exists between them, which is why it needs a pass that can see them all at once. The separation is computed in SCREEN space (that is where the overlap actually happens — two tubes a hand's width apart overlap from across the room and not from close up) and applied as a small WORLD-space lift, so a tag never leaves the item it names.

```csharp
const float MaxLift
const float GapPixels
static float[] Lifts(IReadOnlyList<float> screenY, IReadOnlyList<float> halfHeight,
static bool[] Duplicates(IReadOnlyList<string> text, IReadOnlyList<float> screenX,
static float[] Lifts(IReadOnlyList<float> screenX, IReadOnlyList<float> halfWidth,
```

### `LabelDeclutter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabelDeclutter.cs`</sub>

Drives LabelDeclutterMath over the live ProximityLabels. Thin: it measures, calls the pure solver and hands each label a lift. Polls at the same 5 Hz TutorialHighlighter uses. Tags are read at a human pace, and a per-frame solve over every visible label would cost more than the problem.

```csharp
void Bind(float poll, float lift)
```

### `LabelStyle` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/LabelStyle.cs`</sub>

The look of an item's name tag: dark glass panel, cyan edge, a pointer line to the item, and two lines of text (the name, then the contents in a softer cyan). The sprites are generated in code, so there is no art asset to import or strip, and the panel keeps a constant edge at any size (9-sliced). Pure parts are suite-pinned.

```csharp
static readonly Color32 PanelFill
static readonly Color32 Edge
const string DetailColour
static readonly Vector2 Padding
const float LeaderWidth
const float DotSize
const float MinLeader
static string TagText(string raw)
static Sprite Panel
static Sprite Pixel
static Sprite Dot
const int PanelSize
static Color32 PanelPixel(int x, int y)
```

### `MainMenuController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/MainMenuController.cs`</sub>

Drives the cube spawn-room menu: Laboratory enters the lab at the entrance (Pharmee's gate flow then handles episode choice); Tutorial enters the same lab in ungraded guided-practice mode with everything unlocked; Settings toggles the settings panel; Quit exits the game. Plus the config-gated amber Demo button. The lab scene's ExperimentLauncher reads GameFlow.SelectedModuleId on load; the methane TUTORIAL EXPERIMENT is a separate thing, reached inside the lab via Pharmee's episode picker — not this Tutorial MODE button.

```csharp
static string ResolveLabTarget(ProgressionFlow flow, string fallback)
void OnLaboratory()
void OnDemoLaboratory()
void OnTutorialLaboratory()
void OnSettings()
void OnQuit()
```

### `MenuRoomFx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/MenuRoomFx.cs`</sub>

Brings the cube spawn room to life: the neon trim breathes, the room lights drift and occasionally flicker, and one strip arcs like a loose connection. Everything is driven from ONE component with per-object phase offsets rather than a pile of animators — a room where every light pulses in lockstep reads as a single blinking prop, whereas offset phases read as a room that is alive. All colour work goes through MaterialPropertyBlocks so no shared material is touched (the trim material is used all over the room, and instancing it would leak into other scenes).

```csharp
void Bind(Renderer[] trim, Light[] lights)
static float Breath(float time, float speed, float phase)
```

### `PostLabController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/PostLabController.cs`</sub>

The post-lab "Documentation" phase (manual's Data Sheet + quiz), shown on a world-space tablet once the Chemical Tests phase is complete. The player enters the module's multiple-choice questions; submitting completes the terminal data-sheet task and ends the attempt with the quiz score feeding the grader's Documentation criterion. All UI refs are optional so the open→answer→submit→finish logic is unit-testable headlessly (a scene-built canvas drives the same public methods).

```csharp
bool IsOpen
QuizBank Bank
int CurrentIndex
void SetRefs(ExperimentRunner r, QuizBankLibrary lib)
void Open()
void OpenFor(QuizBank bank)
void OnOptionSelected(int optionIndex)
static bool CanGoBack(int index)
static bool CanGoNext(int index, int count)
void PreviousQuestion()
void NextQuestion()
void AnswerCurrent(int optionIndex)
void Answer(int questionIndex, int optionIndex)
bool AllAnswered
```

### `ProcessReadout` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ProcessReadout.cs`</sub>

A live world-space readout floating over a vessel — an in-game instrument panel (text that reflects the temperature; other experiments that require heating use it for monitoring). Shows "Hard-glass tube / 62 C -> 120 C" while heating, or "Gas collection tube / Collecting 45%" while a gas fills, and tints from cool blue to hot orange. REUSABLE: attach to ANY heated vessel in ANY experiment and BindHeat() it to that vessel's TemperatureSim. It reuses the suite-pinned VesselStatusMath formats, so it reads identically to the station billboards. NOTE: this does NOT replace the thermometer apparatus — the thermometer stays on the bench for the experiments that call for it (client rule: all tools are always present). This is an extra monitoring aid, not a substitute.

```csharp
void BindHeat(string baseLabel, TemperatureSim temp, float targetC, float ambientC
void BindCollect(string baseLabel, GasCollection gas)
static bool ShouldShow(bool hasHeat, float currentC, float ambientC, bool hasGas, float fill01)
string Compose()
```

### `ProximityLabel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ProximityLabel.cs`</sub>

Shows a small floating name tag above an object only when the player's camera is within range — so apparatus and reagents identify themselves as you approach, without cluttering the lab with permanent labels. Builds its own world-space TMP child on first enable; billboards to the camera.

```csharp
string CurrentText
GameObject Tag
void SetDeclutterLift(float metres)
const float LiftGlideSeconds
void SetDuplicateHidden(bool hidden)
bool WantsToShow
bool IsGuided
string Text
int Serial
Vector3 RestPosition
float ScreenHalfWidth(Camera cam)
float ScreenHalfHeight(Camera cam)
void SetLabel(string text, float dist
const float TutorialRadiusMultiplier
```

### `ResultsHistoryController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ResultsHistoryController.cs`</sub>

The local Results/History screen (plan §S2 — the descoped analytics dashboard): shows each experiment's best grade / mastery / attempts + overall completion, and writes a spreadsheet-ready CSV to persistentDataPath. Reads the live ProgressionService; the formatting is a pure static so it is unit-testable.

```csharp
void Show()
void Hide()
void Refresh()
static string BuildDisplayText(ProgressionService service)
void ExportCsv()
```

### `RoundedCorners` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/RoundedCorners.cs`</sub>

Soft rounded corners for every UI panel and button, matching the item name tags. A sprite-less Image draws a hard rectangle, so each one gets the shared 9-sliced rounded sprite (Resources/UI/RoundedPanel), with a corner radius scaled to that Image's own size: panels get a generous curve, buttons a smaller one, thin bars and slider handles become pills. Real art (portraits, icons) and fill bars are left alone. Baked into both scenes by Tools ▸ PharmaSynth ▸ Round UI Corners, and applied again on every scene load, so a panel a builder menu recreates can never come back square.

```csharp
const string ResourcePath
const int SpritePixels
const float MaxRadius
const float StripeMax
static float RadiusFor(Vector2 size)
static float Multiplier(float radius, float canvasPixelsPerUnit, float spritePixelsPerUnit)
static float Alpha(int x, int y)
static float RingAlpha(int x, int y)
const int RingPixels
const string RingResourcePath
const string EdgeName
static Sprite Sprite
static Sprite Ring
static bool Wants(UImage img, Sprite rounded)
```

### `FadeState` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ScreenFader.cs`</sub>

Pure fade math: eased alpha ramp between targets. Edit-mode testable.

```csharp
float Alpha
bool Busy
void Begin(float toAlpha, float seconds)
float Step(float dt)
static float Ease01(float t)
static float Pulse01(float t01, float peak)
```

### `ScreenFader` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/ScreenFader.cs`</sub>

VR-safe screen fade: a black quad floating just in front of the camera (a screen-space canvas is unreliable in the XR compositor). One instance per scene, parented under the active camera. Used for teleports + scene loads.

```csharp
static ScreenFader Instance
static bool HoldBlackOnStart
FadeState State
Renderer FadeRenderer
void PulseWarning(Color tint, float seconds
static Action Compose(Action first, Action second)
void FadeOut(float seconds
void FadeIn(float seconds
void FadeAround(Action mid, float outSeconds
static void FadeOutThen(Action act, float seconds
```

### `Handedness` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/SettingsService.cs`</sub>

### `ComfortSettings` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/SettingsService.cs`</sub>

Plain, clamped comfort/accessibility settings (plan §3.9). Kept as a POCO so the clamping + defaults are unit-testable without PlayerPrefs or a scene.

```csharp
float textScale
float subtitleSpeed
float vignetteIntensity
float snapTurnAngle
bool seatedMode
Handedness handedness
bool reduceFlashing
void SetTextScale(float v)
void SetSubtitleSpeed(float v)
void SetVignette(float v)
void SetSnapTurnAngle(float deg)
ComfortSettings Clone()
static float SteadyGlobal(bool reduceFlashing)
```

### `SettingsService` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/SettingsService.cs`</sub>

Owns the live ComfortSettings, persists them to PlayerPrefs, and raises Changed so listeners (UI text scaler, tunneling vignette, snap-turn provider, subtitle controller, locomotion) re-apply. Audio volumes live in AudioService; this is the comfort/accessibility half of the Settings panel.

```csharp
static SettingsService Instance
ComfortSettings Settings
event Action<ComfortSettings> Changed
void Load()
void Save()
void SetTextScale(float v)
void SetSubtitleSpeed(float v)
void SetVignette(float v)
void SetSnapTurnAngle(float d)
void SetSeatedMode(bool on)
void SetHandedness(Handedness h)
void SetLeftHanded(bool on)
void SetReduceFlashing(bool on)
```

### `TabletChecklistController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/TabletChecklistController.cs`</sub>

Grab-able tablet's procedure card: Materials/Apparatus/Procedure checklist built from the module's graphTasks, auto-ticking as tasks complete, with the current available step marked. Also shows the balanced reaction footer. Renders a text checklist into a single TMP_Text (simple, robust); a per-item prefab version can layer on later. The line-building logic is a pure static so it is unit-testable.

```csharp
static string BuildChecklistText(TaskGraph graph)
static string PhaseLabel(TaskPhase p)
```

### `TextTheme` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/TextTheme.cs`</sub>

The game's one text style: the font every text uses (HUD, dialogue, labels, panels, the cube room, the cinema) and the palette text is drawn in. Lives at Resources/UI/TextTheme; swapping the font here changes every text in the game. It is also TextMesh Pro's default font (TMP Settings), so text created at runtime picks it up; every text already in a scene is baked by Tools ▸ PharmaSynth ▸ Apply Text Theme and re-applied on each scene load, so nothing drifts back to another font.

```csharp
const string ResourcePath
TMP_FontAsset font
Color body
Color accent
Color muted
Color warning
static TextTheme Current
static TMP_FontAsset Font
static bool Apply(TMP_Text t)
static bool UsesAtlasOf(Material m, TMP_FontAsset f)
static int ApplyAll(Scene scene)
```

### `TutorialCoach` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/TutorialCoach.cs`</sub>

Tutorial Mode's voice: the stuck-escalation ladder and the end-of-run summary. Both are "what practice mode says to a struggling player", and both read the same run counters, so they live together. ⚠ Level 3 deliberately speaks the task's EXISTING hint rather than new copy: 76 of 81 hint ACTION lines are voiced, and inventing coach dialogue would mean a fresh voice generation pass for every mistake class.

```csharp
const float NudgeAfter
static int LevelFor(float secondsOnStep)
static string SummaryText(int stepsDone, int corrections)
void Bind(ExperimentRunner r)
void Detach()
int StepsDone
int Corrections
void ShowSummary()
void HelpNow()
```

### `UiButtonSounds` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/UiButtonSounds.cs`</sub>

Adds audio feedback to a UI button: a soft blip when the pointer/ray hovers it and a click when it's pressed. Works with the XR ray (XRUIInputModule dispatches pointer-enter/click to these handlers) and the desktop mouse. Attach to any Button's GameObject — no per-button wiring, no new clips.

```csharp
void SetKeys(string hover, string click)
void OnPointerEnter(PointerEventData _)
void OnPointerClick(PointerEventData _)
```

### `VesselStatusMath` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/VesselStatusMath.cs`</sub>

Pure format functions for the live-status feedback layer: vessel name tags that show contents/volume, hover-card "Now:" lines, and station billboards that show temperature / sim progress. Kept plain so the suite pins every format. All output is TMP-safe ASCII except the em-dash, which the dialogue system already uses everywhere (LiberationSans has it); the degree glyph is deliberately avoided ("62 C", not "62°C").

```csharp
static string Compose(string displayName, string chemName, float ml)
static string ComposeMixed(string displayName, string ledgerSummary)
static string HoverLine(string chemName, float ml, string ledgerSummary, int ledgerCount)
static string HeatLine(string baseLabel, float currentC, float targetC)
static string TempGoalLine(float currentC, float targetC, bool chill)
static string NeedLine(string chemName, float have, float required, bool solid
static string RefusalLine(string reagent)
static string FreeVesselLine(bool poolMember, bool ambiguous, bool empty)
static string ProgressLine(string baseLabel, string verb, float frac01)
```

### `WristWatchController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/UI/WristWatchController.cs`</sub>

Wrist-flip progress tracker (the headline feature). Flipping the wrist so the watch face turns up (supination) while glancing toward it shows a compact panel: current step, progress %, mastery %. A button/thumbstick fallback toggles it so the feature works without the gesture (and is testable pre-HMD). Gesture detection is via the anchor transform's up-vector, which works for both controller supination and hand-tracking palm-up. Hysteresis prevents flicker.

```csharp
static bool SuppressNpcPokes
const float PreviewSeconds
void ShowProcedurePreview(float seconds
static string StepText(string label, string hint, bool tutorial)
static bool IsGazingAt(Vector3 headPos, Vector3 headForward, Vector3 targetPos, float dotThreshold)
```

---

## Safety

`Assets/PharmaSynth/Scripts/Safety/`

### `FumeHoodStatusLabel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/FumeHoodStatusLabel.cs`</sub>

Narrating label for the fume hood: the hood is an OPEN alcove — nothing to open or switch on; protection is purely WHERE the vessel is. The invisible WorkVolume made "am I in far enough?" a guess, so the label says what belongs inside and flips to a ✓ while a vessel is actually protected.

```csharp
void Bind(FumeHoodZone zone, ProximityLabel label)
static string StatusLine(string vesselInside)
```

### `FumeHoodZone` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/FumeHoodZone.cs`</sub>

A fume-hood working volume. Tracks whether the player's hand / active work is inside it, so toxic/volatile reagents can require the hood (plan §3.7).

```csharp
bool IsOccupied
bool Contains(Vector3 worldPos)
```

### `HazardZone` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/HazardZone.cs`</sub>

A hazard volume (spill, hot surface, corrosive) — contact reports a mistake to the runner and can trigger a visual/audio warning. Debounced so a dwell reports once.

```csharp
void SetRunner(ExperimentRunner r)
void Configure(ExperimentRunner r, LabErrorType type, string msg)
void SetPlayerRoot(Transform root)
void SetArmedCheck(System.Func<bool> armed)
static bool IsPlayer(Transform other, Transform playerRoot)
void Report()
```

### `LabAlarm` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/LabAlarm.cs`</sub>

The lab's hazard alarm (manuscript intro: errors trigger a "visual and auditory alert, such as flashing lights, warning messages, or alarm beeps"). One red ceiling fixture + one dynamic light (Quest-cheap), flashing for a few seconds with the alarm SFX whenever a dangerous mix/overheat fires. Built by Tools ▸ PharmaSynth ▸ Build Lab Alarm; re-triggers extend the window.

```csharp
static LabAlarm Instance
void Bind(Light l, Renderer fixture)
static void Trigger()
void TriggerNow()
static bool FlashOn(float sinceStart, float period)
```

### `PPEController` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/PPEController.cs`</sub>

PPE donning at the locker. The three pieces — lab coat, goggles, gloves — are donned INDIVIDUALLY (click each locker item; `PPEDonOnSelect` forwards the clicks here) and ALL THREE are required before an experiment can start (`PPEWorn` == all worn — the gate checks it). Per-piece visuals: coat + goggles + gloves appear on the mirror avatar (PlayerAvatar layer — mirror-only), and the gloves ALSO appear first-person on the controllers. `PPEWearablesBuilder` wires the visuals; `RemovePPE` (HUD Reset) strips everything.

```csharp
UnityEvent onPPEWorn
event Action PPEWornChanged
PPESetModel Set
bool PPEWorn
bool IsWorn(PPEPiece p)
string MissingSummary()
void Don(PPEPiece piece)
void DonPPE()
void RemovePPE()
void BindVisuals(GameObject[] coat, GameObject[] goggles, GameObject[] gloves)
```

### `PPEDonOnSelect` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/PPEDonOnSelect.cs`</sub>

Forwards an XR select on a locker PPE item (coat display / goggles / gloves) to `PPEController.Don(piece)` — each piece is donned individually. Attach next to an XRSimpleInteractable; `PPEWearablesBuilder` wires these.

```csharp
void Bind(PPEController c, PPEPiece p)
```

### `PPEPiece` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/PPESetModel.cs`</sub>

The three personal-protective-equipment pieces (goggles + gloves are required alongside the coat before experimenting).

### `PPESetModel` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/PPESetModel.cs`</sub>

Pure PPE-worn state (no Unity types — edit-mode testable). The PPEController MonoBehaviour drives visuals/audio from this.

```csharp
bool IsWorn(PPEPiece p)
bool AllWorn
int WornCount
bool Don(PPEPiece p)
bool Clear()
string MissingSummary()
```

### `WearableReseat` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/WearableReseat.cs`</sub>

Restores the PPE locker displays (lab coat / goggles / gloves) to their original pegs on demand, so a restart from the lab tour can never leave a piece missing and the campaign unenterable. Snapshots each display's local pose + active state at scene start; `Reseat()` puts them back AND strips any worn PPE, giving a clean, dressable locker every time the player is asked to gear up (gate CoatPrompt) or restarts (ResetToEntrance).

```csharp
static WearableReseat Instance
```

### `Snap` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Safety/WearableReseat.cs`</sub>

```csharp
void Bind(PPEController controller, string[] names)
void Reseat()
```

---

## Audio

`Assets/PharmaSynth/Scripts/Audio/`

### `AudioCategory` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/AudioService.cs`</sub>

Audio mixer categories. Volume sliders in Settings map 1:1 to these.

### `VolumeUtil` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/AudioService.cs`</sub>

Pure volume maths — a perceptual linear(0..1)→decibel curve for an AudioMixer exposed parameter. Kept separate so it is unit-testable without a mixer asset.

```csharp
const float MinDb
static float LinearToDb(float linear01)
```

### `AudioService` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/AudioService.cs`</sub>

One-stop audio playback + volume control. Holds a per-category AudioSource and a SoundBank; gameplay calls Play("key") / PlayAt(...) and sets category volumes (persisted to PlayerPrefs, and pushed to an optional AudioMixer's exposed "<Category>Volume" params). Clips are assigned in the SoundBank later — the whole service works (as silent no-ops) with zero audio assets, so it can ship now.

```csharp
static AudioService Instance
static float JitteredPitch(float amount, float rand01)
static bool PitchVaries(string key)
static void TryPlay(string key)
SoundBank.Entry EntryOf(string key)
AudioSource AmbientSource
AudioSource MusicSource
float VolumeOf(AudioCategory c)
void SetVolume(AudioCategory c, float linear01)
void LoadVolumes()
void Play(string key)
void PlayAt(string key, Vector3 pos)
void PlayAt3D(string key, Vector3 pos, float volumeScale
static void TryPlayAt(string key, Vector3 pos, float volumeScale
```

### `DialogueDucker` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/DialogueDucker.cs`</sub>

Ducks the looping ambient + music beds while ANY NPC is speaking, so dialogue reads clearly, then eases them back afterward (UX pass). Ref-counts speakers so overlapping lines (Pharmee + Dr. Jimenez) hold the dip until both stop. Subscribes to every NPCNarrationController in the scene at Start — no wiring needed. Only touches the source volumes while a duck is active; otherwise leaves them to AudioService / the settings sliders.

```csharp
static float DuckTarget(int speakers, float factor)
int Speakers
```

### `MusicDucker` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/MusicDucker.cs`</sub>

Pulls the background music down while an NPC is speaking, so dialogue is always clearly audible over it (the background music must never mask what the player needs to hear), then eases it back afterwards. Ducks FAST and releases SLOWLY — the standard broadcast shape. A quick duck means the first syllable is never buried; a slow release stops the music pumping up and down between two lines of the same conversation.

```csharp
float attackPerSecond
float releasePerSecond
void Bind(AudioSource src)
static float TargetVolume(float baseVolume, float duckTo, bool speaking)
```

### `MusicSpeaker` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/MusicSpeaker.cs`</sub>

A physical music speaker in the lab: a 3D positional source standing in a corner that plays a PLAYLIST of background tracks — louder as you approach it (logarithmic rolloff), quieter across the room. Its volume also smoothly fades in/out with the screen fade, so moving between the menu room and the lab crossfades the music instead of hard-cutting it. Thin MonoBehaviour: playlist advance + fade envelope are pure/testable; the AudioSource is built at runtime so the clip list can be swapped by a builder.

```csharp
bool IsPlaying
float CurrentGain
void Configure(AudioClip[] clips, float volume, float minD, float maxD)
static int NextIndex(int current, int count, bool shuffle, float rand01)
```

### `NpcFootsteps` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/NpcFootsteps.cs`</sub>

Subtle positional footsteps for a walking NPC (Dr. Jimenez). Tracks its own transform's horizontal movement and plays a quiet 3D footstep at its feet once per stride — so his roaming reads audibly but stays background (distance rolloff keeps it soft across the room). Reuses the shared StrideMath + the SoundBank footstep clip at a lowered, own-source volume.

### `PlaySoundOnEnable` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/PlaySoundOnEnable.cs`</sub>

Plays a SoundBank key through the AudioService when the scene starts — used for looping beds (menu music, lab ambient). No-op if there's no AudioService/clip.

```csharp
void Play()
```

### `ProximityHum` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/ProximityHum.cs`</sub>

Positional machine hum (a subtle AC-unit hum from the AC assets that grows a little louder up close). Builds its own looping 3D AudioSource from a SoundBank key — quiet across the room, louder up close via a logarithmic rolloff. Attach to each AC unit (LabAudioBuilder wires them); no-ops silently when the clip isn't supplied yet.

```csharp
bool IsHumming
void Bind(string key, float vol)
```

### `RobotVocoder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/RobotVocoder.cs`</sub>

A vocoder attacks the source instead. It splits the voice into N frequency bands and measures only the ENERGY ENVELOPE of each — that envelope is what carries the words, and it contains no pitch information at all. Those envelopes are then imposed on a SYNTHETIC carrier at a fixed pitch. The consonants and vowels survive; the vocal cords are thrown away and replaced by an oscillator. That is the difference between "human with an effect" and "a machine talking". Keeping her FEMALE: the carrier pitch is the voice's new pitch, so a carrier in the 180-260 Hz range reads female while staying perfectly monotone — and monotone is a large part of what reads as robotic. Unvoiced sounds (s, f, sh, t) contain no pitch, so a pure tonal carrier turns them to mush. The carrier therefore blends in noise, which is what restores intelligibility on sibilants.

```csharp
const int MaxBands
const int MaxChannels
int Bands
static float BandCenter(int i, int bands, float lowHz, float highHz)
static float EnvelopeCoeff(float ms, int sampleRate)
void Configure(int bands, int channels, int sampleRate, float lowHz, float highHz, float q, float envMs)
void Reset()
float Process(int channel, float voice, float carrier)
```

### `RobotVoiceFx` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/RobotVoiceFx.cs`</sub>

are kept so nobody rebuilds them: • Heavy band-limiting -> "muffled". Fixed by opening the low-pass to 11 kHz and adding the presence lift; those two ARE still in use. • Decimation + comb   -> technically more machine-like, but the verdict was "sounds so human" — they recolour timbre without touching pitch contour. • Vocoder             -> the textbook robot technique (rebuilds the words on a synthetic monotone carrier, discarding the vocal cords entirely) and the verdict was "so much worse". So: the strongest-on-paper techniques lost to the simplest one. Do not swap the character back without asking — every alternative here has already been heard and rejected. Everything stays blended enough to keep the words legible: this is a teaching game and Pharmee explains procedure.

```csharp
RobotVoiceProfile profile
bool active
float VocoderMix
float CarrierHz
float CarrierNoise
float VocoderBands
float VocoderQ
float Downsample
float CombMs
float CombFeedback
float CombMix
float CrushBits
float CrushMix
float RingHz
```

### `RobotVoiceProfile` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/RobotVoiceProfile.cs`</sub>

component. So you can tune by ear in a live session and keep what you land on. ⭐ THE CHOSEN CHARACTER (picked after auditioning all the alternatives): RING MODULATION at a LOW carrier, blended with dry signal. It sounds like "speaking against an electric fan", which is intended — just not too strong. This was arrived at by elimination, so do not "improve" it away: • Heavier band-limiting  -> "muffled"           (rejected) • Decimation + comb      -> "sounds so human"   (rejected — it recolours timbre but leaves the human pitch contour) • Full VOCODER           -> "so much worse"     (rejected outright, even though it is the textbook robot technique) The vocoder, decimation and comb code all still exist and are simply switched off here. Turning them back on is a values change, not a code change — but ask first, because every one of them has already been auditioned and refused.

```csharp
bool active
```

### `SoundBank` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/SoundBank.cs`</sub>

Named clip lookup so gameplay code triggers sounds by key (e.g. "pour", "glass-shatter", "pharmee-warn") and the actual AudioClips are assigned in the asset later — no code change when the audio pass lands. Entries with a null clip are valid placeholders (AudioService no-ops on them).

### `Entry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/SoundBank.cs`</sub>

```csharp
List<Entry> entries
Entry Get(string key)
bool HasClip(string key)
static readonly string[] ExpectedKeys
```

### `SpeakerLedBlink` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/SpeakerLedBlink.cs`</sub>

Makes the lab speaker cabinet's power LED blink like a real machine's standby light: a crisp bright pulse with a long dark gap, rather than a flat always-on emissive. Driven through a MaterialPropertyBlock so the shared SpeakerLED material is never instanced (edit-mode safe). Thin MonoBehaviour: the envelope is a pure static function; the renderer is bound through Bind() because AddComponent doesn't fire Awake in edit mode.

```csharp
float CurrentLevel
void Bind(Color c, float cyclePeriod, float lit, float peak)
static float Level01(float t, float period, float onFraction, float edgeSeconds, float dimLevel)
```

### `VoiceSpeaker` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceBank.cs`</sub>

### `VoiceBank` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceBank.cs`</sub>

Voice-over clip lookup (Pharmee + Dr. Jimenez SPEAK their lines). Keyed by speaker + the normalised-text hash (VoiceLineId), so no line/SO schema changes anywhere — a missing clip simply falls back to the existing blip + typewriter. Rebuilt from Audio/Voice/<speaker>/<id>.mp3 by Tools ▸ PharmaSynth ▸ Voice ▸ Import & Wire Voice Clips.

### `Entry` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceBank.cs`</sub>

```csharp
List<Entry> entries
AudioClip Get(VoiceSpeaker speaker, string id)
void Rebuild()
int BrokenEntries()
```

### `VoiceLineId` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceBank.cs`</sub>

Stable line-id: FNV-1a 64-bit over the whitespace-normalised subtitle text. The generation script names files by this id, so a changed line regenerates exactly one clip and stale clips simply stop matching.

```csharp
static string For(string text)
static string Normalize(string text)
```

### `VoiceCorpus` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceCorpus.cs`</sub>

Every code-authored NPC line, with its speaker — the voice-over corpus (both NPCs speak). The manifest exporter adds the cutscene SO beats on top (assets aren't reachable from runtime code). Numbers were deliberately kept OUT of spoken lines (grade bands, finite unlock variants), so this enumeration is exhaustive and finite.

### `Line` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Audio/VoiceCorpus.cs`</sub>

```csharp
static List<Line> DataLines(ExperimentLibrary library)
static List<Line> CodeLines()
static readonly string[] ModuleIds
```

---

## Editor

`Assets/PharmaSynth/Scripts/Editor/`

### `AlignExperimentStages` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/AlignExperimentStages.cs`</sub>

Experiments spawn their stations/vessels/labels/ waypoints as children of DynamicStage (and Methane uses MethaneStage), both authored at the center table's ORIGINAL spot. This shifts both stages by the table's horizontal offset from that spot so all spawns land on the table wherever it now stands. Idempotent (absolute set from the current table position). Re-run after any further table move.

```csharp
static void Run()
```

### `AnchorFurniture` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/AnchorFurniture.cs`</sub>

Furniture is scenery, not equipment: the stools must not be grabbable. The six lab stools in the Environment prefab carry an `XRGrabInteractable` each, so a player could pick a stool up and carry it around the lab, or fling it across the bench mid-experiment. Nothing in the game asks them to move a stool, and every real interaction the lab needs is with glassware and tools. ⛔ The GRAB goes, the COLLIDER stays. The stool still stops the player walking through it and still holds anything resting on it — the same rule `Anchor Tube Racks` follows. The Rigidbody is left kinematic rather than destroyed so nothing doing `GetComponent&lt;Rigidbody&gt;()` starts null-referencing. Idempotent: a second run finds nothing to strip. Pinned by `bench: lab furniture is not grabbable`.

```csharp
static readonly string[] Fragments
static bool IsFurniture(string objectName)
static void Run()
```

### `AtmosphereBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/AtmosphereBuilder.cs`</sub>

Places the ambient atmosphere emitters: cool vapour sinking from the AC unit + a faint haze layer near the floor. Low-density on purpose (Quest overdraw). Door cold-air is code-hooked in DoorOpener, not here. Tools ▸ PharmaSynth ▸ Build Atmosphere VFX (SampleScene, edit mode, idempotent).

```csharp
static void Build()
```

### `ButtonSoundsBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ButtonSoundsBuilder.cs`</sub>

Adds UiButtonSounds (hover blip + click) to every UI Button in the OPEN scene. Run it once per scene — MainMenu (cube room) and SampleScene (HUD / choice panels / settings / grade / post-lab). Idempotent. Tools ▸ PharmaSynth ▸ Wire Button Sounds (edit mode).

```csharp
static void Build()
```

### `CenterTableBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/CenterTableBuilder.cs`</sub>

Merge Center Tables: removes the second table and makes the remaining one a single wide table at the centre of the room, in landscape. The experiment layouts bake WORLD positions on the left island, so this is a one-time atomic migration: 1. discover both islands geometrically (raycast under the baked positions and at their x-mirror; climb to the Environment child), 2. deactivate the right island (+ its sink follows to the new short end), 3. rigid-remap the left island 90° to the lab centre (landscape), 4. apply the SAME remap to every layout-SO position and every in-footprint scene prop (methane stage children, loose items, proctor points), 5. verify every remapped position still raycasts onto a deck. Re-run safe: once the layouts no longer sit on the old left-island footprint, the guard aborts before touching anything. Run Re-Home Scene Items after.

```csharp
static void Merge()
```

### `ChemHazardFlagAudit` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ChemHazardFlagAudit.cs`</sub>

Stamps the HazardousMix flags (isOxidizer / isConcentratedAcid) onto every ChemicalData asset from the pure HazardFlags name rules, and ensures the Chem_RuinedMixture SO (the dark sludge an overheated batch turns into) exists and is registered in the SceneAssetLibrary. Idempotent.

```csharp
static void Audit()
static ChemicalData EnsureRuinedMixture()
```

### `ChemicalStateAudit` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ChemicalStateAudit.cs`</sub>

Reagent-nature audit: checks whether each reagent is really a liquid or a solid that has to be scooped. Dumps every ChemicalData's state/flags to Temp/chemical-state-audit.md and flags chemicals that are solids in their common lab form but are marked Liquid (manuscript solutions like "10% NaOH" are correctly liquid — only pure solids that get weighed/scooped are suspects).

```csharp
static void Run()
```

### `CinemaCutsceneTools` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/CinemaCutsceneTools.cs`</sub>

Builder + review menus for cinematic cutscenes (CinemaView / CinemaShot). - Build Cutscene Markers: idempotent; creates the `CutsceneMarkers` root (with the CinemaView) and any MISSING marker at a default pose. Never moves an existing marker — hand-tuned framing survives a re-run. - Render Test Sequence Shots: edit mode; writes the start/middle/end frame of every shot to Logs/cutscene-shots/test/ so framing is reviewable without Play mode. - Preview Test Sequence: Play mode; plays it for real (fades, subtitles, return).

```csharp
const string Menu
static readonly (string name, Vector3 pos, Vector3 look)[] DefaultMarkers
static void BuildMarkers()
static CutsceneData TestSequence()
const string LibraryPath
static CinemaShot TemplateShot(CutsceneData.Kind kind, int beat, int beatCount
static IEnumerable<CutsceneData> TemplatedCutscenes(CutsceneLibrary lib)
static int ApplyShots()
static void RenderLibrary()
static void PreviewTimelapse()
static void PreviewTest()
static void RenderTest()
static int RenderShots(CutsceneData data, string folder)
```

### `CompactHudBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/CompactHudBuilder.cs`</sub>

Compact VR HUD layout: the timer + title move OUT of the centre and merge into the Progress pill on the LEFT (small, stacked); the top cluster tucks to the top edge; the three Settings/Restart/Quit buttons collapse behind ONE hamburger icon that opens a vertical dropdown; and Pharmee's bottom dialogue bar is raised so it's fully in view. Idempotent — re-run after tuning the constants below. Operates on the open scene's HudRig (SampleScene only).

```csharp
static void Rebuild()
static void AppendToMenu(RectTransform list, RectTransform button)
```

### `CutsceneReelRecorder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/CutsceneReelRecorder.cs`</sub>

Records every cinematic cutscene and timelapse to video, as the player sees it (the dark room, the screen, the title card and subtitles), for client review. `Cutscenes ▸ Record Cutscene Reel` enters Play mode; the runner plays the 27 prep/success/failure cinematics and the 6 timelapses back to back at a locked 30 fps (Time.captureFramerate, so slow capture never changes timing) and pipes raw frames into ffmpeg — one silent MP4 per clip under Logs/cutscene-reel/. Voice lines are logged with their frame offsets in manifest.txt so the audio can be laid back on offline (Tools/cutscene-reel.py), since captureFramerate decouples game time from the audio clock.

```csharp
const string OutDir
static void Record()
```

### `DemoModeBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/DemoModeBuilder.cs`</sub>

Demo Mode scene wiring. Three menus: • Build Demo Menu Button — MainMenu scene: clones the Laboratory button into a config-gated "Demo Mode" button wired to MainMenuController.OnDemoLaboratory. • Build Demo HUD — SampleScene: a Skip Step / Finish Experiment / Auto-Answer Quiz row under the HUD's top-right cluster, driven by DemoHudController. • Demo Enabled (persistent override) — toggles persistentDataPath/demo-config.json for in-editor testing (the shipped StreamingAssets default stays false). All idempotent.

```csharp
static void BuildMenuButton()
static void BuildDemoHud()
static void ToggleOverride()
static bool ToggleOverrideValidate()
```

### `DevCapture` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/DevCapture.cs`</sub>

Dev-only capture bridge: renders a one-off camera to a PNG on disk so out-of-editor tooling can see the scene (the MCP scene-preview capture is broken on this machine). Pose/output come from Temp/dev-capture-request.json when present; defaults to the player spawn head pose.

### `Request` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/DevCapture.cs`</sub>

```csharp
const string RequestPath
static void Capture()
```

### `EndProductGateBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/EndProductGateBuilder.cs`</sub>

A ready-made GOAL destroys the experiment — attaches EndProductVisibility to both storage roots and binds the runner, so each product bottle SetActive(false)s WHILE ITS OWN MODULE RUNS. The gate is per-EXPERIMENT, not per-chemical: Ethanol, Acetone and Benzoic Acid are each some module's goal AND a manuscript-listed reagent for others, so a global hide would strip Exp 2 (which runs before Exp 3/6) of reagents it needs. The four PURE products are not on the shelf at all, so there is nothing to gate. See EndProductVisibility. Idempotent; safe to re-run after any storage rework.

```csharp
static void Wire()
```

### `EndProductShelfStocker` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/EndProductShelfStocker.cs`</sub>

Acetanilide · Benzamide · Chloroform · Wine are named as a reagent by NO manuscript procedure — their own chemical tests consume the product the player just made (Exp 5's "place 1 gram of acetanilide in a test tube" is testing YOUR synthesis). They are never inputs to anything, so they are simply absent rather than gated. Ethanol · Acetone · Benzoic Acid are the exception: each is some module's goal AND a manuscript-listed reagent for others (Ethanol → Exp 2, 6; Acetone → Exp 2, 7; Benzoic Acid → Exp 4). They stock as ordinary RAW reagents via RawReagentCatalog / ReagentCabinetBuilder, and EndProductVisibility hides each ONLY while its own module is running — see that class. Run order after this: Generate Reagent Labels → Wire End-Product Gate → Wire Shelf Pourers → Re-Home Scene Items (Adopt Current).

```csharp
static void Stock()
```

### `EntranceSealBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/EntranceSealBuilder.cs`</sub>

Seals the skybox seams at BOTH entrances (front corridor door + interior lab door). Each doorway's frame doesn't quite meet the surrounding wall, so the skybox shows through thin cracks at the jambs/lintel. This wraps each doorway with opaque trim strips (top lintel + two jambs) centred on the wall plane and standing slightly proud on both faces, so the seam is covered whether viewed from the corridor or the lab. Reuses GapSealWall's dark material for a consistent look; re-runnable. Tools ▸ PharmaSynth ▸ Seal Entrance Gaps (SampleScene, edit mode, idempotent).

```csharp
static void Build()
```

### `FixtureTools` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/FixtureTools.cs`</sub>

Edit-mode helper so the sinks, speaker, tables and shelves can be repositioned by hand in the editor. Two things block Scene-view clicks: 1. The Environment furniture (tables, wall cabinets, wash-table sinks) has PICKING DISABLED (the pointer toggle in the Hierarchy), as do the stools. 2. Some fixtures (the LabSpeaker, and the shelf ROOTS that sit at the origin with their meshes parented elsewhere) have no click target, so a click selects a child prop instead of the movable unit. This re-enables picking on every fixture, ensures a click collider where one is missing, and selects them all so they show in the Hierarchy — click the one you want in the Scene view and move it with the gizmo (W), then Ctrl-S. Tools ▸ PharmaSynth ▸ Select Movable Furniture (edit mode).

```csharp
static void SelectFurniture()
```

### `FumeHoodSwap` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/FumeHoodSwap.cs`</sub>

Swaps the sealed Tripo fume-hood model (a sash, but not hollow inside) for the OPEN-SASH, hollow-chamber one. Idempotent: • deactivates the old FumeHoodModel (kept in the scene for hand-deletion rather than deleted by the tool), • mounts Art/Generated/Refs/FumeHoodOpen.prefab under FumeHood_StandIn, height-normalised to the house 2.35 m, • re-fits the WorkVolume trigger into the new chamber (upper-front region — hand-tune afterwards, the wire box is visible when selected), • rebuilds the HoodShell walls from the refit volume (front stays open).

```csharp
static void Swap()
```

### `GrabMovementTools` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/GrabMovementTools.cs`</sub>

Applies the GrabTuning velocity-tracked profile to every grabbable so held items collide with the world (without it, props can be pushed through walls/floor). Covers the SceneAssetLibrary prefabs (persisted) AND every XRGrabInteractable in the open scene(s) (catches instance overrides, stools, shelf bottles). Idempotent — re-running reports 0 changes.

```csharp
static void Wire()
```

### `GradeBackButtonBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/GradeBackButtonBuilder.cs`</sub>

Wires a "Complete Experiment" button onto the grade screen (shown only on FAIL, where Continue is hidden; they share the slot) that ends the attempt and returns the player to the entrance, where any unlocked experiment can be picked, so a failed attempt is never a Retry-only loop. Idempotent.

```csharp
static void Wire()
```

### `GradeCardStyler` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/GradeCardStyler.cs`</sub>

Restyles the grade screen into the client slides' "Mission Complete" card: a row of three white tiles (Time · Mistakes · Grade) with large blue figures, the verdict under them, the rubric breakdown below in a smaller type, buttons unchanged. Idempotent: tiles are found-or-created and every rect is SET, never nudged. The controller's text references are untouched (it still fills the same objects).

```csharp
static void Style()
```

### `HandVisualsBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/HandVisualsBuilder.cs`</sub>

Controllers are REPLACED by skinned hands: XR Hands sample meshes (Art/Hands/LeftHand.fbx + RightHand.fbx, real finger bones). Two skins: bare (HandSkin.mat) / nitrile blue (HandNitrile.mat) driven by the PPE gloves state; two poses: free / grab (finger curl while selecting) driven by HandPoseController. Removes any procedural mittens, HandVisualKeeper AND FPGlove_* first-person glove clones (PPE visuals bind to the mirror gloves only; first-person gloving is the material swap). Tools ▸ PharmaSynth ▸ Build Hand Visuals — run per scene, edit mode.

```csharp
static void Build()
```

### `HideStationPads` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/HideStationPads.cs`</sub>

The hand-built Methane station pads render as coloured cubes on the table. The DynamicStage builder hides its own pads (padMr.enabled = false) but not the authored Station_* objects. The pads are purely cosmetic: the trigger COLLIDER + sensors that detect each step stay, and the guides/labels tell the player where to act — so the cube mesh just clutters the view. This disables the MeshRenderer on every Station_* pad while leaving all functionality intact. Idempotent.

```csharp
static void Run()
```

### `HoverInfoBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/HoverInfoBuilder.cs`</sub>

Builds the hover-inspector info card + wires the raycasting HoverInspector into SampleScene. Point the right-hand ray (or gaze) at a reagent, a piece of apparatus or an NPC and a smoothly-animated card names it and explains what it is / how to use it. Tools ▸ PharmaSynth ▸ Build Hover Info Panel (SampleScene, edit mode, idempotent).

```csharp
static void Build()
```

### `IloBeatInjector` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/IloBeatInjector.cs`</sub>

Injects each experiment's ILO beats into its Intro cutscene (Pharmee states the learning outcomes in the opening dialogue). Beats slot in after the greeting beat: a lead-in, then one beat per objective (verbatim Appendix C copy from IloCopy). Idempotent — the lead-in text is the marker.

```csharp
static readonly (string moduleId, string prefix)[] Modules
static void Inject()
static int InjectInto(CutsceneData data, string moduleId)
```

### `JimenezArmDamper` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezArmDamper.cs`</sub>

⛔ WHY THIS EXISTS INSTEAD OF A RE-WEIGHT. Moving the coat's vertex weights off the arm bones does not work on this asset: the coat and the sleeve are one continuous surface with no seam to cut along, so any weight change big enough to free the coat is big enough to rip it (measured against the untouched mesh, worst edge stretch 4.2x → 14.9x and torn edges 32 → 174). Smoothing the transfer across a band removes the tearing but then barely moves the coat (1625 → 1557 dragged vertices). The geometry, not the weighting, is the limit. See JimenezRigMath.TransferFraction. So this attacks the OTHER side of the equation: the coat only drags as far as the arm swings. His clips are HUMANOID, so the knobs are muscle curves (-1..1 across each joint's range), not bone rotations — a bone-name scan finds nothing here. ⛔ Never compounds: the first run copies each clip to Originals/ and EVERY run re-deri

```csharp
const float DefaultFactor
static bool IsArmMuscle(string propertyName)
static void Run()
static void Run(float factor)
```

### `JimenezCoatRig` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezCoatRig.cs`</sub>

with the arm. ⛔ There is nothing to unparent. He is a Tripo auto-rig: ONE SkinnedMeshRenderer over 41 bones, with the coat baked into the body mesh. The coat follows the arm because coat VERTICES carry weight on the arm bones — an auto-rigger assigns weight by proximity, so a coat panel hanging near the elbow picks up elbow weight even though a real coat hangs from the shoulders. This finds that bleed geometrically (a vertex carrying arm weight while sitting well outside the arm) and hands the weight to the spine. The sleeve is kept: anything within `sleeveRadius` of the arm bone still follows it, or he would wear a rigid tube. ⛔ It ALWAYS rebuilds from the ORIGINAL imported mesh, never from its own output, so re-running at a different radius cannot compound. The source .glb is never modified and the change is undone by clearing the mesh override on the prefab.

```csharp
static void Run()
static void Run(float sleeveRadius)
```

### `JimenezHudPortraitBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/JimenezHudPortraitBuilder.cs`</sub>

Gives Dr. Jimenez a HUD presence equal to Pharmee's — he appears in the HUD with his own icon image, just as she does. Two jobs, both idempotent: 1. RENDER his portrait from his own rigged model: a transparent-background headshot framed on the humanoid Head bone, so the icon IS the character the player meets. → Art/UI/jimenez_icon.png, imported with pharmee_icon.png's settings (Sprite/Single, alpha, no mips). 2. WIRE the HudDialogueBar for two speakers: the existing DialogueBar Portrait Image + Pharmee's icon as the primary channel, and Jimenez's narration + name + new icon as an extra channel. Without it his lines appear only in his own world bubble and the HUD bar shows nothing.

```csharp
static void Run()
static void RunForce()
```

### `LabAlarmBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabAlarmBuilder.cs`</sub>

Builds the lab's hazard-alarm fixture (manuscript: "flashing lights, warning messages, alarm beeps"): a small red ceiling box + one red point light + LabAlarm, centred over the lab. Idempotent.

```csharp
static void Build()
```

### `LabDoorFixBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabDoorFixBuilder.cs`</sub>

1. The leaf inherits the Environment prefab's `m_StaticEditorFlags = 87`, which includes **Batching Static**. A static-batched renderer is merged into a combined mesh at load and then IGNORES its transform, so the leaf's collider swings open while the drawn door stays shut, and the player walks through what looks like a closed door. Contribute GI goes with it: a door that moves must not carry a lightmap baked in the closed position. 2. A null `doorBlocker` leaves nothing to hold the player back while the gate is still talking. The blocker is a plain trigger-free box across the doorway that the gatekeeper enables and disables; it is created here so the scene does not depend on a hand-placed one. Idempotent: re-running finds the same objects and rewrites the same values. ⚠ The leaf's lightmap- and navmesh-static flags change, so after running this go through `Build Lab Probes` → `Run Lab L

```csharp
static void Build()
```

### `LabLightingBake` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabLightingBake.cs`</sub>

floats. Baked GI is also FREE at runtime, which is why it is the right tool on a Quest. The three steps that have to happen in THIS order, because each depends on the last: 1. Lightmap UVs. A mesh with no UV2 cannot receive a lightmap. 36 of the project's 107 models lack them, including the entire room shell (Wall, Floor, Ceiling_2, the tables). Anything still missing UV2 after the import pass is set to receive from LIGHT PROBES instead, so it still occludes and bounces without a broken lightmap. 2. Static flags, filtered HARD. A grabbable baked into a lightmap carries its baked shadow around the room in your hand, so anything with a Rigidbody, an XRGrabInteractable, a DropRespawn or a LabItem is excluded, as is everything the stage builder spawns. 3. Light modes. Realtime lights contribute nothing to a bake. Tools > PharmaSynth > Prepare Lab Lighting Bake  - steps 1-3, no bake (fast, in

```csharp
static void Prepare()
static void Run()
```

### `LabLightingBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabLightingBuilder.cs`</sub>

Brightens the lab so it reads as a well-lit laboratory. Quest-friendly recipe, with NO extra shadow-casting lights: 1. The 16 ceiling `Light (n)` fixture meshes get a white EMISSIVE panel material. 2. Ambient is applied from LabRenderTuner (a ceiling-to-floor gradient). 3. A small grid of shadowless point lights (`LabLights` group, re-runnable) fills the room — 6 lights, wide range, warm-white, no shadows (URP per-object cap safe). 4. The directional key light keeps shadows but drops a touch so it doesn't blow out. Tools ▸ PharmaSynth ▸ Brighten Lab Lighting (run in SampleScene, edit mode, idempotent).

```csharp
static void Build()
```

### `LabNavMeshBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabNavMeshBuilder.cs`</sub>

Bakes the walkable surface Tutorial Mode's ground path routes on. Without this there is no NavMesh in the project at all, and `GuidePath` silently draws nothing, so the floor arrows look "not implemented" rather than "not baked". ⚠ **The bake goes STALE when furniture moves**, like the lightmap bake: the benches ARE the obstacles it routes around. Re-run this together with `Build Lab Probes` → `Run Lab Lighting Bake` after any `Select Movable Furniture` session, or the arrows route through a bench that has since moved.

```csharp
static void Build()
```

### `LabNpcPolishBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabNpcPolishBuilder.cs`</sub>

Wires the NPC/audio polish into SampleScene: 1. Pharmee expressions: PharmeeFace pointed at the robot's EYES + MOUTH meshes, default-happy; PharmeeMood resets the face after every line; the gatekeeper's faceBehaviour drives gate moods. 2. Dr. Jimenez proctor roaming — ProctorRoamer + observation points at the reagent shelf, equipment shelf, dynamic stage and fume hood. 3. AC proximity hum — ProximityHum on the air-con / vent assets (falls back to the fume hood if no AC mesh exists). Tools ▸ PharmaSynth ▸ Wire NPC Polish (SampleScene, edit mode, idempotent).

```csharp
const float JimenezIdleLift
static void Build()
```

### `LabProbeBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabProbeBuilder.cs`</sub>

The Mobile RP asset has reflection-probe blending AND box projection switched on, and both need probes to feed them. Without probes: * Every glass material in the ChemLab pack sets _EnvironmentReflections = 1, so it samples the DEFAULT reflection - the built-in procedural outdoor sky - inside a sealed windowless room, and beakers and flasks read as pale plastic. * Every dynamic object (all grabbable glassware, held items, Pharmee, Dr. Jimenez) is lit by flat ambient alone, so a beaker is lit identically under a closed cabinet and directly beneath a lamp. Probe placement is DERIVED from the room's own renderer bounds rather than hard-coded, so it survives the furniture moves the user makes through Select Movable Furniture. Tools > PharmaSynth > Build Lab Probes (edit mode, idempotent - deletes and rebuilds its own root). Run the lighting bake afterwards to populate them.

```csharp
static void Build()
```

### `LabRenderTuner` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabRenderTuner.cs`</sub>

* MSAA 1 (off). On the Adreno tile GPU 4x resolves in tile memory and is close to free, and this scene is nothing but thin edges - tube rims, glass rods, rack rails. It is the largest per-pixel quality gain available. * HDR off, so emission above 1.0 just clamps: the 16 emissive ceiling panels can never read as LIGHTS, only as white paint, and tonemapping has no range to work with. * FLAT grey 0.45 ambient, identical from every direction, so nothing in the room has any vertical shading and the whole space reads as one plane of grey. * The built-in PROCEDURAL SKY, in a sealed windowless room, feeding ambient and every reflection an outdoor gradient. Vignette is deliberately DISABLED rather than tuned down: the template has it at full strength, so it switches on the moment post is enabled, and a vignette in a headset reads as a dirty lens rather than as framing. Tools > PharmaSynth > Tune 

```csharp
const int   Msaa
const float BloomIntensity
const float BloomThreshold
const float PostExposure
const float Contrast
const float Saturation
const float Temperature
static readonly Color AmbientSky
static readonly Color AmbientEquator
static readonly Color AmbientGround
static void Tune()
static void ApplyAmbient()
```

### `LabSurfaceTextureForge` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabSurfaceTextureForge.cs`</sub>

Generates the lab's MISSING surface textures. The Laboratory pack leaves the two largest surfaces in the player's view with no albedo at all - Wall_0/Wall_1 carry a normal map and nothing else, and Ceiling_2 carries no maps whatsoever. A featureless white plane makes the room read as an untextured grey box, and it is worst on the ceiling, which a seated VR player looks straight up into. Everything here is drawn in code and written to PNG, as LabelForge does for reagent labels: deterministic, re-runnable, diffable, and free. All noise wraps, so the maps tile seamlessly. Tools > PharmaSynth > Generate Lab Surface Textures (edit mode, idempotent, re-runnable).

```csharp
static void Generate()
```

### `LabSurfaceTuner` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabSurfaceTuner.cs`</sub>

a sealed indoor lab reads as wet plastic. * The benchtops sit at 1.00. A lab bench is matte epoxy resin. * The four vessel materials the player handles constantly - beaker 100/500, Erlenmeyer, graduated cylinder - sit at smoothness 0, and glass with NO specular and NO reflection reads as pale plastic blobs. The same pack ships CORRECT glass values on GlassMat/GlassInnerMat/GlassOuterMat (0.92-0.95). * Several non-metals sit at metallic 0 + smoothness 1 - a physically impossible "non-metal mirror" that produces a hard white specular blob instead of a highlight. Every value here is a judgement about what the surface IS, so they live in one table rather than being scattered. Suite-pinned (surface:) because a pack reimport or a stray inspector drag restores the bad values silently and pure math cannot see a material regression. Tools > PharmaSynth > Tune Lab Surfaces (edit mode, idempotent, 

### `Surface` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabSurfaceTuner.cs`</sub>

One surface's calibrated look. metallic < 0 means "leave it alone".

```csharp
static readonly Surface[] Surfaces
static void Tune()
```

### `LabelForge` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LabelForge.cs`</sub>

Reagent-label compositor (style: MODERN). For every Reagent_* bottle on the shelf: renders LabelBase_Modern + the chemical's name (crisp TMP text) to a PNG, builds a material, and mounts a label quad on the bottle facing the aisle. Tools ▸ PharmaSynth ▸ Generate Reagent Labels — idempotent, re-run anytime.

```csharp
static void Run()
```

### `LayoutTidy` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LayoutTidy.cs`</sub>

Re-seats every experiment layout onto the LayoutTidyMath zoning grid (a clean center table — stations across the back, vessels center-front, reagents right, tools left; the front strip stays free for the rack and spares). Deterministic + idempotent; also structurally removes the two clamped overlaps at (1.38, −3.88) in Acetone and Benzamide. Run AFTER `Apply Verb Data` so new stations/props get slots too.

```csharp
static void Run()
static int Tidy(ExperimentLayout layout)
```

### `LockMyLayout` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/LockMyLayout.cs`</sub>

One-click self-service layout lock: after moving or duplicating assets, one button does everything needed to make a hand-arranged workspace permanent — with NO rebuilding, so it can never reset placements: 1. Tidy duplicate names  — "Beaker (1)" → "Beaker_100mL_2", + full interaction wiring for any raw duplicate that was missing it. 2. Re-home every item     — current transform becomes its respawn home (moved originals AND duplicates), so nothing snaps back in Play. 3. Save the scene. Run this after ANY manual arrangement. Purely additive — spawns/destroys nothing.

```csharp
static void Run()
```

### `ManualLayoutAdopter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ManualLayoutAdopter.cs`</sub>

Adopts the hand-placed workspace (kits, duplicates, reagent shelf, spawn point) as the canonical layout in ONE run: 1. renames editor duplicates ("Beaker_100mL (1)") to clean unique names + display names, and gives them the full interaction wiring; 2. re-points the teleport target (FrontDoorSpawn) at the rig's current pose; 3. re-homes every DropRespawn to its current transform; 4. creates + registers the DistillingFlask if missing (its model is an unimported .glb, so no prefab exists) and parks it beside a graduated cylinder; 5. drops a ManualLayout_W512 marker that guards the shelf/kits builders from clobbering the hand layout on a re-run. Idempotent; run from SampleScene in edit mode.

```csharp
const string MarkerName
static bool LayoutIsManual()
static void Adopt()
```

### `ManuscriptDataApplier` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ManuscriptDataApplier.cs`</sub>

alone is chemically INERT) → propyl alcohol staged + bound, rule pointed at it (manuscript/test intent: ester with propyl). M2  Chloroform gets the manuscript's oxidation confirmatory test (K-dichromate + conc H2SO4, procedure L3419-21 + results sheet) → new task + reagent + reaction rule. M3  Wine Making ferments non-grape juice, per the manuscript's explicit grape exclusion (L3830-31) → Chem_GrapeJuice renamed "Mixed Fruit Juice" (GUID refs untouched; string lookups updated). M4  Reagent fidelity: Acetanilide prep-HCl 0.1N (not 6N); iodoform tests get their KI (Ethyl Alcohol + Acetone); ester/acid tests use the manuscript's DILUTED acids, with the matching RULE inputs re-pointed (rules match by asset; a layout-only swap would kill the reactions). M5a Chemical Compounding quiz Q3 uses the manuscript's KMnO4 oxidation (a bromine unsaturation question does not fit an all-saturated module)

```csharp
static void Apply()
```

### `MaterialsGuideGenerator` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MaterialsGuideGenerator.cs`</sub>

Fills every module's MATERIALS guide (the watch-panel header): every reagent and apparatus the module needs, listed first as a guide so the player can assemble them before starting. REAGENTS are DERIVED from the module's layout bindings — the ground truth of what the experiment actually consumes — with totals summed per chemical, so the guide can never drift from the tasks. Units follow the game's own convention (1 squeeze = 1 ml): liquids read "N ml", solids/powders "N g". APPARATUS is AUTHORED here per module, from the PROCEDURES — never from the manuscript's own apparatus lists, which are documented as defective (experiments-reference §Apparatus: "stage from the PROCEDURE, never the list"). Idempotent: re-run after any layout change.

```csharp
static void Run()
```

### `MenuAutoRun` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MenuAutoRun.cs`</sub>

Menu-execution twin of SelfTestAutoRun, for when the MCP bridge is down: if Logs/menu-autorun-request.txt exists, execute each listed line in order, capture the console output, write it to Logs/menu-autorun-result.txt, and consume the request. (Logs/, NOT Temp/ — Unity wipes Temp at editor startup, which destroys any request queued while the editor was closed.) Line forms: Tools/PharmaSynth/Wire Shelf Pourers      — execute that menu item OPEN Assets/Scenes/SampleScene.unity      — open that scene first (single mode) CAPTURE px py pz yaw pitch out.png        — DevCapture from that pose to the given path (relative to project) # comment                                 — ignored Runs on the next domain reload in an interactive editor, or via Unity.exe -batchmode -quit -projectPath <proj> -executeMethod MenuAutoRun.RunNow Harmless when no request file is present.

```csharp
static void RunNow()
```

### `MenuCubeRoomBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MenuCubeRoomBuilder.cs`</sub>

Builds the futuristic CUBE SPAWN ROOM in the MainMenu scene: a fully-enclosed, solid, dark room with cyan/teal emissive trim, a couple of soft lights and a glowing floor launch-pad under the menu panel. Sealed on all six sides so no skybox leaks. Re-runnable and idempotent: deletes the prior "MenuCubeRoom" and rebuilds it, and hides the legacy open "MenuRoom" dressing. Tools ▸ PharmaSynth ▸ Build Menu Cube Room. Run with the MainMenu scene open.

```csharp
static void Build()
```

### `MethaneAnchors` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MethaneAnchors.cs`</sub>

Creates the draggable placement anchors the verbs read. Each match/burner gets a "FlameAnchor" child and each scoopula/spatula a "ScoopAnchor" child, dropped at a best-guess spot. SELECT it in the Hierarchy (orange gizmo in the Scene view), drag it onto the exact part (match head, burner mouth, scoop bowl), then run Lock My Layout to bake it. Idempotent: never moves an anchor you've already positioned.

```csharp
static void Run()
```

### `MethaneApparatusGrab` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MethaneApparatusGrab.cs`</sub>

Guarantee the methane apparatus is pick-up-able ("I can't pick up the draw tubes"). Normalises every common grab-blocker on the hard-glass tube, collection tube and burner: active, on the Default layer, with an XRGrabInteractable (velocity-tracked + two-handed), a Rigidbody, a live convex collider, and the shelf/respawn policy. Idempotent.

```csharp
static void Run()
```

### `MethanePlaytestFix` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MethanePlaytestFix.cs`</sub>

Wires the location-free Methane rig's missing pieces: a workspace burner needs a BurnerController to be lit, striking a match needs a MatchStrikerSurface, and the Methane reagent jar needs a LiquidPhysics to be scooped/poured. Without them the heat step is blocked. • every Bunsen/Alcohol burner gets BurnerController + MatchStrikerSurface (strike a match on the burner base to light it, then it lights the tube), • any matchbox-like object becomes a striker too, • the reagent jar becomes a scoopable solid (Sodium Acetate). Idempotent.

```csharp
static void Run()
```

### `MethaneStageVisibilityBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MethaneStageVisibilityBuilder.cs`</sub>

Wires the MethaneStageVisibility controller (methane set present only in Lab Tour + the Methane attempt). Puts the component on ExperimentSystems (a manager that keeps running while the stage is hidden), binds the MethaneStage + runner + LabTourGuide, and re-hides the stage in the authored scene so it doesn't flash on lab entry. Idempotent.

```csharp
static void Wire()
```

### `MusicSpeakerBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/MusicSpeakerBuilder.cs`</sub>

Builds the corner music speaker in the lab: a floor-standing speaker cabinet in the empty back-right corner that plays the Background_Music/Lab playlist as a 3D positional source (louder as you approach) and fades in/out with the screen fade on menu<->lab transitions. Also disables the legacy 2D LabMusicPlayer bed and re-points the menu-room music to the supplied track. Tools ▸ PharmaSynth ▸ Build Lab Music Speaker (SampleScene, edit mode, idempotent).

```csharp
static void Build()
```

### `PPEWearablesBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PPEWearablesBuilder.cs`</sub>

Wires the per-piece wearable PPE: 1. The locker's goggles + gloves become CLICKABLE (collider + XRSimpleInteractable + PPEDonOnSelect), the coat display forwards Coat, and the legacy don-everything paths are disabled (host donOnSelect off, coat display's old persistent calls cleared). 2. Worn visuals cloned from the locker models onto the mirror avatar's bones (coat→Spine01, goggles→Head, gloves→hand bones — PlayerAvatar layer, mirror-only) and first-person gloves onto the controllers (main-camera visible). 3. All visuals assigned to PPEController's per-piece arrays, initially hidden. Tools ▸ PharmaSynth ▸ Build PPE Wearables (run in SampleScene AFTER Build Player Avatar, edit mode, idempotent).

```csharp
static void Build()
```

### `PanelConsolidationBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PanelConsolidationBuilder.cs`</sub>

One procedures display. The entrance LabTablet duplicates the wrist holo board and the wrist mini-panel duplicates the holo header: three surfaces for the same content, with the tablet's fixed rect overflowing into its reaction footer. This menu retires the LabTablet (deactivated, not deleted) and the MiniPanel, and upgrades the holo board to the single panel: status header (ex mini-panel) + focused checklist + the balanced-reaction footer (ex tablet). Idempotent.

```csharp
static void Consolidate()
static void FixHoloScroll()
```

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.Chemistry.cs`</sub>

Self-tests: Pouring, reactions, chemical tests, supply and shelf wiring.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.Experiment.cs`</sub>

Self-tests: Task graph, scoring, grading, runner, quizzes and module content.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.Interaction.cs`</sub>

Self-tests: Verbs, stage building, layouts, physics and grabbing.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.NPC.cs`</sub>

Self-tests: Pharmee, the gatekeeper flow, the examiner, voice, audio and cutscenes.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.Progression.cs`</sub>

Self-tests: Save data, campaign flow, settings, demo mode and result export.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.Scene.cs`</sub>

Self-tests: Surface, effect-material and visual-sweep checks on the lab scene.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.UI.cs`</sub>

Self-tests: HUD, fades, menus, the watch panel and text safety.

### `PharmaSelfTests` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmaSelfTests.cs`</sub>

Re-runnable regression suite for the PharmaSynth engine. Run via menu: Tools ▸ PharmaSynth ▸ Run Self-Tests. One permanent, one-click check. (An Editor-menu suite rather than an NUnit asmdef, to avoid restructuring the runtime assembly; a formal EditMode asmdef migration can layer on later.) This file holds the runner and shared helpers; each area's suites live in PharmaSelfTests.<Area>.cs. A new suite needs a call in Run().

```csharp
static void Run()
```

### `PharmeeGestureSim` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PharmeeGestureSim.cs`</sub>

Proves Pharmee's animation set actually MOVES HIM, in edit mode. The suite pins the pure curves (`PharmeeGestureSuite`), but a correct curve reaching a transform that is not bound produces exactly nothing while every assertion stays green. That is the failure this menu exists to catch, and it is the same reason `Simulate Tutorial Guidance` exists rather than another pin: it has to drive real scene objects, and the suite is kept side-effect-free. For each gesture it applies the pose at its peak and measures the ACTUAL degrees and millimetres the scene transforms moved, then restores them. Tools > PharmaSynth > Simulate Pharmee Gestures (edit mode, restores what it touches).

```csharp
static void Run()
```

### `PhysicsAudit` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PhysicsAudit.cs`</sub>

Physics-attributes / resting-pose audit. Tools ▸ PharmaSynth ▸ Physics Audit (Report)   — non-destructive scan of the scene apparatus + SceneAssetLibrary prefabs: colliders present/degenerate, Rigidbody settings, profile coverage. Writes Temp/physics-audit.md. Tools ▸ PharmaSynth ▸ Physics Audit (Drop Test) — drops every library prefab onto a plane 50 m above the lab for 3 simulated seconds (script-mode simulation, all other dynamic rigidbodies frozen for the sweep) and checks it neither tunnels, rolls away, nor balances implausibly.

```csharp
static void Report()
static void FixSceneItems()
static Rigidbody WireSceneItem(GameObject go, string prefabName, ExperimentRunner runner)
static void DropTest()
static string PrefabNameFor(GameObject go)
```

### `PlayFromMenuBootstrap` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlayFromMenuBootstrap.cs`</sub>

Every editor Play starts in the CUBE SPAWN ROOM ("ensure that every start or play, I start at the cuberoom not at the lab right away") — exactly like the built game (MainMenu is build index 0). Uses the editor's play-mode start scene, so whatever scene is OPEN keeps its edits; Play simply boots MainMenu. Toggle off via the menu when a direct lab test is needed (e.g. iterating on stage layout with the DevExperimentDriver).

### `PlayModeSwitch` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlayModeSwitch.cs`</sub>

One-click switch between the two ways to run PharmaSynth in the editor, so you never have to hunt the Hierarchy or Project Settings: • PC Dev Mode — OpenXR auto-init OFF + the "XR Device Simulator" GameObject ENABLED, so W/A/S/D + mouse move the view on the PC with no headset. • Headset Mode — OpenXR auto-init ON + the simulator DISABLED, so a Quest on Quest Link / Air Link drives the view. Each mode fixes BOTH halves (the XR init setting AND the scene's simulator object) and saves. Flipping only the init setting leaves the simulator off, so nothing drives the camera. Menu shows a checkmark for the active mode. Android is left untouched.

### `PlayerAvatarBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlayerAvatarBuilder.cs`</sub>

Builds the mirror-only first-person avatar. Expects a rigged humanoid prefab (Tripo image→3D + Tripo Rigging v1, casual clothes, T-pose) in Art/Generated/Models with "player"/"avatar" in its name — or select it in the Project. Places it under the XR rig, puts it on the PlayerAvatar layer (culled by the main camera, shown by the mirror), and wires an Animation-Rigging IK setup: two-bone IK on each arm (hands→controllers) + a head rotation constraint (head→HMD), driven by PlayerAvatarRig. Raw-bone IK, so NO Humanoid retarget / T-pose calibration. Tools ▸ PharmaSynth ▸ Build Player Avatar (run in SampleScene, edit mode).

```csharp
static void Build()
```

### `PlaytestAutopilot` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

Plays the game in PLAY MODE, unattended, and reports what broke. ⭐ Why this exists on top of Simulate Everything: that battery runs in EDIT mode, where Update never ticks, coroutines never run, physics never steps, XRI never selects anything and no audio plays. Every bug that only exists in MOTION is invisible to it: items vanishing, dialogue stomping mid-typing, the holo panel not scrolling, quiz buttons not clickable, an error spammed every frame. ⭐ No headset needed: PC Dev Mode leaves OpenXR auto-init OFF on Standalone and the scene's XR Device Simulator drives the rig. ⛔ Deliberately an EDITOR-assembly script ticked by EditorApplication.update (which keeps running during play) rather than a runtime MonoBehaviour. That way it can reuse the editor-only simulators, and nothing it does can ever leak into a player build.

```csharp
static void Launch()
static void LaunchTutorial()
static void Launch(bool tutorial)
static void LaunchVisual()
static void Launch(string mode)
```

### `Pending` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

### `InFlight` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/PlaytestAutopilot.cs`</sub>

A step that has been PERFORMED and is now finishing on real frames.

```csharp
static bool IsCompletionNoise(string bug)
static int InvalidPixelCount(Color[] px)
```

### `QuizNavButtonsBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/QuizNavButtonsBuilder.cs`</sub>

Add "< Back" / "Next >" buttons to the quiz tablet so players can review their answers before submitting. Clones the existing Submit button so styling matches, wires them to PostLabController.PreviousQuestion/NextQuestion, assigns the controller's prevButton/nextButton refs (which grey out at the ends), and makes sure the quiz canvas is XR-ray clickable. Idempotent.

```csharp
static void Build()
static float WidthFor(float buttonWidth, float parentWidth)
static float StepFor(float buttonWidth, float parentWidth, float submitOffsetX)
```

### `RawReagentForge` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RawReagentForge.cs`</sub>

Generates the ChemicalData assets for every RawReagentCatalog row that the game doesn't already know (matched by normalised chemicalName), stamps the HazardousMix flags from the shared HazardFlags rules, and registers everything in the SceneAssetLibrary so layouts and the cabinet builder resolve them. Consumable rows (SmallBox/IceBucket) are physical props, not chemicals — no SO is made for them. Idempotent.

```csharp
static void Generate()
```

### `ReHomeSceneItems` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ReHomeSceneItems.cs`</sub>

Adopts every scene item's CURRENT transform as its DropRespawn home, so equipment relocated by hand keeps its new spot as its default spawn point. Without this, manually moved props teleport back to their previous serialized homes after ~25 s idle / a kill-Z fall / a reset. Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current) — run in SampleScene edit mode after ANY manual re-arrangement, then save the scene. ⛔ NEVER run the ALL-items version straight after a simulator. Every simulator calls `DropRespawn.ResetAllHome()`, which teleports all 120 items back to their BAKED homes, so "adopt current" then re-bakes the pose the reset just restored and writes it to disk, silently destroying whatever had been placed by hand. Nothing is logged and the result is self-consistent, just wrong. Safe order is REOPEN the scene (discarding), then re-home, then save. `Re-Home MOVED Items Only` below 

```csharp
static void AdoptMoved()
static void Adopt()
```

### `ReachabilityAudit` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ReachabilityAudit.cs`</sub>

Asks the one question the run simulator structurally cannot: a step can be mechanically perfect while the bottle it needs sits inside a closed cabinet or above head height. `SimulatedRun` reaches every object by reference, so it never notices; otherwise only a headset session would. Its input is already built and already verified: `TutorialTargets.Build()` resolves taskId → the objects each step is about, for all 9 modules. So the audit asks, of every object a step needs, "could a player standing in this room actually get to it?" Deliberately GEOMETRIC rather than a navmesh walk. The player has continuous locomotion over the whole lab floor, so "can I stand near it" is nearly always true; what goes wrong is height and enclosure, which two raycasts answer.

```csharp
const float HardHigh
const float HardLow
const float Clearance
```

### `Verdict` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ReachabilityAudit.cs`</sub>

```csharp
static Verdict HeightVerdict(float y)
static bool IsEnclosed(bool[] blockedPerDirection)
static bool[] ProbeDirections(GameObject go, Bounds b)
static void RunMenu()
static List<string> RunAll(StringBuilder log)
```

### `ReagentCabinetBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ReagentCabinetBuilder.cs`</sub>

Builds the raw-reagent storage (the manuscript's ~54 materials must exist in the lab): three open shelf units against the wall, stocked from RawReagentCatalog with nature-appropriate labware — reagent bottles, amber bottles for the light-sensitive, powder jars, dropper bottles, consumable boxes (litmus/matches/cotton/filter) and an ice bucket. Every bottle is grabbable, pourable, spill-graded and hover-explained. Chemicals already displayed on the legacy ReagentShelf are skipped. Re-runnable: the ReagentCabinets root is cleared and rebuilt deterministically.

```csharp
static string PrefabFor(RawReagentCatalog.LabwareKind kind)
static void Build()
```

### `RemoveVrInappropriateApparatus` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RemoveVrInappropriateApparatus.cs`</sub>

Deletes the SCENE INSTANCES of apparatus that the manuscript lists but that carry no meaningful VR interaction: pure bench scaffolding and passive instruments the game already abstracts. The prefabs stay in their folders. ⛔ DELIBERATE, DOCUMENTED EXCEPTION to the "ALL tools always present" client rule. These six are NOT a decluttering of usable tools — each is either a support rig the zone-free heat model made unnecessary or an instrument whose reading the game shows on-screen. The PREFAB ASSETS stay in the project, so a future experiment that genuinely needs one can re-place it. Do NOT let "Restore All Bench Items" or an all-tools audit re-add them — they are removed on purpose. Justifications live in experiments-reference.md.

```csharp
static void Run()
```

### `RestoreBenchItems` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RestoreBenchItems.cs`</sub>

Re-activate every bench item that was hidden (client rule: ALL tools and reagents are present across ALL experiments — nothing is ever hidden or removed per-experiment). Undoes any accidental deactivation. Idempotent.

```csharp
static void Run()
```

### `RevealExperimentStage` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealExperimentStage.cs`</sub>

object it spawned, grouped by WHO spawned it, so the stage can be reviewed and pruned by hand. ⛔ DELETES NOTHING. Same intent as RevealMethaneStage: the stage normally only exists at runtime, so there is no way to see what an experiment litters the bench with until you are inside VR. This makes it visible and named. The grouping matters because the spawn sources are independent, and only ONE of them is the layout's fault: • LAYOUT VESSELS   — authored per experiment (Exp 2's 20 tubes duplicate the bench) • SpawnRackKit     — a rack + 6 tubes, EVERY module, predates the permanent bench • SpawnSpares      — 2 spare beakers + a flask, EVERY module • StageConsumables — matches + a "Striker" cube at Heat experiments. The cube is redundant: the matchbox itself is the striker. Re-run after a rebuild; the builder clears the stage each time, so it is idempotent.

```csharp
static void RevealCompounding()
static void RevealEthyl()
static void RevealBenzoic()
static void RevealAcetanilide()
static void RevealAcetone()
static void RevealChloroform()
static void RevealBenzamide()
static void RevealWine()
```

### `RevealMethaneAndWaypoint` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealMethaneAndWaypoint.cs`</sub>

Reveal the Methane set + the waypoint marker in the editor so they can be hand-aligned, and permanently strip the waypoint's yellow ground glow (keep only the arrow). The Methane STATIONS carry the step-detection zones AND are what the waypoint arrow follows, so moving them with the props is how both detection and the arrow get aimed correctly.

```csharp
static void Run()
```

### `RevealMethaneStage` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RevealMethaneStage.cs`</sub>

The Methane tutorial stage is authored inactive (m_IsActive:0) so it stays hidden in the editor. To review / delete it by hand, so this switches it (and any other hidden methane roots) ON in edit mode and lists what became visible. Runtime is unaffected — ExperimentSceneBuilder still SetActive(moduleId==methane) each build. ⚠ Deleting these breaks Experiment 1 until Methane is rewired to build on the workspace (the splint-pop rig especially is wired to the stage's tube).

```csharp
static void Run()
```

### `ReviewCornerBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ReviewCornerBuilder.cs`</sub>

Wires the post-experiment review corner: a ReviewCornerSpawn marker in front of the PostLabTablet (biased toward Dr. Jimenez's spot) that the gatekeeper fade-teleports the player to for the quiz-review flow, plus the gatekeeper's postLab/examiner refs and the quiz's autoOpen=false (the gate opens the quiz after Jimenez's briefing). Idempotent.

```csharp
static void Build()
```

### `RobotVoiceBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RobotVoiceBuilder.cs`</sub>

Wires Pharmee's robot voice colouring onto HIS narration channel only Dr. Jimenez is a human examiner and must stay untouched — the whole point of the two-NPC contrast is that one is a machine and one is not. Ring modulation does the robot work (RobotVoiceFx); Unity's stock filters do the colouring — a band limit so he sounds like he is coming through a speaker grille, and a short chorus for the metallic doubling. All of it is live-tunable in the Inspector during Play, so the character can be dialled in by ear without regenerating a single clip.

```csharp
static void Apply()
static void Remove()
```

### `RoundedCornersBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/RoundedCornersBuilder.cs`</sub>

Tools ▸ PharmaSynth ▸ Round UI Corners: writes the shared rounded and ring sprites (once) and bakes it into every panel and button of SampleScene and MainMenu, then saves both. Idempotent. Run it on a scene freshly loaded from disk, never straight after a suite or simulator run (those drain the bench, and this saves).

```csharp
const string SpriteAsset
const string RingAsset
static void Run()
```

### `SceneFind` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SceneFind.cs`</sub>

Find scene objects by exact name from editor builders. Two lookups because they answer different questions: - `AnyByName`: any loaded object, active or not. The order is whatever Unity returns, so use it only for names that are unique in the scene. - `InActiveScene`: the active scene only, roots first then depth-first. Deterministic, so the first of several same-named objects is always the same one.

```csharp
static GameObject AnyByName(string name)
static Transform AnyTransform(string name)
static GameObject InActiveScene(string name)
static Transform Deep(Transform t, string name)
```

### `SceneRepairs` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SceneRepairs.cs`</sub>

The SCENE half of a set of fixes whose code half lives in the runtime scripts. One idempotent menu per symptom so a single broken area can be re-run alone; "Fix Everything" runs them in order. These are separate from the existing builders because none of them own these objects: three apparatus sit in the scene as RAW model prefabs with no wiring (no collider, no rigidbody, no XRGrabInteractable, so ungrabbable), the wash bottle has no contents, and the balance is pure set dressing.

```csharp
const string MenuRoot
static readonly string[] UngrabbableReport
static void FixEverything()
static readonly string[] FixedRacks
static void FixRacksAnchored()
static readonly string[] YieldWidgets
static void FixYieldGone()
static void FixGrabbable()
static void FixWashBottle()
static void FixFlorenceFlask()
static void FixFunnels()
static void FixBalance()
```

### `ScoopSoundWiring` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ScoopSoundWiring.cs`</sub>

Wire the generated solid-material SFX into the SoundBank so scooping powder doesn't sound like liquid. The scoop verb calls the "scoop" and "powder-pour" keys via AudioService.TryPlayFirstAt, which deliberately has NO liquid fallback, so these clips are what make it audible. Clips generated with elevenlabs-sound-effects-v2 into Audio/Generated/. Idempotent: updates the entries in place if they already exist.

```csharp
static void Run()
```

### `SelfTestAutoRun` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SelfTestAutoRun.cs`</sub>

One-shot self-test runner for when the MCP bridge is down: if Temp/selftest-autorun-request.txt exists after a domain reload, run the suite once, write the console result to Temp/selftest-autorun-result.txt, and consume the request. Harmless when no request file is present.

### `ShelfPourBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ShelfPourBuilder.cs`</sub>

Wires the hand-placed reagent bottles for visible pouring (LiquidPourer is otherwise only on runtime-spawned props, so tipping a shelf bottle shows nothing). Sweeps every LiquidPhysics under the ReagentShelf and ReagentCabinets roots through ShelfPourWiring.WireBottle, and ensures the persisted particle material asset exists so device builds don't strip the URP particle shader. Idempotent; re-running reports 0 additions.

```csharp
static void Wire()
static Material EnsureFxMaterial()
```

### `ShowcaseReelBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/ShowcaseReelBuilder.cs`</sub>

Tools ▸ PharmaSynth ▸ Cutscenes ▸ Build Showcase Reels: wires the arrival reel into both scenes and the campaign-complete ending into the lab. In each scene it makes sure the CutsceneMarkers root has a CinemaView and a ShowcaseDirector, creates the camera markers that are missing (existing ones keep their hand-tuned poses), creates the reel assets if missing, adds the demo HUD's Play Ending button, and saves. "Reset Showcase Reels" rewrites markers and reels to these defaults. Run it on a scene freshly loaded from disk: it saves both scenes.

```csharp
const string LabScene
const string CubeScene
const string Folder
const string CubeReelPath
const string LabReelPath
const string EndingReelPath
static readonly (string name, Vector3 pos, Vector3 look)[] CubeMarkers
static readonly (string name, Vector3 pos, Vector3 look)[] LabMarkers
static List<CutsceneData.Beat> CubeBeats()
static List<CutsceneData.Beat> LabBeats()
static readonly (string name, Vector3 pos, Vector3 look)[] EndingMarkers
static List<CutsceneData.Beat> EndingBeats()
static void Build()
static void Reset()
```

### `SimulateEverything` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulateEverything.cs`</sub>

ONE command that plays the whole game and answers one question: is every experiment actually doable right now? It adds no simulation of its own. `SimulatedRun.Run` and `SimulatedCampaign.Run` already return structured results; this is the single entry point and the single verdict over them and the two tutorial audits. Everything lands in Logs/simulate-everything.txt, worst module FIRST: a report you have to scroll to find the failure in is a report that gets skimmed.

```csharp
static void RunMenu()
```

### `Row` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulateEverything.cs`</sub>

### `SimulatedCampaign` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedCampaign.cs`</sub>

• the module picked from its period through the two-step picker, with the real ProgressionFlow.IsUnlocked gating each pick • the honest pour-through of the experiment (SimulatedRun) • the REAL PostLabController quiz — Open, answer, SubmitAndFinish → Finish • the REAL ExperimentGrader result + the floored grade-screen text • the REAL cutscene outro selection + its subtitle beats • the REAL ProgressionService record + ProgressionFlow unlock + UnlockDiff announcement, then the pick of the NEXT experiment — looping to the campaign-complete celebration after Exp 9. The transcript (Logs/simcampaign.txt) is written as the PLAYER EXPERIENCE: every line Pharmee/Jimenez speaks, every panel prompt, every grade/outro beat — so it can be read as a clueless player and critiqued. SAFETY: progression is recorded to a TEMP save file (Application. temporaryCachePath), never the real pharmasynth_progress.j

### `Result` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedCampaign.cs`</sub>

```csharp
static void RunMenu()
static Result Run(StringBuilder log)
```

### `SimulatedMisplay` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedMisplay.cs`</sub>

Simulates the player who gets it WRONG. Every other simulator plays a flawless run, which answers "does correct play work?". It does not answer the question that actually decides whether nine experiments are doable by a student: **after a mistake, can they still finish?** A contaminated vessel, an out-of-order attempt or an exhausted bottle that quietly makes a run unfinishable looks identical to a clean sim — the perfect path never touches it. Each probe asserts two things, and the second is the important one: the mistake is reported AND the run remains completable afterwards.

```csharp
static void RunMenu()
static List<string> RunAll(StringBuilder log)
```

### `SimulatedRun` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

• a required chemical with NO source on the bench, or too little of it • pours that land invisibly small in their vessel • correct play being flagged as a mistake (mis-authored bindings) HOW it simulates: the PLAYER PATH, not the plumbing. builder.Build() wires the real scene, runner.StartExperiment() opens the real graph, and every reagent is then TRANSFERRED the way a hand would: drawn out of the actual bench source bottle (PourOut) and landed through LiquidPhysics.AddLiquid in VERB-CONTRACT increments (1 ml a squeeze, 0.1 g a spatula dip, 0.5 ml tilt-pour ticks WITH human overshoot). Completion may only arrive through the real event chain: AddLiquid → LiquidAdded → binding → CompleteTask, rack-group polls, ZoneSimStation sims. Calling binding.HandleReagent directly bypasses the binding's subscription to its vessel's events, so a binding that never subscribed (a hard-stuck player) woul

### `Result` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

```csharp
static void SimMethane()
static void SimCompounding()
static void SimEthyl()
static void SimBenzoic()
static void SimAcetanilide()
static void SimAcetone()
static void SimChloroform()
static void SimBenzamide()
static void SimWine()
static Result Run(string moduleId, StringBuilder log)
static bool NeverForce
static LiquidPhysics LastVessel
static string LastKind
static float LastTargetC
```

### `Session` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SimulatedRun.cs`</sub>

One honest run's working state: the mechanisms the build produced, the reaction watchers, the supply/visibility tallies. Run() drives it to the end in edit mode; the VISUAL autopilot performs one step per frame in Play mode and photographs each.

### `SolidJarFitter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SolidJarFitter.cs`</sub>

Gives every solid reagent jar on the shelf its powder mound. `Build Reagent Cabinets` stocks a PowderJar row from the `Beaker_100mL_WithLiquid` prefab, which carries a `Liquid` surface and nothing else, so without a mound a SOLID is drawn as a column of liquid (`LiquidPhysics.DrawsLiquidColumn` has no mound to show instead). The scoopula is unaffected either way: `ScoopMath.CanPickUp` keys on the chemical's `state`. The mound itself is `ExperimentSceneBuilder.EnsurePowderVisual`, the same helper the stage builder and the scoop already use; this only points it at the shelf. Idempotent: a jar that already has its mound is left alone. Called by `Build Reagent Cabinets` at the end of a rebuild, and available on its own for the saved scene.

```csharp
static bool IsHeap(ChemicalData chem)
static void Run()
static int FitAll(out string names)
```

### `SpawnHeightWiring` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SpawnHeightWiring.cs`</sub>

Wires the fixed eye height onto the open scene's XR rig (fixed, not relative to the player's real height). Run once per scene (MainMenu + SampleScene). Idempotent. Tune the constants below and re-run to adjust.

```csharp
const float EyeHeight
static void Wire()
```

### `SpawnVfxBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/SpawnVfxBuilder.cs`</sub>

Builds the cyan "materialize" spawn burst: a one-shot column of cyan particles that rises from the player's feet like smoke, played on every teleport / reset / spawn. Creates a shared soft-dot texture + additive material, then drops a configured `SpawnVFX` object (SpawnBurstFX + ParticleSystem) into the ACTIVE scene. Re-runnable. Run it once in MainMenu and once in SampleScene. Tools ▸ PharmaSynth ▸ Build Spawn VFX.

```csharp
static void Build()
```

### `StoolTools` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/StoolTools.cs`</sub>

Edit-mode helper ("let me select + reposition the stools in the editor, not in Play mode"). The stools sit tucked under the tables, so clicking them in a crowded Scene view is fiddly. This selects all of them in one go — then move them with the transform gizmo and run Re-Home Scene Items to make it stick. Tools ▸ PharmaSynth ▸ Select Stools (edit mode).

```csharp
static void SelectStools()
```

### `TextThemeBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/TextThemeBuilder.cs`</sub>

Tools ▸ PharmaSynth ▸ Apply Text Theme: creates the theme (once) and the game font's TMP assets (Inter, regular + bold, dynamic, LiberationSans as glyph fallback), makes the theme font TextMesh Pro's default so runtime-created text uses it, and bakes it into every text of SampleScene and MainMenu, then saves both. Idempotent. A theme still on the stock LiberationSans is moved to Inter; any other font set in the theme is kept, so to change the game's font, set the theme's font and run this again. Run it on a scene freshly loaded from disk. The balance's LCD readout (legacy UI.Text in a digital face) is an instrument display, not game text, and is left alone.

```csharp
const string ThemeAsset
const string StockFont
const string GameFont
const string GameFontBold
const string Prebaked
static void Run()
static TextTheme EnsureTheme()
```

### `TubeRackSlotBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/TubeRackSlotBuilder.cs`</sub>

⛔ STORAGE RACKS GET NO SLOTS. The tubes were duplicated TOGETHER WITH their kit holders, so every copy is already seated correctly relative to its rack. Moving them onto bounds-GUESSED slots would destroy that placement. Never re-derive rack positions the scene already has right. Only the workspace holders need anchors, because they are the only racks that start EMPTY (the player drags tubes in mid-experiment), so there is no seated tube to copy a position from. Workflow: 1. Tools ▸ PharmaSynth ▸ Name Tubes + Build Rack Slots   (this) 2. Drag each green Slot_* gizmo until its ghost tube sits in the holder's hole 3. Tools ▸ PharmaSynth ▸ Re-Home Scene Items (Adopt Current)  (bakes homes — a broken tube respawns at its baked home via BreakableGlassware→DropRespawn)

```csharp
static void Build()
```

### `TutorialModeBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/TutorialModeBuilder.cs`</sub>

Tutorial Mode scene wiring. Mirrors DemoModeBuilder's shape so the two special modes are built the same way: • Build Tutorial Menu Button — MainMenu scene: clones the Laboratory button into a "Tutorial" button wired to MainMenuController.OnTutorialLaboratory. Unlike the Demo button this one is ALWAYS visible — practice mode is a shipped feature, not a config-gated demo affordance. Idempotent: re-running re-labels and re-wires the existing button in place.

```csharp
static void BuildMenuButton()
static void FixMenuLayout()
static void BuildRoomFx()
static void BuildSceneWiring()
```

### `UprightPourables` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/UprightPourables.cs`</sub>

because adoption bakes whatever pose it finds, tipped or not. ⛔ An item whose visible mesh is not on its own PIVOT cannot be placed by anyone and is held nowhere near the player's hand. Tripo/USD imports can carry their geometry 0.7–1.9 m from their origin (both distilling flasks, the ice bucket and the matchstick). Dragging the object in the Scene view moves the pivot; the mesh keeps its offset and hangs in the air wherever it is put. The runtime also reads `transform.position` (`Matchstick` measures its strike distance from it, `VaporCollectController` its receiver radius, `DropRespawn` its home), so a grabbed match would be "held" 1.9 m from the hand and light no burner. The simulators cannot see this: they drive the APIs directly. Straightening or re-pivoting the transform alone never sticks: `DropRespawn.ResetAllHome` puts the baked pose back at the start of every run, so the HOME i

```csharp
const float Tolerance
const float MeshOffsetLimit
static void RunLegacy()
static void Run()
```

### `VerbDataApplier` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VerbDataApplier.cs`</sub>

One-shot, idempotent data pass for the tool verbs: re-points the layouts whose tasks are TOOL verbs (weigh/stir/grind) and wires the scene-side pieces (Methane's hand-built mortar, the matches-box striker). • Acetone: `weigh-acetates` zone-touch → Weigh (scoopula on the pan). • Benzamide: `stand` ("Stir & stand") zone-touch → Stir (glass rod). • Scene: Methane's Eq_Motar/Eq_Pestle get a GrindController completing `prepare-mixture` (dual-path with the legacy zone-touch), and the matches dispenser box becomes a MatchStrikerSurface.

```csharp
static void Apply()
```

### `VisualSweep` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VisualSweep.cs`</sub>

The photographer behind the VISUAL autopilot: after each honestly-performed step it frames the vessel the step happened in, renders a close-up, reads the numbers behind the picture (fill, colour, precipitate, boil, temperature, live particles, popups) and judges them against what the fired ReactionRule / the verb promised. One manifest row per step feeds Tools/visual-sheet.py (a captioned contact sheet per module). ⭐ Why numbers as well as pixels: a screenshot cannot say "the flask is at 3% fill" or "no particle system is alive within 60 cm", and a reviewer skimming seventy thumbnails misses both. Every verdict names the exact quantity it judged, so a FAIL is actionable on its own.

```csharp
const string Dir
const string Manifest
const string Report
const float NearM
```

### `Expect` <sub>enum</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VisualSweep.cs`</sub>

### `Obs` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VisualSweep.cs`</sub>

What the probe saw. Plain data so the judge is pure and pinnable.

### `Verdict` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VisualSweep.cs`</sub>

```csharp
static Expect ExpectFor(string kind, ReactionRule rule)
static float ColourDistance(Color a, Color b)
const float FillFloor
static Verdict Judge(Expect e, Obs o, ReactionRule rule, float targetC, float boilingPointC)
static bool FlameLatched
static void NoteFlame(bool litNow)
static void ClearFlameLatch()
static bool AnyFlameLit()
static Obs Probe(LiquidPhysics lp)
static string LastFrame
static string Snap(GameObject target, string file, bool fastForwardFx)
static int Ok, Fails, Skips, Photographed
static void BeginRun()
static void BeginStep(int moduleIndex, string module, int step, string taskId)
```

### `VoiceAudioBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceAudioBuilder.cs`</sub>

Makes the generated voice-over AUDIBLE and well-behaved. Three separate concerns, one pass: 1. Dr. Jimenez's narration channel needs a narratorAudioSource. SayRoutine only plays a clip when one exists, so without it every one of his 37 lines falls through to the placeholder blips. 2. Fully 3D voices fade to nothing a couple of metres away. The client wants them heard across the room but LOUDER up close, which is a partial spatial blend: the 2D share guarantees a floor everywhere, the 3D share still swells as you approach. 3. The music ducks under dialogue.

```csharp
const float VoiceSpatialBlend
const float VoiceMinDistance
const float VoiceMaxDistance
const float MusicDuckTo
static AudioSource EnsureVoiceChannel(NPCNarrationController n, out bool added)
static void Apply()
```

### `VoiceImportTool` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceImportTool.cs`</sub>

Imports the generated voice clips and wires the bank into the scene: 1. Quest-friendly import settings on Audio/Voice/** (mono, Vorbis). 2. Rebuilds VoiceBank.asset from Audio/Voice/<Speaker>/<id>.mp3|wav. 3. Points every NPCNarrationController in the open scene at the bank — controllers under Dr. Jimenez speak as Jimenez, everything else as Pharmee. Missing clips keep today's blip+typewriter. Idempotent.

```csharp
const string BankPath
static void ImportAndWire()
```

### `VoiceManifestExporter` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceManifestExporter.cs`</sub>

Exports the full voice-over manifest (NPCs speak): every code-authored line (VoiceCorpus) plus every cutscene beat, one row per unique (speaker, text) with its stable id. Tools/voice/generate-voice.ps1 consumes the manifest; changed lines re-key and regenerate individually.

### `ManifestLine` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceManifestExporter.cs`</sub>

```csharp
static int SpeakerPriority(string speaker)
```

### `Manifest` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoiceManifestExporter.cs`</sub>

```csharp
static void Export()
```

### `VoicePolish` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VoicePolish.cs`</sub>

Pre-recording copy pass over the CUTSCENE beats. Every beat is a SUBTITLE **and** a text-to-speech script, so a line can be perfectly good on screen and wrong in the ear, or describe apparatus that is not in the scene. Run this before spending voice credits: changing a line changes its VoiceLineId, so the manifest must be re-exported afterwards anyway. Edits the assets through SerializedObject rather than the YAML, because these subtitles contain ": " (e.g. "Welcome back! Today: Acetone.") which a hand-written scalar silently truncates.

```csharp
static void Polish()
static void SyncGateLines()
static string ApplyRules(string subtitle)
static bool IsRecordingSafe(string line)
```

### `VrAffordanceBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VrAffordanceBuilder.cs`</sub>

Wires the VR affordance batch into the open scene: 1. HAPTICS — every hand interactor (NearFar + Poke) gets a HapticImpulsePlayer + SimpleHapticFeedback so grabbing, socket-snapping and poking UI buzz. 2. HOVER HIGHLIGHT — every grabbable gets a HoverHighlight so it brightens + pops when a hand/ray hovers it (small real-scale tools are easy to find). 3. SOCKET GHOST — every station socket shows a translucent preview of the correct item snapped in place. (Runtime-spawned experiment props/sockets get 2 & 3 from ExperimentSceneBuilder; haptics is interactor-side so it covers everything automatically.) Tools ▸ PharmaSynth ▸ Wire VR Affordances (edit mode, idempotent).

```csharp
static void Build()
```

### `VrUiFixes` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/VrUiFixes.cs`</sub>

Two VR-UI scene fixes. • Quiz answers clickable: a world-space Canvas needs a TrackedDeviceGraphicRaycaster for the XR ray to hit it; a plain GraphicRaycaster is mouse-only, so a panel without one ignores the controller ray. • Dr. Jimenez's subtitles sit just above his head (at local y=2.15 they float up by the ceiling, unreadable). Both idempotent.

```csharp
static void FixRaycasters()
static void FixDialogueHeight()
```

### `WorkspaceKitsBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceKitsBuilder.cs`</sub>

apparatus that belong together, tightly but not overlapping, with generous duplicates of high-use glass). Kit composition follows the manuscript's Appendix C Equipment lists + the game's Methane heating rig: TOP row   — Heating Set A (full Bunsen rig), Heating Set B (compact Bunsen rig), Alcohol-Burner Set (spirit lamp + clay triangle + crucible: the crucible-work set — the manuscript names no burner, so Bunsen = sustained heat, alcohol lamp = crucible). LOWER row — 4 test-tube rack kits (regular tubes / amber HARD-GLASS tubes / vials / empty drying rack) + brush & wash bottle, then the duplicate glassware and small tools. Existing loose apparatus under "EquipmentShelf" is ADOPTED into matching slots (moved + re-homed); anything missing spawns fresh from the SceneAssetLibrary under a "WorkspaceKits" root. Every placed item gets the full interaction treatment (PhysicsAudit.WireSceneItem +

### `KitSlot` <sub>struct</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceKitsBuilder.cs`</sub>

```csharp
static KitSlot[] Row0Plan()
static KitSlot[] Row1Plan()
static float[] SlotCenters(KitSlot[] slots, out float usedWidth)
static void Build()
```

### `WorkspaceLabelPurge` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceLabelPurge.cs`</sub>

Removes the stale Methane-tutorial text labels that float over the center workspace. They sit directly under WorldLabels (NOT under MethaneStage), so toggling the Methane stage never hides them, and they are orphaned at the x≈1.15 position. Pure scene leftovers: no script references them. Landmark labels (PPE locker, fume hood) and runtime DynLabel_* are kept. Idempotent + re-runnable.

```csharp
static void Purge()
```

### `WorkspaceShelfBuilder` <sub>class</sub>
<sub>`Assets/PharmaSynth/Scripts/Editor/WorkspaceShelfBuilder.cs`</sub>

Builds the equipment-shelf platforms on the center-table overhead gantry. One row sits on the rail tops; this also adds the SECOND, lower row at y≈1.20 (clean full-width tiles + slim side posts, so the lower row reads as built in, not floating). Idempotent + re-runnable. Geometry lives in the pure WorkspaceShelfMath; the apparatus kits go on via Build Workspace Kits.

```csharp
static void Build()
```
