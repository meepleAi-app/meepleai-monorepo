using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
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

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

/// <summary>
/// Wave A.4 follow-up (Issue #616 — Task #68) — cross-BC Testcontainers integration test
/// for <see cref="GetSharedGameByIdQueryHandler"/>.
///
/// Seeds a realistic graph spanning 5 bounded contexts:
///   • Authentication: 2 distinct users (uq_toolkits_game_owner requires distinct OwnerUserIds).
///   • SharedGameCatalog: 1 SharedGame aggregate transitioned Draft → PendingApproval → Published.
///   • GameManagement: 1 GameEntity (ApprovalStatus=Approved=2, SharedGameId link).
///   • GameToolkit: 2 non-default Toolkits (CreateDefault + Override per user).
///   • KnowledgeBase: 1 AgentDefinition (RagAgent + Default config, _gameId shadow set)
///                    + 1 PdfDocument + 1 VectorDocument (IndexingStatus=completed).
///
/// Asserts the projection contract from the spec:
///   • ToolkitsCount=2, AgentsCount=1, KbsCount=1, HasKnowledgeBase=true
///   • IsTopRated=true (AverageRating=7.5m ≥ default threshold 4.0m)
///   • Nested previews populated (Toolkits/Agents/Kbs arrays HaveCount(N))
///   • HybridCache cache-hit on second invocation (DTO equivalence preserved).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetSharedGameByIdQueryHandlerCrossBcTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private SharedGameRepository _repository = null!;
    private GetSharedGameByIdQueryHandler _handler = null!;

    private static readonly Guid TestUserA = Guid.NewGuid();
    private static readonly Guid TestUserB = Guid.NewGuid();

    public GetSharedGameByIdQueryHandlerCrossBcTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"sharedgamebyid_crossbc_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        var collectorMock = new Mock<IDomainEventCollector>();
        collectorMock
            .Setup(x => x.GetAndClearEvents())
            .Returns(new List<IDomainEvent>().AsReadOnly());

        _repository = new SharedGameRepository(_dbContext, collectorMock.Object);
        _handler = new GetSharedGameByIdQueryHandler(
            _repository,
            _dbContext,
            CreateHybridCache(),
            CreateConfiguration(),
            new Mock<ILogger<GetSharedGameByIdQueryHandler>>().Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task Handle_WithSeededCrossBcGraph_ReturnsCompleteAggregatesAndCachesResult()
    {
        // ---------------------------------------------------------------------
        // Arrange — seed graph across BCs
        // ---------------------------------------------------------------------

        // 1) Authentication — 2 users (uq_toolkits_game_owner requires distinct owners)
        var userA = new UserEntity
        {
            Id = TestUserA,
            Email = "user-a@meepleai.test",
            DisplayName = "User A",
            CreatedAt = DateTime.UtcNow,
        };
        var userB = new UserEntity
        {
            Id = TestUserB,
            Email = "user-b@meepleai.test",
            DisplayName = "User B",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Users.AddRange(userA, userB);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 2) SharedGameCatalog — SharedGame aggregate transitioned to Published
        //    AverageRating=7.5m > 4.0m default threshold → IsTopRated=true.
        var sharedGame = SharedGame.Create(
            title: "Catan",
            yearPublished: 1995,
            description: "Strategy game of trading and building",
            minPlayers: 3,
            maxPlayers: 4,
            playingTimeMinutes: 90,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.test/catan.jpg",
            thumbnailUrl: "https://example.test/catan-thumb.jpg",
            rules: GameRules.Create("Rules content", "en"),
            createdBy: TestUserA,
            bggId: 13);
        sharedGame.SubmitForApproval(TestUserA);
        sharedGame.ApprovePublication(TestUserA);

        await _repository.AddAsync(sharedGame, TestContext.Current.CancellationToken);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 3) GameManagement — GameEntity linked to SharedGame, ApprovalStatus=Approved(2).
        //    NOTE: IsPublished is a computed column — must NOT be set by seeder.
        var game = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = "Catan",
            CreatedAt = DateTime.UtcNow,
            SharedGameId = sharedGame.Id,
            ApprovalStatus = 2,
        };
        _dbContext.Games.Add(game);

        // 4) GameToolkit — 2 non-default Toolkits with DISTINCT owners
        //    (uq_toolkits_game_owner unique index on (GameId, OwnerUserId)).
        var toolkitA = Toolkit.CreateDefault(game.Id).Override(TestUserA);
        var toolkitB = Toolkit.CreateDefault(game.Id).Override(TestUserB);
        _dbContext.Toolkits.AddRange(toolkitA, toolkitB);

        // 5) KnowledgeBase — AgentDefinition (Draft is fine — handler has no Status filter)
        var agent = AgentDefinition.Create(
            name: "Catan RAG",
            description: "RAG agent for Catan rules questions",
            type: AgentType.RagAgent,
            config: AgentDefinitionConfig.Default());
        _dbContext.AgentDefinitions.Add(agent);

        // 6) DocumentProcessing — PdfDocument (provides Language for KB preview projection)
        var pdf = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "catan-rules.pdf",
            FilePath = "/blobs/catan-rules.pdf",
            FileSizeBytes = 1024L * 1024L,
            UploadedByUserId = TestUserA,
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            SharedGameId = sharedGame.Id,
        };
        _dbContext.PdfDocuments.Add(pdf);

        // 7) KnowledgeBase — VectorDocument (handler filter: SharedGameId match
        //    + IndexingStatus="completed" + IndexedAt.HasValue).
        var vectorDoc = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdf.Id,
            SharedGameId = sharedGame.Id,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            ChunkCount = 50,
            TotalCharacters = 12_000,
            EmbeddingModel = "intfloat/multilingual-e5-base",
            EmbeddingDimensions = 768,
        };
        _dbContext.VectorDocuments.Add(vectorDoc);

        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // 8) AgentDefinition._gameId is a shadow property mapped to game_id column.
        //    Set via change tracker after the entity is attached, then save again.
        _dbContext.Entry(agent).Property("_gameId").CurrentValue = game.Id;
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);

        // ---------------------------------------------------------------------
        // Act — first invocation populates HybridCache; second hits cache.
        // ---------------------------------------------------------------------
        var firstResult = await _handler.Handle(
            new GetSharedGameByIdQuery(sharedGame.Id),
            TestContext.Current.CancellationToken);

        var secondResult = await _handler.Handle(
            new GetSharedGameByIdQuery(sharedGame.Id),
            TestContext.Current.CancellationToken);

        // ---------------------------------------------------------------------
        // Assert — projection contract + cache equivalence.
        // ---------------------------------------------------------------------
        firstResult.Should().NotBeNull();
        firstResult!.Id.Should().Be(sharedGame.Id);
        firstResult.Title.Should().Be("Catan");
        firstResult.AverageRating.Should().Be(7.5m);

        // Aggregate counts (one per BC fan-out)
        firstResult.ToolkitsCount.Should().Be(2, "two non-default toolkits with distinct owners are linked via approved game");
        firstResult.AgentsCount.Should().Be(1, "one AgentDefinition has _gameId set to the approved GameEntity.Id");
        firstResult.KbsCount.Should().Be(1, "one VectorDocument with SharedGameId match and IndexingStatus=completed");
        firstResult.HasKnowledgeBase.Should().BeTrue();

        // Spec heuristic — 7.5m ≥ default 4.0m threshold (handler compares decimal directly).
        firstResult.IsTopRated.Should().BeTrue();

        // Nested top-N previews
        firstResult.Toolkits.Should().HaveCount(2);
        firstResult.Agents.Should().HaveCount(1);
        firstResult.Kbs.Should().HaveCount(1);
        firstResult.Kbs[0].Language.Should().Be("en", "language is projected from PdfDocument navigation");

        // HybridCache contract — second call returns equivalent DTO from cache.
        secondResult.Should().BeEquivalentTo(firstResult);
    }

    // -------------------------------------------------------------------------
    // Helpers — copied from sibling unit test to keep this file self-contained.
    // -------------------------------------------------------------------------

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
}
