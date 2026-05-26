# S3 Spike — Cutover Readiness + Existing 2FA Infrastructure Inventory

**Date:** 2026-05-26
**Branch:** `feature/sp5-admin-security-s3-strict-2fa`
**Kickoff:** `audits/2026-05-26-s3-three-amigos-kickoff.md`
**Plan:** `docs/superpowers/plans/2026-05-26-sp5-admin-security-s3-strict-2fa.md`

Output del Task 0 (read-only inventory + risk assessment) — informa T1-T9. Ha rivelato infrastruttura 2FA esistente molto più ricca del previsto: due decisioni del kickoff DRAFT vengono **semplificate** in conseguenza (vedi §6 "Amends al kickoff").

---

## §1 — Inventory dei 4 comandi decorati `[RequireTwoFactor]`

Tutti i 4 comandi target di S3 sono già decorati `[RequireTwoFactor]` ma usano il DEFAULT `MaxAgeMinutes = 30`. Nessuno ha tuning esplicito.

| Comando | File | `[RequireTwoFactor]` | `[AuditableAction]` | Audit name attuale |
|---------|------|-----------------------|---------------------|--------------------|
| `ChangeUserRoleCommand` | `BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs:17` | default (30min) | Level 1 | `UserRoleChange` |
| `DeleteUserCommand` | `BoundedContexts/Administration/Application/Commands/DeleteUserCommand.cs:20` | default (30min) | Level 2 | `UserDelete` |
| `SuspendUserCommand` | `BoundedContexts/Administration/Application/Commands/SuspendUserCommand.cs:16` | default (30min) | Level 1 | `UserBlock` |
| `ImpersonationStartCommand` | `BoundedContexts/Administration/Application/Commands/ImpersonationStartCommand.cs:44` | default (30min) **→ T1: 5min** | Level 2 | `ImpersonationStarted` |

**Azione T1**: aggiorna `ImpersonationStartCommand` a `[RequireTwoFactor(MaxAgeMinutes = 5, Reason = "...")]` per D-S3-7.

**Audit name S3 nuovi (per scenari S3-2/S3-4/S3-5)**:
- `TwoFactorRequired` (block emesso quando strict path rifiuta)
- `TwoFactorStepUp` (step-up esplicito, separato da `TwoFactorVerify` di login)
- `TwoFactorStepUpLockout` (5° fail in 1min → lockout 15min, audit forensic)

Il `TwoFactorVerify` audit row è già emesso da `TotpService.VerifyCodeAsync` (vedi §2) — coesiste con `TwoFactorStepUp` per distinguere login vs step-up.

---

## §2 — Sistema 2FA esistente (più maturo del previsto)

### File mappati
| Componente | Path | Funzione |
|------------|------|----------|
| `ITotpService` / `TotpService` | `apps/api/src/Api/Services/TotpService.cs` | Setup, Enable, Verify, Disable. RFC 6238 TOTP via `OtpNet`. |
| `RequireTwoFactorAttribute` | `BoundedContexts/Authentication/Application/Attributes/` | Attribute MediatR (`MaxAgeMinutes`, `Reason`) |
| `TwoFactorEnforcementBehavior` | `BoundedContexts/Authentication/Application/Behaviors/` | Pipeline behavior — shadow mode attuale, target del flip strict |
| `TotpSecret` | `BoundedContexts/Authentication/Domain/ValueObjects/` | Value object encrypted secret |
| `GenerateTotpSetupCommand` (+ Handler + Validator) | `Commands/TwoFactor/` | CQRS setup |
| `Verify2FACommand`, `Enable2FACommand`, `Disable2FACommand`, `AdminDisable2FACommand` | `Commands/TwoFactor/` | CQRS verbs |
| `Get2FAStatusQuery` | `Queries/` | Read status |
| `TwoFactorEnabledEvent` / `TwoFactorDisabledEvent` (+ handlers) | `Domain/Events/`, `Application/EventHandlers/` | Domain events |
| `UsedTotpCodeEntity` + cleanup task | `Infrastructure/Entities/Authentication/`, `BackgroundTasks/` | Replay-attack guard (60s window) |
| `TwoFactorEndpoints.cs` | `Routing/` | REST API (vedi §3) |

