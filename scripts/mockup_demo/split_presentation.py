"""Split mockup posters into standalone HTML pages.

Reads HTML poster files from admin-mockups/mockup/ that contain split annotations
(<meta name="mockup-split-mode" content="...">) and emits one or more standalone
HTML files in admin-mockups/standalone/ — one per variant/section/component —
suitable as pixel-fidelity references for code generation agents.

Three modes (declared by the poster's <meta name="mockup-split-mode">):

  - merge-sections:   N <[data-section]> elements are concatenated into ONE
                      standalone file under <meta name="mockup-output-name">.html
                      Used when the poster shows successive scroll positions of
                      the same page (e.g. dashboard-new-user-mockup.html).

  - per-variant:      Each [data-variant] element becomes its own standalone
                      file, named <output-prefix>--<variant>.html.
                      Used when the poster compares actual UI variants
                      (e.g. mobile-card-layout-mockup.html).

  - per-component:    Each [data-component] element becomes its own standalone
                      file (analogous to per-variant but for component showcase
                      mockups that have no .phone frame).

Idempotency: generated files start with the HTML comment
"<!-- SPLIT-GEN: ... -->". Re-running the script overwrites these files only.
Files without the marker are never overwritten — they are treated as
hand-authored and the script aborts with a clear error.

Usage:
  python -m scripts.mockup_demo.split_presentation                 # process all annotated posters
  python -m scripts.mockup_demo.split_presentation --poster PATH   # single poster
  python -m scripts.mockup_demo.split_presentation --dry-run       # log only
  python -m scripts.mockup_demo.split_presentation --no-index      # skip _index.html

Reference: docs/for-developers/specs/2026-05-26-mockup-poster-split.md
"""
from __future__ import annotations

import argparse
import datetime as _dt
import html
import re
import sys
from dataclasses import dataclass, field
from html.parser import HTMLParser
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
POSTERS_DIR = REPO_ROOT / "admin-mockups" / "mockup"
STANDALONE_DIR = REPO_ROOT / "admin-mockups" / "standalone"

MARKER_PREFIX = "<!-- SPLIT-GEN:"
SUPPORTED_MODES = {"merge-sections", "per-variant", "per-component"}

# Matches the `regenerated=2026-05-26T06:16:16Z` field inside SPLIT-GEN markers.
# Used to ignore the timestamp when comparing existing vs new content for
# idempotency: rerunning with no source changes should report "unchanged".
_TIMESTAMP_RE = re.compile(r" regenerated=\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z")

# Selectors whose CSS rules are stripped from the extracted page styles.
# These are "poster chrome" — gallery layout, labels, legends — and must not
# leak into standalone pages where they would create visual noise.
CHROME_SELECTOR_RE = re.compile(
    r"^\s*("
    r"\.mockup-row"
    r"|\.mockup-col"
    r"|\.mockup-label"
    r"|\.mockup-sublabel"
    r"|\.pg-title"
    r"|\.pg-title\s+[a-z0-9_-]+"  # .pg-title h1, .pg-title p
    r"|\.legend"
    r"|\.leg-item"
    r"|\.leg-badge"
    r"|\.anno-wrap"
    r"|\.anno-(arrow|note|tag)"
    r"|body"
    r"|html"
    r")(\s*[,{]|\s+[.:#\[]|\s*$)",
    re.IGNORECASE,
)

# Standalone page template. Imports the canonical design tokens + components
# from design_files/, plus the poster's filtered page styles. The viewport
# centers the .phone frame on a neutral background.
STANDALONE_TEMPLATE = """<!DOCTYPE html>
<html lang="it" data-theme="light">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
{marker}
<link rel="preconnect" href="https://fonts.googleapis.com"/>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
<link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700;800&family=Nunito:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600;700&display=swap" rel="stylesheet"/>
<link rel="stylesheet" href="../design_files/tokens.css"/>
<link rel="stylesheet" href="../design_files/components.css"/>
<style>
/* ── Standalone viewport (overrides poster body chrome) ───────────────── */
*{{margin:0;padding:0;box-sizing:border-box}}
html,body{{height:100%}}
body.standalone-viewport{{
  font-family:'Nunito',system-ui,sans-serif;
  background:#1a1612;
  color:var(--text,#2b1f12);
  min-height:100vh;
  display:flex;
  flex-direction:column;
  align-items:center;
  justify-content:flex-start;
  gap:32px;
  padding:32px 24px;
}}
body.standalone-viewport > .phone{{
  flex-shrink:0;
}}
h1,h2,h3,h4,h5{{font-family:'Quicksand',sans-serif}}

/* ── Inherited page styles from poster (chrome stripped) ──────────────── */
{page_styles}
</style>
</head>
<body class="standalone-viewport">
{body_content}
</body>
</html>
"""

