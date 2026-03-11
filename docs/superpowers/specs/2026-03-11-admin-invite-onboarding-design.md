# User Invitation System & Onboarding Wizard — Design Spec

> **Date:** 2026-03-11
> **Status:** Approved (spec review v2)
> **Scope:** Admin user invitation flow (single + bulk), accept-invite with onboarding wizard

---

## Problem Statement

Administrators need to invite users to MeepleAI by email. Currently, user creation (`CreateUserCommand`) requires setting a password at creation time with no invitation workflow. There is no mechanism for:

- Sending invitation emails with a secure token link
- Allowing invited users to set their own password
- Guided onboarding for new users

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Password handling | No default password — token-based invite | More secure; no password in transit; reuses existing token patterns |
| Token storage | SHA256 hash only | Same pattern as PasswordReset; plaintext only in email |
| Token expiry | 7 days | Longer than password reset (24h) since invites need more time |
| Resend behavior | New token, old marked Expired | One active invite per email at a time |
| Revoke | No explicit revoke | Token expires naturally in 7 days; admin can resend (which expires old token) to effectively cancel+renew |
| Admin UI | B+C: inline in user list + dedicated page | Quick visibility + full management power |
| Onboarding | 5-step wizard, only password mandatory | Low friction; user can skip and explore later |
| Bounded context | Authentication (token + accept) + Administration (admin endpoints) | Follows existing separation |
| Bulk invite | CSV upload (email,role per row), max 100 per batch | Matches existing BulkImportUsersCommand pattern |
| MustChangePassword | NOT used for invitation flow | User sets their own password in Step 1 — no "default password" to change. Flag reserved for future admin-forced password resets. |
| Role storage | `string` (not enum) | Matches existing `UserEntity.Role` convention; `UserRole` enum is domain logic only |

---

## 1. Domain Model

### New Entity: `InvitationToken` (Authentication BC)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `Guid` | PK |
| `Email` | `string` | Required, max 256, normalized lowercase |
| `Role` | `string` | Required, stored as string (matches `UserEntity.Role` convention) |
| `TokenHash` | `string` | Required, unique index, SHA256 of plaintext token |
| `InvitedByUserId` | `Guid` | FK → Users, required |
| `Status` | `InvitationStatus` | Required, default `Pending` |
| `ExpiresAt` | `DateTime` | Required, CreatedAt + 7 days |
| `AcceptedAt` | `DateTime?` | Set when user completes password step |
| `AcceptedByUserId` | `Guid?` | FK → Users, set on accept |
| `CreatedAt` | `DateTime` | Audit |

**`InvitationStatus` enum:** `Pending`, `Accepted`, `Expired`

**Domain rules:**
- Max 1 `Pending` invitation per email at any time
- Resend = mark old as `Expired`, create new with fresh token
- Token validated by: hash match + status == `Pending` + `ExpiresAt > now`
- SuperAdmin role cannot be assigned via invitation (admin-only escalation)
- Allowed roles for invitation: `"User"`, `"Editor"`, `"Admin"` (validated as strings matching `AllowedRoles` array)

### UserEntity — No Modifications

The invitation flow does **not** add `MustChangePassword` to `UserEntity`. The user sets their own password during the wizard's Step 1 (`AcceptInvitationCommand`). There is no "default password" to force-change. A `MustChangePassword` feature for admin-forced resets is out of scope and can be added independently in the future.

---

## 2. Backend: Commands & Handlers

### New Commands (Authentication BC)

#### `SendInvitationCommand`
- **Input:** `Email`, `Role`
- **Auth:** Admin+ role required. `InvitedByUserId` extracted from authenticated session in the handler (not part of command record).
- **Validation:** Valid email format, role in `["User", "Editor", "Admin"]` (string match), no existing Pending invite for email, no existing active user with email
- **Handler:**
  1. Generate cryptographically random token (32 bytes, base64url)
  2. Create `InvitationToken` entity with `SHA256(token)` as `TokenHash`
  3. Call `IEmailService.SendInvitationEmailAsync(email, adminName, role, inviteUrl, expiresAt)`
  4. Return `InvitationDto` (id, email, role, status, expiresAt)
- **Audit:** `[AuditableAction]`

