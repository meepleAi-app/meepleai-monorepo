# V2 Component Audit — Design

**Date:** 2026-05-20
**Status:** draft (pending user review)
**Scope:** Sub-project A of Phase 2 — static audit of the 108 "done" components listed in `docs/for-developers/frontend/v2-migration-matrix.md` against their source mockups in `admin-mockups/design_files/`. Output: report + GitHub issues. No fixes.

**Predecessors:**
- Phase 1 (`feature/mockup-demo-navigation`, PR #1356) — produced `docs/superpowers/specs/nav-map.md` (533 navigation destinations) and renamed mockups to `librogame-*`.
- Existing v2-migration matrix initiative (108 done / 48 pending / 8 stub) — this audit informs but does not duplicate.

---

## 1. Goal

Identify discrepancies between each "done" component implementation (TSX/Tailwind) and its source mockup, across four dimensions: navigation, structural completeness, design tokens, props API contract. Produce a markdown report and create one GitHub issue per route containing findings of severity Critical or Important.

## 2. Non-goals

- ❌ Fixing discrepancies. Identification only; fixes are separate PRs that close issues.
- ❌ Auditing the 48 "pending" or 8 "stub" components — they have no production code to audit.
- ❌ Visual rendering / browser-based diff (rejected: no Storybook coverage on the 186 feature components and the visual-regression suite was removed 2026-05-20 per CLAUDE.md).
- ❌ Functional tests (does the Link actually navigate at runtime?). Static checks only.
- ❌ Performance, bundle size, a11y, backend, data model.

## 3. Architecture

### 3.1 Static + targeted manual review

Automated static analysis covers all four dimensions. Findings receive a confidence band (HIGH / MEDIUM / LOW). LOW confidence findings go to a dedicated "Manual Review Queue" section of the report — they are not auto-promoted to issues. The user reviews the queue and promotes (or demotes) entries before issue creation.

No browser rendering. No Storybook setup. No new app runtime dependencies.

### 3.2 Module layout

```
scripts/v2_audit/
├── __init__.py
├── matrix_parser.py        # v2-migration-matrix.md → rows
├── component_inspector.py  # TSX file → Link hrefs, classNames, JSX landmarks, props interface
├── mockup_inspector.py     # HTML/JSX mockup → same shape via reuse of Phase 1 clickable_extractor
├── nav_dimension.py        # compare Link hrefs vs nav-map.md
├── structural_dimension.py # compare semantic landmarks + heading hierarchy
├── token_dimension.py      # detect hardcoded color utilities + file-level eslint-disable
├── props_dimension.py      # TS interface vs mockup data.js usage
├── confidence_scorer.py    # severity (Critical/Important/Minor) + confidence (HIGH/MEDIUM/LOW)
├── report_builder.py       # → docs/superpowers/specs/audit-report.md
├── issue_creator.py        # → GitHub issues via `gh` CLI
├── run.py                  # CLI orchestrator
├── create_issues.py        # CLI for issue creation (separate explicit step)
└── tests/                  # ~20 tests with fixture pairs
```

Each `*_dimension.py` module exposes the same interface:

```python
def audit(component_path: Path, mockup_path: Path, context: dict) -> list[Finding]
```

`Finding`:

```python
@dataclass
class Finding:
    dimension: str           # "nav" | "structural" | "tokens" | "props"
    severity: str            # "critical" | "important" | "minor"
    confidence: str          # "high" | "medium" | "low"
    component: str           # filename
    route: str               # /games/[id]
    description: str         # human-readable
    evidence: dict           # {expected, actual, line}
```

### 3.3 TSX parsing strategy

Regex-based extraction targeting known patterns. No tree-sitter / no Babel parser dependency.

- `<Link href="..." ...>` and `router.push("...")` → destination
- `className="..."` → token list (Tailwind utilities)
- `interface XxxProps {` → field name + type (best-effort, multi-line aware)

Complex patterns (computed hrefs, spread props, dynamic className) are detected by regex non-match and marked LOW confidence. Coverage target: 85% of cases parse cleanly; remainder enters Manual Review Queue.

### 3.4 The four dimensions

#### Dimension 1: Navigation (HIGH priority)

**Source of truth:** `docs/superpowers/specs/nav-map.md` from Phase 1, filtered to the mockup that the component implements (via matrix row).

**Findings:**
- Critical: mockup has a destination, component has no corresponding Link or points elsewhere.
- Important: mockup has N links, component has M < N.
- Minor: link ordering / decorative differences.

#### Dimension 2: Structural completeness (MEDIUM priority)

**Comparison:** semantic landmarks (`<header>` `<nav>` `<main>` `<section>` `<article>` `<aside>` `<footer>`) and heading hierarchy (`<h1>`–`<h3>`) extracted from both component and mockup.

**Findings:**
- Critical: mockup has a hero `<section>` with `<h1>`, component lacks the hero entirely.
- Important: card-count mismatch (mockup 4 cards, component 3).
- Minor: `<aside>` collapsed into `<div>`.

**Confidence:** noisy dimension. Component may legitimately be a simpler variant. When >30% of mockup elements have no component counterpart, mark all findings LOW confidence and route to Manual Review Queue.

#### Dimension 3: Design tokens (HIGH priority)

**Forbidden patterns:** hardcoded Tailwind color utilities (`bg-white`, `bg-slate-*`, `text-gray-*`, `border-zinc-*`, etc.). The ESLint rule `local/no-hardcoded-color-utility` already enforces this (DS-15, mode: error). This audit serves as:
- Regression detector for future changes.
- Verifier for the ~6-7 files with file-level `eslint-disable` still present (per CLAUDE.md DS-15 deferred decisions).

**Findings:**
- Critical: file-level `/* eslint-disable local/no-hardcoded-color-utility */` directive present.
- Important: single violation that the ESLint rule should catch (verify config wiring).
- Minor: `text-white` / `border-white` without paired colored `bg-*` (CLAUDE.md exemption is fragile).

#### Dimension 4: Props API contract (MEDIUM priority)

**Comparison:** TypeScript `interface XxxProps` field names + types vs data shapes used in mockup (extracted from `data.js` references and JSX inline data).

**Findings:**
- Critical: mockup displays `player.avatar + player.name + player.stats.wins`; component prop accepts only `name`. Data loss.
- Important: mockup constrains rating 0-10; component accepts unbounded number.
- Minor: prop name differences with preserved semantics.

**Confidence:** most heuristic dimension. Primary purpose is input for manual review, not automated issue creation.

## 4. Execution

### Phase A — Discovery & matrix parsing (~3h)

1. Parse `v2-migration-matrix.md` → 108 done rows.
2. Validate file paths exist. Missing files → finding `MATRIX_DRIFT` (matrix says done, file missing) in dedicated report section.
3. Build `route → [components]` map for grouping.

**Output:** `docs/superpowers/specs/audit-input-validated.json` (input snapshot for reproducibility).

### Phase B — Dimension audits (~5h tool + run)

TDD implementation of 4 dimension modules + confidence scorer + report builder. Full run:

```bash
python -m scripts.v2_audit.run
```

**Output:** `docs/superpowers/specs/audit-report.md`.

### Phase C — USER REVIEW GATE 1: report review (async, ~1h user time)

User reviews the report:
- Mark obvious false positives as `confirmed-false-positive` inline.
- Adjust severity where needed.
- Approve when satisfied.

No issue created in this phase.

### Phase D — Manual queue review (parallel to C, ~1h)

User scans the `## Manual Review Queue` section (LOW confidence findings):
- Promote → Critical/Important (move into main report section).
- Demote → noise (drop).

### Phase E — Issue creation (~30min)

```bash
python -m scripts.v2_audit.create_issues --dry-run   # preview
python -m scripts.v2_audit.create_issues             # create for real
```

**Idempotence:** check existing open issues by title `[audit] Route /xxx: N findings`. If exists, update body (checklist) instead of duplicating.

**Issue format example:**

```markdown
Title: [audit] Route /games/[id]: 4 findings

## Findings

### Critical (1)
- [ ] `GameDetailHero.tsx`: missing <Link> to librogame-runthrough-game-onboarding.html
  - Expected (from nav-map): button "Avvia libro game" → game-onboarding
  - Actual: button has onClick={() => alert('TBD')}
  - File: apps/web/src/components/features/game-detail/GameDetailHero.tsx:42

### Important (3)
- [ ] `GameDetailTabsAnimated.tsx`: 2 missing tab destinations (chat, citation)
- [ ] `GameDetailHero.tsx`: file-level eslint-disable on hardcoded colors
- [ ] `GameDetailHero.tsx`: prop rating accepts unbounded number (mockup constrains 0-10)

## References
- Mockup: admin-mockups/design_files/sp4-game-detail.html
- Matrix row: v2-migration-matrix.md L142
- Nav map: docs/superpowers/specs/nav-map.md

🤖 Generated by `scripts/v2_audit/create_issues.py`
```

Labels: `audit-finding`, `frontend`, `v2-migration`.

### Phase F — Matrix update (~30min)

Add `audit_pr` column to `v2-migration-matrix.md`. Initially empty; PRs that close audit issues will fill it.

## 5. Acceptance criteria

1. ✅ All 108 done rows processed (no skip unless `MATRIX_DRIFT`).
2. ✅ `docs/superpowers/specs/audit-report.md` exists with per-dimension, per-route, manual queue, and summary sections.
3. ✅ User Review Gate completed with explicit approval.
4. ✅ GitHub issues created (~10-25, depending on findings).
5. ✅ `v2-migration-matrix.md` updated with `audit_pr` column.
6. ✅ Tool test suite: ≥20 PASS.

## 6. Quality bars

| Dimension | Precision target | Recall target |
|---|---|---|
| Navigation | ≥95% | ≥90% |
| Tokens | ≥99% | ≥99% |
| Structural | ≥80% | ≥75% |
| Props | ≥70% | ≥70% |

Measurement: post-Phase C, count `confirmed-false-positive` markings vs total findings per dimension.

## 7. Testing

**Unit (TDD):** each `*_dimension.py` has 3-5 fixture-based tests = ~20 tests total.

**Integration:** one test runs the full pipeline on 3 real components (`GameDetailHero`, `GamesHero`, `MeepleCard`) and asserts the report contains pre-curated ground-truth findings.

**Manual smoke check** in Phase E: review 5 random issue drafts before non-dry-run.

## 8. Out of scope

- Fixing findings.
- Auditing pending (48) or stub (8) components.
- Visual diff.
- Functional tests (does the Link navigate?).
- Performance / bundle size / a11y (tracked elsewhere: #1094, etc.).
- Backend / API / data model.

## 9. Deliverables

| Deliverable | Path |
|---|---|
| Spec | `docs/superpowers/specs/2026-05-20-v2-audit-design.md` |
| Plan | `docs/superpowers/plans/2026-05-20-v2-audit.md` |
| Toolchain | `scripts/v2_audit/` (~12 files) |
| Tests | `scripts/v2_audit/tests/` (~20 tests) |
| Report | `docs/superpowers/specs/audit-report.md` |
| Input snapshot | `docs/superpowers/specs/audit-input-validated.json` |
| Issues | GitHub label `audit-finding` (~10-25 issues) |
| Matrix update | `docs/for-developers/frontend/v2-migration-matrix.md` (+ `audit_pr` column) |

## 10. Risks

| Risk | Mitigation |
|---|---|
| TSX regex parser misses complex patterns | LOW confidence marker + Manual Review Queue |
| Too many false positives | Phase C user gate before issue creation |
| GitHub issue spam | Hard cap: 1 issue per route, body grows |
| Mockup post-Phase 1 rename mismatch | Use Phase 1 nav-map.md (librogame-* names) as canonical |
| Matrix drift (done but file missing) | `MATRIX_DRIFT` finding in dedicated section |
| Component is correct, mockup is outdated | False positives expected; Phase C gate handles them |

## 11. Estimate

- Phase A: ~3h
- Phase B: ~5h tool work
- Phase C+D: ~1-2h user async
- Phase E: ~30min
- Phase F: ~30min

**Total: ~9-10h tool work + user review window. 2-3 sessions.**

## 12. Follow-up (out of scope for this spec)

After issues are created, fix waves: PRs that close audit issues. Likely waved by route (1 wave = 1 PR per route). Re-audit after each wave for regression. Separate spec/plan.
