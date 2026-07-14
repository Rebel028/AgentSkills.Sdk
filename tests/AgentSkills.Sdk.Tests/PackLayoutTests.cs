using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Xunit;

namespace AgentSkills.Sdk.Tests;

/// <summary>
/// Phase 3 exit criterion (docs/plan.md): packing the fixture produces a nupkg
/// with the exact spec.md §4 layout. Runs real dotnet pack, so it needs the
/// dotnet CLI on PATH (the Docker test container qualifies).
/// </summary>
public sealed class PackLayoutTests
{
    private static string RepoRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AgentSkills.Sdk.sln")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }

    private static string RunDotnet(string arguments, string workingDirectory, string packagesDirectory)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["NUGET_PACKAGES"] = packagesDirectory;
        using Process process = Process.Start(start)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"dotnet {arguments} failed:\n{output}");
        return output;
    }

    [Fact]
    public void Fixture_nupkg_matches_spec_section4_layout()
    {
        string repoRoot = RepoRoot();
        string workDirectory = Path.Combine(Path.GetTempPath(), "agsk-layout-" + Guid.NewGuid().ToString("N"));
        string feedDirectory = Path.Combine(repoRoot, "artifacts", "packages");
        try
        {
            string nugetCache = Path.Combine(workDirectory, "nuget-cache");
            string output = Path.Combine(workDirectory, "out");
            Directory.CreateDirectory(nugetCache);

            RunDotnet(
                $"pack src/AgentSkills.Sdk/AgentSkills.Sdk.csproj -c Release -o \"{feedDirectory}\"",
                repoRoot, nugetCache);
            string packOutput = RunDotnet(
                $"pack tests/fixtures/FixtureLib/FixtureLib.csproj -c Release -o \"{output}\"",
                repoRoot, nugetCache);
            // Auto-enabled XML doc generation must not surface missing-comment
            // warnings (ADR-0012); Widget.GreetCount is the undocumented canary.
            Assert.DoesNotContain("CS1591", packOutput);

            using ZipArchive nupkg = ZipFile.OpenRead(Path.Combine(output, "FixtureLib.1.0.0.nupkg"));
            string[] skillEntries = nupkg.Entries
                .Select(e => e.FullName)
                .Where(n => n.StartsWith("agent-assets/") || n.StartsWith("build/"))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();

            string[] expected =
            {
                "agent-assets/SKILL.agents.md",
                "agent-assets/SKILL.claude.md",
                "agent-assets/payload/assets/sample-config.json",
                "agent-assets/payload/references/api-docs-guide.md",
                "agent-assets/payload/references/api-docs.xml",
                "agent-assets/payload/references/api.md",
                "agent-assets/payload/references/guides/quickstart.md",
                "agent-assets/payload/scripts/check.sh",
                "agent-assets/skill.gitignore",
                "agent-assets/skill.stamp",
                "build/FixtureLib.targets",
            };
            Assert.Equal(expected, skillEntries);

            ZipArchiveEntry claudeEntry = nupkg.GetEntry("agent-assets/SKILL.claude.md")!;
            using StreamReader reader = new StreamReader(claudeEntry.Open());
            string claude = reader.ReadToEnd();
            Assert.StartsWith(
                "---\n" +
                "name: use-fixturelib-v1-0-0\n" +
                "description: 'Fixture rules for FixtureLib APIs.'\n" +
                "user-invocable: true\n" +
                "context: fork\n" +
                "---\n",
                claude);
            Assert.Contains("# FixtureLib usage", claude);
        }
        finally
        {
            if (Directory.Exists(workDirectory))
            {
                Directory.Delete(workDirectory, recursive: true);
            }
        }
    }
}
