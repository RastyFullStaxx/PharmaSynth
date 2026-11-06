---
tags: [plan, cutscenes]
status: phase-3-done
---
# Cinematic cutscenes — plan

**Goal.** Cutscenes play like a game's cinematic: the screen fades black, the player watches a
camera-directed shot sequence of the real lab (Pharmee pops out, close-up, pan to the bench and the
player's own product), then fades black and returns to normal play. Source: the client's
`handoff_assets/cutscenes-sample.pdf` (3 sequences: preparations complete, experiment complete,
wine fast-forward). Tested in the editor without a headset; the headset pass comes after.

**Approach: cinema view.** The XR camera is never animated. A separate `CutsceneCamera` films the
live scene into a RenderTexture; during a cutscene the player's XR camera renders only a dark
`Cinema` layer holding a wide letterboxed screen showing that texture. The screen is world-fixed in
front of the player's head at cutscene start (comfortable; nothing else to look at, so the shot
can't be missed). Hands, HUD and the lab vanish with the culling-mask swap; input is locked.

## What exists and is reused
- `CutsceneDirector` + 36 `CutsceneData` assets (Intro / ReagentPrep / Success / Failure ×9) —
  triggers, subtitles, Pharmee faces stay; beats gain an optional shot.
- `ScreenFader.FadeOut/FadeIn/FadeAround` — every fade.
- `NPCNarrationController` + `HudDialogueBar` — subtitles (rendered on the cinema layer during a cutscene).
- `TimeSkipController` — the 6 `longProcess` tasks (Exp 3, 4, 5, 8, 9) already fade + hold black; the
  timelapse replaces the hold.
- `PharmeeGatekeeper.WaitOutroThenRemarks` — the review already waits for the outro to end.
- `ChoicePanel` — the "Preparations Complete / Start" and "Experiment Complete / Okay" cards.

## Phase 1 — the cinema player (engine)
1. **`ShotMath`** (pure, suite-tested): evaluate a shot list at time `t` → camera position, rotation,
   FOV. A shot = from-pose → to-pose, duration, ease (smoothstep), `cut` or `blend` from the previous
   shot, look-at target (position) overriding rotation when set. No pitch/roll beyond what look-at gives.
2. **`CutsceneCamera`** (thin MonoBehaviour): a plain `Camera`, disabled when idle, renders into a
   1280×544 RT (2.35:1 letterbox) — Quest-cheap. Plays a shot list via `ShotMath`.
3. **`CinemaView`**: on enter, store + swap the XR camera's culling mask to `Cinema` and clear to black,
   place the screen quad 2.5 m ahead of the head (yaw only), disable interactors + locomotion; on exit,
   restore all of it. Enter/exit always wrapped in `ScreenFader.FadeAround`. Pure `CinemaState`
   records what was changed so exit restores exactly that (suite: restore after enter, after Skip,
   after an abort mid-cutscene).
4. **Clock pause**: `ExperimentRunner.PauseClock(bool)` (today only a one-way `FreezeClock` exists);
   a mid-run cutscene (ReagentPrep, timelapse) never counts against the time score.
5. **Director wiring**: `PlayRoutine` enters the cinema when the cutscene has any shot, runs beats and
   shots on one timeline, exits on the last beat or `Skip()`. A cutscene with no shots plays exactly
   as today (so the 36 assets keep working while shots are authored).
6. **Shot data**: `CutsceneData.Beat` gains `shot` (camera marker name, look-at target enum
   `Pharmee | Bench | Product | Marker`, blend/cut, seconds). Markers are empty transforms under a
   scene root `CutsceneMarkers`, created by an idempotent menu **`Build Cutscene Markers`**
   (preserves hand-moved markers, like Re-Home Adopt Current).
7. **Pharmee pop-in**: a beat flag `pharmeePopIn` moves Pharmee to `Marker_PharmeeCorner` and plays a
   scale-up + bob (reuse `PharmeeGestures`), then returns her to her post at exit.
8. **Product target**: resolve the running module's product vessel from its layout (the vessel bound
   to the final product task). Verify per module in the suite — the shot must point at something real.

