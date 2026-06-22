using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class ListMyGamebookCampaignsHandlerTests
{
    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
        {
            var filtered = Store.Where(s => s.OwnerUserId == o);
            if (g.HasValue)
            {
                filtered = filtered.Where(s => s.GameRef.Id == g.Value);
            }
            return Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(filtered.ToList());
        }

        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default) { Store.Add(s); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
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

        public Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
            => Task.FromResult(Store
                .Where(p => p.CampaignSessionId == campaignSessionId)
                .OrderByDescending(p => p.LastVisitedAt)
                .FirstOrDefault());

        public Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken) { Store.Add(progress); return Task.CompletedTask; }
        public Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken)
        {
            Store.RemoveAll(p => p.CampaignSessionId == campaignSessionId);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Handle_NoCampaigns_ReturnsEmpty()
    {
        // Arrange
        var campaigns = new FakeCampaignRepo();
        var progress = new FakeProgressRepo();
        var handler = new ListMyGamebookCampaignsHandler(campaigns, progress);
        var query = new ListMyGamebookCampaignsQuery(Guid.NewGuid(), GameId: null);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MultipleCampaigns_EachReturnsItsOwnMostRecentProgress()
    {
        // Arrange: 2 campaigns; each has progress rows so resume returns the right paragraph.
        var campaigns = new FakeCampaignRepo();
        var progress = new FakeProgressRepo();
        var handler = new ListMyGamebookCampaignsHandler(campaigns, progress);
        var userId = Guid.NewGuid();

        var sessionA = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "A");
        var sessionB = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "B");
        campaigns.Store.Add(sessionA);
        campaigns.Store.Add(sessionB);

        // Campaign A: older then newer (newer wins → §47)
        var aOlder = SessionBookProgress.Create(sessionA.Id, Guid.NewGuid(), "§10");
        await Task.Delay(10, TestContext.Current.CancellationToken);
        var aNewer = SessionBookProgress.Create(sessionA.Id, Guid.NewGuid(), "§47");
        progress.Store.Add(aOlder);
        progress.Store.Add(aNewer);

        // Campaign B: single row (§999)
        var bOnly = SessionBookProgress.Create(sessionB.Id, Guid.NewGuid(), "§999");
        progress.Store.Add(bOnly);

        var query = new ListMyGamebookCampaignsQuery(userId, GameId: null);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Single(d => d.Id == sessionA.Id).CurrentParagraph.Should().Be(47);
        result.Single(d => d.Id == sessionB.Id).CurrentParagraph.Should().Be(999);
    }

    [Fact]
    public async Task Handle_CampaignWithoutProgress_ReturnsZeroCurrentParagraph()
    {
        // Arrange
        var campaigns = new FakeCampaignRepo();
        var progress = new FakeProgressRepo();
        var handler = new ListMyGamebookCampaignsHandler(campaigns, progress);
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Empty");
        campaigns.Store.Add(session);
        var query = new ListMyGamebookCampaignsQuery(userId, GameId: null);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].CurrentParagraph.Should().Be(0);
        result[0].History.Should().BeEmpty();
    }
}
