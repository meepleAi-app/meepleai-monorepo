# S1 Three-Amigos Kickoff — Audit Schema + Outbox

**Date:** 2026-05-25
**Status:** decisions converged
**Plan di riferimento:** `docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-audit-schema.md`
**Plan supplement:** `docs/superpowers/plans/2026-05-25-sp5-admin-security-s1-supplement.md`

Facilitato come spec-panel three-amigos kickoff (Gregory facilitator; Fowler BE-proxy; Crispin+Adzic QE-proxy; Nygard Sec/Ops-proxy; Wiegers PM-proxy; Hohpe Data-proxy). Output classico three-amigos: domande risolte, scenari executable, ownership matrix, definition of done.

---

## Decisioni convergenti

### Q1 — Atomicità outbox vs handler che chiamano `SaveChanges` autonomamente

**Decisione:** **opzione (a) ristretta**. Refactor degli handler **distruttivi** (`DeleteUserCommand`, `RotateApiKeyCommand`, `EmergencyShutdownCommand`, eventuali altri trigger S3) per delegare `SaveChanges` al behavior → atomicità **garantita** su queste azioni. Per handler non-distruttivi (`ChangeRoleCommand`, `UpdateTierCommand`, ecc.) si accetta **best-effort** (outbox scritta in seconda tx; perdita audit log come warning, non come failure).

**Razionale:** la perdita di un audit su un'azione **distruttiva** è irreparabile e viola il commitment forensico; sulle altre, recuperabile da telemetria/cliente.

### Q2 — Payload size limit

**Decisione:** **tronca per campo, non per JSON intero**. Soglia per campo "collection": >50 items → tronca a 10 + metadata `{"_truncated": ["Games"], "_original_count": {"Games": 543}}`. Soglia totale payload: 256KB hard limit, fail-loud (riga marcata `Failed` con `last_error="payload_oversize"`, mai persa silenziosamente).

**Razionale:** per un delete, `before_json` è l'unico stato; troncare il JSON intero perde campi critici. Troncare le collection grosse preserva i campi scalari (email, role, tier).

### Q3 — Crash recovery + idempotency + monitoring

**Decisione (3 punti):**
1. **`AuditLogEntity.Id == AuditOutboxEntity.Id`** (stesso GUID): la outbox row genera l'ID che diventa la PK di `audit_logs`. Idempotency naturale.
2. **`INSERT ... ON CONFLICT (id) DO NOTHING`** nel processor: il retry post-crash non duplica.
3. **Metriche Prometheus** su `/metrics`:
   - `audit_outbox_pending_count` (gauge): rows con `status=Pending`
   - `audit_outbox_oldest_pending_age_seconds` (gauge): età della row pending più vecchia
   - Alert: `pending_count > 1000` OR `oldest_age > 300s` → processor degraded.

**Razionale (Hohpe):** at-least-once delivery + idempotent consumer = exactly-once effect. Pattern outbox standard.

### Q4 — Definition of Done verificabile per S1

8 criteri misurabili (vedi sezione DoD sotto). Solo dopo tutti ✅ S1 è done.

---

## Acceptance scenarios (Adzic — Given/When/Then)

I 5 scenari diventano gli **integration test acceptance** del Task 4 (processor) o un test file dedicato `tests/Api.Tests/Integration/Administration/S1AcceptanceScenariosTests.cs`.

### Scenario 1 — Audit di un delete user atomico
```
Given an admin "alice@meeple.app" with active session
  And a target user "bob@example.com" with 3 game ownerships
When alice executes DeleteUserCommand(bob)
  And the command's transaction commits successfully
Then bob is soft-deleted in users table
  And exactly ONE row exists in audit_outbox (Pending) with action="user.delete"
  And the outbox payload contains bob's email, role, ownership count
  And the outbox row Id equals the future audit_logs.Id (idempotency key)
```

### Scenario 2 — Audit row promoted by processor
```
Given an audit_outbox row in Pending status from Scenario 1
When AuditOutboxProcessor.RunOnceAsync(batchSize=100) executes
Then exactly ONE row appears in audit_logs with the same Id
  And the outbox row status transitions to Sent
  And audit_logs.before_json contains bob's pre-delete state (truncated per Q2 if >256KB)
  And audit_logs.after_json is null (delete operation)
```

### Scenario 3 — Crash recovery / no duplicates
```
Given 5 outbox rows in Pending
When the processor processes 3 rows successfully (INSERT into audit_logs + MarkSent)
  And the processor is killed before completing the batch (no commit)
  And the processor restarts
Then audit_logs contains exactly 3 rows (the originally processed ones)
  And the remaining 2 outbox rows are picked up on the next batch
  And no duplicate INSERT errors occur (ON CONFLICT DO NOTHING)
```

