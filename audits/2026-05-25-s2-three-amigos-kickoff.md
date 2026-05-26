# S2 Three-Amigos Kickoff — Impersonate Token + Dual Principal

**Date:** 2026-05-25
**Status:** decisions converged
**Plan di riferimento:** `docs/superpowers/plans/2026-05-25-sp5-admin-security-s2-impersonate.md`
**Predecessor:** S1 (`audits/2026-05-25-s1-three-amigos-kickoff.md`) — MERGED in `main-dev` as PR #1532 / commit `fffe55fc7`.

Facilitato come spec-panel three-amigos kickoff (Gregory facilitator; Fowler BE-proxy; Crispin+Adzic QE-proxy; Nygard Sec/Ops-proxy; Wiegers PM-proxy; Newman API-proxy; Hightower Ops-proxy; Cockburn UC-proxy). Output classico three-amigos: domande risolte, scenari executable, ownership matrix, DoD.

---

## Contesto da S1 (già live)

- `audit_logs.impersonated_user_id` (Guid?, indicizzato con filter `IS NOT NULL`) → pronta a ricevere il valore.
- `AuditLoggingBehavior` ha già il campo `ImpersonatedUserId` sul payload outbox — attualmente sempre `null` perché `ICurrentUserService` non è ancora wired. S2 lo wira.
- `[AuditableAction]` + `[AtomicAudit]` + outbox processor → quando l'admin impersona ed esegue un comando distruttivo, l'audit lo cattura naturalmente via il behavior, senza nuove primitives.
- Convenzione `audit_logs.user_id` = soggetto funzionale, `impersonated_user_id` = attore reale (vedi query forensica in S1 §Sblocco di S2).

## ⚠️ Legacy impersonation system già live (scoperto in T0 spike, 2026-05-26)

Il codebase ha già un'implementazione di impersonation in produzione (issues **#3349 / #2890**):
- `ImpersonateUserCommand(TargetUserId, AdminUserId, Reason)` + handler
- Endpoint `POST /admin/users/{userId}/impersonate` (gate `RequireSuperAdminSession`) + `POST /admin/impersonation/end` (`RequireAdminSession`)
- `EndImpersonationCommand` + handler con `RevokeSessionCommand` interno
- **3 path duplicate** che scrivono `audit_logs` direttamente via `IAuditLogRepository.AddAsync` bypassando l'outbox S1 (questo è il caso #1534)
- Sessione mono-principale: `UserSessionEntity.UserId=target`, con `IpAddress="impersonated"` come signal hack

**Decisione utente (2026-05-26): Refactor in-place + tightening.** Manteniamo tutte le 6 decisioni (incluso D-S2-1 superadmin-only) e dismantliamo i 3 manual `AddAsync` in favore di `[AuditableAction]` via behavior. I file legacy vengono rinominati e riscritti in T3 — vedi spike doc `audits/2026-05-26-s2-spike-cluster-classification.md` §4 Wave 3 per il dettaglio.

**Goal generale S2:** integrare l'impersonation esistente con il dual-principal pattern + l'audit outbox di S1; aggiungere expiry cap 15min, revoke kill-switch, e dismantle le 3 scritture audit dirette legacy.

---

## Decisioni convergenti (6)

### D-S2-1 — Eligibility (chi può impersonate, e chi)

**Decisione:** **Solo `superadmin` può avviare impersonate**, e **solo verso un target che NON sia superadmin**. Demo account e suspended account sono **target non eligibili**. Self-impersonate vietata.

**Razionale:** privilege escalation surface ridotta al minimo. Impersonate verso peer/superiore non è una use case operativa reale (sono casi di "scelgo un altro account simile per scaricare i rischi" → audit fraud). Suspended user impersonate confonderebbe ban-check su future request.

**Casi gestiti:**
- 403 `cannot_impersonate_peer_or_higher` su target superadmin.
- 403 `cannot_impersonate_self` su `targetUserId == requesterId`.
- 403 `target_account_ineligible` su suspended/demo/banned.
- 403 `caller_not_authorized` se requester role ≠ superadmin.

