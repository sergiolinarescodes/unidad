# Migrating from unidad 0.1.0 to 2.0

Draft written by the 2.0 cut (`u0-release-cut`). Every cell marked "planned" comes from the architecture; `f4-release-pre1`
replaces it with the real type and assembly from the merged 2.0 surface.

## 1. Who is affected

A consumer whose `Packages/manifest.json` uses `https://github.com/sergiolinarescodes/unidad.git` with no `#ref`. Since
the cut, `main` has no root `package.json`, so Unity's resolve fails loudly ("no package.json" / "is not a valid
package"); it never upgrades silently to 2.0. A `packages-lock.json` keeps the old hash until someone re-resolves.

## 2. Stay on 0.x

```json
"com.unidad.core": "https://github.com/sergiolinarescodes/unidad.git#v0.1.0"
```

Fixes come from the `release/0.x` branch as tags `v0.1.N` (U-111). `file:` consumers (Experimental,
RaceConstructorDeckBuilder, TexelExp, unity-ppo-racing-trainer) carry embedded copies and are untouched.

## 3. Move to 2.0

Unity 7000.0 or newer, and three manifest lines:

```json
"com.unidad.core":    "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.core#v2.0.0",
"com.unidad.modules": "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.modules#v2.0.0",
"com.unidad.unity":   "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.unity#v2.0.0"
```

or embedded copies via `git archive` + `tools/carry-unidad.ps1` (recommended, U-05). Remove `com.gustavopsantos.reflex`
unless the game uses it directly, and remove `"testables": ["com.unidad.core"]`.

## 4. Breaking changes

- Reflex is replaced by `Unidad.Core.DI` (`ContainerBuilder` / `Container`, multi-binding).
- `Unidad.Core` is engine-free (`noEngineReferences`).
- C# 14 per assembly, with internal polyfills.
- No static state (UD014); the analyzers UD001-UD016 apply to consumers.
- `ISystemInstaller.CreateTestFactory`, `ISystemTestFactory` and `TestDependencies` are removed (U-04).
- Live MCP testing, ScenarioBrowser and `DataDrivenScenario` are removed; smoke tests run in dotnet instead.
- Tick discovery and a loud boot failure replace 0.1.0's silent `Start()` catch.
- The namespace `Unidad.Unity` never exists: the adapter is `Unidad.Adapter`, in package `com.unidad.unity`.
- Dead library services not carried (unidad-core D-core-5, planned): the `Timer`, `Inventory`, `Progression` and
  `Resource` services, `Testing/*` and every `*TestFactory`, `IRegistry`/`RegistryBase`, `CommandQueue`, `NullEventBus`,
  `EventBusFactory`, `Debugger/DebugModeService` + `IDebugProvider` (replaced by the debug seam), `JitWarmup`, the
  parameterless `InstallerDiscovery.FindInstallerTypes()`.

## 5. Namespace map

New in 2.0 (planned): `Unidad.Core.Ticking`, `.Jobs`, `.Primitives`, `.Random`, `.Diagnostics`, `.Content`, `.Motion`.
`UnidadBootstrap`, `TickRunner` and `UnityTimeProvider` move to `Unidad.Adapter` (planned).

