# V2 Audit — Nav Route-Tree Scan Refinement Design

**Date:** 2026-05-21
**Status:** draft (pending user review)
**Scope:** Extend `scripts/v2_audit/nav_dimension.py` to include Link hrefs from route-level files (`apps/web/src/app/<route>/page.tsx`, `layout.tsx`, `_components/`) in the "covered hrefs" set. Eliminates the majority of false-positive nav findings documented as SKIP_FP across fix-wave PRs #1377/#1381/#1384/#1396/#1397.

**Predecessor:** Fix-wave sub-project (PR #1398 final snapshot) — exposed the limitation that nav fixes added to page/layout files were invisible to the audit, generating cascading duplicate audit issues.

---

## 1. Goal

Make `audit_nav` recognize nav links present anywhere in a route's filesystem tree, not only in components listed in `v2-migration-matrix.md` for that route. Drop `main_report_count` from 49 to ≤20.

## 2. Non-goals

- ❌ Parent-h1-aware structural fix (separate spec).
- ❌ Mockup→route mapping refinement (separate spec).
- ❌ Re-opening any closed audit-finding issues.
- ❌ Changes to `create_issues.py` or `report_builder.py`.
- ❌ New unit test infrastructure or fixtures beyond what's needed for the 2 new tests.

## 3. Architecture

### 3.1 Route → filesystem path mapping

New helper `_route_to_filesystem_path(route: str) -> Path | None` in `nav_dimension.py`:

```python
_ROUTE_GROUPS = ["(authenticated)", "(public)"]

def _route_to_filesystem_path(route: str) -> Path | None:
    repo_root = Path(__file__).resolve().parents[2]
    app_dir = repo_root / "apps" / "web" / "src" / "app"
    rel = route.lstrip("/")
    for group in _ROUTE_GROUPS:
        candidate = app_dir / group / rel
        if candidate.exists() and candidate.is_dir():
            return candidate
    direct = app_dir / rel
    if direct.exists() and direct.is_dir():
        return direct
    return None
```

Handles Next.js route groups (`(authenticated)`, `(public)`) by trying each prefix; falls back to root-level routes.

### 3.2 Route tree href aggregation

New helper `_collect_route_tree_hrefs(route: str) -> set[str]` with `functools.lru_cache`:

```python
@functools.lru_cache(maxsize=None)
def _collect_route_tree_hrefs(route: str) -> set[str]:
    path = _route_to_filesystem_path(route)
    if path is None:
        return frozenset()
    hrefs = set()
    for tsx in path.glob("**/*.tsx"):
        if ".test.tsx" in tsx.name or ".stories.tsx" in tsx.name:
            continue
        snap = inspect_component(tsx)
        hrefs |= snap.link_hrefs
        hrefs |= snap.router_calls
    return frozenset(hrefs)
```

Returns `frozenset` so the cached value is immutable. Test files (`.test.tsx`, `.stories.tsx`) excluded.

### 3.3 Integration into `audit_nav`

One-line change in `audit_nav()` to union route-tree hrefs into `comp_hrefs`:

```python
def audit_nav(comp, mock, route):
    comp_hrefs = comp.link_hrefs | comp.router_calls | _collect_route_tree_hrefs(route)
    # ...rest of function unchanged
```

### 3.4 Backward compatibility

- Existing `audit_nav` signature unchanged.
- All 6 current `test_nav_dimension.py` tests pass (synthetic snapshots without route filesystem context — `_collect_route_tree_hrefs` returns empty set for non-existent route).
- Real routes that don't exist as folders → empty set → behaves as before.
- Cache cleared automatically on Python interpreter restart; tests can call `_collect_route_tree_hrefs.cache_clear()` if needed.

## 4. Testing

### 4.1 New unit tests (2)

Add to `scripts/v2_audit/tests/test_nav_dimension.py`:

```python
def test_route_tree_scan_picks_up_layout_links(tmp_path, monkeypatch):
    """Layout file in route tree contributes hrefs to audit_nav coverage."""
    # Build fake apps/web/src/app/(authenticated)/test-route/ structure
    fake_repo = tmp_path / "repo"
    route_dir = fake_repo / "apps" / "web" / "src" / "app" / "(authenticated)" / "test-route"
    route_dir.mkdir(parents=True)
    (route_dir / "layout.tsx").write_text(
        'import Link from "next/link";\n'
        'export default function Layout() { return <Link href="/back">Back</Link>; }',
        encoding="utf-8",
    )
    # Monkeypatch repo_root resolution
    from scripts.v2_audit import nav_dimension
    monkeypatch.setattr(
        nav_dimension, "_route_to_filesystem_path",
        lambda r: route_dir if r == "/test-route" else None,
    )
    nav_dimension._collect_route_tree_hrefs.cache_clear()
    hrefs = nav_dimension._collect_route_tree_hrefs("/test-route")
    assert "/back" in hrefs


def test_audit_nav_with_route_tree_hrefs_no_finding(tmp_path, monkeypatch):
    """Mockup destination satisfied by route tree → no finding."""
    from scripts.v2_audit import nav_dimension
    from scripts.v2_audit.component_inspector import ComponentSnapshot
    from scripts.v2_audit.mockup_inspector import MockupSnapshot

    monkeypatch.setattr(
        nav_dimension, "_collect_route_tree_hrefs",
        lambda r: frozenset({"/sessions"}),
    )
    comp = ComponentSnapshot(path=Path("X.tsx"), link_hrefs=set())
    mock = MockupSnapshot(
        path=Path("y.html"),
        link_destinations={"sp4-sessions-index.html"},
    )
    findings = list(nav_dimension.audit_nav(comp, mock, route="/sessions/[id]"))
    critical = [f for f in findings if f.severity == "critical"]
    assert len(critical) == 0
```

### 4.2 Existing tests

All 6 current tests in `test_nav_dimension.py` must continue to pass without modification. Their synthetic ComponentSnapshot/MockupSnapshot don't trigger route-tree scanning (real route folders don't exist for fake `/x`, `/games`, `/games/[id]` paths used in tests).

