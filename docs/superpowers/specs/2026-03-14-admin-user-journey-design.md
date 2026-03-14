# Admin User Journey — Design Spec

**Date**: 2026-03-14
**Status**: Draft
**Approach**: Vertical Slice (4 phases)
**Validation**: Browser E2E tests only

## Overview

End-to-end user story covering admin user invitation, user onboarding with agent interaction, and admin management capabilities. The backend is ~95% implemented; this spec covers missing frontend pages, content-gating logic, minor backend extensions, and E2E test coverage.

## User Story

As an admin, I want to invite users via email. The invited user accepts, sets a password, logs in, adds a game to their collection, creates an agent (gated by KB existence), and asks the agent via voice about the game. The admin can change user roles and view audit logs. Only browser tests validate the implementation.

## Business Rules

| Rule | Description |
|------|-------------|
| R1 | Admin uploads PDF for a game and initializes embedding (KB) |
| R2 | Without initialized PDF/KB, agent creation is blocked for that game |
| R3 | User owns the game (in collection) → agent shows full text + images |
| R4 | User does NOT own the game → agent shows only page/paragraph reference, no text/images |
| R5 | If an agent exists but its KB was deleted, chat handler returns a graceful error ("KB not available") instead of crashing |

## Flows

### F1 — Admin Invites User

```
Admin → /admin/users/invitations → Fills email + role → Submit
→ Backend: SendInvitationCommand → Email sent with token link
→ Invitation appears in table as "Pending"
```

### F2 — User Onboarding

```
F2a: User clicks email link → /accept-invite?token=xxx
     → POST /api/v1/auth/validate-invitation { token } → Form: password + confirm
     → AcceptInvitationCommand → Returns session → Auto-redirect to dashboard

F2b: User searches game → Adds to collection
     → AddToCollectionCommand

F2c: User creates agent for game (only if KB exists)
     → CreateAgentCommand (gated by VectorDocument existence)

F2d: User asks agent via voice
     → Web Speech API → transcription → agent chat
     → Response with content-gated sources
```

### F3 — Admin Management

```
F3a: Admin → /admin/users/{id} → Tab "Role"
     → ChangeUserRoleCommand
     → Badge updates, history entry created

F3b: Admin → Tab "Activity Log" (per-user filtered)
     Admin → /admin/system/audit-log (general view)
     → GetAuditLogsQuery with filters
```

## Architecture

### Bounded Contexts Involved

| Context | Role |
|---------|------|
| Authentication | Invite, accept, password, roles |
| KnowledgeBase | Agent creation gate, content-gating chunks |
| UserLibrary | Ownership check for content-gating |
| Administration | Audit logs, role history |

### Content Access Level

```
ContentAccessLevel:
  FullAccess      → user owns game → full text + images
  ReferenceOnly   → user does NOT own → page/paragraph reference only
  NoAccess        → no KB initialized → agent creation blocked
```

Content-gating is hybrid:
- **Backend** (authoritative): KnowledgeBase handler queries UserLibrary via `GetCollectionStatusQuery` to determine ownership. Chunks are filtered before response — when `hasAccess: false`, `text` and `imageUrl` fields are set to `null` server-side. The backend NEVER sends protected content relying on the frontend to hide it.
- **Frontend** (presentational): renders based on `hasAccess` flag per source and `contentAccessLevel` on response. This is purely UX — the security enforcement is entirely server-side.

### Cross-BC Communication

```
KnowledgeBase (chat handler)
  → IMediator.Send(GetCollectionStatusQuery { UserId, EntityType = EntityType.Game, EntityId = gameId })
  → UserLibrary returns { IsInCollection: bool }
  → KnowledgeBase filters chunk content accordingly
```

No direct service coupling between bounded contexts.

### Response DTO Extension

```json
{
  "message": "The goal of the game is...",
  "sources": [
    {
      "reference": "Page 3, Paragraph 2 - Game Objective",
      "text": "The player must...",
      "imageUrl": "/chunks/img-042.png",
      "hasAccess": true
    }
  ],
  "contentAccessLevel": "FullAccess"
}
```

