# Invite-Only Registration (Beta0)

**Status**: Approved (Revised after spec-panel review)
**Date**: 2026-03-14
**Scope**: Authentication, Administration, SystemConfiguration bounded contexts + Frontend
**Revision**: 2 — addresses 8 critical and 10 major issues from expert review

## Problem

In the beta0 staging environment, public self-registration must be disabled. Users can only join via admin invitation. Admins need a runtime toggle to enable public registration when ready. Users who find the app should be able to request access.

### Success Criteria

- 100% of `POST /auth/register` attempts return 403 when `registration.public_enabled` is `false`
- Admin approval-to-invitation delivery completes within 10 seconds
- Zero access requests are lost during config toggle transitions
- Email enumeration: response body and timing are identical for all request-access outcomes
- Bulk approve processes up to 25 items with per-item success/failure reporting

## Solution

A runtime configuration flag `registration.public_enabled` (default: `false`) gates self-registration. When disabled, the `/register` page shows a "Request Access" form instead. Admins review requests and approve (auto-sending invitations) or reject them. Admins can toggle public registration on from the admin panel at any time.

**Fail-closed default**: If SystemConfiguration is unreachable when checking `registration.public_enabled`, registration is blocked (403). Security-gating features always fail closed.

## Data Model

### New Entity: `AccessRequest` (Authentication Bounded Context)

Aggregate root following existing DDD patterns (same as `InvitationToken`). Factory method: `AccessRequest.Create(email)`.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `Guid` | Primary key |
| `Email` | `string` | Normalized lowercase, unique among Pending |
| `Status` | `AccessRequestStatus` | Pending, Approved, Rejected |
| `RequestedAt` | `DateTime` | Submission timestamp |
| `ReviewedAt` | `DateTime?` | Admin action timestamp |
| `ReviewedBy` | `Guid?` | Admin user ID |
| `RejectionReason` | `string?` | Optional, max 500 chars |
| `InvitationId` | `Guid?` | Correlation ID to InvitationToken (informational, not ownership) |
| `CreatedAt` | `DateTime` | Audit |
| `UpdatedAt` | `DateTime` | Audit |

### State Machine

```
         ┌──────────┐
         │ Pending   │
         └────┬──┬───┘
    Approve   │  │   Reject
              ▼  ▼
    ┌─────────┐  ┌──────────┐
    │ Approved│  │ Rejected │
    └─────────┘  └────┬─────┘
                      │ Re-request (new entity)
                      ▼
                ┌──────────┐
                │ Pending   │ (new AccessRequest)
                └──────────┘
```

**Legal transitions:**
- `Pending → Approved` (admin approve)
- `Pending → Rejected` (admin reject)
- No other transitions allowed. Approved/Rejected are terminal states.
- Re-request after rejection creates a **new** AccessRequest entity. The rejected record is preserved for audit.

**Data retention**: Rejected requests are retained for 90 days, then soft-deleted. Users can request deletion of their access request (GDPR).

### New Enum: `AccessRequestStatus`

- `Pending` — submitted, awaiting review
- `Approved` — admin approved, invitation sent
- `Rejected` — admin rejected

### New Config Key

- Key: `registration.public_enabled`
- Type: `bool`
- Default: `false`
- Managed via: SystemConfiguration bounded context (runtime, no restart)

## API Endpoints

### Public Endpoints

| Method | Path | Purpose | Rate Limit |
|--------|------|---------|------------|
| `GET` | `/api/v1/auth/registration-mode` | Returns `{ publicRegistrationEnabled: bool }` | 30/min per IP |
| `POST` | `/api/v1/auth/request-access` | Submit access request (email) | 3/min per IP |

Rate limits are **per IP address**. Exceeded → 429 with `Retry-After` header.

### Modified Endpoint

