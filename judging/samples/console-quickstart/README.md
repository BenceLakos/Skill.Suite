# console-quickstart

The minimum viable judge: a plain console app, one package reference, results in the Skill.Suite UI.
No test framework involved.

## What an external consumer does

```bash
dotnet new console -o my-judge && cd my-judge
dotnet add package Skill.Suite.TestLog
```

Then emit events as `Program.cs` here does. Inside this repo the sample uses a `ProjectReference`
instead, because the package is not published yet and the tests execute this very app — but the
`dotnet add package` form above is what to copy.

## Run it

```bash
cd judging && dotnet run --project samples/console-quickstart
```

With no `LOG_DIRECTORY` the events go to stdout. Point it at a directory and they go to a file:

```bash
LOG_DIRECTORY=/tmp/quickstart dotnet run --project samples/console-quickstart && cat /tmp/quickstart/events.jsonl
```

Both produce the identical stream — that is the whole point of the sink fallback. Inside a judgement
container the platform sets `LOG_DIRECTORY` for you and reads the file after the container exits.

## What it demonstrates

- One fixture, two unit tests — one passing, one failing — so both verdict paths appear in the UI.
- `Call` and `Assertion` events, which is what makes the UI show *what happened* rather than just a
  red or green row.
- `[Aspect]` ids passed explicitly (`aspect: "A1.1", aspectCompetitorVisible: true`). In an xUnit suite
  `Skill.Suite.TestLog.Xunit` reads the `[Aspect]` attribute and fills these in for you.
- A `score` event whose `value` is a **number** in 0..1 — a string there is silently ignored and the
  competitor would see no score at all.

`Divide` deliberately returns the wrong value so the failing path is real rather than simulated.

## Verified by

`Skill.Suite.TestLog.Tests/FileSinkTests.cs` runs this app as a child process and asserts the file
sink, the stdout fallback, per-run truncation, directory creation, and the fallback when
`LOG_DIRECTORY` cannot be used. If you change `Program.cs`, those assertions change with it.