#### `BulkSendInvitationsCommand`
- **Input:** `CsvContent` (string only — endpoint layer converts `IFormFile` to string before dispatching)
- **Auth:** Admin+ role required
- **CSV format:** `email,role` per row (header optional)
- **Validation:** Each row validated individually; max 100 invites per batch
- **Handler:**
  1. Parse CSV, validate each row
  2. For each valid row: execute `SendInvitationCommand` logic
  3. Collect results: `{ successful: InvitationDto[], failed: { email, error }[] }`
- **Audit:** `[AuditableAction]` with batch metadata

#### `AcceptInvitationCommand`
- **Input:** `Token` (plaintext from request body), `Password`, `ConfirmPassword`
- **Auth:** Unauthenticated (public endpoint)
- **Validation:** Password min 8 chars, upper + lower + digit, passwords match, token not empty
- **Handler:**
  1. Hash token with SHA256, find `InvitationToken` by hash
  2. Validate: status == `Pending`, `ExpiresAt > now`
  3. Create `UserEntity` with email from invitation, hashed password, assigned role, `EmailVerified = true` (admin-supplied email is trusted)
  4. Mark invitation as `Accepted`, set `AcceptedAt` and `AcceptedByUserId`
  5. Create session (auto-login) — return auth cookie + user data
- **Audit:** `[AuditableAction]` — note: audit record will have `adminUserId = null` since this is an unauthenticated endpoint. The `InvitedByUserId` on the `InvitationToken` entity provides the audit trail to the inviting admin. This is expected and acceptable.

#### `ResendInvitationCommand`
- **Input:** `InvitationId` (Guid)
- **Auth:** Admin+ role required
- **Validation:** Invitation must exist. Status must be `Pending` or `Expired` (not `Accepted`). No active user must exist for the invitation's email.
- **Handler:**
  1. Find existing invitation by Id
  2. Validate status and email (see above)
  3. Mark as `Expired`
  4. Generate new token, create new `InvitationToken` for same email + role
  5. Send email
  6. Return new `InvitationDto`
- **Audit:** `[AuditableAction]`

### New Queries

#### `GetInvitationsQuery`
- **Input:** `Status?` (filter), `Page`, `PageSize`, `SortBy` (default: `CreatedAt DESC`)
- **Auth:** Admin+
- **Returns:** `PaginatedResult<InvitationDto>`

#### `GetInvitationStatsQuery`
- **Auth:** Admin+
- **Returns:** `{ pending: int, accepted: int, expired: int, total: int }`

#### `ValidateInvitationTokenQuery`
- **Input:** `Token` (plaintext, sent in POST body — not GET query param, to avoid server log exposure)
- **Auth:** Unauthenticated
- **Returns:** `{ valid: bool, role: string?, expiresAt: DateTime? }` — **email is NOT returned** to prevent user-enumeration via forwarded invite links. The email is shown to the user only after they complete `AcceptInvitationCommand`.

---

## 3. Backend: Endpoints

### Admin Endpoints (AdminUserEndpoints.cs)

```
POST   /api/v1/admin/users/invite                    → SendInvitationCommand
POST   /api/v1/admin/users/bulk/invite                → BulkSendInvitationsCommand (endpoint reads IFormFile, converts to string CsvContent)
POST   /api/v1/admin/users/invitations/{id}/resend    → ResendInvitationCommand
GET    /api/v1/admin/users/invitations                → GetInvitationsQuery
GET    /api/v1/admin/users/invitations/stats           → GetInvitationStatsQuery
```

### Public Auth Endpoints (AuthenticationEndpoints.cs)

```
POST   /api/v1/auth/accept-invitation                 → AcceptInvitationCommand (token in body)
POST   /api/v1/auth/validate-invitation               → ValidateInvitationTokenQuery (token in body, POST to avoid server log exposure)
```

---

## 4. Email Template

**Method:** `IEmailService.SendInvitationEmailAsync(email, inviterName, role, inviteUrl, expiresAt)`

**Email content:**
- **Subject:** "You're invited to MeepleAI"
- **Body:** HTML branded template following existing email style
  - Logo header
  - "{InviterName} has invited you to join MeepleAI as {Role}"
  - CTA button: "Accept Invitation" → `{Frontend:BaseUrl}/accept-invite?token={token}`
  - Expiry notice: "This invitation expires on {expiresAt:format}"
  - Fallback: plaintext link below button
  - Footer: "If you didn't expect this invitation, you can safely ignore this email."

