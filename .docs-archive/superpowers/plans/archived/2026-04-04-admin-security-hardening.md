# Admin Security Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare 4 security hardening identificati dal spec-panel: reason obbligatoria + guard SuperAdmin per impersonation (ADM-001), privilege escalation guards nei role change commands (ADM-002), endpoint ad alto rischio accessibili solo a SuperAdmin (ADM-003), limiti batch per ruolo nelle bulk operations (ADM-004).

**Architecture:** Tutte le modifiche sono backend-only nel bounded context Administration e Authentication. Nessuna migration DB necessaria. I comandi vengono estesi con nuovi campi e regole di validazione. L'interfaccia `IUserRepository` viene estesa con `CountByRoleAsync`. Due endpoint vengono promossi da `RequireAdminSession` a `RequireSuperAdminSession`.

**Tech Stack:** .NET 9, C#, MediatR, FluentValidation, xUnit, Moq, FluentAssertions

---

## Branch Setup

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout main-dev && git pull
git checkout -b feature/admin-security-hardening
git config branch.feature/admin-security-hardening.parent main-dev
```

---

## File Map

| File | Operazione | Task |
|------|-----------|------|
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs` | Modify — add `Reason` field | T1 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandler.cs` | Modify — extend privilege check + include reason in audit log | T1 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/EndImpersonationCommandHandler.cs` | Modify — add `EndedAt` timestamp to audit log | T1 |
| `apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs` | Modify — add request body, use `RequireSuperAdminSession`, pass reason | T1 |
| `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/IUserRepository.cs` | Modify — add `CountByRoleAsync` | T2 |
| `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs` | Modify — implement `CountByRoleAsync` | T2 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs` | Modify — add `AdminRole` field | T3 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandler.cs` | Modify — add role hierarchy guard + SuperAdmin minimum count | T3 |
| `apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs` | Modify — pass `session.User.Role` to ChangeUserRoleCommand (2nd edit) | T3 |
| `apps/api/src/Api/Routing/Admin/AdminUserTierEndpoints.cs` | Modify — pass `session.User.Role` to ChangeUserRoleCommand | T3 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkRoleChangeCommandHandler.cs` | Modify — role hierarchy guard + SuperAdmin minimum + role-based size limit | T4 |
| `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkPasswordResetCommandHandler.cs` | Modify — role-based size limit | T5 |
| `apps/api/src/Api/Routing/Admin/AdminUserBulkEndpoints.cs` | Modify — bulk password reset usa `RequireSuperAdminSession` | T5 |

**Test files creati:**
| File | Task |
|------|------|
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandlerTests.cs` | T1 |
| `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Persistence/UserRepositoryCountByRoleTests.cs` | T2 |
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandlerTests.cs` | T3 |
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkRoleChangeSecurityTests.cs` | T4 |
| `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkPasswordResetSecurityTests.cs` | T5 |

---

## Task 1: [ADM-001 + ADM-003] Impersonation — Reason Obbligatoria + Protezione SuperAdmin

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/EndImpersonationCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandlerTests.cs`

- [ ] **Step 1: Aggiorna ImpersonateUserCommand — aggiungi campo Reason**

Sostituisci il contenuto di `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to impersonate user for debugging (Issue #2890).
/// HIGH SECURITY RISK: Creates full user session for admin debugging.
/// Note: Audit logging is handled manually in the handler (richer context with separate admin/target entries).
/// ADM-001: Reason is mandatory for audit trail compliance.
/// </summary>
internal record ImpersonateUserCommand(
    Guid TargetUserId,
    Guid AdminUserId,
    string Reason
) : ICommand<ImpersonateUserResponseDto>;
```

- [ ] **Step 2: Aggiorna ImpersonateUserCommandHandler — estendi privilege check e audit log**

Nel file `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandler.cs`, sostituisci il blocco che previene l'impersonazione di altri admin (righe con "Prevent impersonation of other admins") con il seguente blocco più completo, e aggiorna i `details` del primo audit log per includere `reason`:

```csharp
        // Prevent impersonation of admin or superadmin users (ADM-001 + ADM-002)
        // Extends original check from Issue #3349 to cover SuperAdmin as well
        if (string.Equals(targetUser.Role.Value, "admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(targetUser.Role.Value, "superadmin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "⚠️ SECURITY: Admin {AdminId} attempted to impersonate privileged user {TargetUserId} (role: {Role})",
                command.AdminUserId, command.TargetUserId, targetUser.Role.Value);
            throw new ForbiddenException("Cannot impersonate admin or SuperAdmin users");
        }
```

Nel blocco `adminAuditLog` (creazione del primo audit log), aggiorna il campo `details` per includere `reason`:

```csharp
        var adminAuditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "impersonate_user_started",
            resource: "User",
            result: "success",
            resourceId: command.TargetUserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                targetUserId = command.TargetUserId,
                targetEmail = targetUser.Email.Value,
                reason = command.Reason,
                sessionToken = sessionResponse.SessionToken[..Math.Min(16, sessionResponse.SessionToken.Length)] + "...",
                expiresAt = sessionResponse.ExpiresAt
            }),
            ipAddress: "admin-action"
        );
