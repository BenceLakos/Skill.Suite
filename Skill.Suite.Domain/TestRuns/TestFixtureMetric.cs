namespace Skill.Suite.Domain.TestRuns;

/// <summary>
/// Which measurement a fixture-scoped metric event carries.
/// </summary>
/// <remarks>
/// Named rather than inferred from the event's wire discriminator, so the domain never has to match on a
/// protocol string to decide which column a number belongs in. The parser is the one place that knows the
/// protocol, and it says which of these it read.
/// </remarks>
public enum TestFixtureMetric
{
    /// <summary>Line coverage this fixture achieved on its own.</summary>
    LineCoverage = 0,

    /// <summary>Kill rate over the mutants this fixture's tests reached.</summary>
    MutationScore = 1,
}
