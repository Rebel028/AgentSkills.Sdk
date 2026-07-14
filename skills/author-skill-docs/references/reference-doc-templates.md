# Reference doc templates

Reference docs are packed under `references/` in the Skill and loaded by the
Consumer's agent on demand. Each doc covers one concern; 1–3 docs total.

## quickstart.md

```markdown
# <PackageId> quickstart

## Install

```shell
dotnet add package <PackageId>
```

## Minimal end-to-end example

<One complete, runnable path from install to first result: setup/registration
(DI if applicable), the primary call, expected output. Grounded in a real
sample or test from the scan.>

## Next steps

<Pointers: which body section or reference doc covers configuration,
advanced scenarios.>
```

## api-overview.md

```markdown
# <PackageId> API overview

<Map of the public surface an agent needs — entry points first, not an
exhaustive dump.>

## Entry points

| Type | Role | Typical first call |
|---|---|---|
| `<Type>` | <one line> | `<method>` |

## <Area, e.g. "Configuration">

<Key types/options with the semantics that are not obvious from signatures:
defaults, valid ranges, ordering constraints, lifetime/disposal rules.>
```

## Optional third doc

Add only when the package has a distinct advanced area that would bloat the
other two — e.g. `configuration.md`, `extensibility.md`, `migration.md`.
Same rules: one concern, scanned-API-grounded snippets, no member-by-member
dumps — for member-level detail the Maintainer can ship the compiler XML docs
verbatim via `AgentSkillIncludeXmlDocs`; rendering them to markdown is
deliberately rejected.
