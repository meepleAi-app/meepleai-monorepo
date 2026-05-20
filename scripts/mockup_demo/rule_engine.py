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

_DATA = Path(__file__).parent / "data" / "canonical_nav.json"
_NAV = json.loads(_DATA.read_text(encoding="utf-8"))


@dataclass
class Decision:
    destination: str | None
    rule: str
    confidence: float
    rationale: str


def _norm(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").strip().lower())


def _try_canonical(
    click: Clickable, table_key: str, class_hint: str, rule_name: str
) -> Decision | None:
    table = _NAV[table_key]
    text = _norm(click.text)
    for key, dest in table.items():
        if _norm(key) == text and (
            not class_hint or class_hint in (click.classes or "").lower()
        ):
            return Decision(
                dest,
                rule_name,
                0.98,
                f"text '{click.text}' matches {table_key}['{key}']",
            )
    return None


def resolve_destination(click: Clickable) -> Decision:
    # 1. canonical sidebar
    d = _try_canonical(click, "sidebar", "nav", "canonical-sidebar")
    if d:
        return d
    # 2. canonical public topbar
    d = _try_canonical(click, "public_topbar", "", "canonical-public-topbar")
    if d:
        return d
    # 3. canonical gameplay topbar
    d = _try_canonical(click, "gameplay_topbar", "", "canonical-gameplay-topbar")
    if d:
        return d
    # 4. canonical event topbar
    d = _try_canonical(click, "event_topbar", "", "canonical-event-topbar")
    if d:
        return d
    return Decision(None, "no-match", 0.0, "no rule matched")
