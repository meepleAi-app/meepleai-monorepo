"""Decides destination mockup for a Clickable using ordered rule chain.

Rules (in priority order):
  1. canonical-sidebar      (text matches a sidebar key, classes hint nav/sidebar)
  2. canonical-public-topbar
  3. canonical-gameplay-topbar
  4. canonical-event-topbar
  5. index-to-detail        (Task 5)
  6. detail-action-keyword  (Task 5)
  7. fuzzy-heuristic        (Task 6)
  8. no-match
"""
from __future__ import annotations

import json
import re
from dataclasses import dataclass
from pathlib import Path

from scripts.mockup_demo.clickable_extractor import Clickable
from scripts.mockup_demo.fuzzy_heuristic import score_candidates

_DATA = Path(__file__).parent / "data" / "canonical_nav.json"
_NAV = json.loads(_DATA.read_text(encoding="utf-8"))

_CATALOG: list[str] | None = None


def _catalog() -> list[str]:
    global _CATALOG
    if _CATALOG is None:
        mockups_dir = Path(__file__).resolve().parents[2] / "admin-mockups" / "design_files"
        _CATALOG = sorted(p.name for p in mockups_dir.glob("*.html"))
    return _CATALOG


@dataclass
class Decision:
    destination: str | None
    rule: str
    confidence: float
    rationale: str


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").strip().lower())


def _try_canonical(
    click: Clickable,
    table_key: str,
    class_hints: tuple[str, ...] | str,
    rule_name: str,
) -> Decision | None:
    table = _NAV[table_key]
    text = _norm(click.text)
    if isinstance(class_hints, str):
        class_hints = (class_hints,) if class_hints else ()
    classes_lower = (click.classes or "").lower()
    for key, dest in table.items():
        if _norm(key) != text:
            continue
        if not class_hints or any(h in classes_lower for h in class_hints):
            return Decision(
                dest,
                rule_name,
                0.98,
                f"text '{click.text}' matches {table_key}['{key}']",
            )
    return None


def resolve_destination(click: Clickable) -> Decision:
    # 1. canonical sidebar (with nav/side/menu class hint)
    d = _try_canonical(click, "sidebar", ("nav", "side", "menu"), "canonical-sidebar")
    if d:
        return d
    # 1b. canonical sidebar loose: sp4-* file context (sidebar with inline styles, no className)
    if click.file_path.name.startswith("sp4-"):
        d = _try_canonical(click, "sidebar", "", "canonical-sidebar")
        if d:
            return d
    # 2. canonical public topbar
    d = _try_canonical(click, "public_topbar", "", "canonical-public-topbar")
    if d:
        return d
    # 3. canonical gameplay topbar - sp6/librogame context
    if any(click.file_path.name.startswith(p) for p in ("sp6-", "librogame-")):
        d = _try_canonical(click, "gameplay_topbar", "", "canonical-gameplay-topbar")
        if d:
            return d
    # 4. canonical event topbar - sp7 context
    if click.file_path.name.startswith("sp7-"):
        d = _try_canonical(click, "event_topbar", "", "canonical-event-topbar")
        if d:
            return d

    # 5. index-to-detail: file matches a known index, element is card/row/tile
    file_stem = click.file_path.stem  # 'sp4-games-index'
    # JSX is sibling of HTML; normalize to the .html name
    index_html = f"{file_stem}.html"
    if index_html in _NAV["index_to_detail"]:
        classes = (click.classes or "").lower()
        if any(token in classes for token in ("card", "row", "tile", "list-item")):
            dest = _NAV["index_to_detail"][index_html]
            return Decision(
                dest, "index-to-detail", 0.85,
                f"file '{index_html}' is an index, element has card/row class"
            )

    # 6. detail-action-keyword: button/CTA text contains a known action keyword
    text_lower = _norm(click.text)
    if click.tag in ("button", "a") or "cta" in (click.classes or "").lower():
        for keyword, dest in _NAV["detail_action_keywords"].items():
            if keyword in text_lower:
                return Decision(
                    dest, "detail-action-keyword", 0.80,
                    f"text '{click.text}' contains keyword '{keyword}'"
                )

    # 7. fuzzy-heuristic
    if click.text and len(click.text.strip()) >= 3:
        scored = score_candidates(click.text, _catalog())
        if scored and scored[0][1] >= 0.7:
            dest, score = scored[0]
            return Decision(
                dest, "fuzzy-heuristic", score,
                f"fuzzy match '{click.text}' → '{dest}' (score {score:.2f})"
            )

    return Decision(None, "no-match", 0.0, "no rule matched")