INDEX_TEMPLATE = """<!DOCTYPE html>
<html lang="it" data-theme="light">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>MeepleAI — Standalone Mockups Index</title>
{marker}
<link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700&family=Nunito:wght@400;500;600;700&display=swap" rel="stylesheet"/>
<style>
*{{margin:0;padding:0;box-sizing:border-box}}
body{{font-family:'Nunito',sans-serif;background:#f7f3ee;color:#2b1f12;padding:32px 24px;min-height:100vh}}
h1{{font-family:'Quicksand',sans-serif;font-size:1.8rem;margin-bottom:8px}}
.subtitle{{color:#5a4a38;font-size:0.95rem;margin-bottom:24px}}
.meta{{color:#8a7860;font-size:0.78rem;font-family:'JetBrains Mono',monospace;margin-bottom:32px}}
.group{{margin-bottom:32px}}
.group-title{{font-family:'Quicksand',sans-serif;font-weight:700;font-size:1.1rem;margin-bottom:12px;color:#2b1f12;border-bottom:1px solid rgba(180,130,80,0.18);padding-bottom:6px}}
.group-source{{font-family:'JetBrains Mono',monospace;font-size:0.72rem;color:#8a7860;margin-bottom:12px}}
.entries{{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:14px}}
.entry{{background:#ffffff;border:1px solid rgba(180,130,80,0.18);border-radius:14px;padding:14px;text-decoration:none;color:inherit;display:flex;flex-direction:column;gap:6px;transition:box-shadow 180ms,transform 180ms}}
.entry:hover{{box-shadow:0 4px 16px rgba(90,60,20,0.1);transform:translateY(-2px)}}
.entry-name{{font-family:'Quicksand',sans-serif;font-weight:600;font-size:0.95rem;color:#2b1f12;word-break:break-all}}
.entry-mode{{font-family:'JetBrains Mono',monospace;font-size:0.7rem;color:#8a7860;text-transform:uppercase;letter-spacing:0.04em}}
.summary{{background:rgba(255,255,255,0.6);border:1px solid rgba(180,130,80,0.18);border-radius:10px;padding:12px 16px;margin-bottom:24px;font-size:0.85rem;color:#5a4a38}}
.summary strong{{color:#2b1f12}}
</style>
</head>
<body>
<h1>Standalone Mockups</h1>
<p class="subtitle">Auto-generated pages from <code>admin-mockups/mockup/*.html</code> posters via <code>scripts/mockup_demo/split_presentation.py</code>.</p>
<p class="meta">Generated: {timestamp} · Source spec: <a href="../../docs/for-developers/specs/2026-05-26-mockup-poster-split.md">2026-05-26-mockup-poster-split.md</a></p>
<div class="summary"><strong>{total}</strong> standalone files from <strong>{posters}</strong> posters.</div>
{groups}
</body>
</html>
"""


@dataclass
class MockupAnnotation:
    """Parsed <meta> annotation from a poster's <head>."""
    mode: str
    output_name: str | None = None  # for merge-sections
    output_prefix: str | None = None  # for per-variant / per-component


@dataclass
class StandaloneFile:
    """A generated standalone HTML file ready to be written."""
    path: Path
    content: str
    source_poster: str  # poster filename, for the index
    mode: str
    variant_key: str  # data-section / data-variant / data-component value


# ─── HTML parsing ────────────────────────────────────────────────────────────

class _MetaCollector(HTMLParser):
    """Collects <meta name="..." content="..."> tags."""

    def __init__(self):
        super().__init__(convert_charrefs=False)
        self.metas: dict[str, str] = {}
        self.title: str | None = None
        self._in_title = False

    def handle_starttag(self, tag, attrs):
        if tag.lower() == "meta":
            adict = {k.lower(): v for k, v in attrs if k}
            name = adict.get("name")
            content = adict.get("content")
            if name and content:
                self.metas[name] = content
        elif tag.lower() == "title":
            self._in_title = True

    def handle_endtag(self, tag):
        if tag.lower() == "title":
            self._in_title = False

    def handle_data(self, data):
        if self._in_title:
            self.title = (self.title or "") + data


