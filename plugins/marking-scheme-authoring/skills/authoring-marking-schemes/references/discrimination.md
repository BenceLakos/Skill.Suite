# Making an aspect losable

An aspect is worth its marks only if a plausible wrong implementation **fails** it. That is not something
to reason about — every inert aspect described below survived review by someone reasoning carefully. It is
something to measure.

## The probe method

1. Name the specific defect the aspect exists to catch, in one sentence.
2. Copy the reference implementation and introduce exactly that defect.
3. Run the suite.
4. Read which aspects went red.

Three possible outcomes. The aspect fails → it grades what it claims. It passes → the aspect is inert,
and the input has to change. It fails *along with several others* → the aspect is real but the marks are
coupled; decide whether that double jeopardy is acceptable.

Keep the probes that matter as a permanent `defective` persona, one defect per rule family, so the
property is re-checked on every revision instead of once.

**Attribution needs isolation.** If a probe's target aspect is already red under a different seeded
defect, you learn nothing from the combined run. Run that probe alone against the clean reference.

## Six blindnesses

### IEEE-754 swallows the boundary

The one that costs the most. Two real cases:

- An aspect specified as *"midpoint rounds away from zero: 62.5 → 63; banker's rounding would give 62"*.
  In `double`, `1 − 0.80` is `0.19999999999999996`, so `C₀` is `62.500000000000014` — strictly above the
  midpoint. Both rounding modes return 63. The aspect graded nothing, and a plain `Math.Round` scored full
  marks.
- An aspect specified as *"`Y = 1` exactly is saturated (boundary: `Y >= 1`)"*. An implementation using
  the strict `>` divides by `0.0`, gets `+Infinity`, and clamping that lands on `MaxCycle` — the right
  answer, by accident. Measured: the strict version passed **all 57 tests**.

The fix is algebraic, not cosmetic. Find inputs where the two implementations produce different **finite**
results. For the midpoint case that meant finding the only shape whose `C₀` is an exact half-integer in
binary — zero demand with an odd total lost time — after which the two modes gave 13 and 12.

Sometimes no such input exists. At `Y = 1` both branches converge on `MaxCycle` for every input, so that
aspect cannot be made to bite. **Weaken the description** to what it does grade ("saturation is handled at
the boundary") and record why.

### Assertions stop carrying distinctness

Once every test asserts every property — the right rule, but it has a consequence — two aspects that
differed only in *which* assertions they omitted become the same test. Two pairs became byte-identical.

**Distinctness must then come from the input.** Each pair was separated by changing one input:

- The Component B pair: the shape aspect got **phases declared out of alphabetical order, with every
  ratio identical**. Order became observable; a value attached to the wrong phase did not, which leaves
  that to its partner.
- The Component C pair: the shape aspect got **out-of-order phases, a demand list in a different order,
  and a zero remainder**. Keyed-versus-positional lookup became observable; the largest-remainder logic
  did not, which leaves that to its partner.

Each pair member now fails under a probe the other passes, in both directions.

### The fixture that supplies the null short-circuits the path

A test passes a null collection to grade "null is treated as empty" — but the same fixture also sets some
*other* field null, and the implementation returns early on that one, never reaching the code the aspect
is about. Two mutants survived for exactly this reason. Check that the path you mean to exercise is
actually reached.

### Alphabetical fixtures make order-preservation ungraded

Every multi-phase fixture declared its phases `P1, P2, P3`, so "program order" and "sorted by id" were the
same sequence. An implementation that sorted its output scored **57/57**. Declare fixtures out of
alphabetical order wherever a rule mentions order.

### Unobservable intermediates

If the contract exposes only a final ratio, an intermediate rounding step is ungraded no matter what the
aspect says — rounding it or not produces the same observable value. No test can be written. Reword the
aspect to the observable, and note the limitation.

### Logically redundant clauses

`MaxGreen <= 0` is implied by `MinGreen > MaxGreen` whenever `MinGreen > 0`, so no input makes it fire
alone. An aspect naming it can only ever grade its sibling. Record the disposition rather than chasing the
surviving mutant.

## Recording what cannot be fixed

Some aspects genuinely cannot be made to bite. Keep a table so the next author does not re-litigate it:

| aspect | blindness | what it actually grades | disposition |
|---|---|---|---|
| C1.6 | IEEE-754 boundary | that saturation is handled at all | wording weakened; not closable at `Y = 1` |
| B1.4 | unobservable intermediate | the final ratio to 4 dp | wording weakened |

That table is the difference between a known limitation and a latent defect.
