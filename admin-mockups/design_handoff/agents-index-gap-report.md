# Agents Index Gap Report — `/agents` (sp4-agents-index.jsx ↔ Wave B.2 PR #634/#637)

> Conformity check applying the [`PILOT_GAP_REPORT.md`](./PILOT_GAP_REPORT.md) methodology to `/agents`. Issue: [#1522](https://github.com/meepleAi-app/meepleai-monorepo/issues/1522).
> Date: 2026-05-26 · Branch: `feature/issue-1522-agents-audit` · Scope: read-only.
> Reference format: [`libro-detail-gap-report.md`](./libro-detail-gap-report.md) (0% drift example) + [`players-detail-gap-report.md`](./players-detail-gap-report.md).

## TL;DR

`/agents` is **already a single-view mockup-structured route** (Wave B.2 #634, shipped #637) — the issue body premise ("replace page.tsx with mockup structure") was based on a misreading: the route already renders the `sp4-agents-index` mockup via `AgentsLibraryView` (single-view, no multi-tab). This is the **opposite of #1521** (`/games` was genuinely multi-tab needing consolidation). The conformity check finds **zero structural drift**.

| Status | Count | Components |
|---|---|---|
| ✅ Implemented + exported | 4 | `AgentsHero`, `AgentFilters`, `AgentsResultsGrid`, `EmptyAgents` |
| 🟢 Implemented as orchestrator (single-view FSM) | 1 | `AgentsLibraryView` (5-state FSM) |
| ❌ Missing as standalone | 0 | — |
| ⚠️ Deferred-by-design / distinct surface | 1 | `/hub/agents` community catalog (separate route, `PopularAgent` schema — NOT a duplication of `/agents` which is `AgentDto`/my-agents) |
| 🔎 Test-quality gap | 1 | jest-axe not integrated in agents test files (a11y is manually verified correct; automated coverage is a P4 nicety) |

→ **Drift ratio**: 0 / 17 mockup sections = **0%** (ties libro-detail #1486 for best alignment).

→ **Effort residuo**: ~XS (1 jest-axe follow-up, P4). No bugs, no missing components, no routing change.

## Convenzioni

| Simbolo | Significato |
|---|---|
| ✅ | Implementato + esposto via barrel |
| 🟢 | Implementato (orchestrator / FSM) — non esposto by-design |
| ❌ | Mancante |
| ⚠️ | Surface distinta / deferred-by-design |
| 🔎 | Test-quality gap / da verificare |

## 1. Mapping mockup → codebase (17 sections, all matched)

| Mockup section | Lines | Status | Codebase |
|---|---|---|---|
| `AgentsHero` (eyebrow + h1 + subtitle + 4 stat tiles + create CTA) | 271–317 | ✅ | `features/agents/AgentsHero.tsx` |
| `AgentFilters` (search + status tablist + sort select + count) | 335–414 | ✅ | `features/agents/AgentFilters.tsx` |
| `AgentCardGrid` (MeepleCard entity=agent, auto-fit grid) | 153–265 | ✅ | `features/agents/AgentsResultsGrid.tsx` (wraps `MeepleCard`) |
| `EmptyAgents` (🤖 empty + create CTA) | 420–468 | ✅ | `EmptyAgents.tsx` kind=`empty` |
| `ErrorState` (⚠️ + retry) | 470–500 | ✅ | `EmptyAgents.tsx` kind=`error` (role=alert) |
| `SkeletonCard` (loading 6/3) | 502–536 | ✅ | `EmptyAgents.tsx` kind=`loading` (aria-busy + aria-live) |
| Mobile + desktop state variants | 809–874 | ✅ | `AgentsLibraryView` FSM |
| `ConnectionChipStrip` footer | 92–123 | ✅ | `MeepleCard` reuse (zero-fork) |
| `EntityChip` game link | 126–147 | ✅ | `MeepleCard` subtitle |
| Status meta enums | 82–86 | ✅ | `lib/agents/derive-status.ts` |
| `filterAgents` | 541–554 | ✅ | `lib/agents/library-filters.ts` (matchQuery + filterByStatus) |
| `sortAgents` | 556–561 | ✅ | `lib/agents/library-filters.ts` (sortAgents) |
| `deriveStats` | — | ✅ | `lib/agents/library-filters.ts` (deriveStats) |
| FSM 5-state | — | ✅ | `AgentsLibraryView` lines 176–184 |
| Visual-test state override | — | ✅ | `AgentsLibraryView` (`?state=` param) |
| i18n `pages.agents.*` | — | ✅ | 29 keys, all present en+it |
| Create CTA modal | — | ✅ | `page.tsx` wires `AgentCreationSheet` |

## 2. Prop-contract vs BE schema (`AgentDto`)

`AgentsResultsGrid` consumes `AgentDto[]` (`apps/web/src/lib/api/schemas/agents.schemas.ts`): `id, name, type, strategyName, strategyParameters, isActive, createdAt, lastInvokedAt, invocationCount, isRecentlyUsed, isIdle, gameId, gameName`.

Mockup fixture demo fields (`chatCount`, `kbCount`, `sessionCount`, `model`) are **illustrative only** — the shipped `MeepleCard` contract never promised those slots, so they gracefully omit (no P83 hiding needed). All mapped fields (name→title, gameName→subtitle, type+strategyName→tags, derived status) are present and correct.

## 3. State coverage (FSM)

`AgentsLibraryView` `realKind` derivation (lines 176–184) covers all 5 mockup states: `loading` (isLoading && no fixture) · `error` (isError) · `empty` (agents.length===0) · `filtered-empty` (filtered.length===0 && agents>0) · `default`. ✅ Complete.

## 4. i18n key check (the #1485 C1 bug class)

All **29 keys** referenced by `AgentsLibraryView` (`pages.agents.hero.*`, `pages.agents.filters.*`, `pages.agents.empty.*`) verified present in **both** `en.json` and `it.json` (programmatic check, 29/29). **No raw-key-path bug** like #1485 C1. ✅

## 5. A11y check (the #1480 keyboard-nav gap class)

- `AgentsResultsGrid` cards are `<Link>` elements (native keyboard-navigable) — **NOT** the `<article onClick>` pattern that lacked `onKeyDown` in #1480. ✅ No gap.
- Search input `type="search"` + sr-only label ✅; status `role="tablist"` + `role="tab"` + `aria-selected` + roving tabindex (`useTablistKeyboardNav`) ✅; sort native `<select>` + `<label>` ✅; loading `aria-busy` + `aria-live="polite"` ✅; error `role="alert"` ✅; focus-visible rings throughout ✅.
- 🔎 **jest-axe not integrated** in the 5 agents test files — a11y is manually verified correct but lacks automated regression coverage. Tracked as follow-up (P4).

## 6. `/hub/agents` — distinct surface (NOT a duplication)

| | `/agents` | `/hub/agents` |
|---|---|---|
| Schema | `AgentDto` (my agents) | `PopularAgent` (community) |
| Hook | `useAgents` | `useDiscoverPopularAgents` |
| Surface | personal agent studio | community discovery catalog |

Legitimately distinct (unlike #1521 where `/games?tab=library` duplicated `/library`). No consolidation needed.

## 7. Follow-up — proposed

| # | Title | Body summary | Effort | Priority |
|---|---|---|---|---|
| F1 | `test(agents): add jest-axe coverage to 5 agents-index test files (#1522 follow-up)` | Add a `toHaveNoViolations` assertion to `AgentsHero` / `AgentFilters` / `AgentsResultsGrid` / `EmptyAgents` (+ optionally `AgentsLibraryView` with IntlProvider) so a11y regressions are caught at PR time. A11y is manually verified correct today; this is automated regression coverage. | XS (~30 min) | P4 |

## 8. Conformity verdict

✅ **Production-ready**, **drift 0%**. `/agents` was already a single-view mockup-faithful route (Wave B.2 #634/#637); the issue premise (needs replacement) was a misreading. All 17 mockup sections matched, all 29 i18n keys resolve, all 5 FSM states render, a11y essentials present, 58 unit tests green. `/hub/agents` is a legitimately distinct community surface. The only follow-up is automated jest-axe coverage (P4).

**Comparison — routing-decision trio (#1480/#1521/#1522) outcomes**:

| Issue | Route | Outcome |
|---|---|---|
| #1480 | `/toolkits` | **Implemented** 7 components (hub was at `/hub/toolkits`, `/toolkits` had only `[id]`) |
| #1521 | `/games` | **Redirect** → `/library` (was multi-tab duplicating `/library`) |
| #1522 | `/agents` | **Already done** (single-view mockup via Wave B.2; 0% drift audit) |

Three sibling "routing" issues, three different correct resolutions — each driven by verifying the actual code state (P72/P97) rather than the issue body's assumption.

---

**Generated by Claude Code (Opus 4.7) in read-only mode.** Issue #1522. Matrix `audit_pr` update + audit-report-final entry + 1 follow-up issue in the same PR as this report.
