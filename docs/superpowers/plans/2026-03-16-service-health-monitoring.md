# Service Health Monitoring Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Proactive health monitoring that detects service state transitions and dispatches multi-channel alerts (Slack + Email) automatically.

**Architecture:** BackgroundService polls ASP.NET HealthChecks every 60s, state machine detects transitions (Healthy/Degraded/Unhealthy), publishes MediatR events handled by existing AlertingService with extended multi-channel Slack routing.

**Tech Stack:** .NET 9, MediatR, EF Core, PostgreSQL, ASP.NET HealthChecks, Slack Incoming Webhooks

**Spec:** `docs/superpowers/specs/2026-03-16-service-health-monitoring-design.md`

---

## File Structure

### New Files
| File | Responsibility |
|------|---------------|
| `Api/Infrastructure/Configuration/HealthMonitorOptions.cs` | Options class for polling interval, thresholds |
| `Api/Infrastructure/Entities/Administration/ServiceHealthStateEntity.cs` | EF Core entity for persisted health state |
| `Api/Infrastructure/EntityConfigurations/Administration/ServiceHealthStateEntityConfiguration.cs` | EF Core fluent config |
| `Api/Infrastructure/BackgroundServices/InfrastructureHealthMonitorService.cs` | Polling loop + state machine |
| `Api/BoundedContexts/Administration/Domain/Events/HealthStatusChangedEvent.cs` | MediatR INotification |
| `Api/BoundedContexts/Administration/Application/EventHandlers/HealthStatusChangedEventHandler.cs` | Routes event → AlertingService |
| `Api/Routing/StatusPageEndpoints.cs` | `/status` HTML + `/status/details` JSON |
| `Api/Services/StatusPageRenderer.cs` | Inline HTML/CSS renderer |

### Modified Files
| File | Change |
|------|--------|
| `Api/Infrastructure/MeepleAiDbContext.cs` | Add `DbSet<ServiceHealthStateEntity>` |
| `Api/Services/IAlertingService.cs` | Add overload with `category` param |
| `Api/Services/AlertingService.cs` | Implement category routing for Slack |
| `Api/Services/SlackAlertChannel.cs` | Read `_slack_webhook`/`_slack_channel` from metadata |
| `Api/appsettings.json` | Add `HealthMonitor` section + `Slack.ChannelRouting` |

### Test Files
| File | Tests |
|------|-------|
| `Api.Tests/Infrastructure/BackgroundServices/InfrastructureHealthMonitorServiceTests.cs` | State machine transitions, hysteresis, reminders |
| `Api.Tests/BoundedContexts/Administration/EventHandlers/HealthStatusChangedEventHandlerTests.cs` | Tag→category mapping, severity mapping |
| `Api.Tests/Services/SlackAlertChannelRoutingTests.cs` | Multi-channel routing logic |
| `Api.Tests/Services/StatusPageRendererTests.cs` | HTML output validation |

---

## Chunk 1: Domain Model + Configuration (Phase C foundation)

### Task 1: HealthMonitorOptions

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Configuration/HealthMonitorOptions.cs`

- [ ] **Step 1: Create options class**

```csharp
// apps/api/src/Api/Infrastructure/Configuration/HealthMonitorOptions.cs
namespace Api.Infrastructure.Configuration;

/// <summary>
/// Configuration options for the Infrastructure Health Monitor background service.
/// Epic #448: Service Health Monitoring.
/// </summary>
public sealed class HealthMonitorOptions
{
    public const string SectionName = "HealthMonitor";

    /// <summary>Polling interval in seconds. Default: 60.</summary>
    public int PollingIntervalSeconds { get; set; } = 60;

    /// <summary>Startup delay before first poll in seconds. Default: 10.</summary>
    public int StartupDelaySeconds { get; set; } = 10;

    /// <summary>Re-alert interval for persistent unhealthy services in minutes. Default: 30.</summary>
    public int ReminderIntervalMinutes { get; set; } = 30;

    /// <summary>Failed checks before transitioning Healthy → Degraded. Default: 1.</summary>
    public int DegradedThreshold { get; set; } = 1;

    /// <summary>Consecutive failures before Degraded → Unhealthy. Default: 3.</summary>
    public int UnhealthyThreshold { get; set; } = 3;

