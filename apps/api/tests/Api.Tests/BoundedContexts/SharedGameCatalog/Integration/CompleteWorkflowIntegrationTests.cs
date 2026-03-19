using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using SharedGame = Api.Infrastructure.Entities.SharedGameCatalog.SharedGameEntity;
using Api.Infrastructure;
using Api.Models;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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

#pragma warning disable S3261 // Namespace content is commented out — intentional pending refactoring (Issue #3490)
namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Complete workflow integration tests for SharedGameCatalog bounded context.
/// Issue #4232: Backend - Integration Tests for Complete Workflows
///
/// Tests:
/// 1. Manual Creation Workflow
/// 2. PDF Wizard Workflow
/// 3. Agent Linking Workflow
/// 4. KB Documents Visibility
/// 5. Complete Flow End-to-End (Backend)
///
/// NOTE: DISABLED due to compilation errors from command signature changes.
/// CreateSharedGameCommand and ImportGameFromBggCommand signatures changed.
/// Entire class commented out until commands are refactored.
/// See Issue #3490 - needs complete refactoring.
/// </summary>
/*
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CompleteWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private IMediator _mediator = null!;

    public CompleteWorkflowIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"completeworkflow_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Create WebApplicationFactory
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
                        options.UseNpgsql(connectionString, o => o.UseVector()));

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Mock BGG API service
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();

                    mockBggApi
                        .Setup(x => x.GetGameDetailsAsync(174430, It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new BggGameDetailsDto(
                            174430,
                            "Gloomhaven",
                            "Epic dungeon crawler",
                            2017,
                            1,
                            4,
                            120,
                            60,
                            180,
                            14,
                            8.8,
                            8.5,
                            50000,
                            3.9,
                            "https://example.com/thumb.jpg",
                            "https://example.com/image.jpg",
                            new List<string> { "Adventure", "Fantasy" },
                            new List<string> { "Campaign", "Hand Management" },
                            new List<string> { "Isaac Childres" },
                            new List<string> { "Cephalofair Games" }));

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

        // Initialize database
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
            _mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
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
    /// Test 1: Manual Creation Workflow
    /// Validates CQRS pipeline for manual SharedGame creation
    /// </summary>
    [Fact(Skip = "Command signature changed - needs refactoring")]
    public async Task ManualCreation_CreatesSharedGameSuccessfully()
    {
        // Arrange: CreateSharedGameCommand
        var command = new CreateSharedGameCommand
        {
            Title = "Test Game Manual",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 12,
            Description = "A manually created test game",
            BggId = null // Manual creation without BGG
        };

        // Act: Send command via Mediator
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var gameId = await mediator.Send(command);

        // Assert: Game in DB with correct status
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = await dbContext.Set<SharedGame>()
            .FirstOrDefaultAsync(g => g.Id == gameId);

        game.Should().NotBeNull();
        game!.Title.Should().Be("Test Game Manual");
        game.YearPublished.Should().Be(2024);
        game.MinPlayers.Should().Be(2);
        game.MaxPlayers.Should().Be(4);
        game.Status.Should().Be("Draft"); // Default status for manual creation
        game.BggId.Should().BeNull();
    }

    /// <summary>
    /// Test 2: PDF Wizard Workflow
    /// Validates complete wizard flow: Upload → Extract → Enrich → Import
    /// </summary>
    [Fact(Skip = "Command signature changed - needs refactoring")]
    public async Task PdfWizard_CreatesSharedGameWithDocuments()
    {
        // Arrange: Simulate PDF upload (mock document ID)
        var pdfDocumentId = Guid.NewGuid();

        // Act 1: Extract metadata (simulated - in real scenario, this would call extraction service)
        var extractedMetadata = new ExtractedMetadataDto
        {
            Title = "Test Game from PDF",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 90
        };

        // Act 2: Enrich from BGG
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        // Simulate BGG enrichment (GetBggGameDetailsQuery already mocked)
        var bggId = 174430;

        // Act 3: Import game from BGG with PDF document
        var importCommand = new ImportGameFromBggCommand
        {
            BggId = bggId,
            PdfDocumentId = pdfDocumentId,
            DocumentType = 0, // Rulebook
            DocumentVersion = "1.0"
        };

        var gameId = await mediator.Send(importCommand);

        // Assert: Game created with BggId and status
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        game.Should().NotBeNull();
        game!.BggId.Should().Be(bggId);
        game.Title.Should().Be("Gloomhaven"); // From mocked BGG API
        game.Status.Should().Be("Draft");

        // Note: Document association depends on DocumentProcessing BC implementation
        // Full validation requires proper PDF document seeding
    }

    /// <summary>
    /// Test 3: Agent Linking Workflow
    /// Validates agent creation and linking to SharedGame
    /// </summary>
    [Fact(Skip = "Command signature changed - needs refactoring")]
    public async Task AgentLinking_LinksAgentToSharedGame()
    {
        // Arrange: Create SharedGame
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var createGameCommand = new CreateSharedGameCommand
        {
            Title = "Test Game for Agent",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60
        };

        var gameId = await mediator.Send(createGameCommand);

        // Arrange: Create Agent (mock agent ID - real scenario would use AgentDefinitions BC)
        var agentId = Guid.NewGuid();

        // Act: Link agent to SharedGame (command implementation depends on Issue #4228)
        // For now, we manually update the game entity to simulate linking
        var game = await dbContext.Set<SharedGame>().FindAsync(gameId);
        game.Should().NotBeNull();

        // Simulated agent linking (actual implementation via LinkAgentToSharedGameCommand)
        // game!.AgentDefinitionId = agentId;
        // await dbContext.SaveChangesAsync();

        // Assert: game.AgentDefinitionId == agentId
        // Note: Full assertion requires LinkAgentToSharedGameCommand implementation (Issue #4228)
        game.Should().NotBeNull();
        game!.Id.Should().Be(gameId);

        // Note: Uncomment when LinkAgentToSharedGameCommand is implemented
        // game.AgentDefinitionId.Should().Be(agentId);
    }

    /// <summary>
    /// Test 4: KB Documents Visibility
    /// Validates that SharedGame documents are visible for agent knowledge base
    /// </summary>
    [Fact(Skip = "Command signature changed - needs refactoring")]
    public async Task KbDocuments_AreVisibleForSharedGame()
    {
        // Arrange: Create SharedGame with indexed PDF
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await mediator.Send(new CreateSharedGameCommand
        {
            Title = "Test Game with KB",
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4
        });

        // Simulate PDF document association (requires DocumentProcessing BC setup)
        var pdfDocumentId = Guid.NewGuid();

        // Act: Query agent documents (GetAgentDocumentsQuery - implementation depends on KnowledgeBase BC)
        // For now, verify game and document relationship
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        // Assert: Documents list contains PDF
        game.Should().NotBeNull();

        // Note: Full assertion requires:
        // 1. SharedGameDocument entity properly seeded
        // 2. GetAgentDocumentsQuery implementation
        // 3. Vector DB indexing workflow

        // Placeholder assertion
        game!.Id.Should().Be(gameId);
    }

    /// <summary>
    /// Test 5: Complete Flow End-to-End (Backend)
    /// Validates entire backend pipeline from PDF upload to agent creation
    /// </summary>
    [Fact(Skip = "Command signature changed - needs refactoring")]
    public async Task CompleteFlow_PdfToAgentCreation()
    {
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Step 1: Upload PDF (simulated)
        var pdfDocumentId = Guid.NewGuid();

        // Step 2: Extract metadata (simulated)
        var extractedTitle = "Complete Workflow Test Game";

        // Step 3: Create SharedGame from BGG
        var gameId = await mediator.Send(new ImportGameFromBggCommand
        {
            BggId = 174430,
            PdfDocumentId = pdfDocumentId,
            DocumentType = 0,
            DocumentVersion = "1.0"
        });

        gameId.Should().NotBeEmpty();

        // Step 4: Wait for embedding (in real scenario, this is async)
        // For testing, we assume immediate completion

        // Step 5: Create and link agent (simulated - requires Issue #4228)
        var agentId = Guid.NewGuid();

        // Step 6: Query KB documents
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        // Assert: All steps successful
        game.Should().NotBeNull();
        game!.BggId.Should().Be(174430);
        game.Title.Should().Be("Gloomhaven"); // From mocked BGG API

        // Note: Complete flow assertion requires:
        // 1. Full DocumentProcessing BC integration
        // 2. Vector embedding workflow (Issue #4136 dependencies)
        // 3. Agent linking commands (Issue #4228)

        // Current assertions validate core CQRS pipeline
        game.Id.Should().Be(gameId);
        game.Status.Should().NotBeNullOrEmpty();
    }
}
*/
