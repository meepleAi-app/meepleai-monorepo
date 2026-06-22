# Mockup Demo Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make all 69 mockup screens in `admin-mockups/design_files/` navigable as an interactive demo, starting from a new `index.html` entry that redirects to `sp4-dashboard.html`.

**Architecture:** Three Python scripts (stdlib only) implement a discovery → apply → validate pipeline. Phase A emits a human-reviewable `nav-map.md`, the user approves/edits it, Phase B patches up to 121 HTML+JSX files using the map, Phase C validates reachability. No build, no new runtime deps. Patches are idempotent via a `/* DEMO-NAV */` marker.

**Tech Stack:** Python 3.11+ stdlib (`pathlib`, `re`, `html.parser`, `json`, `difflib`), pytest for tests, git for checkpoints.

**Spec:** [`docs/superpowers/specs/2026-05-20-mockup-demo-navigation-design.md`](../specs/2026-05-20-mockup-demo-navigation-design.md)

---

## File Structure

```
scripts/mockup-demo/
├── __init__.py
├── clickable_extractor.py   # parses HTML + JSX, yields clickable element records
├── rule_engine.py            # applies §3.6 rule chain → destination
├── fuzzy_heuristic.py        # fallback fuzzy match button text ↔ mockup filename
├── html_patcher.py           # in-place HTML patcher with /* DEMO-NAV */ marker
├── jsx_patcher.py            # in-place JSX patcher with same marker
├── build_map.py              # CLI: produces nav-map.md
├── apply_map.py              # CLI: reads nav-map.md, patches files
├── validate.py               # CLI: produces nav-validation-report.html
├── data/
│   └── canonical_nav.json    # canonical sidebar/topbar mapping
└── tests/
    ├── __init__.py
    ├── conftest.py
    ├── test_clickable_extractor.py
    ├── test_rule_engine.py
    ├── test_fuzzy_heuristic.py
    ├── test_html_patcher.py
    ├── test_jsx_patcher.py
    ├── test_validate.py
    └── fixtures/
        ├── sample_static.html
        ├── sample_react_shell.html
        └── sample_component.jsx
```

**Outputs (not under `scripts/mockup-demo/`):**
- `admin-mockups/design_files/index.html` — new entry redirect.
- `admin-mockups/design_files/*` — patched mockups (existing files modified in place).
- `docs/superpowers/specs/nav-map.md` — Phase A output, user-editable.
- `docs/superpowers/specs/nav-validation-report.html` — Phase C output.
- `admin-mockups/README.md` — updated demo instructions.

**Rationale for split:** parsing, rule logic, fuzzy matching, and patching are independent concerns with distinct test surfaces. Each module is <200 LoC, focused, independently testable. The three CLI entry points compose these modules.

---

## Task 1: Scaffold project structure

**Files:**
- Create: `scripts/mockup-demo/__init__.py`
- Create: `scripts/mockup-demo/data/canonical_nav.json`
- Create: `scripts/mockup-demo/tests/__init__.py`
- Create: `scripts/mockup-demo/tests/conftest.py`
- Create: `scripts/mockup-demo/tests/fixtures/sample_static.html`
- Create: `scripts/mockup-demo/tests/fixtures/sample_react_shell.html`
- Create: `scripts/mockup-demo/tests/fixtures/sample_component.jsx`

- [ ] **Step 1: Verify parent directory exists**

```bash
ls scripts/ 2>/dev/null || mkdir -p scripts/
```

- [ ] **Step 2: Create module dirs and empty init files**

```bash
mkdir -p scripts/mockup-demo/data scripts/mockup-demo/tests/fixtures
touch scripts/mockup-demo/__init__.py scripts/mockup-demo/tests/__init__.py
```

- [ ] **Step 3: Create `canonical_nav.json`**

Write `scripts/mockup-demo/data/canonical_nav.json` with the canonical sidebar/topbar mapping from spec §3.4:

```json
{
  "sidebar": {
    "Dashboard": "sp4-dashboard.html",
    "Home": "sp4-dashboard.html",
    "Discover": "sp4-discover.html",
    "Library": "sp4-library-desktop.html",
    "Games": "sp4-games-index.html",
    "Players": "sp4-players-index.html",
    "Sessions": "sp4-sessions-index.html",
    "Agents": "sp4-agents-index.html",
    "Game Nights": "sp4-game-nights-index.html",
    "Knowledge Base": "sp4-kb-hub.html",
    "Toolkits": "sp4-hub-toolkits.html",
    "Notifications": "notifications.html",
    "Settings": "settings.html",
    "Profile": "settings.html",
    "Logout": "sp3-join.html"
  },
  "public_topbar": {
    "Login": "sp3-join.html",
    "Join": "sp3-join.html",
    "How it works": "sp3-how-it-works.html",
    "FAQ": "sp3-faq-enhanced.html",
    "Legal": "sp3-legal.html",
    "Terms": "sp3-legal.html",
    "Privacy": "sp3-legal.html"
  },
  "gameplay_topbar": {
    "Back": "sp4-game-detail.html",
    "Pause": "librogame-runthrough-resume-picker.html",
    "Menu": "librogame-runthrough-resume-picker.html",
    "Chat": "sp4-game-chat-tab.html",
    "Tutor": "sp4-game-chat-tab.html"
  },
  "event_topbar": {
    "Back": "sp4-game-nights-index.html"
  },
  "index_to_detail": {
    "sp4-games-index.html": "sp4-game-detail.html",
    "sp4-players-index.html": "sp4-player-detail.html",
    "sp4-sessions-index.html": "sp4-session-live.html",
    "sp4-agents-index.html": "sp4-agent-detail.html",
    "sp4-game-nights-index.html": "sp7-game-night-detail-rsvp.html",
    "sp4-kb-hub.html": "sp4-kb-detail.html",
    "sp4-hub-toolkits.html": "sp4-toolkit-detail.html",
    "sp4-hub-games.html": "sp4-game-detail.html",
    "sp4-hub-agents.html": "sp4-agent-detail.html",
    "sp4-discover.html": "sp4-game-detail.html"
  },
  "detail_action_keywords": {
    "avvia libro game": "librogame-runthrough-game-onboarding.html",
    "play": "librogame-runthrough-game-onboarding.html",
    "setup": "librogame-runthrough-setup-wizard.html",
    "new session": "librogame-runthrough-setup-wizard.html",
    "ai tutor": "sp4-game-chat-tab.html",
    "citation": "sp4-citation-pdf-viewer.html",
    "add to library": "sp4-library-desktop.html",
    "upload pdf": "sp4-upload-wizard-extended.html",
    "aggiungi gioco": "sp4-add-game-bgg-step.html",
    "crea game night": "sp7-game-night-create.html",
    "glossary": "sp6-libro-game-glossary-editor.html"
  }
}
```