When `hasAccess: false`, `text` and `imageUrl` are `null` (server-side enforcement, not frontend hiding).

### Agent Creation Gate

Backend (`CreateAgentCommandHandler`):
1. Receive `gameId`
2. Query: exists `VectorDocument` with `SharedGameId = gameId` and status `Indexed`?
3. No → throw `AgentCreationBlockedException`
4. Yes → proceed

**Edge case (R5)**: If an agent already exists but its KB is later deleted, the chat handler checks KB existence at query time and returns a graceful error response: `{ "error": "KB_NOT_AVAILABLE", "message": "The knowledge base for this game is no longer available" }`.

Frontend (Agent Builder Modal):
- Games with KB ready → selectable (green "KB Ready" badge)
- Games without KB → disabled (grey "No KB" badge) with tooltip

## Required Backend Changes

The following backend modifications are needed (not just frontend work):

| Change | Context | Details |
|--------|---------|---------|
| Extend `AcceptInvitationCommand` | Authentication | Add optional `DisplayName` field (or remove name from frontend form — see Phase 1 notes) |
| Extend `ChangeUserRoleCommand` | Administration | Add `Reason` string field for audit trail |
| Add `RevokeInvitationCommand` | Authentication | New command + handler + endpoint `DELETE /admin/users/invitations/{id}` |
| Content-gating in chat handler | KnowledgeBase | Query `GetCollectionStatusQuery`, filter chunk content |
| Agent creation gate | KnowledgeBase | Validate `VectorDocument` exists and is `Indexed` |
| KB deleted edge case (R5) | KnowledgeBase | Graceful error when agent's KB no longer exists |

## Phase 1: Invite Flow

### Admin Invite Page (`/admin/users/invitations`)

**Location**: `app/admin/(dashboard)/users/invitations/page.tsx`

**Components**:
- `InviteUserForm` — email input, role select, submit button
- `BulkInviteSection` — collapsible, textarea (one email per line), common role select
- `InvitationsTable` — columns: email, role, status, sent date, expiry. Filters by status. Row actions: Resend / Revoke. Paginated.

**Navigation**: Section "Users" in admin sidebar → new item "Invitations" (icon: MailPlus)

**API endpoints**:
- `POST /admin/users/invite` → SendInvitationCommand (existing)
- `POST /admin/users/bulk/invite` → BulkSendInvitationsCommand (existing)
- `POST /admin/users/invitations/{id}/resend` → ResendInvitationCommand (existing)
- `GET /admin/users/invitations` → GetInvitationsQuery (existing)
- `GET /admin/users/invitations/stats` → GetInvitationStatsQuery (existing)
- `DELETE /admin/users/invitations/{id}` → RevokeInvitationCommand (**new — must be created**)

### Accept Invite Page (`/accept-invite`)

**Location**: `app/(public)/accept-invite/page.tsx` — public route, no auth required

**Flow**:
1. User clicks email link → `/accept-invite?token=abc123`
2. Page extracts token from query string and POSTs to `POST /api/v1/auth/validate-invitation` with body `{ token }` on mount
   - Valid → show form (email readonly from validation response, password, confirm password)
   - Invalid/expired → error message + "Request new invite" link
3. Submit → `AcceptInvitationCommand` with `{ token, password, confirmPassword }`
4. Success → backend returns `{ sessionToken, expiresAt }` → frontend sets session cookie → auto-redirect to dashboard (authenticated state)

**Note on AcceptInvitationCommand**: The current command accepts `Token`, `Password`, `ConfirmPassword` only. Display name is NOT collected at invite-accept time — the user can set it later in profile settings. This avoids a backend change.

**Password validation** (frontend):
- Min 8 chars, 1 uppercase, 1 number, 1 special
- Visual strength indicator
- Real-time confirm match

