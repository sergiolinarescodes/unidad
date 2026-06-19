---
name: live-test-loop
description: Drive a whole backlog of features to completion by looping until EVERY feature task is both implemented AND covered by a passing Live MCP Test. Use when the user gives a list of features to build/verify and wants them all live-tested, or says "loop until all features are live-tested", "keep going until every feature has a passing live test", "run all the live tests until green", or pairs this with /loop for unattended runs.
---

# Live-Test Loop — build + live-verify every feature until all pass

Drives a set of feature tasks to "done", where **done = implemented AND its Live MCP Test `RunPlan`
returns success**. Repeats until the entire backlog is green. Pairs with the `live-mcp-test` skill
(how to build one feature's live test) and the `/loop` skill (to self-pace an unattended run).

## When to use
The user has several features to build and wants each one genuinely verified in a running scene, not
just compiled. Or: "keep looping until all features marked as tasks are also tested in live scenarios."

## Preconditions
- Unity Editor open with the `unity-mcp` relay connected (`/mcp` to reconnect; editing scripts triggers a
  domain reload that briefly drops the connection — poll `Unity_ManageEditor GetState`).
- The `live-mcp-test` skill recipe is followed for each feature.

## The loop

1. **Build the backlog as tasks.** For each feature the user listed, `TaskCreate` one task. Definition of
   done for EACH: "service + installer + `[LiveTestScene]` + `<Feature>LiveTest.unity` exist, compile clean,
   and `Unidad.LiveTest.RunPlan` returns success with all cases passing." Keep one task per feature cluster
   (one manageable scene each).

2. **Outer loop — repeat until every feature task is `completed` AND the full live suite is green:**

   a. Pick the next `pending` feature task; mark it `in_progress`.

   b. **Implement + live-test it** following the `live-mcp-test` skill: write the C# (service/installer/
      factory/events/scene-object/bootstrap), refresh + compile clean (`Unity_RunCommand AssetDatabase.Refresh`,
      then `Unity_GetConsoleLogs error` == 0), author the scene via MCP.

   c. **Inner verify-and-tune loop** for THIS feature (do NOT mark done until green):
      ```
      Unity_ManageScene Load <scenePath>
      Unity_ManageEditor Play              # poll GetState until IsPlaying
      Unidad.LiveTest.List                 # confirm the right test is active
      Unidad.LiveTest.RunPlan              # -> checks[]
      # if any check fails: inspect with RunAction -> Step/StepUntil -> Probe/Assert and Unity_GetConsoleLogs;
      #   adjust forces/step counts/geometry or fix the service; recompile; re-run RunPlan.
      Unity_ManageEditor Stop
      ```
      Loop (b/c) until `RunPlan.success == true`. Only then mark the task `completed`.

   d. **Full-suite regression** (cheap, do it after each feature and at the end): `Unidad.LiveTest.ListAll`
      → for each entry: `Unity_ManageScene Load` → `Play` → `Unidad.LiveTest.RunPlan` → `Stop`. Aggregate
      every scene's `checks[]` into one pass/fail table. If a previously-green feature regressed, reopen its
      task and fix before continuing.

3. **Stop** when all feature tasks are `completed` AND the latest full-suite run is all-green. Report the
   aggregate table (per scene: passed/total, and any failing check names).

## Unattended / self-paced runs
Wrap the outer loop with the `/loop` skill so it continues across turns without prompting:
- `/loop build the next pending feature and live-test it until RunPlan is green, then run the full
  Unidad.LiveTest.ListAll suite; stop when every feature task is completed and the suite is all-green.`
- Between iterations, if you are waiting on a Unity domain reload / Play-Mode transition, poll
  `Unity_ManageEditor GetState` rather than sleeping. If the MCP connection drops, surface it and ask the
  user to `/mcp` reconnect — do not spin.

## Guardrails
- NEVER mark a feature done on "it compiled" — only on a green live `RunPlan`.
- Cross-scene "run all" is orchestrated tool-call by tool-call (Load→Play→RunPlan→Stop per scene); it is
  NOT a single MCP call (scene switches + domain reloads).
- Keep `Verify`/assertions on probe values; use a one-time `Unity_SceneView_Capture2DScene` only when a
  check is genuinely visual.
- If a feature can't be made green after a few honest tuning attempts, stop and report what fails (the
  failing checks + probe values), rather than loosening assertions to force a pass.
