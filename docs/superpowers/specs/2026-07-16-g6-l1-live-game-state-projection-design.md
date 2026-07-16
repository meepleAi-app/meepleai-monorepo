# Design — G6 Epic · L1: Live game-state projection + streaming (generic)

**Track**: G6 per-game rich flavors — **Layer 1 of 3** (L1 generic mechanism → L2 per-game schemas → L3 rich flavors)
**Parent epic**: [#3025](https://github.com/meepleAi-app/meepleai-monorepo/issues/3025) — Per-game live game-state (backend layer for rich G6 session flavors)
**Date**: 2026-07-16
**Status**: design — awaiting user review before writing-plans
**Scope**: BE (.NET) + FE (Next.js), game-agnostic

---

## 1. Why this exists (the pivot)

Discovery (recon of the 6 remaining G6 mockups #2788–2793) showed every per-game flavor's rich UI (Catan hex board, Codenames word grid, Power Grid market, …) needs **game-specific live state that the `LiveSessionDto` does not carry**. Without it, the 6 MVP flavors collapse to the same themed leaderboard (Catan's) — low-value chrome. Decision: **build the backend live game-state layer first**, then rich flavors faithful to the mockups.

That layer decomposes into 3 independently-shippable pieces:
- **L1 (this spec)** — a **generic, game-agnostic** mechanism to write, persist, expose, and **stream** an opaque live game-state blob to `/sessions/[id]/live`.
- **L2** — per-game state **schemas** (the JSON shape each game writes) + per-game host-edit surfaces. One cycle per game.
- **L3** — per-game **rich flavor** components (FE) that parse L2 state + render faithfully to the mockups. One cycle per game.

L1 keeps the state **opaque** (free-form JSON). L2/L3 add per-game typing. This spec is L1 only.

## 2. Discovery (verified)

- **Container already exists**: `LiveGameSession.GameState` — a `JsonDocument?` (free-form, nullable, mutable) on the **live aggregate the shell loads**, with `UpdateGameState(JsonDocument?)` (`LiveGameSession.cs:828`, disposes prior doc to avoid ArrayPool starvation) + optimistic concurrency via Postgres `xmin`. Same `sessionId` throughout.
- **Separate snapshot model** (`GameSessionState` + `GameStateSnapshot`, #2403) is for **undo/history**, has its own REST endpoints, and is **NOT** the live state. L1 does **not** touch it.
- **Write path gap**: `LiveGameSession.GameState` is currently written **only** by `RestoreSessionSnapshotCommandHandler` — there is **no host-editing endpoint** for live play. (The vision `GET /game-state` route is AI-extraction, unrelated.)
- **Cardinal question answered**: `UpdateGameState` takes the JSON **from the caller** → **host/client-entered, no server game engine**. L3 flavors get host-edit surfaces (like the score editor). Moderate cost, not an engine.
- **Not exposed**: `LiveSessionDto` mapper (`QueryHandlers.cs` `MapToDto`) **ignores** `GameState`; the SSE stream context does not carry it.
- **Streaming infra is proven**: SSE `/live-sessions/{id}/stream` via `LiveSessionStreamForwarder` (`INotificationHandler<T>`) → `ILiveSessionStreamGateway.BroadcastAsync(sessionId, new LiveSessionStreamEvent("session:<type>", payload), ct)` + `SseEventTypeMapper`. The **Whiteboard** feature already streams live state exactly this way. Also a SignalR hub path exists.
- **FE gaps**: no `gameState` slice / `setGameState` in `useLiveSessionStore`; no `session:game-state` SSE consumer; `api.liveSessions` has no live game-state methods (only `getPhases`, tools, snapshots).

## 3. Locked decisions

| # | Decision |
|---|---|
| Container | **Reuse `LiveGameSession.GameState`** (not a new aggregate, not `GameSessionState` snapshots, not ToolState) |
| Schema | **Opaque `JsonDocument` / `unknown` at L1** — per-game typing is L2 |
| Write path | **Host/client-entered** generic PATCH (no engine); L1 adds the missing generic write command+endpoint |
| Streaming | Follow the proven `LiveSessionStreamForwarder` → `ILiveSessionStreamGateway` SSE pattern (event `session:game-state`) |
| Undo/history | Out of scope (the separate `GameStateSnapshot` model); L1 does not integrate it |

## 4. Architecture — the L1 vertical slice (write → expose → stream → FE)

### BE
1. **Write command** — `UpdateLiveGameStateCommand(Guid sessionId, JsonDocument state)` + handler: load `LiveGameSession`, `session.UpdateGameState(state)`, `AddAsync`/`UpdateAsync` + **`await _unitOfWork.SaveChangesAsync(ct)`** (per ADR-060), then the domain event dispatches **post-commit**. Authorization: host/participant only (mirror the score-update authz + IDOR guard). Opaque state — no schema validation at L1 (size cap only, e.g. reject > N KB).
2. **Endpoint** — `PATCH /api/v1/live-sessions/{id}/game-state` (CQRS: `IMediator.Send`), body `{ state: <json> }`. Follows the existing live-session endpoints file + auth pattern.
3. **Domain event** — `GameStateUpdatedDomainEvent(Guid sessionId, JsonDocument state)` (carries the new state so the forwarder needs no re-fetch) raised by `LiveGameSession.UpdateGameState` (or the handler), dispatched post-`SaveChanges`.
4. **Stream forwarder** — `INotificationHandler<GameStateUpdatedDomainEvent>` → `_gateway.BroadcastAsync(sessionId, new LiveSessionStreamEvent("session:game-state", payload), ct)`. Add `session:game-state` to `SseEventTypeMapper`. Payload = the current `GameState` JSON (full-document; delta/JSON-Patch is a future optimization, not L1).
5. **Expose on read** — add `GameState` (nullable `JsonDocument`) to `LiveSessionDto` + wire it in `MapToDto` (`QueryHandlers.cs`) so the initial `GET /sessions/[id]/live` hydration carries the current state.

### FE
6. **Schema** — add `gameState: z.unknown().nullable()` to `LiveSessionDtoSchema` (`live-sessions.schemas.ts`).
7. **Client + hooks** — `api.liveSessions.updateGameState(sessionId, state)` (PATCH) + optionally `getGameState`; a `useUpdateLiveGameState` mutation (host). Read hydrates from `useLiveSession` (the DTO now carries `gameState`).
8. **Store slice** — add `gameState: unknown | null` + `setGameState(next)` to `useLiveSessionStore` (mirror the `scoreData`/`setScoringConfig` slice + the `local/no-store-scores-direct`-style discipline).
9. **SSE consumer** — the existing live SSE consumer handles the new `session:game-state` event → `setGameState(payload)`. Initial value hydrated from the DTO; SSE keeps it live (REST-hydration race guard like the score path).

**Boundary**: L1 ships an **opaque** state end-to-end (a host can PATCH arbitrary JSON, spectators see it live in the store) but renders **nothing game-specific** — no flavor consumes it yet. A tiny dev-only "raw game-state" debug view (behind a flag) MAY be added to prove the pipe; production rendering is L3.

## 5. Out of scope (explicit)

- Per-game state **schemas** / typed contracts (L2).
- Per-game **rich flavor** rendering (L3).
- A server-side **game engine** / auto-progression (confirmed not the model — host-entered).
- **Undo/history** integration with `GameSessionState`/`GameStateSnapshot` (separate model).
- **Delta / JSON-Patch** streaming (L1 sends full document; optimize later if payloads grow).
- **Schema validation** of the state shape (opaque at L1; L2 validates per game).

## 6. Error / edge handling

- Update on a `Completed` session → `ConflictException` (409) (already enforced by `UpdateGameState`).
- Concurrency: `xmin` optimistic — on conflict return 409 (host retries with latest).
- Null/empty state → allowed (clears the state); FE renders nothing.
- Oversized payload → 413/400 with a size cap.
- SSE reconnect → the DTO re-hydration + Last-Event-ID resume already handle it; the store re-seeds from the DTO.
- IDOR: only session host/participants may PATCH (authz guard + test, mirroring the score IDOR fix).

## 7. Testing

- **BE unit**: `UpdateLiveGameStateCommandHandler` (success, completed→409, authz/IDOR, save-then-event ordering); `GameStateUpdatedDomainEvent` forwarder → gateway broadcast; `MapToDto` includes `GameState`.
- **BE integration** (Testcontainers): PATCH game-state → persisted → `GET /sessions/[id]/live` returns it; event → SSE broadcast (assert the forwarder fires).
- **FE unit**: schema parses `gameState`; store `setGameState`; the SSE consumer routes `session:game-state` → `setGameState`; `useUpdateLiveGameState` mutation.
- **FE E2E skeleton**: host PATCHes state → (mock SSE) store updates. (The rich rendering E2E is L3.)

## 8. Definition of Done (L1)

- [ ] `PATCH /live-sessions/{id}/game-state` writes `LiveGameSession.GameState` (host authz + IDOR + 409 on completed) with `SaveChangesAsync`.
- [ ] `GameStateUpdatedDomainEvent` streams as SSE `session:game-state` via the forwarder + `SseEventTypeMapper`.
- [ ] `GameState` exposed in `LiveSessionDto` + FE schema; hydrated on load.
- [ ] `useLiveSessionStore` `gameState` slice + `setGameState`; SSE consumer wired; `api.liveSessions` client + mutation.
- [ ] Opaque end-to-end (no game-specific rendering); tests green; 0 regressions; ADR-060 SaveChanges discipline honored.
- [ ] Epic checklist updated; L2 (per-game schemas) unblocked.

## 9. Risks / adapt-points (confirm in the plan)

1. **Exact SSE forwarder seam** — confirm `ILiveSessionStreamGateway` + `LiveSessionStreamEvent` signatures + the `SseEventTypeMapper` registration (copy the Whiteboard streaming handler).
2. **Domain-event dispatch** — confirm the post-commit dispatch path (ADR-060) and whether `LiveGameSession` already raises events elsewhere to mirror.
3. **Authz/IDOR** — reuse the score-update authorization + IDOR guard (the HIGH finding fixed in `c1efb4fb6`).
4. **Live-session endpoints file** — confirm the exact routing file for `/live-sessions/{id}/*` to add the PATCH route.
5. **SignalR vs SSE** — L1 uses SSE (the forwarder pattern); confirm whether the FE live consumer is SSE or SignalR for this session surface and wire the matching side.
