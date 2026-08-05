#!/usr/bin/env python3
"""Scaffold a CIS marking scheme workbook from an aspect list.

    python3 scaffold-scheme.py <aspects.csv> --out marking-scheme.xlsx \
            --title "09_SWAD Software Applications Development" --session-total 15.00

The CSV columns, in any order:

    aspect_id,criterion,criterion_name,subcrit,subcrit_name,day,type,
    description,expected,requirement,wsos,mark,test

`type` is M or J. `test` is the xUnit method name, or empty for a pipeline-judged aspect. Rows must be
in the order the sheet should list them — aspect ids are positional on the CIS sheet.

Every SUM and SUMIF range is computed from the block boundaries this run actually produced. A
hand-written range that stops one row short is invisible: the sheet still totals *something*.
"""

from __future__ import annotations

import argparse
import csv
import pathlib
from collections import OrderedDict
from decimal import Decimal

try:
    import openpyxl
    from openpyxl.styles import Alignment, Font
except ImportError:
    raise SystemExit("openpyxl is required:  pip3 install openpyxl")

HEADERS = [
    "Sub\nCriterion\nID", "Sub Criterion\nName or Description", "Day of Marking",
    "Aspect\nType\nM = Meas\nJ = Judg", "Aspect - Description", "Judg Score",
    "Extra Aspect Description (Meas or Judg)\nOR\nJudgement Score Description (Judg only)",
    "Requirement\n(Measurement Only)", "WSOS Section", "Calculation Row \n(Export only)", "Max\nMark",
]
WSOS_NAMES = {
    1: "Work organization and self-management",
    2: "Communication and interpersonal skills",
    3: "Problem solving",
    4: "Analysis and design of software applications",
    5: "Development of software applications",
    6: "Testing software applications",
}
SUMIF_HEADROOM = 300


def read_rows(path: str) -> list[dict]:
    rows = [r for r in csv.DictReader(pathlib.Path(path).read_text().splitlines())
            if (r.get("aspect_id") or "").strip()]
    if not rows:
        raise SystemExit(f"{path}: no aspect rows")
    required = {"aspect_id", "criterion", "subcrit", "type", "description", "expected", "wsos", "mark"}
    missing = required - set(rows[0])
    if missing:
        raise SystemExit(f"{path}: missing columns {sorted(missing)}")
    return rows


