namespace JudgeFixture.Contracts;

/// <summary>
/// The contract a submission implements. Deliberately tiny — this module exists to exercise the judge
/// pipeline, not to be an interesting problem.
/// </summary>
public interface ICalculator
{
    /// <summary>Adds two numbers.</summary>
    int Add(int left, int right);

    /// <summary>Divides, returning 0 when the divisor is 0. Must never throw.</summary>
    int Divide(int left, int right);

    /// <summary>Describes the implementation. Non-nullable: returning null is a contract break.</summary>
    string Describe();
}
