# Workspace Root resolves via: AgentSkillsRoot → SolutionDir → .git walk-up → project dir

Order chosen deliberately: explicit `$(AgentSkillsRoot)` always wins; `$(SolutionDir)` next because it is a cheap early exit for the common IDE/solution build (checked against both `''` and `'*Undefined*'`); then an unrolled `Exists()` walk-up (8 levels) looking for `.git` (directory or file, covering worktrees); finally the project directory plus a build warning.

Recorded consequence of ranking `SolutionDir` above the git walk-up: a monorepo with several `.sln` files in subfolders gets skills placed per-solution, not per-repo. Accepted — each solution is a self-contained workspace — and `AgentSkillsRoot` (one line in a root `Directory.Build.props`) is the documented override for exotic layouts.

Rejected: requiring `AgentSkillsRoot` always (two-step onboarding, friction), and the original `SolutionDir`-else-project-dir design (known broken for `dotnet build` on a bare csproj: scatters `.agents/` into every project folder).
