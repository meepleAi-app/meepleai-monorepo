# Issue #002: Migrate ConfigurationService to CQRS Pattern

**Priority**: 🔴 CRITICAL
**Effort**: 50-60 hours
**Impact**: ⭐⭐⭐ HIGH
**Category**: Architecture Alignment
**Status**: Not Started

---

## Problem Description

`ConfigurationService.cs` is a **805 LOC service** with **17 public methods** that violates the Single Responsibility Principle and is inconsistent with the project's DDD/CQRS architecture (224 handlers already operational).

**Current Location**: `/home/user/meepleai-monorepo/apps/api/src/Api/Services/ConfigurationService.cs:805`

### Current Responsibilities (Too Many!)

1. **Configuration CRUD** (5 methods)
   - GetConfigurationsAsync, GetConfigurationByIdAsync, CreateConfigurationAsync, UpdateConfigurationAsync, DeleteConfigurationAsync

2. **Configuration Retrieval** (2 methods)
   - GetConfigurationByKeyAsync, GetValueAsync

3. **Validation Logic** (1 method)
   - ValidateConfiguration

4. **Version Management** (2 methods)
   - GetConfigurationHistoryAsync, RollbackConfigurationAsync

5. **Bulk Operations** (1 method)
   - BulkUpdateConfigurationsAsync

6. **Import/Export** (2 methods)
   - ExportConfigurationsAsync, ImportConfigurationsAsync

7. **Feature Flag Management** (1 method)
   - ToggleConfigurationAsync

8. **Category Management** (1 method)
   - GetCategoriesAsync

9. **Infrastructure Concerns**
   - Cache invalidation
   - Database transactions
   - Audit logging

---

## Architecture Issues

### SRP Violation
17 public methods serving 8+ different responsibilities makes the class difficult to:
- Test in isolation
- Maintain and extend
- Understand and navigate
- Mock for unit tests

### Feature Envy
Tightly coupled with:
- `IHybridCacheService` - caching concerns
- `MeepleAiDbContext` - data access concerns
- `ILogger<ConfigurationService>` - logging concerns

### Inconsistent with DDD/CQRS
- **All other bounded contexts** use CQRS pattern (224 handlers)
- ConfigurationService bypasses MediatR pipeline
- Missing cross-cutting concerns (audit, validation, authorization)
- No domain events for configuration changes

---

## Proposed Solution

Migrate to **CQRS pattern** aligned with SystemConfiguration bounded context:

```
apps/api/src/Api/BoundedContexts/SystemConfiguration/
├── Domain/
│   ├── Entities/
│   │   └── SystemConfiguration.cs (already exists)
│   ├── ValueObjects/
│   │   └── ConfigKey.cs (already exists)
│   └── Services/
│       ├── ConfigurationValidator.cs (NEW ~100 LOC)
│       └── ConfigurationVersionManager.cs (NEW ~150 LOC)
│
├── Application/
│   ├── Commands/
│   │   ├── CreateConfigurationCommand.cs
│   │   ├── UpdateConfigurationCommand.cs
│   │   ├── DeleteConfigurationCommand.cs
│   │   ├── ToggleConfigurationCommand.cs
│   │   ├── RollbackConfigurationCommand.cs
│   │   ├── BulkUpdateConfigurationsCommand.cs
│   │   ├── ImportConfigurationsCommand.cs
│   │   └── (handlers for each)
│   │
│   └── Queries/
│       ├── GetConfigurationByIdQuery.cs
│       ├── GetConfigurationByKeyQuery.cs
│       ├── GetConfigurationHistoryQuery.cs
│       ├── ListConfigurationsQuery.cs
│       ├── ExportConfigurationsQuery.cs
│       ├── GetCategoriesQuery.cs
│       └── (handlers for each)
│
└── Infrastructure/
    └── Persistence/
        └── ConfigurationRepository.cs (already exists)
```

**Reduce ConfigurationService** to ~300 LOC (infrastructure only):
- Keep: Repository access, caching orchestration, persistence operations
- Remove: All business logic → move to handlers/domain services

---

## Acceptance Criteria

