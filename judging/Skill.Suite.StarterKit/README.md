# Skill.Suite.StarterKit

`skill-starter` — derives the competitor starter kit of a Skill Suite session from the session's own
sources, so the kit cannot drift out of step with the contract. A session module's
`make-competitor-start.sh` runs it; it is rarely called by hand.

```bash
dotnet tool install -g Skill.Suite.StarterKit
```

```bash
skill-starter <reference-project-dir> <output-dir> [--kind whitebox|blackbox]
```

```bash
skill-starter ./My.Session.Services  ./competitor-start/My.Session.Services
skill-starter ./My.Session.UnitTests ./competitor-start/My.Session.UnitTests --kind blackbox
```

The `.csproj` is copied verbatim and every source file is rewritten with Roslyn, which keeps each
declaration's text exactly — nullable annotations, generics, `ref`/`out`, default values — and only replaces
what would give the answer away. The two modes are inverses, because the two session types are.

## `--kind whitebox` (default)

The competitor implements the services, so the reference implementation is stubbed:

- public members keep their exact declaration and get a `NotImplementedException` body;
- non-public members are removed — a private helper's name gives away the decomposition;
- fields are removed unless `const` — a lookup table is the answer in data form.

## `--kind blackbox`

The competitor writes the tests, so the reference suite is emptied:

- every method is removed — the tests *are* the answer;
- private fields and constructors are kept — in a suite those are the harness wiring, not the answer;
- a commented-out example test is added, so the delivered project compiles with zero tests.

## Output

The output directory is recreated, never merged, so a stub for a member that has since been deleted cannot
survive into what competitors receive. Re-run it whenever the contract or the reference changes.

A module's `make-competitor-start.sh` calls the tool **twice** — once per kind — because one starter kit
serves both session types: the judge swaps a single folder out of a submission and ignores the rest, so
shipping both projects costs nothing and gives every competitor a solution that opens and builds. The doc
comment each mode writes onto the type is a single line naming the two rules the harness enforces — keep the
folder and its csproj, add no package references — because the member-level docs already carry the contract.

## Exit codes

| Code | Meaning |
|---|---|
| 0 | Success |
| 1 | A source file could not be parsed or rewritten — the offending file is named on stderr |
| 2 | Usage error — usage is printed |