    /// <summary>Consecutive successes before Unhealthy → Healthy. Default: 2.</summary>
    public int RecoveryThreshold { get; set; } = 2;
}
```

- [ ] **Step 2: Add config section to appsettings.json**

In `apps/api/src/Api/appsettings.json`, add before the closing `}`:

```json
"HealthMonitor": {
  "PollingIntervalSeconds": 60,
  "StartupDelaySeconds": 10,
  "ReminderIntervalMinutes": 30,
  "DegradedThreshold": 1,
  "UnhealthyThreshold": 3,
  "RecoveryThreshold": 2
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Configuration/HealthMonitorOptions.cs apps/api/src/Api/appsettings.json
git commit -m "feat(health-monitor): add HealthMonitorOptions configuration"
```

---

### Task 2: ServiceHealthStateEntity + EF Config + Migration

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/Administration/ServiceHealthStateEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/ServiceHealthStateEntityConfiguration.cs`
- Modify: `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`

- [ ] **Step 1: Create entity**

```csharp
// apps/api/src/Api/Infrastructure/Entities/Administration/ServiceHealthStateEntity.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.Administration;

/// <summary>
/// Persisted health state for a monitored service.
/// Hydrated into memory on startup, updated after each poll cycle.
/// Epic #448: Service Health Monitoring.
/// </summary>
[Table("service_health_states")]
public class ServiceHealthStateEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    [Column("service_name")]
    public string ServiceName { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("current_status")]
    public string CurrentStatus { get; set; } = "Healthy";

    [Required]
    [MaxLength(20)]
    [Column("previous_status")]
    public string PreviousStatus { get; set; } = "Healthy";

    [Column("consecutive_failures")]
    public int ConsecutiveFailures { get; set; }

    [Column("consecutive_successes")]
    public int ConsecutiveSuccesses { get; set; }

    [Required]
    [Column("last_transition_at")]
    public DateTime LastTransitionAt { get; set; } = DateTime.UtcNow;

    [Column("last_notified_at")]
    public DateTime? LastNotifiedAt { get; set; }

    [Column("last_description")]
    public string? LastDescription { get; set; }

    [Required]
    [Column("tags", TypeName = "jsonb")]
    public string Tags { get; set; } = "[]";

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Required]
    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Create EF configuration**

```csharp
// apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/ServiceHealthStateEntityConfiguration.cs
using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.EntityConfigurations.Administration;

internal sealed class ServiceHealthStateEntityConfiguration
    : IEntityTypeConfiguration<ServiceHealthStateEntity>
{
    public void Configure(EntityTypeBuilder<ServiceHealthStateEntity> builder)
    {
        builder.HasIndex(e => e.ServiceName).IsUnique();
    }
}
```

- [ ] **Step 3: Add DbSet to MeepleAiDbContext**

In `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs`, add the DbSet property alongside the existing `Alerts` DbSet:

```csharp
public DbSet<ServiceHealthStateEntity> ServiceHealthStates { get; set; }
```

Add the using if not present:
```csharp
using Api.Infrastructure.Entities.Administration;
```

- [ ] **Step 4: Create migration**

```bash
cd apps/api/src/Api
dotnet ef migrations add AddServiceHealthStates
```

After generating, manually add to the migration's `Up` method:

```csharp
// Widen alert_type column for health monitor alert types
migrationBuilder.AlterColumn<string>(
    name: "alert_type",
    table: "alerts",
    maxLength: 100,
    nullable: false,
    oldMaxLength: 50);
```

Also update `AlertEntity.cs` — change `[MaxLength(50)]` to `[MaxLength(100)]` on `AlertType`.

- [ ] **Step 5: Verify migration compiles**

```bash
dotnet build --no-restore
```

Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/Administration/ServiceHealthStateEntity.cs \
  apps/api/src/Api/Infrastructure/EntityConfigurations/Administration/ServiceHealthStateEntityConfiguration.cs \
  apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs \
  apps/api/src/Api/Infrastructure/Entities/Administration/AlertEntity.cs \
  apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(health-monitor): add ServiceHealthState entity and migration"
```

---

### Task 3: HealthStatusChangedEvent

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Domain/Events/HealthStatusChangedEvent.cs`

- [ ] **Step 1: Create event**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Domain/Events/HealthStatusChangedEvent.cs
using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Published when a monitored service transitions health state.
/// Epic #448: Service Health Monitoring.
/// </summary>
public record HealthStatusChangedEvent(
    string ServiceName,
    string PreviousStatus,
    string CurrentStatus,
    string? Description,
    string[] Tags,
    DateTime TransitionAt,
    bool IsReminder
) : INotification;
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Administration/Domain/Events/HealthStatusChangedEvent.cs
git commit -m "feat(health-monitor): add HealthStatusChangedEvent"
```

---

## Chunk 2: State Machine + BackgroundService (Phase C core)

### Task 4: State Machine Tests (TDD)

**Files:**
- Create: `apps/api/tests/Api.Tests/Infrastructure/BackgroundServices/HealthMonitorStateMachineTests.cs`

- [ ] **Step 1: Write state machine transition tests**

```csharp
// apps/api/tests/Api.Tests/Infrastructure/BackgroundServices/HealthMonitorStateMachineTests.cs
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Tests.Infrastructure.BackgroundServices;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class HealthMonitorStateMachineTests
{
    private readonly HealthMonitorOptions _options = new()
    {
        DegradedThreshold = 1,
        UnhealthyThreshold = 3,
        RecoveryThreshold = 2,
        ReminderIntervalMinutes = 30
    };

    [Fact]
    public void Healthy_service_transitions_to_Degraded_after_one_failure()
    {
        var state = CreateHealthyState("postgres");
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, _options);

        Assert.Equal("Degraded", result.CurrentStatus);
        Assert.Equal("Healthy", result.PreviousStatus);
        Assert.Equal(1, result.ConsecutiveFailures);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Degraded_service_transitions_to_Unhealthy_after_threshold_failures()
    {
        var state = CreateState("redis", "Degraded", consecutiveFailures: 2);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, _options);

        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal("Degraded", result.PreviousStatus);
        Assert.Equal(3, result.ConsecutiveFailures);
    }

    [Fact]
    public void Degraded_stays_Degraded_before_threshold()
    {
        var state = CreateState("redis", "Degraded", consecutiveFailures: 1);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, _options);

        Assert.Equal("Degraded", result.CurrentStatus);
        Assert.Equal(2, result.ConsecutiveFailures);
    }

    [Fact]
    public void Unhealthy_service_recovers_after_threshold_successes()
    {
        var state = CreateState("qdrant", "Unhealthy", consecutiveSuccesses: 1);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, _options);

        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal("Unhealthy", result.PreviousStatus);
        Assert.Equal(2, result.ConsecutiveSuccesses);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Unhealthy_stays_Unhealthy_before_recovery_threshold()
    {
        var state = CreateState("qdrant", "Unhealthy", consecutiveSuccesses: 0);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, _options);

        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal(1, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Unhealthy_resets_success_counter_on_failure()
    {
        var state = CreateState("embedding", "Unhealthy", consecutiveSuccesses: 1);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, _options);

        Assert.Equal("Unhealthy", result.CurrentStatus);
        Assert.Equal(0, result.ConsecutiveSuccesses);
    }

    [Fact]
    public void Degraded_recovers_to_Healthy_on_success()
    {
        var state = CreateState("smtp", "Degraded", consecutiveFailures: 1);
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, _options);

        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Healthy_stays_Healthy_on_success()
    {
        var state = CreateHealthyState("postgres");
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, _options);

        Assert.Equal("Healthy", result.CurrentStatus);
        Assert.Equal(0, result.ConsecutiveFailures);
    }

    [Fact]
    public void Transition_detected_returns_true()
    {
        var state = CreateHealthyState("redis");
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Unhealthy, _options);

        Assert.True(HealthStateMachine.HasTransitioned(state, result));
    }

    [Fact]
    public void No_transition_returns_false()
    {
        var state = CreateHealthyState("redis");
        var result = HealthStateMachine.Evaluate(state, HealthStatus.Healthy, _options);

        Assert.False(HealthStateMachine.HasTransitioned(state, result));
    }

    [Fact]
    public void Reminder_needed_when_unhealthy_past_interval()
    {
        var state = CreateState("postgres", "Unhealthy",
            lastNotifiedAt: DateTime.UtcNow.AddMinutes(-35));

        Assert.True(HealthStateMachine.NeedsReminder(state, _options));
    }

    [Fact]
    public void Reminder_not_needed_when_recently_notified()
    {
        var state = CreateState("postgres", "Unhealthy",
            lastNotifiedAt: DateTime.UtcNow.AddMinutes(-10));

        Assert.False(HealthStateMachine.NeedsReminder(state, _options));
    }

    private static ServiceHealthState CreateHealthyState(string name) =>
        new(name, "Healthy", "Healthy", 0, 0, DateTime.UtcNow, null, null);

    private static ServiceHealthState CreateState(
        string name, string status,
        int consecutiveFailures = 0, int consecutiveSuccesses = 0,
        DateTime? lastNotifiedAt = null) =>
        new(name, status, status, consecutiveFailures, consecutiveSuccesses,
            DateTime.UtcNow, lastNotifiedAt, null);
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd apps/api/src/Api && dotnet test --filter "HealthMonitorStateMachineTests" --no-restore
```

Expected: FAIL — `HealthStateMachine` and `ServiceHealthState` types don't exist yet.

- [ ] **Step 3: Commit failing tests**

```bash
git add apps/api/tests/Api.Tests/Infrastructure/BackgroundServices/HealthMonitorStateMachineTests.cs
git commit -m "test(health-monitor): add state machine transition tests (red)"
```

---

### Task 5: State Machine Implementation (Green)

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/HealthStateMachine.cs`

- [ ] **Step 1: Create ServiceHealthState record + HealthStateMachine**

```csharp
// apps/api/src/Api/Infrastructure/BackgroundServices/HealthStateMachine.cs
using Api.Infrastructure.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// In-memory health state for a single monitored service.
/// </summary>
public record ServiceHealthState(
    string ServiceName,
    string CurrentStatus,
    string PreviousStatus,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTime LastTransitionAt,
    DateTime? LastNotifiedAt,
    string? LastDescription
);

/// <summary>
/// Pure-function state machine for health status transitions.
/// Epic #448: Service Health Monitoring.
/// </summary>
public static class HealthStateMachine
{
    public static ServiceHealthState Evaluate(
        ServiceHealthState current,
        HealthStatus checkResult,
        HealthMonitorOptions options)
    {
        var isFailure = checkResult != HealthStatus.Healthy;

        return current.CurrentStatus switch
        {
            "Healthy" when isFailure =>
                current with
                {
                    CurrentStatus = "Degraded",
                    PreviousStatus = "Healthy",
                    ConsecutiveFailures = 1,
                    ConsecutiveSuccesses = 0,
                    LastTransitionAt = DateTime.UtcNow
                },

            "Healthy" =>
                current with { ConsecutiveFailures = 0, ConsecutiveSuccesses = 0 },

            "Degraded" when isFailure =>
                current.ConsecutiveFailures + 1 >= options.UnhealthyThreshold
                    ? current with
                    {
                        CurrentStatus = "Unhealthy",
                        PreviousStatus = "Degraded",
                        ConsecutiveFailures = current.ConsecutiveFailures + 1,
                        ConsecutiveSuccesses = 0,
                        LastTransitionAt = DateTime.UtcNow
                    }
                    : current with
                    {
                        ConsecutiveFailures = current.ConsecutiveFailures + 1,
                        ConsecutiveSuccesses = 0
                    },

            "Degraded" =>
                current with
                {
                    CurrentStatus = "Healthy",
                    PreviousStatus = "Degraded",
                    ConsecutiveFailures = 0,
                    ConsecutiveSuccesses = 0,
                    LastTransitionAt = DateTime.UtcNow
                },

            "Unhealthy" when !isFailure =>
                current.ConsecutiveSuccesses + 1 >= options.RecoveryThreshold
                    ? current with
                    {
                        CurrentStatus = "Healthy",
                        PreviousStatus = "Unhealthy",
                        ConsecutiveFailures = 0,
                        ConsecutiveSuccesses = current.ConsecutiveSuccesses + 1,
                        LastTransitionAt = DateTime.UtcNow
                    }
                    : current with
                    {
                        ConsecutiveSuccesses = current.ConsecutiveSuccesses + 1
                    },

            "Unhealthy" =>
                current with
                {
                    ConsecutiveSuccesses = 0,
                    ConsecutiveFailures = current.ConsecutiveFailures + 1
                },

            _ => current
        };
    }

    public static bool HasTransitioned(ServiceHealthState before, ServiceHealthState after) =>
        before.CurrentStatus != after.CurrentStatus;

    public static bool NeedsReminder(ServiceHealthState state, HealthMonitorOptions options) =>
        state.CurrentStatus == "Unhealthy" &&
        state.LastNotifiedAt.HasValue &&
        (DateTime.UtcNow - state.LastNotifiedAt.Value).TotalMinutes >= options.ReminderIntervalMinutes;
}
```

- [ ] **Step 2: Run tests — verify they pass**

```bash
cd apps/api/src/Api && dotnet test --filter "HealthMonitorStateMachineTests" --no-restore
```

Expected: All 12 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/HealthStateMachine.cs
git commit -m "feat(health-monitor): implement HealthStateMachine with hysteresis"
```

---

### Task 6: InfrastructureHealthMonitorService

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/InfrastructureHealthMonitorService.cs`

- [ ] **Step 1: Create the BackgroundService**

```csharp
// apps/api/src/Api/Infrastructure/BackgroundServices/InfrastructureHealthMonitorService.cs
using System.Text.Json;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure.Configuration;
using Api.Infrastructure.Entities.Administration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Proactive health monitor that polls HealthCheckService every N seconds,
/// detects state transitions, and publishes MediatR events for alerting.
/// Epic #448: Service Health Monitoring.
/// </summary>
internal sealed class InfrastructureHealthMonitorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IPublisher _publisher;
    private readonly HealthMonitorOptions _options;
    private readonly ILogger<InfrastructureHealthMonitorService> _logger;
    private readonly Dictionary<string, ServiceHealthState> _states = new(StringComparer.OrdinalIgnoreCase);

    public InfrastructureHealthMonitorService(
        IServiceScopeFactory scopeFactory,
        IPublisher publisher,
        IOptions<HealthMonitorOptions> options,
        ILogger<InfrastructureHealthMonitorService> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[HealthMonitor] Starting, waiting {Delay}s for initialization",
            _options.StartupDelaySeconds);

        await Task.Delay(TimeSpan.FromSeconds(_options.StartupDelaySeconds), stoppingToken)
            .ConfigureAwait(false);

        await HydrateStateFromDatabaseAsync(stoppingToken).ConfigureAwait(false);

        _logger.LogInformation(
            "[HealthMonitor] Hydrated {Count} service states, polling every {Interval}s",
            _states.Count, _options.PollingIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollHealthChecksAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[HealthMonitor] Poll cycle failed");
            }
#pragma warning restore CA1031

            await Task.Delay(
                TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken)
                .ConfigureAwait(false);
        }
    }

    private async Task HydrateStateFromDatabaseAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var entities = await db.ServiceHealthStates
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var entity in entities)
        {
            _states[entity.ServiceName] = new ServiceHealthState(
                entity.ServiceName,
                entity.CurrentStatus,
                entity.PreviousStatus,
                entity.ConsecutiveFailures,
                entity.ConsecutiveSuccesses,
                entity.LastTransitionAt,
                entity.LastNotifiedAt,
                entity.LastDescription);
        }
    }

    private async Task PollHealthChecksAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var report = await healthCheckService.CheckHealthAsync(ct).ConfigureAwait(false);

        foreach (var (name, entry) in report.Entries)
        {
            var tags = entry.Tags.ToArray();
            var before = _states.GetValueOrDefault(name)
                ?? new ServiceHealthState(name, "Healthy", "Healthy", 0, 0, DateTime.UtcNow, null, null);

            var after = HealthStateMachine.Evaluate(before, entry.Status, _options);
            after = after with { LastDescription = entry.Description };

            if (HealthStateMachine.HasTransitioned(before, after))
            {
                _logger.LogInformation(
                    "[HealthMonitor] {Service}: {Before} → {After}",
                    name, before.CurrentStatus, after.CurrentStatus);

                after = after with { LastNotifiedAt = DateTime.UtcNow };

                await _publisher.Publish(new HealthStatusChangedEvent(
                    name, before.CurrentStatus, after.CurrentStatus,
                    entry.Description, tags, DateTime.UtcNow, IsReminder: false), ct)
                    .ConfigureAwait(false);
            }
            else if (HealthStateMachine.NeedsReminder(after, _options))
            {
                _logger.LogInformation("[HealthMonitor] Reminder for {Service} (still {Status})",
                    name, after.CurrentStatus);

                after = after with { LastNotifiedAt = DateTime.UtcNow };

                await _publisher.Publish(new HealthStatusChangedEvent(
                    name, after.CurrentStatus, after.CurrentStatus,
                    entry.Description, tags, DateTime.UtcNow, IsReminder: true), ct)
                    .ConfigureAwait(false);
            }

            _states[name] = after;
            await PersistStateAsync(db, name, after, tags, ct).ConfigureAwait(false);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private static async Task PersistStateAsync(
        MeepleAiDbContext db, string serviceName,
        ServiceHealthState state, string[] tags, CancellationToken ct)
    {
        var entity = await db.ServiceHealthStates
            .FirstOrDefaultAsync(e => e.ServiceName == serviceName, ct)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new ServiceHealthStateEntity { ServiceName = serviceName };
            db.ServiceHealthStates.Add(entity);
        }

        entity.CurrentStatus = state.CurrentStatus;
        entity.PreviousStatus = state.PreviousStatus;
        entity.ConsecutiveFailures = state.ConsecutiveFailures;
        entity.ConsecutiveSuccesses = state.ConsecutiveSuccesses;
        entity.LastTransitionAt = state.LastTransitionAt;
        entity.LastNotifiedAt = state.LastNotifiedAt;
        entity.LastDescription = state.LastDescription;
        entity.Tags = JsonSerializer.Serialize(tags);
        entity.UpdatedAt = DateTime.UtcNow;
    }
}
```

- [ ] **Step 2: Register in DI**

Find the service registration file (likely `Program.cs` or an extension method) and add:

```csharp
services.Configure<HealthMonitorOptions>(configuration.GetSection(HealthMonitorOptions.SectionName));
services.AddHostedService<InfrastructureHealthMonitorService>();
```

- [ ] **Step 3: Build and verify**

```bash
dotnet build --no-restore
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/BackgroundServices/InfrastructureHealthMonitorService.cs
git commit -m "feat(health-monitor): add InfrastructureHealthMonitorService polling loop"
```

---

## Chunk 3: Event Handler + Multi-channel Slack (Phase C alerting)

### Task 7: Event Handler Tests (TDD)

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/Administration/EventHandlers/HealthStatusChangedEventHandlerTests.cs`

