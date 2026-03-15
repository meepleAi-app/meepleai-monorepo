# Slack Notification System — Design Spec

**Date**: 2026-03-15
**Status**: Draft
**Bounded Context**: UserNotifications (primary), WorkflowIntegration (team channels)

## Problem Statement

MeepleAI needs a multi-channel notification system that sends Slack notifications to:
1. **End users** via a Slack Bot (DM) — all 41+ notification types
2. **Internal team** via webhooks — operations, business events, DevOps alerts

The current system only supports in-app notifications and email. The existing `SlackAlertChannel` handles admin alerts but is not designed for user-facing or multi-channel dispatch.

## Requirements

### Functional
- Users connect their Slack workspace via "Add to Slack" (Bot OAuth)
- Bot sends DM notifications for all 41+ notification types
- Users can mute specific notification types from Slack
- Interactive buttons in Slack messages (approve/reject share requests, RSVP game nights)
- Internal team channels receive operations, business, and DevOps events
- Team channel config: hybrid (base in `appsettings.json`, admin panel override)
- Block Kit formatting with images, sections, and action buttons

### Non-Functional
- Async queue-based delivery with retry (1m, 5m, 30m) and dead-letter after 3 attempts
- Slack API rate limiting: token-bucket with HTTP 429 + `Retry-After` handling
- Bot access tokens encrypted at rest with `IDataProtector` (purpose: `"MeepleAI.SlackBotToken"`)
- Action buttons expire after 24 hours
- Graceful degradation: Slack failures never block email or in-app
- Slack-side token revocation detected and handled (auto-deactivate connection)

## Architecture

### Approach: Notification Channel Abstraction

Extend the existing `IAlertChannel` strategy pattern into a generic `INotificationChannel` system. A single `NotificationDispatcher` resolves enabled channels per notification and enqueues delivery.

**`SlackAlertChannel` deprecation**: The existing `SlackAlertChannel` (which uses the deprecated Slack `attachments` API) will be deprecated and replaced by `SlackTeamNotificationChannel`. Its alert types (`CircuitBreakerTripped`, `RateLimitExceeded`, etc.) will be migrated to the new team channel system. During migration, both coexist — deduplication is handled by removing alert types from `AlertingService` as they are added to `SlackTeamNotificationChannel`.

### Core Abstraction

```
INotificationChannel
├── EmailNotificationChannel     (refactor existing IEmailService)
├── SlackUserNotificationChannel (new — Bot DM to users)
└── SlackTeamNotificationChannel (new — webhook to team channels, replaces SlackAlertChannel)
```

**Note**: In-app notifications remain synchronous (written directly in the event handler via `_notificationRepo.AddAsync()`). They are NOT routed through the async queue — users expect in-app notifications to appear instantly. The `NotificationDispatcher` creates the in-app notification synchronously, then enqueues async channels (Email, Slack).

### INotificationDispatcher Contract

```csharp
public interface INotificationDispatcher
{
    /// <summary>
    /// Dispatches a notification to all enabled channels for the recipient.
    /// In-app notification is created synchronously within the caller's DB transaction.
    /// Async channels (Email, Slack) are enqueued as NotificationQueueItems.
    /// Returns silently if no channels are enabled (no exception).
    /// Throws NotificationDispatchException only on infrastructure failure (DB write fails).
    /// </summary>
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}

public record NotificationMessage
{
    public required NotificationType Type { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required INotificationPayload Payload { get; init; }
    public string? DeepLinkPath { get; init; } // e.g., "/share-requests/abc-123"
}

/// <summary>
/// Marker interface for notification payloads. Each notification type has a concrete payload.
/// All implementations must be serializable to JSON (System.Text.Json).
/// </summary>
public interface INotificationPayload { }

// Concrete payload examples:
public record ShareRequestPayload(Guid ShareRequestId, string RequesterName, string GameTitle, string? GameImageUrl) : INotificationPayload;
public record GameNightPayload(Guid GameNightId, string Title, DateTime ScheduledAt, string OrganizerName) : INotificationPayload;
public record PdfProcessingPayload(Guid PdfId, string FileName, string Status) : INotificationPayload;
public record BadgePayload(Guid BadgeId, string BadgeName, string Description) : INotificationPayload;
public record GenericPayload(string Title, string Body) : INotificationPayload;
```

