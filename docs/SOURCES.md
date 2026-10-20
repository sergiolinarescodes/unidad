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
