# AgentSkills.Sdk — Implementation Plan

Spec: [spec.md](./spec.md). All builds/tests run through Docker (no local .NET SDK — see CLAUDE.md):

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:9.0 dotnet test
```

## Phase 1 — SDK skeleton

- `src/AgentSkills.Sdk/AgentSkills.Sdk.csproj`: `DevelopmentDependency=true`, no `lib/`, packs `build/AgentSkills.Sdk.props` + `build/AgentSkills.Sdk.targets`.
- Props: defaults (`GenerateAgentSkills=true`, consumer-flag derivation).
- Targets: `AgentSkillsGeneratePack` hooked `BeforeTargets="GenerateNuspec"` + `TargetsForTfmSpecificContentInPackage` wiring, first-TFM guard.
- Exit: `dotnet pack` of the SDK itself succeeds; a fixture library referencing it packs unchanged output when `GenerateAgentSkills=false`.

## Phase 2 — Core logic as testable C#

- `src/AgentSkills.Sdk/tasks/*.cs` compiled by `RoslynCodeTaskFactory`; same sources referenced by `tests/AgentSkills.Sdk.Tests`.
- Units: sanitize pipeline, name/description validation (AGSK001/2/4), YAML quoting, frontmatter composition for both Variants, consumer-flag name derivation.
- TDD: name rules table from agentskills.io as test cases (64 chars, hyphens, prerelease, `+build` metadata, unicode input).
- Exit: unit suite green in Docker.

## Phase 3 — Pack Engine

- Inline task: read body / scaffold fallback / full-file passthrough (AGSK003/5/6), compose `SKILL.claude.md` + `SKILL.agents.md`, stage payload items, register `TfmSpecificPackageFile`.
- Exit: fixture pack produces nupkg with exact spec.md §4 layout (assert by unzipping in test).

## Phase 4 — Consumer Targets generator

- C# template rendering the generated `.targets`: workspace-root chain (8-level `.git` walk-up), token map + unknown-token warning, stamp short-circuit, `Copy Retries`, per-dir `.gitignore`, `DesignTimeBuild` guard.
- Snapshot tests: generated XML for representative inputs (dotted PackageId, prerelease version, custom flag name) — parse with `XDocument` to prove well-formed, assert literal `$(SolutionDir)` etc. survive as text.
- Exit: generated file for the fixture package matches approved snapshot.

## Phase 5 — Integration matrix

Consume the packed fixture from a local NuGet feed in test trees:

| Case | Asserts |
|---|---|
| single project, flag unset | no writes anywhere |
| single project, `universal` | `.agents/skills/<name>/` complete, `.gitignore`, stamp |
| `claude-code;universal` | both dirs, correct Variant per dir |
| unknown token | AGSK101 warning, build succeeds |
| monorepo, 6 projects, same version, `-m` parallel build | one dir, zero errors across 20 repeated runs |
| monorepo, v1 + v2 of fixture package | both dirs coexist, no thrash on rebuild |
| `dotnet build` on bare csproj inside git repo | root found via walk-up |
| no git, no sln | project-dir fallback + AGSK102 |
| second build | sync target skipped (stamp fast path; assert via binlog) |
| multi-TFM fixture | single content set, no NU5118 |

- Exit: matrix green in Docker; README quick-start for maintainers written.

## Ordering & risk

Phases sequential; 2 is the highest-value early failure detector (naming/YAML edge cases), 5 is the credibility gate for every concurrency/monorepo claim in the ADRs. Nothing ships before Phase 5 passes repeated parallel-build runs.
