export const meta = {
  name: 'picocad-verify',
  description: 'Visually verify an imported model against the design system, fix import issues (≤3 iterations)',
  whenToUse: 'Phase 4 of /picocad-model, after picocad-unity-import. Args: {jobPath}.',
  phases: [{ title: 'Verify', detail: 'Fable 5 vision agent, spawn → capture → judge → fix loop', model: 'fable' }],
}

const a = typeof args === 'string' ? JSON.parse(args) : args
if (!a || !a.jobPath) throw new Error('args.jobPath required')

const REPO = 'C:/Users/kelns/repos/Experimental'
const PKG = `${REPO}/Packages/com.unidad.core`

phase('Verify')
const verdict = await agent(
  `You are the visual quality judge for a picoCAD → Unity model pipeline. You iterate spawn → capture → judge → fix, at most 3 times, then deliver a verdict.

Job file: ${a.jobPath} (read it: modelId, prefabPath, kind, convert.parityRender). Design system: read the doc at job.designSystem. Tool reference: ${PKG}/Tools~/picocad-pipeline/skill/references/unity-builder.md.

Prerequisite: ToolSearch "select:mcp__unity-mcp__Unidad_PicoCad_SpawnPreview,mcp__unity-mcp__Unidad_PicoCad_CleanupPreview,mcp__unity-mcp__Unity_SceneView_CaptureMultiAngleSceneView,mcp__unity-mcp__Unity_GetConsoleLogs,mcp__unity-mcp__Unity_ManageAsset,mcp__unity-mcp__Unity_RunCommand". If the Unidad tools are unavailable, append {"verify": {"verdict": "SKIPPED", "reason": "mcp/unidad tools unavailable"}} to the job file and return that — do NOT fabricate a judgment.

Each iteration:
1. Spawn: Unidad_PicoCad_SpawnPreview { ModelId: <modelId> } — it adds a ground quad, frames the Scene view, and returns renderer bounds (height) plus clip names. Record the height.
2. Capture: Unity_SceneView_CaptureMultiAngleSceneView focused on the spawned object. Inspect the images.
3. Judge against the design system:
   - silhouette/geometry matches the source parity render (job.convert.parityRender — open and compare)
   - texture crisp (point filtering — no blur), palette correct, no unintended transparency holes
   - faces +Z (front view shows the model's front), pivot/feet at y=0 (not sunken/floating)
   - scale: the returned bounds height vs the kind's reference height in the design system
4. Fixable import issues (wrong texture import settings, material cutoff, missing controller) → fix via Unity_RunCommand / Unity_ManageAsset, re-run the import (Unidad_PicoCad_ImportModel) if needed, then iterate again.
   Design-system deviations that originate in the MODEL itself (proportions, palette choices) → FLAG them, do not fix.
5. Always finish each iteration (and the whole run) with Unidad_PicoCad_CleanupPreview.

Finish: append a "verify" block to the job file: iterations, measuredHeight, captures (file paths if the capture tool returns them), verdict PASS | PASS_WITH_FLAGS | FAIL, flags[], fixesApplied[]. Return that block.`,
  { label: 'verify', model: 'fable', schema: {
      type: 'object',
      properties: {
        verdict: { type: 'string' }, iterations: { type: 'number' },
        measuredHeight: { type: 'number' },
        flags: { type: 'array', items: { type: 'string' } },
        fixesApplied: { type: 'array', items: { type: 'string' } },
        reason: { type: 'string' }, error: { type: 'string' },
      },
      required: ['verdict'],
    } }
)

return verdict
