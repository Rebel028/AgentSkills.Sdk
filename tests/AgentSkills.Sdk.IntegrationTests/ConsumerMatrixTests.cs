using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AgentSkills.Sdk.IntegrationTests;

/// <summary>
/// Phase 5 integration matrix (docs/plan.md). One class so the cases run
/// sequentially against the shared packed feed.
/// </summary>
public sealed class ConsumerMatrixTests : IClassFixture<PackedFeedFixture>
{
    private const string SkillNameV1 = "use-fixturelib-v1-0-0";
    private const string SkillNameV2 = "use-fixturelib-v2-0-0";

    private readonly PackedFeedFixture _feed;

    public ConsumerMatrixTests(PackedFeedFixture feed)
    {
        _feed = feed;
    }

    private static string[] SyncedFiles(string skillDirectory)
    {
        return Directory.GetFiles(skillDirectory, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(skillDirectory, p).Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();
    }

    private static readonly string[] CompleteSkill =
    {
        ".agentskills-stamp",
        ".gitignore",
        "SKILL.md",
        "assets/sample-config.json",
        "references/api-docs-guide.md",
        "references/api-docs.xml",
        "references/api.md",
        "references/guides/quickstart.md",
        "scripts/check.sh",
    };

    [Fact]
    public void Case1_flag_unset_writes_nothing()
    {
        string consumer = _feed.CreateConsumer("case1", flagValue: null);
        _feed.Run("build", consumer);

        Assert.False(Directory.Exists(Path.Combine(consumer, ".agents")));
        Assert.False(Directory.Exists(Path.Combine(consumer, ".claude")));
        Assert.Empty(Directory.GetDirectories(consumer, "use-*", SearchOption.AllDirectories));
    }

    [Fact]
    public void Case2_universal_syncs_complete_skill()
    {
        string consumer = _feed.CreateConsumer("case2", "universal");
        _feed.Run("build", consumer);

        string skillDirectory = Path.Combine(consumer, ".agents", "skills", SkillNameV1);
        Assert.Equal(CompleteSkill, SyncedFiles(skillDirectory));
        Assert.Equal("1.0.0", File.ReadAllLines(Path.Combine(skillDirectory, ".agentskills-stamp"))[0]);
        Assert.Equal("*", File.ReadAllLines(Path.Combine(skillDirectory, ".gitignore"))[0]);
        Assert.False(Directory.Exists(Path.Combine(consumer, ".claude")));
    }

    [Fact]
    public void Case3_claude_and_universal_get_matching_variants()
    {
        string consumer = _feed.CreateConsumer("case3", "claude-code;universal");
        _feed.Run("build", consumer);

        string claudeSkill = File.ReadAllText(
            Path.Combine(consumer, ".claude", "skills", SkillNameV1, "SKILL.md"));
        string agentsSkill = File.ReadAllText(
            Path.Combine(consumer, ".agents", "skills", SkillNameV1, "SKILL.md"));

        Assert.Contains("user-invocable: true", claudeSkill);
        Assert.Contains("context: fork", claudeSkill);
        Assert.DoesNotContain("metadata:", claudeSkill);

        Assert.Contains("metadata:", agentsSkill);
        Assert.Contains("package-id: FixtureLib", agentsSkill);
        Assert.DoesNotContain("\nuser-invocable: true", agentsSkill);

        Assert.Equal(CompleteSkill, SyncedFiles(Path.Combine(consumer, ".claude", "skills", SkillNameV1)));
        Assert.Equal(CompleteSkill, SyncedFiles(Path.Combine(consumer, ".agents", "skills", SkillNameV1)));
    }

    [Fact]
    public void Case4_unknown_token_warns_and_builds()
    {
        string consumer = _feed.CreateConsumer("case4", "bogus-agent");
        string output = _feed.Run("build", consumer);

        Assert.Contains("AGSK101", output);
        Assert.Contains("bogus-agent", output);
        Assert.False(Directory.Exists(Path.Combine(consumer, ".agents")));
        Assert.False(Directory.Exists(Path.Combine(consumer, ".claude")));
    }

    [Fact]
    public void Case5_monorepo_parallel_builds_stay_clean_over_20_runs()
    {
        string root = Path.Combine(_feed.WorkRoot, "case5");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        _feed.WriteNugetConfig(root);

        string aggregatorItems = "";
        for (int index = 1; index <= 6; index++)
        {
            string projectDirectory = Path.Combine(root, $"proj{index}");
            Directory.CreateDirectory(projectDirectory);
            _feed.WriteConsumerProject(
                Path.Combine(projectDirectory, $"proj{index}.csproj"), "universal");
            aggregatorItems += $"    <ProjectReference Include=\"proj{index}/proj{index}.csproj\" />\n";
        }
        File.WriteAllText(Path.Combine(root, "all.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n"
            + "    <TargetFramework>net10.0</TargetFramework>\n"
            // Aggregator only fans out; without this it would glob the child
            // projects' sources from the repo root and double-compile them.
            + "    <EnableDefaultItems>false</EnableDefaultItems>\n  </PropertyGroup>\n"
            + "  <ItemGroup>\n" + aggregatorItems + "  </ItemGroup>\n</Project>\n");

        string skillsRoot = Path.Combine(root, ".agents", "skills");
        for (int run = 1; run <= 20; run++)
        {
            // Remove the synced output so every run exercises the first-sync
            // race across six parallel projects, not the stamp fast path.
            if (Directory.Exists(skillsRoot))
            {
                Directory.Delete(skillsRoot, recursive: true);
            }

            string output = _feed.Run("build all.csproj -m", root);
            Assert.DoesNotContain(": error", output);

            string[] skillDirectories = Directory.GetDirectories(skillsRoot);
            Assert.Single(skillDirectories);
            Assert.Equal(SkillNameV1, Path.GetFileName(skillDirectories[0]));
            Assert.Equal(CompleteSkill, SyncedFiles(skillDirectories[0]));
        }
    }

    [Fact]
    public void Case6_two_versions_coexist_without_thrash()
    {
        string root = Path.Combine(_feed.WorkRoot, "case6");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        _feed.WriteNugetConfig(root);

        string projectV1 = Path.Combine(root, "uses-v1");
        string projectV2 = Path.Combine(root, "uses-v2");
        Directory.CreateDirectory(projectV1);
        Directory.CreateDirectory(projectV2);
        _feed.WriteConsumerProject(Path.Combine(projectV1, "uses-v1.csproj"), "universal", "1.0.0");
        _feed.WriteConsumerProject(Path.Combine(projectV2, "uses-v2.csproj"), "universal", "2.0.0");

        _feed.Run("build uses-v1/uses-v1.csproj", root);
        _feed.Run("build uses-v2/uses-v2.csproj", root);

        string skillsRoot = Path.Combine(root, ".agents", "skills");
        Assert.True(Directory.Exists(Path.Combine(skillsRoot, SkillNameV1)));
        Assert.True(Directory.Exists(Path.Combine(skillsRoot, SkillNameV2)));
        Assert.Equal("2.0.0",
            File.ReadAllLines(Path.Combine(skillsRoot, SkillNameV2, ".agentskills-stamp"))[0]);

        // Rebuilding either project must not rewrite either directory.
        DateTime marker = DateTime.UtcNow;
        _feed.Run("build uses-v1/uses-v1.csproj", root);
        _feed.Run("build uses-v2/uses-v2.csproj", root);
        string[] rewritten = Directory
            .GetFiles(skillsRoot, "*", SearchOption.AllDirectories)
            .Where(f => File.GetLastWriteTimeUtc(f) > marker)
            .ToArray();
        Assert.Empty(rewritten);
    }

    [Fact]
    public void Case7_bare_csproj_finds_root_via_git_walk_up()
    {
        string root = Path.Combine(_feed.WorkRoot, "case7");
        Directory.CreateDirectory(Path.Combine(root, ".git"));
        _feed.WriteNugetConfig(root);
        string projectDirectory = Path.Combine(root, "src", "deep", "consumer");
        Directory.CreateDirectory(projectDirectory);
        _feed.WriteConsumerProject(Path.Combine(projectDirectory, "consumer.csproj"), "universal");

        string output = _feed.Run("build consumer.csproj", projectDirectory);

        Assert.DoesNotContain("AGSK102", output);
        Assert.True(Directory.Exists(Path.Combine(root, ".agents", "skills", SkillNameV1)));
        Assert.False(Directory.Exists(Path.Combine(projectDirectory, ".agents")));
    }

    [Fact]
    public void Case8_no_git_no_sln_falls_back_with_AGSK102()
    {
        string consumer = _feed.CreateConsumer("case8", "universal", withGitDirectory: false);
        string output = _feed.Run("build", consumer);

        Assert.Contains("AGSK102", output);
        Assert.True(Directory.Exists(Path.Combine(consumer, ".agents", "skills", SkillNameV1)));
    }

    [Fact]
    public void Case9_second_build_skips_sync_via_stamp()
    {
        string consumer = _feed.CreateConsumer("case9", "universal");
        _feed.Run("build", consumer);

        string skillDirectory = Path.Combine(consumer, ".agents", "skills", SkillNameV1);
        DateTime marker = DateTime.UtcNow;

        // Detailed verbosity exposes Copy's per-file logging; after the stamp
        // exists the batched includes produce nothing, so no copy lines appear.
        string output = _feed.Run("build -v:d", consumer);
        Assert.DoesNotContain(SkillNameV1 + "/SKILL.md", output.Replace('\\', '/'));

        string[] rewritten = Directory
            .GetFiles(skillDirectory, "*", SearchOption.AllDirectories)
            .Where(f => File.GetLastWriteTimeUtc(f) > marker)
            .ToArray();
        Assert.Empty(rewritten);
    }

    [Fact]
    public void Case10_multi_tfm_fixture_packs_single_content_set()
    {
        string output = _feed.Run(
            "pack tests/fixtures/FixtureLib/FixtureLib.csproj -c Release"
            + $" -o \"{Path.Combine(_feed.WorkRoot, "multi-tfm")}\""
            // Embedded quotes keep the semicolon out of dotnet's -p splitting
            // while still reaching MSBuild unescaped (%3B would stay escaped
            // and break the SDK's TargetFrameworks dispatch).
            + " -p:FixtureTargetFrameworks=\\\"net10.0;netstandard2.0\\\""
            + " -p:Version=3.0.0",
            _feed.RepoRoot);

        Assert.DoesNotContain("NU5118", output);

        string nupkgPath = Path.Combine(_feed.WorkRoot, "multi-tfm", "FixtureLib.3.0.0.nupkg");
        using System.IO.Compression.ZipArchive nupkg = System.IO.Compression.ZipFile.OpenRead(nupkgPath);
        string[] entries = nupkg.Entries.Select(e => e.FullName).ToArray();

        Assert.Contains("lib/net10.0/FixtureLib.dll", entries);
        Assert.Contains("lib/netstandard2.0/FixtureLib.dll", entries);
        // Exactly one content set: no duplicated agent-assets entries.
        Assert.Equal(entries.Length, entries.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Single(entries, e => e == "agent-assets/SKILL.claude.md");
        Assert.Single(entries, e => e == "build/FixtureLib.targets");
    }
}