### `TotpService.VerifyCodeAsync(userId, code)` — overview funzionalità già implementate

Letto in `apps/api/src/Api/Services/TotpService.cs:179-213`. **Riutilizzabile direttamente per lo step-up endpoint** — fornisce tutte le primitive necessarie:

| Feature | Implementazione |
|---------|-----------------|
| Rate limit | Redis key `2fa:totp:{userId}`, 5 attempts / 5 min, via `_rateLimitService.CheckRateLimitAsync` |
| Lockout | 15 min dopo 5 fail (Redis sliding window TTL) |
| Replay-attack prevention | `UsedTotpCodes` table (SHA256 hash, 2 min expiry) — SEC-07 |
| Audit row | `TwoFactorVerify` (Success/Failed) emesso via `_auditService.LogAsync` |
| Security alerting | Alert critical a `_alertingService` su 10+ fail consecutivi |
| Constant-time | 5ms minimum delay artificial — SEC-08 (Issue #2621) |
| Metrics | `MeepleAiMetrics.Record2FAVerification("totp", success, userId, isReplayAttack)` |

Il SignalR di S3-4b ("DB-backed rate-limit") era ridondante — vedi §6 amend del kickoff.

### `IsTwoFactorEnabled = true` set in 2 punti

1. `TotpService.EnableTwoFactorAsync` (riga 159) — dopo verifica TOTP del codice iniziale
2. `User.cs:676` — metodo dominio (probabilmente `EnableTwoFactor()`)

Nessun aggiornamento di un'analogo `LastTotpVerifiedAt` — è la nuova colonna che S3 deve aggiungere.

---

## §3 — Endpoint 2FA esistenti

| Endpoint | Handler | Funzione | Riuso S3? |
|----------|---------|----------|-----------|
| `POST /auth/2fa/setup` | `GenerateTotpSetupCommand` | Generate secret + QR + backup codes | NO (è enroll-init) |
| `POST /auth/2fa/enable` | `Enable2FACommand` | Verify code → enable 2FA | NO (è enroll-finalize) |
| `POST /auth/2fa/verify` | `Verify2FACommand` | **Login-time verify → CREATES new session** | NO (login flow, NON step-up) |
| `POST /auth/2fa/disable` | `Disable2FACommand` | Disable 2FA con password + code | NO |
| `GET /users/me/2fa/status` | `Get2FAStatusQuery` | Lettura status | NO |
| `POST /auth/admin/2fa/disable` | `AdminDisable2FACommand` | Admin override per locked-out users | NO |

**Lo step-up endpoint (T5) è NEW** — opera su sessione ESISTENTE, NON crea nuova session, aggiorna `LastTotpVerifiedAt`. Path proposto: `POST /api/v1/auth/2fa/step-up { code }`.

**Implementation hint**: il handler di `StepUpTwoFactorCommand` può semplicemente:
1. Resolvere current session da `HttpContext.Items["SessionStatusDto"]`
2. Determinare l'ACTOR (vedi §5 impersonate semantics): `session.IsImpersonation ? session.ImpersonatedByUserId : session.UserId`
3. Chiamare `_totpService.VerifyCodeAsync(actorId, code, ct)` — riusa rate-limit, replay-guard, audit `TwoFactorVerify`, metrics
4. Se `true`: aggiornare `session.LastTotpVerifiedAt = _timeProvider.GetUtcNow()` via `ISessionRepository`, emit audit `TwoFactorStepUp` (action specifico per scope step-up)
5. Return `SessionStatusResponse` (riusa wire S2)

Nessun rate-limit counter custom necessario — `TotpService.ValidateTotpRateLimitAndLockoutAsync` già copre il caso.

---

## §4 — Admin senza 2FA (pre-cutover sweep)

### Query SQL proposta
```sql
SELECT id, email, role, created_at, is_two_factor_enabled
FROM users
WHERE role IN ('admin', 'superadmin')
  AND is_two_factor_enabled = false
  AND is_deleted = false
ORDER BY created_at ASC;
```

### Endpoint `GET /admin/users/no-2fa` (T6)

Wrapper CQRS via `GetAdminsWithoutTwoFactorQuery` + handler. Superadmin-only. Output = `IReadOnlyList<AdminWithoutTwoFactorDto>` (id, email, role, createdAt, lastLogin?).

### Ops sequence pre-flip prod (D-S3-5)
1. Chiamata `GET /admin/users/no-2fa` → conta + lista nominativa
2. Email/Slack diretto agli admin: "abilita 2FA entro T per evitare lockout"
3. Re-conta finché count == 0
4. Flip `TwoFactorStrictMode = true` in staging, dogfooding 24h
5. Flip in prod
6. Monitor: `MeepleAiMetrics.Record2FAVerification` + audit `TwoFactorRequired` (block count) — picco atteso a 0 post-sweep; ogni `TwoFactorRequired` row post-flip = bug ops (admin nuovo senza 2FA) o tentativo malizioso (utente non-admin con [RequireTwoFactor] command — non dovrebbe esistere ma è un canary)

---

## §5 — Impersonate context semantics (interazione con S2)

**Decisione architetturale chiave (NEW finding)**: durante impersonate, la TOTP recency deve essere quella dell'**actor** (admin), NON del subject (target).

### Razionale
- Bob (target) può non avere 2FA enabled affatto
- Bob può non aver mai fatto un TOTP verify
- L'azione "danger-zone" è di alice (l'admin), non di bob (l'identità "borrowed")
- Industry pattern (Okta, Salesforce): step-up verifica le credenziali del REAL actor

### Implementazione
S2 ha creato `UserSessionEntity` con `ImpersonatedByUserId` (alice) e `UserId` (bob). L'impersonate session è una NUOVA row in `user_sessions`. Quando S3 aggiunge `last_totp_verified_at` come colonna sulla session:

**Strategy**: l'impersonate session **eredita** il `LastTotpVerifiedAt` dell'actor al momento della creazione.

```csharp
// In ImpersonationStartCommandHandler (T1 modify)
var actorSession = await _sessionRepository.GetCurrentActorSessionAsync(...);
var newSession = new UserSessionEntity {
    UserId = target.Id,
    ImpersonatedByUserId = requester.Id,
    ImpersonatedUntil = ...,
    LastTotpVerifiedAt = actorSession?.LastTotpVerifiedAt,  // NEW — inherit
    ...
};
```

Poiché `ImpersonationStartCommand` stesso è `[RequireTwoFactor(MaxAgeMinutes = 5)]` (D-S3-7), alice DEVE aver appena fatto step-up — il valore ereditato è fresco per definizione (≤5min).

### Step-up DURANTE impersonate
Quando alice (impersonando bob) fa `POST /auth/2fa/step-up`:
- Verifica TOTP code contro `requester.Id` (alice), non `session.UserId` (bob)
- Aggiorna `session.LastTotpVerifiedAt = now` sulla CURRENT impersonate session (non sulla session originale di alice)
- Audit `TwoFactorStepUp`: `user_id = bob` (subject), `impersonated_user_id = alice` (actor) — convenzione S2

### Step-up impatta solo la CURRENT session
Aggiornare `LastTotpVerifiedAt` sull'impersonate session NON aggiorna anche la session originale di alice (sono 2 row separate). Se alice termina l'impersonate, la sua session originale ha un `LastTotpVerifiedAt` separato. Acceptable — alice può fare un altro step-up se necessario sulla session normale.

---

## §6 — Amends al kickoff post-T0

### D-S3-4b — semplificato (Redis rate-limit, no nuovo DB counter)

**ORIGINALE**: "DB-backed counter via tabella `step_up_attempts`, lockout 15min" — proposta del kickoff DRAFT.

**REVISED**: riusa direttamente `TotpService.VerifyCodeAsync` che ha GIÀ:
- Rate-limit Redis 5/5min per `2fa:totp:{userId}` key
- Lockout 15min via sliding window TTL
- Replay-attack guard via `UsedTotpCodes`
- Audit `TwoFactorVerify`
- Security alerting a 10+ fail
- Metrics

**Conseguenze**:
- T5 step (DB migration `step_up_attempts`) → RIMOSSO. No nuova tabella.
- T5 handler diventa: ~30 LOC wrapper su `_totpService.VerifyCodeAsync` + `_sessionRepository.UpdateLastTotpVerifiedAtAsync`
- File `TwoFactorStepUpRateLimiter.cs` → NON CREARE (era nel plan "File Structure")

### D-S3-3 — confermato
`last_totp_verified_at` nullable timestamptz su `user_sessions`. No nuovi indici.

### Inheritance dell'`actor.LastTotpVerifiedAt` in `ImpersonationStartCommand` (NEW finding §5)

**NEW step in T1**: `ImpersonationStartCommandHandler` (modificato in S2) deve ora anche **copiare l'attuale `LastTotpVerifiedAt` dell'actor** nella session impersonate appena creata. Questo è strettamente legato a S3 (la colonna `last_totp_verified_at` non esiste ancora pre-T1), quindi `T1` deve:
1. Aggiungere migration (LastTotpVerifiedAt column)
2. Modificare `UserSessionEntity` + `Session` domain + `SessionRepository` per hydrate
3. **NEW**: Modificare `CreateSessionCommand` + handler per accettare `LastTotpVerifiedAt = null` (caller può passarlo per impersonation)
4. **NEW**: Modificare `ImpersonationStartCommandHandler` per copiare il valore dall'actor session

### Behavior strict path — actor-aware reading

`TwoFactorEnforcementBehavior` strict path: NON leggere `session.Principal.Subject.LastTotpVerifiedAt` ma `session.LastTotpVerifiedAt` direttamente dal `UserSessionEntity` o (più semplice) **passare il `LastTotpVerifiedAt` nel SessionStatusDto** — la colonna è sulla session, è semantica "session attribute", non "user attribute".

Aggiungi `LastTotpVerifiedAt: DateTime?` al `SessionStatusDto` (mirror del pattern S2 `SessionId`). Il behavior legge `sessionStatus.LastTotpVerifiedAt`.

---

## §7 — Risk matrix (Nygard) — failure modes documentati

| # | Failure mode | Probabilità | Impatto | Mitigation |
|---|-------------|-------------|---------|------------|
| F1 | TOTP secret DB unavailable | Bassa | Step-up endpoint ritorna 503; strict path con `LastTotpVerifiedAt` esistente still works fino a expiry | Documentato. No mitigation aggiuntiva. |
| F2 | Redis rate-limit unreachable | Media (Redis storia di flake) | `_rateLimitService.CheckRateLimitAsync` failure mode dipende dall'impl — verificare se fail-open o fail-closed | T5 verificare fail-open assunto; se fail-closed, aggiungere fallback locale |
| F3 | Flag toggle race (admin flippa mid-request) | Bassa | Lettura non-cached → la request successiva legge il nuovo valore. Race minimo. | Documentato. Acceptable. |
| F4 | Esistenti sessioni con `LastTotpVerifiedAt=null` post-deploy | **CERTA** (è l'effetto voluto post-sweep) | Tutti gli admin DEVONO step-up al primo comando `[RequireTwoFactor]` | Default=OFF deploy + ops sweep + dogfood staging |
| F5 | Admin disabilita 2FA per se stesso → invoca command decorato | Bassa | `IsTwoFactorEnabled=false` → 401 `enroll_required` | By design. Re-enroll è il fix. |
| F6 | TOTP code valido ma session expired tra verify e LastTotpVerifiedAt update | Estremamente bassa | Race: il code è single-use (UsedTotpCodes); se la session expire l'update non avviene → user dovrà re-step-up con un nuovo code | Documentato. Frequenza << 1/year. |
| F7 | Step-up DURING impersonate quando l'impersonate session è già expired (S2 D-S2-4 auto-end) | Bassa | Middleware auto-end emette 401 prima che lo step-up reach l'handler | S2 semantica già copre. |

---

## §8 — FE 401 handler audit (T7 wire format spec)

Trovati 8 handler 401 in `apps/web/src/lib/api/core/httpClient.ts` + `errors.ts` + `i18n/errors.ts`. Tutti gestiscono 401 genericamente (probabilmente redirect-to-login).

**Migration richiesta nel FE** (OUT OF SCOPE S3, ma da documentare in `docs/api/2fa-step-up-protocol.md`):

```ts
// PRIMA (oggi):
if (response.status === 401) {
  router.push('/login');
  return;
}

// DOPO (post-S3 deploy strict):
if (response.status === 401) {
  const body = await response.json();
  if (body?.error === 'two_factor_required') {
    switch (body.subcode) {
      case 'step_up_required':
        openTwoFactorStepUpModal({ retryUrl: request.url });
        return;
      case 'enroll_required':
        router.push('/settings/2fa/enroll?reason=admin_action');
        return;
      case 'locked_out':
        toast.error(`Locked out. Retry after ${body.retryAfterSeconds}s.`);
        return;
    }
  }
  router.push('/login');  // fallback per 401 generici
}
```

**Compat path**: il default=OFF del config flag in deploy iniziale significa zero rilascio di 401 con `subcode` finché ops non flippano in staging. Il FE handler attuale (redirect-to-login) continua a funzionare senza modifiche **per tutti i casi shadow**. Quando il flag flippa in staging, i 401 con `subcode` partono — il FE dovrà avere il refactor pronto in quel momento. Tracking: plan FE separato post-merge S3.

---

## §9 — File da creare/modificare (revised after T0)

Update vs il plan original:

**Da rimuovere** (semplificazione D-S3-4b):
- ❌ `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/RateLimit/TwoFactorStepUpRateLimiter.cs`
- ❌ `apps/api/src/Api/Infrastructure/Entities/Authentication/TwoFactorStepUpAttemptEntity.cs`
- ❌ Migration `step_up_attempts` table

**Da aggiungere** (NEW finding §5):
- ✅ Modify `CreateSessionCommand` — accept `LastTotpVerifiedAt? : DateTime?`
- ✅ Modify `ImpersonationStartCommandHandler` — pass actor's `LastTotpVerifiedAt` to `CreateSessionCommand` (T1 extra step)
- ✅ Modify `SessionStatusDto` — add `LastTotpVerifiedAt? : DateTime?` (mirror S2 `SessionId` pattern)

Resto del plan invariato.

---

## §10 — Conclusioni T0

✅ **Cutover readiness CONFIRMED**: infrastruttura 2FA matura, riusabile per step-up. Schema migration trivial (1 colonna nullable). Risk matrix accettabile con default=OFF deploy strategy.

✅ **2 semplificazioni vs draft kickoff**:
1. D-S3-4b → riusa Redis rate-limit di `TotpService` (no nuovo DB counter)
2. Impersonate context → step inheritance + actor-aware behavior reading (chiarito)

✅ **1 nuovo step in T1**: copia di `LastTotpVerifiedAt` dell'actor nella impersonate session creata.

✅ **Pronto a procedere con T1** (migration + Session domain + extra step impersonate inheritance).

---

*Spike completata 2026-05-26. Amend del kickoff riflessi nel commit di T0. T1 può iniziare dopo signoff utente.*
