---
name: live-mcp-test
description: Build a Live MCP Test for a Unidad feature — a dedicated Play-Mode scene Claude drives over the Unity MCP connection, triggering named actions (also editor buttons) and asserting on real numeric/bool probe values. Use when adding/feature-testing any new system under the Bootstrap/DI architecture, or when the user says "add a live test", "live MCP test", "test this feature live", or works under Assets/Scripts with a service that needs end-to-end verification.
---

# Live MCP Test — build & run a feature's live Play-Mode test

A Live MCP Test is the **mandatory** end-to-end test for a new feature: one Play-Mode scene per
feature cluster, exposing named **actions** (the picoCAD-style buttons) and numeric/bool **probes**.
Claude loads the scene, enters Play Mode, triggers actions over MCP, advances physics
**deterministically**, and asserts on probe values. It coexists with `DataDrivenScenario`.

The canonical, working reference is the **2D Movement** feature — copy it:
- Harness (framework, reusable): `Packages/com.unidad.core/Runtime/LiveTesting/*` and
  `Packages/com.unidad.core/Editor/LiveTesting/*` (+ `.../Mcp/*`).
- Example feature (game): `Assets/Scripts/Movement/*` and `Assets/Scenes/LiveTests/MovementLiveTest.unity`.

## When to use
Every new feature cluster under the Bootstrap/DI architecture MUST ship a Live MCP Test before it is
"done". Group by manageable scene: all movement actions in one scene; a world map is a separate scene.

## Recipe (copy Movement)
For a feature `<Feature>` in `Assets/Scripts/<Feature>/`:

1. **Service + interface** — `public interface I<Feature>Service` (exposes a `State` struct = the probe
   source of truth + the verbs) and `internal sealed class <Feature>Service : SystemServiceBase, IFixedTickable`.
   All RULES live here (logic-vs-presentation). No `MonoBehaviour.Update`, no direct `UnityEngine.Time`
   (inject `ITimeProvider`; `FixedTick(dt)` gets the step). Build objects via `IGameObjectFactory`.
   Log events with `Debug.Log("[<Feature>Service] ...")` so console asserts work.
2. **Events** — `public readonly record struct` via `IEventBus`.
3. **Installer** — `public sealed class <Feature>Installer : ISystemInstaller`; registers the service;
   `CreateTestFactory()` returns a `<Feature>TestFactory` (its `GetScenarios()` may be empty — the Live
   MCP Test is the coverage; document that in the factory).
4. **Live test scene object** — `internal sealed class <Feature>LiveTestScene : LiveTestSceneBase`,
   annotated `[LiveTestScene("<id>", "<Name>", "Assets/Scenes/LiveTests/<Feature>LiveTest.unity")]`.
   In its ctor: register `Action(id, name, args => svc.Verb(...))`, `Probe(id, () => LiveTestValue.Of(svc.State.X))`,
   and `BuildPlan()` using `Step(label, actionId, Args(...), wait, Until(...), Check(...))`. Prefer
   `Until("probe", op, value, maxSteps)` over hardcoded wait counts.
5. **Bootstrap** — `public sealed class <Feature>LiveTestBootstrap : UnidadBootstrap`:
   - `RegisterInstallers`: add any dependency installers FIRST (e.g. `Physics2DSystemInstaller`), then `<Feature>Installer`.
   - `ResolveFixedTickables`: return `(IFixedTickable)container.Resolve<I<Feature>Service>()`.
   - `OnContainerReady`: set `Physics2D.simulationMode = SimulationMode2D.Script;` (+ `queriesStartInColliders = false`
     for grounded raycasts), `svc.SpawnLevel()`, then
     `LiveTestRegistry.SetActive(new <Feature>LiveTestScene(svc), dt => { GetComponentInChildren<TickRunner>().FixedTickAll(dt); Physics2D.Simulate(dt); });`
   - `OnDisable`: restore `simulationMode = FixedUpdate` and `LiveTestRegistry.Clear()`.
6. **Scene** — author `Assets/Scenes/LiveTests/<Feature>LiveTest.unity` via MCP: a `SceneScope` object
   (Reflex.Core.SceneScope) with the bootstrap as its **child** (Reflex `SceneScope` auto-discovers child
   `IInstaller`s — no serialized wiring), plus an orthographic 2D camera. See the Movement scene-authoring
   `Unity_RunCommand` for the exact non-destructive (additive) script.

## Gotchas (learned from Movement)
- **2D physics on a factory primitive**: `IGameObjectFactory.CreatePrimitive` leaves a 3D `Collider`
  scheduled for *deferred* `Object.Destroy`; a `Collider2D`/`Rigidbody2D` **cannot** be added while it's
  present (`AddComponent` returns null). `Object.DestroyImmediate(go.GetComponent<Collider>())` first.
- **Dependency installers**: resolve order matters — install what your service `Resolve`s (e.g.
  `Physics2DSystemInstaller` provides `IPhysics2DService` + `IPhysics2DEntityRegistry`) BEFORE your installer.
- **Grounded raycast**: put ground on a dedicated layer (`Unity_ManageEditor AddLayer`), cast from the body
  center with `queriesStartInColliders = false` and a layer mask so it never self-hits.
- **`[McpTool]` registration**: new MCP tools auto-register at editor load via the
  `Unity.AI.MCP.Editor.ToolRegistry`; the asmdef must reference `Unity.AI.MCP.Editor` and gate on
  `UNIDAD_UNITY_MCP` (versionDefine `com.unity.ai.assistant`). Editing scripts triggers a domain reload that
  briefly drops the MCP connection — wait/poll `Unity_ManageEditor GetState`, reconnect with `/mcp` if needed.

## Verify (the MCP loop)
```
Unity_ManageScene Load  <scenePath>
Unity_ManageEditor Play           # poll GetState until IsPlaying
Unidad.LiveTest.List              # confirm activeId == "<id>"
Unidad.LiveTest.Describe          # read the scripted Plan
Unidad.LiveTest.RunPlan           # runs all cases; returns checks[] pass/fail
# on failure, granular: RunAction -> Step/StepUntil -> Probe/Assert; Unity_GetConsoleLogs for [<Feature>Service] events
# tune forces/steps live until every case passes (the deterministic stepping makes this reproducible)
Unity_ManageEditor Stop
```
The MCP tools (`mcp__unity-mcp__Unidad_LiveTest_*`): `List, Describe, RunAction{actionId,args}, Step{fixedSteps},
StepUntil{probeId,op,operand,maxSteps}, Probe, Assert{probeId,op,operand,tolerance}, RunPlan, ListAll`.
The user can drive the SAME actions manually via `Window > Unidad > Live Test Panel`.

When the feature's `RunPlan` is green and committed, the feature is "done".
