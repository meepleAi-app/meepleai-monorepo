# System Configuration - Flussi API

## Panoramica

Il bounded context System Configuration gestisce la configurazione runtime dell'applicazione, feature flags e gestione cache.

---

## 1. Configuration Management

### CRUD

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/admin/configurations` | `GetAllConfigsQuery` | `category?, environment?, activeOnly?, page?, pageSize?` | `[A]` |
| GET | `/admin/configurations/{id}` | `GetConfigByIdQuery` | — | `[A]` |
| GET | `/admin/configurations/key/{key}` | `GetConfigByKeyQuery` | `environment?, activeOnly?` | `[A]` |
| POST | `/admin/configurations` | `CreateConfigurationCommand` | CreateConfigurationRequest | `[A]` |
| PUT | `/admin/configurations/{id}` | `UpdateConfigValueCommand` | UpdateConfigurationRequest | `[A]` |
| DELETE | `/admin/configurations/{id}` | `DeleteConfigurationCommand` | — | `[A]` |
| PATCH | `/admin/configurations/{id}/toggle` | `ToggleConfigurationCommand` | `isActive` (query) | `[A]` |

### Utility

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/configurations/categories` | `GetConfigCategoriesQuery` | — | `[A]` |
| POST | `/admin/configurations/validate` | `ValidateConfigCommand` | `key, value, valueType` | `[A]` |

### Bulk Operations

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| POST | `/admin/configurations/bulk-update` | `BulkUpdateConfigsCommand` | BulkConfigurationUpdateRequest | `[A]` |
| GET | `/admin/configurations/export` | `ExportConfigsQuery` | `environment?, activeOnly?` | `[A]` |
| POST | `/admin/configurations/import` | `ImportConfigsCommand` | ConfigurationImportRequest | `[A]` |

### Versioning e Rollback

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/configurations/{id}/history` | `GetConfigHistoryQuery` | `limit?` | `[A]` |
| POST | `/admin/configurations/{id}/rollback/{version}` | `RollbackConfigCommand` | — | `[A]` |

### Cache

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| POST | `/admin/configurations/cache/invalidate` | `InvalidateCacheCommand` | `key?` | `[A]` |

---

## 2. Feature Flags

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/admin/feature-flags` | `GetAllFeatureFlagsQuery` | — | `[A]` |
| GET | `/admin/feature-flags/{key}` | `IsFeatureEnabledQuery` | — | `[A]` |
| POST | `/admin/feature-flags` | `CreateConfigurationCommand` | CreateFeatureFlagRequest | `[A]` |
| PUT | `/admin/feature-flags/{key}` | `UpdateFeatureFlagCommand` | FeatureFlagUpdateRequest | `[A]` |
| POST | `/admin/feature-flags/{key}/toggle` | `ToggleConfigurationCommand` | `enabled` (query) | `[A]` |
| POST | `/admin/feature-flags/{key}/tier/{tier}/enable` | `EnableFeatureForTierCommand` | — | `[A]` |
| POST | `/admin/feature-flags/{key}/tier/{tier}/disable` | `DisableFeatureForTierCommand` | — | `[A]` |

### Flusso Feature Flag per Tier

```
POST /admin/feature-flags { key: "PdfUpload", ... }
       │
       ▼ Flag creato (disabilitato di default)
       │
POST /admin/feature-flags/PdfUpload/toggle?enabled=true
       │
       ▼ Flag abilitato globalmente
       │
POST /admin/feature-flags/PdfUpload/tier/Premium/enable
       │
       ▼ Abilitato solo per tier Premium
       │
POST /admin/feature-flags/PdfUpload/tier/Free/disable
       │
       ▼ Disabilitato per tier Free
```

---

## Flusso Configuration con Versioning

```
1. Crea:     POST /admin/configurations { key, value, category }
2. Modifica: PUT /admin/configurations/{id} { newValue }
3. History:  GET /admin/configurations/{id}/history
                    │
                    ▼ [v3: "newValue", v2: "oldValue", v1: "original"]
                    │
4. Rollback: POST /admin/configurations/{id}/rollback/1
                    │
                    ▼ Valore ripristinato a "original"
                    │
5. Cache:    POST /admin/configurations/cache/invalidate?key=myKey
```

---

## Flusso Import/Export

```
GET /admin/configurations/export?environment=Production
       │
       ▼ JSON con tutte le configurazioni
       │
  Modifica offline
       │
       ▼
POST /admin/configurations/import { configurations[] }
       │
       ▼ Configurazioni aggiornate in bulk
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 409 |
| **Passati** | 409 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 2s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Config CRUD | `CreateConfigurationTests.cs`, `UpdateConfigTests.cs`, `DeleteConfigTests.cs` | Passato |
| Feature Flags | `GetAllFeatureFlagsTests.cs`, `ToggleFlagTests.cs`, `EnableForTierTests.cs` | Passato |
| Versioning/Rollback | `GetConfigHistoryTests.cs`, `RollbackConfigTests.cs` | Passato |
| Bulk Operations | `BulkUpdateConfigsTests.cs`, `ImportConfigsTests.cs`, `ExportConfigsTests.cs` | Passato |
| Cache | `InvalidateCacheTests.cs` | Passato |
| Domain Entities | FeatureFlag, SystemSetting, ConfigEntry (16 file) | Passato |
| Validators | 5 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