class _SubtreeExtractor(HTMLParser):
    """Extracts inner-HTML of elements matching a data-attribute predicate.

    For each element opened with the target attribute, captures the full
    serialized subtree (open tag, children, close tag) using a depth counter
    so nesting is balanced.
    """

    # Self-closing HTML5 void elements — never have a close tag.
    VOID = {
        "area", "base", "br", "col", "embed", "hr", "img", "input",
        "link", "meta", "param", "source", "track", "wbr",
    }

    def __init__(self, attribute: str):
        super().__init__(convert_charrefs=False)
        self.attribute = attribute
        self.results: dict[str, str] = {}
        self.order: list[str] = []  # preserve doc order
        # Stack of (key, depth, buffer); empty when not capturing.
        self._captures: list[tuple[str, int, list[str]]] = []

    def _emit(self, text: str) -> None:
        for cap in self._captures:
            cap[2].append(text)

    def handle_starttag(self, tag, attrs):
        adict = {k.lower(): v for k, v in attrs if k}
        key = adict.get(self.attribute)
        raw = self.get_starttag_text() or _reconstruct_starttag(tag, attrs)
        if key is not None and not self._captures_has(key):
            # Start a new capture
            self._captures.append((key, 1, [raw]))
            if key not in self.results:
                self.order.append(key)
            return
        # Otherwise, emit into all active captures + increment depth if nested
        self._emit(raw)
        if self._captures and tag.lower() not in self.VOID:
            # Increment depth of innermost capture
            k, d, buf = self._captures[-1]
            self._captures[-1] = (k, d + 1, buf)

    def handle_startendtag(self, tag, attrs):
        raw = self.get_starttag_text() or _reconstruct_starttag(tag, attrs, self_close=True)
        self._emit(raw)

    def handle_endtag(self, tag):
        if not self._captures:
            return
        if tag.lower() in self.VOID:
            # Stray close tag for void element — ignore (already emitted as start)
            return
        k, d, buf = self._captures[-1]
        buf.append(f"</{tag}>")
        new_depth = d - 1
        if new_depth <= 0:
            # Capture complete — store result
            self.results[k] = "".join(buf)
            self._captures.pop()
        else:
            self._captures[-1] = (k, new_depth, buf)

    def handle_data(self, data):
        self._emit(data)

    def handle_entityref(self, name):
        self._emit(f"&{name};")

    def handle_charref(self, name):
        self._emit(f"&#{name};")

    def handle_comment(self, data):
        self._emit(f"<!--{data}-->")

    def _captures_has(self, key: str) -> bool:
        return any(k == key for k, _, _ in self._captures)


def _reconstruct_starttag(tag: str, attrs: list, self_close: bool = False) -> str:
    """Fallback when get_starttag_text() returns None (rare)."""
    parts = [tag]
    for k, v in attrs:
        if v is None:
            parts.append(k)
        else:
            parts.append(f'{k}="{html.escape(v, quote=True)}"')
    body = " ".join(parts)
    return f"<{body}{' /' if self_close else ''}>"


# ─── Annotation parsing ──────────────────────────────────────────────────────

def parse_annotation(html_text: str) -> MockupAnnotation | None:
    """Return the MockupAnnotation from <meta> tags, or None if not annotated."""
    parser = _MetaCollector()
    parser.feed(html_text)
    mode = parser.metas.get("mockup-split-mode")
    if not mode:
        return None
    if mode not in SUPPORTED_MODES:
        raise ValueError(
            f"unsupported mockup-split-mode={mode!r}; expected one of {sorted(SUPPORTED_MODES)}"
        )
    return MockupAnnotation(
        mode=mode,
        output_name=parser.metas.get("mockup-output-name"),
        output_prefix=parser.metas.get("mockup-output-prefix"),
    )


def get_title(html_text: str) -> str:
    parser = _MetaCollector()
    parser.feed(html_text)
    return (parser.title or "Mockup").strip()


# ─── CSS chrome stripping ────────────────────────────────────────────────────

_STYLE_BLOCK_RE = re.compile(r"<style\b[^>]*>(.*?)</style>", re.IGNORECASE | re.DOTALL)


def extract_style_text(html_text: str) -> str:
    """Concatenate the content of all <style> blocks in document order."""
    parts = _STYLE_BLOCK_RE.findall(html_text)
    return "\n".join(parts)


