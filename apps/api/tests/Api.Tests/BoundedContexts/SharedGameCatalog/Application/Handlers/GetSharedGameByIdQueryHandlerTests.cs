using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class GetSharedGameByIdQueryHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<ILogger<GetSharedGameByIdQueryHandler>> _loggerMock;

    public GetSharedGameByIdQueryHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _loggerMock = new Mock<ILogger<GetSharedGameByIdQueryHandler>>();
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection().Build();

    private GetSharedGameByIdQueryHandler CreateHandler(MeepleAiDbContext db) =>
        new(_repositoryMock.Object, db, CreateHybridCache(), CreateConfiguration(), _loggerMock.Object);

    [Fact]
    public async Task Handle_WithExistingGame_ReturnsDetailDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Catan",
            1995,
            "Description",
            3,
            4,
            90,
            10,
            2.5m,
            7.8m,
            "https://example.com/catan.jpg",
            "https://example.com/thumb.jpg",
            GameRules.Create("Rules content", "en"),
            userId,
            13);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(game.Id);
        result.Title.Should().Be("Catan");
        result.YearPublished.Should().Be(1995);
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(4);
        result.PlayingTimeMinutes.Should().Be(90);
        result.MinAge.Should().Be(10);
        result.ComplexityRating.Should().Be(2.5m);
        result.AverageRating.Should().Be(7.8m);
        result.BggId.Should().Be(13);
        result.Rules.Should().NotBeNull();
        result.Rules!.Content.Should().Be("Rules content");
        result.Rules.Language.Should().Be("en");
        result.CreatedBy.Should().Be(userId);
        // A.4 — empty DB → all aggregates default to 0/false.
        result.ToolkitsCount.Should().Be(0);
        result.AgentsCount.Should().Be(0);
        result.KbsCount.Should().Be(0);
        result.ContributorsCount.Should().Be(0);
        result.HasKnowledgeBase.Should().BeFalse();
        // AverageRating 7.8 < default TopRatedThreshold 4.0 on 0..5 scale? No: handler reads
        // configuration value but here the persisted AverageRating is on 0..10 BGG scale.
        // The handler treats AverageRating directly so 7.8 > 4.0 ⇒ IsTopRated=true.
        result.IsTopRated.Should().BeTrue();
        result.IsNew.Should().BeFalse();
        result.Toolkits.Should().NotBeNull().And.BeEmpty();
        result.Agents.Should().NotBeNull().And.BeEmpty();
        result.Kbs.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithGameWithoutRules_ReturnsNullRules()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var game = SharedGame.Create(
            "Simple Game",
            2020,
            "Description",
            2,
            4,
            60,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var query = new GetSharedGameByIdQuery(gameId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = CreateHandler(db);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Rules.Should().BeNull();
        // No AverageRating ⇒ IsTopRated=false.
        result.IsTopRated.Should().BeFalse();
    }
}
