# Admin Invitation Flow — Design Spec

**Date**: 2026-03-17
**Status**: Draft (reviewed, all issues resolved)
**Approach**: Hybrid — extend existing invitation system (Authentication BC)
**Review Score**: 5.6/10 → fixes applied for all Critical/High issues

## Overview

Admin creates a user account and sends an invitation in one action. The system creates a "pending" user, sends an email with a setup link, custom message, and platform intro. The invited user sets their password, gets auto-logged in, goes through a reduced onboarding (skipping admin-configured fields), and finds pre-added or suggested games in their collection/dashboard.

## User Flow

```
Admin                           System                          Invited User
──────                          ──────                          ────────────
Fill form (email, role,    →    Create User (Pending)
tier, message, games,           Create InvitationToken
expiry)                         Send invitation email       →   Receives email
                                                                (link + admin message
                                                                 + platform intro)

                                                                Clicks link
                                                            →   /setup-account?token=xxx
                                                                Sets password
                                                            →   POST /activate-account
                                Validate token
                                Activate user (Pending→Active)
                                Create session (auto-login)
                                                            →   Reduced onboarding
                                                                (avatar, preferences)
                                Game suggestions processed  →   Dashboard
                                (async, post-activation)        (pre-added games in
                                                                 collection, suggested
                                                                 games highlighted)
```

## Key Examples

### Example 1: Happy Path — Single Invitation

```
Given: Admin "Marco" is logged in with role=Admin
And: Game "Catan" (id=aaa-111) exists in SharedGameCatalog
And: Game "Azul" (id=bbb-222) exists in SharedGameCatalog
And: No user exists with email "luca@example.com"

When: Admin submits POST /api/v1/admin/invitations with:
  {
    "email": "luca@example.com",
    "displayName": "Luca Bianchi",
    "role": "User",
    "tier": "Premium",
    "customMessage": "Benvenuto nel gruppo MeepleAI!",
    "expiresInDays": 14,
    "gameSuggestions": [
      { "gameId": "aaa-111", "type": "PreAdded" },
      { "gameId": "bbb-222", "type": "Suggested" }
    ]
  }

Then: System creates User(status=Pending, email="luca@example.com", role=User, tier=Premium)
And: System creates InvitationToken(pendingUserId=<new-user-id>, expiresAt=2026-03-31)
And: System sends email to "luca@example.com" containing:
  - Subject: "Sei stato invitato su MeepleAI!"
  - Body includes: "Benvenuto nel gruppo MeepleAI!" (admin message)
  - Body includes: platform intro text
  - Body includes: CTA link to /setup-account?token=<token>
  - Body includes: "Questo invito scade il 31 marzo 2026"
And: Response is 200 with InvitationDto { emailSent: true, status: "Pending" }
```

### Example 2: User Activates Account

```
Given: Pending user "luca@example.com" exists with InvitationToken(token-hash=xxx)
And: Token is not expired (expiresAt > now)
And: Token has GameSuggestions: [Catan=PreAdded, Azul=Suggested]

When: User submits POST /api/v1/auth/activate-account with:
  { "token": "<valid-token>", "password": "SecureP@ss123" }

Then: User status transitions Pending → Active
And: User password is set (hashed)
And: User emailVerified = true
And: InvitationToken marked as accepted
And: Session created (auto-login)
And: Response is 200 with { sessionToken: "xxx", requiresOnboarding: true }
And: (async) Catan is added to user's collection (PreAdded)
And: (async) Azul appears as "Suggested for you by Marco" in dashboard
```

### Example 3: Batch with Partial Failure

```
Given: User "anna@example.com" already exists in the system

When: Admin submits POST /api/v1/admin/invitations/batch with:
  {
    "invitations": [
      { "email": "paolo@example.com", "displayName": "Paolo", "role": "User", "tier": "Free", "expiresInDays": 7 },
      { "email": "anna@example.com", "displayName": "Anna", "role": "User", "tier": "Free", "expiresInDays": 7 },
      { "email": "sara@example.com", "displayName": "Sara", "role": "Editor", "tier": "Premium", "expiresInDays": 14 }
    ]
  }

Then: Response is 200 with:
  {
    "succeeded": [
      { "email": "paolo@example.com", "emailSent": true, "status": "Pending" },
      { "email": "sara@example.com", "emailSent": true, "status": "Pending" }
    ],
    "failed": [
      { "email": "anna@example.com", "error": "A user with this email already exists" }
    ]
  }
And: paolo@example.com and sara@example.com have pending User records
And: anna@example.com is unchanged
```

### Example 4: Token Validation States