- [ ] **Step 1: Write handler tests**

Tests for: tag→category mapping, severity mapping, alertType format, reminder alertType.

Key test cases:
- Tags `["core","critical"]` → category `"infrastructure"`, service "postgres" → alertType `"health.postgres"`
- Tags `["ai","non-critical"]` → category `"ai"`
- Tags `["external"]` + service name "oauth" → category `"security"` (override)
- Transition →Unhealthy → severity `"critical"`
- Transition →Degraded → severity `"warning"`
- Transition →Healthy → severity `"info"`
- IsReminder=true → alertType `"health.reminder.postgres"`
- AlertType length never exceeds 100 chars

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Commit failing tests**

```bash
git commit -m "test(health-monitor): add event handler mapping tests (red)"
```

---

### Task 8: Event Handler Implementation (Green)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/HealthStatusChangedEventHandler.cs`
- Modify: `apps/api/src/Api/Services/IAlertingService.cs`
- Modify: `apps/api/src/Api/Services/AlertingService.cs`

- [ ] **Step 1: Add overload to IAlertingService**

```csharp
// Add to IAlertingService.cs:
Task<AlertDto> SendAlertAsync(
    string alertType, string severity, string message,
    string? category,
    IDictionary<string, object>? metadata = null,
    CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implement overload in AlertingService**

The new overload passes `category` to Slack via metadata (`_slack_category`), then delegates to existing `SendAlertAsync`:

```csharp
public Task<AlertDto> SendAlertAsync(
    string alertType, string severity, string message,
    string? category,
    IDictionary<string, object>? metadata = null,
    CancellationToken cancellationToken = default)
{
    metadata ??= new Dictionary<string, object>();
    if (category != null)
    {
        metadata["_slack_category"] = category;
    }
    return SendAlertAsync(alertType, severity, message, metadata, cancellationToken);
}
```

- [ ] **Step 3: Create handler**

```csharp
// apps/api/src/Api/BoundedContexts/Administration/Application/EventHandlers/HealthStatusChangedEventHandler.cs
using Api.BoundedContexts.Administration.Domain.Events;
using Api.Infrastructure.Health.Models;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.EventHandlers;

