---
name: authoring-marking-schemes
description: How to derive, verify and revise a WorldSkills CIS marking scheme — the .xlsx that turns a test project brief and a Skill Suite session module into aspects, marks, expected values and a score that survives a dispute. Use this skill whenever the user is working on a marking scheme, a CIS import sheet, aspects, sub-criteria, WSOS sections, the Test Map, expected values, aspect discrimination, or a marking-scheme-*.xlsx sitting beside a *.Contracts / *.Services / *.UnitTests trio — and also when they mention traceability to the brief, generating marking-map.json, the complete / defective / degenerate / stub personas, the scheme's floor, or Stryker kill sets offered as an argument that two aspects are redundant. Reach for it even if the user only says something like "add a couple more aspects", "do the marks still add up", or "is this test actually grading anything", because each of those is a scheme revision, and a revision that is not re-verified end to end is exactly how an aspect goes inert.
---

# Authoring a WorldSkills CIS marking scheme

A **marking scheme** is the spreadsheet that decides what a competitor scores. This skill is the step after
[`authoring-judging-sessions`](https://github.com/BenceLakos/Skill.Suite): that one owns the module — the
template, contracts, reference implementation, judge image, starter kit. This one owns the sheet, and
mostly it owns *proving the sheet grades what it claims to*.

## Three artifacts, and which one wins

| | who reads it | authoritative for | cost to change |
|---|---|---|---|
| the **brief** | competitors | what they were told to build | frozen once submitted |
| the **scheme** (`.xlsx`) | CIS, experts | what each behaviour is worth | re-verify everything |
| the **hidden suite** | nobody, until a dispute | whether a behaviour was delivered | recalibrate + rebuild |

Two invariants hold the triangle together:

- **One aspect = exactly one automated test case = exactly one input.** A `[Theory]` with five rows is one
  all-or-nothing aspect; split it when the rows should score separately.
- **Anything graded but not stated in the brief is unfair.** The brief is the only specification a
  competitor gets. An aspect they could not have derived from it is a mark nobody could earn.

The scheme is not the automated ceiling. Submission and build aspects are judged by the pipeline, not by a
test, so a perfect submission scores the session total *minus* those.

## 1. Trace the brief before you write a single aspect

Build a two-column table — aspect ↔ brief clause — and build it **first**, in both directions. It is the
fairness contract, and it is far cheaper to build than to reconstruct during a dispute.

Forwards: every aspect must name the clause it grades — a numbered global convention, a rule row, a
sentinel bullet, or a worked example. If you cannot name one, either the aspect goes or the brief does.

Backwards: every normative clause should name the aspects that grade it. This direction is the one that
finds real holes. A convention reading *"preserve input order in outputs unless told to sort"* went
completely ungraded, because every multi-phase fixture happened to declare its phases alphabetically — an
implementation that sorted its output scored **57/57**.

Behaviour that is only *derivable* by combining conventions is a judgement call: name the conventions in
the aspect's requirement so the reasoning survives. Behaviour that requires guessing is not gradable.

**A submitted brief is frozen.** After that point a wording gap stops being a fix and becomes a recorded
disposition — write down which reading the answer key takes and why, so an expert can accept either.

## 2. Derive the aspect set

Criteria map to interfaces or components; sub-criteria to a concern within one (sentinels, one rule per
input, evaluation order, output shape). Inside a sub-criterion, one aspect per rule, per boundary, per
sentinel, and per negative case — the input that must *not* raise anything is as gradable as the one that
must.

Give pipeline-judged aspects rows too. They have no test, so `skill-marker report` shows them as `no` with
`0` tests matched even for a perfect submission — correct output, not a broken run.

Watch density. Four aspects asserting the *same* output for four different degenerate inputs is
defensible — a competitor can miss each independently — but count it: that was 0.80 marks on one rule, and
all four are free to an implementation hard-coded to that answer.

## 3. Assign marks, sub-criteria and WSOS sections

Use a short mark ladder and stick to it — `0.15 / 0.20 / 0.35 / 0.70` covers a 15-mark session. Headline
aspects (a worked example from the brief) earn the top rung; sentinels the bottom.

Three reconciliations must hold **at once**, and the sheet's own formulas check them:

- each criterion's aspect marks = its Criteria-table total, and those sum to the session total;
- each WSOS section's aspect marks = its declared WSOS marks;
- every variation = 0.

All three passing still misses a misfiled aspect. Put **equivalent aspects in the same WSOS section**: one
sentinel sat in §5 while its three siblings sat in §3, and every total reconciled regardless, because
moving one aspect between sections just moves the same marks around.

No tool finds that — a section records the *skill being assessed*, not the output, so mixed sections inside
one sub-criterion are normal and correct. A minority-vote heuristic was tried and removed: on a correct
scheme it produced three false positives and no true ones. Review it by hand instead, asking of each
aspect whether it assesses the same skill as its siblings, not whether it asserts the same value.

```bash
python3 scripts/check-scheme.py <marking-scheme.xlsx>
```

## 4. Compute every expected value with an independent model

Write a throwaway model from the **brief's text**, and take every expected value from it. Not by hand, and
never by running the reference implementation — a scheme computed from the code merely restates the code,
bugs included, and calibration against it then proves nothing at all.

For anything with floating-point arithmetic, re-derive each value under several orderings — `(a/b)*c`,
`a*c/b`, exact rational, `decimal` — and ship only values stable across all of them. Otherwise an aspect
can be lost to a defensible difference in evaluation order rather than to a wrong answer.

Choose tolerances deliberately. Integers exactly. A value the brief says is "kept to 4 dp" gets a tolerance
of `1e-9`, **never** a comparison *at* 4 dp: rounding the actual value on the way in passes an
implementation that never rounded, which is precisely what the aspect exists to catch.

See [references/expected-values.md](references/expected-values.md). Re-deriving a value is not the same as
proving the aspect can be lost — that is the next section, and it is the one people skip.

## 5. Prove each aspect can be lost

An aspect earns its marks only if some plausible wrong implementation **fails it**. Establish that by
writing that implementation and running it, not by reasoning about it. Reasoning is how inert aspects get
shipped.

The recurring blindnesses, each of which shipped at least once before being measured:

- **IEEE-754 swallows the boundary.** A "midpoint rounds away from zero" aspect whose `C₀` computes to
  `62.500000000000014` is not on a midpoint, so both rounding modes agree and the aspect grades nothing.
  A `Y >= 1` boundary where the strict `>` divides by `0.0` gets `+Infinity`, which clamps to the same
  right answer. Boundary aspects need an input where the two implementations produce different **finite**
  results.
- **Assertions stop carrying distinctness** once every test asserts every property. Two aspect pairs
  became byte-identical tests; each was separated again by changing one *input*.
- **The fixture that supplies the null short-circuits the path that would notice it.**
- **Alphabetical fixtures** make order-preservation ungraded.
- **Unobservable intermediates** cannot be graded at all if the contract does not expose them.
- **Logically redundant clauses** can never fire alone, so no input isolates them.

When no input can separate the two implementations, **weaken the aspect's description** to what it does
grade. An aspect that promises more than it delivers is what a dispute is won with.
[references/discrimination.md](references/discrimination.md) has the probe recipes and the disposition
table format.

## 6. Build the workbook, and derive everything from it

Three sheets, matched by exact name: `CIS Marking Scheme Import`, `Test Map`, `Calculations`. The full
column-by-column anatomy — including which column is the *expected result* and which is the *input* — is
in [references/cis-format.md](references/cis-format.md).

```bash
python3 scripts/scaffold-scheme.py <aspects.csv> --out <marking-scheme.xlsx> --session-total 15.00
```

The sheet is the single source of truth. The judge's `marking-map.json` is **generated** from it, never
transcribed, so ids and labels cannot drift apart:

```bash
python3 scripts/make-marking-map.py <marking-scheme.xlsx> --out marking-map.json
```

```bash
python3 scripts/make-marking-map.py <marking-scheme.xlsx> --out marking-map.json --check
```

`--check` exits non-zero when the file would change, which turns generation into a gate you can run in the
revision loop.

## 7. Bind the suite to the scheme

Bind by attribute — `[Aspect("C2.3")]` — and never by a test-name prefix. The id in a method name is a
second copy of the same fact with nothing keeping it in step; grading reads the attribute. Method names are
ordinary `Scenario_ExpectedResult`.

**Every test asserts the whole output**: the return value is non-null, its collection is non-null and the
right length, and *every* property matches — not just the one the aspect is named after. A right answer
attached to the wrong id is still wrong. The consequence loops straight back to §5: once assertions no
longer distinguish aspects, the inputs must.

```bash
python3 scripts/check-aspect-binding.py <marking-scheme.xlsx> <path/to/Session.UnitTests>
```

Zero missing, zero extra, zero name mismatches — or the sheet is describing a suite that does not exist.

## 8. Run the verification battery

In order, cheapest first, because the last one needs Docker:

1. the hidden suite is **100% green** against the reference implementation;
2. `check-scheme.py` clean;
3. `check-aspect-binding.py` clean, and `make-marking-map.py --check` clean;
4. a discrimination probe per rule family — each fails **only** its own aspects;
5. four personas through the real judge image, **all exit 0**.

| persona | what it is | must produce |
|---|---|---|
| `complete` | the reference as a submission | the automated ceiling exactly |
| `defective` | seeded defects, one per rule family | each defect fails only its own aspects |
| `degenerate` | compiles, never throws, computes nothing | the scheme's floor — measure it |
| `stub` | every member throws | 0 marks, exit 0, a diagnostic on every test |

[references/verification-battery.md](references/verification-battery.md) covers each persona and the
mutation-testing caveats. The short version: mutation is a secondary signal, it must be run with bail
disabled or its per-test data is execution-order noise, and identical kill sets do **not** mean two aspects
are redundant.

## 9. Revise — nothing in this triangle changes alone

Every scheme revision is a full loop. Skipping a step is how an aspect silently stops grading:

1. re-trace any changed aspect to its brief clause;
2. re-derive changed expected values with the model;
3. `check-scheme.py`, then regenerate `marking-map.json`;
4. `check-aspect-binding.py` — renames and new aspects both land here;
5. **seed a defect for the behaviour you just started grading**;
6. recalibrate, re-run all four personas, rebuild the image;
7. update the calibration record, and mark superseded evidence as superseded.

Step 5 is the one that decays. **Seed a new defect the day you strengthen an aspect**, and verify it scored
full marks *before* the change — otherwise nothing stops the aspect going inert again later.

## Traps worth knowing before they bite

**The scheme's floor is not zero.** A do-nothing implementation collects every aspect whose expected value
is a sentinel, an empty list, or a zero — 2.20 of 15.00 in a real session. Not a defect: those behaviours
must be graded. But measure it and decide, because the only fix is pairing each sentinel aspect with a
companion assertion on a non-degenerate input, and that moves marks.

**Aspect ids are positional in sheet 1.** The aspect rows carry no id at all — columns A–C are empty, and
only the sub-criterion label row spells out `A2`. Only the Test Map names `A2.4`. Insert a row and every id
below it silently shifts, taking `marking-map.json` and the `[Aspect]` attributes out of step with it.

**A logically redundant clause cannot be isolated by any aspect.** `MaxGreen <= 0` is implied by
`MinGreen > MaxGreen` whenever `MinGreen > 0`, so no input makes it fire alone. Do not chase the surviving
mutant; record the disposition and weaken the aspect's wording.

**Mutation kill-set identity is not redundancy.** Stryker perturbs operators. It cannot generate "returns
the list sorted" or "right value, wrong key" — exactly the errors output-shape aspects exist to catch. Two
aspects with byte-identical kill sets were provably distinct under direct probes.

**An unobservable intermediate cannot be graded.** If the contract exposes only a final ratio, an
intermediate rounding step is ungraded no matter what the aspect description claims. Say so in the
description rather than implying a check that cannot exist.

**A frozen brief outranks a better scheme.** Once the brief is submitted, a gap caused by its wording stays
a documented disposition that experts accept either reading of — not something to fix by tightening the
sheet underneath the competitors.