---

## 5. Frontend: Accept Invitation & Onboarding

### Route: `/accept-invite` — new `(onboarding)` route group

The accept-invite page and onboarding wizard live in a **new `(onboarding)` route group** with its own full-width layout. The `(auth)` group layout (max-width ~450px centered card) is too narrow for game search and agent creation steps.

**`(onboarding)/layout.tsx`:** Minimal header (logo only), full-width content area (max-width 768px centered), no sidebar, no navigation.

**Entry flow:**
1. URL: `/accept-invite?token=xxx`
2. Extract token from URL, call `ValidateInvitationTokenQuery` (POST with token in body)
3. If invalid/expired → error page with "This invitation has expired or is invalid. Contact your administrator." message
4. If valid → show `OnboardingWizard`

### OnboardingWizard (5 steps)

| Step | Required | Component | API Call | Notes |
|------|----------|-----------|----------|-------|
| 1. Set Password | Yes | `PasswordStep` | `AcceptInvitationCommand` (creates user + auto-login) | After this step, user is authenticated |
| 2. Profile | Skippable | `ProfileStep` | `UpdateUserProfileCommand` | Display name + avatar |
| 3. Interests | Skippable | `InterestsStep` | `SaveUserInterestsCommand` (new) | See Section 5A |
| 4. First Game | Skippable | `FirstGameStep` | `AddGameToLibraryCommand` | Search catalog |
| 5. First Agent | Skippable/Auto-skip | `FirstAgentStep` | `CreateAgentDefinitionCommand` | Only shown if game added in step 4 |

**Step 1 (Password)** is the critical step — it calls `AcceptInvitationCommand` which creates the user and returns an auth session. Steps 2-5 execute as authenticated API calls.

**Progress bar behavior:** Shows total step count dynamically. If step 5 is auto-skipped (no game added in step 4), the progress bar shows 4 steps total, not 5 with one greyed out. Implementation: `totalSteps` is computed from wizard state — `hasGame ? 5 : 4`.

**Components:**
- `OnboardingWizard` — stepper with dynamic progress bar, skip/next/back navigation. "Skip wizard" link in header (skips remaining steps, redirects to home).
- `PasswordStep` — password + confirm fields, strength meter, validation rules display (reuse pattern from `/reset-password`)
- `ProfileStep` — display name input + avatar upload
- `InterestsStep` — checkbox grid with game category icons (Strategy, Party, Cooperative, Family, Thematic, Abstract, Card, Dice, Miniatures)
- `FirstGameStep` — search bar with debounce → catalog results as cards → click to add
- `FirstAgentStep` — conditional render: shown only if game was added in step 4. Toggle "Create an AI assistant for {GameName}?" + agent name input.

**Post-completion:** redirect to `/` (home dashboard)

### Section 5A: User Interests (New Backend)

**New command: `SaveUserInterestsCommand`**
- **Input:** `Interests` (string array — category names)
- **Auth:** Authenticated user
- **Handler:** Saves interests as JSONB on UserEntity
- **Migration:** Add `Interests` column (`jsonb`, nullable, default `null`) to `Users` table — included in the same migration as `InvitationTokens` table.

**UserEntity change:** Add `Interests` property (`List<string>?`) with JSONB backing field (same pattern as `AgentDefinition.KbCardIds`).

---

## 6. Frontend: Admin Invitation UI

### C) Inline in User List (`/admin/users`)

- Pending invitations appear as rows in the existing users table
- Visual differentiation:
  - Row background: `bg-amber-50`
  - Avatar: mail icon with dashed amber border (instead of initials circle)
  - Status badge: "Invited" in amber
  - Subtitle: "Invited X ago · expires in Xd Xh"
- Actions: "Resend" button inline
- Data source: pending invitations fetched via separate `getInvitations({ status: 'Pending' })` call, merged client-side into user list

### B) Dedicated Page (`/admin/users/invitations`)

- **Sidebar entry:** "Invitations" under Users section with pending count badge
- **Header:** title + "Invite User" button + "Bulk Invite (CSV)" button
- **Filter tabs:** All / Pending / Accepted / Expired (with counts from `getInvitationStats()`)
- **Table columns:** Email, Role (badge), Status (badge), Sent date, Expires/Accepted date, Actions
- **Actions per row:** Resend (for Pending and Expired rows)
- **Bulk invite dialog:** CSV file upload (drag & drop), preview table with per-row validation, confirm send, results summary (success/failure counts)

