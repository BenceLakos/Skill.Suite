using Xunit.Abstractions;

namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Stands in for xUnit's <c>TestOutputHelper</c> so the harness can be driven in-process.
/// </summary>
/// <remarks>
/// <c>LoggedTest</c> discovers the running test by reflecting a private field named <c>test</c> off the
/// output helper and reading its <c>DisplayName</c> property. That is duck-typed, so this fake only has
/// to reproduce the shape — which also means these tests pin the reflection contract: if xUnit renames
/// the field, the real harness returns <c>"unknown"</c> and these tests keep passing, so
/// <see cref="LoggedTestTests.DisplayName_WhenHelperHasNoTestField_FallsBackToUnknown"/> covers the
/// other direction.
/// </remarks>
internal sealed class FakeTestOutputHelper(string displayName) : ITestOutputHelper
{
    // Name and shape matter: LoggedTest looks for a non-public instance field called exactly "test".
    private readonly FakeTest test = new(displayName);

    public void WriteLine(string message) { }

    public void WriteLine(string format, params object[] args) { }

    private sealed class FakeTest(string displayName)
    {
        public string DisplayName { get; } = displayName;
    }
}

/// <summary>An output helper with no <c>test</c> field at all, to exercise the fallback path.</summary>
internal sealed class EmptyTestOutputHelper : ITestOutputHelper
{
    public void WriteLine(string message) { }

    public void WriteLine(string format, params object[] args) { }
}