```

Aggiungi l'using mancante in cima se non già presente:
```csharp
using Api.Middleware.Exceptions;
```

- [ ] **Step 3: Aggiorna EndImpersonationCommandHandler — aggiungi EndedAt all'audit log**

Nel file `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/EndImpersonationCommandHandler.cs`, sostituisci il blocco `auditLog` (creazione dell'audit log finale):

```csharp
        // Create audit log for ending impersonation (ADM-001: include EndedAt for duration tracking)
        var auditLog = new Api.BoundedContexts.Administration.Domain.Entities.AuditLog(
            id: Guid.NewGuid(),
            userId: command.AdminUserId,
            action: "impersonate_user_ended",
            resource: "User",
            result: "success",
            resourceId: impersonatedUserId.ToString(),
            details: System.Text.Json.JsonSerializer.Serialize(new
            {
                sessionId = command.SessionId,
                impersonatedUserId,
                endedByAdminId = command.AdminUserId,
                endedAt = DateTime.UtcNow.ToString("O")
            }),
            ipAddress: "admin-action"
        );
```

- [ ] **Step 4: Aggiorna endpoint impersonation — aggiungi body con Reason e RequireSuperAdminSession**

Nel file `apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs`:

**4a.** Aggiorna la definizione del metodo `MapUserImpersonateEndpoint` per richiedere SuperAdmin e accettare il body:

```csharp
    private static void MapUserImpersonateEndpoint(RouteGroupBuilder group)
    {
        // Impersonate user (SuperAdmin only) - Issue #2890, ADM-001 ADM-003
        group.MapPost("/admin/users/{userId:guid}/impersonate", HandleImpersonateUser)
            .RequireSuperAdminSession()
            .WithName("ImpersonateUser")
            .WithTags("Admin", "Users", "Debug")
            .WithSummary("Impersonate user for debugging (SuperAdmin only)")
            .WithDescription(@"Create session as another user for debugging purposes.

**Authorization**: SuperAdmin session required (ADM-003: elevated from Admin to SuperAdmin)

**Security**:
- ⚠️ HIGH RISK: Creates full user session
- Logs impersonation in audit trail with mandatory reason
- Session marked as impersonated
- Limited duration (24 hours)
- Cannot impersonate admin or SuperAdmin users

**Behavior**:
- Creates new session for target user
- Returns session token
- Original admin session remains active
- Audit log records both admin and impersonated user with reason

**Issue**: #2890 - User Detail Modal/Page")
            .Produces<ImpersonateUserResponseDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound);
    }
```

**4b.** Aggiungi il record request body dopo il record `EndImpersonationRequest` esistente (intorno alla riga 35):

```csharp
/// <summary>
/// Request payload for impersonating a user (ADM-001: mandatory reason).
/// </summary>
internal record ImpersonateUserRequest(string Reason);
```

**4c.** Sostituisci il metodo `HandleImpersonateUser` con la versione aggiornata:

```csharp
    private static async Task<IResult> HandleImpersonateUser(
        Guid userId,
        ImpersonateUserRequest request,
        HttpContext context,
        IMediator mediator,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireSuperAdminSession();
        if (!authorized) return error!;

        if (string.IsNullOrWhiteSpace(request?.Reason) || request.Reason.Trim().Length < 10)
            return Results.BadRequest(new { error = "Impersonation reason is required (minimum 10 characters)" });

        logger.LogWarning("⚠️ SuperAdmin {AdminId} attempting to impersonate user {UserId}, reason: {Reason}",
            session!.User!.Id, userId, request.Reason);

        try
        {
            var command = new ImpersonateUserCommand(
                userId,
                session.User.Id,
                request.Reason.Trim());

            var result = await mediator.Send(command, ct).ConfigureAwait(false);

            logger.LogWarning("⚠️ Impersonation successful: SuperAdmin {AdminId} → User {UserId}",
                session.User.Id, userId);

            return Results.Ok(result);
        }
        catch (ForbiddenException ex)
        {
            logger.LogWarning(ex, "Forbidden impersonation attempt by {AdminId} on user {UserId}", session.User.Id, userId);
            return Results.Json(new { error = "forbidden", message = ex.Message }, statusCode: StatusCodes.Status403Forbidden);
        }
        catch (NotFoundException ex)
        {
            logger.LogWarning(ex, "User {UserId} not found for impersonation", userId);
            return Results.NotFound(new { error = "User not found" });
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain error impersonating user {UserId}", userId);
            return Results.BadRequest(new { error = "domain_error", message = ex.Message });
        }
    }