```
Given: Token "tok-expired" is associated with an expired invitation
And: Token "tok-revoked" is associated with a revoked invitation
And: Token "tok-used" is associated with an already-activated invitation
And: Token "tok-invalid" does not exist

When: GET /api/v1/auth/validate-invitation?token=tok-expired
Then: { "isValid": false, "email": null, "displayName": null, "errorReason": "invalid" }

When: GET /api/v1/auth/validate-invitation?token=tok-revoked
Then: { "isValid": false, "email": null, "displayName": null, "errorReason": "invalid" }

When: GET /api/v1/auth/validate-invitation?token=tok-used
Then: { "isValid": false, "email": null, "displayName": null, "errorReason": "already_used" }

When: GET /api/v1/auth/validate-invitation?token=tok-invalid
Then: { "isValid": false, "email": null, "displayName": null, "errorReason": "invalid" }
```

### Example 5: Race Conditions

```
Scenario: Two admins invite same email simultaneously
Given: No user exists with email "same@example.com"
When: Admin A and Admin B both POST /api/v1/admin/invitations for "same@example.com" within 1 second
Then: Exactly one succeeds (first-writer-wins via unique constraint on email)
And: The other receives 409 ConflictException("A pending invitation for this email already exists")

Scenario: User activates while admin revokes
Given: Pending user with valid token
When: User POSTs /activate-account AND admin DELETEs /invitations/{id} concurrently
Then: First-writer-wins at database level (row lock on User + InvitationToken)
  - If activation commits first: user is Active, revoke returns 400 "Invitation already accepted"
  - If revoke commits first: activation returns 400 with errorReason "invalid"

Scenario: Double activation with same token
Given: Valid token "tok-abc" for pending user
When: User submits /activate-account with "tok-abc" twice (e.g., double-click)
Then: First request succeeds, second returns 400 with errorReason "already_used"
```

### Example 6: Email Delivery Failure — Admin Workflow

```
Given: SMTP server is down

When: Admin invites "user@example.com"
Then: User and InvitationToken are created (committed to DB)
And: Email delivery fails (logged as warning)
And: Response: InvitationDto { emailSent: false, status: "Pending" }
And: Admin panel shows warning icon next to this invitation

When: Admin clicks "Resend" on the invitation
And: SMTP server is now available
Then: New token generated, email sent successfully
And: InvitationDto updated: { emailSent: true }

When: Admin realizes email address was wrong
Then: Admin clicks "Revoke" → pending user hard-deleted
Then: Admin creates new invitation with correct email
```

### Example 7: Stale Game Reference

```
Given: Admin created invitation with GameSuggestion for "Catan" (id=aaa-111)
And: "Catan" was deleted from SharedGameCatalog after invitation was created

When: User activates their account

Then: Phase 1 (activation) succeeds — user is Active, session created
And: Phase 2 (async game suggestions): handler for Catan logs warning "Game aaa-111 not found, skipping"
And: Other game suggestions (if any) are processed normally
And: User activation is NOT affected by stale game reference
```

## Domain Model Changes

### User Entity (Authentication BC) — Extended

New properties:
- `InvitedByUserId` (Guid?) — admin who sent the invitation
- `InvitationExpiresAt` (DateTime?) — when the pending state expires

New status value:
- `UserAccountStatus.Pending` — created by admin, awaiting password setup

**BC boundary resolution**: `UserAccountStatus` lives in Administration BC (`EntityStates.cs`). To manage this cross-BC coupling:
- Move `UserAccountStatus` to a **SharedKernel** project referenced by both BCs
- This makes the coupling explicit and avoids hidden cross-BC surgery for future status values
- Update all consumers in both BCs: `CheckPermissionHandler` (deny all for Pending), `GetUserPermissionsHandler` (empty permissions for Pending), and any `switch`/`if` chain discovered during implementation

New factory method:
```csharp
User.CreatePending(Email email, string displayName, UserRole role,
    UserTier tier, Guid invitedByUserId, DateTime expiresAt, TimeProvider timeProvider)
```
- Sets `Status = Pending`, no password hash
- `EmailVerified = false`
- Uses `TimeProvider` for `CreatedAt` timestamp
- Emits `UserProvisionedEvent`

New method:
```csharp
User.ActivateFromInvitation(PasswordHash passwordHash)
```
- Transitions `Pending → Active`
- Sets password hash
- Sets `EmailVerified = true` (admin-verified email)
- Emits `UserActivatedFromInvitationEvent`

Rules:
- `CanAuthenticate()` returns `false` when `Pending`
- Pending users cannot login, reset password, or use OAuth
- Hard-deleted (not soft-deleted) on expiry — audit records are denormalized (see Audit Strategy)

