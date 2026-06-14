---
name: picocad-model
description: End-to-end model creation - describe a model (or pick a picoCAD2 save) and it appears in Unity as a configured prefab with materials, baked animations, PrimeTween effects, and ModelCatalog registration. Use when the user says "create a model", "import a picoCAD model", "make a <thing> for the game", "new model kind", or "/picocad-model".
---

# picoCAD2 → Unity Model Pipeline

The canonical instructions ship inside the Unidad package so this skill always
matches the installed pipeline version.

**Read and follow, in order:**

1. `Packages/com.unidad.core/Tools~/picocad-pipeline/skill/SKILL.md` — the full
   orchestration flow (checkpoints, workflows, hard rules). Follow it exactly.
2. `tools/picocad-pipeline/pipeline.config.json` — this project's paths.

The references it cites (interview script, job-file schema, Unity builder/MCP
tool guide) live next to it in
`Packages/com.unidad.core/Tools~/picocad-pipeline/skill/references/`.

If the package path does not exist, the Unidad package is missing or outdated —
tell the user instead of improvising.
