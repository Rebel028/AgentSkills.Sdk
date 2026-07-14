namespace FixtureLib;

/// <summary>Trivial type so the fixture assembly is non-empty.</summary>
public sealed class Widget
{
    /// <summary>Returns a greeting.</summary>
    /// <param name="name">Who to greet.</param>
    /// <example><code>new Widget().Greet("World");</code></example>
    public string Greet(string name)
    {
        return $"Hello, {name}!";
    }

    // Deliberately undocumented: proves the SDK's auto-enabled XML doc
    // generation suppresses CS1591 (AgentSkillIncludeXmlDocs, ADR-0012).
    public int GreetCount;
}
