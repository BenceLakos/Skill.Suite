# Skill.Suite.TestLog.Xunit

The xUnit harness graded Skill Suite test suites are written against. It emits the event stream
(`Skill.Suite.TestLog`) automatically: fixture and test lifecycle, every call into the system under
test, every assertion, and the reconstructed verdict.

## Required consumer shape

Every graded test class must look like this. The harness reconstructs verdicts from three independent
signals, and each part below is load-bearing:

```csharp
public sealed class SignalOptimizerTests
    : LoggedTest<SignalOptimizerTests>, IClassFixture<FixtureScope<SignalOptimizerTests>>
{
    private readonly ISignalOptimizer _svc = ServiceResolver.Resolve<ISignalOptimizer>().WithCallLogging();

    public SignalOptimizerTests(ITestOutputHelper output, FixtureScope<SignalOptimizerTests> scope)
        : base(output, scope) { }

    [Aspect("C2.3", CompetitorVisible = true)]
    [Fact]
    public void Optimize_ZeroDemand_SplitsGreenEqually() =>
        Log.AssertEqual("P1:30 P2:30", Greens(_svc.Optimize(program, demand)));
}
```

| Requirement | Why |
|---|---|
| Inherit `LoggedTest<TSelf>` with `TSelf` = the class itself | Names the fixture, brackets the test, resolves the aspect |
| Add `IClassFixture<FixtureScope<TSelf>>` | Emits `start-fixture` / `finish-fixture` |
| Resolve services via `ServiceResolver.Resolve<T>().WithCallLogging()` | Finds the competitor's implementation and logs every call |
| Assert **only** through `Log.Assert*` | An assertion the harness did not see is invisible to grading |

Assert through `Log`, never `Assert` directly: a bare `Assert.Equal` still fails the test in xUnit, but
emits no `assertion` event, so the run shows a red row with nothing explaining it.

## Aspects

`[Aspect]` replaces test-name prefix conventions — name tests normally
(`Method_Scenario_ExpectedResult`).

```csharp
[Aspect("C2.3", CompetitorVisible = true)]   // on a method: this test case
[Aspect("C2.3")]                              // on the class: default for every test in it
```

- **One test case, one aspect.** The attribute is not `AllowMultiple`, so two on one method is a compile
  error. A test that would cover three aspects must be split into three tests — which is what grading
  needs anyway.
- **An aspect owns any number of tests** and scores `yes` only when *all* of them pass.
- **A method-level attribute replaces a class-level one**, it does not combine with it.
- **Theories** share their method's aspect across every data row.
- **`CompetitorVisible`** opts the aspect into the score the competitor sees for their own submission.
  Grading happens either way. Keep an aspect's tests inside one fixture: the competitor-facing rollup is
  computed per fixture, so an aspect split across two of them is displayed as two partial rollups (the
  CIS report, which reads the whole event stream, still grades it as one).
- **No attribute** means ungraded: the test runs and is logged, but no aspect claims it.

## What you get

| Type | Purpose |
|---|---|
| `LoggedTest<TSelf>` | Base class: per-test lifecycle, verdict reconstruction, `Log` |
| `FixtureScope<T>` | Class fixture: fixture lifecycle and tallies |
| `TestLog` | The assert wrappers, reached through the inherited `Log` property |
| `CallLoggingProxy<T>` / `.WithCallLogging()` | Logs every interface call |
| `ServiceResolver` | Reflection-based resolution of the implementation under test |

### Assertion wrappers

`AssertEqual`, `AssertNotEqual`, `AssertTrue`, `AssertFalse`, `AssertNull`, `AssertNotNull`,
`AssertSame`, `AssertNotSame`, `AssertEmpty`, `AssertNotEmpty`, `AssertSingle`, `AssertContains`,
`AssertThrows`, plus two double overloads:

```csharp
Log.AssertEqual(expected, actual, 0.0001);  // absolute tolerance
Log.AssertEqual(expected, actual, 4);       // decimal places
```

Both exist deliberately. With only the tolerance overload in scope, `AssertEqual(1.0, 1.004, 3)` would
convert `3` to a tolerance of `3.0` and accept nearly anything while reading as three-decimal-strict.

