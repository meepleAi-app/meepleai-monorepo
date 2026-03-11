# User Invitation System & Onboarding Wizard — Design Spec

> **Date:** 2026-03-11
> **Status:** Approved
> **Scope:** Admin user invitation flow (single + bulk), accept-invite with onboarding wizard, forced password change

---

## Problem Statement

Administrators need to invite users to MeepleAI by email. Currently, user creation (`CreateUserCommand`) requires setting a password at creation time with no invitation workflow. There is no mechanism for:

- Sending invitation emails with a secure token link
- Allowing invited users to set their own password
- Forcing password change on first login
- Guided onboarding for new users

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Password handling | No default password — token-based invite | More secure; no password in transit; reuses existing token patterns |
| Token storage | SHA256 hash only | Same pattern as PasswordReset; plaintext only in email |
| Token expiry | 7 days | Longer than password reset (24h) since invites need more time |
| Resend behavior | New token, old marked Expired | One active invite per email at a time |
| Revoke | No explicit revoke | Token expires naturally in 7 days |
| Admin UI | B+C: inline in user list + dedicated page | Quick visibility + full management power |
| Onboarding | 5-step wizard, only password mandatory | Low friction; user can skip and explore later |
| Bounded context | Authentication (token + accept) + Administration (admin endpoints) | Follows existing separation |
| Bulk invite | CSV upload (email,role per row), max 100 per batch | Matches existing BulkImportUsersCommand pattern |

---

## 1. Domain Model

### New Entity: `InvitationToken` (Authentication BC)

| Field | Type | Constraints |
|-------|------|-------------|
| `Id` | `Guid` | PK |
| `Email` | `string` | Required, max 256, normalized lowercase |
| `Role` | `UserRole` | Required (User, Editor, Admin) |
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

### Modified Entity: `UserEntity`

Add field:

| Field | Type | Default | Purpose |
|-------|------|---------|---------|
| `MustChangePassword` | `bool` | `false` | Set `true` on invitation accept; forces redirect to `/change-password` |

**Guard behavior:** Authenticated layout middleware checks `user.mustChangePassword`. If `true`, redirect to `/change-password`. The change-password page clears the flag on success via existing `ChangePasswordCommand` (modified to set `MustChangePassword = false`).

---

## 2. Backend: Commands & Handlers

### New Commands (Authentication BC)

#### `SendInvitationCommand`
- **Input:** `Email`, `Role`
- **Auth:** Admin+ role required
- **Validation:** Valid email format, role != SuperAdmin, no existing Pending invite for email, no existing active user with email
- **Handler:**
  1. Generate cryptographically random token (32 bytes, base64url)
  2. Create `InvitationToken` entity with `SHA256(token)` as `TokenHash`
  3. Call `IEmailService.SendInvitationEmailAsync(email, adminName, role, inviteUrl, expiresAt)`
  4. Return `InvitationDto` (id, email, role, status, expiresAt)
- **Audit:** `[AuditableAction]`

#### `BulkSendInvitationsCommand`
- **Input:** `CsvContent` (string) or `CsvFile` (IFormFile)
- **Auth:** Admin+ role required
- **CSV format:** `email,role` per row (header optional)
- **Validation:** Each row validated individually; max 100 invites per batch
- **Handler:**
  1. Parse CSV, validate each row
  2. For each valid row: execute `SendInvitationCommand` logic
  3. Collect results: `{ successful: InvitationDto[], failed: { email, error }[] }`
- **Audit:** `[AuditableAction]` with batch metadata

#### `AcceptInvitationCommand`
- **Input:** `Token` (plaintext from URL), `Password`, `ConfirmPassword`
- **Auth:** Unauthenticated (public endpoint)
- **Validation:** Password min 8 chars, upper + lower + digit, passwords match, token not empty
- **Handler:**
  1. Hash token with SHA256, find `InvitationToken` by hash
  2. Validate: status == `Pending`, `ExpiresAt > now`
  3. Create `UserEntity` with email from invitation, hashed password, assigned role, `MustChangePassword = true`
  4. Mark invitation as `Accepted`, set `AcceptedAt` and `AcceptedByUserId`
  5. Create session (auto-login) — return auth cookie + user data
- **Audit:** `[AuditableAction]`

#### `ResendInvitationCommand`
- **Input:** `InvitationId` (Guid)
- **Auth:** Admin+ role required
- **Handler:**
  1. Find existing invitation by Id
  2. Mark as `Expired`
  3. Generate new token, create new `InvitationToken` for same email + role
  4. Send email
  5. Return new `InvitationDto`
- **Audit:** `[AuditableAction]`

