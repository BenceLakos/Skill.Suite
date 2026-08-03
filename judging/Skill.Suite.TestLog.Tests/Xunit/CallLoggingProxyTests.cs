using Skill.Suite.TestLog.Protocol;
using Skill.Suite.TestLog.Tests.Support;
using Skill.Suite.TestLog.Xunit;
using Xunit;

namespace Skill.Suite.TestLog.Tests.Xunit;

/// <summary>
/// Covers the proxy's logging and its anti-gaming rules.
/// </summary>
public sealed class CallLoggingProxyTests : EventFixture
{
    [Fact]
    public void NullReturnFromANonNullableMember_IsTreatedAsAThrow()
    {
        // Anti-gaming: a service that "implements" everything by returning null would otherwise make the
        // test body NRE somewhere later, where nothing attributes the failure to the service.
        var ex = RunWithProbe(service => Assert.Throws<InvalidOperationException>(() => service.Describe()));

        Assert.Contains("returned null", ex.Message, StringComparison.Ordinal);
        Assert.Contains("non-nullable", ex.Message, StringComparison.Ordinal);
        Assert.Contains("\"threw\":\"InvalidOperationException\"", LineFor("call"), StringComparison.Ordinal);
    }

    [Fact]
    public void ServiceThrow_PreservesTheOriginalStackTrace()
    {
        InvalidOperationException? captured = null;

        using (var scope = new FixtureScope<MethodAnnotatedProbe>())
        using (new MethodAnnotatedProbe(Helper(), scope))
        {
            var service = new ThrowingProbeService().WithCallLogging<IProbeService>();
            captured = Assert.Throws<InvalidOperationException>(service.Work);
        }

        // Rethrown via ExceptionDispatchInfo, so the frame that actually threw is still in the trace.
        // A bare `throw thrown;` would have replaced it with the proxy's own frame.
        Assert.Equal(ThrowingProbeService.Message, captured.Message);
        Assert.Contains(nameof(ThrowingProbeService), captured.StackTrace ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public void CallsAreLoggedWithInterfaceNameAndArguments()
    {
        using (var scope = new FixtureScope<MethodAnnotatedProbe>())
        using (new MethodAnnotatedProbe(Helper(), scope))
        {
            var service = new WellBehavedProbeService().WithCallLogging<IProbeService>();
            service.Describe();
        }

        var evt = Assert.IsType<CallEvent>(TestLogEventReader.Read(LineFor("call")));

        // The target is the contract interface, not the implementing type - competitors' class names
        // must not leak into the event stream.
        Assert.Equal("IProbeService.Describe", evt.Target);
        Assert.Empty(evt.Arguments!);
        Assert.Equal("ok", evt.Returned!.ToString());
        Assert.Null(evt.Threw);
    }

    [Fact]
    public void WithoutATestContext_TheCallStillRunsButNothingIsLogged()
    {
        // Field initializers in a derived test class run before the LoggedTest constructor, so services
        // are routinely wrapped while TestLog.Current is still null. That must not break the call.
        var service = new WellBehavedProbeService().WithCallLogging<IProbeService>();

        Assert.Equal("ok", service.Describe());
        Assert.Empty(NormalizedLines());
    }

    [Fact]
    public void WrappingANonInterface_IsRejectedImmediately()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CallLoggingProxy<WellBehavedProbeService>.Wrap(new WellBehavedProbeService()));

        Assert.Contains("must be an interface", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void WrappingNull_IsRejectedImmediately()
    {
        Assert.Throws<ArgumentNullException>(() => CallLoggingProxy<IProbeService>.Wrap(null!));
    }

    private static InvalidOperationException RunWithProbe(Func<IProbeService, InvalidOperationException> body)
    {
        using var scope = new FixtureScope<MethodAnnotatedProbe>();
        using var probe = new MethodAnnotatedProbe(Helper(), scope);
        return body(new NullReturningProbeService().WithCallLogging<IProbeService>());
    }

    private static FakeTestOutputHelper Helper() =>
        new($"N.MethodAnnotatedProbe.{MethodAnnotatedProbe.GradedVisibleTest}");

    private static string LineFor(string eventName) =>
        NormalizedLines().Last(line => line.Contains($"\"event\":\"{eventName}\"", StringComparison.Ordinal));
}