### InvitationToken Entity — Extended

New properties:
- `CustomMessage` (string?, max 500 chars) — admin's personal message
- `ExpiresAt` (DateTime) — configurable, default 7 days, range 1-30 days
- `PendingUserId` (Guid) — link to the pending user created alongside
- `GameSuggestions` (List\<InvitationGameSuggestion\>) — games to pre-add or suggest

Factory method must accept `TimeProvider`:
```csharp
InvitationToken.Create(Email email, UserRole role, Guid pendingUserId,
    string? customMessage, int expiresInDays, TimeProvider timeProvider)
```
This fixes the existing `DateTime.UtcNow` usage and enables deterministic testing.

### InvitationGameSuggestion — New Entity

```csharp
public class InvitationGameSuggestion
{
    public Guid Id { get; private set; }
    public Guid InvitationTokenId { get; private set; }
    public Guid GameId { get; private set; }          // SharedGameCatalog reference
    public GameSuggestionType Type { get; private set; } // PreAdded | Suggested
}

public enum GameSuggestionType
{
    PreAdded,   // Auto-added to user's collection on activation
    Suggested   // Shown as "suggested for you" in dashboard
}
```

Validation: `GameId` must exist in SharedGameCatalog.

**Cascade delete**: FK `InvitationTokenId` must be configured with `ON DELETE CASCADE` so that when an `InvitationToken` is deleted (expiry/revoke), associated `InvitationGameSuggestions` are cleaned up automatically.

## Commands & Handlers

### Admin Commands

#### ProvisionAndInviteUserCommand

```csharp
public record ProvisionAndInviteUserCommand(
    string Email,
    string DisplayName,
    string Role,           // "User", "Editor", "Admin"
    string Tier,           // "Free", "Premium", "Pro"
    string? CustomMessage,
    int ExpiresInDays,     // default 7, range 1-30
    List<GameSuggestionDto> GameSuggestions
) : IRequest<InvitationDto>;

public record GameSuggestionDto(Guid GameId, string Type); // "PreAdded" | "Suggested"
```

Response includes email delivery status:
```csharp
public record InvitationDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Status,        // "Pending", "Accepted", "Expired", "Revoked"
    DateTime ExpiresAt,
    bool EmailSent,       // false if email delivery failed
    List<GameSuggestionDto> GameSuggestions
);
```

Handler flow:
1. Validate email uniqueness (no existing user or pending invitation)
2. Validate all GameIds exist in SharedGameCatalog
3. `User.CreatePending(...)` with `TimeProvider` — persist
4. `InvitationToken.Create(...)` with `PendingUserId` and `TimeProvider` — persist
5. Add `GameSuggestions` to token
6. `SaveChangesAsync()` — commit user + token + suggestions atomically
7. Send invitation email via `IEmailService.SendInvitationEmailAsync`
   - On success: `EmailSent = true`
   - On failure: log error, `EmailSent = false` (user+token still created)
8. Return `InvitationDto` with `EmailSent` status

Validator (FluentValidation):
- Email: valid format, required
- DisplayName: required, 2-100 chars
- Role: valid enum value
- Tier: valid enum value
- CustomMessage: max 500 chars
- ExpiresInDays: 1-30
- GameSuggestions: max 20 items, valid GameIds

#### BatchProvisionCommand

```csharp
public record BatchProvisionCommand(
    List<BatchInvitationItemDto> Invitations
) : IRequest<BatchInvitationResultDto>;

// Plain DTO, NOT a command — handler constructs ProvisionAndInviteUserCommand per item
public record BatchInvitationItemDto(
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    string? CustomMessage,
    int ExpiresInDays,
    List<GameSuggestionDto> GameSuggestions
);

public record BatchInvitationResultDto(
    List<InvitationDto> Succeeded,       // EmailSent field indicates delivery status
    List<BatchInvitationError> Failed
);

public record BatchInvitationError(string Email, string Error);
```

Handler: constructs a `ProvisionAndInviteUserCommand` per `BatchInvitationItemDto` and dispatches each via `IMediator.Send()`. This ensures FluentValidation fires per-item. Collects successes and failures. Does not abort on individual failures.

Supports two input modes:
- **JSON body** (form multi-riga): `POST /api/v1/admin/invitations/batch`
- **CSV upload**: `POST /api/v1/admin/invitations/batch/csv`

CSV format:
```csv
Email,DisplayName,Role,Tier,ExpiresInDays,CustomMessage,GameIds,GameTypes
mario@example.com,Mario Rossi,User,Free,7,"Benvenuto!",aaa-111;bbb-222,PreAdded;Suggested
```

