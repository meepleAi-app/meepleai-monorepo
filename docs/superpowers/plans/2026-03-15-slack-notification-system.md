# Slack Notification System — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a multi-channel notification system that sends Slack notifications to end users (via Bot DM) and internal team channels (via webhooks), with Block Kit formatting and interactive actions.

**Architecture:** Notification Channel Abstraction pattern — a `NotificationDispatcher` resolves enabled channels per notification, creates in-app notifications synchronously, then enqueues async delivery (Email, Slack) via a unified `NotificationQueueItem` table processed by Quartz.NET jobs.

**Tech Stack:** .NET 9, SlackNet NuGet, EF Core (PostgreSQL), Quartz.NET, MediatR, IDataProtector, System.Text.Json polymorphic serialization

**Spec:** `docs/superpowers/specs/2026-03-15-slack-notification-system-design.md`

---

## Chunk 1: Domain Foundation (Entities, Value Objects, Payloads)

### Task 1: NotificationChannelType Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationChannelType.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationChannelTypeTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class NotificationChannelTypeTests
{
    [Fact]
    public void FromString_WithValidEmail_ReturnsEmailInstance()
    {
        var result = NotificationChannelType.FromString("email");
        Assert.Equal(NotificationChannelType.Email, result);
    }

    [Fact]
    public void FromString_WithValidSlackUser_ReturnsSlackUserInstance()
    {
        var result = NotificationChannelType.FromString("slack_user");
        Assert.Equal(NotificationChannelType.SlackUser, result);
    }

    [Fact]
    public void FromString_WithValidSlackTeam_ReturnsSlackTeamInstance()
    {
        var result = NotificationChannelType.FromString("slack_team");
        Assert.Equal(NotificationChannelType.SlackTeam, result);
    }

    [Fact]
    public void FromString_WithInvalid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => NotificationChannelType.FromString("invalid"));
    }

    [Fact]
    public void FromString_IsCaseInsensitive()
    {
        var result = NotificationChannelType.FromString("SLACK_USER");
        Assert.Equal(NotificationChannelType.SlackUser, result);
    }

    [Fact]
    public void Equality_SameType_AreEqual()
    {
        Assert.Equal(NotificationChannelType.Email, NotificationChannelType.Email);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~NotificationChannelTypeTests" --no-build 2>&1 | head -20`
Expected: Build error — `NotificationChannelType` does not exist

- [ ] **Step 3: Implement NotificationChannelType**

```csharp
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

internal sealed class NotificationChannelType : ValueObject
{
    public string Value { get; }

    public static readonly NotificationChannelType Email = new("email");
    public static readonly NotificationChannelType SlackUser = new("slack_user");
    public static readonly NotificationChannelType SlackTeam = new("slack_team");

    private NotificationChannelType(string value)
    {
        Value = value;
    }

    public static NotificationChannelType FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "email" => Email,
            "slack_user" => SlackUser,
            "slack_team" => SlackTeam,
            _ => throw new ArgumentException($"Unknown notification channel type: {value}", nameof(value))
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~NotificationChannelTypeTests" -v minimal`
Expected: All 6 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationChannelType.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationChannelTypeTests.cs
git commit -m "feat(notifications): add NotificationChannelType value object"
```

---

### Task 2: NotificationQueueStatus Value Object

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationQueueStatus.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationQueueStatusTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class NotificationQueueStatusTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("processing")]
    [InlineData("sent")]
    [InlineData("failed")]
    [InlineData("dead_letter")]
    public void FromString_WithValidStatus_ReturnsInstance(string status)
    {
        var result = NotificationQueueStatus.FromString(status);
        Assert.Equal(status, result.Value);
    }

    [Fact]
    public void FromString_WithInvalid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => NotificationQueueStatus.FromString("invalid"));
    }
}
```

- [ ] **Step 2: Run test — expected FAIL (type missing)**

- [ ] **Step 3: Implement NotificationQueueStatus**

```csharp
using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

internal sealed class NotificationQueueStatus : ValueObject
{
    public string Value { get; }

    public static readonly NotificationQueueStatus Pending = new("pending");
    public static readonly NotificationQueueStatus Processing = new("processing");
    public static readonly NotificationQueueStatus Sent = new("sent");
    public static readonly NotificationQueueStatus Failed = new("failed");
    public static readonly NotificationQueueStatus DeadLetter = new("dead_letter");

    private NotificationQueueStatus(string value) => Value = value;

    public static NotificationQueueStatus FromString(string value)
    {
        var normalized = value.ToLowerInvariant();
        return normalized switch
        {
            "pending" => Pending,
            "processing" => Processing,
            "sent" => Sent,
            "failed" => Failed,
            "dead_letter" => DeadLetter,
            _ => throw new ArgumentException($"Unknown notification queue status: {value}", nameof(value))
        };
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationQueueStatus.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationQueueStatusTests.cs
git commit -m "feat(notifications): add NotificationQueueStatus value object"
```

---

### Task 3: INotificationPayload Interface & Concrete Payloads

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloadSerializationTests.cs`

- [ ] **Step 1: Write failing test for JSON polymorphic serialization**

```csharp
using System.Text.Json;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class NotificationPayloadSerializationTests
{
    private static readonly JsonSerializerOptions Options = NotificationPayloadSerializer.CreateOptions();

    [Fact]
    public void Serialize_ShareRequestPayload_IncludesTypeDiscriminator()
    {
        INotificationPayload payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", "https://img.com/catan.jpg");
        var json = JsonSerializer.Serialize(payload, Options);
        Assert.Contains("\"$type\"", json);
        Assert.Contains("ShareRequestPayload", json);
    }

    [Fact]
    public void RoundTrip_ShareRequestPayload_PreservesAllFields()
    {
        var id = Guid.NewGuid();
        INotificationPayload original = new ShareRequestPayload(id, "Mario", "Catan", "https://img.com/catan.jpg");
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        var result = Assert.IsType<ShareRequestPayload>(deserialized);
        Assert.Equal(id, result.ShareRequestId);
        Assert.Equal("Mario", result.RequesterName);
        Assert.Equal("Catan", result.GameTitle);
    }

    [Fact]
    public void RoundTrip_GameNightPayload_PreservesAllFields()
    {
        var id = Guid.NewGuid();
        var scheduledAt = new DateTime(2026, 4, 1, 19, 0, 0, DateTimeKind.Utc);
        INotificationPayload original = new GameNightPayload(id, "Friday Night", scheduledAt, "Luigi");
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        var result = Assert.IsType<GameNightPayload>(deserialized);
        Assert.Equal(id, result.GameNightId);
        Assert.Equal(scheduledAt, result.ScheduledAt);
    }

    [Fact]
    public void RoundTrip_GenericPayload_PreservesFields()
    {
        INotificationPayload original = new GenericPayload("Title", "Body text");
        var json = JsonSerializer.Serialize(original, Options);
        var deserialized = JsonSerializer.Deserialize<INotificationPayload>(json, Options);

        var result = Assert.IsType<GenericPayload>(deserialized);
        Assert.Equal("Title", result.Title);
        Assert.Equal("Body text", result.Body);
    }
}
```

- [ ] **Step 2: Run test — expected FAIL**

- [ ] **Step 3: Implement payloads and serializer**

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ShareRequestPayload), "ShareRequestPayload")]
[JsonDerivedType(typeof(GameNightPayload), "GameNightPayload")]
[JsonDerivedType(typeof(PdfProcessingPayload), "PdfProcessingPayload")]
[JsonDerivedType(typeof(BadgePayload), "BadgePayload")]
[JsonDerivedType(typeof(GenericPayload), "GenericPayload")]
public interface INotificationPayload;

public record ShareRequestPayload(
    Guid ShareRequestId,
    string RequesterName,
    string GameTitle,
    string? GameImageUrl) : INotificationPayload;

public record GameNightPayload(
    Guid GameNightId,
    string Title,
    DateTime ScheduledAt,
    string OrganizerName) : INotificationPayload;

public record PdfProcessingPayload(
    Guid PdfId,
    string FileName,
    string Status) : INotificationPayload;

public record BadgePayload(
    Guid BadgeId,
    string BadgeName,
    string Description) : INotificationPayload;

public record GenericPayload(
    string Title,
    string Body) : INotificationPayload;

public static class NotificationPayloadSerializer
{
    public static JsonSerializerOptions CreateOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }
}
```

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloadSerializationTests.cs
git commit -m "feat(notifications): add INotificationPayload with polymorphic JSON serialization"
```

---

### Task 4: NotificationQueueItem Aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

[Trait("Category", TestCategories.Unit)]
public class NotificationQueueItemTests
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    [Fact]
    public void Create_WithValidParams_SetsDefaultValues()
    {
        var userId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var payload = new GenericPayload("Test", "Body");

        var item = NotificationQueueItem.Create(
            NotificationChannelType.Email,
            userId,
            NotificationType.ShareRequestCreated,
            payload,
            correlationId: correlationId);

        Assert.Equal(NotificationChannelType.Email, item.ChannelType);
        Assert.Equal(userId, item.RecipientUserId);
        Assert.Equal(NotificationQueueStatus.Pending, item.Status);
        Assert.Equal(0, item.RetryCount);
        Assert.Equal(3, item.MaxRetries);
        Assert.Equal(correlationId, item.CorrelationId);
        Assert.Null(item.SlackChannelTarget);
    }

    [Fact]
    public void Create_ForSlackTeam_AllowsNullRecipient()
    {
        var payload = new GenericPayload("Alert", "Body");

        var item = NotificationQueueItem.Create(
            NotificationChannelType.SlackTeam,
            recipientUserId: null,
            NotificationType.CircuitBreakerTripped,
            payload,
            slackChannelTarget: "https://hooks.slack.com/xxx");

        Assert.Null(item.RecipientUserId);
        Assert.Equal("https://hooks.slack.com/xxx", item.SlackChannelTarget);
    }

    [Fact]
    public void MarkAsProcessing_ChangesStatusToProcessing()
    {
        var item = CreateTestItem();
        item.MarkAsProcessing();
        Assert.Equal(NotificationQueueStatus.Processing, item.Status);
    }

    [Fact]
    public void MarkAsSent_SetsProcessedAtAndStatus()
    {
        var item = CreateTestItem();
        var now = DateTime.UtcNow;
        item.MarkAsProcessing();
        item.MarkAsSent(now);

        Assert.Equal(NotificationQueueStatus.Sent, item.Status);
        Assert.Equal(now, item.ProcessedAt);
    }

    [Fact]
    public void MarkAsFailed_IncrementsRetryCount_CalculatesNextRetry()
    {
        var item = CreateTestItem();
        item.MarkAsProcessing();
        item.MarkAsFailed("Connection timeout");

        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        Assert.Equal(1, item.RetryCount);
        Assert.Equal("Connection timeout", item.LastError);
        Assert.NotNull(item.NextRetryAt);
    }

    [Fact]
    public void MarkAsFailed_AfterMaxRetries_SetsDeadLetter()
    {
        var item = CreateTestItem(); // MaxRetries = 3

        // Fail 3 times (RetryCount 0→1→2→3, dead-letter at >= MaxRetries)
        for (int i = 0; i < 3; i++)
        {
            item.MarkAsProcessing();
            item.MarkAsFailed($"Error {i + 1}");
        }

        Assert.Equal(NotificationQueueStatus.DeadLetter, item.Status);
        Assert.Equal(3, item.RetryCount);
    }

    [Fact]
    public void MarkAsFailed_RetryDelays_MatchExistingEmailQueueItemPattern()
    {
        var item = CreateTestItem();
        var baseTime = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);

        // First failure (RetryCount=1): +1 minute
        item.MarkAsProcessing();
        item.MarkAsFailed("err", baseTime);
        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        Assert.Equal(baseTime.Add(RetryDelays[0]), item.NextRetryAt);

        // Second failure (RetryCount=2): +5 minutes
        item.MarkAsProcessing();
        item.MarkAsFailed("err", baseTime);
        Assert.Equal(NotificationQueueStatus.Failed, item.Status);
        Assert.Equal(baseTime.Add(RetryDelays[1]), item.NextRetryAt);

        // Third failure (RetryCount=3 >= MaxRetries=3): dead letter
        item.MarkAsProcessing();
        item.MarkAsFailed("err", baseTime);
        Assert.Equal(NotificationQueueStatus.DeadLetter, item.Status);
        Assert.Null(item.NextRetryAt);
    }

    private static NotificationQueueItem CreateTestItem()
    {
        return NotificationQueueItem.Create(
            NotificationChannelType.Email,
            Guid.NewGuid(),
            NotificationType.ShareRequestCreated,
            new GenericPayload("Test", "Body"));
    }
}
```

- [ ] **Step 2: Run test — expected FAIL**

- [ ] **Step 3: Implement NotificationQueueItem aggregate**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

internal sealed class NotificationQueueItem : AggregateRoot<Guid>
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30)
    ];

    public NotificationChannelType ChannelType { get; private set; }
    public Guid? RecipientUserId { get; private set; }
    public NotificationType NotificationType { get; private set; }
    public INotificationPayload Payload { get; private set; }
    public string? SlackChannelTarget { get; private set; }
    public string? SlackTeamId { get; private set; } // Denormalized for rate-limit grouping
    public NotificationQueueStatus Status { get; private set; }
    public int RetryCount { get; private set; }
    public int MaxRetries { get; private set; }
    public DateTime? NextRetryAt { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid CorrelationId { get; private set; }

#pragma warning disable CS8618
    private NotificationQueueItem() { } // EF
#pragma warning restore CS8618

    private NotificationQueueItem(
        Guid id,
        NotificationChannelType channelType,
        Guid? recipientUserId,
        NotificationType notificationType,
        INotificationPayload payload,
        string? slackChannelTarget,
        string? slackTeamId,
        Guid correlationId) : base(id)
    {
        ChannelType = channelType;
        RecipientUserId = recipientUserId;
        NotificationType = notificationType;
        Payload = payload;
        SlackChannelTarget = slackChannelTarget;
        SlackTeamId = slackTeamId;
        Status = NotificationQueueStatus.Pending;
        RetryCount = 0;
        MaxRetries = 3;
        CreatedAt = DateTime.UtcNow;
        CorrelationId = correlationId;
    }

    public static NotificationQueueItem Create(
        NotificationChannelType channelType,
        Guid? recipientUserId,
        NotificationType notificationType,
        INotificationPayload payload,
        string? slackChannelTarget = null,
        string? slackTeamId = null,
        Guid? correlationId = null)
    {
        return new NotificationQueueItem(
            Guid.NewGuid(),
            channelType,
            recipientUserId,
            notificationType,
            payload,
            slackChannelTarget,
            slackTeamId,
            correlationId ?? Guid.NewGuid());
    }

    public void MarkAsProcessing()
    {
        Status = NotificationQueueStatus.Processing;
    }

    public void MarkAsSent(DateTime processedAt)
    {
        Status = NotificationQueueStatus.Sent;
        ProcessedAt = processedAt;
        NextRetryAt = null;
    }

    /// <summary>
    /// Marks as failed. Dead-letters when RetryCount >= MaxRetries (matches EmailQueueItem behavior).
    /// </summary>
    public void MarkAsFailed(string errorMessage, DateTime? now = null)
    {
        var currentTime = now ?? DateTime.UtcNow;
        RetryCount++;
        LastError = errorMessage;

        if (RetryCount >= MaxRetries)
        {
            Status = NotificationQueueStatus.DeadLetter;
            NextRetryAt = null;
        }
        else
        {
            Status = NotificationQueueStatus.Failed;
            var delayIndex = Math.Min(RetryCount - 1, RetryDelays.Length - 1);
            NextRetryAt = currentTime.Add(RetryDelays[delayIndex]);
        }
    }

    public void SetNextRetryAt(DateTime nextRetry)
    {
        NextRetryAt = nextRetry;
    }

    public void MarkAsDeadLetter(string reason)
    {
        Status = NotificationQueueStatus.DeadLetter;
        LastError = reason;
        NextRetryAt = null;
    }
}
```

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItem.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationQueueItemTests.cs
git commit -m "feat(notifications): add NotificationQueueItem aggregate with retry and dead-letter"
```

