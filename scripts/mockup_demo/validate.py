"""CLI: validates nav wiring — target existence + BFS reachability + report.

Usage:
  python -m scripts.mockup_demo.validate [--report PATH]

Exits with code 0 if user-facing reachability >= 95%, else 1.
"""
from __future__ import annotations
from collections import deque
from pathlib import Path
import argparse
import re
import sys

REPO_ROOT = Path(__file__).resolve().parents[2]
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
DEFAULT_REPORT = REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-validation-report.html"

# Match href="x.html" but NOT href="#" or external URLs
_HREF_RE = re.compile(r'href\s*=\s*"([^"#][^"]*\.html)"', re.IGNORECASE)
# window.location.href = 'x.html' or window.location.replace('x.html')
_LOCATION_RE = re.compile(r"window\.location\.(?:href|replace)\s*=?\s*\(?\s*['\"]([^'\"]+\.html)['\"]")
# onclick="window.location.href='x.html'"
_ONCLICK_RE = re.compile(r'onclick\s*=\s*"[^"]*?([\w\-]+\.html)', re.IGNORECASE)
# DEMO-NAV-DYNAMIC: string literals in DEMO_NAV_DEST objects: 'x.html' or "x.html"
_NAVDEST_RE = re.compile(r"DEMO_NAV_DEST\s*=\s*\{[^}]*\}", re.DOTALL)
# DEMO-NAV-HINTS: explicit .html filenames listed in a comment hint
_NAVHINTS_RE = re.compile(r"DEMO-NAV-HINTS\s*:(.*?)(?:\*/|$)", re.DOTALL)

_HTML_STR_RE = re.compile(r"['\"]([a-zA-Z0-9_\-]+\.html)['\"]")
_HTML_WORD_RE = re.compile(r"\b([a-zA-Z0-9][a-zA-Z0-9_\-]*\.html)\b")


def _scan_jsx(text_jsx: str, targets: set[str]) -> None:
    """Scan a JSX/JS file for navigation targets using all strategies."""
    for rx in (_HREF_RE, _LOCATION_RE, _ONCLICK_RE):
        for m in rx.finditer(text_jsx):
            targets.add(m.group(1))
    # Extract destinations from DEMO_NAV_DEST lookup tables (dynamic nav iterators)
    for block_match in _NAVDEST_RE.finditer(text_jsx):
        block = block_match.group(0)
        for m in _HTML_STR_RE.finditer(block):
            targets.add(m.group(1))
    # Extract destinations from DEMO-NAV-HINTS comments
    for hint_match in _NAVHINTS_RE.finditer(text_jsx):
        hint_text = hint_match.group(1)
        for m in _HTML_WORD_RE.finditer(hint_text):
            targets.add(m.group(1))


def extract_targets(path: Path) -> set[str]:
    text = path.read_text(encoding="utf-8")
    targets = set()
    for rx in (_HREF_RE, _LOCATION_RE, _ONCLICK_RE):
        for m in rx.finditer(text):
            targets.add(m.group(1))
    # JSX sibling files: also scan the sibling .jsx file if this is an HTML mockup
    if path.suffix == ".html":
        jsx = path.with_suffix(".jsx")
        if jsx.exists():
            _scan_jsx(jsx.read_text(encoding="utf-8"), targets)
        # Also scan *-parts.jsx companions (e.g. sp4-session-live-parts.jsx for sp4-session-live.html)
        stem = path.stem
        parts_jsx = path.parent / f"{stem}-parts.jsx"
        if parts_jsx.exists():
            _scan_jsx(parts_jsx.read_text(encoding="utf-8"), targets)
    return targets


def bfs_reachable(mockups_dir: Path, entry: str) -> tuple[set[str], int]:
    all_html = {p.name for p in mockups_dir.glob("*.html")}
    visited: set[str] = set()
    q = deque([entry])
    while q:
        cur = q.popleft()
        if cur in visited or cur not in all_html:
            continue
        visited.add(cur)
        for tgt in extract_targets(mockups_dir / cur):
            if tgt not in visited:
                q.append(tgt)
    return visited, len(all_html)


