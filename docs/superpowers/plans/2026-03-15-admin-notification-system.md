# Admin Notification System Enhancement - Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add manual admin notification sending, access request Slack alerts, enhanced PDF processing Slack messages, and a compose notification UI for the MeepleAI admin panel.

**Architecture:** Extends the existing `UserNotifications` bounded context which already has a fully implemented `NotificationDispatcher` with 4-channel routing (InApp, Email, Slack DM, Slack Team), 48+ notification types, queue-based delivery with retry, and Slack Block Kit message builders. All new features reuse this infrastructure via CQRS commands and MediatR event handlers.

**Tech Stack:** .NET 9 (MediatR/CQRS, FluentValidation, EF Core), Next.js 16 (React 19, React Query, Zustand, Tailwind 4, shadcn/ui), Slack Block Kit API

---

## File Map

### Backend (C# - apps/api/src/Api/)

| Action | File | Responsibility |
|--------|------|----------------|
| **CREATE** | `BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommand.cs` | Command + handler for admin manual notification dispatch |
| **CREATE** | `BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandValidator.cs` | FluentValidation for manual notification input |
| **CREATE** | `BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandler.cs` | Event handler: access request → admin Slack/email/in-app alert |
| **MODIFY** | `BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs` | Add `AdminAccessRequestCreated`, `AdminManualNotification`, `AdminPdfProcessingStarted` types |
| **MODIFY** | `BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs` | Add `AccessRequestPayload`, `PdfProcessingDetailPayload` records + JSON discriminator |
| **MODIFY** | `BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs` | Enhance: add uploader name, file size, game title, timestamp fields |
| **MODIFY** | `BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs` | Register new admin types in `CanHandle()` |
| **MODIFY** | `BoundedContexts/UserNotifications/Application/EventHandlers/PdfNotificationEventHandler.cs` | Use enriched `PdfProcessingDetailPayload` with uploader/size/game info |
| **CREATE** | `Routing/AdminManualNotificationEndpoints.cs` | Endpoint: `POST /admin/notifications/send` |
| **MODIFY** | `Program.cs` | Register `MapAdminManualNotificationEndpoints()` |

### Frontend (TypeScript - apps/web/src/)

| Action | File | Responsibility |
|--------|------|----------------|
| **CREATE** | `lib/api/schemas/adminNotifications.schemas.ts` | Zod schemas for manual notification request/response |
| **CREATE** | `lib/api/clients/adminNotificationsClient.ts` | API client for `POST /admin/notifications/send` |
| **MODIFY** | `lib/api/index.ts` | Register `adminNotifications` client |
| **CREATE** | `app/admin/(dashboard)/notifications/compose/page.tsx` | Admin compose notification page |
| **CREATE** | `components/admin/notifications/ChannelSelector.tsx` | Multi-select channel checkboxes (Slack/Email/InApp) |
| **CREATE** | `components/admin/notifications/RecipientSelector.tsx` | Recipient picker: single user, role group, all users |
| **CREATE** | `components/admin/notifications/NotificationPreview.tsx` | Preview panel showing formatted message per channel |
| **MODIFY** | `config/admin-dashboard-navigation.ts` | Add "Compose Notification" to System section sidebar |

### Tests (Backend)

| Action | File | Responsibility |
|--------|------|----------------|
| **CREATE** | `tests/Api.Tests/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandTests.cs` | Unit tests for manual notification command handler |
| **CREATE** | `tests/Api.Tests/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandValidatorTests.cs` | Validator tests |
| **CREATE** | `tests/Api.Tests/BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandlerTests.cs` | Event handler tests |
| **CREATE** | `tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilderEnhancedTests.cs` | Enhanced PDF Slack builder tests |

### Tests (Frontend)

| Action | File | Responsibility |
|--------|------|----------------|
| **CREATE** | `app/admin/(dashboard)/notifications/compose/__tests__/page.test.tsx` | Compose page render + interaction tests |

---

## Chunk 1: Backend - New Notification Types & Payloads

### Task 1: Add new NotificationType constants

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs`

- [ ] **Step 1: Add 3 new notification type constants after line 77 (AdminModelAutoFallback)**

```csharp
// Admin notification for new access request (invite-only registration)
public static readonly NotificationType AdminAccessRequestCreated = new("admin_access_request_created");

// Admin manual notification sent from compose UI
public static readonly NotificationType AdminManualNotification = new("admin_manual_notification");

// Admin notification when PDF processing starts (enriched with uploader details)
public static readonly NotificationType AdminPdfProcessingStarted = new("admin_pdf_processing_started");
```

- [ ] **Step 2: Add 3 entries to the `FromString` switch expression (before the `_ => throw` default)**

```csharp
"admin_access_request_created" => AdminAccessRequestCreated,
"admin_manual_notification" => AdminManualNotification,
"admin_pdf_processing_started" => AdminPdfProcessingStarted,
```

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs
git commit -m "feat(notifications): add AdminAccessRequestCreated, AdminManualNotification, AdminPdfProcessingStarted types"
```

### Task 2: Add new payload records

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs`

- [ ] **Step 1: Add JSON discriminator attributes for new payload types (on `INotificationPayload` interface)**

```csharp
[JsonDerivedType(typeof(AccessRequestPayload), "AccessRequestPayload")]
[JsonDerivedType(typeof(PdfProcessingDetailPayload), "PdfProcessingDetailPayload")]
[JsonDerivedType(typeof(ManualNotificationPayload), "ManualNotificationPayload")]
```

- [ ] **Step 2: Add new payload record definitions after `GenericPayload`**

```csharp
public record AccessRequestPayload(
    Guid AccessRequestId,
    string Email,
    DateTime RequestedAt) : INotificationPayload;

public record PdfProcessingDetailPayload(
    Guid PdfId,
    string FileName,
    string Status,
    string UploaderName,
    long FileSizeBytes,
    string? GameTitle) : INotificationPayload;

public record ManualNotificationPayload(
    string Title,
    string Body,
    Guid SentByAdminId,
    string SentByAdminName) : INotificationPayload;
```

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationPayloads.cs
git commit -m "feat(notifications): add AccessRequestPayload, PdfProcessingDetailPayload, ManualNotificationPayload"
```

---

## Chunk 2: Backend - Access Request Admin Alert Handler

