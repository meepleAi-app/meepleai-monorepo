using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetTopContributors;

/// <summary>
/// Unit tests for <see cref="GetTopContributorsQueryHandler"/>.
/// Issue #593 (Wave A.3a) — spec §5.4.
///
/// Covers:
/// - score formula: <c>TotalSessions + TotalWins * 2</c>
/// - deterministic tie-break (UserId ASC)
/// - privacy guards (suspended, non-Active status, null DisplayName)
/// - over-fetch buffer (limit applied AFTER privacy filter)
/// - cache delegation (factory invoked exactly once per call)
/// - constructor null-argument guards
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetTopContributorsQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<GetTopContributorsQueryHandler>> _loggerMock;
    private readonly GetTopContributorsQueryHandler _handler;

    public GetTopContributorsQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<GetTopContributorsQueryHandler>>();

        // Cache pass-through: invoke factory directly so we test handler logic, not cache.
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TopContributorDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<List<TopContributorDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));

        _handler = new GetTopContributorsQueryHandler(_db, _cacheMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    // ============================================================================================
    // Constructor & argument guards
    // ============================================================================================

    [Fact]
    public void Constructor_WithNullContext_Throws()
    {
        var act = () => new GetTopContributorsQueryHandler(null!, _cacheMock.Object, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("context");
    }

    [Fact]
    public void Constructor_WithNullCache_Throws()
    {
        var act = () => new GetTopContributorsQueryHandler(_db, null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("cache");
    }

    [Fact]
    public void Constructor_WithNullLogger_Throws()
    {
        var act = () => new GetTopContributorsQueryHandler(_db, _cacheMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_WithNullQuery_Throws()
    {
        var act = () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("query");
    }

    // ============================================================================================
    // Empty / no-data scenarios
    // ============================================================================================

    [Fact]
    public async Task Handle_WhenNoSessionsAndNoWins_ReturnsEmpty()
    {
        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenSessionsAreNotCompleted_AreIgnored()
    {
        var userId = Guid.NewGuid();
        SeedUser(userId, "Alice");
        SeedSession(userId, status: "InProgress"); // not completed
        SeedSession(userId, status: "Setup");      // not completed
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    // ============================================================================================
    // Score formula: TotalSessions + TotalWins * 2
    // ============================================================================================

    [Fact]
    public async Task Handle_ScoreFormula_TotalSessionsPlusWinsTimesTwo()
    {
        var alice = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedSession(alice, status: "Completed");
        SeedSession(alice, status: "Completed");
        SeedSession(alice, status: "Completed"); // 3 sessions
        SeedGameNightSession(alice, status: "Completed");
        SeedGameNightSession(alice, status: "Completed"); // 2 wins
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(alice);
        result[0].TotalSessions.Should().Be(3);
        result[0].TotalWins.Should().Be(2);
        result[0].Score.Should().Be(3 + 2 * 2); // 7
        result[0].DisplayName.Should().Be("Alice");
    }

    [Fact]
    public async Task Handle_OrdersByScoreDescending()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        var carol = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedUser(bob, "Bob");
        SeedUser(carol, "Carol");

        // Alice: 1 session, 5 wins → score = 11
        SeedSession(alice, status: "Completed");
        for (var i = 0; i < 5; i++) SeedGameNightSession(alice, status: "Completed");
        // Bob: 10 sessions, 0 wins → score = 10
        for (var i = 0; i < 10; i++) SeedSession(bob, status: "Completed");
        // Carol: 2 sessions, 1 win → score = 4
        SeedSession(carol, status: "Completed");
        SeedSession(carol, status: "Completed");
        SeedGameNightSession(carol, status: "Completed");

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(Limit: 5),
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(alice);
        result[0].Score.Should().Be(11);
        result[1].UserId.Should().Be(bob);
        result[1].Score.Should().Be(10);
        result[2].UserId.Should().Be(carol);
        result[2].Score.Should().Be(4);
    }

    [Fact]
    public async Task Handle_TieBreaksByUserIdAscending()
    {
        // Two contributors with identical scores; expect lower UserId first.
        var lowerId = new Guid("00000000-0000-0000-0000-000000000001");
        var higherId = new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff");
        SeedUser(lowerId, "Alpha");
        SeedUser(higherId, "Omega");

        SeedSession(lowerId, status: "Completed");
        SeedSession(higherId, status: "Completed");

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(2);
        result[0].UserId.Should().Be(lowerId);
        result[1].UserId.Should().Be(higherId);
    }

    // ============================================================================================
    // Privacy guards (spec §9 + §5.4)
    // ============================================================================================

    [Fact]
    public async Task Handle_ExcludesSuspendedUsers()
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedUser(bob, "Bob", isSuspended: true);

        SeedSession(alice, status: "Completed");
        SeedSession(bob, status: "Completed");

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(alice);
    }

    [Theory]
    [InlineData("Suspended")]
    [InlineData("Banned")]
    public async Task Handle_ExcludesNonActiveUsers(string status)
    {
        var alice = Guid.NewGuid();
        var bob = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedUser(bob, "Bob", status: status);

        SeedSession(alice, status: "Completed");
        SeedSession(bob, status: "Completed");

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(alice);
    }

    [Fact]
    public async Task Handle_ExcludesUsersWithNullDisplayName()
    {
        var alice = Guid.NewGuid();
        var anonymous = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedUser(anonymous, displayName: null);

        SeedSession(alice, status: "Completed");
        SeedSession(anonymous, status: "Completed");

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].UserId.Should().Be(alice);
    }

    [Fact]
    public async Task Handle_ExcludesSessionsWhereUserHasNoUserRecord()
    {
        // Foreign user-id with no Users row → must be filtered out (privacy guard step 4).
        var orphanUserId = Guid.NewGuid();
        SeedSession(orphanUserId, status: "Completed");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().BeEmpty();
    }

    // ============================================================================================
    // Limit + over-fetch buffer
    // ============================================================================================

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        // Seed 8 contributors with descending scores; ask for limit=3.
        for (var i = 0; i < 8; i++)
        {
            var userId = Guid.NewGuid();
            SeedUser(userId, $"User-{i}");
            // Higher index = higher score
            for (var s = 0; s <= i; s++) SeedSession(userId, status: "Completed");
        }
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(Limit: 3),
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(3);
        // Ordered descending: user-7 (8 sessions) > user-6 (7) > user-5 (6).
        result[0].TotalSessions.Should().Be(8);
        result[1].TotalSessions.Should().Be(7);
        result[2].TotalSessions.Should().Be(6);
    }

    [Fact]
    public async Task Handle_OverFetchBuffer_FillsLimitDespitePrivacyFiltering()
    {
        // Seed limit=3 visible users PLUS several suspended users with higher raw scores.
        // The over-fetch buffer (Math.Max(limit*2, limit+10) = 13) must include enough
        // candidates so that, after dropping suspended users, we still hit limit=3.
        var visibleUsers = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var userId = Guid.NewGuid();
            visibleUsers.Add(userId);
            SeedUser(userId, $"Visible-{i}");
            SeedSession(userId, status: "Completed"); // score=1
        }

        // Seed 5 suspended users with massive scores — these should rank first
        // by raw score but be filtered out by privacy guard.
        for (var i = 0; i < 5; i++)
        {
            var suspendedId = Guid.NewGuid();
            SeedUser(suspendedId, $"Suspended-{i}", isSuspended: true);
            for (var s = 0; s < 100; s++) SeedSession(suspendedId, status: "Completed");
        }

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(Limit: 3),
            TestContext.Current.CancellationToken);

        result.Should().HaveCount(3);
        result.Select(r => r.UserId).Should().BeEquivalentTo(visibleUsers);
    }

    // ============================================================================================
    // Game-night wins counted regardless of game approval status (spec §9 decision 4)
    // ============================================================================================

    [Fact]
    public async Task Handle_CountsWinsRegardlessOfGameApprovalStatus()
    {
        // Spec §9 decision 4: TotalWins counts ALL completed game-night sessions —
        // unpublished/private games still contribute to the score.
        var alice = Guid.NewGuid();
        SeedUser(alice, "Alice");
        SeedGameNightSession(alice, status: "Completed");
        SeedGameNightSession(alice, status: "Completed");
        SeedGameNightSession(alice, status: "Completed");
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await _handler.Handle(
            new GetTopContributorsQuery(),
            TestContext.Current.CancellationToken);

        result.Should().ContainSingle();
        result[0].TotalWins.Should().Be(3);
        result[0].TotalSessions.Should().Be(0);
        result[0].Score.Should().Be(6); // 0 + 3 * 2
    }

    // ============================================================================================
    // Cache integration
    // ============================================================================================

    [Fact]
    public async Task Handle_DelegatesToCacheWithCorrectKeyAndTag()
    {
        await _handler.Handle(
            new GetTopContributorsQuery(Limit: 7),
            TestContext.Current.CancellationToken);

        _cacheMock.Verify(c => c.GetOrCreateAsync(
            "top-contributors:7",
            It.IsAny<Func<CancellationToken, Task<List<TopContributorDto>>>>(),
            It.Is<string[]>(tags => tags.Length == 1 && tags[0] == GetTopContributorsQueryHandler.CacheTag),
            It.Is<TimeSpan?>(ts => ts == TimeSpan.FromHours(1)),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ============================================================================================
    // Helpers
    // ============================================================================================

    private void SeedUser(
        Guid id,
        string? displayName,
        bool isSuspended = false,
        string status = "Active")
    {
        _db.Users.Add(new UserEntity
        {
            Id = id,
            Email = $"{id:N}@test.local",
            DisplayName = displayName,
            IsSuspended = isSuspended,
            Status = status,
            AvatarUrl = null,
            Role = "user",
            Tier = "free",
            CreatedAt = DateTime.UtcNow,
            Language = "en",
            Theme = "system",
        });
    }

    private void SeedSession(Guid createdByUserId, string status)
    {
        _db.GameSessions.Add(new GameSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            CreatedByUserId = createdByUserId,
            Status = status,
            StartedAt = DateTime.UtcNow,
            CompletedAt = status == "Completed" ? DateTime.UtcNow : null,
            PlayersJson = "[]",
        });
    }

    private void SeedGameNightSession(Guid winnerId, string status)
    {
        _db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = Guid.NewGuid(),
            GameNightEventId = Guid.NewGuid(),
            SessionId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            GameTitle = "Test Game",
            PlayOrder = 1,
            Status = status,
            WinnerId = winnerId,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = status == "Completed" ? DateTimeOffset.UtcNow : null,
        });
    }
}
