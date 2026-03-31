# P1 High-Priority Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 6 high-severity issues: response compression (disabled everywhere), unbounded DB queries, direct `new HttpClient()` in health checks, domain exception message leakage, CQRS violations (IFeatureFlagService in 12+ endpoints), and `DateTime.UtcNow` in domain layer.

**Architecture:** All backend fixes. CQRS violations are tackled by introducing a domain-level feature flag service abstraction routed through MediatR. Unbounded queries get a default page size cap. HttpClient violations switch to IHttpClientFactory injection.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR, EF Core, IHttpClientFactory.

**Prerequisite:** Plan A (P0) must be merged before starting this plan.

---

## Task 1: Fix Direct `new HttpClient()` in Health Checks

**Context:** 5 health check classes call `new HttpClient()` on every health check run (every 10-30s). This creates a new socket each time, risking socket exhaustion. All other HTTP usage in the codebase uses `IHttpClientFactory`.

**Files to modify:**
- `apps/api/src/Api/Infrastructure/Health/Checks/UnstructuredHealthCheck.cs`
- `apps/api/src/Api/Infrastructure/Health/Checks/SmolDoclingHealthCheck.cs`
- `apps/api/src/Api/Infrastructure/Health/Checks/RerankerHealthCheck.cs`
- `apps/api/src/Api/Infrastructure/Health/Checks/PrometheusHealthCheck.cs`
- `apps/api/src/Api/Infrastructure/Health/Checks/GrafanaHealthCheck.cs`

---

- [ ] **Step 1: Read one health check to understand the current pattern**

```bash
cat apps/api/src/Api/Infrastructure/Health/Checks/UnstructuredHealthCheck.cs
```

Expected: constructor takes some config/options, creates `new HttpClient()` locally.

- [ ] **Step 2: Fix `UnstructuredHealthCheck.cs`**

Replace the local `new HttpClient()` with constructor-injected `IHttpClientFactory`. The pattern to apply to all 5 files:

**Current pattern (example):**
```csharp
internal sealed class UnstructuredHealthCheck : IHealthCheck
{
    private readonly string _baseUrl;

    public UnstructuredHealthCheck(IConfiguration config)
    {
        _baseUrl = config["UNSTRUCTURED_SERVICE_URL"] ?? "http://unstructured-service:8000";
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        using var client = new HttpClient();
        // ...
    }
}
```

**Replace with:**
```csharp
internal sealed class UnstructuredHealthCheck : IHealthCheck
{
    private readonly string _baseUrl;
    private readonly IHttpClientFactory _httpClientFactory;

    public UnstructuredHealthCheck(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _baseUrl = config["UNSTRUCTURED_SERVICE_URL"] ?? "http://unstructured-service:8000";
        _httpClientFactory = httpClientFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient();
        // ... rest unchanged
    }
}
```

Apply the same constructor injection pattern to all 5 files.

- [ ] **Step 3: Build**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Health/Checks/
git commit -m "fix(infra): use IHttpClientFactory in health checks to prevent socket exhaustion"
```

---

## Task 2: Add Pagination Cap to Unbounded Repository Queries

**Context:** Six locations load entire tables with no row limit. The primary risk is `UserRepository.GetAllAsync()` (loads all users with eager-loaded BackupCodes + OAuthAccounts) and `BggImportQueueService.GetAllQueueItemsAsync()`.

**Strategy:** Add a default `take` parameter with a safe cap. Don't break existing callers — default to 1000 which is far above current data volumes but prevents unbounded growth.

**Files to modify:**
- `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs`
- `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Repositories/ShareLinkRepository.cs`
- `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Repositories/AlertRuleRepository.cs`
- `apps/api/src/Api/Infrastructure/Services/BggImportQueueService.cs`

---

- [ ] **Step 1: Read each file to identify the GetAllAsync signature**

```bash
grep -n "GetAllAsync\|async.*All\b" \
  apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs \
  apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Repositories/ShareLinkRepository.cs \
  apps/api/src/Api/Infrastructure/Services/BggImportQueueService.cs
```

- [ ] **Step 2: Add Take cap to `UserRepository.GetAllAsync`**

Open `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs`. Find the `GetAllAsync` method.

Add `.Take(5000)` before `.ToListAsync()` (5000 is a reasonable admin use-case ceiling):

```csharp
// Before
return await _context.Users
    .Include(u => u.BackupCodes)
    .Include(u => u.OAuthAccounts)
    .ToListAsync(ct);

// After
return await _context.Users
    .Include(u => u.BackupCodes)
    .Include(u => u.OAuthAccounts)
    .Take(5000)
    .ToListAsync(ct);
