using Api.Tests.TestHelpers;
using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CreateGamebookCampaignHandlerTests
{
    private sealed class FakeRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default)
        {
            Store.Add(s);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private readonly FakeRepo _repo = new();
    private readonly Mock<IMediator> _mediator = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public CreateGamebookCampaignHandlerTests()
    {
        // #3636: l'handler consegna il lavoro alla UoW invece di pilotare Begin/Commit.
        // Senza questo setup il mock non esegue il delegate e nulla accade.
        _uow.SetupExecuteInTransaction();
        // #2917: the handler dispatches CreateSessionCommand for the roster. Default to a
        // standalone (night-less) result so the happy-path tests don't need per-test setup.
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreateSessionResult(
                Guid.NewGuid(), "ABC123", new List<ParticipantDto>(), Guid.Empty, false, null, null));

        // Owner display-name resolution (null → the handler falls back to "Owner").
        _mediator
            .Setup(m => m.Send(It.IsAny<GetUserByIdQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDto?)null);
    }

    private CreateGamebookCampaignHandler CreateHandler()
        => new(_repo, _mediator.Object, _uow.Object);

    [Fact]
    public async Task Handle_WithValidCommand_PersistsAndReturnsDto()
    {
        var handler = CreateHandler();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cmd = new CreateGamebookCampaignCommand(gameId, userId, "My Campaign");

        var dto = await handler.Handle(cmd, CancellationToken.None);

        dto.Title.Should().Be("My Campaign");
        dto.OwnerUserId.Should().Be(userId);
        dto.CurrentParagraph.Should().Be(0);
        dto.Id.Should().NotBeEmpty();
        dto.GameRefId.Should().Be(gameId);
        dto.GameRefKind.Should().Be((int)GameRefKind.Shared);
        _repo.Store.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithRoster_DispatchesCreateSessionLinkedToCampaign()
    {
        var handler = CreateHandler();
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cmd = new CreateGamebookCampaignCommand(
            gameId, userId, "Campaign",
            Participants: new List<ParticipantDto> { new() { DisplayName = "Alice", IsOwner = false } },
            GuestNames: new List<string> { "Luca" });

        await handler.Handle(cmd, CancellationToken.None);

        var campaignId = _repo.Store[0].Id;
        _mediator.Verify(m => m.Send(
            It.Is<CreateSessionCommand>(c =>
                c.GamebookCampaignId == campaignId
                && c.UserId == userId
                && c.GameId == gameId
                && c.SkipGameNightEnvelope
                && c.SkipKbReadinessGate
                && c.GuestNames != null && c.GuestNames.Contains("Luca")
                // Regression guard for the review finding: the dispatched command MUST carry the
                // owner (CreateSessionCommandValidator requires an IsOwner participant).
                && c.Participants.Any(p => p.IsOwner && p.UserId == userId)
                && c.Participants.Any(p => p.DisplayName == "Alice")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_DoesNotOpenLiveMode()
    {
        // Decision #2759 point 2: the standalone Session must NOT open live mode, so invariant
        // #10 max-1-live stays scoped to GameNight play. (Contrast: the attach handler DOES.)
        var handler = CreateHandler();
        var cmd = new CreateGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), "Campaign");

        await handler.Handle(cmd, CancellationToken.None);

        _mediator.Verify(
            m => m.Send(It.IsAny<OpenSessionLiveModeCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_HappyPath_CommitsTransaction()
    {
        var handler = CreateHandler();
        var cmd = new CreateGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), "Campaign");

        await handler.Handle(cmd, CancellationToken.None);

        // #3636: Begin/Commit sono interni alla UoW e coperti dai suoi test. Qui conta che il
        // lavoro sia passato da UNA transazione e che l'handler non ne apra di proprie.
        _uow.Verify(
            u => u.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_SessionFailure_RollsBackTransaction()
    {
        var handler = CreateHandler();
        _mediator
            .Setup(m => m.Send(It.IsAny<CreateSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("session failed"));
        var cmd = new CreateGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), "Campaign");

        var act = () => handler.Handle(cmd, CancellationToken.None);

        // #3636: il rollback è responsabilità della UoW (e testato lì). Il contratto dell'handler
        // è che l'eccezione della sessione propaghi invariata invece di essere inghiottita — se il
        // commit avvenisse comunque, la campagna resterebbe orfana di sessione.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void Constructor_WhenRepoIsNull_ThrowsArgumentNullException()
    {
        var act = () => new CreateGamebookCampaignHandler(null!, _mediator.Object, _uow.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("repo");
    }

    [Fact]
    public void Constructor_WhenMediatorIsNull_ThrowsArgumentNullException()
    {
        var act = () => new CreateGamebookCampaignHandler(_repo, null!, _uow.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("mediator");
    }

    [Fact]
    public void Constructor_WhenUnitOfWorkIsNull_ThrowsArgumentNullException()
    {
        var act = () => new CreateGamebookCampaignHandler(_repo, _mediator.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("unitOfWork");
    }
}
