using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// Unit tests for <see cref="RespondToGameNightInvitationByTokenCommandHandler"/>.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// Verifies the idempotency contract D2(b) and the HTTP status mapping
/// (404/410/409) configured by the handler before the aggregate is invoked.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public sealed class RespondToGameNightInvitationByTokenCommandHandlerTests
{
    private const string Token = "abcdefghijklmnopqrstuv";
    private static readonly DateTimeOffset UtcNow = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IGameNightInvitationRepository> _invitationRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly FakeTimeProvider _timeProvider = new(UtcNow);
    private readonly RespondToGameNightInvitationByTokenCommandHandler _sut;

    public RespondToGameNightInvitationByTokenCommandHandlerTests()
    {
        _sut = new RespondToGameNightInvitationByTokenCommandHandler(
            _invitationRepoMock.Object,
            _unitOfWorkMock.Object,
            _timeProvider);
    }

    private static GameNightInvitation Reconstitute(
        GameNightInvitationStatus status,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? respondedAt = null)
    {
        return GameNightInvitation.Reconstitute(
            id: Guid.NewGuid(),
            token: Token,
            gameNightId: Guid.NewGuid(),
            email: "guest@example.com",
            status: status,
            expiresAt: expiresAt ?? UtcNow.AddDays(7),
            respondedAt: respondedAt,
            respondedByUserId: null,
            createdAt: UtcNow.AddDays(-1),
            createdBy: Guid.NewGuid());
    }

    [Fact]
    public async Task Handle_TokenNotFound_ThrowsNotFoundException()
    {
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightInvitation?)null);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CancelledInvitation_ThrowsGoneException()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Cancelled);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Handle_ExpiredStatusInvitation_ThrowsGoneException()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Expired);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Handle_PendingButCutoffPassed_ThrowsGoneException()
    {
        // Pending past expiry → 410 Gone (implicit expiry on read, spec §2.2).
        var invitation = Reconstitute(
            GameNightInvitationStatus.Pending,
            expiresAt: UtcNow.AddSeconds(-1));
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<GoneException>();
    }

    [Fact]
    public async Task Handle_SwitchAcceptedToDeclined_ThrowsConflictException()
    {
        // Already-accepted invitation cannot be flipped to Declined → 409 Conflict.
        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Declined, ResponderUserId: null);

        var act = async () => await _sut.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameStateAccepted_IsIdempotentNoOp()
    {
        // Re-issuing Accept on Accepted invitation: 200 OK without persistence.
        var invitation = Reconstitute(GameNightInvitationStatus.Accepted);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, ResponderUserId: null);

        await _sut.Handle(command, CancellationToken.None);

        _invitationRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_PendingToAccepted_PersistsTransition()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var responder = Guid.NewGuid();
        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Accepted, responder);

        await _sut.Handle(command, CancellationToken.None);

        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        invitation.RespondedByUserId.Should().Be(responder);
        _invitationRepoMock.Verify(
            r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PendingToDeclined_PersistsTransition()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);

        var command = new RespondToGameNightInvitationByTokenCommand(
            Token, GameNightInvitationStatus.Declined, ResponderUserId: null);

        await _sut.Handle(command, CancellationToken.None);

        invitation.Status.Should().Be(GameNightInvitationStatus.Declined);
        _invitationRepoMock.Verify(
            r => r.UpdateAsync(invitation, It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
