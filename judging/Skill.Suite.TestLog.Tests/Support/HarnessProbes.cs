using Skill.Suite.TestLog.Xunit;
using Xunit.Abstractions;

// Inside a namespace under Skill.Suite.TestLog, the simple name `TestLog` binds to the *namespace*
// (found by walking the enclosing chain) and shadows the harness type. Consumers normally never name
// the type - they use the inherited `Log` property - but anything that does needs this alias.
using HarnessTestLog = Skill.Suite.TestLog.Xunit.TestLog;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Test classes shaped exactly like a graded suite, instantiated directly so the harness can be
/// observed without spawning a second test run.
/// </summary>
/// <remarks>
/// These deliberately mirror the required consumer shape from the package README:
/// <c>sealed class XTests : LoggedTest&lt;XTests&gt;, IClassFixture&lt;FixtureScope&lt;XTests&gt;&gt;</c>.
/// </remarks>
internal sealed class MethodAnnotatedProbe(ITestOutputHelper output, FixtureScope<MethodAnnotatedProbe> scope)
    : LoggedTest<MethodAnnotatedProbe>(output, scope)
{
    internal const string GradedVisibleTest = nameof(Graded_AndVisible_Case);
    internal const string GradedHiddenTest = nameof(Graded_ButHidden_Case);
    internal const string UngradedTest = nameof(Ungraded_Case);

    [Aspect("A1.1", CompetitorVisible = true)]
    internal void Graded_AndVisible_Case() { }

    [Aspect("A1.2")]
    internal void Graded_ButHidden_Case() { }

    internal void Ungraded_Case() { }

    /// <summary>The protected per-test log, surfaced so tests can drive the assertion wrappers directly.</summary>
    internal HarnessTestLog Logger => Log;

    internal void Fail() => Log.AssertEqual(1, 2);

    internal void CallInto(IProbeService service) => service.Work();
}

/// <summary>Every test in this fixture inherits the class-level aspect unless it declares its own.</summary>
[Aspect("B1.1", CompetitorVisible = true)]
internal sealed class ClassAnnotatedProbe(ITestOutputHelper output, FixtureScope<ClassAnnotatedProbe> scope)
    : LoggedTest<ClassAnnotatedProbe>(output, scope)
{
    internal const string InheritsClassAspectTest = nameof(Inherits_ClassAspect_Case);
    internal const string OverridesClassAspectTest = nameof(Overrides_ClassAspect_Case);

    internal void Inherits_ClassAspect_Case() { }

    [Aspect("C2.3")]
    internal void Overrides_ClassAspect_Case() { }
}

/// <summary>A service the proxy can wrap.</summary>
internal interface IProbeService
{
    void Work();

    string Describe();
}

internal sealed class ThrowingProbeService : IProbeService
{
    internal const string Message = "the service exploded";

    public void Work() => throw new InvalidOperationException(Message);

    public string Describe() => "throwing";
}

internal sealed class NullReturningProbeService : IProbeService
{
    public void Work() { }

    // Declared non-nullable but returns null: the anti-gaming check must turn this into a throw.
    public string Describe() => null!;
}

internal sealed class WellBehavedProbeService : IProbeService
{
    public void Work() { }

    public string Describe() => "ok";
}
