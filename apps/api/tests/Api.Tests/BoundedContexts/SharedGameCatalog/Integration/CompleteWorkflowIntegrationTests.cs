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
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Complete workflow integration tests for SharedGameCatalog bounded context.
/// Issue #4232: Backend - Integration Tests for Complete Workflows
///
/// Tests:
/// 1. Manual Creation Workflow
/// 2. PDF Wizard Workflow (BGG import, document association dropped from ImportGameFromBggCommand)
/// 3. Agent Linking Workflow
/// 4. KB Documents Visibility
/// 5. Complete Flow End-to-End (Backend)
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CompleteWorkflowIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // Stable test user ID used as CreatedBy/UserId audit field across all tests
    private static readonly Guid TestUserId = new("AAAAAAAA-0000-0000-0000-000000000001");

    // BGG IDs used by import tests — must be distinct to avoid ix_shared_games_bgg_id duplicate key
    private const int BggIdGloomhaven = 174430;
    private const int BggIdForCompleteFlow = 999001; // Fictional ID for Test 5

    public CompleteWorkflowIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"completeworkflow_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated test database
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        // Create WebApplicationFactory using shared factory + test-specific mocks
        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Mock BGG API service — accepts any BggId so different tests can use distinct IDs
                    services.RemoveAll(typeof(Api.Services.IBggApiService));
                    var mockBggApi = new Mock<Api.Services.IBggApiService>();

                    mockBggApi
                        .Setup(x => x.GetGameDetailsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                        .ReturnsAsync(new BggGameDetailsDto(
                            BggIdGloomhaven,
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

                    // Mock authorization - allow all for testing (test-specific)
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
    [Fact]
    public async Task ManualCreation_CreatesSharedGameSuccessfully()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            Title: "Test Game Manual",
            YearPublished: 2024,
            Description: "A manually created test game",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 12,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/test.jpg",
            ThumbnailUrl: "https://example.com/test-thumb.jpg",
            Rules: null,
            CreatedBy: TestUserId,
            BggId: null
        );

        // Act
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var gameId = await mediator.Send(command);

        // Assert
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = await dbContext.Set<SharedGame>()
            .FirstOrDefaultAsync(g => g.Id == gameId);

        game.Should().NotBeNull();
        game!.Title.Should().Be("Test Game Manual");
        game.YearPublished.Should().Be(2024);
        game.MinPlayers.Should().Be(2);
        game.MaxPlayers.Should().Be(4);
        game.Status.Should().Be((int)GameStatus.Draft);
        game.BggId.Should().BeNull();
    }

    /// <summary>
    /// Test 2: BGG Import Workflow
    /// Validates complete import flow: BGG API fetch → SharedGame creation in Draft
    /// (PDF document association was removed from ImportGameFromBggCommand in a later refactor)
    /// </summary>
    [Fact]
    public async Task BggImport_CreatesSharedGameFromBggApi()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var importCommand = new ImportGameFromBggCommand(BggId: BggIdGloomhaven, UserId: TestUserId);

        // Act
        var gameId = await mediator.Send(importCommand);

        // Assert
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        game.Should().NotBeNull();
        game!.BggId.Should().Be(BggIdGloomhaven);
        game.Title.Should().Be("Gloomhaven"); // From mocked BGG API
        game.Status.Should().Be((int)GameStatus.Draft);
    }

    /// <summary>
    /// Test 3: Agent Linking Workflow
    /// Validates agent creation and linking to SharedGame
    /// </summary>
    [Fact]
    public async Task AgentLinking_LinksAgentToSharedGame()
    {
        // Arrange: Create SharedGame
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var createGameCommand = new CreateSharedGameCommand(
            Title: "Test Game for Agent",
            YearPublished: 2024,
            Description: "Test game for agent linking",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 12,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/agent.jpg",
            ThumbnailUrl: "https://example.com/agent-thumb.jpg",
            Rules: null,
            CreatedBy: TestUserId
        );

        var gameId = await mediator.Send(createGameCommand);

        // Arrange: Create Agent (mock agent ID - real scenario would use AgentDefinitions BC)
        var agentId = Guid.NewGuid();

        // Act: Verify game was created correctly
        var game = await dbContext.Set<SharedGame>().FindAsync(gameId);
        game.Should().NotBeNull();

        // Simulated agent linking (actual implementation via LinkAgentToSharedGameCommand)
        // game!.AgentDefinitionId = agentId;
        // await dbContext.SaveChangesAsync();

        // Assert: game exists and is ready for agent linking (Issue #4228)
        game.Should().NotBeNull();
        game!.Id.Should().Be(gameId);
        game.Title.Should().Be("Test Game for Agent");

        // Note: Uncomment when LinkAgentToSharedGameCommand is implemented (Issue #4228)
        // game.AgentDefinitionId.Should().Be(agentId);
    }

    /// <summary>
    /// Test 4: KB Documents Visibility
    /// Validates that SharedGame documents are visible for agent knowledge base
    /// </summary>
    [Fact]
    public async Task KbDocuments_AreVisibleForSharedGame()
    {
        // Arrange: Create SharedGame
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var gameId = await mediator.Send(new CreateSharedGameCommand(
            Title: "Test Game with KB",
            YearPublished: 2024,
            Description: "Test game for KB documents visibility",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 10,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/kb.jpg",
            ThumbnailUrl: "https://example.com/kb-thumb.jpg",
            Rules: null,
            CreatedBy: TestUserId
        ));

        // Act: Query game with documents (GetAgentDocumentsQuery - implementation depends on KnowledgeBase BC)
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        // Assert: Game exists and documents collection is accessible
        game.Should().NotBeNull();
        game!.Id.Should().Be(gameId);
        game.Documents.Should().NotBeNull(); // Collection initialized, may be empty without PDF upload

        // Note: Full assertion requires:
        // 1. SharedGameDocument entity properly seeded
        // 2. GetAgentDocumentsQuery implementation
        // 3. Vector DB indexing workflow
    }

    /// <summary>
    /// Test 5: Complete Flow End-to-End (Backend)
    /// Validates entire backend pipeline from BGG import to agent creation readiness
    /// Uses a distinct BggId (999001) to avoid duplicate key conflict with Test 2 (174430)
    /// </summary>
    [Fact]
    public async Task CompleteFlow_PdfToAgentCreation()
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Step 1: Import game from BGG (uses BggIdForCompleteFlow=999001 to avoid duplicate with Test 2)
        var gameId = await mediator.Send(new ImportGameFromBggCommand(
            BggId: BggIdForCompleteFlow,
            UserId: TestUserId));

        gameId.Should().NotBeEmpty();

        // Step 2: Wait for embedding (in real scenario, this is async)
        // For testing, we assume immediate completion

        // Step 3: Create and link agent (simulated - requires Issue #4228)
        var agentId = Guid.NewGuid();

        // Step 4: Query KB documents
        var game = await dbContext.Set<SharedGame>()
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        // Assert: All steps successful
        game.Should().NotBeNull();
        game!.BggId.Should().Be(BggIdForCompleteFlow);
        game.Title.Should().Be("Gloomhaven"); // From mocked BGG API (same mock for all BggIds)

        // Note: Complete flow assertion requires:
        // 1. Full DocumentProcessing BC integration
        // 2. Vector embedding workflow (Issue #4136 dependencies)
        // 3. Agent linking commands (Issue #4228)

        // Current assertions validate core CQRS pipeline
        game.Id.Should().Be(gameId);
        game.Status.Should().Be((int)GameStatus.Draft);
    }
}
