# PharmaSynth — Vault Home

> [!abstract] What this is
> **PharmaSynth: "Gear Up, Synth It Up!"** — a first-person guided VR chemistry-lab
> education game for **Meta Quest 3**. Unity 6000.5.2f1, URP 17.5, OpenXR 1.17.1 +
> XRI 3.5.1. Client handoff (audit-and-continue). **Hard contract deadline: 2026-08-31.**

This vault **is** the `Docs/` folder of the repo. There is no separate copy — every
note here is a real file tracked in git, and the code it describes sits one directory
up. That is deliberate: this project has lost days to documentation that drifted from
the thing it described, so there is exactly one place a fact may live.

---

## Start here

| If you are… | Read |
|---|---|
| New to the project | This page, then [[Architecture MOC]] |
| New to the code | [[DEVELOPER]] |
| Working on an experiment's chemistry or steps | [[experiments-reference]] |
| Working on a mechanic (liquids, verbs, NPCs, UI) | [[systems-reference]] via [[Systems MOC]] |
| Working on flow, grading, or the door gate | [[gameplay-flow]] |
| Running, building, or testing | [[Build and Test Loop]] |
| About to run a builder menu | [[Editor Menus]] — **and [[Gotchas]] first** |
| Looking for a class or what it does | [[Class Index]] |
| Wondering what's left | [[remaining-work-checklist]] |
| Confused by a term | [[Glossary]] |

---

## The maps

- **[[Architecture MOC]]** — how the codebase is shaped and why
- **[[Systems MOC]]** — every runtime mechanic, and where it is documented
- **[[Content MOC]]** — the 9 experiments, the manuscript, quizzes, assets
- **[[Process MOC]]** — how work gets done here: testing, builders, docs, git

---

## The one-paragraph version of the game

Boot into a **cube spawn room** (Laboratory / Tutorial / Settings / Quit). Fade into
the lab at the front door; **Pharmee**, a robot NPC, greets you and guards the lab
door. Choose a **period** (Prelim / Midterm / Finals), then a **module** within it.
Don a lab coat, goggles and gloves at the locker. Say "I'm ready", the stage builds,
the door opens — and **crossing the threshold starts the clock**. You run the real
procedure: pour reagents, grind, weigh, heat, stir, filter, test. Mistakes are graded
and reagents are finite. When the chemical tests are done the clock freezes and you
are teleported to the **review corner**, where **Dr. Jimenez** briefs you and a
**quiz tablet** asks 3 questions. Submit, get a **grade screen** and an outro
cutscene. Pass both gates — **rubric ≥90% AND BKT mastery ≥0.90** — and the next
module unlocks. Fail and you retry or pick another.

Full detail: [[gameplay-flow]].

---

## The 9 experiments

| # | moduleId | Period | Product | Signature verbs |
|---|---|---|---|---|
| 1 | `tutorial-methane` | Tutorial | (gas; splint pop) | grind, heat, collect, splint |
| 2 | `prelim-chemical-compounding` | Prelim | — (ID lab) | dropper counts, tilt-pour, water bath |
| 3 | `prelim-ethyl-alcohol` | Prelim | Ethanol | ferment, distill, iodoform/ester |
| 4 | `midterm-benzoic-acid` | Midterm | Benzoic Acid | oxidise, filter, acidify, ester |
| 5 | `midterm-acetanilide` | Midterm | Acetanilide | acylate, crystallise |
| 6 | `midterm-acetone` | Midterm | Acetone | weigh, dry-distill, 4 tests |
| 7 | `midterm-chloroform` | Midterm | Chloroform | haloform, decant, oxidation |
| 8 | `final-benzamide` | Final | Benzamide | ice bath, stir, nitrous |
| 9 | `final-winemaking` | Final | Wine | ferment juice, CO₂/limewater |

The **Tutorial** methane experiment sits outside the graded periods. Per-experiment
data — reagents, steps, tests, quiz, layout, apparatus — is in [[experiments-reference]].
See [[Content MOC]] for the manuscript relationship.

---

## Three rules that outrank convenience

> [!danger] 1. The bench already exists
> A layout must **never** stage general apparatus or a reagent bottle. Every tool and
> all 57 raw reagents are permanently in the scene. Vessels *bind* to what is already
> there via `Vessel.benchItem`. Ignoring this once duplicated 46 objects. → [[Gotchas]]

> [!danger] 2. Everything is out, always
> A real lab keeps every instrument on the bench. Never hide, remove, or
> per-experiment-gate apparatus or reagents to "reduce clutter" — the player is meant
> to *choose* the right tool. Reduce confusion with labels and hints instead.
> Three narrow exceptions only. → [[Gotchas]]

> [!danger] 3. Chemistry comes from the manuscript
> The client manuscript's Appendix C is the only chemistry authority. The storyboard
> is a reference to *exceed*, never a source. → [[Content MOC]]

---

## Working in this vault

Every session is expected to **read what it needs before acting, and write back what
it learned**. That protocol is not optional and lives in [[Working Agreement]].

Derived notes — [[Class Index]], [[Editor Menus]], [[Scene Objects]] — are generated
by `python Tools/gen-vault-reference.py` and must never be hand-edited. Everything
else is hand-written and *is* the source of truth.