### Task 3: Write tests for AccessRequestCreatedNotificationHandler

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandlerTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.EventHandlers;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.EventHandlers;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class AccessRequestCreatedNotificationHandlerTests
{
    private readonly INotificationDispatcher _dispatcher = Substitute.For<INotificationDispatcher>();
    private readonly ILogger<AccessRequestCreatedNotificationHandler> _logger =
        Substitute.For<ILogger<AccessRequestCreatedNotificationHandler>>();

    [Fact]
    public async Task Handle_WithAdminUsers_DispatchesNotificationToEachAdmin()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new MeepleAiDbContext(options);
        var admin1Id = Guid.NewGuid();
        var admin2Id = Guid.NewGuid();
        dbContext.Set<UserEntity>().AddRange(
            new UserEntity { Id = admin1Id, Email = "admin1@test.com", Role = "admin", DisplayName = "Admin1" },
            new UserEntity { Id = admin2Id, Email = "admin2@test.com", Role = "admin", DisplayName = "Admin2" },
            new UserEntity { Id = Guid.NewGuid(), Email = "user@test.com", Role = "user", DisplayName = "User1" }
        );
        await dbContext.SaveChangesAsync();

        var handler = new AccessRequestCreatedNotificationHandler(_dispatcher, dbContext, _logger);
        var evt = new AccessRequestCreatedEvent(Guid.NewGuid(), "newuser@example.com");

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert - should dispatch exactly 2 notifications (one per admin)
        await _dispatcher.Received(2).DispatchAsync(
            Arg.Is<NotificationMessage>(m =>
                m.Type == NotificationType.AdminAccessRequestCreated),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoAdmins_LogsWarningAndDoesNotDispatch()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new MeepleAiDbContext(options);
        dbContext.Set<UserEntity>().Add(
            new UserEntity { Id = Guid.NewGuid(), Email = "user@test.com", Role = "user", DisplayName = "User1" }
        );
        await dbContext.SaveChangesAsync();

        var handler = new AccessRequestCreatedNotificationHandler(_dispatcher, dbContext, _logger);
        var evt = new AccessRequestCreatedEvent(Guid.NewGuid(), "newuser@example.com");

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        await _dispatcher.DidNotReceive().DispatchAsync(
            Arg.Any<NotificationMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PayloadContainsAccessRequestEmail()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"test_{Guid.NewGuid()}")
            .Options;

        await using var dbContext = new MeepleAiDbContext(options);
        dbContext.Set<UserEntity>().Add(
            new UserEntity { Id = Guid.NewGuid(), Email = "admin@test.com", Role = "admin", DisplayName = "Admin" }
        );
        await dbContext.SaveChangesAsync();

        var handler = new AccessRequestCreatedNotificationHandler(_dispatcher, dbContext, _logger);
        var requestId = Guid.NewGuid();
        var evt = new AccessRequestCreatedEvent(requestId, "requester@example.com");

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        await _dispatcher.Received(1).DispatchAsync(
            Arg.Is<NotificationMessage>(m =>
                m.Payload is AccessRequestPayload p
                && p.Email == "requester@example.com"
                && p.AccessRequestId == requestId
                && m.DeepLinkPath == "/admin/users/access-requests"),
            Arg.Any<CancellationToken>());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AccessRequestCreatedNotificationHandlerTests" --no-restore -v m`
Expected: FAIL (class `AccessRequestCreatedNotificationHandler` does not exist)

- [ ] **Step 3: Implement the handler**

Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandler.cs`

```csharp
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// Dispatches admin notifications when a new access request is created (invite-only registration).
/// Mirrors NewShareRequestAdminAlertHandler pattern for consistency.
/// </summary>
internal sealed class AccessRequestCreatedNotificationHandler : INotificationHandler<AccessRequestCreatedEvent>
{
    private readonly INotificationDispatcher _dispatcher;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<AccessRequestCreatedNotificationHandler> _logger;

    public AccessRequestCreatedNotificationHandler(
        INotificationDispatcher dispatcher,
        MeepleAiDbContext dbContext,
        ILogger<AccessRequestCreatedNotificationHandler> logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(AccessRequestCreatedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            var adminUsers = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (adminUsers.Count == 0)
            {
                _logger.LogWarning(
                    "No admin users found to notify for access request {AccessRequestId}",
                    notification.AccessRequestId);
                return;
            }

            foreach (var adminId in adminUsers)
            {
                await _dispatcher.DispatchAsync(new NotificationMessage
                {
                    Type = NotificationType.AdminAccessRequestCreated,
                    RecipientUserId = adminId,
                    Payload = new AccessRequestPayload(
                        notification.AccessRequestId,
                        notification.Email,
                        DateTime.UtcNow),
                    DeepLinkPath = "/admin/users/access-requests"
                }, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Dispatched {Count} admin notifications for access request {AccessRequestId} from {Email}",
                adminUsers.Count,
                notification.AccessRequestId,
                notification.Email);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create admin notifications for access request {AccessRequestId}",
                notification.AccessRequestId);
        }
#pragma warning restore CA1031
    }
}
```

- [ ] **Step 4: Register new type in AdminAlertSlackBuilder.CanHandle()**

Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs`

Add to the `CanHandle` method after the existing checks:

```csharp
|| type == NotificationType.AdminAccessRequestCreated
|| type == NotificationType.AdminManualNotification
|| type == NotificationType.AdminPdfProcessingStarted
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AccessRequestCreatedNotificationHandlerTests" --no-restore -v m`
Expected: 3 passed

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/EventHandlers/AccessRequestCreatedNotificationHandlerTests.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs
git commit -m "feat(notifications): add AccessRequestCreatedNotificationHandler for admin Slack/email alerts"
```

---

## Chunk 3: Backend - Enhanced PDF Processing Slack Notification

### Task 4: Enhance PdfProcessingSlackBuilder with uploader details

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilderEnhancedTests.cs`

- [ ] **Step 1: Write tests for the enhanced builder**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.BoundedContexts.UserNotifications.Infrastructure.Slack;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure.Slack;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class PdfProcessingSlackBuilderEnhancedTests
{
    private readonly PdfProcessingSlackBuilder _builder;

    public PdfProcessingSlackBuilderEnhancedTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Frontend:BaseUrl"] = "https://meepleai.app"
            })
            .Build();
        _builder = new PdfProcessingSlackBuilder(config);
    }

    [Fact]
    public void CanHandle_AdminPdfProcessingStarted_ReturnsTrue()
    {
        Assert.True(_builder.CanHandle(NotificationType.AdminPdfProcessingStarted));
    }

    [Fact]
    public void BuildMessage_WithDetailPayload_IncludesUploaderAndSize()
    {
        // Arrange
        var payload = new PdfProcessingDetailPayload(
            PdfId: Guid.NewGuid(),
            FileName: "Catan-Rules-IT.pdf",
            Status: "Processing",
            UploaderName: "marco",
            FileSizeBytes: 4_400_000,
            GameTitle: "Catan");

        // Act
        var result = _builder.BuildMessage(payload, "/documents/123");

        // Assert - result should be anonymous object with blocks
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("Catan-Rules-IT.pdf", json);
        Assert.Contains("marco", json);
        Assert.Contains("4.2 MB", json);
        Assert.Contains("Catan", json);
    }

    [Fact]
    public void BuildMessage_WithLegacyPayload_StillWorks()
    {
        // Arrange - existing PdfProcessingPayload should still work
        var payload = new PdfProcessingPayload(
            PdfId: Guid.NewGuid(),
            FileName: "test.pdf",
            Status: "Ready");

        // Act
        var result = _builder.BuildMessage(payload, "/documents/456");

        // Assert
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("test.pdf", json);
        Assert.Contains("Documento pronto", json);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfProcessingSlackBuilderEnhancedTests" --no-restore -v m`
Expected: FAIL

- [ ] **Step 3: Enhance PdfProcessingSlackBuilder to handle both payload types**

Replace the content of `PdfProcessingSlackBuilder.cs`:

```csharp
using System.Globalization;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Slack;

/// <summary>
/// Block Kit builder for PDF processing notifications (ready, failed, or processing started).
/// Supports both legacy PdfProcessingPayload and enriched PdfProcessingDetailPayload.
/// </summary>
internal sealed class PdfProcessingSlackBuilder : ISlackMessageBuilder
{
    private readonly string _frontendBaseUrl;

    public PdfProcessingSlackBuilder(IConfiguration configuration)
    {
#pragma warning disable S1075
        _frontendBaseUrl = configuration["Frontend:BaseUrl"] ?? "https://meepleai.app";
#pragma warning restore S1075
    }

    public bool CanHandle(NotificationType type)
    {
        return type == NotificationType.PdfUploadCompleted
            || type == NotificationType.ProcessingFailed
            || type == NotificationType.ProcessingJobCompleted
            || type == NotificationType.ProcessingJobFailed
            || type == NotificationType.AdminPdfProcessingStarted;
    }

    public object BuildMessage(INotificationPayload payload, string? deepLinkPath)
    {
        return payload switch
        {
            PdfProcessingDetailPayload detail => BuildDetailMessage(detail, deepLinkPath),
            PdfProcessingPayload pdf => BuildLegacyMessage(pdf, deepLinkPath),
            _ => throw new ArgumentException(
                $"Expected PDF payload but received {payload.GetType().Name}", nameof(payload))
        };
    }

    private object BuildDetailMessage(PdfProcessingDetailPayload detail, string? deepLinkPath)
    {
        var isSuccess = string.Equals(detail.Status, "Ready", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(detail.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        var isProcessing = string.Equals(detail.Status, "Processing", StringComparison.OrdinalIgnoreCase);

        var emoji = isSuccess ? "\u2705" : isProcessing ? "\u23f3" : "\u274c";
        var statusText = isSuccess ? "Documento pronto"
                       : isProcessing ? "Elaborazione PDF avviata"
                       : "Elaborazione fallita";

        var fileSizeFormatted = FormatFileSize(detail.FileSizeBytes);

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"{emoji} {statusText}", emoji = true }
            },
            new
            {
                type = "section",
                fields = new object[]
                {
                    new { type = "mrkdwn", text = $"\ud83d\udcc4 *File*\n{detail.FileName}" },
                    new { type = "mrkdwn", text = $"\ud83d\udc64 *Caricato da*\n{detail.UploaderName}" },
                    new { type = "mrkdwn", text = $"\ud83d\udcbe *Dimensione*\n{fileSizeFormatted}" },
                    new { type = "mrkdwn", text = $"\ud83c\udfae *Gioco*\n{detail.GameTitle ?? "N/A"}" }
                }
            },
            new
            {
                type = "context",
                elements = new object[]
                {
                    new { type = "mrkdwn", text = $"MeepleAI RAG | {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC" }
                }
            }
        };

        if (!string.IsNullOrEmpty(deepLinkPath))
        {
            var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";
            blocks.Add(new
            {
                type = "actions",
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\ud83d\udd17 Apri in MeepleAI", emoji = true },
                        action_id = "open_meepleai",
                        url
                    }
                }
            });
        }

        return new { blocks };
    }

    private object BuildLegacyMessage(PdfProcessingPayload pdf, string? deepLinkPath)
    {
        var isSuccess = string.Equals(pdf.Status, "Ready", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(pdf.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        var emoji = isSuccess ? "\u2705" : "\u274c";
        var statusText = isSuccess ? "Documento pronto" : "Elaborazione fallita";

        var blocks = new List<object>
        {
            new
            {
                type = "header",
                text = new { type = "plain_text", text = $"{emoji} {statusText}", emoji = true }
            },
            new
            {
                type = "section",
                text = new { type = "mrkdwn", text = $"\ud83d\udcc4 *File*: {pdf.FileName}" }
            }
        };

        if (!string.IsNullOrEmpty(deepLinkPath))
        {
            var url = $"{_frontendBaseUrl.TrimEnd('/')}{deepLinkPath}";
            blocks.Add(new
            {
                type = "actions",
                elements = new object[]
                {
                    new
                    {
                        type = "button",
                        text = new { type = "plain_text", text = "\ud83d\udd17 Apri in MeepleAI", emoji = true },
                        action_id = "open_meepleai",
                        url
                    }
                }
            });
        }

        return new { blocks };
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:0.#} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:0.#} MB",
            >= 1024 => $"{bytes / 1024.0:0.#} KB",
            _ => $"{bytes} B"
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfProcessingSlackBuilderEnhancedTests" --no-restore -v m`
Expected: 3 passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilder.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/Slack/PdfProcessingSlackBuilderEnhancedTests.cs
git commit -m "feat(notifications): enhance PdfProcessingSlackBuilder with uploader, file size, game title"
```

### Task 5: Enhance PdfNotificationEventHandler to send enriched admin notification

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/PdfNotificationEventHandler.cs`

- [ ] **Step 1: Add admin notification dispatch for PdfStateChangedEvent (state=Processing)**

The existing handler only fires on `Ready` state. Add a handler for the `Processing` state that sends to all admins:

```csharp
// Add to existing Handle(PdfStateChangedEvent) method, BEFORE the Ready check:
if (evt.NewState == PdfProcessingState.TextExtraction && evt.PreviousState == PdfProcessingState.Uploaded)
{
    // First processing step: notify admins with enriched details
    var pdfDocForAdmin = await _pdfRepo.GetByIdAsync(evt.PdfDocumentId, cancellationToken).ConfigureAwait(false);
    if (pdfDocForAdmin != null)
    {
        var uploaderName = await GetUploaderNameAsync(evt.UploadedByUserId, cancellationToken).ConfigureAwait(false);
        var gameTitleForAdmin = await GetGameTitleAsync(pdfDocForAdmin, cancellationToken).ConfigureAwait(false);

        var adminUsers = await _dbContext.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => u.Role == "admin")
            .Select(u => u.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var adminId in adminUsers)
        {
            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AdminPdfProcessingStarted,
                RecipientUserId = adminId,
                Payload = new PdfProcessingDetailPayload(
                    evt.PdfDocumentId,
                    pdfDocForAdmin.FileName.Value,
                    "Processing",
                    uploaderName,
                    pdfDocForAdmin.FileSize.Bytes,
                    gameTitleForAdmin),
                DeepLinkPath = $"/documents/{evt.PdfDocumentId}"
            }, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Dispatched {Count} admin PDF processing notifications for {FileName} by {Uploader}",
            adminUsers.Count, pdfDocForAdmin.FileName.Value, uploaderName);
    }
}
```

Note: The handler will need additional DI injections (`MeepleAiDbContext`) and helper methods. The exact implementation depends on how the PdfDocument entity relates to games (via `GameId` or similar FK). The key integration points are:
- `_dbContext.Set<UserEntity>()` for admin lookup (inject `MeepleAiDbContext`)
- `pdfDoc.FileSize.Bytes` (long) from the `FileSize` value object
- `pdfDoc.FileName.Value` (string) from the `FileName` value object
- Game title lookup depends on the document's association

- [ ] **Step 2: Add helper methods to the handler**

```csharp
private async Task<string> GetUploaderNameAsync(Guid userId, CancellationToken ct)
{
    var user = await _dbContext.Set<UserEntity>()
        .AsNoTracking()
        .Where(u => u.Id == userId)
        .Select(u => new { u.DisplayName, u.Email })
        .FirstOrDefaultAsync(ct)
        .ConfigureAwait(false);
    return user?.DisplayName ?? user?.Email ?? "Unknown";
}

private async Task<string?> GetGameTitleAsync(dynamic pdfDoc, CancellationToken ct)
{
    // Implementation depends on PdfDocument → Game relationship
    // Check if PdfDocument has GameId or SharedGameId property
    return null; // Placeholder - investigate PdfDocument entity for game FK
}
```

- [ ] **Step 3: Build and verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Run existing PDF notification tests to ensure no regression**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PdfNotification" --no-restore -v m`
Expected: All existing tests pass

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/PdfNotificationEventHandler.cs
git commit -m "feat(notifications): add enriched admin PDF processing notification with uploader details"
```

---

## Chunk 4: Backend - Manual Notification Command & Endpoint

### Task 6: Write tests for SendManualNotificationCommand

**Files:**
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandValidatorTests.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandTests.cs`

- [ ] **Step 1: Write validator tests**

```csharp
using Api.BoundedContexts.UserNotifications.Application.Commands;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "UserNotifications")]
public sealed class SendManualNotificationCommandValidatorTests
{
    private readonly SendManualNotificationCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_NoErrors()
    {
        var cmd = new SendManualNotificationCommand
        {
            Title = "Maintenance Window",
            Message = "System maintenance tonight 22:00 UTC",
            Channels = ["inapp", "email"],
            RecipientType = "all",
            SentByAdminId = Guid.NewGuid(),
            SentByAdminName = "Admin User"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTitle_HasError()
    {
        var cmd = new SendManualNotificationCommand
        {
            Title = "",
            Message = "body",
            Channels = ["inapp"],
            RecipientType = "all",
            SentByAdminId = Guid.NewGuid(),
            SentByAdminName = "Admin"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_EmptyChannels_HasError()
    {
        var cmd = new SendManualNotificationCommand
        {
            Title = "Test",
            Message = "body",
            Channels = [],
            RecipientType = "all",
            SentByAdminId = Guid.NewGuid(),
            SentByAdminName = "Admin"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.Channels);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("")]
    public void Validate_InvalidRecipientType_HasError(string recipientType)
    {
        var cmd = new SendManualNotificationCommand
        {
            Title = "Test",
            Message = "body",
            Channels = ["inapp"],
            RecipientType = recipientType,
            SentByAdminId = Guid.NewGuid(),
            SentByAdminName = "Admin"
        };
        var result = _validator.TestValidate(cmd);
        result.ShouldHaveValidationErrorFor(x => x.RecipientType);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SendManualNotificationCommandValidator" --no-restore -v m`
Expected: FAIL (class does not exist)

- [ ] **Step 3: Implement the command, validator, and handler**

Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommand.cs`

```csharp
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>
/// Command for admin to send manual notifications to users via selected channels.
/// Supports targeting: all users, by role, or specific user IDs.
/// </summary>
internal record SendManualNotificationCommand : ICommand<SendManualNotificationResult>
{
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string[] Channels { get; init; }  // "inapp", "email", "slack"
    public required string RecipientType { get; init; } // "all", "role", "users"
    public string? RecipientRole { get; init; }         // when RecipientType = "role"
    public Guid[]? RecipientUserIds { get; init; }      // when RecipientType = "users"
    public string? DeepLinkPath { get; init; }
    public required Guid SentByAdminId { get; init; }
    public required string SentByAdminName { get; init; }
}

internal record SendManualNotificationResult(
    int TotalRecipients,
    int Dispatched,
    int Skipped);

internal sealed class SendManualNotificationCommandValidator : AbstractValidator<SendManualNotificationCommand>
{
    private static readonly string[] ValidRecipientTypes = ["all", "role", "users"];
    private static readonly string[] ValidChannels = ["inapp", "email", "slack"];

    public SendManualNotificationCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Message).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Channels).NotEmpty()
            .Must(c => c.All(ch => ValidChannels.Contains(ch, StringComparer.OrdinalIgnoreCase)))
            .WithMessage("Channels must be: inapp, email, slack");
        RuleFor(x => x.RecipientType).NotEmpty()
            .Must(rt => ValidRecipientTypes.Contains(rt, StringComparer.OrdinalIgnoreCase))
            .WithMessage("RecipientType must be: all, role, or users");
        RuleFor(x => x.RecipientRole)
            .NotEmpty()
            .When(x => string.Equals(x.RecipientType, "role", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.RecipientUserIds)
            .NotEmpty()
            .When(x => string.Equals(x.RecipientType, "users", StringComparison.OrdinalIgnoreCase));
        RuleFor(x => x.SentByAdminId).NotEmpty();
        RuleFor(x => x.SentByAdminName).NotEmpty();
    }
}

