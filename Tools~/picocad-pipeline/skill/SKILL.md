# picoCAD2 → Unity Model Pipeline (canonical skill instructions)

This file is the canonical body of the `/picocad-model` skill, shipped inside
the Unidad package so every project runs the version matching its installed
package. The project-side `.claude/skills/picocad-model/SKILL.md` is a thin
stub that points here.

You orchestrate the full pipeline: **describe → generate picoCAD2 save → convert
to glTF → import to Unity → prefab + animations + effects → visual verify →
catalog registration**. Workflows do the heavy lifting; YOU own every user
conversation (workflows run headless and cannot ask questions).

## Layout

- **Package code** (`Packages/com.unidad.core/`): converter
  (`Tools~/picocad-pipeline/`), workflows (`Tools~/picocad-pipeline/workflows/`),
  these references (`Tools~/picocad-pipeline/skill/references/`), the runtime
  ModelCatalog (`Runtime/ModelCatalog/`), the editor builder + MCP tools
  (`Editor/PicoCad/`), docs templates (`Documentation~/model-pipeline/`).
- **Project workspace** (`tools/picocad-pipeline/`): `pipeline.config.json`
  (all project paths — read it, never hardcode), `out/` (converter output),
  `jobs/` (job files).
- **Project content**: `Assets/Resources/ModelCatalog/kinds.json` + `models.json`,
  `Assets/Scripts/ModelCatalog/` (sealed installer + generated per-kind code),
  `docs/model-pipeline/DESIGN_SYSTEM.md`.

## Hard rules

1. **Fable 5 owns everything visual & spatial; mechanical models never touch
   it.** Any agent that makes a design, geometry/modeling, texturing, layout, or
   animation judgment — authoring vertices/faces, placing parts, choosing the
   palette, building or painting the texture page, mapping UVs, or reviewing a
   render — runs with `model: 'fable'`. Non-art models (Opus/`claude`, `haiku`)
   are weak at 3D/spatial reasoning, so they must NEVER place geometry, paint
   texture, or judge a render; they do only mechanical plumbing (run the
   converter, file copies, the validation harness, scene-node/metadata
   boilerplate, orchestration, running tools). If a task needs spatial or visual
   judgment it is Fable's — no exceptions. Play to strengths.
2. Invoke workflows by **scriptPath**
   (`Packages/com.unidad.core/Tools~/picocad-pipeline/workflows/picocad-*.js`),
   never by name (the name registry only scans at session start).
3. Inside workflow scripts `args` may arrive as a JSON **string** — they parse
   defensively; pass args as a plain object anyway.
4. All inter-phase state lives in `tools/picocad-pipeline/jobs/<model>.job.json`
   (schema: references/job-spec.md). Create it at checkpoint C; every phase appends.
5. **Unity work goes through the Unidad MCP tools**
   (`Unidad_PicoCad_Status/ImportModel/RegisterKind/SpawnPreview/CleanupPreview`
   on the unity-mcp server — see references/unity-builder.md for exact names and
   fallbacks). Never hand-roll RunCommand snippets when a tool exists.
