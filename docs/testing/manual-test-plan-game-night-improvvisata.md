# Manual Test Plan — Game Night Improvvisata

**Epic**: Game Night Improvvisata (Phases 2-4)
**Date**: 2026-03-13
**Environments**: Local (`localhost:8080` API / `localhost:3000` Web) | Staging

---

## Prerequisites

### Local Environment Setup

1. **Infrastructure running**:
   ```bash
   cd infra && docker compose up -d postgres qdrant redis
   ```
2. **API running**: `cd apps/api/src/Api && dotnet run` (port 8080)
3. **Web running**: `cd apps/web && pnpm dev` (port 3000)
4. **Migrations applied**: `dotnet ef database update`
5. **Admin user seeded**: Check admin credentials in `infra/secrets/admin.secret`
6. **At least 1 SharedGame with BGG data** (for degraded agent tests)
7. **At least 1 SharedGame with KB cards + PDF** (for full agent tests)

### Staging Setup
- Verify deployment is up: `curl https://<staging-url>/health`
- Admin credentials available
- At least 2 test user accounts (1 free tier, 1 premium tier)

### Tools
- **HTTP client**: Scalar UI (`/scalar/v1`) or Postman/Insomnia
- **WebSocket client**: Browser DevTools console or `wscat` (for SignalR)
- **Redis CLI**: `redis-cli` or RedisInsight (for quota counter inspection)

---

## Phase 2: Tier & Quota System

### T2-1: Admin Tier CRUD

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | List all tiers | `GET /api/v1/admin/tiers` with admin JWT | 200, array with at least "free" and "premium" tiers | Local + Staging |
| 2 | Get tier by name | `GET /api/v1/admin/tiers/free` | 200, tier object with name, limits, price fields | Local |
| 3 | Get non-existent tier | `GET /api/v1/admin/tiers/nonexistent` | 404 | Local |
| 4 | Create new tier | `POST /api/v1/admin/tiers` with body: `{ "name": "test-tier", "displayName": "Test", ... }` | 201, created tier | Local |
| 5 | Update tier limits | `PUT /api/v1/admin/tiers/test-tier` with modified limits | 200, updated tier | Local |
| 6 | Non-admin access | All tier endpoints with regular user JWT | 403 Forbidden | Local |
| 7 | Unauthenticated access | All tier endpoints without JWT | 401 Unauthorized | Local |
| 8 | Cleanup | Delete test-tier if cleanup endpoint exists, or verify via DB | Tier removed | Local |

### T2-2: User Usage Endpoint

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Get own usage | `GET /api/v1/users/me/usage` with user JWT | 200, usage snapshot with query counts, PDF counts, etc. | Local + Staging |
| 2 | Fresh user (zero usage) | Create new user, immediately call usage endpoint | All counters at 0 | Local |
| 3 | After performing actions | Upload a PDF, ask an agent question, then check usage | Relevant counters incremented | Local |
| 4 | Unauthenticated | `GET /api/v1/users/me/usage` without JWT | 401 | Local |

### T2-3: Tier Enforcement

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Free tier PDF limit | Set free tier to 3 PDFs/month. Upload 3 PDFs. Try 4th. | 4th upload returns 429 or 403 with limit message | Local |
| 2 | Free tier query limit | Set free tier to 20 queries/day. Send 20 agent queries. Try 21st. | 21st query returns limit exceeded error | Local |
| 3 | Premium tier higher limits | Switch user to premium. Verify higher limits apply. | Can upload more PDFs, send more queries | Local |
| 4 | Redis counter inspection | After quota test, check Redis keys for user counters | Keys exist with correct counts, correct TTL (daily/monthly) | Local |
| 5 | Counter reset (daily) | Set Redis key TTL to expire. Wait or manually expire. Check usage. | Counter resets to 0 | Local |
| 6 | Admin bypasses limits | Perform actions as admin user | No tier limits applied | Local |

### T2-4: Frontend — Usage Dashboard

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Usage bar visibility | Log in as regular user, navigate to profile/usage page | Usage bars show current/max for queries, PDFs, games | Local + Staging |
| 2 | Correct data | Perform some actions, refresh usage page | Numbers match actual usage | Local |
| 3 | Tier name display | Check that current tier name (free/premium) is shown | Correct tier name displayed | Local + Staging |
| 4 | Limit warnings | Approach a limit (e.g., 18/20 queries) | Visual warning indicator (yellow/red bar) | Local |
| 5 | Responsive | Check on mobile viewport (375px) | Layout doesn't break, bars stack vertically | Local |

