export const meta = {
  name: 'picocad-convert',
  description: 'Convert a picoCAD2 save to glTF/OBJ/PNG + manifest, validate, and parity-check',
  whenToUse: 'Phase 2 of /picocad-model, after a source .txt exists. Args: {jobPath}.',
  phases: [{ title: 'Convert' }],
}

const a = typeof args === 'string' ? JSON.parse(args) : args
if (!a || !a.jobPath) throw new Error('args.jobPath required')

phase('Convert')
log(`converting job: ${a.jobPath}`)

const result = await agent(
  `You run the picoCAD2 converter for one model. Work strictly mechanically; report structured results.

Job file (read it first, then append your results to it): ${a.jobPath}
Converter code: Packages/com.unidad.core/Tools~/picocad-pipeline (repo: C:/Users/kelns/repos/Experimental). Project workspace (config, out, jobs): tools/picocad-pipeline.

Steps:
1. Read the job file. Use job.sourceTxt as input and job.modelName as the model name.
2. Run: node Packages/com.unidad.core/Tools~/picocad-pipeline/convert.js "<sourceTxt>" --out "tools/picocad-pipeline/out/<modelName>" --name "<modelName>"
3. Run glTF structural checks: node Packages/com.unidad.core/Tools~/picocad-pipeline/test/gltf-check.js "tools/picocad-pipeline/out/<modelName>/<modelName>.gltf"
4. Run the golden suite to confirm the converter itself is healthy: node Packages/com.unidad.core/Tools~/picocad-pipeline/test/golden.test.js
5. Parity render: node "%APPDATA%/picocad2/preview_model.js" "<sourceTxt>" renders front/side views of the SOURCE next to the source file — copy that PNG into the out dir as <modelName>_parity.png. (If the script fails, note it as a warning, do not fail the phase.)
6. Append a "convert" block to the job JSON (absolute paths): outDir, gltf, manifest, goldenChecks PASS/FAIL, gltfChecks PASS/FAIL, parityRender, animations (from the manifest), warnings (from converter output).
7. Return ONLY the convert block as your result.

If any required step fails, write {"convert": {"error": "<details>"}} into the job file and return that.`,
  { label: 'convert', model: 'haiku', schema: {
      type: 'object',
      properties: {
        error: { type: 'string' },
        outDir: { type: 'string' }, gltf: { type: 'string' }, manifest: { type: 'string' },
        goldenChecks: { type: 'string' }, gltfChecks: { type: 'string' },
        parityRender: { type: 'string' }, animations: { type: 'array', items: { type: 'string' } },
        warnings: { type: 'array', items: { type: 'string' } },
      },
    } }
)

return result
