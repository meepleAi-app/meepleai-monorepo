using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
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
public sealed class GetCampaignProgressHandlerTests
{
    private sealed class FakeCampaignRepo : IGamebookCampaignSessionRepository
    {
        public List<GamebookCampaignSession> Store { get; } = new();
        public Task<GamebookCampaignSession?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<GamebookCampaignSession>> ListByOwnerAsync(Guid ownerUserId, Guid? gameId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GamebookCampaignSession>>(Store);
        public Task AddAsync(GamebookCampaignSession s, CancellationToken ct = default) { Store.Add(s); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeProgressRepo : ISessionBookProgressRepository
    {
        public List<SessionBookProgress> Store { get; } = new();

        public Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken ct)
            => Task.FromResult(Store.FirstOrDefault(p => p.CampaignSessionId == campaignSessionId && p.GameBookId == gameBookId));

        public Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<SessionBookProgress>>(
                Store.Where(p => p.CampaignSessionId == campaignSessionId).ToList());

        public Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken ct)
            => Task.FromResult(Store
                .Where(p => p.CampaignSessionId == campaignSessionId)
                .OrderByDescending(p => p.LastVisitedAt)
                .FirstOrDefault());

        public Task AddAsync(SessionBookProgress p, CancellationToken ct) { Store.Add(p); return Task.CompletedTask; }
        public Task UpdateAsync(SessionBookProgress p, CancellationToken ct) => Task.CompletedTask;

        public Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken ct)
        {
            Store.RemoveAll(p => p.CampaignSessionId == campaignSessionId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeGameBookRepo : IGameBookRepository
    {
        public List<GameBook> Store { get; } = new();

        public Task<GameBook?> GetByIdAsync(Guid id, CancellationToken ct)
            => Task.FromResult(Store.FirstOrDefault(b => b.Id == id));

        public Task<IReadOnlyList<GameBook>> ListByGameRefAsync(GameRef gameRef, Guid? ownerUserId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<GameBook>>(Store);

        public Task<GameBook?> FindCommunityByKbSourceAsync(Guid pdfDocId, CancellationToken ct)
            => Task.FromResult<GameBook?>(null);

        public Task AddAsync(GameBook book, CancellationToken ct) { Store.Add(book); return Task.CompletedTask; }
        public Task UpdateAsync(GameBook book, CancellationToken ct) => Task.CompletedTask;

        public Task<IReadOnlyList<GameBook>> ListByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct)
        {
            var set = new HashSet<Guid>(ids);
            return Task.FromResult<IReadOnlyList<GameBook>>(
                Store.Where(b => set.Contains(b.Id)).ToList());
        }
    }

    private static (FakeCampaignRepo c, FakeProgressRepo p, FakeGameBookRepo b, GetCampaignProgressHandler h, GamebookCampaignSession session) BuildSut()
    {
        var c = new FakeCampaignRepo();
        var p = new FakeProgressRepo();
        var b = new FakeGameBookRepo();
        var h = new GetCampaignProgressHandler(c, p, b);
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var session = GamebookCampaignSession.Create(GameRef.Shared(sharedGameId), userId, "Test Campaign");
        c.Store.Add(session);
        return (c, p, b, h, session);
    }

    private static GameBook BuildBook(Guid? id, string displayName, GameRef gameRef)
    {
        var book = GameBook.CreateCommunity(
            gameRef,
            displayName,
            GameBookRole.Narrative,
            ParagraphScheme.ParagraphNumber,
            "en",
            sequentialRead: false,
            kbSourceDocId: null,
            physicalOnly: false,
            createdBy: Guid.NewGuid());
        if (id is not null)
        {
            // Test seam: align book.Id with the SessionBookProgress.GameBookId pointer.
            typeof(GameBook)
                .GetProperty(nameof(GameBook.Id))!
                .SetValue(book, id.Value);
        }
        return book;
    }

    [Fact]
    public async Task Handle_CampaignMissing_ThrowsNotFoundException()
    {
        var (_, _, _, h, _) = BuildSut();
        var query = new GetCampaignProgressQuery(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await h.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_CallerIsNotOwner_ThrowsConflictException()
    {
        var (_, _, _, h, session) = BuildSut();
        var differentUser = Guid.NewGuid();
        var query = new GetCampaignProgressQuery(session.Id, differentUser);

        var act = async () => await h.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Handle_NoProgressRows_ReturnsEmptyList()
    {
        var (_, _, _, h, session) = BuildSut();
        var query = new GetCampaignProgressQuery(session.Id, session.OwnerUserId);

        var result = await h.Handle(query, CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithProgressRows_ReturnsDtoSortedByLastVisitedDesc()
    {
        // Acceptance criteria #1388: DTO has BookId, BookName, LastLocation, LastVisitedAt.
        // Most recently visited book appears first so the FE "Resume" list highlights it.
        var (_, progress, books, h, session) = BuildSut();
        var bookA = BuildBook(Guid.NewGuid(), "Press Start", session.GameRef);
        var bookB = BuildBook(Guid.NewGuid(), "Rules Reference", session.GameRef);
        books.Store.Add(bookA);
        books.Store.Add(bookB);

        var older = SessionBookProgress.Create(session.Id, bookA.Id, "§120");
        await Task.Delay(15, TestContext.Current.CancellationToken);
        var newer = SessionBookProgress.Create(session.Id, bookB.Id, "§250");
        progress.Store.Add(older);
        progress.Store.Add(newer);

        var result = await h.Handle(new GetCampaignProgressQuery(session.Id, session.OwnerUserId), CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].BookId.Should().Be(bookB.Id);
        result[0].BookName.Should().Be("Rules Reference");
        result[0].LastLocation.Should().Be("§250");
        result[0].LastVisitedAt.Should().Be(newer.LastVisitedAt);
        result[1].BookId.Should().Be(bookA.Id);
        result[1].BookName.Should().Be("Press Start");
    }

    [Fact]
    public async Task Handle_ProgressRowWithoutMatchingBook_IsFilteredOut()
    {
        // Defensive: book deletions may leave dangling progress rows. The endpoint
        // skips them rather than 500ing (so the FE always renders a coherent list).
        var (_, progress, books, h, session) = BuildSut();
        var bookA = BuildBook(Guid.NewGuid(), "Press Start", session.GameRef);
        books.Store.Add(bookA);
        progress.Store.Add(SessionBookProgress.Create(session.Id, bookA.Id, "§50"));
        progress.Store.Add(SessionBookProgress.Create(session.Id, Guid.NewGuid(), "§99")); // orphan

        var result = await h.Handle(new GetCampaignProgressQuery(session.Id, session.OwnerUserId), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].BookId.Should().Be(bookA.Id);
    }
}
