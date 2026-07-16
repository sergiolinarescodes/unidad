# Movement Live MCP Test (sample)

The reference example for the **Live MCP Testing** pattern (see `Documentation~/LiveMcpTesting.md`):
a 2D side-scroller box (dynamic `Rigidbody2D`, real gravity) on two platforms with a hole, with a
full Live MCP Test of five cases — jump, move L/R, move + jump together, run-and-clear-the-hole, and
the unhappy "walk into the hole and fall off".

## Import
Package Manager → **Unidad Core** → **Samples** → **Movement Live MCP Test** → *Import*. Unity copies
it to `Assets/Samples/Unidad Core/<version>/Movement Live MCP Test/`.

## Run it
1. Open the imported `MovementLiveTest.unity`.
2. Enter Play Mode (the bootstrap spawns the level and registers the live test).
3. Drive it:
   - **Manually**: `Window → Unidad → Live Test Panel` → click actions / **Run Plan**.
   - **Over MCP**: `Unidad.LiveTest.List` → `Unidad.LiveTest.RunPlan` (expects 24/24 checks).

## Contents
- `Scripts/` — `IMovementService` + `MovementService` (logic), installer, test factory, the
  `[LiveTestScene]` `MovementLiveTestScene` (actions/probes/Plan), and `MovementLiveTestBootstrap`
  (sets `Physics2D.simulationMode = Script`, builds the level, registers with `LiveTestRegistry`).
- `MovementLiveTest.unity` — a `SceneScope` with the bootstrap as its child, plus an ortho 2D camera.

## Notes
- The sample expects a **`Ground` physics layer** (`MovementService` grounds via `LayerMask.GetMask("Ground")`) — add it under Tags & Layers if your project doesn't have one.
- For the package's own tests (convention tests, `AllSystemScenariosTests`) to run in your project, add `"testables": ["com.unidad.core"]` to `Packages/manifest.json`.
- The `[LiveTestScene]` scene path and `ScenePath` point at the default import location
  (`Assets/Samples/Unidad Core/0.1.0/Movement Live MCP Test/MovementLiveTest.unity`). If you move the
  scene or bump the package version, update those two strings in `MovementLiveTestScene.cs`.
- The level is built at runtime via `IGameObjectFactory`; `respawn` resets it. Forces/step counts in
  the Plan were tuned live — re-tune with `RunAction`/`Step`/`Probe` if you change the geometry.