CSV limits: max 100 rows, max 5MB file size.

#### ResendInvitationCommand (existing, extended)

Regenerates token, updates `ExpiresAt`, re-sends email. Keeps same pending user. No cooldown — admin can resend immediately. Each resend invalidates the previous token.

#### RevokeInvitationCommand (existing, extended)

Revokes token. Cascade delete handles `InvitationGameSuggestions`. Hard-deletes associated pending user (no `GameSuggestions` in UserLibrary exist yet since user was never activated). Returns `{ "userDeleted": true }` in response body to make cascade visible to API consumers.

### User Commands

#### ActivateInvitedAccountCommand

```csharp
public record ActivateInvitedAccountCommand(
    string Token,
    string Password
) : IRequest<ActivationResultDto>;

public record ActivationResultDto(
    string SessionToken,
    bool RequiresOnboarding
);
```

Validator (FluentValidation):
- Token: required, non-empty
- Password: **same rules as `RegisterCommand` validator** — reuse existing `PasswordValidator` class (min 8 chars, 1 uppercase, 1 lowercase, 1 number, 1 special character)

Handler flow (two-phase commit):

**Phase 1 — Activation (single transaction, must succeed atomically):**
1. Validate token (not expired, not revoked, not already used)
2. Find pending user via `PendingUserId`
3. `User.ActivateFromInvitation(passwordHash)`
4. Mark `InvitationToken` as accepted
5. Create session (auto-login)
6. `SaveChangesAsync()` — commits user activation + token + session atomically
7. Return `{SessionToken, RequiresOnboarding: true}`

**Phase 2 — Game suggestions (async via Channel, idempotent):**
8. After successful Phase 1, enqueue game suggestion events to `Channel<GameSuggestionEvent>`
9. `GameSuggestionProcessorService` (BackgroundService) consumes from channel
10. For each event, dispatches to UserLibrary BC handler
11. Retry: exponential backoff (1s, 2s, 4s), max 3 attempts per event
12. If all retries fail: log error, emit `GameSuggestionFailedEvent` for monitoring — user activation is NOT rolled back
13. Frontend polls/refreshes collection after onboarding to pick up pre-added games
14. Events lost on process restart are acceptable (not mission-critical)

**Rationale**: User activation is the critical path. Game suggestions are nice-to-have side effects that must not block or roll back activation.

#### ValidateInvitationTokenQuery

```csharp
public record ValidateInvitationTokenQuery(string Token)
    : IRequest<InvitationValidationDto>;

public record InvitationValidationDto(
    bool IsValid,
    string? Email,           // shown pre-filled in setup form (only when IsValid=true)
    string? DisplayName,     // only when IsValid=true
    string? ErrorReason      // "invalid" (uniform) or "already_used" (redirect to login)
);
```

**Security**: Uses only two error reasons to prevent invitation state enumeration:
- `"invalid"` — covers expired, revoked, and not-found (no distinction exposed)
- `"already_used"` — allows redirect to login page (acceptable disclosure since user exists)

When `IsValid = false` and `ErrorReason != "already_used"`, `Email` and `DisplayName` are null.

## Domain Events

| Event | Emitted By | Listeners |
|-------|-----------|-----------|
| `UserProvisionedEvent` | `User.CreatePending()` | AuditLog (denormalized), Analytics |
| `UserActivatedFromInvitationEvent` | `User.ActivateFromInvitation()` | AuditLog, Analytics |
| `GamePreAddedToCollectionEvent` | `GameSuggestionProcessorService` (async Phase 2) | UserLibrary BC → add to collection |
| `GameSuggestedForUserEvent` | `GameSuggestionProcessorService` (async Phase 2) | UserLibrary BC → create suggestion |
| `GameSuggestionFailedEvent` | `GameSuggestionProcessorService` (retry exhausted) | Monitoring/Alerting |
| `InvitationExpiredEvent` | Cleanup service | AuditLog (denormalized) |

### Cross-BC Event Dispatch Strategy

Game suggestion events are dispatched via a **`Channel<GameSuggestionEvent>`** consumed by a dedicated `GameSuggestionProcessorService : BackgroundService`:

```csharp
// In ActivateInvitedAccountHandler, after Phase 1 SaveChangesAsync:
await _gameSuggestionChannel.Writer.WriteAsync(new GameSuggestionEvent(
    UserId, token.GameSuggestions));

// GameSuggestionProcessorService consumes:
await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
{
    foreach (var suggestion in evt.Suggestions)
    {
        await ProcessWithRetryAsync(suggestion, maxRetries: 3);
    }
}
```

