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
public sealed class RenameGamebookCampaignHandlerTests
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

    private static (FakeRepo repo, RenameGamebookCampaignHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var repo = new FakeRepo();
        var handler = new RenameGamebookCampaignHandler(repo);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Original Title");
        repo.Store.Add(session);
        return (repo, handler, session);
    }

    [Fact]
    public async Task Handle_RenamesCampaign_AndSaves()
    {
        var (repo, handler, session) = BuildSut();
        var cmd = new RenameGamebookCampaignCommand(session.Id, session.OwnerUserId, "Renamed Title");

        var dto = await handler.Handle(cmd, CancellationToken.None);

        dto.Title.Should().Be("Renamed Title");
        session.Title.Should().Be("Renamed Title");
        repo.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TrimsTitle()
    {
        var (_, handler, session) = BuildSut();
        var cmd = new RenameGamebookCampaignCommand(session.Id, session.OwnerUserId, "  Padded   ");

        var dto = await handler.Handle(cmd, CancellationToken.None);

        dto.Title.Should().Be("Padded");
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFoundException()
    {
        var (_, handler, _) = BuildSut();
        var cmd = new RenameGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), "X");

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsConflictException()
    {
        var (_, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new RenameGamebookCampaignCommand(session.Id, differentUser, "X");

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }
}