### T2-5: Pricing Page

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Public access | Navigate to `/pricing` without login | Page loads, shows tier comparison | Local + Staging |
| 2 | Tier comparison table | Check all 3 tiers displayed | Free, Premium, Contributor with correct limits | Local |
| 3 | Current tier highlight | Log in as free user, visit pricing | Free tier highlighted as "Current Plan" | Local |
| 4 | CTA buttons | Check Premium tier button | Links to upgrade flow (or placeholder) | Local |
| 5 | Responsive | Mobile viewport | Table readable, no horizontal overflow | Local |

---

## Phase 3: Multi-Device Session (Wave 1 — E3-1)

### T3-1: Create Session Invite

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Host creates invite | As host of an active LiveGameSession: `POST /api/v1/live-sessions/{sessionId}/invite` | 201, response contains `pin` (6 chars), `linkToken` (32 hex), `expiresAt` | Local + Staging |
| 2 | PIN format | Inspect `pin` field | 6 uppercase alphanumeric chars, no O/0/I/1/l ambiguous chars | Local |
| 3 | Link token format | Inspect `linkToken` field | 32-character hex string (GUID without dashes) | Local |
| 4 | Custom expiry | Send `{ "expiryMinutes": 60 }` | `expiresAt` is ~60min from now | Local |
| 5 | Custom max uses | Send `{ "maxUses": 5 }` | Invite has maxUses=5 | Local |
| 6 | Non-host attempts | As non-host player, try to create invite | 403 Forbidden | Local |
| 7 | Non-existent session | `POST /api/v1/live-sessions/{randomGuid}/invite` | 404 | Local |
| 8 | Unauthenticated | No JWT | 401 | Local |
| 9 | Multiple invites | Create 2 invites for same session | Both succeed, different PINs | Local |

### T3-2: Join Session (Registered User)

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Join via PIN | `POST /api/v1/live-sessions/join` with `{ "token": "<PIN>" }` and user JWT | 200, response with `participantId`, `sessionId`, `connectionToken`, `role: "Player"` | Local + Staging |
| 2 | Join via link token | `POST /api/v1/live-sessions/join` with `{ "token": "<linkToken>" }` and JWT | 200, same as above | Local |
| 3 | ConnectionToken format | Inspect `connectionToken` in response | 6-char alphanumeric (same charset as PIN) | Local |
| 4 | Session not found | Join with invalid token | 404 | Local |
| 5 | Expired invite | Wait past expiry (or manually set `expires_at` in DB to past) | 400 or 410 — invite expired | Local |
| 6 | Revoked invite | Revoke invite in DB (`is_revoked = true`), try to join | 400 — invite revoked | Local |
| 7 | Max uses reached | Set maxUses=1, join once, try again with different user | 400 — invite exhausted | Local |
| 8 | Duplicate join | Same user joins same session twice | 409 Conflict (already a participant) | Local |
| 9 | CurrentUses increment | After successful join, query invite from DB | `current_uses` incremented by 1 | Local |

### T3-3: Join Session (Guest — No Auth)

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Guest join via PIN | `POST /api/v1/live-sessions/join` with `{ "token": "<PIN>", "guestName": "Marco" }` — NO JWT | 200, response with `participantId`, `guestName: "Marco"`, `userId: null` | Local + Staging |
| 2 | Guest join via link | Same but with linkToken | 200 | Local |
| 3 | Guest without name | `{ "token": "<PIN>" }` no guestName, no JWT | 400 — guest name required | Local |
| 4 | Guest name trimming | `{ "token": "<PIN>", "guestName": "  Marco  " }` | Stored as "Marco" (trimmed) | Local |
| 5 | Guest name empty string | `{ "token": "<PIN>", "guestName": "" }` | 400 — guest name required | Local |

### T3-4: List Participants

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | List after joins | `GET /api/v1/live-sessions/{sessionId}/participants` | 200, array with host + joined participants | Local + Staging |
| 2 | Host in list | Check participant with `role: "Host"` | Present, `isActive: true` | Local |
| 3 | Guest in list | Check guest participant | `guestName` set, `userId: null`, `displayName` = guest name | Local |
| 4 | Registered user in list | Check registered participant | `userId` set, `guestName: null` | Local |
| 5 | Left participant | Have a participant leave, then query | `isActive: false`, `leftAt` timestamp set | Local |
| 6 | Empty session | Query session with no participants (besides host) | Returns only host | Local |
| 7 | Unauthenticated | No JWT | 401 | Local |

### T3-5: End-to-End Invite Flow

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Full happy path | 1. Create LiveGameSession 2. Create invite 3. User A joins via PIN 4. Guest B joins via link + guestName 5. List participants | All 3 participants visible (host + 2 joiners) | Local + Staging |
| 2 | Invite lifecycle | 1. Create invite (maxUses=2, expiry=5min) 2. Join user A 3. Join user B 4. Try join user C | User C gets "invite exhausted" error | Local |
| 3 | Multiple invites | Create invite A and B for same session. Join via A, then via B. | Both work, both participants in same session | Local |

