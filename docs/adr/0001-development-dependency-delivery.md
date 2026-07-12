# Deliver as a DevelopmentDependency NuGet; consumers inherit only a generated targets file

The SDK ships as a NuGet package with `<DevelopmentDependency>true</DevelopmentDependency>`, referenced by maintainers with `PrivateAssets="all"`. It runs only during the maintainer's `dotnet pack`. The consumer inherits nothing from the SDK itself — only a self-contained, package-specific `build/[PackageId].targets` generated at pack time.

Rejected: Zakira.Imprint's model of a shared engine flowing transitively (`buildTransitive/` task DLL) to every consumer. Our differentiator is a zero-weight consumer footprint: no binaries, no transitive packages, plain readable XML on the consumer side.