### E2E Tests: `admin-invite.spec.ts`

| # | Test | Assertions |
|---|------|------------|
| 1 | Admin sends invitation | Form submit → row appears as "Pending" |
| 2 | User accepts invitation | Token page → set password → submit → auto-redirected to dashboard in authenticated state |
| 3 | Expired token | Error message shown, no form |
| 4 | Admin resends invitation | Click "Resend" → success feedback |

## Phase 2: Content-Gating RAG

### Backend Changes

**New in KnowledgeBase**:
- `ContentAccessLevel` enum: `FullAccess`, `ReferenceOnly`, `NoAccess`
- Chat response handler: query `GetCollectionStatusQuery { UserId, EntityType.Game, EntityId }` → filter chunks
- `CreateAgentCommandHandler`: validate `VectorDocument` exists and is `Indexed`
- Graceful error when agent's KB is deleted (R5)

**Modified DTOs**:
- Chat response: add `contentAccessLevel` field + `hasAccess` per source

### Frontend Changes

**New component**: `SourceReference`
- `hasAccess: true` → visible chunk text + image + "Page X, Par Y" label
- `hasAccess: false` → blurred card + lock icon + "Page X, Par Y" + CTA "Add game to collection to see content"

**Banner** (top of chat when ReferenceOnly):
```
⚠️ You don't own this game. Responses include rulebook references
   but not the full text. [Add to collection →]
```

**Agent Builder Modal** (existing, modified):
- Game select shows KB status badges
- Games without KB are disabled with tooltip

### E2E Tests: `agent-content-gating.spec.ts`

| # | Test | Assertions |
|---|------|------------|
| 1 | Game without KB → agent not creatable | Game disabled in builder, error shown |
| 2 | User owns game → FullAccess | Source text visible, no lock icon |
| 3 | User doesn't own game → ReferenceOnly | Banner visible, lock icons, no text in sources |
| 4 | User adds game → access unlocks | After adding to collection, new chat request shows FullAccess (note: mock fresh response, not cached) |

## Phase 3: Admin User Management

### User Detail Page (`/admin/users/{id}`)

**Location**: `app/admin/(dashboard)/users/[id]/page.tsx`

**Layout**:
- `UserHeader` — avatar, name, email, role badge, status badge
- Tabs:
  - **Overview**: account info, collection count, agents count
  - **Role**: `ChangeRoleCard` (select + confirm dialog) + `RoleHistoryTable`
  - **Activity Log**: `UserAuditLogTable` filtered by userId

**Note on ChangeRoleCard**: The current `ChangeUserRoleCommand` takes only `UserId` and `NewRole` — no `Reason` field. The reason textarea requires extending the command (listed in Required Backend Changes). If deferred, the card shows only role select + confirm dialog without reason.

**API endpoints**:
- `GET /admin/users/{id}` → user detail (existing)
- `PUT /admin/users/{id}/role` → ChangeUserRoleCommand (existing, to be extended with Reason)
- `GET /admin/users/{id}/role-history` → GetUserRoleHistoryQuery (existing)
- `GET /admin/users/{id}/audit-log` → GetAuditLogsQuery filtered by target user (existing — note: path param, NOT query param)

### Audit Log Page (`/admin/system/audit-log`)

**Location**: `app/admin/(dashboard)/system/audit-log/page.tsx`

**Components**:
- `FilterBar` — search, action type select, result select, date range picker, admin user select, export CSV button
- `AuditLogTable` — columns: timestamp, admin, action, resource, target user, result, details. Expandable rows for JSON details. Click target user → link to `/admin/users/{id}`. Paginated (50/page).
- `AuditLogStats` — cards: total actions today, failed actions (red highlight), most active admin

**Navigation**: Section "System" → new item "Audit Log" (icon: ScrollText)

**API endpoints** (all existing):
- `GET /admin/audit-log` → GetAuditLogsQuery with filters
- `GET /admin/audit-log/export` → ExportAuditLogsQuery