- [ ] **10 command handlers** created and tested
- [ ] **6 query handlers** created and tested
- [ ] **ConfigurationValidator** domain service extracted (~100 LOC)
- [ ] **ConfigurationVersionManager** domain service extracted (~150 LOC)
- [ ] ConfigurationService reduced to ≤300 LOC (infrastructure only)
- [ ] All existing endpoints use IMediator.Send() instead of ConfigurationService
- [ ] All existing tests pass (with handler mocks)
- [ ] No breaking changes to API contracts
- [ ] Domain events added for configuration changes
- [ ] Audit logging works via domain event handlers
- [ ] Cache invalidation works correctly
- [ ] Integration tests added for all handlers

---

## Implementation Plan

### Phase 1: Domain Services Extraction (10 hours)

#### 1.1 Create ConfigurationValidator (~100 LOC)

**File**: `BoundedContexts/SystemConfiguration/Domain/Services/ConfigurationValidator.cs`

**Extract from ConfigurationService.ValidateConfiguration**:
```csharp
namespace Api.BoundedContexts.SystemConfiguration.Domain.Services;

public class ConfigurationValidator
{
    public ValidationResult Validate(SystemConfiguration config)
    {
        var errors = new List<string>();

        // Key validation
        if (string.IsNullOrWhiteSpace(config.Key))
            errors.Add("Configuration key is required");

        // Value validation
        if (string.IsNullOrWhiteSpace(config.Value))
            errors.Add("Configuration value is required");

        // Category validation
        if (!IsValidCategory(config.Category))
            errors.Add($"Invalid category: {config.Category}");

        // Environment validation
        if (!IsValidEnvironment(config.Environment))
            errors.Add($"Invalid environment: {config.Environment}");

        // Value format validation (JSON, number, etc.)
        if (!ValidateValueFormat(config))
            errors.Add("Invalid value format for configuration type");

        return errors.Any()
            ? ValidationResult.Failure(errors)
            : ValidationResult.Success();
    }

    private bool IsValidCategory(string category) { ... }
    private bool IsValidEnvironment(string environment) { ... }
    private bool ValidateValueFormat(SystemConfiguration config) { ... }
}
```

**Testing**:
- Unit tests for each validation rule
- Test edge cases (null, empty, invalid formats)
- Test valid configurations pass

---

#### 1.2 Create ConfigurationVersionManager (~150 LOC)

**File**: `BoundedContexts/SystemConfiguration/Domain/Services/ConfigurationVersionManager.cs`

**Extract from ConfigurationService** (GetConfigurationHistory, RollbackConfiguration):
```csharp
namespace Api.BoundedContexts.SystemConfiguration.Domain.Services;

public class ConfigurationVersionManager
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public async Task<ConfigurationHistory> CreateHistoryEntry(
        SystemConfiguration config,
        string changeType,
        Guid userId)
    {
        var history = new ConfigurationHistory
        {
            ConfigurationId = config.Id,
            Key = config.Key,
            Value = config.Value,
            PreviousValue = config.PreviousValue,
            ChangeType = changeType,
            ChangedBy = userId,
            ChangedAt = _timeProvider.GetUtcNow()
        };

        _dbContext.ConfigurationHistory.Add(history);
        return history;
    }

    public async Task<List<ConfigurationHistory>> GetHistory(string key)
    {
        return await _dbContext.ConfigurationHistory
            .Where(h => h.Key == key)
            .OrderByDescending(h => h.ChangedAt)
            .ToListAsync();
    }

    public async Task<SystemConfiguration> Rollback(
        string key,
        Guid versionId)
    {
        var history = await _dbContext.ConfigurationHistory
            .FindAsync(versionId);

        if (history == null)
            throw new NotFoundException($"Version {versionId} not found");

        var config = await _dbContext.SystemConfigurations
            .FirstOrDefaultAsync(c => c.Key == key);

        if (config == null)
            throw new NotFoundException($"Configuration {key} not found");

        // Rollback to previous value
        config.Value = history.Value;
        config.UpdatedAt = _timeProvider.GetUtcNow();

        return config;
    }
}
```

**Testing**:
- Test history entry creation
- Test rollback functionality
- Test version retrieval and ordering

---

### Phase 2: Query Handlers (12 hours)

