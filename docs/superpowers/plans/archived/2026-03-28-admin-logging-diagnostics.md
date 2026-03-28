# Admin Logging, Diagnostics & Service Management — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give admins a unified view of application logs (via Seq), external service call history (via PostgreSQL), and enhanced service diagnostics (circuit breaker states, LLM costs, correlation drill-down) — all within the existing `/admin/monitor` section.

**Architecture:** Extend the Administration bounded context with three new capabilities: (1) a Seq HTTP client that queries structured Serilog logs, (2) a `DelegatingHandler` that records every named HttpClient call to a `service_call_logs` table, and (3) an in-memory circuit breaker state tracker fed by Polly callbacks. The frontend extends the existing monitor/logs page with tabs and adds a new service-calls sub-page.

**Tech Stack:** .NET 9 (ASP.NET Minimal APIs, MediatR, EF Core, Polly, Serilog.Sinks.Seq) | Next.js 16 (React 19, React Query, Zod, Tailwind 4, shadcn/ui) | Seq (datalust/seq Docker image) | PostgreSQL 16

---

## File Structure

### Phase 1 — Seq + Application Log Viewer

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `infra/docker-compose.yml` (modify) | Add Seq service to monitoring profile |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqQueryClient.cs` | HTTP client to Seq API `/api/events` |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqOptions.cs` | Config options for Seq URL |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQuery.cs` | Query + response records |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandler.cs` | Handler calling SeqQueryClient |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ApplicationLogDto.cs` | DTO for log entries |
| Create | `apps/api/src/Api/Routing/AdminLogEndpoints.cs` | `GET /api/v1/admin/logs` endpoint |
| Modify | `apps/api/src/Api/Logging/LoggingConfiguration.cs` | Add Seq sink |
| Modify | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` | Register SeqQueryClient |
| Modify | `apps/api/src/Api/appsettings.json` | Add `Seq` config section |
| Create | `apps/web/src/lib/api/schemas/admin/admin-logs.schemas.ts` | Zod schemas for log entries |
| Modify | `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts` | Add `getApplicationLogs()` method |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/logs/AppLogViewer.tsx` | Structured log viewer component |
| Modify | `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx` | Add tabs: Container Logs / App Logs |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandlerTests.cs` | Unit tests |

### Phase 2 — Service Call History

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/ServiceCallLogEntry.cs` | Domain entity |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Configurations/ServiceCallLogConfiguration.cs` | EF config |
| Create | `apps/api/src/Api/Infrastructure/Http/ServiceCallLoggingHandler.cs` | DelegatingHandler logging HTTP calls |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IServiceCallLogRepository.cs` | Repository interface |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Repositories/ServiceCallLogRepository.cs` | Repository impl |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQuery.cs` | Paginated query |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandler.cs` | Handler |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQuery.cs` | Aggregation query |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQueryHandler.cs` | Handler |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ServiceCallDto.cs` | DTOs |
| Create | `apps/api/src/Api/Routing/AdminServiceCallEndpoints.cs` | Endpoints |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/ServiceCallLogRetentionJob.cs` | 7-day cleanup |
| Modify | `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` | Register DelegatingHandler on all named clients |
| Modify | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` | Register repository + retention job |
| Modify | `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | Add DbSet |
| Create | EF Migration | `AddServiceCallLogs` |
| Create | `apps/web/src/lib/api/schemas/admin/admin-service-calls.schemas.ts` | Zod schemas |
| Modify | `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts` | Add service call API methods |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/service-calls/page.tsx` | Service call history page |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceCallHistory.tsx` | Main component |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceCallDetail.tsx` | Detail modal |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceSummaryCards.tsx` | Summary cards |
| Modify | `apps/web/src/config/admin-dashboard-navigation.ts` | Add "Service Calls" nav item |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandlerTests.cs` | Unit tests |
| Create | `apps/api/tests/Api.Tests/Infrastructure/Http/ServiceCallLoggingHandlerTests.cs` | Handler tests |

### Phase 3 — Enhanced Service Dashboard

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTracker.cs` | In-memory state tracker |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/CircuitBreakers/GetCircuitBreakerStatesQuery.cs` | Query + handler |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/CircuitBreakers/ResetCircuitBreakerCommand.cs` | Reset command + handler |
| Create | `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/CircuitBreakerStateDto.cs` | DTO |
| Create | `apps/api/src/Api/Routing/AdminCircuitBreakerEndpoints.cs` | Endpoints |
| Modify | `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs` | Wire Polly callbacks to state tracker |
| Modify | `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs` | Register state tracker |
| Create | `apps/web/src/lib/api/schemas/admin/admin-circuit-breakers.schemas.ts` | Zod schemas |
| Modify | `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts` | Add circuit breaker API methods |
| Modify | `apps/web/src/app/admin/(dashboard)/monitor/services/ServicesDashboard.tsx` | Add circuit breaker badges + cost panel |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/services/CircuitBreakerPanel.tsx` | Circuit breaker management panel |
| Create | `apps/web/src/app/admin/(dashboard)/monitor/services/ServiceCostPanel.tsx` | LLM cost breakdown panel |
| Create | `apps/api/tests/Api.Tests/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTrackerTests.cs` | Unit tests |

---

## Phase 1: Application Log Viewer (Seq)

### Task 1.1: Add Seq to Docker Compose

**Files:**
- Modify: `infra/docker-compose.yml`

- [ ] **Step 1: Add Seq service to monitoring profile**

In `infra/docker-compose.yml`, add after the `alertmanager` service block (before `node-exporter`):

```yaml
  seq:
    image: datalust/seq:2024.3
    container_name: meepleai-seq
    restart: unless-stopped
    profiles: [monitoring]
    environment:
      ACCEPT_EULA: "Y"
    volumes:
      - seq_data:/data
    healthcheck:
      test: ["CMD", "wget", "--spider", "-q", "http://localhost:80/api"]
      interval: 15s
      timeout: 5s
      retries: 5
      start_period: 10s
    networks:
      - meepleai
    deploy:
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
```

Add `seq_data:` to the `volumes:` section at the bottom of the file.

- [ ] **Step 2: Add Seq ingestion URL to API service environment**

In the `api` service section of `docker-compose.yml`, add to the `environment` block:

```yaml
      SEQ_URL: "http://seq:5341"
```

- [ ] **Step 3: Verify Seq starts correctly**

```bash
cd infra && docker compose --profile monitoring up seq -d
```

Expected: Container `meepleai-seq` starts. Seq UI accessible at `http://localhost:8341` (if port mapped) or via internal network at `http://seq:80`.

- [ ] **Step 4: Commit**

```bash
git add infra/docker-compose.yml
git commit -m "infra: add Seq structured log aggregator to monitoring profile"
```

---

### Task 1.2: Configure Serilog Seq Sink

**Files:**
- Modify: `apps/api/src/Api/Logging/LoggingConfiguration.cs`
- Modify: `apps/api/src/Api/appsettings.json`
- Modify: `apps/api/src/Api/Api.csproj` (if NuGet not already present)

- [ ] **Step 1: Add Serilog.Sinks.Seq NuGet package**

```bash
cd apps/api/src/Api && dotnet add package Serilog.Sinks.Seq
```

Expected: Package added to `Api.csproj`.

- [ ] **Step 2: Add Seq config section to appsettings.json**

In `apps/api/src/Api/appsettings.json`, add after the `"Prometheus"` section:

```json
  "Seq": {
    "ServerUrl": "http://seq:5341",
    "ApiKey": "",
    "MinimumLevel": "Information",
    "RetentionDays": 7
  },
```

- [ ] **Step 3: Add Seq sink to LoggingConfiguration.cs**

In `apps/api/src/Api/Logging/LoggingConfiguration.cs`, find the method that configures `WriteTo` sinks and add the Seq sink conditionally:

```csharp
var seqUrl = configuration["Seq:ServerUrl"]
          ?? Environment.GetEnvironmentVariable("SEQ_URL");
if (!string.IsNullOrEmpty(seqUrl))
{
    loggerConfig.WriteTo.Seq(
        serverUrl: seqUrl,
        apiKey: configuration["Seq:ApiKey"],
        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Information);
}
```

