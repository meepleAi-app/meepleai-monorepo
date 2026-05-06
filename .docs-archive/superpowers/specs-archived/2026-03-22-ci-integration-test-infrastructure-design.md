# CI Integration Test Infrastructure Fix

## Problem

136 integration tests fail in CI because:

1. **14 WebApplicationFactory tests** each duplicate 50-80 lines of DI/config setup. Most are incomplete — missing config keys, not isolating from CI env vars (`ConnectionStrings__Postgres` overrides test config), not mocking all required services. Result: 403 Forbidden (session middleware can't validate), DI resolution failures.

2. **58 ServiceCollection tests** call `AddMediatR(typeof(Program).Assembly)` which registers ALL handlers from the assembly, including event handlers whose dependencies aren't registered (`IDashboardStreamService`, `INotificationDispatcher`, `IGameSessionRepository`). When `SaveChangesAsync` dispatches domain events, MediatR tries to resolve these handlers → DI crash.

Integration tests have **never passed** on GitHub-hosted runners (`ubuntu-latest`). All "successful" CI runs skipped backend tests (frontend-only changes).

## Solution

Two shared infrastructure classes that encapsulate the correct, complete setup. Tests replace boilerplate with one-line factory calls.

### 1. IntegrationWebApplicationFactory

**File:** `apps/api/tests/Api.Tests/Infrastructure/IntegrationWebApplicationFactory.cs`

A reusable `WebApplicationFactory<Program>` subclass modeled after the working `E2EWebApplicationFactory` and `ReviewLockEndpointsIntegrationTests` patterns.

**Responsibilities:**
- **Config isolation**: `configBuilder.Sources.Clear()` (process-safe, scoped to factory instance) then provides ALL required keys via `AddInMemoryCollection` (JWT, admin, Redis, Qdrant, OpenRouter, embedding, rate limiting, observability, session management). This prevents CI env var `ConnectionStrings__Postgres` from overriding the test's connection string — the root cause of 403 errors.
- **Environment**: `UseEnvironment("Testing")`, sets `DISABLE_RATE_LIMITING` and `RateLimiting__Enabled` env vars
- **Explicit DbContext registration**: In `ConfigureServices` (NOT `ConfigureTestServices`), calls `services.RemoveAll<DbContextOptions<MeepleAiDbContext>>()` then `services.AddDbContext<MeepleAiDbContext>(options => options.UseNpgsql(connectionString, o => o.UseVector()))` using the `connectionString` parameter directly. This is required because `UseEnvironment("Testing")` causes `InfrastructureServiceExtensions.AddDatabaseServices()` to skip its DB setup block.
- **Hosted service removal**: In `ConfigureServices` (NOT `ConfigureTestServices`), removes all `IHostedService` registrations. Must happen in `ConfigureServices` to run before `IStartupFilter` and before WebApplicationFactory finalizes the pipeline.
- **Service mocks** (in `ConfigureTestServices`): Mocks `IConnectionMultiplexer`, `IEmbeddingService`, `IHybridCacheService`
- **Auth infrastructure**: Registers `IDomainEventCollector`

**Factory method:**
```csharp
public static WebApplicationFactory<Program> Create(
    string connectionString,
    string? redisConnectionString = null,
    Dictionary<string, string?>? extraConfig = null)
```

**Usage:**
```csharp
// Basic usage
_factory = IntegrationWebApplicationFactory.Create(connectionString);

// With Redis
_factory = IntegrationWebApplicationFactory.Create(connectionString, _fixture.RedisConnectionString);

// With extra config + test-specific mocks
_factory = IntegrationWebApplicationFactory.Create(connectionString, extraConfig: new() {
        ["BggImportQueue:Enabled"] = "false"
    })
    .WithWebHostBuilder(builder => builder.ConfigureTestServices(services => {
        services.AddScoped(_ => mockBggApi.Object);
    }));
```

### 2. IntegrationServiceCollectionBuilder

**File:** `apps/api/tests/Api.Tests/Infrastructure/IntegrationServiceCollectionBuilder.cs`

Static helper for tests that build `ServiceCollection` directly (repository/handler tests that don't need HTTP pipeline).

**Responsibilities:**
- Creates `ServiceCollection` with logging, DbContext (pgvector), `IDomainEventCollector`, `IUnitOfWork`
- Registers MediatR from `Program` assembly
- Registers `Mock.Of<T>()` stubs for the 6 services that cause DI failures during domain event dispatch: `IDashboardStreamService`, `INotificationDispatcher`, `IGameSessionRepository`, `IHybridCacheService`, `IEmbeddingService`, `IEmailService`

**Factory method:**
```csharp
public static ServiceCollection CreateBase(string connectionString)
```

**Usage:**
```csharp
var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
services.AddScoped<IBatchJobRepository, BatchJobRepository>(); // test-specific
_serviceProvider = services.BuildServiceProvider();
```

## Migration

All 72 files (14 WAF + 58 ServiceCollection) are migrated in a single plan:

1. Create the 2 infrastructure files
2. Migrate 14 WAF tests — replace boilerplate with `IntegrationWebApplicationFactory.Create()`
3. Migrate 58 ServiceCollection tests — replace setup with `IntegrationServiceCollectionBuilder.CreateBase()`
4. Fix remaining individual test data bugs (invalid player ranges, missing AgentId, etc.)
5. Verify in CI

## Files

### New Files
- `apps/api/tests/Api.Tests/Infrastructure/IntegrationWebApplicationFactory.cs`
- `apps/api/tests/Api.Tests/Infrastructure/IntegrationServiceCollectionBuilder.cs`

### Modified Files (14 WAF tests)
- `Integration/KnowledgeBase/AgentEndpointsSmokeTests.cs`
- `Integration/KnowledgeBase/AgentChatEndpointsIntegrationTests.cs`
- `Integration/KnowledgeBase/AgentTypologyEndpointsSmokeTests.cs`
- `Integration/Authentication/AuthenticationEndpointsIntegrationTests.cs`
- `Integration/GameManagement/GameEndpointsIntegrationTests.cs`
- `Integration/UserLibrary/UserLibraryEndpointsIntegrationTests.cs`
- `Integration/UserLibrary/PrivateGameEndpointsIntegrationTests.cs`
- `Integration/AdminGameImportWizardEndpointsIntegrationTests.cs`
- `BoundedContexts/SharedGameCatalog/Integration/BggImportQueueEndpointsIntegrationTests.cs`
- `BoundedContexts/SharedGameCatalog/Integration/WizardEndpointsIntegrationTests.cs`
- `BoundedContexts/SharedGameCatalog/Integration/CompleteWorkflowIntegrationTests.cs`
- `BoundedContexts/SharedGameCatalog/Infrastructure/SharedGameCatalogEndpointsIntegrationTests.cs`
- `BoundedContexts/SharedGameCatalog/Infrastructure/ReviewLockEndpointsIntegrationTests.cs`
- `BoundedContexts/Administration/Performance/DashboardEndpointPerformanceTests.cs`

### Modified Files (58 ServiceCollection tests)
All files in `Integration/` and `BoundedContexts/*/Integration/` and `BoundedContexts/*/Infrastructure/` that use `services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly))`.

## Success Criteria

- CI integration tests: failure count drops from current 161 baseline. The 25 tests fixed in PR #80 are deterministic bugs. The remaining 136 are addressed by this spec (infrastructure issues). Target: <10 remaining failures.
- Remaining failures after migration are expected to be individual test data bugs (e.g., `chk_shared_games_players` constraint violations, `AgentId` null seeding) which are fixed in Phase 4 of the migration plan.
- All 3 CI shards (KnowledgeBase, Games, Core) show significant improvement
- No test behavior changes — same assertions, same test logic, only infrastructure shared
- New WAF tests can be written with 1-line setup instead of 50-80 lines
