using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Application.DTOs.Leaderboard;
using Api.BoundedContexts.GameManagement.Application.Queries.Leaderboard;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Leaderboard;

/// <summary>
/// Handler tests for the game leaderboard (#1467). Covers the social-visibility predicate,
/// registered-only aggregation, ranking, top-N, temporal filter and degenerate cases.
/// Membership is stubbed via <see cref="IGroupMemoryRepository"/>; cache executes the factory.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "1467")]
public sealed class GetGameLeaderboardQueryHandlerTests : IDisposable
{
    private static readonly Guid Game = Guid.NewGuid();
    private static readonly Guid Me = Guid.NewGuid();

    private readonly MeepleAiDbContext _db;
    private readonly Mock<IGroupMemoryRepository> _groupRepoMock = new();
    private readonly Mock<IHybridCacheService> _cacheMock = new();

    public GetGameLeaderboardQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"Leaderboard_{Guid.NewGuid()}")
            .Options;
        _db = new MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            new Mock<IDomainEventCollector>().Object);

        // Default: caller belongs to no groups.
        _groupRepoMock
            .Setup(r => r.GetGroupIdsForUserAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());

        // Cache executes the factory (exercises real logic).
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<GameLeaderboardResponse>>>(),
                It.IsAny<string[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, Func<CancellationToken, Task<GameLeaderboardResponse>>, string[], TimeSpan?, CancellationToken>(
                (_, factory, _, _, ct) => factory(ct));
    }

    public void Dispose() => _db.Dispose();

    private GetGameLeaderboardQueryHandler CreateHandler() =>
        new(_db, _groupRepoMock.Object, _cacheMock.Object);

    private PlayRecordEntity AddRecord(
        Guid? gameId,
        Guid createdBy,
        PlayRecordVisibility visibility,
        Guid? groupId,
        PlayRecordStatus status,
        DateTime sessionDate,
        params (Guid? userId, string name, int? wins, int? points)[] players)
    {
        var rec = new PlayRecordEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            GameName = "Game",
            CreatedByUserId = createdBy,
            Visibility = (int)visibility,
            GroupId = groupId,
            Status = (int)status,
            SessionDate = sessionDate,
            ScoringConfigJson = "{}",
            CreatedAt = sessionDate,
            UpdatedAt = sessionDate,
        };

        foreach (var p in players)
        {
            var player = new RecordPlayerEntity
            {
                Id = Guid.NewGuid(),
                PlayRecordId = rec.Id,
                UserId = p.userId,
                DisplayName = p.name,
            };
            if (p.wins.HasValue)
            {
                player.Scores.Add(new RecordScoreEntity { Id = Guid.NewGuid(), RecordPlayerId = player.Id, Dimension = "wins", Value = p.wins.Value });
            }
            if (p.points.HasValue)
            {
                player.Scores.Add(new RecordScoreEntity { Id = Guid.NewGuid(), RecordPlayerId = player.Id, Dimension = "points", Value = p.points.Value });
            }
            rec.Players.Add(player);
        }

        _db.PlayRecords.Add(rec);
        return rec;
    }

    private Task SaveAsync() => _db.SaveChangesAsync(TestContext.Current.CancellationToken);

    private Task<GameLeaderboardResponse> Run(int limit = 10, DateTime? since = null) =>
        CreateHandler().Handle(new GetGameLeaderboardQuery(Game, Me, since, limit), TestContext.Current.CancellationToken);

    // ── Ranking ────────────────────────────────────────────────────────

    [Fact]
    public async Task Ranking_OrdersByWinsThenAvgScore()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Alice", 1, 80), (b, "Bob", 0, 90));
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 2),
            (a, "Alice", 1, 90), (b, "Bob", 1, 70));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().HaveCount(2);
        result.Entries[0].PlayerId.Should().Be(a);  // 2 wins
        result.Entries[0].Wins.Should().Be(2);
        result.Entries[0].Plays.Should().Be(2);
        result.Entries[0].AvgScore.Should().Be(85);
        result.Entries[1].PlayerId.Should().Be(b);  // 1 win
        result.Entries[1].Wins.Should().Be(1);
    }

    [Fact]
    public async Task EmptyData_ReturnsEmptyResponse()
    {
        var result = await Run();

        result.GameId.Should().Be(Game);
        result.Entries.Should().BeEmpty();
        result.ReturnedCount.Should().Be(0);
    }

    [Fact]
    public async Task LimitCapsResults()
    {
        for (var i = 0; i < 5; i++)
        {
            AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
                (Guid.NewGuid(), $"P{i}", i, 10 * i));
        }
        await SaveAsync();

        var result = await Run(limit: 2);

        result.Entries.Should().HaveCount(2);
        result.ReturnedCount.Should().Be(2);
    }

    [Fact]
    public async Task SinceFilter_ExcludesOlderRecords()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2025, 1, 1),
            (a, "Alice", 1, 50));
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 6, 1),
            (a, "Alice", 1, 90));
        await SaveAsync();

        var result = await Run(since: new DateTime(2026, 1, 1));

        result.Entries.Should().ContainSingle();
        result.Entries[0].Plays.Should().Be(1);
        result.Entries[0].AvgScore.Should().Be(90);
    }

    // ── Visibility ─────────────────────────────────────────────────────

    [Fact]
    public async Task Privacy_OtherUsersPrivateRecordIsExcluded()
    {
        var other = Guid.NewGuid();
        var x = Guid.NewGuid();
        AddRecord(Game, other, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (x, "Stranger", 5, 100));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task GroupRecord_IncludedWhenCallerIsMember()
    {
        var group = Guid.NewGuid();
        var other = Guid.NewGuid();
        var x = Guid.NewGuid();
        _groupRepoMock
            .Setup(r => r.GetGroupIdsForUserAsync(Me, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { group });
        AddRecord(Game, other, PlayRecordVisibility.Group, group, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (x, "Teammate", 3, 75));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().ContainSingle();
        result.Entries[0].PlayerId.Should().Be(x);
        result.Entries[0].Wins.Should().Be(3);
    }

    [Fact]
    public async Task GroupRecord_ExcludedWhenCallerNotMember()
    {
        var group = Guid.NewGuid();
        var other = Guid.NewGuid();
        AddRecord(Game, other, PlayRecordVisibility.Group, group, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (Guid.NewGuid(), "Outsider", 3, 75));
        await SaveAsync();

        var result = await Run();  // default: no groups

        result.Entries.Should().BeEmpty();
    }

    // ── Aggregation rules ──────────────────────────────────────────────

    [Fact]
    public async Task GuestPlayer_IsExcluded()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Alice", 1, 80), (null, "Guest", 1, 99));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().ContainSingle();
        result.Entries[0].PlayerId.Should().Be(a);
    }

    [Fact]
    public async Task NoWinsDimension_WinsAreZero()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Alice", null, 80));
        await SaveAsync();

        var result = await Run();

        result.Entries[0].Wins.Should().Be(0);
        result.Entries[0].Plays.Should().Be(1);
    }

    [Fact]
    public async Task NoPointsDimension_AvgScoreIsNull()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Alice", 1, null));
        await SaveAsync();

        var result = await Run();

        result.Entries[0].AvgScore.Should().BeNull();
    }

    [Fact]
    public async Task NonCompletedRecord_IsExcluded()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.InProgress, new DateTime(2026, 1, 1),
            (a, "Alice", 1, 80));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task FreeFormRecord_IsExcluded()
    {
        var a = Guid.NewGuid();
        AddRecord(null, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Alice", 1, 80));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task DisplayName_TakenFromMostRecentRecord()
    {
        var a = Guid.NewGuid();
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (a, "Marco", 1, 80));
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 3, 1),
            (a, "Marco Rossi", 1, 80));
        await SaveAsync();

        var result = await Run();

        result.Entries[0].DisplayName.Should().Be("Marco Rossi");
        result.Entries[0].Initials.Should().Be("MR");
        result.Entries[0].LastPlayedAt.Should().Be(new DateTime(2026, 3, 1));
    }

    [Fact]
    public async Task TieBreak_IsDeterministicByUserId()
    {
        var lower = new Guid("00000000-0000-0000-0000-000000000001");
        var higher = new Guid("00000000-0000-0000-0000-000000000002");
        AddRecord(Game, Me, PlayRecordVisibility.Private, null, PlayRecordStatus.Completed, new DateTime(2026, 1, 1),
            (higher, "H", 1, 50), (lower, "L", 1, 50));
        await SaveAsync();

        var result = await Run();

        result.Entries.Should().HaveCount(2);
        result.Entries[0].PlayerId.Should().Be(lower);
        result.Entries[1].PlayerId.Should().Be(higher);
    }
}
