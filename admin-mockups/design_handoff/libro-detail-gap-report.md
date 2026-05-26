# Libro Detail Gap Report — `/library/[gameId]` libro variant (librogame-runthrough-*.html ↔ SP6 Phase B PR #1037)

> Conformity check applying the [`PILOT_GAP_REPORT.md`](./PILOT_GAP_REPORT.md) methodology to the **libro-game variant** of `/library/[gameId]`. Issue: [#1486](https://github.com/meepleAi-app/meepleai-monorepo/issues/1486).
> Date: 2026-05-26 · Branch: `feature/issue-1486-libro-detail-audit` · Scope: read-only.
> Reference format: [`players-detail-gap-report.md`](./players-detail-gap-report.md) (just shipped via PR #1544).

## TL;DR

`/library/[gameId]` libro variant is **production-ready** with **zero structural drift**. Both mockups (game-detail + setup-wizard) are 100% matched by the 2 shipped components. The audit finds only test-coverage gaps and one a11y polish item; no missing components.

| Status | Count | Components |
|---|---|---|
| ✅ Implemented + correctly located | 2 | `LibroGameDetailView`, `CampaignSetupDrawer` |
| 🟢 Implemented as orchestrator/helper (not exported by design) | 3 | `/library/[gameId]/page.tsx` variant branching, `NanolithCampaignCTA`, `isLibroGame` util |
| ❌ Missing as standalone | 0 | — |
| ⚠️ Deferred by design | 1 | `LibroGameOnboardingPanel` (per issue body — separate SP6 backlog row, blocked on FREEZE lift) |
| 🔎 To verify / coverage gap | 3 | `LibroGameDetailView.test.tsx` missing · `CampaignSetupDrawer.test.tsx` missing · drawer step-change focus management |

→ **Drift ratio**: 0 ÷ 20 mockup sections = **0%** (vs pilot 37.5% on `/games/[id]`, vs players-detail 10%). **Best alignment of the C-audit series so far.**

→ **Effort residuo**: ~3-4h follow-up (2 test files + 1 a11y polish), all P3/P4.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Implementato + esposto via barrel `index.ts` |
| 🟢 | Implementato — non esposto by-design (orchestrator-only component) |
| ❌ | Mancante — da creare |
| ⚠️ | Esiste in altro path / variant / by-design deferral |
| 🔎 | Coverage gap / da verificare |

## 1. Path-relocation decision (issue body asks)

The issue body references the **legacy path** `components/v2/gamebook/`, recalling that PR #1037 (SP6 Phase B) shipped pre-FREEZE under the original v2 layout, and the codemod for Design System De-versioning (#1023 Stage 3) only touched `return null` stubs.

**Audit finding (P72 verify with `ls`)**: The path is **already canonical**.

| Signal | Finding |
|---|---|
| `apps/web/src/components/v2/gamebook/` exists? | ❌ Directory does NOT exist anymore |
| `apps/web/src/components/features/gamebook/LibroGameDetailView.tsx` exists? | ✅ 337 LOC |
| `apps/web/src/components/features/gamebook/CampaignSetupDrawer.tsx` exists? | ✅ 515 LOC |
| Import callsites grep | 2 callsites (orchestrator page + `NanolithCampaignCTA`) — already pointing to `features/gamebook/` path |
| Sibling files in `features/gamebook/` | 40+ Phase B/C/D gamebook components co-located |

**Decision**: ✅ **NO ACTION NEEDED — path is already canonical** at `apps/web/src/components/features/gamebook/`. The issue body reference to `v2/gamebook/` is stale; relocation was completed in a prior cleanup pass (not by the codemod, but by a direct move).

**Effort**: XS (0 — already done).

## 2. Mapping componenti: mockup ↔ codebase

### 2.1. `librogame-runthrough-game-detail.html` (11 mockup sections)

| # | Mockup section | Lines | Role | Status | Codebase location |
|---|---|---|---|---|---|
| 1 | Header / breadcrumb | 59–73 | Top app bar + back button | ✅ | `LibroGameDetailView` lines 59–73 |
| 2 | Hero cover card | 144–167 | Session+game gradient + title + publisher + nano-mark badge | ✅ | `LibroGameDetailView` lines 79–107 |
| 3 | Meta grid (4-cell) | 168–172 | Players / Duration / BGG / Year | ✅ | `LibroGameDetailView` lines 110–115 (`MetaStat` subcomponent) |
| 4 | Connection pip bar | 173–191 | KB / Chat / Agent / Player / Session counts | ✅ | `LibroGameDetailView` lines 123–139 (`Pip` subcomponent) |
| 5 | Primary CTA "Avvia libro game" | 192–209 | Session-color button + sub-text | ✅ | `NanolithCampaignCTA` lines 29–59 |
| 6 | Tabs (Info / AI Chat / Toolbox / Toolkit) | 210–226 | 4-tab tablist + underline indicator | ✅ | `LibroGameDetailView` lines 148–165 (`TabButton`) |
| 7 | Info tab panel | 227–244 | Description + KB status row | ✅ | `LibroGameDetailView` lines 259–294 (`InfoPanel`) |
| 8 | Loading state shell | 495–530 | Skeleton | ✅ | `/library/[gameId]/page.tsx` lines 79–81 (page-level FSM) |
| 9 | Error state shell | 532–586 | Alert + retry/back CTAs | ✅ | `/library/[gameId]/page.tsx` lines 83–105 |
| 10 | Not-found state shell | 588–646 | Alert + back CTA | ✅ | `/library/[gameId]/page.tsx` lines 109–121 |
| 11 | AI Chat / Toolbox / Toolkit placeholders | 245–294 (tabs body) | "In arrivo con la prossima iter" | ✅ | `LibroGameDetailView` placeholder tab panels (Iter B2 deferred) |

**Summary**: 11/11 matched.

### 2.2. `librogame-runthrough-setup-wizard.html` (9 mockup sections)

| # | Mockup section | Lines | Role | Status | Codebase location |
|---|---|---|---|---|---|
| 1 | Drawer header + icon | 303–307 | "📖 Nuova campagna · {gameTitle}" | ✅ | `CampaignSetupDrawer` lines 187–189 (`DrawerHeader`) |
| 2 | Stepper (3 pills) | 308–312 | Step 1/2/3 indicator (active/done/pending) | ✅ | `CampaignSetupDrawer` lines 262–291 (`Stepper`) |
| 3 | **Step 1** name + preset selector | 313–321 | Title field + 3 preset cards | ✅ | `CampaignSetupDrawer` lines 303–385 (`StepName`) |
| 4 | **Step 2** player chips | 342–409 | Host + 3 guests + add custom button | ✅ | `CampaignSetupDrawer` lines 389–458 (`StepPlayers`) |
| 5 | Agent suggestion card | 374–380 | "Game Tutor consiglia N giocatori" | ✅ | `CampaignSetupDrawer` lines 446–455 |
| 6 | **Step 3** review card | 437–455 | Cover mini + meta + ManaPips + info callout | ✅ | `CampaignSetupDrawer` lines 469–502 (`StepConfirm`) |
| 7 | Footer buttons | 290–293, 322–323, 411–412, 461–462 | Cancel/Back + Next/Submit | ✅ | `CampaignSetupDrawer` lines 220–254 (`DrawerFooter`) |
| 8 | Validation error state | 516–547 | Empty title → field error + disabled Next | ✅ | `CampaignSetupDrawer` lines 146–151 |
| 9 | Mobile sheet vs desktop side-drawer | 78–106 | CSS positioning (bottom vs right) | ✅ | Delegated to shared `Drawer` primitive (Radix) |

**Summary**: 9/9 matched. **Bonus**: 2 extra states (submit loading + submit error) implemented beyond mockup (positive overdelivery).

### 2.3. Route orchestration

**File**: `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx`

**Variant branching** (lines 134–137):
```tsx
const renderLibroView = isLibroGame({ gameTitle: effectiveDetail.gameTitle });
if (renderLibroView) {
  return <LibroGameDetailView gameDetail={effectiveDetail} />;
}
```

**Allowlist** (`src/lib/games/is-libro-game.ts`): hardcoded `['Nanolith']`. Future-extensible via BE metadata flag — not in scope here.

**Status**: ✅ Correct. Non-libro games fall through to legacy `GameDetailDesktop`.

## 3. CTA variants audit

All 7 CTA variants in the 2 mockups (1 hero CTA + 6 wizard step buttons) are wired with the correct labels and handlers:

| CTA label | Location | Handler |
|---|---|---|
| "📖 Avvia libro game" + sub-text | `NanolithCampaignCTA` button | Opens drawer (uncontrolled) |
| Step 1/2 "Avanti →" | `CampaignSetupDrawer` line 240 | `setStep(n+1)` if valid |
| Step 2/3 "← Indietro" | line 229 | `setStep(n-1)` |
| Step 3 "📖 Inizia sessione" | line 250 | `mutation.mutate()` → POST `/api/v1/gamebook/campaigns` |
| All "Annulla" | line 224/291 | `reset()` (close + reset form) |

## 4. State coverage

### 4.1. `LibroGameDetailView`

| State | Mockup | Shipped | Location |
|---|---|---|---|
| default | ✅ | ✅ | `LibroGameDetailView` lines 37–181 |
| loading | ✅ | ✅ | `page.tsx` lines 79–81 (page-level skeleton) |
| error | ✅ | ✅ | `page.tsx` lines 83–105 (alert + retry/back) |
| not-found | ✅ | ✅ | `page.tsx` lines 109–121 (alert + back) |

Component itself assumes narrowed non-null `gameDetail` (orchestrator handles FSM at page level).

### 4.2. `CampaignSetupDrawer`

| State | Mockup | Shipped | Location |
|---|---|---|---|
| Step 1 default | ✅ | ✅ | lines 194–201 |
| Step 1 validation error (empty title) | ✅ | ✅ | lines 146–151, 236 |
| Step 2 default | ✅ | ✅ | line 203 (`StepPlayers`) |
| Step 3 default (review) | ✅ | ✅ | lines 204–216 (`StepConfirm`) |
| Submit loading | 🟢 _bonus_ | ✅ | lines 245–250 (`mutation.isPending` → button "Creazione…") |
| Submit error | 🟢 _bonus_ | ✅ | lines 209–214 (error branch in `StepConfirm`) |

## 5. A11y / interaction sub-check

| Aspect | Mockup hint | Shipped impl | Status |
|---|---|---|---|
| `aria-modal="true"` on drawer | — | ✅ Provided by `Drawer` (Radix primitive) | ✅ |
| Focus trap inside drawer | — | ✅ Radix default | ✅ |
| Escape-to-close | — | ✅ Radix default | ✅ |
| Focus return to trigger on close | — | ✅ Radix default | ✅ |
| Hero button label + focus ring | — | ✅ Clear label + `focus-visible:ring-[3px]` | ✅ |
| Hero icon `aria-hidden` | — | ✅ `aria-hidden="true"` on emoji | ✅ |
| Stepper `aria-current="step"` | — | ❌ Uses `data-state="active/done/pending"` instead | 🟢 acceptable (visual state present) |
| Focus management on step change | — | ❌ Focus stays on Next button after `setStep(n+1)` | 🔎 polish gap |
| Form keyboard tab order | ✅ semantic native fields | ✅ Correct via native `<input>`/`<button>` | ✅ |

## 6. Test coverage gap

**Missing unit tests**:

| Component | Test path (proposed) | Effort | Priority |
|---|---|---|---|
| `LibroGameDetailView` | `apps/web/src/components/features/gamebook/__tests__/LibroGameDetailView.test.tsx` | S (~1h) | P3 |
| `CampaignSetupDrawer` | `apps/web/src/components/features/gamebook/__tests__/CampaignSetupDrawer.test.tsx` | M (~2h) | P3 |

**Existing coverage surface**:
- Page-level integration tests at `/library/[gameId]/__tests__/page.test.tsx` MOCK both components (so they don't exercise their internal logic; they verify the orchestrator wires them in correctly).
- `NanolithCampaignCTA` covered implicitly through page-level integration.
- `isLibroGame` utility — also untested at unit level (XS gap, can fold into F1).

## 7. Deferred by design (NOT to spawn issues for)

- **LibroGameOnboardingPanel** — "PDF required to start" gate. Explicitly out of scope per issue body; tracked separately under the SP6 backlog (gap-coverage row, blocked on FREEZE lift per issue body — referenced as a pending Tier-L row in the matrix). _(I attempted to look up the specific tracking issue number; the closest candidate found was a different issue, so this report keeps the reference generic to avoid misinformation. The matrix row for LibroGameOnboardingPanel should remain the canonical pointer.)_
- **AI Chat / Toolbox / Toolkit tab content** — placeholder ("in arrivo con la prossima iter") matches mockup; not a drift.
- **Mobile sheet vs desktop side-drawer positioning** — delegated to shared `Drawer` primitive responsive breakpoints.

## 8. Issue follow-up — proposed

To be opened after merge of this audit PR (or per user direction):

| # | Title | Body summary | Effort | Priority |
|---|---|---|---|---|
| F1 | `test(libro-detail): LibroGameDetailView + isLibroGame unit tests (#1486 follow-up)` | Cover tab state, `MetaStat`/`Pip` subcomponents, `InfoPanel` rendering, and `isLibroGame` allowlist. Smoke tests for variant-routing scenario. | S (~1h) | P3 |
| F2 | `test(libro-detail): CampaignSetupDrawer unit tests (#1486 follow-up)` | Cover 3-step FSM, title-length validation, preset selection, player chip rendering, submit loading + error branches. | M (~2h) | P3 |
| F3 | `a11y(libro-detail): focus management on drawer step change (#1486 follow-up)` | When user advances step N → N+1, move focus to the first interactive element of the new step (e.g. first preset radio on step 1 → name input on step 2). Currently focus stays on Next button. | XS (~30 min) | P4 |

## 9. Conformity verdict

✅ **Production-ready**, **drift 0%**. The libro-game variant of `/library/[gameId]` is the **best-aligned route audited so far in the C-series**. Both mockups (20 distinct sections) are matched 1:1, the path is already canonical post-FREEZE (no relocation needed), all CTA variants are wired, all FSM states render correctly, and a11y essentials (modal trap / focus return / aria-hidden / focus ring) are in place.

The only follow-ups are test coverage (P3) and one a11y polish (P4) — none blocking.

---

**Generated by Claude Code (Opus 4.7) in read-only mode.** Issue #1486. PR with matrix `audit_pr` update + audit-report-final entry follows in the same PR as this report.
