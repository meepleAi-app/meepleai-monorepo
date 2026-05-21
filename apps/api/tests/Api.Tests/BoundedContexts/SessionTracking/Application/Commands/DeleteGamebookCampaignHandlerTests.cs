using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using MediatR;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Commands;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class DeleteGamebookCampaignHandlerTests
{
    private sealed class CallCounter
    {
        private int _value;
        public int Next() => System.Threading.Interlocked.Increment(ref _value);
    }

    private sealed class FakeRepo : IGamebookCampaignSessionRepository
    {
        private readonly CallCounter _counter;
        public List<GamebookCampaignSession> Store { get; } = new();
        public int SaveCallOrder { get; private set; }

        public FakeRepo(CallCounter counter) => _counter = counter;

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession session, CancellationToken ct = default)
        {
            Store.Add(session);
            return Task.CompletedTask;
        }

        public int SaveCalls { get; private set; }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCalls++;
            SaveCallOrder = _counter.Next();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProgressRepo : ISessionBookProgressRepository
    {
        private readonly CallCounter _counter;
        public Guid? DeletedCampaignId { get; private set; }
        public int DeleteCallOrder { get; private set; }
        public int DeleteCalls { get; private set; }

        public FakeProgressRepo(CallCounter counter) => _counter = counter;

        public Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken ct)
        {
            DeletedCampaignId = campaignSessionId;
            DeleteCalls++;
            DeleteCallOrder = _counter.Next();
            return Task.CompletedTask;
        }

        public Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        public Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private static (FakeRepo repo, FakeProgressRepo progress, DeleteGamebookCampaignHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var counter = new CallCounter();
        var repo = new FakeRepo(counter);
        var progress = new FakeProgressRepo(counter);
        var mediator = new Mock<IMediator>();
        var handler = new DeleteGamebookCampaignHandler(repo, progress, mediator.Object);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Doomed Campaign");
        repo.Store.Add(session);
        return (repo, progress, handler, session);
    }

    [Fact]
    public async Task Handle_SoftDeletes_AndSaves()
    {
        var (repo, _, handler, session) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, session.OwnerUserId);

        await handler.Handle(cmd, CancellationToken.None);

        session.IsDeleted.Should().BeTrue();
        session.DeletedAt.Should().NotBeNull();
        repo.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFoundException()
    {
        var (_, _, handler, _) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsConflictException()
    {
        var (_, _, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, differentUser);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        session.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeletesSessionBookProgressOrphans_BeforeSaving()
    {
        // Issue #1394: SessionBookProgress rows have no FK cascade — handler must
        // explicitly purge them via _progress.DeleteByCampaignAsync BEFORE the shared
        // SaveChangesAsync flush so the delete + soft-delete persist atomically.
        var (repo, progress, handler, session) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, session.OwnerUserId);

        await handler.Handle(cmd, CancellationToken.None);

        progress.DeleteCalls.Should().Be(1);
        progress.DeletedCampaignId.Should().Be(session.Id);
        progress.DeleteCallOrder.Should().BeLessThan(repo.SaveCallOrder,
            "DeleteByCampaignAsync must stage the delete before SaveChangesAsync flushes the unit of work");
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_DoesNotTouchProgressOrphans()
    {
        // Defensive: when campaign lookup fails we must short-circuit before touching
        // the progress table to avoid orphaning rows that belong to a foreign user.
        var (_, progress, handler, _) = BuildSut();
        var cmd = new DeleteGamebookCampaignCommand(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();

        progress.DeleteCalls.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_DoesNotTouchProgressOrphans()
    {
        // Defensive: ownership check must run before any destructive action.
        var (_, progress, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new DeleteGamebookCampaignCommand(session.Id, differentUser);

        var act = async () => await handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();

        progress.DeleteCalls.Should().Be(0);
    }
}
