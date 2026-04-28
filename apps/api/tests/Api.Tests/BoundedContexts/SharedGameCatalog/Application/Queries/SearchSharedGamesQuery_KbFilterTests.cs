using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests focused on the S2 (library-to-game epic) HasKnowledgeBase filter
/// extension of SearchSharedGamesQueryHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SearchSharedGamesQuery_KbFilterTests
{
    private readonly Mock<ILogger<SearchSharedGamesQueryHandler>> _logger = new();

    private static SharedGameEntity CreateGame(string title, bool hasKb) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        YearPublished = 2020,
        Description = "Desc",
        MinPlayers = 2,
        MaxPlayers = 4,
        PlayingTimeMinutes = 30,
        MinAge = 8,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        Status = (int)GameStatus.Published,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
        HasKnowledgeBase = hasKb,
    };

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    /// <summary>
    /// Empty IConfiguration — handler falls back to <c>DefaultTopRatedThreshold</c> (4.5m).
    /// Tests that need a different threshold can override via in-memory collection.
    /// </summary>
    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder().Build();

    private static SearchSharedGamesQuery BuildQuery(bool? hasKnowledgeBase) =>
        new(
            SearchTerm: null,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: GameStatus.Published,
            PageNumber: 1,
            PageSize: 20,
            SortBy: "Title",
            SortDescending: false,
            HasKnowledgeBase: hasKnowledgeBase);

    [Fact(Skip = "EF Core InMemory provider cannot translate the cross-BC nested sub-queries (ctxGames.Any) introduced by Issue #593 (Wave A.3a) Commit 1 in the SharedGameDto projection; follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_NoKbFilter_ReturnsAllGames()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateGame("With KB", hasKb: true),
            CreateGame("Without KB", hasKb: false));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        // Act
        var result = await handler.Handle(BuildQuery(hasKnowledgeBase: null), CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate the cross-BC nested sub-queries (ctxGames.Any) introduced by Issue #593 (Wave A.3a) Commit 1 in the SharedGameDto projection; follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasKnowledgeBaseTrue_ReturnsOnlyKbGames()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateGame("Azul", hasKb: true),
            CreateGame("Catan", hasKb: true),
            CreateGame("Monopoly", hasKb: false));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        // Act
        var result = await handler.Handle(BuildQuery(hasKnowledgeBase: true), CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(g => g.HasKnowledgeBase);
        result.Items.Select(g => g.Title).Should().BeEquivalentTo(new[] { "Azul", "Catan" });
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate the cross-BC nested sub-queries (ctxGames.Any) introduced by Issue #593 (Wave A.3a) Commit 1 in the SharedGameDto projection; follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasKnowledgeBaseFalse_ReturnsOnlyNonKbGames()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateGame("Azul", hasKb: true),
            CreateGame("Monopoly", hasKb: false),
            CreateGame("Risk", hasKb: false));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        // Act
        var result = await handler.Handle(BuildQuery(hasKnowledgeBase: false), CancellationToken.None);

        // Assert
        result.Total.Should().Be(2);
        result.Items.Should().OnlyContain(g => !g.HasKnowledgeBase);
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate the cross-BC nested sub-queries (ctxGames.Any) introduced by Issue #593 (Wave A.3a) Commit 1 in the SharedGameDto projection; follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_ProjectionIncludesHasKnowledgeBaseField()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(CreateGame("Azul", hasKb: true));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        // Act
        var result = await handler.Handle(BuildQuery(hasKnowledgeBase: null), CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle()
            .Which.HasKnowledgeBase.Should().BeTrue();
    }
}
