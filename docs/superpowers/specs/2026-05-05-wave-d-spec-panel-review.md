# Wave D — Sessions Triade Spec-Panel Review

> **Date**: 2026-05-05
> **Trigger**: `/sc:spec-panel prossima wave`
> **Mode**: discussion (5-expert collaborative analysis)
> **Format**: detailed
> **Scope**: Wave D umbrella #582 — `/sessions`, `/sessions/[id]/live`, `/sessions/[id]`

## 1. Context

Wave D is the next sequential wave per V2 migration umbrella #578. Wave C (authenticated detail) completed via PR #702 (C.1 `/games/[id]`) and PR #711 (C.2 `/agents/[id]`). Wave 3 (3 routes) and Wave 4 D1 (`/players`) shipped this session. Wave D is **prerequisite-clean**: all hooks exist, all 16 stub components scaffolded, Wave C completed.

### State inventory

| Asset | Location | Status |
|-------|----------|--------|
| Mockups | `admin-mockups/design_files/sp4-session{s-index,-live,-live-parts,-summary,-summary-parts}.{html,jsx}` | ✅ All 6 files present |
| Stub components D.1 | `apps/web/src/components/v2/sessions/` | ✅ 3 stubs |
| Stub components D.2 | `apps/web/src/components/v2/session-live/` | ✅ 7 stubs |
| Stub components D.3 | `apps/web/src/components/v2/session-summary/` | ✅ 6 stubs |
| Session hooks | `apps/web/src/hooks/queries/useActiveSessions.ts`, `useSession*.ts` | ✅ 8+ hooks (useSession, useActiveSessions, useSessionDetail, useSessionState, useSessionFlow, useSessionDiaryQuery, useSessionVisionSnapshots, usePauseSession) |
| Real-time SSE infra | reused from existing prod | ✅ Preserved per #582 spec |
| Backend prerequisites | NONE flagged in #582 | ✅ Cleaner than Wave 3 (which has 3 backend-blocked routes) |

### Wave D scope per umbrella #582

| Sub-route | Mockup | Effort estimate | Components |
|-----------|--------|-----------------|------------|
| **D.1** `/sessions` | `sp4-sessions-index.jsx` | 4-5 days | ~10 (SessionsHero, SessionFilters, SessionCardList/Grid, ChipStrip, StatusChip, EmptyState×3) |
| **D.2** `/sessions/[id]/live` | `sp4-session-live.jsx` + parts | 10-14 days **(split 2 sub-PR)** | ~14 (LiveTopBar, TurnIndicator, PlayerRosterLive, LiveScoringPanel, ActionLogTimeline, SessionToolsRail, LiveAgentChat, LiveSessionNotes, RightColumnTabs, PauseOverlay, EndgameDialog, ConnectionLostBanner, DesktopBody, MobileBody) |
| **D.3** `/sessions/[id]` | `sp4-session-summary.jsx` + parts | 8-10 days | ~11 (SummaryHeroPodium, KpiGrid, ScoringBreakdownTable, ConnectionBar, Achievements, Diary, PhotoGallery, ChatHighlights, ShareCard, PlayAgain, Confetti) |

**Total wave**: ~22-29 days (matches 4-week umbrella ETA).

## 2. Expert panel

| Expert | Domain | Critique focus for Wave D |
|--------|--------|---------------------------|
| **Alistair Cockburn** | Use cases + actors | Primary actor + business goal per route (gioca / monitora / ricorda) |
| **Karl Wiegers** | Requirements quality | SMART criteria for #582 DoD, testability of real-time |
| **Martin Fowler** | API architecture | 3-route boundaries + responsive desktop/mobile split |
| **Sam Newman** | Distributed systems | Real-time SSE state sync + reconnection semantics |
| **Lisa Crispin** | Testing strategy | Real-time testing + dialog focus trap automation |

## 3. Per-route spec-panel critique

### 3.1 D.1 `/sessions` — Tier S (single-shot)

#### COCKBURN (primary actor + goal)
**Primary actor**: Authenticated user managing personal session inventory.
**Business goal**: Trovare sessioni passate/correnti rapidamente; filtrare per stato (attive/concluse/in pausa); vista list (denso) vs grid (visiva).

**Recommendation**: Default view per viewport (desktop=grid, mobile=list) mirror Wave 3 `/game-nights` pattern. URL state SSOT (`?view=`, `?status=`, `?search=`).

#### WIEGERS (requirements quality)
✅ **STRONG**: Stub components exist + hooks ready (useSession, useActiveSessions). Mockup is canonical baseline.
❌ **MAJOR**: #582 lacks per-route DoD breakdown — DoD in umbrella covers "3 route legacy rimosse" but doesn't specify D.1-only acceptance.
📝 **RECOMMENDATION**: Decompose umbrella DoD into per-route DoD (D.1/D.2/D.3) before dispatch.
🎯 **PRIORITY**: Medium — can defer to per-PR scope in implementation phase

