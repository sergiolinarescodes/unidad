# Driving the Unity side

The Unidad package registers **first-class MCP tools** with Unity's MCP bridge
(`Editor/PicoCad/Mcp/PicoCadMcpTools.cs`, active when `com.unity.ai.assistant`
and PrimeTween are installed). Use these — never reimplement their steps with
ad-hoc `Unity_RunCommand` snippets.

## The tools (load via ToolSearch, names are exact)

| MCP tool | Does |
|---|---|
| `mcp__unity-mcp__Unidad_PicoCad_Status` | Pipeline probe: glTFast installed, registered kinds + models, registry paths. **Call this first.** |
| `mcp__unity-mcp__Unidad_PicoCad_RegisterKind` | Upsert a kind in `Assets/Resources/ModelCatalog/kinds.json` (id, folder, unitScale, viewClass, effectProfile, effects CSV, designNotes). |
| `mcp__unity-mcp__Unidad_PicoCad_ImportModel` | ONE call = stage + build + register: copies `<name>.gltf/.bin/.png/.manifest.json` from `SourceDir` into `Assets/PicoCadImports/<name>/`, imports synchronously, runs `PicoCadPrefabBuilder` (point-filtered texture, looping clips, AnimatorController, prefab → `Assets/Resources/Models/<KindFolder>/`), upserts `models.json`. Params: `SourceDir` (converter out dir, absolute ok), `ModelId`, `KindId`, `Register`. |
| `mcp__unity-mcp__Unidad_PicoCad_SpawnPreview` | Spawn a registered model (+ ground quad) at origin, frame it in the Scene view; returns renderer bounds (height → scale checks) and clip names. |
| `mcp__unity-mcp__Unidad_PicoCad_CleanupPreview` | Destroy `PicoCadPreview__*` roots. Always run after verification. |

All return `{success, message, data}` / `{success: false, error}`. A missing
tool (ToolSearch finds nothing) means the editor predates this package version
or MCP is down → use the fallbacks below.

## Ordering rules (domain reloads kill in-flight MCP state)

1. Any C# codegen (new-kind view/effect classes in the GAME assembly) happens
   BEFORE `ImportModel`, and you WAIT for compilation: poll `Unity_ManageEditor
   GetState` until `IsCompiling == false`, then check `Unity_GetConsoleLogs`
   for errors. Tool registration survives the reload (re-scanned on load).
2. Then `Unidad_PicoCad_ImportModel` (it does its own synchronous asset import).
3. After every editor action: `Unity_GetConsoleLogs(logTypes: "error")`.

## Fallback A — MCP works but Unidad tools are missing (old editor session)

Single `Unity_RunCommand`:

```csharp
using UnityEngine;
using UnityEditor;
using Unidad.Core.Editor.PicoCad;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        AssetDatabase.Refresh();
        var report = PicoCadPrefabBuilder.Build(
            "Assets/PicoCadImports/<NAME>/<NAME>.gltf",
            "Assets/PicoCadImports/<NAME>/<NAME>.manifest.json",
            "<KIND_FOLDER>");
        result.Log("[PicoCadPrefabBuilder] {0}", report.ToString());
    }
}
```

(then update `models.json` from the Node side and reimport it).

## Fallback B — MCP unavailable (manual)

The pipeline must not die when MCP is down. Copy the artifacts into
`Assets/PicoCadImports/<name>/` yourself, then give the user these steps:

1. Focus Unity so the import folder refreshes; check Console for red errors.
2. Select `<name>.gltf` → menu **Tools > PicoCAD > Build Prefab From Imported
   glTF…** (uses the manifest next to the gltf; kind folder defaults to "Misc").
3. Confirm `Assets/Resources/Models/<KindFolder>/<Name>.prefab` exists.
4. You update `models.json` from the Node side afterwards.

Batch-mode alternative when the editor is CLOSED: compile + import happen with
`Unity.exe -batchmode -quit -projectPath <proj> -logFile <log>`; prefer the
open-editor path for the build itself.

## Verification captures

With MCP: `Unidad_PicoCad_SpawnPreview` (returns bounds/height — compare to the
kind's reference height), then `Unity_SceneView_CaptureMultiAngleSceneView`
(+ `Unity_Camera_Capture` close-ups), judge against DESIGN_SYSTEM.md, and
finish with `Unidad_PicoCad_CleanupPreview`. Animation: enter play mode,
capture 2 frames ~0.5s apart.
Without MCP: ask the user for Scene-view screenshots at the checkpoints instead.
