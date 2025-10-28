# CONFIG-01: Dynamic Configuration System - Implementation Plan

**Issue**: #476 - Database Schema & Service for Dynamic Configuration
**Status**: In Progress
**Priority**: 🔴 High
**Effort**: L (2-3 weeks)
**Date**: 2025-10-25

## Summary

Implementing a database-backed dynamic configuration system that enables runtime configuration changes without redeployment. This foundation will unblock CONFIG-02 through CONFIG-07 (rate limiting, AI/LLM, RAG, feature flags, admin UI, testing).

## Business Value

- **Operational Flexibility**: Change configuration without code deployments or service restarts
- **Environment-Specific Configuration**: Different settings for Dev/Staging/Production
- **Audit Trail**: Track who changed what configuration and when
- **Rollback Capability**: Quickly revert problematic configuration changes
- **Administrative Control**: Non-developers can adjust system behavior via admin UI (CONFIG-06)

## Architecture Overview

### Database Schema

**Table**: `system_configurations`

| Column | Type | Purpose |
|--------|------|---------|
| `id` | string (PK) | Unique identifier |
| `key` | string (indexed) | Hierarchical key (e.g., "RateLimit:Admin:MaxTokens") |
| `value` | string (JSON) | Configuration value stored as JSON |
| `value_type` | string | Type hint ("string", "int", "bool", "json") |
| `description` | string? | Human-readable purpose |
| `category` | string (indexed) | Grouping (e.g., "RateLimit", "AI", "Cache") |
| `is_active` | bool | Whether configuration is applied |
| `requires_restart` | bool | If true, hot-reload not possible |
| `environment` | string (indexed) | Applies to which env ("All", "Production", etc.) |
| `version` | int | Incremented on each update |
| `previous_value` | string? | Last value before update (quick rollback) |
| `created_at` | datetime | When created |
| `updated_at` | datetime | Last modification |
| `created_by_user_id` | string (FK) | Creator user ID |
| `updated_by_user_id` | string? (FK) | Last updater user ID |
| `last_toggled_at` | datetime? | When active status last changed |

**Indexes**:
- Primary key on `id`
- Unique index on `(key, environment)` - one value per key per environment
- Index on `category` - for category filtering
- Index on `is_active` - for active-only queries
- Index on `environment` - for environment filtering

### Service Layer

**IConfigurationService** - Core CRUD operations with caching

Key Methods:
- `GetConfigurationsAsync` - Paginated list with filters (category, environment, activeOnly)
- `GetConfigurationByKeyAsync` - Get by hierarchical key
- `GetValueAsync<T>` - Type-safe value retrieval with defaults
- `CreateConfigurationAsync` - Add new configuration
- `UpdateConfigurationAsync` - Modify existing configuration (increments version)
- `DeleteConfigurationAsync` - Remove configuration
- `ToggleConfigurationAsync` - Enable/disable configuration
- `BulkUpdateConfigurationsAsync` - Atomic multi-configuration update
- `ValidateConfigurationAsync` - Pre-apply validation
- `ExportConfigurationsAsync` - Export to JSON for backups/migration
- `ImportConfigurationsAsync` - Import from JSON
- `GetConfigurationHistoryAsync` - View change audit trail
- `RollbackConfigurationAsync` - Revert to previous version
- `GetCategoriesAsync` - List all categories
- `InvalidateCacheAsync` - Force cache refresh

**Caching Strategy**:
- HybridCache integration (L1 in-memory + L2 Redis)
- Cache key: `config:{key}:{environment}`
- TTL: 5 minutes (configurable)
- Invalidation: On update/delete/toggle operations
- Tag-based invalidation: `config:category:{category}`

### API Endpoints

**Admin-Only** (requires `Role = "Admin"`)

```
GET    /api/v1/admin/configurations?category={category}&environment={env}&activeOnly={bool}&page={page}&pageSize={size}
GET    /api/v1/admin/configurations/{id}
GET    /api/v1/admin/configurations/key/{key}?environment={env}
POST   /api/v1/admin/configurations
PUT    /api/v1/admin/configurations/{id}
DELETE /api/v1/admin/configurations/{id}
PATCH  /api/v1/admin/configurations/{id}/toggle
POST   /api/v1/admin/configurations/bulk-update
POST   /api/v1/admin/configurations/validate
GET    /api/v1/admin/configurations/export?environment={env}&activeOnly={bool}
POST   /api/v1/admin/configurations/import
GET    /api/v1/admin/configurations/{id}/history
POST   /api/v1/admin/configurations/{id}/rollback/{version}
GET    /api/v1/admin/configurations/categories
POST   /api/v1/admin/configurations/cache/invalidate
```

