# The verification battery

Five gates, cheapest first. The ordering is deliberate: a textual mismatch found in one second should not
cost a Docker build to discover.

| # | gate | pass condition |
|---|---|---|
| 1 | `dotnet test <Session>.sln -c Release` | 100% green against the reference |
| 2 | `check-scheme.py` | totals, WSOS sections and variation reconcile |
| 3 | `check-aspect-binding.py`, `make-marking-map.py --check` | 0 missing / 0 extra / 0 mismatched |
| 4 | discrimination probes | each defect fails only its own aspects |
| 5 | four personas through the real judge image | as below, **all exit 0** |

Gate 1 is not optional and not a formality. A session whose own answer key does not pass is not ready:
every competitor loses the same marks for the author's bug, and nobody finds out until a dispute.

## The four personas

A submission is a directory containing only the swapped folder. Run each with `judge-run.sh`, which
mirrors the platform exactly — read-only mount, `-w /workspace`, two env vars.

**`complete`** — the reference implementation as a submission. Must score the **automated ceiling** and no
more. The shortfall from the session total must equal exactly the pipeline-judged marks; if it does not,
an aspect is failing that should not, or a mark is misfiled.

**`defective`** — one seeded defect per rule family, each commented in place. Every defect must fail only
its own aspects. This persona is the regression test for the whole scheme: it is what stops a strengthened
aspect going quietly inert three revisions later. When a defect's target aspect is already red under
another defect, verify that one in isolation against the clean reference.

**`degenerate`** — a *probe*, not a plausible submission: three implementations that compile, never throw,
and compute nothing. It measures the scheme's **floor**. Expect it to collect every aspect whose expected
value is a sentinel, an empty list, or a zero — 2.20 of 15.00 in a real session. That is inherent to
grading sentinels, not a defect, but it must be a number you chose rather than one you discovered.

**`stub`** — the shipped starter kit, every member throwing. Must score 0, exit 0, and carry a diagnostic
on every test: each `call` event should have `threw` set, so the platform shows errored tests with an
explanation rather than bare red rows.

## Scoring a run

`skill-marker report` emits `aspect_id,result,tests_matched,tests_passed`. Join it to the sheet:

```bash
python3 scripts/score-report.py <marking-scheme.xlsx> <report.csv> --label complete
```

```bash
python3 scripts/score-report.py <marking-scheme.xlsx> <defect.csv> --compare <clean.csv>
```

`--compare` prints the symmetric difference — the exact tool for "this defect fails only its own aspects".
`--free-list` prints what a run passed, which is how the floor gets measured.

Pipeline-judged aspects always report `no` with `0` tests matched, including for `complete`. Correct
output: they are judged by the run existing and the build succeeding, not by a test.

## Mutation testing

A useful **secondary** signal — it finds behaviour no aspect pins — and a misleading primary one. Two
caveats, both learned the hard way:

**Run with bail disabled.** With bail on, the runner stops at the first killing test, so `killedBy`
records one arbitrary test rather than all of them. Any per-aspect analysis built on that is
execution-order noise. The first such analysis produced a confident, entirely fictional table.

**Identical kill sets are not redundancy.** Mutation perturbs operators. It cannot generate "returns the
list sorted" or "right value, wrong key" — precisely the errors output-shape aspects exist to catch. Two
aspects with byte-identical kill sets (15 mutants each) were provably distinct under direct probes. Use
kill-set overlap as a hint about where to look, never as a verdict.

Classify every surviving mutant before acting. Most are benign: dictionary capacity hints, `>` versus
`>=` in a max scan, defensive bounds that are unreachable, guards for input the brief leaves undefined,
and clauses that are logically redundant in the brief itself. What is left after that classification is
the real finding, and it is usually small.

## What to record, and what expires

In the calibration record, per revision: the gate results, the persona table with marks, the mutation
score with its surviving-mutant classification, and the discrimination probes with the aspects each
failed.

Then mark what the next change invalidates. A persona table is evidence about one revision of the sheet
and one revision of the suite; the moment either moves, it is a historical note, not a current claim.
Superseded evidence left looking current is worse than no evidence at all.
