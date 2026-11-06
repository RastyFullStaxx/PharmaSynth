# PharmaSynth

**Gear Up, Synth It Up!** A guided virtual-reality chemistry lab for Meta Quest 3, built in Unity.

Students put on a lab coat, goggles and gloves, walk into a fully stocked laboratory, and carry out
real organic-chemistry procedures by hand: pouring, weighing, grinding, heating, stirring, filtering
and testing. Every tool and every reagent is always out on the bench, so choosing the right ones is
part of the lesson. A robot lab guide, Pharmee, explains each experiment; a proctor, Dr. Jimenez,
reviews the results; and each run is graded on procedure, safety, time and a short quiz.

![The PharmaSynth laboratory](Docs/media/lab.jpg)

## The experiments

The chemistry follows the course manuscript's laboratory manual (Appendix C), grouped into the
course's three periods. A methane synthesis serves as the tutorial.

| # | Experiment | Period | What the player makes or tests |
| --- | --- | --- | --- |
| 1 | Methane | Tutorial | methane gas, confirmed with a splint |
| 2 | Chemical Compounding | Prelim | identification tests with droppers and a water bath |
| 3 | Ethyl Alcohol | Prelim | ethanol by fermentation and distillation |
| 4 | Benzoic Acid | Midterm | benzoic acid by oxidation, filtering and acidifying |
| 5 | Acetanilide | Midterm | acetanilide by acylation and crystallisation |
| 6 | Acetone | Midterm | acetone by dry distillation, with four tests |
| 7 | Chloroform | Midterm | chloroform by the haloform reaction |
| 8 | Benzamide | Finals | benzamide in an ice bath, stirred by hand |
| 9 | Wine Making | Finals | wine fermented from mixed fruit juice |

To pass an experiment, a student needs a grade of at least 90% **and** a mastery estimate of at
least 0.90 for the skills it trains. Passing unlocks the next experiment in the chain. A separate
Tutorial Mode opens every experiment for ungraded practice, with step-by-step guidance and
unlimited reagents.

## How a run plays

1. Start in the spawn room and choose **Laboratory** or **Tutorial**.
2. Meet Pharmee at the lab door and pick a period, then an experiment.
3. Put on the protective equipment at the locker. Crossing the lab threshold starts the clock.
4. Follow the procedure on the wrist-watch checklist. Any matching glassware works for a step, and
   mistakes are graded, not fatal.
5. When the tests are done, the clock stops and the review corner opens: a briefing, a three-question
   quiz and the grade screen, with a short cutscene for a pass or a retry.

## Requirements

| | |
| --- | --- |
| Unity | **6000.5.2f1** exactly (in Unity Hub it is listed under Archive) |
| Render pipeline | URP 17.5 |
| XR | OpenXR 1.17.1, XR Interaction Toolkit 3.5.1, Unity OpenXR: Meta 2.5.0 |
| Editor modules | Android Build Support (OpenJDK, SDK and NDK); the active build target is Android |
| Git | Git LFS: art, audio and documents are stored in LFS |
| Headset | Meta Quest 3, optional for development (see below) |

All packages are pinned in `Packages/manifest.json`; a fresh clone needs no package setup.

## Getting started

```bash
git lfs install
git clone https://github.com/RastyFullStaxx/PharmaSynth.git
```

Add the folder in Unity Hub (**Add project from disk**) and open it with 6000.5.2f1. The first open
builds the Library folder, which takes a while.

**Without a headset** (the default): open `Assets/Scenes/MainMenu.unity` and press Play. The XR
Device Simulator stands in for the headset: hold the right mouse button to look, use W/A/S/D to
move, and press G to grab with the selected controller. The full control list is in
[`Docs/build-and-run.md`](Docs/build-and-run.md).

**With a Quest 3 over Link**: turn on **Tools ▸ PharmaSynth ▸ Headset Play Mode (OpenXR on Play)**,
connect the headset, then press Play.

## Checking a change

The project carries its own test tools under **Tools ▸ PharmaSynth**, and none of them need a
headset:

| Tool | What it checks | Result file |
| --- | --- | --- |
| **Run Self-Tests** | about 1,700 assertions over the game's rules and the lab scene | `Logs/selftest-result.txt` |
| **Simulate Everything** | plays all nine experiments, the campaign loop and deliberately imperfect play in edit mode | `Logs/simulate-everything.txt` |
| **Autopilot Playtest** | drives the real game loop in Play mode for about eight minutes and screenshots each step | `Logs/autopilot-report.txt` |

The suite and the simulators run experiments on the open scene. Reopen `SampleScene` from disk
afterwards and never save the scene straight after a run.

## Building for Quest 3

The project is already configured for Android: Vulkan with an OpenGL ES 3 fallback, IL2CPP on
ARM64, ASTC textures, and the Meta Quest OpenXR features. Build an APK from **File ▸ Build Profiles**
and install it with `adb install -r PharmaSynth.apk`. Builds are signed with Unity's debug keystore
until a release keystore is added. Details in [`Docs/build-and-run.md`](Docs/build-and-run.md).

## Project layout

```
Assets/PharmaSynth/
  Scripts/             all game code: Experiment, Scoring, Progression, Chemistry,
                       Interaction, NPC, UI, Safety, Audio, Editor
  ScriptableObjects/   the experiments, their bench layouts, quizzes and chemicals
  Art/, Audio/         models, materials, textures, voice and music
Assets/Scenes/         MainMenu (spawn room) and SampleScene (the lab)
Docs/                  the project's documentation, readable as an Obsidian vault
Tools/                 helper scripts: doc generation, manuscript extraction, voice
                       generation, the cutscene reel
```

Experiments are data, not scenes: one lab scene builds whichever experiment is chosen from its
definition asset. The code keeps each MonoBehaviour thin over plain C# rules that the self-tests
call directly.

## Documentation

- [`Docs/DEVELOPER.md`](Docs/DEVELOPER.md): start here to work on the code.
- [`Docs/Home.md`](Docs/Home.md): the map of every document.
- [`Docs/experiments-reference.md`](Docs/experiments-reference.md): each experiment's steps,
  reagents, apparatus and tests.
- [`Docs/systems-reference.md`](Docs/systems-reference.md): how each mechanic works.
- [`Docs/gameplay-flow.md`](Docs/gameplay-flow.md): the full game flow, grading and restarts.
