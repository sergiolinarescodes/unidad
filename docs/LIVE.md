# Live iteration: the inner loop

`tools/live.ps1` keeps one Unity editor open in Play mode on your branch, in a live slot beside the checkout
(`../<slotPrefix>-live1`, `-live2`). You edit in your code slot, `-Sync`, and read a fresh frame in seconds, with no
player build and no restart between rounds.

It is for finding the right look, value or approach on a feature that already works: tuning numbers, colours, layout,
copy, materials, a one-line view change. It works best on changes that need no recompile (stylesheets, materials,
textures, data), which import in place while the game keeps running. A script change compiles in the open editor and
replays Play, which takes seconds, not a player build.

**A live frame is never a verdict.** The look's verdict is still the player-build run (`tools/look.ps1` in a game that
has the look loop, or the lane's `-Run -Graphics` capture) and its review. A live frame comes from the editor's Game
view, not a player: use it to choose what to change, then run the real look once on the commit. A live run folder's
`frames.jsonl` header carries `"live": true` and no tour or checklist hashes, so a verdict check refuses it.

## The verbs

```
pwsh -File tools/live.ps1 -Open   [-Branch <b>] [-From <code slot>] [-Start <t>] [-Set "k=v;..."]
pwsh -File tools/live.ps1 -Sync   [-From <code slot>]          copy what changed, route it, print route + ms
pwsh -File tools/live.ps1 -Shot   -Label <label> -Out <dir>    one PNG of the Game view (overlay UI included)
pwsh -File tools/live.ps1 -Replay                              leave and re-enter Play with the same launch line
pwsh -File tools/live.ps1 -Tour <file> [-Until <label>] -Out <dir>   a tour, through repo.json live.tour
pwsh -File tools/live.ps1 -Eval "<C#>"                         try a value in the running game
pwsh -File tools/live.ps1 -Query                               visible UI Toolkit elements: text, name, rect
pwsh -File tools/live.ps1 -Close  |  -Status
```

`-Branch` defaults to the current directory's branch and `-From` to its checkout, so from a code slot the verbs need no
arguments. Run `-Open` from the code slot after committing once: the live slot checks out the branch's commit (the
local branch, else `origin/<b>`) and copies your uncommitted files over it.

- **-Open** takes a seat, puts the live slot on the branch, copies the code slot's changes, runs `lane.prepare`, writes
  the overlay (below), seeds `Library` from an idle lane slot the first time, starts the editor and enters Play
  through the testbed door (`lane.testbedMethod`) with `UNIDAD_LAUNCH` built from `-Start`/`-Set` exactly as the
  testbed builds it. It returns when the game has played `live.settleSeconds`.
- **-Sync** copies every file that differs between the code slot and the live slot (changed against the slot's commit,
  untracked, or synced before, so a revert syncs too), runs `lane.prepare`, then takes the strongest route:
  | route | paths | what happens |
  |---|---|---|
  | `style` | `.uss`, `.tss`, `.uxml` | imported in place; UI Toolkit restyles the running panels |
  | `asset` | everything else (materials, textures, models, data) | imported in place; the game keeps playing |
  | `replay` | `live.replayPaths` regexes (data the game reads only at boot) | imported, then Play is re-entered |
  | `recompile` | `.cs`, `.asmdef`, `.asmref`, `csc.rsp`, `package.json`, `manifest.json` | leave Play, compile, re-enter Play |
  A red compile prints its errors and leaves the editor open in Edit mode: fix and `-Sync` again.
- **-Eval** runs C# in the open editor (the Unity CLI's `script eval`). It dies with the Play session: anything worth
  keeping goes into the source, then `-Sync`.
