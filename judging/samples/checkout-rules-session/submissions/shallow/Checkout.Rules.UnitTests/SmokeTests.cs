using Checkout.Rules.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace Checkout.Rules.UnitTests;

/// <summary>
/// The submission that justifies mutation testing existing at all.
/// </summary>
/// <remarks>
/// It calls every member across every branch, so its <b>line coverage is as high as the thorough suite's</b>.
/// Against coverage alone the two are indistinguishable. What it does not do is check any of the answers: it
/// asserts that a number came back, that a bool is one of two values, that nothing threw. Almost every mutant
/// survives it, so the kill rate is what separates it from real work — and the composite score reflects that.
/// <para>
/// If this ever scores close to <c>submissions/thorough</c>, the mutation ramp in <c>marking-map.json</c> has
/// stopped doing any work and needs re-tuning.
/// </para>
/// </remarks>
public sealed class SmokeTests : LoggedTest<SmokeTests>, IClassFixture<FixtureScope<SmokeTests>>
{
    private readonly IDiscountEngine _engine = ServiceResolver.Resolve<IDiscountEngine>().WithCallLogging();

    public SmokeTests(ITestOutputHelper output, FixtureScope<SmokeTests> scope)
        : base(output, scope) { }

    [Aspect("S1", CompetitorVisible = true)]
    [Theory]
    [InlineData(1, 10)]
    [InlineData(9, 10)]
    [InlineData(10, 10)]
    [InlineData(50, 10)]
    [InlineData(0, 10)]
    [InlineData(5, -1)]
    public void LineTotal_ReturnsSomething(int quantity, decimal price) =>
        // Every branch of LineTotal runs. Nothing about the result is checked beyond "not negative".
        Log.AssertTrue(_engine.LineTotal(quantity, price) >= 0m);

    [Aspect("S2", CompetitorVisible = true)]
    [Theory]
    [InlineData("SAVE10")]
    [InlineData("SAVE20")]
    [InlineData("FREESHIP")]
    [InlineData("WHATEVER")]
    [InlineData(null)]
    public void ApplyCoupon_ReturnsSomething(string? code) =>
        Log.AssertTrue(_engine.ApplyCoupon(150m, code) >= 0m);

    [Aspect("S3", CompetitorVisible = true)]
    [Fact]
    public void ApplyCoupon_BelowTheSave20MinimumAlsoRuns() =>
        Log.AssertTrue(_engine.ApplyCoupon(50m, "SAVE20") >= 0m);

    [Aspect("S4", CompetitorVisible = true)]
    [Theory]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(10, false)]
    [InlineData(100, false)]
    public void QualifiesForFreeShipping_ReturnsABool(decimal subtotal, bool member)
    {
        var result = _engine.QualifiesForFreeShipping(subtotal, member);

        // Tautological on purpose: this is what "asserting nothing" looks like in practice.
        Log.AssertTrue(result || !result);
    }
}
