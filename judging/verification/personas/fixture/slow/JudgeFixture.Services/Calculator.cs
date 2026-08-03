using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Persona "slow": compiles, but hangs. The image's own wall clock must cut it off, because the platform has
/// no judge timeout and its worker is serial — an unbounded run stalls every other competitor's submission.
/// </summary>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right)
    {
        // Never returns.
        while (true)
        {
            Thread.Sleep(1000);
        }
    }

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "slow";
}