### New Queries

#### `GetInvitationsQuery`
- **Input:** `Status?` (filter), `Page`, `PageSize`
- **Auth:** Admin+
- **Returns:** `PaginatedResult<InvitationDto>`

#### `GetInvitationStatsQuery`
- **Auth:** Admin+
- **Returns:** `{ pending: int, accepted: int, expired: int, total: int }`

#### `ValidateInvitationTokenQuery`
- **Input:** `Token` (plaintext)
- **Auth:** Unauthenticated
- **Returns:** `{ valid: bool, email: string?, role: string?, expiresAt: DateTime? }`

---

## 3. Backend: Endpoints

### Admin Endpoints (AdminUserEndpoints.cs)

```
POST   /api/v1/admin/users/invite                    → SendInvitationCommand
POST   /api/v1/admin/users/bulk/invite                → BulkSendInvitationsCommand
POST   /api/v1/admin/users/invitations/{id}/resend    → ResendInvitationCommand
GET    /api/v1/admin/users/invitations                → GetInvitationsQuery
GET    /api/v1/admin/users/invitations/stats           → GetInvitationStatsQuery
```

### Public Auth Endpoints (AuthenticationEndpoints.cs)

```
POST   /api/v1/auth/accept-invitation                 → AcceptInvitationCommand
GET    /api/v1/auth/validate-invitation?token=X        → ValidateInvitationTokenQuery
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

### Route: `/accept-invite` (auth route group, unauthenticated)

**Entry flow:**
1. URL: `/accept-invite?token=xxx`
2. Call `ValidateInvitationTokenQuery` to check token
3. If invalid/expired → error page with "Contact your administrator" message
4. If valid → show `OnboardingWizard`

### OnboardingWizard (5 steps)

| Step | Required | Component | API Call |
|------|----------|-----------|----------|
| 1. Set Password | Yes | `PasswordStep` | `AcceptInvitationCommand` (creates user + auto-login) |
| 2. Profile | Skippable | `ProfileStep` | `UpdateUserProfileCommand` |
| 3. Interests | Skippable | `InterestsStep` | `UpdateUserPreferencesCommand` (or new) |
| 4. First Game | Skippable | `FirstGameStep` | `AddGameToLibraryCommand` |
| 5. First Agent | Skippable | `FirstAgentStep` | `CreateAgentDefinitionCommand` |

**Step 1 (Password)** is the critical step — it calls `AcceptInvitationCommand` which creates the user and returns an auth session. Steps 2-5 execute as authenticated API calls.

**Components:**
- `OnboardingWizard` — stepper with progress bar, skip/next/back navigation
- `PasswordStep` — password + confirm fields, strength meter, validation rules display (reuse pattern from `/reset-password`)
- `ProfileStep` — display name input + avatar upload
- `InterestsStep` — checkbox grid with game category icons (Strategy, Party, Cooperative, Family, Thematic, Abstract, Card, Dice, Miniatures)
- `FirstGameStep` — search bar with debounce → catalog results as cards → click to add
- `FirstAgentStep` — conditional: if game was added in step 4, show "Create an AI assistant for {GameName}?" toggle + agent name input. If no game, auto-skip.

**Post-completion:** redirect to `/` (home dashboard)

### MustChangePassword Guard

**Location:** `(authenticated)/layout.tsx` or middleware

**Logic:**
- If `user.mustChangePassword === true` AND current path is NOT `/change-password` → redirect to `/change-password`
- The `/change-password` page already exists (uses `ChangePasswordCommand`)
- Add a banner: "You must change your password to continue"
- Modify `ChangePasswordCommand` handler: after successful change, set `MustChangePassword = false`

**Note:** The guard is a safety net. Normal invite flow goes through the wizard (step 1 sets password). The guard catches edge cases: user bookmarks the app, session persists, etc.

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
- Data source: pending invitations mixed into user list query (or separate query merged client-side)

### B) Dedicated Page (`/admin/users/invitations`)

- **Sidebar entry:** "Invitations" under Users section with pending count badge
- **Header:** title + "Invite User" button + "Bulk Invite (CSV)" button
- **Filter tabs:** All / Pending / Accepted / Expired (with counts)
- **Table columns:** Email, Role (badge), Status (badge), Sent date, Expires/Accepted date, Actions
- **Actions per row:** Resend (for Pending and Expired)
- **Bulk invite dialog:** CSV file upload (drag & drop), preview table with per-row validation, confirm send, results summary

### Shared Components

- `InviteUserDialog` — modal form: email input + role select dropdown → shared between user list and invitations page
- `BulkInviteDialog` — CSV upload with drag & drop, preview table, validation feedback, send confirmation
- `InvitationStatusBadge` — Pending (amber), Accepted (green), Expired (red)
- `InvitationRow` — reusable table row component for both views

### Admin API Client

New sub-client `createInvitationsClient()` registered in `createApiClient()`:

```typescript
sendInvitation(email: string, role: string): Promise<InvitationDto>
bulkSendInvitations(csv: File): Promise<BulkInviteResult>
resendInvitation(id: string): Promise<InvitationDto>
getInvitations(filters: InvitationFilters): Promise<PaginatedResult<InvitationDto>>
getInvitationStats(): Promise<InvitationStats>
validateInvitationToken(token: string): Promise<TokenValidation>
```

---

## 7. Database Migration

**Migration name:** `AddInvitationTokenAndMustChangePassword`

**Changes:**
1. New table `InvitationTokens`:
   - All fields from domain model (Section 1)
   - FK `InvitedByUserId` → `Users(Id)` (ON DELETE RESTRICT)
   - FK `AcceptedByUserId` → `Users(Id)` (ON DELETE SET NULL)
   - Unique index on `TokenHash`
   - Composite index on `(Email, Status)` for fast lookup
   - Index on `ExpiresAt` for cleanup queries
2. Alter table `Users`:
   - Add column `MustChangePassword` (`bool`, NOT NULL, DEFAULT false)

---

## 8. Testing Strategy

| Layer | Test Scope | Tool | Count (est.) |
|-------|-----------|------|--------------|
| Unit | `SendInvitationCommandHandler` — creates token, hashes, calls email | xUnit | 5-6 |
| Unit | `AcceptInvitationCommandHandler` — validates token, creates user, sets MustChangePassword | xUnit | 6-8 |
| Unit | `ResendInvitationCommandHandler` — expires old, creates new | xUnit | 3-4 |
| Unit | `BulkSendInvitationsCommandHandler` — CSV parsing, validation, batch results | xUnit | 5-6 |
| Unit | `ValidateInvitationTokenQueryHandler` — valid/expired/invalid cases | xUnit | 3-4 |
| Unit | `InvitationStatusBadge`, `InviteUserDialog`, `BulkInviteDialog` rendering | Vitest | 8-10 |
| Unit | `invitationsClient` — all API methods, error handling | Vitest | 6-8 |
| Unit | `OnboardingWizard` — step navigation, skip, back, completion | Vitest | 8-10 |
| Unit | `PasswordStep`, `ProfileStep`, `InterestsStep`, `FirstGameStep`, `FirstAgentStep` | Vitest | 10-12 |
| Integration | Token expiry enforcement, resend invalidation, unique constraint | xUnit + Testcontainers | 4-5 |
| Integration | MustChangePassword flag lifecycle (set on accept, clear on change) | xUnit + Testcontainers | 2-3 |
| E2E | Admin: invite single user, verify in list + dedicated page | Playwright | 2-3 |
| E2E | Admin: bulk CSV invite, verify results | Playwright | 1-2 |
| E2E | Admin: resend expired invitation | Playwright | 1 |
| E2E | User: accept invite → password → skip onboarding → lands on home | Playwright | 1-2 |
| E2E | User: accept invite → full onboarding (all 5 steps) | Playwright | 1-2 |
| E2E | User: expired token → error page | Playwright | 1 |

**Estimated total:** 60-80 tests
**Coverage target:** 90%+ backend, 85%+ frontend

---

## 9. Security Considerations

- **Token:** 32 bytes cryptographically random, base64url encoded, stored as SHA256 hash only
- **Rate limit:** Max 10 invitations per minute per admin session (prevent spam)
- **Email validation:** Normalize to lowercase, validate format before sending
- **Role escalation:** Cannot invite as SuperAdmin (validator rejects)
- **Token reuse:** One-time use; marked Accepted after first use
- **Brute force:** Rate limit on `/auth/accept-invitation` endpoint (5 attempts per minute per IP)
- **CSRF:** AcceptInvitationCommand is POST with token in body (not query param for logging safety)

---

## 10. Out of Scope

- Email template editor/customizer (use hardcoded HTML template)
- Invitation revoke/cancel (expires naturally in 7 days)
- Custom expiry per invitation (always 7 days)
- Invitation analytics/reporting beyond basic stats
- SSO/SAML invitation integration
- Voice features (already implemented: `useVoiceInput`, `useVoiceOutput`, Web Speech API)
- Audit log system (already implemented: `AuditLoggingBehavior` pipeline)
- Role management UI (already implemented: `ChangeUserRoleCommand`)
