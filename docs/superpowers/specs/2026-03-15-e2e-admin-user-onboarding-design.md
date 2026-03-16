# E2E Admin-User Onboarding Flow Test

**Date**: 2026-03-15
**Status**: Draft (Rev.2 - post spec-review fixes)
**Type**: E2E Browser Test (Playwright)

## Purpose

Validate the complete admin-to-user onboarding journey through a serial Playwright test suite that runs against both local Docker and staging environments.

## Flow Under Test

```
Admin login → Invite user → User accepts → Set password → Login →
Add game to collection → Create agent → Chat with agent →
Admin changes role → Admin checks audit log
```

## Test Architecture

### Strategy: `test.describe.serial()` with shared state

Eight sequential tests sharing state via a custom fixture. Each test is independent in assertion but depends on prior tests for state setup. If any test fails, subsequent tests are skipped.

### Environment Parameterization

| Setting | Local | Staging |
|---------|-------|---------|
| `baseURL` | `http://localhost:3000` | `https://meepleai.app` |
| `apiURL` | `http://localhost:8080` | `https://api.meepleai.app` |
| Admin credentials | Seed data (`admin@meepleai.local` / from `.secret`) | Env vars (`E2E_ADMIN_EMAIL`, `E2E_ADMIN_PASSWORD`) |
| Email strategy | API intercept (extract invitation token from response) | Mailosaur (`E2E_MAILOSAUR_API_KEY`, `E2E_MAILOSAUR_SERVER_ID`) |
| Test user email | `e2e-onboarding-{timestamp}@test.local` | `e2e-onboarding-{timestamp}@{serverId}.mailosaur.net` |
| Cleanup | API call to delete test user after suite | Same |

### Email Invitation Handling

**Local**: Intercept the `POST /admin/users/invite` API response directly via `page.waitForResponse()`. The response `InvitationDto` should contain the invitation token or ID. If the DTO does not expose the plaintext token (security concern), use a test-only query: extract the invitation ID from the response, then query `GET /admin/users/invitations` and use the invitation's accept URL. As a last resort, seed a known token via test helpers. Navigate to `/accept-invite?token={token}`.

**Staging**: After sending the invite, poll Mailosaur for the email sent to the test address. Extract the invitation link from the email body HTML. Navigate to the extracted URL.

## Test Specifications

### Test 1: Admin Login

**Precondition**: Admin credentials available (seed or env vars).

**Steps**:
1. Navigate to `/login`
2. Fill email and password
3. Submit login form
4. Wait for redirect to dashboard/admin area

**Assertions**:
- URL contains `/admin` or `/dashboard`
- Admin user display name visible in navbar
- No error toasts

**Shared state output**: `adminPage` (authenticated browser context)

### Test 2: Admin Invites User

**Precondition**: Admin authenticated (Test 1).

**Steps**:
1. Navigate to admin users page
2. Click "Invite" button
3. Fill invitation form: email (generated test email), role ("user")
4. Submit invitation
5. **Local**: Intercept `POST /admin/users/invite` response via `page.waitForResponse()`, extract invitation token/ID from `InvitationDto`
6. **Staging**: Poll Mailosaur for email, extract invitation link from HTML body

**Assertions**:
- Invitation success toast or confirmation
- Invitation appears in invitations list (status: "Pending")
- **Local**: API response contains invitation ID/token (field: `token` or `id`)
- **Staging**: Email received within 30s, contains valid `/accept-invite?token=` link

**Shared state output**: `invitationToken`, `invitationUrl`, `testUserId` (from response if available)

### Test 3: User Accepts Invitation and Sets Password

**Precondition**: Invitation token/URL available (Test 2).

**Steps**:
1. Open new browser context (unauthenticated)
2. Navigate to `/accept-invite?token={token}` (local) or extracted URL (staging)
3. Verify accept-invite page loads
4. Fill password field (generated strong password: `E2eTest!${timestamp}`)
5. Fill confirm password field
6. Submit form

**Assertions**:
- Accept-invite page renders without errors
- Password strength indicator text contains "Strong" (4-level scale: Weak/Fair/Good/Strong, rendered as `<p>` text content "Strength: Strong")
- Password meets all 4 requirements: length >= 8, uppercase, digit, special char
- Submission succeeds (no validation errors)
- Redirect to `/onboarding` (NOT `/login` or `/dashboard` - the accept-invite page redirects to onboarding after 1.5s delay)

**Shared state output**: `userPassword`, `userEmail`

### Test 4: User Logs In with New Password

**Precondition**: User account created with password (Test 3).

**Steps**:
1. From `/onboarding` (where Test 3 redirected), navigate to `/login`
2. Fill email (test user email) and password
3. Submit login form
4. Wait for redirect

