# Wave A.5a — GameNight Public Token-based RSVP (Backend Extension)

**Issue**: #607 (sub-issue di umbrella #579)
**Branch**: `feature/issue-607-game-night-public-rsvp-be`
**Parent**: `main-dev`
**Date**: 2026-04-28
**Status**: Spec → TDD red phase

---

## 1. Context

### 1.1 Driver

Mockup `admin-mockups/design_files/sp3-accept-invite.jsx` (committed da PR #568 SP3 batch) modella un **invito a serata di gioco (game night RSVP)** con 7 stati pubblici (default/logged-in/accepted-success/declined/token-expired/token-invalid/already-accepted). Il flusso è **token-based**, **public** (no login required), con confetti animation su accept.

### 1.2 Domain mismatch identificato

Discovery pre-spec ha rivelato che il design panel iniziale aveva confuso due flussi distinti:

| Aspetto | Mockup `sp3-accept-invite.jsx` | Route `/accept-invite?token=X` esistente |
|---|---|---|
| BC | GameManagement (game night RSVP) | Authentication (account creation) |
| Trigger | Host invita guest a serata futura | Admin invita user a creare account |
| Form | Accept/Decline boolean | Password + 4 strength rules |
| Stati | 7 pubblici | 4 (loading/valid/submitting/success) |
| Endpoint backend | **NON ESISTE** | `/api/v1/auth/{validate,accept}-invitation` |
| Token shape | Path param `/invites/[token]` (umbrella #579) | Query string `?token=X` |

L'umbrella #579 elenca esplicitamente la route target Wave A.5 come `/invites/[token]` (greenfield), confermando che si tratta di una nuova route, NON una migrazione brownfield di `/accept-invite`.

### 1.3 Backend GameManagement BC — stato attuale

**Già presente**:
- `IGameNightEmailService.SendGameNightInvitationEmailAsync(toEmail, organizerName, title, scheduledAt, location, gameNames, rsvpAcceptUrl, rsvpDeclineUrl, unsubscribeUrl, ct)` (Issue #44/#47) — email pronta con parametri RSVP URL nella signature
- `RespondToGameNightCommand(GameNightId, UserId, Response)` — auth-required, by GameNightId
- `InviteToGameNightCommand` — invita per UserIds list, NO email NO token
- `GameNightEvent` aggregate root con `Invitees` collection, lifecycle Published/Cancelled/Completed
- `IGameNightEventRepository` con CRUD + queries

**Mancante**:
- Aggregate `GameNightInvitation` con token persistente (separata da Invitees by-UserId)
- `IGameNightInvitationRepository`
- Token generation + email orchestration nel flusso invite-by-email
- Endpoint pubblici (no auth) per RSVP via token
- HybridCache + invalidation per public lookup hot path

### 1.4 Decisioni risolte

- **D1 (a)** — Email integration reale: A.5a chiama `SendGameNightInvitationEmailAsync` con URL costruite dinamicamente. Richiede `IUrlBuilder` (riuso esistente o nuovo helper).
- **D2 (b)** — Idempotency same-response: 200 OK quando response richiesta == stato corrente (refresh-safe), 409 Conflict solo quando l'utente cerca di cambiare risposta.

---

## 2. Architecture

### 2.1 Layer breakdown

```
GameManagement BC
├── Domain
│   ├── Entities/GameNightEvent/
│   │   ├── GameNightInvitation.cs         (NEW — aggregate root)
│   │   └── GameNightInvitationStatus.cs   (NEW — enum)
│   ├── ValueObjects/
│   │   └── InvitationToken.cs             (NEW — VO immutable record)
│   └── Events/
│       ├── GameNightInvitationCreated.cs  (NEW)
│       └── GameNightInvitationResponded.cs (NEW)
├── Application
│   ├── Commands/GameNights/
│   │   ├── CreateGameNightInvitationByEmailCommand.cs (NEW)
│   │   ├── CreateGameNightInvitationByEmailCommandHandler.cs
│   │   ├── CreateGameNightInvitationByEmailValidator.cs
│   │   ├── RespondToGameNightInvitationByTokenCommand.cs (NEW)
│   │   ├── RespondToGameNightInvitationByTokenCommandHandler.cs
│   │   └── RespondToGameNightInvitationByTokenValidator.cs
│   ├── Queries/GameNights/
│   │   ├── GetGameNightInvitationByTokenQuery.cs (NEW)
│   │   └── GetGameNightInvitationByTokenQueryHandler.cs
│   ├── DTOs/GameNights/
│   │   ├── GameNightInvitationDto.cs (NEW — admin/organizer view)
│   │   └── PublicGameNightInvitationDto.cs (NEW — public token view)
│   └── EventHandlers/
│       └── GameNightInvitationRespondedHandler.cs (NEW — cache invalidation)
├── Infrastructure
│   ├── Persistence/Configurations/
│   │   └── GameNightInvitationConfiguration.cs (NEW — EF Core)
│   ├── Persistence/Repositories/
│   │   └── GameNightInvitationRepository.cs (NEW)
│   └── Migrations/
│       └── 2026XXXX_AddGameNightInvitations.cs (NEW)
└── Domain/Repositories/
    └── IGameNightInvitationRepository.cs (NEW)

Routing/
└── GameNightEndpoints.cs (EXTEND — 3 new endpoints)

SharedKernel/
└── Application/Services/
    └── IUrlBuilder.cs (EXISTING or NEW — verify in commit 1)
```

### 2.2 Aggregate design — `GameNightInvitation`

**Properties**:
```csharp
public Guid Id { get; private set; }
public string Token { get; private set; }              // 22-char base62
public Guid GameNightId { get; private set; }          // FK → GameNightEvent
public string Email { get; private set; }              // lowercase, validated
public GameNightInvitationStatus Status { get; private set; }
public DateTimeOffset ExpiresAt { get; private set; }
public DateTimeOffset? RespondedAt { get; private set; }
public Guid? RespondedByUserId { get; private set; }   // optional auth association
public DateTimeOffset CreatedAt { get; private set; }
public Guid CreatedBy { get; private set; }            // organizer userId
```

**Status enum**:
```csharp
public enum GameNightInvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Expired = 3,
    Cancelled = 4,  // organizer cancels invite OR game night cancelled
}
```

**Factory**:
```csharp
public static GameNightInvitation Create(
    Guid gameNightId,
    string email,
    DateTimeOffset expiresAt,
    Guid createdBy,
    DateTimeOffset utcNow)
{
    if (string.IsNullOrWhiteSpace(email))
        throw new ArgumentException("Email is required", nameof(email));
    if (expiresAt <= utcNow)
        throw new ArgumentException("ExpiresAt must be in the future", nameof(expiresAt));

    return new GameNightInvitation
    {
        Id = Guid.NewGuid(),
        Token = InvitationToken.Generate().Value,
        GameNightId = gameNightId,
        Email = email.Trim().ToLowerInvariant(),
        Status = GameNightInvitationStatus.Pending,
        ExpiresAt = expiresAt,
        RespondedAt = null,
        RespondedByUserId = null,
        CreatedAt = utcNow,
        CreatedBy = createdBy,
    };
}
```

**Domain methods**:
```csharp
// Idempotent: returns true if state changed, false if already in this state
public bool Accept(Guid? userId, DateTimeOffset utcNow);
public bool Decline(Guid? userId, DateTimeOffset utcNow);
public void Cancel(DateTimeOffset utcNow);
public bool IsExpired(DateTimeOffset utcNow) =>
    Status == GameNightInvitationStatus.Pending && utcNow >= ExpiresAt;
```

**Idempotency rule** (D2 b):
- `Accept` su Pending → transition Pending→Accepted, returns `true`
- `Accept` su Accepted → no-op, returns `false` (caller mappa a 200 OK)
- `Accept` su Declined → throws `InvalidOperationException` (caller mappa a 409 Conflict)
- `Accept` su Expired/Cancelled → throws `InvalidOperationException` (caller mappa a 410 Gone)

### 2.3 Value object — `InvitationToken`

```csharp
public sealed record InvitationToken
{
    public string Value { get; }

    private InvitationToken(string value) => Value = value;

    public static InvitationToken Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Token cannot be empty", nameof(value));
        if (value.Length != 22)
            throw new ArgumentException("Token must be 22 characters", nameof(value));
        if (!IsValidBase62(value))
            throw new ArgumentException("Token must be base62", nameof(value));
        return new InvitationToken(value);
    }

    public static InvitationToken Generate()
    {
        // 16 bytes (~131 bits entropy) → base62 → 22 chars
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return new InvitationToken(Base62.Encode(bytes));
    }

    private static bool IsValidBase62(string s) =>
        s.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'));
}
```

### 2.4 Public DTO — `PublicGameNightInvitationDto`

Surface area allineata 1:1 al mockup `InviteBody` component:

```csharp
public sealed record PublicGameNightInvitationDto(
    string Token,
    string Status,                    // "Pending" | "Accepted" | "Declined" | "Expired" | "Cancelled"
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RespondedAt,

    // Host (organizer) info
    Guid HostUserId,
    string HostDisplayName,
    string? HostAvatarUrl,
    string? HostWelcomeMessage,       // optional custom welcome from organizer

    // Game info
    Guid GameNightId,
    string Title,                     // game night title
    DateTimeOffset ScheduledAt,
    string? Location,
    int? DurationMinutes,
    int ExpectedPlayers,
    int AcceptedSoFar,                // count of Accepted invitations + organizer

    // Primary game (first in playlist if multi-game game night)
    Guid? PrimaryGameId,
    string? PrimaryGameName,
    string? PrimaryGameImageUrl,

    // Idempotency hint for frontend
    string? AlreadyRespondedAs        // "Accepted" | "Declined" | null
);
```

---

## 3. Endpoints

All in `Routing/GameNightEndpoints.cs` (file esistente, esteso):

### 3.1 Public — Get invitation by token

```
GET /api/v1/game-nights/invitations/{token}
Auth: NONE (public)

Responses:
  200 OK   → PublicGameNightInvitationDto (anche se Accepted/Declined → AlreadyRespondedAs popolato)
  404      → Token sconosciuto
  410 Gone → Status == Expired || Cancelled
```

**Performance gate**: p95 cold <100ms, warm <30ms (HybridCache TTL 60s, tag `game-night-invitation:{token}`).

### 3.2 Public — Respond to invitation by token

```
POST /api/v1/game-nights/invitations/{token}/respond
Auth: OPTIONAL (cookie auth se presente, popolata in RespondedByUserId)
Body: { "response": "accepted" | "declined" }

Responses:
  200 OK     → PublicGameNightInvitationDto aggiornato (idempotent same-response OK)
  400        → response invalida (non in {accepted, declined})
  404        → Token sconosciuto
  409 Conflict → Tentativo di cambiare risposta (Accepted→Declined o viceversa)
  410 Gone   → Expired || Cancelled
```

Side-effects su Accept (transition Pending→Accepted):
- Fires `GameNightInvitationResponded` domain event
- Event handler invalida cache `game-night-invitation:{token}`
- Calls `IGameNightEmailService.SendGameNightRsvpConfirmationEmailAsync` (existing method)

### 3.3 Auth-required — Create invitation by email (organizer)

```
POST /api/v1/game-nights/{gameNightId}/invitations
Auth: REQUIRED (organizer-only)
Body: { "email": "guest@example.com" }

Responses:
  201 Created → GameNightInvitationDto (admin view, include Token + ExpiresAt)
  400         → Email format invalida
  401         → Non autenticato
  403         → Non organizer del GameNight
  404         → GameNight sconosciuto
  409 Conflict → Pending invitation già esistente per (gameNightId, email)
  422         → GameNight non pubblicato o scaduto
```

Side-effects:
- Persiste `GameNightInvitation`
- Fires `GameNightInvitationCreated` domain event
- Builds `rsvpAcceptUrl = $"{baseUrl}/invites/{token}?action=accept"` e `rsvpDeclineUrl = $"{baseUrl}/invites/{token}?action=decline"`
- Calls `IGameNightEmailService.SendGameNightInvitationEmailAsync(...)` con URLs costruiti

---

## 4. Test plan

### 4.1 Domain unit tests (`tests/Api.Tests/UnitTests/Domain/GameManagement/`)

`GameNightInvitationTests.cs`:
- `Create_WithValidParams_ReturnsAggregate`
- `Create_WithEmptyEmail_Throws`
- `Create_WithPastExpiresAt_Throws`
- `Create_NormalizesEmailToLowercase`
- `Accept_OnPending_TransitionsToAccepted_ReturnsTrue`
- `Accept_OnAccepted_NoOp_ReturnsFalse` (D2 b idempotency)
- `Accept_OnDeclined_Throws` (response change)
- `Accept_OnExpired_Throws`
- `Accept_OnCancelled_Throws`
- `Decline_OnPending_TransitionsToDeclined_ReturnsTrue`
- `Decline_OnDeclined_NoOp_ReturnsFalse`
- `Decline_OnAccepted_Throws`
- `Cancel_FromPending_TransitionsToCancelled`
- `Cancel_FromAccepted_TransitionsToCancelled` (organizer revokes)
- `IsExpired_WhenPastExpiresAtAndPending_ReturnsTrue`
- `IsExpired_WhenAccepted_ReturnsFalse` (terminal state, expiry irrelevant)
- `Accept_RecordsRespondedAt_AndOptionalUserId`

`InvitationTokenTests.cs`:
- `Generate_Returns22CharBase62`
- `Generate_ProducesUniqueTokens` (10000 iterations, no collisions)
- `Create_WithValidToken_Succeeds`
- `Create_WithEmpty_Throws`
- `Create_WithWrongLength_Throws`
- `Create_WithInvalidChars_Throws`

### 4.2 Application unit tests (`tests/Api.Tests/UnitTests/Application/GameManagement/`)

`CreateGameNightInvitationByEmailCommandHandlerTests.cs`:
- `Handle_WhenOrganizer_PersistsAggregate_AndSendsEmail`
- `Handle_WhenNotOrganizer_ThrowsUnauthorized`
- `Handle_WhenGameNightNotFound_ThrowsNotFound`
- `Handle_WhenGameNightNotPublished_ThrowsConflict`
- `Handle_WhenDuplicatePending_ThrowsConflict`
- `Handle_BuildsRsvpUrls_WithCorrectFormat`
- `Handle_FiresInvitationCreatedEvent`

`GetGameNightInvitationByTokenQueryHandlerTests.cs`:
- `Handle_ValidToken_ReturnsPublicDto`
- `Handle_UnknownToken_ThrowsNotFound`
- `Handle_ExpiredToken_ThrowsGoneException` (or maps to 410)
- `Handle_CachesResultByToken_60sTTL`
- `Handle_PopulatesAlreadyRespondedAs_WhenStatusNotPending`
- `Handle_AcceptedSoFar_CountsOrganizer_PlusAcceptedInvitations`

`RespondToGameNightInvitationByTokenCommandHandlerTests.cs`:
- `Handle_AcceptOnPending_Transitions_AndSendsConfirmationEmail`
- `Handle_AcceptOnAccepted_Idempotent_NoEmailResent` (D2 b)
- `Handle_AcceptOnDeclined_ThrowsConflict` (response change)
- `Handle_DeclineOnPending_Transitions_NoEmail`
- `Handle_OnExpired_ThrowsGone`
- `Handle_OnCancelled_ThrowsGone`
- `Handle_WithUserId_RecordsRespondedByUserId`
- `Handle_WithoutUserId_LeavesRespondedByNull` (anonymous guest)
- `Handle_FiresInvitationRespondedEvent`

`GameNightInvitationRespondedHandlerTests.cs`:
- Pattern mirror Wave A.3a `*ForCatalogAggregatesHandlerTests` (real `HybridCache` from DI, NOT mock):
  - Pre-seed cache entry tagged `game-night-invitation:{token}` → fire event → re-call `GetOrCreateAsync` con stessa key/tag → verify factory re-invokes (= eviction worked)
- **CRITICAL**: inject raw `HybridCache` (NOT wrapper `IHybridCacheService`) per consistency con producer query handler — pitfall #2620 lesson da Wave A.3a

### 4.3 Integration tests (Testcontainers Postgres)

`tests/Api.Tests/IntegrationTests/GameManagement/GameNightInvitationEndpointsTests.cs`:

Public GET `/api/v1/game-nights/invitations/{token}`:
- `GetByToken_ValidPending_Returns200_PublicDto`
- `GetByToken_UnknownToken_Returns404`
- `GetByToken_ExpiredInvitation_Returns410`
- `GetByToken_AlreadyAccepted_Returns200_WithAlreadyRespondedAs`
- `GetByToken_NoAuthRequired_Anonymous_Returns200`
- `GetByToken_Performance_Warm_p95Below30ms` (Stopwatch, 100 iterations)
- `GetByToken_Performance_Cold_p95Below100ms`

Public POST `/api/v1/game-nights/invitations/{token}/respond`:
- `Respond_AcceptPending_Returns200_StatusAccepted`
- `Respond_AcceptAccepted_Idempotent_Returns200`
- `Respond_AcceptDeclined_Returns409_ResponseChange`
- `Respond_DeclinePending_Returns200_StatusDeclined`
- `Respond_DeclineDeclined_Idempotent_Returns200`
- `Respond_OnExpired_Returns410`
- `Respond_OnCancelled_Returns410`
- `Respond_UnknownToken_Returns404`
- `Respond_InvalidResponseValue_Returns400` (e.g. "maybe")
- `Respond_WithCookieAuth_PopulatesRespondedByUserId`
- `Respond_AnonymousGuest_LeavesRespondedByUserIdNull`

Auth POST `/api/v1/game-nights/{gameNightId}/invitations`:
- `CreateInvite_AsOrganizer_Returns201_AndEmailSent` (verify via `IEmailService` mock SendCount)
- `CreateInvite_AsNonOrganizer_Returns403`
- `CreateInvite_GameNightNotFound_Returns404`
- `CreateInvite_DuplicatePending_Returns409`
- `CreateInvite_GameNightNotPublished_Returns422`
- `CreateInvite_InvalidEmail_Returns400`
- `CreateInvite_AnonymousUser_Returns401`

### 4.4 Coverage targets

- Domain: 100% line + branch (small surface, all paths testable)
- Application handlers: 100% line + branch
- Repository: 90%+ via integration tests (some EF-internal paths excluded)
- Endpoints: 100% via integration tests
- **PR target**: codecov/patch ≥85%

---

## 5. Commit boundary (TDD red→green)

Single PR, 4 commit boundary mirror Wave A.3a:

### Commit 1 — Domain layer (RED then GREEN)
- `GameNightInvitation` aggregate + `GameNightInvitationStatus` enum + `InvitationToken` VO
- `GameNightInvitationCreated`/`GameNightInvitationResponded` domain events
- `Base62` helper (verify if exists in SharedKernel, else create minimal impl)
- 100% domain unit tests
- **No EF, no DI registration yet** — pure domain commit

### Commit 2 — Application layer (RED then GREEN)
- 2 commands + 1 query + handlers + validators
- 2 DTOs (`GameNightInvitationDto`, `PublicGameNightInvitationDto`)
- `IGameNightInvitationRepository` interface in Domain
- `GameNightInvitationRespondedHandler` event handler (cache invalidation)
- 100% application unit tests con repository mocks + real HybridCache for handler

### Commit 3 — Infrastructure (RED then GREEN)
- EF Core configuration `GameNightInvitationConfiguration` (table + indexes + FK)
- EF migration `AddGameNightInvitations`
- `GameNightInvitationRepository` impl
- DI registration in `GameManagementServiceExtensions`
- `IUrlBuilder` riuso o nuovo helper minimal
- Repository integration tests con Testcontainers

### Commit 4 — Endpoints + email integration (RED then GREEN)
- 3 endpoint in `GameNightEndpoints.cs`
- `appsettings.json` + `appsettings.Development.json` con `GameNight:InvitationExpiryDays = 14`
- Integration tests E2E con Testcontainers (auth + public)
- OpenAPI/Scalar surface verification

---

## 6. Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| Token enumeration attack | High | 131-bit entropy + rate limit per IP su public GET (riusa middleware esistente se presente, altrimenti TODO follow-up issue) |
| Email enumeration via 404 vs 410 | Medium | Response shape uniforme (no leak su esistenza email destinatario) — usiamo solo token come selettore |
| Cache poisoning su public lookup | Low | Token immutable, cache key = token diretto, no user input nel payload |
| Race condition double-RSVP | Low | EF concurrency token su `GameNightInvitation.RowVersion` (`[Timestamp]` byte[]) — catch `DbUpdateConcurrencyException` → retry once |
| Email service downtime blocking organizer flow | Medium | Email send NON in transazione DB. Se email fail → log error, return 201 con DTO (organizer può re-trigger via UI in follow-up). Trade-off: documentato nel handler. |
| HybridCache tag-store mismatch (pitfall #2620) | High | Inject raw `HybridCache` nell'event handler, **NON** wrapper `IHybridCacheService` — mirror lesson Wave A.3a |
| Performance degrade su lookup hot path | Medium | HybridCache TTL 60s + integration test gate p95 <30ms warm/<100ms cold |
| Migration deployment lock contention | Low | Tabella nuova, no ALTER su esistenti — zero contention |

---

## 7. Out of scope

- `.ics` calendar download endpoint (deferred per umbrella #579 out-of-scope)
- Frontend route `/invites/[token]` (Wave A.5b separate child issue, post-merge)
- Email reminder 24h pre-event (esiste già `SendGameNightReminder24hEmailAsync`, non toccato)
- RSVP via authenticated user dashboard (esiste già `RespondToGameNightCommand`, non toccato)
- Email templates redesign per match v2 brand (mockup è frontend route, email template separata)
- Rate limiting su public endpoints (follow-up se security issue emerge in pen test)
- Webhook outbound notifications (organizer wants to be notified when guest accepts) — esiste già `SendGameNightInvitationEmailAsync` per organizer? Verificare in commit 4, NON estendere qui.

---

## 8. Definition of Done

- [ ] 4 commit committed e pushed su `feature/issue-607-game-night-public-rsvp-be`
- [ ] Tutti i test pass localmente (`dotnet test --filter "BoundedContext=GameManagement"`)
- [ ] PR opened con `Closes #607` body keyword, base = `main-dev`
- [ ] CI all green: Backend Unit ✅, Detect Changes ✅, validate-source-branch ✅, GitGuardian ✅, CI Success ✅
- [ ] codecov/patch ≥85%
- [ ] PR squash merged con `--delete-branch` flag (use `--admin` se codecov/patch flake non-blocking, mirror Wave A pattern)
- [ ] Branch eliminato local (`git branch -D feature/issue-607-game-night-public-rsvp-be`) + remote (auto via `--delete-branch`)
- [ ] `git remote prune origin` per cleanup ref
- [ ] `main-dev` fast-forwarded localmente
- [ ] Issue #607 chiusa (auto via `Closes #607`)
- [ ] Wave A.5b child issue creata sotto #579 (frontend route greenfield)
- [ ] MEMORY.md aggiornato con session entry "PR #XXX MERGED — Wave A.5a backend complete"

---

## 9. References

- Umbrella: #579 (V2 Phase 1 Wave A — SP3 public routes)
- Pattern precedent: #594 (Wave A.3a backend) → #600 (Wave A.3b frontend)
- Existing email infra: Issue #44 / #47 (game night notifications)
- Existing RSVP (auth-required): Issue #46 (GameNight API endpoints)
- Existing invite token flow (different BC): Issue #3354 (live-session invite, SessionTracking BC)
- HybridCache tag-store pitfall: #2620
- Mockup driver: `admin-mockups/design_files/sp3-accept-invite.jsx` (committed PR #568)
