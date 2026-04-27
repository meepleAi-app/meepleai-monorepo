using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure.Entities;
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
/// Unit tests for the Issue #593 (Wave A.3a) chip filters added to
/// <see cref="SearchSharedGamesQueryHandler"/>: <c>HasToolkit</c>,
/// <c>HasAgent</c>, <c>IsTopRated</c>. Each filter is exercised in its
/// true / false / null branches plus the relevant cross-BC boundary
/// conditions (ApprovalStatus, IsDefault toolkit exclusion, configurable
/// rating threshold).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SearchSharedGamesQuery_FilterTests
{
    private const int ApprovedStatus = 2;
    private const int DraftStatus = 0;

    private readonly Mock<ILogger<SearchSharedGamesQueryHandler>> _logger = new();

    // ---------------------------------------------------------------
    // Seed helpers
    // ---------------------------------------------------------------

    private static SharedGameEntity CreateSharedGame(string title, decimal? averageRating = null) => new()
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
        AverageRating = averageRating,
        HasKnowledgeBase = false,
    };

    private static GameEntity CreateGame(Guid sharedGameId, int approvalStatus = ApprovedStatus) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"Game-{sharedGameId:N}",
        SharedGameId = sharedGameId,
        ApprovalStatus = approvalStatus,
        CreatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Creates a non-default Toolkit linked to the supplied game (BR-02
    /// pattern: <c>CreateDefault</c> followed by <c>Override</c>).
    /// </summary>
    private static Toolkit CreateNonDefaultToolkit(Guid gameId)
        => Toolkit.CreateDefault(gameId).Override(Guid.NewGuid());

    private static Toolkit CreateDefaultToolkit(Guid gameId) => Toolkit.CreateDefault(gameId);

    /// <summary>
    /// Constructs an <see cref="AgentDefinition"/> via its internal ctor so
    /// we can attach a <c>GameId</c> (the public <c>Create</c> factory does
    /// not expose this parameter). Possible because of
    /// <c>InternalsVisibleTo("Api.Tests")</c> on the API project.
    /// </summary>
    private static AgentDefinition CreateAgentForGame(Guid gameId)
    {
        var type = AgentType.RagAgent;
        var config = AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f);
        return new AgentDefinition(
            id: Guid.NewGuid(),
            name: $"Agent-{gameId:N}",
            description: "Test agent",
            typeValue: type.Value,
            typeDescription: type.Description,
            config: config,
            strategyJson: "{}",
            promptsJson: "[]",
            toolsJson: "[]",
            isActive: false,
            status: AgentDefinitionStatus.Draft,
            createdAt: DateTime.UtcNow,
            updatedAt: null,
            gameId: gameId);
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

    /// <summary>
    /// Builds an in-memory <see cref="IConfiguration"/> seeded with the
    /// supplied <c>SharedGameCatalog:TopRatedThreshold</c> value, or empty
    /// for default-threshold tests (handler falls back to 4.5m).
    /// </summary>
    private static IConfiguration CreateConfiguration(decimal? topRatedThreshold = null)
    {
        var builder = new ConfigurationBuilder();
        if (topRatedThreshold.HasValue)
        {
            builder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SharedGameCatalog:TopRatedThreshold"] = topRatedThreshold.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
            });
        }
        return builder.Build();
    }

    private static SearchSharedGamesQuery BuildQuery(
        bool? hasToolkit = null,
        bool? hasAgent = null,
        bool? isTopRated = null) =>
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
            HasKnowledgeBase: null,
            HasToolkit: hasToolkit,
            HasAgent: hasAgent,
            IsTopRated: isTopRated);

    // ---------------------------------------------------------------
    // HasToolkit
    // ---------------------------------------------------------------

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasToolkitTrue_ReturnsOnlyGamesWithNonDefaultToolkit()
    {
        // Arrange
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var withToolkit = CreateSharedGame("With Toolkit");
        var withoutToolkit = CreateSharedGame("No Toolkit");
        db.SharedGames.AddRange(withToolkit, withoutToolkit);

        var game = CreateGame(withToolkit.Id);
        db.Games.Add(game);
        db.Toolkits.Add(CreateNonDefaultToolkit(game.Id));

        await db.SaveChangesAsync();
        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        // Act
        var result = await handler.Handle(BuildQuery(hasToolkit: true), CancellationToken.None);

        // Assert
        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("With Toolkit");
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasToolkitTrue_ExcludesGamesWithOnlyDefaultToolkit()
    {
        // BR-02: "with-toolkit" chip means at least one *non-default* (user
        // override) toolkit. Default toolkits are auto-created and don't
        // signal real customization, so they don't count.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sg = CreateSharedGame("Default Only");
        db.SharedGames.Add(sg);
        var game = CreateGame(sg.Id);
        db.Games.Add(game);
        db.Toolkits.Add(CreateDefaultToolkit(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasToolkit: true), CancellationToken.None);

        result.Total.Should().Be(0);
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasToolkitTrue_ExcludesToolkitsAttachedToUnapprovedGames()
    {
        // Cross-BC join requires Game.ApprovalStatus == Approved (2).
        // A toolkit on a Draft game shouldn't bubble its SharedGame up.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sg = CreateSharedGame("Draft Game Toolkit");
        db.SharedGames.Add(sg);
        var game = CreateGame(sg.Id, approvalStatus: DraftStatus);
        db.Games.Add(game);
        db.Toolkits.Add(CreateNonDefaultToolkit(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasToolkit: true), CancellationToken.None);

        result.Total.Should().Be(0);
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasToolkitFalse_ReturnsOnlyGamesWithoutNonDefaultToolkit()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var withToolkit = CreateSharedGame("With Toolkit");
        var withoutToolkit = CreateSharedGame("No Toolkit");
        db.SharedGames.AddRange(withToolkit, withoutToolkit);
        var game = CreateGame(withToolkit.Id);
        db.Games.Add(game);
        db.Toolkits.Add(CreateNonDefaultToolkit(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasToolkit: false), CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("No Toolkit");
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasToolkitNull_DoesNotApplyFilter()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateSharedGame("Game A"),
            CreateSharedGame("Game B"));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasToolkit: null), CancellationToken.None);

        result.Total.Should().Be(2);
    }

    // ---------------------------------------------------------------
    // HasAgent
    // ---------------------------------------------------------------

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasAgentTrue_ReturnsOnlyGamesWithLinkedAgent()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var withAgent = CreateSharedGame("With Agent");
        var withoutAgent = CreateSharedGame("No Agent");
        db.SharedGames.AddRange(withAgent, withoutAgent);
        var game = CreateGame(withAgent.Id);
        db.Games.Add(game);
        db.AgentDefinitions.Add(CreateAgentForGame(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasAgent: true), CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("With Agent");
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasAgentTrue_ExcludesAgentsAttachedToUnapprovedGames()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sg = CreateSharedGame("Draft Game Agent");
        db.SharedGames.Add(sg);
        var game = CreateGame(sg.Id, approvalStatus: DraftStatus);
        db.Games.Add(game);
        db.AgentDefinitions.Add(CreateAgentForGame(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasAgent: true), CancellationToken.None);

        result.Total.Should().Be(0);
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_HasAgentFalse_ReturnsOnlyGamesWithoutLinkedAgent()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var withAgent = CreateSharedGame("With Agent");
        var withoutAgent = CreateSharedGame("No Agent");
        db.SharedGames.AddRange(withAgent, withoutAgent);
        var game = CreateGame(withAgent.Id);
        db.Games.Add(game);
        db.AgentDefinitions.Add(CreateAgentForGame(game.Id));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(hasAgent: false), CancellationToken.None);

        result.Total.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Title.Should().Be("No Agent");
    }

    // ---------------------------------------------------------------
    // IsTopRated
    // ---------------------------------------------------------------

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_IsTopRatedTrue_UsesDefaultThresholdOf4_5()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateSharedGame("Top", averageRating: 4.6m),
            CreateSharedGame("AtThreshold", averageRating: 4.5m),
            CreateSharedGame("Below", averageRating: 4.4m),
            CreateSharedGame("Unrated", averageRating: null));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(isTopRated: true), CancellationToken.None);

        // 4.5 boundary is INCLUSIVE (>=), null AverageRating is excluded.
        result.Total.Should().Be(2);
        result.Items.Select(g => g.Title).Should().BeEquivalentTo(new[] { "Top", "AtThreshold" });
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_IsTopRatedFalse_IncludesNullRatingsAndBelowThreshold()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateSharedGame("Top", averageRating: 4.6m),
            CreateSharedGame("Below", averageRating: 4.4m),
            CreateSharedGame("Unrated", averageRating: null));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(isTopRated: false), CancellationToken.None);

        result.Total.Should().Be(2);
        result.Items.Select(g => g.Title).Should().BeEquivalentTo(new[] { "Below", "Unrated" });
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_IsTopRatedTrue_RespectsConfiguredThresholdOverride()
    {
        // Verifies SharedGameCatalog:TopRatedThreshold from IConfiguration
        // overrides the default 4.5m. With threshold = 4.0m, the 4.4 game
        // qualifies whereas under default it wouldn't.
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateSharedGame("Top", averageRating: 4.6m),
            CreateSharedGame("Mid", averageRating: 4.0m),
            CreateSharedGame("Low", averageRating: 3.9m));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(topRatedThreshold: 4.0m), _logger.Object);

        var result = await handler.Handle(BuildQuery(isTopRated: true), CancellationToken.None);

        result.Total.Should().Be(2);
        result.Items.Select(g => g.Title).Should().BeEquivalentTo(new[] { "Mid", "Top" });
    }

    [Fact(Skip = "EF Core InMemory provider cannot translate cross-BC nested sub-queries (ctxGames.Any inside Select projection); follow-up converts to Testcontainers integration test — tracked in Wave A.3a spec §10.")]
    public async Task Handle_IsTopRatedNull_DoesNotApplyFilter()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.AddRange(
            CreateSharedGame("Top", averageRating: 4.9m),
            CreateSharedGame("Unrated", averageRating: null));
        await db.SaveChangesAsync();

        var handler = new SearchSharedGamesQueryHandler(db, CreateHybridCache(), CreateConfiguration(), _logger.Object);

        var result = await handler.Handle(BuildQuery(isTopRated: null), CancellationToken.None);

        result.Total.Should().Be(2);
    }
}
