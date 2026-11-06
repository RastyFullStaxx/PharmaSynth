# Process MOC

Up: [[Home]] · Siblings: [[Architecture MOC]] · [[Systems MOC]] · [[Content MOC]]

How work actually gets done in this project.

---

## Every session

1. **[[Working Agreement]]** — the vault protocol. Read before acting, write back
   what you learned. Not optional.
2. `git log --oneline -3` on **main**. The user commits checkpoints themselves.
   `feature/asset-intake` is a dead stub — never use it.
3. Run the self-test suite → [[Build and Test Loop]].
4. Open work lives in ONE tracker: [[remaining-work-checklist]] (§13 = the user's
   queued playtest issues). Tick items off as they land.

---

## Testing

→ [[Build and Test Loop]] for the full procedure, the MCP-down fallbacks, and how to
tell a real failure from an artefact of the wrong scene being open.

Headline rules:

- **Edit mode only.** In Play mode ~7 `isPlaying`-gated assertions legitimately fail.
- **Open SampleScene.** Scene-pinned assertions (`bench:`, `match:`, `wired:`,
  `verbwire:`, `simrun:`) fail *en masse* if MainMenu is the open scene. That is not
  a regression.
- **Test at phase boundaries**, not per micro-edit. Once after a coherent batch
  compiles, and always before ending a turn that changed code or data.

---

## Builders

All scene edits go through **idempotent, re-runnable menu items**. There are ~108 of
them → [[Editor Menus]].

> [!danger] Rebuilding destroys components
> A builder that destroys and recreates objects takes their components with them —
> silently. One run wiped `Raw_Matchsticks`' `MatchStrikerSurface` and two
> `FlameAnchor`s, which killed every burner in the game, and the pure tests still
> passed. **Pin actual scene objects.** → [[Gotchas]]

**Recovery order after a bad rebuild:** `Apply Verb Data` (re-adds the striker)
→ `Add Placement Anchors` (re-adds anchors) → **`Re-Home Scene Items (Adopt Current)`**.
Adopt the *current* hand-placement; never re-apply a transform from git.

---

## Documentation

> [!important] Logs are LIVING, not append-only
> When a feature changes, **update the affected lines in place**. Never append dated
> narrative blobs.
>
> When you **correct or supersede** something — a fact you realise is wrong, or a
> mid-work pivot from approach A to B — **edit the canonical line to the final
> truth**. Never leave the stale claim and append "actually B". A doc that
> contradicts itself makes a cold session guess which half is current, and sessions
> *trust* the docs and will not re-verify. A wrong canonical line is worse than a
> verbose one.
>
> **Fix the doc in the same change as the code.**

- [[changelog]] gets **one line per batch**: date · name · one-sentence end state ·
  suite count. The end state, not the journey.
- The *why* of a decision lives in a code comment at the decision site and in a suite
  assertion — not in prose.
- Checklist items: flip `[x]` plus at most one line of note.
- **CLAUDE.md is injected into every message of every session.** Every KB there is a
  recurring tax. Hard cap ~100 lines. "Current state" is *replaced*, never appended.

---

## Efficiency policy

Binding user directive, 2026-07-12.

- **Docs before agents.** Consult this vault before spawning explore/plan subagents —
  each costs 100k+ tokens re-deriving what is already written down. Agents are for
  genuinely unmapped territory, with tight briefs.
- **Prefer menu items over `Unity_RunCommand`** — RunCommand echoes the whole script
  back, so every script costs double.
- **Verify the suite by reading `Logs/selftest-result.txt`**, not by wrapping the run
  in a capture script.
- **One cheapest-sufficient check per fact.** Never suite-assert *and* scene-inspect
  *and* screenshot the same thing.
- **DevCapture only when the visual is the question** (~1.5k tokens per image), one
  shot per change.
- **Bulk content goes through scripts**, not the conversation. Read back a summary
  line, not the file.
- **Session scoping:** batch related small items into one prompt; new session per
  work theme. Long mixed sessions pay a compounding context tax.
- **Output discipline:** short delta summaries mid-batch, full recap only at the end.

---

## Git

- Work on **main**. Commit only when asked. No destructive operations.
- Off-repo backups: `C:\Users\MSI\PharmaSynth-handoff-backup\`.

---

## Tooling environment

Windows 11; PowerShell primary, Git Bash available. Build target **Android**
(IL2CPP / ARM64 / ASTC).

**Unity MCP** is the official Assistant server. It **no longer needs an AI seat** —
entitlement caps were removed in `com.unity.ai.assistant` **2.16.0-pre.1**
(2026-07-21). "Connection revoked" now means the bridge is **awaiting approval**:
open Project Settings ▸ AI ▸ Unity MCP and approve the client.

> [!info] MCP is a speed layer, not a capability layer
> Everything except **screenshots** has a file-based fallback. See
> [[Build and Test Loop]] for the fallbacks and [[Gotchas]] for the MCP traps —
> including the one where `Unity_ReadConsole` lies about compile errors.

⚠ The in-Editor MCP server is **deprecated** (package 2.18) in favour of the **Unity
CLI**. Not broken, no removal date, and MCP-the-protocol is staying — so the migration
is parked until after the 2026-08-31 turnover. Recipe and reasoning:
[[Build and Test Loop]]. Tracked in [[remaining-work-checklist]] §19.

---

## Current phase

All 9 experiments are built, simulate clean, and the full campaign loop plays
end-to-end. The remaining work is the **joint headset playtest pass** — the feel of
pours, grabs and gestures, which nothing in edit mode can see — plus the §10–§13
residue in [[remaining-work-checklist]].

Blocked buckets: the on-device week (no headset yet) and client sign-offs.

When a Quest 3 does arrive, the day-1 device pass is scripted in
[[on-device-test-plan]].