### D-S2-2 — Dual-principal API shape

**Decisione:** **Opzione B — `Principal { Subject, Actor? }` sostituisce `SessionStatusDto.User`**. Refactor breaking del DTO + codemod-guided sui 79 file consumer (~394 occurrences).

**Cluster identificati con grep (sample da `apps/api/src`):**

| # | Cluster | Occ. | File | Sotto impersonate, usa |
|---|---------|------|------|------------------------|
| C1 | Audit attribution (`CreatedBy`, `RequestingUserId`, `AdminUserId`) | 6 | 3 | **Actor** — chi *davvero* esegue |
| C2 | Authorization (`Role`, `HasSufficientRole`, role enum parse) | 9 | 5 | **Actor** — no privilege escalation via impersonate |
| C3 | Resource ownership (`UserId =`, `OwnerId =`, `CreatedByUserId =`) | 23 | 11 | **Subject** — owner del proprio profilo/device/share |
| C4 | Rate-limit / quota (`rateKey = $"user:{...}"`) | 1 | 1 | **Subject** — quota dell'utente impersonato, non aggirabile |

39 call site semanticamente classificati / 394 occurrences totali. Le restanti ~355 sono pattern di estrazione (`if session?.User == null`, `is SessionStatusDto { IsValid: true, ... }`) gestibili da codemod uniforme.

**Razionale (Fowler + Nygard):** un'API che lascia il caller scegliere la semantica giusta produce bug latenti per anni. `Principal` esplicito forza ogni nuovo codice a dichiarare se vuole subject (default `Subject`) o actor (`Actor ?? Subject`). Compile error > silent bug forensico a 6 mesi.

**Wire format JSON resta backward compatible:** `GET /auth/session` continua a serializzare `{ "user": {...}, "actor": {...} | null }` — il FE legge `user` come prima; campo `actor` opt-in.

### D-S2-3 — Read-only vs full impersonate; audit attribution

