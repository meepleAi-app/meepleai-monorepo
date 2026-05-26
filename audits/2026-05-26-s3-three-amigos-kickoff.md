# S3 Three-Amigos Kickoff — Strict 2FA Enforcement Cutover

**Date:** 2026-05-26
**Status:** decisions converged (post-critique revision)
**Plan di riferimento:** `docs/superpowers/plans/2026-05-26-sp5-admin-security-s3-strict-2fa.md`
**Predecessor:** S2 (`audits/2026-05-25-s2-three-amigos-kickoff.md`) — MERGED in `main-dev` as PR #1555 / commit `7ca5b5151`.
**Issue:** Q2 2026 Security Review #186 P1.1 — 2FA admin enforcement.

Facilitato come spec-panel three-amigos kickoff (Gregory facilitator; Fowler BE-proxy; Crispin+Adzic QE-proxy; Nygard Sec/Ops-proxy; Wiegers PM-proxy; Newman API-proxy; Hightower Ops-proxy; Cockburn UC-proxy). Output classico: domande risolte, scenari executable, ownership matrix, DoD. **Draft iniziale revisionato in critique-mode** (vedi sezione "Critique findings applicate") — 3 critical + 3 major fix incorporati prima della persistenza.

---

## Contesto da S2 (già live)

- `TwoFactorEnforcementBehavior` esiste in shadow mode (`apps/api/src/Api/BoundedContexts/Authentication/Application/Behaviors/TwoFactorEnforcementBehavior.cs`). Quando un comando decorato `[RequireTwoFactor]` viene eseguito:
  - 2FA off su user → `LogWarning` + proceed
  - 2FA on → `LogInformation` + proceed (NESSUN check di recency — `LastTotpVerifiedAt` non esiste ancora)
- 4 comandi attualmente decorati:
  - `ChangeUserRoleCommand` (default MaxAge 30min)
  - `DeleteUserCommand` (default 30min)
  - `SuspendUserCommand` (default 30min)
  - `ImpersonationStartCommand` (S2; default 30min — review propone 5min, vedi D-S3-7)
- `ExtractImpersonationActorId` in `AuditLoggingBehavior` (S2) wira l'actor durante impersonate — riusabile per attribution del `TwoFactorStepUp` audit.
- Wire format S2 `SessionStatusResponse` con backward-compat `user` field — riusabile per la response del step-up endpoint.

---

## Decisioni convergenti (7)

### D-S3-1 — Strategia di cutover (config-flag, default OFF)

**Decisione:** `SystemConfiguration.TwoFactorStrictMode` (boolean, DB-persisted, admin toggle in `/admin/config`). **Default `false` al merge** — il behavior continua a girare in shadow finché ops non flip. Flippato prima in staging per validazione, poi in prod. Removal del flag = follow-up dopo 1 sprint di prod-stable.

**Razionale (Nygard + Hightower):** alla deploy ogni sessione admin esistente ha `LastTotpVerifiedAt=null`. Se default=ON, strict immediato = mass-lockout di TUTTI gli admin contemporaneamente. Default=OFF separa "code merge" da "behavior change", consentendo ops di:
1. Sweep pre-flip (D-S3-5) per garantire 100% admin con 2FA enrolled
2. Flip in staging, dogfooding 24h
3. Flip in prod con rollback istantaneo via admin toggle se emerge regression

Flag-read non-cached (per-command pipeline): un toggle prende effetto sulla request successiva, no Redis cache warmup.

### D-S3-2 — HTTP status code e wire format

**Decisione:** `401 Unauthorized` + body `{ error: "two_factor_required", subcode: "step_up_required" | "enroll_required" | "locked_out" }` + header `WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"`.

**Razionale (Newman):** 401 conflate semanticamente con session-invalid ma riusa l'infrastruttura 401 esistente. Il `subcode` permette al FE di distinguere — su `step_up_required` apre la modale TOTP, NON redirect a `/login`. Il `WWW-Authenticate` header rispetta RFC 7235 e permette ai client API non-browser di disambiguare programmaticamente.