**Serialization**: `INotificationPayload` is stored in the `Payload` jsonb column using `System.Text.Json` polymorphic serialization with a `$type` discriminator. The `NotificationQueueItem` EF configuration uses a `ValueConverter` that serializes/deserializes with `JsonSerializerOptions` containing a `JsonPolymorphismOptions` type discriminator registry.

### Dispatch Flow

```
Domain Event (e.g., ShareRequestCreated)
  → MediatR Event Handler (single handler per event)
    → NotificationDispatcher.DispatchAsync(NotificationMessage)
      → 1. Create in-app Notification synchronously (same DB transaction)
      → 2. Resolve async channels:
        - User preferences (check SlackConnection.IsActive + muted types)
        - Team channel config (appsettings + admin overrides)
      → 3. For each async channel:
        → Create NotificationQueueItem (unified table, ChannelType column)
        → All queue items written in same DB transaction as in-app notification
      → 4. Background job (Quartz.NET) processes queue per channel
        → Retry with backoff (1m, 5m, 30m) + dead letter
```

### Event Handler Refactoring

**Before** (email-specific handler):
```csharp
public async Task Handle(ShareRequestCreatedEvent e, CancellationToken ct)
{
    await _notificationRepo.AddAsync(notification);  // in-app
    await _emailService.SendShareRequestEmail(...);   // email direct
}
```

**After** (generic multi-channel handler):
```csharp
public async Task Handle(ShareRequestCreatedEvent e, CancellationToken ct)
{
    await _dispatcher.DispatchAsync(new NotificationMessage
    {
        Type = NotificationType.ShareRequestCreated,
        RecipientUserId = e.TargetUserId,
        Payload = new ShareRequestPayload(e.ShareRequestId, e.RequesterName, e.GameTitle, e.GameImageUrl),
        DeepLinkPath = $"/share-requests/{e.ShareRequestId}"
    }, ct);
}
```

## Slack Bot & User Connection

### Slack App Configuration

Single Slack App "MeepleAI" with:
- **Bot Token Scopes**: `chat:write`, `im:write`
- **Interactivity URL**: `POST /api/v1/integrations/slack/interactions`
- **OAuth Redirect**: `GET /api/v1/integrations/slack/callback`

**Note**: `users:read.email` scope is NOT requested. User identity linking uses `authed_user.id` from the OAuth token exchange response, which is sufficient. If email-based fallback linking is needed in the future, `users.info` on the specific `authed_user.id` can retrieve the email without the broad `users:read.email` scope.

### Connection Flow

1. User clicks "Connect Slack" in MeepleAI settings
2. Redirect to Slack OAuth consent screen
3. Slack returns authorization code to callback endpoint
4. Backend exchanges code for bot access token + `authed_user.id`
5. Save `SlackConnection` entity (token encrypted via `IDataProtector`)
6. Bot opens DM conversation (`conversations.open`) → save `DmChannelId`

### New Entities

**`SlackConnection`** (DDD aggregate in `UserNotifications/Domain`):

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| UserId | Guid | FK to User |
| SlackUserId | string | e.g., "U01ABCDEF" |
| SlackTeamId | string | e.g., "T01ABCDEF" |
| SlackTeamName | string | Workspace name |
| BotAccessToken | string | Encrypted at rest (see Encryption section) |
| DmChannelId | string | Pre-opened DM channel for fast sends |
| IsActive | bool | Can be disconnected without deleting |
| ConnectedAt | DateTime | |
| DisconnectedAt | DateTime? | |

### Encryption Specification

**Bot access tokens** and **webhook URLs** use the same encryption mechanism:

- **Layer**: Infrastructure (EF `ValueConverter` on the entity configuration)
- **Mechanism**: `IDataProtector` with purpose string `"MeepleAI.SlackSecrets"`
- **NOT** `ITimeLimitedDataProtector` — tokens must survive app restarts without expiry
- **Key rotation**: Follows ASP.NET Data Protection key ring rotation (automatic)
- **Example ValueConverter**:
```csharp
var protector = dataProtectionProvider.CreateProtector("MeepleAI.SlackSecrets");
builder.Property(x => x.BotAccessToken)
    .HasConversion(
        v => protector.Protect(v),
        v => protector.Unprotect(v));
```

### NotificationPreferences Extension

To maintain consistency with the existing per-type boolean pattern used for Email and Push channels, Slack preferences follow the same model:

**New columns on `NotificationPreferences`**:

| Field | Type | Description |
|-------|------|-------------|
| SlackEnabled | bool | Master toggle, default true after connection |
| SlackOnShareRequestCreated | bool | Default true |
| SlackOnShareRequestApproved | bool | Default true |
| SlackOnPdfReady | bool | Default true |
| SlackOnBadgeEarned | bool | Default true |
| SlackOnGameNightInvitation | bool | Default true |
| ... (same pattern for each NotificationType) | bool | Default true |

This mirrors the existing `EmailOnDocumentReady`, `PushOnGameNightInvitation` pattern. The `Reconstitute` factory method is extended with the new parameters. Checking "is Slack enabled for type X?" uses the same pattern as email: `SlackEnabled && SlackOnShareRequestCreated`.

**Migration**: A single `ALTER TABLE` migration adds all Slack boolean columns with `DEFAULT true`.

### Team Channel Config (Hybrid)

**Base config** (`appsettings.json` / secrets):
```json
{
  "SlackTeamChannels": {
    "Operations": {
      "WebhookUrl": "from-secret",
      "Channel": "#alerts",
      "Types": ["CircuitBreakerTripped", "RateLimitExceeded", "HealthCheckFailed"]
    },
    "Business": {
      "WebhookUrl": "from-secret",
      "Channel": "#business",
      "Types": ["NewUserRegistered", "TierUpgraded", "PdfUploaded"]
    },
    "DevOps": {
      "WebhookUrl": "from-secret",
      "Channel": "#deploys",
      "Types": ["DeployCompleted", "MigrationRun", "PipelineFailed"]
    }
  }
}
```

**Admin override** — `SlackTeamChannelConfig` table:

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | |
| ChannelName | string | e.g., "#alerts" |
| WebhookUrl | string | Encrypted via EF ValueConverter (same `"MeepleAI.SlackSecrets"` purpose) |
| NotificationTypes | jsonb | Array of NotificationType |
| IsEnabled | bool | |
| OverridesDefault | bool | If true, replaces appsettings entry |

## Block Kit Messages & Interactive Actions

### Template System

```
ISlackMessageBuilder
├── ShareRequestSlackBuilder    → Approve/Reject buttons
├── GameNightSlackBuilder       → RSVP Yes/No/Maybe + game cover image
├── PdfProcessingSlackBuilder   → Direct link to document
├── BadgeSlackBuilder           → Emoji + achievement card
├── AdminAlertSlackBuilder      → Severity color + technical details
└── GenericSlackBuilder         → Fallback for types without specific builder
```

Each builder receives the typed `INotificationPayload` and produces a Slack Block Kit JSON structure.

### Example: Share Request Message

```json
{
  "blocks": [
    {
      "type": "header",
      "text": { "type": "plain_text", "text": "📥 Nuova Share Request" }
    },
    {
      "type": "section",
      "text": {
        "type": "mrkdwn",
        "text": "*Mario Rossi* vuole condividere il regolamento di *Catan* con te."
      },
      "accessory": {
        "type": "image",
        "image_url": "https://meepleai.app/games/catan/cover.jpg",
        "alt_text": "Catan"
      }
    },
    {
      "type": "actions",
      "block_id": "sr:abc-123:1710504000",
      "elements": [
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "✅ Approva" },
          "style": "primary",
          "action_id": "share_request_approve",
          "value": "abc-123"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "❌ Rifiuta" },
          "style": "danger",
          "action_id": "share_request_reject",
          "value": "abc-123"
        },
        {
          "type": "button",
          "text": { "type": "plain_text", "text": "🔗 Apri in MeepleAI" },
          "action_id": "open_meepleai",
          "url": "https://meepleai.app/share-requests/abc-123"
        }
      ]
    }
  ]
}
```

