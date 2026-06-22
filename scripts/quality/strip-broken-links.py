#!/usr/bin/env python3
"""
One-shot helper: rewrite `[Text](broken.md)` to `**Text** _(planned)_`
for a specific list of (source_file, target) pairs from the triage TSV.

Reads stdin lines of form `source<TAB>target<TAB>...` (e.g., subset of
the scan-broken-links.py output).

Skips fragment-only targets and external URLs (they wouldn't be in the
triage anyway).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]


def rewrite_file(file: Path, targets: set[str]) -> int:
    """Rewrite all `[Text](t)` where t in `targets` → `**Text** _(planned)_`."""
    text = file.read_text(encoding="utf-8")
    new_text = text
    count = 0
    for target in targets:
        # Escape regex specials
        esc = re.escape(target)
        # `[Text](target)` or `[Text](target#frag)`
        pattern = re.compile(r"\[([^\]]+)\]\(" + esc + r"(#[^)]*)?\)")
        def repl(m: re.Match) -> str:
            nonlocal count
            count += 1
            return f"**{m.group(1)}** _(planned)_"
        new_text = pattern.sub(repl, new_text)
    if count:
        file.write_text(new_text, encoding="utf-8")
    return count


def main() -> int:
    by_file: dict[str, set[str]] = {}
    for line in sys.stdin:
        line = line.rstrip("\n")
        if not line or line.startswith("#") or line.startswith("source\t"):
            continue
        parts = line.split("\t")
        if len(parts) < 2:
            continue
        src, tgt = parts[0], parts[1]
        by_file.setdefault(src, set()).add(tgt)

    total = 0
    for src, targets in by_file.items():
        path = REPO_ROOT / src
        if not path.exists():
            print(f"SKIP missing source: {src}", file=sys.stderr)
            continue
        n = rewrite_file(path, targets)
        if n:
            print(f"{src}: {n} rewrites")
        total += n
    print(f"TOTAL: {total} broken links converted to plain text", file=sys.stderr)
    return 0


if __name__ == "__main__":
    sys.exit(main())
