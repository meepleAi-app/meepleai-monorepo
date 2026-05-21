using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Queries;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class GetGamebookCampaignHandlerTests
{
    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();

        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid o, Guid? g, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);

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

    private static (FakeCampaignRepo campaigns, FakeProgressRepo progress, GetGamebookCampaignHandler handler) BuildSut()
    {
        var campaigns = new FakeCampaignRepo();
        var progress = new FakeProgressRepo();
        var handler = new GetGamebookCampaignHandler(campaigns, progress);
        return (campaigns, progress, handler);
    }

    [Fact]
    public async Task Handle_SharedCampaign_DtoExposesSharedDiscriminator()
    {
        // Issue #1392: GamebookCampaignDto must surface GameRef discriminator so
        // FE callers can stop hardcoding GameRefKind.Shared in client routing.
        var (campaigns, _, handler) = BuildSut();
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "Shared");
        campaigns.Store.Add(session);
        var query = new GetGamebookCampaignQuery(session.Id, userId);

        var dto = await handler.Handle(query, TestContext.Current.CancellationToken);

        dto.GameRefId.Should().Be(sharedGameId);
        dto.GameRefKind.Should().Be((int)GameRefKind.Shared);
        // Legacy alias preserved.
        dto.GameId.Should().Be(sharedGameId);
    }

    [Fact]
    public async Task Handle_PrivateCampaign_DtoExposesPrivateDiscriminator()
    {
        // Issue #1392: GameRefKind=1 propagates so FE can request the right
        // book picker variant for private-library campaigns.
        var (campaigns, _, handler) = BuildSut();
        var userId = Guid.NewGuid();
        var privateGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Private(privateGameId), userId, "Private");
        campaigns.Store.Add(session);
        var query = new GetGamebookCampaignQuery(session.Id, userId);

        var dto = await handler.Handle(query, TestContext.Current.CancellationToken);

        dto.GameRefId.Should().Be(privateGameId);
        dto.GameRefKind.Should().Be((int)GameRefKind.Private);
        dto.GameId.Should().Be(privateGameId);
    }

    [Fact]
    public async Task Handle_NoProgressRows_ReturnsZeroParagraphAndEmptyHistory()
    {
        // Arrange
        var (campaigns, _, handler) = BuildSut();
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Test");
        campaigns.Store.Add(session);
        var query = new GetGamebookCampaignQuery(session.Id, userId);

        // Act
        var dto = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        dto.CurrentParagraph.Should().Be(0);
        dto.History.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ExistingCampaignWithProgress_ReturnsMostRecentBookParagraph()
    {
        // Arrange: campaign + 2 SessionBookProgress rows; "newer" wins on LastVisitedAt.
        // SessionBookProgress.Create stamps LastVisitedAt=UtcNow at the moment of
        // creation, so the second row's timestamp will be strictly later than the
        // first (Task.Delay guarantees a measurable gap on all platforms).
        var (campaigns, progress, handler) = BuildSut();
        var userId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), userId, "Test");
        campaigns.Store.Add(session);

        var olderProgress = SessionBookProgress.Create(session.Id, Guid.NewGuid(), "§100");
        await Task.Delay(10, TestContext.Current.CancellationToken);
        var newerProgress = SessionBookProgress.Create(session.Id, Guid.NewGuid(), "§250");
        progress.Store.Add(olderProgress);
        progress.Store.Add(newerProgress);

        var query = new GetGamebookCampaignQuery(session.Id, userId);

        // Act
        var dto = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        dto.CurrentParagraph.Should().Be(250);
        dto.History.Should().ContainSingle().Which.Should().Be(250);
    }

    [Fact]
    public async Task Handle_SessionMissing_ThrowsNotFoundException()
    {
        // Arrange
        var (_, _, handler) = BuildSut();
        var query = new GetGamebookCampaignQuery(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = async () => await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CallerIsNotOwner_ThrowsForbiddenException()
    {
        // Issue #1404: ownership failures must map to HTTP 403, not 409, so external
        // clients can distinguish authorization failures from real conflicts.
        var (campaigns, _, handler) = BuildSut();
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(Guid.NewGuid()), ownerId, "Test");
        campaigns.Store.Add(session);
        var query = new GetGamebookCampaignQuery(session.Id, otherUserId);

        // Act
        var act = async () => await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>();
    }
}
