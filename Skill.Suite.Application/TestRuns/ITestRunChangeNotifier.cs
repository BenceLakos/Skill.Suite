namespace Skill.Suite.Application.TestRuns;

/// <summary>
/// In-process pub/sub for test run state changes. Domain event handlers publish via
/// <see cref="Notify"/>; Blazor components subscribe to <see cref="Changed"/> to refresh
/// their view without polling. Singleton-scoped: shared across all circuits.
/// </summary>
public interface ITestRunChangeNotifier
{
    event Action<Guid> Changed;
    void Notify(Guid testRunId);
}
