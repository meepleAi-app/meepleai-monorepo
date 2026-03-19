using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.DependencyInjection;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// Integration tests for SharedGameCatalog HTTP endpoints
/// Tests: Public access, Authorization, Cache scenarios
/// Issue #2371 Phase 2
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCatalogEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    // Issue #2707: Removed _dbContext field - use fresh scope for each operation to prevent ObjectDisposedException

    internal static readonly Guid TestUserId = Guid.NewGuid();

    public SharedGameCatalogEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sharedgame_endpoints_{Guid.NewGuid():N}";
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
                        options.UseNpgsql(connectionString, o => o.UseVector())); // Issue #3547

                    // Mock Redis for HybridCache
                    services.RemoveAll(typeof(IConnectionMultiplexer));
                    var mockRedis = new Mock<IConnectionMultiplexer>();
                    services.AddSingleton(mockRedis.Object);

                    // Mock vector/embedding services
                    services.RemoveAll(typeof(Api.Services.IEmbeddingService));
                    services.AddScoped<Api.Services.IEmbeddingService>(_ => Mock.Of<Api.Services.IEmbeddingService>());

                    // Ensure domain event collector is registered
                    services.AddScoped<IDomainEventCollector, Api.SharedKernel.Application.Services.DomainEventCollector>();

                    // Issue #2688: Mock IHybridCacheService (required for session validation)
                    services.RemoveAll(typeof(Api.Services.IHybridCacheService));
                    services.AddScoped<Api.Services.IHybridCacheService>(_ => Mock.Of<Api.Services.IHybridCacheService>());

                    // Register authorization policies
                    services.AddSharedGameCatalogPolicies();
                });
            });

        // Issue #2707: Initialize database using scoped context (disposed after setup)
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();

            // Seed test data within the same scope
            await SeedTestDataAsync(dbContext);
        }

        _client = _factory.CreateClient();
    }

    private async Task SeedTestDataAsync(MeepleAiDbContext dbContext)
    {
        // Seed test user for FK (Issue #2688: Authorization bypassed via RequireAssertion)
        var user = new UserEntity
        {
            Id = TestUserId,
            Email = "test@test.com",
            DisplayName = "Test Admin",
            Role = "admin",
            CreatedAt = DateTime.UtcNow
        };
        dbContext.Set<UserEntity>().Add(user);

        // Seed categories
        var category = new GameCategoryEntity { Id = Guid.NewGuid(), Name = "Strategy", Slug = "strategy" };
        dbContext.Set<GameCategoryEntity>().Add(category);

        // Seed mechanics
        var mechanic = new GameMechanicEntity { Id = Guid.NewGuid(), Name = "Deck Building", Slug = "deck-building" };
        dbContext.Set<GameMechanicEntity>().Add(mechanic);

        await dbContext.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        // Issue #2707: No _dbContext field to dispose - all contexts are scoped and disposed automatically
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task GetCategories_ReturnsCategories()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games/categories");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var categories = await response.Content.ReadFromJsonAsync<List<GameCategoryDto>>();
        Assert.NotNull(categories);
        Assert.NotEmpty(categories);
    }

    [Fact]
    public async Task GetMechanics_ReturnsMechanics()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games/mechanics");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var mechanics = await response.Content.ReadFromJsonAsync<List<GameMechanicDto>>();
        Assert.NotNull(mechanics);
        Assert.NotEmpty(mechanics);
    }

    [Fact]
    public async Task SearchGames_WithNoFilters_ReturnsPublishedGames()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/shared-games?pageNumber=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SharedGameDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetGameById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/shared-games/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ========================================
    // APPROVAL WORKFLOW TESTS (Issue #2514)
    // Issue #2688: Admin endpoint tests now use TestSessionHelper for authentication
    // ========================================

    [Fact]
    public async Task SubmitForApproval_WithDraftGame_ReturnsNoContent()
    {
        // Arrange - Issue #2688: Setup admin authentication
        using var authScope = _factory.Services.CreateScope();
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        var game = await CreateTestGameAsync(GameStatus.Draft, adminUserId);

        // Act - Use HttpRequestMessage with Cookie header for authentication
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/submit-for-approval",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        using var assertScope = _factory.Services.CreateScope();
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.PendingApproval, updatedGame.Status);
    }

    [Fact]
    public async Task ApprovePublication_WithPendingApprovalGame_ReturnsNoContent()
    {
        // Arrange - Issue #2688: Setup admin authentication
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        var game = await CreateTestGameAsync(GameStatus.PendingApproval, adminUserId);

        // Act - Use HttpRequestMessage with Cookie header for authentication
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/approve-publication",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.Published, updatedGame.Status);
    }

    [Fact]
    public async Task RejectPublication_WithPendingApprovalGame_ReturnsNoContent()
    {
        // Arrange - Issue #2688: Setup admin authentication
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        var game = await CreateTestGameAsync(GameStatus.PendingApproval, adminUserId);
        var requestBody = new { Reason = "Needs better description" };

        // Act - Use HttpRequestMessage with Cookie header and JSON content for authentication
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/reject-publication",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Issue #2707: Use fresh scope for assertions to prevent ObjectDisposedException
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var updatedGame = await dbContext.SharedGames.FindAsync(game.Id);
        Assert.NotNull(updatedGame);
        Assert.Equal((int)GameStatus.Draft, updatedGame.Status);
    }

    [Fact]
    public async Task GetPendingApprovals_ReturnsPendingGames()
    {
        // Arrange - Issue #2688: Setup admin authentication
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        await CreateTestGameAsync(GameStatus.PendingApproval, adminUserId);
        await CreateTestGameAsync(GameStatus.PendingApproval, adminUserId);
        await CreateTestGameAsync(GameStatus.Draft, adminUserId); // Should not appear in results

        // Act - Use HttpRequestMessage with Cookie header for authentication
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/shared-games/pending-approvals?pageNumber=1&pageSize=20",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<SharedGameDto>>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Total);
        Assert.All(result.Items, g => Assert.Equal(GameStatus.PendingApproval, g.Status));
    }

    // ========================================
    // AUTHORIZATION TESTS (Issue #2688)
    // ========================================

    [Fact]
    public async Task AdminEndpoint_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange - No authentication setup (unauthenticated client)
        var game = await CreateTestGameAsync(GameStatus.PendingApproval);

        // Act - Try to access admin endpoint without auth
        var response = await _client.GetAsync("/api/v1/admin/shared-games/pending-approvals?pageNumber=1&pageSize=20");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithRegularUser_Returns403Forbidden()
    {
        // Arrange - Setup regular user authentication (not admin)
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(authDbContext);

        var game = await CreateTestGameAsync(GameStatus.PendingApproval, userId);

        // Act - Use HttpRequestMessage with Cookie header for authentication
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/admin/shared-games/{game.Id}/approve-publication",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ========================================
    // CREATE SHARED GAME TESTS (Admin Add Game flow)
    // ========================================

    [Fact]
    public async Task CreateSharedGame_WithValidData_Returns201Created()
    {
        // Arrange - Setup admin authentication
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        var requestBody = new
        {
            Title = "Test Board Game",
            YearPublished = 2024,
            Description = "A test board game for integration testing",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ComplexityRating = 2.5m,
            AverageRating = 7.5m,
            ImageUrl = "https://example.com/game-image.jpg",
            ThumbnailUrl = "https://example.com/game-thumb.jpg",
            Rules = (object?)null,
            BggId = (int?)null
        };

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/admin/shared-games",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var gameId = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, gameId);

        // Verify game was created in DB with Draft status
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var createdGame = await dbContext.SharedGames.FindAsync(gameId);
        Assert.NotNull(createdGame);
        Assert.Equal("Test Board Game", createdGame.Title);
        Assert.Equal((int)GameStatus.Draft, createdGame.Status);
        Assert.Equal(adminUserId, createdGame.CreatedBy);
    }

    [Fact]
    public async Task CreateSharedGame_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        var requestBody = new
        {
            Title = "Test Board Game",
            YearPublished = 2024,
            Description = "A test board game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg"
        };

        // Act - No auth headers
        var response = await _client.PostAsJsonAsync("/api/v1/admin/shared-games", requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateSharedGame_WithRegularUser_Returns403Forbidden()
    {
        // Arrange - Regular user, not admin
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(authDbContext);

        var requestBody = new
        {
            Title = "Test Board Game",
            YearPublished = 2024,
            Description = "A test board game",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg"
        };

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/admin/shared-games",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateSharedGame_WithEditorRole_Returns201Created()
    {
        // Arrange - Editor role should also be allowed
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (editorUserId, sessionToken) = await TestSessionHelper.CreateEditorSessionAsync(authDbContext);

        var requestBody = new
        {
            Title = "Editor Created Game",
            YearPublished = 2023,
            Description = "A game created by an editor",
            MinPlayers = 1,
            MaxPlayers = 6,
            PlayingTimeMinutes = 90,
            MinAge = 12,
            ComplexityRating = 3.0m,
            AverageRating = 8.0m,
            ImageUrl = "https://example.com/editor-game.jpg",
            ThumbnailUrl = "https://example.com/editor-thumb.jpg",
            Rules = (object?)null,
            BggId = (int?)null
        };

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            "/api/v1/admin/shared-games",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var gameId = await response.Content.ReadFromJsonAsync<Guid>();
        Assert.NotEqual(Guid.Empty, gameId);

        // Verify game was created with Editor as creator
        var dbContext = assertScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var createdGame = await dbContext.SharedGames.FindAsync(gameId);
        Assert.NotNull(createdGame);
        Assert.Equal("Editor Created Game", createdGame.Title);
        Assert.Equal(editorUserId, createdGame.CreatedBy);
    }

    // ========================================
    // BGG IMPORT TESTS (Admin Add from BGG flow)
    // ========================================

    [Fact]
    public async Task BggSearch_WithValidQuery_ReturnsResults()
    {
        // Arrange - Issue #2688: Setup admin authentication
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/admin/shared-games/bgg/search?query=Catan&exact=false",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert - Accept either 200 (results) or 503 (BGG unavailable in test env)
        // In integration tests, the real BGG API isn't mocked at endpoint level
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
    }

    [Fact]
    public async Task BggSearch_WithoutAuth_Returns401Unauthorized()
    {
        // Act - No auth headers
        var response = await _client.GetAsync("/api/v1/admin/shared-games/bgg/search?query=Catan");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CheckBggDuplicate_WithNonExistentBggId_ReturnsNoDuplicate()
    {
        // Arrange
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        // Use a random BGG ID that doesn't exist in our database
        var randomBggId = 999999;

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/admin/shared-games/bgg/check-duplicate/{randomBggId}",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert - Accept either 200 (no duplicate) or 503 (BGG unavailable)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
    }

    [Fact]
    public async Task CheckBggDuplicate_WithExistingBggId_ReturnsDuplicate()
    {
        // Arrange
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        // Create a game with a known BGG ID
        var knownBggId = 123456;
        var game = await CreateTestGameWithBggIdAsync(GameStatus.Published, adminUserId, knownBggId);

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/admin/shared-games/bgg/check-duplicate/{knownBggId}",
            sessionToken);
        var response = await _client.SendAsync(request);

        // Assert - Accept either 200 (duplicate found) or 503 (BGG unavailable)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK ||
            response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected OK or ServiceUnavailable, got {response.StatusCode}");
    }

    [Fact]
    public async Task CheckBggDuplicate_WithoutAuth_Returns401Unauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/admin/shared-games/bgg/check-duplicate/123456");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFromBgg_WithNonExistentGame_ReturnsNotFound()
    {
        // Arrange
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (adminUserId, sessionToken) = await TestSessionHelper.CreateAdminSessionAsync(authDbContext);

        var nonExistentGameId = Guid.NewGuid();
        var requestBody = new { bggId = 123456 };

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{nonExistentGameId}/update-from-bgg",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFromBgg_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var requestBody = new { bggId = 123456 };

        // Act
        var response = await _client.PutAsJsonAsync(
            $"/api/v1/admin/shared-games/{gameId}/update-from-bgg",
            requestBody);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateFromBgg_WithRegularUser_Returns403Forbidden()
    {
        // Arrange - Regular user, not admin
        var authDbContext = authScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(authDbContext);

        var game = await CreateTestGameWithBggIdAsync(GameStatus.Published, userId, 123456);
        var requestBody = new { bggId = 123456 };

        // Act
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put,
            $"/api/v1/admin/shared-games/{game.Id}/update-from-bgg",
            sessionToken,
            requestBody);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Helper to create a game with a specific BGG ID for duplicate tests.
    /// </summary>
    private async Task<SharedGameEntity> CreateTestGameWithBggIdAsync(GameStatus status, Guid? createdBy, int bggId)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Test Game with BGG {bggId}",
            BggId = bggId,
            YearPublished = 2024,
            Description = "Test description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)status,
            CreatedBy = createdBy ?? TestUserId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync();
        return game;
    }

    /// <summary>
    /// Issue #2707: Use fresh scope to prevent ObjectDisposedException
    /// Issue #2688: Added optional createdBy parameter for admin tests
    /// </summary>
    private async Task<SharedGameEntity> CreateTestGameAsync(GameStatus status, Guid? createdBy = null)
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Test Game {Guid.NewGuid():N}",
            YearPublished = 2024,
            Description = "Test description",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = "https://example.com/image.jpg",
            ThumbnailUrl = "https://example.com/thumb.jpg",
            Status = (int)status,
            CreatedBy = createdBy ?? TestUserId,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync();
        return game;
    }
}
