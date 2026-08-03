namespace Checkout.Rules.Contracts;

/// <summary>
/// Checkout pricing rules. In a black-box session this interface is the whole specification: the competitor
/// never sees the implementation and writes their test suite from these rules alone.
/// </summary>
/// <remarks>
/// Every boundary below is stated deliberately and exactly. A competitor is measured on how much of the
/// implementation their suite exercises and how many injected faults it catches, so an ambiguous rule is not a
/// stylistic problem — it is a rule they cannot write a passing test for.
/// <para>
/// House rules, enforced by the harness: pure and deterministic, no state between calls, and
/// <b>never throws</b> — invalid input returns the documented value instead.
/// </para>
/// </remarks>
public interface IDiscountEngine
{
    /// <summary>
    /// Total for one order line, after quantity discount.
    /// </summary>
    /// <param name="quantity">Number of units.</param>
    /// <param name="unitPrice">Price per unit.</param>
    /// <returns>
    /// <c>0</c> when <paramref name="quantity"/> is zero or negative, or <paramref name="unitPrice"/> is
    /// negative. Otherwise <c>quantity × unitPrice</c> less the quantity discount: <b>5%</b> from 10 units,
    /// <b>12%</b> from 50 units. The higher tier wins; the two are never combined. Rounded to 2 decimal
    /// places, halves away from zero.
    /// </returns>
    decimal LineTotal(int quantity, decimal unitPrice);

    /// <summary>
    /// Applies a coupon to an order subtotal.
    /// </summary>
    /// <param name="subtotal">Order subtotal before the coupon.</param>
    /// <param name="code">Coupon code, matched case-insensitively.</param>
    /// <returns>
    /// <list type="bullet">
    /// <item><c>SAVE10</c> — 10% off, no minimum.</item>
    /// <item><c>SAVE20</c> — 20% off, but only when <paramref name="subtotal"/> is at least <c>100</c>;
    /// below that the subtotal is unchanged.</item>
    /// <item><c>FREESHIP</c> — a valid code that changes the subtotal not at all.</item>
    /// <item>Anything else, including <see langword="null"/> and empty — unchanged.</item>
    /// </list>
    /// Never returns less than <c>0</c>. Rounded to 2 decimal places, halves away from zero.
    /// </returns>
    decimal ApplyCoupon(decimal subtotal, string? code);

    /// <summary>
    /// Whether an order ships free.
    /// </summary>
    /// <param name="subtotal">Order subtotal.</param>
    /// <param name="isMember">Whether the customer is a member.</param>
    /// <returns>
    /// For members, <see langword="true"/> from a subtotal of <c>25</c>. For everyone else, from <c>75</c>.
    /// </returns>
    bool QualifiesForFreeShipping(decimal subtotal, bool isMember);
}
