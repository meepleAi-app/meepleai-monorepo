# Service Health Monitoring & Proactive Alerting — Design Spec

**Epic**: #448
**Date**: 2026-03-16
**Priority**: C (Proactive Alerting) → A (Status Page) → B (Frontend Admin)
**Parent Branch**: main-dev
**Approach**: BackgroundService + MediatR events (A+B hybrid)

## Problem

When MeepleAI services degrade or go down, the admin has no proactive notification. The existing `/health` endpoint and admin dashboard require manual checking. No alerts fire automatically when PostgreSQL, Redis, embedding service, or other critical services fail.

## Existing Infrastructure (What We Have)

| Component | Status | Notes |
|-----------|--------|-------|
| ASP.NET Health Checks | 20 checks | postgres, redis, qdrant, embedding, reranker, etc. |
| AlertingService | Working | Receives alerts, dispatches to channels, persists to DB |
| AlertEntity + Repository | Working | `alerts` table with metadata, channel tracking |
| Prometheus AlertManager | Configured | Webhook → API, routing by severity/category |
| SlackAlertChannel | Working (single channel) | Needs multi-channel routing extension |
| EmailAlertChannel | Working (disabled) | Needs `Email__EnableSsl=false` for dev Mailpit |
| PagerDutyAlertChannel | Working (disabled) | Available for production |
| Admin Monitor Hub | 7 tabs | alerts, cache, infra, command, testing, export, email |
| Service Dashboard | `/admin/monitor/services` | On-demand health view |

## What's Missing

1. **Proactive polling** — No BackgroundService monitors health automatically
2. **State transitions** — No detection of Healthy→Degraded→Unhealthy changes
3. **Multi-channel Slack** — Single webhook, no routing by severity/category
4. **Independent status page** — No `/status` when Next.js is down
5. **Alert history UI** — No frontend for viewing alert timeline
6. **Notification config UI** — No admin panel for enabling/disabling channels

---

## Phase C: Proactive Alerting (Priority 1)

### C1. BackgroundService — Health Monitor

**File**: `Api/Infrastructure/BackgroundServices/InfrastructureHealthMonitorService.cs`
**Bounded Context**: Cross-cutting infrastructure (matches existing BackgroundServices pattern)

**Constructor dependencies**:
- `IServiceScopeFactory` — for creating per-poll scopes (DB access, scoped services)
- `IPublisher` — for MediatR event publishing
- `IOptions<HealthMonitorOptions>` — for configurable polling interval
- `ILogger<InfrastructureHealthMonitorService>` — logging

**Important**: The service MUST NOT inject scoped services (`MeepleAiDbContext`, `IAlertingService`) directly. All scoped access happens via `IServiceScopeFactory.CreateScope()` per poll iteration, matching the pattern in `StalePdfRecoveryService` and `BggImportQueueBackgroundService`.

**Startup sequence** (in `ExecuteAsync`):
1. Await a 10-second startup delay (let other services initialize)
2. Hydrate in-memory state from PostgreSQL (`service_health_states` table) — **awaited synchronously before first poll**
3. Enter polling loop

**Polling loop** (every configurable interval, default 60s):
1. Create scope via `IServiceScopeFactory`
2. Call `HealthCheckService.CheckHealthAsync()` → 20 check results
3. Compare each check with previous state (in-memory `Dictionary<string, ServiceHealthState>`)
4. Detect transitions using state machine (see below)
5. For each transition, publish `HealthStatusChangedEvent` via `IPublisher`
6. Persist updated state to PostgreSQL
7. Reminder: if service stays Unhealthy >30 min and `LastNotifiedAt` >30 min ago, publish event with alertType `"health.reminder.{serviceName}"` (distinct from transition alertType, bypasses throttle)

**Configuration** (`appsettings.json`):

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

**DI Registration**:

```csharp
services.Configure<HealthMonitorOptions>(configuration.GetSection("HealthMonitor"));
services.AddHostedService<InfrastructureHealthMonitorService>();
```

**State machine per service**:

```
Healthy ──(1 failed check)──→ Degraded
Degraded ──(3 consecutive failures)──→ Unhealthy
Unhealthy ──(2 consecutive OK)──→ Healthy (hysteresis to prevent flapping)
```

Thresholds are configurable via `HealthMonitorOptions`.

**In-memory state**:

```csharp
record ServiceHealthState(
    string ServiceName,
    HealthStatus CurrentStatus,
    HealthStatus PreviousStatus,
    int ConsecutiveFailures,
    int ConsecutiveSuccesses,
    DateTime LastTransitionAt,
    DateTime? LastNotifiedAt,
    string? LastDescription
);
```