### Shared Components

- `InviteUserDialog` — modal form: email input + role select dropdown → shared between user list and invitations page
- `BulkInviteDialog` — CSV upload with drag & drop, preview table, validation feedback, send confirmation
- `InvitationStatusBadge` — Pending (amber), Accepted (green), Expired (red)
- `InvitationRow` — reusable table row component for both views

### Admin API Client

New sub-client `createInvitationsClient()` registered in `createApiClient()`:

```typescript
sendInvitation(email: string, role: string): Promise<InvitationDto>
bulkSendInvitations(csvContent: string): Promise<BulkInviteResult>
resendInvitation(id: string): Promise<InvitationDto>
getInvitations(filters: InvitationFilters): Promise<PaginatedResult<InvitationDto>>
getInvitationStats(): Promise<InvitationStats>
validateInvitationToken(token: string): Promise<TokenValidation>
```

Note: `bulkSendInvitations` receives `string` (the endpoint component reads the file and sends content). The `BulkInviteDialog` component reads the `File` to string client-side before calling the API.

---

## 7. Database Migration

**Migration name:** `AddInvitationTokensAndUserInterests`

**Changes:**
1. New table `InvitationTokens`:
   - All fields from domain model (Section 1)
   - FK `InvitedByUserId` → `Users(Id)` (ON DELETE RESTRICT)
   - FK `AcceptedByUserId` → `Users(Id)` (ON DELETE SET NULL)
   - Unique index on `TokenHash`
   - Composite index on `(Email, Status)` for fast lookup
   - Index on `ExpiresAt` for cleanup queries
2. Alter table `Users`:
   - Add column `Interests` (`jsonb`, nullable, default `null`)

---

## 8. Testing Strategy

| Layer | Test Scope | Tool | Count (est.) |
|-------|-----------|------|--------------|
| Unit | `SendInvitationCommandHandler` — creates token, hashes, calls email, extracts admin from session | xUnit | 5-6 |
| Unit | `AcceptInvitationCommandHandler` — validates token, creates user, sets EmailVerified=true, null audit context | xUnit | 6-8 |
| Unit | `ResendInvitationCommandHandler` — expires old, creates new, rejects if Accepted, rejects if user exists | xUnit | 5-6 |
| Unit | `BulkSendInvitationsCommandHandler` — CSV parsing, validation, batch results, max 100 limit | xUnit | 5-6 |
| Unit | `ValidateInvitationTokenQueryHandler` — valid/expired/invalid cases, no email in response | xUnit | 3-4 |
| Unit | `SaveUserInterestsCommandHandler` — saves JSONB, validates categories | xUnit | 2-3 |
| Unit | `InvitationStatusBadge`, `InviteUserDialog`, `BulkInviteDialog` rendering | Vitest | 8-10 |
| Unit | `invitationsClient` — all API methods, error handling | Vitest | 6-8 |
| Unit | `OnboardingWizard` — step navigation, skip, back, completion, auto-skip step 5, dynamic progress bar | Vitest | 10-12 |
| Unit | `PasswordStep`, `ProfileStep`, `InterestsStep`, `FirstGameStep`, `FirstAgentStep` | Vitest | 10-12 |
| Integration | Token expiry enforcement, resend invalidation, unique constraint, resend-already-expired idempotency | xUnit + Testcontainers | 5-6 |
| E2E | Admin: invite single user, verify in user list (amber row) + dedicated page | Playwright | 2-3 |
| E2E | Admin: bulk CSV invite, verify results summary | Playwright | 1-2 |
| E2E | Admin: resend expired invitation | Playwright | 1 |
| E2E | User: accept invite → password → skip onboarding → lands on home | Playwright | 1-2 |
| E2E | User: accept invite → full onboarding (all 5 steps) | Playwright | 1-2 |
| E2E | User: expired token → error page | Playwright | 1 |

**Estimated total:** 65-85 tests
**Coverage target:** 90%+ backend, 85%+ frontend

---

## 9. Security Considerations