def strip_chrome_css(css_text: str) -> str:
    """Remove CSS rules whose selectors match the chrome blacklist.

    The CSS is tokenized into rules using a brace counter. Each rule is
    inspected: if any selector in its selector list matches CHROME_SELECTOR_RE,
    the entire rule is dropped. @-rules (@media, @keyframes) are kept and
    recursively processed if they contain nested rules.
    """
    out: list[str] = []
    i = 0
    n = len(css_text)
    while i < n:
        # Skip whitespace
        while i < n and css_text[i].isspace():
            i += 1
        if i >= n:
            break
        # Find the next "{"
        brace = css_text.find("{", i)
        if brace == -1:
            # No more rules — append remainder (comments only, presumably)
            out.append(css_text[i:])
            break
        selector_list = css_text[i:brace].strip()
        # @-rules: handle separately
        if selector_list.startswith("@"):
            block_end = _find_matching_brace(css_text, brace)
            if block_end == -1:
                # Malformed — bail out
                out.append(css_text[i:])
                break
            at_rule_name = selector_list.split()[0].lower()
            inner = css_text[brace + 1:block_end]
            if at_rule_name in ("@media", "@supports"):
                # Recurse into nested rules
                filtered_inner = strip_chrome_css(inner)
                if filtered_inner.strip():
                    out.append(f"{selector_list} {{\n{filtered_inner}\n}}")
            elif at_rule_name in ("@keyframes", "@-webkit-keyframes", "@font-face", "@page"):
                # Keep as-is
                out.append(css_text[i:block_end + 1])
            else:
                # Unknown @-rule — keep to be safe
                out.append(css_text[i:block_end + 1])
            i = block_end + 1
            continue
        block_end = _find_matching_brace(css_text, brace)
        if block_end == -1:
            out.append(css_text[i:])
            break
        # Check if ANY selector in the list matches chrome blacklist
        selectors = [s.strip() for s in selector_list.split(",")]
        all_chrome = all(_is_chrome_selector(s) for s in selectors if s)
        any_chrome = any(_is_chrome_selector(s) for s in selectors if s)
        if all_chrome:
            # Drop the entire rule
            pass
        elif any_chrome:
            # Mixed list — keep only non-chrome selectors
            kept = [s for s in selectors if s and not _is_chrome_selector(s)]
            if kept:
                body = css_text[brace:block_end + 1]
                out.append(", ".join(kept) + " " + body)
        else:
            out.append(css_text[i:block_end + 1])
        i = block_end + 1
    return "\n".join(s for s in out if s.strip())


def _find_matching_brace(text: str, open_idx: int) -> int:
    """Return index of the matching '}' for the '{' at open_idx, or -1."""
    depth = 0
    i = open_idx
    n = len(text)
    while i < n:
        c = text[i]
        if c == "{":
            depth += 1
        elif c == "}":
            depth -= 1
            if depth == 0:
                return i
        elif c == "/" and i + 1 < n and text[i + 1] == "*":
            # Skip comment
            end = text.find("*/", i + 2)
            if end == -1:
                return -1
            i = end + 2
            continue
        elif c in ('"', "'"):
            # Skip string literal
            end = text.find(c, i + 1)
            if end == -1:
                return -1
            i = end + 1
            continue
        i += 1
    return -1


def _is_chrome_selector(selector: str) -> bool:
    return bool(CHROME_SELECTOR_RE.match(selector))


# ─── Processors (mode handlers) ──────────────────────────────────────────────

def _build_marker(poster_name: str, variant_key: str, mode: str) -> str:
    ts = _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return (
        f"<!-- SPLIT-GEN: source=mockup/{poster_name} "
        f"mode={mode} variant={variant_key} regenerated={ts} -->\n"
        f"<!-- DO NOT EDIT — regenerate via "
        f"`python -m scripts.mockup_demo.split_presentation` -->"
    )


def _build_standalone_html(
    title: str,
    marker: str,
    page_styles: str,
    body_content: str,
) -> str:
    return STANDALONE_TEMPLATE.format(
        title=html.escape(title, quote=True),
        marker=marker,
        page_styles=page_styles,
        body_content=body_content,
    )


