# Job file contract

Path: `tools/picocad-pipeline/jobs/<modelId>.job.json` (gitignored).
Created by the skill at checkpoint C; each workflow phase appends its block and
never deletes another phase's data. Workflows receive `{ "jobPath": "<abs path>" }`
as args (parse defensively: `const a = typeof args === 'string' ? JSON.parse(args) : args`).

```jsonc
{
  "schema": 1,
  "modelId": "pig",            // lowercase_snake, becomes catalog id
  "modelName": "pig",          // file/prefab base name
  "mode": "existing | generate",
  "kind": {
    "mode": "oneoff | new | instance",
    "id": "misc",
    "folder": "Misc",
    "isNew": false
  },
  "brief": "...",              // describe-new path: the user's accepted brief
  "designSystem": "docs/model-pipeline/DESIGN_SYSTEM.md",
  "sourceTxt": "C:/Users/.../picocad2/examples/pig.txt",

  // appended by picocad-generate (generate mode only)
  "generate": {
    "generatorScript": ".../gen_pig.js",
    "previews": [".../pig_preview.png"],
    "validation": "PASS",
    "notes": []
  },

  // appended by picocad-convert
  "convert": {
    "outDir": ".../tools/picocad-pipeline/out/pig",
    "gltf": ".../pig.gltf", "manifest": ".../pig.manifest.json",
    "goldenChecks": "PASS", "gltfChecks": "PASS",
    "parityRender": ".../pig_parity.png",
    "animations": ["Motion"], "warnings": []
  },

  // appended by picocad-unity-import
  "unityImport": {
    "importDir": "Assets/PicoCadImports/pig",
    "prefabPath": "Assets/Resources/Models/Misc/pig.prefab",
    "controllerPath": "...", "clipPaths": ["..."],
    "generatedScripts": [],     // new-kind codegen outputs
    "buildLog": "...", "compileOk": true,
    "mcpAvailable": true,       // false => manualSteps was used
    "manualSteps": null
  },

  // appended by picocad-verify
  "verify": {
    "iterations": 1,
    "captures": ["..."],
    "verdict": "PASS | PASS_WITH_FLAGS | FAIL",
    "flags": ["scale 10% over reference height"],
    "fixesApplied": []
  }
}
```

Rules:
- Absolute paths inside the job file (workflow agents have different CWDs).
- A phase that fails writes `"<phase>": { "error": "..." }` and the workflow
  returns that error — the skill decides whether to retry, fix, or surface.
