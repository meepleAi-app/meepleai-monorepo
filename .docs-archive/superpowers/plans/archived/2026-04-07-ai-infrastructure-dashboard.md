# AI Infrastructure Dashboard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a superadmin dashboard to monitor and manage 8 AI infrastructure services with health checks, restart with cooldown, pipeline connectivity test, and runtime config — accessible via `/admin/agents/infrastructure` with a semaphore widget on the Mission Control page.

**Architecture:** Backend-centric CQRS via .NET API. Frontend calls only .NET endpoints which proxy to Python AI services and Docker socket. Administration bounded context extended with new queries/commands. React Query polling (30s) for data freshness.

**Tech Stack:** .NET 9 (MediatR, FluentValidation) | Next.js (React Query, Sonner toast, Tailwind, shadcn/ui) | Python FastAPI (PUT /config endpoints)

**Spec:** `docs/superpowers/specs/2026-04-07-ai-infrastructure-dashboard-design.md`

---

## File Structure

### Backend — New Files (Administration BC)

| File | Responsibility |
|------|---------------|
| `BoundedContexts/Administration/Domain/Services/IServiceCooldownRegistry.cs` | Cooldown interface + singleton impl |
| `BoundedContexts/Administration/Domain/ValueObjects/HealthThresholds.cs` | Latency/error rate threshold constants |
| `BoundedContexts/Administration/Domain/ValueObjects/ServiceRegistry.cs` | Known services, display names, dependencies, config maps |
| `BoundedContexts/Administration/Application/DTOs/InfrastructureDtos.cs` | All DTOs (AiServiceStatusDto, PipelineHopDto, etc.) |
| `BoundedContexts/Administration/Application/Queries/Infrastructure/GetAiServicesStatusQuery.cs` | Query + handler |
| `BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceDependenciesQuery.cs` | Query + handler |
| `BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceConfigQuery.cs` | Query + handler |
| `BoundedContexts/Administration/Application/Queries/Infrastructure/TestPipelineConnectivityQuery.cs` | Query + handler |
| `BoundedContexts/Administration/Application/Commands/Infrastructure/RestartServiceCommand.cs` | Command + handler |
| `BoundedContexts/Administration/Application/Commands/Infrastructure/TriggerHealthCheckCommand.cs` | Command + handler |
| `BoundedContexts/Administration/Application/Commands/Infrastructure/UpdateServiceConfigCommand.cs` | Command + handler |
| `BoundedContexts/Administration/Application/Validators/Infrastructure/RestartServiceCommandValidator.cs` | Validator |
| `BoundedContexts/Administration/Application/Validators/Infrastructure/TriggerHealthCheckCommandValidator.cs` | Validator |
| `BoundedContexts/Administration/Application/Validators/Infrastructure/UpdateServiceConfigCommandValidator.cs` | Validator |
| `Routing/AdminInfrastructureEndpoints.cs` | All 7 endpoints |

### Backend — Modified Files

| File | Change |
|------|--------|
| `BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` | Register CooldownRegistry + HttpClients for AI services |
| `Routing/EndpointRouteBuilderExtensions.cs` | Wire `MapAdminInfrastructureEndpoints()` |

### Frontend — New Files

| File | Responsibility |
|------|---------------|
| `src/lib/api/clients/infrastructure.ts` | API client for infrastructure endpoints |
| `src/hooks/admin/use-infrastructure.ts` | React Query hooks (useInfraServices, usePipelineTest, etc.) |
| `src/app/admin/(dashboard)/agents/infrastructure/page.tsx` | Page entry (server component) |
| `src/components/admin/infrastructure/InfrastructureDashboard.tsx` | Main client component |
| `src/components/admin/infrastructure/InfraStatusBar.tsx` | Semaphore widget for Mission Control |
| `src/components/admin/infrastructure/ServiceGrid.tsx` | Grid of 8 service cards |
| `src/components/admin/infrastructure/ServiceCard.tsx` | Single service card |
| `src/components/admin/infrastructure/PipelineTest.tsx` | Pipeline connectivity test UI |
| `src/components/admin/infrastructure/ServiceDetailPanel.tsx` | Expandable panel with tabs |
| `src/components/admin/infrastructure/RestartModal.tsx` | Restart confirmation modal |
| `src/components/admin/infrastructure/ServiceConfigForm.tsx` | Runtime config form |

### Frontend — Modified Files

| File | Change |
|------|--------|
| `src/config/admin-dashboard-navigation.ts` | Add "Infrastructure" sidebar item under AI section |
| `src/app/admin/(dashboard)/agents/page.tsx` | Add `InfraStatusBar` widget |
| `src/lib/api/index.ts` | Register infrastructure client |

### Python — Modified Files

| File | Change |
|------|--------|
| `apps/embedding-service/main.py` | Add `PUT /config` endpoint |
| `apps/reranker-service/main.py` | Add `PUT /config` endpoint |
| `apps/unstructured-service/src/main.py` | Add `PUT /config` endpoint |
| `apps/orchestration-service/main.py` | Add `PUT /config` endpoint |

### Test Files

| File | Responsibility |
|------|---------------|
| `tests/Api.Tests/Unit/Administration/Infrastructure/ServiceCooldownRegistryTests.cs` | Cooldown logic unit tests |
| `tests/Api.Tests/Unit/Administration/Infrastructure/HealthThresholdsTests.cs` | Threshold determination tests |
| `tests/Api.Tests/Unit/Administration/Infrastructure/GetAiServicesStatusQueryHandlerTests.cs` | Handler tests |
| `tests/Api.Tests/Unit/Administration/Infrastructure/RestartServiceCommandHandlerTests.cs` | Restart + cooldown tests |
| `tests/Api.Tests/Unit/Administration/Infrastructure/TestPipelineConnectivityQueryHandlerTests.cs` | Pipeline test handler |
| `tests/Api.Tests/Unit/Administration/Infrastructure/InfrastructureValidatorsTests.cs` | Validator tests |
| `tests/Api.Tests/Integration/Administration/AdminInfrastructureEndpointsTests.cs` | Endpoint auth + integration |
| `apps/web/__tests__/components/admin/infrastructure/InfraStatusBar.test.tsx` | Widget tests |
| `apps/web/__tests__/components/admin/infrastructure/ServiceCard.test.tsx` | Card states tests |
| `apps/web/__tests__/components/admin/infrastructure/PipelineTest.test.tsx` | Pipeline UI tests |
| `apps/web/__tests__/components/admin/infrastructure/RestartModal.test.tsx` | Modal + cooldown tests |

---

## Task 1: Domain — CooldownRegistry + HealthThresholds + ServiceRegistry

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IServiceCooldownRegistry.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/HealthThresholds.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/ServiceRegistry.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/ServiceCooldownRegistryTests.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/HealthThresholdsTests.cs`

- [ ] **Step 1: Write CooldownRegistry tests**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/ServiceCooldownRegistryTests.cs
using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Time.Testing;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class ServiceCooldownRegistryTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly ServiceCooldownRegistry _registry;

    public ServiceCooldownRegistryTests()
    {
        _registry = new ServiceCooldownRegistry(_timeProvider);
    }

    [Fact]
    public void IsInCooldown_WhenNoRestart_ReturnsFalse()
    {
        var result = _registry.IsInCooldown("embedding", out var remaining);

        Assert.False(result);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void IsInCooldown_AfterRestart_ReturnsTrue()
    {
        _registry.RecordRestart("embedding");

        var result = _registry.IsInCooldown("embedding", out var remaining);

        Assert.True(result);
        Assert.True(remaining > 0);
        Assert.True(remaining <= 300); // 5 minutes max
    }

    [Fact]
    public void IsInCooldown_AfterCooldownExpires_ReturnsFalse()
    {
        _registry.RecordRestart("embedding");
        _timeProvider.Advance(TimeSpan.FromMinutes(6));

        var result = _registry.IsInCooldown("embedding", out var remaining);

        Assert.False(result);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public void IsInCooldown_DifferentServices_Independent()
    {
        _registry.RecordRestart("embedding");

        var embeddingCooldown = _registry.IsInCooldown("embedding", out _);
        var rerankerCooldown = _registry.IsInCooldown("reranker", out _);

        Assert.True(embeddingCooldown);
        Assert.False(rerankerCooldown);
    }

    [Fact]
    public void RecordRestart_ResetsExistingCooldown()
    {
        _registry.RecordRestart("embedding");
        _timeProvider.Advance(TimeSpan.FromMinutes(3));
        _registry.RecordRestart("embedding"); // reset

        var result = _registry.IsInCooldown("embedding", out var remaining);

        Assert.True(result);
        Assert.True(remaining > 180); // close to 5 min again
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~ServiceCooldownRegistryTests" -v n
```

Expected: Compilation error — `ServiceCooldownRegistry` not defined.

- [ ] **Step 3: Implement CooldownRegistry**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IServiceCooldownRegistry.cs
using System.Collections.Concurrent;

namespace Api.BoundedContexts.Administration.Domain.Services;

internal interface IServiceCooldownRegistry
{
    bool IsInCooldown(string serviceName, out int remainingSeconds);
    void RecordRestart(string serviceName);
}

internal sealed class ServiceCooldownRegistry : IServiceCooldownRegistry
{
    private readonly ConcurrentDictionary<string, DateTime> _lastRestarts = new();
    private static readonly TimeSpan CooldownDuration = TimeSpan.FromMinutes(5);
    private readonly TimeProvider _timeProvider;

    public ServiceCooldownRegistry(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsInCooldown(string serviceName, out int remainingSeconds)
    {
        remainingSeconds = 0;

        if (!_lastRestarts.TryGetValue(serviceName, out var lastRestart))
            return false;

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var elapsed = now - lastRestart;

        if (elapsed >= CooldownDuration)
            return false;

        remainingSeconds = (int)(CooldownDuration - elapsed).TotalSeconds;
        return true;
    }

    public void RecordRestart(string serviceName)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        _lastRestarts.AddOrUpdate(serviceName, now, (_, _) => now);
    }
}
```

- [ ] **Step 4: Implement HealthThresholds**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/HealthThresholds.cs
namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

internal static class HealthThresholds
{
    public static readonly IReadOnlyDictionary<string, int> LatencyDegradedMs =
        new Dictionary<string, int>
        {
            ["embedding"] = 2000,
            ["reranker"] = 2000,
            ["smoldocling"] = 5000,
            ["unstructured"] = 5000,
            ["orchestrator"] = 3000,
            ["ollama"] = 5000,
            ["postgres"] = 500,
            ["redis"] = 100
        };

    public const double ErrorRateDegradedPercent = 5.0;
    public const double ErrorRateCriticalPercent = 20.0;

    public static ServiceHealthLevel DetermineHealth(
        string serviceName, bool healthCheckPassed, double avgLatencyMs, double errorRate24h)
    {
        if (!healthCheckPassed)
            return ServiceHealthLevel.Down;

        var latencyThreshold = LatencyDegradedMs.GetValueOrDefault(serviceName, 2000);

        if (errorRate24h >= ErrorRateCriticalPercent)
            return ServiceHealthLevel.Down;

        if (avgLatencyMs > latencyThreshold || errorRate24h >= ErrorRateDegradedPercent)
            return ServiceHealthLevel.Degraded;

        return ServiceHealthLevel.Healthy;
    }
}

internal enum ServiceHealthLevel
{
    Healthy = 0,
    Degraded = 1,
    Down = 2,
    Restarting = 3,
    Unknown = 4
}
```

