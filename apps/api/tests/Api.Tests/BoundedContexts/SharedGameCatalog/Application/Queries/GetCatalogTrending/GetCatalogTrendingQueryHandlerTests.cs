using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.GetCatalogTrending;

/// <summary>
/// Unit tests for <see cref="GetCatalogTrendingQueryHandler"/>.
///
/// Issue #2290: pin the contract that <see cref="TrendingGameDto.HasKnowledgeBase"/>
/// is populated from the joined <see cref="SharedGameEntity.HasKnowledgeBase"/>
/// projection, so the Discover Row 1 KB badge can render without an N+1 lookup
/// on <see cref="SharedGameDto"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2290")]
public sealed class GetCatalogTrendingQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db;
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<GetCatalogTrendingQueryHandler>> _loggerMock;
    private readonly GetCatalogTrendingQueryHandler _handler;

    public GetCatalogTrendingQueryHandlerTests()
    {
        _db = TestDbContextFactory.CreateInMemoryDbContext();
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<GetCatalogTrendingQueryHandler>>();

        // Cache pass-through: invoke factory directly so we test handler logic, not cache.
        _cacheMock
            .Setup(c => c.GetOrCreateAsync(
                It.IsAny<string>(),
                It.IsAny<Func<CancellationToken, Task<List<TrendingGameDto>>>>(),
                It.IsAny<string[]?>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()))
            .Returns((
                string _,
                Func<CancellationToken, Task<List<TrendingGameDto>>> factory,
                string[]? __,
                TimeSpan? ___,
                CancellationToken ct) => factory(ct));

        _handler = new GetCatalogTrendingQueryHandler(_db, _cacheMock.Object, _loggerMock.Object);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Handle_ProjectsHasKnowledgeBase_FromSharedGameEntity_Issue2290()
    {
        // Arrange — two games with distinct HasKnowledgeBase values and one
        // analytics event per game so both surface in the trending result set.
        var gameA = SeedGame(hasKnowledgeBase: true, title: "AI-Ready Game");
        var gameB = SeedGame(hasKnowledgeBase: false, title: "Plain Game");

        SeedEvent(gameA.Id, GameEventType.Play);   // weight 10 → ranks first
        SeedEvent(gameB.Id, GameEventType.Search); // weight 3  → ranks second
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2, "both games have at least one event in the last 7 days");

        var aiReady = result.Single(r => r.GameId == gameA.Id);
        aiReady.HasKnowledgeBase.Should().BeTrue(
            "GameA has HasKnowledgeBase=true on the entity — the new projection must surface it");
        aiReady.Title.Should().Be("AI-Ready Game");

        var plain = result.Single(r => r.GameId == gameB.Id);
        plain.HasKnowledgeBase.Should().BeFalse(
            "GameB has HasKnowledgeBase=false on the entity — the projection must preserve the negative case");
        plain.Title.Should().Be("Plain Game");
    }

    [Fact]
    public async Task Handle_DefaultsHasKnowledgeBaseToFalse_WhenGameRowIsMissing_Issue2290()
    {
        // Arrange — one analytics event but no SharedGameEntity row (orphan
        // event row, e.g. game deleted after the event was recorded). The
        // handler uses `gameMap.TryGetValue(...)` so the DTO must default
        // safely instead of throwing.
        var orphanGameId = Guid.NewGuid();
        SeedEvent(orphanGameId, GameEventType.View);
        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        var result = await _handler.Handle(
            new GetCatalogTrendingQuery { Limit = 10 },
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].GameId.Should().Be(orphanGameId);
        result[0].Title.Should().Be("Unknown Game", "the existing fallback path is preserved");
        result[0].HasKnowledgeBase.Should().BeFalse(
            "missing SharedGameEntity rows must default HasKnowledgeBase to false");
    }

    private SharedGameEntity SeedGame(bool hasKnowledgeBase, string title)
    {
        var entity = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = 2024,
            Description = "Test game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = 1, // Published
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKnowledgeBase,
            IsDeleted = false,
        };
        _db.SharedGames.Add(entity);
        return entity;
    }

    private void SeedEvent(Guid gameId, GameEventType eventType)
    {
        _db.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            EventType = (int)eventType,
            UserId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow.AddHours(-1),
        });
    }
}
