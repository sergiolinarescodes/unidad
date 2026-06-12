# Interview scripts

Use AskUserQuestion. Keep to 2 rounds max per checkpoint; offer concrete options
with a recommended default. Record every answer verbatim in the job file.

## Design system (first run only — fills DESIGN_SYSTEM.template.md)

Round 1:
1. **Visual direction** — one sentence ("cozy low-poly dungeon", "toy-box sci-fi").
2. **Unit meaning** — what 1 Unity unit represents (meter / tile / hero-height).
   Options: "1 m", "1 tile", "1 = hero height".
3. **Reference heights** — for the 2-3 most common archetypes the user expects
   (character, prop, building): target height in Unity units.

Round 2:
4. **Palette policy** — shared global 16-color palette vs per-kind palettes
   (recommend: per-kind, slots 0-3 reserved global).
5. **Transparent slot policy** — which palette index is the cutout color.
6. **Naming** — source file pattern + prefab naming (recommend:
   `<kind>_<name>.txt` and plain `<Name>` prefab in a kind folder).
7. **Effect policy** — beyond spawn/despawn, which baseline effects every kind
   should define (hit-flash? hop? none).

## New kind (checkpoint C)

1. **Kind id + display name** (lowercase_snake id).
2. **Folder** under `Assets/Resources/Models/`.
3. **Unit scale / target height** — pick from the design system reference table
   or give a new height (becomes `unitScale` after first import measures bounds).
4. **Animation profile** — baked motion tracks expected? Which PrimeTween
   effects (ids)?
5. **View behavior** — does the kind need per-frame logic (idle scheduling,
   look-at)? If no: `viewClass` stays empty (DefaultModelView), skip view codegen
   and only generate the effect profile.

## Instance (checkpoint C, existing kind)

Ask ONLY:
1. Model name.
2. Variation notes (palette swap? size within kind tolerance? extra part?).
Everything else comes from the kind entry + design system. Do not re-ask kind
or design questions.

## New-model brief (checkpoint A, describe-new path)

1. What is it (one paragraph).
2. Size class (relative to the design-system reference table).
3. Should it animate via picoCAD motion tracks (idle bounce, spin...)? Describe.
4. Any reference among existing models/styles (`character_styles/<NN>` docs)?
