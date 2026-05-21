"""Gap analysis: compare nav-map.md destinations against Next.js app routes.

Usage:
    python scripts/v2_audit/gap_analysis.py

Outputs a summary table and exits with code 0. Results are also written to
docs/superpowers/specs/route-gap-analysis.md (regenerated each run).
"""
from __future__ import annotations

import re
import sys
from pathlib import Path

# ---------------------------------------------------------------------------
# Resolve repo root and import nav_dimension helpers
# ---------------------------------------------------------------------------
_REPO_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(_REPO_ROOT))

from scripts.v2_audit.nav_dimension import _mockup_to_route, _route_to_filesystem_path  # noqa: E402

NAV_MAP_PATH = _REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-map.md"

# Backtick-quoted 4th column: | `file` | `selector` | text | `destination` | ...
_ROW_RE = re.compile(
    r"\|\s*`([^`]+)`\s*\|\s*`([^`]+)`\s*\|\s*([^|]*?)\s*\|\s*`([^`]+)`\s*\|"
)

# Routes where the heuristic produces a known-singular form but the real plural
# route already exists (we resolve these to the correct plural path).
_SINGULAR_TO_PLURAL: dict[str, str] = {
    "/game/[id]": "/games/[id]",
    "/agent/[id]": "/agents/[id]",
    "/toolkit/[id]": "/toolkits/[id]",
    "/kb/[id]": "/knowledge-base/[id]",
    "/kb/[id]/pdf": "/knowledge-base/[id]/pdf",
}

# Public-feeling segments that should live in (public)
_PUBLIC_PREFIXES = frozenset(
    {"join", "about", "faq", "legal", "terms", "privacy", "shared", "how-it-works"}
)


def _parent_route(route: str) -> str:
    """Return parent route (one segment up) or /dashboard for authenticated roots."""
    parts = route.rstrip("/").rsplit("/", 1)
    if len(parts) == 2 and parts[0]:
        return parts[0]
    return "/dashboard"


def _route_group(route: str) -> str:
    first_segment = route.lstrip("/").split("/")[0]
    if first_segment in _PUBLIC_PREFIXES:
        return "(public)"
    return "(authenticated)"


def collect_destinations(nav_map: Path) -> dict[str, int]:
    """Return {mockup_name: reference_count}."""
    counts: dict[str, int] = {}
    for line in nav_map.read_text(encoding="utf-8").splitlines():
        m = _ROW_RE.match(line.strip())
        if not m:
            continue
        dest = m.group(4).strip()
        if dest in ("TODO", "OUT_OF_SCOPE", "Destination"):
            continue
        # Normalise away extensions
        key = dest.replace(".html", "").replace(".jsx", "")
        counts[key] = counts.get(key, 0) + 1
    return counts


def analyse(dest_counts: dict[str, int]) -> tuple[list, list, list]:
    """
    Returns (existing, missing, unmappable).
    Each entry in existing/missing is a dict:
        dest, heuristic_route, canonical_route, count, exists, heuristic_note
    Each entry in unmappable is a dict: dest, count, reason
    """
    existing: list[dict] = []
    missing: list[dict] = []
    unmappable: list[dict] = []

    for dest, count in sorted(dest_counts.items()):
        heuristic_route = _mockup_to_route(dest)
        if heuristic_route is None:
            unmappable.append({"dest": dest, "count": count, "reason": "_PLANNED_DESTS or no heuristic match"})
            continue

        canonical_route = _SINGULAR_TO_PLURAL.get(heuristic_route, heuristic_route)
        heuristic_note = (
            f"heuristic → {heuristic_route} (corrected to plural)"
            if canonical_route != heuristic_route
            else ""
        )

        fs_path = _route_to_filesystem_path(canonical_route)
        row = {
            "dest": dest,
            "heuristic_route": heuristic_route,
            "canonical_route": canonical_route,
            "count": count,
            "exists": fs_path is not None,
            "heuristic_note": heuristic_note,
            "fs_path": str(fs_path) if fs_path else None,
        }
        if fs_path is not None:
            existing.append(row)
        else:
            missing.append(row)

    return existing, missing, unmappable


def priority(count: int, route: str) -> str:
    top_level_routes = {"/dashboard", "/settings", "/notifications", "/onboarding", "/login"}
    if count >= 3 or route in top_level_routes:
        return "high"
    if count >= 1:
        return "medium"
    return "low"


def main() -> None:
    dest_counts = collect_destinations(NAV_MAP_PATH)
    existing, missing, unmappable = analyse(dest_counts)

    total = len(dest_counts)
    n_existing = len(existing)
    n_missing = len(missing)
    n_unmappable = len(unmappable)

    print(f"Total unique destinations : {total}")
    print(f"Mapped + existing         : {n_existing}")
    print(f"Mapped + MISSING          : {n_missing}")
    print(f"Unmappable / planned      : {n_unmappable}")
    print()

    print("=== MISSING ===")
    for r in sorted(missing, key=lambda x: -x["count"]):
        p = priority(r["count"], r["canonical_route"])
        note = f"  [{r['heuristic_note']}]" if r["heuristic_note"] else ""
        print(f"  [{p:6s}] {r['count']:3d}x  {r['dest']!r:55s} -> {r['canonical_route']}{note}")

    print()
    print("=== UNMAPPABLE ===")
    for u in sorted(unmappable, key=lambda x: -x["count"]):
        print(f"  {u['count']:3d}x  {u['dest']}")


if __name__ == "__main__":
    main()
