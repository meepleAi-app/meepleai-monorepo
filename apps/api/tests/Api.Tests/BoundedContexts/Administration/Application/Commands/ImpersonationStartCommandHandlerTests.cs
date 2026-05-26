using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="ImpersonationStartCommandHandler"/> (SP5 Admin Security S2 — T3).
///
/// Verifies the tightened eligibility (D-S2-1, superadmin-only + self/demo/banned guards) and
/// that the handler delegates session creation to <see cref="CreateSessionCommand"/> with the
/// dual-principal fields populated. Audit is NOT written here anymore — the AuditLoggingBehavior
/// pipeline owns that (verified by the integration/acceptance tests, not this unit test).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ImpersonationStartCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ImpersonationStartCommandHandler _handler;

    public ImpersonationStartCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockMediator = new Mock<IMediator>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));

        _handler = new ImpersonationStartCommandHandler(
            _mockUserRepository.Object,
            _mockMediator.Object,
            _timeProvider,
            Mock.Of<ILogger<ImpersonationStartCommandHandler>>());
    }

    [Fact]
    public async Task Handle_HappyPath_CreatesImpersonationSessionWithDualPrincipalFields()
    {
        // Arrange — superadmin requester + regular target.
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        SetupUser(targetId, "bob@test.com", "user");

        CreateSessionCommand? captured = null;
        var sessionId = Guid.NewGuid();
        var expectedExpiry = _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<CreateSessionResponse>, CancellationToken>((cmd, _) => captured = (CreateSessionCommand)cmd)
            .ReturnsAsync(new CreateSessionResponse(
                User: null!, SessionToken: "tok", ExpiresAt: expectedExpiry, SessionId: sessionId));

        var command = new ImpersonationStartCommand(targetId, requesterId, "Investigating reported library bug", DurationMinutes: 15);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert — response carries the impersonation session metadata.
        result.SessionId.Should().Be(sessionId);
        result.ImpersonatedUserId.Should().Be(targetId);
        result.ImpersonatedUntil.Should().Be(expectedExpiry);

        // Assert — CreateSessionCommand carries subject (UserId) + actor (ImpersonatedByUserId) + expiry.
        captured.Should().NotBeNull();
        captured!.UserId.Should().Be(targetId, "the session subject is the impersonated user");
        captured.ImpersonatedByUserId.Should().Be(requesterId, "the actor is the superadmin");
        captured.ImpersonatedUntil.Should().Be(expectedExpiry);
        captured.IpAddress.Should().BeNull("the legacy 'impersonated' magic-string signal is gone");
    }

    [Fact]
    public async Task Handle_CallerIsAdminNotSuperAdmin_ThrowsForbidden()
    {
        // D-S2-1 tightening: legacy accepted admin; S2 requires superadmin.
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "admin@test.com", "admin");
        SetupUser(targetId, "bob@test.com", "user");

        var command = new ImpersonationStartCommand(targetId, requesterId, "admin trying to impersonate");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Only superadmins*");
    }

    [Fact]
    public async Task Handle_CallerIsRegularUser_ThrowsForbidden()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "user@test.com", "user");
        SetupUser(targetId, "bob@test.com", "user");

        var command = new ImpersonationStartCommand(targetId, requesterId, "user trying to impersonate");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Only superadmins*");
    }

    [Fact]
    public async Task Handle_TargetIsAdmin_ThrowsForbidden()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        SetupUser(targetId, "admin2@test.com", "admin");

        var command = new ImpersonationStartCommand(targetId, requesterId, "impersonate a peer admin");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Cannot impersonate admin or superadmin*");
    }

    [Fact]
    public async Task Handle_TargetIsSuperAdmin_ThrowsForbidden()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        SetupUser(targetId, "root2@test.com", "superadmin");

        var command = new ImpersonationStartCommand(targetId, requesterId, "impersonate another superadmin");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Cannot impersonate admin or superadmin*");
    }

    [Fact]
    public async Task Handle_TargetIsSuspended_ThrowsConflict()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        var suspended = SetupUser(targetId, "suspended@test.com", "user");
        suspended.Suspend("Suspended for testing");

        var command = new ImpersonationStartCommand(targetId, requesterId, "impersonate suspended user");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage($"*{targetId}*");
    }

    [Fact]
    public async Task Handle_TargetIsDemoAccount_ThrowsConflict()
    {
        // D-S2-1 NEW guard: demo accounts are ineligible targets.
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        var demo = SetupUser(targetId, "demo@test.com", "user");
        demo.MarkAsDemoAccount();

        var command = new ImpersonationStartCommand(targetId, requesterId, "impersonate demo account");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*demo account*");
    }

    [Fact]
    public async Task Handle_RequesterNotFound_ThrowsNotFound()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(requesterId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ImpersonationStartCommand(targetId, requesterId, "requester does not exist");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{requesterId}*");
    }

    [Fact]
    public async Task Handle_TargetNotFound_ThrowsNotFound()
    {
        var requesterId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        SetupUser(requesterId, "root@test.com", "superadmin");
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(targetId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var command = new ImpersonationStartCommand(targetId, requesterId, "target does not exist");

        var act = () => _handler.Handle(command, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>().WithMessage($"*{targetId}*");
    }

    private User SetupUser(Guid id, string email, string role)
    {
        var user = CreateTestUser(id, email, role);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        return user;
    }

    private static User CreateTestUser(Guid id, string email, string role = "user")
    {
        var userRole = role.ToLowerInvariant() switch
        {
            "admin" => Role.Admin,
            "superadmin" => Role.SuperAdmin,
            _ => Role.User
        };

        return new User(
            id: id,
            email: new Email(email),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("UnusualPwd123!"),
            role: userRole
        );
    }
}
