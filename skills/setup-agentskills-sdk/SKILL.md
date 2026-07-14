---
name: setup-agentskills-sdk
description: Setup or onboard a .NET library project onto AgentSkills.Sdk so its NuGet package ships an agent skill. Use when a maintainer says "setup AgentSkills.Sdk", "add agent skills to my package", "ship a SKILL.md in my nupkg", or asks how to wire AgentSkillBodyFile / AgentSkillReferenceFiles.
---

# Setup AgentSkills.Sdk

Onboard a Maintainer's library project onto [AgentSkills.Sdk](https://www.nuget.org/packages/AgentSkills.Sdk): package reference, doc-source wiring in the `.csproj`, pack verification.

The full property surface is in [references/property-table.md](references/property-table.md) — read it before step 4.

## Step 1 — Pick the target project

List `*.csproj` files in the repo (skip `bin/`, `obj/`, test projects). If exactly one packable library project exists, or the conversation already names one, use it. Otherwise ask the Maintainer which project to set up — do not guess.

**Done when:** one `.csproj` path is chosen.

## Step 2 — Install the package

Check the csproj for `<PackageReference Include="AgentSkills.Sdk"`. If missing:

```bash
dotnet add <csproj> package AgentSkills.Sdk
```

No version pin — latest stable. Then make sure the reference carries `PrivateAssets="all"` (add it if `dotnet add` didn't).

**Done when:** the csproj contains the reference with `PrivateAssets="all"`.

## Step 3 — Version drift guard

```bash
dotnet restore <csproj>
ls ~/.nuget/packages/agentskills.sdk/
```

Compare the installed version's major against the version documented at the top of [references/property-table.md](references/property-table.md). If the majors differ, read `~/.nuget/packages/agentskills.sdk/<installed>/README.md` and treat it as the authoritative property surface instead of the table.

**Done when:** you know which property reference you're working from.

## Step 4 — Choose doc sources

Scan the repo for `.md` files (exclude `bin/`, `obj/`, `node_modules/`, `.git/`). Offer candidates:

- **Skill Body** (`AgentSkillBodyFile`): one markdown file of agent-facing usage instructions, no frontmatter. Leaving it unset is valid if `<Description>` or `<PackageReadmeFile>` are set — the body is then scaffolded from them.
- **Reference docs** (`AgentSkillReferenceFiles`): API docs, guides — propose a glob like `docs/**/*.md`.

Ask the Maintainer to confirm or adjust. If no suitable `.md` files exist or they are **outdated**, **inconclusive** or clearly **too small to cover the project**. Ask the Maintainer, and invoke the `author-skill-docs` skill (the Authoring Skill, sibling Maintainer Skill in this repo) to draft them, then continue with the files it produced.

**Done when:** a body-file decision (path or scaffold) and a reference-file glob list are agreed.

## Step 5 — Wire the csproj

Add to the target csproj. The paths below are **examples only** — substitute the actual paths agreed in step 4; omit `AgentSkillBodyFile` if scaffolding was chosen:

```xml
<PropertyGroup>
  <!-- EXAMPLE paths — replace with the step-4 decisions -->
  <AgentSkillBodyFile>$(MSBuildProjectDirectory)/../docs/agent-instructions.md</AgentSkillBodyFile>
</PropertyGroup>
<ItemGroup>
  <AgentSkillReferenceFiles Include="$(MSBuildProjectDirectory)/../docs/**/*.md" />
</ItemGroup>
```

Then offer the optional extras — one short question each, skip anything declined; details in [references/property-table.md](references/property-table.md):

- `AgentSkillAssetFiles` / `AgentSkillScriptFiles` — config templates, diagnostic scripts
- `AgentSkillIncludeXmlDocs` — ship the compiler XML doc file as a greppable `references/api-docs.xml` (auto-enables doc generation, suppresses CS1591)
- `AgentSkillNameOverride` / `AgentSkillDescriptionOverride` — identity tuning
- `AgentSkillUserInvocable` / `AgentSkillContextStrategy` — Claude Code extensions
- `AgentSkillsConsumerFlag` — rename the Consumer Flag

**Done when:** the csproj edits are saved and every extra was offered exactly once.

## Step 6 — Verify the pack

```bash
dotnet pack <csproj> -c Release
unzip -l <path-to>.nupkg | grep -E 'agent-assets/|build/'
```

The nupkg must contain `agent-assets/SKILL.claude.md`, `agent-assets/SKILL.agents.md`, and `build/<PackageId>.targets` (plus `agent-assets/payload/references/...` when reference files were wired). On an AGSK error, apply the fix and re-pack:

| Error | Fix |
|---|---|
| AGSK001 | Skill name over 64 chars — set `AgentSkillNameOverride` to a short `[a-z0-9-]` name |
| AGSK002 | Override violates name rules — 1–64 chars, `[a-z0-9-]`, no leading/trailing/double hyphens |
| AGSK003 | `AgentSkillBodyFile` points at a missing file — fix the path |
| AGSK004 | Description over 1024 chars — shorten `AgentSkillDescriptionOverride` |
| AGSK005 | `AgentSkillFullFile` points at a missing file — fix the path |
| AGSK006 | Both `AgentSkillFullFile` and `AgentSkillBodyFile` set — remove one |

**Done when:** pack succeeds and both `agent-assets/` entries and the `.targets` file are listed.

## Step 7 — Hand over the consumer snippet

Tell the Maintainer the Consumer Flag name (PackageId minus non-alphanumerics + `AgentSkills`, unless renamed) and show the opt-in their consumers use:

```xml
<PropertyGroup>
  <!-- EXAMPLE — property name is the package's real Consumer Flag -->
  <MyAwesomeEngineAgentSkills>claude-code;universal</MyAwesomeEngineAgentSkills>
</PropertyGroup>
```

**Done when:** the snippet with the real flag name has been shown to the Maintainer.
