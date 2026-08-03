using Fibonacci.Contracts;

namespace Fibonacci.Services;

/// <summary>
/// A realistic mid-competition submission: the common cases are right, two edge cases are not.
/// </summary>
/// <remarks>
/// Used to demonstrate partial credit end to end. Expect A1.3, A1.4 and A4.1 to fail outright, and A3.2 to
/// fail on its negative case only — one aspect with a mix of green and red unit results, which is the
/// interesting row in the UI. Three of the eight competitor-visible aspects fail, so the bar the competitor
/// sees lands in the middle bucket rather than at either extreme.
/// </remarks>
public sealed class FibonacciService : IFibonacci
{
    public long At(int index)
    {
        // Missing: negative indices should report -1, and index 93 and up overflow rather than reporting -1.
        long previous = 0;
        long current = 1;

        for (var i = 0; i < index; i++)
        {
            (previous, current) = (current, previous + current);
        }

        return previous;
    }

    public IReadOnlyList<long> Sequence(int count)
    {
        var values = new List<long>();
        for (var i = 0; i < count; i++) values.Add(At(i));

        return values;
    }

    public bool IsFibonacci(long value)
    {
        // The classic identity: n is Fibonacci iff 5n^2+4 or 5n^2-4 is a perfect square. Correct for
        // non-negative n, and wrong for negatives, because squaring throws the sign away.
        return IsPerfectSquare(5 * value * value + 4) || IsPerfectSquare(5 * value * value - 4);
    }

    public long Sum(int count)
    {
        // Off by one: the sequence starts at F(0), not F(1), so this adds one term too far along.
        long total = 0;
        for (var i = 1; i <= count; i++) total += At(i);

        return total;
    }

    private static bool IsPerfectSquare(long value)
    {
        if (value < 0) return false;

        var root = (long)Math.Sqrt(value);

        return root * root == value;
    }
}
