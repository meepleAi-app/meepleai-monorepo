# #534 — ME-M3.2 Auto-suppression logic + re-processing queue (spec)

**Parent ADR**: ADR-051 (Mechanic Extractor IP policy) · **Depends on**: #533 (raw feedback rows) · **Followed by**: #535 (admin notification)
**BC**: SharedGameCatalog · **Date**: 2026-07-12

## Obiettivo

Un job ricorrente Quartz che, per ogni **card attiva** con feedback, aggrega le righe grezze `mechanic_card_feedback`
(#533) in contatori sulla card, e **auto-sopprime** le card che sforano soglie admin-tunable, con audit trail e
un domain event di soppressione per il downstream (review manuale / notifica admin = #535).

## Decisioni (brainstorming 2026-07-12)

1. **Branch reprocess DEFERITO** (scelta utente). L'AC «enqueue nuova analisi con `promptVersion=current+1` (se
   disponibile)» non è attuabile: `EmbeddedMechanicPromptProvider.PromptVersion` è hardcoded `v1.0.0` e non esiste
   un meccanismo current+1; ri-eseguire la stessa versione colpisce il guard di idempotenza T7 → no-op. Quindi il
   ramo «se disponibile» è falso in pratica → si esegue **solo il ramo alert**: la soppressione alza
   `MechanicCardSuppressedEvent` che segnala la review manuale. La notifica admin completa (email/Slack) è #535.
2. **System actor**: `SystemUserId = 00000000-0000-0000-0000-000000000001` (già seeded nella tabella users, già usato
   da `SessionAutoSaveBackgroundService` per lo stesso scopo). `suppressed_by` **non ha FK** → uso safe. Estratto in
   una costante condivisa `PlatformActors.System` (SharedKernel) per non duplicare il magic Guid.
3. **Reason string**: l'AC letterale `'auto_feedback'` (13 char) viola il min di dominio `Suppress(reason)` 20..500 char.
   Risolto: reason human-readable ≥20 char che **inizia con** il tag `auto_feedback`, es.
   `"auto_feedback: 5 error reports, feedback score 0.42 below 0.50 threshold"`. Il tag macchina + i valori vanno anche
   nella metadata dell'audit log.
4. **Aggregazione batch nel job** (non event-driven su submit): matcha l'AC «background job evalua feedback counts».
5. **Kill-switch**: config `MechanicCard:AutoSuppressionEnabled` (bool, default true). Se false → run skippa tutto
   (nessuna soppressione E nessun update contatori) e ritorna 0.

## Componenti

### 1. Config (SystemConfiguration, `Environment='All'` per ADR-062)
| Key | Type | Default (nel codice) | Note |
|---|---|---|---|
| `MechanicCard:ErrorReportsThreshold` | int | `5` | soglia numero error report (neg feedback) |
| `MechanicCard:FeedbackScoreThreshold` | decimal | `0.5` | soglia score positivo |
| `MechanicCard:AutoSuppressionEnabled` | bool | `true` | kill-switch |

Category `MechanicCard`. Lettura via `IConfigurationService.GetValueAsync<T>(key, default)` (HybridCache 5-min TTL);
il default nel codice si applica quando la chiave è assente.

**❗ Niente seed via migration**: `system_configurations.CreatedByUserId` ha una FK **Restrict** a `users`, e **nessun
utente è seeded dalle migration** (l'unico `InsertData` di InitialCreate è `IncidentBannerState`). Un seed a migration-time
violerebbe la FK. Inoltre nessuna riga `system_configurations` è mai stata seeded via migration nel repo → il pattern è
la **creazione a runtime** via admin config CRUD (`CreateConfigurationCommand`, che fornisce il `CreatedByUserId` dell'admin
loggato → FK valida). Quindi: l'auto-suppression usa i **default nel codice** finché un admin non tuna le soglie dal
pannello config esistente. AC-6 «admin tunable» soddisfatto dalla lettura via `IConfigurationService` + tuning runtime.

### 2. Dominio — `MechanicCard`
- **Nuovo** `ApplyFeedbackAggregates(int errorReportsCount, decimal? feedbackScore, DateTime utcNow)`:
  set `ErrorReportsCount` + `FeedbackScore` + `UpdatedAt`. Puro state update (valida `errorReportsCount >= 0`).
- **Esistente** `Suppress(Guid actorId, string reason, DateTime utcNow)` — idempotente (throw se già suppressed),
  reason 20..500, alza `MechanicCardSuppressedEvent`. **Primo consumer reale** (finora solo `analysis.Suppress`).

### 3. Repository — `IMechanicCardRepository`
- **Nuovo** `void Update(MechanicCard card)` — mirror di `MechanicAnalysisRepository.Update`:
  `MapToEntity` → `Attach` → `State=Modified` → `Property(Xmin).IsModified=false` → `CollectDomainEvents(card)`.
  (Il repo ritorna aggregati **detached** via `Reconstitute` sotto NoTracking PERF-06; senza `Update` la mutazione
  è un no-op scalare silenzioso — lezione `repo_updateasync_astracking_notracking`.)
- **Nuovo** `Task<IReadOnlyList<MechanicCardFeedbackAggregate>> GetActiveCardFeedbackAggregatesAsync(ct)` —
  GROUP BY `card_id` su `mechanic_card_feedback`, JOIN sulle sole card **attive** (query filter `!IsSuppressed`),
  ritorna `(CardId, SharedGameId, NegativeCount, PositiveCount)` per card con ≥1 feedback.
  `MechanicCardFeedbackAggregate` = record read-model.
- **Esistenti riusati**: `GetByIdIgnoringFiltersAsync` (load detached), `AddAuditLog`.

### 4. Application — CQRS command batch
- `RunMechanicCardAutoSuppressionCommand : IRequest<AutoSuppressionResult>` (marker, nessun payload).
- `AutoSuppressionResult(int Evaluated, int Suppressed)`.
- Handler:
  1. legge 3 config; se `!enabled` → log + return `(0,0)`.
  2. `GetActiveCardFeedbackAggregatesAsync`.
  3. per ogni aggregato:
     - `score = neg+pos>0 ? (decimal)pos/(pos+neg) : null`
     - load card `GetByIdIgnoringFiltersAsync` (skip se null o già suppressed — race)
     - `card.ApplyFeedbackAggregates(neg, score, utcNow)`
     - **breach** = `neg >= errThreshold && score.HasValue && score < scoreThreshold`
     - se breach: `reason` (≥20 char, prefix `auto_feedback`), `card.Suppress(PlatformActors.System, reason, utcNow)`,
       `AddAuditLog(MechanicCardAuditLog.Create(cardId, Suppressed, System, utcNow, metadata))`, `Suppressed++`
     - `repo.Update(card)` + `SaveChangesAsync` **per-card** (un conflitto xmin non rolla l'intero batch;
       catch `DbUpdateConcurrencyException` → log + continue).
  4. return `(Evaluated, Suppressed)`.
- **CQRS**: il job NON è un endpoint → può usare `IMediator.Send`. Manteniamo il command per audit/test.

### 5. Infra — Quartz
- `MechanicCardAutoSuppressionJob : IJob` `[DisallowConcurrentExecution]`, ctor `(IServiceProvider, ILogger<>)`,
  `Execute` → `CreateScope` → `IMediator.Send(new RunMechanicCardAutoSuppressionCommand())` → log summary,
  catch `Exception except OperationCanceledException` (no rethrow).
- Registrazione in `SharedGameCatalogServiceExtensions` via `RegisterMechanicCardAutoSuppressionJob(services)`:
  cron **oraria** `WithCronSchedule("0 0 * * * ?")` (non time-critical).

### 6. Osservabilità
- **Deferita** (fuori AC): counter `mechanic_card_auto_suppressed_total`. `MeepleAiMetrics` è minimale (solo il Meter)
  → aggiungere un counter richiede infra e rischia il pitfall `static_meter_test_pollution`. Per #534 basta il
  logging strutturato nel handler/job (`Evaluated`/`Suppressed`) + l'audit log. Metrica = follow-up se richiesta.

## Test (TDD, Testcontainers per handler)
- **Domain unit** `ApplyFeedbackAggregates`: setta i campi + UpdatedAt; `errorReportsCount<0` → throw.
- **Repo integration** `Update`: load detached → mutate → Update → Save persiste la colonna scalare (prova AsTracking-equiv).
- **Aggregation query**: conteggi neg/pos corretti per card; esclude card suppressed.
- **Handler integration**:
  - 5 neg / 0 pos (score 0.0), enabled → **suppressed**, reason contiene `auto_feedback`, `ErrorReportsCount=5`,
    1 audit log `Suppressed`, evento alzato.
  - 5 neg / 10 pos (score 0.667) → **non** suppressed (AND fallisce), ma contatori aggiornati.
  - 4 neg → non suppressed (sotto count threshold).
  - kill-switch `false` → run skip, 0 suppressed.
  - config override `ErrorReportsThreshold=3` → 3 neg suppressed (prova config-driven).
  - card già suppressed → esclusa dagli aggregati (query filter).
- **Job unit**: `Execute` invia il command (mock scope + IMediator), non rilancia.

## Fuori scope (deferiti)
- Auto-reprocess con prompt bump (no v2 prompt) → #534 alza solo l'evento.
- Notifica admin email/Slack completa → **#535**.
- Admin UI per tuning soglie (config già leggibile/scrivibile via SystemConfiguration admin esistente).