#### FOWLER (architecture)
✅ **PATTERN MATCH**: D.1 mirrors Wave 4 D1 `/players` (PR #717): index page + filters + view-mode toggle + 5-state FSM.
📝 **RECOMMENDATION**: Single-shot dispatch acceptable (Tier S). NO Phase 0.5 contract needed — same pattern proven in Wave 4 D1 in 4-6 days actual.

#### NEWMAN (state sync)
**Real-time question**: should `/sessions` index reflect live status updates (e.g., user pauses session in another tab)? Current hooks have 30s staleTime on `useSession`.
📝 **RECOMMENDATION**: V1 = polling via TanStack Query default refetch (acceptable). V2 = SSE subscription for live status badges.

#### CRISPIN (testing)
✅ Single-shot pattern testable via 5-state FSM × 2-viewport visual regression (Wave 4 D1 blueprint).
📝 **RECOMMENDATION**: Tier S test ratio 70/20/10 (unit-heavy). No real-time complexity to handle in D.1.

#### Tier classification: **S**
**Dispatch strategy**: Single-shot subagent with 5-task TDD pattern (foundation → components → orchestrator → e2e → baselines). Mirror Wave 4 D1 PR #717 blueprint exactly.

---

### 3.2 D.2 `/sessions/[id]/live` — Tier L (Phase 0.5 REQUIRED)

#### COCKBURN (primary actor + goal)
**Primary actors**:
- **Player** (active session participant) — aggiornare punteggi, registrare eventi, comunicare via chat live
- **Spectator** (read-only viewer) — guarda live senza interagire (out-of-scope D.2? Verificare con #582)

**Business goal**: Esperienza session-live senza interruzioni; latenza percepita <100ms su action log; dialog modali (pause/endgame) non perdono stato.

⚠️ **OPEN QUESTION**: D.2 mockup non distingue Player/Spectator. Per Phase 0.5 contract: clarify if `viewer.role` gates write actions or if all viewers can edit.

#### WIEGERS (requirements quality)
❌ **CRITICAL**: Real-time SSE state model not specified in #582. Quali eventi? Action log append? Score update? Player join/leave? Pause/resume?
📝 **RECOMMENDATION**: Phase 0.5 contract must define SSE event schema + reconnection semantics + fallback to polling.
🎯 **PRIORITY**: HIGH — without event schema, backend + frontend can't align.

❌ **CRITICAL**: WCAG dialog focus trap requirement noted but no acceptance criteria
📝 **RECOMMENDATION**:
- PauseOverlay: `role="dialog" aria-modal="true" aria-labelledby={titleId}` + focus trap + ESC closes + restore focus on close
- EndgameDialog: same + Escape disabled (cannot accidentally dismiss endgame summary)
- Tab cycle constrained to dialog content
- Background `aria-hidden="true"` while dialog open
🎯 **PRIORITY**: HIGH — WCAG critical, listed in #582 DoD as "gap noto code review wave 2"

#### FOWLER (architecture)
**Layout pattern**: 3-column desktop (LeftSidebar TurnIndicator + PlayerRoster | CenterColumn LiveScoring + ActionLog | RightColumnTabs Tools/Chat/Notes) vs single-column mobile with bottom tab nav (score/chat/log/tools).

❌ **MAJOR**: Desktop+mobile split divergence risk — separate trees mean drift over time.
📝 **RECOMMENDATION**: Single component tree with responsive Tailwind classes (`hidden lg:block`) + isMobile prop where layout fundamentally differs (mobile = bottom-tab nav, desktop = 3-column). Lessons from Wave B.3 (single-tree responsive uniformity) sustained.

❌ **MAJOR**: PauseOverlay + EndgameDialog state ownership — orchestrator state or local state?
📝 **RECOMMENDATION**: orchestrator owns dialog state (`?dialog=pause|endgame`), URL serializable (deep-link to pause confirmation possible). Mirror Wave 3 `/game-nights` drawer pattern.

#### NEWMAN (real-time SSE)
**Critical concerns**:
1. **Reconnection semantics**: SSE drop → ConnectionLostBanner shown → exponential backoff retry → on reconnect, replay missed events from `lastEventId`
2. **Event ordering guarantee**: server must emit `id` + `retry` headers per SSE spec; client honors `Last-Event-ID` on reconnect
3. **Event schema versioning**: `event.type` field + JSON schema per event type, versioned via `event.version`

📝 **RECOMMENDATION**: Phase 0.5 contract MUST include:
- SSE endpoint shape: `GET /api/v1/sessions/{id}/events?lastEventId={nullable}`
- Event types enum: `score-update | action-log | player-join | player-leave | pause | resume | endgame | chat-message | tool-execution`
- Reconnection budget: max 5 retries with backoff [1s, 2s, 4s, 8s, 16s] before giving up + showing degraded mode
- Polling fallback: if SSE unavailable, fallback to `useSessionState` polling 5s

#### CRISPIN (testing)
❌ **CRITICAL**: Real-time SSE testing notoriously hard.
📝 **RECOMMENDATION**:
- Unit (50%): event reducer pure functions (event → next state)
- Integration (35%): MSW mock SSE stream + reducer composition
- E2E (15%): visual regression on snapshots (frozen state via fixture), NOT live SSE replay
- Smoke (separate): real-backend smoke spec hits `/sessions/{id}/events` against staging, asserts event arrives

⚠️ **Dialog focus trap**: requires axe-core + manual keyboard test in Playwright (Tab cycle stops at dialog boundaries).

#### Tier classification: **L** (definitively)
**Dispatch strategy**: 
1. **Phase 0.5 contract** drafting BEFORE implementation (Wave C.1/C.2/Wave 3 game-nights pattern)
2. **Sub-PR split** per #582 spec: foundation (LiveTopBar + TurnIndicator + PlayerRosterLive + LiveScoringPanel + ActionLogTimeline) → interactions (SessionToolsRail + LiveAgentChat + dialogs)
3. **Real-time** as separate concern: foundation PR uses static fixture (no SSE), interactions PR wires real SSE
4. **Test ratio**: Tier L 50/35/15

---

### 3.3 D.3 `/sessions/[id]` — Tier M-L (Phase 0.5 advisable)

#### COCKBURN (primary actor + goal)
**Primary actor**: Post-game user reviewing session outcome.
**Business goal**: Vedere podio + punteggi + diary + foto + chat highlights + condividere card; sentirsi gratificato (confetti) per la sessione completata.

**Secondary goal** (out-of-scope): export PDF/PNG ShareCard (per #582: "ShareCard preview-only, PNG export = backend impl, fuori scope").

#### WIEGERS (requirements quality)
✅ **STRONG**: 11 components, all stubbed. Confetti + share + podium are visual flourishes built on top of standard data sections.
❌ **MAJOR**: Confetti `prefers-reduced-motion` requirement noted but no fallback specified
📝 **RECOMMENDATION**: When `prefers-reduced-motion: reduce` → no confetti animation, show static medal icon. Defined explicit per WCAG 2.3.3 (Pause/Stop/Hide).

❌ **MINOR**: Tied podium positions (e.g., 2 players tied 2nd place) — `tied` prop in mockup but logic undefined
📝 **RECOMMENDATION**: tied = same scoring → both displayed at same place height + visual indicator (=). Tie-breaker order by alphabetical first name.

#### FOWLER (architecture)
**Pattern**: post-game detail page with multi-section vertical scroll. Each section is a "card" with discriminated state union (loading/error/empty/success).

✅ **PATTERN MATCH**: similar to Wave C.1 `/games/[id]` tabs but **vertical scroll** instead of tabs. 11 components → orchestrator wires `useSession({ id })` + `useSessionDiary({ id })` + `useSessionVisionSnapshots({ id })` + `useSessionFlow.summary({ id })`.

📝 **RECOMMENDATION**: Phase 0.5 contract advisable due to:
- 4+ hook composition (similar to Wave C.1 trigger threshold)
- Confetti + reduced-motion contract
- ShareCard preview boundary (no backend yet for PNG export)
- Empty state matrix (achievements empty, photos empty, chat empty)

#### NEWMAN (state)
**Question**: is session summary cached after first view? Or always fresh?
📝 **RECOMMENDATION**: Cache 1h post-completion (sessions are immutable post-endgame). Invalidate on chat highlights edit (admin moderator action — out of scope D.3).

#### CRISPIN (testing)
✅ Standard detail page test mix: Tier M 60/30/10.
📝 **RECOMMENDATION**: Confetti animation visual baseline excludes `Confetti` element (mask via `data-slot="confetti"`) — confetti is non-deterministic CSS animation, would flake.

#### Tier classification: **M-L** (borderline)
**Dispatch strategy**: 
- IF time-budget allows → Phase 0.5 contract (Wave C.1 pattern reuse, 4+ hook composition justifies)
- IF aggressive timeline → single-shot dispatch with detailed task brief covering Confetti + reduced-motion + tied podium edge cases

**Recommended**: Phase 0.5 contract for safety (matches Wave C.1 trigger threshold of "4+ hook composition").

---

## 4. Cross-route concerns

### 4.1 Dark mode default (Wave D specific)

❌ **MAJOR** (Fowler + Crispin): mockups use dark theme as default for D.2 (live session), with light validation as secondary. Existing v2 routes use light default with dark validation.

📝 **RECOMMENDATION**:
- D.2 dark default: explicit `data-theme="dark"` on `/sessions/[id]/live` route
- Visual baselines: 4 PNG (desktop+mobile × dark+light) for D.2 vs 2 PNG for D.1/D.3
- A11y testing: BOTH themes must pass axe-core (Wave C.1 hotfix sustained — 700-shade text on dark backgrounds)

### 4.2 Bundle budget (#582 DoD)

DoD: "Bundle delta totale wave D < +250 KB"

📝 **RECOMMENDATION** (Newman + Fowler):
- D.1: ~80 KB (Tier S, mirror Wave 4 D1 ~75 KB actual)
- D.2: ~110 KB (Tier L, more components but real-time logic stays in hooks)
- D.3: ~85 KB (Tier M-L, confetti CSS-only adds ~3 KB)
- Total: ~275 KB **OVER budget by 25 KB**

⚠️ **Margin tight**: code-split D.2 dialogs (PauseOverlay + EndgameDialog) via React.lazy — only loaded when triggered. Saves ~20 KB from initial bundle.

### 4.3 Phase 3 review gate (#582 DoD)

DoD: "Phase 3 review gate completato (`docs/frontend/v2-migration-phase1-2-retro.md` redatto)"

📝 **RECOMMENDATION**: Retro doc captures lessons from Wave A+B+C+Wave 3+Wave 4 D1+Wave D for V2 phase 1+2. Should include:
- Phase 0.5 pattern validation (Wave C.1/C.2 + Wave 3 game-nights)
- Tier S vs L dispatch outcomes (Wave 4 D1 vs Wave C.1 retry)
- Backend audit blocker pattern (Wave 3 prerequisites)
- Brownfield route redirect/rewrite gotcha (Wave C.1 lesson)
- A11y CTA contrast 700-shade discipline
- Single-tree responsive uniformity (Wave B.3 lesson)
- Visual fixture sentinel pattern + `?state=` URL override
- Bundle budget tracking discipline

This retro is the gate for **Phase 2** (post-Wave D — additional V2 routes or follow-ups).

## 5. Quality assessment summary

| Sub-route | Original spec quality (#582) | Tier | Phase 0.5 needed? | Effort |
|-----------|------------------------------|------|-------------------|--------|
| D.1 sessions-index | 7.0/10 (clear scope, hooks ready) | S | NO (single-shot) | 4-5 days |
| D.2 session-live | 5.5/10 (real-time SSE underspec, dialog a11y noted but no AC) | L | **YES** (real-time + dialog focus) | 10-14 days (split 2 sub-PR) |
| D.3 session-summary | 6.5/10 (multi-section, confetti reduced-motion noted) | M-L | RECOMMENDED (4+ hook composition + reduced-motion contract) | 8-10 days |

**Wave D umbrella quality**: 6.3/10 → would benefit from per-route Phase 0.5 contracts (D.2 mandatory, D.3 recommended).

## 6. Dispatch sequencing recommendation

### Phase 1 (week 1) — D.1 sessions-index single-shot
1. Open issue D.1 (TBD per umbrella)
2. Single-shot subagent dispatch (Wave 4 D1 PR #717 blueprint)
3. 5-task TDD: foundation → components → orchestrator → e2e → baselines
4. Bundle target ~80 KB

### Phase 2 (week 2) — D.2 Phase 0.5 contract + foundation sub-PR
1. Open issue D.2 (TBD per umbrella)
2. Draft `docs/frontend/contracts/sessions-id-live-hooks.md` (Phase 0.5):
   - SSE event schema + reconnection semantics
   - Dialog focus trap acceptance criteria
   - 12+ FSM cells (cartesian: connection × pause × endgame × tab)
   - Sub-PR split definition (foundation vs interactions)
3. Foundation sub-PR dispatch: LiveTopBar + TurnIndicator + PlayerRosterLive + LiveScoringPanel + ActionLogTimeline (static fixture, no SSE)
4. Bundle target ~60 KB foundation

### Phase 3 (week 3) — D.2 interactions sub-PR
5. Interactions sub-PR dispatch: SessionToolsRail + LiveAgentChat + LiveSessionNotes + dialogs + ConnectionLostBanner + real SSE wiring
6. Sub-PR includes E2E smoke spec for SSE event flow
7. Bundle target ~50 KB interactions (total D.2 ~110 KB)

### Phase 4 (week 4) — D.3 session-summary + Phase 3 retro
8. Open issue D.3 (TBD per umbrella)
9. Phase 0.5 contract `docs/frontend/contracts/sessions-id-summary-hooks.md` (recommended)
10. Single-PR dispatch (Tier M-L)
11. Confetti + reduced-motion + tied podium edge cases tested
12. Phase 3 retro doc `docs/frontend/v2-migration-phase1-2-retro.md` written + Wave D umbrella closed
13. Bundle target ~85 KB

**Total wave timeline**: 4 weeks (matches #582 effort estimate).

## 7. Expert consensus & blind spots

**🤝 Convergent insights**:
- D.1 single-shot dispatch acceptable (Wave 4 D1 pattern proven)
- D.2 Phase 0.5 contract MANDATORY (real-time + dialog complexity)
- D.3 Phase 0.5 contract advisable (4+ hooks + reduced-motion)
- Single-tree responsive uniformity (Wave B.3 lesson) applies
- Bundle budget tight — code-split D.2 dialogs via React.lazy

**⚖️ Productive tensions resolved**:
- Cockburn vs Wiegers on D.3 Tier classification → settled M-L with Phase 0.5 advisory (not mandatory)
- Newman vs Crispin on real-time testing strategy → reducer-based unit + MSW integration + frozen-fixture E2E (no live SSE replay)

**⚠️ Blind spots identified**:
- **Spectator role**: D.2 mockup ambiguous on player vs read-only spectator; clarify in Phase 0.5
- **Dark mode token coverage**: 700-shade discipline holds for light, dark mode contrast may need separate audit (Wave C.1 hotfix only validated light)
- **Mobile bottom-nav vs desktop 3-column**: divergent layouts may force dual component trees if responsive Tailwind not sufficient
- **Confetti accessibility**: even with reduced-motion fallback, screen reader announcement?
- **ShareCard preview boundary**: PNG export deferred to backend, but mockup shows download button — disable or hide in v1?

**🤔 Strategic questions for follow-up**:
1. Is D.2 spectator mode in scope or out? (Affects RBAC complexity)
2. Should Phase 3 retro doc precede D.3 to capture Wave D lessons mid-stream?
3. Bundle budget overrun mitigation: which dialogs/components are most lazy-loadable candidates?
4. Real-time SSE backend endpoint exists? (#582 says "Real-time SSE infra esistente riusata" — verify endpoint shape)
5. Endgame dialog: should completing endgame trigger redirect to D.3 summary automatically?

## 8. Comparison with Wave 3 backend prereqs (PR #732)

| Concern | Wave 3 (PR #732) | Wave D |
|---------|------------------|--------|
| Backend prereqs | 3/5 routes blocked (need #728, #729, #730 phases) | NONE — hooks ready |
| Real-time complexity | None (static index pages + detail) | HIGH (SSE in D.2) |
| Tier classification | Step 2 = M, Step 3a = L, Step 3b = L | D.1 = S, D.2 = L, D.3 = M-L |
| Cross-BC composition | Per-BC endpoints over BFF (Newman) | Single-BC per route (GameManagement) |
| Dispatch readiness | Step 2/3a shipped, Step 3b/4/5 backend-blocked | All 3 sub-routes immediately dispatchable |

**Conclusion**: Wave D is **more dispatch-ready** than remaining Wave 3 work (no backend wait). Recommend prioritize over Wave 3 follow-ups.

## 9. References

- Issue #582 (Wave D umbrella)
- Issue #578 (V2 phase 1 super-umbrella)
- Spec V2 migration §6: `docs/superpowers/specs/2026-04-26-v2-design-migration.md` (lines 95-160)
- Spec V2 phase 1 execution §6: `docs/superpowers/specs/2026-04-27-v2-migration-phase1-execution.md`
- Phase 0.5 FE contract templates:
  - `docs/frontend/contracts/games-id-hooks.md` (Wave C.1)
  - `docs/frontend/contracts/agents-id-hooks.md` (Wave C.2)
  - `docs/frontend/contracts/game-nights-hooks.md` (Wave 3)
- Mockups: `admin-mockups/design_files/sp4-session{s-index,-live,-live-parts,-summary,-summary-parts}.{html,jsx}`
- Stub directories:
  - `apps/web/src/components/v2/sessions/`
  - `apps/web/src/components/v2/session-live/`
  - `apps/web/src/components/v2/session-summary/`
- Hooks: `apps/web/src/hooks/queries/{useActiveSessions, useSessionDetail, useSessionState, useSessionFlow, useSessionQuota, useSessionSnapshots}.ts`
- Memory feedback files (lessons applicable to Wave D):
  - `feedback_v2-tier-dispatch-strategy.md` (Tier classification)
  - `feedback_brownfield-route-redirect-audit.md` (redirect cleanup)
  - `feedback_subagent-serial-only.md` (no parallel dispatch)
- Pattern parents: PR #702 (Wave C.1), PR #711 (Wave C.2), PR #717 (Wave 4 D1), PR #724 (Wave 3 Step 2)

---

**Status**: ⚠️ AMENDED 2026-05-05 — see §10 post-D.1 addendum.
**Next steps post-approval**: see §10.4 revised sequencing.

---

## 10. Post-D.1 Amendments (2026-05-05)

> **Trigger**: `/sc:spec-panel review #734 sequencing` after Wave D.1 PR #736 squash `7b8f558db` MERGED 2026-05-05T18:35:35Z.
> **Validation data**: D.1 actuals (10 commits + 4 review-driven fixes, 187 tests, 6 issues caught by two-stage review including 1 critical at final cumulative review).
> **Wave D umbrella quality reassessed**: 6.3/10 → **5.5/10** (revealed unflagged blind spots).

### 10.1 Predictions vs actuals

| Original prediction | D.1 actual | Validated? |
|---------------------|------------|------------|
| Tier S single-shot acceptable | Tier S, 5-task TDD shipped | ✅ |
| 4-5 days effort | ~1 day (subagent-driven) | ❌ **4-5× overstated** |
| Wave 4 D1 blueprint mirror | Mirror confirmed | ✅ |
| Bundle target ~80 KB | Unverified (--admin merge) | ⏳ |
| Quality 7.0/10 | Final review caught CRITICAL ICU bug | ❌ **Score TOO HIGH** |
| MeepleCard variant=list/grid | Cards diverged (API insufficient) | ❌ **Assumption wrong** |
| Single-shot dispatch acceptable | 6 review-driven fixes across all tasks | ⚠️ Caveat needed |

### 10.2 Four NEW audit gates (must apply to D.2/D.3 + future waves)

#### Gate A — ICU plural defensive pattern (Wiegers + Newman)

**Trigger**: any i18n key with `{count, plural, ...}` or `{var, plural, ...}` ICU formatter.

**Pre-implementation grep audit**:
```bash
grep -E "intl\.messages\[.*\] as string" apps/web/src/app/
grep "{count, plural" apps/web/src/locales/*.json
```

**Required defensive pattern**:
- ✅ Orchestrator: `t(key, { count })` resolves ICU plural via next-intl formatter
- ✅ Component: receives plain `string` (not `template + count` props)
- ❌ ANTI-PATTERN: `intl.messages[key].replace('{count}', String(count))` — naive replace breaks ICU plural opening identifier, produces raw ICU syntax in production UI

**Rationale**: D.1 PR #736 final review caught this defect — naive `.replace('{count}', ...)` replaced only the opening variable identifier of the ICU plural clause, producing `"6, plural, =0 {Nessuna...} =1 {1 partita...} other {# partite registrate}}"` rendered as visible text. Fixed in commit `0af5df2bf`.

**Apply to**: D.2 LiveTopBar (turn count, player count), EndgameDialog (final scores), D.3 SummaryHeroPodium (player counts), KpiGrid templates.

---

#### Gate B — Schema reality v1 carryover audit (Wiegers + Fowler)

**Trigger**: every Phase 0.5 contract drafting + every Tier S audit checklist.

**Required steps**:
1. Read DTO schema source: `apps/web/src/lib/api/schemas/<domain>.schemas.ts`
2. Map mockup field names to actual DTO field names
3. Document v1 carryover gaps explicitly
4. Propose backend extension issue (post-merge followup)
5. Plan graceful degradation in components (e.g., hide score chips when all-zero)

**Required documentation block** in transform/derive functions:
```ts
/**
 * SCHEMA REALITY V1 CARRYOVER (Wave X.Y, mirror PR #717 + PR #736 pattern):
 * <DtoName> exposes only <fields>. Wave X.Y = visual upgrade only;
 * <missing-feature> is decorative (placeholder).
 * Followup issue post-merge: <link or TBD>.
 */
```

**Rationale**: D.1 actual revealed `SessionPlayerDto` exposes only `playerName/playerOrder/color` — NO `score/winner` fields. Implementer initially invented `displayName/score/isWinner` (matching mockup), broke compile against real DTO. Caught by spec compliance review (Task 1, commit `329262f8f`).

**Apply to**: D.2 SSE event schema (verify `ScoreEvent`, `ActionLogEvent` shapes against `apps/api/src/Api/BoundedContexts/SessionTracking/`), D.3 SummaryDto schema, all future routes.

---

#### Gate C — MeepleCard API-fit audit (Fowler)

**Trigger**: any v2 component spec mentioning "use MeepleCard variant=list/grid/...".

**Required steps**:
1. Inspect `apps/web/src/components/ui/data-display/meeple-card/` props (`ListCard.tsx`, `GridCard.tsx`)
2. Match against mockup composition requirements:
   - Children/inline content slots? (MeepleCard has NONE)
   - Wrapper-level animation classes? (`mai-pulse`, etc.)
   - Absolute-positioned overlays? (OutcomeBadge over cover area)
   - Custom left-accent-bar? (`border-l-[3px]` sessions pattern)
3. Decide WRAP (Wave 4 D1 simple cards = title + subtitle) vs DIVERGE (D.1 rich cards = scoring + outcome + connection chips)

**Decision rule**:
- ✅ WRAP: title + subtitle + optional metadata only (Players/Games index)
- ❌ DIVERGE: inline composition required (Sessions list/grid, likely D.3 SummaryHeroPodium)

**Rationale**: D.1 cards diverged from MeepleCard because `MeepleCardProps` has no children slot, no animation injection, no positioned overlays. ENGINEERING-JUSTIFIED divergence per spec compliance reviewer ruling. Pattern note: future Tier S routes with rich card composition may also justify divergence.

**Apply to**: D.3 SummaryHeroPodium (3-place podium with confetti + tied logic — RICHER than MeepleCard, likely DIVERGE).

---

#### Gate D — Bootstrap-then-merge discipline (Crispin)

**Trigger**: any visual regression PR with `gh workflow run visual-regression-migrated.yml ... -f mode=bootstrap`.

**Required sequence**:
1. Open PR (status BLOCKED waiting on bootstrap baselines)
2. Trigger bootstrap workflow
3. **WAIT** for bootstrap PNG commits to land on PR branch
4. Verify visual regression test passes against committed baselines
5. THEN consider --admin merge (only if E2E DB flake confirmed)

**ANTI-PATTERN**: --admin merge before bootstrap completes
- D.1 actual: PR #736 merged at 18:35:35Z, bootstrap workflow `25393528841` dispatched ~5min before merge → no rollback signal if bootstrap fails
- Risk: post-merge baseline drift, broken visual tests on subsequent PRs

**Rationale**: Bootstrap workflow generates canonical PNG baselines on Linux x86-64 runner. If bootstrap fails (e.g., backend missing, fixture mismatch), --admin merge ships code WITHOUT visual regression coverage. Future PRs will fail visual tests until baselines manually added.

**Apply to**: D.2 foundation sub-PR, D.2 interactions sub-PR, D.3 PR — enforce strictly.

### 10.3 Updated audit checklist (additive to §3 per-route critique)

For D.2 Phase 0.5 contract drafting, add these sections:
- §X: i18n defensive pattern (Gate A)
- §X: SSE event schema reality audit (Gate B)
- §X: MeepleCard API-fit audit per component (Gate C)
- §X: SSE-to-polling transition test (NEW from D.1 review)

For D.3 Phase 0.5 contract drafting:
- All Gate A-D applied
- Confetti accessibility audit (screen reader announcement)
- ShareCard preview boundary clarification (download button hidden v1)

### 10.4 Revised sequencing timeline

Original #734 timeline: 4 weeks. Revised post-D.1 (subagent-driven velocity multiplier):

| Phase | Original | Revised |
|-------|----------|---------|
| 1 | Week 1: D.1 single-shot | ✅ Day 1 — D.1 SHIPPED PR #736 |
| 2 | Week 2: D.2 P0.5 + foundation | Days 2-4: spectator resolution + D.2 P0.5 contract + foundation sub-PR |
| 2.5 | — | Day 5: **Phase 3 retro mid-stream** (NEW — captures D.1+D.2 lessons before D.3) |
| 3 | Week 3: D.2 interactions | Days 6-8: D.2 interactions sub-PR + real SSE wiring + smoke spec |
| 4 | Week 4: D.3 + retro | Days 9-11: D.3 P0.5 (informed by retro) + impl + close umbrella |

**Total revised**: ~11 days (1.5-2 weeks) vs original 4 weeks.

⚠️ **CAVEAT**: D.2 has unknown unknowns (real-time + dialog + dark mode). Conservative: 2 weeks total. Re-estimate after D.2 foundation sub-PR ships.

### 10.5 Strategic questions resolution

| # | Question (from §7) | Status | Action |
|---|--------------------|--------|--------|
| 1 | D.2 spectator mode in scope? | OPEN | **Prerequisite gate before D.2 P0.5** |
| 2 | Phase 3 retro precede D.3? | ✅ RESOLVED → YES (mid-stream after D.2 foundation) | §10.4 sequencing reflects |
| 3 | Bundle budget overrun mitigation | PARTIAL (D.1 unverified) | Verify D.1 CI Bundle Size, then assess |
| 4 | Real-time SSE backend endpoint exists? | OPEN | **Verify before D.2 P0.5 dispatch** |
| 5 | Endgame dialog → D.3 auto-redirect? | OPEN (UX defer) | Punt to D.3 implementation |

### 10.6 Pre-D.2-dispatch prerequisite gates (ordered)

1. ✅ **D.1 ship verification**: PR #736 merged, bootstrap workflow status check
2. ⚠️ **D.2 spectator role decision**: re-read #582, RBAC scope
3. ⚠️ **SSE backend endpoint verification**: `grep -rn "sessions.*events" apps/api/src/Api/BoundedContexts/SessionTracking/` — confirm endpoint shape OR file backend prerequisite issue
4. ⚠️ **D.1 bundle baseline verification**: PR #736 Bundle Size check result (post-merge analytics)
5. → THEN draft D.2 Phase 0.5 contract with Gates A+B+C+D applied

### 10.7 Quality scoring methodology amendment

Original quality scores (#734 §5) missed:
- i18n template handling category (caught D.1 ICU plural)
- Schema reality v1 carryover risk (caught D.1 player fields)
- MeepleCard API-fit risk (caught D.1 card divergence)

**Revised scoring axes** (post-D.1):
- Clarity (0-10) — language precision
- Completeness (0-10) — coverage of essential elements
- Testability (0-10) — measurability + validation
- Consistency (0-10) — internal coherence
- **i18n discipline (0-10)** — NEW: ICU formatter handling, template safety
- **Schema reality (0-10)** — NEW: DTO alignment with mockup expectations
- **Component composition (0-10)** — NEW: shared component API-fit + divergence justification

Future Phase 0.5 contracts should self-assess on these 7 axes.

### 10.8 Pattern library updates (memory feedback files)

Capture for future waves in `~/.claude/projects/.../memory/`:
- New: `feedback_icu-plural-defensive-pattern.md` (Gate A)
- New: `feedback_schema-reality-v1-carryover.md` (Gate B + Wave 4 D1 + Wave D.1 evidence)
- New: `feedback_meeplecard-api-fit-audit.md` (Gate C)
- New: `feedback_bootstrap-then-merge-discipline.md` (Gate D)

These join existing `feedback_v2-tier-dispatch-strategy.md` + `feedback_brownfield-route-redirect-audit.md` + `feedback_subagent-serial-only.md`.

---

**Amendment status**: APPLIED 2026-05-05 post-D.1 ship.
**Next decision point**: D.2 Phase 0.5 contract drafting (after prerequisite gates §10.6 resolved).

### 10.9 Prerequisite gates RESOLVED (2026-05-05)

Investigation post-amendment closed both critical OPEN gates from §10.6:

#### Gate D.2.1 — Spectator role decision: ✅ RESOLVED (in-scope, server-enforced)

**Source**: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ParticipantRole.cs`

Backend defines a 3-value enum with server-enforced action gating:

| Role | Value | Capabilities |
|------|-------|--------------|
| **Spectator** | 0 | View-only access. Can send chat messages but **cannot modify session state**. |
| **Player** | 1 | Active participant. Own score, dice, cards, timer, chat. |
| **Host** | 2 | All Player actions + advance turns, pause/resume, kick participants, modify toolkit. |

**Server enforcement**: Commands implement `IRequireSessionRole` with `MinimumRole` property. E.g., `AddSessionEventCommand.MinimumRole = Player` → spectators get 403 if they attempt to add events. `AssignParticipantRoleCommand.MinimumRole = Host` → only host can change roles.

**Source ticket**: Issue #4765 (Player Action Endpoints + Host Validation) introduced role-based gating.

**D.2 Phase 0.5 implications**:
- ✅ Spectator IS in scope (existing concept, server-enforced)
- Frontend must read `participant.role` from session DTO + render variant per role:
  - Player + Host → write actions enabled (score input, dice, cards, timer)
  - Spectator → write actions hidden OR disabled with tooltip "Only players can update scores"
  - Host-only actions (pause/resume, kick, advance turn) → only Host sees these UI controls
- Optimistic UI: client may show button enabled but server enforces — handle 403 gracefully (toast: "Permission denied")
- E2E coverage: 3-role variant matrix (Spectator + Player + Host) for visual regression

#### Gate D.2.2 — SSE backend endpoint verification: ✅ RESOLVED (4 endpoints exist)

**Sources**: `apps/api/src/Api/Routing/SessionTracking/SessionQueryEndpoints.cs`, `apps/api/src/Api/Routing/SessionFlowEndpoints.cs`

Four real-time/event endpoints exist:

| Endpoint | Type | Use case |
|----------|------|----------|
| `GET /game-sessions/{id}/stream` | SSE v1 | Basic SSE, no reconnection state |
| `GET /game-sessions/{id}/stream/v2` | SSE **v2** ⭐ | **Last-Event-ID reconnection, typed events, conn pool 20 max, per-player filtering** |
| `GET /game-sessions/{id}/events` | REST paginated | Timeline events query (NOT SSE — paginated session diary) |
| `GET /sessions/{id}/diary/stream` | SSE | Diary-specific updates |

**Recommended for D.2: `/game-sessions/{id}/stream/v2`** — implements all Newman's reconnection recommendations from §3.2:

- ✅ **Last-Event-ID header** (line 357): `var lastEventId = context.Request.Headers["Last-Event-ID"].FirstOrDefault();`
- ✅ **Typed event names** (line 395): `event: <EventType>\n` (e.g., `session:score`, `session:turn`)
- ✅ **Event ID per envelope** (line 394): `id: <envelope.Id>\n` for client `Last-Event-ID` tracking
- ✅ **Heartbeat** (lines 367-384): 30s interval, `event: heartbeat\ndata: {timestamp}\n\n`
- ✅ **Connection pool limits** (line 351): max 20 connections/session, returns 429 Too Many Requests
- ✅ **Auth + access check** (lines 329-348): 401 (no auth), 403 (no access), 404 (session not found)
- ✅ **Per-player filtering**: `broadcastService.SubscribeAsync(sessionId, userId, lastEventId, ct)` filters events by recipient

**Service**: `ISessionBroadcastService.SubscribeAsync(...)` exposes `IAsyncEnumerable<EventEnvelope>` with `{ Id, EventType, Data }`.

**Headers required** (per server impl):
```
GET /api/v1/game-sessions/{id}/stream/v2
Headers:
  Cookie: <session-cookie>
  Last-Event-ID: <last-id-on-reconnect>  // optional
Response:
  Content-Type: text/event-stream
  Cache-Control: no-cache
  Connection: keep-alive
```

**SSE event format**:
```
id: <event-id>
event: <EventType>  // e.g., session:score, session:turn, session:pause
data: <json-payload>

```

**D.2 Phase 0.5 implications**:
- ✅ Backend SSE is **production-ready**, no backend prerequisite issue needed (unlike Wave 3)
- Frontend `useSessionLiveStream` hook should:
  - Use native `EventSource` API (browser-native SSE client)
  - Track `lastEventId` from `event.lastEventId` automatically (browser handles)
  - On error: exponential backoff retry [1s, 2s, 4s, 8s, 16s] (Newman recommendation)
  - After 5 retries → fall back to polling `useSessionState` 5s
  - Show `ConnectionLostBanner` when SSE drops + auto-hide on reconnect
  - Handle 429 (connection pool full) → show "Sessione affollata, riprova tra poco"
- Event types enum to verify in implementation: grep backend for typed names emitted (e.g., `session:score`, `session:turn`, `session:pause`, `session:resume`, `session:endgame`, `session:chat`, `session:tool-execution`)

#### Gate D.2 unblocked

Both prerequisite gates **CLOSED**. D.2 Phase 0.5 contract drafting can proceed. Updated #10.6 sequencing:

1. ✅ D.1 ship verification (PR #736 merged)
2. ✅ **D.2 spectator role**: in-scope, 3-role variant matrix (Spectator/Player/Host) per server enum
3. ✅ **SSE backend endpoint**: `/game-sessions/{id}/stream/v2` production-ready
4. ⚠️ D.1 bundle baseline verification (deferred, post-merge analytics — non-blocking)
5. → **PROCEED** to D.2 Phase 0.5 contract drafting

#### D.2 Phase 0.5 contract additions (per resolved gates)

The contract MUST include:
- **§ Role-based variant matrix**: Spectator/Player/Host UI affordances + server-enforced gating + 403 handling
- **§ SSE event schema**: typed event names enum, payload schemas per type, version field
- **§ EventSource integration**: `lastEventId` tracking via browser API, retry semantics, 5-retry budget
- **§ Polling fallback**: `useSessionState` 5s after SSE retry exhaustion, banner UX
- **§ Connection pool handling**: 429 response → user-facing "Sessione affollata" message
- **§ Heartbeat handling**: optional client-side detection of stale connection (no event >35s post-heartbeat)

#### Pattern emerso

**Gate resolution via codebase grep** — when prerequisite gates flag UNKNOWN backend state, the fastest resolution path is direct codebase investigation (`grep -rn` on backend BC + Domain/Enums + Routing). For Wave D.2, this revealed:
- Spectator role NOT a UI invention — backend has full 3-role enum + command-level gating already
- SSE infrastructure NOT a future requirement — production-ready v2 endpoint with all advanced features (Last-Event-ID, typed events, conn pool, per-player filtering)

This pattern saves 1-2 days vs filing backend issue + waiting for clarification. Apply BEFORE filing audit issues like Wave 3 #728/#729/#730.
