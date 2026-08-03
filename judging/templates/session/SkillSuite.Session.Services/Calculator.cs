using SkillSuite.Session.Contracts;

namespace SkillSuite.Session.Services;

/// <summary>
/// The reference implementation — the answer key.
/// </summary>
/// <remarks>
/// Never enters the white-box image: <c>.dockerignore</c> excludes this folder's sources and keeps only the
/// csproj, so the competitor's own version can be grafted in its place at run time, and the build fails if a
/// source file slips through. It <i>is</i> baked into the black-box image, in source form, because Stryker has
/// to mutate it — which is why that image is private-registry-only.
/// <para>
/// It lives here so the session can be calibrated before anyone competes: the hidden suite must score 100%
/// against it. A session whose own answer key does not pass is not ready.
/// </para>
/// </remarks>
public sealed class Calculator : ICalculator
{
    public int Add(int left, int right) => left + right;

    public int Divide(int left, int right) => right == 0 ? 0 : left / right;

    public string Describe() => "reference calculator";
}
