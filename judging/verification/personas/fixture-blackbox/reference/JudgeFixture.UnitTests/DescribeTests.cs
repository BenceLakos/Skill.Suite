using JudgeFixture.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace JudgeFixture.UnitTests;

/// <summary>
/// A deliberate SECOND test class, split out of CalculatorTests so this persona has more than one fixture.
/// </summary>
/// <remarks>
/// Per-fixture coverage and mutation are indistinguishable from the whole-suite numbers when a submission has
/// exactly one test class, which is what this persona used to be — the matrix would have passed without the
/// feature working at all. Splitting one test out gives two classes with genuinely different footprints: this
/// one touches only Describe, CalculatorTests touches only the arithmetic.
///
/// The suite's totals are unchanged, so the calibration bands in expected/fixture-blackbox.json still describe
/// the same five tests over the same implementation.
/// </remarks>
public sealed class DescribeTests : LoggedTest<DescribeTests>, IClassFixture<FixtureScope<DescribeTests>>
{
    private readonly ICalculator _svc = ServiceResolver.Resolve<ICalculator>().WithCallLogging();

    public DescribeTests(ITestOutputHelper output, FixtureScope<DescribeTests> scope)
        : base(output, scope) { }

    // Hidden from the competitor's own score, but still graded - exercises the CompetitorVisible flag.
    [Aspect("F3.1")]
    [Fact]
    public void Describe_ReturnsNonNull() =>
        Log.AssertNotNull(_svc.Describe());
}