| Method | Path | Change |
|--------|------|--------|
| `POST` | `/api/v1/auth/register` | Check `registration.public_enabled` at command execution time. If `false` → 403 "Registration is currently unavailable" (generic message, does not reveal invite-only mode exists) |

**Note**: Invited users registering via `POST /auth/accept-invitation` are NOT affected by this flag. Invitation-based registration always works regardless of the toggle.

### Admin Endpoints

| Method | Path | Purpose |
|--------|------|---------|
| `GET` | `/api/v1/admin/access-requests` | List requests (paginated, filterable by status) |
| `GET` | `/api/v1/admin/access-requests/stats` | Count by status (see example below) |
| `POST` | `/api/v1/admin/access-requests/{id}/approve` | Approve → publishes domain event → invitation created async |
| `POST` | `/api/v1/admin/access-requests/{id}/reject` | Reject with optional reason |
| `POST` | `/api/v1/admin/access-requests/bulk-approve` | Approve up to 25 requests, returns per-item results |
| `PUT` | `/api/v1/admin/settings/registration-mode` | Toggle `registration.public_enabled` (lives in SystemConfiguration surface area) |

### Response Examples

**GET /admin/access-requests/stats:**
```json
{ "pending": 15, "approved": 42, "rejected": 3, "total": 60 }
```

**POST /admin/access-requests/bulk-approve (response):**
```json
{
  "processed": 10,
  "succeeded": 9,
  "failed": 1,
  "results": [
    { "id": "...", "status": "approved" },
    { "id": "...", "status": "failed", "error": "Already approved" }
  ]
}
```

**POST /auth/request-access (all outcomes return identical response):**
```json
{ "message": "If this email is eligible, you will receive an invitation when approved." }
```
HTTP 202 Accepted — same body, same timing for: new request, existing account, already pending, re-request after rejection.

## CQRS Commands & Queries

### Commands

| Command | Handler Logic | Idempotency |
|---------|---------------|-------------|
| `RequestAccessCommand(email)` | Validate email, check duplicate Pending (skip silently), check existing account (skip silently), create AccessRequest. Publishes `AccessRequestCreatedEvent`. | Natural: duplicate email check. Always returns 202. |
| `ApproveAccessRequestCommand(id, adminId)` | Guard: status must be Pending. Set Approved. Publish `AccessRequestApprovedEvent`. **Does NOT create invitation directly.** | Status guard: approving non-Pending is a no-op with success response. |
| `RejectAccessRequestCommand(id, adminId, reason?)` | Guard: status must be Pending. Set Rejected. | Status guard: rejecting non-Pending is a no-op. |
| `BulkApproveAccessRequestsCommand(ids[], adminId)` | Max 25 IDs. Dispatches individual `ApproveAccessRequestCommand` per item. Each is its own unit of work. Returns per-item results. | Per-item idempotency via status guard. |
| `SetRegistrationModeCommand(enabled, adminId)` | Routes to SystemConfiguration BC endpoint. Admin audit logged. | Setting same value is a no-op. |

### Event-Driven Invitation Flow (fixes cross-BC coupling)

```
ApproveAccessRequestCommand
  → Handler sets status = Approved, publishes AccessRequestApprovedEvent
    → Event handler subscribes, fires SendInvitationCommand(email, role: User)
      → On success: updates AccessRequest.InvitationId (eventual consistency)
      → On failure: logs error, admin can see "Approved (invitation pending)" status
                    and manually trigger resend via existing invitation UI
```

This decouples the approval operation from invitation creation. Approval is atomic. Invitation is eventual.

### Queries

| Query | Returns |
|-------|---------|
| `GetAccessRequestsQuery(page, pageSize, statusFilter?)` | Paginated `AccessRequestDto` list |
| `GetAccessRequestByIdQuery(id)` | Single `AccessRequestDto` |
| `GetAccessRequestStatsQuery` | `{ pending, approved, rejected, total }` |
| `GetRegistrationModeQuery` | `{ publicRegistrationEnabled: bool }` |

