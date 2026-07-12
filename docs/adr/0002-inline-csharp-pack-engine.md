# Pack engine is an inline C# task; consumer side stays pure MSBuild XML

Pack-time work (name sanitization, YAML frontmatter synthesis, body escaping, consumer-targets generation) is implemented as an inline C# task via `RoslynCodeTaskFactory` inside the SDK's `.targets`. The generated Consumer Targets remain plain declarative MSBuild (`Copy`, `WriteLinesToFile`, property conditions).

Pure-MSBuild string composition was rejected after analysis: `WriteLinesToFile` splits on `;`, `%XX` sequences unescape, two-stage `$()`-escaping (`%24`) makes templates write-only, and the logic cannot be unit-tested. A compiled task DLL was rejected because shipping binaries in the SDK weakens the "lightweight, no custom DLLs" positioning versus Zakira.Imprint — even though it would never reach consumers. The inline task keeps zero shipped binaries while removing every string-escaping hazard: the consumer targets file is written from C# with `File.WriteAllText`, so literal `$(...)` needs no escaping tricks.

Consequence: pack requires an MSBuild with `RoslynCodeTaskFactory` (any .NET SDK build; given, since this hooks `dotnet pack`). Cold-build compile cost ~1s at maintainer pack time only. Core logic lives in `.cs` source files compiled by the factory, shared with the unit-test project.
