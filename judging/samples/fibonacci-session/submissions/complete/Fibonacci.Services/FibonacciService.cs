using Fibonacci.Contracts;

namespace Fibonacci.Services;

/// <summary>A submission that implements the whole contract, edge cases included. Scores every aspect.</summary>
public sealed class FibonacciService : IFibonacci
{
    private const int MaxIndex = 92;
    private const long OutOfRange = -1;

    private static readonly long[] Cache = Build();

    public long At(int index) => index is < 0 or > MaxIndex ? OutOfRange : Cache[index];

    public IReadOnlyList<long> Sequence(int count) =>
        count <= 0 ? [] : Cache[..Math.Min(count, Cache.Length)];

    public bool IsFibonacci(long value) => value >= 0 && Array.BinarySearch(Cache, value) >= 0;

    public long Sum(int count)
    {
        if (count <= 0) return 0;

        long total = 0;
        foreach (var value in Sequence(count)) total += value;

        return total;
    }

    private static long[] Build()
    {
        var values = new long[MaxIndex + 1];
        values[1] = 1;
        for (var i = 2; i < values.Length; i++) values[i] = values[i - 1] + values[i - 2];

        return values;
    }
}
