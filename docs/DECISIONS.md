# Decisions

Appended, never rewritten. Each entry: the date, who decided, the decision, and why. Read the entry before touching what
it names.

**Union rule.** This file merges by union (`.gitattributes: docs/DECISIONS.md merge=union`), so parallel PRs append
without conflicts. A task records its proposals only under the U numbers reserved for it (U-40..U-49 `unidad-tooling`,
U-50..U-69 the port plan, module blocks from U-100), in the order its spec lists them; never "the next free number". A
needed decision that is not reserved is taken as the reversible option under `## <branch>: <title>`, and the merger
renumbers it.

U-01..U-13 copy the framework entries of the CDS6.2 port's standing decisions (`D-xx` there, named in each heading); the
`D-`, `U-` and `C` numbers inside them refer to that port's decisions and open checks. U-50..U-64 are the port plan's
section 3.1, verbatim. U-110..U-118 are the release decisions of the 2.0 cut.

---

## U-01 unidad 2.0 is a new major in the existing unidad repo

(Copy of D-01.) 2026-10-17, the user. Unidad 2.0 lives in `sergiolinarescodes/unidad` (default branch `main`) as a new major version, a
faithful Unity port of the latest Unidad framework (the Godot edition in `10-unidad-godot`: Unidad.Core, the Framework
adapter, Unidad.Analyzers, the generic modules Flow/Save/Text/Audio/Launch, tools, CLAUDE.md, DECISIONS, the PORT_PLAN
method). Tag `v0.1.0` (already on origin, `78b5977` → `ad1524f`) is kept, and a `release/0.x` branch is cut from it for
0.1.x fixes. Before any 2.0 commit lands on `main`, `unidad-template` is pinned to `...unidad.git#v0.1.0`, because it
follows the untagged URL and would otherwise move to 2.0. The `file:` consumers (Experimental, RaceConstructorDeckBuilder,
TexelExp, unity-ppo-racing-trainer) carry embedded copies and are untouched. `Tools~/picocad-pipeline/` and `Skills~/`
stay at their paths on `main` in every 2.x commit, because the global `picocad-model` skill clones `main` and reads them
(every project, Godot ones included, depends on it); moving them needs the global skill edited in the same step, with
the user's approval.

## U-02 Parallelism comes from coding

(Copy of D-05.) 2026-10-17, the user. Unity cannot be opened by many agents. Rules, sim and logic are engine-free plain C# that builds and
smoke-tests with dotnet in seconds; Unity compiles and imports are rare and serial (the lane, D-20). Hard work goes first
instead of constantly checking with Unity.

## U-03 The global workflow applies

