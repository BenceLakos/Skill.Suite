namespace Skill.Suite.TestLog;

/// <summary>
/// Declares which marking-scheme aspect a test case belongs to.
/// </summary>
/// <remarks>
/// <para>
/// The relation is many tests to one aspect: an aspect groups any number of test cases, but a single
/// test case belongs to exactly one aspect, because that is the unit grading works in.
/// <see cref="AttributeUsageAttribute.AllowMultiple"/> is <c>false</c> so that rule is a compile
/// error rather than a convention. A test that would cover several aspects must be split into one
/// test per aspect.
/// </para>
/// <para>
/// Applied to a class, it is the default for every test case in that fixture; a method-level
/// attribute replaces it (the two are not combined).
/// </para>
/// <para>
/// This replaces test-name prefix conventions entirely. Test names should follow ordinary naming
/// (<c>Method_Scenario_ExpectedResult</c>); aspect membership is metadata, not spelling.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Aspect("C2.1", CompetitorVisible = true)]
/// [Fact]
/// public void Optimize_ZeroDemand_SplitsGreenEqually() =&gt; ...
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AspectAttribute(string id) : Attribute
{
    /// <summary>
    /// The marking-scheme aspect id, for example <c>"C2.1"</c>. Grading groups test cases by this
    /// value: an aspect scores <c>yes</c> when it has at least one test case and all of them pass.
    /// </summary>
    public string Id { get; } = id;

    /// <summary>
    /// Whether this aspect feeds the competitor-visible score. Opt-in: every annotated aspect is
    /// graded regardless, but only visible ones contribute to the progress indicator a competitor
    /// sees for their own submission.
    /// </summary>
    /// <remarks>
    /// This is the dial for how much feedback a submission leaks. Marking a handful of aspects
    /// visible gives a coarse but responsive signal; marking all of them makes an individual test
    /// flip almost never move the displayed bucket.
    /// </remarks>
    public bool CompetitorVisible { get; init; }
}
