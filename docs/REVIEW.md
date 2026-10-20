# Review list (unidad)

What the loop's reviewers check on every PR of this repository, besides the generic list in
`tools/loop/review-merge.md`. Per-repo data (U-40): a game writes its own.

- **Framework boundary.** No game names, ids, colours or content under `Packages/com.unidad.*` (`tools/check-tree.ps1`
  catches `Cds`; the rest is judgement). Generic modules use `Unidad.<Module>` namespaces.
- **Engine-free stays engine-free.** `Unidad.Core` and the modules are `noEngineReferences` and never reference
  `UnityEngine`; engine code lives only in `Unidad.Adapter` (+ `.Editor`). No namespace segment is `Unity`.
- **No mutable statics** (UD014), and randomness only through `ISeedSource` / `DeterministicRandom`.
- **Public API has a home.** Every public type belongs to the contracts surface; new public API is a new file, never a
  new member on an existing contract (U-13).
- **Each assembly is complete.** asmdef and csproj agree (`tools/asmdef-parity.ps1`), with `csc.rsp`
  (`-langversion:14`) and `Polyfills.cs`; no netstandard2.1-missing API.
- **Kept trees untouched.** `Tools~/`, `Skills~/` and `Documentation~/model-pipeline/` never change (U-112;
  `pwsh -File tools/release-check.ps1 -Check KeptTrees`).
- **The analyzer DLL changes only with its source**: `pwsh -File analyzers/build.ps1 -Check` is green.
- **Provenance.** Every port has its `docs/SOURCES.md` row; a behaviour that differs from 10 has a DECISIONS entry.
- **Versions.** `package.json` `version` and `com.unidad.*` dependency fields change only in a release run (U-114).
- **Metas.** Every new file and folder under a meta root has its `.meta` with the md5-of-path GUID
  (`bash tools/unity-metas.sh --check`). **No move or rename of a file a scene, prefab, material or PanelSettings
  references** (its GUID comes from its path).
- **Smoke tests** are the PR's own, small and self-contained; golden files and artifacts read back are removed.
