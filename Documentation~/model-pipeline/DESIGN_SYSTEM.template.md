# {{PROJECT_NAME}} — Model Design System

> Scaffolded by `/picocad-model` on first run. The workflow reads and enforces this
> document on every model it creates; agents flag deviations instead of silently
> fixing them. Update it deliberately — it is the contract between picoCAD2
> authoring and Unity.
>
> Reference standard for style docs: the `character_styles/<NN>_<style>/STYLE.md`
> pattern (palette story, silhouette rules, proportions, texture budget, naming).

## Identity

- **Project**: {{PROJECT_NAME}}
- **Visual direction (one sentence)**: {{VISUAL_DIRECTION}}
- **Render target**: URP, point-filtered 128×128 textures, no mipmaps (pixel-art 3D).

## Units & scale

- **1 Unity unit = {{UNIT_MEANING}}** (e.g. "1 meter", "1 tile").
- Reference heights (measured in Unity units after import):
  - {{ARCHETYPE_1}}: {{HEIGHT_1}}
  - {{ARCHETYPE_2}}: {{HEIGHT_2}}
- picoCAD2 models author at native scale; the **kind's `unitScale`** (kinds.json)
  normalizes to the heights above. The verify agent measures rendered bounds
  against this table.

## Orientation conventions

- Characters/props face **+Z in Unity** (authored facing −Z in picoCAD2 — the
  converter's handedness flip takes care of it; do not pre-rotate in picoCAD2).
- Pivot at the feet/base: author with feet resting at **y = 0** in picoCAD2.

## Palette rules

- {{PALETTE_POLICY}} (e.g. "one shared 16-color palette for everything" or
  "per-kind palettes, but slots 0–3 reserved for global accents").
- Transparent slot policy: {{TRANSPARENT_POLICY}} (which palette index is the
  cutout color and what it may be used for).
- Shade ramps: every color must have valid `shade_pal_1`/`shade_pal_2` entries.

## Texture layout (128×128)

- Rows 0–15: palette strip (mandatory, leave intact).
- Rows 16–127: free texture space. {{TEXTURE_LAYOUT_POLICY}}
  (e.g. "top half = body/face regions, bottom half = props/effects").

## Naming

- picoCAD2 project files: `{{SOURCE_NAMING}}` (e.g. `<kind>_<name>.txt`).
- Prefabs: `{{PREFAB_NAMING}}` (e.g. `<Name>` plain, folder conveys the kind).
- Scene-graph node names become Unity child names and **animation paths** — keep
  them lowercase_snake, unique, and stable across variants of a kind.

## Folders

- Source of truth: picoCAD2 saves under `%APPDATA%/picocad2/{{SOURCE_FOLDER_PATTERN}}`.
- Unity: `Assets/Resources/Models/<KindFolder>/` (prefab + clips + controller),
  `Assets/PicoCadImports/<name>/` (gltf/bin/png/manifest — pipeline-owned).

## Animation conventions

- Baked picoCAD2 motion tracks: loop-friendly (end where they start) unless the
  kind says otherwise; default Animator state is the `Motion` clip.
- Code-driven effects (PrimeTween): every kind defines at least
  `spawn` / `despawn`; {{EFFECT_POLICY}}.

## Kinds

> One subsection per kind, appended by the workflow when a NEW kind is created.
> Machine-readable mirror: `Assets/Resources/ModelCatalog/kinds.json`.

<!-- kinds:begin -->
<!-- kinds:end -->

## Changelog

<!-- changelog:begin -->
<!-- changelog:end -->
