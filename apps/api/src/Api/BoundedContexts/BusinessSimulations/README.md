# BusinessSimulations Bounded Context

## Responsabilita

Gestisce il registro finanziario (ledger), gli scenari di costo per agenti AI, le previsioni di risorse infrastrutturali, i budget utente e le statistiche di utilizzo dell'applicazione.

## Funzionalita Principali

- **Financial Ledger**: Registro contabile con entrate (Income) e uscite (Expense), supporto manuale e automatico
- **Tracking Automatico**: Registrazione automatica dei costi di token LLM, pagamenti subscription e costi infrastrutturali
- **Cost Calculator**: Simulatore di costi agente AI basato su strategia RAG, modello LLM, messaggi/giorno e utenti attivi
- **Resource Forecasting**: Proiezioni a 12 mesi su risorse infrastrutturali (DB, token, cache, vector storage) con pattern di crescita configurabili
- **Export Ledger**: Esportazione delle voci contabili in formato CSV, Excel o PDF
- **App Usage Stats**: Statistiche di utilizzo applicazione (DAU/MAU, retention, adoption funnel)
- **Background Jobs**: Aggregazione giornaliera costi infrastrutturali e report mensile PDF automatico
- **Budget Utente**: Proiezione read-only dei dati tier/livello utente per scenari di costo

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (LedgerEntry, CostScenario, ResourceForecast, UserBudget)
- **ValueObjects/**: Oggetti valore immutabili (Money - importo con valuta ISO 4217)
- **Enums/**: Enumerazioni di dominio (LedgerEntryType, LedgerCategory, LedgerEntrySource, GrowthPattern, ResourceType)
- **Events/**: Domain events (TokenUsageLedgerEvent)
- **Repositories/**: Interfacce repository (ILedgerEntryRepository, ICostScenarioRepository, IResourceForecastRepository, IUserBudgetRepository)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - CreateManualLedgerEntry, UpdateLedgerEntry, DeleteLedgerEntry
  - SaveCostScenario, DeleteCostScenario
  - SaveResourceForecast, DeleteResourceForecast
- **Queries/**: Operazioni di lettura
  - GetLedgerEntries (paginazione + filtri), GetLedgerEntryById, GetLedgerSummary
  - ExportLedger (CSV/Excel/PDF)
  - EstimateAgentCost, GetCostScenarios
  - EstimateResourceForecast, GetResourceForecasts
  - GetAppUsageStats (DAU/MAU, retention, funnel)
- **DTOs/**: Data Transfer Objects (LedgerEntryDto, CostScenarioDto, ResourceForecastDto, AppUsageStatsDto)
- **Interfaces/**: Contratti per servizi (ILedgerTrackingService)
- **EventHandlers/**: Gestori di domain events (TokenUsageLedgerEventHandler)
- **Jobs/**: Background jobs Quartz.NET (InfrastructureCostTrackingJob, MonthlyLedgerReportJob)

### Infrastructure/
Implementazioni concrete e adattatori:
- **Persistence/**: Implementazioni EF Core dei repository (LedgerEntryRepository, CostScenarioRepository, ResourceForecastRepository, UserBudgetRepository)
- **Services/**: Implementazioni concrete dei servizi (LedgerTrackingService - tracking automatico token, subscription, infrastruttura)
- **DependencyInjection/**: Registrazione DI del bounded context (BusinessSimulationsServiceExtensions)

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati con interfacce nel Domain layer
- **Factory Method**: Creazione entita tramite metodi statici (`LedgerEntry.Create()`, `LedgerEntry.CreateAutoEntry()`, `LedgerEntry.CreateManualEntry()`)
- **Event-Driven**: `TokenUsageLedgerEvent` pubblicato da TokenTrackingService e gestito da `TokenUsageLedgerEventHandler`
- **Background Jobs**: Quartz.NET per aggregazione costi giornaliera (05:00 UTC) e report mensile (1 del mese, 06:00 UTC)
- **Read-Only Projection**: `UserBudget` mappato via `ToView()` sulla tabella users senza generare migrazioni

## API Endpoints

### Financial Ledger (Admin)
```
GET    /api/v1/admin/financial-ledger                → GetLedgerEntriesQuery
GET    /api/v1/admin/financial-ledger/summary         → GetLedgerSummaryQuery
GET    /api/v1/admin/financial-ledger/{id}            → GetLedgerEntryByIdQuery
POST   /api/v1/admin/financial-ledger                → CreateManualLedgerEntryCommand
PUT    /api/v1/admin/financial-ledger/{id}            → UpdateLedgerEntryCommand
DELETE /api/v1/admin/financial-ledger/{id}            → DeleteLedgerEntryCommand
GET    /api/v1/admin/financial-ledger/export          → ExportLedgerQuery
```

### Cost Calculator (Admin)
```
POST   /api/v1/admin/cost-calculator/estimate         → EstimateAgentCostQuery
POST   /api/v1/admin/cost-calculator/scenarios        → SaveCostScenarioCommand
GET    /api/v1/admin/cost-calculator/scenarios        → GetCostScenariosQuery
DELETE /api/v1/admin/cost-calculator/scenarios/{id}   → DeleteCostScenarioCommand
```

### Resource Forecast (Admin)
```
POST   /api/v1/admin/resource-forecast/estimate       → EstimateResourceForecastQuery
POST   /api/v1/admin/resource-forecast/scenarios      → SaveResourceForecastCommand
GET    /api/v1/admin/resource-forecast/scenarios      → GetResourceForecastsQuery
DELETE /api/v1/admin/resource-forecast/scenarios/{id} → DeleteResourceForecastCommand
```

### Business Stats (Admin)
```
GET    /api/v1/admin/business/usage                   → GetAppUsageStatsQuery
```

## Modelli di Dominio

### LedgerEntry Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: Date, Type (Income/Expense), Category, Amount (Money), Source (Auto/Manual), Description, Metadata, CreatedByUserId
- **Invarianti**:
  - Date non puo essere nel futuro
  - Amount deve essere maggiore di zero
  - Description max 500 caratteri, Metadata max 4000 caratteri
  - Entrate manuali devono avere CreatedByUserId
  - Solo le entrate manuali possono essere eliminate

### CostScenario Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: Name, Strategy, ModelId, MessagesPerDay, ActiveUsers, AvgTokensPerRequest, CostPerRequest, DailyProjection, MonthlyProjection, Warnings
- **Invarianti**:
  - Name obbligatorio (max 200 caratteri)
  - Strategy e ModelId obbligatori
  - MessagesPerDay e ActiveUsers non negativi

### ResourceForecast Aggregate
- **Identita**: Id (GUID)
- **Proprieta**: Name, GrowthPattern, MonthlyGrowthRate, metriche correnti (Users, DbSizeGb, DailyTokens, CacheMb, VectorEntries), coefficienti per-utente, ProjectionsJson (12 mesi), RecommendationsJson, ProjectedMonthlyCost
- **Invarianti**:
  - Name obbligatorio (max 200 caratteri)
  - MonthlyGrowthRate tra 0 e 100
  - Tutte le metriche correnti non negative
  - ProjectionsJson obbligatorio

### UserBudget (Read-Only Projection)
- **Identita**: Id (GUID)
- **Proprieta**: Tier, Level, ExperiencePoints, IsContributor
- **Note**: Mappato in sola lettura sulla tabella `users` via `ToView()`, nessuna migrazione generata

### Money Value Object
- **Proprieta**: Amount (decimal, non negativo), Currency (codice ISO 4217 a 3 lettere)
- **Factory**: `Money.Create()`, `Money.Zero()`, `Money.InEur()`

## Testing

- Unit tests per domain logic e invarianti (factory methods, validazione)
- Integration tests con Testcontainers (PostgreSQL) per repository e handlers
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence (repository pattern)
- **MediatR**: CQRS orchestration e domain events
- **FluentValidation**: Input validation per commands
- **Quartz.NET**: Scheduling background jobs (aggregazione costi, report mensile)
- **KnowledgeBase BC**: `ITokenUsageRepository` per aggregazione costi giornalieri nel job `InfrastructureCostTrackingJob`

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/03-api/board-game-ai-api-specification.md`
- Epic #3688: Business and Simulations
