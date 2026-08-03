using Skill.Suite.TestLog.Protocol;
using Skill.Suite.TestLog.Tests.Support;
using Skill.Suite.TestLog.Xunit;
using Xunit;
using Xunit.Sdk;

namespace Skill.Suite.TestLog.Tests.Xunit;

/// <summary>
/// Drives the harness in-process and checks what it puts on the wire.
/// </summary>
public sealed class LoggedTestTests : EventFixture
{
    [Fact]
    public void MethodAspect_AppearsOnBothStartAndFinish()
    {
        RunProbe(MethodAnnotatedProbe.GradedVisibleTest, _ => { });

        var start = LineFor("start-unit-test");
        var finish = LineFor("finish-unit-test");

        Assert.Contains("\"aspect\":\"A1.1\"", start, StringComparison.Ordinal);
        Assert.Contains("\"aspectVisible\":true", start, StringComparison.Ordinal);
        Assert.Contains("\"aspect\":\"A1.1\"", finish, StringComparison.Ordinal);
        Assert.Contains("\"aspectVisible\":true", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodAspect_WithoutCompetitorVisible_IsGradedButHidden()
    {
        RunProbe(MethodAnnotatedProbe.GradedHiddenTest, _ => { });

        var finish = LineFor("finish-unit-test");
        Assert.Contains("\"aspect\":\"A1.2\"", finish, StringComparison.Ordinal);
        Assert.Contains("\"aspectVisible\":false", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void UnannotatedTest_CarriesNoAspectAtAll()
    {
        RunProbe(MethodAnnotatedProbe.UngradedTest, _ => { });

        Assert.DoesNotContain("aspect", LineFor("start-unit-test"), StringComparison.Ordinal);
        Assert.DoesNotContain("aspect", LineFor("finish-unit-test"), StringComparison.Ordinal);
    }

    [Fact]
    public void ClassAspect_AppliesToEveryTestInTheFixture()
    {
        using var scope = new FixtureScope<ClassAnnotatedProbe>();
        using (new ClassAnnotatedProbe(
                   new FakeTestOutputHelper($"N.ClassAnnotatedProbe.{ClassAnnotatedProbe.InheritsClassAspectTest}"),
                   scope)) { }

        var finish = LineFor("finish-unit-test");
        Assert.Contains("\"aspect\":\"B1.1\"", finish, StringComparison.Ordinal);
        Assert.Contains("\"aspectVisible\":true", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodAspect_ReplacesClassAspectRatherThanCombiningWithIt()
    {
        using var scope = new FixtureScope<ClassAnnotatedProbe>();
        using (new ClassAnnotatedProbe(
                   new FakeTestOutputHelper($"N.ClassAnnotatedProbe.{ClassAnnotatedProbe.OverridesClassAspectTest}"),
                   scope)) { }

        var finish = LineFor("finish-unit-test");

        // One test case belongs to exactly one aspect - the method's, not both.
        Assert.Contains("\"aspect\":\"C2.3\"", finish, StringComparison.Ordinal);
        Assert.DoesNotContain("B1.1", finish, StringComparison.Ordinal);
        Assert.Contains("\"aspectVisible\":false", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void TheoryDisplayName_StillResolvesTheMethodAspect()
    {
        // Display names for theories carry their arguments; every data row shares the method's aspect.
        RunProbe($"{MethodAnnotatedProbe.GradedVisibleTest}(value: 3)", _ => { });

        Assert.Contains("\"aspect\":\"A1.1\"", LineFor("finish-unit-test"), StringComparison.Ordinal);
    }

    [Fact]
    public void TestName_IsStrippedOfItsNamespaceAndClass()
    {
        RunProbe(MethodAnnotatedProbe.UngradedTest, _ => { });

        Assert.Contains(
            $"\"test\":\"{MethodAnnotatedProbe.UngradedTest}\"",
            LineFor("start-unit-test"),
            StringComparison.Ordinal);
    }

    [Fact]
    public void DisplayName_WhenHelperHasNoTestField_FallsBackToUnknown()
    {
        using var scope = new FixtureScope<MethodAnnotatedProbe>();
        using (new MethodAnnotatedProbe(new EmptyTestOutputHelper(), scope)) { }

        Assert.Contains("\"test\":\"unknown\"", LineFor("start-unit-test"), StringComparison.Ordinal);
    }

    [Fact]
    public void FailedAssertion_MarksTheTestFailedAndRecordsTheReason()
    {
        RunProbe(MethodAnnotatedProbe.GradedVisibleTest, probe => Assert.Throws<EqualException>(probe.Fail));

        var assertion = LineFor("assertion");
        var finish = LineFor("finish-unit-test");

        Assert.Contains("\"passed\":false", assertion, StringComparison.Ordinal);
        Assert.Contains("\"outcome\":\"failed\"", finish, StringComparison.Ordinal);
        Assert.DoesNotContain("\"error\":null", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceThrow_IsLoggedAsThrewAndFailsTheTest()
    {
        RunProbe(MethodAnnotatedProbe.GradedVisibleTest, probe =>
        {
            var service = new ThrowingProbeService().WithCallLogging<IProbeService>();
            Assert.Throws<InvalidOperationException>(() => probe.CallInto(service));
        });

        var call = LineFor("call");
        var finish = LineFor("finish-unit-test");

        // Non-empty threw is what makes the consumer mark the unit Errored.
        Assert.Contains("\"threw\":\"InvalidOperationException\"", call, StringComparison.Ordinal);
        Assert.Contains("\"outcome\":\"failed\"", finish, StringComparison.Ordinal);
    }

    [Fact]
    public void CleanRun_ReportsPassedWithNoError()
    {
        RunProbe(MethodAnnotatedProbe.GradedVisibleTest, probe =>
        {
            var service = new WellBehavedProbeService().WithCallLogging<IProbeService>();
            probe.CallInto(service);
        });

        var finish = LineFor("finish-unit-test");
        Assert.Contains("\"outcome\":\"passed\"", finish, StringComparison.Ordinal);
        // Null fields are omitted rather than written explicitly, so this asserts the parsed value.
        Assert.Null(Assert.IsType<FinishUnitTestEvent>(TestLogEventReader.Read(finish)).Error);
    }

    [Fact]
    public void Fixture_BracketsTheRunWithStartAndFinishEvents()
    {
        RunProbe(MethodAnnotatedProbe.UngradedTest, _ => { });

        var lines = NormalizedLines();
        Assert.Contains("\"event\":\"start-fixture\"", lines[0], StringComparison.Ordinal);
        Assert.Contains("\"event\":\"finish-fixture\"", lines[^1], StringComparison.Ordinal);
        Assert.Contains("\"fixture\":\"MethodAnnotatedProbe\"", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_TalliesTestsItRecorded()
    {
        using (var scope = new FixtureScope<MethodAnnotatedProbe>())
        {
            using (new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.UngradedTest), scope)) { }
            using (var failing = new MethodAnnotatedProbe(Helper(MethodAnnotatedProbe.GradedVisibleTest), scope))
            {
                Assert.Throws<EqualException>(failing.Fail);
            }
        }

        var finishFixture = LineFor("finish-fixture");
        Assert.Contains("\"testsRun\":2", finishFixture, StringComparison.Ordinal);
        Assert.Contains("\"testsPassed\":1", finishFixture, StringComparison.Ordinal);
        Assert.Contains("\"testsFailed\":1", finishFixture, StringComparison.Ordinal);
    }

    private static void RunProbe(string testName, Action<MethodAnnotatedProbe> body)
    {
        using var scope = new FixtureScope<MethodAnnotatedProbe>();
        using var probe = new MethodAnnotatedProbe(Helper(testName), scope);
        body(probe);
    }

    private static FakeTestOutputHelper Helper(string testName) =>
        new($"Skill.Suite.TestLog.Tests.Support.MethodAnnotatedProbe.{testName}");

    private static string LineFor(string eventName) =>
        NormalizedLines().Last(line => line.Contains($"\"event\":\"{eventName}\"", StringComparison.Ordinal));
}
