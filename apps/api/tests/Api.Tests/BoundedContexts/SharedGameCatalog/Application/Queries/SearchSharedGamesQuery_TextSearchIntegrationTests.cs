using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
/// #4267 — the text search of <see cref="SearchSharedGamesQueryHandler"/>, on a real
/// PostgreSQL: the full-text operators it relies on do not exist in EF InMemory, which is
/// why the sibling unit suites are skipped and the search by text had no running test.
///
/// The catalog seeded here is built so that the order by title alone and the order by
/// relevance disagree: the games that only mention the searched text in their description
/// sort alphabetically before the ones that carry it in their title.
///
/// Group A, not the Group C its bounded context would suggest (#3742): on the Games shard of
/// dev-async, Group C is one serial chain of classes that the session timeout cuts short, and
/// in Group C this class was never reached — the .trx of the shard did not list it. Group A
/// has no other class on that shard, so here it runs at once. The class owns its database
/// and shares nothing with the others, so the group only decides when it runs.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SearchSharedGamesQuery_TextSearchIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"test_catalog_search_{Guid.NewGuid():N}";
    private MeepleAiDbContext _dbContext = null!;
    private SearchSharedGamesQueryHandler _handler = null!;

    public SearchSharedGamesQuery_TextSearchIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);

        _dbContext.SharedGames.AddRange(
            // Found by "win" only through the description.
            Game("Agricola", 2007, "Whoever farms best wins the game."),
            Game("Brass: Lancashire", 2009, "A winning strategy needs canals and rails."),
            Game("Zooloretto", 2006, "Winner of the Spiel des Jahres."),
            // Found by "win" through the title.
            Game("Wingspan", 2019, "Attract birds to your wildlife preserves."),
            Game("Wingspan: Asia", 2022, "A standalone expansion with new birds.", hasKnowledgeBase: true),
            // Not published: never in a public search.
            Game("Wingspan: Oceania", 2020, "More birds.", status: GameStatus.Draft),
            // Two words: one game has both in the title, the other only in the description.
            Game("Terraforming Mars", 2016, "Corporations compete to transform the red planet."),
            Game("Ark Nova", 2021, "Terra incognita for the keeper whose mistake mars the zoo."));
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        var titleResolver = new Mock<IGameTitleResolver>();
        titleResolver
            .Setup(r => r.EnrichAsync(It.IsAny<IReadOnlyList<SharedGameDto>>(), It.IsAny<CancellationToken>()))
            .Returns<IReadOnlyList<SharedGameDto>, CancellationToken>((games, _) => Task.FromResult(games));

        _handler = new SearchSharedGamesQueryHandler(
            _dbContext,
            new Mock<IBlobStorageService>().Object,
            CreateHybridCache(),
            new ConfigurationBuilder().Build(),
            new Mock<ILogger<SearchSharedGamesQueryHandler>>().Object,
            titleResolver.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Handle_SearchTerm_DefaultSort_PutsTitleMatchesBeforeDescriptionOnlyMatches()
    {
        var result = await _handler.Handle(Search("win"), TestContext.Current.CancellationToken);

        // Inside each group the order is the one the catalog had already: games with a
        // knowledge base first, then by title.
        result.Items.Select(g => g.Title).Should().Equal(
            "Wingspan: Asia",
            "Wingspan",
            "Agricola",
            "Brass: Lancashire",
            "Zooloretto");
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task Handle_SearchTerm_ShortPage_HoldsTheGamesFoundByTitle()
    {
        // The page of a typeahead: with the alphabetical order it was filled by the games
        // that mention the text in the description, and the game typed never showed up.
        var result = await _handler.Handle(Search("win", pageSize: 2), TestContext.Current.CancellationToken);

        result.Items.Select(g => g.Title).Should().Equal("Wingspan: Asia", "Wingspan");
        result.Total.Should().Be(5);
    }

    [Fact]
    public async Task Handle_SearchTerm_DefaultSortDescending_KeepsTitleMatchesFirst()
    {
        var result = await _handler.Handle(
            Search("win", sortDescending: true),
            TestContext.Current.CancellationToken);

        result.Items.Select(g => g.Title).Should().Equal(
            "Wingspan: Asia",
            "Wingspan",
            "Zooloretto",
            "Brass: Lancashire",
            "Agricola");
    }

    [Fact]
    public async Task Handle_SearchTerm_ExplicitSort_KeepsTheRequestedOrder()
    {
        var result = await _handler.Handle(
            Search("win", sortBy: "YearPublished"),
            TestContext.Current.CancellationToken);

        result.Items.Select(g => g.Title).Should().Equal(
            "Zooloretto",
            "Agricola",
            "Brass: Lancashire",
            "Wingspan",
            "Wingspan: Asia");
    }

    [Fact]
    public async Task Handle_MultiWordSearchTerm_MatchesTheTitleWordByWord()
    {
        var result = await _handler.Handle(Search("terra mars"), TestContext.Current.CancellationToken);

        result.Items.Select(g => g.Title).Should().Equal("Terraforming Mars", "Ark Nova");
    }

    [Fact]
    public async Task Handle_NoSearchTerm_KeepsKnowledgeBaseFirstThenTitle()
    {
        // Control: without a text there is nothing to rank by, and the order must not move.
        var result = await _handler.Handle(Search(null), TestContext.Current.CancellationToken);

        result.Items.Select(g => g.Title).Should().Equal(
            "Wingspan: Asia",
            "Agricola",
            "Ark Nova",
            "Brass: Lancashire",
            "Terraforming Mars",
            "Wingspan",
            "Zooloretto");
    }

    private static SearchSharedGamesQuery Search(
        string? term,
        int pageSize = 20,
        string sortBy = "Title",
        bool sortDescending = false) => new(
            SearchTerm: term,
            CategoryIds: null,
            MechanicIds: null,
            MinPlayers: null,
            MaxPlayers: null,
            MaxPlayingTime: null,
            MinComplexity: null,
            MaxComplexity: null,
            Status: null,
            PageNumber: 1,
            PageSize: pageSize,
            SortBy: sortBy,
            SortDescending: sortDescending);

    private static SharedGameEntity Game(
        string title,
        int yearPublished,
        string description,
        bool hasKnowledgeBase = false,
        GameStatus status = GameStatus.Published) => new()
        {
            Id = Guid.NewGuid(),
            Title = title,
            YearPublished = yearPublished,
            Description = description,
            MinPlayers = 1,
            MaxPlayers = 5,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            Status = (int)status,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            HasKnowledgeBase = hasKnowledgeBase,
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
}
