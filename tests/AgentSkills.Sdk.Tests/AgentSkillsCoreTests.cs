using AgentSkills.Sdk.Tasks;
using Xunit;

namespace AgentSkills.Sdk.Tests;

public sealed class SanitizeTests
{
    [Theory]
    [InlineData("MyAwesome.Engine", "myawesome-engine")]
    [InlineData("2.4.1-preview.3", "2-4-1-preview-3")]
    [InlineData("--Weird__Name--", "weird-name")]
    [InlineData("a...b", "a-b")]
    [InlineData("UPPER", "upper")]
    // Unicode letters are outside [a-z0-9]: each run collapses to a single hyphen.
    [InlineData("Ünïcödé", "n-c-d")]
    public void Sanitize_collapses_to_spec_alphabet(string input, string expected)
    {
        Assert.Equal(expected, AgentSkillsCore.Sanitize(input));
    }

    [Theory]
    [InlineData("2.4.1-preview.3+sha.abc", "2.4.1-preview.3")]
    [InlineData("1.0.0+build", "1.0.0")]
    [InlineData("1.0.0", "1.0.0")]
    public void StripBuildMetadata_drops_plus_and_after(string input, string expected)
    {
        Assert.Equal(expected, AgentSkillsCore.StripBuildMetadata(input));
    }
}

public sealed class SkillNameTests
{
    [Fact]
    public void ComputeSkillName_matches_spec_example()
    {
        string name = AgentSkillsCore.ComputeSkillName("MyAwesome.Engine", "2.4.1-preview.3+sha.abc");
        Assert.Equal("use-myawesome-engine-v2-4-1-preview-3", name);
    }

    [Fact]
    public void ComputeSkillName_at_64_chars_is_valid()
    {
        // "use-" (4) + 53 chars + "-v" (2) + "1-0-0" (5) = 64
        string packageId = new string('a', 53);
        string name = AgentSkillsCore.ComputeSkillName(packageId, "1.0.0");
        Assert.Equal(64, name.Length);
        Assert.True(AgentSkillsCore.IsValidSkillName(name));
    }

    [Fact]
    public void ComputeSkillName_at_65_chars_is_invalid()
    {
        string packageId = new string('a', 54);
        string name = AgentSkillsCore.ComputeSkillName(packageId, "1.0.0");
        Assert.Equal(65, name.Length);
        Assert.False(AgentSkillsCore.IsValidSkillName(name));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("a-b-c")]
    [InlineData("use-myawesome-engine-v2-4-1-preview-3")]
    public void IsValidSkillName_accepts_spec_compliant_names(string name)
    {
        Assert.True(AgentSkillsCore.IsValidSkillName(name));
    }

    [Theory]
    [InlineData("")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a--b")]
    [InlineData("A-b")]
    [InlineData("a_b")]
    [InlineData("a b")]
    public void IsValidSkillName_rejects_rule_violations(string name)
    {
        Assert.False(AgentSkillsCore.IsValidSkillName(name));
    }
}

public sealed class ConsumerFlagTests
{
    [Theory]
    [InlineData("MyAwesome.Engine", "MyAwesomeEngineAgentSkills")]
    [InlineData("My.Awesome-Engine", "MyAwesomeEngineAgentSkills")]
    [InlineData("FixtureLib", "FixtureLibAgentSkills")]
    public void DeriveConsumerFlagName_strips_non_alphanumerics(string packageId, string expected)
    {
        Assert.Equal(expected, AgentSkillsCore.DeriveConsumerFlagName(packageId));
    }
}

public sealed class YamlQuotingTests
{
    [Theory]
    [InlineData("plain", "'plain'")]
    [InlineData("It's fine", "'It''s fine'")]
    [InlineData("line1\nline2", "'line1 line2'")]
    [InlineData("a\r\nb", "'a b'")]
    [InlineData("a\rb", "'a b'")]
    public void QuoteYamlSingle_doubles_quotes_and_flattens_newlines(string input, string expected)
    {
        Assert.Equal(expected, AgentSkillsCore.QuoteYamlSingle(input));
    }
}

public sealed class DescriptionTests
{
    [Fact]
    public void DefaultDescription_uses_package_id()
    {
        Assert.Equal(
            "Guidance and rules for integrating MyAwesome.Engine APIs.",
            AgentSkillsCore.DefaultDescription("MyAwesome.Engine"));
    }
}

public sealed class FrontmatterCompositionTests
{
    private static SkillFrontmatter SpecExample()
    {
        return new SkillFrontmatter(
            name: "use-myawesome-engine-v2-4-1-preview-3",
            description: "Expert rules for MyAwesomeEngine APIs.",
            packageId: "MyAwesome.Engine",
            packageVersion: "2.4.1-preview.3",
            userInvocable: "true",
            contextStrategy: "fork");
    }

