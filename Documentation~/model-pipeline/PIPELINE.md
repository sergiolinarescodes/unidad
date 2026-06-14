# picoCAD2 → Unity Model Pipeline — Architecture

One command (`/picocad-model`) takes a model from *description* (or an existing
picoCAD2 save) to a configured Unity prefab: URP material, point-filtered
texture, baked AnimationClips, PrimeTween effect profile, catalog registration,
and visual verification. The entire pipeline ships **inside the Unidad package**
(`com.unidad.core`) so any Unidad project gets it by installing the package.

## Stages

```
brief ──► picocad-generate ──► .txt (picoCAD2 save, human-tweakable in the app)
.txt  ──► picocad-convert  ──► .gltf/.bin/.png + manifest (Tools~/picocad-pipeline)
gltf  ──► picocad-unity-import ──► prefab + clips + controller (+ per-kind C#)
prefab──► picocad-verify   ──► spawn + multi-angle captures judged vs DESIGN_SYSTEM.md
```

## What lives where

**In this package:**
- `Tools~/picocad-pipeline/` — pure-Node converter (golden-tested byte-for-byte
  vs the app's own exports), workflows (`workflows/picocad-*.js`), canonical
  skill instructions + references (`skill/`).
- `Runtime/ModelCatalog/` — Unidad-compliant runtime (asmdef
  `Unidad.Core.ModelCatalog`, gated on PrimeTween via `UNIDAD_PRIMETWEEN`):
  `IModelCatalogService` spawns prefabs via `IGameObjectFactory`, wraps them in
  plain-C# views (no MonoBehaviours on prefabs), routes effects through
  `IAnimationResolver` (PrimeTween in production, instant in tests).
  `ModelCatalogInstallerBase` is abstract — games subclass it and override
  `CreateEffectProfiles()` / `CreateViewFactories()` to register per-kind code.
  The scenario auto-skips (`ScenarioVerificationResult.Skip`) when no model
  fixture exists yet.
- `Editor/PicoCad/` — `PicoCadPrefabBuilder` (texture import settings, looping
  non-legacy clip copies, AnimatorController, prefab into
  `Assets/Resources/Models/<Kind>/`; menu item **Tools > PicoCAD > Build Prefab
  From Imported glTF…**), `ModelCatalogRegistry` (kinds/models json upsert).
- `Editor/PicoCad/Mcp/` — **custom MCP tools** registered with Unity's MCP
  bridge (`com.unity.ai.assistant`), so agents drive Unity with named tool
  calls instead of ad-hoc RunCommand snippets:
  `Unidad.PicoCad.Status`, `Unidad.PicoCad.ImportModel` (stage + build +
  register in one call), `Unidad.PicoCad.RegisterKind`,
  `Unidad.PicoCad.SpawnPreview` (bounds + clips for the verify judge),
  `Unidad.PicoCad.CleanupPreview`. Gated on both PrimeTween and
  `com.unity.ai.assistant` being installed.

**In the consuming project:**
- `tools/picocad-pipeline/` — workspace: `pipeline.config.json` (all paths),
  `out/` (converter output), `jobs/` (per-run job files; gitignore them).
- `Assets/Resources/ModelCatalog/kinds.json` + `models.json` — the registries.
- `Assets/Scripts/ModelCatalog/` — the game's sealed
  `ModelCatalogInstaller : ModelCatalogInstallerBase` plus generated per-kind
  `<Kind>View.cs` / `<Kind>EffectProfile.cs` (project content by design: kinds
  are per-project).
- `docs/model-pipeline/DESIGN_SYSTEM.md` — scaffolded from this folder's
  `DESIGN_SYSTEM.template.md` by interviewing the user on first run, then read
  and enforced on every later run (deviations flagged, not silently fixed).
- `.claude/skills/picocad-model/SKILL.md` — thin stub pointing at the canonical
  skill in `Tools~/picocad-pipeline/skill/SKILL.md`.

## Orchestration

The `/picocad-model` skill owns all user Q&A (checkpoints A/B/C/A2/D/E); the
four workflows do headless work and are invoked by scriptPath from the package.
Agent models: design/texture/animation/visual-judging agents run on **Fable 5**;
mechanical steps on cheap tiers. Job state flows through
`tools/picocad-pipeline/jobs/*.job.json`.

## Model kinds

`kinds.json` defines archetypes (folder, unitScale, viewClass, effectProfile,
effects). Creating a model as an **instance** of an existing kind skips the
design questions and code generation entirely — only a registry entry and a
prefab build run. A **new kind** triggers the kind interview + Fable 5 codegen
of `<Kind>View.cs` / `<Kind>EffectProfile.cs` into the game assembly and a
design-system subsection.

## Correctness guarantees

- Converter geometry/texture parsing is **golden-tested byte-for-byte** against
  picoCAD 2's own exports (`Tools~/picocad-pipeline/test/golden.test.js`).
- Motion semantics are an exact port of the app's `motion.lua` (extracted from
  the Love2D executable), unit-checked against closed-form math.
- glTF output passes structural validity checks before Unity sees it.
- The runtime system runs under `AllSystemScenariosTests` (the scenario spawns
  a real imported prefab, or skips cleanly when none exists) + dedicated NUnit
  tests (`Tests/ModelCatalog/`).

## Adopting in another Unidad project

1. Install `com.unidad.core` (this package) and `com.unity.cloud.gltfast`;
   have PrimeTween installed as a package (`com.kyrylokuzyk.primetween`) — the
   ModelCatalog assemblies self-activate via versionDefines. For the MCP tools,
   also install `com.unity.ai.assistant` and enable Unity MCP.
2. Declare the game installer:
   `public sealed class ModelCatalogInstaller : ModelCatalogInstallerBase { }`
   in your game assembly.
3. Create `tools/picocad-pipeline/pipeline.config.json` by copying the shipped
   template `Tools~/picocad-pipeline/pipeline.config.template.json` (all paths
   are conventional defaults — adjust only if your layout differs) and gitignore
   `out/` + `jobs/`.
4. Copy the shipped stub `Tools~/picocad-pipeline/skill/project-stub.SKILL.md`
   into `.claude/skills/picocad-model/SKILL.md`.
5. Run `/picocad-model` — the first-run interview scaffolds your design system.
