using Fibonacci.Contracts;

namespace Fibonacci.Services;

/// <summary>
/// The reference implementation — the answer key.
/// </summary>
/// <remarks>
/// Never shipped in the judge image: <c>.dockerignore</c> excludes this folder's sources and keeps only the
/// csproj, so the competitor's own version can be grafted in its place at run time. It lives here for
/// calibration, so the session can be proven to score 100% before anyone competes.
/// </remarks>
public sealed class FibonacciService : IFibonacci
{
    /// <summary>Highest index whose value still fits in a <see cref="long"/>.</summary>
    private const int MaxIndex = 92;

    private const long OutOfRange = -1;

    public long At(int index)
    {
        if (index < 0 || index > MaxIndex) return OutOfRange;

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
        if (count <= 0) return [];

        var values = new long[Math.Min(count, MaxIndex + 1)];
        for (var i = 0; i < values.Length; i++) values[i] = At(i);

        return values;
    }

    public bool IsFibonacci(long value)
    {
        if (value < 0) return false;

        for (var index = 0; index <= MaxIndex; index++)
        {
            var candidate = At(index);
            if (candidate == value) return true;
            if (candidate > value) return false;
        }

        return false;
    }

    public long Sum(int count)
    {
        if (count <= 0) return 0;

        long total = 0;
        foreach (var value in Sequence(count)) total += value;

        return total;
    }
}
