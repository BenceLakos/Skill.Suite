namespace Skill.Suite.Domain.TestRuns;

public enum TestRunStatus
{
    Pending = 0,
    Cloning = 1,
    Running = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
}
