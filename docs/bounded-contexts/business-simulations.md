# BusinessSimulations Bounded Context

**Financial ledger, cost scenarios, resource forecasts, usage analytics**

---

## Responsibilities

**Financial Ledger**:
- Income/expense transaction tracking (manual + auto-generated)
- Ledger entry CRUD with category classification
- Monthly ledger reports (scheduled job)
- Ledger summary and export (CSV)

**Cost Scenarios**:
- Agent cost estimation (per strategy, model, usage)
- Saved scenario management (create, list, delete)
- Cost-per-request and daily/monthly projections

**Resource Forecasting**:
- Resource forecast creation and management
- Growth pattern modeling (Linear, Exponential, Logarithmic, Step)
- Resource type tracking (Compute, Storage, Bandwidth, API Calls, Tokens)

**Usage Analytics**:
- Application usage statistics aggregation
- Token consumption tracking via domain events
- Infrastructure cost tracking (scheduled job)

---

## Domain Model

| Aggregate | Key Fields | Purpose |
|-----------|-----------|---------|
| **LedgerEntry** | Date, Type, Category, Amount (Money), Source, Description, Metadata | Financial transaction record |
| **CostScenario** | Name, Strategy, ModelId, MessagesPerDay, ActiveUsers, CostPerRequest, MonthlyProjection | Saved cost estimation |
| **ResourceForecast** | ResourceType, CurrentUsage, GrowthPattern, ForecastPeriodMonths | Resource projection |

**Value Objects**: Money (Amount + Currency), LedgerCategory, LedgerEntryType (Income/Expense), LedgerEntrySource (Auto/Manual), ResourceType, GrowthPattern

**Domain Events**: `LedgerEntryCreatedEvent`, `LedgerEntryUpdatedEvent`, `LedgerEntryDeletedEvent` (handled by `TokenUsageLedgerEventHandler`)

**Domain Methods**: `LedgerEntry.Create()`, `LedgerEntry.CreateAutoEntry()`, `LedgerEntry.CreateManualEntry()`, `UpdateDescription()`, `UpdateMetadata()`, `UpdateCategory()`

---

## CQRS Operations

### Commands
| Command | Handler | Description |
|---------|---------|-------------|
| `CreateManualLedgerEntryCommand` | `CreateManualLedgerEntryCommandHandler` | Create manual ledger entry |
| `UpdateLedgerEntryCommand` | `UpdateLedgerEntryCommandHandler` | Update existing entry |
| `DeleteLedgerEntryCommand` | `DeleteLedgerEntryCommandHandler` | Delete ledger entry |
| `SaveCostScenarioCommand` | `SaveCostScenarioCommandHandler` | Save cost estimation scenario |
| `DeleteCostScenarioCommand` | `DeleteCostScenarioCommandHandler` | Delete saved scenario |
| `SaveResourceForecastCommand` | `SaveResourceForecastCommandHandler` | Save resource forecast |
| `DeleteResourceForecastCommand` | `DeleteResourceForecastCommandHandler` | Delete forecast |

### Queries
| Query | Handler | Description |
|-------|---------|-------------|
| `GetLedgerEntriesQuery` | `GetLedgerEntriesQueryHandler` | List ledger entries with filters |
| `GetLedgerEntryByIdQuery` | `GetLedgerEntryByIdQueryHandler` | Get single entry |
| `GetLedgerSummaryQuery` | `GetLedgerSummaryQueryHandler` | Aggregated financial summary |
| `ExportLedgerQuery` | `ExportLedgerQueryHandler` | Export ledger to CSV |
| `GetCostScenariosQuery` | `GetCostScenariosQueryHandler` | List saved scenarios |
| `EstimateAgentCostQuery` | `EstimateAgentCostQueryHandler` | Calculate agent cost estimate |
| `GetResourceForecastsQuery` | `GetResourceForecastsQueryHandler` | List resource forecasts |
| `EstimateResourceForecastQuery` | `EstimateResourceForecastQueryHandler` | Calculate resource projection |
| `GetAppUsageStatsQuery` | `GetAppUsageStatsQueryHandler` | Application usage statistics |

---

## Scheduled Jobs

| Job | Description |
|-----|-------------|
| `InfrastructureCostTrackingJob` | Auto-generates ledger entries for infrastructure costs |
| `MonthlyLedgerReportJob` | Generates monthly financial summaries |

---

## Infrastructure

### Repositories
- `ILedgerEntryRepository` → `LedgerEntryRepository`
- `ICostScenarioRepository` → `CostScenarioRepository`
- `IResourceForecastRepository` → `ResourceForecastRepository`

### Services
- `ILedgerTrackingService` → `LedgerTrackingService` (auto-tracking via events)

### DI Registration
- `BusinessSimulationsServiceExtensions.AddBusinessSimulations()`

---

## Dependencies

- **Upstream**: Administration (token/billing data), KnowledgeBase (token usage events)
- **Downstream**: None (leaf context)
- **Events consumed**: Token usage events from KnowledgeBase → `TokenUsageLedgerEventHandler`

---

## Related Issues

- Epic #3688: Business and Simulations
- Issue #3720: Financial Ledger Data Model
- Issue #3725: Agent Cost Calculator

---

**Last Updated**: 2026-02-18
**Status**: Production
**Code**: `apps/api/src/Api/BoundedContexts/BusinessSimulations/`
