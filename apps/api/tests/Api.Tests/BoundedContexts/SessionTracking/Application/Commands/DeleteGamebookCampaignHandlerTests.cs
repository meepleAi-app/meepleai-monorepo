using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class DeleteGamebookCampaignHandlerTests
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

        public int SaveCalls { get; private set; }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            return Task.CompletedTask;
        }
    }

    private static (FakeRepo repo, DeleteGamebookCampaignHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var repo = new FakeRepo();
        var mediator = new Mock<IMediator>();
        var handler = new DeleteGamebookCampaignHandler(repo, mediator.Object);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(Guid.NewGuid(), userId, "Doomed Campaign");
        repo.Store.Add(session);
        return (repo, handler, session);
    }

    [Fact]
    public async Task Handle_SoftDeletes_AndSaves()
    {
        var (repo, handler, session) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, session.OwnerUserId);

        await handler.Handle(cmd, CancellationToken.None);

        session.IsDeleted.Should().BeTrue();
        session.DeletedAt.Should().NotBeNull();
        repo.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFoundException()
    {
        var (_, handler, _) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsConflictException()
    {
        var (_, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, differentUser);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        session.IsDeleted.Should().BeFalse();
    }
}
