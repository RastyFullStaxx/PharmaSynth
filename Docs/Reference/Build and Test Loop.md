# Build and Test Loop

Up: [[Home]] · [[Process MOC]] · Related: [[build-and-run]] · [[Gotchas]]

How to run, test and verify. [[build-and-run]] covers the Quest build config and
simulator keys; this note covers the **verification loop** and what to do when the
Unity MCP bridge is unavailable.

---

## The self-test suite

> [!tip] Is the game actually playable? One command.
> `Tools ▸ PharmaSynth ▸ Simulate Everything (full playability check)` plays all 9
> experiments, the campaign loop, the recovery paths and a reachability audit, and writes
> a single ranked verdict to `Logs/simulate-everything.txt`. Run it after any change to a
> module's tasks, layout or verb wiring — then **reopen SampleScene**, because every
> simulator in it mutates the open scene by design. See [[systems-reference]].

`Tools ▸ PharmaSynth ▸ Run Self-Tests` — **well over a thousand assertions**, all in
**edit mode**.

> [!warning] Two things make a green suite look broken
> 1. **Play mode.** ~7 `isPlaying`-gated assertions legitimately fail. Check
>    `Unity_ManageEditor GetState` first, or just exit Play.
> 2. **The wrong scene is open.** Scene-pinned assertions — `bench:`, `match:`,
>    `wired:`, `verbwire:`, `simrun:`, `fumehood:` — inspect real SampleScene objects.
>    With **MainMenu** open they fail *en masse* with `found 0` / `did not run`.
>    **That is not a regression.** Open `Assets/Scenes/SampleScene.unity` and re-run.

**Expected warnings** (green run, not failures): the two W5.9 guard tests and the
Unknown-moduleId negative test.

### Reading the result

Read the one-line summary from disk rather than wrapping the run in a capture script:

```bash
cat Logs/selftest-result.txt
```

> [!tip] An assertion count that did not move means the OLD assembly ran
> Whatever the DLL timestamp says. Cross-check compile status before believing a
> suite result. → [[Gotchas]]

---

## Verifying a compile

There is exactly one trustworthy check:

```bash
grep "error CS" Logs/Editor.log | tail
```

`Unity_ReadConsole` reports 0 errors while the assembly refuses to rebuild. A stale
`Library/ScriptAssemblies/*.dll` after a refresh means the compile **failed**, not
that Unity is idle. → [[Gotchas]]

---

## Working without the Unity MCP bridge

The bridge is free as of `com.unity.ai.assistant` **2.16.0-pre.1** (2026-07-21) — no
AI seat required. **"Connection revoked"** means it is **awaiting approval**, not that
you lack entitlement: approve the client under Project Settings ▸ AI ▸ Unity MCP.
The fallbacks below still matter whenever the editor is closed, busy or mid-reload.

> [!info] MCP is a speed layer, not a capability layer
> Everything below still works. What you actually lose is **latency** — editor-side
> work becomes asynchronous and routed through the user — and **screenshots**, which
> have no fallback.

### The in-Editor MCP server is deprecated (verified 2026-08-27)

`com.unity.ai.assistant` 2.18 added a deprecation notice to every MCP doc page:

> Unity MCP server is deprecated. Use the Unity command-line interface (CLI) instead.
> Unity CLI provides faster iteration times, improved stability, and the ability to
> target runtime and the Editor.

**Deprecated is not broken, and MCP as a protocol is not going away.** Only the
*in-Editor* server inside `com.unity.ai.assistant` is deprecated. The Unity CLI ships
its own MCP server (built on the Unity Pipeline package) using the same protocol, and
Unity states that mode "is not part of the deprecation. It remains fully supported."
No removal date has been announced. The bridge works today — verified end to end on
2026-08-27.

**Status 2026-11-07:** the CLI is now installed (`%LOCALAPPDATA%/Unity/bin/unity`, version 1.0.0-beta.13, still beta); the in-Editor bridge still works. Migrating would add the Pipeline package to the project and rewrite the Claude MCP config, so it waits for a non-beta CLI and the owner's go-ahead.

