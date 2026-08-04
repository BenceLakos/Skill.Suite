using Checkout.Rules.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace Checkout.Rules.UnitTests;

/// <summary>
/// The reference suite — calibration only, and in a black-box session this is the answer key.
/// </summary>
/// <remarks>
/// It exists to prove the session is markable before anyone competes: it should land near the top of the
/// scoring band, which tells you the ramps in <c>marking-map.json</c> leave honest headroom. It is
/// <b>excluded from the black-box image</b> by <c>Dockerfile.blackbox.dockerignore</c> and must never be
/// handed to competitors — it is exactly the work they are being asked to do.
/// <para>
/// A competitor's own suite is not compared against this one. It is measured: how much of
/// <c>DiscountEngine</c> it covers, and how many of Stryker's mutants it kills.
/// </para>
/// </remarks>
public sealed class DiscountEngineTests : LoggedTest<DiscountEngineTests>, IClassFixture<FixtureScope<DiscountEngineTests>>
{
    private readonly IDiscountEngine _svc = ServiceResolver.Resolve<IDiscountEngine>().WithCallLogging();

    public DiscountEngineTests(ITestOutputHelper output, FixtureScope<DiscountEngineTests> scope)
        : base(output, scope) { }

    // ---- LineTotal ----

    [Aspect("A1.1", CompetitorVisible = true)]
    [Fact]
    public void LineTotal_BelowBulk_HasNoDiscount() =>
        Log.AssertEqual(29.97m, _svc.LineTotal(3, 9.99m));

    [Aspect("A1.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void LineTotal_NonPositiveQuantity_IsZero(int quantity) =>
        Log.AssertEqual(0m, _svc.LineTotal(quantity, 10m));

    [Aspect("A1.3", CompetitorVisible = true)]
    [Fact]
    public void LineTotal_NegativeUnitPrice_IsZero() =>
        Log.AssertEqual(0m, _svc.LineTotal(5, -1m));

    [Aspect("A1.4", CompetitorVisible = true)]
    [Theory]
    // The tier boundaries, which is where a >= to > mutation shows up.
    [InlineData(9, 100)]
    [InlineData(10, 95)]
    [InlineData(49, 95)]
    [InlineData(50, 88)]
    public void LineTotal_AtEachTierBoundary_AppliesTheRightDiscount(int quantity, decimal expectedPerHundred) =>
        Log.AssertEqual(expectedPerHundred * quantity, _svc.LineTotal(quantity, 100m) / 1m);

    [Aspect("A1.5")]
    [Fact]
    public void LineTotal_RoundsToTwoDecimals() =>
        // 11 × 1.005 = 11.055, less 5% = 10.50225 → 10.50
        Log.AssertEqual(10.50m, _svc.LineTotal(11, 1.005m));

    // ---- ApplyCoupon ----

    [Aspect("A2.1", CompetitorVisible = true)]
    [Fact]
    public void ApplyCoupon_Save10_TakesATenth() =>
        Log.AssertEqual(45m, _svc.ApplyCoupon(50m, "SAVE10"));

    [Aspect("A2.2", CompetitorVisible = true)]
    [Fact]
    public void ApplyCoupon_IsCaseInsensitive() =>
        Log.AssertEqual(45m, _svc.ApplyCoupon(50m, "save10"));

    [Aspect("A2.3", CompetitorVisible = true)]
    [Theory]
    // The minimum is the interesting boundary: exactly 100 qualifies, a penny under does not.
    [InlineData(100, 80)]
    [InlineData(99.99, 99.99)]
    public void ApplyCoupon_Save20_OnlyAppliesAtTheMinimum(decimal subtotal, decimal expected) =>
        Log.AssertEqual(expected, _svc.ApplyCoupon(subtotal, "SAVE20"));

    [Aspect("A2.4", CompetitorVisible = true)]
    [Fact]
    public void ApplyCoupon_FreeShip_ChangesNothing() =>
        Log.AssertEqual(60m, _svc.ApplyCoupon(60m, "FREESHIP"));

    [Aspect("A2.5", CompetitorVisible = true)]
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("NOPE")]
    public void ApplyCoupon_UnknownOrMissingCode_ChangesNothing(string? code) =>
        Log.AssertEqual(42.50m, _svc.ApplyCoupon(42.50m, code));

    [Aspect("A2.6")]
    [Fact]
    public void ApplyCoupon_NeverGoesNegative() =>
        Log.AssertEqual(0m, _svc.ApplyCoupon(-10m, "SAVE10"));

    // ---- QualifiesForFreeShipping ----

    [Aspect("A3.1", CompetitorVisible = true)]
    [Theory]
    [InlineData(24.99, false)]
    [InlineData(25, true)]
    public void FreeShipping_ForMembers_StartsAtTwentyFive(decimal subtotal, bool expected) =>
        Log.AssertEqual(expected, _svc.QualifiesForFreeShipping(subtotal, isMember: true));

    [Aspect("A3.2", CompetitorVisible = true)]
    [Theory]
    [InlineData(74.99, false)]
    [InlineData(75, true)]
    public void FreeShipping_ForNonMembers_StartsAtSeventyFive(decimal subtotal, bool expected) =>
        Log.AssertEqual(expected, _svc.QualifiesForFreeShipping(subtotal, isMember: false));
}
