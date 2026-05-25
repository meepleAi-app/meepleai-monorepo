# SP5 Admin Security S2 — Impersonate Token + Dual Principal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Introdurre il sistema di impersonation per superadmin: `Principal { Subject, Actor? }` dual-principal nel session DTO, lifecycle (start/end/auto-expiry/revoke) full-audited, gauge operazionale per il kill-switch.

**Architecture:** `UserSessionEntity` estesa con `ImpersonatedByUserId` + `ImpersonatedUntil`. `SessionStatusDto.User` refactored a `Principal { Subject: UserDto, Actor: UserDto? }`. Tre command `[AuditableAction]` (`ImpersonationStartCommand` + `[RequireTwoFactor]`, `ImpersonationEndCommand`, `RevokeImpersonationCommand`). Auth middleware verifica `RevokedAt IS NULL && ImpersonatedUntil > now` ad ogni request (invalidate-on-read, ≤5s SLA). ObservableGauge `meepleai.security.impersonation.active.count` mirror del pattern S1 T4b.

**Predecessor:** S1 (PR #1532, commit `fffe55fc7` su `main-dev`). S2 popola `_currentUserService.ImpersonatedUserId` che `AuditLoggingBehavior` legge già. Convenzione: `audit_logs.user_id` = subject, `impersonated_user_id` = actor.

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

## Task 0: Spike — DTO contract + codemod plan

> **Tipo Task:** spike read-only + design doc. Nessun codice di prodotto; output = il refactor blueprint per T2.

- [ ] **Step 1: Conferma classificazione dei 4 cluster sui 39 call site noti dal three-amigos**

  Per ognuno dei 39 call site identificati (C1 audit-attribution × 6, C2 authorization × 9, C3 resource-ownership × 23, C4 rate-limit × 1), conferma la classificazione leggendo il contesto e annotando la decisione subject vs actor. Output: tabella in `audits/2026-05-25-s2-spike-cluster-classification.md`.

- [ ] **Step 2: Identifica i pattern di estrazione (~355 occorrenze residue)**

  Le ~355 occorrenze restanti sono pattern di estrazione (`if session?.User == null`, `is SessionStatusDto { IsValid: true, ... }`). Verifica che siano TUTTE coperte da una singola sostituzione meccanica `session.User` → `session.Principal.Subject`. Documenta eventuali pattern fuori-norma nel doc spike.

- [ ] **Step 3: Sketch del codemod**

  Decidi se usare:
  - (a) IDE refactor (Rename `User`→`Principal.Subject` via Roslyn) — più sicuro, manuale.
  - (b) `sed` script sui 79 file — veloce, da verificare con build.
  - (c) Mix: IDE rename per il rename del campo + manuale per i 15 punti C1+C2 che richiedono `Actor ?? Subject`.

  Raccomando **(c)**. Documenta lo script + checklist nello spike doc.

- [ ] **Step 4: Commit spike doc**

  ```bash
  git add audits/2026-05-25-s2-spike-cluster-classification.md
  git commit -m "docs(impersonation): T0 spike — Principal refactor blueprint (SP5 S2 T0)"
  ```

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

## Task 3: `ImpersonationStartCommand` + handler

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

- [ ] **Step 2: Crea il comando + attributi**

  ```csharp
  [AuditableAction("ImpersonationStarted", "Session", Level = 2)]
  [AtomicAudit]   // la creazione della session DEVE essere audited atomically
  [RequireTwoFactor(Reason = "Impersonate other user; sensitive action.")]
  internal record ImpersonationStartCommand(
      Guid TargetUserId,
      Guid RequestingUserId,    // dal session principal — l'admin reale
      string Reason,
      int DurationMinutes = 15
  ) : ICommand<ImpersonationStartResult>;

  internal record ImpersonationStartResult(Guid SessionId, DateTime ImpersonatedUntil);
  ```

- [ ] **Step 3: Crea il validator (FluentValidation)**

  ```csharp
  RuleFor(x => x.TargetUserId).NotEmpty();
  RuleFor(x => x.RequestingUserId).NotEmpty();
  RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(500);
  RuleFor(x => x.DurationMinutes).InclusiveBetween(5, 60); // soft range; cap from SystemConfiguration
  ```

- [ ] **Step 4: Implementa il handler**

  Eligibility checks (D-S2-1):
  1. `requester.Role == "superadmin"` else `caller_not_authorized` (403)
  2. `requester.Id != target.Id` else `cannot_impersonate_self`
  3. `target.Role != "superadmin"` else `cannot_impersonate_peer_or_higher`
  4. `target.Status == "Active"` else `target_account_ineligible`
  5. `target.IsDemoAccount == false` else `target_account_ineligible`

  Crea `UserSessionEntity`:
  ```csharp
  var session = new UserSessionEntity
  {
      Id = Guid.NewGuid(),
      UserId = target.Id,                          // subject
      ImpersonatedByUserId = requester.Id,         // actor
      ImpersonatedUntil = now + cappedDuration,
      ExpiresAt = now + cappedDuration,            // mirror per session-expiry logic esistente
      TokenHash = HashSessionToken(generated),
      CreatedAt = now,
      // ... existing fields
  };
  _dbContext.UserSessions.Add(session);
  // SaveChanges in pipeline (AtomicAudit handles transaction)
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

## Task 6: Read endpoints

- [ ] **Step 1: `GetActiveImpersonationsQuery` + handler**

  ```csharp
  internal record GetActiveImpersonationsQuery(Guid? FilterByAdminUserId = null) : IQuery<IReadOnlyList<ImpersonationStatusDto>>;

  // returns: list of { sessionId, adminUserId, adminEmail, targetUserId, targetEmail, startedAt, impersonatedUntil }
  // filter: ImpersonatedByUserId IS NOT NULL AND RevokedAt IS NULL AND ImpersonatedUntil > now
  ```

  Usa l'indice filtrato (T1 step 2).

- [ ] **Step 2: Endpoints**

  ```csharp
  // apps/api/src/Api/Routing/Admin/AdminImpersonationEndpoints.cs
  app.MapPost("/api/v1/admin/impersonation/start", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapPost("/api/v1/admin/impersonation/end", ...).AddEndpointFilter<RequireSessionFilter>();
  app.MapPost("/api/v1/admin/impersonation/revoke", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapGet("/api/v1/admin/impersonation/active", ...).AddEndpointFilter<RequireSuperadminFilter>();
  app.MapGet("/api/v1/admin/impersonation/active/{adminUserId}", ...).AddEndpointFilter<RequireSuperadminFilter>();
  ```

  Tutti gli endpoint usano `IMediator.Send()` (CLAUDE.md CQRS rule).

- [ ] **Step 3: Test integration (smoke)**

- [ ] **Step 4: Commit**

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

- [ ] **Step 1: Per ogni criterio DoD (8 criteri da kickoff doc), esegui la verifica e annota**

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

- [ ] **Step 2: Compila DoD log nel kickoff doc**

  Aggiungi sezione "DoD verification log" come fatto in S1.

- [ ] **Step 3: Commit DoD log**

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