**Assertions**:
- Redirect to `/dashboard` (not login, not error)
- User display name or email visible in UI
- No error toasts

**Note**: Test 3 redirects to `/onboarding`, so user must explicitly navigate to `/login`. Capture `testUserId` from the authenticated session (e.g., from a profile API call or session cookie).

**Shared state output**: `userPage` (authenticated user browser context)

### Test 5: User Adds Game to Collection

**Precondition**: User authenticated (Test 4).

**Steps**:
1. Navigate to `/library`
2. Click "Add Game" or equivalent action button
3. In AddGameDrawer: search for a known game (use seed game or search BGG)
4. Select game from results
5. Confirm add to collection
6. Wait for drawer to close

**Assertions**:
- Game appears in collection list/grid
- Collection count incremented
- Success toast or visual confirmation

**Shared state output**: `gameId`, `gameTitle`

**Environment notes**:
- **Local**: Use a seed game that exists in the local DB (ID configured in `E2EEnvironment.seedGame`). If not found, fail fast with error: `"Seed game not found. Ensure DB is seeded via: dotnet ef database update"`
- **Staging**: Search for a well-known game (e.g., "Catan") that exists in shared catalog

### Test 6: User Creates Agent for the Game

**Precondition**: Game in collection (Test 5).

**Steps**:
1. Navigate to agents page or trigger agent creation from game context
2. Open AgentCreationSheet
3. Select the game added in Test 5
4. Choose agent strategy (e.g., "Tutor")
5. Choose model tier (free tier for test stability)
6. Submit creation
7. Wait for agent to be created and a game session to be established
8. Capture both `agentId` and `gameSessionId` from the UI or API responses

**Assertions**:
- Agent creation succeeds (no errors)
- Agent appears in agents list
- Agent has an associated game session (required for chat)

**Shared state output**: `agentId`, `gameSessionId`

**Technical note**: The chat endpoint is `POST /game-sessions/{gameSessionId}/agent/chat` (not `/agents/{id}/chat`). The `gameSessionId` is required for SSE streaming. Intercept the agent creation API response or the subsequent session launch to capture it.

### Test 7: User Chats with Agent

**Precondition**: Agent ready (Test 6).

**Steps**:
1. Open chat with the created agent
2. Send message: "Qual e' lo scopo del gioco {gameTitle}? Descrivimi un turno di gioco."
3. Wait for SSE streaming response to complete (timeout: 60s)

**Assertions**:
- Chat message appears in thread
- Agent response is received (non-empty)
- Response contains game-related content (not an error message)
- No SSE connection errors
- Response rendered in chat UI (visible text)

**Notes**:
- SSE streaming via `POST /game-sessions/{gameSessionId}/agent/chat` (requires `AgentSessionId`, `UserQuestion`, optional `ChatThreadId`)
- SSE streaming requires waiting for the `[DONE]` event or response completion
- Timeout set higher (60s) due to LLM response time
- On staging, response quality may vary; assert non-empty and no error, not content specifics

### Test 8: Admin Changes User Role and Checks Audit Log

**Precondition**: Admin authenticated (Test 1), test user exists. If admin session has expired (401 on any request), re-authenticate using stored admin credentials before proceeding.

**Steps**:
1. Switch to admin browser context (re-auth if session expired)
2. Navigate to admin users page
3. Find the test user in the users list
4. Change role from "user" to "editor" via InlineRoleSelect
5. Confirm role change
6. Navigate to audit log page (`/admin/audit-log` - singular)
7. Filter/search for the role change event (filter by `Action` and `Resource` fields)

**Assertions**:
- Role change succeeds (UI reflects new role "editor")
- Success toast or confirmation
- Audit log contains entry with:
  - Action type matching role change (verify exact `Action` field value at implementation time)
  - Target user: test user email
  - Old role: "user", New role: "editor" (if captured in audit detail fields)
  - Admin who performed the change
  - Timestamp within last minute

**Note**: The audit log endpoint is `GET /admin/audit-log` (singular, per `AdminAuditLogEndpoints.cs`). Verify at implementation time that role change events are captured by this endpoint and what the exact `Action` field value is for role changes.

## File Structure

```
apps/web/e2e/
  flows/
    admin-user-onboarding.spec.ts       # 8 serial tests
  fixtures/
    onboarding-flow.fixture.ts          # Shared state + page helpers (extends existing fixtures/)
  helpers/
    email-strategy.ts                   # Local (API intercept) vs Staging (Mailosaur)
    environment.ts                      # Config resolution (local vs staging)
    cleanup.ts                          # Test user cleanup after suite
  pages/                                # Follows existing convention (NOT page-objects/)
    admin-users.page.ts                 # Admin user management interactions
    accept-invite.page.ts              # Accept invitation page
    library.page.ts                     # User library/collection
    agent-creation.page.ts             # Agent creation flow
    agent-chat.page.ts                 # Chat with agent
    audit-log.page.ts                  # Audit log viewing
```