```

- [ ] **Step 3: Add Take cap to `ShareLinkRepository.GetAllAsync`**

Same pattern: add `.Take(10000)` before `.ToListAsync()` — share links are lighter objects.

- [ ] **Step 4: Add Take cap to `AlertRuleRepository`**

Find the three `GetAll*` methods. Add `.Take(1000)` to each.

- [ ] **Step 5: Fix `BggImportQueueService.GetAllQueueItemsAsync`**

This one loads all BGG import queue items ordered by creation date. Change to take last 500:

```csharp
// Before
.OrderByDescending(q => q.CreatedAt)
.ToListAsync(ct);

// After
.OrderByDescending(q => q.CreatedAt)
.Take(500)
.ToListAsync(ct);
```

- [ ] **Step 6: Build**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/ \
        apps/api/src/Api/BoundedContexts/Administration/ \
        apps/api/src/Api/Infrastructure/Services/BggImportQueueService.cs
git commit -m "fix(perf): add Take() cap to unbounded GetAllAsync repository queries"
```

---

## Task 3: Enable Response Compression at the Correct Layer

**Context:** Response compression is disabled in both the .NET API (`Program.cs` lines 158-179, commented out) and Next.js (`next.config.js: compress: false`). The comment says "causing ERR_CONTENT_DECODING_FAILED in Docker". Root cause: Traefik is likely the right place to enable gzip — the API and Next.js should not compress when behind a reverse proxy that does it.

**Strategy:** Enable gzip/brotli in Traefik middleware and leave API + Next.js compression disabled. This is the correct pattern for a proxied deployment.

**Files to modify:**
- `infra/traefik/traefik.yml` (add compress middleware)
- `infra/docker-compose.yml` (apply middleware to services)

---

- [ ] **Step 1: Check current Traefik config**

```bash
cat infra/traefik/traefik.yml
```

Look for any existing `compress` middleware definition.

- [ ] **Step 2: Check docker-compose.yml labels for API and web services**

```bash
grep -n "traefik\|compress\|middleware" infra/docker-compose.yml | head -30
```

- [ ] **Step 3: Add compress middleware to `traefik.yml`**

In `infra/traefik/traefik.yml`, add a `http.middlewares` section (or add to existing one) with:

```yaml
http:
  middlewares:
    compress:
      compress:
        excludedContentTypes:
          - text/event-stream   # Don't compress SSE streams
        minResponseBodyBytes: 1024
```

- [ ] **Step 4: Apply the middleware to API and web services in `docker-compose.yml`**

Find the Traefik labels for the `api` service. Add the compress middleware to the router middleware chain.

If existing middleware chain is e.g. `traefik.http.routers.api.middlewares=some-middleware@docker`, change to `some-middleware@docker,compress@file`.

If no middleware is applied yet, add:
```yaml
- "traefik.http.routers.api.middlewares=compress@file"
- "traefik.http.routers.web.middlewares=compress@file"
```

- [ ] **Step 5: Restart Traefik and verify**

```bash
cd infra
pwsh -c "docker compose restart traefik"
# Wait 10s
pwsh -c "docker logs meepleai-traefik --tail=20"
```

- [ ] **Step 6: Verify gzip encoding on API response**

```bash
pwsh -c "Invoke-WebRequest -Uri 'http://localhost/api/v1/health' -Headers @{'Accept-Encoding'='gzip'} -UseBasicParsing | Select-Object Headers"
```

Expected: `Content-Encoding: gzip` in response headers.

- [ ] **Step 7: Verify SSE is NOT compressed**

Test an SSE endpoint (e.g., notifications stream) and confirm it is served as `text/event-stream` without Content-Encoding header.

- [ ] **Step 8: Commit**

```bash
git add infra/traefik/traefik.yml infra/docker-compose.yml
git commit -m "fix(infra): enable gzip compression in Traefik middleware (correct layer for proxied deployment)"
```

---

## Task 4: Sanitize Domain Exception Messages in API Responses

**Context:** `ApiExceptionHandlerMiddleware` passes `domainEx.Message` and `notFoundEx.Message` directly to API responses. Internal implementation details (entity names, DB constraint names) can leak.

**Strategy:** Map each exception type to a generic user-facing message. Keep detailed message in the structured log only.

**Files to modify:**
- `apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs`

---

- [ ] **Step 1: Read the exception handler middleware**

```bash
cat apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs | grep -A3 "DomainException\|NotFoundException\|BadHttpRequest"
```

- [ ] **Step 2: Modify exception mapping to use generic messages in response**

