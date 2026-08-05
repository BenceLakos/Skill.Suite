#!/usr/bin/env python3
"""Generate the judge's marking-map.json from the marking scheme.

    python3 make-marking-map.py <marking-scheme.xlsx> --out marking-map.json
    python3 make-marking-map.py <marking-scheme.xlsx> --out marking-map.json --check

Generating rather than transcribing is the whole anti-drift mechanism: the sheet is the single source
of truth for ids and labels, so they cannot diverge. `--check` exits non-zero when the aspect set
would change, which turns that into a gate for the revision loop.

`parts` and `scoring` are carried over from an existing file — they are black-box scoring dials the
sheet knows nothing about, and regenerating must never silently reset them.
"""

from __future__ import annotations

import argparse
import json
import pathlib
import re

from schemelib import read_scheme

DEFAULT_SCORING = {
    "coverageFloor": 30,
    "coverageCeil": 95,
    "mutationFloor": 20,
    "mutationCeil": 90,
    "coverageWeight": 0.30,
    "coreWeight": 0.70,
}


def strip_jsonc(text: str) -> str:
    """Drop // comments so an existing JSONC map can be parsed. Tolerates // inside strings."""
    out = []
    for line in text.splitlines():
        in_string = False
        escaped = False
        cut = None
        for i, ch in enumerate(line):
            if escaped:
                escaped = False
                continue
            if ch == "\\":
                escaped = True
            elif ch == '"':
                in_string = not in_string
            elif ch == "/" and not in_string and i + 1 < len(line) and line[i + 1] == "/":
                cut = i
                break
        out.append(line[:cut] if cut is not None else line)
    return "\n".join(out)


def load_existing(path: pathlib.Path) -> dict:
    if not path.exists():
        return {}
    try:
        return json.loads(strip_jsonc(path.read_text()))
    except json.JSONDecodeError as exc:
        raise SystemExit(f"{path}: existing file is not valid JSON(C): {exc}")


def render(aspects: list[tuple[str, str]], parts, scoring, source: str) -> str:
    width = max((len(json.dumps(i)) for i, _ in aspects), default=8) + 1
    lines = [
        "{",
        '  "protocol": 1,',
        "",
        f"  // Generated from {source} — do not edit by hand. Regenerate with make-marking-map.py so",
        "  // the ids and labels here cannot drift from the marking scheme they came from.",
        "",
        f'  "parts": {json.dumps(parts)},',
        "",
        "  // Black-box scoring dials; ignored by a white-box session, which marks from aspects alone.",
        '  "scoring": {',
    ]
    keys = list(scoring)
    for i, key in enumerate(keys):
        comma = "," if i < len(keys) - 1 else ""
        lines.append(f'    "{key}": {json.dumps(scoring[key])}{comma}')
    lines += [
        "  },",
        "",
        "  // One row per aspect. Declaring the full set is what makes an aspect nobody attempted show",
        '  // up as a "no" row in `skill-marker report` rather than silently vanishing.',
        '  "aspects": [',
    ]
    for i, (aspect_id, label) in enumerate(aspects):
        comma = "," if i < len(aspects) - 1 else ""
        key = (json.dumps(aspect_id) + ",").ljust(width)
        lines.append(f'    {{ "id": {key} "label": {json.dumps(label, ensure_ascii=False)} }}{comma}')
    lines += ["  ]", "}", ""]
    return "\n".join(lines)


def main() -> int:
    ap = argparse.ArgumentParser(description="Generate marking-map.json from a marking scheme.")
    ap.add_argument("scheme")
    ap.add_argument("--out", required=True)
    ap.add_argument("--check", action="store_true",
                    help="do not write; exit 1 if the aspect set would change")
    args = ap.parse_args()

    scheme = read_scheme(args.scheme)
    out = pathlib.Path(args.out)

    aspects = [(a.id, scheme.test_map_lines.get(a.id) or a.description) for a in scheme.aspects]
    missing = [i for i, label in aspects if not label]
    if missing:
        raise SystemExit(f"aspects with no marking line to use as a label: {missing}")

    existing = load_existing(out)
    parts = existing.get("parts", [])
    scoring = existing.get("scoring", DEFAULT_SCORING)
    rendered = render(aspects, parts, scoring, pathlib.Path(args.scheme).name)

    if args.check:
        if not out.exists():
            print(f"make-marking-map: {out} does not exist")
            return 1
        current = [(a.get("id", ""), a.get("label", "")) for a in existing.get("aspects", [])]
        if current == aspects:
            print(f"make-marking-map: {out} is up to date ({len(aspects)} aspects)")
            return 0
        print(f"make-marking-map: {out} is STALE")
        old_ids = {i for i, _ in current}
        new_ids = {i for i, _ in aspects}
        for i in sorted(new_ids - old_ids):
            print(f"  + {i}")
        for i in sorted(old_ids - new_ids):
            print(f"  - {i}")
        old_labels = dict(current)
        for i, label in aspects:
            if i in old_labels and old_labels[i] != label:
                print(f"  ~ {i} label changed")
        return 1

    out.write_text(rendered)
    print(f"make-marking-map: wrote {out} ({len(aspects)} aspects)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
