# Issue #2430 Block B+ — PolymorphicScoreEditor host swap + mutation wire

**Date**: 2026-06-19
**Branch**: `feature/issue-2430-editor-host-swap`
**Parent**: `main-dev`
**Effort**: ~3 days focused
**Author**: Aaron Degrassi
**Status**: Design approved — proceeding to implementation plan

---

## Context

Block B (PR [#2434](https://github.com/meepleAi-app/meepleai-monorepo/pull/2434), merged 2026-06-19) shipped the **read-only** polymorphic scoring panel in `SessionLiveView`. The mutable counterpart is gated by viewer role: only the host should be able to edit scores. Block B+ wires the `PolymorphicScoreEditor` (Asse D follow-up P1 #1899) into the score tab when `viewerRole === 'Host'`, plumbed through the `useUpdateSessionScores` mutation hook with debounced autosave and error feedback.

Spec-panel (Wiegers / Adzic / Fowler / Nygard / Crispin discussion mode, 2026-06-19) surfaced six design decisions (DEC-1..6) and three strategic questions (Q1..3). All locked via brainstorming on 2026-06-19.

The implementation plan is published separately at `docs/superpowers/plans/2026-06-19-issue-2430-editor-host-swap.md` (per skill `superpowers:writing-plans`).

## Scope

**In-scope**:
1. Extract a new component `ScoreTabContent` that encapsulates ALL polymorphic scoring logic currently inlined in `SessionLiveView` (Block B store selectors + REST hydration effect + memo + a11y placeholder) plus the new Block B+ additions (mutation wire + role-based mount + debounce + toast).
2. Role-based mount: `viewerRole === 'Host'` → `PolymorphicScoreEditor` with `onChange` wired to `useUpdateSessionScores`. `Player` and `Spectator` → existing `ScoringPanelRenderer` read-only path.
3. Optimistic UI: local override of `scoreData` while debounce pending. Rollback to store on 4xx.
4. Debounce: 500ms trailing-only + flush-on-unmount (no input loss).
5. Error handling matrix for 403 / 429 / 4xx validation / 5xx / network — each with explicit toast UX (sonner) and recovery path.
6. 429 rate limit: 30-second countdown UI with editor input disabled.
7. Hoist the inline `useDebouncedCallback` helper from `scores/page.tsx` to a shared lib module and extend it with `flush()` support.
8. Add 7 new i18n keys (Italian + English).
9. Unit + integration tests: 28 cases on `ScoreTabContent` + 2 smoke cases on `SessionLiveView`. Migrate 2 existing Block B a11y placeholder tests from `SessionLiveView.test.tsx` to `ScoreTabContent.test.tsx`.

**Out-of-scope (documented gaps with tracking issues)**:
- EndgameDialog polymorphic `finalScores` adapter — #2431.
- Real Objectives catalogue (replace `MVP_OBJECTIVES_CATALOGUE` placeholder) — #2432.
- Legacy `PUT /api/v1/game-sessions/{id}/participants/{playerId}/score` endpoint deprecation — #2433. Block B+ does NOT touch the `_handleScoreUpdate` dead-code callback in `SessionLiveView` (find via grep `_handleScoreUpdate` at implementation time — Block B may have shifted exact line numbers).
- Multi-pod SignalR fan-out backplane — #2256.
- Player+Points partial editor — declined per DEC-1 (see below). Only the host can edit.
- Conflict detection / optimistic locking — declined per DEC-3 (see below). Last-write-wins via SignalR broadcast is the accepted policy.
- Backend changes — `useUpdateSessionScores` already calls `PUT /api/v1/game-sessions/{id}/scores-polymorphic` (shipped Asse D follow-up P1 #1899). No BE work.

## Design Decisions

### DEC-1 — Role gating: Host=editor, Player+Spectator=renderer

Only `viewerRole === 'Host'` mounts the editor. `Player` and `Spectator` see the same `ScoringPanelRenderer` read-only view. The Block B `_handleScoreUpdate` callback in `SessionLiveView` (currently dead code referring to the legacy `PUT /participants/{id}/score` endpoint) is **not touched** by Block B+; it stays in place until its dedicated deprecation issue (#2433) ships after editor swap stabilizes.

Rationale: simplest 2-state mount, eliminates ambiguity around partial editors for Players. Player+Points carve-out was considered but rejected as scope creep — Block B+ stays focused on the editor mount; partial-edit semantics for non-host Players can be revisited later if requested.

### DEC-2 — Component extraction: `ScoreTabContent` extracted, single PR

Extract a new component `ScoreTabContent` in the same directory tree as `SessionLiveView` (`apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx`). The component owns ALL polymorphic scoring logic — Block B's existing logic plus Block B+'s additions. `SessionLiveView` mounts `<ScoreTabContent />` at both the desktop right column and mobile drawer score sites, passing only `sessionId`, `viewerRole`, `viewerId`, `players` (master list from `activeSession`), `labels`, and `className`.

Rationale: Fowler argued the `SessionLiveView` god-object (~1500 LOC after Block B) needs reduction before adding more logic. Extracting `ScoreTabContent` reduces `SessionLiveView` by ~80 LOC (moves 3 store selectors + REST hydration `useEffect` + `scoringPanelData` memo + 2 a11y placeholder JSX blocks) and centralizes the polymorphic flow into a single testable unit. Single PR (refactor + feat bundled) preferred over two PRs (refactor-then-feat) — the refactor is mechanically simple and the test scaffold covers both moves.

### DEC-3 — Race policy: last-write-wins via SignalR (accepted feature)

When the host edits and SignalR `ScoringConfigured` arrives concurrently (e.g., from another host tab or admin update), the server-side ordering wins. The editor uses optimistic UI for responsiveness, but on every successful mutation response the local override clears and the store (driven by SignalR) becomes authoritative.

Rationale: client-side optimistic locking (version compare, conflict detection) is significant complexity for marginal benefit at MVP scale (single host per session expected). Documented as accepted behavior. Concurrent multi-tab edits resolve to last-write-wins; users see SignalR broadcasts overwrite their pending local state on success.

### DEC-4 — Debounce: 500ms trailing + flush-on-unmount

The host's `onChange` callback is wrapped in a debounced version that fires the mutation after 500ms of input silence. On `ScoreTabContent` unmount (tab change, navigation, role transition), the pending debounced call flushes immediately, ensuring no input is lost.

Rationale: trailing-only debounce matches the existing `scores/page.tsx` autosave UX. Flush-on-unmount addresses Nygard's concern that mid-debounce unmount silently drops user input.

Implementation: the existing inline `useDebouncedCallback` helper in `scores/page.tsx` (lines 27-55 — verified by grep) does NOT have `flush()` support. Block B+ hoists this helper into a shared lib module `apps/web/src/lib/session-live/use-debounced-callback.ts` and extends it with a `flush()` method exposed via a tuple return `[debouncedFn, flush]`. The `scores/page.tsx` callsite migrates to the new shared helper (small additional refactor, ~5 LOC).

### DEC-5 — Rate limit (429): toast + disable + 30s countdown UI

On 429 response, the editor input is disabled for 30 seconds. A toast notifies the host of the rate limit, and an `aria-live="polite"` countdown next to the editor announces the remaining time. After 30s elapses, the input re-enables automatically.

Rationale: Nygard's anti-raffica argument. Without the disable + countdown, a user continuing to type after 429 generates another 429 every 500ms (debounce-fires-mutation-cascade), producing a toast raffica and excess server load. The countdown UI gives explicit affordance.

Backoff: fixed 30 seconds. If the BE returns a `Retry-After` header, future iterations can honor it; for MVP a fixed window matches the BE's current rate limiter policy.

### DEC-6 — Network/5xx error: toast + retry button

On `fetch` failure (`TypeError`, `AbortError`) or 5xx response, show a sticky toast (non-auto-dismiss) with a "Riprova" button that re-invokes the mutation with the last payload. Input stays enabled — the user can keep typing while the retry happens.

Rationale: explicit affordance without over-engineering. No retry queue with exponential backoff (that's the Maximum option, declined as overkill for MVP). User can decide when to retry.

## Architecture

### File changes summary

| Action | Path | LOC est. |
|--------|------|----------|
| NEW | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx` | ~200 |
| NEW | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/ScoreTabContent.test.tsx` | ~520 (28 test cases) |
| NEW | `apps/web/src/lib/session-live/use-debounced-callback.ts` | ~50 |
| NEW | `apps/web/src/lib/session-live/__tests__/use-debounced-callback.test.ts` | ~80 (5 test cases) |
| MOD | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | ~−80 LOC (remove Block B logic) + ~+30 LOC (2 mount points of `<ScoreTabContent />`) |
| MOD | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` | ~−40 LOC (migrate 2 a11y placeholder tests to ScoreTabContent.test.tsx) + ~+30 LOC (2 smoke tests for mount) |
| MOD | `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` | ~−30 LOC (remove inline `useDebouncedCallback`) + ~+5 LOC (import from new lib + adapt callsite to tuple return) |
| MOD | `apps/web/src/locales/it.json` | ~+7 lines (7 new keys under `pages.sessionLive.scoring`) |
| MOD | `apps/web/src/locales/en.json` | ~+7 lines (English translations) |
| MOD | `CLAUDE.md` | ~+1 line (Block B+ entry under "Session live shell (epic #2354)") |

Total: 4 new files, 5 modified files, ~870 LOC net (heavy on test code).

### `ScoreTabContent` contract

```typescript
// apps/web/src/app/(authenticated)/sessions/[id]/live/_components/ScoreTabContent.tsx

export interface ScoreTabContentProps {
  readonly sessionId: string;
  readonly viewerRole: 'Host' | 'Player' | 'Spectator';
  readonly viewerId: string;
  readonly players: ReadonlyArray<{
    readonly id: string;
    readonly name: string;
    readonly displayName?: string;
  }>;
  readonly labels: ScoringPanelRendererLabels;
  readonly className?: string;
}

export function ScoreTabContent(props: ScoreTabContentProps): React.ReactElement;
```

**Behavior contract**:
- Reads `scoringType`, `scoreData`, `setScoringConfig` from `useLiveSessionStore` via direct selectors.
- Runs REST hydration `useEffect` ONLY on first mount (race guard via `getState().scoringType != null`).
- Computes `scoringPanelData` via `mapScoreDataToPanelData` (read-only path).
- Maintains `localScoreOverride: ScoreDataByType[ScoreType] | null` for optimistic UI during pending debounce.
- Maintains `rateLimitDeadline: number | null` (timestamp). When set, editor `disabled={true}`. `useEffect` decrement counter; on reach 0, clears deadline.
- Mounts editor or renderer based on role:
  - `viewerRole === 'Host'` AND `scoringType !== null` → `<PolymorphicScoreEditor>` with `onChange={handleScoreChange}`, `disabled={isRateLimited || mutation.isPending}`, `availableObjectives={MVP_OBJECTIVES_CATALOGUE}` (only consumed when `scoringType === 'Objectives'`).
  - Otherwise + `scoringPanelData !== null` → `<ScoringPanelRenderer>` (Block B path).
  - Otherwise → a11y placeholder (`role="status" aria-live="polite"` with `pages.sessionLive.scoring.loadingLabel`).
- Toast handling via `sonner.toast.*` calls keyed by deterministic ID per error class (no stacking on rapid repeats).

### Data flow

```
User keystroke
  → PolymorphicScoreEditor.onChange({scoringType, data})
  → ScoreTabContent.handleScoreChange:
      setLocalScoreOverride(data)                       // optimistic UI
      debouncedMutate({scoringType, data})              // 500ms timer
  ...500ms silence...
  → debouncedMutate flushes:
      useUpdateSessionScores.mutate({sessionId, scoringType, scoreData: data})
        → PUT /api/v1/game-sessions/{id}/scores-polymorphic
        → onSuccess: localScoreOverride cleared (SignalR will update store)
        → onError: switch by err.kind
                   'forbidden' → toast + editor disabled
                   'rate-limited' → toast + setRateLimitDeadline(Date.now() + 30000)
                   'validation' → toast with details
                   'server' | 'network' → toast + retry button
        → localScoreOverride cleared in all error branches (rollback)
        
ScoreTabContent unmount:
  → useEffect cleanup: flush() invokes pending debouncedMutate immediately
```

### `useDebouncedCallback` hoisted contract

```typescript
// apps/web/src/lib/session-live/use-debounced-callback.ts

export function useDebouncedCallback<TArgs extends readonly unknown[]>(
  callback: (...args: TArgs) => void,
  delay: number
): readonly [
  debouncedFn: (...args: TArgs) => void,
  flush: () => void,
];
```

**Behavior**:
- Returns a tuple `[debouncedFn, flush]`.
- `debouncedFn(...args)` schedules `callback(...args)` after `delay` ms of silence. Subsequent calls reset the timer.
- `flush()` invokes the pending call immediately (if any) and clears the timer. No-op if no pending call.
- Cleanup on unmount clears the timer (does NOT auto-flush — flush is opt-in by caller via the returned `flush()` ref).

**Migration**: `scores/page.tsx` callsite currently uses single-value return; updates to destructure `const [debouncedSave] = useDebouncedCallback(..., 500);` (ignoring `flush` since the existing page-level callsite doesn't need it).

### `ScoreTabContent` integration in `SessionLiveView`

Desktop right column (replaces the existing Block B conditional with both branches):
```tsx
{tab === 'score' && (
  <ScoreTabContent
    sessionId={sessionId ?? ''}
    viewerRole={activeSession.viewerRole}
    viewerId={activeSession.viewerId}
    players={activeSession.players}
    labels={scoringPanelLabels}
    className="p-3"
  />
)}
```

Mobile drawer score case (replaces the existing Block B conditional):
```tsx
case 'score':
default:
  return (
    <ScoreTabContent
      sessionId={sessionId ?? ''}
      viewerRole={activeSession.viewerRole}
      viewerId={activeSession.viewerId}
      players={activeSession.players}
      labels={scoringPanelLabels}
      className="p-2"
    />
  );
```

Block B logic removed from `SessionLiveView`:
- `useLiveSessionStore` selectors for `scoringType`, `scoreData`, `setScoringConfig` (moved to `ScoreTabContent`).
- REST hydration `useEffect` (moved).
- `scoringPanelData` `useMemo` (moved).
- A11y placeholder JSX in both mount sites (encapsulated inside `ScoreTabContent`).
- Imports of `mapScoreDataToPanelData`, `MVP_OBJECTIVES_CATALOGUE`, `ScoreDataByType`, `ScoreType` (moved).

### i18n keys (new)

```json
{
  "scoring": {
    "loadingLabel": "Caricamento punteggi…",  // existing from Block B
    "forbiddenToast": "Permesso negato: solo l'host può modificare i punteggi",
    "rateLimitedTemplate": "Limite raggiunto, riprova tra {seconds}s",
    "rateLimitedToast": "Hai aggiornato i punteggi troppo velocemente. Aspetta {seconds}s.",
    "validationFailedTemplate": "Validazione fallita: {message}",
    "serverErrorToast": "Errore server, riprova",
    "networkErrorToast": "Connessione persa, riprova",
    "retryCta": "Riprova"
  }
}
```

English mirror: "Permission denied: only the host can edit scores" / "Rate limit reached, retry in {seconds}s" / etc.

### Error handling matrix

| Trigger | Toast (sonner) | Editor state | Recovery |
|---------|----------------|--------------|----------|
| 403 Forbidden | `toast.error('Permesso negato…', { id: 'score-403' })`, auto-dismiss 5s | `disabled={true}` permanent until tab unmount | Manual: leave score tab + come back, role re-evaluated |
| 429 Rate Limit | `toast.warning('Limite raggiunto, riprova tra 30s', { id: 'score-429' })`, auto-dismiss 5s | `disabled={true}` for 30s + countdown UI | Auto: input re-enabled after 30s |
| 400 Validation | `toast.error('Validazione fallita: {detail}', { id: 'score-400' })`, auto-dismiss 5s | enabled (user can correct) | User edits → next debounce attempt |
| 5xx Server | `toast.error('Errore server', { action: { label: 'Riprova', onClick: retry }, duration: Infinity })` | enabled | Retry button or natural next edit |
| Network (TypeError/AbortError) | `toast.error('Connessione persa', { action: { label: 'Riprova', onClick: retry }, duration: Infinity })` | enabled | Retry button or natural next edit |

`retry()` closure captures the last submitted payload. Multiple retries safe (idempotent mutation per Block A semantics: last-write-wins).

## Testing

### Unit/integration tests: `ScoreTabContent.test.tsx`

28 cases organized in 6 groups:

| Group | Cases | Notes |
|-------|-------|-------|
| Role gating | 3 | Host → editor mounted, Player → renderer, Spectator → renderer |
| Role transition | 1 | viewerRole 'Host' → 'Player' mid-session → editor unmounts, pending debounce flushes |
| Null gate | 2 | scoringType null + Host → a11y placeholder (NOT editor); scoringType null + Player → placeholder |
| REST hydration | 2 | DTO carries scoringType+scoreData → setScoringConfig; race-ordering (SignalR first, REST no-op) |
| Variant editor mount | 4 | Per ScoreType: editor receives correct prop (data.scores / data.results / data.positions / data.completedByPlayer + availableObjectives) |
| Debounce + mutation | 5 | Single edit → 500ms → mutate once; rapid edits → only last; unmount → flush; multi-player batch into single mutation; success → localScoreOverride cleared |
| Error handling | 8 | 403, 429 (toast + disable + countdown start), 429 countdown reach 0, 5xx + retry button, retry button click, network error, 400 validation, 429 toast dedup |
| Optimistic UI | 3 | typing → localOverride set; effectiveScoreData reflects override; success → cleared |

Mock pattern:
```typescript
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

beforeEach(() => {
  useLiveSessionStore.getState().reset();
});

it('mounts editor when viewerRole=Host', () => {
  act(() => {
    useLiveSessionStore.getState().setScoringConfig({
      scoringType: 'Points',
      scoreData: { scores: [{ playerId: 'p1', points: 10 }] },
    });
  });
  render(
    <ScoreTabContent
      sessionId="s1"
      viewerRole="Host"
      viewerId="u1"
      players={[{ id: 'p1', name: 'Marco' }]}
      labels={MOCK_LABELS}
    />,
    { wrapper: IntlProvider }
  );
  expect(document.querySelector('[data-slot="polymorphic-score-editor"]')).not.toBeNull();
});
```

### Unit tests: `use-debounced-callback.test.ts`

5 cases:
- Single call → callback fires after delay
- Multiple calls within delay → only last fires
- `flush()` invokes pending immediately
- `flush()` no-op when nothing pending
- Unmount → timer cleared but does NOT auto-flush

### Regression: `SessionLiveView.test.tsx` extension

Existing 78 tests:
- 67 untouched → continue green.
- 11 Block B tests:
  - 2 a11y placeholder tests (`renders aria-live placeholder when scoringType is null`, `placeholder shows the localized loading label text`) → **migrate** to `ScoreTabContent.test.tsx` (same assertions, ScoreTabContent context).
  - 5 hydration tests → stay in `SessionLiveView.test.tsx` (the effect now lives in `ScoreTabContent`, but the smoke test asserting that `ScoreTabContent` is mounted with the right props covers the wiring — direct assertion of store state still works because the test mounts the full `SessionLiveView` tree).
  - 4 variant mount tests → stay (same reasoning).
- T4.11 G5a regression-pin → stay (still passes because store seed flows through `ScoreTabContent` to renderer).

2 new smoke tests added:
- `renders ScoreTabContent inside score tab when viewerRole=Host` (assert mount, props passed correctly).
- `renders ScoreTabContent with viewerRole='Player' shows renderer not editor` (assert mount, role propagation).

Final SessionLiveView test count: 67 + 5 hydration + 4 variant + 1 G5a + 2 smoke = **79 tests** (was 78; migration −2, additions +3).

### Existing test files NOT modified

- `score-data-to-panel-data.test.ts` (Block B adapter, 16 tests) — invariato.
- `PolymorphicScoreEditor.test.tsx` (Asse D P1) — invariato.

## Task breakdown

The implementation plan defines TDD task commits. See `docs/superpowers/plans/2026-06-19-issue-2430-editor-host-swap.md` for the executable plan with per-task acceptance criteria.

High-level sequence (preview):

| Task | Type | Description |
|------|------|-------------|
| T1 | PREP | Verify sonner availability (already in deps ^2.0.7) — no install needed |
| T2 | REFACT | Hoist `useDebouncedCallback` to `lib/session-live/use-debounced-callback.ts` with `flush()` tuple return; update `scores/page.tsx` callsite |
| T3 | RED | `use-debounced-callback.test.ts` (5 tests RED) |
| T4 | GREEN | Implement hoisted helper, all 5 tests GREEN |
| T5 | RED | `ScoreTabContent.test.tsx` scaffold (28 tests RED, no impl yet) |
| T6 | GREEN | Implement `ScoreTabContent` extracting Block B logic + role mount + Block B+ additions (mutation wire + debounce + optimistic UI + error matrix) |
| T7 | REFACT | Update `SessionLiveView`: remove Block B logic, mount `<ScoreTabContent />` in 2 sites, migrate 2 a11y tests, add 2 smoke tests |
| T8 | i18n | Add 7 new i18n keys to it.json + en.json |
| T9 | QA | typecheck + lint sweep + targeted regression run (`pnpm test SessionLiveView score-data ScoreTabContent use-debounced`) |
| T10 | DOC | Update CLAUDE.md "Session live shell (epic #2354)" with Block B+ entry |
| T11 | PR | Push + open PR target `main-dev`, link #2430, follow-up issues already filed |

## Follow-up

(All already filed; Block B+ does not create new ones unless implementation surfaces a new gap.)

- **#2431** EndgameDialog polymorphic `finalScores` adapter — naturally follows from the polymorphic flow.
- **#2432** Real Objectives catalogue — replace `MVP_OBJECTIVES_CATALOGUE`.
- **#2433** Legacy `PUT /participants/{id}/score` endpoint deprecation — depends on Block B+ shipping (this issue).
- **#2256** Multi-pod SignalR fan-out backplane — Phase F.

## Risks

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `sonner` not actually in deps despite package.json grep — verify with import smoke test | Low | T1 pre-check; fallback to existing toast lib if missing |
| `useUpdateSessionScores` mutation hook doesn't expose 429 / network kinds — only forbidden/validation/server | Med | Wrap hook in ScoreTabContent error mapper; map `kind === 'server'` + check `status === 429` → 'rate-limited'; map fetch failures → 'network' |
| ScoreTabContent unmount race with mutation in-flight (e.g., user tab change after debounce flushed but before BE responds) | Med | Mutation continues to fire in background; SignalR will eventually update store. No abort needed (mutation is idempotent per Block A semantics). |
| Optimistic state stuck if SignalR broadcast fails to arrive | Low | localScoreOverride cleared on mutation success regardless of SignalR; store update is best-effort |
| Player typing in disabled editor confused by no visual cue | Low | `disabled` prop on editor + `aria-disabled="true"` + visual cue (existing `PolymorphicScoreEditor` Asse D P1 patterns) |
| Block B 11 existing tests break on placeholder migration | Med | Run targeted SessionLiveView test suite after T7 refactor; 2 migrated tests + 2 new smoke = net +1 test |
| Existing `scores/page.tsx` callsite breaks after debounce hoist | Low | T2 updates callsite to new tuple return; targeted test on the page or manual smoke verifies |
| Toast dedup via sonner id fails if id collision with other usages in app | Low | Use deterministic id prefix `score-{status}` ensuring uniqueness |
| ESLint rule `local/no-store-scores-direct` flags new selectors | None | Rule targets only `s.scores`, not `s.scoringType` / `s.scoreData` (verified in Block B) |

## Acceptance criteria

**Functional**:
- [ ] `ScoreTabContent` component shipped with 28 passing tests, encapsulating Block B logic + Block B+ additions.
- [ ] `viewerRole === 'Host'` mounts `PolymorphicScoreEditor`; other roles mount `ScoringPanelRenderer`; null `scoringType` shows a11y placeholder for all roles.
- [ ] `useUpdateSessionScores` mutation wired via `onChange` callback, debounced 500ms trailing + flush-on-unmount.
- [ ] Optimistic UI: `localScoreOverride` reflects user input until mutation success/error, then clears.
- [ ] 403 → toast + freeze input; 429 → toast + disable + 30s countdown; 5xx/network → toast + retry button; 400 → toast with details, no disable.
- [ ] `useDebouncedCallback` helper hoisted to `lib/session-live/`, returns `[fn, flush]` tuple, `scores/page.tsx` migrated.
- [ ] 7 new i18n keys shipped in it.json + en.json.

**Tests**:
- [ ] `ScoreTabContent.test.tsx` → 28/28 green.
- [ ] `use-debounced-callback.test.ts` → 5/5 green.
- [ ] `SessionLiveView.test.tsx` → 79/79 green (67 untouched + 5 hydration + 4 variant + 1 G5a regression + 2 new smoke).
- [ ] `score-data-to-panel-data.test.ts` → 16/16 green (untouched).
- [ ] `PolymorphicScoreEditor.test.tsx` → all green (untouched).

**Definition of Done** (process):
- [ ] `pnpm typecheck` and `pnpm lint` clean (no new errors or warnings).
- [ ] PR opened to `main-dev` with title `feat(session-live): #2430 Block B+ — PolymorphicScoreEditor host swap + mutation wire`.
- [ ] PR body links the 3 already-tracked follow-up issues (#2431, #2432, #2433).
- [ ] CLAUDE.md "Session live shell (epic #2354)" updated with Block B+ entry.
