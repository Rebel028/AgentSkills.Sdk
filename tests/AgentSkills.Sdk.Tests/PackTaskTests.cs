using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgentSkills.Sdk.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;

namespace AgentSkills.Sdk.Tests;

/// <summary>Captures error events so AGSK codes can be asserted.</summary>
internal sealed class FakeBuildEngine : IBuildEngine
{
    public List<BuildErrorEventArgs> Errors { get; } = new List<BuildErrorEventArgs>();
    public List<BuildWarningEventArgs> Warnings { get; } = new List<BuildWarningEventArgs>();

    public bool ContinueOnError => false;
    public int LineNumberOfTaskNode => 0;
    public int ColumnNumberOfTaskNode => 0;
    public string ProjectFileOfTaskNode => "test";

    public void LogErrorEvent(BuildErrorEventArgs e)
    {
        Errors.Add(e);
    }

    public void LogWarningEvent(BuildWarningEventArgs e)
    {
        Warnings.Add(e);
    }

    public void LogMessageEvent(BuildMessageEventArgs e)
    {
    }

    public void LogCustomEvent(CustomBuildEventArgs e)
    {
    }

    public bool BuildProjectFile(string projectFileName, string[] targetNames, IDictionary globalProperties, IDictionary targetOutputs)
    {
        throw new NotSupportedException();
    }
}

public sealed class PackTaskTests : IDisposable
{
    private readonly string _workDirectory;
    private readonly FakeBuildEngine _engine;

    public PackTaskTests()
    {
        _workDirectory = Path.Combine(Path.GetTempPath(), "agsk-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDirectory);
        _engine = new FakeBuildEngine();
    }

    public void Dispose()
    {
        Directory.Delete(_workDirectory, recursive: true);
    }

    private AgentSkillsGeneratePackTask NewTask()
    {
        return new AgentSkillsGeneratePackTask
        {
            BuildEngine = _engine,
            PackageId = "FixtureLib",
            PackageVersion = "1.0.0",
            StagingDirectory = Path.Combine(_workDirectory, "staging"),
            ProjectDirectory = _workDirectory,
            ConsumerFlagName = "FixtureLibAgentSkills",
        };
    }

    private string ErrorCodes()
    {
        return string.Join(",", _engine.Errors.Select(e => e.Code));
    }

    [Fact]
    public void Both_full_and_body_file_fail_with_AGSK006()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.BodyFile = "body.md";
        task.FullFile = "SKILL.md";

        Assert.False(task.Execute());
        Assert.Equal("AGSK006", ErrorCodes());
    }

    [Fact]
    public void Missing_body_file_fails_with_AGSK003()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.BodyFile = "does-not-exist.md";

