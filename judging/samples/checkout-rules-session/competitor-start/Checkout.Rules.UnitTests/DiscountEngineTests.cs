using Checkout.Rules.Contracts;
using Skill.Suite.TestLog;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace Checkout.Rules.UnitTests;
/// <summary>
/// Your test suite. The implementation is hidden — write tests from the contract interface's documented
/// rules alone.
/// </summary>
/// <remarks>
/// You are scored on two things, and neither is how many tests you write:
/// <list type="number">
/// <item><b>Coverage</b> — how much of the hidden implementation your suite exercises.</item>
/// <item><b>Mutation kill rate</b> — how many deliberately injected faults your suite catches. This is
/// the one that separates running the code from checking it: a test that calls a method and asserts the
/// result is a number adds coverage and kills nothing.</item>
/// </list>
/// Keep the class shape below — derive from <c>LoggedTest&lt;TSelf&gt;</c>, take the fixture, resolve
/// through <c>ServiceResolver</c>, and assert through <c>Log</c>. A bare <c>Assert</c> still fails the
/// test but emits no event, so your result arrives with nothing explaining it. Keep this folder's name
/// and its csproj, and do not add package references — the judge restores offline and a new reference
/// fails the run.
/// <para>
/// Boundaries are where the faults are. For every documented threshold, test the value at it and the
/// value just below it.
/// </para>
/// </remarks>
public sealed class DiscountEngineTests : LoggedTest<DiscountEngineTests>, IClassFixture<FixtureScope<DiscountEngineTests>>
{
    private readonly IDiscountEngine _svc = ServiceResolver.Resolve<IDiscountEngine>().WithCallLogging();
    public DiscountEngineTests(ITestOutputHelper output, FixtureScope<DiscountEngineTests> scope) : base(output, scope)
    {
    }
// Write your tests here. This example shows the required shape; it is commented out so the project
// you received compiles with zero tests.
//
// [Aspect("C1", CompetitorVisible = true)]
// [Fact]
// public void SomeRule_AtItsBoundary_BehavesAsDocumented() =>
//     Log.AssertEqual(expected, _service.SomeMethod(input));
//
// [Aspect("C2", CompetitorVisible = true)]
// [Theory]
// [InlineData(9, 180)]
// [InlineData(10, 190)]
// public void SomeRule_OnEachSideOfTheThreshold(int input, decimal expected) =>
//     Log.AssertEqual(expected, _service.SomeMethod(input));
}
