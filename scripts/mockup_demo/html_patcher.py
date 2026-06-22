"""Patches HTML files in place with navigation, idempotent via DEMO-NAV marker."""
from __future__ import annotations
from pathlib import Path
import re

DEMO_MARKER = "/* DEMO-NAV */"


def patch_html_element(path: Path, selector_snippet: str, destination: str) -> bool:
    """Return True if file was modified, False if skipped (OUT_OF_SCOPE, idempotent, snippet not found)."""
    if not destination or destination == "OUT_OF_SCOPE":
        return False
    content = path.read_text(encoding="utf-8")
    if selector_snippet not in content:
        return False
    idx = content.index(selector_snippet)
    end = idx + len(selector_snippet)
    # Look back a few chars for marker (idempotence guard)
    window = content[max(0, idx - 80):end]
    if DEMO_MARKER in window:
        return False

    patched = _apply_patch(selector_snippet, destination)
    new_content = content[:idx] + patched + content[end:]
    path.write_text(new_content, encoding="utf-8")
    return True


_A_OPEN_RE = re.compile(r'<a\b([^>]*?)>', re.IGNORECASE)
_HREF_ATTR_RE = re.compile(r'href\s*=\s*"[^"]*"', re.IGNORECASE)


def _apply_patch(snippet: str, destination: str) -> str:
    # Case A: <a ...> at start of snippet → set/replace href
    m = _A_OPEN_RE.match(snippet)
    if m:
        attrs = m.group(1)
        if _HREF_ATTR_RE.search(attrs):
            new_attrs = _HREF_ATTR_RE.sub(f'href="{destination}"', attrs)
        else:
            new_attrs = attrs + f' href="{destination}"'
        return f'<a{new_attrs} {DEMO_MARKER}>' + snippet[m.end():]
    # Case B: non-anchor → add onclick (no tag change to preserve CSS)
    open_tag_end = snippet.find(">")
    if open_tag_end == -1:
        return snippet
    open_tag = snippet[:open_tag_end]
    rest = snippet[open_tag_end:]
    return f"{open_tag} onclick=\"window.location.href='{destination}'\" {DEMO_MARKER}{rest}"
