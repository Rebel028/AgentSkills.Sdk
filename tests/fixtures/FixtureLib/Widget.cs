namespace FixtureLib;

/// <summary>Trivial type so the fixture assembly is non-empty.</summary>
public sealed class Widget
{
    /// <summary>Returns a greeting.</summary>
    public string Greet(string name)
    {
        return $"Hello, {name}!";
    }
}
