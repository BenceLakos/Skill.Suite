using SkillSuite.Session.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace SkillSuite.Session.UnitTests;

/// <summary>
/// The hidden graded suite. Baked into the judge image and never shipped to competitors.
/// </summary>
/// <remarks>
/// This class is the shape every graded suite must take. Three rules, all enforced by the harness rather than
/// by review:
/// <list type="number">
/// <item>
/// Derive from <c>LoggedTest&lt;TSelf&gt;</c> and take <c>IClassFixture&lt;FixtureScope&lt;TSelf&gt;&gt;</c>.
/// Without the fixture there is no start/finish-fixture pair and the platform records nothing at all.
/// </item>
/// <item>
/// Assert through <c>Log</c>, never bare <c>Assert</c>. A bare assertion still fails the test but emits no
/// event, so the result reaches the UI as a red row with nothing explaining it.
/// </item>
/// <item>
/// Resolve the service with <c>ServiceResolver.Resolve&lt;T&gt;().WithCallLogging()</c>. The proxy records
/// every call with its arguments and return value, which is what makes a disputed mark reviewable.
/// </item>
/// </list>
/// <para>
/// One test case is exactly one aspect, and an aspect counts as satisfied only when every test claiming it
/// passes — so a <c>[Theory]</c> with five rows is all-or-nothing. Split it if the rows should score
/// separately. <c>CompetitorVisible = true</c> lets the aspect move the bar the competitor sees; leave it off
/// for aspects that should grade without revealing anything.
/// </para>
/// </remarks>
public sealed class CalculatorTests : LoggedTest<CalculatorTests>, IClassFixture<FixtureScope<CalculatorTests>>
{
    private readonly ICalculator _svc = ServiceResolver.Resolve<ICalculator>().WithCallLogging();

    public CalculatorTests(ITestOutputHelper output, FixtureScope<CalculatorTests> scope)
        : base(output, scope) { }

    [Aspect("A1.1", CompetitorVisible = true)]
    [Fact]
    public void Add_TwoPositives_ReturnsSum() =>
        Log.AssertEqual(5, _svc.Add(2, 3));

    [Aspect("A1.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(2, -3, -1)]
    [InlineData(-2, -3, -5)]
    public void Add_WithNegatives_ReturnsSum(int left, int right, int expected) =>
        Log.AssertEqual(expected, _svc.Add(left, right));

    [Aspect("A2.1", CompetitorVisible = true)]
    [Fact]
    public void Divide_ByNonZero_ReturnsQuotient() =>
        Log.AssertEqual(3, _svc.Divide(9, 3));

    [Aspect("A2.2", CompetitorVisible = true)]
    [Fact]
    public void Divide_ByZero_ReturnsZeroSentinel() =>
        // The edge case worth grading separately: a naive implementation throws here, and the contract says
        // it must not.
        Log.AssertEqual(0, _svc.Divide(9, 0));

    // Graded, but invisible to the competitor's own score - exercises the CompetitorVisible flag.
    [Aspect("A3.1")]
    [Fact]
    public void Describe_ReturnsNonNull() =>
        Log.AssertNotNull(_svc.Describe());
}