internal sealed class HealthStatusChangedEventHandler
    : INotificationHandler<HealthStatusChangedEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<HealthStatusChangedEventHandler> _logger;

    public HealthStatusChangedEventHandler(
        IServiceScopeFactory scopeFactory,
        ILogger<HealthStatusChangedEventHandler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Handle(HealthStatusChangedEvent evt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var alertingService = scope.ServiceProvider.GetRequiredService<IAlertingService>();

        var category = MapCategory(evt.ServiceName, evt.Tags);
        var severity = MapSeverity(evt.CurrentStatus);
        var alertType = evt.IsReminder
            ? TruncateAlertType($"health.reminder.{evt.ServiceName}")
            : TruncateAlertType($"health.{evt.ServiceName}");

        var message = evt.IsReminder
            ? $"REMINDER: {evt.ServiceName} is still {evt.CurrentStatus}. {evt.Description}"
            : $"{evt.ServiceName}: {evt.PreviousStatus} → {evt.CurrentStatus}. {evt.Description}";

        var metadata = new Dictionary<string, object>
        {
            ["service"] = evt.ServiceName,
            ["previous_status"] = evt.PreviousStatus,
            ["current_status"] = evt.CurrentStatus,
            ["is_reminder"] = evt.IsReminder
        };

        await alertingService.SendAlertAsync(alertType, severity, message, category, metadata, ct)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "[HealthAlert] {AlertType} ({Severity}) → category={Category}",
            alertType, severity, category);
    }

    internal static string MapCategory(string serviceName, string[] tags)
    {
        if (string.Equals(serviceName, "oauth", StringComparison.OrdinalIgnoreCase))
            return "security";

        if (tags.Contains(HealthCheckTags.Core) || tags.Contains(HealthCheckTags.Critical))
            return "infrastructure";
        if (tags.Contains(HealthCheckTags.Ai))
            return "ai";
        if (tags.Contains(HealthCheckTags.External))
            return "external";
        if (tags.Contains(HealthCheckTags.Monitoring))
            return "monitoring";

        return "general";
    }

    internal static string MapSeverity(string currentStatus) => currentStatus switch
    {
        "Unhealthy" => "critical",
        "Degraded" => "warning",
        _ => "info"
    };

    private static string TruncateAlertType(string alertType) =>
        alertType.Length > 100 ? alertType[..100] : alertType;
}
```

- [ ] **Step 4: Run tests — verify pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(health-monitor): implement HealthStatusChangedEventHandler with category routing"
```