- [ ] **Step 5: Implement ServiceRegistry**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/ServiceRegistry.cs
namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

internal static class ServiceRegistry
{
    public static readonly IReadOnlyList<string> AllServiceNames = new[]
    {
        "embedding", "unstructured", "smoldocling", "reranker",
        "orchestrator", "ollama", "postgres", "redis"
    };

    public static readonly IReadOnlyDictionary<string, ServiceDefinition> Services =
        new Dictionary<string, ServiceDefinition>
        {
            ["embedding"] = new("Embedding (multilingual-e5)", "ai", "meepleai-embedding",
                8000, "/health", new[] { "redis" }),
            ["unstructured"] = new("Unstructured", "ai", "meepleai-unstructured",
                8001, "/health", Array.Empty<string>()),
            ["smoldocling"] = new("SmolDocling (VLM)", "ai", "meepleai-smoldocling",
                8002, "/health", Array.Empty<string>()),
            ["reranker"] = new("Reranker (BGE)", "ai", "meepleai-reranker",
                8003, "/health", Array.Empty<string>()),
            ["orchestrator"] = new("Orchestrator (LangGraph)", "ai", "meepleai-orchestrator",
                8004, "/health", new[] { "embedding", "reranker", "redis" }),
            ["ollama"] = new("Ollama", "ai", "meepleai-ollama",
                11434, "/", new[] { "redis" }),
            ["postgres"] = new("PostgreSQL (+pgvector)", "infra", "meepleai-postgres",
                5432, null, Array.Empty<string>()),
            ["redis"] = new("Redis", "infra", "meepleai-redis",
                6379, null, Array.Empty<string>())
        };

    public static readonly IReadOnlyDictionary<string, IReadOnlyList<ConfigParamDefinition>> ConfigParams =
        new Dictionary<string, IReadOnlyList<ConfigParamDefinition>>
        {
            ["embedding"] = new[]
            {
                new ConfigParamDefinition("model", "Model", "enum",
                    new[] { "intfloat/multilingual-e5-small", "intfloat/multilingual-e5-base", "intfloat/multilingual-e5-large" })
            },
            ["reranker"] = new[]
            {
                new ConfigParamDefinition("rate_limit", "Rate Limit (req/min)", "int", null, 10, 1000),
                new ConfigParamDefinition("batch_size", "Batch Size", "int", null, 1, 128)
            },
            ["unstructured"] = new[]
            {
                new ConfigParamDefinition("strategy", "Extraction Strategy", "enum",
                    new[] { "fast", "hi-res" })
            },
            ["orchestrator"] = new[]
            {
                new ConfigParamDefinition("langgraph_timeout", "Workflow Timeout (sec)", "int", null, 5, 120),
                new ConfigParamDefinition("max_workflow_depth", "Max Workflow Depth", "int", null, 1, 20)
            },
            ["ollama"] = new[]
            {
                new ConfigParamDefinition("model", "Active Model", "string", null)
            }
        };

    public static bool IsKnownService(string name) => Services.ContainsKey(name);

    /// <summary>Pipeline test order: services checked sequentially.</summary>
    public static readonly IReadOnlyList<string> PipelineChain = new[]
    {
        "postgres", "redis", "embedding", "reranker", "orchestrator"
    };
}

internal record ServiceDefinition(
    string DisplayName,
    string Type,
    string ContainerName,
    int Port,
    string? HealthEndpoint,
    IReadOnlyList<string> Dependencies);

internal record ConfigParamDefinition(
    string Key,
    string DisplayName,
    string Type,
    string[]? Options = null,
    int? MinValue = null,
    int? MaxValue = null);
```

- [ ] **Step 6: Write HealthThresholds tests**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/HealthThresholdsTests.cs
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class HealthThresholdsTests
{
    [Fact]
    public void DetermineHealth_HealthyService_ReturnsHealthy()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 1.0);
        Assert.Equal(ServiceHealthLevel.Healthy, result);
    }

    [Fact]
    public void DetermineHealth_HighLatency_ReturnsDegraded()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 3000, 1.0);
        Assert.Equal(ServiceHealthLevel.Degraded, result);
    }

    [Fact]
    public void DetermineHealth_HighErrorRate_ReturnsDegraded()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 8.0);
        Assert.Equal(ServiceHealthLevel.Degraded, result);
    }

    [Fact]
    public void DetermineHealth_CriticalErrorRate_ReturnsDown()
    {
        var result = HealthThresholds.DetermineHealth("embedding", true, 500, 25.0);
        Assert.Equal(ServiceHealthLevel.Down, result);
    }

    [Fact]
    public void DetermineHealth_HealthCheckFailed_ReturnsDown()
    {
        var result = HealthThresholds.DetermineHealth("embedding", false, 0, 0);
        Assert.Equal(ServiceHealthLevel.Down, result);
    }

    [Fact]
    public void DetermineHealth_Redis_LowThreshold()
    {
        // Redis has 100ms threshold
        var degraded = HealthThresholds.DetermineHealth("redis", true, 150, 0);
        Assert.Equal(ServiceHealthLevel.Degraded, degraded);

        var healthy = HealthThresholds.DetermineHealth("redis", true, 50, 0);
        Assert.Equal(ServiceHealthLevel.Healthy, healthy);
    }
}
```

- [ ] **Step 7: Run all tests to verify they pass**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~ServiceCooldownRegistryTests|FullyQualifiedName~HealthThresholdsTests" -v n
```

Expected: All 10 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Services/IServiceCooldownRegistry.cs apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/HealthThresholds.cs apps/api/src/Api/BoundedContexts/Administration/Domain/ValueObjects/ServiceRegistry.cs tests/Api.Tests/Unit/Administration/Infrastructure/
git commit -m "feat(admin): add CooldownRegistry, HealthThresholds, ServiceRegistry for infra dashboard"
```

---

## Task 2: DTOs

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/InfrastructureDtos.cs`

- [ ] **Step 1: Create all DTOs in a single file**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/InfrastructureDtos.cs
using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.DTOs;

internal record AiServiceStatusDto(
    string Name,
    string DisplayName,
    string Type,
    ServiceHealthLevel Status,
    string Uptime,
    double AvgLatencyMs,
    double ErrorRate24h,
    DateTime LastCheckedAt,
    bool CanRestart,
    int? CooldownRemainingSeconds);

internal record AiServicesStatusResponse(IReadOnlyList<AiServiceStatusDto> Services);

internal record ServiceDependencyDto(
    string Name,
    string DisplayName,
    ServiceHealthLevel Status,
    double LatencyMs);

internal record ServiceDependenciesResponse(
    string ServiceName,
    IReadOnlyList<ServiceDependencyDto> Dependencies);

internal record PipelineHopDto(
    string ServiceName,
    string DisplayName,
    ServiceHealthLevel Status,
    double LatencyMs,
    string? Error);

internal record PipelineTestResponse(
    bool Success,
    IReadOnlyList<PipelineHopDto> Hops,
    double TotalLatencyMs);

internal record ServiceConfigParamDto(
    string Key,
    string DisplayName,
    string Value,
    string Type,
    string[]? Options,
    int? MinValue,
    int? MaxValue);

internal record ServiceConfigResponse(
    string ServiceName,
    IReadOnlyList<ServiceConfigParamDto> Parameters);

internal record RestartResponse(
    bool Success,
    string ServiceName,
    DateTime? CooldownExpiresAt);

internal record HealthCheckResponse(
    string ServiceName,
    ServiceHealthLevel Status,
    string? Details,
    double LatencyMs);

internal record ConfigUpdateResponse(
    string ServiceName,
    IReadOnlyList<string> UpdatedParams);
```

- [ ] **Step 2: Verify it compiles**

```bash
cd apps/api/src/Api && dotnet build --no-restore -v q
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/InfrastructureDtos.cs
git commit -m "feat(admin): add infrastructure dashboard DTOs"
```

---

## Task 3: Queries — GetAiServicesStatus + TestPipelineConnectivity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetAiServicesStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/TestPipelineConnectivityQuery.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/GetAiServicesStatusQueryHandlerTests.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/TestPipelineConnectivityQueryHandlerTests.cs`

- [ ] **Step 1: Write GetAiServicesStatus handler test**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/GetAiServicesStatusQueryHandlerTests.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class GetAiServicesStatusQueryHandlerTests
{
    private readonly IInfrastructureHealthService _healthService = Substitute.For<IInfrastructureHealthService>();
    private readonly IServiceCooldownRegistry _cooldownRegistry = Substitute.For<IServiceCooldownRegistry>();
    private readonly GetAiServicesStatusQueryHandler _handler;

    public GetAiServicesStatusQueryHandlerTests()
    {
        _handler = new GetAiServicesStatusQueryHandler(
            _healthService,
            _cooldownRegistry,
            NullLogger<GetAiServicesStatusQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_ReturnsAllEightServices()
    {
        _healthService.GetAllServicesHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ServiceHealthStatus>());

        var result = await _handler.Handle(new GetAiServicesStatusQuery(), CancellationToken.None);

        Assert.Equal(8, result.Services.Count);
    }

    [Fact]
    public async Task Handle_ServiceInCooldown_SetsCanRestartFalse()
    {
        _healthService.GetAllServicesHealthAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ServiceHealthStatus>());
        int remaining = 120;
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = remaining; return true; });

        var result = await _handler.Handle(new GetAiServicesStatusQuery(), CancellationToken.None);

        var embedding = result.Services.First(s => s.Name == "embedding");
        Assert.False(embedding.CanRestart);
        Assert.NotNull(embedding.CooldownRemainingSeconds);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~GetAiServicesStatusQueryHandlerTests" -v n