- Handlers in UserLibrary BC are **idempotent** (no error if game already in collection or suggestion already exists)
- Failures are logged and emit `GameSuggestionFailedEvent` for monitoring
- Events lost on process restart are acceptable — admin can manually add games if needed

### UserLibrary BC Listeners

`GamePreAddedToCollectionEvent` handler:
- Adds game to user's collection (same logic as manual "add to collection")
- Idempotent (no error if already exists)
- If GameId no longer exists in SharedGameCatalog: log warning, skip (do not fail)

`GameSuggestedForUserEvent` handler:
- Creates a `GameSuggestion` record (new entity in UserLibrary)
- Displayed in dashboard as "Suggested for you by [admin name]"
- User can accept (→ add to collection) or dismiss
- If GameId no longer exists: log warning, skip

## API Endpoints

### Admin Endpoints (require Admin role)

```
POST   /api/v1/admin/invitations              → ProvisionAndInviteUserCommand
POST   /api/v1/admin/invitations/batch         → BatchProvisionCommand (JSON)
POST   /api/v1/admin/invitations/batch/csv     → BatchProvisionCommand (CSV)
POST   /api/v1/admin/invitations/{id}/resend   → ResendInvitationCommand
DELETE /api/v1/admin/invitations/{id}           → RevokeInvitationCommand
GET    /api/v1/admin/invitations                → GetPendingInvitationsQuery
GET    /api/v1/admin/invitations/{id}           → GetInvitationByIdQuery
```

### Public Endpoints (no auth required)

```
POST   /api/v1/auth/activate-account            → ActivateInvitedAccountCommand
GET    /api/v1/auth/validate-invitation?token=x  → ValidateInvitationTokenQuery
```

## Email Template

**Subject**: "Sei stato invitato su MeepleAI!"

**Structure**:
1. **Header**: MeepleAI logo
2. **Admin message** (if CustomMessage present): styled quote block with admin name
3. **Platform intro**: "MeepleAI e il tuo assistente AI per giochi da tavolo. Gestisci la tua collezione, ottieni risposte dalle regole dei tuoi giochi, e scopri nuovi titoli."
4. **CTA button**: "Configura il tuo account" → `{base_url}/setup-account?token={token}`
5. **Expiry notice**: "Questo invito scade il {ExpiresAt:dd MMMM yyyy}"
6. **Footer**: link supporto, unsubscribe info

Plain-text fallback version included.

## Frontend Pages

### /setup-account?token=xxx (public)

1. On load: `GET /validate-invitation?token=xxx`
2. If valid:
   - Show pre-filled email (readonly) and display name
   - Password field + confirm password (same rules as registration)
   - Submit → `POST /activate-account`
   - On success: set session cookie, redirect to onboarding
3. If invalid:
   - `"invalid"` → "L'invito non e valido o e scaduto. Contatta l'amministratore."
   - `"already_used"` → "Questo invito e gia stato utilizzato." + link login

### /admin/invitations (admin panel)

- **Tabs**: Pending | Accepted | Expired | Revoked
- **Single invite form**: email, display name, role, tier, custom message, expiry days slider (1-30), game picker
- **Email status indicator**: green checkmark if `EmailSent=true`, warning icon if `false` with "Resend" action
- **Batch form**: multi-row table with same fields + "Add row" button
- **CSV import**: file upload with template download link
- **Game picker**: search games from SharedGameCatalog, toggle PreAdded/Suggested per game
- **Actions per invitation**: Resend, Revoke, View details

### Onboarding (reduced for invited users)

Concrete step list for invited users (`User.InvitedByUserId != null`):

| Step ID | Step Name | Shown for invited? | Shown for self-registered? |
|---------|-----------|--------------------|-----------------------------|
| `profile-setup` | Avatar, display name | Yes (name pre-filled) | Yes |
| `role-selection` | Choose role | **No** (admin set it) | Yes |
| `tier-selection` | Choose tier/plan | **No** (admin set it) | Yes |
| `game-preferences` | Categories, complexity, player count | Yes | Yes |
| `notification-settings` | Email/push preferences | Yes | Yes |
| `first-game` | Add your first game | **No** (admin may have pre-added) | Yes |

Detection: `User.InvitedByUserId != null` → filter step list. This is a server-side decision returned in the onboarding configuration endpoint, not a frontend-only check.

## Background Services

### InvitationCleanupService