internal sealed class SendManualNotificationCommandHandler
    : IRequestHandler<SendManualNotificationCommand, SendManualNotificationResult>
{
    private const int MaxRecipients = 100;

    private readonly INotificationDispatcher _dispatcher;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<SendManualNotificationCommandHandler> _logger;

    public SendManualNotificationCommandHandler(
        INotificationDispatcher dispatcher,
        MeepleAiDbContext dbContext,
        ILogger<SendManualNotificationCommandHandler> logger)
    {
        _dispatcher = dispatcher;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SendManualNotificationResult> Handle(
        SendManualNotificationCommand request, CancellationToken cancellationToken)
    {
        // Resolve recipient user IDs
        var recipientIds = await ResolveRecipientsAsync(request, cancellationToken).ConfigureAwait(false);

        if (recipientIds.Count == 0)
        {
            return new SendManualNotificationResult(0, 0, 0);
        }

        if (recipientIds.Count > MaxRecipients)
        {
            _logger.LogWarning(
                "Manual notification capped at {Max} recipients (requested {Requested})",
                MaxRecipients, recipientIds.Count);
            recipientIds = recipientIds.Take(MaxRecipients).ToList();
        }

        var dispatched = 0;
        var skipped = 0;

        foreach (var userId in recipientIds)
        {
            try
            {
                await _dispatcher.DispatchAsync(new NotificationMessage
                {
                    Type = NotificationType.AdminManualNotification,
                    RecipientUserId = userId,
                    Payload = new ManualNotificationPayload(
                        request.Title,
                        request.Message,
                        request.SentByAdminId,
                        request.SentByAdminName),
                    DeepLinkPath = request.DeepLinkPath
                }, cancellationToken).ConfigureAwait(false);

                dispatched++;
            }
#pragma warning disable CA1031
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to dispatch manual notification to user {UserId}", userId);
                skipped++;
            }
#pragma warning restore CA1031
        }

        _logger.LogInformation(
            "Admin {AdminName} sent manual notification '{Title}' to {Dispatched}/{Total} recipients",
            request.SentByAdminName, request.Title, dispatched, recipientIds.Count);

        return new SendManualNotificationResult(recipientIds.Count, dispatched, skipped);
    }

    private async Task<List<Guid>> ResolveRecipientsAsync(
        SendManualNotificationCommand request, CancellationToken ct)
    {
        var query = _dbContext.Set<UserEntity>().AsNoTracking();

        return request.RecipientType.ToLowerInvariant() switch
        {
            "all" => await query.Select(u => u.Id).ToListAsync(ct).ConfigureAwait(false),
            "role" => await query
                .Where(u => u.Role == request.RecipientRole)
                .Select(u => u.Id)
                .ToListAsync(ct).ConfigureAwait(false),
            "users" => request.RecipientUserIds?.ToList() ?? [],
            _ => []
        };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~SendManualNotificationCommandValidator" --no-restore -v m`
Expected: All passed

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommand.cs
git add apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Application/Commands/SendManualNotificationCommandValidatorTests.cs
git commit -m "feat(notifications): add SendManualNotificationCommand with validator and handler"
```

### Task 7: Create the admin endpoint

**Files:**
- Create: `apps/api/src/Api/Routing/AdminManualNotificationEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Create the endpoint file**

```csharp
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Filters;
using MediatR;

namespace Api.Routing;

/// <summary>
/// Admin endpoint for sending manual notifications to users.
/// Supports channel selection (inapp, email, slack) and recipient targeting.
/// </summary>
internal static class AdminManualNotificationEndpoints
{
    public static void MapAdminManualNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin/notifications")
            .WithTags("Admin - Manual Notifications")
            .AddEndpointFilter<RequireAdminSessionFilter>();

        group.MapPost("/send", HandleSendManualNotification)
            .WithName("SendManualNotification")
            .Produces<SendManualNotificationResult>(200)
            .Produces(400)
            .WithSummary("Send a manual notification to selected users via chosen channels");
    }

    private static async Task<IResult> HandleSendManualNotification(
        SendManualNotificationRequest request,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken ct)
    {
        // Extract admin info from session
        var adminId = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst("userId")?.Value;
        var adminName = httpContext.User.FindFirst("displayName")?.Value
            ?? httpContext.User.FindFirst("email")?.Value
            ?? "Admin";

        if (string.IsNullOrEmpty(adminId) || !Guid.TryParse(adminId, out var adminGuid))
        {
            return Results.Unauthorized();
        }

        var command = new SendManualNotificationCommand
        {
            Title = request.Title,
            Message = request.Message,
            Channels = request.Channels,
            RecipientType = request.RecipientType,
            RecipientRole = request.RecipientRole,
            RecipientUserIds = request.RecipientUserIds,
            DeepLinkPath = request.DeepLinkPath,
            SentByAdminId = adminGuid,
            SentByAdminName = adminName
        };

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

internal record SendManualNotificationRequest(
    string Title,
    string Message,
    string[] Channels,
    string RecipientType,
    string? RecipientRole = null,
    Guid[]? RecipientUserIds = null,
    string? DeepLinkPath = null);
```

- [ ] **Step 2: Register endpoint in Program.cs**

Add after line `v1Api.MapAdminNotificationQueueEndpoints();` (line ~627):

```csharp
v1Api.MapAdminManualNotificationEndpoints(); // Admin manual notification send
```

Note: The `/admin/notifications` group prefix is already used by `AdminNotificationQueueEndpoints`. The new endpoint at `/admin/notifications/send` will not conflict with existing routes (`/queue`, `/dead-letter`, `/metrics`, `/legacy-queue/count`).

- [ ] **Step 3: Build to verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Routing/AdminManualNotificationEndpoints.cs
git add apps/api/src/Api/Program.cs
git commit -m "feat(notifications): add POST /admin/notifications/send endpoint"
```

---

## Chunk 5: Frontend - API Client & Schemas

### Task 8: Create Zod schemas for manual notification

**Files:**
- Create: `apps/web/src/lib/api/schemas/adminNotifications.schemas.ts`

- [ ] **Step 1: Create the schema file**

```typescript
/**
 * Admin Notification Schemas
 *
 * Zod schemas for admin manual notification endpoints.
 */

import { z } from 'zod';

// ──────────────────────────────────────────────
// Request
// ──────────────────────────────────────────────

export const SendManualNotificationRequestSchema = z.object({
  title: z.string().min(1).max(200),
  message: z.string().min(1).max(2000),
  channels: z.array(z.enum(['inapp', 'email', 'slack'])).min(1),
  recipientType: z.enum(['all', 'role', 'users']),
  recipientRole: z.string().optional(),
  recipientUserIds: z.array(z.string().uuid()).optional(),
  deepLinkPath: z.string().optional(),
});

// ──────────────────────────────────────────────
// Response
// ──────────────────────────────────────────────

export const SendManualNotificationResultSchema = z.object({
  totalRecipients: z.number().int(),
  dispatched: z.number().int(),
  skipped: z.number().int(),
});

// ──────────────────────────────────────────────
// Type Exports
// ──────────────────────────────────────────────

export type SendManualNotificationRequest = z.infer<typeof SendManualNotificationRequestSchema>;
export type SendManualNotificationResult = z.infer<typeof SendManualNotificationResultSchema>;
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/schemas/adminNotifications.schemas.ts
git commit -m "feat(notifications): add Zod schemas for admin manual notification"
```

### Task 9: Create API client

**Files:**
- Create: `apps/web/src/lib/api/clients/adminNotificationsClient.ts`
- Modify: `apps/web/src/lib/api/index.ts`

- [ ] **Step 1: Create the client file**

```typescript
/**
 * Admin Notifications API Client
 *
 * Client for admin manual notification dispatch.
 */

import type { HttpClient } from '../core/httpClient';
import {
  SendManualNotificationResultSchema,
  type SendManualNotificationRequest,
  type SendManualNotificationResult,
} from '../schemas/adminNotifications.schemas';

export interface CreateAdminNotificationsClientParams {
  httpClient: HttpClient;
}

export function createAdminNotificationsClient({ httpClient }: CreateAdminNotificationsClientParams) {
  return {
    /**
     * Send a manual notification to selected users via chosen channels.
     * POST /api/v1/admin/notifications/send
     */
    async sendManualNotification(
      request: SendManualNotificationRequest
    ): Promise<SendManualNotificationResult> {
      const response = await httpClient.post<SendManualNotificationResult>(
        '/api/v1/admin/notifications/send',
        request,
        SendManualNotificationResultSchema
      );
      return response;
    },
  };
}

export type AdminNotificationsClient = ReturnType<typeof createAdminNotificationsClient>;
```

- [ ] **Step 2: Register in API index**

Modify `apps/web/src/lib/api/index.ts` — add import and registration following the existing pattern.

Look for the pattern where clients are created (e.g., `notifications: createNotificationsClient({ httpClient })`) and add:

```typescript
import { createAdminNotificationsClient } from './clients/adminNotificationsClient';

// In the api object:
adminNotifications: createAdminNotificationsClient({ httpClient }),
```

- [ ] **Step 3: Verify TypeScript compilation**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/clients/adminNotificationsClient.ts
git add apps/web/src/lib/api/index.ts
git commit -m "feat(notifications): add admin notifications API client"
```

---

## Chunk 6: Frontend - Compose Notification Page

### Task 10: Create channel selector component

**Files:**
- Create: `apps/web/src/components/admin/notifications/ChannelSelector.tsx`

- [ ] **Step 1: Create the component**

```tsx
'use client';

import { MailIcon, BellIcon, MessageSquareIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface ChannelSelectorProps {
  selected: string[];
  onChange: (channels: string[]) => void;
}

const CHANNELS = [
  { id: 'inapp', label: 'In-App', icon: BellIcon, description: 'Notification center' },
  { id: 'email', label: 'Email', icon: MailIcon, description: 'Email queue' },
  { id: 'slack', label: 'Slack', icon: MessageSquareIcon, description: 'Slack DM' },
] as const;

export function ChannelSelector({ selected, onChange }: ChannelSelectorProps) {
  function toggle(channelId: string) {
    if (selected.includes(channelId)) {
      onChange(selected.filter(c => c !== channelId));
    } else {
      onChange([...selected, channelId]);
    }
  }

  return (
    <div className="flex gap-3">
      {CHANNELS.map(channel => {
        const Icon = channel.icon;
        const isSelected = selected.includes(channel.id);

        return (
          <button
            key={channel.id}
            type="button"
            onClick={() => toggle(channel.id)}
            className={cn(
              'flex items-center gap-2 px-4 py-3 rounded-lg border-2 transition-all',
              'text-sm font-medium',
              isSelected
                ? 'border-primary bg-primary/5 text-primary'
                : 'border-border bg-background text-muted-foreground hover:border-muted-foreground/50'
            )}
          >
            <Icon className="h-4 w-4" />
            <div className="text-left">
              <div>{channel.label}</div>
              <div className="text-xs font-normal opacity-70">{channel.description}</div>
            </div>
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/admin/notifications/ChannelSelector.tsx
git commit -m "feat(notifications): add ChannelSelector component"
```

### Task 11: Create recipient selector component

**Files:**
- Create: `apps/web/src/components/admin/notifications/RecipientSelector.tsx`

- [ ] **Step 1: Create the component**

```tsx
'use client';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

export interface RecipientSelection {
  type: 'all' | 'role' | 'users';
  role?: string;
  userIds?: string[];
}

export interface RecipientSelectorProps {
  value: RecipientSelection;
  onChange: (value: RecipientSelection) => void;
}

const ROLES = ['admin', 'editor', 'user'] as const;

export function RecipientSelector({ value, onChange }: RecipientSelectorProps) {
  return (
    <div className="space-y-3">
      <div>
        <Label className="text-sm font-medium">Recipients</Label>
        <Select
          value={value.type}
          onValueChange={(type: string) =>
            onChange({ type: type as RecipientSelection['type'] })
          }
        >
          <SelectTrigger className="w-full mt-1.5 bg-white/70 dark:bg-zinc-800/70">
            <SelectValue placeholder="Select recipients" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Users</SelectItem>
            <SelectItem value="role">By Role</SelectItem>
            <SelectItem value="users">Specific Users</SelectItem>
          </SelectContent>
        </Select>
      </div>

      {value.type === 'role' && (
        <div>
          <Label className="text-sm font-medium">Role</Label>
          <Select
            value={value.role ?? ''}
            onValueChange={(role: string) => onChange({ ...value, role })}
          >
            <SelectTrigger className="w-full mt-1.5 bg-white/70 dark:bg-zinc-800/70">
              <SelectValue placeholder="Select role" />
            </SelectTrigger>
            <SelectContent>
              {ROLES.map(r => (
                <SelectItem key={r} value={r}>
                  {r.charAt(0).toUpperCase() + r.slice(1)}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      )}

      {value.type === 'users' && (
        <div>
          <Label className="text-sm font-medium">User IDs (comma-separated)</Label>
          <Input
            className="mt-1.5 bg-white/70 dark:bg-zinc-800/70"
            placeholder="uuid1, uuid2, ..."
            value={(value.userIds ?? []).join(', ')}
            onChange={e => {
              const ids = e.target.value
                .split(',')
                .map(s => s.trim())
                .filter(Boolean);
              onChange({ ...value, userIds: ids });
            }}
          />
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/admin/notifications/RecipientSelector.tsx
git commit -m "feat(notifications): add RecipientSelector component"
```

### Task 12: Create notification preview component

**Files:**
- Create: `apps/web/src/components/admin/notifications/NotificationPreview.tsx`

- [ ] **Step 1: Create the component**

```tsx
'use client';

import { BellIcon, MailIcon, MessageSquareIcon } from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';

interface NotificationPreviewProps {
  title: string;
  message: string;
  channels: string[];
}

export function NotificationPreview({ title, message, channels }: NotificationPreviewProps) {
  if (!title && !message) {
    return (
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-dashed">
        <CardContent className="p-6 text-center text-muted-foreground text-sm">
          Start typing to see a preview
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-3">
      {channels.includes('inapp') && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <BellIcon className="h-4 w-4 text-teal-500" />
              In-App Preview
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            <div className="border-l-2 border-teal-400 pl-3">
              <p className="font-medium text-sm">{title}</p>
              <p className="text-sm text-muted-foreground mt-1">{message}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {channels.includes('email') && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <MailIcon className="h-4 w-4 text-blue-500" />
              Email Preview
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            <div className="bg-slate-50 dark:bg-zinc-900/50 rounded-lg p-3">
              <p className="text-xs text-muted-foreground">Subject:</p>
              <p className="font-medium text-sm">{title}</p>
              <hr className="my-2 border-border/30" />
              <p className="text-sm">{message}</p>
            </div>
          </CardContent>
        </Card>
      )}

      {channels.includes('slack') && (
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md">
          <CardHeader className="pb-2">
            <CardTitle className="text-sm flex items-center gap-2">
              <MessageSquareIcon className="h-4 w-4 text-purple-500" />
              Slack Preview
            </CardTitle>
          </CardHeader>
          <CardContent className="pb-4">
            <div className="bg-slate-50 dark:bg-zinc-900/50 rounded-lg p-3 border-l-4 border-purple-400">
              <p className="font-bold text-sm">{title}</p>
              <p className="text-sm mt-1">{message}</p>
              <p className="text-xs text-muted-foreground mt-2">
                via MeepleAI Admin
              </p>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/admin/notifications/NotificationPreview.tsx
git commit -m "feat(notifications): add NotificationPreview component"
```

### Task 13: Create the compose notification page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/notifications/compose/page.tsx`

- [ ] **Step 1: Create the page**

```tsx
/**
 * Admin Compose Notification Page
 *
 * Allows admins to send manual notifications to users
 * via selected channels (In-App, Email, Slack).
 */

'use client';

import { useState } from 'react';

import { useMutation } from '@tanstack/react-query';
import { SendIcon } from 'lucide-react';
import { toast } from 'sonner';

import { ChannelSelector } from '@/components/admin/notifications/ChannelSelector';
import { NotificationPreview } from '@/components/admin/notifications/NotificationPreview';
import {
  RecipientSelector,
  type RecipientSelection,
} from '@/components/admin/notifications/RecipientSelector';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { SendManualNotificationRequest } from '@/lib/api/schemas/adminNotifications.schemas';

export default function ComposeNotificationPage() {
  const [title, setTitle] = useState('');
  const [message, setMessage] = useState('');
  const [channels, setChannels] = useState<string[]>(['inapp']);
  const [recipients, setRecipients] = useState<RecipientSelection>({ type: 'all' });

  const sendMutation = useMutation({
    mutationFn: (request: SendManualNotificationRequest) =>
      api.adminNotifications.sendManualNotification(request),
    onSuccess: data => {
      toast.success(
        `Notification sent to ${data.dispatched} recipient${data.dispatched !== 1 ? 's' : ''}` +
          (data.skipped > 0 ? ` (${data.skipped} skipped)` : '')
      );
      // Reset form
      setTitle('');
      setMessage('');
      setChannels(['inapp']);
      setRecipients({ type: 'all' });
    },
    onError: (err: unknown) => {
      toast.error(err instanceof Error ? err.message : 'Failed to send notification');
    },
  });

  function handleSend() {
    if (!title.trim() || !message.trim() || channels.length === 0) {
      toast.error('Please fill in all required fields');
      return;
    }

    const request: SendManualNotificationRequest = {
      title: title.trim(),
      message: message.trim(),
      channels: channels as ('inapp' | 'email' | 'slack')[],
      recipientType: recipients.type,
      recipientRole: recipients.role,
      recipientUserIds: recipients.userIds,
    };

    sendMutation.mutate(request);
  }

  const isValid = title.trim().length > 0 && message.trim().length > 0 && channels.length > 0;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Compose Notification
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Send a manual notification to users via selected channels.
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
        {/* Form - 3 columns */}
        <div className="lg:col-span-3 space-y-6">
          {/* Channels */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Channels</CardTitle>
            </CardHeader>
            <CardContent>
              <ChannelSelector selected={channels} onChange={setChannels} />
            </CardContent>
          </Card>

          {/* Recipients */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Recipients</CardTitle>
            </CardHeader>
            <CardContent>
              <RecipientSelector value={recipients} onChange={setRecipients} />
            </CardContent>
          </Card>

          {/* Message */}
          <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
            <CardHeader>
              <CardTitle className="text-base">Message</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <Label className="text-sm font-medium">Title</Label>
                <Input
                  className="mt-1.5 bg-white/70 dark:bg-zinc-800/70"
                  placeholder="Notification title"
                  value={title}
                  onChange={e => setTitle(e.target.value)}
                  maxLength={200}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {title.length}/200
                </p>
              </div>
              <div>
                <Label className="text-sm font-medium">Body</Label>
                <Textarea
                  className="mt-1.5 min-h-[120px] bg-white/70 dark:bg-zinc-800/70"
                  placeholder="Write your notification message..."
                  value={message}
                  onChange={e => setMessage(e.target.value)}
                  maxLength={2000}
                />
                <p className="text-xs text-muted-foreground mt-1">
                  {message.length}/2000
                </p>
              </div>
            </CardContent>
          </Card>

          {/* Send Button */}
          <Button
            size="lg"
            className="w-full gap-2"
            onClick={handleSend}
            disabled={!isValid || sendMutation.isPending}
          >
            <SendIcon className="h-4 w-4" />
            {sendMutation.isPending ? 'Sending...' : 'Send Notification'}
          </Button>
        </div>

        {/* Preview - 2 columns */}
        <div className="lg:col-span-2">
          <div className="sticky top-4">
            <h3 className="font-quicksand text-sm font-semibold text-muted-foreground mb-3">
              PREVIEW
            </h3>
            <NotificationPreview
              title={title}
              message={message}
              channels={channels}
            />
          </div>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Verify TypeScript compilation**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/notifications/compose/page.tsx
git commit -m "feat(notifications): add admin compose notification page"
```

### Task 14: Add compose notification to admin sidebar navigation

**Files:**
- Modify: `apps/web/src/config/admin-dashboard-navigation.ts`

- [ ] **Step 1: Add sidebar item to the System section**

In `DASHBOARD_SECTIONS`, find the `system` section (id: 'system'). Add to the `additionalRoutes` array:

```typescript
'/admin/notifications',
```

Add to the `sidebarItems` array (after the Email item at `/admin/monitor?tab=email`):

```typescript
{
  href: '/admin/notifications/compose',
  label: 'Send Notification',
  icon: BellIcon,
  activePattern: /^\/admin\/notifications\/compose/,
},
```

Note: `BellIcon` is already imported in the file.

- [ ] **Step 2: Verify TypeScript compilation**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/config/admin-dashboard-navigation.ts
git commit -m "feat(notifications): add Compose Notification to admin sidebar"
```

---

## Chunk 7: Frontend Tests

### Task 15: Write compose page test

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/notifications/compose/__tests__/page.test.tsx`

- [ ] **Step 1: Create basic render test**

```tsx
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import ComposeNotificationPage from '../page';

function renderWithProviders() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>
      <ComposeNotificationPage />
    </QueryClientProvider>
  );
}

describe('ComposeNotificationPage', () => {
  it('renders the compose form', () => {
    renderWithProviders();
    expect(screen.getByText('Compose Notification')).toBeInTheDocument();
    expect(screen.getByText('Channels')).toBeInTheDocument();
    expect(screen.getByText('Recipients')).toBeInTheDocument();
    expect(screen.getByText('Message')).toBeInTheDocument();
  });

  it('shows In-App channel selected by default', () => {
    renderWithProviders();
    expect(screen.getByText('In-App')).toBeInTheDocument();
  });

  it('disables send button when title is empty', () => {
    renderWithProviders();
    const sendBtn = screen.getByRole('button', { name: /send notification/i });
    expect(sendBtn).toBeDisabled();
  });

  it('enables send button when title and message are filled', async () => {
    const user = userEvent.setup();
    renderWithProviders();

    const titleInput = screen.getByPlaceholderText('Notification title');
    const messageInput = screen.getByPlaceholderText('Write your notification message...');

    await user.type(titleInput, 'Test notification');
    await user.type(messageInput, 'This is a test message');

    const sendBtn = screen.getByRole('button', { name: /send notification/i });
    expect(sendBtn).toBeEnabled();
  });

  it('shows preview when typing', async () => {
    const user = userEvent.setup();
    renderWithProviders();

    const titleInput = screen.getByPlaceholderText('Notification title');
    await user.type(titleInput, 'Maintenance');

    expect(screen.getByText('Maintenance')).toBeInTheDocument();
    expect(screen.getByText('In-App Preview')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests**

Run: `cd apps/web && pnpm vitest run src/app/admin/\\(dashboard\\)/notifications/compose/__tests__/page.test.tsx`
Expected: All passed

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/notifications/compose/__tests__/page.test.tsx
git commit -m "test(notifications): add compose notification page tests"
```

---

## Chunk 8: Final Integration & Verification

### Task 16: Build verification

- [ ] **Step 1: Run backend build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 2: Run backend tests for UserNotifications context**

Run: `cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=UserNotifications" --no-restore -v m`
Expected: All passed (including existing tests - no regression)

- [ ] **Step 3: Run frontend typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 4: Run frontend lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 5: Run frontend tests**

Run: `cd apps/web && pnpm test`
Expected: All passed

- [ ] **Step 6: Final commit (if any remaining changes)**

```bash
git status
# If clean, skip. Otherwise commit remaining changes.
```

---

## Implementation Notes

### What the existing infrastructure handles automatically

These features work **out of the box** because the `NotificationDispatcher` already:

1. **Creates in-app notifications** for every dispatch call
2. **Checks user preferences** before email/Slack delivery
3. **Enqueues emails** via `EmailProcessorJob` (30s poll, exponential backoff retry)
4. **Enqueues Slack DMs** via `SlackNotificationProcessorJob` (10s poll, rate limiting)
5. **Sends to team channels** based on `SlackNotificationConfiguration.TeamChannels` config

### Configuration needed (ops, not code)

To route `admin_access_request_created` and `admin_pdf_processing_started` to a Slack team channel, add the types to the Slack team channel configuration via the existing admin UI at `/admin/slack/team-channels` or in `appsettings.json`:

```json
{
  "Slack": {
    "TeamChannels": {
      "T_MEEPLEAI": {
        "Channel": "#meepleai-admin",
        "WebhookUrl": "https://hooks.slack.com/...",
        "Types": [
          "admin_access_request_created",
          "admin_pdf_processing_started",
          "admin_manual_notification"
        ]
      }
    }
  }
}
```

### What was intentionally deferred

1. **Slack interactive buttons** (Quick Approve from Slack) - requires additional `HandleSlackInteractionCommand` routing, separate issue
2. **PDF processing thread replies** (completion reply to original message) - requires `slack_thread_ts` storage in Notification metadata, separate issue
3. **Notification audit log** - manual sends should be logged to the AuditLog bounded context, separate issue
4. **User search in RecipientSelector** - currently accepts UUID text input; enhance with user search autocomplete in future iteration
