# UNIDAD 2.0 — the Unity framework with an engine-free core

Unidad 2.0 is a faithful Unity 7000.0 port of the Unidad framework (the Godot edition in `10-unidad-godot`). Its first
consumer is `11-unidad-unity`, the CDS6.2 port. The standing decisions are in `docs/DECISIONS.md`; where every carried
file came from, and every divergence, is `docs/SOURCES.md`; the merge loop's review list is `docs/REVIEW.md`.

**This file is short on purpose.** Prose does not hold; a build error does. What is mechanically checkable lives in
`Unidad.Analyzers` and `tools/`; what remains here is the part a tool cannot check.

## The repo

| Path | What |
|---|---|
| `Packages/com.unidad.core` | `Unidad.Core`: engine-free kernel (DI, events, ticking, content, motion, randomness, diagnostics) + the committed analyzer DLL |
| `Packages/com.unidad.modules` | `Unidad.Flow`, `Unidad.Save`, `Unidad.Text`, `Unidad.Audio`, `Unidad.Launch`: engine-free generic modules |
| `Packages/com.unidad.unity` | `Unidad.Adapter` (+ `.Editor`): the only engine assembly; the modules' Unity hosts; the picoCAD import; `Samples~/Movement` |
| `analyzers/` | the analyzer source (`build.ps1 -Check` proves the committed DLL is its build) |
| `dotnet/` | the sidecar: one csproj per engine-free asmdef, smoke projects, `Check/` projects for the engine halves |
| `Sandbox/` | the framework's own tiny Unity project: the serial compile gate and the Movement testbed |
| `tools/` | the toolset, used here AND copied verbatim into every game (U-40); per-repo data only in `tools/repo.json`, `tools/meta-roots.txt`, `docs/REVIEW.md` |
| `Tools~/`, `Skills~/`, `Documentation~/model-pipeline/` | untouchable: the global `picocad-model` skill clones `main` and reads them (U-01, U-112) |

## The one workflow

```
claim a slot  →  write the code + your own smoke tests  →  pwsh -File tools/gate.ps1  →  (lane compile when needed)  →
push, open the PR with a Testbed line  →  the loop reviews and merges every 30 min  →  release the slot
```

1. **Claim**: `pwsh -File tools/worktree-claim.ps1 -Branch <b> [-Base origin/<pr-branch>]` prints the slot path
   (`../unidad-wt-slot<N>`); work only there. Exit 3 = every slot busy: run it again. Never `git worktree add` a task
   worktree, never spawn an agent with worktree isolation: both start cold and skip the hook.
2. **Smoke tests**: a handful of small self-contained NUnit 3 tests in your own `Packages/<pkg>/Tests~/<Name>SmokeTests.cs`
   (PowerShell tools: `tools/tests/*.ps1`). Plain asserts, inputs inline, real classes built with `new`, hand-written
   fakes. Never a golden file, a recording, a generated image or any file a test reads back.
3. **Gate**: `pwsh -File tools/gate.ps1` (parity, metas, tree check, sidecar build, the smoke and Check projects of the
   paths you touched). While iterating, `dotnet test dotnet/<Asm>.Smoke` alone. Always the editor's bundled dotnet:
   `"C:/Program Files/Unity/Hub/Editor/7000.0.0a10/Editor/Data/DotNetSdk/dotnet.exe"`.
4. **Lane**: a PR touching `com.unidad.unity`, any asmdef, `csc.rsp` or `package.json` also runs, once, after committing,
   `pwsh -File tools/unity-lane.ps1 -Compile -Branch <b>` (the lane compiles your local branch).
5. **PR**: `git push -u origin <b>`, `gh pr create --base main --fill` (main, not master), without asking. The body ends
   with `Testbed: <one pasteable line>` or `Testbed: none (<reason>)`.
6. **Release**, as the last command, from the MAIN checkout: `pwsh -File tools/worktree-done.ps1 -Path ../unidad-wt-slot<N>`.

The loop (`tools/loop/`, task `unidad-loop-unidad`, installed by the user with `pwsh -File tools/loop/install.ps1`)
reviews every open PR in parallel, then merges the passing ones in order on a staging branch, compiles Unity-side
changes once in the lane, bisects a red merge to its PR, and pushes to `main`. A review finding is a comment, never a
hold; a red gate is `loop:blocked` until the next push.

## Enforced by the analyzers — do not restate these as prose

UD001-UD016: the table is `analyzers/Unidad.Analyzers/AnalyzerReleases.Unshipped.md`. Escape a rule with
`#pragma warning disable UDxxx` plus a comment saying why. A class named to dodge a rule is a defect.

## Conventions the tools do not check

- Engine-free core and modules (`noEngineReferences` asmdefs); `Unidad.Adapter` is the only engine assembly.
- Namespaces `Unidad.*`, never a `Unity` segment (inside `Unidad.Unity`, `Unity.Burst` would resolve wrongly).
- Every assembly folder has its own `csc.rsp` (`-langversion:14`) and an internal `Polyfills.cs` (netstandard2.1 + C# 14).
- No mutable statics (UD014). Randomness only from `ISeedSource` / `DeterministicRandom`.
- Lists are multi-bindings (`AddMany` / `ResolveAll`), never a hand-kept array. No game names, ids or colours under
  `Packages/` (`tools/check-tree.ps1` refuses `Cds`).

## Consumers

- 0.x: tag `v0.1.0`, branch `release/0.x` (0.1.x fixes only there, U-111).
- 2.x: `...unidad.git?path=/Packages/com.unidad.<x>#v2.0.0-pre.N`, or an embedded copy carried by tag (11, U-05).
  Framework fixes made in a game arrive as `sync-unidad` up PRs (U-54), keyed by the game's PR in `docs/SOURCES.md`.

## Unity rules (encode, don't rediscover)

- One Unity process per project folder. Agents start Unity ONLY through `tools/unity-lane.ps1` (or `tools/seed-unity.ps1`),
  under the one machine-wide editor lock (`tools/lib/EditorLock.ps1`, U-55). Never open an editor or game window; the
  user runs the Testbed line. Never touch or kill a Unity process you did not start (U-49).
- Never `dotnet build` a slot an editor has open (it writes `Library/ScriptAssemblies`).
- `-enable-msbuild` on every MSBuild-mode launch (it does not persist). No `--` in any project path (MSBuild mode breaks).
- Every file and folder under a meta root lands with its `.meta` (the pre-commit hook writes it: GUID = md5 of the path).
  Folders ending in `~` are invisible to Unity (except `Samples~`, whose metas UPM copies). Never move or rename a file a
  scene, prefab, material or PanelSettings references: its GUID comes from its path.
- Read exit codes, never the console: it may be localised (`Compilación correcta`).
- `EditorSceneManager.NewScene` unloads unreferenced assets: create scenes before loading assets.
- `UnityEngine` collides with common names (`Object`, `Random`, `Debug`, `Vector2`): alias them.

## What tooling cannot check — the actual judgement

1. **A green build is not a working package.** The Sandbox testbed run is the check; make the Testbed line open the
   thing directly.
2. **The wiring gate.** Wire every new verb into a production caller (installer, bootstrap, host, tool command line),
   then grep its callers. A verb only tests call is not done.
3. **Parity with 10.** A behaviour that differs from `10-unidad-godot` is a `docs/SOURCES.md` row or a
   `docs/DECISIONS.md` entry, or a bug.
