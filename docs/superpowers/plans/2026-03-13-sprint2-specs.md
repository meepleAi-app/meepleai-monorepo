# Game Night Sprint 2 — Specification Documents Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create 3 specification documents (#211 API contracts, #212 behavioral examples, #213 SSE requirements) that define the complete Sprint 2 implementation interface before coding begins.

**Architecture:** Pure documentation — no code changes. Specs define the contracts for #214 QuickView, #215 responsive matrix, #216 server-side timer. Written as markdown docs in `docs/specs/game-night-sprint2/`.

**Tech Stack:** Markdown, Given/When/Then (Gherkin-style), OpenAPI-style contract definitions

**Branch:** `feature/issue-235-sprint2-specs` from `main-dev`

---

## Chunk 1: API Contracts (#211)

### Task 1: Create branch and spec structure

**Files:**
- Create: `docs/specs/game-night-sprint2/README.md`

- [ ] **Step 1: Create feature branch**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git checkout main-dev && git pull
git checkout -b feature/issue-235-sprint2-specs
git config branch.feature/issue-235-sprint2-specs.parent main-dev
```

- [ ] **Step 2: Create spec directory and README**

```markdown
# Game Night Sprint 2 — Specifications

Issue #235: Specification documents for Sprint 2 implementation.

## Documents
- `api-contracts.md` (#211) — Endpoint signatures, request/response schemas, error codes
- `behavioral-examples.md` (#212) — Given/When/Then scenarios for all user flows
- `sse-requirements.md` (#213) — SSE operational requirements, reconnection, backpressure

## Implementation Issues (depend on these specs)
- #214: QuickView dual-context resolution
- #215: Responsive test matrix
- #216: Server-side timer
- #217: Accessibility test plan (WCAG 2.1 AA)
- #218: Performance budget
```

- [ ] **Step 3: Commit**

```bash
git add docs/specs/game-night-sprint2/
git commit -m "docs(specs): create Sprint 2 spec directory structure (#235)"
```

### Task 2: API Contracts Document (#211)

**Files:**
- Create: `docs/specs/game-night-sprint2/api-contracts.md`

- [ ] **Step 1: Research existing Game Night endpoints**

Read these files to understand existing patterns:
- `apps/api/src/Api/Routing/GameNightEndpoints.cs`
- `apps/api/src/Api/Routing/LiveSessionEndpoints.cs`
- `apps/api/src/Api/Routing/SessionTrackingEndpoints.cs`
- `apps/api/src/Api/Routing/AgentEndpoints.cs`
- `apps/web/src/lib/api/clients/` — all existing client methods

Document all existing endpoints that Sprint 2 builds upon.

- [ ] **Step 2: Write API contracts spec**

The spec must cover these Sprint 2 endpoints:

**QuickView (#214):**
- `GET /api/v1/games/{gameId}/quick-view` — Returns game summary with dual context (shared catalog + user library)
- `GET /api/v1/library/games/{gameId}/quick-view` — Returns user-specific quick view with play history
- Response: `{ game: GameSummaryDto, userContext?: UserGameContextDto, sharedContext?: SharedGameContextDto }`

**Responsive Matrix (#215):**
- No new endpoints — defines viewport breakpoints and component behavior specs
- Document which existing endpoints are called at each breakpoint

**Server-Side Timer (#216):**
- `POST /api/v1/sessions/{sessionId}/timer/start` — Start session timer
- `POST /api/v1/sessions/{sessionId}/timer/pause` — Pause timer
- `POST /api/v1/sessions/{sessionId}/timer/reset` — Reset timer
- `GET /api/v1/sessions/{sessionId}/timer` — Get current timer state
- SSE event: `timer_tick` with `{ elapsed: number, paused: boolean }`

For each endpoint, define:
- HTTP method + path
- Request body (if any) with TypeScript type
- Response body with TypeScript type
- Error responses (400, 401, 403, 404, 409)
- Rate limits / authorization requirements

- [ ] **Step 3: Commit**

```bash
git add docs/specs/game-night-sprint2/api-contracts.md
git commit -m "docs(specs): add Sprint 2 API contracts (#211)"
```

---

## Chunk 2: Behavioral Examples (#212) & SSE Requirements (#213)

### Task 3: Behavioral Examples Document (#212)

**Files:**
- Create: `docs/specs/game-night-sprint2/behavioral-examples.md`

- [ ] **Step 1: Write Given/When/Then scenarios**

Cover these user flows with concrete examples:

**QuickView Scenarios:**
```gherkin
Feature: QuickView dual-context resolution (#214)

  Scenario: View shared game with library entry
    Given user has "Catan" in their library with 5 plays
    And "Catan" exists in shared catalog with avg rating 8.2
    When user opens QuickView for "Catan"
    Then QuickView shows shared catalog data (rating: 8.2, publisher, year)
    And QuickView shows user context (5 plays, last played date, personal rating)

  Scenario: View shared game without library entry
    Given "Catan" exists in shared catalog
    And user does NOT have "Catan" in their library
    When user opens QuickView for "Catan"
    Then QuickView shows shared catalog data only
    And "Add to Library" CTA is prominently displayed

  Scenario: View private game (no shared catalog entry)
    Given user has a private game "Homebrew RPG"
    And "Homebrew RPG" has NO shared catalog entry
    When user opens QuickView for "Homebrew RPG"
    Then QuickView shows user-provided data only
    And shared catalog section shows "Not in community catalog"
```

**Server-Side Timer Scenarios:**
```gherkin
Feature: Server-side timer (#216)

  Scenario: Start timer for session
    Given an active session with 3 players
    When host clicks "Start Timer"
    Then all connected clients receive timer_tick SSE events
    And timer displays synchronized time across all devices

  Scenario: Timer survives page refresh
    Given a running timer at 5:32
    When a player refreshes the page
    Then timer resumes at the server-side elapsed time (not 0:00)
    And no time is lost

  Scenario: Timer pause/resume
    Given a running timer at 3:15
    When host clicks "Pause"
    Then all clients show paused state at 3:15
    When host clicks "Resume"
    Then timer continues from 3:15
```

**Responsive Matrix Scenarios:**
```gherkin
Feature: Responsive layout (#215)

  Scenario: Desktop (≥1024px) — full layout
    Given viewport width is 1280px
    Then 3-column layout is displayed
    And QuickView opens as side panel
    And all game details are visible

  Scenario: Tablet (768px-1023px) — adapted layout
    Given viewport width is 800px
    Then 2-column layout is displayed
    And QuickView opens as bottom sheet
    And secondary details are collapsed

  Scenario: Mobile (<768px) — stacked layout
    Given viewport width is 375px
    Then single-column stacked layout
    And QuickView opens as full-screen modal
    And only essential info is shown
```

- [ ] **Step 2: Commit**

```bash
git add docs/specs/game-night-sprint2/behavioral-examples.md
git commit -m "docs(specs): add Sprint 2 behavioral examples (#212)"
```

### Task 4: SSE Operational Requirements (#213)

**Files:**
- Create: `docs/specs/game-night-sprint2/sse-requirements.md`

- [ ] **Step 1: Research existing SSE infrastructure**

Read these files:
- `apps/api/src/Api/Routing/AgentEndpoints.cs` — existing SSE streaming for agent chat
- `apps/web/src/lib/hooks/useSessionSync.ts` — existing SSE hook
- `apps/web/src/hooks/queries/useAgentChatStream.ts` — existing SSE consumer
- Any SignalR hub files if they exist

- [ ] **Step 2: Write SSE requirements spec**

Cover:

**Connection Management:**
- Max SSE connections per user: 3 (one per tab)
- Connection timeout: 30 minutes idle
- Heartbeat interval: 15 seconds (`:keep-alive\n\n`)
- Reconnection strategy: exponential backoff (1s, 2s, 4s, 8s, max 30s)
- `Last-Event-ID` header support for resumable streams

**Event Types (Sprint 2):**
| Event | Data | Trigger |
|-------|------|---------|
| `timer_tick` | `{ elapsed, paused, serverTime }` | Every 1s when timer active |
| `timer_state` | `{ elapsed, paused, startedAt }` | On connect/reconnect |
| `session_update` | `{ sessionId, state, players }` | Player join/leave |
| `score_update` | `{ playerId, score, round }` | Score recorded |

**Backpressure:**
- Server-side buffer: 100 events max per connection
- If buffer full: drop oldest non-critical events (keep `timer_state`, `session_update`)
- Client-side: discard events older than 5 seconds for `timer_tick`

**Error Handling:**
- 401: Close connection, redirect to login
- 429: Back off, show "reconnecting" banner
- 500: Retry with exponential backoff
- Network disconnect: Show offline indicator, auto-reconnect

**Testing Requirements:**
- Connection recovery after network drop (simulate with DevTools)
- Multiple tabs don't cause connection storms
- Timer stays synchronized across 3+ clients within 500ms
- Memory leak prevention: no event listener accumulation over 30 minutes

- [ ] **Step 3: Commit**

```bash
git add docs/specs/game-night-sprint2/sse-requirements.md
git commit -m "docs(specs): add Sprint 2 SSE operational requirements (#213)"
```

### Task 5: Final review and PR

- [ ] **Step 1: Review all 3 specs for consistency**

Cross-check:
- API contracts reference the same types as behavioral examples
- SSE events match the timer endpoints defined in API contracts
- Responsive breakpoints are consistent across all docs

- [ ] **Step 2: Create PR**

```bash
git push -u origin feature/issue-235-sprint2-specs
gh pr create --base main-dev --title "docs(specs): Game Night Sprint 2 specifications (#211, #212, #213)" --body "$(cat <<'EOF'
## Summary
- API contracts for QuickView, responsive matrix, and server-side timer (#211)
- Given/When/Then behavioral examples for all Sprint 2 user flows (#212)
- SSE operational requirements: connection management, backpressure, error handling (#213)

## Test plan
- [ ] Review API contracts match existing endpoint patterns
- [ ] Verify behavioral examples cover edge cases
- [ ] Confirm SSE requirements align with existing streaming infrastructure

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Update issues**

```bash
gh issue edit 211 --add-label "status:in-review"
gh issue edit 212 --add-label "status:in-review"
gh issue edit 213 --add-label "status:in-review"
```
