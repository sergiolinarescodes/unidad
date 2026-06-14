export const meta = {
  name: 'picocad-unity-import',
  description: 'Import converted glTF into Unity: codegen (new kinds), one-call MCP import, registries',
  whenToUse: 'Phase 3 of /picocad-model, after picocad-convert. Args: {jobPath}.',
  phases: [
    { title: 'Probe', detail: 'Unidad MCP tools available?' },
    { title: 'Codegen', detail: 'new-kind view/effect classes (Fable 5)', model: 'fable' },
    { title: 'Import', detail: 'Unidad_PicoCad_ImportModel (one call)' },
  ],
}

const a = typeof args === 'string' ? JSON.parse(args) : args
if (!a || !a.jobPath) throw new Error('args.jobPath required')

const REPO = 'C:/Users/kelns/repos/Experimental'
const PKG = `${REPO}/Packages/com.unidad.core`

phase('Probe')
const probe = await agent(
  `Probe the Unity MCP bridge for the Unidad picoCAD pipeline. Mechanical only.

Job file: ${a.jobPath} (read it; note convert.outDir and job.modelName).

1. ToolSearch "select:mcp__unity-mcp__Unidad_PicoCad_Status" and call it.
   - Works → record mcpAvailable: true, unidadTools: true, plus the returned kinds/models and gltfastInstalled.
   - Tool not found → ToolSearch "select:mcp__unity-mcp__Unity_ManageEditor" and call GetState: works → mcpAvailable: true, unidadTools: false (old editor session — needs a domain reload or editor restart to register the package tools; note that). GetState also fails → mcpAvailable: false.
2. If mcpAvailable is false: copy <convert.outDir>/<modelName>.gltf/.bin/.png/.manifest.json into ${REPO}/Assets/PicoCadImports/<modelName>/ so the manual fallback can run later.
3. Append an "unityImport" block to the job file: { mcpAvailable, unidadTools, gltfastInstalled }.
Return that block.`,
  { label: 'probe', model: 'haiku', phase: 'Probe', schema: {
      type: 'object',
      properties: {
        mcpAvailable: { type: 'boolean' }, unidadTools: { type: 'boolean' },
        gltfastInstalled: { type: 'boolean' }, error: { type: 'string' },
      },
      required: ['mcpAvailable'],
    } }
)

const jobText = await agent(
  `Read the file ${a.jobPath} and return its raw JSON content exactly. No commentary, no markdown code fences.`,
  { label: 'read-job', model: 'haiku', phase: 'Probe' }
)
// Agents sometimes fence their reply despite instructions — strip before parsing.
const job = JSON.parse(jobText.replace(/^\s*```(?:json)?\s*/i, '').replace(/\s*```\s*$/, ''))

let codegen = null
if (job.kind && job.kind.mode === 'new' && job.kind.needsView !== false) {
  phase('Codegen')
  codegen = await agent(
    `Generate the per-kind C# for a NEW model kind. The catalog machinery lives in the Unidad package; per-kind code is GAME content. Repo: ${REPO}. Follow existing code style exactly.

Job file: ${a.jobPath}. Read it, the design system doc, and these reference implementations first:
- ${PKG}/Runtime/ModelCatalog/Views/ModelViewBase.cs (base class; views are plain C#, NOT MonoBehaviours)
- ${PKG}/Runtime/ModelCatalog/Effects/IModelEffectProfile.cs and Effects/BounceEffectProfile.cs (the template to mirror)
- ${PKG}/Runtime/ModelCatalog/Effects/ModelEffectUtility.cs — MANDATORY for any material color/flash/tint effect. Never tween shader properties directly: glTFast materials use baseColorFactor/emissiveFactor (not _BaseColor/_Color) and the SRP Batcher ignores MaterialPropertyBlock values. Transform tweens (scale/position/rotation) are fine via plain PrimeTween.
- ${PKG}/Runtime/ModelCatalog/ModelCatalogInstallerBase.cs (the hooks you are overriding)
- ${REPO}/Assets/Scripts/ModelCatalog/ModelCatalogInstaller.cs (the game's sealed installer — THIS is where you register)

Author (C# 10, PrimeTween for tweens, namespace Experimental.ModelCatalog):
1. ${REPO}/Assets/Scripts/ModelCatalog/Views/<KindPascal>View.cs extending Unidad.Core.ModelCatalog.Views.ModelViewBase — only if the kind needs per-frame or hook behavior; otherwise skip and leave viewClass empty.
2. ${REPO}/Assets/Scripts/ModelCatalog/Effects/<KindPascal>EffectProfile.cs implementing Unidad.Core.ModelCatalog.Effects.IModelEffectProfile with the kind's effect ids (job.kind / design system).
3. Register both in the game's ModelCatalogInstaller by overriding CreateEffectProfiles() (yield the base implementation's profiles plus the new one) and, if a view exists, CreateViewFactories().
4. Update ${REPO}/Assets/Resources/ModelCatalog/kinds.json: set viewClass/effectProfile/effects for the kind (write the json directly so the build also works without MCP).
5. Verify compilation: run "dotnet build ${REPO}/Experimental.sln" and confirm 0 errors (warnings ok). Fix until clean.
6. Append generatedScripts + compileOk to the job file's unityImport block.
Return {generatedScripts: [...], compileOk: true} or {error}.`,
    { label: 'codegen', model: 'fable', phase: 'Codegen', schema: {
        type: 'object',
        properties: {
          generatedScripts: { type: 'array', items: { type: 'string' } },
          compileOk: { type: 'boolean' }, error: { type: 'string' },
        },
      } }
  )
  if (codegen?.error) return codegen
}

phase('Import')
const build = await agent(
  `Import a converted picoCAD model into Unity. Follow ${PKG}/Tools~/picocad-pipeline/skill/references/unity-builder.md EXACTLY (ordering rules, tool names, fallbacks).

Job file: ${a.jobPath} (modelName, modelId, kind.id, kind.folder, convert.outDir, unityImport.mcpAvailable/unidadTools).

If unidadTools:
1. ToolSearch "select:mcp__unity-mcp__Unidad_PicoCad_ImportModel,mcp__unity-mcp__Unity_ManageEditor,mcp__unity-mcp__Unity_GetConsoleLogs".
2. If codegen just ran: poll Unity_ManageEditor GetState until IsCompiling is false; Unity_GetConsoleLogs(logTypes:"error") must be clean.
3. ONE call: Unidad_PicoCad_ImportModel { SourceDir: <convert.outDir>, ModelId: <modelId>, KindId: <kind.id>, Register: true }.
4. Unity_GetConsoleLogs(logTypes:"error") — must be clean. Verify the returned prefabPath exists on disk under ${REPO}.
If mcpAvailable but NOT unidadTools: use Fallback A from unity-builder.md (one Unity_RunCommand), then update models.json from the file side and reimport it.
If NOT mcpAvailable: do NOT attempt MCP. Write unityImport.manualSteps = Fallback B steps with paths substituted, and return {manualStepsRequired: true}.

Finally: append prefabPath/controllerPath/clipPaths/messages to the job's unityImport block. Return that block.`,
  { label: 'import', model: 'haiku', phase: 'Import', schema: {
      type: 'object',
      properties: {
        prefabPath: { type: 'string' }, controllerPath: { type: 'string' },
        clipPaths: { type: 'array', items: { type: 'string' } },
        manualStepsRequired: { type: 'boolean' }, error: { type: 'string' },
      },
    } }
)

return { probe, codegen, build }
