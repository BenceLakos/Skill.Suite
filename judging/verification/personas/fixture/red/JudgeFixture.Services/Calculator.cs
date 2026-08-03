using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Persona "red": compiles, but gets two of the five aspects wrong. The run must still be Completed with
/// partial results — red tests are a result, not a failed run, which is what makes partial credit possible.
/// </summary>
public sealed class Calculator : ICalculator
{
    // Wrong: subtracts. Fails F1.1 and F1.2.
    public int Add(int left, int right) => left - right;

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "red";
}