---

### Task 5: SlackConnection Aggregate

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/SlackConnection.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/SlackConnectionTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain.Aggregates;

[Trait("Category", TestCategories.Unit)]
public class SlackConnectionTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var userId = Guid.NewGuid();
        var conn = SlackConnection.Create(userId, "U01ABC", "T01ABC", "My Workspace", "xoxb-token-123", "D01CHANNEL");

        Assert.Equal(userId, conn.UserId);
        Assert.Equal("U01ABC", conn.SlackUserId);
        Assert.Equal("T01ABC", conn.SlackTeamId);
        Assert.Equal("My Workspace", conn.SlackTeamName);
        Assert.Equal("xoxb-token-123", conn.BotAccessToken);
        Assert.Equal("D01CHANNEL", conn.DmChannelId);
        Assert.True(conn.IsActive);
        Assert.Null(conn.DisconnectedAt);
    }

    [Fact]
    public void Disconnect_SetsIsActiveFalseAndTimestamp()
    {
        var conn = CreateTestConnection();
        var now = DateTime.UtcNow;

        conn.Disconnect(now);

        Assert.False(conn.IsActive);
        Assert.Equal(now, conn.DisconnectedAt);
    }

    [Fact]
    public void Reconnect_SetsIsActiveTrueAndUpdatesToken()
    {
        var conn = CreateTestConnection();
        conn.Disconnect(DateTime.UtcNow);

        conn.Reconnect("xoxb-new-token", "D02NEWCHANNEL");

        Assert.True(conn.IsActive);
        Assert.Equal("xoxb-new-token", conn.BotAccessToken);
        Assert.Equal("D02NEWCHANNEL", conn.DmChannelId);
        Assert.Null(conn.DisconnectedAt);
    }

    [Fact]
    public void Deactivate_SetsIsActiveFalse_NoTimestamp()
    {
        var conn = CreateTestConnection();
        conn.Deactivate();
        Assert.False(conn.IsActive);
    }

    private static SlackConnection CreateTestConnection()
    {
        return SlackConnection.Create(Guid.NewGuid(), "U01ABC", "T01ABC", "Workspace", "xoxb-token", "D01CH");
    }
}
```

- [ ] **Step 2: Run test — expected FAIL**

- [ ] **Step 3: Implement SlackConnection**

```csharp
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.UserNotifications.Domain.Aggregates;

