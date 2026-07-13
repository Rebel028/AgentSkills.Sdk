# AgentSkills.Sdk

Ship [agentskills.io](https://agentskills.io/specification)-compliant **agent skills** inside your NuGet package. At `dotnet pack` time the SDK composes a `SKILL.md` (two variants: Claude Code and spec-pure) plus your reference docs, assets and scripts into the nupkg, together with a generated, self-contained `build/<PackageId>.targets`. When a consumer opts in, that targets file copies the skill into their workspace's agent directories (`.claude/skills/`, `.agents/skills/`, …) on build.

- **Zero consumer footprint** — no transitive packages, no DLLs, no tasks on the consumer side; only declarative MSBuild XML.
- **Explicit opt-in** — nothing is written unless the consumer sets the Consumer Flag.
- **Monorepo-safe** — versioned skill directories, stamp-guarded no-op rebuilds, `Copy` retries for parallel builds.
- **Repo hygiene** — every synced skill directory gitignores itself.
- **Toolchain range** — pack and consume verified on .NET 8, 9 and 10 SDKs (CI-guarded); the package ships no binaries, so no runtime dependency exists.

## Maintainer quick start

1. Reference the SDK (pack-time only — `PrivateAssets="all"`):

```xml
<ItemGroup>
  <PackageReference Include="AgentSkills.Sdk" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

2. `dotnet pack`. That's it — with no further configuration the skill body is scaffolded from your `<Description>` and `PackageReadmeFile`.

3. Optionally shape the skill:

```xml
<PropertyGroup>
  <!-- Master switch; true by default when the SDK is referenced. -->
  <GenerateAgentSkills>true</GenerateAgentSkills>

  <!-- Markdown body WITHOUT frontmatter; frontmatter is synthesized. -->
  <AgentSkillBodyFile>$(MSBuildProjectDirectory)/../agent/instructions.md</AgentSkillBodyFile>

  <!-- OR a complete SKILL.md copied as-is (mutually exclusive with body file). -->
  <!-- <AgentSkillFullFile>$(MSBuildProjectDirectory)/../agent/SKILL.md</AgentSkillFullFile> -->

  <!-- Identity overrides; the name is validated, never rewritten. -->
  <AgentSkillDescriptionOverride>Expert rules for MyAwesomeEngine APIs.</AgentSkillDescriptionOverride>
  <!-- <AgentSkillNameOverride>use-my-engine</AgentSkillNameOverride> -->

  <!-- Claude Code extensions (top-level in the claude variant, metadata: in the agents variant). -->
  <AgentSkillUserInvocable>true</AgentSkillUserInvocable>
  <AgentSkillContextStrategy>fork</AgentSkillContextStrategy>

  <!-- Consumer Flag name override; default: PackageId minus non-alphanumerics + "AgentSkills". -->
  <!-- <AgentSkillsConsumerFlag>MyEngineSkills</AgentSkillsConsumerFlag> -->
</PropertyGroup>

<ItemGroup>
  <AgentSkillReferenceFiles Include="$(MSBuildProjectDirectory)/../docs/**/*.md" />
  <AgentSkillAssetFiles    Include="$(MSBuildProjectDirectory)/../templates/appsettings.default.json" />
  <AgentSkillScriptFiles   Include="$(MSBuildProjectDirectory)/../tools/diagnose.sh" />
</ItemGroup>
```

## Consumer usage

Consumers of *your* package opt in per project (or once in `Directory.Build.props`):

```xml
<PropertyGroup>
  <!-- Flag name derives from your PackageId: MyAwesome.Engine → MyAwesomeEngineAgentSkills -->
  <MyAwesomeEngineAgentSkills>claude-code;universal</MyAwesomeEngineAgentSkills>
</PropertyGroup>
```

Tokens follow the [vercel-labs/skills](https://github.com/vercel-labs/skills#supported-agents) `--agent` ids: `claude-code` targets `.claude/skills/`; `universal`, `opencode`, `codex`, `cursor`, `gemini-cli`, `github-copilot`, `amp`, `cline`, `zed`, `warp` target `.agents/skills/`; a token containing `/` is a custom destination relative to the workspace root. Unknown tokens warn (AGSK101) and are skipped.

The workspace root resolves via `$(AgentSkillsRoot)` → `$(SolutionDir)` → `.git` walk-up (8 levels) → project directory (with warning AGSK102).

Skill directories are named `use-<packageid>-v<version>` — versions coexist; deleting `.agents/skills/use-<pkg>-*` is always safe (the current version re-syncs on the next build).

## Diagnostics

| Code | Severity | Condition |
|---|---|---|
| AGSK001 | error | Computed skill name exceeds 64 chars (set `AgentSkillNameOverride`) |
| AGSK002 | error | `AgentSkillNameOverride` violates agentskills.io name rules |
| AGSK003 | error | `AgentSkillBodyFile` set but not found |
| AGSK004 | error | Description exceeds 1024 chars |
| AGSK005 | error | `AgentSkillFullFile` set but not found |
| AGSK006 | error | Both `AgentSkillFullFile` and `AgentSkillBodyFile` set |
| AGSK101 | warning | Unknown Agent Token in the Consumer Flag (skipped) |
| AGSK102 | warning | Workspace root fell back to the project directory |

## Source & issues

[github.com/Rebel028/AgentSkills.Sdk](https://github.com/Rebel028/AgentSkills.Sdk)