#### 2.1 GetConfigurationByIdQueryHandler

**Files**:
- `Application/Queries/GetConfigurationByIdQuery.cs`
- `Application/Handlers/GetConfigurationByIdQueryHandler.cs`

```csharp
// Query
public record GetConfigurationByIdQuery(Guid Id) : IRequest<SystemConfigurationDto>;

// Handler
public class GetConfigurationByIdQueryHandler
    : IRequestHandler<GetConfigurationByIdQuery, SystemConfigurationDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMapper _mapper;

    public async Task<SystemConfigurationDto> Handle(
        GetConfigurationByIdQuery request,
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigurations
            .Include(c => c.CreatedBy)
            .Include(c => c.UpdatedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (config == null)
            throw new NotFoundException($"Configuration {request.Id} not found");

        return _mapper.Map<SystemConfigurationDto>(config);
    }
}
```

**Testing**:
- Test valid configuration retrieval
- Test not found scenario
- Test mapping to DTO

---

#### 2.2 GetConfigurationByKeyQueryHandler

**Files**:
- `Application/Queries/GetConfigurationByKeyQuery.cs`
- `Application/Handlers/GetConfigurationByKeyQueryHandler.cs`

```csharp
public record GetConfigurationByKeyQuery(
    string Key,
    string? Environment = null
) : IRequest<SystemConfigurationDto?>;

public class GetConfigurationByKeyQueryHandler
    : IRequestHandler<GetConfigurationByKeyQuery, SystemConfigurationDto?>
{
    private readonly IHybridCacheService _cache;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IMapper _mapper;

    public async Task<SystemConfigurationDto?> Handle(
        GetConfigurationByKeyQuery request,
        CancellationToken cancellationToken)
    {
        // Check cache first
        var cacheKey = $"config:{request.Key}:{request.Environment}";
        var cached = await _cache.GetAsync<SystemConfigurationDto>(cacheKey);
        if (cached != null) return cached;

        // Query database
        var query = _dbContext.SystemConfigurations
            .AsNoTracking()
            .Where(c => c.Key == request.Key && c.IsActive);

        if (!string.IsNullOrEmpty(request.Environment))
            query = query.Where(c => c.Environment == request.Environment);

        var config = await query.FirstOrDefaultAsync(cancellationToken);
        if (config == null) return null;

        var dto = _mapper.Map<SystemConfigurationDto>(config);

        // Cache result
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));

        return dto;
    }
}
```

**Testing**:
- Test cache hit
- Test cache miss
- Test environment filtering
- Test not found returns null

---

#### 2.3-2.6 Additional Query Handlers

Create handlers for:
- `GetConfigurationHistoryQueryHandler` - wraps ConfigurationVersionManager.GetHistory
- `ListConfigurationsQueryHandler` - with pagination, filtering
- `ExportConfigurationsQueryHandler` - export to JSON/CSV
- `GetCategoriesQueryHandler` - list all categories

**Testing for each**:
- Unit tests for handler logic
- Integration tests with database
- Test caching behavior
- Test pagination and filtering

---

### Phase 3: Command Handlers (18 hours)

#### 3.1 CreateConfigurationCommandHandler

**Files**:
- `Application/Commands/CreateConfigurationCommand.cs`
- `Application/Handlers/CreateConfigurationCommandHandler.cs`

```csharp
public record CreateConfigurationCommand(
    string Key,
    string Value,
    string Category,
    string? Environment,
    string? Description
) : IRequest<SystemConfigurationDto>;

public class CreateConfigurationCommandHandler
    : IRequestHandler<CreateConfigurationCommand, SystemConfigurationDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ConfigurationValidator _validator;
    private readonly IHybridCacheService _cache;
    private readonly IMapper _mapper;

    public async Task<SystemConfigurationDto> Handle(
        CreateConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        // Create entity
        var config = new SystemConfiguration
        {
            Id = Guid.NewGuid(),
            Key = request.Key,
            Value = request.Value,
            Category = request.Category,
            Environment = request.Environment,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        // Validate
        var validation = _validator.Validate(config);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Check for duplicates
        var exists = await _dbContext.SystemConfigurations
            .AnyAsync(c => c.Key == request.Key
                && c.Environment == request.Environment,
                cancellationToken);

        if (exists)
            throw new ConflictException($"Configuration {request.Key} already exists");

        // Save
        _dbContext.SystemConfigurations.Add(config);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync($"config:{request.Key}:{request.Environment}");

        // Raise domain event
        config.AddDomainEvent(new ConfigurationCreatedEvent(config.Id, config.Key));

        return _mapper.Map<SystemConfigurationDto>(config);
    }
}
```

