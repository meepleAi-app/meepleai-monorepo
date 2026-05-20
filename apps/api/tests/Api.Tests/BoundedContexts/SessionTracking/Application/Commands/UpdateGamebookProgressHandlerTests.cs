using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class UpdateGamebookProgressHandlerTests
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

    private static (FakeRepo repo, UpdateGamebookProgressHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var repo = new FakeRepo();
        var handler = new UpdateGamebookProgressHandler(repo);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Test Campaign");
        repo.Store.Add(session);
        return (repo, handler, session);
    }

    [Fact]
    public async Task Handle_AdvancesParagraph_AndSaves()
    {
        // Arrange
        var (_, handler, session) = BuildSut();
        var cmd = new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, 47);

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.CurrentParagraph.Should().Be(47);
        session.Progress.CurrentParagraph.Should().Be(47);
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var (_, handler, _) = BuildSut();
        var cmd = new UpdateGamebookProgressCommand(Guid.NewGuid(), Guid.NewGuid(), 10);

        // Act
        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsConflictException()
    {
        // Arrange
        var (_, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new UpdateGamebookProgressCommand(session.Id, differentUser, 10);

        // Act
        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
