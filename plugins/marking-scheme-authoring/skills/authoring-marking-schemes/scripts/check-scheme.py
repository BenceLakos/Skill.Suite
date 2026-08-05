#!/usr/bin/env python3
"""Validate a CIS marking scheme's arithmetic and internal consistency.

    python3 check-scheme.py <marking-scheme.xlsx> [--session-total 15.00] [--ladder 0.15,0.2,0.35,0.7]

Exits non-zero on any error, so it composes into the revision loop.

Totals are recomputed from the aspect rows rather than read from the reconciliation cells: those are
formulas, and a workbook openpyxl just wrote carries no cached values for them, so reading them would
pass a freshly scaffolded sheet vacuously.
"""

from __future__ import annotations

import argparse
import collections
from decimal import Decimal

from schemelib import ASPECT_ID, Findings, read_scheme


def main() -> int:
    ap = argparse.ArgumentParser(description="Validate a CIS marking scheme workbook.")
    ap.add_argument("scheme")
    ap.add_argument("--session-total", type=Decimal, default=None,
                    help="expected grand total; defaults to the sheet's own declared total")
    ap.add_argument("--ladder", default="0.15,0.2,0.35,0.7",
                    help="comma-separated permitted mark values; empty string disables the check")
    args = ap.parse_args()

    scheme = read_scheme(args.scheme)
    f = Findings(f"check-scheme: {args.scheme}")

    if not scheme.aspects:
        f.error("no aspect rows found — is column D populated with M or J?")
        return f.report()

    total = scheme.total()
    expected_total = args.session_total if args.session_total is not None else scheme.declared_total
    print(f"  {len(scheme.aspects)} aspects, {len(scheme.graded())} with a test, total {total}")

    # -- grand total -------------------------------------------------------------------------
    if expected_total and total != expected_total:
        f.error(f"aspect marks total {total}, expected {expected_total}")

    # -- criterion totals --------------------------------------------------------------------
    for criterion, computed in sorted(scheme.by_criterion().items()):
        declared = scheme.declared_criteria.get(criterion)
        if declared is None:
            # No cached value. If the cell is a formula pointing at this criterion's own block total,
            # the two cannot disagree — there is nothing to reconcile. Only a missing or misdirected
            # link is worth reporting.
            formula = scheme.criteria_formulas.get(criterion)
            target = scheme.block_total_cells.get(f"Criterion {criterion}")
            if formula and target and target in formula:
                continue
            if formula:
                f.error(f"criterion {criterion}: Criteria table holds {formula}, which does not "
                        f"reference its block total {target}")
            else:
                f.warn(f"criterion {criterion} has no row in the Criteria table")
            continue
        if declared != computed:
            f.error(f"criterion {criterion}: aspects sum to {computed}, Criteria table says {declared}")
    if scheme.declared_criteria:
        declared_sum = sum(scheme.declared_criteria.values(), Decimal(0))
        if expected_total and declared_sum != expected_total:
            f.error(f"Criteria table sums to {declared_sum}, expected {expected_total}")

    # -- WSOS sections, and variation --------------------------------------------------------
    for section, computed in sorted(scheme.by_wsos().items()):
        declared = scheme.declared_wsos.get(section)
        if declared is None:
            f.error(f"WSOS section {section} carries {computed} marks but is not declared")
        elif declared != computed:
            f.error(f"WSOS section {section}: aspects sum to {computed}, declared {declared} "
                    f"(variation {abs(declared - computed)}, must be 0)")
    for section, declared in sorted(scheme.declared_wsos.items()):
        if declared and section not in scheme.by_wsos():
            f.error(f"WSOS section {section} declares {declared} marks but no aspect uses it")

    # -- ids -----------------------------------------------------------------------------------
    seen = collections.Counter(a.id for a in scheme.aspects)
    for aspect_id, n in seen.items():
        if n > 1:
            f.error(f"aspect id {aspect_id} appears {n} times")
        if not ASPECT_ID.match(aspect_id):
            f.error(f"aspect id {aspect_id!r} is not of the form A1.1 — the Test Map may be short a row")
    for a in scheme.aspects:
        if a.id.startswith(a.subcrit + ".") is False and ASPECT_ID.match(a.id):
            f.error(f"{a.id} (sheet row {a.row}) sits in sub-criterion {a.subcrit}")
    per_sub: dict[str, list[int]] = collections.defaultdict(list)
    for a in scheme.aspects:
        if ASPECT_ID.match(a.id):
            per_sub[a.subcrit].append(int(a.id.split(".")[1]))
    for sub, numbers in sorted(per_sub.items()):
        if numbers != list(range(1, len(numbers) + 1)):
            f.error(f"sub-criterion {sub} ids are not 1..n in order: {numbers}")

    # -- every aspect row well-formed --------------------------------------------------------------
    ladder = [Decimal(x) for x in args.ladder.split(",") if x.strip()] if args.ladder else []
    for a in scheme.aspects:
        where = f"{a.id} (row {a.row})"
        if not a.description:
            f.error(f"{where} has no Aspect Description")
        if not a.expected:
            f.error(f"{where} has no Extra Aspect Description (the expected result)")
        if a.kind == "M" and not a.requirement:
            f.error(f"{where} is a measurement aspect with no Requirement (the input)")
        if not 1 <= a.wsos <= 6:
            f.error(f"{where} has WSOS section {a.wsos}, expected 1..6")
        if a.mark <= 0:
            f.error(f"{where} has mark {a.mark}")
        elif ladder and a.mark not in ladder:
            f.warn(f"{where} mark {a.mark} is off the ladder {args.ladder}")

    # -- Test Map agrees with the CIS sheet ----------------------------------------------------
    for a in scheme.aspects:
        line = scheme.test_map_lines.get(a.id)
        if line and a.description and line != a.description:
            f.error(f"{a.id}: Test Map marking line differs from CIS column E\n"
                    f"          CIS      : {a.description[:88]}\n"
                    f"          Test Map : {line[:88]}")
    extra = set(scheme.test_map) - {a.id for a in scheme.aspects}
    if extra:
        f.error(f"Test Map has rows with no aspect row on the CIS sheet: {sorted(extra)}")

    # -- declared formula ranges vs the real blocks --------------------------------------------
    rows = [a.row for a in scheme.aspects]
    if scheme.sumif_range:
        lo, hi = scheme.sumif_range
        if hi < max(rows):
            f.error(f"the WSOS SUMIF range stops at row {hi} but aspects run to {max(rows)} — "
                    f"marks below the range are silently ignored")
        elif hi < max(rows) + 20:
            f.warn(f"the WSOS SUMIF range ends at row {hi}, only {hi - max(rows)} rows past the last "
                   f"aspect; give it headroom so appended rows are counted")
    covered: set[int] = set()
    for label, (lo, hi) in scheme.block_sum_ranges.items():
        inside = [r for r in rows if lo <= r <= hi]
        covered.update(inside)
        if not inside:
            f.error(f"block total {label} covers rows {lo}..{hi}, which contain no aspect rows")
    if scheme.block_sum_ranges:
        missed = sorted(set(rows) - covered)
        if missed:
            f.error(f"aspect rows outside every block total range: {missed}")

    # NOTE: a "two aspects with the same expected result sit in different WSOS sections" heuristic
    # was tried here and removed. WSOS sections record the *skill being assessed*, not the output, so
    # two aspects legitimately share an expected value while sitting in different sections — a rule
    # implementation is Development, noticing an ordinal-comparison subtlety is Problem solving. On a
    # correct reference scheme it produced three false positives and no true ones. Misfiled sections
    # are a human review question; see SKILL.md section 3.

    return f.report()


if __name__ == "__main__":
    raise SystemExit(main())
