using Skill.Suite.TestLog.Protocol;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Protocol;

/// <summary>
/// Pins the fixture-naming rule that joins a measurement to a fixture already in the stream.
/// </summary>
/// <remarks>
/// A wrong answer here does not fail anything: it quietly produces a second fixture beside the real one, which
/// looks like a judge bug to whoever has to explain the results.
/// </remarks>
public sealed class FixtureNameTests
{
    [Theory]
    [InlineData("Acme.Widgets.WidgetTests", "WidgetTests")]
    [InlineData("WidgetTests", "WidgetTests")]
    [InlineData("Acme.Widgets.Outer+Inner", "Inner")]
    [InlineData("Acme.Widgets.WidgetTests, Acme.Widgets, Version=1.0.0.0", "WidgetTests")]
    [InlineData("  Acme.WidgetTests  ", "WidgetTests")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void FromTypeName(string? input, string? expected) =>
        Assert.Equal(expected, FixtureNames.FromTypeName(input));

    [Theory]
    [InlineData("Acme.Widgets.WidgetTests.Add_Works", "WidgetTests")]
    [InlineData("Acme.Widgets.Outer+Inner.Add_Works", "Inner")]
    [InlineData("Acme.Widgets.WidgetTests.Add(left: 1.5, right: \"a.b\")", "WidgetTests")]
    [InlineData("Add_Works", null)]
    [InlineData("Add(left: 1)", null)]
    [InlineData(null, null)]
    public void FromTestName(string? input, string? expected) =>
        Assert.Equal(expected, FixtureNames.FromTestName(input));

    [Fact]
    public void FromTypeName_AgreesWithWhatTheHarnessPutsInTheStream()
    {
        // The acceptance oracle: FixtureScope names a fixture typeof(T).Name, and a consumer routing a
        // qualified name must land on exactly that string or it invents a fixture of its own.
        Assert.Equal(
            FixtureNames.FromTypeName(typeof(FixtureNameTests).FullName),
            typeof(FixtureNameTests).Name);
    }
}
