using Checkout.Rules.Contracts;

namespace Checkout.Rules.Services;

/// <summary>
/// The reference implementation, and in a black-box session also the thing being measured.
/// </summary>
/// <remarks>
/// Unlike a white-box session, this <b>is</b> baked into the judge image, in source form: Stryker mutates it to
/// produce the faults a competitor's suite is scored on catching. That makes the black-box image the most
/// sensitive artifact in the system — private registry only.
/// <para>
/// Every branch here is a mutation target, so the boundaries deliberately match the contract's wording
/// exactly. Changing <c>&gt;=</c> to <c>&gt;</c> anywhere below is precisely the kind of fault a competitor's
/// suite is expected to catch.
/// </para>
/// </remarks>
public sealed class DiscountEngine : IDiscountEngine
{
    private const int BulkQuantity = 10;
    private const int WholesaleQuantity = 50;

    private const decimal BulkDiscount = 0.05m;
    private const decimal WholesaleDiscount = 0.12m;

    private const decimal Save20Minimum = 100m;
    private const decimal MemberFreeShippingFrom = 25m;
    private const decimal StandardFreeShippingFrom = 75m;

    public decimal LineTotal(int quantity, decimal unitPrice)
    {
        if (quantity <= 0 || unitPrice < 0m) return 0m;

        var gross = quantity * unitPrice;

        var discount = quantity >= WholesaleQuantity
            ? WholesaleDiscount
            : quantity >= BulkQuantity
                ? BulkDiscount
                : 0m;

        return Round(gross * (1m - discount));
    }

    public decimal ApplyCoupon(decimal subtotal, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Round(Floor(subtotal));

        var discount = code.Trim().ToUpperInvariant() switch
        {
            "SAVE10" => 0.10m,
            "SAVE20" when subtotal >= Save20Minimum => 0.20m,
            _ => 0m,
        };

        return Round(Floor(subtotal * (1m - discount)));
    }

    public bool QualifiesForFreeShipping(decimal subtotal, bool isMember) =>
        subtotal >= (isMember ? MemberFreeShippingFrom : StandardFreeShippingFrom);

    /// <summary>The contract promises a coupon never drives the total below zero.</summary>
    private static decimal Floor(decimal value) => value < 0m ? 0m : value;

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
