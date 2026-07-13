# AgentSkills.Sdk — Specification

> Terminology: [CONTEXT.md](../CONTEXT.md). Decisions with rationale: [docs/adr/](./adr/). This document is the authoritative spec; the chat export (`chat-export-*.md`) is historical background only.

## 1. Overview

`AgentSkills.Sdk` is a meta-tooling NuGet package for .NET library maintainers. Referenced with `PrivateAssets="all"`, it hooks the maintainer's `dotnet pack` and:

1. Composes an [agentskills.io](https://agentskills.io/specification)-compliant Skill from the maintainer's markdown body, reference docs, assets, and scripts.
2. Generates a self-contained `build/[PackageId].targets` (the Consumer Targets) and embeds both into the produced `.nupkg`.

When a consumer references the maintainer's package **directly** and opts in via the Consumer Flag, the Consumer Targets copy the Skill into the workspace's agent directories (`.agents/skills/`, `.claude/skills/`, …) on build.

Guarantees:

- **Zero consumer footprint** — no transitive packages, no DLLs, no tasks on the consumer side; only declarative MSBuild XML ([ADR-0001](./adr/0001-development-dependency-delivery.md), [ADR-0002](./adr/0002-inline-csharp-pack-engine.md)).
- **Explicit opt-in** — nothing is written without the Consumer Flag ([ADR-0010](./adr/0010-explicit-opt-in-no-autodetect.md)).
- **Monorepo/version coexistence** — full-semver Skill Names; concurrent builds handled by stamp + retries ([ADR-0004](./adr/0004-full-semver-naming-no-cleanup.md), [ADR-0008](./adr/0008-stamp-and-retries-concurrency.md)).
- **Repo hygiene** — every synced skill directory is self-gitignored; nothing lands outside the selected agent directories.

## 2. Maintainer interface (.csproj)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <PackageId>MyAwesomeEngine</PackageId>
    <Version>2.4.1-preview.3</Version>

    <!-- Master switch. Defaults to true when the SDK is referenced. -->
    <GenerateAgentSkills>true</GenerateAgentSkills>

    <!-- Consumer Flag name override. Default: PackageId with non-alphanumerics
         stripped + "AgentSkills", e.g. MyAwesomeEngineAgentSkills. -->
    <AgentSkillsConsumerFlag>MyEngineSkills</AgentSkillsConsumerFlag>

    <!-- Skill Body: pure markdown, NO frontmatter (ADR-04 of the original design).
         Unset: body is scaffolded from <Description> + PackageReadmeFile.
         Set but missing on disk: pack error AGSK003. -->
    <AgentSkillBodyFile>$(MSBuildProjectDirectory)\..\agent\instructions.md</AgentSkillBodyFile>

    <!-- Full-file override: complete SKILL.md including frontmatter, copied
         blindly as the single source for BOTH variants. All scaffolding and
         frontmatter synthesis is skipped. Maintainer owns correctness. -->
    <!-- <AgentSkillFullFile>$(MSBuildProjectDirectory)\..\agent\SKILL.md</AgentSkillFullFile> -->

    <!-- Identity overrides. Name is validated (AGSK002), never rewritten. -->
    <AgentSkillNameOverride></AgentSkillNameOverride>
    <AgentSkillDescriptionOverride>Expert rules for MyAwesomeEngine APIs.</AgentSkillDescriptionOverride>

    <!-- Claude Code extensions; emitted top-level in the claude variant,
         under metadata: in the agents variant (ADR-0003). Omitted when empty. -->
    <AgentSkillUserInvocable>true</AgentSkillUserInvocable>
    <AgentSkillContextStrategy>fork</AgentSkillContextStrategy>
  </PropertyGroup>

  <ItemGroup>
    <!-- Level 3 content. RecursiveDir structure is preserved under each bucket. -->
    <AgentSkillReferenceFiles Include="$(MSBuildProjectDirectory)\..\docs\**\*.md" />
    <AgentSkillAssetFiles    Include="$(MSBuildProjectDirectory)\..\templates\appsettings.default.json" />
    <AgentSkillScriptFiles   Include="$(MSBuildProjectDirectory)\..\tools\diagnose.py" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="AgentSkills.Sdk" Version="1.0.0" PrivateAssets="all" />
  </ItemGroup>
</Project>
```

Implicit behavior:

- `PackageReadmeFile`, when set and present, is added as `references/README.md` unless the maintainer already includes a file at that path.
- `AgentSkillFullFile` and `AgentSkillBodyFile` are mutually exclusive; both set = pack error AGSK006.

## 3. Skill identity and naming

Skill Name (and directory name — the spec requires they match):

```
use-<sanitized-packageid>-v<sanitized-version>
```

Sanitize pipeline (applies to PackageId and Version segments):

1. Lowercase (invariant).
2. Strip semver build metadata (`+` and everything after).
3. Replace every run of characters outside `[a-z0-9]` with a single `-`.
4. Trim leading/trailing `-`; collapse `--`.

Example: `MyAwesome.Engine` `2.4.1-preview.3+sha.abc` → `use-myawesome-engine-v2-4-1-preview-3`.

Validation ([ADR-0009](./adr/0009-hard-error-on-name-overflow.md)):

- Result over 64 chars → pack error AGSK001 telling the maintainer to set `AgentSkillNameOverride`.
- `AgentSkillNameOverride` set → validated against the full agentskills.io rules (1–64 chars, `[a-z0-9-]`, no leading/trailing/consecutive hyphens); violation → AGSK002. Never silently rewritten.
- Description (override or default `Guidance and rules for integrating <PackageId> APIs.`) over 1024 chars → AGSK004.

## 4. nupkg layout

```
build/
  <PackageId>.targets          ← Consumer Targets (generated XML)
agent-assets/
  SKILL.claude.md              ← claude Variant (Claude fields top-level)
  SKILL.agents.md              ← agents Variant (spec-pure; extensions in metadata:)
  skill.gitignore              ← copied to <skill dir>/.gitignore (content: *)
  skill.stamp                  ← copied last to <skill dir>/.agentskills-stamp (content: package version)
  payload/
    references/...             ← @(AgentSkillReferenceFiles), RecursiveDir preserved
    assets/...                 ← @(AgentSkillAssetFiles)
    scripts/...                ← @(AgentSkillScriptFiles)
```

The Skill Payload exists once; only `SKILL.md` differs per Variant ([ADR-0003](./adr/0003-per-target-frontmatter-variants.md)). With `AgentSkillFullFile`, both variant files carry identical content.

Frontmatter, claude variant:

```yaml
---
name: use-myawesome-engine-v2-4-1-preview-3
description: Expert rules for MyAwesomeEngine APIs.
user-invocable: true      # only when AgentSkillUserInvocable set
context: fork             # only when AgentSkillContextStrategy set
---
```

agents variant (spec-pure; `metadata:` values are strings per spec):

```yaml
---
name: use-myawesome-engine-v2-4-1-preview-3
description: Expert rules for MyAwesomeEngine APIs.
metadata:
  package-id: MyAwesome.Engine
  package-version: 2.4.1-preview.3
  user-invocable: "true"
  context: "fork"
---
```

Description values are YAML single-quoted with `'` doubled; embedded newlines are replaced with spaces.

## 5. Pack pipeline (maintainer side)

Implemented by the Pack Engine — an inline C# task ([ADR-0002](./adr/0002-inline-csharp-pack-engine.md)). Hook points:

- Public target `AgentSkillsGeneratePack` runs `BeforeTargets="GenerateNuspec"`, gated on `'$(GenerateAgentSkills)' == 'true'`.
- Package files are contributed via the **supported** NuGet extension point: `TargetsForTfmSpecificContentInPackage` + `TfmSpecificPackageFile`. The private `_PackageFiles` / `_GetPackageFiles` API from earlier drafts is banned.
- Multi-targeting: content is emitted only for the first entry of `$(TargetFrameworks)` (guard `'$(TargetFramework)' == '<first>'`) so multi-TFM packs don't produce duplicate-path warnings.

Steps:

1. Compute Skill Name, description; validate (section 3).
2. Read Skill Body (`File.ReadAllText` in C# — no MSBuild escaping hazards). Unset → scaffold: `# <PackageId> guide` + `<Description>` + pointer to `references/`. Set-but-missing → AGSK003.
3. Compose both Variants (or pass `AgentSkillFullFile` through blindly).
4. Render Consumer Targets from a C# template — literal `$(...)` for consumer-time properties is written as plain text; the pack-time values interpolated in are: sanitized package id, sanitized version, Skill Name, Consumer Flag name, package version string, agent-token map.
5. Write all generated files under `$(IntermediateOutputPath)agentskills/`, register with `TfmSpecificPackageFile`.

The Consumer Flag name is derived by stripping every non-alphanumeric from `PackageId` and appending `AgentSkills` — MSBuild property names cannot contain dots.

## 6. Consumer Targets semantics

Sketch of the generated file (illustrative; placeholders `{...}` are interpolated at pack time — everything with `$(...)` is literal text evaluated on the consumer's machine):

```xml
<Project>
  <Target Name="AgentSkillsSync_{safeid}_{safeversion}"
          BeforeTargets="BeforeBuild"
          Condition="'$({flag})' != '' AND '$(DesignTimeBuild)' != 'true'">

    <!-- 1. Workspace Root (ADR-0007) -->
    <PropertyGroup>
      <_AgsRoot Condition="'$(AgentSkillsRoot)' != ''">$(AgentSkillsRoot)</_AgsRoot>
      <_AgsRoot Condition="'$(_AgsRoot)' == '' AND '$(SolutionDir)' != '' AND '$(SolutionDir)' != '*Undefined*'">$(SolutionDir)</_AgsRoot>
      <!-- unrolled 8-level walk-up; Exists() matches .git as dir or file (worktrees) -->
      <_AgsRoot Condition="'$(_AgsRoot)' == '' AND Exists('$(MSBuildProjectDirectory)\.git')">$(MSBuildProjectDirectory)\</_AgsRoot>
      <_AgsRoot Condition="'$(_AgsRoot)' == '' AND Exists('$(MSBuildProjectDirectory)\..\.git')">$(MSBuildProjectDirectory)\..\</_AgsRoot>
      <!-- ... levels 3–8 ... -->
      <_AgsFallback Condition="'$(_AgsRoot)' == ''">true</_AgsFallback>
      <_AgsRoot   Condition="'$(_AgsRoot)' == ''">$(MSBuildProjectDirectory)\</_AgsRoot>
    </PropertyGroup>
    <Warning Condition="'$(_AgsFallback)' == 'true'"
             Text="[{PackageId}] No AgentSkillsRoot, SolutionDir or .git found; skills placed next to the project." />

    <!-- 2. Token → destination map (ADR-0005; baked at pack time, exact match, case-insensitive) -->
    <ItemGroup>
      <_AgsTokens Include="$({flag})" />
      <_AgsDest Include=".agents\skills"  Variant="agents"
                Condition="'@(_AgsTokens->AnyHaveMetadataValue('Identity','universal'))' == 'true' OR ..." />
      <_AgsDest Include=".claude\skills"  Variant="claude"
                Condition="'@(_AgsTokens->AnyHaveMetadataValue('Identity','claude-code'))' == 'true'" />
      <!-- unknown tokens → <Warning>; duplicates removed -->
    </ItemGroup>

    <!-- 3. Per-destination sync, batched over %(_AgsDest.Identity); skipped when
         the Version Stamp already matches (ADR-0008) -->
    <!--   dest dir: $(_AgsRoot)%(_AgsDest.Identity)\{skillname}\               -->
    <!--   Copy payload/** + SKILL.%(Variant).md → SKILL.md                     -->
    <!--   Copy agent-assets/skill.gitignore → .gitignore, agent-assets/skill.stamp →       -->
    <!--     .agentskills-stamp (LAST). Everything is Copy with                 -->
    <!--     SkipUnchangedFiles="true" Retries="3" RetryDelayMilliseconds="200" -->
    <!--     — WriteLinesToFile has no retry and races on concurrent first sync -->
  </Target>
</Project>
```

Binding rules (normative):

- Target name embeds sanitized package id + version — no collisions between packages or versions.
- Sources resolve from `$(MSBuildThisFileDirectory)..\agent-assets\` — written as **literal text** by the Pack Engine, evaluated on the consumer machine (the historical pack-time-expansion bug is impossible by construction: the template is a C# string, not an MSBuild property).
- The flag check generates as a literal property reference: `'$(MyAwesomeEngineAgentSkills)' != ''` — the flag *name* is interpolated at pack time, the *value* is read at consumer build time.
- Stamp file `.agentskills-stamp` contains the exact package version; it is written last so an interrupted sync re-runs. Since the directory name is version-unique and content immutable, a present-and-matching stamp makes the target a no-op.
- `.gitignore` containing `*` sits inside each synced skill directory — the skill, its stamp, and the gitignore itself never reach source control. The workspace's own `.agents/` root is untouched otherwise (consumers may keep hand-written skills beside synced ones).

## 7. Diagnostics

| Code | Severity | Condition |
|---|---|---|
| AGSK001 | error | Computed Skill Name exceeds 64 chars (set `AgentSkillNameOverride`) |
| AGSK002 | error | `AgentSkillNameOverride` violates agentskills.io name rules |
| AGSK003 | error | `AgentSkillBodyFile` set but not found |
| AGSK004 | error | Description exceeds 1024 chars |
| AGSK005 | error | `AgentSkillFullFile` set but not found |
| AGSK006 | error | Both `AgentSkillFullFile` and `AgentSkillBodyFile` set |
| AGSK101 | warning | Unknown Agent Token in Consumer Flag (skipped) |
| AGSK102 | warning | Workspace Root fell back to project directory |

## 8. Out of scope (v1) / v2 candidates

- **Cleanup manifest** for stale version directories — deliberate non-goal in v1 ([ADR-0004](./adr/0004-full-semver-naming-no-cleanup.md)).
- **`buildTransitive/`** ([ADR-0006](./adr/0006-build-only-no-buildtransitive.md)).
- **XmlDoc → markdown** reference generation.
- **MCP fragment merging** (Imprint feature) — would require JSON handling on the consumer side.
- **Multiple skills per package** — v1 packs exactly one skill; layout (`agent-assets/payload/`) leaves room for `agent-assets/<skill>/` later.
- **Global/per-user skill dirs** (`~/.agents/skills/`) — project-level only.
