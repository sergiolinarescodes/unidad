# Sources

Where unidad 2.0's code comes from, and every place it diverges. Merges by union (`.gitattributes`): append rows, never
rewrite them.

## Upstream

| Source | Commit | What |
|---|---|---|
| 10-unidad-godot | 977cacd | Unidad.Core, the generic modules (Flow, Save, Text, Audio, Launch), the analyzers, tools, ported to netstandard2.1 + C# 14 |
| unidad 0.1.0 | ad1524f | the picoCAD import half (`Editor/PicoCad`, `ModelCatalog`) ported into `com.unidad.unity`; `Tools~/`, `Skills~/`, `Documentation~/model-pipeline/` kept byte-identical |

## Divergences

| PR | Path | Why |
|---|---|---|
| f1-adapter-hosts-ui | `com.unidad.unity/Runtime/{Ui,Flow,Save,Audio,DebugMode,Input}`, `Runtime/Pure/{Flow,Save,Audio,DebugMode}`, `Runtime/Resources/Unidad/Ui` | 10's `Game/{Flow,Save,Audio,Debug}` hosts as UI Toolkit documents and Input System actions; the decisions they host are pure classes under `Runtime/Pure` (U-187..U-193); `AudioBusLayout`, the grid/pointer debug sections and `UiKit` are not carried |
| #9 | `Packages/com.unidad.core/Runtime/Primitives/DetMath.cs` | `Math.ScaleB` is missing from netstandard2.1: `Exp` scales by exact powers of two built from exponent bits (U-144) |
| #9 | `Packages/com.unidad.core/Runtime/Patterns/Modifier/ModifierStack.cs` | stable insertion sort instead of 10's unstable `List.Sort` (U-134, U-142) |
| #9 | `Packages/com.unidad.core/Runtime/Ticking/TickSchedule.cs` | pause, `RunWhilePaused` on the unscaled delta, `StepGroup`, `Add(TickBinding)` (U-122, U-141) |
| #9 | `Packages/com.unidad.core/Runtime/Registry/HandlerRegistry.cs`, `DI/Container.cs` | `ResolveKeyed` / `HandlerRegistry.ById`: new in 2.0, no 10 source (U-121, U-140) |
| #9 | `Packages/com.unidad.core/Runtime/Grid/Grid.cs`, `Primitives/DetMath.cs`, `Diagnostics/Log.cs` | `#pragma warning disable UD014` on immutable static arrays and the log seam (U-144) |
| #9 | `Packages/com.unidad.core/Runtime/Jobs/FrameWorkQueue.cs` | nullable annotations removed (U-128) |
