# Invite-Only Registration (Beta0) — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gate self-registration behind a runtime toggle (`Registration:PublicEnabled`), provide a "Request Access" flow for visitors, and give admins tools to review, approve/reject requests and toggle registration mode.

**Architecture:** New `AccessRequest` aggregate in Authentication BC with event-driven invitation on approval. Registration guard as endpoint filter. Frontend conditional rendering on `/register`. Admin panel for request management and mode toggle.

**Tech Stack:** .NET 9, EF Core, MediatR, FluentValidation, Next.js 16, React 19, React Query, Tailwind 4, shadcn/ui, Vitest, Playwright

**Spec:** `docs/superpowers/specs/2026-03-14-invite-only-registration-design.md`

---

## File Map

### Backend — New Files

| File | Responsibility |
|------|---------------|
| `BoundedContexts/Authentication/Domain/Entities/AccessRequest.cs` | Aggregate root: state machine, factory method |
| `BoundedContexts/Authentication/Domain/Enums/AccessRequestStatus.cs` | Pending, Approved, Rejected |
| `BoundedContexts/Authentication/Domain/Repositories/IAccessRequestRepository.cs` | Repository interface |
| `BoundedContexts/Authentication/Domain/Events/AccessRequestCreatedEvent.cs` | Domain event |
| `BoundedContexts/Authentication/Domain/Events/AccessRequestApprovedEvent.cs` | Domain event |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommand.cs` | Command record |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommandHandler.cs` | Handler |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommandValidator.cs` | FluentValidation |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/ApproveAccessRequestCommand.cs` | Command record |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/ApproveAccessRequestCommandHandler.cs` | Handler |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommand.cs` | Command record |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs` | Handler |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/BulkApproveAccessRequestsCommand.cs` | Command record |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/BulkApproveAccessRequestsCommandHandler.cs` | Handler |
| `BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestsQuery.cs` | Query + handler |
| `BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestStatsQuery.cs` | Query + handler |
| `BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestByIdQuery.cs` | Query + handler |
| `BoundedContexts/Authentication/Application/Queries/AccessRequest/GetRegistrationModeQuery.cs` | Query + handler |
| `BoundedContexts/Authentication/Application/Commands/AccessRequest/SetRegistrationModeCommand.cs` | Command + handler |
| `BoundedContexts/Authentication/Application/DTOs/AccessRequestDto.cs` | DTO record |
| `BoundedContexts/Authentication/Application/DTOs/AccessRequestStatsDto.cs` | Stats DTO |
| `BoundedContexts/Authentication/Application/DTOs/BulkApproveResultDto.cs` | Bulk result DTO |
| `BoundedContexts/Authentication/Application/EventHandlers/AccessRequestApprovedEventHandler.cs` | Creates invitation on approval |
| `BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs` | Notifies admins |
| `BoundedContexts/Authentication/Infrastructure/Repositories/AccessRequestRepository.cs` | EF Core implementation |
| `Routing/AccessRequestEndpoints.cs` | Public + admin endpoints |

### Backend — Modified Files

| File | Change |
|------|--------|
| `Infrastructure/Persistence/MeepleAiDbContext.cs` | Add `DbSet<AccessRequest>`, entity config |
| `Routing/AuthenticationEndpoints.cs` | Add registration guard filter to `POST /register` |
| `Infrastructure/DependencyInjection.cs` | Register `IAccessRequestRepository` |

### Frontend — New Files

| File | Responsibility |
|------|---------------|
| `src/lib/api/clients/accessRequestsClient.ts` | API client for all access request endpoints |
| `src/components/auth/RequestAccessForm.tsx` | Request access form component |
| `src/app/admin/(dashboard)/users/access-requests/page.tsx` | Admin access requests list page |
| `src/components/admin/access-requests/ApproveRejectDialog.tsx` | Reject reason dialog |
| `src/components/admin/settings/RegistrationModeToggle.tsx` | Toggle switch component |

### Frontend — Modified Files

| File | Change |
|------|--------|
| `src/app/(auth)/register/page.tsx` | Conditional rendering based on registration mode |
| `src/lib/api/clients/index.ts` | Export accessRequestsClient |
| Admin sidebar nav component | Add "Access Requests" link with badge |

### Test Files

| File | Scope |
|------|-------|
| `tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/AccessRequestTests.cs` | Entity unit tests |
| `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RequestAccessCommandHandlerTests.cs` | Handler tests |
| `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/ApproveAccessRequestCommandHandlerTests.cs` | Handler tests |
| `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RejectAccessRequestCommandHandlerTests.cs` | Handler tests |
| `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/BulkApproveAccessRequestsCommandHandlerTests.cs` | Handler tests |
| `apps/web/__tests__/components/auth/RequestAccessForm.test.tsx` | Component tests |
| `apps/web/__tests__/app/register/page.test.tsx` | Conditional rendering tests |
| `apps/web/e2e/invite-only-registration.spec.ts` | E2E tests |

---

## Chunk 1: Domain Layer

### Task 1: AccessRequestStatus Enum

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/AccessRequestStatus.cs`

- [ ] **Step 1: Create the enum**

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Enums;

public enum AccessRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Enums/AccessRequestStatus.cs
git commit -m "feat(auth): add AccessRequestStatus enum"
```

---

### Task 2: Domain Events

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequestCreatedEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequestApprovedEvent.cs`
- Reference: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/PasswordChangedEvent.cs` (pattern)

- [ ] **Step 1: Create AccessRequestCreatedEvent**

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Events;

public sealed class AccessRequestCreatedEvent : DomainEventBase
{
    public Guid AccessRequestId { get; }
    public string Email { get; }

    public AccessRequestCreatedEvent(Guid accessRequestId, string email)
    {
        AccessRequestId = accessRequestId;
        Email = email;
    }
}
```

- [ ] **Step 2: Create AccessRequestApprovedEvent**

```csharp
namespace Api.BoundedContexts.Authentication.Domain.Events;

public sealed class AccessRequestApprovedEvent : DomainEventBase
{
    public Guid AccessRequestId { get; }
    public string Email { get; }
    public Guid ApprovedByUserId { get; }

    public AccessRequestApprovedEvent(Guid accessRequestId, string email, Guid approvedByUserId)
    {
        AccessRequestId = accessRequestId;
        Email = email;
        ApprovedByUserId = approvedByUserId;
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Events/AccessRequest*.cs
git commit -m "feat(auth): add AccessRequest domain events"
```

---

### Task 3: AccessRequest Entity

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/AccessRequest.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/AccessRequestTests.cs`
- Reference: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/InvitationToken.cs` (pattern)

