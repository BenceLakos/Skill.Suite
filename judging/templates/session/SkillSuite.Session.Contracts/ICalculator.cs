namespace SkillSuite.Session.Contracts;

/// <summary>
/// The service a competitor implements. Replace this with the session's real contract.
/// </summary>
/// <remarks>
/// Two rules to carry over into whatever replaces it, because the harness enforces both:
/// <list type="bullet">
/// <item>
/// <b>Never throw.</b> Invalid input returns a documented sentinel. The call-logging proxy turns an escaped
/// exception into an errored test, so a throw fails the aspect just as surely as a wrong answer.
/// </item>
/// <item>
/// <b>Declare nullability honestly.</b> The proxy reads it off this interface and fails a call that returns
/// null from a non-nullable member, which is what stops a competitor stubbing everything to null.
/// </item>
/// </list>
/// Keep it pure and deterministic: the same input must give the same output, with no state between calls.
/// </remarks>
public interface ICalculator
{
    /// <summary>Adds two numbers.</summary>
    int Add(int left, int right);

    /// <summary>Divides, returning <c>0</c> when the divisor is <c>0</c>. Must never throw.</summary>
    int Divide(int left, int right);

    /// <summary>Describes the implementation. Non-nullable: returning null is a contract break.</summary>
    string Describe();
}
