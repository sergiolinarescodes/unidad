# Sync unidad: the procedure

How framework edits made in a game go up into unidad, and how a unidad tag comes down into the game (U-54, U-62, U-117).
The `sync-unidad-<k>-up` and `sync-unidad-<k>-down` tasks follow this page step by step. Every tool reads exit codes,
never the console (it may be localised). The game here is `11-unidad-unity` (default branch `master`); the scripts take
every path as a parameter.

Names: `T` = the tag the game carries now (its `docs/SOURCES.md` carry block), `N` = T's pre number, the new tag is
`v2.0.0-pre.<N+1>` (`v2.0.0` at ship), `S` = the game's `origin/master` when the run starts.

Never in a sync: edit game code, touch `Tools~/`, `Skills~/` or `Documentation~/model-pipeline/`, move or rewrite a
tag, or carry from a working tree instead of a tag.

## Up (repo unidad, one PR)

1. **Freeze the start.**
   ```
   git -C C:/Users/kelns/repos/11-unidad-unity fetch origin
   git -C C:/Users/kelns/repos/11-unidad-unity rev-parse origin/master        # = S, goes in both PR bodies
   git -C C:/Users/kelns/repos/11-unidad-unity show S:docs/SOURCES.md          # the Divergences rows at S
   ```
   No rows and no upstream-first reason: the run is skipped ("no rows at S" in the task result).
2. **Slot.** `pwsh -File tools/worktree-claim.ps1 -Branch sync/pre.<N+1> -Base origin/main`; work in the printed slot.
3. **Replay.**
   ```
   pwsh -File tools/replay-divergences.ps1 -GameRepo C:/Users/kelns/repos/11-unidad-unity -CarriedTag T -Since S -Out <scratch>/replay.patch
   ```
   It prints `rows: #a,#b` (replayed) and `stale: #x` (rows whose paths no longer differ; the carry drops both).
   Exit 2 = refused: a changed file no row covers, a changed analyzer DLL, or a `package.json` version/dependency edit
   (U-116). Stop, comment on the game PR that made the change (`git log S -- <path>` names it), and run again only once
   that PR has a row or the change is reverted in the game. The task result names the blocker.
4. **No game names.** Grep the patch's added lines for `\bCds\b|\bCDS\b|\bcds\.`; a hit stops the run as in step 3.
5. **Apply.** `git apply --3way --index <scratch>/replay.patch`. A conflict means `main` moved on the same lines since
   `T`: resolve by hand keeping both intents and name each resolved file in the PR body.
6. **Analyzers.** If `git diff --quiet T HEAD -- analyzers` fails: `pwsh -File analyzers/build.ps1`, then
   `pwsh -File analyzers/build.ps1 -Check` (exit 0); the DLL goes in the same commit.
7. **Bump.** `pwsh -File tools/release.ps1 -Version 2.0.0-pre.<N+1>` (the three `package.json`, exit 0).
8. **Upstream rows.** Append to `docs/SOURCES.md` one row per replayed game PR, copied from the game's row:
   `| 11#123 | <path> | <why> |`. Commit `sync: replay 11 #a,#b → 2.0.0-pre.<N+1>`.
9. **Gate.**
   ```
   pwsh -File tools/release-check.ps1 -Check All                                   # exit 0
   pwsh -File tools/sandbox-gate.ps1 -Touched <core,modules,unity[,analyzers]>     # exit 0
   ```
   `-Touched` lists the packages the patch touches (plus `analyzers` when step 6 ran). The gate builds the sidecar,
   runs those smoke projects, then the lane's `-Compile` and `-Bee` of `Sandbox/` under one hold of the editor lock;
   exit 3 is a lock timeout (run it again), not a red build.
10. **PR.** `git push -u origin sync/pre.<N+1>`, `gh pr create --base main --fill`, title `sync: 2.0.0-pre.<N+1>`; the
    body names S, the replayed rows, the stale rows, every resolved conflict and the exit codes. The up task ends here;
    the loop merges it.

## Tag (after the up PR merged)

```
git -C C:/Users/kelns/repos/unidad fetch origin
git -C C:/Users/kelns/repos/unidad worktree add --detach C:/Users/kelns/repos/unidad-tag-pre<N+1> origin/main
cd C:/Users/kelns/repos/unidad-tag-pre<N+1>
pwsh -File tools/release.ps1 -Version 2.0.0-pre.<N+1> -Tag           # exit 0; refuses an existing tag (exit 2)
cd C:/Users/kelns/repos/unidad; git worktree remove C:/Users/kelns/repos/unidad-tag-pre<N+1>
pwsh -File tools/sync-verify.ps1 -Phase Up -Tag v2.0.0-pre.<N+1> -Replayed a,b      # exit 0
```
`-Tag` tags only a clean checkout at `origin/main` whose versions equal `-Version`, after `release-check -Check All`.
A failed check consumes no number.

## Down (repo 11-unidad-unity, one PR)

