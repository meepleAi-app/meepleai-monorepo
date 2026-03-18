# Admin Invitation Flow — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin creates user + sends invitation in one action; user sets password, auto-logs in, goes through reduced onboarding, finds pre-added/suggested games.

**Architecture:** Extend existing Authentication BC invitation system. Add `Pending` status to `UserAccountStatus` (moved to SharedKernel). Two-phase activation: sync user activation + async game suggestions via `Channel<T>`. New `GameSuggestion` entity in UserLibrary BC.

**Tech Stack:** .NET 9, ASP.NET Minimal APIs, MediatR (CQRS), EF Core + PostgreSQL, FluentValidation, xUnit + Moq + FluentAssertions, Next.js 16 (frontend)

**Spec:** `docs/superpowers/specs/2026-03-17-admin-invitation-flow-design.md`

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `SharedKernel/Domain/Enums/UserAccountStatus.cs` | Enum moved from Administration BC |
| `Authentication/Domain/Entities/InvitationGameSuggestion.cs` | Game suggestion value on invitation |
| `Authentication/Domain/Enums/GameSuggestionType.cs` | PreAdded / Suggested enum |
| `Authentication/Domain/Events/UserProvisionedEvent.cs` | Domain event for pending user creation |
| `Authentication/Domain/Events/UserActivatedFromInvitationEvent.cs` | Domain event for activation |
| `Authentication/Domain/Events/GameSuggestionFailedEvent.cs` | Monitoring event for retry exhaustion |
| `Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommand.cs` | Command record |
| `Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommandValidator.cs` | Validator |
| `Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommandHandler.cs` | Handler |
| `Authentication/Application/Commands/Invitation/BatchProvisionCommand.cs` | JSON batch command |
| `Authentication/Application/Commands/Invitation/BatchProvisionCommandHandler.cs` | JSON batch handler |
| `Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommand.cs` | Command record |
| `Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommandValidator.cs` | Validator |
| `Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommandHandler.cs` | Handler |
| `Authentication/Application/Queries/Invitation/ValidateInvitationTokenQuery.cs` | Query + handler |
| `Authentication/Application/DTOs/GameSuggestionDto.cs` | DTO for game suggestions |
| `Authentication/Application/DTOs/BatchInvitationItemDto.cs` | DTO for batch items |
| `Authentication/Application/DTOs/BatchInvitationResultDto.cs` | DTO for batch results |
| `Authentication/Application/DTOs/InvitationValidationDto.cs` | DTO for token validation |
| `Authentication/Application/DTOs/ActivationResultDto.cs` | DTO for activation result |
| `Authentication/Infrastructure/Services/GameSuggestionChannel.cs` | Channel<T> wrapper |
| `Infrastructure/BackgroundServices/InvitationCleanupService.cs` | Cleanup pending users |
| `Infrastructure/BackgroundServices/GameSuggestionProcessorService.cs` | Async game processor |
| `UserLibrary/Domain/Entities/GameSuggestion.cs` | Suggestion entity |
| `UserLibrary/Domain/Events/GameSuggestionAcceptedEvent.cs` | Domain event |
| `UserLibrary/Domain/Events/GamePreAddedToCollectionEvent.cs` | Cross-BC event |
| `UserLibrary/Domain/Events/GameSuggestedForUserEvent.cs` | Cross-BC event |
| `UserLibrary/Domain/Repositories/IGameSuggestionRepository.cs` | Repository interface |
| `UserLibrary/Infrastructure/Persistence/GameSuggestionRepository.cs` | Repository impl |
| `UserLibrary/Application/EventHandlers/GamePreAddedHandler.cs` | Handles pre-add event |
| `UserLibrary/Application/EventHandlers/GameSuggestedHandler.cs` | Handles suggestion event |
| `apps/web/src/app/(auth)/setup-account/page.tsx` | Setup account page |

### Modified Files

| File | Change |
|------|--------|
| `Administration/Domain/Enums/EntityStates.cs` | Remove `UserAccountStatus` (moved to SharedKernel) |
| `Authentication/Domain/Entities/User.cs` | Add `CreatePending()`, `ActivateFromInvitation()`, `InvitedByUserId`, `InvitationExpiresAt` |
| `Authentication/Domain/Entities/InvitationToken.cs` | Add `CustomMessage`, `PendingUserId`, `GameSuggestions`, `TimeProvider` to `Create()` |
| `Authentication/Domain/Repositories/IInvitationTokenRepository.cs` | Add `GetExpiredPendingAsync()` |
| `Authentication/Infrastructure/Repositories/InvitationTokenRepository.cs` | Update mapping for new fields |
| `Authentication/Application/DTOs/InvitationDto.cs` | Add `EmailSent`, `GameSuggestions` |
| `Authentication/Application/Commands/Invitation/BulkSendInvitationsCommandHandler.cs` | Extend for new provisioning flow |
| `Api/Routing/AuthenticationEndpoints.cs` | Add activate-account, validate-invitation endpoints |
| `Api/Routing/AdminEndpoints.cs` | Add admin invitation endpoints |
| `Services/IEmailService.cs` | Extend `SendInvitationEmailAsync` signature |
| `Services/EmailService.cs` | Update implementation |
| `Infrastructure/Persistence/AppDbContext.cs` | Add `InvitationGameSuggestions`, `GameSuggestions` DbSets |
| `Program.cs` | Register new services, background services, Channel |
| All files importing `UserAccountStatus` from Administration BC | Update import to SharedKernel |

---

## Chunk 1: Domain Model Foundation

### Task 1: Move UserAccountStatus to SharedKernel

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Domain/Enums/UserAccountStatus.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Administration/Domain/Enums/EntityStates.cs`
- Modify: All files importing `UserAccountStatus` from Administration BC

- [ ] **Step 1: Create SharedKernel enum with Pending value**

```csharp
// apps/api/src/Api/SharedKernel/Domain/Enums/UserAccountStatus.cs
namespace Api.SharedKernel.Domain.Enums;

