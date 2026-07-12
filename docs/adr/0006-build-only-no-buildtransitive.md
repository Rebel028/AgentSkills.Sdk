# Consumer Targets ship in `build/` only — no `buildTransitive/`

Skills sync only for projects that reference the maintainer's package directly. Transitive-only consumers get nothing; they do not code against the package's API directly, so its skill is noise for them. In a monorepo the direct referencer still syncs the skill once to the Workspace Root, where every project's agent can see it.

Rejected: `buildTransitive/` (Zakira.Imprint parity) — it would evaluate our generated target in every downstream project of every consumer, widening the execution surface and the supply-chain optics for marginal benefit. Revisit only with concrete demand.