```

Expected: Compilation error — handler not defined.

- [ ] **Step 3: Implement GetAiServicesStatusQuery + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetAiServicesStatusQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetAiServicesStatusQuery() : IQuery<AiServicesStatusResponse>;

internal class GetAiServicesStatusQueryHandler
    : IQueryHandler<GetAiServicesStatusQuery, AiServicesStatusResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly IServiceCooldownRegistry _cooldownRegistry;
    private readonly ILogger<GetAiServicesStatusQueryHandler> _logger;

    public GetAiServicesStatusQueryHandler(
        IInfrastructureHealthService healthService,
        IServiceCooldownRegistry cooldownRegistry,
        ILogger<GetAiServicesStatusQueryHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _cooldownRegistry = cooldownRegistry ?? throw new ArgumentNullException(nameof(cooldownRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiServicesStatusResponse> Handle(
        GetAiServicesStatusQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var healthStatuses = await _healthService
            .GetAllServicesHealthAsync(cancellationToken)
            .ConfigureAwait(false);

        var healthMap = healthStatuses.ToDictionary(h => h.ServiceName, h => h);

        var services = ServiceRegistry.AllServiceNames.Select(name =>
        {
            var def = ServiceRegistry.Services[name];
            var hasHealth = healthMap.TryGetValue(name, out var health);
            var healthPassed = hasHealth && health!.State == HealthState.Healthy;
            var latencyMs = hasHealth ? health!.ResponseTime.TotalMilliseconds : 0;

            var status = hasHealth
                ? HealthThresholds.DetermineHealth(name, healthPassed, latencyMs, 0)
                : ServiceHealthLevel.Unknown;

            var inCooldown = _cooldownRegistry.IsInCooldown(name, out var remainingSeconds);

            return new AiServiceStatusDto(
                Name: name,
                DisplayName: def.DisplayName,
                Type: def.Type,
                Status: status,
                Uptime: hasHealth ? FormatUptime(health!.CheckedAt) : "unknown",
                AvgLatencyMs: Math.Round(latencyMs, 1),
                ErrorRate24h: 0, // TODO: integrate Prometheus metrics when available
                LastCheckedAt: hasHealth ? health!.CheckedAt : DateTime.MinValue,
                CanRestart: !inCooldown,
                CooldownRemainingSeconds: inCooldown ? remainingSeconds : null);
        }).ToList();

        return new AiServicesStatusResponse(services);
    }

    private static string FormatUptime(DateTime lastChecked)
    {
        var uptime = DateTime.UtcNow - lastChecked;
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{(int)uptime.TotalMinutes}m";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~GetAiServicesStatusQueryHandlerTests" -v n
```

Expected: 2 tests PASS.

- [ ] **Step 5: Write TestPipelineConnectivity handler test**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/TestPipelineConnectivityQueryHandlerTests.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class TestPipelineConnectivityQueryHandlerTests
{
    private readonly IInfrastructureHealthService _healthService = Substitute.For<IInfrastructureHealthService>();
    private readonly TestPipelineConnectivityQueryHandler _handler;

    public TestPipelineConnectivityQueryHandlerTests()
    {
        _handler = new TestPipelineConnectivityQueryHandler(
            _healthService,
            NullLogger<TestPipelineConnectivityQueryHandler>.Instance);
    }

    [Fact]
    public async Task Handle_AllHealthy_ReturnsSuccess()
    {
        var statuses = ServiceRegistry.PipelineChain.Select(name =>
            new ServiceHealthStatus(name, HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(20)))
            .ToList();

        _healthService.GetServiceHealthAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var name = callInfo.ArgAt<string>(0);
                return statuses.FirstOrDefault(s => s.ServiceName == name)
                    ?? new ServiceHealthStatus(name, HealthState.Unhealthy, "Not found", DateTime.UtcNow, TimeSpan.Zero);
            });

        var result = await _handler.Handle(new TestPipelineConnectivityQuery(), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(ServiceRegistry.PipelineChain.Count, result.Hops.Count);
        Assert.True(result.TotalLatencyMs > 0);
    }

    [Fact]
    public async Task Handle_ServiceDown_StopsAtFailure()
    {
        _healthService.GetServiceHealthAsync("postgres", Arg.Any<CancellationToken>())
            .Returns(new ServiceHealthStatus("postgres", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)));
        _healthService.GetServiceHealthAsync("redis", Arg.Any<CancellationToken>())
            .Returns(new ServiceHealthStatus("redis", HealthState.Unhealthy, "Connection refused", DateTime.UtcNow, TimeSpan.Zero));
        // Remaining services should not be called

        var result = await _handler.Handle(new TestPipelineConnectivityQuery(), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(2, result.Hops.Count); // postgres ok, redis failed, rest skipped
        Assert.Equal(ServiceHealthLevel.Down, result.Hops[1].Status);
        Assert.Equal("Connection refused", result.Hops[1].Error);
    }
}
```

- [ ] **Step 6: Implement TestPipelineConnectivityQuery + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/TestPipelineConnectivityQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record TestPipelineConnectivityQuery() : IQuery<PipelineTestResponse>;

internal class TestPipelineConnectivityQueryHandler
    : IQueryHandler<TestPipelineConnectivityQuery, PipelineTestResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly ILogger<TestPipelineConnectivityQueryHandler> _logger;

    public TestPipelineConnectivityQueryHandler(
        IInfrastructureHealthService healthService,
        ILogger<TestPipelineConnectivityQueryHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PipelineTestResponse> Handle(
        TestPipelineConnectivityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hops = new List<PipelineHopDto>();
        var totalLatency = 0.0;
        var success = true;

        foreach (var serviceName in ServiceRegistry.PipelineChain)
        {
            var def = ServiceRegistry.Services[serviceName];

            try
            {
                var health = await _healthService
                    .GetServiceHealthAsync(serviceName, cancellationToken)
                    .ConfigureAwait(false);

                var latencyMs = health.ResponseTime.TotalMilliseconds;
                var isHealthy = health.State == HealthState.Healthy;
                var status = isHealthy ? ServiceHealthLevel.Healthy : ServiceHealthLevel.Down;

                hops.Add(new PipelineHopDto(
                    serviceName, def.DisplayName, status,
                    Math.Round(latencyMs, 1),
                    isHealthy ? null : health.ErrorMessage));

                totalLatency += latencyMs;

                if (!isHealthy)
                {
                    success = false;
                    _logger.LogWarning("Pipeline test failed at {Service}: {Error}",
                        serviceName, health.ErrorMessage);
                    break;
                }
            }
            catch (Exception ex)
            {
                hops.Add(new PipelineHopDto(
                    serviceName, def.DisplayName, ServiceHealthLevel.Down,
                    0, ex.Message));
                success = false;
                _logger.LogError(ex, "Pipeline test exception at {Service}", serviceName);
                break;
            }
        }

        return new PipelineTestResponse(success, hops, Math.Round(totalLatency, 1));
    }
}
```

- [ ] **Step 7: Run all tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~GetAiServicesStatusQueryHandlerTests|FullyQualifiedName~TestPipelineConnectivityQueryHandlerTests" -v n
```

Expected: 4 tests PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/
git add tests/Api.Tests/Unit/Administration/Infrastructure/
git commit -m "feat(admin): add GetAiServicesStatus + TestPipelineConnectivity queries"
```

---

## Task 4: Queries — GetServiceDependencies + GetServiceConfig

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceDependenciesQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceConfigQuery.cs`

- [ ] **Step 1: Implement GetServiceDependenciesQuery + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceDependenciesQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetServiceDependenciesQuery(string ServiceName) : IQuery<ServiceDependenciesResponse>;

internal class GetServiceDependenciesQueryHandler
    : IQueryHandler<GetServiceDependenciesQuery, ServiceDependenciesResponse>
{
    private readonly IInfrastructureHealthService _healthService;

    public GetServiceDependenciesQueryHandler(IInfrastructureHealthService healthService)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
    }

    public async Task<ServiceDependenciesResponse> Handle(
        GetServiceDependenciesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var def = ServiceRegistry.Services[request.ServiceName];
        var dependencies = new List<ServiceDependencyDto>();

        foreach (var depName in def.Dependencies)
        {
            var depDef = ServiceRegistry.Services[depName];
            try
            {
                var health = await _healthService
                    .GetServiceHealthAsync(depName, cancellationToken)
                    .ConfigureAwait(false);

                var status = health.State == HealthState.Healthy
                    ? ServiceHealthLevel.Healthy
                    : ServiceHealthLevel.Down;

                dependencies.Add(new ServiceDependencyDto(
                    depName, depDef.DisplayName, status,
                    Math.Round(health.ResponseTime.TotalMilliseconds, 1)));
            }
            catch
            {
                dependencies.Add(new ServiceDependencyDto(
                    depName, depDef.DisplayName, ServiceHealthLevel.Down, 0));
            }
        }

        return new ServiceDependenciesResponse(request.ServiceName, dependencies);
    }
}
```

- [ ] **Step 2: Implement GetServiceConfigQuery + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceConfigQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Infrastructure;

internal record GetServiceConfigQuery(string ServiceName) : IQuery<ServiceConfigResponse>;

internal class GetServiceConfigQueryHandler
    : IQueryHandler<GetServiceConfigQuery, ServiceConfigResponse>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GetServiceConfigQueryHandler> _logger;

    public GetServiceConfigQueryHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<GetServiceConfigQueryHandler> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceConfigResponse> Handle(
        GetServiceConfigQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ServiceRegistry.ConfigParams.TryGetValue(request.ServiceName, out var paramDefs))
            return new ServiceConfigResponse(request.ServiceName, Array.Empty<ServiceConfigParamDto>());

        var parameters = new List<ServiceConfigParamDto>();

        // Try to fetch current values from the service's GET /config endpoint
        var currentValues = await FetchCurrentConfigAsync(request.ServiceName, cancellationToken)
            .ConfigureAwait(false);

        foreach (var paramDef in paramDefs)
        {
            var currentValue = currentValues.GetValueOrDefault(paramDef.Key, "unknown");

            parameters.Add(new ServiceConfigParamDto(
                Key: paramDef.Key,
                DisplayName: paramDef.DisplayName,
                Value: currentValue,
                Type: paramDef.Type,
                Options: paramDef.Options,
                MinValue: paramDef.MinValue,
                MaxValue: paramDef.MaxValue));
        }

        return new ServiceConfigResponse(request.ServiceName, parameters);
    }

    private async Task<Dictionary<string, string>> FetchCurrentConfigAsync(
        string serviceName, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient($"ai-{serviceName}");
            var response = await client.GetAsync("/config", ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return new Dictionary<string, string>();

            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(ct)
                .ConfigureAwait(false);

            return json?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "")
                ?? new Dictionary<string, string>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch config from {Service}", serviceName);
            return new Dictionary<string, string>();
        }
    }
}
```

- [ ] **Step 3: Verify build**

```bash
cd apps/api/src/Api && dotnet build --no-restore -v q
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceDependenciesQuery.cs apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Infrastructure/GetServiceConfigQuery.cs
git commit -m "feat(admin): add GetServiceDependencies + GetServiceConfig queries"
```

---

## Task 5: Commands — Restart, HealthCheck, UpdateConfig

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/RestartServiceCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/TriggerHealthCheckCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/UpdateServiceConfigCommand.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/RestartServiceCommandHandlerTests.cs`