## Phase 2 — the three slide sequences
| Sequence | Trigger | Shots |
|---|---|---|
| Preparations complete | ReagentPrep phase completes | card "Preparations Complete / Start" → fade → wide of the lab, Pharmee pops out of the corner → push to Pharmee close-up, commend line → slow pan through the lab to the main bench, objective subtitle → fade back to play |
| Experiment complete | Success outro (`ExperimentFinished`, passed) | card "Experiment Complete / Okay" → fade → Pharmee pops out → close-up commend → pan to the bench, hold on the player's product → fade → grade screen |
| Failure | Failure outro | same template, encourage line, pan to the bench (no product hold) |

- **Grade screen opens after the outro ends** (today they run in parallel), matching the slides'
  "Mission Complete" after the cutscene. The camera can film the bench even though the player is at the
  review corner — the lab isn't reset until Continue.
- Author shots for all 9 modules' ReagentPrep / Success / Failure from one template (data, through a
  menu `Apply Cutscene Shots`, not hand-edits). Intro stays subtitle-only in this batch; it gets
  shots once the template is approved.
- Copy: the slide text is placeholder (it reuses "prepared the reagent" for experiment complete); keep
  our authored lines.

## Phase 3 — timelapse (all 6 long-process tasks)
On a `longProcess` task: fade black → cinema view from **`Marker_Timelapse`, high in the lab corner by
`LabSpeaker`** (security-camera angle), looking at the task's vessel → 6 s timelapse → fade back to play.
- **`TimelapseMath`** (pure): progress 0–1 → day/night phase, screen tint (day white → night blue),
  sun/moon icon, counter ("Day 3 / 7", or "15 min" for short waits), slow push-in.
- Day/night is a tint on the cinema screen material, not the lab lights — the lab is lightmap-baked,
  so dimming realtime lights would do nothing and re-baking is not on the table.
- The vessel's visible change (e.g. juice → wine colour) lerps during the timelapse where the task
  already defines an end state; nothing new is invented per module.
- Task data gains `timelapseDays` / `timelapseMinutes` (one field set per task: wine 7 days, ethyl
  1 week, benzamide 15 min, etc.). The `longProcessMessage` still shows when play returns.
- Wine then flows into its Success outro, showing the wine on the bench (slide 3).

## Testing without a headset
- **Suite** (`cine:` assertions): `ShotMath` endpoints/ease/cut; `TimelapseMath` phases and counter;
  `CinemaState` restore after enter/skip/abort; clock paused during a cutscene; every authored shot's
  marker and look-at target **resolves to a real scene object** (pins the scene, not just the math —
  the Gotchas lesson); every module's product target resolves.
- **`Render Cutscene Shots ▸ <module>`** (edit mode): renders the start/middle/end frame of every shot
  from the `CutsceneCamera` to `Logs/cutscene-shots/<module>/*.png` — framing review without Play
  mode. One contact sheet per sequence for you to check.
- **`Preview Cutscene ▸ <sequence>`** (Play mode, Headset Play Mode OFF): plays it in the Game view
  with fades, subtitles and the return to play — you watch it on the monitor.
- **`Simulate Everything` + `Simulate Campaign`** stay CLEAN (cutscenes must not stall the flow;
  `Skip()` path covered).
- Reload SampleScene from disk after any simulator/suite run before saving (W5.59 lesson).

## Docs in the same change
CLAUDE.md ("never animate the XR camera" → cutscenes use `CutsceneCamera` + cinema view; XR camera
still never animated), `gameplay-flow.md` §8, `systems-reference.md` (cinema view, shot data, timelapse),
`remaining-work-checklist.md` (replace the "cutscene staging polish" item), changelog one line,
`gen-vault-reference.py` for new types/menus.

## Open points for the headset pass (later)
Screen distance/size, timelapse length, whether the pan speed is comfortable, whether Intro gets shots.