- **Token:** 32 bytes cryptographically random, base64url encoded, stored as SHA256 hash only
- **Token transmission:** Always in POST body, never in GET query params (prevents server log/referrer/browser history exposure). Note: the initial email link uses `?token=xxx` in the URL for UX — the frontend extracts it and sends via POST to the API.
- **Rate limit:** Max 10 invitations per minute per admin session (prevent spam)
- **Validate endpoint rate limit:** Max 5 attempts per minute per IP (prevent brute force)
- **Email validation:** Normalize to lowercase, validate format before sending
- **Role escalation:** Cannot invite as SuperAdmin (validator rejects; string match against allowlist)
- **Token reuse:** One-time use; marked Accepted after first use
- **User enumeration:** `ValidateInvitationTokenQuery` does NOT return email — prevents enumeration via forwarded links
- **Audit trail:** `AcceptInvitationCommand` audit records have `adminUserId = null` (unauthenticated context); the inviting admin is traceable via `InvitationToken.InvitedByUserId`

---

## 10. Out of Scope

- Email template editor/customizer (use hardcoded HTML template)
- Invitation revoke/cancel button (expires naturally in 7 days; resend effectively cancels+renews)
- Custom expiry per invitation (always 7 days)
- Invitation analytics/reporting beyond basic stats
- SSO/SAML invitation integration
- `MustChangePassword` / forced password change feature (separate future feature — not needed for invitation flow since user sets own password)
- `/change-password` standalone page (not needed without `MustChangePassword`)
- Voice features (already implemented: `useVoiceInput`, `useVoiceOutput`, Web Speech API)
- Audit log system (already implemented: `AuditLoggingBehavior` pipeline)
- Role management UI (already implemented: `ChangeUserRoleCommand`)

---

## Appendix: Spec Review Fixes Applied (v2)

Issues found during automated spec review and their resolutions:

| # | Severity | Issue | Resolution |
|---|----------|-------|------------|
| 1 | CRITICAL | `MustChangePassword` deadlock — user sets password in Step 1 but flag forces redirect to nonexistent `/change-password` | Removed `MustChangePassword` from invitation flow entirely. User sets own password — no forced change needed. |
| 2 | CRITICAL | `AcceptInvitationCommand` sets `MustChangePassword = true` immediately after user chose password | Same as above — flag removed from this flow |
| 3 | HIGH | `UpdateUserPreferencesCommand` does not exist | Specified new `SaveUserInterestsCommand` with JSONB `Interests` field on UserEntity (Section 5A) |
| 4 | HIGH | `/change-password` page does not exist | Removed from scope — not needed without `MustChangePassword` |
| 5 | HIGH | `AcceptInvitationCommand` audit has null adminUserId | Documented as expected; `InvitedByUserId` provides admin audit trail |
| 6 | HIGH | `ValidateInvitationTokenQuery` returns email (user enumeration risk) | Removed `email` from response — only `valid`, `role`, `expiresAt` returned |
| 7 | HIGH | `IFormFile` in MediatR command violates CQRS | Endpoint converts `IFormFile` → `string`; command receives only `CsvContent: string` |
| 8 | MEDIUM | `ResendInvitationCommand` doesn't check if invitation was Accepted | Added validation: status must be `Pending` or `Expired`, and no active user for email |
| 9 | MEDIUM | Resend-already-expired case not explicitly tested | Added to integration test scope |
| 10 | MEDIUM | Auto-skip step 5 creates hidden navigation branch | Documented: `totalSteps` computed dynamically; progress bar shows 4 or 5 steps |
| 11 | MEDIUM | `UserRole` enum vs string mismatch | Changed `InvitationToken.Role` to `string`; documented enum is domain logic only |
| 12 | MEDIUM | `(auth)` route group too narrow for wizard | Created new `(onboarding)` route group with full-width layout |
| 13 | MEDIUM | GET validate endpoint logs token in server logs | Changed to POST with token in body (consistent with accept endpoint) |
| 14 | LOW | `InvitedByUserId` not in command inputs | Documented: extracted from authenticated session in handler |
| 15 | LOW | `GetInvitationsQuery` missing sort order | Added `SortBy` param with default `CreatedAt DESC` |
| 16 | LOW | No documentation on how to cancel a pending invite | Added note: resend effectively cancels (expires old token) + renews |
| 17 | LOW | `[AuditableAction]` on unauthenticated command | Documented: null adminUserId is expected and acceptable |