This ensures Seq sink is only added when configured (doesn't break local dev without Seq).

- [ ] **Step 4: Verify logs flow to Seq**

```bash
cd infra && docker compose --profile monitoring up seq api -d
# Wait 10 seconds for API to start
pwsh -c "docker logs meepleai-api --tail=5"
```

Expected: API starts successfully. Seq receives structured log events (verify at Seq UI or via `curl http://localhost:5341/api/events?count=5`).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Api.csproj apps/api/src/Api/appsettings.json apps/api/src/Api/Logging/LoggingConfiguration.cs
git commit -m "feat(logging): add Serilog Seq sink for structured log aggregation"
```

---

### Task 1.3: Backend — Seq Query Client + Options

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqOptions.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqQueryClient.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ApplicationLogDto.cs`

- [ ] **Step 1: Create SeqOptions**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqOptions.cs
namespace Api.BoundedContexts.Administration.Infrastructure.External;

public sealed class SeqOptions
{
    public const string SectionName = "Seq";

    public string ServerUrl { get; set; } = "http://seq:5341";
    public string? ApiKey { get; set; }
    public int RetentionDays { get; set; } = 7;
}
```

- [ ] **Step 2: Create ApplicationLogDto**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ApplicationLogDto.cs
namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record ApplicationLogDto(
    string Id,
    DateTime Timestamp,
    string Level,
    string Message,
    string? Source,
    string? CorrelationId,
    string? Exception,
    Dictionary<string, string>? Properties);
```

- [ ] **Step 3: Create SeqQueryClient**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqQueryClient.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.DTOs;
using Microsoft.Extensions.Options;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

public interface ISeqQueryClient
{
    Task<(IReadOnlyList<ApplicationLogDto> Items, int? RemainingCount)> QueryEventsAsync(
        string? filter,
        string? level,
        DateTime? fromUtc,
        DateTime? toUtc,
        int count,
        string? afterId,
        CancellationToken ct);
}

public sealed class SeqQueryClient : ISeqQueryClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SeqQueryClient> _logger;

    public SeqQueryClient(HttpClient httpClient, ILogger<SeqQueryClient> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(IReadOnlyList<ApplicationLogDto> Items, int? RemainingCount)> QueryEventsAsync(
        string? filter,
        string? level,
        DateTime? fromUtc,
        DateTime? toUtc,
        int count,
        string? afterId,
        CancellationToken ct)
    {
        var queryParams = new List<string> { $"count={count}" };

        // Build Seq filter expression
        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(level))
            filterParts.Add($"@Level = '{level}'");
        if (!string.IsNullOrWhiteSpace(filter))
            filterParts.Add($"@Message like '%{EscapeSeqFilter(filter)}%' or @Exception like '%{EscapeSeqFilter(filter)}%'");

        if (filterParts.Count > 0)
            queryParams.Add($"filter={Uri.EscapeDataString(string.Join(" and ", filterParts))}");

        if (fromUtc.HasValue)
            queryParams.Add($"fromDateUtc={fromUtc.Value:O}");
        if (toUtc.HasValue)
            queryParams.Add($"toDateUtc={toUtc.Value:O}");
        if (!string.IsNullOrWhiteSpace(afterId))
            queryParams.Add($"afterId={afterId}");

        var url = $"/api/events?{string.Join("&", queryParams)}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            var items = new List<ApplicationLogDto>();
            foreach (var evt in doc.RootElement.EnumerateArray())
            {
                items.Add(ParseEvent(evt));
            }

            // Seq returns remaining count in header
            int? remaining = null;
            if (response.Headers.TryGetValues("X-Seq-RemainingCount", out var vals)
                && int.TryParse(vals.FirstOrDefault(), out var rem))
            {
                remaining = rem;
            }

            return (items, remaining);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query Seq events at {Url}", url);
            return (Array.Empty<ApplicationLogDto>(), null);
        }
    }

    private static ApplicationLogDto ParseEvent(JsonElement evt)
    {
        var properties = new Dictionary<string, string>();
        if (evt.TryGetProperty("Properties", out var props))
        {
            foreach (var prop in props.EnumerateObject())
            {
                properties[prop.Name] = prop.Value.ToString();
            }
        }

        return new ApplicationLogDto(
            Id: evt.GetProperty("Id").GetString() ?? "",
            Timestamp: evt.GetProperty("Timestamp").GetDateTime(),
            Level: evt.TryGetProperty("Level", out var lvl) ? lvl.GetString() ?? "Information" : "Information",
            Message: evt.TryGetProperty("RenderedMessage", out var msg) ? msg.GetString() ?? "" : "",
            Source: properties.GetValueOrDefault("SourceContext"),
            CorrelationId: properties.GetValueOrDefault("CorrelationId"),
            Exception: evt.TryGetProperty("Exception", out var ex) ? ex.GetString() : null,
            Properties: properties.Count > 0 ? properties : null);
    }

    private static string EscapeSeqFilter(string input)
    {
        return input.Replace("'", "''");
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqOptions.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/External/SeqQueryClient.cs apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ApplicationLogDto.cs
git commit -m "feat(admin): add Seq query client for structured log retrieval"
```

---

### Task 1.4: Backend — Query Handler + Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandler.cs`
- Create: `apps/api/src/Api/Routing/AdminLogEndpoints.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs`

- [ ] **Step 1: Create query + response records**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.Logs;

internal record GetApplicationLogsQuery(
    string? Search,
    string? Level,
    string? Source,
    string? CorrelationId,
    DateTime? From,
    DateTime? To,
    int Count = 50,
    string? AfterId = null
) : IQuery<GetApplicationLogsResponse>;

public sealed record GetApplicationLogsResponse(
    IReadOnlyList<ApplicationLogDto> Items,
    int? RemainingCount,
    string? LastId);
```

- [ ] **Step 2: Create query handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandler.cs
using Api.BoundedContexts.Administration.Infrastructure.External;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Logs;

internal sealed class GetApplicationLogsQueryHandler
    : IQueryHandler<GetApplicationLogsQuery, GetApplicationLogsResponse>
{
    private readonly ISeqQueryClient _seqClient;

    public GetApplicationLogsQueryHandler(ISeqQueryClient seqClient)
    {
        _seqClient = seqClient ?? throw new ArgumentNullException(nameof(seqClient));
    }

    public async Task<GetApplicationLogsResponse> Handle(
        GetApplicationLogsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Build filter combining search text, source context, and correlation ID
        var filterParts = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Source))
            filterParts.Add($"SourceContext like '%{query.Source}%'");
        if (!string.IsNullOrWhiteSpace(query.CorrelationId))
            filterParts.Add($"CorrelationId = '{query.CorrelationId}'");

        var searchFilter = !string.IsNullOrWhiteSpace(query.Search) ? query.Search : null;

        // Combine source/correlation filters with search
        string? combinedFilter = null;
        if (filterParts.Count > 0 && searchFilter != null)
            combinedFilter = $"({string.Join(" and ", filterParts)}) and ({searchFilter})";
        else if (filterParts.Count > 0)
            combinedFilter = string.Join(" and ", filterParts);
        else
            combinedFilter = searchFilter;

        var (items, remaining) = await _seqClient.QueryEventsAsync(
            filter: combinedFilter,
            level: query.Level,
            fromUtc: query.From,
            toUtc: query.To,
            count: Math.Min(query.Count, 200), // Cap at 200
            afterId: query.AfterId,
            ct: cancellationToken).ConfigureAwait(false);

        var lastId = items.Count > 0 ? items[^1].Id : null;

        return new GetApplicationLogsResponse(items, remaining, lastId);
    }
}
```

- [ ] **Step 3: Create endpoint**

```csharp
// apps/api/src/Api/Routing/AdminLogEndpoints.cs
using Api.BoundedContexts.Administration.Application.Queries.Logs;
using Api.Infrastructure.Auth;
using MediatR;

namespace Api.Routing;

internal static class AdminLogEndpoints
{
    public static RouteGroupBuilder MapAdminLogEndpoints(this RouteGroupBuilder group)
    {
        var logGroup = group.MapGroup("/admin/logs")
            .WithTags("Admin", "Logs");

        logGroup.MapGet("/", async (
            string? search,
            string? level,
            string? source,
            string? correlationId,
            DateTime? from,
            DateTime? to,
            int? count,
            string? afterId,
            IMediator mediator,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var (session, error) = httpContext.RequireAdminSession();
            if (error is not null) return error;

            var query = new GetApplicationLogsQuery(
                Search: search,
                Level: level,
                Source: source,
                CorrelationId: correlationId,
                From: from,
                To: to,
                Count: count ?? 50,
                AfterId: afterId);

            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithSummary("Query structured application logs from Seq")
        .WithDescription("Paginated, filterable application log viewer. Supports cursor-based pagination via afterId.");

        return group;
    }
}
```

- [ ] **Step 4: Register Seq client in DI and wire endpoint**

In `AdministrationServiceExtensions.cs`, add after the Docker proxy registration:

```csharp
// Seq structured log query client
services.Configure<SeqOptions>(configuration.GetSection(SeqOptions.SectionName));
services.AddHttpClient<ISeqQueryClient, SeqQueryClient>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<SeqOptions>>().Value;
    client.BaseAddress = new Uri(options.ServerUrl);
    if (!string.IsNullOrEmpty(options.ApiKey))
        client.DefaultRequestHeaders.Add("X-Seq-ApiKey", options.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(15);
});
```

Add required usings:
```csharp
using Api.BoundedContexts.Administration.Infrastructure.External;
using Microsoft.Extensions.Options;
```

Wire the endpoint in the appropriate routing file (where other admin endpoints are mapped):

```csharp
group.MapAdminLogEndpoints();
```

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/Queries/Logs/ apps/api/src/Api/Routing/AdminLogEndpoints.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs
git commit -m "feat(admin): add GET /admin/logs endpoint querying Seq structured logs"
```

---

### Task 1.5: Backend — Unit Tests for Log Query

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandlerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/Logs/GetApplicationLogsQueryHandlerTests.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.Logs;
using Api.BoundedContexts.Administration.Infrastructure.External;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Logs;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class GetApplicationLogsQueryHandlerTests
{
    private readonly ISeqQueryClient _seqClient = Substitute.For<ISeqQueryClient>();
    private readonly GetApplicationLogsQueryHandler _sut;

    public GetApplicationLogsQueryHandlerTests()
    {
        _sut = new GetApplicationLogsQueryHandler(_seqClient);
    }

    [Fact]
    public async Task Handle_ReturnsLogsFromSeq()
    {
        // Arrange
        var logs = new List<ApplicationLogDto>
        {
            new("evt-1", DateTime.UtcNow, "Information", "Test message", "Api.Test", null, null, null)
        };
        _seqClient.QueryEventsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((logs.AsReadOnly() as IReadOnlyList<ApplicationLogDto>, (int?)10));

        var query = new GetApplicationLogsQuery(Search: null, Level: null, Source: null,
            CorrelationId: null, From: null, To: null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("evt-1", result.Items[0].Id);
        Assert.Equal("evt-1", result.LastId);
        Assert.Equal(10, result.RemainingCount);
    }

    [Fact]
    public async Task Handle_CapsCountAt200()
    {
        // Arrange
        _seqClient.QueryEventsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Is<int>(c => c <= 200), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ApplicationLogDto>() as IReadOnlyList<ApplicationLogDto>, (int?)null));

        var query = new GetApplicationLogsQuery(Search: null, Level: null, Source: null,
            CorrelationId: null, From: null, To: null, Count: 999);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        await _seqClient.Received(1).QueryEventsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), 200, Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSourceFilter_BuildsSeqFilter()
    {
        // Arrange
        _seqClient.QueryEventsAsync(
            Arg.Is<string?>(f => f != null && f.Contains("SourceContext")),
            Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ApplicationLogDto>() as IReadOnlyList<ApplicationLogDto>, (int?)null));

        var query = new GetApplicationLogsQuery(Search: null, Level: null, Source: "KnowledgeBase",
            CorrelationId: null, From: null, To: null);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        await _seqClient.Received(1).QueryEventsAsync(
            Arg.Is<string?>(f => f != null && f.Contains("KnowledgeBase")),
            Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsNullLastId()
    {
        // Arrange
        _seqClient.QueryEventsAsync(
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<DateTime?>(),
            Arg.Any<DateTime?>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ApplicationLogDto>() as IReadOnlyList<ApplicationLogDto>, (int?)null));

        var query = new GetApplicationLogsQuery(Search: null, Level: null, Source: null,
            CorrelationId: null, From: null, To: null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.Null(result.LastId);
        Assert.Null(result.RemainingCount);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~GetApplicationLogsQueryHandlerTests" -v n
```

Expected: All 4 tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/Logs/
git commit -m "test(admin): add unit tests for GetApplicationLogsQueryHandler"
```

---

### Task 1.6: Frontend — Zod Schemas + API Client

**Files:**
- Create: `apps/web/src/lib/api/schemas/admin/admin-logs.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts`

- [ ] **Step 1: Create Zod schemas**

```typescript
// apps/web/src/lib/api/schemas/admin/admin-logs.schemas.ts
import { z } from 'zod';

export const ApplicationLogSchema = z.object({
  id: z.string(),
  timestamp: z.string(),
  level: z.string(),
  message: z.string(),
  source: z.string().nullable(),
  correlationId: z.string().nullable(),
  exception: z.string().nullable(),
  properties: z.record(z.string(), z.string()).nullable(),
});

export const ApplicationLogsResponseSchema = z.object({
  items: z.array(ApplicationLogSchema),
  remainingCount: z.number().nullable(),
  lastId: z.string().nullable(),
});

export type ApplicationLog = z.infer<typeof ApplicationLogSchema>;
export type ApplicationLogsResponse = z.infer<typeof ApplicationLogsResponseSchema>;

export interface ApplicationLogsFilters {
  search?: string;
  level?: string;
  source?: string;
  correlationId?: string;
  from?: string;
  to?: string;
  count?: number;
  afterId?: string;
}
```

- [ ] **Step 2: Add API client method**

In `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts`, add:

```typescript
import { ApplicationLogsResponseSchema, type ApplicationLogsFilters, type ApplicationLogsResponse } from '../../schemas/admin/admin-logs.schemas';

// Inside the class/object:
async getApplicationLogs(filters: ApplicationLogsFilters = {}): Promise<ApplicationLogsResponse> {
  const params = new URLSearchParams();
  if (filters.search) params.set('search', filters.search);
  if (filters.level) params.set('level', filters.level);
  if (filters.source) params.set('source', filters.source);
  if (filters.correlationId) params.set('correlationId', filters.correlationId);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.count) params.set('count', String(filters.count));
  if (filters.afterId) params.set('afterId', filters.afterId);

  const query = params.toString();
  const url = `/api/v1/admin/logs${query ? `?${query}` : ''}`;
  const res = await http.get(url);
  return ApplicationLogsResponseSchema.parse(res);
},
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/admin/admin-logs.schemas.ts apps/web/src/lib/api/clients/admin/adminMonitorClient.ts
git commit -m "feat(web): add application logs API schemas and client method"
```

---

### Task 1.7: Frontend — AppLogViewer Component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/logs/AppLogViewer.tsx`