def _broken_targets(mockups_dir: Path) -> list[tuple[str, str]]:
    """Return (source_file, missing_target) tuples."""
    all_html = {p.name for p in mockups_dir.glob("*.html")}
    broken = []
    for f in mockups_dir.glob("*.html"):
        for t in extract_targets(f):
            if t not in all_html:
                broken.append((f.name, t))
    return broken


# Files intentionally excluded from user-facing reachability calculation:
# - 00-05 design system docs (showcase pages, not part of user journey)
# - nanolith-nav-* primitives (UI showcases, not navigable destinations)
_EXCLUDED = {
    "00-hub.html",
    "01-screens.html",
    "02-desktop-patterns.html",
    "03-drawer-variants.html",
    "04-design-system.html",
    "05-dark-mode.html",
    "nanolith-nav-topbar.html",
    "nanolith-nav-bottom-mobile.html",
    "nanolith-nav-chat-panel.html",
}


def emit_report(mockups_dir: Path, report_path: Path) -> dict:
    reachable, total = bfs_reachable(mockups_dir, "index.html")
    all_html = {p.name for p in mockups_dir.glob("*.html")}
    orphans = sorted(all_html - reachable)
    broken = _broken_targets(mockups_dir)
    user_facing_total = len(all_html - _EXCLUDED)
    user_facing_reachable = len((all_html - _EXCLUDED) & reachable)
    pct = (user_facing_reachable / user_facing_total * 100) if user_facing_total else 0.0

    report_path.parent.mkdir(parents=True, exist_ok=True)
    with report_path.open("w", encoding="utf-8") as out:
        out.write("<!doctype html><html><head><meta charset='utf-8'><title>Mockup Demo Nav Validation</title>\n")
        out.write("<style>body{font-family:sans-serif;max-width:900px;margin:2em auto;padding:0 1em}"
                  "h2{margin-top:2em;border-bottom:1px solid #ccc}"
                  "code{background:#f4f4f4;padding:2px 4px;border-radius:3px}"
                  ".ok{color:#080}.warn{color:#c80}.bad{color:#c00}</style></head><body>\n")
        out.write("<h1>Mockup Demo Nav Validation</h1>\n")
        status_class = "ok" if pct >= 95.0 else "warn" if pct >= 80.0 else "bad"
        out.write(f"<p><strong>Reachable (user-facing):</strong> "
                  f"<span class='{status_class}'>{user_facing_reachable}/{user_facing_total} ({pct:.1f}%)</span></p>\n")
        out.write(f"<p><strong>Total HTML files:</strong> {total}</p>\n")
        out.write(f"<p><strong>Total reachable (incl. excluded):</strong> {len(reachable)}</p>\n")

        out.write("<h2>Orphans (unreachable)</h2><ul>\n")
        if not orphans:
            out.write("<li><em>None.</em></li>\n")
        for o in orphans:
            tag = " <em>(excluded from user-facing calc)</em>" if o in _EXCLUDED else ""
            out.write(f"<li><code>{o}</code>{tag}</li>\n")
        out.write("</ul>\n")

        out.write("<h2>Broken targets</h2><ul>\n")
        if not broken:
            out.write("<li><em>None.</em></li>\n")
        for src, tgt in broken:
            out.write(f"<li><code>{src}</code> &rarr; missing <code>{tgt}</code></li>\n")
        out.write("</ul></body></html>\n")
    return {
        "reachable_user_facing": user_facing_reachable,
        "total_user_facing": user_facing_total,
        "percent": pct,
        "orphans": len(orphans),
        "broken": len(broken),
    }


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--report", type=Path, default=DEFAULT_REPORT)
    p.add_argument("--mockups-dir", type=Path, default=MOCKUPS_DIR)
    args = p.parse_args(argv)
    stats = emit_report(args.mockups_dir, args.report)
    print(f"Stats: {stats}")
    print(f"Report written to {args.report}")
    return 0 if stats["percent"] >= 95.0 else 1


if __name__ == "__main__":
    sys.exit(main())
