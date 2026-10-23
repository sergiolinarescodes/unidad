# Unidad

Unidad 2.0 is a Unity framework with an engine-free core: plain C# (netstandard2.1, C# 14) for the container, events,
ticking, content, save, text, audio, flow and launch, plus one Unity adapter that hosts it in a scene. It is a port of
the Godot edition (`10-unidad-godot`), so game rules build and smoke-test with `dotnet` and Unity is only needed for
the view.

Three packages, always released together with one version:

| Package | Assembly | What |
|---|---|---|
| `com.unidad.core` | `Unidad.Core` | engine-free kernel: DI, events, ticking, content, motion, randomness, diagnostics, analyzers |
| `com.unidad.modules` | `Unidad.Flow`, `Unidad.Save`, `Unidad.Text`, `Unidad.Audio`, `Unidad.Launch` | engine-free generic modules |
| `com.unidad.unity` | `Unidad.Adapter` | the Unity adapter, the modules' Unity hosts, the picoCAD model import |

## Install (Unity 7000.0+)

The recommended way is an embedded copy: `git archive <tag> Packages/com.unidad.core Packages/com.unidad.modules
Packages/com.unidad.unity` into your project's `Packages/` (games carry it with `tools/carry-unidad.ps1`). As git
dependencies, add all three to `Packages/manifest.json`:

```json
"com.unidad.core":    "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.core#v2.0.1",
"com.unidad.modules": "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.modules#v2.0.1",
"com.unidad.unity":   "https://github.com/sergiolinarescodes/unidad.git?path=/Packages/com.unidad.unity#v2.0.1"
```

## 0.x

Unidad 0.1.0 (Reflex, Unity 6000.0, the repo root as the package) stays at tag `v0.1.0`:
`"com.unidad.core": "https://github.com/sergiolinarescodes/unidad.git#v0.1.0"`. Fixes land on `release/0.x` as
`v0.1.N`. Moving from 0.1.0 to 2.0: [docs/MIGRATING-0.x.md](docs/MIGRATING-0.x.md).

## Kept for the picoCAD pipeline

`Tools~/`, `Skills~/` and `Documentation~/model-pipeline/` stay at the root, byte-identical to 0.1.0, because the global
`picocad-model` skill clones `main` and reads them. They still describe 0.1.0 paths on purpose; changing them needs the
global skill changed in the same step (U-01, U-112). `tools/release-check.ps1 -Check KeptTrees` guards them.

## License

MIT, see [LICENSE](LICENSE).