```csharp
public class InvitationCleanupService : BackgroundService
{
    // Runs every hour
    // Uses TimeProvider for testability (project convention)
    //
    // 1. Acquire Redis distributed lock ("invitation-cleanup-lock", TTL 5 minutes)
    //    - If lock held by another instance, skip this cycle (log info)
    // 2. Query: Users WHERE Status=Pending AND InvitationExpiresAt < timeProvider.GetUtcNow()
    // 3. For each expired pending user:
    //    a. Mark InvitationToken as expired
    //    b. Cascade delete handles InvitationGameSuggestions
    //    c. Hard delete pending user
    //    d. Emit InvitationExpiredEvent (with denormalized email + displayName for audit)
    // 4. Log: cleanup count for monitoring
    // 5. Release lock
}
```

**Distributed lock**: Required for multi-instance deployments (load-balanced API). Uses Redis (already in stack) to prevent duplicate processing. TTL of 5 minutes ensures lock is released even if process crashes.

### GameSuggestionProcessorService

```csharp
public class GameSuggestionProcessorService : BackgroundService
{
    // Consumes from Channel<GameSuggestionEvent>
    // For each event: process suggestions with retry (1s, 2s, 4s backoff, max 3)
    // On retry exhaustion: emit GameSuggestionFailedEvent, log error
    // Idempotent: safe to replay events
}
```

## GameSuggestion Entity (UserLibrary BC)

New entity to support the "suggested games" feature:

```csharp
public class GameSuggestion
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid SuggestedByUserId { get; private set; }  // admin
    public string? Source { get; private set; }           // "invitation"
    public DateTime CreatedAt { get; private set; }
    public bool IsDismissed { get; private set; }
    public bool IsAccepted { get; private set; }

    // Domain state changes only — no cross-aggregate logic
    public void Accept()
    {
        IsAccepted = true;
        // Emits GameSuggestionAcceptedEvent
        // Application handler dispatches AddGameToLibraryCommand
    }

    public void Dismiss() { IsDismissed = true; }
}
```

`GameSuggestion.Accept()` only updates its own state and emits `GameSuggestionAcceptedEvent`. The application-layer handler for that event dispatches `AddGameToLibraryCommand` to add the game to the user's collection. This respects DDD aggregate boundaries.

## Non-Functional Requirements

| Requirement | Target | Measurement |
|-------------|--------|-------------|
| Single invitation creation | < 2s response time | API latency p99 |
| Batch of 100 invitations | < 60s total processing | API latency |
| Email delivery | < 30s after provisioning | Email service logs |
| Setup-account page load | < 2s | Frontend metrics |
| Cleanup service SLA | Expired users removed within 2h of expiry | Cleanup metric timestamp |
| Game suggestion processing | < 10s after activation | Channel consumer latency |
| Token validation | < 200ms | API latency p99 |

## Testing Strategy

### Unit Tests
- `User.CreatePending()` — correct status, no password, properties set, uses TimeProvider
- `User.ActivateFromInvitation()` — Pending→Active transition, password set, email verified
- `CanAuthenticate()` returns false for Pending
- `InvitationToken.Create()` with TimeProvider — correct expiry calculation
- `InvitationToken` expiry and revocation logic
- `BatchProvisionCommand` — partial failure handling
- `InvitationGameSuggestion` validation
- `GameSuggestion.Accept()` — sets IsAccepted, emits event (no collection logic)
- `GameSuggestion.Dismiss()` — sets IsDismissed
- `ValidateInvitationTokenQuery` — uniform error for expired/revoked/not-found
- `ActivateInvitedAccountCommand` password validation — reuses existing PasswordValidator

### Integration Tests
- Full flow: provision → activate → session created → games processed (async)
- Cleanup service: pending user deleted after expiry, cascade deletes suggestions
- Cleanup service: Redis distributed lock prevents duplicate processing
- Batch: CSV + JSON, mixed successes and failures, EmailSent status tracking
- Game suggestions: PreAdded in collection, Suggested visible (after async processing)
- Token expired/revoked → uniform "invalid" error response
- Duplicate email → ConflictException (409)
- Resend → new token, same pending user
- `UserAccountStatus.Pending` handled correctly by CheckPermissionHandler (deny all)
- Stale GameId in suggestion → logged warning, skipped, activation not affected
- Email delivery: mock `IEmailService` captures sent emails in-memory
  - Verify: correct recipient, token URL present, CustomMessage in body, expiry date formatted
  - Email template snapshot tests for layout/content verification