internal sealed class SlackConnection : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string SlackUserId { get; private set; }
    public string SlackTeamId { get; private set; }
    public string SlackTeamName { get; private set; }
    public string BotAccessToken { get; private set; }
    public string DmChannelId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime ConnectedAt { get; private set; }
    public DateTime? DisconnectedAt { get; private set; }

#pragma warning disable CS8618
    private SlackConnection() { } // EF
#pragma warning restore CS8618

    private SlackConnection(Guid id, Guid userId, string slackUserId, string slackTeamId,
        string slackTeamName, string botAccessToken, string dmChannelId) : base(id)
    {
        UserId = userId;
        SlackUserId = slackUserId;
        SlackTeamId = slackTeamId;
        SlackTeamName = slackTeamName;
        BotAccessToken = botAccessToken;
        DmChannelId = dmChannelId;
        IsActive = true;
        ConnectedAt = DateTime.UtcNow;
    }

    public static SlackConnection Create(Guid userId, string slackUserId, string slackTeamId,
        string slackTeamName, string botAccessToken, string dmChannelId)
    {
        return new SlackConnection(Guid.NewGuid(), userId, slackUserId, slackTeamId,
            slackTeamName, botAccessToken, dmChannelId);
    }

    public void Disconnect(DateTime disconnectedAt)
    {
        IsActive = false;
        DisconnectedAt = disconnectedAt;
    }

    public void Reconnect(string newBotAccessToken, string newDmChannelId)
    {
        BotAccessToken = newBotAccessToken;
        DmChannelId = newDmChannelId;
        IsActive = true;
        DisconnectedAt = null;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    internal static SlackConnection Reconstitute(Guid id, Guid userId, string slackUserId,
        string slackTeamId, string slackTeamName, string botAccessToken, string dmChannelId,
        bool isActive, DateTime connectedAt, DateTime? disconnectedAt)
    {
        var conn = new SlackConnection();
        // Use reflection or internal setter pattern matching existing aggregates
        typeof(AggregateRoot<Guid>).GetProperty("Id")!.SetValue(conn, id);
        conn.UserId = userId;
        conn.SlackUserId = slackUserId;
        conn.SlackTeamId = slackTeamId;
        conn.SlackTeamName = slackTeamName;
        conn.BotAccessToken = botAccessToken;
        conn.DmChannelId = dmChannelId;
        conn.IsActive = isActive;
        conn.ConnectedAt = connectedAt;
        conn.DisconnectedAt = disconnectedAt;
        return conn;
    }
}
```

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/SlackConnection.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/Aggregates/SlackConnectionTests.cs
git commit -m "feat(notifications): add SlackConnection aggregate with connect/disconnect lifecycle"
```