def main() -> int:
    ap = argparse.ArgumentParser(description="Scaffold a CIS marking scheme workbook.")
    ap.add_argument("aspects")
    ap.add_argument("--out", required=True)
    ap.add_argument("--title", default="Marking scheme")
    ap.add_argument("--session-total", type=Decimal, default=None)
    ap.add_argument("--force", action="store_true")
    args = ap.parse_args()

    out = pathlib.Path(args.out)
    if out.exists() and not args.force:
        raise SystemExit(f"{out} exists; pass --force to overwrite")

    rows = read_rows(args.aspects)
    total = sum((Decimal(str(r["mark"])) for r in rows), Decimal(0))
    if args.session_total is not None and total != args.session_total:
        raise SystemExit(f"aspect marks total {total}, --session-total says {args.session_total}")

    criteria: "OrderedDict[str, str]" = OrderedDict()
    for r in rows:
        criteria.setdefault(r["criterion"], r.get("criterion_name", "") or r["criterion"])

    wb = openpyxl.Workbook()
    ws = wb.active
    ws.title = "CIS Marking Scheme Import"
    bold = Font(bold=True)

    ws.cell(1, 1, args.title).font = bold
    ws.cell(3, 1, "WorldSkills Occupational Standards").font = bold
    for col, label in ((1, "Section"), (2, "WSOS Marks"), (9, "WSOS Marks"),
                       (10, "Aspect Marks"), (11, "Variation")):
        ws.cell(4, col, label).font = bold

    used_wsos = {int(r["wsos"]) for r in rows}
    for i in range(1, 7):
        r = 4 + i
        ws.cell(r, 1, i)
        ws.cell(r, 2, WSOS_NAMES[i])
        declared = sum((Decimal(str(x["mark"])) for x in rows if int(x["wsos"]) == i), Decimal(0))
        ws.cell(r, 9, float(declared) if i in used_wsos else 0)
    ws.cell(11, 9, "Total Variation").font = bold

    # Blocks first: the tables above reference their totals, so the row numbers must be known.
    block_rows: dict[str, tuple[int, int, int]] = {}   # criterion -> (header, first, last)
    cursor = 27
    for criterion, name in criteria.items():
        header = cursor
        for c, label in enumerate(HEADERS, start=1):
            cell = ws.cell(header, c, label)
            cell.font = bold
            cell.alignment = Alignment(wrap_text=True, vertical="top")
        ws.cell(header, 12, f"Criterion {criterion}").font = bold
        ws.cell(header, 13, "Total\nMark").font = bold

        r = header + 1
        first_aspect = None
        current_sub = None
        for row in [x for x in rows if x["criterion"] == criterion]:
            if row["subcrit"] != current_sub:
                current_sub = row["subcrit"]
                ws.cell(r, 1, current_sub)
                ws.cell(r, 2, row.get("subcrit_name", ""))
                ws.cell(r, 3, int(row["day"]) if (row.get("day") or "").strip() else 1)
                r += 1
            ws.cell(r, 4, row["type"].strip().upper())
            ws.cell(r, 5, row["description"])
            ws.cell(r, 7, row["expected"])
            ws.cell(r, 8, row.get("requirement", ""))
            ws.cell(r, 9, int(row["wsos"]))
            ws.cell(r, 10, 1)
            mark = ws.cell(r, 11, float(Decimal(str(row["mark"]))))
            mark.number_format = "0.00"
            for c in (5, 7, 8):
                ws.cell(r, c).alignment = Alignment(wrap_text=True, vertical="top")
            first_aspect = first_aspect or r
            r += 1

        last = r - 1
        block_rows[criterion] = (header, header + 1, last)
        ws.cell(header, 14, f"=SUM(K{header + 1}:K{last})").number_format = "0.00"
        cursor = last + 3

    last_aspect_row = max(last for _, _, last in block_rows.values())
    sumif_end = last_aspect_row + SUMIF_HEADROOM
    for i in range(1, 7):
        r = 4 + i
        ws.cell(r, 10, f"=SUMIF($I${27}:$I${sumif_end}, A{r}, $K${27}:$K${sumif_end})")
        ws.cell(r, 11, f"=ABS(I{r}-J{r})")
    ws.cell(11, 11, "=SUM(K5:K10)")

    ws.cell(14, 1, "Criteria").font = bold
    ws.cell(15, 1, "ID").font = bold
    ws.cell(15, 2, "Name").font = bold
    ws.cell(15, 11, "Mark").font = bold
    r = 16
    for criterion, name in criteria.items():
        header, _, _ = block_rows[criterion]
        ws.cell(r, 1, criterion)
        ws.cell(r, 2, name)
        ws.cell(r, 11, f"=N{header}").number_format = "0.00"
        r += 1
    ws.cell(r, 2, "Session total").font = bold
    ws.cell(r, 11, "+".join(f"N{block_rows[c][0]}" for c in criteria).join(("=", ""))
            ).number_format = "0.00"

    for col, width in (("A", 12), ("B", 26), ("C", 8), ("D", 8), ("E", 60),
                       ("F", 10), ("G", 60), ("H", 60), ("I", 12), ("J", 12), ("K", 8)):
        ws.column_dimensions[col].width = width

    tm = wb.create_sheet("Test Map")
    for c, label in enumerate(["Aspect ID", "Criterion", "Sub-crit.", "Sub-criterion name",
                               "Aspect (marking line)",
                               "xUnit test method (bound via [Aspect] attribute)", "WSOS", "Mark"], 1):
        tm.cell(1, c, label).font = bold
    for i, row in enumerate(rows, start=2):
        tm.cell(i, 1, row["aspect_id"])
        tm.cell(i, 2, row["criterion"])
        tm.cell(i, 3, row["subcrit"])
        tm.cell(i, 4, row.get("subcrit_name", ""))
        tm.cell(i, 5, row["description"])
        tm.cell(i, 6, row.get("test", ""))
        tm.cell(i, 7, int(row["wsos"]))
        tm.cell(i, 8, float(Decimal(str(row["mark"])))).number_format = "0.00"
    total_row = len(rows) + 2
    tm.cell(total_row, 5, "Total").font = bold
    tm.cell(total_row, 8, float(total)).number_format = "0.00"
    tm.cell(total_row + 2, 1,
            "Contract: 1 aspect = exactly 1 automated test case (and vice versa). Grading binds each "
            "test to its aspect via the [Aspect(\"id\")] attribute; method names carry no aspect-id "
            "prefix and renaming a method does not move a mark. Every test asserts the whole output, so "
            "aspect distinctness comes from the inputs, not from omitted assertions.")
    for col, width in (("A", 10), ("B", 10), ("C", 10), ("D", 34), ("E", 70), ("F", 46), ("G", 8), ("H", 8)):
        tm.column_dimensions[col].width = width

    calc = wb.create_sheet("Calculations")
    graded = [r for r in rows if (r.get("test", "") or "").strip()
              and not (r.get("test", "") or "").startswith("(")]
    for i, (label, value) in enumerate([
        ("Session total marks", float(total)),
        ("Measurement aspects (= automated checks)", len(rows)),
        ("xUnit test cases (aspects minus pipeline checks)", len(graded)),
        ("WSOS variation (must be 0)", 0),
    ], start=1):
        calc.cell(i, 1, label)
        calc.cell(i, 2, value)
    calc.column_dimensions["A"].width = 48

    wb.save(out)
    print(f"scaffold-scheme: wrote {out}")
    print(f"  {len(rows)} aspects, {len(graded)} with a test, total {total}")
    print(f"  blocks: " + ", ".join(f"{c} rows {f}..{l}" for c, (_, f, l) in block_rows.items()))
    print(f"  SUMIF range I27:I{sumif_end} ({SUMIF_HEADROOM} rows of headroom past the last aspect)")
    print("  now run check-scheme.py against it")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
