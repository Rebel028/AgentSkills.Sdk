---
name: author-skill-docs
description: Author or draft the skill docs a NuGet package feeds into AgentSkills.Sdk — a Skill Body plus reference docs. Use when a Maintainer asks to author, draft, or write agent skill docs for their package, or when chained from the setup-agentskills-sdk skill because the repo has no markdown docs to wire up.
---

# Author Skill Docs

Drafts the documentation a Maintainer feeds into AgentSkills.Sdk: one **Skill Body** (pure markdown, consumed via `AgentSkillBodyFile`) and 1–3 **reference docs** (consumed via `AgentSkillReferenceFiles`).

## Hard rules

- **Output is doc files only.** Never modify source code or the `.csproj` — in standalone mode you print the wiring snippet, you do not apply it.
- **Skill Body contains NO YAML frontmatter block.** The SDK synthesizes `name`/`description` frontmatter at pack time. The body starts directly at its `#` heading.
- **Never generate or edit XML doc comments in source files.** Read XML doc comments as input; never write them. The SDK can ship the compiler's XML doc file verbatim (`AgentSkillIncludeXmlDocs`) — rendering it to markdown stays rejected as an API dump. At most, tip the Maintainer that richer XML doc comments improve both future drafts and the shipped `references/api-docs.xml`.
- **Do not scan the codebase without consent** (step 3). A prior instruction like "scan the code" counts as consent — record it — but the scan-tool choice still gets confirmed before scanning.
- **Do not write a SKILL.md for the Maintainer's package.** The SDK generates it; your output is body + references only.

If the Maintainer signals urgency, batch the questions from steps 1–3 into a single message; do not skip any of them.

## Steps

### 1. Determine mode and output location

- **Chained**: invoked from the setup-agentskills-sdk skill (the conversation is already onboarding a project onto AgentSkills.Sdk). Skip questions setup already answered (project, package id).
- **Standalone**: invoked by name or by a doc-drafting request. Identify which project/package the docs are for.

Ask where generated docs should land; offer `docs/` as the default. An indifferent answer ("wherever", no preference) adopts the default.

Done when: mode known, target project known, output directory settled (explicit choice or default adopted).

### 2. Interview the Maintainer

Ask, in one batch:

1. What problem does the package solve, in one sentence?
2. What are the 2–3 tasks a consumer most often does with it?
3. What do users most often get wrong (pitfalls, misuse, common support questions)?
4. Any surface to de-emphasize (obsolete, internal-ish, experimental)?

If the Maintainer defers to the code scan, source the pitfalls from the scan instead: test edge cases, guard clauses that throw on misuse, README caveats, and open issues if a tracker is reachable.

Done when: purpose, top tasks, and pitfalls are captured, each with a named source (interview or scan artifact).

### 3. Get scan consent and pick a scan tool

Ask permission to scan the codebase to ground the draft in real API and usage (skip the ask if consent was already given — see Hard rules). If declined, draft from the interview alone and mark every unscanned snippet with `<!-- unverified -->`.

If granted, pick the **most token-efficient tool that yields public signatures and XML doc comments**, and confirm the choice with the Maintainer before scanning. Candidates, in typically-cheapest-first order — but the criterion wins over the order (for a small codebase, Grep/Read beats authoring a reflection script):

1. A code-index MCP server already connected to the session
2. LSP (document symbols / hover for signatures and XML doc comments)
3. An existing CLI that dumps API surface (run via Docker if repo rules require)
4. Plain Grep/Read over source files (always available)

Done when: consent recorded (yes/no) and, if yes, tool choice confirmed by the Maintainer.

### 4. Scan

Two passes, cheapest-first:

1. **Public API surface**: public types and members with signatures; harvest existing XML doc comments (`<summary>`, `<param>`, `<example>`) as draft material.
2. **Real usage patterns**: mine README, samples/, and test projects for how the API is actually called — tests show canonical call sequences and setup.

Done when: you can name the package's main entry-point types and show at least one real, compiling-looking usage snippet per top task from step 2.

### 5. Draft the docs

Write to the confirmed output directory:

- **Skill Body** (`<output>/skill-body.md`): pure markdown, no frontmatter. Follow [references/body-template.md](references/body-template.md).
- **1–3 reference docs** (e.g. `<output>/quickstart.md`, `<output>/api-overview.md`): follow [references/reference-doc-templates.md](references/reference-doc-templates.md). Only add a third doc if the package has a distinct advanced area (configuration, extensibility).

Ground every code snippet in the scan; a snippet not traceable to a scanned signature or usage example carries `<!-- unverified -->`. Prefer the pitfalls from step 2 — they are the highest-value content for an agent.

Done when: files exist on disk, body has zero frontmatter, each snippet traces to scanned API or is marked `<!-- unverified -->`.

### 6. Review and hand off

Show the Maintainer the file list and a short summary of each; apply requested edits.

Then, by mode:

- **Chained**: hand back the produced file paths (body path + reference doc paths) to the setup-agentskills-sdk flow so it can wire `AgentSkillBodyFile` and `AgentSkillReferenceFiles` into the `.csproj`. Do not edit the `.csproj` yourself.
- **Standalone**: print the `.csproj` wiring snippet for the Maintainer to apply (or suggest running the setup-agentskills-sdk skill). The paths below are **examples only** — use the real paths of the files you produced:

```xml
<PropertyGroup>
  <!-- EXAMPLE paths — replace with the produced files' real locations -->
  <AgentSkillBodyFile>$(MSBuildProjectDirectory)/../docs/skill-body.md</AgentSkillBodyFile>
</PropertyGroup>
<ItemGroup>
  <AgentSkillReferenceFiles Include="$(MSBuildProjectDirectory)/../docs/quickstart.md;$(MSBuildProjectDirectory)/../docs/api-overview.md" />
</ItemGroup>
```

Done when: Maintainer accepted the docs and (chained) paths were handed back, or (standalone) the wiring snippet was shown.

## Common mistakes

| Mistake | Fix |
|---|---|
| Body starts with `---` frontmatter | Delete it — the SDK owns frontmatter |
| Writing/editing XML doc comments "while you're in there" | Never. Docs output only; shipping XML docs is the SDK's job (`AgentSkillIncludeXmlDocs`) |
| Scanning before asking | Consent first, tool confirmation second, scan third |
| Referencing docs by content instead of pointer in the body | Body should point to `references/<file>` names — the SDK packs reference docs under `references/` in the Skill |
| Documenting every public member | Cover the top tasks and pitfalls; an API dump is noise to an agent |