6. **Model limb-ready by default — this does NOT constrain the design.** Some
   models become limbed enemies; the detachment rig resolves each limb by a child
   transform whose **name contains** `EnemyLimbDef.ModelPartId` (case-insensitive),
   then reparents that transform out of the enemy and throws it. So keep every
   plausibly-detachable part (head, torso, an arm, a leg, a wing, a tail, a tier, a
   pane, a finial) as its **own, limb-meaningfully-named node** instead of one merged
   mesh — a free nicety that costs zero design freedom. Tell the modeling agent this
   as context, not a rule: build the silhouette it wants first; a genuinely
   indivisible design still works (it just degrades to a whole-model highlight with no
   severed parts). The torso/core mass should be a node too (it's the anchor part).

## Flow

### Checkpoint A — intent
Ask what to build. Two paths:
- **Describe new** (primary): collect a one-paragraph brief (what, mood, size
  class, animation wishes). Continue to B, then run `picocad-generate`.
- **Existing save** (secondary): list `.txt` files in the picoCAD2 dir
  (config `picocadDir`, default `%APPDATA%/picocad2`, including
  `character_styles/`, `examples/`). Skip generation.

### Checkpoint B — design system
If the doc at config `designSystemDoc` is missing: run the interview in
references/interview.md (AskUserQuestion, 2 rounds max), then scaffold from
`Packages/com.unidad.core/Documentation~/model-pipeline/DESIGN_SYSTEM.template.md`
replacing every `{{PLACEHOLDER}}`.
If it exists: read it; it constrains everything downstream.

### Checkpoint C — kind triage
Ask: one-off / NEW kind / INSTANCE of existing kind (read existing kinds from
`Assets/Resources/ModelCatalog/kinds.json`, or the `Unidad_PicoCad_Status` tool
when the editor is connected).
- **One-off** → kind `misc`, no codegen.
- **NEW kind** → ask the kind questions (interview.md §kinds); register it via
  the `Unidad_PicoCad_RegisterKind` tool (or edit kinds.json directly when MCP
  is down) AND append a `## Kinds` subsection to DESIGN_SYSTEM.md.
- **INSTANCE** → load the kind entry; ask ONLY variation questions (name,
  palette swaps). Skip codegen and kind questions entirely.
Write the job file now.

### Generation (new-model path only)
Run the `picocad-generate` workflow (scriptPath, args `{jobPath}`). It writes a
generator script + `.txt` following the `character_styles/gen_*.js` conventions
and renders front/side previews.
**Checkpoint A2**: show the previews to the user (SendUserFile). Offer: accept /
revise (re-run with notes) / hand-tweak in picoCAD2 first (the app opens any
valid save we write — never drive the app itself).

### Conversion
Run the `picocad-convert` workflow. Gate: converter validation + glTF checks +
the parity render. On failure, show errors and stop.

### Unity import
Run the `picocad-unity-import` workflow. It probes the Unidad MCP tools, runs
new-kind codegen first (Fable 5, into the GAME assembly — views/effects under
`Assets/Scripts/ModelCatalog/`, registered in the game's sealed
`ModelCatalogInstaller` by overriding the base hooks), waits for compile, then
makes ONE `Unidad_PicoCad_ImportModel` call (stage + build + register).
**Checkpoint D** (new kinds): show the generated C# to the user before
continuing.
If the workflow returns `manualStepsRequired`, walk the user through the manual
fallback steps it wrote into the job file.

### Verification
Run the `picocad-verify` workflow (Fable 5 vision agent, ≤3 fix iterations —
`Unidad_PicoCad_SpawnPreview` + multi-angle captures judged against
DESIGN_SYSTEM.md: measured bounds height vs reference heights, crisp point
filtering, transparency, faces +Z, feet at y=0, animation frames). Design-system
deviations are FLAGGED, not silently fixed.
**Checkpoint E**: show final captures; accept or loop with notes.

### Finalize
- Confirm the model is in `Assets/Resources/ModelCatalog/models.json`
  (ImportModel registers it; verify).
- Append a changelog line to DESIGN_SYSTEM.md.
- Print the usage snippet:
  `catalog.Spawn("<modelId>", position)` /
  `factory.InstantiatePrefab("Models/<KindFolder>/<Name>", ...)`.
- Suggest running the `ModelCatalogScenario` in the Scenario Browser
  (parameter `modelId`) to see it live.

## Troubleshooting

- **Unidad MCP tools not found but MCP connected**: the editor session predates
  the package install — ask the user to let Unity recompile (or restart the
  editor), then ToolSearch again. Fallback A in unity-builder.md works meanwhile.
- **unity-mcp "Connection revoked"**: editor-side approval latched; the user
  must re-approve in Project Settings > AI > Unity MCP FIRST, then reconnect;
  if it persists, use the manual fallback (unity-builder.md) — the pipeline
  works without MCP, you just lose automated captures.
- **Editor closed**: batch mode works for compile/tests
  (`-batchmode -quit` / `-runTests`), but the import/build path needs the
  editor open.
- **Converter golden test broke**: a picoCAD2 update may have changed export
  format — run
  `node Packages/com.unidad.core/Tools~/picocad-pipeline/test/golden.test.js`
  and inspect.
- **No animation on prefab**: model has no motion tracks (expected: Animator is
  removed) or glTFast import setting changed — check the builder report warning.
- **ModelCatalog assemblies inactive**: they're gated on PrimeTween
  (`UNIDAD_PRIMETWEEN` via versionDefines on `com.kyrylokuzyk.primetween`) —
  the project must have PrimeTween installed as a package.
