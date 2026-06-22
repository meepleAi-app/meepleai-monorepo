"""Inspects a mockup HTML/JSX file: link destinations, landmarks, headings.

Reuses scripts.mockup_demo.validate.extract_targets for link destinations
(already handles href=, window.location.href, onclick patterns + sibling .jsx).
"""
from __future__ import annotations
from dataclasses import dataclass, field
from pathlib import Path
import re

# Reuse Phase 1 link extractor
from scripts.mockup_demo.validate import extract_targets

_LANDMARK_RE = re.compile(
    r"<(header|nav|main|section|article|aside|footer)\b",
    re.IGNORECASE,
)
_HEADING_RE = re.compile(r"<(h[1-6])\b", re.IGNORECASE)


@dataclass
class MockupSnapshot:
    path: Path
    link_destinations: set[str] = field(default_factory=set)
    landmarks: set[str] = field(default_factory=set)
    headings: set[str] = field(default_factory=set)


def _scan_text(text: str, snap: MockupSnapshot) -> None:
    """Populate landmarks and headings from raw text in place."""
    for m in _LANDMARK_RE.finditer(text):
        snap.landmarks.add(m.group(1).lower())
    for m in _HEADING_RE.finditer(text):
        snap.headings.add(m.group(1).lower())


def inspect_mockup(path: Path) -> MockupSnapshot:
    snap = MockupSnapshot(path=path)

    # Scan the primary file (HTML shell or plain HTML)
    text = path.read_text(encoding="utf-8")
    snap.link_destinations = extract_targets(path)
    _scan_text(text, snap)

    # Also scan sibling .jsx if this is an HTML shell backed by React
    jsx_sibling = path.with_suffix(".jsx")
    if jsx_sibling.exists():
        jsx_text = jsx_sibling.read_text(encoding="utf-8")
        _scan_text(jsx_text, snap)

    return snap
