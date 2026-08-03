using Fibonacci.Contracts;

namespace Fibonacci.Services;
/// <summary>
/// Your implementation. Every member throws until you write it.
/// </summary>
/// <remarks>
/// Keep this folder's name and its csproj: the judge replaces the whole folder with yours. Do not add
/// package references — the judge restores offline and a new reference fails the run. The contract
/// interface documents what each member has to satisfy, including that it must never throw once done.
/// </remarks>
public sealed class FibonacciService : IFibonacci
{
    /// <summary>Highest index whose value still fits in a <see cref = "long "/>.</summary>
    private const int MaxIndex = 92;
    private const long OutOfRange = -1;
    public long At(int index) => throw new NotImplementedException();
    public IReadOnlyList<long> Sequence(int count) => throw new NotImplementedException();
    public bool IsFibonacci(long value) => throw new NotImplementedException();
    public long Sum(int count) => throw new NotImplementedException();
}
