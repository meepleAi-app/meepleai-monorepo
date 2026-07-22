# ADR-060: Live session persistence strategy

**Status**: Accepted
**Date**: 2026-06-09
**Implemented**: 2026-06-14 (EPIC #2097)
**Authors**: badsworm@gmail.com (Project Owner), Claude Opus 4.7 (Spec-panel facilitator)
**Related issues**: #2090 (umbrella), #2097 (implementation epic), #4750 (original schema design), session manual test 2026-06-09

## Context

Durante manual test session 2026-06-09 (Step 3-4 — polymorphic scoring + live session invariants Asse A #1896), utente ha reportato 404 navigando a `/sessions/{sessionId}` dopo aver creato una session. Indagine ha rivelato che `LiveSessionRepository` (`apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/LiveSessionRepository.cs`) usa una `ConcurrentDictionary<Guid, LiveGameSession>` **in-memory**, registrata come Singleton in DI.

L'API funziona correttamente in flow normale (POST → GET ritorna 200), ma **tutto lo stato è perso al restart del container** `meepleai-api`. Durante questa stessa test session, il restart per fixare logo legacy (PR #2057, #2065) ha causato la perdita delle live sessions di test dell'utente.

L'infrastruttura per la persistenza DB **esiste già**:
- `apps/api/src/Api/Infrastructure/Entities/GameManagement/LiveGameSessionEntity.cs` (Infrastructure entity EF-tracked)
- `apps/api/src/Api/Infrastructure/Configurations/GameManagement/LiveGameSessionEntityConfiguration.cs` (issue #4750 schema)
- 3 tabelle DB: `live_game_sessions`, `live_session_round_scores`, `live_session_turn_records` (in migration `20260608_InitialCreate`)
- Pattern DDD canonico: Domain `LiveGameSession` (Ignore<>) + Infrastructure entity (EF)

L'implementazione in-memory era plausibilmente intesa come MVP temporaneo (`Issue #4749: CQRS commands/queries for live sessions`), mai migrata a DB-backed nonostante l'infrastruttura sia pronta.

## Decision drivers

Dal questionario socratic spec-panel 2026-06-09 (Cockburn / Wiegers / Adzic / Nygard / Fowler / Hightower / Newman / Hohpe / Crispin / Gregory):

| ID | Driver | Risposta | Implicazione |
|----|--------|----------|--------------|
| D1 | Attore primario (Cockburn) | **1c** — entrambi: host single-device E multi-device sincronizzati | Richiede stato condiviso server-side |
| D2 | Behavior al restart (Adzic) | **2.4 → 2.2** (correzione tensione) — Recoverable con continuity | Richiede persistenza durevole |
| D3 | Origine "transient" (Wiegers) | Non noto, probabilmente assunzione MVP non documentata | Assunzione da rimuovere |
| D4 | Durata session × deploy freq (Nygard) | 4h (Mage Knight) — alta probabilità restart mid-session | In-memory inaccettabile |
| D5 | Scale futuro (Hightower) | Multi-instance plausibile | In-memory non scala oltre 1 istanza |
| D6 | DB tables status (Fowler) | Schema canonico (#4750), NON dead code | Persistenza già preparata |
| D7 | Intuizione iniziale (Newman) | D (Hybrid in-memory + snapshot) | Sovrascritta — incompatibile con D1+D5 |
| D8 | User validation (Gregory) | Speculazione, no test con utente reale | Adottare opzione safe by default |

**Tensions identificate e risolte**:
- D1 (multi-device) + D2.4 (offline-first) = contraddittorio → D2 chiarito come "Recoverable transparent"
- D5 (multi-instance) + D7 (Hybrid in-memory) = incompatibile → D7 sovrascritta

## Considered options

### Option A — Mantieni in-memory + accept transient

Documenta lo stato attuale come decisione consapevole. Aggiunge banner UX "session expired" al restart.

- ✅ Zero implementation work
- ✅ Zero write latency
- ❌ Incompatibile con D1 multi-device + D5 multi-instance + D4 4h session
- ❌ Schema DB resta inconsistente (tabelle vuote ma definite)
- ❌ User-facing data loss frequente in produzione

### Option B — Persisti su EF Core (existing tables) [SCELTA]

Swap `LiveSessionRepository` da `ConcurrentDictionary` a EF-backed via `LiveGameSessionEntityConfiguration` esistente.

- ✅ Compatibile con tutti i decision drivers (D1, D2, D4, D5)
- ✅ Infrastruttura pronta — solo refactor del Repository impl
- ✅ Pattern DDD canonico mantenuto (Domain entity puro, Infrastructure EF)
- ✅ Schema DB diventa coerente
- ⚠️ Write latency +5-10ms (Postgres local) — irrilevante per UX board game
- ⚠️ Lavoro stimato 1-3gg (impl swap, mapper Domain↔Infrastructure, integration tests, migration data se necessario)

### Option C — Redis cache con TTL

Hot path read/write su Redis, snapshot periodico a DB per durability.

- ✅ Latenza minore di B (1-2ms)
- ✅ Supporta multi-instance
- ❌ Aggiunge dependency su Redis (già presente per altri use case, ma comunque costo operazionale)
- ❌ Complessità: 2 storage layers + sync logic + invalidation rules
- ❌ Overkill per scale attuale (sessioni live nell'ordine di unità, non migliaia/s)

### Option D — Hybrid: in-memory live + EF snapshot periodico (intuizione iniziale utente)

In-memory primary, snapshot ogni N min su DB.

- ✅ Latenza read minima (in-memory hot)
- ⚠️ Restart può perdere fino a N min di update (window dipende da snapshot frequency)
- ❌ **Incompatibile con D5 multi-instance**: in-memory non condiviso tra istanze
- ❌ Complessità di sync + race conditions tra in-memory e DB
- ❌ **Non c'è hot read path**: il Domain aggregate `LiveGameSession` già carica tutto lo stato in-memory durante l'handler (collezioni `_players`, `_roundScores`, ecc.). Hybrid utile per workload read-heavy che bypassano DB, ma board game session ha mutating workload — ogni score update muta domain state + persiste. Caching layer aggiuntivo = secondo copy dello stesso stato, zero performance gain.

### Option E — Event sourcing

Append-only event log, ricostruisci stato session via replay.

- ✅ Audit trail completo, debugging eccellente, time-travel
- ✅ Multi-instance ready
- ❌ Complessità molto alta: snapshot strategy, replay performance, version migration
- ❌ Overkill per dominio attuale (game session non richiede audit forense)

## Decision outcome

**SCELTA: Opzione B — Persisti su EF Core (existing tables)**

Razionale:
- È **l'unica opzione coerente** con tutti i decision drivers (D1 multi-device + D2 recoverable + D4 4h session + D5 multi-instance)
- L'infrastruttura è **già preparata** (#4750 schema + EntityConfiguration esistenti + migration applicata)
- Pattern DDD canonico già adottato nel resto del BC GameManagement (es: `GameNightEventRepository`, `ShareLinkRepository`)
- Trade-off (5-10ms latency) è **irrilevante per UX** di board game (gli utenti cliccano una volta ogni N secondi, non N volte/secondo)

L'intuizione iniziale dell'utente (Opzione D Hybrid) è stata **sovrascritta** dopo identificazione conflitti tra le sue risposte. Documentato per trasparenza decisionale.

## Consequences

### Positive

- Live sessions **sopravvivono al restart container** (D4 risolto)
- Pronto per **multi-instance scaling** futuro (D5 non blocker)
- **Schema DB coerente** con il codice (D6 dead code eliminato)
- Audit trail naturale (rows DB con timestamps)
- Possibilità di query analytics (es: durata media session, tipo scoring più usato)

### Negative

- **Write latency +5-10ms** per ogni score update / state change. Mitigation: batch updates client-side dove possibile (debounce 500ms già implementato in `PolymorphicScoreEditor`).
- **Disk usage crescente** in DB. Quantificazione: row `live_game_sessions` ~2-5KB (jsonb columns dominanti). At 1000 sessions/month (generoso per scale corrente) = ~5MB/month in `live_game_sessions` + children. **Trascurabile**, rinforza "Option B is fine". Mitigation se scale esplode: retention policy (es: archivia/delete session completed da >90gg).
- **Lock contention** se molti updates concorrenti sullo stesso session. Mitigation: optimistic concurrency con `version` column (RowVersion timestamp) già pattern nel codebase (vedi CLAUDE.md § Key Data Patterns).

### Neutral

- Implementation work stimato 1-3gg. Non bloccante per Step 5-8 test plan (sessions tracking è solo Step 3-4).

## Implementation plan

### Phase 1 — Refactor LiveSessionRepository (P0, 2-3gg)

**Effort sottostimato nel draft iniziale**: il Domain aggregate `LiveGameSession` ha 7 collezioni private (`_players`, `_teams`, `_turnOrder`, `_roundScores`, `_turnRecords`, `_disputes`, `_setupChecklist`) + private setters + private parameterless constructor. Inoltre l'Infrastructure entity ha colonne jsonb (`GameStateJson`, `TurnOrderJson`, ecc.) mentre il Domain side usa tipi (`JsonDocument`, typed collections). Mapper non è banale.

- [ ] Rimuovere `ConcurrentDictionary _sessions`
- [ ] **Aggiungere factory method `LiveGameSession.Reconstitute(LiveGameSessionEntity entity)`** sul Domain aggregate (pattern DDD canonico, **match `GameNightEvent` pattern** in `GameNightEventRepository.cs` — NON usare reflection)
- [ ] Aggiungere extension method `LiveGameSessionEntity LiveGameSession.ToEntity()` per write path (serializza jsonb)
- [ ] Refactor `LiveSessionRepository` per estendere `RepositoryBase` (come `GameNightEventRepository`), NON iniettare `IUnitOfWork` separato — `RepositoryBase` integra UnitOfWork + IDomainEventCollector
- [ ] Refactor metodi `AddAsync`, `UpdateAsync`, `GetByIdAsync`, `GetByCodeAsync`, `GetActiveByUserIdAsync`, `GetAllActiveAsync` per usare DbContext via RepositoryBase
- [ ] Cambiare DI registration da `AddSingleton` a `AddScoped`
- [ ] **Mantenere** `modelBuilder.Ignore<LiveGameSession>()` — la Domain entity NON è EF tracked (solo Infrastructure entity)

### Phase 2 — Domain events transaction boundary (P0, 0.5gg)

`LiveGameSession` raise domain events (`LiveSessionScoreRecordedEvent`, `LiveSessionTurnAdvancedEvent`, ecc.) ad ogni mutation. Devono essere dispatchati nella **stessa transaction** del DB write.

- [ ] Verificare che `RepositoryBase` (esistente) dispatch domain events dopo `SaveChangesAsync` success (NON prima)
- [ ] Pattern: handler chiama `_repository.UpdateAsync(session)` → RepositoryBase fa SaveChanges → on success dispatch eventi via `IDomainEventCollector`
- [ ] Audit handler in `BoundedContexts/GameManagement/Application/Commands/LiveSessions/` per coerenza pattern
- [ ] **NON** introdurre `TransactionBehavior` pipeline (non esiste nel codebase, fuori scope)

### Phase 3 — Integration tests (P0, 0.5gg)
- [ ] `CreateLiveSessionCommandHandlerIntegrationTests`:
  - POST create → assert ritorna 200 + GUID valido
  - Query DB → record presente in `live_game_sessions`
  - GET `/api/v1/live-sessions/{id}` → 200 con stato corretto
- [ ] Test "restart-safe": setup container, create session, restart container (Testcontainers), GET → ancora 200
- [ ] Test concorrenza: 2 update simultanei stesso session → expected behavior (last-write-wins o conflict)

### Phase 4 — Observability (P2, 0.5gg)
- [ ] Metriche Prometheus:
  - `live_sessions_active_gauge` (gauge count active sessions)
  - `live_session_duration_histogram` (histogram durata session)
  - `live_session_writes_total{op="create|update|complete"}` (counter)
- [ ] Health check: `live_sessions_persistence` verifica connessione + latenza query a `live_game_sessions`

### Phase 5 — Data migration (decidere se necessario, P3)
- [ ] Se ci sono session in produzione attualmente in-memory: **non recuperabili** (data already lost on restart precedenti)
- [ ] Skip migration step, accept clean slate post-deploy
- [ ] **Graceful drain step** (optional, low risk): se desiderato, admin può chiamare `GET /api/v1/live-sessions/active` pre-deploy per identificare session attive + warning operators di completarle prima del deploy. Non blocking per il deploy stesso.

## Validation criteria (Crispin acceptance test)

- [ ] **AC-1**: Creazione session → POST `/api/v1/live-sessions` ritorna 200 + GUID. Query DB ritorna 1 record. Latency p95 < 50ms.
- [ ] **AC-2**: Restart-safe → kill container API mid-session. Restart. GET session → 200 con stato pre-restart. Score updates non persi.
- [ ] **AC-3**: Multi-instance ready → run 2 istanze API dietro nginx LB. Create session su istanza-1, fetch da istanza-2. Stato coerente.
- [ ] **AC-4**: Concurrent updates → 2 client update score simultanei → `LiveGameSessionEntityConfiguration` ha già `RowVersion` configurato (`builder.Property(e => e.RowVersion).IsRowVersion()`) → optimistic concurrency. Comportamento: prima write vince, seconda write throw `DbUpdateConcurrencyException` → HTTP 409 via existing exception middleware (rule #2568 CLAUDE.md). Documentare in handler XML doc.
- [ ] **AC-5**: Multi-update persistence sopravvive a restart → 100 score updates programmatici (no wall-clock wait) + Testcontainers PostgreSQL container restart + GET session → assert tutti 100 `RoundScores` presenti in DB. Time-elapsed irrilevante; session age verifiable via `CreatedAt` vs `UpdatedAt` confronto.

## Out of scope

- ❌ Redis caching layer (Option C) — deferred a future ADR se scale lo richiede
- ❌ Event sourcing (Option E) — deferred, complessità non giustificata ora
- ❌ Offline-first browser local storage (intuizione 2.4) — separato use case ("offline gameplay"), può venire come ADR successivo
- ❌ Live session SignalR sync multi-device real-time — separato (ADR distinto necessario per channel design)

## Open questions for follow-up

- Q1: Per le retention policy (cancellare/archiviare session completed >90gg), serve ADR separato. Tracking issue da aprire.
- Q2: SignalR push update mid-session a multi-device clients — design separato. Hub backend `/hubs/gamestate` esistente; il session-live corrente usa `useSignalrSession`. (Il prototipo orfano `useSessionSignalR`, mai cablato, è stato rimosso — vedi #564.)
- ~~Q3: Concorrenza policy~~ → **RISOLTO in AC-4**: optimistic concurrency via RowVersion → 409 on conflict (already configured in `LiveGameSessionEntityConfiguration`).

## References

- Issue #2090 (umbrella questa ADR)
- Issue #4747 (origine `modelBuilder.Ignore<LiveGameSession>` — citato nel codice ma non trovato in GitHub, possibilmente issue interna/closed)
- Issue #4749 (CQRS commands live sessions)
- Issue #4750 (Schema design tabelle live_*)
- Asse A #1896 (Live session invariants — bloccato dalla persistence)
- Pattern reference: `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Persistence/GameNightEventRepository.cs` (corretto EF-backed con UnitOfWork)
- CLAUDE.md § Key Data Patterns (Soft Delete, Audit, Concurrency RowVersion)
- Test session: `docs/for-developers/qa/2026-06-09-manual-test-main-dev.md` Finding #11

## Update 2026-06-14 — Trigger replaced with xmin

Per code-review finding I-1 of PR #2301 and follow-up issue #2305, the `clock_timestamp()::text::bytea` trigger pattern initially shipped with this ADR was replaced with the codebase-standard `xmin` system-column mapping. Same column behavior (Postgres-managed concurrency token), better collision safety (xmin is a unique transaction id per row UPDATE), zero trigger maintenance.

Implementation:
- `LiveGameSessionEntity.Xmin` (uint) replaces `RowVersion` (byte[])
- `LiveGameSessionEntityConfiguration` maps `Xmin` to Postgres `xmin xid` with `ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()`
- Migration `LiveSessionRowVersionToXmin` drops the `ef_update_row_version()` trigger, the helper function, and the legacy `row_version` column

Same migration pattern applied to `GameNightPlaylist` and `MechanicDraft` (issue #2306) which had `bytea NOT NULL row_version` without any trigger — optimistic concurrency was effectively disabled on those tables. The new xmin pattern + integration tests prove the fix.

Pattern reference: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicAnalysisEntityConfiguration.cs:101-107`.
