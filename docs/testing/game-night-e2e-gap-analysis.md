# Game Night Improvvisata — E2E Test Gap Analysis

**Date**: 2026-03-16 | **Epic**: #379 (CLOSED) | **Status**: Implementation 100%, Test coverage ~60%

---

## Existing E2E Test Inventory

### Game Night Specific

| File | Scope | Coverage |
|------|-------|----------|
| `e2e/game-night-journey.spec.ts` | Full user journey | BGG search, sessions page, scoreboard (mock routes) |
| `e2e/game-night-flow.spec.ts` | API flow | Session CRUD, pause/resume, context loading |
| `__tests__/e2e/toolkit-create-session.spec.ts` | Toolkit | Create session, participants, join via code |
| `__tests__/e2e/session-history.spec.ts` | History | Rendering, filters, date range, empty state |

### Multi-Device Sessions

| File | Scope | Coverage |
|------|-------|----------|
| `e2e/sessions/multi-device-session.spec.ts` | Multi-device flow | Create, invite, PIN join (registered + guest), participant list, score proposal, agent toggle |
| `e2e/sessions/session-notes.spec.ts` | Notes | CRUD, timestamps |
| `e2e/sessions/session-limits.spec.ts` | Quotas | Tier-based limits |
| `e2e/sessions/session-archive.spec.ts` | Archive | Archive/unarchive, restore |
| `e2e/sessions/session-templates.spec.ts` | Templates | Create from template, reuse |

### Infrastructure

- **Playwright config**: Multi-browser (Chrome, Firefox, Safari + mobile + tablet)
- **Auth**: `AuthHelper` with mock + real credential support
- **Helpers**: `ChatHelper`, `GamesHelper`, `RealBackendHelper`
- **Fixtures**: admin, editor, user roles with real credentials

---

## Gap Analysis: User Journey Steps

### ✅ Covered (Mock-Based)

| Step | Test | Type |
|------|------|------|
| BGG Search UI | `game-night-journey.spec.ts` | Mock API |
| Session Creation | `toolkit-create-session.spec.ts` | Mock API |
| Invite Generation | `multi-device-session.spec.ts` | Mock API |
| PIN Join (Registered) | `multi-device-session.spec.ts` T3-2 | Mock API |
| PIN Join (Guest) | `multi-device-session.spec.ts` T3-3 | Mock API |
| Participant List | `multi-device-session.spec.ts` T3-4 | Mock API |
| Scoreboard Render | `game-night-journey.spec.ts` | Mock API |
| Pause/Resume API | `game-night-flow.spec.ts` | Mock API |
| Session History | `session-history.spec.ts` | Mock API |

### ❌ Missing E2E Coverage

| # | Gap | Why Missing | Priority | Complexity |
|---|-----|-------------|----------|------------|
| G1 | **PDF Upload → Processing → Agent Auto-Create** | Requires async backend event chain + ML pipeline | 🔴 HIGH | High |
| G2 | **Real-Time Scoreboard via SignalR** | WebSocket not interceptable via `page.route()` | 🔴 HIGH | Medium |
| G3 | **Rule Arbitration (Arbitro Mode)** | Agent RAG query + structured verdict | 🟡 MEDIUM | Medium |
| G4 | **Session Save → AI Recap Generation** | Async LLM call + eventual consistency | 🟡 MEDIUM | High |
| G5 | **Resume → Recap Display + Photo Review** | Depends on G4 | 🟡 MEDIUM | Medium |
| G6 | **Guest Score Proposal → Host Confirm** | Multi-participant SignalR broadcast | 🟡 MEDIUM | Medium |
| G7 | **ScoreAssistant NLP Parse** | Agent BC parse endpoint + confidence flow | 🟡 MEDIUM | Low |
| G8 | **Notification Push (PDF Ready)** | SSE stream + deep link navigation | 🟡 MEDIUM | Low |
| G9 | **Auto-Save Every 10 Minutes** | Background service, not yet implemented | 🟢 LOW | Medium |
| G10 | **Offline Score Buffer + Sync** | Not yet implemented | 🟢 LOW | High |

---

## Recommended Test Strategy

### Tier 1: Mock-Enhanced E2E (No Backend Required)

Expand existing mock-based tests to cover full journey UI:

```
Test: "Game Night Improvvisata — Complete UI Journey"

1. BGG Search → mock search results
2. Import game → mock import response
3. PDF upload → mock upload + processing status
4. Notification arrives → mock SSE event
5. Navigate to agent → mock chat response
6. Create session → mock session creation
7. ScoreAssistant input → mock parse response (high/low confidence)
8. Arbitro dispute → mock verdict response
9. Pause session → mock snapshot creation
10. Resume session → mock recap + photos
```

**Effort**: 2-3 days | **Coverage**: UI flow completeness

### Tier 2: Integration E2E (Real Backend, Mocked ML)

Tests that require real API but can mock external services:

```
Test: "Game Night — Backend Integration"

1. Real auth → create session → real DB
2. Generate invite → verify PIN in DB
3. Join via PIN (second browser context) → verify participant
4. Record score → verify in DB
5. Pause → verify PauseSnapshot in DB
6. Resume → verify session status + new invite
```

**Requires**: Running API + PostgreSQL + Redis
**Mock**: Qdrant, LLM service, embedding service
**Effort**: 3-4 days | **Coverage**: Data integrity, event chain

### Tier 3: Full Integration (Real Everything)

Tests requiring full stack including ML pipeline:

```
Test: "Game Night — Full Pipeline"

1. Import BGG game
2. Upload real PDF (small, 2-page rulebook)
3. Wait for processing completion (poll status endpoint)
4. Verify agent auto-created
5. Query agent with rule question
6. Verify RAG response contains PDF content
```

**Requires**: Full stack (API + PostgreSQL + Qdrant + Redis + Embedding Service)
**Effort**: 5-7 days | **Coverage**: End-to-end pipeline validation

---

## Priority Implementation Order

| Phase | Tests | Effort | Blocks |
|-------|-------|--------|--------|
| **Phase 1** | Tier 1 mock journey (G3, G5, G7, G8) | 2-3 days | Nothing |
| **Phase 2** | Tier 2 backend integration (G2, G6) | 3-4 days | Running backend |
| **Phase 3** | Tier 2 async events (G1, G4) | 2-3 days | Phase 2 |
| **Phase 4** | Tier 3 full pipeline | 5-7 days | All services running |

**Total estimated effort**: 12-17 days

---

## New Helpers Needed

| Helper | Purpose |
|--------|---------|
| `SessionHelper` | Session CRUD, invite generation, participant management |
| `ScoreHelper` | Score mock responses at various confidence levels |
| `ArbitroHelper` | Dispute mock with structured verdict + citations |
| `ResumeHelper` | Snapshot + recap mock data |
| `SSEHelper` | Mock SSE notification events |
| `SignalRHelper` | WebSocket connection setup for real-time tests |

---

## Manual Test Plan Cross-Reference

80+ test cases in `docs/testing/manual-test-plan-game-night-improvvisata.md` map to:
- **T2-x** (Tier & Quota) → Tier 2 integration tests
- **T3-x** (Multi-Device) → Tier 2 + SignalR tests
- **T4-x** (AI Agent) → Tier 3 full pipeline tests

---

**Last Updated**: 2026-03-16
