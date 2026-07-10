# TODO/FIXME Triage — Q3 2026 gate

> Quarterly cadence per umbrella **#564**. Gate date: 2026-07-01 (run 2026-07-09).
> Previous run: 2026-05-22 interim (386 total). Baseline: 2026-04-26 (452 total).

## Snapshot

**Command**: `rg "TODO|FIXME" apps/ infra/ docs/` (excl. `node_modules`, `.next`, `dist`)

**Total: 470 markers in 203 files** — under the 500 escalation threshold (§ umbrella "escalate if > 500").

| Area | 2026-04-26 | 2026-05-22 | **2026-07-09** | Δ vs prev |
|------|-----------:|-----------:|---------------:|:---------:|
| **Total** | 452 | 386 | **470** | **+84** |
| apps/web/e2e (aspirational stubs) | ~117 | 130 | 114 | −16 |
| apps/web/src (frontend) | — | 151 | 142 | −9 |
| apps/api/src (backend) | — | 5 | 5 | 0 |
| apps/api/tests | — | 0 | 0 | 0 |
| apps/orchestration-service | — | 1 | 1 | 0 |
| infra/ | — | 6 | 6 | 0 |
| docs/superpowers/specs (gap-analysis) | ~100 | 13 | 19 | +6 |
| docs/ (non-specs) | ~50 | ~50 | **160** | **+110** |

## Where the +84 came from

Almost entirely **`docs/` non-specs (+110)**, offset by code cleanup (−25 across e2e + frontend src). The docs growth is **not code debt** — it is template/checklist content (e.g., the security-review template's `- [ ]` items, ADR/spec placeholders, operations runbook TODOs). These are documentation authoring artifacts, not actionable engineering debt, and are excluded from the "actionable" triage below.

## Categorization of code markers

### Backend src — 5, all tracked ✅
- `GameToolkitRepository.cs:300-301` — architectural breadcrumb, tracked via **#1458** (`VersionSemver` producer).
- `SessionTracking/README.md:231,253,254` — roadmap notes (GST-003 event emission, GST-005 UserLibrary integration).

No untracked backend debt.

### Frontend src — 142, dominated by tracked/aspirational
- **`#807-followup` cluster (~55)** — `session-summary/*` (ConnectionBar 17, SessionKpiGrid 13, Confetti 7, …) + `gamebook/GameSearchCard` (5): all are `eslint-disable meepleai/no-inline-hsl-v2` rationale comments tracking the same DS follow-up (**#807** — entity color CSS vars need alpha-stop / multi-stop-gradient support before these inline HSL styles can be removed). **Tracked, valid-pending.**
- **DS-17 mockup stubs** — `kb-detail/*`, other `features/*`: `TODO: implement per admin-mockups/…` / `extract props contract from mockup`. Tracked under the DS-17 umbrella **#2063**.
- **e2e/spec stubs (114)** — epic-driven roadmap test stubs (e.g., `epic-4068-permission-flows.spec.ts`). Intentional/aspirational, not debt.

### Net-new actionable (this cycle): 1 cluster → issue opened
- **`useEntityActions.ts` (5)** — 5 entity quick-action handlers with placeholder navigation/fallback pending full UX (copy-code without toast, invite via navigation instead of modal, download/export/RSVP via `router.push`). Not broken (working fallback), but the intended UX is stubbed. → **opened #2776**.

## Stale removal (this cycle): none

No clearly-stale removable TODOs found. The `#807-followup` and DS-17 mockup markers are valid-pending (blocked on an upstream CSS-var feature and the DS-17 migration respectively); removing them would drop legitimate tracking. No PR-removal performed this gate.

## Outcome

| DoD item | Status |
|----------|--------|
| Run `rg` + diff vs previous | ✅ (470 vs 386, +84 — mostly docs template content) |
| Net-new actionable → open issues | ✅ 1 issue (**#2776**, `useEntityActions` UX stubs) |
| Stale TODOs → remove with PR | ✅ none this cycle (documented above) |
| Update umbrella with snapshot + audit link | ✅ (this doc + umbrella comment) |

**Escalation**: none (470 < 500). Next gate: **2026-10-01 (Q4)**.