An assertion event is emitted **after** the underlying xUnit assert runs — `passed` cannot be known any
earlier. A wrapper also only catches `XunitException`: if a comparer itself throws, no `assertion` event
appears, though the test still fails through the unobserved-exception path.

### Call logging and anti-gaming

`.WithCallLogging()` requires an **interface**. Beyond logging, it turns two things into failures that
would otherwise pass silently:

- **A null return from a non-nullable member** is reported as a thrown contract violation. Without it, a
  service that stubs everything with `null` makes the test body throw somewhere unrelated.
  This reads the nullability annotation on the **interface**, i.e. from the Contracts assembly — so a
  Contracts project built without `<Nullable>enable</Nullable>` silently disables the check.
- **A throw from the service** is logged with `threw`, which marks the unit errored, and is rethrown with
  its original stack trace intact.

`Stream` arguments are snapshotted before the call so logging cannot drain them.

### The per-call timeout

Every proxied call runs under a wall clock, default **10 seconds**, set by `JUDGE_CALL_TIMEOUT_SECONDS`
(seconds, fractions allowed; `0` or less disables it; an unparseable value falls back to the default). The
judge image defaults and exports the variable in `judge_configure`, so a session image overrides it with a
single `ENV`.

A call that outlives the budget is reported exactly like a service throw — a `call` event carrying
`threw: "TimeoutException"`, then the exception rethrown so xUnit fails the test too, which is what keeps the
event stream and the TRX agreeing:

```
IFibonacciSequence.IndexOf(1) did not return within 10s - most likely an endless loop.
The call was abandoned and this test failed; the remaining tests still ran.
```

This exists because the alternative was measured on a real submission: an implementation whose `At` returned
0 for every index made a search for the value 1 loop forever, the test host sat in that one test until the
judge's 300-second suite wall clock tore the container down, and the competitor lost **every remaining test**
rather than the one that hung.

What to know before relying on it:

- **.NET cannot stop a synchronous loop**, only walk away from one. The call runs on a background thread that
  is abandoned, not killed, and keeps burning CPU inside the container's `--cpus`. After 8 abandoned calls the
  guard stands down, emits a `marker-error` saying so, and leaves further hangs to the suite wall clock.
- **An abandoned thread cannot spoil a later test.** `Thread.Start` captures the `ExecutionContext`, and the
  current test is an `AsyncLocal`, so the worker keeps pointing at the test that hung — a stray `call` event,
  or an exception the `FirstChanceException` handler sees on that thread, lands on the already-failed test.
- **Not covered**: a hang in the test body, in a service constructor (`ServiceResolver` runs in a field
  initializer, before the test starts), or after the first `await` of a `Task`-returning member — reflection
  hands the Task straight back, so what is timed is the synchronous part, which is all the proxy ever saw.
- **The guard is off under a debugger**, so stepping through a service does not fail the test you are
  inspecting.

### ServiceResolver

Scans loaded assemblies (plus a `LoadFrom` sweep of the app base directory) and picks the
**alphabetically first** concrete implementor by simple name, constructing it through the widest
constructor whose parameters are all contract interfaces, recursively. Instances are cached per
contract; `Reset()` clears the cache and `BuildWith(...)` bypasses it for hand-supplied dependencies.

Note when reading a failure: an unsatisfiable graph — including a cycle — surfaces as
*"No public constructor on X could be satisfied from contract interfaces"*, with the specific cause,
such as *"Cyclic dependency detected"*, as the inner exception.

## Two gotchas worth knowing

**`using` directives must go above the file-scoped namespace** in files whose namespace starts with
`Skill.Suite.TestLog`. Inside `namespace Skill.Suite.TestLog.Xunit`, a `using Xunit.Sdk;` resolves
`Xunit` against the enclosing namespace and fails with CS0234. This deviates from the repo convention
in `CLAUDE.md`, deliberately.

**The type name `TestLog` is shadowed inside `Skill.Suite.*` namespaces**, where the simple name binds
to the namespace rather than the type. Consumers normally never name the type — they use the inherited
`Log` property — but if you must, alias it:
`using HarnessTestLog = Skill.Suite.TestLog.Xunit.TestLog;`.