1. **Slot.** In the game: `pwsh -File tools/worktree-claim.ps1 -Branch sync-unidad-pre<N+1>` (base `origin/master`).
2. **Carry.**
   ```
   pwsh -File tools/carry-unidad.ps1 -Tag v2.0.0-pre.<N+1> -Replayed a,b -Since S
   ```
   It replaces `Packages/com.unidad.{core,modules,unity}` with the tag's files (timestamps reset), re-applies with
   `--3way` the framework edits the game merged after S (their rows stay), mirrors the tag's `tools/` except
   `tools/repo.json`, `tools/meta-roots.txt` and the `repo.json` `gameTools`, rewrites the carry block and deletes the
   replayed rows (pass the stale rows in `-Replayed` too). The result is staged, not committed. Exit 1 = a re-apply
   conflict: resolve keeping that PR's intent; its row stays. Exit 2 = dirty tree or unknown tag.
3. **Assemblies.** If `git diff --name-status refs/unidad/T refs/unidad/v2.0.0-pre.<N+1> -- 'Packages/**/*.asmdef'` is not
   empty, add or remove the matching `dotnet/Unidad.<Name>/Unidad.<Name>.csproj` and its `dotnet/Cds.Sidecar.slnx` line.
   Nothing else outside `Packages/`, `tools/` and `docs/SOURCES.md`.
4. **Checks**, each exit 0:
   ```
   bash tools/unity-metas.sh --check
   pwsh -File tools/asmdef-parity.ps1
   "C:/Program Files/Unity/Hub/Editor/7000.0.0a10/Editor/Data/DotNetSdk/dotnet.exe" build dotnet/Cds.Sidecar.slnx
   pwsh -File tools/gate.ps1
   ```
   A missing `.meta` in a carried package is a unidad bug: stop, fix it upstream, and re-tag in a new run.
5. **Commit** `sync: carry v2.0.0-pre.<N+1>` and push (the lane compiles the committed branch).
6. **Lane**, in order, each exit 0:
   ```
   pwsh -File tools/unity-lane.ps1 -Compile -Branch sync-unidad-pre<N+1>
   pwsh -File tools/unity-lane.ps1 -Bee -Branch sync-unidad-pre<N+1>
   pwsh -File tools/unity-lane.ps1 -Player -Boot -Branch sync-unidad-pre<N+1>         # -Strict on run 3 and at ship
   ```
   Copy the boot report out of `%TEMP%/unidad-lane/` (never commit it).
7. **Verify.**
   ```
   pwsh -File C:/Users/kelns/repos/unidad/tools/sync-verify.ps1 -Phase Down -Tag v2.0.0-pre.<N+1> -Game <slot> -Replayed a,b -BootReport <report> [-Strict]
   ```
8. **PR.** `gh pr create --base master --fill`, title `[unidad] carry v2.0.0-pre.<N+1>`; the body names S, the
   replayed rows, the exit codes and `Testbed: pwsh -File tools/testbed.ps1 -Branch sync-unidad-pre<N+1>` (the Room
   opens exactly as before; nothing visible changes). Release the slot from the main checkout with
   `pwsh -File tools/worktree-done.ps1 -Path <slot>`. The game's loop merges it.

A red lane step: when the cause is framework code, the fix is a unidad PR and a new tag in the next run; when it blocks
the game now, an `[unidad]` PR in the game with a Divergences row. The carry PR never patches framework files by hand.

## Ship (`v2.0.0`, on the user's call)

After `sync-unidad-3-down` merged. Up: replay as above (usually no rows), `release.ps1 -Version 2.0.0`, the README
install lines lose the "until 2.0.0 ships" note, gate, PR; then tag `v2.0.0`. Down: as above with `-Tag v2.0.0`,
`-Player -Boot -Strict` and `sync-verify -Phase Ship`. From then on 2.x follows SemVer (U-114).

## The first carry (`v2.0.0-pre.1`)

The game's base task runs `pwsh -File tools/carry-unidad.ps1 -Tag v2.0.0-pre.1 -Source C:/Users/kelns/repos/unidad`
(no `-Replayed`, no `-Since`). With no carry block in `docs/SOURCES.md` the script writes `## Carried framework` under
the title; with no `docs/SOURCES.md` at all it creates the file with an empty Divergences table.

## The game's `docs/SOURCES.md`

```markdown
## Carried framework

<!-- carry-unidad:begin -->
| Package | Tag | unidad commit |
|---|---|---|
| com.unidad.core | v2.0.0-pre.1 | <40-char commit> |
| com.unidad.modules | v2.0.0-pre.1 | <40-char commit> |
| com.unidad.unity | v2.0.0-pre.1 | <40-char commit> |
<!-- carry-unidad:end -->

## Divergences from the carried tag

| PR | Path | Why |
|---|---|---|
| #123 | Packages/com.unidad.unity/Runtime/Motion/MotionRunner.cs | MotionRunner skipped targets added during Tick |
```
A row is keyed by its PR number (`#` + digits); `Path` is a file or a folder prefix under `Packages/com.unidad.*`
(several, comma separated, are allowed); one PR may add several rows. Only `carry-unidad.ps1` writes between the markers.
`docs/SOURCES.md merge=union` keeps parallel row appends.
