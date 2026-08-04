namespace Skill.Suite.Domain.TestRuns;

public enum TestEventKind
{
    Call = 0,
    Assertion = 1,

    /// <summary>
    /// A diagnostic metric event (test-summary, coverage, mutation, score) emitted by
    /// the judgement image. Doesn't represent a pass/fail outcome — the payload (raw JSON)
    /// is carried on <see cref="TestEventRecord.Payload"/>.
    /// </summary>
    Metric = 2,

    /// <summary>
    /// A judge diagnostic explaining why a run could not complete — a build failure, a timeout, a missing
    /// folder. The text is carried on <see cref="TestEventRecord.Detail"/>.
    /// </summary>
    Error = 3,
}