- [ ] **Step 1: Create the structured log viewer component**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/logs/AppLogViewer.tsx
'use client';

import { useCallback, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { ApplicationLog, ApplicationLogsFilters } from '@/lib/api/schemas/admin/admin-logs.schemas';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { RefreshCw, Search, ChevronDown, ChevronRight } from 'lucide-react';

const LOG_LEVELS = ['Verbose', 'Debug', 'Information', 'Warning', 'Error', 'Fatal'] as const;

const LEVEL_COLORS: Record<string, string> = {
  Verbose: 'bg-gray-100 text-gray-700',
  Debug: 'bg-blue-100 text-blue-700',
  Information: 'bg-green-100 text-green-700',
  Warning: 'bg-amber-100 text-amber-800',
  Error: 'bg-red-100 text-red-700',
  Fatal: 'bg-red-600 text-white',
};

export function AppLogViewer() {
  const [filters, setFilters] = useState<ApplicationLogsFilters>({
    count: 50,
  });
  const [searchInput, setSearchInput] = useState('');
  const [expandedRows, setExpandedRows] = useState<Set<string>>(new Set());

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['admin', 'application-logs', filters],
    queryFn: () => api.admin.getApplicationLogs(filters),
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const handleSearch = useCallback(() => {
    setFilters(prev => ({ ...prev, search: searchInput || undefined, afterId: undefined }));
  }, [searchInput]);

  const handleLevelChange = useCallback((level: string) => {
    setFilters(prev => ({
      ...prev,
      level: level === 'all' ? undefined : level,
      afterId: undefined,
    }));
  }, []);

  const handleLoadMore = useCallback(() => {
    if (data?.lastId) {
      setFilters(prev => ({ ...prev, afterId: data.lastId ?? undefined }));
    }
  }, [data?.lastId]);

  const toggleRow = useCallback((id: string) => {
    setExpandedRows(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

  const formatTimestamp = (ts: string) => {
    const d = new Date(ts);
    return d.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit', second: '2-digit' })
      + '.' + String(d.getMilliseconds()).padStart(3, '0');
  };

  return (
    <div className="space-y-4">
      {/* Filters bar */}
      <div className="flex flex-wrap gap-3 items-center">
        <div className="flex gap-2 flex-1 min-w-[200px]">
          <Input
            placeholder="Search logs..."
            value={searchInput}
            onChange={e => setSearchInput(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && handleSearch()}
            className="max-w-sm"
          />
          <Button variant="outline" size="icon" onClick={handleSearch}>
            <Search className="h-4 w-4" />
          </Button>
        </div>

        <Select
          value={filters.level ?? 'all'}
          onValueChange={handleLevelChange}
        >
          <SelectTrigger className="w-[150px]">
            <SelectValue placeholder="Log Level" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Levels</SelectItem>
            {LOG_LEVELS.map(level => (
              <SelectItem key={level} value={level}>{level}</SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Input
          placeholder="Source (e.g. KnowledgeBase)"
          value={filters.source ?? ''}
          onChange={e => setFilters(prev => ({
            ...prev,
            source: e.target.value || undefined,
            afterId: undefined,
          }))}
          className="w-[200px]"
        />

        <Input
          placeholder="Correlation ID"
          value={filters.correlationId ?? ''}
          onChange={e => setFilters(prev => ({
            ...prev,
            correlationId: e.target.value || undefined,
            afterId: undefined,
          }))}
          className="w-[200px]"
        />

        <Button
          variant="outline"
          size="icon"
          onClick={() => refetch()}
          disabled={isFetching}
        >
          <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Log table */}
      <div className="border rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-3 py-2 text-left w-8"></th>
                <th className="px-3 py-2 text-left w-[100px]">Time</th>
                <th className="px-3 py-2 text-left w-[100px]">Level</th>
                <th className="px-3 py-2 text-left w-[180px]">Source</th>
                <th className="px-3 py-2 text-left">Message</th>
              </tr>
            </thead>
            <tbody className="font-mono text-xs">
              {isLoading && (
                <tr><td colSpan={5} className="px-3 py-8 text-center text-muted-foreground">Loading logs...</td></tr>
              )}
              {!isLoading && (!data?.items || data.items.length === 0) && (
                <tr><td colSpan={5} className="px-3 py-8 text-center text-muted-foreground">
                  {filters.search || filters.level || filters.source
                    ? 'No logs matching filters. Try broadening your search.'
                    : 'No logs available. Ensure Seq is running and receiving events.'}
                </td></tr>
              )}
              {data?.items.map((log: ApplicationLog) => (
                <LogRow
                  key={log.id}
                  log={log}
                  expanded={expandedRows.has(log.id)}
                  onToggle={() => toggleRow(log.id)}
                  formatTimestamp={formatTimestamp}
                />
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Load more */}
      {data?.remainingCount != null && data.remainingCount > 0 && (
        <div className="flex justify-center">
          <Button variant="outline" onClick={handleLoadMore} disabled={isFetching}>
            Load more ({data.remainingCount} remaining)
          </Button>
        </div>
      )}
    </div>
  );
}

function LogRow({
  log,
  expanded,
  onToggle,
  formatTimestamp,
}: {
  log: ApplicationLog;
  expanded: boolean;
  onToggle: () => void;
  formatTimestamp: (ts: string) => string;
}) {
  const hasDetails = log.exception || (log.properties && Object.keys(log.properties).length > 0);
  const levelClass = LEVEL_COLORS[log.level] ?? LEVEL_COLORS.Information;
  const isError = log.level === 'Error' || log.level === 'Fatal';

  return (
    <>
      <tr
        className={`border-t hover:bg-muted/30 cursor-pointer ${isError ? 'bg-red-50/50 dark:bg-red-950/20' : ''}`}
        onClick={hasDetails ? onToggle : undefined}
      >
        <td className="px-3 py-1.5">
          {hasDetails && (expanded
            ? <ChevronDown className="h-3 w-3 text-muted-foreground" />
            : <ChevronRight className="h-3 w-3 text-muted-foreground" />
          )}
        </td>
        <td className="px-3 py-1.5 whitespace-nowrap text-muted-foreground">
          {formatTimestamp(log.timestamp)}
        </td>
        <td className="px-3 py-1.5">
          <Badge variant="secondary" className={`text-xs ${levelClass}`}>
            {log.level.substring(0, 3).toUpperCase()}
          </Badge>
        </td>
        <td className="px-3 py-1.5 text-muted-foreground truncate max-w-[180px]" title={log.source ?? ''}>
          {log.source?.split('.').pop() ?? '—'}
        </td>
        <td className="px-3 py-1.5 truncate max-w-[500px]" title={log.message}>
          {log.message}
        </td>
      </tr>
      {expanded && hasDetails && (
        <tr className="border-t bg-muted/20">
          <td colSpan={5} className="px-6 py-3 space-y-2">
            {log.correlationId && (
              <div><span className="text-muted-foreground">Correlation ID:</span> <code className="text-xs">{log.correlationId}</code></div>
            )}
            {log.source && (
              <div><span className="text-muted-foreground">Full Source:</span> <code className="text-xs">{log.source}</code></div>
            )}
            {log.exception && (
              <div>
                <span className="text-muted-foreground">Exception:</span>
                <pre className="mt-1 p-2 bg-red-50 dark:bg-red-950/30 rounded text-xs overflow-x-auto whitespace-pre-wrap max-h-[200px] overflow-y-auto">
                  {log.exception}
                </pre>
              </div>
            )}
            {log.properties && Object.keys(log.properties).length > 0 && (
              <div>
                <span className="text-muted-foreground">Properties:</span>
                <div className="mt-1 grid grid-cols-2 gap-1 text-xs">
                  {Object.entries(log.properties)
                    .filter(([key]) => key !== 'SourceContext' && key !== 'CorrelationId')
                    .map(([key, value]) => (
                      <div key={key}>
                        <span className="text-muted-foreground">{key}:</span>{' '}
                        <code>{String(value).substring(0, 100)}</code>
                      </div>
                    ))}
                </div>
              </div>
            )}
          </td>
        </tr>
      )}
    </>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/monitor/logs/AppLogViewer.tsx
git commit -m "feat(web): add AppLogViewer component for structured Seq log viewing"
```

---

### Task 1.8: Frontend — Update Logs Page with Tabs

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`

- [ ] **Step 1: Add tab navigation between Container Logs and App Logs**

Replace the content of `apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { LogViewer } from './LogViewer';
import { AppLogViewer } from './AppLogViewer';

type LogTab = 'app' | 'container';

export default function LogViewerPage() {
  const [activeTab, setActiveTab] = useState<LogTab>('app');

  return (
    <div data-testid="logs-page" className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Log Viewer</h1>
        <p className="text-muted-foreground">Application and container log monitoring</p>
      </div>

      {/* Tab navigation */}
      <div className="flex gap-1 border-b">
        <button
          onClick={() => setActiveTab('app')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'app'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Application Logs
        </button>
        <button
          onClick={() => setActiveTab('container')}
          className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
            activeTab === 'container'
              ? 'border-primary text-primary'
              : 'border-transparent text-muted-foreground hover:text-foreground'
          }`}
        >
          Container Logs
        </button>
      </div>

      {/* Tab content */}
      {activeTab === 'app' && <AppLogViewer />}
      {activeTab === 'container' && <LogViewer />}
    </div>
  );
}
```

- [ ] **Step 2: Verify the page renders**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds without errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/monitor/logs/page.tsx
git commit -m "feat(web): add tabs to log viewer page (app logs + container logs)"
```

---

## Phase 2: Service Call History

### Task 2.1: Backend — ServiceCallLogEntry Entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/ServiceCallLogEntry.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Configurations/ServiceCallLogConfiguration.cs`

- [ ] **Step 1: Create domain entity**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/ServiceCallLogEntry.cs
namespace Api.BoundedContexts.Administration.Domain.Entities;

public sealed class ServiceCallLogEntry
{
    public Guid Id { get; private set; }
    public string ServiceName { get; private set; } = null!;
    public string HttpMethod { get; private set; } = null!;
    public string RequestUrl { get; private set; } = null!;
    public int? StatusCode { get; private set; }
    public long LatencyMs { get; private set; }
    public bool IsSuccess { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string? RequestSummary { get; private set; }
    public string? ResponseSummary { get; private set; }

    private ServiceCallLogEntry() { } // EF

    public static ServiceCallLogEntry Create(
        string serviceName,
        string httpMethod,
        string requestUrl,
        int? statusCode,
        long latencyMs,
        bool isSuccess,
        string? errorMessage,
        string? correlationId,
        string? requestSummary = null,
        string? responseSummary = null)
    {
        return new ServiceCallLogEntry
        {
            Id = Guid.NewGuid(),
            ServiceName = serviceName,
            HttpMethod = httpMethod,
            RequestUrl = TruncateAndSanitize(requestUrl, 2000),
            StatusCode = statusCode,
            LatencyMs = latencyMs,
            IsSuccess = isSuccess,
            ErrorMessage = TruncateAndSanitize(errorMessage, 2000),
            CorrelationId = correlationId,
            TimestampUtc = DateTime.UtcNow,
            RequestSummary = TruncateAndSanitize(requestSummary, 1000),
            ResponseSummary = TruncateAndSanitize(responseSummary, 1000),
        };
    }

    private static string? TruncateAndSanitize(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        // Remove potential secrets from URLs
        var sanitized = SanitizeSecrets(value);
        return sanitized.Length > maxLength ? sanitized[..maxLength] + "..." : sanitized;
    }

    private static string SanitizeSecrets(string value)
    {
        // Mask API keys, tokens, passwords in URLs and bodies
        return System.Text.RegularExpressions.Regex.Replace(
            value,
            @"(api[_-]?key|token|password|secret|authorization)[=:]\s*[""']?[\w\-\.]+",
            "$1=***REDACTED***",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
```

- [ ] **Step 2: Create EF configuration**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Configurations/ServiceCallLogConfiguration.cs
using Api.BoundedContexts.Administration.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Configurations;

internal sealed class ServiceCallLogConfiguration : IEntityTypeConfiguration<ServiceCallLogEntry>
{
    public void Configure(EntityTypeBuilder<ServiceCallLogEntry> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("service_call_logs", schema: "administration");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .IsRequired()
            .ValueGeneratedNever();

        builder.Property(x => x.ServiceName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.HttpMethod)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.RequestUrl)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(x => x.StatusCode)
            .IsRequired(false);

        builder.Property(x => x.LatencyMs)
            .IsRequired();

        builder.Property(x => x.IsSuccess)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .IsRequired(false)
            .HasMaxLength(2000);

        builder.Property(x => x.CorrelationId)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(x => x.TimestampUtc)
            .IsRequired();

        builder.Property(x => x.RequestSummary)
            .IsRequired(false)
            .HasMaxLength(1000);

        builder.Property(x => x.ResponseSummary)
            .IsRequired(false)
            .HasMaxLength(1000);

        // Indexes for common queries
        builder.HasIndex(x => x.TimestampUtc)
            .HasDatabaseName("ix_service_call_logs_timestamp")
            .IsDescending();

        builder.HasIndex(x => x.ServiceName)
            .HasDatabaseName("ix_service_call_logs_service_name");

        builder.HasIndex(x => new { x.ServiceName, x.TimestampUtc })
            .HasDatabaseName("ix_service_call_logs_service_timestamp");

        builder.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("ix_service_call_logs_correlation_id");

        builder.HasIndex(x => x.IsSuccess)
            .HasDatabaseName("ix_service_call_logs_is_success");
    }
}
```

- [ ] **Step 3: Add DbSet to MeepleAiDbContext**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, add to the DbSet section:

```csharp
public DbSet<ServiceCallLogEntry> ServiceCallLogs => Set<ServiceCallLogEntry>();
```

Add the using:
```csharp
using Api.BoundedContexts.Administration.Domain.Entities;
```

- [ ] **Step 4: Create migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddServiceCallLogs
```

Expected: Migration files created in `Infrastructure/Migrations/`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Entities/ServiceCallLogEntry.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Configurations/ServiceCallLogConfiguration.cs apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(admin): add ServiceCallLogEntry entity with EF configuration and migration"
```

---

### Task 2.2: Backend — DelegatingHandler for HTTP Call Logging

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Http/ServiceCallLoggingHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IServiceCallLogRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Repositories/ServiceCallLogRepository.cs`

- [ ] **Step 1: Create repository interface**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IServiceCallLogRepository.cs
using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

public interface IServiceCallLogRepository
{
    Task AddAsync(ServiceCallLogEntry entry, CancellationToken ct);
    Task<(IReadOnlyList<ServiceCallLogEntry> Items, int TotalCount)> GetPagedAsync(
        string? serviceName, bool? isSuccess, string? correlationId,
        DateTime? from, DateTime? to, long? minLatencyMs,
        int page, int pageSize, CancellationToken ct);
    Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct);
}
```

- [ ] **Step 2: Create repository implementation**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Repositories/ServiceCallLogRepository.cs
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence.Repositories;

internal sealed class ServiceCallLogRepository : IServiceCallLogRepository
{
    private readonly MeepleAiDbContext _db;

    public ServiceCallLogRepository(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(ServiceCallLogEntry entry, CancellationToken ct)
    {
        _db.ServiceCallLogs.Add(entry);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<(IReadOnlyList<ServiceCallLogEntry> Items, int TotalCount)> GetPagedAsync(
        string? serviceName, bool? isSuccess, string? correlationId,
        DateTime? from, DateTime? to, long? minLatencyMs,
        int page, int pageSize, CancellationToken ct)
    {
        var query = _db.ServiceCallLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(serviceName))
            query = query.Where(x => x.ServiceName == serviceName);
        if (isSuccess.HasValue)
            query = query.Where(x => x.IsSuccess == isSuccess.Value);
        if (!string.IsNullOrWhiteSpace(correlationId))
            query = query.Where(x => x.CorrelationId == correlationId);
        if (from.HasValue)
            query = query.Where(x => x.TimestampUtc >= from.Value);
        if (to.HasValue)
            query = query.Where(x => x.TimestampUtc <= to.Value);
        if (minLatencyMs.HasValue)
            query = query.Where(x => x.LatencyMs >= minLatencyMs.Value);

        var totalCount = await query.CountAsync(ct).ConfigureAwait(false);

        var items = await query
            .OrderByDescending(x => x.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (items, totalCount);
    }

    public async Task DeleteOlderThanAsync(DateTime cutoff, CancellationToken ct)
    {
        await _db.ServiceCallLogs
            .Where(x => x.TimestampUtc < cutoff)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Create the DelegatingHandler**

```csharp
// apps/api/src/Api/Infrastructure/Http/ServiceCallLoggingHandler.cs
using System.Diagnostics;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;

namespace Api.Infrastructure.Http;

/// <summary>
/// DelegatingHandler that logs every HTTP client call to the service_call_logs table.
/// Registered on named HttpClients to capture external service interactions.
/// Uses fire-and-forget pattern to avoid adding latency to the actual HTTP call.
/// </summary>
public sealed class ServiceCallLoggingHandler : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _serviceName;
    private readonly ILogger<ServiceCallLoggingHandler> _logger;

    public ServiceCallLoggingHandler(
        IServiceProvider serviceProvider,
        string serviceName,
        ILogger<ServiceCallLoggingHandler> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _serviceName = serviceName;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage? response = null;
        string? errorMessage = null;

        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response;
        }
        catch (Exception ex)
        {
            errorMessage = $"{ex.GetType().Name}: {ex.Message}";
            throw;
        }
        finally
        {
            sw.Stop();

            var correlationId = Activity.Current?.TraceId.ToString()
                ?? request.Headers.TryGetValues("X-Correlation-Id", out var vals)
                    ? vals.FirstOrDefault()
                    : null;

            var entry = ServiceCallLogEntry.Create(
                serviceName: _serviceName,
                httpMethod: request.Method.Method,
                requestUrl: request.RequestUri?.ToString() ?? "unknown",
                statusCode: response != null ? (int)response.StatusCode : null,
                latencyMs: sw.ElapsedMilliseconds,
                isSuccess: response?.IsSuccessStatusCode ?? false,
                errorMessage: errorMessage ?? (response != null && !response.IsSuccessStatusCode
                    ? $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                    : null),
                correlationId: correlationId);

            // Fire-and-forget: don't slow down the actual HTTP call
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repo = scope.ServiceProvider.GetRequiredService<IServiceCallLogRepository>();
                    await repo.AddAsync(entry, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to persist service call log for {Service}", _serviceName);
                }
            });
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Http/ServiceCallLoggingHandler.cs apps/api/src/Api/BoundedContexts/Administration/Domain/Repositories/IServiceCallLogRepository.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Persistence/Repositories/ServiceCallLogRepository.cs
git commit -m "feat(admin): add ServiceCallLoggingHandler DelegatingHandler and repository"
```

---

### Task 2.3: Backend — Register Handler on Named HttpClients

**Files:**
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs`

- [ ] **Step 1: Create a factory helper for the handler**

In `InfrastructureServiceExtensions.cs`, add a helper extension method at the bottom of the class:

```csharp
/// <summary>
/// Adds the ServiceCallLoggingHandler as the outermost DelegatingHandler on a named HttpClient.
/// </summary>
private static IHttpClientBuilder AddServiceCallLogging(this IHttpClientBuilder builder, string serviceName)
{
    return builder.AddHttpMessageHandler(sp =>
        new ServiceCallLoggingHandler(
            sp,
            serviceName,
            sp.GetRequiredService<ILogger<ServiceCallLoggingHandler>>()));
}
```

Add using: `using Api.Infrastructure.Http;`

- [ ] **Step 2: Chain `.AddServiceCallLogging()` on each named client**

For each `AddHttpClient` call in `InfrastructureServiceExtensions.cs`, chain `.AddServiceCallLogging("ServiceName")` as the **last** handler (outermost, wraps Polly):

```csharp
// OpenRouter
services.AddHttpClient("OpenRouter", client => { ... })
    .AddTransientHttpErrorPolicy(...)
    .AddTransientHttpErrorPolicy(...)
    .AddServiceCallLogging("OpenRouter")
    .ConfigurePrimaryHttpMessageHandler(...);

// EmbeddingService
services.AddHttpClient("EmbeddingService", client => { ... })
    .AddServiceCallLogging("EmbeddingService")
    .ConfigurePrimaryHttpMessageHandler(...);

// Ollama
services.AddHttpClient("Ollama", client => { ... })
    .AddServiceCallLogging("Ollama")
    .ConfigurePrimaryHttpMessageHandler(...);
```

Apply to: `OpenRouter`, `Ollama`, `EmbeddingService`, `HuggingFace`, `BggApi`, `Infisical`, `OrchestrationService`.

Do the same in `DocumentProcessingServiceExtensions.cs` for: `UnstructuredService`, `SmolDoclingService`.

And in `AdministrationServiceExtensions.cs` for: `IOpenRouterService`, `IPrometheusQueryService`.

- [ ] **Step 3: Register repository in DI**

In `AdministrationServiceExtensions.cs`, add:

```csharp
services.AddScoped<IServiceCallLogRepository, ServiceCallLogRepository>();
```

Add usings:
```csharp
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence.Repositories;
```

- [ ] **Step 4: Verify build**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/
git commit -m "feat(admin): register ServiceCallLoggingHandler on all named HttpClients"
```

---

### Task 2.4: Backend — Service Call Query Handlers + DTOs

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ServiceCallDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQueryHandler.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ServiceCallDto.cs
namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record ServiceCallDto(
    Guid Id,
    string ServiceName,
    string HttpMethod,
    string RequestUrl,
    int? StatusCode,
    long LatencyMs,
    bool IsSuccess,
    string? ErrorMessage,
    string? CorrelationId,
    DateTime TimestampUtc,
    string? RequestSummary,
    string? ResponseSummary);

public sealed record ServiceCallSummaryDto(
    string ServiceName,
    int TotalCalls,
    int SuccessCount,
    int ErrorCount,
    double ErrorRate,
    double AvgLatencyMs,
    double P95LatencyMs,
    long MaxLatencyMs,
    DateTime? LastCallAt,
    DateTime? LastErrorAt);
```

- [ ] **Step 2: Create paginated query + handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal record GetServiceCallsQuery(
    string? ServiceName,
    bool? IsSuccess,
    string? CorrelationId,
    DateTime? From,
    DateTime? To,
    long? MinLatencyMs,
    int Page = 1,
    int PageSize = 50
) : IQuery<GetServiceCallsResponse>;

public sealed record GetServiceCallsResponse(
    IReadOnlyList<ServiceCallDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
```

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandler.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal sealed class GetServiceCallsQueryHandler
    : IQueryHandler<GetServiceCallsQuery, GetServiceCallsResponse>
{
    private readonly IServiceCallLogRepository _repo;

    public GetServiceCallsQueryHandler(IServiceCallLogRepository repo)
    {
        _repo = repo ?? throw new ArgumentNullException(nameof(repo));
    }

    public async Task<GetServiceCallsResponse> Handle(
        GetServiceCallsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var (items, totalCount) = await _repo.GetPagedAsync(
            query.ServiceName, query.IsSuccess, query.CorrelationId,
            query.From, query.To, query.MinLatencyMs,
            query.Page, Math.Min(query.PageSize, 100),
            cancellationToken).ConfigureAwait(false);

        var dtos = items.Select(e => new ServiceCallDto(
            e.Id, e.ServiceName, e.HttpMethod, e.RequestUrl,
            e.StatusCode, e.LatencyMs, e.IsSuccess, e.ErrorMessage,
            e.CorrelationId, e.TimestampUtc, e.RequestSummary, e.ResponseSummary
        )).ToList();

        return new GetServiceCallsResponse(dtos, totalCount, query.Page, query.PageSize);
    }
}
```

- [ ] **Step 3: Create summary query + handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal record GetServiceCallSummaryQuery(
    string? Period = "24h"
) : IQuery<IReadOnlyList<ServiceCallSummaryDto>>;
```

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallSummaryQueryHandler.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;

internal sealed class GetServiceCallSummaryQueryHandler
    : IQueryHandler<GetServiceCallSummaryQuery, IReadOnlyList<ServiceCallSummaryDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetServiceCallSummaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<ServiceCallSummaryDto>> Handle(
        GetServiceCallSummaryQuery query, CancellationToken cancellationToken)
    {
        var since = query.Period switch
        {
            "1h" => DateTime.UtcNow.AddHours(-1),
            "6h" => DateTime.UtcNow.AddHours(-6),
            "24h" => DateTime.UtcNow.AddHours(-24),
            "7d" => DateTime.UtcNow.AddDays(-7),
            _ => DateTime.UtcNow.AddHours(-24),
        };

        var groups = await _db.ServiceCallLogs
            .AsNoTracking()
            .Where(x => x.TimestampUtc >= since)
            .GroupBy(x => x.ServiceName)
            .Select(g => new
            {
                ServiceName = g.Key,
                TotalCalls = g.Count(),
                SuccessCount = g.Count(x => x.IsSuccess),
                ErrorCount = g.Count(x => !x.IsSuccess),
                AvgLatencyMs = g.Average(x => (double)x.LatencyMs),
                MaxLatencyMs = g.Max(x => x.LatencyMs),
                LastCallAt = g.Max(x => (DateTime?)x.TimestampUtc),
                LastErrorAt = g.Where(x => !x.IsSuccess).Max(x => (DateTime?)x.TimestampUtc),
                Latencies = g.Select(x => x.LatencyMs).ToList(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return groups.Select(g =>
        {
            // Calculate P95 in-memory
            var sorted = g.Latencies.OrderBy(l => l).ToList();
            var p95Index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
            var p95 = sorted.Count > 0 ? sorted[Math.Max(0, p95Index)] : 0;

            return new ServiceCallSummaryDto(
                g.ServiceName,
                g.TotalCalls,
                g.SuccessCount,
                g.ErrorCount,
                g.TotalCalls > 0 ? Math.Round((double)g.ErrorCount / g.TotalCalls * 100, 1) : 0,
                Math.Round(g.AvgLatencyMs, 1),
                p95,
                g.MaxLatencyMs,
                g.LastCallAt,
                g.LastErrorAt);
        }).OrderByDescending(x => x.TotalCalls).ToList();
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/ServiceCallDto.cs apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ServiceCalls/
git commit -m "feat(admin): add service call query handlers with pagination and summary"
```

---

### Task 2.5: Backend — Endpoints + Retention Job

**Files:**
- Create: `apps/api/src/Api/Routing/AdminServiceCallEndpoints.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/ServiceCallLogRetentionJob.cs`

- [ ] **Step 1: Create endpoints**

```csharp
// apps/api/src/Api/Routing/AdminServiceCallEndpoints.cs
using Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;
using Api.Infrastructure.Auth;
using MediatR;

namespace Api.Routing;

internal static class AdminServiceCallEndpoints
{
    public static RouteGroupBuilder MapAdminServiceCallEndpoints(this RouteGroupBuilder group)
    {
        var callGroup = group.MapGroup("/admin/service-calls")
            .WithTags("Admin", "ServiceCalls");

        callGroup.MapGet("/", async (
            string? service,
            bool? success,
            string? correlationId,
            DateTime? from,
            DateTime? to,
            long? minLatencyMs,
            int? page,
            int? pageSize,
            IMediator mediator,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var (session, error) = httpContext.RequireAdminSession();
            if (error is not null) return error;

            var query = new GetServiceCallsQuery(
                ServiceName: service,
                IsSuccess: success,
                CorrelationId: correlationId,
                From: from,
                To: to,
                MinLatencyMs: minLatencyMs,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await mediator.Send(query, ct);
            return Results.Ok(result);
        })
        .WithSummary("Query service call history");

        callGroup.MapGet("/summary", async (
            string? period,
            IMediator mediator,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var (session, error) = httpContext.RequireAdminSession();
            if (error is not null) return error;

            var result = await mediator.Send(new GetServiceCallSummaryQuery(period ?? "24h"), ct);
            return Results.Ok(result);
        })
        .WithSummary("Get aggregated service call statistics");

        return group;
    }
}
```

- [ ] **Step 2: Create retention job**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/ServiceCallLogRetentionJob.cs
using Api.BoundedContexts.Administration.Domain.Repositories;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

[DisallowConcurrentExecution]
public sealed class ServiceCallLogRetentionJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ServiceCallLogRetentionJob> _logger;

    public ServiceCallLogRetentionJob(
        IServiceProvider serviceProvider,
        ILogger<ServiceCallLogRetentionJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IServiceCallLogRepository>();

        var cutoff = DateTime.UtcNow.AddDays(-7);
        _logger.LogInformation("Cleaning service call logs older than {Cutoff}", cutoff);

        try
        {
            await repo.DeleteOlderThanAsync(cutoff, context.CancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Service call log retention completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Service call log retention failed");
        }
    }
}
```

- [ ] **Step 3: Register retention job in Quartz config**

In `AdministrationServiceExtensions.cs`, inside the `AddQuartz` block, add:

```csharp
// Service call log retention (daily at 4 AM UTC)
q.AddJob<ServiceCallLogRetentionJob>(opts => opts
    .WithIdentity("service-call-log-retention-job", "maintenance")
    .StoreDurably(true));

q.AddTrigger(opts => opts
    .ForJob("service-call-log-retention-job", "maintenance")
    .WithIdentity("service-call-log-retention-trigger", "maintenance")
    .WithCronSchedule("0 0 4 * * ?")
    .WithDescription("Runs daily to clean up service call logs older than 7 days"));
```

- [ ] **Step 4: Wire endpoints in routing**

In the file that maps admin route groups, add:

```csharp
group.MapAdminServiceCallEndpoints();
```

- [ ] **Step 5: Verify build**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Routing/AdminServiceCallEndpoints.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Scheduling/ServiceCallLogRetentionJob.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs
git commit -m "feat(admin): add service call endpoints and 7-day retention job"
```

---

### Task 2.6: Backend — Unit Tests for Service Call Handler

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandlerTests.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/Http/ServiceCallLoggingHandlerTests.cs`

- [ ] **Step 1: Write query handler tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/ServiceCalls/GetServiceCallsQueryHandlerTests.cs
using Api.BoundedContexts.Administration.Application.Queries.ServiceCalls;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.ServiceCalls;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class GetServiceCallsQueryHandlerTests
{
    private readonly IServiceCallLogRepository _repo = Substitute.For<IServiceCallLogRepository>();
    private readonly GetServiceCallsQueryHandler _sut;

    public GetServiceCallsQueryHandlerTests()
    {
        _sut = new GetServiceCallsQueryHandler(_repo);
    }

    [Fact]
    public async Task Handle_ReturnsPaginatedResults()
    {
        // Arrange
        var entries = new List<ServiceCallLogEntry>
        {
            ServiceCallLogEntry.Create("OpenRouter", "POST", "https://openrouter.ai/api/v1/chat",
                200, 1500, true, null, "corr-1"),
        };
        _repo.GetPagedAsync(
            Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<long?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((entries.AsReadOnly() as IReadOnlyList<ServiceCallLogEntry>, 1));

        var query = new GetServiceCallsQuery(ServiceName: "OpenRouter", IsSuccess: null,
            CorrelationId: null, From: null, To: null, MinLatencyMs: null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("OpenRouter", result.Items[0].ServiceName);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_CapsPageSizeAt100()
    {
        // Arrange
        _repo.GetPagedAsync(
            Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<long?>(),
            Arg.Any<int>(), Arg.Is<int>(ps => ps <= 100), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<ServiceCallLogEntry>() as IReadOnlyList<ServiceCallLogEntry>, 0));

        var query = new GetServiceCallsQuery(ServiceName: null, IsSuccess: null,
            CorrelationId: null, From: null, To: null, MinLatencyMs: null,
            Page: 1, PageSize: 500);

        // Act
        await _sut.Handle(query, CancellationToken.None);

        // Assert
        await _repo.Received(1).GetPagedAsync(
            Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<string?>(),
            Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<long?>(),
            1, 100, Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Write DelegatingHandler tests**

```csharp
// apps/api/tests/Api.Tests/Infrastructure/Http/ServiceCallLoggingHandlerTests.cs
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.Infrastructure.Http;

[Trait("Category", "Unit")]
public sealed class ServiceCallLoggingHandlerTests
{
    [Fact]
    public async Task SendAsync_LogsSuccessfulCall()
    {
        // Arrange
        var repo = Substitute.For<IServiceCallLogRepository>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repo);
        var sp = services.BuildServiceProvider();

        var handler = new ServiceCallLoggingHandler(
            sp, "TestService", Substitute.For<ILogger<ServiceCallLoggingHandler>>())
        {
            InnerHandler = new FakeHttpHandler(new HttpResponseMessage(System.Net.HttpStatusCode.OK))
        };

        var client = new HttpClient(handler);
        var request = new HttpRequestMessage(HttpMethod.Get, "https://example.com/api/test");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);

        // Wait for fire-and-forget to complete
        await Task.Delay(200);

        await repo.Received(1).AddAsync(
            Arg.Is<ServiceCallLogEntry>(e =>
                e.ServiceName == "TestService" &&
                e.IsSuccess &&
                e.HttpMethod == "GET"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_LogsFailedCall()
    {
        // Arrange
        var repo = Substitute.For<IServiceCallLogRepository>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repo);
        var sp = services.BuildServiceProvider();

        var handler = new ServiceCallLoggingHandler(
            sp, "TestService", Substitute.For<ILogger<ServiceCallLoggingHandler>>())
        {
            InnerHandler = new FakeHttpHandler(
                new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError))
        };

        var client = new HttpClient(handler);

        // Act
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "https://example.com/api/test"));

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, response.StatusCode);

        await Task.Delay(200);

        await repo.Received(1).AddAsync(
            Arg.Is<ServiceCallLogEntry>(e =>
                e.ServiceName == "TestService" &&
                !e.IsSuccess &&
                e.StatusCode == 500),
            Arg.Any<CancellationToken>());
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;
        public FakeHttpHandler(HttpResponseMessage response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response);
    }
}
```

- [ ] **Step 3: Run tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ServiceCall" -v n
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Queries/ServiceCalls/ apps/api/tests/Api.Tests/Infrastructure/Http/
git commit -m "test(admin): add unit tests for service call query handler and logging handler"
```

---

### Task 2.7: Frontend — Service Call History Page

**Files:**
- Create: `apps/web/src/lib/api/schemas/admin/admin-service-calls.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/service-calls/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceCallHistory.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceSummaryCards.tsx`
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Create Zod schemas**

```typescript
// apps/web/src/lib/api/schemas/admin/admin-service-calls.schemas.ts
import { z } from 'zod';

export const ServiceCallSchema = z.object({
  id: z.string(),
  serviceName: z.string(),
  httpMethod: z.string(),
  requestUrl: z.string(),
  statusCode: z.number().nullable(),
  latencyMs: z.number(),
  isSuccess: z.boolean(),
  errorMessage: z.string().nullable(),
  correlationId: z.string().nullable(),
  timestampUtc: z.string(),
  requestSummary: z.string().nullable(),
  responseSummary: z.string().nullable(),
});

export const ServiceCallsResponseSchema = z.object({
  items: z.array(ServiceCallSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export const ServiceCallSummarySchema = z.object({
  serviceName: z.string(),
  totalCalls: z.number(),
  successCount: z.number(),
  errorCount: z.number(),
  errorRate: z.number(),
  avgLatencyMs: z.number(),
  p95LatencyMs: z.number(),
  maxLatencyMs: z.number(),
  lastCallAt: z.string().nullable(),
  lastErrorAt: z.string().nullable(),
});

export type ServiceCall = z.infer<typeof ServiceCallSchema>;
export type ServiceCallsResponse = z.infer<typeof ServiceCallsResponseSchema>;
export type ServiceCallSummary = z.infer<typeof ServiceCallSummarySchema>;

export interface ServiceCallFilters {
  service?: string;
  success?: boolean;
  correlationId?: string;
  from?: string;
  to?: string;
  minLatencyMs?: number;
  page?: number;
  pageSize?: number;
}
```

- [ ] **Step 2: Add API client methods**

In `adminMonitorClient.ts`, add:

```typescript
import {
  ServiceCallsResponseSchema,
  ServiceCallSummarySchema,
  type ServiceCallFilters,
  type ServiceCallsResponse,
  type ServiceCallSummary,
} from '../../schemas/admin/admin-service-calls.schemas';

async getServiceCalls(filters: ServiceCallFilters = {}): Promise<ServiceCallsResponse> {
  const params = new URLSearchParams();
  if (filters.service) params.set('service', filters.service);
  if (filters.success !== undefined) params.set('success', String(filters.success));
  if (filters.correlationId) params.set('correlationId', filters.correlationId);
  if (filters.from) params.set('from', filters.from);
  if (filters.to) params.set('to', filters.to);
  if (filters.minLatencyMs) params.set('minLatencyMs', String(filters.minLatencyMs));
  if (filters.page) params.set('page', String(filters.page));
  if (filters.pageSize) params.set('pageSize', String(filters.pageSize));
  const query = params.toString();
  const res = await http.get(`/api/v1/admin/service-calls${query ? `?${query}` : ''}`);
  return ServiceCallsResponseSchema.parse(res);
},

async getServiceCallSummary(period = '24h'): Promise<ServiceCallSummary[]> {
  const res = await http.get(`/api/v1/admin/service-calls/summary?period=${period}`);
  return z.array(ServiceCallSummarySchema).parse(res);
},
```

- [ ] **Step 3: Create ServiceSummaryCards component**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceSummaryCards.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { ServiceCallSummary } from '@/lib/api/schemas/admin/admin-service-calls.schemas';
import { Badge } from '@/components/ui/badge';

const SERVICE_COLORS: Record<string, string> = {
  OpenRouter: 'border-l-purple-500',
  EmbeddingService: 'border-l-blue-500',
  BGG: 'border-l-orange-500',
  Ollama: 'border-l-green-500',
  UnstructuredService: 'border-l-amber-500',
  SmolDoclingService: 'border-l-teal-500',
  RerankerService: 'border-l-pink-500',
};

export function ServiceSummaryCards({ period = '24h' }: { period?: string }) {
  const { data: summaries, isLoading } = useQuery({
    queryKey: ['admin', 'service-call-summary', period],
    queryFn: () => api.admin.getServiceCallSummary(period),
    staleTime: 60_000,
    refetchInterval: 120_000,
  });

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading summaries...</div>;
  if (!summaries?.length) return <div className="text-sm text-muted-foreground">No service calls recorded yet.</div>;

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
      {summaries.map((s: ServiceCallSummary) => (
        <div
          key={s.serviceName}
          className={`border rounded-lg p-4 border-l-4 ${SERVICE_COLORS[s.serviceName] ?? 'border-l-gray-400'}`}
        >
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-semibold text-sm">{s.serviceName}</h3>
            <Badge variant={s.errorRate > 10 ? 'destructive' : s.errorRate > 5 ? 'outline' : 'secondary'}>
              {s.errorRate}% errors
            </Badge>
          </div>

          <div className="grid grid-cols-2 gap-2 text-xs">
            <div>
              <span className="text-muted-foreground">Total calls</span>
              <p className="font-mono font-medium">{s.totalCalls.toLocaleString()}</p>
            </div>
            <div>
              <span className="text-muted-foreground">Avg latency</span>
              <p className="font-mono font-medium">{s.avgLatencyMs}ms</p>
            </div>
            <div>
              <span className="text-muted-foreground">P95 latency</span>
              <p className="font-mono font-medium">{s.p95LatencyMs}ms</p>
            </div>
            <div>
              <span className="text-muted-foreground">Errors</span>
              <p className="font-mono font-medium text-red-600">{s.errorCount}</p>
            </div>
          </div>

          {s.lastErrorAt && (
            <div className="mt-2 text-xs text-muted-foreground">
              Last error: {new Date(s.lastErrorAt).toLocaleString('it-IT')}
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Create ServiceCallHistory component**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/service-calls/ServiceCallHistory.tsx
'use client';

import { useCallback, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { ServiceCall, ServiceCallFilters } from '@/lib/api/schemas/admin/admin-service-calls.schemas';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/components/ui/select';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog';
import { RefreshCw, ChevronLeft, ChevronRight } from 'lucide-react';

const SERVICES = [
  'OpenRouter', 'EmbeddingService', 'Ollama', 'BggApi',
  'UnstructuredService', 'SmolDoclingService', 'HuggingFace',
  'OrchestrationService', 'Infisical',
];

export function ServiceCallHistory() {
  const [filters, setFilters] = useState<ServiceCallFilters>({ page: 1, pageSize: 50 });
  const [selectedCall, setSelectedCall] = useState<ServiceCall | null>(null);

  const { data, isLoading, isFetching, refetch } = useQuery({
    queryKey: ['admin', 'service-calls', filters],
    queryFn: () => api.admin.getServiceCalls(filters),
    staleTime: 30_000,
  });

  const totalPages = data ? Math.ceil(data.totalCount / data.pageSize) : 0;

  const setPage = useCallback((page: number) => {
    setFilters(prev => ({ ...prev, page }));
  }, []);

  return (
    <div className="space-y-4">
      {/* Filters */}
      <div className="flex flex-wrap gap-3 items-center">
        <Select
          value={filters.service ?? 'all'}
          onValueChange={v => setFilters(prev => ({ ...prev, service: v === 'all' ? undefined : v, page: 1 }))}
        >
          <SelectTrigger className="w-[180px]">
            <SelectValue placeholder="All Services" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Services</SelectItem>
            {SERVICES.map(s => <SelectItem key={s} value={s}>{s}</SelectItem>)}
          </SelectContent>
        </Select>

        <Select
          value={filters.success === undefined ? 'all' : String(filters.success)}
          onValueChange={v => setFilters(prev => ({
            ...prev,
            success: v === 'all' ? undefined : v === 'true',
            page: 1,
          }))}
        >
          <SelectTrigger className="w-[130px]">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All</SelectItem>
            <SelectItem value="true">Success</SelectItem>
            <SelectItem value="false">Errors</SelectItem>
          </SelectContent>
        </Select>

        <Input
          placeholder="Correlation ID"
          value={filters.correlationId ?? ''}
          onChange={e => setFilters(prev => ({ ...prev, correlationId: e.target.value || undefined, page: 1 }))}
          className="w-[200px]"
        />

        <Button variant="outline" size="icon" onClick={() => refetch()} disabled={isFetching}>
          <RefreshCw className={`h-4 w-4 ${isFetching ? 'animate-spin' : ''}`} />
        </Button>
      </div>

      {/* Table */}
      <div className="border rounded-lg overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-muted/50">
              <tr>
                <th className="px-3 py-2 text-left">Time</th>
                <th className="px-3 py-2 text-left">Service</th>
                <th className="px-3 py-2 text-left">Method</th>
                <th className="px-3 py-2 text-left">URL</th>
                <th className="px-3 py-2 text-left">Status</th>
                <th className="px-3 py-2 text-right">Latency</th>
              </tr>
            </thead>
            <tbody className="text-xs font-mono">
              {isLoading && (
                <tr><td colSpan={6} className="px-3 py-8 text-center text-muted-foreground">Loading...</td></tr>
              )}
              {!isLoading && (!data?.items || data.items.length === 0) && (
                <tr><td colSpan={6} className="px-3 py-8 text-center text-muted-foreground">No service calls found.</td></tr>
              )}
              {data?.items.map((call: ServiceCall) => (
                <tr
                  key={call.id}
                  className={`border-t hover:bg-muted/30 cursor-pointer ${!call.isSuccess ? 'bg-red-50/50 dark:bg-red-950/20' : ''}`}
                  onClick={() => setSelectedCall(call)}
                >
                  <td className="px-3 py-1.5 whitespace-nowrap text-muted-foreground">
                    {new Date(call.timestampUtc).toLocaleTimeString('it-IT', {
                      hour: '2-digit', minute: '2-digit', second: '2-digit',
                    })}
                  </td>
                  <td className="px-3 py-1.5">{call.serviceName}</td>
                  <td className="px-3 py-1.5">
                    <Badge variant="outline" className="text-xs">{call.httpMethod}</Badge>
                  </td>
                  <td className="px-3 py-1.5 truncate max-w-[300px]" title={call.requestUrl}>
                    {call.requestUrl}
                  </td>
                  <td className="px-3 py-1.5">
                    {call.isSuccess ? (
                      <Badge variant="secondary" className="bg-green-100 text-green-700 text-xs">
                        {call.statusCode}
                      </Badge>
                    ) : (
                      <Badge variant="destructive" className="text-xs">
                        {call.statusCode ?? 'ERR'}
                      </Badge>
                    )}
                  </td>
                  <td className="px-3 py-1.5 text-right">
                    <span className={call.latencyMs > 5000 ? 'text-red-600 font-bold' : call.latencyMs > 2000 ? 'text-amber-600' : ''}>
                      {call.latencyMs}ms
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Pagination */}
      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">
            {data?.totalCount} total calls — Page {data?.page} of {totalPages}
          </span>
          <div className="flex gap-2">
            <Button variant="outline" size="sm" disabled={filters.page === 1} onClick={() => setPage((filters.page ?? 1) - 1)}>
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button variant="outline" size="sm" disabled={filters.page === totalPages} onClick={() => setPage((filters.page ?? 1) + 1)}>
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}

      {/* Detail dialog */}
      <Dialog open={!!selectedCall} onOpenChange={() => setSelectedCall(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Service Call Detail</DialogTitle>
          </DialogHeader>
          {selectedCall && (
            <div className="space-y-3 text-sm">
              <div className="grid grid-cols-2 gap-2">
                <div><span className="text-muted-foreground">Service:</span> {selectedCall.serviceName}</div>
                <div><span className="text-muted-foreground">Method:</span> {selectedCall.httpMethod}</div>
                <div><span className="text-muted-foreground">Status:</span> {selectedCall.statusCode ?? 'N/A'}</div>
                <div><span className="text-muted-foreground">Latency:</span> {selectedCall.latencyMs}ms</div>
              </div>
              <div><span className="text-muted-foreground">URL:</span> <code className="text-xs break-all">{selectedCall.requestUrl}</code></div>
              {selectedCall.correlationId && (
                <div><span className="text-muted-foreground">Correlation ID:</span> <code className="text-xs">{selectedCall.correlationId}</code></div>
              )}
              {selectedCall.errorMessage && (
                <div>
                  <span className="text-muted-foreground">Error:</span>
                  <pre className="mt-1 p-2 bg-red-50 dark:bg-red-950/30 rounded text-xs whitespace-pre-wrap">{selectedCall.errorMessage}</pre>
                </div>
              )}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
```

- [ ] **Step 5: Create page**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/service-calls/page.tsx
'use client';

import { ServiceSummaryCards } from './ServiceSummaryCards';
import { ServiceCallHistory } from './ServiceCallHistory';

export default function ServiceCallsPage() {
  return (
    <div data-testid="service-calls-page" className="space-y-6">
      <div>
        <h1 className="text-2xl font-bold tracking-tight">Service Call History</h1>
        <p className="text-muted-foreground">External service interactions, latency, and error tracking</p>
      </div>

      <ServiceSummaryCards />
      <ServiceCallHistory />
    </div>
  );
}
```

- [ ] **Step 6: Add nav item**

In `apps/web/src/config/admin-dashboard-navigation.ts`, find the `system` section's `sidebarItems` array. Add after the "Logs" entry:

```typescript
{
  href: '/admin/monitor/service-calls',
  label: 'Service Calls',
  icon: ActivityIcon,
  activePattern: /^\/admin\/monitor\/service-calls/,
},
```

Add `ActivityIcon` to the lucide-react imports at the top of the file.

- [ ] **Step 7: Verify build**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/lib/api/schemas/admin/admin-service-calls.schemas.ts apps/web/src/lib/api/clients/admin/adminMonitorClient.ts apps/web/src/app/admin/(dashboard)/monitor/service-calls/ apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(web): add service call history page with summary cards and detail modal"
```

---

## Phase 3: Enhanced Service Dashboard

### Task 3.1: Backend — Circuit Breaker State Tracker

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTracker.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/CircuitBreakerStateDto.cs`

- [ ] **Step 1: Create DTO**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/CircuitBreakerStateDto.cs
namespace Api.BoundedContexts.Administration.Application.DTOs;

public sealed record CircuitBreakerStateDto(
    string ServiceName,
    string State,
    int TripCount,
    DateTime? LastTrippedAt,
    DateTime? LastResetAt,
    string? LastError);
```

- [ ] **Step 2: Create state tracker (singleton)**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTracker.cs
using System.Collections.Concurrent;
using Api.BoundedContexts.Administration.Application.DTOs;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

public interface ICircuitBreakerStateTracker
{
    void RecordBreak(string serviceName, string? errorMessage);
    void RecordReset(string serviceName);
    void RecordHalfOpen(string serviceName);
    IReadOnlyList<CircuitBreakerStateDto> GetAllStates();
    CircuitBreakerStateDto? GetState(string serviceName);
    void RegisterService(string serviceName);
}

public sealed class CircuitBreakerStateTracker : ICircuitBreakerStateTracker
{
    private readonly ConcurrentDictionary<string, CircuitBreakerInfo> _states = new();

    public void RegisterService(string serviceName)
    {
        _states.TryAdd(serviceName, new CircuitBreakerInfo { ServiceName = serviceName });
    }

    public void RecordBreak(string serviceName, string? errorMessage)
    {
        var info = _states.GetOrAdd(serviceName, _ => new CircuitBreakerInfo { ServiceName = serviceName });
        info.State = "Open";
        info.TripCount++;
        info.LastTrippedAt = DateTime.UtcNow;
        info.LastError = errorMessage;
    }

    public void RecordReset(string serviceName)
    {
        if (_states.TryGetValue(serviceName, out var info))
        {
            info.State = "Closed";
            info.LastResetAt = DateTime.UtcNow;
        }
    }

    public void RecordHalfOpen(string serviceName)
    {
        if (_states.TryGetValue(serviceName, out var info))
        {
            info.State = "HalfOpen";
        }
    }

    public IReadOnlyList<CircuitBreakerStateDto> GetAllStates()
    {
        return _states.Values
            .Select(i => new CircuitBreakerStateDto(
                i.ServiceName, i.State, i.TripCount,
                i.LastTrippedAt, i.LastResetAt, i.LastError))
            .OrderBy(x => x.ServiceName)
            .ToList();
    }

    public CircuitBreakerStateDto? GetState(string serviceName)
    {
        return _states.TryGetValue(serviceName, out var info)
            ? new CircuitBreakerStateDto(info.ServiceName, info.State, info.TripCount,
                info.LastTrippedAt, info.LastResetAt, info.LastError)
            : null;
    }

    private sealed class CircuitBreakerInfo
    {
        public string ServiceName { get; set; } = "";
        public string State { get; set; } = "Closed";
        public int TripCount { get; set; }
        public DateTime? LastTrippedAt { get; set; }
        public DateTime? LastResetAt { get; set; }
        public string? LastError { get; set; }
    }
}
```

- [ ] **Step 3: Register as singleton**

In `AdministrationServiceExtensions.cs`, add:

```csharp
services.AddSingleton<ICircuitBreakerStateTracker, CircuitBreakerStateTracker>();
```

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTracker.cs apps/api/src/Api/BoundedContexts/Administration/Application/DTOs/CircuitBreakerStateDto.cs apps/api/src/Api/BoundedContexts/Administration/Infrastructure/DependencyInjection/AdministrationServiceExtensions.cs
git commit -m "feat(admin): add CircuitBreakerStateTracker singleton for Polly state visibility"
```

---

### Task 3.2: Backend — Wire Polly Callbacks + Endpoints

**Files:**
- Modify: `apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs`
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/CircuitBreakers/GetCircuitBreakerStatesQuery.cs`
- Create: `apps/api/src/Api/Routing/AdminCircuitBreakerEndpoints.cs`

- [ ] **Step 1: Modify Polly circuit breaker factory to include callbacks**

In `InfrastructureServiceExtensions.cs`, replace the `GetCircuitBreakerPolicy` method:

```csharp
private static AsyncCircuitBreakerPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
    IServiceProvider? sp = null, string? serviceName = null)
{
    var tracker = sp?.GetService<ICircuitBreakerStateTracker>();

    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30),
            onBreak: (outcome, duration) =>
            {
                tracker?.RecordBreak(serviceName ?? "Unknown",
                    outcome.Exception?.Message ?? outcome.Result?.ReasonPhrase);
            },
            onReset: () =>
            {
                tracker?.RecordReset(serviceName ?? "Unknown");
            },
            onHalfOpen: () =>
            {
                tracker?.RecordHalfOpen(serviceName ?? "Unknown");
            });
}
```

Update all callers to pass `sp` and `serviceName`. For clients registered via `AddHttpClient`, the service provider is available through the `ConfigureHttpClient` overload. The simplest approach: register callbacks post-build via a helper:

```csharp
// For each named client with circuit breakers:
services.AddHttpClient("OpenRouter", client => { ... })
    .AddTransientHttpErrorPolicy(policy => policy.WaitAndRetryAsync(...))
    .AddHttpMessageHandler(sp =>
    {
        var tracker = sp.GetRequiredService<ICircuitBreakerStateTracker>();
        tracker.RegisterService("OpenRouter");
        return new CircuitBreakerCallbackHandler(tracker, "OpenRouter");
    })
    .AddServiceCallLogging("OpenRouter")
    .ConfigurePrimaryHttpMessageHandler(...);
```

Note: The exact wiring depends on how Polly policies are structured. If the existing `GetCircuitBreakerPolicy()` is shared, modify it to accept the tracker. If per-client, wire individually.

A simpler alternative: just register all services at startup and rely on the `ServiceCallLoggingHandler` to also track errors. The `CircuitBreakerStateTracker` monitors via Polly's `onBreak`/`onReset` callbacks.

- [ ] **Step 2: Create query**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/Queries/CircuitBreakers/GetCircuitBreakerStatesQuery.cs
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.CircuitBreakers;

internal record GetCircuitBreakerStatesQuery : IQuery<IReadOnlyList<CircuitBreakerStateDto>>;

internal sealed class GetCircuitBreakerStatesQueryHandler
    : IQueryHandler<GetCircuitBreakerStatesQuery, IReadOnlyList<CircuitBreakerStateDto>>
{
    private readonly ICircuitBreakerStateTracker _tracker;

    public GetCircuitBreakerStatesQueryHandler(ICircuitBreakerStateTracker tracker)
    {
        _tracker = tracker ?? throw new ArgumentNullException(nameof(tracker));
    }

    public Task<IReadOnlyList<CircuitBreakerStateDto>> Handle(
        GetCircuitBreakerStatesQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(_tracker.GetAllStates());
    }
}
```

- [ ] **Step 3: Create endpoints**

```csharp
// apps/api/src/Api/Routing/AdminCircuitBreakerEndpoints.cs
using Api.BoundedContexts.Administration.Application.Queries.CircuitBreakers;
using Api.Infrastructure.Auth;
using MediatR;

namespace Api.Routing;

internal static class AdminCircuitBreakerEndpoints
{
    public static RouteGroupBuilder MapAdminCircuitBreakerEndpoints(this RouteGroupBuilder group)
    {
        var cbGroup = group.MapGroup("/admin/circuit-breakers")
            .WithTags("Admin", "CircuitBreakers");

        cbGroup.MapGet("/", async (
            IMediator mediator,
            HttpContext httpContext,
            CancellationToken ct) =>
        {
            var (session, error) = httpContext.RequireAdminSession();
            if (error is not null) return error;

            var result = await mediator.Send(new GetCircuitBreakerStatesQuery(), ct);
            return Results.Ok(result);
        })
        .WithSummary("Get circuit breaker states for all external services");

        return group;
    }
}
```

- [ ] **Step 4: Wire endpoint**

Add `group.MapAdminCircuitBreakerEndpoints();` to the admin routing configuration.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Extensions/InfrastructureServiceExtensions.cs apps/api/src/Api/BoundedContexts/Administration/Application/Queries/CircuitBreakers/ apps/api/src/Api/Routing/AdminCircuitBreakerEndpoints.cs
git commit -m "feat(admin): add circuit breaker state endpoint with Polly callback wiring"
```

---

### Task 3.3: Backend — Unit Tests for Circuit Breaker Tracker

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTrackerTests.cs`

- [ ] **Step 1: Write tests**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTrackerTests.cs
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class CircuitBreakerStateTrackerTests
{
    private readonly CircuitBreakerStateTracker _sut = new();

    [Fact]
    public void RegisterService_CreatesClosedState()
    {
        _sut.RegisterService("TestService");

        var state = _sut.GetState("TestService");

        Assert.NotNull(state);
        Assert.Equal("Closed", state.State);
        Assert.Equal(0, state.TripCount);
    }

    [Fact]
    public void RecordBreak_SetsOpenStateAndIncrementsTripCount()
    {
        _sut.RegisterService("TestService");

        _sut.RecordBreak("TestService", "Connection refused");

        var state = _sut.GetState("TestService");
        Assert.Equal("Open", state!.State);
        Assert.Equal(1, state.TripCount);
        Assert.Equal("Connection refused", state.LastError);
        Assert.NotNull(state.LastTrippedAt);
    }

    [Fact]
    public void RecordReset_SetsClosedState()
    {
        _sut.RegisterService("TestService");
        _sut.RecordBreak("TestService", "error");

        _sut.RecordReset("TestService");

        var state = _sut.GetState("TestService");
        Assert.Equal("Closed", state!.State);
        Assert.NotNull(state.LastResetAt);
    }

    [Fact]
    public void RecordHalfOpen_SetsHalfOpenState()
    {
        _sut.RegisterService("TestService");
        _sut.RecordBreak("TestService", "error");

        _sut.RecordHalfOpen("TestService");

        var state = _sut.GetState("TestService");
        Assert.Equal("HalfOpen", state!.State);
    }

    [Fact]
    public void GetAllStates_ReturnsAllRegisteredServices()
    {
        _sut.RegisterService("ServiceA");
        _sut.RegisterService("ServiceB");

        var states = _sut.GetAllStates();

        Assert.Equal(2, states.Count);
    }

    [Fact]
    public void GetState_UnknownService_ReturnsNull()
    {
        var state = _sut.GetState("Unknown");
        Assert.Null(state);
    }

    [Fact]
    public void MultipleBr breaks_IncrementTripCount()
    {
        _sut.RegisterService("TestService");

        _sut.RecordBreak("TestService", "error1");
        _sut.RecordReset("TestService");
        _sut.RecordBreak("TestService", "error2");

        var state = _sut.GetState("TestService");
        Assert.Equal(2, state!.TripCount);
        Assert.Equal("error2", state.LastError);
    }
}
```

- [ ] **Step 2: Run tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~CircuitBreakerStateTrackerTests" -v n
```

Expected: All 7 tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Infrastructure/Services/CircuitBreakerStateTrackerTests.cs
git commit -m "test(admin): add unit tests for CircuitBreakerStateTracker"
```

---

### Task 3.4: Frontend — Circuit Breaker Panel + Enhanced Service Dashboard

**Files:**
- Create: `apps/web/src/lib/api/schemas/admin/admin-circuit-breakers.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminMonitorClient.ts`
- Create: `apps/web/src/app/admin/(dashboard)/monitor/services/CircuitBreakerPanel.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`

- [ ] **Step 1: Create Zod schemas**

```typescript
// apps/web/src/lib/api/schemas/admin/admin-circuit-breakers.schemas.ts
import { z } from 'zod';

export const CircuitBreakerStateSchema = z.object({
  serviceName: z.string(),
  state: z.string(),
  tripCount: z.number(),
  lastTrippedAt: z.string().nullable(),
  lastResetAt: z.string().nullable(),
  lastError: z.string().nullable(),
});

export type CircuitBreakerState = z.infer<typeof CircuitBreakerStateSchema>;
```

- [ ] **Step 2: Add API method**

In `adminMonitorClient.ts`, add:

```typescript
import { CircuitBreakerStateSchema, type CircuitBreakerState } from '../../schemas/admin/admin-circuit-breakers.schemas';

async getCircuitBreakerStates(): Promise<CircuitBreakerState[]> {
  const res = await http.get('/api/v1/admin/circuit-breakers');
  return z.array(CircuitBreakerStateSchema).parse(res);
},
```

- [ ] **Step 3: Create CircuitBreakerPanel component**

```tsx
// apps/web/src/app/admin/(dashboard)/monitor/services/CircuitBreakerPanel.tsx
'use client';

import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { CircuitBreakerState } from '@/lib/api/schemas/admin/admin-circuit-breakers.schemas';
import { Badge } from '@/components/ui/badge';

const STATE_COLORS: Record<string, string> = {
  Closed: 'bg-green-100 text-green-700',
  Open: 'bg-red-100 text-red-700',
  HalfOpen: 'bg-amber-100 text-amber-700',
};

export function CircuitBreakerPanel() {
  const { data: states, isLoading } = useQuery({
    queryKey: ['admin', 'circuit-breakers'],
    queryFn: () => api.admin.getCircuitBreakerStates(),
    staleTime: 15_000,
    refetchInterval: 30_000,
  });

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading circuit breaker states...</div>;
  if (!states?.length) return <div className="text-sm text-muted-foreground">No circuit breakers registered.</div>;

  const hasIssues = states.some((s: CircuitBreakerState) => s.state !== 'Closed');

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <h2 className="font-semibold text-lg">Circuit Breakers</h2>
        {hasIssues && <Badge variant="destructive">Issues detected</Badge>}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {states.map((cb: CircuitBreakerState) => (
          <div
            key={cb.serviceName}
            className={`border rounded-lg p-3 ${cb.state !== 'Closed' ? 'border-red-300 bg-red-50/30 dark:bg-red-950/10' : ''}`}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="font-medium text-sm">{cb.serviceName}</span>
              <Badge className={STATE_COLORS[cb.state] ?? 'bg-gray-100 text-gray-700'}>
                {cb.state}
              </Badge>
            </div>

            <div className="text-xs text-muted-foreground space-y-0.5">
              <div>Trips: {cb.tripCount}</div>
              {cb.lastTrippedAt && (
                <div>Last trip: {new Date(cb.lastTrippedAt).toLocaleString('it-IT')}</div>
              )}
              {cb.lastError && (
                <div className="text-red-600 truncate" title={cb.lastError}>
                  Error: {cb.lastError}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Add CircuitBreakerPanel to services page**

In `apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx`, add the import and component:

```tsx
import { CircuitBreakerPanel } from './CircuitBreakerPanel';

// In the JSX, add after <RestartServicePanel />:
<CircuitBreakerPanel />
```

- [ ] **Step 5: Verify build**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/schemas/admin/admin-circuit-breakers.schemas.ts apps/web/src/lib/api/clients/admin/adminMonitorClient.ts apps/web/src/app/admin/(dashboard)/monitor/services/CircuitBreakerPanel.tsx apps/web/src/app/admin/(dashboard)/monitor/services/page.tsx
git commit -m "feat(web): add circuit breaker panel to service dashboard"
```

---

### Task 3.5: Final — Verify Full Build + Test Suite

- [ ] **Step 1: Run backend build**

```bash
cd apps/api/src/Api && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 2: Run backend tests**

```bash
cd apps/api && dotnet test --filter "Category=Unit&BoundedContext=Administration" -v n
```

Expected: All new and existing Administration tests pass.

- [ ] **Step 3: Run frontend build**

```bash
cd apps/web && pnpm build
```

Expected: Build succeeds.

- [ ] **Step 4: Run frontend lint and typecheck**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: No errors.

- [ ] **Step 5: Commit any fixes**

If any issues found, fix and commit with:

```bash
git commit -m "fix(admin): resolve build/test issues from logging diagnostics feature"
```
