# Game Night Improvvisata — Phase 2: Backend Integration E2E

**Date**: 2026-03-21
**Phase**: 2 of 5 (from completion roadmap)
**Scope**: Backend integration tests with real services
**Prerequisites**: API + PostgreSQL + Redis + Embedding Service + OpenRouter

---

## Overview

Phase 2 extends the existing integration test suite with improvvisata-specific flows tested against real backend services (including AI). Tests verify the complete journey: save/resume with snapshots, AI-powered score parsing, structured dispute resolution, data integrity, and concurrent access.

## Key API Conventions

- **Session creation (improvvisata)**: `POST /api/v1/game-night/start-session` requires `{ privateGameId: Guid }` — NOT a `gameName`. A `PrivateGame` must exist first (via BGG import or direct creation).
- **Session creation (generic)**: `POST /api/v1/live-sessions` accepts `{ gameName, visibility, scoringDimensions, agentMode }`.
- **Player IDs vs Participant IDs**: `POST /live-sessions/{id}/players` returns `LiveSessionPlayer.Id` (player ID). `POST /live-sessions/join` returns `ParticipantId` (invite participant). These are different entities — scores use player IDs, proposals use participant IDs.
- **Score confidence**: Wire format is `float` in `[0.0, 1.0]`. Threshold for auto-record is `≥ 0.8` (not `≥ 80`).
- **Dispute v2 vs AI Arbitro**: Two separate dispute systems:
  - **AI Arbitro** (improvvisata): `POST /game-night/sessions/{id}/disputes` — body: `{ description: string, raisedByPlayerName: string }` → returns AI verdict directly
  - **Dispute v2** (structured): `POST /live-sessions/{id}/disputes` — body: `{ initiatorPlayerId: Guid, initiatorClaim: string }` → creates dispute for voting flow
- **Snapshot index**: Zero-based integer for `GET /sessions/{id}/snapshots/{index}`

## Files

All files in `apps/web/e2e/integration/`.

### 1. Shared Helpers — `helpers/improvvisata-helpers.ts`

Extracted helper module eliminating ~30 lines of boilerplate per test file.

**Exports**:
- `loginAsAdmin(playwright)` → authenticated `APIRequestContext`
- `createGenericSession(api, gameName, options?)` → creates via `POST /live-sessions` with `{ gameName, visibility, scoringDimensions, agentMode }`, returns session ID (bare UUID). Used for tests that don't need the full improvvisata flow.
- `createImprovvisataSession(api)` → creates via BGG import (`POST /game-night/import-bgg`) then `POST /game-night/start-session` with the resulting `privateGameId`. Returns `{ sessionId, inviteCode, shareLink }`.
- `addPlayersToSession(api, sessionId, players[])` → calls `POST /live-sessions/{id}/players` for each player (always this endpoint, regardless of session creation method). Returns array of player IDs.
- `startSession(api, sessionId)` → `POST /live-sessions/{id}/start`
- `cleanupSession(api, sessionId)` → `POST /live-sessions/{id}/complete` (swallows errors)
- Constants: `API_BASE`, `ADMIN_EMAIL`, `ADMIN_PASSWORD` from env vars

### 2. Save/Resume — `improvvisata-save-resume.spec.ts`

Tests (serial, 3 total):

1. **Save session creates PauseSnapshot** — Create session via generic helper → add players → start → record scores → `POST /api/v1/game-night/sessions/{id}/save` with `{ finalPhotoIds: [] }` → verify status=Paused via `GET /live-sessions/{id}` → verify snapshot exists via `GET /api/v1/sessions/{id}/snapshots`
2. **Resume restores state with recap** — Resume via `POST /api/v1/game-night/sessions/{id}/resume` (empty body) → verify response contains `{ sessionId, inviteCode, shareLink, agentRecap }` directly in the 200 body → verify session status=InProgress via `GET /live-sessions/{id}` → optionally also call `GET /api/v1/live-sessions/{id}/resume-context` as supplementary check
3. **Multiple save/resume cycles** — Save → resume → add score → save → resume → verify all data persists across cycles, each resume returns fresh `inviteCode`

### 3. Score Parsing — `improvvisata-score-parsing.spec.ts`

Tests (serial, 4 total):

1. **Parse natural language score** — Session in progress → `POST /api/v1/live-sessions/{id}/scores/parse` with `{ message: "Marco ha fatto 15 punti" }` → verify response contains `playerId`, `value`, `confidence` (float 0.0–1.0)
2. **High confidence auto-records** — Score with confidence `≥ 0.8` → verify score recorded automatically via `GET /api/v1/live-sessions/{id}/scores`
3. **Low confidence requires confirmation** — Ambiguous input → verify `requiresConfirmation: true` in response → `POST /api/v1/live-sessions/{id}/scores/confirm` with `{ targetPlayerId, round, dimension, value }` → verify score recorded
4. **Invalid input graceful fallback** — Incomprehensible input → verify structured error response, no score recorded

### 4. Disputes — `improvvisata-disputes.spec.ts`

Tests two dispute systems (serial, 6 total):

**AI Arbitro (improvvisata-specific, 1 test):**
1. **AI arbitro verdict** — `POST /api/v1/game-night/sessions/{id}/disputes` with `{ description: "Can I build on an opponent's road?", raisedByPlayerName: "Marco" }` → verify response contains `{ id, verdict, ruleReferences, note }`

