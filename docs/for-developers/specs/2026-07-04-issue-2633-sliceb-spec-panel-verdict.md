# #2633 Slice B — spec-panel verdict & implementation contract

**Issue**: [#2633](https://github.com/meepleAi-app/meepleai-monorepo/issues/2633) — SI-2 night-live wire-up, **Slice B** (FE hook + header + planned games)
**Parent**: [#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619) · **Thread A**
**Date**: 2026-07-04
**Method**: `/sc:spec-panel` critique (Fowler · Wiegers · Nygard · Adzic · Cockburn) → consolidated synthesis
**Predecessor**: [`2026-07-01-game-night-live-wire-design.md`](./2026-07-01-game-night-live-wire-design.md) (Slices A–D). Slice A (`GET /game-nights/{id}/live` → `GameNightLiveDto`) is **shipped & frozen** (PR #2652).
**Status**: RATIFIED 2026-07-04 — 15 locked decisions + 3 product calls confirmed by the issue owner.

---

## 1. Scope

Slice B replaces the `NIGHT` + `PLANNED_GAMES` **fixtures** in `NightLiveClientView.tsx` with a backend-driven
read path over the already-shipped `GET /api/v1/game-nights/{id}/live`. It is a **read-only projection** of the
night header + planned line-up + real session jump. It does **not** implement the LIVE badge / blocked modal
(Slice D = SI-2 proper), the currentGame + diary (Slice C), or any write/drive interaction.

## 2. Locked decisions (LD-1 … LD-15)

| ID | Decision |
|---|---|
| **LD-1** | One exported seam type `NightLiveViewModel` + one **pure** module fn `mapNightLiveToViewModel(dto, now: Date): NightLiveViewModel` in `apps/web/src/lib/game-nights/mapNightLive.ts`. No internal `Date.now()`/`new Date()`/`performance.now()`/`Math.random()`/module-level mutable state. Hook wires it via React Query `select`. |
| **LD-2** | Winner `{name,initials,color}` is **OUT of Slice B** (product-confirmed). Mapper carries `winnerId?: string` through untouched; card renders no winner chip. Resolution deferred to Slice C. Hook is single-endpoint (no `getRsvps`). |
| **LD-3** | Mapper emits Slice-C props as true empty contract: `currentGame: null`, `diaryEvents/diaryGames/diaryPlayers: []`. **Delete** `CURRENT_GAME` + `DIARY_*` module fixtures from `NightLiveClientView.tsx`. |
| **LD-4** | (a) `GameNightSessionStatusSchema = z.enum(['Pending','InProgress','Completed','Skipped','Corrupted'])` (all 5). (b) Pure `toPlannedGameStatus` exhaustive switch (TS `never` default). Table: Pending→`upcoming`, InProgress→`inprogress`, Completed→`completed`, **Skipped→`completed` (muted marker, excluded from `current`)**, Corrupted→`completed` (neutral fallback, never throws). Unknown 6th string fails at the parse boundary. |
| **LD-5** | `PlannedGame.id = SessionId` (a game may be played twice a night; SessionId is the stable diary/jump key). |
| **LD-6** | `total = dto.Sessions.length` (incl. Skipped/Corrupted). `current` = 1-based PlayOrder of the single InProgress session; if none, count of terminal sessions {Completed, Skipped, Corrupted} clamped to `[0, total]`. If >1 InProgress (racy read), pick lowest PlayOrder, never throw. |
| **LD-7** | `elapsed`/`actual` derived in B from timestamps vs injected `now`. Formats match fixtures: `elapsed = 'Hh Mm'` from earliest StartedAt→now; `actual = 'Nm'` (Completed: CompletedAt−StartedAt; InProgress: now−StartedAt; Pending/Skipped/Corrupted: `undefined`). `estimated`/`score` = `undefined` (no DTO source, D-SCORE/TIME). Hook seeds `now` via `useNow(60_000)` so the clock ticks; mapper stays pure. |
| **LD-8** | `status` = `'live'` if any InProgress else `'transition'`. No `'paused'` (no BE signal). `confirmedPlayers`/`totalPlayers` = `undefined` (no RSVP fetch). |
| **LD-9** | `cover` = deterministic 2-stop gradient from `hashToHue(GameId)` (`cover-utils`, BGG-ban compliant). `emoji` = `undefined` (no deterministic source; decorative/optional). `publisher` = `undefined`. No catalog fetch → mapper stays synchronous. |
| **LD-10** | Error taxonomy against real client shapes: `httpClient.get` returns `null` on 401 (`httpClient.ts:136`) → detect `null` **before** `.parse()`, throw typed `UnauthenticatedError` → login redirect. `NetworkError`/`CircuitBreakerError` by `error.name` → retryable "connection lost". `ApiError.statusCode` 403 → non-participant, 404 → not-found. Hook `retry: false`. |
| **LD-11** | Published night with 0 sessions = **happy-path 200**: `plannedGames: []`, `current: 0`, `total: 0`, `elapsed: '0h 0m'`, `status: 'transition'`, header from `dto.Title`. View renders a defined empty state (distinct from loading/error). |
| **LD-12** | Hook returns `UseQueryResult<NightLiveViewModel, Error>` (not a bespoke object); pure mapper via `select`. Follows `useGameNightConflictCheck` convention (`xxxKeys` + raw `UseQueryResult`). |
| **LD-13** | Pause/transition/skip/end drive-controls + `GameTransitionDialog` are **OUT of Slice B**: hidden/disabled so the read-only projection doesn't advertise faked interactions. |
| **LD-14** | Terminal/non-viewable nights (product-confirmed = **redirect + inline**): `Completed` → redirect to `/game-nights/{id}/summary`; `Cancelled` → explicit "serata annullata" state; `Draft` → not-live/empty. Only `Published` mounts the live hub. |
| **LD-15** | Field→source acceptance table (§4) is the pass/fail contract. "Done" = every row test-verified AND **no header/planned value remains a module-level constant** in the view. |

## 3. Product calls (confirmed 2026-07-04)

1. **Winner** → deferred to Slice C (LD-2). Wire-design updated.
2. **Terminal nights** → redirect Completed→`/summary` + inline Cancelled/Draft states, in Slice B (LD-14).
3. **Skipped** → muted tile kept in list, excluded from `current` (LD-4/LD-6).

## 4. Field → source acceptance table (LD-15)

| ViewModel field | Source |
|---|---|
| `night.title` | `dto.Title` |
| `night.shortTitle` / `night.nightCode` | `undefined` (Slice B) |
| `total` | `dto.Sessions.length` |
| `plannedGames[i].id` | `Sessions[i].SessionId` |
| `plannedGames[i].title` | `Sessions[i].GameTitle` |
| `plannedGames[i].status` | `toPlannedGameStatus(Sessions[i].Status)` (LD-4) |
| `plannedGames[i].order` | `Sessions[i].PlayOrder` |
| `plannedGames[i].actual` | derived from StartedAt/CompletedAt + `now` (LD-7) |
| `plannedGames[i].cover` | `hashToHue(Sessions[i].GameId)` gradient (LD-9) |
| `plannedGames[i].winnerId` | `Sessions[i].WinnerId` (carried; no chip) |
| `plannedGames[i].winner` / `emoji` / `publisher` / `estimated` / `score` | `undefined` (Slice B) |
| `current` | derived (LD-6) |
| `elapsed` | derived (LD-7) |
| `status` | derived (LD-8) |
| `confirmedPlayers` / `totalPlayers` | `undefined` (LD-8) |
| `currentGame` | `null` (LD-3) |
| `diaryEvents` / `diaryGames` / `diaryPlayers` | `[]` (LD-3) |

## 5. Acceptance criteria (Given/When/Then)

1. **Determinism**: Given any DTO + fixed `now`, `mapNightLiveToViewModel(dto, now)` called twice is deeply-equal; no clock/random/global read inside.
2. **Happy path**: Published night, 3 sessions (PlayOrder 1..3, Completed/InProgress/Pending) → `title=dto.Title`, `total=3`, `current=2`, 3 games in PlayOrder with status completed/inprogress/upcoming — all DTO-sourced, zero module constants left in the view.
3. **Real jump**: an InProgress/Completed game tapped → navigates to `/sessions/{SessionId}` (`PlannedGame.id === SessionId`).
4. **Enum totality/no-throw**: one `Skipped` + one `Corrupted(999)` session → Skipped→`completed` muted (excluded from `current`), Corrupted→`completed` neutral title, no exception, exhaustive `never` switch over all 5.
5. **Parse robustness**: 200 body with `Status='Corrupted'` parses OK (enum lists all 5); unknown 6th string throws at the boundary (not the mapper emitting `upcoming`).
6. **Elapsed/actual**: InProgress StartedAt 35m before `fixedNow` → `actual='35m'`; header `elapsed='Hh Mm'` from earliest StartedAt; a Completed row's `actual` is identical for ANY `now`; `estimated`/`score` undefined.
7. **Empty happy-path**: Published, 0 sessions → `plannedGames=[]`, `current=0`, `total=0`, `status='transition'`, header renders, defined empty state (not error).
8. **Winner deferred**: Completed session with WinnerId → `winnerId` carried, no winner chip, never throws.
9. **Error taxonomy**: 401 (null) → `UnauthenticatedError`→login redirect; 403 → non-participant copy; 404 → not-found copy; Network/CircuitBreaker → retryable "connection lost" — each asserted separately, never keyed on message string.
10. **Slice-C seam**: `currentGame=null` + `diary*=[]` from the mapper; no `CURRENT_GAME`/`DIARY_*` fixtures remain.
11. **Terminal nights**: Completed → not rendered live (redirect to `/summary`); Cancelled → cancelled state; Draft → not-live/empty; only Published mounts the hub.
12. **Loading**: pending query → skeleton, `NightLiveHub` not mounted with placeholder data.

## 6. Out of scope (Slice C/D / later)

Winner name/initials/color + participant-with-guests read model (C) · currentGame + diary arrays (C) · LIVE badge + blocked modal (D) · pause/transition/skip/end interactive behavior (later; disabled in B) · `confirmedPlayers`/`totalPlayers` real counts (later) · per-game score/estimated (not in domain, D-SCORE/TIME) · publisher & catalog cosmetics beyond `cover-utils` placeholder · `'paused'` status · any BE/DTO change (Slice A frozen).

## 7. File plan

- **new** `apps/web/src/lib/game-nights/mapNightLive.ts` (+ `.test.ts`) — `NightLiveViewModel`, `mapNightLiveToViewModel`, `toPlannedGameStatus`, time/cover helpers.
- **new** `apps/web/src/hooks/useNow.ts` (+ test) — interval clock, 60s default.
- **new** `apps/web/src/lib/game-nights/hooks/useGameNightLive.ts` (+ test) — RQ hook.
- **edit** `apps/web/src/lib/api/schemas/game-nights.schemas.ts` — `GameNightSessionStatusSchema`, `GameNightSessionDtoSchema`, `GameNightLiveDtoSchema`.
- **edit** `apps/web/src/lib/api/clients/gameNightsClient.ts` — `getLive(id)` (null→typed error), interface entry.
- **edit** `apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx` — consume hook, delete fixtures, loading/error/empty/terminal states, disable drive-controls.
