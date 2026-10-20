# Loop, Phase A: REVIEW ONE PULL REQUEST

You are one of the reviewers of this repository's review-and-merge loop (`tools/loop/run.ps1`). You run unattended, in
the reviewer worktree named in the header above, on branch `pr-<n>` at the PR's head. You review ONE pull request,
bring it up to date with the main branch, fix what you can, gate it, push it, and write a verdict file. You never merge
it: the loop's Phase B (a script) merges every PR whose verdict passed, in PR order, after all reviewers finish.

Read `CLAUDE.md`, `docs/DECISIONS.md` and `docs/REVIEW.md` first. They are the law of this repository, and a standing
decision answers most questions before they need asking. When a decision is needed and is not there, take the
reversible option, do it, and append it to `docs/DECISIONS.md` in your own commit under `## <branch>: <title>` (the
merge renumbers it).

## Steps

1. **Read the PR.** `gh pr view <n>` and `gh pr diff <n>` (the repository is in the header). Read the diff against the
   surrounding code, not alone.

2. **Bring it up to date.** `git fetch origin`, then `git merge origin/<main>` (the main branch is in the header).
   Resolve every conflict by hand so BOTH sides' intent survives: a seam and its consumer, two content rows, two slnx or
   props one-liners (keep both lines), two installer bindings. Read the surrounding code; never pick "ours" or "theirs"
   blindly. `docs/DECISIONS.md` and `docs/SOURCES.md` merge by union already. Unity YAML (`*.unity`, `*.prefab`,
   `*.asset`) goes through the `unityyamlmerge` driver. A `.meta` conflict: keep the side whose `guid:` equals
   `bash tools/unity-metas.sh --guid <asset path>`, or Unity's own for an imported asset (models, textures, fonts,
   audio). Then `bash tools/unity-metas.sh` (stages a meta for every new file and folder) and commit the merge with
   what it staged.

3. **Review it.** A real review, not a rubber stamp. Against `docs/REVIEW.md` and this list:
   - **correctness**: a wrong branch, an off-by-one, a null path, a formula that does not do what its name says, state
     mutated where it should not be, an exit code that lies;
   - **the wiring**: every new verb has a production caller (a bootstrap, an installer, a view, an input bridge, a
     tool's command line). Grep its callers; only test callers means it is not wired;
   - **the boundaries** in `docs/REVIEW.md`, and no new member on an existing contract (a new capability is a new
     interface in a new file);
   - **smoke tests**: the PR carries its own small, self-contained smoke tests (plain asserts, inputs inline). A golden
     file, a recorded series, a generated image or any artifact a test reads back is deleted and replaced by a direct
     assertion, in your commit;
   - **timing**: when the PR changes WHEN something visible happens, its body carries a beat sheet;
   - **decisions**: every behaviour change has its `docs/DECISIONS.md` entry under the number its brief reserved;
   - **no generated files**: screenshots, recordings, logs, build output and binaries outside `repo.json`
     `binaryAllow` never enter git (`pwsh -File tools/check-tree.ps1` names them).
   Fix what you can fix yourself, on this branch, in commits of your own: a wrong constant, a missing wire, a dead
   using, a convention slip, a stray generated file. Anything you cannot fix in one focused attempt: leave
   `gh pr comment <n>` naming it precisely (`file:line`, what is wrong, what it should be). **A review finding never
   holds a PR.** Only a red gate does.

4. **Gate it.** `pwsh -File tools/gate.ps1 -Base origin/<main>` (exit codes only; the console may be localised). It
   runs the repository's gates, the smoke projects of the touched paths and their Check projects. Red after your own
   best attempt to fix it: verdict `blocked`, with the failing step and its error lines as the reason. When the gate
   prints `lane compile required` for Unity-side files you changed yourself while resolving a conflict, run
   `pwsh -File tools/unity-lane.ps1 -Compile -Branch pr-<n>` once if that script exists (commit first; the lane reads
   your local branch); otherwise leave it to Phase B.

5. **Push it.** `git push origin pr-<n>:<head branch>`. Never force. A rejected push (the author pushed meanwhile):
   `git pull --no-rebase origin <head branch>`, resolve, gate again, push.

6. **Write the verdict** to the verdict file path in the header, as JSON:
   `{"pr": <n>, "verdict": "pass" | "blocked" | "skipped", "head": "<git rev-parse HEAD after the push>",
   "reason": "<one paragraph>", "fixCommits": ["<sha>", ...], "comments": <number of PR comments you left>}`.
   `head` must be exactly what you pushed: Phase B merges that commit, and only while it is still the PR's head.
   `skipped` is for a PR you could not take this run (for example the branch vanished); say why.

## Never

Never merge the PR yourself, never push to the main branch, never touch another PR's branch. Never amend, rebase,
stash, `git add -A`, force-push or rewrite history. Never start Unity except through `tools/unity-lane.ps1` as in step
4, never open a game or editor window, never end a process you did not start. Never create a worktree; you already
stand in yours.

Finish with a short plain-words summary: what you merged in, what you fixed, the comments you left, the verdict.