## Frontend Changes

### `/register` Page — Conditional Rendering

```
GET /api/v1/auth/registration-mode
├── true  → Existing RegisterForm (unchanged)
└── false → New RequestAccessForm
```

Registration mode check happens on page mount. If mode changes after page load and user submits the register form, backend returns 403 and frontend shows an inline error: "Registration is currently unavailable. You can request access instead." with a link/button to switch to the RequestAccessForm.

### New Component: `RequestAccessForm`

- **Fields**: Email input (required, validated)
- **Button**: "Request Access"
- **Loading state**: Spinner on button, input disabled
- **Success state**: "Your request has been submitted. You'll receive an email when approved." (replaces form)
- **Duplicate pending**: Same success message (enumeration prevention — identical response)
- **Validation error**: Inline "Please enter a valid email address"
- **Rate limit hit**: "Too many requests. Please try again in a moment."
- **Network error**: "Something went wrong. Please try again."
- **Layout**: Same card layout and branding as existing auth pages (LoginForm, RegisterForm)
- **Accessibility**: All inputs labeled, success/error announced via `aria-live="polite"`, keyboard navigable

### Admin Panel Additions

**1. Registration Settings** (admin settings section)
- Toggle switch: "Public Registration" (on/off)
- Current status indicator: "Invite-only mode" / "Public registration enabled"
- Confirmation dialog on toggle: "Are you sure? This will [enable/disable] public registration."

**2. Access Requests Page** (`/admin/users/access-requests`)
- KPI cards: Pending / Approved / Rejected / Total (same pattern as invitations page)
- Table columns: email, status, requested date, reviewed by, actions
- Row actions: "Approve" button / "Reject" button (opens dialog with optional reason, max 500 chars)
- Toolbar: "Approve Selected" bulk action (max 25, disabled if > 25 selected)
- Status filter tabs: All / Pending / Approved / Rejected
- Pending count badge on sidebar nav link

**3. Navigation**
- "Access Requests" link in admin sidebar under Users section
- Pending count badge (live-updated)

### Notifications

On new access request:
- **In-app**: Notification to all users with Admin role. Content: "New access request from {email}". Links to access requests page.
- **Email**: Digest — batched every 15 minutes if > 1 pending request, immediate if first request in window. To: all admin email addresses. Content: "{count} new access request(s) pending review."

## Security

| Concern | Mitigation |
|---------|------------|
| Request spam | Rate limit: 3 req/min per IP on `request-access`. 429 + `Retry-After` header. |
| Email enumeration | All outcomes return identical 202 response (body + timing). No AccessRequest created for existing accounts. |
| Config endpoint abuse | Rate limit: 30/min per IP on `registration-mode` |
| Unauthorized toggle | `RequireAdminSession()` on all admin endpoints |
| Email normalization | Lowercase normalization (same as invite system) |
| Registration bypass | Backend enforces 403 on `POST /register` when disabled. Generic message (no operational state leakage). |
| Config unavailability | Fail closed: if config service unreachable, registration blocked |
| Invite bypass | `POST /auth/accept-invitation` is NOT affected by toggle — invitations always work |

## Edge Cases

| Scenario | Behavior | HTTP |
|----------|----------|------|
| Email already has account | Return success silently, no entity created | 202 |
| Already pending request | Return success silently (identical response) | 202 |
| Previously rejected, requests again | Create new AccessRequest entity. Old rejected record preserved. | 202 |
| Approve succeeds, invite creation fails | AccessRequest marked Approved. Event handler retries 3x with exponential backoff. On final failure: admin sees "Approved (invitation pending)" and can resend. | 200 |
| Toggle ON while pending requests exist | Requests remain, can still be approved or ignored | — |
| Toggle OFF mid-registration (form submitted) | Backend rejects at command execution time with 403. Frontend shows inline error with option to switch to RequestAccessForm. User's entered data is NOT preserved (security: don't cache credentials client-side). | 403 |
| Approved request, invitation expires | Normal 7-day invite expiration. Admin can resend via existing invitation management UI. | — |
| Two admins approve same request simultaneously | Status guard: only first succeeds (Pending → Approved). Second is a no-op returning success. | 200 |
| Bulk approve with mixed states | Per-item processing. Already-approved items return success. Non-existent IDs return failure. | 200 |
| Config service unreachable | Fail closed: registration blocked (403) | 403 |

