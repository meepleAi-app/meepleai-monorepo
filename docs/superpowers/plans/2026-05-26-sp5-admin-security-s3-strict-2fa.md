# SP5 Admin Security S3 — Strict 2FA Enforcement Cutover Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Flippare `TwoFactorEnforcementBehavior` da shadow → strict. Tracciare la recency del TOTP a livello sessione (`LastTotpVerifiedAt`), bloccare 4 comandi sensibili quando la verifica è stale/assente, fornire un endpoint `POST /auth/2fa/step-up`, gated da config-flag `TwoFactorStrictMode` con default=OFF al merge.

**Architecture:** `UserSessionEntity.LastTotpVerifiedAt` (timestamptz nullable). `TwoFactorEnforcementBehavior` strict path throw `TwoFactorRequiredException` quando (2FA off) ∨ (LastTotpVerifiedAt null ∨ stale per `MaxAgeMinutes`). ExceptionFilter mappa a 401 + structured body + `WWW-Authenticate: TOTP-StepUp` header. Step-up endpoint con rate-limit DB-backed. Tutto behind `SystemConfiguration.TwoFactorStrictMode` flag (default false).

**Predecessor:** S2 (PR #1555, commit `7ca5b5151` su `main-dev`). Riutilizzo: `Principal { Subject, Actor? }`, `ExtractImpersonationActorId` per audit attribution durante impersonate, `SessionStatusResponse` wire format con backward-compat.

**Out of scope:**
- ❌ FE step-up modal/banner (plan FE separato; wire format spec in D-S3-2)
- ❌ WebAuthn / passkeys (#186 P1.2)
- ❌ Recovery codes / backup factors (Q2 §11 D5)
- ❌ Auto-enrollment forzato (ops task)
- ❌ Removal del flag (follow-up dopo 1 sprint prod-stable)

**Reference:** decisioni complete in `audits/2026-05-26-s3-three-amigos-kickoff.md` (D-S3-1..7 + 8 acceptance scenarios + DoD 10 criteri).

---

## File Structure

- **Create:**
  - `apps/api/src/Api/SharedKernel/Domain/Exceptions/TwoFactorRequiredException.cs`
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/StepUpTwoFactorCommand.cs` (+ Handler + Validator)
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetAdminsWithoutTwoFactorQuery.cs` (+ Handler)
  - `apps/api/src/Api/Routing/Auth/TwoFactorStepUpEndpoints.cs`
  - `apps/api/src/Api/Routing/Admin/AdminTwoFactorComplianceEndpoints.cs`
  - `apps/api/src/Api/Middleware/TwoFactorRequiredExceptionFilter.cs` (or extend existing exception filter)
  - `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/RateLimit/TwoFactorStepUpRateLimiter.cs`
  - `apps/api/src/Api/Infrastructure/Entities/Authentication/TwoFactorStepUpAttemptEntity.cs` (for rate-limit counter)
  - `apps/api/src/Api/Infrastructure/Migrations/{ts}_AddTwoFactorStrictMode.cs`
  - `docs/api/2fa-step-up-protocol.md` (wire format spec for FE handoff)
  - `tests/Api.Tests/Integration/Administration/S3AcceptanceScenariosTests.cs`
  - `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/StepUpTwoFactorCommandHandlerTests.cs`
  - `tests/Api.Tests/BoundedContexts/Authentication/Application/Behaviors/TwoFactorEnforcementBehaviorStrictTests.cs`

- **Modify:**
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/Behaviors/TwoFactorEnforcementBehavior.cs` (strict path + flag read)
  - `apps/api/src/Api/BoundedContexts/Authentication/Application/Attributes/RequireTwoFactorAttribute.cs` (doc update only — semantics unchanged)
  - `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonationStartCommand.cs` (MaxAgeMinutes = 5)
  - `apps/api/src/Api/Infrastructure/Entities/Authentication/UserSessionEntity.cs` (LastTotpVerifiedAt)
  - `apps/api/src/Api/Infrastructure/EntityConfigurations/Authentication/UserSessionEntityConfiguration.cs`
  - `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/Session.cs` (LastTotpVerifiedAt + UpdateTotpVerified method)
  - `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/SessionRepository.cs` (hydrate)
  - `apps/api/src/Api/BoundedContexts/SystemConfiguration/...` (TwoFactorStrictMode config key, default false)
  - `apps/api/src/Api/Program.cs` (wire new endpoints + rate-limiter + exception filter)

---

## Task 0: Spike — Inventory + cutover risk assessment

> **Tipo Task:** spike read-only + design doc. Nessun codice di prodotto; output = il risk + readiness blueprint per T1-T9.

**Output:** `audits/2026-05-26-s3-spike-cutover-readiness.md`

- [ ] **Step 1: Inventory dei 4 comandi decorati `[RequireTwoFactor]`** — confermare MaxAge attuale, identificare i call site, mappare l'audit emission attuale (action names) per gli scenari S3-1..S3-8.

- [ ] **Step 2: Inventario sistema 2FA enrollment esistente** — dove vive il TOTP secret? Quale endpoint setta `IsTwoFactorEnabled=true`? Come funziona oggi la verifica TOTP (interfaccia + library)? Identifica i punti dove inserire l'aggiornamento di `LastTotpVerifiedAt` (probabile: login + enroll-verify + step-up).

- [ ] **Step 3: Conta gli admin senza 2FA in dev/staging** — `SELECT count(*) FROM users WHERE (role='admin' OR role='superadmin') AND is_two_factor_enabled=false`. Output: numero da sweep prima del flip prod (D-S3-5).

- [ ] **Step 4: Identifica i 401 handler esistenti nel FE** — `apps/web/src/lib/api/*` — verifica che esista logica per distinguere `subcode` nel body. Output: gap document per il plan FE separato.

- [ ] **Step 5: Risk matrix (Nygard)** — failure modes documentati: (a) TOTP store unavailable, (b) rate-limit counter DB unreachable, (c) flag toggle race, (d) sessione esistente con `LastTotpVerifiedAt=null` al flip → tutte richiedono step-up (questo è l'effetto voluto post-sweep).

- [ ] **Step 6: Commit spike doc + amend kickoff se servono refinement**

---

## Task 1: Migration + `LastTotpVerifiedAt` + Session domain

- [ ] **Step 1: Estendi `UserSessionEntity` con la colonna**

  ```csharp
  public DateTime? LastTotpVerifiedAt { get; set; }
  ```

- [ ] **Step 2: `UserSessionEntityConfiguration` mapping**

  ```csharp
  builder.Property(e => e.LastTotpVerifiedAt).HasColumnName("last_totp_verified_at");
  ```

- [ ] **Step 3: `Session` domain entity**

  Aggiungi proprietà + `UpdateTotpVerified(TimeProvider)` metodo; `IsTotpRecent(int maxAgeMinutes, TimeProvider)` query.

- [ ] **Step 4: `SessionRepository.MapToPersistence` + `MapToDomain` hydrate**

- [ ] **Step 5: Genera migration EF**

  ```bash
  cd apps/api/src/Api
  dotnet ef migrations add AddTwoFactorRecencyToUserSession
  ```

- [ ] **Step 6: Test schema migration (Testcontainers)**

- [ ] **Step 7: Commit**

  ```bash
  git commit -m "feat(2fa): migration + LastTotpVerifiedAt on UserSession (SP5 S3 T1)"
  ```

---

## Task 2: `TwoFactorRequiredException` + ExceptionFilter

- [ ] **Step 1: TDD — unit test su ExceptionFilter mapping**

- [ ] **Step 2: Crea `TwoFactorRequiredException`** in `SharedKernel.Domain.Exceptions` con `Subcode` enum (`StepUpRequired`, `EnrollRequired`, `LockedOut`).

- [ ] **Step 3: Estendi il global exception filter** per mappare → 401 + body `{ error, subcode }` + header `WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"`.

- [ ] **Step 4: Test PASS**

- [ ] **Step 5: Commit**

---

## Task 3: `SystemConfiguration.TwoFactorStrictMode` flag (default false)

- [ ] **Step 1: Aggiungi key** al SystemConfiguration BC (pattern `RegistrationMode`).

- [ ] **Step 2: Admin toggle UI** — endpoint esistente `/api/v1/admin/config` deve esporre la nuova key per il toggle.

- [ ] **Step 3: Read non-cached** — il behavior legge il flag a ogni request (no Redis cache su questo specifico flag — vedi D-S3-1).

- [ ] **Step 4: Unit test su read/write del flag**

- [ ] **Step 5: Commit**

---

## Task 4: `TwoFactorEnforcementBehavior` strict path

- [ ] **Step 1: TDD — unit test scenari S3-1, S3-2, S3-3, S3-7, S3-8** (con mock `ISystemConfiguration`)

- [ ] **Step 2: Strict path implementation**

  Pseudo:
  ```csharp
  if (!_systemConfig.GetBool("TwoFactorStrictMode")) {
      // Shadow path (esistente, log only)
      LogShadow(...);
      return await next();
  }

  if (!user.IsTwoFactorEnabled) {
      throw new TwoFactorRequiredException(Subcode.EnrollRequired, ...);
  }

  var session = ExtractSession();
  var maxAge = TimeSpan.FromMinutes(attr.MaxAgeMinutes);
  if (session.LastTotpVerifiedAt is null
      || _timeProvider.GetUtcNow() - session.LastTotpVerifiedAt > maxAge) {
      // Emit best-effort audit "TwoFactorRequired" via _auditService
      throw new TwoFactorRequiredException(Subcode.StepUpRequired, ...);
  }

  return await next();
  ```

- [ ] **Step 3: Aggiorna `ImpersonationStartCommand`** con `[RequireTwoFactor(MaxAgeMinutes = 5)]` (D-S3-7).

- [ ] **Step 4: Test PASS** (5 scenari unit)

- [ ] **Step 5: Commit**

---

## Task 5: Step-up endpoint + DB-backed rate-limit

- [ ] **Step 1: TDD — scenari S3-4 + S3-5**

- [ ] **Step 2: `StepUpTwoFactorCommand` + handler**

  Verifica TOTP code (riusa la library 2FA esistente), update `session.LastTotpVerifiedAt`, emit audit `TwoFactorStepUp`.

- [ ] **Step 3: `TwoFactorStepUpAttemptEntity` + migration**

  Tabella tracking attempts per rate-limit cross-instance (5/min, lockout 15min).

- [ ] **Step 4: `TwoFactorStepUpRateLimiter`** — DB counter (pattern `BggRateLimitMiddleware`).

- [ ] **Step 5: Endpoint `POST /api/v1/auth/2fa/step-up`** — `IMediator.Send` only (CQRS).

- [ ] **Step 6: Test PASS**

- [ ] **Step 7: Commit**

---

## Task 6: Admin sweep endpoint `GET /admin/users/no-2fa`

- [ ] **Step 1: TDD — integration test (superadmin auth gate)**

- [ ] **Step 2: `GetAdminsWithoutTwoFactorQuery` + handler** — filtra `IsTwoFactorEnabled=false AND Role IN ('admin','superadmin')`.

- [ ] **Step 3: Endpoint in `AdminTwoFactorComplianceEndpoints.cs`** con `RequireSuperAdmin` gate.

- [ ] **Step 4: Test PASS**

- [ ] **Step 5: Commit**

---

## Task 7: Wire format spec `docs/api/2fa-step-up-protocol.md`

- [ ] **Step 1: Documenta endpoint contract**

  - Request: `POST /api/v1/auth/2fa/step-up { code: string }`
  - Response 200: `SessionStatusResponse` (riusa shape S2) con `lastTotpVerifiedAt` rinfrescato
  - Response 401: `{ error: "two_factor_required", subcode: "invalid_code" | "locked_out" }`
  - Response 503: `{ error: "two_factor_unavailable" }`

- [ ] **Step 2: Documenta `subcode` semantics** — quando il FE deve mostrare modale step-up vs redirect-to-login vs enroll prompt.

- [ ] **Step 3: Cross-link** dal commit message e dalla PR description per FE handoff.

- [ ] **Step 4: Commit**

---

## Task 8: 8 acceptance scenarios — gate DoD

> Mirror S2 T8.

- [ ] **Step 1-8**: implementa S3-1, S3-2, S3-3, S3-4, S3-5, S3-6, S3-7, S3-8 in `S3AcceptanceScenariosTests.cs`.

  **CRITICAL** (lezione S2): tutti gli scenari DEVONO esercitare la pipeline reale via `ValidateSessionQueryHandler` + `TwoFactorEnforcementBehavior`, NON costruire `SessionStatusDto` manualmente. Vedi [[feedback_acceptance_tests_must_exercise_real_pipeline]].

- [ ] **Step 9: Run all 8 — 8/8 PASS gate**

- [ ] **Step 10: Commit**

---

## Task 9: DoD verification log

> Mirror S2 T9.

- [ ] **Step 1**: per ognuno dei 10 criteri DoD, esegui verifica + annota evidenza
- [ ] **Step 2**: compila DoD log in `audits/2026-05-26-s3-three-amigos-kickoff.md` (sezione "DoD verification log")
- [ ] **Step 3**: commit DoD log
- [ ] **Step 4**: flip PR draft→ready, code review, merge in `main-dev`

---

## Self-Review

**1. Atomicity:** lo step-up command è best-effort sull'audit (S1 default — verifying TOTP non è distruttivo; perdita audit recuperabile da telemetria). `TwoFactorRequiredException` throw BEFORE handler execution → nessuna mutazione → atomicità garantita by design (no partial state).

**2. Cutover safety:** flag default=OFF al merge = zero-risk deploy. Il flip è una decisione ops cosciente in staging-then-prod. Sweep pre-flip (T6 endpoint) garantisce 100% admin enrolled.

**3. Lezione S2 applicata:** DoD criterio #8 esplicito — pipeline reale via `ValidateSessionQueryHandler` + behavior. Acceptance scenarios non costruiscono `SessionStatusDto` manualmente.

**4. Backward compat FE:** wire format `{ error, subcode, WWW-Authenticate }` documentato in T7. FE refactor è OUT OF SCOPE; il FE attuale che redirecta a /login su qualsiasi 401 continua a funzionare (degrade silente) finché ops non flip strict in prod.

**5. Risks (Nygard):**
- **Rate-limit counter DB scrittura**: ogni step-up tentato fa una INSERT. Volume admin atteso: <100/giorno → trascurabile.
- **Flag read frequenza**: ogni request `[RequireTwoFactor]` legge il flag. Cache in-memory L1 con TTL 5s per evitare DB hot-path.
- **TOTP store availability**: se la library 2FA fallisce, step-up restituisce 503 → admin non si sblocca. Mitigation: la verifica TOTP usa SOLO secret store (DB) + RFC 6238 math, no external dep.

---

## Execution Handoff

**Branch:** `feature/sp5-admin-security-s3-strict-2fa` (from `main-dev` HEAD `7ca5b5151`)

**Ordine esecuzione:** T0 (spike) → T1 (migration + Session) → T2 (exception + filter) → T3 (config flag) → T4 (behavior strict) → T5 (step-up endpoint + rate-limit) → T6 (sweep endpoint) → T7 (wire format doc) → T8 (8 acceptance scenarios) → T9 (DoD log + merge).

**Effort stimato:** 4-6 giorni.

**Predecessor dependency:** S2 (PR #1555) ✅ MERGED in `main-dev`.

**Post-merge ops sequence:**
1. Merge S3 in main-dev (flag default=OFF — zero impact)
2. Deploy to staging
3. Ops chiama `GET /admin/users/no-2fa` → sweep + email enrollment
4. Ops flip `TwoFactorStrictMode=true` in staging admin config
5. Dogfooding 24h in staging
6. Repeat 3-4 in prod
7. After 1 sprint prod-stable → follow-up issue per rimuovere il flag (hard-code true)