~~**Not migrating before the 2026-08-31 turnover.**~~ (superseded by the status line above) The Unity CLI is flagged
*experimental* ("features and documentation might change"), it is not installed on
this machine, and `unity pipeline install` mutates the project. Swapping a verified
toolchain for an experimental one days before a hard contract deadline is the wrong
trade. → [[remaining-work-checklist]]

**The migration, when it is time:**

```bash
unity pipeline install
unity mcp configure claude     # rewrites the client config to the CLI's MCP server
unity skill install claude
```

`unity mcp configure --list` shows supported clients. Beyond MCP mode, the CLI adds
`unity` and `unity eval` — direct Editor commands that are faster and cheaper in
tokens than MCP, which matters given how much of a session is prefix re-read. Docs:
`https://docs.unity.com/en-us/unity-cli/replace-mcp-server-unity-cli`.

Unaffected either way: third-party MCP packages installed from GitHub.

### Run the suite

Write a request file; the suite runs on the next domain reload and writes its result:

```bash
echo "why you are running it" > Temp/selftest-autorun-request.txt
```

Result lands in `Temp/selftest-autorun-result.txt`. Handled by `SelfTestAutoRun`
(`[InitializeOnLoad]`), which consumes the request so it fires once.

> [!warning] A domain reload is required
> Reloads happen on script compile or on entering Play mode — **not** on merely
> opening a scene. If nothing has changed, ask the user to run the menu item.

### Run any menu item

```bash
echo "Tools/PharmaSynth/<Menu Path>" > Logs/menu-autorun-request.txt
```

`Logs/`, **not** `Temp/` — Unity wipes `Temp`. Handled by `MenuAutoRun`.

### Headless

With the editor **closed**:

```bash
Unity.exe -batchmode -quit -projectPath <proj> -executeMethod MenuAutoRun.RunNow
```

### Inspect the scene

Parse the scene YAML directly. Note that most interesting objects — the XR rig
included — are **prefab instances** (`--- !u!1001`), whose name and position live in
`m_Modification.m_Modifications`, not in a `Transform` block. A naive scan will miss
them entirely.

[[Scene Objects]] is generated this way; regenerate with:

```bash
python Tools/gen-vault-reference.py
```

---

## Running the game

- **Headset play:** `Tools ▸ PharmaSynth ▸ Headset Play Mode` (OpenXR on Play). ON
  drives a Quest-Link headset in editor Play; OFF is headless keyboard/simulator.
  Initialising with no headset attached can stall Play.

> [!important] Never trust a doc for the current toggle state
> Read `Assets/XR/XRGeneralSettingsPerBuildTarget.asset` → Standalone
> `m_InitManagerOnStart`.

- Whichever scene is **open** is where Play starts. Open `MainMenu` to test the full
  boot flow; open `SampleScene` to drop straight into the lab.
- **No Cinemachine** — never animate the XR camera.
- Dev keys and simulator bindings: [[build-and-run]].

---

## Simulation harnesses

These prove the *logic* end-to-end without a headset:

| Menu | Proves |
|---|---|
| `Simulate Campaign` | door → picker → PPE → run → quiz → grade → outro → debrief → unlock → next, for all 9 |
| `Simulate Tutorial Guidance` | every module's real graph, step by step, with something lit at each step |
| `Audit Tutorial Targets` | 9/9 modules, 81/81 steps resolve to real objects |
| `Reveal Stage ▸ <module>` | exactly what a module spawns onto the bench |

> [!warning] Re-run `Simulate Campaign` after changing any module's tasks or skills
> The two-part gate was **unwinnable** — mastery stayed under 0.90 on flawless runs —
> until each module's `trackedSkills` was set to its signature skills. The
> `content: … is BEATABLE` pins guard this.

---

## What edit mode can never answer

The feel of pours, grabs and gestures. Glow timing, ghost readability, label density.
Comfort and scale in the headset.

That is why the remaining work is a **joint headset playtest pass** — and why a
report like *"I spawn below the floor"* is worth more than any number of green
assertions. → [[remaining-work-checklist]] §13

---

## Definition of done for a change

1. Suite green, in edit mode, with **SampleScene** open.
2. Zero-error console.
3. DevCapture only if the **visual** was the question — one shot.
4. Docs updated **in the same change** → [[Working Agreement]].
5. `Docs/changelog.md` gets one line: date · name · end state · suite count.
