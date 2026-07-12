# Concurrency handled by version-stamp short-circuit plus Copy retries — no locking

Parallel builds (N projects × M target frameworks) may sync the same skill directory concurrently. Because the Skill Name embeds the full package version, all writers write identical bytes; the risk is transient file locks (mainly Windows), not corruption.

Decision: the sync target no-ops when the Version Stamp file already matches (fast path for every build after the first, and for IDE builds); the first sync uses `Copy` with `SkipUnchangedFiles="true" Retries="3"`, and the stamp is written last so a killed build re-syncs. The whole target is guarded with `'$(DesignTimeBuild)' != 'true'`.

The spec's honest claim: the race window is narrowed to first-ever sync, where the worst case is a retried copy of identical content. Rejected: hand-rolled lock files in MSBuild (no native primitive, abandoned locks after killed builds) and unconditional every-build copies (full tree enumeration on every incremental build of a large monorepo).