### Edge Case Tests
- **Double activation**: second attempt with same token returns `"already_used"`
- **Concurrent same-email invite**: exactly one succeeds, other gets 409
- **Activation vs revoke race**: first-writer-wins, loser gets appropriate error
- **Stale GameId**: handler logs warning, skips game, does not fail activation
- **Cleanup vs activation race**: activation transaction holds row lock, cleanup skips that user
- **Unicode email/displayName**: test with accented characters (e.g., "Francois@example.com", "Rene")
- **Max batch size**: 100 rows CSV processed within NFR target
- **Expired token re-use**: returns "invalid", not "already_used"

### Security Tests
- **Rate limiting**: 100 rapid requests to `/validate-invitation` from same IP → 429 after threshold
- **CSV injection**: upload CSV with `=cmd|'/C calc'!A0` in DisplayName → verify sanitized/escaped
- **XSS in CustomMessage**: `<script>alert(1)</script>` → verify HTML-escaped in email output
- **Authorization**: User role calling `POST /admin/invitations` → 403 Forbidden
- **Token brute-force**: random tokens always return same-timing "invalid" (EF Core parameterized query)
- **Password policy**: activation with weak password ("123") → validation error matching RegisterCommand rules

### E2E Tests
- Admin creates invitation → user receives email → setup password → reduced onboarding → dashboard with games
- Admin batch invite via CSV → all users created, email status visible in admin panel
- Invalid invitation → generic error page ("L'invito non e valido o e scaduto")
- Already-used invitation → redirect to login

## Audit Strategy

Pending users are hard-deleted on expiry, but audit records must survive. All audit log entries for invitation-related events are **denormalized** — they store the email and display name directly, not just a UserId FK:

```csharp
// AuditLog record for UserProvisionedEvent:
{
    Action: "UserProvisioned",
    TargetEntityType: "User",
    TargetEntityId: "<user-id>",      // Will be deleted on expiry
    Metadata: {
        "email": "luca@example.com",  // Denormalized — survives user deletion
        "displayName": "Luca Bianchi",
        "invitedBy": "<admin-id>",
        "role": "User",
        "tier": "Premium"
    }
}

// AuditLog record for InvitationExpiredEvent:
{
    Action: "InvitationExpired",
    TargetEntityType: "InvitationToken",
    TargetEntityId: "<token-id>",
    Metadata: {
        "email": "luca@example.com",  // Denormalized
        "displayName": "Luca Bianchi",
        "pendingUserId": "<deleted-user-id>",
        "expiredAt": "2026-03-31T00:00:00Z"
    }
}
```

This ensures audit queries do not produce dangling FK references and the full invitation lifecycle is traceable even after cleanup.

## Observability

### Metrics

| Metric | Type | Description |
|--------|------|-------------|
| `invitations_provisioned_total` | Counter | Total invitations created (labels: role, tier) |
| `invitations_activated_total` | Counter | Total successful activations |
| `invitations_expired_total` | Counter | Total expired (cleaned up by service) |
| `invitations_revoked_total` | Counter | Total admin-revoked |
| `invitation_email_sent_total` | Counter | Emails sent (labels: success, failure) |
| `invitation_email_failure_rate` | Gauge | Rolling 5min email failure percentage |
| `game_suggestion_processed_total` | Counter | Game suggestions processed (labels: type, success/failure) |
| `game_suggestion_retry_exhausted_total` | Counter | Game suggestions that failed all retries |
| `cleanup_service_last_run_at` | Gauge | Timestamp of last successful cleanup run |
| `cleanup_service_users_deleted` | Counter | Pending users deleted per cleanup cycle |
| `invitation_activation_duration_seconds` | Histogram | Phase 1 activation latency |

### Alerts

| Alert | Condition | Severity |
|-------|-----------|----------|
| Email failure spike | `invitation_email_failure_rate > 10%` for 5 min | Warning |
| Cleanup service stale | `cleanup_service_last_run_at` > 2 hours ago | Critical |
| Game suggestion failures | `game_suggestion_retry_exhausted_total` increase > 5 in 1h | Warning |
| Activation latency | `invitation_activation_duration_seconds` p99 > 5s | Warning |

### Health Check

Expose via existing `/health` endpoint:
- `InvitationCleanupService`: last run timestamp, pending user count
- `GameSuggestionProcessorService`: channel queue depth, processing status

## Migration

