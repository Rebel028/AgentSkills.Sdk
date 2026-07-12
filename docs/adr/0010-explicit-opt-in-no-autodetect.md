# Consumer participation is explicit opt-in; no filesystem auto-detection

Nothing is written to a consumer's workspace unless they set the Consumer Flag in their own project (or `Directory.Build.props`). The generated target evaluates to a no-op otherwise.

Rejected: Zakira.Imprint-style zero-config auto-detection (scan for `.claude/`, `.github/` and deploy silently). A NuGet package writing files into a repository because a directory happened to exist is exactly the behavior supply-chain reviewers flag; the opt-in property is the mitigation that makes "a package runs a copy script during your build" defensible. This is a security-posture decision: default-off, consumer-controlled, per-package.
