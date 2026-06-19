# Skills~

Claude Code skills that ship with Unidad Core, so the conventions travel with the framework. Unity
ignores this folder (the `~` suffix), and Claude Code does not load skills from inside a package — so
to use them in a project, copy the folders you want into that project's `.claude/skills/`:

```
cp -r Packages/com.unidad.core/Skills~/<skill> .claude/skills/<skill>
```

## Skills
- **live-mcp-test** — build a Live MCP Test for a feature (the mandatory testing pattern; see
  `Documentation~/LiveMcpTesting.md` and the `Samples~/MovementLiveTest` example).
- **live-test-loop** — drive a backlog of features until every one is implemented AND has a passing
  Live MCP Test.
- **picocad-model** — the picoCAD2 → Unity model pipeline (glTF converter, glTFast, ModelCatalog),
  which the package's `Runtime/ModelCatalog` + `Editor/PicoCad` implement.
- **roomba-modules** — the RoombaGame modular-robot system (an example game built on the framework;
  game-specific, included as a reference pattern).
