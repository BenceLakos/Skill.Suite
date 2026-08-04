# Calibrating a black-box marking map

A white-box session needs one calibration: the hidden suite must be green against the reference
implementation. A black-box session needs a second one, and it cannot be reasoned out — it has to be measured.

## What the score actually is

```
value = 0.3 × scaledCoverage + 0.7 × core
core  = geometric mean of (passRate × scaledCoverage) and mutationScore
```

`scaledCoverage` and `mutationScore` are the ramps from `marking-map.json`: a value at or below the floor scores
0, at or above the ceiling scores 1, linear between.

The geometric mean is the important part. Zero on either term drags `core` to zero regardless of the other, so a
suite with excellent coverage and no real assertions collapses — which is the entire point.

## Measure, then tune

Write two throwaway submissions and run both through the real image:

- **thorough** — covers every documented boundary with exact expected values
- **shallow** — calls every method across every branch, then asserts nothing meaningful (`result >= 0`,
  `value || !value`)

`shallow` should have **line coverage nearly identical to thorough**. That is not a flaw in the sample, it is
the finding: coverage cannot tell the two apart, and only the kill rate can. If your two submissions have very
different coverage, `shallow` is not shallow enough — make it call everything.

Then tune so the gap is wide. A worked example from `samples/checkout-rules-session`:

| submission | line coverage | kill rate | score |
|---|---|---|---|
| thorough | 0.7917 | 0.9143 | 0.9685 |
| shallow | 0.7917 | 0.1714 | 0.2937 |

## The trap: an unreachable coverage ceiling

The coverage collector counts every loaded assembly, not just the implementation. A suite that covers every
branch of the implementation can still report something like 38/48 lines — a practical maximum well under 100%.

Setting `coverageCeil: 100` therefore caps scaled coverage for **everyone** and compresses the whole band. In the
example above it dragged the thorough suite from 0.97 down to 0.6958. An unreachable ceiling is invisible in the
score: nothing errors, every competitor just looks worse than they are.

**So: measure the practical maximum first, then set the ceiling at or just below it.** The shipped defaults
(coverage 40..80, mutation 30..80) are usually right for coverage. The mutation ramp is where per-session tuning
earns its keep, because it is the only term separating the two submissions — 25..95 keeps the gap wide.

Re-measure after changing the implementation. Adding code changes the coverage denominator.

## Parts

`parts` routes by **whole namespace or path segment**, not globs:

```jsonc
"parts": ["pricing", "shipping"]                      // id doubles as the segment
"parts": [{ "id": "aqi", "segments": ["AirWatch.Aqi.Services", "Aqi"] }]
```

Declare one per component so a competitor can see which area their suite neglected. With a single
implementation project, leave it empty — a part would either duplicate `overall` or route nothing at all.
`overall` is reserved and must not be declared.

Unknown JSON keys are **silently ignored** by the deserializer, so a plausible-looking but wrong field name
produces a map that does nothing. Check against the marker's `MarkingMap`, `PartRule` and `ScoringOptions`
types rather than guessing.

## Aspects in a black-box session

Informational only — competitors choose their own `[Aspect]` ids, so the score comes from coverage and
mutation. Declare the reference suite's ids anyway: `skill-marker report` needs a stable row set when an expert
re-marks a downloaded event log by hand.
