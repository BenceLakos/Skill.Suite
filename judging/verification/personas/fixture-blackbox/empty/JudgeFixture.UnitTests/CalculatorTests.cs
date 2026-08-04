using JudgeFixture.Contracts;
using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

namespace JudgeFixture.UnitTests;

/// <summary>
/// Persona "empty": the suite a competitor who wrote nothing would submit. It has to compile and run - the
/// point is that it exercises almost none of the implementation and kills almost no mutants, so it should
/// score near zero rather than failing the run.
/// </summary>
public sealed class CalculatorTests : LoggedTest<CalculatorTests>, IClassFixture<FixtureScope<CalculatorTests>>
{
    private readonly ICalculator _svc = ServiceResolver.Resolve<ICalculator>().WithCallLogging();

    public CalculatorTests(ITestOutputHelper output, FixtureScope<CalculatorTests> scope)
        : base(output, scope) { }

    [Fact]
    public void Service_Resolves() => Log.AssertNotNull(_svc);
}
