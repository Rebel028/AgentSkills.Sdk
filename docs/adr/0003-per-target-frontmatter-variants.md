# Per-target SKILL.md variants reconcile agentskills.io compliance with Claude Code behavior

The agentskills.io spec requires vendor extensions to live under the `metadata:` string map. Claude Code, however, honors its extensions (`user-invocable`, `context`, `allowed-tools`) only as top-level frontmatter keys. One file cannot be both spec-pure and functional in Claude Code.

Decision: the Pack Engine emits one `SKILL.md` Variant per destination family — `claude` (Claude fields top-level) and `agents` (spec-pure, extensions duplicated under `metadata:`). The Skill Payload (`references/`, `assets/`, `scripts/`) is stored once in the nupkg and shared; only `SKILL.md` is duplicated. The Consumer Targets copy the Variant matching each selected Agent Token's destination.

Rejected: top-level keys everywhere (breaks the "strict spec compliance" claim), `metadata:`-only (Claude silently ignores configured behavior).
