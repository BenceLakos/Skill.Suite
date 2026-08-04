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
