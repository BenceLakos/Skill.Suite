namespace Skill.Suite.Marker.Tests.Support;

/// <summary>Locates the checked-in fixture files next to the test binary.</summary>
internal static class Fixture
{
    internal const string Cobertura = "cobertura-sample.xml";
    internal const string Trx = "results-sample.trx";
    internal const string Stryker = "stryker-sample.json";
    internal const string StrykerPerTest = "stryker-per-test.json";
    internal const string FixtureCoverage = "fixture-coverage";
    internal const string MapTwoParts = "map-two-parts.json";
    internal const string MapOverallOnly = "map-overall-only.json";
    internal const string MapObjectParts = "map-object-parts.json";
    internal const string EventsWithAspects = "events-aspects.jsonl";

    /// <summary>Absolute path to a fixture file.</summary>
    internal static string Path(string name) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
