using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using AgentSkills.Sdk.Tasks;
using Xunit;

namespace AgentSkills.Sdk.Tests;

public sealed class ConsumerTargetsGeneratorTests
{
    private static string Render(
        string packageId = "MyAwesome.Engine",
        string packageVersion = "2.4.1-preview.3",
        string skillName = "use-myawesome-engine-v2-4-1-preview-3",
        string flag = "MyAwesomeEngineAgentSkills")
    {
        return ConsumerTargetsGenerator.Render(packageId, packageVersion, skillName, flag);
    }

    [Theory]
    [InlineData("MyAwesome.Engine", "2.4.1-preview.3")]
    [InlineData("Simple", "1.0.0")]
    [InlineData("Weird--Id..Case", "10.20.30-rc.1+meta")]
    public void Generated_xml_is_well_formed(string packageId, string packageVersion)
    {
        string targets = ConsumerTargetsGenerator.Render(packageId, packageVersion, "use-x-v1", "XFlag");
        XDocument document = XDocument.Parse(targets);
        Assert.Equal("Project", document.Root!.Name.LocalName);
    }

    [Fact]
    public void Consumer_time_properties_survive_as_literal_text()
    {
        string targets = Render();
        Assert.Contains("$(SolutionDir)", targets);
        Assert.Contains("$(MSBuildProjectDirectory)", targets);
        Assert.Contains("$(MSBuildThisFileDirectory)", targets);
        Assert.Contains("$(DesignTimeBuild)", targets);
        // The flag *name* is baked; its value is a consumer-time literal reference.
        Assert.Contains("'$(MyAwesomeEngineAgentSkills)' != ''", targets);
    }

    [Fact]
    public void Name_tag_is_collision_free_and_property_safe()
    {
        string targets = Render();
        Assert.Contains("AgentSkillsSync_myawesome_engine_2_4_1_preview_3", targets);
        // MSBuild property names cannot contain hyphens or dots.
        Assert.DoesNotContain("_AgsRoot_myawesome-engine", targets);

        XDocument document = XDocument.Parse(targets);
        XElement target = document.Root!.Elements().Single(e => e.Name.LocalName == "Target");
        Assert.Equal("BeforeBuild", target.Attribute("BeforeTargets")!.Value);
    }

    [Fact]
    public void Walk_up_has_eight_levels_and_git_file_support_via_exists()
    {
        string targets = Render();
        int levels = targets.Split(new[] { ".git')" }, StringSplitOptions.None).Length - 1;
        Assert.Equal(8, levels);
        Assert.Contains("$(MSBuildProjectDirectory)/../../../../../../../.git", targets);
    }

    [Fact]
    public void Token_map_covers_adr_0005()
    {
        string targets = Render();
        foreach (string token in new[]
        {
            "universal", "opencode", "codex", "cursor", "gemini-cli",
            "github-copilot", "amp", "cline", "zed", "warp",
        })
        {
            Assert.Contains($"AnyHaveMetadataValue('Identity', '{token}')", targets);
        }
        Assert.Contains(".agents/skills", targets);
        Assert.Contains(".claude/skills", targets);
        Assert.Contains("claude-code", targets);
        Assert.Contains("AGSK101", targets);
        Assert.Contains("AGSK102", targets);
    }

    [Fact]
    public void Stamp_is_written_last_and_short_circuits()
    {
        string targets = Render();
        XDocument document = XDocument.Parse(targets);
        XElement target = document.Root!.Elements().Single(e => e.Name.LocalName == "Target");
        XElement[] tasks = target.Elements().ToArray();

        // Last task in the target copies the stamp; every sync item is guarded on it.
        XElement lastTask = tasks.Last();
        Assert.Equal("Copy", lastTask.Name.LocalName);
        Assert.Contains(".agentskills-stamp", lastTask.Attribute("DestinationFiles")!.Value);
        Assert.Contains("_AgsStamp_", lastTask.Attribute("SourceFiles")!.Value);

        foreach (XElement copy in tasks.Where(t => t.Name.LocalName == "Copy"))
        {
            // Copies only run when the batched includes (stamp-guarded per
            // destination) produced work, and never with zero destinations.
            Assert.Contains("_AgsDest_", copy.Attribute("Condition")!.Value);
            Assert.Equal("3", copy.Attribute("Retries")!.Value);
            Assert.Equal("200", copy.Attribute("RetryDelayMilliseconds")!.Value);
            Assert.Equal("true", copy.Attribute("SkipUnchangedFiles")!.Value);
        }

        // The per-destination stamp short-circuit lives on the batched includes.
        XElement syncItems = tasks.Last(t => t.Name.LocalName == "ItemGroup");
        foreach (XElement include in syncItems.Elements().Where(e => e.Attribute("AgsDest") != null))
        {
            Assert.Contains("!Exists(", include.Attribute("Condition")!.Value);
            Assert.Contains(".agentskills-stamp", include.Attribute("Condition")!.Value);
        }
    }

    [Fact]
    public void Custom_flag_name_is_respected()
    {
        string targets = Render(flag: "MyEngineSkills");
        Assert.Contains("'$(MyEngineSkills)' != ''", targets);
        Assert.DoesNotContain("MyAwesomeEngineAgentSkills", targets);
    }

    [Fact]
    public void Fixture_output_matches_approved_snapshot()
    {
        string actual = ConsumerTargetsGenerator.Render(
            "FixtureLib", "1.0.0", "use-fixturelib-v1-0-0", "FixtureLibAgentSkills");

        string approvedPath = Path.Combine(SnapshotDirectory(), "FixtureLib.targets.approved");
        if (!File.Exists(approvedPath))
        {
            File.WriteAllText(approvedPath + ".received", actual);
            Assert.Fail($"Approved snapshot missing; wrote {approvedPath}.received for review.");
        }

        string approved = File.ReadAllText(approvedPath).Replace("\r\n", "\n");
        if (approved != actual)
        {
            File.WriteAllText(approvedPath + ".received", actual);
        }
        Assert.Equal(approved, actual);
    }

    private static string SnapshotDirectory()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "AgentSkills.Sdk.sln")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "tests", "AgentSkills.Sdk.Tests", "snapshots");
    }
}
