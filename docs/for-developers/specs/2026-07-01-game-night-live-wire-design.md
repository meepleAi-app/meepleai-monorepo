# Game Night Live wire-up — scoping & design

**Context**: prerequisite for **SI-2 (#2633)** (max-1-live badge + blocked modal). SI-2's trigger ("open a 2nd live Session") requires a working, BE-wired night-live session flow — but `NightLiveClientView` is currently **fixture-stage**.
**Date**: 2026-07-01
**Related**: #2632 (SI-1 done) · #2633 (SI-2, blocked) · #2647 (OpenLiveMode/#15 gap)
**Status**: PROPOSED — slice breakdown for review before implementation.

---

## 1. Current state

`apps/web/src/app/(authenticated)/game-nights/[id]/live/_components/NightLiveClientView.tsx` renders the rich `NightLiveHub` from **hardcoded fixtures** (header comment: *"TODO: replace with backend hook `useGameNightLive(id)`"*):

- `NIGHT` (title, shortTitle, nightCode)
- `PLANNED_GAMES[]` — per game: `status` (completed/inprogress/upcoming), `order`, `actual`/`estimated` time, `score`, `winner`, cover gradient, publisher, emoji
- `CURRENT_GAME` (the in-progress session)
- `DIARY_PLAYERS[]`, `DIARY_GAMES[]`, diary events
- `GameTransitionDialog` state

The BE-wired domain hook `useGameNightMultiSession` (start/complete/finalize + Zustand store) exists but has **zero consumers** — it is not mounted in this view.

## 2. The BE read-model gap

`GET /game-nights/{id}` → `GameNightDto` provides the **header + RSVP counts + `GameIds`** only:
`Id, OrganizerId, OrganizerName, Title, Description, ScheduledAt, Location, MaxPlayers, GameIds, Status, AcceptedCount, PendingCount, TotalInvited, CreatedAt, UpdatedAt`.

It does **not** expose the night's **sessions**. The domain `GameNightSession` (child of `GameNightEvent`) carries: `SessionId, GameId, GameTitle, PlayOrder, Status (Pending/InProgress/Completed/Skipped/Corrupted), WinnerId, StartedAt, CompletedAt`. That covers the view's `status`/`order`/`winner`/timing needs — but is **not surfaced in any DTO/endpoint**.

**Not in the domain at all** (fixture-only): per-game **score** string, **estimated** time, cover gradient, publisher, emoji. Decisions needed (§5).

## 3. Target

A `useGameNightLive(id)` hook feeding `NightLiveClientView` from the backend, replacing all fixtures. Once real, SI-2's max-1-live badge (on the InProgress session) + blocked modal (on the `409 MaxLiveSessionsExceeded` from `startNextGame`/gamebook-attach) mount naturally.

## 4. Slices

- **Slice A (BE read model)** — expose the night's sessions. Add `Sessions: List<GameNightSessionDto>` to `GameNightDto` (or a dedicated `GET /game-nights/{id}/live` → `GameNightLiveDto`). `GameNightSessionDto(SessionId, GameId, GameTitle, PlayOrder, Status, WinnerId, StartedAt, CompletedAt)` — a projection of the already-loaded `_sessions`. Effort **S–M** (DTO + mapper + endpoint/field + test). No new domain.
- **Slice B (FE hook + header + planned games)** — `useGameNightLive(id)` (React Query over `gameNightsClient` + Slice A). Map → `NightLiveHubNight` (title/code) + `PlannedGame[]` from sessions (status/order/winner/timing; game title from `GameTitle`). Replace `NIGHT` + `PLANNED_GAMES` fixtures. Effort **M**.
- **Slice C (current game + diary)** — derive `CURRENT_GAME` from the InProgress session; wire the diary via `gameNightSessionClient` diary endpoint + players from RSVPs. Replace remaining fixtures. Effort **M**.
- **Slice D (SI-2 mounts, #2633)** — `LiveSessionBadge` on the InProgress session; `useGameNightMultiSession` catches `409 ConflictError` from `startNextGame`/gamebook-attach → `maxLiveBlocked` state → `MaxLiveBlockedModal` ("questa serata ha già una sessione live"). `attachGamebookCampaign` client method (gamebook path). Effort **M**.

Threads: A → B → C are sequential (B/C consume A); D depends on B (the live view being real). The gamebook `attachGamebookCampaign` (D) reuses the Phase-2 endpoint `POST /game-nights/{id}/gamebook-sessions`.

## 5. Decisions to lock before implementing

- **D-READ**: extend `GameNightDto` with `Sessions` vs a dedicated `GET .../live` endpoint. *Recommend*: dedicated `GET /game-nights/{id}/live` → `GameNightLiveDto` (keeps the list DTO lean; the live view is a distinct read concern).
- **D-SCORE/TIME**: per-game score + estimated time are fixture-only, absent from the domain. *Recommend*: **drop** them from the live view for now (render status/winner/elapsed from `StartedAt`/`CompletedAt` only); re-introduce if/when a scoring/estimate model lands. Do NOT invent BE fields to match the mockup.
- **D-COSMETIC**: cover gradient/publisher/emoji — derive from the shared-game catalog (`GameIds`) if available, else a deterministic placeholder (mirrors the BGG-ban `cover-utils` pattern).

## 6. Recommendation

Land **Slice A** first (small BE read model, unblocks all FE slices), then B → C → D. SI-2 (#2633) is effectively **Slice D** and should be re-scoped in its issue to depend on Slices A–C. This wire-up is L+ overall and warrants its own focused implementation pass per slice.
