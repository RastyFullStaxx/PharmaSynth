# Experiments Reference — the 11 PharmaSynth modules

**Generated 2026-07-12 (W5.11) from the live ScriptableObject data + manuscript Appendix C.** This is the canonical per-experiment reference: open it whenever a task touches an experiment's chemistry, steps, reagents, tests, quiz or stage layout. Regenerate the data body after content changes with the extraction script pattern (see `Docs/systems-reference.md` §Authoring) or update by hand and keep the suite's ContentSuite counts in sync.

**Authority:** the client manuscript (`Docs/Documentations/manuscript.pdf`, extract via `pdftotext -layout` → `manuscript.txt`; the Read tool cannot open PDFs on this machine). Appendix C (Experiments 1–9) is the lab manual. The storyboard is a reference to EXCEED — never a source for chemistry or labels. Evidence trail for every deviation: `Docs/manuscript-reconciliation.md`.

## Deviations & client flags (authoritative summary)

| Topic | Manuscript says | Game does | Status |
|---|---|---|---|
| Benzoic Acid route | Exp 4 "procedure" is a verbatim copy-paste of the ethanol fermentation (confirmed print defect); materials list names Benzaldehyde | Benzaldehyde + KMnO₄ oxidation | ✅ correct-by-intent; client rubber-stamp pending (§7 signoff) |
| Acetanilide acylating agent | Appendix C procedure: acetyl chloride (intro prose wrongly says anhydride) | Acetyl chloride | ✅ matches; optional safer-anhydride swap = client call |
| Benzamide nitrous test | Reagent header "sodium nitrate" (typo); procedure body: 10% sodium NITRITE | Sodium Nitrite | ✅ matches |
| Chemical Compounding battery | Multi-substrate ID lab: FeCl₃ enol, KMnO₄ rate-of-oxidation (3 butyl alcohols + control), Tollen's, ester formation ×2, aspirin hydrolysis | ~~Single-substrate ethanol (combustion/sodium/bromine/KMnO₄)~~ → **REBUILT to the manuscript 2026-07-15** (13-task graph, ILOs restored verbatim, quiz realigned) | ✅ **client chose "rebuild to manuscript"** — the old battery failed its own ILO ("differentiate tests for *different* compounds") and used sodium/bromine, which the manuscript never lists. Layout/bindings still to author. |
| Wine fruit | Grapes EXCLUDED (L3830-31) | Ferments **Mixed Fruit Juice** (renamed from Grape Juice, W5.9) | ✅ fixed |
| Chloroform oxidation test | Dichromate + conc H₂SO₄ (procedure + results sheet) | Added W5.9: `test-oxidation` + rule | ✅ fixed |
| **Methane** | Not in Appendix C — appears only as a molecular-weight worked example in Exp 1 (Stoichiometry) | Game-authored tutorial | ✅ **CLIENT-CONFIRMED 2026-07-16: methane REMAINS, as the tutorial-only experiment.** Being game-authored is NOT grounds to remove it — do not lump it with the dropped aspirin/caffeine modules. |
| **Aspirin / Caffeine** | Not in Appendix C (Aspirin named in intro prose only; Caffeine absent entirely) | ~~Game-authored modules~~ → **DROPPED 2026-07-16** | ✅ **Absent from the client's grouping → both removed; chain 11 → 9.** Aspirin survives as a **raw reagent** (Exp 2 §D hydrolyses it). |
| **Apparatus lists vs procedures** | Exp 3–8 repeat ONE identical boilerplate list; **Exp 2's list omits the water bath** it uses 3×; **no list mentions the bent/delivery tube** Exp 3 requires | Stage from the PROCEDURE, not the list | ⚠ list defects — see §Apparatus; harmless in-game (all tools are always present) but do NOT infer steps from the lists |
| Wine rubric | Bespoke tasting/presentation rubric (group activity) | Standard 6-category rubric | ✅ client-resolved 2026-07-09 |
| Yield | Data-sheet records yield | Record-only, never graded (±5 stepper on the quiz tablet) | ✅ client-resolved |

## How to read each section
- **Task graph** = the module's `graphTasks` in authored (play) order; `phase` drives the loop (ReagentPrep → Synthesis → ChemicalTests → DataSheet); prerequisites gate order (violation = WrongStep mistake); hints surface in the wrist checklist.
- **Stage layout** = what `ExperimentSceneBuilder` spawns on the center table from `Layout_*.asset`. Station sims: Heat/Crystallise/Filter/Collect run while the required prop occupies the zone; Stir/Grind/Weigh are tool verbs (W5.8); zone-touch completes on prop contact. Every stage ALSO auto-spawns: a test-tube RackKit (6 tubes), 2 spare beakers + 1 spare flask, and (Heat modules) 2 matches + a striker.
- **Vessel bindings** = pour ≥N ml of the named reagent into that vessel to complete the task (works held or on the table). `completesTask:false` = the pour is expected but the WEIGH station completes the task.
- **Reactions** = MasterReactionRegistry rules that can fire from this stage's chemical set; `expectedObservation` pops as floating text on reaction (W5.8 feedback layer).
- **Manuscript lists** are OCR-extracted verbatim; minor column-merge artifacts (e.g. "bath Rubber tubing" = "water bath" + "rubber tubing") are the PDF's two-column layout, kept as-is for fidelity. **These lists are the source for apparatus grouping/kits** (queued work, checklist §13a).

**Wine Making (Exp 9) materials note:** the manuscript frames it as a real-world group activity — 250–500 mL of a **non-grape** fruit juice, sugar, yeast, sealed fermentation vessel + airlock, ~1 week ferment, video documentation. Adapted for solo VR per client resolution (standard rubric); see reconciliation §4.

## Apparatus — what each tool is actually FOR

**Why this exists:** the manuscript lists apparatus in one block per experiment and then **almost never names a tool in the procedure**. It says *"place 1 ml of ethanol"* — never *with what*. So the lists alone don't tell you what a step needs. This table maps every listed tool to the step that actually uses it (inferred from the procedure verbs), so a session knows what to stage and bind.

### ⚠ The lists are UNRELIABLE — the PROCEDURE is the source of truth
1. **Exp 3–8 all share ONE identical boilerplate list** (the same 19 items, verbatim). It is generic, not tailored — Exp 5 does no distillation yet still lists a condenser + distilling flask. **Never infer a step from the list.**
2. **Exp 2's list omits the WATER BATH** — yet its procedure calls for one **three times** (`:2185` heat Tollen's 5 min, `:2192` warm the ester, `:2203` boil the aspirin). A genuine list defect, same class as the Exp 4 copy-paste.
3. **No list anywhere mentions the bent/delivery tube** — yet Exp 3's procedure requires it (`:2430` *"attach a bent tube, fitted to a stopper"*). Equipment used but never listed.
4. **Exp 9 (Wine) has no apparatus list at all** — it is a take-home group activity, not bench work.

### Tool → real use
| Apparatus | What the procedure uses it for (the manual rarely says so) | Experiments |
|---|---|---|
| **Pipette** | EVERY *"place N ml"* / *"add N drops"* of a **liquid**. Never named in a single procedure step. | 2–8 |
| **Porcelain spatula** | EVERY *"place N grams"* of a **solid** (0.5 g aspirin, 0.1 g salicylic acid, 4 g yeast) | 2–8 |
| **Test tube** | the vessel for every discrete test | 2–8 |
| **Test tube holder** | gripping a tube that has been in the water bath | 2–8 |
| **Test tube brush** | washing tubes between tests → feeds the **Sanitation** rubric criterion | 2–8 |
| **Beaker** | bulk liquid, the dilution water, receiving a pour (Exp 2 `C.I.d`: *"pouring the beaker containing 1 ml of distilled water"*) | 2–8 |
| **Erlenmeyer flask** | the larger reaction/fermentation vessel (Exp 3: *"prepare 100 ml of 12% brown sugar solution in a flask"*) | 2–8 |
| **Stirring rod** | mixing / shaking | 2–8 |
| **Water bath** | EVERY *"heat / warm / boil … using a water bath"* | **2** (×3, unlisted!), 3–8 |
| **Aspirator** | suction for the *"filter off"* steps | 2 (`D.I.c`), 3–8 |
| **Watch glass** | the **combustion test** (*"pour 1 ml of the distillate into a watch glass and apply a lighted matchstick"*); evaporation | 3–8 |
| **Thermometer** | the distillation cut-off temperature (ethanol ~78 °C, acetone 56 °C) | 3–8 |
| **Distilling flask** | the distillation pot | 3–8 |
| **Condenser** | condensing the distillate | 3–8 |
| **Cork / stopper** | stoppers the flask and **carries the bent/delivery tube** (Exp 3 `:2430`) | 3–8 |
| **Rubber tubing** | condenser water lines | 3–8 |
| **Iron stand · Iron clamp · S-clamp** | holds the distillation train / condenser | 3–8 |
| **Bent (delivery) tube** | **UNLISTED but required** — bubbles evolved CO₂ into limewater (Exp 3 `:2430-2434`), with a cotton swab capping the receiving tube | **3 only** |
| **Water** | distilled water — effectively a reagent, not a tool | 2–8 |

### Manuscript item → game reality (STATUS FLAGS — review as each module is built)

**Nothing here is removed.** Every tool stays on the bench (⛔ all-tools rule). This flags which canonical items currently do a **real job** vs which are **decorative** — so as we build each procedure we consciously either give them a job or accept them as set dressing.

| Legend | Meaning |
|---|---|
| ✅ **Functional** | a verb/mechanic actually uses it |
| 🔁 **Replaced / consolidated** | the prop was swapped or deleted; another prop or mechanic serves it |
| 📋 **Served by a feature** | prop stays, but its FUNCTION is now a game feature |
| ⬜ **Decorative (FLAGGED)** | present + grabbable, but no mechanic uses it **yet** — candidate for a job |

| Manuscript item | Game reality | Status |
|---|---|---|
| **Pipette** | The glass pipette prop was **deleted** (`RemovePipette`, 2026-07-15) as redundant with the pack's **MechanicalPipette** (modern micropipette) which remains. Its FUNCTION — *"place 1 ml"* — is served by the **pour mechanic** (`LiquidPourer` + `requiredMl` threshold bindings): you tip the vessel and pour. | 🔁 Replaced |
| **Thermometer** | Prop **kept** on the bench. Its FUNCTION — temperature monitoring — is served by **`ProcessReadout.BindHeat`**: "62 C -> 120 C" floats over the heated vessel, tinting cool-blue → hot-orange, plus the red-hot glow. The user can monitor without holding it. | 📋 Served by a feature (prop currently decorative) |
| **Wooden splint** | **Deleted** — appears NOWHERE in the manuscript, which uses a *"lighted matchstick"*. `Matchstick` runs the gas/combustion test. | 🔁 Replaced |
| **S-clamp · Iron clamp** | Consolidated into the game's **UtilityClamp** (3-prong, W5.12) alongside **RetortStand** + **IronRing**. | 🔁 Consolidated |
| **Iron stand** | **RetortStand** | 🔁 Renamed |
| **Water bath** | **WaterBath** prop + `StationSim.Heat` + `TemperatureSim` — every "heat/warm/boil using a water bath". | ✅ Functional |
| **Porcelain spatula** | **`ScoopController`** — dip-and-deposit, 2 g per scoop. | ✅ Functional |
| **Stirring rod** | **`StirController`** (OrbitMath circular motion). ⚠ needs a tip anchor — see below. | ✅ Functional |
| **Test tube brush** | **`BrushController` + `CleanableVessel`** — real swipe-by-swipe scrubbing; feeds the **Sanitation** rubric. | ✅ Functional |
| **Beaker · Erlenmeyer · Test tube · Distilling flask (as a vessel)** | `LiquidPhysics` vessels — pour targets with fill visuals. | ✅ Functional |
| **Aspirator** | Present + grabbable. Suction filtration is served by **`FiltrationController` / `StationSim.Filter`**; the aspirator prop itself has no verb. | ⬜ Decorative (FLAGGED) |
| **Condenser · Rubber tubing** | Present. **Distillation is modelled as Heat + Collect** (there is no distil sim), so nothing binds to them. | ⬜ Decorative (FLAGGED) |
| **Cork / rubber stopper** | Present. Setup steps complete by **proximity/contents**, so a stopper is never mechanically required. | ⬜ Decorative (FLAGGED) |
| **Test tube holder** | Present. Grabbing works directly and nothing is hot-to-touch, so it has no job. | ⬜ Decorative (FLAGGED) |
| **Watch glass** | Present. The manual's combustion test pours onto it — **could** bind as a receiving vessel when we build Exp 3. | ⬜ Decorative (FLAGGED — give it a job in Exp 3) |
| **Bent / delivery tube** | Present (×3). **Required by Exp 3** (CO₂ → limewater) but not yet bound to anything. | ⬜ Decorative (FLAGGED — job comes in Exp 3) |

### Placement anchors — how the user hand-tunes positions

**Why:** bounds-guessing cannot know an imported mesh's axis convention — we could not tell which end of a match is the head or which end of a spatula is the blade, and guessing wasted hours. So the code reads a **`PlacementAnchor`** child that the user **drags in the editor** to the exact spot. Menu **`Add Placement Anchors`** creates them at a best guess (orange gizmo); drag, then **`Lock My Layout`** to bake. Re-running never moves one you have already placed.

| Anchor | Lives on | Supplies | Purpose |
|---|---|---|---|
| `FlameAnchor` | Matchstick · BurnerController | position | exactly where the flame appears (match **head**, burner **mouth**) |
| `ScoopAnchor` | ScoopController (scoopula/spatula) | position | the **blade/bowl** — the pickup probe AND the carried heap ride it |
| `BowlAnchor` | GrindController (mortar) | position | where the ground powder + grinding dust appear |
| `PowderAnchor` | solid receivers (hard-glass tube) | position **+ SIZE** | drag it AND **scale it** — the scale is the powder's size at a full charge |

**⚠ The rule that bit twice:** only an anchor with **`previewsScale = true`** supplies SIZE. A position-only anchor has scale **1**, so sizing from it produces a **1-metre blob** (this caused both the giant gizmos and the giant mortar mound). The flag is the discriminator — suite-pinned.

**✅ LIQUIDS NEED NO ANCHOR** (confirmed with the user): liquid/gas fills auto-fit the vessel's **own interior** via `LocalMeshBounds` / `LongestAxis` / `BoreOf` — they follow the bore in the vessel's LOCAL frame, so they stay contained and aligned even when the vessel is tilted or held. Only **solids** (a mound's resting spot) and **point effects** (a flame's tip) need anchors, because those cannot be derived from bounds.

