# Plan — #2389 Block C cleanup deprecated `scores` + i18n + lint promotion

**Date**: 2026-06-19
**Issue**: [#2389](https://github.com/meepleAi-app/meepleai-monorepo/issues/2389)
**Parent**: epic Game Night Improvvisata polymorphic scoring migration
**Predecessors**:
- PR [#2428](https://github.com/meepleAi-app/meepleai-monorepo/pull/2428) Block A (store + SignalR contract evolution, merged 2026-06-19T05:33Z)
- PR [#2434](https://github.com/meepleAi-app/meepleai-monorepo/pull/2434) Block B (scoringType selector wire-up, merged 2026-06-19T09:00Z)
- PR [#2435](https://github.com/meepleAi-app/meepleai-monorepo/pull/2435) Block B+ (PolymorphicScoreEditor host swap + mutation wire, merged 2026-06-19T16:34Z)
- Spec review: `/sc:spec-panel` 2026-06-19 — Wiegers/Fowler/Newman/Adzic/Nygard

**Effort**: ~1.5gg
**Risk**: LOW (app non distribuita, wait gate sollevato; store senza `persist` middleware → client refresh recupera state via REST hydration)
**Target branch**: `main-dev` (parent)
**Feature branch**: `feature/issue-2389-block-c-cleanup`

---

## Scope locks

### Q1 — Server-side `ScoreUpdated` event status
**(b) Solo `ScoringConfigured`** — confermato via grep:
- `apps/api/src/Api/Hubs/GameStateHub.cs:302` `NotifyScoreUpdated` hub method esiste ma **nessun caller server-side** (`grep -rn "NotifyScoreUpdated" apps/api/src` → 1 match = la definizione stessa).
- Unico caller storico: `useSignalrSession.ts:211` (`sendScore`), chiamato solo da `ScoreBoard.tsx:176,184`. Post-Block B+ il Host usa `PolymorphicScoreEditor` + REST mutation `UpdateSessionScoresCommand`, **non più** `sendScore`.
- Conseguenza: handler `ScoreUpdated` in `useSignalrSession.ts:103-105` è dead code (server non emette più, client non chiama più `NotifyScoreUpdated`). Safe da rimuovere insieme al action `updateScore`.
- **Out of scope FE**: rimozione `NotifyScoreUpdated` server hub method + `ScoreUpdatedEvent` legacy emission cleanup (deferred a ticket BE).

### Q2 — i18n bundle
**(a) Bundled in Block C** — 1 PR singolo, ~16 keys (4 categorie × 4 keys).

### Q3 — Behavioral acceptance
**(a) Playwright E2E** — 2 scenari smoke (Points + Ranking).

---

## Out of scope

| Item | Reason | Tracking |
|---|---|---|
| `apps/web/src/stores/session/store.ts` (`useSessionStore`) | Store distinto, già playerId-keyed | nessuno (no work) |
| BE `NotifyScoreUpdated` hub method + `ScoreUpdatedEvent` legacy emission | Dead code BE, FE-only cleanup | Filer ticket post-merge |
| Proposal flow rimozione (`addProposal` / `pendingProposals` / `GuestScoreProposal`) | Ancora utilizzato dal guest flow; refactor solo `resolveProposal` per non mutare `scores` | nessuno (preservato) |

---

## Discoveries vs spec originale

| Item | Spec originale | Realtà verificata |
|---|---|---|
| Consumer di `state.scores` | Solo `ScoreBoard.tsx:166-183` | + `useSignalrSession.ts:103` (handler `ScoreUpdated`) + `useSignalrSession.ts:211` (`sendScore` invoke) + `resolveProposal` mutation |
| `ScoreBoard` status | Sopravvive con playerId-keyed refactor | **Già broken post-B+** (scores=0 sempre, leader=null sempre); Block C lo ripara |
| ScoreBoard mounted dove | Non specificato | `/sessions/live/[sessionId]/scores/page.tsx:66` (non-Host Points only) + `/join/[token]/GuestJoinView.tsx:369` (guest, isHost=false). Mai con isHost=true post-B+ → Host controls + proposal cards = dead code interno |
| Proposal flow | Non menzionato | Vivo per guest; `resolveProposal` mutation di `scores` è dead path (mai eseguita) ma va rimossa dalla store |

---

## Task list TDD (10 task, 5 phases)

### Phase 1 — Foundation (store + hook migration)

#### T1 — Migrate `useSessionScores().leader` to playerId-keyed

**RED**: aggiungi test a `apps/web/src/lib/domain-hooks/__tests__/useSessionScores.test.ts`:
```ts
it('derives leader from scoreData Points (playerId-keyed)', () => {
  setupStoreWithScoringConfig({ scoringType: 'Points', scoreData: {
    scores: [{ playerId: 'p1', points: 5 }, { playerId: 'p2', points: 10 }]
  }});
  const { result } = renderHook(() => useSessionScores());
  expect(result.current.leader).toBe('p2');
});

it('returns null leader when scoringType !== Points', () => {
  setupStoreWithScoringConfig({ scoringType: 'Ranking', scoreData: {...} });
  expect(useSessionScores().leader).toBeNull();
});
```

**GREEN**: in `useSessionScores.ts:74-78` refactor `leader` per derivare da `scoreData` Points playerId-keyed:
```ts
const leader = useMemo<string | null>(() => {
  if (scoringType !== 'Points' || scoreData == null) return null;
  const pointsData = scoreData as ScoreDataByType['Points'];
  if (!pointsData.scores.length) return null;
  return pointsData.scores.reduce((best, current) =>
    current.points > best.points ? current : best
  ).playerId;
}, [scoringType, scoreData]);
```

Rimuovi `const legacyScores = useLiveSessionStore(s => s.scores);` (riga 66). Aggiorna JSDoc `UseSessionScoresReturn.leader`: "`string | null` — playerId of the leading player (highest Points), or null".

**Verify**: pnpm test useSessionScores; tutti passanti.

#### T2 — Remove `scores` field + `updateScore` action from store

**RED**: aggiungi test a `apps/web/src/lib/stores/__tests__/live-session-store.test.ts`:
```ts
it('resolveProposal removes from pending without touching scores', () => {
  // accept path: scores unchanged (mutation removed)
});
```

**GREEN**: in `live-session-store.ts`:
- Rimuovi `scores: Record<string, number>` da `LiveSessionState` (riga 60)
- Rimuovi `updateScore: (playerName, score) => void` action type (riga 85)
- Rimuovi `scores: {}` da `initialState` (riga 113)
- Rimuovi `updateScore` action impl (righe 136-143)
- Refactor `resolveProposal` (righe 154-176): rimuovi mutation `scores`, mantieni solo `pendingProposals` filter:
  ```ts
  resolveProposal: (proposalId, _accepted) => {
    const proposal = get().pendingProposals.find(p => p.id === proposalId);
    if (!proposal) return;
    set(state => ({
      pendingProposals: state.pendingProposals.filter(p => p.id !== proposalId),
    }), false, 'resolveProposal');
  }
  ```
- Aggiorna JSDoc file-level: rimuovi @deprecated note relative a `scores`.
- Aggiorna `Omit<>` keys (righe 96-105) per rimuovere `'updateScore'`.

**Rimuovi `scores` field da `useSessionScores.ts`**:
- Rimuovi `scores: Record<string, number>` da `UseSessionScoresReturn` (righe 33-41)
- Rimuovi `const scores = useMemo<>(...)` (righe 68-72)
- Aggiorna return statement (riga 80): `return { scoringType, scoreData, players, pendingProposals, leader }`

**Verify**: pnpm test live-session-store + useSessionScores.

---

### Phase 2 — Consumer migration

#### T3 — Refactor `ScoreBoard.tsx` (playerId-keyed + dead code removal)

**RED**: aggiorna `apps/web/src/components/session/live/__tests__/ScoreBoard.test.tsx`:
- Rimuovi test `'calls updateScore when host clicks + button'` (action rimossa)
- Rimuovi test su Host controls (Host non monta più ScoreBoard post-B+)
- Aggiungi test: `'displays scores keyed by playerId from scoreData Points'`
- Aggiungi test: `'highlights leader badge on player.id === leader'`
- Aggiungi test: `'returns empty state when no scoreData'`

**GREEN**: in `ScoreBoard.tsx`:
- Rimuovi import `updateScore`, `useSignalRSession`, `sendScore`, `logger`, `resolveProposal`, `ScoreProposal`
- Rimuovi `useLiveSessionStore` imports per `updateScore`/`resolveProposal`
- Refactor lookup: `scores[player.name]` → `scores[player.id]` (riga 211)
- Refactor leader check: `player.name === leader` → `player.id === leader` (riga 212)
- Rimuovi `handleScoreChange` function (righe 172-179) — dead code
- Rimuovi `handleApprove` + `handleReject` functions (righe 181-191) — dead code
- Rimuovi `isHost && (...)` Host controls block (righe 86-109 in `PlayerScoreCard`)
- Rimuovi `isHost && pendingProposals.length > 0 && (...)` proposal cards block (righe 222-238)
- Rimuovi prop `isHost` da `ScoreBoardProps` + signature (riga 162) — non più usato
- Aggiorna call site `/sessions/live/[sessionId]/scores/page.tsx:66` per rimuovere `isHost={isHost}`
- Aggiorna call site `/join/[token]/GuestJoinView.tsx:369` per rimuovere `isHost={false}`
- Semplifica `PlayerScoreCard`: rimuovi `isHost`, `onIncrement`, `onDecrement` props

**Verify**: pnpm test ScoreBoard; render check su `/scores` page non-Host + `/join/[token]` con scoreData mock.

#### T4 — Cleanup `useSignalrSession.ts` legacy handler + sendScore

**RED**: aggiorna `apps/web/src/lib/domain-hooks/__tests__/useSignalrSession.test.ts`:
- Rimuovi test `'updateScore updates score for the given player'` (riga 98-100, 126)
- Rimuovi test su `sendScore` invoke
- Aggiungi test: `'ScoringConfigured event → setScoringConfig action with scoreData payload'` (regression-pin del nuovo flow, Newman §2)

**GREEN**: in `useSignalrSession.ts`:
- Rimuovi `interface ScoreUpdatedPayload` (riga 33-36 circa)
- Rimuovi handler `conn.on('ScoreUpdated', ...)` (righe 103-105)
- Rimuovi `sendScore` method dal return type (riga 69)
- Rimuovi `sendScore` impl (righe 208-211, invoke `NotifyScoreUpdated`)
- Rimuovi `sendScore` da return statement (riga 229)

**Verify**: pnpm test useSignalrSession; tutti passanti.

---

### Phase 3 — i18n catalog completion

#### T5 — Add ~16 keys to `it.json` + `en.json`

Catalog keys (lista esplicita):
```
pages.sessionLive.scoring.ranking.title
pages.sessionLive.scoring.ranking.columns.rank
pages.sessionLive.scoring.ranking.columns.player
pages.sessionLive.scoring.ranking.columns.score
pages.sessionLive.scoring.binaryWin.title
pages.sessionLive.scoring.binaryWin.winLabel
pages.sessionLive.scoring.binaryWin.loseLabel
pages.sessionLive.scoring.binaryWin.pendingLabel
pages.sessionLive.scoring.objectives.title
pages.sessionLive.scoring.objectives.completedLabel
pages.sessionLive.scoring.objectives.pendingLabel
pages.sessionLive.scoring.objectives.columns.player
pages.sessionLive.scoring.points.title
pages.sessionLive.scoring.points.leaderLabel
pages.sessionLive.scoring.points.columns.player
pages.sessionLive.scoring.points.columns.score
```

**Action**:
- `apps/web/src/locales/it.json`: aggiungi 16 chiavi (verifica fallback attuali in `SessionLiveView.tsx scoringPanelLabels` memo per allineamento)
- `apps/web/src/locales/en.json`: aggiungi 16 chiavi con traduzione inglese
- Verifica con `pnpm i18n:validate` se script esiste (`pnpm run | grep i18n`), altrimenti grep coverage.

#### T6 — Refactor `SessionLiveView.tsx` `scoringPanelLabels` memo

Sostituisci `intl.messages['...'] as string ?? 'fallback'` con `t('pages.sessionLive.scoring....')` per tutte e 4 categorie scoringType. Rimuovi inline italian fallback.

**Verify**: render test su SessionLiveView con scoringType=Ranking/BinaryWin/Objectives/Points — verificare labels corrette.

---

### Phase 4 — Lint promotion + smoke E2E

#### T7 — Promote ESLint `no-store-scores-direct` warn → error

**Pre-condition**: T1-T4 completi → grep `useLiveSessionStore.*scores\b` → 0 match production code.

**Action**: in `apps/web/eslint.config.mjs:291` cambia `'local/no-store-scores-direct': 'warn'` → `'error'`.

**Verify**: `pnpm lint --max-warnings 0` → 0 errors, 0 warnings su rule target.

#### T8 — Smoke E2E Playwright (2 scenari, Q3=a)

Nuovo file `apps/web/e2e/issue-2389-block-c-scoring-smoke.spec.ts`:

```ts
test('Host adjusts score on scoringType=Points → mutation OK + leader recompute', async ({ page }) => {
  // Setup: login as Host, navigate to /sessions/[id]/live, scoringType=Points
  // Action: click +1 button on Player Bob card
  // Assert: ScoreTabContent dispatches UpdateSessionScoresCommand; after debounce (500ms)
  //   leader crown migrates to Bob if score > others
});

test('Spectator receives ScoringConfigured with scoringType=Ranking → polymorphic rendering OK', async ({ page }) => {
  // Setup: login as Spectator, navigate to /sessions/[id]/live
  // Action: mock SignalR ScoringConfigured event with Ranking payload
  // Assert: ScoringPanelRenderer mounts Ranking variant; no console error
});
```

**Verify**: `pnpm test:e2e issue-2389-block-c` → both pass.

---

### Phase 5 — Documentation + PR

#### T9 — Update CLAUDE.md

In `CLAUDE.md` § "G5a polymorphic wire — Issue #2389":
- Aggiorna paragrafo Block B+ per riflettere che backward-compat field `scores: Record<string, number>` è stato **rimosso in Block C** (date 2026-06-19).
- Rimuovi (o archive) riferimenti a "deprecated `scores: Record<string, number>` field"
- Aggiungi reference al PR Block C.

#### T10 — Create PR

```
Title: chore(session-live): #2389 Block C — cleanup deprecated scores + i18n completion + lint promotion
Target: main-dev
Body:
  ## Summary
  - Remove `scores: Record<string, number>` + `updateScore` action from live-session-store
  - Migrate `useSessionScores().leader` to playerId-keyed (derived from scoreData Points)
  - Refactor ScoreBoard.tsx to playerId-keyed lookup + remove dead Host controls
  - Cleanup useSignalrSession.ts: remove legacy `ScoreUpdated` handler + `sendScore` invoke
  - Add ~16 i18n keys for ScoringPanelRenderer labels (it.json + en.json)
  - Promote ESLint local/no-store-scores-direct: warn → error
  - Smoke E2E: 2 Playwright scenarios (Points host adjust + Ranking spectator render)

  ## Test plan
  - [x] Unit tests pass (Vitest)
  - [x] pnpm lint --max-warnings 0
  - [x] pnpm typecheck
  - [x] Smoke E2E pass

  ## Out of scope
  - BE `NotifyScoreUpdated` hub method cleanup (dead code BE, separate ticket)
  - Proposal flow removal (still in use by guest)
```

---

## Acceptance criteria (final, post-spec-panel)

### Structural

- [ ] `grep -rn "useLiveSessionStore.*scores\b" apps/web/src --include="*.ts" --include="*.tsx"` → 0 match (test files esclusi)
- [ ] `grep -rn "useSessionScores().scores" apps/web/src` → 0 match
- [ ] `grep -rn "useLiveSessionStore.*updateScore\b" apps/web/src` → 0 match (esclude `useSessionStore` distinto)
- [ ] `grep -rn "sendScore\b" apps/web/src` → 0 match
- [ ] `LiveSessionState` interface NON contiene `scores` né `updateScore`
- [ ] `UseSessionScoresReturn` interface NON contiene `scores`
- [ ] `UseSessionScoresReturn.leader` JSDoc dichiara `playerId | null`

### Linting

- [ ] `pnpm lint --max-warnings 0` → zero nuovi error/warning vs baseline `main-dev`
- [ ] `local/no-store-scores-direct: 'error'` attivo, 0 violation

### Behavioral

- [ ] Smoke E2E Points host adjust → mutation + leader recompute OK
- [ ] Smoke E2E Ranking spectator render → ScoringPanelRenderer mount OK
- [ ] Regression-pin: `ScoringConfigured` event → `setScoringConfig` action → `useSessionScores().scoreData == payload` (in useSignalrSession.test.ts)

### i18n

- [ ] `it.json` contiene esattamente le 16 keys elencate in T5
- [ ] `en.json` contiene le stesse 16 keys con valori inglesi
- [ ] `SessionLiveView` `scoringPanelLabels` memo NON contiene fallback italiani inline

### Documentation

- [ ] CLAUDE.md § "G5a polymorphic wire" aggiornato per riflettere Block C completed removal

### Coverage

- [ ] Vitest coverage delta ≥ -2pp vs baseline `main-dev`

---

## Rollback path (Nygard §6)

**Scenario**: post-merge Block C, scoring non funziona durante live session.

**Diagnosis**:
1. Check browser console: `[Block C migration]` warnings (se aggiunti come safety net).
2. Check Sentry / Application Insights per errori in `useSessionScores` / `ScoringPanelRenderer`.
3. Check SignalR connection: `ScoringConfigured` events ricevuti? Verifica nel devtools Network → WS.

**Revert**:
1. `git revert <BlockC-merge-SHA>` su `main-dev`.
2. `git push origin main-dev`.
3. CI redeploy FE.
4. Client refresh ricostruisce state via REST hydration (`useLiveSession` query). Zero coordination con BE richiesta — store senza `persist` middleware, nessuno state survive refresh.

**Esimito post-revert**: scrivi RCA in `docs/superpowers/specs/2026-MM-DD-block-c-revert-rca.md`.

---

## Dependency graph (subagent execution)

```
T1 (leader migration) ──┐
                        ├─→ T3 (ScoreBoard refactor) ──┐
T2 (store cleanup) ─────┘                              ├─→ T7 (lint promotion) ─→ T10 (PR)
                                                       │
T4 (useSignalrSession) ────────────────────────────────┤
                                                       │
T5 (i18n keys) ──┐                                     │
                 ├─→ T6 (SessionLiveView refactor) ────┤
                 │                                     │
                 └─→ T8 (smoke E2E) ───────────────────┘
                                                       │
T9 (CLAUDE.md) ────────────────────────────────────────┘
```

**Parallelization opportunities**:
- Wave 1 (parallel): T1, T2, T5
- Wave 2 (parallel after Wave 1): T3 (needs T1+T2), T4 (independent), T6 (needs T5)
- Wave 3 (parallel after Wave 2): T8 (needs T3+T4+T6)
- Wave 4 (sequential): T7 → T9 → T10

**Subagent dispatch**: usa `feature-dev:code-explorer` + `feature-dev:code-architect` per Wave 1 in parallel, poi `coder` subagent per Wave 2, poi `tester` per Wave 3.

---

**Last updated**: 2026-06-19
**Status**: APPROVED FOR EXECUTION (post Q1/Q2/Q3 user lock-in)
