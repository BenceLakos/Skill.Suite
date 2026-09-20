# Skill.Suite.TestLog

Emits the JSON-Lines test-event stream that Skill.Suite parses into test-run results. No
test-framework dependency — a plain console app can produce results the platform displays.

Writing an xUnit suite? Use **`Skill.Suite.TestLog.Xunit`** instead; it wraps all of this and emits
the events for you.

## Quick start

```bash
dotnet new console -o my-judge && cd my-judge
dotnet add package Skill.Suite.TestLog
```

```csharp
using Skill.Suite.TestLog;

TestLogger.StartFixture("DemoTests");

TestLogger.StartUnitTest("DemoTests", "Compute_TwoPlusTwo_ReturnsFour", aspect: "A1.1", aspectCompetitorVisible: true);
TestLogger.Assertion("DemoTests", "Compute_TwoPlusTwo_ReturnsFour", AssertionKinds.Equal, 4, Compute(), passed: true);
TestLogger.FinishUnitTest("DemoTests", "Compute_TwoPlusTwo_ReturnsFour", TestLogOutcome.Passed, 12, null,
    aspect: "A1.1", aspectCompetitorVisible: true);

TestLogger.FinishFixture("DemoTests", testsRun: 1, testsPassed: 1, testsFailed: 0, durationMs: 12);

TestLogger.Emit(new ScoreEvent(MetricEvent.OverallPart, 0.83));
```

Run it with `LOG_DIRECTORY` pointing at a writable directory and the events land in
`$LOG_DIRECTORY/events.jsonl`; run it without and the identical lines go to stdout.

```bash
LOG_DIRECTORY=/tmp/x dotnet run && cat /tmp/x/events.jsonl
```

## Where the output goes

| `LOG_DIRECTORY` | Sink |
|---|---|
| set and writable | `$LOG_DIRECTORY/events.jsonl` |
| unset, empty, or unusable | `Console.Out` |

Resolved **once per process, on first use** — setting the variable later in the same process has no
effect. The file is opened truncating, so the first write of a process clears it. If something else
writes the same file it must append and must run after this process has exited.

## Events

| Method | Event |
|---|---|
| `StartFixture` / `FinishFixture` | `start-fixture` / `finish-fixture` |
| `StartUnitTest` / `FinishUnitTest` | `start-unit-test` / `finish-unit-test` |
| `Call` | `call` |
| `Assertion` | `assertion` |
| `Emit(new TestSummaryEvent(...))` and friends | `test-summary`, `coverage`, `mutation`, `score` |
| `MarkerError` | `marker-error` |

Every event is a record from the `Skill.Suite.TestLog.Protocol` namespace, and `Emit` takes any of them. The
convenience methods above exist only because a fixture name and a test name are what a caller usually has.

**These types are the contract.** The same sources are compiled into the Skill.Suite platform that reads the
stream, so a field exists in exactly one place — its record — and a rename cannot land on one side. Wire names
come from the camelCase naming policy (`TestsRun` becomes `testsRun`), not from per-member attributes. There is
no untyped payload dictionary either: an event the consumer could not parse is not constructible.

Rules worth knowing before you debug a missing result:

- **`start-unit-test` is mandatory.** `call`, `assertion` and `finish-unit-test` are dropped for a
  test the consumer never saw start.
- **Verdicts cannot be downgraded.** A `call` with a non-empty `threw` marks the test errored and an
  `assertion` with `passed:false` marks it failed; a later `outcome: "passed"` will not undo either.
- **`finish-fixture` counts are informational** — the consumer recomputes them from unit outcomes.
- **Metric events share one vocabulary.** `value` is the headline ratio the event exists to report — a pass
  rate, a coverage ratio, a kill rate, a composite quality — and the event kind fixes what it measures.
  `total` and `covered` are likewise shared, so `lines_covered` and `mutants_covered` are one name.
- **`score` needs a numeric `value` in 0..1** to drive the competitor-visible indicator. A string
  `"0.83"` is ignored.
- **`coverage` and `mutation` may carry a `fixture` instead of a `part`**, naming one test class measured on
  its own — the same simple class name `FixtureScope<T>` puts in `start-fixture`. `test-summary` and `score`
  never do: quality stays a part-level verdict. A consumer that does not know the field ignores it, so a newer
  judge image still works against an older platform.
- **Never leave `events.jsonl` empty.** The platform prefers the file whenever it exists, so an empty
  one yields zero results *and* suppresses the stdout fallback.

## Aspects

`[Aspect]` declares which marking-scheme aspect a test belongs to, replacing test-name prefix
conventions:

```csharp
[Aspect("C2.1", CompetitorVisible = true)]
```

One test case belongs to exactly one aspect — the attribute is not `AllowMultiple`, so that is a
compile error rather than a convention. An aspect can own any number of tests, and scores `yes` only
when all of them pass. `CompetitorVisible` opts the aspect into the score the competitor sees for
their own submission; grading happens either way.

`Skill.Suite.TestLog.Xunit` reads the attribute and populates the `aspect` / `aspect_visible` fields
automatically. Using this package directly, pass them to `StartUnitTest` / `FinishUnitTest` yourself.

## Thread safety

All methods are safe to call concurrently; writes are serialized and flushed per line. Nothing here
throws: a serialization failure degrades to a `log-error` line, and an unusable log directory falls
back to stdout.
