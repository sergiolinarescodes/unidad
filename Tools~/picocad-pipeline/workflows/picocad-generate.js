export const meta = {
  name: 'picocad-generate',
  description: 'Generate a picoCAD2 model (mesh + texture + motion) from a design brief',
  whenToUse: 'Describe-new path of /picocad-model, after kind triage. Args: {jobPath}.',
  phases: [
    { title: 'Design & Generate', detail: 'Fable 5 authors a generator script + save file', model: 'fable' },
    { title: 'Validate', detail: 'validators + previews' },
  ],
}

const a = typeof args === 'string' ? JSON.parse(args) : args
if (!a || !a.jobPath) throw new Error('args.jobPath required')

phase('Design & Generate')

const design = await agent(
  `You are the model artist for a picoCAD2 → Unity pipeline. Author a complete picoCAD2 model as a Node.js generator script, following the established generator conventions.

Job file (read first; it has the brief, kind, and design-system path): ${a.jobPath}

MUST read before designing:
- The design system doc referenced by job.designSystem (palette policy, unit scale, reference heights, orientation: feet at y=0, face -Z in picoCAD).
- 2-3 generator examples in %APPDATA%/picocad2/character_styles/*/gen_*.js and slime_equipment/equip_common.js (validateModel, texFace, regionUV helpers — copy the validation discipline, structure your script the same way).
- The kind's entry in the job file; if it references an existing style folder, read its STYLE.md.

Author:
1. A generator script at %APPDATA%/picocad2/generated/gen_<modelName>.js (pure Node, zero deps) that emits the save file at %APPDATA%/picocad2/generated/<modelName>.txt. Requirements:
   - texture.pixels EXACTLY 16384 hex chars; 16-color palette per design system; valid shade_pal_1/2; correct transparent_color usage.
   - Scene graph: lowercase_snake unique node names (they become Unity animation paths), feet at y=0, facing -Z.
   - Motion tracks if the brief asks for animation: tracks use segments {icon:470, start, stop, times, axises, prop, delta} for oscillation, or {start, stop, delta, curve, pingpong} for one-shot tweens; metadata.motion_duration in seconds. Loop-friendly (end = start).
   - Self-validation at the end of the script (counts, ranges, vertex ids 1-based in range) printing PASS/FAIL lines.
2. Run the script to produce the .txt; fix issues until validation passes.
3. Render previews: node %APPDATA%/picocad2/preview_model.js <generated txt> (produces <name>_preview.png next to it).
4. Append a "generate" block to the job file: generatorScript, txt path (also set job.sourceTxt to it), previews[], validation PASS/FAIL, notes[].
Return the generate block.`,
  { label: 'design+generate', model: 'fable', phase: 'Design & Generate', schema: {
      type: 'object',
      properties: {
        error: { type: 'string' },
        generatorScript: { type: 'string' }, txt: { type: 'string' },
        previews: { type: 'array', items: { type: 'string' } },
        validation: { type: 'string' }, notes: { type: 'array', items: { type: 'string' } },
      },
    } }
)

if (design?.error) return design

phase('Validate')
const check = await agent(
  `Independently validate a generated picoCAD2 save file. Do NOT trust the generator's own validation.

Job file: ${a.jobPath} (read the generate block for paths)

Run:
1. node %APPDATA%/picocad2/audit_transparency.js "<txt>" (if the script exists)
2. node -e parse check: JSON.parse the txt, assert texture.pixels.length === 16384, 16 colors, every face's vertex_ids within range and uvs length == 2*corners, metadata.version == "2.0".
3. node Packages/com.unidad.core/Tools~/picocad-pipeline/convert.js "<txt>" --out tools/picocad-pipeline/out/_validate_tmp (repo C:/Users/kelns/repos/Experimental) — the converter's validation must pass.
Update the job file generate.validation accordingly and return {"validation": "PASS"} or {"validation": "FAIL", "error": "..."}.`,
  { label: 'validate', model: 'haiku', phase: 'Validate', schema: {
      type: 'object',
      properties: { validation: { type: 'string' }, error: { type: 'string' } },
      required: ['validation'],
    } }
)

return { ...design, independentValidation: check }