    [Fact]
    public void ClaudeVariant_puts_extensions_top_level()
    {
        string skill = AgentSkillsCore.ComposeClaudeVariant(SpecExample(), "Body.");
        string expected =
            "---\n" +
            "name: use-myawesome-engine-v2-4-1-preview-3\n" +
            "description: 'Expert rules for MyAwesomeEngine APIs.'\n" +
            "user-invocable: true\n" +
            "context: fork\n" +
            "---\n" +
            "\n" +
            "Body.\n";
        Assert.Equal(expected, skill);
    }

    [Fact]
    public void AgentsVariant_puts_extensions_under_metadata_as_strings()
    {
        string skill = AgentSkillsCore.ComposeAgentsVariant(SpecExample(), "Body.");
        string expected =
            "---\n" +
            "name: use-myawesome-engine-v2-4-1-preview-3\n" +
            "description: 'Expert rules for MyAwesomeEngine APIs.'\n" +
            "metadata:\n" +
            "  package-id: MyAwesome.Engine\n" +
            "  package-version: 2.4.1-preview.3\n" +
            "  user-invocable: \"true\"\n" +
            "  context: \"fork\"\n" +
            "---\n" +
            "\n" +
            "Body.\n";
        Assert.Equal(expected, skill);
    }

    [Fact]
    public void Optional_extensions_are_omitted_when_unset()
    {
        SkillFrontmatter frontmatter = new SkillFrontmatter(
            name: "use-x-v1-0-0",
            description: "D.",
            packageId: "X",
            packageVersion: "1.0.0",
            userInvocable: null,
            contextStrategy: null);

        string claude = AgentSkillsCore.ComposeClaudeVariant(frontmatter, "B.");
        Assert.DoesNotContain("user-invocable", claude);
        Assert.DoesNotContain("context", claude);

        string agents = AgentSkillsCore.ComposeAgentsVariant(frontmatter, "B.");
        Assert.DoesNotContain("user-invocable", agents);
        Assert.DoesNotContain("context:", agents);
        // package identity metadata is always present
        Assert.Contains("  package-id: X\n", agents);
        Assert.Contains("  package-version: 1.0.0\n", agents);
    }

    [Fact]
    public void Variants_normalize_crlf_bodies_and_end_with_single_newline()
    {
        string skill = AgentSkillsCore.ComposeClaudeVariant(SpecExample(), "line1\r\nline2\r\n\r\n");
        Assert.EndsWith("line1\nline2\n", skill);
        Assert.DoesNotContain("\r", skill);
    }
}

public sealed class ScaffoldTests
{
    [Fact]
    public void ScaffoldBody_uses_package_metadata_and_points_at_references()
    {
        string body = AgentSkillsCore.ScaffoldBody("MyAwesome.Engine", "Engine for awesome things.");
        string expected =
            "# MyAwesome.Engine guide\n" +
            "\n" +
            "Engine for awesome things.\n" +
            "\n" +
            "See the `references/` directory for detailed documentation.\n";
        Assert.Equal(expected, body);
    }

    [Fact]
    public void ScaffoldBody_omits_description_paragraph_when_empty()
    {
        string body = AgentSkillsCore.ScaffoldBody("X", "");
        string expected =
            "# X guide\n" +
            "\n" +
            "See the `references/` directory for detailed documentation.\n";
        Assert.Equal(expected, body);
    }
}
