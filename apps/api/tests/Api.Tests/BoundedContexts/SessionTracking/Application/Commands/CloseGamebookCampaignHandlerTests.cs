using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class CloseGamebookCampaignHandlerTests
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

    private static (FakeRepo repo, CloseGamebookCampaignHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var repo = new FakeRepo();
        var handler = new CloseGamebookCampaignHandler(repo);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Campagna Eldoria");
        repo.Store.Add(session);
        return (repo, handler, session);
    }

    [Fact]
    public async Task Handle_Completed_SetsOutcomeAndSaves()
    {
        var (repo, handler, session) = BuildSut();
        var cmd = new CloseGamebookCampaignCommand(session.Id, session.OwnerUserId, GamebookCampaignOutcome.Completed);

        var dto = await handler.Handle(cmd, CancellationToken.None);

        session.Outcome.Should().Be(GamebookCampaignOutcome.Completed);
        session.IsClosed.Should().BeTrue();
        dto.Outcome.Should().Be((int)GamebookCampaignOutcome.Completed);
        dto.CompletedAt.Should().NotBeNull();
        repo.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_Abandoned_SetsOutcome()
    {
        var (_, handler, session) = BuildSut();
        var cmd = new CloseGamebookCampaignCommand(session.Id, session.OwnerUserId, GamebookCampaignOutcome.Abandoned);

        var dto = await handler.Handle(cmd, CancellationToken.None);

        dto.Outcome.Should().Be((int)GamebookCampaignOutcome.Abandoned);
        session.Outcome.Should().Be(GamebookCampaignOutcome.Abandoned);
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFound()
    {
        var (_, handler, _) = BuildSut();
        var cmd = new CloseGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid(), GamebookCampaignOutcome.Completed);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsForbidden()
    {
        // IDOR guard: a non-owner must not be able to close someone else's campaign.
        var (repo, handler, session) = BuildSut();
        var attacker = Guid.NewGuid();
        var cmd = new CloseGamebookCampaignCommand(session.Id, attacker, GamebookCampaignOutcome.Abandoned);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
        session.IsClosed.Should().BeFalse();
        repo.SaveCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAlreadyClosed_ThrowsConflict()
    {
        // Idempotency guard (409): a second close on an already-closed campaign fails.
        var (_, handler, session) = BuildSut();
        session.Close(GamebookCampaignOutcome.Completed, session.OwnerUserId);
        var cmd = new CloseGamebookCampaignCommand(session.Id, session.OwnerUserId, GamebookCampaignOutcome.Abandoned);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        session.Outcome.Should().Be(GamebookCampaignOutcome.Completed); // unchanged
    }
}