- **-Shot** writes `<Out>/frames/<n>.png` at `live.size` (1600x900 by default) and appends a `frames.jsonl` line
  (`frame`, `sha256`, `t`, `unityFrame`, `label`). The frame is composed the way the player composes it: every screen
  camera by depth, then every shown screen-space UI Toolkit panel by sorting order (`VisualElement.CaptureToRenderTexture`),
  alpha-blended on top, at the panel scale a 1600x900 screen gives. The Game view's own render target is not used: on
  7000.0.0a10 it holds no runtime panels (it read black over the Movement sample's HUD). World-space UI and anything a
  camera draws are in it through the cameras. The `tools/look/*.py` scripts read a live folder unchanged.
- **-Tour** needs `live.tour` in `tools/repo.json`: the launch keys that make the game play a tour file and capture its
  frames. `-Until <label>` cuts the tour at its first line naming the label and appends `live.tour.hold`, so Play
  stays on that label (`-Replay` re-runs it to the same point). Without `-Until` the tour plays to its end and Play is
  re-entered with the plain launch line.

## The pool and the limits

- At most **two live editors on the machine**, across every repo: seats under `<tools home>/locks/live/`. They are
  apart from the lane's one-editor lock (U-55): a live editor never takes it and a lane run never waits for one.
- A seat belongs to the editor's PID; it frees itself when that editor exits (a dead or reused PID is stale).
  `-Open` waits `-WaitMinutes` (5) for a seat, then exits 3: fall back to the normal look run.
- Each editor **closes itself** after `live.idleMinutes` (10) without a live command, or `live.capMinutes` (60) after it
  started, whichever comes first (`-IdleMinutes`/`-CapMinutes` on `-Open` lower them). Every verb counts as a command.
- To lower a running editor's limits (to test the idle close, say):
  `live.ps1 -Eval 'System.Environment.SetEnvironmentVariable("UNIDAD_LIVE_IDLE_SECONDS", "30"); return "ok";'`; the
  watchdog reads `UNIDAD_LIVE_IDLE_SECONDS` / `UNIDAD_LIVE_CAP_SECONDS` on every check.
- It is registered with the launch guard, so `testbed.ps1 -StopAll` ends it, and no other tool opens or kills it.

## The window: none in front of you

`live.batchmode` (default `true`) starts the editor with `-batchmode`: no main window. It still serves the CLI, plays,
imports and recompiles. Its one window is a floating Game view the overlay makes at load, at `live.size`, so `Screen`
and every panel's scale match the look capture size; it is parked 4000 px left of every monitor, at the bottom of the
z-order, never activated, and re-parked every second (Unity clamps its own window positions onto a monitor; Win32
`SetWindowPos` does not). With `live.batchmode: false` the full editor window opens the same way (`CreateProcess` with
`SW_SHOWNOACTIVATE`, then parked off-screen by `live.ps1` until the overlay takes over). It is never minimised: a
minimised editor stops ticking (no Play frames and, worse, its idle watchdog never fires; seen on 7000.0.0a10). If the
foreground ends up on the editor anyway, `live.ps1` hands it back to the window that had it before `-Open`.

The taskbar shows it as `[live <repo>/<branch>: closes itself after <n> min idle] ...` (the window title while it
loads, then `Game`). You never need to close it: it closes itself after `live.idleMinutes` without a live command and
`live.capMinutes` in all; `-Close` ends it at once.

## Startup: zero errors

Every framework launch (`unity-lane.ps1`, `testbed.ps1`, `seed-unity.ps1`, `live.ps1 -Open`) first writes
`indexOnEditorStartup = false` into `<project>/UserSettings/Search.settings` (`Set-SearchIndexStartupOff`,
`tools/lib/UnidadTools.ps1`): on 7000.0.0a10 a project with no `UserSettings/Search.index` throws
`ArgumentOutOfRangeException` from `SearchInit.IndexationOnStartup` at every editor start (docs/DECISIONS.md
`search-index-startup`). A live open of the Sandbox logs no exception and no `LogError`.

## The overlay: nothing ships

The Unity CLI's commands come from `com.unity.pipeline`, which no game manifest carries. `-Open` writes
`tools/live/editor/` into the live slot as the embedded package `<project>/Packages/com.unidad.live` (its
`package.json` depends on `com.unity.pipeline`; its editor assembly holds the `live.*` commands and the idle watchdog)
and lists that folder in the repo's `info/exclude`; `packages-lock.json` is marked skip-worktree there. The live slot
never shows a change and nothing is ever committed from it.

## Configuring a game (`tools/repo.json`)

```json
"liveSlots": ["live1", "live2"],
"live": {
  "batchmode": true,
  "size": "1600x900",
  "idleMinutes": 10,
  "capMinutes": 60,
  "settleSeconds": 3,
  "replayPaths": ["^Assets/StreamingAssets/Content/"],
  "tour": { "set": "showcase={tour};capture={frames}", "hold": ["rec off", "wait 3600"] }
}
```

`"batchmode": false` opens the full editor window (parked, see above) instead. `lane.testbedMethod` is the door every
live Play goes through; `lane.prepare` runs after every copy. `live.tour.set`
is a `-Set` string where `{tour}` is the (cut) tour file and `{frames}` the capture folder; leave `tour` out when the
game has no tour driver. Every key has a default (`tools/lib/UnidadTools.ps1`).

## Not built

- `[MethodReload]` hot reload of game methods: it needs `com.unity.pipeline` in the shipped manifest.
- Live reload in a player build (`reload_player`), and input injection beyond what a game's own tour driver does.