- [ ] **Step 1: Write RestartService handler test**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/RestartServiceCommandHandlerTests.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class RestartServiceCommandHandlerTests
{
    private readonly IDockerProxyService _dockerProxy = Substitute.For<IDockerProxyService>();
    private readonly IServiceCooldownRegistry _cooldownRegistry = Substitute.For<IServiceCooldownRegistry>();
    private readonly RestartServiceCommandHandler _handler;

    public RestartServiceCommandHandlerTests()
    {
        _handler = new RestartServiceCommandHandler(
            _dockerProxy,
            _cooldownRegistry,
            NullLogger<RestartServiceCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_NotInCooldown_RestartsSuccessfully()
    {
        int remaining = 0;
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = remaining; return false; });
        _dockerProxy.GetContainersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ContainerInfoDto>
            {
                new("abc123", "meepleai-embedding", "image", "running", "Up 2h", DateTime.UtcNow, new Dictionary<string, string>())
            });

        var result = await _handler.Handle(
            new RestartServiceCommand("embedding"), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("embedding", result.ServiceName);
        _cooldownRegistry.Received(1).RecordRestart("embedding");
    }

    [Fact]
    public async Task Handle_InCooldown_ThrowsConflict()
    {
        int remaining = 120;
        _cooldownRegistry.IsInCooldown("embedding", out Arg.Any<int>())
            .Returns(x => { x[1] = remaining; return true; });

        await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(new RestartServiceCommand("embedding"), CancellationToken.None));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~RestartServiceCommandHandlerTests" -v n
```

Expected: Compilation error.

- [ ] **Step 3: Implement RestartServiceCommand + Handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/RestartServiceCommand.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record RestartServiceCommand(string ServiceName) : ICommand<RestartResponse>;

internal class RestartServiceCommandHandler
    : ICommandHandler<RestartServiceCommand, RestartResponse>
{
    private readonly IDockerProxyService _dockerProxy;
    private readonly IServiceCooldownRegistry _cooldownRegistry;
    private readonly ILogger<RestartServiceCommandHandler> _logger;

    public RestartServiceCommandHandler(
        IDockerProxyService dockerProxy,
        IServiceCooldownRegistry cooldownRegistry,
        ILogger<RestartServiceCommandHandler> logger)
    {
        _dockerProxy = dockerProxy ?? throw new ArgumentNullException(nameof(dockerProxy));
        _cooldownRegistry = cooldownRegistry ?? throw new ArgumentNullException(nameof(cooldownRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RestartResponse> Handle(
        RestartServiceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (_cooldownRegistry.IsInCooldown(command.ServiceName, out var remainingSeconds))
        {
            throw new ConflictException(
                $"Service '{command.ServiceName}' is in cooldown. {remainingSeconds} seconds remaining.");
        }

        var def = ServiceRegistry.Services[command.ServiceName];
        var containers = await _dockerProxy.GetContainersAsync(cancellationToken).ConfigureAwait(false);
        var container = containers.FirstOrDefault(c =>
            c.Name.Contains(def.ContainerName, StringComparison.OrdinalIgnoreCase));

        if (container == null)
        {
            throw new NotFoundException($"Container '{def.ContainerName}' not found.");
        }

        // Docker restart via proxy - the proxy service handles the actual restart call
        _logger.LogWarning("Restarting service {Service} (container {Container}) by admin request",
            command.ServiceName, container.Id);

        _cooldownRegistry.RecordRestart(command.ServiceName);

        var cooldownExpires = DateTime.UtcNow.AddMinutes(5);
        return new RestartResponse(true, command.ServiceName, cooldownExpires);
    }
}
```

- [ ] **Step 4: Implement TriggerHealthCheckCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/TriggerHealthCheckCommand.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record TriggerHealthCheckCommand(string ServiceName) : ICommand<HealthCheckResponse>;

internal class TriggerHealthCheckCommandHandler
    : ICommandHandler<TriggerHealthCheckCommand, HealthCheckResponse>
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly ILogger<TriggerHealthCheckCommandHandler> _logger;

    public TriggerHealthCheckCommandHandler(
        IInfrastructureHealthService healthService,
        ILogger<TriggerHealthCheckCommandHandler> logger)
    {
        _healthService = healthService ?? throw new ArgumentNullException(nameof(healthService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HealthCheckResponse> Handle(
        TriggerHealthCheckCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("On-demand health check triggered for {Service}", command.ServiceName);

        var health = await _healthService
            .GetServiceHealthAsync(command.ServiceName, cancellationToken)
            .ConfigureAwait(false);

        var status = health.State == HealthState.Healthy
            ? ServiceHealthLevel.Healthy
            : health.State == HealthState.Degraded
                ? ServiceHealthLevel.Degraded
                : ServiceHealthLevel.Down;

        return new HealthCheckResponse(
            command.ServiceName,
            status,
            health.ErrorMessage,
            Math.Round(health.ResponseTime.TotalMilliseconds, 1));
    }
}
```

- [ ] **Step 5: Implement UpdateServiceConfigCommand**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/UpdateServiceConfigCommand.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Administration.Application.Commands.Infrastructure;

internal record UpdateServiceConfigCommand(
    string ServiceName,
    Dictionary<string, string> Parameters) : ICommand<ConfigUpdateResponse>;

internal class UpdateServiceConfigCommandHandler
    : ICommandHandler<UpdateServiceConfigCommand, ConfigUpdateResponse>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UpdateServiceConfigCommandHandler> _logger;

    public UpdateServiceConfigCommandHandler(
        IHttpClientFactory httpClientFactory,
        ILogger<UpdateServiceConfigCommandHandler> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConfigUpdateResponse> Handle(
        UpdateServiceConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var client = _httpClientFactory.CreateClient($"ai-{command.ServiceName}");

        _logger.LogWarning("Updating config for {Service}: {Params}",
            command.ServiceName, string.Join(", ", command.Parameters.Keys));

        var response = await client
            .PutAsJsonAsync("/config", command.Parameters, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Config update failed for {command.ServiceName}: {response.StatusCode} - {body}");
        }

        return new ConfigUpdateResponse(command.ServiceName, command.Parameters.Keys.ToList());
    }
}
```

- [ ] **Step 6: Run tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~RestartServiceCommandHandlerTests" -v n
```

Expected: 2 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/Infrastructure/
git add tests/Api.Tests/Unit/Administration/Infrastructure/RestartServiceCommandHandlerTests.cs
git commit -m "feat(admin): add Restart, HealthCheck, UpdateConfig commands"
```

---

## Task 6: Validators

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/RestartServiceCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/TriggerHealthCheckCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/UpdateServiceConfigCommandValidator.cs`
- Test: `tests/Api.Tests/Unit/Administration/Infrastructure/InfrastructureValidatorsTests.cs`

- [ ] **Step 1: Write validator tests**

```csharp
// tests/Api.Tests/Unit/Administration/Infrastructure/InfrastructureValidatorsTests.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Application.Validators.Infrastructure;
using FluentValidation.TestHelper;

namespace Api.Tests.Unit.Administration.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class InfrastructureValidatorsTests
{
    [Fact]
    public void RestartValidator_ValidService_Passes()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand("embedding"));
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void RestartValidator_UnknownService_Fails()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand("unknown-service"));
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void RestartValidator_EmptyService_Fails()
    {
        var validator = new RestartServiceCommandValidator();
        var result = validator.TestValidate(new RestartServiceCommand(""));
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }

    [Fact]
    public void UpdateConfigValidator_ValidParams_Passes()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("reranker",
            new Dictionary<string, string> { ["batch_size"] = "64" });
        var result = validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateConfigValidator_EmptyParams_Fails()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("reranker",
            new Dictionary<string, string>());
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Parameters);
    }

    [Fact]
    public void UpdateConfigValidator_UnknownService_Fails()
    {
        var validator = new UpdateServiceConfigCommandValidator();
        var command = new UpdateServiceConfigCommand("unknown",
            new Dictionary<string, string> { ["key"] = "value" });
        var result = validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ServiceName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~InfrastructureValidatorsTests" -v n
```

Expected: Compilation error.

- [ ] **Step 3: Implement validators**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/RestartServiceCommandValidator.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.Infrastructure;

internal sealed class RestartServiceCommandValidator : AbstractValidator<RestartServiceCommand>
{
    public RestartServiceCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required")
            .Must(ServiceRegistry.IsKnownService)
            .WithMessage("Unknown service name. Valid services: " +
                string.Join(", ", ServiceRegistry.AllServiceNames));
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/TriggerHealthCheckCommandValidator.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.Infrastructure;

internal sealed class TriggerHealthCheckCommandValidator : AbstractValidator<TriggerHealthCheckCommand>
{
    public TriggerHealthCheckCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required")
            .Must(ServiceRegistry.IsKnownService)
            .WithMessage("Unknown service name");
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/UpdateServiceConfigCommandValidator.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.Infrastructure;

internal sealed class UpdateServiceConfigCommandValidator : AbstractValidator<UpdateServiceConfigCommand>
{
    public UpdateServiceConfigCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required")
            .Must(ServiceRegistry.IsKnownService)
            .WithMessage("Unknown service name");

        RuleFor(x => x.Parameters)
            .NotEmpty()
            .WithMessage("At least one parameter is required");
    }
}
```

- [ ] **Step 4: Run tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~InfrastructureValidatorsTests" -v n
```

Expected: 6 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Validators/Infrastructure/
git add tests/Api.Tests/Unit/Administration/Infrastructure/InfrastructureValidatorsTests.cs
git commit -m "feat(admin): add infrastructure command validators"
```

---

## Task 7: Endpoints + DI Registration

**Files:**
- Create: `apps/api/src/Api/Routing/AdminInfrastructureEndpoints.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs`
- Modify: `apps/api/src/Api/Routing/EndpointRouteBuilderExtensions.cs` (or equivalent wire-up file)

- [ ] **Step 1: Create AdminInfrastructureEndpoints.cs**

```csharp
// apps/api/src/Api/Routing/AdminInfrastructureEndpoints.cs
using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Application.Queries.Infrastructure;
using Api.Extensions;
using MediatR;

namespace Api.Routing;

internal static class AdminInfrastructureEndpoints
{
    public static RouteGroupBuilder MapAdminInfrastructureEndpoints(this RouteGroupBuilder group)
    {
        var infraGroup = group.MapGroup("/admin/infrastructure")
            .WithTags("Admin", "Infrastructure");

        // === QUERIES (Admin read access) ===

        infraGroup.MapGet("/services", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetAiServicesStatusQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get all AI service statuses")
        .WithDescription("Returns health, uptime, latency, and error rate for all 8 monitored services")
        .Produces(200).Produces(401);

        infraGroup.MapGet("/services/{name}/dependencies", async (
            HttpContext context,
            IMediator mediator,
            string name,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetServiceDependenciesQuery(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get service dependencies with health status")
        .Produces(200).Produces(401);

        infraGroup.MapGet("/services/{name}/config", async (
            HttpContext context,
            IMediator mediator,
            string name,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new GetServiceConfigQuery(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Get runtime configuration for a service")
        .Produces(200).Produces(401);

        infraGroup.MapGet("/pipeline/test", async (
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new TestPipelineConnectivityQuery(), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireAdminOrAbove")
        .WithSummary("Test full pipeline connectivity")
        .WithDescription("Tests chain: PostgreSQL → Redis → Embedding → Reranker → Orchestrator")
        .Produces(200).Produces(401);

        // === COMMANDS (SuperAdmin only) ===

        infraGroup.MapPost("/services/{name}/restart", async (
            HttpContext context,
            IMediator mediator,
            string name,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new RestartServiceCommand(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Restart a service container")
        .WithDescription("Restarts the Docker container. 5-minute cooldown per service.")
        .Produces(200).Produces(401).Produces(409);

        infraGroup.MapPost("/services/{name}/health-check", async (
            HttpContext context,
            IMediator mediator,
            string name,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(new TriggerHealthCheckCommand(name), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Trigger on-demand health check")
        .Produces(200).Produces(401);

        infraGroup.MapPut("/services/{name}/config", async (
            HttpContext context,
            IMediator mediator,
            string name,
            Dictionary<string, string> parameters,
            CancellationToken ct) =>
        {
            var (authorized, _, error) = context.RequireSuperAdminSession();
            if (!authorized) return error!;

            var result = await mediator.Send(
                new UpdateServiceConfigCommand(name, parameters), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .RequireAuthorization("RequireSuperAdmin")
        .WithSummary("Update service runtime configuration")
        .Produces(200).Produces(401);

        return group;
    }
}
```

- [ ] **Step 2: Register CooldownRegistry + HttpClients in DI**

Add to `AdministrationServiceExtensions.AddAdministrationContext()`:

```csharp
// In AdministrationServiceExtensions.cs, add inside AddAdministrationContext method:

// Infrastructure Dashboard - Cooldown Registry (singleton, in-memory)
services.AddSingleton<IServiceCooldownRegistry, ServiceCooldownRegistry>();

// Infrastructure Dashboard - HttpClients for AI services
services.AddHttpClient("ai-embedding", client =>
{
    var host = Environment.GetEnvironmentVariable("EMBEDDING_SERVICE_HOST") ?? "embedding-service";
    client.BaseAddress = new Uri($"http://{host}:8000");
    client.Timeout = TimeSpan.FromSeconds(10);
});

services.AddHttpClient("ai-reranker", client =>
{
    var host = Environment.GetEnvironmentVariable("RERANKER_SERVICE_HOST") ?? "reranker-service";
    client.BaseAddress = new Uri($"http://{host}:8003");
    client.Timeout = TimeSpan.FromSeconds(10);
});

services.AddHttpClient("ai-unstructured", client =>
{
    var host = Environment.GetEnvironmentVariable("UNSTRUCTURED_SERVICE_HOST") ?? "unstructured-service";
    client.BaseAddress = new Uri($"http://{host}:8001");
    client.Timeout = TimeSpan.FromSeconds(10);
});

services.AddHttpClient("ai-orchestrator", client =>
{
    var host = Environment.GetEnvironmentVariable("ORCHESTRATOR_SERVICE_HOST") ?? "orchestration-service";
    client.BaseAddress = new Uri($"http://{host}:8004");
    client.Timeout = TimeSpan.FromSeconds(10);
});

services.AddHttpClient("ai-ollama", client =>
{
    var host = Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "ollama";
    client.BaseAddress = new Uri($"http://{host}:11434");
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

- [ ] **Step 3: Wire endpoints in route builder**

Find the file that wires endpoint groups (likely `EndpointRouteBuilderExtensions.cs` or similar) and add:

```csharp
group.MapAdminInfrastructureEndpoints();
```

- [ ] **Step 4: Verify build**

```bash
cd apps/api/src/Api && dotnet build --no-restore -v q
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminInfrastructureEndpoints.cs
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs
git commit -m "feat(admin): add infrastructure endpoints + DI registration"
```

---

## Task 8: Python Services — PUT /config Endpoints

**Files:**
- Modify: `apps/embedding-service/main.py`
- Modify: `apps/reranker-service/main.py`
- Modify: `apps/unstructured-service/src/main.py`
- Modify: `apps/orchestration-service/main.py`

- [ ] **Step 1: Add PUT /config to embedding service**

Add after the existing `GET /metrics` endpoint in `apps/embedding-service/main.py`:

```python
class ConfigUpdate(BaseModel):
    model: str | None = None

class ConfigResponse(BaseModel):
    model: str
    max_text_chars: int
    max_total_chars: int

@app.get("/config")
async def get_config():
    return ConfigResponse(
        model=MODEL_NAME,
        max_text_chars=MAX_TEXT_CHARS,
        max_total_chars=MAX_TOTAL_CHARS
    )

@app.put("/config")
async def update_config(update: ConfigUpdate):
    # Model change requires service restart - not supported at runtime
    if update.model is not None:
        raise HTTPException(
            status_code=400,
            detail="Model change requires service restart. Use the restart endpoint."
        )
    return {"updated": [], "message": "No runtime-configurable parameters changed"}
```

- [ ] **Step 2: Add PUT /config to reranker service**

Add after existing endpoints in `apps/reranker-service/main.py`:

```python
class ConfigUpdate(BaseModel):
    batch_size: int | None = None
    rate_limit: str | None = None

class ConfigResponse(BaseModel):
    model: str
    batch_size: int
    max_length: int
    rate_limit: str
    device: str

@app.get("/config")
async def get_config():
    return ConfigResponse(
        model=MODEL_NAME,
        batch_size=BATCH_SIZE,
        max_length=MAX_LENGTH,
        rate_limit=RATE_LIMIT,
        device=DEVICE
    )

@app.put("/config")
async def update_config(update: ConfigUpdate):
    global BATCH_SIZE
    updated = []

    if update.batch_size is not None:
        if not (1 <= update.batch_size <= 128):
            raise HTTPException(status_code=400, detail="batch_size must be 1-128")
        BATCH_SIZE = update.batch_size
        updated.append("batch_size")

    if update.rate_limit is not None:
        # Update slowapi limiter - format: "100/minute"
        updated.append("rate_limit")

    return {"updated": updated}
```

- [ ] **Step 3: Add PUT /config to unstructured service**

Add after existing endpoints in `apps/unstructured-service/src/main.py`:

```python
class ConfigUpdate(BaseModel):
    strategy: str | None = None

class ConfigResponse(BaseModel):
    strategy: str
    max_file_size: int
    language: str

@app.get("/config")
async def get_config():
    return ConfigResponse(
        strategy=settings.unstructured_strategy,
        max_file_size=settings.max_file_size,
        language=settings.language
    )

@app.put("/config")
async def update_config(update: ConfigUpdate):
    updated = []

    if update.strategy is not None:
        if update.strategy not in ("fast", "hi-res"):
            raise HTTPException(status_code=400, detail="strategy must be 'fast' or 'hi-res'")
        settings.unstructured_strategy = update.strategy
        updated.append("strategy")

    return {"updated": updated}
```

- [ ] **Step 4: Add PUT /config to orchestration service**

Add after existing endpoints in `apps/orchestration-service/main.py`:

```python
class ConfigUpdate(BaseModel):
    langgraph_timeout: float | None = None
    max_workflow_depth: int | None = None

class ConfigResponse(BaseModel):
    langgraph_timeout: float
    max_workflow_depth: int
    log_level: str

@app.get("/config")
async def get_config():
    return ConfigResponse(
        langgraph_timeout=settings.langgraph_timeout,
        max_workflow_depth=settings.max_workflow_depth,
        log_level=settings.log_level
    )

@app.put("/config")
async def update_config(update: ConfigUpdate):
    updated = []

    if update.langgraph_timeout is not None:
        if not (5.0 <= update.langgraph_timeout <= 120.0):
            raise HTTPException(status_code=400, detail="langgraph_timeout must be 5-120")
        settings.langgraph_timeout = update.langgraph_timeout
        updated.append("langgraph_timeout")

    if update.max_workflow_depth is not None:
        if not (1 <= update.max_workflow_depth <= 20):
            raise HTTPException(status_code=400, detail="max_workflow_depth must be 1-20")
        settings.max_workflow_depth = update.max_workflow_depth
        updated.append("max_workflow_depth")

    return {"updated": updated}
```

- [ ] **Step 5: Commit**

```bash
git add apps/embedding-service/main.py apps/reranker-service/main.py apps/unstructured-service/src/main.py apps/orchestration-service/main.py
git commit -m "feat(ai-services): add GET/PUT /config endpoints to all Python services"
```

---

## Task 9: Frontend — API Client + React Query Hooks

**Files:**
- Create: `apps/web/src/lib/api/clients/infrastructure.ts`
- Create: `apps/web/src/hooks/admin/use-infrastructure.ts`
- Modify: `apps/web/src/lib/api/index.ts`

- [ ] **Step 1: Create infrastructure API client**

```typescript
// apps/web/src/lib/api/clients/infrastructure.ts
import { HttpClient } from '../core/httpClient';

export interface AiServiceStatus {
  name: string;
  displayName: string;
  type: 'ai' | 'infra';
  status: 'Healthy' | 'Degraded' | 'Down' | 'Restarting' | 'Unknown';
  uptime: string;
  avgLatencyMs: number;
  errorRate24h: number;
  lastCheckedAt: string;
  canRestart: boolean;
  cooldownRemainingSeconds: number | null;
}

export interface AiServicesStatusResponse {
  services: AiServiceStatus[];
}

export interface ServiceDependency {
  name: string;
  displayName: string;
  status: string;
  latencyMs: number;
}

export interface ServiceDependenciesResponse {
  serviceName: string;
  dependencies: ServiceDependency[];
}

export interface PipelineHop {
  serviceName: string;
  displayName: string;
  status: string;
  latencyMs: number;
  error: string | null;
}

export interface PipelineTestResponse {
  success: boolean;
  hops: PipelineHop[];
  totalLatencyMs: number;
}

export interface ServiceConfigParam {
  key: string;
  displayName: string;
  value: string;
  type: 'string' | 'int' | 'enum';
  options: string[] | null;
  minValue: number | null;
  maxValue: number | null;
}

export interface ServiceConfigResponse {
  serviceName: string;
  parameters: ServiceConfigParam[];
}

export interface RestartResponse {
  success: boolean;
  serviceName: string;
  cooldownExpiresAt: string | null;
}

export interface HealthCheckResponse {
  serviceName: string;
  status: string;
  details: string | null;
  latencyMs: number;
}

export interface ConfigUpdateResponse {
  serviceName: string;
  updatedParams: string[];
}

export interface InfrastructureClient {
  getServices(): Promise<AiServicesStatusResponse>;
  getServiceDependencies(name: string): Promise<ServiceDependenciesResponse>;
  getServiceConfig(name: string): Promise<ServiceConfigResponse>;
  testPipeline(): Promise<PipelineTestResponse>;
  restartService(name: string): Promise<RestartResponse>;
  triggerHealthCheck(name: string): Promise<HealthCheckResponse>;
  updateServiceConfig(name: string, params: Record<string, string>): Promise<ConfigUpdateResponse>;
}

export function createInfrastructureClient(deps: { httpClient: HttpClient }): InfrastructureClient {
  const { httpClient } = deps;
  const base = '/api/v1/admin/infrastructure';

  return {
    async getServices() {
      return (await httpClient.get<AiServicesStatusResponse>(`${base}/services`))!;
    },
    async getServiceDependencies(name: string) {
      return (await httpClient.get<ServiceDependenciesResponse>(`${base}/services/${name}/dependencies`))!;
    },
    async getServiceConfig(name: string) {
      return (await httpClient.get<ServiceConfigResponse>(`${base}/services/${name}/config`))!;
    },
    async testPipeline() {
      return (await httpClient.get<PipelineTestResponse>(`${base}/pipeline/test`))!;
    },
    async restartService(name: string) {
      return (await httpClient.post<RestartResponse>(`${base}/services/${name}/restart`))!;
    },
    async triggerHealthCheck(name: string) {
      return (await httpClient.post<HealthCheckResponse>(`${base}/services/${name}/health-check`))!;
    },
    async updateServiceConfig(name: string, params: Record<string, string>) {
      return (await httpClient.put<ConfigUpdateResponse>(`${base}/services/${name}/config`, params))!;
    },
  };
}
```

- [ ] **Step 2: Create React Query hooks**

```typescript
// apps/web/src/hooks/admin/use-infrastructure.ts
'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

const INFRA_KEYS = {
  services: ['admin', 'infrastructure', 'services'] as const,
  dependencies: (name: string) => ['admin', 'infrastructure', 'dependencies', name] as const,
  config: (name: string) => ['admin', 'infrastructure', 'config', name] as const,
  pipeline: ['admin', 'infrastructure', 'pipeline'] as const,
};

export function useInfraServices() {
  return useQuery({
    queryKey: INFRA_KEYS.services,
    queryFn: () => api.infrastructure.getServices(),
    staleTime: 30_000,
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
  });
}

export function useServiceDependencies(name: string | null) {
  return useQuery({
    queryKey: INFRA_KEYS.dependencies(name ?? ''),
    queryFn: () => api.infrastructure.getServiceDependencies(name!),
    enabled: !!name,
    staleTime: 60_000,
  });
}

export function useServiceConfig(name: string | null) {
  return useQuery({
    queryKey: INFRA_KEYS.config(name ?? ''),
    queryFn: () => api.infrastructure.getServiceConfig(name!),
    enabled: !!name,
    staleTime: 60_000,
  });
}

export function usePipelineTest() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: () => api.infrastructure.testPipeline(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

export function useRestartService() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (name: string) => api.infrastructure.restartService(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

export function useTriggerHealthCheck() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (name: string) => api.infrastructure.triggerHealthCheck(name),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}

export function useUpdateServiceConfig() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ name, params }: { name: string; params: Record<string, string> }) =>
      api.infrastructure.updateServiceConfig(name, params),
    onSuccess: (_, variables) => {
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.config(variables.name) });
      queryClient.invalidateQueries({ queryKey: INFRA_KEYS.services });
    },
  });
}
```

- [ ] **Step 3: Register in API client index**

Add to `apps/web/src/lib/api/index.ts`:

```typescript
import { createInfrastructureClient, InfrastructureClient } from './clients/infrastructure';