## Implementation Progress

### ✅ Completed

1. **Database Entity** (`SystemConfigurationEntity.cs`)
   - Comprehensive schema with versioning
   - Audit trail (created/updated by, timestamps)
   - Environment-specific configuration support
   - Previous value tracking for quick rollback

2. **DTOs and Models** (`Contracts.cs`)
   - `SystemConfigurationDto` - Full configuration representation
   - `CreateConfigurationRequest` - Creation with validation
   - `UpdateConfigurationRequest` - Partial updates
   - `ConfigurationHistoryDto` - Change audit entries
   - `BulkConfigurationUpdateRequest` - Batch operations
   - `ConfigurationValidationResult` - Validation feedback
   - `ConfigurationExportDto` - Export format
   - `ConfigurationImportRequest` - Import format

3. **Service Interface** (`IConfigurationService.cs`)
   - Complete method signatures
   - XML documentation
   - Type-safe value retrieval
   - Bulk operations support
   - Import/export capabilities
   - History and rollback support

### 🔄 In Progress

4. **ConfigurationService Implementation**
   - EF Core CRUD operations
   - HybridCache integration
   - Validation logic
   - History tracking
   - Rollback implementation

### 📋 Remaining Tasks

5. **DbContext Updates**
   - Add `DbSet<SystemConfigurationEntity>` to `MeepleAiDbContext`
   - Configure entity relationships (CreatedBy, UpdatedBy)
   - Add indexes for performance

6. **EF Core Migration**
   - Create migration: `AddSystemConfigurationsTable`
   - Seed default configurations from appsettings.json
   - Test migration up/down

7. **API Endpoints** (`Program.cs`)
   - Register service in DI container
   - Map all 13 admin endpoints
   - Add authorization policies (Admin-only)
   - OpenAPI documentation

8. **Unit Tests** (`ConfigurationServiceTests.cs`)
   - CRUD operations with SQLite in-memory
   - Caching behavior verification
   - Validation logic tests
   - Bulk update scenarios
   - History and rollback tests
   - Target: 100% code coverage

9. **Integration Tests** (`ConfigurationEndpointsTests.cs`)
   - End-to-end HTTP tests with Testcontainers
   - Authorization validation (Admin-only enforcement)
   - Concurrent update handling
   - Export/import workflows
   - Cache invalidation verification
   - Target: 20+ test scenarios

10. **Documentation**
    - Implementation summary document
    - API usage examples
    - Migration guide for existing configuration
    - Troubleshooting guide

## Dependencies

**Blocks**:
- #472 CONFIG-02 (Dynamic rate limiting)
- #474 CONFIG-03 (Dynamic AI/LLM config)
- #475 CONFIG-04 (Dynamic RAG config)
- #473 CONFIG-05 (Feature flags)
- #477 CONFIG-06 (Frontend admin UI)
- #478 CONFIG-07 (Testing & migration)

**Depends On**:
- ✅ HybridCache infrastructure (PERF-05) - Already implemented
- ✅ Admin user management (ADMIN-01) - Already implemented
- ✅ EF Core migrations - Existing infrastructure

## Migration Strategy

### Phase 1: Database Foundation (Week 1)
1. Create entity and DTOs ✅
2. Implement service interface ✅
3. Implement ConfigurationService
4. Update DbContext
5. Create and test EF Core migration

### Phase 2: API Integration (Week 2)
6. Add API endpoints to Program.cs
7. Configure DI and authorization
8. Write unit tests
9. Write integration tests

### Phase 3: Configuration Migration (Week 3)
10. Script to import appsettings.json → database
11. Migration guide documentation
12. Gradual rollout with feature flag
13. Performance testing and tuning

## Testing Strategy

### Unit Tests (ConfigurationServiceTests.cs)
- **CRUD Operations**: Create, read, update, delete
- **Filtering**: By category, environment, active status
- **Type-Safe Retrieval**: GetValueAsync<T> with various types
- **Caching**: Hit/miss scenarios, invalidation
- **Validation**: Type validation, required fields, format checks
- **Bulk Updates**: Atomic operations, partial failures
- **History**: Version tracking, audit trail
- **Rollback**: Version restoration, previous value handling
- **Edge Cases**: Null handling, non-existent keys, duplicate keys

