## 🔴 BE blocker discovered (spec-panel session 2026-05-28)

Pre-implementation code verification (P74) against the Phase 0.5 contract §7 open questions revealed that **Q1 and Q2 require backend work**, not just FE client methods as the contract optimistically assumed.

### Q1 — Cross-game KB search: NOT available user-facing

- `POST /api/v1/knowledge-base/search` (`KnowledgeBaseEndpoints.cs:220` `HandleSearch`) **requires `gameId`** — rejects with `BadRequest` if absent (lines 229-232). Per-game only.
- The cross-game `VectorSemanticSearchQuery` (`Guid? GameId` nullable) IS implemented but exposed **admin-only** via `/vector-search` (`AdminKnowledgeBaseEndpoints.cs:35`).
- **Gap**: need a user-facing cross-game search endpoint (either make `gameId` optional on `/knowledge-base/search`, or expose `VectorSemanticSearchQuery` to a session-scoped route with RBAC filtering).

### Q2 — SSE kb-ask cross-game: NOT available

- `POST /api/v1/knowledge-base/ask` (`KnowledgeBaseEndpoints.cs:270` `HandleAsk`) returns **synchronous JSON** (`Results.Ok({answer, sources, citations})`), **not SSE**, and **requires `gameId`** (lines 283-287).
- The only SSE chat endpoint is `POST /api/v1/agents/{agentId}/chat` (per-agent, consumed by `useAgentChatStream.ts:256`). Other SSE endpoints are admin-only (bulk-import, queue, game-wizard, debug-chat).
- **Gap**: the Phase 0.5 contract's `useKbAskStream` mirror of `useAgentChatStream` needs a cross-game SSE kb-ask endpoint that doesn't exist.

### Impact

- **Foundation (Phase 1)** is blocked by Q1 (cross-game search).
- **Interactions (Phase 2)** is blocked by Q2 (cross-game SSE ask).
- Forcing FE implementation against per-game endpoints would either (a) violate the mockup's "global cross-game" intent, or (b) require mock/stub data — forbidden by RULES.md "Real Code Only".

### Recommended next step

Open a BE issue for the two cross-game endpoints (search + SSE ask) with RBAC scoping (resolves Q4 too), then dispatch #1482 FE Foundation once it lands. Session 2026-05-28 pivoted to **#1483 /discover** (BE-deps pending verification) to keep delivering.

Phase 0.5 contract (`docs/for-developers/frontend/contracts/kb-globale-hooks.md` §7) should be updated to reclassify Q1/Q2 from "FE client" to "BE endpoint work".

🤖 Generated with [Claude Code](https://claude.com/claude-code)
