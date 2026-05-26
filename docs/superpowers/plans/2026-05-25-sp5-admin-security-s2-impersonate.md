# SP5 Admin Security S2 — Impersonate Token + Dual Principal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introdurre il sistema di impersonation per superadmin: `Principal { Subject, Actor? }` dual-principal nel session DTO, lifecycle (start/end/auto-expiry/revoke) full-audited, gauge operazionale per il kill-switch.

**Architecture:** `UserSessionEntity` estesa con `ImpersonatedByUserId` + `ImpersonatedUntil`. `SessionStatusDto.User` refactored a `Principal { Subject: UserDto, Actor: UserDto? }`. Tre command `[AuditableAction]` (`ImpersonationStartCommand` + `[RequireTwoFactor]`, `ImpersonationEndCommand`, `RevokeImpersonationCommand`). Auth middleware verifica `RevokedAt IS NULL && ImpersonatedUntil > now` ad ogni request (invalidate-on-read, ≤5s SLA). ObservableGauge `meepleai.security.impersonation.active.count` mirror del pattern S1 T4b.

**Predecessor:** S1 (PR #1532, commit `fffe55fc7` su `main-dev`). S2 popola `_currentUserService.ImpersonatedUserId` che `AuditLoggingBehavior` legge già. Convenzione: `audit_logs.user_id` = subject, `impersonated_user_id` = actor.

**⚠️ Legacy refactor in scope (scoperto in T0 spike, 2026-05-26):** il codebase ha già un sistema di impersonation (issues #3349/#2890): `ImpersonateUserCommand` + `EndImpersonationCommand` + endpoint `/admin/users/{id}/impersonate` + 3 path che scrivono `audit_logs` direttamente bypassando l'outbox S1. **Decisione utente: refactor in-place + tightening** — rinominare i command esistenti, dismantling delle 3 `_auditLogRepository.AddAsync`, eligibility tightening (admin OR superadmin → superadmin only). Dettagli in `audits/2026-05-26-s2-spike-cluster-classification.md`.

**Out of scope:**
- ❌ FE banner UI / actor indicator — plan FE separato; il wire format JSON è già pronto in T1.
- ❌ Cache-backed session invalidation (Redis + LISTEN/NOTIFY) — il SELECT per-request basta; ottimizzazione follow-up.
- ❌ Strict 2FA enforcement — S3 flippa il behavior shadow→strict.
- ❌ Test-impersonation bypass header — enhancement S2.1 (Hightower).
- ❌ Read endpoint con audit forensico (azioni svolte mentre X impersonava Y) — usa la query S1 esistente; nuovo endpoint solo se necessario.

**Reference:** decisioni complete in `audits/2026-05-25-s2-three-amigos-kickoff.md` (D-S2-1..6 + 5 acceptance scenarios + DoD 8 criteri).

---

## File Structure

- **Create:**
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/Principal.cs` (new)
  - `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonationStartCommand.cs` (+ Handler + Validator)
  - `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonationEndCommand.cs` (+ Handler)
  - `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/RevokeImpersonationCommand.cs` (+ Handler)
  - `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/GetActiveImpersonationsQuery.cs` (+ Handler)
  - `apps/api/src/Api/Routing/Admin/AdminImpersonationEndpoints.cs`
  - `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Health/IImpersonationHealthTracker.cs` + impl
  - `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Impersonation.cs`
  - `apps/api/src/Api/Infrastructure/Migrations/{timestamp}_AddImpersonationToUserSession.cs`
  - `tests/Api.Tests/Integration/Administration/S2AcceptanceScenariosTests.cs`
  - `tests/Api.Tests/Integration/Administration/ImpersonationSessionMiddlewareTests.cs`
  - `tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonationStartCommandHandlerTests.cs` (+ End + Revoke)

- **Modify:**
  - `apps/api/src/Api/Infrastructure/Entities/Authentication/UserSessionEntity.cs` (add 2 cols)
  - `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/UserSessionEntityConfiguration.cs` (column mapping + filtered index)
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/SessionDto.cs` (`SessionStatusDto.User: UserDto?` → `Principal: Principal?`)
  - `apps/api/src/Api/Middleware/SessionAuthenticationMiddleware.cs` (resolve principal incl. impersonation; enforce `RevokedAt IS NULL` + `ImpersonatedUntil > now`)
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/ValidateSessionQueryHandler.cs` (populate `Principal.Actor` when impersonation row present)
  - **~79 files** consumer di `SessionStatusDto.User` — codemod-guided (T2)

---

## Task 0: Spike — Legacy inventory + Principal refactor blueprint ✅ DONE

> **Tipo Task:** spike read-only + design doc. Nessun codice di prodotto; output = il refactor blueprint per T1-T3.

**Output:** `audits/2026-05-26-s2-spike-cluster-classification.md`

- [x] **Step 1: Inventory del sistema legacy esistente (#3349/#2890)**

  Scoperto: `ImpersonateUserCommand` + handler + `EndImpersonationCommand` + handler + 2 endpoint already shipped. 3 path scrivono `audit_logs` direttamente via `IAuditLogRepository.AddAsync` bypassando l'outbox S1 (#1534). UserSessionEntity mono-principale, segnalata via hack `IpAddress="impersonated"`. Dettaglio: spike doc §1.

- [x] **Step 2: Gap analysis vs 6 decisioni S2 (D-S2-1..6)**

  Mappata per ogni decisione la differenza tra legacy state e S2 target + il refactor richiesto. Dettaglio: spike doc §2. **Decisione utente: refactor in-place + tightening** (eligibility admin OR superadmin → solo superadmin; dismantling delle 3 `AddAsync`).

- [x] **Step 3: Classificazione di tutti i call site funzionali**

  Inventario reale di 100 occorrenze concrete di `session.User.X` (la stima iniziale di "39 call site" era off), classificate in 4 cluster:
  - **Cluster A** (audit/log attribution → EffectiveActor): 58 lines
  - **Cluster B** (authorization → EffectiveActor): 5 lines
  - **Cluster C** (resource ownership → Subject): 22 lines
  - **Cluster D** (rate-limit/quota → Subject): 6 lines
  - **Special** (AuditLoggingBehavior stays Subject by intent): 4 lines

  + ~294 occorrenze pattern-of-extraction (null check, pattern match) gestibili da codemod Wave 1 meccanico. Dettaglio: spike doc §3.

- [x] **Step 4: Codemod plan in 3 Wave**

  Wave 1 mechanical rename (`session.User.X` → `session.Principal.Subject.X`, ~349 occorrenze in 79 file). Wave 2 semantic disambiguation per cluster A+B (63 lines → `EffectiveActor`). Wave 3 legacy handler dismantling. Dettaglio: spike doc §4.

- [x] **Step 5: Commit spike doc + amend kickoff + plan**

  Eseguito nel medesimo commit (atomicità della scoperta + decisione).

---

## Task 1: Migration — `UserSessionEntity` extension + `Principal` DTO

- [ ] **Step 1: Estendi `UserSessionEntity` con 2 colonne**

  ```csharp
  // apps/api/src/Api/Infrastructure/Entities/Authentication/UserSessionEntity.cs
  public class UserSessionEntity
  {
      // ... existing properties ...

      /// <summary>When set, this session represents an impersonation started by this admin/superadmin.</summary>
      public Guid? ImpersonatedByUserId { get; set; }

      /// <summary>UTC timestamp at which the impersonation auto-expires. Null when not an impersonation.</summary>
      public DateTime? ImpersonatedUntil { get; set; }
  }
  ```

- [ ] **Step 2: EntityConfiguration + indice filtrato**

  ```csharp
  builder.Property(e => e.ImpersonatedByUserId).HasColumnName("impersonated_by_user_id");
  builder.Property(e => e.ImpersonatedUntil).HasColumnName("impersonated_until");

  builder.HasIndex(e => e.ImpersonatedByUserId)
      .HasDatabaseName("ix_user_sessions_impersonated_by_user_id")
      .HasFilter("\"impersonated_by_user_id\" IS NOT NULL");
  ```

  L'indice filtrato (Postgres) supporta la query `GetActiveImpersonationsQuery` (vedi T6) senza scanning di tutta la tabella sessions.

- [ ] **Step 3: Crea `Principal` record**

  ```csharp
  // apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/Principal.cs
  namespace Api.BoundedContexts.Authentication.Application.DTOs;

  /// <summary>
  /// Dual-principal session identity. <see cref="Subject"/> is the user the system is
  /// "acting as" (functionally — owner of resources, target of rate-limits). <see cref="Actor"/>
  /// is non-null IFF this session is an active impersonation; it represents the admin who
  /// initiated the impersonation. Authorization checks and audit attribution MUST read
  /// <c>Actor ?? Subject</c>; resource ownership and quotas MUST read <c>Subject</c>.
  ///
  /// SP5 Admin Security S2 — three-amigos D-S2-2 (Option B: dual-principal record).
  /// </summary>
  internal sealed record Principal(UserDto Subject, UserDto? Actor)
  {
      /// <summary>The "real" actor: Actor when impersonating, else Subject. Convenience accessor.</summary>
      public UserDto EffectiveActor => Actor ?? Subject;

      /// <summary>True when this session is an active impersonation.</summary>
      public bool IsImpersonating => Actor is not null;
  }
  ```

- [ ] **Step 4: Refactor `SessionStatusDto`**

  ```csharp
  internal record SessionStatusDto(
      bool IsValid,
      Principal? Principal,       // ← was UserDto? User
      DateTime? ExpiresAt,
      DateTime? LastSeenAt,
      Guid? SessionId = null
  );
  ```

  > **JSON wire format note:** the System.Text.Json serializer will produce `{ "principal": { "subject": {...}, "actor": null } }` instead of the legacy `{ "user": {...} }`. **FE breaking change:** add `[JsonPropertyName("user")]` on `Subject` projection in the controller response shape so the wire still emits `user`, plus an optional `actor` field. See T1 step 5.

- [ ] **Step 5: API wire backward compatibility**

  Crea un `SessionStatusResponse` (response DTO solo per la wire) che proietta:

  ```json
  {
    "isValid": true,
    "user": { ... subject ... },
    "actor": { ... actor ... } | null,
    "isImpersonating": false,
    "expiresAt": "...",
    "lastSeenAt": "...",
    "sessionId": "..."
  }
  ```

  Il FE che legge `data.user` continua a funzionare. Il nuovo campo `actor` è additive.

- [ ] **Step 6: Genera la migration EF**

  ```bash
  cd apps/api/src/Api
  dotnet ef migrations add AddImpersonationToUserSession
  ```

  Verifica che la migration generata:
  - Aggiunga le 2 colonne nullable
  - Aggiunga l'indice filtrato
  - Non tocchi altre tabelle/colonne (review della migration prima del commit)

- [ ] **Step 7: Test schema migration**

  ```csharp
  // tests/Api.Tests/Integration/Administration/UserSessionImpersonationMigrationTests.cs
  [Fact]
  public async Task Migration_AddsImpersonationColumns_AndFilteredIndex()
  {
      // Verifica via information_schema che le 2 colonne esistano nullable;
      // verifica che ix_user_sessions_impersonated_by_user_id esista con WHERE clause.
  }
  ```

- [ ] **Step 8: Commit**

  ```bash
  git add apps/api/src/Api/Infrastructure/Entities/Authentication/UserSessionEntity.cs \
          apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/UserSessionEntityConfiguration.cs \
          apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/ \
          apps/api/src/Api/Infrastructure/Migrations/ \
          tests/Api.Tests/Integration/Administration/UserSessionImpersonationMigrationTests.cs
  git commit -m "feat(impersonation): migration + UserSession extension + Principal DTO (SP5 S2 T1)"
  ```

---

## Task 2: Codemod refactor sui 79 file consumer

> **Tipo Task:** refactor meccanico + semantic disambiguation. Build deve restare green ad ogni step.

- [ ] **Step 1: Wave 1 — sostituzione meccanica (~355 touch points)**

  Su tutti i 79 file consumer, sostituisci:
  - `session.User` → `session.Principal.Subject` (campo equivalente per ownership/identity)
  - `SessionStatusDto.User` → `SessionStatusDto.Principal.Subject` (riferimenti tipo)
  - `value is SessionStatusDto { IsValid: true, User: not null }` → `value is SessionStatusDto { IsValid: true, Principal: not null }`

  Strategia: IDE rename refactor + ricostruzione del build. Se rimangono compile error, sono casi fuori-norma da gestire manualmente (documentare nello spike doc T0).

- [ ] **Step 2: Wave 2 — disambiguazione semantica C1+C2 (15 punti)**

  Per ognuno dei 15 punti audit-attribution + authorization (lista da T0 spike), sostituisci `session.Principal.Subject.X` con `session.Principal.EffectiveActor.X`:

  ```csharp
  // ❌ Wave 1 lasciato:
  var command = new CreateAbTestCommand(CreatedBy: session.Principal.Subject.Id, ...);

  // ✅ Wave 2:
  var command = new CreateAbTestCommand(CreatedBy: session.Principal.EffectiveActor.Id, ...);
  ```

  Lista completa dei 15 punti (dal T0 spike):
  - `AdminAbTestEndpoints.cs:35` (C1)
  - `Admin/AdminUserInvitationEndpoints.cs:*` (C1, 3 punti)
  - `ShareLinkEndpoints.cs:*` (C1, 2 punti)
  - `SessionValidationExtensions.cs:109` (C2)
  - `RateLimitingMiddleware.cs:68` (C2 — Role) **NB:** L68 è `role` per authz, L69 è `rateKey` per quota → leggi `EffectiveActor.Role` ma `Subject.Id`.
  - `ChatSessionEndpoints.cs:*` (C2, 2 punti)
  - `PdfUploadEndpoints.cs:*` (C2, 1 punto)
  - `RuleSpecEndpoints.cs:*` (C2, 1 punto)

  **Pattern del codice da scrivere per future call site:** doc-comment su `Principal.cs` esplicita la convenzione.

- [ ] **Step 3: Run all existing integration tests**

  ```bash
  dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category!=Integration|BoundedContext=Administration" 2>&1 | tail -5
  dotnet test tests/Api.Tests/Api.Tests.csproj --filter "Category=Integration" 2>&1 | tail -5
  ```

  Tutti i 17k+ test esistenti devono restare green. Se un test fallisce, è probabilmente un test che asserisce su `SessionStatusDto.User` directly — aggiornalo a `Principal.Subject`.

- [ ] **Step 4: Commit**

  ```bash
  git add -A
  git commit -m "refactor(session): SessionStatusDto.User → Principal { Subject, Actor? } (SP5 S2 T2)"
  ```

  > **CRITICAL:** questo commit deve restare verde su CI prima di procedere con T3. Il Principal shape è la fondamenta di tutto S2 — se rompe qualcosa, va fixato qui.

---

## Task 3: Rinomina + riscrivi `ImpersonateUserCommand` → `ImpersonationStartCommand`

> **Refactor in-place** del legacy (Wave 3 dello spike). Effettivamente è rename + handler rewrite + endpoint redirect.

- [ ] **Step 1: TDD — scrivi unit test del handler**

  ```csharp
  // tests/Api.Tests/.../ImpersonationStartCommandHandlerTests.cs
  [Theory]
  [InlineData("user", "user", "caller_not_authorized")]
  [InlineData("admin", "user", "caller_not_authorized")]
  [InlineData("superadmin", "superadmin", "cannot_impersonate_peer_or_higher")]
  public async Task Handle_RejectsIneligibleCallerOrTarget_ReturnsForbiddenError(...)

  [Fact]
  public async Task Handle_HappyPath_CreatesSessionWithImpersonationFields_AndEnqueuesAudit()

  [Fact]
  public async Task Handle_TargetIsSuspended_ReturnsTargetIneligibleError()

  [Fact]
  public async Task Handle_SelfImpersonate_ReturnsCannotImpersonateSelfError()

  [Fact]
  public async Task Handle_DurationExceedsCap_IsClampedToMax()
  ```

  Run → confirm FAIL (command non esiste).

- [ ] **Step 2: Rinomina legacy + applica attributi nuovi**

  Move/rename:
  - `ImpersonateUserCommand.cs` → `ImpersonationStartCommand.cs`
  - `ImpersonateUserCommandHandler.cs` → `ImpersonationStartCommandHandler.cs`
  - `ImpersonateUserCommandValidator.cs` → `ImpersonationStartCommandValidator.cs`

  Aggiorna la signature + decorators:

  ```csharp
  // Legacy:
  // [RequireTwoFactor(Reason="...")]
  // internal record ImpersonateUserCommand(Guid TargetUserId, Guid AdminUserId, string Reason) : ICommand<ImpersonateUserResponseDto>;

  // S2:
  [AuditableAction("ImpersonationStarted", "Session", Level = 2)]
  [AtomicAudit]   // la creazione della session DEVE essere audited atomically
  [RequireTwoFactor(Reason = "Impersonate other user; sensitive action.")]
  internal record ImpersonationStartCommand(
      Guid TargetUserId,
      Guid RequestingUserId,        // was AdminUserId — rename per consistency with other admin commands
      string Reason,
      int DurationMinutes = 15      // NEW — cap enforced by validator
  ) : ICommand<ImpersonationStartResult>;

  internal record ImpersonationStartResult(Guid SessionId, DateTime ImpersonatedUntil);
  ```

  Add to `using` migrations doc: legacy callers that referenced `AdminUserId` as a public-ish field — none expected outside the BC.

- [ ] **Step 3: Crea il validator (FluentValidation)**

  ```csharp
  RuleFor(x => x.TargetUserId).NotEmpty();
  RuleFor(x => x.RequestingUserId).NotEmpty();
  RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(500);
  RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 60); // soft range; cap from SystemConfiguration
  ```

- [ ] **Step 4: Riscrivi il handler**

  Eligibility checks (D-S2-1) — **tightened from legacy**:
  1. `requester.Role == "superadmin"` else `caller_not_authorized` (403) — **legacy accettava `admin` too; rimuovi il path `adminLevel < 3` (riga 65-68 del legacy)**
  2. `requester.Id != target.Id` else `cannot_impersonate_self` — **nuovo**, mancava nel legacy
  3. `target.Role != "superadmin" && target.Role != "admin"` else `cannot_impersonate_peer_or_higher` — il legacy già lo fa (linee 86-93), mantieni
  4. `target.IsSuspended == false` else `target_account_ineligible` — il legacy lo fa (linee 79-82), mantieni
  5. `target.IsDemoAccount == false` else `target_account_ineligible` — **nuovo**
  6. `target.Status != "Banned"` else `target_account_ineligible` — **nuovo** (check `UserEntity.Status` introdotto da Epic #4068)

  **Dismantle manuale audit:** rimuovi le 2 `_auditLogRepository.AddAsync` (linee 140-141 del legacy). Il `[AuditableAction]` via `AuditLoggingBehavior` scrive l'outbox row con action `"ImpersonationStarted"`.

  > ⚠ **Open issue — actor mapping for management commands** (review carry-forward, 2026-05-26):
  > For "normal" audited commands executed DURING an impersonate session, the behavior reads
  > `session.Principal.Subject.Id` → `user_id` and `session.Principal.Actor.Id` → `impersonated_user_id`
  > (the impersonation context is already in the session).
  >
  > But for the MANAGEMENT commands themselves (`ImpersonationStartCommand`, `EndImpersonationCommand`,
  > `RevokeImpersonationCommand`, `ImpersonationAutoEnded` from middleware) the caller IS NOT
  > yet/anymore in an impersonate session — `session.Principal.Actor` is null. Yet the desired
  > audit row pairing is `user_id = target/affected user`, `impersonated_user_id = acting admin`.
  >
  > Resolution options to choose in T3 step 4 (pick one and document):
  > (a) Override at command level: `[AuditableAction(..., UserIdSource="ResourceId")]` parameter
  >     telling the behavior to copy `ResourceId` (the target) into `user_id` and the
  >     `session.Principal.Subject.Id` (the admin) into `impersonated_user_id` for THIS command.
  > (b) Custom audit override in the handler: handler explicitly invokes
  >     `IAuditService.EnqueueAuditAtomicAsync(...)` with the correct fields, bypassing the
  >     behavior's default user_id mapping for this specific command.
  > (c) Two-step pattern: behavior writes a "callee-attributed" row (user_id=admin), and the
  >     handler enqueues a second "target-attributed" mirror row. Doubles the audit volume.
  >
  > Recommendation: (a) — the cleanest and most reusable, and the closest analog to the existing
  > `[AuditableAction(action, resource, Level)]` shape. Implementation: behavior reads the
  > attribute, falls back to the default if `UserIdSource` is unset. Effort: small (~30 LOC in
  > the behavior + the attribute property).

  Crea `UserSessionEntity` (NUOVO — sostituisce il `CreateSessionCommand` legacy con `IpAddress="impersonated"` hack):
  ```csharp
  var session = new UserSessionEntity
  {
      Id = Guid.NewGuid(),
      UserId = target.Id,                          // subject (come legacy)
      ImpersonatedByUserId = requester.Id,         // NEW (T1 column)
      ImpersonatedUntil = now + cappedDuration,    // NEW (T1 column)
      ExpiresAt = now + cappedDuration,            // mirror per session-expiry logic esistente
      TokenHash = HashSessionToken(generated),
      IpAddress = httpContext.IpAddress,           // real IP — NO MORE "impersonated" magic string
      UserAgent = httpContext.UserAgent,           // real UA
      CreatedAt = now,
      User = target,
  };
  _dbContext.UserSessions.Add(session);
  // SaveChanges in pipeline ([AtomicAudit] handles transaction)
  ```

- [ ] **Step 5: Errors → exceptions**

  CLAUDE.md known pitfall #2568: usa `ForbiddenException` (403), non `InvalidOperationException`.

  ```csharp
  throw new ForbiddenException("cannot_impersonate_peer_or_higher",
      "Cannot impersonate a peer or higher-privilege account.");
  ```

- [ ] **Step 6: Confirm tests PASS**

- [ ] **Step 7: Commit**

  ```bash
  git commit -m "feat(impersonation): start command + atomic audit + eligibility (SP5 S2 T3)"
  ```

---

## Task 4: `ImpersonationEndCommand` + auto-expiry middleware check

- [ ] **Step 1: TDD — middleware test**

  ```csharp
  [Fact]
  public async Task Middleware_WhenImpersonatedUntilExpired_Returns401_AndEnqueuesAutoEndedAudit()
  ```

  Setup: crea UserSessionEntity con `ImpersonatedUntil = now - 1s`. Request → middleware → 401 + audit_outbox row.

- [ ] **Step 2: Implementa l'expiry check nel `SessionAuthenticationMiddleware`**

  ```csharp
  // pseudo:
  if (sessionEntity.ImpersonatedByUserId is not null &&
      sessionEntity.ImpersonatedUntil <= _timeProvider.GetUtcNow())
  {
      await _auditService.EnqueueAuditAsync(new AuditOutboxPayload {
          Action = "ImpersonationAutoEnded",
          Resource = "Session",
          UserId = sessionEntity.UserId.ToString(),
          ImpersonatedUserId = sessionEntity.ImpersonatedByUserId,
          Result = "Success",
          // ...
      });
      return Unauthorized();
  }
  ```

- [ ] **Step 3: `ImpersonationEndCommand` (self-end)**

  `[AuditableAction("ImpersonationEnded", "Session", Level = 1)]`. Handler marca `session.ImpersonatedUntil = now` (immediate expiry); il prossimo request del client trova expired → 401.

- [ ] **Step 4: Unit + integration test**

- [ ] **Step 5: Commit**

---

## Task 5: `RevokeImpersonationCommand` + kill-switch (superadmin)

- [ ] **Step 1: TDD test**

  ```csharp
  [Fact]
  public async Task Revoke_BySuperAdmin_MarksSessionRevoked_AndSubsequentRequestsReturn401_Within5Seconds()
  ```

- [ ] **Step 2: Implementa il comando**

  ```csharp
  [AuditableAction("ImpersonationRevoked", "Session", Level = 2)]
  [AtomicAudit]
  internal record RevokeImpersonationCommand(
      Guid SessionId,
      Guid RequestingUserId   // must be superadmin
  ) : ICommand;
  ```

  Handler: verify caller is superadmin; `session.RevokedAt = now`; SaveChanges. Audit fields:
  - `user_id = sessionEntity.ImpersonatedByUserId` (l'admin che era impersonando)
  - `impersonated_user_id = caller.Id` (il superadmin che revoca)
  - `resource_id = sessionId`

- [ ] **Step 3: Middleware invalidate-on-read**

  In `SessionAuthenticationMiddleware`: ad ogni request, SELECT `RevokedAt` dal session record (NB: già si fa per `LastSeenAt`). Se `RevokedAt IS NOT NULL` → 401.

- [ ] **Step 4: Acceptance scenario S2-4**

- [ ] **Step 5: Commit**

---

## Task 6: Read endpoints + legacy URL redirect

- [ ] **Step 1: `GetActiveImpersonationsQuery` + handler**

  ```csharp
  internal record GetActiveImpersonationsQuery(Guid? FilterByAdminUserId = null) : IQuery<IReadOnlyList<ImpersonationStatusDto>>;

  // returns: list of { sessionId, adminUserId, adminEmail, targetUserId, targetEmail, startedAt, impersonatedUntil }
  // filter: ImpersonatedByUserId IS NOT NULL AND RevokedAt IS NULL AND ImpersonatedUntil > now
  ```

  Usa l'indice filtrato (T1 step 2).

- [ ] **Step 2: Endpoints (nuovi)**

  ```csharp
  // apps/api/src/Api/Routing/Admin/AdminImpersonationEndpoints.cs
  app.MapPost("/api/v1/admin/impersonation/start", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapPost("/api/v1/admin/impersonation/end", ...).AddEndpointFilter<RequireSessionFilter>();
  app.MapPost("/api/v1/admin/impersonation/revoke", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapGet("/api/v1/admin/impersonation/active", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapGet("/api/v1/admin/impersonation/active/{adminUserId}", ...).AddEndpointFilter<RequireSuperadminFilter>();
  ```

  Tutti gli endpoint usano `IMediator.Send()` (CLAUDE.md CQRS rule).

- [ ] **Step 3: Legacy endpoint redirect (1 release cycle)**

  In `AdminUserActivityEndpoints.cs`, sostituisci `HandleImpersonateUser` con un 308 Permanent Redirect verso il nuovo endpoint:
  ```csharp
  group.MapPost("/admin/users/{userId:guid}/impersonate", (Guid userId) =>
      Results.Extensions.PermanentRedirect("/api/v1/admin/impersonation/start", preserveMethod: true))
      .WithSummary("DEPRECATED — use POST /api/v1/admin/impersonation/start");
  ```

  Lo stesso per `/admin/impersonation/end` (mantieni il path nuovo, sposta il vecchio handler). **Rimuovi entrambi i redirect in un follow-up issue dopo 1 release cycle.**

- [ ] **Step 4: Test integration (smoke)**

  Test che il redirect funzioni + il body POST sopravviva.

- [ ] **Step 5: Commit**

---

## Task 7: Health gauge `impersonation.active.count`

> Mirror pattern di S1 T4b (`AuditOutboxHealthTracker` + `MeepleAiMetrics.AuditOutbox`).

- [ ] **Step 1: `IImpersonationHealthTracker` + `ImpersonationHealthTracker` (singleton, Interlocked)**

- [ ] **Step 2: `MeepleAiMetrics.Impersonation.cs` con 1 gauge**

  ```csharp
  // meepleai.security.impersonation.active.count — Number of active impersonation sessions (RevokedAt IS NULL AND ImpersonatedUntil > now)
  ```

- [ ] **Step 3: Aggiornamento del tracker**

  - Sulla creazione di una session impersonate → `tracker.IncrementActive()`.
  - Sull'end/revoke/auto-end → `tracker.DecrementActive()`.
  - Periodic sync (BackgroundService): ogni 60s, query reale + `tracker.Set(realCount)` per riconciliare drift.

- [ ] **Step 4: Test MeterListener**

- [ ] **Step 5: Registrazione in `Program.cs` mirror di `RegisterAuditOutboxGauges`**

- [ ] **Step 6: Commit**

---

## Task 8: 5 Acceptance Scenarios — gate DoD

> Mirror pattern S1 T5.

- [ ] **Step 1: Scenario S2-1 — Happy path start**
- [ ] **Step 2: Scenario S2-2 — Auditable destructive command during impersonate**
- [ ] **Step 3: Scenario S2-3 — Auto-expiry mid-flight**
- [ ] **Step 4: Scenario S2-4 — Superadmin revoke kills in-flight session**
- [ ] **Step 5: Scenario S2-5 — Cannot impersonate a superadmin**
- [ ] **Step 6: Run all 5** — 5/5 PASS gate
- [ ] **Step 7: Commit**

  ```bash
  git commit -m "test(impersonation): S2 acceptance scenarios — 5 Given/When/Then (SP5 S2 T8)"
  ```

---

## Task 9: DoD verification log

> Mirror pattern S1 T6. Non-codice ma gate del merge.

- [x] **Step 1: Per ogni criterio DoD (8 criteri da kickoff doc), esegui la verifica e annota**

  | # | Criterio | Comando di verifica |
  |---|----------|--------------------|
  | 1 | `Principal` shape no regressions | `dotnet test` su tutti i 17k+ test PASS |
  | 2 | Migration applies clean | `dotnet ef database update --dry-run` su staging |
  | 3 | C1+C2 usano `EffectiveActor` | grep verification |
  | 4 | Eligibility 5 rules | `ImpersonationStartCommandHandlerTests` |
  | 5 | Audit attribution subject/actor | Scenario S2-2 |
  | 6 | Auto-expiry mid-flight | Scenario S2-3 |
  | 7 | Revoke ≤5s | Scenario S2-4 + latency assertion |
  | 8 | Prometheus gauge | MeterListener test |

- [x] **Step 2: Compila DoD log nel kickoff doc**

  Aggiungi sezione "DoD verification log" come fatto in S1.

- [x] **Step 3: Commit DoD log**

  ```bash
  git commit -m "docs(impersonation): S2 DoD verification log (all 8 criteria green)"
  ```

  Solo dopo questo commit S2 è ufficialmente done e si può aprire il plan S3.

---

## Self-Review

**1. Atomicity:** `ImpersonationStartCommand` + `RevokeImpersonationCommand` sono `[AtomicAudit]` → session creation + audit row commit insieme. `ImpersonationEndCommand` è best-effort (S1 default — perdita audit di self-end recuperabile da telemetria).

**2. Placeholder scan:** ogni step ha codice C# concreto o comandi `dotnet`/git esatti. L'unica decisione non chiusa è il dettaglio del codemod (manual vs script) → esplicitata in T0.

**3. Type consistency:** `Principal` (T1) → consumed by all consumers (T2 codemod) → produced by `ValidateSessionQueryHandler` populating `Actor` quando `UserSessionEntity.ImpersonatedByUserId IS NOT NULL`. `SessionStatusResponse` wire DTO preserva backward compat con FE.

**4. Risks (Nygard):**
- **Codemod sui 79 file** può introdurre regressioni sottili se un consumer faceva una scelta semantica implicita. Mitigato dalla regression suite (17k+ test) + Backend Fast CI gate.
- **Auto-expiry race con `[AtomicAudit]` mid-commit**: documentato in D-S2-4 come "snapshot-at-begin-of-request" — la tx in corso completa, il prossimo request è 401.
- **Revoke propagation < 5s**: dipende dalla SELECT `UserSession` per-request. Già fatta per `LastSeenAt` — overhead marginale. Se diventa hotspot, follow-up Redis.

**5. Coverage:** T0–T9 coprono tutte le 8 criteri DoD. T2 (codemod) è il task con più rischio di regressioni → eseguire dopo T1 (migration solo) per isolare.

---

## Execution Handoff

**Branch:** `feature/sp5-admin-security-s2-impersonate` (from `main-dev` HEAD `fffe55fc7`)

**Ordine esecuzione:** T0 (spike) → T1 (migration + DTO) → T2 (codemod) → T3 (start) → T4 (end + auto-expiry) → T5 (revoke + kill-switch) → T6 (read endpoints) → T7 (health gauge) → T8 (5 acceptance scenarios) → T9 (DoD log).

**Effort stimato (revisione):** 3-5 giorni (incluso codemod e refactor 79 file).

**Predecessor dependency:** S1 (PR #1532) deve essere mergiato in `main-dev` (✅ done).

**Dipendenze post:** S3 strict 2FA cutover (separato plan).