```

Verifica che `using Api.Middleware.Exceptions;` sia presente in cima al file.

- [ ] **Step 5: Scrivi test per ADM-001**

Crea `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class ImpersonateUserCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IAuditLogRepository> _mockAuditLogRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<ImpersonateUserCommandHandler>> _mockLogger;
    private readonly ImpersonateUserCommandHandler _handler;

    public ImpersonateUserCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockAuditLogRepository = new Mock<IAuditLogRepository>();
        _mockMediator = new Mock<IMediator>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<ImpersonateUserCommandHandler>>();

        _handler = new ImpersonateUserCommandHandler(
            _mockUserRepository.Object,
            _mockAuditLogRepository.Object,
            _mockMediator.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidCommand_CreatesSessionAndAuditLogs()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        const string reason = "Testing permissions for issue #1234";

        var targetUser = CreateUser(targetUserId, "user@test.com", "user");
        var adminUser = CreateUser(adminUserId, "admin@test.com", "admin");
        var sessionResponse = new CreateSessionResponseDto("token_abc123xyz", DateTime.UtcNow.AddHours(24));

        _mockUserRepository.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetUser);
        _mockUserRepository.Setup(r => r.GetByIdAsync(adminUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(adminUser);
        _mockMediator.Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sessionResponse);

        var command = new ImpersonateUserCommand(targetUserId, adminUserId, reason);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.ImpersonatedUserId.Should().Be(targetUserId);
        result.SessionToken.Should().Be("token_abc123xyz");

        // Two audit logs created: one for admin, one for target user
        _mockAuditLogRepository.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.Administration.Domain.Entities.AuditLog>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TargetIsAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var targetAdminUser = CreateUser(targetUserId, "otheradmin@test.com", "admin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetAdminUser);

        var command = new ImpersonateUserCommand(targetUserId, adminUserId, "Testing admin user behavior");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*admin*");
    }

    [Fact]
    public async Task Handle_TargetIsSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var targetSuperAdmin = CreateUser(targetUserId, "superadmin@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(targetSuperAdmin);

        var command = new ImpersonateUserCommand(targetUserId, adminUserId, "Testing superadmin permissions");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*admin*");
    }

    [Fact]
    public async Task Handle_TargetUserNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();

        _mockUserRepository.Setup(r => r.GetByIdAsync(targetUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ImpersonateUserCommand(targetUserId, adminUserId, "Debugging user issue");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Api.Middleware.Exceptions.NotFoundException>();
    }

    private static User CreateUser(Guid id, string email, string role)
    {
        var user = User.Create(
            Email.Create(email),
            PasswordHash.Create("hashed_password"),
            "Test User");
        // Use reflection to set Id and Role for testing
        typeof(User).GetProperty("Id")?.SetValue(user, id);
        user.UpdateRole(Role.Parse(role));
        return user;
    }
}
```

- [ ] **Step 6: Esegui i test del Task 1**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet test tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandlerTests.cs --filter "FullyQualifiedName~ImpersonateUserCommandHandlerTests" -v normal
```

Atteso: tutti i test passano.

- [ ] **Step 7: Build per verificare compilazione**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet build src/Api/Api.csproj --no-incremental 2>&1 | tail -20
```

Atteso: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit Task 1**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommand.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/EndImpersonationCommandHandler.cs
git add apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ImpersonateUserCommandHandlerTests.cs
git commit -m "feat(admin): ADM-001 + ADM-003 — impersonation reason required, SuperAdmin-only, extend audit log"
```

---

## Task 2: [ADM-002a] Repository — Aggiungi CountByRoleAsync

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/IUserRepository.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Persistence/UserRepositoryCountByRoleTests.cs`

- [ ] **Step 1: Aggiungi CountByRoleAsync a IUserRepository**

In `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/IUserRepository.cs`, aggiungi dopo `CountAdminsAsync`:

```csharp
    /// <summary>
    /// Counts users with a specific role name (case-insensitive).
    /// Used for privilege escalation guards (e.g., ensure at least one SuperAdmin remains).
    /// ADM-002: Privilege escalation prevention.
    /// </summary>
    Task<int> CountByRoleAsync(string roleName, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Implementa CountByRoleAsync in UserRepository**

In `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs`, aggiungi dopo il metodo `CountAdminsAsync` esistente:

```csharp
    public async Task<int> CountByRoleAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var roleValue = roleName.ToLowerInvariant();
        return await DbContext.Users
            .AsNoTracking()
            .CountAsync(u => u.Role == roleValue, cancellationToken)
            .ConfigureAwait(false);
    }
```

- [ ] **Step 3: Scrivi test per CountByRoleAsync**

Crea `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Persistence/UserRepositoryCountByRoleTests.cs`:

```csharp
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Persistence;

[Trait("Category", TestCategories.Unit)]
public class UserRepositoryCountByRoleTests
{
    private readonly Mock<IUserRepository> _mockRepository;

    public UserRepositoryCountByRoleTests()
    {
        _mockRepository = new Mock<IUserRepository>();
    }

    [Theory]
    [InlineData("superadmin", 2)]
    [InlineData("admin", 5)]
    [InlineData("user", 100)]
    [InlineData("editor", 0)]
    public async Task CountByRoleAsync_ReturnsCorrectCount(string role, int expectedCount)
    {
        // Arrange
        _mockRepository.Setup(r => r.CountByRoleAsync(role, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedCount);

        // Act
        var count = await _mockRepository.Object.CountByRoleAsync(role, CancellationToken.None);

        // Assert
        count.Should().Be(expectedCount);
        _mockRepository.Verify(r => r.CountByRoleAsync(role, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CountByRoleAsync_CaseInsensitive_WorksForSuperAdmin()
    {
        // Arrange — verify interface contract is case-insensitive at the repository level
        _mockRepository.Setup(r => r.CountByRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var count = await _mockRepository.Object.CountByRoleAsync("SuperAdmin", CancellationToken.None);

        // Assert
        count.Should().Be(1);
    }
}
```

- [ ] **Step 4: Build per verificare compilazione**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet build src/Api/Api.csproj --no-incremental 2>&1 | tail -10
```

Atteso: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit Task 2**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/IUserRepository.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Persistence/UserRepository.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Authentication/Persistence/UserRepositoryCountByRoleTests.cs
git commit -m "feat(admin): ADM-002a — add CountByRoleAsync to IUserRepository for privilege guards"
```

---

## Task 3: [ADM-002b] ChangeUserRoleCommand — Role Hierarchy Guard

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/Admin/AdminUserTierEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandlerTests.cs`

- [ ] **Step 1: Aggiungi AdminRole a ChangeUserRoleCommand**

Sostituisci il contenuto di `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// ADM-002: AdminRole required to enforce role hierarchy privilege checks.
/// </summary>
/// <param name="UserId">The ID of the user whose role is being changed.</param>
/// <param name="NewRole">The new role to assign.</param>
/// <param name="Reason">Optional reason for the role change.</param>
/// <param name="AdminRole">The role of the admin performing this action (from session). Used for privilege level validation.</param>
[AuditableAction("UserRoleChange", "User", Level = 1)]
internal record ChangeUserRoleCommand(
    string UserId,
    string NewRole,
    string? Reason = null,
    string AdminRole = "admin"
) : ICommand<UserDto>;
```

- [ ] **Step 2: Aggiorna ChangeUserRoleCommandHandler — aggiungi role hierarchy guard**

Sostituisci il contenuto di `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandler.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal class ChangeUserRoleCommandHandler : ICommandHandler<ChangeUserRoleCommand, UserDto>
{
    private static readonly string[] AllowedRoles = { "Admin", "Editor", "Creator", "User" };

    // ADM-002: Role hierarchy levels for privilege escalation prevention
    private static readonly Dictionary<string, int> RoleLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "user", 0 }, { "creator", 1 }, { "editor", 2 }, { "admin", 3 }, { "superadmin", 4 }
    };

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeUserRoleCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<UserDto> Handle(ChangeUserRoleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validate input before domain operations
        Guard.AgainstNullOrWhiteSpace(command.UserId, nameof(command.UserId));
        if (!Guid.TryParse(command.UserId, out var userId))
            throw new ValidationException($"Invalid UserId format: {command.UserId}");
        Guard.AgainstNullOrWhiteSpace(command.NewRole, nameof(command.NewRole));
        Guard.AgainstInvalidValue(command.NewRole, AllowedRoles, nameof(command.NewRole));

        // ADM-002: Privilege escalation check — cannot assign role >= caller's own level
        var adminLevel = RoleLevels.GetValueOrDefault(command.AdminRole, 0);
        var targetLevel = RoleLevels.GetValueOrDefault(command.NewRole, 0);

        if (targetLevel >= adminLevel)
            throw new ForbiddenException(
                $"Cannot assign role '{command.NewRole}': you can only assign roles below your own privilege level");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user == null)
            throw new DomainException($"User {command.UserId} not found");

        // ADM-002: Minimum SuperAdmin guard — prevent demoting the last SuperAdmin
        if (user.Role.Value.Equals("superadmin", StringComparison.OrdinalIgnoreCase) &&
            !command.NewRole.Equals("superadmin", StringComparison.OrdinalIgnoreCase))
        {
            var superAdminCount = await _userRepository.CountByRoleAsync(
                Role.SuperAdmin.Value, cancellationToken).ConfigureAwait(false);
            if (superAdminCount <= 1)
                throw new ForbiddenException(
                    "Cannot demote the last SuperAdmin. The system requires at least one SuperAdmin.");
        }

        var newRole = Role.Parse(command.NewRole);
        user.UpdateRole(newRole);

        // Persist updates - required because repository uses AsNoTracking
        await _userRepository.UpdateAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email.Value,
            DisplayName: user.DisplayName,
            Role: user.Role.Value,
            CreatedAt: user.CreatedAt,
            LastSeenAt: null
        );
    }
}
```

- [ ] **Step 3: Aggiorna endpoint in AdminUserActivityDetailEndpoints.cs — passa AdminRole**

Sostituisci la riga di costruzione del comando nel metodo `HandleChangeUserRole`:

```csharp
            var command = new ChangeUserRoleCommand(userId.ToString(), request.NewRole, request.Reason, session!.User!.Role);