---

## Phase 4: AI Game Night (Wave 1 — E4-1 + E4-2)

### T4-1: Degraded Agent Service

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Agent with KB cards | Call `GetAgentCapability` for agent with KB cards | `Level: Full`, `HasKbCards: true` | Local |
| 2 | Agent with BGG only | Create agent linked to SharedGame (via game's AgentDefinitionId), no KB cards | `Level: Degraded`, `HasBggMetadata: true`, `HasKbCards: false` | Local |
| 3 | Agent with nothing | Create standalone agent, no game link, no KB cards | `Level: None` | Local |
| 4 | Degraded prompt content | Build degraded prompt for a game with full BGG data | Prompt contains: game name, player count, play time, complexity, categories, mechanics, limitations disclaimer | Local |
| 5 | Degraded prompt — missing fields | Build prompt for game with only name (no complexity, no mechanics) | Prompt omits missing fields gracefully (no "null" strings) | Local |
| 6 | Description truncation | Game with >1000 char description | Description truncated to 1000 chars + "..." | Local |
| 7 | Rulebook analysis flag | Agent with KB cards + RulebookAnalysis exists for linked game | `HasRulebookAnalysis: true` | Local |

> **How to test**: These are service-level tests. Use either:
> - Unit tests: `dotnet test --filter "FullyQualifiedName~DegradedAgent"`
> - Integration: Create test data in DB, call endpoint that triggers agent query, inspect system prompt in logs

### T4-2: Tier-Aware LLM Routing

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Free user routing | Ask agent question as free-tier user | Routed to Ollama/free models only | Local |
| 2 | Premium user routing | Ask agent question as premium-tier user | Routed to standard models (OpenRouter) | Local |
| 3 | Admin routing | Ask agent question as admin | Routed to Admin-tier models (full access) | Local |
| 4 | Internal pipeline (null user) | Trigger ConversationQueryRewriter (internal) | Uses User tier (not blocked by Anonymous tier) | Local |
| 5 | Tier upgrade takes effect | Change user tier from free → premium in DB. Ask question. | Now routes to premium models | Local |
| 6 | Verify in logs | Check API logs for routing decision | Log shows selected provider and tier | Local |

> **How to test routing**: Enable debug logging for `HybridAdaptiveRoutingStrategy`:
> ```json
> "Logging": { "LogLevel": { "Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement": "Debug" } }
> ```

### T4-3: Integration — Agent Query During Session

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Full agent in session | Start session with game that has KB cards + PDF. Ask agent question. | Full RAG response with rulebook knowledge | Local + Staging |
| 2 | Degraded agent in session | Start session with game that has BGG data but no KB cards. Ask question. | Response mentions limitations, answers with BGG metadata only | Local + Staging |
| 3 | No agent available | Start session with game that has no agent or data | Appropriate error/message: "No AI assistant available for this game" | Local |
| 4 | Free tier query limit | As free user in session, exhaust session query budget | After limit reached, further queries return quota exceeded | Local |
| 5 | Premium tier higher budget | As premium user, verify higher query limit | Can ask more questions before hitting limit | Local |

---

## Cross-Cutting Tests

### CX-1: Security

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | JWT required on protected endpoints | Hit all `RequireAuthenticatedUser` endpoints without JWT | 401 on all | Local |
| 2 | Admin-only endpoints | Hit admin tier CRUD as regular user | 403 on all | Local |
| 3 | Join endpoint allows unauthenticated | `POST /live-sessions/join` without JWT but with guestName | Succeeds (guest join) | Local |
| 4 | IDOR on invite creation | Try to create invite for session you don't host | 403 | Local |
| 5 | IDOR on participant list | Try to list participants of a session you're not in | Depends on policy — verify expected behavior | Local |
| 6 | PIN brute force | Try 100 random PINs rapidly | Rate limiting should kick in (if implemented) or at minimum no information leakage | Local |

### CX-2: Data Integrity

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | FK constraints | Delete a LiveGameSession. Check session_invites and session_participants. | Cascade delete removes related rows | Local |
| 2 | Unique PIN constraint | Insert duplicate PIN in session_invites | DB constraint violation | Local |
| 3 | Concurrent joins | 10 users join same invite (maxUses=10) simultaneously | Exactly 10 succeed, no over-count | Local |
| 4 | Redis-DB consistency | Compare Redis usage counters with actual usage in DB | Counts match | Local |

### CX-3: Error Handling

| # | Test | Steps | Expected | Env |
|---|------|-------|----------|-----|
| 1 | Invalid UUIDs | Send malformed GUIDs in path params | 400 Bad Request, not 500 | Local |
| 2 | Empty body | `POST /live-sessions/join` with empty body | 400 with validation message | Local |
| 3 | Extra fields | Send unknown fields in JSON body | Ignored silently (not 400) | Local |
| 4 | Very long guest name | `guestName` with 500+ chars | Either truncated or 400 validation error | Local |

---

## Staging-Specific Tests

### S-1: Environment Verification

| # | Test | Steps | Expected |
|---|------|-------|----------|
| 1 | Health check | `GET /health` | 200 OK |
| 2 | DB migration status | Check `/admin/system` or migration table | All migrations applied |
| 3 | Redis connectivity | Check via admin panel or logs | Connected, no timeout errors |
| 4 | Tier seed data | `GET /api/v1/admin/tiers` | Free + Premium tiers exist |

### S-2: Performance Baseline

| # | Test | Steps | Expected |
|---|------|-------|----------|
| 1 | Invite creation latency | Time `POST .../invite` (10 requests) | < 200ms p95 |
| 2 | Join session latency | Time `POST .../join` (10 requests) | < 300ms p95 |
| 3 | Participants query | List participants for session with 6 players | < 100ms |
| 4 | Usage endpoint | Time `GET /users/me/usage` (10 requests) | < 150ms p95 |
| 5 | Agent query (degraded) | Time degraded agent response | < 3s (Ollama) |
| 6 | Agent query (full RAG) | Time full RAG agent response | < 10s (includes embedding + retrieval) |

---

## Test Execution Checklist

### Local Validation

- [ ] **Infra**: Docker services running (postgres, qdrant, redis)
- [ ] **Build**: `dotnet build` passes (0 errors, 0 warnings)
- [ ] **Unit tests**: `dotnet test --filter "Category=Unit"` — all pass
- [ ] **Phase 2 — Tier CRUD** (T2-1): Tests 1-7 pass
- [ ] **Phase 2 — User Usage** (T2-2): Tests 1-4 pass
- [ ] **Phase 2 — Enforcement** (T2-3): Tests 1-6 pass
- [ ] **Phase 2 — Usage UI** (T2-4): Tests 1-5 pass
- [ ] **Phase 2 — Pricing** (T2-5): Tests 1-5 pass
- [ ] **Phase 3 — Create Invite** (T3-1): Tests 1-9 pass
- [ ] **Phase 3 — Join (registered)** (T3-2): Tests 1-9 pass
- [ ] **Phase 3 — Join (guest)** (T3-3): Tests 1-5 pass
- [ ] **Phase 3 — Participants** (T3-4): Tests 1-7 pass
- [ ] **Phase 3 — E2E flow** (T3-5): Tests 1-3 pass
- [ ] **Phase 4 — Degraded Agent** (T4-1): Tests 1-7 pass
- [ ] **Phase 4 — Tier Routing** (T4-2): Tests 1-6 pass
- [ ] **Phase 4 — Integration** (T4-3): Tests 1-5 pass
- [ ] **Security** (CX-1): Tests 1-6 pass
- [ ] **Data integrity** (CX-2): Tests 1-4 pass
- [ ] **Error handling** (CX-3): Tests 1-4 pass

### Staging Validation

- [ ] **Environment** (S-1): Tests 1-4 pass
- [ ] **Performance** (S-2): Tests 1-6 within thresholds
- [ ] **Phase 2 — Tier CRUD** (T2-1): Tests 1, 2, 6, 7 pass
- [ ] **Phase 3 — E2E flow** (T3-5): Test 1 (full happy path) passes
- [ ] **Phase 3 — Guest join** (T3-3): Test 1 passes
- [ ] **Phase 4 — Agent query** (T4-3): Tests 1, 2 pass
- [ ] **Security** (CX-1): Tests 1-4 pass

---

## Notes

- **Redis inspection commands**:
  ```bash
  redis-cli KEYS "tier:usage:*"           # List all usage keys
  redis-cli GET "tier:usage:<userId>:daily:agent_query"  # Check specific counter
  redis-cli TTL "tier:usage:<userId>:daily:agent_query"  # Check expiry
  ```

- **Useful DB queries**:
  ```sql
  -- Check session invites
  SELECT id, pin, link_token, max_uses, current_uses, expires_at, is_revoked
  FROM session_invites WHERE session_id = '<id>';

  -- Check participants
  SELECT id, user_id, guest_name, role, connection_token, is_active, joined_at
  FROM session_participants WHERE session_id = '<id>';

  -- Check user tier
  SELECT id, email, tier FROM users WHERE email = '<email>';
  ```

- **Log filtering for routing decisions**:
  ```bash
  # In API logs, look for:
  grep "SelectProvider\|MapUserToLlmTier\|routing decision" logs/api.log
  ```
