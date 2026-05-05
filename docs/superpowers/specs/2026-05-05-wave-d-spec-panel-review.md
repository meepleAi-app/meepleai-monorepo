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

**Status**: DRAFT — pending user review before Phase 1 (D.1) dispatch.
**Next steps post-approval**:
1. Open child issue D.1 under umbrella #582 (route `/sessions`)
2. D.1 single-shot subagent dispatch (Wave 4 D1 blueprint)
3. After D.1 ships → open D.2 child issue + draft Phase 0.5 contract `sessions-id-live-hooks.md`
4. After D.2 contract review → foundation sub-PR dispatch
5. Continue per §6 sequencing
