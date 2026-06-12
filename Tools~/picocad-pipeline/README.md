# picocad-pipeline

Pure-Node (zero dependency) converter from **picoCAD 2** save files to
Unity-ready assets, plus the supporting test suite. Part of the
`/picocad-model` workflow (see `Documentation~/model-pipeline/PIPELINE.md` in
this package). Lives inside the Unidad package; per-project state (config,
out/, jobs/) lives in the consuming project's `tools/picocad-pipeline/`.

## Usage

```
node convert.js <model.txt> --out <dir> [--scale N] [--name <override>]
```

Emits into `<dir>`:

| File | Purpose |
|---|---|
| `<name>.gltf` + `<name>.bin` | **Primary Unity artifact** — named node hierarchy with local transforms (real pivots), per-corner vertices, NEAREST samplers, `alphaMode: MASK` when the transparent palette slot is used, and TRS animation channels baked from picoCAD motion tracks |
| `<name>.png` | 128×128 RGBA texture, `transparent_color` → alpha 0 |
| `<name>.obj` + `<name>.mtl` | Parity artifact — byte-identical to picoCAD 2's own OBJ export (golden-tested), not consumed by Unity |
| `<name>.manifest.json` | Pipeline sidecar: palette, transparency flag, animation names, source path |

## Why a custom converter

picoCAD 2 (Love2D app) has no CLI; its built-in OBJ/GLTF exports are GUI-only
and **flatten the hierarchy and drop all animation data**. This converter parses
the save file (single-line JSON) directly. Motion-track evaluation
(`lib/motion.js`) is an exact port of the app's own `motion.lua` (sine
oscillation via `times`, one-shot easings linear/quint/soft/elastic/bounce/
instant/pinch + pingpong, rot deltas stored in turns, clips chained in
start-order, looping at `t % motion_duration`).

## Tests

```
node test/golden.test.js     # converter vs the app's own exports (byte/pixel level)
node test/gltf-check.js out/<name>/<name>.gltf   # structural glTF validity
```

`golden.test.js` discovers every `<model>.txt` + `<model>.obj` pair in the
picoCAD2 data dir (`%APPDATA%/picocad2` by default, or pass a dir) and requires
byte-identical OBJ/MTL and pixel-identical texture RGB.

## Config

`pipeline.config.json` holds every project-specific path (picocad dir, Unity
target folders, registries, fps). Adopters edit this file only.