---

### Task 9: Slack Multi-channel Routing

**Files:**
- Modify: `apps/api/src/Api/Services/SlackAlertChannel.cs`
- Modify: `apps/api/src/Api/appsettings.json` (add `ChannelRouting`)
- Create: `apps/api/tests/Api.Tests/Services/SlackAlertChannelRoutingTests.cs`

- [ ] **Step 1: Write routing tests**

Test cases:
- metadata with `_slack_category="infrastructure"` + severity="warning" → uses `ChannelRouting["infrastructure"]`
- severity="critical" (any category) → uses `ChannelRouting["critical"]`
- unknown category → falls back to default WebhookUrl/Channel
- no metadata → uses default
- `ChannelRouting` entry with empty WebhookUrl → falls back to default

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Add ChannelRouting to SlackConfiguration**

Find `SlackConfiguration` class and add:

```csharp
public Dictionary<string, SlackChannelRoute>? ChannelRouting { get; set; }

public class SlackChannelRoute
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Modify SlackAlertChannel.SendAsync**

At the start of `SendAsync`, resolve the webhook URL and channel:

```csharp
var (webhookUrl, channel) = ResolveRoute(severity, metadata);

// Use webhookUrl instead of _config.WebhookUrl
// Use channel instead of _config.Channel
```

```csharp
private (string WebhookUrl, string Channel) ResolveRoute(
    string severity, IDictionary<string, object>? metadata)
{
    var category = metadata?.GetValueOrDefault("_slack_category")?.ToString();

    // Critical → always use critical channel
    if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase)
        && TryGetRoute("critical", out var criticalRoute))
        return criticalRoute;

    // Category-based routing
    if (category != null && TryGetRoute(category, out var categoryRoute))
        return categoryRoute;

    // Default
    return (_config.WebhookUrl, _config.Channel);
}