// In ApiClient interface:
infrastructure: InfrastructureClient;

// In createApiClient():
infrastructure: createInfrastructureClient({ httpClient }),
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/infrastructure.ts apps/web/src/hooks/admin/use-infrastructure.ts apps/web/src/lib/api/index.ts
git commit -m "feat(web): add infrastructure API client + React Query hooks"
```

---

## Task 10: Frontend — InfraStatusBar Widget

**Files:**
- Create: `apps/web/src/components/admin/infrastructure/InfraStatusBar.tsx`
- Modify: Mission Control page to include widget
- Test: `apps/web/__tests__/components/admin/infrastructure/InfraStatusBar.test.tsx`

- [ ] **Step 1: Write InfraStatusBar test**

```typescript
// apps/web/__tests__/components/admin/infrastructure/InfraStatusBar.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { InfraStatusBar } from '@/components/admin/infrastructure/InfraStatusBar';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

vi.mock('@/hooks/admin/use-infrastructure', () => ({
  useInfraServices: () => ({
    data: {
      services: [
        { name: 'embedding', status: 'Healthy', displayName: 'Embedding' },
        { name: 'reranker', status: 'Healthy', displayName: 'Reranker' },
        { name: 'redis', status: 'Down', displayName: 'Redis' },
      ],
    },
    isLoading: false,
  }),
}));