**Wire format spec**: `docs/api/2fa-step-up-protocol.md` — handoff esplicito per il team FE (separato plan FE post-cutover).

### D-S3-3 — Schema `LastTotpVerifiedAt`

**Decisione:** colonna `last_totp_verified_at` (timestamptz nullable) su `user_sessions`. No index (read-by-PK è sufficiente — la sessione corrente è già caricata via token hash).

**Razionale (Fowler):** TOTP recency è per-sessione (un device verificato non implica gli altri). Migration tipo S2 (additive, nullable). `SessionRepository.MapToDomain` + `MapToPersistence` aggiornati per hydrate del campo.

### D-S3-4 — Step-up endpoint

**Decisione:** `POST /api/v1/auth/2fa/step-up { code: "123456" }` → 200 con `SessionStatusResponse` (riusa wire S2, ora con `lastTotpVerifiedAt` rinfrescato) | 401 (codice errato) | 503 (TOTP store down).

**Razionale (Newman):** endpoint dedicato auth-prefix, separato da `/auth/2fa/enroll` (esistente). Body minimale `{ code }`; la sessione corrente identifica subject+actor.

### D-S3-4b — Rate-limit step-up (DB-backed)

**Decisione:** 5 attempt/min per session via counter DB-backed (analogo al pattern `BggRateLimitMiddleware`). Dopo 5 fail consecutivi → lockout 15min (audit `TwoFactorStepUpLockout`).

**Razionale (Nygard):** in-memory counter non funziona su API multi-istanza (3 pod = 15/min reale). DB counter è authoritative cross-instance.

### D-S3-5 — Policy admin senza 2FA enrolled (hard block)

**Decisione:** `enroll_required` 401, no mutation. Pre-req ops: sweep + email forzato a admin senza 2FA prima del flip in prod. **NEW endpoint admin** `GET /admin/users/no-2fa` per il sweep (superadmin-only, returns lista admin/superadmin con `IsTwoFactorEnabled=false`).

**Razionale (Wiegers):** Hard block è coerente con il principio "no halfway secure". Il pre-cutover sweep è SMART (lista enumerable, target 0 admin senza 2FA prima del flip prod).

### D-S3-6 — Audit attribution

**Decisione:** ogni step-up emette audit `TwoFactorStepUp` (action="verify"), ogni rigetto strict emette `TwoFactorRequired` (action="block"). Entrambi best-effort (S1 default — non distruttivi). Durante impersonate, l'attribution segue la convenzione S2: `user_id` = subject, `impersonated_user_id` = actor (l'admin che esegue lo step-up).

