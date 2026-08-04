using JudgeFixture.Contracts;

namespace JudgeFixture.Services;

/// <summary>
/// Attack persona: the implementation is deliberately wrong, and the cheating is in the project file.
/// </summary>
/// <remarks>
/// Every method returns a value the hidden suite rejects, so an honest run of this submission fails every
/// aspect. If it ever reports a pass, the MSBuild target in the csproj executed — which is the whole point of
/// the persona.
/// </remarks>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right) => -1;

    public int Divide(int left, int right) => -1;

    public string Describe() => null!;
}