### Integration Tests (ConfigurationEndpointsTests.cs)
- **Authentication**: Admin-only enforcement, 401/403 responses
- **CRUD Workflows**: Full lifecycle via HTTP
- **Pagination**: Large result sets, page boundaries
- **Filtering**: Combined filters (category + environment + activeOnly)
- **Concurrency**: Multiple simultaneous updates
- **Export/Import**: Round-trip consistency
- **Cache Behavior**: Invalidation propagation, TTL expiration
- **Error Handling**: Validation errors, not found, conflicts

### BDD Scenarios

**Feature**: Dynamic Configuration Management
**As a** system administrator
**I want to** manage configuration dynamically via API
**So that** I can tune system behavior without redeployments

**Scenario 1**: Create new configuration
```gherkin
Given I am authenticated as an admin
When I POST /api/v1/admin/configurations with valid data
Then the response status is 201 Created
And the configuration is stored in the database
And the cache is populated
```

**Scenario 2**: Update configuration with versioning
```gherkin
Given a configuration exists with version 1
When I PUT /api/v1/admin/configurations/{id} with new value
Then the version increments to 2
And the previous_value field stores the old value
And the cache is invalidated
```

**Scenario 3**: Rollback to previous version
```gherkin
Given a configuration with version 3 and previous_value stored
When I POST /api/v1/admin/configurations/{id}/rollback/2
Then the current value is restored from history
And the version is incremented to 4
```

## Configuration Examples

### Rate Limiting (CONFIG-02)
```json
{
  "key": "RateLimit:Admin:MaxTokens",
  "value": "1000",
  "valueType": "int",
  "description": "Maximum token bucket capacity for admin users",
  "category": "RateLimit",
  "isActive": true,
  "requiresRestart": false,
  "environment": "Production"
}
```

### AI/LLM (CONFIG-03)
```json
{
  "key": "AI:Temperature",
  "value": "0.7",
  "valueType": "double",
  "description": "LLM temperature for response generation",
  "category": "AI",
  "isActive": true,
  "requiresRestart": false,
  "environment": "All"
}
```

### RAG (CONFIG-04)
```json
{
  "key": "RAG:TopK",
  "value": "10",
  "valueType": "int",
  "description": "Number of top results to retrieve from vector search",
  "category": "RAG",
  "isActive": true,
  "requiresRestart": false,
  "environment": "All"
}
```

### Feature Flags (CONFIG-05)
```json
{
  "key": "FeatureFlags:StreamingResponses",
  "value": "true",
  "valueType": "bool",
  "description": "Enable SSE streaming for QA responses",
  "category": "FeatureFlags",
  "isActive": true,
  "requiresRestart": false,
  "environment": "All"
}
```

## Security Considerations

- **Admin-Only Access**: All configuration endpoints require `Role = "Admin"`
- **Audit Trail**: Every change tracked with user ID and timestamp
- **Validation**: Type-safe validation before persisting configuration
- **Sensitive Data**: Avoid storing secrets in configurations (use Azure Key Vault, etc.)
- **Environment Isolation**: Production configurations separated from Development

## Performance Optimization

- **Caching**: HybridCache L1+L2 reduces database load
- **Indexes**: Efficient filtering on category, environment, is_active
- **Pagination**: Large configuration lists don't overwhelm clients
- **Bulk Updates**: Single transaction for multiple changes
- **Lazy Loading**: Configurations loaded on-demand, not at startup

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| Configuration corruption | High | Validation before persist, rollback capability, audit trail |
| Cache inconsistency | Medium | Tag-based invalidation, short TTL (5 min) |
| Performance degradation | Medium | Caching layer, indexes, pagination |
| Unauthorized access | High | Admin-only endpoints, audit logging |
| Migration complexity | Medium | Gradual rollout, feature flag, thorough testing |

## Success Criteria

- ✅ Database schema deployed and migrated
- ✅ ConfigurationService fully implemented
- ✅ All 13 API endpoints functional
- ✅ 100% unit test coverage
- ✅ 20+ integration test scenarios passing
- ✅ Performance: <50ms p95 latency for GetValueAsync
- ✅ Caching: >90% cache hit rate in steady state
- ✅ Documentation: Implementation guide, API docs, migration guide

## Timeline

- **Week 1 (Oct 25-31)**: Database foundation ✅ Entity, DTOs, Interface complete
- **Week 2 (Nov 1-7)**: Service implementation, API endpoints, tests
- **Week 3 (Nov 8-14)**: Migration tooling, documentation, performance tuning

**Target Completion**: November 14, 2025

---

**Generated with Claude Code**
**Co-Authored-By**: Claude <noreply@anthropic.com>