### block_id Format & Expiry

**Format**: `{action_prefix}:{resource_guid}:{unix_timestamp_seconds}`

- `action_prefix`: short action type code, **lowercase alphanumeric only** (e.g., `sr` for share request, `gn` for game night). New prefixes must follow this constraint.
- `resource_guid`: the resource GUID (standard format with hyphens)
- `unix_timestamp_seconds`: creation timestamp for expiry validation
- Separator is `:` — safe because GUIDs use `-`, timestamps are numeric, and prefixes are alphanumeric-only

**Parsing**:
1. `block_id.Split(':')` → must produce exactly 3 segments, otherwise reject
2. `long.TryParse(segments[2], out var ts)` → if parsing fails (non-numeric/tampered), respond 200 with `"⚠️ Invalid action. Please visit MeepleAI."` (never throw — Slack expects 200)
3. `DateTimeOffset.FromUnixTimeSeconds(ts)` → if older than 24h, respond 200 with expiry message

### Team Channel Message Formats

Operations:
```
🚨 [CRITICAL] Circuit Breaker Tripped
Service: EmbeddingService | Failures: 15/10 | State: Open
Since: 2026-03-15 14:22 UTC
→ Dashboard: https://meepleai.app/admin/monitoring
```

Business:
```
👤 [NEW USER] mario.rossi@email.com
Tier: Free | Source: Google OAuth | Region: EU
Total users: 1,247
```

DevOps:
```
🚀 [DEPLOY] v2.4.1 deployed successfully
Duration: 3m 22s | Migrations: 1 | Tests: ✅ 13,134 passed
```

### Interactivity Endpoint

```
POST /api/v1/integrations/slack/interactions
```