| 0.1.0 namespace | 2.0 home | note |
|---|---|---|
| `Unidad.Core.Abstractions` | `Unidad.Core.Abstractions` in `Unidad.Core` (planned) | interfaces only |
| `Unidad.Core.Bootstrap` | `Unidad.Core.Bootstrap` in `Unidad.Core` (planned) | `ISystemInstaller`, discovery; `CreateTestFactory` dropped |
| `Unidad.Core.DI` | `Unidad.Core.DI` in `Unidad.Core` (planned) | Reflex replaced by `ContainerBuilder`/`Container`, multi-binding |
| `Unidad.Core.Debugger` | removed (planned) | `DebugModeService`/`IDebugProvider` replaced by the debug seam (D-core-5) |
| `Unidad.Core.EventBus` | `Unidad.Core.EventBus` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Factory` | `Unidad.Adapter` (`com.unidad.unity`) (planned) | `IGameObjectFactory`/`GameObjectFactory` |
| `Unidad.Core.Grid` | `Unidad.Core.Grid` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Grid.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.HistoryService` | `Unidad.Core.HistoryService` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.HistoryService.Data` | `Unidad.Core.HistoryService.Data` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.HistoryService.Query` | `Unidad.Core.HistoryService.Query` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Inventory` | removed (planned) | service not carried (D-core-5) |
| `Unidad.Core.Inventory.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.LiveTesting` | removed (planned) | smoke tests in dotnet instead |
| `Unidad.Core.ModelCatalog` | `Unidad.Adapter.Models` (planned) |  |
| `Unidad.Core.ModelCatalog.Effects` | `Unidad.Adapter.Models` (planned) |  |
| `Unidad.Core.ModelCatalog.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.ModelCatalog.Views` | `Unidad.Adapter.Models` (planned) |  |
| `Unidad.Core.ObjectPool` | `Unidad.Adapter` (planned) | `ComponentPool`/`PoolRegistry` |
| `Unidad.Core.Patterns.CommandQueue` | removed (planned) | not carried (D-core-5) |
| `Unidad.Core.Patterns.Modifier` | `Unidad.Core.Patterns.Modifier` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Patterns.Scoring` | `Unidad.Core.Patterns.Scoring` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Patterns.StateMachine` | `Unidad.Core.Patterns.StateMachine` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Physics` | removed (planned) |  |
| `Unidad.Core.Physics.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Physics2D` | removed (planned) |  |
| `Unidad.Core.Physics2D.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Progression` | removed (planned) | service not carried (D-core-5) |
| `Unidad.Core.Progression.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Registry` | removed (planned) | `IRegistry`/`RegistryBase` not carried (D-core-5) |
| `Unidad.Core.Rendering` | removed (planned) |  |
| `Unidad.Core.Resource` | removed (planned) | service not carried (D-core-5) |
| `Unidad.Core.Resource.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Systems` | `Unidad.Core.Systems` in `Unidad.Core` (planned) | same namespace, engine-free |
| `Unidad.Core.Testing` | removed (planned) | not carried (D-core-5); smoke tests use hand-written fakes |
| `Unidad.Core.Timer` | removed (planned) | service not carried (D-core-5) |
| `Unidad.Core.Timer.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.UI` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Components` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Core` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.DesignSystem` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Dialog` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Events` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Integration.TextAnimations` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary`; TextAnimations integration dropped |
| `Unidad.Core.UI.TextAnimation` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary`; TextAnimations integration dropped |
| `Unidad.Core.UI.TextAnimation.ElementAnimation` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary`; TextAnimations integration dropped |
| `Unidad.Core.UI.Tooltip` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.UI.Tooltip.Scenarios` | removed (planned) | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.UI.WorldSpace` | removed (planned) | UI Toolkit through the adapter's `PanelLibrary` |
| `Unidad.Core.DOTS` | removed (planned) |  |
| `Unidad.Core.DOTS.Bridge` | removed (planned) |  |
| `Unidad.Core.Editor.LiveTesting` | removed (planned) |  |
| `Unidad.Core.Editor.LiveTesting.Mcp` | removed (planned) |  |
| `Unidad.Core.Editor.Mcp` | removed (planned) |  |
| `Unidad.Core.Editor.PicoCad` | `com.unidad.unity/Editor/PicoCad` (`Unidad.Adapter.Editor`) (planned) |  |
| `Unidad.Core.Editor.ScenarioBrowser` | removed (planned) |  |

## 6. Assembly names

| 0.1.0 assembly | 2.0 |
|---|---|
| `Unidad.Core.Runtime` | `Unidad.Core` (+ `Unidad.Adapter` for engine parts) (planned) |
| `Unidad.Core.Editor` | `Unidad.Adapter.Editor` (planned) |
| `Unidad.Core.ModelCatalog` | `Unidad.Adapter` (planned) |
| `Unidad.Core.ModelCatalog.Editor` | `Unidad.Adapter.Editor` (planned) |
| `Unidad.Core.DOTS*` | removed (planned) |
| `Unidad.Core.UI.Integration.*` | removed (planned) |
| `Unidad.Core.Editor.LiveTesting*`, `*.Mcp` | removed (planned) |
| `*.Tests` | removed (planned) |