- [ ] **Step 4: Create test fixtures**

Write `scripts/mockup-demo/tests/fixtures/sample_static.html`:

```html
<!doctype html>
<html><body>
  <nav class="sidebar">
    <a class="nav-item" href="#">Dashboard</a>
    <a class="nav-item" href="#">Games</a>
  </nav>
  <button class="cta-primary">Avvia libro game</button>
  <div class="game-card" data-game-id="42">Card content</div>
</body></html>
```

Write `scripts/mockup-demo/tests/fixtures/sample_react_shell.html`:

```html
<!doctype html>
<html><body>
  <div id="root"></div>
  <script type="text/babel" src="sample_component.jsx"></script>
</body></html>
```

Write `scripts/mockup-demo/tests/fixtures/sample_component.jsx`:

```jsx
const App = () => (
  <div>
    <nav className="sidebar">
      <li className="nav-item">Dashboard</li>
      <li className="nav-item">Games</li>
    </nav>
    <button className="cta-primary" onClick={() => handlePlay()}>
      Avvia libro game
    </button>
    <div className="game-card">Card</div>
  </div>
);
```

Write `scripts/mockup-demo/tests/conftest.py`:

```python
"""Shared pytest fixtures for mockup-demo tests."""
from pathlib import Path
import pytest

FIXTURES = Path(__file__).parent / "fixtures"

@pytest.fixture
def sample_html_path():
    return FIXTURES / "sample_static.html"

@pytest.fixture
def sample_jsx_path():
    return FIXTURES / "sample_component.jsx"
```

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/
git commit -m "feat(mockup-demo): scaffold module structure and fixtures (refs spec 2026-05-20)"
```

---

## Task 2: Clickable extractor for HTML

**Files:**
- Create: `scripts/mockup-demo/clickable_extractor.py`
- Create: `scripts/mockup-demo/tests/test_clickable_extractor.py`

- [ ] **Step 1: Write failing test for HTML extraction**

Write `scripts/mockup-demo/tests/test_clickable_extractor.py`:

```python
from scripts.mockup_demo.clickable_extractor import extract_clickables, Clickable

def test_extract_html_anchors_and_buttons(sample_html_path):
    items = list(extract_clickables(sample_html_path))
    assert any(it.text.strip() == "Dashboard" and it.tag == "a" for it in items)
    assert any(it.text.strip() == "Games" and it.tag == "a" for it in items)
    assert any(it.text.strip() == "Avvia libro game" and it.tag == "button" for it in items)
    assert any("game-card" in (it.classes or "") for it in items)

def test_clickable_has_required_fields(sample_html_path):
    items = list(extract_clickables(sample_html_path))
    for it in items:
        assert it.file_path == sample_html_path
        assert it.tag in ("a", "button", "div", "li", "span")
        assert it.line_number > 0
        assert it.snippet  # exact source snippet for later locating
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_clickable_extractor.py -v`
Expected: ImportError / ModuleNotFoundError on `clickable_extractor`.

- [ ] **Step 3: Implement HTML extractor**

Write `scripts/mockup-demo/clickable_extractor.py`:

```python
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
    # implemented in Task 3
    return iter(())
```

- [ ] **Step 4: Run test to verify it passes**

Run: `python -m pytest scripts/mockup-demo/tests/test_clickable_extractor.py::test_extract_html_anchors_and_buttons -v`
Expected: PASS for HTML test. JSX test left for next task.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/clickable_extractor.py scripts/mockup-demo/tests/test_clickable_extractor.py
git commit -m "feat(mockup-demo): HTML clickable extractor with tests"
```

---

## Task 3: Clickable extractor for JSX

**Files:**
- Modify: `scripts/mockup-demo/clickable_extractor.py`
- Modify: `scripts/mockup-demo/tests/test_clickable_extractor.py`

- [ ] **Step 1: Add failing JSX test**

Append to `scripts/mockup-demo/tests/test_clickable_extractor.py`:

```python
def test_extract_jsx_clickables(sample_jsx_path):
    items = list(extract_clickables(sample_jsx_path))
    assert any(it.text.strip() == "Dashboard" and it.kind == "jsx" for it in items)
    assert any(it.text.strip() == "Avvia libro game" and it.on_click_existing is not None for it in items)
    assert any("game-card" in (it.classes or "") for it in items)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_clickable_extractor.py::test_extract_jsx_clickables -v`
Expected: FAIL (JSX extractor returns nothing).

- [ ] **Step 3: Implement JSX extractor**

Replace `_extract_jsx` body in `scripts/mockup-demo/clickable_extractor.py`:

```python
_JSX_TAG_RE = re.compile(
    r"<(?P<tag>a|button|div|li|span)\b(?P<attrs>[^>]*?)>(?P<inner>[^<{}]*)",
    re.IGNORECASE | re.DOTALL,
)
_JSX_CLASSNAME_RE = re.compile(r'className\s*=\s*"([^"]*)"')
_JSX_ONCLICK_RE = re.compile(r"onClick\s*=\s*\{([^}]*)\}")


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
            yield Clickable(
                file_path=path,
                tag=tag,
                line_number=text.count("\n", 0, m.start()) + 1,
                text=inner,
                classes=classes,
                snippet=m.group(0),
                on_click_existing=on_click.group(1).strip() if on_click else None,
                kind="jsx",
            )
```

