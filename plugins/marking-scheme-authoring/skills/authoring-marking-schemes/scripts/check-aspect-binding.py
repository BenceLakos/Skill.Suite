#!/usr/bin/env python3
"""Check that the marking scheme and the hidden suite describe the same set of aspects.

    python3 check-aspect-binding.py <marking-scheme.xlsx> <path/to/Session.UnitTests>

Grading binds a test to its aspect through the [Aspect("id")] attribute, so this is the check that
catches the sheet and the suite drifting apart — which they do on every revision.

Exits non-zero on any mismatch.
"""

from __future__ import annotations

import argparse
import collections
import pathlib
import re

from schemelib import Findings, read_scheme

# [Aspect("C2.3", CompetitorVisible = true)]  [Fact]  public void Scenario_Expected()
ASPECT_DECL = re.compile(
    r'\[Aspect\("(?P<id>[^"]+)"(?P<visible>[^\]]*CompetitorVisible\s*=\s*true)?[^\]]*\)\]'
    r'(?:\s*\[[^\]]*\])*\s*public\s+(?:async\s+)?(?:void|Task)\s+(?P<name>\w+)\s*\(',
    re.MULTILINE,
)
ID_PREFIXED = re.compile(r"^[A-Z]\d+_\d+_")


def main() -> int:
    ap = argparse.ArgumentParser(description="Cross-check scheme aspects against [Aspect] attributes.")
    ap.add_argument("scheme")
    ap.add_argument("suite", help="the *.UnitTests directory")
    ap.add_argument("--pipeline-aspect", action="append", default=[],
                    help="aspect id judged by the pipeline, not a test (repeatable); "
                         "defaults to those with no test named in the Test Map")
    args = ap.parse_args()

    scheme = read_scheme(args.scheme)
    suite_dir = pathlib.Path(args.suite)
    if not suite_dir.is_dir():
        raise SystemExit(f"not a directory: {suite_dir}")

    f = Findings(f"check-aspect-binding: {args.scheme}  <->  {args.suite}")

    found: dict[str, list[tuple[str, str, bool]]] = collections.defaultdict(list)
    per_file: dict[str, list[str]] = collections.defaultdict(list)
    for path in sorted(suite_dir.rglob("*.cs")):
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in ASPECT_DECL.finditer(text):
            found[m.group("id")].append((m.group("name"), path.name, bool(m.group("visible"))))
            per_file[path.name].append(m.group("name"))

    pipeline = set(args.pipeline_aspect) or {a.id for a in scheme.aspects if a.is_pipeline_judged}
    expected = {a.id: a.test for a in scheme.aspects if a.id not in pipeline}

    visible = sum(1 for occurrences in found.values() for _, _, v in occurrences if v)
    print(f"  scheme: {len(scheme.aspects)} aspects ({len(pipeline)} pipeline-judged)")
    print(f"  suite : {len(found)} aspect ids across {len(per_file)} fixtures, {visible} CompetitorVisible")

    missing = sorted(set(expected) - set(found))
    if missing:
        f.error(f"in the scheme, no test claims them: {missing}")

    extra = sorted(set(found) - set(expected) - pipeline)
    if extra:
        f.error(f"claimed by a test but absent from the scheme: {extra}")

    claimed_by_test = sorted(set(found) & pipeline)
    if claimed_by_test:
        f.error(f"pipeline-judged aspects must not be claimed by a test: {claimed_by_test}")

    for aspect_id, occurrences in sorted(found.items()):
        if len(occurrences) > 1:
            where = ", ".join(f"{n} ({fn})" for n, fn, _ in occurrences)
            f.error(f"{aspect_id} is claimed by {len(occurrences)} tests: {where}")

    for aspect_id, declared in sorted(expected.items()):
        if aspect_id not in found:
            continue
        actual = found[aspect_id][0][0]
        if declared and declared != actual:
            f.error(f"{aspect_id}: Test Map says {declared!r}, suite has {actual!r}")
        if not declared:
            f.warn(f"{aspect_id}: no test method named in the Test Map (suite has {actual!r})")

    for aspect_id, occurrences in sorted(found.items()):
        name = occurrences[0][0]
        if ID_PREFIXED.match(name):
            f.warn(f"{aspect_id}: method {name!r} still carries an aspect-id prefix — grading binds "
                   f"through the attribute, so the prefix is a second copy nothing keeps in step")

    all_names = [n for names in per_file.values() for n in names]
    for name, n in collections.Counter(all_names).items():
        if n > 1:
            f.error(f"method name {name!r} is used {n} times across fixtures")

    return f.report()


if __name__ == "__main__":
    raise SystemExit(main())
