using Checkout.Rules.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace Checkout.Rules.UnitTests;

/// <summary>
/// A competitor's thorough suite: written independently from the contract, covering every documented boundary.
/// </summary>
/// <remarks>
/// Deliberately not a copy of the reference suite — different names, different groupings, different data — so
/// this sample demonstrates the real thing being measured rather than a suite that happens to match. It should
/// score near the top of the band: high coverage, and it kills mutants because each assertion pins an exact
/// value at a boundary rather than merely running the code.
/// </remarks>
public sealed class CheckoutTests : LoggedTest<CheckoutTests>, IClassFixture<FixtureScope<CheckoutTests>>
{
    private readonly IDiscountEngine _engine = ServiceResolver.Resolve<IDiscountEngine>().WithCallLogging();

    public CheckoutTests(ITestOutputHelper output, FixtureScope<CheckoutTests> scope)
        : base(output, scope) { }

    [Aspect("C1", CompetitorVisible = true)]
    [Theory]
    [InlineData(1, 20, 20)]
    [InlineData(9, 20, 180)]        // last quantity before the bulk tier
    [InlineData(10, 20, 190)]       // bulk tier: 5% off 200
    [InlineData(49, 20, 931)]       // last quantity before wholesale: 5% off 980
    [InlineData(50, 20, 880)]       // wholesale: 12% off 1000
    [InlineData(120, 20, 2112)]     // well past wholesale, tiers must not stack
    public void LineTotal_AppliesTheCorrectTier(int quantity, decimal price, decimal expected) =>
        Log.AssertEqual(expected, _engine.LineTotal(quantity, price));

    [Aspect("C2", CompetitorVisible = true)]
    [Theory]
    [InlineData(0, 5)]
    [InlineData(-1, 5)]
    [InlineData(5, -0.01)]
    public void LineTotal_RejectsInvalidInput(int quantity, decimal price) =>
        Log.AssertEqual(0m, _engine.LineTotal(quantity, price));

    [Aspect("C3")]
    [Fact]
    public void LineTotal_RoundsHalvesAwayFromZero() =>
        // 3 × 0.125 = 0.375, no discount, so the rounding rule alone decides between 0.38 and 0.37.
        Log.AssertEqual(0.38m, _engine.LineTotal(3, 0.125m));

    [Aspect("C4", CompetitorVisible = true)]
    [Theory]
    [InlineData("SAVE10", 200, 180)]
    [InlineData("save10", 200, 180)]
    [InlineData("SaVe10", 200, 180)]
    public void Coupon_Save10_IsTenPercentAndCaseInsensitive(string code, decimal subtotal, decimal expected) =>
        Log.AssertEqual(expected, _engine.ApplyCoupon(subtotal, code));

    [Aspect("C5", CompetitorVisible = true)]
    [Theory]
    [InlineData(99.99, 99.99)]      // one penny short of the minimum
    [InlineData(100, 80)]           // exactly the minimum
    [InlineData(250, 200)]
    public void Coupon_Save20_HonoursItsMinimum(decimal subtotal, decimal expected) =>
        Log.AssertEqual(expected, _engine.ApplyCoupon(subtotal, "SAVE20"));

    [Aspect("C6", CompetitorVisible = true)]
    [Theory]
    [InlineData("FREESHIP")]
    [InlineData("freeship")]
    [InlineData("BOGUS")]
    [InlineData("")]
    [InlineData(null)]
    public void Coupon_InertAndUnknownCodes_LeaveTheSubtotalAlone(string? code) =>
        Log.AssertEqual(80m, _engine.ApplyCoupon(80m, code));

    [Aspect("C7")]
    [Fact]
    public void Coupon_ClampsANegativeSubtotalToZero() =>
        Log.AssertEqual(0m, _engine.ApplyCoupon(-25m, "SAVE10"));

    [Aspect("C8", CompetitorVisible = true)]
    [Theory]
    [InlineData(24.99, true, false)]
    [InlineData(25, true, true)]
    [InlineData(74.99, false, false)]
    [InlineData(75, false, true)]
    [InlineData(1000, false, true)]
    public void FreeShipping_UsesTheRightThresholdPerCustomer(decimal subtotal, bool member, bool expected) =>
        Log.AssertEqual(expected, _engine.QualifiesForFreeShipping(subtotal, member));

    [Aspect("C9")]
    [Fact]
    public void FreeShipping_MembersQualifyEarlierThanNonMembers()
    {
        // The one assertion that pins the two thresholds *relative* to each other, so swapping them is caught.
        Log.AssertTrue(_engine.QualifiesForFreeShipping(30m, isMember: true));
        Log.AssertFalse(_engine.QualifiesForFreeShipping(30m, isMember: false));
    }
}
