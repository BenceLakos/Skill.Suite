using JudgeFixture.Contracts;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace JudgeFixture.UnitTests;

/// <summary>
/// Persona "weak": the suite that games coverage. Every method is called, so line coverage is as high as the
/// reference suite's, but nothing meaningful is asserted — so almost every injected fault survives.
/// </summary>
/// <remarks>
/// This is the persona that justifies mutation testing at all. Against coverage alone this suite is
/// indistinguishable from a good one; the mutation score is the only signal that separates "ran the code"
/// from "checked the code". If this scores close to the reference suite, the scoring constants are wrong.
/// </remarks>
public sealed class CalculatorTests : LoggedTest<CalculatorTests>, IClassFixture<FixtureScope<CalculatorTests>>
{
    private readonly ICalculator _svc = ServiceResolver.Resolve<ICalculator>().WithCallLogging();

    public CalculatorTests(ITestOutputHelper output, FixtureScope<CalculatorTests> scope)
        : base(output, scope) { }

    [Fact]
    public void Add_IsCallable()
    {
        // Calls it, then asserts something that holds for any implementation.
        var result = _svc.Add(2, 3);
        Log.AssertTrue(result == result);
    }

    [Fact]
    public void Divide_IsCallable()
    {
        var result = _svc.Divide(9, 3);
        Log.AssertTrue(result >= int.MinValue);
    }

    [Fact]
    public void Divide_ByZero_IsCallable()
    {
        var result = _svc.Divide(9, 0);
        Log.AssertTrue(result >= int.MinValue);
    }

    [Fact]
    public void Describe_IsCallable() => Log.AssertNotNull(_svc.Describe());
}