- [ ] **Step 4: Run all extractor tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_clickable_extractor.py -v`
Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/clickable_extractor.py scripts/mockup-demo/tests/test_clickable_extractor.py
git commit -m "feat(mockup-demo): JSX clickable extractor"
```

---

## Task 4: Rule engine — canonical nav lookup

**Files:**
- Create: `scripts/mockup-demo/rule_engine.py`
- Create: `scripts/mockup-demo/tests/test_rule_engine.py`

- [ ] **Step 1: Write failing test**

Write `scripts/mockup-demo/tests/test_rule_engine.py`:

```python
from pathlib import Path
from scripts.mockup_demo.clickable_extractor import Clickable
from scripts.mockup_demo.rule_engine import resolve_destination, Decision

def _click(text="", classes="", file_name="sp4-dashboard.jsx", tag="li"):
    return Clickable(
        file_path=Path(f"admin-mockups/design_files/{file_name}"),
        tag=tag,
        line_number=1,
        text=text,
        classes=classes,
        snippet="<...>",
        on_click_existing=None,
        kind="jsx" if file_name.endswith(".jsx") else "html",
    )

def test_canonical_sidebar_lookup():
    decision = resolve_destination(_click(text="Games", classes="sidebar nav-item"))
    assert decision.destination == "sp4-games-index.html"
    assert decision.rule == "canonical-sidebar"
    assert decision.confidence >= 0.95

def test_canonical_public_topbar_lookup():
    decision = resolve_destination(_click(text="FAQ", classes="topbar-link", file_name="sp3-join.html", tag="a"))
    assert decision.destination == "sp3-faq-enhanced.html"
    assert decision.rule == "canonical-public-topbar"

def test_no_match_returns_none_destination():
    decision = resolve_destination(_click(text="Some random text", classes=""))
    assert decision.destination is None
    assert decision.rule == "no-match"
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_rule_engine.py -v`
Expected: ImportError on `rule_engine`.

- [ ] **Step 3: Implement rule engine canonical lookups**

Write `scripts/mockup-demo/rule_engine.py`:

```python
"""Decides destination mockup for a Clickable using ordered rule chain.

Rules (in priority order):
  1. canonical-sidebar      (text matches a sidebar key, classes hint nav/sidebar)
  2. canonical-public-topbar
  3. canonical-gameplay-topbar
  4. canonical-event-topbar
  5. index-to-detail        (file is *-index.html + element is row/card)
  6. detail-action-keyword  (text matches a known action keyword)
  7. fuzzy-heuristic        (delegated to fuzzy_heuristic.score)
  8. no-match
"""
from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
import json
import re

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


def _try_canonical(click: Clickable, table_key: str, class_hint: str, rule_name: str) -> Decision | None:
    table = _NAV[table_key]
    text = _norm(click.text)
    for key, dest in table.items():
        if _norm(key) == text and (not class_hint or class_hint in (click.classes or "").lower()):
            return Decision(dest, rule_name, 0.98, f"text '{click.text}' matches {table_key}['{key}']")
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
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_rule_engine.py -v`
Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/rule_engine.py scripts/mockup-demo/tests/test_rule_engine.py
git commit -m "feat(mockup-demo): rule engine canonical nav lookups"
```

---

## Task 5: Rule engine — index→detail and detail-action rules

**Files:**
- Modify: `scripts/mockup-demo/rule_engine.py`
- Modify: `scripts/mockup-demo/tests/test_rule_engine.py`

- [ ] **Step 1: Add failing tests**

Append to `scripts/mockup-demo/tests/test_rule_engine.py`:

```python
def test_index_to_detail():
    # Click on a card inside sp4-games-index → game detail
    decision = resolve_destination(_click(
        text="Catan", classes="game-card", file_name="sp4-games-index.jsx", tag="div"
    ))
    assert decision.destination == "sp4-game-detail.html"
    assert decision.rule == "index-to-detail"

def test_detail_action_keyword():
    decision = resolve_destination(_click(
        text="Avvia libro game", classes="cta-primary", file_name="sp4-game-detail.jsx", tag="button"
    ))
    assert decision.destination == "librogame-runthrough-game-onboarding.html"
    assert decision.rule == "detail-action-keyword"

def test_canonical_takes_priority_over_keyword():
    # 'Settings' is a canonical sidebar entry, must not match any keyword
    decision = resolve_destination(_click(text="Settings", classes="sidebar nav-item"))
    assert decision.destination == "settings.html"
    assert decision.rule == "canonical-sidebar"
```

- [ ] **Step 2: Run tests to verify failure**

Run: `python -m pytest scripts/mockup-demo/tests/test_rule_engine.py::test_index_to_detail -v`
Expected: FAIL (destination is None).

- [ ] **Step 3: Extend rule_engine.py with rules 5 and 6**

Add to `scripts/mockup-demo/rule_engine.py` BEFORE the final `return Decision(None, "no-match", ...)`:

```python
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
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_rule_engine.py -v`
Expected: 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/rule_engine.py scripts/mockup-demo/tests/test_rule_engine.py
git commit -m "feat(mockup-demo): index-to-detail and detail-action rules"
```

---

## Task 6: Fuzzy heuristic for ambiguous cases

**Files:**
- Create: `scripts/mockup-demo/fuzzy_heuristic.py`
- Create: `scripts/mockup-demo/tests/test_fuzzy_heuristic.py`
- Modify: `scripts/mockup-demo/rule_engine.py`

- [ ] **Step 1: Write failing test**

Write `scripts/mockup-demo/tests/test_fuzzy_heuristic.py`:

```python
from scripts.mockup_demo.fuzzy_heuristic import score_candidates

CATALOG = [
    "sp4-games-index.html",
    "sp4-game-detail.html",
    "sp7-game-night-live.html",
    "sp4-discover.html",
]

def test_high_confidence_match():
    results = score_candidates("Discover", CATALOG)
    assert results[0][0] == "sp4-discover.html"
    assert results[0][1] >= 0.7

def test_low_confidence_match():
    results = score_candidates("Random unrelated text 123", CATALOG)
    assert results[0][1] < 0.7

def test_returns_sorted_descending():
    results = score_candidates("game", CATALOG)
    scores = [s for _, s in results]
    assert scores == sorted(scores, reverse=True)
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_fuzzy_heuristic.py -v`
Expected: ModuleNotFoundError.

