# AgentSkills.Sdk

Meta-tooling SDK for .NET package maintainers: embeds LLM agent skills into a `.nupkg` at pack time and syncs them into a consumer's workspace at build time.

## Language

### Roles

**Maintainer**:
The author of a NuGet library who references the SDK to ship skills inside their package.
_Avoid_: package owner, developer (ambiguous)

**Consumer**:
A developer whose project references the Maintainer's package and may opt in to receive skills.
_Avoid_: end-user, client

### Artifacts

**Skill**:
A self-contained directory with a `SKILL.md` plus optional `references/`, `assets/`, `scripts/`, per the [agentskills.io](https://agentskills.io/specification) spec.
_Avoid_: agent docs, plugin

**Skill Payload**:
Everything in a Skill except `SKILL.md`: references, assets, scripts. Stored once in the nupkg, shared by all Variants.

**Variant**:
A target-specific rendering of `SKILL.md`. The `claude` Variant carries Claude Code frontmatter keys top-level; the `agents` Variant is spec-pure with extensions under `metadata:`.

**Skill Body**:
Pure markdown content (no frontmatter) supplied by the Maintainer via `AgentSkillBodyFile`, or scaffolded from package metadata.

**Consumer Targets**:
The generated `build/[PackageId].targets` file shipped inside the Maintainer's nupkg; the only thing that executes on the Consumer's machine.
_Avoid_: consumer script, sync script

**Pack Engine**:
The inline C# MSBuild task (RoslynCodeTaskFactory) inside the SDK that runs at the Maintainer's pack time. Never reaches the Consumer.

### Consumer-side concepts

**Agent Token**:
A value in the Consumer Flag identifying a target agent, using the `--agent` ids from the [vercel-labs/skills](https://github.com/vercel-labs/skills#supported-agents) table (e.g. `claude-code`, `opencode`, `universal`).

**Consumer Flag**:
The per-package MSBuild property a Consumer sets to opt in; value is a semicolon list of Agent Tokens. Default name: `<PackageIdNoDots>AgentSkills`.
_Avoid_: opt-in switch, docs flag

**Workspace Root**:
The directory under which `.agents/` / `.claude/` are created. Resolved by the chain: `AgentSkillsRoot` property → `$(SolutionDir)` → `.git` walk-up → project directory.

**Skill Name**:
The computed identity `use-<sanitized-packageid>-v<sanitized-semver>`; also the directory name. Immutable per package version.

**Version Stamp**:
Marker file written last inside a synced Skill directory; its presence means sync completed, letting later builds no-op.
