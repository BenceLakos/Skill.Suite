using JudgeFixture.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace JudgeFixture.UnitTests;

/// <summary>
/// The hidden suite, in exactly the shape a graded session suite must use. Baked into the judge image and
/// never shipped to competitors.
/// </summary>
public sealed class CalculatorTests : LoggedTest<CalculatorTests>, IClassFixture<FixtureScope<CalculatorTests>>
{
    private readonly ICalculator _svc = ServiceResolver.Resolve<ICalculator>().WithCallLogging();

    public CalculatorTests(ITestOutputHelper output, FixtureScope<CalculatorTests> scope)
        : base(output, scope) { }

    [Aspect("F1.1", CompetitorVisible = true)]
    [Fact]
    public void Add_TwoPositives_ReturnsSum() =>
        Log.AssertEqual(5, _svc.Add(2, 3));

    [Aspect("F1.2", CompetitorVisible = true)]
    [Fact]
    public void Add_WithNegative_ReturnsSum() =>
        Log.AssertEqual(-1, _svc.Add(2, -3));

    [Aspect("F2.1", CompetitorVisible = true)]
    [Fact]
    public void Divide_ByNonZero_ReturnsQuotient() =>
        Log.AssertEqual(3, _svc.Divide(9, 3));

    [Aspect("F2.2", CompetitorVisible = true)]
    [Fact]
    public void Divide_ByZero_ReturnsZeroSentinel() =>
        Log.AssertEqual(0, _svc.Divide(9, 0));

    // Describe_ReturnsNonNull lives in DescribeTests, so this persona has two fixtures with different
    // footprints - see the note there.
}