## Observability

### Metrics

| Metric | Type | Labels |
|--------|------|--------|
| `access_requests_total` | Counter | `status: pending\|approved\|rejected` |
| `registration_mode_toggles` | Counter | `enabled: true\|false` |
| `invitation_send_failures` | Counter | `source: access_request_approval` |
| `registration_attempts_blocked` | Counter | — |

### Structured Logging

All AccessRequest state transitions logged with:
- `correlationId`, `requestId`, `email` (hashed), `status`, `adminId` (if applicable), `timestamp`

### Alerts

- Invitation failure rate > 10% over 5 minutes → alert ops
- Access request spike > 50 in 5 minutes → potential abuse alert
- Registration mode changed → audit notification to all admins

## Concrete Scenarios

### Scenario 1: Complete Happy Path

```gherkin
Given registration mode is "invite-only"
And admin "admin@meeple.ai" is authenticated

# Phase 1: Request
When visitor submits access request with email "newuser@example.com"
Then an AccessRequest is created with status "Pending"
And response is 202 with body: { "message": "If this email is eligible..." }
And admin receives in-app notification "New access request from newuser@example.com"

# Phase 2: Approval
When admin approves the access request for "newuser@example.com"
Then the AccessRequest status changes to "Approved"
And an AccessRequestApprovedEvent is published
And an InvitationToken is created with 7-day expiration
And an invitation email is sent to "newuser@example.com"
And AccessRequest.InvitationId is set to the invitation's ID

# Phase 3: Registration
When "newuser@example.com" clicks the invitation link
Then they are directed to /accept-invite?token={token}
And they can complete registration (accept-invitation bypasses registration toggle)
And they proceed through the onboarding wizard
```

### Scenario 2: Email Enumeration Prevention

```gherkin
Scenario: Existing user requests access
  When "existing@example.com" (already registered) submits access request
  Then response is 202 with body: { "message": "If this email is eligible..." }
  And no AccessRequest entity is created
  And response timing is identical to a genuine new request

Scenario: New user requests access
  When "new@example.com" submits access request
  Then response is 202 with body: { "message": "If this email is eligible..." }
  And an AccessRequest entity is created with status "Pending"
  And response body is identical to existing-user response
```

### Scenario 3: Re-request After Rejection

```gherkin
Given user "alice@example.com" submitted an access request on 2026-03-01
And the request was rejected on 2026-03-05 with reason "Not in beta group"
When "alice@example.com" submits a new access request on 2026-03-14
Then a NEW AccessRequest entity is created with status "Pending"
And the previous rejected request remains unchanged (audit trail)
And response is 202 (identical to all other outcomes)
```

### Scenario 4: Registration Mode Decision Table

| `public_enabled` | Has valid invite token | Endpoint | Result |
|---|---|---|---|
| `true` | no | `POST /register` | Allow registration |
| `true` | yes | `POST /accept-invitation` | Allow registration |
| `false` | no | `POST /register` | 403 Forbidden |
| `false` | yes | `POST /accept-invitation` | Allow registration |
| unreachable | any | `POST /register` | 403 (fail closed) |

### Scenario 5: Bulk Approve Partial Failure

```gherkin
Given 3 pending access requests: [req-1, req-2, req-3]
And req-2 was already approved by another admin
When admin submits bulk-approve for [req-1, req-2, req-3]
Then response contains:
  | id    | status   | error              |
  | req-1 | approved |                    |
  | req-2 | approved |                    |  (no-op, already approved)
  | req-3 | approved |                    |
And processed=3, succeeded=3, failed=0
```

