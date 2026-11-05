using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
    private readonly Mock<IGameNightEventRepository> _gameNightRepoMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly Mock<IDomainEventCollector> _eventsMock = new();
    private readonly FakeTimeProvider _timeProvider = new(UtcNow);
    private readonly RespondToGameNightInvitationByTokenCommandHandler _sut;

    public RespondToGameNightInvitationByTokenCommandHandlerTests()
    {
        _sut = new RespondToGameNightInvitationByTokenCommandHandler(
            _invitationRepoMock.Object,
            _gameNightRepoMock.Object,
            _unitOfWorkMock.Object,
            _eventsMock.Object,
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

    // ── A cancelled night cannot be answered (#4856) ─────────────────────────────────────────────────────────

    private GameNightEvent GivenTheNight(GameNightInvitation invitation, bool cancelled)
    {
        var night = GameNightEvent.Create(
            organizerId: Guid.NewGuid(), title: "Friday Catan", scheduledAt: UtcNow.AddDays(7));
        night.Publish([]);
        if (cancelled)
        {
            night.Cancel();
        }

        _invitationRepoMock
            .Setup(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()))
            .ReturnsAsync(invitation);
        _gameNightRepoMock
            .Setup(r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        return night;
    }

    [Theory]
    [InlineData(GameNightInvitationStatus.Pending, GameNightInvitationStatus.Accepted)]
    [InlineData(GameNightInvitationStatus.Pending, GameNightInvitationStatus.Declined)]
    [InlineData(GameNightInvitationStatus.Accepted, GameNightInvitationStatus.Accepted)]
    [InlineData(GameNightInvitationStatus.Accepted, GameNightInvitationStatus.Declined)]
    [InlineData(GameNightInvitationStatus.Declined, GameNightInvitationStatus.Declined)]
    public async Task Handle_TheNightIsCancelled_NoAnswerGoesThrough_WhateverTheInvitationAndTheAnswer(
        GameNightInvitationStatus stored, GameNightInvitationStatus answer)
    {
        var invitation = Reconstitute(stored, respondedAt: stored == GameNightInvitationStatus.Pending ? null : UtcNow);
        GivenTheNight(invitation, cancelled: true);

        var act = async () => await _sut.Handle(
            new RespondToGameNightInvitationByTokenCommand(Token, answer, ResponderUserId: null),
            CancellationToken.None);

        var gone = await act.Should().ThrowAsync<GoneException>();
        gone.Which.Message.Should().Contain("cancelled", "the guest is told why, not only that it is gone");
        invitation.Status.Should().Be(stored, "nothing was changed");
        _invitationRepoMock.Verify(
            r => r.UpdateAsync(It.IsAny<GameNightInvitation>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TheNightIsOn_TheAnswerIsStillAccepted()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        GivenTheNight(invitation, cancelled: false);

        await _sut.Handle(
            new RespondToGameNightInvitationByTokenCommand(Token, GameNightInvitationStatus.Accepted, ResponderUserId: null),
            CancellationToken.None);

        invitation.Status.Should().Be(GameNightInvitationStatus.Accepted);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheNightIsLookedUpByTheNightOfTheInvitation()
    {
        var invitation = Reconstitute(GameNightInvitationStatus.Pending);
        GivenTheNight(invitation, cancelled: false);

        await _sut.Handle(
            new RespondToGameNightInvitationByTokenCommand(Token, GameNightInvitationStatus.Declined, ResponderUserId: null),
            CancellationToken.None);

        _gameNightRepoMock.Verify(
            r => r.GetByIdAsync(invitation.GameNightId, It.IsAny<CancellationToken>()), Times.Once);
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

    // ── Another writer got there first (#4744) ───────────────────────────────────────────────────────────────
    // The invitation carries its row version: a save that overlaps another write is refused. The handler decides
    // again from a fresh read, so an answer is never lost and a withdrawal is never undone.

    private static GameNightInvitation Fresh(GameNightInvitationStatus status) => Reconstitute(status);

    private void GivenTheReads(params GameNightInvitation[] reads)
    {
        var sequence = _invitationRepoMock.SetupSequence(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()));
        foreach (var read in reads)
        {
            sequence = sequence.ReturnsAsync(read);
        }
    }

    private void GivenTheSaveIsRefused(int times)
    {
        var sequence = _unitOfWorkMock.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()));
        for (var i = 0; i < times; i++)
        {
            sequence = sequence.ThrowsAsync(new DbUpdateConcurrencyException("another writer got there first"));
        }

        sequence.ReturnsAsync(1);
    }

    private Task AnswerAccepted() => _sut.Handle(
        new RespondToGameNightInvitationByTokenCommand(Token, GameNightInvitationStatus.Accepted, ResponderUserId: null),
        CancellationToken.None);

    [Fact]
    public async Task Handle_TheSaveIsRefusedOnce_TheAnswerIsDecidedAgainFromAFreshRead_AndGoesThrough()
    {
        var first = Fresh(GameNightInvitationStatus.Pending);
        var second = Fresh(GameNightInvitationStatus.Pending);
        GivenTheReads(first, second);
        GivenTheSaveIsRefused(1);

        await AnswerAccepted();

        second.Status.Should().Be(GameNightInvitationStatus.Accepted);
        _invitationRepoMock.Verify(r => r.GetByTokenAsync(Token, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
        _eventsMock.Verify(e => e.Clear(), Times.Once, "the refused attempt's events must not be published with the retry's");
    }

    [Fact]
    public async Task Handle_TheSaveIsRefused_AndTheInvitationWasWithdrawnMeanwhile_TheAnswerIsGone()
    {
        // The organizer withdrew while the guest's answer was being saved. Before the row version the answer simply
        // won (the whole row was rewritten) and put a withdrawn invitation back in play.
        GivenTheReads(Fresh(GameNightInvitationStatus.Pending), Fresh(GameNightInvitationStatus.Cancelled));
        GivenTheSaveIsRefused(1);

        var act = () => AnswerAccepted();

        await act.Should().ThrowAsync<GoneException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheSaveIsRefused_AndTheSameAnswerWonTheRace_ItIsTheIdempotentNoOp()
    {
        GivenTheReads(Fresh(GameNightInvitationStatus.Pending), Fresh(GameNightInvitationStatus.Accepted));
        GivenTheSaveIsRefused(1);

        await AnswerAccepted();

        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TheSaveIsRefused_AndTheOppositeAnswerWonTheRace_ItIsAConflict()
    {
        GivenTheReads(Fresh(GameNightInvitationStatus.Pending), Fresh(GameNightInvitationStatus.Declined));
        GivenTheSaveIsRefused(1);

        var act = () => AnswerAccepted();

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_TheSaveIsRefusedEveryTime_GivesUpWithAConflict_AfterAsManyAttemptsAsTheHelperAllows()
    {
        GivenTheReads(
            Fresh(GameNightInvitationStatus.Pending),
            Fresh(GameNightInvitationStatus.Pending),
            Fresh(GameNightInvitationStatus.Pending));
        GivenTheSaveIsRefused(10);

        var act = () => AnswerAccepted();

        await act.Should().ThrowAsync<ConflictException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _eventsMock.Verify(e => e.Clear(), Times.Exactly(3));
    }

    [Fact]
    public async Task Handle_AFailureThatIsNotAConcurrencyRefusal_IsNotRetried()
    {
        GivenTheReads(Fresh(GameNightInvitationStatus.Pending));
        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => AnswerAccepted();

        await act.Should().ThrowAsync<InvalidOperationException>();
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _eventsMock.Verify(e => e.Clear(), Times.Never);
    }
}
