#!/usr/bin/env python3
"""
Scan markdown files for broken relative-path links.

Used as the local equivalent of the CI Lychee gate, scoped to the
previously-excluded trees in `.github/workflows/docs-linkcheck.yml`
(see issue #1014).

Output: tab-separated `source_file<TAB>target<TAB>resolved_path<TAB>category`,
sorted by source file. Empty exit on zero broken links.

Categories:
  - RENAMED   : a file with the same basename exists elsewhere → likely
                a docs reorg; can usually be sed-fixed.
  - DELETED   : target name matches a known cleanup pattern (qdrant, alpha,
                v2-mockup) → reference is stale, remove.
  - ORPHAN    : neither — human triage required.

Usage:
  python3 scripts/quality/scan-broken-links.py [TREE...]

If no TREE arg given, defaults to the 6 trees from issue #1014 plus
`docs/for-developers/frontend/` for spot-coverage.
"""
from __future__ import annotations

import os
import re
import sys
from collections import defaultdict
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

DEFAULT_TREES = [
    "docs/for-developers/deployment",
    "docs/for-developers/security",
    "docs/for-developers/testing",
    "docs/for-developers/plans",
    "docs/for-claude",
    "docs/superpowers",
]

# Markdown relative-link regex. Captures `]( <target> [#fragment] )`.
# Excludes: external URLs, mailto, pure anchors (no path).
LINK_RE = re.compile(r"\]\((?!https?://|mailto:|tel:|#)([^)#\s]+)(#[^)]*)?\)")

# Fenced code block (``` ... ```) — multiline.
CODE_FENCE_RE = re.compile(r"```.*?```", re.DOTALL)
# Inline code (`...`) — single line, non-greedy.
INLINE_CODE_RE = re.compile(r"`[^`\n]+`")


def strip_code_regions(text: str) -> str:
    """Remove fenced + inline code so link-regex doesn't match inside them.

    We preserve original byte positions by replacing with same-length space
    runs; the LINK_RE only cares about its own match content, not positions.
    """
    text = CODE_FENCE_RE.sub(lambda m: " " * len(m.group(0)), text)
    text = INLINE_CODE_RE.sub(lambda m: " " * len(m.group(0)), text)
    return text

DELETED_PATTERNS = (
    "qdrant",
    "alpha-mode",
    "alpha_mode",
    "ALPHA_MODE",
    "compose.alpha",
)


EXCLUDED_DIR_PARTS = {
    ".docs-archive",
    "archive",
    "node_modules",
    ".next",
    ".git",
    ".worktrees",
    "dist",
    "build",
    ".turbo",
    "__pycache__",
    ".venv",
    "venv",
}


def is_excluded_path(p: Path) -> bool:
    """Skip archives, node_modules-like dirs, build artefacts."""
    return any(part in EXCLUDED_DIR_PARTS for part in p.parts)


def classify(target: str, basename_index: dict[str, list[str]]) -> str:
    target_basename = Path(target).name
    matches = basename_index.get(target_basename, [])
    if matches:
        return f"RENAMED:{matches[0]}"
    if any(p in target.lower() for p in DELETED_PATTERNS):
        return "DELETED"
    return "ORPHAN"


def build_basename_index(root: Path) -> dict[str, list[str]]:
    """Index basenames under docs/ + root-level *.md (avoids walking node_modules)."""
    idx: dict[str, list[str]] = defaultdict(list)
    # Walk only docs/
    for p in (root / "docs").rglob("*"):
        if p.is_file() and not is_excluded_path(p):
            idx[p.name].append(str(p.relative_to(root)).replace(os.sep, "/"))
    # Plus root-level markdowns (CLAUDE.md, README.md, etc.)
    for p in root.glob("*.md"):
        if p.is_file():
            idx[p.name].append(str(p.relative_to(root)).replace(os.sep, "/"))
    return idx


def scan(trees: list[str]) -> list[tuple[str, str, str, str]]:
    basename_index = build_basename_index(REPO_ROOT)
    broken: list[tuple[str, str, str, str]] = []
    for tree in trees:
        root = REPO_ROOT / tree
        if not root.is_dir():
            continue
        for md in root.rglob("*.md"):
            if is_excluded_path(md):
                continue
            raw = md.read_text(encoding="utf-8", errors="ignore")
            text = strip_code_regions(raw)
            for match in LINK_RE.finditer(text):
                target = match.group(1).strip()
                if not target or target.startswith("/"):
                    continue
                resolved = (md.parent / target).resolve()
                if resolved.exists():
                    continue
                category = classify(target, basename_index)
                source_rel = str(md.relative_to(REPO_ROOT)).replace(os.sep, "/")
                broken.append((source_rel, target, str(resolved.relative_to(REPO_ROOT) if resolved.is_relative_to(REPO_ROOT) else resolved), category))
    return sorted(broken)


def main() -> int:
    trees = sys.argv[1:] or DEFAULT_TREES
    broken = scan(trees)
    if not broken:
        print("0 broken relative links across:", " ".join(trees))
        return 0
    print(f"# {len(broken)} broken relative links")
    print("source\ttarget\tresolved\tcategory")
    for src, tgt, res, cat in broken:
        print(f"{src}\t{tgt}\t{res}\t{cat}")
    return 1 if any(c == "ORPHAN" or c == "DELETED" or c.startswith("RENAMED") for (_, _, _, c) in broken) else 0


if __name__ == "__main__":
    sys.exit(main())