**Note**: Reuse existing `apps/web/e2e/pages/auth/LoginPage.ts` for login interactions. New page objects go in `pages/` (existing convention), NOT `page-objects/`. Helpers merge into existing `apps/web/e2e/helpers/` directory.

## Shared State Schema

```typescript
interface OnboardingFlowState {
  // Set by Test 1
  adminPage: Page;
  adminCredentials: { email: string; password: string };  // For re-auth if session expires

  // Set by Test 2
  invitationToken: string;
  invitationUrl: string;
  testUserEmail: string;

  // Set by Test 3
  userPassword: string;

  // Set by Test 4
  userPage: Page;
  testUserId: string;  // Captured from login response or profile API, needed for cleanup

  // Set by Test 5
  gameId: string;
  gameTitle: string;

  // Set by Test 6
  agentId: string;
  gameSessionId: string;  // Required for chat endpoint: POST /game-sessions/{id}/agent/chat
}
```

## Configuration

```typescript
// apps/web/e2e/helpers/environment.ts
interface E2EEnvironment {
  name: 'local' | 'staging';
  baseURL: string;
  apiURL: string;
  admin: { email: string; password: string };
  email: {
    strategy: 'api-intercept' | 'mailosaur';
    mailosaurApiKey?: string;
    mailosaurServerId?: string;
  };
  seedGame?: string;       // Local: known game ID from seed
  searchGame?: string;     // Staging: game name to search
  timeouts: {
    email: number;         // Local: 5s, Staging: 30s
    agentReady: number;    // 30s
    chatResponse: number;  // 60s
  };
}
```

## Cleanup Strategy

`test.afterAll()` hook performs cleanup:
1. Delete test user via admin API (`DELETE /admin/users/{userId}`)
2. Delete created agent via API (`DELETE /agents/{agentId}`)
3. Remove game from collection if applicable
4. Close all browser contexts

If cleanup fails, log warning but don't fail the test suite (orphaned test data is acceptable).

## CI/CD Integration

- **Local tests**: Run in GitHub Actions with `docker compose up` in CI
- **Staging tests**: Triggered manually or on deploy-to-staging, requires env secrets
- **Playwright config**: Add `onboarding` project with serial mode

```typescript
// playwright.config.ts addition
{
  name: 'onboarding-local',
  testDir: './e2e/flows',
  testMatch: /admin-user-onboarding\.spec\.ts/,
  use: { baseURL: 'http://localhost:3000' },
  fullyParallel: false,  // serial execution required
  workers: 1,            // prevent contention with parallel workers
  timeout: 180_000,      // 3 min per test (Test 7 chat can take 60s+)
},
{
  name: 'onboarding-staging',
  testDir: './e2e/flows',
  testMatch: /admin-user-onboarding\.spec\.ts/,
  use: { baseURL: 'https://meepleai.app' },
  fullyParallel: false,
  workers: 1,
  timeout: 180_000,
}
```

## Dependencies

| Dependency | Purpose | Required For |
|------------|---------|-------------|
| `@playwright/test` | Test framework | All |
| `mailosaur` | Email testing on staging | Staging only |
| Docker Compose | Local services | Local only |
| Seed data | Known admin user (`admin@meepleai.local`) + at least 1 game with known ID | Local only |
| `E2E_ADMIN_EMAIL`, `E2E_ADMIN_PASSWORD` | Admin credentials | Staging only |
| `E2E_MAILOSAUR_API_KEY`, `E2E_MAILOSAUR_SERVER_ID` | Mailosaur config | Staging only |

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|-----------|
| SSE chat timeout | Test 7 flaky | 60s timeout + retry once |
| Mailosaur email delay | Test 2 slow on staging | 30s polling timeout |
| Seed game not in DB | Test 5 fails locally | Document required seed, fail fast with clear error |
| LLM rate limit | Test 7 fails | Use free tier model, retry once |
| Stale test users | DB pollution | afterAll cleanup + periodic manual cleanup |
| Admin session expires mid-suite | Later tests fail | Re-auth in fixture if 401 detected |

## Out of Scope

- Voice/audio interaction (not part of this US)
- Push notification testing
- Slack notification testing
- Multi-browser/cross-browser testing (Chromium only)
- Performance/load testing
- Mobile viewport testing (desktop only for admin flows)