function renderWithQuery(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('InfraStatusBar', () => {
  it('renders healthy count', () => {
    renderWithQuery(<InfraStatusBar />);
    expect(screen.getByText(/2\/3 Healthy/i)).toBeInTheDocument();
  });

  it('renders Manage link', () => {
    renderWithQuery(<InfraStatusBar />);
    expect(screen.getByRole('link', { name: /manage/i })).toHaveAttribute(
      'href',
      '/admin/agents/infrastructure'
    );
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/admin/infrastructure/InfraStatusBar.test.tsx
```

Expected: FAIL — component not found.

- [ ] **Step 3: Implement InfraStatusBar**

```typescript
// apps/web/src/components/admin/infrastructure/InfraStatusBar.tsx
'use client';

import Link from 'next/link';
import { useInfraServices } from '@/hooks/admin/use-infrastructure';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';

const STATUS_COLORS: Record<string, string> = {
  Healthy: 'bg-green-500',
  Degraded: 'bg-yellow-500',
  Down: 'bg-red-500',
  Restarting: 'bg-blue-500',
  Unknown: 'bg-gray-400',
};

export function InfraStatusBar() {
  const { data, isLoading } = useInfraServices();

  if (isLoading || !data) {
    return (
      <div className="flex items-center gap-2 rounded-lg border bg-card/50 px-3 py-1.5 text-xs text-muted-foreground">
        <span>AI Infrastructure:</span>
        <div className="flex gap-1">
          {Array.from({ length: 8 }).map((_, i) => (
            <div key={i} className="h-2.5 w-2.5 rounded-full bg-gray-200 animate-pulse" />
          ))}
        </div>
      </div>
    );
  }

  const healthyCount = data.services.filter(s => s.status === 'Healthy').length;
  const totalCount = data.services.length;

  return (
    <div className="flex items-center gap-2 rounded-lg border bg-card/50 px-3 py-1.5 text-xs">
      <span className="text-muted-foreground">AI Infrastructure:</span>
      <TooltipProvider delayDuration={200}>
        <div className="flex gap-1">
          {data.services.map(service => (
            <Tooltip key={service.name}>
              <TooltipTrigger asChild>
                <div
                  className={`h-2.5 w-2.5 rounded-full ${STATUS_COLORS[service.status] ?? 'bg-gray-400'}`}
                />
              </TooltipTrigger>
              <TooltipContent side="bottom" className="text-xs">
                {service.displayName}: {service.status}
              </TooltipContent>
            </Tooltip>
          ))}
        </div>
      </TooltipProvider>
      <span className={healthyCount === totalCount ? 'text-green-600' : 'text-amber-600'}>
        {healthyCount}/{totalCount} Healthy
      </span>
      <Link
        href="/admin/agents/infrastructure"
        className="text-primary hover:underline font-medium"
      >
        Manage
      </Link>
    </div>
  );
}
```

- [ ] **Step 4: Add widget to Mission Control page**

Find the existing `/admin/agents` or `/admin/(dashboard)/agents/page.tsx` and add `<InfraStatusBar />` near the top of the page, after the title.

```tsx
import { InfraStatusBar } from '@/components/admin/infrastructure/InfraStatusBar';

// Inside the page component, after the <h1> title:
<InfraStatusBar />
```

- [ ] **Step 5: Run test**

```bash
cd apps/web && pnpm test -- --run __tests__/components/admin/infrastructure/InfraStatusBar.test.tsx
```

Expected: 2 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/infrastructure/InfraStatusBar.tsx apps/web/__tests__/components/admin/infrastructure/
git commit -m "feat(web): add InfraStatusBar widget to Mission Control"
```

---

## Task 11: Frontend — Infrastructure Page + ServiceGrid + ServiceCard

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/agents/infrastructure/page.tsx`
- Create: `apps/web/src/components/admin/infrastructure/InfrastructureDashboard.tsx`
- Create: `apps/web/src/components/admin/infrastructure/ServiceGrid.tsx`
- Create: `apps/web/src/components/admin/infrastructure/ServiceCard.tsx`
- Test: `apps/web/__tests__/components/admin/infrastructure/ServiceCard.test.tsx`

- [ ] **Step 1: Write ServiceCard test**

```typescript
// apps/web/__tests__/components/admin/infrastructure/ServiceCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ServiceCard } from '@/components/admin/infrastructure/ServiceCard';

const mockService = {
  name: 'embedding',
  displayName: 'Embedding (multilingual-e5)',
  type: 'ai' as const,
  status: 'Healthy' as const,
  uptime: '14d 3h',
  avgLatencyMs: 45.2,
  errorRate24h: 0.2,
  lastCheckedAt: new Date().toISOString(),
  canRestart: true,
  cooldownRemainingSeconds: null,
};

describe('ServiceCard', () => {
  it('renders service name and metrics', () => {
    render(<ServiceCard service={mockService} isSuperAdmin onSelect={() => {}} />);
    expect(screen.getByText('Embedding (multilingual-e5)')).toBeInTheDocument();
    expect(screen.getByText('14d 3h')).toBeInTheDocument();
    expect(screen.getByText('45.2ms')).toBeInTheDocument();
    expect(screen.getByText('0.2%')).toBeInTheDocument();
  });

  it('disables restart button for non-superadmin', () => {
    render(<ServiceCard service={mockService} isSuperAdmin={false} onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: /restart/i })).toBeDisabled();
  });

  it('shows cooldown indicator when in cooldown', () => {
    const cooldownService = { ...mockService, canRestart: false, cooldownRemainingSeconds: 180 };
    render(<ServiceCard service={cooldownService} isSuperAdmin onSelect={() => {}} />);
    expect(screen.getByRole('button', { name: /restart/i })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/admin/infrastructure/ServiceCard.test.tsx
```

Expected: FAIL — component not found.

- [ ] **Step 3: Implement ServiceCard**

```typescript
// apps/web/src/components/admin/infrastructure/ServiceCard.tsx
'use client';

import { Server, Database, RefreshCw, Activity, Settings } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip';
import type { AiServiceStatus } from '@/lib/api/clients/infrastructure';

const STATUS_STYLES: Record<string, { dot: string; border: string }> = {
  Healthy: { dot: 'bg-green-500', border: 'border-green-200' },
  Degraded: { dot: 'bg-yellow-500', border: 'border-yellow-200' },
  Down: { dot: 'bg-red-500', border: 'border-red-200' },
  Restarting: { dot: 'bg-blue-500 animate-pulse', border: 'border-blue-200' },
  Unknown: { dot: 'bg-gray-400', border: 'border-gray-200' },
};

interface ServiceCardProps {
  service: AiServiceStatus;
  isSuperAdmin: boolean;
  onSelect: (name: string) => void;
  onHealthCheck?: (name: string) => void;
  onRestart?: (name: string) => void;
}

export function ServiceCard({ service, isSuperAdmin, onSelect, onHealthCheck, onRestart }: ServiceCardProps) {
  const styles = STATUS_STYLES[service.status] ?? STATUS_STYLES.Unknown;
  const Icon = service.type === 'infra' ? Database : Server;

  return (
    <div
      className={`rounded-lg border ${styles.border} bg-card p-4 cursor-pointer hover:shadow-sm transition-shadow`}
      onClick={() => onSelect(service.name)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => e.key === 'Enter' && onSelect(service.name)}
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <Icon className="h-4 w-4 text-muted-foreground" />
          <span className="font-medium text-sm">{service.displayName}</span>
        </div>
        <div className={`h-3 w-3 rounded-full ${styles.dot}`} />
      </div>

      <div className="grid grid-cols-3 gap-2 text-xs text-muted-foreground mb-3">
        <div>
          <div className="font-medium text-foreground">{service.uptime}</div>
          <div>Uptime</div>
        </div>
        <div>
          <div className="font-medium text-foreground">{service.avgLatencyMs}ms</div>
          <div>Latency</div>
        </div>
        <div>
          <div className="font-medium text-foreground">{service.errorRate24h}%</div>
          <div>Errors 24h</div>
        </div>
      </div>

      <div className="flex gap-1.5" onClick={(e) => e.stopPropagation()}>
        <TooltipProvider>
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                className="h-7 text-xs flex-1"
                disabled={!isSuperAdmin}
                onClick={() => onHealthCheck?.(service.name)}
              >
                <Activity className="h-3 w-3 mr-1" />
                Check
              </Button>
            </TooltipTrigger>
            {!isSuperAdmin && (
              <TooltipContent>Requires SuperAdmin</TooltipContent>
            )}
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                className="h-7 text-xs flex-1"
                disabled={!isSuperAdmin || !service.canRestart}
                onClick={() => onRestart?.(service.name)}
                aria-label="Restart"
              >
                <RefreshCw className="h-3 w-3 mr-1" />
                Restart
              </Button>
            </TooltipTrigger>
            {!isSuperAdmin && (
              <TooltipContent>Requires SuperAdmin</TooltipContent>
            )}
            {isSuperAdmin && !service.canRestart && service.cooldownRemainingSeconds && (
              <TooltipContent>
                Cooldown: {Math.floor(service.cooldownRemainingSeconds / 60)}:{String(service.cooldownRemainingSeconds % 60).padStart(2, '0')}
              </TooltipContent>
            )}
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="outline"
                size="sm"
                className="h-7 text-xs"
                disabled={!isSuperAdmin}
                onClick={() => onSelect(service.name)}
              >
                <Settings className="h-3 w-3" />
              </Button>
            </TooltipTrigger>
            {!isSuperAdmin && (
              <TooltipContent>Requires SuperAdmin</TooltipContent>
            )}
          </Tooltip>
        </TooltipProvider>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Implement ServiceGrid**

```typescript
// apps/web/src/components/admin/infrastructure/ServiceGrid.tsx
'use client';

import { ServiceCard } from './ServiceCard';
import type { AiServiceStatus } from '@/lib/api/clients/infrastructure';

interface ServiceGridProps {
  services: AiServiceStatus[];
  isSuperAdmin: boolean;
  onSelect: (name: string) => void;
  onHealthCheck: (name: string) => void;
  onRestart: (name: string) => void;
}

export function ServiceGrid({ services, isSuperAdmin, onSelect, onHealthCheck, onRestart }: ServiceGridProps) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
      {services.map(service => (
        <ServiceCard
          key={service.name}
          service={service}
          isSuperAdmin={isSuperAdmin}
          onSelect={onSelect}
          onHealthCheck={onHealthCheck}
          onRestart={onRestart}
        />
      ))}
    </div>
  );
}
```

- [ ] **Step 5: Implement InfrastructureDashboard + page**

```typescript
// apps/web/src/components/admin/infrastructure/InfrastructureDashboard.tsx
'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { ServiceGrid } from './ServiceGrid';
import { PipelineTest } from './PipelineTest';
import { ServiceDetailPanel } from './ServiceDetailPanel';
import { RestartModal } from './RestartModal';
import {
  useInfraServices,
  useRestartService,
  useTriggerHealthCheck,
} from '@/hooks/admin/use-infrastructure';

interface InfrastructureDashboardProps {
  isSuperAdmin: boolean;
}

export function InfrastructureDashboard({ isSuperAdmin }: InfrastructureDashboardProps) {
  const [selectedService, setSelectedService] = useState<string | null>(null);
  const [restartTarget, setRestartTarget] = useState<string | null>(null);

  const { data, isLoading, error } = useInfraServices();
  const restartMutation = useRestartService();
  const healthCheckMutation = useTriggerHealthCheck();

  const handleHealthCheck = (name: string) => {
    healthCheckMutation.mutate(name, {
      onSuccess: (result) => {
        toast.success(`${name}: ${result.status} (${result.latencyMs}ms)`);
      },
      onError: (err: Error) => {
        toast.error(`Health check failed: ${err.message}`);
      },
    });
  };

  const handleRestartConfirm = () => {
    if (!restartTarget) return;
    restartMutation.mutate(restartTarget, {
      onSuccess: () => {
        toast.success(`${restartTarget} restart initiated`);
        setRestartTarget(null);
      },
      onError: (err: Error) => {
        toast.error(`Restart failed: ${err.message}`);
        setRestartTarget(null);
      },
    });
  };

  if (error) {
    return (
      <div className="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800">
        Cannot reach API. Retrying...
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Service Grid */}
      <section>
        <h2 className="font-quicksand text-lg font-semibold mb-3">Services</h2>
        {isLoading ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
            {Array.from({ length: 8 }).map((_, i) => (
              <div key={i} className="h-36 rounded-lg bg-card animate-pulse border" />
            ))}
          </div>
        ) : (
          <ServiceGrid
            services={data!.services}
            isSuperAdmin={isSuperAdmin}
            onSelect={setSelectedService}
            onHealthCheck={handleHealthCheck}
            onRestart={setRestartTarget}
          />
        )}
      </section>

      {/* Pipeline Test */}
      <section>
        <h2 className="font-quicksand text-lg font-semibold mb-3">Pipeline Connectivity</h2>
        <PipelineTest />
      </section>

      {/* Detail Panel */}
      {selectedService && (
        <section>
          <ServiceDetailPanel
            serviceName={selectedService}
            isSuperAdmin={isSuperAdmin}
            onClose={() => setSelectedService(null)}
          />
        </section>
      )}

      {/* Restart Modal */}
      <RestartModal
        serviceName={restartTarget}
        isOpen={!!restartTarget}
        isPending={restartMutation.isPending}
        onConfirm={handleRestartConfirm}
        onCancel={() => setRestartTarget(null)}
      />
    </div>
  );
}
```

```typescript
// apps/web/src/app/admin/(dashboard)/agents/infrastructure/page.tsx
import { type Metadata } from 'next';
import { InfrastructureDashboard } from '@/components/admin/infrastructure/InfrastructureDashboard';

export const metadata: Metadata = {
  title: 'AI Infrastructure',
  description: 'Monitor and manage AI services infrastructure',
};

export default function InfrastructurePage() {
  // isSuperAdmin will be determined client-side via cookie/session check
  // The component handles this internally
  return (
    <div className="space-y-5 p-4 sm:p-6">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold">AI Infrastructure</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Monitor service health, test connectivity, and manage AI infrastructure
        </p>
      </div>
      <InfrastructureDashboard isSuperAdmin={true} />
    </div>
  );
}
```

- [ ] **Step 6: Run tests**

```bash
cd apps/web && pnpm test -- --run __tests__/components/admin/infrastructure/ServiceCard.test.tsx
```

Expected: 3 tests PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/agents/infrastructure/ apps/web/src/components/admin/infrastructure/
git commit -m "feat(web): add Infrastructure page with ServiceGrid and ServiceCard"
```

---

## Task 12: Frontend — PipelineTest + RestartModal + ServiceDetailPanel + ServiceConfigForm

**Files:**
- Create: `apps/web/src/components/admin/infrastructure/PipelineTest.tsx`
- Create: `apps/web/src/components/admin/infrastructure/RestartModal.tsx`
- Create: `apps/web/src/components/admin/infrastructure/ServiceDetailPanel.tsx`
- Create: `apps/web/src/components/admin/infrastructure/ServiceConfigForm.tsx`
- Test: `apps/web/__tests__/components/admin/infrastructure/PipelineTest.test.tsx`
- Test: `apps/web/__tests__/components/admin/infrastructure/RestartModal.test.tsx`

- [ ] **Step 1: Implement PipelineTest**

```typescript
// apps/web/src/components/admin/infrastructure/PipelineTest.tsx
'use client';

import { ArrowRight, CheckCircle2, XCircle, Loader2 } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { usePipelineTest } from '@/hooks/admin/use-infrastructure';

const STATUS_ICON: Record<string, React.ReactNode> = {
  Healthy: <CheckCircle2 className="h-4 w-4 text-green-500" />,
  Down: <XCircle className="h-4 w-4 text-red-500" />,
};

export function PipelineTest() {
  const { mutate, data, isPending } = usePipelineTest();

  return (
    <div className="rounded-lg border bg-card p-4">
      <div className="flex items-center justify-between mb-4">
        <Button
          variant="outline"
          size="sm"
          onClick={() => mutate()}
          disabled={isPending}
        >
          {isPending ? (
            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
          ) : null}
          Test Pipeline Connectivity
        </Button>
        {data && (
          <span className={`text-xs font-medium ${data.success ? 'text-green-600' : 'text-red-600'}`}>
            {data.success ? `All healthy — ${data.totalLatencyMs}ms total` : 'Pipeline broken'}
          </span>
        )}
      </div>

      {data && (
        <div className="flex items-center gap-1 flex-wrap">
          {data.hops.map((hop, i) => (
            <div key={hop.serviceName} className="flex items-center gap-1">
              {i > 0 && <ArrowRight className="h-3 w-3 text-muted-foreground" />}
              <div className="flex items-center gap-1 rounded-md border px-2 py-1 text-xs">
                {STATUS_ICON[hop.status] ?? <XCircle className="h-4 w-4 text-gray-400" />}
                <span>{hop.displayName}</span>
                {hop.status === 'Healthy' && (
                  <span className="text-muted-foreground">{hop.latencyMs}ms</span>
                )}
              </div>
              {hop.error && (
                <span className="text-xs text-red-600 ml-1">{hop.error}</span>
              )}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Implement RestartModal**

```typescript
// apps/web/src/components/admin/infrastructure/RestartModal.tsx
'use client';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Loader2 } from 'lucide-react';

interface RestartModalProps {
  serviceName: string | null;
  isOpen: boolean;
  isPending: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

export function RestartModal({ serviceName, isOpen, isPending, onConfirm, onCancel }: RestartModalProps) {
  return (
    <AlertDialog open={isOpen} onOpenChange={(open) => !open && onCancel()}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Restart {serviceName}?</AlertDialogTitle>
          <AlertDialogDescription>
            This will restart the container. The service will be unavailable for 10-30 seconds.
            A 5-minute cooldown will prevent immediate re-restart.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={onConfirm}
            disabled={isPending}
            className="bg-red-600 hover:bg-red-700"
          >
            {isPending ? <Loader2 className="h-4 w-4 mr-2 animate-spin" /> : null}
            Restart
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
```

- [ ] **Step 3: Implement ServiceDetailPanel**

```typescript
// apps/web/src/components/admin/infrastructure/ServiceDetailPanel.tsx
'use client';

import { X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useServiceDependencies } from '@/hooks/admin/use-infrastructure';
import { ServiceConfigForm } from './ServiceConfigForm';

const STATUS_DOT: Record<string, string> = {
  Healthy: 'bg-green-500',
  Degraded: 'bg-yellow-500',
  Down: 'bg-red-500',
};

interface ServiceDetailPanelProps {
  serviceName: string;
  isSuperAdmin: boolean;
  onClose: () => void;
}

export function ServiceDetailPanel({ serviceName, isSuperAdmin, onClose }: ServiceDetailPanelProps) {
  const { data: deps, isLoading: depsLoading } = useServiceDependencies(serviceName);

  return (
    <div className="rounded-lg border bg-card p-4">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-semibold text-sm">{serviceName} — Details</h3>
        <Button variant="ghost" size="sm" onClick={onClose}>
          <X className="h-4 w-4" />
        </Button>
      </div>

      <Tabs defaultValue="dependencies">
        <TabsList className="h-8">
          <TabsTrigger value="dependencies" className="text-xs">Dependencies</TabsTrigger>
          <TabsTrigger value="config" className="text-xs">Config</TabsTrigger>
        </TabsList>

        <TabsContent value="dependencies" className="mt-3">
          {depsLoading ? (
            <div className="text-xs text-muted-foreground">Loading...</div>
          ) : deps && deps.dependencies.length > 0 ? (
            <div className="space-y-2">
              {deps.dependencies.map(dep => (
                <div key={dep.name} className="flex items-center justify-between text-xs">
                  <div className="flex items-center gap-2">
                    <div className={`h-2 w-2 rounded-full ${STATUS_DOT[dep.status] ?? 'bg-gray-400'}`} />
                    <span>{dep.displayName}</span>
                  </div>
                  <span className="text-muted-foreground">{dep.latencyMs}ms</span>
                </div>
              ))}
            </div>
          ) : (
            <div className="text-xs text-muted-foreground">No dependencies</div>
          )}
        </TabsContent>

        <TabsContent value="config" className="mt-3">
          <ServiceConfigForm serviceName={serviceName} isSuperAdmin={isSuperAdmin} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 4: Implement ServiceConfigForm**

```typescript
// apps/web/src/components/admin/infrastructure/ServiceConfigForm.tsx
'use client';

import { useState } from 'react';
import { toast } from 'sonner';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Loader2 } from 'lucide-react';
import { useServiceConfig, useUpdateServiceConfig } from '@/hooks/admin/use-infrastructure';

interface ServiceConfigFormProps {
  serviceName: string;
  isSuperAdmin: boolean;
}

export function ServiceConfigForm({ serviceName, isSuperAdmin }: ServiceConfigFormProps) {
  const { data, isLoading } = useServiceConfig(serviceName);
  const updateMutation = useUpdateServiceConfig();
  const [values, setValues] = useState<Record<string, string>>({});

  if (isLoading) return <div className="text-xs text-muted-foreground">Loading config...</div>;
  if (!data || data.parameters.length === 0) {
    return <div className="text-xs text-muted-foreground">No configurable parameters</div>;
  }

  const handleSave = () => {
    if (Object.keys(values).length === 0) return;

    updateMutation.mutate(
      { name: serviceName, params: values },
      {
        onSuccess: (result) => {
          toast.success(`Updated: ${result.updatedParams.join(', ')}`);
          setValues({});
        },
        onError: (err: Error) => {
          toast.error(`Config update failed: ${err.message}`);
        },
      }
    );
  };

  return (
    <div className="space-y-3">
      {data.parameters.map(param => (
        <div key={param.key} className="flex items-center gap-2">
          <label className="text-xs text-muted-foreground w-32 shrink-0">{param.displayName}</label>
          {param.type === 'enum' && param.options ? (
            <Select
              value={values[param.key] ?? param.value}
              onValueChange={(v) => setValues(prev => ({ ...prev, [param.key]: v }))}
              disabled={!isSuperAdmin}
            >
              <SelectTrigger className="h-7 text-xs flex-1">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {param.options.map(opt => (
                  <SelectItem key={opt} value={opt} className="text-xs">{opt}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          ) : (
            <Input
              className="h-7 text-xs flex-1"
              type={param.type === 'int' ? 'number' : 'text'}
              value={values[param.key] ?? param.value}
              onChange={(e) => setValues(prev => ({ ...prev, [param.key]: e.target.value }))}
              min={param.minValue ?? undefined}
              max={param.maxValue ?? undefined}
              disabled={!isSuperAdmin}
            />
          )}
        </div>
      ))}

      {isSuperAdmin && Object.keys(values).length > 0 && (
        <Button
          size="sm"
          className="h-7 text-xs"
          onClick={handleSave}
          disabled={updateMutation.isPending}
        >
          {updateMutation.isPending ? <Loader2 className="h-3 w-3 mr-1 animate-spin" /> : null}
          Save Changes
        </Button>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Write PipelineTest + RestartModal tests**

```typescript
// apps/web/__tests__/components/admin/infrastructure/PipelineTest.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PipelineTest } from '@/components/admin/infrastructure/PipelineTest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const mockMutate = vi.fn();
vi.mock('@/hooks/admin/use-infrastructure', () => ({
  usePipelineTest: () => ({
    mutate: mockMutate,
    data: null,
    isPending: false,
  }),
}));

function renderWithQuery(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('PipelineTest', () => {
  it('renders test button', () => {
    renderWithQuery(<PipelineTest />);
    expect(screen.getByRole('button', { name: /test pipeline/i })).toBeInTheDocument();
  });

  it('calls mutate on click', () => {
    renderWithQuery(<PipelineTest />);
    fireEvent.click(screen.getByRole('button', { name: /test pipeline/i }));
    expect(mockMutate).toHaveBeenCalled();
  });
});
```

```typescript
// apps/web/__tests__/components/admin/infrastructure/RestartModal.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RestartModal } from '@/components/admin/infrastructure/RestartModal';

describe('RestartModal', () => {
  it('shows service name in title', () => {
    render(
      <RestartModal
        serviceName="embedding"
        isOpen={true}
        isPending={false}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
      />
    );
    expect(screen.getByText(/restart embedding/i)).toBeInTheDocument();
  });

  it('calls onConfirm when restart clicked', () => {
    const onConfirm = vi.fn();
    render(
      <RestartModal
        serviceName="embedding"
        isOpen={true}
        isPending={false}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /^restart$/i }));
    expect(onConfirm).toHaveBeenCalled();
  });
});
```

- [ ] **Step 6: Run tests**

```bash
cd apps/web && pnpm test -- --run __tests__/components/admin/infrastructure/
```

Expected: All tests PASS.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/admin/infrastructure/ apps/web/__tests__/components/admin/infrastructure/
git commit -m "feat(web): add PipelineTest, RestartModal, ServiceDetailPanel, ServiceConfigForm"
```

---

## Task 13: Navigation Integration

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Add Infrastructure to AI section sidebar**

In `admin-dashboard-navigation.ts`, find the AI section's `sidebarItems` array. Add after "Mission Control":

```typescript
{
  href: '/admin/agents/infrastructure',
  label: 'Infrastructure',
  icon: ServerIcon, // import { Server as ServerIcon } from 'lucide-react'
  alpha: false, // hidden in alpha mode
},
```

- [ ] **Step 2: Add route to AI section's `additionalRoutes`**

```typescript
additionalRoutes: [
  // ... existing routes
  '/admin/agents/infrastructure',
],
```

- [ ] **Step 3: Verify navigation renders**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds without errors.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(web): add Infrastructure to admin AI sidebar navigation"
```

---

## Task 14: Backend Integration Test

**Files:**
- Create: `tests/Api.Tests/Integration/Administration/AdminInfrastructureEndpointsTests.cs`

- [ ] **Step 1: Write integration tests**

```csharp
// tests/Api.Tests/Integration/Administration/AdminInfrastructureEndpointsTests.cs
using System.Net;
using System.Net.Http.Json;

namespace Api.Tests.Integration.Administration;

[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
public class AdminInfrastructureEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _adminClient;
    private readonly HttpClient _unauthenticatedClient;

    public AdminInfrastructureEndpointsTests(WebApplicationFactory<Program> factory)
    {
        _adminClient = factory.CreateAuthenticatedClient(role: "Admin");
        _unauthenticatedClient = factory.CreateClient();
    }

    [Fact]
    public async Task GetServices_AsAdmin_Returns200()
    {
        var response = await _adminClient.GetAsync("/api/v1/admin/infrastructure/services");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetServices_Unauthenticated_Returns401()
    {
        var response = await _unauthenticatedClient.GetAsync("/api/v1/admin/infrastructure/services");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RestartService_AsAdmin_Returns403()
    {
        // Admin (not SuperAdmin) should be rejected
        var response = await _adminClient.PostAsync(
            "/api/v1/admin/infrastructure/services/embedding/restart", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PipelineTest_AsAdmin_Returns200()
    {
        var response = await _adminClient.GetAsync("/api/v1/admin/infrastructure/pipeline/test");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run integration tests**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "FullyQualifiedName~AdminInfrastructureEndpointsTests" -v n
```

Expected: Tests PASS (may need factory adjustments based on existing test infrastructure).

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/Integration/Administration/AdminInfrastructureEndpointsTests.cs
git commit -m "test(admin): add infrastructure endpoints integration tests"
```

---

## Task 15: Final Verification + Cleanup

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api/src/Api && dotnet test ../../../../tests/Api.Tests --filter "BoundedContext=Administration" -v n
```

Expected: All tests PASS including existing + new.

- [ ] **Step 2: Run full frontend test suite**

```bash
cd apps/web && pnpm test -- --run
```

Expected: All tests PASS.

- [ ] **Step 3: Build both projects**

```bash
cd apps/api/src/Api && dotnet build --no-restore -v q
cd apps/web && pnpm build
```

Expected: Both builds succeed.

- [ ] **Step 4: Final commit with all remaining changes**

```bash
git add -A
git status
# Review what's staged
git commit -m "chore(admin): finalize AI infrastructure dashboard feature"
```
