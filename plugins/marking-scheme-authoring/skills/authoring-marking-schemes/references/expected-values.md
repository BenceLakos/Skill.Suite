# Expected values you can defend

Every number in the Extra Aspect Description column is a claim you may have to defend to an expert holding
a competitor's failing run. There are three places such a number can come from, and only one of them is
admissible.

| source | verdict |
|---|---|
| worked out by hand | plausible until the fifth one, then quietly wrong |
| taken from running the reference implementation | **inadmissible** |
| computed by an independent model of the brief | the only defensible one |

The middle row is the trap worth naming. A scheme whose expected values come from the implementation
merely restates the implementation, bugs included — and the calibration run that follows, where the
hidden suite passes 100% against that same implementation, then proves nothing whatsoever. Both artifacts
agree because they have the same author and the same mistake.

## Writing the model

Write a throwaway script that implements the brief's formulas **from the brief's text**, in a different
language from the implementation if you can — the difference is what makes it independent. Python is
convenient because `fractions.Fraction` and `decimal.Decimal` are in the standard library.

Keep it. It belongs in `_design/` next to the calibration record: it is answer-key-adjacent, so it never
ships, but it is the only auditable record of where the numbers came from. When someone disputes a value
two months later, the model is the answer.

Cross-check the model against any worked example the brief already contains before trusting it on anything
else. If the brief works through one case, the model must reproduce it exactly.

## Stability across arithmetic orderings

A competitor's arithmetic may differ from yours in the last bit and still be correct. Re-derive every
value under several orderings and ship only what is stable across all of them:

```
(y / Y) * G        y * G / Y        y * (G / Y)        Fraction        Decimal
```

If a value moves between orderings, the aspect is not gradable as written — a competitor would lose it for
a defensible choice rather than a wrong answer. Change the input until it is stable.

**The dangerous cases look innocuous.** Two allocation cases landed on the documented answer only because
a floating-point error pushed a raw share just below a whole number, flooring it one lower, *and* the
largest-remainder pass then handed the second back. Right answer, wrong route. Neither case looked like it
needed checking; both were only safe because the check was run anyway. Re-derive everything, not just what
looks fragile.

## Choosing a tolerance

| output | comparison |
|---|---|
| integers (cycle lengths, green times, counts) | exact |
| a value the brief says is "kept to N dp" | absolute tolerance `1e-9` |
| a value with no stated precision | fix the brief, then pick one |

Never compare *at* the stated precision. Asserting "equal to 4 decimal places" rounds the actual value on
the way in, so an implementation that never rounded at all sails through — and rounding is exactly what
such an aspect exists to grade. A `1e-9` tolerance absorbs any arithmetic ordering and absorbs nothing
else.

Watch for a value that is zero in both a correct and an obviously broken implementation. `0.0` is not much
of an assertion; see the floor discussion in the verification battery.

## What to write down

In the calibration record, for each component: how many values were checked, that they reproduce the
scheme exactly, and which orderings were tried. Name any value that needed the scheme changed, and say
why. That paragraph is what turns "we checked" into evidence.

## This is not discrimination

Re-deriving a value proves the scheme is *arithmetically right*. It says nothing about whether a wrong
implementation would fail the aspect — a value can be perfectly correct and still be produced by every
plausible implementation, correct or not. That is a separate proof, and it is the one that actually
decides whether the mark is real. See [discrimination.md](discrimination.md).