def process_merge_sections(
    poster_path: Path,
    annotation: MockupAnnotation,
) -> list[StandaloneFile]:
    if not annotation.output_name:
        raise ValueError(
            f"{poster_path.name}: merge-sections mode requires "
            f"<meta name='mockup-output-name'>"
        )
    html_text = poster_path.read_text(encoding="utf-8")
    page_styles = strip_chrome_css(extract_style_text(html_text))
    title = get_title(html_text)

    extractor = _SubtreeExtractor("data-section")
    extractor.feed(html_text)
    if not extractor.results:
        raise ValueError(
            f"{poster_path.name}: no elements with data-section found "
            f"(needed for merge-sections mode)"
        )

    # For merge-sections, each captured element is typically <div class="mockup-col">
    # wrapping a <div class="phone">. We want to extract ONLY the .phone subtree
    # and concatenate the contents of all .phone .pgc sections into one phone frame.
    #
    # Simpler approach (pilot): emit each .phone as-is, stacked vertically.
    # Future refinement: merge .pgc contents into a single phone.
    body_parts = []
    for key in extractor.order:
        section_html = extractor.results[key]
        phone_html = _extract_first_phone(section_html)
        if phone_html:
            body_parts.append(f"  <!-- section: {key} -->\n  {phone_html}")
        else:
            # Fallback: emit the whole .mockup-col subtree (will include label,
            # which is undesirable but better than dropping content)
            body_parts.append(f"  <!-- section: {key} (no .phone found) -->\n  {section_html}")

    body_content = "\n\n".join(body_parts)
    marker = _build_marker(poster_path.name, "all-sections", annotation.mode)
    standalone_html = _build_standalone_html(title, marker, page_styles, body_content)

    output_path = STANDALONE_DIR / f"{annotation.output_name}.html"
    return [StandaloneFile(
        path=output_path,
        content=standalone_html,
        source_poster=poster_path.name,
        mode=annotation.mode,
        variant_key="all-sections",
    )]


def process_per_variant(
    poster_path: Path,
    annotation: MockupAnnotation,
) -> list[StandaloneFile]:
    if not annotation.output_prefix:
        raise ValueError(
            f"{poster_path.name}: per-variant mode requires "
            f"<meta name='mockup-output-prefix'>"
        )
    html_text = poster_path.read_text(encoding="utf-8")
    page_styles = strip_chrome_css(extract_style_text(html_text))
    title = get_title(html_text)

    extractor = _SubtreeExtractor("data-variant")
    extractor.feed(html_text)
    if not extractor.results:
        raise ValueError(
            f"{poster_path.name}: no elements with data-variant found"
        )

    results = []
    for key in extractor.order:
        variant_html = extractor.results[key]
        # If the variant is a .mockup-col, dive into .phone; else use as-is
        phone_html = _extract_first_phone(variant_html)
        body_content = f"  {phone_html or variant_html}"
        marker = _build_marker(poster_path.name, key, annotation.mode)
        standalone_html = _build_standalone_html(
            f"{title} — {key}", marker, page_styles, body_content,
        )
        output_path = STANDALONE_DIR / f"{annotation.output_prefix}--{key}.html"
        results.append(StandaloneFile(
            path=output_path,
            content=standalone_html,
            source_poster=poster_path.name,
            mode=annotation.mode,
            variant_key=key,
        ))
    return results


def process_per_component(
    poster_path: Path,
    annotation: MockupAnnotation,
) -> list[StandaloneFile]:
    if not annotation.output_prefix:
        raise ValueError(
            f"{poster_path.name}: per-component mode requires "
            f"<meta name='mockup-output-prefix'>"
        )
    html_text = poster_path.read_text(encoding="utf-8")
    page_styles = strip_chrome_css(extract_style_text(html_text))
    title = get_title(html_text)

    extractor = _SubtreeExtractor("data-component")
    extractor.feed(html_text)
    if not extractor.results:
        raise ValueError(
            f"{poster_path.name}: no elements with data-component found"
        )

    results = []
    for key in extractor.order:
        component_html = extractor.results[key]
        # Component mode: no .phone unwrapping — emit subtree directly.
        # Override body class so we get a flat layout instead of phone centering.
        body_content = f"  {component_html}"
        marker = _build_marker(poster_path.name, key, annotation.mode)
        standalone_html = _build_standalone_html(
            f"{title} — {key}", marker, page_styles, body_content,
        )
        # Patch body class for component layout (left-aligned, plain bg)
        standalone_html = standalone_html.replace(
            'body.standalone-viewport{',
            'body.standalone-viewport{background:#f7f3ee !important;align-items:flex-start !important;justify-content:flex-start !important;',
            1,
        )
        output_path = STANDALONE_DIR / f"{annotation.output_prefix}--{key}.html"
        results.append(StandaloneFile(
            path=output_path,
            content=standalone_html,
            source_poster=poster_path.name,
            mode=annotation.mode,
            variant_key=key,
        ))
    return results