- [ ] **Step 3: Implement fuzzy heuristic**

Write `scripts/mockup-demo/fuzzy_heuristic.py`:

```python
"""Scores button text against mockup filename catalog using difflib.SequenceMatcher.

Returns list of (filename, score) sorted descending. Threshold for usable
match is 0.7 (caller decides).
"""
from __future__ import annotations
from difflib import SequenceMatcher
import re


def _normalize_filename(name: str) -> str:
    # Drop prefix tokens (sp3-, sp4-, sp6-, sp7-, librogame-, nanolith-)
    base = re.sub(r"^(sp[3467]-|librogame-|nanolith-)", "", name)
    base = base.replace(".html", "").replace("-", " ")
    return base.strip().lower()


def _score(query: str, candidate: str) -> float:
    return SequenceMatcher(None, query.lower().strip(), _normalize_filename(candidate)).ratio()


def score_candidates(query: str, catalog: list[str]) -> list[tuple[str, float]]:
    scored = [(c, _score(query, c)) for c in catalog]
    return sorted(scored, key=lambda t: t[1], reverse=True)
```

- [ ] **Step 4: Wire fuzzy heuristic into rule_engine as rule 7**

Edit `scripts/mockup-demo/rule_engine.py`. Add import at top:

```python
from scripts.mockup_demo.fuzzy_heuristic import score_candidates
```

Add a module-level catalog loader (cached):

```python
_CATALOG: list[str] | None = None

def _catalog() -> list[str]:
    global _CATALOG
    if _CATALOG is None:
        mockups_dir = Path(__file__).resolve().parents[2] / "admin-mockups" / "design_files"
        _CATALOG = sorted(p.name for p in mockups_dir.glob("*.html"))
    return _CATALOG
```

Insert rule 7 in `resolve_destination`, BEFORE the final `no-match`:

```python
    # 7. fuzzy-heuristic
    if click.text and len(click.text.strip()) >= 3:
        scored = score_candidates(click.text, _catalog())
        if scored and scored[0][1] >= 0.7:
            dest, score = scored[0]
            return Decision(
                dest, "fuzzy-heuristic", score,
                f"fuzzy match '{click.text}' → '{dest}' (score {score:.2f})"
            )
```

- [ ] **Step 5: Run all tests**

Run: `python -m pytest scripts/mockup-demo/tests/ -v`
Expected: 9 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add scripts/mockup-demo/fuzzy_heuristic.py scripts/mockup-demo/rule_engine.py scripts/mockup-demo/tests/test_fuzzy_heuristic.py
git commit -m "feat(mockup-demo): fuzzy heuristic with rule_engine integration"
```

---

## Task 7: build_map.py CLI — emit nav-map.md

**Files:**
- Create: `scripts/mockup-demo/build_map.py`

- [ ] **Step 1: Write build_map.py**

```python
"""CLI: scans admin-mockups/design_files/, applies rule engine, emits nav-map.md.

Usage:
  python -m scripts.mockup_demo.build_map [--out PATH]

Output columns: File | Selector | Button text | Destination | Confidence | Rule
"""
from __future__ import annotations
from pathlib import Path
import argparse
import json
import sys

from scripts.mockup_demo.clickable_extractor import extract_clickables
from scripts.mockup_demo.rule_engine import resolve_destination

REPO_ROOT = Path(__file__).resolve().parents[2]
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
DEFAULT_OUT = REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-map.md"


def _selector(click) -> str:
    """Stable identifier: tag + first 30 chars of snippet hash for locating later."""
    return f"{click.tag}:L{click.line_number}:{click.text[:30].strip()}"


