# The CIS marking scheme workbook

Three sheets, matched by **exact name**. Anything that reads the file — the scaffolder, the validators,
`make-marking-map.py` — locates them by name, so renaming a tab breaks the toolchain silently.

```
CIS Marking Scheme Import     the importable sheet: sections, criteria, one row per aspect
Test Map                      aspect id -> test method, the only place ids are written down
Calculations                  four cross-checks a human reads at a glance
```

## Sheet 1 — `CIS Marking Scheme Import`

Four regions, in row order. Row numbers below are from a real 59-aspect / 15.00-mark session; only the
*shape* is fixed, the offsets move with the aspect count.

**The WSOS section table** (rows 4–11). Column A is the section number, B its name, **I the declared WSOS
marks**, J the marks the aspects actually carry, K the variation:

```
J5:  =SUMIF($I$28:$I$400, A5, $K$28:$K$400)
K5:  =ABS(I5-J5)
K11: =SUM(K5:K10)                            total variation, must display 0
```

**The SUMIF range must overshoot the last aspect row.** `$I$28:$I$400` deliberately runs far past the end.
A range that stops at the last aspect silently ignores every row appended later, and the sheet keeps
totalling *something*, so nothing looks wrong.

**The Criteria table** (rows 14–19). Column A the criterion id, B its name, K the mark — pulled from each
block's total rather than retyped:

```
K16: =N27      K17: =N56      K18: =N79
K19: =N27+N56+N79                            the session total
```

**One block per criterion.** Each opens with a header row carrying eleven column labels in A–K plus the
block's own total in L–N:

```
L27: Criterion A     M27: Total Mark     N27: =SUM(K28:K53)
```

Inside a block, two kinds of row alternate:

- a **sub-criterion label row** — A = `A2`, B = the sub-criterion name, C = day of marking, D–K empty;
- an **aspect row** — A–C empty, D–K filled.

The eleven aspect columns:

| col | label | content |
|---|---|---|
| A | Sub Criterion ID | *empty on aspect rows* |
| B | Sub Criterion Name or Description | *empty on aspect rows* |
| C | Day of Marking | *empty on aspect rows* |
| D | Aspect Type | `M` measurement, `J` judgement — this is what identifies an aspect row |
| E | Aspect - Description | the marking line an expert reads |
| F | Judg Score | judgement aspects only |
| G | Extra Aspect Description | **the expected result** |
| H | Requirement (Measurement Only) | **the input** |
| I | WSOS Section | the section this aspect's marks land in |
| J | Calculation Row (Export only) | |
| K | Max Mark | |

Columns **G and H are the pair that matters** and the pair most often confused: G is what the
implementation must produce, H is what it is given. A reader who swaps them writes a test backwards.

**Aspect rows carry no aspect id.** Nothing in this sheet says `A2.4`. The ids are positional — derived by
counting aspect rows in order — and written down only in the Test Map. Inserting a row therefore
renumbers every aspect below it.

No merged cells. No defined names. Marks formatted `0.00`. Several header labels contain embedded newlines
(`Sub\nCriterion\nID`), so match them loosely if you match them at all.

## Sheet 2 — `Test Map`

Eight columns, one row per aspect, then a total row and a contract note.

```
Aspect ID | Criterion | Sub-crit. | Sub-criterion name | Aspect (marking line)
          | xUnit test method (bound via [Aspect] attribute) | WSOS | Mark
```

This is the sheet the tooling reads: it is the only place the aspect id and the test method name appear
together. Keep the marking line in step with sheet 1's column E — nothing enforces it, and
`check-scheme.py` compares them field-for-field for that reason.

The last row is a prose contract note. Keep it current; it is where a future author learns that grading
binds through the `[Aspect]` attribute rather than a name prefix.

## Sheet 3 — `Calculations`

Four label/value pairs, each a cross-check a human can read without opening the other sheets:

```
Session total marks                              15
Measurement aspects (= automated checks)         59
xUnit test cases (aspects minus pipeline checks) 57
WSOS variation (must be 0)                       0
```

## Reading the file from Python

`openpyxl` with `data_only=True` returns the values Excel cached when it last saved — verified working on
a real scheme, which returns `0.35 / 6.9 / 7.75` for the WSOS table.

It returns **`None` for every formula cell in a workbook openpyxl just wrote**, because nothing has
computed them yet. A validator that reads the reconciliation cells therefore passes a freshly scaffolded
sheet vacuously. `check-scheme.py` recomputes the totals from the aspect rows instead, which is the only
approach that works on both a hand-edited and a generated file.

Sum marks in `Decimal`, not `float`. It happens to reconcile exactly in binary floats for a
`0.15 / 0.20 / 0.35 / 0.70` ladder, but that is a property of those particular values, not a guarantee.
