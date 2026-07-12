# Skill names carry the full semantic version; stale version directories are not cleaned up

Skill Name = `use-<sanitized-packageid>-v<sanitized-semver>` (full version incl. prerelease, build metadata stripped). This lets projects in one monorepo referencing different versions of the same package coexist without write collisions, and makes each skill directory's content immutable.

Deliberate consequence: after an upgrade, the previous version's directory remains on disk until manually deleted. No cleanup manifest, no sibling-directory deletion — any automatic cleanup requires cross-project knowledge a single project's build does not have, and naive deletion would make v1/v2 projects thrash each other's directories every build (the exact scenario versioned names exist to support). Directories are `.gitignore`d, so the repository history is never polluted; docs state that deleting `.agents/skills/use-<pkg>-*` is always safe (current version regenerates on next build).

A central manifest with orphan removal (Zakira.Imprint's `.imprint/manifest.json` approach) is recorded as a v2 candidate; it demands cross-build locking and JSON handling that the pure-XML consumer side deliberately avoids.