```

(Sostituisce la riga originale: `var command = new ChangeUserRoleCommand(userId.ToString(), request.NewRole, request.Reason);`)

- [ ] **Step 4: Aggiorna endpoint in AdminUserTierEndpoints.cs — passa AdminRole**

Nel file `apps/api/src/Api/Routing/Admin/AdminUserTierEndpoints.cs`, sostituisci la riga di costruzione del comando (riga ~297):

```csharp
            var command = new ChangeUserRoleCommand(userId.ToString(), request.NewRole, request.Reason, session!.User!.Role);
```

- [ ] **Step 5: Scrivi test per ADM-002b**

Crea `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandlerTests.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class ChangeUserRoleCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly ChangeUserRoleCommandHandler _handler;

    public ChangeUserRoleCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _handler = new ChangeUserRoleCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object);
    }

    [Fact]
    public async Task Handle_AdminAssignsEditor_Succeeds()
    {
        // Arrange — Admin (level 3) assigns Editor (level 2): allowed
        var userId = Guid.NewGuid();
        var user = CreateUser(userId, "user@test.com", "user");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var command = new ChangeUserRoleCommand(userId.ToString(), "Editor", "Promotion", AdminRole: "admin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AdminAssignsAdmin_ThrowsForbiddenException()
    {
        // Arrange — Admin (level 3) tries to assign Admin (level 3): blocked (same level)
        var userId = Guid.NewGuid();

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "admin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privilege level*");
    }

    [Fact]
    public async Task Handle_EditorAssignsEditor_ThrowsForbiddenException()
    {
        // Arrange — Editor (level 2) tries to assign Editor (level 2): blocked (same level)
        var userId = Guid.NewGuid();

        var command = new ChangeUserRoleCommand(userId.ToString(), "Editor", null, AdminRole: "editor");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_DemoteLastSuperAdmin_ThrowsForbiddenException()
    {
        // Arrange — Demoting the only SuperAdmin to Admin: blocked
        var userId = Guid.NewGuid();
        var superAdminUser = CreateUser(userId, "superadmin@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superAdminUser);
        _mockUserRepository.Setup(r => r.CountByRoleAsync("superadmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1); // Only 1 SuperAdmin

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "superadmin");

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*last SuperAdmin*");
    }

    [Fact]
    public async Task Handle_DemoteOneSuperAdminWhenMultipleExist_Succeeds()
    {
        // Arrange — Demoting a SuperAdmin when 2 exist: allowed
        var userId = Guid.NewGuid();
        var superAdminUser = CreateUser(userId, "superadmin2@test.com", "superadmin");

        _mockUserRepository.Setup(r => r.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(superAdminUser);
        _mockUserRepository.Setup(r => r.CountByRoleAsync("superadmin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(2); // 2 SuperAdmins exist

        var command = new ChangeUserRoleCommand(userId.ToString(), "Admin", null, AdminRole: "superadmin");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static User CreateUser(Guid id, string email, string role)
    {
        var user = User.Create(
            Email.Create(email),
            PasswordHash.Create("hashed_password"),
            "Test User");
        typeof(User).GetProperty("Id")?.SetValue(user, id);
        user.UpdateRole(Role.Parse(role));
        return user;
    }
}
```

- [ ] **Step 6: Esegui i test del Task 3**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet test tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandlerTests.cs -v normal
```

Atteso: tutti i test passano.

- [ ] **Step 7: Build**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet build src/Api/Api.csproj --no-incremental 2>&1 | tail -10
```

Atteso: `Build succeeded. 0 Error(s)`

- [ ] **Step 8: Commit Task 3**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommand.cs
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandler.cs
git add apps/api/src/Api/Routing/Admin/AdminUserActivityDetailEndpoints.cs
git add apps/api/src/Api/Routing/Admin/AdminUserTierEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/ChangeUserRoleCommandHandlerTests.cs
git commit -m "feat(admin): ADM-002b — role hierarchy guard + last-SuperAdmin protection in ChangeUserRoleCommand"
```

---

## Task 4: [ADM-002c + ADM-004] BulkRoleChange — Role Guards + Size Limits

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkRoleChangeCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkRoleChangeSecurityTests.cs`

- [ ] **Step 1: Aggiorna BulkRoleChangeCommandHandler — aggiungi role hierarchy, SuperAdmin minimum e size limit**

Dopo le costanti esistenti (`private const int MaxBulkSize = 1000;`), aggiungi:

```csharp
    // ADM-002 + ADM-004: Role hierarchy levels and role-based batch size limits
    private static readonly Dictionary<string, int> RoleLevels = new(StringComparer.OrdinalIgnoreCase)
    {
        { "user", 0 }, { "creator", 1 }, { "editor", 2 }, { "admin", 3 }, { "superadmin", 4 }
    };

    private static readonly Dictionary<string, int> MaxBulkSizeByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        { "superadmin", 1000 },
        { "admin", 100 },
    };
```

Nel metodo `Handle`, dopo la validazione della role (`newRole = Role.Parse(command.NewRole)`) e prima del loop, aggiungi:

```csharp
        // ADM-002: Load requester to enforce privilege checks
        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken)
            .ConfigureAwait(false);
        if (requester is null)
            throw new DomainException($"Requester {command.RequesterId} not found");

        // ADM-004: Role-based batch size limit (Admin: max 100, SuperAdmin: max 1000)
        var allowedBulkSize = MaxBulkSizeByRole.GetValueOrDefault(requester.Role.Value, 100);
        if (distinctUserIds.Count > allowedBulkSize)
            throw new ForbiddenException(
                $"Bulk role change of {distinctUserIds.Count} users exceeds your role limit of {allowedBulkSize}. Contact a SuperAdmin for larger operations.");

        // ADM-002: Role hierarchy check — cannot assign role >= requester's own level
        var requesterLevel = RoleLevels.GetValueOrDefault(requester.Role.Value, 0);
        var targetLevel = RoleLevels.GetValueOrDefault(command.NewRole, 0);
        if (targetLevel >= requesterLevel)
            throw new ForbiddenException(
                $"Cannot bulk assign role '{command.NewRole}': you can only assign roles below your own privilege level");

        // ADM-002: Minimum SuperAdmin guard for bulk demotion
        if (command.NewRole.Equals("user", StringComparison.OrdinalIgnoreCase) ||
            command.NewRole.Equals("editor", StringComparison.OrdinalIgnoreCase) ||
            command.NewRole.Equals("creator", StringComparison.OrdinalIgnoreCase) ||
            command.NewRole.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            var superAdminCount = await _userRepository.CountByRoleAsync(
                Role.SuperAdmin.Value, cancellationToken).ConfigureAwait(false);
            var superAdminsInBatch = 0;
            foreach (var uid in distinctUserIds)
            {
                var u = await _userRepository.GetByIdAsync(uid, cancellationToken).ConfigureAwait(false);
                if (u?.Role.Value.Equals("superadmin", StringComparison.OrdinalIgnoreCase) == true)
                    superAdminsInBatch++;
            }
            if (superAdminCount - superAdminsInBatch < 1)
                throw new ForbiddenException(
                    "Bulk operation would demote all SuperAdmins. The system requires at least one SuperAdmin.");
        }
```

Aggiungi gli using mancanti in cima:
```csharp
using Api.Middleware.Exceptions;
```

- [ ] **Step 2: Scrivi test per ADM-002c + ADM-004**

Crea `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkRoleChangeSecurityTests.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class BulkRoleChangeSecurityTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkRoleChangeCommandHandler>> _mockLogger;
    private readonly BulkRoleChangeCommandHandler _handler;

    public BulkRoleChangeSecurityTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkRoleChangeCommandHandler>>();
        _handler = new BulkRoleChangeCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_AdminExceedsAdminSizeLimit_ThrowsForbiddenException()
    {
        // Arrange — Admin tries to bulk change 101 users: blocked (Admin limit is 100)
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkRoleChangeCommand(userIds, "Editor", requesterId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*role limit*");
    }

    [Fact]
    public async Task Handle_SuperAdminWithin1000Limit_Succeeds()
    {
        // Arrange — SuperAdmin bulk changes 500 users: allowed
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "superadmin@test.com", "superadmin");
        var userIds = Enumerable.Range(0, 500).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        // Each target user
        _mockUserRepository.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != requesterId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateUser(id, $"{id}@test.com", "user"));
        _mockUserRepository.Setup(r => r.CountByRoleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2); // 2 SuperAdmins, none in batch

        var command = new BulkRoleChangeCommand(userIds, "Editor", requesterId);

        // Act — should NOT throw (may return BulkOperationResult with successes)
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(500);
    }

    [Fact]
    public async Task Handle_AdminAssignsAdminRole_ThrowsForbiddenException()
    {
        // Arrange — Admin (level 3) tries to bulk assign Admin (level 3): blocked
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "admin@test.com", "admin");
        var userIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkRoleChangeCommand(userIds, "Admin", requesterId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*privilege level*");
    }

    private static User CreateUser(Guid id, string email, string role)
    {
        var user = User.Create(
            Email.Create(email),
            PasswordHash.Create("hashed_password"),
            "Test User");
        typeof(User).GetProperty("Id")?.SetValue(user, id);
        user.UpdateRole(Role.Parse(role));
        return user;
    }
}
```

- [ ] **Step 3: Esegui i test del Task 4**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet test tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkRoleChangeSecurityTests.cs -v normal
```

Atteso: tutti i test passano.

- [ ] **Step 4: Build**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet build src/Api/Api.csproj --no-incremental 2>&1 | tail -10
```

Atteso: `Build succeeded. 0 Error(s)`

- [ ] **Step 5: Commit Task 4**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkRoleChangeCommandHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkRoleChangeSecurityTests.cs
git commit -m "feat(admin): ADM-002c + ADM-004 — BulkRoleChange: role hierarchy, SuperAdmin minimum, role-based size limits"
```

---

## Task 5: [ADM-003 + ADM-004] BulkPasswordReset — SuperAdmin Only + Size Limit

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkPasswordResetCommandHandler.cs`
- Modify: `apps/api/src/Api/Routing/Admin/AdminUserBulkEndpoints.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkPasswordResetSecurityTests.cs`

- [ ] **Step 1: Aggiorna BulkPasswordResetCommandHandler — aggiungi role-based size limit**

Dopo la costante `MaxBulkSize = 1000`, aggiungi:

```csharp
    // ADM-004: Role-based batch size limits for password reset
    private static readonly Dictionary<string, int> MaxBulkSizeByRole = new(StringComparer.OrdinalIgnoreCase)
    {
        { "superadmin", 1000 },
        { "admin", 100 },
    };
```

Nel metodo `Handle`, dopo la validazione delle password e prima del loop principale, aggiungi:

```csharp
        // ADM-004: Role-based batch size limit — load requester role
        var requester = await _userRepository.GetByIdAsync(command.RequesterId, cancellationToken)
            .ConfigureAwait(false);
        if (requester is not null)
        {
            var allowedBulkSize = MaxBulkSizeByRole.GetValueOrDefault(requester.Role.Value, 100);
            if (distinctUserIds.Count > allowedBulkSize)
                throw new ForbiddenException(
                    $"Bulk password reset of {distinctUserIds.Count} users exceeds your role limit of {allowedBulkSize}. Contact a SuperAdmin for larger operations.");
        }
```

Aggiungi using in cima al file:
```csharp
using Api.Middleware.Exceptions;
```

- [ ] **Step 2: Aggiorna endpoint BulkPasswordReset — usa RequireSuperAdminSession**

Nel file `apps/api/src/Api/Routing/Admin/AdminUserBulkEndpoints.cs`, nel metodo `HandleBulkPasswordReset`, sostituisci:

```csharp
        var (authorized, session, error) = context.RequireAdminSession();
```

con:

```csharp
        var (authorized, session, error) = context.RequireSuperAdminSession();
```

Aggiorna anche il description dell'endpoint `.WithDescription` per riflettere il requisito SuperAdmin:

```csharp
        .WithDescription(@"Resets passwords for multiple users in a single operation.

**Authorization**: SuperAdmin session required (ADM-003: elevated from Admin — high-risk operation).

**Limits (ADM-004)**:
- SuperAdmin: max 1000 users per request
- Admin role: max 100 users per request

**Behavior**:
- Partial success supported: failures for individual users do not block others
- Single DB transaction for all successful resets")
```

- [ ] **Step 3: Scrivi test per ADM-004 (BulkPasswordReset)**

Crea `apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkPasswordResetSecurityTests.cs`:

```csharp
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class BulkPasswordResetSecurityTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<BulkPasswordResetCommandHandler>> _mockLogger;
    private readonly BulkPasswordResetCommandHandler _handler;

    public BulkPasswordResetSecurityTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<BulkPasswordResetCommandHandler>>();
        _handler = new BulkPasswordResetCommandHandler(
            _mockUserRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_AdminExceeds100UserLimit_ThrowsForbiddenException()
    {
        // Arrange — Admin tries to reset 101 passwords: blocked (limit is 100)
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 101).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*role limit*");
    }

    [Fact]
    public async Task Handle_AdminResets100Users_Succeeds()
    {
        // Arrange — Admin resets exactly 100: allowed
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "admin@test.com", "admin");
        var userIds = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _mockUserRepository.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != requesterId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateUser(id, $"{id}@test.com", "user"));

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        // Act — should succeed (no ForbiddenException)
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(100);
    }

    [Fact]
    public async Task Handle_SuperAdminResets500Users_Succeeds()
    {
        // Arrange — SuperAdmin resets 500: allowed (limit is 1000)
        var requesterId = Guid.NewGuid();
        var requester = CreateUser(requesterId, "superadmin@test.com", "superadmin");
        var userIds = Enumerable.Range(0, 500).Select(_ => Guid.NewGuid()).ToList();

        _mockUserRepository.Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(requester);
        _mockUserRepository.Setup(r => r.GetByIdAsync(It.Is<Guid>(id => id != requesterId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => CreateUser(id, $"{id}@test.com", "user"));

        var command = new BulkPasswordResetCommand(userIds, "SecurePassword123!", requesterId);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalRequested.Should().Be(500);
    }

    private static User CreateUser(Guid id, string email, string role)
    {
        var user = User.Create(
            Email.Create(email),
            PasswordHash.Create("hashed_password"),
            "Test User");
        typeof(User).GetProperty("Id")?.SetValue(user, id);
        user.UpdateRole(Role.Parse(role));
        return user;
    }
}
```

- [ ] **Step 4: Esegui tutti i test del progetto per verifica regressione**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet test tests/Api.Tests/ --filter "Category=Unit" -v minimal 2>&1 | tail -30
```

Atteso: tutti i test Unit passano (0 failures).

- [ ] **Step 5: Build finale**

```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/api
dotnet build src/Api/Api.csproj --no-incremental 2>&1 | tail -10
```

Atteso: `Build succeeded. 0 Error(s)`

- [ ] **Step 6: Commit Task 5**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git add apps/api/src/Api/BoundedContexts/Administration/Application/Commands/BulkPasswordResetCommandHandler.cs
git add apps/api/src/Api/Routing/Admin/AdminUserBulkEndpoints.cs
git add apps/api/tests/Api.Tests/BoundedContexts/Administration/Application/Commands/BulkPasswordResetSecurityTests.cs
git commit -m "feat(admin): ADM-003 + ADM-004 — BulkPasswordReset: SuperAdmin-only endpoint, role-based size limits"
```

---

## Chiusura Branch

- [ ] **Push e apertura PR**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git push -u origin feature/admin-security-hardening
gh pr create \
  --base main-dev \
  --title "feat(admin): Admin Security Hardening — ADM-001/002/003/004" \
  --body "$(cat <<'EOF'
## Summary
- **ADM-001**: Impersonation richiede `Reason` obbligatoria (min 10 chars); audit log include reason e EndedAt
- **ADM-002**: Role hierarchy guard in ChangeUserRoleCommand e BulkRoleChangeCommand; protezione ultimo SuperAdmin
- **ADM-003**: Impersonation e BulkPasswordReset elevati a RequireSuperAdminSession
- **ADM-004**: Limiti bulk per ruolo — Admin: max 100, SuperAdmin: max 1000 — in BulkRoleChange e BulkPasswordReset

## Checklist implementazione
- [ ] 5 nuovi file di test, ~80 assertion
- [ ] 0 nuove migration DB
- [ ] Tutti gli endpoint critici aggiornati
- [ ] Build pulita senza warning

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review Checklist

### Spec Coverage
| Requirement | Task | Status |
|-------------|------|--------|
| ADM-001: Reason obbligatoria su impersonation | T1 | ✅ |
| ADM-001: Audit log include reason | T1 | ✅ |
| ADM-001: Audit log EndImpersonation include timestamp | T1 | ✅ |
| ADM-002: Privilege escalation guard (no role >= proprio) | T3, T4 | ✅ |
| ADM-002: Minimum SuperAdmin count guard | T3, T4 | ✅ |
| ADM-002: CountByRoleAsync repository | T2 | ✅ |
| ADM-003: Impersonation richiede SuperAdmin | T1 | ✅ |
| ADM-003: BulkPasswordReset richiede SuperAdmin | T5 | ✅ |
| ADM-004: BulkRoleChange role-based limit | T4 | ✅ |
| ADM-004: BulkPasswordReset role-based limit | T5 | ✅ |

### Placeholder Scan
- Nessun TODO, TBD, o placeholder trovato
- Tutti i blocchi di codice sono completi e compilabili

### Type Consistency
- `ForbiddenException` usato uniformemente in T1/T3/T4/T5 (tutti da `Api.Middleware.Exceptions`)
- `Role.SuperAdmin.Value` = `"superadmin"` (lowercase) — usato consistentemente in CountByRoleAsync, ImpersonateUserCommandHandler, ChangeUserRoleCommandHandler
- `session.User.Role` è `string` in `UserDto` — passato come `AdminRole` a `ChangeUserRoleCommand` ✅
- `MaxBulkSizeByRole` definito localmente in ciascun handler — non shared (YAGNI) ✅