### E2E Tests: `admin-role-audit.spec.ts`

| # | Test | Assertions |
|---|------|------------|
| 1 | Admin changes user role | Select new role → confirm → badge updated |
| 2 | Role history visible | History table shows entry from Test 1 |
| 3 | User activity log | Tab shows "RoleChanged" action |
| 4 | General audit log | Table loads, filter by "Role Change" works, click target user navigates |
| 5 | Export CSV | Click export → download initiated |

## Phase 4: Smoke Test Journey

### `full-user-journey.spec.ts`

Single test covering F1→F2→F3 sequentially using two browser contexts with independent mocks.

**Multi-context setup**: Use `browser.newContext()` to create separate `adminContext` and `userContext`. Register `context.route()` mocks on each context independently. Create pages via `context.newPage()`.

**Flow**:
1. Admin sends invitation (adminContext page)
2. User accepts invite, sets password (userContext page)
3. User is auto-redirected to dashboard (session from AcceptInvitation response)
4. User adds game "Descent" to collection
5. User creates agent for "Descent" (KB Ready)
6. User asks agent via voice (mocked Web Speech API): "What is the game's purpose and describe a turn"
7. User verifies: response received, sources with FullAccess
8. Admin changes user role from User to Contributor (adminContext page)
9. Admin verifies role history
10. Admin checks audit log — invitation + role change entries visible

### Mocking Strategy

| What | How | Why |
|------|-----|-----|
| Email sending | `adminContext.route()` intercepts invite response with token | Can't read real emails |
| Web Speech API | `page.evaluate()` injects MockSpeechRecognition | No real mic in Playwright |
| Agent RAG response | `userContext.route()` with sources mock (fresh per request, no cache) | Backend AI unavailable in test |
| Embedding status | `userContext.route()` with pre-initialized game | No real embedding in E2E |

**Critical**: use `context.route()` NOT `page.route()` for Next.js dev server compatibility.

### Flakiness Mitigation

| Risk | Mitigation |
|------|------------|
| Multi-page (admin + user) | `browser.newContext()` + `context.newPage()` for isolated contexts |
| SSR timing | `page.waitForLoadState('networkidle')` before assertions |
| Multiple elements match | `.first()` on every locator |
| Mock intercept timing | Route before navigation, `**` glob pattern |
| Toast assertions | `page.on('console')` to capture `[Toast]` |
| Cached responses | Mock returns fresh data per request, not cached |

## Test Summary

```
tests/e2e/
├── admin-invite.spec.ts          # Phase 1 — 4 tests
├── agent-content-gating.spec.ts  # Phase 2 — 4 tests
├── admin-role-audit.spec.ts      # Phase 3 — 5 tests
└── full-user-journey.spec.ts     # Phase 4 — 1 journey test
                                  # Total: 14 tests
```

## Implementation Phases

| Phase | Deliverable | New Pages | E2E Tests | Dependencies |
|-------|------------|-----------|-----------|--------------|
| 1 | Invite Flow | 2 (admin + accept) | 4 | RevokeInvitationCommand (new backend) |
| 2 | Content-Gating | 0 (modify existing) | 4 | Existing agent/KB + backend content-gating |
| 3 | Admin Management | 2 (user detail + audit) | 5 | ChangeUserRoleCommand extension (optional) |
| 4 | Smoke Journey | 0 | 1 | Phases 1+2+3 |
| **Total** | | **4 new pages** | **14 tests** | |

Phases 1 and 3 are independent and can be parallelized. Phase 2 depends on existing agent/KB (already implemented). Phase 4 requires all previous phases.

## Out of Scope

- Email template editor UI (backend exists, separate epic)
- Admin UI for email queue management
- Voice API fallback (server-side speech-to-text)
- OCR pipeline for scanned PDFs
- Bulk role change UI (endpoint exists, can be added later)
- Display name collection at invite-accept time (user sets in profile later)
