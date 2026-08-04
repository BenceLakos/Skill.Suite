using Skill.Suite.Domain.Common;
using Skill.Suite.Domain.TestRuns.Events;

namespace Skill.Suite.Domain.TestRuns;

public sealed class TestRun : AuditableEntity<Guid>
{
    /// <summary>
    /// Name of the synthetic unit the parser places metric events into. One per fixture
    /// (i.e. one per `part`), so a fixture's "quality" unit accumulates every metric
    /// event the judge emits for that part (coverage / mutation / score / test-summary).
    /// </summary>
    public const string QualityUnitName = "quality";

    /// <summary>
    /// Fixture that collects judge diagnostics. Hyphenated so it can never collide with a C# type name,
    /// and therefore never with a real test fixture.
    /// </summary>
    public const string DiagnosticsFixtureName = "judge-diagnostics";

    /// <summary>Synthetic unit inside <see cref="DiagnosticsFixtureName"/> holding the diagnostic records.</summary>
    public const string DiagnosticsUnitName = "marker-error";

    private TestRun() { }

    public Guid SessionId { get; private set; }
    public Guid? CompetitorId { get; private set; }

    public string RepositoryUrl { get; private set; } = string.Empty;
    public string? RepositoryName { get; private set; }
    public string? Branch { get; private set; }
    public string? CommitSha { get; private set; }

    public string FolderName { get; private set; } = string.Empty;
    public string JudgementImage { get; private set; } = string.Empty;
    public TestRunStatus Status { get; private set; }

    public DateTime? StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string? FailureReason { get; private set; }

    public List<TestFixtureResult> Fixtures { get; private set; } = new();

