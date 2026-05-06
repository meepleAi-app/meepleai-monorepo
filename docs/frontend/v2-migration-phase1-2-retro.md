# V2 Migration Phase 1+2 — Mid-Stream Retro

> **Date**: 2026-05-05
> **Trigger**: per PR #741 §10.4 sequencing — Phase 3 retro mid-stream after Wave D.2 Foundation merge
> **Scope**: lessons from Wave A.1-A.6, B.1-B.3, C.1-C.2, Wave 3, Wave 4 D1, Wave D.1, Wave D.2 Foundation
> **Audience**: dev team + future Wave D.2 Interactions implementer + Phase 2 V2 routes

## Executive summary

V2 migration Phase 1 ha shipped **9 routes v2** (Wave A×6 + B×3 + C×2 + Wave 3 ×1 + Wave 4 D1 ×1 + Wave D.1 + D.2 Foundation). Backend prereq blockers identificati per 3 Wave 3 routes (#728/#729/#730), specs concretizzate via PR #732. Subagent-driven development pattern + two-stage review hanno catturato 6+ critical issues per route che single-shot review avrebbe perso.

**Sum velocity**: ~10x speedup vs originale 4-week per Wave D estimate (PR #741 §10.4 actuals).

## Pattern emergesi (validati cross-wave)

### Pattern P1 — `Subagent-driven 5-task TDD dispatch`

**Validation**: ~15 routes shipped consistently con 5-task pattern (foundation → components → orchestrator → e2e → baselines).

**Strengths**:
- Predictable cadence per task (~1-3 ore each)
- Two-stage review (spec compliance + code quality) catches drift
- Fresh subagent context = no pollution from previous tasks
- Single task = single commit = clean git history

**Weaknesses**:
- 4-5× original effort estimate overrun (estimates calibrated pre-velocity-multiplier)
- Subagent coordination cost grows with multi-task waves (D.2 split = double the dispatches)
- Review fatigue on long sequences (D.1 had 11 commits + 3 review rounds)

**Recommendation**: keep pattern. Recalibrate effort estimates × 0.2-0.25 for future Tier S routes.

### Pattern P2 — `Two-stage subagent review (spec + quality)`

**Validation**: caught **6 critical/major issues per route** (Wave D.1 alone). Issues missed by implementer self-review:
- Schema reality v1 carryover (Wave D.1 SessionPlayerDto)
- Public API contract drift (parseStateOverride re-export missing)
- i18n key drift (resultsRegionLabel vs resultsLabel)
- WCAG 4.1.2 violations (no-op CTA buttons)
- ICU plural defensive pattern (raw replace anti-pattern)
- Hardcoded language strings

**ROI**: 30% extra agent time → prevents post-merge fixes (1-2 days each).

**Recommendation**: continue both stages on all critical/major tasks. Skip second stage (code quality) only on minimal mechanical work (matrix updates, version bumps).

### Pattern P3 — `Phase 0.5 contract per Tier L routes`

**Validation**: Wave C.1 retry success after retrofit + Wave C.2 + Wave 3 game-nights + Wave D.2 all benefited from explicit contract drafting BEFORE implementation.

**Trigger threshold**:
- ≥3 hooks composed
- Real-time SSE / streaming
- Cartesian FSM (state × variant × dialog)
- Cross-BC composition

**Output**: `docs/frontend/contracts/<route>-hooks.md` documents:
- Route surface + URL state schema (SSOT)
- Hook dependency graph
- FSM cell matrix
- Component contracts per role/variant
- Test coverage plan
- Bundle budget
- Audit checklist

**Recommendation**: keep mandatory for Tier L. Optional for Tier M with 4+ hooks.

### Pattern P4 — `4 audit gates A-D (post-D.1 amendments)`

Per PR #741 §10.2:

- **Gate A (ICU plural)**: `t(key, { count })` resolved orchestrator-side, plain string to component
- **Gate B (schema reality)**: read DTO source pre-implementation, document v1 carryover gaps
- **Gate C (MeepleCard API-fit)**: decide WRAP vs DIVERGE explicitly per component
- **Gate D (bootstrap-then-merge)**: open PR → trigger bootstrap → wait PNG commits → merge

**Validation**: D.2 Foundation applied all 4 gates pre-implementation, zero post-merge fixes (vs D.1 pre-amendments which had 4 review fixes).

**Recommendation**: apply gates as Phase 0.5 contract checklist + pre-implementation grep audit per route.

### Pattern P5 — `URL state SSOT (no useState mirror)`

**Validation**: Wave 4 D1 → Wave D.1 → Wave D.2 sustained. URL params as single source of truth, no `useState` duplication.

**Pattern**:
```ts
const searchParams = useSearchParams();
const filterValue = parseFilter(searchParams.get('filter'), 'all');
// On change:
router.replace({ ...currentParams, filter: 'completed' }, { scroll: false });
```

**Anti-pattern caught**: D.1 initial impl had `useState<Filter>` synced via `useEffect` from URL → duplicate state, sync drift, deep-link broken.

**Recommendation**: enforce in Phase 0.5 contract §1 URL state schema. Reject useState mirror in code review.

### Pattern P6 — `Schema reality v1 carryover (Wave 4 D1 → D.1 → D.2)`

**Validation**: 3 routes consecutive applied pattern when backend DTO didn't match mockup field expectations.

**Pattern**:
```ts
/**
 * SCHEMA REALITY V1 CARRYOVER (Wave X.Y, mirror PR #717+#736 pattern):
 * <DtoName> exposes only <fields>. Wave X.Y = visual upgrade only;
 * <missing-feature> is decorative (placeholder).
 * Followup issue post-merge: <link or TBD>.
 */
```

**Examples**:
- Wave 4 D1: `usePlayerStatistics` aggregated stats, playerId from URL slug decorative
- Wave D.1: `SessionPlayerDto` missing score/winner fields → score=0 placeholder, ScoringInline graceful degradation
- Wave D.2: SSE event payload shapes documented, real verification deferred to Interactions sub-PR

**Recommendation**: pre-implementation grep audit on DTO source MANDATORY (Gate B). Document gap explicitly. File followup backend issue.

### Pattern P7 — `MeepleCard divergence justified per richness`

**Validation**: Wave 4 D1 (Players) wrap MeepleCard cleanly (title+subtitle only). Wave D.1 (Sessions list/grid) DIVERGE (left-accent-bar + inline scoring + mai-pulse animation). Wave D.2 (Live UI) ALL 7 DIVERGE (real-time UI not card pattern).

**Decision rule**:
- ✅ WRAP: title + subtitle + optional metadata (Players/Games index)
- ❌ DIVERGE: inline composition + wrapper animations + positioned overlays + role-gated affordances

**Cost of divergence**: ~40% more component code (custom layout vs MeepleCard reuse). Acceptable when API doesn't fit.

**Recommendation**: Phase 0.5 contract §5 component contracts must declare WRAP/DIVERGE upfront with justification.

### Pattern P8 — `Single-tree responsive uniformity (Wave B.3 lesson)`

**Validation**: Wave D.1 sustained, Wave D.2 sustained. Single component tree with Tailwind responsive classes (`hidden lg:block`) instead of mobile/desktop split components.

**Anti-pattern caught**: original Wave D.2 mockup suggested separate `MobileBody` vs `DesktopBody`. Initial impl tempted to fork orchestrator. **Solution**: keep orchestrator unified, render both bodies via responsive Tailwind. `MobileBody` and `DesktopBody` are layout SHELLS, NOT separate trees.

**Recommendation**: enforce single-tree in Phase 0.5 contract §4 layout pattern.

### Pattern P9 — `Bootstrap-then-merge discipline (Gate D)`

**Validation**: D.1 PR #736 violated this (--admin merge before bootstrap → workflow ran ~5 min before merge). D.2 Foundation enforces correctly (this PR).

**Sequence**:
1. PR opens (CI runs but baselines absent)
2. Trigger `gh workflow run visual-regression-migrated.yml -f mode=bootstrap`
3. Wait for PNG commits to land on branch
4. Verify visual regression test passes against baselines
5. THEN consider merge (--admin only if E2E DB flake confirmed)

**Recommendation**: enforce strict sequencing. CI workflow should fail PR if no baselines committed (future enhancement).

### Pattern P10 — `Triple auth helper E2E specs`

Standard E2E auth bypass: `seedAuthSession + seedCookieConsent + mockAuthEndpoints`. Applied in 100% of v2 E2E specs.

**Recommendation**: codify as helper file. Already present, just sustain usage.

## Anti-patterns catturati (post-mortem)

### AP1 — `networkidle waiting strategy in Playwright`

**Caught**: Wave B.1 PR #635 + sustained D.1.

**Anti-pattern**:
```ts
await page.goto('/route', { waitUntil: 'networkidle' });
```

**Fix**:
```ts
await page.goto('/route', { waitUntil: 'domcontentloaded' });
await page.waitForSelector('[data-slot="root"]', { timeout: 30_000 });
```

**Reason**: `networkidle` flakes with TanStack Query revalidation windows + SSR + lazy chunks.

### AP2 — `data-slot collision con shared root`

**Caught**: Wave B.1 PR #635.

**Anti-pattern**: adding `data-entity` to nested element when parent already exposes same attribute → duplicate DOM matches.

**Fix**: prefer `data-slot` (semantic nominal unique) over `data-entity` (semantic value, can collide).

### AP3 — `ICU plural template raw .replace()`

**Caught**: Wave D.1 final review PR #736 commit `0af5df2bf`.

**Anti-pattern**:
```ts
const subtitle = labels.subtitleTemplate.replace('{count}', String(count));
// where subtitleTemplate = "{count, plural, =0 {Nessuna} =1 {1 partita} other {# partite}}"
```

**Fix**: orchestrator resolves via `t(key, { count })`, component receives plain `string`.

**Damage**: production UI shows raw ICU syntax: `"6, plural, =0 {Nessuna...} =1 {1 partita...} other {# partite}}"`. Caught by final review.

### AP4 — `Brownfield route redirect/rewrite gotcha`

**Caught**: Wave C.1 retry PR #702 (~30 min debug lost).

**Anti-pattern**: `next.config.js` had `/games/:id → /discover/:id` permanent redirect, blocking new v2 brownfield route.

**Fix**: pre-implementation `grep -n "/route" next.config.js` audit + remove conflicting redirect explicitly.

### AP5 — `Schema invented field names from mockup`

**Caught**: Wave D.1 spec compliance review.

**Anti-pattern**: implementer copies field names from mockup (`displayName`, `score`, `isWinner`) ignoring real DTO (`playerName`, `playerOrder`, `color`).

**Fix**: Gate B audit pre-implementation. Document v1 carryover gaps. Type implementations against real DTOs.

### AP6 — `useState mirror of URL state`

**Caught**: Wave 4 D1 initial draft → corrected via Phase 0.5 contract.

**Anti-pattern**: `const [filter, setFilter] = useState<Filter>('all')` synced via useEffect from URL.

**Fix**: URL params SSOT. `parseFilter(searchParams.get('filter'))` derived inline. `router.replace()` on change.

### AP7 — `Hardcoded UI strings in production code`

**Caught**: Wave D.2 Foundation orchestrator (commit `b936108ff` initial).

**Anti-pattern**: `scoreOverflowTemplate: '+{count} altri'` hardcoded Italian in orchestrator props.

**Fix**: i18n key + `t(key, { count })` resolved. Both locale files updated.

### AP8 — `Conditional render anti-pattern (no-op CTA)`

**Caught**: Wave D.1 EmptySessions component review.

**Anti-pattern**: button rendered with `onClick={onPrimaryAction}` where `onPrimaryAction?` optional → undefined click handler when prop missing → no-op silent failure (WCAG 4.1.2 violation).

**Fix**: conditional render `{onPrimaryAction && <button onClick={onPrimaryAction} ...>}`.

## Backend integration learnings

### BE1 — `Audit issue per Wave 3 backend prereqs (PR #732)`

**Pattern**: when route depends on backend endpoints not yet shipped, file backend audit issue with concrete DTO + sequencing recommendation. Don't block FE Phase 0.5 indefinitely.

**Output**: PR #732 spec-panel review filed 4-week roadmap for /discover (#728), /toolkits/[id] (#729), /kb/[id] (#730).

### BE2 — `Codebase-grep-FIRST pre-flight (PR #741 §10.9)`

**Pattern**: before filing backend prereq audit issue, verify backend DOESN'T already implement. Wave D.2 grep revealed `ParticipantRole` enum + SSE endpoint v2 already shipped (Issue #4765 + GST-003), avoiding unnecessary backend audit.

**Recommendation**: codebase-grep-FIRST in §10.6 prerequisite gates checklist.

## Process improvements applied

### PI1 — Effort estimate recalibration

**Before** (D.1 pre-velocity-multiplier): 4-5 days estimate / route Tier S.
**After** (D.1 actual ~1 day): 1-2 days estimate / route Tier S.
**Recalibrated** in PR #741 §10.4 sequencing timeline.

### PI2 — Phase 3 retro mid-stream (this doc)

**Before**: retro deferred to Phase 4 post-Wave D.3 close.
**After** (PR #741 §10.4): retro mid-stream after Foundation merge → captures lessons IN TIME for Interactions + D.3.

### PI3 — Quality scoring methodology 4 → 7 axes

**Before**: Clarity / Completeness / Testability / Consistency.
**After** (PR #741 §10.7): + i18n discipline + Schema reality + Component composition.

## Lessons applicable to Wave D.2 Interactions sub-PR (next)

**Apply directly**:
1. ICU plural defensive pattern Gate A on chat/notes/dialog count labels
2. Schema reality v1 carryover Gate B on SSE event payloads (real backend grep: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Events/`)
3. MeepleCard divergence Gate C: dialogs (PauseOverlay/EndgameDialog) ALSO DIVERGE (modal pattern)
4. Bootstrap-then-merge Gate D: enforce strictly
5. Single-tree responsive: dialogs render conditional, NOT separate trees per viewport
6. URL state SSOT: dialog state via `?dialog=pause|endgame` (no useState modal flag)
7. WCAG dialog focus trap: `role="dialog"` + `aria-modal="true"` + focus trap library + ESC close (PauseOverlay) / ESC disabled (EndgameDialog intentional)
8. Optimistic UI + 403 rollback: write actions Player+Host optimistic, server enforces, rollback on 403 toast

**New patterns to discover** (Interactions sub-PR specific):
- SSE retry budget client-side [1s, 2s, 4s, 8s, 16s] then polling fallback
- 429 connection pool handling (max 20/session)
- Reducer composition: SSE events → state transitions
- Real-time UI flake mitigation in E2E (frozen fixture vs live SSE replay)
- Lazy-loaded dialog bundles (React.lazy, ~20 KB savings target)

## Lessons applicable to Wave D.3 (Session Summary, post-D.2)

**Apply patterns**:
- Phase 0.5 contract recommended (4+ hook composition + Confetti + reduced-motion)
- Schema reality audit: SummaryDto fields verification
- MeepleCard divergence: SummaryHeroPodium + 3-place podium + tied logic = DIVERGE
- Reduced-motion CSS-only confetti fallback (static medal icon)
- Tied podium semantics (alphabetical first-name tiebreaker)
- Cache 1h post-completion (sessions immutable post-endgame)

## Lessons applicable to Wave 3 follow-ups (when backend lands)

**Wave 3 routes blocked** (per PR #732 4-week roadmap):
- `/discover` (#728) — 5 NEW endpoints
- `/toolkits/[id]` (#729) — 7 marketplace features
- `/kb/[id]` (#730) — 5 chunk-level endpoints

**When backend ready**:
- Apply all 4 audit gates pre-implementation
- Phase 0.5 contracts already drafted (or will be) — implementations follow contracts strictly
- Tier classification: L for /discover (cross-BC composition), M-L for /kb/[id] (split-view), M for /toolkits/[id] (variant matrix)

## Acceptance for Wave D umbrella close

Per #582 DoD:
- [x] D.1 sessions-index — SHIPPED PR #736
- [ ] D.2 sessions-live — Foundation in PR (THIS), Interactions pending
- [ ] D.3 sessions-summary — pending post-D.2
- [x] Phase 3 retro mid-stream (THIS DOC)
- [ ] Phase 3 retro finalized post-D.3
- [ ] Bundle delta total Wave D < +250 KB (TBD via CI Bundle Size)

## Memory feedback files updated

Per pattern cross-wave validation:
- ✅ `feedback_v2-tier-dispatch-strategy.md` (Phase 0.5 pattern validated)
- ✅ `feedback_brownfield-route-redirect-audit.md` (redirect cleanup)
- ✅ `feedback_subagent-serial-only.md` (no parallel dispatch)

**New feedback files to create** (per PR #741 §10.8):
- 🔄 `feedback_icu-plural-defensive-pattern.md` (Gate A — AP3 evidence)
- 🔄 `feedback_schema-reality-v1-carryover.md` (Gate B — Wave 4 D1 + D.1 + D.2 evidence)
- 🔄 `feedback_meeplecard-api-fit-audit.md` (Gate C — divergence decisions)
- 🔄 `feedback_bootstrap-then-merge-discipline.md` (Gate D — D.1 violation evidence)

## References

- Phase 0.5 contracts: `docs/frontend/contracts/{games-id,agents-id,game-nights,sessions-id-live}-hooks.md`
- Spec V2 migration: `docs/superpowers/specs/2026-04-26-v2-design-migration.md`
- Wave D spec-panel: `docs/superpowers/specs/2026-05-05-wave-d-spec-panel-review.md` (with §10 amendments + §10.9 gate resolution)
- Wave 3 backend prereqs: `docs/superpowers/specs/2026-05-05-wave-3-backend-prerequisites-spec.md`
- Pattern parents: PR #635 (B.1) · #637 (B.2) · #638 (B.3) · #702 (C.1) · #711 (C.2) · #717 (4-D1) · #724 (3-step2) · #727 (3-step3a) · #732 (Wave 3 backend) · #734 (D spec-panel) · #736 (D.1) · #741 (post-D.1 amendments) · #744 (D.2 contract) · #749 (D.2 Foundation, this PR)

---

**Status**: MID-STREAM RETRO — captures lessons through Wave D.2 Foundation.
**Next update**: post-D.2 Interactions merge → final retro update before Wave D.3 + umbrella #582 close.