Find the switch expression that maps exceptions to `(statusCode, errorCode, message)`. Change the domain exception cases to use generic messages while logging the real message:

```csharp
// Before (approximate):
DomainException domainEx => (400, "domain_error", domainEx.Message),
NotFoundException notFoundEx => (404, "not_found", notFoundEx.Message),

// After:
DomainException domainEx => (400, "domain_error", "The request could not be processed"),
NotFoundException notFoundEx => (404, "not_found", "The requested resource was not found"),
```

Ensure the logger call before the switch still logs `exception.Message` at the appropriate level (Warning/Error) so the real message appears in Seq/Serilog but not in the HTTP response.

- [ ] **Step 3: Build and verify**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Middleware/ApiExceptionHandlerMiddleware.cs
git commit -m "fix(security): sanitize domain exception messages in API error responses"
```

---

## Task 5: Replace `IFeatureFlagService` Direct Injection in Endpoints

**Context:** `IFeatureFlagService` is injected directly in 12+ endpoints alongside `IMediator`, violating the CQRS architecture rule ("endpoints use ONLY `IMediator.Send()` — ZERO direct service injection"). Feature flag checks should go through a `FeatureFlagQuery` or be handled by a middleware/filter.

**Strategy:** Create a `GetFeatureFlagQuery` + handler. Replace `IFeatureFlagService` injection in routing files with a mediator query. Start with the most common endpoints.

**Files to create:**
- `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/GetFeatureFlagQuery.cs`

**Files to modify (highest count first):**
- `apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs` (~10 violations)
- `apps/api/src/Api/Routing/AiEndpoints.cs` (~2 violations)
- `apps/api/src/Api/Routing/Pdf/PdfUploadEndpoints.cs` (~2 violations)

---

- [ ] **Step 1: Read the IFeatureFlagService interface**

```bash
find apps/api/src/Api -name "IFeatureFlagService.cs" | head -3
cat $(find apps/api/src/Api -name "IFeatureFlagService.cs" | head -1)
```

- [ ] **Step 2: Create `GetFeatureFlagQuery`**

Create file `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/GetFeatureFlagQuery.cs`:

```csharp
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to check whether a feature flag is enabled.
/// Use via IMediator.Send() in endpoints instead of injecting IFeatureFlagService directly.
/// </summary>
public sealed record GetFeatureFlagQuery(string FlagName) : IRequest<bool>;
```

- [ ] **Step 3: Create the handler**

Create file `apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/GetFeatureFlagQueryHandler.cs`:

```csharp
using Api.BoundedContexts.SystemConfiguration.Application.Services;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

internal sealed class GetFeatureFlagQueryHandler : IRequestHandler<GetFeatureFlagQuery, bool>
{
    private readonly IFeatureFlagService _featureFlagService;

