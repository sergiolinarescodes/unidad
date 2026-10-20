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