### Scenario 4 — Best-effort handler regression
```
Given a non-destructive handler (e.g. ChangeUserRoleCommand) that calls SaveChanges autonomously
When the command executes successfully
Then the role change is persisted in users table
  And an audit_outbox row is written (best-effort, may be in a separate tx)
  And IF the second tx fails, a warning is logged + the audit is skipped (no exception bubbles to user)
```

### Scenario 5 — Payload truncation guard
```
Given an admin entity with 543 game ownerships
When the interceptor captures the before/after snapshot
Then collections >50 items are truncated to 10 + metadata
  And the resulting payload_json is ≤256KB
  And IF the payload still exceeds 256KB after truncation
Then the outbox row is marked Failed with last_error="payload_oversize"
  And the underlying mutation still commits (audit failure does NOT block the business action)
```

---

## Ownership Matrix

| Area | Owner | Notes |
|------|-------|-------|
| Schema migration + entity (Task 1) | BE | DBA review se esistente; nota su `jsonb` choice |
| Interceptor + Behavior refactor (Task 2-3) | BE | code review BE + security (Nygard-proxy) per i 3-4 handler distruttivi |
| Processor BackgroundService (Task 4) | BE | health endpoint + Prometheus metrics |
| Test (unit + integration acceptance) | QE | Testcontainers fixtures; scenari 1-5 sono i gate |
| Monitoring/dashboard + alert rules | Ops | scrape config Prometheus + alert thresholds |
| D-4 spike report (Task 0) | BE + security | audit document mergiato |
| Three-amigos follow-up | Facilitator | re-convene dopo Task 2 se i contratti cambiano |

---

## Definition of Done (8 criteri)

| # | Criterio | Verifica |
|---|----------|----------|
| 1 | Task 0 D-4 chiuso, report mergiato | PR `audits/2026-05-25-audit-log-d4-spike.md` mergiata |
| 2 | Migration applicata su dev senza errori | `dotnet ef database update` aggiunge le 4 colonne + tabella `audit_outbox` |
| 3 | Tutti i ~8 handler `[AuditableAction]` esistenti continuano a produrre audit (no regression) | Integration test sweep su comandi esistenti |
| 4 | Le azioni distruttive (Q1 list) sono atomiche con audit | Scenario 3 + chaos test "kill mid-tx → no audit" |
| 5 | Outbox processor drena (latenza p95 < 10s sotto carico tipico) | Benchmark synthetic + osservato 1 settimana in dev |
| 6 | Payload truncation funziona (Q2) | Scenario 5 |
| 7 | Idempotency (Q3) | Scenario 3 |
| 8 | Metriche Prometheus esposte (Q3) | Scrape `/metrics`, verifica `audit_outbox_pending_count` + `audit_outbox_oldest_pending_age_seconds` |

---

## Open follow-ups (non bloccanti per S1)

- **Read endpoint merge `audit_logs ∪ outbox.pending`** se la latenza diventa problema (Nygard).
- **LISTEN/NOTIFY Postgres** invece di polling 5s, se il throughput sale (Hohpe).
- **Audit `ChangeRole`/`UpdateTier` come "atomico" anche loro** — valutazione post-S1 (Crispin).

---

## DoD verification log

**Verificato il:** 2026-05-25 · **Branch:** `feature/sp5-admin-security-s1-impl` · **Verificatore:** @DegrassiAaron

Esito complessivo: **7/8 criteri verificati a livello di codice/test**; **criterio 5 (latenza p95 processor) differito** a osservabilità ops (non misurabile in questo ambiente: nessun carico di produzione né load-generator). Il criterio 8 è soddisfatto con una **deviazione di naming documentata**.

