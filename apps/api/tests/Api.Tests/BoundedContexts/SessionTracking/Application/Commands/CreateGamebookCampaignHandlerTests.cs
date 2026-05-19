using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
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

    [Fact]
    public async Task Handle_WithValidCommand_PersistsAndReturnsDto()
    {
        // Arrange
        var repo = new FakeRepo();
        var mediator = new Mock<IMediator>();
        var handler = new CreateGamebookCampaignHandler(repo, mediator.Object);
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var cmd = new CreateGamebookCampaignCommand(gameId, userId, "My Campaign");

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.Title.Should().Be("My Campaign");
        dto.GameId.Should().Be(gameId);
        dto.OwnerUserId.Should().Be(userId);
        dto.CurrentParagraph.Should().Be(0);
        dto.Id.Should().NotBeEmpty();
        repo.Store.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_WhenRepoIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CreateGamebookCampaignHandler(null!, new Mock<IMediator>().Object);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("repo");
    }
}
