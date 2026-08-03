# Checkout Rules — a worked black-box session

The inverse of [fibonacci-session](../fibonacci-session/README.md). There, competitors implement the services
and a hidden suite marks them. Here **the competitor writes the test suite**, the implementation is baked in,
and the suite is *measured* rather than marked.

```
Checkout.Rules.Contracts/    IDiscountEngine — the whole specification; competitors test against this
Checkout.Rules.Services/     the reference implementation — baked into the image IN SOURCE FORM
Checkout.Rules.UnitTests/    the reference suite — calibration only, and the answer key
competitor-start/            the wired-but-empty test project competitors receive
submissions/thorough/        a good competitor suite
submissions/shallow/         high coverage, asserts nothing — the point of the whole exercise
```

## What makes it black-box

Three things invert:

| | white-box | black-box |
|---|---|---|
| swapped folder | `*.Services` | `*.UnitTests` |
| in the image | hidden tests | **the reference implementation, as source** |
| the score | aspects passed | coverage × mutation kill rate |

The implementation ships as source because Stryker has to mutate it to generate the faults a suite is scored on
catching. That makes this the most sensitive image in the system — more so than a white-box one, which only
holds tests. Private registry, immutable tags, never public CI.

The module's own `Checkout.Rules.UnitTests` is the **reference suite**: it exists to prove the session is
markable before anyone competes, and it is excluded from the image by `Dockerfile.blackbox.dockerignore`.
Handing it to a competitor would hand them the marks.

## Why `submissions/shallow` exists

This is the row that justifies mutation testing existing at all. Measured on the real image:

| submission | line coverage | mutation kill rate | score |
|---|---|---|---|
| `thorough` | **0.7917** | 0.9143 | **0.9685** |
| `shallow` | **0.7917** | 0.1714 | **0.2937** |

The coverage figures are not a typo — they are **identical**. `shallow` calls every member across every branch
and then asserts nothing meaningful: that a number came back, that a bool is one of two values. Against
coverage alone the two submissions are indistinguishable. Only the kill rate separates running the code from
checking it.

If `shallow` ever scores close to `thorough`, the mutation ramp in `marking-map.json` has stopped doing any
work.

## Calibrating the marking map

Two things bit during calibration, both worth knowing before tuning your own:

**The coverage ceiling was unreachable.** The collector counts every loaded assembly, so a suite covering every
branch of `DiscountEngine` still reports 38/48 lines — a practical maximum of ~79%. A `coverageCeil` of 100
silently capped scaled coverage at 0.58 for *everyone* and dragged the thorough suite down to 0.6958. An
unreachable ceiling is invisible in the score; it just makes every competitor look worse than they are. Measure
first, then set the ramp.

**The mutation ramp is where the tuning actually matters**, because it is the only term separating the two
submissions. 25..95 keeps the gap wide.

The composite, for reference: `value = 0.3 × scaledCoverage + 0.7 × core`, where `core` is the geometric mean of
`passingCoverage` and `mutationScore`. That geometric mean is why `shallow` collapses to 0.29 — zero on either
term drags the core to zero regardless of the other.

## Build and run

Calibrate first — the reference suite must be green against the reference implementation:

```bash
cd judging/samples/checkout-rules-session && JUDGING_ROOT=../.. ./pack-contracts.sh && dotnet test Checkout.Rules.sln -c Release
```

```bash
cd judging/samples/checkout-rules-session && BASE_IMAGE=skill-suite-judge-base:9.0 ./build-image.sh blackbox
```

```bash
judging/tools/judge-run.sh checkout-rules-blackbox-judge:1.0.0 judging/samples/checkout-rules-session/submissions/shallow
```

A black-box run takes minutes rather than seconds: Stryker re-runs the whole suite once per mutant.

## Driving it from the UI

Create the session with judgement image `checkout-rules-blackbox-judge:1.0.0` and template folder
`/starter-packages/checkout-rules-session/competitor-start`. Everything else is identical to a white-box
session — the platform does not know or care which kind an image is. It reads the `score` event either way and
lifts `value` onto the fixture's quality, which drives the competitor's bucket.

A competitor pushes a `Checkout.Rules.UnitTests` folder, not a services folder. `submissions/thorough` and
`submissions/shallow` are ready to push as two competitors, and the ~0.97 against ~0.29 spread is visible as
different buckets in the competitor view.

## Regenerating the starter kit

```bash
cd judging/samples/checkout-rules-session && ./make-competitor-start.sh blackbox
```

Generated, not hand-written — same principle as the white-box samples, opposite rewrite. `skill-starter --kind
blackbox` removes every method from the reference suite (the tests *are* the answer) while keeping the harness
wiring: the private service field, the fixture constructor, the base list. Without those the delivered project
cannot resolve the service and emits no fixture events at all. A commented-out example is added so the
competitor has the required shape to copy, and the script compiles the result before it finishes.
