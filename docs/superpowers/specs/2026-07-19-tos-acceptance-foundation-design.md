# ToS Acceptance Foundation — Design Spec

**Date:** 2026-07-19
**Issue:** #2954 (prereq PR-2 of epic #539), finding **F1** (re-consenso)
**Branch:** `feature/issue-2954-tos-acceptance-foundation` → PR to `main-dev`
**Parent context:** spec-panel review of ToS/legal docs (2026-07-19 session) + 5-agent investigation workflow.

---

## 1. Context & problem

The Mechanic Extractor ToS §5 was materially changed (PR #3000, merged on `main-dev`, **not** deployed to prod): the OLD §5 stated uploaded content is *"processed for personal use only, not shared with other users"*; the NEW §5 authorizes MeepleAI to **publish derived comprehension cards** visible to other authenticated users. The consultant flagged re-consent for existing users as an **open decision** ("did not exclude"; `domanda 8`; deliverable *"Parere sul re-consenso"* still outstanding).

A 5-agent investigation established (all high-confidence, file:line evidence in the session transcript) that **there is no ToS-acceptance infrastructure at all**:

- **No per-user ToS acceptance record** — no column on `User`/`Session`, no table. Acceptance is never persisted.
- **New registrations lose the signal too**: `RegisterForm.tsx` builds `termsAcceptedAt: new Date()` (commented *"GDPR audit requirement"*) but `register/_content.tsx` forwards only `{email, password}`; `RegisterRequest` (authClient) has no terms field. The required checkbox is **client-cosmetic only** — no server-side enforcement.
- **No ToS versioning** — the only version signal is the literal `lastUpdated={new Date('2026-07-15')}` in `terms/page.tsx`.
- **No re-acceptance gate** — the `EmailVerificationMiddleware` / `User.RequiresVerification` pattern exists as a template but has no ToS analog.
- The AI-consent versioning (`UserAiConsent`) is nominal (frontend literal `1.0.0`, never enforced, single-row overwriting, AI-specific, check-service is dead code) → **not reusable**.

## 2. Decision this spec implements

Per user decision **"Fondazione ora, gate dopo"**: build the recording/versioning **foundation** now — needed under *every* possible legal answer and also closes the existing "no acceptance record for anyone" gap — and defer the **enforcement-strength** decision (blocking gate vs. active notice) to the final professional legal review that already gates prod deployment.

The data model is an **append-only** record (chosen over 2-columns-on-User and over generalizing `UserAiConsent`), matching the legal analyst's explicit condition that the record preserve an audit history of *which* version was accepted *when*.

## 3. Goal & scope boundary

### In scope (foundation)
1. Append-only per-user ToS acceptance record + persistence.
2. Server-authoritative current ToS version (single source of truth).
3. Record acceptance at registration — fix the dropped `termsAcceptedAt` **and** enforce acceptance server-side (close the cosmetic-checkbox gap).
4. An authenticated endpoint to record an acceptance (what a *future* re-consent gate will call) + a status read-model (`needsReAcceptance`) the future gate will consume.

### Out of scope (enforcement — deferred to post-legal-opinion)
- ❌ No blocking login gate / middleware.
- ❌ No frontend re-accept modal.
- ❌ No forced re-consent / notification campaign for existing users.
- ❌ No backfill of existing users.

`needsReAcceptance` is **computed and exposed** but **nothing consumes it yet**. This boundary is a hard requirement of the design — building enforcement now would pre-empt the legal decision.

## 4. Data model

New table **`user_terms_acceptances`** (append-only), in the **Authentication** bounded context (co-located with `User` and registration).

Entity `TermsAcceptance` (sealed, private setters, private EF ctor, static `Create` factory — mirrors `UserAiConsent` conventions):

| Field | Type | Notes |
|---|---|---|
| `Id` | `Guid` PK | `Guid.NewGuid()` in factory |
| `UserId` | `Guid` | FK → `users.Id`, `OnDelete(Cascade)` |
| `TermsVersion` | `string` (max 50) | e.g. `"2026-07-15"` |
| `AcceptedAt` | `DateTime` (UTC) | **server-stamped** |
| `Context` | `string` (max 32) | `"Registration"` \| `"ReConsent"` — **string, not int+CHECK** to avoid the enum-range CHECK-drift pitfall (#2974) |
| `IpAddress` | `string?` (max 45) | nullable, audit |
| `UserAgent` | `string?` (max 512) | nullable, audit |
| `CreatedAt` | `DateTime` (UTC) | standard audit column |

**Factory:** `TermsAcceptance.Create(userId, termsVersion, context, ipAddress?, userAgent?)` — throws `ArgumentException` on empty `userId` / blank `termsVersion` (mirrors `UserAiConsent.Create`).

**Context type:** modeled as a C# `enum TermsAcceptanceContext { Registration, ReConsent }` at the domain boundary, **persisted as its string name** (`.HasConversion<string>()`), so growing the enum never requires a DB constraint change.

**Indexes:** `(UserId, AcceptedAt DESC)` for the "latest acceptance" lookup. **No unique constraint** (append-only allows N rows/user).

**Migration:** `AddUserTermsAcceptances` — `CreateTable` + FK + index. Pure EF-generated (no `migrationBuilder.Sql()`) → immune to the flatten-drops-raw-SQL pitfall. **No backfill**: existing users have no row → `needsReAcceptance = true` once a gate exists (correct semantics).

## 5. Current ToS version — single source of truth

A server-side constant `TermsVersion.Current = "2026-07-15"` (static class in the Authentication BC, e.g. `Domain/Constants/TermsVersion.cs`). Rationale: the ToS text lives in code (`it.json`/`en.json`) and changes via deploy, so its version **co-deploys atomically** with the text.

The frontend `terms/page.tsx` keeps its display literal (`lastUpdated`), annotated with a **cross-reference comment** to the BE constant. No public version endpoint / SSR fetch on a static page (YAGNI); drift risk is low (both change in the same ToS-edit commit). The future gate reads the current version from `GET /terms/status` (§6.3), not from a separate endpoint.

## 6. API surface (CQRS — endpoints use `IMediator` only)

### 6.1 `POST /api/v1/auth/register` — extended
- Request DTO gains `termsAccepted: bool`; `RegisterCommand` gains `bool TermsAccepted = false`.
- `RegisterCommandValidator`: `RuleFor(x => x.TermsAccepted).Equal(true)` → **server-side enforcement** (422 via the FluentValidation pipeline, matching the existing validator's behavior) — closes the cosmetic-checkbox gap.
- `RegisterCommandHandler`: after the user+session `AddAsync`, also `_termsAcceptanceRepository.AddAsync(TermsAcceptance.Create(userId, TermsVersion.Current, Registration, command.IpAddress, command.UserAgent))` — committed in the **same** existing `_unitOfWork.SaveChangesAsync` (FK to the just-added user satisfied within one transaction). `AcceptedAt` from the handler's existing `TimeProvider`.

### 6.2 `POST /api/v1/users/me/terms/accept` — authenticated
- `me`-scoped: userId from `session.Principal.Subject.Id` (never a route/body param) → no IDOR.
- `RecordTermsAcceptanceCommand(userId, context: ReConsent)` records acceptance of `TermsVersion.Current`.
- **Idempotent:** if the user's latest accepted version already equals `Current`, no new row is written (returns 200). This keeps the append-only log meaningful (only real version transitions recorded) while safe to call repeatedly. This endpoint is the integration point a *future* gate/modal calls.

### 6.3 `GET /api/v1/users/me/terms/status` — authenticated
- `me`-scoped read-model. Returns:
  ```
  { currentVersion: string,
    acceptedVersion: string | null,
    acceptedAt: string | null,
    needsReAcceptance: boolean }
  ```
- `needsReAcceptance = acceptedVersion != currentVersion` (null accepted → true). This is the flag a future gate consumes; **nothing consumes it in this scope**.

Endpoints registered via a `MapTermsConsentEndpoints` `RouteGroupBuilder` extension (mirrors `UserAiConsentEndpoints`), `.RequireSession().RequireAuthorization()`.

## 7. CQRS / DDD structure

- `TermsAcceptance` entity (Domain) + `ITermsAcceptanceRepository` (Domain) + impl (Infrastructure). **DI registers both interface and impl** (#2565).
- `RecordTermsAcceptanceCommand` (+ validator + handler) — used by 6.2 and internally callable shape for registration (registration handler uses the repository directly, same BC/transaction).
- `GetTermsConsentStatusQuery` (+ handler) → status DTO.
- Typed exceptions only (never `InvalidOperationException`); domain events not needed here.

## 8. Frontend register-pipeline fix

- `authClient.ts`: `RegisterRequest` gains `termsAccepted: boolean`; `register()` posts it.
- `register/_content.tsx`: forward the acceptance through to `register({ email, password, termsAccepted: true })` (the form already gates submit on the required checkbox; wire the boolean, not the client timestamp — server stamps the authoritative time).
- `RegisterForm` submit payload already carries the acceptance moment; the consumer now forwards a `termsAccepted: true` boolean.
- Update consumer tests (register flow) — run the component's own tests, not just the edited module (per prior lesson).

## 9. Security

- **Server-side terms enforcement** at registration (validator) — the checkbox is no longer bypassable via a direct API call.
- **Server-stamped `AcceptedAt`** — acceptance time is authoritative, not client-supplied.
- **`me`-scoped** accept/status endpoints — userId derived from the session; add a cross-tenant test asserting the endpoint ignores any attempt to act on another user (no route/body userId exists).
- Append-only record = tamper-evident audit trail (no in-place overwrite).

## 10. Testing strategy (TDD)

**Backend unit:**
- `TermsAcceptance.Create` — happy path + throws on empty userId / blank version.
- `RecordTermsAcceptanceCommandHandler` — appends a row; **no-op when latest accepted == Current** (idempotency); records `Context = ReConsent`.
- `GetTermsConsentStatusQueryHandler` — `needsReAcceptance`: no rows → true; stale version → true; current → false; returns latest `acceptedAt`.
- `RegisterCommandValidator` — `TermsAccepted == false` → validation failure.

**Backend integration (Testcontainers Postgres):**
- Registration writes exactly one `TermsAcceptance` row with `Current` version + `Registration` context + user's ip/ua.
- Accept endpoint appends; second call with same current version is a no-op (still one row).
- Status endpoint returns the correct DTO across the three `needsReAcceptance` states.
- Append-only: a re-consent to a *new* version yields a second row (history preserved).
- me-scoped: endpoints operate only on the session user.

**Frontend (vitest):**
- `authClient.register` includes `termsAccepted` in the POST body.
- `register/_content.tsx` forwards `termsAccepted: true`.
- Existing register-form / register-page tests updated and green.

## 11. Files

**Create (BE):**
- `BoundedContexts/Authentication/Domain/Entities/TermsAcceptance.cs`
- `BoundedContexts/Authentication/Domain/Enums/TermsAcceptanceContext.cs`
- `BoundedContexts/Authentication/Domain/Constants/TermsVersion.cs`
- `BoundedContexts/Authentication/Domain/Repositories/ITermsAcceptanceRepository.cs`
- `BoundedContexts/Authentication/Infrastructure/Persistence/TermsAcceptanceRepository.cs`
- `Infrastructure/EntityConfigurations/Authentication/TermsAcceptanceEntityConfiguration.cs`
- `BoundedContexts/Authentication/Application/Commands/TermsAcceptance/RecordTermsAcceptanceCommand.cs` (+ validator + handler)
- `BoundedContexts/Authentication/Application/Queries/TermsAcceptance/GetTermsConsentStatusQuery.cs` (+ handler + DTO)
- `Routing/TermsConsentEndpoints.cs`
- EF migration `AddUserTermsAcceptances`

**Modify (BE):**
- `RegisterCommand.cs` (+`TermsAccepted`), `RegisterCommandValidator.cs`, `RegisterCommandHandler.cs`
- `/auth/register` endpoint request DTO (+`termsAccepted`)
- DI registration (repo interface+impl); endpoint group wiring
- `MeepleAiDbContext` `DbSet<TermsAcceptance>` if the DbContext enumerates sets explicitly

**Modify (FE):**
- `lib/api/clients/authClient.ts`, `app/(auth)/register/_content.tsx` (+ tests)
- `app/(public)/terms/page.tsx` (cross-ref comment on `lastUpdated`)

## 12. Non-goals / deferred to legal reviewer

The **legal determination** of whether re-consent is *required* stays with the professional reviewer. Open questions handed off (from the decision dossier): GDPR Art.6 basis (consent vs. contractual necessity); sufficiency of §10 "continued use = acceptance" for a material change; enforceability of §10 as a unilateral-modification clause (art. 1341-1342 c.c.); retroactive-authorization exposure (cards already live); whether an append-only audit trail is legally required; the decline-path for a future gate; and whether ToS re-consent should be bundled with or kept separate from the §6 AI-consent. This foundation keeps **all** downstream enforcement options open.

---

*Design approved by user 2026-07-19. Enforcement strength intentionally deferred; this spec delivers only the recording/versioning foundation.*