- [ ] **Step 1: Write failing tests for the entity**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class AccessRequestTests
{
    [Fact]
    public void Create_WithValidEmail_ReturnsPendingAccessRequest()
    {
        var request = AccessRequest.Create("test@example.com");

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("test@example.com", request.Email);
        Assert.Equal(AccessRequestStatus.Pending, request.Status);
        Assert.True(request.RequestedAt <= DateTime.UtcNow);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.ReviewedBy);
        Assert.Null(request.RejectionReason);
        Assert.Null(request.InvitationId);
    }

    [Fact]
    public void Create_NormalizesEmailToLowercase()
    {
        var request = AccessRequest.Create("Test@EXAMPLE.com");
        Assert.Equal("test@example.com", request.Email);
    }

    [Fact]
    public void Approve_FromPending_SetsApprovedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Approve(adminId);

        Assert.Equal(AccessRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ReviewedAt);
        Assert.Equal(adminId, request.ReviewedBy);
    }

    [Fact]
    public void Reject_FromPending_SetsRejectedState()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Reject(adminId, "Not in beta group");

        Assert.Equal(AccessRequestStatus.Rejected, request.Status);
        Assert.NotNull(request.ReviewedAt);
        Assert.Equal(adminId, request.ReviewedBy);
        Assert.Equal("Not in beta group", request.RejectionReason);
    }

    [Fact]
    public void Reject_WithoutReason_SetsNullReason()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        Assert.Null(request.RejectionReason);
    }

    [Fact]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Approve_WhenRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Approve(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Reject(Guid.NewGuid()));
    }

    [Fact]
    public void Reject_WhenApproved_ThrowsInvalidOperationException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => request.Reject(Guid.NewGuid()));
    }

    [Fact]
    public void Approve_PublishesAccessRequestApprovedEvent()
    {
        var request = AccessRequest.Create("test@example.com");
        var adminId = Guid.NewGuid();

        request.Approve(adminId);

        var domainEvent = request.DomainEventBases
            .OfType<AccessRequestApprovedEvent>()
            .SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(request.Id, domainEvent.AccessRequestId);
        Assert.Equal("test@example.com", domainEvent.Email);
        Assert.Equal(adminId, domainEvent.ApprovedByUserId);
    }

    [Fact]
    public void Create_PublishesAccessRequestCreatedEvent()
    {
        var request = AccessRequest.Create("test@example.com");

        var domainEvent = request.DomainEventBases
            .OfType<AccessRequestCreatedEvent>()
            .SingleOrDefault();
        Assert.NotNull(domainEvent);
        Assert.Equal(request.Id, domainEvent.AccessRequestId);
        Assert.Equal("test@example.com", domainEvent.Email);
    }

    [Fact]
    public void SetInvitationId_StoresCorrelationId()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        var invitationId = Guid.NewGuid();

        request.SetInvitationId(invitationId);

        Assert.Equal(invitationId, request.InvitationId);
    }

    [Fact]
    public void Reject_WithReasonExceeding500Chars_ThrowsArgumentException()
    {
        var request = AccessRequest.Create("test@example.com");
        var longReason = new string('a', 501);

        Assert.Throws<ArgumentException>(() => request.Reject(Guid.NewGuid(), longReason));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~AccessRequestTests" -v minimal
```

Expected: FAIL — `AccessRequest` class does not exist.

- [ ] **Step 3: Implement the AccessRequest entity**

Follow `InvitationToken.cs` pattern: private setters, factory method, state transition methods, domain events.

```csharp
using Api.BoundedContexts.Authentication.Domain.Enums;
using Api.BoundedContexts.Authentication.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

internal sealed class AccessRequest : AggregateRoot<Guid>
{
    public string Email { get; private set; } = null!;
    public AccessRequestStatus Status { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public string? RejectionReason { get; private set; }
    public Guid? InvitationId { get; private set; }

    private AccessRequest() { } // EF Core

    public static AccessRequest Create(string email)
    {
        var request = new AccessRequest
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Status = AccessRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        request.AddDomainEventBase(new AccessRequestCreatedEvent(request.Id, request.Email));
        return request;
    }

    public void Approve(Guid adminId)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve access request in '{Status}' status. Only Pending requests can be approved.");

        Status = AccessRequestStatus.Approved;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEventBase(new AccessRequestApprovedEvent(Id, Email, adminId));
    }

    public void Reject(Guid adminId, string? reason = null)
    {
        if (Status != AccessRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject access request in '{Status}' status. Only Pending requests can be rejected.");

        if (reason is not null && reason.Length > 500)
            throw new ArgumentException("Rejection reason cannot exceed 500 characters.", nameof(reason));

        Status = AccessRequestStatus.Rejected;
        ReviewedAt = DateTime.UtcNow;
        ReviewedBy = adminId;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetInvitationId(Guid invitationId)
    {
        InvitationId = invitationId;
        UpdatedAt = DateTime.UtcNow;
    }

    // Hydration for EF Core
    public static AccessRequest CreateForHydration() => new();
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~AccessRequestTests" -v minimal
```

Expected: ALL PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Entities/AccessRequest.cs \
        tests/Api.Tests/BoundedContexts/Authentication/Domain/Entities/AccessRequestTests.cs
git commit -m "feat(auth): add AccessRequest entity with state machine and tests"
```

---

### Task 4: Repository Interface

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/IAccessRequestRepository.cs`
- Reference: `apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/IInvitationTokenRepository.cs`

- [ ] **Step 1: Create repository interface**

```csharp
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Enums;

namespace Api.BoundedContexts.Authentication.Domain.Repositories;

// IRepository<AccessRequest, Guid> already provides: GetByIdAsync, AddAsync, UpdateAsync
public interface IAccessRequestRepository : IRepository<AccessRequest, Guid>
{
    Task<AccessRequest?> GetPendingByEmailAsync(string email, CancellationToken ct = default);
    Task<IReadOnlyList<AccessRequest>> GetByStatusAsync(
        AccessRequestStatus? status, int page, int pageSize,
        CancellationToken ct = default);
    Task<int> CountByStatusAsync(AccessRequestStatus status, CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Domain/Repositories/IAccessRequestRepository.cs
git commit -m "feat(auth): add IAccessRequestRepository interface"
```

---

## Chunk 2: Application Layer — Commands

### Task 5: DTOs

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/AccessRequestDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/AccessRequestStatsDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/BulkApproveResultDto.cs`

- [ ] **Step 1: Create all three DTOs**

```csharp
// AccessRequestDto.cs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record AccessRequestDto(
    Guid Id,
    string Email,
    string Status,
    DateTime RequestedAt,
    DateTime? ReviewedAt,
    Guid? ReviewedBy,
    string? RejectionReason,
    Guid? InvitationId,
    DateTime CreatedAt);

// AccessRequestStatsDto.cs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record AccessRequestStatsDto(
    int Pending,
    int Approved,
    int Rejected,
    int Total);

// BulkApproveResultDto.cs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record BulkApproveResultDto(
    int Processed,
    int Succeeded,
    int Failed,
    IReadOnlyList<BulkApproveItemResult> Results);

public record BulkApproveItemResult(
    Guid Id,
    string Status,
    string? Error = null);
```

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/AccessRequest*.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Application/DTOs/BulkApproveResultDto.cs
git commit -m "feat(auth): add AccessRequest DTOs"
```

---

### Task 6: RequestAccessCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommandValidator.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RequestAccessCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RequestAccessCommandHandlerTests.cs`

- [ ] **Step 1: Create the command record**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record RequestAccessCommand(string Email) : ICommand<Unit>;
```

- [ ] **Step 2: Create the validator**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RequestAccessCommandValidator : AbstractValidator<RequestAccessCommand>
{
    public RequestAccessCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.");
    }
}
```

- [ ] **Step 3: Write failing handler tests**

```csharp
namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RequestAccessCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _accessRequestRepoMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly RequestAccessCommandHandler _handler;

    public RequestAccessCommandHandlerTests()
    {
        _accessRequestRepoMock = new Mock<IAccessRequestRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _handler = new RequestAccessCommandHandler(
            _accessRequestRepoMock.Object,
            _userRepoMock.Object);
    }

    [Fact]
    public async Task Handle_NewEmail_CreatesAccessRequest()
    {
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessRequest?)null);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new RequestAccessCommand("new@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.Is<AccessRequest>(r => r.Email == "new@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ExistingAccount_DoesNotCreateRequest()
    {
        var existingUser = TestUserFactory.CreateUser("existing@example.com");
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var command = new RequestAccessCommand("existing@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.IsAny<AccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AlreadyPending_DoesNotCreateDuplicate()
    {
        var pendingRequest = AccessRequest.Create("pending@example.com");
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync("pending@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingRequest);

        var command = new RequestAccessCommand("pending@example.com");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.AddAsync(It.IsAny<AccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NormalizesEmail()
    {
        _accessRequestRepoMock
            .Setup(x => x.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessRequest?)null);
        _userRepoMock
            .Setup(x => x.GetByEmailAsync(It.IsAny<Email>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new RequestAccessCommand("TEST@Example.COM");
        await _handler.Handle(command, CancellationToken.None);

        _accessRequestRepoMock.Verify(
            x => x.GetPendingByEmailAsync("test@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~RequestAccessCommandHandlerTests" -v minimal
```

- [ ] **Step 5: Implement the handler**

Key behavior: Always performs ALL DB lookups (email exists? pending exists?) regardless of outcome for timing equalization. Never throws — always returns `Unit.Value`.

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class RequestAccessCommandHandler : ICommandHandler<RequestAccessCommand, Unit>
{
    private readonly IAccessRequestRepository _accessRequestRepository;
    private readonly IUserRepository _userRepository;

    public RequestAccessCommandHandler(
        IAccessRequestRepository accessRequestRepository,
        IUserRepository userRepository)
    {
        _accessRequestRepository = accessRequestRepository;
        _userRepository = userRepository;
    }

    public async Task<Unit> Handle(RequestAccessCommand request, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // Always perform both lookups for timing equalization
        var existingUserTask = _userRepository.GetByEmailAsync(
            Email.Create(normalizedEmail), ct);
        var pendingRequestTask = _accessRequestRepository.GetPendingByEmailAsync(
            normalizedEmail, ct);

        var existingUser = await existingUserTask.ConfigureAwait(false);
        var pendingRequest = await pendingRequestTask.ConfigureAwait(false);

        // Silent skip: existing account or already pending
        if (existingUser is not null || pendingRequest is not null)
            return Unit.Value;

        var accessRequest = Domain.Entities.AccessRequest.Create(normalizedEmail);
        await _accessRequestRepository.AddAsync(accessRequest, ct).ConfigureAwait(false);

        return Unit.Value;
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~RequestAccessCommandHandlerTests" -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/Request*.cs \
        tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RequestAccessCommandHandlerTests.cs
git commit -m "feat(auth): add RequestAccessCommand with enumeration prevention"
```

---

### Task 7: ApproveAccessRequestCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/ApproveAccessRequestCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/ApproveAccessRequestCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/ApproveAccessRequestCommandHandlerTests.cs`

- [ ] **Step 1: Create the command**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record ApproveAccessRequestCommand(Guid Id, Guid AdminId) : ICommand<Unit>;
```

- [ ] **Step 2: Write failing handler tests**

Test idempotency semantics: domain throws on non-Pending, handler catches for idempotent cases (already Approved → 200), rethrows for illegal transitions (Rejected → 409).

```csharp
namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ApproveAccessRequestCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly ApproveAccessRequestCommandHandler _handler;

    public ApproveAccessRequestCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _handler = new ApproveAccessRequestCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_PendingRequest_ApprovesSuccessfully()
    {
        var request = AccessRequest.Create("test@example.com");
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(AccessRequestStatus.Approved, request.Status);
        _repoMock.Verify(x => x.UpdateAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_AlreadyApproved_ReturnsSuccessWithoutModification()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid()); // Already approved
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        // Should NOT throw — idempotent no-op
        await _handler.Handle(command, CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<AccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RejectedRequest_ThrowsConflictException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        var command = new ApproveAccessRequestCommand(request.Id, Guid.NewGuid());
        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonExistentId_ThrowsNotFoundException()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessRequest?)null);

        var command = new ApproveAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid());
        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(command, CancellationToken.None));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ApproveAccessRequestCommandHandlerTests" -v minimal
```

- [ ] **Step 4: Implement the handler**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal class ApproveAccessRequestCommandHandler : ICommandHandler<ApproveAccessRequestCommand, Unit>
{
    private readonly IAccessRequestRepository _repository;

    public ApproveAccessRequestCommandHandler(IAccessRequestRepository repository)
    {
        _repository = repository;
    }

    public async Task<Unit> Handle(ApproveAccessRequestCommand request, CancellationToken ct)
    {
        var accessRequest = await _repository.GetByIdAsync(request.Id, ct).ConfigureAwait(false)
            ?? throw new NotFoundException($"Access request '{request.Id}' not found.");

        if (accessRequest.Status == AccessRequestStatus.Approved)
            return Unit.Value; // Idempotent no-op

        try
        {
            accessRequest.Approve(request.AdminId);
        }
        catch (InvalidOperationException)
        {
            throw new ConflictException(
                $"Cannot approve access request in '{accessRequest.Status}' status.");
        }

        await _repository.UpdateAsync(accessRequest, ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~ApproveAccessRequestCommandHandlerTests" -v minimal
```

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/Approve*.cs \
        tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/ApproveAccessRequestCommandHandlerTests.cs
git commit -m "feat(auth): add ApproveAccessRequestCommand with idempotency"
```

---

### Task 8: RejectAccessRequestCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/RejectAccessRequestCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RejectAccessRequestCommandHandlerTests.cs`

- [ ] **Step 1: Create the command**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record RejectAccessRequestCommand(Guid Id, Guid AdminId, string? Reason = null) : ICommand<Unit>;
```

- [ ] **Step 2: Write failing tests**

Same idempotency pattern: Already Rejected → no-op, Approved → ConflictException.

```csharp
namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class RejectAccessRequestCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly RejectAccessRequestCommandHandler _handler;

    public RejectAccessRequestCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _handler = new RejectAccessRequestCommandHandler(_repoMock.Object);
    }

    [Fact]
    public async Task Handle_PendingRequest_RejectsWithReason()
    {
        var request = AccessRequest.Create("test@example.com");
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await _handler.Handle(
            new RejectAccessRequestCommand(request.Id, Guid.NewGuid(), "Not in beta"),
            CancellationToken.None);

        Assert.Equal(AccessRequestStatus.Rejected, request.Status);
        Assert.Equal("Not in beta", request.RejectionReason);
    }

    [Fact]
    public async Task Handle_AlreadyRejected_ReturnsSuccessWithoutModification()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Reject(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await _handler.Handle(
            new RejectAccessRequestCommand(request.Id, Guid.NewGuid()),
            CancellationToken.None);

        _repoMock.Verify(x => x.UpdateAsync(It.IsAny<AccessRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ApprovedRequest_ThrowsConflictException()
    {
        var request = AccessRequest.Create("test@example.com");
        request.Approve(Guid.NewGuid());
        _repoMock.Setup(x => x.GetByIdAsync(request.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        await Assert.ThrowsAsync<ConflictException>(
            () => _handler.Handle(
                new RejectAccessRequestCommand(request.Id, Guid.NewGuid()),
                CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonExistentId_ThrowsNotFoundException()
    {
        _repoMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessRequest?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => _handler.Handle(
                new RejectAccessRequestCommand(Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None));
    }
}
```

- [ ] **Step 3: Run tests to verify they fail, implement handler, run tests to verify they pass**

Handler follows same pattern as ApproveAccessRequestCommandHandler but checks for `AccessRequestStatus.Rejected` as idempotent state.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/Reject*.cs \
        tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/RejectAccessRequestCommandHandlerTests.cs
git commit -m "feat(auth): add RejectAccessRequestCommand with idempotency"
```

---

### Task 9: BulkApproveAccessRequestsCommand

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/BulkApproveAccessRequestsCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/BulkApproveAccessRequestsCommandHandler.cs`
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/BulkApproveAccessRequestsCommandHandlerTests.cs`

- [ ] **Step 1: Create the command**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record BulkApproveAccessRequestsCommand(
    IReadOnlyList<Guid> Ids,
    Guid AdminId) : ICommand<BulkApproveResultDto>;
```

- [ ] **Step 2: Write failing tests**

Key tests: max 25 limit, per-item results, mixed states, non-existent IDs.

```csharp
namespace Api.Tests.BoundedContexts.Authentication.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class BulkApproveAccessRequestsCommandHandlerTests
{
    private readonly Mock<IAccessRequestRepository> _repoMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly BulkApproveAccessRequestsCommandHandler _handler;

    public BulkApproveAccessRequestsCommandHandlerTests()
    {
        _repoMock = new Mock<IAccessRequestRepository>();
        _mediatorMock = new Mock<IMediator>();
        _handler = new BulkApproveAccessRequestsCommandHandler(
            _repoMock.Object, _mediatorMock.Object);
    }

    [Fact]
    public async Task Handle_ExceedsMaxBatchSize_ThrowsValidationException()
    {
        var ids = Enumerable.Range(0, 26).Select(_ => Guid.NewGuid()).ToList();
        var command = new BulkApproveAccessRequestsCommand(ids, Guid.NewGuid());

        await Assert.ThrowsAsync<ValidationException>(
            () => _handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_MixedStates_ReturnsPerItemResults()
    {
        var pending = AccessRequest.Create("a@test.com");
        var approved = AccessRequest.Create("b@test.com");
        approved.Approve(Guid.NewGuid());

        _repoMock.Setup(x => x.GetByIdAsync(pending.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pending);
        _repoMock.Setup(x => x.GetByIdAsync(approved.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(approved);

        var command = new BulkApproveAccessRequestsCommand(
            new[] { pending.Id, approved.Id }, Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(2, result.Succeeded); // Already approved = success (idempotent)
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task Handle_NonExistentId_ReportsFailureForThatItem()
    {
        var nonExistentId = Guid.NewGuid();
        _repoMock.Setup(x => x.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AccessRequest?)null);

        var command = new BulkApproveAccessRequestsCommand(
            new[] { nonExistentId }, Guid.NewGuid());
        var result = await _handler.Handle(command, CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal("Not found", result.Results[0].Error);
    }
}
```

- [ ] **Step 3: Run tests to verify they fail, implement handler, run tests to verify they pass**

Handler iterates IDs, dispatches individual `ApproveAccessRequestCommand` via MediatR, catches exceptions, collects per-item results.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/BulkApprove*.cs \
        tests/Api.Tests/BoundedContexts/Authentication/Application/Commands/BulkApproveAccessRequestsCommandHandlerTests.cs
git commit -m "feat(auth): add BulkApproveAccessRequestsCommand (max 25, per-item results)"
```

---

### Task 10: SetRegistrationModeCommand + GetRegistrationModeQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/SetRegistrationModeCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/GetRegistrationModeQuery.cs`

- [ ] **Step 1: Create SetRegistrationModeCommand**

This command delegates to the SystemConfiguration BC via MediatR `UpdateConfigValueCommand`. `IConfigurationService` is read-only — writes go through SystemConfiguration's own commands.

```csharp
namespace Api.BoundedContexts.Authentication.Application.Commands.AccessRequest;

internal record SetRegistrationModeCommand(bool Enabled, Guid AdminId) : ICommand<Unit>;

internal class SetRegistrationModeCommandHandler : ICommandHandler<SetRegistrationModeCommand, Unit>
{
    private readonly IMediator _mediator;

    public SetRegistrationModeCommandHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Unit> Handle(SetRegistrationModeCommand request, CancellationToken ct)
    {
        // Delegate to SystemConfiguration BC's own command via MediatR
        await _mediator.Send(
            new UpdateConfigValueCommand(
                "Registration:PublicEnabled",
                request.Enabled.ToString().ToLowerInvariant()),
            ct).ConfigureAwait(false);

        return Unit.Value;
    }
}
```

> **Note**: If `UpdateConfigValueCommand` does not exist, use `CreateConfigurationCommand` from the SystemConfiguration BC. Check `BoundedContexts/SystemConfiguration/Application/Commands/` for the exact command name and signature.

- [ ] **Step 2: Create GetRegistrationModeQuery**

```csharp
namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

public record GetRegistrationModeQuery : IQuery<RegistrationModeDto>;

public record RegistrationModeDto(bool PublicRegistrationEnabled);

internal class GetRegistrationModeQueryHandler : IQueryHandler<GetRegistrationModeQuery, RegistrationModeDto>
{
    private readonly IConfigurationService _configService;

    public GetRegistrationModeQueryHandler(IConfigurationService configService)
    {
        _configService = configService;
    }

    public async Task<RegistrationModeDto> Handle(GetRegistrationModeQuery request, CancellationToken ct)
    {
        // Fail closed: default false if config not found or service unreachable
        // GetValueAsync signature: GetValueAsync<T>(string key, T? defaultValue, string? environment = null)
        bool enabled;
        try
        {
            enabled = await _configService.GetValueAsync<bool?>(
                "Registration:PublicEnabled", false).ConfigureAwait(false) ?? false;
        }
        catch
        {
            enabled = false; // Fail closed
        }

        return new RegistrationModeDto(enabled);
    }
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Commands/AccessRequest/SetRegistrationMode*.cs \
        apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/GetRegistrationMode*.cs
git commit -m "feat(auth): add registration mode toggle command and query (fail-closed)"
```

---

### Task 11: Access Request Queries

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestStatsQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/GetAccessRequestByIdQuery.cs`

- [ ] **Step 1: Create queries with handlers**

Follow existing `GetInvitationsQuery` pattern. Defaults: page=1, pageSize=20 (max 100), sortBy=RequestedAt desc.

```csharp
// GetAccessRequestsQuery.cs
namespace Api.BoundedContexts.Authentication.Application.Queries.AccessRequest;

public record GetAccessRequestsQuery(
    string? Status = null,
    int Page = 1,
    int PageSize = 20) : IQuery<GetAccessRequestsResponse>;

public record GetAccessRequestsResponse(
    IReadOnlyList<AccessRequestDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

// GetAccessRequestStatsQuery.cs
public record GetAccessRequestStatsQuery : IQuery<AccessRequestStatsDto>;

// GetAccessRequestByIdQuery.cs
public record GetAccessRequestByIdQuery(Guid Id) : IQuery<AccessRequestDto>;
```

Handlers query `IAccessRequestRepository` and map to DTOs.

- [ ] **Step 2: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/Queries/AccessRequest/*.cs
git commit -m "feat(auth): add access request list, stats, and detail queries"
```

---

### Task 12: Event Handlers

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestApprovedEventHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequestCreatedEventHandler.cs`
- Reference: `apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/PasswordChangedEventHandler.cs`

- [ ] **Step 1: Create AccessRequestApprovedEventHandler**

Fires `SendInvitationCommand(email, "User", approvedByUserId)` — reuses existing invitation infrastructure.

```csharp
namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal class AccessRequestApprovedEventHandler : DomainEventHandlerBase<AccessRequestApprovedEvent>
{
    private readonly IMediator _mediator;
    private readonly IAccessRequestRepository _repository;
    private readonly ILogger<AccessRequestApprovedEventHandler> _logger;

    public AccessRequestApprovedEventHandler(
        IMediator mediator,
        IAccessRequestRepository repository,
        ILogger<AccessRequestApprovedEventHandler> logger)
    {
        _mediator = mediator;
        _repository = repository;
        _logger = logger;
    }

    protected override async Task HandleEventAsync(
        AccessRequestApprovedEvent domainEvent, CancellationToken ct)
    {
        try
        {
            var invitationResult = await _mediator.Send(
                new SendInvitationCommand(
                    domainEvent.Email,
                    "User",
                    domainEvent.ApprovedByUserId),
                ct).ConfigureAwait(false);

            // Update correlation ID
            var accessRequest = await _repository.GetByIdAsync(
                domainEvent.AccessRequestId, ct).ConfigureAwait(false);
            if (accessRequest is not null)
            {
                accessRequest.SetInvitationId(invitationResult.Id);
                await _repository.UpdateAsync(accessRequest, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create invitation for approved access request {AccessRequestId}, email {Email}",
                domainEvent.AccessRequestId, domainEvent.Email);
            // Approval stands. Admin can resend via invitation UI.
        }
    }

    protected override Guid GetUserId(AccessRequestApprovedEvent domainEvent)
        => domainEvent.ApprovedByUserId;

    protected override Dictionary<string, object> GetAuditMetadata(
        AccessRequestApprovedEvent domainEvent)
        => new()
        {
            ["AccessRequestId"] = domainEvent.AccessRequestId,
            ["Email"] = domainEvent.Email
        };
}
```

- [ ] **Step 2: Create AccessRequestCreatedEventHandler**

Sends notification to all admins. Uses existing notification patterns.

```csharp
namespace Api.BoundedContexts.Authentication.Application.EventHandlers;

internal class AccessRequestCreatedEventHandler : DomainEventHandlerBase<AccessRequestCreatedEvent>
{
    // TODO: Check BoundedContexts/UserNotifications/ for the correct notification service interface
    // Likely IPushNotificationService or similar. INotificationService does NOT exist.
    private readonly IPushNotificationService _notificationService;
    private readonly ILogger<AccessRequestCreatedEventHandler> _logger;

    // Constructor...

    protected override async Task HandleEventAsync(
        AccessRequestCreatedEvent domainEvent, CancellationToken ct)
    {
        try
        {
            await _notificationService.NotifyAdminsAsync(
                "New Access Request",
                $"New access request from {domainEvent.Email}",
                "/admin/users/access-requests",
                ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to notify admins about access request {AccessRequestId}",
                domainEvent.AccessRequestId);
            // Request creation still succeeds
        }
    }

    protected override Guid GetUserId(AccessRequestCreatedEvent domainEvent)
        => Guid.Empty; // System action, no user

    protected override Dictionary<string, object> GetAuditMetadata(
        AccessRequestCreatedEvent domainEvent)
        => new() { ["Email"] = domainEvent.Email };
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/Authentication/Application/EventHandlers/AccessRequest*.cs
git commit -m "feat(auth): add event handlers for access request approval and creation"
```

---

## Chunk 3: Infrastructure & Routing

### Task 13: EF Core Configuration + Repository + Migration

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Persistence/MeepleAiDbContext.cs`
- Create: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Repositories/AccessRequestRepository.cs`
- Modify: `apps/api/src/Api/Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Add DbSet and entity configuration to MeepleAiDbContext**

```csharp
// In MeepleAiDbContext.cs — add DbSet
public DbSet<AccessRequest> AccessRequests => Set<AccessRequest>();

// In OnModelCreating — add entity config
modelBuilder.Entity<AccessRequest>(entity =>
{
    entity.ToTable("access_requests");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
    entity.Property(e => e.Status)
        .HasConversion<string>()
        .HasMaxLength(20)
        .IsRequired();
    entity.Property(e => e.RejectionReason).HasMaxLength(500);
    entity.HasIndex(e => new { e.Email, e.Status })
        .HasFilter("\"Status\" = 'Pending'")
        .IsUnique();
});
```

- [ ] **Step 2: Implement AccessRequestRepository**

Follow `InvitationTokenRepository` pattern with EF Core queries.

- [ ] **Step 3: Register in DI**

```csharp
// In DependencyInjection.cs
services.AddScoped<IAccessRequestRepository, AccessRequestRepository>();
```

- [ ] **Step 4: Create migration**

```bash
cd apps/api/src/Api && dotnet ef migrations add AddAccessRequests
```

- [ ] **Step 5: Verify migration SQL looks correct**

Review generated migration file. Verify: table name, column types, unique index on (Email, Status) with Pending filter.

- [ ] **Step 6: Seed Registration:PublicEnabled config key**

Add to seed data: `Registration:PublicEnabled` = `false`.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Infrastructure/ \
        apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/ \
        apps/api/src/Api/Migrations/
git commit -m "feat(auth): add AccessRequest EF Core config, repository, and migration"
```

---

### Task 14: Registration Guard Endpoint Filter

**Files:**
- Create: `apps/api/src/Api/Routing/Filters/RequirePublicRegistrationFilter.cs`
- Modify: `apps/api/src/Api/Routing/AuthenticationEndpoints.cs`

- [ ] **Step 1: Create the endpoint filter**

Follows `RequireAdminSessionFilter` pattern. Reads config via query-side service. Short-circuits with 403.

```csharp
namespace Api.Routing.Filters;

public class RequirePublicRegistrationFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var configService = context.HttpContext.RequestServices
            .GetRequiredService<IConfigurationService>();

        // Fail closed: if config unreachable or missing, block registration
        bool publicEnabled;
        try
        {
            publicEnabled = await configService.GetValueAsync<bool?>(
                "Registration:PublicEnabled", false).ConfigureAwait(false) ?? false;
        }
        catch
        {
            publicEnabled = false; // Fail closed
        }

        if (!publicEnabled)
        {
            return Results.Json(
                new { message = "Registration is currently unavailable." },
                statusCode: 403);
        }

        return await next(context).ConfigureAwait(false);
    }
}
```

- [ ] **Step 2: Apply filter to register endpoint**

In `AuthenticationEndpoints.cs`, find `MapPost("/register"...)` and add:

```csharp
.AddEndpointFilter<RequirePublicRegistrationFilter>()
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/Filters/RequirePublicRegistrationFilter.cs \
        apps/api/src/Api/Routing/AuthenticationEndpoints.cs
git commit -m "feat(auth): add registration guard endpoint filter (fail-closed)"
```

---

### Task 15: Access Request Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/AccessRequestEndpoints.cs`

- [ ] **Step 1: Create endpoints file with public and admin routes**

```csharp
namespace Api.Routing;

public static class AccessRequestEndpoints
{
    public static void MapAccessRequestEndpoints(this IEndpointRouteBuilder app)
    {
        // Public endpoints
        var publicGroup = app.MapGroup("/api/v1/auth")
            .WithTags("Authentication");

        publicGroup.MapGet("/registration-mode", HandleGetRegistrationMode)
            .WithName("GetRegistrationMode")
            .RequireRateLimiting("AuthGeneral"); // 30/min

        publicGroup.MapPost("/request-access", HandleRequestAccess)
            .WithName("RequestAccess")
            .RequireRateLimiting("AuthRegister"); // 3/min

        // Admin endpoints
        var adminGroup = app.MapGroup("/api/v1/admin/access-requests")
            .WithTags("Admin - Access Requests");

        adminGroup.MapGet("/", HandleGetAccessRequests).WithName("GetAccessRequests");
        adminGroup.MapGet("/stats", HandleGetAccessRequestStats).WithName("GetAccessRequestStats");
        adminGroup.MapPost("/{id:guid}/approve", HandleApproveAccessRequest).WithName("ApproveAccessRequest");
        adminGroup.MapPost("/{id:guid}/reject", HandleRejectAccessRequest).WithName("RejectAccessRequest");
        adminGroup.MapPost("/bulk-approve", HandleBulkApprove).WithName("BulkApproveAccessRequests");

        // Registration mode toggle (admin)
        var settingsGroup = app.MapGroup("/api/v1/admin/settings")
            .WithTags("Admin - Settings");

        settingsGroup.MapPut("/registration-mode", HandleSetRegistrationMode)
            .WithName("SetRegistrationMode");
    }

    // Public handlers
    private static async Task<IResult> HandleGetRegistrationMode(
        IMediator mediator, CancellationToken ct)
    {
        var result = await mediator.Send(new GetRegistrationModeQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleRequestAccess(
        RequestAccessPayload payload,
        IMediator mediator,
        CancellationToken ct)
    {
        await mediator.Send(new RequestAccessCommand(payload.Email), ct).ConfigureAwait(false);
        // Always return 202 with identical message (enumeration prevention)
        return Results.Accepted(value: new
        {
            message = "If this email is eligible, you will receive an invitation when approved."
        });
    }

    // Admin handlers — all require RequireAdminSession()
    private static async Task<IResult> HandleGetAccessRequests(
        HttpContext context, IMediator mediator,
        [FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(
            new GetAccessRequestsQuery(status, page, Math.Min(pageSize, 100)), ct)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleGetAccessRequestStats(
        HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(new GetAccessRequestStatsQuery(), ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleApproveAccessRequest(
        Guid id, HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        await mediator.Send(
            new ApproveAccessRequestCommand(id, session!.UserId), ct).ConfigureAwait(false);
        return Results.Ok(new { status = "approved" });
    }

    private static async Task<IResult> HandleRejectAccessRequest(
        Guid id, RejectPayload? payload,
        HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        await mediator.Send(
            new RejectAccessRequestCommand(id, session!.UserId, payload?.Reason), ct)
            .ConfigureAwait(false);
        return Results.Ok(new { status = "rejected" });
    }

    private static async Task<IResult> HandleBulkApprove(
        BulkApprovePayload payload,
        HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        var result = await mediator.Send(
            new BulkApproveAccessRequestsCommand(payload.Ids, session!.UserId), ct)
            .ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static async Task<IResult> HandleSetRegistrationMode(
        SetRegistrationModePayload payload,
        HttpContext context, IMediator mediator, CancellationToken ct)
    {
        var (authorized, session, error) = context.RequireAdminSession();
        if (!authorized) return error!;

        await mediator.Send(
            new SetRegistrationModeCommand(payload.Enabled, session!.UserId), ct)
            .ConfigureAwait(false);
        return Results.Ok(new { publicRegistrationEnabled = payload.Enabled });
    }
}

// Payload records
internal record RequestAccessPayload(string Email);
internal record RejectPayload(string? Reason);
internal record BulkApprovePayload(IReadOnlyList<Guid> Ids);
internal record SetRegistrationModePayload(bool Enabled);
```

- [ ] **Step 2: Register endpoints in Program.cs or routing setup**

Find where `MapAuthenticationEndpoints()` is called and add `app.MapAccessRequestEndpoints()`.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/AccessRequestEndpoints.cs
git commit -m "feat(auth): add access request public and admin endpoints"
```

---

## Chunk 4: Frontend

### Task 16: API Client

**Files:**
- Create: `apps/web/src/lib/api/clients/accessRequestsClient.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts`
- Reference: `apps/web/src/lib/api/clients/invitationsClient.ts` (pattern)

- [ ] **Step 1: Create the API client**

```typescript
// accessRequestsClient.ts
import type { HttpClient } from '../httpClient';

export interface AccessRequestDto {
  id: string;
  email: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  requestedAt: string;
  reviewedAt: string | null;
  reviewedBy: string | null;
  rejectionReason: string | null;
  invitationId: string | null;
}

export interface AccessRequestStats {
  pending: number;
  approved: number;
  rejected: number;
  total: number;
}

export interface BulkApproveResult {
  processed: number;
  succeeded: number;
  failed: number;
  results: Array<{ id: string; status: string; error?: string }>;
}

export interface RegistrationMode {
  publicRegistrationEnabled: boolean;
}

export interface GetAccessRequestsParams {
  status?: string;
  page?: number;
  pageSize?: number;
}

export interface GetAccessRequestsResponse {
  items: AccessRequestDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface CreateAccessRequestsClientParams {
  httpClient: HttpClient;
}

export function createAccessRequestsClient({ httpClient }: CreateAccessRequestsClientParams) {
  return {
    // Public
    getRegistrationMode(): Promise<RegistrationMode> {
      return httpClient.get('/api/v1/auth/registration-mode');
    },

    requestAccess(email: string): Promise<{ message: string }> {
      return httpClient.post('/api/v1/auth/request-access', { email });
    },

    // Admin
    getAccessRequests(params?: GetAccessRequestsParams): Promise<GetAccessRequestsResponse> {
      const searchParams = new URLSearchParams();
      if (params?.status) searchParams.set('status', params.status);
      if (params?.page) searchParams.set('page', params.page.toString());
      if (params?.pageSize) searchParams.set('pageSize', params.pageSize.toString());
      return httpClient.get(`/api/v1/admin/access-requests?${searchParams}`);
    },

    getAccessRequestStats(): Promise<AccessRequestStats> {
      return httpClient.get('/api/v1/admin/access-requests/stats');
    },

    approveAccessRequest(id: string): Promise<void> {
      return httpClient.post(`/api/v1/admin/access-requests/${id}/approve`);
    },

    rejectAccessRequest(id: string, reason?: string): Promise<void> {
      return httpClient.post(`/api/v1/admin/access-requests/${id}/reject`, { reason });
    },

    bulkApproveAccessRequests(ids: string[]): Promise<BulkApproveResult> {
      return httpClient.post('/api/v1/admin/access-requests/bulk-approve', { ids });
    },

    setRegistrationMode(enabled: boolean): Promise<void> {
      return httpClient.put('/api/v1/admin/settings/registration-mode', { enabled });
    },
  };
}
```

- [ ] **Step 2: Export from index**

Add to `apps/web/src/lib/api/clients/index.ts`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/accessRequestsClient.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(web): add accessRequests API client"
```

---

### Task 17: RequestAccessForm Component

**Files:**
- Create: `apps/web/src/components/auth/RequestAccessForm.tsx`
- Create: `apps/web/__tests__/components/auth/RequestAccessForm.test.tsx`

- [ ] **Step 1: Write failing component tests**

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { RequestAccessForm } from '@/components/auth/RequestAccessForm';

// Mock API client
const mockRequestAccess = vi.fn();
vi.mock('@/lib/api', () => ({
  useApi: () => ({
    accessRequests: { requestAccess: mockRequestAccess },
  }),
}));

describe('RequestAccessForm', () => {
  beforeEach(() => {
    mockRequestAccess.mockReset();
  });

  it('renders email input and submit button', () => {
    render(<RequestAccessForm />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /request access/i })).toBeInTheDocument();
  });

  it('shows validation error for empty email', async () => {
    render(<RequestAccessForm />);
    fireEvent.click(screen.getByRole('button', { name: /request access/i }));
    await waitFor(() => {
      expect(screen.getByText(/valid email/i)).toBeInTheDocument();
    });
  });

  it('shows success message after submission', async () => {
    mockRequestAccess.mockResolvedValueOnce({ message: 'ok' });
    render(<RequestAccessForm />);

    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'test@example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /request access/i }));

    await waitFor(() => {
      expect(screen.getByText(/submitted/i)).toBeInTheDocument();
    });
    // Form should be replaced by success message
    expect(screen.queryByLabelText(/email/i)).not.toBeInTheDocument();
  });

  it('disables form during submission', async () => {
    mockRequestAccess.mockImplementation(() => new Promise(() => {})); // Never resolves
    render(<RequestAccessForm />);

    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'test@example.com' },
    });
    fireEvent.click(screen.getByRole('button', { name: /request access/i }));

    await waitFor(() => {
      expect(screen.getByLabelText(/email/i)).toBeDisabled();
    });
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
cd apps/web && pnpm test -- RequestAccessForm
```

- [ ] **Step 3: Implement RequestAccessForm**

Same card layout as `RegisterForm`. React Hook Form + Zod validation. Uses `useMutation` from React Query. Shows success state with `aria-live="polite"`.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/auth/RequestAccessForm.tsx \
        apps/web/__tests__/components/auth/RequestAccessForm.test.tsx
git commit -m "feat(web): add RequestAccessForm component with success/error states"
```

---

### Task 18: Conditional Register Page

**Files:**
- Modify: `apps/web/src/app/(auth)/register/page.tsx`
- Create: `apps/web/__tests__/app/register/page.test.tsx`

- [ ] **Step 1: Write failing tests for conditional rendering**

```typescript
import { render, screen, waitFor } from '@testing-library/react';
import RegisterPage from '@/app/(auth)/register/page';

const mockGetRegistrationMode = vi.fn();
vi.mock('@/lib/api', () => ({
  useApi: () => ({
    accessRequests: { getRegistrationMode: mockGetRegistrationMode },
  }),
}));

describe('RegisterPage', () => {
  it('shows RegisterForm when public registration enabled', async () => {
    mockGetRegistrationMode.mockResolvedValueOnce({ publicRegistrationEnabled: true });
    render(<RegisterPage />);

    await waitFor(() => {
      expect(screen.getByText(/create.*account/i)).toBeInTheDocument();
    });
  });

  it('shows RequestAccessForm when public registration disabled', async () => {
    mockGetRegistrationMode.mockResolvedValueOnce({ publicRegistrationEnabled: false });
    render(<RegisterPage />);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /request access/i })).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Run tests, implement conditional rendering, verify tests pass**

Page calls `GET /registration-mode` on mount via `useQuery`. Renders `RegisterForm` or `RequestAccessForm` based on result. Shows loading skeleton during fetch.

On 403 from register API: re-fetches registration mode to distinguish real toggle vs transient failure.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/\\(auth\\)/register/page.tsx \
        apps/web/__tests__/app/register/page.test.tsx
git commit -m "feat(web): conditional register page (RegisterForm vs RequestAccessForm)"
```

---

### Task 19: Admin Access Requests Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/users/access-requests/page.tsx`
- Create: `apps/web/src/components/admin/access-requests/ApproveRejectDialog.tsx`
- Reference: `apps/web/src/app/admin/(dashboard)/users/invitations/page.tsx` (pattern)

- [ ] **Step 1: Create the admin page**

Follow invitations page pattern exactly:
- `useQuery` for list + stats
- KPI cards (Pending / Approved / Rejected / Total)
- Status filter tabs
- Paginated table with row actions
- Approve button (direct action with confirmation)
- Reject button (opens dialog with optional reason textarea, max 500 chars)
- "Approve Selected" toolbar button (disabled if > 25 selected)
- `useMutation` for approve/reject/bulk-approve with toast notifications
- Invalidate queries on mutation success

- [ ] **Step 2: Create ApproveRejectDialog**

Dialog with textarea for rejection reason (optional, max 500 chars). Cancel / Reject buttons.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/app/admin/\\(dashboard\\)/users/access-requests/ \
        apps/web/src/components/admin/access-requests/
git commit -m "feat(web): add admin access requests page with KPIs, filtering, bulk approve"
```

---

### Task 20: Registration Mode Toggle in Admin Settings

**Files:**
- Create: `apps/web/src/components/admin/settings/RegistrationModeToggle.tsx`
- Modify: Admin settings page (find existing settings page and add section)

- [ ] **Step 1: Create RegistrationModeToggle**

- `useQuery` for current mode
- Switch component (shadcn/ui)
- Confirmation dialog: "Are you sure? This will [enable/disable] public registration."
- `useMutation` for toggle with toast notification
- Status text: "Invite-only mode" / "Public registration enabled"

- [ ] **Step 2: Add to admin settings page**

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/settings/RegistrationModeToggle.tsx
git commit -m "feat(web): add registration mode toggle in admin settings"
```

---

### Task 21: Admin Sidebar Navigation

**Files:**
- Modify: Admin sidebar navigation component (find via `apps/web/src/components/admin/` or layout)

- [ ] **Step 1: Add "Access Requests" link under Users section**

- Link to `/admin/users/access-requests`
- Pending count badge (fetched via stats query)
- Icon consistent with existing sidebar items

- [ ] **Step 2: Commit**

```bash
git commit -m "feat(web): add access requests link with pending badge to admin sidebar"
```

---

## Chunk 5: Integration & E2E Tests

### Task 22: Backend Integration Tests

**Files:**
- Create: `tests/Api.Tests/BoundedContexts/Authentication/Integration/AccessRequestIntegrationTests.cs`

- [ ] **Step 1: Write integration tests using Testcontainers**

Key journeys to test:
1. Request access → approve → invitation created → accept invitation → user account exists
2. Bulk approve 3 requests → 3 invitations created
3. Config toggle persists and affects `POST /register` immediately
4. Concurrent approve: two threads approve same request → only one invitation
5. Rate limit: 4th request-access within 60s → 429

```csharp
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Authentication")]
public class AccessRequestIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task FullFlow_RequestApproveAccept_CreatesUser()
    {
        // 1. Request access
        var requestResponse = await Client.PostAsJsonAsync(
            "/api/v1/auth/request-access",
            new { email = "newuser@test.com" });
        Assert.Equal(HttpStatusCode.Accepted, requestResponse.StatusCode);

        // 2. Admin approves
        var requests = await AdminClient.GetFromJsonAsync<GetAccessRequestsResponse>(
            "/api/v1/admin/access-requests?status=Pending");
        var accessRequest = requests!.Items.Single(r => r.Email == "newuser@test.com");

        var approveResponse = await AdminClient.PostAsync(
            $"/api/v1/admin/access-requests/{accessRequest.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        // 3. Verify invitation was created (eventual via event handler)
        // ... poll or check directly via DB
    }

    [Fact]
    public async Task Register_WhenPublicDisabled_Returns403()
    {
        // Ensure Registration:PublicEnabled = false (default)
        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "test@test.com", password = "Test1234!", displayName = "Test" });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_WhenPublicEnabled_Succeeds()
    {
        // Admin enables public registration
        await AdminClient.PutAsJsonAsync(
            "/api/v1/admin/settings/registration-mode",
            new { enabled = true });

        var response = await Client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { email = "public@test.com", password = "Test1234!", displayName = "Test" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run integration tests**

```bash
cd apps/api && dotnet test --filter "Category=Integration&FullyQualifiedName~AccessRequest" -v minimal
```

- [ ] **Step 3: Commit**

```bash
git add tests/Api.Tests/BoundedContexts/Authentication/Integration/AccessRequestIntegrationTests.cs
git commit -m "test(auth): add access request integration tests"
```

---

### Task 23: Frontend E2E Tests

**Files:**
- Create: `apps/web/e2e/invite-only-registration.spec.ts`

- [ ] **Step 1: Write E2E tests**

```typescript
import { test, expect } from '@playwright/test';

test.describe('Invite-Only Registration', () => {
  test('shows request access form when registration is invite-only', async ({ page }) => {
    // Default: Registration:PublicEnabled = false
    await page.goto('/register');
    await expect(page.getByRole('button', { name: /request access/i })).toBeVisible();
    await expect(page.getByLabel(/email/i)).toBeVisible();
  });

  test('submits access request and shows success message', async ({ page }) => {
    await page.goto('/register');
    await page.getByLabel(/email/i).fill('visitor@example.com');
    await page.getByRole('button', { name: /request access/i }).click();
    await expect(page.getByText(/submitted/i)).toBeVisible();
  });

  test('admin can toggle registration mode', async ({ page }) => {
    // Login as admin
    await page.goto('/login');
    // ... admin login flow
    await page.goto('/admin/settings');
    // Toggle public registration
    const toggle = page.getByRole('switch', { name: /public registration/i });
    await toggle.click();
    // Confirm dialog
    await page.getByRole('button', { name: /confirm/i }).click();
    await expect(page.getByText(/public registration enabled/i)).toBeVisible();
  });

  test('admin can approve access request', async ({ page }) => {
    // Navigate to access requests page
    await page.goto('/admin/users/access-requests');
    // Find pending request and approve
    await page.getByRole('button', { name: /approve/i }).first().click();
    await expect(page.getByText(/approved/i)).toBeVisible();
  });
});
```

- [ ] **Step 2: Run E2E tests**

```bash
cd apps/web && pnpm test:e2e -- invite-only-registration
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/invite-only-registration.spec.ts
git commit -m "test(e2e): add invite-only registration E2E tests"
```

---

## Chunk 6: Final Validation

### Task 24: Build & Lint Verification

- [ ] **Step 1: Backend build**

```bash
cd apps/api/src/Api && dotnet build
```

- [ ] **Step 2: Run all backend tests**

```bash
cd apps/api && dotnet test --filter "FullyQualifiedName~AccessRequest" -v minimal
```

- [ ] **Step 3: Frontend build + typecheck + lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm build
```

- [ ] **Step 4: Run frontend tests**

```bash
cd apps/web && pnpm test
```

- [ ] **Step 5: Final commit if any fixes needed**

---

### Task 25: PR Creation

- [ ] **Step 1: Detect parent branch**

```bash
git config branch.$(git branch --show-current).parent
```

- [ ] **Step 2: Push and create PR to parent branch**

```bash
git push -u origin $(git branch --show-current)
gh pr create --base <parent-branch> --title "feat(auth): invite-only registration (beta0)" --body "..."
```

PR body should reference the spec: `docs/superpowers/specs/2026-03-14-invite-only-registration-design.md`

---

## Summary

| Chunk | Tasks | Focus |
|-------|-------|-------|
| 1 | 1-4 | Domain layer: entity, enum, events, repository interface |
| 2 | 5-12 | Application layer: commands, queries, DTOs, event handlers |
| 3 | 13-15 | Infrastructure: EF Core, migration, endpoint filter, routing |
| 4 | 16-21 | Frontend: API client, forms, pages, admin UI |
| 5 | 22-23 | Integration and E2E tests |
| 6 | 24-25 | Build verification and PR |

**Total**: 25 tasks, ~60 steps, TDD throughout.

---

## Addendum: Implementation Notes (from plan review)

### Missing Tests — Add During Implementation

The following tasks lack tests and MUST have them added during implementation:

**Task 10 (SetRegistrationMode + GetRegistrationMode):**
- Test `GetRegistrationModeQuery`: config returns true → DTO true, config returns false → DTO false, config unreachable → DTO false (fail-closed)
- Test `SetRegistrationModeCommand`: dispatches `UpdateConfigValueCommand` with correct key/value

**Task 11 (Queries):**
- Test `GetAccessRequestsQuery`: pagination defaults, max pageSize cap at 100, status filtering, empty results
- Test `GetAccessRequestStatsQuery`: counts by status, zero counts
- Test `GetAccessRequestByIdQuery`: found → returns DTO, not found → throws NotFoundException

**Task 12 (Event Handlers):**
- Test `AccessRequestApprovedEventHandler`: successful invitation creation → sets InvitationId, invitation failure → logs error, does not throw
- Test `AccessRequestCreatedEventHandler`: notification failure → logs warning, does not throw

### Codebase Pattern Notes

1. **Entity base class**: Use `internal sealed class AccessRequest : AggregateRoot<Guid>` (not `AggregateRoot`)
2. **Event base class**: Use `DomainEventBase` (not `DomainEvent`)
3. **Config writes**: `IConfigurationService` is read-only. Write via MediatR `UpdateConfigValueCommand` from SystemConfiguration BC
4. **`GetValueAsync` signature**: `GetValueAsync<T>(string key, T? defaultValue, string? environment = null)` — do NOT pass CancellationToken as 3rd arg
5. **Repository interface**: `IRepository<T, TId>` already provides `GetByIdAsync`, `AddAsync`, `UpdateAsync` — do not redeclare
6. **Notification service**: No `INotificationService` exists. Check `BoundedContexts/UserNotifications/` for actual interface (likely `IPushNotificationService`)
7. **Rate limit policies**: Verify that `"AuthRegister"` (3/min) and `"AuthGeneral"` (30/min) rate limit policies exist. If not, add them in rate limit configuration.
8. **`sortBy` query param**: Add to `GetAccessRequestsQuery` record — default `RequestedAt` desc per spec
