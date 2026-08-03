using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Persona "broken-code": does not compile. Must be reported Failed with a marker-error naming the compile
/// error — never Completed-with-no-results, which is what happens when the build is not a separate step,
/// because `dotnet test` returns the same exit code for a compile error as for a failing test.
/// </summary>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right) => left + right

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "broken";
}
