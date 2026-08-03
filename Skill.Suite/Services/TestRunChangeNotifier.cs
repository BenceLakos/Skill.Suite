using Skill.Suite.Application.TestRuns;

namespace Skill.Suite.Services;

internal sealed class TestRunChangeNotifier : ITestRunChangeNotifier
{
    public event Action<Guid>? Changed;

    public void Notify(Guid testRunId) => Changed?.Invoke(testRunId);

    event Action<Guid> ITestRunChangeNotifier.Changed
    {
        add => Changed += value;
        remove => Changed -= value;
    }
}