---

### Task 6: NotificationMessage Record & INotificationDispatcher Interface

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Services/INotificationDispatcher.cs`
- No test (interface-only)

- [ ] **Step 1: Create the interface and message record**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Application.Services;

public record NotificationMessage
{
    public required NotificationType Type { get; init; }
    public required Guid RecipientUserId { get; init; }
    public required INotificationPayload Payload { get; init; }
    public string? DeepLinkPath { get; init; }
}

public interface INotificationDispatcher
{
    Task DispatchAsync(NotificationMessage message, CancellationToken ct = default);
}
```

- [ ] **Step 2: Verify it builds**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/Services/INotificationDispatcher.cs
git commit -m "feat(notifications): add INotificationDispatcher interface and NotificationMessage record"
```

---

### Task 7: Repository Interfaces

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/INotificationQueueRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/ISlackConnectionRepository.cs`

- [ ] **Step 1: Create INotificationQueueRepository**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface INotificationQueueRepository
{
    Task AddAsync(NotificationQueueItem item, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<NotificationQueueItem> items, CancellationToken ct = default);
    Task UpdateAsync(NotificationQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationQueueItem>> GetPendingByChannelAsync(
        NotificationChannelType channelType, int batchSize, CancellationToken ct = default);
    Task<int> GetPendingCountAsync(CancellationToken ct = default);
    Task<int> GetDeadLetterCountAsync(CancellationToken ct = default);
    Task<IReadOnlyList<NotificationQueueItem>> GetDeadLetterItemsAsync(
        int batchSize, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create ISlackConnectionRepository**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;

namespace Api.BoundedContexts.UserNotifications.Domain.Repositories;

internal interface ISlackConnectionRepository
{
    Task AddAsync(SlackConnection connection, CancellationToken ct = default);
    Task UpdateAsync(SlackConnection connection, CancellationToken ct = default);
    Task<SlackConnection?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<SlackConnection?> GetBySlackUserIdAsync(string slackUserId, CancellationToken ct = default);
    Task<SlackConnection?> GetActiveByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> GetActiveConnectionCountAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Verify build**

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/INotificationQueueRepository.cs apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Repositories/ISlackConnectionRepository.cs
git commit -m "feat(notifications): add repository interfaces for NotificationQueue and SlackConnection"
```

---

## Chunk 2: Infrastructure (Entities, Repositories, Migration, DI)

### Task 8: EF Core Entities

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/SlackConnectionEntity.cs`
- Create: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/SlackTeamChannelConfigEntity.cs`

- [ ] **Step 1: Create NotificationQueueEntity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

[Table("notification_queue_items")]
public class NotificationQueueEntity
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] [MaxLength(20)] [Column("channel_type")] public string ChannelType { get; set; } = string.Empty;
    [Column("recipient_user_id")] public Guid? RecipientUserId { get; set; }
    [Required] [MaxLength(50)] [Column("notification_type")] public string NotificationType { get; set; } = string.Empty;
    [Required] [Column("payload", TypeName = "jsonb")] public string Payload { get; set; } = string.Empty;
    [MaxLength(500)] [Column("slack_channel_target")] public string? SlackChannelTarget { get; set; }
    [Required] [MaxLength(20)] [Column("status")] public string Status { get; set; } = "pending";
    [Required] [Column("retry_count")] public int RetryCount { get; set; }
    [Required] [Column("max_retries")] public int MaxRetries { get; set; } = 3;
    [Column("next_retry_at")] public DateTime? NextRetryAt { get; set; }
    [MaxLength(2000)] [Column("last_error")] public string? LastError { get; set; }
    [Required] [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("processed_at")] public DateTime? ProcessedAt { get; set; }
    [Required] [Column("correlation_id")] public Guid CorrelationId { get; set; }
    [MaxLength(20)] [Column("slack_team_id")] public string? SlackTeamId { get; set; }
}
```

- [ ] **Step 2: Create SlackConnectionEntity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

[Table("slack_connections")]
public class SlackConnectionEntity
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] [Column("user_id")] public Guid UserId { get; set; }
    [Required] [MaxLength(20)] [Column("slack_user_id")] public string SlackUserId { get; set; } = string.Empty;
    [Required] [MaxLength(20)] [Column("slack_team_id")] public string SlackTeamId { get; set; } = string.Empty;
    [Required] [MaxLength(100)] [Column("slack_team_name")] public string SlackTeamName { get; set; } = string.Empty;
    [Required] [Column("bot_access_token")] public string BotAccessToken { get; set; } = string.Empty;
    [Required] [MaxLength(20)] [Column("dm_channel_id")] public string DmChannelId { get; set; } = string.Empty;
    [Required] [Column("is_active")] public bool IsActive { get; set; } = true;
    [Required] [Column("connected_at")] public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;
    [Column("disconnected_at")] public DateTime? DisconnectedAt { get; set; }
}
```

- [ ] **Step 3: Create SlackTeamChannelConfigEntity**

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Api.Infrastructure.Entities.UserNotifications;

[Table("slack_team_channel_configs")]
public class SlackTeamChannelConfigEntity
{
    [Key] [Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Required] [MaxLength(100)] [Column("channel_name")] public string ChannelName { get; set; } = string.Empty;
    [Required] [Column("webhook_url")] public string WebhookUrl { get; set; } = string.Empty;
    [Required] [Column("notification_types", TypeName = "jsonb")] public string NotificationTypes { get; set; } = "[]";
    [Required] [Column("is_enabled")] public bool IsEnabled { get; set; } = true;
    [Required] [Column("overrides_default")] public bool OverridesDefault { get; set; }
}
```

- [ ] **Step 4: Verify build**

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationQueueEntity.cs apps/api/src/Api/Infrastructure/Entities/UserNotifications/SlackConnectionEntity.cs apps/api/src/Api/Infrastructure/Entities/UserNotifications/SlackTeamChannelConfigEntity.cs
git commit -m "feat(notifications): add EF entities for NotificationQueue, SlackConnection, SlackTeamChannelConfig"
```

---

### Task 9: EF Core DbContext Configuration & Migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Persistence/MeepleAiDbContext.cs` (add DbSets)
- Create: Migration via `dotnet ef migrations add`

- [ ] **Step 1: Add DbSets to MeepleAiDbContext**

Add to the existing DbContext:
```csharp
public DbSet<NotificationQueueEntity> NotificationQueueItems => Set<NotificationQueueEntity>();
public DbSet<SlackConnectionEntity> SlackConnections => Set<SlackConnectionEntity>();
public DbSet<SlackTeamChannelConfigEntity> SlackTeamChannelConfigs => Set<SlackTeamChannelConfigEntity>();
```

- [ ] **Step 2: Add entity configuration for encrypted columns**

Create `apps/api/src/Api/Infrastructure/Persistence/Configurations/SlackConnectionEntityConfiguration.cs`:

```csharp
using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

internal class SlackConnectionEntityConfiguration : IEntityTypeConfiguration<SlackConnectionEntity>
{
    private readonly IDataProtector _protector;

    public SlackConnectionEntityConfiguration(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("MeepleAI.SlackSecrets");
    }

    public void Configure(EntityTypeBuilder<SlackConnectionEntity> builder)
    {
        builder.HasIndex(e => e.UserId).IsUnique();
        builder.HasIndex(e => e.SlackUserId);
        builder.HasIndex(e => new { e.IsActive, e.UserId });

        builder.Property(e => e.BotAccessToken)
            .HasConversion(
                v => _protector.Protect(v),
                v => _protector.Unprotect(v));
    }
}
```

Create `apps/api/src/Api/Infrastructure/Persistence/Configurations/SlackTeamChannelConfigEntityConfiguration.cs`:

```csharp
using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

internal class SlackTeamChannelConfigEntityConfiguration : IEntityTypeConfiguration<SlackTeamChannelConfigEntity>
{
    private readonly IDataProtector _protector;

    public SlackTeamChannelConfigEntityConfiguration(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("MeepleAI.SlackSecrets");
    }

    public void Configure(EntityTypeBuilder<SlackTeamChannelConfigEntity> builder)
    {
        builder.HasIndex(e => e.ChannelName);

        builder.Property(e => e.WebhookUrl)
            .HasConversion(
                v => _protector.Protect(v),
                v => _protector.Unprotect(v));
    }
}
```

Create `apps/api/src/Api/Infrastructure/Persistence/Configurations/NotificationQueueEntityConfiguration.cs`:

```csharp
using Api.Infrastructure.Entities.UserNotifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.Infrastructure.Persistence.Configurations;

internal class NotificationQueueEntityConfiguration : IEntityTypeConfiguration<NotificationQueueEntity>
{
    public void Configure(EntityTypeBuilder<NotificationQueueEntity> builder)
    {
        var options = NotificationPayloadSerializer.CreateOptions();

        builder.HasIndex(e => new { e.Status, e.ChannelType, e.NextRetryAt })
            .HasDatabaseName("IX_notification_queue_pending");
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.RecipientUserId);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.SlackTeamId);

        // CRITICAL: Payload jsonb serialization must use INotificationPayload as generic type
        // to emit the $type discriminator for polymorphic deserialization.
        builder.Property(e => e.Payload)
            .HasColumnType("jsonb");
        // Note: Payload is stored as string in entity, serialization happens in the repository
        // MapToPersistence/MapToDomain methods using JsonSerializer.Serialize<INotificationPayload>()
    }
}
```

- [ ] **Step 3: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddSlackNotificationSystem`

- [ ] **Step 4: Review migration SQL**

Run: `cd apps/api/src/Api && dotnet ef migrations script --idempotent | head -80`
Expected: CREATE TABLE for `slack_connections`, `notification_queue_items`, `slack_team_channel_configs` with correct columns, indexes, and encrypted column types.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Infrastructure/ apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(notifications): add EF configurations and migration for Slack notification tables"
```

---

### Task 10: Repository Implementations

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueueRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs`

- [ ] **Step 1: Implement NotificationQueueRepository**

Follow the existing `EmailQueueRepository` pattern with `MapToPersistence`/`MapToDomain` methods. Key query: `GetPendingByChannelAsync` filters by `(status == "pending") || (status == "failed" && nextRetryAt <= now)` ordered by `CreatedAt`.

- [ ] **Step 2: Implement SlackConnectionRepository**

Follow existing `NotificationRepository` pattern. Key: `GetActiveByUserIdAsync` filters `IsActive == true`.

- [ ] **Step 3: Verify build**

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueueRepository.cs apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/SlackConnectionRepository.cs
git commit -m "feat(notifications): implement NotificationQueue and SlackConnection repositories"
```

---

### Task 11: DI Registration

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs`

- [ ] **Step 1: Add new registrations**

Add to `AddUserNotificationsContext()`:
```csharp
// New repositories
services.AddScoped<INotificationQueueRepository, NotificationQueueRepository>();
services.AddScoped<ISlackConnectionRepository, SlackConnectionRepository>();
```

- [ ] **Step 2: Verify build**

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs
git commit -m "feat(notifications): register Slack notification repositories in DI"
```

---

### Task 11b: NotificationPreferences Slack Extension

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationPreferences.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationPreferencesEntity.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationPreferencesRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/UpdateSlackPreferencesCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/UpdateSlackPreferencesCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/Handlers/UpdateSlackPreferencesCommandHandlerTests.cs`

- [ ] **Step 1: Add Slack boolean columns to `NotificationPreferencesEntity`**

Follow existing Email/Push pattern — add per-type booleans:
```csharp
[Required] [Column("slack_enabled")] public bool SlackEnabled { get; set; } = true;
[Required] [Column("slack_on_document_ready")] public bool SlackOnDocumentReady { get; set; } = true;
[Required] [Column("slack_on_document_failed")] public bool SlackOnDocumentFailed { get; set; } = true;
[Required] [Column("slack_on_share_request_created")] public bool SlackOnShareRequestCreated { get; set; } = true;
[Required] [Column("slack_on_share_request_approved")] public bool SlackOnShareRequestApproved { get; set; } = true;
[Required] [Column("slack_on_badge_earned")] public bool SlackOnBadgeEarned { get; set; } = true;
[Required] [Column("slack_on_game_night_invitation")] public bool SlackOnGameNightInvitation { get; set; } = true;
[Required] [Column("slack_on_game_night_reminder")] public bool SlackOnGameNightReminder { get; set; } = true;
```

- [ ] **Step 2: Extend `NotificationPreferences` domain aggregate**

Add matching properties + `UpdateSlackPreferences(...)` method + extend `Reconstitute` factory.

- [ ] **Step 3: Update `NotificationPreferencesRepository` mapper**

Add Slack fields to `MapToPersistence`/`MapToDomain`.

- [ ] **Step 4: Create `UpdateSlackPreferencesCommand` + handler**

Follow existing `UpdateNotificationPreferencesCommand` pattern.

- [ ] **Step 5: Write test for handler**

- [ ] **Step 6: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddSlackNotificationPreferences`

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/ apps/api/src/Api/Infrastructure/ apps/api/tests/
git commit -m "feat(notifications): extend NotificationPreferences with Slack per-type toggles"
```

---

### Task 11c: Payload Serialization Round-Trip Test

**Files:**
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueuePayloadRoundTripTests.cs`

- [ ] **Step 1: Write test verifying repository-level Payload round-trip**

```csharp
[Trait("Category", TestCategories.Unit)]
public class NotificationQueuePayloadRoundTripTests
{
    private static readonly JsonSerializerOptions Options = NotificationPayloadSerializer.CreateOptions();

    [Fact]
    public void MapToPersistence_SerializesWithTypeDiscriminator()
    {
        INotificationPayload payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null);
        // Repository serialization MUST use INotificationPayload as generic type param
        var json = JsonSerializer.Serialize<INotificationPayload>(payload, Options);
        Assert.Contains("\"$type\"", json);
    }

    [Fact]
    public void MapToDomain_DeserializesToCorrectConcreteType()
    {
        INotificationPayload payload = new GameNightPayload(Guid.NewGuid(), "Friday", DateTime.UtcNow, "Luigi");
        var json = JsonSerializer.Serialize<INotificationPayload>(payload, Options);
        var result = JsonSerializer.Deserialize<INotificationPayload>(json, Options);
        Assert.IsType<GameNightPayload>(result);
    }

    [Fact]
    public void Serialize_WithoutGenericTypeParam_DoesNotIncludeDiscriminator()
    {
        // This test documents the WRONG way — calling Serialize(payload, Options)
        // without the <INotificationPayload> type param omits $type
        var payload = new ShareRequestPayload(Guid.NewGuid(), "Mario", "Catan", null);
        var json = JsonSerializer.Serialize(payload, Options); // infers concrete type
        Assert.DoesNotContain("\"$type\"", json); // Proves the generic param is required
    }
}
```

- [ ] **Step 2: Run tests — expected PASS**

- [ ] **Step 3: Commit**

```bash
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationQueuePayloadRoundTripTests.cs
git commit -m "test(notifications): add payload serialization round-trip tests documenting generic type requirement"
```

---

## Chunk 3: NotificationDispatcher & SlackNet Integration

### Task 12: Install SlackNet NuGet Package

- [ ] **Step 1: Add SlackNet package**

Run: `cd apps/api/src/Api && dotnet add package SlackNet`

- [ ] **Step 2: Verify build**

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Api.csproj
git commit -m "chore(api): add SlackNet NuGet package for Slack API integration"
```

---

### Task 13: Slack Configuration Options

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Configuration/SlackNotificationConfiguration.cs`

- [ ] **Step 1: Create config classes**

```csharp
namespace Api.BoundedContexts.UserNotifications.Infrastructure.Configuration;

public class SlackNotificationConfiguration
{
    public const string SectionName = "SlackNotification";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string SigningSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public Dictionary<string, SlackTeamChannelSettings> TeamChannels { get; set; } = new();
}

public class SlackTeamChannelSettings
{
    public string WebhookUrl { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public List<string> Types { get; set; } = [];
}
```

- [ ] **Step 2: Register in DI**

Add to `UserNotificationsServiceExtensions`:
```csharp
services.Configure<SlackNotificationConfiguration>(
    configuration.GetSection(SlackNotificationConfiguration.SectionName));
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Configuration/SlackNotificationConfiguration.cs
git commit -m "feat(notifications): add Slack notification configuration options"
```

---

### Task 14: NotificationDispatcher Implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcherTests.cs`

- [ ] **Step 1: Write failing test**

Test that `DispatchAsync` creates an in-app notification synchronously + enqueues `NotificationQueueItem` for Email channel. Mock all repositories.

- [ ] **Step 2: Run test — expected FAIL**

- [ ] **Step 3: Implement NotificationDispatcher**

Key logic:
1. Create in-app `Notification` via `_notificationRepository.AddAsync()`
2. Check `NotificationPreferences` for enabled channels
3. If email enabled → create `NotificationQueueItem(Email, ...)`
4. If Slack enabled + `SlackConnection.IsActive` → create `NotificationQueueItem(SlackUser, ...)`
5. Check team channel config → create `NotificationQueueItem(SlackTeam, ...)` for matching types
6. `_notificationQueueRepository.AddRangeAsync(items)`

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Write additional test for Slack channel resolution**

- [ ] **Step 6: Run all tests — expected PASS**

- [ ] **Step 7: Register in DI**: `services.AddScoped<INotificationDispatcher, NotificationDispatcher>();`

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcherTests.cs apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/DependencyInjection/UserNotificationsServiceExtensions.cs
git commit -m "feat(notifications): implement NotificationDispatcher with multi-channel resolution"
```

---

### Task 15: SlackNotificationProcessorJob

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs`

- [ ] **Step 1: Write failing test**

Test that job fetches pending Slack items, calls Slack API, marks as Sent. Test 429 handling sets NextRetryAt. Test `invalid_auth` deactivates connection.

- [ ] **Step 2: Run test — expected FAIL**

- [ ] **Step 3: Implement the job**

Follow `EmailProcessorJob` pattern. Key differences:
- Fetch `NotificationChannelType.SlackUser` and `SlackTeam` items
- Group by `SlackTeamId` (extracted from `SlackConnection`)
- Handle HTTP 429 with `Retry-After`
- Handle `invalid_auth` / `token_revoked` → deactivate `SlackConnection`
- Uses `[DisallowConcurrentExecution]`

- [ ] **Step 4: Run test — expected PASS**

- [ ] **Step 5: Register Quartz job in DI** (10-second interval, cron: `0/10 * * * * ?`)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJob.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Scheduling/SlackNotificationProcessorJobTests.cs
git commit -m "feat(notifications): add SlackNotificationProcessorJob with rate-limit and token-revocation handling"
```

---

## Chunk 4: Block Kit Message Builders

### Task 16: ISlackMessageBuilder Interface & GenericSlackBuilder

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ISlackMessageBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GenericSlackBuilder.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/GenericSlackBuilderTests.cs`

- [ ] **Step 1: Write failing test** — GenericSlackBuilder produces valid Block Kit JSON with header + section
- [ ] **Step 2: Implement interface and GenericSlackBuilder**
- [ ] **Step 3: Run test — expected PASS**
- [ ] **Step 4: Commit**

---

### Task 17: ShareRequestSlackBuilder (with interactive actions)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilder.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/ShareRequestSlackBuilderTests.cs`

- [ ] **Step 1: Write failing test** — verify Block Kit includes header, section with image accessory, actions block with approve/reject/open buttons, correct block_id format `sr:{guid}:{timestamp}`
- [ ] **Step 2: Implement builder**
- [ ] **Step 3: Run test — expected PASS**
- [ ] **Step 4: Commit**

---

### Task 18: GameNightSlackBuilder & remaining builders

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/GameNightSlackBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/BadgeSlackBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackMessageBuilderFactory.cs`
- Tests for each builder

- [ ] **Step 1: Write tests for GameNight (RSVP buttons), PdfProcessing (link), Badge (card), AdminAlert (severity colors)**
- [ ] **Step 2: Implement all builders**
- [ ] **Step 3: Implement `SlackMessageBuilderFactory` that resolves the correct builder by `NotificationType`**
- [ ] **Step 4: Run all tests — expected PASS**
- [ ] **Step 5: Register factory in DI**
- [ ] **Step 6: Commit**

---

## Chunk 5: Slack OAuth & User Endpoints

### Task 19: Slack OAuth Connect/Callback Commands

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/ConnectSlackCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/ConnectSlackCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/SlackOAuthCallbackCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/SlackOAuthCallbackCommandHandler.cs`
- Tests for both handlers

- [ ] **Step 1: Write failing test** — `ConnectSlackCommandHandler` returns OAuth redirect URL
- [ ] **Step 2: Implement** — builds Slack OAuth URL with client_id, redirect_uri, scopes (`chat:write,im:write`)
- [ ] **Step 3: Write failing test** — `SlackOAuthCallbackCommandHandler` exchanges code, creates `SlackConnection`, opens DM
- [ ] **Step 4: Implement** — calls `oauth.v2.access`, extracts `authed_user.id` + `access_token`, `conversations.open`, saves connection
- [ ] **Step 5: Run tests — expected PASS**
- [ ] **Step 6: Commit**

---

### Task 20: Disconnect & Status Commands

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/DisconnectSlackCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/DisconnectSlackCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetSlackConnectionStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/GetSlackConnectionStatusQueryHandler.cs`
- Tests

- [ ] **Step 1-4: Write tests, implement, verify**
- [ ] **Step 5: Commit**

---

### Task 21: Slack Integration Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/SlackIntegrationEndpoints.cs`

- [ ] **Step 1: Create routing file**

```csharp
// GET  /api/v1/integrations/slack/connect     → ConnectSlackCommand
// GET  /api/v1/integrations/slack/callback     → SlackOAuthCallbackCommand
// DELETE /api/v1/integrations/slack/disconnect → DisconnectSlackCommand
// GET  /api/v1/integrations/slack/status       → GetSlackConnectionStatusQuery
// PUT  /api/v1/notifications/preferences/slack → UpdateSlackPreferencesCommand
```

All endpoints use `IMediator.Send()` per CQRS pattern.

- [ ] **Step 2: Register in routing**
- [ ] **Step 3: Commit**

---

## Chunk 6: Slack Interactivity Endpoint

### Task 22: Slack Interaction Handler (Two-Phase)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/HandleSlackInteractionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/HandleSlackInteractionCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/SlackSignatureValidator.cs`
- Create: `apps/api/src/Api/Routing/SlackInteractionEndpoints.cs`
- Tests

- [ ] **Step 1: Write test** — signature validation rejects invalid HMAC
- [ ] **Step 2: Implement `SlackSignatureValidator`** — HMAC-SHA256 with signing secret
- [ ] **Step 3: Write test** — expired block_id (>24h) returns expiry message
- [ ] **Step 4: Write test** — valid action dispatches correct MediatR command via `ActionMap`
- [ ] **Step 5: Implement handler** — two-phase: immediate 200 "Processing...", then async dispatch via `response_url`
- [ ] **Step 6: Write test** — malformed block_id returns "Invalid action" message
- [ ] **Step 7: Create endpoint** — `POST /api/v1/integrations/slack/interactions`
- [ ] **Step 8: Run all tests — expected PASS**
- [ ] **Step 9: Commit**

---

## Chunk 7: Admin Endpoints & Observability

### Task 23: Admin Notification Queue Endpoints

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetNotificationQueueQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetDeadLetterQueueQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/RetryDeadLetterCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetLegacyQueueCountQuery.cs`
- Create handlers for each
- Create: `apps/api/src/Api/Routing/AdminNotificationQueueEndpoints.cs`
- Tests

- [ ] **Step 1-4: Implement queries/commands, handlers, tests**
- [ ] **Step 5: Create admin routing** (all under `/api/v1/admin/notifications/`)
- [ ] **Step 6: Commit**

---

### Task 24: Admin Slack Endpoints

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetSlackConnectionsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Queries/GetSlackTeamChannelsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/UpdateSlackTeamChannelCommand.cs`
- Create handlers + tests
- Create: `apps/api/src/Api/Routing/AdminSlackEndpoints.cs`

- [ ] **Step 1-4: Implement, test, route**
- [ ] **Step 5: Commit**

---

### Task 25: Health Checks

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/HealthChecks/SlackQueueHealthCheck.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/HealthChecks/SlackApiHealthCheck.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/HealthChecks/SlackHealthCheckTests.cs`

- [ ] **Step 1: Write test** — `SlackQueueHealthCheck` returns Unhealthy if queue depth > 100
- [ ] **Step 2: Implement `SlackQueueHealthCheck`** — queries `GetPendingCountAsync()`
- [ ] **Step 3: Write test** — `SlackApiHealthCheck` returns Unhealthy if Slack API unreachable
- [ ] **Step 4: Implement `SlackApiHealthCheck`** — calls Slack `api.test` endpoint, returns Healthy/Unhealthy
- [ ] **Step 5: Register both in DI**

```csharp
services.AddHealthChecks()
    .AddCheck<SlackApiHealthCheck>("slack_api")
    .AddCheck<SlackQueueHealthCheck>("slack_queue");
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/HealthChecks/ apps/api/tests/
git commit -m "feat(notifications): add Slack API and queue health checks"
```

---

## Chunk 8: Event Handler Migration & Cleanup Jobs

### Task 26: Migrate ShareRequest Event Handler to NotificationDispatcher

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/ShareRequestCreatedNotificationHandler.cs`
- Test: Update existing tests

- [ ] **Step 1: Read existing handler implementation**
- [ ] **Step 2: Write test** — handler calls `_dispatcher.DispatchAsync()` with correct `ShareRequestPayload`
- [ ] **Step 3: Refactor handler** — replace direct `_notificationRepo.AddAsync()` + `_emailService.Send*()` with single `_dispatcher.DispatchAsync()` call
- [ ] **Step 4: Run existing + new tests — expected PASS**
- [ ] **Step 5: Commit**

---

### Task 27: Migrate Remaining Event Handlers (batch)

**Files:**
- Modify: All event handlers in `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/`

- [ ] **Step 1: List all handlers that create notifications + send emails**
- [ ] **Step 2: Refactor each to use `_dispatcher.DispatchAsync()` with typed payload**
- [ ] **Step 3: Create additional `INotificationPayload` records as needed per handler**
- [ ] **Step 4: Run all notification tests — expected PASS**
- [ ] **Step 5: Commit**

---

### Task 28: Update Cleanup & Dead Letter Jobs

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/NotificationCleanupJob.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Scheduling/DeadLetterMonitorJob.cs`

- [ ] **Step 1: Extend `NotificationCleanupJob`** — also clean `NotificationQueueItems` older than retention period
- [ ] **Step 2: Extend `DeadLetterMonitorJob`** — also check `NotificationQueueItems` dead letter count
- [ ] **Step 3: Run tests — expected PASS**
- [ ] **Step 4: Commit**

---

## Chunk 9: Secrets & Configuration

### Task 29: Slack Secret File

**Files:**
- Create: `infra/secrets/slack.secret.example`
- Modify: `infra/secrets/setup-secrets.ps1` (add Slack secret generation)

- [ ] **Step 1: Create example file**

```bash
# Slack App Credentials (from api.slack.com)
SLACK_CLIENT_ID=your_client_id
SLACK_CLIENT_SECRET=your_client_secret
SLACK_SIGNING_SECRET=your_signing_secret
SLACK_REDIRECT_URI=https://meepleai.app/api/v1/integrations/slack/callback

# Team Channel Webhooks
SLACK_OPS_WEBHOOK_URL=https://hooks.slack.com/services/T.../B.../xxx
SLACK_BUSINESS_WEBHOOK_URL=https://hooks.slack.com/services/T.../B.../yyy
SLACK_DEVOPS_WEBHOOK_URL=https://hooks.slack.com/services/T.../B.../zzz
```

- [ ] **Step 2: Add Slack section to `appsettings.json`**
- [ ] **Step 3: Update setup-secrets.ps1 for Slack secret**
- [ ] **Step 4: Commit**

---

## Chunk 10: Integration Tests & Final Verification

### Task 30: Integration Tests

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Integration/NotificationDispatcherIntegrationTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Integration/SlackNotificationProcessorJobIntegrationTests.cs`

- [ ] **Step 1: Write integration test** — full pipeline: domain event → handler → dispatcher → queue items created in DB
- [ ] **Step 2: Write integration test** — Slack processor job picks up items, sends (mock HTTP), marks sent
- [ ] **Step 3: Write integration test** — 429 handling, token revocation
- [ ] **Step 4: Run all tests** — `dotnet test --filter "Category=Integration"`
- [ ] **Step 5: Commit**

---

### Task 31: Full Test Suite Verification

- [ ] **Step 1: Run all backend tests**

Run: `cd apps/api && dotnet test -v minimal`
Expected: All tests PASS, no regressions

- [ ] **Step 2: Run build check**

Run: `cd apps/api/src/Api && dotnet build`
Expected: Build succeeded, 0 warnings related to new code

- [ ] **Step 3: Final commit with all pending changes**

---

## File Map Summary

### New Files (27 files)

| Category | Files |
|----------|-------|
| **Domain** | `NotificationChannelType.cs`, `NotificationQueueStatus.cs`, `NotificationPayloads.cs`, `NotificationQueueItem.cs`, `SlackConnection.cs` |
| **Repositories (interfaces)** | `INotificationQueueRepository.cs`, `ISlackConnectionRepository.cs` |
| **Application** | `INotificationDispatcher.cs`, `ConnectSlackCommand.cs`, `SlackOAuthCallbackCommand.cs`, `DisconnectSlackCommand.cs`, `HandleSlackInteractionCommand.cs` + handlers |
| **Infrastructure** | `NotificationDispatcher.cs`, `SlackNotificationProcessorJob.cs`, `SlackNotificationConfiguration.cs`, `SlackSignatureValidator.cs` |
| **EF Entities** | `NotificationQueueEntity.cs`, `SlackConnectionEntity.cs`, `SlackTeamChannelConfigEntity.cs` + configurations |
| **Slack Builders** | `ISlackMessageBuilder.cs`, `GenericSlackBuilder.cs`, `ShareRequestSlackBuilder.cs`, `GameNightSlackBuilder.cs`, `PdfProcessingSlackBuilder.cs`, `BadgeSlackBuilder.cs`, `AdminAlertSlackBuilder.cs`, `SlackMessageBuilderFactory.cs` |
| **Routing** | `SlackIntegrationEndpoints.cs`, `SlackInteractionEndpoints.cs`, `AdminNotificationQueueEndpoints.cs`, `AdminSlackEndpoints.cs` |
| **Config** | `slack.secret.example` |

### Modified Files (6 files)

| File | Change |
|------|--------|
| `MeepleAiDbContext.cs` | Add 3 DbSets |
| `UserNotificationsServiceExtensions.cs` | Register repositories, services, Quartz job, config |
| `NotificationCleanupJob.cs` | Extend to clean NotificationQueueItems |
| `DeadLetterMonitorJob.cs` | Extend to monitor NotificationQueueItems |
| `ShareRequestCreatedNotificationHandler.cs` + all event handlers | Refactor to use `INotificationDispatcher` |
| `Api.csproj` | Add SlackNet package |