| # | Criterio | Status | Verificato il | Evidenza |
|---|----------|--------|--------------|----------|
| 1 | Task 0 D-4 report mergiato | ✅ committed | 2026-05-25 | `audits/2026-05-25-audit-log-d4-spike.md` in HEAD (`6b350611b`). Nota: il merge della PR S1 nel branch padre chiude formalmente questo criterio. |
| 2 | Migration applica pulita (4 colonne + `audit_outbox`) | ✅ | 2026-05-25 | Migration `20260525080310_ExtendAuditLogSchema` aggiunge `before_json`/`after_json`/`impersonated_user_id`/`step_up_token_id` + tabella `audit_outbox` + indici `ix_audit_outbox_status_created_at` / `ix_audit_logs_impersonated_user_id`. Applicata via `MigrateAsync` da 45 integration test Testcontainers senza errori. |
| 3 | No regression sugli 8 handler `[AuditableAction]` | ✅ | 2026-05-25 | 67/67 test esistenti PASS (ChangeUserRole, DeleteUser, SuspendUser, UpdateUserTier, UpdateUserAiConsent + `AuditOutboxWriteIntegrationTests`). Il relax del constraint `IRequest<TResponse>`→`IBaseRequest` e lo split atomic/best-effort non introducono regressioni. |
| 4 | Azioni distruttive atomiche con audit | ✅ | 2026-05-25 | `AtomicAuditIntegrationTests` (2: commit-both + rollback-both) + Scenario 1 (delete + Pending outbox) + Scenario 4 (contrasto best-effort). `[AtomicAudit]` su `DeleteUserCommand`; `HandleAtomicAsync` avvolge mutazione+outbox in unica transazione. |
| 5 | Processor p95 < 10s sotto carico | ⚠️ **DIFFERITO** | — | **Non verificato empiricamente.** Il tool `tools/AuditOutboxLoadGen` previsto dal piano non esiste e non c'è osservazione su dev di 1 settimana. Il design (poll 5s + batch 100 = ~20 row/s sostenuto) supporta il target per il volume admin atteso, ma manca una misura p95 reale. **Follow-up raccomandato:** strumentare un benchmark synthetic o osservare i gauge in staging prima del cutover strict di S3. Non bloccante per il merge S1 (il path è funzionalmente provato dai 45 test). |
| 6 | Payload truncation (Q2) | ✅ | 2026-05-25 | Scenario 5 (60 BackupCodes → `_truncated` marker, ≤256KB, mutazione committa) + unit test `PayloadTruncator`. |
| 7 | Idempotency (Q3) | ✅ | 2026-05-25 | Scenario 3 (crash recovery, no duplicati) + `AuditOutboxIdempotencyTests`. Contratto `audit_logs.Id == audit_outbox.Id` + pre-check esistenza batch (variante EF-native dell'`ON CONFLICT`). |
| 8 | Metriche Prometheus esposte (Q3) | ✅ *(naming deviation)* | 2026-05-25 | 3 ObservableGauge registrati (`RegisterAuditOutboxGauges`) + test MeterListener. **Deviazione:** i nomi seguono la convenzione namespace del codebase `meepleai.*`, quindi l'export Prometheus produce `meepleai_audit_outbox_pending_count`, `meepleai_audit_outbox_pending_oldest_age_seconds`, `meepleai_audit_outbox_failed_count` — **non** i nomi nudi `audit_outbox_pending_count` / `audit_outbox_oldest_pending_age_seconds` indicati in Q3. **Azione Ops:** allineare le alert rule ai nomi effettivi esportati. Aggiunto un terzo gauge (`failed.count`) non previsto da Q3, utile per alert su poison-message. |

### Scoperte durante l'implementazione (da tracciare separatamente — non bloccanti)

1. **`UserRepository.UpdateAsync:205` azzera `BackupCodes`** (`Clear()` + rebuild dal dominio). Combinato con `GetByIdAsync` AsNoTracking→`MapToDomain` che non ri-popola i codici, **ogni `ChangeUserRole`/`SuspendUser` cancella i backup code 2FA dell'utente target.** Potenziale bug pre-esistente, non introdotto da S1. Verificare e aprire issue.
2. **Doppio path di scrittura `audit_logs`**: `DomainEventHandlerBase.CreateAuditLogAsync` scrive direttamente in `audit_logs` sui domain event (es. `RoleChangedEvent` → `action=DomainEvent.RoleChangedEvent`), in parallelo al path outbox→processor di S1. I due path coesistono; il read endpoint dovrà esserne consapevole (vedi follow-up "Read endpoint merge").
3. **H1 (review T3b)**: `MeepleAiDbContext.SaveChangesAsync` dispatcha i domain event via `MediatR.Publish` **dentro** `base.SaveChangesAsync`. Se la transazione atomica fa rollback, i side-effect degli event sono già avvenuti. Mitigato con `_eventCollector.Clear()` per-tentativo + vincolo documentato su `[AtomicAudit]`. Fix completo (dispatch post-commit via event outbox durevole) = follow-up.

**S1 code-complete: 2026-05-25 by @DegrassiAaron — 7/8 DoD ✅, criterio 5 differito a ops (vedi sopra).**

---

*Questo documento è il riferimento autoritativo per la DoD di S1. Citarlo dai commit messages di Task 0-4 e dal merge finale di S1.*