**Entity for persistence**: `ServiceHealthStateEntity` in `service_health_states` table.
- **Namespace**: `Api.Infrastructure.Entities.Administration`
- **DbSet**: Add `DbSet<ServiceHealthStateEntity>` to `MeepleAiDbContext`
- **Config**: `ServiceHealthStateEntityConfiguration` in `Infrastructure/EntityConfigurations/Administration/`
- Fields: `Id`, `ServiceName` (unique), `CurrentStatus`, `PreviousStatus`, `ConsecutiveFailures`, `ConsecutiveSuccesses`, `LastTransitionAt`, `LastNotifiedAt`, `LastDescription`, `Tags` (jsonb), `CreatedAt`, `UpdatedAt`

### C2. Events and Handler

**Event** (`Api/BoundedContexts/Administration/Application/Events/HealthStatusChangedEvent.cs`):

```csharp
record HealthStatusChangedEvent(
    string ServiceName,
    string PreviousStatus,   // "Healthy", "Degraded", "Unhealthy"
    string CurrentStatus,
    string? Description,
    string[] Tags,           // from HealthCheckTags: ["core","critical"] or ["ai","non-critical"]
    DateTime TransitionAt,
    bool IsReminder          // true for 30-min reminder re-alerts
) : INotification;
```

**Handler** (`Api/BoundedContexts/Administration/Application/EventHandlers/HealthStatusChangedEventHandler.cs`):

Dependencies: `IServiceScopeFactory` → resolve `IAlertingService` from scope.

1. Map tags to alert category:
   - tags contains `HealthCheckTags.Core` ("core") or `HealthCheckTags.Critical` ("critical") → `"infrastructure"`
   - tags contains `HealthCheckTags.Ai` ("ai") → `"ai"`
   - tags contains `HealthCheckTags.External` ("external") → `"external"`
   - tags contains `HealthCheckTags.Monitoring` ("monitoring") → `"monitoring"`
   - **service name** is "oauth" → `"security"` (override, since OAuth uses "external" tag but belongs to security)
   - fallback → `"general"`

2. Map transition to severity:
   - →Unhealthy = `"critical"`
   - →Degraded = `"warning"`
   - →Healthy (recovery) = `"info"`

3. Build alertType string: `"health.{serviceName}"` (max 50 chars, matching `AlertEntity.AlertType` column constraint of `VARCHAR(50)`)

4. For reminders: alertType = `"health.reminder.{svcName}"` (truncate service name if needed to fit 50 chars). This ensures reminders are NOT throttled by the existing 60-min throttle on the original alertType.

5. Call `IAlertingService.SendAlertAsync(alertType, severity, message, category, metadata)`

**Note on OverallHealthChangedEvent**: Removed from design. Overall status changes are observable from individual service transitions. Adding a separate event would duplicate alerts without clear value. The status page (Phase A) displays aggregate status directly from health check results.

### C3. Multi-channel Slack Routing

**Design decision**: Category-based routing is handled at the `AlertingService` level, NOT in `IAlertChannel`. This avoids breaking the existing interface signature.

**`IAlertChannel` interface**: UNCHANGED. No new parameters.

```csharp
// Stays exactly as-is:
Task<bool> SendAsync(string alertType, string severity, string message,
    IDictionary<string, object>? metadata = null,
    CancellationToken cancellationToken = default);
```

**`IAlertingService` interface change**: Add overload with category:

```csharp
// Existing (unchanged):
Task SendAlertAsync(string alertType, string severity, string message, ...);

// New overload:
Task SendAlertAsync(string alertType, string severity, string message,
    string? category, IDictionary<string, object>? metadata = null,
    CancellationToken ct = default);
```

**`AlertingService` routing logic** (in the new overload):
1. Persist alert to DB (existing logic)
2. For SlackAlertChannel: look up routing config by severity/category to determine webhook URL + channel
3. For other channels (Email, PagerDuty): use existing behavior unchanged
4. Pass resolved webhook URL + channel to Slack via metadata: `metadata["_slack_webhook"] = url`, `metadata["_slack_channel"] = channel`

**`SlackAlertChannel` modification**: Check metadata for `_slack_webhook` and `_slack_channel` overrides before falling back to default config. This keeps the interface unchanged while enabling per-alert routing.

**New config** in `appsettings.json` under `Alerting.Slack`:

```json
"Slack": {
  "Enabled": true,
  "WebhookUrl": "",
  "Channel": "#alerts",
  "ChannelRouting": {
    "critical":       { "WebhookUrl": "", "Channel": "#alerts-critical" },
    "infrastructure": { "WebhookUrl": "", "Channel": "#alerts-infra" },
    "ai":             { "WebhookUrl": "", "Channel": "#alerts-ai" },
    "security":       { "WebhookUrl": "", "Channel": "#alerts-security" }
  }
}
```

**Note**: `WebhookUrl` and `Channel` property names preserved for backward compatibility with existing env var bindings (`Alerting__Slack__WebhookUrl`). `ChannelRouting` is additive — no breaking changes.

**Routing precedence**:
1. severity == "critical" → `ChannelRouting["critical"]`
2. category match in `ChannelRouting` → use that entry
3. Fallback → default `WebhookUrl` + `Channel`

**Slack channels to create** (when webhooks are available):

| Channel | Receives |
|---------|----------|
| `#alerts-critical` | All critical alerts (any service) |
| `#alerts-infra` | Warning/info from postgres, redis, qdrant, config |
| `#alerts-ai` | Warning/info from embedding, reranker, openrouter, smoldocling |
| `#alerts-security` | Warning/info from oauth, 2FA |
| `#alerts` | Default/fallback for everything else |

### C4. Email Alerts

Enable `EmailAlertChannel` with config:

```json
"Email": {
  "Enabled": true,
  "To": "admin@meepleai.app",
  "CriticalOnly": false
}
```

Dev environment: Mailpit catches all emails (already working with `Email__EnableSsl=false` in `api.env.dev`).

### C5. Migration

New table `service_health_states`:

```sql
CREATE TABLE service_health_states (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    service_name VARCHAR(100) NOT NULL UNIQUE,
    current_status VARCHAR(20) NOT NULL,
    previous_status VARCHAR(20) NOT NULL,
    consecutive_failures INT NOT NULL DEFAULT 0,
    consecutive_successes INT NOT NULL DEFAULT 0,
    last_transition_at TIMESTAMPTZ NOT NULL,
    last_notified_at TIMESTAMPTZ,
    last_description TEXT,
    tags JSONB NOT NULL DEFAULT '[]',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_service_health_states_service_name ON service_health_states (service_name);
```

Also: ALTER `alerts.alert_type` from `VARCHAR(50)` to `VARCHAR(100)` to accommodate health alert type strings like `"health.reminder.embedding-service"`.

---

## Phase A: Independent Status Page (Priority 2)

### A1. Public Status Endpoint

**File**: `Api/Routing/StatusPageEndpoints.cs`

- `GET /status` → HTML page (no auth required, rate-limited)
- `GET /status/details` → JSON with response times + error details
  - Auth: `ApiKey` header scheme (existing `ApiKeyAuthenticationMiddleware`)
  - This works from external monitoring tools without cookie auth

### A2. HTML Renderer

**File**: `Api/Services/StatusPageRenderer.cs`

Static HTML with inline CSS (zero external dependencies). Works when Next.js, CDNs, or any frontend is down.

Features:
- Green/yellow/red indicators per service, grouped by tag category
- Response time per check
- Overall status banner
- Auto-refresh every 30s via `<meta http-equiv="refresh" content="30">`
- "Last updated" timestamp
- Link to admin dashboard (when available)

Layout:

```
┌──────────────────────────────────────┐
│  MeepleAI Service Status             │
│  Overall: ● Healthy                  │
│  Last check: 2026-03-16 10:30:00 UTC │
├──────────────────────────────────────┤
│  Core Services                       │
│  ● postgres        Healthy    14ms   │
│  ● redis           Healthy    13ms   │
│  ● qdrant          Healthy    25ms   │
│                                      │
│  AI Services                         │
│  ○ embedding       Degraded   150ms  │
│  ● reranker        Healthy    39ms   │
│  ...                                 │
├──────────────────────────────────────┤
│  Auto-refreshes every 30 seconds     │
└──────────────────────────────────────┘
```

---

## Phase B: Frontend Admin Enhancements (Priority 3)

### B1. Alert History Tab

New tab in `/admin/monitor` — "History".

- Table with all alerts (active + resolved)
- Columns: Service, Severity, Message, Triggered At, Resolved At, Channels notified
- Filters: severity, status (active/resolved), date range
- Uses `GET /api/v1/admin/alerts` (existing endpoint)

