# Migrating from unidad 0.1.0 to 2.0

Written by the 2.0 cut (`u0-release-cut`); every cell of the maps below was checked against the merged surface of
`v2.0.0-pre.1` (`f4-release-pre1`).

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
- Dead library services not carried (unidad-core D-core-5): the `Timer`, `Inventory`, `Progression` and
  `Resource` services, `Testing/*` and every `*TestFactory`, `IRegistry`/`RegistryBase`, `CommandQueue`, `NullEventBus`,
  `EventBusFactory`, `Debugger/DebugModeService` + `IDebugProvider` (replaced by the debug seam), `JitWarmup`, the
  parameterless `InstallerDiscovery.FindInstallerTypes()`.

## 5. Namespace map

New in 2.0: `Unidad.Core.Ticking`, `.Jobs`, `.Primitives`, `.Random`, `.Diagnostics`, `.Content`, `.Motion`.
`UnidadBootstrap`, `TickRunner` and `UnityTimeProvider` move to `Unidad.Adapter`.
The adapter adds `Unidad.Adapter.Models`, `.Motion`, `.Stage`, `.Ui` and `Unidad.Adapter.Editor` (+ `.PicoCad`). The
generic modules of `com.unidad.modules` are new assemblies with namespaces of the same name: `Unidad.Launch`,
`Unidad.Flow`, `Unidad.Text`, `Unidad.Save`, `Unidad.Audio`.

| 0.1.0 namespace | 2.0 home | note |
|---|---|---|
| `Unidad.Core.Abstractions` | `Unidad.Core.Abstractions` in `Unidad.Core` | interfaces only |
| `Unidad.Core.Bootstrap` | `Unidad.Core.Bootstrap` in `Unidad.Core` | `ISystemInstaller`, discovery; `CreateTestFactory` dropped |
| `Unidad.Core.DI` | `Unidad.Core.DI` in `Unidad.Core` | Reflex replaced by `ContainerBuilder`/`Container`, multi-binding |
| `Unidad.Core.Debugger` | removed | `DebugModeService`/`IDebugProvider` replaced by the debug seam: `IDebugModeState`, `IDebugSection`, `IDebugUi` in `Unidad.Core.Diagnostics` (D-core-5) |
| `Unidad.Core.EventBus` | `Unidad.Core.EventBus` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Factory` | `Unidad.Adapter` (`com.unidad.unity`) | `IGameObjectFactory` (public), `GameObjectFactory` (internal) |
| `Unidad.Core.Grid` | `Unidad.Core.Grid` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Grid.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.HistoryService` | `Unidad.Core.HistoryService` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.HistoryService.Data` | `Unidad.Core.HistoryService.Data` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.HistoryService.Query` | `Unidad.Core.HistoryService.Query` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Inventory` | removed | service not carried (D-core-5) |
| `Unidad.Core.Inventory.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.LiveTesting` | removed | smoke tests in dotnet instead |
| `Unidad.Core.ModelCatalog` | `Unidad.Adapter.Models` in `Unidad.Adapter` | `IModelCatalogService` -> `IModelCatalog` + `IModelSource` + `ModelEntry`; `ModelCatalogService` -> internal `ModelCatalog`; `ModelCatalogInstallerBase`, `LocalGameObjectFactory`, `ModelEffectPreview`, `PrimeTweenAnimationResolver`, `ModelCatalogTestFactory` not carried |
| `Unidad.Core.ModelCatalog.Effects` | `Unidad.Adapter.Models` in `Unidad.Adapter` | `IModelEffectProfile`/`BounceEffectProfile` -> `IModelDecorator` + `ModelEffects.Flash`; motion through `Unidad.Adapter.Motion` |
| `Unidad.Core.ModelCatalog.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.ModelCatalog.Views` | removed | `ModelViewBase` not carried; spawn through `IGameObjectFactory.InstantiateModel` |
| `Unidad.Core.ObjectPool` | `Unidad.Adapter` | `GameObjectPool` -> `ComponentPool<T>`, `PoolRegistry`; `IPoolable` -> `Unidad.Core.Abstractions` |
| `Unidad.Core.Patterns.CommandQueue` | removed | not carried (D-core-5) |
| `Unidad.Core.Patterns.Modifier` | `Unidad.Core.Patterns.Modifier` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Patterns.Scoring` | `Unidad.Core.Patterns.Scoring` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Patterns.StateMachine` | `Unidad.Core.Patterns.StateMachine` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Physics` | removed |  |
| `Unidad.Core.Physics.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Physics2D` | removed |  |
| `Unidad.Core.Physics2D.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Progression` | removed | service not carried (D-core-5) |
| `Unidad.Core.Progression.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Registry` | `Unidad.Core.Registry` in `Unidad.Core` | `IRegistry`/`RegistryBase` not carried (D-core-5); the namespace now holds `HandlerRegistry` and `OrderedRegistry` |
| `Unidad.Core.Rendering` | removed |  |
| `Unidad.Core.Resource` | removed | service not carried (D-core-5) |
| `Unidad.Core.Resource.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.Systems` | `Unidad.Core.Systems` in `Unidad.Core` | same namespace, engine-free |
| `Unidad.Core.Testing` | removed | not carried (D-core-5); smoke tests use hand-written fakes |
| `Unidad.Core.Timer` | removed | service not carried (D-core-5) |
| `Unidad.Core.Timer.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.UI` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Components` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Core` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.DesignSystem` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Dialog` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Events` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Integration.TextAnimations` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`); TextAnimations integration dropped |
| `Unidad.Core.UI.TextAnimation` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`); TextAnimations integration dropped |
| `Unidad.Core.UI.TextAnimation.ElementAnimation` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`); TextAnimations integration dropped |
| `Unidad.Core.UI.Tooltip` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.UI.Tooltip.Scenarios` | removed | scenario harness gone; smoke tests in dotnet |
| `Unidad.Core.UI.WorldSpace` | removed | UI Toolkit through the adapter's `IPanelLibrary` (`Unidad.Adapter.Ui`) |
| `Unidad.Core.DOTS` | removed |  |
| `Unidad.Core.DOTS.Bridge` | removed |  |
| `Unidad.Core.Editor.LiveTesting` | removed |  |
| `Unidad.Core.Editor.LiveTesting.Mcp` | removed |  |
| `Unidad.Core.Editor.Mcp` | removed |  |
| `Unidad.Core.Editor.PicoCad` | `Unidad.Adapter.Editor.PicoCad` in `Unidad.Adapter.Editor` | `PicoCadPrefabBuilder` (no Animator, target folder a parameter), `PicoCadManifest`; `ModelCatalogRegistry`, `ModelEffectPreviewEditor`, `PicoCadMcpTools` not carried |
| `Unidad.Core.Editor.ScenarioBrowser` | removed |  |

## 6. Assembly names

| 0.1.0 assembly | 2.0 |
|---|---|
| `Unidad.Core.Runtime` | `Unidad.Core` (+ `Unidad.Adapter` for engine parts) |
| `Unidad.Core.Editor` | `Unidad.Adapter.Editor` |
| `Unidad.Core.ModelCatalog` | `Unidad.Adapter` |
| `Unidad.Core.ModelCatalog.Editor` | `Unidad.Adapter.Editor` |
| `Unidad.Core.DOTS*` | removed |
| `Unidad.Core.UI.Integration.*` | removed |
| `Unidad.Core.Editor.LiveTesting*`, `*.Mcp` | removed |
| `*.Tests` | removed; each assembly has a dotnet smoke project (`dotnet/Unidad.*.Smoke`) |
