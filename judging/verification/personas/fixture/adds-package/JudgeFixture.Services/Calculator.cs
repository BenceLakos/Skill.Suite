using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// The reference implementation. Never baked into the judge image — it is the answer key, and each persona
/// under verification/personas/fixture/ supplies its own version of this folder instead.
/// </summary>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right) => left + right;

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "reference";
}
