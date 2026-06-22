"""Patches JSX files in place with onClick navigation, idempotent.

Limitation: the attrs regex `_OPEN_TAG_RE` handles up to 2 levels of brace
nesting in JSX expressions (e.g., `style={{ key: val }}`). Snippets with 3+
levels of nesting (e.g., `onClick={(e) => { setState({k: v}); }}`) cause the
match to fail; in that case, `_apply_patch` logs a warning and returns None,
and `patch_jsx_element` returns False. This is acceptable for the current
mockup corpus but should be revisited if new mockups use deeper nesting.
"""
from __future__ import annotations
from pathlib import Path
import re

DEMO_MARKER = "/* DEMO-NAV */"

# Match opening tag and capture attrs (incl. attrs with JSX expressions like style={{}}).
# Attrs pattern allows two-deep brace nesting to handle style={{...}}.
_OPEN_TAG_RE = re.compile(
    r"^<(?P<tag>\w+)\b"
    r"(?P<attrs>(?:[^>{}]|\{(?:[^{}]|\{[^}]*\})*\})*)"
    r">",
    re.DOTALL,
)
# Match onClick attribute and capture its body (one-level deep handler).
_ONCLICK_RE = re.compile(r"onClick\s*=\s*\{(?P<body>(?:[^{}]|\{[^}]*\})*)\}")


def patch_jsx_element(path: Path, selector_snippet: str, destination: str) -> bool:
    """Return True if file was modified, False if skipped."""
    if not destination or destination == "OUT_OF_SCOPE":
        return False
    content = path.read_text(encoding="utf-8")
    if selector_snippet not in content:
        return False
    idx = content.index(selector_snippet)
    end = idx + len(selector_snippet)
    window = content[max(0, idx - 80):end]
    if DEMO_MARKER in window:
        return False

    patched = _apply_patch(selector_snippet, destination)
    if patched is None:
        return False
    new_content = content[:idx] + patched + content[end:]
    path.write_text(new_content, encoding="utf-8")
    return True


def _apply_patch(snippet: str, destination: str) -> str | None:
    """Inject or wrap onClick on the first opening tag of the snippet."""
    m = _OPEN_TAG_RE.match(snippet)
    if not m:
        # 3-deep brace handlers (e.g., onClick={(e) => { setState({k: v}); }})
        # are not parseable by our 2-deep regex. Skip with warning.
        snippet_preview = snippet[:80].replace("\n", " ")
        print(f"[jsx_patcher] WARN: skipped snippet (regex parse failed): {snippet_preview!r}")
        return None
    attrs = m.group("attrs")
    tag = m.group("tag")
    rest = snippet[m.end():]

    # navigation expression - use setTimeout 0 to let React state settle before redirect
    nav_call = (
        f"setTimeout(() => {{ window.location.href = '{destination}'; }}, 0); "
        f"{DEMO_MARKER}"
    )

    existing = _ONCLICK_RE.search(attrs)
    if existing:
        existing_body = existing.group("body").strip()
        # Wrap: invoke original handler, then navigate
        new_onclick = (
            f"onClick={{(e) => {{ ({existing_body})(e); {nav_call} }}}}"
        )
        new_attrs = _ONCLICK_RE.sub(new_onclick, attrs, count=1)
    else:
        new_attrs = attrs + f" onClick={{() => {{ {nav_call} }}}}"
    return f"<{tag}{new_attrs}>" + rest
