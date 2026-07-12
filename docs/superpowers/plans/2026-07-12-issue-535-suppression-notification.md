# #535 ME-M3.3 Admin Suppression Notification — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** When a MechanicCard is suppressed, notify all admins in-app (always) with an opt-in per-admin email, including game title + reason + a deep-link.

**Architecture:** The existing `MechanicCardSuppressedEvent` (#534) is handled by a new `DomainEventHandlerBase<MechanicCardSuppressedEvent>` in the UserNotifications BC that fans out `INotificationDispatcher.DispatchAsync` to every admin. A new `NotificationType.AdminMechanicCardSuppressed` is wired through the dispatcher's title/severity/slack/email switches. A new per-user preference `EmailOnCardSuppressed` (default false) gates the email channel and is settable via a dedicated command + endpoint.

**Tech Stack:** .NET 9, MediatR (`ICommand`/`ICommandHandler`, `INotificationHandler`), EF Core + PostgreSQL, xUnit + Testcontainers.

**Spec:** `docs/superpowers/specs/2026-07-12-issue-535-suppression-notification.md`

## Global Constraints
- Event handler auto-discovered by MediatR (no manual DI). Cross-BC references are same-assembly (`internal` OK).
- In-app notification is created by `DispatchAsync` regardless of prefs (AC-2). Email is gated by `IsEmailEnabledForType` → `prefs.EmailOnCardSuppressed`; `preferences == null` still sends email (existing dispatcher behavior).
- Dedup: every dispatched `NotificationMessage` sets `SourceEventId = domainEvent.EventId`.
- Admins resolved via `IUserRepository.GetAdminUsersAsync()` (includes admin + superadmin).
- DeepLinkPath = `/admin/knowledge-base/mechanic-extractor/dashboard` (metrics #532 / re-process #534 routes don't exist yet).
- Backend test path `apps/api/tests/Api.Tests`; run with explicit csproj + kill testhost first.

---

### Task 1: `NotificationType.AdminMechanicCardSuppressed` + dispatcher wiring

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Services/NotificationDispatcher.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Slack/AdminAlertSlackBuilder.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/NotificationTypeCardSuppressedTests.cs`

**Interfaces:**
- Produces: `NotificationType.AdminMechanicCardSuppressed` (value `"admin_mechanic_card_suppressed"`); `IsEmailEnabledForType` returns `prefs.EmailOnCardSuppressed` for it (consumed by Task 2's pref).

- [ ] **Step 1: Write the failing test**

`NotificationTypeCardSuppressedTests.cs`:
```csharp
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationTypeCardSuppressedTests
{
    [Fact]
    public void FromString_ParsesAdminMechanicCardSuppressed()
    {
        NotificationType.FromString("admin_mechanic_card_suppressed")
            .Should().Be(NotificationType.AdminMechanicCardSuppressed);
        NotificationType.AdminMechanicCardSuppressed.Value.Should().Be("admin_mechanic_card_suppressed");
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (member/parse missing):
```bash
cd apps/api/src/Api
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~NotificationTypeCardSuppressedTests"
```

- [ ] **Step 3: Add the type + FromString case**

In `NotificationType.cs`, after the last `Admin*` member (near `AdminPdfProcessingStarted`):
```csharp
    public static readonly NotificationType AdminMechanicCardSuppressed = new("admin_mechanic_card_suppressed");
```
In `FromString(...)` switch, add:
```csharp
            "admin_mechanic_card_suppressed" => AdminMechanicCardSuppressed,
```

- [ ] **Step 4: Wire the dispatcher (NotificationDispatcher.cs)**

`IsEmailEnabledForType` — before the final `return true;`:
```csharp
        if (type == NotificationType.AdminMechanicCardSuppressed)
            return prefs.EmailOnCardSuppressed;
```
`ResolveTitle` — in the `// Admin types` block, before `return "Notifica MeepleAI";`:
```csharp
        if (type == NotificationType.AdminMechanicCardSuppressed) return "[Admin] Scheda Meccanica Soppressa";
```
`ResolveSeverity` — add to the `Warning` block's OR chain:
```csharp
            || type == NotificationType.AdminMechanicCardSuppressed
```
`IsSlackEnabledForType` — add to the admin-types exclusion OR chain (returns false):
```csharp
            || type == NotificationType.AdminMechanicCardSuppressed
```

- [ ] **Step 5: Wire AdminAlertSlackBuilder.CanHandle**

In `AdminAlertSlackBuilder.cs` `CanHandle` OR-chain, add:
```csharp
            || type == NotificationType.AdminMechanicCardSuppressed
```

> `prefs.EmailOnCardSuppressed` does not exist until Task 2 — Task 1 will not compile standalone. Implement Task 1 + Task 2 together, or add the `EmailOnCardSuppressed` property (Task 2 Step 3) first. Recommended: do Task 2's aggregate/entity property before compiling Task 1.

- [ ] **Step 6: Run — expect PASS** (after Task 2 property exists):
```bash
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~NotificationTypeCardSuppressedTests"
```

- [ ] **Step 7: Commit**
```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Domain/ValueObjects/NotificationType.cs apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/NotificationTypeCardSuppressedTests.cs
git commit -m "feat(user-notifications): #535 AdminMechanicCardSuppressed type + dispatcher wiring"
```

---

### Task 2: `EmailOnCardSuppressed` preference (persisted, default false)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Domain/Aggregates/NotificationPreferences.cs`
- Modify: `apps/api/src/Api/Infrastructure/Entities/UserNotifications/NotificationPreferencesEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/EntityConfigurations/UserNotifications/NotificationPreferencesEntityConfiguration.cs`
- Modify: `apps/api/src/Api/BoundedContexts/UserNotifications/Infrastructure/Persistence/NotificationPreferencesRepository.cs`
- Create: migration via `dotnet ef migrations add AddEmailOnCardSuppressedPreference`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/NotificationPreferencesCardSuppressionTests.cs`

**Interfaces:**
- Produces: `NotificationPreferences.EmailOnCardSuppressed` (bool, default false); `NotificationPreferences.UpdateCardSuppressionEmailPreference(bool email)`; `Reconstitute(..., bool emailOnCardSuppressed = false)`.

- [ ] **Step 1: Write the failing aggregate test**

`NotificationPreferencesCardSuppressionTests.cs`:
```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class NotificationPreferencesCardSuppressionTests
{
    [Fact]
    public void New_DefaultsEmailOnCardSuppressed_False()
    {
        new NotificationPreferences(Guid.NewGuid()).EmailOnCardSuppressed.Should().BeFalse();
    }

    [Fact]
    public void UpdateCardSuppressionEmailPreference_SetsFlag()
    {
        var prefs = new NotificationPreferences(Guid.NewGuid());
        prefs.UpdateCardSuppressionEmailPreference(true);
        prefs.EmailOnCardSuppressed.Should().BeTrue();
        prefs.UpdateCardSuppressionEmailPreference(false);
        prefs.EmailOnCardSuppressed.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (property/method missing).

- [ ] **Step 3: Aggregate property + method + Reconstitute param**

In `NotificationPreferences.cs`, add the property near the email bools:
```csharp
    /// <summary>#535: opt-in email when one of the admin's games has a mechanic card suppressed. Default off.</summary>
    public bool EmailOnCardSuppressed { get; private set; }
```
Add the update method near the other `UpdateXxxPreferences` methods:
```csharp
    /// <summary>#535: toggle the per-admin card-suppression email opt-in.</summary>
    public void UpdateCardSuppressionEmailPreference(bool email)
    {
        EmailOnCardSuppressed = email;
    }
```
In the `Reconstitute(...)` factory signature, append the last parameter (after `quietHoursEnd`):
```csharp
        TimeOnly? quietHoursEnd = null,
        bool emailOnCardSuppressed = false
```
and set it in the returned instance body: `EmailOnCardSuppressed = emailOnCardSuppressed,`.

> Confirm the exact tail of the `Reconstitute` parameter list + object initializer before editing; append the new param LAST with a default so existing callers stay valid.

- [ ] **Step 4: Entity + config**

`NotificationPreferencesEntity.cs` — add near the email bools:
```csharp
    public bool EmailOnCardSuppressed { get; set; }
```
`NotificationPreferencesEntityConfiguration.cs` — add near the email property configs:
```csharp
        builder.Property(e => e.EmailOnCardSuppressed).IsRequired().HasDefaultValue(false);
```

- [ ] **Step 5: Repo mapper both directions**

`NotificationPreferencesRepository.cs` `MapToDomain` — append `entity.EmailOnCardSuppressed` as the LAST argument of the `Reconstitute(...)` call (after `entity.QuietHoursEnd`).
`MapToPersistence` — add to the entity initializer:
```csharp
            EmailOnCardSuppressed = domain.EmailOnCardSuppressed,
```

- [ ] **Step 6: Migration**
```bash
cd apps/api/src/Api
dotnet build
dotnet ef migrations add AddEmailOnCardSuppressedPreference --no-build
```
Verify the generated `Up()` adds a single `AddColumn<bool>(... "notification_preferences" ... defaultValue: false)` and `Down()` drops it. If `Up()` is empty (stale build), delete the `.cs`+`.Designer.cs`, `dotnet build`, re-run with build.

- [ ] **Step 7: Run — expect PASS**:
```bash
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~NotificationPreferencesCardSuppressionTests"
```

- [ ] **Step 8: Commit**
```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications apps/api/src/Api/Infrastructure apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Domain/NotificationPreferencesCardSuppressionTests.cs
git commit -m "feat(user-notifications): #535 EmailOnCardSuppressed preference (default off)"
```

---

### Task 3: Settable via API — command + endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/UpdateCardSuppressionEmailPreferenceCommand.cs`
- Create: `.../UpdateCardSuppressionEmailPreferenceCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/NotificationPreferencesEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/UpdateCardSuppressionEmailPreferenceHandlerTests.cs`

**Interfaces:**
- Consumes: `INotificationPreferencesRepository` (`GetByUserIdAsync`, `AddAsync`, `UpdateAsync`), `NotificationPreferences.UpdateCardSuppressionEmailPreference` (Task 2).
- Produces: `record UpdateCardSuppressionEmailPreferenceCommand(Guid UserId, bool EmailOnCardSuppressed) : ICommand`.

- [ ] **Step 1: Write the failing integration test**

`UpdateCardSuppressionEmailPreferenceHandlerTests.cs`:
```csharp
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class UpdateCardSuppressionEmailPreferenceHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public UpdateCardSuppressionEmailPreferenceHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me535_pref_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Command_PersistsFlag()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new UpdateCardSuppressionEmailPreferenceCommand(_userId, true));
        }
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.Set<NotificationPreferencesEntity>().AsNoTracking().SingleAsync(p => p.UserId == _userId);
            row.EmailOnCardSuppressed.Should().BeTrue();
        }
    }
}
```

- [ ] **Step 2: Run — expect FAIL** (command type missing).

- [ ] **Step 3: Command + handler**

`UpdateCardSuppressionEmailPreferenceCommand.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

/// <summary>#535: sets the calling admin's opt-in for card-suppression emails.</summary>
internal record UpdateCardSuppressionEmailPreferenceCommand(Guid UserId, bool EmailOnCardSuppressed) : ICommand;
```
`UpdateCardSuppressionEmailPreferenceCommandHandler.cs` (mirror `UpdateNotificationPreferencesCommandHandler`):
```csharp
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

internal sealed class UpdateCardSuppressionEmailPreferenceCommandHandler
    : ICommandHandler<UpdateCardSuppressionEmailPreferenceCommand>
{
    private readonly INotificationPreferencesRepository _repository;

    public UpdateCardSuppressionEmailPreferenceCommandHandler(INotificationPreferencesRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task Handle(UpdateCardSuppressionEmailPreferenceCommand command, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);
        var prefs = existing ?? new NotificationPreferences(command.UserId);
        prefs.UpdateCardSuppressionEmailPreference(command.EmailOnCardSuppressed);

        if (existing is null)
            await _repository.AddAsync(prefs, cancellationToken).ConfigureAwait(false);
        else
            await _repository.UpdateAsync(prefs, cancellationToken).ConfigureAwait(false);
    }
}
```

- [ ] **Step 4: Endpoint**

In `NotificationPreferencesEndpoints.cs`, after the existing `PUT /notifications/preferences` mapping, add (mirror its auth + user-id resolution):
```csharp
        group.MapPut("/notifications/preferences/card-suppression", async (
            UpdateCardSuppressionEmailPreferenceRequest body,
            HttpContext context,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userId = /* resolve from context, mirror the sibling endpoint */;
            await mediator.Send(new UpdateCardSuppressionEmailPreferenceCommand(userId, body.EmailOnCardSuppressed), ct);
            return Results.NoContent();
        })
        .RequireAuthorization()
        .WithName("UpdateCardSuppressionEmailPreference");
```
with `internal record UpdateCardSuppressionEmailPreferenceRequest(bool EmailOnCardSuppressed);`.

> Open `NotificationPreferencesEndpoints.cs` and mirror EXACTLY how the existing `PUT /notifications/preferences` resolves the user id (it may bind `UpdateNotificationPreferencesCommand` directly with UserId in the body, or read a claim). Match that pattern so auth is consistent. If the sibling binds the command directly with UserId from the body, do the same (bind `UpdateCardSuppressionEmailPreferenceCommand` directly) and drop the request record.

- [ ] **Step 5: Run — expect PASS**:
```bash
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~UpdateCardSuppressionEmailPreferenceHandlerTests"
```

- [ ] **Step 6: Commit**
```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands apps/api/src/Api/Routing/NotificationPreferencesEndpoints.cs apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/UpdateCardSuppressionEmailPreferenceHandlerTests.cs
git commit -m "feat(user-notifications): #535 settable card-suppression email preference API"
```

---

### Task 4: Event handler — admin fan-out (AC-2 + AC-3 + AC-4)

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers/MechanicCardSuppressedAdminNotificationHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/MechanicCardSuppressedAdminNotificationHandlerTests.cs`

**Interfaces:**
- Consumes: `MechanicCardSuppressedEvent` (SharedGameCatalog), `IUserRepository.GetAdminUsersAsync`, `ISharedGameRepository.GetByIdAsync`, `INotificationDispatcher.DispatchAsync`, `NotificationType.AdminMechanicCardSuppressed` (Task 1), `GenericPayload`.

- [ ] **Step 1: Write the failing integration test**

`MechanicCardSuppressedAdminNotificationHandlerTests.cs`. Seeds two admins (1 admin + 1 superadmin) + a published card (reuse the #534 seed helper `MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync` for the card/game), then suppresses the card through the real repo path so the event dispatches, and asserts each admin has an in-app Notification of the new type.
```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure;

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class MechanicCardSuppressedAdminNotificationHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _seedUserId;

    public MechanicCardSuppressedAdminNotificationHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me535_handler_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_seedUserId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task Suppression_NotifiesAllAdmins_InApp()
    {
        Guid adminId, superAdminId, cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (adminId, _) = await TestSessionHelper.CreateAdminSessionAsync(db, Guid.NewGuid());
            (superAdminId, _) = await TestSessionHelper.CreateSuperAdminSessionAsync(db, Guid.NewGuid());
            cardId = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _seedUserId, negatives: 0, positives: 0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var card = await repo.GetByIdIgnoringFiltersAsync(cardId);
            card!.Suppress(_seedUserId, "manual takedown for the admin-notification integration test", DateTime.UtcNow);
            repo.Update(card);
            await uow.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var typeValue = NotificationType.AdminMechanicCardSuppressed.Value;
            foreach (var id in new[] { adminId, superAdminId })
            {
                (await db.Set<NotificationEntity>().CountAsync(n => n.UserId == id && n.Type == typeValue))
                    .Should().Be(1, $"admin {id} should get exactly one in-app suppression notification");
            }
        }
    }
}
```

> Verify the helper names: `TestSessionHelper.CreateAdminSessionAsync` / `CreateSuperAdminSessionAsync` (the #533 test used `CreateUserSessionAsync`; check `TestSessionHelper` for the role-specific factories — it exposes `CreateSessionAsync(db, "Admin"/"SuperAdmin", ...)`). Verify `NotificationEntity` name/namespace + its `Type` column is the string value. Adjust the count query if notifications are read via a DbSet property (e.g. `db.Notifications`).

- [ ] **Step 2: Run — expect FAIL** (handler missing → 0 notifications).

- [ ] **Step 3: Implement the handler**

`MechanicCardSuppressedAdminNotificationHandler.cs`:
```csharp
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Services;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.EventHandlers;

using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.EventHandlers;

/// <summary>
/// #535 ME-M3.3: fans out an admin in-app notification (email opt-in per admin) when a mechanic card is
/// suppressed. Auto-discovered by MediatR; dispatched post-commit via the domain-event outbox.
/// </summary>
internal sealed class MechanicCardSuppressedAdminNotificationHandler
    : DomainEventHandlerBase<MechanicCardSuppressedEvent>
{
    private const string DeepLink = "/admin/knowledge-base/mechanic-extractor/dashboard";

    private readonly INotificationDispatcher _dispatcher;
    private readonly IUserRepository _userRepository;
    private readonly ISharedGameRepository _sharedGameRepository;

    public MechanicCardSuppressedAdminNotificationHandler(
        MeepleAiDbContext dbContext,
        INotificationDispatcher dispatcher,
        IUserRepository userRepository,
        ISharedGameRepository sharedGameRepository,
        ILogger<MechanicCardSuppressedAdminNotificationHandler> logger)
        : base(dbContext, logger)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
    }

    protected override async Task HandleEventAsync(MechanicCardSuppressedEvent domainEvent, CancellationToken cancellationToken)
    {
        var game = await _sharedGameRepository.GetByIdAsync(domainEvent.SharedGameId, cancellationToken).ConfigureAwait(false);
        var title = string.IsNullOrWhiteSpace(game?.Title) ? "un gioco" : game!.Title;

        var admins = await _userRepository.GetAdminUsersAsync(cancellationToken).ConfigureAwait(false);
        foreach (var admin in admins)
        {
            await _dispatcher.DispatchAsync(new NotificationMessage
            {
                Type = NotificationType.AdminMechanicCardSuppressed,
                RecipientUserId = admin.Id,
                Payload = new GenericPayload(
                    "[Admin] Scheda Meccanica Soppressa",
                    $"La scheda meccaniche di «{title}» è stata soppressa. Motivo: {domainEvent.Reason}"),
                DeepLinkPath = DeepLink,
                SourceEventId = domainEvent.EventId
            }, cancellationToken).ConfigureAwait(false);
        }
    }
}
```

> Verify: `GenericPayload` namespace (`Api.BoundedContexts.UserNotifications.Domain.ValueObjects`), `NotificationMessage` namespace (`...Application.Services`), `IUserRepository` namespace + that domain `User` exposes `.Id`, `SharedGame` exposes `.Title`, and `DomainEventHandlerBase` ctor `(MeepleAiDbContext, ILogger)`. Base class handles logging + rethrow; do not add a top-level try/catch (mirror `SharedGameSubmittedForApprovalNotificationHandler`).

- [ ] **Step 4: Run — expect PASS**:
```bash
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~MechanicCardSuppressedAdminNotificationHandlerTests"
```

- [ ] **Step 5: Full build + #535 suite green**:
```bash
dotnet build
dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~CardSuppress|FullyQualifiedName~NotificationTypeCardSuppressed|FullyQualifiedName~NotificationPreferencesCardSuppression"
```

- [ ] **Step 6: Commit**
```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/EventHandlers apps/api/tests/Api.Tests/BoundedContexts/UserNotifications/Infrastructure/MechanicCardSuppressedAdminNotificationHandlerTests.cs
git commit -m "feat(user-notifications): #535 admin fan-out notification on card suppression"
```

---

## Self-Review
- **AC-1** (event) → pre-existing (#534). ✓
- **AC-2** (in-app all admins) → Task 4 handler + `GetAdminUsersAsync` (incl superadmin). ✓
- **AC-3** (email opt-in) → Task 1 dispatcher `IsEmailEnabledForType` + Task 2 pref (default false) + Task 3 settable API. ✓ (FE checkbox = follow-up.)
- **AC-4** (game + reason + link) → Task 4 payload body + DeepLinkPath. ✓ (metrics/re-process routes don't exist → dashboard.)
- **Placeholders**: two flagged verification points (endpoint user-id resolution; test-helper role factories + NotificationEntity DbSet) — concrete "open file X, mirror pattern Y" instructions, resolved during execution.
- **Type consistency**: `AdminMechanicCardSuppressed`, `EmailOnCardSuppressed`, `UpdateCardSuppressionEmailPreference`, `UpdateCardSuppressionEmailPreferenceCommand` used identically across tasks.