        Assert.False(task.Execute());
        Assert.Equal("AGSK003", ErrorCodes());
    }

    [Fact]
    public void Missing_full_file_fails_with_AGSK005()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.FullFile = "does-not-exist.md";

        Assert.False(task.Execute());
        Assert.Equal("AGSK005", ErrorCodes());
    }

    [Fact]
    public void Name_overflow_fails_with_AGSK001()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.PackageId = new string('a', 60);

        Assert.False(task.Execute());
        Assert.Equal("AGSK001", ErrorCodes());
    }

    [Fact]
    public void Invalid_name_override_fails_with_AGSK002()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.NameOverride = "Not--Valid";

        Assert.False(task.Execute());
        Assert.Equal("AGSK002", ErrorCodes());
    }

    [Fact]
    public void Oversized_description_fails_with_AGSK004()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.DescriptionOverride = new string('d', 1025);

        Assert.False(task.Execute());
        Assert.Equal("AGSK004", ErrorCodes());
    }

    [Fact]
    public void Scaffold_run_stages_variants_and_targets()
    {
        AgentSkillsGeneratePackTask task = NewTask();
        task.PackageDescription = "A fixture.";

        Assert.True(task.Execute(), ErrorCodes());

        string claude = File.ReadAllText(Path.Combine(task.StagingDirectory, "SKILL.claude.md"));
        Assert.StartsWith("---\nname: use-fixturelib-v1-0-0\n", claude);
        Assert.Contains("# FixtureLib guide", claude);

        string[] packagePaths = task.PackageFiles.Select(f => f.GetMetadata("PackagePath")).ToArray();
        Assert.Contains("agent-assets/SKILL.claude.md", packagePaths);
        Assert.Contains("agent-assets/SKILL.agents.md", packagePaths);
        Assert.Contains("build/FixtureLib.targets", packagePaths);
    }

    [Fact]
    public void Full_file_passes_through_to_both_variants()
    {
        string fullPath = Path.Combine(_workDirectory, "SKILL.md");
        File.WriteAllText(fullPath, "---\nname: custom\n---\nAs-is body.\n");
        AgentSkillsGeneratePackTask task = NewTask();
        task.FullFile = "SKILL.md";

        Assert.True(task.Execute(), ErrorCodes());

        string claude = File.ReadAllText(Path.Combine(task.StagingDirectory, "SKILL.claude.md"));
        string agents = File.ReadAllText(Path.Combine(task.StagingDirectory, "SKILL.agents.md"));
        Assert.Equal("---\nname: custom\n---\nAs-is body.\n", claude);
        Assert.Equal(claude, agents);
    }

    [Fact]
    public void Payload_preserves_recursive_dir_and_buckets()
    {
        string docPath = Path.Combine(_workDirectory, "api.md");
        File.WriteAllText(docPath, "# api");
        TaskItem reference = new TaskItem(docPath);
        reference.SetMetadata("RecursiveDir", "guides\\nested\\");

        AgentSkillsGeneratePackTask task = NewTask();
        task.ReferenceFiles = new ITaskItem[] { reference };

        Assert.True(task.Execute(), ErrorCodes());
        string[] packagePaths = task.PackageFiles.Select(f => f.GetMetadata("PackagePath")).ToArray();
        Assert.Contains("agent-assets/payload/references/guides/nested/api.md", packagePaths);
    }

    [Fact]
    public void Readme_joins_references_unless_path_taken()
    {
        string readmePath = Path.Combine(_workDirectory, "README.md");
        File.WriteAllText(readmePath, "# readme");

        AgentSkillsGeneratePackTask withReadme = NewTask();
        withReadme.ReadmeFile = "README.md";
        Assert.True(withReadme.Execute(), ErrorCodes());
        Assert.Contains("agent-assets/payload/references/README.md",
            withReadme.PackageFiles.Select(f => f.GetMetadata("PackagePath")));

        // A maintainer-supplied references/README.md wins over PackageReadmeFile.
        string ownReadme = Path.Combine(_workDirectory, "own-README.md");
        File.WriteAllText(ownReadme, "# mine");
        TaskItem reference = new TaskItem(ownReadme);
        // Rename target: Filename+Extension come from the source; use a file
        // literally named README.md in a different directory instead.
        string docsDirectory = Path.Combine(_workDirectory, "docs");
        Directory.CreateDirectory(docsDirectory);
        string docsReadme = Path.Combine(docsDirectory, "README.md");
        File.WriteAllText(docsReadme, "# mine");
        TaskItem docsItem = new TaskItem(docsReadme);

        AgentSkillsGeneratePackTask deduped = NewTask();
        deduped.StagingDirectory = Path.Combine(_workDirectory, "staging2");
        deduped.ReadmeFile = "README.md";
        deduped.ReferenceFiles = new ITaskItem[] { docsItem };
        Assert.True(deduped.Execute(), ErrorCodes());

        ITaskItem[] readmeEntries = deduped.PackageFiles
            .Where(f => f.GetMetadata("PackagePath") == "agent-assets/payload/references/README.md")
            .ToArray();
        Assert.Single(readmeEntries);
        Assert.Equal(docsReadme, readmeEntries[0].ItemSpec);
    }
}