**Razionale (Adzic + Crispin):** lo step-up DURANTE impersonate è un'azione DELL'ADMIN REALE (alice impersonando bob fa step-up → è alice che verifica il suo TOTP, bob non c'entra). `ExtractImpersonationActorId` (S2) già wira correttamente.

### D-S3-7 — Per-command MaxAge tuning

**Decisione:** decorazione esplicita per comando:
- `ImpersonationStartCommand` → `[RequireTwoFactor(MaxAgeMinutes = 5)]` (HIGH RISK — sessione completa target user)
- `DeleteUserCommand`/`ChangeUserRoleCommand`/`SuspendUserCommand` → default 30min

**Razionale (Wiegers):** la default uniforme 30min non riflette il risk profile. `ImpersonationStart` concede pieno accesso a un altro account — fresh challenge 5min è industry-standard (Salesforce, Okta).

---

## Critique findings applicate (3 critical + 3 major)

| # | Critique | Expert | Applied to |
|---|----------|--------|-----------|
| C1 | Mass-lockout risk al cutover (default=ON) | Nygard + Hightower | D-S3-1 → default=OFF |
| C2 | Per-command MaxAge tuning mancante | Wiegers | D-S3-7 (new) |
| C3 | Distributed rate-limit (in-memory non basta) | Nygard | D-S3-4b (new) |
| M1 | FE migration path su `subcode` non documentato | Newman | D-S3-2 + wire format spec |
| M2 | Regression scenario per non-decorated commands | Crispin | S3-7 (new) |
| M3 | Step-up failure modes (503 TOTP store down) | Nygard | D-S3-4 |

---

## Acceptance scenarios (8 — gate DoD)

File target: `apps/api/tests/Api.Tests/Integration/Administration/S3AcceptanceScenariosTests.cs`.

### S3-1 — Happy path
```
Given admin "alice@admin.test" with 2FA enabled, LastTotpVerifiedAt = now - 5min
  And TwoFactorStrictMode = true
When alice POSTs /admin/users/bob/delete
Then response 200, bob soft-deleted
  And audit_outbox has "UserDelete" row (user_id=alice)
```

### S3-2 — Stale TOTP (default MaxAge 30min)
```
Given admin alice with 2FA enabled, LastTotpVerifiedAt = now - 45min
  And TwoFactorStrictMode = true
When alice POSTs /admin/users/bob/delete
Then response 401 with body { error: "two_factor_required", subcode: "step_up_required" }
  And header WWW-Authenticate: TOTP-StepUp realm="meepleai-admin"
  And bob is NOT deleted
  And audit_outbox has "TwoFactorRequired" row (user_id=alice, resource="DeleteUser")
```

### S3-3 — Not enrolled
```
Given admin alice with IsTwoFactorEnabled=false
  And TwoFactorStrictMode = true
When alice POSTs /admin/users/bob/change-role
Then response 401 { error: "two_factor_required", subcode: "enroll_required" }
  And no mutation
```

### S3-4 — Step-up + retry
```
Given Scenario S3-2 state (401 step_up_required)
When alice POSTs /auth/2fa/step-up { code: <valid TOTP> }
Then response 200 with SessionStatusResponse (lastTotpVerifiedAt = now)
  And audit_outbox has "TwoFactorStepUp" row (user_id=alice)
When alice re-POSTs /admin/users/bob/delete
Then response 200, bob deleted
  And audit_outbox has "UserDelete" row
```

### S3-5 — Throttle + lockout
```
Given admin alice with 5 failed step-up attempts in last 60s
When alice POSTs /auth/2fa/step-up { code: any }
Then response 429 with body { error: "two_factor_required", subcode: "locked_out", retryAfterSeconds: 900 }
  And audit_outbox has "TwoFactorStepUpLockout" row (user_id=alice, forensic detail)
```

### S3-6 — Impersonation context
```
Given superadmin alice is impersonating bob (S2 active session)
  And alice's LastTotpVerifiedAt = now - 10min (STALE for ImpersonationStart per D-S3-7, but FRESH for DeleteUser default 30min)
  And TwoFactorStrictMode = true
When the impersonate-session executes DeleteUserCommand(carol)
Then response 200, carol deleted
  And audit_outbox: user_id=bob, impersonated_user_id=alice
  // alice's 10min recency satisfies DeleteUser MaxAge=30min
```

### S3-7 — Non-decorated command (regression guard)
```
Given any user invoking CreateGameCommand (NOT [RequireTwoFactor])
  And TwoFactorStrictMode = true
  And user with 2FA disabled / no LastTotpVerifiedAt
When user POSTs /games
Then response 200 (created)
  // Behavior MUST short-circuit on missing attribute; no false positives
```

### S3-8 — Per-command MaxAge differentiation
```
Given admin alice with LastTotpVerifiedAt = now - 6min
  And TwoFactorStrictMode = true
When alice POSTs /admin/impersonation/start (MaxAgeMinutes = 5 per D-S3-7)
Then response 401 step_up_required (6min > 5min)
When alice POSTs /admin/users/X/delete (MaxAgeMinutes = 30 default)
Then response 200 (6min ≤ 30min)
```

---

## Ownership Matrix

| Area | Owner | Notes |
|------|-------|-------|
| Migration `last_totp_verified_at` + `UserSessionEntity` + Session domain | BE | Pattern S2 |
| `TwoFactorEnforcementBehavior` strict path + flag read | BE | Throw `TwoFactorRequiredException` |
| `TwoFactorRequiredException` + ExceptionFilter mapping → 401 + body + header | BE | New domain exception in SharedKernel.Domain.Exceptions |
| `POST /auth/2fa/step-up` endpoint + rate-limit (DB counter) | BE | IMediator.Send only (CQRS) |
| `GET /admin/users/no-2fa` ops sweep endpoint | BE | Superadmin filter |
| `SystemConfiguration.TwoFactorStrictMode` flag | BE | Pattern `RegistrationMode` |
| Wire format doc `docs/api/2fa-step-up-protocol.md` | BE + FE handoff | Specifica `subcode` + header + retry semantics |
| 8 acceptance scenarios + unit tests | QE | Lezione S2: **pipeline reale obbligatoria** ([[feedback_acceptance_tests_must_exercise_real_pipeline]]) |
| Pre-cutover sweep ops (email admin senza 2FA) | Ops | Usa `GET /admin/users/no-2fa` |
| FE step-up modal + 401 handler refactor | FE | OUT OF SCOPE S3 — plan FE separato; wire ready in D-S3-2 |

---

## Definition of Done (10 criteri)

| # | Criterio | Verifica |
|---|----------|----------|
| 1 | Migration `last_totp_verified_at` applica clean | `dotnet ef database update --dry-run` ok + Migration Safety Gate CI verde |
| 2 | Strict behavior throw `TwoFactorRequiredException` dietro flag `TwoFactorStrictMode` | Scenari S3-1..3 PASS |
| 3 | ExceptionFilter → 401 + body + header `WWW-Authenticate` | 1 unit test su filter mapping |
| 4 | `POST /auth/2fa/step-up` con rate-limit DB-backed (5/min, lockout 15min) | Scenari S3-4 + S3-5 |
| 5 | 4 comandi decorati + ImpersonationStart con MaxAge=5min | Scenario S3-8 |
| 6 | Audit `TwoFactorRequired` + `TwoFactorStepUp` + `TwoFactorStepUpLockout` via outbox | Scenari S3-2/S3-4/S3-5 |
| 7 | Admin endpoint `GET /admin/users/no-2fa` per ops sweep | Integration test + superadmin gate |
| 8 | Acceptance tests via pipeline reale (no fixture diretto di `SessionStatusDto`) | `S3AcceptanceScenariosTests` traversa `ValidateSessionQueryHandler` + behavior |
| 9 | Wire format `docs/api/2fa-step-up-protocol.md` published | PR include il doc |
| 10 | Regression scenario S3-7 (non-decorated commands) | PASS — behavior short-circuit su attr null |

---

## Open follow-ups (non bloccanti per S3)

- **FE step-up modal** + 401 `subcode` handler refactor (plan FE separato; wire format ready in D-S3-2)
- **WebAuthn / passkeys** (#186 P1.2, epic separato)
- **Recovery codes / backup factors** (Q2 §11 D5)
- **Auto-enrollment forzato** (ops task; il blocker S3 è il SWEEP, l'enroll automatico è enhancement)
- **Removal del flag `TwoFactorStrictMode`** dopo 1 sprint di prod-stable

---

*Questo documento è il riferimento autoritativo per la DoD di S3. Citarlo dai commit messages dei task e dal merge finale. Cita anche il three-amigos S2 per il pattern dual-principal e [[feedback_acceptance_tests_must_exercise_real_pipeline]] per il pattern di testing.*