public enum UserAccountStatus
{
    Active = 0,
    Suspended = 1,
    Banned = 2,
    Pending = 3
}
```

- [ ] **Step 2: Remove enum from Administration BC EntityStates.cs**

Remove the `UserAccountStatus` enum from `EntityStates.cs`. Keep any other enums in that file.

- [ ] **Step 3: Update all imports project-wide**

Search for `using Api.BoundedContexts.Administration.Domain.Enums` that reference `UserAccountStatus` and change to `using Api.SharedKernel.Domain.Enums`. Run:

```bash
cd apps/api/src/Api && grep -r "Administration.Domain.Enums" --include="*.cs" -l
```

**IMPORTANT**: `EntityStates.cs` contains OTHER enums too (`GamePublicationState`, `CollectionVisibility`, `DocumentProcessingState`). The grep will match ALL files using any of those enums. Only update files that specifically reference `UserAccountStatus`. Files using the other three enums must KEEP their `Administration.Domain.Enums` using (or add both usings if they also use `UserAccountStatus`).

Update each file's using statement. Key files likely include:
- `Authentication/Domain/Entities/User.cs`
- `Administration/Application/Commands/` (permission handlers)
- Any handler that checks `UserAccountStatus`

- [ ] **Step 4: Update CheckPermissionHandler and GetUserPermissionsHandler for Pending**

Find these handlers in Administration BC. Add a `Pending` case that denies all permissions / returns empty permissions. Pattern:

```csharp
UserAccountStatus.Pending => Array.Empty<Permission>(),
// or for switch expressions:
UserAccountStatus.Pending => false,
```

- [ ] **Step 5: Build and verify no compilation errors**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 6: Run existing tests to verify no regressions**

```bash
cd apps/api && dotnet test --filter "Category=Unit" --no-build
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "refactor(shared-kernel): move UserAccountStatus to SharedKernel, add Pending status"
```

---

### Task 2: Extend User Entity — Pending State

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/User.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserProvisionedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserActivatedFromInvitationEvent.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/UserPendingStateTests.cs`

- [ ] **Step 1: Write failing tests for CreatePending and ActivateFromInvitation**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/UserPendingStateTests.cs
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class UserPendingStateTests
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    [Fact]
    public void CreatePending_SetsCorrectState()
    {
        var email = Email.Create("test@example.com");
        var user = User.CreatePending(
            email, "Test User", Role.User, UserTier.Free,
            Guid.NewGuid(), DateTime.UtcNow.AddDays(7), _timeProvider);

        user.Status.Should().Be(UserAccountStatus.Pending);
        user.Email.Should().Be(email);
        user.DisplayName.Should().Be("Test User");
        user.InvitedByUserId.Should().NotBeNull();
        user.InvitationExpiresAt.Should().NotBeNull();
        user.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public void CreatePending_CannotAuthenticate()
    {
        var user = CreatePendingUser();
        user.CanAuthenticate().Should().BeFalse();
    }

    [Fact]
    public void ActivateFromInvitation_TransitionsPendingToActive()
    {
        var user = CreatePendingUser();
        var passwordHash = PasswordHash.Create("SecureP@ss123");

        user.ActivateFromInvitation(passwordHash, _timeProvider);

        user.Status.Should().Be(UserAccountStatus.Active);
        user.EmailVerified.Should().BeTrue();
        user.CanAuthenticate().Should().BeTrue();
    }

    [Fact]
    public void ActivateFromInvitation_ThrowsIfNotPending()
    {
        // Active user cannot be "activated from invitation"
        var email = Email.Create("active@example.com");
        var user = new User(Guid.NewGuid(), email, "Active",
            PasswordHash.Create("Pass1!"), Role.User);

        var act = () => user.ActivateFromInvitation(PasswordHash.Create("NewPass1!"), _timeProvider);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*");
    }

    [Fact]
    public void CreatePending_EmitsUserProvisionedEvent()
    {
        var user = CreatePendingUser();
        user.DomainEvents.Should().ContainSingle(e => e is UserProvisionedEvent);
    }

    [Fact]
    public void ActivateFromInvitation_EmitsUserActivatedEvent()
    {
        var user = CreatePendingUser();
        user.ClearDomainEvents(); // clear provisioned event
        user.ActivateFromInvitation(PasswordHash.Create("SecureP@ss123"), _timeProvider);
        user.DomainEvents.Should().ContainSingle(e => e is UserActivatedFromInvitationEvent);
    }

    private User CreatePendingUser()
    {
        return User.CreatePending(
            Email.Create("pending@example.com"), "Pending User",
            Role.User, UserTier.Free,
            Guid.NewGuid(), DateTime.UtcNow.AddDays(7), _timeProvider);
    }
}
```

- [ ] **Step 2: Run tests — verify they fail**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~UserPendingStateTests" --no-build 2>&1 | head -30
```

