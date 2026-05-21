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

_JSX_TAG_RE = re.compile(
    r"<(?P<tag>a|button|div|li|span)\b"
    r"(?P<attrs>(?:[^>{}]|\{(?:[^{}]|\{[^}]*\})*\})*)"
    r">(?P<inner>[^<]*)",
    re.IGNORECASE | re.DOTALL,
)
_JSX_CLASSNAME_RE = re.compile(r'className\s*=\s*"([^"]*)"')
_JSX_ONCLICK_RE = re.compile(r"onClick\s*=\s*\{([^}]*)\}")

# Matches label values in navItem/tab array literals, e.g. { id:'x', label:'Giochi' }
_JSX_LABEL_IN_ARRAY_RE = re.compile(r"label\s*:\s*['\"]([^'\"]+)['\"]")
# Matches JSX expressions that reference a label property: {it.label}, {item.label}, etc.
_JSX_LABEL_EXPR_RE = re.compile(r"^\{[a-z_]\w*\.label\}$", re.IGNORECASE)


def _extract_jsx_label_values(file_text: str, match_pos: int) -> list[str]:
    """Return label values from the nearest preceding array literal that contains label: '...' entries.

    When a JSX element has text ``{it.label}`` the actual labels come from an array
    defined just before the ``.map(it => ...)`` call.  We scan backwards from the
    match position to find the nearest block that contains ``label: 'X'`` entries
    and return those values.  Empty list if nothing found.
    """
    preceding = file_text[:match_pos]
    labels = _JSX_LABEL_IN_ARRAY_RE.findall(preceding)
    if not labels:
        return []
    # The nearest labels are at the end — take up to the last 12 (one navItems block)
    return labels[-12:]


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
    text = path.read_text(encoding="utf-8")
    for m in _JSX_TAG_RE.finditer(text):
        tag = m.group("tag").lower()
        attrs = m.group("attrs") or ""
        inner = (m.group("inner") or "").strip()
        cls_match = _JSX_CLASSNAME_RE.search(attrs)
        classes = cls_match.group(1) if cls_match else None
        if tag in CLICKABLE_TAGS_HTML or (classes and CLICKABLE_CLASS_PATTERNS.search(classes)):
            on_click = _JSX_ONCLICK_RE.search(attrs)
            line_number = text.count("\n", 0, m.start()) + 1
            on_click_str = on_click.group(1).strip() if on_click else None
            # Expand JSX label expressions like {it.label} → one Clickable per resolved label
            if _JSX_LABEL_EXPR_RE.match(inner):
                labels = _extract_jsx_label_values(text, m.start())
                for label in labels:
                    yield Clickable(
                        file_path=path,
                        tag=tag,
                        line_number=line_number,
                        text=label,
                        classes=classes,
                        snippet=m.group(0),
                        on_click_existing=on_click_str,
                        kind="jsx",
                    )
            else:
                yield Clickable(
                    file_path=path,
                    tag=tag,
                    line_number=line_number,
                    text=inner,
                    classes=classes,
                    snippet=m.group(0),
                    on_click_existing=on_click_str,
                    kind="jsx",
                )
