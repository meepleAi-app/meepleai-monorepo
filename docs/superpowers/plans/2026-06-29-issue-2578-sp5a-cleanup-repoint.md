# SP5-a — Cleanup & repoint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Checkbox (`- [ ]`) steps.

**Goal:** Repoint the 3 FE toolkit hooks to the native live-session stream (BE already publishes their events), remove the dead-code `ChatSessionId`/`SetAgentMode` (ADR-083 SP0), and verify the FE write paths are not fail-silently.

**Architecture:** FE-mostly. The native `/api/v1/live-sessions/{id}/stream` (SP2) reuses `SessionBroadcastService` keyed on the companion `TrackingSessionId`; toolkit/whiteboard/timer events already publish to `ISessionBroadcastService` + map in `SseEventTypeMapper`, so the hooks just need the URL swapped (no BE forwarder work). `ChatSessionId` is a confirmed dead-code false-bridge to KnowledgeBase.

**Tech Stack:** Next.js + TS (Vitest); .NET 9 (xUnit).

## Global Constraints
- **No worktree**: commit directly on `feature/issue-2578-sp5a-cleanup-repoint`.
- Scoping reference: `.superpowers/sdd/sp5-scoping-brief.md`.
- toolkit-repoint: VERIFY each hook discriminates by **payload fields**, not event-name, and the native stream's envelope (`SseEventEnvelope`) is compatible with what the hook parses — before swapping. The legacy `/stream/v2` endpoint stays live (deprecated headers already added in SP2 T11); only the FE consumers move.
- ChatSessionId removal must be **systematic across all 5 layers** (domain, mapper, BE DTO, FE Zod schema, tests) — a dangling reference breaks build/typecheck.

---

## Task 1: Repoint the 3 toolkit hooks to the native stream
**Files:** `apps/web/src/lib/domain-hooks/useTurnOrder.ts`, `useWidgetSync.ts`, `useWhiteboardTool.ts` (+ their tests).

- [ ] **Step 1: Investigate** each hook: which event types it consumes (`session:toolkit`/`session:whiteboard`/`session:timer`), and whether it keys off the event **name** or payload **fields**. Confirm (read `SseEventTypeMapper` + the native `/live-sessions/{id}/stream` envelope) that those events flow to the companion channel the native stream reads, so repointing is event-compatible. If a hook keys off something the native stream doesn't carry, STOP and report (scope grew).
- [ ] **Step 2: Update the tests first** (test-first): change the hardcoded `/api/v1/game-sessions/{id}/stream/v2` assertion to `/api/v1/live-sessions/{id}/stream` for each hook; add a "never the legacy route" assertion. → RED.
- [ ] **Step 3: Repoint** the 3 hook URLs. Remove the `// SP2: stays on legacy ... see #2561 / SP5` comments (now resolved).
- [ ] **Step 4: GREEN** — `pnpm test useTurnOrder useWidgetSync useWhiteboardTool` + `pnpm typecheck`.
- [ ] **Step 5: Commit** — `feat(session-live): #2578 SP5-a repoint toolkit hooks to native stream`

---

## Task 2: Remove dead-code `ChatSessionId` / `SetAgentMode`
**Files (verify each):** `apps/api/src/Api/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs` (the `ChatSessionId` property + `SetAgentMode` method, ~:65/:790-803), the LiveGameSession mapper/EF config, the BE `LiveSessionDto`/Contracts, the FE Zod schema (`live-sessions.schemas.ts` or similar), and all tests referencing them.

- [ ] **Step 1: Find all references** — grep `ChatSessionId` and `SetAgentMode` across BE + FE. Confirm `SetAgentMode` is only called by tests (dead in production) per ADR-083 SP0.
- [ ] **Step 2: Remove** systematically: domain property + method, mapper line, DTO field, FE schema field, and the tests that exercised them. If `SetAgentMode` had any non-test caller, STOP and report (not actually dead).
- [ ] **Step 3: If a DB column backs `ChatSessionId`**, decide: leave the column (nullable, unused) and only remove the domain/DTO surface, OR add a drop migration. Prefer leaving the column (lower risk) + a code comment, UNLESS the codebase convention is to drop. Note the decision.
- [ ] **Step 4: Build + typecheck** — `dotnet build apps/api/src/Api` + the GameManagement unit suite + `pnpm typecheck` (FE schema change). All green (a dangling ref would fail).
- [ ] **Step 5: Commit** — `refactor(session-live): #2578 SP5-a remove dead-code ChatSessionId/SetAgentMode (ADR-083 SP0)`

---

## Task 3: Verify fail-silently on FE writes (finding #16)
**Files:** the FE write paths (diary/score/notes mutations) under `apps/web/src/lib/...`.

- [ ] **Step 1: Investigate** — do the FE write mutations (diary POST, score POST, notes PUT) treat the **POST/PUT response** as the ack (success/error surfaced to the user), or do they fire-and-forget and rely on the SSE event echoing back (so a broken stream = silent write loss / no error)?
- [ ] **Step 2: Adjudicate**:
  - If writes already use the response as ack (expected per scoping) → add a focused test asserting an error response surfaces an error to the user (no silent success), and document in the report that finding #16 is verified-OK.
  - If a write IS fire-and-forget with no response-ack → fix it to surface the POST result (success/error) independent of the SSE, + a test.
- [ ] **Step 3: GREEN** — `pnpm test` for the affected mutation(s) + typecheck.
- [ ] **Step 4: Commit** — `test(session-live): #2578 SP5-a verify FE writes use response-ack (not fail-silently)`

---

## Self-Review
- toolkit-repoint must not break event delivery (verify envelope compat in T1 Step 1).
- ChatSessionId removal must be complete (typecheck/build is the guard).
- fail-silently is verification-first; only fix if a real gap.

## Out of scope (→ SP5-b / SP5-c)
- Observability completion (RagFirstTokenLatency, counters, SLO) → SP5-b.
- timeout/circuit-breaker, degradation-contract/bulkhead, backfill → SP5-c (backfill needs owner OQ#5).
- Physical removal of `/stream/v2` → Fase 4.