_PHONE_DIV_RE = re.compile(
    r'<div\b[^>]*\bclass\s*=\s*"[^"]*\bphone\b[^"]*"[^>]*>',
    re.IGNORECASE,
)


def _extract_first_phone(html_fragment: str) -> str | None:
    """Return the first <div class="phone ..."> ... </div> subtree, or None."""
    m = _PHONE_DIV_RE.search(html_fragment)
    if not m:
        return None
    start = m.start()
    # Walk forward counting <div> balance from the open tag
    depth = 0
    i = start
    n = len(html_fragment)
    while i < n:
        # Find next <div or </div
        next_open = html_fragment.find("<div", i)
        next_close = html_fragment.find("</div", i)
        if next_close == -1:
            return None
        if next_open != -1 and next_open < next_close:
            depth += 1
            # Skip past the closing ">"
            gt = html_fragment.find(">", next_open)
            if gt == -1:
                return None
            i = gt + 1
        else:
            depth -= 1
            gt = html_fragment.find(">", next_close)
            if gt == -1:
                return None
            if depth == 0:
                return html_fragment[start:gt + 1]
            i = gt + 1
    return None


# ─── File I/O with idempotency contract ──────────────────────────────────────

def write_standalone(file: StandaloneFile, dry_run: bool = False) -> str:
    """Write file.content to file.path. Returns status:
      - 'written'   : file written or overwritten (was marked or didn't exist)
      - 'unchanged' : file existed with marker and content matched bit-for-bit
      - 'dry-run'   : dry-run mode, no write
      - 'protected' : file existed WITHOUT marker — refused to overwrite
    """
    if dry_run:
        return "dry-run"

    file.path.parent.mkdir(parents=True, exist_ok=True)

    if file.path.exists():
        existing = file.path.read_text(encoding="utf-8")
        # Look for marker in first 1KB
        head = existing[:1024]
        if MARKER_PREFIX not in head:
            raise RuntimeError(
                f"refusing to overwrite {file.path} — file exists without SPLIT-GEN marker. "
                f"If you want to regenerate, delete the file first."
            )
        # Ignore the timestamp field when comparing — it's the only field that
        # changes between identical regenerations.
        if _TIMESTAMP_RE.sub("", existing) == _TIMESTAMP_RE.sub("", file.content):
            return "unchanged"

    file.path.write_text(file.content, encoding="utf-8")
    return "written"


# ─── Index generation ────────────────────────────────────────────────────────

def generate_index(
    files: list[StandaloneFile],
    dry_run: bool = False,
) -> str:
    by_poster: dict[str, list[StandaloneFile]] = {}
    for f in files:
        by_poster.setdefault(f.source_poster, []).append(f)

    groups_html = []
    for poster in sorted(by_poster):
        entries = sorted(by_poster[poster], key=lambda f: f.path.name)
        rows = "\n".join(
            f'    <a class="entry" href="{html.escape(f.path.name, quote=True)}">'
            f'<span class="entry-name">{html.escape(f.path.name)}</span>'
            f'<span class="entry-mode">{html.escape(f.mode)} · {html.escape(f.variant_key)}</span>'
            f'</a>'
            for f in entries
        )
        groups_html.append(
            f'<div class="group">\n'
            f'  <div class="group-title">{html.escape(poster)}</div>\n'
            f'  <div class="group-source">→ {len(entries)} standalone file{"s" if len(entries) != 1 else ""}</div>\n'
            f'  <div class="entries">\n{rows}\n  </div>\n'
            f'</div>'
        )

    timestamp = _dt.datetime.now(_dt.timezone.utc).strftime("%Y-%m-%d %H:%M UTC")
    marker = (
        f"<!-- SPLIT-GEN: index regenerated={timestamp} -->\n"
        f"<!-- DO NOT EDIT — regenerate via "
        f"`python -m scripts.mockup_demo.split_presentation` -->"
    )
    content = INDEX_TEMPLATE.format(
        marker=marker,
        timestamp=html.escape(timestamp),
        total=len(files),
        posters=len(by_poster),
        groups="\n".join(groups_html),
    )

    index_path = STANDALONE_DIR / "_index.html"
    if dry_run:
        return "dry-run"
    index_path.parent.mkdir(parents=True, exist_ok=True)
    # Protect index too: only overwrite if marked or absent
    if index_path.exists():
        existing = index_path.read_text(encoding="utf-8")
        head = existing[:1024]
        if MARKER_PREFIX not in head:
            raise RuntimeError(
                f"refusing to overwrite {index_path} — missing SPLIT-GEN marker"
            )
        # Ignore timestamp field for idempotency comparison
        _INDEX_TS_RE = re.compile(r"index regenerated=[^>]+")
        if (
            _INDEX_TS_RE.sub("", existing) == _INDEX_TS_RE.sub("", content)
            and _TIMESTAMP_RE.sub("", existing) == _TIMESTAMP_RE.sub("", content)
        ):
            return "unchanged"
    index_path.write_text(content, encoding="utf-8")
    return "written"