### Scenario 6: Toggle OFF Mid-Registration

```gherkin
Given registration mode is "public"
And user has loaded /register and is filling out the form
When admin toggles registration to "invite-only"
And user submits the registration form
Then POST /register returns 403
And frontend displays inline error: "Registration is currently unavailable"
And a "Request Access" link/button appears
And the user's password input is cleared (security)
```

## Testing Strategy

### Unit Tests

**AccessRequest Entity:**
- `Create()` factory → status is Pending, RequestedAt is set
- `Approve(adminId)` → status Approved, ReviewedAt/ReviewedBy set
- `Reject(adminId, reason)` → status Rejected, reason stored
- `Approve()` on non-Pending → throws `InvalidOperationException`
- `Reject()` on non-Pending → throws `InvalidOperationException`
- RejectionReason max 500 chars validation

**RequestAccessCommand:**
- Valid email → creates entity, returns 202
- Invalid email format → 400
- Empty email → 400
- Duplicate pending email → returns 202 (no new entity)
- Existing account email → returns 202 (no new entity)

**Registration Mode Guard:**
- Config `false` → `POST /register` returns 403
- Config `true` → `POST /register` proceeds normally
- Config unreachable → `POST /register` returns 403 (fail closed)
- `POST /accept-invitation` unaffected by config

**ApproveAccessRequestCommand:**
- Pending → Approved, event published
- Already Approved → no-op, success
- Rejected → error (illegal transition)
- Non-existent ID → 404

### Integration Tests

- Full flow: request → approve → event → invitation created → accept invitation → user account exists
- Bulk approve 5 requests → 5 invitations created, per-item results
- Bulk approve with mixed states → correct per-item reporting
- Config toggle persists across requests
- Config toggle affects `POST /register` immediately
- Notification delivery on new request (event assertion)
- Concurrent approve: two admins approve same request → only one invitation created
- Rate limit: 4th request-access within 60s → 429

### E2E Tests

**Journey 1 — Happy Path:**
- Navigate to /register → see RequestAccessForm (mode is invite-only)
- Submit email → success message shown
- Admin navigates to /admin/users/access-requests → sees pending request
- Admin clicks Approve → request moves to Approved
- (Invitation sent — verified in integration test)

**Journey 2 — Admin Toggle:**
- Admin enables public registration from settings
- Navigate to /register → see RegisterForm
- Admin disables public registration
- Navigate to /register → see RequestAccessForm

**Journey 3 — Bulk Approve:**
- Admin selects multiple pending requests → clicks "Approve Selected"
- All selected move to Approved status
- KPI cards update

**Accessibility:**
- RequestAccessForm: screen reader announces form purpose, success/error states via aria-live
- Admin toggle: keyboard navigable, confirmation dialog accessible
- Access requests table: sortable, filterable, keyboard navigable

### Test Data Setup

- Seed admin account via existing test infrastructure
- Create pending/approved/rejected AccessRequests via factory helpers
- Use Testcontainers PostgreSQL (existing pattern)
- Test isolation: database cleanup between tests

## Architecture Notes

- `AccessRequest` lives in Authentication bounded context alongside `InvitationToken`
- Approval publishes `AccessRequestApprovedEvent` → event handler creates invitation (decoupled, eventual consistency)
- `SetRegistrationModeCommand` routes to SystemConfiguration BC endpoint directly (admin endpoint lives in SystemConfiguration surface area, no cross-BC write)
- `InvitationId` on AccessRequest is a correlation ID for traceability, not an ownership reference
- Frontend uses a single API call on `/register` mount to determine which form to render
- All endpoints follow CQRS pattern: commands/queries via MediatR only
- Audit logging for all admin actions (approve, reject, toggle) via existing Administration BC patterns