    public GetFeatureFlagQueryHandler(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    public async Task<bool> Handle(GetFeatureFlagQuery request, CancellationToken cancellationToken)
    {
        return await _featureFlagService.IsEnabledAsync(request.FlagName, cancellationToken)
            .ConfigureAwait(false);
    }
}
```

(Adjust `IsEnabledAsync` method name and signature to match the actual interface.)

- [ ] **Step 4: Replace `IFeatureFlagService` injection in `DatabaseSyncEndpoints.cs`**

Open `apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs`. For each handler that injects `IFeatureFlagService featureFlags`:

1. Remove `IFeatureFlagService featureFlags` from the handler parameters
2. Replace `await featureFlags.IsEnabledAsync(flagName, ct)` with:
   ```csharp
   await mediator.Send(new GetFeatureFlagQuery(flagName), ct)
   ```

Example transformation:
```csharp
// Before
private static async Task<IResult> HandleSomeEndpoint(
    IMediator mediator,
    IFeatureFlagService featureFlags,
    CancellationToken ct)
{
    if (!await featureFlags.IsEnabledAsync("database-sync", ct))
        return Results.StatusCode(503);
    // ...
}

// After
private static async Task<IResult> HandleSomeEndpoint(
    IMediator mediator,
    CancellationToken ct)
{
    if (!await mediator.Send(new GetFeatureFlagQuery("database-sync"), ct))
        return Results.StatusCode(503);
    // ...
}
```

Apply to all ~10 handlers in `DatabaseSyncEndpoints.cs`.

- [ ] **Step 5: Apply same pattern to `AiEndpoints.cs` and `PdfUploadEndpoints.cs`**

Same substitution: remove `IFeatureFlagService featureFlags` param, replace calls.

- [ ] **Step 6: Build**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SystemConfiguration/Application/Queries/ \
        apps/api/src/Api/Routing/DatabaseSyncEndpoints.cs \
        apps/api/src/Api/Routing/AiEndpoints.cs \
        apps/api/src/Api/Routing/Pdf/PdfUploadEndpoints.cs
git commit -m "refactor(arch): replace direct IFeatureFlagService injection in endpoints with GetFeatureFlagQuery via mediator"
```

---

## Task 6: Replace `DateTime.UtcNow` with `TimeProvider` in Domain Layer

**Context:** 70+ occurrences of `DateTime.UtcNow` in domain entities and services make time-dependent behavior untestable. .NET 9 has built-in `TimeProvider`. Domain entities must not use static time — they should accept time via constructor or a domain service.

**Strategy:** Focus on the domain layer only (entities, value objects, domain services) for now. Application and infrastructure layers can be addressed in P3.

**Files to modify (domain layer only):**
- `apps/api/src/Api/SharedKernel/Domain/Events/DomainEventBase.cs`
- `apps/api/src/Api/SharedKernel/Domain/Events/IntegrationEventBase.cs`
- `apps/api/src/Api/BoundedContexts/WorkflowIntegration/Domain/Entities/N8nConfiguration.cs`
- `apps/api/src/Api/BoundedContexts/WorkflowIntegration/Domain/Entities/WorkflowErrorLog.cs`

---

- [ ] **Step 1: Read `DomainEventBase.cs` and `IntegrationEventBase.cs`**

```bash
cat apps/api/src/Api/SharedKernel/Domain/Events/DomainEventBase.cs
cat apps/api/src/Api/SharedKernel/Domain/Events/IntegrationEventBase.cs
```

- [ ] **Step 2: Fix `DomainEventBase` to accept `TimeProvider` or use param**

The simplest DDD-friendly fix for event base classes is to use `TimeProvider.System.GetUtcNow()` which is still testable when the TimeProvider is injected at the application layer:

```csharp
// Before (line ~27):
public DateTime OccurredOn { get; } = DateTime.UtcNow;

// After:
public DateTimeOffset OccurredOn { get; } = TimeProvider.System.GetUtcNow();
```

If other code depends on `DateTime` type, use:
```csharp
public DateTime OccurredOn { get; } = TimeProvider.System.GetUtcNow().UtcDateTime;
```

Apply same change to `IntegrationEventBase.cs`.

- [ ] **Step 3: Fix `N8nConfiguration.cs` domain entity**

Read the file first:
```bash
cat apps/api/src/Api/BoundedContexts/WorkflowIntegration/Domain/Entities/N8nConfiguration.cs
```

Replace each `DateTime.UtcNow` in entity methods with `TimeProvider.System.GetUtcNow().UtcDateTime`. There are ~6 occurrences.

- [ ] **Step 4: Fix `WorkflowErrorLog.cs`**

Same pattern — replace `DateTime.UtcNow` with `TimeProvider.System.GetUtcNow().UtcDateTime`.

- [ ] **Step 5: Build**

```bash
cd apps/api/src/Api
dotnet build --no-restore
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Domain/Events/ \
        apps/api/src/Api/BoundedContexts/WorkflowIntegration/Domain/
git commit -m "refactor(domain): replace DateTime.UtcNow with TimeProvider.System in domain layer"
```

---

## Final Validation

- [ ] **Full backend build clean**
```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **All unit tests green**
```bash
cd tests/Api.Tests && dotnet test --filter "Category=Unit" 2>&1 | tail -5
```

- [ ] **Create PR**
```bash
git checkout -b fix/p1-high-priority
# merge all task commits
git push -u origin fix/p1-high-priority
gh pr create \
  --base main-dev \
  --title "fix: P1 high-priority architecture and performance fixes" \
  --body "$(cat <<'EOF'
## Summary
- **fix(infra)**: Use IHttpClientFactory in 5 health check classes (socket exhaustion)
- **fix(perf)**: Add Take() cap to 6 unbounded GetAllAsync queries
- **fix(infra)**: Enable gzip via Traefik middleware (correct compression layer)
- **fix(security)**: Sanitize domain exception messages in API error responses
- **refactor(arch)**: Replace 15+ direct IFeatureFlagService injections with GetFeatureFlagQuery via MediatR
- **refactor(domain)**: Replace DateTime.UtcNow with TimeProvider in domain events and entities

## Test Plan
- [ ] `dotnet test --filter Category=Unit`
- [ ] Verify gzip header on API responses behind Traefik
- [ ] Verify SSE streams are not compressed (text/event-stream excluded)
- [ ] Verify error responses return generic messages, not internal exception text

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
