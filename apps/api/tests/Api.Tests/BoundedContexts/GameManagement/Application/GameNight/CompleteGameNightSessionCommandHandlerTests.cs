using Api.Tests.TestHelpers;
using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #2634 C4: completing a game night session sets the GameNightSession winner AND atomically
/// finalizes the correlated tracking Session (no orphan / canonical winner), with the WinnerId
/// write-validated as a participant and concurrency mapped to 409.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
public class CompleteGameNightSessionCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _gameNightRepo = new();
    private readonly Mock<ISessionRepository> _sessionRepo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAutoSaveSchedulerService> _autoSave = new();
    private readonly CompleteGameNightSessionCommandHandler _handler;

    public CompleteGameNightSessionCommandHandlerTests()
    {
        // #3636: l'handler consegna il lavoro alla UoW invece di pilotare Begin/Commit.
        // Senza questo setup il mock non esegue il delegate e nulla accade.
        _uow.SetupExecuteInTransaction();
        _handler = new CompleteGameNightSessionCommandHandler(
            _gameNightRepo.Object, _sessionRepo.Object, _mediator.Object, _uow.Object, _autoSave.Object);
        _mediator.Setup(m => m.Send(It.IsAny<FinalizeSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FinalizeSessionResult(null, new Dictionary<Guid, decimal>()));
    }

    /// <summary>Builds a started night whose current session is a tracking Session with 2 participants.</summary>
    private (GameNightEvent night, Session session) StartedNightWithTwoPlayers(Guid organizerId)
    {
        var gameId = Guid.NewGuid();
        var session = Session.Create(organizerId, gameId, SessionType.GameSpecific); // owner participant
        session.AddParticipant(ParticipantInfo.Create("Player Two", isOwner: false, joinOrder: 2), Guid.NewGuid());

        var night = GameNightEvent.Create(
            organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [gameId]);
        night.Publish([]);
        night.AddSession(session.Id, gameId, "Catan");
        night.StartCurrentSession(); // InProgress

        _gameNightRepo.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);
        _sessionRepo.Setup(r => r.GetByIdAsync(session.Id, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        return (night, session);
    }

    [Fact]
    public async Task Handle_NonOrganizer_ThrowsForbidden()
    {
        var (night, _) = StartedNightWithTwoPlayers(Guid.NewGuid());
        var act = () => _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, null, Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_NightNotFound_ThrowsNotFound()
    {
        _gameNightRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameNightEvent?)null);
        var act = () => _handler.Handle(
            new CompleteGameNightSessionCommand(Guid.NewGuid(), null, Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NoInProgressSession_ThrowsConflict()
    {
        var organizerId = Guid.NewGuid();
        var night = GameNightEvent.Create(organizerId, "Serata", DateTimeOffset.UtcNow.AddHours(1), gameIds: [Guid.NewGuid()]);
        night.Publish([]); // no session started → no CurrentSession
        _gameNightRepo.Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>())).ReturnsAsync(night);

        var act = () => _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, null, organizerId), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_WinnerNotAParticipant_ThrowsConflict_AndDoesNotFinalize()
    {
        var organizerId = Guid.NewGuid();
        var (night, _) = StartedNightWithTwoPlayers(organizerId);

        var act = () => _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, Guid.NewGuid() /* stranger */, organizerId),
            CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        _mediator.Verify(m => m.Send(It.IsAny<FinalizeSessionCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WithWinner_CompletesAndFinalizesWithWinnerRankOne()
    {
        var organizerId = Guid.NewGuid();
        var (night, session) = StartedNightWithTwoPlayers(organizerId);
        var winner = session.Participants.ElementAt(1); // Player Two
        var loser = session.Participants.ElementAt(0);

        await _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, winner.Id, organizerId), CancellationToken.None);

        night.Sessions[0].WinnerId.Should().Be(winner.Id);
        // #3636: Begin/Commit sono interni alla UoW. Si verifica che il lavoro sia passato da UNA
        // transazione e che l'handler non ne apra di proprie.
        _uow.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
        _mediator.Verify(m => m.Send(
            It.Is<FinalizeSessionCommand>(c =>
                c.SessionId == session.Id &&
                c.FinalRanks[winner.Id] == 1 &&
                c.FinalRanks[loser.Id] == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoWinner_FinalizesWithEveryoneRankTwo()
    {
        var organizerId = Guid.NewGuid();
        var (night, session) = StartedNightWithTwoPlayers(organizerId);

        await _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, null, organizerId), CancellationToken.None);

        _mediator.Verify(m => m.Send(
            It.Is<FinalizeSessionCommand>(c =>
                c.SessionId == session.Id &&
                c.FinalRanks.Count == 2 &&
                c.FinalRanks.Values.All(r => r == 2)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ConcurrencyOnSave_RollsBackAndThrows409()
    {
        var organizerId = Guid.NewGuid();
        var (night, _) = StartedNightWithTwoPlayers(organizerId);
        // #3636: il fallimento emerge dal SaveChanges interno alla UoW.
        _uow.Setup(u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException());

        var act = () => _handler.Handle(
            new CompleteGameNightSessionCommand(night.Id, null, organizerId), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        // Il rollback è responsabilità della UoW: qui conta la mappatura dell'eccezione.
    }
}
