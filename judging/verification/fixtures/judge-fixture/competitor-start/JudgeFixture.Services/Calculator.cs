using JudgeFixture.Contracts;

namespace JudgeFixture.Services;
/// <summary>
/// Your implementation. Every member throws until you write it.
/// </summary>
/// <remarks>
/// Keep this folder's name and its csproj: the judge replaces the whole folder with yours. Do not add
/// package references — the judge restores offline and a new reference fails the run. The contract
/// interface documents what each member has to satisfy, including that it must never throw once done.
/// </remarks>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right) => throw new NotImplementedException();
    public int Divide(int left, int right) => throw new NotImplementedException();
    public string Describe() => throw new NotImplementedException();
}