**Anchor candidates — attach as each procedure needs the tool**
| Tool | Anchor to add | Why |
|---|---|---|
| **Stirring rod** | **`StirAnchor`** (rod tip) | ⚠ **`StirController` tracks `_rod.position` — the transform ORIGIN.** This is the exact latent bug that made the grind silently never register (the origin can be the handle, never entering the vessel). **Exp 2 and Exp 3 both use the rod — fix this when we get there.** |
| **Test tube brush** | `BrushAnchor` (bristle end) | `BrushController` accumulates origin travel; less brittle (any motion counts) but the contact point is the bristles |
| **MechanicalPipette / dropper** | `TipAnchor` | only if we ever model drop-dispensing rather than pouring |
| **Crucible tongs** | `GripAnchor` | only if we model gripping |
| **Watch glass · funnel · any liquid vessel** | *(none)* | liquid auto-fits — see above |

### Per-STEP apparatus — where to find it
The manual gives one apparatus block per experiment and never names a tool in a step, so **each module's Task-graph table carries an `apparatus the step needs` column** — inferred from the procedure verbs. It is authored as each module is polished (the one-by-one method):
- ✅ **tutorial-methane** — mortar · pestle · scoopula · open beaker (acetate+soda lime) · hard-glass tube · gas collection tube · burner · matches
- ✅ **prelim-chemical-compounding** — full per-step column authored (2026-07-15); roll-up under its task graph
- ⬜ **prelim-ethyl-alcohol → final-winemaking** — author the column as each module is reached. **Read the PROCEDURE, never the apparatus list** (see the defects above).

**Verbatim per-experiment lists** are reproduced under each module's *Manuscript Equipment & Apparatus* line below (OCR artifacts kept for fidelity — "bath Rubber tubing" = *water bath* + *rubber tubing*, a two-column merge).

**Game-status note:** the **pipette was deliberately dropped** (2026-07-15) as redundant with the modern micropipette already in the pack — but note the manuscript lists a pipette for **every** experiment, so pouring stands in for it. The **wooden splint was deleted** (never in the manuscript — it uses a *"lighted matchstick"*; see `manuscript-reconciliation.md` §0).

---

---

## ⭐ CLIENT PERIOD GROUPING (2026-07-16)

The client's grouping maps **exactly** onto the manuscript's 8 bench labs:

| Period | Modules | Manuscript |
|---|---|---|
| **Tutorial** | methane | *game-authored — sits OUTSIDE the graded periods.* ✅ **Client-confirmed 2026-07-16: stays as the tutorial-only experiment.** |
| **Prelim** | chemical reactions · ethyl | Exp 2 · Exp 3 |
| **Midterm** | benzoic · acetanilide · acetone · chloroform | Exp 4 · 5 · 6 · 7 |
| **Finals** | benzamide · wine making | Exp 8 · Exp 9 |

**Manuscript Exp 1 = STOICHIOMETRY** — a pen-and-paper exercise (balance equations, molecular weight, dimensional analysis, % yield), NOT a bench lab (`:1899`, `:2055`). That is why "methane" appears in the manuscript only as a molecular-weight worked example (`:1995`) — the game's methane tutorial is entirely game-authored.

