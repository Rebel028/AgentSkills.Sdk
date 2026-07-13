using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace AgentSkills.Sdk.IntegrationTests;

/// <summary>
/// Packs the SDK and FixtureLib (v1.0.0 and v2.0.0) once into a local feed;
/// every matrix case consumes from it. One shared NuGet cache keeps restores
/// offline and fast.
/// </summary>
public sealed class PackedFeedFixture : IDisposable
{
    public string RepoRoot { get; }
    public string WorkRoot { get; }
    public string FeedDirectory { get; }
    public string NugetCache { get; }

    public PackedFeedFixture()
    {
        RepoRoot = FindRepoRoot();
        WorkRoot = Path.Combine(Path.GetTempPath(), "agsk-matrix-" + Guid.NewGuid().ToString("N"));
        FeedDirectory = Path.Combine(WorkRoot, "feed");
        NugetCache = Path.Combine(WorkRoot, "nuget-cache");
        Directory.CreateDirectory(FeedDirectory);
        Directory.CreateDirectory(NugetCache);

        Run($"pack src/AgentSkills.Sdk/AgentSkills.Sdk.csproj -c Release -o \"{FeedDirectory}\"", RepoRoot);
        string fixtureProject = "tests/fixtures/FixtureLib/FixtureLib.csproj";
        string restoreToFeed = $"-p:RestoreSources=\"{FeedDirectory}\"";
        Run($"pack {fixtureProject} -c Release -o \"{FeedDirectory}\" {restoreToFeed}", RepoRoot);
        Run($"pack {fixtureProject} -c Release -o \"{FeedDirectory}\" {restoreToFeed} -p:Version=2.0.0", RepoRoot);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(WorkRoot, recursive: true);
        }
        catch (IOException)
        {
            // Temp cleanup only; never fail the run for it.
        }
    }

    /// <summary>Runs dotnet with the shared cache; throws on non-zero exit.
    /// Crashes and hangs (SIGILL/SIGSEGV under emulated CI, stuck child) get
    /// one retry — they are environment noise, not product behavior.</summary>
    public string Run(string arguments, string workingDirectory)
    {
        string output = RunAllowFailure(arguments, workingDirectory, out int exitCode);
        for (int retry = 0; retry < 2 && IsEnvironmentCrash(exitCode, output); retry++)
        {
            output = RunAllowFailure(arguments, workingDirectory, out exitCode);
        }
        Assert.True(exitCode == 0, $"dotnet {arguments} exited {exitCode}:\n{output}");
        return output;
    }

    private static bool IsEnvironmentCrash(int exitCode, string output)
    {
        if (exitCode == 0)
        {
            return false;
        }
        // 132/139 = SIGILL/SIGSEGV, -1 = hang-kill; MSB4166 = a worker node
        // died the same way mid -m build.
        return exitCode == 132 || exitCode == 139 || exitCode == -1
            || output.Contains("MSB4166");
    }

    /// <summary>Runs dotnet, returning combined output and the exit code
    /// (-1 when the process was killed after the 2-minute timeout).</summary>
    public string RunAllowFailure(string arguments, string workingDirectory, out int exitCode)
    {
        ProcessStartInfo start = new ProcessStartInfo("dotnet", arguments)
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.Environment["NUGET_PACKAGES"] = NugetCache;
        // Keep consumer-side evaluation clean of any outer MSBuild state.
        start.Environment.Remove("MSBuildSDKsPath");
        // Avoids sporadic SIGILL from the JIT under emulated/virtualized ARM hosts.
        start.Environment["DOTNET_EnableWriteXorExecute"] = "0";
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";

        using Process process = Process.Start(start)!;
        System.Threading.Tasks.Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        System.Threading.Tasks.Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            exitCode = -1;
            return "TIMED OUT after 120s\n" + stdout.Result + stderr.Result;
        }
        process.WaitForExit(); // flush async output
        exitCode = process.ExitCode;
        return stdout.Result + stderr.Result;
    }

    /// <summary>Creates a consumer project dir with a local-feed NuGet.config.</summary>
    public string CreateConsumer(
        string name,
        string? flagValue,
        string packageVersion = "1.0.0",
        bool withGitDirectory = true,
        string? parentDirectory = null)
    {
        string directory = Path.Combine(parentDirectory ?? WorkRoot, name);
        Directory.CreateDirectory(directory);
        if (withGitDirectory)
        {
            Directory.CreateDirectory(Path.Combine(directory, ".git"));
        }
        WriteNugetConfig(directory);
        WriteConsumerProject(Path.Combine(directory, name + ".csproj"), flagValue, packageVersion);
        return directory;
    }

    public void WriteNugetConfig(string directory)
    {
        File.WriteAllText(Path.Combine(directory, "NuGet.config"),
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<configuration>\n  <packageSources>\n    <clear />\n"
            + $"    <add key=\"local\" value=\"{FeedDirectory}\" />\n  </packageSources>\n</configuration>\n");
    }

    public void WriteConsumerProject(string projectPath, string? flagValue, string packageVersion = "1.0.0")
    {
        StringBuilder project = new StringBuilder();
        project.Append("<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n");
        project.Append("    <TargetFramework>net10.0</TargetFramework>\n");
        if (flagValue != null)
        {
            project.Append("    <FixtureLibAgentSkills>").Append(flagValue).Append("</FixtureLibAgentSkills>\n");
        }
        project.Append("  </PropertyGroup>\n  <ItemGroup>\n");
        project.Append("    <PackageReference Include=\"FixtureLib\" Version=\"").Append(packageVersion).Append("\" />\n");
        project.Append("  </ItemGroup>\n</Project>\n");
        File.WriteAllText(projectPath, project.ToString());
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "docs", "spec.md")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
