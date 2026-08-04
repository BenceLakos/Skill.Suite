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

    // Hidden from the competitor's own score, but still graded - exercises the CompetitorVisible flag.
    [Aspect("F3.1")]
    [Fact]
    public void Describe_ReturnsNonNull() =>
        Log.AssertNotNull(_svc.Describe());

    // A [Theory], deliberately. xunit reports a parameterised case as `Name(left: 6, right: 3, expected: 2)`,
    // and the integrity manifest compares against bare method names - so a suite of only [Fact]s cannot detect a
    // regression that rejects parameterised tests as fabricated. That exact bug shipped once and passed the
    // matrix 8/8, because nothing here was parameterised. This row is the guard.
    [Aspect("F2.3", CompetitorVisible = true)]
    [Theory]
    [InlineData(6, 3, 2)]
    [InlineData(-6, 3, -2)]
    public void Divide_ExactQuotients(int left, int right, int expected) =>
        Log.AssertEqual(expected, _svc.Divide(left, right));
}
