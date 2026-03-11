# Phase 1: Admin Invite System — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin can invite users via email with a token-based link; invited users accept, set their password, and enter the platform.

**Architecture:** New `UserInvitation` entity in Authentication bounded context. User record created only at acceptance time (not at invite-send). Dedicated `SendInvitationEmailCommand` in UserNotifications. Frontend: new `/accept-invite` page + modified `/change-password` for forced-change flow.

**Tech Stack:** .NET 9, EF Core, MediatR, FluentValidation, Next.js 16, React 19, Zod, react-hook-form

**Spec:** `docs/superpowers/specs/2026-03-11-admin-invite-onboarding-design.md`

---

## File Structure

### Backend — New Files
| File | Responsibility |
|------|---------------|
| `Authentication/Domain/Entities/UserInvitation.cs` | DDD entity: invitation with token, status, expiry |
| `Authentication/Domain/Enums/InvitationStatus.cs` | Enum: Pending, Accepted, Expired, Revoked |
| `Authentication/Domain/Events/UserInvitedEvent.cs` | Domain event when invitation is created |
| `Authentication/Domain/Events/InvitationAcceptedEvent.cs` | Domain event when invitation is accepted |
| `Authentication/Domain/Repositories/IUserInvitationRepository.cs` | Repository interface |
| `Authentication/Infrastructure/Repositories/UserInvitationRepository.cs` | EF Core implementation |
| `Authentication/Infrastructure/Configuration/UserInvitationConfiguration.cs` | EF entity config |
| `Authentication/Application/Commands/Invitation/InviteUserCommand.cs` | Command + response records |
| `Authentication/Application/Commands/Invitation/InviteUserCommandHandler.cs` | Creates invitation, sends email |
| `Authentication/Application/Commands/Invitation/AcceptInvitationCommand.cs` | Command + response records |
| `Authentication/Application/Commands/Invitation/AcceptInvitationCommandHandler.cs` | Validates token, creates user, creates session |
| `Authentication/Application/Commands/Invitation/RevokeInvitationCommand.cs` | Command record |
| `Authentication/Application/Commands/Invitation/RevokeInvitationCommandHandler.cs` | Marks invitation as revoked |
| `Authentication/Application/Validators/InviteUserCommandValidator.cs` | FluentValidation |
| `Authentication/Application/Validators/AcceptInvitationCommandValidator.cs` | FluentValidation |
| `Authentication/Application/Queries/GetPendingInvitationsQuery.cs` | Query + handler |
| `UserNotifications/Application/Commands/SendInvitationEmailCommand.cs` | Email command for invitations |
| `UserNotifications/Application/Handlers/SendInvitationEmailCommandHandler.cs` | Renders + enqueues invitation email |
| `Routing/AdminInvitationEndpoints.cs` | Admin endpoints for invite management |
| `Routing/InvitationEndpoints.cs` | Public endpoint for accept-invite |

### Backend — Modified Files
| File | Change |
|------|--------|
| `Authentication/Domain/Entities/User.cs` | Add `MustChangePassword`, `InvitedBy` properties + `SetMustChangePassword()` method |
| `Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs` | Register `IUserInvitationRepository` |
| `Program.cs` | Map new endpoint groups |

### Frontend — New Files
| File | Responsibility |
|------|---------------|
| `app/(auth)/accept-invite/_content.tsx` | Accept invitation page |
| `app/(auth)/accept-invite/page.tsx` | Next.js page wrapper |
| `components/admin/users/InviteUserModal.tsx` | Modal form for inviting users |
| `components/admin/users/PendingInvitationsTable.tsx` | Table showing pending invitations |
| `lib/api/clients/invitationClient.ts` | API client for invitation endpoints |
| `lib/api/schemas/invitation.schemas.ts` | Zod schemas for invitation DTOs |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `app/(auth)/reset-password/_content.tsx` | Handle `MustChangePassword` mode (no current password field) |
| `lib/api/index.ts` | Register `invitationClient` in factory |
| `lib/api/clients/authClient.ts` | Add `acceptInvite(token)` method |
| `app/admin/(dashboard)/users/page.tsx` | Add "Invita Utente" button |

### Tests — New Files
| File | Scope |
|------|-------|
| `Api.Tests/Authentication/Commands/InviteUserCommandTests.cs` | Unit: handler + validator |
| `Api.Tests/Authentication/Commands/AcceptInvitationCommandTests.cs` | Unit: handler + validator |
| `Api.Tests/Authentication/Commands/RevokeInvitationCommandTests.cs` | Unit: handler |
| `Api.Tests/Authentication/Domain/UserInvitationTests.cs` | Unit: entity behavior |
| `Api.Tests/Authentication/Queries/GetPendingInvitationsQueryTests.cs` | Unit: query handler |
| `apps/web/__tests__/admin/users/InviteUserModal.test.tsx` | Frontend: modal |
| `apps/web/__tests__/auth/accept-invite.test.tsx` | Frontend: accept page |

### Migration
| File | Description |
|------|-------------|
| `Infrastructure/Migrations/{timestamp}_AddUserInvitations.cs` | Auto-generated by `dotnet ef migrations add` |

---

## Chunk 1: Domain Layer (Entity + Events + Repository Interface)

### Task 1: Create InvitationStatus enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/InvitationStatus.cs`

- [ ] **Step 1: Write the enum**

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Enums;

