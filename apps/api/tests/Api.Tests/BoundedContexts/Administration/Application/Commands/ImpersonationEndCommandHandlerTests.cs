using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Unit tests for <see cref="ImpersonationEndCommandHandler"/> (SP5 Admin Security S2 — T4).
///
/// Q3 Security (#2655, finding #9): the self-end endpoint must NOT revoke an arbitrary session.
/// It may only end a session that (1) is an active impersonation and (2) was started by the
/// requesting admin (<c>ImpersonatedByUserId == RequestingUserId</c>). Any other target is
/// rejected — a non-impersonation login session or another admin's impersonation cannot be
/// terminated through this endpoint (the superadmin kill-switch <c>/revoke</c> handles those).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ImpersonationEndCommandHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly FakeTimeProvider _timeProvider;
    private readonly ImpersonationEndCommandHandler _handler;

    public ImpersonationEndCommandHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _timeProvider = new FakeTimeProvider(DateTimeOffset.Parse("2026-07-07T12:00:00Z"));

        _handler = new ImpersonationEndCommandHandler(
            _mockMediator.Object,
            _mockSessionRepository.Object,
            Mock.Of<ILogger<ImpersonationEndCommandHandler>>());
    }

    [Fact]
    public async Task Handle_HappyPath_SelfEnd_RevokesViaRevokeSessionCommand()
    {
        // Arrange — admin alice ends her OWN impersonation of bob.
        var aliceId = Guid.NewGuid();   // acting admin == session.ImpersonatedByUserId
        var bobId = Guid.NewGuid();     // subject == session.UserId
        var sessionId = Guid.NewGuid();

        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevokeSessionResponse(true, null));

        // Act
        var result = await _handler.Handle(
            new ImpersonationEndCommand(sessionId, aliceId), CancellationToken.None);

        // Assert
        result.Should().BeTrue();
        _mockMediator.Verify(m => m.Send(
            It.Is<RevokeSessionCommand>(c => c.SessionId == sessionId && c.IsRequestingUserAdmin),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_SessionNotFound_ThrowsNotFound()
    {
        var aliceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Session?)null);

        var act = () => _handler.Handle(
            new ImpersonationEndCommand(sessionId, aliceId), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        _mockMediator.Verify(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SessionNotAnImpersonation_ThrowsForbidden()
    {
        // A regular login session (ImpersonatedByUserId == null) must not be terminable here,
        // even by an admin — otherwise any admin can force-logout any user by session id.
        var aliceId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var regular = new Session(
            id: sessionId, userId: Guid.NewGuid(), token: SessionToken.Generate(), timeProvider: _timeProvider);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(regular);

        var act = () => _handler.Handle(
            new ImpersonationEndCommand(sessionId, aliceId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _mockMediator.Verify(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ImpersonationStartedByAnotherAdmin_ThrowsForbidden()
    {
        // CORE of finding #9: admin carol must not be able to end alice's impersonation session.
        var aliceId = Guid.NewGuid();   // admin who started the impersonation
        var carolId = Guid.NewGuid();   // a DIFFERENT admin issuing the request
        var bobId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var act = () => _handler.Handle(
            new ImpersonationEndCommand(sessionId, carolId), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        _mockMediator.Verify(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SessionAlreadyRevoked_ReturnsTrueIdempotent()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        session.Revoke(_timeProvider, "already revoked");
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var result = await _handler.Handle(
            new ImpersonationEndCommand(sessionId, aliceId), CancellationToken.None);

        result.Should().BeTrue("ending an already-revoked own impersonation is idempotent");
        _mockMediator.Verify(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_RevokeSessionFails_ReturnsFalse()
    {
        var aliceId = Guid.NewGuid();
        var bobId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        var session = CreateImpersonationSession(sessionId, subjectId: bobId, actorId: aliceId);
        _mockSessionRepository
            .Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);
        _mockMediator
            .Setup(m => m.Send(It.IsAny<RevokeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RevokeSessionResponse(false, "boom"));

        var result = await _handler.Handle(
            new ImpersonationEndCommand(sessionId, aliceId), CancellationToken.None);

        result.Should().BeFalse();
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