**Dispute v2 (structured flow, 5 tests):**
2. **Open structured dispute** — `POST /api/v1/live-sessions/{sessionId}/disputes` with `{ initiatorPlayerId: Guid, initiatorClaim: "I claimed the longest road first" }` → verify dispute created, returns dispute ID
3. **Respondent counter-claim** — `PUT /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/respond` with `{ respondentPlayerId: Guid, respondentClaim: "My road was longer" }` → verify counter-claim registered
4. **Cast votes** — Each player votes via `POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/vote` with `{ playerId: Guid, acceptsVerdict: bool }` → verify votes registered
5. **Tally and resolve** — `POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/tally` → verify final verdict, dispute status closed
6. **Timeout fallback** — Open dispute, no response → `POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/timeout` → verify timeout handling

### 5. Data Integrity — `improvvisata-data-integrity.spec.ts`

Tests (serial — one complete journey with intermediate checks, 6 total):

1. **Session state transitions are consistent** — Created → InProgress → Paused → InProgress → Completed. At each step, GET full session and verify temporal fields (`createdAt`, `startedAt`, `pausedAt`) populate correctly and don't regress.
2. **Scores persist across save/resume** — Record 3 scores for 2 players → save → resume → GET scores → verify count, values, round, dimension all identical. No lost or duplicated scores.
3. **Snapshot state is complete** — After save, `GET /api/v1/sessions/{id}/snapshots` → verify snapshot list has entries. Then `GET /api/v1/sessions/{id}/snapshots/0` (zero-based index) → full state reconstruction, compare with session state pre-pause.
4. **Player soft-delete doesn't corrupt scores** — Add 3 players via `POST /live-sessions/{id}/players`, record scores for all, remove one via `DELETE /live-sessions/{id}/players/{playerId}` → verify: player `isActive=false`, removed player's scores still present in total, other players intact.
5. **Invite token lifecycle** — `POST /live-sessions/{id}/invite` with `{ maxUses: 10, expiryMinutes: 30 }` → verify response `{ pin, linkToken, expiresAt, maxUses }`, PIN is 6 chars no ambiguous chars. Join with PIN via `POST /live-sessions/join` with `{ token: pin, guestName: "Guest" }` → verify `{ participantId, sessionId, connectionToken, displayName, role }`. After resume (which generates fresh invite), verify new invite code differs from old.
6. **Dispute resolution doesn't affect session state** — Open dispute v2 during active session → vote → tally → verify: session status still `InProgress`, scores unchanged, dispute history accessible via `GET /api/v1/games/{gameId}/dispute-history`.

### 6. Concurrent Access — `improvvisata-concurrent.spec.ts`

Two separate `APIRequestContext` instances simulating host and guest operating simultaneously.

Tests (6 total):

1. **Simultaneous score recording** — Host and guest record score in same round via `Promise.all` on `POST /api/v1/live-sessions/{id}/scores` → verify both scores persist, no conflict, correct total.
2. **Score proposal + confirmation flow** — Guest proposes score via `POST /api/v1/live-sessions/{sessionId}/scores/propose` with `{ participantId, targetPlayerId, round, dimension, value, proposerName }` (202 Accepted) → Host confirms via `POST /api/v1/live-sessions/{sessionId}/scores/confirm` with `{ targetPlayerId, round, dimension, value }` (204 No Content) → verify score recorded.
3. **Concurrent dispute voting** — Open dispute v2 with 3+ players, all vote in parallel (`Promise.all` on 3 `POST /api/v1/live-sessions/{sessionId}/disputes/{disputeId}/vote`) → tally → verify all votes counted, coherent verdict.
4. **Save while guest is scoring** — Guest sends score request, host calls save simultaneously → verify: either score is included in snapshot, or session is paused and score fails with appropriate error (no silent data loss).
5. **Join while session is active** — Session in progress, new guest joins via PIN (`POST /api/v1/live-sessions/join`) → verify: participant added without interrupting session, participant list updated via `GET /live-sessions/{id}/participants`.
6. **Optimistic concurrency on score edit** — Host and guest edit same score entry simultaneously via `PUT /api/v1/live-sessions/{id}/scores` → verify: one succeeds (204), other receives conflict error (409 from `DbUpdateConcurrencyException` via exception middleware, or 500 if middleware doesn't map it — test accepts either and logs which), no data corruption.

Note: Concurrent tests use `Promise.all` on parallel API calls, not SignalR/WebSocket clients.

## Test Count Summary

| File | Tests |
|------|-------|
| `improvvisata-save-resume.spec.ts` | 3 |
| `improvvisata-score-parsing.spec.ts` | 4 |
| `improvvisata-disputes.spec.ts` | 6 |
| `improvvisata-data-integrity.spec.ts` | 6 |
| `improvvisata-concurrent.spec.ts` | 6 |
| **Total** | **25** |

## Conventions

- All files in `apps/web/e2e/integration/`
- Shared helpers in `apps/web/e2e/integration/helpers/improvvisata-helpers.ts`
- Each file has cleanup in `afterEach`/`afterAll`
- Tests serial within each file, files independent of each other
- Both improvvisata endpoints (`/game-night/sessions/`) and generic (`/live-sessions/`) tested
- Real AI services used (OpenRouter, embedding service) — no mocks
- Full endpoint paths always include `/api/v1/` prefix

## Runtime Requirements

- API running on `localhost:8080` (or `PLAYWRIGHT_API_BASE`)
- PostgreSQL + Redis running
- Embedding service running (for agent context)
- OpenRouter API key configured (for score parsing, dispute resolution)
- Admin user seeded with credentials matching env vars