Expected: compilation errors (methods don't exist yet).

- [ ] **Step 3: Create domain events**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserProvisionedEvent.cs
namespace Api.BoundedContexts.Authentication.Domain.Events;

public sealed record UserProvisionedEvent(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    Guid InvitedByUserId) : IDomainEvent;
```

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/UserActivatedFromInvitationEvent.cs
namespace Api.BoundedContexts.Authentication.Domain.Events;

public sealed record UserActivatedFromInvitationEvent(
    Guid UserId,
    string Email,
    string Role,
    string Tier,
    Guid InvitedByUserId) : IDomainEvent;
```

- [ ] **Step 4: Add properties and methods to User entity**

Add to `User.cs`:

Properties:
```csharp
public Guid? InvitedByUserId { get; private set; }
public DateTime? InvitationExpiresAt { get; private set; }
```

Factory method:
```csharp
public static User CreatePending(
    Email email, string displayName, Role role, UserTier tier,
    Guid invitedByUserId, DateTime expiresAt, TimeProvider timeProvider)
{
    var user = new User
    {
        Id = Guid.NewGuid(),
        Email = email,
        DisplayName = displayName,
        Role = role,
        Tier = tier,
        Status = UserAccountStatus.Pending,
        InvitedByUserId = invitedByUserId,
        InvitationExpiresAt = expiresAt,
        EmailVerified = false,
        CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    };
    user.AddDomainEvent(new UserProvisionedEvent(
        user.Id, email.Value, displayName, role.ToString(), tier.ToString(), invitedByUserId));
    return user;
}
```

Activation method:
```csharp
public void ActivateFromInvitation(PasswordHash passwordHash, TimeProvider timeProvider)
{
    if (Status != UserAccountStatus.Pending)
        throw new InvalidOperationException("Only Pending users can be activated from invitation");

    PasswordHash = passwordHash;
    Status = UserAccountStatus.Active;
    EmailVerified = true;
    EmailVerifiedAt = timeProvider.GetUtcNow().UtcDateTime;

    AddDomainEvent(new UserActivatedFromInvitationEvent(
        Id, Email.Value, Role.ToString(), Tier.ToString(), InvitedByUserId!.Value));
}
```

Update `CanAuthenticate()` if it doesn't already handle `Pending`:
```csharp
public bool CanAuthenticate() => Status == UserAccountStatus.Active;
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~UserPendingStateTests" -v n
```

Expected: All 6 tests PASS.

- [ ] **Step 6: Run full test suite to check regressions**

```bash
cd apps/api && dotnet test --filter "Category=Unit&BoundedContext=Authentication" --no-build
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(auth): add Pending state to User entity with CreatePending and ActivateFromInvitation"
```

---

### Task 3: Extend InvitationToken Entity

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/InvitationToken.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/InvitationGameSuggestion.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/GameSuggestionType.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/InvitationTokenExtendedTests.cs`

- [ ] **Step 1: Create GameSuggestionType enum**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/GameSuggestionType.cs
namespace Api.BoundedContexts.Authentication.Domain.Enums;

public enum GameSuggestionType
{
    PreAdded = 0,
    Suggested = 1
}
```

- [ ] **Step 2: Create InvitationGameSuggestion entity**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/InvitationGameSuggestion.cs
namespace Api.BoundedContexts.Authentication.Domain.Entities;

public sealed class InvitationGameSuggestion
{
    public Guid Id { get; private set; }
    public Guid InvitationTokenId { get; private set; }
    public Guid GameId { get; private set; }
    public GameSuggestionType Type { get; private set; }

    private InvitationGameSuggestion() { } // EF Core

    public static InvitationGameSuggestion Create(Guid invitationTokenId, Guid gameId, GameSuggestionType type)
    {
        return new InvitationGameSuggestion
        {
            Id = Guid.NewGuid(),
            InvitationTokenId = invitationTokenId,
            GameId = gameId,
            Type = type
        };
    }
}
```

- [ ] **Step 3: Write failing tests for InvitationToken extensions**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/InvitationTokenExtendedTests.cs
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class InvitationTokenExtendedTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 3, 17, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Create_WithTimeProvider_SetsCorrectExpiry()
    {
        var token = InvitationToken.Create(
            Email.Create("test@example.com"), Role.User, Guid.NewGuid(),
            "Welcome!", 14, _timeProvider, "tokenHash123");

        token.ExpiresAt.Should().Be(new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc));
        token.CustomMessage.Should().Be("Welcome!");
        token.PendingUserId.Should().NotBeEmpty();
    }

    [Fact]
    public void Create_WithGameSuggestions_AttachesSuggestions()
    {
        var token = InvitationToken.Create(
            Email.Create("test@example.com"), Role.User, Guid.NewGuid(),
            null, 7, _timeProvider, "tokenHash123");

        var gameId = Guid.NewGuid();
        token.AddGameSuggestion(gameId, GameSuggestionType.PreAdded);
        token.AddGameSuggestion(Guid.NewGuid(), GameSuggestionType.Suggested);

        token.GameSuggestions.Should().HaveCount(2);
        token.GameSuggestions.First().GameId.Should().Be(gameId);
        token.GameSuggestions.First().Type.Should().Be(GameSuggestionType.PreAdded);
    }
}
```

- [ ] **Step 4: Extend InvitationToken entity**

Add to `InvitationToken.cs`:

Properties:
```csharp
public string? CustomMessage { get; private set; }
public Guid? PendingUserId { get; private set; }
private readonly List<InvitationGameSuggestion> _gameSuggestions = new();
public IReadOnlyList<InvitationGameSuggestion> GameSuggestions => _gameSuggestions.AsReadOnly();
```

Update or add overloaded `Create` factory:
```csharp
public static InvitationToken Create(
    Email email, Role role, Guid pendingUserId,
    string? customMessage, int expiresInDays, TimeProvider timeProvider, string tokenHash)
{
    var token = new InvitationToken
    {
        Id = Guid.NewGuid(),
        Email = email,
        Role = role,
        TokenHash = tokenHash,
        PendingUserId = pendingUserId,
        CustomMessage = customMessage,
        Status = InvitationStatus.Pending,
        ExpiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(expiresInDays),
        CreatedAt = timeProvider.GetUtcNow().UtcDateTime
    };
    return token;
}
```

Add method:
```csharp
public void AddGameSuggestion(Guid gameId, GameSuggestionType type)
{
    _gameSuggestions.Add(InvitationGameSuggestion.Create(Id, gameId, type));
}
```

- [ ] **Step 5: Run tests — verify they pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~InvitationTokenExtendedTests" -v n
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(auth): extend InvitationToken with CustomMessage, PendingUserId, GameSuggestions, TimeProvider"
```

---

### Task 4: Create GameSuggestion Entity in UserLibrary BC

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/GameSuggestion.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameSuggestionAcceptedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IGameSuggestionRepository.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/UserLibrary/Domain/Entities/GameSuggestionTests.cs`

- [ ] **Step 1: Write failing tests**

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class GameSuggestionTests
{
    [Fact]
    public void Create_SetsProperties()
    {
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        var timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var suggestion = GameSuggestion.Create(userId, gameId, adminId, "invitation", timeProvider);

        suggestion.UserId.Should().Be(userId);
        suggestion.GameId.Should().Be(gameId);
        suggestion.SuggestedByUserId.Should().Be(adminId);
        suggestion.Source.Should().Be("invitation");
        suggestion.IsDismissed.Should().BeFalse();
        suggestion.IsAccepted.Should().BeFalse();
    }

    [Fact]
    public void Accept_SetsIsAccepted_EmitsEvent()
    {
        var suggestion = GameSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invitation", new FakeTimeProvider(DateTimeOffset.UtcNow));

        suggestion.Accept();

        suggestion.IsAccepted.Should().BeTrue();
        suggestion.DomainEvents.Should().ContainSingle(e => e is GameSuggestionAcceptedEvent);
    }

    [Fact]
    public void Dismiss_SetsIsDismissed()
    {
        var suggestion = GameSuggestion.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "invitation", new FakeTimeProvider(DateTimeOffset.UtcNow));

        suggestion.Dismiss();

        suggestion.IsDismissed.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Implement GameSuggestion entity**

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Entities/GameSuggestion.cs
namespace Api.BoundedContexts.UserLibrary.Domain.Entities;

public sealed class GameSuggestion : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public Guid GameId { get; private set; }
    public Guid SuggestedByUserId { get; private set; }
    public string? Source { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsDismissed { get; private set; }
    public bool IsAccepted { get; private set; }

    private GameSuggestion() { }

    public static GameSuggestion Create(Guid userId, Guid gameId, Guid suggestedByUserId, string? source, TimeProvider timeProvider)
    {
        return new GameSuggestion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            SuggestedByUserId = suggestedByUserId,
            Source = source,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime,
            IsDismissed = false,
            IsAccepted = false
        };
    }

    public void Accept()
    {
        IsAccepted = true;
        RaiseDomainEvent(new GameSuggestionAcceptedEvent(Id, UserId, GameId));
    }

    public void Dismiss()
    {
        IsDismissed = true;
    }
}
```

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Events/GameSuggestionAcceptedEvent.cs
namespace Api.BoundedContexts.UserLibrary.Domain.Events;

public sealed record GameSuggestionAcceptedEvent(Guid SuggestionId, Guid UserId, Guid GameId) : IDomainEvent;
```

```csharp
// apps/api/src/Api/BoundedContexts/UserLibrary/Domain/Repositories/IGameSuggestionRepository.cs
namespace Api.BoundedContexts.UserLibrary.Domain.Repositories;

public interface IGameSuggestionRepository
{
    Task<GameSuggestion?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<GameSuggestion>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(GameSuggestion suggestion, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid userId, Guid gameId, CancellationToken ct = default);
}
```

- [ ] **Step 3: Run tests — verify pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~GameSuggestionTests" -v n
```

- [ ] **Step 4: Commit**

```bash
git add -A && git commit -m "feat(user-library): add GameSuggestion entity with Accept/Dismiss and repository interface"
```

---

### Task 5: EF Core Migration — New Tables and Columns

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Persistence/AppDbContext.cs` (or per-BC DbContext)
- Modify: EF configuration files for InvitationToken, User, new entities
- Create: Migration file

- [ ] **Step 1: Add DbSet and configure InvitationGameSuggestion**

Add `DbSet<InvitationGameSuggestion>` and configure the entity in the appropriate `OnModelCreating` or configuration class:

```csharp
builder.Entity<InvitationGameSuggestion>(e =>
{
    e.ToTable("InvitationGameSuggestions");
    e.HasKey(x => x.Id);
    e.Property(x => x.GameId).IsRequired();
    e.Property(x => x.Type).IsRequired();
    e.HasOne<InvitationToken>()
        .WithMany(t => t.GameSuggestions)
        .HasForeignKey(x => x.InvitationTokenId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 2: Configure User new columns**

```csharp
builder.Entity<User>(e =>
{
    // Add to existing configuration:
    e.Property(x => x.InvitedByUserId);
    e.Property(x => x.InvitationExpiresAt);
});
```

- [ ] **Step 3: Configure InvitationToken new columns**

```csharp
builder.Entity<InvitationToken>(e =>
{
    // Add to existing configuration:
    e.Property(x => x.CustomMessage).HasMaxLength(500);
    e.Property(x => x.PendingUserId);
    e.HasMany(x => x.GameSuggestions)
        .WithOne()
        .HasForeignKey(x => x.InvitationTokenId)
        .OnDelete(DeleteBehavior.Cascade);
});
```

- [ ] **Step 4: Configure GameSuggestion (UserLibrary BC)**

```csharp
builder.Entity<GameSuggestion>(e =>
{
    e.ToTable("GameSuggestions");
    e.HasKey(x => x.Id);
    e.Property(x => x.UserId).IsRequired();
    e.Property(x => x.GameId).IsRequired();
    e.Property(x => x.SuggestedByUserId).IsRequired();
    e.Property(x => x.Source).HasMaxLength(50);
    e.Property(x => x.CreatedAt).IsRequired();
    e.Property(x => x.IsDismissed).HasDefaultValue(false);
    e.Property(x => x.IsAccepted).HasDefaultValue(false);
});
```

- [ ] **Step 5: Create migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddInvitationFlowTables
```

- [ ] **Step 6: Review generated migration SQL**

Open the generated migration file and verify:
- `InvitedByUserId` and `InvitationExpiresAt` added to Users
- `CustomMessage`, `PendingUserId` added to InvitationTokens (**NOTE: `ExpiresAt` already exists on InvitationTokens from a prior migration — do NOT add it again**)
- `InvitationGameSuggestions` table with cascade delete on `InvitationTokenId`
- `GameSuggestions` table
- `Pending = 3` added to `UserAccountStatus` enum column

- [ ] **Step 7: Apply migration locally**

```bash
cd apps/api/src/Api && dotnet ef database update
```

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat(db): add migration for invitation flow tables and columns"
```

---

## Chunk 2: Commands, Handlers & Background Services

### Task 6: ProvisionAndInviteUserCommand + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/GameSuggestionDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/InvitationDto.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Invitation/ProvisionAndInviteUserCommandHandlerTests.cs`

- [ ] **Step 1: Create DTOs**

```csharp
// GameSuggestionDto.cs
namespace Api.BoundedContexts.Authentication.Application.DTOs;
public sealed record GameSuggestionDto(Guid GameId, string Type);
```

Update `InvitationDto.cs` — add `bool EmailSent` and `List<GameSuggestionDto> GameSuggestions` fields.

- [ ] **Step 2: Create command record**

```csharp
// ProvisionAndInviteUserCommand.cs
namespace Api.BoundedContexts.Authentication.Application.Commands.Invitation;

internal sealed record ProvisionAndInviteUserCommand(
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    string? CustomMessage,
    int ExpiresInDays,
    List<GameSuggestionDto> GameSuggestions,
    Guid InvitedByUserId
) : ICommand<InvitationDto>;
```

- [ ] **Step 3: Create validator**

```csharp
// ProvisionAndInviteUserCommandValidator.cs
internal sealed class ProvisionAndInviteUserCommandValidator : AbstractValidator<ProvisionAndInviteUserCommand>
{
    private static readonly string[] ValidRoles = { "User", "Editor", "Admin" };
    private static readonly string[] ValidTiers = { "Free", "Premium", "Pro" };
    private static readonly string[] ValidSuggestionTypes = { "PreAdded", "Suggested" };

    public ProvisionAndInviteUserCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().Length(2, 100);
        RuleFor(x => x.Role).NotEmpty()
            .Must(r => ValidRoles.Contains(r, StringComparer.OrdinalIgnoreCase));
        RuleFor(x => x.Tier).NotEmpty()
            .Must(t => ValidTiers.Contains(t, StringComparer.OrdinalIgnoreCase));
        RuleFor(x => x.CustomMessage).MaximumLength(500);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 30);
        RuleFor(x => x.GameSuggestions).Must(g => g == null || g.Count <= 20)
            .WithMessage("Maximum 20 game suggestions");
        RuleForEach(x => x.GameSuggestions).ChildRules(g =>
        {
            g.RuleFor(s => s.GameId).NotEmpty();
            g.RuleFor(s => s.Type).Must(t => ValidSuggestionTypes.Contains(t, StringComparer.OrdinalIgnoreCase));
        });
    }
}
```

- [ ] **Step 4: Write failing handler tests**

Test key scenarios:
- Happy path: creates pending user + token + sends email
- Duplicate email: throws ConflictException
- Email failure: returns `EmailSent = false`, user still created

```csharp
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ProvisionAndInviteUserCommandHandlerTests
{
    // ... mock setup following existing AcceptInvitationCommandHandlerTests pattern

    [Fact]
    public async Task Handle_ValidCommand_CreatesPendingUserAndToken()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _invitationRepoMock.Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);

        var command = new ProvisionAndInviteUserCommand(
            "new@example.com", "New User", "User", "Free",
            "Welcome!", 14, new List<GameSuggestionDto>(), Guid.NewGuid());

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Email.Should().Be("new@example.com");
        result.Status.Should().Be("Pending");
        result.EmailSent.Should().BeTrue();
        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Status == UserAccountStatus.Pending), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsConflict()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateActiveUser());

        var command = new ProvisionAndInviteUserCommand(
            "existing@example.com", "Dup", "User", "Free", null, 7, new(), Guid.NewGuid());

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_EmailFailure_ReturnsEmailSentFalse()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);
        _invitationRepoMock.Setup(r => r.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((InvitationToken?)null);
        _emailServiceMock.Setup(e => e.SendInvitationEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("SMTP down"));

        var command = new ProvisionAndInviteUserCommand(
            "new@example.com", "User", "User", "Free", null, 7, new(), Guid.NewGuid());

        var result = await _handler.Handle(command, CancellationToken.None);

        result.EmailSent.Should().BeFalse();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 5: Implement handler**

Follow existing `SendInvitationCommandHandler` pattern:
1. Check email uniqueness (user + pending invitation)
2. Generate token (RandomNumberGenerator.GetBytes(32) → Base64 → SHA256)
3. `User.CreatePending(...)` with TimeProvider
4. `InvitationToken.Create(...)` with TimeProvider
5. Add game suggestions
6. `SaveChangesAsync()`
7. Try/catch email send → set `EmailSent`
8. Return `InvitationDto`

- [ ] **Step 6: Run tests — verify pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ProvisionAndInviteUserCommandHandlerTests" -v n
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(auth): add ProvisionAndInviteUserCommand with handler and validator"
```

---

### Task 7: ActivateInvitedAccountCommand + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/ActivationResultDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Services/GameSuggestionChannel.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Invitation/ActivateInvitedAccountCommandHandlerTests.cs`

- [ ] **Step 1: Create ActivationResultDto**

```csharp
namespace Api.BoundedContexts.Authentication.Application.DTOs;
public sealed record ActivationResultDto(string SessionToken, bool RequiresOnboarding);
```

- [ ] **Step 2: Create GameSuggestionChannel (Channel<T> wrapper)**

```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Services/GameSuggestionChannel.cs
namespace Api.BoundedContexts.Authentication.Infrastructure.Services;

public sealed record GameSuggestionEvent(
    Guid UserId,
    Guid InvitedByUserId,
    IReadOnlyList<InvitationGameSuggestion> Suggestions);

public sealed class GameSuggestionChannel
{
    private readonly Channel<GameSuggestionEvent> _channel;

    public GameSuggestionChannel()
    {
        _channel = Channel.CreateUnbounded<GameSuggestionEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelWriter<GameSuggestionEvent> Writer => _channel.Writer;
    public ChannelReader<GameSuggestionEvent> Reader => _channel.Reader;
}
```

- [ ] **Step 3: Create command + validator**

```csharp
// ActivateInvitedAccountCommand.cs
internal sealed record ActivateInvitedAccountCommand(
    string Token,
    string Password
) : ICommand<ActivationResultDto>;
```

```csharp
// ActivateInvitedAccountCommandValidator.cs
// Reuse password rules from AcceptInvitationCommandValidator
internal sealed class ActivateInvitedAccountCommandValidator : AbstractValidator<ActivateInvitedAccountCommand>
{
    public ActivateInvitedAccountCommandValidator()
    {
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain at least one lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain at least one special character");
    }
}
```

- [ ] **Step 4: Write failing handler tests**

Key scenarios: valid activation, expired token, double activation, Pending user not found.

- [ ] **Step 5: Implement handler**

Phase 1 (sync): validate token → find user → `ActivateFromInvitation` → mark token accepted → create session → `SaveChangesAsync` → return result.
Phase 2 (async): write to `GameSuggestionChannel` after successful save.

- [ ] **Step 6: Run tests — verify pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ActivateInvitedAccountCommandHandlerTests" -v n
```

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(auth): add ActivateInvitedAccountCommand with two-phase activation and Channel dispatch"
```

---

### Task 8: Replace ValidateInvitationTokenQuery + Add Admin Read Queries

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/InvitationValidationDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/Invitation/ValidateInvitationTokenQuery.cs` (REPLACE existing — old one returns `ValidateInvitationTokenResponse(Valid, Role, ExpiresAt)`)
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/Invitation/ValidateInvitationTokenQueryHandler.cs` (REPLACE existing handler)
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs` (update existing `MapValidateInvitationEndpoint` to use new response shape)
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/Invitation/GetPendingInvitationsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/Invitation/GetInvitationByIdQuery.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Queries/Invitation/ValidateInvitationTokenQueryTests.cs`

**IMPORTANT**: A `ValidateInvitationTokenQuery` already exists in the codebase with a different response shape (`ValidateInvitationTokenResponse(Valid, Role, ExpiresAt)`). We are REPLACING it with the new security-hardened version that returns uniform errors. Delete the old `ValidateInvitationTokenResponse` record and update the existing endpoint in `AuthenticationEndpoints.cs`.

- [ ] **Step 1: Create InvitationValidationDto**

```csharp
namespace Api.BoundedContexts.Authentication.Application.DTOs;
public sealed record InvitationValidationDto(
    bool IsValid, string? Email, string? DisplayName, string? ErrorReason);
```

- [ ] **Step 2: Replace existing ValidateInvitationTokenQuery and handler**

Replace the existing query record to return `InvitationValidationDto` instead of the old response. Replace the handler to use uniform error responses ("invalid" for expired/revoked/not-found, "already_used" for accepted tokens).

```csharp
internal sealed record ValidateInvitationTokenQuery(string Token) : IQuery<InvitationValidationDto>;

internal sealed class ValidateInvitationTokenQueryHandler
    : IQueryHandler<ValidateInvitationTokenQuery, InvitationValidationDto>
{
    // Hash token → lookup → check status → return uniform errors
    // Delete old ValidateInvitationTokenResponse record
}
```

- [ ] **Step 3: Update AuthenticationEndpoints.cs**

Update the existing `MapValidateInvitationEndpoint` to use the new `InvitationValidationDto` response. Remove any `using` alias for the old response type.

- [ ] **Step 4: Write failing tests**

Test all 5 states: valid token, expired → "invalid", revoked → "invalid", not found → "invalid", already used → "already_used". Verify Email/DisplayName are null when `IsValid=false` (except "already_used").

- [ ] **Step 5: Implement GetPendingInvitationsQuery and GetInvitationByIdQuery**

```csharp
// GetPendingInvitationsQuery.cs — returns List<InvitationDto> filtered by status
// Supports tab filtering: Pending, Accepted, Expired, Revoked
internal sealed record GetPendingInvitationsQuery(string? StatusFilter) : IQuery<List<InvitationDto>>;

// GetInvitationByIdQuery.cs — returns single InvitationDto with GameSuggestions
internal sealed record GetInvitationByIdQuery(Guid Id) : IQuery<InvitationDto>;
```

These are needed by the admin endpoints in Task 10 Step 2.

- [ ] **Step 6: Run tests — verify pass**

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(auth): replace ValidateInvitationTokenQuery with uniform errors, add admin read queries"
```

---

### Task 9: Background Services

**Files:**
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/InvitationCleanupService.cs`
- Create: `apps/api/src/Api/Infrastructure/BackgroundServices/GameSuggestionProcessorService.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GamePreAddedHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/UserLibrary/Application/EventHandlers/GameSuggestedHandler.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/BackgroundServices/InvitationCleanupServiceTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/BackgroundServices/GameSuggestionProcessorServiceTests.cs`

- [ ] **Step 1: Write failing tests for InvitationCleanupService**

Test: expired pending users are hard-deleted, tokens marked expired, non-expired users untouched.

- [ ] **Step 2: Implement InvitationCleanupService**

Follow `SessionAutoSaveBackgroundService` pattern:
- `IServiceScopeFactory` + `ILogger` + `TimeProvider`
- While loop with `Task.Delay(TimeSpan.FromHours(1))`
- Acquire Redis distributed lock (`invitation-cleanup-lock`, TTL 5 minutes) before processing. If lock held, skip cycle and log info. Use `IConnectionMultiplexer` (already in DI from Redis/HybridCache setup).
- Query expired pending users
- Hard delete user + mark token expired
- Emit `InvitationExpiredEvent` with denormalized email+displayName for audit
- Log cleanup count
- Release lock in finally block

- [ ] **Step 3: Run tests — verify pass**

- [ ] **Step 4: Write failing tests for GameSuggestionProcessorService**

Test: processes events from channel, retries on failure, logs on retry exhaustion.

- [ ] **Step 5: Implement GameSuggestionProcessorService**

```csharp
// Reads from GameSuggestionChannel.Reader
// For each suggestion: dispatch to IMediator (GamePreAddedToCollectionEvent or GameSuggestedForUserEvent)
// Retry with backoff: 1s, 2s, 4s
// On exhaustion: log error
```

- [ ] **Step 6: Implement UserLibrary event handlers**

`GamePreAddedHandler`: adds game to collection (reuse existing add-to-collection logic), idempotent.
`GameSuggestedHandler`: creates `GameSuggestion` entity, idempotent (check exists first).

- [ ] **Step 7: Run all tests — verify pass**

- [ ] **Step 8: Commit**

```bash
git add -A && git commit -m "feat(services): add InvitationCleanupService and GameSuggestionProcessorService with UserLibrary handlers"
```

---

## Chunk 3: API Endpoints, DI Registration & Frontend

### Task 10: API Endpoints

**Files:**
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`
- Modify: `apps/api/src/Api/Routing/AdminUserEndpoints.cs` (existing admin invitation routing lives here via `MapInvitationEndpoints`)

- [ ] **Step 1: Add public auth endpoints**

In `AuthenticationEndpoints.cs`, add:

```csharp
// POST /api/v1/auth/activate-account
private static void MapActivateAccountEndpoint(RouteGroupBuilder group)
{
    group.MapPost("/auth/activate-account", async (
        ActivateInvitedAccountCommand command,
        IMediator mediator,
        HttpContext context,
        CancellationToken ct) =>
    {
        var result = await mediator.Send(command, ct);
        // Set session cookie (follow existing login pattern)
        CookieHelpers.WriteSessionCookie(context, result.SessionToken, /* expiry */);
        return Results.Ok(new { sessionToken = result.SessionToken, requiresOnboarding = result.RequiresOnboarding });
    })
    .AllowAnonymous()
    .WithName("ActivateInvitedAccount");
}

// GET /api/v1/auth/validate-invitation?token=x
private static void MapValidateInvitationEndpoint(RouteGroupBuilder group)
{
    group.MapGet("/auth/validate-invitation", async (
        [FromQuery] string token,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var result = await mediator.Send(new ValidateInvitationTokenQuery(token), ct);
        return Results.Ok(result);
    })
    .AllowAnonymous()
    .WithName("ValidateInvitationToken");
}
```

- [ ] **Step 2: Add admin invitation endpoints**

In `AdminUserEndpoints.cs`, extend the existing `MapInvitationEndpoints` method with:
- `POST /api/v1/admin/invitations` → `ProvisionAndInviteUserCommand`
- `POST /api/v1/admin/invitations/batch` → `BatchProvisionCommand` (JSON)
- `POST /api/v1/admin/invitations/batch/csv` → `BatchProvisionCommand` (CSV)
- `POST /api/v1/admin/invitations/{id}/resend` → `ResendInvitationCommand`
- `DELETE /api/v1/admin/invitations/{id}` → `RevokeInvitationCommand`
- `GET /api/v1/admin/invitations` → `GetPendingInvitationsQuery`
- `GET /api/v1/admin/invitations/{id}` → `GetInvitationByIdQuery`

All require Admin role via `.RequireAuthorization("Admin")`.

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat(api): add invitation flow API endpoints for auth and admin"
```

---

### Task 11: DI Registration & Program.cs

**Files:**
- Modify: `apps/api/src/Api/Program.cs`
- Modify: Authentication BC DI extension
- Modify: UserLibrary BC DI extension

- [ ] **Step 1: Register GameSuggestionChannel as singleton**

```csharp
builder.Services.AddSingleton<GameSuggestionChannel>();
```

- [ ] **Step 2: Register background services**

```csharp
builder.Services.AddHostedService<InvitationCleanupService>();
builder.Services.AddHostedService<GameSuggestionProcessorService>();
```

- [ ] **Step 3: Register GameSuggestionRepository**

```csharp
builder.Services.AddScoped<IGameSuggestionRepository, GameSuggestionRepository>();
```

- [ ] **Step 4: Build and verify**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(di): register invitation flow services, channel, and background workers"
```

---

### Task 12: Extend IEmailService for Invitation

**Files:**
- Modify: `apps/api/src/Api/Services/IEmailService.cs`
- Modify: `apps/api/src/Api/Services/EmailService.cs`

- [ ] **Step 1: Add extended invitation email method**

```csharp
// In IEmailService.cs — add overload or new method:
Task SendInvitationEmailAsync(
    string toEmail,
    string displayName,
    string role,
    string token,
    string invitedByName,
    string? customMessage,
    DateTime expiresAt,
    CancellationToken ct = default);
```

- [ ] **Step 2: Implement in EmailService.cs**

Build email body with:
- MeepleAI logo header
- Custom message (HTML-escaped) in quote block
- Platform intro text
- CTA button linking to `/setup-account?token={token}`
- Expiry notice
- Plain-text fallback

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat(email): extend invitation email with custom message, platform intro, and expiry notice"
```

---

### Task 13: Frontend — Setup Account Page

**Files:**
- Create: `apps/web/src/app/(auth)/setup-account/page.tsx`

- [ ] **Step 1: Create setup-account page**

```tsx
// apps/web/src/app/(auth)/setup-account/page.tsx
'use client';

import { useSearchParams, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
// Follow existing auth page patterns (login, register)

export default function SetupAccountPage() {
    const searchParams = useSearchParams();
    const token = searchParams.get('token');
    const router = useRouter();

    const [validation, setValidation] = useState(null);
    const [password, setPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    // 1. On mount: GET /api/v1/auth/validate-invitation?token=xxx
    // 2. If valid: show form with pre-filled email (readonly)
    // 3. If invalid: show error message
    // 4. On submit: POST /api/v1/auth/activate-account
    // 5. On success: redirect to onboarding

    // Follow existing auth page styling and patterns
}
```

- [ ] **Step 2: Add route to auth layout if needed**

Verify the `(auth)` layout handles `/setup-account` correctly (public route, no auth required).

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "feat(web): add setup-account page for invitation activation flow"
```

---

## Chunk 4: Integration Testing & Final Wiring

### Task 14: Integration Tests

**Files:**
- Create: `apps/api/tests/Api.Tests/Integration/Authentication/InvitationFlowIntegrationTests.cs`

- [ ] **Step 1: Write end-to-end integration test**

```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public class InvitationFlowIntegrationTests : IClassFixture<TestDatabaseFixture>
{
    [Fact]
    public async Task FullFlow_ProvisionActivateAndVerifyGames()
    {
        // 1. Admin provisions user with game suggestions
        // 2. Verify: User exists with Pending status
        // 3. Activate account with valid token + password
        // 4. Verify: User is Active, EmailVerified, session created
        // 5. Verify: PreAdded games in collection (after async processing)
        // 6. Verify: Suggested games visible
    }

    [Fact]
    public async Task ExpiredToken_ReturnsInvalid()
    {
        // 1. Create invitation with 0-day expiry (already expired)
        // 2. Validate token → errorReason = "invalid"
    }

    [Fact]
    public async Task BatchWithPartialFailure_ReturnsCorrectResults()
    {
        // 1. Create existing user
        // 2. Batch: 2 new + 1 existing
        // 3. Verify: 2 succeeded, 1 failed
    }
}
```

- [ ] **Step 2: Run integration tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~InvitationFlowIntegrationTests" -v n
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "test(auth): add integration tests for invitation flow end-to-end"
```

---

### Task 15: Security Tests

**Files:**
- Create: `apps/api/tests/Api.Tests/Security/InvitationSecurityTests.cs`

- [ ] **Step 1: Write security tests**

```csharp
[Trait("Category", "Security")]
public class InvitationSecurityTests
{
    [Fact]
    public async Task NonAdminCannotCreateInvitation() { /* 403 */ }

    [Fact]
    public async Task WeakPasswordRejected() { /* validation error */ }

    [Fact]
    public async Task UniformErrorForExpiredRevokedNotFound() { /* all return "invalid" */ }

    [Fact]
    public async Task CsvInjectionSanitized() { /* =cmd formula in DisplayName */ }

    [Fact]
    public async Task XssInCustomMessageEscaped() { /* <script> tag */ }
}
```

- [ ] **Step 2: Run security tests**

```bash
cd apps/api && dotnet test --filter "Category=Security" -v n
```

- [ ] **Step 3: Commit**

```bash
git add -A && git commit -m "test(security): add invitation flow security tests for auth, XSS, CSV injection"
```

---

### Task 16: Batch Commands — JSON and CSV Paths

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/BatchProvisionCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/BatchProvisionCommandHandler.cs`
- Modify: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/Invitation/BulkSendInvitationsCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/Invitation/BatchProvisionCommandHandlerTests.cs`

The spec defines TWO batch modes: JSON body (`POST /admin/invitations/batch`) and CSV (`POST /admin/invitations/batch/csv`). The existing `BulkSendInvitationsCommand` handles CSV only.

- [ ] **Step 1: Create BatchProvisionCommand for JSON path**

```csharp
// BatchProvisionCommand.cs — NEW command for JSON batch
internal sealed record BatchProvisionCommand(
    List<BatchInvitationItemDto> Invitations,
    Guid InvitedByUserId
) : ICommand<BatchInvitationResultDto>;
```

- [ ] **Step 2: Implement BatchProvisionCommandHandler**

```csharp
// Iterates Invitations, constructs ProvisionAndInviteUserCommand per item,
// dispatches via IMediator.Send(). Collects successes/failures.
// Does NOT abort on individual failures.
```

- [ ] **Step 3: Write tests for BatchProvisionCommand**

Test: partial failure (2 succeed, 1 duplicate), all succeed, all fail. Verify `EmailSent` tracking.

- [ ] **Step 4: Update BulkSendInvitationsCommandHandler for CSV path**

Update the existing CSV handler to dispatch `ProvisionAndInviteUserCommand` instead of `SendInvitationCommand` for each CSV row. Follow existing CSV parsing logic but construct the new command.

- [ ] **Step 5: Run all batch tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~BulkSend|FullyQualifiedName~BatchProvision" -v n
```

- [ ] **Step 6: Commit**

```bash
git add -A && git commit -m "feat(auth): add BatchProvisionCommand for JSON path, update CSV bulk handler"
```

---

### Task 17: Final Verification & Cleanup

- [ ] **Step 1: Full build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 2: Run all unit tests**

```bash
cd apps/api && dotnet test --filter "Category=Unit"
```

- [ ] **Step 3: Run all integration tests**

```bash
cd apps/api && dotnet test --filter "Category=Integration"
```

- [ ] **Step 4: Run frontend build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 5: Verify API starts and endpoints respond**

```bash
cd apps/api/src/Api && dotnet run &
# Test endpoints:
curl -s http://localhost:8080/api/v1/auth/validate-invitation?token=test | jq .
```

- [ ] **Step 6: Final commit**

```bash
git add -A && git commit -m "chore: final wiring and verification for invitation flow"
```

---

## Task Dependency Graph

```
Task 1 (SharedKernel enum) ──→ Task 2 (User Pending) ──→ Task 6 (Provision cmd)
                                                      ──→ Task 7 (Activate cmd) ──→ Task 9 (Background services)
                              Task 3 (InvitationToken) ──→ Task 6
                              Task 4 (GameSuggestion) ──→ Task 9
                              Task 5 (Migration) ──→ Task 6, Task 7
Task 6 + Task 7 + Task 8 ──→ Task 10 (Endpoints) ──→ Task 11 (DI) ──→ Task 14 (Integration tests)
Task 12 (Email) ──→ Task 6
Task 13 (Frontend) — independent, can run in parallel
Task 15 (Security tests) — after Task 10
Task 16 (Batch update) — after Task 6
Task 17 (Final) — last
```

## Estimated Tasks: 17 tasks, ~85 steps
