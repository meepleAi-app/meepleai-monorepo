using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for complete SharedGame workflows (manual creation and PDF wizard flows).
/// Issue #4232: Backend integration tests for SharedGame complete workflows
/// Tests: 5 complete workflow scenarios using CQRS pipeline
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CompleteWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public CompleteWorkflowIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"complete_workflow_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Create WebApplicationFactory with test configuration
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");

                builder.ConfigureAppConfiguration((context, configBuilder) =>
                {
                    configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["OPENROUTER_API_KEY"] = "test-key",
                        ["ConnectionStrings:Postgres"] = connectionString
                    });
                });

                builder.ConfigureTestServices(services =>
                {
                    // Replace DbContext with test database
                    services.RemoveAll(typeof(DbContextOptions<MeepleAiDbContext>));
                    services.AddDbContext<MeepleAiDbContext>(options =>
                        options.UseNpgsql(connectionString, o => o.UseVector())
                            .EnableSensitiveDataLogging());

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IQdrantService));
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IQdrantService>(_ => Mock.Of<Api.Services.IQdrantService>());
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Mock BGG API service to avoid real API calls
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();

                    // Setup mock BGG API responses
                    mockBggApi
                        .Setup(x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync((int bggId, CancellationToken ct) => new Api.Models.BggGameDetailsDto(
                            bggId,
                            $"Test Game {bggId}",
                            "Test game description from BGG",
                            2024,
                            2,
                            4,
                            60,
                            45,
                            90,
                            12,
                            8.5,
                            8.0,
                            1000,
                            3.5,
                            $"https://example.com/thumb_{bggId}.jpg",
                            $"https://example.com/image_{bggId}.jpg",
                            new List<string> { "Strategy", "Adventure" },
                            new List<string> { "Deck Building", "Area Control" },
                            new List<string> { "Test Designer" },
                            new List<string> { "Test Publisher" }));

                    services.AddScoped(_ => mockBggApi.Object);

                    // Ensure domain event collector is registered
                    services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector,
                        Api.SharedKernel.Application.Services.DomainEventCollector>();

                    // Mock authorization - allow all for testing
                    services.AddAuthorization(options =>
                    {
                        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                            .RequireAssertion(_ => true)
                            .Build();
                    });
                });
            });

        // Initialize database with migrations
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            // Seed minimal test data
            await SeedTestDataAsync(dbContext);
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    /// <summary>
    /// Seed minimal test data required for workflow tests.
    /// </summary>
    private async Task SeedTestDataAsync(MeepleAiDbContext dbContext)
    {
        // Seed test user for FK relationships
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = "workflow-test@test.com",
            DisplayName = "Workflow Test User",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Set<UserEntity>().Add(user);

        // Seed categories
        var category = new GameCategoryEntity
        {
            Id = Guid.NewGuid(),
            Name = "Strategy",
            Slug = "strategy"
        };
        dbContext.Set<GameCategoryEntity>().Add(category);

        // Seed mechanics
        var mechanic = new GameMechanicEntity
        {
            Id = Guid.NewGuid(),
            Name = "Deck Building",
            Slug = "deck-building"
        };
        dbContext.Set<GameMechanicEntity>().Add(mechanic);

        await dbContext.SaveChangesAsync();
    }

    // ========================================
    // TEST 1: Manual Creation Workflow
    // ========================================

    [Fact]
    public async Task ManualCreation_CreatesSharedGameSuccessfully()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = dbContext.Set<UserEntity>().First().Id;

        var command = new CreateSharedGameCommand(
            Title: "Test Board Game",
            YearPublished: 2024,
            Description: "A test board game for integration testing",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: 2.5m,
            AverageRating: 7.5m,
            ImageUrl: "https://example.com/game-image.jpg",
            ThumbnailUrl: "https://example.com/game-thumb.jpg",
            Rules: null,
            BggId: null,
            CreatedBy: userId
        );

        // Act
        var gameId = await mediator.Send(command);

        // Assert
        gameId.Should().NotBe(Guid.Empty);

        // Verify game was created in DB with correct properties
        using var assertScope = _factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var createdGame = await assertDbContext.SharedGames.FindAsync(gameId);

        createdGame.Should().NotBeNull();
        createdGame!.Title.Should().Be("Test Board Game");
        createdGame.Status.Should().Be((int)GameStatus.Draft);
        createdGame.CreatedBy.Should().Be(userId);
        createdGame.MinPlayers.Should().Be(2);
        createdGame.MaxPlayers.Should().Be(4);
        createdGame.PlayingTimeMinutes.Should().Be(60);
    }

    // ========================================
    // TEST 2: PDF Wizard Workflow (Simplified)
    // ========================================

    [Fact]
    public async Task PdfWizard_ImportFromBgg_CreatesSharedGame()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = dbContext.Set<UserEntity>().First().Id;

        // Step: Import game from BGG (simplified workflow without PDF)
        var importCommand = new ImportGameFromBggCommand(
            BggId: 174430,
            UserId: userId
        );

        // Act
        var gameId = await mediator.Send(importCommand);

        // Assert
        gameId.Should().NotBe(Guid.Empty);

        // Verify game was created with BggId and correct status
        using var assertScope = _factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var createdGame = await assertDbContext.SharedGames.FindAsync(gameId);

        createdGame.Should().NotBeNull();
        createdGame!.BggId.Should().Be(174430);
        createdGame.Status.Should().Be((int)GameStatus.Draft); // Default status
        createdGame.Title.Should().Be("Test Game 174430"); // From mock
        createdGame.CreatedBy.Should().Be(userId);
    }

    // ========================================
    // TEST 3: Agent Linking Workflow
    // ========================================

    [Fact]
    public async Task AgentLinking_LinksAgentToSharedGame()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = dbContext.Set<UserEntity>().First().Id;

        // Step 1: Create a SharedGame
        var createGameCommand = new CreateSharedGameCommand(
            Title: "Game for Agent Linking",
            YearPublished: 2024,
            Description: "Test game for agent linking",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: 2.5m,
            AverageRating: 7.5m,
            ImageUrl: "https://example.com/agent-game.jpg",
            ThumbnailUrl: "https://example.com/agent-thumb.jpg",
            Rules: null,
            CreatedBy: userId,
            BggId: null
        );
        var gameId = await mediator.Send(createGameCommand);

        // Step 2: Create AgentDefinition using helper (in fresh scope)
        Guid agentId;
        using (var agentScope = _factory.Services.CreateScope())
        {
            var agentDbContext = agentScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var agentMediator = agentScope.ServiceProvider.GetRequiredService<IMediator>();

            var agent = await CreateTestAgentAsync(agentDbContext, "Test Agent for Linking", "Agent for linking test");
            agentId = agent.Id;

            // Act: Link agent to SharedGame
            var linkCommand = new LinkAgentToSharedGameCommand(gameId, agentId);
            await agentMediator.Send(linkCommand);
        }

        // Assert (in fresh scope)
        using var assertScope = _factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await assertDbContext.SharedGames.FindAsync(gameId);

        updatedGame.Should().NotBeNull();
        updatedGame!.AgentDefinitionId.Should().Be(agentId);
    }

    // ========================================
    // TEST 4: KB Documents Visibility
    // ========================================

    [Fact]
    public async Task KbDocuments_AreVisibleForSharedGame()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = dbContext.Set<UserEntity>().First().Id;

        // Step 1: Create SharedGame
        var createGameCommand = new CreateSharedGameCommand(
            Title: "Game with KB Documents",
            YearPublished: 2024,
            Description: "Test game for KB documents",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: 2.5m,
            AverageRating: 7.5m,
            ImageUrl: "https://example.com/kb-game.jpg",
            ThumbnailUrl: "https://example.com/kb-thumb.jpg",
            Rules: null,
            CreatedBy: userId,
            BggId: null
        );
        var gameId = await mediator.Send(createGameCommand);

        // Step 2: Create PDF document with proper FK using helper
        var (pdfDoc, game) = await CreateTestPdfWithGameAsync(dbContext, userId, "kb-test-rules.pdf", gameId);

        // Step 3: Link PDF to SharedGame via SharedGameDocumentEntity
        var sharedGameDoc = new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = gameId,
            PdfDocumentId = pdfDoc.Id,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true
        };
        dbContext.Set<SharedGameDocumentEntity>().Add(sharedGameDoc);
        await dbContext.SaveChangesAsync();

        // Step 4: Create VectorDocument (simulating indexed document) - use GameEntity.Id for FK
        var vectorDoc = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id, // FK to GameEntity (private games), not SharedGame
            PdfDocumentId = pdfDoc.Id,
            ChunkCount = 10,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "nomic-embed-text",
            EmbeddingDimensions = 768
        };
        dbContext.Set<VectorDocumentEntity>().Add(vectorDoc);
        await dbContext.SaveChangesAsync();

        // Step 5: Create runtime agent with document configuration
        var (agent, agentConfig) = await CreateRuntimeAgentWithDocumentsAsync(
            dbContext,
            userId,
            new List<Guid> { sharedGameDoc.Id }, // Reference SharedGameDocumentEntity.Id
            "KB Test Runtime Agent"
        );

        // Act: Query for agent documents
        var query = new GetAgentDocumentsQuery(agent.Id); // Use runtime agent ID
        var documentsDto = await mediator.Send(query);

        // Assert: Documents list should contain the PDF
        documentsDto.Should().NotBeNull();
        documentsDto!.Documents.Should().NotBeEmpty();
        documentsDto.Documents.Should().Contain(d => d.PdfDocumentId == pdfDoc.Id);
    }

    // ========================================
    // TEST 5: Complete Flow End-to-End (Backend)
    // ========================================

    [Fact]
    public async Task CompleteFlow_PdfToAgentCreation()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var userId = dbContext.Set<UserEntity>().First().Id;

        // Step 1: Upload PDF (with proper GameEntity FK)
        var (pdfDoc, game) = await CreateTestPdfWithGameAsync(dbContext, userId, "e2e-test-rules.pdf");

        // Step 2: Extract metadata (simulated - commands may require actual file)
        // Note: ExtractGameMetadataFromPdfQuery requires actual file processing
        // For integration test, we'll skip this step and proceed to import

        // Step 3: Create SharedGame from BGG
        var importCommand = new ImportGameFromBggCommand(
            BggId: 174430,
            UserId: userId
        );
        var sharedGameId = await mediator.Send(importCommand);
        sharedGameId.Should().NotBe(Guid.Empty);

        // Update PDF to link to SharedGame
        pdfDoc.SharedGameId = sharedGameId;
        dbContext.Set<PdfDocumentEntity>().Update(pdfDoc);
        await dbContext.SaveChangesAsync();

        // Link PDF to SharedGame via SharedGameDocumentEntity
        var sharedGameDoc = new SharedGameDocumentEntity
        {
            Id = Guid.NewGuid(),
            SharedGameId = sharedGameId,
            PdfDocumentId = pdfDoc.Id,
            DocumentType = 0, // Rulebook
            Version = "1.0",
            IsActive = true
        };
        dbContext.Set<SharedGameDocumentEntity>().Add(sharedGameDoc);
        await dbContext.SaveChangesAsync();

        // Step 4: Wait for embedding (simulate with VectorDocumentEntity) - use GameEntity.Id
        var vectorDoc = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id, // FK to GameEntity (private games), not SharedGame
            PdfDocumentId = pdfDoc.Id,
            ChunkCount = 15,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "nomic-embed-text",
            EmbeddingDimensions = 768
        };
        dbContext.Set<VectorDocumentEntity>().Add(vectorDoc);
        await dbContext.SaveChangesAsync();

        // Step 5: Create runtime agent with document configuration
        var (runtimeAgent, agentConfig) = await CreateRuntimeAgentWithDocumentsAsync(
            dbContext,
            userId,
            new List<Guid> { sharedGameDoc.Id }, // Reference SharedGameDocumentEntity.Id
            "E2E Runtime Agent"
        );

        // Also link AgentDefinition to SharedGame (for Agent Builder integration)
        using (var agentDefScope = _factory.Services.CreateScope())
        {
            var agentDefDbContext = agentDefScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var agentDefMediator = agentDefScope.ServiceProvider.GetRequiredService<IMediator>();

            var agentDef = await CreateTestAgentAsync(agentDefDbContext, "E2E Agent Definition", "Complete flow");

            var linkCommand = new LinkAgentToSharedGameCommand(sharedGameId, agentDef.Id);
            await agentDefMediator.Send(linkCommand);
        }

        // Step 6: Query KB documents (using runtime agent)
        var docsQuery = new GetAgentDocumentsQuery(runtimeAgent.Id);
        var documentsDto = await mediator.Send(docsQuery);

        // Assert: All steps successful
        documentsDto.Should().NotBeNull();
        documentsDto!.Documents.Should().NotBeEmpty();
        documentsDto.Documents.Should().Contain(d => d.PdfDocumentId == pdfDoc.Id);

        using var assertScope = _factory.Services.CreateScope();
        var assertDbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var finalGame = await assertDbContext.SharedGames.FindAsync(sharedGameId);

        finalGame.Should().NotBeNull();
        finalGame!.AgentDefinitionId.Should().NotBeNull(); // Linked to AgentDefinition
        finalGame.BggId.Should().Be(174430);
    }

    // ========================================
    // HELPER METHODS - Full Test Infrastructure
    // ========================================

    /// <summary>
    /// Creates a minimal GameEntity (private game) to satisfy PDF FK constraints.
    /// </summary>
    private async Task<GameEntity> CreateTestGameEntityAsync(MeepleAiDbContext dbContext, Guid userId, string name = "Test Private Game")
    {
        var gameEntity = new GameEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            CreatedAt = DateTime.UtcNow,
            MinPlayers = 2,
            MaxPlayers = 4,
            YearPublished = 2024
        };
        dbContext.Set<GameEntity>().Add(gameEntity);
        await dbContext.SaveChangesAsync();
        return gameEntity;
    }

    /// <summary>
    /// Creates a test PDF document with proper FK to GameEntity and optional link to SharedGame.
    /// </summary>
    private async Task<(PdfDocumentEntity pdf, GameEntity game)> CreateTestPdfWithGameAsync(
        MeepleAiDbContext dbContext,
        Guid userId,
        string fileName = "test-rules.pdf",
        Guid? sharedGameId = null)
    {
        // Create GameEntity for FK
        var game = await CreateTestGameEntityAsync(dbContext, userId, $"Game for {fileName}");

        // Create PDF document
        var pdfDoc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            GameId = game.Id,
            SharedGameId = sharedGameId,
            FileName = fileName,
            FilePath = $"/test/uploads/{fileName}",
            FileSizeBytes = 1024 * 150,
            UploadedByUserId = userId,
            UploadedAt = DateTime.UtcNow,
            ProcessingState = "Ready",
            ProcessingStatus = "completed"
        };
        dbContext.Set<PdfDocumentEntity>().Add(pdfDoc);
        await dbContext.SaveChangesAsync();

        return (pdfDoc, game);
    }

    /// <summary>
    /// Creates a test AgentDefinition using factory pattern.
    /// </summary>
    private async Task<AgentDefinition> CreateTestAgentAsync(
        MeepleAiDbContext dbContext,
        string name = "Test Agent",
        string description = "Test agent description")
    {
        var agentConfig = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.AgentDefinitionConfig.Create(
            model: "gpt-4",
            maxTokens: 2048,
            temperature: 0.7f
        );
        var agent = AgentDefinition.Create(
            name: name,
            description: description,
            type: Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects.AgentType.RagAgent,
            config: agentConfig
        );
        dbContext.Set<AgentDefinition>().Add(agent);
        await dbContext.SaveChangesAsync();
        return agent;
    }

    /// <summary>
    /// Creates a runtime AgentEntity with configuration and selected documents.
    /// This is for testing GetAgentDocumentsQuery which requires runtime agents.
    /// </summary>
    private async Task<(AgentEntity agent, AgentConfigurationEntity config)> CreateRuntimeAgentWithDocumentsAsync(
        MeepleAiDbContext dbContext,
        Guid userId,
        List<Guid> documentIds,
        string name = "Runtime Test Agent")
    {
        // Create runtime agent entity
        var agent = new AgentEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = "RAG",
            StrategyName = "HybridSearch",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Set<AgentEntity>().Add(agent);
        await dbContext.SaveChangesAsync();

        // Create configuration with selected documents
        var config = new Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            LlmProvider = 0, // OpenRouter
            LlmModel = "gpt-4",
            AgentMode = 0, // Chat
            SelectedDocumentIdsJson = System.Text.Json.JsonSerializer.Serialize(documentIds),
            Temperature = 0.7m,
            MaxTokens = 2048,
            IsCurrent = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };
        dbContext.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>().Add(config);
        await dbContext.SaveChangesAsync();

        return (agent, config);
    }
}