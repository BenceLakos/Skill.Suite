namespace Fibonacci.Contracts;

/// <summary>
/// The Fibonacci service a competitor implements.
/// </summary>
/// <remarks>
/// House style for this session, and the rules the hidden suite grades against:
/// <list type="bullet">
/// <item>Pure and deterministic — same input, same output, no state between calls.</item>
/// <item><b>Never throws.</b> Invalid input returns a documented sentinel instead.</item>
/// <item>The sequence starts <c>F(0) = 0</c>, <c>F(1) = 1</c>.</item>
/// </list>
/// "Never throws" is not decoration: the call-logging proxy turns an escaped exception into an errored test,
/// so a throw fails the aspect just as surely as a wrong number.
/// </remarks>
public interface IFibonacci
{
    /// <summary>The Fibonacci number at <paramref name="index"/>.</summary>
    /// <param name="index">Zero-based position in the sequence.</param>
    /// <returns>
    /// <c>F(index)</c>, or <c>-1</c> when <paramref name="index"/> is negative or beyond what fits in a
    /// <see cref="long"/> (index 93 and above).
    /// </returns>
    long At(int index);

    /// <summary>The first <paramref name="count"/> Fibonacci numbers, starting at <c>F(0)</c>.</summary>
    /// <param name="count">How many numbers to return.</param>
    /// <returns>The sequence, or an empty list when <paramref name="count"/> is zero or negative.</returns>
    IReadOnlyList<long> Sequence(int count);

    /// <summary>Whether a value appears anywhere in the Fibonacci sequence.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns><see langword="true"/> when it does. Negative values are never Fibonacci numbers.</returns>
    bool IsFibonacci(long value);

    /// <summary>The sum of the first <paramref name="count"/> Fibonacci numbers.</summary>
    /// <param name="count">How many numbers to add.</param>
    /// <returns>The sum, or <c>0</c> when <paramref name="count"/> is zero or negative.</returns>
    long Sum(int count);
}
