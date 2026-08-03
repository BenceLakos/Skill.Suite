using Skill.Suite.TestLog.Tests.Support;
using Skill.Suite.TestLog.Xunit;
using Xunit;
using Xunit.Sdk;

// See HarnessProbes.cs: `TestLog` binds to the namespace, not the type, from inside this namespace.
using HarnessTestLog = Skill.Suite.TestLog.Xunit.TestLog;

namespace Skill.Suite.TestLog.Tests.Xunit;

/// <summary>
/// Covers the assertion wrappers: the event they emit, and the double-comparison overloads.
/// </summary>
public sealed class TestLogAssertionTests : EventFixture
{
    [Fact]
    public void AssertEqual_WithPrecision_ComparesDecimalPlacesNotTolerance()
    {
        // The trap this overload exists to close: with only (double, double, double) in scope the literal
        // 3 converts to a tolerance of 3.0, so a test reading as three-decimal-strict accepts +/-3.
        // With both overloads present the int literal binds to precision, and 1.004 != 1.000 at 3 dp.
        RunAssert(log => Assert.Throws<EqualException>(() => log.AssertEqual(1.0, 1.004, 3)));

        Assert.Contains("\"passed\":false", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AssertEqual_WithPrecision_PassesWhenRoundedValuesMatch()
    {
        RunAssert(log => log.AssertEqual(1.0, 1.0004, 3));

        Assert.Contains("\"passed\":true", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AssertEqual_WithTolerance_AcceptsValuesInsideIt()
    {
        RunAssert(log => log.AssertEqual(0.3333, 0.33334, 0.001));

        Assert.Contains("\"passed\":true", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AssertEqual_WithTolerance_RejectsValuesOutsideIt()
    {
        RunAssert(log => Assert.Throws<EqualException>(() => log.AssertEqual(0.3333, 0.34, 0.001)));

        Assert.Contains("\"passed\":false", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AllThreeEqualOverloads_ShareTheSameAssertionKind()
    {
        // Protocol stability: the UI groups on `kind`, and expected/actual already carry the detail.
        RunAssert(log =>
        {
            log.AssertEqual(1, 1);
            log.AssertEqual(1.0, 1.0, 0.1);
            log.AssertEqual(1.0, 1.0, 3);
        });

        var assertions = NormalizedLines()
            .Where(line => line.Contains("\"event\":\"assertion\"", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(3, assertions.Length);
        Assert.All(assertions, line => Assert.Contains("\"kind\":\"equal\"", line, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("not-equal")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("not-null")]
    [InlineData("same")]
    [InlineData("not-same")]
    [InlineData("empty")]
    [InlineData("not-empty")]
    [InlineData("single")]
    [InlineData("contains")]
    [InlineData("throws")]
    public void EveryWrapper_EmitsItsDocumentedKind(string kind)
    {
        var subject = new object();
        var other = new object();

        RunAssert(log =>
        {
            switch (kind)
            {
                case "not-equal": log.AssertNotEqual(1, 2); break;
                case "true": log.AssertTrue(true); break;
                case "false": log.AssertFalse(false); break;
                case "null": log.AssertNull(null); break;
                case "not-null": log.AssertNotNull(subject); break;
                case "same": log.AssertSame(subject, subject); break;
                case "not-same": log.AssertNotSame(subject, other); break;
                case "empty": log.AssertEmpty(Array.Empty<int>()); break;
                case "not-empty": log.AssertNotEmpty(new[] { 1 }); break;
                case "single": log.AssertSingle(new[] { 1 }); break;
                case "contains": log.AssertContains("ell", "hello"); break;
                case "throws": log.AssertThrows<InvalidOperationException>(() => throw new InvalidOperationException()); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        });

        Assert.Contains($"\"kind\":\"{kind}\"", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AssertSingle_ReturnsTheElementSoItCanBeAssertedFurther()
    {
        var captured = 0;
        RunAssert(log => captured = log.AssertSingle(new[] { 42 }));

        Assert.Equal(42, captured);
    }

    [Fact]
    public void AssertThrows_AcceptsADerivedExceptionType()
    {
        RunAssert(log => log.AssertThrows<ArgumentException>(() => throw new ArgumentNullException("p")));

        Assert.Contains("\"passed\":true", LineFor("assertion"), StringComparison.Ordinal);
    }

    [Fact]
    public void AssertThrows_WhenNothingThrows_FailsWithTheNoExceptionSentinel()
    {
        RunAssert(log => Assert.Throws<XunitException>(() => log.AssertThrows<InvalidOperationException>(() => { })));

        var assertion = LineFor("assertion");
        Assert.Contains("\"actual\":\"<no exception>\"", assertion, StringComparison.Ordinal);
        Assert.Contains("\"passed\":false", assertion, StringComparison.Ordinal);
    }

    [Fact]
    public void ReachingAnAssertion_ClearsAnEarlierHandledServiceThrow()
    {
        // A service that throws and is caught by the test body is legitimate (that is what AssertThrows
        // is for), so any assertion reached afterwards consumes the pending throw and the test can pass.
        using (var scope = new FixtureScope<MethodAnnotatedProbe>())
        using (var probe = new MethodAnnotatedProbe(Helper(), scope))
        {
            var service = new ThrowingProbeService().WithCallLogging<IProbeService>();
            Assert.Throws<InvalidOperationException>(service.Work);
            probe.Logger.AssertTrue(true);
        }

        Assert.Contains("\"outcome\":\"passed\"", LineFor("finish-unit-test"), StringComparison.Ordinal);
    }

    private static void RunAssert(Action<HarnessTestLog> body)
    {
        using var scope = new FixtureScope<MethodAnnotatedProbe>();
        using var probe = new MethodAnnotatedProbe(Helper(), scope);
        body(probe.Logger);
    }

    private static FakeTestOutputHelper Helper() =>
        new($"N.MethodAnnotatedProbe.{MethodAnnotatedProbe.GradedVisibleTest}");

    private static string LineFor(string eventName) =>
        NormalizedLines().Last(line => line.Contains($"\"event\":\"{eventName}\"", StringComparison.Ordinal));
}