# ─── Top-level orchestration ─────────────────────────────────────────────────

PROCESSORS = {
    "merge-sections": process_merge_sections,
    "per-variant": process_per_variant,
    "per-component": process_per_component,
}


def process_poster(poster_path: Path) -> list[StandaloneFile]:
    """Read poster, parse annotation, dispatch to the right processor."""
    html_text = poster_path.read_text(encoding="utf-8")
    annotation = parse_annotation(html_text)
    if annotation is None:
        return []
    processor = PROCESSORS[annotation.mode]
    return processor(poster_path, annotation)


def collect_posters(posters_dir: Path, single: Path | None = None) -> list[Path]:
    if single is not None:
        return [single.resolve()]
    return sorted(posters_dir.glob("*.html"))


def main(argv: list[str] | None = None) -> int:
    global STANDALONE_DIR  # noqa: PLW0603 — allow --out-dir override
    p = argparse.ArgumentParser(
        description="Split annotated mockup posters into standalone HTML pages."
    )
    p.add_argument("--poster", type=Path, default=None,
                   help="Process a single poster file (otherwise: all in mockup/)")
    p.add_argument("--posters-dir", type=Path, default=POSTERS_DIR,
                   help="Directory containing source posters")
    p.add_argument("--out-dir", type=Path, default=STANDALONE_DIR,
                   help="Output directory for standalone files")
    p.add_argument("--dry-run", action="store_true",
                   help="Log actions without writing files")
    p.add_argument("--no-index", action="store_true",
                   help="Skip generating _index.html")
    p.add_argument("--verbose", "-v", action="store_true")
    args = p.parse_args(argv)

    # Override module-level STANDALONE_DIR if user passed --out-dir
    STANDALONE_DIR = args.out_dir

    posters = collect_posters(args.posters_dir, args.poster)
    if not posters:
        print(f"No posters found in {args.posters_dir}", file=sys.stderr)
        return 2

    all_files: list[StandaloneFile] = []
    annotated_count = 0
    skipped_count = 0
    errors: list[tuple[str, str]] = []

    for poster_path in posters:
        try:
            files = process_poster(poster_path)
        except (ValueError, RuntimeError) as e:
            errors.append((poster_path.name, str(e)))
            continue
        if not files:
            skipped_count += 1
            if args.verbose:
                print(f"  skip   {poster_path.name} (no <meta mockup-split-mode>)")
            continue
        annotated_count += 1
        for f in files:
            try:
                status = write_standalone(f, dry_run=args.dry_run)
            except RuntimeError as e:
                errors.append((f.path.name, str(e)))
                continue
            print(f"  {status:9} {f.path.relative_to(REPO_ROOT)}  ({f.mode}/{f.variant_key})")
        all_files.extend(files)

    if not args.no_index and all_files:
        try:
            status = generate_index(all_files, dry_run=args.dry_run)
            print(f"  {status:9} {(STANDALONE_DIR / '_index.html').relative_to(REPO_ROOT)}  (index)")
        except RuntimeError as e:
            errors.append(("_index.html", str(e)))

    print()
    print(
        f"Summary: {annotated_count} poster(s) processed, "
        f"{skipped_count} skipped, "
        f"{len(all_files)} standalone file(s) generated, "
        f"{len(errors)} error(s)."
    )
    if errors:
        print("\nErrors:")
        for name, msg in errors:
            print(f"  - {name}: {msg}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
