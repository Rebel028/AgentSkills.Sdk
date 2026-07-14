# AgentSkills.Sdk property reference

Documented for AgentSkills.Sdk **1.0.0**. If the installed major version differs (`ls ~/.nuget/packages/agentskills.sdk/`), this table may be stale — read `~/.nuget/packages/agentskills.sdk/<version>/README.md` instead; the packed README is the authoritative surface for the installed version.

## Properties (`PropertyGroup`)

| Property | Default | Purpose |
|---|---|---|
| `GenerateAgentSkills` | `true` when the SDK is referenced | Master switch for the whole pack step |
| `AgentSkillBodyFile` | unset → body scaffolded from `<Description>` + `PackageReadmeFile` | Skill Body: pure markdown, **no frontmatter**; frontmatter is synthesized. Set-but-missing → AGSK003 |
| `AgentSkillFullFile` | unset | Complete `SKILL.md` including frontmatter, copied as-is for both variants; skips all scaffolding. Mutually exclusive with `AgentSkillBodyFile` (AGSK006); missing file → AGSK005 |
| `AgentSkillNameOverride` | computed `use-<sanitized-packageid>-v<sanitized-version>` | Skill name override. Validated (1–64 chars, `[a-z0-9-]`, no leading/trailing/consecutive hyphens → AGSK002), never rewritten. Computed name over 64 chars → AGSK001 |
| `AgentSkillDescriptionOverride` | `Guidance and rules for integrating <PackageId> APIs.` | Skill description; over 1024 chars → AGSK004 |
| `AgentSkillUserInvocable` | unset (omitted) | Claude Code extension; top-level `user-invocable:` in the claude variant, `metadata:` in the agents variant |
| `AgentSkillContextStrategy` | unset (omitted) | Claude Code extension; emitted as `context:` (e.g. `fork`), same variant placement |
| `AgentSkillIncludeXmlDocs` | unset (off) | Ships the compiler XML doc file verbatim as `references/api-docs.xml` plus a static navigation guide `references/api-docs-guide.md`. When `DocumentationFile` is empty, the SDK sets it and appends `1591` to `$(NoWarn)` (no CS1591 noise); the opt-in wins over `GenerateDocumentationFile=false`. No XML file found at pack time → AGSK007 |
| `AgentSkillsConsumerFlag` | PackageId minus non-alphanumerics + `AgentSkills` | Name of the MSBuild property consumers set to opt in (property names can't contain dots) |

## Items (`ItemGroup`)

| Item | Lands in nupkg under | Purpose |
|---|---|---|
| `AgentSkillReferenceFiles` | `agent-assets/payload/references/` | Docs shipped beside `SKILL.md`; `RecursiveDir` structure preserved |
| `AgentSkillAssetFiles` | `agent-assets/payload/assets/` | Config templates, sample files |
| `AgentSkillScriptFiles` | `agent-assets/payload/scripts/` | Diagnostic / helper scripts |

## Implicit behavior

- `PackageReadmeFile`, when set and present, is added as `references/README.md` unless the Maintainer already ships a file at that path.
- With `AgentSkillIncludeXmlDocs`, Maintainer files already at `references/api-docs.xml` or `references/api-docs-guide.md` win silently over the generated ones. The `.xml` also lands in `lib/` per standard NuGet behavior (IntelliSense).
- Skill name sanitize pipeline: lowercase → strip semver build metadata → non-`[a-z0-9]` runs become `-` → trim/collapse hyphens. `MyAwesome.Engine` `2.4.1-preview.3` → `use-myawesome-engine-v2-4-1-preview-3`.

## Consumer side (for the hand-over snippet)

- Consumer Flag value: semicolon list of Agent Tokens ([vercel-labs/skills](https://github.com/vercel-labs/skills#supported-agents) `--agent` ids). `claude-code` → `.claude/skills/`; `universal` and most others → `.agents/skills/`. Unknown token → warning AGSK101.
- Workspace root: `$(AgentSkillsRoot)` → `$(SolutionDir)` → `.git` walk-up (8 levels) → project directory (warning AGSK102).