**Testing**:
- Test successful creation
- Test validation failures
- Test duplicate key detection
- Test cache invalidation
- Test domain event raised

---

#### 3.2 UpdateConfigurationCommandHandler

```csharp
public record UpdateConfigurationCommand(
    Guid Id,
    string Value,
    string? Description
) : IRequest<SystemConfigurationDto>;

public class UpdateConfigurationCommandHandler
    : IRequestHandler<UpdateConfigurationCommand, SystemConfigurationDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ConfigurationValidator _validator;
    private readonly ConfigurationVersionManager _versionManager;
    private readonly IHybridCacheService _cache;
    private readonly IMapper _mapper;

    public async Task<SystemConfigurationDto> Handle(
        UpdateConfigurationCommand request,
        CancellationToken cancellationToken)
    {
        var config = await _dbContext.SystemConfigurations
            .FindAsync(request.Id);

        if (config == null)
            throw new NotFoundException($"Configuration {request.Id} not found");

        // Store previous value for history
        var previousValue = config.Value;

        // Update
        config.Value = request.Value;
        config.Description = request.Description ?? config.Description;
        config.UpdatedAt = DateTime.UtcNow;

        // Validate
        var validation = _validator.Validate(config);
        if (!validation.IsValid)
            throw new ValidationException(validation.Errors);

        // Create history entry
        await _versionManager.CreateHistoryEntry(
            config, "Update", currentUserId);

        // Save
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate cache
        await _cache.RemoveAsync($"config:{config.Key}:{config.Environment}");

        // Raise domain event
        config.AddDomainEvent(new ConfigurationUpdatedEvent(
            config.Id, config.Key, previousValue, config.Value));

        return _mapper.Map<SystemConfigurationDto>(config);
    }
}
```

**Testing**:
- Test successful update
- Test not found scenario
- Test validation on update
- Test history entry creation
- Test cache invalidation
- Test domain event with old/new values

---

#### 3.3-3.7 Additional Command Handlers

Create handlers for:
- `DeleteConfigurationCommandHandler` - soft delete or hard delete
- `ToggleConfigurationCommandHandler` - toggle IsActive flag
- `RollbackConfigurationCommandHandler` - wraps ConfigurationVersionManager.Rollback
- `BulkUpdateConfigurationsCommandHandler` - batch updates
- `ImportConfigurationsCommandHandler` - import from JSON/CSV

**Testing for each**:
- Unit tests for command logic
- Integration tests with database
- Test transaction rollback on errors
- Test cache invalidation
- Test domain events

---

### Phase 4: Domain Events (8 hours)

#### 4.1 Create Domain Events

**Files**:
- `Domain/Events/ConfigurationCreatedEvent.cs`
- `Domain/Events/ConfigurationUpdatedEvent.cs`
- `Domain/Events/ConfigurationDeletedEvent.cs`
- `Domain/Events/ConfigurationToggledEvent.cs`

```csharp
public record ConfigurationCreatedEvent(
    Guid ConfigurationId,
    string Key
) : IDomainEvent;

public record ConfigurationUpdatedEvent(
    Guid ConfigurationId,
    string Key,
    string OldValue,
    string NewValue
) : IDomainEvent;
```

---

#### 4.2 Create Event Handlers

**Files**:
- `Application/EventHandlers/ConfigurationCreatedEventHandler.cs`
- `Application/EventHandlers/ConfigurationUpdatedEventHandler.cs`