### 4.3 Integration smoke

Manual: `python -m scripts.v2_audit.run` before commit. Capture `main_report_count`. Acceptance requires drop from 49 → ≤20.

## 5. Acceptance criteria

1. ✅ All 38 tests pass (36 existing + 2 new in nav_dimension).
2. ✅ `python -m scripts.v2_audit.run` reports `main_report_count` ≤ 20.
3. ✅ Specifically: nav-dimension findings count drops by ≥10 vs current baseline.
4. ✅ No regression: a route folder that genuinely lacks a Link still emits Critical finding (verifiable via synthetic test with empty tree).
5. ✅ `audit-report.md` regenerated and committed.
6. ✅ Spec + plan + code + tests + report on a single PR to `main-dev`.

## 6. Risks

| Risk | Mitigation |
|---|---|
| `lru_cache` retains state across tests | `cache_clear()` callable in test setup; tests use monkeypatching, not real filesystem |
| Routes in non-standard locations (not in `(authenticated)`/`(public)`) | Fallback to direct app-root path; if still not found, returns empty set (no false negative) |
| Performance: 70 components × tree scan | `lru_cache` deduplicates per-route scans; ~20 unique routes total. <1s overhead |
| False negative: link exists in tree but mockup says different specific route | Mockup→route mapping logic unchanged; new code only widens "covered" set, never narrows |

## 7. Out of scope

- Audit cascade: tool refinement reduces FPs at source; closed duplicate issues (#1378-#1387) stay closed.
- Other audit dimensions (structural, tokens, props) unchanged.
- No new toolchain abstractions or refactors.

## 8. Deliverables

| Deliverable | Path |
|---|---|
| Spec | `docs/superpowers/specs/2026-05-21-audit-nav-route-scan-design.md` |
| Plan | `docs/superpowers/plans/2026-05-21-audit-nav-route-scan.md` |
| Code | `scripts/v2_audit/nav_dimension.py` (+~30 LoC) |
| Tests | `scripts/v2_audit/tests/test_nav_dimension.py` (+2 tests) |
| Regen | `docs/superpowers/specs/audit-report.md` |
| PR | 1 PR to `main-dev` |

## 9. Estimate

- Implementation: ~45min
- Tests: ~30min
- Smoke + validation: ~15min
- PR + CI: ~30min

**Total: ~2h elapsed.**