public enum InvitationStatus
{
    Pending = 0,
    Accepted = 1,
    Expired = 2,
    Revoked = 3
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/InvitationStatus.cs
git commit -m "feat(auth): add InvitationStatus enum"
```

### Task 2: Create UserInvitation entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/UserInvitation.cs`
- Test: `apps/api/tests/Api.Tests/Authentication/Domain/UserInvitationTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class UserInvitationTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreatePendingInvitation()
    {
        // Arrange
        var email = "test@example.com";
        var role = "user";
        var displayName = "Test User";
        var createdBy = Guid.NewGuid();

        // Act
        var invitation = UserInvitation.Create(email, role, displayName, createdBy);

        // Assert
        invitation.Email.Should().Be(email);
        invitation.Role.Should().Be(role);
        invitation.DisplayName.Should().Be(displayName);
        invitation.CreatedBy.Should().Be(createdBy);
        invitation.Status.Should().Be(InvitationStatus.Pending);
        invitation.InvitationTokenHash.Should().NotBeNullOrEmpty();
        invitation.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        invitation.AcceptedAt.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldSetExpiryTo48Hours()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        var expectedExpiry = DateTime.UtcNow.AddHours(48);
        invitation.ExpiresAt.Should().BeCloseTo(expectedExpiry, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Accept_WhenPending_ShouldMarkAsAccepted()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.Accept();
        invitation.Status.Should().Be(InvitationStatus.Accepted);
        invitation.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public void Accept_WhenAlreadyAccepted_ShouldThrow()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.Accept();
        var act = () => invitation.Accept();
        act.Should().Throw<InvalidOperationException>().WithMessage("*already accepted*");
    }

    [Fact]
    public void Accept_WhenExpired_ShouldThrow()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        // Use reflection to set ExpiresAt to the past for testing
        typeof(UserInvitation).GetProperty(nameof(UserInvitation.ExpiresAt))!
            .SetValue(invitation, DateTime.UtcNow.AddHours(-1));
        var act = () => invitation.Accept();
        act.Should().Throw<InvalidOperationException>().WithMessage("*expired*");
    }

    [Fact]
    public void Accept_WhenRevoked_ShouldThrow()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.Revoke();
        var act = () => invitation.Accept();
        act.Should().Throw<InvalidOperationException>().WithMessage("*revoked*");
    }

    [Fact]
    public void Revoke_WhenPending_ShouldMarkAsRevoked()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.Revoke();
        invitation.Status.Should().Be(InvitationStatus.Revoked);
    }

    [Fact]
    public void IsExpired_WhenPastExpiryDate_ShouldReturnTrue()
    {
        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        typeof(UserInvitation).GetProperty(nameof(UserInvitation.ExpiresAt))!
            .SetValue(invitation, DateTime.UtcNow.AddHours(-1));
        invitation.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithCorrectToken_ShouldReturnTrue()
    {
        var (invitation, rawToken) = UserInvitation.CreateWithRawToken("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.ValidateToken(rawToken).Should().BeTrue();
    }

    [Fact]
    public void ValidateToken_WithWrongToken_ShouldReturnFalse()
    {
        var (invitation, _) = UserInvitation.CreateWithRawToken("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.ValidateToken("wrong-token").Should().BeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~UserInvitationTests" --no-build 2>&1 | head -20`
Expected: FAIL — `UserInvitation` class does not exist

- [ ] **Step 3: Write the entity**

```csharp
using System.Security.Cryptography;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

public sealed class UserInvitation : AggregateRoot<Guid>
{
    private const int TokenLengthBytes = 32;
    private const int ExpiryHours = 48;

    public string Email { get; private set; } = string.Empty;
    public string Role { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string InvitationTokenHash { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; set; } // public set for testing only
    public InvitationStatus Status { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime? AcceptedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // EF Core constructor
    private UserInvitation() : base() { }

    private UserInvitation(
        Guid id,
        string email,
        string role,
        string displayName,
        string tokenHash,
        Guid createdBy) : base(id)
    {
        Email = email ?? throw new ArgumentNullException(nameof(email));
        Role = role ?? throw new ArgumentNullException(nameof(role));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        InvitationTokenHash = tokenHash ?? throw new ArgumentNullException(nameof(tokenHash));
        CreatedBy = createdBy;
        Status = InvitationStatus.Pending;
        ExpiresAt = DateTime.UtcNow.AddHours(ExpiryHours);
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Factory method for production use. Returns entity with hashed token.
    /// Use CreateWithRawToken for testing.
    /// </summary>
    public static UserInvitation Create(string email, string role, string displayName, Guid createdBy)
    {
        var rawToken = GenerateToken();
        var tokenHash = HashToken(rawToken);
        var invitation = new UserInvitation(Guid.NewGuid(), email, role, displayName, tokenHash, createdBy);
        invitation.AddDomainEvent(new UserInvitedEvent(invitation.Id, email, role, createdBy));
        return invitation;
    }

    /// <summary>
    /// Factory method for testing and for the handler that needs to send the raw token via email.
    /// </summary>
    public static (UserInvitation Invitation, string RawToken) CreateWithRawToken(
        string email, string role, string displayName, Guid createdBy)
    {
        var rawToken = GenerateToken();
        var tokenHash = HashToken(rawToken);
        var invitation = new UserInvitation(Guid.NewGuid(), email, role, displayName, tokenHash, createdBy);
        invitation.AddDomainEvent(new UserInvitedEvent(invitation.Id, email, role, createdBy));
        return (invitation, rawToken);
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    public bool ValidateToken(string rawToken)
    {
        var hash = HashToken(rawToken);
        return string.Equals(InvitationTokenHash, hash, StringComparison.Ordinal);
    }

    public void Accept()
    {
        if (Status == InvitationStatus.Accepted)
            throw new InvalidOperationException("Invitation has already been accepted");
        if (Status == InvitationStatus.Revoked)
            throw new InvalidOperationException("Invitation has been revoked");
        if (IsExpired)
            throw new InvalidOperationException("Invitation has expired");

        Status = InvitationStatus.Accepted;
        AcceptedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new InvitationAcceptedEvent(Id, Email));
    }

    public void Revoke()
    {
        if (Status != InvitationStatus.Pending)
            throw new InvalidOperationException($"Cannot revoke invitation with status {Status}");

        Status = InvitationStatus.Revoked;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkExpired()
    {
        if (Status == InvitationStatus.Pending && IsExpired)
        {
            Status = InvitationStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(TokenLengthBytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~UserInvitationTests" -v minimal`
Expected: All 10 tests PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/UserInvitation.cs
git add apps/api/tests/Api.Tests/Authentication/Domain/UserInvitationTests.cs
git commit -m "feat(auth): add UserInvitation entity with token hashing and status management"
```

### Task 3: Create domain events

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserInvitedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/InvitationAcceptedEvent.cs`

- [ ] **Step 1: Write UserInvitedEvent**

```csharp
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.Authentication.Domain.Events;

internal sealed class UserInvitedEvent : DomainEventBase
{
    public Guid InvitationId { get; }
    public string Email { get; }
    public string Role { get; }
    public Guid InvitedBy { get; }

    public UserInvitedEvent(Guid invitationId, string email, string role, Guid invitedBy)
    {
        InvitationId = invitationId;
        Email = email;
        Role = role;
        InvitedBy = invitedBy;
    }
}
```

- [ ] **Step 2: Write InvitationAcceptedEvent**

```csharp
using Api.SharedKernel.Domain;

namespace Api.BoundedContexts.Authentication.Domain.Events;

internal sealed class InvitationAcceptedEvent : DomainEventBase
{
    public Guid InvitationId { get; }
    public string Email { get; }

    public InvitationAcceptedEvent(Guid invitationId, string email)
    {
        InvitationId = invitationId;
        Email = email;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserInvitedEvent.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/InvitationAcceptedEvent.cs
git commit -m "feat(auth): add UserInvited and InvitationAccepted domain events"
```

### Task 4: Add MustChangePassword and InvitedBy to User entity

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`

- [ ] **Step 1: Read User.cs to find exact insertion point**

Read `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs` — find the properties section and the end of public methods.

- [ ] **Step 2: Add properties and method**

Add after existing properties (find the `UserTier` or `Tier` property area):

```csharp
public bool MustChangePassword { get; private set; }
public Guid? InvitedBy { get; private set; }
```

Add a domain method:

```csharp
public void SetMustChangePassword(bool value)
{
    MustChangePassword = value;
}

public void SetInvitedBy(Guid? invitedBy)
{
    InvitedBy = invitedBy;
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs
git commit -m "feat(auth): add MustChangePassword and InvitedBy properties to User entity"
```

### Task 5: Create repository interface

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/IUserInvitationRepository.cs`

- [ ] **Step 1: Write the interface**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

public interface IUserInvitationRepository
{
    Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);
    Task<UserInvitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<UserInvitation>> GetByStatusAsync(InvitationStatus status, int limit = 50, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<UserInvitation>> GetExpiredPendingAsync(DateTime cutoff, CancellationToken ct = default);
    Task<bool> ExistsPendingForEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(UserInvitation invitation, CancellationToken ct = default);
    Task UpdateAsync(UserInvitation invitation, CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/IUserInvitationRepository.cs
git commit -m "feat(auth): add IUserInvitationRepository interface"
```

### Task 6: Create EF configuration and repository implementation

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Configuration/UserInvitationConfiguration.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Repositories/UserInvitationRepository.cs`

- [ ] **Step 1: Write EF configuration**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Api.BoundedContexts.Authentication.Infrastructure.Configuration;

internal sealed class UserInvitationConfiguration : IEntityTypeConfiguration<UserInvitation>
{
    public void Configure(EntityTypeBuilder<UserInvitation> builder)
    {
        builder.ToTable("user_invitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.InvitationTokenHash)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        // Indexes
        builder.HasIndex(x => x.InvitationTokenHash).IsUnique();
        builder.HasIndex(x => new { x.Email, x.Status });
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ExpiresAt);
    }
}
```

- [ ] **Step 2: Write repository implementation**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Infrastructure.Repositories;

internal sealed class UserInvitationRepository : IUserInvitationRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public UserInvitationRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<UserInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .FirstOrDefaultAsync(x => x.Id == id, ct)
            .ConfigureAwait(false);
    }

    public async Task<UserInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .FirstOrDefaultAsync(x => x.InvitationTokenHash == tokenHash, ct)
            .ConfigureAwait(false);
    }

    public async Task<UserInvitation?> GetPendingByEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .FirstOrDefaultAsync(x => x.Email == email && x.Status == InvitationStatus.Pending, ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserInvitation>> GetByStatusAsync(
        InvitationStatus status, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .Where(x => x.Status == status)
            .OrderByDescending(x => x.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserInvitation>> GetExpiredPendingAsync(DateTime cutoff, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .Where(x => x.Status == InvitationStatus.Pending && x.ExpiresAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsPendingForEmailAsync(string email, CancellationToken ct = default)
    {
        return await _dbContext.Set<UserInvitation>()
            .AnyAsync(x => x.Email == email && x.Status == InvitationStatus.Pending, ct)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(UserInvitation invitation, CancellationToken ct = default)
    {
        await _dbContext.Set<UserInvitation>().AddAsync(invitation, ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserInvitation invitation, CancellationToken ct = default)
    {
        _dbContext.Set<UserInvitation>().Update(invitation);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Register repository in DI**

Modify `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs` — add:

```csharp
services.AddScoped<IUserInvitationRepository, UserInvitationRepository>();
```

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Configuration/UserInvitationConfiguration.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Repositories/UserInvitationRepository.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/DependencyInjection/AuthenticationServiceExtensions.cs
git commit -m "feat(auth): add UserInvitation EF configuration, repository, and DI registration"
```

### Task 7: Create database migration

- [ ] **Step 1: Generate migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddUserInvitations`
Expected: Migration file created in `Infrastructure/Migrations/`

- [ ] **Step 2: Also add MustChangePassword and InvitedBy to User table**

If the migration doesn't automatically pick up User entity changes, add manually in the `Up()` method:

```csharp
migrationBuilder.AddColumn<bool>(
    name: "MustChangePassword",
    table: "users",
    type: "boolean",
    nullable: false,
    defaultValue: false);

migrationBuilder.AddColumn<Guid>(
    name: "InvitedBy",
    table: "users",
    type: "uuid",
    nullable: true);
```

- [ ] **Step 3: Apply migration**

Run: `cd apps/api/src/Api && dotnet ef database update`
Expected: Migration applied successfully

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(auth): add UserInvitations migration + MustChangePassword on User"
```

---

## Chunk 2: Application Layer (Commands + Handlers + Validators)

### Task 8: Create SendInvitationEmailCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/SendInvitationEmailCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/SendInvitationEmailCommandHandler.cs`

- [ ] **Step 1: Write the command**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserNotifications.Application.Commands;

internal record SendInvitationEmailCommand(
    string ToEmail,
    string DisplayName,
    string InviteLink,
    string AdminName,
    DateTime ExpiresAt
) : ICommand<Guid>;
```

- [ ] **Step 2: Write the handler**

```csharp
using Api.BoundedContexts.UserNotifications.Domain.Entities;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.UserNotifications.Application.Handlers;

internal class SendInvitationEmailCommandHandler : ICommandHandler<SendInvitationEmailCommand, Guid>
{
    private readonly IEmailQueueRepository _emailQueueRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SendInvitationEmailCommandHandler> _logger;

    public SendInvitationEmailCommandHandler(
        IEmailQueueRepository emailQueueRepository,
        IUnitOfWork unitOfWork,
        ILogger<SendInvitationEmailCommandHandler> logger)
    {
        _emailQueueRepository = emailQueueRepository ?? throw new ArgumentNullException(nameof(emailQueueRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid> Handle(SendInvitationEmailCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var htmlBody = RenderInvitationEmail(command);

        var emailQueueItem = EmailQueueItem.Create(
            userId: Guid.Empty, // No user yet at invite time
            command.ToEmail,
            $"Sei stato invitato a MeepleAI da {command.AdminName}",
            htmlBody,
            correlationId: null);

        await _emailQueueRepository.AddAsync(emailQueueItem, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Invitation email enqueued {EmailId} to {Email}", emailQueueItem.Id, command.ToEmail);

        return emailQueueItem.Id;
    }

    private static string RenderInvitationEmail(SendInvitationEmailCommand command)
    {
        return $"""
        <div style="font-family: 'Nunito', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 32px;">
            <h1 style="font-family: 'Quicksand', sans-serif; color: #f59e0b;">Benvenuto su MeepleAI!</h1>
            <p>Ciao <strong>{command.DisplayName}</strong>,</p>
            <p><strong>{command.AdminName}</strong> ti ha invitato a unirti a MeepleAI, l'assistente AI per i giochi da tavolo.</p>
            <p>
                <a href="{command.InviteLink}"
                   style="display: inline-block; padding: 12px 24px; background: #f59e0b; color: white; text-decoration: none; border-radius: 8px; font-weight: bold;">
                    Accetta l'invito
                </a>
            </p>
            <p style="color: #6b7280; font-size: 14px;">
                Questo invito scade il {command.ExpiresAt:dd/MM/yyyy HH:mm} UTC.
            </p>
        </div>
        """;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/Commands/SendInvitationEmailCommand.cs
git add apps/api/src/Api/BoundedContexts/UserNotifications/Application/Handlers/SendInvitationEmailCommandHandler.cs
git commit -m "feat(notifications): add SendInvitationEmailCommand with HTML template"
```

### Task 9: Create InviteUserCommand + Handler + Validator

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/InviteUserCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/InviteUserCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/InviteUserCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/Authentication/Commands/InviteUserCommandTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Domain;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.Authentication.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class InviteUserCommandTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserInvitationRepository _invitationRepository = Substitute.For<IUserInvitationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly ILogger<InviteUserCommandHandler> _logger = Substitute.For<ILogger<InviteUserCommandHandler>>();

    private InviteUserCommandHandler CreateHandler() =>
        new(_userRepository, _invitationRepository, _unitOfWork, _mediator, _logger);

    [Fact]
    public async Task Handle_WithValidData_ShouldCreateInvitationAndSendEmail()
    {
        // Arrange
        var command = new InviteUserCommand("test@example.com", "user", "Test User", Guid.NewGuid(), "Admin Name");
        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _invitationRepository.ExistsPendingForEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(false);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.InvitationId.Should().NotBeEmpty();
        await _invitationRepository.Received(1).AddAsync(Arg.Any<UserInvitation>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _mediator.Received(1).Send(Arg.Any<IRequest<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyRegistered_ShouldThrowConflict()
    {
        var existingUser = Substitute.For<User>();
        var command = new InviteUserCommand("existing@example.com", "user", "Test", Guid.NewGuid(), "Admin");
        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(existingUser);

        var handler = CreateHandler();
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task Handle_WhenPendingInvitationExists_ShouldThrowConflict()
    {
        var command = new InviteUserCommand("pending@example.com", "user", "Test", Guid.NewGuid(), "Admin");
        _userRepository.GetByEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns((User?)null);
        _invitationRepository.ExistsPendingForEmailAsync(command.Email, Arg.Any<CancellationToken>())
            .Returns(true);

        var handler = CreateHandler();
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*pending invitation*");
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~InviteUserCommandTests" --no-build 2>&1 | head -20`
Expected: FAIL — classes don't exist

- [ ] **Step 3: Write the command**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal record InviteUserCommand(
    string Email,
    string Role,
    string DisplayName,
    Guid AdminId,
    string AdminName
) : ICommand<InviteUserResponse>;

internal record InviteUserResponse(
    Guid InvitationId,
    string Email,
    DateTime ExpiresAt
);
```

- [ ] **Step 4: Write the validator**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

internal sealed class InviteUserCommandValidator : AbstractValidator<InviteUserCommand>
{
    private static readonly string[] ValidRoles = { "user", "editor", "creator", "admin" };

    public InviteUserCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Email must be a valid email address")
            .MaximumLength(255);

        RuleFor(x => x.DisplayName)
            .NotEmpty().WithMessage("Display name is required")
            .MinimumLength(2)
            .MaximumLength(100);

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required")
            .Must(role => ValidRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Role must be one of: {string.Join(", ", ValidRoles)}");

        RuleFor(x => x.AdminId)
            .NotEmpty().WithMessage("AdminId is required");

        RuleFor(x => x.AdminName)
            .NotEmpty().WithMessage("AdminName is required");
    }
}
```

- [ ] **Step 5: Write the handler**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal class InviteUserCommandHandler : ICommandHandler<InviteUserCommand, InviteUserResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<InviteUserCommandHandler> _logger;

    public InviteUserCommandHandler(
        IUserRepository userRepository,
        IUserInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<InviteUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InviteUserResponse> Handle(InviteUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check if email already registered
        var existingUser = await _userRepository.GetByEmailAsync(command.Email, cancellationToken)
            .ConfigureAwait(false);
        if (existingUser != null)
            throw new ConflictException($"Email {command.Email} is already registered");

        // Check if pending invitation exists
        var hasPending = await _invitationRepository.ExistsPendingForEmailAsync(command.Email, cancellationToken)
            .ConfigureAwait(false);
        if (hasPending)
            throw new ConflictException($"A pending invitation already exists for {command.Email}");

        // Create invitation with raw token (needed for email link)
        var (invitation, rawToken) = UserInvitation.CreateWithRawToken(
            command.Email, command.Role, command.DisplayName, command.AdminId);

        await _invitationRepository.AddAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Send invitation email via MediatR (cross-bounded-context)
        var inviteLink = $"/accept-invite?token={Uri.EscapeDataString(rawToken)}";
        await _mediator.Send(new SendInvitationEmailCommand(
            command.Email,
            command.DisplayName,
            inviteLink,
            command.AdminName,
            invitation.ExpiresAt
        ), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Invitation created {InvitationId} for {Email} by admin {AdminId}",
            invitation.Id, command.Email, command.AdminId);

        return new InviteUserResponse(invitation.Id, command.Email, invitation.ExpiresAt);
    }
}
```

- [ ] **Step 6: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~InviteUserCommandTests" -v minimal`
Expected: All 3 tests PASS

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/InviteUserCommandValidator.cs
git add apps/api/tests/Api.Tests/Authentication/Commands/InviteUserCommandTests.cs
git commit -m "feat(auth): add InviteUserCommand with validation, conflict checks, and email dispatch"
```

### Task 10: Create AcceptInvitationCommand + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/AcceptInvitationCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/AcceptInvitationCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/AcceptInvitationCommandValidator.cs`
- Test: `apps/api/tests/Api.Tests/Authentication/Commands/AcceptInvitationCommandTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.Authentication.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class AcceptInvitationCommandTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUserInvitationRepository _invitationRepository = Substitute.For<IUserInvitationRepository>();
    private readonly ISessionRepository _sessionRepository = Substitute.For<ISessionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<AcceptInvitationCommandHandler> _logger = Substitute.For<ILogger<AcceptInvitationCommandHandler>>();

    private AcceptInvitationCommandHandler CreateHandler() =>
        new(_userRepository, _invitationRepository, _sessionRepository, _unitOfWork, _logger);

    [Fact]
    public async Task Handle_WithValidToken_ShouldCreateUserAndSession()
    {
        // Arrange
        var (invitation, rawToken) = UserInvitation.CreateWithRawToken("test@example.com", "user", "Test User", Guid.NewGuid());
        _invitationRepository.GetPendingByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((UserInvitation?)null);

        // We need to find invitation by iterating — simplified: mock returns invitation for any hash
        _invitationRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(invitation);

        var command = new AcceptInvitationCommand(rawToken, "127.0.0.1", "TestBrowser");
        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SessionToken.Should().NotBeNullOrEmpty();
        result.MustChangePassword.Should().BeTrue();
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _sessionRepository.Received(1).AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithExpiredToken_ShouldThrow()
    {
        var (invitation, rawToken) = UserInvitation.CreateWithRawToken("test@example.com", "user", "Test", Guid.NewGuid());
        invitation.ExpiresAt = DateTime.UtcNow.AddHours(-1); // Expire it

        _invitationRepository.GetByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(invitation);

        var command = new AcceptInvitationCommand(rawToken, "127.0.0.1", "TestBrowser");
        var handler = CreateHandler();

        var act = () => handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
    }
}
```

- [ ] **Step 2: Write the command**

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal record AcceptInvitationCommand(
    string Token,
    string? IpAddress = null,
    string? UserAgent = null
) : ICommand<AcceptInvitationResponse>;

internal record AcceptInvitationResponse(
    Guid UserId,
    string SessionToken,
    DateTime ExpiresAt,
    bool MustChangePassword
);
```

- [ ] **Step 3: Write the validator**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Validators;

internal sealed class AcceptInvitationCommandValidator : AbstractValidator<AcceptInvitationCommand>
{
    public AcceptInvitationCommandValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Invitation token is required");
    }
}
```

- [ ] **Step 4: Write the handler**

```csharp
using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal class AcceptInvitationCommandHandler : ICommandHandler<AcceptInvitationCommand, AcceptInvitationResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly ISessionRepository _sessionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AcceptInvitationCommandHandler> _logger;

    public AcceptInvitationCommandHandler(
        IUserRepository userRepository,
        IUserInvitationRepository invitationRepository,
        ISessionRepository sessionRepository,
        IUnitOfWork unitOfWork,
        ILogger<AcceptInvitationCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AcceptInvitationResponse> Handle(AcceptInvitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Hash the incoming token to find the invitation
        var tokenHash = HashToken(command.Token);
        var invitation = await _invitationRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
            throw new NotFoundException("Invalid or expired invitation token");

        // Accept (validates status + expiry internally)
        invitation.Accept();

        // Create user with random password (must change on first login)
        var email = Email.Create(invitation.Email);
        var role = Role.Parse(invitation.Role);
        var randomPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var passwordHash = PasswordHash.Create(randomPassword);

        var user = new User(
            Guid.NewGuid(),
            email,
            invitation.DisplayName,
            passwordHash,
            role);

        user.SetMustChangePassword(true);
        user.SetInvitedBy(invitation.CreatedBy);
        // Admin-supplied email is trusted — mark as verified
        user.VerifyEmail();

        // Create session
        var sessionToken = SessionToken.Generate();
        var session = Session.Create(
            user.Id,
            sessionToken,
            command.IpAddress,
            command.UserAgent);

        // Persist
        await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _sessionRepository.AddAsync(session, cancellationToken).ConfigureAwait(false);
        await _invitationRepository.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Invitation {InvitationId} accepted by {Email}, user {UserId} created",
            invitation.Id, invitation.Email, user.Id);

        return new AcceptInvitationResponse(
            user.Id,
            sessionToken.Value,
            session.ExpiresAt,
            MustChangePassword: true);
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 5: Run tests**

Run: `cd apps/api && dotnet test --filter "FullyQualifiedName~AcceptInvitationCommandTests" -v minimal`
Expected: All tests PASS

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/AcceptInvitationCommand.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/AcceptInvitationCommandHandler.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Validators/AcceptInvitationCommandValidator.cs
git add apps/api/tests/Api.Tests/Authentication/Commands/AcceptInvitationCommandTests.cs
git commit -m "feat(auth): add AcceptInvitationCommand — creates user at acceptance, sets MustChangePassword"
```

### Task 11: Create RevokeInvitationCommand + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/RevokeInvitationCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/RevokeInvitationCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/Authentication/Commands/RevokeInvitationCommandTests.cs`

- [ ] **Step 1: Write the command + handler**

```csharp
// RevokeInvitationCommand.cs
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal record RevokeInvitationCommand(Guid InvitationId) : ICommand<bool>;
```

```csharp
// RevokeInvitationCommandHandler.cs
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain;
using Api.SharedKernel.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal class RevokeInvitationCommandHandler : ICommandHandler<RevokeInvitationCommand, bool>
{
    private readonly IUserInvitationRepository _invitationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RevokeInvitationCommandHandler> _logger;

    public RevokeInvitationCommandHandler(
        IUserInvitationRepository invitationRepository,
        IUnitOfWork unitOfWork,
        ILogger<RevokeInvitationCommandHandler> logger)
    {
        _invitationRepository = invitationRepository ?? throw new ArgumentNullException(nameof(invitationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(RevokeInvitationCommand command, CancellationToken cancellationToken)
    {
        var invitation = await _invitationRepository.GetByIdAsync(command.InvitationId, cancellationToken)
            .ConfigureAwait(false);

        if (invitation == null)
            throw new NotFoundException($"Invitation {command.InvitationId} not found");

        invitation.Revoke();
        await _invitationRepository.UpdateAsync(invitation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Invitation {InvitationId} revoked", command.InvitationId);
        return true;
    }
}
```

- [ ] **Step 2: Write test + run**

```csharp
// Brief test — verify revoke works and not-found throws
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public class RevokeInvitationCommandTests
{
    [Fact]
    public async Task Handle_WithExistingPendingInvitation_ShouldRevoke()
    {
        var repo = Substitute.For<IUserInvitationRepository>();
        var uow = Substitute.For<IUnitOfWork>();
        var logger = Substitute.For<ILogger<RevokeInvitationCommandHandler>>();

        var invitation = UserInvitation.Create("test@example.com", "user", "Test", Guid.NewGuid());
        repo.GetByIdAsync(invitation.Id, Arg.Any<CancellationToken>()).Returns(invitation);

        var handler = new RevokeInvitationCommandHandler(repo, uow, logger);
        var result = await handler.Handle(new RevokeInvitationCommand(invitation.Id), CancellationToken.None);

        result.Should().BeTrue();
        invitation.Status.Should().Be(InvitationStatus.Revoked);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/RevokeInvitationCommand.cs
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/RevokeInvitationCommandHandler.cs
git add apps/api/tests/Api.Tests/Authentication/Commands/RevokeInvitationCommandTests.cs
git commit -m "feat(auth): add RevokeInvitationCommand"
```

### Task 12: Create GetPendingInvitationsQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetPendingInvitationsQuery.cs`

- [ ] **Step 1: Write query + handler in single file**

```csharp
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

internal record GetPendingInvitationsQuery(int Limit = 50, int Offset = 0)
    : IQuery<IReadOnlyList<InvitationDto>>;

internal record InvitationDto(
    Guid Id,
    string Email,
    string Role,
    string DisplayName,
    InvitationStatus Status,
    DateTime ExpiresAt,
    Guid CreatedBy,
    DateTime CreatedAt,
    DateTime? AcceptedAt
);

internal class GetPendingInvitationsQueryHandler
    : IQueryHandler<GetPendingInvitationsQuery, IReadOnlyList<InvitationDto>>
{
    private readonly IUserInvitationRepository _repository;

    public GetPendingInvitationsQueryHandler(IUserInvitationRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<InvitationDto>> Handle(
        GetPendingInvitationsQuery query, CancellationToken cancellationToken)
    {
        var invitations = await _repository.GetByStatusAsync(
            InvitationStatus.Pending, query.Limit, query.Offset, cancellationToken)
            .ConfigureAwait(false);

        return invitations.Select(i => new InvitationDto(
            i.Id, i.Email, i.Role, i.DisplayName,
            i.Status, i.ExpiresAt, i.CreatedBy, i.CreatedAt, i.AcceptedAt
        )).ToList();
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/GetPendingInvitationsQuery.cs
git commit -m "feat(auth): add GetPendingInvitationsQuery"
```

---

## Chunk 3: Endpoints + Frontend

### Task 13: Create Admin Invitation Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/AdminInvitationEndpoints.cs`
- Create: `apps/api/src/Api/Routing/InvitationEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs`

- [ ] **Step 1: Write AdminInvitationEndpoints**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using Api.BoundedContexts.Authentication.Application.Queries;
using MediatR;

namespace Api.Routing;

internal static class AdminInvitationEndpoints
{
    public static RouteGroupBuilder MapAdminInvitationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/admin/users/invite", HandleInviteUser)
            .WithName("InviteUser")
            .WithTags("Admin", "Invitations")
            .WithSummary("Send invitation email to a new user")
            .Produces<InviteUserResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapGet("/admin/users/invitations", HandleGetPendingInvitations)
            .WithName("GetPendingInvitations")
            .WithTags("Admin", "Invitations")
            .WithSummary("List pending invitations")
            .Produces<IReadOnlyList<InvitationDto>>(StatusCodes.Status200OK);

        group.MapDelete("/admin/users/invitations/{id:guid}", HandleRevokeInvitation)
            .WithName("RevokeInvitation")
            .WithTags("Admin", "Invitations")
            .WithSummary("Revoke a pending invitation")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<IResult> HandleInviteUser(
        InviteUserRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var command = new InviteUserCommand(
            request.Email,
            request.Role,
            request.DisplayName,
            session!.User!.Id,
            session.User.DisplayName);

        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetPendingInvitations(
        HttpContext context,
        IMediator mediator,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetPendingInvitationsQuery(limit, offset), ct)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRevokeInvitation(
        Guid id,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var (authorized, _, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        await mediator.Send(new RevokeInvitationCommand(id), ct).ConfigureAwait(false);
        return Results.Ok();
    }
}

internal record InviteUserRequest(string Email, string Role, string DisplayName);
```

- [ ] **Step 2: Write InvitationEndpoints (public)**

```csharp
using Api.BoundedContexts.Authentication.Application.Commands.Invitation;
using MediatR;

namespace Api.Routing;

internal static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/auth/accept-invite", HandleAcceptInvitation)
            .WithName("AcceptInvitation")
            .WithTags("Auth", "Invitations")
            .WithSummary("Accept an invitation and create user account")
            .Produces<AcceptInvitationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .AllowAnonymous();

        return group;
    }

    private static async Task<IResult> HandleAcceptInvitation(
        AcceptInviteRequest request,
        HttpContext context,
        IMediator mediator,
        CancellationToken ct)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();

        var command = new AcceptInvitationCommand(request.Token, ipAddress, userAgent);
        var result = await mediator.Send(command, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}

internal record AcceptInviteRequest(string Token);
```

- [ ] **Step 3: Register endpoints in Program.cs**

Find the endpoint registration section and add:

```csharp
app.MapGroup("/api/v1").MapAdminInvitationEndpoints();
app.MapGroup("/api/v1").MapInvitationEndpoints();
```

- [ ] **Step 4: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Routing/AdminInvitationEndpoints.cs
git add apps/api/src/Api/Routing/InvitationEndpoints.cs
git add apps/api/src/Api/Program.cs
git commit -m "feat(auth): add admin invitation and accept-invite endpoints"
```

### Task 14: Create frontend invitation API client + schemas

**Files:**
- Create: `apps/web/src/lib/api/schemas/invitation.schemas.ts`
- Create: `apps/web/src/lib/api/clients/invitationClient.ts`
- Modify: `apps/web/src/lib/api/index.ts`

- [ ] **Step 1: Write Zod schemas**

```typescript
// invitation.schemas.ts
import { z } from 'zod';

export const inviteUserRequestSchema = z.object({
  email: z.string().email(),
  role: z.enum(['user', 'editor', 'creator', 'admin']),
  displayName: z.string().min(2).max(100),
});

export const inviteUserResponseSchema = z.object({
  invitationId: z.string().uuid(),
  email: z.string(),
  expiresAt: z.string().datetime(),
});

export const invitationDtoSchema = z.object({
  id: z.string().uuid(),
  email: z.string(),
  role: z.string(),
  displayName: z.string(),
  status: z.enum(['Pending', 'Accepted', 'Expired', 'Revoked']),
  expiresAt: z.string().datetime(),
  createdBy: z.string().uuid(),
  createdAt: z.string().datetime(),
  acceptedAt: z.string().datetime().nullable(),
});

export const acceptInviteResponseSchema = z.object({
  userId: z.string().uuid(),
  sessionToken: z.string(),
  expiresAt: z.string().datetime(),
  mustChangePassword: z.boolean(),
});

export type InviteUserRequest = z.infer<typeof inviteUserRequestSchema>;
export type InviteUserResponse = z.infer<typeof inviteUserResponseSchema>;
export type InvitationDto = z.infer<typeof invitationDtoSchema>;
export type AcceptInviteResponse = z.infer<typeof acceptInviteResponseSchema>;
```

- [ ] **Step 2: Write API client**

```typescript
// invitationClient.ts
import type { HttpClient } from '../core/httpClient';
import type { InviteUserRequest, InviteUserResponse, InvitationDto, AcceptInviteResponse } from '../schemas/invitation.schemas';

export function createInvitationClient(http: HttpClient) {
  return {
    invite(request: InviteUserRequest): Promise<InviteUserResponse> {
      return http.post<InviteUserResponse>('/api/v1/admin/users/invite', request);
    },

    getPendingInvitations(limit = 50, offset = 0): Promise<InvitationDto[]> {
      return http.get<InvitationDto[]>(`/api/v1/admin/users/invitations?limit=${limit}&offset=${offset}`);
    },

    revokeInvitation(id: string): Promise<void> {
      return http.delete<void>(`/api/v1/admin/users/invitations/${id}`);
    },

    acceptInvite(token: string): Promise<AcceptInviteResponse> {
      return http.post<AcceptInviteResponse>('/api/v1/auth/accept-invite', { token });
    },
  };
}
```

- [ ] **Step 3: Register in API factory**

In `apps/web/src/lib/api/index.ts`, add import and registration:

```typescript
import { createInvitationClient } from './clients/invitationClient';

// Inside createApiClient():
invitations: createInvitationClient(httpClient),
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/api/schemas/invitation.schemas.ts
git add apps/web/src/lib/api/clients/invitationClient.ts
git add apps/web/src/lib/api/index.ts
git commit -m "feat(web): add invitation API client and Zod schemas"
```

### Task 15: Create Accept Invite page

**Files:**
- Create: `apps/web/src/app/(auth)/accept-invite/page.tsx`
- Create: `apps/web/src/app/(auth)/accept-invite/_content.tsx`

- [ ] **Step 1: Write the page wrapper**

```tsx
// page.tsx
import AcceptInviteContent from './_content';

export default function AcceptInvitePage() {
  return <AcceptInviteContent />;
}
```

- [ ] **Step 2: Write the content component**

```tsx
// _content.tsx
'use client';

import { useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { AuthLayout } from '@/components/layouts/AuthLayout';
import { Button } from '@/components/ui/primitives/button';
import { createApiClient } from '@/lib/api';
import { Loader2, CheckCircle, XCircle } from 'lucide-react';

type InviteState = 'validating' | 'success' | 'error';

export default function AcceptInviteContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get('token');
  const [state, setState] = useState<InviteState>('validating');
  const [errorMessage, setErrorMessage] = useState('');

  useEffect(() => {
    if (!token) {
      setState('error');
      setErrorMessage('Token di invito mancante');
      return;
    }

    const acceptInvite = async () => {
      try {
        const api = createApiClient();
        const result = await api.invitations.acceptInvite(token);

        if (result.mustChangePassword) {
          setState('success');
          // Redirect to change password after brief success message
          setTimeout(() => {
            router.push('/reset-password?mode=forced');
          }, 2000);
        }
      } catch (err: unknown) {
        setState('error');
        const message = err instanceof Error ? err.message : 'Errore durante l\'accettazione dell\'invito';
        setErrorMessage(message);
      }
    };

    acceptInvite();
  }, [token, router]);

  return (
    <AuthLayout>
      <div className="flex flex-col items-center justify-center min-h-[400px] text-center space-y-6">
        {state === 'validating' && (
          <>
            <Loader2 className="h-12 w-12 animate-spin text-amber-500" />
            <h2 className="font-quicksand text-2xl font-bold">Accettazione invito...</h2>
            <p className="font-nunito text-muted-foreground">Stiamo verificando il tuo invito</p>
          </>
        )}

        {state === 'success' && (
          <>
            <CheckCircle className="h-12 w-12 text-green-500" />
            <h2 className="font-quicksand text-2xl font-bold">Benvenuto su MeepleAI!</h2>
            <p className="font-nunito text-muted-foreground">
              Il tuo account è stato creato. Ora devi impostare la tua password.
            </p>
            <p className="font-nunito text-sm text-muted-foreground">Reindirizzamento in corso...</p>
          </>
        )}

        {state === 'error' && (
          <>
            <XCircle className="h-12 w-12 text-red-500" />
            <h2 className="font-quicksand text-2xl font-bold">Invito non valido</h2>
            <p className="font-nunito text-muted-foreground">{errorMessage}</p>
            <Button onClick={() => router.push('/login')} variant="outline">
              Vai al login
            </Button>
          </>
        )}
      </div>
    </AuthLayout>
  );
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/\(auth\)/accept-invite/
git commit -m "feat(web): add accept-invite page with token validation and redirect to password change"
```

### Task 16: Modify reset-password page for forced change mode

**Files:**
- Modify: `apps/web/src/app/(auth)/reset-password/_content.tsx`

- [ ] **Step 1: Read the existing file**

Read `apps/web/src/app/(auth)/reset-password/_content.tsx` to understand current structure.

- [ ] **Step 2: Add forced-change mode**

Add detection of `mode=forced` query param. When in forced mode:
- Hide the "current password" field (if any)
- Show header: "Imposta la tua nuova password"
- After success, redirect to `/onboarding` (Phase 2) or `/dashboard` (until Phase 2 is built)
- Call `UpdatePassword` endpoint (not `ChangePassword`)

The exact edits depend on reading the current file. Key changes:

```tsx
const mode = searchParams.get('mode');
const isForcedChange = mode === 'forced';

// In the form:
{!isForcedChange && (
  <AccessibleFormInput label="Password attuale" type="password" {...register('currentPassword')} />
)}

// After success:
if (isForcedChange) {
  router.push('/dashboard'); // Will be /onboarding after Phase 2
} else {
  router.push('/dashboard');
}
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(auth\)/reset-password/_content.tsx
git commit -m "feat(web): support forced password change mode on reset-password page"
```

### Task 17: Create InviteUserModal component

**Files:**
- Create: `apps/web/src/components/admin/users/InviteUserModal.tsx`
- Test: `apps/web/__tests__/admin/users/InviteUserModal.test.tsx`

- [ ] **Step 1: Write the component**

```tsx
'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/primitives/select';
import { useToast } from '@/hooks/use-toast';
import { createApiClient } from '@/lib/api';
import { inviteUserRequestSchema, type InviteUserRequest } from '@/lib/api/schemas/invitation.schemas';
import { Loader2, Mail } from 'lucide-react';

interface InviteUserModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: () => void;
}

export function InviteUserModal({ open, onOpenChange, onSuccess }: InviteUserModalProps) {
  const { toast } = useToast();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const {
    register,
    handleSubmit,
    setValue,
    reset,
    formState: { errors },
  } = useForm<InviteUserRequest>({
    resolver: zodResolver(inviteUserRequestSchema),
    defaultValues: { role: 'user' },
  });

  const onSubmit = async (data: InviteUserRequest) => {
    setIsSubmitting(true);
    try {
      const api = createApiClient();
      await api.invitations.invite(data);
      toast({
        title: 'Invito inviato',
        description: `Email di invito inviata a ${data.email}`,
      });
      reset();
      onOpenChange(false);
      onSuccess?.();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : 'Errore nell\'invio dell\'invito';
      toast({
        title: 'Errore',
        description: message,
        variant: 'destructive',
      });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[425px]">
        <DialogHeader>
          <DialogTitle className="font-quicksand flex items-center gap-2">
            <Mail className="h-5 w-5 text-amber-500" />
            Invita Utente
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="email">Email</Label>
            <Input
              id="email"
              type="email"
              placeholder="utente@esempio.com"
              {...register('email')}
            />
            {errors.email && (
              <p className="text-sm text-red-500">{errors.email.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="displayName">Nome</Label>
            <Input
              id="displayName"
              placeholder="Mario Rossi"
              {...register('displayName')}
            />
            {errors.displayName && (
              <p className="text-sm text-red-500">{errors.displayName.message}</p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="role">Ruolo</Label>
            <Select
              defaultValue="user"
              onValueChange={(value) => setValue('role', value as InviteUserRequest['role'])}
            >
              <SelectTrigger>
                <SelectValue placeholder="Seleziona ruolo" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="user">User</SelectItem>
                <SelectItem value="editor">Editor</SelectItem>
                <SelectItem value="creator">Creator</SelectItem>
                <SelectItem value="admin">Admin</SelectItem>
              </SelectContent>
            </Select>
            {errors.role && (
              <p className="text-sm text-red-500">{errors.role.message}</p>
            )}
          </div>

          <DialogFooter>
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>
              Annulla
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Invia Invito
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 2: Write test**

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { InviteUserModal } from '@/components/admin/users/InviteUserModal';

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    invitations: {
      invite: vi.fn().mockResolvedValue({ invitationId: '123', email: 'test@example.com', expiresAt: new Date().toISOString() }),
    },
  }),
}));

vi.mock('@/hooks/use-toast', () => ({
  useToast: () => ({ toast: vi.fn() }),
}));

describe('InviteUserModal', () => {
  it('renders form fields when open', () => {
    render(<InviteUserModal open={true} onOpenChange={vi.fn()} />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/nome/i)).toBeInTheDocument();
    expect(screen.getByText(/invia invito/i)).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(<InviteUserModal open={false} onOpenChange={vi.fn()} />);
    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test**

Run: `cd apps/web && pnpm test -- --run __tests__/admin/users/InviteUserModal.test.tsx`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/admin/users/InviteUserModal.tsx
git add apps/web/__tests__/admin/users/InviteUserModal.test.tsx
git commit -m "feat(web): add InviteUserModal with form validation and API integration"
```

### Task 18: Add "Invita Utente" button to admin users page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Read the current file**

Read `apps/web/src/app/admin/(dashboard)/users/page.tsx` to understand current layout.

- [ ] **Step 2: Add invite button + modal**

Add state for modal visibility and the `InviteUserModal` component. Add a button in the page header area:

```tsx
import { InviteUserModal } from '@/components/admin/users/InviteUserModal';
import { UserPlus } from 'lucide-react';

// In the component:
const [inviteModalOpen, setInviteModalOpen] = useState(false);

// In the JSX header area:
<Button onClick={() => setInviteModalOpen(true)}>
  <UserPlus className="mr-2 h-4 w-4" />
  Invita Utente
</Button>

<InviteUserModal
  open={inviteModalOpen}
  onOpenChange={setInviteModalOpen}
/>
```

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/users/page.tsx
git commit -m "feat(web): add 'Invita Utente' button to admin users page"
```

### Task 19: Run full test suite

- [ ] **Step 1: Run backend tests**

Run: `cd apps/api && dotnet test --filter "BoundedContext=Authentication" -v minimal 2>&1 | tail -20`
Expected: All tests pass, including new invitation tests

- [ ] **Step 2: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run 2>&1 | tail -20`
Expected: All tests pass

- [ ] **Step 3: Run full build**

Run: `cd apps/api/src/Api && dotnet build && cd ../../../../apps/web && pnpm build`
Expected: Both builds succeed

- [ ] **Step 4: Final commit if any fixes needed**

```bash
git add -A
git commit -m "fix(auth): resolve any build/test issues from Phase 1 integration"
```
