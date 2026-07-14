# Skill Body template

The Skill Body is the markdown that becomes the packed SKILL.md content below the frontmatter. It is read by a coding agent working in the Consumer's project, so write for an agent: imperative, concrete, no marketing prose.

**No frontmatter.** Start directly at the `#` heading.

```markdown
# <PackageId>

<One-sentence purpose — what the package does and when a consumer reaches for it.>

## Core usage

<For each top task (2–3), a short heading + minimal working snippet, grounded
in the scanned API. Show the canonical call sequence, not every overload.>

### <Task 1, verb-first, e.g. "Render a template">

```csharp
<smallest realistic snippet>
```

## Pitfalls

<The misuse patterns from the Maintainer interview. Each: what goes wrong,
what to do instead. This is the highest-value section for an agent.>

- **<Pitfall>**: <wrong way> — instead <right way>.

## Reference docs

- `references/quickstart.md` — <one line on what it covers>
- `references/api-overview.md` — <one line>
```

Guidelines:

- Keep the body under ~150 lines; depth goes in the reference docs.
- Every snippet must trace to a scanned signature or usage example; any
  snippet that doesn't (scan declined, or API not covered) carries an
  `<!-- unverified -->` marker.
- Point to reference docs by their `references/<file>` path — that is where the
  SDK packs `AgentSkillReferenceFiles` inside the Skill.
- Do not restate what the agent can infer from the API (parameter lists,
  obvious property names); document intent, ordering, and traps.
