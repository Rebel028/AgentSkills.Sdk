# Name overflow is a hard pack error, not silent truncation

The sanitize pipeline (lowercase → strip semver build metadata → collapse non-`[a-z0-9]` runs to single hyphens → trim/collapse hyphens) can still yield a Skill Name over the spec's 64-char limit for long PackageIds with prerelease tags. In that case `dotnet pack` fails with an error instructing the maintainer to set `AgentSkillNameOverride`. The override itself is validated against the same rules and errors if non-compliant — never silently rewritten.

Rejected: hash-suffix truncation (opaque names, magic) and sanitizing the override (maintainer's explicit choice mutating without notice).
