"""CLI: reads docs/superpowers/specs/nav-map.md and patches mockup files.

Usage:
  python -m scripts.mockup_demo.apply_map [--dry-run] [--map PATH]
"""
from __future__ import annotations
from pathlib import Path
import argparse
import re
import sys

from scripts.mockup_demo.html_patcher import patch_html_element
from scripts.mockup_demo.jsx_patcher import patch_jsx_element
from scripts.mockup_demo.clickable_extractor import extract_clickables

REPO_ROOT = Path(__file__).resolve().parents[2]
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
DEFAULT_MAP = REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-map.md"

# Markdown row pattern: | `file` | `selector` | text | `destination` | ...
_ROW_RE = re.compile(
    r"\|\s*`([^`]+)`\s*\|\s*`([^`]+)`\s*\|\s*([^|]*?)\s*\|\s*`([^`]+)`\s*\|"
)


def parse_nav_map(map_path: Path) -> list[tuple[str, str, str, str]]:
    """Returns list of (filename, selector, text, destination) tuples.

    Skips rows where destination is 'TODO', 'OUT_OF_SCOPE', or the literal 'Destination' (header).
    """
    rows = []
    for line in map_path.read_text(encoding="utf-8").splitlines():
        m = _ROW_RE.match(line.strip())
        if not m:
            continue
        fname, selector, text, dest = m.groups()
        dest = dest.strip()
        if dest in ("TODO", "OUT_OF_SCOPE", "Destination"):
            continue
        rows.append((fname.strip(), selector.strip(), text.strip(), dest))
    return rows


def _find_snippet(file_path: Path, selector: str) -> str | None:
    """Selector format: 'tag:Lline:text-prefix'. Re-extract and match by text prefix."""
    parts = selector.split(":", 2)
    if len(parts) < 3:
        return None
    text_prefix = parts[2].strip()
    # If the prefix is empty (icon-only buttons), we can't locate uniquely; skip
    if not text_prefix:
        return None
    for click in extract_clickables(file_path):
        click_text = (click.text or "").strip()
        if click_text.startswith(text_prefix) or text_prefix in click_text[:50]:
            return click.snippet
    return None


def apply_map(map_path: Path, mockups_dir: Path, dry_run: bool = False) -> dict:
    stats = {
        "patched": 0,
        "skipped_idempotent": 0,
        "missing_file": 0,
        "missing_snippet": 0,
        "out_of_scope_in_data": 0,
    }
    rows = parse_nav_map(map_path)
    print(f"Parsed {len(rows)} actionable rows from {map_path.name}")
    for fname, selector, _text, dest in rows:
        fpath = mockups_dir / fname
        if not fpath.exists():
            stats["missing_file"] += 1
            continue
        snippet = _find_snippet(fpath, selector)
        if not snippet:
            stats["missing_snippet"] += 1
            continue
        if dry_run:
            print(f"DRY-RUN would patch {fname}: {selector} -> {dest}".encode(sys.stdout.encoding, errors="replace").decode(sys.stdout.encoding))
            stats["patched"] += 1
            continue
        if fpath.suffix == ".jsx":
            patched = patch_jsx_element(fpath, snippet, dest)
        else:
            patched = patch_html_element(fpath, snippet, dest)
        if patched:
            stats["patched"] += 1
        else:
            stats["skipped_idempotent"] += 1
    return stats


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--map", type=Path, default=DEFAULT_MAP)
    p.add_argument("--mockups-dir", type=Path, default=MOCKUPS_DIR)
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args(argv)
    stats = apply_map(args.map, args.mockups_dir, args.dry_run)
    print(f"Stats: {stats}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