**Two-phase handling** (to respect Slack's 3-second timeout):

1. User clicks button in Slack
2. Slack POSTs signed payload to endpoint
3. Validate HMAC-SHA256 signature (Slack signing secret)
4. Parse `block_id` → validate expiry (24h)
   - If expired: respond 200 with message `"⏰ This action has expired. Please visit MeepleAI to take action."` (replace original message)
   - If valid: continue
5. Respond 200 immediately with a temporary message: `"⏳ Processing..."` (replaces buttons)
6. Enqueue async processing: extract `action_id` + `value` → map to MediatR command
7. Dispatch command (same authorization as REST endpoint)
8. On completion: call Slack `response_url` (valid 30 minutes, up to 5 deferred responses) with final confirmation message (e.g., `"✅ Share request approved by you"`)
9. On failure: call `response_url` with error message (e.g., `"❌ Could not process action. Please try in MeepleAI."`)

**Action mapping**:
```csharp
private static readonly Dictionary<string, Func<string, IRequest>> ActionMap = new()
{
    ["share_request_approve"] = val => new ApproveShareRequestCommand(Guid.Parse(val)),
    ["share_request_reject"]  = val => new RejectShareRequestCommand(Guid.Parse(val)),
    ["game_night_rsvp_yes"]   = val => new RsvpGameNightCommand(Guid.Parse(val), RsvpStatus.Yes),
    ["game_night_rsvp_no"]    = val => new RsvpGameNightCommand(Guid.Parse(val), RsvpStatus.No),
    ["game_night_rsvp_maybe"] = val => new RsvpGameNightCommand(Guid.Parse(val), RsvpStatus.Maybe),
};
```

### Action Security

- `value` contains only the resource GUID, never sensitive data
- MediatR command executes same authorization checks as REST endpoint
- Slack user verified against `SlackConnection.SlackUserId` to identify MeepleAI user
- Actions expire after 24h (timestamp in `block_id`, validated before processing)
- Expired actions: respond 200 with user-friendly expiry message (NOT HTTP error)
- Idempotent: repeated action → "Already approved" message instead of error

## Unified Queue & Delivery

### NotificationQueueItem (DDD Aggregate)

Replaces and extends existing `EmailQueueItem`. Field naming aligns with existing `EmailQueueItem.RetryCount`:

| Field | Type | Description |
|-------|------|-------------|
| Id | Guid | Primary key |
| ChannelType | enum | Email, SlackUser, SlackTeam |
| RecipientUserId | Guid? | null for team channels |
| NotificationType | NotificationType | |
| Payload | jsonb | Serialized `INotificationPayload` with `$type` discriminator |
| SlackChannelTarget | string? | DM channel ID or webhook URL |
| Status | enum | Pending, Processing, Sent, Failed, DeadLetter |
| RetryCount | int | Current retry count (0-based, aligns with `EmailQueueItem`) |
| MaxRetries | int | Default 3 |
| NextRetryAt | DateTime? | |
| LastError | string? | |
| CreatedAt | DateTime | |
| ProcessedAt | DateTime? | |
| CorrelationId | Guid | Links all channels for same notification |

**Note**: `InApp` is NOT a `ChannelType`. In-app notifications are written synchronously by the dispatcher, not queued.

### Retry Strategy

Consistent with existing `EmailQueueItem.RetryDelays`:

```
RetryCount 0 (first attempt): immediate
RetryCount 1 (second attempt): +1 minute
RetryCount 2 (third attempt): +5 minutes
RetryCount 3 (fourth attempt): +30 minutes
After 4 failures (RetryCount > 3): → DeadLetter status
```

This matches the existing `TimeSpan[] RetryDelays = [1m, 5m, 30m]` array in `EmailQueueItem`.

### Quartz.NET Jobs

| Job | Interval | Purpose |
|-----|----------|---------|
| SlackNotificationProcessorJob | 10 seconds | Process Slack queue (real-time feel) |
| EmailProcessorJob | 30 seconds | Existing, unchanged initially |
| DeadLetterMonitorJob | 1 hour | Extended to query both tables during migration |
| NotificationCleanupJob | 3 AM UTC | Extended to clean both tables during migration |

### Slack Rate Limiting Strategy

The `SlackNotificationProcessorJob` handles Slack's Tier 3 rate limit (token bucket, ~1 msg/sec/workspace):

1. Fetch pending items ordered by `SlackTeamId`, then `CreatedAt`
2. Group by workspace (`SlackTeamId`)
3. For each workspace, send messages sequentially
4. **On HTTP 429**: read `Retry-After` header, set `NextRetryAt = Now + Retry-After` on ALL remaining items for that workspace, stop processing that workspace, move to next
5. **On `invalid_auth` or `token_revoked`**: mark `SlackConnection.IsActive = false` for the user, set item to `DeadLetter` with error `"token_revoked"`, create in-app notification to user: "Your Slack connection needs to be re-authorized"
6. **On other errors**: increment `RetryCount`, apply standard retry strategy
7. Job uses `[DisallowConcurrentExecution]` to prevent parallel instances racing on rate limits

### EmailQueueItem Migration Strategy

1. Create `NotificationQueueItem` as new table
2. New notifications (both email and Slack) use the new table
3. `EmailQueueItem` remains active — `EmailProcessorJob` continues processing existing items
4. `NotificationCleanupJob` updated in the SAME PR to also clean `NotificationQueueItem`
5. **Deprecation gate**: Before removing `EmailQueueItem` table and its job:
   - Verify `GetPendingAsync()` returns 0 rows
   - Verify `GetDeadLetterCountAsync()` returns 0 rows (or all dead-letter items manually resolved)
   - Admin endpoint `GET /api/v1/admin/notifications/legacy-queue/count` provides this check
6. After gate passes (expected ~30 days): remove `EmailQueueItem` table, `EmailProcessorJob`, and related code in a separate migration PR

## Observability

### Structured Logging (Serilog)

```
[INF] Notification dispatched {CorrelationId, NotificationType, Channels: ["Email","SlackUser"], RecipientUserId}
[INF] Slack message sent {CorrelationId, ChannelType, SlackChannel, Duration: 245ms}
[WRN] Slack delivery failed, retrying {CorrelationId, RetryCount: 2, Error, NextRetryAt}
[WRN] Slack rate limited {SlackTeamId, RetryAfter: 5s, RemainingItems: 3}
[WRN] Slack token revoked, deactivating connection {UserId, SlackUserId, SlackTeamId}
[ERR] Notification dead-lettered {CorrelationId, ChannelType, RetryCount: 4, LastError}
```

### Metrics

| Metric | Labels |
|--------|--------|
| notifications_dispatched_total | type, channel |
| notifications_sent_total | channel, status |
| notifications_failed_total | channel, error_class |
| notification_delivery_latency_ms | channel (p50, p95, p99) |
| slack_connections_active_total | — |
| slack_rate_limited_total | workspace |
| dead_letter_queue_depth | channel |

### Admin Endpoints (CQRS)

| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/v1/admin/notifications/queue | Current queue with filters |
| GET | /api/v1/admin/notifications/dead-letter | Failed messages |
| POST | /api/v1/admin/notifications/dead-letter/{id}/retry | Manual retry |
| GET | /api/v1/admin/notifications/metrics | Aggregated statistics |
| GET | /api/v1/admin/notifications/legacy-queue/count | EmailQueueItem remaining count (migration gate) |
| GET | /api/v1/admin/slack/connections | Connected users |
| GET | /api/v1/admin/slack/team-channels | Team channel config |
| PUT | /api/v1/admin/slack/team-channels/{id} | Admin override |

### Health Checks

```csharp
services.AddHealthChecks()
    .AddCheck<SlackApiHealthCheck>("slack_api")        // verifies Slack API connectivity
    .AddCheck<SlackQueueHealthCheck>("slack_queue");    // alert if queue depth > 100
```

## User-Facing Endpoints (CQRS)

| Method | Path | Purpose |
|--------|------|---------|
| GET | /api/v1/integrations/slack/connect | Redirect to Slack OAuth |
| GET | /api/v1/integrations/slack/callback | OAuth callback |
| DELETE | /api/v1/integrations/slack/disconnect | Revoke token + deactivate |
| GET | /api/v1/integrations/slack/status | Connection status |
| PUT | /api/v1/notifications/preferences/slack | Update Slack preferences |
| POST | /api/v1/integrations/slack/interactions | Slack interactivity webhook |

## Database Migrations

New tables:
1. `SlackConnections` — user Slack connection data (encrypted token column)
2. `NotificationQueueItems` — unified notification queue (replaces EmailQueueItem over time)
3. `SlackTeamChannelConfigs` — admin overrides for team channels (encrypted webhook column)

Alter tables:
1. `NotificationPreferences` — add per-type Slack boolean columns (mirrors Email pattern)

## Security Considerations

- Bot access tokens and webhook URLs encrypted via EF `ValueConverter` + `IDataProtector` (purpose: `"MeepleAI.SlackSecrets"`)
- Slack signing secret validates all incoming interaction requests (HMAC-SHA256)
- Rate limiting: token-bucket with 429/Retry-After handling (not fixed delay)
- Token revocation on user disconnect (MeepleAI-initiated)
- Slack-side revocation detected: `invalid_auth`/`token_revoked` → auto-deactivate + notify user
- No sensitive data in action button values (GUID only)
- 24h expiry on interactive actions (timestamp in `block_id`)
- Expired actions return 200 with user-friendly message (not HTTP error)
- Same authorization model for Slack actions as REST endpoints
- `users:read.email` scope NOT requested (data minimization)

## Dependencies

- **SlackNet** NuGet package (Slack Web API client, actively maintained)
- Slack App created at api.slack.com with Bot scopes (`chat:write`, `im:write`)
- Slack signing secret and client credentials in `infra/secrets/slack.secret`

## Out of Scope

- Slack slash commands (e.g., `/meepleai search Catan`)
- Slack Events API subscriptions (listening to workspace events)
- Multi-language message templates (future i18n)
- Discord/Teams channels (future, same `INotificationChannel` interface)
- Mobile push notifications (future channel)
