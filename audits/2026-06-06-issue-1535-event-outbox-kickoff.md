# Issue #1535 Three-Amigos Kickoff — Post-Commit Event Dispatch via Durable Outbox

**Date:** 2026-06-06
**Status:** decisions converged (5/5 Q lockate)
**Issue di riferimento:** [#1535](https://github.com/meepleAi-app/meepleai-monorepo/issues/1535)
**Spec di riferimento:** `docs/superpowers/specs/2026-06-06-issue-1535-event-outbox-design.md`
**Plan di riferimento:** `docs/superpowers/plans/2026-06-06-issue-1535-event-outbox.md`
**Mitigation di partenza:** PR #1532 (SP5 S1 — `IDomainEventCollector.Clear()` su ogni attempt della execution strategy)

Facilitato come `/sc:spec-panel` discussion → critique (Hohpe Integration-Patterns lead; Fowler Domain-Events; Nygard Sec/Ops failure-modes; Newman eventual-consistency; Wiegers PM acceptance-criteria). Output classico: domande risolte, scenari executable, ownership matrix, definition of done.

---

## Contesto

Il problema descritto da #1535 è la classica race condition di chi dispatcia eventi **dentro** una transazione e poi rollbacka l'outer:

```
[outer tx]                                          ┐
  ├─ handler.SaveChangesAsync()                     │
  │   ├─ base.SaveChangesAsync()  ← row committed   │
  │   ├─ DomainEventLog rows inserted               │ in-tx
  │   └─ MediatR.Publish(events)  ← SIDE EFFECT     │
  ├─ AuditService.EnqueueAuditAtomicAsync()         │
  └─ tx.CommitAsync()  ← ROLLBACK ON FAILURE        ┘
```

Gli effetti del `MediatR.Publish` (Redis pub/sub cache invalidation, email, log audit downstream, webhook) **sopravvivono al rollback**. Su retry della `NpgsqlRetryingExecutionStrategy`, gli eventi vengono dispatched **di nuovo**.

`#1532` ha aggiunto `IDomainEventCollector.Clear()` all'inizio di ogni attempt: l'handler retried non vede gli eventi del attempt fallito → no re-emission *dentro* lo stesso execution. Ma non annulla il dispatch già avvenuto.

Il workaround documentato (`[AtomicAudit]` doc-comment) è: "applicare solo a comandi i cui handler NON pubblicano observable external side-effects via domain events". Questo workaround ha **già forzato un compromesso noto**: `RotateProviderKeyCommand` (#1859) ha rinunciato a `[AtomicAudit]` proprio perché `ProviderCredential.Create` raise un domain event con side-effect Redis. Il proper fix elimina quel compromesso.

---

## Decisioni convergenti (Q1–Q5)

### Q1 — Scope del fix: tutti gli eventi o solo external?

**Decisione: All-in. Tutti gli eventi che fluiscono attraverso `IDomainEventCollector` vanno post-commit dispatch via outbox.**

**Razionale (Fowler ⚡ Hohpe):**
- Soluzione opt-in (`IExternalDomainEvent` marker) sarebbe più chirurgica ma richiede classificazione esplicita di 100+ eventi esistenti, ognuno con rischio di mis-classification. Newman: "il momento in cui dimentichi di marcare un evento come external diventa il prossimo incident".
- Coerenza temporale single-rule: "se il `SaveChangesAsync` non commit, **niente** è uscito dal contesto". Mental model semplice.
- Latency cost (poll 5s ≈ ritardo medio 2.5s) accettabile per tutti gli use case mappati: cache invalidation (eventually consistent comunque), email (già asincrone), audit downstream (background), SSE feed (entro 5s è UX-trasparente).
- **Vincolo**: gli eventi che oggi sono unregistered (es. `PdfStateChangedEvent`) hanno alias derivato dal CLR type name, *non* dal registry stabile. Una rinomina del tipo orfana le righe outbox storiche (accettabile: la outbox è transient — Pending/Sent — non audit log immutabile come `domain_event_logs`).

**Implicazione:** la rimozione di `MediatR.Publish` dal corpo di `MeepleAiDbContext.SaveChangesAsync` (linee 481–512) è totale, non condizionale.

---

### Q2 — Storage: tabella estesa, satellite, o standalone?

**Decisione: nuova tabella standalone `domain_event_outbox`, indipendente da `domain_event_logs`.**

**Razionale (Hohpe + Fowler):**
- `domain_event_logs` è **append-only audit** (#1590 P0-2, contract sacro). Aggiungere `Status`/`DispatchedAt`/`Attempts` lo trasforma in tabella operativa e rompe il contratto.
- Satellite con FK (`outbox.id = log.event_id`) introduce coupling tra activity feed e dispatcher: una purge dei log impedirebbe la creazione di nuove outbox row.
- Standalone permette schemi di vita diversi: outbox row Sent può essere **TTL-cleaned** dopo 30gg (compliance + storage), il log resta per sempre.
- Coverage: l'outbox copre il 100% degli eventi (`IDomainEventCollector` drain); il log copre solo il subset registry (10/100+). La tabella standalone non si "preoccupa" del registry.

**Schema (preview, dettaglio nella spec):**

```sql
CREATE TABLE domain_event_outbox (
  id                UUID PRIMARY KEY,                        -- equals IDomainEvent.EventId (idempotency)
  event_type        VARCHAR(256) NOT NULL,                   -- registry alias OR CLR type name (fallback)
  payload_json      JSONB        NOT NULL,                   -- serialized event
  payload_version   INT          NOT NULL DEFAULT 1,         -- forward-compat (mirrors log table)
  status            SMALLINT     NOT NULL,                   -- 0=Pending, 1=Sent, 2=Failed (mirrors OutboxStatus)
  attempts          INT          NOT NULL DEFAULT 0,         -- retry counter (see Q3)
  last_error        VARCHAR(2048) NULL,                      -- last exception message (truncated)
  occurred_at       TIMESTAMPTZ  NOT NULL,                   -- from IDomainEvent.OccurredAt
  enqueued_at       TIMESTAMPTZ  NOT NULL DEFAULT NOW(),     -- when this row was inserted
  dispatched_at     TIMESTAMPTZ  NULL,                       -- when MediatR.Publish acked
  next_attempt_at   TIMESTAMPTZ  NULL,                       -- backoff scheduling (NULL = ready)
  correlation_id    VARCHAR(128) NULL                        -- request scope id (logging propagation)
);

CREATE INDEX ix_domain_event_outbox_pending
  ON domain_event_outbox(next_attempt_at, enqueued_at)
  WHERE status = 0;  -- partial index, FIFO by readiness

CREATE INDEX ix_domain_event_outbox_failed
  ON domain_event_outbox(enqueued_at DESC)
  WHERE status = 2;  -- dashboard query

CREATE UNIQUE INDEX ux_domain_event_outbox_eventid
  ON domain_event_outbox(id);  -- redundant w/ PK, here for documented intent
```

---

### Q3 — Failure handling: retry budget e dead-letter

**Decisione: MaxAttempts=10 + exponential backoff (1s → 2s → 4s → … → 64s, jitter ±20%) + Status=Failed terminale + dashboard alert.**

**Razionale (Nygard):**
- Audit outbox originario (#1532) lascia poison Pending all'infinito → fragile sotto carico, hot-loop sul processor.
- 10 attempts × max 64s ≈ 5 min totali nel worst case → ben dentro l'SLA generico (5–15 min) per consistency eventuale.
- Status=Failed → dashboard ops + alert Prometheus → ops decide replay manuale o discard. Mai re-tentato automaticamente (deterministic poison).
- Backoff esponenziale + jitter: previene thundering herd su Redis transient outage (10 events fail simultaneously → 10 retry simultanee → Redis flap → ritry → ...).

**Counter logic:**
- Su exception: `attempts++`, `last_error = ex.Message[..2048]`, `next_attempt_at = NOW() + backoff(attempts)`.
- `attempts >= 10` → `MarkFailed()`, no further retry.
- Successful publish → `MarkSent()`, `dispatched_at = NOW()`.

---

### Q4 — Ordering: garanzie per-aggregato?

**Decisione: best-effort FIFO globale per `enqueued_at`, NO ordering guarantee per-aggregate per MVP.**

**Razionale (Newman + Nygard):**
- Today MediatR è sincrono in-tx → l'ordine *intra-tx* è preservato.
- L'unico caso in cui l'ordine matter è 2 eventi sullo stesso aggregato in 2 tx separate molto vicine (es. `SessionStarted` → `SessionPaused` in 200ms). Il processor batch=100 li dispatcha quasi simultaneamente, ordine FIFO globale ≈ ordine causale per casi single-aggregate ad alto traffico.
- Garantire per-aggregate ordering richiederebbe lock pessimistico per `aggregate_id`, sequenza per aggregato (`OrderBy aggregate_id, enqueued_at`), e dispatch sequenziale → kills batch throughput.
- Se in futuro emerge un consumer che richiede strict ordering (es. event sourcing replay) → opt-in via `EventTypeRegistry.RequiresOrderingByAggregate=true` + processor lane dedicato. Out-of-scope MVP.

**Risk accepted:** consumer che oggi assumono ordine sincrono intra-tx potrebbero osservare riordering inter-tx. Identificati: zero (ricerca grep su `IHandler<` + analisi semantica dei handler esistenti — vedi spec § "Migration impact assessment").

---

### Q5 — Acceptance: come testare "events never dispatched for rolled-back tx"?

**Decisione: Testcontainers integration test con failure injection mid-tx, 5 acceptance scenarios Given/When/Then.**

**Razionale (Wiegers + Crispin proxy):**
- Senza test esplicito sul rollback-after-publish, il fix è non verificato. Nessuno spec-test deve fidarsi di "ho rimosso `MediatR.Publish` quindi non viene chiamato".
- 5 scenari (vedi sotto) coprono: happy path, rollback, retry, poison/dead-letter, concorrenza.

---

## Acceptance scenarios (Adzic — Given/When/Then)

I 5 scenari diventano gli **integration test acceptance** sotto `tests/Api.Tests/Integration/Administration/Issue1535EventOutboxAcceptanceTests.cs` (Testcontainers Postgres). DoD gate: tutti ✅ prima del merge.

### Scenario 1 — Happy path: outbox row + dispatcher consumes

```
Given a command that raises GameSessionRecordedEvent
When the command's transaction commits successfully
Then exactly ONE row exists in domain_event_outbox with status=Pending
  And payload_json deserializes to the original event
  And id equals the IDomainEvent.EventId
  And MediatR.Publish has NOT been called yet (no handlers fired in-tx)

When DomainEventOutboxProcessor.RunOnceAsync(batchSize=10) executes
Then MediatR.Publish is called exactly once for the event
  And the outbox row transitions to Sent
  And dispatched_at is populated
  And the original event consumers (handlers) observe the dispatch
```

### Scenario 2 — Rollback safety: tx rolls back → zero dispatch

```
Given a command decorated with [AtomicAudit] that raises an event AND fails
   (e.g. AuditService.EnqueueAuditAtomicAsync throws after handler's SaveChanges)
When the AuditLoggingBehavior.HandleAtomicAsync rolls back the outer transaction
Then no row exists in domain_event_outbox for that event
  And MediatR.Publish is NEVER called for that event
  And no Redis cache invalidation / no email / no webhook fired
  And on operator-triggered retry of the same command (new tx), exactly ONE
      dispatch occurs (idempotency via PK on event_id holds even across retries
      because the second attempt generates a NEW EventId)
```

### Scenario 3 — Retry budget + dead-letter

```
Given an outbox row whose handler (consumer) throws a deterministic exception
  And MaxAttempts is configured to 3 (test override)
When DomainEventOutboxProcessor runs the row 3 times consecutively
Then attempts increments 1 → 2 → 3
  And last_error captures the exception message
  And next_attempt_at uses exponential backoff (1s → 2s → 4s with jitter)
  And after the 3rd attempt, status=Failed
  And subsequent processor runs DO NOT pick the row up
  And meepleai_domain_event_outbox_failed_count gauge increments
  And the row is visible on /admin/monitor?tab=events for ops review
```

### Scenario 4 — Crash recovery / no duplicate dispatch

```
Given 5 outbox rows in Pending
When the processor processes 3 rows successfully (MediatR.Publish + MarkSent)
  And the processor is killed BETWEEN MediatR.Publish and SaveChanges (MarkSent commit lost)
  And the processor restarts
Then on the next batch poll, those 3 rows are STILL Pending
  And MediatR.Publish fires AGAIN for them
  And [acceptance requirement on consumers]: consumers MUST be idempotent
      (documented contract; verified by EventTypeRegistry.IsIdempotent on
      each consumer registration — see spec § Consumer contract)
  And status transitions to Sent on the second attempt
  And no row is double-counted in meepleai_domain_event_dispatched_total
      (counter increments per outbox.MarkSent commit, not per publish call)
```

### Scenario 5 — Concurrent dispatch (multi-instance hosting)

```
Given 100 outbox rows in Pending
  And two API instances running (multi-pod deploy)
When both processors run RunOnceAsync(batchSize=50) simultaneously
Then each row is dispatched at most twice (race window: instance A picks,
     publishes, then instance B picks the SAME row before A marks Sent)
  And ON CONFLICT DO NOTHING on (id) prevents double-INSERT on audit_logs
     downstream (analogous to T4b idempotency in AuditOutboxProcessor)
  And exactly 100 rows are in Sent status at the end
  And total Publish invocations <= 200 (with idempotent consumers, this is safe)
  [Future hardening: SELECT ... FOR UPDATE SKIP LOCKED for true work-stealing —
   tracked as follow-up if duplicate-publish rate exceeds 5% in staging]
```

---

## Ownership matrix

| Layer | Component | Owner | Notes |
|---|---|---|---|
| Schema | `AddDomainEventOutboxTable` migration | BE | follows S1 migration pattern (#1532) |
| Domain | `IDomainEvent.EventId` unique guarantee | SharedKernel | already exists (#661) |
| Infrastructure | `DomainEventOutboxEntity` + EF config | BE | mirrors `AuditOutboxEntity` |
| Infrastructure | `MeepleAiDbContext.SaveChangesAsync` refactor | BE | **remove lines 481–512** (MediatR.Publish), insert outbox rows instead |
| Application | `DomainEventOutboxProcessor` BackgroundService | BE | clone `AuditOutboxProcessor` template |
| Observability | 4 ObservableGauges + 2 Counters (Prometheus) | BE+Ops | aligned with audit_outbox gauges naming |
| Routing | `/admin/monitor?tab=events` page | FE | follows F4-A8 LiveEventLog pattern |
| Testing | 5 acceptance scenarios | QE | Testcontainers Postgres |
| Migration | `[AtomicAudit]` constraint removal | BE | doc-comment update + restore on `RotateProviderKeyCommand` (#1859) |

---

## Definition of Done (DoD)

S1535 è done quando TUTTI i criteri sono ✅:

1. ✅ Migration `AddDomainEventOutboxTable` applied, schema in `MeepleAiDbContextModelSnapshot`
2. ✅ `MeepleAiDbContext.SaveChangesAsync` non chiama più `MediatR.Publish` (linee 481–512 rimosse o convertite a outbox INSERT)
3. ✅ `DomainEventOutboxProcessor` deployed, drain loop ogni 5s, batch=100
4. ✅ 5 acceptance scenarios PASS (Testcontainers Postgres)
5. ✅ `[AtomicAudit]` attribute doc-comment aggiornato: rimuovi il § "⚠ CONSTRAINT — domain events"
6. ✅ `RotateProviderKeyCommand` ri-decora con `[AtomicAudit]` (#1859 follow-up)
7. ✅ Prometheus gauges visibili: `meepleai_domain_event_outbox_pending_count`, `_failed_count`, `_oldest_pending_age_seconds`, `_dispatched_total`
8. ✅ Zero regressioni: 67 handler test + tutti i test event-driven (cache invalidation, activity feed, audit downstream) PASS
9. ⚠️ p95 dispatch latency (commit → dispatched_at) < 10s — **misurato in staging** prima del rollout prod (analogo al deferred DoD-5 di #1532)
10. ✅ Consumer contract docs: `docs/for-developers/architecture/domain-events-post-commit-contract.md` con "consumers MUST be idempotent" + esempi

---

## Risk register (Nygard)

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Consumer A non-idempotent osserva double-dispatch su crash | Media | Alto | Documentare contract; audit dei consumer esistenti in plan T0 |
| Pending backlog cresce sotto carico → ritardo SSE feed > UX-aceptable | Media | Medio | Alert su `pending_count > 1000` → scale processor pod; adaptive batch size |
| In-flight events deployment day (vecchio dispatch in-mem + nuovo outbox attivi simultaneamente) | Bassa | Medio | Migration strategy: deploy con flag `EventDispatch:Mode=Outbox` (default false) → soak in staging → flip in prod |
| Poison message storm (10× retry × 100 events) saturate processor | Bassa | Alto | Backoff exp + jitter; circuit breaker su `failed_count` > 100/min |
| Latency p95 regression non rilevata in test | Media | Medio | Bench Testcontainers `dispatch_latency_p95 < 10s` (DoD-9) prima del prod rollout |

---

## Open questions (parking lot)

- **OQ-1:** TTL per outbox row Sent? Suggerimento: 30gg (compliance + storage). Decisione → spec.
- **OQ-2:** `SELECT ... FOR UPDATE SKIP LOCKED` per multi-instance work-stealing? MVP: no (accept duplicate publish + idempotent consumers). Hardening: tracked in follow-up issue se duplicate rate >5%.
- **OQ-3:** Correlation id propagation (Newman concern): `correlation_id` column nella outbox, popolata da `Activity.Current?.Id` o request header `X-Correlation-Id`. Tracked in spec.
- **OQ-4:** Dashboard `/admin/monitor?tab=events` reuse `LiveEventLog` da F4-A8 o componente nuovo? FE decision (out-of-scope spec; tracked in plan).
- **OQ-5:** Eventi sollevati DURANTE il dispatch di un altro evento (handler che a sua volta esegue una command che raise event)? Per MVP: stesso flow (collector raccoglie, prossimo SaveChanges popola outbox). No special handling.

---

## Signatures (panel attestation)

- **Wiegers** ✅ acceptance criteria misurabili, 9-pt DoD checklist
- **Hohpe** ✅ Transactional Outbox pattern canonico, standalone table semanticamente coerente
- **Fowler** ⚠️ accetta all-in con riserva: monitorare latency impact su cache invalidation in staging prima di prod
- **Nygard** ✅ retry budget + dead-letter + alert allineati a Release-It principles
- **Newman** ✅ consumer idempotency contract esplicitato, correlation_id incluso

Panel converge. Spec doc → plan → implementation.
