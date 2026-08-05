#!/usr/bin/env python3
"""Score a judged run against the marking scheme.

    python3 score-report.py <marking-scheme.xlsx> <cis-report.csv> [--label complete]
    python3 score-report.py <scheme.xlsx> <defect.csv> --compare <clean.csv>
    python3 score-report.py <scheme.xlsx> <degenerate.csv> --free-list

`cis-report.csv` is what `skill-marker report` emits: aspect_id,result,tests_matched,tests_passed.

`--compare` prints the symmetric difference against another run — the tool for "this seeded defect
fails only its own aspects". `--free-list` prints what a run passed, which is how the scheme's floor
gets measured with the degenerate probe.
"""

from __future__ import annotations

import argparse
import csv
import pathlib
from decimal import Decimal

from schemelib import read_scheme

TWO = Decimal("0.01")


def q(value: Decimal) -> str:
    """Marks always at two decimals, so 0.8 and 0.80 do not look like different numbers."""
    return str(value.quantize(TWO))


def load_report(path: str) -> dict[str, bool]:
    rows = list(csv.DictReader(pathlib.Path(path).read_text().splitlines()))
    if not rows or "aspect_id" not in rows[0]:
        raise SystemExit(f"{path}: not a skill-marker report (want aspect_id,result,...)")
    return {r["aspect_id"].strip(): r["result"].strip().lower() == "yes" for r in rows}


def main() -> int:
    ap = argparse.ArgumentParser(description="Score a CIS report against the marking scheme.")
    ap.add_argument("scheme")
    ap.add_argument("report")
    ap.add_argument("--label", default=None)
    ap.add_argument("--compare", default=None, help="another report to diff against")
    ap.add_argument("--free-list", action="store_true", help="list the aspects this run passed")
    args = ap.parse_args()

    scheme = read_scheme(args.scheme)
    marks = {a.id: a.mark for a in scheme.aspects}
    criterion = {a.id: a.criterion for a in scheme.aspects}
    result = load_report(args.report)

    unknown = sorted(set(result) - set(marks))
    if unknown:
        print(f"  warn  report has aspects absent from the scheme: {unknown}")
    absent = sorted(set(marks) - set(result))
    if absent:
        print(f"  warn  scheme has aspects absent from the report: {absent}")

    passed = [i for i, ok in result.items() if ok and i in marks]
    by_crit: dict[str, Decimal] = {}
    for i in passed:
        by_crit[criterion[i]] = by_crit.get(criterion[i], Decimal(0)) + marks[i]
    total = sum(by_crit.values(), Decimal(0))

    pipeline = {a.id for a in scheme.aspects if a.is_pipeline_judged}
    ceiling = sum((m for i, m in marks.items() if i not in pipeline), Decimal(0))

    label = args.label or pathlib.Path(args.report).parent.name or "run"
    print(f"\n  {label}: {len(passed)}/{len(marks)} aspects")
    for c in sorted(set(criterion.values())):
        earned = by_crit.get(c, Decimal(0))
        possible = sum((m for i, m in marks.items() if criterion[i] == c), Decimal(0))
        print(f"    criterion {c}: {q(earned)} / {q(possible)}")
    print(f"    TOTAL      : {q(total)} / {q(scheme.total())}")
    print(f"    automated ceiling {q(ceiling)} "
          f"(pipeline-judged aspects hold the remaining {q(scheme.total() - ceiling)})")

    if total == ceiling:
        print("    -> hits the automated ceiling exactly")

    failed = sorted(i for i in marks if i not in pipeline and not result.get(i, False))
    if failed:
        print(f"    failed: {', '.join(failed)}")

    if args.free_list:
        print(f"\n  aspects this run passed ({len(passed)}), i.e. the floor if the run computes nothing:")
        for i in sorted(passed):
            print(f"    {i}  {q(marks[i])}")

    if args.compare:
        other = load_report(args.compare)
        mine = {i for i, ok in result.items() if not ok}
        theirs = {i for i, ok in other.items() if not ok}
        only_mine = sorted(mine - theirs)
        only_theirs = sorted(theirs - mine)
        print(f"\n  vs {args.compare}:")
        print(f"    failing only here : {only_mine or '— none —'}")
        print(f"    failing only there: {only_theirs or '— none —'}")
        cost = sum((marks[i] for i in only_mine if i in marks), Decimal(0))
        print(f"    marks lost only here: {q(cost)}")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
