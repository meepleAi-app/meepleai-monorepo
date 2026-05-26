using Api.BoundedContexts.Administration.Application;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="RevokeImpersonationCommandHandler"/> (SP5 Admin Security S2 — T5).
/// Verifies superadmin-only enforcement, impersonation-session validation, and the bespoke audit
/// attribution (user_id = impersonating admin, impersonated_user_id = superadmin caller — Scenario S2-4).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RevokeImpersonationCommandHandlerTests
{
    private readonly Mock<IUserRepository> _mockUserRepository;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<AuditService> _mockAuditService;
    private readonly FakeTimeProvider _timeProvider;
    private readonly RevokeImpersonationCommandHandler _handler;

    public RevokeImpersonationCommandHandlerTests()
    {
        _mockUserRepository = new Mock<IUserRepository>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockMediator = new Mock<IMediator>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-05-26T12:00:00Z"));

        // AuditService has virtual methods — mock with a satisfied base ctor (InMemory ctx, unused).
        _mockAuditService = new Mock<AuditService>(
            new MeepleAiDbContext(
                new DbContextOptionsBuilder<MeepleAiDbContext>()
                    .UseInMemoryDatabase($"revoke_audit_{Guid.NewGuid()}").Options,
                Mock.Of<IMediator>(),
                Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>()),
            Mock.Of<ILogger<AuditService>>(),
            null!);

        _handler = new RevokeImpersonationCommandHandler(
            _mockUserRepository.Object,
            _mockSessionRepository.Object,
            _mockMediator.Object,
            _mockAuditService.Object,
            _timeProvider,
            Mock.Of<ILogger<RevokeImpersonationCommandHandler>>());
    }

    [Fact]
    public async Task Handle_HappyPath_RevokesAndWritesAuditWithCorrectAttribution()
    {
        // Arrange — root (superadmin) revokes alice's impersonation of bob.
        var rootId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();   // impersonating admin (session.ImpersonatedByUserId)
        var bobId = Guid.NewGuid();     // subject (session.UserId)
        var sessionId = Guid.NewGuid();

        SetupUser(rootId, "superadmin");
        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevokeSessionResponse(true, null));

        AuditOutboxPayload? capturedAudit = null;
        _mockAuditService
            .Setup(s => s.EnqueueAuditAsync(It.IsAny<AuditOutboxPayload>(), It.IsAny<CancellationToken>()))
            .Callback<AuditOutboxPayload, CancellationToken>((p, _) => capturedAudit = p)
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(new RevokeImpersonationCommand(sessionId, rootId), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockMediator.Verify(m => m.Send(
            It.Is<RevokeSessionCommand>(c => c.SessionId == sessionId && c.IsRequestingUserAdmin),
            It.IsAny<CancellationToken>()), Times.Once);

        capturedAudit.Should().NotBeNull();
        capturedAudit!.Action.Should().Be("ImpersonationRevoked");
        capturedAudit.Resource.Should().Be("Session");
        capturedAudit.UserId.Should().Be(aliceId.ToString(),
            "user_id = the impersonating admin whose session is killed (Scenario S2-4)");
        capturedAudit.ImpersonatedUserId.Should().Be(rootId,
            "impersonated_user_id = the superadmin invoking the kill-switch (Scenario S2-4)");
        capturedAudit.ResourceId.Should().Be(sessionId.ToString());
    }

    [Fact]
    public async Task Handle_RequesterNotSuperAdmin_ThrowsForbidden()
    {
        var adminId = Guid.NewGuid();
        SetupUser(adminId, "admin");

        var act = () => _handler.Handle(new RevokeImpersonationCommand(Guid.NewGuid(), adminId), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>().WithMessage("*Only superadmins*");
    }

    [Fact]
    public async Task Handle_SessionNotFound_ReturnsFalse()
    {
        var rootId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupUser(rootId, "superadmin");
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var result = await _handler.Handle(new RevokeImpersonationCommand(sessionId, rootId), CancellationToken.None);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_SessionNotAnImpersonation_ThrowsConflict()
    {
        var rootId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupUser(rootId, "superadmin");

        // A regular (non-impersonation) session — ImpersonatedByUserId is null.
        var regular = new Session(
            id: sessionId, userId: Guid.NewGuid(), token: SessionToken.Generate(), timeProvider: _timeProvider);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regular);

        var act = () => _handler.Handle(new RevokeImpersonationCommand(sessionId, rootId), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>().WithMessage("*not an impersonation*");
    }

    [Fact]
    public async Task Handle_SessionAlreadyRevoked_ReturnsTrueIdempotent()
    {
        var rootId = Guid.NewGuid();
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SetupUser(rootId, "superadmin");

        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        session.Revoke(_timeProvider, "already revoked");
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(new RevokeImpersonationCommand(sessionId, rootId), CancellationToken.None);

        result.Should().BeTrue("revoking an already-revoked session is idempotent");
        _mockMediator.Verify(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private void SetupUser(Guid id, string role)
    {
        var userRole = role.ToLowerInvariant() switch
        {
            "admin" => Role.Admin,
            "superadmin" => Role.SuperAdmin,
            _ => Role.User
        };
        var user = new User(
            id: id,
            email: new Email($"{role}@test.com"),
            displayName: role,
            passwordHash: PasswordHash.Create("UnusualPwd123!"),
            role: userRole);
        _mockUserRepository
            .Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
    }

    private Session CreateImpersonationSession(Guid sessionId, Guid subjectId, Guid actorId)
        => new(
            id: sessionId,
            userId: subjectId,
            token: SessionToken.Generate(),
            lifetime: TimeSpan.FromMinutes(15),
            impersonatedByUserId: actorId,
            impersonatedUntil: _timeProvider.GetUtcNow().UtcDateTime.AddMinutes(15),
            timeProvider: _timeProvider);
}