(Copy of D-06.) 2026-10-17, the user (global CLAUDE.md). One task = one developer = one branch. The developer writes the code and a
handful of small self-contained smoke tests in the module's own test file (D-17); the gate is build green plus own smoke
tests green; push and `gh pr create --base master --fill` without asking; the scheduled review-and-merge worker
(`tools/loop/`) resolves conflicts, reviews and merges. No CI, no suite runs, no nightly passes, no screen tours, no proof
pictures, no snapshot pinning, no charters, no sign-off gates. Agents never open a game window when a task finishes; the PR
ends with a `Testbed:` line the user runs by hand. Every decision about WHEN something visible happens goes through
`/animation-flow` (beat sheet in the PR). Every visible 3D shape goes through `/picocad-model` (D-04 for CDS's own models).
For this port run all subagents are the `developer` agent on Opus 5.5 at medium effort (the user's request of 2026-10-17);
each brief still carries a real plan: files, the change per file, the smoke tests, the base branch, its F rows, its
reserved D numbers and its own catalogue/text/token file names.

## U-04 Three UPM packages

(Copy of D-10.) 2026-10-17, the port plan. `unidad/Packages/com.unidad.core` (engine-free `Unidad.Core` + the analyzer DLL),
`com.unidad.modules` (engine-free `Unidad.Flow/Save/Text/Audio/Launch`), `com.unidad.unity` (assembly `Unidad.Adapter`: the
adapter, the modules' Unity hosts, and 0.1.0's picoCAD import half, `Editor/PicoCad` + `ModelCatalog`, ported). Plus
`analyzers/` (source), `Sandbox/` (tiny Unity project for the framework's own compile gate), `dotnet/` (sidecar),
`tools/` (game-repo templates), and the untouched `Tools~/` and `Skills~/` (D-01). Version `2.0.0-pre.N` while 11 is in
flight, `2.0.0` when 11 ships. No namespace segment is ever `Unity` (inside `Unidad.Unity`, `Unity.Burst` would resolve
to `Unidad.Unity.Burst`). Generic modules use `Unidad.<Module>` namespaces and carry no game names; `SceneId` is a string
record registered by the game. `CreateTestFactory`, `ISystemTestFactory` and `TestDependencies` are dropped (in
MIGRATING-0.x.md). The framework is built contracts-first: F0 `unidad-contracts` writes the whole public surface and
every project file, then F1 ports core, analyzers, modules, adapter and tooling in parallel. Why: an engine-free core
usable by dotnet tools; modules that always ship together share one package; F0 makes F1 truly parallel.

## U-05 11 carries the framework as embedded copies

(Copy of D-11.) 2026-10-17, the port plan. `11-unidad-unity/Packages/com.unidad.{core,modules,unity}` come from
`git -C ../unidad archive <tag> ...`, never a working tree; `docs/SOURCES.md` records tag, SHA and a Divergences table
keyed by PR number (union merge). A PR editing `Packages/com.unidad.*` has `[unidad]` in its title and adds a row;
game-shaped content never lands there; during W2 the adapter takes blocker fixes only (new motion targets, panel options
and pools are contributed from game View assemblies). `sync-unidad` runs on demand (after W1a, after W2, before ship):
it replays the rows whose PR merged before it started, gates (MSBuild + Bee Sandbox compile), tags `v2.0.0-pre.N+1`,
re-carries, removes exactly the replayed rows, and runs the player boot check (C14). Why: code slots have no Library, so a
git-URL package (PackageCache) is invisible to the sidecar; `file:` couples every worktree to a sibling checkout.

## U-06 C# 14 over netstandard2.1

(Copy of D-13.) 2026-10-17, the port plan (probe Q2). `csc.rsp` with `-langversion:14` beside every asmdef (Bee: the user's editor and
every player) and a root `UnityProject.Build.props` with `<LangVersion>14</LangVersion>` conditioned on `Cds.*` and
`Unidad.*` projects (MSBuild mode ignores csc.rsp; Unity's own packages stay at C# 9). `asmdef-parity` fails any asmdef
folder without the csc.rsp. One internal `Polyfills.cs` per C# 14 assembly (`IsExternalInit`, `RequiredMemberAttribute`,
`CompilerFeatureRequiredAttribute`, `SetsRequiredMembersAttribute` under `#if !NET5_0_OR_GREATER`). netstandard2.1-missing
APIs are never used (the sidecar's netstandard2.1 target catches them). The editor's `enablelastdotnet`/net10.0 hacks are
not used.

## U-07 The sidecar

(Copy of D-14.) 2026-10-17, the port plan. `dotnet/Cds.Sidecar.slnx` (framework csprojs, every Logic csproj, every smoke csproj, Tools)
with `Directory.Build.props` (LangVersion 14, warnings as errors, the analyzer DLL, the refpack pointer; NO global
`AllowUnsafeBlocks`), `Directory.Build.targets` (Compile globs inferred from the project name), `NuGet.config` (nuget.org
only), a 3-line `Cds.<Module>.csproj` (netstandard2.1; ProjectReferences and `AllowUnsafeBlocks` equal to its asmdef's)
and `Cds.<Module>.Smoke.csproj` (net10.0, NUnit 3) per module, and `Tools/` consoles (`Cds.ContentCheck` owned by
content-conversion, `Cds.Sim.Cli` by battle-host, `Cds.Drafter.Train` by ai-drafter). A separate
`dotnet/Cds.ViewCheck.slnx` holds one Check csproj per Unity-side asmdef, compiled against the reference pack
(`%LOCALAPPDATA%/Unidad/refpacks/<master-sha>/`, selected by `current.txt`); without a refpack it prints "lane compile
required". `tools/asmdef-parity.ps1` (owned by 11-base) keeps every csproj equal to its asmdef and runs in pre-commit,
the developer gate and the loop. If C1 passes, engine-free csprojs reference System.Text.Json 10.0.8 (Unity's version).
Always the editor's bundled `dotnet.exe` (SDK 10.0.303). Package versions are pinned to the latest stable at W0 by web
search.

## U-08 Runtime shape

(Copy of D-18.) 2026-10-17, the port plan. Unidad's container with multi-binding (`AddMany/HasMany/ResolveAll`; single services resolve
highest order first); constructor injection only; cycles throw with the chain. `ISystemInstaller` files, one per task.
Discovery over `CurrentAssemblies.GetLoadedAssemblies()` filtered by `[assembly: UnidadModule]` (fallback AppDomain +
attribute + load-context filter, C3). One gameplay scene `Room.unity` with one object, `RoomBootstrap : StageBootstrap`;
views bind as `IStageHost`s. `TickRunner` component inserts Input/Simulation/Presentation/Fixed systems into the
PlayerLoop on `OnEnable` and removes them on `OnDisable`, plus a strip matching the marker's `FullName` from
`SubsystemRegistration` and from `[OnCodeUnloading]` (fallback: MonoBehaviour runner, C4). `TickBinding(ITickable,
TickGroup, RateHz, RunWhilePaused)` in `Unidad.Core.Ticking`. Pause = `TickRunner.Paused` + `Time.timeScale = 0`. One
`EventBus` per container; no R3, UnityEvents or static events. Randomness only from `ISeedSource`/`SeedMix`/
`DeterministicRandom`. **Stubs:** `11-contracts` registers a stub (`IStub`, `Order.Stub = int.MinValue`) for every
public service and every content-named handler id, so master boots from W1a on; the real implementation registers at
order 0 and nothing is deleted. The boot logs winning stubs; `--set strict=true` fails the boot on any, and W3, W4 and
every player check run strict. Launch routing: `IStartTarget`, `IGrantHandler`, `IDateHandler` multi-bindings (declared
in `Cds.Room`, bound by the owning modules); `Cds.Boot` only dispatches by id.

## U-09 No static state

(Copy of D-19.) 2026-10-17, the port plan (Unity 7 has no domain reload; CRITIC G8). No mutable static field in any `Unidad.*` or `Cds.*`
assembly (UD014). Allowed: `const`, immutable `static readonly`, `Log.Current`, the PlayerLoop strip, and
`[AutoStaticsCleanup]` partial types in View assemblies with a pragma and reason. Every CDS singleton
(`SkillUnlockRegistry`, `ThemeCardPoolRegistry`, `ContentManager`, dialogue managers, `ActionRegistry`, `ConditionRegistry`,
`EventDispatcher`, `[ThreadStatic]` sim buffers, static events, the static once-only delivery set) is a container
singleton or an instance field. Batch workers are cancelled and awaited before their arenas are freed on dispose, and
never log from a worker thread.

## U-10 The serial Unity lane

(Copy of D-20.) 2026-10-17, the port plan (probe Q3). Every Unity process an agent starts goes through `tools/unity-lane.ps1`: modes
`-Compile` (editor-free MSBuild-mode `dotnet build 11-unidad-unity.slnx` in a warm Unity slot; regenerate with a batch
editor only when C7 says projects list files; BurstCheck per D-16), `-Bee` (a Bee batch compile, the user's and the
player's pipeline; per loop run, per player build, per sync; the fallback for the whole lane if MSBuild mode breaks),
`-Import`, `-Exec <Method>`, `-Player [-Boot]` (the C14 player boot), `-Claim/-Release` (reserves a slot, never the editor
lock). Two Unity slots (`../11wt-unity1/2`) and the testbed, seeded by robocopy of `Library/` plus the generated MSBuild
files; a per-slot lock and a machine-wide editor lock held per `Unity.exe` process (one agent batch editor at a time, 15
minutes max per call, queue printed); MSBuild-mode compiles may run in both slots at once. `-enable-msbuild` is passed on
every MSBuild-mode launch (it does not persist). Never `dotnet build` a slot an editor has open. The user's interactive
editors are never touched. The Unity CLI and `com.unity.pipeline` are optional tools, not dependencies (beta/exp).
Proposed for the user: a Unity paragraph in the global CLAUDE.md beside the Godot queue.
Amended by the port plan (U-55, U-61): three agent Unity slots plus a loop-only slot; per-PR regenerate whenever the
asmdef/`.cs` list hash changes; the lane checks out the local branch; `-Player -Start -Set -Frames -Release` and `-Run`;
caps 15 min (Compile/Import/Exec) and 45 min (Player/Run); the lock is re-entrant per owner and the testbed GUI is
`owner=user`, never capped or killed.

## U-11 Every Unity file lands with its .meta

(Copy of D-24.) 2026-10-17, the port plan. `tools/unity-metas.sh` (pre-commit hook enabled by `worktree-claim.ps1`, and a loop step)
writes a minimal meta for every new file and folder under `Assets/` and `Packages/com.unidad.*` outside `~` folders, with
GUID = first 32 hex of md5(repo-relative path) (parallel tasks creating one folder write identical metas; no duplicate
GUIDs), from importer templates captured by `11-base` from real a10 imports (`.cs`, folder, `.asmdef`, `csc.rsp`, `.uxml`,
`.uss`, `.tss`, `.json` in and outside StreamingAssets, `.shader`, `.hlsl`). It refuses orphan metas. Models, textures and
fonts get metas from Unity in an `asset` task. Force Text serialization, visible metas.
Amended by the port plan (U-58): the templates are captured once, for every committed type, by unidad's
`tools/seed-unity.ps1` and reach games with `tools/`; a rename of a YAML-referenced file is refused.

## U-12 Analyzers

(Copy of D-33.) 2026-10-17, the port plan. UD001-UD004, UD006, UD008-UD012 retargeted at Unity APIs; new UD013 (ambient randomness in a
service), UD014 (mutable statics), UD015 (Burst subset, including collection expressions into spans, `params
ReadOnlySpan` and floats), UD016 (a `Cds.*` type under the `Unidad` namespace). UD005 and UD007 stay retired. One committed
DLL (`Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll`, RoslynAnalyzer meta), the only committed binary. The
sidecar (every Logic and Check csproj) is the analyzer gate, `Cds.Sim` included; Unity's pickup is secondary, and C2
records its scope (DLL moves into `Runtime/` or to `Assets/Unidad.Analyzers/` as the result requires). Severity Error;
escapes by pragma with a reason only.

## U-13 No shared hot files after W1a

(Copy of D-50.) 2026-10-17, the port plan (10's W1: 3 of 5 merges conflicted on DECISIONS.md). Two parallel tasks never edit one file:
- catalogues merge by glob from per-task slices (D-26); text is one file per task (D-27); theme tokens are per-module USS
  files (D-28); start/grant/date targets, reward handlers, motion targets and drop zones are multi-bindings, one per
  owner; installers, test files and `Lane.<Task>.cs` recipes are one per task;
- DECISIONS.md and SOURCES.md merge by union, with F-numbered and reserved-D-numbered entries and PR-keyed Divergences rows;
- contracts never gain members after W1a: a new capability is a new interface in a new file, stubbed at `Order.Stub` when
  another module implements it (named in the PR body); records gain only optional members, and required members never
  change after `content-conversion`;
- folder metas are deterministic (D-24), and `11-contracts` creates every module's folders up front.

---

## U-50..U-64 The port plan's unidad decisions (PORT_PLAN.md section 3.1, 2026-10-20)

**U-50 Content lives in `Unidad.Core.Content`.** `f0-contracts` declares these types in `Unidad.Core` (proposal D-core-2
wins over D-U1):
- `IContentSource`
- `ContentJson`, including `Catalogue`: `<name>.json` plus `<name>.*.json` across first-level folders
- `ContentException`, `ContentPath`
- `DirectoryContentSource`, `MemoryContentSource`, `LayeredContentSource`
- `IContentCheck` with `Check(ContentReport)`, and `ContentReport.Run`

`Unidad.Text` consumes these types and declares none of them. There is no `ContentChecks.RunAll`: the adapter runs
`ContentReport.Run(container.ResolveAll<IContentCheck>())` after the build. `BootSeed` stays in `Unidad.Launch`,
`TextKeySet` in `Unidad.Text`, and `InputFocus` in `Unidad.Flow`.

**U-51 One conditional JSON fallback.** `f1-tooling-lane`'s first Sandbox compile decides C1 (framework half). If a
`noEngineReferences` asmdef cannot reference System.Text.Json 10.0.8 on a10, `f2-json-fallback` writes our own strict
reader behind the unchanged `ContentJson` and `Unidad.Save` APIs. If it can, that task is skipped (D-75). `11-base`'s C1
spike only confirms the player side.

**U-52 Who writes the repo's shared files.** `u0-release-cut` writes:
- the final `.gitignore`, `.gitattributes` and `README.md`;
- `docs/DECISIONS.md`: the header, the union rule, U-01..U-13 (copied from D-01, D-05, D-06, D-10, D-11, D-13, D-14, D-18,
  D-19, D-20, D-24, D-33 and D-50), and U-50..U-64 from this section;
- the `docs/SOURCES.md` skeleton.

After that, tasks only append their own entries. `f0-contracts` writes no `.gitignore` or `.gitattributes` and deletes
nothing, because the cut already did.

**U-53 The adapter's first Unity compile is F3.** The F1 adapter tasks gate on the sidecar and on Check csprojs compiled
against `Data/Managed/UnityEngine/*.dll` (U-59) plus the seed refpack (U-58), so every adapter file, incl. the Input
System bridges and the glTFast-based PicoCad builder, compiles in its own task. They never call the lane, because the lane
(`f1-tooling-lane`) is a same-wave peer. `f3-adapter-sandbox-gate` runs the first Sandbox compile, the first Bee compile and the first player
boot, and fixes the blockers.

**U-54 Sync is split into up and down.** Each sync run is two tasks:
- `sync-unidad-N-up` (unidad): replay the Divergences rows, bump, gate, open the PR, tag on merge.
- `sync-unidad-N-down` (11): carry the tag, then run the lane's Compile, Bee and Player -Boot.

These replace the release module's `sync-unidad-1/2/3` and `rel-ship`. There is one procedure,
`docs/SYNC-UNIDAD.md`, written by `f2-release-tooling`.

**U-55 One editor lock per machine.** `f0-tooling-base` writes `tools/lib/EditorLock.ps1` (`Invoke-WithEditorLock
-Owner -TimeoutMinutes <cap> -ScriptBlock`, lock file `%LOCALAPPDATA%/Unidad/locks/unity-editor.lock`, re-entrant for the
same `-Owner`, so `sandbox-gate` inside a lane call never deadlocks), and every game copies it unchanged. Every agent
batch editor takes this lock: lane, sandbox-gate, seed. The testbed takes it only for its compile/regenerate step and
releases it before it opens the user's GUI editor, which is registered `owner=user` and is never counted, capped or
killed (U-60).

**U-56 Decision numbering in unidad.** U-40..U-49 belong to `unidad-tooling`, U-50..U-69 to this plan, and module blocks
start at U-100 (3.3). The release, tooling and modules specs exist (`SPEC/unidad-release.md`, `SPEC/unidad-tooling.md`,
`SPEC/unidad-modules.md`); their tasks record their proposals in their blocks, and the brief wins where they differ.

**U-57 Per-repo tool data.** unidad and every game share one toolset. The per-repo data lives only in `tools/repo.json`,
`tools/meta-roots.txt` and `docs/REVIEW.md` (the tooling's U-40). 11 uses `repo.json`; the `tools/unidad.project.json`
proposed by the 11-base spec is not created.

**U-58 One seed project gives the meta templates and the first refpack.** The a10 probe project is gone, so
`f0-tooling-base` writes `tools/seed-unity.ps1`: under the editor lock it creates a throwaway project in
`%LOCALAPPDATA%/Unidad/seed/` with the final game manifest (inputsystem 6.7.0, URP 17.7.0, ugui 7.0.0, glTFast 6.20.0,
burst, collections), drops one file of every type the repos commit (`.cs`, folder, `.asmdef`, `csc.rsp`, `.uxml`, `.uss`,
`.tss`, `.json` in and out of StreamingAssets, `.shader`, `.hlsl`, `.fbx`, `.ttf`, `.otf`, `.png`, `.wav`, `.ogg`,
`.asset`, `.unity`, `.prefab`, `.mat`, `.dll`), runs one batch import + compile (`-quit`, about a minute), copies the
importer metas into `tools/meta-templates/` and the package/engine DLLs into
`%LOCALAPPDATA%/Unidad/refpacks/seed/`, and points both repos' `current.txt` at it when no newer refpack exists.
`unity-metas.sh` keeps refusing unknown types. `f1-tooling-lane` only re-verifies the templates; `f3-adapter-sandbox-gate`
re-renders every committed `Packages/**/*.meta` that F0 wrote before the templates existed.

**U-59 Check csprojs reference exactly the engine DLLs they need.** Runtime Check projects reference
`<UnityInstall>/Data/Managed/UnityEngine/*.dll`; editor Check projects add `Data/Managed/UnityEditor.dll` and
`UnityEditor.Graphs.dll`. Never a recursive `Data/Managed/**` glob (it imports duplicate `System.CodeDom` and editor
tooling: CS1704). The refpack carries only engine-module and package DLLs plus `Unidad.Adapter(.Editor)`, never
`Unidad.Core`/`Unidad.Modules` (those come from the source csprojs, so no CS0433). A Check project that references a Cds
Logic project and `Unidad.Adapter` builds with zero warnings (asserted by C8).

**U-60 The testbed previews; it never blocks agents.** `tools/testbed.ps1 -Branch b`:
- if `origin/b` exists and is not merged, it checks out a detached, uncommitted merge of the default branch and `origin/b`
  in the testbed slot (no push); if `b` is merged or gone, it checks out the default branch and prints `b merged at <sha>;
  showing <default>`; `-Ref <sha|branch>` reproduces an exact commit;
- it closes an earlier testbed editor that it started itself before `checkout --force`;
- it takes the editor lock only for its compile/regenerate step, then opens the GUI editor as `owner=user` (U-55);
- `-Set` takes one string, split only before a `key=` (`;` or `,` followed by `[a-z][a-z0-9.]*=`), so comma grants
  survive `pwsh -File`; `-Start x` emits `--set start=x`; `report` defaults to `$env:TEMP/cds-report.txt`.
"Visible after: X" means: run the line again after X merges.

**U-61 Lane: local refs, runs, slots, caps.** `tools/unity-lane.ps1`:
- `-Branch b` checks out the local `refs/heads/b` (slots are worktrees of one repo, so no push is needed), falling back to
  `origin/b`; agents commit before `-Compile` and push after;
- `-Player [-Boot] [-Strict] [-Start t] [-Set "k=v;..."] [-Frames n] [-Release]` builds and runs a self-quitting player;
  `-Run [-Start t] [-Set ...] [-Frames n]` reuses `<slot>/Build/` when its build stamp equals the branch SHA, so a sweep
  builds once and boots N times; older wording `--quit-after N` means `--set frames=N`;
- `-Import -Collect` copies importer metas back into the caller's worktree with the same filter as `-Exec -Collect`;
- per-mode hold caps: Compile/Import/Exec 15 min, Player/Run 45 min;
- slots: 3 agent Unity slots plus `wt-unity-loop`, used only by the loop on the default branch/staging, so its Bee
  Library stays warm; `-Claim` is released after 20 minutes without a lane call, and `-Compile` may borrow a claimed slot
  between the claimant's calls under the slot lock;
- regenerate: whenever the asmdef/`.cs` list hash changes, per PR (never once per loop run, whatever C7 says). Where C7
  shows listed files, `UnityProject.Build.props` adds a glob of `**/*.cs` per asmdef folder minus listed items, so a new
  file compiles without an editor and only a new asmdef needs a regenerate. LaneSmoke asserts a new `.cs` with a CS
  error makes `-Compile` exit 1;
- a `postCompileChecks` entry whose method does not exist is skipped with a warning.
unidad's own `repo.json` names `unidad-wt-unity1`, `unidad-wt-loop`, `unidad-wt-testbed` and `projectSubdir: Sandbox`.

**U-62 Every sync carries the toolset.** `carry-unidad.ps1` also mirrors unidad's `tools/` into the game, except
`tools/repo.json`, `tools/meta-roots.txt` and files the game lists in `repo.json` `gameTools`. `sync-verify -Phase Down`
allows `tools/**` minus those. So lane, testbed and loop fixes made in unidad reach 11 at every sync, and `w0-base-boot`
re-copies `tools/` before it first runs `carry-unidad.ps1`.

**U-63 Binary allowlist per repo.** `check-tree.ps1` reads `binaryAllow` from `repo.json`. unidad: the analyzer DLL and
`Packages/com.unidad.unity/Runtime/Resources/**` audio. 11: `*.fbx`, `*.ttf`, `*.otf`, `*.png`, `*.wav`, `*.ogg`,
`Assets/Resources/Cds/Fonts/**`, the carried analyzer DLL; never `*.exe`, any other `*.dll` or `Library/`. The matching
`.gitattributes` binary lines go in with it.

**U-64 Loop staging checks.** Besides the lane compile, the loop's Phase B runs every `repo.json` `stagingChecks` command
on `loop/staging` and bisects by PR on red. 11 lists `dotnet run --project dotnet/Tools/Cds.ContentCheck -- --full`
(engine-free, seconds; it composes the container from every installer, so two real bindings of one keyed id or a broken
installer fail the merge, not the next boot). An entry whose project does not exist yet is skipped.

---

## U-110 The template is pinned before 2.0

2026-10-20, the 2.0 cut (u0-release-cut, D-R1). `unidad-template` follows `unidad.git` and would have moved to 2.0 on
the first 2.x commit, so it was pinned to `#v0.1.0` (manifest and lock entry, hash `ad1524f`) before the cut merged
(unidad-template PR #2). `tools/release-check.ps1 -Check TemplatePinned` reads the template's `main` manifest through
`gh api` and guards it. Why: no consumer may move to a new major without asking for it.

## U-111 `release/0.x` carries 0.1.x

2026-10-20, the 2.0 cut (D-R2). `release/0.x` was pushed at `ad1524f` (the commit of tag `v0.1.0`). A 0.1.x fix lands
only there (`gh pr create --base release/0.x`), its root `package.json` goes to `0.1.N` and it is tagged `v0.1.N`. It is
never merged into `main`, and `main` is never merged into it. `release-check -Check ReleaseBranch` asserts the branch.

## U-112 The kept trees are pinned by hash

2026-10-20, the 2.0 cut (D-R3). `Tools~/` (`63fb6dc`), `Skills~/` (`c9c0db2`) and `Documentation~/model-pipeline/`
(`4dc586e`) stay at the root of every 2.x commit, byte-identical to 0.1.0, because the global `picocad-model` skill clones
`main` and reads them (`Documentation~/model-pipeline/DESIGN_SYSTEM.template.md` is read at Checkpoint B, so it is kept
beside the other two; `Documentation~/LiveMcpTesting.md` went). Their 0.1.0 paths stay stale on purpose; fixing them
needs the global skill edited in the same step with the user's approval. `.gitattributes` marks them `-text` and
`release-check -Check KeptTrees` compares the three tree hashes.

## U-113 The orchestrator merges U0

2026-10-20, the 2.0 cut (D-R4, from `u0-pin-template`). `unidad-template` has no merge worker, and unidad had none before
`f0-tooling-base`: the orchestrator merges the template pin and the cut by hand. The cut's PR opens only after
`release-check -Check TemplatePinned` passes, and the orchestrator runs it again before merging.

## U-114 One version for the three packages

2026-10-20, the 2.0 cut (D-R5). `com.unidad.core`, `com.unidad.modules` and `com.unidad.unity` always carry the same
`version`, and every `com.unidad.*` dependency names that version. `2.0.0-pre.N` while 11 is in flight (pre.N advances by
one per tag, and a red gate never consumes a number), `2.0.0` when 11 ships, then SemVer. Only a release run changes
the version fields. `release-check -Check Versions` asserts it once `Packages/` exists.

## U-115 Tags never move

2026-10-20, the 2.0 cut (D-R6). Tags are annotated `v<version>` on a `main` commit (`release/0.x` for `v0.1.N`) and are
never moved or deleted; `v0.1.0` stays. A wrong tag is fixed by the next number. Games always carry a tag, never a branch
tip.

## U-116 11's loop refuses framework edits that cannot be replayed

2026-10-20, the 2.0 cut (D-R7). A `[unidad]` PR in 11 whose Divergences row names
`Packages/com.unidad.core/Analyzers/Unidad.Analyzers.dll` (its source lives only in unidad `analyzers/`) or a
`package.json` `version`/`dependencies` field is refused by 11's merge loop. Analyzer fixes and version changes are
unidad PRs followed by a carry.

## U-117 Sync cadence

2026-10-20, the 2.0 cut (D-R8). `sync-unidad` runs after W1a, after W2 and before ship (and on demand for an upstream
blocker fix), as an up PR on unidad and a down carry on 11 (U-54). The carry re-applies with `--3way` the framework edits
merged in 11 after the sync started (`carry-unidad.ps1 -Since <S>`), so they are never overwritten and their rows stay.

## U-118 One machine-wide editor lock

2026-10-20, the 2.0 cut (D-R9). `sandbox-gate.ps1` takes the same editor lock as every game's `unity-lane.ps1`
(`tools/lib/EditorLock.ps1`, U-55): one agent-started `Unity.exe` on the machine at a time. Until the helper exists,
`sandbox-gate.ps1` refuses to start Unity.

## U-40 One toolset, data per repo

2026-10-20, `f0-tooling-base` (unidad-tooling). `tools/**` in unidad is a working toolset used by unidad itself AND the
template every game copies verbatim (U-62 carries it at each sync). The only per-repo files are `tools/repo.json`,
`tools/meta-roots.txt` and `docs/REVIEW.md` (U-57). No script hard-codes a repo name, branch, slot prefix, slnx or Unity
method: they read `repo.json` through `tools/lib/UnidadTools.ps1`. unidad's slots are `../unidad-wt-slot<N>` (8 code
slots), `../unidad-wt-unity1`, `../unidad-wt-loop` and `../unidad-wt-testbed`; its Unity project is `Sandbox`.

## U-41 Editor-lock caps and FIFO

2026-10-20, `f0-tooling-base`. The machine-wide editor lock (`tools/lib/EditorLock.ps1`, U-55) is a FIFO queue of
tickets (`<home>/locks/queue/<utcTicks>-<pid>.json`): a waiter takes the lock only when its ticket is the oldest live one
and the lock is free or stale (dead PID, or a PID reused by another program: start time 2 s off). It prints the queue
every 30 s and gives up after `-WaitMinutes` (the caller exits 3). Each holder records its hold cap (`-TimeoutMinutes`;
`repo.json` `lane.capsMinutes`, U-61: Compile/Import/Exec 15, Player/Run 45, Init 45); the caller waits for the Unity
process it started and, at the cap, kills that PID's tree only. The lock is re-entrant for the same `-Owner` (passed to
child processes as `UNIDAD_EDITOR_LOCK_OWNER`), so a nested sandbox-gate never deadlocks.

## U-42 Exec -Collect

2026-10-20, `f0-tooling-base` (contract for `f1-tooling-lane`). The lane works on commits: `-Branch b` uses the local
branch `b` from the shared object store, else `origin/b` (U-61). What an `-Import` or `-Exec` writes is copied back into
the caller's worktree (`-Collect <roots>`, default `lane.collectRoots`, never Library/Temp/Logs/UserSettings/Build or
generated project files) and committed by the developer, never by the lane.

## U-43 Refpack per repo and SHA

2026-10-20, `f0-tooling-base`. Reference packs live in `%LOCALAPPDATA%/Unidad/refpacks/<repo>/<sha>/` (editor DLLs in
`Editor/`), and each repo has its own pointer `refpacks/<repo>/current.txt` (the absolute directory, forward slashes, no
newline), written to a `.tmp` and moved over, so two repos never overwrite each other's pointer and readers never see a
half-written one. A refpack holds the package DLLs of `Library/ScriptAssemblies` selected by `repo.json` `refpack`;
engine DLLs come from the install's `Data/Managed` directly and `Unidad.*` from source (U-59). The first one is the seed
refpack `refpacks/seed/` (U-58); `gate.ps1` builds Check projects only while a pointer exists, else it prints
`lane compile required`.

## U-44 Everything as --set

2026-10-20, `f0-tooling-base` (contract for `f1-tooling-lane`). The testbed and the lane's player runs pass every
setting as `--set key=value`: `-Start x` becomes `--set start=x`, `-Frames n` `--set frames=n`, and `-Set` takes one
string split only before a `key=` (U-60), so comma grants survive `pwsh -File`.

## U-45 Loop phases, and which scripts claim what

2026-10-20, `f0-tooling-base`. The loop (`tools/loop/run.ps1`, scheduled task `unidad-loop-<name>` every
`loop.everyMinutes` = 30, one run at a time, installed only by the user) has two phases. Phase A: one headless agent per
open PR, up to `loop.reviewers` = 4 in parallel, each in its own reviewer worktree `../<name>-loop-r<k>` of the loop
clone, with `tools/loop/review-merge.md` as the brief; it merges main in, reviews, fixes, gates, pushes the PR head and
writes a verdict JSON. Phase B is a script (U-48). The scheduled task runs the clone's own copy of `run.ps1`, so the
loop always runs the merged toolset. `worktree-claim.ps1 -Kind code` (default) claims code slots; `-Kind unity` the
repo's Unity slots, which otherwise belong to the lane (`unity-lane.ps1 -Claim`). `worktree-done.ps1 -Sweep` never
touches the lane's slots, the testbed, the loop clone or its reviewers.

## U-46 Sandbox shape

2026-10-20, `f0-tooling-base` (contract for `f1-tooling-lane`). `Sandbox/` commits no scene: its
`Unidad.Sandbox.Editor.Testbed.Play` builds the Movement scene in memory, and the lane's prepare step
(`tools/sandbox-prepare.ps1`) copies `Packages/com.unidad.unity/Samples~/Movement` into the gitignored
`Sandbox/Assets/Samples/Movement`, so the Sandbox compile covers the sample (0.1.0 `ad1524f`: sample gaps were invisible
because `Samples~` never compiled). `Sandbox/Assets` is a meta root.

## U-47 Captured meta templates

2026-10-20, `f0-tooling-base`. `tools/meta-templates/<key>.meta` are captured from a real 7000.0.0a10 import by the seed
run (`tools/seed-unity.ps1`, U-58): `folder cs asmdef asmref text default package uxml uss tss shader hlsl cginc compute
png wav ogg ttf otf fbx unity prefab mat asset dll`, with `guid:` replaced by `{{GUID}}` and `timeCreated:` dropped.
`tools/unity-metas.sh` renders them with GUID = first 32 hex of md5(repo-relative path); a `.json` (or any file) under
`StreamingAssets/` takes `default`, `md txt bytes xml csv` take `text`, `csc.rsp`/`LICENSE`/extensionless files take
`default`; any other extension with no template is refused. A meta Unity already wrote on disk is staged as is. Games
reuse these templates; they are recaptured with the same script after an editor upgrade.

## U-48 Phase B is a script; bisect by PR

2026-10-20, `f0-tooling-base`. Phase B merges, in PR order into `loop/staging` from main, every PR whose verdict is
`pass` at the PR's current head, running `gate.ps1 -BuildOnly` after each merge. A PR whose merge conflicts with one
merged earlier in the run is left for the next run (its Phase A resolves it against the new main). A PR whose build is
red, or whose own verdict is `blocked`, gets `loop:blocked`, a comment with the failing step's last 40 lines, and its
head in `.loop/blocked.json`; any new push to the PR head lifts the label. Then every `repo.json` `stagingChecks`
command (U-64) and, when a merged PR touched `loop.lanePaths` and `tools/unity-lane.ps1` exists, one lane `-Compile` and
`-Bee` of staging on the loop slot. A red step is bisected over the run's merges with `Find-FirstRed` (main counts as
green; at most ceil(log2 k) lane calls); the culprit is blocked, staging is reset before it and later PRs wait for the
next run. A lane exit of 2 or more (queue timeout, STOP, environment) pushes nothing this run. Then a fast-forward push
to main (GitHub marks each PR merged), PRs stacked on a merged head are retargeted to main BEFORE the head is deleted,
the refpack is refreshed from the loop slot, and `worktree-done.ps1 -Sweep` runs.

## U-49 Own PIDs only

2026-10-20, `f0-tooling-base`. Every tool that starts a process (the seed, the lane, the testbed, the loop) waits for
and kills only the PIDs it started, by their captured PID. An unregistered `Unity.exe` is the user's and is never
touched, counted or killed. The STOP file (`<home>/guard/STOP`, `f1-tooling-lane`) blocks every lane mode, the
testbed and Phase B's lane calls.
## U-120 Content lives in `Unidad.Core.Content`

2026-10-20, f0-contracts (P1, settled by U-50). `IContentSource`, `ContentJson`, `ContentException`, `ContentPath`,
`IContentCheck`, `ContentReport` and the three sources are core types. Save, Audio's catalogue, the adapter's models and
every game module read content without referencing `Unidad.Text`.

## U-121 Single bindings carry an order

2026-10-20, f0-contracts (P2). The highest order wins a contract; among equal orders the later registration (10's rule).
`Order.Stub = int.MinValue`, `Order.Default = 0`, `AddStub` = `AddSingleton(factory, Order.Stub, contracts)`, marker
`IStub`. `Container.StubbedContracts()` lists the contracts a stub still wins (from registrations, no instantiation) and
`Container.WinningStubs` names them plus the keyed ids `ResolveKeyed` resolved to a stub; `StubLedger` and
`HandlerRegistry.ById` serve content-named handlers. The adapter logs them after the stage hosts bind and fails the boot
under `--set strict=true`.

## U-122 TickGroup keeps 10's ordinals and adds Fixed

2026-10-20, f0-contracts (P3). Input 0, Simulation 1, Streaming 2, Presentation 3, Rendering 4, Fixed 5. A
`RunWhilePaused` entry always receives the unscaled delta; every other entry is skipped while paused, its accumulator
untouched (`TickSchedule.Step(dt, unscaledDt, paused)`, and `StepGroup` for one PlayerLoop phase).

## U-123 F0 writes the dependency kernel; F1 fills the rest

2026-10-20, f0-contracts (P4). Real bodies: DI with ordered single bindings and stubs, discovery, EventBus,
SystemServiceBase, OrderedRegistry, StateMachine, DeterministicRandom, SeedMix (+ `Salt`), SeedSource, Log,
CompositeDisposable, ContentJson with `Catalogue`, Directory/MemoryContentSource, `ContentPath.Validate/Matches`,
TickBinding, ReadModelCell, StubLedger, the attributes, and the members `TickSchedule` already had in 10. Every other body
throws `NotImplementedException`; the F1 tasks fill them and never rewrite the kernel (bug fixes only).

## U-124 Dead code is not carried

2026-10-20, f0-contracts (P5, with unidad-core's D-core-5). Not declared: `Timer`, `Inventory`, `Progression`, `Resource`,
`IRegistry`/`RegistryBase`, `CommandQueue`, `NullEventBus`, `EventBusFactory`, `Debugger/DebugModeService`,
`IDebugProvider`, `Testing/*`, every `*TestFactory`, `ISystemInstaller.CreateTestFactory`, `JitWarmup`,
`IDebugUi.RichReadout`/`ClickMode`, `HudLines`, the parameterless `InstallerDiscovery.FindInstallerTypes()` and the
`--movie --pause-at --vsync` flags. `f4-release-pre1` lists them in `docs/MIGRATING-0.x.md`.

## U-125 Motion targets are passed, never registered

2026-10-20, f0-contracts (P6). `IMotionTarget<T>` instances are handed to `IMotionRunner.Play`; a game's View assembly
keeps its own (TMP colour and scale). There is no container entry for them.

## U-126 No adapter content source and no MainThreadQueue

2026-10-20, f0-contracts (P7). The adapter binds core's `DirectoryContentSource` over `streamingAssetsPath/Content` and
drains core's `MainThreadDispatcher` first in the Input group. No duplicate types.

## U-127 glTFast is not a package dependency

2026-10-20, f0-contracts (P8). `com.unidad.unity`'s package.json does not list `com.unity.cloud.gltfast`; the
`Unidad.Adapter.Editor` asmdef references `glTFast` by name and the consumer's manifest carries the package. The
framework resolves offline either way.

## U-128 Nullable is disabled in every framework assembly

2026-10-20, f0-contracts (P9). No `-nullable` in csc.rsp and `<Nullable>disable</Nullable>` in the sidecar, so ported
`?` annotations on reference types are removed (CS8632 would fail the warnings-as-errors build).

## U-129 Analyzer markers match by simple name

2026-10-20, f0-contracts (P10). `DeterministicSimulation`, `BurstSubset`, `ProcessOverride`, `UnidadModule` and
`AutoStaticsCleanup` are matched by name in any namespace. `Unidad.Core.Abstractions` declares all but `UnidadModule`
(in `Unidad.Core.Bootstrap`); an assembly that references nothing (`Cds.Sim`) may declare its own copies.

## U-130 Check projects compile the adapter without Unity

2026-10-20, f0-contracts (P11, U-59). `dotnet/Unidad.ViewCheck.slnx` builds `Unidad.Adapter` and `Unidad.Adapter.Editor`
against `Data/Managed/UnityEngine/UnityEngine.*Module.dll` and `Unity.Scripting.dll` (the editor check adds
`UnityEngine/UnityEditor.*Module.dll`, `Data/Managed/UnityEditor.dll` and `UnityEditor.Graphs.dll`), plus the refpack when
`%LOCALAPPDATA%/Unidad/refpacks/unidad/current.txt` names one (its `Unidad.*` DLLs excluded). Until a refpack exists,
`Runtime/Input/**` and `Editor/PicoCad/**` are left out: the Input System and glTFast are package DLLs only a Unity import
yields. The F0 surface uses neither, so F0 builds with no Unity process.

## U-131 The 0.1.0 root package is gone

2026-10-20, f0-contracts (P12, done by `u0-release-cut`, U-52). The 2.0 packages live only under `Packages/`; the root
has no `package.json`.

## U-132 F0 writes md5-of-path metas

2026-10-20, f0-contracts (P13, U-11). Every file and folder under `Packages/com.unidad.*` outside `~` folders has a meta
whose guid is the first 32 hex of md5(repo-relative path, forward slashes), from the minimal templates (`.cs`
MonoImporter, folder, `.asmdef` AssemblyDefinitionImporter, `package.json` PackageManifestImporter, `csc.rsp`
DefaultImporter). Package roots have none. `f3-adapter-sandbox-gate` re-renders them from the captured templates.

## U-133 System.Text.Json stays off the public surface outside Save

2026-10-20, f0-contracts (P14). `ContentJson`'s options are internal; the only System.Text.Json types on the public
surface are `JsonNode`, `JsonObject` and `JsonArray` in `Unidad.Save`. If C1 fails, only Save's participant signatures move.

## U-134 ModifierStack sorts stably

2026-10-20, f0-contracts (F0-1; `f1-core-runtime` implements). Priority descending, then insertion order; 10 used an
unstable `List.Sort`.

## U-135 Autosave counts tick time

2026-10-20, f0-contracts (F0-2; `f1-modules` implements). The periodic autosave adds the `Tick(dt)` it is given while a
settled predicate holds, never the wall clock, so a pause never counts.

## U-136 Save keeps unknown sections

2026-10-20, f0-contracts (F0-3; `f1-modules` implements). Sections of a loaded slot that no participant claims are written
back on every save, so a disabled mod's section survives.

## U-137 Save and autosave are ticked

2026-10-20, f0-contracts (F0-4; `f1-adapter-hosts-ui` implements). `SaveHost` registers the save service and the
autosave in the Simulation group; 10's master never ticked them.

## U-138 UD008 is symbol-checked, UD009 matches by name

2026-10-20, f0-contracts (F0-5; `f1-analyzers` implements). UD008 resolves symbols instead of matching text; UD009 finds
`DeterministicSimulation` by simple name (U-129).

## U-139 Timer's registration bug goes with Timer

2026-10-20, f0-contracts (F0-6). 10's Timer service registered itself as the contract `ITickable`. The service is not
carried (U-124), so the defect goes with it.

## f0-contracts: Each assembly's surface follows the spec of the task that implements it

2026-10-20, f0-contracts. The contracts spec, the module specs and the plan disagree on some shapes. F0 declares, per
assembly, the surface of the spec its F1 implementer builds against, plus the members the plan names:
- `Unidad.Core` from `unidad-core` §b (`ISeedSource.Derive(string)` with `SeedMix.Salt`, `WinningStubs`/`ResolveKeyed`,
  the debug seam in `Unidad.Core.Diagnostics`, `IContentCheck.Check(ContentReport)`, the Motion records and classes),
  with the plan's additions (`StubbedContracts()`, `StepGroup`, `ReadModelCell`, `StubLedger`, `HandlerRegistry`,
  `InstallerDiscovery.Modules`, `ContentPath`, and `Ease` values that include the Sine curves);
- the five modules from `unidad-modules` §2 (U-50 moves its content types to core; Voice and Animalese stay);
- the adapter from `unidad-adapter` §b (`ITickRegistry`, `IMotionRunner`, `UiDocumentHost`, `IModelSource`) with the
  plan's frozen editor CLI: `LaneCli.Compile/Import/PlayScene` (`-unidadScene`) and `BuildCli.Player` (`-buildOut`,
  `-development`, `-scene`, `-release`; writes `build-report.txt`; exit 0/1).

Reversible: a shape can still change by a unidad PR before `f4-release-pre1` tags.