    public static TestRun Create(
        Guid sessionId,
        Guid? competitorId,
        string repositoryUrl,
        string? repositoryName,
        string? branch,
        string? commitSha,
        string folderName,
        string judgementImage)
    {
        var run = new TestRun
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            CompetitorId = competitorId,
            RepositoryUrl = repositoryUrl.Trim(),
            RepositoryName = string.IsNullOrWhiteSpace(repositoryName) ? null : repositoryName.Trim(),
            Branch = string.IsNullOrWhiteSpace(branch) ? null : branch.Trim(),
            CommitSha = string.IsNullOrWhiteSpace(commitSha) ? null : commitSha.Trim(),
            FolderName = folderName.Trim(),
            JudgementImage = judgementImage.Trim(),
            Status = TestRunStatus.Pending,
        };
        run.RaiseDomainEvent(new TestRunCreatedEvent(run.Id));
        return run;
    }

    public void MarkCloning()
    {
        Status = TestRunStatus.Cloning;
        RaiseDomainEvent(new TestRunStatusChangedEvent(Id, Status));
    }

    public void MarkRunning(DateTime startedAt)
    {
        Status = TestRunStatus.Running;
        StartedAt = startedAt;
        RaiseDomainEvent(new TestRunStatusChangedEvent(Id, Status));
    }

    public void MarkCompleted(DateTime finishedAt)
    {
        Status = TestRunStatus.Completed;
        FinishedAt = finishedAt;
        // A completed run has no failure reason by definition. Clearing it matters on the recovery path: a run
        // that failed with "produced no test results" and is later promoted by reprocessing its log would
        // otherwise read Completed while still displaying the failure that reprocessing just disproved.
        FailureReason = null;
        RaiseDomainEvent(new TestRunCompletedEvent(Id));
    }

    public void MarkFailed(string reason, DateTime finishedAt)
    {
        Status = TestRunStatus.Failed;
        FailureReason = reason;
        FinishedAt = finishedAt;
        RaiseDomainEvent(new TestRunFailedEvent(Id, reason));
    }

    public void MarkCancelled(string reason, DateTime finishedAt)
    {
        Status = TestRunStatus.Cancelled;
        FailureReason = reason;
        FinishedAt = finishedAt;
        RaiseDomainEvent(new TestRunFailedEvent(Id, reason));
    }

    public TestFixtureResult StartFixture(string name, DateTime startedAt) =>
        StartFixture(name, startedAt, TestFixtureKind.Tests);

    private TestFixtureResult StartFixture(string name, DateTime startedAt, TestFixtureKind kind)
    {
        var fixture = TestFixtureResult.Start(Id, name, startedAt, kind);
        Fixtures.Add(fixture);
        return fixture;
    }

    /// <summary>
    /// Finds a fixture of the given kind. Scoping by kind is what keeps a metric part named after a real
    /// test class from attaching its events to that class and overwriting its quality.
    /// </summary>
    private TestFixtureResult? FindFixture(string name, TestFixtureKind kind) =>
        Fixtures.LastOrDefault(f => f.Kind == kind && f.Name == TestRunLimits.TruncateName(name));

    public void FinishFixture(string name, int testsRun, int testsPassed, int testsFailed, long durationMs, DateTime finishedAt)
    {
        var fixture = FindFixture(name, TestFixtureKind.Tests);
        fixture?.Finish(testsRun, testsPassed, testsFailed, durationMs, finishedAt);
    }

    public UnitTestResult StartUnitTest(
        string fixtureName, string testName, DateTime startedAt,
        string? aspect = null, bool aspectCompetitorVisible = false)
    {
        var fixture = FindFixture(fixtureName, TestFixtureKind.Tests)
                      ?? StartFixture(fixtureName, startedAt, TestFixtureKind.Tests);
        return fixture.StartUnitTest(testName, startedAt, aspect, aspectCompetitorVisible);
    }

    public void FinishUnitTest(
        string fixtureName, string testName, TestOutcome outcome, long durationMs, string? error,
        DateTime finishedAt, string? aspect = null, bool aspectCompetitorVisible = false)
    {
        var fixture = FindFixture(fixtureName, TestFixtureKind.Tests);
        var test = fixture?.FindUnitTest(testName);
        if (test is null) return;

        test.SetAspect(aspect, aspectCompetitorVisible);
        test.Finish(outcome, durationMs, error, finishedAt);
    }

    public void AppendUnitTestEvent(string fixtureName, string testName, TestEventRecord record)
    {
        var fixture = FindFixture(fixtureName, TestFixtureKind.Tests);
        var test = fixture?.FindUnitTest(testName);
        test?.AppendEvent(record);
    }

    /// <summary>
    /// Records a judge diagnostic: why the run could not complete, as opposed to how a submission scored.
    /// </summary>
    /// <remarks>
    /// Held in a fixture rather than a column on the run for two reasons: it reuses the existing event
    /// plumbing, and it is automatically correct under log reprocessing, which deletes and rebuilds all
    /// fixtures — a run-level field would have to be reset explicitly, and would be missed.
    /// </remarks>
    public void RecordDiagnostic(string detail, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(detail)) return;

        var fixture = FindFixture(DiagnosticsFixtureName, TestFixtureKind.Diagnostics)
                      ?? StartFixture(DiagnosticsFixtureName, timestamp, TestFixtureKind.Diagnostics);
        var unit = fixture.FindUnitTest(DiagnosticsUnitName)
                   ?? fixture.StartUnitTest(DiagnosticsUnitName, timestamp);

        unit.AppendEvent(new TestEventRecord(
            Kind: TestEventKind.Error,
            Timestamp: timestamp,
            Target: null,
            Arguments: null,
            Returned: null,
            Threw: null,
            AssertionKind: null,
            Expected: null,
            Actual: null,
            Passed: null,
            Payload: null,
            Detail: TestRunLimits.TruncateDetail(detail)));
    }

    /// <summary>
    /// Records a metric event (test-summary, coverage, mutation, score) under the part
    /// it belongs to. The fixture is the part name; the unit is the single per-fixture
    /// "quality" unit. Each call appends a <see cref="TestEventKind.Metric"/> record on
    /// that unit, the same way Call / Assertion records hang off a regular test — so
    /// every metric the judge emits for a part lives in one place with its full payload.
    /// <paramref name="eventType"/> is stored on the record's <c>Target</c> field so the
    /// UI can group / label without re-parsing the JSON payload.
    /// Part names are data-driven, declared per session in the judge's marking map.
    /// </summary>
    public void RecordMetric(string partName, string eventType, DateTime timestamp, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(partName) || string.IsNullOrWhiteSpace(eventType))
            return;

        var fixture = FindFixture(partName, TestFixtureKind.Metrics)
                      ?? StartFixture(partName, timestamp, TestFixtureKind.Metrics);
        var unit = fixture.FindUnitTest(QualityUnitName) ?? fixture.StartUnitTest(QualityUnitName, timestamp);

        unit.AppendEvent(new TestEventRecord(
            Kind: TestEventKind.Metric,
            Timestamp: timestamp,
            Target: eventType,
            Arguments: null,
            Returned: null,
            Threw: null,
            AssertionKind: null,
            Expected: null,
            Actual: null,
            Passed: null,
            Payload: payloadJson));
    }

    /// <summary>
    /// Stores the part-level quality (0..1) extracted from a <c>score</c> metric event.
    /// Call after RecordMetric so the fixture is guaranteed to exist.
    /// </summary>
    public void SetFixtureQuality(string partName, double quality)
    {
        var fixture = FindFixture(partName, TestFixtureKind.Metrics);
        fixture?.SetQuality(quality);
    }
}