private bool TryGetRoute(string key, out (string WebhookUrl, string Channel) route)
{
    route = default;
    if (_config.ChannelRouting?.TryGetValue(key, out var entry) == true
        && !string.IsNullOrEmpty(entry.WebhookUrl))
    {
        route = (entry.WebhookUrl, entry.Channel);
        return true;
    }
    return false;
}
```

- [ ] **Step 5: Add ChannelRouting to appsettings.json**

```json
"Slack": {
  "Enabled": false,
  "WebhookUrl": "",
  "Channel": "#alerts",
  "ChannelRouting": {
    "critical": { "WebhookUrl": "", "Channel": "#alerts-critical" },
    "infrastructure": { "WebhookUrl": "", "Channel": "#alerts-infra" },
    "ai": { "WebhookUrl": "", "Channel": "#alerts-ai" },
    "security": { "WebhookUrl": "", "Channel": "#alerts-security" }
  }
}
```

- [ ] **Step 6: Run tests — verify pass**

- [ ] **Step 7: Commit**

```bash
git commit -m "feat(health-monitor): multi-channel Slack routing by severity and category"
```

---

## Chunk 4: Status Page (Phase A)

### Task 10: Status Page Renderer Tests (TDD)

**Files:**
- Create: `apps/api/tests/Api.Tests/Services/StatusPageRendererTests.cs`

- [ ] **Step 1: Write renderer tests**

Test cases:
- Renders valid HTML with all healthy services → green indicators
- Renders degraded service → yellow indicator
- Renders unhealthy service → red indicator
- Overall status reflects worst service status
- Contains auto-refresh meta tag
- Groups services by tag category

- [ ] **Step 2: Run tests — verify fail**

- [ ] **Step 3: Commit failing tests**

---

### Task 11: Status Page Implementation (Green)

**Files:**
- Create: `apps/api/src/Api/Services/StatusPageRenderer.cs`
- Create: `apps/api/src/Api/Routing/StatusPageEndpoints.cs`

- [ ] **Step 1: Create StatusPageRenderer**

Pure function: takes `HealthReport` → returns HTML string. Inline CSS, no external dependencies. Groups by tags (Core, AI, External, Monitoring). Auto-refresh `<meta>` tag.

- [ ] **Step 2: Create StatusPageEndpoints**

```csharp
// GET /status → HTML (no auth, rate-limited)
// GET /status/details → JSON (API key auth)
```

Register with `app.MapStatusPageEndpoints()`.

- [ ] **Step 3: Run tests — verify pass**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(status-page): add independent /status HTML page"
```

