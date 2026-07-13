# CLAUDE.md

## Project

`AgentSkills.Sdk` — NuGet SDK that ships **agent skills** (SKILL.md, manifests) from a library package into the consumer's project on build, via MSBuild `.targets`.

Consumer opts in declaratively in their `.csproj` via the per-package Consumer Flag (e.g. `<MyAwesomeEngineAgentSkills>claude-code;universal</MyAwesomeEngineAgentSkills>`); the packed `build/[PackageId].targets` copies skill files to the right per-agent directory (`.claude/skills/`, `.agents/skills/`, …). No wizard, no `.csproj` mutation, no `dotnet tool`.

Spec: `docs/spec.md`. Plan: `docs/plan.md`. Decisions: `docs/adr/`. Glossary: `CONTEXT.md`.

## Rules

- **Discover skills first.** Check available skills before acting. If one applies, use it.
- **Be brief.** No preamble, no filler, no recap of what you just did.
- **Ask when unsure.** Something looks wrong, ambiguous, or contradicts these rules — stop and ask. Do not guess.

## Git

- **Never commit to `master`.** Branch first.
- **Never add co-author trailers.** No `Co-Authored-By`, no "Generated with" lines. Commit message is the user's alone.
- **Never rename branches** unless explicitly asked.

## C#

- **Never use `var`.** Always write the explicit type. `List<string> items = new();` not `var items = ...`.
- Follow standard .NET conventions: nullable enabled, `async`/`await` all the way, `IDisposable`/`IAsyncDisposable` where owned, no `.Result`/`.Wait()`.
- Public API gets XML doc comments — the docs are the product here.

## Build & Test

Sandbox has **no .NET SDK**. Use Docker for every build, test, and pack:

```bash
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test
docker run --rm -v "$PWD":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet pack -c Release
```

Never claim a build or test passed without running it.
