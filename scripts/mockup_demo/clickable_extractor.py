"""Extracts clickable elements from HTML and JSX files.

Yields Clickable records: file_path, tag, line_number, text, classes, snippet,
on_click_existing, kind ('html' | 'jsx').
"""
from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
from typing import Iterator
import re

CLICKABLE_TAGS_HTML = ("a", "button")
# Classes that strongly signal clickability even on neutral tags
CLICKABLE_CLASS_PATTERNS = re.compile(
    r"\b(card|row|nav-item|tab|hub-card|tile|menu-item|list-item|cta)\b",
    re.IGNORECASE,
)


@dataclass
class Clickable:
    file_path: Path
    tag: str
    line_number: int
    text: str
    classes: str | None
    snippet: str
    on_click_existing: str | None
    kind: str  # "html" or "jsx"


def extract_clickables(path: Path) -> Iterator[Clickable]:
    if path.suffix == ".jsx":
        yield from _extract_jsx(path)
    else:
        yield from _extract_html(path)


_HTML_TAG_RE = re.compile(
    r"<(?P<tag>a|button|div|li|span)\b(?P<attrs>[^>]*)>(?P<inner>[^<]*)",
    re.IGNORECASE | re.DOTALL,
)
_CLASS_ATTR_RE = re.compile(r'class\s*=\s*"([^"]*)"', re.IGNORECASE)
_ONCLICK_ATTR_RE = re.compile(r'onclick\s*=\s*"([^"]*)"', re.IGNORECASE)


def _extract_html(path: Path) -> Iterator[Clickable]:
    text = path.read_text(encoding="utf-8")
    for m in _HTML_TAG_RE.finditer(text):
        tag = m.group("tag").lower()
        attrs = m.group("attrs") or ""
        inner = (m.group("inner") or "").strip()
        cls_match = _CLASS_ATTR_RE.search(attrs)
        classes = cls_match.group(1) if cls_match else None
        if tag in CLICKABLE_TAGS_HTML or (classes and CLICKABLE_CLASS_PATTERNS.search(classes)):
            on_click = _ONCLICK_ATTR_RE.search(attrs)
            yield Clickable(
                file_path=path,
                tag=tag,
                line_number=text.count("\n", 0, m.start()) + 1,
                text=inner,
                classes=classes,
                snippet=m.group(0),
                on_click_existing=on_click.group(1) if on_click else None,
                kind="html",
            )


def _extract_jsx(path: Path) -> Iterator[Clickable]:
    # Implemented in Task 3 (JSX extractor)
    return iter(())