**Decisione:** **Full impersonate**. L'attore impersonante può eseguire qualsiasi comando, incluso `[AtomicAudit]` distruttivo. L'audit del comando segue la convenzione S1:
- `audit_logs.user_id` = **subject** (l'utente impersonato — coerente con la query forensica "azioni svolte come Bob")
- `audit_logs.impersonated_user_id` = **actor** (l'admin reale)
- `[AtomicAudit]` semantics inalterate: la mutazione + l'audit row commit atomically.

**Razionale (Adzic + Hightower):** read-only impersonate richiede una whitelist/blacklist di comandi che diventerebbe stale. Full impersonate + audit forense è il pattern industry-standard (es. Salesforce "Login As", Google Workspace admin impersonation). La discriminazione subject/actor è già nelle colonne audit di S1 — usarla è zero costo aggiuntivo.

### D-S2-4 — Auto-expiry semantics

**Decisione:** **401 mid-request + audit `ImpersonationAutoEnded`**. Quando `ImpersonatedUntil` scade e una request raggiunge il middleware di auth, la request è rifiutata con `401 Unauthorized` e una riga `audit_logs.action="ImpersonationAutoEnded"` viene scritta via outbox (best-effort).

**Razionale (Wiegers Q5 + Nygard F2):** snapshot-at-begin-of-request, non point-in-time-of-commit. Il behavior verifica `ImpersonatedUntil > now` all'inizio del middleware. Se la tx commit ritarda di 200ms e tecnicamente la sessione è expired, la request comunque commit (atomicità S1 inalterata). Il prossimo request del client trova `ImpersonatedUntil <= now` e ottiene 401.

**Cap default:** 15 minuti. **Configurabile via `SystemConfiguration` BC** (`ImpersonationMaxDurationMinutes`, default 15, range 5-60). Newman raccomanda non hardcodare.

### D-S2-5 — Revoke propagation SLA

**Decisione:** **≤5s via invalidate-on-read**. Il superadmin POSTa `/admin/impersonation/revoke { sessionId }` → il record `UserSessionEntity` viene marcato `RevokedAt = now` (UPDATE atomica). Il middleware di auth, in ogni request, esegue una SELECT del session record (già fa questo per `LastSeenAt` tracking) e verifica `RevokedAt IS NULL`.

**Razionale (Nygard F1):** in-memory caching del session record sarebbe più veloce ma introduce finestra di compromesso. Postgres `SELECT` su PK indicizzata < 1ms — l'overhead è marginale rispetto alla finestra di sicurezza. Se la latenza diventa misurabile, follow-up con Redis-cache + LISTEN/NOTIFY invalidation (pattern già noto nel codebase).

**Failure mode F1 (in-flight request after revoke)** documentato: la request che ha già superato il middleware completa con audit; il revoke prende effetto sulla request successiva. Acceptable per il threat model (un revoke vuole prevenire ulteriori abusi, non rollbackare azioni già committed).

### D-S2-6 — Step-up 2FA per START impersonate

**Decisione:** **Yes, in shadow mode**. `ImpersonationStartCommand` decorato con `[RequireTwoFactor(Reason="Impersonate other user; sensitive action.")]`. La `TwoFactorEnforcementBehavior` in S2 resta in **warning-only mode** (consistente con lo state del behavior). S3 flipperà a strict.

**Razionale (Wiegers Q4):** marca la dipendenza con S3 nello stesso modo in cui `DeleteUserCommand` già fa. Quando S3 fliperrà lo strict mode, `ImpersonationStartCommand` farà parte del cutover automaticamente — zero retrofit.

---

## Acceptance scenarios (Adzic — Given/When/Then)

I 5 scenari diventano gli **integration test acceptance** del DoD (gate). File target: `tests/Api.Tests/Integration/Administration/S2AcceptanceScenariosTests.cs`.

### Scenario S2-1 — Happy path start
```
Given superadmin "alice@meeple.app" with 2FA-verified session
  And target user "bob@example.com" exists, role=user, status=Active
When alice POSTs /admin/impersonation/start { targetUserId: bob, reason: "bug-1234", durationMinutes: 15 }
Then response 201 Created with { sessionId, impersonatedUntil }
  And UserSessionEntity created with UserId=bob.Id, ImpersonatedByUserId=alice.Id, ImpersonatedUntil≈now+15min
  And audit_outbox has 1 Pending row (action="ImpersonationStarted", user_id=bob, impersonated_user_id=alice)
  And SessionStatusDto from /auth/session of the new session returns Principal { Subject: bob, Actor: alice }
```

### Scenario S2-2 — Auditable destructive command during impersonate
```
Given an active impersonation session (alice → bob) with valid 2FA
When the impersonate-session executes DeleteUserCommand(carol)   // [AtomicAudit]
Then carol is hard-deleted
  And audit_outbox has 1 row (action="UserDelete", user_id=bob, impersonated_user_id=alice)
  And the [AtomicAudit] transaction-atomicity guarantee from S1 holds
  And after processor drain, audit_logs.Id == audit_outbox.Id (idempotency preserved)
```

### Scenario S2-3 — Auto-expiry mid-flight
```
Given an active impersonation session expiring at T+15min
When the request arrives at T+15min+1s
Then auth middleware returns 401 Unauthorized
  And audit_outbox has 1 row (action="ImpersonationAutoEnded", user_id=bob, impersonated_user_id=alice)
  And subsequent requests from same session continue to return 401
```

### Scenario S2-4 — Superadmin revoke kills in-flight session
```
Given alice is impersonating bob (UserSessionEntity X)
When superadmin "root" POSTs /admin/impersonation/revoke { sessionId: X }
Then within ≤5s, any subsequent request bearing session X's cookie returns 401
  And audit_outbox has 1 row (action="ImpersonationRevoked", user_id=root, impersonated_user_id=alice)  // <- root revoking alice's impersonate of bob
  And the original UserSessionEntity X has RevokedAt set
```

### Scenario S2-5 — Cannot impersonate a superadmin
```
Given alice is a superadmin
  And target user "trent" is also a superadmin
When alice POSTs /admin/impersonation/start { targetUserId: trent, reason: "..." }
Then response 403 Forbidden with code "cannot_impersonate_peer_or_higher"
  And no UserSessionEntity is created
  And audit_outbox has 1 row (action="ImpersonationStartFailed", user_id=trent, impersonated_user_id=alice, Result="Error")
```

---

## Ownership Matrix

| Area | Owner | Notes |
|------|-------|-------|
| `Principal` DTO refactor + codemod | BE | Wave 1: rename `User` → `Principal.Subject` su ~355 touch points (meccanico). Wave 2: 15 punti C1+C2 → `Principal.Actor ?? Subject`. |
| `UserSessionEntity` extension + migration | BE | Nullable cols `impersonated_by_user_id` + `impersonated_until`. |
| Commands (Start/End/Revoke) + handlers | BE | Tutti `[AuditableAction]`. `Start` anche `[RequireTwoFactor]` (shadow). |
| Auth middleware: invalidate-on-read | BE | Verifica `RevokedAt IS NULL` + `ImpersonatedUntil > now` ad ogni request. |
| Health gauge `impersonation_active_count` | BE/Ops | ObservableGauge stile S1 T4b. |
| Read endpoints (active list, by-admin) | BE | Solo superadmin role. |
| Test (5 scenari + unit) | QE | Riusa `SharedTestcontainersFixture` + `IntegrationServiceCollectionBuilder.CreateBase` come S1. |
| Three-amigos follow-up | Facilitator | re-convene dopo T1 (migration shape) se necessario. |
| FE banner UI + actor indicator | FE | **OUT OF SCOPE S2** — separato plan FE; il wire format JSON è già pronto. |

---

## Definition of Done (8 criteri)

| # | Criterio | Verifica |
|---|----------|----------|
| 1 | `Principal` shape mergiato senza regressioni sui 79 file consumer | Backend Fast green; all 17k+ test PASS |
| 2 | Migration `user_sessions` aggiunge 2 colonne | `dotnet ef database update --dry-run` ok |
| 3 | C1+C2 (15 punti audit/authz) usano `Actor ?? Subject` | Code review checklist + grep di `session.Principal.Subject` in file C1+C2 = 0 |
| 4 | `ImpersonationStartCommand` enforce 6 eligibility rules (D-S2-1) | 6 unit test + scenario S2-5 |
| 5 | Audit attribution: subject e actor finiscono nel posto giusto (D-S2-3) | Scenario S2-2 |
| 6 | Auto-expiry mid-flight (D-S2-4) | Scenario S2-3 |
| 7 | Revoke propagation ≤5s (D-S2-5) | Scenario S2-4 + latency benchmark |
| 8 | Gauge `meepleai.security.impersonation.active.count` esposto | MeterListener test + scrape `/metrics` |

---

## Open follow-ups (non bloccanti per S2)

- **Cache-backed session invalidation con Redis + LISTEN/NOTIFY** se la SELECT per-request diventa hotspot (Nygard).
- **FE banner permanente "Stai impersonando X"** durante sessione attiva (plan FE separato).
- **Test-impersonation bypass header** (`X-Meeple-TestImpersonation: true` per e2e dev/staging) — utile ma non bloccante (Hightower).
- **Strict mode S3 cutover** flippa il `TwoFactorEnforcementBehavior` da shadow a enforced anche per `ImpersonationStartCommand`.

---

*Questo documento è il riferimento autoritativo per la DoD di S2. Citarlo dai commit messages dei task e dal merge finale di S2. Cita anche il three-amigos S1 per il pattern outbox/audit.*