```csharp
public class ConfigurationUpdatedEventHandler
    : INotificationHandler<ConfigurationUpdatedEvent>
{
    private readonly IAuditService _auditService;
    private readonly ILogger<ConfigurationUpdatedEventHandler> _logger;

    public async Task Handle(
        ConfigurationUpdatedEvent notification,
        CancellationToken cancellationToken)
    {
        // Audit log
        await _auditService.LogAsync(
            "Configuration",
            notification.ConfigurationId,
            "Updated",
            new {
                Key = notification.Key,
                OldValue = notification.OldValue,
                NewValue = notification.NewValue
            });

        // Log
        _logger.LogInformation(
            "Configuration {Key} updated from {OldValue} to {NewValue}",
            notification.Key, notification.OldValue, notification.NewValue);

        // Could trigger other side effects:
        // - Send notification to admins
        // - Invalidate dependent caches
        // - Trigger workflow integration
    }
}
```

**Testing**:
- Test audit log creation
- Test event handler invocation
- Test multiple handlers for same event

---

### Phase 5: Update Endpoints (6 hours)

Update `ConfigurationEndpoints.cs` (from Issue #001) to use MediatR:

```csharp
// Before
group.MapGet("/", async (IConfigurationService service) =>
{
    var configs = await service.GetConfigurationsAsync();
    return Results.Ok(configs);
});

// After
group.MapGet("/", async (
    IMediator mediator,
    string? category,
    string? environment,
    bool activeOnly = true,
    int page = 1,
    int pageSize = 50) =>
{
    var query = new ListConfigurationsQuery(
        category, environment, activeOnly, page, pageSize);

    var result = await mediator.Send(query);
    return Results.Ok(result);
});
```

**Update all 14 configuration endpoints**:
- GET /admin/configuration → ListConfigurationsQuery
- GET /admin/configuration/{id} → GetConfigurationByIdQuery
- POST /admin/configuration → CreateConfigurationCommand
- PUT /admin/configuration/{id} → UpdateConfigurationCommand
- DELETE /admin/configuration/{id} → DeleteConfigurationCommand
- POST /admin/configuration/{id}/toggle → ToggleConfigurationCommand
- POST /admin/configuration/bulk → BulkUpdateConfigurationsCommand
- GET /admin/configuration/history/{key} → GetConfigurationHistoryQuery
- POST /admin/configuration/rollback/{key} → RollbackConfigurationCommand
- POST /admin/configuration/export → ExportConfigurationsQuery
- POST /admin/configuration/import → ImportConfigurationsCommand
- GET /admin/configuration/categories → GetCategoriesQuery
- POST /admin/configuration/validate → (inline validation)

**Testing**:
- Integration tests for all endpoints
- Test MediatR pipeline behaviors (logging, validation)
- Test error responses

---

### Phase 6: Reduce ConfigurationService (4 hours)

**Goal**: Reduce from 805 LOC to ~300 LOC

**Keep in ConfigurationService** (infrastructure only):
```csharp
public class ConfigurationService : IConfigurationService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<ConfigurationService> _logger;

    // Only infrastructure methods
    public async Task<T?> GetValueAsync<T>(string key, string? environment = null)
    {
        // Simple cache + DB lookup, no business logic
        // Delegates to GetConfigurationByKeyQuery via mediator
    }

    public async Task InvalidateCacheAsync(string key, string? environment = null)
    {
        // Simple cache invalidation
    }

    // Remove all other methods - they're in handlers now!
}
```

**Delete from ConfigurationService**:
- ❌ GetConfigurationsAsync → ListConfigurationsQueryHandler
- ❌ GetConfigurationByIdAsync → GetConfigurationByIdQueryHandler
- ❌ CreateConfigurationAsync → CreateConfigurationCommandHandler
- ❌ UpdateConfigurationAsync → UpdateConfigurationCommandHandler
- ❌ DeleteConfigurationAsync → DeleteConfigurationCommandHandler
- ❌ ToggleConfigurationAsync → ToggleConfigurationCommandHandler
- ❌ BulkUpdateConfigurationsAsync → BulkUpdateConfigurationsCommandHandler
- ❌ GetConfigurationHistoryAsync → GetConfigurationHistoryQueryHandler
- ❌ RollbackConfigurationAsync → RollbackConfigurationCommandHandler
- ❌ ExportConfigurationsAsync → ExportConfigurationsQueryHandler
- ❌ ImportConfigurationsAsync → ImportConfigurationsCommandHandler
- ❌ GetCategoriesAsync → GetCategoriesQueryHandler
- ❌ ValidateConfiguration → ConfigurationValidator.Validate

---

### Phase 7: Integration Testing (12 hours)

**Test Suite** (~50 integration tests):

1. **Command Handler Tests** (25 tests)
   - Create configuration (success, validation failure, duplicate)
   - Update configuration (success, not found, validation failure)
   - Delete configuration (success, not found)
   - Toggle configuration (success, not found)
   - Rollback configuration (success, version not found, config not found)
   - Bulk update (success, partial failure)
   - Import configurations (success, invalid format)

2. **Query Handler Tests** (15 tests)
   - Get by ID (success, not found)
   - Get by key (success, not found, environment filtering)
   - List configurations (pagination, filtering, sorting)
   - Get history (success, empty history)
   - Export configurations (JSON, CSV formats)
   - Get categories (success, empty)

3. **Domain Event Tests** (10 tests)
   - ConfigurationCreated event raised and handled
   - ConfigurationUpdated event with audit log
   - ConfigurationDeleted event
   - ConfigurationToggled event
   - Multiple event handlers for same event

**Test Infrastructure**:
- Testcontainers for PostgreSQL
- In-memory cache for testing
- Mock IMediator for unit tests
- Integration tests with real database

---

## Migration Strategy

### Step 1: Create Domain Services (Week 1)
- ConfigurationValidator
- ConfigurationVersionManager
- Unit tests for both

### Step 2: Create Query Handlers (Week 1-2)
- 6 query handlers + tests
- Verify caching works correctly

### Step 3: Create Command Handlers (Week 2-3)
- 7 command handlers + tests
- Verify domain events work

### Step 4: Create Domain Events (Week 3)
- 4 domain events + handlers
- Verify audit logging works

### Step 5: Update Endpoints (Week 3)
- Update ConfigurationEndpoints.cs to use MediatR
- Integration tests

### Step 6: Reduce ConfigurationService (Week 4)
- Remove all business logic
- Keep only infrastructure
- Verify everything still works

### Step 7: Final Testing (Week 4)
- Full regression testing
- Performance testing
- Load testing

---

## Testing Requirements

### Unit Tests (~80 tests)
- ConfigurationValidator: 15 tests
- ConfigurationVersionManager: 10 tests
- Each handler: 3-5 tests × 16 handlers = 55 tests

### Integration Tests (~50 tests)
- Command handlers: 25 tests
- Query handlers: 15 tests
- Domain events: 10 tests

### E2E Tests
- Test all 14 configuration endpoints via HTTP
- Test full workflow: create → update → rollback → delete

---

## Dependencies

**Blocks**:
- Issue #001: Split AdminEndpoints (ConfigurationEndpoints.cs needs to use MediatR)

**Blocked by**:
- None

**Related Issues**:
- Issue #003: Refactor RagService (similar CQRS migration pattern)

---

## Success Metrics

- ✅ 16 handlers created and tested (10 commands + 6 queries)
- ✅ 2 domain services extracted (~250 LOC total)
- ✅ 4 domain events + handlers implemented
- ✅ ConfigurationService reduced to ≤300 LOC
- ✅ 130+ tests passing (80 unit + 50 integration)
- ✅ Zero breaking changes to API
- ✅ All endpoints use MediatR
- ✅ 100% CQRS compliance achieved

---

## Estimated Timeline

**Total Effort**: 50-60 hours

| Week | Tasks | Hours |
|------|-------|-------|
| 1 | Domain services + Query handlers | 22h |
| 2 | Command handlers | 18h |
| 3 | Domain events + Endpoint updates | 14h |
| 4 | ConfigurationService reduction + Testing | 16h |
| - | **Buffer** | 10h |

**Recommended approach**: 4-week sprint (12-15 hours/week)

---

## References

- Analysis: `docs/02-development/backend-codebase-analysis.md`
- Current File: `apps/api/src/Api/Services/ConfigurationService.cs:805`
- CQRS Examples: `apps/api/src/Api/BoundedContexts/*/Application/Handlers/`
- Domain Events: Issue #1190 implementation