New database objects:
- `ALTER TABLE Users ADD InvitedByUserId UUID NULL`
- `ALTER TABLE Users ADD InvitationExpiresAt TIMESTAMPTZ NULL`
- `ALTER TABLE InvitationTokens ADD CustomMessage VARCHAR(500) NULL`
- `ALTER TABLE InvitationTokens ADD ExpiresAt TIMESTAMPTZ NOT NULL DEFAULT (NOW() + INTERVAL '7 days')`
- `ALTER TABLE InvitationTokens ADD PendingUserId UUID NULL`
- `CREATE TABLE InvitationGameSuggestions (Id UUID PK, InvitationTokenId UUID FK ON DELETE CASCADE, GameId UUID NOT NULL, Type INT NOT NULL)`
- `CREATE TABLE GameSuggestions (Id UUID PK, UserId UUID FK, GameId UUID NOT NULL, SuggestedByUserId UUID NOT NULL, Source VARCHAR(50), CreatedAt TIMESTAMPTZ, IsDismissed BOOLEAN DEFAULT FALSE, IsAccepted BOOLEAN DEFAULT FALSE)`
- Move `UserAccountStatus` to SharedKernel, add `Pending = 3`
- Update `CheckPermissionHandler` and `GetUserPermissionsHandler` to handle `Pending` status

Note: PostgreSQL syntax (project uses PostgreSQL, not SQL Server).

## Security Considerations

- Invitation tokens: 32-byte cryptographically random, URL-safe Base64, hashed (SHA-256) in DB
- Token lookup via EF Core parameterized queries (timing-safe at DB level)
- Setup-account page: rate-limited to prevent brute force
- Pending users cannot authenticate — enforced at domain level
- Admin-only endpoints: require `Admin` role via auth middleware
- CSV upload: validate file size (max 5MB), row count limits (max 100), sanitize inputs (CSV injection protection)
- Custom message: HTML-escaped in email template to prevent XSS
- ValidateInvitationTokenQuery: uniform "invalid" error to prevent state enumeration
- Email/DisplayName only returned when token is valid (not on error responses)
- Password validation: reuses `RegisterCommand` validator — consistent policy across all entry points

## Review Resolution Log

### Round 1 — Code Reviewer (8 issues)

| # | Severity | Issue | Resolution |
|---|----------|-------|------------|
| 1 | Critical | `UserAccountStatus.Pending` modifies Administration BC enum | Move to SharedKernel, update all consumers explicitly |
| 2 | Critical | Cross-BC events have no atomicity strategy | Two-phase: sync activation + async Channel-based game suggestions |
| 3 | Critical | Batch email failures indistinguishable from successes | Added `EmailSent: bool` to `InvitationDto`, admin UI shows status |
| 4 | High | `InvitationToken.Create()` bypasses TimeProvider | Added `TimeProvider` to factory method signature |
| 5 | High | `GameSuggestion.Accept()` embeds application logic | Emits `GameSuggestionAcceptedEvent`, handler dispatches command |
| 6 | High | ValidateInvitationTokenQuery discloses state | Uniform "invalid" + "already_used" only |
| 7 | High | No cascade-delete for InvitationGameSuggestions | `ON DELETE CASCADE` on `InvitationTokenId` FK |
| 8 | High | BatchProvisionCommand uses command-of-commands | Changed to `List<BatchInvitationItemDto>` (DTO) |

### Round 2 — Spec Panel (5 experts, 13 issues)

| # | Severity | Expert | Issue | Resolution |
|---|----------|--------|-------|------------|
| W1 | High | Wiegers | Missing password acceptance criteria | Added validator referencing existing `PasswordValidator` |
| W2 | Medium | Wiegers | No NFR section | Added NFR table with measurable targets |
| W3 | Medium | Wiegers | Ambiguous reduced onboarding | Added concrete step ID table with show/skip per user type |
| A1 | Critical | Adzic | Zero concrete examples | Added 7 Given/When/Then examples covering all key flows |
| A2 | High | Adzic | Email failure scenario underspecified | Added Example 6 with admin workflow for email failures |
| A3 | Medium | Adzic | Race condition examples missing | Added Example 5 with 3 race condition scenarios |
| F1 | High | Fowler | Cross-BC enum is shared kernel anti-pattern | Resolved: move to SharedKernel project |
| F2 | Medium | Fowler | `Task.Run()` vs Channel ambiguity | Resolved: specified `Channel<T>` + BackgroundService explicitly |
| F3 | Low | Fowler | DELETE cascades silently to User | Added `{ "userDeleted": true }` in revoke response |
| N1 | Critical | Nygard | No monitoring or alerting | Added Observability section with metrics, alerts, health checks |
| N2 | High | Nygard | Hard delete creates audit gaps | Added Audit Strategy with denormalized records |
| N3 | Medium | Nygard | Cleanup service no distributed lock | Added Redis distributed lock |
| C1 | High | Crispin | Missing edge case coverage | Added Edge Case Tests subsection |
| C2 | High | Crispin | No security testing | Added Security Tests subsection |
| C3 | Medium | Crispin | Email not testable | Added email mock strategy with snapshot tests |