### ✅ RESOLVED — Aspirin + Caffeine DROPPED (user 2026-07-16)
Both were **game-authored** (Aspirin is named only in the manuscript's intro prose; Caffeine appears nowhere) and are absent from the client's grouping. **Both dropped — the chain is now 9** (tutorial + the 8 bench labs).

**Deleted:** the two module defs, layouts, quizzes, 8 cutscene assets, `AspirinSynthesis`, `Test_CaffeineMurexide` — plus their entries in `ExperimentLibrary` / `QuizBankLibrary` / `CutsceneLibrary` / `MasterReactionRegistry` and the SampleScene builder's layout list. Suite pins moved in the same change (catalog 9, period split 1/2/4/2, 36 cutscenes, 9 banks / 29 questions, 8 layouts, results 9 rows).

**⚠ Aspirin is now a RAW REAGENT, not an end product.** Exp 2 §D hydrolyses aspirin (`0.5 g` in a test tube) and tests unhydrolysed aspirin as the control — but Aspirin existed ONLY as a demo-only end product, so dropping the module naively would have **hidden the reagent Exp 2 needs and made it unplayable outside demo**. It is now in `RawReagentCatalog` (Powder, `GroupOrganics`, "Exp 2") and out of `DemoMode.IsEndProduct`; **end products 9 → 7**.

**Orphaned but deliberately KEPT** (the hard client rule: never remove bench reagents/apparatus): `Chem_Caffeine`, the `Murexide Reagent` catalog row (marked `"(unused)"`), `Label_Aspirin` / `Label_Caffeine`, `Art/UI/Icons/aspirin.png`. `Salicylic Acid` stays — Exp 2's ester test uses it (its `usedIn` dropped "Aspirin" → "Exp 2").

**The Appendix-C TOC does NOT contradict this grouping.** Its Term column is a **vertically-centred merged cell** (labels land beside Exp 2 / 5 / 8), which reads as 3/3/3 but fits 3/4/2 just as well — a 4-row span centres between rows, which is exactly the extraction artifact seen. The narrative chapter independently confirms the client's grouping. Don't "re-derive" a chloroform conflict from it.

---

## ⭐ POLISH STATUS — we are perfecting the experiments ONE BY ONE

**Method (user directive 2026-07-15):** take ONE module from "the data exists" to "actually playable end-to-end in VR", finish it, then move to the next. **Cross-check the module against the manuscript BEFORE building** — the tutorial burned hours on props the manuscript never mentions (`manuscript-reconciliation.md` §0: "splint" appears **nowhere**; the **bent/delivery tube is real but belongs to Exp 3 only**; Exp 9 is a group activity with no bench chemistry).

| Module | Period | Polish status |
|---|---|---|
| **tutorial-methane** | Tutorial | ✅ **DONE (2026-07-15)** — playable end-to-end: scoop → grind → load tube → heat → collect → match test → quiz → grade |
| **prelim-chemical-compounding** | Prelim | 🔨 **PLAYABLE, awaiting headset playtest (2026-07-16)** — rebuilt to manuscript Exp 2. ✅ 13-task graph + ILOs + quiz. ✅ VR design SETTLED + BUILT: **dropper = counted squeezes**, **spatula 0.1 g/dip**, KMnO₄→liquid 0.1%, **RackTaskGroup** (a step waits for EVERY tube), layout rebuilt (20 vessels / 4 racks), 8 reactions, 13 two-line hints. ⬜ Left: headset pass. (Two-step picker shipped 2026-07-16; the `StirController` tip-tracking fix landed in W5.20 and Exp 2 has no stir step anyway — verified 2026-07-29.) |
| **prelim-ethyl-alcohol** | Prelim | ✅ BUILT + simulated clean (8/8). Zone-free ferment→CO₂→limewater (`FermentationController`) + week time-skip + distillation + 3 warm tests. **The distill step DECANTS the fermented wash from the FlorenceFlask** (2026-07-18 — the old bench-Ethanol shorthand was unplayable: that bottle is HIDDEN during Exp 3 as its own end product). ⬜ Left: headset playtest; combustion could gain real match-ignition. |
| **midterm-benzoic-acid** | Midterm | ✅ **BUILT + simulated clean 2026-07-18 (9/9)** — oxidise (heat-gated) → funnel-filter → acidify (white crystals) → **ice-bath crystallise** (time-skip) → litmus/FeCl3/ester tests drawing from the purified flask. New reusable: `IceBathController`+`VesselChillTask`, `VesselLitmusTask`+mixture pH, vessel pour-out, temp-goal tags. ⬜ Left: headset playtest. |
| **midterm-acetanilide** | Midterm | ✅ **BUILT + simulated clean 2026-07-18 (8/8)** — the FUME-HOOD module: hood-sanctioned acylation (position-based check, finally wired) → heat-gated white plates → ice-water + chill crystallise → filter/wash/dry time-skip → hydrolysis boil + **two-tube bromination comparison** (rackGroup). ⬜ Left: headset playtest (hood carry feel). |
| **midterm-acetone** | Midterm | ✅ **BUILT + simulated clean 2026-07-18 (8/8)** — bench-balance WEIGH (live grams) → **NAKED-FLAME** dry distillation (150 °C glow, hard-glass) → **VAPOR collect** at 56 °C into the receiver → 4 tests (2 authored negatives, iodoform warm, bisulfite adduct in the ice). New reusable: `NakedFlameHeat`, `VaporCollectController`, `VesselWeighTask`+balance rig. ⬜ Left: headset playtest. |
| **midterm-chloroform** | Midterm | ✅ **BUILT + simulated clean 2026-07-18 (13/13)** — two water-bath distillations (heat task + vapor collect ×2, 65 °C), decant wash, CaCl2 dry, balance weigh, then the **lit-match non-flammability confirm** (`VesselFlameTask`, new reusable), the hood-sanctioned dichromate oxidation and the warm AgNO3 white-ppt test. Rule fixes: oxidation resultLiquid-null trap + cold-fire, AgNO3 missing precipitate + plain-vs-alcoholic AgNO3. The legacy builder fixture is now SYNTHETIC (no module stages stations any more). ⬜ Left: headset playtest. |
| **final-benzamide** | Final | ✅ **BUILT + simulated clean 2026-07-18 (10/10)** — ice-bath-controlled synthesis (rule fires COLD), the **zone-free glass-rod STIR** (`Vessel.stirTaskId`; tip-tracking FIXED — closest-point like the pestle, either bench rod, no anchor), filter/wash/dry time-skips, weigh, then the **BASE litmus read** (new: red litmus turns blue on the ammonia tube), the acid litmus read, and the instant nitrous effervescence. Fixes: 2 resultLiquid-null traps, boil gates 40→90, alkali paired the 10% NaOH bottle, **Sodium Nitrite added to the catalog/bench** (was missing entirely), pending rule cleared on fire. ⬜ Left: headset playtest. |
| **final-winemaking** | Final | ✅ **BUILT + simulated clean 2026-07-19 (7/7)** — the LAST module, and **all 9 now build + simulate clean**. Zone-free: pour the must (juice from the new `Raw_MixedFruitJuice` bottle + sugar + nutrient + yeast → new `WineFermentation` rule makes Wine) → glass-rod rouse → CO₂/limewater confirm + week time-skip → rack the clear wine off the lees through the funnel → clarity + tasting draws from the racked flask → label. Fixes: jar no longer start-fills with juice (unplayable shorthand), Wine is actually produced (was only ever juice). ⬜ Left: headset playtest. |

### The METHANE FLOW — the template every module follows
1. **Gate** — Pharmee → Campaign → pick module → don **all 3 PPE** (hard-gated: `GatekeeperModel.RequiresPPEToOpen`) → "I'm ready" → cross the threshold (timer starts).
2. **ReagentPrep** — scoop the solid from its open beaker into the mortar → grind with the pestle.
3. **Synthesis** — **load** the ground mix into the reaction vessel → **heat** it → **collect** the product.
4. **ChemicalTests** — confirm with the manuscript's own test (methane: lit **match** → pop; the manuscript always uses a "lighted matchstick", never a splint).
5. **DataSheet** — clock freezes → review corner → Jimenez briefs → quiz → grade → Retry / Complete Experiment.

### Reusable systems — REUSE THESE, don't rebuild
| Need | Reuse | Notes |
|---|---|---|
| Step text on the wrist panel | task `label` + `hint`; `ChecklistPager` renders the ACTIVE step's hint | **Write hints to match the MECHANIC and name the exact item** ("Grab the item labelled *Hard-glass tube*"). A label naming a prop the code never checks (the phantom "delivery tube & trough") cost hours of confusion. |
| Temperature monitoring | `ProcessReadout.BindHeat(name, TemperatureSim, targetC)` | Floats "62 C -> 120 C" over the vessel, tints cool-blue → hot-orange. **Every heating module reuses this.** It does NOT replace the thermometer — that apparatus stays on the bench. |
| Collection progress | `ProcessReadout.BindCollect(name, GasCollection)` | "Collecting 45%". |
| Hot-vessel glow | `MethaneApparatusRig.GlowFor(currentC, targetC)` | Emissive red ramp; saturates at the reaction temperature. |
| Solid handling | `ScoopController` (2 g/dip) + `GrindController` | Grinding requires the mortar to actually HOLD the reagent (`CanGrind`) — else you finish the step with powder still on the scoop. |
| Solid SFX | keys `scoop` / `powder-pour` via `AudioService.TryPlayFirstAt` | **Granular, never liquid** — deliberately NO liquid fallback (silence beats the wrong material). Clips in `Audio/Generated/`, wired by `Wire Scoop Sounds`. |
| Powder / gas fills | `ExperimentSceneBuilder.EnsurePowderVisual` + `LocalMeshBounds` / `LongestAxis` / `AxisAlign` / `BoreOf` | Fit contents in the vessel's **LOCAL** frame, sized as a **FRACTION** of local bounds. |
| Hand-tuned placement | `PlacementAnchor` children + menu `Add Placement Anchors` | `FlameAnchor` (match head / burner mouth), `ScoopAnchor` (blade), `BowlAnchor` (mortar), `PowderAnchor` (position **and size** — scale it). Only size-setting anchors set `previewsScale`. |
| Quiz → grade | `PostLabController` | **NEVER score-gated** (client rule). Back/Next review nav, picked answer highlighted; score shown plainly on the grade screen (`ExperimentResult.quizScore01`); Retry re-runs the experiment, **Complete Experiment** exits. |
| Full reset | `DropRespawn.ResetAllHome()` | Re-homes every item + restores contents, disassembles rigs, **extinguishes all burners/matches**, destroys used consumables so dispensers restock. |
| Methane-only props | `MethaneStageVisibility` | Gates ONLY the 4 methane staged props. **All general apparatus/reagents stay permanent** — see the ⛔ rule in CLAUDE.md. |
| **Play the module WITHOUT a headset** | `Tools ▸ PharmaSynth ▸ Simulate Run ▸ <module>` (`SimulatedRun.Run`) | **PLAYER-PATH honest**: every reagent is drawn from the real bench bottle and landed through `LiquidPhysics.AddLiquid` (verb-contract increments, 20% overshoot on bulk pours, snapshot/restore of all vessel contents); completion may only arrive through the real event chain (binding/rack/station). ⛔ NEVER "simulate" by calling `binding.HandleReagent` directly — the direct call reported CLEAN while a real player was hard-stuck (the binding had never subscribed to its vessel's events). Audits supplies + end-of-run fill visibility; transcript → `Logs/simrun-<module>.txt`. **Run BEFORE every headset pass**; the suite plays Exp 2 clean as a pin (`simrun:`). Cannot feel grabs/gestures/visual placement — headset checklist. Bug classes it has caught: no-completion-mechanism deadlock, phantom fume-hood mistakes, the unsubscribed-binding stuck bug, expected-mix scolding, ghost bindings, funnel-wastes-the-pour. |
| **Play the whole CAMPAIGN LOOP without a headset** | `Tools ▸ PharmaSynth ▸ Simulate Campaign (full loop Exp 2-9)` (`SimulatedCampaign.Run`) | Drives the flow AROUND every module, Exp 2→9 in one walk: the pure `GatekeeperModel` FSM (a `Fire` that returns false = an illegal transition, so a broken gate is CAUGHT not narrated), the two-step picker with the real `ProgressionFlow.IsUnlocked` gating each pick, the honest pour-through (`SimulatedRun`), the REAL `PostLabController` quiz (Open → Answer → SubmitAndFinish → `runner.Finish`), the real grader + floored grade-screen text, the real cutscene outro + its subtitle beats, then record → `UnlockDiff` announcement → next pick, looping to the campaign-complete celebration. Transcript → `Logs/simcampaign.txt`, written as the PLAYER EXPERIENCE (every line Pharmee/Jimenez speaks, every picker row, every grade/outro beat) so it can be read cold and critiqued. **Progression goes to a TEMP save** (`Application.temporaryCachePath`), never the real `pharmasynth_progress.json`. ⚠ It does NOT advance the clock (elapsed 0 → TimeManagement always full credit), so par-times are not exercised here. Bug class it caught: **the whole campaign was unwinnable** (see the beatability gotcha below). |
| Closing beat with no physical verb | `ExperimentTask.autoCompleteWhenOthersDone` (wrap-up flag) | "Record your observations"-type steps auto-complete via `Graph.Tick()` once every other task is done. Exp 2's `record-observations` uses it. |
| **Heat-gated reactions** (2026-07-17) | `ReactionRule.minTemperatureC` + `LiquidPhysics.SetTemperature`/`ReactionPending` | A cold mix of the right recipe HOLDS as pending — no early observation, no wrong-mix scold; MixFeedback shows "Needs heat — warm to N C (water bath)". 12 rules game-wide carry a threshold — every warm-bath test (Tollens, esters, iodoform, chloroform AgNO3, benzamide acid/alkali…) now happens WHEN the procedure says. `SetContents` clears pending. |
| **⛔ ZONE-FREE tool rule (user 2026-07-17, binding for every module pass)** | `WaterBathController` + `VesselHeatTask` (`ExperimentLayout.Vessel.heatToC`) | "The entire lab IS the zone — tools function when brought together ANYWHERE." NO fixed stations/pads/DynLabels/TeleAnchors for steps the tools can own: heating = the bench `WaterBath` (player pours distilled water in ≥5 ml, stands a LIT burner within 0.45 m; heats to a 100 °C cap, warms vessels within 0.32 m, label narrates its next need) + `heatToC` on the synthesis vessel (task completes when served AND hot, wherever); filtering = the funnel pour itself (destination binding completes on receiving the product). Exp 2's two stations are deleted; when polishing each next module, REPLACE its Heat/Filter stations the same way (Weigh/Stir/Collect stations remain until each gets its pass). |
| **Ice-bath chill step** (Exp 4 crystallise; Exp 8's ice bath) | `IceBathController` on `Raw_IceBucket` + `VesselChillTask` (`Vessel.chillToC` + `chillTaskId`) | The water bath's cold twin, zone-free: any vessel within the chill zone is pulled to ~2 °C; the task completes only when the vessel HOLDS something AND is ≤ `chillToC` (ambient 25 °C can never self-complete). Pair with `longProcess` for the crystallise/dry time-skip. Wired by Apply W5.8. |
| **User-scalable EFFECT-ZONE anchors** (2026-07-18) | `PlacementAnchor` children read by the tool controllers via `WaterBathMath.EffectRadius` | The wire-sphere gizmo's world **scale IS the diameter** of the tool's reach — scale it in the editor and the mechanic follows exactly. Four exist (created by Apply W5.8, centred on the rendered body): `WaterBath/HeatZone` (orange, vessels warm inside) · `WaterBath/BurnerZone` (red, a lit burner inside drives the bath) · `Raw_IceBucket/ChillZone` (blue, vessels chill inside) · `FlorenceFlask/FermentZone` (green, limewater clouds inside). Moving the anchor re-centres the zone; deleting it falls back to the coded constant. Add the same pattern to any future area-of-effect. |
| **Flame (non-)flammability confirm** (Exp 7 test-flammability) | `VesselFlameTask` (`Vessel.flameTaskId`) + `FlameTestMath` | Zone-free: once the sample vessel is served, ANY lit match or burner flame held within 0.2 m confirms ("Won't ignite — NON-FLAMMABLE ✓" FloatingText) and completes the task. Resets on Retry. The sim lights the bench burner and holds the dish at its flame — the same `PollFlames` scan a play frame runs. |
| **Stir verb, zone-free** (Exp 8 "shake frequently") | `StirController` (`Vessel.stirTaskId`) + `OrbitMath` | Circle ANY bench glass rod inside the vessel (on the bench or held) — the controller tracks whichever rod's closest-geometry TIP is nearest (anchor-free, origin-agnostic like `GrindController.PestleTip`; the old origin-tracking never entered the mouth). Progress popups at 25% steps → "Well stirred!". Pair with `longProcess` for stand time-skips. |
| **Litmus confirmation step** (Exp 4 acid; Exp 8 BASE — red→blue) | `VesselLitmusTask` (`Vessel.litmusTaskId`) + `LiquidPhysics.CurrentPH` | A strip from the bench litmus box touched to the served tube completes the task when the MIXTURE reads acid (strip turns red + "acid confirmed" FloatingText). `CurrentPH` is mixture pH — the component farthest from 7 dominates (`LitmusMath.DominantPH`), so pour order can't soft-lock the read. ⚠ pH must be AUTHORED on the ChemicalData (most assets default 7 — benzoic 2.9 / HCl 6N 0.5 / H₂SO₄ 0.4 authored 2026-07-18; suite pins them). |
| Live temp goal on the vessel itself | `VesselStatusMath.TempGoalLine` (auto via `VesselStatus`) | Heat/chill vessels append "25 C — warm to 50 C (water bath)" / "chill to 8 C (ice bath)" to their name tag until the goal is reached. No extra wiring — reads the vessel's VesselHeatTask/VesselChillTask. |
| Vessels POUR OUT | `ShelfPourWiring.WireBottle` called by `BuildVessel` | Every task vessel (bench-bound or spawned) gets a `LiquidPourer`+spout+spill grading — the filter pour and draw-from-your-own-product steps were unplayable while only shelf bottles poured (2026-07-18; the sim's direct PourOut masked it). |
| Pharmee's spoken guidance | `PharmeeBrain.InstructionFor` | Speaks ONLY the ACTION line (after the →) of a two-line hint — short, imperative, "do this now"; the fact line stays on the wrist panel. |
| **Sim source honesty** (know when re-simulating) | `SimulatedRun.FindSource` | Sources rank: **pure-product vessel** (ledger collapsed to `DemoMode.ProductFor` — the dried crystals; the ONLY vessel scooped from) > chill (crude crystallising flask — decanted, not scooped) > **washed product** (product + Distilled Water ONLY in the ledger — Exp 7's crude beaker under its wash; decantable, any other foreign entry = test residue) > heat (single-entry filtrates/distillates) > fermentation wash > shelf; the module's own hidden END PRODUCT bottle is never legal. This is what exposed Exp 3's hidden-bottle shorthand. |
| **Fume hood step** (Exp 5 — the ONE hood module) | `LiquidTaskBinding.SetFumeHood` / `InFumeHood` + the hood's `WorkVolume` (`FumeHoodZone` + trigger BoxCollider) | A `requiresFumeHood` chem (Aniline, Acetyl Chloride) is only sanctioned while the RECEIVING VESSEL sits inside the hood volume — position-based (the old hand-occupancy trigger was never wired and ALWAYS violated; builder wires the zone into every binding now). The sim carries the vessel in, pours, returns it. Resize the WorkVolume BoxCollider to tune the sanctioned area. **The hood does NOT open** — it is an always-open alcove: `FumeHoodStatusLabel` narrates ("do aniline & acetyl chloride work IN here" → "<vessel> protected ✓"), and the **open-front collider shell** (`HoodShell_Back/Left/Right/Top/Counter`, hand-adjustable solid boxes) stops items passing through the walls while the open front stays the door. **Model = `FumeHoodOpen.prefab` (2026-07-18, Tripo P1 from `FumeHoodOpenRef.png`)** — genuinely hollow with a raised sash + interior counter; the old sealed `FumeHoodModel` is deactivated in the scene for hand-deletion; re-run `Swap In Open Fume Hood` + `Apply W5.8` after moving it — nothing can be trapped, and the Counter panel lets the vessel be SET DOWN inside while pouring. |
| ⚠ Fixture rule: building a bench-bound layout in a SUITE fixture | teardown `builder.Build("tutorial-methane")` in `finally` | Build() attaches bindings to REAL bench items — destroying only the fixture LEAKS them into later tests (the leaked Acetanilide bindings demanded a shelf-less chemical and broke the deplete monitor, 2026-07-18). |

### ⚠ Hard-won gotchas — CHECK THESE FIRST on the next module
- **⛔ BEATABILITY: a module's `trackedSkills` decides whether it can be PASSED AT ALL** (2026-07-19, found by `Simulate Campaign` — the entire campaign was unwinnable and no test noticed). Pass = grade ≥90 **AND BKT mastery ≥0.90**, and mastery is the MEAN over tracked skills. With the default BKT parameters one correct observation only reaches **0.68** (2 → 0.92, 3 → 0.99), so any tracked skill that appears in only ONE task drags the mean under the gate forever. `BuildMasteryModel` falls back to *every distinct skill the tasks reference* when `trackedSkills` is empty — which is exactly the worst case. Measured before the fix: tutorial 0.74 · Exp 3 **0.83** (a flawless run + a perfect quiz graded **TRY AGAIN**) · Exp 4 0.85 · Exp 6 0.87 · Exp 9 0.86; Exp 2 scraped through at 0.9001. **Every module now names its SIGNATURE skills** (each tracked skill has ≥2 tasks): methane [Transfer] · Exp 2 [TestInterp, Heating, Measuring] · Exp 3 [Measuring, TestInterp] · Exp 4 [TestInterp, Filtration, Measuring] · Exp 5 [Measuring, TestInterp] · Exp 6 [TestInterp, Measuring] · Exp 7 [Measuring, TestInterp, Safety, Filtration] · Exp 8 [Measuring, TestInterp, Filtration] · Exp 9 [TestInterp, Measuring]. Suite-pinned per module (`content: <file> is BEATABLE`) by replaying the runner's exact observation walk — **when you add/re-skill tasks, re-check that pin**.
- **`LiquidPhysics` on an opaque vessel makes the vessel VANISH in play.** `Start()` adopts the host's own renderer when `mainRenderer` is null, then disables it while empty (this hid the mortar for days). `ShouldAdoptHostRenderer` now only adopts a real `_Fill` surface — never point `mainRenderer` at a vessel's own mesh.
- **Verb refs are NOT serialized.** `_runner` / `_pestle` are null after a domain reload, so the task silently never completes. Bind at runtime (`GrindController.BindRunner`, `AutoFindPestle`).
- **Never mix absolute metres with local-unit bounds** — on an import-scaled prefab a "3 cm" cap becomes microscopic and the content is invisible.
- **Never guess a tool's working end from bounds** (which end is the blade/head is unknowable) — use an anchor.
- **World-space fills break on tilted/held vessels** — always fit in local space.
- **A world-space canvas needs `TrackedDeviceGraphicRaycaster`**, or the XR ray cannot click it (the quiz answers were dead for this reason).
- **Read `SampleScene.unity` directly to diagnose** — it is ground truth and far faster than theorising.

---

## tutorial-methane

**moduleId** `tutorial-methane` | **period** Tutorial | **prerequisite** `(none)` | **manuscript** (game-authored) | **end product** Methane gas (match pop; no shelf product)

**Official ILOs:** (1) (game-authored - pending client confirmation)


### Task graph (play order)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
| # | task | phase | label | prerequisites | hint |
|---|------|-------|-------|---------------|------|
| 1 | `prepare-mixture` | ReagentPrep | Grind the mixture in the mortar | - | Scoop the sodium acetate + soda lime into the mortar, then GRAB THE PESTLE and grind in the bowl until it is a fine powder. (You do not move the powder into the tube — grinding is all this step needs.) |
| 2 | `setup-apparatus` | Synthesis | Load the mixture into the hard-glass tube | `prepare-mixture` | Scoop the ground mixture from the mortar into the "Hard-glass tube" — this is the tube you will heat. (Ignore the bent delivery tubes; they are not used.) |
| 3 | `heat-mixture` | Synthesis | Heat the hard-glass tube | `setup-apparatus` | Grab a match and strike it on the matchbox, touch it to the burner to light it, then hold the lit burner under the "Hard-glass tube". Heating it makes the methane gas. |
| 4 | `collect-gas` | Synthesis | Collect the gas in the collection tube | `heat-mixture` | Keep the "Gas collection tube" held against the heated "Hard-glass tube" — methane collects until the tube is full (a few seconds). |
| 5 | `test-gas` | ChemicalTests | Test the gas with a lit match | `collect-gas` | Light a match and bring it to the mouth of the full "Gas collection tube" — methane pops and burns with a pale blue flame. Test complete! |

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Which gas is produced when sodium acetate is heated with soda lime?**
   - Ethane
   - Methane [CORRECT]
   - Carbon dioxide
   - Hydrogen
   - *why:* Decarboxylation of the acetate gives methane (CH4).
2. **Why is methane collected over water?**
   - It reacts with air
   - It is denser than water
   - It is nearly insoluble in water [CORRECT]
   - It dissolves the soda lime
   - *why:* Methane is only slightly soluble in water, so it displaces it cleanly.
3. **A clean, pale-blue flame on ignition indicates:**
   - Complete combustion [CORRECT]
   - Presence of water
   - Carbon dioxide
   - Incomplete reaction
   - *why:* Complete combustion of the collected methane.

## prelim-chemical-compounding

**moduleId** `prelim-chemical-compounding` | **period** Prelim | **prerequisite** `tutorial-methane` | **manuscript** Exp 2 (test battery DIVERGES - client flag, see header) | **end product** identification lab (no product)

**Official ILOs:** (1) Write chemical reactions involved in some organic compounds such as alcohols, aldehydes, ketones, carboxylic acids and esters. (2) Differentiate tests for different organic compounds.

**Manuscript Equipment & Apparatus:** Stirring rod; Aspirator; Test tube; Beaker; Test tube brush; Erlenmeyer flask; Test tube holder; Pipette; Porcelain spatula

**Manuscript Reagents:** 0.1% Potassium permanganate; Concentrated sulfuric; acid 10% Ferric chloride; Diluted acetic acid; 10% Sodium bicarbonate; Ethyl alcohol; 6N Sodium hydroxide; Glycerol; 6N Sulfuric acid; Methanol; Acetaldehyde; n-butyl alcohol; Acetone; Phenol; Aspirin; Salicylic acid; Benedict's reagent; Sec-butyl alcohol; Benzyl alcohol; Ter-butyl; alcohol Concentrated hydrochloric acid Tollen's; reagent


### Task graph (play order) — REBUILT to manuscript Exp 2 (2026-07-15)

Structure follows the manual's own sections **A → B → C → D**. Preps are free-order; each section gates the next.
**Apparatus column = what the step ACTUALLY needs, inferred from the procedure verbs** (the manual names almost no tool — see §Apparatus). **Nothing is pre-set** — the student prepares every tube; volumes are played as **dropper squeezes** (drops/small ml), **tilt-pour into a tolerance band** (bulk ml), or **spatula dips** (grams). See §VR adaptation.

| # | task | phase | label | prereq | manual ref | apparatus the step needs |
|---|------|-------|-------|--------|-----------|--------------------------|
| 1 | `prep-enol-tubes` | ReagentPrep | Set up the enol-test tubes (5 alcohols) | - | A.I.a | **5 × test tube + rack**; *pour* (stands in for **pipette**) for the 5 samples; **dropper** for the 1 ml samples; **tilt-pour** the 10 ml water |
| 2 | `test-enol-fecl3` | ChemicalTests | Enol test - add ferric chloride to each tube | 1 | A.I.b-c | **pipette/pour** (FeCl₃ ×5 tubes); the 5 tubes |
| 3 | `prep-oxidation-tubes` | ReagentPrep | Set up the rate-of-oxidation tubes (4 tubes) | - | A.II.a | **4 × test tube + rack**; student builds the **KMnO₄ + NaOH medium** (2 squeezes each) |
| 4 | `test-oxidation-alkaline` | ChemicalTests | Rate of oxidation in ALKALINE conditions (+ negative control) | 3 | A.II.b-c | **pipette/pour** (3 butyl alcohols); **stirring rod** (shake); **per-tube decolorisation TIMER** (`ProcessReadout`) |
| 5 | `test-oxidation-acidic` | ChemicalTests | Rate of oxidation in ACIDIC conditions | 4 | A.II.d-e | as #4, second rack; student builds the **KMnO₄ + H₂SO₄ medium**; both racks side-by-side |
| 6 | `test-tollens` | ChemicalTests | Aldehyde vs ketone - Tollen's test (water bath 5 min) | 5 | B.I | **2 × test tube**; **pipette/pour** (acetone, acetaldehyde, Tollen's); ⚠ **WATER BATH** (*absent from Exp 2's own apparatus list*); **test tube holder** (hot tube) |
| 7 | `test-ester-acetate` | ChemicalTests | Ester formation - ethyl acetate | 6 | C.I.a-b | **test tube**; **pipette/pour** (ethanol, acetic acid, conc H₂SO₄); ⚠ **WATER BATH**; **odour cue** (on-screen) |
| 8 | `test-ester-salicylate` | ChemicalTests | Ester formation - methyl salicylate | 7 | C.I.c-d | **test tube**; **PORCELAIN SPATULA** (0.1 g salicylic acid = **solid → scoop**); **pipette/pour** (methanol, conc H₂SO₄); ⚠ **WATER BATH**; **beaker** (1 ml water); **odour cue** |
| 9 | `prep-hydrolysis` | ChemicalTests | Hydrolyse the aspirin (boil in the water bath) | 8 | D.I.a-b | **test tube**; **PORCELAIN SPATULA** (0.5 g aspirin = **solid → scoop**); **pipette/pour** (10 ml water, conc HCl); ⚠ **WATER BATH** (boil) |
| 10 | `filter-hydrolysate` | ChemicalTests | Filter off the undissolved crystals | 9 | D.I.c | **ASPIRATOR** (the list's filtration tool) or **funnel + filter paper**; receiving vessel — ⬜ *decides the aspirator's job* |
| 11 | `test-hydrolysis-fecl3` | ChemicalTests | Test the filtrate with ferric chloride | 10 | D.I.d | **pipette/pour** (FeCl₃ ×2 drops) onto the filtrate |
| 12 | `test-hydrolysis-control` | ChemicalTests | Compare against UNhydrolysed aspirin | 11 | D.I.e | **test tube**; **PORCELAIN SPATULA** (plain aspirin); **pipette/pour** (FeCl₃) |
| 13 | `record-observations` | DataSheet | Record every colour and odour on the data sheet | 2,5,6,8,12 | Data Sheet | quiz tablet (+ results board) |

**Apparatus roll-up for the whole module:** 15 × test tube + racks · beaker · **water bath** (⚠ unlisted by the manual, needed 3×) · **porcelain spatula** (3 solid weigh-outs) · stirring rod · aspirator *or* funnel+filter paper · test tube holder · test tube brush (between tests → Sanitation). **No distillation train, no condenser, no thermometer** — this module never distils.

**par time** 900 s · **tracked skills** Measuring, Heating, Filtration, TestInterpretation · **all 20 reagents already exist as `ChemicalData`**.

**Still to author:** `Layout_ChemicalCompounding` (stations/vessels/bindings), the reaction rules + `expectedObservation` per test, and the odour cue (see VR-adaptation notes).

### VR adaptation — design decisions (SETTLED + BUILT 2026-07-16)

**The problem in numbers.** Done literally, Exp 2 is **~64 discrete pours across ~19 tubes** (A.I 15 · A.II ~28 for the two runs · B 5 · C 8 · D 8) — 30-40 min of pipetting, which is the least educational part of the lab.

**The governing principle (agreed):**
> **Preserve every SAMPLE, every OBSERVATION and every COMPARISON. Compress only the MANIPULATION.**

**The accuracy rule — NO pre-setting: the student prepares EVERY tube.**
An earlier draft pre-set the "constant" (the bulk water/medium) to save pours. **That was dropped 2026-07-16.** The real problem was never the pour COUNT — it is that **VR cannot hit an accurate volume**. Discretise the action and the accuracy problem vanishes, so there is no reason to pre-stage anything.

> **The watch panel prints the manuscript's REAL quantity (for facts), then the achievable action beneath it.**
> manual: *"In both test tubes, add 2 ml of Tollen's reagent."*
> panel: **"Add 2 ml of Tollen's reagent"** · *"2 squeezes of the sample, then 2 of Tollen's, in BOTH tubes."*

**THE CONTRACT: the number in the instruction IS the action count, whatever its unit.** The manuscript
measures Exp 2 in **three** units and all three become countable actions:

| Unit | Exp 2 examples | Verb |
|---|---|---|
| **drops** (dominant) | 5 drops FeCl₃ · 2 drops NaOH · 2 drops acetone · 10 drops ethanol · 1 drop conc H₂SO₄ | **Dropper** — 1 squeeze = 1 drop (physically honest) |
| **ml** | 1 ml sample · 2 ml KMnO₄ · 2 ml Tollen's | **Dropper** — 1 squeeze = 1 ml (the deliberate abstraction) |
| **ml, bulk** | the 10 ml of water | **Tilt-pour** — existing `LiquidPourer`, generous `requiredMl` band + live readout |
| **grams** | 0.1 g salicylic · 0.5 g aspirin | **Porcelain spatula** — 0.1 g/dip = 1 and 5 dips |

⚠ Do NOT collapse drops and ml into one rule: "1 squeeze = 1 ml" would turn "5 drops" into 5 ml. The
manuscript distinguishes them and so must the copy; the SQUEEZE COUNT is what unifies them.

**This is the same trick the project already uses for solids.** `ScoopMath.GramsPerScoop = 2f` plays
"weigh 4 g" as two countable dips — `DropperMath.MlPerSqueeze` is its liquid twin (pure static math +
thin controller + `Bind()` seam). `RawReagentCatalog.LabwareKind.DropperBottle` already existed,
already documented as "test reagents added by drops".

**⚠ The scoop could not express Exp 2's solids at all**: `GramsPerScoop = 2f`, but Exp 2 weighs 0.1 g and
0.5 g — BOTH smaller than one dip. Hence `ScoopMath.GramsPerSpatula = 0.1f` + `ScoopController.gramsPerDip`.

Errors stay real: wrong count, wrong tube and wrong reagent all still grade, animate, and can starve the
supply into a restart. Only the analog precision is gone.

**Other agreed adaptations**
- **Odour → on-screen cue** ✅ **client-approved**: the two ester tests show e.g. *"Sweet, fruity odour - ethyl acetate formed"* / *"Wintergreen - methyl salicylate"*. The one genuine sensory loss.
- **Time compression:** the 5-min water bath / 3-5 min boil → **~15 s** with `ProcessReadout` counting. No learning in real-time waiting.
- **BUT keep the decolorisation TIMER.** The manual explicitly says *"note the time of decolorization"* — that is a **measurement**, so each tube gets a live per-tube timer. Compress the wait; never hide the number.
- **Both oxidation racks side by side** (alkaline + acidic simultaneously). The manual runs them sequentially, so the student compares from memory — VR can show both at once. This is **better than the manual**, not a compromise.
- **Results board:** a rack-side panel auto-filling the manual's own data-sheet table as each result lands, so `record-observations` is a real review rather than recall.

**⛔ UNTOUCHABLE — these ARE the experiment (never trim to save actions)**
- all **5 enol samples** (phenol-vs-the-rest is the lesson)
- all **3 butyl alcohols + the negative control** (the 1°/2°/3° rate difference, and the control teaches experimental design)
- **acetone vs acetaldehyde** (the contrast IS the test)
- **hydrolysed vs unhydrolysed aspirin** (same)

**✅ DECIDED 2026-07-16**
1. **No pre-set** — the student prepares everything; accuracy is solved by the dropper / tilt-pour / spatula split above.
2. **Episode picker: period → MODULE (two-step).** Choosing **Prelim** lists its **two modules** (Compounding, Ethyl Alcohol), the second dimmed until the first passes — same for Midterm (4) and Final (2). Today `GatekeeperModel.EpisodeOptions` lists only the **four periods** and `ChooseEpisode` silently auto-starts `FirstPlayableInPeriod`, so the player never sees module names. Needs: a `ModuleOptions(flow, period)` beside `EpisodeOptions`, one new gate state between `EpisodePick` and the confirm, and a row-index handler in `PharmeeGatekeeper` (~line 229). The 9-module chain **already locks in sequence** — only the picker's second level is missing. **Exp 2 stays ONE module** (13 tasks); a picker change, not a content split.
3. **A PASSED module stays REPLAYABLE**, marked ✓ PASSED with its previous grade; best score kept. Note `FirstPlayableInPeriod` already falls back to "first unlocked" once a period is fully passed — replay exists today only as an accident of that fallback; the module picker makes it intentional.

**✅ BUILT 2026-07-16** — Exp 2 is playable end-to-end; suite 1055/1055.
1. `DropperMath` + `DropperController` (the contract above; a full dropper = 10 squeezes, the largest single Exp 2 instruction). Each squeeze deposits through the normal `AddLiquid` path, so `LiquidTaskBinding.requiredMl` counts it for free — no new task plumbing.
2. `ScoopMath.GramsPerSpatula = 0.1f` + `ScoopController.gramsPerDip`.
3. **KMnO₄ → Liquid, renamed `Potassium Permanganate 0.1%`** — it was **Solid**, which `LiquidPourer` cannot pour, so A.II's "2 ml of 0.1% KMnO₄" was unbuildable. The manuscript only ever uses the 0.1% SOLUTION at the bench.
4. **`RackMath` + `RackTaskGroup`** — see §Stage layout.
5. **`Layout_ChemicalCompounding` REBUILT** from the retired battery.
6. **8 reaction rules + `expectedObservation`**; **13 hints** rewritten.
7. **`LiquidTaskBinding` multi-reagent fix** — `_accumulated` was keyed by taskId alone, so a task naming SEVERAL reagents pooled them and **whichever landed first completed the step with half the chemistry missing**. It is now per-STEP and a task waits for all its reagents. This also silently fixed three SHIPPED modules (Acetone + EthylAlcohol `test-iodoform` needed KI *and* hypochlorite; BenzoicAcid `test-ester` needed the alcohol *and* its acid catalyst).

**⬜ STILL TO DO**
1. **Two-step episode picker** (per §2) — not started.
2. ⚠ **Fix `StirController` before wiring any "shake/stir" step** — it tracks the rod's transform ORIGIN (`_rod.position`), the same latent bug that made the grind silently never complete. Give it tip-tracking + a `StirAnchor`, exactly as `GrindController` got (`PestleTip` closest-point + `BowlAnchor`). See §Apparatus → anchor candidates.
3. **Headset playtest** — the 19-tube reach, dropper feel and rack pacing are unvalidated in VR.

### Stage layout (rebuilt 2026-07-16 — the old file was the RETIRED battery)

⚠ The previous `Layout_ChemicalCompounding` still staged `test-combustion` / `test-sodium` /
`gather-ethanol` / `test-bromine` / `test-kmno4` — **zero overlap** with the 13 manuscript tasks. The
stage built, nothing errored, and **not one task could ever complete**. `LayoutGraphCoverageSuite` now
pins layout↔graph coverage both ways so this cannot recur.

**Rack groups** (`rackGroup` + `completesTask:0` → the step waits for EVERY tube):
- `enol` (5 tubes) — Ethanol · n-Butyl · Benzyl · **Phenol** · Glycerol. 1 ml sample + 10 ml water each, then 5 drops FeCl₃.
- `oxalk` (4) — 2 ml KMnO₄ 0.1% + 2 drops NaOH 6N in all four; the 3 butyl alcohols into tubes 1–3.
- `oxacid` (4) — the same with H₂SO₄ 6N. **Both racks side by side** (the manual runs them sequentially so the student compares from memory — VR shows both at once: better than the manual, not a compromise).
- `tollens` (2) — Acetone vs Acetaldehyde, 2 drops each + 2 ml Tollen's.

**Why RackTaskGroup exists:** `LiquidTaskBinding` is per-VESSEL, so five tubes bound to one task meant
**the first tube to hit its threshold completed the step** and the other four became optional — silently
throwing away the comparison that IS the lesson (the doc's four UNTOUCHABLES). Members defer completion;
the group calls the step in once every member is ready, and floats `"tube 3 of 5"` while it fills.

**The negative control is the 4th oxidation tube.** It takes the medium but **declares no binding for the
alcohol step**, so it is not a member of that group — leaving it alone is correct play, and pouring into
it is a genuine wrong-reagent mistake. That is how the control teaches experimental design.

**Standalone vessels:** `EsterAcetateTube` (10 drops ethanol + 4 acetic + 1 conc H₂SO₄) ·
`EsterSalicylateTube` (methanol + 1 spatula dip salicylic + acid) · `HydrolysisTube` (5 dips aspirin +
10 ml water + 1 drop conc HCl) · `HydrolysisControlTube` (plain aspirin + FeCl₃) · `FiltrateBeaker`.

**Stations:** `prep-hydrolysis` (Heat 95 °C — the bath OWNS this step) · `filter-hydrolysate` (Filter,
Funnel prop). **No station for `record-observations`** — DataSheet steps close via the quiz/tablet (the
shipped Acetanilide layout stages none either).

⚠ **ENGINE CONSTRAINT (carry forward):** a task completes by **EITHER its pours OR a station, never
"pour THEN heat"** — each heated step must pick one owner. So `prep-hydrolysis` is owned by the BATH (its
label *is* "boil in the water bath"; its reagents are `completesTask:0`, same shape as Acetanilide's
`heat-bath`), while `test-tollens` is owned by its RACK and gets **no** station — a station there would
race the rack and complete the step the moment the tubes were warm, reagent or not. The ester warmings
likewise complete on their reagents; their graded evidence is the **odour cue**, not the heat.
*Known consequence of the same constraint:* boiling an empty tube completes `prep-hydrolysis`, exactly as
in every shipped Heat module. Worth revisiting engine-wide, not per-module.

**⛔ Tools staged: NONE — and no reagent bottles either.** The bench already holds every
general tool (`Eq_Dropper`, `Eq_PorcelainSpatula`, `Eq_Funnel`/`Funnel_2`, `Kit_BunsenBurner_0_9`,
`Kit_TestTube_0-5`, `Kit_Hard-GlassTestTube_0-3`, `Eq_TestTubeBrush`, `Eq_GlassRod`, `Eq_WashBottle`,
cylinders, beakers, watch glass…) and **all 21 of this module's reagents** as shelf bottles
(`Reagent_*` west cubby + `Raw_*` east cabinets — Ethanol, Acetone, Ferric Chloride 10%, KMnO₄ 0.1%,
Salicylic Acid and Aspirin included). The first draft of this layout staged **46 duplicates** of that
bench. Stations point at bench itemIds instead (`kit-bunsenburner`, `kit-funnel`) — a `ZoneItemSensor`
matches a `LabItem.itemId` wherever it already lives. Suite-pinned (`bench:` assertions).
**Only the task-bound VESSELS are staged.**

⚠ **Tube budget:** Exp 2 needs **19 regular tubes simultaneously** — the worst case in the whole game
(every other module needs ≤4; Exp 6 has 4 tests, Exp 7/8 three each). The bench has 6, so **13 more
regular tubes** are needed. **Hard-glass: 4 on the bench is plenty** — the only naked-flame tube in any
experiment is Exp 6's dry distillation; everything else (including Exp 2's boil) heats in a water bath
at ≤100 °C. Methane's hard-glass is its own staged prop. (19 assumes the alkaline + acidic racks sit
side by side, which the design deliberately prefers over the manual's sequential reuse; reusing would
make it 15.)

### Reactions & expected observations (authored 2026-07-16 — 8 rules)

**Colours are OURS.** The manuscript's `"10% - violet - 0"`-style annotations are a previous developer's
notes of unknown meaning and are **explicitly disregarded (user 2026-07-16)**. These come from the real
chemistry.

| Rule | Pair | Outcome | Observation |
|---|---|---|---|
| `Test_EnolPhenolFeCl3` | Phenol + FeCl₃ 10% | ColorChange | Deep violet at once — the four other alcohols stay unchanged: **that contrast IS the test** |
| `Test_OxidationPrimaryKMnO4` | n-Butyl + KMnO₄ | ColorChange | Purple fades **fastest**; brown MnO₂ left. Note the decolorisation time |
| `Test_OxidationSecondaryKMnO4` | sec-Butyl + KMnO₄ | ColorChange | Fades **noticeably slower** — a 2° alcohol resists more |
| `Test_TollensAldehyde` | Acetaldehyde + Tollen's | Precipitate (≥50 °C) | Bright **silver mirror**; acetone leaves it clear |
| `Test_EsterEthylAcetate` | Ethanol + Diluted Acetic | **Odor** (≥50 °C) | *Sweet, fruity — ethyl acetate formed* |
| `Test_EsterMethylSalicylate` | Methanol + Salicylic Acid | **Odor** (≥50 °C) | *Sharp wintergreen — methyl salicylate formed* |
| `Test_AspirinHydrolysis` | Aspirin + conc HCl | ColorChange (**≥90 °C**) | Boiling in acid frees salicylic acid into the filtrate |
| `Test_HydrolysateFeCl3` | Salicylic Acid + FeCl₃ | ColorChange | Violet — the ester **was** hydrolysed; plain aspirin gives none |

**⛔ The NEGATIVES are deliberately UNAUTHORED — do not "fix" this.** tert-Butyl resisting KMnO₄ (no
α-hydrogen), acetone leaving Tollen's clear (a ketone cannot reduce it), and unboiled aspirin giving no
violet are **not missing rules**: "nothing happens" IS each test's answer, and the contrast is the whole
lesson. A rule there would invent chemistry the manuscript denies. Suite-pinned as absent.

**Odour → on-screen cue** ✅ client-approved: the one genuine sensory loss, so both esters carry it in
`expectedObservation` and fire `ReactionOutcome.Odor`.

### Watch-panel instructions — the 13 hints (rewritten 2026-07-16)

**MATERIALS header (2026-07-17, ALL 9 modules):** the holo board opens with a gather-first guide —
reagents **with totals derived from the layout bindings** (`Tools ▸ PharmaSynth ▸ Generate Materials
Guides`; liquids "N ml" / solids "N g" in the game's own 1-squeeze-=-1-ml system, so it can never
drift from what the tasks consume) and **apparatus authored from the PROCEDURE** (never the
manuscript's defective lists). Stored as `materialReagents`/`materialApparatus` on the module,
rendered by `ChecklistPager.BuildMaterialsHeader` above the checklist. NOT a checklist item — it
completes nothing, stays fully visible, and the board **keeps its scroll position between opens**
(the W5.12 snap-to-top is gone; re-run the generator after any layout change).


**The contract:** every step prints the manuscript's **REAL quantity** (so the student learns the true
recipe), then the **achievable action** underneath.

> *"Add 2 ml of Tollen's reagent"*
> `→ 2 squeezes of the sample, then 2 of Tollen's, in BOTH tubes. Only the aldehyde plates a silver mirror.`

**The machinery was already built and is generic — the instruction quality is pure DATA** (the `hint`
field). `ChecklistPager.BuildFocusedText` renders the CURRENT step's hint under it (`•` done · `»` current
· `□` pending) and `WristWatchController` rebuilds that text **every frame** while the board is up, so it
can never go stale. Suite-pinned: every hint is two lines, and every action line names a **countable
verb** (squeeze / dip / tilt-pour / funnel / burner / board) — that test caught two real copy failures,
including an action line that said only "compare" and never named a thing to touch.

**Step-completion alerts are generic too — nothing to build per module:**
- **HUD toast** `"Step complete: <label> ✓"` (`ExperimentHudController:82`)
- **success chime** `grade-pass` (`:84`)
- **Pharmee praises + speaks the NEXT step** (`PharmeeBrain:126`)
- **Rack progress** floats `"tube 3 of 5"` so a finished tube never reads as a finished step.

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **In the enol test, which sample turns violet with ferric chloride?**
   - Ethanol
   - Phenol [CORRECT]
   - Glycerol
   - n-Butyl alcohol
   - *why:* Only an enol/phenol gives the violet iron(III) complex; simple alcohols do not.
2. **In the rate-of-oxidation test, which alcohol resists oxidation by KMnO4?**
   - n-Butyl alcohol (primary)
   - sec-Butyl alcohol (secondary)
   - tert-Butyl alcohol (tertiary) [CORRECT]
   - All oxidise at the same rate
   - *why:* A tertiary alcohol has no H on the carbinol carbon, so KMnO4 is not decolourised - primary oxidises fastest.
3. **Why is the fourth oxidation tube left with no alcohol added?**
   - To save reagents
   - As a negative control, to prove KMnO4 alone does not fade [CORRECT]
   - To rinse the pipette
   - To dilute the others
   - *why:* The control shows any colour change is caused by the alcohol, not by the permanganate itself.
4. **In Tollen's test, the silver mirror forms with which sample?**
   - Acetone (a ketone)
   - Acetaldehyde (an aldehyde) [CORRECT]
   - Both equally
   - Neither
   - *why:* Aldehydes are oxidised by Tollen's reagent and deposit silver; ketones are not.
5. **Adding ferric chloride to the BOILED aspirin filtrate gives violet. Why?**
   - The aspirin is undissolved
   - Hydrolysis freed salicylic acid, which has a phenol group [CORRECT]
   - The acid neutralised the iron
   - Ferric chloride always turns violet
   - *why:* Hydrolysing the ester frees salicylic acid; its phenolic -OH gives the violet complex - intact aspirin does not.

## prelim-ethyl-alcohol

**moduleId** `prelim-ethyl-alcohol` | **period** Prelim | **prerequisite** `prelim-chemical-compounding` | **manuscript** Exp 3 | **end product** Ethanol

**Official ILOs:** (1) Synthesize ethyl alcohol. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** Concentrated sulfuric acid; 6% Sodium hypochlorite; Diluted acetic acid; 10% Potassium iodide; Dry yeast; 10% Sodium hydroxide; Ethanol; Ammonium phosphate; Saturated calcium hydroxide; Brown sugar

### ⭐ VR apparatus review (2026-07-17) — every manuscript item IS on the bench
All 19 apparatus map to existing bench objects: distilling flask `kit-distillingflask` · Erlenmeyer `kit-erlenmeyerflask` · **delivery/"bath Rubber tubing"** `kit-deliverytube` (the CO₂→limewater bubbler — Exp 3's signature) · cotton swab `Raw_CottonSwabs` · cork `kit-rubberstopper` · watch glass `Eq_WatchGlass` · graduated cylinder `Eq_GraduatedCylinder_50mL` · porcelain spatula · stirring rod `Eq_GlassRod` · test tubes/brush/holder · pipette = `Eq_Dropper_1–4`.

**⛔ Six items REMOVED from the bench (menu `Remove VR-Inappropriate Apparatus`) — a DELIBERATE, documented exception to the "all tools always present" rule; prefabs kept in the project so any later module can re-place them. Do NOT let an all-tools audit re-add them.**

| Removed | Why it is not a VR interaction |
|---|---|
| Iron stand (`kit-retortstand`) | Support scaffold to hold a flask on a rod. VR heats **zone-free** — carry the flask to the water bath; there is no clamp-rig to assemble. |
| Iron clamp + S-clamp (`kit-utilityclamp`) | The clamps that fix glassware to that stand — same scaffold, no VR action. |
| Aspirator (`kit-aspirator`) | Vacuum suction for filtration/transfer. The game moves liquid by **pour/decant**, never suction. |
| Condenser (`kit-condenser`) | Distillation cooling. Distillation is an **abstracted heat + collect** sim (lenient 70–80 °C window); the player never assembles a condenser train. |
| Thermometer (`kit-thermometer`) | Temperature is shown live by the floating **ProcessReadout** / water-bath label, so the physical instrument is decorative. |
| Iron ring ×2 (`Eq_IronRing` + `IronRing_2`, no itemId) | Both clamp onto the retort stand (already removed) → orphaned. Matched by name CONTAINS so both go. |
| Clay triangle (`kit-claytriangle`) | Cradles a crucible over an open flame — the crucible is gone and the kept tripod+gauze already cover "hold over the burner". |
| Crucible (`kit-crucible`) + tongs (`kit-crucibletongs`) | Strong >500 °C ignition/ashing vessel + its handler — **0 manuscript & 0 game usage**; the water bath caps at 100 °C. |
| Alcohol burner (`kit-alcoholburner`) | Redundant flame source — the Bunsen burner beside the water bath covers all heating. |
| Empty vials ×8 (`Kit_Vial_0–3` = `kit-vial`; `Eq_Vial_Brown` + `Vial_Brown_2/3/4`, no itemId) | **0 manuscript, 0 game usage** — leftover reagent-staging containers from the retired-battery layout; reagents now come from the `Raw_` bottles. |
| Forceps (`Eq_Forceps`, no itemId) | **0 manuscript, 0 game usage** — only generic metadata refs (size/physics/hover), no task or verb reads it; VR grab replaces it. |

**KEPT** (user 2026-07-18): **tripod (`kit-tripod`) + wire gauze (`kit-wiregauze`)** — a genuine heat platform the player sets over a burner to hold a vessel for heating.

The chemistry apparatus (flasks, delivery tube, watch glass, graduated cylinder, droppers, tubes, spatula, rod) all stay interactive.

### ⏳ Time-skip (fermentation) — reusable across ALL experiments
The manuscript ferments **one full week**. VR compresses this: once the must is prepared and sealed, the completing task is authored `longProcess` → `TimeSkipController` plays a **6 s corner-camera timelapse** (days and nights, a day counter) and returns with a "time has passed" success message. This is the standard treatment for every lengthy real-world wait in any module (overnight dries, hour-long crystallisations) — author the closing task `longProcess=true` + a message, and `timelapseHours` only when the procedure states the duration.

### ✅ BUILT + SIMULATED CLEAN 2026-07-17 (8/8 · 0 mistakes · 0 bugs)
Zone-free, bench-bound (like Exp 2). **8 tasks**: prepare-must → ferment (CO₂/limewater + **week time-skip**) → adjust-ph → distill → test-combustion/iodoform/ester → record-yield (auto-completes). **Vessels:** `FlorenceFlask` (fermentation, `fermentTaskId`) · limewater `Kit_TestTube_5` · `DistillingFlask` (`heatToC 70`) · `Eq_WatchGlass` (combustion) · iodoform `Kit_TestTube_6` (`heatToC 60`) · ester `Kit_TestTube_7` (`heatToC 50`). **The CO₂→limewater mechanic** = `FermentationController` (zone-free, mirrors the water bath): once prepare-must is done the flask evolves CO₂ into any nearby limewater vessel → `Limewater_CO2` clouds it milky → ferment completes → time-skip. CO₂ is a REACTION DRIVER, added via `AddLiquid(notify:false)` so it drives the reaction without being graded a reagent. Distillation + warm tests reuse `heatToC` + the water bath. **VR simplifications** (documented): bulk solids use the scoopula (2 g), sub-gram the spatula; **the distill step DECANTS the fermented wash from the FlorenceFlask into the distilling flask** (2026-07-18 — the manuscript's own step; the old bench-Ethanol shorthand was UNPLAYABLE because that bottle is hidden during Exp 3 as its own end product. The `Ferment` rule turns the must into Ethanol at the yeast pour, so the flask pours Ethanol; vessels can pour out since `WireBottle` runs in `BuildVessel`); combustion completes on the ethanol-on-watch-glass pour (match = guidance). Generator: scratchpad `gen_ethyl.py`.

The pre-polish stub graph is retained below for history.

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
| # | task | phase | label | prerequisites | hint |
|---|------|-------|-------|---------------|------|
| 1 | `prepare-must` | ReagentPrep | Prepare the fermentation must (sugar + yeast) | - | 12% sugar + 4 g yeast + a pinch of ammonium phosphate ferments to alcohol. → Florence Flask: 6 scoopula dips of brown sugar, 2 scoopula dips of yeast, 1 scoopula dip of ammonium phosphate, then tilt-pour distilled water to the 20 ml band. |
| 2 | `ferment` | Synthesis | Ferment; confirm CO2 with limewater | `prepare-must` | Yeast digests sugar to ethanol + CO2; the CO2 clouds limewater (CaCO3) — proof it works. → Pour 10 ml of limewater into a clean test tube, then lead the delivery tube from the flask into it and watch it turn milky. |
| 3 | `adjust-ph` | Synthesis | Adjust the wash slightly alkaline | `ferment` | Distilling runs cleaner from a slightly alkaline wash. → Squeeze 2 dropper-loads of 10% sodium hydroxide into the Florence Flask to bring the wash slightly alkaline. |
| 4 | `distill` | Synthesis | Distil; collect the 70–80°C fraction | `adjust-ph` | Ethanol boils at 78°C — collect the 70-80°C cut and leave the water behind. → Decant 20 ml of the fermented wash into the Distilling Flask, then warm it at the water bath until it reads 70 C. |
| 5 | `test-combustion` | ChemicalTests | Combustion test (blue flame) | `distill` | Pure ethanol burns with a clean BLUE flame (complete combustion). → Pour 3 ml of your distillate onto the Watch Glass, then strike a match and hold the flame to it. |
| 6 | `test-iodoform` | ChemicalTests | Iodoform test (yellow precipitate) | `distill` | Ethanol gives a pale-YELLOW iodoform precipitate with KI + hypochlorite. → In a clean test tube: 1 squeeze of your distillate, 10 squeezes of potassium iodide 10%, 5 squeezes of sodium hypochlorite - then warm it at the water bath to 60 C. |
| 7 | `test-ester` | ChemicalTests | Ester formation (fruity odour) | `distill` | Ethanol + acetic acid + sulfuric acid make a sweet, fruity ESTER smell. → In a clean test tube: 10 squeezes of your distillate, 4 squeezes of diluted acetic acid, 1 squeeze of sulfuric acid - then warm it at the water bath. |
| 8 | `record-yield` | DataSheet | Record your observations | `test-combustion`, `test-iodoform`, `test-ester` | Every colour, odour and the distillate volume goes on the data sheet. → Read the results back, then answer on the data sheet. |

### Stage layout

**Stations:** `distill` (Heat to 78 C) ; `test-combustion` (zone-touch)

**Reagents staged (pourable):** Brown Sugar (Vial_Brown, auto-supply) ; Yeast (Vial_Brown, auto-supply) ; Sodium Hypochlorite (Vial_Brown, auto-supply) ; Glacial Acetic Acid (Vial_Brown, auto-supply) ; Potassium Iodide 10% (Vial_Brown, auto-supply)

**Tools staged:** Distillation Flask ; Lit Splint

**Vessel Beaker_500mL** (Fermentation Beaker) - starts EMPTY
  - pour **Brown Sugar** >= 50 ml -> completes `prepare-must`
  - pour **Yeast** >= 50 ml -> completes `ferment`
  - pour **Potassium Iodide 10%** >= 50 ml -> completes `test-iodoform`

**Vessel TestTube_WithLiquid** (Distillate Test Tube) - starts with Ethanol
  - pour **Sodium Hypochlorite** >= 50 ml -> completes `test-iodoform`
  - pour **Diluted Acetic Acid** >= 50 ml -> completes `test-ester`

### Reactions & expected observations

- **EsterFormation**: Ethanol + Diluted Acetic Acid -> Ethyl Ester - "Sweet fruity ester odour"
- **Ferment**: Brown Sugar + Yeast -> Ethanol - "Steady CO2 bubbling; fermentation underway"
- **Iodoform**: Ethanol + Sodium Hypochlorite - "Yellow iodoform precipitate"

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Fermentation of sugar by yeast produces ethanol and:**
   - Oxygen
   - Carbon dioxide [CORRECT]
   - Hydrogen
   - Methane
   - *why:* CO2 is the by-product, confirmed by limewater turning milky.
2. **At roughly what temperature is the ethanol fraction distilled?**
   - 30-40 C
   - 100-110 C
   - 70-80 C [CORRECT]
   - 150-160 C
   - *why:* Ethanol boils ~78 degrees; the 70-80 cut is collected.
3. **A positive iodoform test gives a precipitate that is:**
   - Yellow [CORRECT]
   - White
   - Black
   - Blue
   - *why:* Iodoform (CHI3) is a yellow precipitate.

## midterm-benzoic-acid

**moduleId** `midterm-benzoic-acid` | **period** Midterm | **prerequisite** `prelim-ethyl-alcohol` | **manuscript** Exp 4 (printed procedure is errata; game route = benzaldehyde + KMnO4) | **end product** Benzoic Acid

**Official ILOs:** (1) Synthesize benzoic acid. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** Benzoic acid; 0.1% Potassium permanganate; Concentrated sulfuric acid; 6N Hydrochloric acid; Methanol; 6N Sodium hydroxide; Propyl; 10% Sodium hydroxide; alcohol Benzaldehyde


### \u2705 BUILT + SIMULATED CLEAN 2026-07-18 (9/9 \u00b7 0 mistakes \u00b7 0 bugs \u00b7 0 warnings)

Zone-free, bench-bound (Exp 2/3 pattern), stations DELETED. The printed manuscript procedure is the confirmed errata (Exp 3 copy-paste) \u2014 the game route benzaldehyde + 0.1% KMnO\u2084 is correct-by-intent. Generator: scratchpad `gen_benzoic.py`.

### Task graph (play order \u2014 labels/hints as built)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `prepare-permanganate` | ReagentPrep | Prepare the 0.1% permanganate oxidising bath | - | tilt-pour ~40 ml KMnO\u2084 0.1% into `Eq_Beaker_500mL` (panel prints the real prep fact: 0.1 g / 100 ml) |
| 2 | `oxidise-benzaldehyde` | Synthesis | Oxidise benzaldehyde with the warm permanganate | 1 | 2 dropper squeezes benzaldehyde + warm the beaker \u226540 \u00b0C at the water bath (`heatToC 40`; BenzoicOxidation is heat-gated at 40 \u2014 pending until warm, MixFeedback cues "Needs heat") |
| 3 | `filter-mno2` | Synthesis | Filter off the brown MnO2 | 2 | funnel pour beaker \u2192 `ErlenmeyerFlask_400mL_2` (\u226520 ml Benzoic Acid received; the MnO\u2082 ppt stays behind \u2014 PourOut pours liquid only) |
| 4 | `acidify` | Synthesis | Acidify the filtrate with 6N HCl | 3 | 5 squeezes HCl 6N \u2192 `Acidify_BenzoicPpt` drops white crystals per squeeze |
| 5 | `crystallise` | Synthesis | Chill in the ice bath; crystallise | 4 | set the flask in `Raw_IceBucket` (`chillToC 8`, `VesselChillTask`) \u2192 **longProcess time-skip** ("crystals grew, recrystallised from hot water and dried") |
| 6 | `test-litmus` | ChemicalTests | Litmus test \u2014 blue litmus turns red | 5 | Test Tube 8: **1 scoopula dip of crystals (2 g)** + 2 squeezes distilled water, then TOUCH a litmus strip (`litmusTaskId`; mixture pH 2.9 \u2192 red + "acid confirmed" FloatingText) |
| 7 | `test-fecl3` | ChemicalTests | Ferric chloride test \u2014 buff precipitate | 5 | Test Tube 9: **1 scoopula dip of crystals** + 2 squeezes NaOH 10% + 5 squeezes FeCl\u2083 \u2192 buff `Ferric Benzoate` ppt |
| 8 | `test-ester` | ChemicalTests | Ester test \u2014 fruity odour with propyl alcohol | 5 | Test Tube 10: **1 scoopula dip of crystals** + 10 squeezes propyl + 1 squeeze H\u2082SO\u2084, warm \u226550 \u00b0C (`heatToC 50`; rule gated at 40) \u2014 odour shown on screen |
| 9 | `record-yield` | DataSheet | Record yield and observations | 6,7,8 | auto-completes (wrap-up flag) |

**All product draws come from the PURIFIED Erlenmeyer flask** (sim source rule: chill vessel > heat vessel), never the crude beaker and never the bench `Raw_BenzoicAcid` \u2014 that bottle is HIDDEN during this module (`EndProductVisibility`).

### Stage layout (5 bench-bound vessels; stations: []; nothing spawned)

- `Eq_Beaker_500mL` **ReactionBeaker** \u2014 KMnO\u2084 0.1% \u226540 (task 1, completes) \u00b7 Benzaldehyde \u22652 (task 2, deferred) \u00b7 `heatToC 40`
- `ErlenmeyerFlask_400mL_2` **FiltrateFlask** \u2014 Benzoic Acid \u226520 (task 3, completes) \u00b7 HCl 6N \u22655 (task 4, completes) \u00b7 `chillToC 8` / `chillTaskId crystallise`
- `Kit_TestTube_8` **LitmusTube** \u2014 Benzoic Acid \u22652 + Distilled Water \u22652 (deferred) \u00b7 `litmusTaskId test-litmus`
- `Kit_TestTube_9` **FeCl3Tube** \u2014 Benzoic Acid \u22652 + NaOH 10% \u22652 + FeCl\u2083 10% \u22655 (all complete together)
- `Kit_TestTube_10` **EsterTube** \u2014 Benzoic Acid \u22652 + Propyl \u226510 + H\u2082SO\u2084 \u22651 (deferred) \u00b7 `heatToC 50`

Tools used from the bench: funnel (LiquidPassthrough), droppers, water bath + burner, **ice bucket** (`IceBathController`), **litmus box** (`Raw_LitmusPaper` strips). Supply audit: worst draw is KMnO\u2084 40 of 150 ml \u2014 ample margin.

### Reactions & expected observations

- **BenzoicOxidation** (min 40 \u00b0C): Benzaldehyde + KMnO\u2084 0.1% \u2192 Benzoic Acid + MnO\u2082 ppt \u2014 "Violet discharged; brown MnO2 forms"
- **Acidify_BenzoicPpt** (new 2026-07-18): Benzoic Acid + HCl 6N \u2192 white Benzoic Acid crystals \u2014 "White benzoic acid crystals separate as the filtrate turns acidic."
- **Draft_BenzoateFeCl3**: Benzoic Acid + FeCl\u2083 10% \u2192 buff **Ferric Benzoate** ppt (real precipitate authored 2026-07-18; was a null ppt)
- **Test_BenzoateEster** (min 40 \u00b0C): Benzoic Acid + Propyl Alcohol \u2192 Ethyl Ester \u2014 "Sweet aromatic ester odour"

**pH data authored 2026-07-18** (litmus was dead on arrival \u2014 everything read 7): Benzoic Acid 2.9 \u00b7 HCl 6N 0.5 \u00b7 Sulfuric Acid 0.4; suite-pinned.

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Benzoic acid is prepared here by oxidising:**
   - Toluene
   - Benzaldehyde [CORRECT]
   - Phenol
   - Aniline
   - *why:* Benzaldehyde is oxidised by dilute KMnO4 to benzoic acid.
2. **The brown solid filtered off after oxidation is:**
   - Carbon
   - Iron oxide
   - Manganese dioxide [CORRECT]
   - Silver
   - *why:* Reduced permanganate leaves manganese dioxide (MnO2).
3. **Why acidify the filtrate with HCl?**
   - To precipitate benzoic acid [CORRECT]
   - To dissolve crystals
   - To add colour
   - To neutralise nothing
   - *why:* It converts soluble benzoate back to insoluble benzoic acid.

## midterm-acetanilide

**moduleId** `midterm-acetanilide` | **period** Midterm | **prerequisite** `midterm-benzoic-acid` | **manuscript** Exp 5 | **end product** Acetanilide

**Official ILOs:** (1) Synthesize acetanilide. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** Bromine water; 0.1N Hydrochloric acid; Concentrated hydrochloric acid; Acetyl chloride; Glacial acetic acid; Aniline


### \u2705 BUILT + SIMULATED CLEAN 2026-07-18 (8/8 \u00b7 0 mistakes \u00b7 0 bugs \u00b7 0 warnings)

Zone-free, bench-bound; stations DELETED. **The one genuine FUME-HOOD module**: aniline + acetyl chloride are only sanctioned while the vessel sits inside the hood's `WorkVolume` (position-based check \u2014 the old hand-occupancy trigger was never wired and always violated; builder now wires `FumeHoodZone` into every binding). Generator: scratchpad `gen_acetanilide.py`. **VR adaptations (documented):** the manuscript's 0.1N-HCl prep preamble is unused by its own procedure \u2014 the bottle IS used here for the bromination tubes' trace acid (conc HCl there would collide with the hydrolysis heat-gate rule); "wash 3\u00d7 + dry + weigh" is the filter-wash time-skip (weighing stays record-only until Exp 6's weigh verb).

### Task graph (play order \u2014 as built)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `prep-aniline` | ReagentPrep | Measure aniline into the flask \u2014 in the FUME HOOD | - | carry the FlorenceFlask INTO the hood, 2 squeezes aniline there |
| 2 | `add-acetic` | Synthesis | Add glacial acetic acid | 1 | 2 squeezes |
| 3 | `acylate` | Synthesis | Carefully add acetyl chloride; warm at the bath | 2 | 2 squeezes acetyl chloride (in the hood) + warm \u226560 \u00b0C (`heatToC 60`; the Acetanilide rule is heat-gated at 40 \u2192 "White acetanilide plates" + ppt at the bath) |
| 4 | `ice-crystallise` | Synthesis | Add ice-cold water; chill to crystallise | 3 | tilt-pour ~20 ml distilled water + set the flask in the ice bucket (`chillToC 8` \u2014 VesselChillTask now honors the water binding: served AND cold) |
| 5 | `filter-wash` | Synthesis | Filter the crystals; wash and dry | 4 | funnel pour flask \u2192 `Eq_Beaker_100mL` (\u226510 ml Acetanilide) \u2192 **longProcess time-skip** ("washed three times and dried") |
| 6 | `test-hydrolysis` | ChemicalTests | Hydrolysis test \u2014 boil with conc. HCl | 5 | Test Tube 11: 1 scoopula dip of crystals + 3 squeezes conc HCl, boil \u226590 \u00b0C (`Test_AcetanilideHydrolysis`, gated 90 \u2014 crystals reappear) |
| 7 | `test-bromination` | ChemicalTests | Bromination \u2014 acetanilide vs free aniline | 5 | **two-tube rackGroup** (the comparison IS the lesson): Tube 12 = crystals + 5 water + 1 of 0.1N HCl + 5 bromine (yellow); Tube 13 = 2 aniline **(fume hood!)** + 5 water + 1 of 0.1N HCl + 2 bromine (`Test_AnilineBromination` \u2014 instant white tribromoaniline ppt) |
| 8 | `record-yield` | DataSheet | Record % yield and observations | 6,7 | auto-completes (wrap-up) |

### Stage layout (5 bench-bound vessels; stations: [])

- `FlorenceFlask` **ReactionFlask** \u2014 Aniline 2 (t1 \u2713) \u00b7 Glacial Acetic 2 (t2 \u2713) \u00b7 Acetyl Chloride 2 (t3 deferred) \u00b7 Distilled Water 20 (t4 deferred) \u00b7 `heatToC 60` + `chillToC 8`/`chillTaskId ice-crystallise`
- `Eq_Beaker_100mL` **CollectionBeaker** \u2014 Acetanilide \u226510 (t5 \u2713, longProcess) \u2014 becomes the PURE-PRODUCT vessel the tests scoop from
- `Kit_TestTube_11` **HydrolysisTube** \u2014 Acetanilide 2 + Conc HCl 3 (deferred) \u00b7 `heatToC 90`
- `Kit_TestTube_12` / `Kit_TestTube_13` \u2014 the bromination pair, `rackGroup bromination` (task fires only when BOTH tubes are fully served)

### Reactions & expected observations

- **Acetanilide** (min 40 \u00b0C; fixed 2026-07-18 \u2014 resultLiquid was NULL, trapping the product): Aniline + Acetyl Chloride \u2192 Acetanilide + white ppt \u2014 "White acetanilide plates"
- **Test_AcetanilideHydrolysis** (new, min 90 \u00b0C): Acetanilide + Conc HCl \u2192 crystals reappear on cooling/dilution
- **Test_AcetanilideBromination**: Acetanilide + Bromine Water \u2192 yellow solution (slow uptake)
- **Test_AnilineBromination** (new): Aniline + Bromine Water \u2192 instant white **Tribromoaniline** ppt (new chem) \u2014 the free amine takes bromine far faster; the volume contrast (5 vs 2 squeezes) is the recorded result

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Acetanilide is formed by acetylating:**
   - Phenol
   - Aniline [CORRECT]
   - Benzene
   - Toluene
   - *why:* Aniline's -NH2 is acetylated to the amide.
2. **Why must aniline be handled in the fume hood?**
   - It is flammable only
   - It is harmless
   - It is toxic and volatile [CORRECT]
   - It is radioactive
   - *why:* Aniline is toxic and volatile.
3. **Acetanilide contains which functional group?**
   - Amide [CORRECT]
   - Alcohol
   - Ketone
   - Ester
   - *why:* The product is an amide.

## midterm-acetone

**moduleId** `midterm-acetone` | **period** Midterm | **prerequisite** `midterm-acetanilide` | **manuscript** Exp 6 | **end product** Acetone

**Official ILOs:** (1) Synthesize acetone. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** 5% Sodium hypochlorite; Saturated sodium; bisulfite 10% Potassium hydroxide solution Schiff's reagent; Calcium acetate; Sodium acetate; Ethyl alcohol; Tollen's reagent


### ✅ BUILT + SIMULATED CLEAN 2026-07-18 (8/8 · 0 mistakes · 0 bugs · 0 warnings)

Zone-free, bench-bound; stations DELETED (the LAST classic Weigh/Heat/Collect stations went with this pass — the suite's legacy-rig fixtures re-homed: builder fixture → Layout_Chloroform, simrig → a SYNTHETIC in-memory layout, immune to future polish). Generator: scratchpad `gen_acetone.py`.

**New zone-free tools (reusable):** the **bench balance** (`Eq_Balance`: pan trigger + `WeighStation` + live grams TMP display, wired by Apply W5.8; `Vessel.weighTaskId` + `VesselWeighTask` = served AND settled on the pan) · **NAKED FLAME** (`NakedFlameHeat` on every burner: a LIT burner heats vessels within 0.18 m toward 400 °C — the ONE heat source beyond the bath's 100 °C cap; `Vessel.heatTaskId` names the owner explicitly for heat steps with no reagents of their own) · **VAPOR COLLECTION** (`Vessel.vaporTaskId` + `VaporCollectController`: once the task is available and the source is at heatToC, it converts its charge into the module's product, condensing into the nearest vessel whose binding expects it).

**VR adaptations (documented):** the 10% KOH prep preamble is unused by its own procedure (dropped — same class as Exp 5's 0.1N HCl); `setup-distill` folded into the heat/collect hints (the condenser was removed as VR-inappropriate; the receiver-by-the-mouth run IS the setup); Tollen's = the Silver Nitrate bottle (ammoniacal silver nitrate); ⚠ engine wake-rule: the SAMPLE pours FIRST in the iodoform/bisulfite tubes (FindReaction pairs against the first-poured chemical).

### Task graph (play order — as built)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `weigh-acetates` | ReagentPrep | Weigh 7 g of each acetate into the hard-glass tube | - | `Kit_Hard-GlassTestTube_0` on the balance + 4 scoopula dips of EACH acetate (7 g min → 8 g landed, live grams display) → settled on the pan completes |
| 2 | `heat-glow` | Synthesis | Heat the tube over the open flame until the acetates glow | 1 | hold the tube over a LIT burner to 150 °C (`heatTaskId`; open flame, no bath) |
| 3 | `collect-56` | Synthesis | Collect the acetone distillate at 56 °C | 2 | keep `Kit_TestTube_14` by the hot tube's mouth — the vapor condenses ≥8 ml (`vaporTaskId`) |
| 4 | `test-tollens` | ChemicalTests | Tollen's — NO silver mirror (deliberate negative) | 3 | Tube 15: 2 acetone + 5 water + 2 silver nitrate |
| 5 | `test-schiff` | ChemicalTests | Schiff's — no magenta (deliberate negative) | 3 | Tube 16: 5 Schiff's + 2 acetone |
| 6 | `test-iodoform` | ChemicalTests | Iodoform — yellow precipitate | 3 | Tube 17: 2 acetone + 10 KI + 10 hypochlorite, warm ≥60 °C at the bath |
| 7 | `test-bisulfite` | ChemicalTests | Bisulfite adduct — in the freezing mixture | 3 | Tube 18: 2 bisulfite + 1 ethanol (NO adduct with an alcohol — the contrast) + 1 acetone → white `Bisulfite Adduct` ppt (new chem+rule) → chill ≤8 °C in the ice bucket |
| 8 | `record-yield` | DataSheet | Record % yield and observations | 4–7 | auto-completes (wrap-up) |

**All test draws come from the RECEIVER tube** — the sim's pure-product rule now also rejects test tubes whose rule collapsed their ledger back to "Acetone" (the Tollens-residue masquerade caught on round one).

### Reactions & expected observations

- **Test_AcetoneTollens** (negative, authored): Acetone + Silver Nitrate → "No silver mirror — ketone"
- **Test_AcetoneSchiff** (negative, authored): Acetone + Schiff's → "No magenta restored — ketone"
- **Test_AcetoneIodoform** (min 40 °C): Acetone + Hypochlorite → yellow iodoform ppt
- **Test_AcetoneBisulfite** (new 2026-07-18): Acetone + Sodium Bisulfite → white `Bisulfite Adduct` ppt — clearest chilled

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Acetone is prepared by dry distillation of:**
   - Sodium chloride
   - Acetate salts [CORRECT]
   - Sugar
   - Limestone
   - *why:* Calcium/sodium acetate decompose to acetone.
2. **The acetone fraction is collected at about:**
   - 100 C
   - 78 C
   - 56 C [CORRECT]
   - 150 C
   - *why:* Acetone boils at 56 degrees.
3. **Acetone is a ketone, so Tollen's test is:**
   - Negative [CORRECT]
   - Positive
   - Explosive
   - Yellow
   - *why:* Ketones do not reduce Tollen's reagent (no silver mirror).

## midterm-chloroform

**moduleId** `midterm-chloroform` | **period** Midterm | **prerequisite** `midterm-acetone` | **manuscript** Exp 7 | **end product** Chloroform

**Official ILOs:** (1) Synthesize chloroform. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** Concentrated sulfuric acid; Acetone; Potassium; Alcoholic silver nitrate; dichromate Bleaching powder


**VR adaptations (documented):** `reflux-setup` dissolved with the VR-removed condenser (the dropwise addition goes straight into the open flask); the separatory funnel became the bench **Graduated Cylinder 50 mL**; both distillations reuse Exp 6's heat-task + vapor-collect pairing at 65 \u00b0C (chloroform bp 61 \u2014 WATER BATH, never the open flame); the \u00d72 decantation wash is ONE wash water pour + swirl, and the "decant" IS the next step's tilt-pour of the dense bottom layer into the Distilling Flask; amounts are game-scaled (150 g \u2192 5 scoopula dips etc.) with the real quantities kept in the hint fact lines. \u26a0 engine wake-rule: bleaching powder pours FIRST into the flask, and alcoholic AgNO3 FIRST in tube 16 (FindReaction pairs against the first-poured chemical).

### Task graph (play order \u2014 as built, 13 tasks)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `prep-bleach` | ReagentPrep | Dissolve 150 g bleaching powder in 400 mL water (1 L flask) | - | `ErlenmeyerFlask_400mL_2`: 5 scoopula dips bleaching powder FIRST + 20 ml water tilt-pour |
| 2 | `prep-acetone-mix` | ReagentPrep | Mix 12 g acetone + 50 mL water (the dropwise charge) | - | `Eq_GraduatedCylinder_50mL`: 5 ml acetone + 10 ml water |
| 3 | `add-dropwise` | Synthesis | Add the acetone-water mixture drop by drop | 1, 2 | 5 dropper squeezes of acetone into the flask \u2192 `Chloroform` rule PENDS cold, fires \u226540 \u00b0C at the bath |
| 4 | `distil` | Synthesis | Arrange for distillation \u2014 bring the flask to the boil | 3 | flask in the water bath to 65 \u00b0C (`heatTaskId`) |
| 5 | `collect-crude` | Synthesis | Collect the crude chloroform distillate | 4 | hold `Eq_Beaker_100mL` by the hot flask's mouth \u2014 \u22658 ml condense (`vaporTaskId`) |
| 6 | `decant-wash` | Synthesis | Wash the chloroform by decantation (\u00d72) | 5 | 4 squeezes of distilled water into the crude beaker + swirl |
| 7 | `dry-cacl2` | Synthesis | Dry the washed chloroform over anhydrous CaCl2 | 6 | decant 8 ml chloroform into `DistillingFlask` + 1 scoopula CaCl2, warm to 65 \u00b0C (deferred binding + `heatToC`) |
| 8 | `dry-redistil` | Synthesis | Redistil until the liquid runs clear | 7 | keep the flask hot; `Kit_TestTube_14` at the mouth collects \u22656 ml clear (`vaporTaskId`) |
| 9 | `weigh-product` | Synthesis | Weigh your chloroform distillate | 8 | Tube 14 settled on the bench balance (`weighTaskId`, no pour steps of its own) |
| 10 | `test-flammability` | ChemicalTests | Non-flammability test (lit match) | 9 | 2 ml chloroform onto `Eq_WatchGlass`, then a LIT match/burner flame held to it (`flameTaskId` \u2192 **VesselFlameTask**, new reusable) \u2014 "Won't ignite \u2713" |
| 11 | `test-oxidation` | ChemicalTests | Oxidation \u2014 dichromate + conc. H2SO4 (fume hood) | 9 | Tube 15 IN THE HOOD: 1 ml chloroform + 0.4 g dichromate (hood-gated reagent) + 2 squeezes H2SO4, warm \u226560 \u00b0C at the bath |
| 12 | `test-agno3` | ChemicalTests | Alcoholic AgNO3 \u2014 nothing cold, white AgCl warm | 9 | Tube 16: 1 squeeze alcoholic AgNO3 FIRST + 1 ml chloroform \u2192 pends cold, white AgCl ppt \u226550 \u00b0C at the bath |
| 13 | `record-yield` | DataSheet | Record % yield and observations | 9\u201312 | auto-completes (wrap-up) |

**All test draws come from Test Tube 14** (the pure-product receiver). The wash beaker is the sim's new **washed-product source class** (product + Distilled Water only in the ledger = decantable; any other foreign entry marks test residue).

### Reactions & expected observations

- **Chloroform** (min 40 \u00b0C): Acetone + Bleaching Powder \u2192 Chloroform \u2014 "Dense, sweet, non-flammable chloroform"
- **Test_ChloroformOxidation** (min 60 \u00b0C, fixed 2026-07-18 \u2014 resultLiquid was NULL, the product trap, and it fired cold): Chloroform + Potassium Dichromate \u2192 Chloroform \u2014 "Pungent, suffocating odour (phosgene-like)". Potassium Dichromate is `requiresFumeHood` (only Exp 7 uses it).
- **Test_ChloroformAgNO3** (min 50 \u00b0C, fixed 2026-07-18 \u2014 claimed a precipitate with resultPrecipitate NULL, and paired PLAIN silver nitrate): Chloroform + **Alcoholic Silver Nitrate** \u2192 white **Silver Chloride** ppt (new chem) \u2014 "Nothing in the cold - on warming in the water bath a white precipitate (AgCl) separates."
- **Test_AcetoneTollens** stays authored for Exp 6 (Acetone + Silver Nitrate negative).

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Chloroform is made by reacting bleaching powder with:**
   - Ethanol
   - Acetone [CORRECT]
   - Benzene
   - Phenol
   - *why:* The haloform reaction uses acetone.
2. **A distinctive physical property of chloroform is that it is:**
   - Highly flammable
   - A gas at room temperature
   - Non-flammable and dense [CORRECT]
   - Water-soluble in all ratios
   - *why:* Chloroform is dense and non-flammable.
3. **The acetone-water mix is added to the flask:**
   - Drop by drop [CORRECT]
   - All at once
   - After distillation
   - Frozen
   - *why:* Dropwise, to control the exothermic reaction.

## final-benzamide

**moduleId** `final-benzamide` | **period** Final | **prerequisite** `midterm-chloroform` | **manuscript** Exp 8 | **end product** Benzamide

**Official ILOs:** (1) Synthesize benzamide. (2) Determine its identity through chemical tests.

**Manuscript Equipment & Apparatus:** S-clamp; Aspirator; Stirring rod; Beaker; Test tubes; Condenser; Test tube brush; Cork; Test tube holder; Distilling flask; Thermometer; Erlenmeyer flask; Iron clamp; Iron stand; Watch glass; Pipette; Water; Porcelain spatula; bath Rubber tubing

**Manuscript Reagents:** Concentrated; 10% Sodium hydroxide; Diluted hydrochloric; ammonia 10% Sodium nitrate solution; acid Benzoyl chloride


**VR adaptations (documented):** the old "measure in the fume hood" hint was game-invented \u2014 the manuscript never asks for the hood in Exp 8 (the ICE BATH is its safety teaching), so no hood step; the **Beaker 100 mL stands in for the 50-ml flask** (5+1 ml stays a visible 6% fill); "shake the flask frequently" plays as the **glass-rod STIR verb** (either bench rod \u2014 the controller tracks whichever rod tip is nearest, anchor-free); the stand + filter/wash/oven-dry compress into two time-skips; a **weigh step** was added (the data sheet's % yield needs the crystal mass, Exp 6/7 precedent). \u26a0 engine wake-rule: the benzamide dips pour FIRST in every test tube.

### Task graph (play order \u2014 as built, 10 tasks)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `measure-ammonia` | ReagentPrep | Place 5 mL concentrated ammonia in the flask | - | `Eq_Beaker_100mL`: 5 squeezes of ammonia solution |
| 2 | `ice-bath` | ReagentPrep | Cool the flask in an ice bath | 1 | beaker in the ice bucket to \u22645 \u00b0C (`chillTaskId`) |
| 3 | `add-benzoyl` | Synthesis | Add 1 mL benzoyl chloride drop by drop | 2 | 1 dropper squeeze into the CHILLED beaker \u2192 `Benzamide` rule fires cold (white solid, no gate \u2014 the ice is the control) |
| 4 | `stir-stand` | Synthesis | Shake while adding, then let stand 10\u201315 min | 3 | circle a GLASS ROD inside the beaker (`stirTaskId` \u2192 **StirController**, tip-tracked, either bench rod) \u2192 "Well stirred!" \u2192 time-skip |
| 5 | `filter-dry` | Synthesis | Filter, wash with cold water, oven-dry | 4 | tilt-pour 4 ml through the funnel onto `Eq_WatchGlass` \u2192 wash/oven time-skip |
| 6 | `weigh-product` | Synthesis | Weigh your benzamide crystals | 5 | watch glass settled on the bench balance (`weighTaskId`) |
| 7 | `test-alkali` | ChemicalTests | Alkaline hydrolysis \u2014 red litmus turns BLUE | 6 | Tube 15: 5 dips benzamide + 2 squeezes NaOH 10% \u2192 warm \u226590 \u00b0C at the bath (ammonia gas \u2697) + litmus strip reads BASE (`litmusTaskId`) |
| 8 | `test-acid` | ChemicalTests | Acid hydrolysis \u2014 the tube reads ACID | 6 | Tube 16: 5 dips + 2 squeezes dil. HCl \u2192 warm \u226590 \u00b0C (benzoic crystals \u2697) + litmus reads ACID |
| 9 | `test-nitrous` | ChemicalTests | Nitrous-acid test \u2014 brisk N2 effervescence | 6 | Tube 17: 5 dips + 2 squeezes dil. HCl + 3 squeezes sodium nitrite \u2192 instant effervescence \u2697 (completes on delivery) |
| 10 | `record-yield` | DataSheet | Record % yield and observations | 6\u20139 | auto-completes (wrap-up) |

**All test dips come from the Watch Glass** (the filtered, dried pure-product dish). **Sodium Nitrite was MISSING from the bench entirely** \u2014 absent from `RawReagentCatalog`, so no bottle existed; added 2026-07-18 as the manuscript's 10% SOLUTION (liquid, `Raw_SodiumNitrite`, cabinet rebuilt + recovery chain run).

### Reactions & expected observations

- **Benzamide** (no gate \u2014 fires IN the ice; fixed 2026-07-18: resultLiquid was NULL, so the beaker's contents stayed "Ammonia Solution" and nothing downstream could pour or find the product): Benzoyl Chloride + Ammonia Solution \u2192 Benzamide (+ white ppt) \u2014 "White benzamide solid"
- **Test_BenzamideAlkali** (min 90 \u00b0C "heat to boiling", was 40; inputB fixed \u2192 **Sodium Hydroxide 10%** \u2014 plain "Sodium Hydroxide" has NO bench bottle): \u2192 "Ammonia gas evolved on boiling \u2014 turns red litmus blue"
- **Test_BenzamideAcid** (min 90 \u00b0C, was 40; resultLiquid NULL fixed \u2192 Benzamide): + Diluted HCl \u2192 white benzoic ppt \u2014 "White benzoic acid crystals separate on boiling"
- **Test_BenzamideNitrous** (instant, cold): + Sodium Nitrite \u2192 "Brisk effervescence \u2014 nitrogen gas". \u26a0 Engine: a FIRED reaction now CLEARS any pending rule (`LiquidPhysics.ApplyReaction`) \u2014 without this, tube 17's half-armed acid-hydrolysis pend outlived the effervescence and kept showing "needs heat".

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **Benzamide is prepared from ammonia and:**
   - Benzoic acid
   - Benzoyl chloride [CORRECT]
   - Benzaldehyde
   - Aniline
   - *why:* Benzoyl chloride reacts with ammonia to give benzamide.
2. **Why is the reaction run in an ice bath?**
   - To freeze the product
   - To speed it up
   - To control the vigorous reaction [CORRECT]
   - To colour it
   - *why:* Benzoyl chloride reacts vigorously; cooling controls it.
3. **Alkaline hydrolysis of benzamide releases which gas?**
   - Ammonia [CORRECT]
   - Hydrogen
   - Chlorine
   - Oxygen
   - *why:* Boiling with NaOH liberates ammonia.

## final-winemaking

**moduleId** `final-winemaking` | **period** Final | **prerequisite** `final-benzamide` | **manuscript** Exp 9 (real-world group activity; grapes EXCLUDED) | **end product** Wine

**Official ILOs:** (1) Learn the basic methodology in preparation and synthesis of alcohol using the fermentation technique with basic household ingredients.


### Task graph (play order)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
| # | task | phase | label | prerequisites | hint |
|---|------|-------|-------|---------------|------|
| 1 | `prepare-must` | ReagentPrep | Prepare the must - fruit juice (NOT grapes) + sugar + yeast | - | The manual: ferment at least 250 mL of any fruit juice EXCEPT grapes. Sugar feeds the yeast; a pinch of nutrient keeps it healthy. → Fermentation Jar: tilt-pour Mixed Fruit Juice to the 40 ml band, 3 scoopula dips of brown sugar, 1 scoopula dip of ammonium phosphate, then pitch 2 scoopula dips of yeast - watch the tag turn to 'Wine'. |
| 2 | `stir-must` | ReagentPrep | Swirl the must to rouse the yeast | `prepare-must` | Rousing spreads the yeast evenly through the sugary juice so every part ferments. → Circle the GLASS ROD in the jar until the 'Well stirred!' cue shows. |
| 3 | `confirm-ferment` | Synthesis | Confirm fermentation - CO2 turns limewater milky | `stir-must` | Fermenting wine gives off CO2; leading that gas through limewater proves it is alive - the limewater turns milky (CaCO3). → Pour 10 ml of limewater into a clean test tube and hold it at the jar's mouth until it clouds - then the week-long ferment passes. |
| 4 | `rack` | Synthesis | Rack the clear wine off the lees | `confirm-ferment` | Racking siphons the clear wine off the sediment (lees) without disturbing it - the manual: 'do not shake; decant the liquid'. → Tilt-pour the clear wine from the jar THROUGH the racking funnel into the Wine Bottle (Florence flask) to the 30 ml band, leaving the lees behind. |
| 5 | `test-clarity` | ChemicalTests | Assess clarity, colour and appearance | `rack` | General appearance is a fifth of the wine-tasting grade - a bright, clear pour with no haze scores well. → Pour a splash (about 6 ml) of your racked wine onto the Watch Glass and judge its clarity against the light. |
| 6 | `test-tasting` | ChemicalTests | Tasting finale - evaluate aroma and flavour | `rack` | Flavour, smell and taste are a quarter of the grade - the tasting finale of the whole course. → Pour about 12 ml into the Tasting Glass and evaluate the aroma and flavour. |
| 7 | `record-label` | DataSheet | Create the label & record volume and observations | `test-clarity`, `test-tasting` | Design the bottle label and log the final volume (250-500 mL) with your appearance and tasting notes. → Review your wine, then answer on the data sheet. |
**VR adaptations (documented):** the manuscript's Exp 9 is a **group ACTIVITY with no bench procedure** (guidelines: ferment \u2265250 mL of any non-grape fruit juice, confirm, rack, taste, label) \u2014 so the chemistry is game-authored within it. The **jar START-filled with juice** (unplayable \u2014 no bench bottle to pour from, the Exp 3 hidden-shorthand class); it now starts EMPTY and the player pours **Mixed Fruit Juice** from the new `Raw_MixedFruitJuice` bottle. **Wine was never actually produced** (the jar only ever held juice \u2192 racking/tasting drew JUICE); new `WineFermentation` rule makes it. The old zone-touch stations (seal/ferment/rack/clarity/taste) are gone; the airlock is folded into the CO\u2082-confirm hint.

### Task graph (play order \u2014 as built, 7 tasks)

| # | task | phase | label | prereq | how it completes |
|---|------|-------|-------|--------|------------------|
| 1 | `prepare-must` | ReagentPrep | Prepare the must \u2014 fruit juice (NOT grapes) + sugar + yeast | - | `Eq_Beaker_500mL`: 40 ml Mixed Fruit Juice + 3 dips brown sugar + 1 dip ammonium phosphate + 2 dips yeast \u2192 **`WineFermentation` fires** (juice + yeast \u2192 Wine, CO\u2082); the tag turns to "Wine" |
| 2 | `stir-must` | ReagentPrep | Swirl the must to rouse the yeast | 1 | circle a GLASS ROD in the jar (`stirTaskId` \u2192 StirController) |
| 3 | `confirm-ferment` | Synthesis | Confirm fermentation \u2014 CO\u2082 turns limewater milky | 2 | pour 10 ml Limewater into Test Tube 5 by the jar's mouth \u2192 CO\u2082 bubbles in (`FermentationController`) \u2192 CaCO\u2083 milky \u2697 + week time-skip. **No "seal the airlock" action** \u2014 the airlock is a concept in the fact line; the verb is the limewater test |
| 4 | `rack` | Synthesis | Rack the clear wine off the lees | 3 | tilt-pour ~30 ml Wine from the jar (fermentation-wash source) through the funnel into the FlorenceFlask (Wine Bottle) |
| 5 | `test-clarity` | ChemicalTests | Assess clarity, colour, appearance | 4 | pour ~6 ml of the RACKED wine onto `Eq_WatchGlass` |
| 6 | `test-tasting` | ChemicalTests | Tasting finale \u2014 aroma & flavour | 4 | pour ~12 ml into `Eq_Beaker_100mL` (tasting glass) |
| 7 | `record-label` | DataSheet | Create the label & record volume + observations | 5, 6 | auto-completes (wrap-up) |

**Clarity + tasting draw from the RACKED FlorenceFlask** (the clear wine off the lees = a pure-product source; the jar with its sediment is the lower-priority fermentation-wash). Uses every apparatus: jar, glass rod, limewater tube, racking funnel, wine bottle, watch glass, tasting glass. **Volumes were scaled UP from the first pass** (juice 20→40, rack 8→30, clarity 2→6, tasting 3→12) — the wine bottle/tasting glass were poured so thin (3% of capacity) they read as EMPTY in VR; every pour now clears the visibility floor.

### Reactions & expected observations

- **WineFermentation** (new 2026-07-19, no gate): Mixed Fruit Juice + Yeast \u2192 **Wine** (CO\u2082 evolves) \u2014 "Yeast pitched into the juice \u2014 fermentation begins, CO\u2082 bubbles up and the must turns to young wine." Brown sugar + ammonium phosphate accumulate as nutrients (sanctioned, no mistake).
- **Limewater_CO2**: Limewater + Carbon Dioxide \u2192 milky CaCO\u2083 ppt \u2014 "Limewater turns milky" (the FermentationController bubbles the jar's CO\u2082 into the tube; the tube is no longer pre-seeded).

### Quiz (Documentation score)

<!-- generated by Tools/gen-vault-reference.py from the live assets; do not hand-edit -->
1. **In winemaking, yeast converts sugar into ethanol and:**
   - Oxygen
   - Carbon dioxide [CORRECT]
   - Methane
   - Water only
   - *why:* Fermentation releases CO2.
2. **Racking the wine means:**
   - Adding more sugar to restart fermentation
   - Decanting the clear wine off the sediment (lees) [CORRECT]
   - Shaking the jar to mix the yeast back in
   - Boiling the wine to concentrate it
   - *why:* Racking decants the clear wine off the lees without disturbing the sediment.
3. **Evolved gas turning limewater milky confirms:**
   - Carbon dioxide is being produced [CORRECT]
   - The wine is finished
   - Oxygen is present
   - The yeast is dead
   - *why:* Milky limewater confirms carbon dioxide from fermentation.
