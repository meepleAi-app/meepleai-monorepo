# Authentication Flow Security Fixes — Specification

**Data**: 2026-05-06
**Autore**: Claude (sc:spec-panel review)
**Scope**: Risolvere 22 issue identificati nella security review del bounded context Authentication: 7 critici (security bugs), 9 importanti (production hardening), 6 raccomandazioni (code quality).
**Status**: APPROVED + REVIEWED (3 expert agents 2026-05-06)
**Branch target**: `main` (hotfix per critici di sicurezza) — caveat: security reviewer raccomanda `main-dev` con cherry-pick selettivo, utente ha confermato `main` direct
**Review history**:
- v1.0 — initial spec (utente approval 2026-05-06)
- v1.1 — incorporates findings da security/architecture/quality review (3 agents)
- Verified: middleware path filter bug ipotizzato in review → NOT REAL (`Program.cs:668-680` mounta `/api/v1` group, auth è sotto `/api/v1/auth/*`, middleware `/api/*` filter intercetta correttamente)
**Trigger**: `/sc:spec-panel "analiza il flow di auth ed eventuali bug o problemi"` review eseguita 2026-05-06 — risultati: score 6.5/10, 7 bug critici block-release.

---

## 1. Context

### 1.1 Review summary

Multi-expert panel review (Wiegers, Fowler, Nygard, Crispin, Newman) sul bounded context `Authentication` (272 file C#, 80 test file, 48 commands, 24 queries) ha identificato:

| Severity | Count | Esempi |
|---|---|---|
| 🔴 CRITICAL | 7 | Hash mismatch endpoint, NRE su pending users, fake password hash OAuth |
| 🟡 IMPORTANT | 9 | Token in URL, expires mismatch, CQRS inconsistency |
| 🟢 RECOMMENDED | 6 | Implicit operators, TimeProvider drift, displayName privacy |

Score complessivo: **6.5/10** — architettura DDD/CQRS solida, ma **non production-ready** senza fix critici.

### 1.2 Bugs critici identificati

| ID | File | Issue |
|---|---|---|
| **C1** | `AuthenticationEndpoints.cs:268,456` | Hash session calcolato in modo diverso da storage → endpoint `/auth/session/extend` e `/auth/sessions/revoke-all` silently broken |
| **C2** | `LoginCommandHandler.cs:70-72` | Login controlla solo `IsSuspended`, non `Status==Active`. Pending users (PasswordHash null) → NRE → 500 → user enumeration leak |
| **C3** | `HandleOAuthCallbackCommandHandler.cs:586-594` | `GenerateRandomPasswordHash()` ritorna 32 random base64 bytes, NON un PBKDF2 hash. Bypassa anche User aggregate (usa UserEntity diretto). |
| **C4** | `CookieHelpers.cs:104,119` | Cookie `meepleai_user_role` plaintext senza HMAC → potenziale privilege escalation se Next.js middleware si fida del valore |
| **C5** | `RegisterCommandHandler.cs:53-66` | TOCTOU race su email duplicate check + first-user-is-admin logic |
| **C6** | `LoginCommandHandler.cs:68` + `TwoFactorEndpoints.cs:114` | `RecordSuccessfulLogin()` chiamato PRIMA di verifica 2FA → 2FA brute-force via re-login bypassa rate limit |
| **C7** | `ChangePasswordCommandHandler.cs:48-52` | Password change non revoca sessioni esistenti → attacker mantiene accesso post-compromise |

### 1.3 Issue importanti identificati

| ID | File | Issue |
|---|---|---|
| **I1** | `AuthenticationEndpoints.cs:661-686` | `GET /auth/validate-invitation?token=X` → token in access log, browser history, Referer |
| **I2** | `LoginCommandHandler.cs` + endpoints | Cookie `ExpiresAt` (config) vs DB `Session.ExpiresAt` (default) potenzialmente divergenti |
| **I3** | `AuthenticationEndpoints.cs:198,222,309` | 3 pattern auth coesistono (`TryGetActiveSession`, `RequireAuthorization`, claims). CLAUDE.md dichiara CQRS come 🔴 CRITICAL |
| **I4** | `TwoFactorEndpoints.cs:235-285` | Admin disable 2FA non richiede password admin → admin session compromise → 2FA bypass massivo |
| **I5** | `RegisterCommandHandler.cs:117-131` | Email verification fire-and-forget senza outbox → email perse silently |
| **I6** | `HandleOAuthCallbackCommandHandler.cs:200-220` | Manual rollback InMemory incompleto: non rolling back email verification flag, generic exception path |
| **I7** | `PasswordHash.cs:19,42` | 8 char min (NIST 2024: 12+), 210k iterations PBKDF2 (OWASP 2023: 600k) |
| **I8** | `HandleOAuthCallbackCommandHandler.cs:463-476` | OAuth callback usa `UserEntity` direct invece di `User` aggregate → no domain events, validation bypass |
| **I9** | `CookieHelpers.cs:31-43` | Dev workaround manual Set-Cookie senza URL-encode token |

### 1.4 Issues aggiunti dalla review v1.1 (3 esperti agents)

| ID | File | Issue | Reviewer source |
|---|---|---|---|
| **C8** | TUTTI endpoint state-changing auth | **CSRF protection completamente mancante** su `/auth/change-password`, `/auth/sessions/revoke-all`, `/auth/2fa/disable`, `/auth/oauth/{provider}/unlink`. Cookie auth + no CSRF token → vulnerabile a classic CSRF. SameSite=Lax non sufficiente per form POST cross-origin. | Security |
| **I10** | New: `AuditLog` table | **Audit log per security events mancante** — GDPR Art.32 + SOC2 require persisted audit per: lockout, password change, OAuth link/unlink, role assignment, bootstrap admin creation, admin disable 2FA. Logging non strutturato non è auditabile. | Security |
| **I11** | `OAuthEndpoints.cs`, `PasswordResetEndpoints.cs`, `TwoFactorEndpoints.cs`, `AuthenticationEndpoints.cs:661` | **Rate limit IP-based non uniforme** su tutti endpoint auth. Oggi solo register/login/oauth. Mancano: forgot-password, verify-2fa, validate-invitation, accept-invitation. Vector per enumeration/brute-force/DoS. | Security |

### 1.5 Recommended fixes

| ID | File | Issue |
|---|---|---|
| **R1** | `SessionToken.cs:98-102`, `PasswordHash.cs:75-79` | `implicit operator string` permette leak via cast esplicito che bypassa `[REDACTED]` ToString |
| **R2** | `User.cs:113` (UtcNow) vs `:173` (TimeProvider) | Mix DateTime.UtcNow/_timeProvider rende test deterministici impossibili |
| **R3** | `AuthenticationEndpoints.cs:95-97` | DisplayName fallback `email.Split('@')[0]` espone PII (cognome utente) |
| **R4** | `AuthenticationEndpoints.cs:347-394` | `/auth/sessions/{id}/extend` aggiorna cookie corrente anche se sessionId è di altra session |
| **R5** | `AuthenticationEndpoints.cs:178-194` | Logout senza cookie ritorna `{ ok: true }` (silent success) invece di 401 |
| **R6** | `TempSessionService.cs:109-125` | `CleanupExpiredSessionsAsync` esiste ma non è chiamato da nessun BackgroundService |

---

## 2. Decision

### 2.1 Scope e strategia

**Scope deciso** (utente 2026-05-06): tutti i 22 issue iniziali + 3 issue aggiunti da review (C8, I10, I11) = **25 issue totali**.

**Strategia decomposizione**: 1 PR monolitico con 25 commit issue-by-issue (revertable granulare).

**Branch target**: `main` (hotfix per natura security-critical dei C-issues).

**Branch name**: `hotfix/auth-flow-security-fixes-2026-05-06`.

**Backwards compatibility**: big bang, no data migration. Schema migration EF accettate (campi nullable). Legacy data (es. fake OAuth hash) gestita on-the-fly via null-safe checks.

**Test strategy**:
- TDD obbligatorio per C1-C7 (red test first → fix → green)
- Post-fix tests per I1-I9 + R1-R6
- Coverage target: ≥90% per file modificati

### 2.2 Principi guida architetturali

1. **DDD aggregate integrity**: tutti i path passano da `User` aggregate. No `UserEntity` direct. Risolve C3 + I8.
2. **Single source of truth per hash session**: centralizzato in `SessionToken.ComputeHash()`. Risolve C1.
3. **Domain methods invece di property setters**: lockout 2FA esposto come `RecordFailed2FA()` / `Reset2FACounter()`.
4. **Event-driven side effects**: `PasswordChangedEvent` → handler revokes sessions. Risolve C7.
5. **Defensive validation everywhere**: pattern OAuth handler diffuso a tutti gli handlers.

---

## 3. Implementation Sequencing

22 commit ordinati per minimizzare conflitti e dipendenze.

### Phase A — Foundation (commit 1-2)

**Goal**: pulire fondamenta prima dei fix critici.

```
#01 [C1-prep] Centralize SessionToken hash via static helper
    Creates SessionTokenHasher.HashFromCookie(string) utility
    No behavior change — only refactor di TempSessionService.HashToken e SessionToken.ComputeHash
    Prepara terreno per #03 (C1 fix)

#02 [I3] Standardize CQRS pattern: /auth/me, /auth/session/status
    Refactor inline endpoints to use Queries:
    - /auth/me → GetCurrentUserQuery (new)
    - /auth/session/status → GetSessionStatusQuery (existing, reuse)
    - Eliminate duplicated TryGetActiveSession+inline logic
```

### Phase B — Critical Security Fixes (commit 3-10) ⚠️ TDD

**Goal**: 8 bug critici (7 originali + C8 CSRF) risolti con test red→green prima del fix.

```
#03 [C1] Fix hash mismatch in /auth/session/extend, /auth/sessions/revoke-all
#04 [C2] Login: use CanAuthenticate() instead of IsSuspended check + null-safe VerifyPassword
#05 [C3+I8] OAuth: nullable PasswordHash + User.CreateForOAuth() factory
#06 [C4] Cookie role HMAC con versioning lazy migration (v2 + v1 grace period 7gg)
#07 [C5] Race condition: catch unique violation + bootstrap admin token (constant-time + audit + single-use)
#08 [C6] 2FA brute-force lockout (FailedTwoFactorAttempts + TwoFactorLockedUntil) + IP-based rate limit
#09 [C7] Password change → revoke other sessions + opt-in "include current" flag (compromise scenario)
#10 [C8] CSRF protection: anti-forgery middleware su endpoint state-changing auth
```

### Phase C — Important Hardening (commit 11-21)

```
#11 [I1] Remove GET /auth/validate-invitation (POST only)
#12 [I2] Session ExpiresAt: handler returns canonical value, endpoint uses it
#13 [I4] Admin disable 2FA requires admin password verification
#14 [I5] Email verification → outbox pattern in UserNotifications BC (cross-BC event)
#15 [I6] OAuth callback: simplify InMemory rollback via try/finally compensation (no class)
#16 [I7] Password policy: 12+ chars, 600k PBKDF2 iter, top-1000 blocklist + max length 128
#17 [I8 cleanup] Verify all OAuth paths use User aggregate
#18 [I9] Cookie dev workaround: URL-encode token + warning log
#19 [I10] AuditLog entity in new SecurityAudit BC + log security events
#20 [I11] Rate limit IP uniforme su forgot-password, verify-2fa, validate-invitation, accept-invitation
#21 [I3] CQRS standardization (moved to end of Phase C — più sicuro dopo critici stabili)
```

### Phase D — Code Quality (commit 22-27)

```
#22 [R1] Remove implicit string operators on SessionToken/PasswordHash
#23 [R2] TimeProvider injection consistency in User aggregate
#24 [R3] DisplayName default: Player-{ShortGuid} instead of email prefix
#25 [R4] /auth/sessions/{id}/extend: validate sessionId == current
#26 [R5] Logout endpoint: 401 if no session
#27 [R6] CleanupExpiredTempSessionsService BackgroundService (+ EmailOutboxService da #14)
```

### Phase A revisita — Foundation (commit 1-2)

```
#01 [C1-prep + Schema] Centralize SessionToken hash via static helper
    Creates SessionTokenHasher.HashFromCookie(string) utility
    EF Migration consolidata: PasswordHash nullable + 2FA lockout fields + AuditLog table + EmailOutbox table
    No behavior change a runtime — solo refactor + schema prep

#02 [Test infra] Setup Testcontainers + FakeTimeProvider standard
    BaseTestContext class per test transazionali (C5, C7, I5, I6)
    FakeTimeProvider DI standard
    Performance benchmark baseline (BenchmarkDotNet su PasswordHash.Create/Verify)
```

### Dipendenze esplicite (aggiornate)

| Issue | Dipende da | Perché |
|---|---|---|
| **#01 (foundation)** | — | Schema prep + utility, no behavior change |
| **#02 (test infra)** | #01 | Test infra dipende da schema |
| **#05 (C3+I8)** | #01 | Richiede `PasswordHash` nullable migration |
| **#08 (C6)** | #01 | Richiede 2FA lockout columns |
| **#09 (C7)** | #03 (C1) | Usa session repository con hash corretto |
| **#19 (I10 audit)** | #01 | Richiede `AuditLog` table |
| **#21 (I3 CQRS)** | tutti i critici | Refactor sicuro solo dopo logica stabilizzata |
| **#22 (R1 implicit)** | #03 (C1) | Rimozione operator dopo verifica call sites |

---

## 4. Per-Issue Implementation Approach

### Group A — Session Token Hash & Lifecycle (C1, C7, R4)

**C1 — Hash mismatch fix**:

```csharp
// AuthenticationEndpoints.cs — replace inline hash with utility
- var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
- var tokenHash = Convert.ToBase64String(hash);
+ string tokenHash;
+ try {
+     tokenHash = SessionToken.FromStored(token).ComputeHash();
+ } catch (ValidationException) {
+     return Results.Unauthorized();  // malformed cookie
+ }
```

**C7 — Password change session revoke**:

- `ChangePasswordCommandHandler` chiama `_sessionRepository.RevokeAllByUserIdAsync(userId, exceptSessionId: currentSession)` dopo password update
- Endpoint passa `currentSessionId` dal middleware-populated `SessionStatusDto`
- `PasswordChangedEvent` (già in Domain Events) → nuovo `PasswordChangedSessionRevocationHandler` per separation of concerns

**R4 — Extend session match check**:

- Endpoint compara `sessionId` parametro con `currentSession.Id` da `TryGetActiveSession()`
- Solo se match: aggiorna cookie corrente. Altrimenti: solo conferma JSON.

### Group B — Login & Lockout (C2, C6)

**C2 — Status check + null PasswordHash**:

```csharp
// LoginCommandHandler.cs
- if (user.IsSuspended)
-     throw new DomainException("Account is suspended");
+ if (!user.CanAuthenticate())
+     throw new DomainException("Account is not available");

// User.cs - VerifyPassword null-safe
public bool VerifyPassword(string plaintextPassword)
{
+    if (PasswordHash == null) return false;  // OAuth-only users
     return PasswordHash.Verify(plaintextPassword);
}
```

**C6 — 2FA brute-force lockout** (DoS-aware, da review v1.1):

Threat model security review identificato: lockout by `userId` permette **targeted DoS** (attacker con email vittima locka account ogni 15min).

Mitigazione doppio approccio:
- **Lockout temp session, non utente**: invalidare la temp session su 5 failure → user può fare nuovo login (ma rallenta brute force).
  - `User.cs`: NO nuovi campi (no `TwoFactorLockedUntil`).
  - `TempSessionEntity`: aggiungere `FailedAttemptCount`. Se > 5 → invalidate temp session.
- **Rate limit IP-based forte**: 10 attempts/15min per IP su `/auth/2fa/verify` (non solo session token come oggi).
- **Login resetta counter solo POST verifica 2FA**: `LoginCommandHandler` sposta `RecordSuccessfulLogin()` DOPO il 2FA verify success, non prima.
- **No lockout permanente account** → no DoS vector.

Edge case: se attacker conosce email + password vittima e tenta 2FA brute-force, IP rate limit blocca. Vittima può fare login normalmente da altro IP.

EF migration: `Add-Migration AddTempSessionFailedAttempts` — campo nullable con default 0.

### Group C — OAuth Integrity (C3, I6, I8)

**C3 + I8 — Single commit**:

```csharp
// User.cs — new factory method
public static User CreateForOAuth(
    Guid id, Email email, string displayName, Role role,
    UserTier? tier, TimeProvider timeProvider)
{
    var user = new User { Id = id, ... };
    user.PasswordHash = null;    // OAuth-only marker (nullable now)
    user.EmailVerified = true;   // OAuth provider verified
    user.EmailVerifiedAt = timeProvider.GetUtcNow().UtcDateTime;
    user.AddDomainEvent(new UserCreatedViaOAuthEvent(id, email.Value));
    return user;
}

// HandleOAuthCallbackCommandHandler.cs — replace UserEntity construction
- user = new UserEntity { ..., PasswordHash = GenerateRandomPasswordHash(), ... };
+ var domainUser = User.CreateForOAuth(Guid.NewGuid(), new Email(userInfo.Email),
+     userInfo.Name ?? emailPrefix, Role.User, null, _timeProvider);
+ await _userRepository.AddAsync(domainUser, ct);

// User.cs — make PasswordHash nullable
- public PasswordHash PasswordHash { get; private set; }
+ public PasswordHash? PasswordHash { get; private set; }
```

EF migration richiesta: `PasswordHash` da `NOT NULL` → `NULLABLE`. Legacy fake hashes restano in DB (ignorabili — `VerifyPassword` null-safe gestisce).

**I6 — InMemory rollback complete**:

- Estrarre rollback in `OAuthCallbackRollbackContext` class che cattura snapshot stato pre-modifica
- Su failure: ripristina email verification flags, rimuove account creato, rimuove user nuovo
- Test esistenti continuano a passare

### Group D — Cookie Security (C4, I9, R3)

**C4 — HMAC role cookie con VERSIONING + LAZY MIGRATION** (updated da review v1.1):

Strategia: NON invalidare big-bang i cookie esistenti. Coesistenza v1 (plaintext, deprecato) + v2 (HMAC).

```csharp
// CookieHelpers.cs
private const string UserRoleCookieNameV1 = "meepleai_user_role";       // legacy plaintext
private const string UserRoleCookieNameV2 = "meepleai_user_role_v2";    // HMAC protected

public static void WriteUserRoleCookie(HttpContext context, string role, DateTime expiresAt)
{
    var protector = context.RequestServices
        .GetRequiredService<IDataProtectionProvider>()
        .CreateProtector("MeepleAi.UserRoleCookie.v2");
    var protectedRole = protector.Protect(role.ToLowerInvariant());

    // Write v2 (new clients)
    context.Response.Cookies.Append(UserRoleCookieNameV2, protectedRole, options);
    // Delete v1 (cleanup legacy)
    context.Response.Cookies.Delete(UserRoleCookieNameV1, options);
}

public static string? ReadUserRoleCookie(HttpContext context) {
    // Try v2 first
    if (context.Request.Cookies.TryGetValue(UserRoleCookieNameV2, out var v2Value)) {
        try { return protector.Unprotect(v2Value); }
        catch (CryptographicException) { return null; }
    }
    // Fallback v1 (grace period 7gg post-deploy, hardcoded sunset date)
    if (DateTime.UtcNow < new DateTime(2026, 5, 13) &&
        context.Request.Cookies.TryGetValue(UserRoleCookieNameV1, out var v1Value)) {
        return v1Value.ToLowerInvariant();
    }
    return null;
}
```

**Key rotation**: configurare `services.AddDataProtection().PersistKeysToStackExchangeRedis(...)` per multi-instance. Verificare se già configurato in `Program.cs` o aggiungere.

**Trade-off**: per 7gg, attacker può spoof v1 cookie. Ma session cookie (la vera auth) resta HttpOnly+SameSite=Lax → impatto limitato. Sunset date hardcoded forza cleanup.

**I9 — Cookie dev workaround**:

- Aggiungere `Uri.EscapeDataString(token)` per il valore manuale
- Log warning chiaro che questo code path è solo dev
- Considerare alternativa: configurazione Kestrel HTTPS dev cert

**R3 — DisplayName default**:

- `AuthenticationEndpoints.cs:95-97`: cambia fallback da `email.Split('@')[0]` a `$"Player-{Guid.NewGuid():N}".Substring(0, 14)`
- Validator richiede displayName non null/empty (no fallback su frontend)

### Group E — Registration & Race Conditions (C5, R5)

**C5 — Race + bootstrap admin**:

```csharp
// RegisterCommandHandler.cs
private Role DetermineRole(string? bootstrapToken)
{
    var configuredToken = _config["Authentication:BootstrapAdminToken"];
    if (!string.IsNullOrEmpty(configuredToken) && bootstrapToken == configuredToken)
    {
        return Role.Admin;  // first admin via secret token (env-managed)
    }
    return Role.User;  // default for everyone else
}

// Wrap save in try-catch for unique constraint
try {
    await _unitOfWork.SaveChangesAsync(ct);
} catch (DbUpdateException ex) when (IsUniqueViolation(ex, "Email")) {
    throw new ConflictException("Email is already registered");  // 409
}
```

**Configurazione bootstrap admin** (hardened da review v1.1):
- Env variable: `AUTHENTICATION__BOOTSTRAPADMINTOKEN` (ASP.NET config convention)
- Secret file: `infra/secrets/bootstrap_admin_token.secret` (con `.example` template)
- Token usage: include in registration payload come `bootstrapToken: "..."` field opzionale
- **Constant-time comparison** (security review):
  ```csharp
  var configBytes = Encoding.UTF8.GetBytes(configuredToken);
  var inputBytes = Encoding.UTF8.GetBytes(bootstrapToken);
  if (configBytes.Length != inputBytes.Length) return Role.User;
  if (!CryptographicOperations.FixedTimeEquals(configBytes, inputBytes)) return Role.User;
  return Role.Admin;
  ```
- **Single-use enforcement**: aggiungere flag `BootstrapAdminCreated` su tabella `SystemConfiguration`. Se true → ignorare token anche se valido. Set true atomicamente nello stesso commit della creazione admin.
- **Audit log obbligatorio** (richiede I10): scrivi `AuditLog` entry con eventType=`BootstrapAdminCreated`, ipAddress, userAgent, timestamp.
- **Operatore best practice**: dopo bootstrap, rimuovere env var (defense in depth)

Rimuoviamo logica `HasAnyUsersAsync()` da `RegisterCommandHandler`.

`RegisterCommand` deve esporre un nuovo field `BootstrapToken: string?`. `RegisterPayload` (DTO endpoint) idem.

**R5 — Logout 401**:

- `AuthenticationEndpoints.cs:178`: se cookie assente → `Results.Unauthorized()` invece di `{ ok: true }`

### Group F — CQRS Standardization (I3)

- `/auth/me`: usa `GetCurrentUserQuery(sessionId)` che ritorna user dto
- `/auth/session/status`: usa `GetSessionStatusQuery` (già esiste, riusare)
- Eliminare logiche inline in endpoints

### Group G — Other Important (I1, I2, I4, I5, I7)

**I1 — Token in URL**: rimuovere `MapValidateInvitationGetEndpoint`. Verificare frontend usa POST.

**I2 — ExpiresAt canonical**:
- `LoginResponse.ExpiresAt` ritornato dal handler
- Endpoint usa `result.ExpiresAt` invece di calcolare da config

**I4 — Admin re-auth**:
- `AdminDisable2FACommand`: aggiungere `AdminPassword` field
- Handler verifica `adminUser.VerifyPassword(command.AdminPassword)` prima di disabilitare

**I5 — Outbox pattern email in UserNotifications BC** (architettura corretta da review v1.1):

Il pattern outbox NON appartiene ad Authentication BC. Email verification è una notification → vive in `UserNotifications`.

- Nuovo entity `EmailOutboxEntity` in `BoundedContexts/UserNotifications/Domain/Entities/`
- Schema: `Id, ToEmail, Subject, BodyHtml, IdempotencyKey, ScheduledAt, SentAt, AttemptCount, LastError, CreatedAt`
- **IdempotencyKey** (security review): UUID per idempotent retry — evita double-send se BackgroundService crasha mid-send
- `Authentication.RegisterCommandHandler`: emette `UserRegisteredEvent(userId, email, displayName)`
- `UserNotifications.UserRegisteredEventHandler`: aggiunge `EmailOutboxEntity` per verification email
- Nuovo `EmailOutboxBackgroundService`: poll ogni 30s, send pending, retry exponential backoff (1m, 5m, 30m, 2h)
- Max retry: 5 attempts → mark as `FailedPermanent` per ops review
- Cleanup vecchie entries `Sent` o `FailedPermanent` dopo 30 giorni

**I7 — Password policy**:
- `PasswordHash.cs:42`: min 12 chars (was 8)
  - ⚠️ Breaking per UI: aggiornare i seguenti form frontend (validation + helper text):
    - `apps/web/src/app/(auth)/register/page.tsx`
    - `apps/web/src/app/(auth)/setup-account/page.tsx`
    - `apps/web/src/app/(auth)/reset-password/page.tsx`
    - Form change-password in user profile (verify path)
- `PasswordHash.cs:19`: `Iterations` const → 600_000 (was 210_000)
  - Applicato solo ai NUOVI hash, vecchi hash 210k continuano a funzionare via `VerifyVersionedHash` che legge iterations da hash stored
  - Long-term: rehash on next successful login se hash usa vecchio iter count (out of scope, future work)
- Top-1000 password blocklist:
  - File: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Resources/CommonPasswords.txt` (gitignored se troppo grosso, embedded resource preferred)
  - Source: SecLists `10-million-password-list-top-1000.txt`
  - Check in `PasswordHash.Create()` con `HashSet<string>` per O(1) lookup

### Group I — Security Hardening (C8, I10, I11) — NEW v1.1

**C8 — CSRF protection su endpoint state-changing**:

ASP.NET Core ha `IAntiforgery` nativo ma richiede setup esplicito. Approach:
- `Program.cs`: `services.AddAntiforgery(opt => { opt.Cookie.Name = "X-XSRF-TOKEN"; opt.HeaderName = "X-XSRF-TOKEN"; });`
- Endpoint protetti (filter): `/auth/change-password`, `/auth/sessions/revoke-all`, `/auth/2fa/disable`, `/auth/oauth/{provider}/unlink`, `/auth/logout-all-devices`
- Pattern: `.AddEndpointFilter<AntiforgeryEndpointFilter>()` extension custom
- Frontend (`apps/web`): inietta token da cookie `X-XSRF-TOKEN` in header su ogni POST/PUT/DELETE
- GET `/auth/csrf-token` per primo fetch del token

**I10 — Audit log table per security events** in nuovo BC `SecurityAudit`:

```csharp
// New BC: BoundedContexts/SecurityAudit/Domain/Entities/AuditLogEntity.cs
public class AuditLogEntity {
    public Guid Id { get; init; }
    public Guid? ActorUserId { get; init; }       // chi ha fatto azione (null per system)
    public Guid? TargetUserId { get; init; }      // su chi (null per global event)
    public string EventType { get; init; }        // enum constant: "LoginFailure", "AccountLocked", etc.
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public DateTime Timestamp { get; init; }
    public string? Metadata { get; init; }        // JSON for event-specific data
    public string? CorrelationId { get; init; }   // tracing distribuito
}
```

Eventi mandatory:
- `LoginFailure` (email, ip, ua)
- `LoginSuccess` (userId)
- `AccountLocked` (userId, attemptCount)
- `PasswordChanged` (userId)
- `PasswordReset` (userId)
- `OAuthLinked` / `OAuthUnlinked` (userId, provider)
- `RoleChanged` (actorAdminId, targetUserId, oldRole, newRole)
- `BootstrapAdminCreated` (userId, ip) — required by C5 hardening
- `Admin2FADisabled` (adminId, targetUserId)
- `BulkSessionRevoke` (userId, count)

Pattern: `IAuditLogger` interface, async `LogAsync(AuditEvent event)` chiamato dagli handler. Background flush per performance (queue + batch insert).

Retention: 7 anni per compliance GDPR/SOC2 (configurable).

**I11 — Rate limit IP uniforme su tutti auth endpoint**:

`Program.cs` configurazione rate limiting policies aggiuntive:

```csharp
// Existing: AuthRegister (5/min), AuthLogin (10/min)
// New:
services.AddRateLimiter(options => {
    options.AddPolicy("AuthForgotPassword", ...);   // 3/hour per IP
    options.AddPolicy("AuthVerify2FA", ...);         // 10/15min per IP (oltre session token)
    options.AddPolicy("AuthValidateInvitation", ...); // 20/min per IP
    options.AddPolicy("AuthAcceptInvitation", ...);   // 5/hour per IP
    options.AddPolicy("OAuthCallback", ...);          // 30/min per IP (già fatto via service)
});
```

Endpoint da proteggere:
- `POST /auth/password/request-reset` → `AuthForgotPassword`
- `POST /auth/2fa/verify` → `AuthVerify2FA`
- `POST /auth/validate-invitation` → `AuthValidateInvitation`
- `POST /auth/accept-invitation` → `AuthAcceptInvitation`
- `POST /auth/activate-account` → `AuthAcceptInvitation` (riusa policy)

### Group H — Quality & Background (R1, R2, R6)

**R1 — No implicit operators**:
- Rimuovere `implicit operator string(SessionToken)` e `implicit operator string(PasswordHash)`
- Update call sites: usare `.Value` esplicitamente (~10-15 file da toccare)

**R2 — TimeProvider consistency**:
- `User` constructor accetta `TimeProvider` (default `TimeProvider.System`)
- Tutti i `DateTime.UtcNow` interni → `_timeProvider.GetUtcNow().UtcDateTime`
- Update callers (`RegisterCommandHandler`, ecc.)

**R6 — Cleanup background service**:
- Nuovo `CleanupExpiredTempSessionsService : BackgroundService`
- Loop: ogni 1h chiama `_tempSessionService.CleanupExpiredSessionsAsync()`
- Registrato come `IHostedService` in DI

---

## 5. Testing Strategy

### Distribuzione test (updated v1.1)

| Phase | Issues | TDD? | Test count atteso |
|---|---|---|---|
| **B Critical** | C1-C8 | ✅ Mandatory | 35-40 test |
| **C Important** | I1-I11 + I3 | Post-fix | 25-30 test |
| **D Recommended** | R1-R6 | Post-fix | 8-10 test |
| **TOTAL** | 28 issue | — | **~70-80 test nuovi** |

Quality review v1.1 ha aumentato stima da 30 → 70-80 test per coverage adeguata edge cases.

### TDD Workflow per ogni Critical fix

```
1. RED   → Test fallisce riproducendo bug
2. COMMIT → "test(auth): add failing test for Cn"
3. GREEN → Applica fix
4. COMMIT → "fix(auth): <description> (Cn)"
5. REFACTOR → Pulizia se necessaria
```

### Test specifici per C1-C8 (expanded v1.1)

**C1 — Hash Mismatch** (4 test):
- `ExtendSession_WithValidCookie_ShouldExtendExpiration()` ❌ → ✅
- `ExtendSession_HashComputationMatchesStorage()` (regression)
- `ExtendSession_WithMalformedBase64Cookie_ReturnsUnauthorized()` (NEW)
- `ExtendSession_WithEmptyToken_ReturnsUnauthorized()` (NEW)
- `RevokeAllSessions_WithCurrentSession_ShouldExcludeCurrent()` ❌ → ✅

**C2 — Login Pending User** (6 test):
- `Login_PendingUser_ShouldReturnAccountUnavailable_NoNullRefException()`
- `Login_SuspendedUser_ShouldReturnAccountUnavailable()`
- `Login_BannedUser_ShouldReturnAccountUnavailable()`
- `Login_DeletedUser_ShouldReturnAccountUnavailable()` (NEW soft-delete)
- `Login_PendingUser_DoesNotIncrementFailedLoginCounter()` (NEW amplification mitigation)

**C3 — OAuth Password Hash** (5 test):
- `OAuthCallback_NewUser_PasswordHashIsNull()`
- `OAuthCallback_NewUser_DomainEventEmitted()` (UserCreatedViaOAuthEvent)
- `OAuthOnlyUser_LoginWithPassword_ReturnsUnauthorized()` (no NRE)
- `OAuthOnlyUser_ChangePassword_ThrowsConflictException()`
- `LegacyOAuthUser_WithRandomBase64Hash_ChangePassword_RejectsAsCorrupted()` (NEW migration safety)

**C4 — Cookie Role HMAC + Versioning** (5 test):
- `WriteUserRoleCookie_ValueIsProtected_NotPlaintext()`
- `WriteUserRoleCookie_DeletesV1Cookie()` (NEW lazy migration)
- `ReadUserRoleCookie_TamperedV2_ReturnsNull()`
- `ReadUserRoleCookie_ValidV2_ReturnsRole()`
- `ReadUserRoleCookie_FallbackV1WithinGracePeriod_ReturnsRole()` (NEW)
- `ReadUserRoleCookie_ExpiredHmac_ReturnsNull()` (NEW)
- `ReadUserRoleCookie_WrongPurpose_ReturnsNull()` (NEW cross-purpose)

**C5 — Race Conditions** (5 test, Testcontainers):
- `Register_DuplicateEmail_ConcurrentRequests_OneSuccessOneConflict()`
- `Register_DuplicateEmail_5ConcurrentRequests_ExactlyOneSuccess()` (NEW property-based)
- `Register_WithBootstrapAdminToken_AssignsAdminRole()`
- `Register_WithoutBootstrapToken_AssignsUserRole()`
- `Register_WithWrongBootstrapToken_AssignsUserRole_NotAdmin()` (NEW timing-attack)
- `Register_FirstUser_WithoutBootstrapToken_AssignsUserRole_NotAdmin()` (NEW regression)
- `BootstrapAdminToken_ConstantTimeCompare_NoTimingLeak()` (NEW security)
- `BootstrapAdminToken_SingleUse_SecondCallIgnored()` (NEW)

**C6 — 2FA Lockout DoS-Aware** (6 test):
- `Verify2FA_5FailedAttempts_InvalidatesTempSession()` (UPDATED: temp session, not user)
- `Verify2FA_AfterTempSessionInvalidated_RequiresNewLogin()` (UPDATED)
- `Verify2FA_IpRateLimit_BlocksAfter10Attempts()` (NEW)
- `Login_FailedLoginCounterNotResetUntil2FAVerified()` (regression)
- `Verify2FA_FailedAttemptDuringLockout_DoesNotExtendLockout()` (NEW edge case)
- `Verify2FA_BoundaryTiming_ValidCodeAcceptedExactlyAtUnlock()` (NEW)

**C7 — Password Change Session Revoke** (5 test):
- `ChangePassword_RevokesAllOtherSessions_KeepsCurrent()`
- `ChangePassword_WithIncludeCurrentFlag_RevokesAllSessions()` (NEW compromise scenario)
- `ChangePassword_PublishesPasswordChangedEvent()`
- `PasswordChangedEvent_TriggersSessionRevocation_ExceptCurrent()`
- `RevokedSession_ApiCall_Returns401()` (NEW behavior verification)
- `PasswordChangedEvent_HandlerFails_PasswordChangeStillSucceeds()` (NEW eventual consistency)

**C8 — CSRF Protection** (5 test):
- `ChangePassword_WithoutCsrfToken_Returns400()`
- `ChangePassword_WithInvalidCsrfToken_Returns400()`
- `ChangePassword_WithValidCsrfToken_Succeeds()`
- `RevokeAllSessions_WithoutCsrfToken_Returns400()`
- `OAuthUnlink_WithoutCsrfToken_Returns400()`

### Test infrastructure requirements (updated v1.1)

- **Database — Testcontainers per TUTTI i test transazionali** (C5, C7, I5, I6, I10): InMemory ha comportamento non realistico su transazioni multi-aggregate. Regola guideline: "ogni test che esercita unique constraints, transazioni reali, concorrenza, o multi-aggregate save → Testcontainers PostgreSQL".
- Test puramente unit (no DB) restano su mocks.
- **TimeProvider — uniform usage**: `FakeTimeProvider` registrato come default in test suite. Nessun `DateTime.UtcNow` direct nei test.
- **DataProtection**: `EphemeralDataProtectionProvider` per unit, `IDataProtectionBuilder` reale (filesystem temp) per integration test C4 versioning.
- **Mocks**: `IConfigurationService` mock per bootstrap admin token, `IAuditLogger` mock per security events.
- **Test base class**: nuovo `AuthBoundedContextTestBase` con setup standard (Testcontainers, FakeTimeProvider, DataProtection ephemeral).

### Coverage goals (explicit branch coverage v1.1)

- **Branch coverage measurement** via Coverlet (`/p:CollectCoverage=true /p:CoverletOutputFormat=cobertura`)
- **CI gate fail-on-threshold**:
  - Per Critical fix: ≥90% **branch** coverage
  - Per Important fix: ≥85% branch coverage
  - Per Recommended fix: ≥75% branch coverage
  - Globale Authentication BC: ≥90% branch (was line, ora explicit)

### Performance baseline (NEW v1.1)

I7 alza PBKDF2 da 210k → 600k iterations. Login latency aumenta ~3x.

- **Benchmark**: BenchmarkDotNet su `PasswordHash.Create()` e `PasswordHash.Verify()` con 600k iter
- **Baseline metric**: `meeple_auth_login_duration_ms` P50/P95/P99 misurato pre/post fix
- **CI gate**: P95 login < 500ms su staging (max acceptable degradation)
- **Production monitoring**: alert se P95 > 750ms post-deploy

### E2E smoke test (expanded v1.1)

3 spec Playwright (1 originale + 2 nuove dalla quality review):

1. **`apps/web/e2e/auth-flow.spec.ts`**: register → login → enable 2FA → login with 2FA → change password → re-login forced (sessions revoked)
2. **`apps/web/e2e/oauth-flow.spec.ts`** (NEW): OAuth Google callback end-to-end (con mock provider) — copre C3+I8
3. **`apps/web/e2e/2fa-recovery.spec.ts`** (NEW): backup codes + admin disable 2FA con re-auth (I4)
4. **`apps/web/e2e/session-revocation.spec.ts`** (NEW): multi-tab simulation di logout-all-devices + password change

**Smoke test staging automated**: `scripts/smoke-staging-auth.sh` con curl assertions invece di checklist manuale.

### CI validation gates (expanded v1.1)

Prima di merge:
- [ ] `dotnet test --filter "BoundedContext=Authentication"` passes
- [ ] Coverage report **≥90% branch** per file modificati (was line, explicit branch ora)
- [ ] `pnpm test` passes
- [ ] `pnpm test:e2e` passes (4 spec Playwright)
- [ ] `pnpm typecheck` passes
- [ ] `dotnet build /p:TreatWarningsAsErrors=true` passes
- [ ] **Performance baseline**: P95 login < 500ms su staging
- [ ] **Migration backward test**: dedicated CI job `migration-rollback-test` (Up + Down + assertion DB schema state)
- [ ] **Mutation testing** (Stryker.NET) score ≥75% su Authentication BC (NEW)

---

## 6. Risks & Rollback

### 6.1 Top production risks

| # | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| **R-A** | Cookie HMAC invalida cookie role esistenti → all users appear logged-out client-side | High (100% by design) | Medium | Banner "session refresh needed". Session cookie session resta valido. Middleware Next.js auto-fetch /auth/me su cookie role assente |
| **R-B** | EF migration fails in prod (PasswordHash nullable, 2FA lockout fields, email outbox) | Low | High | Test migration su staging via `make staging`. Migration `Up`/`Down` testate |
| **R-C** | 2FA lockout regression — users locked out aggressively | Medium | High | Threshold conservativo (5 attempts, 15 min). Admin-disable-2FA escape hatch |
| **R-D** | Session revoke after password change kicks user out of multiple devices unexpectedly | High (intended) | Low | Email "Password changed - other devices logged out for security" |
| **R-E** | Password policy 12 char min breaks existing UI | Medium | Medium | Frontend update incluso. Existing users con password 8-11 char continuano a funzionare. Solo nuove password subiscono check |
| **R-F** | OAuth users con legacy fake hash break su login attempt with password | Low | Low | `VerifyPassword` null-safe + check formato hash; legacy random base64 fallisce silently → 401 |
| **R-G** | CQRS refactor (I3) introduce regression nei middleware-driven endpoints | Medium | Medium | E2E Playwright cover `/auth/me`. Manual smoke staging |
| **R-H** | Background services memory/connection leak | Low | Medium | `IServiceScopeFactory` pattern, `CancellationToken` handling. Load test staging |

### 6.2 Rollback strategy

**Layered rollback**:

1. **Single commit revert** (per issue specifico):
   ```bash
   git revert <commit-sha>
   git push origin main
   ```

2. **Full PR revert**:
   ```bash
   git revert -m 1 <merge-commit-sha>
   git push origin main
   ```

3. **DB migration rollback**:
   ```bash
   cd apps/api/src/Api
   dotnet ef database update <LastSafeMigration>
   ```
   Migrations forward-compatible (campi nullable). Code revert SENZA DB rollback è sicuro.

4. **Hot patch (worst case)**: branch `hotfix/auth-rollback-<date>` da commit pre-PR, cherry-pick critici.

### 6.3 Deployment plan

```
1. PR review approved
2. Merge to main
3. CI runs: build + tests + integration on Testcontainers
4. Deploy to STAGING (auto via make staging)
5. Smoke test su staging:
   - Register → Login → 2FA enable/verify → Logout
   - OAuth Google flow end-to-end
   - Password change scenario
   - Multi-device session revoke
6. Manual approval for production deploy
7. Production deploy with rolling restart (zero downtime)
8. Post-deploy monitoring 24h:
   - Error rate dashboards
   - Login success rate
   - 2FA verification success rate
   - Background services health
```

### 6.4 Observability additions

```csharp
_logger.LogWarning("2FA lockout triggered for user {UserId}, IP {IpAddress}, attempts {Count}", ...);
_logger.LogInformation("Password change triggered session revocation for user {UserId}, revoked {Count} sessions", ...);
_logger.LogInformation("Email outbox: {SentCount} sent, {FailedCount} retrying, {ScheduledCount} scheduled", ...);
```

Metriche Prometheus (verify se in stack):
- `meeple_auth_2fa_lockouts_total{reason}`
- `meeple_auth_password_change_sessions_revoked_total`
- `meeple_email_outbox_pending_count`

---

## 7. Definition of Done

PR è "complete" quando:

- [ ] Tutti i 22 commit applicati nell'ordine pianificato (Phase A → B → C → D)
- [ ] Tutti i test passano (`dotnet test` + `pnpm test` + `pnpm test:e2e`)
- [ ] Coverage Authentication BC ≥ 90%
- [ ] Smoke test su staging verde
- [ ] PR description checklist (ogni issue un checkbox)
- [ ] Code review almeno 1 approver
- [ ] Migration EF testate forward + backward
- [ ] Documentation aggiornata (CLAUDE.md sezione "Active Freezes" se rilevante)

---

## 8. Open Questions / Future Work

Items NOT in scope ma da considerare in futuro:

- **HaveIBeenPwned k-anonymity API** integration per breach check (I7 nice-to-have, deferred)
- **Migrate test InMemory → Testcontainers** per tutti i test transactional (richiesta da I6 ma scope troppo grosso per questo PR)
- **Session fingerprinting** (IP + UserAgent hash per detection device hijack)
- **Audit log table** per security events (lockout, password change, OAuth link/unlink) — attualmente solo log non strutturato
- **Webhook notification** per security events critici (admin email su lockout massivo)
- **Rate limit by IP** uniforme su tutti gli endpoint auth (oggi solo register/login)
