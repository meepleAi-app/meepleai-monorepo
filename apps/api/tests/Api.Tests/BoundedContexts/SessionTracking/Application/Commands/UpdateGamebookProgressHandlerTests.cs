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
    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();
        public int SaveChangesCount { get; private set; }

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default)
        {
            Store.Add(s);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProgressRepo : ISessionBookProgressRepository
    {
        public List<SessionBookProgress> Store { get; } = new();

        public Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken cancellationToken)
            => Task.FromResult(Store.FirstOrDefault(p =>
                p.CampaignSessionId == campaignSessionId && p.GameBookId == gameBookId));

        public Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => Task.FromResult<IReadOnlyList<SessionBookProgress>>(
                Store.Where(p => p.CampaignSessionId == campaignSessionId).ToList());

        public Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken)
        {
            Store.Add(progress);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken)
        {
            // In-memory entity is mutated in place by UpdateLocation; no-op for the fake.
            return Task.CompletedTask;
        }
    }

    private static (FakeCampaignRepo campaigns, FakeProgressRepo progress, UpdateGamebookProgressHandler handler, GamebookCampaignSession session) BuildSut()
    {
        var campaigns = new FakeCampaignRepo();
        var progress = new FakeProgressRepo();
        var handler = new UpdateGamebookProgressHandler(campaigns, progress);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Test Campaign");
        campaigns.Store.Add(session);
        return (campaigns, progress, handler, session);
    }

    [Fact]
    public async Task Handle_FirstCall_CreatesPerBookProgressAndAdvancesParagraph()
    {
        // Arrange
        var (campaigns, progress, handler, session) = BuildSut();
        var bookId = Guid.NewGuid();
        var cmd = new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, bookId, 47);

        // Act
        var dto = await handler.Handle(cmd, CancellationToken.None);

        // Assert
        dto.CurrentParagraph.Should().Be(47);
        progress.Store.Should().HaveCount(1);
        var row = progress.Store[0];
        row.CampaignSessionId.Should().Be(session.Id);
        row.GameBookId.Should().Be(bookId);
        row.LastLocation.Should().Be("§47");
        campaigns.SaveChangesCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_SecondCall_UpdatesExistingPerBookProgress()
    {
        // Arrange
        var (campaigns, progress, handler, session) = BuildSut();
        var bookId = Guid.NewGuid();
        await handler.Handle(
            new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, bookId, 10),
            CancellationToken.None);

        // Act
        var dto = await handler.Handle(
            new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, bookId, 47),
            CancellationToken.None);

        // Assert
        dto.CurrentParagraph.Should().Be(47);
        progress.Store.Should().HaveCount(1, "second call updates the existing (campaign,book) row");
        progress.Store[0].LastLocation.Should().Be("§47");
        campaigns.SaveChangesCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_TwoBooksInSameCampaign_CreatesTwoSeparateProgressRows()
    {
        // Arrange
        var (_, progress, handler, session) = BuildSut();
        var bookA = Guid.NewGuid();
        var bookB = Guid.NewGuid();

        // Act
        await handler.Handle(
            new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, bookA, 12),
            CancellationToken.None);
        await handler.Handle(
            new UpdateGamebookProgressCommand(session.Id, session.OwnerUserId, bookB, 200),
            CancellationToken.None);

        // Assert
        progress.Store.Should().HaveCount(2);
        progress.Store.Should().Contain(p => p.GameBookId == bookA && p.LastLocation == "§12");
        progress.Store.Should().Contain(p => p.GameBookId == bookB && p.LastLocation == "§200");
    }

    [Fact]
    public async Task Handle_WhenSessionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var (_, _, handler, _) = BuildSut();
        var cmd = new UpdateGamebookProgressCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10);

        // Act
        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenCallerIsNotOwner_ThrowsConflictException()
    {
        // Arrange
        var (_, _, handler, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var cmd = new UpdateGamebookProgressCommand(session.Id, differentUser, Guid.NewGuid(), 10);

        // Act
        var act = async () => await handler.Handle(cmd, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
    }
}