### B2. Notification Config Panel

In `/admin/monitor?tab=alerts` — expandable config section.

- Toggle Slack/Email/PagerDuty on/off
- Webhook URL inputs per Slack channel
- Test button per channel ("Send test alert")
- New endpoint: `PUT /api/v1/admin/alerts/config`

### B3. Service Health Badge

Small indicator in admin header.

- Green/yellow/red dot with count of unhealthy services
- Click → navigates to `/admin/monitor/services`
- Polls every configurable interval (aligned with BackgroundService, default 60s)
- Component: `<ServiceHealthBadge />`

---

## Files Summary

| File | Action | Phase | Notes |
|------|--------|-------|-------|
| `HealthMonitorOptions.cs` | New | C | Options class for configurable intervals/thresholds |
| `InfrastructureHealthMonitorService.cs` | New | C | BackgroundService with IServiceScopeFactory |
| `HealthStatusChangedEvent.cs` | New | C | MediatR INotification |
| `HealthStatusChangedEventHandler.cs` | New | C | Maps tags→category, dispatches to AlertingService |
| `ServiceHealthStateEntity.cs` | New | C | In `Infrastructure/Entities/Administration/` |
| `ServiceHealthStateEntityConfiguration.cs` | New | C | In `Infrastructure/EntityConfigurations/Administration/` |
| `MeepleAiDbContext.cs` | Modify | C | Add `DbSet<ServiceHealthStateEntity>` |
| Migration `AddServiceHealthStates` | New | C | New table + ALTER alerts.alert_type length |
| `SlackAlertChannel.cs` | Modify | C | Read `_slack_webhook`/`_slack_channel` from metadata |
| `SlackConfiguration.cs` | Modify | C | Add `ChannelRouting` dictionary property |
| `IAlertingService.cs` | Modify | C | Add overload with `category` parameter |
| `AlertingService.cs` | Modify | C | Routing logic for Slack channel selection |
| `appsettings.json` | Modify | C | Add `HealthMonitor` section + `Slack.ChannelRouting` |
| `StatusPageEndpoints.cs` | New | A | `/status` (HTML) + `/status/details` (JSON) |
| `StatusPageRenderer.cs` | New | A | Inline HTML/CSS renderer |
| `AlertHistoryTab.tsx` | New | B | React component |
| `NotificationConfigPanel.tsx` | New | B | React component |
| `ServiceHealthBadge.tsx` | New | B | React component |

## Testing Strategy

**Backend** (~45 tests):
- Unit: State machine transitions with hysteresis (12), event handler tag→category mapping (8), Slack routing logic with fallbacks (6), status page renderer output (4), alertType format validation (3)
- Integration: BackgroundService startup hydration + lifecycle (4), alert persistence round-trip (4), email dispatch via Mailpit (2), reminder throttle bypass (2)

**Frontend** (~15 tests):
- Component: AlertHistoryTab rendering + filters (5), NotificationConfigPanel form (4), ServiceHealthBadge states (3)
- Integration: API calls + error handling (3)

## Non-Goals

- Custom alert rule engine (alert rules CRUD exists but is stubbed — not in scope)
- Metric threshold alerting (CPU/memory/disk — Prometheus handles this)
- Alert escalation / on-call rotation
- PagerDuty integration (channel exists, config only)
- `OverallHealthChangedEvent` (individual service transitions are sufficient)

## Review Issues Addressed

| # | Issue | Resolution |
|---|-------|------------|
| 1 | IAlertChannel signature break | Keep interface unchanged; route via metadata |
| 2 | IAlertingService not updated | Add overload (not rename), added to files list |
| 3 | Scoped-in-singleton violation | Use IServiceScopeFactory, documented in C1 |
| 4 | AlertType VARCHAR(50) too short | ALTER to VARCHAR(100) in migration |
| 5 | Startup hydration ordering | Hydrate synchronously before first poll |
| 6 | Reminder conflicts with throttle | Separate alertType `health.reminder.*` bypasses throttle |
| 7 | OverallHealthChangedEvent undefined | Removed from design (non-goal) |
| 8 | "security" tag doesn't exist | Route by service name ("oauth") instead of tag |
| 9 | DbSet/EF config missing | Added to files list with exact paths |
| 10 | /status/details auth underspecified | ApiKey header scheme specified |
| 11 | Poll interval hardcoded | Configurable via HealthMonitorOptions |
| 12 | SlackConfiguration rename breaks bindings | Keep existing property names, add ChannelRouting additively |
