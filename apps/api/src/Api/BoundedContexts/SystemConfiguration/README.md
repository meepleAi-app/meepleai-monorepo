# SystemConfiguration Bounded Context

## Responsabilità

Gestisce la configurazione dinamica del sistema, feature flags, impostazioni runtime e rollback delle configurazioni.

## Funzionalità Principali

- **Dynamic Configuration**: Configurazione runtime senza redeploy
- **Feature Flags**: Abilitazione/disabilitazione feature per ambiente o utente
- **3-Tier Fallback**: Database → appsettings.json → defaults
- **Version Control**: Storico modifiche con rollback
- **Bulk Operations**: Import/export configurazioni
- **Admin UI**: Interfaccia `/admin/configuration`

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (ConfigurationSetting, FeatureFlag, ConfigurationHistory)
- **ValueObjects/**: Oggetti valore immutabili (ConfigKey, ConfigValue, ConfigVersion)
- **Services/**: Domain services per logica complessa
  - ConfigValidationService (validazione valori)
  - ConfigMigrationService (migrazioni versioni)
- **Events/**: Domain events (ConfigurationChanged, FeatureFlagToggled, ConfigRolledBack, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - UpdateConfiguration
  - ToggleFeatureFlag
  - RollbackConfiguration
  - BulkImportConfiguration
  - DeleteConfiguration
- **Queries/**: Operazioni di lettura
  - GetConfiguration
  - GetAllConfigurations
  - GetConfigurationHistory
  - GetFeatureFlags
  - GetConfigurationByCategory
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi (IConfigurationRepository)
- **Services/**: Application services per orchestrazione (ConfigurationService)

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core (ConfigurationRepository)
- **Services/**: Implementazioni concrete di servizi
  - ConfigurationProvider: 3-tier fallback logic
  - ConfigurationCache: Redis caching
- **Adapters/**: Adattatori per fonti di configurazione esterne

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati
- **Strategy Pattern**: Pluggable configuration sources

## 3-Tier Configuration Fallback (CONFIG-01-06)

```
Request Configuration Value (key)
    ↓
Tier 1: Database (ConfigurationSettings table)
    ├─ Found? → Return value (highest priority)
    └─ Not found → Tier 2
         ↓
Tier 2: appsettings.json
    ├─ Found? → Return value (medium priority)
    └─ Not found → Tier 3
         ↓
Tier 3: Default Values (hardcoded in domain)
    └─ Return default (lowest priority)
```

### Cache Strategy
- **Redis L2 Cache**: 5min TTL per database configs
- **Memory L1 Cache**: 1min TTL per appsettings
- **Cache Invalidation**: Automatic on configuration update

## Configuration Categories

### Features (Feature Flags)
```json
{
  "Features.PdfUpload": true,
  "Features.PdfIndexing": true,
  "Features.OAuth": true,
  "Features.TwoFactorAuth": true,
  "Features.AdminDashboard": true,
  "Features.BetaFeatures": false
}
```

### RateLimit
```json
{
  "RateLimit.ChatRequests": 10,           // per minute
  "RateLimit.PdfUploads": 5,              // per hour
  "RateLimit.ApiRequests": 1000,          // per hour
  "RateLimit.WindowMinutes": 1
}
```

### AI/LLM
```json
{
  "AI.Provider": "OpenRouter",
  "AI.Model.Primary": "gpt-4-turbo",
  "AI.Model.Secondary": "claude-3-sonnet",
  "AI.Timeout.Seconds": 30,
  "AI.MaxTokens": 4000,
  "AI.Temperature": 0.7
}
```

### RAG
```json
{
  "RAG.VectorWeight": 0.70,               // 70% vector, 30% keyword
  "RAG.KeywordWeight": 0.30,
  "RAG.MinConfidence": 0.70,
  "RAG.TopK": 10,
  "RAG.MaxContextChars": 8000
}
```

### PDF
```json
{
  "PDF.Extractor.Provider": "Orchestrator",
  "PDF.Quality.MinimumThreshold": 0.80,
  "PDF.MaxFileSizeBytes": 100000000,      // 100MB
  "PDF.AllowedExtensions": ".pdf"
}
```

## API Endpoints

```
GET    /api/v1/configuration               → GetAllConfigurationsQuery
GET    /api/v1/configuration/{key}         → GetConfigurationQuery
PUT    /api/v1/configuration/{key}         → UpdateConfigurationCommand
DELETE /api/v1/configuration/{key}         → DeleteConfigurationCommand
GET    /api/v1/configuration/category/{cat}→ GetConfigurationByCategoryQuery
GET    /api/v1/configuration/history/{key} → GetConfigurationHistoryQuery
POST   /api/v1/configuration/rollback/{key}/{version} → RollbackConfigurationCommand
POST   /api/v1/configuration/bulk/import   → BulkImportConfigurationCommand
GET    /api/v1/configuration/bulk/export   → BulkExportConfigurationQuery
GET    /api/v1/feature-flags               → GetFeatureFlagsQuery
PUT    /api/v1/feature-flags/{key}         → ToggleFeatureFlagCommand
```

## Database Entities

Vedi `Infrastructure/Entities/SystemConfiguration/`:
- `ConfigurationSetting`: Impostazione con key, value, category, version
- `ConfigurationHistory`: Storico modifiche per rollback
- `FeatureFlag`: Feature flag con targeting rules (user, role, environment)

## Domain Model

### ConfigurationSetting Aggregate
```
ConfigurationSetting (Aggregate Root)
├── Id (Guid)
├── Key (ConfigKey value object) - unique
├── Value (ConfigValue value object)
├── Category (string: "Features", "RateLimit", "AI", "RAG", "PDF")
├── Description (string)
├── DataType (string: "bool", "int", "string", "json")
├── Version (int) - incremented on update
├── LastModifiedBy (Guid - User ID)
├── LastModifiedAt (DateTime)
├── IsEncrypted (bool) - for sensitive values
└── ValidationRules (string - JSON schema for validation)
```

### Value Objects

**ConfigKey**:
- Format: `{Category}.{SubCategory}.{Name}` (es. "Features.PdfUpload")
- Max length: 200 characters
- Allowed: alphanumeric, `.`, `-`, `_`

**ConfigValue**:
- Max length: 10,000 characters
- Type validation: bool, int, string, JSON
- Encryption: Sensitive values encrypted at rest

## Version Control & Rollback

### History Tracking
Ogni modifica crea un record in `ConfigurationHistory`:
- Previous value
- New value
- Changed by (User ID)
- Timestamp
- Reason (optional)

### Rollback
```bash
POST /api/v1/configuration/rollback/Features.PdfUpload/3

# Ripristina la versione 3 di Features.PdfUpload
# Crea nuova versione con valore della v3
```

## Bulk Operations

### Export
```bash
GET /api/v1/configuration/bulk/export?category=Features

# Response: JSON file con tutte le configurazioni
{
  "exportedAt": "2025-11-15T10:30:00Z",
  "category": "Features",
  "configurations": [
    { "key": "Features.PdfUpload", "value": "true", ... },
    ...
  ]
}
```

### Import
```bash
POST /api/v1/configuration/bulk/import
Content-Type: application/json

{
  "configurations": [...],
  "overwrite": true,  // overwrite existing?
  "validate": true    // validate before import?
}
```

## Admin UI

### Configuration Management
- **Path**: `/admin/configuration`
- **Features**:
  - View all configurations by category
  - Edit inline with validation
  - Toggle feature flags
  - View history and rollback
  - Export/import bulk configs
  - Search and filter

### Role-Based Access
- **Admin**: Full access (read, write, rollback, delete)
- **Editor**: Read-only
- **User**: No access

## Performance Optimizations

- **HybridCache**: L1 (memory) + L2 (Redis), 5min TTL
- **Lazy Loading**: Configurations loaded on-demand
- **Bulk Caching**: All configs cached after first request
- **Cache Invalidation**: Automatic on update via domain events

## Testing

- Unit tests per domain logic, validation
- Integration tests con Testcontainers (PostgreSQL, Redis)
- E2E tests per admin UI
- Test coverage: >90%

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **HybridCache**: L1+L2 caching (Microsoft.Extensions.Caching.Hybrid)
- **ASP.NET Core Data Protection**: Encryption per valori sensibili

## Security

- **Sensitive Values**: Encrypted at rest (API keys, passwords)
- **Audit Trail**: Tutte le modifiche loggato in ConfigurationHistory
- **Role-Based Access**: Solo Admin può modificare
- **Validation**: JSON Schema validation per valori complessi

## Configuration Service (Legacy)

Il `ConfigurationService` è mantenuto come service di orchestrazione/infrastruttura per:
- 3-tier fallback logic
- Cache management
- Encryption/decryption
- Type conversion

**Non è un application service** - è un infrastructure service utilizzato dagli handlers CQRS.

## Testing

```bash
# Unit tests
dotnet test --filter "FullyQualifiedName~SystemConfiguration.Domain"
dotnet test --filter "FullyQualifiedName~SystemConfiguration.Application"

# Integration tests
dotnet test --filter "Category=Integration&FullyQualifiedName~SystemConfiguration"
```

## Migration Notes

### From appsettings.json to Database
1. Export current appsettings.json
2. Import via bulk import endpoint
3. Verify values loaded correctly
4. Update appsettings.json to remove migrated keys (keep as fallback)

### Adding New Configuration
1. Define in domain (default value)
2. Add to database via API or admin UI
3. Update validation rules if needed
4. Add to documentation

## Note di Migrazione

Questo context è stato completamente migrato alla nuova architettura DDD/CQRS. Il `ConfigurationService` è mantenuto come infrastructure/orchestration service, ma i principali use case sono gestiti tramite CQRS handlers.

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md` (Configuration section)
- `docs/06-security/environment-variables-production.md`
- `docs/02-development/configuration-management.md`