---

## Chunk 5: Frontend Components (Phase B)

### Task 12: AlertHistoryTab

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/monitor/AlertHistoryTab.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` (add tab)

- [ ] **Step 1: Create AlertHistoryTab component**

Table with filters (severity, active/resolved, date range). Uses existing `GET /api/v1/admin/alerts` endpoint.

- [ ] **Step 2: Add "History" tab to monitor page**

Add to `TABS` array and `renderTabContent` switch.

- [ ] **Step 3: Write component test**

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(admin): add alert history tab to monitor hub"
```

---

### Task 13: ServiceHealthBadge

**Files:**
- Create: `apps/web/src/components/admin/ServiceHealthBadge.tsx`

- [ ] **Step 1: Create badge component**

Small dot indicator (green/yellow/red) with unhealthy count. Polls `/api/v1/admin/alerts?activeOnly=true` every 60s. Click navigates to `/admin/monitor/services`.

- [ ] **Step 2: Write component test**

- [ ] **Step 3: Commit**

```bash
git commit -m "feat(admin): add ServiceHealthBadge indicator"
```

---

## Chunk 6: Integration Testing + Final Verification

### Task 14: Integration Tests

- [ ] **Step 1: BackgroundService lifecycle test** — verify startup hydration, poll cycle, state persistence
- [ ] **Step 2: Alert persistence round-trip** — verify alert saved to DB with correct metadata
- [ ] **Step 3: Email dispatch via Mailpit** — send test alert, verify Mailpit receives it

- [ ] **Step 4: Commit**

```bash
git commit -m "test(health-monitor): add integration tests"
```

---

### Task 15: Final Verification + PR

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api/src/Api && dotnet test
```

- [ ] **Step 2: Run frontend tests**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 3: Manual smoke test**

Start API locally, verify:
1. `/status` returns HTML page
2. `/api/v1/alerts/prometheus` webhook works
3. Mailpit receives alert emails
4. Admin dashboard shows alert history

- [ ] **Step 4: Create PR to main-dev**

```bash
git push -u origin feature/issue-448-service-health-monitoring
gh pr create --base main-dev --title "feat(epic-448): service health monitoring & proactive alerting"
```
