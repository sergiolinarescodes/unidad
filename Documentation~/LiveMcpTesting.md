# Live MCP Testing

A harness for testing a feature **live**: a dedicated Play-Mode scene that an AI agent (or a
developer) drives over the Unity MCP connection — triggering named actions and asserting on real,
numeric/bool state read from the running scene. It complements `DataDrivenScenario` (which stays for
visual/setup checks); it does not replace it.

Unlike an edit-mode scenario, a Live MCP Test exercises the *real* running system (real physics, real
DI-wired services) and verifies behavior end-to-end while the scene is actually playing.

## Pieces

Runtime (`Runtime/LiveTesting/`, assembly `Unidad.Core.Runtime`):
- `ILiveTestScene` / `LiveTestSceneBase` — a feature's live test: a stable `Id`/`Name`/`ScenePath`, a
  set of **actions** (`LiveTestAction` — the buttons), **probes** (`LiveTestProbe` — the asserted
  values), and a scripted **Plan** (`LiveTestStep`s with actions + waits + `LiveTestAssertion`s).
- `[LiveTestScene(id, name, scenePath)]` — instantiation-free metadata so the runner can enumerate
  every live test without constructing the (service-bound) scene object.
- `LiveTestRegistry` — the static bridge. A scene's bootstrap registers the active `ILiveTestScene`
  plus a `FixedStep` callback here at Play start; Editor MCP tools + the panel read from it without
  needing to know any game-specific service interface type.
- `LiveTestAsserter` — evaluates assertions against a probe snapshot, reusing `ScenarioVerificationResult`.

Editor (`Editor/LiveTesting/`, assembly `Unidad.Core.Editor.LiveTesting`):
- `LiveTestPlanDriver` — runs a Plan (and ad-hoc `Step`/`StepUntil`) by driving `LiveTestRegistry.FixedStep`.
- `LiveTestPanelWindow` — `Window > Unidad > Live Test Panel`: one button per action (same delegate the
  MCP tools call), Step buttons, "Run Plan", and a live probe read-out.

MCP tools (`Editor/LiveTesting/Mcp/`, gated on `UNIDAD_UNITY_MCP` = `com.unity.ai.assistant` present):
`Unidad.LiveTest.{List, Describe, RunAction, Step, StepUntil, Probe, Assert, RunPlan, ListAll}` —
surfaced over MCP as `mcp__unity-mcp__Unidad_LiveTest_*`.

## The key design: deterministic stepping

A synchronous MCP tool cannot pump Unity frames (that would deadlock the editor), and real-time play
only advances when the editor is actively ticking. So a live-test scene runs
`Physics2D.simulationMode = Script` and advances the world **on demand**, one fixed step at a time:

```
FixedStep = dt => { tickRunner.FixedTickAll(dt); Physics2D.Simulate(dt); }  // FixedTick BEFORE Simulate
```

`Physics2D.Simulate(0.02)` is mathematically identical to one real-time `FixedUpdate` — same
integration, gravity, collisions — but unpaced, so a whole Plan runs in one call and every assertion
is reproducible and latency-independent. Prefer `StepUntil(probe, op, value, maxSteps)` over hardcoded
wait counts.

## Recipe — add a Live MCP Test for `<Feature>`

1. **Service** — `public interface I<Feature>Service` (expose a `State` struct = the probe source of
   truth + the verbs) and `internal sealed <Feature>Service : SystemServiceBase, IFixedTickable`. All
   rules live here (logic, not in the scene). No `MonoBehaviour.Update`; inject `ITimeProvider`. Build
   objects via `IGameObjectFactory`. Log events with `Debug.Log("[<Feature>Service] ...")`.
2. **Installer** — `<Feature>Installer : ISystemInstaller`; `CreateTestFactory()` returns a factory
   whose `GetScenarios()` may be empty (the Live MCP Test is the coverage).
3. **Live test scene object** — `<Feature>LiveTestScene : LiveTestSceneBase` with
   `[LiveTestScene("<id>","<Name>","Assets/Scenes/LiveTests/<Feature>LiveTest.unity")]`; register
   `Action`/`Probe` and `BuildPlan()` with `Step(...)`.
4. **Bootstrap** — `<Feature>LiveTestBootstrap : UnidadBootstrap`:
   - `RegisterInstallers`: add dependency installers FIRST (e.g. `Physics2DSystemInstaller`), then `<Feature>Installer`.
   - `ResolveFixedTickables`: return `(IFixedTickable)c.Resolve<I<Feature>Service>()`.
   - `OnContainerReady`: `Physics2D.simulationMode = Script` (+ `queriesStartInColliders = false` for
     grounded raycasts), build the level, then `LiveTestRegistry.SetActive(scene, dt => { GetComponentInChildren<TickRunner>().FixedTickAll(dt); Physics2D.Simulate(dt); })`.
   - `OnDisable`: restore `simulationMode = FixedUpdate` and `LiveTestRegistry.Clear()`.
5. **Scene** — a `SceneScope` (Reflex) with the bootstrap as its **child** (SceneScope auto-discovers
   child `IInstaller`s — no serialized wiring), plus an orthographic 2D camera.

## Verify (the MCP loop)

```
Unity_ManageScene Load  <scenePath>
Unity_ManageEditor Play          # poll GetState until IsPlaying
Unidad.LiveTest.List             # confirm activeId
Unidad.LiveTest.RunPlan          # runs every case; returns checks[] pass/fail
Unity_ManageEditor Stop
```
On failure, drill in with `RunAction → Step/StepUntil → Probe/Assert` and `Unity_GetConsoleLogs`, and
tune values live (no recompile) before baking the final numbers into the Plan.

## Gotchas

- **2D physics on a factory primitive**: `IGameObjectFactory.CreatePrimitive` leaves a 3D `Collider`
  scheduled for *deferred* `Object.Destroy`; a `Collider2D`/`Rigidbody2D` cannot be added while it is
  present (`AddComponent` returns null). `Object.DestroyImmediate(go.GetComponent<Collider>())` first.
- **Grounded raycast**: put ground on a dedicated layer, cast from the body center with
  `queriesStartInColliders = false` + a layer mask so it never self-hits.
- **Dependency installers**: install what your service `Resolve`s before your installer.
- **Run-all is orchestrated, not one call**: `Unidad.LiveTest.ListAll` then, per scene,
  Load → Play → RunPlan → Stop — domain reloads make a single cross-scene call impossible.
- **Unity MCP relay allows one client**: define the `unity-mcp` server in a single scope; a duplicate
  scope (user + local) spawns two competing relay clients that disconnect each other.