def build_map(mockups_dir: Path, out_path: Path) -> int:
    rows = []
    files = sorted(list(mockups_dir.glob("*.html")) + list(mockups_dir.glob("*.jsx")))
    for f in files:
        for click in extract_clickables(f):
            decision = resolve_destination(click)
            rows.append((f.name, click, decision))

    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8") as out:
        out.write("# Mockup Demo Navigation Map\n\n")
        out.write(f"Generated by `scripts/mockup-demo/build_map.py`. "
                  f"Edit destinations in the 'TODO/Ambiguous' section below; "
                  f"do not edit auto-rules unless overriding.\n\n")
        out.write(f"**Total clickables:** {len(rows)}\n\n")
        # Group by file
        by_file: dict[str, list] = {}
        for fname, click, dec in rows:
            by_file.setdefault(fname, []).append((click, dec))

        # Section 1: auto-resolved rows
        out.write("## Auto-resolved\n\n")
        out.write("| File | Selector | Button text | Destination | Confidence | Rule |\n")
        out.write("|---|---|---|---|---|---|\n")
        for fname in sorted(by_file):
            for click, dec in by_file[fname]:
                if dec.destination:
                    out.write(
                        f"| `{fname}` | `{_selector(click)}` "
                        f"| {click.text[:40]} | `{dec.destination}` "
                        f"| {dec.confidence:.2f} | {dec.rule} |\n"
                    )

        # Section 2: TODO / Ambiguous
        out.write("\n## TODO / Ambiguous\n\n")
        out.write("Fill in the Destination column. Use `OUT_OF_SCOPE` to apply no-op policy.\n\n")
        out.write("| File | Selector | Button text | Destination | Rationale |\n")
        out.write("|---|---|---|---|---|\n")
        for fname in sorted(by_file):
            for click, dec in by_file[fname]:
                if not dec.destination:
                    out.write(
                        f"| `{fname}` | `{_selector(click)}` "
                        f"| {click.text[:40]} | `TODO` | {dec.rationale} |\n"
                    )
    return len(rows)


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--out", type=Path, default=DEFAULT_OUT)
    p.add_argument("--mockups-dir", type=Path, default=MOCKUPS_DIR)
    args = p.parse_args(argv)
    n = build_map(args.mockups_dir, args.out)
    print(f"Wrote {args.out} ({n} clickables)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Smoke test build_map on real mockups**

Run: `cd D:/Repositories/meepleai-monorepo-main && python -m scripts.mockup_demo.build_map`
Expected output: `Wrote .../docs/superpowers/specs/nav-map.md (N clickables)` where N > 200.

- [ ] **Step 3: Inspect the produced nav-map.md**

Run: `head -50 docs/superpowers/specs/nav-map.md`
Expected: header + Auto-resolved table with sidebar rows visible.

- [ ] **Step 4: Commit script + generated map**

```bash
git add scripts/mockup-demo/build_map.py docs/superpowers/specs/nav-map.md
git commit -m "feat(mockup-demo): build_map CLI + initial nav-map.md"
```

---

## Task 8: USER REVIEW GATE — nav-map.md

- [ ] **Step 1: Ask user to review nav-map.md**

Message to user:

> `nav-map.md` is committed at `docs/superpowers/specs/nav-map.md`. Please review:
> 1. **Auto-resolved** section — sample 10–20 rows, flag any wrong destinations.
> 2. **TODO/Ambiguous** section — fill in destinations (or write `OUT_OF_SCOPE`).
>
> Reply with edits inline or commit your changes; I'll proceed to Phase B once you confirm.

- [ ] **Step 2: Wait for user approval**

Do NOT proceed to Task 9 until user confirms.

- [ ] **Step 3: After confirmation, commit any edits**

If user modified `nav-map.md`:

```bash
git add docs/superpowers/specs/nav-map.md
git commit -m "chore(mockup-demo): user-approved nav-map edits"
```

---

## Task 9: HTML patcher with idempotence marker

**Files:**
- Create: `scripts/mockup-demo/html_patcher.py`
- Create: `scripts/mockup-demo/tests/test_html_patcher.py`

- [ ] **Step 1: Write failing test**

Write `scripts/mockup-demo/tests/test_html_patcher.py`:

```python
from pathlib import Path
from scripts.mockup_demo.html_patcher import patch_html_element

DEMO_MARKER = "/* DEMO-NAV */"

def test_patch_anchor_adds_href(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<a class="nav-item" href="#">Games</a>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games</a>',
                       destination="sp4-games-index.html")
    content = f.read_text(encoding="utf-8")
    assert 'href="sp4-games-index.html"' in content
    assert DEMO_MARKER in content

def test_idempotent(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<a class="nav-item" href="#">Games</a>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games</a>',
                       destination="sp4-games-index.html")
    once = f.read_text(encoding="utf-8")
    patch_html_element(f, selector_snippet='<a class="nav-item" href="#">Games</a>',
                       destination="sp4-games-index.html")
    twice = f.read_text(encoding="utf-8")
    assert once == twice

def test_button_gets_onclick(tmp_path):
    f = tmp_path / "x.html"
    f.write_text('<button class="cta">Play</button>', encoding="utf-8")
    patch_html_element(f, selector_snippet='<button class="cta">Play</button>',
                       destination="librogame-runthrough-game-onboarding.html")
    content = f.read_text(encoding="utf-8")
    assert "onclick=" in content
    assert "librogame-runthrough-game-onboarding.html" in content
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_html_patcher.py -v`
Expected: ModuleNotFoundError.

- [ ] **Step 3: Implement html_patcher**

Write `scripts/mockup-demo/html_patcher.py`:

```python
"""Patches HTML files in place with navigation, idempotent via DEMO-NAV marker."""
from __future__ import annotations
from pathlib import Path
import re

DEMO_MARKER = "/* DEMO-NAV */"


def patch_html_element(path: Path, selector_snippet: str, destination: str) -> bool:
    """Return True if file was modified, False if already patched (idempotent)."""
    if not destination or destination == "OUT_OF_SCOPE":
        return False
    content = path.read_text(encoding="utf-8")
    if selector_snippet not in content:
        return False
    # If snippet already has DEMO_MARKER, skip
    idx = content.index(selector_snippet)
    end = idx + len(selector_snippet)
    # Look back a few chars for marker
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
    # Case A: <a ...> → set/replace href
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
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_html_patcher.py -v`
Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/html_patcher.py scripts/mockup-demo/tests/test_html_patcher.py
git commit -m "feat(mockup-demo): HTML patcher with idempotence"
```

---

## Task 10: JSX patcher

**Files:**
- Create: `scripts/mockup-demo/jsx_patcher.py`
- Create: `scripts/mockup-demo/tests/test_jsx_patcher.py`

- [ ] **Step 1: Write failing test**

Write `scripts/mockup-demo/tests/test_jsx_patcher.py`:

```python
from pathlib import Path
from scripts.mockup_demo.jsx_patcher import patch_jsx_element

DEMO_MARKER = "/* DEMO-NAV */"

def test_jsx_element_without_onclick(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text('<li className="nav-item">Games</li>', encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games</li>',
                      destination="sp4-games-index.html")
    content = f.read_text(encoding="utf-8")
    assert "onClick" in content
    assert "sp4-games-index.html" in content
    assert DEMO_MARKER in content

def test_jsx_element_with_existing_onclick_wraps(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text(
        '<button className="cta" onClick={() => handlePlay()}>Play</button>',
        encoding="utf-8",
    )
    patch_jsx_element(f, selector_snippet='<button className="cta" onClick={() => handlePlay()}>Play</button>',
                      destination="librogame-runthrough-game-onboarding.html")
    content = f.read_text(encoding="utf-8")
    assert "handlePlay()" in content  # original preserved
    assert "librogame-runthrough-game-onboarding.html" in content
    assert DEMO_MARKER in content

def test_idempotent(tmp_path):
    f = tmp_path / "c.jsx"
    f.write_text('<li className="nav-item">Games</li>', encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games</li>',
                      destination="sp4-games-index.html")
    once = f.read_text(encoding="utf-8")
    patch_jsx_element(f, selector_snippet='<li className="nav-item">Games</li>',
                      destination="sp4-games-index.html")
    twice = f.read_text(encoding="utf-8")
    assert once == twice
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_jsx_patcher.py -v`
Expected: ModuleNotFoundError.

- [ ] **Step 3: Implement jsx_patcher**

Write `scripts/mockup-demo/jsx_patcher.py`:

```python
"""Patches JSX files in place with onClick navigation, idempotent."""
from __future__ import annotations
from pathlib import Path
import re

DEMO_MARKER = "/* DEMO-NAV */"

_OPEN_TAG_RE = re.compile(r"^<(?P<tag>\w+)\b(?P<attrs>[^>]*?)>", re.DOTALL)
_ONCLICK_RE = re.compile(r"onClick\s*=\s*\{(?P<body>[^}]*)\}")


def patch_jsx_element(path: Path, selector_snippet: str, destination: str) -> bool:
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
    new_content = content[:idx] + patched + content[end:]
    path.write_text(new_content, encoding="utf-8")
    return True


def _apply_patch(snippet: str, destination: str) -> str:
    m = _OPEN_TAG_RE.match(snippet)
    if not m:
        return snippet
    attrs = m.group("attrs")
    rest = snippet[m.end():]
    nav_call = (
        f"setTimeout(() => {{ window.location.href = '{destination}'; }}, 0); "
        f"{DEMO_MARKER}"
    )
    existing = _ONCLICK_RE.search(attrs)
    if existing:
        existing_body = existing.group("body").strip()
        # Wrap: preserve original, then navigate
        new_onclick = (
            f"onClick={{(e) => {{ ({existing_body})(e); {nav_call} }}}}"
        )
        new_attrs = _ONCLICK_RE.sub(new_onclick, attrs)
    else:
        new_attrs = attrs + f" onClick={{() => {{ {nav_call} }}}}"
    return f"<{m.group('tag')}{new_attrs}>" + rest
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_jsx_patcher.py -v`
Expected: 3 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/mockup-demo/jsx_patcher.py scripts/mockup-demo/tests/test_jsx_patcher.py
git commit -m "feat(mockup-demo): JSX patcher with onClick wrapping"
```

---

## Task 11: apply_map.py CLI

**Files:**
- Create: `scripts/mockup-demo/apply_map.py`

- [ ] **Step 1: Write apply_map.py**

```python
"""CLI: reads docs/superpowers/specs/nav-map.md and patches mockup files.

Usage:
  python -m scripts.mockup_demo.apply_map [--dry-run] [--map PATH]
"""
from __future__ import annotations
from pathlib import Path
import argparse
import re
import sys

from scripts.mockup_demo.html_patcher import patch_html_element
from scripts.mockup_demo.jsx_patcher import patch_jsx_element
from scripts.mockup_demo.clickable_extractor import extract_clickables

REPO_ROOT = Path(__file__).resolve().parents[2]
MOCKUPS_DIR = REPO_ROOT / "admin-mockups" / "design_files"
DEFAULT_MAP = REPO_ROOT / "docs" / "superpowers" / "specs" / "nav-map.md"

_ROW_RE = re.compile(
    r"\|\s*`([^`]+)`\s*\|\s*`([^`]+)`\s*\|\s*([^|]*)\s*\|\s*`([^`]+)`\s*\|"
)


def parse_nav_map(map_path: Path) -> list[tuple[str, str, str, str]]:
    """Returns list of (filename, selector, text, destination) tuples.

    Skips rows where destination is 'TODO' or 'OUT_OF_SCOPE'.
    """
    rows = []
    for line in map_path.read_text(encoding="utf-8").splitlines():
        m = _ROW_RE.match(line.strip())
        if not m:
            continue
        fname, selector, text, dest = m.groups()
        if dest.strip() in ("TODO", "OUT_OF_SCOPE", "Destination"):
            continue
        rows.append((fname.strip(), selector.strip(), text.strip(), dest.strip()))
    return rows


def _find_snippet(file_path: Path, selector: str) -> str | None:
    """Selector format: 'tag:Lline:text-prefix'. Re-extract and match."""
    parts = selector.split(":", 2)
    if len(parts) < 3:
        return None
    text_prefix = parts[2]
    for click in extract_clickables(file_path):
        if click.text.strip().startswith(text_prefix.strip()):
            return click.snippet
    return None


def apply_map(map_path: Path, mockups_dir: Path, dry_run: bool = False) -> dict:
    stats = {"patched": 0, "skipped": 0, "missing": 0}
    for fname, selector, _text, dest in parse_nav_map(map_path):
        fpath = mockups_dir / fname
        if not fpath.exists():
            stats["missing"] += 1
            continue
        snippet = _find_snippet(fpath, selector)
        if not snippet:
            stats["missing"] += 1
            continue
        if dry_run:
            print(f"DRY-RUN would patch {fname}: {selector} -> {dest}")
            stats["patched"] += 1
            continue
        if fpath.suffix == ".jsx":
            patched = patch_jsx_element(fpath, snippet, dest)
        else:
            patched = patch_html_element(fpath, snippet, dest)
        stats["patched" if patched else "skipped"] += 1
    return stats


def main(argv=None):
    p = argparse.ArgumentParser()
    p.add_argument("--map", type=Path, default=DEFAULT_MAP)
    p.add_argument("--mockups-dir", type=Path, default=MOCKUPS_DIR)
    p.add_argument("--dry-run", action="store_true")
    args = p.parse_args(argv)
    stats = apply_map(args.map, args.mockups_dir, args.dry_run)
    print(f"Stats: {stats}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

**Note on OUT_OF_SCOPE rows (spec §3.7 deviation):** `apply_map` skips rows where the destination is `OUT_OF_SCOPE` instead of injecting an explicit `e.preventDefault() + console.info` no-op. Skipped elements remain functionally inert (no href, no onClick added). The console-info tracking from the spec is dropped as a non-critical nice-to-have. If tracking is needed later, add a `patch_no_op_html/jsx` helper and route OUT_OF_SCOPE through it.

- [ ] **Step 2: Dry-run on real data**

Run: `python -m scripts.mockup_demo.apply_map --dry-run`
Expected: prints `DRY-RUN would patch ...` lines and stats. No file modified.

- [ ] **Step 3: Verify dry-run did not modify**

Run: `git status admin-mockups/design_files/`
Expected: clean (no modifications).

- [ ] **Step 4: Commit script**

```bash
git add scripts/mockup-demo/apply_map.py
git commit -m "feat(mockup-demo): apply_map CLI with dry-run"
```

---

## Task 12: Create index.html entry redirect

**Files:**
- Create: `admin-mockups/design_files/index.html`

- [ ] **Step 1: Write index.html**

```html
<!doctype html>
<html lang="it">
<head>
  <meta charset="utf-8">
  <meta http-equiv="refresh" content="0; url=sp4-dashboard.html">
  <title>MeepleAI Mockup Demo</title>
  <script>window.location.replace('sp4-dashboard.html');</script>
</head>
<body>
  <p>Redirecting to <a href="sp4-dashboard.html">dashboard</a>…</p>
</body>
</html>
```

- [ ] **Step 2: Verify by opening in browser via MCP**

Use Playwright/Chrome MCP to navigate to `file:///D:/Repositories/meepleai-monorepo-main/admin-mockups/design_files/index.html` and verify final URL ends in `sp4-dashboard.html`.

- [ ] **Step 3: Commit**

```bash
git add admin-mockups/design_files/index.html
git commit -m "feat(mockup-demo): add index.html entry redirect to dashboard"
```

---

## Task 13: Apply nav-map.md for real

**Files:**
- Modify: up to 121 files in `admin-mockups/design_files/`

- [ ] **Step 1: Run apply_map.py for real**

```bash
python -m scripts.mockup_demo.apply_map
```

Expected output: `Stats: {'patched': N, 'skipped': M, 'missing': K}` with N > 100.

- [ ] **Step 2: Inspect a sample of patched files**

```bash
git diff --stat admin-mockups/design_files/ | tail -10
git diff admin-mockups/design_files/sp4-dashboard.jsx | head -40
```

Expected: `/* DEMO-NAV */` markers visible, `onClick` or `href` additions visible.

- [ ] **Step 3: Verify idempotence**

```bash
python -m scripts.mockup_demo.apply_map
```

Expected stats: `patched: 0` (all already marked).

- [ ] **Step 4: Commit patched files**

```bash
git add admin-mockups/design_files/
git commit -m "feat(mockup-demo): apply nav wiring to all mockup files"
```

---

## Task 14: validate.py — existence check + BFS reachability

**Files:**
- Create: `scripts/mockup-demo/validate.py`
- Create: `scripts/mockup-demo/tests/test_validate.py`

- [ ] **Step 1: Write failing test**

Write `scripts/mockup-demo/tests/test_validate.py`:

```python
from pathlib import Path
from scripts.mockup_demo.validate import extract_targets, bfs_reachable

def test_extract_targets_html(tmp_path):
    f = tmp_path / "a.html"
    f.write_text('<a href="b.html">b</a> <button onclick="window.location.href=\'c.html\'">c</button>',
                 encoding="utf-8")
    targets = extract_targets(f)
    assert "b.html" in targets
    assert "c.html" in targets

def test_extract_targets_jsx(tmp_path):
    f = tmp_path / "a.jsx"
    f.write_text("onClick={() => { window.location.href = 'd.html' }}", encoding="utf-8")
    targets = extract_targets(f)
    assert "d.html" in targets

def test_bfs_reachable(tmp_path):
    (tmp_path / "index.html").write_text('<a href="a.html">a</a>', encoding="utf-8")
    (tmp_path / "a.html").write_text('<a href="b.html">b</a>', encoding="utf-8")
    (tmp_path / "b.html").write_text('end', encoding="utf-8")
    (tmp_path / "orphan.html").write_text('orphan', encoding="utf-8")
    reachable, total = bfs_reachable(tmp_path, "index.html")
    assert "index.html" in reachable
    assert "a.html" in reachable
    assert "b.html" in reachable
    assert "orphan.html" not in reachable
```

- [ ] **Step 2: Run test to verify it fails**

Run: `python -m pytest scripts/mockup-demo/tests/test_validate.py -v`
Expected: ModuleNotFoundError.

- [ ] **Step 3: Implement validate.py**

```python
"""CLI: validates nav wiring — target existence + BFS reachability + report.

Usage:
  python -m scripts.mockup_demo.validate [--report PATH]
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

_HREF_RE = re.compile(r'href\s*=\s*"([^"#][^"]*\.html)"', re.IGNORECASE)
_LOCATION_RE = re.compile(r"window\.location\.(?:href|replace)\s*=?\s*\(?\s*['\"]([^'\"]+\.html)['\"]")
_ONCLICK_RE = re.compile(r'onclick\s*=\s*"[^"]*?([\w\-]+\.html)', re.IGNORECASE)


def extract_targets(path: Path) -> set[str]:
    text = path.read_text(encoding="utf-8")
    targets = set()
    for rx in (_HREF_RE, _LOCATION_RE, _ONCLICK_RE):
        for m in rx.finditer(text):
            targets.add(m.group(1))
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


def emit_report(mockups_dir: Path, report_path: Path) -> dict:
    reachable, total = bfs_reachable(mockups_dir, "index.html")
    all_html = {p.name for p in mockups_dir.glob("*.html")}
    orphans = sorted(all_html - reachable)
    broken = _broken_targets(mockups_dir)
    EXCLUDED = {f"0{i}-{n}" for i, n in
                [(0, "hub.html"), (1, "screens.html"), (2, "desktop-patterns.html"),
                 (3, "drawer-variants.html"), (4, "design-system.html"), (5, "dark-mode.html")]}
    EXCLUDED |= {"nanolith-nav-topbar.html", "nanolith-nav-bottom-mobile.html",
                 "nanolith-nav-chat-panel.html"}
    user_facing_total = len(all_html - EXCLUDED)
    user_facing_reachable = len((all_html - EXCLUDED) & reachable)
    pct = (user_facing_reachable / user_facing_total * 100) if user_facing_total else 0.0

    report_path.parent.mkdir(parents=True, exist_ok=True)
    with report_path.open("w", encoding="utf-8") as out:
        out.write("<!doctype html><html><body>\n")
        out.write("<h1>Mockup Demo Nav Validation</h1>\n")
        out.write(f"<p><strong>Reachable (user-facing):</strong> "
                  f"{user_facing_reachable}/{user_facing_total} ({pct:.1f}%)</p>\n")
        out.write(f"<p><strong>Total HTML files:</strong> {total}</p>\n")
        out.write("<h2>Orphans</h2><ul>\n")
        for o in orphans:
            tag = " (excluded)" if o in EXCLUDED else ""
            out.write(f"<li>{o}{tag}</li>\n")
        out.write("</ul>\n")
        out.write("<h2>Broken targets</h2><ul>\n")
        for src, tgt in broken:
            out.write(f"<li><code>{src}</code> → missing <code>{tgt}</code></li>\n")
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
```

- [ ] **Step 4: Run tests**

Run: `python -m pytest scripts/mockup-demo/tests/test_validate.py -v`
Expected: 3 tests PASS.

- [ ] **Step 5: Run validate on real data**

Run: `python -m scripts.mockup_demo.validate`
Expected: prints stats with `percent` ≥ 95.0; exit code 0.

- [ ] **Step 6: Commit script and report**

```bash
git add scripts/mockup-demo/validate.py scripts/mockup-demo/tests/test_validate.py docs/superpowers/specs/nav-validation-report.html
git commit -m "feat(mockup-demo): validate CLI with reachability report"
```

---

## Task 15: Browser spot-check

- [ ] **Step 1: Open demo in Chrome via MCP**

Navigate to `file:///D:/Repositories/meepleai-monorepo-main/admin-mockups/design_files/index.html` using `mcp__claude-in-chrome__navigate` (load via ToolSearch first).

- [ ] **Step 2: Verify final URL is sp4-dashboard.html**

Use `mcp__claude-in-chrome__tabs_context_mcp` to read current URL.
Expected: ends with `sp4-dashboard.html`.

- [ ] **Step 3: Click 10 representative paths**

Walk each path manually via `mcp__claude-in-chrome__find` + click. Expected paths:
1. Dashboard → sidebar "Games" → games-index → first row → game-detail
2. game-detail → "Avvia libro game" → librogame onboarding → setup → play-session
3. play-session → encounter CTA → encounter-cheatsheet
4. play-session → "Traduci paragrafo" → translate-viewer
5. play-session → session end → resume-picker
6. Dashboard → sidebar "Game Nights" → index → row → detail-rsvp → live → summary
7. Dashboard → "Notifications" → notifications.html
8. Logo (any authenticated mockup) → back to dashboard
9. Dashboard → "Logout" → sp3-join → "How it works" → sp3-how-it-works
10. sp3-join → auth-flow → onboarding → sp4-dashboard

- [ ] **Step 4: Read console messages**

Use `mcp__claude-in-chrome__read_console_messages` with `pattern: "error"`.
Expected: zero matches except expected `demo: no target for X` info traces.

- [ ] **Step 5: Document any failures**

If any step fails, add the failing case to `docs/superpowers/specs/nav-validation-report.html` under a new `<h2>Manual spot-check failures</h2>` section, then commit.

---

## Task 16: Update README

**Files:**
- Create or modify: `admin-mockups/README.md`

- [ ] **Step 1: Check if README exists**

```bash
ls admin-mockups/README.md 2>/dev/null
```

- [ ] **Step 2: Write/update README**

Write `admin-mockups/README.md`:

```markdown
# MeepleAI Mockups

Static HTML/JSX mockups covering the full product surface.

## Interactive demo

Open `design_files/index.html` in a browser. You'll be redirected to the dashboard (`sp4-dashboard.html`). From there, click any sidebar item, hub card, CTA, or row to navigate.

The demo runs without a build step — file:// works.

## Wiring details

Navigation is wired via the `scripts/mockup-demo/` toolchain. See:
- [Design](../docs/superpowers/specs/2026-05-20-mockup-demo-navigation-design.md)
- [Implementation plan](../docs/superpowers/plans/2026-05-20-mockup-demo-navigation.md)
- [Nav map](../docs/superpowers/specs/nav-map.md) — full mapping per mockup
- [Validation report](../docs/superpowers/specs/nav-validation-report.html) — reachability

To regenerate the nav map after editing rules:

```bash
python -m scripts.mockup_demo.build_map
# Review docs/superpowers/specs/nav-map.md
python -m scripts.mockup_demo.apply_map
python -m scripts.mockup_demo.validate
```

Patches are idempotent (guarded by `/* DEMO-NAV */` marker).

## Cluster overview

| Cluster | Prefix | Count |
|---|---|---|
| Design system | `00-`…`05-` | 6 |
| Public/marketing | `sp3-*` + 5 other | 13 |
| App authenticated | `sp4-*` (non-hub) | 22 |
| Hub composti | `sp4-hub-*` | 3 |
| Gameplay librogame | `librogame-*` + `sp6-*` | 17 |
| Game night | `sp7-*` | 5 |
| Nav primitives | `nanolith-nav-*` | 3 |
```

- [ ] **Step 3: Commit**

```bash
git add admin-mockups/README.md
git commit -m "docs(mockup-demo): README with demo instructions"
```

---

## Final acceptance check

Before declaring done, verify every spec acceptance criterion (spec §5):

- [ ] AC1: `index.html` redirects to `sp4-dashboard.html` (Task 12, Task 15.2).
- [ ] AC2: Every sidebar/topbar voice navigates correctly (Task 13 patching, Task 15.3 spot-check paths 1, 6, 7, 8).
- [ ] AC3: Every `*-index.html` has at least one clickable row → detail (Task 7 rule + Task 13 application).
- [ ] AC4: Gameplay loop end-to-end navigable (Task 15.3 paths 2–5).
- [ ] AC5: Auth flow end-to-end navigable (Task 15.3 path 10).
- [ ] AC6: ≥95% user-facing reachability (Task 14 validate exit code 0).
- [ ] AC7: 10 paths no console errors (Task 15.4).

Once all checkboxes are ticked, the demo is shippable. Phase 2 (porting to Next.js app) is a separate plan.
