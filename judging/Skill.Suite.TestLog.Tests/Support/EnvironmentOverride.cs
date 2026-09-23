namespace Skill.Suite.TestLog.Tests.Support;

/// <summary>
/// Sets an environment variable for the body of a test and puts back whatever was there.
/// </summary>
/// <remarks>
/// The harness reads its per-call budget from the real environment on every call rather than through a test
/// seam, so these tests exercise the same code path a judge container does. Parallelization is off for the
/// whole assembly (<c>AssemblyInfo.cs</c>), which is what makes process-wide mutation safe here.
/// </remarks>
internal sealed class EnvironmentOverride : IDisposable
{
    private readonly string _name;
    private readonly string? _previous;

    internal EnvironmentOverride(string name, string? value)
    {
        _name = name;
        _previous = Environment.GetEnvironmentVariable(name);
        Environment.SetEnvironmentVariable(name, value);
    }

    public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
}
